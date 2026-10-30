using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using FirstPerson.Core;
using FirstPerson.Net;
using FirstPerson.Pad;
using FirstPerson.Towns;
using FirstPerson.Vr;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal sealed class Hud
	{
		private struct DropGeometry
		{
			public Rect Panel;

			public Rect View;

			public Rect Bar;

			public float ItemH;

			public float ContentH;

			public float Max;

			public bool Up;
		}

		private sealed class CardText
		{
			public string Line;

			public float Until;

			public bool Alive;

			public Actor Host;

			private int _away = int.MinValue;

			private string _awayText;

			public string AwayOf(int tiles)
			{
				if (tiles != _away)
				{
					_away = tiles;
					_awayText = tiles + " away";
				}
				return _awayText;
			}
		}

		private sealed class InvDrag
		{
			public int Cell = -1;

			public string Key;

			public EquipmentType Worn;

			public Sprite Icon;

			public Vector2 From;

			public int Frame;

			public bool Moving;
		}

		private struct MpTask
		{
			public Vector2 At;

			public string Title;

			public string Line;

			public Color Color;

			public Quest Quest;

			public JournalEntry Entry;

			public bool Picked;
		}

		private sealed class NumberLook
		{
			public float Seen;

			public float Side;

			public float Weight;

			public float PunchAt = -9f;

			public int Amount;

			public int Px;

			public bool Kill;

			public bool Placed;

			public bool Word;

			public bool Head;

			public Vector3 At;
		}

		private struct PurseChange
		{
			public int Amount;

			public float At;
		}

		private sealed class MeterState
		{
			public Actor For;

			public Actor You;

			public float Until;

			public bool Romance;

			public bool Courting;

			public bool Lovers;

			public bool Taken;

			public int Affection;

			public int Reach;

			public int Done;

			public int Wanted;

			public int Opinion;

			public int Mood;

			public int Devotion;

			public string Partner;
		}

		private struct ScenePip
		{
			public Actor Actor;

			public Vector2 S;

			public Vector2 Body;

			public bool Orderable;
		}

		private sealed class YardLook
		{
			public City City;

			public float At;

			public bool Docks;

			public readonly int[] Afloat = new int[3];

			public readonly int[] Cap = new int[3];

			public readonly int[] Ordered = new int[3];

			public int Building;

			public float Soonest;
		}

		private enum Vital
		{
			Health,
			Food,
			Stamina
		}

		private sealed class NumberText
		{
			private int _a;

			private int _b;

			private string _text;

			public string Of(int a, int b = int.MinValue)
			{
				if (_text == null || a != _a || b != _b)
				{
					_a = a;
					_b = b;
					_text = ((b == int.MinValue) ? a.ToString() : (a + "/" + b));
				}
				return _text;
			}
		}

		private struct Meter
		{
			public float Shown;

			public float Trail;

			public float HoldUntil;

			public bool Started;
		}

		private readonly WorldBoxMod _mod;

		private Texture2D _white;

		private Texture2D _vignette;

		private GUIStyle _label;

		private GUIStyle _small;

		private GUIStyle _center;

		private GUIStyle _big;

		private GUIStyle _float;

		private readonly System.Random _shakeRandom = new System.Random();

		internal bool NoWorld;

		internal bool VrView;

		private const int FrameStrips = 16;

		private const int StretchStrips = 3;

		private const int MeshStrips = 48;

		private static readonly int[] BandSteps = new int[3] { 4, 12, 4 };

		private const int MaxGridRows = 20;

		private readonly float[] _stripX = new float[49];

		private readonly float[] _stripS = new float[49];

		private readonly float[] _bandY = new float[4];

		private readonly float[] _gridY = new float[21];

		private readonly int[] _rowAt = new int[4];

		private readonly float[,] _gridC = new float[21, 49];

		private readonly float[,] _gridR = new float[21, 49];

		private Material _frameMesh;

		private bool _frameMeshTried;

		private Matrix4x4 _frameRoll = Matrix4x4.identity;

		private readonly Dictionary<string, Sprite> _resourceIcons = new Dictionary<string, Sprite>();

		private GUIStyle _button;

		private GUIStyle _rowName;

		private GUIStyle _rowCost;

		private GUIStyle _tabOn;

		private GUIStyle _tabOff;

		private Texture2D _heart;

		private const string VrHint = "Trigger attack, gather   L-trigger guard   Grip talk, use, enter   L-grip reload   B ability, back\nL-stick walk, click to run   R-stick turn, click for binoculars   A jump   Hold-A quick menu\nX inventory   Y map   Menu settings, help   Wrist turn your left wrist up for the minimap, tasks and you";

		private string[] _helpLines;

		private string _helpFrom;

		private Meter _targetMeter;

		private Actor _targetFor;

		private GUIStyle _smallRight;

		private GUIStyle _wrap;

		private GUIStyle _titleStyle;

		private GUIStyle _hintRight;

		private GUIStyle _headInk;

		private GUIStyle _bodyInk;

		private GUIStyle _wrapInk;

		private GUIStyle _wrapSmallInk;

		private GUIStyle _centerInk;

		private GUIStyle _centerCream;

		private GUIStyle _buttonOff;

		private GUIStyle _rowCostRight;

		private readonly Dictionary<AbilityIcon, Texture2D> _abilityIcons = new Dictionary<AbilityIcon, Texture2D>();

		private float _readyFlash;

		private bool _wasCooling;

		private static readonly string[] BombRows = new string[12]
		{
			".......YF...", "......F.Y...", ".....XF.....", "...XXXXX....", "..XBBBBBX...", ".XBHBBBBBX..", ".XBHBBBBBX..", ".XBBBBBBBX..", ".XBBBBBBDX..", "..XBBBBDX...",
			"...XXXXX....", "............"
		};

		private static readonly string[] BoneRows = new string[12]
		{
			"............", ".XX.........", "XWWX........", "XWWWX.......", ".XWWWX......", "..XWWWX.....", "...XWWWX....", "....XWWWX...", ".....XWWWX..", "......XWWWX.",
			".......XWWX.", "........XX.."
		};

		private static readonly string[] RockRows = new string[12]
		{
			"............", "............", "....XXXX....", "...XGGHGX...", "..XGGGHHGX..", ".XGGGGGGGGX.", ".XGGGGGGGDX.", ".XGGGGGGDDX.", "..XGGGGDDX..", "...XXXXXX...",
			"............", "............"
		};

		private static readonly string[] OrbRows = new string[12]
		{
			"............", "....XXXX....", "...XCCCCX...", "..XCHHCCCX..", ".XCHHCCCCCX.", ".XCHCCCCCCX.", ".XCCCCCCCDX.", ".XCCCCCCCDX.", "..XCCCCCDX..", "...XCCDDX...",
			"....XXXX....", "............"
		};

		private static readonly string[] FlameRows = new string[12]
		{
			".....X......", "....XRX.....", "....XRX..X..", "...XRRRX.XX.", "..XRRYRRXRX.", "..XRYYYRRRX.", ".XRRYWYYRRX.", ".XRYYWWYYRX.", ".XRYWWWWYRX.", ".XRYYWWYYRX.",
			"..XRRYYRRX..", "...XXXXXX..."
		};

		private static readonly string[] FlakeRows = new string[12]
		{
			".....X......", "..X..C..X...", "...C.C.C....", "....CCC.....", ".X..CWC..X..", "XCCCWWWCCCX.", ".X..CWC..X..", "....CCC.....", "...C.C.C....", "..X..C..X...",
			".....X......", "............"
		};

		private static readonly string[] SkullRows12 = new string[12]
		{
			"............", "...XXXXXX...", "..XWWWWWWX..", ".XWWWWWWWWX.", ".XWXXWWXXWX.", ".XWXGWWXGWX.", ".XWWWXXWWWX.", "..XWWWWWWX..", "...XWXWXWX..", "...XWXWXWX..",
			"....XXXXX...", "............"
		};

		private static readonly string[] CrossRows = new string[12]
		{
			"............", "....XXXX....", "....XCCX....", "....XCCX....", ".XXXXCCXXXX.", ".XCCCCCCCCX.", ".XCCCCCCCCX.", ".XXXXCCXXXX.", "....XCCX....", "....XCCX....",
			"....XXXX....", "............"
		};

		private static readonly string[] LeafRows = new string[12]
		{
			"..........X.", "........XXCX", "......XXCCCX", "....XXCCCHCX", "...XCCCCHCX.", "..XCCCCHCCX.", "..XCCCHCCX..", ".XCCCHCCX...", ".XCCHCCX....", ".XDHXXX.....",
			"XDX.........", "X..........."
		};

		private static readonly string[] ShieldRows = new string[12]
		{
			"............", ".XXXXXXXXXX.", ".XGGGGGGGGX.", ".XGHHGGGGGX.", ".XGHGGGGGGX.", ".XGGGGGGGGX.", ".XGGGGGGGDX.", "..XGGGGGDX..", "..XGGGGGDX..", "...XGGGDX...",
			"....XGDX....", ".....XX....."
		};

		private static readonly string[] ShoutRows = new string[12]
		{
			"............", "........X...", ".....X...X..", "..XX..X...X.", ".XCCX..X..X.", ".XCCCX.X..X.", ".XCCCX.X..X.", ".XCCX..X..X.", "..XX..X...X.", ".....X...X..",
			"........X...", "............"
		};

		private static readonly string[] FistRows = new string[12]
		{
			"............", "...XXXXXX...", "..XCHCHCHX..", "..XCHCHCHX..", ".XXCCCCCCX..", "XCCXCCCCCX..", "XCCCCCCCCX..", ".XCCCCCCDX..", "..XCCCCDX...", "...XCCCDX...",
			"...XXXXXX...", "............"
		};

		private static readonly string[] DashRows = new string[12]
		{
			"............", "............", "......XX....", "......XCX...", "XX.XXXXCCX..", ".XCCCCCCCCX.", "..XCCCCCCCCX", ".XCCCCCCCCX.", "XX.XXXXCCX..", "......XCX...",
			"......XX....", "............"
		};

		private static readonly string[] FangRows = new string[12]
		{
			"............", ".XXXXXXXXXX.", ".XWWWWWWWWX.", "..XWWXXWWX..", "..XWWXXWWX..", "...XWX.XWX..", "...XWX.XWX..", "....X...X...", "....C...C...", "....C...C...",
			"...XCX.XCX..", "....X...X..."
		};

		private const int TimerBeads = 16;

		private Texture2D _eyecups;

		private float _eyecupsAspect;

		private GUIStyle _binoName;

		private GUIStyle _binoSmall;

		private static readonly Color StopSteel = new Color(0.78f, 0.86f, 0.97f);

		private static readonly Color StopPale = new Color(0.88f, 0.9f, 0.92f);

		private static readonly Color StopRim = new Color(0.05f, 0.07f, 0.1f);

		internal bool StopMarkerDrawn;

		internal bool GuardMarkerDrawn;

		internal bool GuardWordDrawn;

		private static readonly Color GuardSteel = new Color(0.5f, 0.88f, 1f);

		private static readonly Color GuardBrokenRed = new Color(1f, 0.36f, 0.26f);

		internal bool GuardUpDrawn;

		private const float CaptureReach = 30f;

		private City _capCity;

		private Kingdom _capBy;

		private float _capTicks;

		private float _capShown;

		private float _capAlpha;

		private float _capLookedAt = -10f;

		private string _capText;

		private Color _capColor;

		private bool _capLosing;

		internal static string CaptureLineForTest;

		internal static float CaptureFillForTest = -1f;

		private Sprite _demolishIcon;

		private bool _demolishLooked;

		private float _dismantleShown = -1f;

		private Building _dismantleFor;

		internal static float DismantleFillForTest = -1f;

		private static readonly string[] ShelfNames = new string[4] { "Weapons", "Armour", "Jewellery", "Traits" };

		private static readonly string[] ShelfIcons = new string[4] { "iconDamage", "iconArmor", "iconShiny", "iconEditTrait" };

		private static List<EquipmentAsset>[] _gear;

		private static List<ActorTrait> _traits;

		private string _shelfHover;

		private static readonly string[] CheatNames = new string[4] { "+100 gold", "+1000 gold", "Reputation +500", "Reputation -500" };

		internal bool CompassReadoutShown;

		internal bool CompassMarkShown;

		private Vector2 _talkMouse;

		internal static float TalkCardTop;

		private static readonly Color RomanceText = new Color(1f, 0.62f, 0.78f);

		private static readonly Color QuestText = new Color(1f, 0.82f, 0.36f);

		private static readonly Color RiskText = new Color(1f, 0.5f, 0.38f);

		private static readonly Color BackText = new Color(0.78f, 0.78f, 0.74f);

		private int _talkFirst;

		private int _talkPicked = -1;

		private int _talkSets = -1;

		private string _cardRel;

		private string _cardSuffix;

		private string _cardLine;

		internal static int TalkRowsShown;

		internal static int TalkFirstRow;

		private Vector2 _boardScroll;

		private static readonly string[] ShareLabels = new string[4] { "¼", "½", "¾", "All" };

		internal Rect WarMapLinkRectForTest;

		internal Rect CouncilLinkRectForTest;

		private static readonly float[] Shares = new float[4] { 0.25f, 0.5f, 0.75f, 1f };

		private static readonly Color StatUp = new Color(0.45f, 0.9f, 0.35f);

		private static readonly Color StatDown = new Color(1f, 0.38f, 0.3f);

		private static readonly Color CardFaint = new Color(0.62f, 0.62f, 0.66f);

		private static readonly string[] CardStats = new string[12]
		{
			"damage", "armor", "speed", "attack_speed", "critical_chance", "range", "accuracy", "stamina", "mana", "targets",
			"projectiles", "area_of_effect"
		};

		private const float RevealPop = 0.22f;

		private const float RevealFly = 0.45f;

		private Rect _bagArea;

		private readonly Dictionary<SettingsMenu.Row, Rect> _dropAt = new Dictionary<SettingsMenu.Row, Rect>();

		private readonly Dictionary<string, Rect> _dropOnScreen = new Dictionary<string, Rect>();

		private Rect[] _dropItemsOnScreen = new Rect[0];

		private Vector2 _pageOrigin;

		private Vector2 _dropMouse;

		private bool _dropHeld;

		private bool _dropBarHeld;

		private int _fmButton = -1;

		private bool _fmDragged;

		private Vector2 _fmDragFrom;

		private Vector2 _fmCentreFrom;

		internal Rect FamilyMapRectForTest;

		private static readonly Color FamilyGreen = new Color(0.45f, 0.95f, 0.4f);

		private static readonly Color HungerOrange = new Color(1f, 0.66f, 0.25f);

		private static readonly Color DangerRed = new Color(1f, 0.3f, 0.24f);

		private static readonly string[] PeopleTabs = new string[2] { "Starred", "Contacts" };

		private static readonly string[] PeopleTabsMp = new string[3] { "Starred", "Contacts", "Players" };

		private static readonly string[] MetSorts = new string[3] { "Newest", "Talked", "Name" };

		private readonly Dictionary<long, CardText> _cardTexts = new Dictionary<long, CardText>();

		private float _fpsSmooth;

		private int _fpsFrame = -1;

		private int _fpsShown;

		private int _fpsTenths;

		private string _fpsText;

		internal bool FlyMinimapDrawn;

		private GUIStyle _gameField;

		private float _gameFieldFor = -1f;

		internal static int FieldFocusFrame = -10;

		private bool _listMouseIn;

		internal Rect HelpRectForTest;

		private Meter _hullMeter;

		private Actor _hullOf;

		private readonly List<KeyValuePair<string, bool>> _growNeeds = new List<KeyValuePair<string, bool>>();

		private static readonly Color HomeGreen = new Color(0.45f, 1f, 0.45f, 1f);

		internal Vector2? HomeMarkDrawnForTest;

		private static Sprite _homeIcon;

		private static bool _homeIconTried;

		private const float HotbarFade = 0.6f;

		private const float HotbarNameFor = 1.6f;

		private int _hotbarSig;

		private int _hotbarChosen = -1;

		private Actor _hotbarOf;

		private float _hotbarNameAt = -10f;

		private readonly List<Vector2> _huntMarks = new List<Vector2>();

		private static Texture2D _hurtArc;

		internal int HurtMarksDrawn;

		internal float HurtMarkAngle;

		private static readonly EquipmentType[] WornLeft = new EquipmentType[3]
		{
			EquipmentType.Helmet,
			EquipmentType.Armor,
			EquipmentType.Boots
		};

		private static readonly EquipmentType[] WornRight = new EquipmentType[3]
		{
			EquipmentType.Weapon,
			EquipmentType.Amulet,
			EquipmentType.Ring
		};

		internal static Func<Rect, Actor, string> SkillsPage;

		private static readonly string[] CraftGroups = new string[4] { "Tools", "Weapons", "Armour", "Supplies" };

		private const float QuickAlpha = 0.55f;

		private InvDrag _invDrag;

		private int _invDrawnFrame = -10;

		private Rect _invWin;

		private int _bagCellCount;

		private readonly List<Rect> _bagCells = new List<Rect>();

		private readonly Dictionary<EquipmentType, Rect> _wornRects = new Dictionary<EquipmentType, Rect>();

		private readonly List<KeyValuePair<string, int>> _bagNow = new List<KeyValuePair<string, int>>();

		private int _bagNowFrame = -1;

		private Actor _bagNowOf;

		internal static Vector2? BagMouseForTest;

		private readonly List<Ledger.Row> _ledgerShown = new List<Ledger.Row>();

		private Vector2 _magicScroll;

		private List<Ability> _magicSpells;

		private List<string> _magicEarned;

		private List<ActorTrait> _magicBorn;

		private List<Moves.Move> _magicMoves;

		private Actor _magicFor;

		private float _magicUntil;

		private static readonly Comparison<ActorTrait> ByTraitName = (ActorTrait a, ActorTrait b) => string.CompareOrdinal(Teachers.Name(a.id), Teachers.Name(b.id));

		internal Rect MapPageRectForTest;

		private int _mpButton = -1;

		private bool _mpDragged;

		private Vector2 _mpDragFrom;

		private Vector2 _mpCentreFrom;

		private string _mpPicked;

		private readonly List<MpTask> _mpTasks = new List<MpTask>();

		private static readonly Color TaskGold = new Color(0.95f, 0.74f, 0.2f);

		private static readonly Color ReadyGold = new Color(1f, 0.92f, 0.45f);

		private static readonly Color CourtPink = new Color(1f, 0.62f, 0.78f);

		private readonly List<MapTabs.Page> _mapPages = new List<MapTabs.Page>();

		internal Rect MapWindowRectForTest;

		private static Texture2D _markTex;

		private readonly List<KeyValuePair<Actor, bool>> _marked = new List<KeyValuePair<Actor, bool>>();

		private float _markedAt = -10f;

		private Actor _markedFor;

		private int _markedMode;

		internal int MarksFoe;

		internal int MarksFriend;

		private const float MarkReach = 40f;

		private const float MarkSize = 0.24f;

		private const float MarkLift = 0.12f;

		private Texture2D _mmRing;

		private Material _mmMaterial;

		private bool _mmMaterialTried;

		private const float MapRadius = 36f;

		private const float RingWidth = 3f;

		internal bool MinimapDrawn;

		internal Rect MinimapRectForTest;

		internal bool BigMapDrawn;

		private static readonly string[] ArrowRows = new string[7] { "...K...", "..KWK..", "..KWK..", ".KWWWK.", ".KWWWK.", "KWWKWWK", "KKK.KKK" };

		private readonly List<City> _mapTowns = new List<City>();

		private static readonly Color MpRed = new Color(1f, 0.45f, 0.4f);

		private readonly Dictionary<int, float> _rowSeen = new Dictionary<int, float>();

		private int _banArmed;

		private float _banArmedAt;

		private readonly Dictionary<string, string> _rawTyped = new Dictionary<string, string>();

		private static float _codeCopiedAt = -999f;

		private static string _clipCode = "";

		private static float _clipReadAt = -999f;

		private readonly List<string> _talk = new List<string>();

		private string _speaking;

		private float _speakingAt = -1f;

		private const float ChatSize = 5.2f;

		private const float ChatStays = 30f;

		private const int ChatLines = 10;

		private readonly List<KeyValuePair<City, string>> _spawnTowns = new List<KeyValuePair<City, string>>();

		private float _spawnTownsAt = -10f;

		private static Action<Rect, bool> _padSpot;

		private static bool _padLooked;

		private static readonly Color Bad = new Color(1f, 0.42f, 0.36f);

		private static readonly Color Warn = new Color(1f, 0.8f, 0.35f);

		private static readonly Color Good = new Color(0.55f, 0.9f, 0.45f);

		private Rect _availView;

		private Rect _activeView;

		private float _cardH;

		private float _cardGap;

		private static readonly Dictionary<string, KeyValuePair<float, List<ulong>>> MissingCache = new Dictionary<string, KeyValuePair<float, List<ulong>>>();

		private List<string> _publishWhy;

		private PackInfo _publishWhyFor;

		private float _publishWhyAt;

		internal bool PadMenuDrawn;

		internal readonly Rect[] PadMenuRects = new Rect[PadMenu.Items.Length];

		private Texture2D _ripple;

		internal int PingsInView;

		internal int PingsAtEdge;

		internal bool WheelDrawn;

		private string _captureFile;

		private bool _capturing;

		private static readonly Color NumberGold = new Color(1f, 0.87f, 0.38f);

		private static readonly Color NumberOrange = new Color(1f, 0.55f, 0.16f);

		private static readonly Color KillRed = new Color(0.95f, 0.2f, 0.13f);

		private static readonly Color Rim = new Color(0.1f, 0.06f, 0.04f);

		private static readonly Color GainGold = new Color(1f, 0.84f, 0.33f);

		private static readonly Color Spark = new Color(1f, 0.9f, 0.55f);

		private static readonly Color HeadRed = new Color(1f, 0.33f, 0.18f);

		private const string KillWord = "KILL";

		private const string HeadWord = "HEADSHOT";

		private static readonly Rect SkullPart = new Rect(6f, 7f, 15f, 14f);

		private readonly Dictionary<FloatingText, NumberLook> _numbers = new Dictionary<FloatingText, NumberLook>();

		private readonly List<FloatingText> _numbersGone = new List<FloatingText>();

		private float _nextSide = 1f;

		private static readonly Vector2[] RimSteps = new Vector2[8]
		{
			new Vector2(-1f, -1f),
			new Vector2(0f, -1f),
			new Vector2(1f, -1f),
			new Vector2(-1f, 0f),
			new Vector2(1f, 0f),
			new Vector2(-1f, 1f),
			new Vector2(0f, 1f),
			new Vector2(1f, 1f)
		};

		private readonly Dictionary<string, float> _slotX = new Dictionary<string, float>();

		private readonly Dictionary<string, Rect> _slotAt = new Dictionary<string, Rect>();

		private readonly List<string> _slotsGone = new List<string>();

		private float _slotsStepped = -1f;

		private float _lastLandSound = -9f;

		private int _landStreak;

		private Texture2D _glow;

		private readonly List<PurseChange> _purseChanges = new List<PurseChange>();

		private Actor _purseHost;

		private int _purseMoney;

		private float _purseShown;

		private float _purseStepped = -1f;

		private int _underGuards = -1;

		private int _underSworn = -1;

		private string _under;

		private float _menuLeft;

		private GUIStyle _questTitle;

		private GUIStyle _questLine;

		private GUIStyle _wantedStyle;

		private float _questU = -1f;

		private static Texture2D _star;

		private readonly List<KeyValuePair<Actor, bool>> _starred = new List<KeyValuePair<Actor, bool>>();

		internal int StarsGold;

		internal int StarsPale;

		private const float StarSize = 0.22f;

		private const float StarLift = 0.14f;

		private static readonly KingdomStyle[] RowStyles = new KingdomStyle[3]
		{
			KingdomStyle.IronSpikes,
			KingdomStyle.GoldWings,
			KingdomStyle.GildedSpire
		};

		internal Rect[] StyleRectsForTest = new Rect[3];

		internal int StyleHoverForTest = -1;

		private int _styleTip = -1;

		private Rect _styleRow;

		private int _styleTapped = -1;

		private float _styleTappedAt;

		internal static bool MetersForTest;

		private static readonly Color RomancePink = new Color(1f, 0.42f, 0.62f);

		private static readonly Color ReachPink = new Color(1f, 0.42f, 0.62f, 0.35f);

		private static readonly Color GoodGreen = new Color(0.45f, 0.82f, 0.3f);

		private static readonly Color BadRed = new Color(0.95f, 0.35f, 0.28f);

		private static readonly Color MoodGold = new Color(1f, 0.8f, 0.3f);

		private static readonly Color DevotionViolet = new Color(0.66f, 0.5f, 1f);

		private static readonly Color BarBack = new Color(0f, 0f, 0f, 0.55f);

		private readonly MeterState _talkMeters = new MeterState();

		private readonly MeterState _rowMeters = new MeterState();

		private Vector2 _hallScroll;

		private Vector2 _loadoutScroll;

		private string _runHover;

		private static List<string> _loadoutOrder;

		private readonly List<ScenePip> _scPips = new List<ScenePip>(512);

		private int _scButton = -1;

		private bool _scDragged;

		private bool _scGroundSet;

		private bool _scOnSky;

		private bool _scShift;

		private Vector2 _scSkyFrom;

		private Vector2 _scSkyAt;

		private Vector2 _scFrom;

		private Vector2 _scGround;

		private Vector2 _scAtFrom;

		private WarFlag _scFlag;

		private WarFlag _scHoverFlag;

		private Rect _scBar;

		private Rect _scKeys;

		internal Actor SceneHover;

		internal Rect SceneButtonRectForTest;

		internal readonly Dictionary<string, Rect> SceneEyesForTest = new Dictionary<string, Rect>();

		private static readonly string[] TabIcons = new string[8] { "iconShiny", "iconInspect", "iconWorldLaws", "icon_musicvolume", "iconPossessed", "iconCrystalSword", "iconFriendship", "icon_hammer_steel" };

		private static readonly string[] TabArt = new string[10] { "art_load", "art_frostland", "art_windmill", "art_cultures", "art_world_creation_2", "art_monolith", "art_favorite_items", "art_cultures", "art_world_creation_2", "art_knowledge" };

		private static readonly Dictionary<string, string> RowIcons = new Dictionary<string, string>
		{
			{ "Field of view", "iconInspect" },
			{ "Eye height", "iconLevels" },
			{ "Head bob", "iconWalker" },
			{ "Screen shake", "iconEarthquake" },
			{ "Mouse sensitivity", "iconFinger" },
			{ "Resolution", "icon_fullscreen" },
			{ "Draw distance", "iconTileMountains" },
			{ "Detail distance", "iconBuildings" },
			{ "Creature distance", "iconHumans" },
			{ "Palette filter", "iconPrinterSquares" },
			{ "Vertical sync", "icon_VSync" },
			{ "Renderer", "iconCybercore" },
			{ "Classic", "iconPrinterSquares" },
			{ "Language", "iconLanguage" },
			{ "Run speed", "iconSpeed" },
			{ "Others' pace", "iconPopulation" },
			{ "Time of day", "iconClock" },
			{ "Length of a day", "icon_nightlights" },
			{ "Gusts of wind", "icon_treewind" },
			{ "Draw while the game runs", "iconClockXSonic" },
			{ "Master volume", "icon_SFXvolume" },
			{ "Music", "icon_musicvolume" },
			{ "Sound effects", "icon_tech_steal_resources" },
			{ "Footsteps", "iconWalker" },
			{ "Birds and night sounds", "icon_soundambient" },
			{ "3D sound", "icon_SFXvolume" },
			{ "Voices", "iconTalk" },
			{ "Voice style", "iconTalk" },
			{ "Pose view", "iconWalker" },
			{ "3D map", "iconWorldInfo" },
			{ "Friendly fire", "iconFriendship" },
			{ "Parry timing", "iconArmor" },
			{ "Enemy wind-ups", "iconCrystalSword" },
			{ "Markers", "iconWarZones" },
			{ "Family follows you", "iconFamily" },
			{ "3D map reach", "iconTileSummit" },
			{ "Camera dive", "iconGodFinger" }
		};

		internal static readonly Rect[] TabOnScreen = new Rect[16];

		private GUIStyle _setTrack;

		private GUIStyle _setThumb;

		private float _setStylesFor = -1f;

		private bool _pageMouseIn;

		private readonly YardLook _yard = new YardLook();

		private int _smButton = -1;

		private bool _smDragged;

		private Vector2 _smDragFrom;

		private Vector2 _smCentreFrom;

		internal Rect SoldierMapRectForTest;

		private static readonly Color ArmyGold = new Color(1f, 0.84f, 0.3f, 0.95f);

		private static readonly Color OrderRed = new Color(1f, 0.32f, 0.24f);

		private static readonly Color SiteGreen = new Color(0.45f, 1f, 0.4f);

		private static readonly Color Plot = new Color(1f, 0.96f, 0.68f);

		private const int StorageCols = 6;

		private string _storageTip;

		private static Texture2D _touchDisc;

		private static Texture2D _touchRing;

		private static readonly Color TouchFill = new Color(0.08f, 0.06f, 0.05f, 0.34f);

		private static readonly Color TouchFillDown = new Color(0.91f, 0.77f, 0.47f, 0.55f);

		private static readonly Color TouchEdge = new Color(0.95f, 0.89f, 0.77f, 0.7f);

		private static readonly Color TouchEdgeLit = new Color(1f, 0.82f, 0.38f, 0.95f);

		private readonly Texture2D[] _flameTex = new Texture2D[12];

		private GUIStyle _keyStyle;

		private GUIStyle _plateTitle;

		private GUIStyle _plateBody;

		private int _vitalsFor = -1;

		private readonly NumberText _staminaText = new NumberText();

		private readonly NumberText _manaText = new NumberText();

		private readonly NumberText _bagText = new NumberText();

		private readonly NumberText _xpText = new NumberText();

		private readonly NumberText _ageText = new NumberText();

		private readonly NumberText _killsText = new NumberText();

		private readonly NumberText _levelText = new NumberText();

		private readonly NumberText _purseText = new NumberText();

		private float _enter = 1f;

		private float _panelAlpha = 1f;

		private bool _movingPass;

		private int _lastHudFrame = -100;

		private float _enterStart = -1f;

		private float _lastHudAlpha;

		private bool _leaving;

		private const float EnterSeconds = 0.65f;

		private float _nameplateBottom;

		private const float PipW = 11f;

		private const float PipH = 10f;

		private const float PipGap = 1f;

		private const float ColGap = 12f;

		private const float SidePad = 16f;

		private const float TopPad = 11f;

		private const float RowGap = 3f;

		private const float MeterH = 8f;

		private const float BottomPad = 5f;

		private float _lastHealth = -1f;

		private float _heartFlashUntil;

		private float _regenWave = -10f;

		private float _lastFood = -1f;

		private float _foodWave = -10f;

		private float _glintAt = -10f;

		private float _glintNext;

		private readonly int[] _heartState = new int[10];

		private readonly int[] _foodState = new int[10];

		private readonly float[] _heartLostAt = new float[10] { -9f, -9f, -9f, -9f, -9f, -9f, -9f, -9f, -9f, -9f };

		private Meter _staminaMeter;

		private Meter _manaMeter;

		private Actor _vitalsHost;

		private Rect _vitalsRect;

		internal static float VitalsTop;

		private float _vitalsAlpha = 1f;

		private float _bagLeft;

		private float _bagRight;

		private float _bagTop;

		private readonly Dictionary<string, float> _statusSince = new Dictionary<string, float>();

		private readonly List<string> _statusGone = new List<string>();

		private Meter _xpMeter;

		private string _role;

		private Actor _roleFor;

		private bool _roleCiv;

		private float _roleUntil;

		private static bool _bannerLogged;

		private static readonly string[] Points = new string[8] { "E", "NE", "N", "NW", "W", "SW", "S", "SE" };

		private Texture2D _dayStrip;

		private bool _stripNatural;

		private float _stripDeclination;

		private string _ageId;

		private string _ageTitle;

		private string _yearText;

		private string _monthText;

		private int _year = -1;

		private int _month = -1;

		private float _flipAt = -10f;

		private float _ageFlashAt = -10f;

		private Color _ageColor = Color.white;

		private static readonly string[] SunPixels = new string[7] { "..OOO..", ".OYYYO.", "OYWWWYO", "OYWWWYO", "OYWWWYO", ".OYYYO.", "..OOO.." };

		private static readonly string[] MoonPixels = new string[7] { "..LLG..", ".LGG...", "LGG....", "LG.....", "LGG....", ".LGG...", "..LLG.." };

		internal bool VrMenuDrawn;

		internal readonly Rect[] VrMenuRects = new Rect[VrMenu.Items.Length];

		internal bool VrWristDrawn;

		internal readonly Rect[] VrWristSlots = new Rect[12];

		internal int VrWristSlotCount;

		internal int VrWristHover = -1;

		private static readonly Color BoardGreen = new Color(0.55f, 0.95f, 0.5f);

		private WarFlag _flagDrag;

		private Vector2 _flagGrab;

		private int _bandKeyNumber;

		private float _bandKeyAt = -10f;

		internal Func<Vector2, Vector2> WarMapToScreenForTest;

		private static readonly Color PoleBrown = new Color(0.25f, 0.16f, 0.1f);

		private static readonly Color TagInk = new Color(0.07f, 0.05f, 0.04f);

		internal int PinsFrameForTest = -1;

		internal int PinsDrawnForTest;

		private const float PinReach = 60f;

		private const float PinFade = 45f;

		private static readonly Color SailBlue = new Color(0.62f, 0.86f, 1f);

		private static readonly Color FireRed = new Color(1f, 0.32f, 0.22f);

		private Vector2 _mapDragFrom;

		private int _mapButton = -1;

		private bool _mapDragged;

		private bool _mapShift;

		private Vector2 _mapCentreFrom;

		internal Rect WarMapRectForTest;

		private static readonly WarFlag KeyFlag = new WarFlag
		{
			Number = 1,
			Born = -10f
		};

		private Rect _orderBar;

		private Rect _townCard;

		private readonly int[] _groupCounts = new int[7];

		private City _cardCity;

		private bool _areaOn;

		private bool _areaMoved;

		private Vector2 _areaFrom;

		private WarFlag _areaFlag;

		private float _areaRadius;

		internal readonly Rect[] TownCardSharesForTest = new Rect[4];

		private static readonly Color[] OrderTints = new Color[5]
		{
			new Color(1f, 0.55f, 0.42f),
			new Color(0.62f, 0.82f, 1f),
			new Color(1f, 0.78f, 0.35f),
			new Color(1f, 0.4f, 0.4f),
			new Color(1f, 0.6f, 0.2f)
		};

		private static readonly WarFlag[] GroupTags = new WarFlag[7];

		private Texture2D _landFlat;

		private Texture2D _landTerrainy;

		private readonly Dictionary<string, Sprite> _newsIcons = new Dictionary<string, Sprite>();

		private static readonly Regex NewsTags = new Regex("<[^>]+>");

		private string _realmLine;

		private float _realmLineAt = -10f;

		private Kingdom _realmLineOf;

		private float _newsBannerBottom;

		private int _newsBannerFrame = -1;

		private Rect _newsBannerRect;

		private float _newsBannerDrawnAt = -10f;

		private string _fitFor;

		private string _fitOut;

		private float _fitSize;

		private float _fitW;

		private readonly List<WorldNews.Siege> _worstSieges = new List<WorldNews.Siege>();

		private float _freedomAskedAt = -10f;

		private static readonly Color EntryStripe = new Color(0.55f, 0.78f, 1f);

		private string _confirmEntry;

		private float _confirmEntryUntil;

		internal bool FrameMeshForTest => FrameMesh() != null;

		internal int HealAheadForTest { get; private set; }

		internal bool HealTimerForTest { get; private set; }

		private static float ShelfCell => Mathf.Round(21f * GameArt.P);

		private static float CheatsHeight => 4f * GameArt.P + 14f * GameArt.P + 15f * GameArt.P + 3f * GameArt.P;

		private float CompassStripAngle
		{
			get
			{
				if (_mod.Compass.Reading)
				{
					WorldBoxMod.CompassMark? compassOn = _mod.CompassOn;
					if (compassOn.HasValue)
					{
						WorldBoxMod.CompassMark valueOrDefault = compassOn.GetValueOrDefault();
						if (_mod.Host != null)
						{
							Vector2 vector = valueOrDefault.At - _mod.Host.current_position;
							if (!(vector.sqrMagnitude > 9f))
							{
								return float.NaN;
							}
							return Mathf.Atan2(vector.y, vector.x);
						}
					}
				}
				return float.NaN;
			}
		}

		public float FavoritesWidth => Mathf.Round(Mathf.Min(178f * GameArt.P, (float)Screen.width * 0.3f));

		internal float HotbarShownForTest { get; private set; }

		internal string BagTipForTest { get; private set; }

		internal int BagCellCountForTest => _bagCellCount;

		internal float CompassBottomForTest => CompassBottom;

		private static string PadHint => "L-stick walk (L3 run, back twice backstep)   R-stick look (R3 ping)   RT/X attack, gather, draw   LT guard, parry   A jump   B ability   Y talk, use (hold: more)\nRB/LB hotbar   D-pad map, eat, minimap, star   View inventory   Menu settings (hold: quick menu)\n" + ((PadControls.Kind == PadControls.Pictures.Deck) ? "L4 put away   L5 quick menu   R4 reload   R5 map view   R-trackpad swipe: compass, torch, binoculars\n" : "") + "Windows: D-pad moves, A picks, B backs out, RB/LB tabs, R-stick cursor   Map: R-stick cursor, RT click, L-stick pans, RB/LB zoom";

		internal bool CapturePending => _captureFile != null;

		internal int BagShownForTest => ShownBagCount();

		public static bool MetersHeld
		{
			get
			{
				if (!MetersForTest)
				{
					return PlayInput.GetKey(KeyCode.Tab);
				}
				return true;
			}
		}

		internal bool SceneMouseBusyForTest
		{
			get
			{
				if (_scButton < 0)
				{
					return _scDragged;
				}
				return true;
			}
		}

		internal int ScenePipsForTest => _scPips.Count;

		private static Sprite SceneIcon => GameArt.IconNamed("iconSpectate") ?? GameArt.IconNamed("iconInspect");

		internal Rect SceneKeysRectForTest => _scKeys;

		private static float HeadingH => Mathf.Round(15f * GameArt.P);

		private bool PanelsNow
		{
			get
			{
				if (!(_enter >= 1f))
				{
					return _movingPass;
				}
				return true;
			}
		}

		private static float CompassBottom => Mathf.Round(54f * GameArt.P);

		internal Rect OrderBarRectForTest => _orderBar;

		internal Rect TownCardRectForTest => _townCard;

		internal City CardCityForTest
		{
			get
			{
				return _cardCity;
			}
			set
			{
				_cardCity = value;
			}
		}

		private float NewsBannerBottom
		{
			get
			{
				if (_newsBannerFrame != Time.frameCount)
				{
					return 0f;
				}
				return _newsBannerBottom;
			}
		}

		internal Rect NewsBannerRectForTest
		{
			get
			{
				if (!(Time.unscaledTime - _newsBannerDrawnAt < 0.6f))
				{
					return Rect.zero;
				}
				return _newsBannerRect;
			}
		}

		public Hud(WorldBoxMod mod)
		{
			_mod = mod;
		}

		private float VrLift(float sh)
		{
			if (!VrView)
			{
				return 0f;
			}
			return Mathf.Round(sh * 0.14f);
		}

		public void Draw(float viewAlpha = 1f, float hudAlpha = 1f)
		{
			if (PhotoMode.Active)
			{
				// Photo mode: minimal HUD (crosshair only when aiming), hint bar
				try
				{
					if (Event.current != null && Event.current.type == EventType.Repaint)
					{
						GameArt.Label(new Rect(12f, Screen.height - 22f, Screen.width - 24f, 14f), "Photo mode — F10 exit  ·  F9 screenshot  ·  +/- FOV  ·  Backspace reset  ·  HUD hidden", 3.8f, TextAnchor.MiddleLeft, new Color(1,1,1,0.85f));
						// subtle crosshair dot in center when not in menu
						if (!_mod.AnyMenuOpen && _mod.PoseViewOut < 0.5f)
						{
							float cx = Screen.width * 0.5f; float cy = Screen.height * 0.5f;
							GUI.color = new Color(1,1,1,0.5f);
							GUI.DrawTexture(new Rect(cx-1, cy-1, 2, 2), Texture2D.whiteTexture);
							GUI.color = Color.white;
						}
					}
				} catch {}
				DrawMovingPanels(Screen.width, Screen.height, 0f, 1f);
				return;
			}
			EnsureAssets();
			GameArt.Ensure();
			float num = Screen.width;
			float num2 = Screen.height;
			CombatFeedback feedback = _mod.Feedback;
			bool flag = _mod.DeathTimer >= 0f || _mod.LeavingDead;
			float num3 = Entering(hudAlpha);
			if (viewAlpha < 1f)
			{
				Color color = GUI.color;
				GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(viewAlpha));
				GUI.DrawTexture(new Rect(0f, 0f, num, num2), _mod.ShownFrame, ScaleMode.StretchToFill, alphaBlend: true);
				GUI.color = color;
				DrawMovingPanels(num, num2, 0f, Mathf.Clamp01(viewAlpha));
				return;
			}
			float num4 = (_mod.Settings.ScreenShake ? feedback.Shake : 0f);
			float num5 = ((num4 > 0f) ? ((float)(_shakeRandom.NextDouble() * 2.0 - 1.0) * num4 * num2 * 0.012f) : 0f);
			float num6 = ((num4 > 0f) ? ((float)(_shakeRandom.NextDouble() * 2.0 - 1.0) * num4 * num2 * 0.012f) : 0f);
			float num7 = ((num4 > 0f) ? (num2 * 0.02f) : 0f);
			Rect rect = new Rect(0f - num7 + num5, 0f - num7 + num6, num + num7 * 2f, num2 + num7 * 2f);
			Matrix4x4 matrix = GUI.matrix;
			_frameRoll = Matrix4x4.identity;
			float num8 = (NoWorld ? 0f : _mod.Lean);
			if (Mathf.Abs(num8) > 0.01f)
			{
				float f = Mathf.Abs(num8) * (MathF.PI / 180f);
				float num9 = Mathf.Cos(f) + Mathf.Sin(f) * Mathf.Max(num / num2, num2 / num);
				Vector3 vector = new Vector3(num * 0.5f, num2 * 0.5f, 0f);
				_frameRoll = Matrix4x4.TRS(vector, Quaternion.Euler(0f, 0f, num8), new Vector3(num9, num9, 1f)) * Matrix4x4.Translate(-vector);
				GUI.matrix = matrix * _frameRoll;
			}
			Texture frameBehind = _mod.FrameBehind;
			if (frameBehind != null && !NoWorld)
			{
				GUI.DrawTexture(rect, frameBehind, ScaleMode.StretchToFill, alphaBlend: false);
			}
			if (!NoWorld)
			{
				DrawFrame(rect, frameBehind != null);
			}
			GUI.matrix = matrix;
			if (hudAlpha <= 0f)
			{
				DrawMovingPanels(num, num2, 0f, 1f);
				return;
			}
			_enter = num3;
			DrawOverlay(num, num2, feedback, flag);
			_enter = 1f;
			if (hudAlpha < 1f && !NoWorld)
			{
				Color color2 = GUI.color;
				GUI.color = new Color(1f, 1f, 1f, 1f - hudAlpha);
				GUI.matrix = matrix * _frameRoll;
				DrawFrame(rect, blend: true);
				GUI.matrix = matrix;
				GUI.color = color2;
			}
			if (num3 < 1f && !flag)
			{
				DrawMovingPanels(num, num2, num3, 1f);
			}
		}

		private void DrawFrame(Rect view, bool blend, bool seamless = true)
		{
			FrameProjection shown = _mod.Shown;
			if (!shown.Moves || shown.Width <= 0 || shown.Height <= 0)
			{
				GUI.DrawTexture(view, _mod.ShownFrame, ScaleMode.StretchToFill, blend);
				return;
			}
			float num = shown.Width;
			float num2 = shown.Height;
			Material material = (seamless ? FrameMesh() : null);
			int num3 = ((material != null) ? 48 : 16);
			int num4 = num3 * 3 / 16;
			for (int i = 0; i <= num3; i++)
			{
				float num5 = ((i == 0) ? view.x : ((i == num3) ? view.xMax : Mathf.Round(view.x + view.width * (float)i / (float)num3)));
				_stripX[i] = num5;
				_stripS[i] = (num5 - view.x) / view.width * num;
			}
			_bandY[0] = view.y;
			_bandY[1] = Mathf.Round(view.y + view.height * 0.2f);
			_bandY[2] = Mathf.Round(view.y + view.height * 0.8f);
			_bandY[3] = view.yMax;
			int num6 = 0;
			_rowAt[0] = 0;
			for (int j = 0; j < 3; j++)
			{
				int num7 = ((!(material != null)) ? 1 : BandSteps[j]);
				for (int k = 1; k <= num7; k++)
				{
					num6++;
					_gridY[num6] = Mathf.Lerp(_bandY[j], _bandY[j + 1], (float)k / (float)num7);
				}
				_rowAt[j + 1] = num6;
			}
			_gridY[0] = view.y;
			for (int l = 0; l <= num6; l++)
			{
				float row = (_gridY[l] - view.y) / view.height * num2;
				for (int m = 0; m <= num3; m++)
				{
					shown.Source(_stripS[m], row, out var srcCol, out var srcRow);
					_gridC[l, m] = srcCol;
					_gridR[l, m] = srcRow;
				}
			}
			if (num4 > 0 && num3 > num4 * 2)
			{
				for (int n = 0; n <= num6; n++)
				{
					if (_gridC[n, 0] < 0f && _gridC[n, num4] > 0f)
					{
						for (int num8 = 0; num8 < num4; num8++)
						{
							_gridC[n, num8] = _gridC[n, num4] * _stripS[num8] / _stripS[num4];
						}
					}
					int num9 = num3 - num4;
					if (_gridC[n, num3] > num && _gridC[n, num9] < num)
					{
						for (int num10 = num9 + 1; num10 <= num3; num10++)
						{
							_gridC[n, num10] = _gridC[n, num9] + (num - _gridC[n, num9]) * (_stripS[num10] - _stripS[num9]) / (num - _stripS[num9]);
						}
					}
				}
			}
			int num11 = _rowAt[1];
			int num12 = _rowAt[2];
			for (int num13 = 0; num13 <= num3; num13++)
			{
				if (_gridR[0, num13] < 0f && _gridR[num11, num13] > 0f)
				{
					for (int num14 = 0; num14 < num11; num14++)
					{
						_gridR[num14, num13] = _gridR[num11, num13] * (_gridY[num14] - view.y) / Mathf.Max(1f, _gridY[num11] - view.y);
					}
				}
				if (_gridR[num6, num13] > num2 && _gridR[num12, num13] < num2)
				{
					for (int num15 = num12 + 1; num15 <= num6; num15++)
					{
						_gridR[num15, num13] = _gridR[num12, num13] + (num2 - _gridR[num12, num13]) * (_gridY[num15] - _gridY[num12]) / Mathf.Max(1f, view.yMax - _gridY[num12]);
					}
				}
			}
			if (material != null)
			{
				DrawStrips(material, num3, num6, num, num2);
				return;
			}
			for (int num16 = 0; num16 < num3; num16++)
			{
				for (int num17 = 0; num17 < num6; num17++)
				{
					float u = (_gridC[num17, num16] + _gridC[num17 + 1, num16]) * 0.5f / num;
					float u2 = (_gridC[num17, num16 + 1] + _gridC[num17 + 1, num16 + 1]) * 0.5f / num;
					float r = (_gridR[num17, num16] + _gridR[num17, num16 + 1]) * 0.5f;
					float r2 = (_gridR[num17 + 1, num16] + _gridR[num17 + 1, num16 + 1]) * 0.5f;
					Cell(_stripX[num16], _gridY[num17], _stripX[num16 + 1], _gridY[num17 + 1], u, u2, r, r2, num2, blend);
				}
			}
		}

		internal void DrawFrameForTest(Rect view, bool seamless)
		{
			DrawFrame(view, blend: false, seamless);
		}

		private Material FrameMesh()
		{
			if (_frameMeshTried)
			{
				return _frameMesh;
			}
			_frameMeshTried = true;
			Shader shader = Shader.Find("Hidden/Internal-GUITexture");
			if (shader == null)
			{
				Log.Info("hud: no GUI texture shader; a turned frame is drawn in rectangles");
			}
			else
			{
				_frameMesh = new Material(shader)
				{
					name = "FirstPersonFrameStrips",
					hideFlags = HideFlags.HideAndDontSave
				};
			}
			return _frameMesh;
		}

		private void DrawStrips(Material mesh, int cols, int rows, float w, float h)
		{
			mesh.mainTexture = _mod.ShownFrame;
			mesh.SetPass(0);
			GL.PushMatrix();
			GL.LoadPixelMatrix(0f, Screen.width, Screen.height, 0f);
			GL.MultMatrix(_frameRoll);
			GL.Begin(7);
			GL.Color(GUI.color * 0.5f);
			for (int i = 0; i < cols; i++)
			{
				float num = _stripX[i];
				float num2 = _stripX[i + 1];
				if (num2 <= num)
				{
					continue;
				}
				for (int j = 0; j < rows; j++)
				{
					float num3 = _gridY[j];
					float num4 = _gridY[j + 1];
					if (!(num4 <= num3))
					{
						GL.TexCoord2(_gridC[j, i] / w, 1f - _gridR[j, i] / h);
						GL.Vertex3(num, num3, 0f);
						GL.TexCoord2(_gridC[j, i + 1] / w, 1f - _gridR[j, i + 1] / h);
						GL.Vertex3(num2, num3, 0f);
						GL.TexCoord2(_gridC[j + 1, i + 1] / w, 1f - _gridR[j + 1, i + 1] / h);
						GL.Vertex3(num2, num4, 0f);
						GL.TexCoord2(_gridC[j + 1, i] / w, 1f - _gridR[j + 1, i] / h);
						GL.Vertex3(num, num4, 0f);
					}
				}
			}
			GL.End();
			GL.PopMatrix();
		}

		private void Cell(float x0, float y0, float x1, float y1, float u0, float u1, float r0, float r1, float h, bool blend)
		{
			if (!(x1 <= x0) && !(y1 <= y0))
			{
				GUI.DrawTextureWithTexCoords(new Rect(x0, y0, x1 - x0, y1 - y0), _mod.ShownFrame, new Rect(u0, 1f - r1 / h, u1 - u0, (r1 - r0) / h), blend);
			}
		}

		private void DrawOverlay(float sw, float sh, CombatFeedback fb, bool dead)
		{
			Actor host = _mod.Host;
			bool flag = false;
			try
			{
				flag = !dead && host != null && host.isAlive() && ((BaseSimObject)host).hasAnyStatusEffect() && ((BaseSimObject)host).hasStatus("burning");
			}
			catch
			{
			}
			if (flag)
			{
				DrawOnFire(sw, sh);
			}
			if (!dead)
			{
				if (_mod.Swimming)
				{
					DrawWaterline(sw, sh);
				}
				DrawFloatingTexts(sw, sh, fb);
				DrawHearts(sw, sh);
				DrawSpeechBubble(sw, sh);
				DrawPings(sw, sh);
				DrawHeldCompass(sw, sh);
				if (!DrawBinoculars(sw, sh) && _mod.PoseViewOut < 0.5f)
				{
					DrawCrosshair(sw, sh, fb);
				}
				DrawHurtFrom(sw, sh, fb, _mod.ViewYaw);
				DrawConversation(sw, sh);
				DrawCivilHud(sw, sh);
				Hub.DrawHud(sw, sh);
				DrawWork(sw, sh);
				if (VrView)
				{
					DrawTownBanner(sw, sh);
				}
				else
				{
					DrawMinimap(sw, sh);
					float a = DrawTownBanner(sw, sh);
					DrawCampaign(sw, sh, Mathf.Max(a, MinimapBottom(sw, sh)));
				}
				DrawNewsAlerts(sw, sh);
			}
			float num = fb.Hurt * 0.85f;
			Actor host2 = _mod.Host;
			if (!dead && host2 != null && host2.isAlive() && (float)host2.getHealth() / (float)Mathf.Max(1, host2.getMaxHealth()) < 0.3f)
			{
				num = Mathf.Max(num, 0.3f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f));
			}
			if (dead)
			{
				num = 1f;
			}
			if (num > 0.01f)
			{
				Color color = GUI.color;
				GUI.color = new Color(0.75f, 0f, 0f, Mathf.Clamp01(num));
				GUI.DrawTexture(new Rect(0f, 0f, sw, sh), _vignette, ScaleMode.StretchToFill, alphaBlend: true);
				GUI.color = color;
			}
			if (dead)
			{
				DrawDeathScreen(sw, sh, fb);
				return;
			}
			DrawInfo(sw, sh, fb);
			DrawBagGains(sw, sh);
			DrawMapAndWheel(sw, sh);
			DrawTouch(sw, sh);
		}

		private void DrawWork(float sw, float sh)
		{
			Skin.Ensure();
			float p = GameArt.P;
			Work work = _mod.Work;
			if (work.Target != null && work.Progress > 0f)
			{
				float num = Mathf.Round(70f * p);
				GameArt.Bar(new Rect(Mathf.Round(sw * 0.5f - num * 0.5f), Mathf.Round(sh * 0.5f + sh * 0.14f), num, Mathf.Round(7f * p)), work.Progress, GameArt.AgeYellow);
			}
			DrawForgeHud(sw, sh);
			DrawBag(sw, sh - VrLift(sh), work);
		}

		private void DrawCraftWindow()
		{
			EnsureAssets();
			Crafting craft = _mod.Craft;
			Actor host = _mod.Host;
			if (host == null)
			{
				return;
			}
			GameArt.Ensure();
			float num = Screen.width;
			float num2 = Screen.height;
			float p = GameArt.P;
			float num3 = num - FavoritesWidth - 16f * p;
			float num4 = Mathf.Round(Mathf.Min(num3 - 20f * p, 530f * p));
			float num5 = Mathf.Round(Mathf.Min(num2 - 40f * p, 340f * p));
			bool close;
			Rect rect = GameWindow(_invWin = new Rect(Mathf.Round((num3 - num4) * 0.5f + 6f * p), Mathf.Round((num2 - num5) * 0.5f + 4f * p), num4, num5), "Inventory", _mod.QuickInventory ? null : "Tab", out close, !_mod.QuickInventory);
			if (close)
			{
				craft.Open = false;
				_mod.YouView.Dragging = false;
				return;
			}
			bool flag = Event.current.type == EventType.Repaint;
			string text = null;
			float x = rect.x;
			float num6 = Mathf.Round(Mathf.Min(184f * p, rect.width * 0.45f));
			float y = rect.y;
			string text2 = null;
			try
			{
				text2 = host.getName();
			}
			catch
			{
			}
			SectionTitle(new Rect(x, y, num6, Mathf.Round(20f * p)), string.IsNullOrEmpty(text2) ? WorldBoxMod.Describe(host) : text2, GameArt.IconNamed("iconArmor"));
			y += 22f * p;
			Work work = _mod.Work;
			float num7 = Mathf.Round(20f * p);
			float num8 = Mathf.Round(2f * p);
			float num9 = Mathf.Round(Mathf.Clamp(rect.yMax - y - 26f * p - 2f * (num7 + num8), 96f * p, 160f * p));
			text = DrawDoll(new Rect(x, y, num6, num9), host) ?? text;
			y += num9 + 4f * p;
			SectionTitle(new Rect(x, y, num6, Mathf.Round(20f * p)), Bag.Name(host) + "  " + Bag.Used(host) + "/" + Bag.Size(host) + " slots", GameArt.Loot);
			y += 22f * p;
			text = DrawBagGrid(new Rect(x, y, num6, rect.yMax - y), host, work, num7, num8) ?? text;
			_bagArea = new Rect(x, y, num6, rect.yMax - y);
			float num10 = x + num6 + 8f * p;
			text = DrawCraftGrid(new Rect(num10, rect.y, rect.xMax - num10, rect.height), host, craft) ?? text;
			if (text != null && flag)
			{
				DrawInventoryTip(text);
			}
		}

		private static string NiceName(EquipmentAsset a)
		{
			if (a == null)
			{
				return "-";
			}
			try
			{
				string translatedName = a.getTranslatedName();
				if (!string.IsNullOrEmpty(translatedName) && !translatedName.StartsWith("$"))
				{
					return translatedName;
				}
			}
			catch
			{
			}
			return a.id.Replace('_', ' ');
		}

		private Sprite ResourceIcon(string id)
		{
			if (_resourceIcons.TryGetValue(id, out var value))
			{
				return value;
			}
			if (Supplies.Is(id))
			{
				value = Supplies.Icon(id);
			}
			else
			{
				try
				{
					value = AssetManager.resources.get(id)?.getSpriteIcon();
				}
				catch
				{
					value = null;
				}
			}
			_resourceIcons[id] = value;
			return value;
		}

		private static Sprite IconOf(EquipmentAsset a)
		{
			try
			{
				return a.getSprite();
			}
			catch
			{
				return null;
			}
		}

		private static void DrawSprite(Rect r, Sprite s)
		{
			if (!(s == null) && !(s.texture == null))
			{
				Texture texture = s.texture;
				Rect rect;
				try
				{
					rect = s.textureRect;
				}
				catch
				{
					rect = s.rect;
				}
				float num = rect.width / Mathf.Max(1f, rect.height);
				GUI.DrawTextureWithTexCoords((num >= 1f) ? new Rect(r.x, r.y + r.height * (1f - 1f / num) * 0.5f, r.width, r.height / num) : new Rect(r.x + r.width * (1f - num) * 0.5f, r.y, r.width * num, r.height), texture, new Rect(rect.x / (float)texture.width, rect.y / (float)texture.height, rect.width / (float)texture.width, rect.height / (float)texture.height));
			}
		}

		private void DrawConversation(float sw, float sh)
		{
			string text = Bandages.WithHint(_mod.ShownPrompt(_mod.Prompt), _mod.Host);
			if (!string.IsNullOrEmpty(text) && !PlayerTalk.CardOpen)
			{
				DrawPrompt(sw, sh, text);
			}
			Interactions talk = _mod.Talk;
			if (!_mod.Converse.Open && talk.Speaker != null && !string.IsNullOrEmpty(talk.Line))
			{
				float p = GameArt.P;
				float num = Time.unscaledTime - talk.LineStart;
				float num2 = Mathf.Clamp01((talk.LineUntil - Time.unscaledTime) / 0.6f);
				float num3 = Mathf.Round(Mathf.Min(sw * 0.6f, 360f * p));
				float height = Mathf.Round(36f * p);
				Rect r = new Rect(Mathf.Round((sw - num3) * 0.5f), Mathf.Round(sh * 0.68f), num3, height);
				GameArt.TipPanel(r, num2);
				string text2 = talk.SpeakerName ?? "";
				Rect r2 = new Rect(r.x + 8f * p, r.y - 11f * p, Mathf.Round(GameArt.Measure(text2, 5.6f).x + 16f * p), Mathf.Round(14f * p));
				GameArt.Sliced(r2, GameArt.NamePlate, new Color(1f, 1f, 1f, num2));
				GameArt.Label(new Rect(r2.x, r2.y - 0.5f * p, r2.width, r2.height), text2, 5.6f, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, num2));
				int letters = Mathf.Min(talk.Line.Length, (int)(num * 55f));
				Color color = (talk.Romantic ? new Color(1f, 0.62f, 0.78f, num2) : new Color(1f, 1f, 1f, num2));
				GameArt.Label(new Rect(r.x + 8f * p, r.y + 6f * p, r.width - 16f * p, r.height - 9f * p), Lang.Typed(talk.Line, letters), 5.4f, TextAnchor.UpperLeft, color, wrap: true);
			}
		}

		private void DrawSpeechBubble(float sw, float sh)
		{
			Interactions talk = _mod.Talk;
			if (talk.Speaker == null)
			{
				return;
			}
			Vector3 vector = _mod.SpeakerHead();
			FrameProjection shown = _mod.Shown;
			if (shown.Project(vector.x, vector.y, vector.z, out var col, out var row, out var depth))
			{
				float num = col / (float)shown.Width * sw;
				float num2 = row / (float)shown.Height * sh;
				float num3 = Mathf.Clamp(sh * 0.05f / Mathf.Max(0.5f, depth * 0.35f), sh * 0.02f, sh * 0.06f);
				Rect r = new Rect(num - num3, num2 - num3 * 1.1f, num3 * 2f, num3);
				Fill(r, new Color(1f, 1f, 1f, 0.92f));
				Fill(new Rect(num - num3 * 0.15f, r.yMax, num3 * 0.3f, num3 * 0.25f), new Color(1f, 1f, 1f, 0.92f));
				int num4 = ((Time.unscaledTime - talk.LineStart < (float)talk.Line.Length / 55f) ? (1 + (int)(Time.unscaledTime * 4f) % 3) : 3);
				float num5 = num3 * 0.22f;
				for (int i = 0; i < num4; i++)
				{
					Fill(new Rect(r.center.x - num5 * 1.8f + (float)i * num5 * 1.4f, r.center.y - num5 * 0.5f, num5, num5), talk.Romantic ? new Color(0.9f, 0.25f, 0.4f) : new Color(0.25f, 0.25f, 0.3f));
				}
			}
		}

		private void DrawHearts(float sw, float sh)
		{
			if (_mod.Talk.Hearts.Count == 0)
			{
				return;
			}
			if (_heart == null)
			{
				_heart = MakeHeart();
			}
			FrameProjection shown = _mod.Shown;
			foreach (Heart heart in _mod.Talk.Hearts)
			{
				if (!(heart.Age < 0f) && shown.Project(heart.At.x, heart.At.y, heart.At.z, out var col, out var row, out var depth))
				{
					float num = Mathf.Clamp(sh * 0.035f / Mathf.Max(0.4f, depth * 0.3f), sh * 0.012f, sh * 0.05f);
					float num2 = col / (float)shown.Width * sw;
					float num3 = row / (float)shown.Height * sh;
					Color color = GUI.color;
					GUI.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01(heart.Age / heart.Life));
					GUI.DrawTexture(new Rect(num2 - num * 0.5f, num3 - num * 0.5f, num, num * 6f / 7f), _heart);
					GUI.color = color;
				}
			}
		}

		private static Texture2D MakeHeart()
		{
			string[] array = new string[7] { ".xx.xx.", "xhxxxxx", "xxxxxxx", "xxxxxxx", ".xxxxx.", "..xxx..", "...x..." };
			Texture2D texture2D = new Texture2D(7, 7, TextureFormat.RGBA32, mipChain: false)
			{
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp
			};
			Color32[] array2 = new Color32[49];
			for (int i = 0; i < 7; i++)
			{
				for (int j = 0; j < 7; j++)
				{
					char c = array[i][j];
					int num = (6 - i) * 7 + j;
					Color32 color;
					switch (c)
					{
					default:
						color = new Color32(0, 0, 0, 0);
						break;
					case 'h':
						color = new Color32(byte.MaxValue, 180, 200, byte.MaxValue);
						break;
					case 'x':
						color = new Color32(235, 50, 80, byte.MaxValue);
						break;
					}
					array2[num] = color;
				}
			}
			texture2D.SetPixels32(array2);
			texture2D.Apply(updateMipmaps: false);
			return texture2D;
		}

		private void DrawWaterline(float sw, float sh)
		{
			float unscaledTime = Time.unscaledTime;
			float num = sh * 0.8f + Mathf.Sin(unscaledTime * 2.1f) * sh * 0.01f;
			Fill(new Rect(0f, num, sw, sh - num), new Color(0.12f, 0.42f, 0.75f, 0.38f));
			float num2 = Mathf.Max(6f, sh * 0.02f);
			float num3 = Mathf.Max(2f, sh * 0.005f);
			float num4 = unscaledTime * sh * 0.03f % (num2 * 2f);
			for (float num5 = (0f - num2) * 2f + num4; num5 < sw; num5 += num2 * 2f)
			{
				float num6 = Mathf.Sin(unscaledTime * 3f + num5 * 0.02f) * num3;
				Fill(new Rect(num5, num - num3 + num6, num2, num3), new Color(0.9f, 0.97f, 1f, 0.6f));
			}
		}

		private void DrawCrosshair(float sw, float sh, CombatFeedback fb)
		{
			float num = sw * 0.5f;
			float num2 = sh * 0.5f;
			float num3 = Mathf.Max(6f, sh * 0.011f * GameArt.UserScale);
			float num4 = Mathf.Max(2f, sh * 0.0025f * GameArt.UserScale);
			Actor host = _mod.Host;
			float num5 = 1f;
			if (_mod.Melee.Active)
			{
				num5 = _mod.Melee.Readiness;
			}
			else
			{
				try
				{
					num5 = ((host == null || host.isAttackReady()) ? 1f : 0f);
				}
				catch
				{
				}
			}
			bool flag = num5 >= 1f;
			float num6 = num3 * (1.1f - 0.75f * num5) + fb.Kick * num3 * 0.6f;
			if (_mod.Bow.InHand)
			{
				flag = _mod.Bow.State.ArrowNocked;
				num6 = num3 * (1.4f - 1.15f * _mod.Bow.Draw) + fb.Kick * num3 * 0.6f;
			}
			Color color = new Color(1f, 1f, 1f, flag ? 0.9f : 0.5f);
			if (_mod.Picked is Actor actor && host != null)
			{
				if (IsEnemy(host, actor))
				{
					color = new Color(1f, 0.3f, 0.25f, color.a);
				}
				else if (host.kingdom != null && actor.kingdom == host.kingdom)
				{
					color = new Color(0.45f, 1f, 0.45f, color.a);
				}
			}
			if (!_mod.VrAimDotShown)
			{
				if (_mod.Shotgun.InHand)
				{
					DrawShotgunReticle(num, num2, num4, color, fb);
				}
				else
				{
					Fill(new Rect(num - num6 - num3, num2 - num4 * 0.5f, num3, num4), color);
					Fill(new Rect(num + num6, num2 - num4 * 0.5f, num3, num4), color);
					Fill(new Rect(num - num4 * 0.5f, num2 - num6 - num3, num4, num3), color);
					Fill(new Rect(num - num4 * 0.5f, num2 + num6, num4, num3), color);
					Fill(new Rect(num - num4 * 0.5f, num2 - num4 * 0.5f, num4, num4), color);
				}
			}
			if (fb.HitMarker > 0f)
			{
				Color c = (fb.HitWasKill ? new Color(1f, 0.2f, 0.15f, fb.HitMarker) : (fb.HitWasHead ? new Color(1f, 0.36f, 0.2f, fb.HitMarker) : new Color(1f, 1f, 1f, fb.HitMarker)));
				float num7 = num3 * 1.2f;
				float num8 = num3 * (fb.HitWasHead ? 1.45f : 1.1f);
				Matrix4x4 matrix = GUI.matrix;
				GUIUtility.RotateAroundPivot(45f, new Vector2(num, num2));
				Fill(new Rect(num - num7 - num8, num2 - num4 * 0.75f, num8, num4 * 1.5f), c);
				Fill(new Rect(num + num7, num2 - num4 * 0.75f, num8, num4 * 1.5f), c);
				Fill(new Rect(num - num4 * 0.75f, num2 - num7 - num8, num4 * 1.5f, num8), c);
				Fill(new Rect(num - num4 * 0.75f, num2 + num7, num4 * 1.5f, num8), c);
				GUI.matrix = matrix;
			}
			DrawStops(num, num2, num3, num4, fb);
		}

		private void DrawInfo(float sw, float sh, CombatFeedback fb)
		{
			Actor host = _mod.Host;
			Actor body = _mod.Body;
			Mathf.Max(6f, sh * 0.012f);
			if (host != null && host.isAlive())
			{
				if (!_mod.Craft.Open && !_mod.Royal.Open && !_mod.Royal.ObeliskOpen && !VrView)
				{
					DrawNameplate(sw, sh, body, fb);
				}
				DrawVitals(sw, sh - VrLift(sh), body);
				DrawHotbar(sw, sh - VrLift(sh), host);
				if (!VrView)
				{
					DrawCompass(sw, sh, _mod.ViewYaw, _mod.CompassMarks());
				}
			}
			if (_mod.Picked is Actor actor && actor.isAlive())
			{
				float num = 1f;
				try
				{
					num = actor.getHealthRatio();
				}
				catch
				{
				}
				bool flag = host != null && IsEnemy(host, actor);
				if (num < 0.999f || flag)
				{
					float p = GameArt.P;
					bool flag2 = false;
					if (flag)
					{
						try
						{
							flag2 = People.IsHumanoid(actor);
						}
						catch
						{
						}
					}
					float num2 = (flag2 ? 44f : 64f);
					float num3 = (flag2 ? 6f : 9f);
					float num4 = (flag2 ? 0.75f : 1f);
					float num5 = Mathf.Round(num2 * p);
					float height = Mathf.Round(num3 * p);
					Rect r = new Rect(Mathf.Round(sw * 0.5f - num5 * 0.5f + 3f * p), Mathf.Round(sh * 0.5f + sh * 0.045f), num5, height);
					GameArt.Sliced(new Rect(r.x - 2f * p, r.y - 1.5f * p, r.width + 4f * p, r.height + 3f * p), GameArt.Inner12, new Color(0.83f, 0.83f, 0.83f, 1f));
					_targetMeter = ((actor == _targetFor) ? _targetMeter : default(Meter));
					_targetFor = actor;
					Step(ref _targetMeter, num);
					GameArt.Bar(r, _targetMeter.Shown, flag ? new Color(0.86f, 0.2f, 0.12f) : GameArt.HealthGreen, _targetMeter.Trail);
					Rect r2 = new Rect(Mathf.Round(r.x - 8f * p * num4), Mathf.Round(r.center.y - 6.5f * p * num4), 14f * p * num4, 13f * p * num4);
					GameArt.IconPart(new Rect(r2.x + p * 0.75f, r2.y + p * 0.75f, r2.width, r2.height), GameArt.Heart, GameArt.HeartPart, new Color(0f, 0f, 0f, 0.5f));
					GameArt.IconPart(r2, GameArt.Heart, GameArt.HeartPart, Color.white);
				}
			}
			else
			{
				DrawDismantleBar(sw, sh);
			}
			if (Time.unscaledTime < _mod.HintUntil)
			{
				DrawHelp(sw, sh, VrView ? "Trigger attack, gather   L-trigger guard   Grip talk, use, enter   L-grip reload   B ability, back\nL-stick walk, click to run   R-stick turn, click for binoculars   A jump   Hold-A quick menu\nX inventory   Y map   Menu settings, help   Wrist turn your left wrist up for the minimap, tasks and you" : ((PadControls.Active && PadControls.Shown) ? PadHint : "WASD walk   Mouse look   LMB attack   RMB guard   E use   Hold E more   Q tool   Hold Q tools\nTab you   M world   V map   G let go   F1 help, every key   Esc settings"));
			}
			float newsBannerBottom = NewsBannerBottom;
			newsBannerBottom = DrawCaptureBar(sw, newsBannerBottom);
			if (host != null && host.isAlive())
			{
				newsBannerBottom = Mathf.Max(newsBannerBottom, CompassBottom);
			}
			if (fb.Banner != null && Time.unscaledTime < fb.BannerUntil)
			{
				newsBannerBottom = Pill(sw, Mathf.Max(sh * 0.155f, newsBannerBottom), fb.Banner, new Color(1f, 0.45f, 0.35f), Mathf.Clamp01((fb.BannerUntil - Time.unscaledTime) / 0.5f));
			}
			bool flag3 = false;
			try
			{
				flag3 = ScrollWindow.isWindowActive();
			}
			catch
			{
			}
			bool flag4 = (NetSession.Joined ? WorldSync.HostPaused : Config.paused);
			if (NetSession.Joined || (NetSession.Hosting && !Config.paused))
			{
				flag3 = false;
			}
			if (flag3 || flag4)
			{
				Pill(sw, Mathf.Max(sh * 0.26f, newsBannerBottom + 2f * GameArt.P), flag3 ? "Paused: a game window is open behind the view (Esc closes it)" : (NetSession.Joined ? "Paused by the host" : "Paused"), GameArt.AgeYellow, 1f);
			}
			if (_mod.ToastText != null && Time.unscaledTime < _mod.ToastUntil)
			{
				Pill(sw, Mathf.Max(sh * 0.205f, newsBannerBottom), _mod.ToastText, Color.white, Mathf.Clamp01((_mod.ToastUntil - Time.unscaledTime) / 0.4f));
			}
		}

		private void DrawDeathScreen(float sw, float sh, CombatFeedback fb)
		{
			float num = (_mod.LeavingDead ? 1f : Mathf.Clamp01(_mod.DeathTimer / 1.2f));
			Fill(new Rect(0f, 0f, sw, sh), new Color(0.1f, 0f, 0f, 0.55f * num));
			Skin.Ensure();
			float p = GameArt.P;
			GameArt.Label(new Rect(0f, sh * 0.32f, sw, sh * 0.16f), "You Died", sh / 9f / p, TextAnchor.MiddleCenter, new Color(0.86f, 0.12f, 0.08f, num));
			string text = _mod.DeathName + ((fb.Kills > 0) ? ("   ·   " + fb.Kills + ((fb.Kills == 1) ? " kill" : " kills") + " this life") : "");
			float num2 = Mathf.Round(Mathf.Min(sw * 0.8f, GameArt.Measure(text, 6.5f).x + 40f * p));
			Rect r = new Rect(Mathf.Round((sw - num2) * 0.5f), Mathf.Round(sh * 0.5f), num2, Mathf.Round(20f * p));
			GameArt.Sliced(r, GameArt.Spectate, new Color(1f, 1f, 1f, num));
			GameArt.Sliced(r, GameArt.ButtonBorder, new Color(1f, 1f, 1f, num), 4f, 4f, 4f, 4f);
			GameArt.Icon(new Rect(r.x + 4f * p, r.center.y - 7f * p, 14f * p, 14f * p), GameArt.Skulls, new Color(1f, 1f, 1f, num));
			GameArt.Icon(new Rect(r.xMax - 18f * p, r.center.y - 7f * p, 14f * p, 14f * p), GameArt.Skulls, new Color(1f, 1f, 1f, num));
			GameArt.Label(new Rect(r.x, r.y - 0.5f * p, r.width, r.height), text, 6.5f, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, num));
		}

		private static bool IsEnemy(Actor host, Actor target)
		{
			try
			{
				return host.kingdom != null && target.kingdom != null && host.kingdom != target.kingdom && host.kingdom.isEnemy(target.kingdom);
			}
			catch
			{
				return false;
			}
		}

		private void DrawHealth(Rect r, BaseSimObject obj)
		{
			int num = Math.Max(1, obj.getMaxHealth());
			float num2 = Mathf.Clamp01((float)obj.getHealth() / (float)num);
			Fill(r, new Color(0f, 0f, 0f, 0.55f));
			Fill(new Rect(r.x + 1f, r.y + 1f, (r.width - 2f) * num2, r.height - 2f), Color.Lerp(new Color(0.85f, 0.15f, 0.1f), new Color(0.3f, 0.85f, 0.25f), num2));
		}

		private void Fill(Rect r, Color c)
		{
			Color color = GUI.color;
			GUI.color = c;
			GUI.DrawTexture(r, _white);
			GUI.color = color;
		}

		private static void Text(Rect r, string text, GUIStyle style, Color color)
		{
			if (!string.IsNullOrEmpty(text))
			{
				text = Lang.T(text);
				Font wasFont;
				FontStyle wasStyle;
				bool num = Lang.Fit(style, text, out wasFont, out wasStyle);
				float num2 = Mathf.Max(1f, (float)Screen.height / 540f);
				style.normal.textColor = new Color(0f, 0f, 0f, 0.75f * color.a);
				GUI.Label(new Rect(r.x + num2, r.y + num2, r.width, r.height), text, style);
				style.normal.textColor = color;
				GUI.Label(r, text, style);
				if (num)
				{
					Lang.Restore(style, wasFont, wasStyle);
				}
			}
		}

		private float Pill(float sw, float y, string text, Color color, float alpha)
		{
			if (string.IsNullOrEmpty(text) || alpha <= 0f)
			{
				return y;
			}
			Skin.Ensure();
			float p = GameArt.P;
			Vector2 vector = GameArt.Measure(text, 6.5f);
			float num = Mathf.Round(vector.y + 8f * p);
			float num2 = Mathf.Round(vector.x + 24f * p);
			float num3 = (1f - Smooth(alpha)) * 4f * p;
			Rect r = new Rect(Mathf.Round((sw - num2) * 0.5f), Mathf.Round(y - num3), num2, num);
			GameArt.TipPanel(r, alpha);
			color.a *= alpha;
			GameArt.Label(new Rect(r.x, r.y - 0.5f * p, r.width, r.height), text, 6.5f, TextAnchor.MiddleCenter, color);
			return Mathf.Round(y + num + 6f * p);
		}

		private void DrawHelp(float sw, float sh, string hint)
		{
			float p = GameArt.P;
			Rect r = (VrView ? new Rect(Mathf.Round(sw * 0.2f), Mathf.Round(sh * 0.56f), Mathf.Round(sw * 0.6f), 0f) : new Rect(Mathf.Round(sw * 0.06f), Mathf.Round(sh * 0.63f), Mathf.Round(sw * 0.88f), 0f));
			string[] array = Lang.T(hint).Split('\n');
			float num = Mathf.Round(9f * p);
			r.height = Mathf.Round((float)array.Length * num + 16f * p);
			GameArt.StonePanel(r);
			Rect r2 = new Rect(Mathf.Round(r.center.x - 40f * p), Mathf.Round(r.y - 13f * p), Mathf.Round(80f * p), Mathf.Round(16f * p));
			GameArt.Sliced(r2, GameArt.Tab, Color.white, 6f, 6f, 2f, 4f);
			GameArt.Label(new Rect(r2.x, r2.y + 1f * p, r2.width, r2.height), "How to play", 6f, TextAnchor.MiddleCenter, Color.white);
			if (_helpLines == null || _helpFrom != array[0])
			{
				_helpFrom = array[0];
				_helpLines = new string[array.Length];
				for (int i = 0; i < array.Length; i++)
				{
					StringBuilder stringBuilder = new StringBuilder();
					string[] array2 = array[i].Split(new string[1] { "   " }, StringSplitOptions.RemoveEmptyEntries);
					for (int j = 0; j < array2.Length; j++)
					{
						string text = array2[j].Trim();
						int num2 = (text.StartsWith("[ ]") ? 3 : text.IndexOf(' '));
						if (stringBuilder.Length > 0)
						{
							stringBuilder.Append("<color=#7d8070>   ·   </color>");
						}
						if (num2 <= 0)
						{
							stringBuilder.Append(text);
						}
						else
						{
							stringBuilder.Append("<color=#F3961F>").Append(text.Substring(0, num2)).Append("</color>")
								.Append(text.Substring(num2));
						}
					}
					_helpLines[i] = stringBuilder.ToString();
				}
			}
			for (int k = 0; k < _helpLines.Length; k++)
			{
				GameArt.Label(new Rect(r.x + 10f * p, r.y + 9f * p + (float)k * num, r.width - 20f * p, num), _helpLines[k], 5f, TextAnchor.MiddleCenter, Color.white);
			}
		}

		private void EnsureAssets()
		{
			if (_white == null)
			{
				_white = new Texture2D(1, 1);
				_white.SetPixel(0, 0, Color.white);
				_white.Apply();
			}
			if (_vignette == null)
			{
				_vignette = new Texture2D(64, 64, TextureFormat.RGBA32, mipChain: false)
				{
					wrapMode = TextureWrapMode.Clamp
				};
				Color32[] array = new Color32[4096];
				for (int i = 0; i < 64; i++)
				{
					for (int j = 0; j < 64; j++)
					{
						float num = (float)j / 63f * 2f - 1f;
						float num2 = (float)i / 63f * 2f - 1f;
						float num3 = Mathf.Pow(Mathf.Clamp01((Mathf.Sqrt(num * num + num2 * num2) / 1.414f - 0.35f) / 0.65f), 1.6f);
						array[i * 64 + j] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)(num3 * 255f));
					}
				}
				_vignette.SetPixels32(array);
				_vignette.Apply(updateMipmaps: false);
			}
			Skin.Ensure();
			int num4 = Mathf.Max(12, (int)((float)Screen.height * Device.HudScale) / 42);
			if (_label == null || _label.fontSize != num4)
			{
				float u = Skin.U;
				_label = new GUIStyle(GUI.skin.label)
				{
					font = Skin.Serif,
					fontSize = num4,
					fontStyle = FontStyle.Bold,
					wordWrap = false
				};
				_small = new GUIStyle(_label)
				{
					fontSize = Mathf.Max(10, num4 * 3 / 4),
					fontStyle = FontStyle.Normal
				};
				_smallRight = new GUIStyle(_small)
				{
					alignment = TextAnchor.UpperRight
				};
				_center = new GUIStyle(_label)
				{
					alignment = TextAnchor.UpperCenter,
					fontSize = Mathf.Max(11, num4 * 4 / 5)
				};
				_big = new GUIStyle(_label)
				{
					alignment = TextAnchor.MiddleCenter
				};
				_float = new GUIStyle(_label)
				{
					alignment = TextAnchor.MiddleCenter
				};
				_wrap = new GUIStyle(_label)
				{
					alignment = TextAnchor.UpperLeft,
					wordWrap = true,
					fontStyle = FontStyle.Normal,
					fontSize = _center.fontSize
				};
				_button = Skin.Button(17f * u);
				_buttonOff = Skin.Button(17f * u);
				Skin.Disabled(_buttonOff);
				_tabOff = Skin.Tab(17f * u, on: false);
				_tabOn = Skin.Tab(17f * u, on: true);
				_rowName = Skin.Text(19f * u, bold: true);
				_rowName.clipping = TextClipping.Clip;
				_rowCost = Skin.Text(16f * u);
				_rowCost.clipping = TextClipping.Clip;
				_titleStyle = Skin.Text(22f * u, bold: true, TextAnchor.MiddleCenter);
				_hintRight = Skin.Text(15f * u, bold: false, TextAnchor.MiddleRight);
				_headInk = Skin.Text(19f * u, bold: true);
				_bodyInk = Skin.Text(17f * u);
				_wrapInk = Skin.Text(18f * u, bold: false, TextAnchor.UpperLeft, wrap: true);
				_wrapSmallInk = Skin.Text(16f * u, bold: false, TextAnchor.UpperLeft, wrap: true);
				_rowCostRight = Skin.Text(16f * u, bold: true, TextAnchor.MiddleRight);
				_centerInk = Skin.Text(18f * u, bold: true, TextAnchor.UpperCenter);
				_centerCream = Skin.Text(18f * u, bold: true, TextAnchor.MiddleCenter);
			}
		}

		private void DrawAbility(float sw, float sh, Actor host)
		{
			Ability ability = _mod.Ability;
			if (ability == null || host == null)
			{
				return;
			}
			float vitalsAlpha = _vitalsAlpha;
			if (!(vitalsAlpha <= 0.01f))
			{
				float p = GameArt.P;
				float unscaledTime = Time.unscaledTime;
				float num = Abilities.CooldownLeft(host);
				bool flag = num > 0f;
				if (_wasCooling && !flag)
				{
					_readyFlash = unscaledTime;
				}
				_wasCooling = flag;
				string why;
				bool flag2 = Abilities.CanPay(host, ability, out why);
				float num2 = Mathf.Round(30f * p);
				Rect r = new Rect(Mathf.Round(_vitalsRect.xMax + 5f * p), Mathf.Round(_vitalsRect.yMax - num2 - 4f * p), num2, num2);
				float num3 = 1f + 0.12f * Bump(unscaledTime - _readyFlash, 0.35f);
				if (num3 > 1f)
				{
					r = new Rect(r.center.x - num2 * num3 * 0.5f, r.center.y - num2 * num3 * 0.5f, num2 * num3, num2 * num3);
				}
				if (_mod.AbilityAiming)
				{
					float num4 = 0.45f + 0.3f * Mathf.Sin(unscaledTime * 8f);
					GameArt.Sliced(new Rect(r.x - 3f * p, r.y - 3f * p, r.width + 6f * p, r.height + 6f * p), GameArt.Tooltip, new Color(1f, 0.82f, 0.3f, num4 * vitalsAlpha), 7f, 7f, 7f, 7f);
				}
				GameArt.StoneButton(r, null, vitalsAlpha);
				Rect position = new Rect(r.x + r.width * 0.16f, r.y + r.height * 0.16f, r.width * 0.68f, r.height * 0.68f);
				Texture2D image = AbilityIconTexture(ability.Icon);
				Color color = GUI.color;
				GUI.color = new Color(0f, 0f, 0f, 0.5f * vitalsAlpha) * color;
				GUI.DrawTexture(new Rect(position.x + p * 0.75f, position.y + p * 0.75f, position.width, position.height), image);
				GUI.color = ((!flag2 && !flag) ? new Color(0.55f, 0.5f, 0.55f, vitalsAlpha) : new Color(1f, 1f, 1f, vitalsAlpha)) * color;
				GUI.DrawTexture(position, image);
				GUI.color = color;
				Rect r2 = new Rect(r.x + 3f * p * num3, r.y + 3f * p * num3, r.width - 6f * p * num3, r.height - 6f * p * num3);
				if (flag)
				{
					float num5 = Mathf.Clamp01(num / Mathf.Max(0.1f, ability.Cooldown));
					GameArt.Flat(new Rect(r2.x, r2.y, r2.width, Mathf.Round(r2.height * num5)), new Color(0f, 0f, 0f, 0.62f * vitalsAlpha));
					GameArt.Label(r2, Mathf.CeilToInt(num).ToString(), 9f, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, vitalsAlpha));
				}
				float num6 = Mathf.Clamp01(1f - (unscaledTime - _readyFlash) / 0.5f);
				if (num6 > 0f)
				{
					GameArt.Flat(r2, new Color(1f, 0.9f, 0.5f, 0.55f * num6 * vitalsAlpha));
				}
				GameArt.Label(new Rect(r.x + 3f * p, r.y + 2f * p, 10f * p, 7f * p), GameArt.VrKey("X"), 5f, TextAnchor.UpperLeft, new Color(GameArt.KeyOrange.r, GameArt.KeyOrange.g, GameArt.KeyOrange.b, vitalsAlpha));
				string text = (flag ? ("back in " + Mathf.CeilToInt(num) + " s") : (_mod.AbilityAiming ? "let go to use" : ((!flag2) ? ("not enough " + Abilities.CostName(host, ability)) : ((Abilities.Cost(host, ability) > 0) ? (Abilities.Cost(host, ability) + " " + Abilities.CostName(host, ability) + (ability.Aimed ? "  ·  hold to aim" : "")) : (ability.Aimed ? "hold to aim" : "ready")))));
				float num7 = r.xMax + 4f * p;
				float num8 = _menuLeft - 6f * p - num7;
				if (!(num8 < 40f * p))
				{
					GameArt.Label(new Rect(num7, r.y + 4f * p, num8, 9f * p), ability.Name, 5.6f, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, vitalsAlpha));
					Color color2 = ((flag || !flag2) ? new Color(1f, 0.55f, 0.42f, vitalsAlpha) : (_mod.AbilityAiming ? new Color(GameArt.AgeYellow.r, GameArt.AgeYellow.g, GameArt.AgeYellow.b, vitalsAlpha) : new Color(GameArt.Grey.r, GameArt.Grey.g, GameArt.Grey.b, vitalsAlpha)));
					GameArt.Label(new Rect(num7, r.y + 14f * p, num8, 8f * p), text, 4.6f, TextAnchor.MiddleLeft, color2);
				}
			}
		}

		private Texture2D AbilityIconTexture(AbilityIcon icon)
		{
			if (_abilityIcons.TryGetValue(icon, out var value))
			{
				return value;
			}
			Color32 outline = new Color32(24, 16, 14, byte.MaxValue);
			Color32 clear = new Color32(0, 0, 0, 0);
			string[] rows;
			Color32 main;
			Color32 hi;
			Color32 dark;
			switch (icon)
			{
			case AbilityIcon.Bomb:
				rows = BombRows;
				main = new Color32(46, 46, 54, byte.MaxValue);
				hi = new Color32(120, 120, 136, byte.MaxValue);
				dark = new Color32(20, 20, 26, byte.MaxValue);
				break;
			case AbilityIcon.Bone:
				rows = BoneRows;
				main = new Color32(236, 228, 206, byte.MaxValue);
				hi = main;
				dark = main;
				break;
			case AbilityIcon.Rock:
				rows = RockRows;
				main = new Color32(128, 124, 116, byte.MaxValue);
				hi = new Color32(176, 170, 160, byte.MaxValue);
				dark = new Color32(86, 82, 78, byte.MaxValue);
				break;
			case AbilityIcon.Snow:
				rows = OrbRows;
				main = new Color32(230, 240, 252, byte.MaxValue);
				hi = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
				dark = new Color32(170, 196, 226, byte.MaxValue);
				break;
			case AbilityIcon.Fire:
				rows = FlameRows;
				main = new Color32(226, 70, 30, byte.MaxValue);
				hi = new Color32(byte.MaxValue, 196, 60, byte.MaxValue);
				dark = new Color32(byte.MaxValue, 246, 200, byte.MaxValue);
				break;
			case AbilityIcon.Plasma:
				rows = OrbRows;
				main = new Color32(70, 220, 210, byte.MaxValue);
				hi = new Color32(210, byte.MaxValue, 250, byte.MaxValue);
				dark = new Color32(30, 140, 150, byte.MaxValue);
				break;
			case AbilityIcon.Acid:
				rows = OrbRows;
				main = new Color32(130, 210, 60, byte.MaxValue);
				hi = new Color32(220, byte.MaxValue, 160, byte.MaxValue);
				dark = new Color32(70, 140, 30, byte.MaxValue);
				break;
			case AbilityIcon.Spore:
				rows = OrbRows;
				main = new Color32(186, 140, 220, byte.MaxValue);
				hi = new Color32(240, 220, byte.MaxValue, byte.MaxValue);
				dark = new Color32(120, 80, 160, byte.MaxValue);
				break;
			case AbilityIcon.Blink:
				rows = DashRows;
				main = new Color32(150, 190, byte.MaxValue, byte.MaxValue);
				hi = main;
				dark = main;
				break;
			case AbilityIcon.Frost:
				rows = FlakeRows;
				main = new Color32(140, 210, byte.MaxValue, byte.MaxValue);
				hi = new Color32(240, 250, byte.MaxValue, byte.MaxValue);
				dark = main;
				break;
			case AbilityIcon.Skull:
				rows = SkullRows12;
				main = new Color32(232, 228, 212, byte.MaxValue);
				hi = main;
				dark = new Color32(120, byte.MaxValue, 140, byte.MaxValue);
				break;
			case AbilityIcon.Heal:
				rows = CrossRows;
				main = new Color32(250, 236, 150, byte.MaxValue);
				hi = main;
				dark = main;
				break;
			case AbilityIcon.Leaf:
				rows = LeafRows;
				main = new Color32(96, 180, 64, byte.MaxValue);
				hi = new Color32(180, 230, 120, byte.MaxValue);
				dark = new Color32(110, 80, 50, byte.MaxValue);
				break;
			case AbilityIcon.Shield:
				rows = ShieldRows;
				main = new Color32(150, 160, 176, byte.MaxValue);
				hi = new Color32(220, 226, 236, byte.MaxValue);
				dark = new Color32(96, 104, 120, byte.MaxValue);
				break;
			case AbilityIcon.Rally:
				rows = ShoutRows;
				main = new Color32(232, 196, 110, byte.MaxValue);
				hi = main;
				dark = main;
				break;
			case AbilityIcon.Howl:
				rows = ShoutRows;
				main = new Color32(170, 186, 210, byte.MaxValue);
				hi = main;
				dark = main;
				break;
			case AbilityIcon.Roar:
				rows = ShoutRows;
				main = new Color32(220, 120, 70, byte.MaxValue);
				hi = main;
				dark = main;
				break;
			case AbilityIcon.Rage:
				rows = FistRows;
				main = new Color32(214, 64, 48, byte.MaxValue);
				hi = new Color32(250, 150, 120, byte.MaxValue);
				dark = new Color32(130, 30, 24, byte.MaxValue);
				break;
			case AbilityIcon.Slam:
				rows = FistRows;
				main = new Color32(200, 150, 96, byte.MaxValue);
				hi = new Color32(240, 200, 150, byte.MaxValue);
				dark = new Color32(130, 90, 54, byte.MaxValue);
				break;
			case AbilityIcon.Venom:
				rows = FangRows;
				main = new Color32(240, 236, 220, byte.MaxValue);
				hi = main;
				dark = main;
				break;
			case AbilityIcon.Wing:
				rows = DashRows;
				main = new Color32(240, 240, 236, byte.MaxValue);
				hi = main;
				dark = main;
				break;
			default:
				rows = DashRows;
				main = new Color32(232, 196, 110, byte.MaxValue);
				hi = main;
				dark = main;
				break;
			}
			Color32 fang = new Color32(130, 230, 80, byte.MaxValue);
			Color32 fuse = new Color32(byte.MaxValue, 200, 60, byte.MaxValue);
			Color32 spark = new Color32(byte.MaxValue, 120, 40, byte.MaxValue);
			Color32 white = new Color32(byte.MaxValue, 250, 236, byte.MaxValue);
			value = Pixels(rows, delegate(char c, int x)
			{
				if (c != 'X')
				{
					if (c != 'B' && c != 'C' && c != 'G' && (c != 'W' || rows != BoneRows) && (c != 'W' || rows != SkullRows12) && (c != 'W' || rows != FangRows) && c != 'R')
					{
						if (c != 'H' && (c != 'Y' || rows != FlameRows))
						{
							switch (c)
							{
							default:
								return clear;
							case 'Y':
								return spark;
							case 'F':
								return fuse;
							case 'W':
								return white;
							case 'D':
								return dark;
							}
						}
						return hi;
					}
					return main;
				}
				return outline;
			});
			if (icon == AbilityIcon.Venom)
			{
				value = Pixels(rows, delegate(char c, int x)
				{
					switch (c)
					{
					default:
						return clear;
					case 'C':
						return fang;
					case 'W':
						return main;
					case 'X':
						return outline;
					}
				});
			}
			if (icon == AbilityIcon.Skull)
			{
				value = Pixels(rows, delegate(char c, int x)
				{
					switch (c)
					{
					default:
						return clear;
					case 'G':
						return dark;
					case 'W':
						return main;
					case 'X':
						return outline;
					}
				});
			}
			_abilityIcons[icon] = value;
			return value;
		}

		private void DrawHealing(float x, float y, Rect bar, Actor host, float health, float alpha)
		{
			if (Event.current.type != EventType.Repaint)
			{
				return;
			}
			HealAheadForTest = 0;
			HealTimerForTest = false;
			float left;
			float length;
			int num = Bandages.Timer(host, out left, out length);
			if (num <= 0)
			{
				return;
			}
			float p = GameArt.P;
			float unscaledTime = Time.unscaledTime;
			int num2 = Bandages.Pending(host);
			int num3 = 1;
			try
			{
				num3 = Mathf.Max(1, host.getMaxHealth());
			}
			catch
			{
			}
			if (num2 > 0)
			{
				float num4 = health * 10f;
				float num5 = Mathf.Min(1f, health + (float)num2 / (float)num3) * 10f;
				float num6 = 11f * p;
				float height = 10f * p;
				float num7 = 1f * p;
				bool flag = health < 0.3f;
				for (int i = 0; i < 10; i++)
				{
					int num8 = ((num4 >= (float)i + 0.75f) ? 2 : ((num4 >= (float)i + 0.25f) ? 1 : 0));
					int num9 = ((num5 >= (float)i + 0.75f) ? 2 : ((num5 >= (float)i + 0.25f) ? 1 : 0));
					if (num9 > num8)
					{
						HealAheadForTest++;
						Rect rect = new Rect(x + (float)i * (num6 + num7), y, num6, height);
						float num10 = 0f;
						if (flag)
						{
							num10 += Mathf.Round(Mathf.Sin(unscaledTime * 31f + (float)i * 1.9f) * 0.7f * p);
						}
						num10 -= Bump(unscaledTime - _regenWave - (float)i * 0.045f, 0.32f) * 2f * p;
						Rect heartPart = GameArt.HeartPart;
						float num11 = Mathf.Min(rect.width / heartPart.width, rect.height / heartPart.height);
						Rect rect2 = new Rect(Mathf.Round(rect.center.x - heartPart.width * num11 * 0.5f), Mathf.Round(rect.center.y - heartPart.height * num11 * 0.5f + num10), Mathf.Round(heartPart.width * num11), Mathf.Round(heartPart.height * num11));
						Rect r = rect2;
						Rect part = heartPart;
						float num12 = Mathf.Round(rect2.width * 0.5f);
						if (num8 == 1)
						{
							r = new Rect(rect2.x + num12, rect2.y, rect2.width - num12, rect2.height);
							part = new Rect(heartPart.x + heartPart.width * 0.5f, heartPart.y, heartPart.width * 0.5f, heartPart.height);
						}
						else if (num9 == 1)
						{
							r = new Rect(rect2.x, rect2.y, num12, rect2.height);
							part = new Rect(heartPart.x, heartPart.y, heartPart.width * 0.5f, heartPart.height);
						}
						float num13 = 0.5f + 0.25f * Mathf.Sin(unscaledTime * 5f - (float)i * 0.8f);
						GameArt.Silhouette(r, GameArt.Heart, part, new Color(0.6f, 1f, 0.55f, num13 * alpha));
					}
				}
			}
			HealTimerForTest = true;
			float num14 = Mathf.Round(15f * p);
			Rect r2 = new Rect(Mathf.Round(bar.x - num14 - 9f * p), Mathf.Round(y + 10f * p * 0.5f - num14 * 0.5f), num14, num14);
			GameArt.TipPanel(r2, alpha);
			Supplies.Kind kind = Strongest(host);
			Color color = GUI.color;
			GUI.color = new Color(1f, 1f, 1f, alpha);
			float num15 = Mathf.Round(num14 * 0.14f);
			DrawSprite(new Rect(r2.x + num15, r2.y + num15, r2.width - num15 * 2f, r2.height - num15 * 2f), Supplies.Icon((kind != null) ? kind.Id : "tw_item_bandage"));
			GUI.color = color;
			float num16 = ((length > 0f) ? Mathf.Clamp01(left / length) : 0f);
			float num17 = num14 * 0.5f + 2.5f * p;
			float num18 = Mathf.Max(1f, Mathf.Round(1.6f * p));
			for (int j = 0; j < 16; j++)
			{
				float f = (float)j / 16f * MathF.PI * 2f;
				Vector2 vector = r2.center + new Vector2(Mathf.Sin(f), 0f - Mathf.Cos(f)) * num17;
				float t = Mathf.Clamp01(num16 * 16f - (float)j);
				Color c = Color.Lerp(new Color(0.12f, 0.1f, 0.1f, 0.6f * alpha), new Color(0.7f, 1f, 0.6f, alpha), t);
				GameArt.Flat(new Rect(Mathf.Round(vector.x - num18 * 0.5f), Mathf.Round(vector.y - num18 * 0.5f), num18, num18), c);
			}
			if (num > 1)
			{
				GameArt.Label(new Rect(r2.x - 6f * p, r2.yMax - 2f * p, r2.width + 4f * p, 7f * p), "x" + num, 4f, TextAnchor.UpperLeft, new Color(0.8f, 1f, 0.7f, alpha));
			}
		}

		private static Supplies.Kind Strongest(Actor a)
		{
			Supplies.Kind result = null;
			float num = -1f;
			foreach (Bandages.Heal item in Bandages.Active)
			{
				if (item.Who == a && item.Seconds - item.Age > num)
				{
					num = item.Seconds - item.Age;
					result = item.Kind;
				}
			}
			return result;
		}

		private bool DrawBinoculars(float sw, float sh)
		{
			Binoculars binoculars = _mod.Binoculars;
			float lens = binoculars.Lens;
			if (lens <= 0f)
			{
				return false;
			}
			if (lens < 0.5f)
			{
				Fill(new Rect(0f, 0f, sw, sh), new Color(0f, 0f, 0f, lens * 2f));
				return true;
			}
			float num = Mathf.Min(1f, lens * 2f - 1f);
			EnsureEyecups(sw, sh);
			float num2 = 0.45f + 0.55f * num;
			float num3 = sw * num2;
			float num4 = sh * num2;
			Rect position = new Rect((sw - num3) * 0.5f, (sh - num4) * 0.5f, num3, num4);
			Color c = new Color(0f, 0f, 0f, 1f);
			GUI.DrawTexture(position, _eyecups, ScaleMode.StretchToFill, alphaBlend: true);
			if (num2 < 1f)
			{
				Fill(new Rect(0f, 0f, sw, position.y + 1f), c);
				Fill(new Rect(0f, position.yMax - 1f, sw, sh - position.yMax + 1f), c);
				Fill(new Rect(0f, position.y, position.x + 1f, num4), c);
				Fill(new Rect(position.xMax - 1f, position.y, sw - position.xMax + 1f, num4), c);
			}
			if (num < 1f)
			{
				Fill(new Rect(0f, 0f, sw, sh), new Color(0f, 0f, 0f, 1f - num));
			}
			float looking = binoculars.Looking;
			if (looking <= 0f)
			{
				return true;
			}
			DrawRangingScale(sw, sh, looking);
			DrawBinocularReadout(sw, sh, binoculars, looking);
			return true;
		}

		private void DrawRangingScale(float sw, float sh, float alpha)
		{
			float num = sw * 0.5f;
			float num2 = sh * 0.5f;
			float num3 = Mathf.Max(1f, Mathf.Round(sh * 0.0014f));
			float num4 = sh * 0.014f;
			float num5 = sh * 0.034f;
			float num6 = sh * 0.3f;
			Color c = new Color(0.04f, 0.05f, 0.05f, 0.62f * alpha);
			Color c2 = new Color(1f, 1f, 0.94f, 0.16f * alpha);
			Fill(new Rect(num - num6, num2 - num3 * 0.5f + num3, num6 - num4, num3), c2);
			Fill(new Rect(num + num4, num2 - num3 * 0.5f + num3, num6 - num4, num3), c2);
			Fill(new Rect(num - num6, num2 - num3 * 0.5f, num6 - num4, num3), c);
			Fill(new Rect(num + num4, num2 - num3 * 0.5f, num6 - num4, num3), c);
			for (int i = 1; (float)i * num5 < num6; i++)
			{
				float num7 = ((i % 5 == 0) ? 0.018f : 0.008f) * sh;
				Fill(new Rect(num - (float)i * num5 - num3 * 0.5f, num2 - num7, num3, num7 * 2f), c);
				Fill(new Rect(num + (float)i * num5 - num3 * 0.5f, num2 - num7, num3, num7 * 2f), c);
			}
			float num8 = sh * 0.16f;
			Fill(new Rect(num - num3 * 0.5f, num2 + num4, num3, num8 - num4), c);
			for (int j = 1; (float)j * num5 < num8; j++)
			{
				Fill(new Rect(num - sh * 0.006f, num2 + (float)j * num5 - num3 * 0.5f, sh * 0.012f, num3), c);
			}
			float num9 = Mathf.Max(2f, Mathf.Round(sh * 0.0028f));
			Fill(new Rect(num - num9 * 0.5f, num2 - num9 * 0.5f, num9, num9), c);
		}

		private void DrawBinocularReadout(float sw, float sh, Binoculars b, float alpha)
		{
			if (_binoName == null || _binoName.fontSize != _center.fontSize)
			{
				_binoName = new GUIStyle(_center)
				{
					alignment = TextAnchor.MiddleCenter
				};
				_binoSmall = new GUIStyle(_small)
				{
					alignment = TextAnchor.MiddleCenter
				};
			}
			Color color = new Color(Skin.Cream.r, Skin.Cream.g, Skin.Cream.b, 0.95f * alpha);
			Color c = new Color(0.02f, 0.03f, 0.03f, 0.5f * alpha);
			float num = (float)_binoName.fontSize * 1.45f;
			float num2 = (float)_binoName.fontSize * 0.6f;
			string text = NameOf(_mod.Picked, _mod.Host);
			float binocularsRange = _mod.BinocularsRange;
			string text2 = ((binocularsRange > 0f) ? (Mathf.RoundToInt(binocularsRange) + ((Mathf.RoundToInt(binocularsRange) == 1) ? " tile away" : " tiles away")) : "out of range");
			float num3 = sh * 0.5f + sh * 0.2f;
			float num4 = Lang.Size(_binoSmall, text2).x;
			if (text != null)
			{
				num4 = Mathf.Max(num4, Lang.Size(_binoName, text).x);
			}
			float num5 = ((text != null) ? (num * 2f) : num);
			Fill(new Rect(Mathf.Round(sw * 0.5f - num4 * 0.5f - num2), Mathf.Round(num3 - num2 * 0.35f), Mathf.Round(num4 + num2 * 2f), Mathf.Round(num5 + num2 * 0.7f)), c);
			if (text != null)
			{
				Skin.Shadowed(new Rect(0f, num3, sw, num), text, _binoName, color);
				num3 += num;
			}
			Skin.Shadowed(new Rect(0f, num3, sw, num), text2, _binoSmall, color);
			int num6 = Binoculars.Powers.Length;
			float num7 = Mathf.Max(3f, Mathf.Round(sh * 0.006f));
			float num8 = num7 * 2.4f;
			float num9 = sw * 0.5f + sh * 0.29f;
			float num10 = sh * 0.5f + sh * 0.3f;
			float num11 = Mathf.Max((float)(num6 - 1) * num8 + num7, Lang.Size(_binoSmall, "wheel: zoom   C: ping").x) + num2 * 2f;
			float num12 = num + num7 * 2.5f + num * 0.9f;
			Fill(new Rect(Mathf.Round(num9 - num11 * 0.5f), Mathf.Round(num10 - num * 0.5f - num2 * 0.35f), Mathf.Round(num11), Mathf.Round(num12 + num2 * 0.7f)), c);
			Skin.Shadowed(new Rect(num9 - num11 * 0.5f, num10 - num * 0.5f, num11, num), b.ChosenPower.ToString("0") + "×", _binoName, color);
			float num13 = num9 - (float)(num6 - 1) * num8 * 0.5f;
			for (int i = 0; i < num6; i++)
			{
				Color c2 = ((i == b.Power) ? color : new Color(color.r, color.g, color.b, 0.3f * alpha));
				Fill(new Rect(Mathf.Round(num13 + (float)i * num8 - num7 * 0.5f), Mathf.Round(num10 + num * 0.55f), num7, num7), c2);
			}
			Skin.Shadowed(new Rect(num9 - num11 * 0.5f, num10 + num * 0.55f + num7 * 1.6f, num11, num * 0.9f), "wheel: zoom   C: ping", _binoSmall, new Color(color.r, color.g, color.b, 0.7f * alpha));
		}

		private static string NameOf(BaseSimObject picked, Actor you)
		{
			try
			{
				if (picked is Actor actor && actor.isAlive())
				{
					string text = ((Dialogue.IsPerson(actor) && !PeopleMet.Knows(you, actor)) ? PeopleMet.Looks(actor) : WorldBoxMod.Describe(actor));
					if (actor.kingdom != null && actor.kingdom.isCiv() && !string.IsNullOrEmpty(actor.kingdom.name))
					{
						text = text + "  ·  " + actor.kingdom.name;
					}
					return text;
				}
				if (picked is Building building && building.asset != null)
				{
					string text2 = HouseInterior.RoomName(building.asset) ?? Plain(building.asset.id);
					string text3 = ((building.city != null) ? building.city.name : null);
					return string.IsNullOrEmpty(text3) ? text2 : (text2 + " in " + text3);
				}
			}
			catch
			{
			}
			return null;
		}

		private static string Plain(string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				return "building";
			}
			string[] array = id.Split('_');
			StringBuilder stringBuilder = new StringBuilder();
			string[] array2 = array;
			foreach (string text in array2)
			{
				if (text.Length == 0 || char.IsDigit(text[0]))
				{
					continue;
				}
				switch (text)
				{
				case "human":
				case "elf":
				case "orc":
				case "dwarf":
					continue;
				}
				if (stringBuilder.Length > 0)
				{
					stringBuilder.Append(' ');
				}
				stringBuilder.Append(text);
			}
			if (stringBuilder.Length <= 0)
			{
				return "building";
			}
			return stringBuilder.ToString();
		}

		private void EnsureEyecups(float sw, float sh)
		{
			float num = sw / Mathf.Max(1f, sh);
			if (_eyecups != null && Mathf.Abs(num - _eyecupsAspect) < 0.01f)
			{
				return;
			}
			if (_eyecups != null)
			{
				UnityEngine.Object.Destroy(_eyecups);
			}
			_eyecupsAspect = num;
			int num2 = Mathf.Clamp(Mathf.RoundToInt(sh * 0.5f), 180, 540);
			int num3 = Mathf.Max(1, Mathf.RoundToInt((float)num2 * num));
			Color32[] array = new Color32[num3 * num2];
			for (int i = 0; i < num2; i++)
			{
				float v = ((float)i + 0.5f - (float)num2 * 0.5f) / (float)num2;
				for (int j = 0; j < num3; j++)
				{
					float value = Binoculars.Shade(((float)j + 0.5f - (float)num3 * 0.5f) / (float)num2, v, 1f);
					array[i * num3 + j] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f));
				}
			}
			_eyecups = new Texture2D(num3, num2, TextureFormat.RGBA32, mipChain: false)
			{
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear,
				name = "BinocularEyecups"
			};
			_eyecups.SetPixels32(array);
			_eyecups.Apply(updateMipmaps: false);
		}

		private void DrawStops(float cx, float cy, float arm, float thick, CombatFeedback fb)
		{
			if (Event.current.type == EventType.Repaint)
			{
				StopMarkerDrawn = (GuardMarkerDrawn = (GuardWordDrawn = (GuardUpDrawn = false)));
			}
			DrawGuard(cx, cy, arm, thick);
			if (fb.BlockMarker > 0f)
			{
				float num = (1f - fb.BlockMarker) / 2.2f;
				float alpha = Mathf.Clamp01(fb.BlockMarker * 1.7f);
				if (fb.BlockKind == Stop.Dodged)
				{
					Slip(cx, cy, arm, thick, num, StopPale, alpha);
				}
				else
				{
					float gap = arm * (1.75f + 1.1f * Mathf.Exp((0f - num) * 26f));
					float num2 = arm * 0.28f * Mathf.Exp((0f - num) * 12f) * Mathf.Sin(num * 110f);
					Color c = Color.Lerp(StopSteel, Color.white, 1f - Mathf.Clamp01(num / 0.06f));
					Brackets(cx + num2, cy, gap, arm, thick, c, alpha);
				}
				if (Event.current.type == EventType.Repaint)
				{
					StopMarkerDrawn = true;
				}
			}
			if (fb.GuardFlash > 0f)
			{
				float num3 = (1f - fb.GuardFlash) / 2.2f;
				float alpha2 = Mathf.Clamp01(fb.GuardFlash * 1.5f);
				Color color = CombatFeedback.WordColor(fb.GuardKind, yours: true);
				if (fb.GuardKind == Stop.Dodged)
				{
					Slip(cx, cy, arm, thick, num3, color, alpha2);
				}
				else
				{
					float gap2 = arm * (1.5f + 1.4f * Out(num3 / 0.22f));
					Brackets(cx, cy, gap2, arm, thick, Color.Lerp(color, Color.white, 1f - Mathf.Clamp01(num3 / 0.08f)), alpha2);
					if (fb.GuardKind == Stop.Parried)
					{
						Burst(cx, cy, arm, thick, num3, color);
					}
				}
				if (Event.current.type == EventType.Repaint)
				{
					GuardMarkerDrawn = true;
				}
			}
			if (!string.IsNullOrEmpty(fb.GuardWord) && fb.GuardWordAge < 1f)
			{
				float guardWordAge = fb.GuardWordAge;
				float scale = 1f + 0.55f * Mathf.Exp((0f - guardWordAge) * 16f) * Mathf.Cos(guardWordAge * 24f);
				float num4 = 1f - Smooth(guardWordAge / 0.12f);
				float alpha3 = 1f - Smooth((guardWordAge - 0.6f) / 0.4f);
				float y = cy + arm * 4.2f + arm * 0.4f * (1f - Mathf.Exp((0f - guardWordAge) / 0.12f));
				int px = Mathf.Max(12, Mathf.RoundToInt(8.5f * GameArt.P));
				Color color2 = Color.Lerp(fb.GuardWordBroken ? GuardBrokenRed : CombatFeedback.WordColor(fb.GuardKind, yours: true), Color.white, 0.85f * num4);
				PopText(new Vector2(cx, y), fb.GuardWord, px, scale, color2, alpha3);
				if (Event.current.type == EventType.Repaint)
				{
					GuardWordDrawn = true;
				}
			}
		}

		private void DrawGuard(float cx, float cy, float arm, float thick)
		{
			GuardControl guard = _mod.Guard;
			if (guard == null || !guard.Active)
			{
				return;
			}
			if (guard.State.Staggered)
			{
				float sinceBroken = guard.SinceBroken;
				float num = arm * 0.35f * Mathf.Exp((0f - sinceBroken) * 6f) * Mathf.Sin(sinceBroken * 90f);
				Brackets(cx + num, cy, arm * 2.3f, arm, thick, GuardBrokenRed, Mathf.Clamp01(1f - sinceBroken / 0.7f) * 0.9f);
				if (Event.current.type == EventType.Repaint)
				{
					GuardUpDrawn = true;
				}
				return;
			}
			float raise = guard.Raise;
			if (!(raise <= 0.02f))
			{
				Color c = (guard.State.ParryOpen ? Color.Lerp(GuardSteel, Color.white, 0.8f) : GuardSteel);
				Brackets(cx, cy, arm * (2.6f - 0.8f * raise), arm, thick, c, 0.85f * raise);
				if (Event.current.type == EventType.Repaint)
				{
					GuardUpDrawn = true;
				}
			}
		}

		private void Brackets(float cx, float cy, float gap, float arm, float thick, Color c, float alpha)
		{
			if (alpha <= 0.01f)
			{
				return;
			}
			float num = arm * 2.1f;
			float num2 = Mathf.Max(2f, Mathf.Round(thick * 1.5f));
			float num3 = arm * 0.6f;
			float num4 = Mathf.Max(1f, Mathf.Round(thick * 0.5f));
			Color color = new Color(StopRim.r, StopRim.g, StopRim.b, 0.55f * alpha);
			Color color2 = new Color(c.r, c.g, c.b, alpha);
			for (int i = 0; i < 2; i++)
			{
				float num5 = ((i == 0) ? num4 : 0f);
				Color c2 = ((i == 0) ? color : color2);
				for (int j = -1; j <= 1; j += 2)
				{
					float num6 = ((j < 0) ? (cx - gap - num2) : (cx + gap));
					float num7 = ((j < 0) ? num6 : (num6 + num2 - num3));
					Fill(new Rect(num6 - num5, cy - num * 0.5f - num5, num2 + 2f * num5, num + 2f * num5), c2);
					Fill(new Rect(num7 - num5, cy - num * 0.5f - num5, num3 + 2f * num5, num2 + 2f * num5), c2);
					Fill(new Rect(num7 - num5, cy + num * 0.5f - num2 - num5, num3 + 2f * num5, num2 + 2f * num5), c2);
				}
			}
		}

		private void Slip(float cx, float cy, float arm, float thick, float a, Color c, float alpha)
		{
			if (alpha <= 0.01f)
			{
				return;
			}
			float num = arm * (1.3f + 2.4f * Out(a / 0.3f));
			float num2 = Mathf.Max(2f, Mathf.Round(thick * 1.2f));
			for (int i = -1; i <= 1; i += 2)
			{
				for (int j = -1; j <= 1; j += 2)
				{
					float num3 = arm * ((j < 0) ? 0.8f : 1.15f) * (1f - 0.4f * Mathf.Clamp01(a / 0.45f));
					float num4 = ((i < 0) ? (cx - num - num3) : (cx + num));
					float num5 = cy + (float)j * arm * 0.45f - num2 * 0.5f;
					Fill(new Rect(num4 - 1f, num5 - 1f, num3 + 2f, num2 + 2f), new Color(StopRim.r, StopRim.g, StopRim.b, 0.45f * alpha));
					Fill(new Rect(num4, num5, num3, num2), new Color(c.r, c.g, c.b, alpha));
				}
			}
		}

		private void Burst(float cx, float cy, float arm, float thick, float a, Color c)
		{
			float num = Mathf.Clamp01(a / 0.25f);
			if (!(num >= 1f))
			{
				float num2 = arm * (1.2f + 2.6f * Out(num));
				float num3 = arm * 0.9f * (1f - num);
				float num4 = Mathf.Max(2f, Mathf.Round(thick * 1.3f));
				Color c2 = new Color(Mathf.Lerp(1f, c.r, num), Mathf.Lerp(1f, c.g, num), Mathf.Lerp(1f, c.b, num), 1f - num * num);
				Matrix4x4 matrix = GUI.matrix;
				GUIUtility.RotateAroundPivot(45f, new Vector2(cx, cy));
				Fill(new Rect(cx - num2 - num3, cy - num4 * 0.5f, num3, num4), c2);
				Fill(new Rect(cx + num2, cy - num4 * 0.5f, num3, num4), c2);
				Fill(new Rect(cx - num4 * 0.5f, cy - num2 - num3, num4, num3), c2);
				Fill(new Rect(cx - num4 * 0.5f, cy + num2, num4, num3), c2);
				GUI.matrix = matrix;
			}
		}

		private static float Out(float x)
		{
			x = Mathf.Clamp01(x);
			return 1f - (1f - x) * (1f - x);
		}

		private void DrawNoticeRows(float top, float width, float rowH, float gap, List<Quest> today, List<Quest> taken, Law.Board b, Actor you)
		{
			float k = GameArt.P;
			if (Event.current.type == EventType.Repaint)
			{
				GameArt.Label(new Rect(4f * k, top + 2f * k, width - 8f * k, rowH - 4f * k), "Today's notices (" + (Calendar.DateText ?? "today") + "): they come down at dawn", 5f, TextAnchor.MiddleLeft, GameArt.Orange);
			}
			Sprite gold = GameArt.IconNamed("iconMoney") ?? GameArt.IconNamed("iconGold");
			int row = 1;
			foreach (Quest item in taken)
			{
				Notice(item, mine: true);
			}
			foreach (Quest item2 in today)
			{
				Notice(item2, mine: false);
			}
			void Notice(Quest q, bool mine)
			{
				Rect r = new Rect(0f, top + (float)row * (rowH + gap), width, rowH);
				row++;
				bool fromBoard = q.FromBoard;
				Actor actor = (fromBoard ? null : Chronicle.Find(q.Giver));
				if (Event.current.type == EventType.Repaint)
				{
					GameArt.Sliced(r, GameArt.Inner, mine ? new Color(0.85f, 1f, 0.85f, 1f) : new Color(0.96f, 0.93f, 0.84f, 1f));
					Sprite s = null;
					try
					{
						if (actor != null && actor.isAlive())
						{
							s = actor.getSpriteToRender();
						}
					}
					catch
					{
					}
					PortraitWell(new Rect(r.x + 3f * k, r.y + 3f * k, rowH - 6f * k, rowH - 6f * k), s);
					float x = r.x + rowH + 2f * k;
					GameArt.Label(new Rect(x, r.y + 2.5f * k, r.width * 0.56f, 10f * k), Quests.Title(q), 5.4f, TextAnchor.MiddleLeft, Color.white);
					GameArt.Label(new Rect(x, r.y + 13.5f * k, r.width * 0.58f, 9f * k), mine ? Quests.Status(q) : ((Quests.LevelOfKind(q.Kind) > 1) ? $"Level {Quests.LevelOfKind(q.Kind)}: the town asks it" : (fromBoard ? "Posted by the town" : ("Pinned up by " + q.GiverName))), 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
					float num = r.xMax - 66f * k;
					GameArt.Icon(new Rect(num - 44f * k, r.y + 8f * k, 11f * k, 11f * k), gold);
					GameArt.Label(new Rect(num - 32f * k, r.y + 7f * k, 30f * k, 12f * k), q.Money.ToString(), 5.4f, TextAnchor.MiddleLeft, GameArt.Orange);
				}
				Rect r2 = new Rect(r.xMax - 60f * k, r.y + (rowH - 15f * k) * 0.5f, 54f * k, 15f * k);
				if (mine && fromBoard && q.Stage == 1)
				{
					if (GameButton(r2, "Hand in", red: true, enabled: true, null, 5f, _listMouseIn))
					{
						Quests.HandInAtBoard(b.City, q, you, _mod.Toast);
					}
				}
				else if (mine)
				{
					if (Event.current.type == EventType.Repaint)
					{
						StateTag(r2.x + 8f * k, r2.y + 3f * k, (q.Stage == 1) ? "Done" : "Taken", new Color(0.55f, 1f, 0.5f));
					}
				}
				else if (GameButton(r2, "Take", red: true, Quests.Log.Count < 5 && (actor != null || fromBoard), null, 5f, _listMouseIn))
				{
					Quests.TakeNotice(b.City, q, you, _mod.Toast);
				}
			}
		}

		private float DrawCaptureBar(float sw, float below)
		{
			float unscaledTime = Time.unscaledTime;
			float p = GameArt.P;
			if (unscaledTime - _capLookedAt > 0.25f)
			{
				_capLookedAt = unscaledTime;
				FindCapture(_mod.Host);
			}
			bool flag = _capCity != null && _capBy != null && _capTicks > 0f;
			_capAlpha = Mathf.MoveTowards(_capAlpha, flag ? 1f : 0f, Time.unscaledDeltaTime * (flag ? 5f : 2f));
			if (_capAlpha <= 0.01f || _capText == null)
			{
				CaptureLineForTest = null;
				CaptureFillForTest = -1f;
				return below;
			}
			float num = Mathf.Clamp01(_capTicks / 100f);
			_capShown = ((Mathf.Abs(_capShown - num) > 0.5f) ? num : Mathf.MoveTowards(_capShown, num, Time.unscaledDeltaTime * 0.6f));
			float num2 = Smooth(_capAlpha);
			Skin.Ensure();
			float a = Mathf.Round(Mathf.Max(150f * p, GameArt.Measure(_capText, 4.8f).x + 40f * p));
			a = Mathf.Min(a, sw - 20f * p);
			float height = Mathf.Round(22f * p);
			float f = Mathf.Max(below, CompassBottom + 2f * p);
			Rect r = new Rect(Mathf.Round((sw - a) * 0.5f), Mathf.Round(f), a, height);
			GameArt.TipPanel(r, num2);
			if (_capLosing)
			{
				GameArt.Sliced(r, GameArt.ButtonBorder, new Color(1f, 0.3f, 0.2f, (0.55f + 0.45f * Mathf.Sin(unscaledTime * 6f)) * num2), 4f, 4f, 4f, 4f);
			}
			float num3 = Mathf.Round(16f * p);
			Rect r2 = new Rect(r.x + 4f * p, Mathf.Round(r.center.y - num3 * 0.5f), num3, num3);
			bool civ = false;
			try
			{
				civ = _capBy != null && _capBy.isCiv();
			}
			catch
			{
			}
			if (!DrawBanner(r2, _capBy, civ, num2))
			{
				GameArt.Icon(r2, GameArt.Warfare, new Color(1f, 1f, 1f, num2));
			}
			float num4 = r2.xMax + 4f * p;
			float num5 = r.xMax - 6f * p - num4;
			GameArt.Label(new Rect(num4, r.y + 1f * p, num5, 9f * p), _capText, 4.8f, TextAnchor.MiddleLeft, new Color(_capColor.r, _capColor.g, _capColor.b, num2));
			GameArt.Bar(new Rect(num4, Mathf.Round(r.y + 12f * p), Mathf.Round(num5), Mathf.Round(6f * p)), text: Mathf.RoundToInt(_capShown * 100f) + "%", frac: _capShown, fill: _capColor, trail: 0f, alpha: num2, textSize: 4.2f);
			CaptureLineForTest = _capText;
			CaptureFillForTest = _capShown;
			return Mathf.Round(r.yMax + 3f * p);
		}

		private void FindCapture(Actor host)
		{
			City city = null;
			Kingdom kingdom = null;
			float capTicks = 0f;
			float num = 900f;
			if (host != null)
			{
				City city2 = null;
				Vector2 vector = Vector2.zero;
				try
				{
					vector = host.current_position;
					WorldTile tile = World.world.GetTile((int)vector.x, (int)vector.y);
					city2 = ((tile != null && tile.zone != null) ? tile.zone.city : null);
				}
				catch
				{
				}
				try
				{
					foreach (City item in World.world.cities.list)
					{
						if (item == null || !item.isAlive())
						{
							continue;
						}
						Kingdom kingdom2 = item.being_captured_by;
						float captureTicks = item.getCaptureTicks();
						if (kingdom2 == null && item.isGettingCaptured())
						{
							kingdom2 = item.getCapturingKingdom();
						}
						if (kingdom2 != null && !(captureTicks <= 0f) && kingdom2.isAlive())
						{
							Vector2 vector2 = ((item.city_center != Vector2.zero) ? item.city_center : item.last_city_center);
							float num2 = ((item == city2) ? (-1f) : (vector2 - vector).sqrMagnitude);
							if (!(num2 >= num))
							{
								num = num2;
								city = item;
								kingdom = kingdom2;
								capTicks = captureTicks;
							}
						}
					}
				}
				catch
				{
				}
			}
			if (city == null)
			{
				_capCity = null;
				return;
			}
			_capCity = city;
			_capBy = kingdom;
			_capTicks = capTicks;
			Kingdom kingdom3 = null;
			try
			{
				kingdom3 = host.kingdom;
			}
			catch
			{
			}
			string text = "the town";
			string text2 = Royal.NameOf(kingdom);
			try
			{
				if (!string.IsNullOrEmpty(city.name))
				{
					text = city.name;
				}
			}
			catch
			{
			}
			_capLosing = false;
			if (kingdom3 != null && SameSideAs(kingdom3, kingdom))
			{
				_capText = "Taking " + text;
				_capColor = new Color(0.45f, 0.86f, 0.3f);
			}
			else if (kingdom3 != null && SameSideAs(kingdom3, city.kingdom))
			{
				_capText = text + " is falling to " + text2;
				_capColor = new Color(0.95f, 0.3f, 0.2f);
				_capLosing = true;
			}
			else
			{
				_capText = text2 + " is taking " + text;
				_capColor = GameArt.AgeYellow;
			}
		}

		private static bool SameSideAs(Kingdom mine, Kingdom other)
		{
			try
			{
				if (mine == null || other == null)
				{
					return false;
				}
				if (other == mine)
				{
					return true;
				}
				return mine.hasAlliance() && other.hasAlliance() && mine.getAlliance() == other.getAlliance();
			}
			catch
			{
				return false;
			}
		}

		private void DrawDismantleBar(float sw, float sh)
		{
			Building building = Dismantle.Current();
			if (building == null)
			{
				DismantleFillForTest = -1f;
				_dismantleFor = null;
				return;
			}
			float num = 0f;
			try
			{
				num = 1f - Mathf.Clamp01((float)building.getHealth() / (float)Mathf.Max(1, building.getMaxHealth()));
			}
			catch
			{
			}
			if (building != _dismantleFor)
			{
				_dismantleFor = building;
				_dismantleShown = num;
			}
			_dismantleShown = Mathf.MoveTowards(_dismantleShown, num, Time.unscaledDeltaTime * 1.5f);
			float num2 = Mathf.Clamp01((6f - (Time.unscaledTime - Dismantle.At)) / 0.5f);
			if (num2 <= 0f)
			{
				DismantleFillForTest = -1f;
				return;
			}
			float p = GameArt.P;
			float num3 = Mathf.Round(64f * p);
			float height = Mathf.Round(9f * p);
			Rect r = new Rect(Mathf.Round(sw * 0.5f - num3 * 0.5f + 3f * p), Mathf.Round(sh * 0.5f + sh * 0.045f), num3, height);
			GameArt.Sliced(new Rect(r.x - 2f * p, r.y - 1.5f * p, r.width + 4f * p, r.height + 3f * p), GameArt.Inner12, new Color(0.83f, 0.83f, 0.83f, num2));
			GameArt.Bar(r, _dismantleShown, new Color(0.93f, 0.62f, 0.2f), 0f, Mathf.RoundToInt(_dismantleShown * 100f) + "%", num2, 4.4f);
			if (!_demolishLooked)
			{
				_demolishLooked = true;
				try
				{
					_demolishIcon = GameArt.IconNamed("iconDemolish");
				}
				catch
				{
				}
			}
			Rect r2 = new Rect(Mathf.Round(r.x - 9f * p), Mathf.Round(r.center.y - 7f * p), 14f * p, 14f * p);
			if (_demolishIcon != null)
			{
				GameArt.Icon(new Rect(r2.x + p * 0.75f, r2.y + p * 0.75f, r2.width, r2.height), _demolishIcon, new Color(0f, 0f, 0f, 0.5f * num2));
				GameArt.Icon(r2, _demolishIcon, new Color(1f, 1f, 1f, num2));
			}
			DismantleFillForTest = _dismantleShown;
		}

		private static void EnsureShelves()
		{
			if (_gear != null)
			{
				return;
			}
			_gear = new List<EquipmentAsset>[3]
			{
				new List<EquipmentAsset>(),
				new List<EquipmentAsset>(),
				new List<EquipmentAsset>()
			};
			_traits = new List<ActorTrait>();
			try
			{
				foreach (EquipmentAsset item in AssetManager.items.list)
				{
					if (item != null && !string.IsNullOrEmpty(item.id) && item.id[0] != '$' && item.show_in_meta_editor && !(IconOf(item) == null))
					{
						switch (item.equipment_type)
						{
						case EquipmentType.Weapon:
							_gear[0].Add(item);
							break;
						case EquipmentType.Helmet:
						case EquipmentType.Armor:
						case EquipmentType.Boots:
							_gear[1].Add(item);
							break;
						case EquipmentType.Ring:
						case EquipmentType.Amulet:
							_gear[2].Add(item);
							break;
						}
					}
				}
				List<EquipmentAsset>[] gear = _gear;
				for (int i = 0; i < gear.Length; i++)
				{
					gear[i].Sort(delegate(EquipmentAsset x, EquipmentAsset y)
					{
						int equipment_type = (int)x.equipment_type;
						int num = equipment_type.CompareTo((int)y.equipment_type);
						return (num == 0) ? x.equipment_value.CompareTo(y.equipment_value) : num;
					});
				}
				foreach (ActorTrait item2 in AssetManager.traits.list)
				{
					if (item2 != null && !string.IsNullOrEmpty(item2.id) && item2.can_be_given)
					{
						Sprite sprite = null;
						try
						{
							sprite = item2.getSprite();
						}
						catch
						{
						}
						if (sprite != null)
						{
							_traits.Add(item2);
						}
					}
				}
				_traits.Sort(delegate(ActorTrait x, ActorTrait y)
				{
					int type = (int)x.type;
					int num = type.CompareTo((int)y.type);
					return (num == 0) ? string.CompareOrdinal(TraitName(x), TraitName(y)) : num;
				});
			}
			catch (Exception ex)
			{
				Log.Error("character tab: could not read the items and traits: " + ex.Message);
			}
			Log.Info("character tab: " + _gear[0].Count + " weapons, " + _gear[1].Count + " armor, " + _gear[2].Count + " jewelry, " + _traits.Count + " traits");
		}

		internal int ShelfCountForTest(int shelf)
		{
			EnsureShelves();
			return ShelfCount(shelf);
		}

		internal EquipmentAsset ShelfItemForTest(int shelf, EquipmentType type)
		{
			EnsureShelves();
			return _gear[shelf].Find((EquipmentAsset a) => a.equipment_type == type);
		}

		private static string TraitName(ActorTrait t)
		{
			try
			{
				string translatedName = t.getTranslatedName();
				if (!string.IsNullOrEmpty(translatedName) && !translatedName.StartsWith("$"))
				{
					return translatedName;
				}
			}
			catch
			{
			}
			return t.id.Replace('_', ' ');
		}

		private static int ShelfColumns(float cw)
		{
			return Mathf.Max(1, Mathf.FloorToInt((cw - 10f * GameArt.P + 2f * GameArt.P) / (ShelfCell + 2f * GameArt.P)));
		}

		private int ShelfCount(int shelf)
		{
			if (shelf != 3)
			{
				return _gear[Mathf.Clamp(shelf, 0, 2)].Count;
			}
			return _traits.Count;
		}

		private float CharacterShelfHeight(SettingsMenu menu, float cw)
		{
			float p = GameArt.P;
			Actor host = _mod.Host;
			bool flag = false;
			try
			{
				flag = host?.isAlive() ?? false;
			}
			catch
			{
			}
			if (!flag || Run.Locked)
			{
				return 34f * p;
			}
			EnsureShelves();
			int num = Mathf.Max(1, Mathf.CeilToInt((float)ShelfCount(menu.Shelf) / (float)ShelfColumns(cw)));
			return 16f * p + 15f * p + 4f * p + 14f * p + (float)num * (ShelfCell + 2f * p) + 8f * p + 3f * p + CheatsHeight;
		}

		private float DrawCharacterShelf(float cx, float y, float cw, SettingsMenu menu)
		{
			float p = GameArt.P;
			Actor host = _mod.Host;
			bool flag = Event.current.type == EventType.Repaint;
			Vector2 mousePosition = Event.current.mousePosition;
			if (host == null || !host.isAlive())
			{
				Rect r = new Rect(cx, y, cw, 31f * p);
				GameArt.Sliced(r, GameArt.Inner, new Color(1f, 1f, 1f, 0.56f));
				GameArt.Label(new Rect(r.x + 8f * p, r.y, r.width - 16f * p, r.height), "Take over a creature (dive into one from the map) to give it weapons, armour and traits here.", 4.6f, TextAnchor.MiddleCenter, GameArt.Grey, wrap: true);
				return y + 34f * p;
			}
			if (Run.Locked)
			{
				Rect r2 = new Rect(cx, y, cw, 31f * p);
				GameArt.Sliced(r2, GameArt.Inner, new Color(1f, 1f, 1f, 0.56f));
				GameArt.Label(new Rect(r2.x + 8f * p, r2.y, r2.width - 16f * p, r2.height), "Sandbox only. In a locked-in life, buy gear from traders and learn traits from teachers.", 4.6f, TextAnchor.MiddleCenter, GameArt.Grey, wrap: true);
				return y + 34f * p;
			}
			EnsureShelves();
			int num = Mathf.Clamp(menu.Shelf, 0, 3);
			string text = "";
			try
			{
				text = host.getName();
			}
			catch
			{
			}
			GameArt.Label(new Rect(cx + 2f * p, y, cw * 0.45f, 14f * p), text, 5.6f, TextAnchor.MiddleLeft, GameArt.Orange);
			bool flag2 = true;
			try
			{
				flag2 = host.equipment != null && host.understandsHowToUseItems();
			}
			catch
			{
				flag2 = false;
			}
			string text2 = _shelfHover ?? ((num < 3 && !flag2) ? "This creature can't use items" : "Click to give, click again to take away");
			GameArt.Label(new Rect(cx + cw * 0.45f, y, cw * 0.55f - 2f * p, 14f * p), text2, 4.4f, TextAnchor.MiddleRight, (_shelfHover != null) ? Color.white : GameArt.Grey);
			y += 16f * p;
			if (flag)
			{
				_shelfHover = null;
			}
			float num2 = 2f * p;
			float num3 = (cw - num2 * 3f) / 4f;
			for (int i = 0; i < 4; i++)
			{
				Rect r3 = new Rect(Mathf.Round(cx + (float)i * (num3 + num2)), y, Mathf.Round(num3), Mathf.Round(15f * p));
				bool flag3 = _pageMouseIn && r3.Contains(mousePosition);
				GameArt.FlatButton(r3, i == num, ShelfNames[i], 4.8f, (flag3 || i == num) ? 1f : 0.86f, GameArt.IconNamed(ShelfIcons[i]));
				if (PageButton(r3) && menu.Shelf != i)
				{
					menu.Shelf = i;
				}
			}
			y += 15f * p + 4f * p;
			GameArt.Label(new Rect(cx + 2f * p, y, cw - 4f * p, 12f * p), ShelfSummary(host, num), 4.4f, TextAnchor.MiddleLeft, GameArt.Peach);
			y += 14f * p;
			int num4 = ShelfColumns(cw);
			int num5 = ShelfCount(num);
			int num6 = Mathf.Max(1, Mathf.CeilToInt((float)num5 / (float)num4));
			float shelfCell = ShelfCell;
			float num7 = shelfCell + 2f * p;
			Rect r4 = new Rect(cx, y, cw, (float)num6 * num7 + 8f * p);
			GameArt.Sliced(r4, GameArt.Inner, new Color(1f, 1f, 1f, 0.56f));
			float num8 = cx + Mathf.Round((cw - ((float)num4 * num7 - 2f * p)) * 0.5f);
			float num9 = y + 4f * p;
			for (int j = 0; j < num5; j++)
			{
				Rect r5 = new Rect(num8 + (float)(j % num4) * num7, num9 + (float)(j / num4) * num7, shelfCell, shelfCell);
				bool flag4 = _pageMouseIn && r5.Contains(mousePosition);
				bool flag5;
				Sprite s;
				string text3;
				if (num == 3)
				{
					ActorTrait actorTrait = _traits[j];
					flag5 = Has(host, actorTrait);
					try
					{
						s = actorTrait.getSprite();
					}
					catch
					{
						s = null;
					}
					text3 = TraitName(actorTrait);
				}
				else
				{
					EquipmentAsset equipmentAsset = _gear[num][j];
					flag5 = Wearing(host, equipmentAsset);
					s = IconOf(equipmentAsset);
					text3 = NiceName(equipmentAsset) + "  (" + equipmentAsset.equipment_type.ToString() + ")";
				}
				if (flag)
				{
					bool flag6 = num < 3 && !flag2;
					GameArt.Sliced(r5, GameArt.Inner, (flag4 && !flag6) ? Color.white : new Color(0.86f, 0.86f, 0.86f, flag6 ? 0.45f : 1f));
					if (flag5)
					{
						GameArt.Sliced(r5, GameArt.ButtonBorder, new Color(1f, 0.82f, 0.3f, 1f), 4f, 4f, 4f, 4f);
					}
					Color color = GUI.color;
					if (flag6)
					{
						GUI.color = new Color(color.r, color.g, color.b, color.a * 0.35f);
					}
					DrawSprite(new Rect(r5.x + 3f * p, r5.y + 3f * p, r5.width - 6f * p, r5.height - 6f * p), s);
					GUI.color = color;
					if (flag4)
					{
						_shelfHover = ((num < 3 && !flag2) ? "This creature can't use items" : (text3 + (flag5 ? " - click to take away" : " - click to give")));
					}
				}
				if (PageButton(r5))
				{
					if (num == 3)
					{
						ToggleTrait(host, _traits[j]);
					}
					else
					{
						ToggleItem(host, _gear[num][j]);
					}
				}
			}
			return DrawCheats(cx, r4.yMax + 3f * p, cw, host);
		}

		private float DrawCheats(float cx, float y, float cw, Actor host)
		{
			float p = GameArt.P;
			Vector2 mousePosition = Event.current.mousePosition;
			y += 4f * p;
			City city = CheatTown(host);
			string text = ((city == null) ? "no town here" : (Rep.NameOf(city) + ": " + Rep.Level(host, city).ToString() + " (" + Rep.Points(host, city) + ")"));
			GameArt.Label(new Rect(cx + 2f * p, y, cw - 4f * p, 12f * p), "Cheats    Gold " + Economy.Money(host) + "    Reputation " + text, 4.4f, TextAnchor.MiddleLeft, GameArt.Peach);
			y += 14f * p;
			float num = 2f * p;
			float num2 = (cw - num * 3f) / 4f;
			for (int i = 0; i < CheatNames.Length; i++)
			{
				Rect r = new Rect(Mathf.Round(cx + (float)i * (num2 + num)), y, Mathf.Round(num2), Mathf.Round(15f * p));
				bool flag = _pageMouseIn && r.Contains(mousePosition);
				GameArt.FlatButton(r, red: false, CheatNames[i], 4.8f, flag ? 1f : 0.86f, GameArt.IconNamed((i < 2) ? "iconMoney" : "iconKingdom"));
				if (PageButton(r))
				{
					Cheat(host, i);
				}
			}
			return y + 15f * p + 3f * p;
		}

		internal static City CheatTown(Actor host)
		{
			City city = null;
			try
			{
				city = Hub.TownAt(host.current_position);
			}
			catch
			{
			}
			return city ?? Freedom.HomeTown(host);
		}

		internal void Cheat(Actor host, int which)
		{
			try
			{
				if (which < 2)
				{
					int pValue = ((which == 0) ? 100 : 1000);
					host.addMoney(pValue);
					_mod.Toast("+" + pValue + " gold.");
					Log.Info("character tab: cheat +" + pValue + " gold to " + WorldBoxMod.Describe(host));
					return;
				}
				City city = CheatTown(host);
				if (city == null)
				{
					_mod.Toast("Stand in a town, or join one, for reputation.");
					return;
				}
				int amount = ((which == 2) ? 500 : (-500));
				Rep.Add(host, city, amount, "god's favour");
				Log.Info("character tab: cheat " + amount + " reputation in " + Rep.NameOf(city) + " for " + WorldBoxMod.Describe(host));
			}
			catch (Exception ex)
			{
				Log.Error("character tab: cheat " + which + " failed: " + ex.Message);
			}
		}

		private string ShelfSummary(Actor host, int shelf)
		{
			object obj;
			switch (shelf)
			{
			case 3:
			{
				int num = 0;
				try
				{
					num = host.getTraits().Count;
				}
				catch
				{
				}
				return num + ((num == 1) ? " trait" : " traits");
			}
			default:
				obj = new EquipmentType[2]
				{
					EquipmentType.Ring,
					EquipmentType.Amulet
				};
				break;
			case 1:
				obj = new EquipmentType[3]
				{
					EquipmentType.Helmet,
					EquipmentType.Armor,
					EquipmentType.Boots
				};
				break;
			case 0:
				obj = new EquipmentType[1];
				break;
			}
			List<string> list = new List<string>();
			EquipmentType[] array = (EquipmentType[])obj;
			for (int i = 0; i < array.Length; i++)
			{
				EquipmentType pType = array[i];
				Item item = null;
				try
				{
					item = host.equipment.getSlot(pType).getItem();
				}
				catch
				{
				}
				list.Add(pType.ToString() + ": " + ((item != null) ? NiceName(item.asset) : "-"));
			}
			return string.Join("    ", list.ToArray());
		}

		private static bool Has(Actor host, ActorTrait t)
		{
			try
			{
				return host.hasTrait(t);
			}
			catch
			{
				return false;
			}
		}

		private static bool Wearing(Actor host, EquipmentAsset a)
		{
			try
			{
				return host.equipment.getSlot(a.equipment_type).getItem()?.asset == a;
			}
			catch
			{
				return false;
			}
		}

		internal void ToggleTrait(Actor host, ActorTrait t)
		{
			try
			{
				if (host.hasTrait(t))
				{
					host.removeTrait(t);
					_mod.Toast("Took away " + TraitName(t) + ".");
				}
				else if (host.addTrait(t, pRemoveOpposites: true))
				{
					TraitSwitch.Earned(host, t.id);
					_mod.Toast("You are now " + TraitName(t) + ".");
				}
				else
				{
					_mod.Toast(TraitName(t) + " won't take on this creature.");
				}
				host.setStatsDirty();
				Log.Info("character tab: trait " + t.id + " " + (host.hasTrait(t) ? "given to " : "taken from ") + WorldBoxMod.Describe(host));
			}
			catch (Exception ex)
			{
				Log.Error("character tab: trait " + t.id + " failed: " + ex.Message);
			}
		}

		private static int SpareOf(Actor host, EquipmentAsset a)
		{
			List<Gear.Piece> list = Gear.Of(host);
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i].Asset == a)
				{
					return i;
				}
			}
			return -1;
		}

		internal void ToggleItem(Actor host, EquipmentAsset a)
		{
			try
			{
				if (host.equipment == null || !host.understandsHowToUseItems())
				{
					_mod.Toast("This creature can't use items.");
					return;
				}
				ActorEquipmentSlot slot = host.equipment.getSlot(a.equipment_type);
				Item item = slot.getItem();
				int num = ((item != null && item.asset == a) ? (-1) : SpareOf(host, a));
				if (item != null && item.asset == a)
				{
					slot.takeAwayItem();
					Gear.LetGo(item);
					_mod.Toast("Took away the " + NiceName(a) + ".");
				}
				else if (num >= 0)
				{
					int num2 = BagOrder.Of(host).IndexOf(BagOrder.PieceKey(num));
					if (a.equipment_type == EquipmentType.Weapon && num2 >= 0)
					{
						Hotbar.Hold(host, num2, _mod.Toast);
					}
					else
					{
						int num3 = Gear.Count(host);
						if (Gear.PutOn(host, num, _mod.Toast, quiet: true) && Gear.Count(host) < num3)
						{
							BagOrder.PieceGone(host, num);
						}
					}
					_mod.Toast(Wearing(host, a) ? ("You now have " + NiceName(a) + " (from your bag).") : "That won't go on this creature.");
				}
				else
				{
					Item item2 = World.world.items.generateItem(a, host.kingdom, host.getName(), 1, host, 0, pByPlayer: true);
					if (item2 == null)
					{
						_mod.Toast("That won't go on this creature.");
						return;
					}
					if (item != null)
					{
						Gear.Piece piece = Gear.FromItem(item);
						bool value = Bag.Free(host) > 0;
						slot.takeAwayItem();
						Gear.LetGo(item);
						if (piece != null)
						{
							Bag.PutPiece(host, piece, null, value);
						}
					}
					slot.setItem(item2, host);
					_mod.Toast("You now have " + NiceName(a) + ".");
				}
				host.setStatsDirty();
				Log.Info("character tab: " + a.id + (Wearing(host, a) ? " put on " : " taken off ") + WorldBoxMod.Describe(host));
			}
			catch (Exception ex)
			{
				Log.Error("character tab: " + a.id + " failed: " + ex.Message);
				_mod.Toast("That won't go on this creature.");
			}
		}

		private void DrawHeldCompass(float sw, float sh)
		{
			CompassReadoutShown = (CompassMarkShown = false);
			if (Event.current.type != EventType.Repaint)
			{
				return;
			}
			CompassState compass = _mod.Compass;
			if (!compass.Out || !compass.Shown || compass.Lid < 0.8f || VrView)
			{
				return;
			}
			float num = Mathf.Clamp01((compass.Lid - 0.8f) / 0.7f);
			FrameProjection shown = _mod.Shown;
			if (shown.Width <= 0 || shown.Height <= 0)
			{
				return;
			}
			float p = GameArt.P;
			WorldBoxMod.CompassMark? compassOn = _mod.CompassOn;
			if (!shown.Project(compass.At.X, compass.At.Y, compass.At.Z, out var col, out var row, out var _))
			{
				return;
			}
			float num2 = col / (float)shown.Width * sw;
			float num3 = row / (float)shown.Height * sh;
			if (compassOn.HasValue)
			{
				WorldBoxMod.CompassMark valueOrDefault = compassOn.GetValueOrDefault();
				if (!MarkedElsewhere(valueOrDefault))
				{
					DrawCompassMark(sw, sh, shown, valueOrDefault, num, num2, num3);
				}
			}
			string text = ((!compassOn.HasValue) ? "No tasks" : compassOn.GetValueOrDefault().Name);
			object obj;
			if (compassOn.HasValue)
			{
				WorldBoxMod.CompassMark valueOrDefault2 = compassOn.GetValueOrDefault();
				obj = ((Mathf.RoundToInt(valueOrDefault2.Distance) < 2) ? "Here" : (Mathf.RoundToInt(valueOrDefault2.Distance) + " tiles")) + ((_mod.CompassCount > 1) ? ("  ·  " + (_mod.CompassIndex + 1) + "/" + _mod.CompassCount) : "");
			}
			else
			{
				obj = null;
			}
			string text2 = (string)obj;
			float size = 5f;
			float size2 = 4.4f;
			float a = GameArt.Measure(text, size).x;
			if (text2 != null)
			{
				a = Mathf.Max(a, GameArt.Measure(text2, size2).x);
			}
			a = Mathf.Min(a, sw * 0.36f) + 10f * p;
			float num4 = ((text2 != null) ? 19f : 11f) * p;
			float num5 = num2 - sh * 0.085f;
			Rect r = new Rect(Mathf.Round(Mathf.Max(4f * p, num5 - a)), Mathf.Round(num3 - num4 * 0.5f), Mathf.Round(a), Mathf.Round(num4));
			GameArt.TipPanel(r, 0.85f * num);
			Color color = compassOn?.Color ?? new Color(0.8f, 0.8f, 0.8f);
			GameArt.Label(new Rect(r.x + 5f * p, r.y + 1f * p, r.width - 10f * p, 9f * p), text, size, TextAnchor.MiddleRight, new Color(color.r, color.g, color.b, num));
			if (text2 != null)
			{
				GameArt.Label(new Rect(r.x + 5f * p, r.y + 9.5f * p, r.width - 10f * p, 8f * p), text2, size2, TextAnchor.MiddleRight, new Color(1f, 1f, 1f, 0.9f * num));
			}
			CompassReadoutShown = true;
		}

		private bool MarkedElsewhere(WorldBoxMod.CompassMark m)
		{
			if (m.Key.StartsWith("g"))
			{
				return true;
			}
			if (m.Actor != null)
			{
				foreach (KeyValuePair<Actor, bool> item in _starred)
				{
					if (item.Key == m.Actor)
					{
						return true;
					}
				}
			}
			return false;
		}

		private void DrawCompassMark(float sw, float sh, FrameProjection r, WorldBoxMod.CompassMark m, float alpha, float cx, float cy)
		{
			float p = GameArt.P;
			Vector3 vector = new Vector3(m.At.x, m.At.y, 0f);
			try
			{
				vector.z = _mod.Land.SampleHeight(m.At.x, m.At.y);
			}
			catch
			{
			}
			Vector3 vector2 = vector + new Vector3(0f, 0f, 1.2f);
			if (m.Actor != null && _mod.StarHead(m.Actor, out var head))
			{
				vector2 = new Vector3(head.x, head.y, head.z + 0.55f);
				vector = new Vector3(head.x, head.y, head.z + 0.25f);
			}
			if (!r.Project(vector2.x, vector2.y, vector2.z, out var col, out var row, out var _))
			{
				return;
			}
			if (!r.Project(vector.x, vector.y, vector.z, out var col2, out var row2, out var _))
			{
				col2 = col;
				row2 = row;
			}
			float num = col / (float)r.Width * sw;
			float num2 = row / (float)r.Height * sh;
			float num3 = row2 / (float)r.Height * sh;
			float num4 = Mathf.Round(11f * p);
			if (!(num < num4) && !(num > sw - num4) && !(num2 < num4 * 2f) && !(num2 > sh - num4) && (!(Mathf.Abs(num - cx) < sh * 0.12f) || !(num2 > cy - sh * 0.34f)))
			{
				if (_star == null)
				{
					_star = MakeStar();
				}
				float num5 = Mathf.Max(1f, Mathf.Round(0.8f * p));
				float num6 = num2 + num4 * 0.45f;
				float height = Mathf.Max(0f, num3 - num6);
				GameArt.Flat(new Rect(Mathf.Round(num - num5 * 0.5f) + 1f, num6 + 1f, num5, height), new Color(0f, 0f, 0f, 0.45f * alpha));
				GameArt.Flat(new Rect(Mathf.Round(num - num5 * 0.5f), num6, num5, height), new Color(m.Color.r, m.Color.g, m.Color.b, 0.8f * alpha));
				float num7 = Mathf.Sin(Time.unscaledTime * 2.6f) * 1.2f * p;
				Color color = GUI.color;
				GUI.color = new Color(m.Color.r, m.Color.g, m.Color.b, alpha);
				GUI.DrawTexture(new Rect(Mathf.Round(num - num4 * 0.5f), Mathf.Round(num2 - num4 * 0.5f + num7), num4, num4), _star);
				GUI.color = color;
				GameArt.Label(new Rect(num - 30f * p, num2 - num4 * 0.5f - 9f * p + num7, 60f * p, 8f * p), Mathf.RoundToInt(m.Distance).ToString(), 4.6f, TextAnchor.LowerCenter, new Color(1f, 1f, 1f, 0.95f * alpha));
				CompassMarkShown = true;
			}
		}

		public void DrawTalk(Conversation c, string hint = null, bool mouse = true)
		{
			EnsureAssets();
			GameArt.Ensure();
			GameArt.EnsureWindowArt();
			float num = Screen.width;
			float num2 = Screen.height;
			float p = GameArt.P;
			bool flag = c.Panel == TalkPanel.Family && c.Tree != null;
			float num3 = Mathf.Round(Mathf.Min(num * 0.62f, 380f * p));
			int count = c.Choices.Count;
			bool flag2 = count > 6;
			float num4 = Mathf.Round(15f * p);
			float num5 = Mathf.Round(3f * p);
			int num6 = (flag2 ? ((count + 1) / 2) : count);
			float num7 = Mathf.Round(40f * p);
			float num8 = Mathf.Round(26f * p);
			float num9 = num2 - 46f * p;
			float num10 = Mathf.Round(flag ? (num2 * 0.36f) : Mathf.Max(num2 * 0.04f, 46f * p));
			int b = Mathf.FloorToInt((num9 - 6f * p - num7 - num8 + 3f * p - num10) / (num4 + num5));
			int num11 = Mathf.Min(num6, Mathf.Max(2, b));
			bool flag3 = num11 < num6;
			if (!flag3 || c.Sets != _talkSets)
			{
				_talkFirst = 0;
			}
			if (flag3 && (c.Selected != _talkPicked || c.Sets != _talkSets))
			{
				int num12 = (flag2 ? (c.Selected % num6) : c.Selected);
				if (num12 < _talkFirst)
				{
					_talkFirst = num12;
				}
				else if (num12 >= _talkFirst + num11)
				{
					_talkFirst = num12 - num11 + 1;
				}
			}
			_talkFirst = Mathf.Clamp(_talkFirst, 0, num6 - num11);
			_talkPicked = c.Selected;
			_talkSets = c.Sets;
			TalkRowsShown = num11;
			TalkFirstRow = _talkFirst;
			float num13 = Mathf.Round(Mathf.Min(b: num9 - (float)num11 * (num4 + num5) - num7 - 6f * p, a: num2 * (flag ? 0.66f : 0.58f)));
			float num14 = Mathf.Round((num - num3) * 0.5f);
			if (flag)
			{
				DrawFamilyTree(c, new Rect(num14, Mathf.Round(num2 * 0.06f), num3, num13 - num8 - num2 * 0.06f - 4f * p));
			}
			Rect rect = new Rect(num14, num13 - num8 + 3f * p, Mathf.Round(Mathf.Min(num3 * 0.75f, 230f * p)), num8);
			TalkCardTop = (flag ? 0f : (rect.y / num2));
			GameArt.Sliced(rect, GameArt.Spectate, Color.white);
			GameArt.Sliced(rect, GameArt.ButtonBorder, Color.white, 4f, 4f, 4f, 4f);
			Sprite s = null;
			try
			{
				if (c.With != null && c.With.isAlive())
				{
					s = c.With.getSpriteToRender();
				}
			}
			catch
			{
			}
			Rect r = new Rect(rect.x + 3f * p, rect.y + 3f * p, num8 - 6f * p, num8 - 6f * p);
			PortraitWell(r, s, c.With);
			if (Event.current.type == EventType.Repaint)
			{
				Color c2 = Dialogue.MoodTint(c);
				if (c2.a > 0f)
				{
					GameArt.Flat(r, c2);
				}
			}
			Color color = Color.white;
			try
			{
				if (c.With != null && c.With.kingdom != null && c.With.kingdom.isCiv())
				{
					color = c.With.kingdom.getColor().getColorText();
				}
			}
			catch
			{
			}
			float num15 = rect.x + num8 + 2f * p;
			float width = TalkStar(c, rect).x - num15 - 3f * p;
			GameArt.Label(new Rect(num15, rect.y + 1.5f * p, width, 11f * p), c.Shown ?? "", 6.5f, TextAnchor.MiddleLeft, color);
			string text = CardRelation(c.Relation, c.CardSuffix);
			string text2 = Births.ExpectingLabel(c.With);
			if (text2 != null)
			{
				text = ((text != null) ? (text + "  ·  ") : "") + text2;
			}
			if (text != null)
			{
				GameArt.Label(new Rect(num15, rect.y + 13f * p, width, 9f * p), text, 4.4f, TextAnchor.MiddleLeft, GameArt.Grey);
			}
			if (!flag)
			{
				DrawTalkMeters(c, rect);
			}
			Rect r2 = new Rect(num14, num13, num3, num7);
			GameArt.TipPanel(r2);
			string text3 = c.Line ?? "";
			int letters = Mathf.Clamp((int)(c.Age * 55f), 0, text3.Length);
			if (c.Thinking)
			{
				GameArt.Label(new Rect(r2.x + 8f * p, r2.y + 5f * p, r2.width - 16f * p, r2.height - 9f * p), "*thinking" + new string('.', 1 + (int)(Time.unscaledTime * 2.5f) % 3) + "*", 5.4f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
			}
			else if (c.LineIsYours)
			{
				GameArt.Label(new Rect(r2.x + 8f * p, r2.y + 5f * p, r2.width - 16f * p, r2.height - 9f * p), Lang.Template("You: {0}").Replace("{0}", Lang.Typed(text3, letters)), 5.4f, TextAnchor.UpperLeft, GameArt.Peach, wrap: true);
			}
			else
			{
				GameArt.Label(new Rect(r2.x + 8f * p, r2.y + 5f * p, r2.width - 16f * p, r2.height - 9f * p), Lang.Typed(text3, letters), 5.4f, TextAnchor.UpperLeft, c.Romantic ? RomanceText : Color.white, wrap: true);
			}
			if (c.MorePages && c.Typed)
			{
				float a = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
				PixelArrowDown(new Vector2(r2.xMax - 9f * p, r2.yMax - 7f * p), new Color(1f, 1f, 1f, a));
			}
			float num16 = r2.yMax + 5f * p;
			float num17 = (flag3 ? (num3 - 9f * p) : num3);
			float num18 = (flag2 ? Mathf.Round((num17 - num5) * 0.5f) : num17);
			Vector2 mousePosition = Event.current.mousePosition;
			for (int i = 0; i < count; i++)
			{
				Choice choice = c.Choices[i];
				int num19 = (flag2 ? (i / num6) : 0);
				int num20 = (flag2 ? (i % num6) : i) - _talkFirst;
				if (num20 < 0 || num20 >= num11)
				{
					continue;
				}
				Rect r3 = new Rect(num14 + (float)num19 * (num18 + num5), num16 + (float)num20 * (num4 + num5), num18, num4);
				bool flag4 = mouse && r3.Contains(mousePosition);
				if (flag4 && Event.current.type == EventType.Repaint && (mousePosition - _talkMouse).sqrMagnitude > 1f)
				{
					c.Selected = i;
				}
				bool flag5 = c.Selected == i && choice.Enabled;
				if (Event.current.type == EventType.Repaint)
				{
					Color color2 = GUI.color;
					if (!choice.Enabled)
					{
						GUI.color = new Color(color2.r * 0.7f, color2.g * 0.7f, color2.b * 0.7f, color2.a * 0.85f);
					}
					if (flag5)
					{
						GameArt.Sliced(r3, GameArt.RedButton, Color.white, 5f, 5f, 5f, 6f, Mathf.Min(p, r3.height / 14f));
					}
					else
					{
						GameArt.Sliced(r3, GameArt.GreyButton, (flag4 && choice.Enabled) ? Color.white : new Color(0.88f, 0.88f, 0.88f, 1f), 3f, 3f, 3f, 3f, Mathf.Min(p, r3.height / 14f));
					}
					GUI.color = color2;
					Color color3 = ((!choice.Enabled) ? GameArt.Grey : (flag5 ? Color.white : ((choice.Tone == ChoiceTone.Romance) ? RomanceText : ((choice.Tone == ChoiceTone.Quest) ? QuestText : ((choice.Tone == ChoiceTone.Risk) ? RiskText : ((choice.Tone == ChoiceTone.Back || choice.Tone == ChoiceTone.Leave) ? BackText : Color.white))))));
					float num21 = ((!string.IsNullOrEmpty(choice.Note) && !choice.Enabled) ? Mathf.Min(num18 * 0.42f, GameArt.Measure(choice.Note, 4.2f).x + 6f * p) : 0f);
					if (i < 9)
					{
						GameArt.Label(new Rect(r3.x + 5f * p, r3.y - 0.5f * p, 10f * p, r3.height), (i + 1).ToString(), 5f, TextAnchor.MiddleLeft, flag5 ? Color.white : GameArt.KeyOrange);
					}
					GameArt.Label(new Rect(r3.x + 15f * p, r3.y - 0.5f * p, r3.width - 20f * p - num21, r3.height), choice.Label, 5.2f, TextAnchor.MiddleLeft, color3);
					if (num21 > 0f)
					{
						GameArt.Label(new Rect(r3.xMax - num21 - 5f * p, r3.y - 0.5f * p, num21, r3.height), choice.Note, 4.2f, TextAnchor.MiddleRight, RiskText);
					}
				}
				if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && flag4)
				{
					c.Selected = i;
					Sounds.PlayUI("event:/SFX/Ui/Click");
					if (c.EndAfter && c.Typed && choice.Tone == ChoiceTone.Leave)
					{
						WorldBoxMod.Instance?.EndConversation();
					}
					else
					{
						c.Pick(i);
					}
					Event.current.Use();
					break;
				}
			}
			if (Event.current.type == EventType.Repaint)
			{
				_talkMouse = mousePosition;
			}
			if (flag3)
			{
				float num22 = num4 + num5;
				float num23 = GameScrollbar(new Rect(num14 + num3 - 7f * p, num16 + 2f * p, 6f * p, (float)num11 * num22 - num5 - 4f * p), (float)_talkFirst * num22, (float)(num6 - num11) * num22, (float)num11 / (float)num6);
				_talkFirst = Mathf.Clamp(Mathf.RoundToInt(num23 / num22), 0, num6 - num11);
			}
			float num24 = num16 + (float)num11 * (num4 + num5) + 1f * p;
			if (num24 < num2 - 8f * p)
			{
				GameArt.Label(new Rect(num14, num24, num3, 9f * p), hint ?? "W/S, wheel or mouse to choose   ·   E, Enter or click to say   ·   1-9   ·   Hold Tab for meters   ·   Esc to leave", 4.2f, TextAnchor.MiddleCenter, GameArt.Grey);
			}
		}

		private string CardRelation(string rel, string suffix)
		{
			if (suffix == null)
			{
				return rel;
			}
			if ((object)rel != _cardRel || (object)suffix != _cardSuffix)
			{
				_cardRel = rel;
				_cardSuffix = suffix;
				_cardLine = ((rel != null) ? (rel + suffix) : suffix.TrimStart(' ', '·'));
			}
			return _cardLine;
		}

		private Rect TalkStar(Conversation c, Rect head)
		{
			float p = GameArt.P;
			float num = Mathf.Round(head.height - 12f * p);
			Rect rect = new Rect(Mathf.Round(head.xMax - num - 7f * p), Mathf.Round(head.y + (head.height - num) * 0.5f - 1f * p), num, num);
			Actor with = c.With;
			bool flag = SafeFavorite(with);
			bool flag2 = rect.Contains(Event.current.mousePosition);
			if (Event.current.type == EventType.Repaint)
			{
				Color tint = (flag ? Color.white : (flag2 ? new Color(1f, 1f, 1f, 0.8f) : new Color(0.55f, 0.55f, 0.55f, 0.6f)));
				GameArt.Icon(rect, GameArt.Star, tint);
				GameArt.Label(new Rect(rect.xMax - 2f * p, rect.yMax - 6f * p, 6f * p, 7f * p), "B", 4f, TextAnchor.MiddleLeft, GameArt.KeyOrange);
			}
			if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && flag2 && with != null)
			{
				_mod.StarCreature(with);
				Event.current.Use();
			}
			return rect;
		}

		private static void PixelArrowDown(Vector2 c, Color color)
		{
			float num = Mathf.Max(1f, Mathf.Round(GameArt.P));
			for (int i = 0; i < 3; i++)
			{
				GameArt.Flat(new Rect(Mathf.Round(c.x - num * (2.5f - (float)i)), Mathf.Round(c.y + ((float)i - 1.5f) * num), num * (float)(5 - i * 2), num), color);
			}
		}

		public void DrawBoard(Law.Board b, Actor you)
		{
			if (you == null)
			{
				return;
			}
			List<Poster> list = Law.Posters(b, you);
			List<Quest> list2 = Quests.Today(b.City, you);
			List<Quest> list3 = Quests.TakenToday(b.City);
			int num = list2.Count + list3.Count;
			int num2 = list.Count + ((num > 0) ? (num + 1) : 0);
			float p = GameArt.P;
			float num3 = 70f + (float)Mathf.Max(3, num2) * 31f + 30f;
			bool close;
			Rect rect = GameWindow(WindowRect(380f, Mathf.Min(330f, num3 + 40f)), "The board of " + b.City.name, "E", out close);
			if (close)
			{
				Law.BoardOpen = false;
				return;
			}
			Quest quest = Law.TakenAt(b.City);
			bool flag = quest != null && quest.Stage < 2;
			float num4 = Mathf.Round(18f * p);
			Rect view = new Rect(rect.x, rect.y, rect.width - 9f * p, rect.height - num4 - 4f * p);
			float num5 = Mathf.Round(28f * p);
			float num6 = Mathf.Round(3f * p);
			float contentH = Mathf.Max(view.height, (float)num2 * (num5 + num6));
			float num7 = BeginList(view, ref _boardScroll, contentH);
			if (list.Count == 0 && num == 0)
			{
				GameArt.Label(new Rect(6f * p, num7 + 6f * p, view.width - 12f * p, 10f * p), "The board is bare. Peaceful times... for now.", 5f, TextAnchor.MiddleLeft, GameArt.Grey);
			}
			Sprite s = GameArt.IconNamed("iconMoney") ?? GameArt.IconNamed("iconGold");
			for (int i = 0; i < list.Count; i++)
			{
				Poster poster = list[i];
				Rect r = new Rect(0f, num7 + (float)i * (num5 + num6), view.width, num5);
				float num8 = ((poster.Target != null && poster.Target.isAlive()) ? (poster.Target.current_position - you.current_position).magnitude : (-1f));
				if (Event.current.type == EventType.Repaint)
				{
					GameArt.Sliced(r, GameArt.Inner, poster.You ? new Color(1f, 0.8f, 0.78f, 1f) : new Color(0.92f, 0.92f, 0.92f, 1f));
					Sprite s2 = null;
					try
					{
						if (poster.Target != null && poster.Target.isAlive())
						{
							s2 = poster.Target.getSpriteToRender();
						}
					}
					catch
					{
					}
					PortraitWell(new Rect(r.x + 3f * p, r.y + 3f * p, num5 - 6f * p, num5 - 6f * p), s2);
					float x = r.x + num5 + 2f * p;
					GameArt.Label(new Rect(x, r.y + 2.5f * p, r.width * 0.5f, 10f * p), (poster.You ? "WANTED: " : "") + poster.Name, 5.6f, TextAnchor.MiddleLeft, poster.You ? RiskText : Color.white);
					GameArt.Label(new Rect(x, r.y + 13.5f * p, r.width * 0.55f, 9f * p), poster.Why + ((num8 >= 0f) ? $"   ·   {num8:F0} tiles away" : "   ·   dead"), 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
					float num9 = r.xMax - 66f * p;
					GameArt.Icon(new Rect(num9 - 44f * p, r.y + 8f * p, 11f * p, 11f * p), s);
					GameArt.Label(new Rect(num9 - 32f * p, r.y + 7f * p, 30f * p, 12f * p), poster.Reward.ToString(), 5.4f, TextAnchor.MiddleLeft, GameArt.Orange);
				}
				Rect r2 = new Rect(r.xMax - 60f * p, r.y + (num5 - 15f * p) * 0.5f, 54f * p, 15f * p);
				bool flag2 = flag && quest.Target == poster.Id && quest.Holder == you.data.id;
				if (poster.You)
				{
					continue;
				}
				if (flag2 && quest.Stage == 1)
				{
					if (GameButton(r2, "Claim", red: true, enabled: true, null, 5f, _listMouseIn))
					{
						Law.Claim(b, you, _mod.Toast);
					}
				}
				else if (flag2)
				{
					if (Event.current.type == EventType.Repaint)
					{
						StateTag(r2.x + 8f * p, r2.y + 3f * p, "Taken", new Color(0.55f, 1f, 0.5f));
					}
				}
				else if (GameButton(r2, "Take", red: true, !flag && num8 >= 0f, null, 5f, _listMouseIn))
				{
					Law.Take(b, poster, you, _mod.Toast);
				}
			}
			if (num > 0)
			{
				DrawNoticeRows(num7 + (float)list.Count * (num5 + num6), view.width, num5, num6, list2, list3, b, you);
			}
			EndList(view, ref _boardScroll, contentH);
			string text = ((!flag) ? "One bounty at a time per town. Kill the target, then claim it here or from a leader." : ((quest.Holder == you.data.id) ? ("You hold a bounty from this board: " + Quests.Status(quest)) : "Someone already holds a bounty from this board."));
			float num10 = Mathf.Round(54f * p);
			GameArt.Label(new Rect(rect.x + 2f * p, rect.yMax - num4, rect.width - num10 - 10f * p, num4), text, 4.2f, TextAnchor.MiddleLeft, GameArt.Grey, wrap: true);
			if (GameButton(new Rect(rect.xMax - num10, rect.yMax - num4 + 1f * p, num10, num4 - 2f * p), "Done (E)", red: false))
			{
				Law.BoardOpen = false;
			}
		}

		public void DrawNaming(Actor you)
		{
			Actor naming = Births.Naming;
			if (naming == null)
			{
				return;
			}
			float p = GameArt.P;
			bool flag = Models.IsFemale(naming);
			bool close;
			Rect rect = GameWindow(WindowRect(250f, 140f), flag ? "A daughter is born!" : "A son is born!", null, out close);
			if (close)
			{
				Births.FinishNaming(you, keep: true);
				return;
			}
			float num = Mathf.Round(44f * p);
			Sprite s = null;
			try
			{
				s = naming.getSpriteToRender();
			}
			catch
			{
			}
			PortraitWell(new Rect(rect.x + 4f * p, rect.y + 4f * p, num, num), s);
			float num2 = rect.x + num + 12f * p;
			float width = rect.xMax - num2 - 4f * p;
			GameArt.Label(new Rect(num2, rect.y + 4f * p, width, 12f * p), "What will you call " + (flag ? "her" : "him") + "?", 6f, TextAnchor.MiddleLeft, Color.white);
			Births.NameText = GameTextField(new Rect(num2, rect.y + 20f * p, width, 18f * p), Births.NameText, 24, "fp_baby_name");
			if (Births.NameFocus)
			{
				GUI.FocusControl("fp_baby_name");
				Births.NameFocus = false;
			}
			float y = rect.yMax - 20f * p;
			float num3 = Mathf.Round((rect.width - 4f * p) * 0.5f);
			if (GameButton(new Rect(rect.x, y, num3, 18f * p), "Name " + (flag ? "her" : "him") + " (Enter)", red: true))
			{
				Births.FinishNaming(you, keep: false);
			}
			if (GameButton(new Rect(rect.x + num3 + 4f * p, y, num3, 18f * p), "Keep \"" + Dialogue.NameOf(naming) + "\"", red: false))
			{
				Births.FinishNaming(you, keep: true);
			}
			if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
			{
				Births.FinishNaming(you, keep: false);
				Event.current.Use();
			}
		}

		public void DrawHeirs(WorldBoxMod mod)
		{
			EnsureAssets();
			GameArt.Ensure();
			float num = Screen.width;
			float num2 = Screen.height;
			float p = GameArt.P;
			List<TreeNode> heirs = mod.Heirs;
			float num3 = Mathf.Round(Mathf.Min(86f * p, num * 0.2f));
			float num4 = Mathf.Round(30f * p);
			float num5 = Mathf.Round(5f * p);
			float num6 = (float)heirs.Count * num3 + (float)(heirs.Count - 1) * num5;
			float num7 = Mathf.Round((num - num6) * 0.5f);
			float num8 = Mathf.Round(num2 * 0.6f);
			float num9 = Mathf.Clamp01((mod.DeathTimer - 1.2f) / 0.6f);
			if (num9 <= 0f)
			{
				return;
			}
			GameArt.Label(new Rect(0f, num8 - 15f * p, num, 12f * p), "Carry on as your heir?", 7f, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, num9));
			for (int i = 0; i < heirs.Count; i++)
			{
				TreeNode treeNode = heirs[i];
				Rect r = new Rect(num7 + (float)i * (num3 + num5), num8, num3, num4);
				bool flag = r.Contains(Event.current.mousePosition);
				if (Event.current.type == EventType.Repaint)
				{
					GameArt.Sliced(r, GameArt.Inner, new Color(1f, 1f, 1f, num9));
					if (flag)
					{
						GameArt.Sliced(r, GameArt.ButtonBorder, new Color(1f, 0.82f, 0.3f, num9), 4f, 4f, 4f, 4f);
					}
					Sprite s = null;
					try
					{
						if (treeNode.Actor != null && treeNode.Actor.isAlive())
						{
							s = treeNode.Actor.getSpriteToRender();
						}
					}
					catch
					{
					}
					PortraitWell(new Rect(r.x + 3f * p, r.y + 3f * p, num4 - 6f * p, num4 - 6f * p), s, num9);
					float num10 = r.x + num4 + 1f * p;
					GameArt.Label(new Rect(num10, r.y + 3f * p, r.xMax - num10 - 3f * p, 10f * p), i + 1 + ". " + treeNode.Name, 5.2f, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, num9));
					int num11 = -1;
					try
					{
						if (treeNode.Actor != null && treeNode.Actor.isAlive())
						{
							num11 = treeNode.Actor.getAge();
						}
					}
					catch
					{
					}
					if (num11 < 0 && treeNode.Born != int.MinValue)
					{
						num11 = Mathf.Max(0, Chronicle.Year - treeNode.Born);
					}
					GameArt.Label(new Rect(num10, r.y + 15f * p, r.xMax - num10 - 3f * p, 9f * p), "your " + treeNode.Role + ((num11 >= 0) ? (", " + num11) : ""), 4.2f, TextAnchor.MiddleLeft, new Color(GameArt.Grey.r, GameArt.Grey.g, GameArt.Grey.b, num9));
				}
				if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && flag)
				{
					Sounds.PlayUI("event:/SFX/Ui/Click");
					mod.ContinueAs(i);
					Event.current.Use();
					break;
				}
			}
			float num12 = Mathf.Max(0f, 15.2f - mod.DeathTimer);
			GameArt.Label(new Rect(0f, num8 + num4 + 4f * p, num, 9f * p), $"Click, or press 1-{heirs.Count}   ·   or wait ({num12:F0}) to let go", 4.4f, TextAnchor.MiddleCenter, new Color(GameArt.Grey.r, GameArt.Grey.g, GameArt.Grey.b, num9));
		}

		public void DrawFade(float k)
		{
			EnsureAssets();
			float value = ((k > 0.8f) ? ((1f - k) / 0.2f) : ((k < 0.3f) ? (k / 0.3f) : 1f));
			Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, Mathf.Clamp01(value)));
		}

		public void DrawCouncil()
		{
			DrawCouncilBody();
			DrawStyleTip();
		}

		private void DrawCouncilBody()
		{
			EnsureAssets();
			Royal royal = _mod.Royal;
			Actor actor = _mod.Body ?? _mod.Host;
			if (actor == null || !Royal.IsKing(actor))
			{
				royal.Open = false;
				return;
			}
			Kingdom kingdom = actor.kingdom;
			Action<string> toast = _mod.Toast;
			Rect inner = MapWindow("Royal Council", out var close);
			if (close)
			{
				royal.Open = false;
				return;
			}
			float k = GameArt.P;
			inner = DrawCourtSeats(inner, kingdom, actor);
			bool enabled = CourtPart(actor, kingdom, Seat.Chancellor, courtBusiness: true);
			bool flag = CourtPart(actor, kingdom, Seat.Steward, courtBusiness: true);
			bool enabled2 = CourtPart(actor, kingdom, Seat.Marshal, courtBusiness: false);
			float x = inner.x;
			float num = Mathf.Round(176f * k);
			float y = inner.y;
			Rect r = new Rect(x, y, num, Mathf.Round(46f * k));
			GameArt.StonePanel(r);
			DrawBanner(new Rect(r.x + 5f * k, r.y + 8f * k, 22f * k, 28f * k), kingdom, civ: true, 1f);
			Color color = Color.white;
			try
			{
				color = kingdom.getColor().getColorText();
			}
			catch
			{
			}
			float num2 = r.x + 32f * k;
			GameArt.Label(new Rect(num2, r.y + 3f * k, r.xMax - num2 - 4f * k, 11f * k), Royal.NameOf(kingdom), 6.5f, TextAnchor.MiddleLeft, color);
			GameArt.Label(new Rect(num2, r.y + 14f * k, r.xMax - num2 - 4f * k, 9f * k), "King " + WorldBoxMod.Describe(actor), 4.4f, TextAnchor.MiddleLeft, GameArt.Grey);
			float num3 = Chip(Chip(Chip(num2, r.y + 28f * k, GameArt.IconNamed("iconCityList") ?? GameArt.IconNamed("iconCity"), Royal.CityCount(kingdom).ToString(), 1f) + 3f * k, r.y + 28f * k, GameArt.IconNamed("iconPopulation"), Royal.Population(kingdom).ToString(), 1f) + 3f * k, r.y + 28f * k, GameArt.IconNamed("iconArmy") ?? GameArt.Warfare, Royal.Warriors(kingdom).ToString(), 1f);
			long num4 = Royal.KillsToSpend(kingdom);
			Chip(num3 + 3f * k, r.y + 28f * k, GameArt.Kills, num4.ToString(), 1f);
			y = DrawStyleRow(x, r.yMax + 4f * k, num, kingdom, actor);
			SectionTitle(new Rect(x, y, num, Mathf.Round(20f * k)), "Your army", GameArt.IconNamed("iconArmy"));
			y += 22f * k;
			float num5 = Mathf.Round((num - 4f * k) * 0.5f);
			float num6 = Mathf.Round(17f * k);
			if (GameButton(new Rect(x, y, num5, num6), "Draft", red: true, flag, GameArt.IconNamed("iconArmy")))
			{
				royal.Levy(actor, toast);
			}
			if (GameButton(new Rect(x + num5 + 4f * k, y, num5, num6), "Rally to me", red: true, enabled: true, GameArt.Crown))
			{
				royal.Rally(actor, toast);
			}
			y += num6 + 3f * k;
			bool enabled3 = royal.Active != null;
			if (GameButton(new Rect(x, y, num5, num6), "Defend", red: false, enabled: true, GameArt.IconNamed("iconCityList")))
			{
				royal.Defend(actor, toast);
			}
			if (GameButton(new Rect(x + num5 + 4f * k, y, num5, num6), "Recall", red: false, enabled3, GameArt.IconNamed("iconArrowBack")))
			{
				royal.Recall(toast);
			}
			y += num6 + 3f * k;
			List<GreatWar.Ruin> list = GreatWar.Ruins(actor);
			if (list.Count > 0)
			{
				string label = ((list.Count == 1) ? ("Claim the ruins of " + list[0].Name) : $"Claim ruins ({list.Count})");
				if (GameButton(new Rect(x, y, num, num6), label, red: false, enabled: true, GameArt.IconNamed("iconCityList")))
				{
					GreatWar.ClaimRuin(actor, list[0], toast);
				}
				y += num6 + 3f * k;
			}
			y += 3f * k;
			y = DrawTaxRows(x, y, num, kingdom, actor, flag);
			SectionTitle(new Rect(x, y, num, Mathf.Round(20f * k)), "Under way", GameArt.Warfare);
			y += 22f * k;
			Rect way = new Rect(x, y, num, inner.yMax - y);
			GameArt.Sliced(way, GameArt.Inner, new Color(1f, 1f, 1f, 0.6f));
			float ly;
			float lineW;
			if (Event.current.type == EventType.Repaint)
			{
				ly = way.y + 5f * k;
				lineW = way.width - 12f * k;
				bool flag2 = false;
				if (royal.Active != null)
				{
					Line("Campaign against " + royal.Active.TargetName + ": " + (royal.Active.Status ?? "marching"), new Color(1f, 0.5f, 0.4f), 2f);
					flag2 = true;
				}
				for (int i = 0; i < royal.Raids.Count && i < 4; i++)
				{
					Royal.RaidParty raidParty = royal.Raids[i];
					Line(raidParty.Status ?? (raidParty.What + " on their way to " + raidParty.TargetName), Color.white, 2f);
					flag2 = true;
				}
				for (int j = 0; j < royal.Storms.Count && j < 3; j++)
				{
					Line(royal.Storms[j].Status ?? (Royal.Label(royal.Storms[j].Kind) + " over " + royal.Storms[j].TargetName), GameArt.Peach, 2f);
					flag2 = true;
				}
				if (!flag2)
				{
					Line("Nothing yet. Declare war and send your army, or pick a rival, choose one of its towns and spend your kingdom's kills on a strike. Every kill your people make goes into the war chest.", GameArt.Grey, 5f);
				}
			}
			float num7 = x + num + 8f * k;
			float width = inner.xMax - num7 - 9f * k;
			List<Kingdom> list2 = Royal.Rivals(kingdom);
			if (royal.Selected != null && !list2.Contains(royal.Selected))
			{
				royal.Selected = null;
			}
			if (royal.Selected == null && list2.Count > 0)
			{
				royal.Selected = list2[0];
			}
			SectionTitle(new Rect(num7, inner.y, width, Mathf.Round(20f * k)), "Kingdoms", GameArt.IconNamed("iconKingdomList") ?? GameArt.IconNamed("iconKingdom"));
			float num8 = Mathf.Round(136f * k);
			Rect view = new Rect(num7, inner.y + 22f * k, width, inner.height - 22f * k - num8 - 4f * k);
			float num9 = Mathf.Round(30f * k);
			float num10 = Mathf.Round(3f * k);
			float contentH = Mathf.Max(view.height, (float)list2.Count * (num9 + num10));
			float num11 = BeginList(view, ref royal.Scroll, contentH);
			Peace.Mood tip = null;
			Rect tipAt = default(Rect);
			if (list2.Count == 0)
			{
				GameArt.Label(new Rect(6f * k, num11 + 6f * k, view.width - 12f * k, 10f * k), "There are no other kingdoms in the world.", 4.8f, TextAnchor.MiddleLeft, GameArt.Grey);
			}
			for (int l = 0; l < list2.Count; l++)
			{
				Kingdom kingdom2 = list2[l];
				Rect card = new Rect(0f, num11 + (float)l * (num9 + num10), view.width, num9);
				bool flag3 = Royal.AtWar(kingdom, kingdom2);
				bool flag4 = !flag3 && Royal.Allied(kingdom, kingdom2);
				bool flag5 = royal.Selected == kingdom2;
				if (Event.current.type == EventType.Repaint)
				{
					KingdomCard(card, kingdom2, Royal.CityCount(kingdom2) + " cities   " + Royal.Population(kingdom2) + " people   " + Royal.Warriors(kingdom2) + " warriors", flag5, GameArt.Grey);
					float x2 = GameArt.Measure(Royal.NameOf(kingdom2), 5.8f).x;
					StateTag(card.x + 26f * k + x2 + 4f * k, card.y + 3.5f * k, flag3 ? "At war" : (flag4 ? "Allied" : "At peace"), flag3 ? new Color(1f, 0.45f, 0.35f) : (flag4 ? new Color(0.55f, 1f, 0.5f) : GameArt.Grey));
					if (flag3)
					{
						PeaceMood(card, card.x + 26f * k + x2 + 15f * k + GameArt.Measure("At war", 4.4f).x, kingdom, kingdom2, ref tip, ref tipAt);
					}
				}
				PadNav.Spot(new Rect(card.x, card.y, card.width * 0.5f, card.height));
				if (GUI.Button(new Rect(card.x, card.y, card.width * 0.5f, card.height), GUIContent.none, GUIStyle.none) && _listMouseIn && !flag5)
				{
					royal.Selected = kingdom2;
					royal.TownIndex = 0;
					Sounds.PlayUI("event:/SFX/Ui/Click");
				}
				float num12 = Mathf.Round(14f * k);
				float y2 = card.y + Mathf.Round((num9 - num12) * 0.5f);
				float num13 = card.xMax - 5f * k;
				if (flag3)
				{
					float num14 = Mathf.Round(50f * k);
					num13 -= num14;
					if (GameButton(new Rect(num13, y2, num14, num12), "Make peace", red: false, enabled, null, 4.4f, _listMouseIn))
					{
						Peace.Ask(actor, kingdom2, toast);
					}
					float num15 = Mathf.Round(19f * k);
					for (int num16 = Shares.Length - 1; num16 >= 0; num16--)
					{
						num13 -= num15 + 2f * k;
						if (GameButton(new Rect(num13, y2, num15, num12), ShareLabels[num16], red: true, enabled2, null, 4.4f, _listMouseIn))
						{
							royal.SendArmy(actor, kingdom2, Shares[num16], toast, _mod.PlayHorn);
						}
					}
					if (Event.current.type == EventType.Repaint)
					{
						GameArt.Label(new Rect(num13 - 44f * k, y2, 41f * k, num12), "Send army", 4.2f, TextAnchor.MiddleRight, new Color(1f, 0.55f, 0.45f));
					}
					continue;
				}
				float num17 = Mathf.Round(36f * k);
				num13 -= num17;
				if (GameButton(new Rect(num13, y2, num17, num12), "Tribute", red: false, flag, null, 4.4f, _listMouseIn))
				{
					royal.DemandTribute(actor, kingdom2, toast);
				}
				num13 -= num17 + 2f * k;
				if (GameButton(new Rect(num13, y2, num17, num12), "Gift", red: false, flag, null, 4.4f, _listMouseIn))
				{
					royal.SendGift(actor, kingdom2, toast);
				}
				List<City> list3 = (flag4 ? GreatWar.AllyHolds(actor, kingdom2) : null);
				if (list3 != null && list3.Count > 0)
				{
					float num18 = Mathf.Round(46f * k);
					num13 -= num18 + 2f * k;
					if (GameButton(new Rect(num13, y2, num18, num12), "Ask for town", red: false, enabled, null, 4.4f, _listMouseIn))
					{
						royal.AskForTown(actor, kingdom2, list3[0], toast);
					}
				}
				if (!flag4)
				{
					num13 -= num17 + 2f * k;
					if (GameButton(new Rect(num13, y2, num17, num12), "Ally", red: false, enabled, null, 4.4f, _listMouseIn))
					{
						royal.ProposeAlliance(actor, kingdom2, toast);
					}
					float num19 = Mathf.Round(52f * k);
					num13 -= num19 + 2f * k;
					if (GameButton(new Rect(num13, y2, num19, num12), "Declare war", red: true, enabled, null, 4.4f, _listMouseIn))
					{
						royal.DeclareWar(actor, kingdom2, toast, _mod.PlayHorn);
					}
				}
			}
			EndList(view, ref royal.Scroll, contentH);
			if (tip != null)
			{
				DrawPeaceTip(tip, new Rect(tipAt.x + view.x, tipAt.y + view.y, tipAt.width, tipAt.height));
			}
			Rect r2 = new Rect(num7, inner.yMax - num8, width, num8);
			GameArt.Sliced(r2, GameArt.Inner, new Color(1f, 0.86f, 0.84f, 1f));
			Kingdom selected = royal.Selected;
			List<City> list4 = ((selected != null) ? Royal.CitiesOf(selected, actor.current_position) : null);
			Rect r3 = new Rect(r2.x + 2f * k, r2.y + 3f * k, r2.width - 4f * k, Mathf.Round(20f * k));
			if (list4 == null || list4.Count == 0)
			{
				SectionTitle(r3, "Dark deeds", GameArt.Kills);
				GameArt.Label(new Rect(r2.x, r3.yMax + 6f * k, r2.width, 10f * k), (selected == null) ? "Pick a kingdom above." : "They have no towns to strike.", 4.8f, TextAnchor.MiddleCenter, GameArt.Grey);
				return;
			}
			royal.TownIndex = (royal.TownIndex % list4.Count + list4.Count) % list4.Count;
			City city = list4[royal.TownIndex];
			SectionTitle(r3, "Dark deeds against " + city.name);
			float num20 = Mathf.Round(15f * k);
			if (GameButton(new Rect(r3.x + 4f * k, r3.y + 2f * k, num20, num20), "<", red: false))
			{
				royal.TownIndex--;
			}
			if (GameButton(new Rect(r3.xMax - 4f * k - num20, r3.y + 2f * k, num20, num20), ">", red: false))
			{
				royal.TownIndex++;
			}
			GameArt.Label(new Rect(r2.x, r3.yMax - 1f * k, r2.width, 8f * k), Royal.NameOf(selected) + "  ·  " + SafeCount(city) + " people" + ((city == selected.capital) ? "  ·  capital" : "") + "  ·  town " + (royal.TownIndex + 1) + " of " + list4.Count, 4.2f, TextAnchor.MiddleCenter, GameArt.Grey);
			int num21 = 8;
			float num22 = (r2.width - 8f * k) / (float)num21;
			float num23 = Mathf.Round(Mathf.Min(22f * k, num22 - 8f * k));
			for (int m = 0; m < Royal.DeedRows.Length; m++)
			{
				for (int n = 0; n < Royal.DeedRows[m].Length; n++)
				{
					Royal.Deed deed = Royal.DeedRows[m][n];
					float f = r2.x + 4f * k + (float)n * num22 + (num22 - num23) * 0.5f;
					float f2 = r3.yMax + 11f * k + (float)m * (num23 + 12f * k);
					Rect r4 = new Rect(Mathf.Round(f), Mathf.Round(f2), num23, num23);
					if (CostButton(r4, DeedIcon(deed), Royal.Label(deed), royal.CooldownLeft(Royal.KeyOf(deed)), Royal.CooldownOf(deed), Royal.PriceOf(kingdom, deed), num4, live: true, CourtPart(actor, kingdom, Court.SeatFor(deed), courtBusiness: true)))
					{
						royal.Unleash(actor, city, deed, toast, _mod.PlayHorn);
					}
					if (deed == Royal.Deed.MeteorStorm && Event.current.type == EventType.Repaint)
					{
						GameArt.Icon(new Rect(r4.x - 2f * k, r4.yMax - 10f * k, 10f * k, 10f * k), DeedIcon(deed));
					}
				}
			}
			int num24 = Royal.DeedRows.Length - 1;
			int num25 = Royal.DeedRows[num24].Length;
			if (num25 < num21 && Event.current.type == EventType.Repaint)
			{
				float num26 = r3.yMax + 11f * k + (float)num24 * (num23 + 12f * k);
				Rect r5 = new Rect(Mathf.Round(r2.x + 4f * k + (float)num25 * num22 + 4f * k), Mathf.Round(num26 - 1f * k), Mathf.Round((float)(num21 - num25) * num22 - 8f * k), Mathf.Round(num23 + 9f * k));
				GameArt.TipPanel(r5, 0.9f);
				float num27 = Mathf.Round(12f * k);
				GameArt.Icon(new Rect(r5.x + 4f * k, r5.y + 3f * k, num27, num27), GameArt.Kills);
				GameArt.Label(new Rect(r5.x + num27 + 7f * k, r5.y + 1.5f * k, r5.width - num27 - 9f * k, 10f * k), num4 + ((num4 == 1) ? " kill to spend" : " kills to spend"), 5.2f, TextAnchor.MiddleLeft, GameArt.Peach);
				GameArt.Label(new Rect(r5.x + num27 + 7f * k, r5.y + 11f * k, r5.width - num27 - 9f * k, 8f * k), Royal.KillsEarned(kingdom) + " earned  ·  " + Royal.KillsSpent(kingdom) + " spent", 3.9f, TextAnchor.MiddleLeft, GameArt.Grey);
				GameArt.Label(new Rect(r5.x + 4f * k, r5.y + 20f * k, r5.width - 8f * k, 8f * k), "Every kill your people make adds one.", 3.7f, TextAnchor.MiddleLeft, GameArt.Grey);
			}
			void Line(string text, Color c, float lines)
			{
				if (!(ly > way.yMax - 8f * k))
				{
					GameArt.Label(new Rect(way.x + 6f * k, ly, lineW, 8f * k * lines), text, 4.4f, TextAnchor.UpperLeft, c, wrap: true);
					ly += 8f * k * lines + 2f * k;
				}
			}
		}

		private bool CostButton(Rect r, Sprite icon, string label, float wait, float cooldown, int cost, long chest, bool live = true, bool enabled = true)
		{
			bool result = PowerButton(r, icon, label, wait, cooldown, enabled, live);
			if (Event.current.type != EventType.Repaint)
			{
				return result;
			}
			float p = GameArt.P;
			bool flag = chest < cost;
			if (flag)
			{
				GameArt.Flat(new Rect(r.x + r.width * 0.12f, r.y + r.height * 0.12f, r.width * 0.76f, r.height * 0.76f), new Color(0.06f, 0.02f, 0.02f, 0.5f));
			}
			string text = cost.ToString();
			float num = Mathf.Round(GameArt.Measure(text, 3.9f).x + 5f * p);
			float height = Mathf.Round(7.5f * p);
			Rect r2 = new Rect(Mathf.Round(r.xMax - num + 4f * p), Mathf.Round(r.y - 3f * p), num, height);
			GameArt.TipPanel(r2);
			GameArt.Label(new Rect(r2.x, r2.y - 0.5f * p, r2.width, r2.height), text, 3.9f, TextAnchor.MiddleCenter, flag ? new Color(1f, 0.42f, 0.35f) : GameArt.Peach);
			return result;
		}

		private static int SafeCount(City c)
		{
			try
			{
				return c.countUnits();
			}
			catch
			{
				return 0;
			}
		}

		private void DrawCampaign(float sw, float sh, float top = 0f)
		{
			Royal royal = _mod.Royal;
			List<KeyValuePair<string, Color>> list = new List<KeyValuePair<string, Color>>();
			if (royal.Active != null && royal.Active.Status != null)
			{
				list.Add(new KeyValuePair<string, Color>(royal.Active.Status, Skin.InkRed));
			}
			for (int i = 0; i < royal.Raids.Count && i < 3; i++)
			{
				if (royal.Raids[i].Status != null)
				{
					list.Add(new KeyValuePair<string, Color>(royal.Raids[i].Status, Skin.Ink));
				}
			}
			for (int j = 0; j < royal.Storms.Count && j < 3; j++)
			{
				if (royal.Storms[j].Status != null)
				{
					list.Add(new KeyValuePair<string, Color>(royal.Storms[j].Status, Skin.InkRed));
				}
			}
			if (list.Count == 0)
			{
				return;
			}
			EnsureAssets();
			float p = GameArt.P;
			float num = Mathf.Round(Mathf.Min(sw * 0.3f, 170f * p));
			float num2 = Mathf.Round(9f * p);
			Rect r = new Rect(Mathf.Round(sw - num - 6f * p), Mathf.Round(Mathf.Max(6f * p, top + 4f * p)), num, Mathf.Round((float)list.Count * num2 + 24f * p));
			GameArt.StonePanel(r);
			string text = ((royal.Active != null) ? "The War" : ((royal.Raids.Count > 0) ? "Raids" : "Storms"));
			GameArt.Icon(new Rect(r.x + 5f * p, r.y + 3f * p, 12f * p, 12f * p), GameArt.Warfare);
			GameArt.Label(new Rect(r.x + 20f * p, r.y + 3f * p, num - 26f * p, 12f * p), text, 6.5f, TextAnchor.MiddleLeft, GameArt.Peach);
			float num3 = r.y + 17f * p;
			foreach (KeyValuePair<string, Color> item in list)
			{
				Color color = ((item.Value == Skin.InkRed) ? new Color(1f, 0.5f, 0.4f) : Color.white);
				GameArt.Label(new Rect(r.x + 8f * p, num3, num - 16f * p, num2), item.Key, 4.8f, TextAnchor.MiddleLeft, color);
				num3 += num2;
			}
		}

		private float DrawTaxRows(float x, float y, float w, Kingdom home, Actor host, bool steward)
		{
			float k = GameArt.P;
			SectionTitle(new Rect(x, y, w, Mathf.Round(20f * k)), "Taxes", GameArt.IconNamed("iconMoney") ?? GameArt.Loot);
			y += 22f * k;
			float h = Mathf.Round(15f * k);
			float ab = h;
			Row("Townsfolk", tribute: false);
			Row("Towns' tribute", tribute: true);
			GameArt.Label(new Rect(x, y, w, 9f * k), "Due next season: " + Taxes.Due(host), 4.4f, TextAnchor.MiddleLeft, GameArt.Grey);
			return y + 9f * k + 6f * k;
			void Row(string label, bool tribute)
			{
				int num = Taxes.StepOf(home, tribute);
				GameArt.Label(new Rect(x, y, w * 0.45f, h), label, 4.4f, TextAnchor.MiddleLeft, GameArt.Grey);
				float num2 = x + w * 0.45f;
				float num3 = w - w * 0.45f - 2f * ab;
				if (GameButton(new Rect(num2, y, ab, h), "<", red: false, steward && num > 0, null, 4.4f))
				{
					Taxes.SetStep(home, tribute, num - 1, _mod.Toast);
				}
				Color color = ((num >= 4) ? new Color(1f, 0.45f, 0.4f) : ((num == 3) ? GameArt.Peach : Color.white));
				GameArt.Label(new Rect(num2 + ab, y, num3, h), Taxes.StepNames[num] + " " + Mathf.RoundToInt(Taxes.RateOf(home, tribute) * 100f) + "%", 4.4f, TextAnchor.MiddleCenter, color);
				if (GameButton(new Rect(num2 + ab + num3, y, ab, h), ">", red: false, steward && num < 4, null, 4.4f))
				{
					Taxes.SetStep(home, tribute, num + 1, _mod.Toast);
				}
				y += h + 2f * k;
			}
		}

		private static string StatName(string id)
		{
			switch (id)
			{
			case "damage":
				return "Damage";
			case "armor":
				return "Armor";
			case "speed":
				return "Speed";
			case "attack_speed":
				return "Attack speed";
			case "critical_chance":
				return "Critical";
			case "range":
				return "Range";
			case "accuracy":
				return "Accuracy";
			case "stamina":
				return "Stamina";
			case "mana":
				return "Mana";
			case "targets":
				return "Targets";
			case "projectiles":
				return "Projectiles";
			case "area_of_effect":
				return "Area";
			default:
				return id;
			}
		}

		private static string StatText(string id, float v)
		{
			if (id == "critical_chance")
			{
				return Mathf.RoundToInt(v * 100f) + "%";
			}
			if (!(Mathf.Abs(v - Mathf.Round(v)) < 0.05f))
			{
				return v.ToString("0.0");
			}
			return Mathf.RoundToInt(v).ToString();
		}

		private static Sprite RecipeIcon(Crafting.Recipe r)
		{
			if (r != null)
			{
				if (r.Supply == null)
				{
					return IconOf(r.Asset);
				}
				return Supplies.Icon(r.Supply.Id);
			}
			return null;
		}

		private string DrawCraftQueue(Rect r, Actor host, Crafting craft)
		{
			float p = GameArt.P;
			Event current = Event.current;
			bool flag = current.type == EventType.Repaint;
			string result = null;
			float num = Mathf.Round(r.height);
			float num2 = Mathf.Round(3f * p);
			if (craft.Queue.Count == 0)
			{
				if (flag)
				{
					GameArt.Label(new Rect(r.x + 2f * p, r.y, r.width - 4f * p, r.height), "Pick a recipe. Double-click to craft", 4.3f, TextAnchor.MiddleLeft, CardFaint);
				}
				return null;
			}
			if (flag)
			{
				GameArt.Label(new Rect(r.x, r.y, 34f * p, r.height), "Forging", 4.4f, TextAnchor.MiddleLeft, GameArt.Peach);
			}
			float num3 = r.x + 34f * p;
			int num4 = -1;
			for (int i = 0; i < craft.Queue.Count; i++)
			{
				Crafting.Order order = craft.Queue[i];
				Rect r2 = new Rect(num3, r.y, num, num);
				num3 += num + num2;
				bool flag2 = r2.Contains(current.mousePosition);
				if (flag)
				{
					float num5 = ((i == 0) ? (BlowShake(craft) * p) : 0f);
					PortraitWell(new Rect(r2.x + num5, r2.y, r2.width, r2.height), RecipeIcon(order.Recipe), (i == 0) ? 1f : 0.75f);
					if (i == 0 && craft.Progress >= 0f)
					{
						float num6 = Mathf.Round((r2.height - 4f * p) * craft.Progress);
						GameArt.Flat(new Rect(r2.x + 2f * p, r2.yMax - 2f * p - num6, r2.width - 4f * p, num6), (craft.Waiting != null) ? new Color(0.7f, 0.7f, 0.75f, 0.35f) : new Color(1f, 0.7f, 0.2f, 0.42f));
					}
					if (order.Left > 1)
					{
						GameArt.Label(new Rect(r2.x + 1f * p, r2.yMax - 8f * p, r2.width - 2f * p, 7.5f * p), "x" + order.Left, 4f, TextAnchor.LowerRight, GameArt.Orange);
					}
					if (flag2)
					{
						Hover(r2);
					}
				}
				if (flag2)
				{
					result = string.Concat(order.Recipe.Name + ((order.Left > 1) ? (" x" + order.Left) : ""), "\n", (i == 0) ? ((craft.Waiting ?? (Mathf.RoundToInt(Mathf.Max(0f, craft.Progress) * 100f) + "% made")) + ". ") : "", "Right-click to cancel");
				}
				if (current.type == EventType.MouseDown && flag2)
				{
					if (current.button == 1)
					{
						num4 = i;
					}
					else if (current.button == 0)
					{
						craft.Selected = order.Recipe;
						Sounds.PlayUI("event:/SFX/Ui/Click");
					}
					current.Use();
				}
			}
			if (flag && craft.Queue.Count > 0 && craft.Blows > 0)
			{
				DrawSparks(new Vector2(r.x + 34f * p + num * 0.5f, r.y + num * 0.4f), craft.BlowAt, craft.Blows, p, SparkTint(craft.Working));
			}
			if (flag && craft.Waiting != null)
			{
				GameArt.Label(new Rect(num3 + 2f * p, r.y, r.xMax - num3 - 2f * p, r.height), craft.Waiting, 4.2f, TextAnchor.MiddleRight, GameArt.Orange);
			}
			if (num4 >= 0 && craft.Cancel(host, num4))
			{
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
			return result;
		}

		private string DrawRecipeCard(Rect r, Actor host, Crafting craft, Crafting.Recipe sel)
		{
			float p = GameArt.P;
			Event current = Event.current;
			bool flag = current.type == EventType.Repaint;
			string text = null;
			if (flag)
			{
				GameArt.TipPanel(r);
			}
			if (sel == null)
			{
				if (flag)
				{
					GameArt.Label(new Rect(r.x + 8f * p, r.y, r.width - 16f * p, r.height), craft.ShowAll ? "Pick a recipe" : "Gather wood, stone or ore to craft. Show unable to craft lists every recipe", 4.6f, TextAnchor.MiddleCenter, GameArt.Grey, wrap: true);
				}
				return null;
			}
			float num = Mathf.Round(5f * p);
			float num2 = Mathf.Round(Mathf.Min(38f * p, r.height * 0.42f));
			Rect rect = new Rect(r.x + num, r.y + num, num2, num2);
			bool flag2 = craft.Working == sel;
			if (flag)
			{
				float num3 = (flag2 ? (BlowShake(craft) * p) : 0f);
				PortraitWell(new Rect(rect.x + num3, rect.y, rect.width, rect.height), RecipeIcon(sel));
				if (flag2 && craft.Progress >= 0f)
				{
					float num4 = Mathf.Round((rect.height - 4f * p) * craft.Progress);
					GameArt.Flat(new Rect(rect.x + 2f * p, rect.yMax - 2f * p - num4, rect.width - 4f * p, num4), new Color(1f, 0.7f, 0.2f, 0.32f));
				}
			}
			if (flag && flag2 && craft.Blows > 0)
			{
				DrawSparks(rect.center, craft.BlowAt, craft.Blows, p * 1.3f, SparkTint(sel));
			}
			float num5 = rect.xMax + 6f * p;
			float width = r.xMax - num - num5;
			float num6 = r.y + num - 1f * p;
			string text2 = Crafting.Locked(host, sel);
			if (flag)
			{
				GameArt.Label(new Rect(num5, num6, width, 10f * p), sel.Name, 6f, TextAnchor.MiddleLeft, Color.white, wrap: false, sel.Supply != null);
				GameArt.Label(new Rect(num5, num6 + 9.5f * p, width, 8f * p), RecipeKind(sel), 4.2f, TextAnchor.MiddleLeft, (text2 != null) ? StatDown : CardFaint);
			}
			num6 += 19f * p;
			text = ((sel.Supply != null) ? (DrawSupplyLines(new Rect(num5, num6, width, 18f * p), host, sel.Supply) ?? text) : (DrawStatLines(new Rect(num5, num6, width, 18f * p), host, sel) ?? text));
			float num7 = Mathf.Max(rect.yMax + 3f * p, num6 + 19f * p);
			float num8 = r.x + num;
			if (flag)
			{
				GameArt.Label(new Rect(num8, num7, 24f * p, 12f * p), "Needs", 4.3f, TextAnchor.MiddleLeft, GameArt.Peach);
			}
			num8 += 24f * p;
			bool flag3 = sel.Res2 != null && sel.Res2 == sel.Res1;
			text = MaterialChip(ref num8, num7, host, sel.Res1, flag3 ? (sel.Amount1 + sel.Amount2) : sel.Amount1) ?? text;
			if (sel.Res2 != null && !flag3)
			{
				text = MaterialChip(ref num8, num7, host, sel.Res2, sel.Amount2) ?? text;
			}
			if (flag)
			{
				string text3 = sel.Seconds.ToString("0.#") + " s";
				GameArt.Label(new Rect(r.xMax - num - 40f * p, num7, 40f * p, 12f * p), text3, 4.2f, TextAnchor.MiddleRight, CardFaint);
			}
			float y = num7 + 14f * p;
			if (sel.Asset != null)
			{
				text = DrawOdds(new Rect(r.x + num, y, r.width - 2f * num, 9f * p), host, sel) ?? text;
			}
			float num9 = Mathf.Round(16f * p);
			float y2 = Mathf.Round(r.yMax - num - num9);
			string text4 = craft.WhyNot(host, sel);
			bool flag4 = text4 == null;
			float num10 = Mathf.Round(Mathf.Min(110f * p, r.width * 0.45f));
			Rect r2 = new Rect(Mathf.Round(r.x + (r.width - num10) * 0.5f - ((sel.Supply != null) ? (18f * p) : 0f)), y2, num10, num9);
			string label = (flag4 ? "Craft" : ShortWhy(host, sel, text4));
			Sprite sprite = GameArt.IconNamed("iconCraftIron") ?? GameArt.IconNamed("icon_hammer_steel");
			if (GameButton(r2, label, flag4, flag4, flag4 ? sprite : null, 5.2f) && flag4)
			{
				AskCraft(host, craft, sel, 1);
			}
			if (!flag4 && r2.Contains(current.mousePosition))
			{
				text = text4;
			}
			if (sel.Supply != null)
			{
				int num11 = Mathf.Min(5, Crafting.AffordCount(host, sel));
				Rect r3 = new Rect(r2.xMax + 4f * p, y2, Mathf.Round(32f * p), num9);
				bool flag5 = flag4 && num11 > 1;
				if (GameButton(r3, "x" + 5, red: false, flag5) && flag5)
				{
					AskCraft(host, craft, sel, 5);
				}
				if (r3.Contains(current.mousePosition))
				{
					text = (flag5 ? ("Up to " + 5 + ". You can pay for " + num11) : (text4 ?? "You can pay for one"));
				}
			}
			return text;
		}

		private void AskCraft(Actor host, Crafting craft, Crafting.Recipe r, int count)
		{
			if (craft.Start(host, r, _mod.Toast, count))
			{
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
		}

		private static string RecipeKind(Crafting.Recipe r)
		{
			string text = ((r.Supply != null) ? "Supply" : (Crafting.IsTool(r) ? "Tool" : ((r.Slot == EquipmentType.Weapon) ? "Weapon" : SlotName(r.Slot))));
			string text2 = null;
			try
			{
				text2 = r.Asset?.material;
			}
			catch
			{
			}
			if (!string.IsNullOrEmpty(text2) && text2 != "basic")
			{
				text = text + ". " + char.ToUpperInvariant(text2[0]) + text2.Substring(1);
			}
			return text + ". " + ((r.Rank <= 0) ? "By hand" : ("Workbench level " + r.Rank));
		}

		private string ShortWhy(Actor host, Crafting.Recipe r, string why)
		{
			if (r.Rank > 0)
			{
				if (Workbench.Level(host) < r.Rank)
				{
					return "Level " + r.Rank + " workbench";
				}
				if (!Workbench.AtHome(host))
				{
					return "At home";
				}
			}
			if (why != null && why.StartsWith("No room", StringComparison.Ordinal))
			{
				return "Bag full";
			}
			if (why != null && why.StartsWith("Already", StringComparison.Ordinal))
			{
				return "Queue full";
			}
			if (why != null && why.StartsWith("This creature", StringComparison.Ordinal))
			{
				return "Can't craft";
			}
			return "Missing materials";
		}

		private string DrawStatLines(Rect r, Actor host, Crafting.Recipe sel)
		{
			float p = GameArt.P;
			bool flag = Event.current.type == EventType.Repaint;
			Item item = null;
			try
			{
				item = host.equipment?.getSlot(sel.Slot)?.getItem();
			}
			catch
			{
			}
			BaseStats s = null;
			try
			{
				s = item?.getFullStats();
			}
			catch
			{
			}
			List<string> list = new List<string>(4);
			string[] cardStats = CardStats;
			foreach (string text in cardStats)
			{
				float f = Stat(sel.Asset?.base_stats, text);
				float f2 = Stat(s, text);
				if (Mathf.Abs(f) > 0.001f || Mathf.Abs(f2) > 0.001f)
				{
					list.Add(text);
				}
				if (list.Count == 4)
				{
					break;
				}
			}
			float num = Mathf.Round(r.width * 0.5f);
			float num2 = Mathf.Round(8.5f * p);
			if (Crafting.IsTool(sel) && flag)
			{
				float num3 = Blueprints.ToolFactor(sel.Asset.id);
				GameArt.Label(new Rect(r.x, r.y, r.width, num2), "Builds " + num3.ToString("0.##") + "x as fast", 4.3f, TextAnchor.MiddleLeft, GameArt.Peach);
				return null;
			}
			for (int j = 0; j < list.Count && flag; j++)
			{
				string id = list[j];
				float num4 = Stat(sel.Asset?.base_stats, id);
				float num5 = Stat(s, id);
				Rect r2 = new Rect(r.x + (float)(j % 2) * num, r.y + (float)(j / 2) * num2, num, num2);
				string text2 = StatName(id) + " " + StatText(id, num4);
				GameArt.Label(r2, text2, 4.3f, TextAnchor.MiddleLeft, Color.white);
				if (item != null)
				{
					float num6 = num4 - num5;
					float x = GameArt.Measure(text2, 4.3f).x;
					string text3 = ((Mathf.Abs(num6) < 0.001f) ? "=" : (((num6 > 0f) ? "+" : "-") + StatText(id, Mathf.Abs(num6))));
					GameArt.Label(new Rect(r2.x + x + 3f * p, r2.y, num - x - 3f * p, num2), text3, 4.1f, TextAnchor.MiddleLeft, (Mathf.Abs(num6) < 0.001f) ? CardFaint : ((num6 > 0f) ? StatUp : StatDown));
				}
			}
			if (list.Count == 0 && flag)
			{
				GameArt.Label(new Rect(r.x, r.y, r.width, num2), "No stats of its own", 4.2f, TextAnchor.MiddleLeft, CardFaint);
			}
			if (item != null && new Rect(r.x, r.y, r.width, num2 * 2f).Contains(Event.current.mousePosition))
			{
				return "Against your " + NiceName(item.asset).ToLowerInvariant();
			}
			return null;
		}

		private static float Stat(BaseStats s, string id)
		{
			if (s == null)
			{
				return 0f;
			}
			try
			{
				return s[id];
			}
			catch
			{
				return 0f;
			}
		}

		private string DrawSupplyLines(Rect r, Actor host, Supplies.Kind kind)
		{
			float p = GameArt.P;
			if (Event.current.type != EventType.Repaint)
			{
				return null;
			}
			float num = Mathf.Round(8.5f * p);
			string text = ((kind.Heal > 0f) ? ("Heals " + Mathf.RoundToInt(kind.Heal * 100f) + "% over " + Mathf.RoundToInt(kind.Seconds) + " s") : ((kind.Id == "tw_item_torch") ? "A light to carry" : ((kind.Id == "tw_item_bedroll") ? "A night outdoors, and a save" : ((kind.Id == "tw_item_lamppost" || kind.Id == "tw_item_brazier") ? "Set down with Y" : ""))));
			GameArt.Label(new Rect(r.x, r.y, r.width, num), text, 4.3f, TextAnchor.MiddleLeft, Color.white);
			int num2 = Supplies.Count(host, kind.Id);
			GameArt.Label(new Rect(r.x, r.y + num, r.width, num), ((kind.Makes > 1) ? ("Makes " + kind.Makes + ". ") : "") + "You carry " + num2, 4.2f, TextAnchor.MiddleLeft, CardFaint);
			return null;
		}

		private string MaterialChip(ref float x, float y, Actor host, string res, int need)
		{
			float p = GameArt.P;
			int num = CraftStores.Count(host, res);
			string text = num + "/" + need;
			float num2 = Mathf.Round(12f * p);
			float x2 = GameArt.Measure(text, 4.6f).x;
			Rect rect = new Rect(x, y, num2 + 3f * p + x2, num2);
			if (Event.current.type == EventType.Repaint)
			{
				GameArt.Icon(new Rect(x, y, num2, num2), ResourceIcon(res));
				GameArt.Label(new Rect(x + num2 + 2f * p, y, x2 + 2f * p, num2), text, 4.6f, TextAnchor.MiddleLeft, (num >= need) ? Color.white : StatDown);
			}
			x = rect.xMax + 7f * p;
			if (!rect.Contains(Event.current.mousePosition))
			{
				return null;
			}
			int num3 = CraftStores.InChests(host, res);
			string text2 = Work.ResourceName(res);
			return text2 + "\n" + (num - num3) + " in your bag" + ((num3 > 0) ? (", " + num3 + " in storage") : "");
		}

		private string DrawOdds(Rect r, Actor host, Crafting.Recipe sel)
		{
			float p = GameArt.P;
			int tries = Crafting.Rolls(host);
			float[] array = CraftOdds.For(sel.Asset, tries);
			if (array == null)
			{
				return null;
			}
			if (Event.current.type == EventType.Repaint)
			{
				float num = r.x;
				for (int i = 0; i < array.Length; i++)
				{
					int num2 = Mathf.RoundToInt(array[i] * 100f);
					if (num2 > 0 || i <= 0)
					{
						string text = CraftOdds.Word((Rarity)i) + " " + num2 + "%";
						float x = GameArt.Measure(text, 4.2f).x;
						GameArt.Label(new Rect(num, r.y, x + 2f * p, r.height), text, 4.2f, TextAnchor.MiddleLeft, CraftOdds.ColorOf((Rarity)i));
						num += x + 7f * p;
					}
				}
			}
			if (!r.Contains(Event.current.mousePosition))
			{
				return null;
			}
			int num3 = Workbench.Usable(host);
			return "Rolls " + tries + "\n" + ((num3 > 0) ? ("Your workbench adds " + num3) : ((Workbench.Level(host) > 0) ? "Your workbench adds more at home" : "A workbench adds a roll per level"));
		}

		private static float RevealHold(Crafting.Made m)
		{
			if (m.Piece != null)
			{
				if (m.Quality != Rarity.R3_Legendary)
				{
					if (m.Quality != Rarity.R2_Epic)
					{
						if (m.Quality != Rarity.R1_Rare)
						{
							return 1.9f;
						}
						return 2.4f;
					}
					return 2.8f;
				}
				return 3.4f;
			}
			return 1.4f;
		}

		private static float BlowShake(Crafting craft)
		{
			float num = Time.unscaledTime - craft.BlowAt;
			if (num < 0f || num > 0.18f)
			{
				return 0f;
			}
			return Mathf.Round(Mathf.Sin(num * 70f) * (1f - num / 0.18f) * 1.5f);
		}

		private static Color SparkTint(Crafting.Recipe r)
		{
			if (r == null)
			{
				return Spark;
			}
			if (r.Supply != null)
			{
				return new Color(0.62f, 0.85f, 0.4f);
			}
			if (r.Rank <= 0)
			{
				if (!(r.Res1 == "stone"))
				{
					return new Color(0.85f, 0.62f, 0.35f);
				}
				return new Color(0.8f, 0.78f, 0.72f);
			}
			if (r.Rank != 1)
			{
				return new Color(1f, 0.72f, 0.25f);
			}
			return new Color(0.85f, 0.83f, 0.78f);
		}

		private static void DrawSparks(Vector2 origin, float at, int seed, float scale, Color tint)
		{
			float num = Time.unscaledTime - at;
			if (num < 0f || num > 0.42f)
			{
				return;
			}
			for (int i = 0; i < 9; i++)
			{
				int num2 = (seed * 7919 + i * 104729) * -1640531535;
				float t = (float)(uint)(num2 & 0xFFFF) / 65535f;
				float num3 = (float)(((uint)num2 >> 16) & 0xFFFF) / 65535f;
				float f = Mathf.Lerp(200f, 340f, t) * (MathF.PI / 180f);
				float num4 = (40f + 70f * num3) * scale;
				float num5 = 0.42f * (0.55f + 0.45f * num3);
				if (!(num > num5))
				{
					float num6 = num / num5;
					Vector2 vector = new Vector2(origin.x + Mathf.Cos(f) * num4 * num, origin.y + Mathf.Sin(f) * num4 * num + 160f * scale * num * num);
					float num7 = Mathf.Max(1f, Mathf.Round(scale * ((num6 < 0.5f) ? 1.6f : 1f)));
					Color c = Color.Lerp(Color.white, tint, Mathf.Clamp01(num6 * 2.5f));
					c.a = 1f - num6 * num6;
					GameArt.Flat(new Rect(Mathf.Round(vector.x), Mathf.Round(vector.y), num7, num7), c);
				}
			}
		}

		private bool DrawCraftResult(Rect anchor, Rect bag)
		{
			Crafting.Made last = _mod.Craft.Last;
			if (last == null)
			{
				return false;
			}
			float p = GameArt.P;
			float num = Time.unscaledTime - last.At;
			float num2 = RevealHold(last);
			if (num < 0f || num > num2 + 0.45f)
			{
				return false;
			}
			Event current = Event.current;
			Color color = CraftOdds.ColorOf(last.Quality);
			Sprite s = RecipeIcon(last.Recipe);
			float num3 = Mathf.Round(Mathf.Min(anchor.width, 168f * p));
			float num4 = Mathf.Round(46f * p);
			Rect r = new Rect(Mathf.Round(anchor.center.x - num3 * 0.5f), Mathf.Round(anchor.center.y - num4 * 0.5f), num3, num4);
			float num5 = Mathf.Round(34f * p);
			Rect r2 = new Rect(r.x + 6f * p, Mathf.Round(r.center.y - num5 * 0.5f), num5, num5);
			if (num < num2)
			{
				if (current.type != EventType.Repaint)
				{
					return true;
				}
				float num6 = ((num < 0.22f) ? Mathf.Lerp(0.7f, 1.06f, num / 0.22f) : Mathf.Lerp(1.06f, 1f, Mathf.Clamp01((num - 0.22f) / 0.12f)));
				GameArt.Flat(anchor, new Color(0f, 0f, 0f, 0.45f * Mathf.Clamp01(num / 0.15f)));
				Matrix4x4 matrix = GUI.matrix;
				GUIUtility.ScaleAroundPivot(new Vector2(num6, num6), r.center);
				if (last.Quality >= Rarity.R1_Rare)
				{
					DrawRays(r2.center, num, (int)last.Quality, p, color);
				}
				GameArt.TipPanel(r);
				GameArt.Sliced(r, GameArt.ButtonBorder, new Color(color.r, color.g, color.b, (last.Piece != null) ? 0.9f : 0.5f), 4f, 4f, 4f, 4f, p);
				PortraitWell(r2, s);
				float num7 = r2.xMax + 6f * p;
				float width = r.xMax - 6f * p - num7;
				if (last.Piece != null)
				{
					GameArt.Label(new Rect(num7, r.y + 4f * p, width, 8f * p), CraftOdds.Word(last.Quality), 4.6f, TextAnchor.MiddleLeft, color);
					GameArt.Label(new Rect(num7, r.y + 12f * p, width, 11f * p), last.Name, 6.2f, TextAnchor.MiddleLeft, color, wrap: false, translate: false);
					GameArt.Label(new Rect(num7, r.y + 24f * p, width, 8f * p), Kept(last), 4.2f, TextAnchor.MiddleLeft, new Color(0.85f, 0.85f, 0.88f), wrap: false, translate: false);
					GameArt.Label(new Rect(num7, r.y + 33f * p, width, 8f * p), last.Spilled ? "At your feet (E to pick up)" : "In your bag", 4.1f, TextAnchor.MiddleLeft, last.Spilled ? GameArt.Orange : GameArt.Grey);
				}
				else
				{
					GameArt.Label(new Rect(num7, r.y + 8f * p, width, 8f * p), "Made", 4.6f, TextAnchor.MiddleLeft, GameArt.Peach);
					GameArt.Label(new Rect(num7, r.y + 16f * p, width, 11f * p), CraftedName(last.Name), 6.2f, TextAnchor.MiddleLeft, Color.white);
					GameArt.Label(new Rect(num7, r.y + 28f * p, width, 8f * p), last.Spilled ? "At your feet (E to pick up)" : "In your bag", 4.1f, TextAnchor.MiddleLeft, last.Spilled ? GameArt.Orange : GameArt.Grey);
				}
				if (num < 0.3f)
				{
					GameArt.Flat(r, new Color(1f, 1f, 1f, 0.55f * (1f - num / 0.3f)));
				}
				GUI.matrix = matrix;
				return true;
			}
			if (current.type != EventType.Repaint)
			{
				return true;
			}
			float num8 = Mathf.Clamp01((num - num2) / 0.45f);
			float num9 = num8 * num8 * (3f - 2f * num8);
			if (num8 < 0.4f)
			{
				Color color2 = GUI.color;
				GUI.color = new Color(1f, 1f, 1f, 1f - num8 / 0.4f);
				GameArt.TipPanel(r);
				GUI.color = color2;
			}
			Vector2 center = r2.center;
			Vector2 center2 = bag.center;
			Vector2 vector = Vector2.Lerp(center, center2, num9) + new Vector2(0f, (0f - Mathf.Sin(num9 * MathF.PI)) * 30f * p);
			float num10 = Mathf.Lerp(num5, 14f * p, num9);
			if (num8 < 0.98f)
			{
				GameArt.Icon(new Rect(Mathf.Round(vector.x - num10 * 0.5f), Mathf.Round(vector.y - num10 * 0.5f), num10, num10), s, new Color(1f, 1f, 1f, 1f));
			}
			DrawSparks(center2, last.At + num2 + 0.40499997f, last.Serial, p, color);
			return true;
		}

		private void CraftResultInput(Rect anchor)
		{
			Crafting.Made last = _mod.Craft.Last;
			Event current = Event.current;
			if (last == null || current.type != EventType.MouseDown)
			{
				return;
			}
			float p = GameArt.P;
			float num = Time.unscaledTime - last.At;
			float num2 = RevealHold(last);
			if (!(num < 0f) && !(num >= num2))
			{
				float num3 = Mathf.Round(Mathf.Min(anchor.width, 168f * p));
				float num4 = Mathf.Round(46f * p);
				if (new Rect(Mathf.Round(anchor.center.x - num3 * 0.5f), Mathf.Round(anchor.center.y - num4 * 0.5f), num3, num4).Contains(current.mousePosition))
				{
					last.At = Time.unscaledTime - num2;
					current.Use();
				}
			}
		}

		private static void DrawRays(Vector2 c, float t, int grade, float k, Color q)
		{
			Matrix4x4 matrix = GUI.matrix;
			int num = 4 + grade * 2;
			float num2 = (18f + 7f * (float)grade) * k;
			float num3 = Mathf.Max(1f, Mathf.Round((1.5f + (float)grade * 0.5f) * k));
			float a = 0.35f + 0.15f * (float)grade + 0.1f * Mathf.Sin(t * 6f);
			for (int i = 0; i < num; i++)
			{
				GUI.matrix = matrix;
				GUIUtility.RotateAroundPivot(t * 40f + (float)i * 360f / (float)num, c);
				GameArt.Flat(new Rect(c.x - num3 * 0.5f, c.y - num2, num3, num2), new Color(q.r, q.g, q.b, a));
			}
			GUI.matrix = matrix;
		}

		private static string Kept(Crafting.Made m)
		{
			if (m.Piece?.Mods == null || m.Piece.Mods.Length == 0)
			{
				return "No enchantments";
			}
			string[] array = new string[m.Piece.Mods.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = CraftOdds.ModName(m.Piece.Mods[i]);
			}
			return string.Join(", ", array);
		}

		private static string CraftedName(string s)
		{
			if (string.IsNullOrEmpty(s))
			{
				return s;
			}
			if (s.StartsWith("a ", StringComparison.Ordinal))
			{
				s = s.Substring(2);
			}
			return char.ToUpperInvariant(s[0]) + s.Substring(1);
		}

		private void DrawForgeHud(float sw, float sh)
		{
			Crafting craft = _mod.Craft;
			if (craft.Open)
			{
				return;
			}
			float p = GameArt.P;
			float num = Mathf.Round(sh * 0.62f);
			if (craft.Working != null && Event.current.type == EventType.Repaint)
			{
				int num2 = 0;
				foreach (Crafting.Order item in craft.Queue)
				{
					num2 += item.Left;
				}
				num2--;
				string text = craft.Waiting ?? ((craft.AtBench ? "Forging " : "Making ") + craft.Working.Name.ToLowerInvariant());
				float num3 = Mathf.Round(Mathf.Max(120f * p, GameArt.Measure(text, 5.2f).x + 44f * p));
				Rect r = new Rect(Mathf.Round(sw * 0.5f - num3 * 0.5f), num, num3, Mathf.Round(24f * p));
				GameArt.TipPanel(r);
				float num4 = Mathf.Round(18f * p);
				Rect r2 = new Rect(r.x + 4f * p + BlowShake(craft) * p, r.y + 3f * p, num4, num4);
				PortraitWell(r2, RecipeIcon(craft.Working));
				float num5 = r.x + num4 + 9f * p;
				float width = r.xMax - 5f * p - num5;
				GameArt.Label(new Rect(num5, r.y + 2f * p, width, 9f * p), text, 5.2f, TextAnchor.MiddleLeft, (craft.Waiting != null) ? GameArt.Orange : Color.white);
				if (num2 > 0)
				{
					GameArt.Label(new Rect(num5, r.y + 2f * p, width, 9f * p), "+" + num2, 4.4f, TextAnchor.MiddleRight, GameArt.Orange);
				}
				float frac = Mathf.Max(0f, craft.Progress);
				GameArt.Bar(new Rect(num5, r.y + 13f * p, width, 6f * p), frac, (craft.Waiting != null) ? GameArt.Grey : GameArt.AgeYellow);
				if (craft.Blows > 0)
				{
					DrawSparks(r2.center, craft.BlowAt, craft.Blows, p, SparkTint(craft.Working));
				}
			}
			DrawCraftResult(bag: new Rect(_bagLeft, _bagTop, Mathf.Round(22f * p), Mathf.Round(22f * p)), anchor: new Rect(sw * 0.5f - 90f * p, num - 54f * p, 180f * p, 50f * p));
		}

		private static bool IsDropdown(SettingsMenu.Row r)
		{
			if (r.IsChoice)
			{
				return r.Dropdown;
			}
			return false;
		}

		private static void PadStepsChoice(Rect r, SettingsMenu.Row row, SettingsMenu menu, Settings s)
		{
			PadNav.Slider(r, delegate(int d)
			{
				int num = row.Chosen(s);
				int num2 = Mathf.Clamp(num + d, 0, row.Choices.Length - 1);
				if (num2 != num && menu.Drop == null)
				{
					menu.Pick(row, s, num2);
					Sounds.PlayUI("event:/SFX/Ui/Click");
				}
			});
		}

		private void SettingsDropdown(Rect r, SettingsMenu.Row row, SettingsMenu menu, Settings s)
		{
			float p = GameArt.P;
			_dropAt[row] = new Rect(r.x + _pageOrigin.x, r.y + _pageOrigin.y, r.width, r.height);
			if (Event.current.type == EventType.Repaint)
			{
				Vector2 vector = GUIUtility.GUIToScreenPoint(r.min);
				Vector2 vector2 = GUIUtility.GUIToScreenPoint(r.max);
				_dropOnScreen[row.Name] = Rect.MinMaxRect(vector.x, vector.y, vector2.x, vector2.y);
			}
			bool flag = menu.Drop == row;
			int i = row.Chosen(s);
			bool flag2 = _pageMouseIn && r.Contains(Event.current.mousePosition);
			GameArt.FlatButton(r, red: false, null, 5f, (flag2 || flag) ? 1f : 0.9f);
			DropLabel(new Rect(r.x + 6f * p, r.y - 0.5f * p, r.width - 20f * p, r.height), row, i, 5f, Color.white);
			DropGeometry g;
			bool up = flag && DropLayout(menu, out g) && g.Up;
			DropArrow(new Vector2(r.xMax - 8f * p, r.center.y), Color.white, up);
			bool num = PageButton(r);
			if (PadNav.Wanted)
			{
				PadStepsChoice(r, row, menu, s);
			}
			if (num)
			{
				if (flag)
				{
					menu.ShutDrop();
				}
				else
				{
					menu.OpenDrop(row, s);
				}
			}
		}

		private bool DropLayout(SettingsMenu menu, out DropGeometry g)
		{
			g = default(DropGeometry);
			SettingsMenu.Row drop = menu.Drop;
			if (drop == null || !_dropAt.TryGetValue(drop, out var value))
			{
				return false;
			}
			float p = GameArt.P;
			int num = drop.Choices.Length;
			float num2 = Mathf.Round(12f * p);
			float num3 = Mathf.Round(3f * p);
			float num4 = Mathf.Round(4f * p);
			float num5 = Mathf.Round(1f * p);
			float num6 = (float)num * num2;
			float num7 = num6 + num3 * 2f;
			float num8 = (float)Screen.height - num4 - (value.yMax + num5);
			float num9 = value.y - num5 - num4;
			bool flag = num7 > num8 && (num7 <= num9 || num9 > num8);
			float num10 = Mathf.Max(num2 + num3 * 2f, Mathf.Min(num7, flag ? num9 : num8));
			float num11 = Mathf.Max(value.width, Mathf.Round(70f * p));
			Rect panel = new Rect(value.xMax - num11, flag ? (value.y - num5 - num10) : (value.yMax + num5), num11, num10);
			Rect view = new Rect(panel.x + num3, panel.y + num3, panel.width - num3 * 2f, panel.height - num3 * 2f);
			float num12 = Mathf.Max(0f, num6 - view.height);
			Rect bar = default(Rect);
			if (num12 > 0.5f)
			{
				float num13 = Mathf.Round(5f * p);
				bar = new Rect(view.xMax - num13, view.y, num13, view.height);
				view.width -= num13 + Mathf.Round(2f * p);
			}
			else
			{
				num12 = 0f;
			}
			if (menu.DropScroll < 0f)
			{
				menu.DropScroll = (float)menu.DropLit * num2 - (view.height - num2) * 0.5f;
			}
			else if (menu.DropFollow)
			{
				float num14 = (float)menu.DropLit * num2;
				if (num14 < menu.DropScroll)
				{
					menu.DropScroll = num14;
				}
				else if (num14 + num2 > menu.DropScroll + view.height)
				{
					menu.DropScroll = num14 + num2 - view.height;
				}
			}
			menu.DropScroll = Mathf.Clamp(menu.DropScroll, 0f, num12);
			g = new DropGeometry
			{
				Panel = panel,
				View = view,
				Bar = bar,
				ItemH = num2,
				ContentH = num6,
				Max = num12,
				Up = flag
			};
			return true;
		}

		private static Rect DropItem(SettingsMenu menu, DropGeometry g, int i)
		{
			return new Rect(g.View.x, Mathf.Round(g.View.y + (float)i * g.ItemH - menu.DropScroll), g.View.width, g.ItemH);
		}

		private static int DropItemAt(SettingsMenu menu, DropGeometry g, Vector2 p)
		{
			if (!g.View.Contains(p))
			{
				return -1;
			}
			int num = Mathf.FloorToInt((p.y - g.View.y + menu.DropScroll) / g.ItemH);
			if (num < 0 || num >= menu.Drop.Choices.Length)
			{
				return -1;
			}
			return num;
		}

		private void DropInput(SettingsMenu menu, Settings s)
		{
			Event current = Event.current;
			if (DropTake(menu, s, current.type, current.mousePosition, current.button, current.delta, current.keyCode))
			{
				current.Use();
			}
		}

		private bool DropTake(SettingsMenu menu, Settings s, EventType type, Vector2 p, int button, Vector2 delta, KeyCode key)
		{
			if (_dropHeld && (type == EventType.MouseDrag || type == EventType.MouseUp))
			{
				if (_dropBarHeld && type == EventType.MouseDrag && DropLayout(menu, out var g))
				{
					DropBarTo(menu, g, p.y);
				}
				if (type == EventType.MouseUp)
				{
					_dropHeld = (_dropBarHeld = false);
				}
				return true;
			}
			SettingsMenu.Row drop = menu.Drop;
			if (drop == null)
			{
				return false;
			}
			List<SettingsMenu.Row> list = menu.RowsOf(menu.Tab);
			if (!menu.Open || PacksUi.Open || list == null || !list.Contains(drop))
			{
				menu.ShutDrop();
				return false;
			}
			if (!DropLayout(menu, out var g2))
			{
				return false;
			}
			switch (type)
			{
			case EventType.MouseDown:
				_dropHeld = true;
				if (g2.Max > 0f && new Rect(g2.Bar.x - GameArt.P, g2.Bar.y, g2.Bar.width + 2f * GameArt.P, g2.Bar.height).Contains(p))
				{
					_dropBarHeld = true;
					DropBarTo(menu, g2, p.y);
					return true;
				}
				if (g2.Panel.Contains(p))
				{
					int num3 = DropItemAt(menu, g2, p);
					if (num3 >= 0 && button == 0)
					{
						DropChoose(menu, s, num3);
					}
					return true;
				}
				menu.ShutDrop();
				return true;
			case EventType.ScrollWheel:
				if (g2.Max > 0f)
				{
					menu.DropScroll = Mathf.Clamp(menu.DropScroll + delta.y / 3f * g2.ItemH, 0f, g2.Max);
				}
				return true;
			case EventType.KeyDown:
			{
				int num = drop.Choices.Length;
				int num2 = Mathf.Max(1, Mathf.FloorToInt(g2.View.height / g2.ItemH) - 1);
				switch (key)
				{
				case KeyCode.UpArrow:
					menu.DropMove(-1);
					break;
				case KeyCode.DownArrow:
					menu.DropMove(1);
					break;
				case KeyCode.PageUp:
					menu.DropMove(-num2);
					break;
				case KeyCode.PageDown:
					menu.DropMove(num2);
					break;
				case KeyCode.Home:
					menu.DropMove(-num);
					break;
				case KeyCode.End:
					menu.DropMove(num);
					break;
				case KeyCode.Return:
				case KeyCode.KeypadEnter:
					DropChoose(menu, s, menu.DropLit);
					break;
				case KeyCode.Escape:
					menu.EscapeDrop();
					break;
				default:
					return false;
				}
				return true;
			}
			default:
				return false;
			}
		}

		private static void DropBarTo(SettingsMenu menu, DropGeometry g, float y)
		{
			float num = DropThumbH(g);
			menu.DropScroll = Mathf.Clamp01((y - g.Bar.y - num * 0.5f) / Mathf.Max(1f, g.Bar.height - num)) * g.Max;
		}

		private static float DropThumbH(DropGeometry g)
		{
			return Mathf.Max(10f * GameArt.P, g.Bar.height * Mathf.Clamp01(g.View.height / Mathf.Max(1f, g.ContentH)));
		}

		private static void DropChoose(SettingsMenu menu, Settings s, int i)
		{
			SettingsMenu.Row drop = menu.Drop;
			menu.ShutDrop();
			if (drop != null && i >= 0 && i < drop.Choices.Length)
			{
				menu.Pick(drop, s, i);
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
		}

		private void DrawDropList(SettingsMenu menu, Settings s)
		{
			if (menu.Drop == null || !DropLayout(menu, out var g))
			{
				if (_dropItemsOnScreen.Length != 0)
				{
					_dropItemsOnScreen = new Rect[0];
				}
				return;
			}
			Event current = Event.current;
			bool flag = current.type == EventType.Repaint;
			SettingsMenu.Row drop = menu.Drop;
			int num = drop.Choices.Length;
			int num2 = drop.Chosen(s);
			float p = GameArt.P;
			if (flag)
			{
				Vector2 mousePosition = current.mousePosition;
				if ((mousePosition - _dropMouse).sqrMagnitude > 0.25f)
				{
					int num3 = DropItemAt(menu, g, mousePosition);
					if (num3 >= 0)
					{
						menu.DropLit = num3;
					}
				}
				_dropMouse = mousePosition;
				if (_dropItemsOnScreen.Length != num)
				{
					_dropItemsOnScreen = new Rect[num];
				}
			}
			int num4 = Mathf.Clamp(Mathf.FloorToInt(menu.DropScroll / g.ItemH), 0, num - 1);
			int num5 = Mathf.Clamp(Mathf.CeilToInt((menu.DropScroll + g.View.height) / g.ItemH), 0, num);
			for (int i = num4; i < num5; i++)
			{
				Rect gui = Clip(DropItem(menu, g, i), g.View);
				if (gui.height >= g.ItemH * 0.5f)
				{
					PadNav.Spot(gui);
				}
			}
			if (!flag)
			{
				return;
			}
			for (int j = 0; j < num; j++)
			{
				Rect rect = ((j >= num4 && j < num5) ? Clip(DropItem(menu, g, j), g.View) : default(Rect));
				Vector2 vector = GUIUtility.GUIToScreenPoint(rect.min);
				Vector2 vector2 = GUIUtility.GUIToScreenPoint(rect.max);
				_dropItemsOnScreen[j] = ((rect.height > 0f) ? Rect.MinMaxRect(vector.x, vector.y, vector2.x, vector2.y) : default(Rect));
			}
			if (menu.DropFollow)
			{
				menu.DropFollow = false;
				if (PadControls.Active && PadControls.Shown)
				{
					Vector2 vector3 = GUIUtility.GUIToScreenPoint(Clip(DropItem(menu, g, menu.DropLit), g.View).center);
					PadControls.Place(new Vector2(vector3.x, (float)Screen.height - vector3.y));
				}
			}
			Rect panel = g.Panel;
			GameArt.Sliced(new Rect(panel.x + p, panel.y + 1.5f * p, panel.width, panel.height), GameArt.Tooltip, new Color(0f, 0f, 0f, 0.45f), 7f, 7f, 7f, 7f);
			GameArt.Sliced(panel, GameArt.Tooltip, Color.white, 7f, 7f, 7f, 7f);
			GameArt.Sliced(panel, GameArt.ButtonBorder, new Color(1f, 1f, 1f, 0.35f), 4f, 4f, 4f, 4f);
			Sprite sprite = GameArt.IconNamed("iconOn");
			GUI.BeginClip(g.View);
			try
			{
				Vector2 vector4 = new Vector2(g.View.x, g.View.y);
				for (int k = num4; k < num5; k++)
				{
					Rect rect2 = DropItem(menu, g, k);
					Rect r = new Rect(rect2.x - vector4.x, rect2.y - vector4.y, rect2.width, rect2.height - Mathf.Round(1f * p));
					bool flag2 = k == num2;
					bool flag3 = k == menu.DropLit;
					if (flag2)
					{
						GameArt.FlatButton(r, red: true, null, 5f, flag3 ? 1f : 0.9f);
					}
					else if (flag3)
					{
						GameArt.FlatButton(r, red: false, null, 5f);
					}
					float num6 = ((flag2 && sprite != null) ? Mathf.Round(9f * p) : 0f);
					DropLabel(new Rect(r.x + 5f * p, r.y - 0.5f * p, r.width - 8f * p - num6, r.height), drop, k, 5f, (flag2 || flag3) ? Color.white : new Color(0.86f, 0.86f, 0.84f));
					if (num6 > 0f)
					{
						Rect r2 = new Rect(Mathf.Round(r.xMax - num6 - 2f * p), Mathf.Round(r.center.y - num6 * 0.5f), num6, num6);
						GameArt.Icon(new Rect(r2.x + p * 0.5f, r2.y + p * 0.5f, num6, num6), sprite, new Color(0f, 0f, 0f, 0.35f));
						GameArt.Icon(r2, sprite, Color.white);
					}
					if (flag3)
					{
						GameArt.Sliced(r, GameArt.ButtonBorder, new Color(1f, 0.82f, 0.3f, 1f), 4f, 4f, 4f, 4f, Mathf.Min(p, r.height / 12f));
					}
				}
			}
			finally
			{
				GUI.EndClip();
			}
			if (g.Max > 0f)
			{
				float num7 = DropThumbH(g);
				Rect r3 = new Rect(g.Bar.x, Mathf.Round(g.Bar.y + (g.Bar.height - num7) * (menu.DropScroll / g.Max)), g.Bar.width, Mathf.Round(num7));
				if (GameArt.ScrollTrack != null)
				{
					GameArt.Sliced(g.Bar, GameArt.ScrollTrack, Color.white, -1f, -1f, -1f, -1f, g.Bar.width / 17f);
				}
				else
				{
					GameArt.Sliced(g.Bar, GameArt.DarkField, Color.white, 3f, 3f, 3f, 3f);
				}
				GameArt.Sliced(r3, GameArt.RedButton, Color.white, 5f, 5f, 5f, 6f, Mathf.Min(p, g.Bar.width / 10f));
			}
		}

		private static Rect Clip(Rect r, Rect to)
		{
			float num = Mathf.Max(r.xMin, to.xMin);
			float num2 = Mathf.Max(r.yMin, to.yMin);
			return Rect.MinMaxRect(num, num2, Mathf.Max(num, Mathf.Min(r.xMax, to.xMax)), Mathf.Max(num2, Mathf.Min(r.yMax, to.yMax)));
		}

		private static void DropLabel(Rect r, SettingsMenu.Row row, int i, float size, Color color)
		{
			if (Event.current.type != EventType.Repaint || i < 0 || i >= row.Choices.Length)
			{
				return;
			}
			string text = row.Choices[i];
			string text2 = ((row.Codes != null && i < row.Codes.Length) ? row.Codes[i] : null);
			if (text2 == null)
			{
				GameArt.Label(r, text, size, TextAnchor.MiddleLeft, color);
				return;
			}
			GUIStyle gUIStyle = GameArt.Style(size);
			Font font = gUIStyle.font;
			FontStyle fontStyle = gUIStyle.fontStyle;
			bool bold;
			Font font2 = Lang.FaceFor(text, text2, font, out bold);
			if (font2 != null)
			{
				gUIStyle.font = font2;
			}
			if (bold)
			{
				gUIStyle.fontStyle = FontStyle.Normal;
			}
			float num = Mathf.Max(1f, Mathf.Round(GameArt.P * 0.75f));
			try
			{
				gUIStyle.normal.textColor = new Color(0f, 0f, 0f, 0.5f * color.a);
				GUI.Label(new Rect(r.x + num, r.y + num, r.width, r.height), text, gUIStyle);
				gUIStyle.normal.textColor = color;
				GUI.Label(r, text, gUIStyle);
			}
			finally
			{
				gUIStyle.font = font;
				gUIStyle.fontStyle = fontStyle;
			}
		}

		private static void DropArrow(Vector2 c, Color color, bool up)
		{
			float num = Mathf.Max(1f, Mathf.Round(GameArt.P * 0.75f));
			for (int i = 0; i < 2; i++)
			{
				float num2 = ((i == 0) ? num : 0f);
				Color c2 = ((i == 0) ? new Color(0f, 0f, 0f, 0.4f * color.a) : color);
				for (int j = 0; j < 4; j++)
				{
					int num3 = 7 - j * 2;
					float num4 = c.y + (up ? (1.5f - (float)j) : ((float)j - 1.5f)) * num;
					GameArt.Flat(new Rect(Mathf.Round(c.x - num * (float)num3 * 0.5f + num2), Mathf.Round(num4 - num * 0.5f + num2), num * (float)num3, num), c2);
				}
			}
		}

		internal bool DropButtonForTest(string name, out Rect r)
		{
			return _dropOnScreen.TryGetValue(name, out r);
		}

		internal bool DropEventForTest(SettingsMenu menu, Settings s, Event e)
		{
			if (DropTake(menu, s, e.type, e.mousePosition, e.button, e.delta, e.keyCode))
			{
				return true;
			}
			if (e.type != EventType.MouseUp || menu.Drop != null)
			{
				return false;
			}
			List<SettingsMenu.Row> list = menu.RowsOf(menu.Tab);
			foreach (KeyValuePair<SettingsMenu.Row, Rect> item in _dropAt)
			{
				if (item.Value.Contains(e.mousePosition) && list != null && list.Contains(item.Key))
				{
					menu.OpenDrop(item.Key, s);
					return true;
				}
			}
			return false;
		}

		internal bool DropItemForTest(int i, out Rect r)
		{
			r = ((i >= 0 && i < _dropItemsOnScreen.Length) ? _dropItemsOnScreen[i] : default(Rect));
			if (r.width > 0f)
			{
				return r.height > 0f;
			}
			return false;
		}

		public void DrawFamilyMap()
		{
			EnsureAssets();
			FamilyMap familyMap = _mod.FamilyMap;
			Actor host = _mod.Host;
			if (host == null)
			{
				familyMap.Open = false;
				return;
			}
			Event current = Event.current;
			bool flag = current.type == EventType.Repaint;
			bool close;
			Rect rect = MapWindow("Your Family", out close);
			if (close)
			{
				familyMap.Open = false;
				return;
			}
			float p = GameArt.P;
			float num = Mathf.Round(190f * p);
			Rect r = new Rect(rect.x + num + 10f * p, rect.y, rect.xMax - (rect.x + num + 10f * p), rect.height);
			GameArt.Sliced(r, GameArt.Inner, Color.white);
			Rect mapRect = new Rect(Mathf.Round(r.x + 4f * p), Mathf.Round(r.y + 4f * p), Mathf.Round(r.width - 8f * p), Mathf.Round(r.height - 8f * p));
			FamilyMapRectForTest = mapRect;
			if (flag)
			{
				FmPanKeys(familyMap);
			}
			float num2 = familyMap.Span / Mathf.Max(0.01f, familyMap.Zoom);
			float ppt = mapRect.width / num2;
			float viewH = mapRect.height / ppt;
			familyMap.Centre = SmClamp(familyMap.Centre, num2, viewH);
			float vx0 = familyMap.Centre.x - num2 * 0.5f;
			float vy0 = familyMap.Centre.y - viewH * 0.5f;
			Actor actor = null;
			if (flag)
			{
				Fill(mapRect, Minimap.FogColor);
				Minimap minimap = _mod.Minimap;
				float num3 = Mathf.Max(minimap.WorldW, minimap.WorldH);
				if (minimap.WorldLand != null && num3 > 0f)
				{
					float num4 = Mathf.Max(vx0, 0f);
					float num5 = Mathf.Max(vy0, 0f);
					float num6 = Mathf.Min(vx0 + num2, num3);
					float num7 = Mathf.Min(vy0 + viewH, num3);
					if (num6 > num4 && num7 > num5)
					{
						Vector2 vector = ToScreen(new Vector2(num4, num7));
						Vector2 vector2 = ToScreen(new Vector2(num6, num5));
						GUI.DrawTextureWithTexCoords(Rect.MinMaxRect(vector.x, vector.y, vector2.x, vector2.y), minimap.WorldLand, new Rect(num4 / num3, num5 / num3, (num6 - num4) / num3, (num7 - num5) / num3));
					}
				}
			}
			actor = FmContents(familyMap, host, mapRect, ToScreen, ppt, current, flag);
			FmButtons(familyMap, host, mapRect);
			FmInput(familyMap, host, mapRect, ToWorld, ToScreen, ppt, current);
			if (flag && actor != null && mapRect.Contains(current.mousePosition) && _fmButton < 0)
			{
				bool flag2 = FamilyFollow.Members.Contains(actor);
				string text = (flag2 ? (FamilyFollow.Name(actor) + ", your " + FamilyFollow.Relation(host, actor)) : WorldBoxMod.Describe(actor));
				string text2 = (flag2 ? FamilyDoing(actor) : Favorites.Doing(actor));
				string text3 = "";
				try
				{
					text3 = actor.getHealth() + "/" + actor.getMaxHealth() + " hp";
				}
				catch
				{
				}
				string text4 = (flag2 ? (OrderText(actor) + "   " + text3) : ("Foe   " + text3));
				float width = Mathf.Max(GameArt.Measure(text, 5f).x, Mathf.Max(GameArt.Measure(text4, 4.2f).x, GameArt.Measure(text2, 4.2f).x)) + 12f * p;
				Rect r2 = new Rect(current.mousePosition.x + 12f * p, current.mousePosition.y + 8f * p, width, 30f * p);
				if (r2.xMax > (float)Screen.width)
				{
					r2.x = current.mousePosition.x - r2.width - 8f * p;
				}
				GameArt.TipPanel(r2);
				GameArt.Label(new Rect(r2.x + 6f * p, r2.y + 1.5f * p, r2.width, 9f * p), text, 5f, TextAnchor.MiddleLeft, flag2 ? FamilyGreen : DangerRed);
				GameArt.Label(new Rect(r2.x + 6f * p, r2.y + 10.5f * p, r2.width, 8f * p), text2, 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
				GameArt.Label(new Rect(r2.x + 6f * p, r2.y + 19f * p, r2.width, 8f * p), text4, 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
			}
			FmPanel(familyMap, host, new Rect(rect.x, rect.y, num, rect.height));
			Vector2 ToScreen(Vector2 vector3)
			{
				return new Vector2(mapRect.x + (vector3.x - vx0) * ppt, mapRect.y + (vy0 + viewH - vector3.y) * ppt);
			}
			Vector2 ToWorld(Vector2 s)
			{
				return new Vector2(vx0 + (s.x - mapRect.x) / ppt, vy0 + viewH - (s.y - mapRect.y) / ppt);
			}
		}

		private static string FamilyDoing(Actor a)
		{
			try
			{
				if (FamilyFollow.OnOurTask(a))
				{
					if (FamilyFollow.OrderOf(a) == FamilyFollow.Orders.Wait)
					{
						return a._is_moving ? "On the way" : "Waiting";
					}
					return (((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id == "fp_family_run") ? "Catching up" : (a._is_moving ? "Walking with you" : "By your side");
				}
				string text = Favorites.Doing(a);
				string[] array = new string[3] { "child ", "sexual ", "task " };
				foreach (string text2 in array)
				{
					if (text.StartsWith(text2))
					{
						text = text.Substring(text2.Length);
					}
				}
				if (text.StartsWith("reproduction"))
				{
					return "With their lover";
				}
				if (text.Contains("random") || text.Contains("jump"))
				{
					return "Playing about";
				}
				if (text.Contains("sleep"))
				{
					return "Asleep";
				}
				if (text.Contains("eat") || text.Contains("food"))
				{
					return "Eating";
				}
				return (text.Length > 0) ? (char.ToUpperInvariant(text[0]) + text.Substring(1)) : text;
			}
			catch
			{
				return "";
			}
		}

		private static string OrderText(Actor a)
		{
			switch (FamilyFollow.OrderOf(a))
			{
			case FamilyFollow.Orders.Follow:
				return "Following you";
			case FamilyFollow.Orders.Wait:
				return "Waiting";
			default:
				return "Living their life";
			}
		}

		private Actor FmContents(FamilyMap map, Actor host, Rect mapRect, Func<Vector2, Vector2> toScreen, float ppt, Event ev, bool repaint)
		{
			float p = GameArt.P;
			float unscaledTime = Time.unscaledTime;
			float num = 0.5f + 0.5f * Mathf.Sin(unscaledTime * 5f);
			Vector2 origin = new Vector2(mapRect.x, mapRect.y);
			Vector2 vector = ev.mousePosition - origin;
			Actor result = null;
			float num2 = 10f * p * 10f * p;
			float num3 = Mathf.Round(Mathf.Clamp(ppt * 1.1f, 3.5f * p, 5f * p));
			if (repaint)
			{
				GUI.BeginGroup(mapRect);
			}
			float num4 = Mathf.Round(Mathf.Clamp(ppt * 4f, 10f * p, 17f * p));
			if (repaint)
			{
				foreach (FamilyMap.Town town in map.Towns)
				{
					Vector2 s = S(town.P);
					if (!In(s, 60f * p))
					{
						continue;
					}
					Rect r = new Rect(Mathf.Round(s.x - num4 * 0.4f), Mathf.Round(s.y - num4 * 0.85f), Mathf.Round(num4 * 0.8f), num4);
					if (town.Home)
					{
						float num5 = num4 * 1.6f;
						GUI.color = new Color(0.45f, 1f, 0.45f, 0.4f);
						GUI.DrawTexture(new Rect(s.x - num5 * 0.5f, r.center.y - num5 * 0.5f, num5, num5), Skin.DiscTex);
						GUI.color = Color.white;
					}
					bool civ = false;
					try
					{
						civ = town.Owner != null && town.Owner.isCiv();
					}
					catch
					{
					}
					if (!DrawBanner(r, town.Owner, civ, 1f))
					{
						GameArt.Flat(new Rect(s.x - 4f * p, s.y - 4f * p, 8f * p, 8f * p), new Color(0f, 0f, 0f, 0.7f));
						GameArt.Flat(new Rect(s.x - 3f * p, s.y - 3f * p, 6f * p, 6f * p), (town.Owner != null) ? Royal.ColorOf(town.Owner) : Color.white);
					}
					Color color = Color.white;
					try
					{
						if (town.Owner != null)
						{
							color = town.Owner.getColor().getColorText();
						}
					}
					catch
					{
					}
					GameArt.Label(new Rect(s.x - 60f * p, r.yMax, 120f * p, 9f * p), town.Name + (town.Home ? " (home)" : ""), 4.3f, TextAnchor.UpperCenter, color);
				}
			}
			HashSet<WorldTile> hashSet = new HashSet<WorldTile>();
			foreach (Actor member in FamilyFollow.Members)
			{
				if (FamilyFollow.OrderOf(member) != FamilyFollow.Orders.Wait)
				{
					continue;
				}
				WorldTile worldTile = FamilyFollow.WaitPlace(member);
				if (worldTile == null)
				{
					continue;
				}
				Vector2 vector2 = S(new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f));
				if (repaint)
				{
					try
					{
						Dashed(S(member.current_position), vector2, Mathf.Max(1f, Mathf.Round(0.9f * p)), new Color(FamilyGreen.r, FamilyGreen.g, FamilyGreen.b, 0.6f), 4f * p, 0f);
					}
					catch
					{
					}
				}
				if (hashSet.Add(worldTile) && repaint)
				{
					float num6 = Mathf.Round(14f * p);
					GameArt.Flat(new Rect(Mathf.Round(vector2.x), Mathf.Round(vector2.y - num6), Mathf.Max(1f, Mathf.Round(p)), num6), new Color(0.25f, 0.16f, 0.1f));
					GameArt.Flat(new Rect(Mathf.Round(vector2.x + p), Mathf.Round(vector2.y - num6), 7f * p, 5f * p), FamilyGreen);
					PixelDiamond(vector2, 1, FamilyGreen);
				}
			}
			foreach (FamilyMap.Dot fo in map.Foes)
			{
				Vector2 vector3 = S(fo.P);
				if (In(vector3, 10f * p))
				{
					if (repaint)
					{
						GameArt.Flat(new Rect(Mathf.Round(vector3.x) - num3 * 0.5f - p, Mathf.Round(vector3.y) - num3 * 0.5f - p, num3 + 2f * p, num3 + 2f * p), new Color(0.05f, 0.04f, 0.04f, 0.95f));
						GameArt.Flat(new Rect(Mathf.Round(vector3.x) - num3 * 0.5f, Mathf.Round(vector3.y) - num3 * 0.5f, num3, num3), DangerRed);
					}
					float sqrMagnitude = (vector3 - vector).sqrMagnitude;
					if (sqrMagnitude < num2)
					{
						num2 = sqrMagnitude;
						result = fo.Actor;
					}
				}
			}
			Actor actor = null;
			float num7 = 12f * p * 12f * p;
			List<Vector2> list = new List<Vector2>();
			foreach (Actor member2 in FamilyFollow.Members)
			{
				Vector2 current_position;
				try
				{
					if (member2 == null || !member2.isAlive())
					{
						continue;
					}
					current_position = member2.current_position;
					goto IL_066f;
				}
				catch
				{
				}
				continue;
				IL_066f:
				Vector2 vector4 = S(current_position);
				if (!In(vector4, 20f * p))
				{
					continue;
				}
				float num8 = Mathf.Round(vector4.x);
				float num9 = Mathf.Round(vector4.y);
				float num10 = num3 + p;
				bool flag = map.Picked.Contains(member2);
				Actor by;
				bool flag2 = FamilyFollow.InDanger(member2, out by);
				if (repaint)
				{
					if (flag)
					{
						float num11 = num10 + 5f * p;
						GameArt.Flat(new Rect(num8 - num11 * 0.5f, num9 - num11 * 0.5f, num11, num11), ArmyGold);
					}
					Color c = (flag2 ? new Color(DangerRed.r, DangerRed.g, DangerRed.b, 0.6f + 0.4f * num) : new Color(0.05f, 0.04f, 0.04f, 0.95f));
					GameArt.Flat(new Rect(num8 - num10 * 0.5f - p, num9 - num10 * 0.5f - p, num10 + 2f * p, num10 + 2f * p), c);
					GameArt.Flat(new Rect(num8 - num10 * 0.5f, num9 - num10 * 0.5f, num10, num10), (FamilyFollow.OrderOf(member2) == FamilyFollow.Orders.Free) ? new Color(0.7f, 0.85f, 0.7f) : FamilyGreen);
					bool flag3 = false;
					foreach (Vector2 item in list)
					{
						if (Mathf.Abs(item.x - num8) < 34f * p && Mathf.Abs(item.y - num9) < 10f * p)
						{
							flag3 = true;
							break;
						}
					}
					if (!flag3 || flag)
					{
						list.Add(new Vector2(num8, num9));
						GameArt.Label(new Rect(num8 - 50f * p, num9 - num10 * 0.5f - 10f * p, 100f * p, 9f * p), FamilyFollow.Name(member2), 4.2f, TextAnchor.LowerCenter, flag ? ArmyGold : Color.white);
					}
				}
				float sqrMagnitude2 = (vector4 - vector).sqrMagnitude;
				if (sqrMagnitude2 < num7)
				{
					num7 = sqrMagnitude2;
					actor = member2;
				}
			}
			if (actor != null)
			{
				result = actor;
			}
			if (repaint)
			{
				Vector2 vector5 = S(host.current_position);
				float viewYaw = _mod.ViewYaw;
				Line(vector5, vector5 + new Vector2(Mathf.Cos(viewYaw), 0f - Mathf.Sin(viewYaw)) * 14f * p, Mathf.Max(2f, p), new Color(1f, 1f, 1f, 0.95f));
				PixelDiamond(vector5, 2, Color.white);
				GUI.EndGroup();
			}
			return result;
			bool In(Vector2 vector6, float m)
			{
				if (vector6.x > 0f - m && vector6.y > 0f - m && vector6.x < mapRect.width + m)
				{
					return vector6.y < mapRect.height + m;
				}
				return false;
			}
			Vector2 S(Vector2 w)
			{
				return toScreen(w) - origin;
			}
		}

		private void FmButtons(FamilyMap map, Actor host, Rect mapRect)
		{
			float p = GameArt.P;
			float num = Mathf.Round(16f * p);
			float x = mapRect.xMax - num - 4f * p;
			float num2 = mapRect.y + 4f * p;
			string tip = null;
			Rect tipAt = default(Rect);
			Rect r = new Rect(x, num2, num, num);
			Tip(r, "Zoom in");
			if (GameButton(r, "+", red: false, map.Zoom < map.ZoomMax - 0.001f, null, 7f))
			{
				FmZoomAbout(map, mapRect, mapRect.center, 1.5f);
			}
			Rect r2 = new Rect(x, num2 + num + 2f * p, num, num);
			Tip(r2, "Zoom out");
			if (GameButton(r2, "−", red: false, map.Zoom > map.ZoomMin + 0.001f, null, 7f))
			{
				FmZoomAbout(map, mapRect, mapRect.center, 2f / 3f);
			}
			float num3 = num2 + (num + 2f * p) * 2f + 3f * p;
			Rect r3 = new Rect(x, num3, num, num);
			Tip(r3, "You and your family");
			if (GameButton(r3, null, red: false, enabled: true, GameArt.IconNamed("iconFamily") ?? GameArt.IconNamed("iconHumans")))
			{
				map.Fit(host);
			}
			Rect r4 = new Rect(x, num3 + num + 2f * p, num, num);
			Tip(r4, "Centre on you");
			if (GameButton(r4, null, red: false, enabled: true, GameArt.IconNamed("iconInspect")))
			{
				map.Centre = host.current_position;
			}
			Rect r5 = new Rect(x, num3 + (num + 2f * p) * 2f, num, num);
			Tip(r5, "The whole world");
			if (GameButton(r5, null, red: false, enabled: true, GameArt.IconNamed("iconWorldInfo")))
			{
				map.Zoom = map.ZoomMin;
				map.Centre = new Vector2(WarMap.WorldWidth * 0.5f, WarMap.WorldHeight * 0.5f);
			}
			if (tip != null && Event.current.type == EventType.Repaint)
			{
				float num4 = GameArt.Measure(tip, 4.6f).x + 10f * p;
				Rect r6 = new Rect(tipAt.x - num4 - 3f * p, tipAt.y + 2f * p, num4, 12f * p);
				GameArt.TipPanel(r6);
				GameArt.Label(r6, tip, 4.6f, TextAnchor.MiddleCenter, Color.white);
			}
			if (Event.current.type == EventType.Repaint)
			{
				Rect r7 = new Rect(mapRect.x + 4f * p, mapRect.yMax - 14f * p, Mathf.Min(mapRect.width - 8f * p, 250f * p), 11f * p);
				GameArt.TipPanel(r7);
				GameArt.Label(r7, "Left: pick one   Drag: move   Wheel: zoom   Right click: wait there", 4f, TextAnchor.MiddleCenter, Color.white);
			}
			void Tip(Rect rect, string text)
			{
				if (rect.Contains(Event.current.mousePosition))
				{
					tip = text;
					tipAt = rect;
				}
			}
		}

		private void FmPanKeys(FamilyMap map)
		{
			if (!_mod.IsTypingNow)
			{
				Vector2 zero = Vector2.zero;
				if (PlayInput.GetKey(KeyCode.A) || PlayInput.GetKey(KeyCode.LeftArrow))
				{
					zero.x -= 1f;
				}
				if (PlayInput.GetKey(KeyCode.D) || PlayInput.GetKey(KeyCode.RightArrow))
				{
					zero.x += 1f;
				}
				if (PlayInput.GetKey(KeyCode.W) || PlayInput.GetKey(KeyCode.UpArrow))
				{
					zero.y += 1f;
				}
				if (PlayInput.GetKey(KeyCode.S) || PlayInput.GetKey(KeyCode.DownArrow))
				{
					zero.y -= 1f;
				}
				if (!(zero == Vector2.zero))
				{
					map.Centre += zero.normalized * (map.Span / Mathf.Max(0.01f, map.Zoom)) * 0.8f * Mathf.Min(0.1f, Time.unscaledDeltaTime);
				}
			}
		}

		private static void FmZoomAbout(FamilyMap map, Rect mapRect, Vector2 at, float factor)
		{
			float num = map.Span / Mathf.Max(0.01f, map.Zoom);
			float num2 = mapRect.width / num;
			float num3 = mapRect.height / num2;
			Vector2 vector = new Vector2(map.Centre.x - num * 0.5f + (at.x - mapRect.x) / num2, map.Centre.y + num3 * 0.5f - (at.y - mapRect.y) / num2);
			map.Zoom = Mathf.Clamp(map.Zoom * factor, map.ZoomMin, map.ZoomMax);
			float num4 = mapRect.width / (map.Span / map.Zoom);
			map.Centre = new Vector2(vector.x - (at.x - mapRect.center.x) / num4, vector.y + (at.y - mapRect.center.y) / num4);
		}

		private void FmInput(FamilyMap map, Actor host, Rect mapRect, Func<Vector2, Vector2> toWorld, Func<Vector2, Vector2> toScreen, float ppt, Event ev)
		{
			switch (ev.type)
			{
			case EventType.ScrollWheel:
				if (mapRect.Contains(ev.mousePosition))
				{
					FmZoomAbout(map, mapRect, ev.mousePosition, (ev.delta.y > 0f) ? (5f / 6f) : 1.2f);
					ev.Use();
				}
				break;
			case EventType.MouseDown:
				if (mapRect.Contains(ev.mousePosition) && ev.button <= 2)
				{
					_fmButton = ev.button;
					_fmDragFrom = ev.mousePosition;
					_fmCentreFrom = map.Centre;
					_fmDragged = false;
					ev.Use();
				}
				break;
			case EventType.MouseDrag:
				if (_fmButton >= 0)
				{
					if ((ev.mousePosition - _fmDragFrom).sqrMagnitude > 36f)
					{
						_fmDragged = true;
					}
					if (_fmDragged)
					{
						Vector2 vector = ev.mousePosition - _fmDragFrom;
						map.Centre = _fmCentreFrom - new Vector2(vector.x / ppt, (0f - vector.y) / ppt);
					}
					ev.Use();
				}
				break;
			case EventType.MouseUp:
			{
				if (_fmButton < 0)
				{
					break;
				}
				int fmButton = _fmButton;
				_fmButton = -1;
				if (!_fmDragged && fmButton == 0)
				{
					Actor actor = null;
					float num = 12f * GameArt.P * 12f * GameArt.P;
					foreach (Actor member in FamilyFollow.Members)
					{
						try
						{
							if (member != null && member.isAlive())
							{
								float sqrMagnitude = (toScreen(member.current_position) - ev.mousePosition).sqrMagnitude;
								if (sqrMagnitude < num)
								{
									num = sqrMagnitude;
									actor = member;
								}
							}
						}
						catch
						{
						}
					}
					if (actor == null)
					{
						map.Picked.Clear();
					}
					else if (!map.Picked.Remove(actor))
					{
						map.Picked.Add(actor);
					}
					Sounds.PlayUI("event:/SFX/Ui/Click");
				}
				else if (!_fmDragged && fmButton == 1)
				{
					WorldTile worldTile = TileAt(toWorld(ev.mousePosition));
					bool flag = false;
					if (worldTile != null && Royal.Standable(worldTile))
					{
						foreach (Actor item in map.Whom())
						{
							try
							{
								if (item.current_tile != null && item.current_tile.isSameIsland(worldTile))
								{
									flag = true;
									break;
								}
							}
							catch
							{
							}
						}
					}
					if (flag)
					{
						FmOrder(map, host, FamilyFollow.Orders.Wait, worldTile);
					}
					else
					{
						_mod.Toast((worldTile == null || !Royal.Standable(worldTile)) ? "Nobody can wait there." : "They can't get there from where they are (it's across the water).");
					}
				}
				_fmDragged = false;
				ev.Use();
				break;
			}
			case EventType.MouseMove:
			case EventType.KeyDown:
			case EventType.KeyUp:
				break;
			}
		}

		private void FmOrder(FamilyMap map, Actor host, FamilyFollow.Orders order, WorldTile at)
		{
			if (FamilyFollow.Members.Count == 0)
			{
				_mod.Toast("You have no family to give orders to.");
			}
			else if (FamilyFollow.Give(host, map.Whom(), order, at) != 0)
			{
				Sounds.PlayUI("event:/SFX/Ui/Click");
				string text = map.WhomName();
				string text2 = char.ToUpperInvariant(text[0]) + text.Substring(1);
				switch (order)
				{
				case FamilyFollow.Orders.Follow:
					_mod.Toast((text == "everyone") ? "\"Come with me.\" Your family follows you." : ("\"Come with me.\" " + text2 + " follows you."));
					return;
				case FamilyFollow.Orders.Free:
					_mod.Toast((text == "everyone") ? "\"Go about your day.\" Your family goes back to their lives." : ("\"Go about your day.\" " + text2 + " goes back to their life."));
					return;
				}
				string text3 = ((at == null) ? "here" : Service.Heading(host.current_position, new Vector2((float)at.x + 0.5f, (float)at.y + 0.5f)));
				_mod.Toast("\"Wait for me " + ((text3 == "here") ? "here" : "there") + ".\" " + ((text == "everyone") ? "Your family waits" : (text2 + " waits")) + " " + ((text3 == "here") ? "here" : text3) + ".");
			}
		}

		private void FmPanel(FamilyMap map, Actor host, Rect panel)
		{
			float p = GameArt.P;
			bool flag = Event.current.type == EventType.Repaint;
			float y = panel.y;
			List<Actor> members = FamilyFollow.Members;
			City city = null;
			try
			{
				city = host.city;
			}
			catch
			{
			}
			SectionTitle(new Rect(panel.x, y, panel.width, Mathf.Round(20f * p)), "Household", GameArt.IconNamed("iconFamily"));
			y += 22f * p;
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			List<string> list = new List<string>();
			List<string> list2 = new List<string>();
			foreach (Actor item in members)
			{
				try
				{
					if (item != null && item.isAlive())
					{
						if ((item.current_position - host.current_position).magnitude <= 10f)
						{
							num++;
						}
						if (FamilyFollow.Eats(item) && item.isHungry())
						{
							num2++;
							list.Add(FamilyFollow.Name(item));
						}
						if (FamilyFollow.InDanger(item, out var _))
						{
							num3++;
							list2.Add(FamilyFollow.Name(item));
						}
					}
				}
				catch
				{
				}
			}
			string text = ((city != null) ? ("Home: " + city.name + ((city.kingdom != null) ? (" of " + city.kingdom.name) : "")) : "No home: you wander, as nomads do");
			y += Para(panel.x + 2f * p, y, panel.width - 4f * p, text, 4.5f, (city != null) ? Color.white : HungerOrange) + 1f * p;
			if (members.Count == 0)
			{
				y += Para(panel.x + 2f * p, y, panel.width - 4f * p, "You have no family yet. Court someone to find a lover (talk, then Flirt). Your children will be here too.", 4.3f, GameArt.Grey) + 2f * p;
			}
			else
			{
				y += Para(panel.x + 2f * p, y, panel.width - 4f * p, $"With you: {num} of {members.Count}", 4.5f, (num == members.Count) ? FamilyGreen : Color.white);
				y += Para(panel.x + 2f * p, y, panel.width - 4f * p, (num2 == 0) ? "Everyone is fed" : ("Hungry: " + string.Join(", ", list.ToArray())), 4.5f, (num2 == 0) ? FamilyGreen : HungerOrange);
				if (num3 > 0)
				{
					y += Para(panel.x + 2f * p, y, panel.width - 4f * p, "Under attack: " + string.Join(", ", list2.ToArray()), 4.5f, DangerRed);
				}
				y += 2f * p;
			}
			if (city != null && Run.LockedOn(host) && !host.isKing() && !host.isCityLeader())
			{
				y += Para(panel.x + 2f * p, y, panel.width - 4f * p, "To found a town of your own: leave this one (talk to its leader), then Settle here on open land. Or serve your king and ask for a charter.", 4.2f, GameArt.Grey) + 2f * p;
			}
			float num4 = Mathf.Round(15f * p);
			float num5 = 2f * p;
			float num6 = Mathf.Round((panel.width - num5) * 0.5f);
			bool flag2 = city == null && Village.CanFound(host);
			int num7 = (flag2 ? 3 : 2);
			float num8 = panel.yMax - (float)num7 * (num4 + num5) - 11f * p;
			Rect view = new Rect(panel.x, y, panel.width - 8f * p, Mathf.Max(20f * p, num8 - y - 3f * p));
			float num9 = Mathf.Round(31f * p);
			float num10 = Mathf.Round(2f * p);
			float contentH = Mathf.Max(view.height, (float)members.Count * (num9 + num10));
			GameArt.Sliced(new Rect(view.x - 2f * p, view.y - 2f * p, view.width + 4f * p, view.height + 4f * p), GameArt.Inner, new Color(1f, 1f, 1f, 0.5f));
			float num11 = BeginList(view, ref map.ListScroll, contentH);
			for (int i = 0; i < members.Count; i++)
			{
				Actor actor = members[i];
				Rect rect = new Rect(0f, num11 + (float)i * (num9 + num10), view.width, num9);
				bool picked = map.Picked.Contains(actor);
				bool over = _listMouseIn && rect.Contains(Event.current.mousePosition);
				if (Event.current.type == EventType.Repaint)
				{
					FmRow(rect, host, actor, picked, over);
				}
				PadNav.Spot(rect);
				if (GUI.Button(rect, GUIContent.none, GUIStyle.none) && _listMouseIn)
				{
					if (!map.Picked.Remove(actor))
					{
						map.Picked.Add(actor);
					}
					Sounds.PlayUI("event:/SFX/Ui/Click");
				}
			}
			EndList(view, ref map.ListScroll, contentH);
			float num12 = num8;
			if (flag)
			{
				GameArt.Label(new Rect(panel.x + 2f * p, num12, panel.width - 4f * p, 9f * p), "Orders to " + map.WhomName() + ((map.Picked.Count > 0) ? " (click again to unpick)" : ""), 4.3f, TextAnchor.MiddleLeft, GameArt.Peach);
			}
			num12 += 11f * p;
			bool enabled = members.Count > 0;
			if (GameButton(new Rect(panel.x, num12, num6, num4), "Follow me", red: true, enabled, GameArt.IconNamed("iconArrowFamilyMembers"), 4.6f))
			{
				FmOrder(map, host, FamilyFollow.Orders.Follow, null);
			}
			if (GameButton(new Rect(panel.x + num6 + num5, num12, num6, num4), "Wait here", red: false, enabled, GameArt.IconNamed("iconArrowDestination"), 4.6f))
			{
				WorldTile at = null;
				try
				{
					at = host.current_tile;
				}
				catch
				{
				}
				FmOrder(map, host, FamilyFollow.Orders.Wait, at);
			}
			num12 += num4 + num5;
			if (GameButton(new Rect(panel.x, num12, num6, num4), "Live your lives", red: false, enabled, GameArt.IconNamed("iconHumans"), 4.4f))
			{
				FmOrder(map, host, FamilyFollow.Orders.Free, null);
			}
			int num13 = FamilyFollow.FoodInBag(host);
			if (GameButton(new Rect(panel.x + num6 + num5, num12, num6, num4), "Share food" + ((num13 > 0) ? (" (" + num13 + ")") : ""), num2 > 0 && num13 > 0, enabled, GameArt.IconNamed("iconHunger") ?? GameArt.IconNamed("iconResMeat"), 4.4f))
			{
				_mod.Toast(FamilyFollow.ShareFood(host, map.Picked));
			}
			num12 += num4 + num5;
			if (flag2 && GameButton(new Rect(panel.x, num12, panel.width, num4), "Settle: place your village's bonfire" + Founding.Note(host), red: true, enabled: true, GameArt.IconNamed("iconVillages") ?? GameArt.IconNamed("iconCity"), 4.6f))
			{
				map.Open = false;
				Planning.BeginFounding(host);
			}
		}

		private void FmRow(Rect row, Actor host, Actor a, bool picked, bool over)
		{
			float p = GameArt.P;
			GameArt.Sliced(row, GameArt.Inner, over ? Color.white : new Color(0.9f, 0.9f, 0.9f, 1f));
			if (picked)
			{
				GameArt.Sliced(row, GameArt.ButtonBorder, new Color(1f, 0.82f, 0.3f, 1f), 4f, 4f, 4f, 4f);
			}
			Actor by;
			bool flag = FamilyFollow.InDanger(a, out by);
			float num = row.x + 5f * p;
			float num2 = row.width - 10f * p;
			GameArt.Label(new Rect(num, row.y + 1.5f * p, num2 * 0.6f, 9f * p), FamilyFollow.Name(a), 5f, TextAnchor.MiddleLeft, flag ? DangerRed : Color.white);
			string text = FamilyFollow.Relation(host, a);
			int num3 = 0;
			try
			{
				num3 = a.getAge();
			}
			catch
			{
			}
			GameArt.Label(new Rect(num, row.y + 1.5f * p, num2, 9f * p), text + ", " + num3, 4.2f, TextAnchor.MiddleRight, GameArt.Grey);
			float f = 1f;
			float num4 = 1f;
			try
			{
				f = Mathf.Clamp01((float)a.getHealth() / (float)Mathf.Max(1, a.getMaxHealth()));
			}
			catch
			{
			}
			bool flag2 = FamilyFollow.Eats(a);
			try
			{
				if (flag2)
				{
					num4 = Mathf.Clamp01(a.getNutritionRatio());
				}
			}
			catch
			{
			}
			float y = row.y + 12f * p;
			float num5 = (num2 - 4f * p) * 0.5f;
			float height = Mathf.Round(4f * p);
			Bar2(new Rect(num, y, num5, height), f, new Color(0.85f, 0.2f, 0.18f));
			if (flag2)
			{
				Bar2(new Rect(num + num5 + 4f * p, y, num5, height), num4, (num4 < 0.3f) ? HungerOrange : new Color(0.85f, 0.65f, 0.3f));
			}
			Color color = ((FamilyFollow.OrderOf(a) == FamilyFollow.Orders.Free) ? GameArt.Grey : FamilyGreen);
			string text2 = (flag ? "Under attack!" : FamilyDoing(a));
			if (flag2 && a.isHungry())
			{
				text2 = (flag ? (text2 + " ") : "") + "Hungry";
			}
			GameArt.Label(new Rect(num, row.y + 18f * p, num2 * 0.5f, 10f * p), OrderText(a), 4.2f, TextAnchor.MiddleLeft, color);
			GameArt.Label(new Rect(num + num2 * 0.4f, row.y + 18f * p, num2 * 0.6f, 10f * p), text2, 4.1f, TextAnchor.MiddleRight, flag ? DangerRed : GameArt.Grey);
		}

		private static void Bar2(Rect r, float f, Color c)
		{
			GameArt.Flat(r, new Color(0f, 0f, 0f, 0.55f));
			if (f > 0f)
			{
				GameArt.Flat(new Rect(r.x, r.y, Mathf.Max(1f, r.width * f), r.height), c);
			}
		}

		private void DrawFamilyTree(Conversation c, Rect area)
		{
			FamilyTree tree = c.Tree;
			float p = GameArt.P;
			if (area.height < 70f * p)
			{
				return;
			}
			bool flag = false;
			bool flag2 = tree.Parents[0] != null || tree.Parents[1] != null;
			bool flag3 = tree.Children.Count > 0;
			TreeNode[] grandparents = tree.Grandparents;
			for (int i = 0; i < grandparents.Length; i++)
			{
				if (grandparents[i] != null)
				{
					flag = true;
				}
			}
			int num = 1 + (flag ? 1 : 0) + (flag2 ? 1 : 0) + (flag3 ? 1 : 0);
			float num2 = 24f * p;
			float num3 = Mathf.Min(36f * p, (area.height - num2 - 8f * p) / 4f);
			float num4 = num2 + 8f * p + (float)num * num3;
			if (num4 < area.height)
			{
				area = new Rect(area.x, area.yMax - num4, area.width, num4);
			}
			GameArt.StonePanel(area);
			List<string> list = new List<string>();
			if (tree.FamilyName != null)
			{
				list.Add("The " + tree.FamilyName);
			}
			if (tree.ClanName != null)
			{
				list.Add("clan " + tree.ClanName + (string.IsNullOrEmpty(tree.Motto) ? "" : (": \"" + tree.Motto + "\"")));
			}
			if (tree.Generation > 0)
			{
				list.Add("generation " + tree.Generation);
			}
			SectionTitle(new Rect(area.x + 4f * p, area.y + 3f * p, area.width - 8f * p, Mathf.Round(20f * p)), (list.Count > 0) ? string.Join("   ·   ", list.ToArray()) : "Family", GameArt.IconNamed("iconFamily") ?? GameArt.IconNamed("iconFamilyList"));
			float top = area.y + num2 + 2f * p;
			float num5 = area.yMax - 6f * p;
			float rowH = (num5 - top) / (float)num;
			int[] rowOf = new int[4];
			int num6 = 0;
			rowOf[0] = (flag ? num6++ : (-1));
			rowOf[1] = (flag2 ? num6++ : (-1));
			rowOf[2] = num6++;
			rowOf[3] = (flag3 ? num6 : (-1));
			float num7 = area.width - 12f * p;
			float cardH = Mathf.Round(Mathf.Min(rowH - 5f * p, 26f * p));
			float cardW = Mathf.Round(Mathf.Min(num7 / 6.4f, 70f * p));
			float x = area.center.x;
			float num8 = Mathf.Min(num7 * 0.22f, cardW * 1.35f);
			Rect rect = Card(x - num8, 1);
			Rect rect2 = Card(x + num8, 1);
			Rect rect3 = Card(x, 2);
			Rect[] array = new Rect[4]
			{
				Card(x - num8 - cardW * 0.56f, 0),
				Card(x - num8 + cardW * 0.56f, 0),
				Card(x + num8 - cardW * 0.56f, 0),
				Card(x + num8 + cardW * 0.56f, 0)
			};
			for (int j = 0; j < 4; j++)
			{
				if (tree.Grandparents[j] != null && tree.Parents[j / 2] != null)
				{
					Link(array[j], (j / 2 == 0) ? rect : rect2);
				}
			}
			if (tree.Parents[0] != null)
			{
				Link(rect, rect3);
			}
			if (tree.Parents[1] != null)
			{
				Link(rect2, rect3);
			}
			List<Rect> list2 = new List<Rect>();
			float num9 = cardW + 3f * p;
			int num10 = Mathf.Max(1, Mathf.FloorToInt((num7 * 0.5f - cardW * 0.6f) / num9));
			for (int k = 0; k < tree.Siblings.Count && k < num10; k++)
			{
				list2.Add(Card(x - (float)(k + 1) * num9, 2));
			}
			List<Rect> list3 = new List<Rect>();
			for (int l = 0; l < tree.Partners.Count && l < num10; l++)
			{
				list3.Add(Card(x + (float)(l + 1) * num9, 2));
			}
			int num11 = Mathf.Min(tree.Children.Count, Mathf.FloorToInt(num7 / num9));
			List<Rect> list4 = new List<Rect>();
			for (int m = 0; m < num11; m++)
			{
				list4.Add(Card(x + ((float)m - (float)(num11 - 1) * 0.5f) * num9, 3));
			}
			foreach (Rect item in list4)
			{
				Link(rect3, item);
			}
			for (int n = 0; n < 4; n++)
			{
				Node(c, tree.Grandparents[n], array[n]);
			}
			Node(c, tree.Parents[0], rect);
			Node(c, tree.Parents[1], rect2);
			Node(c, tree.Self, rect3, self: true);
			for (int num12 = 0; num12 < list2.Count; num12++)
			{
				Node(c, tree.Siblings[num12], list2[num12]);
			}
			for (int num13 = 0; num13 < list3.Count; num13++)
			{
				Node(c, tree.Partners[num13], list3[num13]);
			}
			for (int num14 = 0; num14 < list4.Count; num14++)
			{
				Node(c, tree.Children[num14], list4[num14]);
			}
			if (tree.Count == 0)
			{
				GameArt.Label(new Rect(area.x, rect3.yMax + 3f * p, area.width, 10f * p), "No kin that anyone remembers.", 4.8f, TextAnchor.MiddleCenter, GameArt.Grey);
			}
			if (c.Marked != null && c.MarkedName != null)
			{
				GameArt.Label(new Rect(area.x + 8f * p, area.yMax - 11f * p, area.width - 16f * p, 9f * p), "On your compass: " + c.MarkedName, 4.4f, TextAnchor.MiddleLeft, RomanceText);
			}
			Rect Card(float num15, int gen)
			{
				return new Rect(Mathf.Round(num15 - cardW * 0.5f), Mathf.Round(top + (float)Mathf.Max(0, rowOf[gen]) * rowH + (rowH - cardH) * 0.5f), cardW, cardH);
			}
		}

		private void Link(Rect from, Rect to)
		{
			if (Event.current.type == EventType.Repaint)
			{
				float num = Mathf.Max(1f, Mathf.Round(GameArt.P * 0.7f));
				Color c = new Color(0.78f, 0.8f, 0.72f, 0.75f);
				float num2 = Mathf.Round(from.center.x);
				float yMax = from.yMax;
				float num3 = Mathf.Round(to.center.x);
				float y = to.y;
				float num4 = Mathf.Round((yMax + y) * 0.5f);
				Fill(new Rect(num2 - num * 0.5f, yMax, num, num4 - yMax), c);
				Fill(new Rect(Mathf.Min(num2, num3) - num * 0.5f, num4 - num * 0.5f, Mathf.Abs(num3 - num2) + num, num), c);
				Fill(new Rect(num3 - num * 0.5f, num4, num, y - num4), c);
			}
		}

		private void Node(Conversation c, TreeNode n, Rect r, bool self = false)
		{
			if (n == null)
			{
				return;
			}
			float p = GameArt.P;
			bool flag = !n.Dead && n.Actor != null && n.Actor.isAlive();
			bool flag2 = flag && c.Marked == n.Actor;
			if (Event.current.type == EventType.Repaint)
			{
				GameArt.Sliced(r, GameArt.Inner, flag ? Color.white : new Color(0.7f, 0.7f, 0.7f, 1f));
				if (self)
				{
					GameArt.Sliced(r, GameArt.ButtonBorder, new Color(1f, 0.82f, 0.3f, 1f), 4f, 4f, 4f, 4f);
				}
				else if (flag2)
				{
					GameArt.Sliced(r, GameArt.ButtonBorder, new Color(1f, 0.55f, 0.75f, 1f), 4f, 4f, 4f, 4f);
				}
				float num = r.height - 6f * p;
				Rect r2 = new Rect(r.x + 3f * p, r.y + 3f * p, num, num);
				Sprite s = null;
				if (flag)
				{
					try
					{
						s = n.Actor.getSpriteToRender();
					}
					catch
					{
					}
				}
				PortraitWell(r2, s);
				if (!flag)
				{
					GameArt.IconFit(new Rect(r2.x + 2f * p, r2.y + 2f * p, r2.width - 4f * p, r2.height - 4f * p), GameArt.IconNamed("iconDead"), Color.white);
				}
				float num2 = r2.xMax + 3f * p;
				float width = r.xMax - num2 - 2f * p;
				GameArt.Label(new Rect(num2, r.y + 1.5f * p, width, r.height * 0.5f), n.Name ?? "?", 4.6f, TextAnchor.MiddleLeft, flag ? Color.white : GameArt.Grey);
				int num3 = -1;
				if (flag)
				{
					try
					{
						num3 = n.Actor.getAge();
					}
					catch
					{
					}
				}
				if (num3 < 0 && n.Born != int.MinValue)
				{
					num3 = Mathf.Max(0, Chronicle.Year - n.Born);
				}
				string text = ((!n.Dead) ? ((num3 >= 0) ? ("age " + num3) : "") : ((n.Died != int.MinValue) ? ("died " + Ago(n.Died)) : "gone"));
				string text2 = (self ? "" : (n.Role ?? ""));
				GameArt.Label(new Rect(num2, r.y + r.height * 0.5f, width, r.height * 0.45f), text2 + ((text2.Length > 0 && text.Length > 0) ? ", " : "") + text, 3.8f, TextAnchor.MiddleLeft, GameArt.Grey);
			}
			if (!self && flag && Event.current.type == EventType.MouseDown && Event.current.button == 0 && r.Contains(Event.current.mousePosition))
			{
				c.Marked = n.Actor;
				c.MarkedName = n.Name;
				Sounds.PlayUI("event:/SFX/Ui/Click");
				c.Toast?.Invoke("Marked " + n.Name + " on your compass");
				Event.current.Use();
			}
		}

		private static string Ago(int year)
		{
			int num = Chronicle.Year - year;
			if (num > 0)
			{
				if (num != 1)
				{
					return num + " years ago";
				}
				return "last year";
			}
			return "this year";
		}

		public void DrawFavorites()
		{
			EnsureAssets();
			GameArt.Ensure();
			Skin.Ensure();
			Actor host = _mod.Host;
			if (host == null)
			{
				return;
			}
			float num = Screen.width;
			float num2 = Screen.height;
			float p = GameArt.P;
			float favoritesWidth = FavoritesWidth;
			float num3 = Mathf.Round(Mathf.Min(num2 - 40f * p, 330f * p));
			Rect win = new Rect(Mathf.Round(num - favoritesWidth - 8f * p), Mathf.Round((num2 - num3) * 0.5f + 4f * p), favoritesWidth, num3);
			bool close;
			Rect rect = GameWindow(win, "People", null, out close, dim: false);
			if (close)
			{
				_mod.Craft.Open = false;
				return;
			}
			float num4 = Mathf.Round(16f * p);
			float num5 = Mathf.Round(3f * p);
			string[] array = (NetSession.Active ? PeopleTabsMp : PeopleTabs);
			if (Favorites.Tab >= array.Length)
			{
				Favorites.Tab = 0;
			}
			float num6 = Mathf.Round((rect.width - num5 * (float)(array.Length - 1)) / (float)array.Length);
			for (int i = 0; i < array.Length; i++)
			{
				if (GameButton(new Rect(rect.x + (float)i * (num6 + num5), rect.y, num6, num4), array[i], Favorites.Tab == i, enabled: true, (i == 0) ? GameArt.Star : null, 4.8f))
				{
					Favorites.Tab = i;
				}
			}
			Rect inner = new Rect(rect.x, rect.y + num4 + 5f * p, rect.width, rect.height - num4 - 5f * p);
			if (Favorites.Tab == 0)
			{
				DrawStarred(inner, host);
			}
			else if (Favorites.Tab == 2)
			{
				DrawPlayers(inner, host);
			}
			else
			{
				DrawContacts(inner, host);
			}
		}

		private void DrawStarred(Rect inner, Actor host)
		{
			float p = GameArt.P;
			Favorites favs = _mod.Favs;
			Actor favoriteTarget = _mod.FavoriteTarget;
			bool flag = favoriteTarget != null && favoriteTarget != host && SafeAlive(favoriteTarget);
			float num = Mathf.Round(16f * p);
			float y = inner.y;
			string label = (flag ? ((SafeFavorite(favoriteTarget) ? "Unstar " : "Star ") + ShortName(favoriteTarget)) : "Look at someone, then Tab");
			if (GameButton(new Rect(inner.x, y, inner.width, num), label, red: true, flag, GameArt.Star, 4.8f))
			{
				favs.Toggle(favoriteTarget);
				favs.Update(0f, force: true);
			}
			y += num + 3f * p;
			bool flag2 = SafeFavorite(host);
			if (GameButton(new Rect(inner.x, y, inner.width, num), flag2 ? "Unstar yourself" : "Star yourself", red: false, enabled: true, GameArt.Star, 4.8f))
			{
				favs.Toggle(host);
				favs.Update(0f, force: true);
			}
			y += num + 5f * p;
			Rect view = new Rect(inner.x, y, inner.width - 9f * p, inner.yMax - y);
			if (favs.List.Count == 0)
			{
				GameArt.Sliced(new Rect(view.x, view.y, inner.width, view.height), GameArt.Inner, new Color(1f, 1f, 1f, 0.6f));
				GameArt.Label(new Rect(view.x + 6f * p, view.y + 5f * p, inner.width - 12f * p, view.height - 10f * p), "No favourites yet.\n\nStar someone on the Contacts tab, or press B on whoever you look at or talk to.\n\nClick a card to leap into their eyes, or its ping button to mark them. [ and ] go from one to the next.", 4.4f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
				return;
			}
			float num2 = Mathf.Round(30f * p);
			float num3 = Mathf.Round(3f * p);
			float contentH = Mathf.Max(view.height, (float)favs.List.Count * (num2 + num3));
			float num4 = BeginList(view, ref favs.Scroll, contentH);
			Vector2 current_position = host.current_position;
			for (int i = 0; i < favs.List.Count; i++)
			{
				Actor actor = favs.List[i];
				if (!SafeAlive(actor))
				{
					continue;
				}
				Rect rect = new Rect(0f, num4 + (float)i * (num2 + num3), view.width, num2);
				bool flag3 = actor == host;
				float num5 = Mathf.Round(15f * p);
				Rect rect2 = new Rect(rect.xMax - num5 - 4f * p, rect.y + Mathf.Round((num2 - num5) * 0.5f), num5, num5);
				bool flag4 = !flag3 && _mod.Pings.On(actor) != null;
				bool flag5 = _listMouseIn && !flag3 && rect2.Contains(Event.current.mousePosition);
				bool flag6 = _listMouseIn && !flag3 && !flag5 && rect.Contains(Event.current.mousePosition);
				if (Event.current.type == EventType.Repaint)
				{
					GameArt.Sliced(rect, GameArt.Inner, (flag6 || flag3) ? Color.white : new Color(0.9f, 0.9f, 0.9f, 1f));
					if (flag3)
					{
						GameArt.Sliced(rect, GameArt.ButtonBorder, new Color(1f, 0.82f, 0.3f, 1f), 4f, 4f, 4f, 4f);
					}
					else if (flag6)
					{
						GameArt.Sliced(rect, GameArt.ButtonBorder, new Color(1f, 1f, 1f, 0.7f), 4f, 4f, 4f, 4f);
					}
					Color c = new Color(0.5f, 0.45f, 0.4f);
					try
					{
						if (actor.kingdom != null)
						{
							c = Royal.ColorOf(actor.kingdom);
						}
					}
					catch
					{
					}
					GameArt.Flat(new Rect(rect.x + 3f * p, rect.y + 4f * p, 2f * p, rect.height - 8f * p), c);
					Sprite s = null;
					try
					{
						s = actor.getSpriteToRender();
					}
					catch
					{
					}
					PortraitWell(new Rect(rect.x + 7f * p, rect.y + 3f * p, num2 - 6f * p, num2 - 6f * p), s, actor);
					float num6 = rect.x + num2 + 4f * p;
					float num7 = (flag3 ? rect.xMax : rect2.x) - num6 - 4f * p;
					GameArt.Label(new Rect(num6, rect.y + 1.5f * p, num7, 9f * p), (flag3 ? "You: " : "") + CardName(actor), 5f, TextAnchor.MiddleLeft, flag3 ? GameArt.AgeYellow : Color.white);
					string text = ((actor.asset != null) ? actor.asset.id.Replace('_', ' ') : "creature");
					string text2 = People.AgeLabel(actor);
					string text3 = (flag3 ? "" : ("   " + Mathf.RoundToInt((actor.current_position - current_position).magnitude) + " away"));
					GameArt.Label(new Rect(num6, rect.y + 10f * p, num7, 8f * p), text + ((text2 != null) ? (", " + text2) : "") + text3, 3.9f, TextAnchor.MiddleLeft, GameArt.Grey);
					float num8 = 1f;
					try
					{
						num8 = actor.getHealthRatio();
					}
					catch
					{
					}
					GameArt.Bar(new Rect(num6, rect.y + 20f * p, num7 * 0.42f, 6f * p), num8, Color.Lerp(new Color(0.86f, 0.2f, 0.12f), GameArt.HealthGreen, num8));
					string text4 = Favorites.Doing(actor);
					GameArt.Label(new Rect(num6 + num7 * 0.46f, rect.y + 18.5f * p, num7 * 0.54f, 9f * p), text4, 3.9f, TextAnchor.MiddleLeft, text4.StartsWith("fighting") ? new Color(1f, 0.5f, 0.4f) : new Color(0.55f, 1f, 0.5f));
					if (!flag3)
					{
						if (flag4)
						{
							GameArt.Sliced(rect2, GameArt.ButtonBorder, Pings.ColorOf(PingKind.Look), 4f, 4f, 4f, 4f);
						}
						else if (flag5)
						{
							GameArt.Sliced(rect2, GameArt.ButtonBorder, new Color(1f, 1f, 1f, 0.7f), 4f, 4f, 4f, 4f);
						}
						PingIcon(rect2.center, Mathf.Round(num5 * 0.55f), PingKind.Look, GUI.color.a * (flag4 ? 1f : (flag5 ? 0.8f : 0.45f)));
					}
				}
				PadNav.Spot(rect2);
				if (!flag3 && GUI.Button(rect2, GUIContent.none, GUIStyle.none) && _listMouseIn)
				{
					_mod.PingFavorite(actor);
					Sounds.PlayUI("event:/SFX/Ui/Click");
				}
				PadNav.Spot(rect);
				if (!flag3 && GUI.Button(rect, GUIContent.none, GUIStyle.none) && _listMouseIn)
				{
					if (Possess.JumpTo(actor))
					{
						Sounds.PlayUI("event:/SFX/Ui/Click");
					}
					else
					{
						_mod.Toast(CardName(actor) + " can't be taken over right now.");
					}
				}
			}
			EndList(view, ref favs.Scroll, contentH);
		}

		private CardText CardTextOf(long id)
		{
			if (!_cardTexts.TryGetValue(id, out var value))
			{
				if (_cardTexts.Count > 400)
				{
					_cardTexts.Clear();
				}
				value = (_cardTexts[id] = new CardText());
			}
			return value;
		}

		private void DrawContacts(Rect inner, Actor host)
		{
			float p = GameArt.P;
			List<Met> list = PeopleMet.Sorted(host);
			float num = Mathf.Round(16f * p);
			float num2 = Mathf.Round(3f * p);
			float num3 = Mathf.Round((inner.width - 2f * num2) / 3f);
			for (int i = 0; i < MetSorts.Length; i++)
			{
				if (GameButton(new Rect(inner.x + (float)i * (num3 + num2), inner.y, num3, num), MetSorts[i], PeopleMet.Sort == i, enabled: true, null, 4.6f))
				{
					PeopleMet.Sort = i;
					PeopleMet.Scroll = Vector2.zero;
				}
			}
			float num4 = inner.y + num + 5f * p;
			Rect view = new Rect(inner.x, num4, inner.width - 9f * p, inner.yMax - num4);
			if (list.Count == 0)
			{
				GameArt.Sliced(new Rect(view.x, view.y, inner.width, view.height), GameArt.Inner, new Color(1f, 1f, 1f, 0.6f));
				GameArt.Label(new Rect(view.x + 6f * p, view.y + 5f * p, inner.width - 12f * p, view.height - 10f * p), "No one yet.\n\nPress E on someone to say hello. They're kept here with what they think of you.\n\nClick a card to put them on your compass. The star makes them a favourite.", 4.4f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
				return;
			}
			float num5 = Mathf.Round(30f * p);
			float contentH = Mathf.Max(view.height, (float)list.Count * (num5 + num2));
			float num6 = BeginList(view, ref PeopleMet.Scroll, contentH);
			Vector2 current_position = host.current_position;
			Conversation converse = _mod.Converse;
			Favorites favs = _mod.Favs;
			int num7 = 0;
			while (num7 < list.Count)
			{
				Met met = list[num7];
				Rect rect = new Rect(0f, num6 + (float)num7 * (num5 + num2), view.width, num5);
				Actor actor = Chronicle.Find(met.Id);
				bool flag = SafeAlive(actor);
				bool flag2 = flag && converse.Marked == actor;
				float num8 = Mathf.Round(15f * p);
				Rect rect2 = new Rect(rect.xMax - num8 - 4f * p, rect.y + Mathf.Round((num5 - num8) * 0.5f), num8, num8);
				bool flag3 = flag && SafeFavorite(actor);
				bool flag4 = _listMouseIn && flag && rect2.Contains(Event.current.mousePosition);
				bool flag5 = _listMouseIn && flag && !flag4 && rect.Contains(Event.current.mousePosition);
				float num9;
				float num10;
				CardText cardText;
				int num11;
				if (Event.current.type == EventType.Repaint)
				{
					GameArt.Sliced(rect, GameArt.Inner, (!flag) ? new Color(0.7f, 0.7f, 0.7f, 0.85f) : ((flag5 || flag2) ? Color.white : new Color(0.9f, 0.9f, 0.9f, 1f)));
					if (flag2)
					{
						GameArt.Sliced(rect, GameArt.ButtonBorder, new Color(1f, 0.55f, 0.75f, 1f), 4f, 4f, 4f, 4f);
					}
					else if (flag5)
					{
						GameArt.Sliced(rect, GameArt.ButtonBorder, new Color(1f, 1f, 1f, 0.7f), 4f, 4f, 4f, 4f);
					}
					Color c = new Color(0.5f, 0.45f, 0.4f);
					Color color = (flag ? Color.white : GameArt.Grey);
					try
					{
						if (flag && actor.kingdom != null)
						{
							c = Royal.ColorOf(actor.kingdom);
							if (actor.kingdom.isCiv())
							{
								color = actor.kingdom.getColor().getColorText();
							}
						}
					}
					catch
					{
					}
					GameArt.Flat(new Rect(rect.x + 3f * p, rect.y + 4f * p, 2f * p, rect.height - 8f * p), c);
					Sprite s = null;
					try
					{
						if (flag)
						{
							s = actor.getSpriteToRender();
						}
					}
					catch
					{
					}
					PortraitWell(new Rect(rect.x + 7f * p, rect.y + 3f * p, num5 - 6f * p, num5 - 6f * p), s, actor);
					num9 = rect.x + num5 + 4f * p;
					num10 = (flag ? rect2.x : rect.xMax) - num9 - 4f * p;
					string text = met.Name ?? "someone";
					if (text.Length > 22)
					{
						text = text.Substring(0, 21) + "…";
					}
					GameArt.Label(new Rect(num9, rect.y + 1.5f * p, num10, 9f * p), text, 5f, TextAnchor.MiddleLeft, color);
					cardText = CardTextOf(met.Id);
					float unscaledTime = Time.unscaledTime;
					if (cardText.Line == null || unscaledTime >= cardText.Until || cardText.Alive != flag || cardText.Host != host)
					{
						string text2 = null;
						try
						{
							if (flag)
							{
								text2 = Dialogue.RelationTo(actor, host);
							}
						}
						catch
						{
						}
						string text3 = (string.IsNullOrEmpty(met.Kind) ? "" : met.Kind.Replace('_', ' '));
						string text4 = (flag ? People.AgeLabel(actor) : null);
						cardText.Line = (text2 ?? text3) + ((text4 != null) ? (", " + text4) : "");
						cardText.Until = unscaledTime + 0.5f;
						cardText.Alive = flag;
						cardText.Host = host;
					}
					GameArt.Label(new Rect(num9, rect.y + 10f * p, num10, 8f * p), cardText.Line, 3.9f, TextAnchor.MiddleLeft, GameArt.Grey);
					Regard regard = null;
					try
					{
						if (flag)
						{
							regard = Chronicle.RegardOf(actor, host, create: false);
						}
					}
					catch
					{
					}
					if (flag)
					{
						num11 = (DrawRowMeters(new Rect(num9, rect.y + 18.5f * p, num10, 9f * p), actor, host) ? 1 : 0);
						if (num11 != 0)
						{
							goto IL_0784;
						}
					}
					else
					{
						num11 = 0;
					}
					if (regard != null)
					{
						OpinionWords(regard.Opinion, out var words, out var color2);
						GameArt.Label(new Rect(num9, rect.y + 18.5f * p, num10 * 0.6f, 9f * p), words, 3.9f, TextAnchor.MiddleLeft, color2);
					}
					else if (met.Talks == 0)
					{
						GameArt.Label(new Rect(num9, rect.y + 18.5f * p, num10 * 0.6f, 9f * p), "not talked yet", 3.9f, TextAnchor.MiddleLeft, GameArt.Grey);
					}
					goto IL_0784;
				}
				goto IL_08f3;
				IL_08f3:
				PadNav.Spot(rect2);
				if (flag && GUI.Button(rect2, GUIContent.none, GUIStyle.none) && _listMouseIn)
				{
					_mod.StarCreature(actor);
					favs.Update(0f, force: true);
				}
				PadNav.Spot(rect);
				if (GUI.Button(rect, GUIContent.none, GUIStyle.none) && _listMouseIn && flag)
				{
					Sounds.PlayUI("event:/SFX/Ui/Click");
					if (flag2)
					{
						converse.Marked = null;
						_mod.Toast(met.Name + " taken off your compass");
					}
					else
					{
						converse.Marked = actor;
						converse.MarkedName = met.Name;
						_mod.Toast(met.Name + " is on your compass");
					}
				}
				num7++;
				continue;
				IL_0784:
				if (num11 == 0)
				{
					GameArt.Label(new Rect(num9 + num10 * 0.6f, rect.y + 18.5f * p, num10 * 0.4f, 9f * p), flag ? cardText.AwayOf(Mathf.RoundToInt((actor.current_position - current_position).magnitude)) : "dead", 3.9f, TextAnchor.MiddleRight, flag ? GameArt.Grey : new Color(1f, 0.5f, 0.38f));
				}
				if (flag)
				{
					if (flag4)
					{
						GameArt.Sliced(rect2, GameArt.ButtonBorder, new Color(1f, 1f, 1f, 0.7f), 4f, 4f, 4f, 4f);
					}
					float num12 = Mathf.Round(num8 * 0.8f);
					GameArt.Icon(new Rect(Mathf.Round(rect2.center.x - num12 * 0.5f), Mathf.Round(rect2.center.y - num12 * 0.5f), num12, num12), GameArt.Star, flag3 ? Color.white : (flag4 ? new Color(1f, 1f, 1f, 0.8f) : new Color(0.55f, 0.55f, 0.55f, 0.6f)));
				}
				goto IL_08f3;
			}
			EndList(view, ref PeopleMet.Scroll, contentH);
		}

		private static void OpinionWords(int o, out string words, out Color color)
		{
			if (o >= 60)
			{
				words = "loves you";
				color = new Color(1f, 0.62f, 0.78f);
			}
			else if (o >= 20)
			{
				words = "likes you";
				color = new Color(0.55f, 1f, 0.5f);
			}
			else if (o > -20)
			{
				words = "doesn't mind you";
				color = new Color(0.85f, 0.85f, 0.8f);
			}
			else if (o > -60)
			{
				words = "dislikes you";
				color = new Color(1f, 0.7f, 0.4f);
			}
			else
			{
				words = "hates you";
				color = new Color(1f, 0.4f, 0.32f);
			}
		}

		private static bool SafeAlive(Actor a)
		{
			try
			{
				return a?.isAlive() ?? false;
			}
			catch
			{
				return false;
			}
		}

		private static bool SafeFavorite(Actor a)
		{
			try
			{
				return a?.isFavorite() ?? false;
			}
			catch
			{
				return false;
			}
		}

		private static string CardName(Actor a)
		{
			string text = Favorites.NameOf(a);
			if (string.IsNullOrEmpty(text))
			{
				text = ((a.asset != null) ? a.asset.id.Replace('_', ' ') : "creature");
			}
			if (text.Length <= 22)
			{
				return text;
			}
			return text.Substring(0, 21) + "…";
		}

		private string ShortName(Actor a)
		{
			string text = PeopleMet.Label(_mod.Host, a);
			if (string.IsNullOrEmpty(text))
			{
				text = ((a.asset != null) ? a.asset.id.Replace('_', ' ') : "creature");
			}
			if (text.Length <= 22)
			{
				return text;
			}
			return text.Substring(0, 21) + "…";
		}

		internal void DrawFps()
		{
			if (Event.current.type == EventType.Repaint && _mod.Settings.ShowFps)
			{
				if (_fpsFrame != Time.frameCount)
				{
					_fpsFrame = Time.frameCount;
					float num = Mathf.Max(0.0005f, Time.unscaledDeltaTime);
					_fpsSmooth = ((_fpsSmooth <= 0f) ? num : Mathf.Lerp(_fpsSmooth, num, 0.08f));
				}
				GameArt.Ensure();
				float p = GameArt.P;
				int num2 = Mathf.RoundToInt(1f / Mathf.Max(0.0005f, _fpsSmooth));
				int num3 = Mathf.RoundToInt(_fpsSmooth * 10000f);
				if (_fpsText == null || num2 != _fpsShown || num3 != _fpsTenths)
				{
					_fpsShown = num2;
					_fpsTenths = num3;
					_fpsText = string.Format(Lang.Template("{0} fps   {1} ms"), num2, ((float)num3 / 10f).ToString("0.0"));
				}
				string fpsText = _fpsText;
				Color color = ((num2 >= 50) ? new Color(0.6f, 1f, 0.55f) : ((num2 >= 30) ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 0.45f, 0.35f)));
				float f = GameArt.Measure(fpsText, 4.6f, translate: false).x + 8f * p;
				float num4 = Mathf.Round(10f * p);
				Rect r = new Rect(Mathf.Round(4f * p), Mathf.Round((float)Screen.height - num4 - 4f * p), Mathf.Round(f), num4);
				GameArt.TipPanel(r, 0.8f);
				GameArt.Label(r, fpsText, 4.6f, TextAnchor.MiddleCenter, color, wrap: false, translate: false);
			}
		}

		public void DrawFly(bool crosshair, string label, float health, Vector3 me, float yaw, float speed, float speedUntil, float hintUntil, bool cursorFree)
		{
			EnsureAssets();
			Skin.Ensure();
			float num = Screen.width;
			float num2 = Screen.height;
			float unscaledTime = Time.unscaledTime;
			if (crosshair)
			{
				float num3 = num * 0.5f;
				float num4 = num2 * 0.5f;
				float num5 = Mathf.Max(6f, num2 * 0.009f * GameArt.UserScale);
				float num6 = Mathf.Max(2f, num2 * 0.0022f * GameArt.UserScale);
				float num7 = num5 * 0.45f;
				Color color = new Color(0f, 0f, 0f, 0.35f);
				Color color2 = new Color(1f, 1f, 1f, 0.85f);
				Color[] array = new Color[2] { color, color2 };
				foreach (Color color3 in array)
				{
					float num8 = ((color3 == color) ? 1f : 0f);
					Fill(new Rect(num3 - num7 - num5 + num8, num4 - num6 * 0.5f + num8, num5, num6), color3);
					Fill(new Rect(num3 + num7 + num8, num4 - num6 * 0.5f + num8, num5, num6), color3);
					Fill(new Rect(num3 - num6 * 0.5f + num8, num4 - num7 - num5 + num8, num6, num5), color3);
					Fill(new Rect(num3 - num6 * 0.5f + num8, num4 + num7 + num8, num6, num5), color3);
				}
			}
			if (label != null)
			{
				float y = (crosshair ? (num2 * 0.5f + num2 * 0.045f) : (num2 * 0.86f));
				float num9 = Pill(num, y, label, Skin.Ink, 1f);
				if (health >= 0f)
				{
					float num10 = Mathf.Round(num2 * 0.09f);
					float height = Mathf.Max(3f, Mathf.Round(num2 * 0.005f));
					Rect rect = new Rect(Mathf.Round(num * 0.5f - num10 * 0.5f), Mathf.Round(num9 + 2f * GameArt.P), num10, height);
					Fill(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), new Color(0f, 0f, 0f, 0.6f));
					Fill(new Rect(rect.x, rect.y, Mathf.Round(rect.width * Mathf.Clamp01(health)), rect.height), Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(0.4f, 0.9f, 0.35f), health));
				}
			}
			if (unscaledTime < speedUntil)
			{
				Pill(num, num2 * 0.2f, "Speed x" + speed.ToString((speed < 1f) ? "0.00" : "0.0"), Color.white, Mathf.Clamp01((speedUntil - unscaledTime) / 0.4f));
			}
			if (unscaledTime < hintUntil)
			{
				Pill(num, num2 * 0.8f, "WASD fly   Space / Ctrl up, down   Shift faster   Wheel speed   Click power   Middle click take over   Tab cursor   F land", Skin.Ink, Mathf.Clamp01((hintUntil - unscaledTime) / 0.6f));
			}
			else if (cursorFree)
			{
				Pill(num, num2 * 0.93f, "Cursor free   Tab to fly", Skin.Ink, 0.85f);
			}
			DrawFlyMinimap(num, num2, me, yaw);
		}

		private void DrawFlyMinimap(float sw, float sh, Vector3 me3, float yaw)
		{
			FlyMinimapDrawn = false;
			Minimap minimap = _mod.Minimap;
			if (!_mod.Settings.Minimap || minimap.Land == null)
			{
				return;
			}
			float p = GameArt.P;
			float num = Mathf.Round(78f * p);
			Rect position = new Rect(Mathf.Round(sw - num - 6f * p), Mathf.Round(6f * p), num, num);
			Vector2 center = position.center;
			Vector2 vector = me3;
			float num2 = 36f * p;
			float flyMinimapReach = _mod.FlyMinimapReach;
			float num3 = num2 / flyMinimapReach;
			Vector2 vector2 = new Vector2(Mathf.Cos(yaw), Mathf.Sin(yaw));
			Vector2 vector3 = new Vector2(Mathf.Sin(yaw), 0f - Mathf.Cos(yaw));
			Fan(minimap.Land, center, num2 + 0.8f * p, vector, vector2, vector3, flyMinimapReach * (num2 + 0.8f * p) / num2, minimap.TexX, minimap.TexY, minimap.TexSize, 1f);
			float num4 = Mathf.Max(2f, Mathf.Round(1.6f * p));
			foreach (KeyValuePair<Vector2, Color> flyDot in _mod.FlyDots)
			{
				Vector2 lhs = flyDot.Key - vector;
				Vector2 vector4 = new Vector2(center.x + Vector2.Dot(lhs, vector3) * num3, center.y - Vector2.Dot(lhs, vector2) * num3);
				if (!((vector4 - center).sqrMagnitude > (num2 - num4) * (num2 - num4)))
				{
					GameArt.Flat(new Rect(Mathf.Round(vector4.x - num4 * 0.5f) - 1f, Mathf.Round(vector4.y - num4 * 0.5f) - 1f, num4 + 2f, num4 + 2f), new Color(0f, 0f, 0f, 0.7f));
					GameArt.Flat(new Rect(Mathf.Round(vector4.x - num4 * 0.5f), Mathf.Round(vector4.y - num4 * 0.5f), num4, num4), flyDot.Value);
				}
			}
			if (_mmRing == null)
			{
				_mmRing = MakeRing();
			}
			GUI.DrawTexture(position, _mmRing);
			Color color = GUI.color;
			Vector2 vector5 = new Vector2(Vector2.Dot(Vector2.up, vector3), 0f - Vector2.Dot(Vector2.up, vector2));
			Vector2 vector6 = center + vector5 * (num2 + 1.5f * p);
			float num5 = Mathf.Round(9f * p);
			GUI.color = new Color(0.08f, 0.08f, 0.09f, 0.95f);
			GUI.DrawTexture(new Rect(vector6.x - num5 * 0.5f, vector6.y - num5 * 0.5f, num5, num5), Skin.DiscTex);
			GUI.color = color;
			GameArt.Label(new Rect(vector6.x - num5, vector6.y - num5 * 0.5f - 0.5f * p, num5 * 2f, num5), "N", 4.6f, TextAnchor.MiddleCenter, new Color(1f, 0.42f, 0.3f, 1f));
			PixelArrow(center, Mathf.Max(1f, Mathf.Round(p)), 0f, 1f);
			FlyMinimapDrawn = true;
		}

		private Rect GameWindow(Rect win, string title, string key, out bool close, bool dim = true)
		{
			EnsureAssets();
			GameArt.Ensure();
			GameArt.EnsureWindowArt();
			float p = GameArt.P;
			close = false;
			if (dim && Event.current.type == EventType.Repaint)
			{
				Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.02f, 0.03f, 0.06f, 0.45f));
			}
			GameArt.Window(win, Color.white);
			GameArt.Label(new Rect(win.x, win.y + 5f * p, win.width, 14f * p), title, 7.5f, TextAnchor.MiddleCenter, GameArt.Orange);
			if (!string.IsNullOrEmpty(key))
			{
				GameArt.Label(new Rect(win.x, win.y + 16f * p, win.width - 46f * p, 10f * p), key + " to close", 4.2f, TextAnchor.MiddleRight, GameArt.Grey);
			}
			Rect r = new Rect(Mathf.Round(win.xMax - 33f * p), Mathf.Round(win.y + 2f * p), Mathf.Round(34f * p), Mathf.Round(36f * p));
			bool num = r.Contains(Event.current.mousePosition);
			GameArt.Sliced(r, GameArt.CloseTab, Color.white);
			float num2 = Mathf.Round((num ? 25f : 23f) * p);
			Rect r2 = new Rect(Mathf.Round(r.center.x - num2 * 0.5f - 1f * p), Mathf.Round(r.center.y - num2 * 0.5f + 1f * p), num2, num2);
			Sprite sprite = GameArt.IconNamed("iconClose");
			if (sprite != null)
			{
				GameArt.Icon(new Rect(r2.x + p, r2.y + p, num2, num2), sprite, new Color(0f, 0f, 0f, 0.4f));
				GameArt.Icon(r2, sprite, Color.white);
			}
			else
			{
				GameArt.Label(r2, "X", 9f, TextAnchor.MiddleCenter, new Color(0.95f, 0.25f, 0.2f));
			}
			if (Button(r, GUIContent.none, GUIStyle.none))
			{
				close = true;
			}
			return new Rect(win.x + 10f * p, win.y + 30f * p, win.width - 20f * p, win.height - 40f * p);
		}

		private static Rect WindowRect(float wantW, float wantH)
		{
			float p = GameArt.P;
			float num = Screen.width;
			float num2 = Screen.height;
			float num3 = Mathf.Round(Mathf.Min(wantW * p, num - 30f * p));
			float num4 = Mathf.Round(Mathf.Min(wantH * p, num2 - 24f * p));
			return new Rect(Mathf.Round((num - num3) * 0.5f), Mathf.Round((num2 - num4) * 0.5f + 4f * p), num3, num4);
		}

		private static void SectionTitle(Rect r, string text, Sprite icon = null)
		{
			float p = GameArt.P;
			GameArt.Sliced(r, GameArt.SectionStrip, Color.white);
			GameArt.Label(new Rect(r.x + 18f * p, r.y - 0.5f * p, r.width - 36f * p, r.height - 4f * p), text, 5.8f, TextAnchor.MiddleCenter, GameArt.Orange);
			if (!(icon == null))
			{
				float num = Mathf.Round(Mathf.Min(13f * p, r.height - 6f * p));
				GameArt.Icon(new Rect(r.x + 7f * p, r.y + 2f * p, num, num), icon, Color.white);
				GameArt.Icon(new Rect(r.xMax - 7f * p - num, r.y + 2f * p, num, num), icon, Color.white);
			}
		}

		private bool GameButton(Rect r, string label, bool red, bool enabled = true, Sprite icon = null, float size = 5f, bool live = true)
		{
			Vector2 mousePosition = Event.current.mousePosition;
			bool flag = enabled && live && r.Contains(mousePosition);
			if (Event.current.type == EventType.Repaint)
			{
				Color color = GUI.color;
				if (!enabled)
				{
					GUI.color = new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, color.a * 0.85f);
				}
				GameArt.FlatButton(r, red && enabled, label, size, flag ? 1f : 0.88f, icon);
				GUI.color = color;
			}
			if (enabled && live)
			{
				PadNav.Spot(r);
			}
			if (!GUI.Button(r, GUIContent.none, GUIStyle.none) || !enabled || !live)
			{
				return false;
			}
			Sounds.PlayUI("event:/SFX/Ui/Click");
			return true;
		}

		private bool PowerButton(Rect r, Sprite icon, string label, float wait, float cooldown, bool enabled = true, bool live = true)
		{
			float p = GameArt.P;
			Vector2 mousePosition = Event.current.mousePosition;
			bool flag = enabled && wait <= 0f;
			bool flag2 = flag && live && r.Contains(mousePosition);
			if (Event.current.type == EventType.Repaint)
			{
				Rect r2 = (flag2 ? new Rect(r.x - p, r.y - p, r.width + 2f * p, r.height + 2f * p) : r);
				GameArt.StoneButton(r2, icon, 1f, gems: true, flag ? Color.white : new Color(0.55f, 0.55f, 0.58f, 1f));
				if (wait > 0f)
				{
					Rect r3 = new Rect(r2.x + r2.width * 0.12f, r2.y + r2.height * 0.12f, r2.width * 0.76f, r2.height * 0.76f);
					float num = Mathf.Clamp01(wait / Mathf.Max(0.1f, cooldown));
					GameArt.Flat(new Rect(r3.x, r3.y, r3.width, Mathf.Round(r3.height * num)), new Color(0f, 0f, 0f, 0.55f));
					GameArt.Label(r3, Mathf.CeilToInt(wait).ToString(), 6.5f, TextAnchor.MiddleCenter, Color.white);
				}
				if (flag2)
				{
					GameArt.Sliced(r2, GameArt.ButtonBorder, new Color(1f, 0.85f, 0.35f, 0.9f), 4f, 4f, 4f, 4f, r2.width / 32f);
				}
				if (!string.IsNullOrEmpty(label))
				{
					GameArt.Label(new Rect(r.x - 10f * p, r.yMax + 0.5f * p, r.width + 20f * p, 9f * p), label, 3.9f, TextAnchor.UpperCenter, flag ? Color.white : GameArt.Grey, wrap: true);
				}
			}
			if (flag && live)
			{
				PadNav.Spot(r);
			}
			if (!GUI.Button(r, GUIContent.none, GUIStyle.none) || !flag || !live)
			{
				return false;
			}
			Sounds.PlayUI("event:/SFX/Ui/Click");
			return true;
		}

		private static void KingdomCard(Rect card, Kingdom k, string line, bool picked, Color lineColor)
		{
			float p = GameArt.P;
			GameArt.Sliced(card, GameArt.Inner, picked ? Color.white : new Color(0.9f, 0.9f, 0.9f, 1f));
			if (picked)
			{
				GameArt.Sliced(card, GameArt.ButtonBorder, new Color(1f, 0.82f, 0.3f, 1f), 4f, 4f, 4f, 4f);
			}
			bool civ = false;
			try
			{
				civ = k?.isCiv() ?? false;
			}
			catch
			{
			}
			float num = Mathf.Min(card.height - 6f * p, 26f * p);
			DrawBanner(new Rect(card.x + 4f * p, card.y + (card.height - num) * 0.5f, num * 0.8f, num), k, civ, 1f);
			Color color = Color.white;
			try
			{
				color = WarMap.Readable(k.getColor().getColorText());
			}
			catch
			{
			}
			float x = card.x + num * 0.8f + 8f * p;
			GameArt.Label(new Rect(x, card.y + 3f * p, card.width * 0.5f, 10f * p), Royal.NameOf(k), 5.8f, TextAnchor.MiddleLeft, color);
			GameArt.Label(new Rect(x, card.y + 13.5f * p, card.width * 0.6f, 8f * p), line, 4.2f, TextAnchor.MiddleLeft, lineColor);
		}

		private static void StateTag(float x, float y, string text, Color color)
		{
			float p = GameArt.P;
			float f = GameArt.Measure(text, 4.4f).x + 8f * p;
			Rect r = new Rect(Mathf.Round(x), Mathf.Round(y), Mathf.Round(f), Mathf.Round(9f * p));
			GameArt.TipPanel(r);
			GameArt.Label(new Rect(r.x, r.y - 0.5f * p, r.width, r.height), text, 4.4f, TextAnchor.MiddleCenter, color);
		}

		private static void PortraitWell(Rect r, Sprite s, float alpha = 1f)
		{
			float p = GameArt.P;
			GameArt.Sliced(r, GameArt.Button, new Color(1f, 1f, 1f, alpha), 6f, 6f, 6f, 6f, Mathf.Min(p, r.width / 24f));
			if (!(s == null) && Event.current.type == EventType.Repaint)
			{
				Color color = GUI.color;
				GUI.color = new Color(color.r, color.g, color.b, color.a * alpha);
				float num = Mathf.Max(2f * p, r.width * 0.12f);
				DrawSprite(new Rect(r.x + num, r.y + num, r.width - num * 2f, r.height - num * 2f), s);
				GUI.color = color;
			}
		}

		private string GameTextField(Rect r, string text, int max, string controlName, float size = 6.5f)
		{
			float p = GameArt.P;
			GameArt.Sliced(r, GameArt.DarkField, Color.white, 3f, 3f, 3f, 3f);
			if (_gameField == null || !Mathf.Approximately(_gameFieldFor, p))
			{
				_gameFieldFor = p;
				_gameField = new GUIStyle(GUI.skin.textField)
				{
					font = (GameArt.Font ?? Skin.Serif),
					fontSize = Mathf.Max(10, Mathf.RoundToInt(size * p)),
					alignment = TextAnchor.MiddleCenter,
					padding = new RectOffset(4, 4, 2, 2)
				};
				GUIStyleState[] array = new GUIStyleState[6] { _gameField.normal, _gameField.hover, _gameField.focused, _gameField.active, _gameField.onNormal, _gameField.onFocused };
				foreach (GUIStyleState obj in array)
				{
					obj.background = null;
					obj.textColor = Color.white;
				}
			}
			if (controlName != null)
			{
				GUI.SetNextControlName(controlName);
				if (GUIUtility.keyboardControl != 0 && GUI.GetNameOfFocusedControl() == controlName && controlName != "fp_baby_name" && controlName != "ledger_search" && !controlName.StartsWith("mp_", StringComparison.Ordinal))
				{
					FieldFocusFrame = Time.frameCount;
				}
			}
			Font wasFont;
			FontStyle wasStyle;
			bool num = Lang.Fit(_gameField, text, out wasFont, out wasStyle);
			string result = GUI.TextField(r, text ?? "", max, _gameField);
			if (num)
			{
				Lang.Restore(_gameField, wasFont, wasStyle);
			}
			return result;
		}

		private float BeginList(Rect view, ref Vector2 scroll, float contentH)
		{
			float p = GameArt.P;
			Event current = Event.current;
			float max = Mathf.Max(0f, contentH - view.height);
			if (current.type == EventType.ScrollWheel && view.Contains(current.mousePosition))
			{
				scroll.y += current.delta.y * 14f * p;
				current.Use();
			}
			scroll.y = Mathf.Clamp(scroll.y, 0f, max);
			GUI.BeginGroup(view);
			_listMouseIn = new Rect(0f, 0f, view.width, view.height).Contains(Event.current.mousePosition);
			return 0f - Mathf.Round(scroll.y);
		}

		private void EndList(Rect view, ref Vector2 scroll, float contentH)
		{
			GUI.EndGroup();
			_listMouseIn = false;
			float num = Mathf.Max(0f, contentH - view.height);
			if (num > 0.5f)
			{
				scroll.y = GameScrollbar(new Rect(view.xMax + 2f * GameArt.P, view.y + 2f * GameArt.P, 6f * GameArt.P, view.height - 4f * GameArt.P), scroll.y, num, view.height / contentH);
			}
		}

		private float GameScrollbar(Rect track, float scroll, float max, float shown)
		{
			float p = GameArt.P;
			float num = Mathf.Max(18f * p, track.height * Mathf.Clamp01(shown));
			float num2 = ((max > 0f) ? (scroll / max) : 0f);
			Rect r = new Rect(track.x, Mathf.Round(track.y + (track.height - num) * num2), track.width, Mathf.Round(num));
			Event current = Event.current;
			if (current.type == EventType.Repaint)
			{
				if (GameArt.ScrollTrack != null)
				{
					GameArt.Sliced(track, GameArt.ScrollTrack, Color.white, -1f, -1f, -1f, -1f, track.width / 17f);
				}
				else
				{
					GameArt.Sliced(track, GameArt.DarkField, Color.white, 3f, 3f, 3f, 3f);
				}
				GameArt.Sliced(r, GameArt.RedButton, Color.white, 5f, 5f, 5f, 6f, Mathf.Min(p, track.width / 10f));
			}
			int controlID = GUIUtility.GetControlID(FocusType.Passive);
			switch (current.GetTypeForControl(controlID))
			{
			case EventType.MouseDown:
				if (new Rect(track.x - 3f * p, track.y, track.width + 6f * p, track.height).Contains(current.mousePosition))
				{
					GUIUtility.hotControl = controlID;
					scroll = Mathf.Clamp01((current.mousePosition.y - track.y - num * 0.5f) / Mathf.Max(1f, track.height - num)) * max;
					current.Use();
				}
				break;
			case EventType.MouseDrag:
				if (GUIUtility.hotControl == controlID)
				{
					scroll = Mathf.Clamp01((current.mousePosition.y - track.y - num * 0.5f) / Mathf.Max(1f, track.height - num)) * max;
					current.Use();
				}
				break;
			case EventType.MouseUp:
				if (GUIUtility.hotControl == controlID)
				{
					GUIUtility.hotControl = 0;
					current.Use();
				}
				break;
			}
			return scroll;
		}

		private static Sprite DeedIcon(Royal.Deed d)
		{
			switch (d)
			{
			case Royal.Deed.Bandits:
				return GameArt.IconNamed("iconBandit");
			case Royal.Deed.Undead:
				return GameArt.IconNamed("iconSkeleton");
			case Royal.Deed.Demons:
				return GameArt.IconNamed("iconDemon");
			case Royal.Deed.Zombies:
				return GameArt.IconNamed("iconZombie");
			case Royal.Deed.Plague:
				return GameArt.IconNamed("iconPlague");
			case Royal.Deed.Madness:
				return GameArt.IconNamed("iconMadness");
			case Royal.Deed.Meteor:
				return GameArt.IconNamed("iconMeteorite");
			case Royal.Deed.Tornado:
				return GameArt.IconNamed("iconTornado");
			case Royal.Deed.Earthquake:
				return GameArt.IconNamed("iconEarthquake");
			case Royal.Deed.FireCloud:
				return GameArt.IconNamed("iconCloudFire");
			case Royal.Deed.AcidCloud:
				return GameArt.IconNamed("iconCloudAcid");
			case Royal.Deed.LavaCloud:
				return GameArt.IconNamed("iconCloudLava");
			case Royal.Deed.Rebellion:
				return GameArt.IconNamed("iconRebellion");
			case Royal.Deed.MeteorStorm:
				return GameArt.IconNamed("iconMeteorite");
			case Royal.Deed.LightningStorm:
				return StormIcon(Royal.Storm.Lightning);
			case Royal.Deed.BombStorm:
				return GameArt.IconNamed("iconBomb");
			case Royal.Deed.Napalm:
				return GameArt.IconNamed("iconNapalmBomb");
			case Royal.Deed.CrabBombs:
				return GameArt.IconNamed("iconCrabParachute") ?? GameArt.IconNamed("iconCrab");
			case Royal.Deed.Nuke:
				return GameArt.IconNamed("iconAtomicBomb");
			case Royal.Deed.CzarBomba:
				return GameArt.IconNamed("iconCzarBomba") ?? GameArt.IconNamed("iconAtomicBomb");
			default:
				return GameArt.Kills;
			}
		}

		private static Sprite StormIcon(Royal.Storm s)
		{
			switch (s)
			{
			case Royal.Storm.Meteors:
				return GameArt.IconNamed("iconMeteorite");
			case Royal.Storm.Lightning:
				return GameArt.IconNamed("iconCloudLightning") ?? GameArt.IconNamed("iconLightning");
			default:
				return GameArt.IconNamed("iconDivineLight") ?? GameArt.IconNamed("iconGodFinger");
			}
		}

		public void DrawGodToast(string text, float until)
		{
			EnsureAssets();
			Pill(Screen.width, (float)Screen.height * 0.12f, text, Skin.Ink, Mathf.Clamp01((until - Time.unscaledTime) / 0.4f));
		}

		public void DrawGodLabel(string text, float alpha)
		{
			EnsureAssets();
			Pill(Screen.width, (float)Screen.height * 0.075f, text, Skin.Ink, alpha);
		}

		internal bool DrawHelpWindow()
		{
			EnsureAssets();
			GameArt.Ensure();
			float p = GameArt.P;
			bool close;
			Rect rect = GameWindow(HelpRectForTest = WindowRect(560f, 380f), "Help", "F1", out close);
			if (close)
			{
				return true;
			}
			Event current = Event.current;
			float num = Mathf.Round(15f * p);
			float num2 = Mathf.Round(70f * p);
			float num3 = Mathf.Round(4f * p);
			for (int i = 0; i < Help.Pages.Length; i++)
			{
				Rect r = new Rect(rect.x + (float)i * (num2 + num3), rect.y, num2, num);
				if (GameButton(r, Help.Pages[i], Help.Page == i) && Help.Page != i)
				{
					Help.Page = i;
					Help.Scroll = Vector2.zero;
				}
			}
			string text = ((Help.Page == 0) ? "Hold E or Q for a wheel. Move the mouse to choose, let go to take it" : "Esc closes this window");
			GameArt.Label(new Rect(rect.x + 2f * (num2 + num3) + 6f * p, rect.y, rect.xMax - (rect.x + 2f * (num2 + num3) + 6f * p), num), text, 4f, TextAnchor.MiddleRight, GameArt.Grey);
			float num4 = rect.y + num + 5f * p;
			Help.Part[] array = Help.PartsOf(Help.Page);
			int num5 = Mathf.Clamp(Help.Section[Help.Page], 0, array.Length - 1);
			float num6 = Mathf.Round(118f * p);
			float num7 = Mathf.Round(15f * p);
			float num8 = Mathf.Round(2f * p);
			for (int j = 0; j < array.Length; j++)
			{
				Rect r2 = new Rect(rect.x, num4 + (float)j * (num7 + num8), num6, num7);
				if (r2.yMax > rect.yMax)
				{
					break;
				}
				Sprite icon = GameArt.IconNamed(array[j].Icon);
				if (GameButton(r2, array[j].Name, num5 == j, enabled: true, icon, 4.6f) && num5 != j)
				{
					num5 = (Help.Section[Help.Page] = j);
					Help.Scroll = Vector2.zero;
				}
			}
			Rect view = new Rect(rect.x + num6 + 8f * p, num4, rect.xMax - (rect.x + num6 + 8f * p) - 9f * p, rect.yMax - num4);
			GameArt.Sliced(new Rect(view.x - 2f * p, view.y - 2f * p, view.width + 13f * p, view.height + 4f * p), GameArt.Inner, new Color(1f, 1f, 1f, 0.7f));
			Help.Part part = array[num5];
			float num9 = Mathf.Round(13f * p);
			float num10 = Mathf.Round(4f * p);
			float num11 = Mathf.Round(92f * p);
			float size = 4.6f;
			float contentH;
			if (part.Rows != null)
			{
				contentH = num10 * 2f + (float)part.Rows.Length * num9;
			}
			else
			{
				contentH = num10;
				string[] lines = part.Lines;
				foreach (string text2 in lines)
				{
					contentH += LineHeight(text2, view.width - 2f * num10 - 8f * p, size) + 4f * p;
				}
				contentH += num10;
			}
			float num12 = BeginList(view, ref Help.Scroll, contentH);
			if (current.type == EventType.Repaint)
			{
				float num13 = num12 + num10;
				if (part.Rows != null)
				{
					for (int l = 0; l < part.Rows.Length; l++)
					{
						string[] array2 = part.Rows[l];
						if (num13 + num9 >= 0f && num13 <= view.height)
						{
							if (l % 2 == 1)
							{
								Fill(new Rect(0f, num13, view.width, num9), new Color(0f, 0f, 0f, 0.12f));
							}
							KeyCaps(new Rect(num10, num13, num11, num9), array2[0]);
							GameArt.Label(new Rect(num10 + num11 + 6f * p, num13, view.width - num11 - 2f * num10 - 6f * p, num9), array2[1], size, TextAnchor.MiddleLeft, Color.white);
						}
						num13 += num9;
					}
				}
				else
				{
					string[] lines = part.Lines;
					foreach (string text3 in lines)
					{
						float num14 = LineHeight(text3, view.width - 2f * num10 - 8f * p, size);
						GameArt.Label(new Rect(num10, num13 + 1.5f * p, 4f * p, 6f * p), "•", size, TextAnchor.MiddleLeft, GameArt.Orange);
						GameArt.Label(new Rect(num10 + 8f * p, num13, view.width - 2f * num10 - 8f * p, num14), text3, size, TextAnchor.UpperLeft, Color.white, wrap: true);
						num13 += num14 + 4f * p;
					}
				}
			}
			EndList(view, ref Help.Scroll, contentH);
			return false;
		}

		private static float LineHeight(string text, float width, float size)
		{
			return Mathf.Ceil(GameArt.Style(size, TextAnchor.UpperLeft, wrap: true).CalcHeight(new GUIContent(Lang.T(text)), Mathf.Max(10f, width)));
		}

		private static void KeyCaps(Rect r, string keys)
		{
			float p = GameArt.P;
			float num = r.x;
			string[] array = keys.Split(new string[1] { "  " }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string text in array)
			{
				float num2 = Mathf.Max(Mathf.Round(9.2f * p), GameArt.Measure(text, 4.2f).x + 4f * p);
				if (num + num2 > r.xMax + 2f * p)
				{
					GameArt.Label(new Rect(num, r.y, r.xMax - num, r.height), "…", 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
					break;
				}
				num += GameArt.KeyCap(num, r.center.y, text, 4.2f) + 3f * p;
			}
		}

		private void DrawHull(Rect bar, float alpha)
		{
			Actor ship = _mod.HijackedShip;
			if (ship == null || alpha <= 0.01f)
			{
				_hullOf = null;
				return;
			}
			if (ship != _hullOf)
			{
				_hullOf = ship;
				_hullMeter = default(Meter);
			}
			float p = GameArt.P;
			float num = Ratio(() => ship.getHealthRatio());
			int num2 = 0;
			int num3 = 0;
			try
			{
				num2 = Mathf.CeilToInt(ship.getHealth());
				num3 = Mathf.CeilToInt(ship.getMaxHealth());
			}
			catch
			{
			}
			Step(ref _hullMeter, num);
			float num4 = Mathf.Round(bar.width * 0.46f);
			float num5 = 8f * p;
			Rect r = new Rect(Mathf.Round(bar.center.x - num4 * 0.5f - 6f * p), Mathf.Round(bar.y - num5 - 9f * p), num4 + 12f * p, num5 + 7f * p);
			GameArt.BarSlab(r, alpha);
			Rect r2 = new Rect(r.x + 8f * p, Mathf.Round(r.y + 3.5f * p), num4 - 2f * p, num5);
			Sprite sprite = GameArt.IconNamed("iconBoat");
			Rect part = ((sprite != null) ? new Rect(0f, 0f, sprite.rect.width, sprite.rect.height) : default(Rect));
			Color color = ((num < 0.3f) ? Color.Lerp(new Color(0.85f, 0.25f, 0.15f), new Color(1f, 0.6f, 0.2f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f)) : new Color(0.62f, 0.45f, 0.26f));
			MeterBar(r2, sprite, part, _hullMeter, color, (num3 > 0) ? (num2 + "/" + num3) : null, alpha);
		}

		internal void DrawHomeCard()
		{
			Building house = HomeCard.House;
			Actor host = _mod.Host;
			if (house == null || host == null || !house.isAlive())
			{
				HomeCard.Close();
				return;
			}
			_growNeeds.Clear();
			string next;
			bool flag = HomeGrowth.Next(house, out next, _growNeeds);
			float num = (flag ? (14f + 11f * (float)_growNeeds.Count) : 16f);
			bool close;
			Rect rect = GameWindow(WindowRect(360f, 236f + num), HomeGrowth.IsTent(house) ? "Your tent" : "Your house", "E", out close);
			if (close)
			{
				HomeCard.Close();
				return;
			}
			float p = GameArt.P;
			float y = rect.y;
			bool flag2 = HomeCard.Yours(house, host);
			bool flag3 = HomeLock.Locked(house);
			Rect r = new Rect(rect.x, y, rect.width, Mathf.Round(30f * p));
			GameArt.Sliced(r, GameArt.Inner, new Color(0.92f, 0.92f, 0.92f, 1f));
			string text = ((!flag2) ? "Not yours to lock" : (flag3 ? "The door is locked" : "The door is unlocked"));
			string text2 = ((!flag2) ? "Only your own home locks" : (flag3 ? "Only your family comes in" : "Anyone the town sends may come in"));
			GameArt.Label(new Rect(r.x + 6f * p, r.y + 3f * p, r.width * 0.55f, 11f * p), text, 5f, TextAnchor.MiddleLeft, Color.white);
			GameArt.Label(new Rect(r.x + 6f * p, r.y + 15f * p, r.width * 0.55f, 10f * p), text2, 3.9f, TextAnchor.MiddleLeft, GameArt.Grey);
			Rect r2 = new Rect(r.xMax - Mathf.Round(r.width * 0.38f) - 4f * p, r.y + 5f * p, Mathf.Round(r.width * 0.38f), r.height - 10f * p);
			if (GameButton(r2, flag3 ? "Unlock" : "Lock", !flag3, flag2, null, 4.6f))
			{
				HomeLock.SetLocked(house, !flag3);
				_mod.Toast(flag3 ? "Door unlocked" : "Door locked");
				Log.Info("home: door " + (flag3 ? "unlocked" : "locked"));
			}
			y = r.yMax + 6f * p;
			bool flag4 = Event.current == null || Event.current.type == EventType.Repaint;
			string text3 = "";
			if (flag4)
			{
				HouseOccupancy.Scan(latest: false);
				List<string> list = new List<string>();
				foreach (Actor item in HouseOccupancy.Of(house).Inside)
				{
					if (item != null && item != host && item.isAlive())
					{
						list.Add(NameOf(item));
					}
				}
				text3 = ((list.Count == 0) ? "Nobody" : string.Join(", ", list));
			}
			SectionTitle(new Rect(rect.x, y, rect.width, Mathf.Round(18f * p)), "Inside now");
			y += 20f * p;
			GameArt.Label(new Rect(rect.x + 4f * p, y, rect.width - 8f * p, 20f * p), text3, 4.3f, TextAnchor.UpperLeft, Color.white, wrap: true);
			y += 22f * p;
			SectionTitle(new Rect(rect.x, y, rect.width, Mathf.Round(18f * p)), "Let in when locked");
			y += 20f * p;
			List<string> list2 = new List<string>();
			if (flag4)
			{
				foreach (Actor item2 in HomeCard.Family)
				{
					if (item2 != null && item2.isAlive())
					{
						list2.Add(NameOf(item2));
					}
				}
			}
			string text4 = ((!flag4) ? "" : ((list2.Count == 0) ? "Your family (none living)" : ("Your family: " + string.Join(", ", (list2.Count > 8) ? list2.GetRange(0, 8) : list2) + ((list2.Count > 8) ? $" and {list2.Count - 8} more" : ""))));
			GameArt.Label(new Rect(rect.x + 4f * p, y, rect.width - 8f * p, 34f * p), text4, 4.3f, TextAnchor.UpperLeft, Color.white, wrap: true);
			y += 36f * p;
			SectionTitle(new Rect(rect.x, y, rect.width, Mathf.Round(18f * p)), flag ? (HomeGrowth.SizeName(house) + " to " + next.ToLowerInvariant()) : HomeGrowth.SizeName(house));
			y += 20f * p;
			if (!flag)
			{
				GameArt.Label(new Rect(rect.x + 4f * p, y, rect.width - 8f * p, 11f * p), "As big as a home gets", 4.3f, TextAnchor.UpperLeft, GameArt.Grey);
				return;
			}
			foreach (KeyValuePair<string, bool> growNeed in _growNeeds)
			{
				GameArt.Label(new Rect(rect.x + 4f * p, y, rect.width - 8f * p, 11f * p), (growNeed.Value ? "Has " : "Needs ") + LowerFirst(growNeed.Key), 4.1f, TextAnchor.UpperLeft, growNeed.Value ? Color.white : new Color(1f, 0.62f, 0.45f));
				y += 11f * p;
			}
			y += 12f * p;
			Rect r3 = new Rect(rect.x, y, Mathf.Round(rect.width * 0.42f), Mathf.Round(15f * p));
			bool flag5 = flag2;
			foreach (KeyValuePair<string, bool> growNeed2 in _growNeeds)
			{
				if (!growNeed2.Value)
				{
					flag5 = false;
				}
			}
			if (GameButton(r3, "Grow to " + next.ToLowerInvariant(), red: false, flag5, null, 4.2f))
			{
				string text5 = HomeGrowth.GrowNow(house);
				_mod.Toast(text5 ?? ("Your home is now a " + HomeGrowth.SizeName(house).ToLowerInvariant()));
			}
			GameArt.Label(new Rect(r3.xMax + 6f * p, y, rect.xMax - r3.xMax - 6f * p, r3.height), flag5 ? "Paid from the town's stores" : "Or the town grows it in time", 3.9f, TextAnchor.MiddleLeft, GameArt.Grey);
		}

		private static string LowerFirst(string s)
		{
			if (!string.IsNullOrEmpty(s) && !s.StartsWith("Town level", StringComparison.Ordinal))
			{
				return char.ToLowerInvariant(s[0]) + s.Substring(1);
			}
			return s?.Replace("Town level", "town level");
		}

		private static string NameOf(Actor a)
		{
			try
			{
				string name = a.getName();
				return string.IsNullOrEmpty(name) ? "someone" : name;
			}
			catch
			{
				return "someone";
			}
		}

		internal static bool HomeAt(Actor host, out Vector2 at)
		{
			at = default(Vector2);
			Building building = Workbench.HomeOf(host);
			if (building == null)
			{
				return false;
			}
			try
			{
				WorldTile current_tile = building.current_tile;
				if (current_tile == null)
				{
					return false;
				}
				at = new Vector2((float)current_tile.x + 0.5f, (float)current_tile.y + 0.5f);
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static void HomeMark(Vector2 c, float size, float alpha)
		{
			if (!_homeIconTried)
			{
				_homeIconTried = true;
				_homeIcon = GameArt.IconNamed("iconHouseTier0") ?? GameArt.IconNamed("iconBuildings") ?? GameArt.IconNamed("iconCity");
			}
			Color color = GUI.color;
			float num = Mathf.Round(size * 1.3f);
			float num2 = Mathf.Round(size * 1.5f);
			GUI.color = new Color(0f, 0f, 0f, 0.55f * alpha);
			GUI.DrawTexture(new Rect(c.x - num2 * 0.5f + 1f, c.y - num2 * 0.5f + 1f, num2, num2), Skin.DiscTex);
			GUI.color = new Color(HomeGreen.r, HomeGreen.g, HomeGreen.b, alpha);
			GUI.DrawTexture(new Rect(c.x - num2 * 0.5f, c.y - num2 * 0.5f, num2, num2), Skin.DiscTex);
			GUI.color = new Color(0.1f, 0.09f, 0.08f, 0.92f * alpha);
			GUI.DrawTexture(new Rect(c.x - num * 0.5f, c.y - num * 0.5f, num, num), Skin.DiscTex);
			GUI.color = color;
			Rect r = new Rect(Mathf.Round(c.x - size * 0.5f), Mathf.Round(c.y - size * 0.5f), size, size);
			if (_homeIcon != null)
			{
				GameArt.Icon(r, _homeIcon, new Color(1f, 1f, 1f, alpha));
				return;
			}
			float num3 = Mathf.Max(1f, Mathf.Round(size / 8f));
			Color c2 = new Color(HomeGreen.r, HomeGreen.g, HomeGreen.b, alpha);
			for (int i = 0; i < 3; i++)
			{
				GameArt.Flat(new Rect(Mathf.Round(c.x - (float)(i + 1) * num3), Mathf.Round(c.y - 3f * num3 + (float)i * num3), (float)(i + 1) * 2f * num3, num3), c2);
			}
			GameArt.Flat(new Rect(Mathf.Round(c.x - 2f * num3), Mathf.Round(c.y), 4f * num3, 3f * num3), c2);
		}

		private static void HomeMarkLabelled(Vector2 s, float k, bool label)
		{
			HomeMark(s, Mathf.Round(10f * k), 1f);
			if (label)
			{
				GameArt.Label(new Rect(s.x - 40f * k, s.y - 16f * k, 80f * k, 8f * k), "Your house", 4f, TextAnchor.UpperCenter, HomeGreen);
			}
		}

		private void DrawHotbar(float sw, float sh, Actor host)
		{
			if (Event.current.type != EventType.Repaint)
			{
				return;
			}
			HotbarShownForTest = 0f;
			if (host == null || !PanelsNow || _mod.Mounted || _mod.Craft.Open || _mod.Converse.Open || _mod.DeathTimer >= 0f || Planning.Begun)
			{
				return;
			}
			int num = Hotbar.Size(host);
			if (num <= 0)
			{
				return;
			}
			float p = GameArt.P;
			float unscaledTime = Time.unscaledTime;
			List<Gear.Piece> gear = Gear.Of(host);
			List<KeyValuePair<string, int>> bag = BagNow(host);
			List<string> list = BagOrder.Of(host);
			int num2 = Hotbar.Chosen(host);
			int num3 = num2;
			for (int i = 0; i < num; i++)
			{
				string text = ((i < list.Count) ? list[i] : "");
				num3 = num3 * 31 + text.GetHashCode();
				if (BagOrder.ResourceOf(text) != null)
				{
					num3 = num3 * 31 + SlotAmount(bag, text);
				}
				else if (text == "h")
				{
					num3 = num3 * 31 + (Hotbar.Weapon(host)?.asset?.id ?? "").GetHashCode();
				}
			}
			if (host != _hotbarOf || num3 != _hotbarSig)
			{
				Hotbar.ChangedAt = unscaledTime;
			}
			if (host == _hotbarOf && num2 != _hotbarChosen)
			{
				_hotbarNameAt = unscaledTime;
			}
			_hotbarOf = host;
			_hotbarSig = num3;
			_hotbarChosen = num2;
			float num4 = (HotbarShownForTest = Mathf.Clamp01(1f - (unscaledTime - Hotbar.ChangedAt - 3f) / 0.6f) * _vitalsAlpha);
			if (num4 <= 0.01f)
			{
				return;
			}
			float num6 = Mathf.Round(20f * p);
			float num7 = Mathf.Round(2f * p);
			float num8 = Mathf.Round(2f * p);
			float num9 = (float)num * (num6 + num7) - num7;
			float num10 = Mathf.Round(sw * 0.5f - num9 * 0.5f);
			float num11 = Mathf.Round(_vitalsRect.y - num6 - 5f * p);
			Color color = GUI.color;
			GUI.color = new Color(color.r, color.g, color.b, color.a * num4);
			try
			{
				for (int j = 0; j < num; j++)
				{
					bool flag = j == num2;
					Rect c = new Rect(num10 + (float)j * (num6 + num7), flag ? (num11 - num8) : num11, num6, num6);
					string text2 = ((j < list.Count) ? list[j] : "");
					DrawBagCell(c, text2, (text2.Length > 0) ? BagIcon(host, text2, gear) : null, bag, 1f);
					HotbarMarks(c, j, flag);
				}
				float num12 = unscaledTime - _hotbarNameAt;
				string text3 = ((num12 < 1.6f && num2 < list.Count) ? HeldName(host, list[num2], gear) : null);
				if (text3 != null)
				{
					GameArt.Label(new Rect(Mathf.Round(sw * 0.5f - 120f * p), Mathf.Round(num11 - num8 - 11f * p), Mathf.Round(240f * p), Mathf.Round(9f * p)), text3, 5f, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, Mathf.Clamp01((1.6f - num12) / 0.4f)));
				}
			}
			finally
			{
				GUI.color = color;
			}
		}

		private static string HeldName(Actor host, string key, List<Gear.Piece> gear)
		{
			if (key == "h")
			{
				return NiceName(Hotbar.Weapon(host)?.asset);
			}
			string text = BagOrder.ResourceOf(key);
			if (text != null)
			{
				return Work.ResourceName(text);
			}
			int num = BagOrder.PieceOf(key);
			if (num < 0 || num >= gear.Count)
			{
				return null;
			}
			return Gear.NameOf(gear[num]);
		}

		private void DrawHuntMarks(Func<Vector2, Vector2> toMap, Func<Vector2, bool> shows, Vector2 centre, float radius, float size, float alpha, bool rim)
		{
			Quests.HuntMarks(_huntMarks);
			if (_huntMarks.Count == 0)
			{
				return;
			}
			float p = GameArt.P;
			Sprite sprite = GameArt.IconNamed("iconCitizenJobHunter");
			Color color = GUI.color;
			foreach (Vector2 huntMark in _huntMarks)
			{
				Vector2 vector = toMap(huntMark);
				float num = alpha;
				if (rim)
				{
					Vector2 vector2 = vector - centre;
					float num2 = radius - 4.5f * p;
					if (vector2.magnitude > num2)
					{
						vector = centre + vector2.normalized * num2;
						num *= 0.75f;
					}
				}
				else if (shows != null && !shows(vector))
				{
					continue;
				}
				float num3 = Mathf.Round(size * 1.3f);
				GUI.color = new Color(0f, 0f, 0f, 0.55f * num);
				GUI.DrawTexture(new Rect(vector.x - num3 * 0.5f + 1f, vector.y - num3 * 0.5f + 1f, num3, num3), Skin.DiscTex);
				GUI.color = new Color(0.32f, 0.2f, 0.1f, 0.95f * num);
				GUI.DrawTexture(new Rect(vector.x - num3 * 0.5f, vector.y - num3 * 0.5f, num3, num3), Skin.DiscTex);
				GUI.color = color;
				Rect r = new Rect(Mathf.Round(vector.x - size * 0.5f), Mathf.Round(vector.y - size * 0.5f), size, size);
				if (sprite != null)
				{
					GameArt.Icon(r, sprite, new Color(1f, 1f, 1f, num));
				}
				else
				{
					GameArt.Flat(new Rect(vector.x - size * 0.25f, vector.y - size * 0.25f, size * 0.5f, size * 0.5f), new Color(0.85f, 0.55f, 0.3f, num));
				}
			}
			GUI.color = color;
		}

		private void DrawHurtFrom(float sw, float sh, CombatFeedback fb, float yaw)
		{
			if (Event.current.type != EventType.Repaint)
			{
				return;
			}
			HurtMarksDrawn = 0;
			if (fb.HurtMarks.Count == 0)
			{
				return;
			}
			if (_hurtArc == null)
			{
				_hurtArc = MakeHurtArc();
			}
			Vector2 pivotPoint = new Vector2(sw * 0.5f, sh * 0.5f);
			Matrix4x4 matrix = GUI.matrix;
			Color color = GUI.color;
			float num = float.MaxValue;
			foreach (HurtMark hurtMark in fb.HurtMarks)
			{
				if (!(hurtMark.Toward.sqrMagnitude < 1E-06f))
				{
					float num2 = Mathf.Atan2(hurtMark.Toward.y, hurtMark.Toward.x) - yaw;
					float num3 = sh * 0.085f * HurtArc.Pop(hurtMark.Age);
					GUI.matrix = matrix;
					GUIUtility.RotateAroundPivot((0f - num2) * 57.29578f, pivotPoint);
					GUI.color = (hurtMark.Blocked ? new Color(0.5f, 0.8f, 1f, 0.9f * HurtArc.Alpha(hurtMark.Age)) : new Color(1f, 0.17f, 0.11f, 0.92f * HurtArc.Alpha(hurtMark.Age)));
					GUI.DrawTexture(new Rect(pivotPoint.x + -0.47f * num3, pivotPoint.y - 1.3f * num3, 0.94f * num3, 0.52f * num3), _hurtArc);
					HurtMarksDrawn++;
					if (hurtMark.Age < num)
					{
						num = hurtMark.Age;
						HurtMarkAngle = Mathf.Repeat(num2 + MathF.PI, MathF.PI * 2f) - MathF.PI;
					}
				}
			}
			GUI.matrix = matrix;
			GUI.color = color;
		}

		private static Texture2D MakeHurtArc()
		{
			int height;
			uint[] array = HurtArc.Paint(192, out height);
			Color32[] array2 = new Color32[array.Length];
			for (int i = 0; i < array2.Length; i++)
			{
				uint num = array[i];
				array2[i] = new Color32((byte)(num & 0xFF), (byte)((num >> 8) & 0xFF), (byte)((num >> 16) & 0xFF), (byte)(num >> 24));
			}
			Texture2D texture2D = new Texture2D(192, height, TextureFormat.RGBA32, mipChain: true);
			texture2D.filterMode = FilterMode.Trilinear;
			texture2D.wrapMode = TextureWrapMode.Clamp;
			texture2D.hideFlags = HideFlags.HideAndDontSave;
			texture2D.SetPixels32(array2);
			texture2D.Apply(updateMipmaps: true);
			return texture2D;
		}

		private string DrawDoll(Rect r, Actor host)
		{
			float p = GameArt.P;
			Event current = Event.current;
			CharacterView youView = _mod.YouView;
			bool flag = false;
			try
			{
				flag = host.equipment != null && host.understandsHowToUseItems();
			}
			catch
			{
			}
			float num = Mathf.Round(22f * p);
			float num2 = Mathf.Round(4f * p);
			Rect r2 = (flag ? new Rect(r.x + num + num2, r.y, r.width - 2f * (num + num2), r.height) : r);
			string text = null;
			if (current.type == EventType.MouseDown && current.button == 0 && r2.Contains(current.mousePosition))
			{
				youView.Dragging = true;
				current.Use();
			}
			else if (current.type == EventType.MouseDrag && youView.Dragging)
			{
				youView.Turn -= current.delta.x * 0.012f;
				current.Use();
			}
			else if (current.rawType == EventType.MouseUp || (current.type == EventType.Repaint && !Input.GetMouseButton(0)))
			{
				youView.Dragging = false;
			}
			if (current.type == EventType.Repaint)
			{
				PortraitWell(r2, null);
				float num3 = Mathf.Max(1f, Mathf.Round(p));
				float num4 = Mathf.Round(3f * p);
				int num5 = Mathf.FloorToInt((r2.width - num4 * 2f) / num3);
				int num6 = Mathf.FloorToInt((r2.height - num4 * 2f) / num3);
				Rect position = new Rect(Mathf.Round(r2.center.x - (float)num5 * num3 * 0.5f), Mathf.Round(r2.center.y - (float)num6 * num3 * 0.5f), (float)num5 * num3, (float)num6 * num3);
				if (_mod.DrawYou(num5, num6) && youView.Picture != null)
				{
					GUI.DrawTexture(position, youView.Picture);
				}
				else
				{
					Sprite s = null;
					try
					{
						s = host.getSpriteToRender();
					}
					catch
					{
					}
					float num7 = Mathf.Round(r2.width * 0.18f);
					DrawSprite(new Rect(position.x + num7, position.y + num7, position.width - num7 * 2f, position.height - num7 * 2f), s);
				}
			}
			if (r2.Contains(current.mousePosition) && !youView.Dragging)
			{
				text = "Drag to turn";
			}
			if (!flag)
			{
				return text;
			}
			for (int i = 0; i < 3; i++)
			{
				float y = Mathf.Round(r.y + (r.height - num) * (0.06f + 0.44f * (float)i));
				text = WornSlot(new Rect(r.x, y, num, num), host, WornLeft[i]) ?? text;
				text = WornSlot(new Rect(r.xMax - num, y, num, num), host, WornRight[i]) ?? text;
			}
			return text;
		}

		private string WornSlot(Rect r, Actor host, EquipmentType t)
		{
			Item item = null;
			try
			{
				item = host.equipment.getSlot(t).getItem();
			}
			catch
			{
			}
			bool flag = r.Contains(Event.current.mousePosition);
			if (Event.current.type == EventType.Repaint)
			{
				PortraitWell(r, (item != null) ? IconOf(item.asset) : null);
				if (item == null)
				{
					GameArt.Label(r, SlotName(t), 3.4f, TextAnchor.MiddleCenter, new Color(0.62f, 0.62f, 0.66f, 0.85f));
				}
				else if (flag)
				{
					Hover(r);
				}
			}
			WornInput(r, t, item);
			if (!flag)
			{
				return null;
			}
			if (item == null)
			{
				return "No " + SlotName(t).ToLowerInvariant();
			}
			return NiceName(item.asset) + (HeldInBag(host, t) ? "\nClick to put away" : "\nClick to take off");
		}

		private static void Hover(Rect r)
		{
			GameArt.Sliced(r, GameArt.ButtonBorder, new Color(1f, 0.85f, 0.35f, 0.9f), 4f, 4f, 4f, 4f, r.width / 32f);
		}

		private static string SlotName(EquipmentType t)
		{
			if (t != EquipmentType.Armor)
			{
				return t.ToString();
			}
			return "Armour";
		}

		private static string DrawBenchStrip(Rect r, Actor host, int level, bool home)
		{
			float p = GameArt.P;
			string text = ((level == 0) ? "By hand. A house brings a workbench" : (home ? $"Workbench level {level} of {5}, up to {Workbench.AddsAt(level)}" : "By hand. Your workbench is at home"));
			if (Event.current.type == EventType.Repaint)
			{
				GameArt.TipPanel(r);
				Color color = (home ? new Color(1f, 0.82f, 0.4f) : new Color(0.85f, 0.85f, 0.88f));
				GameArt.Label(new Rect(r.x + 4f * p, r.y - 0.5f * p, r.width - 8f * p, r.height), text, 4.4f, TextAnchor.MiddleCenter, color);
			}
			if (!r.Contains(Event.current.mousePosition))
			{
				return null;
			}
			if (level == 0)
			{
				return "Wood, bone and leather by hand. Stone and metal need a workbench";
			}
			int num = Workbench.HouseLevel(host);
			int num2 = Math.Max(0, level - num);
			if (num2 <= 0)
			{
				return $"House gives level {num}. Tasks can add tools";
			}
			return $"House gives level {num}, tools from tasks {num2} more";
		}

		private static void DrawInventoryTip(string text)
		{
			float p = GameArt.P;
			Vector2 mousePosition = Event.current.mousePosition;
			string[] array = text.Split('\n');
			float num = 0f;
			string[] array2 = array;
			foreach (string text2 in array2)
			{
				num = Mathf.Max(num, GameArt.Measure(text2, 4.8f).x);
			}
			num = Mathf.Round(num + 12f * p);
			float num2 = Mathf.Round(9f * p);
			float num3 = Mathf.Round(num2 * (float)array.Length + 3f * p);
			Rect r = new Rect(Mathf.Round(mousePosition.x + 10f * p), Mathf.Round(mousePosition.y + 8f * p), num, num3);
			if (r.xMax > (float)Screen.width - 4f * p)
			{
				r.x = Mathf.Round(mousePosition.x - num - 6f * p);
			}
			if (r.yMax > (float)Screen.height - 4f * p)
			{
				r.y = Mathf.Round(mousePosition.y - num3 - 4f * p);
			}
			GameArt.TipPanel(r);
			for (int j = 0; j < array.Length; j++)
			{
				GameArt.Label(new Rect(r.x, r.y + 1f * p + (float)j * num2, r.width, num2), array[j], (j == 0) ? 4.8f : 4.3f, TextAnchor.MiddleCenter, (j == 0) ? Color.white : new Color(0.8f, 0.8f, 0.84f));
			}
		}

		private static int CraftGroup(Crafting.Recipe r)
		{
			if (r.Supply == null)
			{
				if (!Crafting.IsTool(r))
				{
					if (r.Slot != EquipmentType.Weapon)
					{
						return 2;
					}
					return 1;
				}
				return 0;
			}
			return 3;
		}

		private string DrawCraftGrid(Rect area, Actor host, Crafting craft)
		{
			float p = GameArt.P;
			Event current = Event.current;
			bool flag = current.type == EventType.Repaint;
			string text = null;
			float num = area.width - 9f * p;
			craft.EnsureRecipes();
			bool flag2 = SkillsPage != null;
			if (craft.Tab != 0 && craft.Tab != 3 && (!flag2 || craft.Tab != 2))
			{
				craft.Tab = 0;
			}
			float num2 = Mathf.Round(20f * p);
			Sprite icon = GameArt.IconNamed("iconCraftIron") ?? GameArt.IconNamed("icon_hammer_steel");
			int num3 = (flag2 ? 3 : 2);
			float num4 = Mathf.Round((num - (float)(num3 - 1) * 4f * p) / (float)num3);
			float height = Mathf.Round(17f * p);
			float x = area.x;
			if (GameButton(new Rect(x, area.y, num4, height), "Crafting", craft.Tab == 0, enabled: true, icon) && craft.Tab != 0)
			{
				craft.Tab = 0;
				craft.Scroll = Vector2.zero;
			}
			x += num4 + 4f * p;
			if (flag2)
			{
				if (GameButton(new Rect(x, area.y, num4, height), "Skills", craft.Tab == 2, enabled: true, GameArt.IconNamed("iconExperience")) && craft.Tab != 2)
				{
					craft.Tab = 2;
				}
				x += num4 + 4f * p;
			}
			if (GameButton(new Rect(x, area.y, num4, height), "Magic", craft.Tab == 3, enabled: true, GameArt.IconNamed("iconMana") ?? GameArt.IconNamed("iconSpells")) && craft.Tab != 3)
			{
				craft.Tab = 3;
			}
			Rect rect = new Rect(area.x, area.y + num2 + 2f * p, num, area.height - num2 - 2f * p);
			if (craft.Tab == 2)
			{
				try
				{
					return SkillsPage(rect, host);
				}
				catch (Exception ex)
				{
					Log.Error("skills page: " + ex.Message);
					SkillsPage = null;
					craft.Tab = 0;
					return null;
				}
			}
			if (craft.Tab == 3)
			{
				try
				{
					return DrawMagicPage(rect, host);
				}
				catch (Exception ex2)
				{
					Log.Error("magic page: " + ex2);
					craft.Tab = 0;
					return null;
				}
			}
			int num5 = Workbench.Level(host);
			bool home = num5 > 0 && Workbench.AtHome(host);
			float num6 = Mathf.Round(Mathf.Min(num * 0.42f, 13f * p + GameArt.Measure("Show unable to craft", 4.5f).x + 6f * p));
			Rect r = new Rect(area.x, area.y + num2 + 2f * p, num - num6 - 5f * p, Mathf.Round(13f * p));
			text = DrawBenchStrip(r, host, num5, home) ?? text;
			Rect r2 = new Rect(r.xMax + 5f * p, r.y + 0.5f * p, num6, Mathf.Round(12f * p));
			if (Switch(r2, "Show unable to craft", craft.ShowAll))
			{
				craft.ShowAll = !craft.ShowAll;
				craft.Scroll = Vector2.zero;
			}
			float num7 = Mathf.Round(Mathf.Clamp(area.height * 0.4f, 84f * p, 104f * p));
			float num8 = Mathf.Round(20f * p);
			Rect rect2 = new Rect(area.x, area.yMax - num7, num, num7);
			Rect r3 = new Rect(area.x, rect2.y - num8 - 3f * p, num, num8);
			List<Crafting.Recipe> list = new List<Crafting.Recipe>();
			List<bool> list2 = new List<bool>();
			int[] array = new int[CraftGroups.Length];
			for (int i = 0; i < CraftGroups.Length; i++)
			{
				foreach (Crafting.Recipe recipe5 in craft.Recipes)
				{
					if (CraftGroup(recipe5) == i)
					{
						bool flag3 = Crafting.CanMake(host, recipe5);
						if (flag3 || craft.ShowAll || craft.Queued(recipe5) != 0 || recipe5 == craft.Selected)
						{
							list.Add(recipe5);
							list2.Add(flag3);
							array[i]++;
						}
					}
				}
			}
			if (craft.Selected != null && !craft.Recipes.Contains(craft.Selected))
			{
				craft.Selected = null;
			}
			if (craft.Selected == null && list.Count > 0)
			{
				craft.Selected = craft.Working ?? list[0];
			}
			Rect view = new Rect(area.x, r2.yMax + 4f * p, num, r3.y - 3f * p - (r2.yMax + 4f * p));
			float num9 = Mathf.Round(26f * p);
			float num10 = Mathf.Round(3f * p);
			float num11 = Mathf.Round(11f * p);
			int num12 = Mathf.Max(1, Mathf.FloorToInt((view.width + num10) / (num9 + num10)));
			float num13 = 0f;
			int[] array2 = array;
			foreach (int num14 in array2)
			{
				if (num14 > 0)
				{
					num13 += num11 + (float)((num14 + num12 - 1) / num12) * (num9 + num10);
				}
			}
			float contentH = Mathf.Max(view.height, num13);
			float num15 = Mathf.Round((view.width - ((float)num12 * (num9 + num10) - num10)) * 0.5f);
			float num16 = BeginList(view, ref craft.Scroll, contentH);
			Crafting.Recipe recipe = null;
			Crafting.Recipe recipe2 = null;
			Crafting.Recipe recipe3 = null;
			float progress = craft.Progress;
			float num17 = num16;
			int num18 = -1;
			int num19 = 0;
			for (int k = 0; k < list.Count; k++)
			{
				Crafting.Recipe recipe4 = list[k];
				int num20 = CraftGroup(recipe4);
				if (num20 != num18)
				{
					if (num18 >= 0)
					{
						num17 += num11 + (float)((num19 + num12 - 1) / num12) * (num9 + num10);
					}
					num18 = num20;
					num19 = 0;
					if (flag)
					{
						string text2 = CraftGroups[num20];
						float x2 = GameArt.Measure(text2, 4.4f).x;
						GameArt.Label(new Rect(num15, num17, x2 + 2f * p, num11 - 2f * p), text2, 4.4f, TextAnchor.MiddleLeft, GameArt.Peach);
						float num21 = num15 + x2 + 5f * p;
						float num22 = num15 + (float)num12 * (num9 + num10) - num10 - num21;
						if (num22 > 0f)
						{
							GameArt.Flat(new Rect(num21, Mathf.Round(num17 + (num11 - 2f * p) * 0.5f), num22, Mathf.Max(1f, Mathf.Round(p * 0.5f))), new Color(1f, 0.74f, 0.4f, 0.35f));
						}
					}
				}
				Rect rect3 = new Rect(num15 + (float)(num19 % num12) * (num9 + num10), num17 + num11 + (float)(num19 / num12) * (num9 + num10), num9, num9);
				num19++;
				int num23 = craft.Queued(recipe4);
				bool flag4 = craft.Working == recipe4;
				bool flag5 = craft.Selected == recipe4;
				bool flag6 = _listMouseIn && rect3.Contains(current.mousePosition);
				string text3 = Crafting.Locked(host, recipe4);
				if (flag)
				{
					float num24 = (flag4 ? (BlowShake(craft) * p) : 0f);
					PortraitWell(new Rect(rect3.x + num24, rect3.y, rect3.width, rect3.height), RecipeIcon(recipe4), list2[k] ? 1f : 0.4f);
					if (flag4 && progress >= 0f)
					{
						float num25 = Mathf.Round((rect3.height - 4f * p) * progress);
						GameArt.Flat(new Rect(rect3.x + 2f * p, rect3.yMax - 2f * p - num25, rect3.width - 4f * p, num25), new Color(1f, 0.78f, 0.25f, 0.4f));
					}
					if (text3 != null && recipe4.Rank > Workbench.Level(host))
					{
						DrawLock(new Rect(rect3.xMax - 9f * p, rect3.y + 1.5f * p, 7.5f * p, 7.5f * p));
					}
					int num26 = num23 - (flag4 ? 1 : 0);
					if (num26 > 0)
					{
						GameArt.Label(new Rect(rect3.x + 2f * p, rect3.yMax - 8.5f * p, rect3.width - 3f * p, 8f * p), "+" + num26, 4.2f, TextAnchor.LowerRight, GameArt.Orange);
					}
					if (flag5)
					{
						GameArt.Sliced(rect3, GameArt.ButtonBorder, new Color(1f, 0.62f, 0.12f, 1f), 4f, 4f, 4f, 4f, rect3.width / 32f);
					}
					else if (flag6)
					{
						Hover(rect3);
					}
				}
				if (flag6)
				{
					text = recipe4.Name + "\n" + (flag4 ? "Being made" : (text3 ?? (list2[k] ? recipe4.Cost : (recipe4.Cost + ". Not enough"))));
					if (current.type == EventType.MouseDown)
					{
						if (current.button == 1)
						{
							recipe3 = recipe4;
						}
						else if (current.button == 0)
						{
							recipe = recipe4;
							if (current.clickCount >= 2)
							{
								recipe2 = recipe4;
							}
						}
						current.Use();
					}
				}
				PadNav.Spot(rect3);
				if (GUI.Button(rect3, GUIContent.none, GUIStyle.none) && _listMouseIn)
				{
					recipe = recipe4;
				}
			}
			if (list.Count == 0 && flag)
			{
				GameArt.Label(new Rect(8f * p, num16 + 6f * p, view.width - 16f * p, 20f * p), craft.ShowAll ? "Nothing here" : "Nothing you can make with what you have", 4.6f, TextAnchor.MiddleCenter, GameArt.Grey, wrap: true);
			}
			EndList(view, ref craft.Scroll, contentH);
			if (recipe != null && recipe != craft.Selected)
			{
				craft.Selected = recipe;
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
			if (recipe2 != null)
			{
				AskCraft(host, craft, recipe2, 1);
			}
			if (recipe3 != null && craft.CancelLast(host, recipe3))
			{
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
			CraftResultInput(rect2);
			text = DrawCraftQueue(r3, host, craft) ?? text;
			text = DrawRecipeCard(rect2, host, craft, craft.Selected) ?? text;
			if (DrawCraftResult(rect2, (_bagArea.width > 0f) ? _bagArea : new Rect(_invWin.x, _invWin.yMax - 40f * p, 100f * p, 30f * p)) && rect2.Contains(current.mousePosition))
			{
				text = null;
			}
			return text;
		}

		private static void DrawLock(Rect r)
		{
			float num = r.width / 7f;
			Color c = new Color(0.12f, 0.1f, 0.1f, 0.9f);
			Color c2 = new Color(0.92f, 0.78f, 0.36f, 1f);
			GameArt.Flat(new Rect(r.x + 1f * num, r.y + 3f * num, 5f * num, 4f * num), c);
			GameArt.Flat(new Rect(r.x + 1.5f * num, r.y + 3.5f * num, 4f * num, 3f * num), c2);
			GameArt.Flat(new Rect(r.x + 2f * num, r.y + 0.5f * num, 3f * num, 1f * num), c2);
			GameArt.Flat(new Rect(r.x + 1.75f * num, r.y + 1f * num, 1f * num, 2.5f * num), c2);
			GameArt.Flat(new Rect(r.x + 4.25f * num, r.y + 1f * num, 1f * num, 2.5f * num), c2);
			GameArt.Flat(new Rect(r.x + 3.1f * num, r.y + 4.4f * num, 0.8f * num, 1.4f * num), c);
		}

		private static bool Switch(Rect r, string label, bool on)
		{
			float p = GameArt.P;
			float num = Mathf.Round(r.height);
			Rect r2 = new Rect(r.x, r.y, num, num);
			if (Event.current.type == EventType.Repaint)
			{
				GameArt.Sliced(r2, GameArt.Button, Color.white, 6f, 6f, 6f, 6f, Mathf.Min(p, num / 24f));
				if (on)
				{
					GameArt.Flat(new Rect(r2.x + num * 0.28f, r2.y + num * 0.28f, num * 0.44f, num * 0.44f), GameArt.Orange);
				}
				bool flag = r.Contains(Event.current.mousePosition);
				GameArt.Label(new Rect(r2.xMax + 4f * p, r.y - 0.5f * p, r.width - num - 4f * p, r.height), label, 4.5f, TextAnchor.MiddleLeft, flag ? Color.white : new Color(0.85f, 0.85f, 0.88f));
			}
			Rect rect = new Rect(r.x, r.y, Mathf.Min(r.width, num + 6f * p + GameArt.Measure(label, 4.5f).x), r.height);
			PadNav.Spot(rect);
			if (!GUI.Button(rect, GUIContent.none, GUIStyle.none))
			{
				return false;
			}
			Sounds.PlayUI("event:/SFX/Ui/Click");
			return true;
		}

		private List<KeyValuePair<string, int>> BagNow(Actor host)
		{
			if (_bagNowFrame == Time.frameCount && _bagNowOf == host)
			{
				return _bagNow;
			}
			_bagNowFrame = Time.frameCount;
			_bagNowOf = host;
			_bagNow.Clear();
			try
			{
				Dictionary<string, ResourceContainer> dictionary = host.inventory?.getResources();
				if (dictionary != null)
				{
					foreach (KeyValuePair<string, ResourceContainer> item in dictionary)
					{
						if (item.Value.amount > 0)
						{
							_bagNow.Add(new KeyValuePair<string, int>(item.Key, item.Value.amount));
						}
					}
				}
			}
			catch
			{
			}
			Supplies.AddTo(host, _bagNow);
			_bagNow.Sort((KeyValuePair<string, int> a, KeyValuePair<string, int> b) => string.CompareOrdinal(a.Key, b.Key));
			return _bagNow;
		}

		internal int BagCountNow(Actor host)
		{
			int num = 0;
			foreach (KeyValuePair<string, int> item in BagNow(host))
			{
				num += item.Value;
			}
			return num;
		}

		public void DrawCraftPanel()
		{
			if (Time.frameCount - _invDrawnFrame > 1)
			{
				_invDrag = null;
			}
			_invDrawnFrame = Time.frameCount;
			if (Event.current.type == EventType.Layout)
			{
				_wornRects.Clear();
			}
			Color color = GUI.color;
			if (_mod.QuickInventory)
			{
				GUI.color = new Color(color.r, color.g, color.b, color.a * 0.55f);
			}
			try
			{
				DrawCraftWindow();
			}
			finally
			{
				GUI.color = color;
			}
			InventoryDragEnd(_mod.Host);
		}

		public void DrawFavoritesBeside()
		{
			Color color = GUI.color;
			if (_mod.QuickInventory)
			{
				GUI.color = new Color(color.r, color.g, color.b, color.a * 0.55f);
			}
			try
			{
				DrawFavorites();
			}
			finally
			{
				GUI.color = color;
			}
		}

		private string DrawBagGrid(Rect r, Actor host, Work work, float cell, float gap)
		{
			float p = GameArt.P;
			Event current = Event.current;
			bool flag = current.type == EventType.Repaint;
			Vector2 vector = BagMouseForTest ?? current.mousePosition;
			cell = Mathf.Min(cell, Mathf.Floor((r.width - 7f * gap) / 8f));
			float num = Mathf.Round(4f * p);
			int num2 = Hotbar.Size(host);
			int num3 = Mathf.Max(0, Mathf.FloorToInt((r.height - cell - num + gap) / (cell + gap)));
			int num4 = Mathf.Min(Bag.Size(host), num2 + num3 * 8);
			int num5 = (num4 - num2 + 8 - 1) / 8;
			float num6 = Mathf.Round(r.x + (r.width - (8f * (cell + gap) - gap)) * 0.5f);
			float num7 = Mathf.Round(r.x + (r.width - ((float)num2 * (cell + gap) - gap)) * 0.5f);
			float y = Mathf.Round(r.y + (float)num5 * (cell + gap) + ((num5 > 0) ? (num - gap) : 0f));
			List<Gear.Piece> list = Gear.Of(host);
			List<KeyValuePair<string, int>> bag = BagNow(host);
			List<string> list2 = BagOrder.Fit(host, bag, list.Count, num4);
			_bagCellCount = num4;
			int num8 = 0;
			int num9 = 0;
			for (int i = 0; i < list2.Count; i++)
			{
				if (list2[i].Length != 0)
				{
					num9++;
					if (i >= num4 - 1)
					{
						num8++;
					}
				}
			}
			bool flag2 = num9 > 0 && list2.Count > num4 && num8 > 1;
			bool flag3 = _invDrag != null && _invDrag.Moving;
			string text = null;
			int num10 = Hotbar.Chosen(host);
			_bagCells.Clear();
			for (int j = 0; j < num4; j++)
			{
				Rect rect = ((j < num2) ? new Rect(num7 + (float)j * (cell + gap), y, cell, cell) : new Rect(num6 + (float)((j - num2) % 8) * (cell + gap), r.y + (float)((j - num2) / 8) * (cell + gap), cell, cell));
				_bagCells.Add(rect);
				bool flag4 = flag2 && j == num4 - 1;
				string text2 = ((!flag4 && j < list2.Count) ? list2[j] : "");
				bool flag5 = text2.Length > 0;
				bool flag6 = rect.Contains(vector);
				Sprite icon = (flag5 ? BagIcon(host, text2, list) : null);
				if (flag)
				{
					if (flag4)
					{
						PortraitWell(rect, null);
						GameArt.Label(rect, "+" + num8, 4.6f, TextAnchor.MiddleCenter, Color.white);
					}
					else
					{
						DrawBagCell(rect, text2, icon, bag, (flag3 && _invDrag.Cell == j) ? 0.35f : 1f);
					}
					if (j < num2)
					{
						HotbarMarks(rect, j, j == num10);
					}
					if (flag6 && (flag5 || flag3) && !flag4)
					{
						Hover(rect);
					}
				}
				if (flag4)
				{
					if (flag6)
					{
						text = num8 + " more";
					}
					continue;
				}
				if (flag5 && flag6 && current.type == EventType.MouseDown && current.button == 0 && _invDrag == null)
				{
					_invDrag = new InvDrag
					{
						Cell = j,
						Key = text2,
						Icon = icon,
						From = vector,
						Frame = Time.frameCount
					};
					current.Use();
				}
				if (flag5 && flag6 && current.type == EventType.KeyDown && current.keyCode >= KeyCode.Alpha1 && (int)current.keyCode < 49 + num2 && _invDrag == null)
				{
					int num11 = (int)(current.keyCode - 49);
					if (num11 != j)
					{
						BagOrder.Swap(host, j, num11);
						Hotbar.Moved();
						Sounds.PlayUI("event:/SFX/Ui/Click");
					}
					current.Use();
				}
				if (flag5 && flag6 && !flag3)
				{
					text = BagTip(host, bag, list, text2);
				}
			}
			if (num9 == 0 && flag)
			{
				Rect r2 = new Rect(Mathf.Round(r.x + 8f * p), Mathf.Round(r.y + cell * 0.5f), Mathf.Round(r.width - 16f * p), Mathf.Round(cell + gap));
				GameArt.TipPanel(r2);
				GameArt.Label(new Rect(r2.x + 4f * p, r2.y, r2.width - 8f * p, r2.height), "Empty. Hold the left mouse button on trees, rocks, ore, bushes and crops to gather.", 4.3f, TextAnchor.MiddleCenter, new Color(0.85f, 0.85f, 0.88f), wrap: true);
			}
			if (flag)
			{
				BagTipForTest = text;
			}
			return text;
		}

		private void WornInput(Rect r, EquipmentType t, Item item)
		{
			_wornRects[t] = r;
			Event current = Event.current;
			bool flag = r.Contains(current.mousePosition);
			if (current.type == EventType.Repaint && flag && _invDrag != null && _invDrag.Moving && _invDrag.Cell >= 0)
			{
				Hover(r);
			}
			if (item != null && flag && current.type == EventType.MouseDown && current.button == 0 && _invDrag == null)
			{
				_invDrag = new InvDrag
				{
					Worn = t,
					Icon = IconOf(item.asset),
					From = current.mousePosition,
					Frame = Time.frameCount
				};
				current.Use();
			}
		}

		private Sprite BagIcon(Actor host, string key, List<Gear.Piece> gear)
		{
			if (key == "h")
			{
				return IconOf(Hotbar.Weapon(host)?.asset);
			}
			string text = BagOrder.ResourceOf(key);
			if (text != null)
			{
				return ResourceIcon(text);
			}
			int num = BagOrder.PieceOf(key);
			if (num < 0 || num >= gear.Count)
			{
				return null;
			}
			return IconOf(gear[num].Asset);
		}

		private static int BagAmount(List<KeyValuePair<string, int>> bag, string id)
		{
			foreach (KeyValuePair<string, int> item in bag)
			{
				if (item.Key == id)
				{
					return item.Value;
				}
			}
			return 0;
		}

		private static int SlotAmount(List<KeyValuePair<string, int>> bag, string key)
		{
			string text = BagOrder.ResourceOf(key);
			if (text == null)
			{
				return 0;
			}
			return BagOrder.StackAmount(key, BagAmount(bag, text));
		}

		private static string BagTip(Actor host, List<KeyValuePair<string, int>> bag, List<Gear.Piece> gear, string key)
		{
			if (key == "h")
			{
				return NiceName(Hotbar.Weapon(host)?.asset) + "\nIn your hand";
			}
			string text = BagOrder.ResourceOf(key);
			int num = SlotAmount(bag, key);
			Supplies.Kind kind = Supplies.Of(text);
			if (kind != null && !Supplies.Heals(kind))
			{
				return num + " " + kind.Name + "\n" + Torches.Tip(host);
			}
			if (kind != null)
			{
				return num + " " + kind.Name + "\nHeals " + Mathf.RoundToInt(kind.Heal * 100f) + "% over " + Mathf.RoundToInt(kind.Seconds) + " s. Click to use";
			}
			if (text != null)
			{
				return num + " " + Work.ResourceName(text) + "\n" + (Eating.Edible(host, text) ? "Click to eat" : "Drag out of the window to drop");
			}
			int num2 = BagOrder.PieceOf(key);
			if (num2 < 0 || num2 >= gear.Count)
			{
				return null;
			}
			Rarity rarity = CraftOdds.QualityOf(gear[num2].Asset, gear[num2].Mods);
			return Gear.NameOf(gear[num2]) + "\n" + ((rarity > Rarity.R0_Normal) ? (CraftOdds.Word(rarity) + ". ") : "") + ((Hotbar.IsWeapon(gear[num2]) && Hotbar.UsesItems(host)) ? "Click to hold" : "Click to put on");
		}

		private void DrawBagCell(Rect c, string key, Sprite icon, List<KeyValuePair<string, int>> bag, float iconAlpha)
		{
			float p = GameArt.P;
			bool flag = !string.IsNullOrEmpty(key);
			PortraitWell(c, null, flag ? 1f : 0.55f);
			if (flag)
			{
				float num = Mathf.Round(c.width * 0.175f);
				Color color = GUI.color;
				GUI.color = new Color(color.r, color.g, color.b, color.a * iconAlpha);
				DrawSprite(new Rect(c.x + num, c.y + num, c.width - num * 2f, c.height - num * 2f), icon);
				if (BagOrder.ResourceOf(key) != null)
				{
					GameArt.Label(new Rect(c.x, c.yMax - 8.5f * p, c.width - 2f * p, 8f * p), SlotAmount(bag, key).ToString(), 4.4f, TextAnchor.LowerRight, Color.white);
				}
				GUI.color = color;
			}
		}

		private static void HotbarMarks(Rect c, int i, bool chosen)
		{
			float p = GameArt.P;
			if (chosen)
			{
				for (int j = 1; j <= 2; j++)
				{
					Rect r = new Rect(c.x - (float)j * p, c.y - (float)j * p, c.width + 2f * (float)j * p, c.height + 2f * (float)j * p);
					GameArt.Sliced(r, GameArt.ButtonBorder, new Color(1f, 0.97f, 0.8f, 1f), 4f, 4f, 4f, 4f, r.width / 28f);
				}
			}
			GameArt.Label(new Rect(c.x + 2f * p, c.y + 0.5f * p, c.width, 6f * p), (i + 1).ToString(), 3.8f, TextAnchor.UpperLeft, GameArt.KeyOrange);
		}

		private void InventoryDragEnd(Actor host)
		{
			InvDrag invDrag = _invDrag;
			if (invDrag == null || host == null)
			{
				_invDrag = null;
				return;
			}
			Event current = Event.current;
			Vector2 mousePosition = current.mousePosition;
			if (current.type == EventType.MouseDrag)
			{
				if (!invDrag.Moving && (mousePosition - invDrag.From).sqrMagnitude > 36f)
				{
					invDrag.Moving = true;
				}
				current.Use();
			}
			else if (current.rawType == EventType.MouseUp)
			{
				if (current.button != 0)
				{
					if (current.button == 1)
					{
						_invDrag = null;
						current.Use();
					}
					return;
				}
				_invDrag = null;
				if (!invDrag.Moving)
				{
					InventoryClick(host, invDrag);
				}
				else
				{
					InventoryDrop(host, invDrag, mousePosition, current.shift);
				}
				current.Use();
			}
			else
			{
				if (current.type != EventType.Repaint)
				{
					return;
				}
				if (Time.frameCount > invDrag.Frame + 1 && !Input.GetMouseButton(0))
				{
					_invDrag = null;
				}
				else if (invDrag.Moving && !(invDrag.Icon == null))
				{
					float p = GameArt.P;
					float num = Mathf.Round(18f * p);
					DrawSprite(new Rect(Mathf.Round(mousePosition.x - num * 0.5f), Mathf.Round(mousePosition.y - num * 0.5f), num, num), invDrag.Icon);
					if (!_invWin.Contains(mousePosition))
					{
						string text = ((BagOrder.ResourceOf(invDrag.Key) != null && !current.shift) ? "Drop all" : "Drop");
						float width = GameArt.Measure(text, 4.6f).x + 10f * p;
						Rect r = new Rect(Mathf.Round(mousePosition.x + num * 0.5f + 2f * p), Mathf.Round(mousePosition.y - 6f * p), width, 12f * p);
						GameArt.TipPanel(r);
						GameArt.Label(r, text, 4.6f, TextAnchor.MiddleCenter, Color.white);
					}
				}
			}
		}

		private static bool HeldInBag(Actor host, EquipmentType t)
		{
			if (t == EquipmentType.Weapon)
			{
				return BagOrder.Of(host).Contains("h");
			}
			return false;
		}

		private void InventoryClick(Actor host, InvDrag d)
		{
			if (d.Cell < 0)
			{
				if (HeldInBag(host, d.Worn) ? Hotbar.Stow(host, _mod.Toast) : Gear.TakeOff(host, d.Worn, _mod.Toast))
				{
					Sounds.PlayUI("event:/SFX/Ui/Click");
				}
			}
			else
			{
				if (d.Key == "h")
				{
					return;
				}
				string text = BagOrder.ResourceOf(d.Key);
				Supplies.Kind kind = Supplies.Of(text);
				if (kind != null && !Supplies.Heals(kind))
				{
					if (Torches.Toggle(host, _mod.Toast))
					{
						Sounds.PlayUI("event:/SFX/Ui/Click");
					}
				}
				else if (kind != null)
				{
					int j = BagOrder.TakeFrom(host, d.Cell, 1, BagNow(host));
					if (Bandages.Use(host, kind, host, _mod.Toast))
					{
						_bagNowFrame = -1;
					}
					else
					{
						BagOrder.Swap(host, d.Cell, j);
					}
				}
				else if (text != null)
				{
					if (Eating.Edible(host, text))
					{
						int j2 = BagOrder.TakeFrom(host, d.Cell, 1, BagNow(host));
						if (Eating.EatFromBag(host, text, _mod.Toast))
						{
							_bagNowFrame = -1;
						}
						else
						{
							BagOrder.Swap(host, d.Cell, j2);
						}
					}
				}
				else
				{
					int num = BagOrder.PieceOf(d.Key);
					if (num >= 0)
					{
						PutOnFromBag(host, num, d.Cell);
					}
				}
			}
		}

		private void InventoryDrop(Actor host, InvDrag d, Vector2 m, bool one)
		{
			for (int i = 0; i < _bagCells.Count; i++)
			{
				if (!_bagCells[i].Contains(m))
				{
					continue;
				}
				if (d.Cell >= 0)
				{
					BagOrder.Swap(host, d.Cell, i);
					Hotbar.Moved();
					Sounds.PlayUI("event:/SFX/Ui/Click");
				}
				else if (HeldInBag(host, d.Worn))
				{
					BagOrder.Swap(host, BagOrder.Of(host).IndexOf("h"), i);
					Hotbar.Moved();
					Sounds.PlayUI("event:/SFX/Ui/Click");
				}
				else if (Gear.TakeOff(host, d.Worn, _mod.Toast))
				{
					Sounds.PlayUI("event:/SFX/Ui/Click");
					int num = BagOrder.Fit(host, BagNow(host), Gear.Count(host), _bagCellCount).IndexOf(BagOrder.PieceKey(Gear.Count(host) - 1));
					if (num >= 0)
					{
						BagOrder.Swap(host, num, i);
					}
				}
				return;
			}
			foreach (KeyValuePair<EquipmentType, Rect> wornRect in _wornRects)
			{
				if (wornRect.Value.Contains(m))
				{
					int num2 = ((d.Cell >= 0) ? BagOrder.PieceOf(d.Key) : (-1));
					if (num2 >= 0)
					{
						PutOnFromBag(host, num2, d.Cell);
					}
					return;
				}
			}
			if (_invWin.Contains(m))
			{
				return;
			}
			if (d.Cell < 0 || d.Key == "h")
			{
				_mod.DropWorn((d.Cell < 0) ? d.Worn : EquipmentType.Weapon);
				return;
			}
			string text = BagOrder.ResourceOf(d.Key);
			if (text != null)
			{
				int num3 = (one ? 1 : SlotAmount(BagNow(host), d.Key));
				if (num3 > 0)
				{
					int j = BagOrder.TakeFrom(host, d.Cell, num3, BagNow(host));
					if (_mod.DropResource(text, num3))
					{
						_bagNowFrame = -1;
					}
					else
					{
						BagOrder.Swap(host, d.Cell, j);
					}
				}
			}
			else
			{
				int num4 = BagOrder.PieceOf(d.Key);
				if (num4 >= 0)
				{
					_mod.DropPiece(num4);
				}
			}
		}

		private void PutOnFromBag(Actor host, int index, int cell)
		{
			if (Hotbar.UsesItems(host) && index < Gear.Count(host) && Hotbar.IsWeapon(Gear.Of(host)[index]))
			{
				Hotbar.Hold(host, cell, _mod.Toast);
				Sounds.PlayUI("event:/SFX/Ui/Click");
				return;
			}
			int num = Gear.Count(host);
			if (Gear.PutOn(host, index, _mod.Toast))
			{
				Sounds.PlayUI("event:/SFX/Ui/Click");
				if (Gear.Count(host) < num)
				{
					BagOrder.PieceGone(host, index);
				}
			}
		}

		internal Vector2? BagCellForTest(int i)
		{
			if (i < 0 || i >= _bagCells.Count)
			{
				return null;
			}
			return _bagCells[i].center;
		}

		internal int SlotAmountForTest(Actor host, int i)
		{
			List<string> list = BagOrder.Of(host);
			if (i < 0 || i >= list.Count)
			{
				return 0;
			}
			return SlotAmount(BagNow(host), list[i]);
		}

		internal void DropSlotForTest(Actor host, int i, bool one)
		{
			List<string> list = BagOrder.Of(host);
			if (i >= 0 && i < list.Count)
			{
				_bagNowFrame = -1;
				InventoryDrop(host, new InvDrag
				{
					Cell = i,
					Key = list[i],
					Moving = true
				}, new Vector2(-10000f, -10000f), one);
				_bagNowFrame = -1;
			}
		}

		public void DrawJournal()
		{
			EnsureAssets();
			GameArt.Ensure();
			Actor host = _mod.Host;
			if (host == null)
			{
				Journal.Open = false;
				return;
			}
			float p = GameArt.P;
			bool close;
			Rect rect = MapWindow("Map", out close);
			if (close)
			{
				Journal.Open = false;
				return;
			}
			Vector2 me = Vector2.zero;
			try
			{
				me = host.current_position;
			}
			catch
			{
			}
			float num = Mathf.Round(Mathf.Min(250f * p, rect.width * 0.42f));
			Rect rect2 = new Rect(rect.xMax - num, rect.y, num, rect.height);
			float y = rect2.y;
			float num2 = Mathf.Round(14f * p);
			float num3 = Mathf.Round(3f * p);
			int num4 = Journal.Tabs.Length;
			float num5 = Mathf.Round((rect2.width - (float)(num4 - 1) * num3) / (float)num4);
			for (int i = 0; i < num4; i++)
			{
				string obj2 = Journal.Tabs[i];
				object obj3;
				switch (i)
				{
				default:
					obj3 = "";
					break;
				case 2:
					obj3 = $" ({_mod.Pings.List.Count})";
					break;
				case 0:
					obj3 = $" ({Quests.Log.Count}/{5})";
					break;
				case 1:
					obj3 = $" ({TavernBoard.Dailies.Count})";
					break;
				}
				string label = obj2 + (string)obj3;
				if (GameButton(new Rect(rect2.x + (float)i * (num5 + num3), y, num5, num2), label, Journal.Tab == i, enabled: true, null, 4.6f))
				{
					Journal.Tab = i;
					Journal.Scroll = Vector2.zero;
				}
			}
			y += num2 + 4f * p;
			Rect area = new Rect(rect2.x, y, rect2.width, rect2.yMax - y);
			if (Journal.Tab == 0)
			{
				DrawJournalTasks(area, me);
			}
			else if (Journal.Tab == 1)
			{
				DrawTavernBoard(area, me);
			}
			else if (Journal.Tab == 3)
			{
				DrawJournalStanding(area, me);
			}
			else
			{
				DrawJournalPings(area, me);
			}
			DrawMapPage(new Rect(rect.x, rect.y, rect2.x - Mathf.Round(8f * p) - rect.x, rect.height), host);
		}

		private void DrawJournalTasks(Rect area, Vector2 me)
		{
			float p = GameArt.P;
			List<Quest> log = Quests.Log;
			List<JournalEntry> list = Hub.JournalEntries();
			JournalEntry journalEntry = ((Journal.PickedEntry != null) ? list.Find((JournalEntry e) => e.Key == Journal.PickedEntry) : null);
			Quest quest = ((journalEntry != null) ? null : log.Find((Quest q) => Journal.Is(q, Journal.PickedGiver, Journal.PickedKind)));
			if (quest == null && journalEntry == null && log.Count > 0)
			{
				quest = log[0];
				Journal.PickedGiver = quest.Giver;
				Journal.PickedKind = quest.Kind;
			}
			if (quest == null && journalEntry == null && list.Count > 0)
			{
				journalEntry = list[0];
				Journal.PickedEntry = journalEntry.Key;
			}
			float num = ((quest != null || journalEntry != null) ? Mathf.Round(52f * p) : 0f);
			string text = $"Done {Quests.Completed}  ·  failed {Quests.Failed}";
			GameArt.Label(new Rect(area.x, area.yMax - 8f * p, area.width, 8f * p), text, 3.8f, TextAnchor.MiddleRight, GameArt.Grey);
			GameArt.Label(new Rect(area.x, area.yMax - 8f * p, area.width, 8f * p), Quests.LevelLine(_mod.Host), 3.8f, TextAnchor.MiddleLeft, GameArt.Orange);
			Rect view = new Rect(area.x, area.y, area.width - 9f * p, area.height - num - 12f * p);
			if (log.Count == 0 && list.Count == 0)
			{
				GameArt.Sliced(new Rect(view.x, view.y, area.width, view.height), GameArt.Inner, new Color(1f, 1f, 1f, 0.6f));
				GameArt.Label(new Rect(view.x + 6f * p, view.y + 5f * p, area.width - 12f * p, view.height - 10f * p), "No tasks yet. Ask people \"Any work for me?\" when you talk to them, or read a town's bounty board. You can carry " + 5 + " at a time.", 4.4f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
				return;
			}
			float num2 = Mathf.Round(25f * p);
			float num3 = Mathf.Round(3f * p);
			float contentH = Mathf.Max(view.height, (float)(log.Count + list.Count) * (num2 + num3));
			float num4 = BeginList(view, ref Journal.Scroll, contentH);
			for (int num5 = 0; num5 < log.Count; num5++)
			{
				Quest quest2 = log[num5];
				Rect rect = new Rect(0f, num4 + (float)num5 * (num2 + num3), view.width, num2);
				bool flag = quest2.Stage == 1;
				bool flag2 = quest2 == quest;
				Vector2 at;
				bool flag3 = Quests.Heading(quest2, out at);
				bool flag4 = _listMouseIn && rect.Contains(Event.current.mousePosition);
				if (rect.yMax >= 0f && rect.y <= view.height && Event.current.type == EventType.Repaint)
				{
					GameArt.Sliced(rect, GameArt.Inner, (flag4 || flag2) ? Color.white : new Color(0.9f, 0.9f, 0.9f, 1f));
					Color tint = (flag ? new Color(1f, 0.92f, 0.45f) : (flag2 ? new Color(1f, 1f, 1f, 0.85f) : (flag4 ? new Color(1f, 1f, 1f, 0.55f) : Color.clear)));
					if (tint.a > 0f)
					{
						GameArt.Sliced(rect, GameArt.ButtonBorder, tint, 4f, 4f, 4f, 4f);
					}
					GameArt.Flat(new Rect(rect.x + 3f * p, rect.y + 4f * p, 2f * p, rect.height - 8f * p), flag ? new Color(1f, 0.92f, 0.45f) : (Quests.IsCourt(quest2.Kind) ? new Color(1f, 0.62f, 0.78f) : new Color(0.95f, 0.74f, 0.2f)));
					float num6 = rect.x + 9f * p;
					float num7 = rect.xMax - num6 - 4f * p;
					GameArt.Label(new Rect(num6, rect.y + 1.5f * p, num7 * 0.68f, 9f * p), Quests.Title(quest2), 4.6f, TextAnchor.MiddleLeft, flag ? new Color(1f, 0.92f, 0.45f) : Color.white);
					string text2 = Quests.TimeLeft(quest2);
					if (text2 != null)
					{
						GameArt.Label(new Rect(num6 + num7 * 0.68f, rect.y + 1.5f * p, num7 * 0.32f, 9f * p), text2, 3.8f, TextAnchor.MiddleRight, GameArt.Grey);
					}
					GameArt.Label(new Rect(num6, rect.y + 11.5f * p, num7 * 0.62f, 9f * p), (flag ? "Done! " : "") + Quests.Status(quest2), 3.9f, TextAnchor.MiddleLeft, flag ? new Color(1f, 0.92f, 0.45f) : GameArt.Grey);
					if (flag3)
					{
						bool flag5 = IsMarked(at);
						GameArt.Label(new Rect(num6 + num7 * 0.6f, rect.y + 11.5f * p, num7 * 0.4f, 9f * p), Service.Heading(me, at) + (flag5 ? "  ·  marked" : ""), 3.8f, TextAnchor.MiddleRight, flag5 ? Pings.ColorOf(PingKind.Go) : GameArt.Grey);
					}
				}
				PadNav.Spot(rect);
				if (GUI.Button(rect, GUIContent.none, GUIStyle.none) && _listMouseIn)
				{
					Sounds.PlayUI("event:/SFX/Ui/Click");
					Journal.PickedGiver = quest2.Giver;
					Journal.PickedKind = quest2.Kind;
					Journal.PickedEntry = null;
					Journal.ConfirmGiver = -1L;
				}
			}
			DrawEntryRows(list, journalEntry, num4 + (float)log.Count * (num2 + num3), view, num2, num3, me);
			EndList(view, ref Journal.Scroll, contentH);
			Rect rect2 = new Rect(area.x, view.yMax + 3f * p, area.width, num);
			GameArt.TipPanel(rect2);
			if (journalEntry != null)
			{
				DrawEntryDetail(rect2, journalEntry, me);
				return;
			}
			GameArt.Label(new Rect(rect2.x + 6f * p, rect2.y + 4f * p, rect2.width - 12f * p, rect2.height - 24f * p), Quests.Promise(quest), 4.3f, TextAnchor.UpperLeft, Color.white, wrap: true);
			float num8 = Mathf.Round(13f * p);
			float width = Mathf.Round((rect2.width - 18f * p) / 2f);
			Rect r = new Rect(rect2.x + 6f * p, rect2.yMax - num8 - 4f * p, width, num8);
			Rect r2 = new Rect(r.xMax + 6f * p, r.y, width, num8);
			Vector2 at2;
			bool flag6 = Quests.Heading(quest, out at2);
			bool flag7 = flag6 && IsMarked(at2);
			if (GameButton(r, (!flag6) ? "No way to show" : (flag7 ? "Take the mark off" : "Mark the way"), red: false, flag6, null, 4.4f))
			{
				MarkNews(at2, Quests.Title(quest), me);
			}
			bool flag8 = Journal.ConfirmGiver == quest.Giver && Time.unscaledTime < Journal.ConfirmUntil;
			if (GameButton(r2, flag8 ? "Really give it up?" : "Give it up", flag8, enabled: true, null, 4.4f))
			{
				if (flag8)
				{
					_mod.AbandonFromJournal(quest);
					return;
				}
				Journal.ConfirmGiver = quest.Giver;
				Journal.ConfirmUntil = Time.unscaledTime + 4f;
			}
		}

		private void DrawTavernBoard(Rect area, Vector2 me)
		{
			float p = GameArt.P;
			float y = area.y;
			GameArt.Label(new Rect(area.x, y, area.width, 8f * p), "Tavern Board — daily tasks (refresh 10m):", 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
			y += 9f * p;
			var dailies = TavernBoard.Dailies;
			if (dailies.Count == 0)
			{
				GameArt.Label(new Rect(area.x, y, area.width, 8f * p), "No tasks — wait for refresh or visit a town.", 3.8f, TextAnchor.MiddleLeft, GameArt.Grey);
				return;
			}
			for (int i=0;i<dailies.Count && i<5;i++)
			{
				var d = dailies[i];
				Rect r = new Rect(area.x, y + i*22f*p, area.width - 64f*p, 20f*p);
				GameArt.Sliced(r, GameArt.Inner, new Color(0.95f,0.95f,0.92f));
				GameArt.Label(new Rect(r.x+4f*p, r.y+1f*p, r.width-8f*p, 9f*p), d.Title, 4.0f, TextAnchor.MiddleLeft, Color.black);
				GameArt.Label(new Rect(r.x+4f*p, r.y+10f*p, r.width-8f*p, 7f*p), $"{d.RewardGold}g +{d.RewardRep} rep", 3.4f, TextAnchor.MiddleLeft, GameArt.Grey);
				Rect btn = new Rect(r.xMax+4f*p, r.y+2f*p, 60f*p, 16f*p);
				if (GameButton(btn, "Take", false, true, null, 3.6f))
				{
					TavernBoard.Accept(d.Id, _mod.Host, _mod.Toast);
				}
			}
		}

		private void DrawJournalPings(Rect area, Vector2 me)
		{
			float p = GameArt.P;
			float y = area.y;
			GameArt.Label(new Rect(area.x, y, area.width, 8f * p), "Ping where you're looking:", 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
			y += 9f * p;
			int num = Pings.Wheel.Length;
			float num2 = Mathf.Round(3f * p);
			float num3 = Mathf.Round(14f * p);
			float num4 = Mathf.Round((area.width - (float)(num - 1) * num2) / (float)num);
			for (int i = 0; i < num; i++)
			{
				PingKind pingKind = Pings.Wheel[i];
				Rect r = new Rect(area.x + (float)i * (num4 + num2), y, num4, num3);
				if (GameButton(r, Pings.Name(pingKind), red: false, enabled: true, null, 4f))
				{
					_mod.PingFromJournal(pingKind);
				}
				if (Event.current.type == EventType.Repaint)
				{
					GameArt.Flat(new Rect(r.x + 3f * p, r.yMax - 2.5f * p, r.width - 6f * p, 1.5f * p), Pings.ColorOf(pingKind));
				}
			}
			y += num3 + 5f * p;
			List<Ping> list = _mod.Pings.List;
			float num5 = Mathf.Round(14f * p);
			Rect view = new Rect(area.x, y, area.width - 9f * p, area.yMax - y - num5 - 4f * p);
			if (list.Count == 0)
			{
				GameArt.Sliced(new Rect(view.x, view.y, area.width, view.height), GameArt.Inner, new Color(1f, 1f, 1f, 0.6f));
				GameArt.Label(new Rect(view.x + 6f * p, view.y + 5f * p, area.width - 12f * p, view.height - 10f * p), "No pings. Tap C on something to ping it, or hold C for the ping wheel. A click on the map marks the way. Up to " + 8 + " at a time.", 4.4f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
			}
			else
			{
				float num6 = Mathf.Round(20f * p);
				float contentH = Mathf.Max(view.height, (float)list.Count * (num6 + num2));
				float num7 = BeginList(view, ref Journal.Scroll, contentH);
				Ping ping = null;
				float unscaledTime = Time.unscaledTime;
				int num8 = list.Count - 1;
				int num9 = 0;
				while (num8 >= 0)
				{
					Ping ping2 = list[num8];
					Rect rect = new Rect(0f, num7 + (float)num9 * (num6 + num2), view.width, num6);
					Rect r2 = new Rect(rect.xMax - 40f * p, rect.y + 3.5f * p, 37f * p, rect.height - 7f * p);
					Vector3 vector = Pings.Where(ping2);
					if (rect.yMax >= 0f && rect.y <= view.height && Event.current.type == EventType.Repaint)
					{
						GameArt.Sliced(rect, GameArt.Inner, new Color(0.92f, 0.92f, 0.92f, 1f));
						Color color = Pings.ColorOf(ping2.Kind);
						GameArt.Flat(new Rect(rect.x + 3f * p, rect.y + 4f * p, 2f * p, rect.height - 8f * p), color);
						float num10 = rect.x + 9f * p;
						float width = r2.x - num10 - 4f * p;
						GameArt.Label(new Rect(num10, rect.y + 1.5f * p, width, 8.5f * p), Pings.Name(ping2.Kind) + "  ·  " + PingOn(ping2), 4.4f, TextAnchor.MiddleLeft, color);
						string text = ((ping2.Kind == PingKind.Go) ? "until you get there" : (Mathf.CeilToInt(Mathf.Max(0f, ping2.Until - unscaledTime)) + " s left"));
						GameArt.Label(new Rect(num10, rect.y + 10.5f * p, width, 8f * p), Service.Heading(me, new Vector2(vector.x, vector.y)) + "  ·  " + text, 3.8f, TextAnchor.MiddleLeft, GameArt.Grey);
					}
					if (GameButton(r2, "Take off", red: false, enabled: true, null, 3.8f, _listMouseIn))
					{
						ping = ping2;
					}
					else if (GUI.Button(rect, GUIContent.none, GUIStyle.none) && _listMouseIn)
					{
						Sounds.PlayUI("event:/SFX/Ui/Click");
						Journal.GlideTo = new Vector2(vector.x, vector.y);
					}
					num8--;
					num9++;
				}
				EndList(view, ref Journal.Scroll, contentH);
				if (ping != null)
				{
					_mod.Pings.Remove(ping);
					_mod.Toast(Pings.Name(ping.Kind) + " ping taken off");
				}
			}
			// Bookmarks section
			try
			{
				UserWaypoints.EnsureLoaded();
				var bms = UserWaypoints.All;
				float by = view.yMax + 6f * p;
				float bh = 10f * p;
				GameArt.Label(new Rect(area.x, by, area.width, bh), $"Bookmarks ({bms.Count})  ·  K to add, Shift+K remove nearest:", 4.0f, TextAnchor.MiddleLeft, GameArt.Grey);
				by += bh + 2f * p;
				if (bms.Count == 0)
				{
					GameArt.Label(new Rect(area.x, by, area.width, 8f * p), "No bookmarks yet. Press K where you stand to save a spot.", 3.6f, TextAnchor.MiddleLeft, GameArt.Grey);
				}
				else
				{
					for (int bi = 0; bi < bms.Count && bi < 6; bi++)
					{
						var bm = bms[bi];
						Rect br = new Rect(area.x, by + bi * (9f * p), area.width - 42f * p, 8f * p);
						Color bc = UserWaypoints.ColorOf(bm);
						GameArt.Flat(new Rect(br.x, br.y + 1f * p, 2f * p, br.height - 2f * p), bc);
						Vector2 bAt = new Vector2(bm.X, bm.Y);
						GameArt.Label(new Rect(br.x + 4f * p, br.y, br.width, br.height), bm.Name + "  ·  " + Service.Heading(me, bAt), 3.8f, TextAnchor.MiddleLeft, bc);
						Rect del = new Rect(br.xMax + 4f * p, br.y, 38f * p, br.height);
						if (GameButton(del, "Remove", red: false, enabled: true, null, 3.2f, _listMouseIn))
						{
							UserWaypoints.Remove(bm.Id);
							_mod.Toast("Bookmark removed");
						}
					}
				}
				Rect addR = new Rect(area.x, by + Mathf.Min(6, bms.Count) * 9f * p + 4f * p, 70f * p, 8f * p);
				if (GameButton(addR, "Add here", red: false, enabled: true, null, 3.6f))
				{
					try { var at = _mod.Host != null ? _mod.Host.current_position : Vector2.zero; var nb = UserWaypoints.Add(at); _mod.Toast($"Bookmark \"{nb.Name}\" added"); } catch {}
				}
				if (bms.Count > 0)
				{
					Rect clr = new Rect(addR.xMax + 6f * p, addR.y, 70f * p, 8f * p);
					if (GameButton(clr, "Clear all", red: true, enabled: true, null, 3.6f))
					{
						UserWaypoints.Clear();
						_mod.Toast("All bookmarks cleared");
					}
				}
			}
			catch { }
			Rect r3 = new Rect(area.xMax - 70f * p, area.yMax - num5, 70f * p, num5);
			if (GameButton(r3, "Take all off", red: false, list.Count > 0, null, 4.2f))
			{
				_mod.Pings.Clear();
				_mod.Toast("All pings taken off");
			}
			GameArt.Label(new Rect(area.x, area.yMax - num5, area.width - 76f * p, num5), "Hold C for the ping wheel.", 3.8f, TextAnchor.MiddleLeft, GameArt.Grey);
		}

		private float MapButton(float right, float y, float size, float alpha)
		{
			int num = 0;
			foreach (Quest item in Quests.Log)
			{
				if (item.Stage == 1)
				{
					num++;
				}
			}
			float result = KeyButton(right, y, size, GameArt.IconNamed("iconWorldInfo") ?? GameArt.Warfare, "M", (num > 0) ? num.ToString() : null, alpha);
			if (WorldNews.UnreadMine > 0 && Event.current.type == EventType.Repaint)
			{
				float p = GameArt.P;
				float num2 = Mathf.Round(5f * p);
				Rect r = new Rect(Mathf.Round(right - num2 - 2f * p), Mathf.Round(y + 2f * p), num2, num2);
				Fill(new Rect(r.x - p, r.y - p, num2 + 2f * p, num2 + 2f * p), new Color(0f, 0f, 0f, 0.6f * alpha));
				Fill(r, new Color(0.95f, 0.2f, 0.15f, alpha));
			}
			return result;
		}

		private static string PingOn(Ping p)
		{
			try
			{
				if (p.Actor != null && p.Actor.isAlive())
				{
					return Dialogue.IsPerson(p.Actor) ? Dialogue.NameOf(p.Actor) : p.Actor.asset.id.Replace('_', ' ');
				}
				if (p.On is Building building && building.asset != null)
				{
					return building.asset.id.Replace('_', ' ');
				}
			}
			catch
			{
			}
			return "a place";
		}

		internal void DrawLedger()
		{
			if (_mod.Host == null || !Ledger.Open)
			{
				Ledger.Close();
				return;
			}
			float p = GameArt.P;
			bool close;
			Rect rect = GameWindow(WindowRect(300f, 300f), Ledger.Title, "E", out close);
			if (close)
			{
				Ledger.Close();
				return;
			}
			Rect r = new Rect(rect.x + 4f * p, rect.y, Mathf.Round(rect.width * 0.55f), Mathf.Round(15f * p));
			Ledger.Search = GameTextField(r, Ledger.Search, 24, "ledger_search", 5.5f);
			if (string.IsNullOrEmpty(Ledger.Search) && GUI.GetNameOfFocusedControl() != "ledger_search")
			{
				GameArt.Label(r, "Search names", 5f, TextAnchor.MiddleCenter, GameArt.Grey);
			}
			string text = (Ledger.Royal ? $"{Ledger.People.Count} subjects" : $"{Ledger.People.Count} residents");
			GameArt.Label(new Rect(r.xMax + 6f * p, r.y, rect.xMax - r.xMax - 10f * p, r.height), text, 4.6f, TextAnchor.MiddleRight, GameArt.Grey);
			string text2 = (Ledger.Search ?? "").Trim();
			_ledgerShown.Clear();
			foreach (Ledger.Row person in Ledger.People)
			{
				if (text2.Length == 0 || person.Name.IndexOf(text2, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					_ledgerShown.Add(person);
				}
			}
			bool num = text2.Length == 0 && Ledger.Offices.Count > 0;
			float num2 = 15f;
			float num3 = 18f;
			float num4 = 3f;
			float contentH = ((num ? (num3 + num4 + (float)Ledger.Offices.Count * num2 + num4) : 0f) + num3 + num4 + (float)Math.Max(1, _ledgerShown.Count) * num2) * p;
			Rect view = new Rect(rect.x, r.yMax + 5f * p, rect.width - 8f * p, rect.yMax - r.yMax - 7f * p);
			Vector2 scroll = Ledger.Scroll;
			float num5 = BeginList(view, ref scroll, contentH);
			float width = view.width;
			Ledger.Row row = null;
			if (num)
			{
				SectionTitle(new Rect(0f, num5, width, Mathf.Round(num3 * p)), "Offices");
				num5 += (num3 + num4) * p;
				foreach (Ledger.Row office in Ledger.Offices)
				{
					if (LedgerRow(new Rect(0f, num5, width, num2 * p), view.height, office))
					{
						row = office;
					}
					num5 += num2 * p;
				}
				num5 += num4 * p;
			}
			SectionTitle(new Rect(0f, num5, width, Mathf.Round(num3 * p)), (text2.Length != 0) ? $"Found {_ledgerShown.Count}" : (Ledger.Royal ? "Subjects" : "Residents"));
			num5 += (num3 + num4) * p;
			if (_ledgerShown.Count == 0)
			{
				GameArt.Label(new Rect(0f, num5, width, num2 * p), (text2.Length == 0) ? "No one is listed" : "No one by that name", 5f, TextAnchor.MiddleCenter, GameArt.Grey);
			}
			foreach (Ledger.Row item in _ledgerShown)
			{
				if (LedgerRow(new Rect(0f, num5, width, num2 * p), view.height, item))
				{
					row = item;
				}
				num5 += num2 * p;
			}
			EndList(view, ref scroll, contentH);
			Ledger.Scroll = scroll;
			if (row != null)
			{
				Ledger.Find(row);
			}
		}

		private bool LedgerRow(Rect r, float viewH, Ledger.Row row)
		{
			if (r.yMax < 0f || r.y > viewH)
			{
				return false;
			}
			float p = GameArt.P;
			if (Event.current.type == EventType.Repaint && r.Contains(Event.current.mousePosition))
			{
				GameArt.Flat(r, new Color(1f, 1f, 1f, 0.06f));
			}
			float num = Mathf.Round(36f * p);
			Rect r2 = new Rect(r.x + 4f * p, r.y, Mathf.Round(r.width * 0.42f), r.height);
			GameArt.Label(r2, row.Name, 5.2f, TextAnchor.MiddleLeft, Color.white);
			string text = row.Role ?? "";
			if (Ledger.Royal && row.Town != null)
			{
				text = ((text.Length > 0) ? (text + ", " + row.Town) : row.Town);
			}
			GameArt.Label(new Rect(r2.xMax + 4f * p, r.y, r.xMax - num - r2.xMax - 10f * p, r.height), text, 4.6f, TextAnchor.MiddleLeft, GameArt.Grey);
			Rect r3 = new Rect(r.xMax - num - 2f * p, r.y + 1.5f * p, num, r.height - 3f * p);
			return GameButton(r3, "Find", red: false, enabled: true, null, 4.6f, _listMouseIn);
		}

		private void MagicLists(Actor host)
		{
			float unscaledTime = Time.unscaledTime;
			if (_magicSpells != null && host == _magicFor && unscaledTime < _magicUntil)
			{
				return;
			}
			List<Ability> list = Abilities.Known(host);
			List<string> list2 = TraitSwitch.EarnedOf(host);
			List<ActorTrait> list3 = new List<ActorTrait>();
			try
			{
				foreach (ActorTrait trait in host.getTraits())
				{
					if (trait != null && !list2.Contains(trait.id))
					{
						list3.Add(trait);
					}
				}
			}
			catch
			{
			}
			list3.Sort(ByTraitName);
			List<Moves.Move> list4 = Moves.Known(host);
			List<Ability> magicSpells = list;
			List<string> magicEarned = list2;
			List<ActorTrait> magicBorn = list3;
			List<Moves.Move> magicMoves = list4;
			_magicSpells = magicSpells;
			_magicEarned = magicEarned;
			_magicBorn = magicBorn;
			_magicMoves = magicMoves;
			_magicFor = host;
			_magicUntil = unscaledTime + 0.5f;
		}

		private void MagicChanged()
		{
			_magicUntil = 0f;
		}

		private string DrawMagicPage(Rect area, Actor host)
		{
			float p = GameArt.P;
			Event current = Event.current;
			bool flag = current.type == EventType.Repaint;
			string result = null;
			MagicLists(host);
			List<Ability> magicSpells = _magicSpells;
			Ability ability = _mod.Ability;
			List<string> magicEarned = _magicEarned;
			List<ActorTrait> magicBorn = _magicBorn;
			List<Moves.Move> magicMoves = _magicMoves;
			Moves.Move move = Moves.Ready(host);
			float num = Mathf.Round(11f * p);
			float num2 = Mathf.Round(20f * p);
			float num3 = Mathf.Round(15f * p);
			float num4 = Mathf.Round(2f * p);
			float num5 = Mathf.Round(16f * p);
			float contentH = num + ((magicSpells.Count == 0) ? num5 : ((float)magicSpells.Count * (num2 + num4))) + 4f * p + num + ((magicMoves.Count == 0) ? num5 : ((float)magicMoves.Count * (num2 + num4))) + 4f * p + num + ((magicEarned.Count == 0) ? num5 : ((float)magicEarned.Count * (num3 + num4))) + 4f * p + ((magicBorn.Count > 0) ? (num + (float)magicBorn.Count * (num3 + num4)) : 0f);
			float num6 = BeginList(area, ref _magicScroll, contentH);
			float width = area.width;
			Heading(new Rect(0f, num6, width, num), "Spells", flag);
			num6 += num;
			if (magicSpells.Count == 0)
			{
				if (flag)
				{
					GameArt.Label(new Rect(4f * p, num6, width - 8f * p, num5), "You know no spells. Each raid you hold teaches one; some kinds, traits, faiths and weapons teach them too.", 4.2f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
				}
				num6 += num5;
			}
			foreach (Ability item in magicSpells)
			{
				Rect r = new Rect(0f, num6, width, num2);
				num6 += num2 + num4;
				bool flag2 = item == ability;
				if (flag)
				{
					GameArt.Sliced(r, GameArt.Inner, flag2 ? new Color(0.85f, 1f, 0.85f, 1f) : new Color(0.96f, 0.93f, 0.84f, 1f));
					Texture2D texture2D = AbilityIconTexture(item.Icon);
					if (texture2D != null)
					{
						GUI.DrawTexture(new Rect(r.x + 3f * p, r.y + 3f * p, num2 - 6f * p, num2 - 6f * p), texture2D, ScaleMode.ScaleToFit);
					}
					GameArt.Label(new Rect(r.x + num2 + 1f * p, r.y + 1.5f * p, r.width - num2 - 44f * p, 9f * p), item.Name, 5f, TextAnchor.MiddleLeft, Color.white);
					GameArt.Label(new Rect(r.x + num2 + 1f * p, r.y + 10.5f * p, r.width - num2 - 44f * p, 8f * p), (item.SpellId != null) ? "A spell" : "Your kind's own power", 3.9f, TextAnchor.MiddleLeft, GameArt.Grey);
				}
				Rect r2 = new Rect(r.xMax - 40f * p, r.y + (num2 - 13f * p) * 0.5f, 37f * p, 13f * p);
				if (flag2)
				{
					if (flag)
					{
						StateTag(r2.x + 4f * p, r2.y + 2f * p, "On X", new Color(0.55f, 1f, 0.5f));
					}
				}
				else if (GameButton(r2, "Put on X", red: false, enabled: true, null, 4.4f, _listMouseIn))
				{
					Abilities.Choose(host, item);
					MagicChanged();
					_mod.Toast("X: " + item.Name);
				}
				if (_listMouseIn && r.Contains(current.mousePosition))
				{
					result = item.Name + "\n" + item.Blurb + ((item.Cost > 0) ? string.Format("\nCosts {0} {1}, then {2:F0} s to come back", Abilities.Cost(host, item), item.IsMagic ? "mana" : "stamina", item.Cooldown) : "");
				}
			}
			num6 += 4f * p;
			Heading(new Rect(0f, num6, width, num), "Moves", flag);
			num6 += num;
			if (magicMoves.Count == 0)
			{
				if (flag)
				{
					GameArt.Label(new Rect(4f * p, num6, width - 8f * p, num5), "None yet. Level 2 and 3 quests and big bounties teach them", 4.2f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
				}
				num6 += num5;
			}
			foreach (Moves.Move item2 in magicMoves)
			{
				Rect r3 = new Rect(0f, num6, width, num2);
				num6 += num2 + num4;
				bool flag3 = item2 == move;
				if (flag)
				{
					GameArt.Sliced(r3, GameArt.Inner, flag3 ? new Color(0.85f, 1f, 0.85f, 1f) : new Color(0.96f, 0.93f, 0.84f, 1f));
					GameArt.Label(new Rect(r3.x + 4f * p, r3.y + 1.5f * p, r3.width - 48f * p, 9f * p), Moves.Name(item2), 5f, TextAnchor.MiddleLeft, Color.white);
					GameArt.Label(new Rect(r3.x + 4f * p, r3.y + 10.5f * p, r3.width - 48f * p, 8f * p), Moves.Input(item2), 3.9f, TextAnchor.MiddleLeft, GameArt.Grey);
				}
				Rect r4 = new Rect(r3.xMax - 40f * p, r3.y + (num2 - 13f * p) * 0.5f, 37f * p, 13f * p);
				if (flag3)
				{
					if (GameButton(r4, "Put by", red: false, enabled: true, null, 4.4f, _listMouseIn))
					{
						Moves.SetReady(host, Moves.Move.None);
						MagicChanged();
						_mod.Toast("No move ready");
					}
				}
				else if (GameButton(r4, "Ready it", red: false, enabled: true, null, 4.4f, _listMouseIn))
				{
					Moves.SetReady(host, item2);
					MagicChanged();
					_mod.Toast("Ready: " + Moves.Name(item2) + " (" + Moves.Input(item2) + ")");
				}
				if (_listMouseIn && r3.Contains(current.mousePosition))
				{
					result = Moves.Name(item2) + "\n" + Moves.Blurb(item2) + "\n" + Moves.Input(item2) + (flag3 ? "" : "\nOnly the ready move works");
				}
			}
			num6 += 4f * p;
			Heading(new Rect(0f, num6, width, num), "Traits you earned", flag);
			num6 += num;
			if (magicEarned.Count == 0)
			{
				if (flag)
				{
					GameArt.Label(new Rect(4f * p, num6, width - 8f * p, num5), "None yet. Teachers, level 2 and 3 quests and kings can give you traits; you can switch those on and off here.", 4.2f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
				}
				num6 += num5;
			}
			foreach (string item3 in magicEarned)
			{
				Rect r5 = new Rect(0f, num6, width, num3);
				num6 += num3 + num4;
				bool flag4 = TraitSwitch.IsOn(host, item3);
				ActorTrait t = null;
				try
				{
					t = AssetManager.traits.get(item3);
				}
				catch
				{
				}
				TraitRow(r5, t, item3, flag4 ? 1f : 0.55f, flag);
				if (Switch(new Rect(r5.xMax - 30f * p, r5.y + (num3 - 9f * p) * 0.5f, 28f * p, 9f * p), flag4 ? "On" : "Off", flag4))
				{
					bool flag5 = TraitSwitch.Set(host, item3, !flag4);
					MagicChanged();
					_mod.Toast(flag5 ? (Teachers.Name(item3) + ": " + ((!flag4) ? "on" : "off")) : (Teachers.Name(item3) + " won't switch " + ((!flag4) ? "on" : "off") + " on this creature"));
				}
				if (_listMouseIn && r5.Contains(current.mousePosition))
				{
					result = TraitTip(t, item3);
				}
			}
			num6 += 4f * p;
			if (magicBorn.Count > 0)
			{
				Heading(new Rect(0f, num6, width, num), "Born with", flag);
				num6 += num;
				foreach (ActorTrait item4 in magicBorn)
				{
					Rect r6 = new Rect(0f, num6, width, num3);
					num6 += num3 + num4;
					TraitRow(r6, item4, item4.id, 1f, flag);
					if (flag)
					{
						GameArt.Label(new Rect(r6.xMax - 40f * p, r6.y, 38f * p, r6.height), "born", 3.9f, TextAnchor.MiddleRight, GameArt.Grey);
					}
					if (_listMouseIn && r6.Contains(current.mousePosition))
					{
						result = TraitTip(item4, item4.id);
					}
				}
			}
			EndList(area, ref _magicScroll, contentH);
			return result;
		}

		private static void Heading(Rect r, string text, bool repaint)
		{
			if (repaint)
			{
				float p = GameArt.P;
				float x = GameArt.Measure(text, 4.4f).x;
				GameArt.Label(new Rect(r.x + 2f * p, r.y, x + 2f * p, r.height - 2f * p), text, 4.4f, TextAnchor.MiddleLeft, GameArt.Peach);
				float num = r.x + x + 6f * p;
				float num2 = r.xMax - num - 2f * p;
				if (num2 > 0f)
				{
					GameArt.Flat(new Rect(num, Mathf.Round(r.y + (r.height - 2f * p) * 0.5f), num2, Mathf.Max(1f, Mathf.Round(p * 0.5f))), new Color(1f, 0.74f, 0.4f, 0.35f));
				}
			}
		}

		private static void TraitRow(Rect r, ActorTrait t, string id, float alpha, bool repaint)
		{
			if (repaint)
			{
				float p = GameArt.P;
				GameArt.Sliced(r, GameArt.Inner, new Color(0.96f, 0.93f, 0.84f, alpha));
				Sprite sprite = null;
				try
				{
					sprite = t?.getSprite();
				}
				catch
				{
				}
				if (sprite != null)
				{
					GameArt.Icon(new Rect(r.x + 2.5f * p, r.y + 2.5f * p, r.height - 5f * p, r.height - 5f * p), sprite, new Color(1f, 1f, 1f, alpha));
				}
				GameArt.Label(new Rect(r.x + r.height + 1f * p, r.y, r.width - r.height - 34f * p, r.height), Teachers.Name(id), 4.6f, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, alpha));
			}
		}

		private static string TraitTip(ActorTrait t, string id)
		{
			string text = null;
			try
			{
				text = t?.getTranslatedDescription();
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(text) || text.StartsWith("$"))
			{
				return Teachers.Name(id);
			}
			return Teachers.Name(id) + "\n" + text;
		}

		private void DrawMapPage(Rect frame, Actor host)
		{
			Event current = Event.current;
			bool flag = current.type == EventType.Repaint;
			float p = GameArt.P;
			Minimap minimap = _mod.Minimap;
			Explored.Map hostMap = _mod.HostMap;
			float num = Mathf.Round(11f * p);
			Rect r = new Rect(frame.x, frame.y, frame.width, frame.height - num);
			GameArt.Sliced(r, GameArt.Inner, Color.white);
			Rect mapRect = new Rect(Mathf.Round(r.x + 4f * p), Mathf.Round(r.y + 4f * p), Mathf.Round(r.width - 8f * p), Mathf.Round(r.height - 8f * p));
			MapPageRectForTest = mapRect;
			float worldMax = Mathf.Max(WarMap.WorldWidth, WarMap.WorldHeight) * 1.05f;
			Vector2 vector = Vector2.zero;
			try
			{
				vector = host.current_position;
			}
			catch
			{
			}
			List<JournalEntry> entries = Hub.JournalEntries();
			MpCollectTasks(entries);
			if (Journal.Fit)
			{
				MpFit(vector, hostMap, mapRect, worldMax);
				Journal.Fit = false;
				_mpPicked = MpPickedKey();
			}
			else
			{
				string text = MpPickedKey();
				if (text != _mpPicked)
				{
					_mpPicked = text;
					foreach (MpTask mpTask2 in _mpTasks)
					{
						if (mpTask2.Picked)
						{
							Journal.GlideTo = mpTask2.At;
						}
					}
				}
			}
			if (flag)
			{
				MpMove();
			}
			float across = Journal.Across;
			float ppt = mapRect.width / across;
			float viewH = mapRect.height / ppt;
			Journal.Centre = SmClamp(Journal.Centre, across, viewH);
			float vx0 = Journal.Centre.x - across * 0.5f;
			float vy0 = Journal.Centre.y - viewH * 0.5f;
			if (flag)
			{
				Fill(mapRect, Minimap.FogColor);
				MpTexture(minimap.WorldLand, Mathf.Max(minimap.WorldW, minimap.WorldH), vx0, vy0, across, viewH, ToScreen);
				if (minimap.WorldFog != null)
				{
					MpTexture(minimap.WorldFog, minimap.WorldFog.width * 4, vx0, vy0, across, viewH, ToScreen);
				}
				MpMarks(host, hostMap, mapRect, ToScreen, ppt, vector);
			}
			Vector2 mousePosition = current.mousePosition;
			bool num2 = mapRect.Contains(mousePosition);
			int num3 = -1;
			Ping ping = null;
			if (num2 && !_mpDragged)
			{
				float num4 = 9f * p * 9f * p;
				for (int i = 0; i < _mpTasks.Count; i++)
				{
					float sqrMagnitude = (ToScreen(_mpTasks[i].At) - mousePosition).sqrMagnitude;
					if (sqrMagnitude < num4)
					{
						num4 = sqrMagnitude;
						num3 = i;
					}
				}
				num4 = 9f * p * 9f * p;
				foreach (Ping item in _mod.Pings.List)
				{
					Vector3 vector2 = Pings.Where(item);
					float sqrMagnitude2 = (ToScreen(new Vector2(vector2.x, vector2.y)) - mousePosition).sqrMagnitude;
					if (sqrMagnitude2 < num4)
					{
						num4 = sqrMagnitude2;
						ping = item;
					}
				}
			}
			MpButtons(mapRect, vector, hostMap, worldMax);
			MpInput(mapRect, ToWorld, ppt, current, worldMax, num3, ping);
			if (flag)
			{
				float num5 = ((hostMap != null) ? (Explored.Share(hostMap) * 100f) : 0f);
				string text2 = ((num5 < 1f) ? "Less than 1% of the world explored" : (Mathf.RoundToInt(num5) + "% of the world explored"));
				Rect r2 = new Rect(frame.x + 2f * p, r.yMax + 1f * p, frame.width - 4f * p, num - 1f * p);
				GameArt.Label(r2, text2, 4.4f, TextAnchor.MiddleLeft, GameArt.Peach);
				GameArt.Label(r2, "Click to mark the way   Right click removes a ping   Drag to move", 3.9f, TextAnchor.MiddleRight, GameArt.Grey);
			}
			if (flag && (num3 >= 0 || ping != null))
			{
				string text3;
				string text4;
				Color color;
				if (num3 >= 0)
				{
					MpTask mpTask = _mpTasks[num3];
					text3 = mpTask.Title;
					text4 = mpTask.Line;
					color = mpTask.Color;
				}
				else
				{
					Vector3 vector3 = Pings.Where(ping);
					text3 = Pings.Name(ping.Kind) + "  ·  " + PingOn(ping);
					text4 = Service.Heading(vector, new Vector2(vector3.x, vector3.y)) + "  ·  right click removes it";
					color = Pings.ColorOf(ping.Kind);
				}
				float width = Mathf.Max(GameArt.Measure(text3, 5f).x, GameArt.Measure(text4, 4.2f).x) + 12f * p;
				Rect r3 = new Rect(mousePosition.x + 12f * p, mousePosition.y + 8f * p, width, 21f * p);
				if (r3.xMax > (float)Screen.width - 4f * p)
				{
					r3.x = mousePosition.x - r3.width - 8f * p;
				}
				GameArt.TipPanel(r3);
				GameArt.Label(new Rect(r3.x + 6f * p, r3.y + 1.5f * p, r3.width, 9f * p), text3, 5f, TextAnchor.MiddleLeft, color);
				GameArt.Label(new Rect(r3.x + 6f * p, r3.y + 10.5f * p, r3.width, 8f * p), text4, 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
			}
			Vector2 ToScreen(Vector2 vector4)
			{
				return new Vector2(mapRect.x + (vector4.x - vx0) * ppt, mapRect.y + (vy0 + viewH - vector4.y) * ppt);
			}
			Vector2 ToWorld(Vector2 s)
			{
				return new Vector2(vx0 + (s.x - mapRect.x) / ppt, vy0 + viewH - (s.y - mapRect.y) / ppt);
			}
		}

		private void MpCollectTasks(List<JournalEntry> entries)
		{
			_mpTasks.Clear();
			Vector2 vector = Vector2.zero;
			try
			{
				vector = _mod.Host.current_position;
			}
			catch
			{
			}
			JournalEntry journalEntry = ((Journal.PickedEntry != null) ? entries.Find((JournalEntry e) => e.Key == Journal.PickedEntry) : null);
			foreach (Quest item in Quests.Log)
			{
				if (Quests.Heading(item, out var at))
				{
					bool flag = item.Stage == 1;
					_mpTasks.Add(new MpTask
					{
						At = at,
						Quest = item,
						Title = Quests.Title(item),
						Line = (flag ? "Done! " : "") + Quests.Status(item) + "  ·  " + Service.Heading(vector, at),
						Color = (flag ? ReadyGold : (Quests.IsCourt(item.Kind) ? CourtPink : TaskGold)),
						Picked = (journalEntry == null && Journal.Is(item, Journal.PickedGiver, Journal.PickedKind))
					});
				}
			}
			foreach (JournalEntry entry in entries)
			{
				if (entry != null && entry.HasWay)
				{
					_mpTasks.Add(new MpTask
					{
						At = entry.Way,
						Entry = entry,
						Title = entry.Title,
						Line = (entry.Ready ? "Done! " : "") + entry.Status + "  ·  " + Service.Heading(vector, entry.Way),
						Color = (entry.Ready ? ReadyGold : TaskGold),
						Picked = (entry == journalEntry)
					});
				}
			}
		}

		private static string MpPickedKey()
		{
			if (Journal.PickedEntry == null)
			{
				return Journal.PickedGiver + "/" + Journal.PickedKind;
			}
			return "e" + Journal.PickedEntry;
		}

		private static void MpFit(Vector2 me, Explored.Map known, Rect mapRect, float worldMax)
		{
			float num = mapRect.width / Mathf.Max(1f, mapRect.height);
			Rect r;
			Rect rect = ((known != null && Explored.Bounds(known, out r)) ? r : new Rect(me.x, me.y, 0f, 0f));
			float num2 = Mathf.Min(rect.xMin, me.x);
			float num3 = Mathf.Min(rect.yMin, me.y);
			float num4 = Mathf.Max(rect.xMax, me.x);
			float num5 = Mathf.Max(rect.yMax, me.y);
			float num6 = Mathf.Max(Mathf.Max(num4 - num2, (num5 - num3) * num) + 24f, 64f);
			if (num6 <= 200f)
			{
				Journal.Across = num6;
				Journal.Centre = new Vector2((num2 + num4) * 0.5f, (num3 + num5) * 0.5f);
			}
			else
			{
				Journal.Across = 160f;
				Journal.Centre = me;
			}
			Journal.Across = Mathf.Clamp(Journal.Across, 24f, worldMax);
			Journal.GlideTo = null;
		}

		private void MpMove()
		{
			Vector2 zero = Vector2.zero;
			if (!_mod.IsTypingNow)
			{
				if (PlayInput.GetKey(KeyCode.A) || PlayInput.GetKey(KeyCode.LeftArrow))
				{
					zero.x -= 1f;
				}
				if (PlayInput.GetKey(KeyCode.D) || PlayInput.GetKey(KeyCode.RightArrow))
				{
					zero.x += 1f;
				}
				if (PlayInput.GetKey(KeyCode.W) || PlayInput.GetKey(KeyCode.UpArrow))
				{
					zero.y += 1f;
				}
				if (PlayInput.GetKey(KeyCode.S) || PlayInput.GetKey(KeyCode.DownArrow))
				{
					zero.y -= 1f;
				}
			}
			float num = Mathf.Min(0.1f, Time.unscaledDeltaTime);
			if (zero != Vector2.zero)
			{
				Journal.GlideTo = null;
				Journal.Centre += zero.normalized * Journal.Across * 0.8f * num;
			}
			else if (Journal.GlideTo.HasValue)
			{
				Vector2 value = Journal.GlideTo.Value;
				Vector2 centre = Journal.Centre;
				Journal.Centre = Vector2.Lerp(centre, value, 1f - Mathf.Exp(-9f * num));
				if ((Journal.Centre - value).sqrMagnitude < 0.04f || (Journal.Centre - centre).sqrMagnitude < 0.0001f)
				{
					Journal.Centre = value;
					Journal.GlideTo = null;
				}
			}
		}

		private static void MpTexture(Texture2D tex, float tiles, float vx0, float vy0, float viewW, float viewH, Func<Vector2, Vector2> toScreen)
		{
			if (!(tex == null) && !(tiles <= 0f))
			{
				float num = Mathf.Max(vx0, 0f);
				float num2 = Mathf.Max(vy0, 0f);
				float num3 = Mathf.Min(vx0 + viewW, tiles);
				float num4 = Mathf.Min(vy0 + viewH, tiles);
				if (!(num3 <= num) && !(num4 <= num2))
				{
					Vector2 vector = toScreen(new Vector2(num, num4));
					Vector2 vector2 = toScreen(new Vector2(num3, num2));
					GUI.DrawTextureWithTexCoords(Rect.MinMaxRect(vector.x, vector.y, vector2.x, vector2.y), tex, new Rect(num / tiles, num2 / tiles, (num3 - num) / tiles, (num4 - num2) / tiles));
				}
			}
		}

		private void MpMarks(Actor host, Explored.Map known, Rect mapRect, Func<Vector2, Vector2> toScreen, float ppt, Vector2 me)
		{
			float p = GameArt.P;
			Vector2 origin = new Vector2(mapRect.x, mapRect.y);
			GUI.BeginGroup(mapRect);
			try
			{
				_mapTowns.Clear();
				try
				{
					foreach (City item in World.world.cities.list)
					{
						if (item != null && item.isAlive())
						{
							_mapTowns.Add(item);
						}
					}
				}
				catch
				{
				}
				City city = null;
				try
				{
					city = host.city;
				}
				catch
				{
				}
				float num = Mathf.Round(Mathf.Clamp(ppt * 5f, 9f * p, 15f * p));
				foreach (City mapTown in _mapTowns)
				{
					WorldTile tile;
					try
					{
						tile = mapTown.getTile();
					}
					catch
					{
						continue;
					}
					if (tile == null || (known != null && !Explored.SeenAt(known, (float)tile.x + 0.5f, (float)tile.y + 0.5f)))
					{
						continue;
					}
					Vector2 s = S(new Vector2((float)tile.x + 0.5f, (float)tile.y + 0.5f));
					if (!In(s, 60f * p))
					{
						continue;
					}
					Kingdom kingdom = null;
					try
					{
						kingdom = mapTown.kingdom;
					}
					catch
					{
					}
					bool civ = false;
					try
					{
						civ = kingdom?.isCiv() ?? false;
					}
					catch
					{
					}
					Rect r = new Rect(Mathf.Round(s.x - num * 0.4f), Mathf.Round(s.y - num * 0.85f), Mathf.Round(num * 0.8f), num);
					if (mapTown == city)
					{
						float num2 = num * 1.5f;
						GUI.color = new Color(0.45f, 1f, 0.45f, 0.45f);
						GUI.DrawTexture(new Rect(s.x - num2 * 0.5f, r.center.y - num2 * 0.5f, num2, num2), Skin.DiscTex);
						GUI.color = Color.white;
					}
					if (!DrawBanner(r, kingdom, civ, 1f))
					{
						GameArt.Flat(new Rect(s.x - 3f * p, s.y - 3f * p, 6f * p, 6f * p), new Color(0f, 0f, 0f, 0.7f));
						GameArt.Flat(new Rect(s.x - 2f * p, s.y - 2f * p, 4f * p, 4f * p), (kingdom != null) ? Royal.ColorOf(kingdom) : Color.white);
					}
					Color color = Color.white;
					try
					{
						if (kingdom != null)
						{
							color = kingdom.getColor().getColorText();
						}
					}
					catch
					{
					}
					GameArt.Label(new Rect(s.x - 60f * p, r.yMax, 120f * p, 8f * p), mapTown.name + ((mapTown == city) ? " (home)" : ""), 4f, TextAnchor.UpperCenter, color);
				}
				if (host.kingdom == _mod.WarMap.FlagsKingdom)
				{
					foreach (WarFlag flag in _mod.WarMap.Flags)
					{
						Vector2 s2 = S(flag.P);
						if (In(s2, 10f * p))
						{
							DrawFlagMini(s2, flag, 1f);
						}
					}
				}
				if (_star == null)
				{
					_star = MakeStar();
				}
				Color color2 = GUI.color;
				foreach (MpTask mpTask in _mpTasks)
				{
					Vector2 s3 = S(mpTask.At);
					if (In(s3, 20f * p))
					{
						float num3 = Mathf.Round((mpTask.Picked ? 15f : 11f) * p);
						if (mpTask.Picked)
						{
							float num4 = num3 * 1.7f;
							GUI.color = new Color(mpTask.Color.r, mpTask.Color.g, mpTask.Color.b, 0.3f + 0.12f * Mathf.Sin(Time.unscaledTime * 4f));
							GUI.DrawTexture(new Rect(s3.x - num4 * 0.5f, s3.y - num4 * 0.5f, num4, num4), Skin.DiscTex);
						}
						GUI.color = new Color(mpTask.Color.r, mpTask.Color.g, mpTask.Color.b, 0.98f);
						GUI.DrawTexture(new Rect(Mathf.Round(s3.x - num3 * 0.5f), Mathf.Round(s3.y - num3 * 0.5f), num3, num3), _star);
						GUI.color = color2;
						if (mpTask.Picked)
						{
							GameArt.Label(new Rect(s3.x - 70f * p, s3.y + num3 * 0.5f, 140f * p, 8f * p), mpTask.Title, 4f, TextAnchor.UpperCenter, mpTask.Color);
						}
					}
				}
				GUI.color = color2;
				foreach (Ping item2 in _mod.Pings.List)
				{
					Vector3 vector = Pings.Where(item2);
					Vector2 vector2 = S(new Vector2(vector.x, vector.y));
					if (In(vector2, 10f * p))
					{
						PingIcon(vector2, Mathf.Round(8f * p), item2.Kind, 1f);
					}
				}
				HomeMarkDrawnForTest = null;
				if (HomeAt(host, out var at))
				{
					Vector2 vector3 = S(at);
					if (In(vector3, 10f * p))
					{
						HomeMarkLabelled(vector3, p, label: true);
						HomeMarkDrawnForTest = vector3;
					}
				}
				Vector2 vector4 = S(me);
				float viewYaw = _mod.ViewYaw;
				Line(vector4, vector4 + new Vector2(Mathf.Cos(viewYaw), 0f - Mathf.Sin(viewYaw)) * 12f * p, Mathf.Max(2f, p), new Color(1f, 1f, 1f, 0.95f));
				PixelDiamond(vector4, 2, Color.white);
			}
			finally
			{
				GUI.EndGroup();
			}
			bool In(Vector2 vector5, float m)
			{
				if (vector5.x > 0f - m && vector5.y > 0f - m && vector5.x < mapRect.width + m)
				{
					return vector5.y < mapRect.height + m;
				}
				return false;
			}
			Vector2 S(Vector2 w)
			{
				return toScreen(w) - origin;
			}
		}

		private void MpButtons(Rect mapRect, Vector2 me, Explored.Map known, float worldMax)
		{
			float p = GameArt.P;
			float num = Mathf.Round(16f * p);
			float x = mapRect.xMax - num - 4f * p;
			float num2 = mapRect.y + 4f * p;
			string tip = null;
			Rect tipAt = default(Rect);
			Rect r = new Rect(x, num2, num, num);
			Tip(r, "Zoom in");
			if (GameButton(r, "+", red: false, Journal.Across > 24.01f, null, 7f))
			{
				Journal.GlideTo = null;
				MpZoomAbout(mapRect, mapRect.center, 2f / 3f, worldMax);
			}
			Rect r2 = new Rect(x, num2 + num + 2f * p, num, num);
			Tip(r2, "Zoom out");
			if (GameButton(r2, "−", red: false, Journal.Across < worldMax - 0.01f, null, 7f))
			{
				Journal.GlideTo = null;
				MpZoomAbout(mapRect, mapRect.center, 1.5f, worldMax);
			}
			float num3 = num2 + (num + 2f * p) * 2f + 3f * p;
			Rect r3 = new Rect(x, num3, num, num);
			Tip(r3, "Centre on you");
			if (GameButton(r3, null, red: false, enabled: true, GameArt.IconNamed("iconInspect")))
			{
				Journal.GlideTo = me;
			}
			Rect r4 = new Rect(x, num3 + num + 2f * p, num, num);
			Tip(r4, "The whole world");
			if (GameButton(r4, null, red: false, enabled: true, GameArt.IconNamed("iconWorldInfo")))
			{
				Journal.GlideTo = null;
				Journal.Across = worldMax;
				Journal.Centre = new Vector2(WarMap.WorldWidth * 0.5f, WarMap.WorldHeight * 0.5f);
			}
			if (tip != null && Event.current.type == EventType.Repaint)
			{
				float num4 = GameArt.Measure(tip, 4.6f).x + 10f * p;
				Rect r5 = new Rect(tipAt.x - num4 - 3f * p, tipAt.y + 2f * p, num4, 12f * p);
				GameArt.TipPanel(r5);
				GameArt.Label(r5, tip, 4.6f, TextAnchor.MiddleCenter, Color.white);
			}
			void Tip(Rect rect, string text)
			{
				if (rect.Contains(Event.current.mousePosition))
				{
					tip = text;
					tipAt = rect;
				}
			}
		}

		private static void MpZoomAbout(Rect mapRect, Vector2 at, float factor, float worldMax)
		{
			float num = mapRect.width / Journal.Across;
			float num2 = mapRect.height / num;
			Vector2 vector = new Vector2(Journal.Centre.x - Journal.Across * 0.5f + (at.x - mapRect.x) / num, Journal.Centre.y + num2 * 0.5f - (at.y - mapRect.y) / num);
			Journal.Across = Mathf.Clamp(Journal.Across * factor, 24f, worldMax);
			float num3 = mapRect.width / Journal.Across;
			Journal.Centre = new Vector2(vector.x - (at.x - mapRect.center.x) / num3, vector.y + (at.y - mapRect.center.y) / num3);
		}

		private void MpInput(Rect mapRect, Func<Vector2, Vector2> toWorld, float ppt, Event ev, float worldMax, int hoverTask, Ping hoverPing)
		{
			switch (ev.type)
			{
			case EventType.ScrollWheel:
				if (mapRect.Contains(ev.mousePosition))
				{
					Journal.GlideTo = null;
					MpZoomAbout(mapRect, ev.mousePosition, (ev.delta.y > 0f) ? 1.2f : (5f / 6f), worldMax);
					ev.Use();
				}
				break;
			case EventType.MouseDown:
				if (mapRect.Contains(ev.mousePosition) && ev.button <= 2)
				{
					_mpButton = ev.button;
					_mpDragFrom = ev.mousePosition;
					_mpCentreFrom = Journal.Centre;
					_mpDragged = false;
					ev.Use();
				}
				break;
			case EventType.MouseDrag:
				if (_mpButton >= 0)
				{
					if ((ev.mousePosition - _mpDragFrom).sqrMagnitude > 36f)
					{
						_mpDragged = true;
					}
					if (_mpDragged)
					{
						Journal.GlideTo = null;
						Vector2 vector2 = ev.mousePosition - _mpDragFrom;
						Journal.Centre = _mpCentreFrom - new Vector2(vector2.x / ppt, (0f - vector2.y) / ppt);
					}
					ev.Use();
				}
				break;
			case EventType.MouseUp:
			{
				if (_mpButton < 0)
				{
					break;
				}
				int mpButton = _mpButton;
				_mpButton = -1;
				bool flag = !_mpDragged && ev.button == mpButton && mapRect.Contains(ev.mousePosition);
				if (flag && mpButton == 0)
				{
					if (hoverTask >= 0 && hoverTask < _mpTasks.Count)
					{
						MpTask mpTask = _mpTasks[hoverTask];
						if (mpTask.Entry != null)
						{
							Journal.PickedEntry = mpTask.Entry.Key;
						}
						else
						{
							Journal.PickedEntry = null;
							Journal.PickedGiver = mpTask.Quest.Giver;
							Journal.PickedKind = mpTask.Quest.Kind;
						}
						Journal.ConfirmGiver = -1L;
						if (Journal.Tab != 0)
						{
							Journal.Tab = 0;
							Journal.Scroll = Vector2.zero;
						}
						_mpPicked = MpPickedKey();
						Sounds.PlayUI("event:/SFX/Ui/Click");
					}
					else
					{
						Vector2 vector = toWorld(ev.mousePosition);
						vector = new Vector2(Mathf.Clamp(vector.x, 0.5f, WarMap.WorldWidth - 0.5f), Mathf.Clamp(vector.y, 0.5f, WarMap.WorldHeight - 0.5f));
						_mod.MarkFromMap(vector, (hoverPing != null && hoverPing.Kind == PingKind.Go) ? hoverPing : null);
					}
				}
				else if (flag && mpButton == 1 && hoverPing != null)
				{
					_mod.MarkFromMap(Vector2.zero, hoverPing);
				}
				_mpDragged = false;
				ev.Use();
				break;
			}
			case EventType.MouseMove:
			case EventType.KeyDown:
			case EventType.KeyUp:
				break;
			}
		}

		private Rect MapWindow(string title, out bool close)
		{
			EnsureAssets();
			GameArt.Ensure();
			GameArt.EnsureWindowArt();
			float p = GameArt.P;
			float num = Screen.width;
			float num2 = Screen.height;
			float num3 = CompassBottom + Mathf.Round(4f * p);
			float num4 = Mathf.Round(Mathf.Min(700f * p, num - 70f * p));
			float num5 = Mathf.Round(Mathf.Min(430f * p, num2 - num3 - 6f * p));
			Rect rect = new Rect(Mathf.Round((num - num4) * 0.5f + 12f * p), Mathf.Max(num3, Mathf.Round((num2 - num5) * 0.5f + 4f * p)), num4, num5);
			if (Event.current.type == EventType.Repaint)
			{
				Fill(new Rect(0f, 0f, num, num2), new Color(0.02f, 0.03f, 0.06f, 0.45f));
				DrawCompass(num, num2, _mod.ViewYaw, _mod.CompassMarks());
			}
			MapWindowRectForTest = rect;
			int num6 = DrawMapTabs(rect);
			Rect result = GameWindow(rect, title, "M", out close, dim: false);
			if (num6 >= 0 && Event.current.type == EventType.Repaint)
			{
				string text = MapTabs.Names[(int)_mapPages[num6]];
				float num7 = GameArt.Measure(text, 5.5f).x + 10f * p;
				Rect r = new Rect(Mathf.Round(rect.x - 24f * p - num7), Mathf.Round(rect.y + (46f + (float)num6 * 30f) * p + 6f * p), Mathf.Round(num7), Mathf.Round(14f * p));
				GameArt.TipPanel(r);
				GameArt.Label(r, text, 5.5f, TextAnchor.MiddleCenter, Color.white);
			}
			return result;
		}

		private int DrawMapTabs(Rect win)
		{
			float p = GameArt.P;
			_mapPages.Clear();
			MapTabs.Page[] all = MapTabs.All;
			foreach (MapTabs.Page page in all)
			{
				if (_mod.HasMapPage(page))
				{
					_mapPages.Add(page);
				}
			}
			MapTabs.Page mapPageShown = _mod.MapPageShown;
			int result = -1;
			Vector2 mousePosition = Event.current.mousePosition;
			for (int j = 0; j < _mapPages.Count; j++)
			{
				MapTabs.Page page2 = _mapPages[j];
				bool flag = page2 == mapPageShown;
				Rect r = new Rect(Mathf.Round(win.x - (flag ? 22f : 20f) * p), Mathf.Round(win.y + (46f + (float)j * 30f) * p), Mathf.Round(28f * p), Mathf.Round(27f * p));
				Rect rect = new Rect(r.x, r.y, r.width - 4f * p, r.height);
				switch (page2)
				{
				case MapTabs.Page.War:
					WarMapLinkRectForTest = rect;
					break;
				case MapTabs.Page.Council:
					CouncilLinkRectForTest = rect;
					break;
				}
				bool flag2 = rect.Contains(mousePosition);
				if (flag2)
				{
					result = j;
				}
				if (Event.current.type == EventType.Repaint)
				{
					GameArt.Sliced(r, GameArt.SideTab, (flag2 || flag) ? Color.white : new Color(0.86f, 0.86f, 0.86f, 1f), 7f, 2f, 6f, 6f);
					Rect r2 = new Rect(r.x + 3f * p, r.y + 3f * p, r.width - 8f * p, r.height - 6f * p);
					if (flag)
					{
						GameArt.FlatButton(r2, red: true, null, 5f);
					}
					Sprite s = GameArt.IconNamed(MapTabs.Icons[(int)page2]) ?? GameArt.IconNamed("iconWorldInfo");
					float num = Mathf.Round(18f * p);
					Rect r3 = new Rect(Mathf.Round(r2.center.x - num * 0.5f), Mathf.Round(r2.center.y - num * 0.5f), num, num);
					GameArt.Icon(new Rect(r3.x + p * 0.75f, r3.y + p * 0.75f, num, num), s, new Color(0f, 0f, 0f, 0.45f));
					GameArt.Icon(r3, s, (flag || flag2) ? Color.white : new Color(0.85f, 0.85f, 0.85f, 1f));
				}
				if (Button(rect, GUIContent.none, GUIStyle.none) && !flag)
				{
					_mod.WantMapPage(page2);
				}
			}
			return result;
		}

		private void DrawMarkers(float sw, float sh, Actor host)
		{
			if (Event.current.type != EventType.Repaint)
			{
				return;
			}
			MarksFoe = (MarksFriend = 0);
			int num = ((_mod.Settings != null) ? _mod.Settings.Markers : 0);
			if (num <= 0 || host == null || !host.isAlive())
			{
				_marked.Clear();
				return;
			}
			float unscaledTime = Time.unscaledTime;
			if (unscaledTime - _markedAt > 0.25f || host != _markedFor || num != _markedMode)
			{
				FindMarked(host, num, unscaledTime);
			}
			if (_marked.Count == 0)
			{
				return;
			}
			if (_markTex == null)
			{
				_markTex = MakeMark();
			}
			FrameProjection shown = _mod.Shown;
			if (shown.Width <= 0 || shown.Height <= 0)
			{
				return;
			}
			float u = Skin.U;
			float num2 = shown.Focal / (float)shown.Height * sh;
			Actor speaker = _mod.Talk.Speaker;
			Color color = GUI.color;
			foreach (KeyValuePair<Actor, bool> item in _marked)
			{
				Actor key = item.Key;
				if (key == speaker || Starred(key) || !_mod.StarHead(key, out var head))
				{
					continue;
				}
				float num3 = Mathf.Sin(unscaledTime * 3f + (float)(key.data.id % 11) * 0.6f) * 0.03f;
				if (!shown.Project(head.x, head.y, head.z + 0.12f + num3, out var col, out var row, out var depth, out var along) || col < 0f || col > (float)shown.Width || row < 0f || row > (float)shown.Height)
				{
					continue;
				}
				float num4 = 1f - Mathf.Clamp01((depth - 30f) / 10f);
				if (!(num4 <= 0f))
				{
					float num5 = Mathf.Clamp(0.24f * num2 / along, 13f * u, 28f * u);
					float num6 = col / (float)shown.Width * sw;
					float num7 = row / (float)shown.Height * sh;
					bool value = item.Value;
					GUI.color = (value ? new Color(1f, 0.27f, 0.22f, 0.95f * num4) : (FamilyFollow.IsFamily(key) ? new Color(0.45f, 0.95f, 0.4f, 0.9f * num4) : new Color(0.35f, 0.7f, 1f, 0.85f * num4)));
					GUI.DrawTexture(new Rect(num6 - num5 * 0.5f, num7 - num5, num5, num5), _markTex);
					if (value)
					{
						MarksFoe++;
					}
					else
					{
						MarksFriend++;
					}
				}
			}
			GUI.color = color;
		}

		private bool Starred(Actor a)
		{
			foreach (KeyValuePair<Actor, bool> item in _starred)
			{
				if (item.Key == a)
				{
					return true;
				}
			}
			return false;
		}

		private void FindMarked(Actor host, int mode, float now)
		{
			_markedAt = now;
			_markedFor = host;
			_markedMode = mode;
			_marked.Clear();
			try
			{
				Vector2 current_position = host.current_position;
				List<Actor> simpleList = World.world.units.getSimpleList();
				for (int i = 0; i < simpleList.Count; i++)
				{
					Actor actor = simpleList[i];
					if (actor == null || actor == host || !actor.isAlive())
					{
						continue;
					}
					float num = actor.current_position.x - current_position.x;
					float num2 = actor.current_position.y - current_position.y;
					if (!(num * num + num2 * num2 > 1600f))
					{
						if (FriendlyFire.Foe(host, actor))
						{
							_marked.Add(new KeyValuePair<Actor, bool>(actor, value: true));
						}
						else if (mode >= 2 && (FriendlyFire.Friendly(host, actor) || FamilyFollow.IsFamily(actor)))
						{
							_marked.Add(new KeyValuePair<Actor, bool>(actor, value: false));
						}
					}
				}
			}
			catch
			{
			}
		}

		private static Texture2D MakeMark()
		{
			Vector2[] v = new Vector2[4]
			{
				new Vector2(24f, 3.84f),
				new Vector2(45.12f, 43.199997f),
				new Vector2(24f, 31.68f),
				new Vector2(2.8799999f, 43.199997f)
			};
			float num = 3.84f;
			Color32[] array = new Color32[2304];
			for (int i = 0; i < 48; i++)
			{
				for (int j = 0; j < 48; j++)
				{
					float num2 = PolygonDistance(new Vector2((float)j + 0.5f, (float)i + 0.5f), v);
					float num3 = Mathf.Clamp01(0.5f - num2);
					if (num3 <= 0f)
					{
						array[i * 48 + j] = new Color32(0, 0, 0, 0);
						continue;
					}
					Color color = ((0f - num2 < num) ? new Color(0.12f, 0.08f, 0.06f) : (Color.white * Mathf.Lerp(0.78f, 1f, (float)i / 48f)));
					color.a = num3;
					array[i * 48 + j] = color;
				}
			}
			Texture2D texture2D = new Texture2D(48, 48, TextureFormat.RGBA32, mipChain: true);
			texture2D.filterMode = FilterMode.Trilinear;
			texture2D.wrapMode = TextureWrapMode.Clamp;
			texture2D.hideFlags = HideFlags.HideAndDontSave;
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: true);
			return texture2D;
		}

		private bool MinimapShows(float sw, float sh, out Rect r)
		{
			r = default(Rect);
			if (!_mod.Settings.Minimap || _mod.Minimap.Land == null)
			{
				return false;
			}
			if (_mod.Host == null || _mod.DeathTimer >= 0f || _mod.LeavingDead)
			{
				return false;
			}
			if (_mod.Binoculars.Lens > 0f || _mod.BigMapOpen)
			{
				return false;
			}
			if (_mod.Converse.Open || Law.BoardOpen || _mod.Craft.Open || _mod.Royal.Open || _mod.WarMap.Open || _mod.SoldierMap.Open || _mod.FamilyMap.Open || Journal.Open)
			{
				return false;
			}
			float p = GameArt.P;
			float num = Mathf.Round(78f * p);
			r = new Rect(Mathf.Round(sw - num - 6f * p), Mathf.Round(6f * p), num, num);
			return true;
		}

		internal float MinimapBottom(float sw, float sh)
		{
			if (!MinimapShows(sw, sh, out var r))
			{
				return 0f;
			}
			return r.yMax + 2f * GameArt.P;
		}

		internal float TopRightEdge(float sw, float sh)
		{
			if (!MinimapShows(sw, sh, out var r))
			{
				return sw - 6f * GameArt.P;
			}
			return r.x - 4f * GameArt.P;
		}

		private void DrawMinimap(float sw, float sh)
		{
			MinimapDrawn = false;
			if (Event.current.type != EventType.Repaint || !MinimapShows(sw, sh, out var r))
			{
				return;
			}
			Skin.Ensure();
			Minimap minimap = _mod.Minimap;
			Actor host = _mod.Host;
			float p = GameArt.P;
			float enter = _enter;
			Vector2 centre = r.center;
			float num = 36f * p;
			float minimapReach = _mod.MinimapReach;
			float ppt = num / minimapReach;
			Vector2 me = host.current_position;
			float viewYaw = _mod.ViewYaw;
			Vector2 fwd = new Vector2(Mathf.Cos(viewYaw), Mathf.Sin(viewYaw));
			Vector2 right = new Vector2(Mathf.Sin(viewYaw), 0f - Mathf.Cos(viewYaw));
			float num2 = Mathf.Clamp01(enter * 1.5f - 0.3f);
			if (num2 <= 0f)
			{
				return;
			}
			Fan(minimap.Land, centre, num + 0.8f * p, me, fwd, right, minimapReach * (num + 0.8f * p) / num, minimap.TexX, minimap.TexY, minimap.TexSize, num2);
			if (minimap.Fog != null)
			{
				Fan(minimap.Fog, centre, num + 0.8f * p, me, fwd, right, minimapReach * (num + 0.8f * p) / num, minimap.TexX, minimap.TexY, minimap.Fog.width * 4, num2);
			}
			Color color = GUI.color;
			GUI.color = new Color(1f, 1f, 1f, num2);
			float num3 = Mathf.Max(2f, Mathf.Round(1.6f * p));
			foreach (KeyValuePair<Vector2, Color> minimapDot in _mod.MinimapDots)
			{
				Vector2 vector = ToMap(minimapDot.Key);
				if (!((vector - centre).sqrMagnitude > (num - num3) * (num - num3)))
				{
					GameArt.Flat(new Rect(Mathf.Round(vector.x - num3 * 0.5f) - 1f, Mathf.Round(vector.y - num3 * 0.5f) - 1f, num3 + 2f, num3 + 2f), new Color(0f, 0f, 0f, 0.7f * num2));
					GameArt.Flat(new Rect(Mathf.Round(vector.x - num3 * 0.5f), Mathf.Round(vector.y - num3 * 0.5f), num3, num3), new Color(minimapDot.Value.r, minimapDot.Value.g, minimapDot.Value.b, num2));
				}
			}
			float unscaledTime = Time.unscaledTime;
			foreach (Ping item in _mod.Pings.List)
			{
				Vector3 vector2 = Pings.Where(item);
				Vector2 vector3 = ToMap(new Vector2(vector2.x, vector2.y));
				Vector2 vector4 = vector3 - centre;
				float num4 = num - 4.5f * p;
				bool flag = vector4.magnitude > num4;
				if (flag)
				{
					vector3 = centre + vector4.normalized * num4;
				}
				float num5 = 1f + 0.5f * Mathf.Clamp01(1f - (unscaledTime - item.Born) / 0.5f);
				PingIcon(vector3, Mathf.Round(9f * p * num5), item.Kind, flag ? (0.75f * num2) : num2);
			}
			try
			{
				UserWaypoints.EnsureLoaded();
				foreach (var bm in UserWaypoints.All)
				{
					Vector2 bPos = new Vector2(bm.X, bm.Y);
					Vector2 bMap = ToMap(bPos);
					Vector2 d = bMap - centre;
					float rim = num - 4.5f * p;
					bool atRim = d.magnitude > rim;
					if (atRim) bMap = centre + d.normalized * rim;
					Color bc = UserWaypoints.ColorOf(bm);
					// small diamond for bookmark
					float s = Mathf.Round(6f * p);
					GUI.color = new Color(bc.r, bc.g, bc.b, atRim ? 0.7f*num2 : num2);
					GameArt.Flat(new Rect(bMap.x - s*0.5f, bMap.y - s*0.5f, s, s), bc);
					GUI.color = color;
				}
			} catch {}
			if (HomeAt(host, out var at))
			{
				Vector2 vector5 = ToMap(at);
				Vector2 vector6 = vector5 - centre;
				float num6 = num - 4.5f * p;
				bool flag2 = vector6.magnitude > num6;
				if (flag2)
				{
					vector5 = centre + vector6.normalized * num6;
				}
				HomeMark(vector5, Mathf.Round(7f * p), flag2 ? (0.75f * num2) : num2);
			}
			DrawHuntMarks(ToMap, null, centre, num, Mathf.Round(8f * p), num2, rim: true);
			DrawPartyMini(ToMap, centre, num, num2);
			if (host.kingdom == _mod.WarMap.FlagsKingdom)
			{
				foreach (WarFlag flag4 in _mod.WarMap.Flags)
				{
					Vector2 vector7 = ToMap(flag4.P);
					Vector2 vector8 = vector7 - centre;
					float num7 = num - 4.5f * p;
					bool flag3 = vector8.magnitude > num7;
					if (flag3)
					{
						vector7 = centre + vector8.normalized * num7;
					}
					DrawFlagMini(vector7 + new Vector2(0f, 3f * p), flag4, flag3 ? 0.7f : 1f);
				}
			}
			GUI.color = color;
			if (_mmRing == null)
			{
				_mmRing = MakeRing();
			}
			GUI.color = new Color(1f, 1f, 1f, num2);
			GUI.DrawTexture(r, _mmRing);
			GUI.color = color;
			Vector2 vector9 = new Vector2(Vector2.Dot(Vector2.up, right), 0f - Vector2.Dot(Vector2.up, fwd));
			Vector2 vector10 = centre + vector9 * (num + 1.5f * p);
			float num8 = Mathf.Round(9f * p);
			GUI.color = new Color(0.08f, 0.08f, 0.09f, 0.95f * num2);
			GUI.DrawTexture(new Rect(vector10.x - num8 * 0.5f, vector10.y - num8 * 0.5f, num8, num8), Skin.DiscTex);
			GUI.color = color;
			GameArt.Label(new Rect(vector10.x - num8, vector10.y - num8 * 0.5f - 0.5f * p, num8 * 2f, num8), "N", 4.6f, TextAnchor.MiddleCenter, new Color(1f, 0.42f, 0.3f, num2));
			PixelArrow(centre, Mathf.Max(1f, Mathf.Round(p)), 0f, num2);
			MinimapDrawn = true;
			MinimapRectForTest = r;
			Vector2 ToMap(Vector2 w)
			{
				Vector2 lhs = w - me;
				return new Vector2(centre.x + Vector2.Dot(lhs, right) * ppt, centre.y - Vector2.Dot(lhs, fwd) * ppt);
			}
		}

		private void Fan(Texture2D tex, Vector2 centre, float radius, Vector2 me, Vector2 fwd, Vector2 right, float reach, float x0, float y0, float size, float alpha)
		{
			Material material = MinimapMaterial();
			if (tex == null || material == null || size <= 0f)
			{
				return;
			}
			material.mainTexture = tex;
			material.SetPass(0);
			GL.PushMatrix();
			GL.LoadPixelMatrix(0f, Screen.width, Screen.height, 0f);
			GL.MultMatrix(GUI.matrix);
			GL.Begin(4);
			GL.Color(new Color(0.5f, 0.5f, 0.5f, 0.5f * alpha));
			Vector2 vector = (me - new Vector2(x0, y0)) / size;
			Vector2 vector2 = default(Vector2);
			Vector2 vector3 = default(Vector2);
			for (int i = 0; i <= 72; i++)
			{
				float f = (float)i * MathF.PI * 2f / 72f;
				float num = Mathf.Cos(f);
				float num2 = Mathf.Sin(f);
				Vector2 vector4 = new Vector2(centre.x + num * radius, centre.y - num2 * radius);
				Vector2 vector5 = (me + right * (num * reach) + fwd * (num2 * reach) - new Vector2(x0, y0)) / size;
				if (i > 0)
				{
					GL.TexCoord2(vector.x, vector.y);
					GL.Vertex3(centre.x, centre.y, 0f);
					GL.TexCoord2(vector3.x, vector3.y);
					GL.Vertex3(vector2.x, vector2.y, 0f);
					GL.TexCoord2(vector5.x, vector5.y);
					GL.Vertex3(vector4.x, vector4.y, 0f);
				}
				vector2 = vector4;
				vector3 = vector5;
			}
			GL.End();
			GL.PopMatrix();
		}

		private Material MinimapMaterial()
		{
			if (_mmMaterialTried)
			{
				return _mmMaterial;
			}
			_mmMaterialTried = true;
			Shader shader = Shader.Find("Hidden/Internal-GUITexture");
			if (shader == null)
			{
				Log.Error("minimap: no GUI texture shader");
			}
			else
			{
				_mmMaterial = new Material(shader)
				{
					name = "FirstPersonMinimap",
					hideFlags = HideFlags.HideAndDontSave
				};
			}
			return _mmMaterial;
		}

		private static Texture2D MakeRing()
		{
			int num = Mathf.RoundToInt(78f);
			float num2 = (float)num * 0.5f;
			Color32[] array = new Color32[num * num];
			for (int i = 0; i < num; i++)
			{
				for (int j = 0; j < num; j++)
				{
					float num3 = (float)j + 0.5f - num2;
					float num4 = (float)i + 0.5f - num2;
					float num5 = Mathf.Sqrt(num3 * num3 + num4 * num4);
					Color color;
					if (num5 >= 39f)
					{
						color = Color.clear;
					}
					else if (num5 >= 38f)
					{
						color = new Color(0.07f, 0.07f, 0.08f, 1f);
					}
					else if (num5 >= 36f)
					{
						float num6 = (0f - num3 + num4) / Mathf.Max(1f, num5);
						float num7 = ((num5 < 37f) ? (0.52f + 0.14f * num6) : (0.42f + 0.1f * num6));
						color = new Color(num7, num7 * 1.03f, num7 * 0.94f, 1f);
					}
					else
					{
						color = ((!(num5 >= 35f)) ? Color.clear : new Color(0f, 0f, 0f, 0.5f));
					}
					array[i * num + j] = color;
				}
			}
			Texture2D texture2D = new Texture2D(num, num, TextureFormat.RGBA32, mipChain: false);
			texture2D.filterMode = FilterMode.Point;
			texture2D.wrapMode = TextureWrapMode.Clamp;
			texture2D.hideFlags = HideFlags.HideAndDontSave;
			texture2D.name = "FirstPersonMinimapRing";
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false);
			return texture2D;
		}

		private static void PixelArrow(Vector2 c, float p, float turn, float alpha)
		{
			Matrix4x4 matrix = GUI.matrix;
			if (turn != 0f)
			{
				GUIUtility.RotateAroundPivot(turn, c);
			}
			int num = ArrowRows.Length;
			int length = ArrowRows[0].Length;
			float num2 = Mathf.Round(c.x - (float)length * p * 0.5f);
			float num3 = Mathf.Round(c.y - (float)num * p * 0.5f);
			for (int i = 0; i < num; i++)
			{
				for (int j = 0; j < length; j++)
				{
					char c2 = ArrowRows[i][j];
					if (c2 != '.')
					{
						GameArt.Flat(new Rect(num2 + (float)j * p, num3 + (float)i * p, p, p), (c2 == 'K') ? new Color(0.05f, 0.05f, 0.06f, alpha) : new Color(1f, 1f, 1f, alpha));
					}
				}
			}
			GUI.matrix = matrix;
		}

		private void PingIcon(Vector2 c, float size, PingKind kind, float alpha)
		{
			Color color = Pings.ColorOf(kind);
			Color color2 = GUI.color;
			float num = Mathf.Round(size * 1.25f);
			float num2 = Mathf.Round(size * 1.42f);
			GUI.color = new Color(0f, 0f, 0f, 0.55f * alpha);
			GUI.DrawTexture(new Rect(c.x - num2 * 0.5f + 1f, c.y - num2 * 0.5f + 1f, num2, num2), Skin.DiscTex);
			GUI.color = new Color(color.r, color.g, color.b, alpha);
			GUI.DrawTexture(new Rect(c.x - num2 * 0.5f, c.y - num2 * 0.5f, num2, num2), Skin.DiscTex);
			GUI.color = new Color(0.1f, 0.09f, 0.08f, 0.92f * alpha);
			GUI.DrawTexture(new Rect(c.x - num * 0.5f, c.y - num * 0.5f, num, num), Skin.DiscTex);
			GUI.color = color2;
			Sprite sprite = GameArt.IconNamed(Pings.Icon(kind));
			Rect r = new Rect(Mathf.Round(c.x - size * 0.5f), Mathf.Round(c.y - size * 0.5f), size, size);
			if (sprite != null)
			{
				GameArt.Icon(r, sprite, new Color(1f, 1f, 1f, alpha));
			}
			else
			{
				GameArt.Flat(new Rect(c.x - size * 0.25f, c.y - size * 0.25f, size * 0.5f, size * 0.5f), new Color(color.r, color.g, color.b, alpha));
			}
		}

		private Rect BigMapView(Explored.Map map, Actor host, float world)
		{
			Vector2 current_position = host.current_position;
			Rect r;
			Rect rect = (Explored.Bounds(map, out r) ? r : new Rect(current_position.x, current_position.y, 0f, 0f));
			float num = Mathf.Min(rect.xMin, current_position.x);
			float num2 = Mathf.Min(rect.yMin, current_position.y);
			float num3 = Mathf.Max(rect.xMax, current_position.x);
			float num4 = Mathf.Max(rect.yMax, current_position.y);
			foreach (Ping item in _mod.Pings.List)
			{
				Vector3 vector = Pings.Where(item);
				num = Mathf.Min(num, vector.x);
				num2 = Mathf.Min(num2, vector.y);
				num3 = Mathf.Max(num3, vector.x);
				num4 = Mathf.Max(num4, vector.y);
			}
			float num5 = Mathf.Min(world, Mathf.Max(64f, Mathf.Max(num3 - num, num4 - num2) + 24f));
			float num6 = Mathf.Clamp((num + num3) * 0.5f, num5 * 0.5f, world - num5 * 0.5f);
			float num7 = Mathf.Clamp((num2 + num4) * 0.5f, num5 * 0.5f, world - num5 * 0.5f);
			return new Rect(num6 - num5 * 0.5f, num7 - num5 * 0.5f, num5, num5);
		}

		private void DrawBigMap(float sw, float sh)
		{
			BigMapDrawn = false;
			if (!_mod.BigMapOpen || Event.current.type != EventType.Repaint)
			{
				return;
			}
			Minimap minimap = _mod.Minimap;
			Actor host = _mod.Host;
			Explored.Map hostMap = _mod.HostMap;
			if (host == null || minimap.WorldLand == null)
			{
				return;
			}
			float p = GameArt.P;
			Skin.Ensure();
			string text = null;
			try
			{
				text = host.getName();
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(text))
			{
				text = WorldBoxMod.Describe(host);
			}
			bool close;
			Rect rect = GameWindow(WindowRect(330f, 340f), text + "'s map", null, out close);
			float num = Mathf.Round(12f * p);
			float num2 = Mathf.Floor(Mathf.Min(rect.width, rect.height - num));
			Rect r = new Rect(Mathf.Round(rect.center.x - num2 * 0.5f), rect.y, num2, num2);
			GameArt.Sliced(r, GameArt.Inner, Color.white);
			Rect area = new Rect(r.x + Mathf.Round(4f * p), r.y + Mathf.Round(4f * p), r.width - Mathf.Round(8f * p), r.height - Mathf.Round(8f * p));
			float num3 = Mathf.Max(minimap.WorldW, minimap.WorldH);
			Rect view = BigMapView(hostMap, host, num3);
			float ppt = area.width / view.width;
			GUI.DrawTextureWithTexCoords(area, minimap.WorldLand, new Rect(view.x / num3, view.y / num3, view.width / num3, view.height / num3));
			if (minimap.WorldFog != null)
			{
				float num4 = minimap.WorldFog.width * 4;
				GUI.DrawTextureWithTexCoords(area, minimap.WorldFog, new Rect(view.x / num4, view.y / num4, view.width / num4, view.height / num4));
			}
			_mapTowns.Clear();
			try
			{
				foreach (City item in World.world.cities.list)
				{
					if (item != null && item.isAlive())
					{
						_mapTowns.Add(item);
					}
				}
			}
			catch
			{
			}
			City city = null;
			try
			{
				city = host.city;
			}
			catch
			{
			}
			float num5 = Mathf.Round(Mathf.Clamp(ppt * 5f, 9f * p, 14f * p));
			foreach (City mapTown in _mapTowns)
			{
				WorldTile tile;
				try
				{
					tile = mapTown.getTile();
				}
				catch
				{
					continue;
				}
				if (tile == null || !Explored.SeenAt(hostMap, (float)tile.x + 0.5f, (float)tile.y + 0.5f))
				{
					continue;
				}
				Vector2 s = ToMap(new Vector2((float)tile.x + 0.5f, (float)tile.y + 0.5f));
				if (!Shows(s))
				{
					continue;
				}
				Kingdom kingdom = null;
				try
				{
					kingdom = mapTown.kingdom;
				}
				catch
				{
				}
				bool civ = false;
				try
				{
					civ = kingdom?.isCiv() ?? false;
				}
				catch
				{
				}
				Rect r2 = new Rect(Mathf.Round(s.x - num5 * 0.4f), Mathf.Round(s.y - num5 * 0.85f), Mathf.Round(num5 * 0.8f), num5);
				if (mapTown == city)
				{
					float num6 = num5 * 1.5f;
					GUI.color = new Color(0.45f, 1f, 0.45f, 0.45f);
					GUI.DrawTexture(new Rect(s.x - num6 * 0.5f, r2.center.y - num6 * 0.5f, num6, num6), Skin.DiscTex);
					GUI.color = Color.white;
				}
				if (!DrawBanner(r2, kingdom, civ, 1f))
				{
					GameArt.Flat(new Rect(s.x - 3f * p, s.y - 3f * p, 6f * p, 6f * p), new Color(0f, 0f, 0f, 0.7f));
					GameArt.Flat(new Rect(s.x - 2f * p, s.y - 2f * p, 4f * p, 4f * p), (kingdom != null) ? Royal.ColorOf(kingdom) : Color.white);
				}
				Color color = Color.white;
				try
				{
					if (kingdom != null)
					{
						color = kingdom.getColor().getColorText();
					}
				}
				catch
				{
				}
				string text2 = mapTown.name + ((mapTown == city) ? " (home)" : "");
				GameArt.Label(new Rect(s.x - 60f * p, r2.yMax, 120f * p, 8f * p), text2, 4f, TextAnchor.UpperCenter, color);
			}
			foreach (Ping item2 in _mod.Pings.List)
			{
				Vector3 vector = Pings.Where(item2);
				Vector2 vector2 = ToMap(new Vector2(vector.x, vector.y));
				if (Shows(vector2))
				{
					PingIcon(vector2, Mathf.Round(8f * p), item2.Kind, 1f);
				}
			}
			DrawHuntMarks(ToMap, Shows, Vector2.zero, 0f, Mathf.Round(7f * p), 1f, rim: false);
			if (host.kingdom == _mod.WarMap.FlagsKingdom)
			{
				foreach (WarFlag flag in _mod.WarMap.Flags)
				{
					Vector2 s2 = ToMap(flag.P);
					if (Shows(s2))
					{
						DrawFlagMini(s2, flag, 1f);
					}
				}
			}
			if (HomeAt(host, out var at))
			{
				Vector2 s3 = ToMap(at);
				if (Shows(s3))
				{
					HomeMarkLabelled(s3, p, label: true);
				}
			}
			Vector2 vector3 = ToMap(host.current_position);
			float viewYaw = _mod.ViewYaw;
			Line(vector3, vector3 + new Vector2(Mathf.Cos(viewYaw), 0f - Mathf.Sin(viewYaw)) * 12f * p, Mathf.Max(2f, p), new Color(1f, 1f, 1f, 0.95f));
			PixelDiamond(vector3, 2, Color.white);
			float num7 = Explored.Share(hostMap) * 100f;
			string text3 = ((num7 < 1f) ? "Less than 1% of the world explored" : (Mathf.RoundToInt(num7) + "% of the world explored"));
			GameArt.Label(new Rect(r.x, r.yMax + 1f * p, r.width * 0.6f, num), text3, 4.8f, TextAnchor.MiddleLeft, GameArt.Peach);
			GameArt.Label(new Rect(r.x + r.width * 0.4f, r.yMax + 1f * p, r.width * 0.6f, num), "let go of N to close", 4.2f, TextAnchor.MiddleRight, GameArt.Grey);
			BigMapDrawn = true;
			bool Shows(Vector2 point)
			{
				return area.Contains(point);
			}
			Vector2 ToMap(Vector2 w)
			{
				return new Vector2(area.x + (w.x - view.x) * ppt, area.yMax - (w.y - view.y) * ppt);
			}
		}

		private float MultiplayerPageHeight(float cw)
		{
			return MpPage(0f, 0f, cw, draw: false, null);
		}

		private float DrawMultiplayerPage(float cx, float y, float cw, SettingsMenu menu)
		{
			MpUi.RefreshIfStale();
			return MpPage(cx, y, cw, draw: true, menu);
		}

		private float MpPage(float cx, float y, float cw, bool draw, SettingsMenu menu)
		{
			float p = GameArt.P;
			Vector2 vector = (draw ? Event.current.mousePosition : Vector2.zero);
			MpPrefs prefs = MpPrefs.Current;
			NetSession current = NetSession.Current;
			float num = Mathf.Round(16f * p);
			float num2 = 3f * p;
			bool steamOn = NetSession.SteamOn;
			if (current != null)
			{
				float num3 = SessionCard(cx, y, cw, draw: false, vector, current) - y;
				if (draw)
				{
					GameArt.Sliced(new Rect(cx, y, cw, num3), GameArt.Inner, new Color(1f, 1f, 1f, 0.56f));
					SessionCard(cx, y, cw, draw: true, vector, current);
				}
				y += num3 + num2 * 2f;
			}
			y = Heading(cx, y, cw, draw, "Your name");
			if (draw)
			{
				GameArt.Label(new Rect(cx + 2f * p, y, cw * 0.4f, num), "Shown over your head", 4.4f, TextAnchor.MiddleLeft, GameArt.Grey);
				if (RawField(new Rect(cx + cw * 0.4f, y, cw * 0.6f - 2f * p, num), prefs.Nickname, 20, "mp_name", 5.2f, out var typed))
				{
					prefs.Nickname = MpPrefs.Clean(typed, 20);
					prefs.Save();
				}
			}
			y += num + num2;
			y = MpChoice(cx, y, cw, draw, vector, "Voice chat (Steam)", new string[2] { "On", "Off" }, (!prefs.Voice) ? 1 : 0, delegate(int i)
			{
				prefs.Voice = i == 0;
			});
			if (prefs.Voice)
			{
				y = MpChoice(cx, y, cw, draw, vector, "Talk", new string[2]
				{
					"Hold " + KeyName(prefs.TalkKey),
					"Open mic"
				}, prefs.OpenMic ? 1 : 0, delegate(int i)
				{
					prefs.OpenMic = i == 1;
				});
				y = MpChoice(cx, y, cw, draw, vector, "Voices' volume", new string[3] { "Quiet", "Normal", "Loud" }, (!(prefs.VoiceVolume < 0.75f)) ? ((!(prefs.VoiceVolume > 1.25f)) ? 1 : 2) : 0, delegate(int i)
				{
					MpPrefs mpPrefs = prefs;
					float voiceVolume;
					switch (i)
					{
					default:
						voiceVolume = 1.6f;
						break;
					case 1:
						voiceVolume = 1f;
						break;
					case 0:
						voiceVolume = 0.5f;
						break;
					}
					mpPrefs.VoiceVolume = voiceVolume;
				});
			}
			y += num2;
			if (current == null)
			{
				MpUi.Poll();
				y = Heading(cx, y, cw, draw, "Join by code or address");
				if (draw)
				{
					GameArt.Label(new Rect(cx + 2f * p, y, cw * 0.4f, num), "Code or address", 4.4f, TextAnchor.MiddleLeft, GameArt.Grey);
					if (MpUi.JoinAddress == "" && prefs.LastAddress != "" && GUIUtility.keyboardControl == 0)
					{
						MpUi.JoinAddress = prefs.LastAddress;
					}
					MpUi.JoinAddress = GameTextField(new Rect(cx + cw * 0.4f, y, cw * 0.6f - 2f * p, num), MpUi.JoinAddress, 64, "mp_addr", 5.2f);
				}
				y += num + num2;
				if (draw)
				{
					GameArt.Label(new Rect(cx + 2f * p, y, cw * 0.4f, num), "Password (if it has one)", 4.4f, TextAnchor.MiddleLeft, GameArt.Grey);
					MpUi._joinPassword = GameTextField(new Rect(cx + cw * 0.4f, y, cw * 0.6f - 70f * p, num), MpUi._joinPassword, 40, "mp_pass", 5.2f);
					Rect r = new Rect(cx + cw - 66f * p, y, 64f * p, num);
					GameArt.FlatButton(r, red: true, "Join", 5f, (_pageMouseIn && r.Contains(vector)) ? 1f : 0.9f);
					if (PageButton(r))
					{
						MpUi.JoinTyped();
					}
				}
				y += num + num2;
				if (MpUi.Resolving)
				{
					y = MpNote(cx + 2f * p, y, cw - 4f * p, draw, "Finding the host of that Steam world", GameArt.Peach, 4.4f) + num2;
				}
				else if (MpUi.FindingCode)
				{
					y = MpNote(cx + 2f * p, y, cw - 4f * p, draw, "Looking for that code", GameArt.Peach, 4.4f) + num2;
				}
				else if (MpUi.Problem != "")
				{
					y = MpNote(cx + 2f * p, y, cw - 4f * p, draw, MpUi.Problem, MpRed, 4.6f) + num2;
				}
				y = UpdateLine(cx, y, cw, draw, vector, MpUi.ProblemKey, MpUi._joinPassword);
				string text = ClipboardCode();
				if (prefs.Recent.Count > 0 || text != "")
				{
					if (draw)
					{
						float num4 = cx + 2f * p;
						GameArt.Label(new Rect(num4, y, 40f * p, num), (prefs.Recent.Count > 0) ? "Recent" : "Copied", 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
						num4 += 42f * p;
						if (text != "")
						{
							string text2 = "Paste " + text;
							float num5 = Mathf.Min(GameArt.Measure(text2, 4.2f).x + 10f * p, cx + cw - num4);
							Rect r2 = new Rect(num4, y + 1f * p, num5, num - 2f * p);
							GameArt.FlatButton(r2, red: true, text2, 4.2f, (_pageMouseIn && r2.Contains(vector)) ? 1f : 0.88f);
							if (PageButton(r2))
							{
								MpUi.JoinAddress = text;
							}
							num4 += num5 + 3f * p;
						}
						foreach (string item in prefs.Recent)
						{
							float num6 = Mathf.Min(GameArt.Measure(item, 4.2f).x + 10f * p, cx + cw - num4);
							if (num6 < 30f * p)
							{
								break;
							}
							Rect r3 = new Rect(num4, y + 1f * p, num6, num - 2f * p);
							GameArt.FlatButton(r3, red: false, item, 4.2f, (_pageMouseIn && r3.Contains(vector)) ? 1f : 0.82f);
							if (PageButton(r3))
							{
								MpUi.JoinAddress = item;
							}
							num4 += num6 + 3f * p;
						}
					}
					y += num + num2;
				}
				y += num2;
				y = Heading(cx, y, cw, draw, "Worlds to join");
				if (draw)
				{
					string text3 = (MpUi.Refreshing ? "Looking..." : ((MpUi.Rows.Count == 0) ? "No worlds found" : (MpUi.Rows.Count + ((MpUi.Rows.Count == 1) ? " world" : " worlds"))));
					if (!SteamLobbies.Available)
					{
						text3 += ". Steam isn't running: only worlds on your network";
					}
					if (MpUi.RefreshNote != "")
					{
						text3 = text3 + ". " + MpUi.RefreshNote;
					}
					GameArt.Label(new Rect(cx + 2f * p, y, cw - 74f * p, num), text3, 4.2f, TextAnchor.MiddleLeft, GameArt.Grey, wrap: true);
					Rect r4 = new Rect(cx + cw - 66f * p, y, 64f * p, num);
					GameArt.FlatButton(r4, red: false, "Refresh", 4.8f, (_pageMouseIn && r4.Contains(vector)) ? 1f : 0.9f);
					if (PageButton(r4))
					{
						MpUi.Refresh();
					}
				}
				y += num + num2;
				float num7 = Mathf.Round(26f * p);
				foreach (ServerInfo row in MpUi.Rows)
				{
					string text4 = ((MpUi.Problem != "" && MpUi.ProblemKey == MpUi.RowKey(row)) ? MpUi.Problem : null);
					bool flag = MpUi.Mismatch(row) != null;
					float num8 = num7 + ((text4 != null) ? (MpNoteHeight(text4, cw - 12f * p, 4.2f) + 2f * p) : 0f);
					if (draw)
					{
						GameArt.Sliced(new Rect(cx, y, cw, num8), GameArt.Inner, new Color(1f, 1f, 1f, 0.5f));
						string text5 = (row.Locked ? "[locked] " : "") + row.Name;
						GameArt.Label(new Rect(cx + 6f * p, y + 1f * p, cw - 120f * p, 12f * p), text5, 5f, TextAnchor.MiddleLeft, Color.white);
						string text6 = Join(", ", flag ? "another Worldfall build" : "", (row.World != row.Name) ? row.World : "", row.MapSize, row.Age, HostRules.PowerNames[row.Rules.Powers], HostRules.DeathNames[row.Rules.Death], row.Rules.Pvp ? "PvP" : "no PvP", row.Source);
						GameArt.Label(new Rect(cx + 6f * p, y + 13f * p, cw - 120f * p, 11f * p), text6, 3.9f, TextAnchor.MiddleLeft, flag ? MpRed : GameArt.Grey);
						string text7 = row.Players + "/" + row.MaxPlayers + ((row.Ping >= 0) ? ("  " + row.Ping + " ms") : "");
						GameArt.Label(new Rect(cx + cw - 112f * p, y, 44f * p, num7), text7, 4.4f, TextAnchor.MiddleRight, GameArt.Peach);
						Rect r5 = new Rect(cx + cw - 62f * p, y + 5f * p, 58f * p, num7 - 10f * p);
						bool flag2 = row.Players >= row.MaxPlayers;
						GameArt.FlatButton(r5, !flag2 && !flag, flag2 ? "Full" : "Join", 4.8f, (_pageMouseIn && r5.Contains(vector)) ? 1f : 0.88f);
						if (PageButton(r5) && !flag2)
						{
							MpUi.Join(row);
						}
						if (text4 != null)
						{
							MpNote(cx + 6f * p, y + num7 - 2f * p, cw - 12f * p, draw: true, text4, MpRed, 4.2f);
						}
					}
					y += num8 + 2f * p;
				}
				y += num2 * 2f;
			}
			if (current == null || current.IsHost)
			{
				y = Heading(cx, y, cw, draw, (current == null) ? "Host this world" : "This world's rules");
				if (current == null)
				{
					y = MpField(cx, y, cw, draw, "Name in the browser", (prefs.WorldTitle == "") ? WorldSync.WorldName() : prefs.WorldTitle, "mp_title", 40, delegate(string v)
					{
						prefs.WorldTitle = MpPrefs.Clean(v, 40);
					});
					y = MpField(cx, y, cw, draw, "Password (empty: anyone)", prefs.Password, "mp_hostpass", 40, delegate(string v)
					{
						prefs.Password = v;
					});
					y = MpChoice(cx, y, cw, draw, vector, "Players", new string[4] { "2", "4", "6", "8" }, (prefs.MaxPlayers > 2) ? ((prefs.MaxPlayers <= 4) ? 1 : ((prefs.MaxPlayers <= 6) ? 2 : 3)) : 0, delegate(int i)
					{
						prefs.MaxPlayers = 2 + i * 2;
					});
					y = MpChoice(cx, y, cw, draw, vector, "List on Steam", new string[2] { "On", "Off" }, (!prefs.ListOnSteam) ? 1 : 0, delegate(int i)
					{
						prefs.ListOnSteam = i == 0;
					});
					if (!steamOn)
					{
						y = MpNote(cx + cw * 0.4f, y, cw * 0.6f - 2f * p, draw, "Steam is not running. Friends can join by address only", prefs.AllowIp ? GameArt.Peach : MpRed, 4.2f) + num2;
					}
					else if (!prefs.ListOnSteam)
					{
						y = MpNote(cx + cw * 0.4f, y, cw * 0.6f - 2f * p, draw, "Off. Steam friends can still join from your profile or an invite", GameArt.Grey, 4.2f) + num2;
					}
					y = MpChoice(cx, y, cw, draw, vector, "Join by address", new string[2] { "On", "Off" }, (!prefs.AllowIp) ? 1 : 0, delegate(int i)
					{
						prefs.AllowIp = i == 0;
					});
					if (!prefs.AllowIp)
					{
						y = MpNote(cx + cw * 0.4f, y, cw * 0.6f - 2f * p, draw, steamOn ? "Off. Only Steam friends can join, addresses are refused" : "Off, and Steam is not running. Nobody could join", steamOn ? GameArt.Orange : MpRed, 4.2f) + num2;
					}
					if (prefs.AllowIp)
					{
						y = MpField(cx, y, cw, draw, "Port", prefs.Port.ToString(), "mp_port", 5, delegate(string v)
						{
							if (int.TryParse(v, out var result) && result >= 1024 && result <= 65535)
							{
								prefs.Port = result;
							}
						});
						y = MpChoice(cx, y, cw, draw, vector, "Open port on router", new string[2] { "On", "Off" }, (!prefs.RouterPort) ? 1 : 0, delegate(int i)
						{
							prefs.RouterPort = i == 0;
						});
						if (!prefs.RouterPort)
						{
							y = MpNote(cx + cw * 0.4f, y, cw * 0.6f - 2f * p, draw, "Off. Forward TCP and UDP port " + prefs.Port + " by hand for players outside your network", GameArt.Grey, 4.2f) + num2;
						}
					}
				}
				HostRules rules = ((current != null) ? current.Rules : prefs.Rules);
				y = MpChoice(cx, y, cw, draw, vector, "God powers for players", HostRules.PowerNames, rules.Powers, delegate(int i)
				{
					rules.Powers = i;
					prefs.Rules.Powers = i;
				});
				y = MpChoice(cx, y, cw, draw, vector, "When a player dies", HostRules.DeathNames, rules.Death, delegate(int i)
				{
					rules.Death = i;
					prefs.Rules.Death = i;
				});
				y = MpChoice(cx, y, cw, draw, vector, "Lock-in runs", HostRules.RunNames, rules.Runs, delegate(int i)
				{
					rules.Runs = i;
					prefs.Rules.Runs = i;
				});
				y = MpChoice(cx, y, cw, draw, vector, "Players dig", HostRules.DigNames, rules.GuestsDig ? 1 : 0, delegate(int i)
				{
					rules.GuestsDig = i == 1;
					prefs.Rules.GuestsDig = i == 1;
				});
				if (current == null)
				{
					if (draw)
					{
						Rect r6 = new Rect(cx + cw * 0.5f - 60f * p, y + 2f * p, 120f * p, 18f * p);
						GameArt.FlatButton(r6, red: true, "Host this world", 5.6f, (_pageMouseIn && r6.Contains(vector)) ? 1f : 0.92f);
						if (PageButton(r6))
						{
							prefs.Save();
							string text8 = NetSession.Host();
							if (text8 != null)
							{
								MpUi.HostFailed("Could not host", text8);
							}
						}
					}
					y += 22f * p;
					if (MpUi.HostProblem != "")
					{
						y = MpNote(cx + 2f * p, y, cw - 4f * p, draw, MpUi.HostProblem, MpRed, 4.6f) + num2;
					}
				}
			}
			y = MpNote(cx + 2f * p, y + num2, cw - 4f * p, draw, "Worldfall 0.9.8.9, build " + NetProtocol.Build + ". Every player needs the same build", GameArt.Grey, 3.9f);
			return y;
		}

		private float SessionCard(float cx, float y, float cw, bool draw, Vector2 mouse, NetSession s)
		{
			float p = GameArt.P;
			float num = Mathf.Round(16f * p);
			float num2 = cx + 6f * p;
			float num3 = cw - 12f * p;
			if (draw)
			{
				string text = (s.IsHost ? ("Hosting " + s.Describe().Name) : ("In " + (s.HostInfo?.Name ?? "a world")));
				GameArt.Label(new Rect(num2, y + 2f * p, cw - (s.IsHost ? 80f : 142f) * p, 14f * p), text, 5.6f, TextAnchor.MiddleLeft, GameArt.Orange);
				Rect r = new Rect(cx + cw - 74f * p, y + 3f * p, 70f * p, 14f * p);
				GameArt.FlatButton(r, red: true, s.IsHost ? "Stop hosting" : "Leave", 4.8f, (_pageMouseIn && r.Contains(mouse)) ? 1f : 0.9f);
				if (PageButton(r))
				{
					NetSession.Leave();
				}
				if (!s.IsHost)
				{
					Rect r2 = new Rect(r.x - 62f * p, r.y, 58f * p, r.height);
					bool canResync = WorldSync.CanResync;
					GameArt.FlatButton(r2, red: false, WorldSync.Resyncing ? "Resyncing" : "Resync", 4.8f, (!canResync) ? 0.55f : ((_pageMouseIn && r2.Contains(mouse)) ? 1f : 0.9f));
					if (canResync && PageButton(r2))
					{
						WorldSync.AskResync();
					}
				}
			}
			y += 18f * p;
			y = MpNote(num2, y, num3, draw, SessionLine(s), GameArt.Grey, 4.4f);
			if (s.IsHost)
			{
				MpPrefs prefs = MpPrefs.Current;
				bool steamOn = NetSession.SteamOn;
				if (s.OpenProblems != "")
				{
					y = MpNote(num2, y, num3, draw, s.OpenProblems, MpRed, 4.2f);
				}
				y = CodeLine(cx, y, cw, draw, mouse, s);
				if (steamOn)
				{
					bool flag = SteamLobbies.LobbyId != 0;
					string note = ((SteamLobbies.Problem != "") ? SteamLobbies.Problem : ((!flag) ? "Opening the Steam lobby" : (prefs.ListOnSteam ? "Join with the code or an invite. Listed on Steam" : "Join with the code or an invite. Not listed on Steam")));
					y = JoinRow(cx, y, cw, draw, mouse, "Steam players", note, (SteamLobbies.Problem != "") ? MpRed : GameArt.Grey, "Invite friends", flag, delegate
					{
						MpUi.HostProblem = SteamLobbies.Invite() ?? "";
					});
				}
				else
				{
					y = JoinRow(cx, y, cw, draw, mouse, "Steam players", "Steam is not running here. Steam players join with the code like phones", GameArt.Orange);
				}
				y = JoinRow(cx, y, cw, draw, mouse, "Phones and Macs", PhonesLine(s, out var ok), ok ? GameArt.Grey : GameArt.Orange);
				if (prefs.AllowIp && s.LanAddresses.Count > 0)
				{
					y = MpNote(num2 + 66f * p, y, num3 - 66f * p, draw, "This PC at home: " + string.Join(", ", s.LanAddresses.ConvertAll((string a) => a + ":" + prefs.Port).ToArray()), GameArt.Grey, 3.9f);
				}
				if (prefs.AllowIp && WinNet.IsPublic)
				{
					y = MpNote(num2 + 66f * p, y, num3 - 66f * p, draw, "Windows treats this network as Public, so its firewall may keep them out. Set it to Private in Windows network settings", GameArt.Orange, 4.2f);
				}
				if (MpUi.HostProblem != "")
				{
					y = MpNote(num2, y, num3, draw, MpUi.HostProblem, MpRed, 4.2f);
				}
				if (Updater.Mismatch && !Updater.OtherHosts)
				{
					y = UpdateLine(num2 - 2f * p, y, num3 + 4f * p, draw, mouse, "", "");
				}
			}
			y += 2f * p;
			if (draw)
			{
				GameArt.Label(new Rect(num2, y, num3, 12f * p), "Players", 4.8f, TextAnchor.MiddleLeft, GameArt.Peach);
			}
			y += 14f * p;
			foreach (NetPlayer value2 in s.Players.Values)
			{
				if (draw)
				{
					string text2 = (value2.Host ? "host" : "") + ((value2.Body != 0L) ? ((value2.Host ? ", " : "") + BodyName(value2.Body)) : ((value2.Host ? ", " : "") + "god view")) + (value2.Host ? "" : (", " + PingText(s, value2)));
					GameArt.Label(new Rect(cx + 8f * p, y, cw * 0.4f, num), value2.Name, 4.8f, TextAnchor.MiddleLeft, (value2 == s.Me) ? GameArt.Orange : Color.white);
					GameArt.Label(new Rect(cx + cw * 0.4f, y, cw * 0.6f - (s.IsHost ? (84f * p) : (8f * p)), num), text2, 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
					if (s.IsHost && !value2.Host)
					{
						float realtimeSinceStartup = Time.realtimeSinceStartup;
						if (!_rowSeen.TryGetValue(value2.Id, out var value))
						{
							value = (_rowSeen[value2.Id] = realtimeSinceStartup);
						}
						bool flag2 = realtimeSinceStartup - value > 1.5f;
						bool flag3 = _banArmed == value2.Id && realtimeSinceStartup - _banArmedAt < 4f;
						Rect r3 = new Rect(cx + cw - 82f * p, y + 1f * p, 38f * p, num - 2f * p);
						Rect r4 = new Rect(cx + cw - 42f * p, y + 1f * p, 38f * p, num - 2f * p);
						GameArt.FlatButton(r3, red: false, "Kick", 4.4f, (!flag2) ? 0.5f : ((_pageMouseIn && r3.Contains(mouse)) ? 1f : 0.85f));
						GameArt.FlatButton(r4, red: true, flag3 ? "Sure?" : "Ban", 4.4f, (!flag2) ? 0.5f : ((_pageMouseIn && r4.Contains(mouse)) ? 1f : 0.85f));
						if (PageButton(r3) && flag2)
						{
							s.Kick(value2, "Kicked by the host", ban: false);
						}
						if (PageButton(r4) && flag2)
						{
							if (flag3)
							{
								_banArmed = 0;
								s.Kick(value2, "Banned by the host", ban: true);
							}
							else
							{
								_banArmed = value2.Id;
								_banArmedAt = realtimeSinceStartup;
							}
						}
					}
				}
				y += num + 2f * p;
			}
			int num5 = (s.IsHost ? BannedPlayers(MpPrefs.Current) : 0);
			if (num5 > 0)
			{
				if (draw)
				{
					GameArt.Label(new Rect(cx + 8f * p, y, cw * 0.6f, num), (num5 == 1) ? "1 player banned" : (num5 + " players banned"), 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
					Rect r5 = new Rect(cx + cw - 82f * p, y + 1f * p, 78f * p, num - 2f * p);
					GameArt.FlatButton(r5, red: false, "Lift bans", 4.4f, (_pageMouseIn && r5.Contains(mouse)) ? 1f : 0.85f);
					if (PageButton(r5))
					{
						MpPrefs.Current.Banned.Clear();
						MpPrefs.Current.Save();
						Log.Info("NET bans lifted");
					}
				}
				y += num + 2f * p;
			}
			return y + 4f * p;
		}

		private static int BannedPlayers(MpPrefs prefs)
		{
			int num = 0;
			foreach (string item in prefs.Banned)
			{
				if (item.Length >= 16 && item.IndexOf(':') < 0 && item.IndexOf('.') < 0)
				{
					num++;
				}
			}
			if (num <= 0)
			{
				return prefs.Banned.Count;
			}
			return num;
		}

		private static float MpNote(float x, float y, float w, bool draw, string text, Color colour, float size)
		{
			if (string.IsNullOrEmpty(text))
			{
				return y;
			}
			float num = MpNoteHeight(text, w, size);
			if (draw)
			{
				GameArt.Label(new Rect(x, y, w, num), text, size, TextAnchor.UpperLeft, colour, wrap: true);
			}
			return y + num + 1f * GameArt.P;
		}

		private static float MpNoteHeight(string text, float w, float size)
		{
			float num = Mathf.Round(size * 2.3f * GameArt.P);
			float num2;
			try
			{
				Vector2 vector = GameArt.Measure(text, size);
				num2 = vector.x;
				if (vector.y > 0f)
				{
					num = Mathf.Ceil(vector.y);
				}
			}
			catch
			{
				num2 = (float)text.Length * size * 0.55f * GameArt.P;
			}
			return (float)Mathf.Max(1, Mathf.CeilToInt(num2 * 1.1f / Mathf.Max(20f, w))) * num;
		}

		private float Heading(float cx, float y, float cw, bool draw, string text)
		{
			float p = GameArt.P;
			if (draw)
			{
				GameArt.Label(new Rect(cx + 2f * p, y, cw - 4f * p, 14f * p), text, 5.6f, TextAnchor.MiddleLeft, GameArt.Orange);
			}
			return y + 15f * p;
		}

		private bool RawField(Rect r, string value, int max, string name, float size, out string typed)
		{
			bool num = GUI.GetNameOfFocusedControl() == name;
			string value2;
			string text = ((num && _rawTyped.TryGetValue(name, out value2)) ? value2 : value);
			if (!num)
			{
				_rawTyped.Remove(name);
			}
			typed = GameTextField(r, text, max, name, size);
			if (typed == text)
			{
				return false;
			}
			_rawTyped[name] = typed;
			return true;
		}

		private float MpField(float cx, float y, float cw, bool draw, string label, string value, string name, int max, Action<string> set)
		{
			float p = GameArt.P;
			float num = Mathf.Round(16f * p);
			if (draw)
			{
				GameArt.Label(new Rect(cx + 2f * p, y, cw * 0.5f, num), label, 4.2f, TextAnchor.MiddleLeft, GameArt.Grey, wrap: true);
				if (RawField(new Rect(cx + cw * 0.5f, y, cw * 0.5f - 2f * p, num), value, max, name, 5f, out var typed))
				{
					set(typed);
					MpPrefs.Current.Save();
				}
			}
			return y + num + 3f * p;
		}

		private float MpChoice(float cx, float y, float cw, bool draw, Vector2 mouse, string label, string[] choices, int chosen, Action<int> pick)
		{
			float p = GameArt.P;
			float num = Mathf.Round(16f * p);
			if (draw)
			{
				GameArt.Label(new Rect(cx + 2f * p, y, cw * 0.4f, num), label, 4.4f, TextAnchor.MiddleLeft, GameArt.Grey, wrap: true);
				float num2 = cx + cw * 0.4f;
				float num3 = (cw * 0.6f - 2f * p - (float)(choices.Length - 1) * 2f * p) / (float)choices.Length;
				for (int i = 0; i < choices.Length; i++)
				{
					Rect r = new Rect(Mathf.Round(num2 + (float)i * (num3 + 2f * p)), y, Mathf.Round(num3), num);
					GameArt.FlatButton(r, i == chosen, choices[i], 4.4f, ((_pageMouseIn && r.Contains(mouse)) || i == chosen) ? 1f : 0.82f);
					if (PageButton(r) && i != chosen)
					{
						pick(i);
						MpPrefs.Current.Save();
					}
				}
			}
			return y + num + 3f * p;
		}

		private float JoinRow(float cx, float y, float cw, bool draw, Vector2 mouse, string label, string note, Color colour, string button = null, bool buttonOn = true, Action pressed = null)
		{
			float p = GameArt.P;
			float num = cx + 6f * p;
			float num2 = y + 1f * p;
			float num3 = ((button != null) ? (74f * p) : 0f);
			if (draw)
			{
				GameArt.Label(new Rect(num, num2, 64f * p, 12f * p), label, 4.4f, TextAnchor.MiddleLeft, GameArt.Peach);
			}
			float a = MpNote(num + 66f * p, num2 + 1f * p, cw - 78f * p - num3, draw, note, colour, 4.2f);
			if (button != null && draw)
			{
				Rect r = new Rect(cx + cw - 74f * p, num2, 70f * p, 14f * p);
				GameArt.FlatButton(r, buttonOn, button, 4.6f, (_pageMouseIn && r.Contains(mouse)) ? 1f : 0.88f);
				if (PageButton(r))
				{
					pressed?.Invoke();
				}
			}
			return Mathf.Max(a, num2 + ((button != null) ? 16f : 13f) * p) + 1f * p;
		}

		private static string PhonesLine(NetSession s, out bool ok)
		{
			MpPrefs current = MpPrefs.Current;
			ok = false;
			if (!current.AllowIp)
			{
				return "Join by address is off, so they can't join. Turn it on before hosting";
			}
			if (!current.RouterPort)
			{
				return "From the list at home. With the code elsewhere only once port " + current.Port + " is forwarded on your router";
			}
			switch (Upnp.State)
			{
			case Upnp.Phase.Working:
				ok = true;
				return "From the list at home. Opening port " + current.Port + " on your router";
			case Upnp.Phase.Open:
				ok = true;
				return "From the list at home, or with the code anywhere. Port " + current.Port + " is open on your router";
			case Upnp.Phase.Failed:
				return "From the list at home only. " + Upnp.Note;
			default:
				ok = IpTransport.LoopbackOnly;
				return "From the list at home";
			}
		}

		private float CodeLine(float cx, float y, float cw, bool draw, Vector2 mouse, NetSession s)
		{
			float p = GameArt.P;
			float num = cx + 6f * p;
			float num2 = cw - 12f * p;
			string code = s.Code;
			if (code == "")
			{
				bool flag = Upnp.State == Upnp.Phase.Working;
				string text = (flag ? "Making your join code" : ((Upnp.CarrierNat || MpPrefs.Current.AllowIp) ? "No join code. Start Steam to get one" : "No join code. Turn Join by address on or start Steam"));
				return MpNote(num, y + 1f * p, num2, draw, text, flag ? GameArt.Grey : GameArt.Orange, 4.4f);
			}
			float num3 = 16f * p;
			if (draw)
			{
				GameArt.Label(new Rect(num, y + 1f * p, 64f * p, num3), "Join code", 4.4f, TextAnchor.MiddleLeft, GameArt.Peach);
				GameArt.Label(new Rect(num + 66f * p, y + 1f * p, num2 - 142f * p, num3), code, 6.4f, TextAnchor.MiddleLeft, GameArt.Orange);
				Rect r = new Rect(cx + cw - 74f * p, y + 2f * p, 70f * p, 14f * p);
				bool flag2 = Time.realtimeSinceStartup - _codeCopiedAt < 2f;
				GameArt.FlatButton(r, red: true, flag2 ? "Copied" : "Copy", 4.6f, (_pageMouseIn && r.Contains(mouse)) ? 1f : 0.88f);
				if (PageButton(r))
				{
					GUIUtility.systemCopyBuffer = code;
					_codeCopiedAt = Time.realtimeSinceStartup;
				}
			}
			return y + num3 + 2f * p;
		}

		private static string ClipboardCode()
		{
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (realtimeSinceStartup - _clipReadAt > 1f)
			{
				_clipReadAt = realtimeSinceStartup;
				string text = "";
				try
				{
					text = (GUIUtility.systemCopyBuffer ?? "").Trim();
				}
				catch
				{
				}
				_clipCode = ((text.Length <= 20 && JoinCode.Looks(text)) ? JoinCode.Read(text).Code : "");
				if (NetSession.Hosting && _clipCode == NetSession.Current.Code)
				{
					_clipCode = "";
				}
			}
			if (!(_clipCode != "") || !(JoinCode.Read(MpUi.JoinAddress ?? "").Code != _clipCode))
			{
				return "";
			}
			return _clipCode;
		}

		private float UpdateLine(float cx, float y, float cw, bool draw, Vector2 mouse, string rejoin, string password)
		{
			if (!Updater.Mismatch && Updater.State == Updater.Step.Idle)
			{
				return y;
			}
			float p = GameArt.P;
			float num = 3f * p;
			float num2 = Mathf.Round(16f * p);
			bool num3 = (Updater.ShouldUpdate && Updater.State == Updater.Step.Idle) || Updater.State == Updater.Step.Failed;
			string text = ((Updater.State != Updater.Step.Idle) ? Updater.Status : Updater.Advice);
			Color colour = ((Updater.State == Updater.Step.Failed) ? MpRed : ((Updater.State != Updater.Step.Idle) ? GameArt.Peach : GameArt.Grey));
			float num4 = (num3 ? (90f * p) : 0f);
			float num5 = y;
			y = MpNote(cx + 2f * p, y + 1f * p, cw - 4f * p - num4 - 2f * p, draw, text, colour, 4.4f);
			if (num3 && draw)
			{
				Rect r = new Rect(cx + cw - num4 - 2f * p, num5, num4, num2);
				GameArt.FlatButton(r, red: true, (rejoin == "") ? "Update Worldfall" : "Update and join", 4.6f, (_pageMouseIn && r.Contains(mouse)) ? 1f : 0.9f);
				if (PageButton(r))
				{
					Updater.Start(rejoin, password);
				}
			}
			return Mathf.Max(y, num5 + num2) + num;
		}

		private static string SessionLine(NetSession s)
		{
			if (!s.IsHost)
			{
				if (!(s.Stage != ""))
				{
					return "Connected. Ping " + (int)(s.HostLink?.PingMs ?? 0f) + " ms. / or Enter to chat";
				}
				return s.Stage;
			}
			List<string> list = new List<string> { s.Players.Count + "/" + MpPrefs.Current.MaxPlayers + " players" };
			if (MpPrefs.Current.AllowIp)
			{
				list.Add("port " + MpPrefs.Current.Port);
			}
			return string.Join(", ", list.ToArray());
		}

		private static string KeyName(int key)
		{
			KeyCode keyCode = (KeyCode)key;
			string text = keyCode.ToString();
			if (!(text == "CapsLock"))
			{
				return text;
			}
			return "Caps Lock";
		}

		private static string PingText(NetSession s, NetPlayer p)
		{
			int num = p.PingMs;
			if (p == s.Me && !s.IsHost && s.HostLink != null)
			{
				num = Mathf.RoundToInt(s.HostLink.PingMs);
			}
			if (num <= 0)
			{
				return "-- ms";
			}
			return num + " ms";
		}

		private static string BodyName(long id)
		{
			try
			{
				Actor actor = World.world.units.get(id);
				return (actor != null) ? actor.getName() : "a creature";
			}
			catch
			{
				return "a creature";
			}
		}

		private static string Join(string sep, params string[] parts)
		{
			List<string> list = new List<string>();
			foreach (string text in parts)
			{
				if (!string.IsNullOrEmpty(text))
				{
					list.Add(text);
				}
			}
			return string.Join(sep, list.ToArray());
		}

		internal void DrawNet(bool firstPerson)
		{
			NetSession current = NetSession.Current;
			string text = GUI.GetNameOfFocusedControl() ?? "";
			bool flag = _mod != null && _mod.SettingsMenu.Open;
			bool flag2 = GUIUtility.keyboardControl != 0 && text.StartsWith("mp_", StringComparison.Ordinal);
			if (flag2 && ((text == "mp_chat") ? (!MpUi.ChatOpen) : (!flag)))
			{
				GUIUtility.keyboardControl = 0;
				flag2 = false;
			}
			MpUi.Typing = MpUi.ChatOpen || flag2;
			if (current == null)
			{
				return;
			}
			float num = Screen.width;
			float num2 = Screen.height;
			float p = GameArt.P;
			Event current2 = Event.current;
			if (current2.type == EventType.Repaint)
			{
				DrawNameTags(current, firstPerson, num, num2);
			}
			if (!firstPerson && NeedsBody(current))
			{
				DrawSpawnPanel(current, num, num2);
			}
			if (ModSync.PromptShown)
			{
				ModsPrompt(current, num, num2, input: true);
			}
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			float num3 = 8f * p;
			float num4 = Mathf.Round(Mathf.Min(300f * p, num * 0.45f));
			float num5 = num2 - 74f * p;
			if (realtimeSinceStartup - _speakingAt > 0.25f)
			{
				_speakingAt = realtimeSinceStartup;
				_talk.Clear();
				if (VoiceChat.Talking)
				{
					_talk.Add("You");
				}
				foreach (KeyValuePair<int, float> item in VoiceChat.Heard)
				{
					if (realtimeSinceStartup - item.Value < 1f && current.Players.TryGetValue(item.Key, out var value))
					{
						_talk.Add(value.Name);
					}
				}
				_speaking = ((_talk.Count > 0) ? ("Speaking: " + string.Join(", ", _talk.ToArray())) : null);
			}
			if (_speaking != null)
			{
				GameArt.Label(new Rect(num3, num5 + 31f * p, num4, 10f * p), _speaking, 4.6f, TextAnchor.MiddleLeft, new Color(0.6f, 1f, 0.6f));
			}
			if (MpUi.ChatOpen)
			{
				MpUi.ChatKey(current2);
				Rect r = new Rect(num3, num5, num4, Mathf.Round(18f * p));
				GameArt.TipPanel(r);
				GUI.SetNextControlName("mp_chat");
				MpUi.ChatText = GameTextField(new Rect(r.x + 3f * p, r.y + 1f * p, r.width - 6f * p, r.height - 2f * p), MpUi.ChatText, 200, "mp_chat", 5.4f);
				if (MpUi.ChatOpen)
				{
					GUI.FocusControl("mp_chat");
				}
				if (MpUi.CaretToEnd && current2.type == EventType.Repaint && GUI.GetNameOfFocusedControl() == "mp_chat" && GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) is TextEditor textEditor)
				{
					textEditor.text = MpUi.ChatText;
					textEditor.MoveTextEnd();
					MpUi.CaretToEnd = false;
				}
				string text2 = "Enter: send   Esc: close   /p: party only";
				if (PlayerTalk.Talking && current.Players.TryGetValue(PlayerTalk.With, out var value2))
				{
					text2 = "Enter: say to " + value2.Name + "   /a: everyone   /p: party   Esc: close";
				}
				GameArt.Label(new Rect(num3, num5 + 19f * p, num4, 10f * p), text2, 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
			}
			int num6 = 0;
			float num7 = Mathf.Max(num2 * 0.3f, 40f * p);
			int num8 = current.Chat.Count - 1;
			while (num8 >= 0 && num6 < 10)
			{
				NetSession.ChatLine chatLine = current.Chat[num8];
				float num9 = realtimeSinceStartup - chatLine.At;
				float num10 = (MpUi.ChatOpen ? 1f : Mathf.Clamp01(1f - (num9 - 30f) / 3f));
				if (!(num10 <= 0f))
				{
					if (chatLine.Shown == null)
					{
						chatLine.Shown = (chatLine.System ? chatLine.Text : ((chatLine.Party ? "(party) " : (chatLine.Private ? "(talk) " : "")) + chatLine.Name + ": " + chatLine.Text));
					}
					string shown = chatLine.Shown;
					if (chatLine.ShownW != num4 || chatLine.ShownK != p)
					{
						chatLine.ShownW = num4;
						chatLine.ShownK = p;
						chatLine.ShownH = Mathf.Max(Mathf.Round(13f * p), MpNoteHeight(shown, num4, 5.2f) + 2f * p);
					}
					float shownH = chatLine.ShownH;
					if (!(num5 - shownH < num7))
					{
						num5 -= shownH;
						num6++;
						Color color = (chatLine.System ? new Color(1f, 0.85f, 0.5f, num10) : (chatLine.Party ? new Color(0.6f, 0.9f, 1f, num10) : (chatLine.Private ? new Color(1f, 0.75f, 0.95f, num10) : new Color(1f, 1f, 1f, num10))));
						GameArt.Label(new Rect(num3 + 1f, num5 + 1f, num4, shownH), shown, 5.2f, TextAnchor.MiddleLeft, new Color(0f, 0f, 0f, 0.75f * num10), wrap: true);
						GameArt.Label(new Rect(num3, num5, num4, shownH), shown, 5.2f, TextAnchor.MiddleLeft, color, wrap: true);
						num8--;
						continue;
					}
					break;
				}
				break;
			}
		}

		private void DrawPartyMini(Func<Vector2, Vector2> toMap, Vector2 centre, float radius, float alpha)
		{
			NetSession current = NetSession.Current;
			if (current?.Me == null || current.Me.Party == 0)
			{
				return;
			}
			float p = GameArt.P;
			float num = Mathf.Max(3f, Mathf.Round(3f * p));
			foreach (NetPlayer value in current.Players.Values)
			{
				if (value == current.Me || !Parties.SameParty(value, current.Me) || value.Body == 0L)
				{
					continue;
				}
				Actor actor = null;
				try
				{
					actor = World.world.units.get(value.Body);
				}
				catch
				{
				}
				if (actor != null)
				{
					Vector2 vector = toMap(actor.current_position);
					Vector2 vector2 = vector - centre;
					float num2 = radius - 4f * p;
					bool flag = vector2.magnitude > num2;
					if (flag)
					{
						vector = centre + vector2.normalized * num2;
					}
					GameArt.Flat(new Rect(Mathf.Round(vector.x - num * 0.5f) - 1f, Mathf.Round(vector.y - num * 0.5f) - 1f, num + 2f, num + 2f), new Color(0f, 0f, 0f, 0.8f * alpha));
					GameArt.Flat(new Rect(Mathf.Round(vector.x - num * 0.5f), Mathf.Round(vector.y - num * 0.5f), num, num), new Color(0.45f, 0.85f, 1f, (flag ? 0.7f : 1f) * alpha));
				}
			}
		}

		internal void DrawNetOver()
		{
			if (!Config.game_loaded)
			{
				return;
			}
			NetSession current = NetSession.Current;
			float num = Screen.width;
			float sh = Screen.height;
			float p = GameArt.P;
			if (current != null && ModSync.PromptShown)
			{
				ModsPrompt(current, num, sh, input: false);
			}
			if (Event.current.type != EventType.Repaint)
			{
				return;
			}
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			float num2 = Mathf.Round(Mathf.Min(300f * p, num - 20f * p));
			if (current != null && !current.IsHost && current.Stage != "" && !ModSync.PromptShown)
			{
				Rect r = new Rect(Mathf.Round((num - num2) * 0.5f), Mathf.Round(30f * p), num2, Mathf.Round(18f * p));
				GameArt.TipPanel(r);
				GameArt.Label(r, current.Stage, 5.4f, TextAnchor.MiddleCenter, Color.white);
			}
			else if (MpUi.Resolving)
			{
				Rect r2 = new Rect(Mathf.Round((num - num2) * 0.5f), Mathf.Round(30f * p), num2, Mathf.Round(18f * p));
				GameArt.TipPanel(r2);
				GameArt.Label(r2, "Finding the host through Steam", 5.4f, TextAnchor.MiddleCenter, Color.white);
			}
			else if (!(MpUi.BannerUntil <= realtimeSinceStartup))
			{
				if (WorldBoxMod.WorldLoading)
				{
					MpUi.BannerUntil = Mathf.Max(MpUi.BannerUntil, realtimeSinceStartup + 4f);
				}
				float num3 = Mathf.Clamp01((MpUi.BannerUntil - realtimeSinceStartup) / 0.6f);
				float num4 = MpNoteHeight(MpUi.BannerText, num2 - 16f * p, 4.8f);
				Rect r3 = new Rect(Mathf.Round((num - num2) * 0.5f), Mathf.Round(30f * p), num2, Mathf.Round(24f * p + num4));
				GameArt.TipPanel(r3, num3);
				Color color = (MpUi.BannerBad ? MpRed : GameArt.Orange);
				color.a *= num3;
				GameArt.Label(new Rect(r3.x + 8f * p, r3.y + 4f * p, num2 - 16f * p, 14f * p), MpUi.BannerTitle, 5.6f, TextAnchor.MiddleCenter, color);
				GameArt.Label(new Rect(r3.x + 8f * p, r3.y + 19f * p, num2 - 16f * p, num4), MpUi.BannerText, 4.8f, TextAnchor.UpperCenter, new Color(1f, 1f, 1f, num3), wrap: true);
			}
		}

		private void ModsPrompt(NetSession s, float sw, float sh, bool input)
		{
			float p = GameArt.P;
			float num = Mathf.Round(Mathf.Min(260f * p, sw - 20f * p));
			float num2 = Mathf.Round(11f * p);
			float num3 = num - 16f * p;
			int num4 = Mathf.Min(ModSync.Missing.Count, 8);
			bool flag = ModSync.Missing.Count > num4;
			bool flag2 = ModSync.Missing.Exists((ModSync.Mod m) => m.Workshop == 0);
			float num5 = ((ModSync.Note != "") ? (MpNoteHeight(ModSync.Note, num3, 4.2f) + 3f * p) : 0f);
			float num6 = 26f * p + (float)(num4 + (flag ? 1 : 0)) * num2 + (flag2 ? (40f * p) : 0f) + num5 + 30f * p;
			Rect rect = new Rect(Mathf.Round((sw - num) * 0.5f), Mathf.Round((sh - num6) * 0.4f), num, Mathf.Round(num6));
			Vector2 mousePosition = Event.current.mousePosition;
			float num7 = rect.x + 8f * p;
			float num8 = rect.y + 5f * p;
			if (input)
			{
				PanelBlocker.Cover(rect);
			}
			else
			{
				GameArt.TipPanel(rect);
				GameArt.Label(new Rect(num7, num8, num3, 14f * p), "This world uses mods you don't have", 5.6f, TextAnchor.MiddleLeft, GameArt.Orange);
			}
			num8 += 18f * p;
			for (int num9 = 0; num9 < num4; num9++)
			{
				ModSync.Mod mod = ModSync.Missing[num9];
				string text = ((mod.Workshop != 0L) ? "Steam Workshop" : "the host's computer");
				if (!input)
				{
					GameArt.Label(new Rect(num7, num8, num3, num2), mod.Name + ((mod.Version != "") ? (" " + mod.Version) : "") + "  (" + (mod.Size / 1024 + 1) + " KB, from " + text + ")", 4.2f, TextAnchor.MiddleLeft, Color.white);
				}
				num8 += num2;
			}
			if (flag)
			{
				if (!input)
				{
					GameArt.Label(new Rect(num7, num8, num3, num2), "and " + (ModSync.Missing.Count - num4) + " more", 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
				}
				num8 += num2;
			}
			if (flag2)
			{
				num8 += 3f * p;
				if (!input)
				{
					GameArt.Label(new Rect(num7, num8, num3, 20f * p), "Mods are programs. Some are only on the host's computer, and a bad host could send you a virus. Only take them from people you trust", 4f, TextAnchor.UpperLeft, new Color(1f, 0.55f, 0.45f), wrap: true);
				}
				num8 += 21f * p;
				Rect rect2 = new Rect(num7, num8, num3, 14f * p);
				if (!input)
				{
					GameArt.FlatButton(rect2, ModSync.TrustHost, (ModSync.TrustHost ? "[x] " : "[ ] ") + "I trust this host. Send me their mod files", 4.4f, rect2.Contains(mousePosition) ? 1f : 0.88f);
				}
				else if (GUI.Button(rect2, GUIContent.none, GUIStyle.none))
				{
					ModSync.TrustHost = !ModSync.TrustHost;
					ModSync.Note = "";
				}
				num8 += 16f * p;
			}
			if (num5 > 0f)
			{
				if (!input)
				{
					MpNote(num7, num8 + 2f * p, num3, draw: true, ModSync.Note, MpRed, 4.2f);
				}
				num8 += num5;
			}
			num8 += 4f * p;
			float num10 = (num3 - 8f * p) / 3f;
			Rect rect3 = new Rect(num7, num8, num10, 16f * p);
			Rect rect4 = new Rect(num7 + num10 + 4f * p, num8, num10, 16f * p);
			Rect rect5 = new Rect(num7 + (num10 + 4f * p) * 2f, num8, num10, 16f * p);
			if (!input)
			{
				GameArt.FlatButton(rect3, red: true, "Get them, restart", 4.4f, rect3.Contains(mousePosition) ? 1f : 0.9f);
				GameArt.FlatButton(rect4, red: false, "Join without", 4.4f, rect4.Contains(mousePosition) ? 1f : 0.85f);
				GameArt.FlatButton(rect5, red: false, "Leave", 4.4f, rect5.Contains(mousePosition) ? 1f : 0.85f);
				return;
			}
			if (GUI.Button(rect3, GUIContent.none, GUIStyle.none))
			{
				ModSync.GetThem(s);
			}
			if (GUI.Button(rect4, GUIContent.none, GUIStyle.none))
			{
				ModSync.SkipThem(s);
			}
			if (GUI.Button(rect5, GUIContent.none, GUIStyle.none))
			{
				ModSync.Asking = false;
				NetSession.Leave();
			}
		}

		private void DrawPlayers(Rect inner, Actor host)
		{
			float p = GameArt.P;
			NetSession current = NetSession.Current;
			if (current == null)
			{
				return;
			}
			float num = inner.y;
			float num2 = Mathf.Round(15f * p);
			foreach (KeyValuePair<int, float> item in new List<KeyValuePair<int, float>>(Parties.Pending))
			{
				if (Time.realtimeSinceStartup - item.Value > 120f)
				{
					Parties.Pending.Remove(item.Key);
					continue;
				}
				NetPlayer value;
				string text = (current.Players.TryGetValue(item.Key, out value) ? value.Name : "Someone");
				GameArt.Label(new Rect(inner.x, num, inner.width - 76f * p, num2), text + " invites you", 4.4f, TextAnchor.MiddleLeft, GameArt.Peach);
				if (GameButton(new Rect(inner.xMax - 74f * p, num, 36f * p, num2), "Join", red: true, enabled: true, null, 4.2f))
				{
					Parties.AcceptFrom(item.Key);
				}
				if (GameButton(new Rect(inner.xMax - 36f * p, num, 36f * p, num2), "No", red: false, enabled: true, null, 4.2f))
				{
					Parties.Decline(item.Key);
				}
				num += num2 + 3f * p;
			}
			Vector2 vector = host?.current_position ?? Vector2.zero;
			foreach (NetPlayer value2 in current.Players.Values)
			{
				float num3 = Mathf.Round(30f * p);
				Rect r = new Rect(inner.x, num, inner.width, num3);
				if (r.yMax > inner.yMax)
				{
					break;
				}
				GameArt.Sliced(r, GameArt.Inner, new Color(1f, 1f, 1f, 0.5f));
				bool flag = Parties.SameParty(value2, current.Me);
				Color color = ((value2 == current.Me) ? GameArt.Orange : (flag ? new Color(0.55f, 0.9f, 1f) : Color.white));
				GameArt.Label(new Rect(r.x + 5f * p, r.y + 1f * p, r.width * 0.6f, 13f * p), value2.Name + (value2.Host ? " (host)" : ""), 4.8f, TextAnchor.MiddleLeft, color);
				string text2 = ((value2.Body != 0L) ? BodyName(value2.Body) : "god view");
				try
				{
					Actor actor = ((value2.Body != 0L) ? World.world.units.get(value2.Body) : null);
					if (actor != null && host != null && value2 != current.Me)
					{
						text2 = text2 + ", " + Mathf.RoundToInt((actor.current_position - vector).magnitude) + " tiles";
					}
				}
				catch
				{
				}
				if (!value2.Host)
				{
					text2 = text2 + ", " + PingText(current, value2);
				}
				if (flag)
				{
					text2 += ", your party";
				}
				GameArt.Label(new Rect(r.x + 5f * p, r.y + 15f * p, r.width - 10f * p, 12f * p), text2, 3.9f, TextAnchor.MiddleLeft, GameArt.Grey);
				float num4 = r.xMax - 4f * p;
				if (value2 == current.Me)
				{
					if (value2.Party != 0 && GameButton(new Rect(num4 - 56f * p, r.y + 2f * p, 56f * p, 12f * p), "Leave party", red: false, enabled: true, null, 3.9f))
					{
						Parties.LeaveParty();
					}
				}
				else
				{
					if (!flag && GameButton(new Rect(num4 - 44f * p, r.y + 2f * p, 44f * p, 12f * p), "Invite", red: true, enabled: true, null, 3.9f))
					{
						Parties.InviteTo(value2);
					}
					if (current.IsHost && GameButton(new Rect(num4 - 90f * p, r.y + 2f * p, 40f * p, 12f * p), "Kick", red: false, enabled: true, null, 3.9f))
					{
						current.Kick(value2, "Kicked by the host", ban: false);
					}
				}
				num += num3 + 3f * p;
			}
			GameArt.Label(new Rect(inner.x, Mathf.Min(num + 2f * p, inner.yMax - 12f * p), inner.width, 12f * p), "/ or Enter: chat   /p: your party only", 3.9f, TextAnchor.MiddleLeft, GameArt.Grey);
		}

		private static bool NeedsBody(NetSession s)
		{
			try
			{
				if (ControllableUnit.isControllingUnit())
				{
					return false;
				}
				return s.IsHost || (WorldSync.Mirroring && WorldSync.MyBody == 0);
			}
			catch
			{
				return false;
			}
		}

		private void FindSpawnTowns()
		{
			_spawnTownsAt = Time.realtimeSinceStartup;
			_spawnTowns.Clear();
			try
			{
				Dictionary<City, int> dictionary = new Dictionary<City, int>();
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple.isAlive() && simple.city != null)
					{
						dictionary[simple.city] = ((!dictionary.TryGetValue(simple.city, out var value)) ? 1 : (value + 1));
					}
				}
				List<KeyValuePair<City, int>> list = new List<KeyValuePair<City, int>>(dictionary);
				list.Sort((KeyValuePair<City, int> x, KeyValuePair<City, int> y) => y.Value.CompareTo(x.Value));
				foreach (KeyValuePair<City, int> item in list)
				{
					if (_spawnTowns.Count >= 8)
					{
						break;
					}
					City key = item.Key;
					if (key.isAlive() && key.data != null)
					{
						string text = "";
						try
						{
							text = key.getActorAsset()?.id ?? "";
						}
						catch
						{
						}
						_spawnTowns.Add(new KeyValuePair<City, string>(key, (key.data.name ?? "A town") + "|" + text + ", " + item.Value + " people"));
					}
				}
			}
			catch
			{
			}
		}

		private void DrawSpawnPanel(NetSession s, float sw, float sh)
		{
			float num = GameArt.P * 0.7f;
			if (Time.realtimeSinceStartup - _spawnTownsAt > 2f)
			{
				FindSpawnTowns();
			}
			Actor actor = null;
			try
			{
				if (s.Me != null && s.Me.LastBody != 0L)
				{
					actor = World.world.units.get(s.Me.LastBody);
				}
			}
			catch
			{
			}
			if (actor != null && !actor.isAlive())
			{
				actor = null;
			}
			float num2 = Mathf.Round(150f * num);
			float num3 = Mathf.Round(18f * num);
			bool flag = !s.IsHost;
			float f = 22f * num + ((actor != null) ? (num3 + 4f * num) : 0f) + (float)_spawnTowns.Count * (num3 + 2f * num) + 26f * num + (flag ? (num3 + 2f * num) : 0f);
			Rect rect = new Rect(Mathf.Round(sw - num2 - 8f * num), Mathf.Round(62f * GameArt.P), num2, Mathf.Round(f));
			PanelBlocker.Cover(rect);
			GameArt.TipPanel(rect);
			Vector2 mousePosition = Event.current.mousePosition;
			float num4 = rect.x + 6f * num;
			float num5 = rect.y + 4f * num;
			float num6 = num2 - 12f * num;
			GameArt.Label(new Rect(num4, num5, num6, 14f * num), "Choose a body", 4.48f, TextAnchor.MiddleLeft, GameArt.Orange);
			num5 += 18f * num;
			if (actor != null)
			{
				Rect rect2 = new Rect(num4, num5, num6, num3);
				string text = "";
				try
				{
					text = actor.getName();
				}
				catch
				{
				}
				GameArt.FlatButton(rect2, red: true, "Your last body: " + text, 3.68f, rect2.Contains(mousePosition) ? 1f : 0.9f);
				PadNav.Spot(rect2);
				if (GUI.Button(rect2, GUIContent.none, GUIStyle.none))
				{
					WorldSync.AskLastBody();
				}
				num5 += num3 + 4f * num;
			}
			foreach (KeyValuePair<City, string> spawnTown in _spawnTowns)
			{
				string[] array = spawnTown.Value.Split('|');
				GameArt.Label(new Rect(num4, num5, num6 - 54f * num, num3 * 0.55f), array[0], 3.68f, TextAnchor.MiddleLeft, Color.white);
				GameArt.Label(new Rect(num4, num5 + num3 * 0.5f, num6 - 54f * num, num3 * 0.5f), (array.Length > 1) ? array[1] : "", 3.04f, TextAnchor.MiddleLeft, GameArt.Grey);
				Rect rect3 = new Rect(num4 + num6 - 52f * num, num5 + 1f * num, 52f * num, num3 - 2f * num);
				GameArt.FlatButton(rect3, red: false, "Be born", 3.5200002f, rect3.Contains(mousePosition) ? 1f : 0.85f);
				PadNav.Spot(rect3);
				if (GUI.Button(rect3, GUIContent.none, GUIStyle.none))
				{
					WorldSync.AskBorn(spawnTown.Key);
				}
				num5 += num3 + 2f * num;
			}
			if (_spawnTowns.Count == 0)
			{
				GameArt.Label(new Rect(num4, num5, num6, num3), "No towns yet", 3.5200002f, TextAnchor.MiddleLeft, GameArt.Grey);
				num5 += num3;
			}
			GameArt.Label(new Rect(num4, num5 + 2f * num, num6, 20f * num), "Or take any creature that isn't a player: select it, then the game's possess button.", 3.04f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
			if (flag)
			{
				Rect rect4 = new Rect(num4, num5 + 24f * num, num6, num3 - 2f * num);
				bool canResync = WorldSync.CanResync;
				GameArt.FlatButton(rect4, red: false, WorldSync.Resyncing ? "Resyncing" : "Resync with the host", 3.5200002f, (!canResync) ? 0.55f : (rect4.Contains(mousePosition) ? 1f : 0.85f));
				PadNav.Spot(rect4);
				if (GUI.Button(rect4, GUIContent.none, GUIStyle.none) && canResync)
				{
					WorldSync.AskResync();
				}
			}
		}

		private void DrawNameTags(NetSession s, bool firstPerson, float sw, float sh)
		{
			float p = GameArt.P;
			long num = WorldSync.LocalBodyId();
			foreach (NetPlayer value in s.Players.Values)
			{
				if (value.Body == 0L || value.Body == num || value == s.Me)
				{
					continue;
				}
				Actor actor = null;
				try
				{
					actor = World.world.units.get(value.Body);
				}
				catch
				{
				}
				if (actor == null || !actor.isAlive())
				{
					continue;
				}
				float num2;
				float num3;
				float num4;
				if (firstPerson)
				{
					FrameProjection shown = _mod.Shown;
					if (shown.Width <= 0 || !_mod.StarHead(actor, out var head) || !shown.Project(head.x, head.y, head.z + 0.35f, out var col, out var row, out var depth, out var _) || col < 0f || col > (float)shown.Width || row < 0f || row > (float)shown.Height || depth > 60f)
					{
						continue;
					}
					num2 = col / (float)shown.Width * sw;
					num3 = row / (float)shown.Height * sh;
					num4 = Mathf.Clamp(1.4f - depth / 60f, 0.6f, 1.2f);
				}
				else
				{
					Camera main = Camera.main;
					if (main == null)
					{
						continue;
					}
					Vector3 vector = main.WorldToScreenPoint(new Vector3(actor.current_position.x, actor.current_position.y + 2f, 0f));
					if (vector.z < 0f || vector.x < 0f || vector.x > sw || vector.y < 0f || vector.y > sh)
					{
						continue;
					}
					num2 = vector.x;
					num3 = sh - vector.y;
					num4 = 0.9f;
				}
				string name = value.Name;
				string text = "";
				try
				{
					text = actor.getName();
				}
				catch
				{
				}
				float size = 5f * num4;
				Vector2 vector2 = GameArt.Measure(name, size);
				Rect r = new Rect(num2 - vector2.x * 0.5f - 3f * p, num3 - 12f * p * num4, vector2.x + 6f * p, 9f * p * num4);
				GameArt.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), name, size, TextAnchor.MiddleCenter, new Color(0f, 0f, 0f, 0.7f));
				GameArt.Label(r, name, size, TextAnchor.MiddleCenter, new Color(0.55f, 0.9f, 1f));
				if (text != "" && text != name)
				{
					GameArt.Label(new Rect(num2 - 60f * p, num3 - 4f * p * num4, 120f * p, 7f * p * num4), text, 3.8f * num4, TextAnchor.MiddleCenter, new Color(0.85f, 0.85f, 0.85f, 0.85f));
				}
				DrawBubble(value.Id, num2, r.y - 2f * p, num4);
			}
		}

		private void DrawBubble(int player, float cx, float bottom, float scale)
		{
			float p = GameArt.P;
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			float num = 1f;
			string text;
			if (PlayerTalk.TypingNow(player))
			{
				text = new string('.', 1 + (int)(realtimeSinceStartup * 3f) % 3).PadRight(3);
			}
			else
			{
				if (!PlayerTalk.Bubbles.TryGetValue(player, out var value) || !(realtimeSinceStartup - value.At < 8f))
				{
					return;
				}
				text = value.Text;
				num = Mathf.Clamp01((8f - (realtimeSinceStartup - value.At)) / 0.6f);
			}
			float size = Mathf.Max(3.6f, 4.8f * scale);
			float num2 = Mathf.Round(130f * p * Mathf.Max(0.8f, scale));
			float num3 = 4f * p;
			float num4 = Mathf.Min(num2 - 2f * num3, GameArt.Measure(text, size).x * 1.1f + 2f);
			float num5 = MpNoteHeight(text, num4, size);
			Rect r = new Rect(Mathf.Round(cx - num4 * 0.5f - num3), Mathf.Round(bottom - num5 - 2f * num3 - 3f * p), Mathf.Round(num4 + 2f * num3), Mathf.Round(num5 + 2f * num3));
			GameArt.TipPanel(r, num);
			float num6 = Mathf.Max(1f, Mathf.Round(p));
			for (int i = 0; i < 3; i++)
			{
				GameArt.Flat(new Rect(Mathf.Round(cx - num6 * (2.5f - (float)i)), r.yMax + (float)i * num6 - num6, num6 * (float)(5 - i * 2), num6), new Color(0.12f, 0.1f, 0.1f, 0.9f * num));
			}
			GameArt.Label(new Rect(r.x + num3, r.y + num3, num4, num5), text, size, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, num), wrap: true);
		}

		public void DrawObelisk()
		{
			EnsureAssets();
			Royal royal = _mod.Royal;
			Actor host = _mod.Host;
			if (host == null || !Royal.IsKing(host))
			{
				royal.ObeliskOpen = false;
				return;
			}
			Kingdom kingdom = host.kingdom;
			Action<string> toast = _mod.Toast;
			bool close;
			Rect rect = GameWindow(WindowRect(540f, 320f), "The Obelisk", "E", out close);
			if (close)
			{
				royal.ObeliskOpen = false;
				return;
			}
			float p = GameArt.P;
			bool flag = Event.current.type == EventType.Repaint;
			float x = rect.x;
			float num = Mathf.Round(172f * p);
			float y = rect.y;
			Rect r = new Rect(x, y, num, Mathf.Round(50f * p));
			GameArt.Sliced(r, GameArt.Inner, Color.white);
			GameArt.Cover(new Rect(r.x + 3f * p, r.y + 3f * p, r.width - 6f * p, r.height - 6f * p), GameArt.Art("art_disasters") ?? GameArt.Art("art_monolith"), Color.white, Mathf.Sin(Time.unscaledTime * 0.11f) * 0.5f);
			y = r.yMax + 3f * p;
			GameArt.Label(new Rect(x + 2f * p, y, num - 4f * p, 16f * p), "The old stone answers your hand. Choose a village and the sky will fall on it.", 4.3f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
			y += 18f * p;
			long num2 = Royal.KillsToSpend(kingdom);
			if (flag)
			{
				GameArt.Icon(new Rect(x + 2f * p, y - 1f * p, 10f * p, 10f * p), GameArt.Kills);
				GameArt.Label(new Rect(x + 15f * p, y - 1f * p, num - 17f * p, 10f * p), "It asks for blood: " + num2 + ((num2 == 1) ? " kill" : " kills") + " to spend", 4.6f, TextAnchor.MiddleLeft, GameArt.Peach);
			}
			y += 11f * p;
			SectionTitle(new Rect(x, y, num, Mathf.Round(20f * p)), "Storms", GameArt.IconNamed("iconMonolith"));
			y += 22f * p;
			float num3 = Mathf.Round(20f * p);
			Royal.Storm[] stormKinds = Royal.StormKinds;
			for (int i = 0; i < stormKinds.Length; i++)
			{
				Royal.Storm s = stormKinds[i];
				float num4 = royal.CooldownLeft("storm_" + s);
				Rect r2 = new Rect(x, y, num, Mathf.Round(30f * p));
				GameArt.Sliced(r2, GameArt.Inner, new Color(0.92f, 0.92f, 0.92f, 1f));
				if (flag)
				{
					GameArt.StoneButton(new Rect(r2.x + 4f * p, r2.y + 5f * p, num3, num3), StormIcon(s), 1f, gems: true, (num4 > 0f) ? new Color(0.55f, 0.55f, 0.58f) : Color.white);
					float num5 = r2.x + num3 + 9f * p;
					GameArt.Label(new Rect(num5, r2.y + 2f * p, r2.xMax - num5 - 4f * p, 9f * p), Royal.Label(s), 5.2f, TextAnchor.MiddleLeft, Color.white);
					int num6 = Royal.CostOf(s);
					GameArt.Label(new Rect(num5, r2.y + 2f * p, r2.xMax - num5 - 5f * p, 9f * p), num6 + " kills  ·  " + ((num4 > 0f) ? (Mathf.CeilToInt(num4) + " s") : "ready"), 4.4f, TextAnchor.MiddleRight, (num2 < num6) ? new Color(1f, 0.5f, 0.42f) : ((num4 > 0f) ? GameArt.Grey : new Color(0.55f, 1f, 0.5f)));
					GameArt.Label(new Rect(num5, r2.y + 11f * p, r2.xMax - num5 - 4f * p, 17f * p), Royal.Describe(s), 3.9f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
				}
				y = r2.yMax + 2f * p;
			}
			y += 2f * p;
			SectionTitle(new Rect(x, y, num, Mathf.Round(20f * p)), "Raging now", GameArt.IconNamed("iconCloudLightning"));
			y += 22f * p;
			Rect r3 = new Rect(x, y, num, rect.yMax - y);
			if (r3.height > 10f * p)
			{
				GameArt.Sliced(r3, GameArt.Inner, new Color(1f, 1f, 1f, 0.6f));
				if (flag)
				{
					float num7 = r3.y + 4f * p;
					if (royal.Storms.Count == 0)
					{
						GameArt.Label(new Rect(r3.x + 6f * p, num7, r3.width - 12f * p, 9f * p), "The skies are quiet.", 4.4f, TextAnchor.UpperLeft, GameArt.Grey);
					}
					for (int j = 0; j < royal.Storms.Count && j < 4; j++)
					{
						if (!(num7 < r3.yMax - 8f * p))
						{
							break;
						}
						GameArt.Label(new Rect(r3.x + 6f * p, num7, r3.width - 12f * p, 16f * p), royal.Storms[j].Status ?? (Royal.Label(royal.Storms[j].Kind) + " gathering over " + royal.Storms[j].TargetName), 4.3f, TextAnchor.UpperLeft, new Color(1f, 0.55f, 0.45f), wrap: true);
						num7 += 17f * p;
					}
				}
			}
			float num8 = x + num + 8f * p;
			float width = rect.xMax - num8 - 9f * p;
			List<City> list = Royal.RivalVillages(kingdom, host.current_position);
			SectionTitle(new Rect(num8, rect.y, width, Mathf.Round(20f * p)), "Villages", GameArt.IconNamed("iconCityList") ?? GameArt.IconNamed("iconCity"));
			Rect view = new Rect(num8, rect.y + 22f * p, width, rect.height - 22f * p);
			float num9 = Mathf.Round(30f * p);
			float num10 = Mathf.Round(3f * p);
			float contentH = Mathf.Max(view.height, (float)list.Count * (num9 + num10));
			float num11 = BeginList(view, ref royal.ObeliskScroll, contentH);
			if (list.Count == 0)
			{
				GameArt.Label(new Rect(6f * p, num11 + 6f * p, view.width - 12f * p, 10f * p), "There are no other villages in the world to strike.", 4.8f, TextAnchor.MiddleLeft, GameArt.Grey);
			}
			for (int k = 0; k < list.Count; k++)
			{
				City city = list[k];
				Kingdom kingdom2 = city.kingdom;
				Rect r4 = new Rect(0f, num11 + (float)k * (num9 + num10), view.width, num9);
				bool flag2 = Royal.AtWar(kingdom, kingdom2);
				if (flag)
				{
					bool flag3 = false;
					try
					{
						flag3 = kingdom2 != null && city == kingdom2.capital;
					}
					catch
					{
					}
					GameArt.Sliced(r4, GameArt.Inner, new Color(0.92f, 0.92f, 0.92f, 1f));
					bool civ = false;
					try
					{
						civ = kingdom2?.isCiv() ?? false;
					}
					catch
					{
					}
					float num12 = r4.height - 6f * p;
					DrawBanner(new Rect(r4.x + 4f * p, r4.y + 3f * p, num12 * 0.8f, num12), kingdom2, civ, 1f);
					Color color = Color.white;
					try
					{
						color = kingdom2.getColor().getColorText();
					}
					catch
					{
					}
					float x2 = r4.x + num12 * 0.8f + 8f * p;
					GameArt.Label(new Rect(x2, r4.y + 3f * p, r4.width * 0.5f, 10f * p), city.name + (flag3 ? "  (capital)" : ""), 5.6f, TextAnchor.MiddleLeft, color);
					GameArt.Label(new Rect(x2, r4.y + 13.5f * p, r4.width * 0.55f, 8f * p), Royal.NameOf(kingdom2) + "  ·  " + SafeCount(city) + " people  ·  " + Mathf.RoundToInt(Royal.DistanceTo(city, host.current_position)) + " tiles" + (flag2 ? "  ·  at war" : ""), 4.2f, TextAnchor.MiddleLeft, flag2 ? new Color(1f, 0.55f, 0.45f) : GameArt.Grey);
				}
				float num13 = Mathf.Round(22f * p);
				float num14 = r4.xMax - 5f * p;
				for (int num15 = Royal.StormKinds.Length - 1; num15 >= 0; num15--)
				{
					Royal.Storm storm = Royal.StormKinds[num15];
					num14 -= num13;
					float wait = royal.CooldownLeft("storm_" + storm);
					if (CostButton(new Rect(num14, r4.y + (num9 - num13) * 0.5f, num13, num13), StormIcon(storm), null, wait, Royal.CooldownOf(storm), Royal.CostOf(storm), num2, _listMouseIn))
					{
						royal.CallStorm(host, city, storm, toast);
					}
					num14 -= 4f * p;
				}
			}
			EndList(view, ref royal.ObeliskScroll, contentH);
		}

		private static void PadSpot(Rect r)
		{
			if (!_padLooked)
			{
				_padLooked = true;
				try
				{
					MethodInfo methodInfo = typeof(Hud).Assembly.GetType("FirstPerson.Pad.PadNav")?.GetMethod("Spot", new Type[2]
					{
						typeof(Rect),
						typeof(bool)
					});
					if (methodInfo != null)
					{
						_padSpot = (Action<Rect, bool>)Delegate.CreateDelegate(typeof(Action<Rect, bool>), methodInfo);
					}
				}
				catch
				{
				}
			}
			_padSpot?.Invoke(r, arg2: false);
		}

		private float ModsPageHeight(float cw)
		{
			return Mathf.Round(90f * GameArt.P);
		}

		private float DrawModsPage(float cx, float y, float cw)
		{
			float p = GameArt.P;
			Rect r = new Rect(cx, y, cw, ModsPageHeight(cw));
			GameArt.Sliced(r, GameArt.Inner, new Color(1f, 1f, 1f, 0.56f));
			List<PackInfo> active = Packs.Active;
			int num = 0;
			foreach (PackInfo item in Packs.All)
			{
				if (item.Errors.Count > 0)
				{
					num++;
				}
			}
			float num2 = y + 6f * p;
			GameArt.Label(new Rect(cx + 8f * p, num2, cw - 16f * p, 10f * p), (active.Count == 0) ? "No packs on" : (active.Count + ((active.Count == 1) ? " pack on" : " packs on") + ", " + Packs.Available.Count + " off"), 5.5f, TextAnchor.MiddleLeft, Color.white);
			num2 += 11f * p;
			string text = ((active.Count > 0) ? ("Top: " + active[0].Title) : "Packs replace models, sounds and text");
			GameArt.Label(new Rect(cx + 8f * p, num2, cw - 16f * p, 10f * p), text, 4.6f, TextAnchor.MiddleLeft, GameArt.Grey);
			num2 += 10f * p;
			if (num > 0)
			{
				GameArt.Label(new Rect(cx + 8f * p, num2, cw - 16f * p, 10f * p), num + ((num == 1) ? " pack has errors" : " packs have errors"), 4.6f, TextAnchor.MiddleLeft, Bad);
			}
			num2 += 14f * p;
			float num3 = Mathf.Round((cw - 24f * p) / 3f);
			float num4 = Mathf.Round(18f * p);
			for (int i = 0; i < 3; i++)
			{
				Rect r2 = new Rect(Mathf.Round(cx + 8f * p + (float)i * (num3 + 4f * p)), Mathf.Round(num2), num3, num4);
				if (GameButton(r2, (PacksUi.Pages[i] == "Installed") ? "Manage" : PacksUi.Pages[i], i == 0, enabled: true, null, 5.5f, _pageMouseIn))
				{
					PacksUi.OpenAt(i);
				}
			}
			num2 += num4 + 6f * p;
			GameArt.Label(new Rect(cx + 8f * p, num2, cw - 16f * p, 18f * p), "Workshop packs need Steam. Local packs go in FirstPerson/packs", 4.3f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
			return y + r.height + 4f * p;
		}

		public bool DrawPacksWindow()
		{
			GUI.depth = -1000;
			float p = GameArt.P;
			Rect win = WindowRect(600f, 340f);
			bool close;
			Rect rect = GameWindow(win, "Mods", null, out close);
			if (close)
			{
				PacksUi.Open = false;
				Sounds.PlayUI("event:/SFX/UI/WindowClose");
				return false;
			}
			bool flag = PacksUi.Confirm != null;
			Keys(flag);
			float num = Mathf.Round(16f * p);
			float num2 = Mathf.Round(70f * p);
			for (int i = 0; i < PacksUi.Pages.Length; i++)
			{
				Rect r = new Rect(Mathf.Round(rect.x + (float)i * (num2 + 3f * p)), Mathf.Round(rect.y), num2, num);
				if (GameButton(r, PacksUi.Pages[i], PacksUi.Page == i, enabled: true, null, 5.2f, !flag) && PacksUi.Page != i)
				{
					PacksUi.Page = i;
					PacksUi.Details = null;
					if (i == 1 && !PacksUi.Searched)
					{
						PacksUi.RunSearch();
					}
				}
			}
			Rect r2 = new Rect(Mathf.Round(rect.xMax - num2), Mathf.Round(rect.y), num2, num);
			if (GameButton(r2, "Pack folder", red: false, enabled: true, null, 5f, !flag))
			{
				PacksUi.ShowFolder(Packs.LocalFolder);
			}
			Rect r3 = new Rect(Mathf.Round(r2.x - num2 * 0.8f - 3f * p), Mathf.Round(rect.y), Mathf.Round(num2 * 0.8f), num);
			if (GameButton(r3, "Refresh", red: false, enabled: true, null, 5f, !flag))
			{
				PacksUi.Rescan();
				PacksUi.Say(Packs.All.Count + " packs found");
			}
			Rect body = new Rect(rect.x, rect.y + num + 4f * p, rect.width, rect.height - num - 4f * p - 14f * p);
			if (PacksUi.Page == 0)
			{
				if (PacksUi.Details != null && Packs.Find(PacksUi.Details) != null)
				{
					DrawPackDetails(body, Packs.Find(PacksUi.Details), !flag);
				}
				else
				{
					PacksUi.Details = null;
					DrawInstalled(body, !flag);
				}
			}
			else if (PacksUi.Page == 1)
			{
				DrawBrowse(body, !flag);
			}
			else
			{
				DrawPublish(body, !flag);
			}
			Rect r4 = new Rect(rect.x, rect.yMax - 12f * p, rect.width, 12f * p);
			bool flag2 = Time.unscaledTime < PacksUi.NoteUntil && PacksUi.Note.Length > 0;
			string text = (Packs.Busy ? "Applying" : null);
			GameArt.Label(r4, flag2 ? PacksUi.Note : (text ?? FootKeys()), 4.4f, TextAnchor.MiddleLeft, flag2 ? Color.white : GameArt.Grey);
			if (flag)
			{
				DrawConfirm(win);
			}
			return false;
		}

		private static string FootKeys()
		{
			if (PacksUi.Page != 0)
			{
				if (PacksUi.Page != 1)
				{
					return "Dry run shows what would be sent. Tab next page";
				}
				return "Enter searches. Tab next page";
			}
			return "Enter on or off. Shift+Up/Down order. I details. Del remove. Tab next page";
		}

		private void Keys(bool modal)
		{
			Event current = Event.current;
			if (current.type != EventType.KeyDown)
			{
				return;
			}
			if (modal)
			{
				if (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter)
				{
					PacksUi.Confirmed();
					current.Use();
				}
				else if (current.keyCode == KeyCode.Backspace || current.keyCode == KeyCode.N)
				{
					PacksUi.Confirm = null;
					current.Use();
				}
			}
			else if (GUIUtility.keyboardControl != 0)
			{
				if ((current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter) && PacksUi.Page == 1)
				{
					PacksUi.RunSearch();
					GUIUtility.keyboardControl = 0;
					current.Use();
				}
			}
			else if (current.keyCode == KeyCode.Tab)
			{
				PacksUi.Page = (PacksUi.Page + ((!current.shift) ? 1 : 2)) % 3;
				PacksUi.Details = null;
				if (PacksUi.Page == 1 && !PacksUi.Searched)
				{
					PacksUi.RunSearch();
				}
				current.Use();
			}
			else
			{
				if (PacksUi.Page != 0)
				{
					return;
				}
				string text = PacksUi.FocusedKey();
				if (PacksUi.Details != null)
				{
					if (current.keyCode == KeyCode.Backspace || current.keyCode == KeyCode.I)
					{
						PacksUi.Details = null;
						current.Use();
					}
					return;
				}
				switch (current.keyCode)
				{
				default:
					return;
				case KeyCode.UpArrow:
				case KeyCode.DownArrow:
				{
					int num = ((current.keyCode != KeyCode.UpArrow) ? 1 : (-1));
					if (current.shift && PacksUi.FocusActive && text != null)
					{
						PacksUi.MoveActive(text, num);
						break;
					}
					PacksUi.Focus += num;
					PacksUi.ClampFocus();
					break;
				}
				case KeyCode.LeftArrow:
					PacksUi.FocusActive = false;
					PacksUi.ClampFocus();
					break;
				case KeyCode.RightArrow:
					PacksUi.FocusActive = true;
					PacksUi.ClampFocus();
					break;
				case KeyCode.Return:
				case KeyCode.Space:
				case KeyCode.KeypadEnter:
					if (text != null)
					{
						PacksUi.Toggle(text);
					}
					break;
				case KeyCode.Delete:
					if (text != null)
					{
						PacksUi.AskRemove(text);
					}
					break;
				case KeyCode.I:
					if (text != null)
					{
						PacksUi.Details = text;
					}
					break;
				}
				current.Use();
			}
		}

		private void DrawInstalled(Rect body, bool live)
		{
			float p = GameArt.P;
			float num = 10f * p;
			float num2 = Mathf.Round((body.width - num) * 0.5f);
			_cardH = Mathf.Round(44f * p);
			_cardGap = Mathf.Round(3f * p);
			Rect rect = new Rect(body.x, body.y, num2, body.height);
			Rect rect2 = new Rect(body.xMax - num2, body.y, num2, body.height);
			List<PackInfo> available = Packs.Available;
			List<PackInfo> active = Packs.Active;
			SectionTitle(new Rect(rect.x, rect.y, rect.width, 16f * p), "Available (" + available.Count + ")");
			SectionTitle(new Rect(rect2.x, rect2.y, rect2.width, 16f * p), "Active, top wins (" + active.Count + ")");
			_availView = new Rect(rect.x, rect.y + 19f * p, rect.width - 8f * p, rect.height - 19f * p);
			_activeView = new Rect(rect2.x, rect2.y + 19f * p, rect2.width - 8f * p, rect2.height - 19f * p);
			GameArt.Sliced(_availView, GameArt.Inner, new Color(1f, 1f, 1f, 0.45f));
			GameArt.Sliced(_activeView, GameArt.Inner, new Color(1f, 1f, 1f, 0.45f));
			Dictionary<string, List<PackInfo>> clashes = Packs.Clashes();
			DrawPackList(_availView, available, active: false, ref PacksUi.ScrollAvailable, clashes, live);
			DrawPackList(_activeView, active, active: true, ref PacksUi.ScrollActive, clashes, live);
			if (available.Count == 0)
			{
				GameArt.Label(new Rect(_availView.x + 8f * p, _availView.y + 8f * p, _availView.width - 16f * p, 40f * p), (Packs.All.Count == 0) ? "No packs installed. Find some on the Browse page, or make one in the pack folder" : "Every pack is on", 4.8f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
			}
			if (active.Count == 0)
			{
				GameArt.Label(new Rect(_activeView.x + 8f * p, _activeView.y + 8f * p, _activeView.width - 16f * p, 30f * p), "Drag a pack here, or press its arrow", 4.8f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
			}
			DragCards(live);
		}

		private void DrawPackList(Rect view, List<PackInfo> list, bool active, ref Vector2 scroll, Dictionary<string, List<PackInfo>> clashes, bool live)
		{
			float p = GameArt.P;
			float contentH = (float)list.Count * (_cardH + _cardGap) + 4f * p;
			float num = BeginList(view, ref scroll, contentH);
			for (int i = 0; i < list.Count; i++)
			{
				Rect r = new Rect(3f * p, num + 3f * p + (float)i * (_cardH + _cardGap), view.width - 6f * p, _cardH);
				bool focused = PacksUi.FocusActive == active && PacksUi.Focus == i && PacksUi.Details == null;
				bool dragged = PacksUi.Dragging && PacksUi.PressKey == list[i].Key;
				DrawPackCard(r, list[i], active, i, list.Count, focused, dragged, clashes, live && _listMouseIn);
			}
			EndList(view, ref scroll, contentH);
		}

		private void DrawPackCard(Rect r, PackInfo p, bool active, int index, int count, bool focused, bool dragged, Dictionary<string, List<PackInfo>> clashes, bool live)
		{
			float p2 = GameArt.P;
			if (Event.current.type == EventType.Repaint)
			{
				GameArt.Sliced(r, GameArt.Well ?? GameArt.Inner, dragged ? new Color(1f, 1f, 1f, 0.35f) : Color.white, 4f, 4f, 4f, 4f);
				if (focused)
				{
					GameArt.Sliced(r, GameArt.ButtonBorder, new Color(1f, 0.85f, 0.35f, 0.95f), 4f, 4f, 4f, 4f, p2);
				}
				float num = r.height - 6f * p2;
				Rect rect = new Rect(r.x + 3f * p2, r.y + 3f * p2, num, num);
				Texture2D texture2D = PacksUi.PictureOf(p);
				if (texture2D != null)
				{
					GUI.DrawTexture(rect, texture2D, ScaleMode.ScaleAndCrop);
				}
				else
				{
					GameArt.Flat(rect, new Color(0.15f, 0.16f, 0.2f, 1f));
					GameArt.Label(rect, (p.Title.Length > 0) ? p.Title.Substring(0, 1).ToUpperInvariant() : "?", 12f, TextAnchor.MiddleCenter, GameArt.Grey);
				}
				float num2 = rect.xMax + 4f * p2;
				float width = r.xMax - num2 - 32f * p2;
				GameArt.Label(new Rect(num2, r.y + 2f * p2, width, 10f * p2), p.Title, 5.4f, TextAnchor.MiddleLeft, Color.white);
				GameArt.Label(new Rect(num2, r.y + 11f * p2, width, 8f * p2), p.Author + "  v" + p.Version + "  " + p.SourceName, 4.1f, TextAnchor.MiddleLeft, GameArt.Grey);
				string text = string.Join(", ", p.Kinds.ToArray());
				int num3 = (active ? Overruled(p, clashes) : 0);
				string text2 = p.Overrides + ((p.Overrides == 1) ? " replacement" : " replacements") + ((num3 > 0) ? (", " + num3 + " under others") : "");
				GameArt.Label(new Rect(num2, r.y + 19f * p2, width, 8f * p2), ((text.Length > 0) ? (text + "  ") : "") + text2, 4.1f, TextAnchor.MiddleLeft, GameArt.Peach);
				List<string> value;
				int num4 = p.Errors.Count + (Packs.Problems.TryGetValue(p.Key, out value) ? value.Count : 0);
				List<string> value2;
				string text3 = ((num4 > 0) ? (num4 + ((num4 == 1) ? " error" : " errors")) : ((p.Warnings.Count > 0) ? p.Warnings[0] : ((Packs.Notes.TryGetValue(p.Key, out value2) && value2.Count > 0) ? value2[0] : MissingNote(p))));
				if (text3 != null)
				{
					GameArt.Label(new Rect(num2, r.y + 28f * p2, width, 8f * p2), text3, 4.1f, TextAnchor.MiddleLeft, (num4 > 0) ? Bad : Warn);
				}
			}
			float num5 = Mathf.Round(13f * p2);
			float num6 = r.xMax - num5 - 3f * p2;
			float num7 = r.y + 3f * p2;
			if (CardButton(new Rect(num6, num7, num5, num5), active ? "<" : ">", live))
			{
				PacksUi.Toggle(p.Key);
			}
			if (active)
			{
				if (CardButton(new Rect(num6 - num5 - 2f * p2, num7, num5, num5), "^", live, index > 0))
				{
					PacksUi.MoveActive(p.Key, -1);
				}
				if (CardButton(new Rect(num6 - num5 - 2f * p2, num7 + num5 + 2f * p2, num5, num5), "v", live, index < count - 1))
				{
					PacksUi.MoveActive(p.Key, 1);
				}
			}
			if (CardButton(new Rect(num6, num7 + num5 + 2f * p2, num5, num5), "i", live))
			{
				PacksUi.Details = p.Key;
			}
			if (CardButton(new Rect(num6, num7 + 2f * (num5 + 2f * p2), num5, Mathf.Min(num5, r.yMax - (num7 + 2f * (num5 + 2f * p2)) - 2f * p2)), "x", live))
			{
				PacksUi.AskRemove(p.Key);
			}
			List<ulong> list = Missing(p);
			if (list.Count > 0 && CardButton(new Rect(r.x + r.height, r.yMax - 10f * p2, 52f * p2, 9f * p2), "Subscribe", live))
			{
				foreach (ulong item in list)
				{
					PacksUi.Say(PackSteam.Subscribe(item) ?? "Subscribing to what it needs");
				}
			}
			if (live)
			{
				PadSpot(new Rect(r.x, r.y, r.width - num5 - 6f * p2, r.height));
			}
			Event current = Event.current;
			if (live && current.type == EventType.MouseDown && current.button == 0 && r.Contains(current.mousePosition))
			{
				PacksUi.PressKey = p.Key;
				PacksUi.PressActive = active;
				PacksUi.PressAt = GUIUtility.GUIToScreenPoint(current.mousePosition);
				PacksUi.Dragging = false;
				PacksUi.FocusActive = active;
				PacksUi.Focus = index;
				current.Use();
			}
		}

		private static string MissingNote(PackInfo p)
		{
			int count = Missing(p).Count;
			if (count <= 0)
			{
				return null;
			}
			return "Needs " + count + ((count == 1) ? " item" : " items") + " from the Workshop";
		}

		private static List<ulong> Missing(PackInfo p)
		{
			if (p.Requires.Count == 0)
			{
				return new List<ulong>();
			}
			if (MissingCache.TryGetValue(p.Key, out var value) && Time.unscaledTime < value.Key)
			{
				return value.Value;
			}
			List<ulong> list = new List<ulong>();
			foreach (ulong id in p.Requires)
			{
				bool flag = Packs.All.Exists((PackInfo o) => o.WorkshopId == id);
				try
				{
					flag = flag || PackSteam.IsInstalled(id);
				}
				catch
				{
				}
				if (!flag)
				{
					list.Add(id);
				}
			}
			MissingCache[p.Key] = new KeyValuePair<float, List<ulong>>(Time.unscaledTime + 3f, list);
			return list;
		}

		private static int Overruled(PackInfo p, Dictionary<string, List<PackInfo>> clashes)
		{
			int num = 0;
			foreach (KeyValuePair<string, List<PackInfo>> clash in clashes)
			{
				if (clash.Value.Count > 1 && clash.Value[0] != p && clash.Value.Contains(p))
				{
					num++;
				}
			}
			return num;
		}

		private bool CardButton(Rect r, string label, bool live, bool enabled = true)
		{
			live = live && enabled;
			bool flag = live && r.Contains(Event.current.mousePosition);
			if (Event.current.type == EventType.Repaint)
			{
				Color color = GUI.color;
				if (!enabled)
				{
					GUI.color = new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f, color.a * 0.6f);
				}
				GameArt.FlatButton(r, label == ">" || label == "<", label, 5f, flag ? 1f : 0.86f);
				GUI.color = color;
			}
			if (live)
			{
				PadSpot(r);
			}
			if (!GUI.Button(r, GUIContent.none, GUIStyle.none) || !live)
			{
				return false;
			}
			Sounds.PlayUI("event:/SFX/Ui/Click");
			return true;
		}

		private void DragCards(bool live)
		{
			if (PacksUi.PressKey == null)
			{
				return;
			}
			Event current = Event.current;
			Vector2 vector = GUIUtility.GUIToScreenPoint(current.mousePosition);
			float p = GameArt.P;
			if (current.type == EventType.MouseDrag && !PacksUi.Dragging && (vector - PacksUi.PressAt).magnitude > 5f * p)
			{
				PacksUi.Dragging = true;
			}
			bool flag = _activeView.Contains(current.mousePosition);
			bool flag2 = _availView.Contains(current.mousePosition);
			int num = DropIndex(current.mousePosition.y);
			if (PacksUi.Dragging && current.type == EventType.Repaint)
			{
				PackInfo packInfo = Packs.Find(PacksUi.PressKey);
				if (packInfo != null)
				{
					if (flag)
					{
						float y = _activeView.y + 3f * p + (float)num * (_cardH + _cardGap) - Mathf.Round(PacksUi.ScrollActive.y) - _cardGap * 0.5f - p;
						GameArt.Flat(new Rect(_activeView.x + 4f * p, y, _activeView.width - 8f * p, 2f * p), GameArt.Orange);
					}
					Rect r = new Rect(current.mousePosition.x - 60f * p, current.mousePosition.y - 8f * p, 120f * p, 16f * p);
					GameArt.TipPanel(r);
					GameArt.Label(r, packInfo.Title, 5f, TextAnchor.MiddleCenter, Color.white);
				}
			}
			if (current.type == EventType.MouseDrag && PacksUi.Dragging)
			{
				current.Use();
			}
			if (current.rawType != EventType.MouseUp)
			{
				return;
			}
			string pressKey = PacksUi.PressKey;
			bool dragging = PacksUi.Dragging;
			PacksUi.PressKey = null;
			PacksUi.Dragging = false;
			if (!live)
			{
				return;
			}
			if (dragging)
			{
				if (flag)
				{
					PacksUi.Drop(pressKey, ontoActive: true, num);
				}
				else if (flag2 && Packs.IsActive(pressKey))
				{
					PacksUi.Drop(pressKey, ontoActive: false, 0);
				}
			}
			else if (PacksUi.LastClickKey == pressKey && Time.unscaledTime - PacksUi.LastClickAt < 0.35f)
			{
				PacksUi.Toggle(pressKey);
				PacksUi.LastClickAt = -1f;
			}
			else
			{
				PacksUi.LastClickKey = pressKey;
				PacksUi.LastClickAt = Time.unscaledTime;
			}
			current.Use();
		}

		private int DropIndex(float y)
		{
			float num = y - _activeView.y - 3f * GameArt.P + Mathf.Round(PacksUi.ScrollActive.y);
			return Mathf.Clamp(max: Packs.ActiveKeys.Count, value: Mathf.FloorToInt(num / (_cardH + _cardGap) + 0.5f), min: 0);
		}

		private void DrawPackDetails(Rect body, PackInfo p, bool live)
		{
			float p2 = GameArt.P;
			Rect r = new Rect(body.x, body.y, 50f * p2, 15f * p2);
			if (GameButton(r, "Back", red: false, enabled: true, null, 5f, live))
			{
				PacksUi.Details = null;
				return;
			}
			bool flag = Packs.IsActive(p.Key);
			Rect r2 = new Rect(r.xMax + 4f * p2, body.y, 60f * p2, 15f * p2);
			if (GameButton(r2, flag ? "Turn off" : "Turn on", !flag, enabled: true, null, 5f, live))
			{
				PacksUi.Toggle(p.Key);
			}
			Rect r3 = new Rect(r2.xMax + 4f * p2, body.y, 60f * p2, 15f * p2);
			if (GameButton(r3, "Its folder", red: false, enabled: true, null, 5f, live))
			{
				PacksUi.ShowFolder(p.ModFolder);
			}
			Rect r4 = new Rect(r3.xMax + 4f * p2, body.y, 50f * p2, 15f * p2);
			if (GameButton(r4, "Remove", red: false, enabled: true, null, 5f, live))
			{
				PacksUi.AskRemove(p.Key);
			}
			List<KeyValuePair<string, Color>> lines = new List<KeyValuePair<string, Color>>();
			Add(p.Title + "  by " + p.Author + "  v" + p.Version, Color.white);
			Add(p.SourceName + ((p.WorkshopId != 0L) ? ("  item " + p.WorkshopId) : "") + "  id " + p.Id, GameArt.Grey);
			if (!string.IsNullOrEmpty(p.Description))
			{
				Add(p.Description, GameArt.Peach);
			}
			if (!string.IsNullOrEmpty(p.MinWorldfall))
			{
				Add("Made for Worldfall " + p.MinWorldfall + (p.NewerWorldfall ? " or later. This is 0.9.8.9" : ""), p.NewerWorldfall ? Warn : GameArt.Grey);
			}
			if (p.LoadAfter.Count > 0)
			{
				Add("Goes above " + string.Join(", ", p.LoadAfter.ToArray()), GameArt.Grey);
			}
			foreach (string error in p.Errors)
			{
				Add(error, Bad);
			}
			if (Packs.Problems.TryGetValue(p.Key, out var value))
			{
				foreach (string item in value)
				{
					Add(item, Bad);
				}
			}
			foreach (string warning in p.Warnings)
			{
				Add(warning, Warn);
			}
			if (Packs.Notes.TryGetValue(p.Key, out var value2))
			{
				foreach (string item2 in value2)
				{
					Add(item2, Warn);
				}
			}
			Add("", Color.white);
			Add("Replaces", GameArt.Orange);
			Dictionary<string, List<PackInfo>> dictionary = Packs.Clashes();
			List<string> list = new List<string>();
			foreach (string key in p.ModelFiles.Keys)
			{
				list.Add("model:" + key);
			}
			foreach (string key2 in p.SoundFiles.Keys)
			{
				list.Add("sound:" + key2);
			}
			foreach (string key3 in p.LangFiles.Keys)
			{
				list.Add("lang:" + key3);
			}
			list.Sort(StringComparer.Ordinal);
			foreach (string item3 in list)
			{
				string text = item3.Substring(0, item3.IndexOf(':'));
				string text2 = item3.Substring(item3.IndexOf(':') + 1);
				string text3 = ((text == "model") ? "Model " : ((text == "sound") ? "Sounds " : "Language ")) + text2;
				if (text == "model" && !Models.Known(text2) && PackModelNames.IsAskedFor(text2))
				{
					text3 += " (new creature)";
				}
				if (!flag)
				{
					Add(text3, Color.white);
					continue;
				}
				dictionary.TryGetValue(item3, out var value3);
				if (value3 == null || value3.Count <= 1)
				{
					Add(text3 + "  wins", Good);
				}
				else if (value3[0] == p)
				{
					Add(text3 + "  wins over " + string.Join(", ", value3.GetRange(1, value3.Count - 1).ConvertAll((PackInfo o) => o.Title).ToArray()), Good);
				}
				else
				{
					Add(text3 + "  under " + value3[0].Title, Warn);
				}
			}
			if (list.Count == 0)
			{
				Add("Nothing", GameArt.Grey);
			}
			Rect rect = new Rect(body.x, body.y + 19f * p2, body.width - 8f * p2, body.height - 19f * p2);
			GameArt.Sliced(rect, GameArt.Inner, new Color(1f, 1f, 1f, 0.45f));
			float num = 10f * p2;
			float num2 = 64f * p2;
			float contentH = Mathf.Max(num2 + 8f * p2, (float)lines.Count * num + 8f * p2);
			float num3 = BeginList(rect, ref PacksUi.ScrollDetails, contentH);
			Texture2D texture2D = PacksUi.PictureOf(p);
			if (texture2D != null && Event.current.type == EventType.Repaint)
			{
				GUI.DrawTexture(new Rect(rect.width - num2 - 6f * p2, num3 + 4f * p2, num2, num2), texture2D, ScaleMode.ScaleAndCrop);
			}
			for (int num4 = 0; num4 < lines.Count; num4++)
			{
				GameArt.Label(new Rect(6f * p2, num3 + 4f * p2 + (float)num4 * num, rect.width - num2 - 18f * p2, num), lines[num4].Key, 4.6f, TextAnchor.MiddleLeft, lines[num4].Value);
			}
			EndList(rect, ref PacksUi.ScrollDetails, contentH);
			void Add(string s, Color c)
			{
				lines.Add(new KeyValuePair<string, Color>(s, c));
			}
		}

		private void DrawConfirm(Rect win)
		{
			float p = GameArt.P;
			PackInfo packInfo = Packs.Find(PacksUi.ConfirmKey);
			if (packInfo == null)
			{
				PacksUi.Confirm = null;
				return;
			}
			if (Event.current.type == EventType.Repaint)
			{
				Fill(win, new Color(0f, 0f, 0f, 0.45f));
			}
			Rect r = new Rect(Mathf.Round(win.center.x - 120f * p), Mathf.Round(win.center.y - 40f * p), Mathf.Round(240f * p), Mathf.Round(80f * p));
			GameArt.Window(r, Color.white);
			string confirm = PacksUi.Confirm;
			string text;
			string text2;
			string label;
			if (!(confirm == "remove"))
			{
				if (confirm == "newer")
				{
					text = "Turn on " + packInfo.Title + "?";
					text2 = "It is made for Worldfall " + packInfo.MinWorldfall + ". This is 0.9.8.9";
					label = "Turn on";
				}
				else
				{
					text = "Publish " + packInfo.Title + "?";
					text2 = ((packInfo.WorkshopId != 0L) ? ("Updates item " + packInfo.WorkshopId) : "A new Workshop item") + ", " + PackPublish.Visibilities[PacksUi.Visibility].ToLowerInvariant();
					label = "Upload";
				}
			}
			else
			{
				text = "Remove " + packInfo.Title + "?";
				text2 = ((packInfo.Source == PackSource.Workshop) ? "You are unsubscribed and Steam deletes its files" : "Its folder goes to the recycle bin");
				label = "Remove";
			}
			GameArt.Label(new Rect(r.x + 10f * p, r.y + 6f * p, r.width - 20f * p, 14f * p), text, 6f, TextAnchor.MiddleCenter, GameArt.Orange);
			GameArt.Label(new Rect(r.x + 12f * p, r.y + 24f * p, r.width - 24f * p, 22f * p), text2, 4.8f, TextAnchor.UpperCenter, Color.white, wrap: true);
			Rect r2 = new Rect(Mathf.Round(r.center.x - 74f * p), Mathf.Round(r.yMax - 24f * p), Mathf.Round(70f * p), Mathf.Round(16f * p));
			Rect r3 = new Rect(Mathf.Round(r.center.x + 4f * p), r2.y, r2.width, r2.height);
			if (GameButton(r2, label, red: true))
			{
				PacksUi.Confirmed();
			}
			if (GameButton(r3, "Cancel", red: false))
			{
				PacksUi.Confirm = null;
			}
		}

		private void DrawBrowse(Rect body, bool live)
		{
			float p = GameArt.P;
			bool flag;
			try
			{
				flag = PackSteam.Available;
			}
			catch
			{
				flag = false;
			}
			if (!flag)
			{
				GameArt.Label(new Rect(body.x + 8f * p, body.y + 10f * p, body.width - 16f * p, 30f * p), "The Workshop needs Steam running and signed in", 5.5f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
				return;
			}
			float num = Mathf.Round(15f * p);
			Rect r = new Rect(body.x, body.y, Mathf.Round(170f * p), num);
			PacksUi.Search = GameTextField(r, PacksUi.Search, 64, "packs_search", 5.5f);
			Rect r2 = new Rect(r.xMax + 3f * p, body.y, 50f * p, num);
			if (GameButton(r2, "Search", red: true, !PackSteam.Querying, null, 5f, live))
			{
				PacksUi.RunSearch();
			}
			float num2 = r2.xMax + 8f * p;
			for (int i = 0; i < PackSteam.Sorts.Length; i++)
			{
				float num3 = GameArt.Measure(PackSteam.Sorts[i], 4.6f).x + 10f * p;
				if (GameButton(new Rect(num2, body.y, num3, num), PackSteam.Sorts[i], PacksUi.Sort == i, enabled: true, null, 4.6f, live) && PacksUi.Sort != i)
				{
					PacksUi.Sort = i;
					PacksUi.RunSearch();
				}
				num2 += num3 + 2f * p;
			}
			float num4 = body.y + num + 3f * p;
			num2 = body.x;
			for (int j = 0; j < PacksUi.KindFilter.Length; j++)
			{
				float num5 = GameArt.Measure(PacksUi.KindFilter[j], 4.6f).x + 10f * p;
				if (GameButton(new Rect(num2, num4, num5, num), PacksUi.KindFilter[j], PacksUi.Kind == j, enabled: true, null, 4.6f, live) && PacksUi.Kind != j)
				{
					PacksUi.Kind = j;
					PacksUi.RunSearch();
				}
				num2 += num5 + 2f * p;
			}
			int num6 = Mathf.Max(1, (PackSteam.Total + 49) / 50);
			Rect r3 = new Rect(body.xMax - 40f * p, num4, 40f * p, num);
			Rect r4 = new Rect(r3.x - 43f * p, num4, 40f * p, num);
			if (GameButton(r4, "Prev", red: false, PackSteam.Page > 1 && !PackSteam.Querying, null, 4.6f, live))
			{
				PackSteam.Query(PacksUi.Search, (PacksUi.Kind == 0) ? null : PacksUi.KindFilter[PacksUi.Kind].ToLowerInvariant(), PacksUi.Sort, PackSteam.Page - 1);
			}
			if (GameButton(r3, "Next", red: false, PackSteam.Page < num6 && !PackSteam.Querying, null, 4.6f, live))
			{
				PackSteam.Query(PacksUi.Search, (PacksUi.Kind == 0) ? null : PacksUi.KindFilter[PacksUi.Kind].ToLowerInvariant(), PacksUi.Sort, PackSteam.Page + 1);
			}
			GameArt.Label(new Rect(r4.x - 120f * p, num4, 116f * p, num), PackSteam.Status + ((PackSteam.Total > 50) ? (", page " + PackSteam.Page + " of " + num6) : ""), 4.4f, TextAnchor.MiddleRight, GameArt.Grey);
			Rect rect = new Rect(body.x, num4 + num + 4f * p, body.width - 8f * p, body.yMax - (num4 + num + 4f * p));
			GameArt.Sliced(rect, GameArt.Inner, new Color(1f, 1f, 1f, 0.45f));
			float num7 = Mathf.Round(48f * p);
			float num8 = 3f * p;
			List<PackSteam.Entry> results = PackSteam.Results;
			float contentH = (float)results.Count * (num7 + num8) + 4f * p;
			float num9 = BeginList(rect, ref PacksUi.ScrollBrowse, contentH);
			for (int k = 0; k < results.Count; k++)
			{
				Rect r5 = new Rect(3f * p, num9 + 3f * p + (float)k * (num7 + num8), rect.width - 6f * p, num7);
				bool flag2 = r5.yMax > 0f && r5.y < rect.height;
				if (flag2)
				{
					PackSteam.WantPreview(results[k]);
				}
				DrawBrowseRow(r5, results[k], live && _listMouseIn && flag2);
			}
			EndList(rect, ref PacksUi.ScrollBrowse, contentH);
			if (results.Count == 0 && !PackSteam.Querying)
			{
				DrawBrowseEmpty(rect, live);
			}
		}

		private void DrawBrowseEmpty(Rect view, bool live)
		{
			float p = GameArt.P;
			bool flag = PacksUi.Search.Length > 0 || PacksUi.Kind != 0;
			Rect rect = new Rect(view.x + 20f * p, view.y + 24f * p, view.width - 40f * p, 60f * p);
			GameArt.Label(new Rect(rect.x, rect.y, rect.width, 12f * p), flag ? "No packs match" : "No packs on the Workshop yet", 6f, TextAnchor.MiddleCenter, Color.white);
			GameArt.Label(new Rect(rect.x, rect.y + 14f * p, rect.width, 20f * p), flag ? "Try fewer words, or All kinds." : "Packs are models, sounds and translations laid over Worldfall. Make one in the pack folder, then put it up from Publish.", 4.6f, TextAnchor.UpperCenter, GameArt.Grey, wrap: true);
			float num = 70f * p;
			float height = 15f * p;
			float num2 = rect.center.x - num - 2f * p;
			float y = rect.y + 38f * p;
			if (flag)
			{
				if (GameButton(new Rect(rect.center.x - num * 0.5f, y, num, height), "Show all", red: true, enabled: true, null, 5f, live))
				{
					PacksUi.Search = "";
					PacksUi.Kind = 0;
					PacksUi.RunSearch();
				}
				return;
			}
			if (GameButton(new Rect(num2, y, num, height), "Make a pack", red: true, enabled: true, null, 5f, live))
			{
				PacksUi.ShowFolder(Packs.Find(PacksUi.NewPack())?.Folder ?? Packs.LocalFolder);
				PacksUi.Say("Made an empty pack in the pack folder");
			}
			if (GameButton(new Rect(num2 + num + 4f * p, y, num, height), "Publish", red: false, enabled: true, null, 5f, live))
			{
				PacksUi.Page = 2;
			}
		}

		private void DrawBrowseRow(Rect r, PackSteam.Entry e, bool live)
		{
			float p = GameArt.P;
			if (Event.current.type == EventType.Repaint)
			{
				GameArt.Sliced(r, GameArt.Well ?? GameArt.Inner, Color.white, 4f, 4f, 4f, 4f);
				float num = r.height - 6f * p;
				Rect rect = new Rect(r.x + 3f * p, r.y + 3f * p, num, num);
				if (e.Preview != null)
				{
					GUI.DrawTexture(rect, e.Preview, ScaleMode.ScaleAndCrop);
				}
				else
				{
					GameArt.Flat(rect, new Color(0.15f, 0.16f, 0.2f, 1f));
				}
				float num2 = rect.xMax + 5f * p;
				float width = r.xMax - num2 - 90f * p;
				GameArt.Label(new Rect(num2, r.y + 2f * p, width, 10f * p), e.Title, 5.4f, TextAnchor.MiddleLeft, Color.white);
				string text = "+" + e.Up + " -" + e.Down;
				string text2 = ((e.Size <= 0) ? "" : ((e.Size >= 1048576) ? (((float)e.Size / 1048576f).ToString("0.0") + " MB") : (e.Size / 1024 + " KB")));
				string text3 = PackSteam.AuthorOf(e);
				GameArt.Label(new Rect(num2, r.y + 11f * p, width, 8f * p), ((text3.Length > 0) ? (text3 + "  ") : "") + text + "  " + text2 + ((e.Kinds.Count > 0) ? ("  " + string.Join(", ", e.Kinds.ToArray())) : ""), 4.1f, TextAnchor.MiddleLeft, GameArt.Grey);
				string text4 = PackSteam.PlainText(e.Description);
				if (text4.Length > 160)
				{
					text4 = text4.Substring(0, 157) + "...";
				}
				GameArt.Label(new Rect(num2, r.y + 20f * p, width, r.height - 22f * p), text4, 4.2f, TextAnchor.UpperLeft, GameArt.Peach, wrap: true);
			}
			float num3 = 80f * p;
			float num4 = 14f * p;
			Rect r2 = new Rect(r.xMax - num3 - 4f * p, r.y + 4f * p, num3, num4);
			bool flag = PackSteam.IsSubscribed(e.Id);
			float num5 = PackSteam.DownloadOf(e.Id);
			if (num5 >= 0f)
			{
				GameArt.Bar(r2, num5, GameArt.HealthGreen, 0f, "Downloading " + Mathf.RoundToInt(num5 * 100f) + "%", 1f, 4.4f);
			}
			else if (GameButton(r2, flag ? "Unsubscribe" : "Subscribe", !flag, enabled: true, null, 5f, live))
			{
				PacksUi.Say((flag ? PackSteam.Unsubscribe(e.Id) : PackSteam.Subscribe(e.Id)) ?? (flag ? ("Unsubscribed from " + e.Title) : ("Subscribed. Downloading " + e.Title)));
				if (flag)
				{
					PacksUi.Rescan();
				}
			}
			if (flag && num5 < 0f)
			{
				PackInfo packInfo = Packs.All.Find((PackInfo packInfo2) => packInfo2.WorkshopId == e.Id && packInfo2.Source == PackSource.Workshop);
				Rect r3 = new Rect(r2.x, r2.yMax + 3f * p, num3, num4);
				if (packInfo != null)
				{
					bool flag2 = Packs.IsActive(packInfo.Key);
					if (GameButton(r3, flag2 ? "Turn off" : "Turn on", !flag2, enabled: true, null, 5f, live))
					{
						PacksUi.Toggle(packInfo.Key);
					}
				}
				else
				{
					GameArt.Label(r3, PackSteam.IsInstalled(e.Id) ? "Installed" : "Waiting for Steam", 4.4f, TextAnchor.MiddleCenter, GameArt.Grey);
				}
			}
			Rect r4 = new Rect(r.xMax - num3 - 4f * p, r.yMax - num4 - 3f * p, num3 * 0.5f - 1f * p, num4);
			Rect r5 = new Rect(r4.xMax + 2f * p, r4.y, r4.width, num4);
			if (GameButton(r4, "Vote up", red: false, enabled: true, null, 4.4f, live))
			{
				PackSteam.Vote(e.Id, up: true);
				PacksUi.Say("Voted up " + e.Title);
			}
			if (GameButton(r5, "Vote down", red: false, enabled: true, null, 4.4f, live))
			{
				PackSteam.Vote(e.Id, up: false);
				PacksUi.Say("Voted down " + e.Title);
			}
		}

		private List<string> PublishCheck(PackInfo p)
		{
			float unscaledTime = Time.unscaledTime;
			if (_publishWhy != null && p == _publishWhyFor && unscaledTime - _publishWhyAt < 0.5f)
			{
				return _publishWhy;
			}
			List<string> result = (_publishWhy = PackPublish.Check(p));
			_publishWhyFor = p;
			_publishWhyAt = unscaledTime;
			return result;
		}

		private void DrawPublish(Rect body, bool live)
		{
			float p = GameArt.P;
			List<PackInfo> list = Packs.All.FindAll((PackInfo packInfo2) => packInfo2.Source == PackSource.Local);
			float num = Mathf.Round(170f * p);
			Rect rect = new Rect(body.x, body.y + 19f * p, num - 8f * p, body.height - 19f * p - 19f * p);
			SectionTitle(new Rect(body.x, body.y, num, 16f * p), "Your packs");
			GameArt.Sliced(rect, GameArt.Inner, new Color(1f, 1f, 1f, 0.45f));
			float num2 = 16f * p;
			float contentH = (float)list.Count * (num2 + 2f * p) + 4f * p;
			float num3 = BeginList(rect, ref PacksUi.ScrollPublish, contentH);
			for (int num4 = 0; num4 < list.Count; num4++)
			{
				Rect r = new Rect(3f * p, num3 + 3f * p + (float)num4 * (num2 + 2f * p), rect.width - 6f * p, num2);
				if (GameButton(r, list[num4].Title, PacksUi.PublishKey == list[num4].Key, enabled: true, null, 4.8f, live && _listMouseIn))
				{
					PacksUi.PublishKey = list[num4].Key;
				}
			}
			EndList(rect, ref PacksUi.ScrollPublish, contentH);
			Rect r2 = new Rect(body.x, rect.yMax + 3f * p, num - 8f * p, 15f * p);
			if (GameButton(r2, "New pack", red: false, enabled: true, null, 5f, live))
			{
				PacksUi.PublishKey = PacksUi.NewPack();
				PacksUi.Say("Made a new pack in the pack folder");
			}
			Rect rect2 = new Rect(body.x + num + 4f * p, body.y, body.width - num - 4f * p, body.height);
			PackInfo packInfo = ((PacksUi.PublishKey != null) ? Packs.Find(PacksUi.PublishKey) : null);
			if (packInfo == null)
			{
				GameArt.Label(new Rect(rect2.x + 6f * p, rect2.y + 4f * p, rect2.width - 12f * p, 40f * p), (list.Count == 0) ? "No packs of your own yet. New pack makes one in the pack folder" : "Pick one of your packs", 5f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
				return;
			}
			SectionTitle(new Rect(rect2.x, rect2.y, rect2.width, 16f * p), packInfo.Title);
			float num5 = rect2.y + 20f * p;
			GameArt.Label(new Rect(rect2.x + 4f * p, num5, rect2.width, 9f * p), "id " + packInfo.Id + "  v" + packInfo.Version + "  " + ((packInfo.WorkshopId != 0L) ? ("Workshop item " + packInfo.WorkshopId) : "Not published yet"), 4.5f, TextAnchor.MiddleLeft, GameArt.Grey);
			num5 += 10f * p;
			List<string> list2 = PublishCheck(packInfo);
			GameArt.Label(new Rect(rect2.x + 4f * p, num5, rect2.width - 8f * p, 9f * p), (list2.Count == 0) ? "Ready to publish" : (list2[0] + ((list2.Count > 1) ? (" (+" + (list2.Count - 1) + " more)") : "")), 4.6f, TextAnchor.MiddleLeft, (list2.Count == 0) ? Good : Bad);
			num5 += 12f * p;
			GameArt.Label(new Rect(rect2.x + 4f * p, num5, 50f * p, 14f * p), "Visibility", 4.6f, TextAnchor.MiddleLeft, Color.white);
			for (int num6 = 0; num6 < 3; num6++)
			{
				if (GameButton(new Rect(rect2.x + 56f * p + (float)num6 * 52f * p, num5, 50f * p, 14f * p), PackPublish.Visibilities[num6], PacksUi.Visibility == num6, enabled: true, null, 4.6f, live))
				{
					PacksUi.Visibility = num6;
				}
			}
			num5 += 17f * p;
			GameArt.Label(new Rect(rect2.x + 4f * p, num5, 50f * p, 14f * p), "Changes", 4.6f, TextAnchor.MiddleLeft, Color.white);
			PacksUi.Changelog = GameTextField(new Rect(rect2.x + 56f * p, num5, rect2.width - 60f * p, 14f * p), PacksUi.Changelog, 300, "packs_changelog", 4.8f);
			num5 += 18f * p;
			PackPublish.Job current = PackPublish.Current;
			bool flag = current != null && !current.Done;
			if (GameButton(new Rect(rect2.x + 4f * p, num5, 70f * p, 15f * p), "Dry run", red: false, !flag, null, 5f, live))
			{
				PacksUi.Say(PackPublish.Start(packInfo, PacksUi.Visibility, PacksUi.Changelog, dry: true).State);
			}
			if (GameButton(new Rect(rect2.x + 78f * p, num5, 70f * p, 15f * p), "Publish", red: true, !flag && list2.Count == 0, null, 5f, live))
			{
				PacksUi.Confirm = "publish";
				PacksUi.ConfirmKey = packInfo.Key;
			}
			num5 += 19f * p;
			if (current != null && current.Pack == packInfo)
			{
				if (flag)
				{
					GameArt.Bar(new Rect(rect2.x + 4f * p, num5, rect2.width - 8f * p, 10f * p), current.Progress, GameArt.HealthGreen, 0f, current.State + " " + Mathf.RoundToInt(current.Progress * 100f) + "%", 1f, 4.4f);
				}
				else
				{
					GameArt.Label(new Rect(rect2.x + 4f * p, num5, rect2.width - 8f * p, 10f * p), current.State, 4.6f, TextAnchor.MiddleLeft, current.Failed ? Bad : Good);
				}
				num5 += 13f * p;
				Rect rect3 = new Rect(rect2.x, num5, rect2.width - 8f * p, rect2.yMax - num5);
				GameArt.Sliced(rect3, GameArt.Inner, new Color(1f, 1f, 1f, 0.45f));
				List<string> list3 = new List<string>();
				foreach (string item in current.Report)
				{
					list3.AddRange(item.Split('\n'));
				}
				float num7 = 8.5f * p;
				float contentH2 = (float)list3.Count * num7 + 6f * p;
				float num8 = BeginList(rect3, ref PacksUi.ScrollReport, contentH2);
				for (int num9 = 0; num9 < list3.Count; num9++)
				{
					GameArt.Label(new Rect(5f * p, num8 + 3f * p + (float)num9 * num7, rect3.width - 10f * p, num7), list3[num9], 4f, TextAnchor.MiddleLeft, GameArt.Grey);
				}
				EndList(rect3, ref PacksUi.ScrollReport, contentH2);
			}
			else if (list2.Count > 1)
			{
				int num10 = 1;
				while (num10 < list2.Count && num5 < rect2.yMax - 10f * p)
				{
					GameArt.Label(new Rect(rect2.x + 4f * p, num5, rect2.width - 8f * p, 9f * p), list2[num10], 4.4f, TextAnchor.MiddleLeft, Bad);
					num10++;
					num5 += 9f * p;
				}
			}
		}

		internal void DrawPadMenu(float sw, float sh)
		{
			Event current = Event.current;
			bool flag = current.type == EventType.Repaint;
			if (flag)
			{
				PadMenuDrawn = false;
			}
			if (!PadMenu.Open)
			{
				return;
			}
			Skin.Ensure();
			GameArt.Ensure();
			float p = GameArt.P;
			int num = PadMenu.Items.Length;
			int num2 = 5;
			int num3 = (num + num2 - 1) / num2;
			float num4 = Mathf.Round(46f * p);
			float num5 = Mathf.Round(18f * p);
			float num6 = Mathf.Round(3f * p);
			float num7 = (float)num2 * num4 + (float)(num2 - 1) * num6;
			float num8 = (float)num3 * num5 + (float)(num3 - 1) * num6;
			Rect rect = new Rect(Mathf.Round((sw - num7) * 0.5f), Mathf.Round(sh * 0.45f - num8 * 0.5f), num7, num8);
			float num9 = Mathf.Round(6f * p);
			float num10 = Mathf.Round(13f * p);
			if (flag)
			{
				Rect r = new Rect(rect.x - num9, rect.y - num9 - num10, rect.width + num9 * 2f, rect.height + num9 * 2f + num10 * 2f);
				GameArt.TipPanel(r, 0.97f);
				GameArt.Label(new Rect(r.x, r.y + num9 * 0.5f, r.width, num10), "Quick menu", 8f, TextAnchor.MiddleCenter, Color.white);
				GameArt.Label(new Rect(r.x, rect.yMax + num9 * 0.5f, r.width, num10), "D-pad picks. A presses, B closes", 6f, TextAnchor.MiddleCenter, GameArt.Grey);
			}
			for (int i = 0; i < num; i++)
			{
				PadMenu.Item item = PadMenu.Items[i];
				Rect rect2 = new Rect(rect.x + (float)(i % num2) * (num4 + num6), rect.y + (float)(i / num2) * (num5 + num6), num4, num5);
				PadMenuRects[i] = rect2;
				if (flag)
				{
					bool flag2 = i == PadMenu.Choice;
					if (flag2)
					{
						float num11 = Mathf.Max(2f, Mathf.Round(1.5f * p));
						GameArt.Flat(new Rect(rect2.x - num11, rect2.y - num11, rect2.width + num11 * 2f, rect2.height + num11 * 2f), GameArt.Orange);
					}
					GameArt.FlatButton(rect2, flag2, null, 6.5f, flag2 ? 1.25f : 1f);
					GameArt.Label(rect2, item.Name, 6.5f, TextAnchor.MiddleCenter, flag2 ? new Color(1f, 0.9f, 0.6f) : Color.white);
				}
				if (current.type == EventType.MouseMove && rect2.Contains(current.mousePosition))
				{
					PadMenu.Point(i);
				}
				if (GUI.Button(rect2, GUIContent.none, GUIStyle.none))
				{
					PadMenu.Point(i);
					KeyCode keyCode = PadMenu.Close();
					if (keyCode != KeyCode.None)
					{
						PadControls.Tap(keyCode);
					}
				}
			}
			if (flag)
			{
				PadMenuDrawn = true;
			}
		}

		private void PeaceMood(Rect card, float x, Kingdom home, Kingdom other, ref Peace.Mood tip, ref Rect tipAt)
		{
			if (Event.current.type == EventType.Repaint)
			{
				Peace.Mood mood;
				try
				{
					mood = Peace.MoodOf(other, home);
				}
				catch
				{
					return;
				}
				StateTag(x, card.y + 3.5f * GameArt.P, mood.Word, mood.Color);
				if (_listMouseIn && new Rect(card.x, card.y, card.width * 0.5f, card.height).Contains(Event.current.mousePosition))
				{
					tip = mood;
					tipAt = card;
				}
			}
		}

		private static void DrawPeaceTip(Peace.Mood m, Rect card)
		{
			if (m == null || Event.current.type != EventType.Repaint)
			{
				return;
			}
			float p = GameArt.P;
			float num = 8f * p;
			float num2 = 0f;
			foreach (KeyValuePair<string, Color> line in m.Lines)
			{
				num2 = Mathf.Max(num2, GameArt.Measure(line.Key, 4.4f).x);
			}
			num2 += 10f * p;
			float num3 = (float)m.Lines.Count * num + 6f * p;
			Rect r = new Rect(Mathf.Round(card.x - num2 - 4f * p), Mathf.Round(card.y), Mathf.Round(num2), Mathf.Round(num3));
			if (r.x < 2f * p)
			{
				r.x = Mathf.Round(card.x + 4f * p);
			}
			if (r.yMax > (float)Screen.height - 2f * p)
			{
				r.y = Mathf.Round((float)Screen.height - 2f * p - num3);
			}
			GameArt.TipPanel(r);
			float num4 = r.y + 3f * p;
			foreach (KeyValuePair<string, Color> line2 in m.Lines)
			{
				GameArt.Label(new Rect(r.x + 5f * p, num4, r.width - 8f * p, num), line2.Key, 4.4f, TextAnchor.MiddleLeft, line2.Value);
				num4 += num;
			}
		}

		private void DrawPings(float sw, float sh)
		{
			PingsInView = (PingsAtEdge = 0);
			if (Event.current.type != EventType.Repaint || _mod.Pings.List.Count == 0)
			{
				return;
			}
			Actor host = _mod.Host;
			if (host == null)
			{
				return;
			}
			Skin.Ensure();
			FrameProjection shown = _mod.Shown;
			if (shown.Width <= 0 || shown.Height <= 0)
			{
				return;
			}
			float p = GameArt.P;
			float unscaledTime = Time.unscaledTime;
			Vector2 current_position = host.current_position;
			Vector2 vector = new Vector2(sw * 0.5f, sh * 0.5f);
			float viewYaw = _mod.ViewYaw;
			float num = Mathf.Round(10f * p);
			float num2 = Mathf.Round(15f * p);
			foreach (Ping item in _mod.Pings.List)
			{
				Vector3 vector2 = Pings.Where(item);
				float magnitude = (new Vector2(vector2.x, vector2.y) - current_position).magnitude;
				Vector3 vector3 = vector2;
				Vector3 head = default(Vector3);
				bool flag = item.Actor != null && _mod.StarHead(item.Actor, out head);
				if (flag)
				{
					vector3 = new Vector3(head.x, head.y, head.z + 0.2f);
				}
				float num3 = Mathf.Clamp01((item.Until - unscaledTime) / 1.5f);
				float col;
				float row;
				float depth;
				bool num4 = shown.Project(vector3.x, vector3.y, vector3.z, out col, out row, out depth);
				float num5 = col / (float)shown.Width * sw;
				float num6 = row / (float)shown.Height * sh;
				if (num4 && num5 > 6f * p && num5 < sw - 6f * p && num6 > num2 + num && num6 < sh - 6f * p)
				{
					PingsInView++;
					Vector2 c = new Vector2(Mathf.Round(num5), Mathf.Round(num6 - num2));
					Color color = Pings.ColorOf(item.Kind);
					float num7 = Mathf.Max(1f, Mathf.Round(0.8f * p));
					GameArt.Flat(new Rect(c.x - num7 * 0.5f + 1f, c.y + num * 0.6f + 1f, num7, Mathf.Max(0f, num6 - c.y - num * 0.6f)), new Color(0f, 0f, 0f, 0.45f * num3));
					GameArt.Flat(new Rect(c.x - num7 * 0.5f, c.y + num * 0.6f, num7, Mathf.Max(0f, num6 - c.y - num * 0.6f)), new Color(color.r, color.g, color.b, 0.85f * num3));
					if (!flag)
					{
						PixelDiamond(new Vector2(c.x, num6), 1, new Color(color.r, color.g, color.b, 0.9f * num3));
					}
					Ripple(c, num, item, unscaledTime, num3);
					float num8 = 1f + 0.35f * Mathf.Clamp01(1f - (unscaledTime - item.Born) / 0.35f);
					PingIcon(c, Mathf.Round(num * num8), item.Kind, num3);
					GameArt.Label(new Rect(c.x - 30f * p, c.y - num - 9f * p, 60f * p, 8f * p), Mathf.RoundToInt(magnitude).ToString(), 4.6f, TextAnchor.LowerCenter, new Color(1f, 1f, 1f, 0.95f * num3));
					if ((new Vector2(num5, num6 - num2) - vector).sqrMagnitude < 40f * p * 40f * p || unscaledTime - item.Born < 2.5f)
					{
						string text = Pings.Name(item.Kind);
						string text2 = ((item.On != null) ? NameOf(item.On, _mod.Host) : null);
						if (!string.IsNullOrEmpty(text2))
						{
							text = text + "  ·  " + text2;
						}
						float num9 = GameArt.Measure(text, 4.6f).x + 10f * p;
						Rect r = new Rect(Mathf.Round(c.x - num9 * 0.5f), Mathf.Round(c.y + num * 0.75f + 2f * p), Mathf.Round(num9), Mathf.Round(10f * p));
						if (!flag || r.yMax < num6 - 2f * p)
						{
							GameArt.TipPanel(r, 0.85f * num3);
							GameArt.Label(r, text, 4.6f, TextAnchor.MiddleCenter, new Color(color.r, color.g, color.b, num3));
						}
					}
				}
				else
				{
					PingsAtEdge++;
					Vector2 vector4 = new Vector2(vector2.x, vector2.y) - current_position;
					float f = Mathf.Atan2(vector4.y, vector4.x) - viewYaw;
					float num10 = 0f - Mathf.Sin(f);
					float num11 = 0f - Mathf.Cos(f);
					float num12 = Mathf.Min(sw * 0.45f / Mathf.Max(0.01f, Mathf.Abs(num10)), sh * 0.4f / Mathf.Max(0.01f, Mathf.Abs(num11)));
					Vector2 vector5 = new Vector2(Mathf.Round(vector.x + num10 * num12), Mathf.Round(vector.y + num11 * num12));
					PingIcon(vector5, Mathf.Round(num * 0.85f), item.Kind, 0.85f * num3);
					PixelArrow(vector5 + new Vector2(num10, num11).normalized * num * 1.05f, Mathf.Max(1f, Mathf.Round(p * 0.75f)), Mathf.Atan2(num10, 0f - num11) * 57.29578f, 0.85f * num3);
					GameArt.Label(new Rect(vector5.x - 30f * p, vector5.y + num * 0.6f, 60f * p, 8f * p), Mathf.RoundToInt(magnitude).ToString(), 4.2f, TextAnchor.UpperCenter, new Color(1f, 1f, 1f, 0.85f * num3));
				}
			}
		}

		private void Ripple(Vector2 c, float size, Ping p, float now, float fade)
		{
			float num = (now - p.Born) / 0.8f;
			if (!(num < 0f) && !(num >= 1f))
			{
				if (_ripple == null)
				{
					_ripple = MakeRipple();
				}
				Color color = Pings.ColorOf(p.Kind);
				float num2 = size * (1.2f + 2.6f * num);
				Color color2 = GUI.color;
				GUI.color = new Color(color.r, color.g, color.b, (1f - num) * 0.9f * fade);
				GUI.DrawTexture(new Rect(c.x - num2 * 0.5f, c.y - num2 * 0.5f, num2, num2), _ripple);
				GUI.color = color2;
			}
		}

		private static Texture2D MakeRipple()
		{
			Color32[] array = new Color32[4096];
			for (int i = 0; i < 64; i++)
			{
				for (int j = 0; j < 64; j++)
				{
					float num = (float)j + 0.5f - 32f;
					float num2 = (float)i + 0.5f - 32f;
					float num3 = Mathf.Sqrt(num * num + num2 * num2);
					float a = Mathf.Clamp01(1f - Mathf.Abs(num3 - 28.16f) / 2.2f);
					array[i * 64 + j] = new Color(1f, 1f, 1f, a);
				}
			}
			Texture2D texture2D = new Texture2D(64, 64, TextureFormat.RGBA32, mipChain: false);
			texture2D.filterMode = FilterMode.Bilinear;
			texture2D.wrapMode = TextureWrapMode.Clamp;
			texture2D.hideFlags = HideFlags.HideAndDontSave;
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false);
			return texture2D;
		}

		private void DrawPingWheel(float sw, float sh)
		{
			WheelDrawn = false;
			if (!_mod.PingWheelOpen || Event.current.type != EventType.Repaint)
			{
				return;
			}
			Skin.Ensure();
			float p = GameArt.P;
			float num = Smooth((Time.unscaledTime - _mod.PingWheelOpenedAt) / 0.14f);
			Vector2 c = new Vector2(Mathf.Round(sw * 0.5f), Mathf.Round(sh * 0.5f));
			float num2 = Mathf.Round(64f * p * (0.85f + 0.15f * num));
			float num3 = Mathf.Round(22f * p * (0.85f + 0.15f * num));
			int num4 = Pings.Wheel.Length;
			int pingWheelChoice = _mod.PingWheelChoice;
			float num5 = 360f / (float)num4;
			float num6 = 1.6f;
			for (int i = 0; i < num4; i++)
			{
				float a = (float)i * num5 - num5 * 0.5f + num6;
				float a2 = (float)i * num5 + num5 * 0.5f - num6;
				Color color = Pings.ColorOf(Pings.Wheel[i]);
				bool flag = i == pingWheelChoice;
				Color color2 = (flag ? new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, 0.82f * num) : new Color(0.05f, 0.06f, 0.08f, 0.62f * num));
				Sector(c, num3, num2 + (flag ? (4f * p) : 0f), a, a2, color2);
				Sector(c, num2 + (flag ? (4f * p) : 0f) - Mathf.Max(1f, Mathf.Round(p)), num2 + (flag ? (4f * p) : 0f), a, a2, new Color(color.r, color.g, color.b, (flag ? 1f : 0.55f) * num));
			}
			for (int j = 0; j < num4; j++)
			{
				PingKind k = Pings.Wheel[j];
				bool flag2 = j == pingWheelChoice;
				float f = (float)j * num5 * (MathF.PI / 180f);
				float num7 = (num3 + num2) * 0.5f + (flag2 ? (2f * p) : 0f);
				Vector2 vector = new Vector2(Mathf.Round(c.x + Mathf.Sin(f) * num7), Mathf.Round(c.y - Mathf.Cos(f) * num7 - 3f * p));
				Sprite sprite = GameArt.IconNamed(Pings.Icon(k));
				float num8 = Mathf.Round((flag2 ? 15f : 12f) * p);
				if (sprite != null)
				{
					GameArt.Icon(new Rect(vector.x - num8 * 0.5f + p, vector.y - num8 * 0.5f + p, num8, num8), sprite, new Color(0f, 0f, 0f, 0.5f * num));
					GameArt.Icon(new Rect(vector.x - num8 * 0.5f, vector.y - num8 * 0.5f, num8, num8), sprite, new Color(1f, 1f, 1f, (flag2 ? 1f : 0.8f) * num));
				}
				Color color3 = Pings.ColorOf(k);
				GameArt.Label(new Rect(vector.x - 30f * p, vector.y + num8 * 0.5f, 60f * p, 8f * p), Pings.Name(k), 4.4f, TextAnchor.UpperCenter, flag2 ? new Color(color3.r, color3.g, color3.b, num) : new Color(0.85f, 0.85f, 0.85f, 0.85f * num));
			}
			Color color4 = GUI.color;
			GUI.color = new Color(0.04f, 0.04f, 0.05f, 0.75f * num);
			GUI.DrawTexture(new Rect(c.x - num3 + 2f * p, c.y - num3 + 2f * p, num3 * 2f - 4f * p, num3 * 2f - 4f * p), Skin.DiscTex);
			GUI.color = color4;
			string text = ((pingWheelChoice >= 0) ? Pings.Name(Pings.Wheel[pingWheelChoice]) : "Cancel");
			Color color5 = ((pingWheelChoice >= 0) ? Pings.ColorOf(Pings.Wheel[pingWheelChoice]) : GameArt.Grey);
			GameArt.Label(new Rect(c.x - num3, c.y - 5f * p, num3 * 2f, 10f * p), text, 5f, TextAnchor.MiddleCenter, new Color(color5.r, color5.g, color5.b, num));
			string text2;
			if (!_mod.PingAimOk)
			{
				text2 = (_mod.Binoculars.Active ? "Nothing in range" : "Nothing there");
			}
			else
			{
				string text3 = ((_mod.PingAimOn != null) ? NameOf(_mod.PingAimOn, _mod.Host) : null);
				int num9 = Mathf.RoundToInt(_mod.PingAimDistance);
				text2 = (string.IsNullOrEmpty(text3) ? "The ground" : text3) + "  ·  " + num9 + ((num9 == 1) ? " tile" : " tiles");
			}
			float num10 = GameArt.Measure(text2, 4.8f).x + 12f * p;
			Rect r = new Rect(Mathf.Round(c.x - num10 * 0.5f), Mathf.Round(c.y + num2 + 8f * p), Mathf.Round(num10), Mathf.Round(11f * p));
			GameArt.TipPanel(r, num);
			GameArt.Label(r, text2, 4.8f, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, num));
			WheelDrawn = true;
		}

		private void Sector(Vector2 c, float r0, float r1, float a0, float a1, Color color)
		{
			Material material = MinimapMaterial();
			if (!(material == null) && !(r1 <= r0) && !(a1 <= a0))
			{
				material.mainTexture = _white;
				material.SetPass(0);
				GL.PushMatrix();
				GL.LoadPixelMatrix(0f, Screen.width, Screen.height, 0f);
				GL.Begin(7);
				GL.Color(new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, color.a * 0.5f));
				int num = Mathf.Max(2, Mathf.CeilToInt((a1 - a0) / 4f));
				for (int i = 0; i < num; i++)
				{
					float f = Mathf.Lerp(a0, a1, (float)i / (float)num) * (MathF.PI / 180f);
					float f2 = Mathf.Lerp(a0, a1, (float)(i + 1) / (float)num) * (MathF.PI / 180f);
					float num2 = Mathf.Sin(f);
					float num3 = 0f - Mathf.Cos(f);
					float num4 = Mathf.Sin(f2);
					float num5 = 0f - Mathf.Cos(f2);
					GL.TexCoord2(0.5f, 0.5f);
					GL.Vertex3(c.x + num2 * r0, c.y + num3 * r0, 0f);
					GL.Vertex3(c.x + num2 * r1, c.y + num3 * r1, 0f);
					GL.Vertex3(c.x + num4 * r1, c.y + num5 * r1, 0f);
					GL.Vertex3(c.x + num4 * r0, c.y + num5 * r0, 0f);
				}
				GL.End();
				GL.PopMatrix();
			}
		}

		private void DrawMapAndWheel(float sw, float sh)
		{
			DrawBigMap(sw, sh);
			DrawPingWheel(sw, sh);
			DrawPoseWheel(sw, sh);
			DrawRadialWheels(sw, sh);
			CaptureIfAsked(sw, sh);
		}

		internal void CaptureHudForTest(string file)
		{
			_captureFile = file;
		}

		private void CaptureIfAsked(float sw, float sh)
		{
			if (_captureFile == null || _capturing || Event.current.type != EventType.Repaint)
			{
				return;
			}
			string captureFile = _captureFile;
			_captureFile = null;
			_capturing = true;
			int num = Mathf.Max(16, (int)sw);
			int num2 = Mathf.Max(16, (int)sh);
			RenderTexture temporary = RenderTexture.GetTemporary(num, num2, 24, RenderTextureFormat.ARGB32);
			RenderTexture active = RenderTexture.active;
			try
			{
				RenderTexture.active = temporary;
				GL.Clear(clearDepth: true, clearColor: true, Color.black);
				DrawFrame(new Rect(0f, 0f, sw, sh), blend: false);
				DrawOverlay(sw, sh, _mod.Feedback, dead: false);
				if (NetSession.Active)
				{
					DrawNet(firstPerson: true);
				}
				if (_mod.SettingsMenu.Open)
				{
					if (PacksUi.Open)
					{
						DrawPacksWindow();
					}
					else
					{
						DrawSettings(_mod.SettingsMenu, _mod.Settings);
					}
				}
				if (_mod.SoldierMap.Open)
				{
					DrawSoldierMap();
				}
				if (_mod.FamilyMap.Open)
				{
					DrawFamilyMap();
				}
				if (_mod.WarMap.Open)
				{
					DrawWarMap();
				}
				else if (!_mod.AnyMenuOpen)
				{
					DrawWarMarks(sw, sh);
				}
				if (_mod.Royal.Open)
				{
					DrawCouncil();
				}
				if (_mod.Royal.ObeliskOpen)
				{
					DrawObelisk();
				}
				if (WorldNews.Open)
				{
					DrawWorldNews();
				}
				if (Journal.Open)
				{
					DrawJournal();
				}
				if (_mod.Converse.Open)
				{
					DrawTalk(_mod.Converse);
				}
				else if (PlayerTalk.CardOpen)
				{
					DrawTalk(PlayerTalk.Card, PlayerTalk.Hint, mouse: false);
				}
				if (_mod.Craft.Open)
				{
					DrawCraftPanel();
					DrawFavoritesBeside();
				}
				if (Help.Open && !_mod.SettingsMenu.Open)
				{
					DrawHelpWindow();
				}
				if (HomeCard.Open)
				{
					DrawHomeCard();
				}
				DrawFps();
				_mod.DrawPadForCapture();
				Texture2D texture2D = new Texture2D(num, num2, TextureFormat.RGB24, mipChain: false);
				texture2D.ReadPixels(new Rect(0f, 0f, num, num2), 0, 0);
				texture2D.Apply(updateMipmaps: false);
				bool flag = captureFile.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase);
				File.WriteAllBytes(captureFile, flag ? texture2D.EncodeToJPG(93) : texture2D.EncodeToPNG());
				UnityEngine.Object.Destroy(texture2D);
				if (!flag)
				{
					Log.Info("hud captured " + Path.GetFileName(captureFile));
				}
			}
			catch (Exception ex)
			{
				Log.Error("hud capture: " + ex.Message);
			}
			finally
			{
				RenderTexture.active = active;
				RenderTexture.ReleaseTemporary(temporary);
				_capturing = false;
			}
		}

		private void DrawFloatingTexts(float sw, float sh, CombatFeedback fb)
		{
			float unscaledTime = Time.unscaledTime;
			FrameProjection shown = _mod.Shown;
			foreach (FloatingText text in fb.Texts)
			{
				NumberLook numberLook = LookOf(text, unscaledTime);
				if (text.Amount != numberLook.Amount)
				{
					numberLook.Amount = text.Amount;
					numberLook.Weight = HitWeight(text);
					numberLook.PunchAt = unscaledTime;
					numberLook.Px = 0;
					numberLook.Head = text.Head;
				}
				if (!numberLook.Placed)
				{
					numberLook.Placed = true;
					numberLook.At = new Vector3(text.World.x, text.World.y, text.World.z - 0.35f);
					if (text.Victim != null && !text.Word && _mod.StarHead(text.Victim, out var head))
					{
						numberLook.At = new Vector3(head.x, head.y, head.z + 0.06f);
					}
				}
				if (shown.Project(numberLook.At.x, numberLook.At.y, numberLook.At.z, out var col, out var row, out var depth))
				{
					if (numberLook.Px == 0)
					{
						numberLook.Px = NumberPx(depth, numberLook);
					}
					float num = unscaledTime - numberLook.Seen;
					float num2 = numberLook.Px;
					float num3 = Smooth((text.Age / text.Life - 0.6f) / 0.4f);
					float x = col / (float)shown.Width * sw + numberLook.Side * num2 * 0.9f * (1f - Mathf.Exp((0f - num) / 0.22f));
					float a = row / (float)shown.Height * sh - num2 * (0.75f * (1f - Mathf.Exp((0f - num) / 0.09f)) + 0.5f * num + 0.35f * num3);
					a = Mathf.Max(a, sh * 0.13f + num2 * (text.Head ? 1.6f : 0.6f));
					float num4 = 1f + (numberLook.Kill ? 0.85f : (0.6f + 0.3f * numberLook.Weight)) * Mathf.Exp((0f - num) * 16f) * Mathf.Cos(num * 24f);
					float num5 = unscaledTime - numberLook.PunchAt;
					if (num5 >= 0f && num5 < 0.5f)
					{
						num4 += 0.45f * Mathf.Exp((0f - num5) * 14f) * Mathf.Cos(num5 * 26f);
					}
					float num6 = num4 * (1f - 0.25f * num3);
					float num7 = Mathf.Max(1f - Smooth(num / 0.14f), (num5 >= 0f) ? (1f - Smooth(num5 / 0.12f)) : 0f);
					Color a2 = (numberLook.Kill ? KillRed : (text.Word ? text.Color : (text.Head ? HeadRed : ((text.Victim != null || text.Amount > 0) ? Color.Lerp(NumberGold, NumberOrange, numberLook.Weight) : text.Color))));
					a2 = Color.Lerp(a2, Color.white, 0.85f * num7);
					float alpha = 1f - num3;
					if (numberLook.Kill)
					{
						DrawKill(new Vector2(x, a), numberLook.Px, num6, a2, alpha, num7);
					}
					else
					{
						PopText(new Vector2(x, a), text.Text, numberLook.Px, num6, a2, alpha);
					}
					if (text.Head)
					{
						float num8 = num - 0.04f;
						float scale = ((num8 <= 0f) ? 0f : ((1f + 0.5f * Mathf.Exp((0f - num8) * 16f) * Mathf.Cos(num8 * 24f)) * (1f - 0.25f * num3)));
						Color color = Color.Lerp(numberLook.Kill ? KillRed : HeadRed, Color.white, 0.85f * (1f - Smooth(Mathf.Max(0f, num8) / 0.14f)));
						PopText(new Vector2(x, a - (float)numberLook.Px * 1.05f * num6), "HEADSHOT", Mathf.Max(10, Mathf.RoundToInt((float)numberLook.Px * 0.52f)), scale, color, alpha);
					}
				}
			}
			if (_numbers.Count == 0)
			{
				return;
			}
			_numbersGone.Clear();
			foreach (FloatingText key in _numbers.Keys)
			{
				if (!fb.Texts.Contains(key))
				{
					_numbersGone.Add(key);
				}
			}
			foreach (FloatingText item in _numbersGone)
			{
				_numbers.Remove(item);
			}
		}

		private NumberLook LookOf(FloatingText t, float now)
		{
			if (_numbers.TryGetValue(t, out var value))
			{
				return value;
			}
			value = new NumberLook
			{
				Seen = now - t.Age,
				Amount = t.Amount,
				Kill = (t.Text == "KILL"),
				Weight = HitWeight(t),
				Word = t.Word,
				Head = t.Head
			};
			value.Side = (value.Kill ? 0f : (_nextSide * (0.55f + 0.45f * (float)_shakeRandom.NextDouble())));
			if (!value.Kill)
			{
				_nextSide = 0f - _nextSide;
			}
			_numbers[t] = value;
			return value;
		}

		internal void PoseNumberForTest(FloatingText t, float age)
		{
			t.Age = age;
			LookOf(t, Time.unscaledTime).Seen = Time.unscaledTime - age;
		}

		private static float HitWeight(FloatingText t)
		{
			if (t.Victim == null || t.Amount <= 0)
			{
				return 0f;
			}
			int num = 0;
			try
			{
				num = t.Victim.getMaxHealth();
			}
			catch
			{
			}
			if (num <= 0)
			{
				return 0f;
			}
			return Mathf.Clamp01((float)t.Amount / ((float)num * 0.4f));
		}

		private static int NumberPx(float depth, NumberLook look)
		{
			float num = Mathf.Clamp(1.7f / Mathf.Sqrt(Mathf.Max(0.6f, depth)), 0.6f, 1.3f);
			float num2 = (look.Kill ? 11f : (look.Word ? 8.5f : (look.Head ? 11.5f : 10f))) * num * (1f + 0.45f * look.Weight);
			return Mathf.Max(12, Mathf.RoundToInt(num2 * GameArt.P));
		}

		private void DrawKill(Vector2 at, int px, float scale, Color color, float alpha, float white)
		{
			if (alpha <= 0.01f || scale <= 0.01f)
			{
				return;
			}
			float num = TextWidth("KILL", px);
			float num2 = Mathf.Round((float)px * 0.92f);
			float num3 = Mathf.Round(num2 * SkullPart.width / SkullPart.height);
			float num4 = Mathf.Round((float)px * 0.22f);
			float num5 = Mathf.Round(at.x - (num3 + num4 + num) * 0.5f);
			Sprite skulls = GameArt.Skulls;
			if (skulls != null)
			{
				Rect r = new Rect(at.x + (num5 - at.x) * scale, at.y - num2 * 0.5f * scale, num3 * scale, num2 * scale);
				float num6 = Mathf.Max(1f, Mathf.Round((float)px / 15f * scale));
				Vector2[] rimSteps = RimSteps;
				for (int i = 0; i < rimSteps.Length; i++)
				{
					Vector2 vector = rimSteps[i];
					GameArt.IconPart(new Rect(r.x + vector.x * num6, r.y + vector.y * num6, r.width, r.height), skulls, SkullPart, new Color(Rim.r, Rim.g, Rim.b, 0.85f * alpha * alpha));
				}
				GameArt.IconPart(r, skulls, SkullPart, new Color(1f, 1f, 1f, alpha));
				if (white > 0.01f)
				{
					GameArt.Silhouette(r, skulls, SkullPart, new Color(1f, 1f, 1f, 0.85f * white * alpha));
				}
			}
			else
			{
				num5 = Mathf.Round(at.x - num * 0.5f) - num3 - num4;
			}
			PopText(new Vector2(num5 + num3 + num4, at.y), "KILL", px, scale, color, alpha, TextAnchor.MiddleLeft, at);
		}

		private float TextWidth(string text, int px)
		{
			return Lang.Size(GameArt.Style((float)px / GameArt.P), text).x;
		}

		private float PopText(Vector2 at, string text, int px, float scale, Color color, float alpha, TextAnchor anchor = TextAnchor.MiddleCenter, Vector2? pivot = null)
		{
			if (string.IsNullOrEmpty(text))
			{
				return 0f;
			}
			GUIStyle gUIStyle = GameArt.Style((float)px / GameArt.P, anchor);
			float x = Lang.Size(gUIStyle, text).x;
			if (alpha <= 0.01f || scale <= 0.01f)
			{
				return x;
			}
			text = Lang.T(text);
			Font wasFont;
			FontStyle wasStyle;
			bool flag = Lang.Fit(gUIStyle, text, out wasFont, out wasStyle);
			at = new Vector2(Mathf.Round(at.x), Mathf.Round(at.y));
			Rect position = ((anchor == TextAnchor.MiddleLeft) ? new Rect(at.x, at.y - (float)px, 600f, (float)px * 2f) : new Rect(at.x - 300f, at.y - (float)px, 600f, (float)px * 2f));
			Matrix4x4 matrix = GUI.matrix;
			if (Mathf.Abs(scale - 1f) > 0.002f)
			{
				GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), pivot ?? at);
			}
			Color color2 = GUI.color;
			GUI.color = Color.white;
			float num = Mathf.Max(1f, Mathf.Round((float)px / 15f));
			gUIStyle.normal.textColor = new Color(Rim.r, Rim.g, Rim.b, 0.55f * alpha * alpha);
			GUI.Label(new Rect(position.x + num, position.y + num * 2f, position.width, position.height), text, gUIStyle);
			gUIStyle.normal.textColor = new Color(Rim.r, Rim.g, Rim.b, 0.85f * alpha * alpha);
			Vector2[] rimSteps = RimSteps;
			for (int i = 0; i < rimSteps.Length; i++)
			{
				Vector2 vector = rimSteps[i];
				GUI.Label(new Rect(position.x + vector.x * num, position.y + vector.y * num, position.width, position.height), text, gUIStyle);
			}
			color.a *= alpha;
			gUIStyle.normal.textColor = color;
			GUI.Label(position, text, gUIStyle);
			if (flag)
			{
				Lang.Restore(gUIStyle, wasFont, wasStyle);
			}
			GUI.color = color2;
			GUI.matrix = matrix;
			return x;
		}

		private static int Flying(Work work, string id, float now)
		{
			int num = 0;
			foreach (BagGain item in work.Gained)
			{
				if (((id == null) ? (!Supplies.Is(item.Id)) : (item.Id == id)) && now < item.LandAt)
				{
					num += item.Amount;
				}
			}
			return num;
		}

		private int ShownBagCount()
		{
			return Mathf.Max(0, _mod.Work.BagCount - Flying(_mod.Work, null, Time.unscaledTime));
		}

		private Rect BagButtonRect()
		{
			float num = Mathf.Round(22f * GameArt.P);
			return new Rect(_bagLeft, _bagTop, num, num);
		}

		private void DrawBag(float sw, float sh, Work work)
		{
			_slotAt.Clear();
			if (work.Bag.Count == 0)
			{
				return;
			}
			float p = GameArt.P;
			float unscaledTime = Time.unscaledTime;
			float num = Mathf.Round(20f * p);
			float num2 = Mathf.Round(2f * p);
			int count = work.Bag.Count;
			float num3 = Mathf.Max(sw * 0.55f, _bagRight - (float)count * (num + num2) + num2);
			float y = Mathf.Round(_bagTop - num - 4f * p);
			float num4 = ((_slotsStepped >= 0f) ? Mathf.Clamp(unscaledTime - _slotsStepped, 0f, 0.1f) : 1f);
			_slotsStepped = unscaledTime;
			float t = 1f - Mathf.Exp((0f - num4) * 16f);
			for (int i = 0; i < count; i++)
			{
				string key = work.Bag[i].Key;
				float num5 = num3 + (float)i * (num + num2);
				float value;
				float num6 = (_slotX.TryGetValue(key, out value) ? Mathf.Lerp(value, num5, t) : num5);
				if (Mathf.Abs(num6 - num5) < 0.5f)
				{
					num6 = num5;
				}
				_slotX[key] = num6;
				Rect rect = new Rect(Mathf.Round(num6), y, num, num);
				_slotAt[key] = rect;
				int num7 = Mathf.Max(0, work.Bag[i].Value - Flying(work, key, unscaledTime));
				float num8 = 1f;
				float num9 = -9f;
				foreach (BagGain item in work.Gained)
				{
					if (!(item.Id != key))
					{
						if (unscaledTime >= item.LandAt)
						{
							num9 = Mathf.Max(num9, item.LandAt);
						}
						else if (num7 == 0)
						{
							num8 = Mathf.Min(num8, Settle(Mathf.Clamp01((unscaledTime - item.Start) / 0.3f)));
						}
					}
				}
				float num10 = ((num9 > 0f) ? (unscaledTime - num9) : 9f);
				float num11 = ((num10 < 0.6f) ? (0.2f * Mathf.Exp((0f - num10) * 10f) * Mathf.Cos(num10 * 22f)) : 0f);
				Rect r = Scaled(rect, num8 * (1f + num11));
				GameArt.StoneButton(r, null, Mathf.Clamp01(num8 * 1.5f), gems: false);
				if (num7 > 0)
				{
					Sprite sprite = ResourceIcon(key);
					Rect r2 = FitIcon(new Rect(r.x + r.width * 0.17f, r.y + r.height * 0.12f, r.width * 0.66f, r.height * 0.66f), sprite);
					GameArt.Icon(r2, sprite);
					if (num10 < 0.15f && sprite != null)
					{
						GameArt.Silhouette(r2, sprite, new Rect(0f, 0f, TexSize(sprite).x, TexSize(sprite).y), new Color(1f, 1f, 1f, 0.75f * (1f - num10 / 0.15f)));
					}
					Rect r3 = new Rect(rect.x, rect.yMax - 8f * p, num - 2f * p, 7f * p);
					float num12 = ((num10 < 0.5f) ? (0.55f * Mathf.Exp((0f - num10) * 11f)) : 0f);
					Matrix4x4 matrix = GUI.matrix;
					if (num12 > 0.002f)
					{
						GUIUtility.ScaleAroundPivot(new Vector2(1f + num12, 1f + num12), new Vector2(r3.xMax - 3f * p, r3.yMax - 3.5f * p));
					}
					GameArt.Label(r3, num7.ToString(), 4.6f, TextAnchor.LowerRight, Color.Lerp(GameArt.Peach, Color.white, Mathf.Clamp01(num12 * 1.8f)));
					GUI.matrix = matrix;
				}
			}
			if (_slotX.Count <= count)
			{
				return;
			}
			_slotsGone.Clear();
			foreach (string key2 in _slotX.Keys)
			{
				if (!_slotAt.ContainsKey(key2))
				{
					_slotsGone.Add(key2);
				}
			}
			foreach (string item2 in _slotsGone)
			{
				_slotX.Remove(item2);
			}
		}

		private void DrawBagGains(float sw, float sh)
		{
			Work work = _mod.Work;
			if (work.Gained.Count == 0)
			{
				return;
			}
			EnsureGlow();
			float unscaledTime = Time.unscaledTime;
			float p = GameArt.P;
			FrameProjection shown = _mod.Shown;
			float num = Mathf.Round(20f * p);
			foreach (BagGain item in work.Gained)
			{
				float num2 = unscaledTime - item.Start;
				if (num2 < 0f || unscaledTime >= item.Gone)
				{
					continue;
				}
				Sprite icon = ResourceIcon(item.Id);
				if (shown.Project(item.From.x, item.From.y, item.From.z, out var col, out var row, out var _))
				{
					item.LastSeen = new Vector2(col / (float)shown.Width * sw, row / (float)shown.Height * sh);
					item.Seen = true;
				}
				else if (!item.Seen)
				{
					item.LastSeen = new Vector2(sw * 0.5f, sh * 0.62f);
					item.Seen = true;
				}
				float num3 = ((float)item.Order - (float)(item.Of - 1) * 0.5f) * 23f * p;
				Vector2 vector = new Vector2(item.LastSeen.x, item.LastSeen.y + num3 - 13f * p * (1f - Mathf.Exp((0f - num2) / 0.08f)));
				if (num2 > 0.24f)
				{
					vector.y -= Mathf.Sin((Mathf.Min(num2, 0.52f) - 0.24f) * 9f) * 1.2f * p;
				}
				Rect value;
				Rect rect = (_slotAt.TryGetValue(item.Id, out value) ? value : BagButtonRect());
				Vector2 b = new Vector2(rect.center.x, rect.y + rect.height * 0.45f);
				float num4 = unscaledTime - item.LandAt;
				float num5 = Mathf.Clamp01((num2 - 0.05f) / 0.14f) * (1f - Smooth((num2 - 0.52f - 0.2f) / 0.35f));
				if (num5 > 0.01f)
				{
					float num6 = (1f - Smooth((num2 - 0.05f) / 0.2f)) * 6f * p;
					float num7 = Smooth((num2 - 0.52f - 0.1f) / 0.45f) * 9f * p;
					int px = Mathf.Max(12, Mathf.RoundToInt(9f * p));
					int px2 = Mathf.Max(11, Mathf.RoundToInt(7.2f * p));
					string text = "+" + item.Amount;
					string text2 = Work.ResourceName(item.Id);
					float num8 = TextWidth(text, px);
					float num9 = TextWidth(text2, px2);
					float num10 = Mathf.Round(2f * p);
					float num11 = vector.x - num * 0.5f - 4f * p - num8 - num10 - num9 + num6;
					float num12 = vector.y - num7;
					PopText(new Vector2(num11, num12), text, px, 1f, GainGold, num5, TextAnchor.MiddleLeft);
					PopText(new Vector2(num11 + num8 + num10, num12 + 0.5f * p), text2, px2, 1f, Color.white, num5, TextAnchor.MiddleLeft);
				}
				if (num2 < 0.52f)
				{
					float num13 = Settle(Mathf.Clamp01(num2 / 0.24f));
					float num14 = 0.16f * Bump(num2 - 0.40999997f, 0.11f);
					float alpha = 0.5f * Mathf.Clamp01(num13) * (0.85f + 0.15f * Mathf.Sin(num2 * 14f));
					Glow(vector, num * 2.3f * num13, alpha);
					DrawGain(icon, vector, num * num13 * (1f + num14), num * num13 * (1f - num14), 1f - Smooth(num2 / 0.16f), 1f);
					continue;
				}
				if (num4 < 0f)
				{
					float num15 = Mathf.Clamp01((num2 - 0.52f) / 0.46f);
					Vector2 c = new Vector2(vector.x, Mathf.Min(vector.y, b.y) - sh * 0.12f);
					for (int num16 = 7; num16 >= 1; num16--)
					{
						float num17 = num15 - (float)num16 * 0.035f;
						if (!(num17 <= 0f))
						{
							Vector2 vector2 = Arc(vector, c, b, Pull(num17));
							float num18 = 1f - (float)num16 / 8f;
							float num19 = Mathf.Max(2f, Mathf.Round(3.6f * p * num18));
							Rect r = new Rect(Mathf.Round(vector2.x - num19 * 0.5f), Mathf.Round(vector2.y - num19 * 0.5f), num19, num19);
							float num20 = Mathf.Max(1f, Mathf.Round(p * 0.6f));
							GameArt.Flat(new Rect(r.x + num20, r.y + num20, num19, num19), new Color(Rim.r, Rim.g, Rim.b, 0.45f * num18));
							GameArt.Flat(r, new Color(Spark.r, Spark.g, Spark.b, 0.85f * num18));
						}
					}
					float num21 = Pull(num15);
					Vector2 at = Arc(vector, c, b, num21);
					float num22 = Mathf.Lerp(num, rect.width * 0.66f, num21 * num21);
					float num23 = 0.12f * Bump(num15, 0.4f);
					Glow(at, num22 * 2.1f, 0.42f * (1f - 0.6f * num21));
					DrawGain(icon, at, num22 * (1f - num23), num22 * (1f + num23), 0f, 1f);
					continue;
				}
				if (!item.Landed)
				{
					item.Landed = true;
					if (num4 < 0.25f)
					{
						LandSound(unscaledTime);
					}
				}
				if (num4 < 0.4f)
				{
					float num24 = num4 / 0.4f;
					float num25 = 1f - (1f - num24) * (1f - num24);
					for (int i = 0; i < 8; i++)
					{
						float f = ((float)i / 8f + (float)item.Order * 0.07f + 0.03f) * MathF.PI * 2f;
						float num26 = rect.width * (0.5f + 0.45f * num25);
						Vector2 vector3 = new Vector2(b.x + Mathf.Cos(f) * num26, b.y + Mathf.Sin(f) * num26 * 0.9f);
						float num27 = Mathf.Max(1f, Mathf.Round(2.4f * p * (1f - num24)));
						Color c2 = Color.Lerp(Color.white, Spark, num24);
						c2.a = 1f - num24;
						GameArt.Flat(new Rect(Mathf.Round(vector3.x - num27 * 0.5f), Mathf.Round(vector3.y - num27 * 0.5f), num27, num27), c2);
					}
				}
				float num28 = num4 / 0.8f;
				float num29 = 1f - (1f - num28) * (1f - num28);
				float alpha2 = 1f - Smooth((num28 - 0.45f) / 0.55f);
				PopText(new Vector2(rect.center.x, rect.y - 4f * p - 9f * p * num29), "+" + item.Amount, Mathf.Max(11, Mathf.RoundToInt(6f * p)), 1f + 0.5f * Mathf.Exp((0f - num4) * 14f), Color.Lerp(Color.white, GainGold, Smooth(num4 / 0.15f)), alpha2);
			}
		}

		private static float Pull(float u)
		{
			return 0.3f * u + 0.7f * u * u * u;
		}

		private static Vector2 Arc(Vector2 a, Vector2 c, Vector2 b, float t)
		{
			float num = 1f - t;
			return num * num * a + 2f * num * t * c + t * t * b;
		}

		private static void DrawGain(Sprite icon, Vector2 at, float w, float h, float white, float alpha)
		{
			if (!(icon == null) && !(w < 0.5f) && !(h < 0.5f))
			{
				Rect r = new Rect(Mathf.Round(at.x - w * 0.5f), Mathf.Round(at.y - h * 0.5f), Mathf.Round(w), Mathf.Round(h));
				float num = Mathf.Max(1f, Mathf.Round(GameArt.P * 0.75f));
				float num2 = Mathf.Max(1f, Mathf.Round(GameArt.P * 0.5f));
				GameArt.Icon(new Rect(r.x + num, r.y + num * 1.5f, r.width, r.height), icon, new Color(0f, 0f, 0f, 0.4f * alpha));
				for (int i = 0; i < 4; i++)
				{
					GameArt.Icon(new Rect(r.x + ((i < 2) ? (0f - num2) : num2), r.y + ((i % 2 == 0) ? (0f - num2) : num2), r.width, r.height), icon, new Color(Rim.r, Rim.g, Rim.b, 0.55f * alpha));
				}
				GameArt.Icon(r, icon, new Color(1f, 1f, 1f, alpha));
				if (white > 0.01f)
				{
					GameArt.Silhouette(r, icon, new Rect(0f, 0f, TexSize(icon).x, TexSize(icon).y), new Color(1f, 1f, 1f, Mathf.Clamp01(white) * alpha));
				}
			}
		}

		private void Glow(Vector2 at, float size, float alpha)
		{
			if (!(_glow == null) && !(alpha <= 0.01f) && !(size < 1f))
			{
				Color color = GUI.color;
				GUI.color = new Color(1f, 0.93f, 0.7f, alpha);
				GUI.DrawTexture(new Rect(at.x - size * 0.5f, at.y - size * 0.5f, size, size), _glow);
				GUI.color = color;
			}
		}

		private void EnsureGlow()
		{
			if (_glow != null)
			{
				return;
			}
			_glow = new Texture2D(48, 48, TextureFormat.RGBA32, mipChain: false)
			{
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear,
				hideFlags = HideFlags.HideAndDontSave
			};
			Color32[] array = new Color32[2304];
			for (int i = 0; i < 48; i++)
			{
				for (int j = 0; j < 48; j++)
				{
					float num = ((float)j + 0.5f) / 48f * 2f - 1f;
					float num2 = ((float)i + 0.5f) / 48f * 2f - 1f;
					float num3 = Mathf.Clamp01(Mathf.Sqrt(num * num + num2 * num2));
					float num4 = (1f - num3) * (1f - num3);
					array[i * 48 + j] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)(num4 * 255f));
				}
			}
			_glow.SetPixels32(array);
			_glow.Apply(updateMipmaps: false);
		}

		private void LandSound(float now)
		{
			if (now - _lastLandSound < 0.05f)
			{
				return;
			}
			_landStreak = ((now - _lastLandSound < 1.2f) ? Mathf.Min(_landStreak + 1, 7) : 0);
			_lastLandSound = now;
			try
			{
				_mod.Audio.PlayPickup(_landStreak);
			}
			catch
			{
			}
		}

		private static Rect Scaled(Rect r, float s)
		{
			if (Mathf.Abs(s - 1f) < 0.001f)
			{
				return r;
			}
			float num = Mathf.Round(r.width * s);
			float num2 = Mathf.Round(r.height * s);
			return new Rect(Mathf.Round(r.center.x - num * 0.5f), Mathf.Round(r.center.y - num2 * 0.5f), num, num2);
		}

		private static Rect FitIcon(Rect r, Sprite s)
		{
			if (s == null)
			{
				return r;
			}
			Vector2 vector = TexSize(s);
			float num = vector.x / Mathf.Max(1f, vector.y);
			if (!(num >= 1f))
			{
				return new Rect(r.x + r.width * (1f - num) * 0.5f, r.y, r.width * num, r.height);
			}
			return new Rect(r.x, r.y + r.height * (1f - 1f / num) * 0.5f, r.width, r.height / num);
		}

		private static Vector2 TexSize(Sprite s)
		{
			Rect rect;
			try
			{
				rect = s.textureRect;
			}
			catch
			{
				rect = s.rect;
			}
			return new Vector2(rect.width, rect.height);
		}

		private void PortraitWell(Rect r, Sprite s, Actor who, float alpha = 1f)
		{
			Texture2D texture2D = ((who != null) ? _mod.PortraitOf(who) : null);
			if (texture2D == null)
			{
				PortraitWell(r, s, alpha);
				return;
			}
			PortraitWell(r, null, alpha);
			if (Event.current.type == EventType.Repaint)
			{
				float p = GameArt.P;
				float num = Mathf.Max(1.5f * p, r.width * 0.08f);
				Color color = GUI.color;
				GUI.color = new Color(color.r, color.g, color.b, color.a * alpha);
				GUI.DrawTexture(new Rect(r.x + num, r.y + num, r.width - num * 2f, r.height - num * 2f), texture2D, ScaleMode.ScaleToFit, alphaBlend: true);
				GUI.color = color;
			}
		}

		private void DrawPoseWheel(float sw, float sh)
		{
			if (_mod.PoseWheelOpen && Event.current.type == EventType.Repaint)
			{
				Skin.Ensure();
				float p = GameArt.P;
				float num = Smooth((Time.unscaledTime - _mod.PoseWheelOpenedAt) / 0.14f);
				Vector2 c = new Vector2(Mathf.Round(sw * 0.5f), Mathf.Round(sh * 0.5f));
				float num2 = Mathf.Round(60f * p * (0.85f + 0.15f * num));
				float num3 = Mathf.Round(22f * p * (0.85f + 0.15f * num));
				int num4 = WorldBoxMod.PoseWheel.Length;
				int poseWheelChoice = _mod.PoseWheelChoice;
				float num5 = 360f / (float)num4;
				float num6 = 1.6f;
				Color color = new Color(0.86f, 0.74f, 0.5f);
				for (int i = 0; i < num4; i++)
				{
					float a = (float)i * num5 - num5 * 0.5f + num6;
					float a2 = (float)i * num5 + num5 * 0.5f - num6;
					bool flag = i == poseWheelChoice;
					Color color2 = (flag ? new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, 0.82f * num) : new Color(0.05f, 0.06f, 0.08f, 0.62f * num));
					float num7 = num2 + (flag ? (4f * p) : 0f);
					Sector(c, num3, num7, a, a2, color2);
					Sector(c, num7 - Mathf.Max(1f, Mathf.Round(p)), num7, a, a2, new Color(color.r, color.g, color.b, (flag ? 1f : 0.5f) * num));
				}
				for (int j = 0; j < num4; j++)
				{
					bool flag2 = j == poseWheelChoice;
					float f = (float)j * num5 * (MathF.PI / 180f);
					float num8 = (num3 + num2) * 0.5f + (flag2 ? (2f * p) : 0f);
					Vector2 vector = new Vector2(Mathf.Round(c.x + Mathf.Sin(f) * num8), Mathf.Round(c.y - Mathf.Cos(f) * num8));
					GameArt.Label(new Rect(vector.x - 30f * p, vector.y - 5f * p, 60f * p, 10f * p), WorldBoxMod.PoseName(WorldBoxMod.PoseWheel[j]), flag2 ? 5f : 4.6f, TextAnchor.MiddleCenter, flag2 ? new Color(color.r, color.g, color.b, num) : new Color(0.85f, 0.85f, 0.85f, 0.85f * num));
				}
				Color color3 = GUI.color;
				GUI.color = new Color(0.04f, 0.04f, 0.05f, 0.75f * num);
				GUI.DrawTexture(new Rect(c.x - num3 + 2f * p, c.y - num3 + 2f * p, num3 * 2f - 4f * p, num3 * 2f - 4f * p), Skin.DiscTex);
				GUI.color = color3;
				string text = ((poseWheelChoice >= 0) ? WorldBoxMod.PoseName(WorldBoxMod.PoseWheel[poseWheelChoice]) : "Cancel");
				Color color4 = ((poseWheelChoice >= 0) ? color : GameArt.Grey);
				GameArt.Label(new Rect(c.x - num3, c.y - 5f * p, num3 * 2f, 10f * p), text, 5f, TextAnchor.MiddleCenter, new Color(color4.r, color4.g, color4.b, num));
				string text2 = "Tap P to sit or get up · E on a seat sits in it";
				float num9 = GameArt.Measure(text2, 4.6f).x + 12f * p;
				Rect r = new Rect(Mathf.Round(c.x - num9 * 0.5f), Mathf.Round(c.y + num2 + 8f * p), Mathf.Round(num9), Mathf.Round(11f * p));
				GameArt.TipPanel(r, num);
				GameArt.Label(r, text2, 4.6f, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, num));
				WheelDrawn = true;
			}
		}

		private float DrawPurse(float right, float y, float size, Actor host, float alpha)
		{
			float p = GameArt.P;
			float unscaledTime = Time.unscaledTime;
			int num = Economy.Money(host);
			if (host != _purseHost)
			{
				_purseHost = host;
				_purseMoney = num;
				_purseShown = num;
				_purseChanges.Clear();
			}
			if (num != _purseMoney)
			{
				_purseChanges.Add(new PurseChange
				{
					Amount = num - _purseMoney,
					At = unscaledTime
				});
				_purseMoney = num;
			}
			float num2 = ((_purseStepped >= 0f) ? Mathf.Clamp(unscaledTime - _purseStepped, 0f, 0.1f) : 0f);
			_purseStepped = unscaledTime;
			_purseShown = Mathf.MoveTowards(_purseShown, num, Mathf.Max(8f, Mathf.Abs((float)num - _purseShown) * 4f) * num2);
			int a = Mathf.RoundToInt(_purseShown);
			int count = Economy.Hired.Count;
			int count2 = Economy.ProtectorsOf(host).Count;
			if (count != _underGuards || count2 != _underSworn)
			{
				_underGuards = count;
				_underSworn = count2;
				_under = ((count > 0 && count2 > 0) ? string.Format("{0} guard{1} · sworn", count, (count > 1) ? "s" : "") : ((count > 0) ? string.Format("{0} guard{1}", count, (count > 1) ? "s" : "") : ((count2 <= 0) ? null : ((count2 == 1) ? "protected" : (count2 + " realms")))));
			}
			string under = _under;
			string text = _purseText.Of(a);
			float num3 = Mathf.Round((26f + 4.2f * (float)Mathf.Max(text.Length, 3) + ((under != null) ? 6f : 0f)) * p);
			Rect r = new Rect(right - num3, y, num3, size);
			if (Event.current.type != EventType.Repaint)
			{
				return r.x;
			}
			GameArt.Sliced(r, GameArt.Inner, new Color(1f, 1f, 1f, alpha));
			Sprite sprite = GameArt.IconNamed("iconMoney") ?? GameArt.IconNamed("iconGold");
			float num4 = ((_purseChanges.Count > 0) ? (unscaledTime - _purseChanges[_purseChanges.Count - 1].At) : 9f);
			float num5 = ((num4 < 0.45f) ? (0.25f * Mathf.Exp((0f - num4) * 9f) * Mathf.Cos(num4 * 20f)) : 0f);
			float num6 = Mathf.Round(11f * p * (1f + num5));
			float num7 = ((under != null) ? (r.y + 2.5f * p) : (r.y + (size - num6) * 0.5f));
			if (sprite != null)
			{
				GameArt.Icon(new Rect(r.x + 4f * p, num7, num6, num6), sprite, new Color(1f, 1f, 1f, alpha));
			}
			Color color = Color.Lerp(new Color(1f, 0.85f, 0.3f), Color.white, Mathf.Clamp01(num5 * 4f));
			color.a = alpha;
			GameArt.Label(new Rect(r.x + 17f * p, num7 - 0.5f * p, num3 - 20f * p, num6), text, 5.4f, TextAnchor.MiddleLeft, color);
			if (under != null)
			{
				GameArt.Label(new Rect(r.x + 3f * p, r.yMax - 8.5f * p, num3 - 6f * p, 7f * p), under, 3.8f, TextAnchor.MiddleCenter, new Color((count > 0) ? 0.62f : 0.55f, (count > 0) ? 0.9f : 0.8f, 1f, alpha));
			}
			for (int num8 = _purseChanges.Count - 1; num8 >= 0; num8--)
			{
				PurseChange purseChange = _purseChanges[num8];
				float num9 = unscaledTime - purseChange.At;
				if (num9 > 1.6f)
				{
					_purseChanges.RemoveAt(num8);
				}
				else
				{
					int num10 = _purseChanges.Count - 1 - num8;
					float num11 = (14f + 16f * (1f - Mathf.Exp((0f - num9) * 3f)) + (float)num10 * 9f) * p;
					float a2 = alpha * Mathf.Clamp01(1.6f - num9) * Mathf.Clamp01(num9 * 12f);
					Color color2 = ((purseChange.Amount > 0) ? new Color(0.55f, 1f, 0.45f, a2) : new Color(1f, 0.55f, 0.35f, a2));
					GameArt.Label(new Rect(r.x, r.y - num11, num3, 9f * p), ((purseChange.Amount > 0) ? "+" : "") + purseChange.Amount, 5f, TextAnchor.MiddleCenter, color2);
				}
			}
			return r.x;
		}

		private void EnsureQuestStyles()
		{
			float u = Skin.U;
			if (_questTitle == null || !Mathf.Approximately(_questU, u))
			{
				_questU = u;
				_questTitle = Skin.Text(15f * u, bold: true);
				_questTitle.clipping = TextClipping.Clip;
				_questLine = Skin.Text(13f * u);
				_questLine.clipping = TextClipping.Clip;
				_wantedStyle = Skin.Text(15f * u, bold: true, TextAnchor.MiddleRight);
			}
		}

		private void DrawCivilHud(float sw, float sh)
		{
			if (_mod.Converse.Open || Law.BoardOpen || _mod.Craft.Open || _mod.Royal.Open)
			{
				return;
			}
			Skin.Ensure();
			EnsureQuestStyles();
			_ = Skin.U;
			Actor actor = _mod.Body ?? _mod.Host;
			if (actor == null)
			{
				return;
			}
			DrawQuestStars(sw, sh, actor);
			DrawMarkers(sw, sh, actor);
			if (!VrView)
			{
				float p = GameArt.P;
				float num = Mathf.Round(Mathf.Min(150f * p, sw * 0.32f));
				float x = Mathf.Round(sw - num - 6f * p);
				float y = Mathf.Round(Mathf.Max(sh * 0.17f, MinimapBottom(sw, sh) + 4f * p));
				DrawCivilCards(x, y, num, actor);
				float num2 = Births.Progress(actor);
				if (num2 >= 0f)
				{
					string text = Births.ExpectingLabel(actor) ?? "Expecting";
					float num3 = Mathf.Max(_nameplateBottom, 60f * p) + 3f * p;
					GameArt.Label(new Rect(6f * p, num3, 160f * p, 8f * p), text, 4.8f, TextAnchor.MiddleLeft, new Color(1f, 0.8f, 0.86f));
					GameArt.Bar(new Rect(6f * p, num3 + 9f * p, 100f * p, 6f * p), num2, new Color(0.92f, 0.5f, 0.62f));
				}
			}
		}

		private float DrawCivilCards(float x, float y, float w, Actor host)
		{
			float p = GameArt.P;
			List<KeyValuePair<Kingdom, int>> list = Law.WantedCached(host);
			foreach (KeyValuePair<Kingdom, int> item in list)
			{
				Rect r = new Rect(x, y, w, Mathf.Round(10f * p));
				GameArt.TipPanel(r, 0.9f);
				GameArt.Label(new Rect(r.x + 4f * p, r.y - 0.5f * p, r.width - 8f * p, r.height), "Wanted in " + item.Key.name + ": " + item.Value + " gold", 4.8f, TextAnchor.MiddleRight, new Color(1f, 0.42f, 0.32f));
				y += r.height + 2f * p;
			}
			if (list.Count > 0)
			{
				y += 2f * p;
			}
			y = DrawSiegeCards(x, y, w, host);
			y = DrawServiceCard(x, y, w, host);
			foreach (Quest item2 in Quests.Log)
			{
				if (item2.Stage < 2)
				{
					Rect r2 = new Rect(x, y, w, Mathf.Round(20f * p));
					GameArt.TipPanel(r2, 0.92f);
					bool flag = item2.Stage == 1;
					Sprite star = GameArt.Star;
					float num = (flag ? Mathf.Round(Mathf.Sin(Time.unscaledTime * 4f) * 0.8f * p) : 0f);
					bool flag2 = Quests.IsCourt(item2.Kind);
					GameArt.Icon(new Rect(r2.x + 3f * p, r2.y + 3f * p + num, 11f * p, 11f * p), star, flag ? new Color(0.55f, 1f, 0.45f) : (flag2 ? new Color(1f, 0.62f, 0.78f) : Color.white));
					GameArt.Label(new Rect(r2.x + 17f * p, r2.y + 1.5f * p, w - 21f * p, 9f * p), Quests.Title(item2), 5f, TextAnchor.MiddleLeft, flag ? new Color(0.6f, 1f, 0.5f) : Color.white);
					string text = Quests.Status(item2);
					if (Quests.Heading(item2, out var at))
					{
						text += $"   ({(at - host.current_position).magnitude:F0} tiles)";
					}
					bool flag3 = item2.Kind == QuestKind.Raid && item2.Stage == 0 && Raiders.Current != null && !Raiders.Current.Over;
					GameArt.Label(new Rect(r2.x + 17f * p, r2.y + 10f * p, w - 21f * p, 8f * p), text, 4.2f, TextAnchor.MiddleLeft, flag3 ? GameArt.Orange : GameArt.Grey);
					y += r2.height + 2f * p;
				}
			}
			return y;
		}

		private void DrawQuestStars(float sw, float sh, Actor host)
		{
			if (Event.current.type != EventType.Repaint)
			{
				return;
			}
			StarsGold = (StarsPale = 0);
			Quests.Starred(_starred);
			if (_starred.Count == 0)
			{
				return;
			}
			if (_star == null)
			{
				_star = MakeStar();
			}
			FrameProjection shown = _mod.Shown;
			if (shown.Width <= 0 || shown.Height <= 0)
			{
				return;
			}
			float u = Skin.U;
			float num = shown.Focal / (float)shown.Height * sh;
			Actor speaker = _mod.Talk.Speaker;
			float unscaledTime = Time.unscaledTime;
			Color color = GUI.color;
			foreach (KeyValuePair<Actor, bool> item in _starred)
			{
				Actor key = item.Key;
				if (key == host || key == speaker || !_mod.StarHead(key, out var head))
				{
					continue;
				}
				float num2 = (float)(key.data.id % 13) * 0.7f;
				float num3 = Mathf.Sin(unscaledTime * 2.3f + num2) * 0.045f;
				if (!shown.Project(head.x, head.y, head.z + 0.14f + num3, out var col, out var row, out var depth, out var along) || col < (float)(-shown.Width) * 0.05f || col > (float)shown.Width * 1.05f || row < (float)(-shown.Height) * 0.05f || row > (float)shown.Height * 1.05f)
				{
					continue;
				}
				float num4 = 1f - Mathf.Clamp01((depth - 45f) / 15f);
				if (!(num4 <= 0f))
				{
					float num5 = Mathf.Clamp(0.22f * num / along, 11f * u, 30f * u) * (1f + 0.06f * Mathf.Sin(unscaledTime * 4.1f + num2));
					float num6 = col / (float)shown.Width * sw;
					float num7 = row / (float)shown.Height * sh;
					bool value = item.Value;
					GUI.color = (value ? new Color(1f, 0.84f, 0.28f, 0.97f * num4) : new Color(0.86f, 0.89f, 0.96f, 0.78f * num4));
					GUI.DrawTexture(new Rect(num6 - num5 * 0.5f, num7 - num5 * 0.5f, num5, num5), _star);
					if (value)
					{
						StarsGold++;
					}
					else
					{
						StarsPale++;
					}
				}
			}
			GUI.color = color;
		}

		private static Texture2D MakeStar()
		{
			Vector2[] array = new Vector2[10];
			float num = 30.08f;
			float num2 = num * 0.47f;
			for (int i = 0; i < 10; i++)
			{
				float f = MathF.PI / 2f + (float)i * MathF.PI / 5f;
				float num3 = ((i % 2 == 0) ? num : num2);
				array[i] = new Vector2(32f + Mathf.Cos(f) * num3, 30.08f + Mathf.Sin(f) * num3);
			}
			float num4 = 4.8f;
			Color32[] array2 = new Color32[4096];
			for (int j = 0; j < 64; j++)
			{
				for (int k = 0; k < 64; k++)
				{
					float num5 = PolygonDistance(new Vector2((float)k + 0.5f, (float)j + 0.5f), array);
					float num6 = Mathf.Clamp01(0.5f - num5);
					if (num6 <= 0f)
					{
						array2[j * 64 + k] = new Color32(0, 0, 0, 0);
						continue;
					}
					Color color;
					if (0f - num5 < num4)
					{
						color = new Color(0.16f, 0.1f, 0.05f);
					}
					else
					{
						float num7 = Mathf.Lerp(0.74f, 1f, (float)j / 64f);
						if (0f - num5 < num4 * 1.9f && (float)j > 28.8f)
						{
							num7 = Mathf.Min(1f, num7 + 0.12f);
						}
						color = new Color(num7, num7, num7);
					}
					color.a = num6;
					array2[j * 64 + k] = color;
				}
			}
			Texture2D texture2D = new Texture2D(64, 64, TextureFormat.RGBA32, mipChain: true);
			texture2D.filterMode = FilterMode.Trilinear;
			texture2D.wrapMode = TextureWrapMode.Clamp;
			texture2D.hideFlags = HideFlags.HideAndDontSave;
			texture2D.SetPixels32(array2);
			texture2D.Apply(updateMipmaps: true);
			return texture2D;
		}

		private static float PolygonDistance(Vector2 p, Vector2[] v)
		{
			float num = (p - v[0]).sqrMagnitude;
			float num2 = 1f;
			int i = 0;
			int num3 = v.Length - 1;
			for (; i < v.Length; i++)
			{
				Vector2 vector = v[num3] - v[i];
				Vector2 vector2 = p - v[i];
				num = Mathf.Min(num, (vector2 - vector * Mathf.Clamp01(Vector2.Dot(vector2, vector) / Vector2.Dot(vector, vector))).sqrMagnitude);
				bool flag = p.y >= v[i].y;
				bool flag2 = p.y < v[num3].y;
				bool flag3 = vector.x * vector2.y > vector.y * vector2.x;
				if ((flag && flag2 && flag3) || (!flag && !flag2 && !flag3))
				{
					num2 = 0f - num2;
				}
				num3 = i;
			}
			return num2 * Mathf.Sqrt(num);
		}

		private void DrawRadialWheels(float sw, float sh)
		{
			DrawRadialWheel(_mod.ActionWheel, sw, sh);
			DrawRadialWheel(_mod.ToolWheel, sw, sh);
		}

		private void DrawRadialWheel(RadialWheel w, float sw, float sh)
		{
			if (w == null || !w.Open || w.Count == 0 || Event.current.type != EventType.Repaint)
			{
				return;
			}
			Skin.Ensure();
			float p = GameArt.P;
			float num = Smooth((Time.unscaledTime - w.OpenedAt) / 0.14f);
			Vector2 vector = new Vector2(Mathf.Round(sw * 0.5f), Mathf.Round(sh * 0.5f));
			float num2 = Mathf.Round(64f * p * (0.85f + 0.15f * num));
			float num3 = Mathf.Round(24f * p * (0.85f + 0.15f * num));
			w.ShownAt = vector;
			w.ShownOuter = 64f * p;
			int count = w.Count;
			int choice = w.Choice;
			float num4 = 360f / (float)count;
			float num5 = ((count > 1) ? 1.6f : 0f);
			Color tint = w.Tint;
			for (int i = 0; i < count; i++)
			{
				float a = (float)i * num4 - num4 * 0.5f + num5;
				float a2 = (float)i * num4 + num4 * 0.5f - num5;
				bool flag = i == choice;
				Color color = (flag ? (w.Dim[i] ? new Color(0.22f, 0.2f, 0.2f, 0.8f * num) : new Color(tint.r * 0.5f, tint.g * 0.5f, tint.b * 0.5f, 0.82f * num)) : new Color(0.05f, 0.06f, 0.08f, 0.62f * num));
				float num6 = num2 + (flag ? (4f * p) : 0f);
				Sector(vector, num3, num6, a, a2, color);
				Sector(vector, num6 - Mathf.Max(1f, Mathf.Round(p)), num6, a, a2, new Color(tint.r, tint.g, tint.b, (flag ? 1f : 0.5f) * (w.Dim[i] ? 0.4f : 1f) * num));
			}
			for (int j = 0; j < count; j++)
			{
				bool flag2 = j == choice;
				float f = (float)j * num4 * (MathF.PI / 180f);
				float num7 = (num3 + num2) * 0.5f + (flag2 ? (2f * p) : 0f);
				Vector2 vector2 = new Vector2(Mathf.Round(vector.x + Mathf.Sin(f) * num7), Mathf.Round(vector.y - Mathf.Cos(f) * num7));
				Color color2 = (w.Dim[j] ? new Color(0.55f, 0.55f, 0.55f, 0.7f * num) : (flag2 ? new Color(tint.r, tint.g, tint.b, num) : new Color(0.88f, 0.88f, 0.88f, 0.9f * num)));
				GameArt.Label(new Rect(vector2.x - 34f * p, vector2.y - 5f * p, 68f * p, 10f * p), w.Labels[j], flag2 ? 5f : 4.6f, TextAnchor.MiddleCenter, color2);
			}
			Color color3 = GUI.color;
			GUI.color = new Color(0.04f, 0.04f, 0.05f, 0.75f * num);
			GUI.DrawTexture(new Rect(vector.x - num3 + 2f * p, vector.y - num3 + 2f * p, num3 * 2f - 4f * p, num3 * 2f - 4f * p), Skin.DiscTex);
			GUI.color = color3;
			int num8;
			object obj;
			if (choice >= 0)
			{
				num8 = ((choice < count) ? 1 : 0);
				if (num8 != 0)
				{
					obj = w.Labels[choice];
					goto IL_0409;
				}
			}
			else
			{
				num8 = 0;
			}
			obj = "Cancel";
			goto IL_0409;
			IL_0409:
			string text = (string)obj;
			Color color4 = ((num8 != 0 && !w.Dim[choice]) ? tint : GameArt.Grey);
			GameArt.Label(new Rect(vector.x - num3, vector.y - 5f * p, num3 * 2f, 10f * p), text, 4.8f, TextAnchor.MiddleCenter, new Color(color4.r, color4.g, color4.b, num));
			string text2 = ((num8 != 0 && w.Dim[choice] && w.Notes[choice] != null) ? w.Notes[choice] : w.Tip);
			if (!string.IsNullOrEmpty(text2))
			{
				float num9 = GameArt.Measure(text2, 4.6f).x + 12f * p;
				Rect r = new Rect(Mathf.Round(vector.x - num9 * 0.5f), Mathf.Round(vector.y + num2 + 8f * p), Mathf.Round(num9), Mathf.Round(11f * p));
				GameArt.TipPanel(r, num);
				GameArt.Label(r, text2, 4.6f, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, num));
			}
		}

		private float DrawStyleRow(float x, float y, float w, Kingdom home, Actor host)
		{
			for (int i = 0; i < StyleRectsForTest.Length; i++)
			{
				StyleRectsForTest[i] = default(Rect);
			}
			_styleTip = -1;
			if (!Royal.IsKing(host) || !Regalia.Styled(home))
			{
				return y;
			}
			float p = GameArt.P;
			float unscaledTime = Time.unscaledTime;
			float num = Mathf.Round(17f * p);
			float num2 = Mathf.Round(15f * p);
			KingdomStyle style = Regalia.LookOf(home).Style;
			if (Event.current.type == EventType.Repaint)
			{
				GameArt.IconFit(new Rect(x, y + Mathf.Round((num - num2) * 0.5f), num2, num2), GameArt.Crown, Color.white);
			}
			bool onPhone = Device.OnPhone;
			if (_styleTapped >= 0 && (!onPhone || unscaledTime - _styleTappedAt > 3f))
			{
				_styleTapped = -1;
			}
			float num3 = Mathf.Floor((w - 21f * p) / 3f);
			float num4 = x + 17f * p;
			for (int j = 0; j < RowStyles.Length; j++)
			{
				KingdomStyle kingdomStyle = RowStyles[j];
				Rect rect = new Rect(Mathf.Round(num4 + (float)j * (num3 + 2f * p)), y, num3, num);
				StyleRectsForTest[j] = rect;
				bool flag = kingdomStyle == style;
				bool flag2 = ((StyleHoverForTest >= 0) ? (StyleHoverForTest == j) : (onPhone ? (_styleTapped == j) : rect.Contains(Event.current.mousePosition)));
				if (GameButton(rect, Regalia.StyleName(kingdomStyle), flag, enabled: true, null, 4f, !flag))
				{
					if (onPhone && _styleTapped != j)
					{
						_styleTapped = j;
						_styleTappedAt = unscaledTime;
						flag2 = true;
					}
					else
					{
						_styleTapped = -1;
						if (Regalia.SetStyle(host, kingdomStyle))
						{
							_mod.Toast("Royal style: " + Regalia.StyleName(kingdomStyle) + ".");
						}
					}
				}
				if (flag && Event.current.type == EventType.Repaint)
				{
					GameArt.Sliced(rect, GameArt.ButtonBorder, new Color(1f, 0.82f, 0.3f, 1f), 4f, 4f, 4f, 4f);
				}
				if (flag2)
				{
					_styleTip = j;
					Regalia.PreviewStyle(home, kingdomStyle);
				}
			}
			_styleRow = new Rect(x, y, w, num);
			return y + num + 4f * p;
		}

		private void DrawStyleTip()
		{
			int styleTip = _styleTip;
			_styleTip = -1;
			if (styleTip < 0 || styleTip >= RowStyles.Length || Event.current.type != EventType.Repaint)
			{
				return;
			}
			float p = GameArt.P;
			bool onPhone = Device.OnPhone;
			float num = Mathf.Round(70f * p);
			float num2 = Mathf.Round((onPhone ? 108f : 100f) * p);
			float x = Mathf.Min(Mathf.Round(_styleRow.xMax + 4f * p), (float)Screen.width - num - 2f * p);
			float y = Mathf.Clamp(Mathf.Round(_styleRow.y - 30f * p), 2f * p, (float)Screen.height - num2 - 2f * p);
			Rect r = new Rect(x, y, num, num2);
			GameArt.TipPanel(r);
			Rect r2 = new Rect(r.x + 4f * p, r.y + 4f * p, Mathf.Round(62f * p), Mathf.Round(74f * p));
			PortraitWell(r2, null);
			float num3 = Mathf.Max(1f, Mathf.Round(p));
			float num4 = Mathf.Round(3f * p);
			int num5 = Mathf.FloorToInt((r2.width - num4 * 2f) / num3);
			int num6 = Mathf.FloorToInt((r2.height - num4 * 2f) / num3);
			Rect position = new Rect(Mathf.Round(r2.center.x - (float)num5 * num3 * 0.5f), Mathf.Round(r2.center.y - (float)num6 * num3 * 0.5f), (float)num5 * num3, (float)num6 * num3);
			if (_mod.DrawYou(num5, num6) && _mod.YouView.Picture != null)
			{
				GUI.DrawTexture(position, _mod.YouView.Picture);
			}
			else
			{
				Sprite s = null;
				try
				{
					s = ((_mod.Host != null) ? _mod.Host.getSpriteToRender() : null);
				}
				catch
				{
				}
				float num7 = Mathf.Round(r2.width * 0.18f);
				DrawSprite(new Rect(position.x + num7, position.y + num7, position.width - num7 * 2f, position.height - num7 * 2f), s);
			}
			float x2 = r.x + 4f * p;
			float width = r.width - 8f * p;
			float num8 = r2.yMax + 1f * p;
			GameArt.Label(new Rect(x2, num8, width, 8f * p), Regalia.StyleName(RowStyles[styleTip]), 5.2f, TextAnchor.MiddleCenter, GameArt.Orange);
			GameArt.Label(new Rect(x2, num8 + 8f * p, width, 12f * p), "Crowns, hall flags and captains' banners.", 3.8f, TextAnchor.UpperCenter, GameArt.Grey, wrap: true);
			if (onPhone)
			{
				GameArt.Label(new Rect(x2, num8 + 20f * p, width, 7f * p), "Tap again to choose.", 3.8f, TextAnchor.MiddleCenter, Color.white);
			}
		}

		private static MeterState Meters(MeterState m, Actor npc, Actor you, bool fresh = false)
		{
			float unscaledTime = Time.unscaledTime;
			if (!fresh && m.For == npc && m.You == you && unscaledTime < m.Until)
			{
				return m;
			}
			m.For = npc;
			m.You = you;
			m.Until = unscaledTime + 0.25f;
			m.Opinion = Chronicle.Opinion(npc, you);
			m.Mood = Dialogue.SafeHappiness(npc);
			m.Romance = (m.Courting = (m.Lovers = (m.Taken = false)));
			try
			{
				m.Lovers = npc.lover == you;
				FlirtOutcome? flirtOutcome = Romance.Blocker(you, npc);
				if (m.Lovers)
				{
					m.Romance = true;
					m.Affection = (m.Reach = 100);
				}
				else if (!flirtOutcome.HasValue)
				{
					Suitor suitor = Romance.Of(npc, you);
					m.Romance = true;
					m.Affection = suitor.Affection;
					m.Reach = Mathf.Min(100, suitor.Affection + Romance.RoomToday(npc, you, suitor));
					m.Courting = suitor.Asked || suitor.Done > 0;
					m.Done = suitor.Done;
					m.Wanted = suitor.Wanted;
					m.Taken = Romance.Taken(npc, you);
					if (m.Taken)
					{
						m.Devotion = Romance.Devotion(npc);
						m.Partner = Dialogue.NameOf(npc.lover);
					}
				}
			}
			catch
			{
				m.Romance = false;
			}
			return m;
		}

		private void DrawTalkMeters(Conversation c, Rect head)
		{
			if (!MetersHeld || c.With == null || c.You == null || (c.Stranger != null && c.Page < c.NameAt))
			{
				return;
			}
			MeterState meterState = Meters(_talkMeters, c.With, c.You);
			float p = GameArt.P;
			float num = Mathf.Round(9f * p);
			float num2 = Mathf.Round(4f * p);
			int num3 = 2 + (meterState.Romance ? 1 : 0) + (meterState.Taken ? 1 : 0);
			Rect r = new Rect(head.x, head.y - (float)num3 * num - num2 * 2f - 2f * p, head.width, (float)num3 * num + num2 * 2f);
			if (r.y < 2f * p)
			{
				r.y = head.yMax + 2f * p;
			}
			if (Event.current.type == EventType.Repaint)
			{
				GameArt.TipPanel(r);
				float y = r.y + num2;
				Rect r2 = new Rect(r.x + num2, y, r.width - num2 * 2f, num);
				if (meterState.Romance)
				{
					string label = (meterState.Lovers ? "Romance" : (meterState.Courting ? $"Courting {Mathf.Min(meterState.Done, meterState.Wanted)}/{meterState.Wanted}" : "Romance"));
					string value = (meterState.Lovers ? "Yours" : ((meterState.Affection >= 100) ? "Full" : meterState.Affection.ToString()));
					MeterRow(r2, label, value, (float)meterState.Affection / 100f, RomancePink, (float)meterState.Reach / 100f, centred: false);
					r2.y += num;
				}
				MeterRow(r2, "Opinion", Signed(meterState.Opinion), (float)meterState.Opinion / 100f, (meterState.Opinion >= 0) ? GoodGreen : BadRed, -1f, centred: true);
				r2.y += num;
				MeterRow(r2, "Mood", Signed(meterState.Mood), (float)meterState.Mood / 100f, (meterState.Mood >= 0) ? MoodGold : BadRed, -1f, centred: true);
				r2.y += num;
				if (meterState.Taken)
				{
					MeterRow(r2, "Devotion to " + meterState.Partner, meterState.Devotion.ToString(), (float)meterState.Devotion / 100f, DevotionViolet, -1f, centred: false);
				}
			}
		}

		private bool DrawRowMeters(Rect line, Actor npc, Actor you)
		{
			if (!MetersHeld || npc == null || you == null)
			{
				return false;
			}
			MeterState meterState = Meters(_rowMeters, npc, you, fresh: true);
			if (Event.current.type != EventType.Repaint)
			{
				return true;
			}
			float p = GameArt.P;
			float num = Mathf.Round((line.width - 4f * p) * 0.5f);
			Rect rect = new Rect(line.x, line.y, num, line.height);
			Rect rect2 = new Rect(line.x + num + 4f * p, line.y, num, line.height);
			if (meterState.Romance)
			{
				MiniBar(rect, meterState.Lovers ? "Yours" : (meterState.Courting ? "Courting" : "Romance"), (float)meterState.Affection / 100f, RomancePink, centred: false);
			}
			MiniBar(meterState.Romance ? rect2 : rect, "Opinion", (float)meterState.Opinion / 100f, (meterState.Opinion >= 0) ? GoodGreen : BadRed, centred: true);
			return true;
		}

		private static string Signed(int v)
		{
			if (v <= 0)
			{
				return v.ToString();
			}
			return "+" + v;
		}

		private static void MeterRow(Rect r, string label, string value, float fill, Color color, float reach, bool centred)
		{
			float p = GameArt.P;
			float num = Mathf.Round(Mathf.Max(r.width * 0.26f, GameArt.Measure(label, 4.2f).x + 6f * p));
			float num2 = Mathf.Round(18f * p);
			GameArt.Label(new Rect(r.x, r.y, num, r.height), label, 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
			Rect bar = new Rect(r.x + num, r.y + Mathf.Round(r.height * 0.3f), r.width - num - num2 - 3f * p, Mathf.Max(2f, Mathf.Round(r.height * 0.4f)));
			Bar(bar, fill, color, reach, centred);
			GameArt.Label(new Rect(bar.xMax + 3f * p, r.y, num2, r.height), value, 4.2f, TextAnchor.MiddleRight, Color.white);
		}

		private static void MiniBar(Rect r, string label, float fill, Color color, bool centred)
		{
			_ = GameArt.P;
			float num = Mathf.Round(r.width * 0.45f);
			GameArt.Label(new Rect(r.x, r.y, num, r.height), label, 3.7f, TextAnchor.MiddleLeft, GameArt.Grey);
			Bar(new Rect(r.x + num, r.y + Mathf.Round(r.height * 0.32f), r.width - num, Mathf.Max(2f, Mathf.Round(r.height * 0.36f))), fill, color, -1f, centred);
		}

		private static void Bar(Rect bar, float fill, Color color, float reach, bool centred)
		{
			GameArt.Flat(bar, BarBack);
			if (centred)
			{
				float num = bar.x + bar.width * 0.5f;
				float num2 = Mathf.Clamp(fill, -1f, 1f) * bar.width * 0.5f;
				GameArt.Flat((num2 >= 0f) ? new Rect(num, bar.y, num2, bar.height) : new Rect(num + num2, bar.y, 0f - num2, bar.height), color);
				GameArt.Flat(new Rect(num - 0.5f, bar.y - 1f, 1f, bar.height + 2f), new Color(1f, 1f, 1f, 0.5f));
				return;
			}
			fill = Mathf.Clamp01(fill);
			if (reach > fill)
			{
				GameArt.Flat(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(reach), bar.height), ReachPink);
			}
			GameArt.Flat(new Rect(bar.x, bar.y, bar.width * fill, bar.height), color);
			if (reach > fill && reach < 1f)
			{
				GameArt.Flat(new Rect(bar.x + bar.width * reach - 0.5f, bar.y - 1f, 1f, bar.height + 2f), Color.white);
			}
		}

		internal void DrawRun(WorldBoxMod mod)
		{
			EnsureAssets();
			GameArt.Ensure();
			if (Run.LegacyOpen && Run.LastLegacy != null)
			{
				DrawLegacy();
			}
			else if (Run.Asking != Run.Ask.None)
			{
				DrawRunAsk(mod);
			}
			else if (Run.LockInOpen)
			{
				DrawLockIn(mod);
			}
			else if (Run.Locked && Event.current.type == EventType.Repaint)
			{
				DrawRunBadge();
			}
		}

		private void DrawRunBadge()
		{
			float p = GameArt.P;
			Run.Record current = Run.Current;
			if (current != null)
			{
				bool flag = false;
				try
				{
					flag = Run.Body != null && Run.Body.isKing();
				}
				catch
				{
				}
				string text = "Locked in · gen " + current.Generation + (flag ? " · king" : "");
				float num = Mathf.Round(GameArt.Measure(text, 4.4f).x + 14f * p);
				float num2 = Mathf.Round(((float)Screen.width - 150f * p) * 0.5f);
				Rect r = new Rect(Mathf.Round(num2 - num - 4f * p), Mathf.Round(9f * p), num, Mathf.Round(13f * p));
				GameArt.Sliced(r, GameArt.Inner, new Color(1f, 1f, 1f, 0.7f));
				GameArt.Label(r, text, 4.4f, TextAnchor.MiddleCenter, flag ? GameArt.Orange : Color.white);
				string text2 = GreatWar.Card(Run.Body);
				if (text2 != null)
				{
					float num3 = Mathf.Round(GameArt.Measure(text2, 4.4f).x + 14f * p);
					Rect r2 = new Rect(Mathf.Round(r.xMax - num3), Mathf.Round(r.yMax + 2f * p), num3, Mathf.Round(13f * p));
					GameArt.Sliced(r2, GameArt.Inner, new Color(1f, 0.85f, 0.8f, 0.75f));
					GameArt.Label(r2, text2, 4.4f, TextAnchor.MiddleCenter, GameArt.Orange);
				}
			}
		}

		private void DrawLockIn(WorldBoxMod mod)
		{
			float k = GameArt.P;
			Actor host = mod.Host;
			if (host == null || !host.isAlive())
			{
				Run.LockInOpen = false;
				return;
			}
			Rect win = WindowRect(440f, 300f);
			bool close;
			Rect rect = GameWindow(win, "Lock in this life", "Esc", out close);
			if (close)
			{
				Run.LockInOpen = false;
				return;
			}
			float num = Mathf.Round(70f * k);
			float num2 = Mathf.Round(14f * k);
			if (GameButton(new Rect(rect.x, rect.y, num, num2), "Loadout", Run.Tab == 0))
			{
				Run.Tab = 0;
			}
			if (GameButton(new Rect(rect.x + num + 4f * k, rect.y, num, num2), "Hall of Lives", Run.Tab == 1))
			{
				Run.Tab = 1;
			}
			Rect body = new Rect(rect.x, rect.y + num2 + 5f * k, rect.width, rect.height - num2 - 5f * k);
			if (Run.Tab == 1)
			{
				DrawHall(body);
				return;
			}
			int num3 = Run.Budget();
			int num4 = Run.PointsLeft(Run.Picked, num3);
			float num5 = Mathf.Round(body.width * 0.32f);
			Rect who = new Rect(body.x, body.y, num5, body.height - 62f * k);
			GameArt.Sliced(who, GameArt.Inner, new Color(1f, 1f, 1f, 0.5f));
			float y = who.y + 4f * k;
			string text = "";
			string text2 = "";
			int num6 = 0;
			try
			{
				text = host.asset.id.Replace('_', ' ');
				text2 = host.getAge() + " years";
				num6 = host.money;
			}
			catch
			{
			}
			Line(Run.SafeName(host), 6.2f, GameArt.Orange, 13f);
			Line(text + ", " + text2, 4.4f, Color.white, 10f);
			Line(num6 + " gold, gear and traits kept", 4.2f, GameArt.Grey, 10f);
			y += 4f * k;
			Line($"Points: {num4} of {num3}", 6f, (num4 < 0) ? new Color(1f, 0.4f, 0.35f) : Color.white, 13f);
			int num7 = Run.ToNextPoint();
			Line($"{Run.Credits} credits from past lives" + ((num7 > 0) ? $", {num7} more for a point" : ""), 4f, GameArt.Grey, 16f);
			Line("Good traits cost points; bad ones give points back. Unspent points are lost.", 3.9f, GameArt.Grey, 22f);
			if (_runHover != null)
			{
				Line(_runHover, 4.4f, Color.white, 20f);
			}
			Rect r = new Rect(body.x + num5 + 6f * k, body.y, body.width - num5 - 6f * k, body.height - 62f * k);
			GameArt.Sliced(r, GameArt.Inner, new Color(1f, 1f, 1f, 0.5f));
			DrawLoadoutShelf(new Rect(r.x + 4f * k, r.y + 4f * k, r.width - 14f * k, r.height - 8f * k), host, num4);
			GameArt.Label(new Rect(body.x, body.yMax - 58f * k, body.width, 34f * k), "In a locked-in life: no free gear or traits (traders and teachers instead). V still looks down from above you. Leaping into another body, or letting go of this one, ends the run. When you die, an heir with a home carries it on; with none, the run ends and is scored. The goal: become king, win a great war, and live forever.", 4f, TextAnchor.MiddleLeft, GameArt.Grey, wrap: true);
			float num8 = Mathf.Round(110f * k);
			float num9 = Mathf.Round(18f * k);
			Rect r2 = new Rect(Mathf.Round(body.center.x - num8 - 4f * k), Mathf.Round(body.yMax - num9), num8, num9);
			Rect r3 = new Rect(Mathf.Round(body.center.x + 4f * k), Mathf.Round(body.yMax - num9), num8, num9);
			if (GameButton(r2, "Lock in this life", red: true, num4 >= 0) && Run.Lock(host, Run.Picked.ToList(), mod.Toast))
			{
				Run.LockInOpen = false;
			}
			if (GameButton(r3, "Not now", red: false))
			{
				Run.LockInOpen = false;
			}
			void Line(string s, float size, Color c, float h)
			{
				GameArt.Label(new Rect(who.x + 6f * k, y, who.width - 12f * k, h * k), s, size, TextAnchor.UpperLeft, c, wrap: true);
				y += h * k;
			}
		}

		private void DrawLoadoutShelf(Rect view, Actor host, int left)
		{
			float p = GameArt.P;
			if (_loadoutOrder == null)
			{
				_loadoutOrder = (from id in Run.Costs.Keys
					where AssetManager.traits.get(id) != null
					orderby (Run.Costs[id] >= 0) ? Run.Costs[id] : (100 - Run.Costs[id]), id
					select id).ToList();
			}
			float num = Mathf.Round(24f * p);
			float num2 = 3f * p;
			int num3 = Mathf.Max(1, Mathf.FloorToInt((view.width + num2) / (num + num2)));
			float contentH = (float)Mathf.CeilToInt((float)_loadoutOrder.Count / (float)num3) * (num + num2 + 7f * p);
			float num4 = BeginList(view, ref _loadoutScroll, contentH);
			bool flag = Event.current.type == EventType.Repaint;
			if (flag)
			{
				_runHover = null;
			}
			for (int num5 = 0; num5 < _loadoutOrder.Count; num5++)
			{
				string text = _loadoutOrder[num5];
				ActorTrait actorTrait = AssetManager.traits.get(text);
				int num6 = Run.Costs[text];
				Rect rect = new Rect((float)(num5 % num3) * (num + num2), num4 + (float)(num5 / num3) * (num + num2 + 7f * p), num, num);
				bool flag2 = false;
				try
				{
					flag2 = host.hasTrait(text);
				}
				catch
				{
				}
				bool flag3 = Run.Picked.Contains(text);
				bool flag4 = !flag3 && Run.Clashes(host, text, Run.Picked);
				bool flag5 = !flag2 && !flag4 && (flag3 || num6 <= left);
				if (flag)
				{
					GameArt.Sliced(rect, GameArt.Inner, flag3 ? new Color(1f, 0.85f, 0.35f, 1f) : new Color(1f, 1f, 1f, flag2 ? 0.25f : 0.6f));
					Sprite sprite = null;
					try
					{
						sprite = actorTrait.getSprite();
					}
					catch
					{
					}
					if (sprite != null)
					{
						GameArt.Icon(new Rect(rect.x + 3f * p, rect.y + 3f * p, rect.width - 6f * p, rect.height - 6f * p), sprite, (flag2 || !flag5) ? new Color(1f, 1f, 1f, 0.35f) : Color.white);
					}
					string text2 = (flag2 ? "yours" : ((num6 > 0) ? num6.ToString() : ("+" + -num6)));
					GameArt.Label(new Rect(rect.x - 4f * p, rect.yMax, rect.width + 8f * p, 7f * p), text2, 3.6f, TextAnchor.UpperCenter, flag2 ? GameArt.Grey : ((num6 < 0) ? new Color(0.55f, 0.9f, 0.55f) : (flag3 ? GameArt.Orange : Color.white)));
				}
				if (_listMouseIn && rect.Contains(Event.current.mousePosition))
				{
					_runHover = TraitName(actorTrait) + (flag2 ? " (yours already)" : (flag4 ? ": the opposite of one you have or picked" : ((num6 > 0) ? string.Format(": {0} point{1}", num6, (num6 > 1) ? "s" : "") : $": gives {-num6} back")));
				}
				PadNav.Spot(rect);
				if (_listMouseIn && !flag2 && GUI.Button(rect, GUIContent.none, GUIStyle.none))
				{
					if (flag3)
					{
						Run.Picked.Remove(text);
					}
					else if (flag5)
					{
						Run.Picked.Add(text);
					}
					Sounds.PlayUI("event:/SFX/Ui/Click");
				}
			}
			EndList(view, ref _loadoutScroll, contentH);
		}

		private void DrawHall(Rect body)
		{
			float p = GameArt.P;
			IReadOnlyList<Run.Legacy> hall = Run.Hall;
			GameArt.Label(new Rect(body.x + 4f * p, body.y, body.width - 8f * p, 12f * p), $"{Run.Credits} credits · next life's loadout: {Run.Budget()} points", 4.8f, TextAnchor.MiddleLeft, GameArt.Orange);
			Rect view = new Rect(body.x, body.y + 14f * p, body.width - 10f * p, body.height - 14f * p);
			float num = Mathf.Round(22f * p);
			float contentH = (float)Mathf.Max(1, hall.Count) * num;
			float num2 = BeginList(view, ref _hallScroll, contentH);
			if (hall.Count == 0)
			{
				GameArt.Label(new Rect(0f, num2, view.width, num * 2f), "No lives yet. Lock into one (Esc > Start a run) and see how far it goes.", 4.6f, TextAnchor.MiddleCenter, GameArt.Grey, wrap: true);
			}
			for (int i = 0; i < hall.Count; i++)
			{
				Run.Legacy legacy = hall[i];
				Rect r = new Rect(0f, num2 + (float)i * num, view.width, num - 2f * p);
				if (Event.current.type == EventType.Repaint)
				{
					GameArt.Sliced(r, GameArt.Inner, new Color(1f, 1f, 1f, 0.45f));
				}
				GameArt.Label(new Rect(r.x + 5f * p, r.y + 1f * p, r.width * 0.62f, 10f * p), legacy.Name + ", " + legacy.Species + (legacy.Abandoned ? " (given up)" : ""), 4.6f, TextAnchor.MiddleLeft, legacy.Abandoned ? GameArt.Grey : Color.white);
				GameArt.Label(new Rect(r.x + 5f * p, r.y + 10f * p, r.width * 0.62f, 9f * p), string.Format("{0} years, {1} generation{2}. {3}", legacy.Years, legacy.Generations, (legacy.Generations > 1) ? "s" : "", legacy.Epitaph), 3.8f, TextAnchor.MiddleLeft, GameArt.Grey);
				GameArt.Label(new Rect(r.x + r.width * 0.62f, r.y, r.width * 0.38f - 6f * p, r.height), $"{legacy.Total} points · {legacy.Credits} credits", 4.4f, TextAnchor.MiddleRight, GameArt.Orange);
			}
			EndList(view, ref _hallScroll, contentH);
		}

		private void DrawRunAsk(WorldBoxMod mod)
		{
			float p = GameArt.P;
			Actor body = Run.Body;
			string text = ((body != null) ? Run.SafeName(body) : "this person");
			bool flag = Run.Asking == Run.Ask.LeftBody;
			Rect win = WindowRect(300f, 150f);
			bool close;
			Rect rect = GameWindow(win, flag ? ("You let go of " + text) : "Give up this run?", "Esc", out close);
			if (close)
			{
				if (flag)
				{
					mod.RunLater();
				}
				else
				{
					mod.RunAnswer(abandon: false);
				}
				return;
			}
			string text2 = (flag ? ("Your locked-in life goes on without you. Go back into " + text + ", or give the run up: it ends now and goes to the Hall of Lives as given up, for half the credits. Esc: later, to save or look round.") : (text + "'s run ends now. It goes to the Hall of Lives as given up, for half the credits." + ((Run.LeapTo != null) ? (" You leap into " + Run.SafeName(Run.LeapTo) + ".") : "") + " The free Character shelves open again: back to the sandbox."));
			GameArt.Label(new Rect(rect.x + 6f * p, rect.y, rect.width - 12f * p, rect.height - 26f * p), text2, 4.8f, TextAnchor.MiddleCenter, Color.white, wrap: true);
			float num = Mathf.Round(112f * p);
			float num2 = Mathf.Round(18f * p);
			Rect r = new Rect(Mathf.Round(rect.center.x - num - 4f * p), Mathf.Round(rect.yMax - num2), num, num2);
			Rect r2 = new Rect(Mathf.Round(rect.center.x + 4f * p), Mathf.Round(rect.yMax - num2), num, num2);
			if (GameButton(r, flag ? "Go back" : "Keep living", red: true))
			{
				mod.RunAnswer(abandon: false);
			}
			if (GameButton(r2, "Give up the run", red: false))
			{
				mod.RunAnswer(abandon: true);
			}
		}

		private void DrawLegacy()
		{
			float k = GameArt.P;
			Run.Legacy lastLegacy = Run.LastLegacy;
			Rect win = WindowRect(320f, 230f);
			bool close;
			Rect inner = GameWindow(win, lastLegacy.Name + "'s life", "Esc", out close);
			if (close)
			{
				Run.LegacyOpen = false;
				return;
			}
			float y = inner.y;
			Line(string.Format("{0}{1} years, {2} generation{3}. {4}.", string.IsNullOrEmpty(lastLegacy.Species) ? "" : (lastLegacy.Species + ", "), lastLegacy.Years, lastLegacy.Generations, (lastLegacy.Generations > 1) ? "s" : "", Capital(lastLegacy.How)), 4.8f, Color.white, 12f);
			Line(lastLegacy.Epitaph, 5f, GameArt.Orange, 14f);
			y += 3f * k;
			Row("Years and generations", lastLegacy.Survival);
			Row("Wealth and property", lastLegacy.Wealth);
			Row("Renown and deeds", lastLegacy.Deeds);
			Row("War and conquest", lastLegacy.War);
			Row("Score", lastLegacy.Total);
			y += 4f * k;
			Line(string.Format("{0} credits{1}. Next life's loadout: {2} points", lastLegacy.Credits, lastLegacy.Abandoned ? " (half: given up)" : "", lastLegacy.BudgetAfter) + ((lastLegacy.BudgetAfter > lastLegacy.BudgetBefore) ? $" (was {lastLegacy.BudgetBefore})." : "."), 4.6f, Color.white, 12f);
			float num = Mathf.Round(120f * k);
			float num2 = Mathf.Round(18f * k);
			if (GameButton(new Rect(Mathf.Round(inner.center.x - num * 0.5f), Mathf.Round(inner.yMax - num2), num, num2), "Back to the world", red: true))
			{
				Run.LegacyOpen = false;
			}
			void Line(string s, float size, Color c, float h, TextAnchor anchor = TextAnchor.MiddleCenter)
			{
				GameArt.Label(new Rect(inner.x + 6f * k, y, inner.width - 12f * k, h * k), s, size, anchor, c, wrap: true);
				y += h * k;
			}
			void Row(string what, int n)
			{
				GameArt.Label(new Rect(inner.x + 30f * k, y, inner.width * 0.6f, 10f * k), what, 4.6f, TextAnchor.MiddleLeft, GameArt.Grey);
				GameArt.Label(new Rect(inner.x, y, inner.width - 30f * k, 10f * k), n.ToString(), 4.8f, TextAnchor.MiddleRight, Color.white);
				y += 10f * k;
			}
		}

		private static string Capital(string s)
		{
			if (!string.IsNullOrEmpty(s))
			{
				return char.ToUpperInvariant(s[0]) + s.Substring(1);
			}
			return "";
		}

		internal void SceneReset()
		{
			_scButton = -1;
			_scDragged = false;
			_scShift = (_areaOn = false);
			_scFlag = null;
			_scPips.Clear();
			SceneHover = null;
			_mod.Scene.LetGo();
		}

		internal void SceneMouseLost()
		{
			_scButton = -1;
			_scDragged = false;
			_scShift = (_areaOn = false);
			_scFlag = null;
			_mod.Scene.LetGo();
		}

		internal bool ScenePipForTest(Actor a, out Vector2 pip, out Vector2 body)
		{
			foreach (ScenePip scPip in _scPips)
			{
				if (scPip.Actor == a)
				{
					pip = scPip.S;
					body = scPip.Body;
					return true;
				}
			}
			pip = (body = default(Vector2));
			return false;
		}

		private void SceneButton(Rect mapRect, Func<Vector2, Vector2> toScreen, Vector2 centre, MapTabs.Page page)
		{
			float p = GameArt.P;
			Rect r = (SceneButtonRectForTest = new Rect(Mathf.Round(mapRect.x + 4f * p), Mathf.Round(mapRect.y + 4f * p), Mathf.Round(GameArt.Measure("View scene", 4.8f).x + 26f * p), Mathf.Round(16f * p)));
			if (r.Contains(Event.current.mousePosition) && Event.current.type == EventType.Repaint)
			{
				SceneFootprint(mapRect, toScreen, centre);
				string text = ((page == MapTabs.Page.War) ? "The framed ground in 3D. Orders work there too" : (_mod.SoldierMap.Captaincy ? "The framed ground in 3D. Your soldiers take orders there too" : "The framed ground in 3D"));
				float width = GameArt.Measure(text, 4.4f).x + 10f * p;
				Rect r2 = new Rect(r.x, r.yMax + 3f * p, width, 12f * p);
				GameArt.TipPanel(r2);
				GameArt.Label(r2, text, 4.4f, TextAnchor.MiddleCenter, Color.white);
			}
			if (GameButton(r, "View scene", red: true, enabled: true, SceneIcon, 4.8f))
			{
				_mod.EnterScene(page, centre);
			}
		}

		private void SceneFootprint(Rect mapRect, Func<Vector2, Vector2> toScreen, Vector2 centre)
		{
			SceneView scene = _mod.Scene;
			int num = ((_mod.Renderer.Width > 0) ? _mod.Renderer.Width : 640);
			int num2 = ((_mod.Renderer.Height > 0) ? _mod.Renderer.Height : 360);
			float num3 = _mod.SceneGroundZ(centre);
			ViewState camera = SceneView.Camera(centre, num3, scene.Zoom, scene.Turn, scene.Tip, num, num2);
			Vector2[] array = new Vector2[4];
			float[] array2 = new float[4] { 0f, num, num, 0f };
			float[] array3 = new float[4] { 0f, 0f, num2, num2 };
			for (int i = 0; i < 4; i++)
			{
				Vec3 vec = GodCamera.Ray(camera, num, num2, array2[i], array3[i]);
				float num4 = ((vec.Z < -0.001f) ? Mathf.Min((num3 - camera.Z) / vec.Z, scene.Zoom * 12f) : (scene.Zoom * 12f));
				array[i] = toScreen(new Vector2(camera.X + vec.X * num4, camera.Y + vec.Y * num4)) - mapRect.position;
			}
			float p = GameArt.P;
			float thick = Mathf.Max(1.5f, Mathf.Round(p));
			GUI.BeginGroup(mapRect);
			for (int j = 0; j < 4; j++)
			{
				Line(array[j] + new Vector2(1f, 1f), array[(j + 1) % 4] + new Vector2(1f, 1f), thick, new Color(0f, 0f, 0f, 0.45f));
				Line(array[j], array[(j + 1) % 4], thick, new Color(1f, 0.85f, 0.3f, 0.95f));
			}
			PixelDiamond(toScreen(centre) - mapRect.position, 1, new Color(1f, 0.85f, 0.3f, 0.95f));
			GUI.EndGroup();
		}

		private bool SceneEye(Rect r, string tip)
		{
			SceneEyesForTest[tip] = r;
			if (r.Contains(Event.current.mousePosition) && Event.current.type == EventType.Repaint)
			{
				float p = GameArt.P;
				float num = GameArt.Measure(tip, 4.4f).x + 10f * p;
				Rect r2 = new Rect(r.x - num - 3f * p, r.y, num, 12f * p);
				GameArt.TipPanel(r2);
				GameArt.Label(r2, tip, 4.4f, TextAnchor.MiddleCenter, Color.white);
			}
			return GameButton(r, null, red: false, enabled: true, SceneIcon);
		}

		public void DrawScene()
		{
			EnsureAssets();
			GameArt.Ensure();
			Skin.Ensure();
			SceneView sc = _mod.Scene;
			Actor host = _mod.Host;
			if (host == null || !sc.On)
			{
				return;
			}
			Event current = Event.current;
			bool num = current.type == EventType.Repaint;
			float num2 = Screen.width;
			float num3 = Screen.height;
			float p = GameArt.P;
			bool flag = sc.Page == MapTabs.Page.War;
			if (num)
			{
				_lastHudFrame = Time.frameCount;
				DrawFrame(new Rect(0f, 0f, num2, num3), blend: false);
				SceneMarks(sc, host, flag, num2, num3, current.mousePosition);
			}
			float num4 = SceneBar(sc, host, flag, num2);
			if (flag)
			{
				WarMap warMap = _mod.WarMap;
				OrderKeys(warMap, host, current, delegate(Vector2 at)
				{
					SceneMouseLost();
					sc.JumpTo(at);
				});
				DrawOrderBar(warMap, host, num2 * 0.5f, num3 - Mathf.Round(28f * p), num2 - 16f * p, delegate(Vector2 at)
				{
					SceneMouseLost();
					sc.JumpTo(at);
				});
				if (_cardCity != null && _mod.SceneProject(SceneBannerPoint(warMap.TownPoint(_cardCity)), out var gui))
				{
					DrawTownCard(warMap, host, gui, new Rect(0f, num4, num2, num3 - num4 - 60f * p));
				}
				else
				{
					_townCard = default(Rect);
				}
			}
			else
			{
				_orderBar = (_townCard = default(Rect));
			}
			SceneInput(sc, host, flag, current);
			if (num)
			{
				SceneTips(flag, current.mousePosition, num2, num3, num4);
				CaptureSceneIfAsked(sc, host, flag, num2, num3, current.mousePosition);
			}
		}

		private void SceneTips(bool war, Vector2 mouse, float sw, float sh, float below)
		{
			float p = GameArt.P;
			if (_scButton < 0 && !_scBar.Contains(mouse) && !OverOrderUi(mouse))
			{
				if (_scHoverFlag != null)
				{
					SceneFlagTip(_mod.WarMap, _scHoverFlag, mouse);
				}
				else if (SceneHover != null)
				{
					SceneUnitTip(SceneHover, !war && SceneHover == _mod.SoldierMap.Captain, mouse);
				}
			}
			string prompt = (war ? "LMB:Select|RMB:Go or attack|Shift+RMB:Flag|Ctrl+1-6:Group|WASD:Move|Wheel:Zoom|Arrows:Turn|F:Follow" : (_mod.SoldierMap.Captaincy ? "RMB:Send your soldiers|WASD:Move|Wheel:Zoom|Arrows:Turn|F:Follow" : "WASD:Move|Wheel:Zoom|Arrows:Turn|F:Follow"));
			SceneKeys(sw, sh - Mathf.Round(24f * p), prompt);
			if (_mod.ToastText != null && Time.unscaledTime < _mod.ToastUntil)
			{
				Pill(sw, below + 6f * p, _mod.ToastText, Color.white, Mathf.Clamp01((_mod.ToastUntil - Time.unscaledTime) / 0.4f));
			}
		}

		private void CaptureSceneIfAsked(SceneView sc, Actor host, bool war, float sw, float sh, Vector2 mouse)
		{
			if (_captureFile == null || _capturing)
			{
				return;
			}
			string captureFile = _captureFile;
			_captureFile = null;
			_capturing = true;
			int num = Mathf.Max(16, (int)sw);
			int num2 = Mathf.Max(16, (int)sh);
			RenderTexture temporary = RenderTexture.GetTemporary(num, num2, 24, RenderTextureFormat.ARGB32);
			RenderTexture active = RenderTexture.active;
			try
			{
				RenderTexture.active = temporary;
				GL.Clear(clearDepth: true, clearColor: true, Color.black);
				DrawFrame(new Rect(0f, 0f, sw, sh), blend: false);
				SceneMarks(sc, host, war, sw, sh, mouse);
				float num3 = SceneBar(sc, host, war, sw);
				if (war)
				{
					float p = GameArt.P;
					WarMap warMap = _mod.WarMap;
					DrawOrderBar(warMap, host, sw * 0.5f, sh - Mathf.Round(28f * p), sw - 16f * p, null);
					if (_cardCity != null && _mod.SceneProject(SceneBannerPoint(warMap.TownPoint(_cardCity)), out var gui))
					{
						DrawTownCard(warMap, host, gui, new Rect(0f, num3, sw, sh - num3 - 60f * p));
					}
				}
				SceneTips(war, mouse, sw, sh, num3);
				Texture2D texture2D = new Texture2D(num, num2, TextureFormat.RGB24, mipChain: false);
				texture2D.ReadPixels(new Rect(0f, 0f, num, num2), 0, 0);
				texture2D.Apply(updateMipmaps: false);
				File.WriteAllBytes(captureFile, texture2D.EncodeToPNG());
				UnityEngine.Object.Destroy(texture2D);
				Log.Info("hud captured " + Path.GetFileName(captureFile));
			}
			catch (Exception ex)
			{
				Log.Error("scene capture: " + ex.Message);
			}
			finally
			{
				RenderTexture.active = active;
				RenderTexture.ReleaseTemporary(temporary);
				_capturing = false;
			}
		}

		private void SceneMarks(SceneView sc, Actor host, bool war, float sw, float sh, Vector2 mouse)
		{
			float p = GameArt.P;
			float unscaledTime = Time.unscaledTime;
			float num = 0.5f + 0.5f * Mathf.Sin(unscaledTime * 5f);
			_scPips.Clear();
			SceneHover = null;
			_scHoverFlag = null;
			if (!_mod.SceneShownSet)
			{
				return;
			}
			WarMap warMap = _mod.WarMap;
			SoldierMap soldierMap = _mod.SoldierMap;
			if (war)
			{
				foreach (WarMap.Town town in warMap.Towns)
				{
					SceneTown(town.P, town.Owner, town.Name, town.Capital, (town.Side == WarMap.Side.Neutral) ? 0.7f : 1f, sw, sh);
				}
			}
			else
			{
				foreach (SoldierMap.Town town2 in soldierMap.Towns)
				{
					SceneTown(town2.P, town2.Owner, town2.Name + (town2.Home ? " (home)" : ""), town2.Capital, (town2.Ours || town2.Target) ? 1f : 0.75f, sw, sh);
				}
			}
			List<Vector2> list = (war ? warMap.Clashes : soldierMap.Clashes);
			if (war)
			{
				foreach (Vector2 otherClash in warMap.OtherClashes)
				{
					if (_mod.SceneProject(new Vector3(otherClash.x, otherClash.y, _mod.SceneGroundZ(otherClash) + 2.5f), out var gui) && In(gui, 20f * p))
					{
						GameArt.Icon(new Rect(Mathf.Round(gui.x - 4.5f * p), Mathf.Round(gui.y - 4.5f * p), 9f * p, 9f * p), GameArt.Warfare, new Color(1f, 1f, 1f, 0.6f));
					}
				}
			}
			foreach (Vector2 item in list)
			{
				if (_mod.SceneProject(new Vector3(item.x, item.y, _mod.SceneGroundZ(item) + 2.5f), out var gui2) && In(gui2, 20f * p))
				{
					float num2 = Mathf.Round(13f * p * (1f + 0.18f * num));
					GameArt.Icon(new Rect(Mathf.Round(gui2.x - num2 * 0.5f + p), Mathf.Round(gui2.y - num2 * 0.5f + p), num2, num2), GameArt.Warfare, new Color(0f, 0f, 0f, 0.5f));
					GameArt.Icon(new Rect(Mathf.Round(gui2.x - num2 * 0.5f), Mathf.Round(gui2.y - num2 * 0.5f), num2, num2), GameArt.Warfare);
				}
			}
			bool flag = war && warMap.FlagsKingdom != null && warMap.FlagsKingdom == host.kingdom;
			if (flag)
			{
				foreach (WarFlag flag6 in warMap.Flags)
				{
					if (flag6.Coming > 0 && SceneFoot(flag6.ComingFrom, out var s) && SceneFoot(flag6.P, out var s2))
					{
						MarchLine(s, s2, flag6.Color);
					}
				}
				WarFlag warFlag = ((warMap.SelectedFlag != null && warMap.Flags.Contains(warMap.SelectedFlag)) ? warMap.SelectedFlag : null);
				if (warFlag != null && (!_areaOn || _areaFlag != warFlag))
				{
					SceneRing(warFlag.P, warFlag.Radius, OrderTint(warFlag.Order), 0.8f);
				}
				foreach (WarFlag post in warMap.Posts)
				{
					if (SceneFoot(post.P, out var s3) && In(s3, 20f * p))
					{
						if (post.Coming > 0 && SceneFoot(post.ComingFrom, out var s4))
						{
							MarchLine(s4, s3, OrderTint(post.Order));
						}
						if (post.Target != null)
						{
							SceneRing(post.P, 1.2f, OrderTint(post.Order), 0.9f);
						}
						PixelDiamond(s3, 2, OrderTint(post.Order));
					}
				}
				if (_scButton < 0 && !_scBar.Contains(mouse) && !_scKeys.Contains(mouse) && !OverOrderUi(mouse))
				{
					_scHoverFlag = FlagUnder(warMap, SceneFootOrAway, mouse);
				}
				foreach (WarFlag flag7 in warMap.Flags)
				{
					if (SceneFoot(flag7.P, out var s5) && In(s5, 30f * p))
					{
						DrawFlagMarker(s5, flag7, warMap.Home, 1.15f, 1f, flag7 == _scHoverFlag);
						FlagOrderBadge(new Vector2(s5.x - 10f * p, s5.y - 23f * p), flag7, 8f);
					}
				}
			}
			if (war && _areaOn && _scButton == 1)
			{
				Color color = ((_areaFlag != null) ? OrderTint(_areaFlag.Order) : OrderTint(warMap.CurrentOrder));
				SceneRing(_areaFrom, _areaRadius, color, 0.95f);
				if (_areaMoved && SceneFoot(_areaFrom, out var s6))
				{
					GameArt.Label(new Rect(s6.x - 30f * p, s6.y - 6f * p, 60f * p, 9f * p), Mathf.RoundToInt(_areaRadius) + " tiles", 4.6f, TextAnchor.MiddleCenter, color);
				}
			}
			if (!war)
			{
				SceneOrders(soldierMap, host, unscaledTime);
			}
			Rect r = default(Rect);
			bool flag2 = war && _scButton == 0 && _scDragged && _scFlag == null;
			if (flag2)
			{
				r = Rect.MinMaxRect(Mathf.Min(_scFrom.x, mouse.x), Mathf.Min(_scFrom.y, mouse.y), Mathf.Max(_scFrom.x, mouse.x), Mathf.Max(_scFrom.y, mouse.y));
			}
			float num3 = Mathf.Round(4f * p);
			if (war)
			{
				WarMap.Side[] array = new WarMap.Side[4]
				{
					WarMap.Side.Neutral,
					WarMap.Side.Enemy,
					WarMap.Side.Ally,
					WarMap.Side.Own
				};
				for (int i = 0; i < array.Length; i++)
				{
					foreach (WarMap.Unit unit in warMap.Units)
					{
						if (unit.Side != array[i] || !ScenePoints(unit.Actor, out var pip, out var body) || !In(pip, 10f * p))
						{
							continue;
						}
						bool flag3 = unit.Ours && (unit.Warrior || unit.Ship);
						bool flag4 = warMap.Marked.Contains(unit.Actor) || (flag2 && flag3 && (r.Contains(body) || r.Contains(pip)));
						_scPips.Add(new ScenePip
						{
							Actor = unit.Actor,
							S = pip,
							Body = body,
							Orderable = flag3
						});
						if (unit.Ship)
						{
							DrawShipGlyph(pip, unit.Color, flag4, unit.Fighting, num, 0.9f);
							continue;
						}
						bool flag5 = unit.Side == WarMap.Side.Neutral && !flag4 && unit.Actor.kingdom != warMap.Picked;
						Color edge = ((unit.Fighting && !flag5) ? new Color(0.95f, 0.15f, 0.1f, 0.6f + 0.4f * num) : ((unit.Side == WarMap.Side.Ally) ? new Color(0.1f, 0.42f, 0.12f, 0.95f) : ((unit.Side == WarMap.Side.Enemy) ? new Color(0.42f, 0.05f, 0.04f, 0.95f) : new Color(0.05f, 0.04f, 0.04f, flag5 ? 0.5f : 0.95f))));
						ScenePipAt(pip, flag5 ? Mathf.Max(2f, Mathf.Round(num3 * 0.75f)) : num3, unit.Color, edge, flag4, unit.King, flag5);
						if (unit.Ours && flag && !unit.King)
						{
							WarFlag warFlag2 = warMap.FlagOf(unit.Actor);
							if (warFlag2 != null)
							{
								BandPip(Mathf.Round(pip.x), Mathf.Round(pip.y), num3, warFlag2.Color);
							}
						}
					}
				}
			}
			else
			{
				for (int j = 0; j < 3; j++)
				{
					foreach (SoldierMap.Dot dot in soldierMap.Dots)
					{
						if (((dot.Captain || dot.Army) ? 2 : (dot.Ours ? 1 : 0)) == j && ScenePoints(dot.Actor, out var pip2, out var body2) && In(pip2, 10f * p))
						{
							_scPips.Add(new ScenePip
							{
								Actor = dot.Actor,
								S = pip2,
								Body = body2
							});
							Color edge2 = (dot.Fighting ? new Color(0.95f, 0.15f, 0.1f, 0.6f + 0.4f * num) : ((!dot.Ours) ? new Color(0.42f, 0.05f, 0.04f, 0.95f) : new Color(0.05f, 0.04f, 0.04f, 0.95f)));
							ScenePipAt(pip2, num3, dot.Color, edge2, dot.Captain, dot.King, faint: false);
							if (dot.Captain)
							{
								GameArt.Label(new Rect(pip2.x - 40f * p, pip2.y - num3 - 11f * p, 80f * p, 8f * p), "Captain", 4.2f, TextAnchor.LowerCenter, ArmyGold);
							}
						}
					}
				}
			}
			if (flag2)
			{
				GameArt.Flat(r, new Color(1f, 0.85f, 0.3f, 0.12f));
				Outline(r, Mathf.Max(1f, Mathf.Round(p * 0.8f)), new Color(1f, 0.85f, 0.3f, 0.95f));
			}
			if (ScenePoints(host, out var pip3, out var _) && In(pip3, 10f * p))
			{
				PixelDiamond(pip3, 2, Color.white);
				GameArt.Label(new Rect(pip3.x - 30f * p, pip3.y - 14f * p, 60f * p, 8f * p), "You", 4.4f, TextAnchor.LowerCenter, Color.white);
			}
			if (_scButton < 0 && _scHoverFlag == null && !_scBar.Contains(mouse) && !_scKeys.Contains(mouse) && !OverOrderUi(mouse))
			{
				SceneHover = PipUnder(mouse, orderableOnly: false);
			}
			bool In(Vector2 vector, float m)
			{
				if (vector.x > 0f - m && vector.y > 0f - m && vector.x < sw + m)
				{
					return vector.y < sh + m;
				}
				return false;
			}
		}

		private void SceneOrders(SoldierMap sm, Actor host, float now)
		{
			float p = GameArt.P;
			if (sm.Captaincy && Service.Command == Service.CommandKind.March && Service.CommandTile != null)
			{
				Vector2 p2 = new Vector2((float)Service.CommandTile.x + 0.5f, (float)Service.CommandTile.y + 0.5f);
				if (SceneFoot(p2, out var s))
				{
					if (Service.ArmyMiddle(host, out var at, out var _) && SceneFoot(at, out var s2))
					{
						Dashed(s2, s, Mathf.Max(2f, Mathf.Round(1.2f * p)), ArmyGold, 6f * p, now * 10f * p);
					}
					float num = Mathf.Round(20f * p);
					GameArt.Flat(new Rect(Mathf.Round(s.x), Mathf.Round(s.y - num), Mathf.Max(1f, Mathf.Round(p)), num), PoleBrown);
					if (!DrawBanner(new Rect(Mathf.Round(s.x + p), Mathf.Round(s.y - num), 9f * p, 11f * p), host.kingdom, civ: true, 1f))
					{
						GameArt.Flat(new Rect(s.x + p, s.y - num, 9f * p, 7f * p), ArmyGold);
					}
					PixelDiamond(s, 1, ArmyGold);
				}
			}
			if (sm.Goal(out var p3) && SceneFoot(p3, out var s3))
			{
				float num2 = 0.5f + 0.5f * Mathf.Sin(now * 4f);
				float num3 = Mathf.Round((9f + 3f * num2) * p);
				GUI.color = new Color(OrderRed.r, OrderRed.g, OrderRed.b, 0.35f + 0.25f * num2);
				GUI.DrawTexture(new Rect(s3.x - num3, s3.y - num3 * 0.5f, num3 * 2f, num3), Skin.DiscTex);
				GUI.color = Color.white;
				PixelDiamond(s3, 2, OrderRed);
				GameArt.Label(new Rect(s3.x - 40f * p, s3.y + 4f * p, 80f * p, 8f * p), "Your orders", 4.4f, TextAnchor.UpperCenter, OrderRed);
			}
		}

		private Vector3 SceneBannerPoint(Vector2 p)
		{
			return new Vector3(p.x, p.y, _mod.SceneGroundZ(p) + 3f);
		}

		private int SceneTownUnder(WarMap map, Vector2 m)
		{
			float p = GameArt.P;
			float num = Mathf.Round(12f * p);
			float num2 = float.MaxValue;
			int result = -1;
			for (int i = 0; i < map.Towns.Count; i++)
			{
				if (_mod.SceneProject(SceneBannerPoint(map.Towns[i].P), out var gui) && Rect.MinMaxRect(gui.x - 30f * p, gui.y - num - 10f * p, gui.x + 30f * p, gui.y + 10f * p).Contains(m))
				{
					float sqrMagnitude = (gui - m).sqrMagnitude;
					if (sqrMagnitude < num2)
					{
						num2 = sqrMagnitude;
						result = i;
					}
				}
			}
			return result;
		}

		private void SceneTown(Vector2 p, Kingdom owner, string name, bool capital, float alpha, float sw, float sh)
		{
			if (!_mod.SceneProject(new Vector3(p.x, p.y, _mod.SceneGroundZ(p) + 3f), out var gui))
			{
				return;
			}
			float p2 = GameArt.P;
			float num = Mathf.Round(12f * p2);
			if (gui.x < -60f * p2 || gui.y < -30f * p2 || gui.x > sw + 60f * p2 || gui.y > sh + 30f * p2)
			{
				return;
			}
			Rect r = new Rect(Mathf.Round(gui.x - num * 0.4f), Mathf.Round(gui.y - num), Mathf.Round(num * 0.8f), num);
			bool civ = false;
			try
			{
				civ = owner?.isCiv() ?? false;
			}
			catch
			{
			}
			if (!DrawBanner(r, owner, civ, alpha))
			{
				GameArt.Flat(new Rect(gui.x - 3f * p2, gui.y - 6f * p2, 6f * p2, 6f * p2), (owner != null) ? Royal.ColorOf(owner) : Color.white);
			}
			if (capital)
			{
				GameArt.Icon(new Rect(Mathf.Round(gui.x - 5f * p2), Mathf.Round(r.y - 9f * p2), 10f * p2, 10f * p2), GameArt.Crown, new Color(1f, 1f, 1f, alpha));
			}
			Color color = Color.white;
			try
			{
				if (owner != null)
				{
					color = WarMap.Readable(owner.getColor().getColorText());
				}
			}
			catch
			{
			}
			color.a = alpha;
			GameArt.Label(new Rect(gui.x - 70f * p2, r.yMax + 0.5f * p2, 140f * p2, 9f * p2), name, 4.6f, TextAnchor.UpperCenter, color);
		}

		private static void ScenePipAt(Vector2 s, float d, Color fill, Color edge, bool marked, bool king, bool faint)
		{
			float p = GameArt.P;
			float num = Mathf.Round(s.x);
			float num2 = Mathf.Round(s.y);
			if (marked)
			{
				float num3 = d + 4f * p;
				GameArt.Flat(new Rect(num - num3 * 0.5f, num2 - num3 * 0.5f, num3, num3), new Color(1f, 0.85f, 0.25f, 0.95f));
			}
			if (faint)
			{
				fill.a = 0.6f;
			}
			GameArt.Flat(new Rect(num - d * 0.5f - p, num2 - d * 0.5f - p, d + 2f * p, d + 2f * p), edge);
			GameArt.Flat(new Rect(num - d * 0.5f, num2 - d * 0.5f, d, d), fill);
			if (king)
			{
				GameArt.Icon(new Rect(num - 5f * p, num2 - d * 0.5f - 9f * p, 10f * p, 10f * p), GameArt.Crown, new Color(1f, 1f, 1f, faint ? 0.7f : 1f));
			}
		}

		private bool ScenePoints(Actor a, out Vector2 pip, out Vector2 body)
		{
			pip = (body = default(Vector2));
			Vector2 current_position;
			float num;
			float z;
			try
			{
				if (a == null || !a.isAlive())
				{
					return false;
				}
				current_position = a.current_position;
				num = Mathf.Max(0f, _mod.SceneGroundZ(current_position) - _mod.GroundAt(current_position));
				z = _mod.GroundAt(current_position) + num + a.position_height;
			}
			catch
			{
				return false;
			}
			float p = GameArt.P;
			float num2 = 40f * p;
			if (!_mod.SceneProject(new Vector3(current_position.x, current_position.y, z), out var gui) || gui.x < 0f - num2 || gui.y < 0f - num2 || gui.x > (float)Screen.width + num2 || gui.y > (float)Screen.height + num2 * 3f)
			{
				return false;
			}
			Vector3 vector;
			try
			{
				vector = _mod.HeadOf(a);
			}
			catch
			{
				return false;
			}
			float num3 = 0f;
			try
			{
				num3 = a.position_height;
			}
			catch
			{
			}
			if (!_mod.SceneProject(new Vector3(vector.x, vector.y, vector.z + num + num3 + 0.3f), out pip))
			{
				return false;
			}
			body = (pip + gui) * 0.5f;
			return true;
		}

		private bool SceneFoot(Vector2 p, out Vector2 s)
		{
			return _mod.SceneProject(new Vector3(p.x, p.y, _mod.SceneGroundZ(p) + 0.05f), out s);
		}

		private Vector2 SceneFootOrAway(Vector2 p)
		{
			if (!SceneFoot(p, out var s))
			{
				return new Vector2(-100000f, -100000f);
			}
			return s;
		}

		private Actor PipUnder(Vector2 mouse, bool orderableOnly)
		{
			float p = GameArt.P;
			float num = 10f * p * 10f * p;
			Actor result = null;
			foreach (ScenePip scPip in _scPips)
			{
				if (!orderableOnly || scPip.Orderable)
				{
					float num2 = Mathf.Min((scPip.S - mouse).sqrMagnitude, (scPip.Body - mouse).sqrMagnitude);
					if (num2 < num)
					{
						num = num2;
						result = scPip.Actor;
					}
				}
			}
			return result;
		}

		private float SceneBar(SceneView sc, Actor host, bool war, float sw)
		{
			float p = GameArt.P;
			float num = Mathf.Round(16f * p);
			float num2 = Mathf.Round(3f * p);
			float size = 4.6f;
			float num3 = Mathf.Round(6f * p);
			WarMap warMap = _mod.WarMap;
			SoldierMap soldierMap = _mod.SoldierMap;
			List<Vector2> list = ((!war) ? soldierMap.Clashes : ((warMap.Clashes.Count > 0) ? warMap.Clashes : warMap.OtherClashes));
			Vector2 p2;
			bool flag = !war && soldierMap.Goal(out p2);
			string text = SceneTitle(sc, war);
			string text2 = (war ? ("War Map" + ((warMap.Marked.Count > 0) ? ("  ·  " + warMap.Marked.Count + " selected") : "")) : ((soldierMap.Captaincy ? "Captain's Map" : "Soldier's Map") + ((soldierMap.Order != null) ? ("  ·  " + soldierMap.Order.Title) : "")));
			string[] array = new string[5]
			{
				"Back to map (Esc)",
				"Your eyes (M)",
				sc.Following ? "Following (F)" : "Follow (F)",
				"Next battle",
				"Your orders"
			};
			bool[] obj = new bool[5] { true, true, true, true, false };
			obj[4] = !war;
			bool[] array2 = obj;
			float num4 = Mathf.Max(GameArt.Measure(text, 5.6f).x, GameArt.Measure(text2, 4.2f).x) + 4f * p;
			float num5 = Mathf.Round(54f * p);
			float num6 = num3 * 2f + num4 + num5 + num2 * 2f;
			float[] array3 = new float[array.Length];
			for (int i = 0; i < array.Length; i++)
			{
				if (array2[i])
				{
					array3[i] = Mathf.Round(GameArt.Measure(array[i], size).x + 12f * p);
					num6 += array3[i] + num2;
				}
			}
			num6 = Mathf.Min(num6, sw - 8f * p);
			Rect r = (_scBar = new Rect(Mathf.Round((sw - num6) * 0.5f), Mathf.Round(5f * p), Mathf.Round(num6), Mathf.Round(26f * p)));
			bool flag2 = Event.current.type == EventType.Repaint;
			if (flag2)
			{
				GameArt.TipPanel(r);
				GameArt.Label(new Rect(r.x + num3, r.y + 2.5f * p, num4, 11f * p), text, 5.6f, TextAnchor.MiddleLeft, GameArt.Orange);
				GameArt.Label(new Rect(r.x + num3, r.y + 13f * p, num4, 9f * p), text2, 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
			}
			float num7 = r.x + num3 + num4 + num2;
			float y = Mathf.Round(r.center.y - num * 0.5f);
			for (int j = 0; j < array.Length; j++)
			{
				if (!array2[j])
				{
					continue;
				}
				Rect r2 = new Rect(num7, y, array3[j], num);
				num7 += array3[j] + num2;
				bool enabled = ((j != 3) ? (j != 4 || flag) : (list.Count > 0));
				if (!GameButton(r2, array[j], j == 2 && sc.Following, enabled, null, size))
				{
					continue;
				}
				switch (j)
				{
				case 0:
					_mod.LeaveScene(toMap: true);
					break;
				case 1:
					_mod.LeaveScene(toMap: false);
					break;
				case 2:
					_mod.SceneFollowKey();
					break;
				case 3:
				{
					Vector2? vector = _mod.NextSceneBattle(list, sc.At, fresh: false);
					if (vector.HasValue)
					{
						SceneMouseLost();
						sc.JumpTo(vector.Value);
					}
					break;
				}
				case 4:
				{
					if (soldierMap.Goal(out var p3))
					{
						SceneMouseLost();
						sc.JumpTo(p3);
					}
					break;
				}
				}
			}
			if (flag2)
			{
				float frac = 1f;
				try
				{
					frac = host.getHealthRatio();
				}
				catch
				{
				}
				Rect r3 = new Rect(Mathf.Round(num7 + 1f * p), Mathf.Round(r.center.y - 6f * p), 13f * p, 12f * p);
				GameArt.IconPart(r3, GameArt.Heart, GameArt.HeartPart, Color.white);
				float num8 = Mathf.Clamp01(1f - (Time.unscaledTime - _mod.SceneHurtAt) / 0.8f);
				GameArt.Bar(new Rect(Mathf.Round(r3.xMax + 2f * p), Mathf.Round(r.center.y - 3f * p), Mathf.Round(num5 - 18f * p), Mathf.Round(6f * p)), frac, Color.Lerp(GameArt.HealthGreen, new Color(0.9f, 0.15f, 0.1f), num8 * (0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 18f))));
			}
			return r.yMax;
		}

		private string SceneTitle(SceneView sc, bool war)
		{
			string text = null;
			float num = 2025f;
			if (war)
			{
				foreach (WarMap.Town town in _mod.WarMap.Towns)
				{
					float sqrMagnitude = (town.P - sc.At).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						text = town.Name;
					}
				}
			}
			else
			{
				foreach (SoldierMap.Town town2 in _mod.SoldierMap.Towns)
				{
					float sqrMagnitude2 = (town2.P - sc.At).sqrMagnitude;
					if (sqrMagnitude2 < num)
					{
						num = sqrMagnitude2;
						text = town2.Name;
					}
				}
			}
			if (text == null)
			{
				return "Open country";
			}
			if (!(num < 100f))
			{
				return "Near " + text;
			}
			return text;
		}

		private void SceneKeys(float sw, float top, string prompt)
		{
			EnsureVitals();
			float p = GameArt.P;
			float num = 5f;
			float num2 = 8f * p;
			float num3 = 6f * p;
			float num4 = 0f;
			string[] array = prompt.Split('|');
			float[] array2 = new float[array.Length];
			for (int i = 0; i < array.Length; i++)
			{
				SplitPrompt(array[i], out var key, out var label);
				float a = Mathf.Round((num + 5f) * p);
				float num5 = ((key.Length > 0) ? (Mathf.Max(a, GameArt.Measure(key, num).x + 4f * p) + 4f * p) : 0f);
				array2[i] = num5 + GameArt.Measure(label, num).x;
				num4 += array2[i] + ((i > 0) ? num2 : 0f);
			}
			float height = Mathf.Round((num + 10f) * p);
			Rect r = (_scKeys = new Rect(Mathf.Round((sw - num4) * 0.5f - num3), Mathf.Round(top), Mathf.Round(num4 + num3 * 2f), height));
			GameArt.TipPanel(r, 0.9f);
			float y = r.center.y;
			for (int j = 0; j < array.Length; j++)
			{
				SplitPrompt(array[j], out var key2, out var label2);
				float num6 = r.x + num3 + SumBefore(array2, j, num2);
				if (key2.Length > 0)
				{
					num6 += GameArt.KeyCap(num6, y, key2, num) + 4f * p;
				}
				GameArt.Label(new Rect(num6, r.y - 0.5f * p, array2[j], height), label2, num, TextAnchor.MiddleLeft, Color.white);
			}
		}

		private void SceneUnitTip(Actor a, bool captain, Vector2 mouse)
		{
			float p = GameArt.P;
			string text = WorldBoxMod.Describe(a);
			string text2 = Favorites.Doing(a);
			string text3 = "";
			if (captain)
			{
				text = "Captain " + text;
			}
			try
			{
				text3 = a.getHealth() + "/" + a.getMaxHealth() + " hp";
			}
			catch
			{
			}
			float width = Mathf.Max(GameArt.Measure(text, 5f).x, GameArt.Measure(text2 + "   " + text3, 4.2f).x) + 12f * p;
			Rect r = new Rect(mouse.x + 12f * p, mouse.y + 8f * p, width, 21f * p);
			if (r.xMax > (float)Screen.width)
			{
				r.x = mouse.x - r.width - 8f * p;
			}
			GameArt.TipPanel(r);
			GameArt.Label(new Rect(r.x + 6f * p, r.y + 1.5f * p, r.width, 9f * p), text, 5f, TextAnchor.MiddleLeft, Color.white);
			GameArt.Label(new Rect(r.x + 6f * p, r.y + 10.5f * p, r.width, 8f * p), text2 + "   " + text3, 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
		}

		private void SceneFlagTip(WarMap map, WarFlag f, Vector2 mouse)
		{
			float p = GameArt.P;
			int count = map.MarkedOwn().Count;
			string text = "Flag " + f.Number + "   " + BandLine(f);
			string text2 = ((count > 0) ? ("Right-click to send the " + count + " marked here") : "Right-click to pull it up");
			float width = Mathf.Max(GameArt.Measure(text, 5f).x, Mathf.Max(GameArt.Measure("Click to mark its band", 4.2f).x, GameArt.Measure(text2, 4.2f).x)) + 12f * p;
			Rect r = new Rect(mouse.x + 12f * p, mouse.y + 8f * p, width, 29f * p);
			if (r.xMax > (float)Screen.width)
			{
				r.x = mouse.x - r.width - 8f * p;
			}
			GameArt.TipPanel(r);
			GameArt.Label(new Rect(r.x + 6f * p, r.y + 1.5f * p, r.width, 9f * p), text, 5f, TextAnchor.MiddleLeft, f.Color);
			GameArt.Label(new Rect(r.x + 6f * p, r.y + 10.5f * p, r.width, 8f * p), "Click to mark its band", 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
			GameArt.Label(new Rect(r.x + 6f * p, r.y + 18.5f * p, r.width, 8f * p), text2, 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
		}

		private void SceneInput(SceneView sc, Actor host, bool war, Event ev)
		{
			Vector2 mousePosition = ev.mousePosition;
			bool flag = _scBar.Contains(mousePosition) || _scKeys.Contains(mousePosition) || OverOrderUi(mousePosition);
			switch (ev.type)
			{
			case EventType.ScrollWheel:
				if (!flag)
				{
					SceneWheel(sc, ev.delta.y, mousePosition);
					ev.Use();
				}
				break;
			case EventType.MouseDown:
				if (!flag && ev.button <= 2)
				{
					ScenePress(sc, host, war, ev.button, mousePosition, ev.clickCount, ev.shift);
					ev.Use();
				}
				break;
			case EventType.MouseDrag:
				if (_scButton >= 0)
				{
					SceneMove(sc, mousePosition);
					ev.Use();
				}
				break;
			case EventType.MouseUp:
				if (_scButton >= 0)
				{
					SceneRelease(sc, host, war, ev.button, mousePosition, ev.shift);
					ev.Use();
				}
				break;
			case EventType.MouseMove:
			case EventType.KeyDown:
			case EventType.KeyUp:
				break;
			}
		}

		private void SceneWheel(SceneView sc, float delta, Vector2 m)
		{
			if (!(Mathf.Abs(delta) < 0.01f))
			{
				Vector2 point;
				Vector2? ground = (_mod.SceneGround(m, out point) ? new Vector2?(point) : ((Vector2?)null));
				sc.Wheel(Mathf.Clamp((0f - delta) / 3f, -3f, 3f), ground, m, _mod.SceneZoomFar);
			}
		}

		private void ScenePress(SceneView sc, Actor host, bool war, int button, Vector2 m, int clicks, bool shift)
		{
			if (_scButton >= 0)
			{
				return;
			}
			WarMap warMap = _mod.WarMap;
			_scButton = button;
			_scShift = war && button == 1 && shift;
			if (_scShift)
			{
				Vector2 point;
				bool num = _mod.SceneGround(m, out point);
				WarFlag warFlag = ((warMap.FlagsKingdom == host.kingdom) ? FlagUnder(warMap, SceneFootOrAway, m) : null);
				if (num || warFlag != null)
				{
					AreaPress(warMap, point, warFlag);
				}
				else
				{
					_scShift = false;
				}
			}
			_scFrom = m;
			_scDragged = false;
			_scGroundSet = _mod.SceneGround(m, out _scGround);
			_scAtFrom = _mod.SceneShownAt;
			_scOnSky = !_scGroundSet;
			_scSkyFrom = m;
			_scSkyAt = sc.At;
			_scFlag = ((war && button == 0 && warMap.FlagsKingdom == host.kingdom) ? FlagUnder(warMap, SceneFootOrAway, m) : null);
			if (!war || button != 0 || _scFlag != null || clicks != 2)
			{
				return;
			}
			int num2 = 0;
			foreach (ScenePip scPip in _scPips)
			{
				if (scPip.Orderable && scPip.S.x >= 0f && scPip.S.y >= 0f && scPip.S.x <= (float)Screen.width && scPip.S.y <= (float)Screen.height && warMap.Marked.Add(scPip.Actor))
				{
					num2++;
				}
			}
			if (num2 > 0)
			{
				Sounds.PlayUI("event:/SFX/Ui/Click");
				_mod.Toast(num2 + " more of your warriors selected");
			}
			_scButton = -1;
		}

		private void SceneMove(SceneView sc, Vector2 m)
		{
			if ((m - _scFrom).sqrMagnitude > 36f)
			{
				_scDragged = true;
			}
			if (_scButton == 1 && _scShift)
			{
				AreaMove(_mod.SceneGround(m, out var point) ? new Vector2?(point) : ((Vector2?)null), _scDragged);
			}
			else if (_scButton >= 1 && _scDragged)
			{
				DragScene(sc, m);
			}
		}

		private void SceneRelease(SceneView sc, Actor host, bool war, int released, Vector2 m, bool shift)
		{
			if (released == _scButton)
			{
				int scButton = _scButton;
				_scButton = -1;
				sc.LetGo();
				if (scButton == 1 && _scShift)
				{
					_scShift = false;
					AreaMove(_mod.SceneGround(m, out var point) ? new Vector2?(point) : ((Vector2?)null), _scDragged);
					AreaRelease(_mod.WarMap, host);
				}
				else if (scButton == 0 && war)
				{
					SceneLeftClick(sc, _mod.WarMap, host, m, shift);
				}
				else if (scButton == 1 && !_scDragged)
				{
					SceneRightClick(_mod.WarMap, host, war, m);
				}
				_scDragged = false;
				_scFlag = null;
			}
		}

		internal void SceneMouseForTest(int button, Vector2 from, Vector2 to, bool shift = false, int clicks = 1)
		{
			SceneView scene = _mod.Scene;
			Actor host = _mod.Host;
			if (!scene.On || host == null)
			{
				return;
			}
			bool war = scene.Page == MapTabs.Page.War;
			if (_scBar.Contains(from) || _scKeys.Contains(from) || OverOrderUi(from))
			{
				return;
			}
			ScenePress(scene, host, war, button, from, clicks, shift);
			if (_scButton < 0)
			{
				return;
			}
			if (to != from)
			{
				for (int i = 1; i <= 10; i++)
				{
					SceneMove(scene, Vector2.Lerp(from, to, (float)i / 10f));
				}
			}
			SceneRelease(scene, host, war, button, to, shift);
		}

		internal void ScenePressForTest(int button, Vector2 from, Vector2 to)
		{
			SceneView scene = _mod.Scene;
			Actor host = _mod.Host;
			if (scene.On && host != null)
			{
				ScenePress(scene, host, scene.Page == MapTabs.Page.War, button, from, 1, shift: false);
				for (int i = 1; i <= 10; i++)
				{
					SceneMove(scene, Vector2.Lerp(from, to, (float)i / 10f));
				}
			}
		}

		internal bool SceneButtonsForTest(Vector2 from, Vector2 to)
		{
			SceneView scene = _mod.Scene;
			Actor host = _mod.Host;
			if (!scene.On || host == null)
			{
				return false;
			}
			bool war = scene.Page == MapTabs.Page.War;
			ScenePress(scene, host, war, 1, from, 1, shift: false);
			SceneMove(scene, Vector2.Lerp(from, to, 0.5f));
			ScenePress(scene, host, war, 0, to, 1, shift: false);
			SceneRelease(scene, host, war, 0, to, shift: false);
			bool num = _scButton == 1;
			SceneMove(scene, to);
			SceneRelease(scene, host, war, 1, to, shift: false);
			if (num)
			{
				return _scButton < 0;
			}
			return false;
		}

		internal void SceneWheelForTest(float delta, Vector2 at)
		{
			if (_mod.Scene.On)
			{
				SceneWheel(_mod.Scene, delta, at);
			}
		}

		private void DragScene(SceneView sc, Vector2 m)
		{
			if (_mod.SceneGround(m, out var point))
			{
				if (!_scGroundSet || _scOnSky)
				{
					_scGround = point;
					_scGroundSet = true;
				}
				else
				{
					sc.Dragged(_mod.SceneShownAt + (_scGround - point));
				}
				_scOnSky = false;
				sc.Hold(_scGround, m);
				_scSkyFrom = m;
				_scSkyAt = sc.At;
			}
			else
			{
				_scOnSky = true;
				sc.Unhold();
				float num = 2f * sc.Zoom / Mathf.Max(1f, Screen.height);
				Vector2 vector = m - _scSkyFrom;
				sc.Dragged(_scSkyAt - (sc.Right * vector.x - sc.Ahead * vector.y) * num);
			}
		}

		private void SceneLeftClick(SceneView sc, WarMap map, Actor host, Vector2 m, bool shift)
		{
			if (_scDragged && _scFlag == null)
			{
				Rect rect = Rect.MinMaxRect(Mathf.Min(_scFrom.x, m.x), Mathf.Min(_scFrom.y, m.y), Mathf.Max(_scFrom.x, m.x), Mathf.Max(_scFrom.y, m.y));
				if (!shift)
				{
					map.Marked.Clear();
					map.SelectedFlag = null;
				}
				int num = 0;
				foreach (ScenePip scPip in _scPips)
				{
					if (scPip.Orderable && (rect.Contains(scPip.Body) || rect.Contains(scPip.S)) && map.Marked.Add(scPip.Actor))
					{
						num++;
					}
				}
				if (num > 0)
				{
					Sounds.PlayUI("event:/SFX/Ui/Click");
				}
			}
			else
			{
				if (_scDragged)
				{
					return;
				}
				if (_scFlag != null && map.Flags.Contains(_scFlag))
				{
					bool num2 = _bandKeyNumber == _scFlag.Number && Time.unscaledTime - _bandKeyAt < 0.4f;
					PickBand(map, _scFlag, shift);
					if (num2)
					{
						sc.JumpTo(_scFlag.P);
					}
					return;
				}
				Actor actor = PipUnder(m, orderableOnly: false);
				int num3 = ((actor == null) ? SceneTownUnder(map, m) : (-1));
				if (actor != null)
				{
					ClickUnit(map, host, actor, shift);
				}
				else if (num3 >= 0)
				{
					map.Pick(map.Towns[num3].Owner, goThere: false);
					_cardCity = map.Towns[num3].City;
					Sounds.PlayUI("event:/SFX/Ui/Click");
				}
				else if (_cardCity != null)
				{
					_cardCity = null;
					Sounds.PlayUI("event:/SFX/Ui/Click");
				}
				else if (!shift && map.Marked.Count > 0)
				{
					map.Marked.Clear();
					map.SelectedFlag = null;
					Sounds.PlayUI("event:/SFX/Ui/Click");
				}
			}
		}

		private void SceneRightClick(WarMap map, Actor host, bool war, Vector2 m)
		{
			Vector2 point2;
			if (war)
			{
				WarFlag warFlag = ((map.FlagsKingdom == host.kingdom) ? FlagUnder(map, SceneFootOrAway, m) : null);
				int count = map.MarkedOwn().Count;
				if (warFlag != null)
				{
					if (count > 0)
					{
						SendToFlag(map, host, warFlag);
					}
					else
					{
						PullUpFlag(map, warFlag);
					}
				}
				else if (count == 0 && map.MarkedShips().Count > 0)
				{
					_mod.Toast("Ships take their orders on the map");
				}
				else
				{
					Actor actor = PipUnder(m, orderableOnly: false);
					int num = ((actor == null) ? SceneTownUnder(map, m) : (-1));
					Vector2 point;
					if (actor != null || num >= 0)
					{
						RightOrder(map, host, actor?.current_position ?? map.Towns[num].P, actor, (num >= 0) ? new WarMap.Town?(map.Towns[num]) : ((WarMap.Town?)null));
					}
					else if (_mod.SceneGround(m, out point))
					{
						RightOrder(map, host, point, null, null);
					}
				}
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
			else if (!_mod.SoldierMap.Captaincy)
			{
				_mod.Toast("Only a captain gives orders");
			}
			else if (_mod.SceneGround(m, out point2))
			{
				WorldTile worldTile = TileAt(point2);
				if (worldTile != null)
				{
					Command(host, Service.CommandKind.March, worldTile, "Holding your mark", "Your soldiers march to your mark (" + Service.Heading(host.current_position, point2) + ").");
				}
			}
		}

		public bool DrawSettings(SettingsMenu menu, Settings s)
		{
			bool flag = Event.current.type == EventType.Repaint;
			if (flag)
			{
				menu.Unroll = Mathf.MoveTowards(menu.Unroll, menu.Open ? 1f : 0f, Time.unscaledDeltaTime / (menu.Open ? 0.24f : 0.15f));
			}
			if (menu.Unroll <= 0f || (!menu.Open && !flag))
			{
				return false;
			}
			EnsureAssets();
			GameArt.Ensure();
			GameArt.EnsureWindowArt();
			EnsureSettingsStyles();
			float num = Screen.width;
			float num2 = Screen.height;
			float p = GameArt.P;
			float unroll = menu.Unroll;
			if (flag)
			{
				Fill(new Rect(0f, 0f, num, num2), new Color(0.02f, 0.03f, 0.06f, 0.55f * unroll));
				DrawGears(num, num2, unroll);
			}
			float num3 = Mathf.Round(Mathf.Min(300f * p, num - 90f * p));
			float num4 = Mathf.Round(Mathf.Min(330f * p, num2 - 36f * p));
			Rect rect = new Rect(Mathf.Round((num - num3) * 0.5f + 10f * p), Mathf.Round((num2 - num4) * 0.5f + 5f * p), num3, num4);
			float num5 = (menu.Open ? Mathf.LerpUnclamped(0.86f, 1f, Settle(unroll)) : Mathf.Lerp(0.93f, 1f, unroll));
			Matrix4x4 matrix = GUI.matrix;
			Color color = GUI.color;
			GUIUtility.ScaleAroundPivot(new Vector2(num5, num5), rect.center);
			GUI.color = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(unroll * 1.7f));
			bool result = false;
			try
			{
				DropInput(menu, s);
				Vector2 mousePosition = Event.current.mousePosition;
				if (menu.Drop != null)
				{
					Event.current.mousePosition = new Vector2(-100000f, -100000f);
				}
				DrawSettingsTabs(rect, menu, out var hoverTab);
				GameArt.Window(rect, Color.white);
				GameArt.Label(new Rect(rect.x, rect.y + 5f * p, rect.width, 14f * p), "Settings", 7.5f, TextAnchor.MiddleCenter, GameArt.Orange);
				Rect view = new Rect(rect.x + 9f * p, rect.y + 30f * p, rect.width - 18f * p, rect.height - 40f * p);
				DrawSettingsPage(view, menu, s, rect);
				Rect r = new Rect(Mathf.Round(rect.xMax - 33f * p), Mathf.Round(rect.y + 2f * p), Mathf.Round(34f * p), Mathf.Round(36f * p));
				bool num6 = r.Contains(Event.current.mousePosition);
				GameArt.Sliced(r, GameArt.CloseTab, Color.white);
				float num7 = Mathf.Round((num6 ? 25f : 23f) * p);
				Sprite sprite = GameArt.IconNamed("iconClose");
				Rect r2 = new Rect(Mathf.Round(r.center.x - num7 * 0.5f - 1f * p), Mathf.Round(r.center.y - num7 * 0.5f + 1f * p), num7, num7);
				if (sprite != null)
				{
					GameArt.Icon(new Rect(r2.x + p, r2.y + p, num7, num7), sprite, new Color(0f, 0f, 0f, 0.4f));
					GameArt.Icon(r2, sprite, Color.white);
				}
				else
				{
					GameArt.Label(r2, "X", 9f, TextAnchor.MiddleCenter, new Color(0.95f, 0.25f, 0.2f));
				}
				if (menu.Open && Button(r, GUIContent.none, GUIStyle.none))
				{
					result = true;
				}
				float num8 = Mathf.Round(104f * p);
				float num9 = Mathf.Round(16f * p);
				float num10 = Mathf.Round(6f * p);
				int num11 = 1 + (_mod.CanRunButton ? 1 : 0) + (_mod.CanLeaveBody ? 1 : 0);
				float num12 = Mathf.Round(rect.center.x - ((float)num11 * num8 + (float)(num11 - 1) * num10) * 0.5f);
				float y = Mathf.Round(Mathf.Min(rect.yMax + 5f * p, num2 - num9 - 4f * p));
				if (GameButton(new Rect(num12, y, num8, num9), "Help (F1)", red: false, menu.Open, GameArt.IconNamed("iconAbout") ?? GameArt.About))
				{
					_mod.AskHelp();
				}
				num12 += num8 + num10;
				if (_mod.CanRunButton)
				{
					if (GameButton(new Rect(num12, y, num8, num9), _mod.RunButtonName, red: false, menu.Open, GameArt.IconNamed("iconSkull") ?? GameArt.Kills))
					{
						_mod.AskRun();
					}
					num12 += num8 + num10;
				}
				if (_mod.CanLeaveBody && GameButton(new Rect(num12, y, num8, num9), "Leave body (G)", red: true, menu.Open, GameArt.IconNamed("iconPossessed")))
				{
					_mod.AskLeaveBody();
				}
				if (hoverTab >= 0)
				{
					string text = SettingsMenu.Tabs[hoverTab];
					float num13 = GameArt.Measure(text, 5.5f).x + 10f * p;
					Rect r3 = new Rect(Mathf.Round(rect.x - 24f * p - num13), Mathf.Round(rect.y + (46f + (float)hoverTab * 30f) * p + 6f * p), Mathf.Round(num13), Mathf.Round(14f * p));
					GameArt.TipPanel(r3);
					GameArt.Label(r3, text, 5.5f, TextAnchor.MiddleCenter, Color.white);
				}
				Event.current.mousePosition = mousePosition;
				DrawDropList(menu, s);
				return result;
			}
			finally
			{
				GUI.matrix = matrix;
				GUI.color = color;
			}
		}

		private void DrawSettingsTabs(Rect win, SettingsMenu menu, out int hoverTab)
		{
			float p = GameArt.P;
			hoverTab = -1;
			Vector2 mousePosition = Event.current.mousePosition;
			for (int i = 0; i < SettingsMenu.Tabs.Length; i++)
			{
				bool flag = menu.Tab == i;
				Rect r = new Rect(Mathf.Round(win.x - (flag ? 22f : 20f) * p), Mathf.Round(win.y + (46f + (float)i * 30f) * p), Mathf.Round(28f * p), Mathf.Round(27f * p));
				Rect rect = new Rect(r.x, r.y, r.width - 4f * p, r.height);
				if (i < TabOnScreen.Length)
				{
					TabOnScreen[i] = new Rect(GUIUtility.GUIToScreenPoint(rect.position), rect.size);
				}
				bool flag2 = rect.Contains(mousePosition);
				if (flag2)
				{
					hoverTab = i;
				}
				GameArt.Sliced(r, GameArt.SideTab, (flag2 || flag) ? Color.white : new Color(0.86f, 0.86f, 0.86f, 1f), 7f, 2f, 6f, 6f);
				Rect r2 = new Rect(r.x + 3f * p, r.y + 3f * p, r.width - 8f * p, r.height - 6f * p);
				if (flag)
				{
					GameArt.FlatButton(r2, red: true, null, 5f);
				}
				Sprite s = GameArt.IconNamed(TabIcons[i]);
				float num = Mathf.Round(18f * p);
				Rect r3 = new Rect(Mathf.Round(r2.center.x - num * 0.5f), Mathf.Round(r2.center.y - num * 0.5f), num, num);
				GameArt.Icon(new Rect(r3.x + p * 0.75f, r3.y + p * 0.75f, num, num), s, new Color(0f, 0f, 0f, 0.45f));
				GameArt.Icon(r3, s, (flag || flag2) ? Color.white : new Color(0.85f, 0.85f, 0.85f, 1f));
				PadNav.Tab(rect, flag);
				if (menu.Open && Button(rect, GUIContent.none, GUIStyle.none) && !flag)
				{
					menu.Tab = i;
					menu.Scroll = Vector2.zero;
					menu.HelpRow = null;
				}
			}
		}

		private void DrawSettingsPage(Rect view, SettingsMenu menu, Settings s, Rect win)
		{
			float p = GameArt.P;
			Event current = Event.current;
			float num = view.width - 8f * p;
			float num2 = 4f * p;
			float num3 = SettingsPageHeight(menu, num);
			float num4 = Mathf.Max(0f, num3 - view.height);
			if (current.type == EventType.ScrollWheel && win.Contains(current.mousePosition) && menu.Open)
			{
				menu.Scroll.y += current.delta.y * 14f * p;
				current.Use();
			}
			menu.Scroll.y = Mathf.Clamp(menu.Scroll.y, 0f, num4);
			_pageOrigin = view.position;
			GUI.BeginGroup(view);
			Vector2 mousePosition = current.mousePosition;
			_pageMouseIn = new Rect(0f, 0f, view.width, view.height).Contains(mousePosition) && menu.Open;
			float num5 = 3f * p - Mathf.Round(menu.Scroll.y);
			int tab = menu.Tab;
			Rect r = new Rect(num2, num5, num, Mathf.Round(70f * p));
			if (r.yMax > 0f && r.y < view.height)
			{
				GameArt.Sliced(r, GameArt.Inner, Color.white);
				Rect r2 = new Rect(r.x + 3f * p, r.y + 3f * p, r.width - 6f * p, r.height - 6f * p);
				GameArt.Cover(panX: Mathf.Sin(Time.unscaledTime * 0.13f + (float)tab * 1.7f) * 0.6f, r: r2, s: GameArt.Art(TabArt[Mathf.Clamp(tab, 0, TabArt.Length - 1)]), tint: Color.white, panY: Mathf.Sin(Time.unscaledTime * 0.09f + (float)tab) * 0.4f);
				GameArt.Sliced(r2, GameArt.RoundMask, new Color(1f, 1f, 1f, 0.12f));
			}
			num5 += r.height + 4f * p;
			Rect r3 = new Rect(num2 - 2f * p, num5, num + 4f * p, Mathf.Round(20f * p));
			GameArt.Sliced(r3, GameArt.SectionStrip, Color.white);
			GameArt.Label(new Rect(r3.x, r3.y - 0.5f * p, r3.width, r3.height - 4f * p), SettingsMenu.Tabs[tab], 6.5f, TextAnchor.MiddleCenter, GameArt.Orange);
			Sprite s2 = GameArt.IconNamed(TabIcons[tab]);
			GameArt.Icon(new Rect(r3.x + 8f * p, r3.y + 1.5f * p, 13f * p, 13f * p), s2, Color.white);
			GameArt.Icon(new Rect(r3.xMax - 21f * p, r3.y + 1.5f * p, 13f * p, 13f * p), s2, Color.white);
			num5 += r3.height + 3f * p;
			switch (tab)
			{
			case 6:
				num5 = DrawMultiplayerPage(num2, num5, num, menu);
				break;
			case 7:
				num5 = DrawModsPage(num2, num5, num);
				break;
			default:
				num5 = DrawSettingsRows(num2, num5, num, menu, s);
				break;
			}
			if (tab == 5)
			{
				num5 = DrawCharacterShelf(num2, num5, num, menu);
			}
			num5 += 4f * p;
			string text = "Changes apply at once and are kept for next time. F1 shows every key";
			GameArt.Label(new Rect(num2 + 2f * p, num5, num * 0.62f, 16f * p), text, 4.4f, TextAnchor.MiddleLeft, GameArt.Grey, wrap: true);
			Rect r4 = new Rect(Mathf.Round(num2 + num - 70f * p), Mathf.Round(num5), Mathf.Round(70f * p), Mathf.Round(16f * p));
			bool flag = _pageMouseIn && r4.Contains(mousePosition);
			GameArt.FlatButton(r4, red: false, "Defaults", 5.5f, flag ? 1f : 0.9f);
			if (PageButton(r4))
			{
				menu.Defaults(s);
			}
			GUI.EndGroup();
			if (num4 > 0.5f)
			{
				DrawPageScrollbar(new Rect(win.xMax + 1f * p, view.y + 8f * p, 7f * p, view.height - 16f * p), menu, num4, view.height / num3);
			}
		}

		private bool PageButton(Rect r)
		{
			PadNav.Spot(r);
			if (!GUI.Button(r, GUIContent.none, GUIStyle.none) || !_pageMouseIn)
			{
				return false;
			}
			Sounds.PlayUI("event:/SFX/Ui/Click");
			return true;
		}

		private float SettingsPageHeight(SettingsMenu menu, float cw)
		{
			float p = GameArt.P;
			float num = 3f * p + Mathf.Round(70f * p) + 4f * p + Mathf.Round(20f * p) + 3f * p;
			if (menu.Tab == 6)
			{
				num += MultiplayerPageHeight(cw) + 10f * p;
			}
			else if (menu.Tab == 7)
			{
				num += ModsPageHeight(cw) + 10f * p;
			}
			else
			{
				List<SettingsMenu.Row> list = menu.RowsOf(menu.Tab);
				if (list != null)
				{
					foreach (SettingsMenu.Row item in list)
					{
						num += CardHeight(item, menu, cw) + 3f * p;
					}
				}
				num += 10f * p;
				if (menu.Tab == 5)
				{
					num += CharacterShelfHeight(menu, cw);
				}
			}
			return num + 4f * p + 16f * p + 6f * p;
		}

		private static bool IsSwitch(SettingsMenu.Row r)
		{
			if (r.IsChoice && r.Choices.Length == 2 && r.Choices[0] == "Off")
			{
				return r.Choices[1] == "On";
			}
			return false;
		}

		private float CardHeight(SettingsMenu.Row r, SettingsMenu menu, float cw)
		{
			float num = Mathf.Round(((IsSwitch(r) || IsDropdown(r)) ? 25f : 36f) * GameArt.P);
			if (r.Heading != null)
			{
				num += HeadingH;
			}
			if (menu.HelpRow == r)
			{
				num += HelpHeight(r, cw);
			}
			return num;
		}

		private static float HelpHeight(SettingsMenu.Row r, float cw)
		{
			return Mathf.Round(GameArt.MeasureHeight(r.Help, 4.3f, cw - 34f * GameArt.P) + 11f * GameArt.P);
		}

		private float DrawSettingsRows(float cx, float y, float cw, SettingsMenu menu, Settings s)
		{
			List<SettingsMenu.Row> list = menu.RowsOf(menu.Tab);
			if (list == null)
			{
				return y;
			}
			float p = GameArt.P;
			float num = 3f * p;
			float num2 = 10f * p;
			foreach (SettingsMenu.Row item in list)
			{
				num2 += CardHeight(item, menu, cw) + num;
			}
			GameArt.Sliced(new Rect(cx, y, cw, num2 - num), GameArt.Inner, new Color(1f, 1f, 1f, 0.56f));
			y += 5f * p;
			Vector2 mousePosition = Event.current.mousePosition;
			Sprite sprite = GameArt.IconNamed("iconQuestionMark");
			foreach (SettingsMenu.Row item2 in list)
			{
				float num3 = CardHeight(item2, menu, cw);
				Rect r = new Rect(cx + 5f * p, y, cw - 10f * p, num3);
				y += num3 + num;
				if (item2.Heading != null)
				{
					Rect rect = new Rect(r.x, r.y, r.width, HeadingH);
					GameArt.Label(new Rect(rect.x + 3f * p, rect.y + 1f * p, rect.width - 6f * p, rect.height - 4f * p), item2.Heading, 5.2f, TextAnchor.MiddleLeft, GameArt.Orange);
					GameArt.Flat(new Rect(rect.x + 2f * p, rect.yMax - 2.5f * p, rect.width - 4f * p, Mathf.Max(1f, Mathf.Round(p * 0.75f))), new Color(1f, 0.607f, 0.11f, 0.35f));
					r.y += HeadingH;
					r.height -= HeadingH;
				}
				bool flag = menu.HelpRow == item2;
				float num4 = (flag ? HelpHeight(item2, cw) : 0f);
				Rect rect2 = new Rect(r.x, r.y, r.width, r.height - num4);
				bool flag2 = _pageMouseIn && r.Contains(mousePosition);
				GameArt.Sliced(r, GameArt.Inner, flag2 ? Color.white : new Color(0.93f, 0.93f, 0.93f, 1f));
				string value;
				Sprite sprite2 = GameArt.IconNamed(RowIcons.TryGetValue(item2.Name, out value) ? value : null);
				Rect r2 = new Rect(rect2.x + 5f * p, rect2.y + ((IsSwitch(item2) || IsDropdown(item2)) ? ((rect2.height - 15f * p) * 0.5f) : (4f * p)), 15f * p, 15f * p);
				if (sprite2 != null)
				{
					GameArt.Icon(new Rect(r2.x, r2.y + p, r2.width, r2.height), sprite2, new Color(0f, 0f, 0f, 0.25f));
					GameArt.Icon(r2, sprite2, Color.white);
				}
				float num5 = rect2.x + 24f * p;
				float num6 = (item2.IsChoice ? 0f : (62f * p));
				float num7 = Mathf.Round(Mathf.Min(92f * p, rect2.width * 0.42f));
				float b = (IsSwitch(item2) ? (46f * p) : (IsDropdown(item2) ? (num7 + 6f * p) : 0f));
				float num8 = Mathf.Max(num6, b);
				float num9 = rect2.xMax - num5 - 6f * p - num8;
				float num10 = Mathf.Round(11f * p);
				GameArt.Label(new Rect(num5, rect2.y + 2.5f * p, num9 - num10 - 3f * p, 10f * p), item2.Name, 5.6f, TextAnchor.MiddleLeft, Color.white);
				GameArt.Label(new Rect(num5, rect2.y + 11.5f * p, num9, 8f * p), item2.Note, 4.1f, TextAnchor.MiddleLeft, GameArt.Grey);
				Rect r3 = new Rect(Mathf.Round(rect2.xMax - 6f * p - num8 - num10), Mathf.Round(rect2.y + 2f * p), num10, num10);
				bool flag3 = _pageMouseIn && r3.Contains(mousePosition);
				GameArt.FlatButton(r3, flag, null, 4f, (flag3 || flag) ? 1f : 0.82f, sprite);
				if (sprite == null)
				{
					GameArt.Label(r3, "?", 5f, TextAnchor.MiddleCenter, Color.white);
				}
				if (PageButton(r3))
				{
					menu.ToggleHelp(item2);
				}
				if (IsSwitch(item2))
				{
					SettingsSwitch(new Rect(rect2.xMax - 46f * p, rect2.center.y - 8f * p, 40f * p, 16f * p), item2, menu, s);
				}
				else if (IsDropdown(item2))
				{
					SettingsDropdown(new Rect(rect2.xMax - 6f * p - num7, Mathf.Round(rect2.center.y - 8f * p), num7, Mathf.Round(16f * p)), item2, menu, s);
				}
				else if (item2.IsChoice)
				{
					SettingsChoices(new Rect(rect2.x + 6f * p, rect2.y + 22f * p, rect2.width - 12f * p, 11f * p), item2, menu, s);
				}
				else
				{
					GameArt.Label(new Rect(rect2.xMax - num6 - 6f * p, rect2.y + 2.5f * p, num6, 10f * p), item2.Show(item2.Value(s)), 5.6f, TextAnchor.MiddleRight, GameArt.Peach);
					SettingsSlider(new Rect(rect2.x + 6f * p, rect2.y + 22f * p, rect2.width - 12f * p, 10f * p), item2, menu, s);
				}
				if (flag)
				{
					Rect r4 = new Rect(r.x + 6f * p, rect2.yMax - 1f * p, r.width - 12f * p, num4 - 4f * p);
					GameArt.TipPanel(r4);
					GameArt.Label(new Rect(r4.x + 5f * p, r4.y + 3f * p, r4.width - 10f * p, r4.height - 5f * p), item2.Help, 4.3f, TextAnchor.UpperLeft, Color.white, wrap: true);
				}
			}
			return y + 5f * p;
		}

		private void SettingsSwitch(Rect r, SettingsMenu.Row row, SettingsMenu menu, Settings s)
		{
			_ = GameArt.P;
			bool flag = row.Chosen(s) == 1;
			bool flag2 = _pageMouseIn && r.Contains(Event.current.mousePosition);
			GameArt.FlatButton(r, flag, flag ? "ON" : "OFF", 5.5f, flag2 ? 1f : 0.93f, GameArt.IconNamed(flag ? "iconOn" : "iconOff"));
			if (PageButton(r))
			{
				menu.Pick(row, s, (!flag) ? 1 : 0);
			}
		}

		private void SettingsChoices(Rect r, SettingsMenu.Row row, SettingsMenu menu, Settings s)
		{
			float p = GameArt.P;
			int num = row.Choices.Length;
			int num2 = row.Chosen(s);
			float num3 = 2f * p;
			float num4 = (r.width - num3 * (float)(num - 1)) / (float)num;
			Vector2 mousePosition = Event.current.mousePosition;
			float size = ((num > 4) ? 4.4f : 4.8f);
			for (int i = 0; i < num; i++)
			{
				Rect r2 = new Rect(Mathf.Round(r.x + (float)i * (num4 + num3)), r.y, Mathf.Round(num4), r.height);
				bool flag = !row.Fixed && _pageMouseIn && r2.Contains(mousePosition);
				GameArt.FlatButton(r2, i == num2, row.Choices[i], size, (flag || i == num2) ? 1f : (row.Fixed ? 0.6f : 0.86f));
				if (!row.Fixed && PageButton(r2))
				{
					menu.Pick(row, s, i);
				}
			}
		}

		private static void PadSteps(Rect r, SettingsMenu.Row row, SettingsMenu menu, Settings s)
		{
			PadNav.Slider(r, delegate(int d)
			{
				menu.Slide(row, s, Mathf.Clamp(row.Value(s) + (float)d * row.Step, row.Min, row.Max));
			});
		}

		private void SettingsSlider(Rect r, SettingsMenu.Row row, SettingsMenu menu, Settings s)
		{
			float p = GameArt.P;
			float num = Mathf.Round(15f * p);
			float num2 = row.ToSlider(row.Value(s));
			Rect r2 = new Rect(r.x, Mathf.Round(r.center.y - 4f * p), r.width, Mathf.Round(8f * p));
			float a = r2.x + num * 0.5f;
			float b = r2.xMax - num * 0.5f;
			float num3 = Mathf.Lerp(a, b, num2);
			bool flag = _pageMouseIn && new Rect(r.x, r.y - 3f * p, r.width, r.height + 6f * p).Contains(Event.current.mousePosition);
			if (Event.current.type == EventType.Repaint)
			{
				GameArt.Sliced(r2, GameArt.GreyButton, Color.white, 3f, 3f, 3f, 3f);
				if (num3 - r2.x > 2f * p)
				{
					GameArt.Sliced(new Rect(r2.x, r2.y, num3 - r2.x, r2.height), GameArt.RedButton, Color.white, 5f, 5f, 5f, 6f, Mathf.Min(p, r2.height / 14f));
				}
				Rect r3 = new Rect(Mathf.Round(num3 - num * 0.5f), Mathf.Round(r.center.y - 6.5f * p), num, Mathf.Round(13f * p));
				GameArt.Sliced(new Rect(r3.x + p * 0.5f, r3.y + p, r3.width, r3.height), GameArt.GreyButton, new Color(0f, 0f, 0f, 0.35f), 3f, 3f, 3f, 3f);
				GameArt.Sliced(r3, GameArt.RedButton, flag ? Color.white : new Color(0.93f, 0.93f, 0.93f, 1f), 5f, 5f, 5f, 6f, Mathf.Min(p, r3.height / 14f));
			}
			Rect position = new Rect(r.x, r.y - 3f * p, r.width, r.height + 6f * p);
			if (!_pageMouseIn && GUIUtility.hotControl == 0 && Event.current.type == EventType.MouseDown)
			{
				position = new Rect(-9999f, -9999f, position.width, position.height);
			}
			if (PadNav.Wanted)
			{
				PadSteps(r, row, menu, s);
			}
			float num4 = GUI.HorizontalSlider(position, num2, 0f, 1f, _setTrack, _setThumb);
			if (menu.Open && num4 != num2)
			{
				menu.Slide(row, s, row.FromSlider(num4));
			}
		}

		private void DrawPageScrollbar(Rect track, SettingsMenu menu, float maxScroll, float shown)
		{
			float p = GameArt.P;
			float num = Mathf.Max(18f * p, track.height * Mathf.Clamp01(shown));
			float num2 = ((maxScroll > 0f) ? (menu.Scroll.y / maxScroll) : 0f);
			Rect r = new Rect(track.x, Mathf.Round(track.y + (track.height - num) * num2), track.width, Mathf.Round(num));
			Event current = Event.current;
			if (current.type == EventType.Repaint)
			{
				if (GameArt.ScrollTrack != null)
				{
					GameArt.Sliced(track, GameArt.ScrollTrack, Color.white, -1f, -1f, -1f, -1f, track.width / 17f);
				}
				else
				{
					GameArt.Sliced(track, GameArt.DarkField, Color.white, 3f, 3f, 3f, 3f);
				}
				GameArt.Sliced(r, GameArt.RedButton, Color.white, 5f, 5f, 5f, 6f, Mathf.Min(p, track.width / 10f));
			}
			int controlID = GUIUtility.GetControlID(FocusType.Passive);
			switch (current.GetTypeForControl(controlID))
			{
			case EventType.MouseDown:
				if (menu.Open && new Rect(track.x - 3f * p, track.y, track.width + 6f * p, track.height).Contains(current.mousePosition))
				{
					GUIUtility.hotControl = controlID;
					menu.Scroll.y = Mathf.Clamp01((current.mousePosition.y - track.y - num * 0.5f) / Mathf.Max(1f, track.height - num)) * maxScroll;
					current.Use();
				}
				break;
			case EventType.MouseDrag:
				if (GUIUtility.hotControl == controlID)
				{
					menu.Scroll.y = Mathf.Clamp01((current.mousePosition.y - track.y - num * 0.5f) / Mathf.Max(1f, track.height - num)) * maxScroll;
					current.Use();
				}
				break;
			case EventType.MouseUp:
				if (GUIUtility.hotControl == controlID)
				{
					GUIUtility.hotControl = 0;
					current.Use();
				}
				break;
			case EventType.MouseMove:
				break;
			}
		}

		private void DrawGears(float sw, float sh, float alpha)
		{
			Sprite sprite = GameArt.IconNamed("iconOptions");
			if (!(sprite == null))
			{
				float p = GameArt.P;
				float unscaledTime = Time.unscaledTime;
				Matrix4x4 matrix = GUI.matrix;
				for (int i = 0; i < 22; i++)
				{
					float num = Mathf.Repeat(Mathf.Sin((float)i * 12.9898f) * 43758.547f, 1f);
					float num2 = Mathf.Repeat(Mathf.Sin((float)i * 78.233f) * 12345.678f, 1f);
					float num3 = Mathf.Repeat(Mathf.Sin((float)i * 39.425f) * 24634.635f, 1f);
					float num4 = Mathf.Round((10f + 16f * num3) * p);
					float f = Mathf.Repeat(num + unscaledTime * 0.004f * (0.5f + num2), 1f) * (sw + num4 * 2f) - num4;
					float f2 = Mathf.Repeat(num2 + unscaledTime * 0.007f * (0.5f + num3), 1f) * (sh + num4 * 2f) - num4;
					Rect r = new Rect(Mathf.Round(f), Mathf.Round(f2), num4, num4);
					GUIUtility.RotateAroundPivot(unscaledTime * (12f + 30f * num) * ((i % 2 == 0) ? 1f : (-1f)), r.center);
					GameArt.Icon(r, sprite, new Color(1f, 1f, 1f, (0.45f + 0.4f * num3) * alpha));
					GUI.matrix = matrix;
				}
			}
		}

		private void EnsureSettingsStyles()
		{
			float p = GameArt.P;
			if (_setTrack == null || !Mathf.Approximately(_setStylesFor, p))
			{
				_setStylesFor = p;
				_setTrack = new GUIStyle();
				_setThumb = new GUIStyle
				{
					fixedWidth = Mathf.Round(15f * p)
				};
			}
		}

		private YardLook Yard(City c)
		{
			float unscaledTime = Time.unscaledTime;
			if (_yard.City == c && unscaledTime - _yard.At < 0.5f)
			{
				return _yard;
			}
			_yard.City = c;
			_yard.At = unscaledTime;
			_yard.Docks = Shipyard.HasDocks(c);
			for (int i = 0; i < 3; i++)
			{
				Shipyard.Hull h = (Shipyard.Hull)i;
				_yard.Afloat[i] = (_yard.Docks ? Shipyard.Afloat(c, h) : 0);
				_yard.Cap[i] = (_yard.Docks ? Shipyard.Cap(c, h) : 0);
				_yard.Ordered[i] = Shipyard.Ordered(c, h);
			}
			_yard.Building = Shipyard.Building(c, out _yard.Soonest);
			return _yard;
		}

		private float ShipyardHeight(City c)
		{
			if (!Yard(c).Docks)
			{
				return 12f;
			}
			return 40f;
		}

		private void DrawShipyard(City c, Actor host, float x, float y, float w, bool repaint)
		{
			float p = GameArt.P;
			YardLook yardLook = Yard(c);
			if (!yardLook.Docks)
			{
				if (repaint)
				{
					GameArt.Label(new Rect(x, y, w, 9f * p), "No docks. Ships are built at docks", 3.9f, TextAnchor.MiddleLeft, GameArt.Grey);
				}
				return;
			}
			if (repaint)
			{
				GameArt.Label(new Rect(x, y, w, 9f * p), "Ships", 5f, TextAnchor.MiddleLeft, GameArt.Orange);
				string text = ((yardLook.Building == 0) ? ("Purse " + Economy.Money(host) + " gold") : (yardLook.Building + " on order" + ((yardLook.Soonest >= 0f) ? (" · next in " + Mathf.CeilToInt(yardLook.Soonest) + " s") : " · waiting for a dock")));
				GameArt.Label(new Rect(x, y, w, 9f * p), text, 3.9f, TextAnchor.MiddleRight, (yardLook.Building > 0) ? GameArt.Peach : GameArt.Grey);
			}
			y += 10f * p;
			float num = 2f * p;
			float num2 = Mathf.Round((w - num * 2f) / 3f);
			float height = Mathf.Round(14f * p);
			for (int i = 0; i < 3; i++)
			{
				Shipyard.Hull h = (Shipyard.Hull)i;
				Rect r = new Rect(Mathf.Round(x + (float)i * (num2 + num)), y, num2, height);
				bool flag = yardLook.Cap[i] > 0 && yardLook.Afloat[i] + yardLook.Ordered[i] < yardLook.Cap[i];
				if (GameButton(r, Shipyard.Short[i] + " " + Shipyard.Prices[i], red: false, flag, null, 4f) && Shipyard.Order(host, c, h, _mod.Toast))
				{
					_yard.At = -1f;
					Sounds.PlayUI("event:/SFX/Ui/Click");
				}
				if (repaint)
				{
					string text2 = ((yardLook.Cap[i] != 0) ? (yardLook.Afloat[i] + yardLook.Ordered[i] + " of " + yardLook.Cap[i] + ((yardLook.Ordered[i] > 0) ? (" (" + yardLook.Ordered[i] + " building)") : "")) : ((i == 2) ? "docks building" : "needs a harbour"));
					GameArt.Label(new Rect(r.x, r.yMax + 0.5f * p, r.width, 7f * p), text2, 3.5f, TextAnchor.UpperCenter, flag ? GameArt.Grey : new Color(0.75f, 0.55f, 0.5f));
				}
			}
		}

		private bool BoardButton(WarMap map, Actor host, Rect r, int selected, Vector2 mouse, bool repaint)
		{
			float p = GameArt.P;
			bool flag = r.Contains(mouse);
			bool flag2 = selected > 0;
			if (repaint)
			{
				Rect r2 = ((flag && flag2) ? new Rect(r.x - p, r.y - p, r.width + 2f * p, r.height + 2f * p) : r);
				GameArt.StoneButton(r2, null, 1f, flag2, Color.white);
				float num = Mathf.Round(r2.width * 0.62f);
				Sprite sprite = GameArt.IconNamed("iconBoat");
				if (sprite != null)
				{
					GameArt.Icon(new Rect(Mathf.Round(r2.center.x - num * 0.5f), Mathf.Round(r2.center.y - num * 0.5f), num, num), sprite, flag2 ? Color.white : new Color(1f, 1f, 1f, 0.4f));
				}
				if (flag && flag2)
				{
					GameArt.Sliced(r2, GameArt.ButtonBorder, new Color(1f, 1f, 1f, 0.6f), 4f, 4f, 4f, 4f, r2.width / 32f);
				}
				GameArt.Label(new Rect(r.x - 4f * p, r.yMax - 0.5f * p, r.width + 8f * p, 7f * p), "Board", 3.8f, TextAnchor.UpperCenter, GameArt.Grey);
			}
			PadNav.Spot(r);
			if (GUI.Button(r, GUIContent.none, GUIStyle.none))
			{
				if (!flag2)
				{
					_mod.Toast("Select warriors first, then Board");
					return flag;
				}
				List<Actor> troops = map.MarkedOwn();
				Actor actor = map.TransportFor(host, troops);
				if (actor == null)
				{
					_mod.Toast("No transport of yours can reach them. Order one on a harbour town's card");
				}
				else
				{
					BoardOrder(map, host, actor);
				}
			}
			return flag;
		}

		private void DrawShotgunReticle(float cx, float cy, float thick, Color color, CombatFeedback fb)
		{
			ShotgunControl shotgun = _mod.Shotgun;
			FrameProjection shown = _mod.Shown;
			float num = Screen.width;
			float num2 = ((shown.Width > 0) ? (shown.Focal * num / (float)shown.Width) : (num * 0.5f));
			float num3 = Mathf.Max(10f, num2 * Mathf.Tan(0.055099998f));
			float b = Hands.ShotgunKick(shotgun.State.SinceShot);
			num3 *= 1f + 0.35f * Mathf.Max(0f, b);
			int num4 = 4;
			float num5 = 30f;
			int num6 = Mathf.Max(8, Mathf.CeilToInt(num3 * (90f - num5) * (MathF.PI / 180f) / (thick * 0.6f)));
			float num7 = Mathf.Max(1f, thick * 0.35f);
			Color c = new Color(0f, 0f, 0f, color.a * 0.45f);
			for (int i = 0; i < 2; i++)
			{
				for (int j = 0; j < num4; j++)
				{
					float num8 = (float)j * 90f + 45f;
					float a = num8 - (90f - num5) * 0.5f;
					float b2 = num8 + (90f - num5) * 0.5f;
					for (int k = 0; k <= num6; k++)
					{
						float f = Mathf.Lerp(a, b2, (float)k / (float)num6) * (MathF.PI / 180f);
						float num9 = cx + Mathf.Cos(f) * num3;
						float num10 = cy + Mathf.Sin(f) * num3;
						if (i == 0)
						{
							Fill(new Rect(num9 - thick * 0.5f - num7, num10 - thick * 0.5f - num7, thick + num7 * 2f, thick + num7 * 2f), c);
						}
						else
						{
							Fill(new Rect(num9 - thick * 0.5f, num10 - thick * 0.5f, thick, thick), color);
						}
					}
				}
			}
			float num11 = thick * 1.3f;
			Fill(new Rect(cx - num11 * 0.5f - num7, cy - num11 * 0.5f - num7, num11 + num7 * 2f, num11 + num7 * 2f), c);
			Fill(new Rect(cx - num11 * 0.5f, cy - num11 * 0.5f, num11, num11), color);
			DrawShells(cx, cy + num3 + thick * 6f, thick);
		}

		private void DrawShells(float cx, float top, float thick)
		{
			ShotgunControl shotgun = _mod.Shotgun;
			ShotgunState state = shotgun.State;
			float num = Time.unscaledTime - shotgun.ChangedAt;
			float num2 = ((state.Loaded < 6 || state.Reloading) ? 1f : Mathf.Clamp01(1f - (num - 1.8f) / 0.8f));
			if (num2 <= 0.01f)
			{
				return;
			}
			float num3 = Mathf.Max(2f, thick * 1.1f);
			float num4 = 3f * num3;
			float height = 7f * num3;
			float num5 = 2f * num3;
			int num6 = 6;
			float num7 = cx - ((float)num6 * num4 + (float)(num6 - 1) * num5) * 0.5f;
			Color c = new Color(0.08f, 0.06f, 0.06f, 0.75f * num2);
			for (int i = 0; i < num6; i++)
			{
				float x = num7 + (float)i * (num4 + num5);
				bool num8 = i < state.Loaded;
				float num9 = ((num8 && i == state.Loaded - 1 && num < 0.25f) ? ((1f - num / 0.25f) * num3) : 0f);
				Rect rect = new Rect(x, top - num9, num4, height);
				Fill(new Rect(rect.x - num3 * 0.5f, rect.y - num3 * 0.5f, rect.width + num3, rect.height + num3), c);
				if (num8)
				{
					Fill(new Rect(rect.x, rect.y, rect.width, rect.height - 2f * num3), new Color(0.78f, 0.2f, 0.18f, 0.95f * num2));
					Fill(new Rect(rect.x, rect.y, num3, rect.height - 2f * num3), new Color(0.95f, 0.38f, 0.32f, 0.95f * num2));
					Fill(new Rect(rect.x, rect.yMax - 2f * num3, rect.width, 2f * num3), new Color(0.9f, 0.72f, 0.36f, 0.95f * num2));
				}
				else
				{
					Fill(new Rect(rect.x + num3 * 0.5f, rect.y + num3 * 0.5f, rect.width - num3, rect.height - num3), new Color(0.35f, 0.3f, 0.3f, 0.35f * num2));
				}
			}
			if (state.Reloading)
			{
				int num10 = Mathf.Min(num6 - 1, state.Loaded);
				if (state.Loaded < num6 && (int)(Time.unscaledTime * 6f) % 2 == 0)
				{
					float x2 = num7 + (float)num10 * (num4 + num5);
					Fill(new Rect(x2, top, num4, height), new Color(1f, 0.85f, 0.5f, 0.35f * num2));
				}
			}
		}

		private static void Outline(Rect r, float t, Color c)
		{
			GameArt.Flat(new Rect(r.x, r.y, r.width, t), c);
			GameArt.Flat(new Rect(r.x, r.yMax - t, r.width, t), c);
			GameArt.Flat(new Rect(r.x, r.y, t, r.height), c);
			GameArt.Flat(new Rect(r.xMax - t, r.y, t, r.height), c);
		}

		public void DrawSoldierMap()
		{
			EnsureAssets();
			SoldierMap soldierMap = _mod.SoldierMap;
			Actor actor = _mod.Body ?? _mod.Host;
			if (actor == null)
			{
				soldierMap.Open = false;
				return;
			}
			Event current = Event.current;
			bool flag = current.type == EventType.Repaint;
			bool close;
			Rect rect = MapWindow(soldierMap.Founder ? "Founder's Map" : (soldierMap.Captaincy ? "Captain's Map" : "Soldier's Map"), out close);
			if (close)
			{
				soldierMap.Open = false;
				return;
			}
			float p = GameArt.P;
			float num = Mathf.Round(158f * p);
			Rect r = new Rect(rect.x + num + 8f * p, rect.y, rect.xMax - (rect.x + num + 8f * p), rect.height);
			GameArt.Sliced(r, GameArt.Inner, Color.white);
			Rect mapRect = new Rect(Mathf.Round(r.x + 4f * p), Mathf.Round(r.y + 4f * p), Mathf.Round(r.width - 8f * p), Mathf.Round(r.height - 8f * p));
			SoldierMapRectForTest = mapRect;
			if (flag)
			{
				SmPanKeys(soldierMap);
			}
			float num2 = soldierMap.Span / Mathf.Max(0.01f, soldierMap.Zoom);
			float ppt = mapRect.width / num2;
			float viewH = mapRect.height / ppt;
			soldierMap.Centre = SmClamp(soldierMap.Centre, num2, viewH);
			float vx0 = soldierMap.Centre.x - num2 * 0.5f;
			float vy0 = soldierMap.Centre.y - viewH * 0.5f;
			Actor actor2 = null;
			if (flag)
			{
				Fill(mapRect, Minimap.FogColor);
				Minimap minimap = _mod.Minimap;
				float num3 = Mathf.Max(minimap.WorldW, minimap.WorldH);
				if (minimap.WorldLand != null && num3 > 0f)
				{
					float num4 = Mathf.Max(vx0, 0f);
					float num5 = Mathf.Max(vy0, 0f);
					float num6 = Mathf.Min(vx0 + num2, num3);
					float num7 = Mathf.Min(vy0 + viewH, num3);
					if (num6 > num4 && num7 > num5)
					{
						Vector2 vector = ToScreen(new Vector2(num4, num7));
						Vector2 vector2 = ToScreen(new Vector2(num6, num5));
						GUI.DrawTextureWithTexCoords(Rect.MinMaxRect(vector.x, vector.y, vector2.x, vector2.y), minimap.WorldLand, new Rect(num4 / num3, num5 / num3, (num6 - num4) / num3, (num7 - num5) / num3));
					}
				}
				actor2 = SmContents(soldierMap, actor, mapRect, ToScreen, ppt, current);
			}
			SmButtons(soldierMap, actor, mapRect);
			if (!soldierMap.Founder)
			{
				SceneButton(mapRect, ToScreen, soldierMap.Centre, MapTabs.Page.Orders);
			}
			SmInput(soldierMap, actor, mapRect, ToWorld, ppt, current);
			if (flag && actor2 != null && mapRect.Contains(current.mousePosition) && _smButton < 0)
			{
				string text = WorldBoxMod.Describe(actor2);
				string text2 = Favorites.Doing(actor2);
				string text3 = "";
				if (actor2 == soldierMap.Captain)
				{
					text = "Captain " + text;
				}
				try
				{
					text3 = actor2.getHealth() + "/" + actor2.getMaxHealth() + " hp";
				}
				catch
				{
				}
				float width = Mathf.Max(GameArt.Measure(text, 5f).x, GameArt.Measure(text2 + "   " + text3, 4.2f).x) + 12f * p;
				Rect r2 = new Rect(current.mousePosition.x + 12f * p, current.mousePosition.y + 8f * p, width, 21f * p);
				if (r2.xMax > (float)Screen.width)
				{
					r2.x = current.mousePosition.x - r2.width - 8f * p;
				}
				GameArt.TipPanel(r2);
				GameArt.Label(new Rect(r2.x + 6f * p, r2.y + 1.5f * p, r2.width, 9f * p), text, 5f, TextAnchor.MiddleLeft, Color.white);
				GameArt.Label(new Rect(r2.x + 6f * p, r2.y + 10.5f * p, r2.width, 8f * p), text2 + "   " + text3, 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
			}
			Rect panel = new Rect(rect.x, rect.y, num, rect.height);
			if (soldierMap.Founder)
			{
				SmFounderPanel(soldierMap, actor, panel);
			}
			else
			{
				SmSoldierPanel(soldierMap, actor, panel);
			}
			Vector2 ToScreen(Vector2 vector3)
			{
				return new Vector2(mapRect.x + (vector3.x - vx0) * ppt, mapRect.y + (vy0 + viewH - vector3.y) * ppt);
			}
			Vector2 ToWorld(Vector2 s)
			{
				return new Vector2(vx0 + (s.x - mapRect.x) / ppt, vy0 + viewH - (s.y - mapRect.y) / ppt);
			}
		}

		private Actor SmContents(SoldierMap map, Actor host, Rect mapRect, Func<Vector2, Vector2> toScreen, float ppt, Event ev)
		{
			float p = GameArt.P;
			float unscaledTime = Time.unscaledTime;
			float num = 0.5f + 0.5f * Mathf.Sin(unscaledTime * 5f);
			GUI.BeginGroup(mapRect);
			Vector2 origin = new Vector2(mapRect.x, mapRect.y);
			Vector2 mousePosition = ev.mousePosition;
			if (map.Founder)
			{
				float num2 = 4f;
				Commission commission = map.Commission;
				foreach (TileZone site in map.Sites)
				{
					Vector2 vector = new Vector2((float)site.centerTile.x + 0.5f, (float)site.centerTile.y + 0.5f);
					Vector2 s = S(vector + new Vector2(0f - num2, num2));
					Vector2 s2 = S(vector + new Vector2(num2, 0f - num2));
					if (In(s, 40f * p) || In(s2, 40f * p))
					{
						bool flag = commission != null && Mathf.Abs(site.centerTile.x - commission.X) < 2 && Mathf.Abs(site.centerTile.y - commission.Y) < 2;
						Rect r = Rect.MinMaxRect(s.x + 1f, s.y + 1f, s2.x - 1f, s2.y - 1f);
						GameArt.Flat(r, new Color(Plot.r, Plot.g, Plot.b, flag ? (0.4f + 0.15f * num) : 0.16f));
						Outline(r, Mathf.Max(1f, Mathf.Round(0.7f * p)), new Color(Plot.r, Plot.g, Plot.b, flag ? 1f : 0.7f));
					}
				}
			}
			float num3 = Mathf.Round(Mathf.Clamp(ppt * 4f, 10f * p, 17f * p));
			foreach (SoldierMap.Town town in map.Towns)
			{
				Vector2 s3 = S(town.P);
				if (!In(s3, 60f * p))
				{
					continue;
				}
				Rect r2 = new Rect(Mathf.Round(s3.x - num3 * 0.4f), Mathf.Round(s3.y - num3 * 0.85f), Mathf.Round(num3 * 0.8f), num3);
				if (town.Home)
				{
					float num4 = num3 * 1.6f;
					GUI.color = new Color(0.45f, 1f, 0.45f, 0.4f);
					GUI.DrawTexture(new Rect(s3.x - num4 * 0.5f, r2.center.y - num4 * 0.5f, num4, num4), Skin.DiscTex);
					GUI.color = Color.white;
				}
				bool civ = false;
				try
				{
					civ = town.Owner != null && town.Owner.isCiv();
				}
				catch
				{
				}
				if (!DrawBanner(r2, town.Owner, civ, 1f))
				{
					GameArt.Flat(new Rect(s3.x - 4f * p, s3.y - 4f * p, 8f * p, 8f * p), new Color(0f, 0f, 0f, 0.7f));
					GameArt.Flat(new Rect(s3.x - 3f * p, s3.y - 3f * p, 6f * p, 6f * p), (town.Owner != null) ? Royal.ColorOf(town.Owner) : Color.white);
				}
				if (town.Capital)
				{
					GameArt.Icon(new Rect(Mathf.Round(s3.x - 5f * p), Mathf.Round(r2.y - 9f * p), 10f * p, 10f * p), GameArt.Crown);
				}
				Color color = Color.white;
				try
				{
					if (town.Owner != null)
					{
						color = town.Owner.getColor().getColorText();
					}
				}
				catch
				{
				}
				GameArt.Label(new Rect(s3.x - 60f * p, r2.yMax, 120f * p, 9f * p), town.Name + (town.Home ? " (home)" : ""), 4.3f, TextAnchor.UpperCenter, color);
			}
			Order order = map.Order;
			Vector2 g;
			bool flag2 = map.Goal(out g);
			if (order != null && (order.Kind == OrderKind.Attack || order.Kind == OrderKind.March) && flag2)
			{
				Vector2 w = Service.CityPoint(order.Home);
				try
				{
					if (map.Captain != null && map.Captain.isAlive())
					{
						w = map.Captain.current_position;
					}
				}
				catch
				{
				}
				Dashed(S(w), S(g), Mathf.Max(2f, Mathf.Round(1.4f * p)), new Color(OrderRed.r, OrderRed.g, OrderRed.b, 0.8f), 7f * p, unscaledTime * 12f * p);
			}
			if (flag2)
			{
				Dashed(S(host.current_position), S(g), Mathf.Max(1f, Mathf.Round(0.9f * p)), new Color(1f, 1f, 1f, 0.55f), 4f * p, 0f);
			}
			if (map.Captaincy && Service.Command == Service.CommandKind.March && Service.CommandTile != null)
			{
				Vector2 w2 = new Vector2((float)Service.CommandTile.x + 0.5f, (float)Service.CommandTile.y + 0.5f);
				if (Service.ArmyMiddle(host, out var at, out var _))
				{
					Dashed(S(at), S(w2), Mathf.Max(2f, Mathf.Round(1.2f * p)), ArmyGold, 6f * p, unscaledTime * 10f * p);
				}
				Vector2 c = S(w2);
				float num5 = Mathf.Round(16f * p);
				GameArt.Flat(new Rect(Mathf.Round(c.x), Mathf.Round(c.y - num5), Mathf.Max(1f, Mathf.Round(p)), num5), new Color(0.25f, 0.16f, 0.1f));
				if (!DrawBanner(new Rect(Mathf.Round(c.x + p), Mathf.Round(c.y - num5), 8f * p, 10f * p), host.kingdom, civ: true, 1f))
				{
					GameArt.Flat(new Rect(c.x + p, c.y - num5, 8f * p, 6f * p), ArmyGold);
				}
				PixelDiamond(c, 1, ArmyGold);
			}
			foreach (Vector2 clash in map.Clashes)
			{
				Vector2 s4 = S(clash);
				if (In(s4, 20f * p))
				{
					float num6 = Mathf.Round(12f * p * (1f + 0.18f * num));
					GameArt.Icon(new Rect(Mathf.Round(s4.x - num6 * 0.5f + p), Mathf.Round(s4.y - num6 * 0.5f + p), num6, num6), GameArt.Warfare, new Color(0f, 0f, 0f, 0.5f));
					GameArt.Icon(new Rect(Mathf.Round(s4.x - num6 * 0.5f), Mathf.Round(s4.y - num6 * 0.5f), num6, num6), GameArt.Warfare);
				}
			}
			Actor result = null;
			float num7 = 9f * p * 9f * p;
			float num8 = Mathf.Round(Mathf.Clamp(ppt * 1.1f, 3f * p * 0.9f, 5f * p));
			Vector2 vector2 = default(Vector2);
			bool flag3 = false;
			for (int i = 0; i < 3; i++)
			{
				foreach (SoldierMap.Dot dot in map.Dots)
				{
					int num9 = (dot.Captain ? 3 : ((dot.Army || dot.Settler) ? 2 : (dot.Ours ? 1 : 0)));
					if ((num9 != i && (i != 2 || num9 != 3)) || (dot.Captain && i != 2))
					{
						continue;
					}
					Vector2 vector3 = S(dot.P);
					if (!In(vector3, 10f * p))
					{
						continue;
					}
					float num10 = Mathf.Round(vector3.x);
					float num11 = Mathf.Round(vector3.y);
					float num12 = (dot.Captain ? (num8 + 2f * p) : num8);
					if (dot.Captain)
					{
						vector2 = new Vector2(num10, num11);
						flag3 = true;
						continue;
					}
					Color color2 = (dot.Fighting ? new Color(0.95f, 0.15f, 0.1f, 0.6f + 0.4f * num) : new Color(0.05f, 0.04f, 0.04f, 0.95f));
					Color color3 = dot.Color;
					if (num9 == 1)
					{
						color3 = Color.Lerp(color3, new Color(0.5f, 0.5f, 0.5f), 0.35f);
					}
					if (map.Captaincy && dot.Army && !dot.Fighting)
					{
						color2 = ArmyGold;
					}
					GameArt.Flat(new Rect(num10 - num12 * 0.5f - p, num11 - num12 * 0.5f - p, num12 + 2f * p, num12 + 2f * p), dot.Settler ? SiteGreen : color2);
					GameArt.Flat(new Rect(num10 - num12 * 0.5f, num11 - num12 * 0.5f, num12, num12), color3);
					if (dot.King)
					{
						GameArt.Icon(new Rect(num10 - 5f * p, num11 - num12 * 0.5f - 9f * p, 10f * p, 10f * p), GameArt.Crown);
					}
					float sqrMagnitude = (vector3 - mousePosition).sqrMagnitude;
					if (sqrMagnitude < num7)
					{
						num7 = sqrMagnitude;
						result = dot.Actor;
					}
				}
			}
			if (flag3)
			{
				float num13 = num8 + 2f * p;
				float num14 = num13 + 5f * p * (1f + 0.15f * num);
				GameArt.Flat(new Rect(vector2.x - num14 * 0.5f, vector2.y - num14 * 0.5f, num14, num14), ArmyGold);
				GameArt.Flat(new Rect(vector2.x - num13 * 0.5f - p, vector2.y - num13 * 0.5f - p, num13 + 2f * p, num13 + 2f * p), new Color(0.05f, 0.04f, 0.04f, 0.95f));
				GameArt.Flat(new Rect(vector2.x - num13 * 0.5f, vector2.y - num13 * 0.5f, num13, num13), Royal.ColorOf(host.kingdom));
				Sprite sprite = GameArt.IconNamed("iconCaptain") ?? GameArt.IconNamed("iconArmy");
				float num15 = Mathf.Round(10f * p);
				if (sprite != null)
				{
					GameArt.Icon(new Rect(vector2.x - num15 * 0.5f, vector2.y - num14 * 0.5f - num15 - p, num15, num15), sprite);
				}
				GameArt.Label(new Rect(vector2.x + num14 * 0.5f + 2f * p, vector2.y - 4f * p, 120f * p, 8f * p), "Captain " + Service.NameOf(map.Captain), 4.2f, TextAnchor.MiddleLeft, ArmyGold);
				if ((vector2 - mousePosition).sqrMagnitude < num7 * 2f)
				{
					result = map.Captain;
				}
			}
			if (flag2)
			{
				Vector2 c2 = S(g);
				if (map.Founder)
				{
					float num16 = Mathf.Round(16f * p);
					GameArt.Flat(new Rect(Mathf.Round(c2.x), Mathf.Round(c2.y - num16), Mathf.Max(1f, Mathf.Round(p)), num16), new Color(0.25f, 0.16f, 0.1f));
					if (!DrawBanner(new Rect(Mathf.Round(c2.x + p), Mathf.Round(c2.y - num16), 8f * p, 10f * p), host.kingdom, civ: true, 1f))
					{
						GameArt.Flat(new Rect(c2.x + p, c2.y - num16, 8f * p, 6f * p), SiteGreen);
					}
					PixelDiamond(c2, 1, SiteGreen);
				}
				else
				{
					Sprite s5 = OrderIcon(order);
					float num17 = Mathf.Round(14f * p * (1f + 0.15f * num));
					GameArt.Icon(new Rect(Mathf.Round(c2.x - num17 * 0.5f + p), Mathf.Round(c2.y - num17 * 0.5f + p), num17, num17), s5, new Color(0f, 0f, 0f, 0.5f));
					GameArt.Icon(new Rect(Mathf.Round(c2.x - num17 * 0.5f), Mathf.Round(c2.y - num17 * 0.5f), num17, num17), s5);
					if (!map.Towns.Exists((SoldierMap.Town t) => (t.P - g).sqrMagnitude < 64f))
					{
						GameArt.Label(new Rect(c2.x - 60f * p, c2.y + num17 * 0.5f, 120f * p, 8f * p), "Orders", 4.2f, TextAnchor.UpperCenter, OrderRed);
					}
				}
			}
			Vector2 vector4 = S(host.current_position);
			float viewYaw = _mod.ViewYaw;
			Line(vector4, vector4 + new Vector2(Mathf.Cos(viewYaw), 0f - Mathf.Sin(viewYaw)) * 14f * p, Mathf.Max(2f, p), new Color(1f, 1f, 1f, 0.95f));
			PixelDiamond(vector4, 2, Color.white);
			GUI.EndGroup();
			return result;
			bool In(Vector2 vector5, float m)
			{
				if (vector5.x > 0f - m && vector5.y > 0f - m && vector5.x < mapRect.width + m)
				{
					return vector5.y < mapRect.height + m;
				}
				return false;
			}
			Vector2 S(Vector2 arg)
			{
				return toScreen(arg) - origin;
			}
		}

		private static Sprite OrderIcon(Order o)
		{
			if (o == null)
			{
				return GameArt.IconNamed("iconSoldier") ?? GameArt.Warfare;
			}
			switch (o.Kind)
			{
			case OrderKind.Defend:
				return GameArt.IconNamed("iconShield") ?? GameArt.Warfare;
			case OrderKind.Muster:
				return GameArt.IconNamed("iconArmy") ?? GameArt.Warfare;
			case OrderKind.Patrol:
				return GameArt.IconNamed("iconSoldier") ?? GameArt.IconNamed("iconArmy") ?? GameArt.Warfare;
			case OrderKind.Hold:
				return GameArt.IconNamed("iconArrowDestination") ?? GameArt.Warfare;
			default:
				return GameArt.Warfare;
			}
		}

		private void Dashed(Vector2 a, Vector2 b, float thick, Color color, float dash, float phase)
		{
			Vector2 vector = b - a;
			float magnitude = vector.magnitude;
			if (magnitude < 1f || dash < 1f)
			{
				return;
			}
			Vector2 vector2 = vector / magnitude;
			for (float num = 0f - phase % (dash * 2f); num < magnitude; num += dash * 2f)
			{
				float num2 = Mathf.Max(0f, num);
				float num3 = Mathf.Min(magnitude, num + dash);
				if (num3 > num2)
				{
					Line(a + vector2 * num2, a + vector2 * num3, thick, color);
				}
			}
		}

		private void SmButtons(SoldierMap map, Actor host, Rect mapRect)
		{
			float p = GameArt.P;
			float num = Mathf.Round(16f * p);
			float x = mapRect.xMax - num - 4f * p;
			float num2 = mapRect.y + 4f * p;
			string tip = null;
			Rect tipAt = default(Rect);
			Rect r = new Rect(x, num2, num, num);
			Tip(r, "Zoom in");
			if (GameButton(r, "+", red: false, map.Zoom < map.ZoomMax - 0.001f, null, 7f))
			{
				SmZoomAbout(map, mapRect, mapRect.center, 1.5f);
			}
			Rect r2 = new Rect(x, num2 + num + 2f * p, num, num);
			Tip(r2, "Zoom out");
			if (GameButton(r2, "−", red: false, map.Zoom > map.ZoomMin + 0.001f, null, 7f))
			{
				SmZoomAbout(map, mapRect, mapRect.center, 2f / 3f);
			}
			float num3 = num2 + (num + 2f * p) * 2f + 3f * p;
			Rect r3 = new Rect(x, num3, num, num);
			Tip(r3, map.Founder ? "Your site" : "Your orders");
			if (GameButton(r3, null, red: false, map.Goal(out var _), map.Founder ? (GameArt.IconNamed("iconVillages") ?? GameArt.IconNamed("iconCity")) : (GameArt.IconNamed("iconArrowAttackTarget") ?? GameArt.Warfare)) && map.Goal(out var p3))
			{
				map.Centre = p3;
			}
			Rect r4 = new Rect(x, num3 + num + 2f * p, num, num);
			bool flag = !map.Founder && map.Captain != null;
			Tip(r4, map.Founder ? "Your settlers" : (flag ? "Your captain" : "No captain yet"));
			if (GameButton(r4, null, red: false, map.Founder || flag, GameArt.IconNamed("iconCaptain") ?? GameArt.IconNamed("iconArmy")))
			{
				if (flag)
				{
					try
					{
						map.Centre = map.Captain.current_position;
					}
					catch
					{
					}
				}
				else
				{
					using (List<Actor>.Enumerator enumerator = Service.SettlersOf(host).GetEnumerator())
					{
						if (enumerator.MoveNext())
						{
							Actor current = enumerator.Current;
							map.Centre = current.current_position;
						}
					}
				}
			}
			Rect r5 = new Rect(x, num3 + (num + 2f * p) * 2f, num, num);
			Tip(r5, "Centre on you");
			if (GameButton(r5, null, red: false, enabled: true, GameArt.IconNamed("iconInspect")))
			{
				map.Centre = host.current_position;
			}
			Rect r6 = new Rect(x, num3 + (num + 2f * p) * 3f, num, num);
			Tip(r6, "The whole world");
			if (GameButton(r6, null, red: false, enabled: true, GameArt.IconNamed("iconWorldInfo")))
			{
				map.Zoom = map.ZoomMin;
				map.Centre = new Vector2(WarMap.WorldWidth * 0.5f, WarMap.WorldHeight * 0.5f);
			}
			if (tip != null && Event.current.type == EventType.Repaint)
			{
				float num4 = GameArt.Measure(tip, 4.6f).x + 10f * p;
				Rect r7 = new Rect(tipAt.x - num4 - 3f * p, tipAt.y + 2f * p, num4, 12f * p);
				GameArt.TipPanel(r7);
				GameArt.Label(r7, tip, 4.6f, TextAnchor.MiddleCenter, Color.white);
			}
			void Tip(Rect rect, string text)
			{
				if (rect.Contains(Event.current.mousePosition))
				{
					tip = text;
					tipAt = rect;
				}
			}
		}

		private void SmPanKeys(SoldierMap map)
		{
			if (!_mod.IsTypingNow)
			{
				Vector2 zero = Vector2.zero;
				if (PlayInput.GetKey(KeyCode.A) || PlayInput.GetKey(KeyCode.LeftArrow))
				{
					zero.x -= 1f;
				}
				if (PlayInput.GetKey(KeyCode.D) || PlayInput.GetKey(KeyCode.RightArrow))
				{
					zero.x += 1f;
				}
				if (PlayInput.GetKey(KeyCode.W) || PlayInput.GetKey(KeyCode.UpArrow))
				{
					zero.y += 1f;
				}
				if (PlayInput.GetKey(KeyCode.S) || PlayInput.GetKey(KeyCode.DownArrow))
				{
					zero.y -= 1f;
				}
				if (!(zero == Vector2.zero))
				{
					map.Centre += zero.normalized * (map.Span / Mathf.Max(0.01f, map.Zoom)) * 0.8f * Mathf.Min(0.1f, Time.unscaledDeltaTime);
				}
			}
		}

		private static Vector2 SmClamp(Vector2 c, float viewW, float viewH)
		{
			return new Vector2(Axis(c.x, WarMap.WorldWidth, viewW), Axis(c.y, WarMap.WorldHeight, viewH));
			float Axis(float v, float world, float view)
			{
				if (!(view > world))
				{
					return Mathf.Clamp(v, 0f, world);
				}
				return Mathf.Clamp(v, world - view * 0.5f, view * 0.5f);
			}
		}

		private static void SmZoomAbout(SoldierMap map, Rect mapRect, Vector2 at, float factor)
		{
			float num = map.Span / Mathf.Max(0.01f, map.Zoom);
			float num2 = mapRect.width / num;
			float num3 = mapRect.height / num2;
			Vector2 vector = new Vector2(map.Centre.x - num * 0.5f + (at.x - mapRect.x) / num2, map.Centre.y + num3 * 0.5f - (at.y - mapRect.y) / num2);
			map.Zoom = Mathf.Clamp(map.Zoom * factor, map.ZoomMin, map.ZoomMax);
			float num4 = mapRect.width / (map.Span / map.Zoom);
			map.Centre = new Vector2(vector.x - (at.x - mapRect.center.x) / num4, vector.y + (at.y - mapRect.center.y) / num4);
		}

		private void SmInput(SoldierMap map, Actor host, Rect mapRect, Func<Vector2, Vector2> toWorld, float ppt, Event ev)
		{
			switch (ev.type)
			{
			case EventType.ScrollWheel:
				if (mapRect.Contains(ev.mousePosition))
				{
					SmZoomAbout(map, mapRect, ev.mousePosition, (ev.delta.y > 0f) ? (5f / 6f) : 1.2f);
					_smDragFrom = ev.mousePosition;
					_smCentreFrom = map.Centre;
					ev.Use();
				}
				break;
			case EventType.MouseDown:
				if (mapRect.Contains(ev.mousePosition) && ev.button <= 2)
				{
					_smButton = ev.button;
					_smDragFrom = ev.mousePosition;
					_smCentreFrom = map.Centre;
					_smDragged = false;
					ev.Use();
				}
				break;
			case EventType.MouseDrag:
				if (_smButton >= 0)
				{
					if ((ev.mousePosition - _smDragFrom).sqrMagnitude > 36f)
					{
						_smDragged = true;
					}
					if (_smDragged)
					{
						Vector2 vector2 = ev.mousePosition - _smDragFrom;
						map.Centre = _smCentreFrom - new Vector2(vector2.x / ppt, (0f - vector2.y) / ppt);
					}
					ev.Use();
				}
				break;
			case EventType.MouseUp:
			{
				if (_smButton < 0)
				{
					break;
				}
				int smButton = _smButton;
				_smButton = -1;
				if (!_smDragged && smButton == 1 && map.Captaincy)
				{
					Vector2 vector = toWorld(ev.mousePosition);
					WorldTile worldTile = TileAt(vector);
					if (worldTile != null)
					{
						Command(host, Service.CommandKind.March, worldTile, "Holding your mark", "Your soldiers march to your mark (" + Service.Heading(host.current_position, vector) + ").");
					}
				}
				else if (!_smDragged && smButton == 1 && map.Founder)
				{
					TileZone tileZone = map.SiteNear(toWorld(ev.mousePosition));
					if (tileZone != null && Service.MoveSite(host, tileZone))
					{
						Sounds.PlayUI("event:/SFX/Ui/Click");
						_mod.Toast("Your site is moved: " + Service.Heading(host.current_position, new Vector2((float)tileZone.centerTile.x + 0.5f, (float)tileZone.centerTile.y + 0.5f)) + ".");
					}
				}
				_smDragged = false;
				ev.Use();
				break;
			}
			case EventType.MouseMove:
			case EventType.KeyDown:
			case EventType.KeyUp:
				break;
			}
		}

		private static float Para(float x, float y, float w, string text, float size, Color color)
		{
			float num = Lang.Height(GameArt.Style(size, TextAnchor.UpperLeft, wrap: true), text, w);
			GameArt.Label(new Rect(x, y, w, num), text, size, TextAnchor.UpperLeft, color, wrap: true);
			return num;
		}

		private void SmSoldierPanel(SoldierMap map, Actor host, Rect panel)
		{
			float p = GameArt.P;
			float x = panel.x;
			float width = panel.width;
			float y = panel.y;
			bool flag = Event.current.type == EventType.Repaint;
			Order order = map.Order;
			City city = null;
			try
			{
				city = host.city;
			}
			catch
			{
			}
			bool captaincy = map.Captaincy;
			float num = Mathf.Round(28f * p);
			if (flag)
			{
				KingdomCard(new Rect(x, y, width, num), host.kingdom, (captaincy ? "Captain of " : "Soldier of ") + ((city != null) ? city.name : "?"), picked: false, captaincy ? ArmyGold : new Color(0.6f, 1f, 0.55f));
			}
			y += num + 4f * p;
			if (order == null)
			{
				GameArt.Label(new Rect(x + 2f * p, y, width - 4f * p, 20f * p), "Waiting for orders...", 5f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
				return;
			}
			GameArt.Label(new Rect(x + 2f * p, y, width - 4f * p, 8f * p), "Orders from " + order.From, 4.3f, TextAnchor.MiddleLeft, GameArt.Grey);
			y += 9f * p;
			GameArt.Icon(new Rect(x + 1f * p, y + 1f * p, 11f * p, 11f * p), OrderIcon(order));
			GameArt.Label(new Rect(x + 15f * p, y, width - 16f * p, 13f * p), order.Title, 6f, TextAnchor.MiddleLeft, (order.Kind == OrderKind.Patrol) ? GameArt.Peach : GameArt.Orange, wrap: true);
			y += 15f * p;
			y += Para(x + 2f * p, y, width - 4f * p, order.Detail, 4.3f, Color.white) + 2f * p;
			if (order.HasWhere)
			{
				GameArt.Label(new Rect(x + 2f * p, y, width - 4f * p, 9f * p), "→ " + Service.Heading(host.current_position, order.Where), 4.8f, TextAnchor.MiddleLeft, GameArt.Peach);
				y += 10f * p;
			}
			SectionTitle(new Rect(x, y, width, Mathf.Round(20f * p)), captaincy ? "Your soldiers" : "Your army", GameArt.IconNamed(captaincy ? "iconCaptain" : "iconArmy") ?? GameArt.IconNamed("iconArmy"));
			y += 22f * p;
			Sprite sprite = GameArt.IconNamed("iconCaptain") ?? GameArt.IconNamed("iconArmy");
			float num2 = Mathf.Round((width - 3f * p) * 0.5f);
			float num3 = Mathf.Round(15f * p);
			if (captaincy)
			{
				int count = Service.ArmyCached(host).Count;
				string text = ((Service.Command == Service.CommandKind.Follow) ? "following you" : Service.CommandLabel.ToLowerInvariant());
				if (sprite != null)
				{
					GameArt.Icon(new Rect(x + 1f * p, y, 9f * p, 9f * p), sprite);
				}
				GameArt.Label(new Rect(x + 12f * p, y - 0.5f * p, width - 13f * p, 10f * p), count + ((count == 1) ? " soldier, " : " soldiers, ") + text, 4.4f, TextAnchor.MiddleLeft, ArmyGold);
				y += 11f * p;
				if (GameButton(new Rect(x, y, num2, num3), "Follow me", Service.Command == Service.CommandKind.Follow, count > 0, null, 4.4f))
				{
					Command(host, Service.CommandKind.Follow, null, "Following you", "Your soldiers fall in behind you.");
				}
				if (GameButton(new Rect(x + num2 + 3f * p, y, num2, num3), "Charge!", red: false, count > 0 && order.HasWhere && order.Kind != OrderKind.Patrol, null, 4.4f))
				{
					Command(host, Service.CommandKind.March, TileAt(order.Where), "Charging the target", "Your soldiers charge: " + order.Title.ToLowerInvariant() + "!");
				}
				y += num3 + 3f * p;
				if (GameButton(new Rect(x, y, num2, num3), "Hold here", red: false, count > 0, null, 4.4f))
				{
					Command(host, Service.CommandKind.March, TileAt(host.current_position), "Holding where you stood", "Your soldiers hold this ground.");
				}
				if (GameButton(new Rect(x + num2 + 3f * p, y, num2, num3), "Show orders", red: false, order.HasWhere, null, 4.4f))
				{
					map.Centre = order.Where;
				}
				y += num3 + 4f * p;
				y += Para(x + 2f * p, y, width - 4f * p, "Right-click the map: your soldiers march there and hold it.", 3.9f, GameArt.Grey) + 3f * p;
			}
			else
			{
				string text2 = ((map.Captain != null) ? ("Captain " + Service.NameOf(map.Captain) + " · " + Service.Heading(host.current_position, map.Captain.current_position)) : "No captain yet.");
				if (sprite != null)
				{
					GameArt.Icon(new Rect(x + 1f * p, y, 9f * p, 9f * p), sprite);
				}
				GameArt.Label(new Rect(x + 12f * p, y - 0.5f * p, width - 13f * p, 10f * p), text2, 4.4f, TextAnchor.MiddleLeft, ArmyGold);
				y += 11f * p;
				GameArt.Label(new Rect(x + 2f * p, y, width - 4f * p, 9f * p), map.Ours + " soldiers of " + ((city != null) ? city.name : "your town") + ((map.Foes > 0) ? ("  ·  " + map.Foes + " foes") : ""), 4.4f, TextAnchor.MiddleLeft, Color.white);
				y += 10f * p;
				float num4 = Service.Merit(host);
				GameArt.Label(new Rect(x + 2f * p, y, width * 0.45f, 8f * p), "To captain", 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
				if (flag)
				{
					GameArt.Bar(new Rect(x + width * 0.42f, y + 1.5f * p, width * 0.56f, 5f * p), Mathf.Clamp01(num4 / 3f), ArmyGold);
				}
				y += 10f * p;
				float num5 = Service.YearsServed(host);
				string text3 = ((num5 >= 1f) ? ("Served " + Mathf.FloorToInt(num5) + ((Mathf.FloorToInt(num5) == 1) ? " year" : " years") + "  ·  ") : "");
				int num6 = Service.Kills(host);
				GameArt.Label(new Rect(x + 2f * p, y, width - 4f * p, 9f * p), text3 + ((num6 > 0) ? (num6 + " felled  ·  ") : "") + "pay " + 4 + " gold a year", 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
				y += 11f * p;
				if (GameButton(new Rect(x, y, num2, num3), "Show orders", red: true, order.HasWhere, null, 4.4f))
				{
					map.Centre = order.Where;
				}
				if (GameButton(new Rect(x + num2 + 3f * p, y, num2, num3), "Find captain", red: false, map.Captain != null, null, 4.4f))
				{
					try
					{
						map.Centre = map.Captain.current_position;
					}
					catch
					{
					}
				}
				y += num3 + 5f * p;
			}
			if (flag && !(panel.yMax - y < 40f * p))
			{
				SmKey(new Rect(x, y, width, Mathf.Round(38f * p)), host, founder: false, captaincy);
			}
		}

		private void Command(Actor host, Service.CommandKind kind, WorldTile tile, string label, string said)
		{
			int num = Service.CommandArmy(host, kind, tile, label);
			_mod.Toast((num > 0) ? said : "You have no soldiers to command.");
			if (num > 0 && kind == Service.CommandKind.March)
			{
				_mod.PlayHorn();
			}
		}

		private static WorldTile TileAt(Vector2 p)
		{
			try
			{
				return World.world.GetTile((int)p.x, (int)p.y);
			}
			catch
			{
				return null;
			}
		}

		private void SmFounderPanel(SoldierMap map, Actor host, Rect panel)
		{
			float p = GameArt.P;
			float x = panel.x;
			float width = panel.width;
			float y = panel.y;
			bool flag = Event.current.type == EventType.Repaint;
			Commission commission = map.Commission;
			float num = Mathf.Round(28f * p);
			if (flag)
			{
				KingdomCard(new Rect(x, y, width, num), host.kingdom, "Founder for " + Royal.NameOf(host.kingdom), picked: false, SiteGreen);
			}
			y += num + 4f * p;
			if (commission == null)
			{
				return;
			}
			bool flag2 = Service.FoundableHere(host) != null;
			GameArt.Label(new Rect(x + 2f * p, y, width - 4f * p, 8f * p), Renames.Apply("Charged by " + commission.Giver + " of " + commission.HomeName), 4.3f, TextAnchor.MiddleLeft, GameArt.Grey);
			y += 9f * p;
			Sprite sprite = GameArt.IconNamed("iconVillages") ?? GameArt.IconNamed("iconCity");
			if (sprite != null)
			{
				GameArt.Icon(new Rect(x + 1f * p, y + 1f * p, 11f * p, 11f * p), sprite);
			}
			GameArt.Label(new Rect(x + 15f * p, y, width - 16f * p, 13f * p), "Found a village", 6f, TextAnchor.MiddleLeft, GameArt.Orange);
			y += 15f * p;
			y += Para(x + 2f * p, y, width - 4f * p, "Walk to the flag, or onto any outlined free land. Stand on it and press E to raise the village. You'll lead it.", 4.3f, Color.white) + 3f * p;
			GameArt.Label(new Rect(x + 2f * p, y, width - 4f * p, 9f * p), "→ " + Service.Heading(host.current_position, commission.Site), 4.8f, TextAnchor.MiddleLeft, GameArt.Peach);
			y += 11f * p;
			GameArt.Label(new Rect(x + 2f * p, y, width - 4f * p, 9f * p), flag2 ? "Good land here: press E!" : "Not here: find outlined land.", 4.8f, TextAnchor.MiddleLeft, flag2 ? SiteGreen : GameArt.Grey);
			y += 12f * p;
			SectionTitle(new Rect(x, y, width, Mathf.Round(20f * p)), "Your family", GameArt.IconNamed("iconCity"));
			y += 22f * p;
			List<Actor> list = Service.SettlersOf(host);
			if (list.Count == 0)
			{
				GameArt.Label(new Rect(x + 2f * p, y, width - 4f * p, 9f * p), "No family here: you go alone.", 4.4f, TextAnchor.MiddleLeft, GameArt.Grey);
				y += 10f * p;
			}
			foreach (Actor item in list)
			{
				float magnitude = (item.current_position - host.current_position).magnitude;
				string text = ((magnitude < 8f) ? "with you" : (Mathf.RoundToInt(magnitude) + " tiles behind"));
				string text2 = Service.Relation(host, item);
				GameArt.Label(new Rect(x + 2f * p, y, width - 4f * p, 9f * p), Service.NameOf(item) + ((text2 != null) ? (" (" + text2 + ")") : "") + "  ·  " + text, 4.4f, TextAnchor.MiddleLeft, Color.white);
				y += 10f * p;
			}
			y += 3f * p;
			if (flag)
			{
				SmKey(new Rect(x, y, width, Mathf.Round(28f * p)), host, founder: true, captain: false);
				y += 32f * p;
				GameArt.Label(new Rect(x + 2f * p, y, width - 4f * p, panel.yMax - y), "Right-click outlined land to make it your site. Drag or WASD to move, the wheel to zoom.", 3.9f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
			}
		}

		private void SmKey(Rect key, Actor host, bool founder, bool captain)
		{
			float k = GameArt.P;
			GameArt.Sliced(key, GameArt.Inner, new Color(1f, 1f, 1f, 0.6f));
			float kx = key.x + 6f * k;
			float ky = key.y + 4f * k;
			float col = (key.width - 12f * k) * 0.5f;
			float row = 10f * k;
			float sq = Mathf.Round(4f * k);
			Color c = Royal.ColorOf(host.kingdom);
			Color edge = new Color(0.05f, 0.04f, 0.04f, 0.95f);
			if (founder)
			{
				Rect r = new Rect(kx, ky + 1f * k, 7f * k, 7f * k);
				GameArt.Flat(r, new Color(Plot.r, Plot.g, Plot.b, 0.3f));
				Outline(r, Mathf.Max(1f, Mathf.Round(0.7f * k)), Plot);
				Entry(0, "Free land");
				PixelDiamond(new Vector2(kx + col + 3f * k, ky + 4f * k), 1, SiteGreen);
				Entry(1, "Your site");
				Sq(kx + 1f * k, ky + row + 3f * k, c, SiteGreen);
				Entry(2, "Family");
				PixelDiamond(new Vector2(kx + col + 3f * k, ky + row + 4f * k), 1, Color.white);
				Entry(3, "You");
				return;
			}
			Sq(kx + 1f * k, ky + 3f * k, c, edge);
			Entry(0, captain ? "Your soldiers" : "Your army");
			if (captain)
			{
				DrawBanner(new Rect(kx + col, ky, 6f * k, 8f * k), host.kingdom, civ: true, 1f);
				Entry(1, "Your command");
			}
			else
			{
				GameArt.Flat(new Rect(kx + col - 1f * k, ky + 1f * k, sq + 6f * k, sq + 6f * k), ArmyGold);
				Sq(kx + col + 2f * k, ky + 4f * k, c, edge);
				Entry(1, "Captain");
			}
			Sq(kx + 1f * k, ky + row + 3f * k, new Color(0.85f, 0.2f, 0.15f), edge);
			Entry(2, "Foes");
			GameArt.Icon(new Rect(kx + col - 1f * k, ky + row, 9f * k, 9f * k), GameArt.Warfare);
			Entry(3, "Orders, battle");
			Line(new Vector2(kx - 1f * k, ky + row * 2f + 4f * k), new Vector2(kx + 7f * k, ky + row * 2f + 4f * k), Mathf.Max(2f, k), OrderRed);
			Entry(4, "The march");
			PixelDiamond(new Vector2(kx + col + 3f * k, ky + row * 2f + 4f * k), 1, Color.white);
			Entry(5, "You");
			void Entry(int i, string label)
			{
				float x = kx + (float)(i % 2) * col + 9f * k;
				float num = ky + (float)(i / 2) * row;
				GameArt.Label(new Rect(x, num - 1f * k, col - 9f * k, row), label, 4.2f, TextAnchor.MiddleLeft, Color.white);
			}
			void Sq(float px, float py, Color c3, Color c2)
			{
				GameArt.Flat(new Rect(px - k, py - k, sq + 2f * k, sq + 2f * k), c2);
				GameArt.Flat(new Rect(px, py, sq, sq), c3);
			}
		}

		internal float DrawServiceCard(float x, float y, float w, Actor host)
		{
			if (!_mod.SoldierMap.Open && !_mod.Interior.Inside)
			{
				DrawOrderPin(Screen.width, Screen.height);
			}
			Order orders = _mod.Orders;
			Commission charge = _mod.Charge;
			if (orders == null && charge == null)
			{
				return y;
			}
			float p = GameArt.P;
			Rect r = new Rect(x, y, w, Mathf.Round(20f * p));
			GameArt.TipPanel(r, 0.92f);
			Color white = Color.white;
			Color white2 = Color.white;
			Sprite sprite;
			string text;
			string text2;
			if (orders != null)
			{
				sprite = OrderIcon(orders);
				text = orders.Title;
				white2 = ((orders.Kind == OrderKind.Attack || orders.Kind == OrderKind.March || orders.Kind == OrderKind.Defend) ? new Color(1f, 0.55f, 0.45f) : Color.white);
				text2 = orders.From + (orders.HasWhere ? ("  ·  " + Service.Heading(host.current_position, orders.Where)) : "") + "  ·  M: map";
			}
			else
			{
				sprite = GameArt.IconNamed("iconVillages") ?? GameArt.IconNamed("iconCity");
				bool num = Service.FoundableHere(host) != null;
				text = (num ? "Found a village: press E" : "Found a village");
				white2 = (num ? SiteGreen : Color.white);
				text2 = Service.Heading(host.current_position, charge.Site) + "  ·  E on free land  ·  M: map";
			}
			if (sprite != null)
			{
				GameArt.Icon(new Rect(r.x + 3f * p, r.y + 3f * p, 11f * p, 11f * p), sprite, white);
			}
			GameArt.Label(new Rect(r.x + 17f * p, r.y + 1.5f * p, w - 21f * p, 9f * p), text, 5f, TextAnchor.MiddleLeft, white2);
			GameArt.Label(new Rect(r.x + 17f * p, r.y + 10f * p, w - 21f * p, 8f * p), text2, 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
			return y + r.height + 2f * p;
		}

		internal void DrawOrderPin(float sw, float sh)
		{
			Actor actor = _mod.Body ?? _mod.Host;
			if (actor == null)
			{
				return;
			}
			Order orders = _mod.Orders;
			Commission charge = _mod.Charge;
			Vector2 vector;
			Sprite sprite;
			Color color;
			if (orders != null && orders.HasWhere)
			{
				vector = orders.Where;
				if (orders.Kind == OrderKind.Muster && (vector - actor.current_position).sqrMagnitude < 625f)
				{
					return;
				}
				sprite = OrderIcon(orders);
				color = ((orders.Kind == OrderKind.Patrol) ? GameArt.Peach : ((orders.Kind == OrderKind.Muster) ? ArmyGold : OrderRed));
			}
			else
			{
				if (charge == null)
				{
					return;
				}
				vector = charge.Site;
				sprite = GameArt.IconNamed("iconVillages") ?? GameArt.IconNamed("iconCity");
				color = SiteGreen;
			}
			float magnitude = (vector - actor.current_position).magnitude;
			if (magnitude < ((orders != null && orders.HasWhere && orders.Kind == OrderKind.Patrol) ? 1.5f : 6f) || sprite == null)
			{
				return;
			}
			GameArt.Ensure();
			FrameProjection shown = _mod.Shown;
			if (shown.Width > 0 && shown.Height > 0)
			{
				float p = GameArt.P;
				float num = Mathf.Round(11f * p);
				float num2 = Mathf.Round(16f * p);
				float num3 = 0f;
				try
				{
					num3 = _mod.Land.SampleHeight(vector.x, vector.y);
				}
				catch
				{
				}
				float col;
				float row;
				float depth;
				bool num4 = shown.Project(vector.x, vector.y, num3 + 1.2f, out col, out row, out depth);
				float num5 = col / (float)shown.Width * sw;
				float num6 = row / (float)shown.Height * sh;
				if (num4 && num5 > 6f * p && num5 < sw - 6f * p && num6 > num2 + num && num6 < sh - 6f * p)
				{
					Vector2 vector2 = new Vector2(Mathf.Round(num5), Mathf.Round(num6 - num2));
					float num7 = Mathf.Max(1f, Mathf.Round(0.8f * p));
					GameArt.Flat(new Rect(vector2.x - num7 * 0.5f, vector2.y + num * 0.6f, num7, Mathf.Max(0f, num6 - vector2.y - num * 0.6f)), new Color(color.r, color.g, color.b, 0.8f));
					GameArt.Icon(new Rect(vector2.x - num * 0.5f + p, vector2.y - num * 0.5f + p, num, num), sprite, new Color(0f, 0f, 0f, 0.5f));
					GameArt.Icon(new Rect(vector2.x - num * 0.5f, vector2.y - num * 0.5f, num, num), sprite);
					GameArt.Label(new Rect(vector2.x - 30f * p, vector2.y - num - 9f * p, 60f * p, 8f * p), Mathf.RoundToInt(magnitude).ToString(), 4.6f, TextAnchor.LowerCenter, color);
				}
				else
				{
					Vector2 vector3 = vector - actor.current_position;
					float f = Mathf.Atan2(vector3.y, vector3.x) - _mod.ViewYaw;
					float num8 = 0f - Mathf.Sin(f);
					float num9 = 0f - Mathf.Cos(f);
					float num10 = Mathf.Min(sw * 0.46f / Mathf.Max(0.01f, Mathf.Abs(num8)), sh * 0.42f / Mathf.Max(0.01f, Mathf.Abs(num9)));
					PixelDiamond(new Vector2(sw * 0.5f + num8 * num10, sh * 0.5f + num9 * num10), 2, new Color(color.r, color.g, color.b, 0.75f));
				}
			}
		}

		internal void DrawStorage()
		{
			Building house = Chest.House;
			Actor host = _mod.Host;
			if (house == null || host == null || !house.isAlive())
			{
				Chest.Close();
				return;
			}
			float p = GameArt.P;
			int num = Bag.Size(host);
			int num2 = Chest.Size(house);
			int num3 = Mathf.Max((num + 6 - 1) / 6, (num2 + 6 - 1) / 6);
			float num4 = 136f;
			bool close;
			Rect rect = GameWindow(WindowRect(num4 * 2f + 46f, 78f + (float)num3 * 23f), HomeGrowth.IsTent(house) ? "Tent storage" : "House storage", "E", out close);
			if (close)
			{
				Chest.Close();
				return;
			}
			bool shift = Event.current.shift;
			float num5 = Mathf.Round((rect.width - num4 * 2f * p) / 3f);
			Rect col = new Rect(rect.x + num5, rect.y, num4 * p, rect.height);
			Rect col2 = new Rect(col.xMax + num5, rect.y, num4 * p, rect.height);
			_storageTip = null;
			List<KeyValuePair<string, int>> list = Bag.Contents(host);
			List<Gear.Piece> list2 = Gear.Of(host);
			SectionTitle(new Rect(col.x, col.y, col.width, Mathf.Round(18f * p)), $"Your {Bag.Name(host).ToLowerInvariant()} {Bag.Used(host)}/{num}");
			int num6 = 0;
			string text = null;
			int amount = 0;
			int num7 = -1;
			bool flag = false;
			foreach (KeyValuePair<string, int> item in list)
			{
				for (int num8 = item.Value; num8 > 0; num8 -= Bag.Stack(item.Key))
				{
					int num9 = Mathf.Min(num8, Bag.Stack(item.Key));
					if (StorageCell(col, num6++, 21f, 2f, ResourceIcon(item.Key), num9, Work.ResourceName(item.Key)))
					{
						text = item.Key;
						amount = (shift ? 1 : num9);
						flag = true;
					}
				}
			}
			for (int i = 0; i < list2.Count; i++)
			{
				if (StorageCell(col, num6++, 21f, 2f, IconOf(list2[i].Asset), 0, Gear.NameOf(list2[i])))
				{
					num7 = i;
					flag = true;
				}
			}
			while (num6 < num)
			{
				StorageCell(col, num6++, 21f, 2f, null, 0, null);
			}
			List<KeyValuePair<string, int>> list3 = Chest.Resources(house);
			List<Gear.Piece> list4 = Chest.Pieces(house);
			SectionTitle(new Rect(col2.x, col2.y, col2.width, Mathf.Round(18f * p)), $"Storage {Chest.Used(house)}/{num2}");
			num6 = 0;
			foreach (KeyValuePair<string, int> item2 in list3)
			{
				for (int num10 = item2.Value; num10 > 0; num10 -= Bag.Stack(item2.Key))
				{
					int num11 = Mathf.Min(num10, Bag.Stack(item2.Key));
					if (StorageCell(col2, num6++, 21f, 2f, ResourceIcon(item2.Key), num11, Work.ResourceName(item2.Key)))
					{
						text = item2.Key;
						amount = (shift ? 1 : num11);
						flag = false;
					}
				}
			}
			for (int j = 0; j < list4.Count; j++)
			{
				if (StorageCell(col2, num6++, 21f, 2f, IconOf(list4[j].Asset), 0, Gear.NameOf(list4[j])))
				{
					num7 = j;
					flag = false;
				}
			}
			while (num6 < num2)
			{
				StorageCell(col2, num6++, 21f, 2f, null, 0, null);
			}
			GameArt.Label(new Rect(rect.x + num5, rect.yMax - 12f * p, rect.width - num5 * 2f, 11f * p), _storageTip ?? "Click moves a stack across, Shift+click moves one", 4.2f, TextAnchor.MiddleLeft, (_storageTip != null) ? Color.white : GameArt.Grey);
			if (text != null)
			{
				int num12 = (flag ? Chest.Store(host, house, text, amount) : Chest.Fetch(host, house, text, amount));
				if (num12 == 0)
				{
					_mod.Toast(flag ? "No room in storage" : ("No room in your " + Bag.Name(host).ToLowerInvariant()));
				}
				else
				{
					Log.Info(string.Format("chest: {0} {1} {2}", flag ? "stored" : "took", num12, text));
				}
			}
			else if (num7 >= 0)
			{
				if (!(flag ? Chest.StorePiece(host, house, num7) : Chest.FetchPiece(host, house, num7)))
				{
					_mod.Toast(flag ? "No room in storage" : ("No room in your " + Bag.Name(host).ToLowerInvariant()));
				}
				else
				{
					Log.Info("chest: " + (flag ? "stored" : "took") + " a piece");
				}
			}
		}

		private bool StorageCell(Rect col, int index, float cell, float gap, Sprite icon, int amount, string name)
		{
			float p = GameArt.P;
			int num = index % 6;
			int num2 = index / 6;
			Rect r = new Rect(Mathf.Round(col.x + (float)num * (cell + gap) * p), Mathf.Round(col.y + 22f * p + (float)num2 * (cell + gap) * p), Mathf.Round(cell * p), Mathf.Round(cell * p));
			bool flag = name != null;
			PortraitWell(r, null, flag ? 1f : 0.55f);
			if (!flag)
			{
				return false;
			}
			float num3 = Mathf.Round(r.width * 0.175f);
			DrawSprite(new Rect(r.x + num3, r.y + num3, r.width - num3 * 2f, r.height - num3 * 2f), icon);
			if (amount > 0)
			{
				GameArt.Label(new Rect(r.x, r.yMax - 8.5f * p, r.width - 2f * p, 8f * p), amount.ToString(), 4.4f, TextAnchor.LowerRight, Color.white);
			}
			if (r.Contains(Event.current.mousePosition))
			{
				_storageTip = name;
			}
			return Button(r, GUIContent.none, GUIStyle.none);
		}

		private void DrawTouch(float sw, float sh)
		{
			if (!TouchControls.Shown || Event.current.type != EventType.Repaint)
			{
				return;
			}
			EnsureTouchArt();
			GameArt.Ensure();
			if (!TouchControls.MenuMode && !TouchControls.MoreOpen)
			{
				DrawStick(sh);
			}
			TouchControls.Button[] buttons = TouchControls.Buttons;
			foreach (TouchControls.Button button in buttons)
			{
				if (button.Visible)
				{
					DrawTouchButton(button, sh);
				}
			}
			if (TouchControls.MoreOpen)
			{
				DrawTouchPanel(sw, sh);
			}
		}

		private void DrawStick(float h)
		{
			float stickRadius = TouchControls.StickRadius;
			float knobRadius = TouchControls.KnobRadius;
			if (TouchControls.StickHeld)
			{
				Vector2 stickBase = TouchControls.StickBase;
				Disc(stickBase, stickRadius, new Color(0f, 0f, 0f, 0.22f));
				Ring(stickBase, stickRadius, TouchEdge);
				Vector2 c = stickBase + new Vector2(0f, (0f - stickRadius) * 2.05f);
				bool lockLit = TouchControls.LockLit;
				Disc(c, knobRadius * 0.85f, lockLit ? TouchFillDown : TouchFill);
				Ring(c, knobRadius * 0.85f, lockLit ? TouchEdgeLit : TouchEdge);
				TouchText(c, knobRadius * 0.85f, "Walk", lockLit ? Color.white : TouchEdge);
				Disc(TouchControls.StickKnob, knobRadius, TouchFillDown);
				Ring(TouchControls.StickKnob, knobRadius, TouchControls.Sprint ? TouchEdgeLit : TouchEdge);
				if (TouchControls.Sprint)
				{
					TouchText(TouchControls.StickKnob, knobRadius, "Run", Color.white);
				}
			}
			else
			{
				Vector2 stickRest = TouchControls.StickRest;
				Ring(stickRest, stickRadius, new Color(TouchEdge.r, TouchEdge.g, TouchEdge.b, 0.35f));
				Disc(stickRest, knobRadius, TouchControls.AutoRun ? TouchFillDown : new Color(0f, 0f, 0f, 0.25f));
				Ring(stickRest, knobRadius, TouchControls.AutoRun ? TouchEdgeLit : new Color(TouchEdge.r, TouchEdge.g, TouchEdge.b, 0.5f));
				if (TouchControls.AutoRun)
				{
					TouchText(stickRest, knobRadius, "Walking", Color.white);
				}
			}
		}

		private void DrawTouchButton(TouchControls.Button b, float h)
		{
			bool flag = b.Lit || b.Pressed;
			Disc(b.C, b.R, b.Pressed ? TouchFillDown : TouchFill);
			Ring(b.C, b.R, flag ? TouchEdgeLit : TouchEdge);
			Sprite sprite = GameArt.IconNamed(b.Icon);
			if (sprite != null)
			{
				float num = b.R * 0.95f;
				GameArt.IconFit(new Rect(b.C.x - num * 0.5f, b.C.y - num * 0.5f - ((b == TouchControls.Attack) ? 0f : (b.R * 0.12f)), num, num), sprite, Color.white);
				if (b != TouchControls.Attack)
				{
					TouchText(b.C + new Vector2(0f, b.R * 0.52f), b.R * 0.62f, b.Name, Color.white);
				}
			}
			else
			{
				TouchText(b.C, b.R, b.Name, Color.white);
			}
		}

		private void DrawTouchPanel(float sw, float sh)
		{
			Rect panel = TouchControls.Panel;
			float num = sh * 0.025f;
			GameArt.Flat(new Rect(0f, 0f, sw, sh), new Color(0f, 0f, 0f, 0.35f));
			GameArt.Window(new Rect(panel.x - num, panel.y - num * 1.6f, panel.width + num * 2f, panel.height + num * 2.6f), Color.white);
			TouchControls.Tile[] tiles = TouchControls.Tiles;
			foreach (TouchControls.Tile tile in tiles)
			{
				Rect box = tile.Box;
				GameArt.Flat(box, new Color(0f, 0f, 0f, 0.28f));
				Sprite sprite = GameArt.IconNamed(tile.Icon);
				float num2 = box.height * 0.42f;
				if (sprite != null)
				{
					GameArt.IconFit(new Rect(box.center.x - num2 * 0.5f, box.y + box.height * 0.14f, num2, num2), sprite, Color.white);
				}
				float num3 = Mathf.Max(9f, box.height * 0.15f);
				GameArt.Label(new Rect(box.x, box.y + box.height * 0.62f, box.width, box.height * 0.3f), tile.Name, num3 / Mathf.Max(0.5f, GameArt.P), TextAnchor.MiddleCenter, Color.white);
			}
		}

		private static void TouchText(Vector2 c, float r, string text, Color color)
		{
			float num = Mathf.Max(9f, r * ((text.Length > 5) ? 0.36f : 0.44f));
			GameArt.Label(new Rect(c.x - r, c.y - r, r * 2f, r * 2f), text, num / Mathf.Max(0.5f, GameArt.P), TextAnchor.MiddleCenter, color);
		}

		private static void Disc(Vector2 c, float r, Color color)
		{
			Round(_touchDisc, c, r, color);
		}

		private static void Ring(Vector2 c, float r, Color color)
		{
			Round(_touchRing, c, r, color);
		}

		private static void Round(Texture2D tex, Vector2 c, float r, Color color)
		{
			Color color2 = GUI.color;
			GUI.color = color;
			GUI.DrawTexture(new Rect(c.x - r, c.y - r, r * 2f, r * 2f), tex, ScaleMode.StretchToFill, alphaBlend: true);
			GUI.color = color2;
		}

		private static void EnsureTouchArt()
		{
			if (_touchDisc != null && _touchRing != null)
			{
				return;
			}
			_touchDisc = new Texture2D(128, 128, TextureFormat.RGBA32, mipChain: false)
			{
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp,
				hideFlags = HideFlags.HideAndDontSave
			};
			_touchRing = new Texture2D(128, 128, TextureFormat.RGBA32, mipChain: false)
			{
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp,
				hideFlags = HideFlags.HideAndDontSave
			};
			Color32[] array = new Color32[16384];
			Color32[] array2 = new Color32[16384];
			float num = 64f;
			for (int i = 0; i < 128; i++)
			{
				for (int j = 0; j < 128; j++)
				{
					float num2 = new Vector2((float)j + 0.5f - num, (float)i + 0.5f - num).magnitude / num;
					float num3 = Mathf.Clamp01((1f - num2) * num / 1.5f);
					array[i * 128 + j] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)(num3 * 255f));
					float num4 = Mathf.Clamp01(1f - Mathf.Abs(num2 - 0.93f) * num / 3.2f) * num3;
					array2[i * 128 + j] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)(num4 * 255f));
				}
			}
			_touchDisc.SetPixels32(array);
			_touchDisc.Apply(updateMipmaps: false);
			_touchRing.SetPixels32(array2);
			_touchRing.Apply(updateMipmaps: false);
		}

		private float DrawTownBanner(float sw, float sh)
		{
			City townShown = _mod.TownShown;
			float num = _mod.TownUntil - Time.unscaledTime;
			if (townShown == null || num <= 0f)
			{
				return 0f;
			}
			Color color = Color.white;
			string name;
			string text2;
			try
			{
				if (!townShown.isAlive())
				{
					return 0f;
				}
				name = townShown.name;
				Kingdom kingdom = townShown.kingdom;
				object obj = kingdom?.name;
				if (kingdom != null)
				{
					color = Royal.ColorOf(kingdom);
				}
				bool flag = kingdom != null && kingdom.capital == townShown;
				Kingdom kingdom2 = _mod.Host?.kingdom;
				string text = ((kingdom2 == null || kingdom == null) ? null : ((kingdom2 == kingdom) ? "your kingdom" : (Royal.AtWar(kingdom2, kingdom) ? "at war with you" : (Royal.Allied(kingdom2, kingdom) ? "your ally" : null))));
				if (obj == null)
				{
					obj = "No kingdom";
				}
				text2 = (string)obj + (flag ? "  ·  capital" : "") + ((text != null) ? ("  ·  " + text) : "");
			}
			catch
			{
				return 0f;
			}
			EnsureVitals();
			float p = GameArt.P;
			float num2 = 5.5f - num;
			float num3 = Mathf.Clamp01(num2 / 0.45f) * Mathf.Clamp01(num / 1f);
			Color color2 = color;
			try
			{
				if (townShown.kingdom != null)
				{
					color2 = townShown.kingdom.getColor().getColorText();
				}
			}
			catch
			{
			}
			float value = Mathf.Max(GameArt.Measure(name, 7f).x, GameArt.Measure(text2, 4.6f).x) + 44f * p;
			value = Mathf.Round(Mathf.Clamp(value, 150f * p, sw * 0.36f));
			float num4 = TopRightEdge(sw, sh);
			if (!VrView)
			{
				value = Mathf.Round(Mathf.Min(value, num4 - (sw * 0.5f + 84f * p)));
			}
			float num5 = (1f - Settle(Mathf.Clamp01(num2 / 0.45f))) * 30f * p;
			float num6 = RepHud.BannerExtra(townShown);
			Rect r = (VrView ? new Rect(Mathf.Round((sw - value) * 0.5f), Mathf.Round(sh * 0.13f - num5), value, Mathf.Round(28f * p) + num6) : new Rect(Mathf.Round(num4 - value + num5), Mathf.Round(6f * p), value, Mathf.Round(28f * p) + num6));
			GameArt.Sliced(r, GameArt.Spectate, new Color(1f, 1f, 1f, num3));
			GameArt.Sliced(r, GameArt.ButtonBorder, new Color(1f, 1f, 1f, num3), 4f, 4f, 4f, 4f);
			Kingdom kingdom3 = null;
			try
			{
				kingdom3 = townShown.kingdom;
			}
			catch
			{
			}
			bool civ = false;
			try
			{
				civ = kingdom3?.isCiv() ?? false;
			}
			catch
			{
			}
			float num7 = r.x + 8f * p;
			if (DrawBanner(new Rect(r.x + 4f * p, r.y + 2f * p, 19f * p, 24f * p), kingdom3, civ, num3))
			{
				num7 = r.x + 27f * p;
			}
			color2.a = num3;
			GameArt.Label(new Rect(num7, r.y + 0.5f * p, r.xMax - num7 - 6f * p, 13f * p), name, 7f, TextAnchor.MiddleLeft, color2);
			GameArt.Label(new Rect(num7, r.y + 14.5f * p, r.xMax - num7 - 6f * p, 10f * p), text2, 4.6f, TextAnchor.MiddleLeft, new Color(GameArt.Grey.r, GameArt.Grey.g, GameArt.Grey.b, num3));
			if (num6 > 0f)
			{
				RepHud.DrawInBanner(new Rect(num7, r.y + 25f * p, r.xMax - num7 - 8f * p, num6), townShown, num3);
			}
			return r.yMax + 4f * p;
		}

		private static bool Button(Rect r, string text, GUIStyle style)
		{
			text = Lang.T(text);
			Font wasFont;
			FontStyle wasStyle;
			bool flag = Lang.Fit(style, text, out wasFont, out wasStyle);
			PadNav.Spot(r);
			bool num = GUI.Button(r, text, style);
			if (flag)
			{
				Lang.Restore(style, wasFont, wasStyle);
			}
			if (num)
			{
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
			return num;
		}

		private static bool Button(Rect r, GUIContent content, GUIStyle style)
		{
			string text = content.text;
			content.text = Lang.T(text);
			Font wasFont;
			FontStyle wasStyle;
			bool num = Lang.Fit(style, content.text, out wasFont, out wasStyle);
			PadNav.Spot(r);
			bool flag = GUI.Button(r, content, style);
			if (num)
			{
				Lang.Restore(style, wasFont, wasStyle);
			}
			content.text = text;
			if (flag)
			{
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
			return flag;
		}

		private void EnsureVitals()
		{
			Skin.Ensure();
			GameArt.Ensure();
			if (_flameTex[0] == null)
			{
				Color32 color = new Color32(0, 0, 0, 0);
				for (int i = 0; i < 12; i++)
				{
					SpriteImage spriteImage = Flames.Frame(i % 3, i);
					Texture2D texture2D = new Texture2D(spriteImage.Width, spriteImage.Height, TextureFormat.RGBA32, mipChain: false)
					{
						filterMode = FilterMode.Point,
						wrapMode = TextureWrapMode.Clamp
					};
					Color32[] array = new Color32[spriteImage.Width * spriteImage.Height];
					for (int j = 0; j < array.Length; j++)
					{
						uint num = spriteImage.Pixels[j];
						array[j] = ((num >> 24 >= 128) ? new Color32((byte)(num & 0xFF), (byte)((num >> 8) & 0xFF), (byte)((num >> 16) & 0xFF), byte.MaxValue) : color);
					}
					texture2D.SetPixels32(array);
					texture2D.Apply(updateMipmaps: false);
					_flameTex[i] = texture2D;
				}
			}
			int height = Screen.height;
			if (_vitalsFor != height)
			{
				_vitalsFor = height;
				float num2 = (float)height / 1080f;
				_keyStyle = Skin.Text(15f * num2, bold: true, TextAnchor.MiddleCenter);
				_plateTitle = Skin.Text(21f * num2, bold: true, TextAnchor.MiddleLeft);
				_plateBody = Skin.Text(16f * num2);
			}
		}

		private static Texture2D Pixels(string[] rows, Func<char, int, Color32> paint)
		{
			int num = rows.Length;
			int length = rows[0].Length;
			Texture2D texture2D = new Texture2D(length, num, TextureFormat.RGBA32, mipChain: false)
			{
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp
			};
			Color32[] array = new Color32[length * num];
			for (int i = 0; i < num; i++)
			{
				for (int j = 0; j < length; j++)
				{
					array[(num - 1 - i) * length + j] = paint(rows[i][j], j);
				}
			}
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false);
			return texture2D;
		}

		private static float Ratio(Func<float> f)
		{
			try
			{
				float num = f();
				return float.IsNaN(num) ? 1f : Mathf.Clamp01(num);
			}
			catch
			{
				return 1f;
			}
		}

		private static float Ratio(Actor a, Vital v)
		{
			try
			{
				float num;
				switch (v)
				{
				default:
					num = a.getStaminaRatio();
					break;
				case Vital.Food:
					num = a.getNutritionRatio();
					break;
				case Vital.Health:
					num = a.getHealthRatio();
					break;
				}
				float num2 = num;
				return float.IsNaN(num2) ? 1f : Mathf.Clamp01(num2);
			}
			catch
			{
				return 1f;
			}
		}

		private static float Smooth(float t)
		{
			t = Mathf.Clamp01(t);
			return t * t * (3f - 2f * t);
		}

		private static float Settle(float t)
		{
			t = Mathf.Clamp01(t);
			float num = t - 1f;
			return 1f + 2.4f * num * num * num + 1.4f * num * num;
		}

		private static Rect LerpRect(Rect a, Rect b, float t)
		{
			return new Rect(Mathf.LerpUnclamped(a.x, b.x, t), Mathf.LerpUnclamped(a.y, b.y, t), Mathf.LerpUnclamped(a.width, b.width, t), Mathf.LerpUnclamped(a.height, b.height, t));
		}

		private static float Bump(float t, float length)
		{
			if (!(t < 0f) && !(t > length))
			{
				return Mathf.Sin(t / length * MathF.PI);
			}
			return 0f;
		}

		private float Entering(float hudAlpha)
		{
			int frameCount = Time.frameCount;
			float unscaledTime = Time.unscaledTime;
			hudAlpha = Mathf.Clamp01(hudAlpha);
			if (frameCount - _lastHudFrame > 3)
			{
				_lastHudAlpha = 0f;
				_enterStart = -1f;
				_leaving = false;
			}
			_lastHudFrame = frameCount;
			if (hudAlpha < _lastHudAlpha - 0.0001f)
			{
				_leaving = true;
			}
			else if (hudAlpha > _lastHudAlpha + 0.0001f || hudAlpha >= 1f)
			{
				_leaving = false;
			}
			_lastHudAlpha = hudAlpha;
			if (_leaving)
			{
				_enterStart = -1f;
				return hudAlpha;
			}
			if (hudAlpha <= 0f)
			{
				_enterStart = -1f;
				return 0f;
			}
			if (_enterStart < 0f)
			{
				_enterStart = unscaledTime;
			}
			return Mathf.Clamp01((unscaledTime - _enterStart) / 0.65f);
		}

		private void DrawMovingPanels(float sw, float sh, float e, float alpha)
		{
			Actor host = _mod.Host;
			bool flag = false;
			try
			{
				flag = host?.isAlive() ?? false;
			}
			catch
			{
			}
			if (!flag || _mod.DeathTimer >= 0f || _mod.LeavingDead || alpha <= 0f)
			{
				return;
			}
			EnsureVitals();
			_movingPass = true;
			_enter = e;
			_panelAlpha = alpha;
			try
			{
				Actor body = _mod.Body;
				DrawVitals(sw, sh, body);
				DrawCompass(sw, sh, _mod.ViewYaw, _mod.CompassMarks());
				if (e > 0f && !_mod.Craft.Open && !_mod.Royal.Open && !_mod.Royal.ObeliskOpen)
				{
					DrawNameplate(sw, sh, body, _mod.Feedback);
				}
			}
			finally
			{
				_movingPass = false;
				_enter = 1f;
				_panelAlpha = 1f;
			}
		}

		private void DrawVitals(float sw, float sh, Actor host)
		{
			EnsureVitals();
			if (!PanelsNow)
			{
				return;
			}
			if (host != _vitalsHost)
			{
				_vitalsHost = host;
				_lastHealth = (_lastFood = -1f);
				_staminaMeter = (_manaMeter = (_xpMeter = default(Meter)));
				for (int i = 0; i < 10; i++)
				{
					_heartState[i] = 0;
					_heartLostAt[i] = -9f;
				}
				_statusSince.Clear();
			}
			float p = GameArt.P;
			float unscaledTime = Time.unscaledTime;
			float num = Ratio(host, Vital.Health);
			float num2 = Ratio(host, Vital.Food);
			float num3 = Ratio(host, Vital.Stamina);
			int num4 = 0;
			int num5 = 0;
			int num6 = 0;
			int a = 0;
			try
			{
				num4 = host.getMaxMana();
				num5 = host.getMana();
			}
			catch
			{
			}
			try
			{
				num6 = host.getMaxStamina();
				a = host.getStamina();
			}
			catch
			{
			}
			bool flag = true;
			try
			{
				flag = host.getMaxNutrition() > 0;
			}
			catch
			{
			}
			if (_lastHealth >= 0f && num < _lastHealth - 0.001f)
			{
				_heartFlashUntil = unscaledTime + 0.16f;
			}
			if (_lastHealth >= 0f && num > _lastHealth + 0.001f && unscaledTime - _regenWave > 0.7f)
			{
				_regenWave = unscaledTime;
			}
			if (_lastFood >= 0f && num2 > _lastFood + 0.04f)
			{
				_foodWave = unscaledTime;
			}
			_lastFood = num2;
			if (unscaledTime > _glintNext)
			{
				_glintAt = unscaledTime;
				_glintNext = unscaledTime + 7f + (float)(_shakeRandom.NextDouble() * 4.0);
			}
			int num7 = ((!flag) ? 1 : 2);
			float num8 = 119f;
			float num9 = (float)num7 * num8 + (float)(num7 - 1) * 12f;
			float num10 = Mathf.Round((num9 + 32f) * p);
			float num11 = Mathf.Round(37f * p);
			Rect rect = new Rect(Mathf.Round((sw - num10) * 0.5f), sh - num11, num10, num11);
			float enter = _enter;
			float panelAlpha = _panelAlpha;
			Rect rect2 = rect;
			float num12 = 0f;
			if (enter < 1f)
			{
				Rect r;
				bool num13 = GameArt.GamePossessionRect(out r);
				Rect r2;
				Rect a2 = (num13 ? r : (GameArt.GameBarRect(out r2) ? r2 : new Rect(rect.x, sh + 6f * p, rect.width, rect.height)));
				rect2 = LerpRect(a2, rect, (a2.y >= sh) ? Settle(enter) : Smooth(enter));
				if (num13)
				{
					num12 = 1f - Smooth(Mathf.InverseLerp(0.05f, 0.6f, enter));
				}
			}
			if (num12 < 0.99f)
			{
				GameArt.BarSlab(rect2, panelAlpha);
			}
			if (num12 > 0.01f)
			{
				DrawPanelLookalike(rect2, host, num, panelAlpha * num12);
			}
			float num14 = panelAlpha * ((enter >= 1f) ? 1f : Smooth(Mathf.InverseLerp(0.4f, 1f, enter)));
			_vitalsRect = rect2;
			VitalsTop = rect2.y;
			_vitalsAlpha = num14;
			float num15 = Mathf.Round(rect2.center.x - rect.center.x);
			float num16 = Mathf.Round(rect2.y - rect.y);
			float num17 = rect.x + 16f * p + num15;
			float num18 = rect.y + 11f * p + num16;
			if (num14 > 0.01f)
			{
				DrawHearts(num17, num18, num, num14);
				DrawHealing(num17, num18, rect2, host, num, num14);
				if (flag)
				{
					DrawFood(num17 + (num8 + 12f) * p, num18, num2, num14);
				}
				float y = num18 + 13f * p;
				bool flag2 = num4 > 0;
				Step(ref _staminaMeter, num3);
				Step(ref _manaMeter, flag2 ? Mathf.Clamp01((float)num5 / (float)num4) : 0f);
				Color color = ((num3 < 0.15f) ? Color.Lerp(new Color(0.55f, 0.82f, 0.22f), new Color(0.95f, 0.45f, 0.2f), 0.5f + 0.5f * Mathf.Sin(unscaledTime * 9f)) : new Color(0.55f, 0.82f, 0.22f));
				string text = ((num6 > 0) ? _staminaText.Of(a, num6) : null);
				if (flag2)
				{
					float num19 = ((num7 == 2) ? num8 : ((num9 - 6f) * 0.5f));
					MeterBar(new Rect(num17, y, num19 * p, 8f * p), GameArt.Stamina, GameArt.StaminaPart, _staminaMeter, color, text, num14);
					MeterBar(new Rect(num17 + (num9 - num19) * p, y, num19 * p, 8f * p), GameArt.Mana, GameArt.ManaPart, _manaMeter, new Color(0.27f, 0.56f, 1f), _manaText.Of(num5, num4), num14);
				}
				else
				{
					MeterBar(new Rect(num17, y, num9 * p, 8f * p), GameArt.Stamina, GameArt.StaminaPart, _staminaMeter, color, text, num14);
				}
			}
			_lastHealth = num;
			DrawHull(rect2, num14);
			DrawMenuButtons(sw, sh, host, enter, num14);
			DrawAbility(sw, sh, _mod.Host);
		}

		private void DrawPanelLookalike(Rect r, Actor host, float health, float alpha)
		{
			float p = GameArt.P;
			GameArt.Sliced(r, GameArt.Spectate, new Color(1f, 1f, 1f, alpha));
			GameArt.Sliced(r, GameArt.ButtonBorder, new Color(1f, 1f, 1f, alpha), 4f, 4f, 4f, 4f);
			string text = "";
			try
			{
				text = host.getName();
			}
			catch
			{
			}
			Color color = new Color(1f, 0.286f, 0.11f);
			try
			{
				if (host.kingdom != null && host.kingdom.isCiv())
				{
					color = host.kingdom.getColor().getColorText();
				}
			}
			catch
			{
			}
			color.a = alpha;
			float num = Mathf.Clamp(r.height / (33f * p), 0.6f, 1.6f);
			GameArt.Label(new Rect(r.x, r.y + r.height * 0.06f, r.width, r.height * 0.45f), text, 6f * num, TextAnchor.MiddleCenter, color);
			float num2 = Mathf.Min(r.width * 0.45f, 92f * p * num);
			float f = 8f * p * num;
			Rect r2 = new Rect(Mathf.Round(r.center.x - num2 * 0.5f + 5f * p * num), Mathf.Round(r.y + r.height * 0.62f), Mathf.Round(num2), Mathf.Round(f));
			GameArt.Bar(r2, health, GameArt.HealthGreen, 0f, null, alpha);
			float num3 = 13f * p * num;
			float num4 = num3 * GameArt.HeartPart.width / GameArt.HeartPart.height;
			GameArt.IconPart(new Rect(Mathf.Round(r2.x - num4 * 0.6f), Mathf.Round(r2.center.y - num3 * 0.5f), num4, num3), GameArt.Heart, GameArt.HeartPart, new Color(1f, 1f, 1f, alpha));
		}

		private static void Step(ref Meter m, float v)
		{
			float unscaledTime = Time.unscaledTime;
			float num = Mathf.Min(0.1f, Time.unscaledDeltaTime);
			if (!m.Started)
			{
				m.Shown = (m.Trail = v);
				m.Started = true;
			}
			m.Shown += (v - m.Shown) * (1f - Mathf.Exp((0f - num) * 14f));
			if (v >= m.Trail)
			{
				m.Trail = v;
				m.HoldUntil = 0f;
			}
			else if (m.HoldUntil <= 0f)
			{
				m.HoldUntil = unscaledTime + 0.45f;
			}
			else if (unscaledTime > m.HoldUntil)
			{
				m.Trail = Mathf.MoveTowards(m.Trail, v, num * 0.9f);
			}
		}

		private void MeterBar(Rect r, Sprite icon, Rect part, Meter m, Color color, string text, float alpha)
		{
			float p = GameArt.P;
			GameArt.Bar(new Rect(r.x + 5f * p, r.y, r.width - 5f * p, r.height), m.Shown, color, (m.HoldUntil > 0f) ? m.Trail : 0f, text, alpha, 4.5f);
			float num = (r.height + 5f * p) / Mathf.Max(part.width, part.height);
			float num2 = part.width * num;
			float num3 = part.height * num;
			Rect r2 = new Rect(Mathf.Round(r.x + 5f * p - num2 * 0.55f), Mathf.Round(r.center.y - num3 * 0.5f), num2, num3);
			GameArt.IconPart(new Rect(r2.x + p * 0.75f, r2.y + p * 0.75f, num2, num3), icon, part, new Color(0f, 0f, 0f, 0.5f * alpha));
			GameArt.IconPart(r2, icon, part, new Color(1f, 1f, 1f, alpha));
		}

		private void DrawHearts(float x, float y, float health, float alpha)
		{
			float p = GameArt.P;
			float unscaledTime = Time.unscaledTime;
			float num = health * 10f;
			float num2 = 11f * p;
			float height = 10f * p;
			float num3 = 1f * p;
			bool flag = health < 0.3f;
			bool flag2 = unscaledTime < _heartFlashUntil;
			int num4 = -1;
			for (int i = 0; i < 10; i++)
			{
				int num5 = ((num >= (float)i + 0.75f) ? 2 : ((num >= (float)i + 0.25f) ? 1 : 0));
				if (num5 < _heartState[i] && _lastHealth >= 0f)
				{
					_heartLostAt[i] = unscaledTime;
				}
				_heartState[i] = num5;
				if (num5 > 0)
				{
					num4 = i;
				}
			}
			float num6 = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(unscaledTime * MathF.PI * 2f * 1.3f)), 8f);
			for (int j = 0; j < 10; j++)
			{
				Rect box = new Rect(x + (float)j * (num2 + num3), y, num2, height);
				float num7 = 0f;
				float scale = 1f;
				float num8 = 0f;
				if (flag)
				{
					num7 += Mathf.Round(Mathf.Sin(unscaledTime * 31f + (float)j * 1.9f) * 0.7f * p);
				}
				num7 -= Bump(unscaledTime - _regenWave - (float)j * 0.045f, 0.32f) * 2f * p;
				if (flag && j == num4)
				{
					scale = 1f + 0.28f * num6;
					num8 = 0.35f * num6;
				}
				if (flag2)
				{
					num8 = 0.85f;
				}
				else if (!flag)
				{
					num8 = Mathf.Max(num8, 0.6f * Bump(unscaledTime - _glintAt - (float)j * 0.05f, 0.22f));
				}
				Pip(box, GameArt.Heart, GameArt.HeartPart, _heartState[j], rightHalf: false, alpha, num8, scale, num7);
				float num9 = unscaledTime - _heartLostAt[j];
				if (num9 >= 0f && num9 < 0.45f)
				{
					float num10 = num9 / 0.45f;
					Pip(box, GameArt.Heart, GameArt.HeartPart, 2, rightHalf: false, alpha * (1f - num10), 1f - num10 * 0.6f, 1f + num10 * 0.8f, (0f - num10) * 8f * p, ghost: true);
				}
			}
		}

		private void DrawFood(float x, float y, float food, float alpha)
		{
			float p = GameArt.P;
			float unscaledTime = Time.unscaledTime;
			float num = food * 10f;
			float num2 = 11f * p;
			float height = 10f * p;
			float num3 = 1f * p;
			bool flag = food < 0.2f;
			for (int i = 0; i < 10; i++)
			{
				int num4 = ((num >= (float)i + 0.75f) ? 2 : ((num >= (float)i + 0.25f) ? 1 : 0));
				_foodState[i] = num4;
				Rect box = new Rect(x + (float)(9 - i) * (num2 + num3), y, num2, height);
				float num5 = 0f;
				if (flag)
				{
					num5 += Mathf.Round(Mathf.Sin(unscaledTime * 23f + (float)i * 2.3f) * 0.6f * p);
				}
				Pip(dy: num5 - Bump(unscaledTime - _foodWave - (float)i * 0.045f, 0.32f) * 2f * p, box: box, s: GameArt.Meat, part: GameArt.MeatPart, state: num4, rightHalf: true, alpha: alpha, white: 0.5f * Bump(unscaledTime - _foodWave - (float)i * 0.045f, 0.32f), scale: 1f);
			}
		}

		private static void Pip(Rect box, Sprite s, Rect part, int state, bool rightHalf, float alpha, float white, float scale, float dy, bool ghost = false)
		{
			float num = Mathf.Min(box.width / part.width, box.height / part.height) * scale;
			float num2 = part.width * num;
			float num3 = part.height * num;
			Rect rect = new Rect(Mathf.Round(box.center.x - num2 * 0.5f), Mathf.Round(box.center.y - num3 * 0.5f + dy), Mathf.Round(num2), Mathf.Round(num3));
			if (!ghost)
			{
				GameArt.IconPart(new Rect(rect.x + GameArt.P * 0.75f, rect.y + GameArt.P * 0.75f, rect.width, rect.height), s, part, new Color(0f, 0f, 0f, 0.35f * alpha));
				GameArt.IconPart(rect, s, part, new Color(0.17f, 0.12f, 0.13f, 0.85f * alpha));
			}
			if (state != 0)
			{
				Color tint = new Color(1f, 1f, 1f, alpha);
				Rect r = rect;
				Rect part2 = part;
				if (state == 1)
				{
					float num4 = Mathf.Round(rect.width * 0.5f);
					r = (rightHalf ? new Rect(rect.x + num4, rect.y, rect.width - num4, rect.height) : new Rect(rect.x, rect.y, num4, rect.height));
					part2 = (rightHalf ? new Rect(part.x + part.width * 0.5f, part.y, part.width * 0.5f, part.height) : new Rect(part.x, part.y, part.width * 0.5f, part.height));
				}
				GameArt.IconPart(r, s, part2, tint);
				if (white > 0.01f)
				{
					GameArt.Silhouette(r, s, part2, new Color(1f, 1f, 1f, Mathf.Clamp01(white) * alpha));
				}
			}
		}

		private void DrawMenuButtons(float sw, float sh, Actor host, float e, float alpha)
		{
			float p = GameArt.P;
			float num = Mathf.Round(22f * p);
			float num2 = Mathf.Round(3f * p);
			float num3 = Mathf.Round(sh - num - 4f * p + (1f - Settle(e)) * (num + 12f * p));
			float num4 = (_bagRight = Mathf.Round(sw - 6f * p));
			_bagTop = num3;
			if (alpha <= 0.01f || VrView)
			{
				_bagLeft = (_menuLeft = num4);
				return;
			}
			float num5 = KeyButton(num4, num3, num, GameArt.IconNamed("iconOptions") ?? GameArt.About, "Esc", null, alpha);
			num5 = MapButton((_bagLeft = KeyButton(num5 - num2, num3, num, GameArt.Loot, "Tab", _bagText.Of(ShownBagCount(), 24), alpha)) - num2, num3, num, alpha);
			_menuLeft = DrawPurse(num5 - num2, num3, num, host, alpha);
		}

		private static float KeyButton(float right, float y, float size, Sprite icon, string key, string count, float alpha)
		{
			float p = GameArt.P;
			Rect r = new Rect(right - size, y, size, size);
			GameArt.StoneButton(r, icon, alpha);
			key = GameArt.PadCap(key);
			GameArt.Label(new Rect(r.x + 3f * p, r.y + 2f * p, size, 7f * p), key, 5f, TextAnchor.UpperLeft, new Color(GameArt.KeyOrange.r, GameArt.KeyOrange.g, GameArt.KeyOrange.b, alpha));
			if (count != null)
			{
				GameArt.Label(new Rect(r.x, r.yMax - 9f * p, size - 3f * p, 7f * p), count, 4.5f, TextAnchor.LowerRight, new Color(1f, 1f, 1f, alpha));
			}
			return r.x;
		}

		private void DrawNameplate(float sw, float sh, Actor host, CombatFeedback fb)
		{
			EnsureVitals();
			if (!PanelsNow)
			{
				return;
			}
			float p = GameArt.P;
			float unscaledTime = Time.unscaledTime;
			string text = "";
			string text2 = null;
			Kingdom kingdom = null;
			bool flag = false;
			Color color = Color.white;
			try
			{
				text = host.getName();
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(text))
			{
				text = WorldBoxMod.Describe(host);
			}
			try
			{
				kingdom = host.kingdom;
				flag = kingdom?.isCiv() ?? false;
				if (flag)
				{
					try
					{
						color = kingdom.getColor().getColorText();
					}
					catch
					{
						color = Royal.ColorOf(kingdom);
					}
				}
				text2 = RoleText(host, flag, unscaledTime);
			}
			catch
			{
			}
			int a = 0;
			int a2 = 0;
			int a3 = 1;
			float v = 0f;
			int num = 0;
			int num2 = 1;
			try
			{
				a = host.getAge();
			}
			catch
			{
			}
			try
			{
				if (host.data != null)
				{
					a2 = host.data.kills;
					a3 = host.data.level;
					num = host.data.experience;
				}
				num2 = Mathf.Max(1, host.getExpToLevelup());
				v = Mathf.Clamp01((float)num / (float)num2);
			}
			catch
			{
			}
			float num3 = GameArt.Measure(text, 7f).x / p;
			float num4 = Mathf.Clamp(Mathf.Max(num3 + 58f, GameArt.Measure(text2 ?? "", 4.6f).x / p + 40f), 160f, 250f);
			float num5 = 40f;
			Rect r = new Rect(Mathf.Round(6f * p), Mathf.Round(6f * p), Mathf.Round(num4 * p), Mathf.Round(num5 * p));
			float enter = _enter;
			float num6 = _panelAlpha;
			if (enter < 1f)
			{
				r.x -= Mathf.Round((1f - Settle(enter)) * (r.width + 12f * p));
				num6 *= Smooth(Mathf.InverseLerp(0.2f, 0.8f, enter));
			}
			if (num6 <= 0.01f)
			{
				return;
			}
			GameArt.Sliced(r, GameArt.Spectate, new Color(1f, 1f, 1f, num6));
			GameArt.Sliced(r, GameArt.ButtonBorder, new Color(1f, 1f, 1f, num6), 4f, 4f, 4f, 4f);
			if (!DrawBanner(new Rect(r.x + 5f * p, r.y + 5f * p, 22f * p, 26f * p), kingdom, flag, num6))
			{
				Rect r2 = new Rect(r.x + 4f * p, r.y + 6f * p, 24f * p, 24f * p);
				GameArt.Sliced(r2, GameArt.Button, new Color(1f, 1f, 1f, num6), 6f, 6f, 6f, 6f, r2.width / 32f);
				Sprite s = null;
				try
				{
					s = ((host.asset != null) ? host.asset.getSpriteIcon() : null);
				}
				catch
				{
				}
				GameArt.IconFit(new Rect(r2.x + 2f * p, r2.y + 2f * p, r2.width - 4f * p, r2.height - 4f * p), s, new Color(1f, 1f, 1f, num6));
			}
			float num7 = r.x + 32f * p;
			color.a = num6;
			GameArt.Label(new Rect(num7, r.y + 1f * p, r.xMax - num7 - 22f * p, 13f * p), text, 7f, TextAnchor.MiddleLeft, color);
			float num8 = Mathf.Min(num7 + num3 * p + 3f * p, r.xMax - 21f * p);
			Sprite sprite = null;
			try
			{
				sprite = ((host.asset != null) ? host.asset.getSpriteIcon() : null);
			}
			catch
			{
			}
			if (flag && sprite != null)
			{
				GameArt.IconFit(new Rect(num8, r.y + 3f * p, 9f * p, 9f * p), sprite, new Color(1f, 1f, 1f, num6));
				num8 += 10f * p;
			}
			Sprite s2 = null;
			try
			{
				s2 = (host.isSexMale() ? GameArt.Male : (host.isSexFemale() ? GameArt.Female : null));
			}
			catch
			{
			}
			GameArt.IconFit(new Rect(num8, r.y + 3f * p, 9f * p, 9f * p), s2, new Color(1f, 1f, 1f, num6));
			if (text2 != null)
			{
				GameArt.Label(new Rect(num7, r.y + 14f * p, r.xMax - num7 - 6f * p, 9f * p), text2, 4.6f, TextAnchor.MiddleLeft, new Color(GameArt.Grey.r, GameArt.Grey.g, GameArt.Grey.b, num6));
			}
			float y = r.y + 25f * p;
			Chip(Chip(Chip(num7, y, GameArt.Age, _ageText.Of(a), num6) + 3f * p, y, GameArt.Kills, _killsText.Of(a2), num6) + 3f * p, y, GameArt.Levels, _levelText.Of(a3), num6);
			Step(ref _xpMeter, v);
			Rect r3 = new Rect(r.x + 4f * p, r.yMax + 2f * p, r.width - 8f * p, 7f * p);
			MeterBar(r3, GameArt.Experience, GameArt.XpPart, _xpMeter, GameArt.AgeYellow, _xpText.Of(num, num2), num6);
			float num9 = r3.yMax + 3f * p;
			float num10 = r.x + 4f * p;
			float num11 = 14f * p;
			_statusGone.Clear();
			foreach (string key2 in _statusSince.Keys)
			{
				_statusGone.Add(key2);
			}
			try
			{
				foreach (KeyValuePair<string, Status> item in host.getStatusesDict())
				{
					string key = item.Key;
					switch (key)
					{
					case "possessed":
					case "possessed_follower":
					case "fp_torch":
						continue;
					}
					_statusGone.Remove(key);
					if (!_statusSince.TryGetValue(key, out var value))
					{
						value = unscaledTime;
						_statusSince[key] = unscaledTime;
					}
					if (num10 + num11 > r.xMax + 40f * p)
					{
						break;
					}
					Rect r4 = new Rect(num10, num9, num11, num11);
					GameArt.TipPanel(r4, num6);
					Sprite s3 = null;
					try
					{
						s3 = AssetManager.status.get(key)?.getSprite();
					}
					catch
					{
					}
					float num12 = 1f + 0.35f * Bump(unscaledTime - value, 0.3f);
					float num13 = (num11 - 2f * p) * num12;
					GameArt.IconFit(new Rect(r4.center.x - num13 * 0.5f, r4.center.y - num13 * 0.5f, num13, num13), s3, new Color(1f, 1f, 1f, num6));
					Status value2 = item.Value;
					float num14 = 1f;
					try
					{
						if (value2 != null && value2.duration > 0f)
						{
							num14 = Mathf.Clamp01((float)value2.getRemainingTime() / value2.duration);
						}
					}
					catch
					{
					}
					if (num14 < 1f)
					{
						GameArt.Flat(new Rect(r4.x + p, r4.y + p, r4.width - 2f * p, (r4.height - 2f * p) * (1f - num14)), new Color(0f, 0f, 0f, 0.45f * num6));
					}
					num10 += num11 + 2f * p;
					float num15 = unscaledTime - value;
					if (num15 < 3f)
					{
						string text3 = ((key == "pregnant" || key == "pregnant_parthenogenesis") ? (Births.ExpectingLabel(host) ?? "expecting") : key.Replace('_', ' '));
						float num16 = num6 * Mathf.Clamp01((3f - num15) / 0.6f) * Mathf.Clamp01(num15 / 0.15f);
						float num17 = GameArt.Measure(text3, 4.6f).x + 8f * p;
						GameArt.TipPanel(new Rect(num10, num9 + 1f * p, num17, num11 - 2f * p), num16);
						GameArt.Label(new Rect(num10, num9 + 1f * p, num17, num11 - 2f * p), text3, 4.6f, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, num16));
						num10 += num17 + 2f * p;
					}
				}
			}
			catch
			{
			}
			foreach (string item2 in _statusGone)
			{
				_statusSince.Remove(item2);
			}
			_nameplateBottom = num9 + num11;
		}

		private string RoleText(Actor host, bool civ, float now)
		{
			if (host == _roleFor && civ == _roleCiv && now < _roleUntil)
			{
				return _role;
			}
			string result = (_role = (civ ? RoleLine(host) : SpeciesName(host)));
			_roleFor = host;
			_roleCiv = civ;
			_roleUntil = now + 0.5f;
			return result;
		}

		internal static string RoleLine(Actor host)
		{
			Kingdom kingdom = host.kingdom;
			string text = ((host.city != null) ? host.city.name : null);
			if (host.isKing())
			{
				return (Models.IsFemale(host) ? "Queen of " : "King of ") + kingdom.name + ((text != null) ? ("  ·  " + text) : "");
			}
			string text2 = Regalia.NameplateRole(host);
			if (text2 != null)
			{
				return text2;
			}
			if (host.isCityLeader())
			{
				return "Leader of " + (text ?? "a town") + "  ·  " + kingdom.name;
			}
			try
			{
				if (Service.CommissionOf(host) != null)
				{
					return "Founder for " + kingdom.name + ((text != null) ? ("  ·  " + text) : "");
				}
			}
			catch
			{
			}
			return ((text != null) ? (text + "  ·  ") : "") + kingdom.name;
		}

		private static string SpeciesName(Actor a)
		{
			try
			{
				string text = LocalizedTextManager.getText(a.asset.getLocaleID());
				if (!string.IsNullOrEmpty(text) && !text.StartsWith("$"))
				{
					return text;
				}
			}
			catch
			{
			}
			try
			{
				return a.asset.id.Replace('_', ' ');
			}
			catch
			{
				return null;
			}
		}

		private static float Chip(float x, float y, Sprite icon, string value, float alpha)
		{
			float p = GameArt.P;
			float width = Mathf.Round(GameArt.Measure(value, 5f).x + 15f * p);
			float height = Mathf.Round(11f * p);
			Rect r = new Rect(Mathf.Round(x), Mathf.Round(y), width, height);
			GameArt.Sliced(r, GameArt.InnerSmall, new Color(0.774f, 0.774f, 0.774f, 0.75f * alpha));
			Rect r2 = new Rect(r.x - 1f * p, r.y - 1f * p, 13f * p, 13f * p);
			GameArt.Icon(new Rect(r2.x + p * 0.75f, r2.y + p * 0.75f, r2.width, r2.height), icon, new Color(0f, 0f, 0f, 0.5f * alpha));
			GameArt.Icon(r2, icon, new Color(1f, 1f, 1f, alpha));
			GameArt.Label(new Rect(r.x + 12f * p, r.y - 0.5f * p, width, height), value, 5f, TextAnchor.MiddleLeft, new Color(GameArt.Orange.r, GameArt.Orange.g, GameArt.Orange.b, alpha));
			return r.xMax;
		}

		private static bool DrawBanner(Rect r, Kingdom kingdom, bool civ, float alpha)
		{
			if (!civ || kingdom == null)
			{
				return false;
			}
			Sprite sprite = null;
			Sprite sprite2 = null;
			Color tint = Color.white;
			Color tint2 = Color.white;
			try
			{
				sprite = kingdom.getElementBackground();
				sprite2 = kingdom.getElementIcon();
				ColorAsset color = kingdom.getColor();
				tint = color.getColorMainSecond();
				tint2 = color.getColorBanner();
			}
			catch
			{
			}
			if (sprite == null)
			{
				return false;
			}
			float p = GameArt.P;
			tint.a = alpha;
			tint2.a = alpha;
			if (!_bannerLogged)
			{
				_bannerLogged = true;
				Log.Info(string.Format("hud: banner {0} {1}x{2}, emblem {3}", sprite.name, sprite.rect.width, sprite.rect.height, (sprite2 != null) ? (sprite2.name + " " + sprite2.rect.width + "x" + sprite2.rect.height) : "none"));
			}
			GameArt.IconFit(new Rect(r.x + p, r.y + p, r.width, r.height), sprite, new Color(0f, 0f, 0f, 0.5f * alpha));
			GameArt.IconFit(r, sprite, tint);
			if (sprite2 != null)
			{
				GameArt.IconFit(r, sprite2, tint2);
			}
			return true;
		}

		private void DrawCompass(float sw, float sh, float yaw, List<KeyValuePair<float, Color>> marks)
		{
			EnsureVitals();
			if (!PanelsNow)
			{
				return;
			}
			float p = GameArt.P;
			float unscaledTime = Time.unscaledTime;
			Rect rect = new Rect(Mathf.Round((sw - 150f * p) * 0.5f), Mathf.Round(7f * p), Mathf.Round(150f * p), Mathf.Round(34f * p));
			float enter = _enter;
			float panelAlpha = _panelAlpha;
			Matrix4x4 matrix = GUI.matrix;
			if (enter < 1f)
			{
				Rect a;
				if (GameArt.GameAgeRect(out var r))
				{
					float num = r.width / rect.width;
					a = new Rect(r.center.x - rect.width * num * 0.5f, r.center.y - rect.height * num * 0.5f, rect.width * num, rect.height * num);
				}
				else
				{
					a = new Rect(rect.x, 0f - rect.height - 12f * p, rect.width, rect.height);
				}
				Rect rect2 = LerpRect(a, rect, (a.y > rect.y + rect.height) ? Smooth(enter) : Settle(enter));
				float num2 = rect2.width / rect.width;
				GUI.matrix = matrix * Matrix4x4.TRS(new Vector3(rect2.x - rect.x * num2, rect2.y - rect.y * num2, 0f), Quaternion.identity, new Vector3(num2, num2, 1f));
			}
			try
			{
				DrawAgePanel(rect, panelAlpha, unscaledTime);
			}
			finally
			{
				GUI.matrix = matrix;
			}
			float num3 = panelAlpha * ((enter >= 1f) ? 1f : Smooth(Mathf.InverseLerp(0.6f, 1f, enter)));
			if (num3 > 0.01f)
			{
				DrawCompassBand(new Rect(rect.x + 6f * p, rect.yMax + 3f * p, rect.width - 12f * p, 10f * p), yaw, marks, num3);
			}
		}

		private void DrawAgePanel(Rect hud, float a, float now)
		{
			float p = GameArt.P;
			WorldAgeAsset worldAgeAsset = null;
			try
			{
				worldAgeAsset = World.world_era;
			}
			catch
			{
			}
			string text = ((worldAgeAsset != null) ? worldAgeAsset.id : "");
			if (text != _ageId)
			{
				if (_ageId != null)
				{
					_ageFlashAt = now;
				}
				_ageId = text;
				_ageTitle = null;
				try
				{
					if (worldAgeAsset != null)
					{
						_ageTitle = LocalizedTextManager.getText(worldAgeAsset.getLocaleID());
					}
				}
				catch
				{
				}
				if (string.IsNullOrEmpty(_ageTitle) || _ageTitle.StartsWith("$"))
				{
					_ageTitle = AgeName(text);
				}
				_ageColor = worldAgeAsset?.title_color ?? Color.white;
			}
			int num = 0;
			int num2 = 0;
			float num3 = 0f;
			float num4 = 0f;
			try
			{
				num = Date.getCurrentYear();
				num2 = Date.getCurrentMonth();
				num3 = Mathf.Clamp01(((float)num2 + Date.getMonthTime() / 5f - 1f) / 12f);
			}
			catch
			{
			}
			try
			{
				num4 = Mathf.Clamp01(World.world.map_stats.current_age_progress);
			}
			catch
			{
			}
			if (num2 != _month || num != _year)
			{
				if (_month >= 0)
				{
					_flipAt = now;
				}
				_month = num2;
				_year = num;
				_yearText = "Year " + num;
				_monthText = null;
				try
				{
					_monthText = LocalizedTextManager.getText(AssetManager.months.getMonth(num2).getLocaleID());
				}
				catch
				{
				}
			}
			GameArt.Sliced(hud, GameArt.Spectate, new Color(1f, 1f, 1f, a));
			float num5 = Bump(now - _ageFlashAt, 1.2f);
			if (num5 > 0f)
			{
				GameArt.Sliced(hud, GameArt.WindowBar, new Color(_ageColor.r, _ageColor.g, _ageColor.b, 0.35f * num5 * a), 5f, 5f, 6f, 6f);
			}
			GameArt.Label(color: new Color(_ageColor.r, _ageColor.g, _ageColor.b, a), r: new Rect(hud.x, hud.y + 0.5f * p, hud.width, 14f * p), text: _ageTitle, size: 7f, anchor: TextAnchor.MiddleCenter);
			Sprite s = null;
			try
			{
				s = worldAgeAsset?.getSprite();
			}
			catch
			{
			}
			float num6 = 20f * p;
			for (int i = 0; i < 2; i++)
			{
				float num7 = Mathf.Round(Mathf.Sin(now * 1.6f + (float)i * 1.7f) * 0.6f * p);
				Rect r = new Rect((i == 0) ? (hud.x - 7f * p) : (hud.xMax - num6 + 7f * p), hud.y - 3f * p + num7, num6, num6);
				GameArt.Icon(new Rect(r.x + p, r.y + p, num6, num6), s, new Color(0f, 0f, 0f, 0.5f * a));
				GameArt.Icon(r, s, new Color(1f, 1f, 1f, a));
			}
			GameArt.Label(new Rect(hud.x + 15f * p, hud.y + 14.5f * p, hud.width * 0.5f, 8f * p), _yearText, 4.6f, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, a));
			GameArt.Label(new Rect(hud.center.x, hud.y + 14.5f * p, hud.width * 0.5f - 15f * p, 8f * p), Calendar.DateText ?? _monthText, 4.6f, TextAnchor.MiddleRight, new Color(0.717f, 0.717f, 0.717f, a));
			float num8 = Mathf.Repeat(_mod.Day.Hour, 1f);
			Rect r2 = new Rect(hud.x + 13f * p, hud.y + 23f * p, hud.width - 26f * p, 7f * p);
			GameArt.Sliced(r2, GameArt.DarkField, new Color(1f, 1f, 1f, 0.9f * a), 3f, 3f, 3f, 3f);
			Rect position = new Rect(r2.x + p, r2.y + p, r2.width - 2f * p, r2.height - 2f * p);
			Texture2D image = DayStrip();
			Color color = GUI.color;
			GUI.color = new Color(0.55f, 0.55f, 0.6f, a) * color;
			GUI.DrawTexture(position, image);
			GUI.color = new Color(1f, 1f, 1f, a) * color;
			float num9 = Mathf.Round(position.width * num8);
			if (num9 > 0f)
			{
				GUI.DrawTextureWithTexCoords(new Rect(position.x, position.y, num9, position.height), image, new Rect(0f, 0f, num8, 1f));
			}
			GUI.color = color;
			GameArt.Flat(new Rect(position.x, position.yMax - Mathf.Max(1f, Mathf.Round(p * 0.75f)), Mathf.Round(position.width * num3), Mathf.Max(1f, Mathf.Round(p * 0.75f))), new Color(0.3f, 0.75f, 0.9f, 0.9f * a));
			bool flag = num8 > 0.23f && num8 < 0.77f;
			float num10 = Mathf.Round(position.x + position.width * num8);
			float num11 = Mathf.Round(r2.center.y);
			float num12 = 0.5f + 0.5f * Mathf.Sin(now * 2.2f);
			Color color2 = (flag ? new Color(1f, 0.8f, 0.35f, (0.28f + 0.18f * num12) * a) : new Color(0.7f, 0.82f, 1f, (0.2f + 0.14f * num12) * a));
			float num13 = (flag ? 7f : 6f) * p * (1f + 0.08f * num12);
			Color color3 = GUI.color;
			GUI.color = color2 * color3;
			GUI.DrawTexture(new Rect(num10 - num13, num11 - num13, num13 * 2f, num13 * 2f), Skin.DiscTex);
			GUI.color = new Color(color2.r, color2.g, color2.b, color2.a * 0.6f) * color3;
			GUI.DrawTexture(new Rect(num10 - num13 * 0.65f, num11 - num13 * 0.65f, num13 * 1.3f, num13 * 1.3f), Skin.DiscTex);
			GUI.color = color3;
			PixelSprite(new Vector2(num10, num11), flag ? SunPixels : MoonPixels, Mathf.Max(1f, Mathf.Round(p * 1.1f)), a);
			float num14 = Smooth((now - _flipAt) / 0.6f) * 180f;
			for (int j = 0; j < 2; j++)
			{
				Rect r3 = new Rect((j == 0) ? (hud.x + 3f * p) : (hud.xMax - 12f * p), r2.center.y - 5.5f * p, 9f * p, 11f * p);
				Matrix4x4 matrix = GUI.matrix;
				if (num14 > 0f && num14 < 180f)
				{
					GUIUtility.RotateAroundPivot(num14, r3.center);
				}
				GameArt.IconPart(new Rect(r3.x + p * 0.75f, r3.y + p * 0.75f, r3.width, r3.height), GameArt.Clock, new Rect(7f, 3f, 15f, 21f), new Color(0f, 0f, 0f, 0.5f * a));
				GameArt.IconPart(r3, GameArt.Clock, new Rect(7f, 3f, 15f, 21f), new Color(1f, 1f, 1f, a));
				GUI.matrix = matrix;
			}
			float height = Mathf.Max(1f, Mathf.Round(1.5f * p));
			GameArt.Flat(new Rect(hud.x + 8f * p, hud.yMax - 2.5f * p, hud.width - 16f * p, height), new Color(0f, 0f, 0f, 0.35f * a));
			GameArt.Flat(new Rect(hud.x + 8f * p, hud.yMax - 2.5f * p, Mathf.Round((hud.width - 16f * p) * num4), height), new Color(GameArt.AgeYellow.r, GameArt.AgeYellow.g, GameArt.AgeYellow.b, a));
		}

		private static void PixelSprite(Vector2 c, string[] rows, float p, float alpha)
		{
			int num = rows.Length;
			int length = rows[0].Length;
			float num2 = Mathf.Round(c.x - (float)length * p * 0.5f);
			float num3 = Mathf.Round(c.y - (float)num * p * 0.5f);
			for (int i = 0; i < 2; i++)
			{
				for (int j = 0; j < num; j++)
				{
					for (int k = 0; k < length; k++)
					{
						char c2 = rows[j][k];
						if (c2 == '.')
						{
							continue;
						}
						Color color2;
						if (i != 0)
						{
							Color32 color;
							switch (c2)
							{
							default:
								color = new Color32(117, 122, 129, byte.MaxValue);
								break;
							case 'L':
								color = new Color32(211, 217, 197, byte.MaxValue);
								break;
							case 'W':
								color = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
								break;
							case 'Y':
								color = new Color32(byte.MaxValue, 236, 63, byte.MaxValue);
								break;
							case 'O':
								color = new Color32(byte.MaxValue, 141, 26, byte.MaxValue);
								break;
							}
							color2 = color;
						}
						else
						{
							color2 = new Color(0f, 0f, 0f, 0.45f * alpha);
						}
						Color c3 = color2;
						if (i == 1)
						{
							c3.a = alpha;
						}
						float num4 = ((i == 0) ? p : 0f);
						GameArt.Flat(new Rect(num2 + (float)k * p + num4, num3 + (float)j * p + num4, p, p), c3);
					}
				}
			}
		}

		private Texture2D DayStrip()
		{
			DayCycle day = _mod.Day;
			bool naturalCycle = day.NaturalCycle;
			float num = (naturalCycle ? day.Declination : 0f);
			if (_dayStrip != null && naturalCycle == _stripNatural && Mathf.Abs(num - _stripDeclination) < 0.005f)
			{
				return _dayStrip;
			}
			_stripNatural = naturalCycle;
			_stripDeclination = num;
			if (_dayStrip == null)
			{
				_dayStrip = new Texture2D(96, 1, TextureFormat.RGBA32, mipChain: false)
				{
					wrapMode = TextureWrapMode.Clamp,
					filterMode = FilterMode.Bilinear,
					hideFlags = HideFlags.HideAndDontSave
				};
			}
			Color[] array = new Color[96];
			for (int i = 0; i < 96; i++)
			{
				Atmosphere atmosphere = day.SkyAt((float)i / 95f);
				Color a = Color.Lerp(Rgb(atmosphere.Horizon), Rgb(atmosphere.Zenith), 0.45f);
				array[i] = Color.Lerp(a, new Color(0.1f, 0.13f, 0.3f), Mathf.Clamp01(atmosphere.Night) * 0.5f);
			}
			_dayStrip.SetPixels(array);
			_dayStrip.Apply(updateMipmaps: false);
			return _dayStrip;
		}

		private void DrawCompassBand(Rect r, float yaw, List<KeyValuePair<float, Color>> marks, float a)
		{
			float p = GameArt.P;
			GameArt.Sliced(r, GameArt.DarkField, new Color(1f, 1f, 1f, 0.82f * a), 3f, 3f, 3f, 3f);
			Rect rect = new Rect(r.x + 4f * p, r.y, r.width - 8f * p, r.height);
			float num = 1.7453293f;
			float current = yaw * 57.29578f;
			for (int i = 0; i < 24; i++)
			{
				float num2 = Mathf.DeltaAngle(current, (float)i * 15f) * (MathF.PI / 180f);
				if (!(Mathf.Abs(num2) > num * 0.5f))
				{
					float num3 = rect.center.x - num2 / (num * 0.5f) * (rect.width * 0.5f);
					float num4 = Mathf.Clamp01(1.4f - Mathf.Abs(num2) / (num * 0.5f) * 1.4f) * a;
					if (i % 3 == 0)
					{
						string text = Points[i / 3 % 8];
						Color color = ((text == "N") ? new Color(1f, 0.42f, 0.3f, num4) : ((text.Length == 1) ? new Color(1f, 1f, 1f, num4) : new Color(0.72f, 0.72f, 0.72f, num4)));
						GameArt.Label(new Rect(num3 - 12f * p, r.y - 0.5f * p, 24f * p, r.height), text, (text.Length == 1) ? 5f : 4.2f, TextAnchor.MiddleCenter, color);
					}
					else
					{
						GameArt.Flat(new Rect(Mathf.Round(num3), r.y + r.height * 0.4f, Mathf.Max(1f, Mathf.Round(p * 0.6f)), r.height * 0.3f), new Color(0.75f, 0.75f, 0.7f, 0.7f * num4));
					}
				}
			}
			if (marks != null)
			{
				foreach (KeyValuePair<float, Color> mark in marks)
				{
					float num5 = Mathf.DeltaAngle(current, mark.Key * 57.29578f) * (MathF.PI / 180f);
					PixelDiamond(new Vector2(rect.center.x - Mathf.Clamp(num5 / (num * 0.5f), -1f, 1f) * (rect.width * 0.5f), r.yMax), 2, new Color(mark.Value.r, mark.Value.g, mark.Value.b, a));
				}
			}
			float compassStripAngle = CompassStripAngle;
			if (!float.IsNaN(compassStripAngle))
			{
				WorldBoxMod.CompassMark? compassOn = _mod.CompassOn;
				if (compassOn.HasValue)
				{
					WorldBoxMod.CompassMark valueOrDefault = compassOn.GetValueOrDefault();
					float num6 = Mathf.DeltaAngle(current, compassStripAngle * 57.29578f) * (MathF.PI / 180f);
					float x = rect.center.x - Mathf.Clamp(num6 / (num * 0.5f), -1f, 1f) * (rect.width * 0.5f);
					PixelDiamond(new Vector2(x, r.yMax), 4, new Color(1f, 1f, 1f, a));
					PixelDiamond(new Vector2(x, r.yMax), 3, new Color(valueOrDefault.Color.r, valueOrDefault.Color.g, valueOrDefault.Color.b, a));
				}
			}
			float num7 = Mathf.Round(r.center.x);
			float num8 = Mathf.Max(1f, Mathf.Round(p));
			for (int j = 0; j < 3; j++)
			{
				GameArt.Flat(new Rect(num7 - num8 * (float)(2 - j) - num8 * 0.5f, r.y - num8 * (float)(3 - j), num8 * (float)(5 - j * 2), num8), new Color(GameArt.KeyOrange.r, GameArt.KeyOrange.g, GameArt.KeyOrange.b, a));
			}
		}

		private static void PixelDiamond(Vector2 c, int radius, Color color)
		{
			float num = Mathf.Max(1f, Mathf.Round(GameArt.P));
			for (int i = 0; i < 2; i++)
			{
				int num2 = ((i == 0) ? (radius + 1) : radius);
				Color c2 = ((i == 0) ? new Color(0f, 0f, 0f, 0.8f * color.a) : color);
				for (int j = -num2; j <= num2; j++)
				{
					int num3 = num2 - Mathf.Abs(j);
					GameArt.Flat(new Rect(Mathf.Round(c.x - num * ((float)num3 + 0.5f)), Mathf.Round(c.y + (float)j * num - num * 0.5f), num * (float)(num3 * 2 + 1), num), c2);
				}
			}
		}

		private static Color Rgb(uint c)
		{
			return new Color((float)(c & 0xFF) / 255f, (float)((c >> 8) & 0xFF) / 255f, (float)((c >> 16) & 0xFF) / 255f, 1f);
		}

		private static string AgeName(string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				return "An Unknown Age";
			}
			string text = (id.StartsWith("age_") ? id.Substring(4) : id);
			if (text.Length == 0)
			{
				return "An Unknown Age";
			}
			return "Age of " + char.ToUpperInvariant(text[0]) + text.Substring(1).Replace('_', ' ');
		}

		private void DrawOnFire(float sw, float sh)
		{
			EnsureVitals();
			int num = 9;
			float num2 = sw / (float)num;
			for (int i = 0; i < num; i++)
			{
				int num3 = ((int)(Time.unscaledTime * 14f + (float)i * 5.3f) + i * 3) % 12;
				float num4 = sh * (0.16f + 0.08f * Mathf.Sin(Time.unscaledTime * 3.1f + (float)i * 1.7f));
				float num5 = num2 * 1.5f;
				Color color = GUI.color;
				GUI.color = new Color(1f, 1f, 1f, 0.8f);
				GUI.DrawTexture(new Rect((float)i * num2 - num5 * 0.25f, sh - num4 * 0.92f, num5, num4), _flameTex[num3]);
				GUI.color = color;
			}
		}

		private void DrawPrompt(float sw, float sh, string prompt)
		{
			EnsureVitals();
			float p = GameArt.P;
			float num = 6f;
			string[] array = prompt.Split('|');
			float num2 = 9f * p;
			float num3 = 6f * p;
			float num4 = 0f;
			float[] array2 = new float[array.Length];
			for (int i = 0; i < array.Length; i++)
			{
				SplitPrompt(array[i], out var key, out var label);
				float a = Mathf.Round((num + 5f) * p);
				float num5 = ((key.Length > 0) ? (Mathf.Max(a, GameArt.Measure(key, num).x + 4f * p) + 4f * p) : 0f);
				array2[i] = num5 + GameArt.Measure(label, num).x;
				num4 += array2[i] + ((i > 0) ? num2 : 0f);
			}
			float height = Mathf.Round((num + 11f) * p);
			Rect r = new Rect(Mathf.Round((sw - num4) * 0.5f - num3), Mathf.Round(sh * 0.5f + sh * 0.08f), Mathf.Round(num4 + num3 * 2f), height);
			GameArt.TipPanel(r);
			float y = r.center.y;
			for (int j = 0; j < array.Length; j++)
			{
				SplitPrompt(array[j], out var key2, out var label2);
				float num6 = r.x + num3 + SumBefore(array2, j, num2);
				if (key2.Length > 0)
				{
					num6 += GameArt.KeyCap(num6, y, key2, num) + 4f * p;
				}
				GameArt.Label(new Rect(num6, r.y - 0.5f * p, array2[j], height), label2, num, TextAnchor.MiddleLeft, Color.white);
			}
		}

		private static float SumBefore(float[] widths, int count, float gap)
		{
			float num = 0f;
			for (int i = 0; i < count; i++)
			{
				num += widths[i] + gap;
			}
			return num;
		}

		private static void SplitPrompt(string g, out string key, out string label)
		{
			int num = g.IndexOf(':');
			if (num < 0)
			{
				key = "";
				label = g;
			}
			else
			{
				key = GameArt.VrKey(g.Substring(0, num));
				label = g.Substring(num + 1);
			}
		}

		internal void DrawVrMenu(float sw, float sh)
		{
			Event current = Event.current;
			bool flag = current.type == EventType.Repaint;
			if (flag)
			{
				VrMenuDrawn = false;
			}
			if (!VrMenu.Open)
			{
				return;
			}
			Skin.Ensure();
			GameArt.Ensure();
			float p = GameArt.P;
			int num = VrMenu.Items.Length;
			int num2 = 4;
			int num3 = (num + num2 - 1) / num2;
			float num4 = Mathf.Round(sw * 0.115f);
			float num5 = Mathf.Round(sh * 0.105f);
			float num6 = Mathf.Round(sw * 0.007f);
			float num7 = (float)num2 * num4 + (float)(num2 - 1) * num6;
			float num8 = (float)num3 * num5 + (float)(num3 - 1) * num6;
			Rect rect = new Rect(Mathf.Round((sw - num7) * 0.5f), Mathf.Round(sh * 0.45f - num8 * 0.5f), num7, num8);
			float num9 = Mathf.Round(8f * p);
			float num10 = Mathf.Round(16f * p);
			if (flag)
			{
				Rect r = new Rect(rect.x - num9, rect.y - num9 - num10, rect.width + num9 * 2f, rect.height + num9 * 2f + num10 * 2f);
				GameArt.TipPanel(r, 0.97f);
				GameArt.Label(new Rect(r.x, r.y + num9 * 0.5f, r.width, num10), "Quick menu", 8f, TextAnchor.MiddleCenter, Color.white);
				GameArt.Label(new Rect(r.x, rect.yMax + num9 * 0.5f, r.width, num10), "Point and let go of A, or pull the trigger", 6f, TextAnchor.MiddleCenter, GameArt.Grey);
			}
			int i = -1;
			float num11 = Mathf.Clamp(num5 * 0.3f / p, 5f, 11f);
			for (int j = 0; j < num; j++)
			{
				VrMenu.Item item = VrMenu.Items[j];
				Rect rect2 = new Rect(rect.x + (float)(j % num2) * (num4 + num6), rect.y + (float)(j / num2) * (num5 + num6), num4, num5);
				VrMenuRects[j] = rect2;
				if (VrMouse.Driving && rect2.Contains(current.mousePosition))
				{
					i = j;
				}
				if (flag)
				{
					bool flag2 = j == VrMenu.Choice;
					float num12 = ((item.Button != null) ? (rect2.height * 0.12f) : 0f);
					if (flag2)
					{
						float num13 = Mathf.Max(2f, Mathf.Round(2.5f * p));
						GameArt.Flat(new Rect(rect2.x - num13, rect2.y - num13, rect2.width + num13 * 2f, rect2.height + num13 * 2f), GameArt.Orange);
					}
					GameArt.FlatButton(rect2, flag2, null, num11, flag2 ? 1.25f : 1f);
					GameArt.Label(new Rect(rect2.x, rect2.y - num12, rect2.width, rect2.height), item.Name, num11, TextAnchor.MiddleCenter, flag2 ? new Color(1f, 0.9f, 0.6f) : Color.white);
					if (item.Button != null)
					{
						GameArt.Label(new Rect(rect2.x, rect2.y + rect2.height * 0.5f + num11 * p * 0.35f, rect2.width, num11 * p), item.Button, num11 * 0.62f, TextAnchor.MiddleCenter, GameArt.KeyOrange);
					}
				}
				if (GUI.Button(rect2, GUIContent.none, GUIStyle.none))
				{
					VrMenu.Press(j);
				}
			}
			if (flag)
			{
				VrMenu.Hover(i);
				VrMenuDrawn = true;
			}
		}

		internal float DrawVrWrist(float w, float h)
		{
			VrWristDrawn = false;
			Actor host = _mod.Host;
			Actor body = _mod.Body;
			if (Event.current.type != EventType.Repaint || host == null || !host.isAlive())
			{
				return 0f;
			}
			EnsureAssets();
			EnsureVitals();
			Skin.Ensure();
			float p = GameArt.P;
			float num = Mathf.Round(6f * p);
			float unscaledTime = Time.unscaledTime;
			_nameplateBottom = 0f;
			DrawNameplate(w, h, body ?? host, _mod.Feedback);
			float num2 = Mathf.Max(_nameplateBottom, 46f * p) + 3f * p;
			float num3 = Births.Progress(host);
			if (num3 >= 0f)
			{
				GameArt.Label(new Rect(num, num2, 160f * p, 8f * p), Births.ExpectingLabel(host) ?? "Expecting", 4.8f, TextAnchor.MiddleLeft, new Color(1f, 0.8f, 0.86f));
				GameArt.Bar(new Rect(num, num2 + 9f * p, 100f * p, 6f * p), num3, new Color(0.92f, 0.5f, 0.62f));
				num2 += 18f * p;
			}
			float num4 = Mathf.Round(22f * p);
			float num5 = KeyButton(w - num, num2, num4, GameArt.Loot, GameArt.VrKey("Tab"), ShownBagCount() + "/" + 24, 1f);
			DrawPurse(num5 - 3f * p, num2, num4, host, 1f);
			num2 += num4 + 6f * p;
			Rect hud = new Rect(Mathf.Round((w - 150f * p) * 0.5f), Mathf.Round(num2), Mathf.Round(150f * p), Mathf.Round(34f * p));
			DrawAgePanel(hud, 1f, unscaledTime);
			DrawCompassBand(new Rect(hud.x + 6f * p, hud.yMax + 3f * p, hud.width - 12f * p, 10f * p), _mod.ViewYaw, _mod.CompassMarks(), 1f);
			num2 = hud.yMax + 17f * p;
			if (MinimapShows(w, h, out var r))
			{
				Matrix4x4 matrix = GUI.matrix;
				GUI.matrix = matrix * Matrix4x4.Translate(new Vector3(Mathf.Round((w - r.width) * 0.5f) - r.x, Mathf.Round(num2) - r.y, 0f));
				try
				{
					DrawMinimap(w, h);
				}
				finally
				{
					GUI.matrix = matrix;
				}
				num2 += r.height + 6f * p;
			}
			num2 = DrawCivilCards(num, num2, w - num * 2f, body ?? host);
			int num6 = 0;
			Royal royal = _mod.Royal;
			if (royal.Active != null && royal.Active.Status != null)
			{
				num6++;
			}
			for (int i = 0; i < royal.Raids.Count && i < 3; i++)
			{
				if (royal.Raids[i].Status != null)
				{
					num6++;
				}
			}
			for (int j = 0; j < royal.Storms.Count && j < 3; j++)
			{
				if (royal.Storms[j].Status != null)
				{
					num6++;
				}
			}
			if (num6 > 0)
			{
				float num7 = 10000f;
				float num8 = Mathf.Round(Mathf.Min(num7 * 0.3f, 170f * p));
				Matrix4x4 matrix2 = GUI.matrix;
				GUI.matrix = matrix2 * Matrix4x4.Translate(new Vector3(num - Mathf.Round(num7 - num8 - 6f * p), 0f, 0f));
				try
				{
					DrawCampaign(num7, h, num2);
				}
				finally
				{
					GUI.matrix = matrix2;
				}
				num2 += 4f * p + (float)num6 * Mathf.Round(9f * p) + 24f * p;
			}
			VrWristSlotCount = 0;
			int num9 = Mathf.Min(Hotbar.Size(host), VrWristSlots.Length);
			if (num9 > 0)
			{
				List<Gear.Piece> gear = Gear.Of(host);
				List<KeyValuePair<string, int>> bag = BagNow(host);
				List<string> list = BagOrder.Of(host);
				int num10 = Hotbar.Chosen(host);
				float num11 = Mathf.Round(3f * p);
				float num12 = Mathf.Round(Mathf.Min(26f * p, (w - num * 2f - num11 * (float)(num9 - 1)) / (float)num9));
				float num13 = Mathf.Round((w - ((float)num9 * num12 + (float)(num9 - 1) * num11)) * 0.5f);
				num2 += 3f * p;
				for (int k = 0; k < num9; k++)
				{
					Rect rect = new Rect(num13 + (float)k * (num12 + num11), Mathf.Round(num2), num12, num12);
					string text = ((k < list.Count) ? list[k] : "");
					DrawBagCell(rect, text, (text.Length > 0) ? BagIcon(host, text, gear) : null, bag, 1f);
					HotbarMarks(rect, k, k == num10);
					if (k == VrWristHover)
					{
						float num14 = Mathf.Max(2f, Mathf.Round(p * 1.5f));
						Color orange = GameArt.Orange;
						GameArt.Flat(new Rect(rect.x - num14, rect.y - num14, rect.width + num14 * 2f, num14), orange);
						GameArt.Flat(new Rect(rect.x - num14, rect.yMax, rect.width + num14 * 2f, num14), orange);
						GameArt.Flat(new Rect(rect.x - num14, rect.y, num14, rect.height), orange);
						GameArt.Flat(new Rect(rect.xMax, rect.y, num14, rect.height), orange);
					}
					VrWristSlots[k] = rect;
				}
				VrWristSlotCount = num9;
				num2 += num12 + 4f * p;
			}
			VrWristDrawn = true;
			return Mathf.Min(h, num2 + num);
		}

		private static void DrawShipLoad(Vector2 s, int n, float scale = 1f)
		{
			if (n > 0)
			{
				float p = GameArt.P;
				float num = Mathf.Round(9f * p * scale);
				string text = n.ToString();
				float width = Mathf.Round(GameArt.Measure(text, 3.8f).x + 3f * p);
				float num2 = Mathf.Round(6.5f * p);
				Rect r = new Rect(Mathf.Round(s.x + num * 0.5f - 1f * p), Mathf.Round(s.y - num * 0.5f - num2 + 1f * p), width, num2);
				GameArt.Flat(new Rect(r.x - p, r.y - p, r.width + 2f * p, r.height + 2f * p), new Color(0.05f, 0.04f, 0.04f, 0.95f));
				GameArt.Flat(r, new Color(0.17f, 0.36f, 0.15f, 1f));
				GameArt.Label(new Rect(r.x, r.y - 0.5f * p, r.width, r.height), text, 3.8f, TextAnchor.MiddleCenter, Color.white);
			}
		}

		private void DrawLifts(WarMap map, Func<Vector2, Vector2> toScreen)
		{
			foreach (Lift lift in map.Lifts)
			{
				Vector2 to;
				try
				{
					to = toScreen(lift.Ship.current_position);
				}
				catch
				{
					continue;
				}
				if (lift.Now == Lift.Stage.Boarding && lift.Boarding.Count > 0)
				{
					Vector2 zero = Vector2.zero;
					int num = 0;
					foreach (Actor item in lift.Boarding)
					{
						try
						{
							zero += item.current_position;
							num++;
						}
						catch
						{
						}
					}
					if (num > 0)
					{
						MarchLine(toScreen(zero / num), to, BoardGreen);
					}
				}
				else if (lift.Now == Lift.Stage.Landing && lift.Flag != null && lift.Drop != null)
				{
					MarchLine(toScreen(new Vector2((float)lift.Drop.x + 0.5f, (float)lift.Drop.y + 0.5f)), toScreen(lift.Flag.P), lift.Flag.Color);
				}
			}
		}

		private static Actor OwnShipUnder(WarMap map, Func<Vector2, Vector2> toScreen, Vector2 mouse)
		{
			float p = GameArt.P;
			float num = 6f * p * 6f * p;
			Actor result = null;
			foreach (WarMap.Unit unit in map.Units)
			{
				if (unit.Ship && unit.Ours)
				{
					Vector2 vector;
					try
					{
						vector = toScreen(unit.Actor.current_position);
					}
					catch
					{
						continue;
					}
					float sqrMagnitude = (vector - mouse).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						result = unit.Actor;
					}
				}
			}
			return result;
		}

		private bool BoardOrder(WarMap map, Actor host, Actor under)
		{
			bool flag;
			try
			{
				flag = under != null && under.isAlive() && under.kingdom == host.kingdom && WarMap.IsShip(under);
			}
			catch
			{
				flag = false;
			}
			if (!flag)
			{
				return false;
			}
			if (!WarMap.Transport(under))
			{
				_mod.Toast("Only transports carry soldiers");
				return true;
			}
			List<Actor> list = map.MarkedOwn();
			int cantReach;
			int noRoom;
			string why;
			int num = map.Board(_mod.Royal, host, list, under, null, out cantReach, out noRoom, out why);
			if (num == 0)
			{
				_mod.Toast(why ?? "None of your marked warriors can go aboard");
				return true;
			}
			foreach (Actor item in list)
			{
				map.Marked.Remove(item);
			}
			map.Marked.Add(under);
			string text = ((cantReach > 0) ? (". " + cantReach + " are on other land") : "") + ((noRoom > 0) ? (". " + noRoom + " find no room aboard") : "");
			_mod.Toast(((num == 1) ? "1 warrior goes" : (num + " warriors go")) + " aboard. Right-click land to put them ashore" + text + "." + Afterword(map, 0));
			_mod.PlayHorn();
			return true;
		}

		private List<Actor> LandOrder(WarMap map, Actor host, List<Actor> ships, Vector2 at, Actor under, WarFlag onFlag, bool force, out WarFlag landFlag, out string said)
		{
			landFlag = null;
			said = null;
			List<Actor> list = new List<Actor>();
			List<Actor> list2 = new List<Actor>();
			foreach (Actor ship in ships)
			{
				(map.Loaded(ship) ? list : list2).Add(ship);
			}
			if (list.Count == 0 || force)
			{
				return ships;
			}
			bool flag;
			try
			{
				flag = under != null && under.isAlive() && WarMap.IsShip(under) && under.kingdom != host.kingdom;
			}
			catch
			{
				flag = false;
			}
			if (flag)
			{
				return ships;
			}
			WorldTile worldTile = null;
			try
			{
				worldTile = World.world.GetTile(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y));
			}
			catch
			{
			}
			if (worldTile == null)
			{
				return ships;
			}
			WarFlag warFlag = onFlag;
			bool flag2 = false;
			if (warFlag == null)
			{
				bool flag3 = false;
				try
				{
					flag3 = worldTile.is_liquid;
				}
				catch
				{
				}
				if (flag3)
				{
					return ships;
				}
				WorldTile worldTile2 = WarMap.GroundFor(worldTile);
				if (worldTile2 == null)
				{
					said = "No firm ground there to land them";
					_mod.Toast(said);
					return list2;
				}
				if (map.Flags.Count >= 6 && map.FlagsKingdom == host.kingdom)
				{
					said = "All six flags are up. Right-click one of them to land the soldiers there";
					_mod.Toast(said);
					return list2;
				}
				warFlag = map.Plant(worldTile2, host.kingdom);
				flag2 = warFlag != null;
				if (warFlag == null)
				{
					return list2;
				}
			}
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			foreach (Actor item in list)
			{
				int num4 = map.Carrying(item);
				if (map.LandAt(item, warFlag))
				{
					num++;
					num2 += num4;
				}
				else
				{
					num3++;
				}
			}
			if (num == 0)
			{
				if (flag2)
				{
					map.PullUp(warFlag);
				}
				said = ((list.Count == 1) ? "Your ship can't" : "Your ships can't") + " reach the shore there";
				_mod.Toast(said);
				return list2;
			}
			landFlag = warFlag;
			said = ((num == 1) ? "A ship sails" : (num + " ships sail")) + " to land " + num2 + ((num2 == 1) ? " soldier" : " soldiers") + " at flag " + warFlag.Number + ((num3 > 0) ? (". " + num3 + " can't reach that shore") : "");
			_mod.Toast(said);
			_mod.PlayHorn();
			return list2;
		}

		private int SendBand(WarMap map, Actor host, List<Actor> own, WarFlag f, out int stranded, out string how)
		{
			List<Actor> list = new List<Actor>();
			List<Actor> list2 = new List<Actor>();
			foreach (Actor item in own)
			{
				(WarMap.CanWalkTo(item, f.Tile) ? list : list2).Add(item);
			}
			int cantReach;
			int num = map.Assign(_mod.Royal, host, list, f, out cantReach);
			int num2 = 0;
			int noShip = 0;
			if (list2.Count > 0)
			{
				num2 = map.Ferry(_mod.Royal, host, list2, f, map.MarkedShips(), out noShip, out var _);
			}
			stranded = cantReach + noShip;
			if (num2 == 0)
			{
				how = W(num) + ((num == 1) ? " marches" : " march") + " to hold it";
			}
			else if (num == 0)
			{
				how = W(num2) + " go by ship to hold it";
			}
			else
			{
				how = W(num) + ((num == 1) ? " marches" : " march") + " to hold it and " + num2 + " go by ship";
			}
			return num + num2;
			string W(int n)
			{
				if (n != 1)
				{
					return n + " warriors";
				}
				return "1 warrior";
			}
		}

		private static string Stranded(WarMap map, int stranded)
		{
			return ((stranded > 0) ? (" " + stranded + " can't cross the water: no transport of yours can carry them. Order one on a harbour town's card.") : "") + Afterword(map, 0);
		}

		private static string FerryLine(WarMap map)
		{
			if (map.Lifts.Count == 0)
			{
				return null;
			}
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			foreach (Lift lift in map.Lifts)
			{
				num++;
				num2 += lift.Aboard;
				num3 += lift.Boarding.Count;
				if (lift.Now == Lift.Stage.Landing)
				{
					num4++;
				}
			}
			string text = num2 + ((num2 == 1) ? " soldier" : " soldiers") + " aboard " + ((num == 1) ? "1 ship" : (num + " ships"));
			if (num3 > 0)
			{
				text = text + " · " + num3 + " going aboard";
			}
			if (num4 > 0)
			{
				text = text + " · " + num4 + " landing";
			}
			return text;
		}

		private void DrawFlagMarker(Vector2 foot, WarFlag f, Kingdom kingdom, float scale, float alpha, bool lit)
		{
			float p = GameArt.P;
			float unscaledTime = Time.unscaledTime;
			scale *= 1f + 0.35f * Mathf.Clamp01(1f - (unscaledTime - f.Born) / 0.3f);
			if (lit)
			{
				scale *= 1.12f;
			}
			Color color = GUI.color;
			GUI.color = new Color(color.r, color.g, color.b, color.a * alpha);
			float num = Mathf.Round(20f * p * scale);
			float num2 = Mathf.Max(1f, Mathf.Round(p * scale));
			float num3 = Mathf.Round(foot.x);
			float num4 = Mathf.Round(foot.y);
			GameArt.Flat(new Rect(num3 + 1f, num4 - num + 1f, num2, num), new Color(0f, 0f, 0f, 0.45f));
			GameArt.Flat(new Rect(num3, num4 - num, num2, num), PoleBrown);
			Rect r = new Rect(num3 + num2, num4 - num, Mathf.Round(8f * p * scale), Mathf.Round(10f * p * scale));
			if (!DrawBanner(r, kingdom, kingdom != null, 1f))
			{
				GameArt.Flat(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), new Color(0f, 0f, 0f, 0.45f));
				GameArt.Flat(r, (kingdom != null) ? Royal.ColorOf(kingdom) : new Color(0.95f, 0.8f, 0.2f));
			}
			float num5 = Mathf.Round(7.5f * p * scale);
			DrawNumberTag(new Rect(num3 + num2, Mathf.Round(r.yMax + 0.5f * p), num5, num5), f, lit);
			PixelDiamond(new Vector2(num3, num4), 1, f.Color);
			GUI.color = color;
		}

		private static void DrawNumberTag(Rect tag, WarFlag f, bool lit)
		{
			float p = GameArt.P;
			float num = Mathf.Max(1f, Mathf.Round(p * 0.8f));
			GameArt.Flat(new Rect(tag.x - num, tag.y - num, tag.width + 2f * num, tag.height + 2f * num), lit ? new Color(1f, 1f, 1f, 0.95f) : new Color(0.05f, 0.04f, 0.04f, 0.95f));
			GameArt.Flat(tag, f.Color);
			GameArt.Flat(new Rect(tag.x, tag.yMax - num, tag.width, num), new Color(0f, 0f, 0f, 0.25f));
			if (Event.current != null && Event.current.type == EventType.Repaint)
			{
				GUIStyle gUIStyle = GameArt.Style(tag.height / p * 0.78f, TextAnchor.MiddleCenter);
				gUIStyle.normal.textColor = TagInk;
				GUI.Label(new Rect(tag.x, tag.y - 0.5f * p, tag.width, tag.height), f.Number.ToString(), gUIStyle);
			}
		}

		private static Rect FlagHitRect(Vector2 foot)
		{
			float p = GameArt.P;
			return Rect.MinMaxRect(foot.x - 3f * p, foot.y - 22f * p, foot.x + 11f * p, foot.y - 2.5f * p);
		}

		private static WarFlag FlagUnder(WarMap map, Func<Vector2, Vector2> toScreen, Vector2 mouse)
		{
			for (int num = map.Flags.Count - 1; num >= 0; num--)
			{
				if (FlagHitRect(toScreen(map.Flags[num].P)).Contains(mouse))
				{
					return map.Flags[num];
				}
			}
			return null;
		}

		private void MarchLine(Vector2 from, Vector2 to, Color c)
		{
			float p = GameArt.P;
			Vector2 vector = to - from;
			float magnitude = vector.magnitude;
			if (magnitude < 14f * p)
			{
				return;
			}
			Vector2 vector2 = vector / magnitude;
			float num = 5f * p;
			float num2 = 3.5f * p;
			float num3 = num + num2;
			float thick = Mathf.Max(1f, Mathf.Round(p * 0.9f));
			for (float num4 = Time.unscaledTime * 10f * p % num3 - num3; num4 < magnitude - 6f * p; num4 += num3)
			{
				float num5 = Mathf.Max(0f, num4);
				float num6 = Mathf.Min(magnitude - 6f * p, num4 + num);
				if (!(num6 <= num5))
				{
					Line(from + vector2 * num5 + new Vector2(1f, 1f), from + vector2 * num6 + new Vector2(1f, 1f), thick, new Color(0f, 0f, 0f, 0.4f));
					Line(from + vector2 * num5, from + vector2 * num6, thick, new Color(c.r, c.g, c.b, 0.85f));
				}
			}
		}

		private static void BandPip(float sx, float sy, float dot, Color c)
		{
			float p = GameArt.P;
			float num = Mathf.Max(2f, Mathf.Round(dot * 0.6f));
			float num2 = Mathf.Max(1f, Mathf.Round(p * 0.6f));
			float num3 = Mathf.Round(sx + dot * 0.5f - num * 0.2f);
			float num4 = Mathf.Round(sy - dot * 0.5f - num * 0.8f);
			GameArt.Flat(new Rect(num3 - num2, num4 - num2, num + 2f * num2, num + 2f * num2), new Color(0.05f, 0.04f, 0.04f, 0.95f));
			GameArt.Flat(new Rect(num3, num4, num, num), c);
		}

		private static string BandLine(WarFlag f)
		{
			string text = ((f.AtSea > 0) ? (f.AtSea + " by sea") : null);
			if (f.Band.Count == 0)
			{
				string text2 = text;
				if (text2 == null)
				{
					if (f.Fallen <= 0)
					{
						return "nobody sent yet";
					}
					text2 = "all " + f.Fallen + " fallen";
				}
				return text2;
			}
			string text3 = f.Holding + " holding";
			if (f.Coming > 0)
			{
				text3 = text3 + " · " + f.Coming + " coming";
			}
			if (f.Fighting > 0)
			{
				text3 = text3 + " · " + f.Fighting + " fighting";
			}
			if (f.Eating > 0)
			{
				text3 = text3 + " · " + f.Eating + " eating";
			}
			if (f.Holding == 0 && f.Coming == 0 && f.Fighting == 0 && f.Eating == 0)
			{
				text3 = f.Band.Count + " sent";
			}
			if (text != null)
			{
				text3 = text3 + " · " + text;
			}
			return text3;
		}

		private void FlagTip(WarMap map, WarFlag f, Vector2 mouse)
		{
			float p = GameArt.P;
			int count = map.MarkedOwn().Count;
			string text = "Flag " + f.Number + " (" + WarMap.OrderNames[(int)f.Order] + ")   " + BandLine(f);
			string text2 = "Click: select its band   Drag: move it   Shift+right-drag: its area";
			string text3 = ((count > 0) ? ("Right-click: send the " + count + " selected here") : "Right-click: pull it up");
			float width = Mathf.Max(GameArt.Measure(text, 5f).x, Mathf.Max(GameArt.Measure(text2, 4.2f).x, GameArt.Measure(text3, 4.2f).x)) + 12f * p;
			Rect r = new Rect(mouse.x + 12f * p, mouse.y + 8f * p, width, 29f * p);
			if (r.xMax > (float)Screen.width)
			{
				r.x = mouse.x - r.width - 8f * p;
			}
			GameArt.TipPanel(r);
			GameArt.Label(new Rect(r.x + 6f * p, r.y + 1.5f * p, r.width, 9f * p), text, 5f, TextAnchor.MiddleLeft, f.Color);
			GameArt.Label(new Rect(r.x + 6f * p, r.y + 10.5f * p, r.width, 8f * p), text2, 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
			GameArt.Label(new Rect(r.x + 6f * p, r.y + 18.5f * p, r.width, 8f * p), text3, 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
		}

		private void FlagAt(WarMap map, Actor host, WorldTile clicked)
		{
			WorldTile worldTile = WarMap.GroundFor(clicked);
			if (worldTile == null)
			{
				_mod.Toast("No firm ground there to plant a flag.");
				return;
			}
			List<Actor> list = map.MarkedOwn();
			WarFlag warFlag = map.WholeBand(list);
			if (warFlag != null)
			{
				map.Move(warFlag, worldTile);
				map.Marked.Clear();
				_mod.Toast("Flag " + warFlag.Number + " moves: its " + warFlag.Band.Count + " warriors march to hold the new ground.");
				_mod.PlayHorn();
				return;
			}
			if (map.Flags.Count >= 6 && map.FlagsKingdom == host.kingdom)
			{
				_mod.Toast("All six flags are up. Drag one here, or right-click one with nobody marked to pull it up.");
				return;
			}
			WarFlag warFlag2 = map.Plant(worldTile, host.kingdom);
			if (warFlag2 != null)
			{
				if (list.Count == 0)
				{
					_mod.Toast("Flag " + warFlag2.Number + " planted. Mark warriors and right-click it to send them to hold it.");
					return;
				}
				if (SendBand(map, host, list, warFlag2, out var stranded, out var how) == 0)
				{
					map.PullUp(warFlag2);
					_mod.Toast((stranded > 0) ? "None of your marked warriors can reach that ground. No transport of yours can carry them." : "None of your marked warriors can go there.");
					return;
				}
				map.Marked.Clear();
				_mod.Toast("Flag " + warFlag2.Number + " planted. " + how + "." + Stranded(map, stranded));
				_mod.PlayHorn();
			}
		}

		private void SendToFlag(WarMap map, Actor host, WarFlag f)
		{
			List<Actor> list = map.MarkedOwn();
			if (list.Count != 0)
			{
				if (SendBand(map, host, list, f, out var stranded, out var how) == 0)
				{
					_mod.Toast((stranded > 0) ? ("None of your marked warriors can reach flag " + f.Number + ". No transport of yours can carry them.") : ("None of your marked warriors can go to flag " + f.Number + "."));
					return;
				}
				map.Marked.Clear();
				_mod.Toast("Flag " + f.Number + ". " + how + "." + Stranded(map, stranded));
				_mod.PlayHorn();
			}
		}

		private void PullUpFlag(WarMap map, WarFlag f)
		{
			int count = f.Band.Count;
			map.PullUp(f);
			_mod.Toast("Flag " + f.Number + " pulled up." + ((count > 0) ? (" Its " + count + ((count == 1) ? " warrior goes" : " warriors go") + " back to their duties.") : ""));
		}

		private void PickBand(WarMap map, WarFlag f, bool add)
		{
			float unscaledTime = Time.unscaledTime;
			if (_bandKeyNumber == f.Number && unscaledTime - _bandKeyAt < 0.4f)
			{
				map.Centre = f.P;
			}
			_bandKeyNumber = f.Number;
			_bandKeyAt = unscaledTime;
			int num = map.MarkBand(f, add);
			map.SelectedFlag = f;
			Sounds.PlayUI("event:/SFX/Ui/Click");
			if (num == 0)
			{
				_mod.Toast("Nobody holds flag " + f.Number + " yet. Select warriors and right-click it");
			}
		}

		private static string Afterword(WarMap map, int cantReach)
		{
			string text = "";
			if (cantReach > 0)
			{
				text = text + " " + cantReach + " can't reach it across the water.";
			}
			foreach (WarFlag item in map.LastEmptied)
			{
				text = text + " Flag " + item.Number + " comes down.";
			}
			return text;
		}

		private float DrawFlagRows(WarMap map, Actor host, float x, float y, float w)
		{
			float p = GameArt.P;
			bool flag = Event.current.type == EventType.Repaint;
			int num = ((map.FlagsKingdom == host.kingdom) ? map.Flags.Count : 0);
			GameArt.Label(new Rect(x + 2f * p, y, w, 9f * p), "Flags", 5f, TextAnchor.MiddleLeft, GameArt.Orange);
			GameArt.Label(new Rect(x, y, w - 2f * p, 9f * p), num + " of " + 6, 4.2f, TextAnchor.MiddleRight, GameArt.Grey);
			y += 10f * p;
			if (num == 0)
			{
				GameArt.Label(new Rect(x + 2f * p, y, w - 4f * p, 16f * p), "Shift+right-click the map to plant a flag. Each holds its own band.", 4.1f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
				return y + 16f * p;
			}
			int count = map.MarkedOwn().Count;
			float num2 = Mathf.Round(13f * p);
			float num3 = Mathf.Round(12f * p);
			float num4 = Mathf.Round(24f * p);
			float num5 = Mathf.Round(12f * p);
			WarFlag warFlag = null;
			WarFlag warFlag2 = null;
			WarFlag warFlag3 = null;
			WarFlag warFlag4 = null;
			foreach (WarFlag flag3 in map.Flags)
			{
				Rect r = new Rect(x, y, w, num2);
				Rect rect = new Rect(x, y, w - num4 - num3 - num5 - 6f * p, num2);
				bool flag2 = rect.Contains(Event.current.mousePosition);
				if (flag)
				{
					GameArt.Flat(r, new Color(0f, 0f, 0f, flag2 ? 0.3f : 0.16f));
					float num6 = Mathf.Round(9f * p);
					DrawNumberTag(new Rect(x + 3f * p, Mathf.Round(y + (num2 - num6) * 0.5f), num6, num6), flag3, flag2 || map.SelectedFlag == flag3);
					DrawOrderIcon(new Rect(x + 14f * p, Mathf.Round(y + (num2 - num6) * 0.5f), num6, num6), flag3.Order, Color.white);
					GameArt.Label(new Rect(x + 25f * p, y, rect.width - 25f * p, num2), BandLine(flag3), 4.1f, TextAnchor.MiddleLeft, (flag3.Band.Count == 0 && flag3.Fallen > 0) ? new Color(1f, 0.5f, 0.4f) : Color.white);
				}
				PadNav.Spot(rect);
				if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
				{
					warFlag3 = flag3;
				}
				if (SceneEye(new Rect(r.xMax - num4 - num3 - num5 - 4f * p, y + 1f * p, num5, num2 - 2f * p), "Flag " + flag3.Number + " in 3D"))
				{
					warFlag4 = flag3;
				}
				if (GameButton(new Rect(r.xMax - num4 - num3 - 2f * p, y + 1f * p, num4, num2 - 2f * p), "Send", red: true, count > 0, null, 4.2f))
				{
					warFlag2 = flag3;
				}
				if (GameButton(new Rect(r.xMax - num3, y + 1f * p, num3, num2 - 2f * p), "x", red: false, enabled: true, null, 4.6f))
				{
					warFlag = flag3;
				}
				y += num2 + 2f * p;
			}
			if (warFlag3 != null)
			{
				PickBand(map, warFlag3, Event.current.shift);
			}
			if (warFlag2 != null)
			{
				SendToFlag(map, host, warFlag2);
			}
			if (warFlag != null)
			{
				PullUpFlag(map, warFlag);
			}
			if (warFlag4 != null)
			{
				_mod.EnterScene(MapTabs.Page.War, warFlag4.P);
			}
			return y;
		}

		private void DrawFlagPins(WarMap map, float sw, float sh)
		{
			Actor host = _mod.Host;
			PinsFrameForTest = Time.frameCount;
			PinsDrawnForTest = 0;
			if (map.Flags.Count == 0 || host == null || host.kingdom != map.FlagsKingdom)
			{
				return;
			}
			FrameProjection shown = _mod.Shown;
			if (shown.Width <= 0 || shown.Height <= 0)
			{
				return;
			}
			float p = GameArt.P;
			Vector2 current_position = host.current_position;
			Vector2 vector = new Vector2(sw * 0.5f, sh * 0.5f);
			Kingdom flagsKingdom = map.FlagsKingdom;
			foreach (WarFlag flag in map.Flags)
			{
				Vector2 p2 = flag.P;
				float magnitude = (p2 - current_position).magnitude;
				if (magnitude > 60f)
				{
					continue;
				}
				float num = _mod.GroundAt(p2);
				if (!shown.Project(p2.x, p2.y, num + 0.05f, out var col, out var row, out var _))
				{
					continue;
				}
				float num2 = col / (float)shown.Width * sw;
				float num3 = row / (float)shown.Height * sh;
				if (!(num2 < 8f * p) && !(num2 > sw - 16f * p) && !(num3 < 30f * p) && !(num3 > sh - 4f * p))
				{
					float num4 = ((magnitude < 8f) ? 1.5f : 1.25f);
					float num5 = Mathf.Clamp01((60f - magnitude) / 15f);
					Color color = GUI.color;
					GUI.color = new Color(color.r, color.g, color.b, color.a * num5);
					DrawFlagMarker(new Vector2(num2, num3), flag, flagsKingdom, num4, 0.95f, lit: false);
					PinsDrawnForTest++;
					float num6 = num3 - 21f * p * num4;
					GameArt.Label(new Rect(num2 - 30f * p + 4f * p * num4, num6 - 9f * p, 60f * p, 8f * p), Mathf.RoundToInt(magnitude).ToString(), 4.8f, TextAnchor.LowerCenter, flag.Color);
					if ((new Vector2(num2, num3 - 10f * p) - vector).sqrMagnitude < 34f * p * 34f * p)
					{
						string text = "Flag " + flag.Number + "  ·  " + BandLine(flag);
						float num7 = GameArt.Measure(text, 4.6f).x + 10f * p;
						Rect r = new Rect(Mathf.Round(num2 - num7 * 0.5f + 4f * p), Mathf.Round(num3 + 3f * p), Mathf.Round(num7), Mathf.Round(10f * p));
						GameArt.TipPanel(r, 0.85f);
						GameArt.Label(r, text, 4.6f, TextAnchor.MiddleCenter, flag.Color);
					}
					GUI.color = color;
				}
			}
		}

		private void DrawFlagMini(Vector2 s, WarFlag f, float alpha)
		{
			float p = GameArt.P;
			float num = Mathf.Round(6.5f * p);
			float num2 = Mathf.Round(9f * p);
			float num3 = Mathf.Max(1f, Mathf.Round(p * 0.8f));
			Color color = GUI.color;
			GUI.color = new Color(color.r, color.g, color.b, color.a * alpha);
			float num4 = Mathf.Round(s.x);
			float num5 = Mathf.Round(s.y);
			GameArt.Flat(new Rect(num4, num5 - num2, num3, num2), PoleBrown);
			DrawNumberTag(new Rect(num4 + num3, num5 - num2, num, num), f, lit: false);
			GUI.color = color;
		}

		private static void DrawShipGlyph(Vector2 s, Color color, bool marked, bool fighting, float pulse, float scale = 1f)
		{
			float p = GameArt.P;
			float num = Mathf.Round(9f * p * scale);
			float num2 = Mathf.Round(s.x);
			float num3 = Mathf.Round(s.y);
			Rect r = new Rect(num2 - num * 0.5f, num3 - num * 0.5f, num, num);
			if (marked)
			{
				float num4 = Mathf.Round(2f * p);
				GameArt.Flat(new Rect(r.x - num4, r.y - num4, r.width + 2f * num4, r.height + 2f * num4), new Color(1f, 0.85f, 0.25f, 0.95f));
			}
			Color c = (fighting ? new Color(0.95f, 0.15f, 0.1f, 0.6f + 0.4f * pulse) : new Color(0.05f, 0.04f, 0.04f, 0.95f));
			GameArt.Flat(new Rect(r.x - p, r.y - p, r.width + 2f * p, r.height + 2f * p), c);
			GameArt.Flat(r, new Color(color.r * 0.8f, color.g * 0.8f, color.b * 0.8f, 1f));
			Sprite sprite = GameArt.IconNamed("iconBoat");
			if (sprite != null)
			{
				GameArt.IconFit(new Rect(r.x + 0.5f * p, r.y + 0.5f * p, r.width - p, r.height - p), sprite, Color.white);
			}
		}

		private static void DrawReticle(Vector2 c, int ships, bool holding, float pulse)
		{
			float p = GameArt.P;
			float num = Mathf.Round(6.5f * p * (1f + 0.12f * pulse));
			Color color = (holding ? new Color(0.75f, 0.72f, 0.7f, 0.9f) : new Color(FireRed.r, FireRed.g, FireRed.b, 0.95f));
			DottedRing(c, num, color, Mathf.Max(1f, Mathf.Round(p)));
			float num2 = Mathf.Max(1f, Mathf.Round(p * 0.9f));
			float num3 = Mathf.Round(4f * p);
			GameArt.Flat(new Rect(Mathf.Round(c.x - num2 * 0.5f), Mathf.Round(c.y - num - num3 * 0.5f), num2, num3), color);
			GameArt.Flat(new Rect(Mathf.Round(c.x - num2 * 0.5f), Mathf.Round(c.y + num - num3 * 0.5f), num2, num3), color);
			GameArt.Flat(new Rect(Mathf.Round(c.x - num - num3 * 0.5f), Mathf.Round(c.y - num2 * 0.5f), num3, num2), color);
			GameArt.Flat(new Rect(Mathf.Round(c.x + num - num3 * 0.5f), Mathf.Round(c.y - num2 * 0.5f), num3, num2), color);
			PixelDiamond(c, 1, color);
			if (ships > 1)
			{
				GameArt.Label(new Rect(c.x + num + 1f * p, c.y - num - 4f * p, 20f * p, 8f * p), "×" + ships, 4.4f, TextAnchor.MiddleLeft, color);
			}
		}

		private static void DottedRing(Vector2 c, float r, Color col, float dot)
		{
			int num = Mathf.Clamp(Mathf.RoundToInt(MathF.PI * 2f * r / (dot * 3.5f)), 16, 260);
			for (int i = 0; i < num; i++)
			{
				float f = (float)i * MathF.PI * 2f / (float)num;
				GameArt.Flat(new Rect(Mathf.Round(c.x + Mathf.Cos(f) * r - dot * 0.5f), Mathf.Round(c.y + Mathf.Sin(f) * r - dot * 0.5f), dot, dot), col);
			}
		}

		private void DrawFleetOrders(WarMap map, Func<Vector2, Vector2> toScreen, float ppt, float pulse)
		{
			if (map.FleetKingdom != map.Home && map.ShipOrders.Count > 0)
			{
				return;
			}
			float p = GameArt.P;
			int num = 0;
			foreach (Actor item in map.Marked)
			{
				if (num >= 12)
				{
					break;
				}
				bool flag;
				try
				{
					flag = item != null && item.isAlive() && item.kingdom == map.Home && WarMap.IsShip(item) && WarMap.Armed(item);
				}
				catch
				{
					flag = false;
				}
				if (flag)
				{
					float num2 = 14f;
					try
					{
						num2 = item.getAttackRange();
					}
					catch
					{
					}
					Vector2 vector = toScreen(item.current_position);
					DottedRing(vector + new Vector2(1f, 1f), num2 * ppt, new Color(0f, 0f, 0f, 0.35f), Mathf.Max(2f, Mathf.Round(p)));
					DottedRing(vector, num2 * ppt, new Color(1f, 0.85f, 0.3f, 0.75f), Mathf.Max(2f, Mathf.Round(p)));
					num++;
				}
			}
			foreach (ShipOrder shipOrder2 in map.ShipOrders)
			{
				Vector2 vector2;
				try
				{
					vector2 = toScreen(shipOrder2.Ship.current_position);
				}
				catch
				{
					continue;
				}
				if (shipOrder2.What == ShipOrder.Kind.Sail)
				{
					if (shipOrder2.To != null)
					{
						Vector2 vector3 = toScreen(new Vector2((float)shipOrder2.To.x + 0.5f, (float)shipOrder2.To.y + 0.5f));
						MarchLine(vector2, vector3, SailBlue);
						PixelDiamond(vector3, 1, new Color(SailBlue.r, SailBlue.g, SailBlue.b, 0.9f));
					}
				}
				else
				{
					Vector2 to = toScreen((shipOrder2.Holding == "out of reach") ? shipOrder2.Mark : shipOrder2.LastAim);
					Color c = ((shipOrder2.Holding == "out of reach") ? FireRed : new Color(FireRed.r, FireRed.g, FireRed.b, 0.45f));
					MarchLine(vector2, to, c);
				}
			}
			for (int i = 0; i < map.ShipOrders.Count; i++)
			{
				ShipOrder shipOrder = map.ShipOrders[i];
				if (shipOrder.What != ShipOrder.Kind.Bombard)
				{
					continue;
				}
				bool flag2 = true;
				for (int j = 0; j < i && flag2; j++)
				{
					if (map.ShipOrders[j].SameTarget(shipOrder))
					{
						flag2 = false;
					}
				}
				if (!flag2)
				{
					continue;
				}
				int num3 = 0;
				bool holding = true;
				foreach (ShipOrder shipOrder3 in map.ShipOrders)
				{
					if (shipOrder3.SameTarget(shipOrder))
					{
						num3++;
						if (shipOrder3.Holding == null)
						{
							holding = false;
						}
					}
				}
				DrawReticle(toScreen(shipOrder.Mark), num3, holding, pulse);
			}
			DrawLifts(map, toScreen);
		}

		private static ShipOrder ReticleUnder(WarMap map, Func<Vector2, Vector2> toScreen, Vector2 mouse)
		{
			float p = GameArt.P;
			foreach (ShipOrder shipOrder in map.ShipOrders)
			{
				if (shipOrder.What == ShipOrder.Kind.Bombard && (toScreen(shipOrder.Mark) - mouse).sqrMagnitude < 9f * p * 9f * p)
				{
					return shipOrder;
				}
			}
			return null;
		}

		private static Actor UnitUnder(WarMap map, Func<Vector2, Vector2> toScreen, Vector2 mouse)
		{
			float p = GameArt.P;
			float num = 9f * p * 9f * p;
			Actor result = null;
			foreach (WarMap.Unit unit in map.Units)
			{
				float sqrMagnitude = (toScreen(unit.P) - mouse).sqrMagnitude;
				if (sqrMagnitude < num)
				{
					num = sqrMagnitude;
					result = unit.Actor;
				}
			}
			return result;
		}

		private void ShipTip(WarMap map, Actor ship, Vector2 mouse)
		{
			float p = GameArt.P;
			string text = WorldBoxMod.Describe(ship);
			string text2 = "";
			try
			{
				text2 = ship.getHealth() + "/" + ship.getMaxHealth() + " hull";
			}
			catch
			{
			}
			string text3 = "";
			try
			{
				text3 = Royal.NameOf(ship.kingdom);
			}
			catch
			{
			}
			string text4 = WarMap.ShipDoing(ship) ?? Favorites.Doing(ship);
			int num = 0;
			try
			{
				Boat simpleComponent = ship.getSimpleComponent<Boat>();
				if (simpleComponent != null)
				{
					num = simpleComponent.countPassengers();
				}
			}
			catch
			{
			}
			string text5 = text4 + ((num > 0) ? ("   " + num + " aboard") : "") + "   " + text2;
			string text6 = null;
			bool flag = false;
			try
			{
				flag = ship.kingdom == map.Home;
			}
			catch
			{
			}
			if (flag && WarMap.Transport(ship) && map.MarkedOwn().Count > 0)
			{
				text6 = "Right-click: the marked warriors go aboard (" + map.SeatsLeft(ship) + " seats)";
			}
			else if (flag && map.Loaded(ship))
			{
				text6 = "Mark her, then right-click land to put her soldiers ashore";
			}
			else if (flag && !WarMap.Armed(ship))
			{
				text6 = "No guns: she can sail, not fire";
			}
			else if (!flag && map.MarkedShips().Count > 0)
			{
				text6 = "Right-click: your marked ships fire on her";
			}
			float width = Mathf.Max(GameArt.Measure(text + "   " + text3, 5f).x, Mathf.Max(GameArt.Measure(text5, 4.2f).x, (text6 != null) ? GameArt.Measure(text6, 4.2f).x : 0f)) + 12f * p;
			Rect r = new Rect(mouse.x + 12f * p, mouse.y + 8f * p, width, ((text6 != null) ? 29f : 21f) * p);
			if (r.xMax > (float)Screen.width)
			{
				r.x = mouse.x - r.width - 8f * p;
			}
			GameArt.TipPanel(r);
			GameArt.Label(new Rect(r.x + 6f * p, r.y + 1.5f * p, r.width, 9f * p), text + "   " + text3, 5f, TextAnchor.MiddleLeft, Color.white);
			GameArt.Label(new Rect(r.x + 6f * p, r.y + 10.5f * p, r.width, 8f * p), text5, 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
			if (text6 != null)
			{
				GameArt.Label(new Rect(r.x + 6f * p, r.y + 18.5f * p, r.width, 8f * p), text6, 4.2f, TextAnchor.MiddleLeft, GameArt.Peach);
			}
		}

		private void ReticleTip(WarMap map, ShipOrder o, Vector2 mouse)
		{
			float p = GameArt.P;
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			bool flag = false;
			foreach (ShipOrder shipOrder in map.ShipOrders)
			{
				if (shipOrder.SameTarget(o))
				{
					num++;
					num2 += shipOrder.Shots;
					if (shipOrder.Holding == "out of reach")
					{
						num3++;
					}
					else if (shipOrder.Holding != null)
					{
						flag = true;
					}
				}
			}
			string text = ((num == 1) ? "1 ship" : (num + " ships")) + " firing on " + o.TargetName;
			string text2 = num2 + ((num2 == 1) ? " shot" : " shots") + ((num3 > 0) ? ("   " + num3 + " closing in") : "") + (flag ? "   holding fire: your own are there" : "");
			string text3 = ((map.MarkedShips().Count > 0) ? "Right-click: the marked ships join them" : "Right-click (nothing marked): call them off");
			float width = Mathf.Max(GameArt.Measure(text, 5f).x, Mathf.Max(GameArt.Measure(text2, 4.2f).x, GameArt.Measure(text3, 4.2f).x)) + 12f * p;
			Rect r = new Rect(mouse.x + 12f * p, mouse.y + 8f * p, width, 29f * p);
			if (r.xMax > (float)Screen.width)
			{
				r.x = mouse.x - r.width - 8f * p;
			}
			GameArt.TipPanel(r);
			GameArt.Label(new Rect(r.x + 6f * p, r.y + 1.5f * p, r.width, 9f * p), text, 5f, TextAnchor.MiddleLeft, new Color(1f, 0.5f, 0.4f));
			GameArt.Label(new Rect(r.x + 6f * p, r.y + 10.5f * p, r.width, 8f * p), text2, 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
			GameArt.Label(new Rect(r.x + 6f * p, r.y + 18.5f * p, r.width, 8f * p), text3, 4.2f, TextAnchor.MiddleLeft, GameArt.Peach);
		}

		private void FleetOrder(WarMap map, Actor host, List<Actor> ships, Vector2 at, Actor under, bool force)
		{
			Kingdom kingdom = host.kingdom;
			WorldTile worldTile = null;
			try
			{
				worldTile = World.world.GetTile(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y));
			}
			catch
			{
			}
			if (worldTile == null)
			{
				return;
			}
			City city = null;
			try
			{
				city = ((worldTile.zone != null) ? worldTile.zone.city : null);
			}
			catch
			{
			}
			Kingdom kingdom2 = null;
			try
			{
				kingdom2 = ((city != null && city.isAlive()) ? city.kingdom : null);
			}
			catch
			{
			}
			Actor actor = null;
			bool flag = kingdom2 != null && kingdom2 != kingdom && Royal.AtWar(kingdom, kingdom2);
			try
			{
				if (under != null && under.isAlive() && under.kingdom != kingdom && under.kingdom != null && (force ? (!Royal.Allied(kingdom, under.kingdom)) : Royal.AtWar(kingdom, under.kingdom)) && (WarMap.IsShip(under) || !flag))
				{
					actor = under;
				}
			}
			catch
			{
			}
			bool flag2 = false;
			try
			{
				flag2 = worldTile.is_liquid;
			}
			catch
			{
			}
			if (force && actor == null && kingdom2 != null && (kingdom2 == kingdom || Royal.Allied(kingdom, kingdom2)))
			{
				_mod.Toast((kingdom2 == kingdom) ? "Your ships won't fire on your own town." : ("Your ships won't fire on " + city.name + ": " + Royal.NameOf(kingdom2) + " is your ally."));
				return;
			}
			if (actor != null || force || flag)
			{
				string text = ((actor != null) ? WorldBoxMod.Describe(actor) : (force ? "that spot" : city.name));
				int unarmed;
				int num = map.Bombard(host, ships, at, actor, (actor == null && !force) ? city : null, text, out unarmed);
				string text2 = ((unarmed > 0) ? (" (" + unarmed + ((unarmed == 1) ? " has" : " have") + " no guns.)") : "");
				if (num == 0)
				{
					_mod.Toast("None of your marked ships can fire on " + text + "." + text2);
					return;
				}
				_mod.Toast(((num == 1) ? "A ship sails" : (num + " ships sail")) + " to fire on " + text + "!" + text2);
				_mod.PlayHorn();
				return;
			}
			WorldTile worldTile2 = (flag2 ? worldTile : CoastNear(at, ships[0]));
			if (worldTile2 == null)
			{
				_mod.Toast("No water near there for your ships.");
				return;
			}
			int cantReach;
			int num2 = map.SailTo(host, ships, worldTile2, out cantReach);
			string text3 = ((!flag2 && kingdom2 != null && kingdom2 != kingdom && !Royal.Allied(kingdom, kingdom2)) ? (" You're not at war with " + Royal.NameOf(kingdom2) + ": Ctrl+right-click to fire on " + city.name + " anyway.") : "");
			if (num2 == 0)
			{
				_mod.Toast("None of your marked ships can reach that water." + text3);
			}
			else
			{
				_mod.Toast(((num2 == 1) ? "A ship sets sail" : (num2 + " ships set sail")) + (flag2 ? "." : " for the coast there.") + ((cantReach > 0) ? (" " + cantReach + " can't reach it.") : "") + text3);
			}
		}

		private static WorldTile CoastNear(Vector2 p, Actor ship)
		{
			WorldTile worldTile = null;
			try
			{
				worldTile = ship.current_tile;
			}
			catch
			{
			}
			for (int i = 1; i <= 12; i++)
			{
				for (int j = 0; j < 8 * i; j++)
				{
					float f = (float)j * MathF.PI * 2f / (float)(8 * i);
					WorldTile worldTile2 = null;
					try
					{
						worldTile2 = World.world.GetTile(Mathf.FloorToInt(p.x + Mathf.Cos(f) * (float)i), Mathf.FloorToInt(p.y + Mathf.Sin(f) * (float)i));
					}
					catch
					{
					}
					bool flag = false;
					try
					{
						flag = worldTile2 != null && worldTile2.is_liquid && worldTile2.Type != null && !worldTile2.Type.lava && (worldTile == null || worldTile2.isSameIsland(worldTile) || !worldTile.is_liquid);
					}
					catch
					{
					}
					if (flag)
					{
						return worldTile2;
					}
				}
			}
			return null;
		}

		private float DrawFleetPanel(WarMap map, Actor host, float x, float y, float w)
		{
			float p = GameArt.P;
			GameArt.Label(new Rect(x + 2f * p, y, w, 9f * p), "Fleet", 5f, TextAnchor.MiddleLeft, GameArt.Orange);
			GameArt.Label(new Rect(x, y, w - 2f * p, 9f * p), map.OurShips + ((map.OurShips == 1) ? " ship" : " ships"), 4.2f, TextAnchor.MiddleRight, GameArt.Grey);
			y += 10f * p;
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			if (map.FleetKingdom == map.Home)
			{
				foreach (ShipOrder shipOrder in map.ShipOrders)
				{
					if (shipOrder.What == ShipOrder.Kind.Sail)
					{
						num2++;
					}
					else if (shipOrder.Holding == "out of reach")
					{
						num3++;
					}
					else
					{
						num++;
					}
				}
			}
			string text = ((map.OurShips == 0) ? "No ships of yours. Click a town of yours to order them." : ((num + num3 + num2 == 0) ? "Mark ships, then right-click: water to sail, an enemy to fire on it." : (((num > 0) ? (num + " firing") : "") + ((num3 > 0) ? (((num > 0) ? " · " : "") + num3 + " closing in") : "") + ((num2 > 0) ? (((num + num3 > 0) ? " · " : "") + num2 + " sailing") : ""))));
			GameArt.Label(new Rect(x + 2f * p, y, w - 4f * p, 9f * p), text, 4.1f, TextAnchor.MiddleLeft, (num > 0) ? new Color(1f, 0.55f, 0.45f) : GameArt.Grey);
			y += 10f * p;
			string text2 = ((map.FleetKingdom == map.Home) ? FerryLine(map) : null);
			if (text2 != null)
			{
				GameArt.Label(new Rect(x + 2f * p, y, w - 4f * p, 9f * p), text2, 4.1f, TextAnchor.MiddleLeft, BoardGreen);
				y += 10f * p;
			}
			float num4 = Mathf.Round((w - 3f * p) * 0.5f);
			float num5 = Mathf.Round(15f * p);
			if (GameButton(new Rect(x, y, num4, num5), "Mark ships", red: false, map.OurShips > 0, GameArt.IconNamed("iconBoat"), 4.4f) && map.MarkShips(Event.current.shift) == 0 && map.MarkedShips().Count == 0)
			{
				_mod.Toast("You have no ships on the map.");
			}
			bool enabled = map.FleetKingdom == map.Home && map.ShipOrders.Count > 0;
			if (GameButton(new Rect(x + num4 + 3f * p, y, num4, num5), "Cease fire", red: false, enabled, null, 4.4f))
			{
				List<Actor> list = new List<Actor>();
				foreach (Actor item in map.MarkedShips())
				{
					if (map.OrderOf(item) != null)
					{
						list.Add(item);
					}
				}
				if (list.Count == 0)
				{
					foreach (ShipOrder shipOrder2 in map.ShipOrders)
					{
						list.Add(shipOrder2.Ship);
					}
				}
				int kept;
				int num6 = map.CeaseFire(list, out kept);
				_mod.Toast((kept == 0) ? (num6 + ((num6 == 1) ? " ship leaves" : " ships leave") + " your orders and goes back to her own business.") : ((kept != num6) ? (num6 - kept + " go back to their own business. " + kept + " with soldiers aboard hold where they are") : ((num6 == 1) ? "Your ship holds her fire and keeps her soldiers aboard" : ("Your " + num6 + " ships hold their fire and keep their soldiers aboard"))));
			}
			return y + num5 + 4f * p;
		}

		public void DrawWarMap()
		{
			EnsureAssets();
			WarMap map = _mod.WarMap;
			Actor host = _mod.Host;
			if (host == null || !Royal.IsKing(host))
			{
				map.Open = false;
				return;
			}
			Event current = Event.current;
			bool flag = current.type == EventType.Repaint;
			bool close;
			Rect rect = MapWindow("War Map", out close);
			if (close)
			{
				map.Open = false;
				return;
			}
			float p = GameArt.P;
			_ = MapWindowRectForTest;
			OrderKeys(map, host, current, delegate(Vector2 centre)
			{
				map.Centre = centre;
			});
			float num = Mathf.Round(172f * p);
			Rect r = new Rect(rect.x + num + 8f * p, rect.y, rect.xMax - (rect.x + num + 8f * p), rect.height);
			GameArt.Sliced(r, GameArt.Inner, Color.white);
			Rect mapRect = new Rect(Mathf.Round(r.x + 4f * p), Mathf.Round(r.y + 4f * p), Mathf.Round(r.width - 8f * p), Mathf.Round(r.height - 8f * p));
			WarMapRectForTest = mapRect;
			if (flag)
			{
				PanWithKeys(map, mapRect);
			}
			float num2 = map.Span / Mathf.Max(0.01f, map.Zoom);
			float ppt = mapRect.width / num2;
			float viewH = mapRect.height / ppt;
			Vector2 vector = ClampCentre(map, num2, viewH);
			float vx0 = vector.x - num2 * 0.5f;
			float vy0 = vector.y - viewH * 0.5f;
			map.WantView = new Rect(vx0, vy0, num2, viewH);
			map.WantPixels = mapRect.width;
			WarMapToScreenForTest = ToScreen;
			Actor actor = null;
			WarFlag hoverFlag = null;
			if (flag)
			{
				Fill(mapRect, new Color(0.06f, 0.08f, 0.13f));
				if (map.Terrain != null && map.TexSize > 0f)
				{
					float num3 = Mathf.Max(vx0, map.TexX);
					float num4 = Mathf.Max(vy0, map.TexY);
					float num5 = Mathf.Min(vx0 + num2, map.TexX + map.TexSize);
					float num6 = Mathf.Min(vy0 + viewH, map.TexY + map.TexSize);
					if (num5 > num3 && num6 > num4)
					{
						Vector2 vector2 = ToScreen(new Vector2(num3, num6));
						Vector2 vector3 = ToScreen(new Vector2(num5, num4));
						GUI.DrawTextureWithTexCoords(texCoords: new Rect((num3 - map.TexX) / map.TexSize, (num4 - map.TexY) / map.TexSize, (num5 - num3) / map.TexSize, (num6 - num4) / map.TexSize), position: Rect.MinMaxRect(vector2.x, vector2.y, vector3.x, vector3.y), image: map.Terrain);
					}
				}
				DrawTerritoryFill(map, ToScreen, vx0, vy0, num2, viewH, ppt);
				actor = DrawMapContents(map, host, mapRect, ToScreen, ToWorld, ppt, current, out hoverFlag);
				DrawLayerKey(map, mapRect);
			}
			DrawMapButtons(map, host, mapRect, num2);
			SceneButton(mapRect, ToScreen, map.Centre, MapTabs.Page.War);
			DrawOrderBar(map, host, mapRect.center.x, mapRect.yMax - 4f * p, mapRect.width - 8f * p, delegate(Vector2 centre)
			{
				map.Centre = centre;
			});
			if (_cardCity != null)
			{
				DrawTownCard(map, host, ToScreen(map.TownPoint(_cardCity)), mapRect);
			}
			else
			{
				_townCard = default(Rect);
			}
			HandleMapInput(map, host, mapRect, ToWorld, ToScreen, ppt, current);
			bool flag2 = OverOrderUi(current.mousePosition);
			if (flag2)
			{
				actor = null;
				hoverFlag = null;
			}
			ShipOrder shipOrder = ((flag && hoverFlag == null && _mapButton < 0 && mapRect.Contains(current.mousePosition) && !flag2) ? ReticleUnder(map, ToScreen, current.mousePosition) : null);
			int num7 = ((flag && hoverFlag == null && shipOrder == null && _mapButton < 0 && mapRect.Contains(current.mousePosition) && !flag2) ? TownUnder(map, ToScreen, current.mousePosition, ppt) : (-1));
			if (num7 >= 0 && actor != null && actor.kingdom == host.kingdom && (ToScreen(actor.current_position) - current.mousePosition).sqrMagnitude <= 16f * p * p)
			{
				num7 = -1;
			}
			if (num7 >= 0)
			{
				TownTip(map, map.Towns[num7], current.mousePosition);
			}
			else if (flag && hoverFlag != null && mapRect.Contains(current.mousePosition) && _mapButton < 0)
			{
				FlagTip(map, hoverFlag, current.mousePosition);
			}
			else if (shipOrder != null)
			{
				ReticleTip(map, shipOrder, current.mousePosition);
			}
			else if (flag && actor != null && WarMap.IsShip(actor) && mapRect.Contains(current.mousePosition) && _mapButton < 0)
			{
				ShipTip(map, actor, current.mousePosition);
			}
			else if (flag && actor != null && mapRect.Contains(current.mousePosition) && _mapButton < 0)
			{
				string text = WorldBoxMod.Describe(actor);
				string text2 = Favorites.Doing(actor);
				string text3 = "";
				try
				{
					text3 = actor.getHealth() + "/" + actor.getMaxHealth() + " hp";
				}
				catch
				{
				}
				float width = Mathf.Max(GameArt.Measure(text, 5f).x, GameArt.Measure(text2 + "   " + text3, 4.2f).x) + 12f * p;
				Rect r2 = new Rect(current.mousePosition.x + 12f * p, current.mousePosition.y + 8f * p, width, 21f * p);
				if (r2.xMax > (float)Screen.width)
				{
					r2.x = current.mousePosition.x - r2.width - 8f * p;
				}
				GameArt.TipPanel(r2);
				GameArt.Label(new Rect(r2.x + 6f * p, r2.y + 1.5f * p, r2.width, 9f * p), text, 5f, TextAnchor.MiddleLeft, Color.white);
				GameArt.Label(new Rect(r2.x + 6f * p, r2.y + 10.5f * p, r2.width, 8f * p), text2 + "   " + text3, 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
			}
			DrawWarPanel(map, host, new Rect(rect.x, rect.y, num, rect.height));
			Vector2 ToScreen(Vector2 vector4)
			{
				return new Vector2(mapRect.x + (vector4.x - vx0) * ppt, mapRect.y + (vy0 + viewH - vector4.y) * ppt);
			}
			Vector2 ToWorld(Vector2 s)
			{
				return new Vector2(vx0 + (s.x - mapRect.x) / ppt, vy0 + viewH - (s.y - mapRect.y) / ppt);
			}
		}

		private Actor DrawMapContents(WarMap map, Actor host, Rect mapRect, Func<Vector2, Vector2> toScreen, Func<Vector2, Vector2> toWorld, float ppt, Event ev, out WarFlag hoverFlag)
		{
			float p = GameArt.P;
			float unscaledTime = Time.unscaledTime;
			GUI.BeginGroup(mapRect);
			Vector2 origin = new Vector2(mapRect.x, mapRect.y);
			Vector2 mousePosition = ev.mousePosition;
			float num = 0.5f + 0.5f * Mathf.Sin(unscaledTime * 5f);
			DrawBorders(map, (Vector2 w) => toScreen(w) - origin, mapRect, num);
			DrawRealmLinks(map, (Vector2 w) => toScreen(w) - origin, num);
			float num2 = Mathf.Round(Mathf.Clamp(ppt * 4f, 11f * p, 18f * p));
			foreach (WarMap.Town town in map.Towns)
			{
				Vector2 s = toScreen(town.P) - origin;
				if (!In(s, 60f * p))
				{
					continue;
				}
				Kingdom owner = town.Owner;
				bool flag = town.Side == WarMap.Side.Neutral && owner != map.Picked;
				float num3 = (flag ? Mathf.Round(num2 * 0.8f) : num2);
				float num4 = (flag ? 0.75f : 1f);
				Rect r = new Rect(Mathf.Round(s.x - num3 * 0.4f), Mathf.Round(s.y - num3 * 0.85f), Mathf.Round(num3 * 0.8f), num3);
				if (!DrawBanner(r, owner, owner != null, num4))
				{
					GameArt.Flat(new Rect(s.x - 4f * p, s.y - 4f * p, 8f * p, 8f * p), new Color(0f, 0f, 0f, 0.7f));
					GameArt.Flat(new Rect(s.x - 3f * p, s.y - 3f * p, 6f * p, 6f * p), town.Color);
				}
				if (town.Capital)
				{
					GameArt.Icon(new Rect(Mathf.Round(s.x - 5f * p), Mathf.Round(r.y - 9f * p), 10f * p, 10f * p), GameArt.Crown, new Color(1f, 1f, 1f, num4));
				}
				Color color = Color.white;
				try
				{
					if (owner != null)
					{
						color = WarMap.Readable(owner.getColor().getColorText());
					}
				}
				catch
				{
				}
				color.a = num4;
				GameArt.Label(new Rect(s.x - 60f * p, r.yMax, 120f * p, 9f * p), town.Name, flag ? 4f : 4.4f, TextAnchor.UpperCenter, color);
				if (town.Objective)
				{
					float num5 = Mathf.Round(11f * p * (1f + 0.15f * num));
					GameArt.Icon(new Rect(Mathf.Round(r.xMax), Mathf.Round(r.y - num5 * 0.6f), num5, num5), GameArt.Warfare);
				}
			}
			foreach (Vector2 otherClash in map.OtherClashes)
			{
				Vector2 s2 = toScreen(otherClash) - origin;
				if (In(s2, 20f * p))
				{
					float num6 = Mathf.Round(9f * p);
					GameArt.Icon(new Rect(Mathf.Round(s2.x - num6 * 0.5f), Mathf.Round(s2.y - num6 * 0.5f), num6, num6), GameArt.Warfare, new Color(1f, 1f, 1f, 0.6f));
				}
			}
			foreach (Vector2 clash in map.Clashes)
			{
				Vector2 s3 = toScreen(clash) - origin;
				if (In(s3, 20f * p))
				{
					float num7 = Mathf.Round(13f * p * (1f + 0.18f * num));
					GameArt.Icon(new Rect(Mathf.Round(s3.x - num7 * 0.5f + p), Mathf.Round(s3.y - num7 * 0.5f + p), num7, num7), GameArt.Warfare, new Color(0f, 0f, 0f, 0.5f));
					GameArt.Icon(new Rect(Mathf.Round(s3.x - num7 * 0.5f), Mathf.Round(s3.y - num7 * 0.5f), num7, num7), GameArt.Warfare);
				}
			}
			bool flag2 = map.FlagsKingdom == map.Home && map.Home != null;
			if (flag2)
			{
				foreach (WarFlag flag8 in map.Flags)
				{
					if (flag8.Coming > 0)
					{
						MarchLine(toScreen(flag8.ComingFrom) - origin, toScreen(flag8.P) - origin, flag8.Color);
					}
				}
				WarFlag warFlag = ((map.SelectedFlag != null && map.Flags.Contains(map.SelectedFlag)) ? map.SelectedFlag : null);
				if (warFlag != null && (!_areaOn || _areaFlag != warFlag))
				{
					MapRing(toScreen(warFlag.P) - origin, warFlag.Radius * ppt, OrderTint(warFlag.Order), 0.75f);
				}
				foreach (WarFlag post in map.Posts)
				{
					Vector2 vector = toScreen(post.P) - origin;
					if (post.Coming > 0)
					{
						MarchLine(toScreen(post.ComingFrom) - origin, vector, OrderTint(post.Order));
					}
					if (In(vector, 20f * p))
					{
						Color col = OrderTint(post.Order);
						if (post.Target != null)
						{
							MapRing(vector, 5f * p, col, 0.9f);
						}
						PixelDiamond(vector, 1, new Color(col.r, col.g, col.b, 0.95f));
					}
				}
			}
			if (_areaOn && _mapButton == 1)
			{
				Color color2 = ((_areaFlag != null) ? OrderTint(_areaFlag.Order) : OrderTint(map.CurrentOrder));
				Vector2 c = toScreen(_areaFrom) - origin;
				MapRing(c, _areaRadius * ppt, color2, 0.95f);
				if (_areaMoved)
				{
					GameArt.Label(new Rect(c.x - 30f * p, c.y - _areaRadius * ppt - 10f * p, 60f * p, 8f * p), Mathf.RoundToInt(_areaRadius) + " tiles", 4.4f, TextAnchor.MiddleCenter, color2);
				}
			}
			DrawFleetOrders(map, (Vector2 w) => toScreen(w) - origin, ppt, num);
			Rect r2 = default(Rect);
			bool flag3 = _mapButton == 0 && _mapDragged && _flagDrag == null;
			if (flag3)
			{
				Vector2 vector2 = _mapDragFrom - origin;
				r2 = Rect.MinMaxRect(Mathf.Min(vector2.x, mousePosition.x), Mathf.Min(vector2.y, mousePosition.y), Mathf.Max(vector2.x, mousePosition.x), Mathf.Max(vector2.y, mousePosition.y));
			}
			Actor result = null;
			float num8 = 9f * p * 9f * p;
			float num9 = Mathf.Round(Mathf.Clamp(ppt * 1.1f, 3f * p * 0.9f, 5f * p));
			WarMap.Side[] array = new WarMap.Side[4]
			{
				WarMap.Side.Neutral,
				WarMap.Side.Enemy,
				WarMap.Side.Ally,
				WarMap.Side.Own
			};
			for (int num10 = 0; num10 < array.Length; num10++)
			{
				foreach (WarMap.Unit unit in map.Units)
				{
					if (unit.Side != array[num10])
					{
						continue;
					}
					Vector2 vector3 = toScreen(unit.P) - origin;
					if (!In(vector3, 10f * p))
					{
						continue;
					}
					bool flag4 = map.Marked.Contains(unit.Actor) || (flag3 && unit.Ours && (unit.Warrior || unit.Ship) && r2.Contains(vector3));
					float num11 = Mathf.Round(vector3.x);
					float num12 = Mathf.Round(vector3.y);
					if (unit.Ship)
					{
						DrawShipGlyph(vector3, unit.Color, flag4, unit.Fighting, num);
						DrawShipLoad(vector3, WarMap.AboardCount(unit.Actor));
						float sqrMagnitude = (vector3 - mousePosition).sqrMagnitude;
						if (sqrMagnitude < num8)
						{
							num8 = sqrMagnitude;
							result = unit.Actor;
						}
						continue;
					}
					bool flag5 = unit.Side == WarMap.Side.Neutral && !flag4 && unit.Actor.kingdom != map.Picked;
					float num13 = (flag5 ? Mathf.Max(2f, Mathf.Round(num9 * 0.7f)) : num9);
					if (flag4)
					{
						float num14 = num13 + 4f * p;
						GameArt.Flat(new Rect(num11 - num14 * 0.5f, num12 - num14 * 0.5f, num14, num14), new Color(1f, 0.85f, 0.25f, 0.95f));
					}
					Color c2 = ((unit.Fighting && !flag5) ? new Color(0.95f, 0.15f, 0.1f, 0.6f + 0.4f * num) : ((unit.Side == WarMap.Side.Ally) ? new Color(0.1f, 0.42f, 0.12f, 0.95f) : ((unit.Side == WarMap.Side.Enemy) ? new Color(0.42f, 0.05f, 0.04f, 0.95f) : new Color(0.05f, 0.04f, 0.04f, flag5 ? 0.5f : 0.95f))));
					Color color3 = unit.Color;
					if (flag5)
					{
						color3.a = 0.6f;
					}
					GameArt.Flat(new Rect(num11 - num13 * 0.5f - p, num12 - num13 * 0.5f - p, num13 + 2f * p, num13 + 2f * p), c2);
					GameArt.Flat(new Rect(num11 - num13 * 0.5f, num12 - num13 * 0.5f, num13, num13), color3);
					if (unit.King)
					{
						GameArt.Icon(new Rect(num11 - 5f * p, num12 - num13 * 0.5f - 9f * p, 10f * p, 10f * p), GameArt.Crown, new Color(1f, 1f, 1f, flag5 ? 0.7f : 1f));
					}
					else if (unit.Ours && flag2)
					{
						WarFlag warFlag2 = map.FlagOf(unit.Actor);
						if (warFlag2 != null)
						{
							BandPip(num11, num12, num9, warFlag2.Color);
						}
					}
					float sqrMagnitude2 = (vector3 - mousePosition).sqrMagnitude;
					if (sqrMagnitude2 < num8)
					{
						num8 = sqrMagnitude2;
						result = unit.Actor;
					}
				}
			}
			hoverFlag = null;
			if (flag2)
			{
				bool flag6 = _flagDrag != null && _mapDragged;
				if (!flag3 && !flag6 && _mapButton < 0)
				{
					hoverFlag = FlagUnder(map, (Vector2 w) => toScreen(w) - origin, mousePosition);
				}
				foreach (WarFlag flag9 in map.Flags)
				{
					Vector2 vector4 = toScreen(flag9.P) - origin;
					if (In(vector4, 30f * p))
					{
						bool flag7 = flag6 && flag9 == _flagDrag;
						DrawFlagMarker(vector4, flag9, map.Home, 1f, flag7 ? 0.35f : 1f, flag9 == hoverFlag);
						FlagOrderBadge(new Vector2(vector4.x - 9f * p, vector4.y - 20f * p), flag9, 7f);
					}
				}
				if (flag6)
				{
					Vector2 vector5 = toScreen(_flagDrag.P) - origin;
					MarchLine(vector5, mousePosition + _flagGrab, _flagDrag.Color);
					DrawFlagMarker(mousePosition + _flagGrab, _flagDrag, map.Home, 1f, 0.9f, lit: true);
				}
				if (hoverFlag != null)
				{
					result = null;
				}
			}
			if (flag3)
			{
				GameArt.Flat(r2, new Color(1f, 0.85f, 0.3f, 0.12f));
				float num15 = Mathf.Max(1f, Mathf.Round(p * 0.8f));
				Color c3 = new Color(1f, 0.85f, 0.3f, 0.95f);
				GameArt.Flat(new Rect(r2.x, r2.y, r2.width, num15), c3);
				GameArt.Flat(new Rect(r2.x, r2.yMax - num15, r2.width, num15), c3);
				GameArt.Flat(new Rect(r2.x, r2.y, num15, r2.height), c3);
				GameArt.Flat(new Rect(r2.xMax - num15, r2.y, num15, r2.height), c3);
			}
			Vector2 vector6 = toScreen(host.current_position) - origin;
			float viewYaw = _mod.ViewYaw;
			Line(vector6, vector6 + new Vector2(Mathf.Cos(viewYaw), 0f - Mathf.Sin(viewYaw)) * 14f * p, Mathf.Max(2f, p), new Color(1f, 1f, 1f, 0.95f));
			PixelDiamond(vector6, 2, Color.white);
			GUI.EndGroup();
			return result;
			bool In(Vector2 vector7, float m)
			{
				if (vector7.x > 0f - m && vector7.y > 0f - m && vector7.x < mapRect.width + m)
				{
					return vector7.y < mapRect.height + m;
				}
				return false;
			}
		}

		private void DrawMapButtons(WarMap map, Actor host, Rect mapRect, float viewW)
		{
			float p = GameArt.P;
			float num = Mathf.Round(16f * p);
			float x = mapRect.xMax - num - 4f * p;
			float num2 = mapRect.y + 4f * p;
			string tip = null;
			Rect tipAt = default(Rect);
			Rect r = new Rect(x, num2, num, num);
			Tip(r, "Zoom in");
			if (GameButton(r, "+", red: false, map.Zoom < map.ZoomMax - 0.001f, null, 7f))
			{
				ZoomAbout(map, mapRect, mapRect.center, 1.5f);
			}
			Rect r2 = new Rect(x, num2 + num + 2f * p, num, num);
			Tip(r2, "Zoom out");
			if (GameButton(r2, "−", red: false, map.Zoom > map.ZoomMin + 0.001f, null, 7f))
			{
				ZoomAbout(map, mapRect, mapRect.center, 2f / 3f);
			}
			Rect r3 = new Rect(x, num2 + (num + 2f * p) * 2f + 3f * p, num, num);
			Tip(r3, "Back to the armies");
			if (GameButton(r3, null, red: false, enabled: true, GameArt.IconNamed("iconArmy") ?? GameArt.Warfare))
			{
				map.Fit();
			}
			Rect r4 = new Rect(x, num2 + (num + 2f * p) * 3f + 3f * p, num, num);
			Tip(r4, "The whole world");
			if (GameButton(r4, null, red: false, enabled: true, GameArt.IconNamed("iconWorldInfo")))
			{
				map.Zoom = map.ZoomMin;
				map.Centre = new Vector2(WarMap.WorldWidth * 0.5f, WarMap.WorldHeight * 0.5f);
			}
			Rect r5 = new Rect(x, num2 + (num + 2f * p) * 4f + 3f * p, num, num);
			Tip(r5, "Centre on you");
			if (GameButton(r5, null, red: false, enabled: true, GameArt.IconNamed("iconInspect")))
			{
				map.Centre = host.current_position;
			}
			DrawLayerButtons(map, x, num2 + (num + 2f * p) * 5f + 6f * p, num, ref tip, ref tipAt);
			if (tip != null && Event.current.type == EventType.Repaint)
			{
				float num3 = GameArt.Measure(tip, 4.6f).x + 10f * p;
				Rect r6 = new Rect(tipAt.x - num3 - 3f * p, tipAt.y + 2f * p, num3, 12f * p);
				GameArt.TipPanel(r6);
				GameArt.Label(r6, tip, 4.6f, TextAnchor.MiddleCenter, Color.white);
			}
			void Tip(Rect rect, string text)
			{
				if (rect.Contains(Event.current.mousePosition))
				{
					tip = text;
					tipAt = rect;
				}
			}
		}

		private void PanWithKeys(WarMap map, Rect mapRect)
		{
			if (!_mod.IsTypingNow)
			{
				Vector2 zero = Vector2.zero;
				if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
				{
					zero.x -= 1f;
				}
				if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
				{
					zero.x += 1f;
				}
				if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
				{
					zero.y += 1f;
				}
				if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
				{
					zero.y -= 1f;
				}
				if (!(zero == Vector2.zero))
				{
					float num = map.Span / Mathf.Max(0.01f, map.Zoom);
					map.Centre += zero.normalized * num * 0.8f * Mathf.Min(0.1f, Time.unscaledDeltaTime);
				}
			}
		}

		private Vector2 ClampCentre(WarMap map, float viewW, float viewH)
		{
			map.Centre = new Vector2(Axis(map.Centre.x, WarMap.WorldWidth, viewW), Axis(map.Centre.y, WarMap.WorldHeight, viewH));
			return map.Centre;
			float Axis(float c, float world, float view)
			{
				if (!(view > world))
				{
					return Mathf.Clamp(c, 0f, world);
				}
				return Mathf.Clamp(c, world - view * 0.5f, view * 0.5f);
			}
		}

		private void ZoomAbout(WarMap map, Rect mapRect, Vector2 at, float factor)
		{
			float num = map.Span / Mathf.Max(0.01f, map.Zoom);
			float num2 = mapRect.width / num;
			float num3 = mapRect.height / num2;
			Vector2 vector = new Vector2(map.Centre.x - num * 0.5f + (at.x - mapRect.x) / num2, map.Centre.y + num3 * 0.5f - (at.y - mapRect.y) / num2);
			map.Zoom = Mathf.Clamp(map.Zoom * factor, map.ZoomMin, map.ZoomMax);
			float num4 = map.Span / map.Zoom;
			float num5 = mapRect.width / num4;
			map.Centre = new Vector2(vector.x - (at.x - mapRect.center.x) / num5, vector.y + (at.y - mapRect.center.y) / num5);
		}

		private void HandleMapInput(WarMap map, Actor host, Rect mapRect, Func<Vector2, Vector2> toWorld, Func<Vector2, Vector2> toScreen, float ppt, Event ev)
		{
			float p = GameArt.P;
			switch (ev.type)
			{
			case EventType.ScrollWheel:
				if (mapRect.Contains(ev.mousePosition) && !OverOrderUi(ev.mousePosition))
				{
					ZoomAbout(map, mapRect, ev.mousePosition, (ev.delta.y > 0f) ? (5f / 6f) : 1.2f);
					_mapDragFrom = ev.mousePosition;
					_mapCentreFrom = map.Centre;
					ev.Use();
				}
				break;
			case EventType.MouseDown:
				if (!mapRect.Contains(ev.mousePosition) || ev.button > 2 || OverOrderUi(ev.mousePosition))
				{
					break;
				}
				_mapButton = ev.button;
				_mapDragFrom = ev.mousePosition;
				_mapDragged = false;
				_mapCentreFrom = map.Centre;
				_mapShift = ev.button == 1 && ev.shift;
				if (_mapShift)
				{
					AreaPress(map, toWorld(ev.mousePosition), (map.FlagsKingdom == host.kingdom) ? FlagUnder(map, toScreen, ev.mousePosition) : null);
				}
				_flagDrag = ((ev.button == 0 && map.FlagsKingdom == host.kingdom) ? FlagUnder(map, toScreen, ev.mousePosition) : null);
				if (_flagDrag != null)
				{
					_flagGrab = toScreen(_flagDrag.P) - ev.mousePosition;
				}
				if (_flagDrag == null && ev.button == 0 && ev.clickCount == 2)
				{
					Vector2 vector6 = toWorld(new Vector2(mapRect.x, mapRect.yMax));
					Vector2 vector7 = toWorld(new Vector2(mapRect.xMax, mapRect.y));
					int num6 = map.MarkIn(Rect.MinMaxRect(vector6.x, vector6.y, vector7.x, vector7.y));
					if (num6 > 0)
					{
						Sounds.PlayUI("event:/SFX/Ui/Click");
						_mod.Toast(num6 + " more of your warriors selected");
						map.SelectedFlag = null;
					}
					_mapButton = -1;
				}
				ev.Use();
				break;
			case EventType.MouseDrag:
				if (_mapButton >= 0)
				{
					if ((ev.mousePosition - _mapDragFrom).sqrMagnitude > 36f)
					{
						_mapDragged = true;
					}
					if (_mapButton == 1 && _mapShift)
					{
						AreaMove(toWorld(ev.mousePosition), _mapDragged);
					}
					else if (_mapButton >= 1 && _mapDragged)
					{
						Vector2 vector5 = ev.mousePosition - _mapDragFrom;
						map.Centre = _mapCentreFrom - new Vector2(vector5.x / ppt, (0f - vector5.y) / ppt);
					}
					ev.Use();
				}
				break;
			case EventType.MouseUp:
			{
				if (_mapButton < 0)
				{
					break;
				}
				int mapButton = _mapButton;
				_mapButton = -1;
				WarFlag flagDrag = _flagDrag;
				_flagDrag = null;
				if (mapButton == 1 && _mapShift)
				{
					_mapShift = false;
					AreaMove(toWorld(ev.mousePosition), _mapDragged);
					AreaRelease(map, host);
				}
				else if (mapButton == 0 && flagDrag != null && map.Flags.Contains(flagDrag))
				{
					if (!_mapDragged)
					{
						PickBand(map, flagDrag, ev.shift);
					}
					else
					{
						Vector2 vector = toWorld(ev.mousePosition + _flagGrab);
						WorldTile worldTile = null;
						try
						{
							worldTile = WarMap.GroundFor(World.world.GetTile(Mathf.FloorToInt(vector.x), Mathf.FloorToInt(vector.y)));
						}
						catch
						{
						}
						if (worldTile == null)
						{
							_mod.Toast("No firm ground there for flag " + flagDrag.Number + ".");
						}
						else
						{
							map.Move(flagDrag, worldTile);
							Sounds.PlayUI("event:/SFX/Ui/Click");
							if (flagDrag.Band.Count > 0)
							{
								_mod.Toast("Flag " + flagDrag.Number + " moves: its " + flagDrag.Band.Count + ((flagDrag.Band.Count == 1) ? " warrior marches" : " warriors march") + " to hold the new ground.");
								_mod.PlayHorn();
							}
						}
					}
				}
				else
				{
					switch (mapButton)
					{
					case 0:
						if (!_mapDragged)
						{
							Actor actor3 = null;
							float num4 = 9f * p * 9f * p;
							foreach (WarMap.Unit unit in map.Units)
							{
								float sqrMagnitude = (toScreen(unit.P) - ev.mousePosition).sqrMagnitude;
								if (sqrMagnitude < num4)
								{
									num4 = sqrMagnitude;
									actor3 = unit.Actor;
								}
							}
							int num5 = TownUnder(map, toScreen, ev.mousePosition, ppt);
							if (actor3 != null && num5 >= 0 && (num4 > 4f * p * 4f * p || actor3.kingdom != host.kingdom))
							{
								actor3 = null;
							}
							if (actor3 != null)
							{
								ClickUnit(map, host, actor3, ev.shift);
							}
							else if (num5 >= 0)
							{
								map.Pick(map.Towns[num5].Owner, goThere: false);
								_cardCity = map.Towns[num5].City;
								Sounds.PlayUI("event:/SFX/Ui/Click");
							}
							else if (_cardCity != null)
							{
								_cardCity = null;
								Sounds.PlayUI("event:/SFX/Ui/Click");
							}
							else if (!ev.shift && map.Marked.Count > 0)
							{
								map.Marked.Clear();
								map.SelectedFlag = null;
								Sounds.PlayUI("event:/SFX/Ui/Click");
							}
							else if (map.Picked != null)
							{
								map.Picked = null;
								Sounds.PlayUI("event:/SFX/Ui/Click");
							}
						}
						else
						{
							Vector2 vector3 = toWorld(_mapDragFrom);
							Vector2 vector4 = toWorld(ev.mousePosition);
							if (!ev.shift)
							{
								map.Marked.Clear();
								map.SelectedFlag = null;
							}
							if (map.MarkIn(Rect.MinMaxRect(Mathf.Min(vector3.x, vector4.x), Mathf.Min(vector3.y, vector4.y), Mathf.Max(vector3.x, vector4.x), Mathf.Max(vector3.y, vector4.y))) > 0)
							{
								Sounds.PlayUI("event:/SFX/Ui/Click");
							}
						}
						break;
					case 1:
					{
						if (_mapDragged)
						{
							break;
						}
						List<Actor> list = map.MarkedShips();
						bool flag = map.MarkedOwn().Count > 0;
						Vector2 vector2 = toWorld(ev.mousePosition);
						Actor actor = UnitUnder(map, toScreen, ev.mousePosition);
						WarFlag warFlag = ((map.FlagsKingdom == host.kingdom) ? FlagUnder(map, toScreen, ev.mousePosition) : null);
						Actor actor2 = (flag ? OwnShipUnder(map, toScreen, ev.mousePosition) : null);
						bool num = ((actor2 != null) ? BoardOrder(map, host, actor2) : (flag && warFlag == null && BoardOrder(map, host, actor)));
						WarFlag landFlag = null;
						string said = null;
						if (!num && list.Count > 0)
						{
							List<Actor> list2 = LandOrder(map, host, list, vector2, actor, warFlag, ev.control, out landFlag, out said);
							if (list2.Count > 0)
							{
								FleetOrder(map, host, list2, vector2, actor, ev.control);
							}
						}
						if (!num && (flag || list.Count == 0))
						{
							if (warFlag == null)
							{
								warFlag = landFlag;
							}
							ShipOrder shipOrder = ((list.Count == 0 && !flag) ? ReticleUnder(map, toScreen, ev.mousePosition) : null);
							if (warFlag != null)
							{
								if (flag)
								{
									SendToFlag(map, host, warFlag);
								}
								else
								{
									PullUpFlag(map, warFlag);
								}
							}
							else if (shipOrder != null)
							{
								string targetName = shipOrder.TargetName;
								int num2 = map.CeaseFireOn(shipOrder);
								_mod.Toast(((num2 == 1) ? "Your ship holds her fire" : ("Your " + num2 + " ships hold their fire")) + " on " + targetName + ".");
							}
							else if ((list.Count <= 0 || !ev.control) && (flag || list.Count == 0))
							{
								int num3 = TownUnder(map, toScreen, ev.mousePosition, ppt);
								if (num3 >= 0 && actor != null && actor.kingdom == host.kingdom && (toScreen(actor.current_position) - ev.mousePosition).sqrMagnitude <= 16f * p * p)
								{
									num3 = -1;
								}
								RightOrder(map, host, vector2, actor, (num3 >= 0) ? new WarMap.Town?(map.Towns[num3]) : ((WarMap.Town?)null));
							}
						}
						if (said != null && _mod.ToastText != said)
						{
							_mod.Toast(said + ". " + _mod.ToastText);
						}
						Sounds.PlayUI("event:/SFX/Ui/Click");
						break;
					}
					}
				}
				_mapDragged = false;
				ev.Use();
				break;
			}
			case EventType.MouseMove:
			case EventType.KeyDown:
			case EventType.KeyUp:
				break;
			}
		}

		private void DrawWarPanel(WarMap map, Actor host, Rect panel)
		{
			float p = GameArt.P;
			float num = Mathf.Round(16f * p);
			float num2 = Mathf.Round((panel.width - 3f * p) * 0.5f);
			if (GameButton(new Rect(panel.x, panel.y, num2, num), "Realms", map.Tab == 0, enabled: true, GameArt.IconNamed("iconDiplomacy"), 4.8f))
			{
				map.Tab = 0;
			}
			if (GameButton(new Rect(panel.x + num2 + 3f * p, panel.y, panel.width - num2 - 3f * p, num), "Orders", map.Tab == 1, enabled: true, GameArt.IconNamed("iconArmy"), 4.8f))
			{
				map.Tab = 1;
			}
			Rect panel2 = new Rect(panel.x, panel.y + num + 4f * p, panel.width, panel.height - num - 4f * p);
			if (map.Tab == 0)
			{
				DrawRealmsPage(map, host, panel2);
			}
			else
			{
				DrawOrdersPage(map, host, panel2);
			}
		}

		private void DrawOrdersPage(WarMap map, Actor host, Rect panel)
		{
			float k = GameArt.P;
			float x = panel.x;
			float width = panel.width;
			float num = panel.y;
			bool flag = Event.current.type == EventType.Repaint;
			if (map.Wars.Count > 1)
			{
				float num2 = Mathf.Round(15f * k);
				if (GameButton(new Rect(x, num, num2, num2), "<", red: false))
				{
					map.FocusWar(map.Wars[((map.WarIndex - 1) % map.Wars.Count + map.Wars.Count) % map.Wars.Count]);
				}
				if (GameButton(new Rect(x + width - num2, num, num2, num2), ">", red: false))
				{
					map.FocusWar(map.Wars[((map.WarIndex + 1) % map.Wars.Count + map.Wars.Count) % map.Wars.Count]);
				}
				GameArt.Label(new Rect(x, num, width, num2), "War " + ((map.WarIndex % map.Wars.Count + map.Wars.Count) % map.Wars.Count + 1) + " of " + map.Wars.Count, 5f, TextAnchor.MiddleCenter, Color.white);
				num += num2 + 3f * k;
			}
			float num3 = Mathf.Round(28f * k);
			if (flag)
			{
				KingdomCard(new Rect(x, num, width, num3), map.Home, map.OurWarriors + " warriors" + ((map.OurShips > 0) ? ("  ·  " + map.OurShips + ((map.OurShips == 1) ? " ship" : " ships")) : "") + ((map.OurFighting > 0) ? ("  ·  " + map.OurFighting + " fighting") : ""), picked: false, new Color(0.6f, 1f, 0.55f));
			}
			num += num3 + 3f * k;
			if (map.Enemy != null)
			{
				if (flag)
				{
					KingdomCard(new Rect(x, num, width, num3), map.Enemy, map.TheirWarriors + " warriors" + ((map.TheirShips > 0) ? ("  ·  " + map.TheirShips + ((map.TheirShips == 1) ? " ship" : " ships")) : "") + ((map.TheirFighting > 0) ? ("  ·  " + map.TheirFighting + " fighting") : ""), picked: false, new Color(1f, 0.5f, 0.4f));
				}
				num += num3 + 3f * k;
			}
			else
			{
				GameArt.Label(new Rect(x + 2f * k, num, width - 4f * k, 16f * k), "You are at peace. Declare war on the Council tab.", 4.4f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
				num += 17f * k;
			}
			Royal.Campaign active = _mod.Royal.Active;
			if (active != null && active.Status != null)
			{
				GameArt.Label(new Rect(x + 2f * k, num, width - 4f * k, 16f * k), active.Status, 4.3f, TextAnchor.UpperLeft, GameArt.Peach, wrap: true);
				num += 17f * k;
			}
			if (map.Clashes.Count > 0)
			{
				GameArt.Label(new Rect(x + 2f * k, num, width - 18f * k, 9f * k), map.Clashes.Count + ((map.Clashes.Count == 1) ? " battle raging" : " battles raging"), 4.6f, TextAnchor.MiddleLeft, new Color(1f, 0.5f, 0.4f));
				if (SceneEye(new Rect(x + width - 13f * k, num - 1.5f * k, 13f * k, 12f * k), "View the battle in 3D"))
				{
					Vector2? vector = _mod.NextSceneBattle(map.Clashes, map.Centre, fresh: true);
					if (vector.HasValue)
					{
						_mod.EnterScene(MapTabs.Page.War, vector.Value);
					}
				}
				num += 12f * k;
			}
			num += 2f * k;
			SectionTitle(new Rect(x, num, width, Mathf.Round(20f * k)), "Orders", GameArt.IconNamed("iconArmy"));
			num += 22f * k;
			int count = map.MarkedOwn().Count;
			int count2 = map.MarkedShips().Count;
			GameArt.Label(new Rect(x + 2f * k, num, width, 9f * k), map.Marked.Count + " selected" + ((map.Marked.Count > count + count2) ? ("  (" + (count + count2) + " yours)") : "") + ((count2 > 0) ? ("  ·  " + count2 + ((count2 == 1) ? " ship" : " ships")) : ""), 5f, TextAnchor.MiddleLeft, GameArt.AgeYellow);
			num += 11f * k;
			float num4 = Mathf.Round((width - 3f * k) * 0.5f);
			float num5 = Mathf.Round(16f * k);
			if (GameButton(new Rect(x, num, num4, num5), "Select all", red: false, enabled: true, null, 4.4f))
			{
				map.MarkAll();
				map.SelectedFlag = null;
			}
			if (GameButton(new Rect(x + num4 + 3f * k, num, num4, num5), "Clear", red: false, map.Marked.Count > 0, null, 4.4f))
			{
				map.Marked.Clear();
				map.SelectedFlag = null;
			}
			num += num5 + 3f * k;
			List<Actor> list = map.MarkedOwn();
			int num6 = 0;
			foreach (Actor item in list)
			{
				if (map.CommandOf(item) != null)
				{
					num6++;
				}
			}
			if (GameButton(new Rect(x, num, num4, num5), "Rally to me", red: true, list.Count > 0, null, 4.4f))
			{
				map.StandDown(list);
				int num7 = _mod.Royal.SendTo(host, list, host.current_tile, _mod.Toast);
				_mod.Toast(((num7 > 0) ? (num7 + " selected warriors come to your side.") : "None of the selected can come.") + Afterword(map, 0));
			}
			if (GameButton(new Rect(x + num4 + 3f * k, num, num4, num5), "Stand down", red: false, num6 > 0, null, 4.4f))
			{
				int num8 = map.StandDown(list);
				_mod.Toast(num8 + ((num8 == 1) ? " warrior goes" : " warriors go") + " back to their usual duties." + Afterword(map, 0));
			}
			num += num5 + 5f * k;
			num = DrawFleetPanel(map, host, x, num, width);
			num = DrawFlagRows(map, host, x, num, width) + 3f * k;
			float kx;
			float ky;
			float col;
			float row;
			float sq;
			if (flag)
			{
				float num9 = Mathf.Round(68f * k);
				Rect r = new Rect(x, num, width, num9);
				GameArt.Sliced(r, GameArt.Inner, new Color(1f, 1f, 1f, 0.6f));
				kx = r.x + 6f * k;
				ky = r.y + 4f * k;
				col = (width - 12f * k) * 0.5f;
				row = 10f * k;
				sq = Mathf.Round(4f * k);
				Color color = Royal.ColorOf(map.Home);
				Color c = ((map.Enemy != null) ? Royal.ColorOf(map.Enemy) : new Color(0.85f, 0.2f, 0.15f));
				Dot(kx + 1f * k, ky + 3f * k, color, ring: false);
				Entry(0, "Your warriors");
				Dot(kx + col + 1f * k, ky + 3f * k, c, ring: false, new Color(0.42f, 0.05f, 0.04f, 0.95f));
				Entry(1, "Enemies");
				Dot(kx + 1f * k, ky + row + 3f * k, color, ring: true);
				Entry(2, "Selected");
				GameArt.Icon(new Rect(kx + col - 1f * k, ky + row, 9f * k, 9f * k), GameArt.Warfare);
				Entry(3, "A battle");
				DrawFlagMini(new Vector2(kx + 1f * k, ky + row * 2f + 8.5f * k), KeyFlag, 1f);
				Entry(4, "A flag");
				GameArt.Icon(new Rect(kx + col - 1f * k, ky + row * 2f, 9f * k, 9f * k), GameArt.Crown);
				Entry(5, "A king");
				PixelDiamond(new Vector2(kx + 3f * k, ky + row * 3f + 4f * k), 1, Color.white);
				Entry(6, "You");
				Dot(kx + col + 1f * k, ky + row * 3f + 3f * k, color, ring: false);
				BandPip(kx + col + 1f * k + sq * 0.5f, ky + row * 3f + 3f * k + sq * 0.5f, sq, KeyFlag.Color);
				Entry(7, "Holds flag 1");
				DrawShipGlyph(new Vector2(kx + 3f * k, ky + row * 4f + 4f * k), color, marked: false, fighting: false, 0f, 0.8f);
				Entry(8, "A ship");
				DrawReticle(new Vector2(kx + col + 3f * k, ky + row * 4f + 4f * k), 1, holding: false, 0f);
				Entry(9, "Ships fire here");
				WarMap.Realm realm = map.Realms.Find((WarMap.Realm realm2) => realm2.Side == WarMap.Side.Ally);
				Dot(kx + 1f * k, ky + row * 5f + 3f * k, realm?.Color ?? WarMap.AllySide, ring: false, new Color(0.1f, 0.42f, 0.12f, 0.95f));
				Entry(10, "Allies");
				Dot(kx + col + 1f * k, ky + row * 5f + 3f * k, new Color(0.62f, 0.62f, 0.6f, 0.6f), ring: false, new Color(0.05f, 0.04f, 0.04f, 0.5f));
				Entry(11, "Others");
				num += num9 + 4f * k;
				float num10 = panel.yMax - num;
				float width2 = width - 4f * k;
				string text = ((Lang.Height(GameArt.Style(3.9f, TextAnchor.UpperLeft, wrap: true), "Click a warrior or ship to select it (Shift adds), a town for its card. Drag a box over yours. Double-click selects all yours in view. Right-click sends the selected: onto a foe they chase it, onto a town they take it, onto ground they fight what is near and hold there. Shift+right-click plants a flag with the bar's order. Drag to set its area. Ctrl+1-6 saves a group, 1-6 selects it and goes to it. Selected ships sail to water or fire on a foe (Ctrl: a spot). Warriors right-clicked onto a transport go aboard. Right drag or WASD moves the map.", width2) <= num10) ? "Click a warrior or ship to select it (Shift adds), a town for its card. Drag a box over yours. Double-click selects all yours in view. Right-click sends the selected: onto a foe they chase it, onto a town they take it, onto ground they fight what is near and hold there. Shift+right-click plants a flag with the bar's order. Drag to set its area. Ctrl+1-6 saves a group, 1-6 selects it and goes to it. Selected ships sail to water or fire on a foe (Ctrl: a spot). Warriors right-clicked onto a transport go aboard. Right drag or WASD moves the map." : ((Lang.Height(GameArt.Style(3.9f, TextAnchor.UpperLeft, wrap: true), "Right-click: go or attack. Shift+right-click: a flag. Ctrl+1-6 saves a group, 1-6 selects it.", width2) <= num10) ? "Right-click: go or attack. Shift+right-click: a flag. Ctrl+1-6 saves a group, 1-6 selects it." : null));
				if (text != null)
				{
					GameArt.Label(new Rect(x + 2f * k, num, width2, num10), text, 3.9f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
				}
			}
			void Dot(float px, float py, Color c2, bool ring, Color? rim = null)
			{
				if (ring)
				{
					GameArt.Flat(new Rect(px - 2f * k, py - 2f * k, sq + 4f * k, sq + 4f * k), new Color(1f, 0.85f, 0.25f, 0.95f));
				}
				GameArt.Flat(new Rect(px - k, py - k, sq + 2f * k, sq + 2f * k), rim ?? new Color(0.05f, 0.04f, 0.04f, 0.95f));
				GameArt.Flat(new Rect(px, py, sq, sq), c2);
			}
			void Entry(int i, string label)
			{
				float x2 = kx + (float)(i % 2) * col + 9f * k;
				float num11 = ky + (float)(i / 2) * row;
				GameArt.Label(new Rect(x2, num11 - 1f * k, col - 9f * k, row), label, 4.2f, TextAnchor.MiddleLeft, Color.white);
			}
		}

		private void Line(Vector2 a, Vector2 b, float thick, Color color)
		{
			Vector2 vector = b - a;
			float magnitude = vector.magnitude;
			if (!(magnitude < 0.5f))
			{
				Matrix4x4 matrix = GUI.matrix;
				GUIUtility.RotateAroundPivot(Mathf.Atan2(vector.y, vector.x) * 57.29578f, a);
				Fill(new Rect(a.x, a.y - thick * 0.5f, magnitude, thick), color);
				GUI.matrix = matrix;
			}
		}

		public void DrawWarMarks(float sw, float sh)
		{
			WarMap warMap = _mod.WarMap;
			if ((warMap.Marked.Count == 0 && warMap.Flags.Count == 0) || _mod.Host == null)
			{
				return;
			}
			GameArt.Ensure();
			DrawFlagPins(warMap, sw, sh);
			if (warMap.Marked.Count == 0)
			{
				return;
			}
			FrameProjection shown = _mod.Shown;
			Vector2 current_position = _mod.Host.current_position;
			float p = GameArt.P;
			Color color = new Color(1f, 0.84f, 0.3f, 0.95f);
			int num = 0;
			foreach (Actor item in warMap.Marked)
			{
				if (num++ > 40)
				{
					break;
				}
				Vector3 vector;
				try
				{
					vector = _mod.HeadOf(item);
				}
				catch
				{
					continue;
				}
				float magnitude = (new Vector2(vector.x, vector.y) - current_position).magnitude;
				if (shown.Project(vector.x, vector.y, vector.z + 0.35f, out var col, out var row, out var depth) && col >= 0f && col < (float)shown.Width && row >= 0f && row < (float)shown.Height)
				{
					float num2 = col / (float)shown.Width * sw;
					float num3 = row / (float)shown.Height * sh;
					int num4 = ((depth < 8f) ? 3 : 2);
					PixelDiamond(new Vector2(num2, num3), num4, color);
					GameArt.Flat(new Rect(Mathf.Round(num2 - 0.5f * p), Mathf.Round(num3 + ((float)num4 + 1.5f) * p), Mathf.Max(1f, p), 3f * p), color);
					if (magnitude > 6f)
					{
						GameArt.Label(new Rect(num2 - 30f * p, num3 + ((float)num4 + 4.5f) * p, 60f * p, 8f * p), Mathf.RoundToInt(magnitude).ToString(), 4.4f, TextAnchor.UpperCenter, color);
					}
				}
				else
				{
					float viewYaw = _mod.ViewYaw;
					Vector2 vector2 = new Vector2(vector.x, vector.y) - current_position;
					float f = Mathf.Atan2(vector2.y, vector2.x) - viewYaw;
					float num5 = 0f - Mathf.Sin(f);
					float num6 = 0f - Mathf.Cos(f);
					float num7 = Mathf.Min(sw * 0.46f / Mathf.Max(0.01f, Mathf.Abs(num5)), sh * 0.42f / Mathf.Max(0.01f, Mathf.Abs(num6)));
					PixelDiamond(new Vector2(sw * 0.5f + num5 * num7, sh * 0.5f + num6 * num7), 2, new Color(color.r, color.g, color.b, 0.7f));
				}
			}
		}

		private bool OverOrderUi(Vector2 m)
		{
			if (!(_orderBar.width > 0f) || !_orderBar.Contains(m))
			{
				if (_cardCity != null && _townCard.width > 0f)
				{
					return _townCard.Contains(m);
				}
				return false;
			}
			return true;
		}

		private static WarFlag GroupTag(int n)
		{
			object obj = GroupTags[n];
			if (obj == null)
			{
				WarFlag[] groupTags = GroupTags;
				WarFlag obj2 = new WarFlag
				{
					Number = n
				};
				WarFlag warFlag = obj2;
				groupTags[n] = obj2;
				obj = warFlag;
			}
			return (WarFlag)obj;
		}

		private static Color OrderTint(WarOrder o)
		{
			return OrderTints[(int)o];
		}

		private static Sprite OrderSprite(WarOrder o)
		{
			switch (o)
			{
			case WarOrder.Attack:
				return GameArt.IconNamed("iconAttack") ?? GameArt.Warfare;
			case WarOrder.Wait:
				return GameArt.IconNamed("iconShield") ?? GameArt.IconNamed("iconArmy");
			case WarOrder.Destroy:
				return GameArt.IconNamed("iconDemolish") ?? GameArt.IconNamed("icon_hammer_steel");
			case WarOrder.Kill:
				return GameArt.IconNamed("iconSkulls") ?? GameArt.Kills;
			default:
				return GameArt.IconNamed("iconHouseTier0") ?? GameArt.IconNamed("iconBuildings") ?? GameArt.IconNamed("iconCity");
			}
		}

		private static void DrawOrderIcon(Rect r, WarOrder o, Color tint)
		{
			GameArt.Icon(r, OrderSprite(o), tint);
			if (o == WarOrder.Pillage)
			{
				Sprite sprite = GameArt.IconNamed("iconFire") ?? GameArt.IconNamed("iconCloudFire");
				if (!(sprite == null))
				{
					float num = Mathf.Round(r.width * 0.62f);
					GameArt.Icon(new Rect(Mathf.Round(r.xMax - num * 0.8f), Mathf.Round(r.y - num * 0.15f), num, num), sprite, tint);
				}
			}
		}

		private bool OrderKeys(WarMap map, Actor host, Event ev, Action<Vector2> jump)
		{
			if (ev.type != EventType.KeyDown || _mod.IsTypingNow || host == null)
			{
				return false;
			}
			int num = 0;
			if (ev.keyCode >= KeyCode.Alpha1 && ev.keyCode <= KeyCode.Alpha6)
			{
				num = (int)(ev.keyCode - 49 + 1);
			}
			else if (ev.keyCode >= KeyCode.Keypad1 && ev.keyCode <= KeyCode.Keypad6)
			{
				num = (int)(ev.keyCode - 257 + 1);
			}
			if (num > 0)
			{
				if (ev.control || ev.command)
				{
					SaveGroupKey(map, host, num);
				}
				else
				{
					GroupKey(map, host, num, ev.shift, jump);
				}
				ev.Use();
				return true;
			}
			for (int i = 0; i < WarMap.OrderKeys.Length; i++)
			{
				if (ev.keyCode == WarMap.OrderKeys[i] && !ev.control && !ev.alt)
				{
					ChooseOrder(map, (WarOrder)i);
					ev.Use();
					return true;
				}
			}
			return false;
		}

		private void SaveGroupKey(WarMap map, Actor host, int n)
		{
			List<Actor> list = map.MarkedOwn();
			if (list.Count == 0)
			{
				_mod.Toast($"Select warriors first, then Ctrl+{n} makes them group {n}");
				return;
			}
			int n2 = map.SaveGroup(n, list, host.kingdom);
			WarFlag warFlag = (map.SelectedFlag = map.FlagNumbered(n));
			Sounds.PlayUI("event:/SFX/Ui/Click");
			_mod.Toast((warFlag != null) ? $"Group {n} saved with {Warriors(n2)}. Press {n} to jump to them" : $"Group {n} saved with {Warriors(n2)}. Press {n} to jump to them, Shift+right-click for their flag");
		}

		private void GroupKey(WarMap map, Actor host, int n, bool add, Action<Vector2> jump)
		{
			Vector2 middle;
			int num = map.SelectGroup(n, add, out middle);
			Sounds.PlayUI("event:/SFX/Ui/Click");
			_bandKeyNumber = n;
			_bandKeyAt = Time.unscaledTime;
			if (num == 0 && map.FlagNumbered(n) == null)
			{
				_mod.Toast($"No group {n}. Select warriors and press Ctrl+{n}");
				return;
			}
			if (num == 0)
			{
				_mod.Toast($"Nobody holds flag {n} yet. Select warriors and right-click it");
			}
			jump?.Invoke(middle);
		}

		private void ChooseOrder(WarMap map, WarOrder o)
		{
			WarFlag warFlag = ((map.SelectedFlag != null && map.Flags.Contains(map.SelectedFlag)) ? map.SelectedFlag : null);
			bool num = warFlag != null && warFlag.Order != o;
			map.ChooseOrder(o);
			Sounds.PlayUI("event:/SFX/Ui/Click");
			if (num)
			{
				_mod.Toast($"Flag {warFlag.Number} set to {WarMap.OrderNames[(int)o]}. {WarMap.OrderTips[(int)o]}");
				if (o != WarOrder.Wait && warFlag.Band.Count > 0)
				{
					_mod.PlayHorn();
				}
			}
			else if (warFlag == null)
			{
				_mod.Toast("Next flag set to " + WarMap.OrderNames[(int)o] + ". " + WarMap.OrderTips[(int)o]);
			}
		}

		private void DrawOrderBar(WarMap map, Actor host, float cx, float bottom, float maxW, Action<Vector2> jump)
		{
			float p = GameArt.P;
			bool flag = Event.current.type == EventType.Repaint;
			map.GroupCounts(_groupCounts);
			WarFlag warFlag = ((map.SelectedFlag != null && map.Flags.Contains(map.SelectedFlag)) ? map.SelectedFlag : null);
			WarOrder currentOrder = map.CurrentOrder;
			List<Actor> list = map.MarkedOwn();
			int count = list.Count;
			int num = ((count > 0) ? map.GroupOfAll(list) : 0);
			float num2 = Mathf.Round(13f * p);
			float num3 = Mathf.Round(20f * p);
			float num4 = Mathf.Round(19f * p);
			float num5 = Mathf.Round(5f * p);
			float num6 = Mathf.Round(2f * p);
			string text = ((warFlag != null) ? $"Flag {warFlag.Number} · {WarMap.OrderNames[(int)currentOrder]}" : ("Next flag · " + WarMap.OrderNames[(int)currentOrder]));
			string text2 = ((count == 0) ? "None selected" : ((num > 0) ? $"{count} selected (group {num})" : $"{count} selected"));
			string text3 = ((warFlag != null) ? BandLine(warFlag) : ((count > 0) ? "RMB go · Shift+RMB flag" : "Click or drag to select"));
			float value = Mathf.Round(Mathf.Max(GameArt.Measure(text, 5f).x, Mathf.Max(GameArt.Measure(text2, 4.2f).x, GameArt.Measure(text3, 3.9f).x)) + 8f * p);
			value = Mathf.Clamp(value, 84f * p, 120f * p);
			float a = num5 * 2f + 6f * (num2 + num6) + 4f * p + (float)(WarMap.OrderNames.Length + 1) * (num4 + 4f * p) + 3f * p + value;
			a = Mathf.Min(a, maxW);
			float num7 = Mathf.Round(30f * p);
			Rect r = (_orderBar = new Rect(Mathf.Round(cx - a * 0.5f), Mathf.Round(bottom - num7), Mathf.Round(a), num7));
			if (flag)
			{
				GameArt.TipPanel(r, 0.95f);
			}
			float num8 = r.x + num5;
			float y = r.y + Mathf.Round((num7 - num3) * 0.5f);
			Vector2 mousePosition = Event.current.mousePosition;
			string text4 = null;
			Rect rect = default(Rect);
			for (int i = 1; i <= 6; i++)
			{
				Rect rect2 = new Rect(num8, y, num2, num3);
				num8 += num2 + num6;
				int num9 = _groupCounts[i];
				bool flag2 = num == i || (warFlag != null && warFlag.Number == i);
				if (flag)
				{
					Color color = WarMap.FlagColor(i);
					GameArt.Flat(rect2, new Color(0f, 0f, 0f, flag2 ? 0.55f : 0.3f));
					if (flag2)
					{
						Outline(rect2, Mathf.Max(1f, Mathf.Round(p * 0.8f)), new Color(1f, 0.85f, 0.3f, 0.95f));
					}
					Rect rect3 = new Rect(Mathf.Round(rect2.x + (num2 - 9f * p) * 0.5f), Mathf.Round(rect2.y + 2f * p), Mathf.Round(9f * p), Mathf.Round(9f * p));
					if (num9 > 0 || map.FlagNumbered(i) != null)
					{
						DrawNumberTag(rect3, GroupTag(i), rect2.Contains(mousePosition));
					}
					else
					{
						GameArt.Label(rect3, i.ToString(), 4.6f, TextAnchor.MiddleCenter, new Color(color.r, color.g, color.b, 0.45f));
					}
					GameArt.Label(new Rect(rect2.x, rect2.y + 11f * p, rect2.width, 8f * p), (num9 > 0) ? num9.ToString() : "-", 4f, TextAnchor.MiddleCenter, (num9 > 0) ? Color.white : GameArt.Grey);
				}
				if (rect2.Contains(mousePosition))
				{
					text4 = ((num9 > 0) ? $"Group {i} ({i}). Ctrl+{i} saves the selection" : $"Ctrl+{i} makes the selection group {i}");
					rect = rect2;
				}
				PadNav.Spot(rect2);
				if (GUI.Button(rect2, GUIContent.none, GUIStyle.none))
				{
					if (Event.current.control)
					{
						SaveGroupKey(map, host, i);
					}
					else
					{
						GroupKey(map, host, i, Event.current.shift, jump);
					}
				}
			}
			num8 += 4f * p;
			float y2 = r.y + Mathf.Round((num7 - num4) * 0.5f) - 2f * p;
			for (int j = 0; j < WarMap.OrderNames.Length; j++)
			{
				WarOrder warOrder = (WarOrder)j;
				Rect rect4 = new Rect(num8, y2, num4, num4);
				num8 += num4 + 4f * p;
				bool flag3 = currentOrder == warOrder;
				bool flag4 = rect4.Contains(mousePosition);
				if (flag)
				{
					Rect r2 = ((flag4 || flag3) ? new Rect(rect4.x - p, rect4.y - p, rect4.width + 2f * p, rect4.height + 2f * p) : rect4);
					GameArt.StoneButton(r2, null, 1f, gems: true, Color.white);
					float num10 = Mathf.Round(r2.width * 0.62f);
					DrawOrderIcon(new Rect(Mathf.Round(r2.center.x - num10 * 0.5f), Mathf.Round(r2.center.y - num10 * 0.5f), num10, num10), warOrder, Color.white);
					if (flag3)
					{
						GameArt.Sliced(r2, GameArt.ButtonBorder, new Color(1f, 0.85f, 0.35f, 1f), 4f, 4f, 4f, 4f, r2.width / 32f);
					}
					else if (flag4)
					{
						GameArt.Sliced(r2, GameArt.ButtonBorder, new Color(1f, 1f, 1f, 0.6f), 4f, 4f, 4f, 4f, r2.width / 32f);
					}
					string text5 = WarMap.OrderKeys[j].ToString();
					GameArt.Label(new Rect(rect4.x - 4f * p, rect4.yMax - 0.5f * p, rect4.width + 8f * p, 7f * p), text5, 3.8f, TextAnchor.UpperCenter, flag3 ? GameArt.AgeYellow : GameArt.Grey);
				}
				if (flag4)
				{
					text4 = $"{WarMap.OrderNames[j]} ({WarMap.OrderKeys[j]}). {WarMap.OrderTips[j]}";
					rect = rect4;
				}
				PadNav.Spot(rect4);
				if (GUI.Button(rect4, GUIContent.none, GUIStyle.none))
				{
					ChooseOrder(map, warOrder);
				}
			}
			Rect rect5 = new Rect(num8, y2, num4, num4);
			num8 += num4 + 4f * p;
			if (BoardButton(map, host, rect5, count, mousePosition, flag))
			{
				text4 = "Board. The selected go aboard the nearest transport";
				rect = rect5;
			}
			num8 += 3f * p;
			Rect rect6 = new Rect(num8, r.y + 2f * p, r.xMax - num8 - num5, num7 - 4f * p);
			if (flag)
			{
				GameArt.Label(new Rect(rect6.x, rect6.y, rect6.width, 9f * p), text, 5f, TextAnchor.MiddleLeft, OrderTint(currentOrder));
				GameArt.Label(new Rect(rect6.x, rect6.y + 9f * p, rect6.width, 8f * p), text2, 4.2f, TextAnchor.MiddleLeft, (count > 0) ? GameArt.AgeYellow : GameArt.Grey);
				GameArt.Label(new Rect(rect6.x, rect6.y + 17f * p, rect6.width, 8f * p), text3, 3.9f, TextAnchor.MiddleLeft, GameArt.Grey);
			}
			if (text4 != null && flag)
			{
				float num11 = GameArt.Measure(text4, 4.4f).x + 10f * p;
				Rect r3 = new Rect(Mathf.Round(Mathf.Clamp(rect.center.x - num11 * 0.5f, 2f, (float)Screen.width - num11 - 2f)), Mathf.Round(r.y - 14f * p), Mathf.Round(num11), Mathf.Round(12f * p));
				GameArt.TipPanel(r3);
				GameArt.Label(r3, text4, 4.4f, TextAnchor.MiddleCenter, Color.white);
			}
		}

		private static bool FoeOfRealm(Kingdom home, BaseSimObject o)
		{
			try
			{
				return o != null && o.isAlive() && o.kingdom != null && o.kingdom != home && home.isEnemy(o.kingdom);
			}
			catch
			{
				return false;
			}
		}

		private static Building FoeBuildingAt(Kingdom home, WorldTile t)
		{
			if (t == null)
			{
				return null;
			}
			for (int i = 0; i <= 1; i++)
			{
				for (int j = -i; j <= i; j++)
				{
					for (int k = -i; k <= i; k++)
					{
						WorldTile worldTile = null;
						try
						{
							worldTile = World.world.GetTile(t.x + k, t.y + j);
						}
						catch
						{
						}
						Building building = null;
						try
						{
							building = worldTile?.building;
						}
						catch
						{
						}
						if (building != null && !building.isRuin() && FoeOfRealm(home, building))
						{
							return building;
						}
					}
				}
			}
			return null;
		}

		private static string Warriors(int n)
		{
			if (n != 1)
			{
				return n + " warriors";
			}
			return "1 warrior";
		}

		private void RightOrder(WarMap map, Actor host, Vector2 world, Actor under, WarMap.Town? town)
		{
			List<Actor> list = map.MarkedOwn();
			Kingdom kingdom = host.kingdom;
			if (list.Count == 0)
			{
				_mod.Toast((map.MarkedShips().Count > 0) ? "Ships take their orders on the map" : "Select warriors first. Shift+right-click plants a flag");
				return;
			}
			WorldTile worldTile = null;
			try
			{
				worldTile = World.world.GetTile((int)world.x, (int)world.y);
			}
			catch
			{
			}
			BaseSimObject baseSimObject = null;
			WorldTile at = null;
			WarOrder warOrder = WarOrder.Attack;
			float radius = 5f;
			string text;
			if (under != null && under != host && !WarMap.IsShip(under) && FoeOfRealm(kingdom, under))
			{
				baseSimObject = under;
				at = under.current_tile;
				text = "go for " + WorldBoxMod.Describe(under);
			}
			else if (town.HasValue && FoeRealm(kingdom, town.Value.Owner))
			{
				at = WarMap.GroundFor(TileAt(town.Value.P));
				radius = 10f;
				text = "march on " + town.Value.Name;
			}
			else if (town.HasValue && town.Value.Owner != null)
			{
				at = WarMap.GroundFor(TileAt(town.Value.P));
				warOrder = WarOrder.Wait;
				radius = 9f;
				text = "guard " + town.Value.Name;
			}
			else
			{
				Building building = FoeBuildingAt(kingdom, worldTile);
				if (building != null)
				{
					baseSimObject = building;
					at = building.current_tile;
					text = "attack the building";
				}
				else
				{
					at = WarMap.GroundFor(worldTile);
					text = "move there and fight what is near";
				}
			}
			if (at == null)
			{
				_mod.Toast("No firm ground there");
				return;
			}
			if (baseSimObject == null && !town.HasValue && !list.Exists((Actor a) => WarMap.CanWalkTo(a, at)))
			{
				FlagOrder(map, host, worldTile, 8f);
				return;
			}
			int cantReach;
			WarFlag warFlag = map.Post(_mod.Royal, host, list, at, warOrder, radius, baseSimObject, out cantReach);
			if (warFlag == null)
			{
				_mod.Toast((cantReach > 0) ? "None of the selected can reach it across the water" : "None of the selected can go there");
				return;
			}
			int count = warFlag.Band.Count;
			_mod.Toast(Warriors(count) + " " + text + ((cantReach > 0) ? $". {cantReach} can't reach it across the water" : "") + Afterword(map, 0));
			if (baseSimObject != null || (warOrder == WarOrder.Attack && town.HasValue))
			{
				_mod.PlayHorn();
			}
		}

		private void ClickUnit(WarMap map, Actor host, Actor who, bool shift)
		{
			map.SelectedFlag = null;
			if (!shift && !map.Marked.Contains(who) && map.MarkedOwn().Count > 0 && !WarMap.IsShip(who) && FoeOfRealm(host.kingdom, who))
			{
				RightOrder(map, host, who.current_position, who, null);
				return;
			}
			if (shift)
			{
				map.ToggleMark(who);
			}
			else if (map.Marked.Count == 1 && map.Marked.Contains(who))
			{
				map.Marked.Clear();
			}
			else
			{
				map.Marked.Clear();
				map.Marked.Add(who);
			}
			Sounds.PlayUI("event:/SFX/Ui/Click");
		}

		private static bool FoeRealm(Kingdom home, Kingdom k)
		{
			try
			{
				return k != null && k != home && home.isEnemy(k);
			}
			catch
			{
				return false;
			}
		}

		private void FlagOrder(WarMap map, Actor host, WorldTile clicked, float radius)
		{
			WorldTile worldTile = WarMap.GroundFor(clicked);
			if (worldTile == null)
			{
				_mod.Toast("No firm ground there to plant a flag");
				return;
			}
			WarOrder currentOrder = map.CurrentOrder;
			List<Actor> list = map.MarkedOwn();
			int num = map.GroupOfAll(list);
			WarFlag whole = map.WholeBand(list) ?? ((num > 0) ? map.FlagNumbered(num) : null);
			string arg = WarMap.OrderNames[(int)currentOrder];
			if (whole != null)
			{
				map.Move(whole, worldTile);
				whole.Order = currentOrder;
				whole.Radius = radius;
				List<Actor> list2 = list.FindAll((Actor a) => map.FlagOf(a) != whole);
				int stranded;
				string how;
				int num2 = ((list2.Count > 0) ? SendBand(map, host, list2, whole, out stranded, out how) : 0);
				map.SelectedFlag = whole;
				_mod.Toast($"Flag {whole.Number} moves with its {arg} order. {Warriors(whole.Band.Count)} on the march" + ((num2 > 0) ? $", {num2} of them new" : ""));
				_mod.PlayHorn();
				return;
			}
			int num3 = ((num > 0) ? num : map.FreeNumber());
			if (num3 == 0 || (map.Flags.Count >= 6 && map.FlagsKingdom == host.kingdom))
			{
				_mod.Toast("All six flags are up. Drag one here, or right-click one with nobody selected to pull it up");
				return;
			}
			if (num == 0 && list.Count > 0)
			{
				map.SaveGroup(num3, list, host.kingdom);
			}
			WarFlag warFlag = map.Plant(worldTile, host.kingdom, num3);
			if (warFlag != null)
			{
				warFlag.Order = currentOrder;
				warFlag.Radius = radius;
				map.SelectedFlag = warFlag;
				int stranded2;
				string how2;
				if (list.Count == 0)
				{
					_mod.Toast($"Flag {warFlag.Number} planted with {arg}. Select warriors and right-click it to send them");
				}
				else if (SendBand(map, host, list, warFlag, out stranded2, out how2) == 0)
				{
					map.PullUp(warFlag);
					_mod.Toast((stranded2 > 0) ? "None of the selected can reach that ground. No transport of yours can carry them" : "None of the selected can go there");
				}
				else
				{
					_mod.Toast($"Flag {warFlag.Number} planted with {arg}. {how2}" + Stranded(map, stranded2));
					_mod.PlayHorn();
				}
			}
		}

		private void AreaPress(WarMap map, Vector2 world, WarFlag onFlag)
		{
			_areaOn = true;
			_areaMoved = false;
			_areaFlag = onFlag;
			_areaFrom = onFlag?.P ?? world;
			_areaRadius = onFlag?.Radius ?? 8f;
		}

		private void AreaMove(Vector2? world, bool dragged)
		{
			if (_areaOn && world.HasValue)
			{
				if (dragged)
				{
					_areaMoved = true;
				}
				if (_areaMoved)
				{
					_areaRadius = Mathf.Clamp((world.Value - _areaFrom).magnitude, 3f, 30f);
				}
			}
		}

		private void AreaRelease(WarMap map, Actor host)
		{
			if (!_areaOn)
			{
				return;
			}
			_areaOn = false;
			WarFlag areaFlag = _areaFlag;
			_areaFlag = null;
			if (areaFlag != null && map.Flags.Contains(areaFlag))
			{
				if (!_areaMoved)
				{
					if (map.MarkedOwn().Count > 0)
					{
						SendToFlag(map, host, areaFlag);
					}
					else
					{
						PullUpFlag(map, areaFlag);
					}
				}
				else
				{
					areaFlag.Radius = _areaRadius;
					areaFlag.Born = Time.unscaledTime;
					map.SelectedFlag = areaFlag;
					Sounds.PlayUI("event:/SFX/Ui/Click");
					_mod.Toast($"Flag {areaFlag.Number} now covers {Mathf.RoundToInt(_areaRadius)} tiles");
				}
			}
			else
			{
				float radius = (_areaMoved ? _areaRadius : ((map.CurrentOrder == WarOrder.Wait) ? 6f : 8f));
				FlagOrder(map, host, TileAt(_areaFrom), radius);
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
		}

		private void MapRing(Vector2 c, float r, Color col, float alpha)
		{
			float p = GameArt.P;
			float thick = Mathf.Max(1f, Mathf.Round(p * 0.9f));
			int num = Mathf.Clamp(Mathf.RoundToInt(r / (3f * p)), 16, 64);
			Vector2 a = c + new Vector2(r, 0f);
			for (int i = 1; i <= num; i++)
			{
				float f = (float)i * MathF.PI * 2f / (float)num;
				Vector2 vector = c + new Vector2(Mathf.Cos(f) * r, Mathf.Sin(f) * r);
				if (i % 2 == 0 || r < 30f * p)
				{
					Line(a, vector, thick, new Color(col.r, col.g, col.b, alpha));
				}
				a = vector;
			}
		}

		private void SceneRing(Vector2 c, float radius, Color col, float alpha)
		{
			float p = GameArt.P;
			float thick = Mathf.Max(1.5f, Mathf.Round(p));
			Vector2 s;
			bool flag = SceneFoot(c + new Vector2(radius, 0f), out s);
			for (int i = 1; i <= 40; i++)
			{
				float f = (float)i * MathF.PI * 2f / 40f;
				Vector2 s2;
				bool flag2 = SceneFoot(c + new Vector2(Mathf.Cos(f) * radius, Mathf.Sin(f) * radius), out s2);
				if (flag && flag2 && i % 2 == 0)
				{
					Line(s, s2, thick, new Color(col.r, col.g, col.b, alpha));
				}
				else if (flag && flag2)
				{
					Line(s, s2, thick, new Color(col.r, col.g, col.b, alpha * 0.45f));
				}
				s = s2;
				flag = flag2;
			}
		}

		private static void FlagOrderBadge(Vector2 at, WarFlag f, float size)
		{
			float p = GameArt.P;
			float num = Mathf.Round(size * p);
			Rect r = new Rect(Mathf.Round(at.x), Mathf.Round(at.y), num, num);
			GameArt.Flat(new Rect(r.x - p * 0.5f, r.y - p * 0.5f, r.width + p, r.height + p), new Color(0f, 0f, 0f, 0.55f));
			DrawOrderIcon(r, f.Order, Color.white);
		}

		private void DrawTownCard(WarMap map, Actor host, Vector2 anchor, Rect bounds)
		{
			City cardCity = _cardCity;
			_townCard = default(Rect);
			Kingdom kingdom = null;
			bool flag;
			try
			{
				flag = cardCity?.isAlive() ?? false;
				kingdom = (flag ? cardCity.kingdom : null);
			}
			catch
			{
				flag = false;
			}
			if (!flag || kingdom == null)
			{
				_cardCity = null;
				return;
			}
			float p = GameArt.P;
			Kingdom kingdom2 = host.kingdom;
			Royal royal = _mod.Royal;
			bool flag2 = kingdom == kingdom2;
			bool flag3 = !flag2 && Royal.AtWar(kingdom2, kingdom);
			bool flag4 = !flag2 && !flag3 && Royal.Allied(kingdom2, kingdom);
			bool flag5 = !flag2 && FoeRealm(kingdom2, kingdom);
			int count = map.MarkedOwn().Count;
			bool flag6 = CourtPart(host, kingdom2, Seat.Marshal, courtBusiness: false);
			bool flag7 = CourtPart(host, kingdom2, Seat.Chancellor, courtBusiness: true);
			bool flag8 = royal.Active != null && royal.Active.Target == kingdom;
			float num = (flag2 ? Mathf.Round(ShipyardHeight(cardCity) * p) : 0f);
			float num2 = Mathf.Round(172f * p);
			float num3 = Mathf.Round((flag3 ? 70f : ((flag2 || flag4) ? 34f : 50f)) * p) + num + ((count > 0) ? Mathf.Round(18f * p) : 0f);
			Rect rect = new Rect(Mathf.Round(anchor.x + 16f * p), Mathf.Round(anchor.y - num3 * 0.5f), num2, num3);
			if (rect.xMax > bounds.xMax - 4f * p)
			{
				rect.x = Mathf.Round(anchor.x - 16f * p - num2);
			}
			rect.x = Mathf.Clamp(rect.x, bounds.x + 4f * p, Mathf.Max(bounds.x + 4f * p, bounds.xMax - num2 - 4f * p));
			rect.y = Mathf.Clamp(rect.y, bounds.y + 4f * p, Mathf.Max(bounds.y + 4f * p, bounds.yMax - num3 - 4f * p));
			_townCard = rect;
			bool flag9 = Event.current.type == EventType.Repaint;
			if (flag9)
			{
				GameArt.TipPanel(rect, 0.97f);
				DrawBanner(new Rect(rect.x + 5f * p, rect.y + 5f * p, 13f * p, 16f * p), kingdom, civ: true, 1f);
				Color color = Color.white;
				try
				{
					color = WarMap.Readable(kingdom.getColor().getColorText());
				}
				catch
				{
				}
				string text = cardCity.name;
				try
				{
					text = Renames.Apply(cardCity.name);
				}
				catch
				{
				}
				GameArt.Label(new Rect(rect.x + 22f * p, rect.y + 3f * p, num2 - 40f * p, 10f * p), text + ((kingdom.capital == cardCity) ? "  (capital)" : ""), 5.6f, TextAnchor.MiddleLeft, Color.white);
				string text2 = (flag2 ? "Your town" : (flag3 ? "At war" : (flag4 ? "Allied" : "At peace")));
				Color color2 = (flag2 ? new Color(0.6f, 1f, 0.55f) : (flag3 ? new Color(1f, 0.45f, 0.35f) : (flag4 ? new Color(0.55f, 1f, 0.5f) : GameArt.Grey)));
				GameArt.Label(new Rect(rect.x + 22f * p, rect.y + 13f * p, num2 - 26f * p, 8f * p), Royal.NameOf(kingdom) + "  ·  " + SafeCount(cardCity) + " people", 4.2f, TextAnchor.MiddleLeft, color);
				GameArt.Label(new Rect(rect.x + 22f * p, rect.y + 21f * p, num2 - 26f * p, 8f * p), text2, 4.2f, TextAnchor.MiddleLeft, color2);
			}
			Rect r = new Rect(rect.xMax - 14f * p, rect.y + 3f * p, 11f * p, 11f * p);
			if (GameButton(r, "x", red: false, enabled: true, null, 4.6f))
			{
				_cardCity = null;
				return;
			}
			float num4 = rect.y + 31f * p;
			float num5 = Mathf.Round(14f * p);
			float num6 = rect.x + 5f * p;
			float num7 = num2 - 10f * p;
			Action<string> toast = _mod.Toast;
			if (flag3)
			{
				if (flag9)
				{
					GameArt.Label(new Rect(num6, num4, 44f * p, num5), "Send army", 4.2f, TextAnchor.MiddleLeft, new Color(1f, 0.55f, 0.45f));
				}
				float num8 = Mathf.Round(19f * p);
				float num9 = num6 + 44f * p;
				for (int i = 0; i < Shares.Length; i++)
				{
					TownCardSharesForTest[i] = new Rect(num9, num4, num8, num5);
					if (GameButton(new Rect(num9, num4, num8, num5), ShareLabels[i], red: true, flag6, null, 4.4f))
					{
						royal.SendArmy(host, kingdom, Shares[i], toast, _mod.PlayHorn, cardCity);
					}
					num9 += num8 + 2f * p;
				}
				if (GameButton(new Rect(num9 + 2f * p, num4, num6 + num7 - num9 - 2f * p, num5), "Recall", red: false, flag8, null, 4.2f))
				{
					royal.Recall(toast);
				}
				num4 += num5 + 3f * p;
				if (!flag6 && flag9)
				{
					GameArt.Label(new Rect(num6, num4, num7, 8f * p), "No marshal. Appoint one in talk", 3.9f, TextAnchor.MiddleLeft, GameArt.Grey);
				}
				else if (flag8 && flag9 && royal.Active.Objective != null)
				{
					GameArt.Label(new Rect(num6, num4, num7, 8f * p), "Your army marches on " + royal.Active.Objective.name, 3.9f, TextAnchor.MiddleLeft, GameArt.Peach);
				}
				num4 += 10f * p;
			}
			else if (!flag2 && !flag4)
			{
				if (GameButton(new Rect(num6, num4, Mathf.Round(64f * p), num5), "Declare war", red: true, flag7, null, 4.4f))
				{
					royal.DeclareWar(host, kingdom, toast, _mod.PlayHorn);
				}
				if (!flag7 && flag9)
				{
					GameArt.Label(new Rect(num6 + 68f * p, num4 - 1f * p, num7 - 68f * p, num5 + 2f * p), Court.AwayLine(host), 3.7f, TextAnchor.MiddleLeft, GameArt.Grey, wrap: true);
				}
				num4 += num5 + 4f * p;
			}
			if (flag2)
			{
				DrawShipyard(cardCity, host, num6, num4, num7, flag9);
				num4 += num;
			}
			if (count > 0)
			{
				bool flag10 = flag3 || flag5;
				string label = (flag10 ? "Attack with " : "Guard with ") + count + " selected";
				if (GameButton(new Rect(num6, num4, num7, num5), label, flag10, enabled: true, flag10 ? OrderSprite(WarOrder.Attack) : OrderSprite(WarOrder.Wait), 4.4f))
				{
					WarMap.Town value = new WarMap.Town
					{
						City = cardCity,
						Owner = kingdom,
						Name = cardCity.name,
						P = map.TownPoint(cardCity)
					};
					RightOrder(map, host, value.P, null, value);
				}
			}
		}

		private void DrawTerritoryFill(WarMap map, Func<Vector2, Vector2> toScreen, float vx0, float vy0, float viewW, float viewH, float ppt)
		{
			Texture2D texture2D = map.TerritoryFor(map.Overlay);
			if (!(texture2D == null))
			{
				float num = (float)map.ZonesX * map.ZoneSize;
				float num2 = (float)map.ZonesY * map.ZoneSize;
				float num3 = Mathf.Max(vx0, 0f);
				float num4 = Mathf.Max(vy0, 0f);
				float num5 = Mathf.Min(vx0 + viewW, num);
				float num6 = Mathf.Min(vy0 + viewH, num2);
				if (!(num5 <= num3) && !(num6 <= num4))
				{
					Vector2 vector = toScreen(new Vector2(num3, num6));
					Vector2 vector2 = toScreen(new Vector2(num5, num4));
					Rect texCoords = new Rect(num3 / num, num4 / num2, (num5 - num3) / num, (num6 - num4) / num2);
					Color color = GUI.color;
					GUI.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0.55f, Mathf.InverseLerp(6f * GameArt.P, 20f * GameArt.P, ppt)));
					GUI.DrawTextureWithTexCoords(Rect.MinMaxRect(vector.x, vector.y, vector2.x, vector2.y), texture2D, texCoords);
					GUI.color = color;
				}
			}
		}

		private void DrawBorders(WarMap map, Func<Vector2, Vector2> toLocal, Rect area, float pulse)
		{
			if (map.Borders.Count == 0)
			{
				return;
			}
			float p = GameArt.P;
			float num = Mathf.Max(1f, Mathf.Round(p * 0.75f));
			int num2 = map.IndexOf(map.Picked);
			WarMap.Layer layer = map.Overlay;
			if (layer == WarMap.Layer.None && num2 < 0)
			{
				return;
			}
			float num3 = 4f * p;
			foreach (WarMap.Border border in map.Borders)
			{
				Vector2 vector = toLocal(border.A);
				Vector2 vector2 = toLocal(border.B);
				if (Mathf.Max(vector.x, vector2.x) < 0f - num3 || Mathf.Min(vector.x, vector2.x) > area.width + num3 || Mathf.Max(vector.y, vector2.y) < 0f - num3 || Mathf.Min(vector.y, vector2.y) > area.height + num3)
				{
					continue;
				}
				bool flag = border.Low == num2 && num2 >= 0;
				bool flag2 = border.High == num2 && num2 >= 0;
				if (layer == WarMap.Layer.None && !flag && !flag2)
				{
					continue;
				}
				bool flag3 = map.SameGroup(border.Low, border.High, layer);
				for (int i = 0; i < 2; i++)
				{
					int num4 = ((i == 0) ? border.Low : border.High);
					if (num4 < 0 || num4 >= map.Realms.Count)
					{
						continue;
					}
					bool flag4 = num4 == num2;
					if (layer != WarMap.Layer.None || flag4)
					{
						Color color = Edge(num4);
						float num5 = num;
						if (flag4)
						{
							num5 = num * 2f;
							color = Color.Lerp(color, Color.white, 0.25f + 0.35f * pulse);
							color.a = 1f;
						}
						else if (flag3)
						{
							color.a *= 0.4f;
							num5 = Mathf.Max(1f, Mathf.Round(num * 0.5f));
						}
						Rect rect;
						if (border.Vertical)
						{
							float y = Mathf.Min(vector.y, vector2.y);
							float height = Mathf.Abs(vector2.y - vector.y);
							rect = ((i == 0) ? new Rect(vector.x - num5, y, num5, height) : new Rect(vector.x, y, num5, height));
						}
						else
						{
							float x = Mathf.Min(vector.x, vector2.x);
							float width = Mathf.Abs(vector2.x - vector.x);
							rect = ((i == 0) ? new Rect(x, vector.y, width, num5) : new Rect(x, vector.y - num5, width, num5));
						}
						GameArt.Flat(new Rect(Mathf.Round(rect.x), Mathf.Round(rect.y), Mathf.Max(1f, Mathf.Round(rect.width)), Mathf.Max(1f, Mathf.Round(rect.height))), color);
					}
				}
			}
			Color Edge(int o)
			{
				Color result = ((layer == WarMap.Layer.None || layer == WarMap.Layer.Kingdoms) ? map.Realms[o].Color : map.LandColor(o, layer));
				result.a = ((layer == WarMap.Layer.Sides && map.Realms[o].Side == WarMap.Side.Neutral) ? 0.55f : 0.95f);
				return result;
			}
		}

		private void DrawRealmLinks(WarMap map, Func<Vector2, Vector2> toLocal, float pulse)
		{
			WarMap.Realm realm = map.RealmOf(map.Picked) ?? map.RealmOf(map.Home);
			if (realm == null || !realm.HasCapital)
			{
				return;
			}
			float p = GameArt.P;
			Vector2 vector = toLocal(realm.Capital);
			Sprite sprite = GameArt.IconNamed("iconAlliance");
			Sprite sprite2 = GameArt.IconNamed("iconWar") ?? GameArt.Warfare;
			for (int i = 0; i < 2; i++)
			{
				List<Kingdom> obj = ((i == 0) ? realm.Friends : realm.Foes);
				Color c = ((i == 0) ? WarMap.AllySide : WarMap.EnemySide);
				foreach (Kingdom item in obj)
				{
					WarMap.Realm realm2 = map.RealmOf(item);
					if (realm2 == null || !realm2.HasCapital)
					{
						continue;
					}
					Vector2 vector2 = toLocal(realm2.Capital);
					if (!((vector2 - vector).sqrMagnitude < 30f * p * 30f * p))
					{
						DashLine(vector, vector2, c, (i == 1 && realm.Side == WarMap.Side.Own) ? (0.75f + 0.25f * pulse) : 0.8f);
						Sprite sprite3 = ((i == 0) ? sprite : sprite2);
						if (!(sprite3 == null))
						{
							Vector2 vector3 = (vector + vector2) * 0.5f;
							float num = Mathf.Round(10f * p);
							GameArt.Flat(new Rect(Mathf.Round(vector3.x - num * 0.5f - p), Mathf.Round(vector3.y - num * 0.5f - p), num + 2f * p, num + 2f * p), new Color(0f, 0f, 0f, 0.55f));
							GameArt.Icon(new Rect(Mathf.Round(vector3.x - num * 0.5f), Mathf.Round(vector3.y - num * 0.5f), num, num), sprite3);
						}
					}
				}
			}
		}

		private void DashLine(Vector2 from, Vector2 to, Color c, float alpha)
		{
			float p = GameArt.P;
			Vector2 vector = to - from;
			float magnitude = vector.magnitude;
			if (!(magnitude < 4f * p))
			{
				Vector2 vector2 = vector / magnitude;
				float num = 6f * p;
				float num2 = 4f * p;
				float thick = Mathf.Max(2f, Mathf.Round(1.3f * p));
				for (float num3 = 0f; num3 < magnitude; num3 += num + num2)
				{
					float num4 = Mathf.Min(magnitude, num3 + num);
					Line(from + vector2 * num3 + new Vector2(1f, 1f), from + vector2 * num4 + new Vector2(1f, 1f), thick, new Color(0f, 0f, 0f, 0.45f * alpha));
					Line(from + vector2 * num3, from + vector2 * num4, thick, new Color(c.r, c.g, c.b, alpha));
				}
			}
		}

		private static int TownUnder(WarMap map, Func<Vector2, Vector2> toLocal, Vector2 mouse, float ppt)
		{
			float p = GameArt.P;
			float num = Mathf.Round(Mathf.Clamp(ppt * 4f, 11f * p, 18f * p));
			int result = -1;
			float num2 = float.MaxValue;
			for (int i = 0; i < map.Towns.Count; i++)
			{
				Vector2 vector = toLocal(map.Towns[i].P);
				if (new Rect(vector.x - num * 0.5f, vector.y - num * 0.95f, num, num * 1.25f).Contains(mouse))
				{
					float sqrMagnitude = (vector - mouse).sqrMagnitude;
					if (sqrMagnitude < num2)
					{
						num2 = sqrMagnitude;
						result = i;
					}
				}
			}
			return result;
		}

		private void TownTip(WarMap map, WarMap.Town t, Vector2 mouse)
		{
			float p = GameArt.P;
			WarMap.Realm realm = map.RealmOf(t.Owner);
			string text = (t.Capital ? "Capital of " : "") + Royal.NameOf(t.Owner);
			string text2 = ((t.Side == WarMap.Side.Own) ? "Your realm" : WarMap.SideName(t.Side)) + "   " + t.People + ((t.People == 1) ? " person" : " people");
			float width = Mathf.Max(Mathf.Max(GameArt.Measure(t.Name, 5f).x, GameArt.Measure(text, 4.4f).x), GameArt.Measure(text2, 4.2f).x) + 12f * p;
			Rect r = new Rect(mouse.x + 12f * p, mouse.y + 8f * p, width, 30f * p);
			if (r.xMax > (float)Screen.width)
			{
				r.x = mouse.x - r.width - 8f * p;
			}
			GameArt.TipPanel(r);
			GameArt.Label(new Rect(r.x + 6f * p, r.y + 1.5f * p, r.width, 9f * p), t.Name, 5f, TextAnchor.MiddleLeft, Color.white);
			GameArt.Label(new Rect(r.x + 6f * p, r.y + 10.5f * p, r.width, 8f * p), text, 4.4f, TextAnchor.MiddleLeft, realm?.Text ?? GameArt.Grey);
			GameArt.Label(new Rect(r.x + 6f * p, r.y + 19f * p, r.width, 8f * p), text2, 4.2f, TextAnchor.MiddleLeft, StateColor(t.Side));
		}

		private static Color StateColor(WarMap.Side s)
		{
			switch (s)
			{
			default:
				return GameArt.Grey;
			case WarMap.Side.Own:
				return new Color(0.6f, 0.8f, 1f);
			case WarMap.Side.Ally:
				return new Color(0.55f, 1f, 0.5f);
			case WarMap.Side.Enemy:
				return new Color(1f, 0.45f, 0.35f);
			}
		}

		private void DrawLayerButtons(WarMap map, float x, float y, float s, ref string tip, ref Rect tipAt)
		{
			float p = GameArt.P;
			Vector2 mousePosition = Event.current.mousePosition;
			WarMap.Layer[] array = new WarMap.Layer[3]
			{
				WarMap.Layer.Kingdoms,
				WarMap.Layer.Alliances,
				WarMap.Layer.Sides
			};
			string[] array2 = new string[3] { "iconKingdomZones", "iconAllianceZones", "iconWarZones" };
			string[] array3 = new string[3] { "Kingdoms", "Alliances", "War sides" };
			for (int i = 0; i < array.Length; i++)
			{
				Rect rect = new Rect(x, y + (float)i * (s + 2f * p), s, s);
				if (rect.Contains(mousePosition))
				{
					tip = array3[i] + ((map.Overlay == array[i]) ? " (click to hide)" : "");
					tipAt = rect;
				}
				if (GameButton(rect, null, map.Overlay == array[i], enabled: true, GameArt.IconNamed(array2[i]) ?? GameArt.IconNamed("iconKingdom")))
				{
					map.Overlay = ((map.Overlay != array[i]) ? array[i] : WarMap.Layer.None);
				}
			}
		}

		private void DrawLayerKey(WarMap map, Rect mapRect)
		{
			if (map.Overlay != WarMap.Layer.Sides && map.Overlay != WarMap.Layer.Alliances)
			{
				return;
			}
			float p = GameArt.P;
			float num = Mathf.Round(6f * p);
			float size = 4.2f;
			List<KeyValuePair<string, Color>> list = new List<KeyValuePair<string, Color>>();
			if (map.Overlay == WarMap.Layer.Sides)
			{
				list.Add(new KeyValuePair<string, Color>("You", WarMap.OwnSide));
				list.Add(new KeyValuePair<string, Color>("Allies", WarMap.AllySide));
				list.Add(new KeyValuePair<string, Color>("Enemies", WarMap.EnemySide));
				list.Add(new KeyValuePair<string, Color>("Neutral", WarMap.NeutralSide));
			}
			else
			{
				for (int i = 0; i < map.Pacts.Count && i < 4; i++)
				{
					list.Add(new KeyValuePair<string, Color>(map.Pacts[i].Name, map.Pacts[i].Color));
				}
				if (map.Pacts.Count > 4)
				{
					list.Add(new KeyValuePair<string, Color>("+" + (map.Pacts.Count - 4) + " more", new Color(0f, 0f, 0f, 0f)));
				}
				if (list.Count == 0)
				{
					list.Add(new KeyValuePair<string, Color>("No alliances in the world", new Color(0f, 0f, 0f, 0f)));
				}
			}
			float num2 = 8f * p;
			foreach (KeyValuePair<string, Color> item in list)
			{
				num2 += ((item.Value.a > 0f) ? (num + 3f * p) : 0f) + GameArt.Measure(item.Key, size).x + 8f * p;
			}
			Rect r = new Rect(Mathf.Round(mapRect.x + 4f * p), Mathf.Round(mapRect.yMax - 16f * p), Mathf.Round(Mathf.Min(num2, mapRect.width - 40f * p)), Mathf.Round(12f * p));
			GameArt.TipPanel(r, 0.9f);
			float num3 = r.x + 5f * p;
			foreach (KeyValuePair<string, Color> item2 in list)
			{
				if (num3 > r.xMax - 10f * p)
				{
					break;
				}
				if (item2.Value.a > 0f)
				{
					GameArt.Flat(new Rect(Mathf.Round(num3 - p), Mathf.Round(r.center.y - num * 0.5f - p), num + 2f * p, num + 2f * p), new Color(0.05f, 0.04f, 0.04f, 0.95f));
					GameArt.Flat(new Rect(Mathf.Round(num3), Mathf.Round(r.center.y - num * 0.5f), num, num), new Color(item2.Value.r, item2.Value.g, item2.Value.b, 1f));
					num3 += num + 3f * p;
				}
				float x = GameArt.Measure(item2.Key, size).x;
				GameArt.Label(new Rect(num3, r.y, x + 2f * p, r.height), item2.Key, size, TextAnchor.MiddleLeft, Color.white);
				num3 += x + 8f * p;
			}
		}

		private static void DrawAllianceBanner(Rect r, Alliance al, float alpha = 1f)
		{
			Sprite sprite = null;
			Sprite sprite2 = null;
			Color tint = Color.white;
			Color tint2 = Color.white;
			try
			{
				sprite = al.getBackgroundSprite();
				sprite2 = al.getIconSprite();
				ColorAsset color = al.getColor();
				tint = color.getColorMainSecond();
				tint2 = color.getColorBanner();
			}
			catch
			{
			}
			if (sprite == null)
			{
				Sprite sprite3 = GameArt.IconNamed("iconAlliance");
				if (sprite3 != null)
				{
					GameArt.IconFit(r, sprite3, new Color(1f, 1f, 1f, alpha));
				}
				return;
			}
			float p = GameArt.P;
			tint.a = alpha;
			tint2.a = alpha;
			GameArt.IconFit(new Rect(r.x + p, r.y + p, r.width, r.height), sprite, new Color(0f, 0f, 0f, 0.5f * alpha));
			GameArt.IconFit(r, sprite, tint);
			if (sprite2 != null)
			{
				GameArt.IconFit(r, sprite2, tint2);
			}
		}

		private void DrawRealmsPage(WarMap map, Actor host, Rect panel)
		{
			float p = GameArt.P;
			bool flag = Event.current.type == EventType.Repaint;
			Rect view = new Rect(panel.x, panel.y, panel.width - 9f * p, panel.height);
			float width = view.width;
			float num = Mathf.Round(20f * p);
			float num2 = Mathf.Round(30f * p);
			float num3 = Mathf.Round(46f * p);
			float num4 = Mathf.Round(21f * p);
			float num5 = Mathf.Round(14f * p);
			float num6 = Mathf.Round(19f * p);
			float num7 = Mathf.Round(3f * p);
			List<WarMap.WarEntry> list = new List<WarMap.WarEntry>();
			List<WarMap.WarEntry> list2 = new List<WarMap.WarEntry>();
			foreach (WarMap.WarEntry allWar in map.AllWars)
			{
				(allWar.Ours ? list : list2).Add(allWar);
			}
			WarMap.Pact pact = null;
			List<WarMap.Pact> list3 = new List<WarMap.Pact>();
			foreach (WarMap.Pact pact2 in map.Pacts)
			{
				if (pact2.Ours)
				{
					pact = pact2;
				}
				else
				{
					list3.Add(pact2);
				}
			}
			List<WarMap.Realm> list4 = new List<WarMap.Realm>();
			foreach (WarMap.Realm realm3 in map.Realms)
			{
				if (realm3.K != map.Home)
				{
					list4.Add(realm3);
				}
			}
			float contentH = num2 + num7 + num + 2f * p + ((list.Count > 0) ? ((float)list.Count * (num3 + num7)) : num6) + num + 2f * p + ((pact != null) ? (num5 + 2f * p + (float)(pact.Members.Count - 1) * (num5 + 1f * p) + num7) : num6) + num + 2f * p + ((list4.Count > 0) ? ((float)list4.Count * (num4 + 2f * p)) : num6) + ((list2.Count > 0) ? (num + 2f * p + (float)list2.Count * (num5 + 2f * p)) : 0f) + ((list3.Count > 0) ? (num + 2f * p + (float)list3.Count * (num5 + 2f * p)) : 0f) + 4f * p;
			float num8 = BeginList(view, ref map.RealmScroll, contentH);
			bool listMouseIn = _listMouseIn;
			Kingdom kingdom = null;
			Kingdom kingdom2 = null;
			WarMap.Realm realm = map.RealmOf(map.Home);
			if (flag && realm != null)
			{
				KingdomCard(new Rect(0f, num8, width, num2), realm.K, realm.Towns + ((realm.Towns == 1) ? " town" : " towns") + "  ·  " + realm.Warriors + " warriors" + ((map.OurShips > 0) ? ("  ·  " + map.OurShips + ((map.OurShips == 1) ? " ship" : " ships")) : ""), map.Picked == realm.K, new Color(0.6f, 0.8f, 1f));
			}
			PadNav.Spot(new Rect(0f, num8, width, num2));
			if (realm != null && GUI.Button(new Rect(0f, num8, width, num2), GUIContent.none, GUIStyle.none) && listMouseIn)
			{
				kingdom = realm.K;
			}
			num8 += num2 + num7;
			SectionTitle(new Rect(0f, num8, width, num), "Your wars", GameArt.IconNamed("iconWar") ?? GameArt.Warfare);
			num8 += num + 2f * p;
			if (list.Count == 0)
			{
				if (flag)
				{
					GameArt.Label(new Rect(3f * p, num8, width - 6f * p, num6), "At peace. Declare war on the Council tab", 4.2f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
				}
				num8 += num6;
			}
			foreach (WarMap.WarEntry item in list)
			{
				Rect rect = new Rect(0f, num8, width, num3);
				if (flag)
				{
					DrawWarCard(map, item, rect);
				}
				PadNav.Spot(rect);
				if (GUI.Button(rect, GUIContent.none, GUIStyle.none) && listMouseIn)
				{
					kingdom2 = item.Foe;
				}
				num8 += num3 + num7;
			}
			SectionTitle(new Rect(0f, num8, width, num), "Alliance", GameArt.IconNamed("iconAlliance"));
			num8 += num + 2f * p;
			if (pact == null)
			{
				if (flag)
				{
					GameArt.Label(new Rect(3f * p, num8, width - 6f * p, num6), "No allies. Propose an alliance on the Council tab", 4.2f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
				}
				num8 += num6;
			}
			else
			{
				if (flag)
				{
					DrawAllianceBanner(new Rect(3f * p, num8, 11f * p, 13.5f * p), pact.A);
					GameArt.Label(new Rect(17f * p, num8, width - 60f * p, num5), pact.Name, 5f, TextAnchor.MiddleLeft, GameArt.Peach);
					GameArt.Label(new Rect(0f, num8, width - 3f * p, num5), pact.Members.Count + " realms  ·  " + pact.Warriors + " warriors", 3.9f, TextAnchor.MiddleRight, GameArt.Grey);
				}
				num8 += num5 + 2f * p;
				foreach (Kingdom member in pact.Members)
				{
					if (member != map.Home)
					{
						WarMap.Realm realm2 = map.RealmOf(member);
						Rect rect2 = new Rect(0f, num8, width, num5);
						if (flag && realm2 != null)
						{
							DrawMemberRow(map, realm2, rect2);
						}
						PadNav.Spot(rect2);
						if (GUI.Button(rect2, GUIContent.none, GUIStyle.none) && listMouseIn)
						{
							kingdom = member;
						}
						num8 += num5 + 1f * p;
					}
				}
				num8 += num7;
			}
			SectionTitle(new Rect(0f, num8, width, num), "Kingdoms", GameArt.IconNamed("iconKingdomList") ?? GameArt.IconNamed("iconKingdom"));
			num8 += num + 2f * p;
			if (list4.Count == 0)
			{
				if (flag)
				{
					GameArt.Label(new Rect(3f * p, num8, width - 6f * p, num6), "No other kingdoms in the world", 4.2f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
				}
				num8 += num6;
			}
			foreach (WarMap.Realm item2 in list4)
			{
				Rect rect3 = new Rect(0f, num8, width, num4);
				if (flag)
				{
					DrawRealmRow(map, item2, rect3);
				}
				PadNav.Spot(rect3);
				if (GUI.Button(rect3, GUIContent.none, GUIStyle.none) && listMouseIn)
				{
					kingdom = item2.K;
				}
				num8 += num4 + 2f * p;
			}
			if (list2.Count > 0)
			{
				SectionTitle(new Rect(0f, num8, width, num), "Other wars", GameArt.IconNamed("iconWarList") ?? GameArt.Warfare);
				num8 += num + 2f * p;
				foreach (WarMap.WarEntry item3 in list2)
				{
					Rect rect4 = new Rect(0f, num8, width, num5);
					if (flag)
					{
						DrawOtherWarRow(map, item3, rect4);
					}
					PadNav.Spot(rect4);
					if (GUI.Button(rect4, GUIContent.none, GUIStyle.none) && listMouseIn)
					{
						kingdom = item3.MainAttacker ?? ((item3.Attackers.Count > 0) ? item3.Attackers[0] : null);
					}
					num8 += num5 + 2f * p;
				}
			}
			if (list3.Count > 0)
			{
				SectionTitle(new Rect(0f, num8, width, num), "Other alliances", GameArt.IconNamed("iconAllianceList") ?? GameArt.IconNamed("iconAlliance"));
				num8 += num + 2f * p;
				foreach (WarMap.Pact item4 in list3)
				{
					Rect rect5 = new Rect(0f, num8, width, num5);
					if (flag)
					{
						DrawPactRow(map, item4, rect5);
					}
					PadNav.Spot(rect5);
					if (GUI.Button(rect5, GUIContent.none, GUIStyle.none) && listMouseIn && item4.Members.Count > 0)
					{
						kingdom = item4.Members[0];
					}
					num8 += num5 + 2f * p;
				}
			}
			EndList(view, ref map.RealmScroll, contentH);
			if (kingdom2 != null)
			{
				Sounds.PlayUI("event:/SFX/Ui/Click");
				map.FocusWar(kingdom2);
				map.Picked = kingdom2;
				map.PickedAt = Time.unscaledTime;
			}
			else if (kingdom != null)
			{
				Sounds.PlayUI("event:/SFX/Ui/Click");
				map.Pick(kingdom, map.Picked != kingdom);
			}
		}

		private void DrawWarCard(WarMap map, WarMap.WarEntry e, Rect card)
		{
			float p = GameArt.P;
			bool flag = e.Foe != null && e.Foe == map.Enemy;
			GameArt.Sliced(card, GameArt.Inner, flag ? Color.white : new Color(0.9f, 0.9f, 0.9f, 1f));
			if (flag)
			{
				GameArt.Sliced(card, GameArt.ButtonBorder, new Color(1f, 0.82f, 0.3f, 1f), 4f, 4f, 4f, 4f);
			}
			string text = (e.Total ? "Total war" : ((e.Years <= 0) ? "This year" : (e.Years + ((e.Years == 1) ? " year" : " years"))));
			float x = GameArt.Measure(text, 3.9f).x;
			GameArt.Label(new Rect(card.x + 5f * p, card.y + 2.5f * p, card.width - x - 14f * p, 9f * p), e.Name, 4.8f, TextAnchor.MiddleLeft, GameArt.Peach);
			GameArt.Label(new Rect(card.x, card.y + 2.5f * p, card.width - 5f * p, 9f * p), text, 3.9f, TextAnchor.MiddleRight, GameArt.Grey);
			List<Kingdom> list = (e.WeAttack ? e.Attackers : e.Defenders);
			List<Kingdom> list2 = (e.WeAttack ? e.Defenders : e.Attackers);
			int fallen = (e.WeAttack ? e.DeadAttackers : e.DeadDefenders);
			int fallen2 = (e.WeAttack ? e.DeadDefenders : e.DeadAttackers);
			int num = 0;
			int num2 = 0;
			foreach (Kingdom item in list)
			{
				num += map.RealmOf(item)?.Warriors ?? 0;
			}
			foreach (Kingdom item2 in list2)
			{
				num2 += map.RealmOf(item2)?.Warriors ?? 0;
			}
			SideRow(map, list, num, fallen, new Rect(card.x + 4f * p, card.y + 13f * p, card.width - 8f * p, 11f * p), WarMap.OwnSide);
			SideRow(map, list2, num2, fallen2, new Rect(card.x + 4f * p, card.y + 25f * p, card.width - 8f * p, 11f * p), WarMap.EnemySide);
			Rect rect = new Rect(Mathf.Round(card.x + 5f * p), Mathf.Round(card.yMax - 8f * p), Mathf.Round(card.width - 10f * p), Mathf.Max(2f, Mathf.Round(3f * p)));
			float num3 = ((num + num2 > 0) ? ((float)num / (float)(num + num2)) : 0.5f);
			GameArt.Flat(new Rect(rect.x - p, rect.y - p, rect.width + 2f * p, rect.height + 2f * p), new Color(0.05f, 0.04f, 0.04f, 0.9f));
			GameArt.Flat(new Rect(rect.x, rect.y, Mathf.Round(rect.width * num3), rect.height), WarMap.OwnSide);
			GameArt.Flat(new Rect(rect.x + Mathf.Round(rect.width * num3), rect.y, rect.width - Mathf.Round(rect.width * num3), rect.height), WarMap.EnemySide);
		}

		private void SideRow(WarMap map, List<Kingdom> side, int warriors, int fallen, Rect r, Color stripe)
		{
			float p = GameArt.P;
			GameArt.Flat(new Rect(r.x, r.y + 1f * p, Mathf.Max(1f, Mathf.Round(1.5f * p)), r.height - 2f * p), stripe);
			string text = warriors + " warriors" + ((fallen > 0) ? ("  ·  " + fallen + " fallen") : "");
			float x = GameArt.Measure(text, 3.9f).x;
			float num = Mathf.Round(8f * p);
			float num2 = Mathf.Round(10f * p);
			float num3 = r.x + 4f * p;
			float num4 = r.xMax - x - 6f * p;
			int num5 = 0;
			foreach (Kingdom item in side)
			{
				if (num3 + num > num4 && num5 < side.Count - 1)
				{
					break;
				}
				DrawBanner(new Rect(Mathf.Round(num3), Mathf.Round(r.y + (r.height - num2) * 0.5f), num, num2), item, civ: true, 1f);
				num3 += num + 1.5f * p;
				num5++;
			}
			if (num5 == 1)
			{
				GameArt.Label(new Rect(num3 + 1f * p, r.y, num4 - num3, r.height), Royal.NameOf(side[0]), 4.2f, TextAnchor.MiddleLeft, map.RealmOf(side[0])?.Text ?? Color.white);
			}
			else if (num5 < side.Count)
			{
				GameArt.Label(new Rect(num3, r.y, 20f * p, r.height), "+" + (side.Count - num5), 3.9f, TextAnchor.MiddleLeft, GameArt.Grey);
			}
			GameArt.Label(r, text, 3.9f, TextAnchor.MiddleRight, Color.white);
		}

		private void DrawMemberRow(WarMap map, WarMap.Realm r, Rect row)
		{
			float p = GameArt.P;
			if (map.Picked == r.K)
			{
				GameArt.Flat(row, new Color(1f, 0.82f, 0.3f, 0.18f));
			}
			else if (row.Contains(Event.current.mousePosition))
			{
				GameArt.Flat(row, new Color(1f, 1f, 1f, 0.06f));
			}
			DrawBanner(new Rect(6f * p + row.x, row.y + 2f * p, 8f * p, 10f * p), r.K, civ: true, 1f);
			GameArt.Label(new Rect(row.x + 17f * p, row.y, row.width * 0.6f, row.height), r.Name, 4.4f, TextAnchor.MiddleLeft, r.Text);
			string text = r.Warriors + " warriors" + ((r.Foes.Count > 0) ? ("  ·  " + r.Foes.Count + ((r.Foes.Count == 1) ? " war" : " wars")) : "");
			GameArt.Label(new Rect(row.x, row.y, row.width - 3f * p, row.height), text, 3.8f, TextAnchor.MiddleRight, (r.Foes.Count > 0) ? new Color(1f, 0.6f, 0.5f) : GameArt.Grey);
		}

		private void DrawRealmRow(WarMap map, WarMap.Realm r, Rect row)
		{
			float p = GameArt.P;
			bool flag = map.Picked == r.K;
			bool flag2 = row.Contains(Event.current.mousePosition);
			GameArt.Flat(row, new Color(0f, 0f, 0f, flag ? 0.32f : (flag2 ? 0.24f : 0.14f)));
			if (flag)
			{
				GameArt.Sliced(row, GameArt.ButtonBorder, new Color(1f, 0.82f, 0.3f, 1f), 4f, 4f, 4f, 4f);
			}
			GameArt.Flat(new Rect(row.x, row.y, Mathf.Max(1f, Mathf.Round(1.5f * p)), row.height), WarMap.SideColor(r.Side));
			DrawBanner(new Rect(row.x + 4f * p, row.y + 3f * p, 12f * p, 15f * p), r.K, civ: true, 1f);
			float num = row.x + 19f * p;
			float num2 = 0f;
			if (r.HasOpinion)
			{
				string text = ((r.Opinion > 0) ? "+" : "") + r.Opinion;
				Color color = ((r.Opinion > 0) ? new Color(0.55f, 1f, 0.5f) : ((r.Opinion < 0) ? new Color(1f, 0.45f, 0.35f) : GameArt.Grey));
				num2 = GameArt.Measure(text, 4.4f).x;
				GameArt.Label(new Rect(row.x, row.y + 1.5f * p, row.width - 4f * p, 9f * p), text, 4.4f, TextAnchor.MiddleRight, color);
				Sprite sprite = GameArt.IconNamed((r.Opinion >= 0) ? "iconFriendship" : "iconSpite");
				if (sprite != null)
				{
					GameArt.Icon(new Rect(Mathf.Round(row.xMax - 4f * p - num2 - 9f * p), Mathf.Round(row.y + 2f * p), 8f * p, 8f * p), sprite);
				}
				num2 += 11f * p;
			}
			string text2 = ((r.Side == WarMap.Side.Neutral) ? "" : WarMap.SideName(r.Side));
			float num3 = row.xMax - num - num2 - 6f * p - ((text2.Length > 0) ? (GameArt.Measure(text2, 3.9f).x + 5f * p) : 0f);
			GameArt.Label(new Rect(num, row.y + 1.5f * p, num3, 9f * p), r.Name, 4.6f, TextAnchor.MiddleLeft, r.Text);
			if (text2.Length > 0)
			{
				float num4 = Mathf.Min(num3, GameArt.Measure(r.Name, 4.6f).x);
				GameArt.Label(new Rect(num + num4 + 4f * p, row.y + 1.5f * p, 60f * p, 9f * p), text2, 3.9f, TextAnchor.MiddleLeft, StateColor(r.Side));
			}
			string text3 = r.Towns + ((r.Towns == 1) ? " town" : " towns") + "  ·  " + r.Warriors + " warriors";
			if (r.Alliance != null && r.Side != WarMap.Side.Ally)
			{
				foreach (WarMap.Pact pact in map.Pacts)
				{
					if (pact.A == r.Alliance)
					{
						text3 = text3 + "  ·  " + pact.Name;
						break;
					}
				}
			}
			if (r.Side == WarMap.Side.Neutral && r.Foes.Count > 0)
			{
				text3 = text3 + "  ·  " + r.Foes.Count + ((r.Foes.Count == 1) ? " war" : " wars");
			}
			GameArt.Label(new Rect(num, row.y + 10.5f * p, row.xMax - num - 4f * p, 9f * p), text3, 3.8f, TextAnchor.MiddleLeft, GameArt.Grey);
		}

		private void DrawOtherWarRow(WarMap map, WarMap.WarEntry e, Rect row)
		{
			float p = GameArt.P;
			if (row.Contains(Event.current.mousePosition))
			{
				GameArt.Flat(row, new Color(1f, 1f, 1f, 0.06f));
			}
			Kingdom kingdom = e.MainAttacker ?? ((e.Attackers.Count > 0) ? e.Attackers[0] : null);
			Kingdom kingdom2 = e.MainDefender ?? ((e.Defenders.Count > 0) ? e.Defenders[0] : null);
			string text = ((e.Years <= 0) ? "" : (e.Years + ((e.Years == 1) ? " year" : " years")));
			float num = GameArt.Measure(text, 3.8f).x + 4f * p;
			float num2 = (row.width - num - 18f * p) * 0.5f;
			float num3 = row.x + 3f * p;
			if (kingdom != null)
			{
				DrawBanner(new Rect(num3, row.y + 2f * p, 8f * p, 10f * p), kingdom, civ: true, 1f);
				GameArt.Label(new Rect(num3 + 10f * p, row.y, num2 - 10f * p, row.height), Royal.NameOf(kingdom) + ((e.Attackers.Count > 1) ? (" +" + (e.Attackers.Count - 1)) : ""), 4f, TextAnchor.MiddleLeft, map.RealmOf(kingdom)?.Text ?? Color.white);
			}
			GameArt.Label(new Rect(num3 + num2, row.y, 14f * p, row.height), "vs", 3.8f, TextAnchor.MiddleCenter, new Color(1f, 0.5f, 0.4f));
			num3 += num2 + 15f * p;
			if (kingdom2 != null)
			{
				DrawBanner(new Rect(num3, row.y + 2f * p, 8f * p, 10f * p), kingdom2, civ: true, 1f);
				GameArt.Label(new Rect(num3 + 10f * p, row.y, num2 - 10f * p, row.height), Royal.NameOf(kingdom2) + ((e.Defenders.Count > 1) ? (" +" + (e.Defenders.Count - 1)) : ""), 4f, TextAnchor.MiddleLeft, map.RealmOf(kingdom2)?.Text ?? Color.white);
			}
			GameArt.Label(new Rect(row.x, row.y, row.width - 3f * p, row.height), text, 3.8f, TextAnchor.MiddleRight, GameArt.Grey);
		}

		private void DrawPactRow(WarMap map, WarMap.Pact p, Rect row)
		{
			float p2 = GameArt.P;
			if (row.Contains(Event.current.mousePosition))
			{
				GameArt.Flat(row, new Color(1f, 1f, 1f, 0.06f));
			}
			DrawAllianceBanner(new Rect(row.x + 3f * p2, row.y + 1f * p2, 10f * p2, 12f * p2), p.A);
			float num = 7f * p2;
			float a = (float)p.Members.Count * (num + 1f * p2);
			float num2 = Mathf.Max(30f * p2, row.width - 18f * p2 - Mathf.Min(a, row.width * 0.5f) - 4f * p2);
			GameArt.Label(new Rect(row.x + 16f * p2, row.y, num2, row.height), p.Name, 4.2f, TextAnchor.MiddleLeft, GameArt.Peach);
			float num3 = row.xMax - 3f * p2 - num;
			int num4 = p.Members.Count - 1;
			while (num4 >= 0 && num3 > row.x + 16f * p2 + num2)
			{
				DrawBanner(new Rect(Mathf.Round(num3), row.y + 2.5f * p2, num, 9f * p2), p.Members[num4], civ: true, 1f);
				num3 -= num + 1f * p2;
				num4--;
			}
		}

		internal void DrawWorldLand(WorldBoxMod mod)
		{
			if (!ReliefWorld.CardOpen)
			{
				return;
			}
			EnsureAssets();
			GameArt.Ensure();
			float p = GameArt.P;
			Rect win = WindowRect(330f, 196f);
			bool close;
			Rect rect = GameWindow(win, "This world's land", "Esc", out close);
			if (close)
			{
				ReliefWorld.Postpone();
				return;
			}
			GameArt.Label(new Rect(rect.x, rect.y, rect.width, 11f * p), "Chosen once for this world. Digging works in both.", 4.6f, TextAnchor.MiddleCenter, GameArt.Grey);
			if (_landFlat == null)
			{
				_landFlat = LandPicture(terrainy: false);
			}
			if (_landTerrainy == null)
			{
				_landTerrainy = LandPicture(terrainy: true);
			}
			float num = 8f * p;
			float num2 = Mathf.Round((rect.width - num) * 0.5f);
			float num3 = Mathf.Round(num2 * 40f / 96f);
			for (int i = 0; i < 2; i++)
			{
				bool flag = i == 1;
				float num4 = Mathf.Round(rect.x + (float)i * (num2 + num));
				float num5 = rect.y + 14f * p;
				Rect position = new Rect(num4, num5, num2, num3);
				if (Event.current.type == EventType.Repaint)
				{
					Fill(new Rect(position.x - p, position.y - p, position.width + 2f * p, position.height + 2f * p), new Color(0.1f, 0.07f, 0.05f, 1f));
					GUI.DrawTexture(position, flag ? _landTerrainy : _landFlat, ScaleMode.StretchToFill, alphaBlend: false);
				}
				num5 += num3 + 4f * p;
				string text = (flag ? "Valleys, ridges, mountain lakes and sea cliffs, made from this world's seed." : "The land as the game draws it.");
				GameArt.Label(new Rect(num4, num5, num2, 22f * p), text, 4.4f, TextAnchor.UpperCenter, Color.white, wrap: true);
				Rect r = new Rect(num4 + Mathf.Round(num2 * 0.12f), Mathf.Round(rect.yMax - 18f * p), Mathf.Round(num2 * 0.76f), Mathf.Round(18f * p));
				if (GameButton(r, flag ? "Terrainy" : "Flat", flag))
				{
					ReliefWorld.Choose((!flag) ? 1 : 2);
				}
			}
		}

		private static Texture2D LandPicture(bool terrainy)
		{
			Texture2D texture2D = new Texture2D(96, 40, TextureFormat.RGBA32, mipChain: false)
			{
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
				name = (terrainy ? "fp_land_terrainy" : "fp_land_flat")
			};
			Color32[] array = new Color32[3840];
			Color32 b = new Color32(132, 182, 226, byte.MaxValue);
			Color32 a = new Color32(206, 226, 240, byte.MaxValue);
			Color32 color = new Color32(46, 104, 170, byte.MaxValue);
			Color32 color2 = new Color32(86, 150, 64, byte.MaxValue);
			Color32 color3 = new Color32(122, 182, 82, byte.MaxValue);
			Color32 color4 = new Color32(112, 82, 54, byte.MaxValue);
			Color32 color5 = new Color32(122, 116, 110, byte.MaxValue);
			Color32 color6 = new Color32(88, 82, 78, byte.MaxValue);
			Color32 color7 = new Color32(74, 140, 204, byte.MaxValue);
			for (int i = 0; i < 96; i++)
			{
				bool river;
				bool cliff;
				float num = Ground(i, terrainy, out river, out cliff);
				for (int j = 0; j < 40; j++)
				{
					Color32 color8 = Color32.Lerp(a, b, (float)j / 40f);
					float num2 = (float)j + 0.5f;
					if (i < 14 && num2 < 9f)
					{
						color8 = color;
					}
					if (num2 <= num && i >= 14)
					{
						float num3 = num - num2;
						bool flag = cliff || (terrainy && num > 20f);
						color8 = ((!(num3 < 1.2f)) ? ((!(num3 < 3f)) ? ((!flag) ? color4 : ((((int)(num2 / 2.2f) & 1) == 0) ? color5 : color6)) : (flag ? color6 : color2)) : (flag ? color5 : color3));
					}
					if (river && num2 <= 10.5f && num2 > num)
					{
						color8 = color7;
					}
					array[j * 96 + i] = color8;
				}
			}
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
			return texture2D;
		}

		private static float Ground(int x, bool terrainy, out bool river, out bool cliff)
		{
			river = false;
			cliff = false;
			if (x < 14)
			{
				return 0f;
			}
			if (!terrainy)
			{
				return 10f + 2.2f * Mathf.Sin((float)x * 0.09f) + 1.2f * Mathf.Sin((float)x * 0.23f + 1f);
			}
			if (x < 22)
			{
				cliff = x < 16;
				return 21f + (float)(x - 14) * 0.4f;
			}
			if (x < 34)
			{
				return 24f - (float)(x - 22) * 1.1f;
			}
			if (x < 44)
			{
				river = x >= 37 && x < 41;
				if (!river)
				{
					return 11.5f + Mathf.Abs((float)x - 39f) * 0.4f;
				}
				return 9f;
			}
			if (x < 70)
			{
				return 12f + Mathf.Floor((float)(x - 44) / 4.5f) * 2.4f;
			}
			return 27f + ((x < 82) ? ((float)(x - 70) * 0.7f) : ((float)(96 - x) * 0.6f));
		}

		private Sprite NewsIcon(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return GameArt.IconNamed("iconWorldLog");
			}
			if (_newsIcons.TryGetValue(path, out var value))
			{
				return value;
			}
			try
			{
				value = SpriteTextureLoader.getSprite(path);
			}
			catch
			{
				value = null;
			}
			if (value == null)
			{
				value = GameArt.IconNamed("iconWorldLog");
			}
			_newsIcons[path] = value;
			return value;
		}

		private static Color ToneColor(NewsTone t)
		{
			switch (t)
			{
			case NewsTone.Bad:
				return new Color(1f, 0.36f, 0.26f);
			case NewsTone.Warn:
				return new Color(1f, 0.62f, 0.18f);
			case NewsTone.Good:
				return new Color(0.5f, 0.95f, 0.4f);
			default:
				return new Color(0.55f, 0.75f, 1f);
			}
		}

		public void DrawWorldNews()
		{
			EnsureAssets();
			GameArt.Ensure();
			Actor host = _mod.Host;
			float p = GameArt.P;
			bool close;
			Rect rect = MapWindow("World News", out close);
			if (close)
			{
				WorldNews.Open = false;
				WorldNews.MarkRead();
				return;
			}
			bool flag = host != null;
			Vector2 vector = Vector2.zero;
			try
			{
				if (flag)
				{
					vector = host.current_position;
				}
			}
			catch
			{
				flag = false;
			}
			float num = rect.y;
			Kingdom realm = WorldNews.Realm;
			if (realm != null)
			{
				Rect card = new Rect(rect.x, num, rect.width, Mathf.Round(26f * p));
				if (Event.current.type == EventType.Repaint)
				{
					KingdomCard(card, realm, RealmLine(realm), picked: false, GameArt.Grey);
				}
				num += card.height + 3f * p;
			}
			int more;
			List<WorldNews.Siege> list = WorstSieges(3, out more);
			foreach (WorldNews.Siege item in list)
			{
				Rect r = new Rect(rect.x, num, rect.width, Mathf.Round(13f * p));
				bool flag2 = r.Contains(Event.current.mousePosition);
				if (Event.current.type == EventType.Repaint)
				{
					GameArt.TipPanel(r);
					float num2 = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6f);
					GameArt.Sliced(r, GameArt.ButtonBorder, new Color(1f, 0.3f, 0.2f, flag2 ? 1f : num2), 4f, 4f, 4f, 4f);
					GameArt.Icon(new Rect(r.x + 3f * p, r.y + 1.5f * p, 10f * p, 10f * p), NewsIcon("ui/Icons/iconWar"), Color.white);
					string text = WorldNews.AttackerName(item.By);
					string text2 = (item.Capital ? "Your capital " : (item.Home ? "Your home " : "")) + item.Name + ((text != null) ? (" is under attack by " + text) : " is under attack");
					GameArt.Label(new Rect(r.x + 16f * p, r.y, r.width * 0.55f, r.height), text2, 4.6f, TextAnchor.MiddleLeft, new Color(1f, 0.55f, 0.45f));
					Rect r2 = new Rect(r.x + r.width * 0.58f, r.y + 3f * p, r.width * 0.2f, r.height - 6f * p);
					GameArt.Bar(r2, item.Progress, new Color(0.86f, 0.2f, 0.12f), 0f, Mathf.RoundToInt(item.Progress * 100f) + "%", 1f, 3.8f);
					if (flag)
					{
						GameArt.Label(new Rect(r2.xMax + 3f * p, r.y, r.xMax - r2.xMax - 6f * p, r.height), Service.Heading(vector, item.At), 3.9f, TextAnchor.MiddleRight, GameArt.Grey);
					}
				}
				if (Button(r, GUIContent.none, GUIStyle.none) && flag)
				{
					MarkNews(item.At, item.Name, vector);
				}
				num += r.height + 2f * p;
			}
			if (more > 0)
			{
				GameArt.Label(new Rect(rect.x + 4f * p, num - 1f * p, rect.width - 8f * p, 8f * p), "and " + more + ((more == 1) ? " more city" : " more cities") + " under attack", 4f, TextAnchor.MiddleLeft, new Color(1f, 0.55f, 0.45f));
				num += 8f * p;
			}
			if (list.Count > 0)
			{
				num += 2f * p;
			}
			float num3 = Mathf.Round(14f * p);
			float num4 = Mathf.Round(3f * p);
			int num5 = WorldNews.Filters.Length;
			float num6 = Mathf.Round((rect.width - (float)(num5 - 1) * num4) / (float)num5);
			for (int i = 0; i < num5; i++)
			{
				string text3 = WorldNews.Filters[i];
				if (i == 2)
				{
					text3 = text3 + " (" + Mathf.RoundToInt(70f) + ")";
				}
				if (GameButton(new Rect(rect.x + (float)i * (num6 + num4), num, num6, num3), text3, WorldNews.Filter == i, enabled: true, null, 4.4f))
				{
					WorldNews.Filter = i;
					WorldNews.Scroll = Vector2.zero;
				}
			}
			num += num3 + 4f * p;
			List<NewsItem> list2 = WorldNews.Shown(vector, flag);
			Rect view = new Rect(rect.x, num, rect.width - 9f * p, rect.yMax - num);
			if (list2.Count == 0)
			{
				GameArt.Sliced(new Rect(view.x, view.y, rect.width, view.height), GameArt.Inner, new Color(1f, 1f, 1f, 0.6f));
				string text4 = ((WorldNews.Filter != 1) ? ((WorldNews.Filter == 2) ? ("Nothing has happened within " + Mathf.RoundToInt(70f) + " tiles of you lately.") : ((WorldNews.Filter == 3) ? "No wars, sieges or conquests to tell of. Long may it last." : "No news yet. Kings, wars, cities and disasters will be told of here as they happen.")) : ((realm == null) ? "You belong to no realm. Join a town (talk to its leader) and its news will be yours." : "No news of your realm yet. Should a city of yours be attacked or taken, you'll hear of it at once."));
				GameArt.Label(new Rect(view.x + 6f * p, view.y + 5f * p, rect.width - 12f * p, view.height - 10f * p), text4, 4.4f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
				return;
			}
			float num7 = Mathf.Round(25f * p);
			float contentH = Mathf.Max(view.height, (float)list2.Count * (num7 + num4));
			float num8 = BeginList(view, ref WorldNews.Scroll, contentH);
			for (int j = 0; j < list2.Count; j++)
			{
				NewsItem newsItem = list2[j];
				Rect rect2 = new Rect(0f, num8 + (float)j * (num7 + num4), view.width, num7);
				Vector2 at;
				bool flag3 = newsItem.Where(out at);
				bool num9 = rect2.yMax >= 0f && rect2.y <= view.height;
				bool flag4 = _listMouseIn && flag3 && flag && rect2.Contains(Event.current.mousePosition);
				bool flag5 = flag3 && IsMarked(at);
				if (num9 && Event.current.type == EventType.Repaint)
				{
					GameArt.Sliced(rect2, GameArt.Inner, (flag4 || flag5) ? Color.white : new Color(0.9f, 0.9f, 0.9f, 1f));
					if (flag5)
					{
						GameArt.Sliced(rect2, GameArt.ButtonBorder, Pings.ColorOf(PingKind.Go), 4f, 4f, 4f, 4f);
					}
					else if (flag4)
					{
						GameArt.Sliced(rect2, GameArt.ButtonBorder, new Color(1f, 1f, 1f, 0.7f), 4f, 4f, 4f, 4f);
					}
					if (newsItem.Mine)
					{
						GameArt.Flat(new Rect(rect2.x + 3f * p, rect2.y + 4f * p, 2f * p, rect2.height - 8f * p), ToneColor(newsItem.Tone));
					}
					PortraitWell(new Rect(rect2.x + 7f * p, rect2.y + 3f * p, num7 - 6f * p, num7 - 6f * p), NewsIcon(newsItem.Icon));
					float num10 = rect2.x + num7 + 4f * p;
					float num11 = rect2.xMax - num10 - 4f * p;
					bool flag6 = WorldNews.IsNew(newsItem);
					float num12 = num11 - (flag6 ? (16f * p) : 0f);
					GameArt.Label(new Rect(num10, rect2.y + 1.5f * p, num12, 15f * p), FitNewsText(newsItem.Text, 4.5f, num12 * 1.8f), 4.5f, TextAnchor.UpperLeft, Color.white, wrap: true);
					if (flag6)
					{
						GameArt.Label(new Rect(rect2.xMax - 18f * p, rect2.y + 1.5f * p, 14f * p, 7f * p), "new", 3.7f, TextAnchor.UpperRight, GameArt.Orange);
					}
					GameArt.Label(new Rect(num10, rect2.y + 16.5f * p, num11 * 0.5f, 7f * p), WorldNews.When(newsItem), 3.7f, TextAnchor.MiddleLeft, GameArt.Grey);
					if (flag3 && flag)
					{
						GameArt.Label(new Rect(num10 + num11 * 0.4f, rect2.y + 16.5f * p, num11 * 0.6f, 7f * p), Service.Heading(vector, at) + (flag5 ? "  ·  marked" : ""), 3.7f, TextAnchor.MiddleRight, flag5 ? Pings.ColorOf(PingKind.Go) : GameArt.Grey);
					}
				}
				PadNav.Spot(rect2);
				if (GUI.Button(rect2, GUIContent.none, GUIStyle.none) && _listMouseIn && flag3 && flag)
				{
					Sounds.PlayUI("event:/SFX/Ui/Click");
					MarkNews(at, NewsTags.Replace(newsItem.Text, ""), vector);
				}
			}
			EndList(view, ref WorldNews.Scroll, contentH);
		}

		private string RealmLine(Kingdom k)
		{
			if (k == _realmLineOf && Time.unscaledTime - _realmLineAt < 1f && _realmLine != null)
			{
				return _realmLine;
			}
			_realmLineOf = k;
			_realmLineAt = Time.unscaledTime;
			List<string> list = new List<string>();
			try
			{
				if (k.capital != null && k.capital.isAlive())
				{
					list.Add("capital " + k.capital.name);
				}
				else
				{
					list.Add("no capital");
				}
			}
			catch
			{
			}
			try
			{
				int num = 0;
				foreach (City city in k.getCities())
				{
					if (city != null && city.isAlive())
					{
						num++;
					}
				}
				list.Add((num == 1) ? "1 city" : (num + " cities"));
			}
			catch
			{
			}
			try
			{
				List<string> list2 = new List<string>();
				foreach (War war in k.getWars())
				{
					if (war == null)
					{
						continue;
					}
					foreach (Kingdom item in war.getOppositeSideKingdom(k))
					{
						if (item != null && !list2.Contains(item.name))
						{
							list2.Add(item.name);
						}
					}
				}
				list.Add((list2.Count == 0) ? "at peace" : ("at war with " + ((list2.Count <= 3) ? string.Join(", ", list2.ToArray()) : (list2[0] + ", " + list2[1] + " and " + (list2.Count - 2) + " more"))));
			}
			catch
			{
			}
			_realmLine = string.Join("  ·  ", list.ToArray());
			return _realmLine;
		}

		private bool IsMarked(Vector2 at)
		{
			foreach (Ping item in _mod.Pings.List)
			{
				if (item.Kind == PingKind.Go && (new Vector2(item.At.x, item.At.y) - at).sqrMagnitude < 2f)
				{
					return true;
				}
			}
			return false;
		}

		private void MarkNews(Vector2 at, string what, Vector2 me)
		{
			foreach (Ping item in _mod.Pings.List)
			{
				if (item.Kind == PingKind.Go && (new Vector2(item.At.x, item.At.y) - at).sqrMagnitude < 2f)
				{
					_mod.Pings.Remove(item);
					_mod.Toast("Mark taken off");
					return;
				}
			}
			float z = 0f;
			try
			{
				z = _mod.GroundAt(at);
			}
			catch
			{
			}
			_mod.Pings.Add(PingKind.Go, new Vector3(at.x, at.y, z), null, null, far: true);
			_mod.Toast("Marked on your map and compass: " + Service.Heading(me, at));
		}

		internal bool MarkNewsForTest(NewsItem n)
		{
			if (_mod.Host == null || !n.Where(out var at))
			{
				return false;
			}
			MarkNews(at, NewsTags.Replace(n.Text, ""), _mod.Host.current_position);
			return true;
		}

		private void DrawNewsAlerts(float sw, float sh)
		{
			if (WorldNews.Open || _mod.Settings.NewsAlerts <= 0)
			{
				return;
			}
			float p = GameArt.P;
			float unscaledTime = Time.unscaledTime;
			NewsItem banner = WorldNews.Banner;
			if (banner == null || unscaledTime >= WorldNews.BannerUntil)
			{
				return;
			}
			float num = Mathf.Clamp01((unscaledTime - WorldNews.BannerSince) / 0.25f) * Mathf.Clamp01((WorldNews.BannerUntil - unscaledTime) / 0.5f);
			float num2 = Mathf.Round(Mathf.Min(14f * p, sh * 0.06f));
			float num3 = Mathf.Round(Mathf.Min(sw * 0.5f, 300f * p));
			float num4 = Mathf.Round(num2 - 4f * p);
			float num5 = Mathf.Round(4f * p);
			float num6 = Mathf.Max(Mathf.Round(8.6f * p), GameArt.Measure(KeyCode.M.ToString(), 3.6f).x + 4f * p);
			string text = null;
			if (banner.Where(out var at) && _mod.Host != null)
			{
				try
				{
					text = Service.Heading(_mod.Host.current_position, at);
				}
				catch
				{
				}
			}
			float num7 = ((text != null) ? GameArt.Measure(text, 3.7f).x : 0f);
			float num8 = num5 + num4 + num5 + num5 + num6 + num5;
			float num9 = num3 - num8;
			if (text != null && GameArt.Measure(banner.Text, 4.6f).x + 6f * p + num7 > num9)
			{
				text = null;
			}
			float w = num9 - ((text != null) ? (num7 + 6f * p) : 0f);
			string text2 = FitNewsText(banner.Text, 4.6f, w);
			float x = GameArt.Measure(text2, 4.6f).x;
			float num10 = Mathf.Round(Mathf.Min(num3, num8 + x + ((text != null) ? (6f * p + num7) : 0f)));
			float num11 = (1f - Smooth(Mathf.Clamp01((unscaledTime - WorldNews.BannerSince) / 0.25f))) * 4f * p;
			float num12 = Mathf.Round(57f * p);
			Rect r = new Rect(Mathf.Round((sw - num10) * 0.5f), Mathf.Round(num12 - num11), num10, num2);
			GameArt.TipPanel(r, num);
			Color color = ToneColor(banner.Tone);
			float num13 = ((banner.Tone == NewsTone.Bad || banner.Tone == NewsTone.Warn) ? (0.55f + 0.45f * Mathf.Sin(unscaledTime * 7f)) : 0.8f);
			GameArt.Sliced(r, GameArt.ButtonBorder, new Color(color.r, color.g, color.b, num13 * num), 4f, 4f, 4f, 4f);
			GameArt.Icon(new Rect(r.x + num5, Mathf.Round(r.y + (num2 - num4) * 0.5f), num4, num4), NewsIcon(banner.Icon), new Color(1f, 1f, 1f, num));
			float num14 = r.x + num5 + num4 + num5;
			GameArt.Label(new Rect(num14, r.y - 0.5f * p, x + 2f * p, num2), text2, 4.6f, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, num));
			if (text != null)
			{
				GameArt.Label(new Rect(num14 + x + 6f * p, r.y - 0.5f * p, num7 + 2f * p, num2), text, 3.7f, TextAnchor.MiddleLeft, new Color(GameArt.Grey.r, GameArt.Grey.g, GameArt.Grey.b, num));
			}
			GameArt.KeyCap(r.xMax - num5 - num6, r.y + num2 * 0.5f, KeyCode.M.ToString(), 3.6f, num);
			_newsBannerRect = new Rect(r.x, num12, num10, num2);
			_newsBannerDrawnAt = unscaledTime;
			_newsBannerBottom = Mathf.Round(num12 + num2 + 3f * p);
			_newsBannerFrame = Time.frameCount;
		}

		private string FitNewsText(string rich, float size, float w)
		{
			if (rich == _fitFor && size == _fitSize && Mathf.Abs(w - _fitW) < 1f)
			{
				return _fitOut;
			}
			_fitFor = rich;
			_fitSize = size;
			_fitW = w;
			if (GameArt.Measure(rich, size).x <= w)
			{
				return _fitOut = rich;
			}
			string text = NewsTags.Replace(Lang.T(rich), "");
			int num = 0;
			int num2 = text.Length;
			while (num < num2)
			{
				int num3 = (num + num2 + 1) / 2;
				if (GameArt.Measure(text.Substring(0, num3) + "...", size).x <= w)
				{
					num = num3;
				}
				else
				{
					num2 = num3 - 1;
				}
			}
			return _fitOut = text.Substring(0, num).TrimEnd() + "...";
		}

		private float DrawSiegeCards(float x, float y, float w, Actor host)
		{
			if (_mod.Settings.NewsAlerts <= 0 || WorldNews.Sieges.Count == 0)
			{
				return y;
			}
			float p = GameArt.P;
			Vector2 vector = Vector2.zero;
			try
			{
				vector = host.current_position;
			}
			catch
			{
			}
			int more;
			WorldNews.Siege siege = WorstSieges(1, out more)[0];
			Rect r = new Rect(x, y, w, Mathf.Round(16f * p));
			GameArt.TipPanel(r, 0.92f);
			if (siege.Capital || siege.Home)
			{
				GameArt.Sliced(r, GameArt.ButtonBorder, new Color(1f, 0.3f, 0.2f, 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f)), 4f, 4f, 4f, 4f);
			}
			GameArt.Icon(new Rect(r.x + 3f * p, r.y + 3f * p, 10f * p, 10f * p), NewsIcon("ui/Icons/iconWar"), Color.white);
			string text = ((more > 0) ? ("+" + more + " (" + KeyCode.M.ToString() + ")") : null);
			float num = ((text != null) ? (GameArt.Measure(text, 3.8f).x + 3f * p) : 0f);
			string text2 = FitNewsText((siege.Capital ? "Capital " : (siege.Home ? "Home " : "")) + siege.Name + " besieged", 4.4f, w - 20f * p - num);
			GameArt.Label(new Rect(r.x + 16f * p, r.y + 0.5f * p, w - 20f * p - num, 8f * p), text2, 4.4f, TextAnchor.MiddleLeft, new Color(1f, 0.5f, 0.4f));
			if (text != null)
			{
				GameArt.Label(new Rect(r.xMax - 4f * p - num, r.y + 0.5f * p, num, 8f * p), text, 3.8f, TextAnchor.MiddleRight, new Color(1f, 0.5f, 0.4f));
			}
			Rect r2 = new Rect(r.x + 16f * p, r.y + 9f * p, w * 0.3f, 5f * p);
			GameArt.Bar(r2, siege.Progress, new Color(0.86f, 0.2f, 0.12f), 0f, Mathf.RoundToInt(siege.Progress * 100f) + "%", 1f, 3.2f);
			GameArt.Label(new Rect(r2.xMax + 3f * p, r.y + 8f * p, r.xMax - r2.xMax - 6f * p, 7f * p), ((WorldNews.AttackerName(siege.By) != null) ? (WorldNews.AttackerName(siege.By) + "  ·  ") : "") + Service.Heading(vector, siege.At), 3.6f, TextAnchor.MiddleRight, GameArt.Grey);
			return y + r.height + 4f * p;
		}

		private List<WorldNews.Siege> WorstSieges(int most, out int more)
		{
			more = Mathf.Max(0, WorldNews.Sieges.Count - most);
			return WorldNews.Worst(WorldNews.Sieges, _worstSieges, most);
		}

		private Rect DrawCourtSeats(Rect inner, Kingdom home, Actor host)
		{
			float p = GameArt.P;
			float num = Mathf.Round(44f * p);
			float num2 = Mathf.Round(4f * p);
			float num3 = Mathf.Round(92f * p);
			float num4 = Mathf.Floor((inner.width - num3 - num2 * 5f) / 5f);
			for (int i = 0; i < Court.Seats.Length; i++)
			{
				DrawSeatChip(new Rect(inner.x + (float)i * (num4 + num2), inner.y, num4, num), home, Court.Seats[i]);
			}
			Rect rect = new Rect(inner.xMax - num3, inner.y, num3, num);
			bool flag = Court.AtThrone(host);
			int num5 = CourtThrone.Waiting(host);
			float num6 = Mathf.Round(16f * p);
			if (GameButton(new Rect(rect.x, rect.y + 2f * p, rect.width, num6), (num5 > 0) ? ("Hold court (" + num5 + ")") : "Hold court", red: false, flag, GameArt.Crown, 4.4f))
			{
				CourtThrone.Ask(host, _mod.Toast);
			}
			if (Event.current.type == EventType.Repaint)
			{
				Rect r = new Rect(rect.x, rect.y + num6 + 4f * p, rect.width, num - num6 - 4f * p);
				GameArt.TipPanel(r, 0.9f);
				bool flag2 = flag;
				string text = (flag ? "The council travels with you." : Court.ThroneLine(host));
				GameArt.Label(new Rect(r.x + 4f * p, r.y + 1f * p, r.width - 8f * p, r.height - 2f * p), text, 3.8f, TextAnchor.MiddleCenter, flag2 ? new Color(0.6f, 1f, 0.55f) : GameArt.Peach, wrap: true);
			}
			float num7 = inner.y + num + Mathf.Round(5f * p);
			return new Rect(inner.x, num7, inner.width, inner.yMax - num7);
		}

		private void DrawSeatChip(Rect chip, Kingdom home, Seat s)
		{
			if (Event.current.type != EventType.Repaint)
			{
				return;
			}
			float p = GameArt.P;
			Actor actor = Court.Holder(home, s);
			bool num = Court.Focus == s && Time.unscaledTime - Court.FocusAt < 5f;
			GameArt.Sliced(chip, GameArt.Inner, (actor != null) ? Color.white : new Color(0.8f, 0.8f, 0.8f, 1f));
			if (num)
			{
				GameArt.Sliced(chip, GameArt.ButtonBorder, new Color(1f, 0.82f, 0.3f, 1f), 4f, 4f, 4f, 4f);
			}
			float num2 = chip.height - 8f * p;
			Sprite s2 = null;
			try
			{
				if (actor != null)
				{
					s2 = actor.getSpriteToRender();
				}
			}
			catch
			{
			}
			PortraitWell(new Rect(chip.x + 4f * p, chip.y + 4f * p, num2, num2), s2, (actor != null) ? 1f : 0.45f);
			float num3 = chip.x + num2 + 8f * p;
			float width = chip.xMax - num3 - 3f * p;
			GameArt.Label(new Rect(num3, chip.y + 2.5f * p, width, 9f * p), Court.Title(s), 5f, TextAnchor.MiddleLeft, GameArt.Orange);
			string text = null;
			try
			{
				if (actor != null)
				{
					text = Renames.Apply(actor.getName());
				}
			}
			catch
			{
			}
			GameArt.Label(new Rect(num3, chip.y + 11.5f * p, width, 8f * p), (actor != null) ? (text ?? "?") : "Seat empty", 4.3f, TextAnchor.MiddleLeft, (actor != null) ? Color.white : GameArt.Grey);
			string text2 = ((actor != null) ? Court.EffectLine(home, s) : null);
			string text3 = ((actor == null) ? "Appoint someone in talk." : (text2 ?? ("Runs " + Court.Duty(s) + ".")));
			GameArt.Label(new Rect(num3, chip.y + 20f * p, width, chip.height - 22f * p), text3, 3.6f, TextAnchor.UpperLeft, (text2 != null) ? GameArt.Peach : GameArt.Grey, wrap: true);
		}

		internal static bool CourtPart(Actor host, Kingdom home, Seat s, bool courtBusiness)
		{
			return true;
		}

		internal float FreedomHeightForTest(Actor host)
		{
			return FreedomHeight(host);
		}

		private float FreedomHeight(Actor host)
		{
			if (Freedom.HomeTown(host) == null || Royal.IsKing(host))
			{
				return 0f;
			}
			float p = GameArt.P;
			return Mathf.Round(10f * p) + Mathf.Round(26f * p) + Mathf.Round(3f * p);
		}

		private float DrawFreedom(Actor host, Rect view, float ry, bool paint)
		{
			if (FreedomHeight(host) <= 0f)
			{
				return ry;
			}
			float p = GameArt.P;
			City city = Freedom.HomeTown(host);
			float num = Mathf.Round(10f * p);
			float num2 = Mathf.Round(26f * p);
			if (paint)
			{
				GameArt.Label(new Rect(0f, ry, view.width, num), "Your town", 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
			}
			ry += num;
			Rect r = new Rect(0f, ry, view.width, num2);
			string text = Freedom.WhyNot(host, city);
			string text2 = null;
			try
			{
				text2 = Royal.NameOf(city.kingdom);
			}
			catch
			{
			}
			bool flag = text == null && Time.unscaledTime - _freedomAskedAt < 4f;
			if (paint)
			{
				GameArt.Sliced(r, GameArt.Inner, new Color(0.9f, 0.9f, 0.9f, 1f));
				GameArt.Label(new Rect(r.x + 6f * p, r.y + 2f * p, r.width * 0.5f, 9f * p), Rep.NameOf(city) ?? "Your town", 4.5f, TextAnchor.MiddleLeft, Color.white);
				string text3 = text ?? (flag ? ("War with " + (text2 ?? "your realm")) : ("Break away from " + (text2 ?? "your realm")));
				GameArt.Label(new Rect(r.x + 6f * p, r.y + 12.5f * p, r.width * 0.5f, 9f * p), text3, 3.8f, TextAnchor.MiddleLeft, (text != null) ? GameArt.Grey : RepHud.Gold);
			}
			Rect r2 = new Rect(r.xMax - Mathf.Round(r.width * 0.46f) - 3f * p, r.y + 4f * p, Mathf.Round(r.width * 0.46f), num2 - 8f * p);
			bool flag2 = r.yMax >= 0f && r.y <= view.height;
			if (GameButton(r2, flag ? "Confirm" : "Declare independence", red: true, text == null, null, 4.2f, _listMouseIn && flag2))
			{
				if (!flag)
				{
					_freedomAskedAt = Time.unscaledTime;
				}
				else
				{
					_freedomAskedAt = -10f;
					if (Freedom.Declare(host, _mod.Toast))
					{
						_mod.CloseMapWindow();
					}
				}
			}
			return ry + num2 + Mathf.Round(3f * p);
		}

		private void DrawJournalStanding(Rect area, Vector2 me)
		{
			float p = GameArt.P;
			Actor host = _mod.Host;
			List<KeyValuePair<City, int>> list = Rep.Towns(host);
			List<string> list2 = RepHud.Titles(host);
			List<Kingdom> list3 = new List<Kingdom>();
			try
			{
				if (Rep.Civ(host.kingdom))
				{
					list3.Add(host.kingdom);
				}
			}
			catch
			{
			}
			foreach (KeyValuePair<Kingdom, int> item in Rep.Realms(host))
			{
				if (!list3.Contains(item.Key))
				{
					list3.Add(item.Key);
				}
			}
			foreach (KeyValuePair<City, int> item2 in list)
			{
				Kingdom kingdom = Rep.RealmOf(item2.Key);
				if (kingdom != null && !list3.Contains(kingdom))
				{
					list3.Add(kingdom);
				}
			}
			float y = area.y;
			string text = ((list2.Count > 0) ? string.Join("  ·  ", list2.ToArray()) : "No titles");
			GameArt.Label(new Rect(area.x, y, area.width, 9f * p), text, 4.4f, TextAnchor.MiddleLeft, (list2.Count > 0) ? RepHud.Gold : GameArt.Grey);
			y += 11f * p;
			Rect view = new Rect(area.x, y, area.width - 9f * p, area.yMax - y);
			if (list.Count == 0 && list3.Count == 0 && FreedomHeight(host) <= 0f)
			{
				GameArt.Sliced(new Rect(view.x, view.y, area.width, view.height), GameArt.Inner, new Color(1f, 1f, 1f, 0.6f));
				GameArt.Label(new Rect(view.x + 6f * p, view.y + 5f * p, area.width - 12f * p, view.height - 10f * p), "No town knows you yet. Work, trade, take on tasks and defend a town to earn its trust.", 4.4f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
				return;
			}
			float num = Mathf.Round(22f * p);
			float num2 = Mathf.Round(3f * p);
			float num3 = Mathf.Round(10f * p);
			float contentH = Mathf.Max(view.height, num3 * 2f + (float)(list3.Count + list.Count) * (num + num2) + FreedomHeight(host));
			float num4 = BeginList(view, ref Journal.Scroll, contentH);
			bool flag = Event.current.type == EventType.Repaint;
			if (list3.Count > 0)
			{
				if (flag)
				{
					GameArt.Label(new Rect(0f, num4, view.width, num3), "Realms", 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
				}
				num4 += num3;
				foreach (Kingdom item3 in list3)
				{
					string text2 = null;
					try
					{
						text2 = item3.name;
					}
					catch
					{
					}
					int points = Rep.StandingPoints(host, item3);
					if (flag && num4 + num >= 0f && num4 <= view.height)
					{
						StandingRow(new Rect(0f, num4, view.width, num), item3, text2 ?? "A realm", points, Rep.LevelOf(points), Rep.Reigns(host, item3));
					}
					num4 += num + num2;
				}
			}
			if (list.Count > 0)
			{
				if (flag)
				{
					GameArt.Label(new Rect(0f, num4, view.width, num3), "Towns", 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
				}
				num4 += num3;
				foreach (KeyValuePair<City, int> item4 in list)
				{
					Kingdom kg = null;
					string text3 = null;
					bool rules = Rep.Rules(host, item4.Key);
					try
					{
						kg = item4.Key.kingdom;
						text3 = item4.Key.name + "  ·  " + TownTier.Word(TownTier.Of(item4.Key));
					}
					catch
					{
					}
					if (flag && num4 + num >= 0f && num4 <= view.height)
					{
						StandingRow(new Rect(0f, num4, view.width, num), kg, text3 ?? "A town", item4.Value, Rep.LevelOf(item4.Value), rules);
					}
					num4 += num + num2;
				}
			}
			num4 = DrawFreedom(host, view, num4, flag);
			EndList(view, ref Journal.Scroll, contentH);
		}

		private void DrawEntryRows(List<JournalEntry> extra, JournalEntry picked, float top, Rect view, float rowH, float gap, Vector2 me)
		{
			float p = GameArt.P;
			for (int i = 0; i < extra.Count; i++)
			{
				JournalEntry journalEntry = extra[i];
				Rect rect = new Rect(0f, top + (float)i * (rowH + gap), view.width, rowH);
				bool flag = journalEntry == picked;
				bool flag2 = _listMouseIn && rect.Contains(Event.current.mousePosition);
				if (rect.yMax >= 0f && rect.y <= view.height && Event.current.type == EventType.Repaint)
				{
					GameArt.Sliced(rect, GameArt.Inner, (flag2 || flag) ? Color.white : new Color(0.9f, 0.9f, 0.9f, 1f));
					Color tint = (journalEntry.Ready ? new Color(1f, 0.92f, 0.45f) : (flag ? new Color(1f, 1f, 1f, 0.85f) : (flag2 ? new Color(1f, 1f, 1f, 0.55f) : Color.clear)));
					if (tint.a > 0f)
					{
						GameArt.Sliced(rect, GameArt.ButtonBorder, tint, 4f, 4f, 4f, 4f);
					}
					GameArt.Flat(new Rect(rect.x + 3f * p, rect.y + 4f * p, 2f * p, rect.height - 8f * p), journalEntry.Ready ? new Color(1f, 0.92f, 0.45f) : EntryStripe);
					float num = rect.x + 9f * p;
					float num2 = rect.xMax - num - 4f * p;
					GameArt.Label(new Rect(num, rect.y + 1.5f * p, num2 * 0.68f, 9f * p), journalEntry.Title ?? "", 4.6f, TextAnchor.MiddleLeft, journalEntry.Ready ? new Color(1f, 0.92f, 0.45f) : Color.white);
					if (journalEntry.TimeLeft != null)
					{
						GameArt.Label(new Rect(num + num2 * 0.68f, rect.y + 1.5f * p, num2 * 0.32f, 9f * p), journalEntry.TimeLeft, 3.8f, TextAnchor.MiddleRight, GameArt.Grey);
					}
					GameArt.Label(new Rect(num, rect.y + 11.5f * p, num2 * 0.62f, 9f * p), journalEntry.Status ?? "", 3.9f, TextAnchor.MiddleLeft, journalEntry.Ready ? new Color(1f, 0.92f, 0.45f) : GameArt.Grey);
					if (journalEntry.HasWay)
					{
						bool flag3 = IsMarked(journalEntry.Way);
						GameArt.Label(new Rect(num + num2 * 0.6f, rect.y + 11.5f * p, num2 * 0.4f, 9f * p), Service.Heading(me, journalEntry.Way) + (flag3 ? "  ·  marked" : ""), 3.8f, TextAnchor.MiddleRight, flag3 ? Pings.ColorOf(PingKind.Go) : GameArt.Grey);
					}
				}
				PadNav.Spot(rect);
				if (GUI.Button(rect, GUIContent.none, GUIStyle.none) && _listMouseIn)
				{
					Sounds.PlayUI("event:/SFX/Ui/Click");
					Journal.PickedEntry = journalEntry.Key;
					Journal.PickedGiver = -1L;
					Journal.ConfirmGiver = -1L;
					_confirmEntry = null;
				}
			}
		}

		private void DrawEntryDetail(Rect box, JournalEntry e, Vector2 me)
		{
			float p = GameArt.P;
			GameArt.Label(new Rect(box.x + 6f * p, box.y + 4f * p, box.width - 12f * p, box.height - 24f * p), e.Promise ?? e.Status ?? "", 4.3f, TextAnchor.UpperLeft, Color.white, wrap: true);
			float num = Mathf.Round(13f * p);
			float width = Mathf.Round((box.width - 18f * p) / 2f);
			Rect r = new Rect(box.x + 6f * p, box.yMax - num - 4f * p, width, num);
			Rect r2 = new Rect(r.xMax + 6f * p, r.y, width, num);
			bool flag = e.HasWay && IsMarked(e.Way);
			if (GameButton(r, (!e.HasWay) ? "No way to show" : (flag ? "Take the mark off" : "Mark the way"), red: false, e.HasWay, null, 4.4f))
			{
				MarkNews(e.Way, e.Title, me);
			}
			bool flag2 = e.GiveUp != null;
			bool flag3 = flag2 && _confirmEntry == e.Key && Time.unscaledTime < _confirmEntryUntil;
			if (GameButton(r2, flag3 ? "Really give it up?" : "Give it up", flag3, flag2, null, 4.4f))
			{
				if (flag3)
				{
					_confirmEntry = null;
					e.GiveUp();
				}
				else
				{
					_confirmEntry = e.Key;
					_confirmEntryUntil = Time.unscaledTime + 4f;
				}
			}
		}

		private void StandingRow(Rect row, Kingdom kg, string name, int points, RepLevel level, bool rules)
		{
			float p = GameArt.P;
			GameArt.Sliced(row, GameArt.Inner, new Color(0.9f, 0.9f, 0.9f, 1f));
			bool civ = false;
			try
			{
				civ = kg?.isCiv() ?? false;
			}
			catch
			{
			}
			float num = row.x + 5f * p;
			if (DrawBanner(new Rect(row.x + 3f * p, row.y + 3f * p, 13f * p, 16f * p), kg, civ, 1f))
			{
				num = row.x + 19f * p;
			}
			float num2 = row.xMax - num - 4f * p;
			GameArt.Label(new Rect(num, row.y + 1.5f * p, num2 * 0.62f, 9f * p), name, 4.5f, TextAnchor.MiddleLeft, Color.white);
			string text = (rules ? "Yours" : Rep.Name(level));
			GameArt.Label(new Rect(num + num2 * 0.62f, row.y + 1.5f * p, num2 * 0.38f, 9f * p), text, 4.4f, TextAnchor.MiddleRight, rules ? RepHud.Gold : RepHud.LevelColor(level));
			Rep.Span(level, out var _, out var hi);
			float frac = (rules ? 1f : Rep.Progress(points));
			string text2 = (rules ? "" : ((level == RepLevel.Champion) ? (points + " / " + 5000) : (points + " / " + hi + " to " + Rep.Name(Rep.Next(level)))));
			GameArt.Bar(new Rect(num, row.y + 12.5f * p, num2 * 0.58f, 6f * p), frac, rules ? RepHud.Gold : RepHud.BarColor(level));
			GameArt.Label(new Rect(num + num2 * 0.6f, row.y + 11f * p, num2 * 0.4f, 9f * p), text2, 3.8f, TextAnchor.MiddleRight, GameArt.Grey);
		}
	}
}
