using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using FirstPerson.Core;
using FirstPerson.Towns;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FirstPerson.Net
{
	internal static class WorldSync
	{
		private sealed class Tracked
		{
			public Actor A;

			public long Id;

			public int Seen;

			public uint Hash;

			public int Version = 1;

			public float HashedAt = -999f;

			public bool Near;

			public double LastAttack;

			public int AttackedFrame = -1;
		}

		private sealed class Known
		{
			public readonly Dictionary<long, int> Versions = new Dictionary<long, int>();

			public readonly Dictionary<long, int> BVersions = new Dictionary<long, int>();

			public float MotionAt;

			public int Sends;

			public int NearSentFrame;

			public int MidSentFrame;

			public int FarCursor;

			public Vector2 Focus;

			public bool HasFocus;
		}

		private struct Sample
		{
			public float T;

			public Vector2 P;

			public float H;

			public byte Flags;

			public int Health;
		}

		private sealed class Track
		{
			private readonly Sample[] _s = new Sample[8];

			public int Count;

			private int _head;

			public bool AttackPending;

			public Sample Last => _s[_head];

			public void Add(Sample x)
			{
				if (Count <= 0 || !(x.T <= Last.T))
				{
					_head = (_head + 1) % _s.Length;
					_s[_head] = x;
					if (Count < _s.Length)
					{
						Count++;
					}
				}
			}

			public bool At(float t, out Vector2 p, out float h, out Sample nearest)
			{
				nearest = Last;
				p = Last.P;
				h = Last.H;
				if (Count == 0)
				{
					return false;
				}
				for (int i = 0; i < Count - 1; i++)
				{
					Sample sample = _s[(_head - i + _s.Length) % _s.Length];
					Sample sample2 = _s[(_head - i - 1 + _s.Length) % _s.Length];
					if (t >= sample2.T && t <= sample.T)
					{
						float num = ((sample.T > sample2.T) ? ((t - sample2.T) / (sample.T - sample2.T)) : 1f);
						if ((sample.P - sample2.P).sqrMagnitude > 64f)
						{
							num = 1f;
						}
						p = Vector2.Lerp(sample2.P, sample.P, num);
						h = Mathf.Lerp(sample2.H, sample.H, num);
						nearest = ((num < 0.5f) ? sample2 : sample);
						return true;
					}
				}
				if (Count > 0 && t < _s[(_head - Count + 1 + _s.Length) % _s.Length].T)
				{
					Sample sample3 = _s[(_head - Count + 1 + _s.Length) % _s.Length];
					p = sample3.P;
					h = sample3.H;
					nearest = sample3;
				}
				return true;
			}
		}

		private sealed class Remote
		{
			public NetPlayer P;

			public Actor A;

			public long Id;

			public readonly Track Path = new Track();

			public float Offset = float.NaN;

			public float OffsetAt;

			public float OffsetWindow = float.PositiveInfinity;

			public float KeptAt;

			public long TalkingTo;

			public float TalkSeenAt;

			public long LastCity;

			public string LastAsset;
		}

		private sealed class TrackedB
		{
			public Building B;

			public long Id;

			public int Seen;

			public uint Hash;

			public int Version = 1;

			public float HashedAt = -999f;
		}

		private sealed class WaitingB
		{
			public string Json;

			public float Since;
		}

		private sealed class Budget
		{
			public float Left;

			public float At;
		}

		private sealed class RemoteGuard
		{
			public NetPlayer P;

			public Actor A;

			public int Flags;

			public float At;

			public float ParryUntil;

			public float RiposteUntil;

			public float Chip = 0.25f;

			public Vector2 Facing = Vector2.right;
		}

		private struct LocalFx
		{
			public string Id;

			public Vector2 P;

			public float At;
		}

		private sealed class JoinerMover
		{
			public BaseEffect E;

			public Vector2 P;

			public float Z;

			public float Seen;
		}

		private sealed class MetaKind
		{
			public byte Kind;

			public string Field;

			public object Manager;

			public Type DataType;

			public MethodInfo LoadObject;

			public MethodInfo RemoveObject;

			public MethodInfo Get;

			public FieldInfo DataField;

			public MethodInfo Save;

			public MethodInfo IsAlive;

			public readonly Dictionary<long, TrackedM> Tracked = new Dictionary<long, TrackedM>();
		}

		private sealed class TrackedM
		{
			public object O;

			public long Id;

			public int Seen;

			public uint Hash;

			public float HashedAt;
		}

		private sealed class MetaEntry
		{
			public MetaKind K;

			public TrackedM T;
		}

		private sealed class Baseline
		{
			public string S;

			public JObject J;
		}

		private sealed class Pending
		{
			public int Seq;

			public float At;

			public List<KeyValuePair<string, JToken>> Ops;

			public List<byte> Kinds;
		}

		private sealed class PowerHand
		{
			public NetPlayer Of;

			public float ClickTimer;

			public bool UsedPower;

			public Kingdom WhisperA;

			public Kingdom WhisperB;

			public Kingdom UnityA;

			public Kingdom UnityB;

			public float LastAt = -99f;

			public string LastId;
		}

		private sealed class Wrapped
		{
			public GodPower Power;

			public PowerActionWithID Special;

			public PowerActionWithID Brush;

			public PowerActionWithID Click;

			public PowerAction PowerBrush;

			public PowerAction PowerClick;
		}

		private struct HeldTile
		{
			public short Edit;

			public short Level;

			public byte Raw;

			public int Main;

			public int Top;
		}

		private sealed class Respawn
		{
			public NetPlayer P;

			public float At;

			public long City;

			public string Asset;
		}

		private sealed class Transfer
		{
			public NetPlayer Player;

			public byte[] Bytes;

			public int Next;

			public int Pieces;
		}

		private static readonly Dictionary<long, Tracked> _tracked = new Dictionary<long, Tracked>();

		private static readonly List<Tracked> _trackedList = new List<Tracked>();

		private static readonly Dictionary<int, Known> _known = new Dictionary<int, Known>();

		private static int _scanStamp;

		private static int _hashCursor;

		private static readonly JsonSerializerSettings SaveJson = new JsonSerializerSettings
		{
			DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate
		};

		private const byte DataActor = 1;

		private static int _jsonErrors;

		private const float NearRange = 48f;

		private const float MidRange = 120f;

		private const int FarPerSecond = 400;

		private static float _nearAt;

		private static readonly List<Tracked> _send = new List<Tracked>();

		private static readonly List<int> _sendSince = new List<int>();

		private const byte MMoving = 1;

		private const byte MFlip = 2;

		private const byte MAttacked = 4;

		private const byte MHeight = 8;

		private const byte MInside = 16;

		private const byte MBoat = 32;

		private const byte MBurning = 64;

		private const byte MGod = 128;

		private static FieldInfo[] _dataFields;

		private static int _applyErrors;

		private static readonly Dictionary<Type, FieldInfo[]> _fieldsOf = new Dictionary<Type, FieldInfo[]>();

		private static readonly Dictionary<long, Track> _motion = new Dictionary<long, Track>();

		private static float _hostClockOffset = float.NaN;

		private static float _offsetSeenAt;

		private static float _offsetWindow = float.PositiveInfinity;

		private const float InterpDelay = 0.12f;

		private const byte ActStrike = 1;

		private const byte ActShotHit = 2;

		private const byte ActTalk = 3;

		private const byte ActIgnite = 4;

		private const byte ActAbility = 5;

		private const byte ActRushHit = 6;

		private const byte IgniteBuilding = 1;

		private const byte IgniteGround = 2;

		private static long _talkSent;

		private static float _talkSentAt;

		internal static int AbilitiesSent;

		internal static int AbilitiesCast;

		private const byte EventKill = 1;

		private static FieldInfo _killedByYou;

		private static int _blowsLogged;

		private static FieldInfo _blowScale;

		private static bool _blowScaleLooked;

		private static readonly Dictionary<int, Remote> _remotes = new Dictionary<int, Remote>();

		private static WorldAction _possessedAction;

		private static bool _statusHooked;

		private static long _myBody;

		private static long _askedBody;

		private static bool _takeGiven;

		private static float _givenAt;

		private static float _motionSentAt;

		private static float _bodyAskedAt;

		private static bool _attackedSinceSent;

		private static readonly Dictionary<long, TrackedB> _trackedB = new Dictionary<long, TrackedB>();

		private static readonly List<TrackedB> _trackedBList = new List<TrackedB>();

		private static int _scanBStamp;

		private static int _hashBCursor;

		private static float _scannedBAt = -1f;

		private const byte DataBuilding = 2;

		private static readonly HashSet<long> _burningB = new HashSet<long>();

		private static readonly HashSet<long> _burningNow = new HashSet<long>();

		private static float _burningAt;

		private static FieldInfo[] _bFields;

		private static readonly Dictionary<long, WaitingB> _waitLand = new Dictionary<long, WaitingB>();

		private const float LandWait = 6f;

		private static Func<BuildingData, Building> _baseLoad;

		private static object _baseLoadFor;

		private static int _spotsKept;

		private static float _clockSentAt;

		private static double _lastHostTime;

		private static float _lastHostReal;

		private static float _hostRate;

		private static readonly List<KeyValuePair<int, Vector2>> _gods = new List<KeyValuePair<int, Vector2>>();

		private static readonly List<KeyValuePair<int, Vector2>> _joinerGods = new List<KeyValuePair<int, Vector2>>();

		private static double _clock;

		private static float _clockRate;

		private static bool _clockSet;

		private static bool _hostPaused;

		private static bool _seaSet;

		private static string _hostSpeed;

		private static float _speedToldAt = -99f;

		private static float _hostHour;

		private static int _hostDay;

		private static bool _hourFresh;

		private static string _speedSetTo;

		private static string _ownSpeed;

		private const byte ActPunch = 12;

		private const byte ActGuard = 13;

		private const byte EventGuard = 2;

		private const byte EventCatch = 3;

		private const byte EventLight = 4;

		private const byte BudgetStrike = 1;

		private const byte BudgetShot = 2;

		private const byte BudgetCatch = 4;

		private const byte GuardUp = 1;

		private const byte GuardParry = 2;

		private const float ShotReach = 60f;

		private const float StrikeLag = 2.5f;

		private static readonly Dictionary<long, Budget> _budgets = new Dictionary<long, Budget>();

		private static readonly Dictionary<int, RemoteGuard> _guards = new Dictionary<int, RemoteGuard>();

		private static readonly NetWriter GW = new NetWriter(64);

		private static int _guardSentFlags;

		private static float _guardSentAt = -9f;

		private static Vector2 _guardSentFacing;

		internal static int ActsRefused;

		internal static int GuardsSent;

		internal static int GuardsIn;

		internal static int GuardsStopped;

		internal static int GuardsHeard;

		internal static string LastActRefusal;

		private const int FireL = 1;

		private const int FireR = 2;

		private const int StrikeL = 4;

		private const int StrikeR = 8;

		private const int FlatL = 16;

		private const int FlatR = 32;

		private static readonly CrabFire.Shot _myShot = new CrabFire.Shot();

		private static float _crabSentAt;

		private static bool _crabWasFiring;

		private static readonly List<KeyValuePair<Actor, int>> _hatched = new List<KeyValuePair<Actor, int>>();

		private static bool _hatchHooked;

		private static readonly CrabFire.Shot _readShot = new CrabFire.Shot();

		private static NetSession _s;

		private static readonly NetWriter W = new NetWriter(65536);

		private static int _frame;

		private static object _hostStats;

		private static bool _crabGuardTried;

		private static readonly List<KeyValuePair<int, byte[]>> _buffered = new List<KeyValuePair<int, byte[]>>();

		private const byte FxPlain = 0;

		private const byte FxMeteor = 1;

		private const byte FxNuke = 2;

		private const byte FxAntimatter = 3;

		private const byte FxFireworks = 4;

		private const byte MoverCloud = 0;

		private const byte MoverTornado = 1;

		private const byte MoverSanta = 2;

		private const byte MoverBoulder = 3;

		private const float FxNear = 100f;

		private const int FxMaxPerFrame = 80;

		private const int DropsMaxPerFrame = 120;

		private static readonly HashSet<string> FxSkip = new HashSet<string>
		{
			"fx_tile_effect", "fx_change_happiness_positive", "fx_change_happiness_negative", "fx_hmm", "fx_money_got_loot", "fx_money_got_money", "fx_money_paid_tax", "fx_money_paid_tribute", "fx_money_spend_money", "fx_conversion_religion",
			"fx_conversion_culture", "fx_conversion_language", "fx_experience_gain", "fx_plot_progress", "fx_infinity_coin", "fx_new_border", "fx_zone_highlight", "fx_bad_place", "fx_debug_tile", "fx_move",
			"fx_status_particle"
		};

		private static HashSet<BaseEffect> _fxWas = new HashSet<BaseEffect>();

		private static HashSet<BaseEffect> _fxNow = new HashSet<BaseEffect>();

		private static HashSet<Drop> _dropsWas = new HashSet<Drop>();

		private static HashSet<Drop> _dropsNow = new HashSet<Drop>();

		private static readonly List<BaseEffect> _fxNew = new List<BaseEffect>();

		private static readonly List<BaseEffect> _fxFor = new List<BaseEffect>();

		private static readonly List<Drop> _dropsNew = new List<Drop>();

		private static readonly Dictionary<BaseEffect, uint> _moverKeys = new Dictionary<BaseEffect, uint>();

		private static readonly List<BaseEffect> _movers = new List<BaseEffect>();

		private static uint _nextMoverKey = 1u;

		private static float _moversSentAt;

		private static bool _fxPrimed;

		private static readonly List<LocalFx> _localFx = new List<LocalFx>();

		private static readonly Dictionary<uint, JoinerMover> _joinerMovers = new Dictionary<uint, JoinerMover>();

		private static readonly List<uint> _moversGone = new List<uint>();

		private static Detour.Handle _dropLandHook;

		private static Detour.Handle _meteorHook;

		private const byte ActLight = 7;

		private const byte LightPlace = 1;

		private const byte LightTake = 2;

		private static int _lightsSent = -1;

		private static float _lightsSentAt;

		internal static int LightsSent;

		internal static int LightsPlacedForJoiners;

		internal static int LightListsIn;

		internal static int LightsRefused;

		internal static int LightsRefunded;

		private static readonly string[] MetaFields = new string[13]
		{
			"subspecies", "cultures", "languages", "religions", "families", "clans", "kingdoms", "cities", "alliances", "wars",
			"plots", "armies", "books"
		};

		private static List<MetaKind> _meta;

		private static float _metaAt = -1f;

		private static int _metaStamp;

		private static readonly List<MetaEntry> _metaRound = new List<MetaEntry>();

		private static int _metaCursor;

		private static readonly Dictionary<long, long> _townRealm = new Dictionary<long, long>();

		private static float _townRealmsAt;

		private static readonly Dictionary<long, long> _hostRealm = new Dictionary<long, long>();

		private const byte PatchActor = 1;

		private const byte PatchBuilding = 2;

		private const byte PatchMeta = 3;

		private const byte OpSet = 0;

		private const byte OpAdd = 1;

		private const byte OpRemove = 2;

		private const byte PatchFields = 0;

		private const byte PatchCreate = 1;

		private const byte PatchKill = 2;

		private static readonly Dictionary<long, Baseline> _baseA = new Dictionary<long, Baseline>();

		private static readonly Dictionary<long, Baseline> _baseB = new Dictionary<long, Baseline>();

		private static readonly Dictionary<long, Baseline> _baseM = new Dictionary<long, Baseline>();

		private static readonly HashSet<long> _hostActors = new HashSet<long>();

		private static readonly HashSet<long> _hostBuildings = new HashSet<long>();

		private static float _patchAt;

		private static float _createAt;

		private static long _myRange;

		private static readonly Dictionary<long, List<Pending>> _unacked = new Dictionary<long, List<Pending>>();

		private static int _patchSeq;

		private static readonly HashSet<string> Additive = new HashSet<string> { "health", "nutrition", "stamina", "mana", "happiness", "money", "loot", "experience", "renown", "kills" };

		private static readonly List<KeyValuePair<string, JToken>> _ops = new List<KeyValuePair<string, JToken>>();

		private static readonly List<byte> _opKinds = new List<byte>();

		private static int _patchesLogged;

		private static readonly HashSet<long> _sentMeta = new HashSet<long>();

		private const float PatchReach = 15f;

		internal static int PatchesRefused;

		private const byte ActPlot = 14;

		private const byte EventPlot = 5;

		private const byte PlotMark = 1;

		private const byte PlotHelp = 2;

		private const byte BudgetPlotHelp = 6;

		private static float _plotsAt;

		internal static int PlotsAsked;

		internal static int PlotsRaisedForJoiners;

		internal static int PlotAnswers;

		internal static int PlotHelpTaken;

		internal static string LastPlotAnswer = "";

		private static readonly Dictionary<int, PowerHand> _hands = new Dictionary<int, PowerHand>();

		private const float StrokeGap = 0.4f;

		private static readonly List<Wrapped> _wrapped = new List<Wrapped>();

		private static int _sentFrame;

		private static readonly HashSet<string> _sentThisFrame = new HashSet<string>();

		private static float _noPowersToldAt = -99f;

		private const byte ActDig = 8;

		private const byte ActDigTo = 9;

		private const byte ReliefWhole = 0;

		private const byte ReliefTiles = 1;

		private const int MostTiles = 3000;

		private static int _reliefSent = int.MinValue;

		private static float _reliefSentAt;

		private static short[] _sentEdit = new short[0];

		private static byte[] _sentRaw = new byte[0];

		internal static int DigsSent;

		internal static int DigsForGuests;

		internal static int ReliefIn;

		private static short[] _sentLevel = new short[0];

		private static ushort[] _sentMain = new ushort[0];

		private static ushort[] _sentTop = new ushort[0];

		private static readonly Dictionary<int, HeldTile> _held = new Dictionary<int, HeldTile>();

		private const float ResyncEvery = 15f;

		private static bool _resyncing;

		private static float _resyncAskedAt = -99f;

		private static readonly Dictionary<int, float> _resyncAt = new Dictionary<int, float>();

		private static readonly Dictionary<long, KeyValuePair<long, float>> _rulerWait = new Dictionary<long, KeyValuePair<long, float>>();

		private static readonly List<long> _rulerKeys = new List<long>();

		private static float _rulersAt;

		private static readonly HashSet<long> _shotsSeen = new HashSet<long>();

		private static readonly HashSet<long> _shotsNow = new HashSet<long>();

		private static float _shotsPrunedAt;

		private static Detour.Handle _landHook;

		private static Detour.Handle _hitHook;

		private const byte PossessLetGo = 0;

		private const byte PossessTake = 1;

		private const byte PossessBorn = 2;

		private const byte PossessLast = 3;

		private const string BodiesKey = "mp_bodies";

		private static readonly Dictionary<string, long> _lastBody = new Dictionary<string, long>();

		private static readonly List<Respawn> _respawns = new List<Respawn>();

		private static long[] _tileSig;

		private static int _tileCursor;

		private const int TilesPerFrame = 24000;

		private static readonly List<WorldTile> _changedTiles = new List<WorldTile>();

		private const byte ActTown = 15;

		private const byte EventTown = 6;

		private const byte TownFound = 1;

		private const byte TownPlace = 2;

		private const byte TownRemove = 3;

		private const byte TownKnock = 4;

		private const byte TownNext = 5;

		private const byte TownBlow = 6;

		private const byte TownStreet = 7;

		private const byte TownWall = 8;

		private const byte TownUnwall = 9;

		private const byte BudgetTownBlow = 7;

		private const byte BudgetTownPlan = 8;

		private const int MostTownTiles = 60;

		internal static int TownsFoundedForJoiners;

		internal static int TownPlansForJoiners;

		internal static int TownBlowsTaken;

		internal static int TownMovesForJoiners;

		internal static int TownAnswers;

		internal static string LastTownAnswer = "";

		private static readonly Dictionary<int, float> _needsToldAt = new Dictionary<int, float>();

		private static readonly List<Transfer> _transfers = new List<Transfer>();

		private static byte[] _incoming;

		private static int _incomingPieces;

		private static int _gotPieces;

		private static bool _loadingWorld;

		private static bool _worldIn;

		private static bool _autosavesBefore = true;

		private static bool _savedBefore;

		private static float _loadStarted;

		private static object _joinerStats;

		private static uint[] _crcTable;

		private static float _citiesRushedAt = -99f;

		public static long MyBody => _myBody;

		public static float SeaOffset { get; private set; }

		public static bool HostPaused
		{
			get
			{
				if (NetSession.Joined)
				{
					return _hostPaused;
				}
				return false;
			}
		}

		private static bool Loading
		{
			get
			{
				try
				{
					return !Config.game_loaded || World.world == null || SmoothLoader.isLoading();
				}
				catch
				{
					return true;
				}
			}
		}

		private static float Now => Time.realtimeSinceStartup;

		internal static bool GuestDigHeld
		{
			get
			{
				if (Mirroring)
				{
					if (_s?.Rules != null)
					{
						return !_s.Rules.GuestsDig;
					}
					return true;
				}
				return false;
			}
		}

		public static bool Resyncing
		{
			get
			{
				if (_resyncing)
				{
					if (_incoming == null && !_loadingWorld)
					{
						return Now - _resyncAskedAt < 30f;
					}
					return true;
				}
				return false;
			}
		}

		public static bool CanResync
		{
			get
			{
				if (Mirroring && !Resyncing && !_loadingWorld)
				{
					return Now - _resyncAskedAt > 15f;
				}
				return false;
			}
		}

		internal static Actor MyBodyActor
		{
			get
			{
				if (_myBody == 0L)
				{
					return null;
				}
				try
				{
					Actor actor = World.world.units.get(_myBody);
					return (actor != null && actor.isAlive()) ? actor : null;
				}
				catch
				{
					return null;
				}
			}
		}

		private static string BeforeFile => Path.Combine(Log.Folder, "before_multiplayer.wbox");

		public static bool Mirroring
		{
			get
			{
				if (_worldIn)
				{
					return NetSession.Joined;
				}
				return false;
			}
		}

		public static bool LoadingHostWorld => _loadingWorld;

		private static void ResetActors()
		{
			_tracked.Clear();
			_trackedList.Clear();
			_known.Clear();
			_motion.Clear();
			_hostClockOffset = float.NaN;
			_offsetWindow = float.PositiveInfinity;
			_hashCursor = 0;
			ResetBuildings();
		}

		private static void ForgetPlayer(NetPlayer p)
		{
			_known.Remove(p.Id);
		}

		private static void MarkAllKnown(NetPlayer p)
		{
			Known known = new Known();
			foreach (Tracked tracked in _trackedList)
			{
				known.Versions[tracked.Id] = tracked.Version;
			}
			MarkBuildingsKnown(known);
			_known[p.Id] = known;
		}

		private static void HostScanActors(bool initial)
		{
			int num = ++_scanStamp;
			List<Actor> simpleList;
			try
			{
				simpleList = World.world.units.getSimpleList();
			}
			catch
			{
				return;
			}
			List<Tracked> list = null;
			foreach (Actor item in simpleList)
			{
				if (item == null || item.data == null || !item.isAlive())
				{
					continue;
				}
				long id = item.data.id;
				if (_tracked.TryGetValue(id, out var value))
				{
					value.A = item;
					value.Seen = num;
					continue;
				}
				value = new Tracked
				{
					A = item,
					Id = id,
					Seen = num,
					LastAttack = item.last_attack_timestamp
				};
				_tracked[id] = value;
				_trackedList.Add(value);
				if (!initial)
				{
					(list ?? (list = new List<Tracked>())).Add(value);
				}
			}
			for (int num2 = _trackedList.Count - 1; num2 >= 0; num2--)
			{
				Tracked tracked = _trackedList[num2];
				if (tracked.Seen != num)
				{
					_trackedList.RemoveAt(num2);
					_tracked.Remove(tracked.Id);
					bool died = false;
					try
					{
						died = tracked.A != null && tracked.A.data != null && tracked.A.data.id == tracked.Id && !tracked.A.isAlive();
					}
					catch
					{
					}
					SendDespawn(tracked, died);
				}
			}
			if (list == null)
			{
				return;
			}
			foreach (Tracked item2 in list)
			{
				SendSpawn(item2);
			}
		}

		private static void SendSpawn(Tracked t)
		{
			if (_known.Count == 0)
			{
				return;
			}
			List<string> items;
			string text = ActorJson(t.A, out items);
			if (text == null)
			{
				return;
			}
			t.Hash = Fnv(text);
			t.HashedAt = Now;
			W.Reset();
			W.VarU(32uL);
			W.VarU((ulong)t.Id);
			W.Pos(t.A.current_position.x);
			W.Pos(t.A.current_position.y);
			W.Str(text);
			W.VarU((ulong)items.Count);
			foreach (string item in items)
			{
				W.Str(item);
			}
			foreach (KeyValuePair<int, Known> item2 in _known)
			{
				NetPlayer netPlayer = PlayerById(item2.Key);
				if (netPlayer?.Link != null)
				{
					item2.Value.Versions[t.Id] = t.Version;
					netPlayer.Link.Send(W, Delivery.Reliable);
				}
			}
		}

		private static void SendDespawn(Tracked t, bool died)
		{
			if (_known.Count == 0)
			{
				return;
			}
			W.Reset();
			W.VarU(33uL);
			W.VarU((ulong)t.Id);
			W.Bool(died);
			foreach (KeyValuePair<int, Known> item in _known)
			{
				if (item.Value.Versions.Remove(t.Id))
				{
					PlayerById(item.Key)?.Link?.Send(W, Delivery.Reliable);
				}
			}
		}

		private static void HostData()
		{
			if (_known.Count == 0)
			{
				return;
			}
			float now = Now;
			float num = now;
			HostBuildingData(now + 0.0005f);
			int num2 = 0;
			int count = _trackedList.Count;
			for (int i = 0; i < count; i++)
			{
				if (!(Now - now < 0.002f))
				{
					break;
				}
				_hashCursor = (_hashCursor + 1) % count;
				Tracked tracked = _trackedList[_hashCursor];
				float num3 = (tracked.Near ? 0.5f : 8f);
				if (num - tracked.HashedAt < num3)
				{
					continue;
				}
				num2++;
				tracked.HashedAt = num;
				List<string> items;
				string text = ActorJson(tracked.A, out items);
				if (text == null)
				{
					continue;
				}
				uint num4 = Fnv(text);
				if (num4 == tracked.Hash)
				{
					continue;
				}
				tracked.Hash = num4;
				tracked.Version++;
				W.Reset();
				W.VarU(34uL);
				W.U8(1);
				W.VarU((ulong)tracked.Id);
				W.Str(text);
				W.VarU((ulong)items.Count);
				foreach (string item in items)
				{
					W.Str(item);
				}
				foreach (KeyValuePair<int, Known> item2 in _known)
				{
					if (item2.Value.Versions.TryGetValue(tracked.Id, out var value) && value < tracked.Version)
					{
						item2.Value.Versions[tracked.Id] = tracked.Version;
						PlayerById(item2.Key)?.Link?.Send(W, Delivery.Reliable);
					}
				}
			}
		}

		private static string ActorJson(Actor a, out List<string> items)
		{
			items = new List<string>();
			try
			{
				if (a == null || a.data == null)
				{
					return null;
				}
				try
				{
					a.prepareForSave();
				}
				catch
				{
				}
				ActorData data = a.data;
				int x = data.x;
				int y = data.y;
				data.x = 0;
				data.y = 0;
				string result;
				try
				{
					result = JsonConvert.SerializeObject(data, SaveJson);
				}
				finally
				{
					data.x = x;
					data.y = y;
				}
				if (data.saved_items != null)
				{
					foreach (long saved_item in data.saved_items)
					{
						Item item = World.world.items.get(saved_item);
						if (item?.data != null)
						{
							items.Add(JsonConvert.SerializeObject(item.data, SaveJson));
						}
					}
				}
				return result;
			}
			catch (Exception ex)
			{
				if (_jsonErrors++ < 5)
				{
					Log.Error("NET creature data: " + ex.Message);
				}
				return null;
			}
		}

		private static uint Fnv(string s)
		{
			uint num = 2166136261u;
			for (int i = 0; i < s.Length; i++)
			{
				num ^= s[i];
				num *= 16777619;
			}
			return num;
		}

		private static void HostMotion()
		{
			float now = Now;
			bool flag = now - _nearAt >= 0.2f;
			if (flag)
			{
				_nearAt = now;
				foreach (Tracked tracked in _trackedList)
				{
					tracked.Near = false;
				}
			}
			foreach (NetPlayer value2 in _s.Players.Values)
			{
				if (value2.Link == null || !value2.Ready || !_known.TryGetValue(value2.Id, out var value))
				{
					continue;
				}
				Vector2 vector = FocusOf(value2, value);
				if (flag)
				{
					foreach (Tracked tracked2 in _trackedList)
					{
						if (tracked2.A != null && (tracked2.A.current_position - vector).sqrMagnitude < 2304f)
						{
							tracked2.Near = true;
						}
					}
				}
				if (!(now - value.MotionAt < 1f / 15f))
				{
					bool midTurn = value.Sends++ % 3 == 0;
					value.MotionAt = now;
					SendMotion(value2, value, vector, midTurn);
				}
			}
			foreach (Tracked tracked3 in _trackedList)
			{
				if (tracked3.A != null && tracked3.A.last_attack_timestamp != tracked3.LastAttack)
				{
					tracked3.LastAttack = tracked3.A.last_attack_timestamp;
					tracked3.AttackedFrame = _frame;
				}
			}
		}

		private static Vector2 FocusOf(NetPlayer p, Known k)
		{
			if (p.Body != 0L && _tracked.TryGetValue(p.Body, out var value) && value.A != null)
			{
				return value.A.current_position;
			}
			if (!k.HasFocus)
			{
				return new Vector2((float)MapBox.width * 0.5f, (float)MapBox.height * 0.5f);
			}
			return k.Focus;
		}

		private static bool Same(Tracked t)
		{
			if (t.A != null && t.A.data != null)
			{
				return t.A.data.id == t.Id;
			}
			return false;
		}

		private static void SendMotion(NetPlayer p, Known k, Vector2 focus, bool midTurn)
		{
			_send.Clear();
			_sendSince.Clear();
			int num = 26;
			int count = _trackedList.Count;
			foreach (Tracked tracked2 in _trackedList)
			{
				if (Same(tracked2) && k.Versions.ContainsKey(tracked2.Id))
				{
					float sqrMagnitude = (tracked2.A.current_position - focus).sqrMagnitude;
					bool flag = sqrMagnitude < 2304f;
					if (flag || (midTurn && sqrMagnitude < 14400f))
					{
						_send.Add(tracked2);
						_sendSince.Add(flag ? k.NearSentFrame : k.MidSentFrame);
					}
				}
			}
			for (int i = 0; i < num; i++)
			{
				if (count <= 0)
				{
					break;
				}
				k.FarCursor = (k.FarCursor + 1) % count;
				Tracked tracked = _trackedList[k.FarCursor];
				if (Same(tracked) && tracked.Id != p.Body && k.Versions.ContainsKey(tracked.Id) && !((tracked.A.current_position - focus).sqrMagnitude < 14400f))
				{
					_send.Add(tracked);
					_sendSince.Add(int.MaxValue);
				}
			}
			k.NearSentFrame = _frame;
			if (midTurn)
			{
				k.MidSentFrame = _frame;
			}
			int num2 = 0;
			while (num2 < _send.Count)
			{
				W.Reset();
				W.VarU(31uL);
				W.F32(Now);
				int length = W.Length;
				W.U16(0);
				int num3 = 0;
				while (num2 < _send.Count && W.Length < 1080)
				{
					WriteMotion(_send[num2], _sendSince[num2]);
					num2++;
					num3++;
				}
				W.Buffer[length] = (byte)num3;
				W.Buffer[length + 1] = (byte)(num3 >> 8);
				p.Link.Send(W, Delivery.Unreliable);
			}
		}

		private static void WriteMotion(Tracked t, int attackedSince)
		{
			Actor a = t.A;
			int num = 0;
			if (a.is_moving)
			{
				num |= 1;
			}
			if (a.flip)
			{
				num |= 2;
			}
			if (t.AttackedFrame >= attackedSince)
			{
				num |= 4;
			}
			if (Mathf.Abs(a.position_height) > 0.01f)
			{
				num |= 8;
			}
			if (a.is_inside_building)
			{
				num |= 0x10;
			}
			if (a.is_inside_boat)
			{
				num |= 0x20;
			}
			if (Torches.Burning(a))
			{
				num |= 0x40;
			}
			W.VarU((ulong)t.Id);
			W.U8(num);
			W.Pos(a.current_position.x);
			W.Pos(a.current_position.y);
			if ((num & 8) != 0)
			{
				W.I16((int)Mathf.Clamp(a.position_height * 64f, -32000f, 32000f));
			}
			int num2 = 0;
			try
			{
				num2 = a.data.health;
			}
			catch
			{
			}
			W.VarI(num2);
		}

		private static NetPlayer PlayerById(int id)
		{
			if (_s == null || !_s.Players.TryGetValue(id, out var value))
			{
				return null;
			}
			return value;
		}

		private static void JoinerIndexActors()
		{
			_motion.Clear();
		}

		private static void JoinerSpawn(NetReader r)
		{
			long num = (long)r.VarU();
			Vector2 current_position = new Vector2(r.Pos(), r.Pos());
			string text = r.Str();
			int num2 = r.Count(64);
			List<string> list = new List<string>(num2);
			for (int i = 0; i < num2; i++)
			{
				list.Add(r.Str());
			}
			_hostActors.Add(num);
			Actor actor = World.world.units.get(num);
			if (actor != null)
			{
				ApplyActorData(actor, text, list);
				return;
			}
			ActorData actorData = JsonConvert.DeserializeObject<ActorData>(text, SaveManager._settings);
			if (actorData != null)
			{
				if (_motion.TryGetValue(num, out var value) && value.Count > 0)
				{
					current_position = value.Last.P;
				}
				actorData.x = Mathf.Clamp((int)current_position.x, 0, MapBox.width - 1);
				actorData.y = Mathf.Clamp((int)current_position.y, 0, MapBox.height - 1);
				LoadItems(list);
				actor = World.world.units.loadObject(actorData);
				if (actor != null)
				{
					LinkLoaded(actor);
					actor.current_position = current_position;
					actor.dirty_current_tile = true;
				}
			}
		}

		private static void JoinerDespawn(NetReader r)
		{
			long num = (long)r.VarU();
			bool flag = r.Bool();
			_motion.Remove(num);
			_hostActors.Remove(num);
			_baseA.Remove(num);
			Actor actor = World.world.units.get(num);
			if (actor == null || !actor.isAlive())
			{
				return;
			}
			if (actor == ControllableUnit._unit_main)
			{
				LetGoForLoad();
			}
			try
			{
				if (flag)
				{
					actor.dieSimpleNone();
				}
				else
				{
					actor.removeByMetamorphosis();
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET removing creature " + num + ": " + ex.Message);
			}
		}

		private static void JoinerData(NetReader r)
		{
			switch ((byte)r.U8())
			{
			case 2:
				JoinerBuildingData((long)r.VarU(), r.Str());
				break;
			case 1:
			{
				long num = (long)r.VarU();
				string json = r.Str();
				int num2 = r.Count(64);
				List<string> list = new List<string>(num2);
				for (int i = 0; i < num2; i++)
				{
					list.Add(r.Str());
				}
				Actor actor = World.world.units.get(num);
				if (actor != null)
				{
					if (_baseA.ContainsKey(num))
					{
						PatchActorIfChanged(actor);
					}
					ApplyActorData(actor, WithUnacked(num, json), list);
					RebaseActor(actor);
				}
				break;
			}
			}
		}

		private static void ApplyActorData(Actor a, string json, List<string> items)
		{
			ActorData actorData = JsonConvert.DeserializeObject<ActorData>(json, SaveManager._settings);
			if (actorData == null || a.data == null)
			{
				return;
			}
			ActorData d = a.data;
			if (actorData.id != 0L && actorData.id != d.id)
			{
				return;
			}
			ActorAsset actorAsset = null;
			if (actorData.asset_id != d.asset_id && !string.IsNullOrEmpty(actorData.asset_id))
			{
				actorAsset = AssetManager.actor_library.get(actorData.asset_id);
				if (actorAsset == null)
				{
					return;
				}
			}
			long lover = d.lover;
			long homeBuildingID = d.homeBuildingID;
			long family = d.family;
			long army = d.army;
			long clan = d.clan;
			long plot = d.plot;
			long culture = d.culture;
			long language = d.language;
			long religion = d.religion;
			long subspecies = d.subspecies;
			UnitProfession profession = d.profession;
			string text = ((d.saved_traits == null) ? "" : string.Join(",", d.saved_traits));
			string text2 = ((d.saved_items == null) ? "" : string.Join(",", d.saved_items));
			long cityID = d.cityID;
			long civ_kingdom_id = d.civ_kingdom_id;
			if (_dataFields == null)
			{
				_dataFields = AllFields(typeof(ActorData));
			}
			int x = d.x;
			int y = d.y;
			FieldInfo[] dataFields = _dataFields;
			foreach (FieldInfo fieldInfo in dataFields)
			{
				fieldInfo.SetValue(d, fieldInfo.GetValue(actorData));
			}
			d.x = x;
			d.y = y;
			try
			{
				if (((d.saved_traits == null) ? "" : string.Join(",", d.saved_traits)) != text)
				{
					TraitTools.loadTraits(a, d.saved_traits);
				}
				if (((d.saved_items == null) ? "" : string.Join(",", d.saved_items)) != text2 && a.equipment != null)
				{
					LoadItems(items);
					TakeOffUnlisted(a, d.saved_items);
					a.equipment.load(d.saved_items, a);
				}
				if (d.cityID != cityID)
				{
					a.setCity(World.world.cities.get(d.cityID));
				}
				if (d.civ_kingdom_id != civ_kingdom_id)
				{
					Kingdom kingdom = World.world.kingdoms.get(d.civ_kingdom_id);
					if (kingdom != null)
					{
						a.setKingdom(kingdom);
					}
				}
				if (actorAsset != null)
				{
					a.setAsset(actorAsset);
					Relink(delegate
					{
						a.afterEvolutionEvents();
					});
				}
				if (d.subspecies != subspecies)
				{
					Subspecies o = World.world.subspecies.get(d.subspecies);
					if (o != null)
					{
						Relink(delegate
						{
							a.setSubspecies(o);
						});
					}
				}
				if (d.lover != lover)
				{
					Actor o2 = World.world.units.get(d.lover);
					if (o2 != null && o2.isAlive())
					{
						Relink(delegate
						{
							a.setLover(o2);
						});
					}
				}
				if (d.homeBuildingID != homeBuildingID)
				{
					Building o3 = World.world.buildings.get(d.homeBuildingID);
					if (Standing(o3))
					{
						Relink(delegate
						{
							a.setHomeBuilding(o3);
						});
					}
				}
				if (d.family != family)
				{
					Family o4 = World.world.families.get(d.family);
					if (o4 != null)
					{
						Relink(delegate
						{
							a.setFamily(o4);
						});
					}
				}
				if (d.army != army)
				{
					Army o5 = World.world.armies.get(d.army);
					if (o5 != null)
					{
						Relink(delegate
						{
							a.setArmy(o5);
						});
					}
				}
				if (d.clan != clan)
				{
					Clan o6 = World.world.clans.get(d.clan);
					if (o6 != null)
					{
						Relink(delegate
						{
							a.setClan(o6);
						});
					}
				}
				if (d.plot != plot)
				{
					Plot o7 = World.world.plots.get(d.plot);
					if (o7 != null)
					{
						Relink(delegate
						{
							a.setPlot(o7);
						});
					}
				}
				if (d.culture != culture)
				{
					Culture o8 = World.world.cultures.get(d.culture);
					if (o8 != null)
					{
						Relink(delegate
						{
							a.setCulture(o8);
						});
					}
				}
				if (d.language != language)
				{
					Language o9 = World.world.languages.get(d.language);
					if (o9 != null)
					{
						Relink(delegate
						{
							a.setLanguage(o9);
						});
					}
				}
				if (d.religion != religion)
				{
					Religion o10 = World.world.religions.get(d.religion);
					if (o10 != null)
					{
						Relink(delegate
						{
							a.setReligion(o10);
						});
					}
				}
				if (d.profession != profession)
				{
					Relink(delegate
					{
						a.setProfession(d.profession, pCancelBeh: false);
					});
				}
				a.setStatsDirty();
			}
			catch (Exception ex)
			{
				if (_applyErrors++ < 10)
				{
					Log.Error("NET creature " + d.id + " data: " + ex.Message);
				}
			}
		}

		private static void TakeOffUnlisted(Actor a, List<long> keep)
		{
			foreach (ActorEquipmentSlot item2 in new List<ActorEquipmentSlot>(a.equipment))
			{
				Item item = item2.getItem();
				if (item?.data != null && (keep == null || !keep.Contains(item.data.id)))
				{
					item2.takeAwayItem();
				}
			}
		}

		private static void LinkLoaded(Actor a)
		{
			ActorData actorData = a?.data;
			if (actorData == null)
			{
				return;
			}
			if (a.city == null && actorData.cityID != 0L)
			{
				City o = World.world.cities.get(actorData.cityID);
				if (o != null)
				{
					Relink(delegate
					{
						a.setCity(o);
					});
				}
			}
			if ((a.kingdom == null || a.kingdom.data == null || a.kingdom.data.id != actorData.civ_kingdom_id) && actorData.civ_kingdom_id != 0L)
			{
				Kingdom o2 = World.world.kingdoms.get(actorData.civ_kingdom_id);
				if (o2 != null)
				{
					Relink(delegate
					{
						a.setKingdom(o2);
					});
				}
			}
			if (a.subspecies == null && actorData.subspecies != 0L)
			{
				Subspecies o3 = World.world.subspecies.get(actorData.subspecies);
				if (o3 != null)
				{
					Relink(delegate
					{
						a.setSubspecies(o3);
					});
				}
			}
			if (a.family == null && actorData.family != 0L)
			{
				Family o4 = World.world.families.get(actorData.family);
				if (o4 != null)
				{
					Relink(delegate
					{
						a.setFamily(o4);
					});
				}
			}
			if (a.army == null && actorData.army != 0L)
			{
				Army o5 = World.world.armies.get(actorData.army);
				if (o5 != null)
				{
					Relink(delegate
					{
						a.setArmy(o5);
					});
				}
			}
			if (a.clan == null && actorData.clan != 0L)
			{
				Clan o6 = World.world.clans.get(actorData.clan);
				if (o6 != null)
				{
					Relink(delegate
					{
						a.setClan(o6);
					});
				}
			}
			if (a.plot == null && actorData.plot != 0L)
			{
				Plot o7 = World.world.plots.get(actorData.plot);
				if (o7 != null)
				{
					Relink(delegate
					{
						a.setPlot(o7);
					});
				}
			}
			if (a.culture == null && actorData.culture != 0L)
			{
				Culture o8 = World.world.cultures.get(actorData.culture);
				if (o8 != null)
				{
					Relink(delegate
					{
						a.setCulture(o8);
					});
				}
			}
			if (a.language == null && actorData.language != 0L)
			{
				Language o9 = World.world.languages.get(actorData.language);
				if (o9 != null)
				{
					Relink(delegate
					{
						a.setLanguage(o9);
					});
				}
			}
			if (a.religion == null && actorData.religion != 0L)
			{
				Religion o10 = World.world.religions.get(actorData.religion);
				if (o10 != null)
				{
					Relink(delegate
					{
						a.setReligion(o10);
					});
				}
			}
			if (a.lover == null && actorData.lover != 0L)
			{
				Actor o11 = World.world.units.get(actorData.lover);
				if (o11 != null && o11.isAlive())
				{
					Relink(delegate
					{
						a.setLover(o11);
					});
				}
			}
			if (actorData.homeBuildingID == 0L)
			{
				return;
			}
			Building o12 = World.world.buildings.get(actorData.homeBuildingID);
			if (Standing(o12))
			{
				Relink(delegate
				{
					a.setHomeBuilding(o12);
				});
			}
		}

		private static void Relink(Action set)
		{
			try
			{
				set();
			}
			catch (Exception ex)
			{
				if (_applyErrors++ < 10)
				{
					Log.Error("NET relinking a creature: " + ex.Message + " at " + (ex.StackTrace ?? "").Split('\n')[0].Trim());
				}
			}
		}

		private static void LoadItems(List<string> items)
		{
			foreach (string item2 in items)
			{
				try
				{
					ItemData itemData = JsonConvert.DeserializeObject<ItemData>(item2, SaveManager._settings);
					if (itemData == null)
					{
						continue;
					}
					Item item = World.world.items.get(itemData.id);
					if (item == null)
					{
						World.world.items.loadObject(itemData);
					}
					else if (item.data != null)
					{
						FieldInfo[] array = AllFields(typeof(ItemData));
						foreach (FieldInfo fieldInfo in array)
						{
							fieldInfo.SetValue(item.data, fieldInfo.GetValue(itemData));
						}
					}
				}
				catch (Exception ex)
				{
					if (_applyErrors++ < 10)
					{
						Log.Error("NET item data: " + ex.Message);
					}
				}
			}
		}

		private static FieldInfo[] AllFields(Type t)
		{
			if (_fieldsOf.TryGetValue(t, out var value))
			{
				return value;
			}
			List<FieldInfo> list = new List<FieldInfo>();
			Type type = t;
			while (type != null && type != typeof(object))
			{
				FieldInfo[] fields = type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				foreach (FieldInfo fieldInfo in fields)
				{
					if (!fieldInfo.IsInitOnly)
					{
						list.Add(fieldInfo);
					}
				}
				type = type.BaseType;
			}
			value = list.ToArray();
			_fieldsOf[t] = value;
			return value;
		}

		private static void JoinerMotionMessage(NetReader r)
		{
			float num = r.F32();
			BestOffset(Now - num, ref _hostClockOffset, ref _offsetSeenAt, ref _offsetWindow);
			int num2 = r.U16();
			for (int i = 0; i < num2; i++)
			{
				long key = (long)r.VarU();
				byte b = (byte)r.U8();
				Sample x = new Sample
				{
					T = num,
					Flags = b
				};
				x.P = new Vector2(r.Pos(), r.Pos());
				if ((b & 8) != 0)
				{
					x.H = (float)r.I16() / 64f;
				}
				x.Health = (int)r.VarI();
				if (!_motion.TryGetValue(key, out var value))
				{
					value = (_motion[key] = new Track());
				}
				value.Add(x);
				if ((b & 4) != 0)
				{
					value.AttackPending = true;
				}
			}
		}

		private static void ShowBurning(Actor a, bool burning)
		{
			try
			{
				if (burning != Torches.Burning(a))
				{
					if (burning)
					{
						((BaseSimObject)a).addStatusEffect("burning", 0f, pColorEffect: true);
					}
					else
					{
						a.finishStatusEffect("burning");
					}
				}
			}
			catch
			{
			}
		}

		private static void BestOffset(float off, ref float best, ref float windowStart, ref float windowMin)
		{
			if (off < windowMin)
			{
				windowMin = off;
			}
			if (float.IsNaN(best) || off < best)
			{
				best = off;
			}
			else if (Now - windowStart > 5f)
			{
				best = windowMin;
				windowMin = float.PositiveInfinity;
				windowStart = Now;
			}
		}

		private static void JoinerMotion()
		{
			if (float.IsNaN(_hostClockOffset))
			{
				return;
			}
			float num = Now - _hostClockOffset - 0.12f;
			long num2 = LocalBodyId();
			double last_attack_timestamp = 0.0;
			try
			{
				last_attack_timestamp = World.world.getCurWorldTime();
			}
			catch
			{
			}
			List<long> list = null;
			foreach (KeyValuePair<long, Track> item in _motion)
			{
				Actor actor = World.world.units.get(item.Key);
				if (item.Key == num2)
				{
					if (actor != null && actor.isAlive() && item.Value.Count > 0)
					{
						ShowBurning(actor, (item.Value.Last.Flags & 0x40) != 0);
					}
					continue;
				}
				if (actor == null || !actor.isAlive())
				{
					if (item.Value.Last.T < num - 5f)
					{
						(list ?? (list = new List<long>())).Add(item.Key);
					}
					continue;
				}
				Track value = item.Value;
				if (!value.At(num, out var p, out var h, out var nearest))
				{
					continue;
				}
				try
				{
					actor.current_position = p;
					actor.dirty_current_tile = true;
					actor.position_height = h;
					actor._is_moving = (nearest.Flags & 1) != 0;
					ShowBurning(actor, (nearest.Flags & 0x40) != 0);
					bool flag = (nearest.Flags & 2) != 0;
					if (actor.flip != flag)
					{
						actor.setFlip(flag);
					}
					if (value.AttackPending)
					{
						value.AttackPending = false;
						actor.last_attack_timestamp = last_attack_timestamp;
					}
					if (actor.data == null || actor.data.health == nearest.Health)
					{
						continue;
					}
					actor.data.health = nearest.Health;
					if (_baseA.TryGetValue(item.Key, out var value2))
					{
						try
						{
							value2.J = value2.J ?? JObject.Parse(value2.S);
							value2.J["health"] = nearest.Health;
							value2.S = null;
						}
						catch
						{
						}
					}
				}
				catch (Exception ex)
				{
					if (_applyErrors++ < 10)
					{
						Log.Error("NET moving creature " + item.Key + ": " + ex.Message);
					}
				}
			}
			if (list == null)
			{
				return;
			}
			foreach (long item2 in list)
			{
				_motion.Remove(item2);
			}
		}

		private static void JoinerTalk()
		{
			long num = 0L;
			try
			{
				Conversation conversation = WorldBoxMod.Instance?.Converse;
				if (conversation != null && conversation.Open && conversation.With?.data != null)
				{
					num = conversation.With.data.id;
				}
			}
			catch
			{
			}
			if (num != _talkSent || (num != 0L && !(Now - _talkSentAt < 2f)))
			{
				_talkSent = num;
				_talkSentAt = Now;
				W.Reset();
				W.VarU(51uL);
				W.U8(3);
				W.VarI(num);
				_s.ToHost(W, Delivery.Reliable);
			}
		}

		private static void HostHoldTalkers()
		{
			foreach (Remote value in _remotes.Values)
			{
				if (value.TalkingTo == 0L || Now - value.TalkSeenAt > 5f)
				{
					value.TalkingTo = 0L;
					continue;
				}
				Actor actor = World.world.units.get(value.TalkingTo);
				if (actor == null || !actor.isAlive())
				{
					value.TalkingTo = 0L;
					continue;
				}
				try
				{
					actor.makeWait(1f);
					actor.stopMovement();
					if (value.A != null)
					{
						actor.setFlip(value.A.current_position.x < actor.current_position.x);
					}
				}
				catch
				{
				}
			}
		}

		internal static bool ForwardStrike(Actor me, Actor target, Vector3 pos, WorldTile tile, float range, float head, bool slice, float scale)
		{
			if (!Mirroring || me == null || me.data == null || me.data.id != _myBody)
			{
				return false;
			}
			SendStrike(target, pos, tile, range, head, slice, scale);
			try
			{
				me.startAttackCooldown();
				Vector3 pDirection = pos - (Vector3)me.current_position;
				me.punchTargetAnimation(pDirection, me.flip, pReverse: false, 0f);
				me.last_attack_timestamp = World.world.getCurWorldTime();
			}
			catch
			{
			}
			return true;
		}

		private static void SendStrike(Actor target, Vector3 pos, WorldTile tile, float range, float head, bool slice, float scale)
		{
			W.Reset();
			W.VarU(51uL);
			W.U8(1);
			W.VarI((target?.data != null) ? target.data.id : 0);
			W.Pos(pos.x);
			W.Pos(pos.y);
			W.VarI(tile?.x ?? (-1));
			W.VarI(tile?.y ?? (-1));
			W.F32(range);
			W.F32(head);
			W.Bool(slice);
			W.F32(scale);
			W.Bool(Torches.MainLit);
			_s.ToHost(W, Delivery.Reliable);
			_attackedSinceSent = true;
		}

		internal static bool ForwardShotHit(Actor owner, Actor victim, string projectile, float head)
		{
			if (!Mirroring || owner == null || owner.data == null || owner.data.id != _myBody || victim?.data == null)
			{
				return false;
			}
			W.Reset();
			W.VarU(51uL);
			W.U8(2);
			W.VarI(victim.data.id);
			W.Str(projectile ?? "");
			W.F32(head);
			_s.ToHost(W, Delivery.Reliable);
			_attackedSinceSent = true;
			return true;
		}

		internal static bool ForwardIgnite(Building b, WorldTile ground)
		{
			if (!Mirroring || _myBody == 0L || (b?.data == null && ground == null))
			{
				return false;
			}
			W.Reset();
			W.VarU(51uL);
			W.U8(4);
			if (b?.data != null)
			{
				W.U8(1);
				W.VarI(b.data.id);
			}
			else
			{
				W.U8(2);
				W.VarI(ground.x);
				W.VarI(ground.y);
			}
			_s.ToHost(W, Delivery.Reliable);
			return true;
		}

		internal static bool ForwardAbility(Actor me, Ability ab, Vector2 at, Actor target, Vector2 dir)
		{
			if (!Mirroring || ab == null || me == null || me.data == null || me.data.id != _myBody)
			{
				return false;
			}
			W.Reset();
			W.VarU(51uL);
			W.U8(5);
			W.Str(Abilities.KeyOf(ab));
			W.Pos(at.x);
			W.Pos(at.y);
			W.VarI((target?.data != null) ? target.data.id : 0);
			W.F32(dir.x);
			W.F32(dir.y);
			_s.ToHost(W, Delivery.Reliable);
			_attackedSinceSent = true;
			AbilitiesSent++;
			return true;
		}

		internal static bool ForwardRushHit(Actor me, Actor victim, float damage, Vector2 dir, float stun)
		{
			if (!Mirroring || me == null || me.data == null || me.data.id != _myBody || victim?.data == null)
			{
				return false;
			}
			W.Reset();
			W.VarU(51uL);
			W.U8(6);
			W.VarI(victim.data.id);
			W.F32(damage);
			W.F32(dir.x);
			W.F32(dir.y);
			W.F32(stun);
			_s.ToHost(W, Delivery.Reliable);
			_attackedSinceSent = true;
			return true;
		}

		private static void TickMyTimers(Actor me)
		{
			try
			{
				float num = Mathf.Max(0f, _clockRate) * Time.unscaledDeltaTime;
				if (me.attack_timer > 0f)
				{
					me.attack_timer = Mathf.Max(0f, me.attack_timer - num);
				}
			}
			catch
			{
			}
		}

		private static void TellKill(NetPlayer killer, Actor victim)
		{
			if (victim?.data == null)
			{
				return;
			}
			foreach (NetPlayer value in _s.Players.Values)
			{
				bool flag = value == killer;
				if (flag || Parties.SameParty(value, killer))
				{
					if (value.Host)
					{
						CreditKill(victim, own: false);
						continue;
					}
					W.Reset();
					W.VarU(37uL);
					W.U8(1);
					W.VarI(victim.data.id);
					W.Bool(flag);
					value.Link?.Send(W, Delivery.Reliable);
				}
			}
		}

		private static void JoinerEvent(NetReader r)
		{
			switch ((byte)r.U8())
			{
			case 2:
				JoinerGuarded(r);
				break;
			case 4:
				JoinerLightAnswer(r);
				break;
			case 5:
				JoinerPlotAnswer(r);
				break;
			case 6:
				JoinerTownAnswer(r);
				break;
			case 1:
			{
				long pID = r.VarI();
				bool own = r.Bool();
				Actor actor = World.world.units.get(pID);
				if (actor != null)
				{
					CreditKill(actor, own);
				}
				break;
			}
			}
		}

		private static void CreditKill(Actor victim, bool own)
		{
			try
			{
				if (own)
				{
					Quests.OnKill(victim);
					return;
				}
				if (_killedByYou == null)
				{
					_killedByYou = typeof(Quests).GetField("KilledByYou", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				}
				if (_killedByYou?.GetValue(null) is Dictionary<long, double> dictionary)
				{
					dictionary[victim.data.id] = World.world.getCurWorldTime();
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET kill credit: " + ex.Message);
			}
		}

		private static void SetBlowScale(float v)
		{
			if (!_blowScaleLooked)
			{
				_blowScaleLooked = true;
				_blowScale = typeof(WorldSync).Assembly.GetType("FirstPerson.Moves")?.GetField("BlowScale", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			}
			try
			{
				_blowScale?.SetValue(null, v);
			}
			catch
			{
			}
		}

		private static void HostAct(NetPlayer p, NetReader r)
		{
			int num = r.U8();
			if (!_remotes.TryGetValue(p.Id, out var value) || value.A == null || !value.A.isAlive())
			{
				return;
			}
			Actor a = value.A;
			Kingdom kingdom = World.world.kingdoms_wild.get("possessed");
			switch (num)
			{
			case 3:
			{
				long num3 = r.VarI();
				if (num3 != value.TalkingTo && num3 != 0L)
				{
					Actor actor2 = World.world.units.get(num3);
					try
					{
						actor2?.cancelAllBeh();
					}
					catch
					{
					}
				}
				value.TalkingTo = num3;
				value.TalkSeenAt = Now;
				break;
			}
			case 1:
			{
				long num10 = r.VarI();
				Vector3 vector4 = new Vector3(r.Pos(), r.Pos(), 0f);
				int num11 = (int)r.VarI();
				int pY = (int)r.VarI();
				float num12 = r.F32();
				float num13 = r.F32();
				bool blow = r.Bool();
				float num14 = r.F32();
				num12 = (float.IsNaN(num12) ? 1f : Mathf.Clamp(num12, 0f, 10f));
				num13 = (float.IsNaN(num13) ? 1f : Mathf.Clamp(num13, 1f, 3f));
				num14 = (float.IsNaN(num14) ? 1f : Mathf.Clamp(num14, 0.5f, 3f));
				bool flag = r.Bool();
				Actor actor5 = ((num10 != 0L) ? World.world.units.get(num10) : null);
				if (!StrikeInReach(a, actor5, vector4))
				{
					Refused(p, "blow", "out of reach");
					break;
				}
				if (!Spend(p, 1, 6f, 3f))
				{
					Refused(p, "blow", "too many");
					break;
				}
				int num15 = 0;
				try
				{
					num15 = ((actor5 != null && actor5.isAlive()) ? actor5.getHealth() : 0);
				}
				catch
				{
				}
				WorldTile pTileTarget = ((num11 >= 0) ? World.world.GetTile(num11, pY) : null);
				if (actor5 != null && actor5.isAlive())
				{
					Vector2 current_position3 = actor5.current_position;
					vector4 = new Vector3(current_position3.x, current_position3.y + ((BaseSimObject)actor5).getHeight(), 0f);
					pTileTarget = actor5.current_tile;
				}
				FriendlyFire.Meant = actor5;
				Headshots.Aim(a, actor5, num13);
				Slicing.Blow = blow;
				SetBlowScale(num14);
				try
				{
					a.attack_timer = 0f;
					a.tryToAttack((BaseSimObject)null, pDoChecks: false, (Action)null, vector4, kingdom, pTileTarget, num12 * 0.2f);
				}
				catch (Exception ex5)
				{
					Log.Error("NET " + p?.ToString() + "'s blow: " + ex5.Message);
				}
				finally
				{
					FriendlyFire.Meant = null;
					Headshots.Clear();
					Slicing.Blow = false;
					SetBlowScale(1f);
				}
				if (_blowsLogged++ < 40 && actor5 != null)
				{
					Log.Info("NET " + p?.ToString() + "'s blow at " + WorldBoxMod.Describe(actor5) + " " + (actor5.current_position - a.current_position).magnitude.ToString("0.0") + " tiles off: health " + num15 + " -> " + actor5.getHealth());
				}
				if (actor5 != null && num15 > 0 && !actor5.isAlive())
				{
					TellKill(p, actor5);
				}
				else if (flag && actor5 != null && actor5.getHealth() < num15)
				{
					try
					{
						ActionLibrary.addBurningEffectOnTarget(a, actor5, actor5.current_tile);
						break;
					}
					catch
					{
						break;
					}
				}
				break;
			}
			case 7:
				HostLight(p, a, r);
				break;
			case 14:
				HostPlot(p, a, r);
				break;
			case 15:
				HostTown(p, a, r);
				break;
			case 8:
				HostDig(p, a, r);
				break;
			case 9:
				HostDigTo(p, a, r);
				break;
			case 12:
				HostPunch(p, a, r);
				break;
			case 13:
				HostGuard(p, value, r);
				break;
			case 4:
			{
				int num9 = r.U8();
				try
				{
					switch (num9)
					{
					case 1:
					{
						Building building = World.world.buildings.get(r.VarI());
						if (building != null && building.isAlive() && !Torches.Burning(building) && !((building.current_position - a.current_position).sqrMagnitude > 144f) && !ActionLibrary.addBurningEffectOnTarget(a, building, building.current_tile))
						{
							building.current_tile?.startFire(pForce: false);
						}
						break;
					}
					case 2:
					{
						WorldTile tile = World.world.GetTile((int)r.VarI(), (int)r.VarI());
						if (tile != null && !tile.is_liquid && !tile.isOnFire() && tile.building == null && !((new Vector2((float)tile.x + 0.5f, (float)tile.y + 0.5f) - a.current_position).sqrMagnitude > 64f))
						{
							tile.startFire(pForce: false);
						}
						break;
					}
					}
					break;
				}
				catch (Exception ex4)
				{
					Log.Error("NET " + p?.ToString() + "'s fire: " + ex4.Message);
					break;
				}
			}
			case 5:
			{
				string key = r.Str();
				Vector2 vector = new Vector2(r.Pos(), r.Pos());
				long num4 = r.VarI();
				Vector2 dir = new Vector2(r.F32(), r.F32());
				Ability ability = Abilities.ByKey(a, key);
				if (ability == null || Abilities.MovesBody(ability) || Abilities.CooldownLeft(a) > 1f)
				{
					break;
				}
				if (float.IsNaN(vector.x) || float.IsNaN(vector.y))
				{
					vector = a.current_position;
				}
				Vector2 vector2 = vector - a.current_position;
				float num5 = ability.Range + 1.5f;
				if (vector2.sqrMagnitude > num5 * num5)
				{
					vector = a.current_position + vector2.normalized * num5;
				}
				if (float.IsNaN(dir.x) || float.IsNaN(dir.y) || dir.sqrMagnitude < 0.0001f)
				{
					dir = ((vector2.sqrMagnitude > 0.0001f) ? vector2.normalized : Vector2.right);
				}
				else
				{
					dir.Normalize();
				}
				Actor actor3 = ((num4 != 0L) ? World.world.units.get(num4) : null);
				if (actor3 != null && (!actor3.isAlive() || (actor3.current_position - a.current_position).sqrMagnitude > num5 * num5))
				{
					actor3 = null;
				}
				int num6 = 0;
				try
				{
					num6 = actor3?.getHealth() ?? 0;
				}
				catch
				{
				}
				Abilities.StartCooldown(a, ability);
				Abilities.Pay(a, ability);
				AbilityUse arg = new AbilityUse
				{
					Self = a,
					At = vector,
					Target = actor3,
					Dir = dir
				};
				Actor caster = Abilities.Caster;
				Abilities.Caster = a;
				AbilitiesCast++;
				Log.Info("NET " + p?.ToString() + " casts " + ability.Name);
				try
				{
					if (!ability.Effect(arg))
					{
						Abilities.Refund(a, ability);
					}
				}
				catch (Exception ex2)
				{
					Log.Error("NET " + p?.ToString() + "'s " + ability.Name + ": " + ex2.Message);
				}
				finally
				{
					Abilities.Caster = caster;
				}
				if (actor3 != null && num6 > 0 && !actor3.isAlive())
				{
					TellKill(p, actor3);
				}
				break;
			}
			case 6:
			{
				Actor actor4 = World.world.units.get(r.VarI());
				float num7 = r.F32();
				Vector2 vector3 = new Vector2(r.F32(), r.F32());
				float num8 = r.F32();
				if (actor4 == null || !actor4.isAlive() || actor4 == a || (actor4.current_position - a.current_position).sqrMagnitude > 16f)
				{
					break;
				}
				float max = 10f;
				try
				{
					max = Mathf.Max(5f, ((BaseSimObject)a).stats["damage"]) * 1.6f;
				}
				catch
				{
				}
				num7 = (float.IsNaN(num7) ? 0f : Mathf.Clamp(num7, 0f, max));
				num8 = (float.IsNaN(num8) ? 0f : Mathf.Clamp(num8, 0f, 1.5f));
				if (float.IsNaN(vector3.x) || float.IsNaN(vector3.y) || vector3.sqrMagnitude < 0.0001f)
				{
					vector3 = Vector2.right;
				}
				else
				{
					vector3.Normalize();
				}
				try
				{
					((BaseSimObject)actor4).getHit(num7, pFlash: true, AttackType.Weapon, (BaseSimObject)a, pSkipIfShake: false, pMetallicWeapon: false, pCheckDamageReduction: true);
					if (!actor4.isAlive())
					{
						TellKill(p, actor4);
						break;
					}
					Vector2 current_position2 = actor4.current_position;
					actor4.calculateForce(current_position2.x, current_position2.y, current_position2.x - vector3.x, current_position2.y - vector3.y, 2.5f, 0.5f, pCheckCancelJobOnLand: true);
					if (num8 > 0f)
					{
						actor4.makeStunned(num8);
					}
					break;
				}
				catch (Exception ex3)
				{
					Log.Error("NET " + p?.ToString() + "'s charge: " + ex3.Message);
					break;
				}
			}
			case 2:
			{
				long pID = r.VarI();
				string text = r.Str();
				float num2 = r.F32();
				num2 = (float.IsNaN(num2) ? 1f : Mathf.Clamp(num2, 1f, 3f));
				Actor actor = World.world.units.get(pID);
				if (actor == null || !actor.isAlive() || actor == a)
				{
					break;
				}
				if ((actor.current_position - a.current_position).sqrMagnitude > 3600f)
				{
					Refused(p, "shot", "out of reach");
					break;
				}
				if (!Spend(p, 2, 24f, 10f))
				{
					Refused(p, "shot", "too many");
					break;
				}
				Vector2 current_position = actor.current_position;
				WorldTile pHitTile = World.world.GetTile((int)current_position.x, (int)current_position.y) ?? actor.current_tile;
				AttackData pData = new AttackData(a, pHitTile, new Vector3(current_position.x, current_position.y, ((BaseSimObject)actor).getHeight()), a.current_position, null, kingdom, AttackType.Weapon, pMetallicWeapon: false, pSkipShake: false, pProjectile: true, string.IsNullOrEmpty(text) ? null : text, null, 0.05f);
				FriendlyFire.Meant = actor;
				Headshots.Aim(a, actor, num2);
				try
				{
					MapBox.checkAttackFor(pData, actor);
				}
				catch (Exception ex)
				{
					Log.Error("NET " + p?.ToString() + "'s shot: " + ex.Message);
				}
				finally
				{
					FriendlyFire.Meant = null;
					Headshots.Clear();
				}
				if (!actor.isAlive())
				{
					TellKill(p, actor);
				}
				break;
			}
			case 10:
			case 11:
				break;
			}
		}

		private static void ResetBodies()
		{
			_remotes.Clear();
			_myBody = 0L;
			_askedBody = 0L;
			_takeGiven = false;
			_motionSentAt = 0f;
		}

		public static bool IsOtherPlayersBody(Actor a)
		{
			NetSession current = NetSession.Current;
			if (current == null || a == null || a.data == null)
			{
				return false;
			}
			long id = a.data.id;
			foreach (NetPlayer value in current.Players.Values)
			{
				if (value.Body == id && value != current.Me)
				{
					return true;
				}
			}
			return false;
		}

		public static bool HostStrikesFor(Actor a)
		{
			NetSession current = NetSession.Current;
			if (current != null && current.IsHost)
			{
				return IsOtherPlayersBody(a);
			}
			return false;
		}

		public static bool IsPlayerBody(Actor a)
		{
			if (NetSession.Current == null || a == null)
			{
				return false;
			}
			try
			{
				if (a == ControllableUnit._unit_main)
				{
					return true;
				}
			}
			catch
			{
			}
			return IsOtherPlayersBody(a);
		}

		private static void HostPossess(NetPlayer p, NetReader r)
		{
			int num = r.U8();
			long num2 = r.VarI();
			switch (num)
			{
			case 0:
				ReleaseBody(p);
				Confirm(p, 0L, "");
				_s.SendPlayers();
				return;
			case 2:
				HostBorn(p, num2);
				return;
			case 3:
			{
				long num3 = LastBodyOf(p);
				if (num3 == 0L)
				{
					Refuse(p, "Your last body is gone.");
					return;
				}
				num2 = num3;
				break;
			}
			}
			Actor actor = World.world.units.get(num2);
			string text = null;
			if (actor == null || !actor.isAlive())
			{
				text = "That creature is gone.";
			}
			else if (actor == ControllableUnit._unit_main || ControllableUnit.isControllingUnit(actor))
			{
				text = "The host is in that creature.";
			}
			else if (IsOtherPlayersBody(actor) && p.Body != num2)
			{
				text = "Another player is in that creature.";
			}
			else if (!actor.canBePossessed())
			{
				text = "That creature can't be taken over.";
			}
			if (text != null)
			{
				Refuse(p, text);
			}
			else
			{
				GiveBody(p, actor);
			}
		}

		private static void Refuse(NetPlayer p, string why)
		{
			bool num = _remotes.ContainsKey(p.Id);
			ReleaseBody(p);
			Confirm(p, 0L, why);
			if (num)
			{
				_s.SendPlayers();
			}
		}

		private static void Confirm(NetPlayer p, long id, string why)
		{
			W.Reset();
			W.VarU(60uL);
			W.VarI(id);
			W.Str(why);
			p.Link?.Send(W, Delivery.Reliable);
		}

		private static void Hold(Actor a)
		{
			try
			{
				bool num = !((BaseSimObject)a).hasStatus("possessed");
				((BaseSimObject)a).addStatusEffect("possessed", 10f, pColorEffect: false);
				if (num)
				{
					a.cancelAllBeh();
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET holding a body: " + ex.Message);
			}
		}

		private static void ReleaseBody(NetPlayer p)
		{
			if (!_remotes.TryGetValue(p.Id, out var value))
			{
				p.Body = 0L;
				p.God = true;
				return;
			}
			_remotes.Remove(p.Id);
			p.Body = 0L;
			p.God = true;
			RememberBody(p, value.Id);
			try
			{
				if (value.A != null && value.A.isAlive())
				{
					value.A.finishStatusEffect("possessed");
					value.A.setPossessedMovement(pValue: false);
					value.A.cancelAllBeh();
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET releasing a body: " + ex.Message);
			}
		}

		private static void HostReleaseBodies()
		{
			foreach (NetPlayer item in new List<NetPlayer>(_s.Players.Values))
			{
				ReleaseBody(item);
			}
		}

		private static void HostMyMotion(NetPlayer p, NetReader r)
		{
			float num = r.F32();
			byte b = (byte)r.U8();
			Vector2 vector = new Vector2(r.Pos(), r.Pos());
			float h = (((b & 8) != 0) ? ((float)r.I16() / 64f) : 0f);
			if (_known.TryGetValue(p.Id, out var value))
			{
				value.Focus = vector;
				value.HasFocus = true;
			}
			if ((b & 0x80) == 0 && _remotes.TryGetValue(p.Id, out var value2))
			{
				BestOffset(Now - num, ref value2.Offset, ref value2.OffsetAt, ref value2.OffsetWindow);
				value2.Path.Add(new Sample
				{
					T = num,
					P = vector,
					H = h,
					Flags = b
				});
				if ((b & 4) != 0)
				{
					value2.Path.AttackPending = true;
				}
			}
		}

		private static void HostBodies()
		{
			float now = Now;
			List<Remote> list = null;
			foreach (Remote value in _remotes.Values)
			{
				Actor a = value.A;
				if (a == null || !a.isAlive() || a.data == null || a.data.id != value.Id)
				{
					(list ?? (list = new List<Remote>())).Add(value);
					continue;
				}
				if (now - value.KeptAt > 2f)
				{
					value.KeptAt = now;
					Hold(a);
				}
				try
				{
					value.LastCity = ((a.city?.data != null) ? a.city.data.id : value.LastCity);
					value.LastAsset = a.asset?.id ?? value.LastAsset;
				}
				catch
				{
				}
				if (float.IsNaN(value.Offset) || value.Path.Count == 0 || !value.Path.At(now - value.Offset - 0.1f, out var p, out var h, out var nearest))
				{
					continue;
				}
				try
				{
					a.current_position = p;
					a.dirty_current_tile = true;
					a.position_height = h;
					a.setPossessedMovement((nearest.Flags & 1) != 0);
					bool flag = (nearest.Flags & 2) != 0;
					if (a.flip != flag)
					{
						a.setFlip(flag);
					}
				}
				catch (Exception ex)
				{
					if (_applyErrors++ < 10)
					{
						Log.Error("NET moving a player's body: " + ex.Message);
					}
				}
			}
			if (list == null)
			{
				return;
			}
			foreach (Remote item in list)
			{
				_remotes.Remove(item.P.Id);
				_s.System(item.P.Name + " died");
				HostDeath(item.P, item.A, item.Id, item.LastCity, item.LastAsset);
			}
			_s.SendPlayers();
		}

		private static void HookStatus()
		{
			if (_statusHooked)
			{
				return;
			}
			try
			{
				StatusAsset statusAsset = AssetManager.status.get("possessed");
				if (statusAsset != null && statusAsset.action != null)
				{
					_possessedAction = statusAsset.action;
					statusAsset.action = (BaseSimObject target, WorldTile tile) => (NetSession.Current != null && target is Actor a && IsOtherPlayersBody(a)) || _possessedAction(target, tile);
					_statusHooked = true;
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET possessed status: " + ex.Message);
			}
		}

		private static void JoinerBody()
		{
			HookStatus();
			Actor actor = null;
			try
			{
				actor = ControllableUnit._unit_main;
			}
			catch
			{
			}
			long num = ((actor != null && actor.isAlive() && actor.data != null) ? actor.data.id : 0);
			if (_takeGiven && _myBody != 0L && num != _myBody)
			{
				Actor actor2 = World.world.units.get(_myBody);
				if (actor2 != null && actor2.isAlive())
				{
					try
					{
						ControllableUnit.setControllableCreature(actor2);
					}
					catch (Exception ex)
					{
						Log.Error("NET taking the given body: " + ex.Message);
					}
					KeepFrozen();
					actor = actor2;
					num = _myBody;
					_takeGiven = false;
				}
				else
				{
					if (!(Now - _givenAt > 15f))
					{
						return;
					}
					_takeGiven = false;
					_myBody = 0L;
				}
			}
			if (num == _myBody)
			{
				_takeGiven = false;
			}
			if (num != _myBody && num != _askedBody && Now - _bodyAskedAt > 0.5f)
			{
				_askedBody = num;
				_bodyAskedAt = Now;
				W.Reset();
				W.VarU(53uL);
				W.U8((num != 0) ? 1 : 0);
				W.VarI(num);
				_s.ToHost(W, Delivery.Reliable);
				if (num == 0L)
				{
					_myBody = 0L;
				}
			}
			if (actor != null && num != 0L)
			{
				try
				{
					if (!((BaseSimObject)actor).hasStatus("possessed"))
					{
						((BaseSimObject)actor).addStatusEffect("possessed", 10f, pColorEffect: false);
					}
					TickMyTimers(actor);
					_possessedAction?.Invoke(actor, actor.current_tile);
				}
				catch (Exception ex2)
				{
					if (_applyErrors++ < 10)
					{
						Log.Error("NET moving your body: " + ex2.Message);
					}
				}
			}
			float num2 = ((num != 0L) ? 0.05f : 0.5f);
			if (Now - _motionSentAt < num2)
			{
				return;
			}
			_motionSentAt = Now;
			W.Reset();
			W.VarU(50uL);
			W.F32(Now);
			if (actor != null && num != 0L)
			{
				int num3 = 0;
				if (actor.is_moving)
				{
					num3 |= 1;
				}
				if (actor.flip)
				{
					num3 |= 2;
				}
				bool num4 = Mathf.Abs(actor.position_height) > 0.01f;
				if (num4)
				{
					num3 |= 8;
				}
				if (_attackedSinceSent)
				{
					num3 |= 4;
				}
				_attackedSinceSent = false;
				W.U8(num3);
				W.Pos(actor.current_position.x);
				W.Pos(actor.current_position.y);
				if (num4)
				{
					W.I16((int)Mathf.Clamp(actor.position_height * 64f, -32000f, 32000f));
				}
			}
			else
			{
				Vector3 vector = ((Camera.main != null) ? Camera.main.transform.position : Vector3.zero);
				W.U8(128);
				W.Pos(vector.x);
				W.Pos(vector.y);
			}
			_s.ToHost(W, Delivery.Unreliable);
		}

		private static void JoinerYourBody(NetReader r)
		{
			long num = r.VarI();
			string text = r.Str() ?? "";
			if (num != 0L)
			{
				_myBody = num;
				_askedBody = 0L;
				Actor actor = null;
				try
				{
					actor = ControllableUnit._unit_main;
				}
				catch
				{
				}
				if (actor?.data == null || actor.data.id != num)
				{
					_takeGiven = true;
					_givenAt = Now;
				}
				if (text != "")
				{
					WorldBoxMod.Instance?.Toast(text);
				}
				return;
			}
			_askedBody = 0L;
			_myBody = 0L;
			try
			{
				if (ControllableUnit.isControllingUnit())
				{
					ControllableUnit.clear(pCallKill: false);
				}
			}
			catch
			{
			}
			KeepFrozen();
			if (text != "")
			{
				WorldBoxMod.Instance?.Toast(text);
			}
		}

		private static void HostBurning()
		{
			if (Now - _burningAt < 0.5f)
			{
				return;
			}
			_burningAt = Now;
			_burningNow.Clear();
			try
			{
				foreach (Building simple in World.world.buildings.getSimpleList())
				{
					if (simple?.data != null && simple.isAlive() && Torches.Burning(simple))
					{
						_burningNow.Add(simple.data.id);
					}
				}
			}
			catch
			{
				return;
			}
			List<long> list = null;
			List<long> list2 = null;
			foreach (long item in _burningNow)
			{
				if (!_burningB.Contains(item))
				{
					(list ?? (list = new List<long>())).Add(item);
				}
			}
			foreach (long item2 in _burningB)
			{
				if (!_burningNow.Contains(item2))
				{
					(list2 ?? (list2 = new List<long>())).Add(item2);
				}
			}
			if (list == null && list2 == null)
			{
				return;
			}
			_burningB.Clear();
			_burningB.UnionWith(_burningNow);
			foreach (KeyValuePair<int, Known> item3 in _known)
			{
				NetPlayer netPlayer = PlayerById(item3.Key);
				if (netPlayer != null && netPlayer.Ready)
				{
					SendBurning(netPlayer, list, list2);
				}
			}
		}

		private static void SendBurning(NetPlayer p, ICollection<long> on, ICollection<long> off)
		{
			int num = on?.Count ?? 0;
			int num2 = off?.Count ?? 0;
			if (p?.Link == null || num + num2 == 0)
			{
				return;
			}
			W.Reset();
			W.VarU(43uL);
			W.VarU((ulong)num);
			if (on != null)
			{
				foreach (long item in on)
				{
					W.VarI(item);
				}
			}
			W.VarU((ulong)num2);
			if (off != null)
			{
				foreach (long item2 in off)
				{
					W.VarI(item2);
				}
			}
			p.Link.Send(W, Delivery.Reliable);
		}

		private static void JoinerBurning(NetReader r)
		{
			for (int i = 0; i < 2; i++)
			{
				int num = r.Count(100000);
				for (int j = 0; j < num; j++)
				{
					Building building = World.world.buildings.get(r.VarI());
					if (building == null || !building.isAlive())
					{
						continue;
					}
					try
					{
						if (i == 0)
						{
							if (!Torches.Burning(building))
							{
								((BaseSimObject)building).addStatusEffect("burning", 0f, pColorEffect: true);
							}
						}
						else if (Torches.Burning(building))
						{
							building.finishStatusEffect("burning");
						}
					}
					catch
					{
					}
				}
			}
		}

		private static void ResetBuildings()
		{
			_burningB.Clear();
			_burningAt = 0f;
			_trackedB.Clear();
			_trackedBList.Clear();
			_hashBCursor = 0;
			_scannedBAt = -1f;
		}

		private static void MarkBuildingsKnown(Known k)
		{
			foreach (TrackedB trackedB in _trackedBList)
			{
				k.BVersions[trackedB.Id] = trackedB.Version;
			}
		}

		private static bool Standing(Building b)
		{
			try
			{
				return b != null && b.data != null && b.data.state != BuildingState.Removed && b.isAlive();
			}
			catch
			{
				return false;
			}
		}

		private static void HostScanBuildings(bool initial)
		{
			if (!initial && Now - _scannedBAt < 0.5f)
			{
				return;
			}
			_scannedBAt = Now;
			int num = ++_scanBStamp;
			List<Building> simpleList;
			try
			{
				simpleList = World.world.buildings.getSimpleList();
			}
			catch
			{
				return;
			}
			List<TrackedB> list = null;
			foreach (Building item in simpleList)
			{
				if (!Standing(item))
				{
					continue;
				}
				long id = item.data.id;
				if (_trackedB.TryGetValue(id, out var value))
				{
					value.B = item;
					value.Seen = num;
					continue;
				}
				value = new TrackedB
				{
					B = item,
					Id = id,
					Seen = num
				};
				_trackedB[id] = value;
				_trackedBList.Add(value);
				if (!initial)
				{
					(list ?? (list = new List<TrackedB>())).Add(value);
				}
			}
			for (int num2 = _trackedBList.Count - 1; num2 >= 0; num2--)
			{
				TrackedB trackedB = _trackedBList[num2];
				if (trackedB.Seen != num)
				{
					_trackedBList.RemoveAt(num2);
					_trackedB.Remove(trackedB.Id);
					SendBuildingGone(trackedB);
				}
			}
			if (list == null)
			{
				return;
			}
			foreach (TrackedB item2 in list)
			{
				SendBuildingData(item2, born: true);
			}
		}

		private static string BuildingJson(Building b)
		{
			try
			{
				try
				{
					b.prepareForSave();
				}
				catch
				{
				}
				return JsonConvert.SerializeObject(b.data, SaveJson);
			}
			catch (Exception ex)
			{
				if (_jsonErrors++ < 5)
				{
					Log.Error("NET building data: " + ex.Message);
				}
				return null;
			}
		}

		private static void SendBuildingData(TrackedB t, bool born)
		{
			if (_known.Count == 0 || t.B?.data == null || t.B.data.id != t.Id || !Standing(t.B))
			{
				return;
			}
			string text = BuildingJson(t.B);
			if (text == null)
			{
				return;
			}
			uint num = Fnv(text);
			if (!born && num == t.Hash)
			{
				return;
			}
			t.Hash = num;
			t.HashedAt = Now;
			if (!born)
			{
				t.Version++;
			}
			W.Reset();
			W.VarU(34uL);
			W.U8(2);
			W.VarU((ulong)t.Id);
			W.Str(text);
			foreach (KeyValuePair<int, Known> item in _known)
			{
				int value;
				bool flag = item.Value.BVersions.TryGetValue(t.Id, out value);
				if (born || (flag && value < t.Version))
				{
					item.Value.BVersions[t.Id] = t.Version;
					PlayerById(item.Key)?.Link?.Send(W, Delivery.Reliable);
				}
			}
		}

		private static void SendBuildingGone(TrackedB t)
		{
			if (_known.Count == 0)
			{
				return;
			}
			W.Reset();
			W.VarU(36uL);
			W.VarU((ulong)t.Id);
			foreach (KeyValuePair<int, Known> item in _known)
			{
				if (item.Value.BVersions.Remove(t.Id))
				{
					PlayerById(item.Key)?.Link?.Send(W, Delivery.Reliable);
				}
			}
		}

		private static void HostBuildingData(float until)
		{
			int count = _trackedBList.Count;
			if (count == 0 || _known.Count == 0)
			{
				return;
			}
			float now = Now;
			for (int i = 0; i < count; i++)
			{
				if (!(Now < until))
				{
					break;
				}
				_hashBCursor = (_hashBCursor + 1) % count;
				TrackedB trackedB = _trackedBList[_hashBCursor];
				if (!(now - trackedB.HashedAt < 4f))
				{
					trackedB.HashedAt = now;
					SendBuildingData(trackedB, born: false);
				}
			}
		}

		private static void JoinerBuildingData(long id, string json)
		{
			_hostBuildings.Add(id);
			if (_baseB.ContainsKey(id))
			{
				Building building = World.world.buildings.get(id);
				if (Standing(building))
				{
					PatchBuildingIfChanged(building);
				}
			}
			try
			{
				ApplyBuildingData(id, json);
			}
			finally
			{
				RebaseBuilding(id);
			}
		}

		private static void ApplyBuildingData(long id, string json)
		{
			BuildingData buildingData = JsonConvert.DeserializeObject<BuildingData>(json, SaveManager._settings);
			if (buildingData == null || (buildingData.id != 0L && buildingData.id != id))
			{
				return;
			}
			Building building = World.world.buildings.get(id);
			if (building != null && building.data != null && Standing(building) && building.data.asset_id == buildingData.asset_id)
			{
				BuildingState state = building.data.state;
				if (_bFields == null)
				{
					_bFields = AllFields(typeof(BuildingData));
				}
				FieldInfo[] bFields = _bFields;
				foreach (FieldInfo fieldInfo in bFields)
				{
					fieldInfo.SetValue(building.data, fieldInfo.GetValue(buildingData));
				}
				try
				{
					building.data.resources?.loadFromSave();
				}
				catch (Exception ex)
				{
					if (_applyErrors++ < 10)
					{
						Log.Error("NET building " + id + " stock: " + ex.Message);
					}
				}
				try
				{
					if (buildingData.state != state)
					{
						if (buildingData.state == BuildingState.Ruins)
						{
							building.data.state = state;
							building.startMakingRuins();
						}
						else
						{
							building.setState(buildingData.state);
						}
					}
					building.setAnimData(buildingData.frameID);
					((BaseSimObject)building).updateStats();
					return;
				}
				catch (Exception ex2)
				{
					if (_applyErrors++ < 10)
					{
						Log.Error("NET building " + id + " data: " + ex2.Message);
					}
					return;
				}
			}
			if (building != null && Standing(building))
			{
				try
				{
					building.removeBuildingFinal();
				}
				catch
				{
				}
			}
			if (buildingData.state == BuildingState.Removed)
			{
				_waitLand.Remove(id);
			}
			else
			{
				if (WaitsForLand(id, buildingData, json))
				{
					return;
				}
				try
				{
					if (ClearSpot(buildingData) && World.world.buildings.get(id) == null && LoadDirect(buildingData) == null && _applyErrors++ < 10)
					{
						WorldTile tileSimple = World.world.GetTileSimple(buildingData.mainX, buildingData.mainY);
						Log.Error("NET building " + id + " (" + buildingData.asset_id + ", state " + buildingData.state.ToString() + ") would not load at " + buildingData.mainX + "," + buildingData.mainY + ((tileSimple == null) ? ": no tile" : (": tile " + tileSimple.main_type?.id + ((tileSimple.building != null) ? (", taken by " + tileSimple.building.asset?.id + " " + tileSimple.building.data?.id) : ""))));
					}
				}
				catch (Exception ex3)
				{
					if (_applyErrors++ < 10)
					{
						Log.Error("NET building " + id + " load: " + ex3.Message);
					}
				}
			}
		}

		private static bool WaitsForLand(long id, BuildingData d, string json)
		{
			try
			{
				BuildingAsset buildingAsset = AssetManager.buildings.get(d.asset_id);
				WorldTile tileSimple = World.world.GetTileSimple(d.mainX, d.mainY);
				if (buildingAsset == null || !buildingAsset.city_building || d.cityID <= 0 || tileSimple?.zone == null || tileSimple.zone.city != null)
				{
					_waitLand.Remove(id);
					return false;
				}
				float now = Now;
				if (_waitLand.TryGetValue(id, out var value))
				{
					value.Json = json;
					if (now - value.Since > 6f)
					{
						_waitLand.Remove(id);
						Log.Info("NET building " + id + " (" + d.asset_id + ") loaded without its town's land after " + 6f + " s");
						return false;
					}
					return true;
				}
				_waitLand[id] = new WaitingB
				{
					Json = json,
					Since = now
				};
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static void JoinerWaitingBuildings()
		{
			if (_waitLand.Count == 0)
			{
				return;
			}
			float now = Now;
			List<long> list = null;
			foreach (KeyValuePair<long, WaitingB> item in _waitLand)
			{
				bool flag = now - item.Value.Since > 6f;
				if (!flag)
				{
					try
					{
						BuildingData buildingData = JsonConvert.DeserializeObject<BuildingData>(item.Value.Json, SaveManager._settings);
						WorldTile worldTile = ((buildingData != null) ? World.world.GetTileSimple(buildingData.mainX, buildingData.mainY) : null);
						flag = buildingData == null || worldTile?.zone?.city != null;
					}
					catch
					{
						flag = true;
					}
				}
				if (flag)
				{
					(list ?? (list = new List<long>())).Add(item.Key);
				}
			}
			if (list == null)
			{
				return;
			}
			foreach (long item2 in list)
			{
				if (!_waitLand.TryGetValue(item2, out var value))
				{
					continue;
				}
				if (now - value.Since > 6f)
				{
					value.Since = now - 6f - 1f;
				}
				else
				{
					_waitLand.Remove(item2);
				}
				try
				{
					ApplyBuildingData(item2, value.Json);
				}
				catch (Exception ex)
				{
					if (_applyErrors++ < 10)
					{
						Log.Error("NET building " + item2 + " load: " + ex.Message);
					}
				}
				finally
				{
					RebaseBuilding(item2);
				}
			}
		}

		private static Building LoadDirect(BuildingData d)
		{
			BuildingAsset buildingAsset = AssetManager.buildings.get(d.asset_id);
			WorldTile tileSimple = World.world.GetTileSimple(d.mainX, d.mainY);
			if (buildingAsset == null || tileSimple == null || d.state == BuildingState.Removed)
			{
				return null;
			}
			BuildingManager buildings = World.world.buildings;
			if (_baseLoad == null || _baseLoadFor != buildings)
			{
				IntPtr functionPointer = typeof(SystemManager<Building, BuildingData>).GetMethod("loadObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(BuildingData) }, null).MethodHandle.GetFunctionPointer();
				_baseLoad = (Func<BuildingData, Building>)Activator.CreateInstance(typeof(Func<BuildingData, Building>), buildings, functionPointer);
				_baseLoadFor = buildings;
			}
			Building building = _baseLoad(d);
			if (building == null)
			{
				return null;
			}
			((BaseSimObject)building).create();
			building.setBuilding(tileSimple, buildingAsset, d);
			building.loadBuilding(d);
			return building;
		}

		private static bool ClearSpot(BuildingData d)
		{
			try
			{
				BuildingAsset buildingAsset = AssetManager.buildings.get(d.asset_id);
				if (buildingAsset == null)
				{
					return true;
				}
				Building building = World.world.GetTileSimple(d.mainX, d.mainY)?.building;
				if (building?.data == null || building.data.id == d.id)
				{
					return true;
				}
				if (Minor(buildingAsset) && building.asset != null && !Minor(building.asset) && _hostBuildings.Contains(building.data.id) && Standing(building))
				{
					if (_spotsKept++ < 20)
					{
						Log.Info("NET building " + d.id + " (" + d.asset_id + ") not loaded: the host's " + building.asset.id + " " + building.data.id + " stands there");
					}
					return false;
				}
				building.removeBuildingFinal();
			}
			catch
			{
			}
			return true;
		}

		private static bool Minor(BuildingAsset a)
		{
			switch (a.building_type)
			{
			case BuildingType.Building_Tree:
			case BuildingType.Building_Fruits:
			case BuildingType.Building_Poops:
			case BuildingType.Building_Wheat:
			case BuildingType.Building_Plant:
			case BuildingType.Building_Creep:
				return true;
			default:
				return false;
			}
		}

		private static void JoinerBuildingGone(NetReader r)
		{
			long num = (long)r.VarU();
			_hostBuildings.Remove(num);
			_baseB.Remove(num);
			_waitLand.Remove(num);
			Building building = World.world.buildings.get(num);
			if (building == null || !Standing(building))
			{
				return;
			}
			try
			{
				building.startDestroyBuilding();
			}
			catch
			{
				try
				{
					building.removeBuildingFinal();
				}
				catch
				{
				}
			}
		}

		private static void HostClock()
		{
			float now = Now;
			if (now - _clockSentAt < 0.1f)
			{
				return;
			}
			double world_time = World.world.map_stats.world_time;
			if (_lastHostReal > 0f && now > _lastHostReal)
			{
				float num = (float)((world_time - _lastHostTime) / (double)(now - _lastHostReal));
				_hostRate = ((_hostRate <= 0f) ? num : Mathf.Lerp(_hostRate, num, 0.3f));
			}
			_lastHostTime = world_time;
			_lastHostReal = now;
			_clockSentAt = now;
			W.Reset();
			W.VarU(30uL);
			W.F64(world_time);
			W.F32(Mathf.Max(0f, _hostRate));
			W.Bool(World.world.isPaused());
			string s = "";
			try
			{
				s = Config.time_scale_asset?.id ?? "";
			}
			catch
			{
			}
			W.Str(s);
			DayCycle obj2 = WorldBoxMod.Instance?.Day;
			W.F32(obj2?.TimeOfDay ?? (-1f));
			W.VarU((ulong)Math.Max(0, FirstPerson.Towns.Calendar.Day));
			HostGodPlayers(_gods);
			W.VarU((ulong)_gods.Count);
			foreach (KeyValuePair<int, Vector2> god in _gods)
			{
				W.VarU((ulong)god.Key);
				W.Pos(god.Value.x);
				W.Pos(god.Value.y);
			}
			W.F32(Time.unscaledTime);
			_s.ToAll(W, Delivery.Unreliable);
		}

		private static void HostGodPlayers(List<KeyValuePair<int, Vector2>> into)
		{
			into.Clear();
			foreach (NetPlayer value2 in _s.Players.Values)
			{
				if (value2.Body != 0L)
				{
					continue;
				}
				Known value;
				if (value2.Host)
				{
					if (!ControllableUnit.isControllingUnit())
					{
						Camera main = Camera.main;
						if (main != null)
						{
							into.Add(new KeyValuePair<int, Vector2>(value2.Id, main.transform.position));
						}
					}
				}
				else if (value2.Ready && _known.TryGetValue(value2.Id, out value) && value.HasFocus)
				{
					into.Add(new KeyValuePair<int, Vector2>(value2.Id, value.Focus));
				}
			}
		}

		public static void GodPlayers(List<KeyValuePair<int, Vector2>> into)
		{
			into.Clear();
			NetSession current = NetSession.Current;
			if (current == null)
			{
				return;
			}
			if (current.IsHost)
			{
				HostGodPlayers(into);
			}
			else
			{
				foreach (KeyValuePair<int, Vector2> joinerGod in _joinerGods)
				{
					into.Add(joinerGod);
				}
			}
			int me = ((current.Me != null) ? current.Me.Id : (-1));
			into.RemoveAll((KeyValuePair<int, Vector2> g) => g.Key == me);
		}

		private static void JoinerClockMessage(NetReader r)
		{
			double num = r.F64();
			float num2 = r.F32();
			_hostPaused = r.Bool();
			_hostSpeed = r.Str();
			float num3 = r.F32();
			int hostDay = (int)r.VarU();
			if (num3 >= 0f)
			{
				_hostHour = num3;
				_hostDay = hostDay;
				_hourFresh = true;
			}
			int num4 = r.Count(64);
			_joinerGods.Clear();
			for (int i = 0; i < num4; i++)
			{
				_joinerGods.Add(new KeyValuePair<int, Vector2>((int)r.VarU(), new Vector2(r.Pos(), r.Pos())));
			}
			float num5 = r.F32();
			float num6 = ((_s?.HostLink != null) ? _s.HostLink.PingMs : 0f);
			float num7 = num5 + num6 * 0.0005f - Time.unscaledTime;
			if (!_seaSet || Mathf.Abs(num7 - SeaOffset) > 2f)
			{
				SeaOffset = num7;
				_seaSet = true;
			}
			else
			{
				SeaOffset += (num7 - SeaOffset) * 0.1f;
			}
			double num8 = num + (double)(num2 * num6) * 0.0005;
			_clockRate = (_hostPaused ? 0f : num2);
			if (!_clockSet || Math.Abs(num8 - _clock) > 2.0)
			{
				_clock = num8;
				_clockSet = true;
				return;
			}
			double num9 = num8 - _clock;
			if (num9 > 0.0)
			{
				_clock += num9 * 0.3;
			}
			else
			{
				_clockRate *= 0.9f;
			}
		}

		public static bool HostHour(out float hour, out int day)
		{
			hour = _hostHour;
			day = _hostDay;
			if (!_hourFresh || !NetSession.Joined)
			{
				return false;
			}
			_hourFresh = false;
			return true;
		}

		private static void KeepHostSpeed()
		{
			if (string.IsNullOrEmpty(_hostSpeed))
			{
				return;
			}
			try
			{
				WorldTimeScaleAsset time_scale_asset = Config.time_scale_asset;
				if (time_scale_asset != null && time_scale_asset.id == _hostSpeed)
				{
					return;
				}
				WorldTimeScaleAsset worldTimeScaleAsset = AssetManager.time_scales.get(_hostSpeed);
				if (worldTimeScaleAsset != null)
				{
					bool num = _speedSetTo != null && time_scale_asset != null && time_scale_asset.id != _speedSetTo;
					if (_ownSpeed == null && time_scale_asset != null)
					{
						_ownSpeed = time_scale_asset.id;
					}
					Config.setWorldSpeed(worldTimeScaleAsset, pUpdateDebug: false);
					_speedSetTo = worldTimeScaleAsset.id;
					if (num && Now - _speedToldAt > 3f)
					{
						_speedToldAt = Now;
						WorldBoxMod.Instance?.Toast("The host sets the world's speed.");
					}
				}
			}
			catch
			{
			}
		}

		private static void OwnSpeedBack()
		{
			string ownSpeed = _ownSpeed;
			_hostSpeed = (_speedSetTo = (_ownSpeed = null));
			_hourFresh = false;
			SeaOffset = 0f;
			_seaSet = false;
			_hostDay = 0;
			if (ownSpeed == null)
			{
				return;
			}
			try
			{
				WorldTimeScaleAsset worldTimeScaleAsset = AssetManager.time_scales.get(ownSpeed);
				if (worldTimeScaleAsset != null)
				{
					Config.setWorldSpeed(worldTimeScaleAsset, pUpdateDebug: false);
				}
			}
			catch
			{
			}
		}

		private static void JoinerClock()
		{
			KeepHostSpeed();
			if (!_clockSet)
			{
				return;
			}
			_clock += _clockRate * Time.unscaledDeltaTime;
			try
			{
				World.world.map_stats.world_time = _clock;
			}
			catch
			{
			}
		}

		private static void KeepFrozen()
		{
			try
			{
				if (!Config.paused)
				{
					Config.paused = true;
				}
				Config.autosaves = false;
				World.world._meta_skip = true;
			}
			catch
			{
			}
		}

		private static void ResetActs()
		{
			_budgets.Clear();
			_guards.Clear();
			_guardSentFlags = 0;
			_guardSentAt = -9f;
		}

		private static bool Spend(NetPlayer p, byte kind, float most, float perSecond)
		{
			long key = ((long)p.Id << 8) | kind;
			float now = Now;
			if (!_budgets.TryGetValue(key, out var value))
			{
				Dictionary<long, Budget> budgets = _budgets;
				Budget obj = new Budget
				{
					Left = most,
					At = now
				};
				value = obj;
				budgets[key] = obj;
			}
			value.Left = Mathf.Min(most, value.Left + (now - value.At) * perSecond);
			value.At = now;
			if (value.Left < 1f)
			{
				return false;
			}
			value.Left -= 1f;
			return true;
		}

		private static void Refused(NetPlayer p, string what, string why)
		{
			ActsRefused++;
			LastActRefusal = what + ": " + why;
			if (ActsRefused <= 30 || ActsRefused % 100 == 0)
			{
				Log.Info("NET " + p?.ToString() + "'s " + what + " refused: " + why + " (" + ActsRefused + " so far)");
			}
		}

		private static bool StrikeInReach(Actor body, Actor target, Vector3 pos)
		{
			float num = PossessionCombat.ReachOf(body) + 2.5f;
			if (target != null && target.isAlive())
			{
				float num2 = 0.5f;
				try
				{
					num2 = ((BaseSimObject)target).stats["size"];
				}
				catch
				{
				}
				num += num2;
				return (target.current_position - body.current_position).sqrMagnitude <= num * num;
			}
			if (float.IsNaN(pos.x) || float.IsNaN(pos.y))
			{
				return false;
			}
			num += 2f;
			return (new Vector2(pos.x, pos.y) - body.current_position).sqrMagnitude <= num * num;
		}

		internal static bool ForwardSwing(Actor me, Actor victim, bool head)
		{
			if (!Mirroring || me?.data == null || me.data.id != _myBody || victim?.data == null)
			{
				return false;
			}
			Vector2 current_position = victim.current_position;
			float range = 1f;
			try
			{
				range = me.getAttackRange();
			}
			catch
			{
			}
			SendStrike(victim, new Vector3(current_position.x, current_position.y + ((BaseSimObject)victim).getHeight(), 0f), victim.current_tile, range, head ? 1.5f : 1f, slice: true, 1f);
			return true;
		}

		internal static bool ForwardPunch(Actor me, Actor victim, bool head)
		{
			if (!Mirroring || me?.data == null || me.data.id != _myBody || victim?.data == null)
			{
				return false;
			}
			W.Reset();
			W.VarU(51uL);
			W.U8(12);
			W.VarI(victim.data.id);
			W.Bool(head);
			_s.ToHost(W, Delivery.Reliable);
			_attackedSinceSent = true;
			return true;
		}

		private static void HostPunch(NetPlayer p, Actor body, NetReader r)
		{
			Actor actor = World.world.units.get(r.VarI());
			bool flag = r.Bool();
			if (actor == null || !actor.isAlive() || actor == body)
			{
				return;
			}
			if (!StrikeInReach(body, actor, Vector3.zero))
			{
				Refused(p, "punch", "out of reach");
			}
			else if (!Spend(p, 1, 6f, 3f))
			{
				Refused(p, "punch", "too many");
			}
			else
			{
				if (!FriendlyFire.MayHurt(body, actor))
				{
					return;
				}
				try
				{
					((BaseSimObject)actor).getHit(PossessionCombat.BareDamage(body) * (flag ? 1.5f : 1f), pFlash: true, AttackType.Weapon, (BaseSimObject)body, pSkipIfShake: false, pMetallicWeapon: false, pCheckDamageReduction: true);
					if (!actor.isAlive())
					{
						TellKill(p, actor);
					}
				}
				catch (Exception ex)
				{
					Log.Error("NET " + p?.ToString() + "'s punch: " + ex.Message);
				}
			}
		}

		private static void JoinerGuard()
		{
			if (_myBody == 0L)
			{
				return;
			}
			Actor actor = null;
			try
			{
				actor = ControllableUnit._unit_main;
			}
			catch
			{
			}
			GuardControl guardControl = ((actor?.data != null && actor.data.id == _myBody) ? GuardControl.Current : null);
			int num = 0;
			float v = 0f;
			if (guardControl != null && guardControl.Active)
			{
				GuardState state = guardControl.State;
				if (state.Blocking)
				{
					num |= 1;
				}
				if (state.ParryOpen)
				{
					num |= 2;
					v = Mathf.Max(0f, state.ParryWindow - state.Held);
				}
			}
			Vector2 flat = FirstPersonAim.Flat;
			float now = Now;
			if (num != _guardSentFlags || (num != 0 && !(now - _guardSentAt < 0.05f) && (!(now - _guardSentAt < 0.25f) || !(Vector2.Angle(flat, _guardSentFacing) < 10f))))
			{
				_guardSentFlags = num;
				_guardSentAt = now;
				_guardSentFacing = flat;
				W.Reset();
				W.VarU(51uL);
				W.U8(13);
				W.U8(num);
				W.F32(v);
				W.U8((int)(Mathf.Clamp01(guardControl?.Chip ?? 0.5f) * 255f));
				W.F32(flat.x);
				W.F32(flat.y);
				_s.ToHost(W, Delivery.Reliable);
				GuardsSent++;
			}
		}

		private static void HostGuard(NetPlayer p, Remote rb, NetReader r)
		{
			int num = r.U8();
			float num2 = r.F32();
			float value = (float)r.U8() / 255f;
			Vector2 vector = new Vector2(r.F32(), r.F32());
			if (!_guards.TryGetValue(p.Id, out var value2))
			{
				value2 = (_guards[p.Id] = new RemoteGuard());
			}
			bool flag = (value2.Flags & 2) != 0;
			value2.P = p;
			value2.A = rb.A;
			value2.At = Now;
			value2.Flags = num & 3;
			if ((num & 2) != 0 && !flag)
			{
				value2.ParryUntil = Now + (float.IsNaN(num2) ? 0f : Mathf.Clamp(num2, 0f, 0.3f)) + 0.05f;
			}
			value2.Chip = Mathf.Clamp(value, 0.25f, 0.7f);
			if (!float.IsNaN(vector.x) && !float.IsNaN(vector.y) && vector.sqrMagnitude > 0.0001f)
			{
				value2.Facing = vector.normalized;
			}
			GuardsIn++;
		}

		internal static bool RemoteGuardTake(AttackData data, BaseSimObject by, Actor target, out AttackDataResult result)
		{
			result = AttackDataResult.Continue;
			if (_guards.Count == 0 || _s == null || !_s.IsHost || target == null)
			{
				return false;
			}
			RemoteGuard remoteGuard = null;
			foreach (RemoteGuard value in _guards.Values)
			{
				if (value.A == target)
				{
					remoteGuard = value;
					break;
				}
			}
			if (remoteGuard == null || remoteGuard.A.data == null || remoteGuard.P.Body != remoteGuard.A.data.id || Now - remoteGuard.At > 0.6f)
			{
				return false;
			}
			bool flag = Now < remoteGuard.ParryUntil;
			bool flag2 = (remoteGuard.Flags & 1) != 0;
			if (!flag && !flag2)
			{
				return false;
			}
			bool tooBig;
			GuardControl.Guarded guarded = GuardControl.Judge(data, by, target, flag, flag2, remoteGuard.Facing, remoteGuard.Chip, out result, out tooBig);
			switch (guarded)
			{
			case GuardControl.Guarded.None:
				return false;
			case GuardControl.Guarded.Parried:
				remoteGuard.ParryUntil = 0f;
				remoteGuard.RiposteUntil = Now + 0.8f;
				break;
			case GuardControl.Guarded.Broken:
				remoteGuard.Flags = 0;
				break;
			}
			GuardsStopped++;
			GW.Reset();
			GW.VarU(37uL);
			GW.U8(2);
			GW.U8((int)guarded);
			GW.VarI((by != null && by.isActor() && by.a.data != null) ? by.a.data.id : 0);
			GW.Bool(data.is_projectile);
			remoteGuard.P.Link?.Send(GW, Delivery.Reliable);
			return guarded != GuardControl.Guarded.Broken;
		}

		internal static void RemoteRiposte(ref AttackData data, Actor by)
		{
			if (_guards.Count == 0 || data.is_projectile || by == null)
			{
				return;
			}
			foreach (RemoteGuard value in _guards.Values)
			{
				if (value.A == by)
				{
					if (Now < value.RiposteUntil && GuardControl.Critical(ref data))
					{
						value.RiposteUntil = 0f;
					}
					break;
				}
			}
		}

		internal static bool GuardUpForTest(NetPlayer p)
		{
			if (p != null && _guards.TryGetValue(p.Id, out var value) && (value.Flags & 1) != 0)
			{
				return Now - value.At < 0.6f;
			}
			return false;
		}

		private static void JoinerGuarded(NetReader r)
		{
			GuardControl.Guarded what = (GuardControl.Guarded)r.U8();
			long num = r.VarI();
			bool shot = r.Bool();
			Actor actor = null;
			try
			{
				actor = ControllableUnit._unit_main;
			}
			catch
			{
			}
			if (actor?.data != null && actor.data.id == _myBody)
			{
				Actor actor2 = ((num != 0L) ? World.world.units.get(num) : null);
				GuardControl.Current?.TakenOnHost(what, actor, actor2, shot);
				GuardsHeard++;
			}
		}

		private static void HookHatch()
		{
			if (_hatchHooked)
			{
				return;
			}
			_hatchHooked = true;
			CrabGuard.Hatched = delegate(Actor crab, int owner)
			{
				if (NetSession.Hosting)
				{
					_hatched.Add(new KeyValuePair<Actor, int>(crab, owner));
				}
			};
		}

		private static void HostCrabs()
		{
			HookHatch();
			if (_hatched.Count > 0)
			{
				foreach (KeyValuePair<Actor, int> item in _hatched)
				{
					Actor key = item.Key;
					if (!_s.Players.TryGetValue(item.Value, out var value) || value == _s.Me)
					{
						continue;
					}
					try
					{
						if (key != null && key.isAlive() && key.data != null)
						{
							if (!value.God && value.Body != 0L)
							{
								Log.Info("NET " + value?.ToString() + "'s Crabzilla hatched, but they're in a body: it stands");
							}
							else
							{
								GiveBody(value, key);
							}
						}
					}
					catch (Exception ex)
					{
						Log.Error("NET giving " + value?.ToString() + " their Crabzilla: " + ex.Message);
					}
				}
				_hatched.Clear();
			}
			float elapsed;
			try
			{
				elapsed = (World.world.isPaused() ? 0f : World.world.elapsed);
			}
			catch
			{
				elapsed = 0f;
			}
			foreach (Remote value2 in _remotes.Values)
			{
				if (Crabzillas.Is(value2.A) && CrabFire.Of(value2.A, out var shot))
				{
					Crabzillas.BlastFor(value2.A, shot, elapsed);
				}
			}
			SendMyCrab();
		}

		private static void JoinerCrabs()
		{
			SendMyCrab();
		}

		private static void SendMyCrab()
		{
			if (!CrabFire.Mine(out var crab, _myShot) || crab?.data == null)
			{
				_crabWasFiring = false;
				return;
			}
			bool isHost = _s.IsHost;
			if (!isHost && crab.data.id != _myBody)
			{
				return;
			}
			bool any = _myShot.Any;
			if ((any || _crabWasFiring) && (!any || !(Now - _crabSentAt < 0.05f)))
			{
				_crabSentAt = Now;
				_crabWasFiring = any;
				W.Reset();
				W.VarU(58uL);
				WriteShot(crab.data.id, _myShot);
				if (isHost)
				{
					_s.ToAll(W, Delivery.Unreliable);
				}
				else
				{
					_s.ToHost(W, Delivery.Unreliable);
				}
			}
		}

		private static void WriteShot(long id, CrabFire.Shot shot)
		{
			int num = 0;
			if (shot.On[0])
			{
				num |= 1;
			}
			if (shot.On[1])
			{
				num |= 2;
			}
			if (shot.Strikes[0])
			{
				num |= 4;
			}
			if (shot.Strikes[1])
			{
				num |= 8;
			}
			if (float.IsNaN(shot.At[0].Z))
			{
				num |= 0x10;
			}
			if (float.IsNaN(shot.At[1].Z))
			{
				num |= 0x20;
			}
			W.VarI(id);
			W.U8(num);
			for (int i = 0; i < 2; i++)
			{
				Vec3 vec = shot.At[i];
				W.Pos(vec.X);
				W.Pos(vec.Y);
				if ((num & ((i == 0) ? 16 : 32)) == 0)
				{
					W.I16((int)Mathf.Clamp(vec.Z * 64f, -32000f, 32000f));
				}
			}
		}

		private static long ReadShot(NetReader r, CrabFire.Shot into)
		{
			long result = r.VarI();
			int num = r.U8();
			into.On[0] = (num & 1) != 0;
			into.On[1] = (num & 2) != 0;
			into.Strikes[0] = (num & 4) != 0;
			into.Strikes[1] = (num & 8) != 0;
			for (int i = 0; i < 2; i++)
			{
				float x = r.Pos();
				float y = r.Pos();
				float z = (((num & ((i == 0) ? 16 : 32)) == 0) ? ((float)r.I16() / 64f) : float.NaN);
				into.At[i] = new Vec3(x, y, z);
			}
			return result;
		}

		private static void Keep(long id, CrabFire.Shot from)
		{
			CrabFire.Shot shot = CrabFire.Heard(id);
			for (int i = 0; i < 2; i++)
			{
				shot.On[i] = from.On[i];
				shot.Strikes[i] = from.Strikes[i];
				shot.At[i] = from.At[i];
			}
		}

		private static void HostCrabFire(NetPlayer p, NetReader r)
		{
			long num = ReadShot(r, _readShot);
			if (num == p.Body && num != 0L)
			{
				Keep(num, _readShot);
				W.Reset();
				W.VarU(58uL);
				WriteShot(num, _readShot);
				_s.ToAll(W, Delivery.Unreliable, readyOnly: true, p);
			}
		}

		private static void JoinerCrabFire(NetReader r)
		{
			long num = ReadShot(r, _readShot);
			if (num != 0L && num != _myBody)
			{
				Keep(num, _readShot);
			}
		}

		public static void HostStart(NetSession s)
		{
			_s = s;
			_frame = 0;
			_hostStats = World.world?.map_stats;
			ResetActors();
			ResetBodies();
			HookStatus();
			ResetMeta();
			HostMeta(initial: true);
			LoadLastBodies();
			HostScanActors(initial: true);
			HostScanBuildings(initial: true);
			ResetTiles();
			HostTiles();
			ResetEffects();
		}

		public static void Tick(NetSession s)
		{
			_s = s;
			_frame++;
			if (!_crabGuardTried && !CrabGuard.Installed)
			{
				_crabGuardTried = true;
				if (Detour.Begin())
				{
					try
					{
						CrabGuard.Install();
					}
					catch (Exception ex)
					{
						Log.Error("NET could not take over Crabzilla's controls: " + ex.Message);
					}
					Detour.End();
				}
			}
			if (s.IsHost)
			{
				if (!Loading)
				{
					HostMeta(initial: false);
					HostScanActors(initial: false);
					HostScanBuildings(initial: false);
					HostBurning();
					HostLights();
					HostPlots();
					HostRelief();
					HostTiles();
					HostClock();
					HostBodies();
					HostCrabs();
					HostHoldTalkers();
					HostRespawns();
					HostShots();
					HostEffects();
					HostMotion();
					HostData();
					HostTransfers();
				}
			}
			else
			{
				JoinerTransfer();
				if (_worldIn && !Loading)
				{
					KeepFrozen();
					JoinerClock();
					JoinerApplyBuffered();
					JoinerWaitingBuildings();
					JoinerTownRealms();
					JoinerRulers();
					JoinerMotion();
					JoinerMovers();
					JoinerBody();
					JoinerCrabs();
					JoinerGuard();
					JoinerTalk();
					JoinerPatches();
				}
			}
		}

		public static void LateTick()
		{
			NetSession current = NetSession.Current;
			if (current != null && !current.IsHost && _worldIn && !Loading)
			{
				KeepFrozen();
			}
		}

		public static bool HostWorldChanged()
		{
			try
			{
				return World.world == null || World.world.map_stats != _hostStats || !Config.game_loaded || SmoothLoader.isLoading();
			}
			catch
			{
				return true;
			}
		}

		public static void Stop(NetSession s, string reason, bool byMe)
		{
			if (s.IsHost)
			{
				if (!s.WorldGone)
				{
					HostReleaseBodies();
				}
			}
			else
			{
				JoinerStop(reason, byMe);
			}
			_hostStats = null;
			_s = null;
			ResetActors();
			ResetBodies();
			ResetTiles();
			ResetMeta();
			ResetPatches();
			ResetEffects();
			_transfers.Clear();
			CrabFire.Clear();
			_hatched.Clear();
			ResetActs();
			_resyncing = false;
			_resyncAskedAt = -99f;
			_resyncAt.Clear();
			_rulerWait.Clear();
		}

		public static void PlayerReady(NetSession s, NetPlayer p)
		{
			_clockSentAt = 0f;
			if (_known.TryGetValue(p.Id, out var value))
			{
				value.MotionAt = 0f;
			}
			SendBurning(p, _burningB, null);
			SendLights(p);
			SendRelief(p);
			Log.Info("NET " + p?.ToString() + " has the world");
		}

		public static long LastBodyFor(NetPlayer p)
		{
			return LastBodyOf(p);
		}

		public static void PlayerLeft(NetSession s, NetPlayer p)
		{
			_transfers.RemoveAll((Transfer t) => t.Player == p);
			ForgetPlayer(p);
			ReleaseBody(p);
			_hands.Remove(p.Id);
			_guards.Remove(p.Id);
		}

		public static void HostMessage(NetSession s, NetPlayer p, int id, NetReader r)
		{
			if (!Loading)
			{
				switch (id)
				{
				case 50:
					HostMyMotion(p, r);
					break;
				case 53:
					HostPossess(p, r);
					break;
				case 51:
					HostAct(p, r);
					break;
				case 54:
					HostPower(p, r);
					break;
				case 52:
					HostPatch(p, r);
					break;
				case 58:
					HostCrabFire(p, r);
					break;
				case 55:
				case 56:
				case 57:
					break;
				}
			}
		}

		public static void JoinerMessage(NetSession s, int id, NetReader r)
		{
			switch (id)
			{
			case 20:
				JoinerWorldBegin(r);
				return;
			case 21:
				JoinerWorldChunk(r);
				return;
			case 22:
				JoinerWorldEnd(r);
				return;
			case 30:
				JoinerClockMessage(r);
				return;
			case 31:
				if (_worldIn && !Loading)
				{
					JoinerMotionMessage(r);
				}
				return;
			case 60:
				JoinerYourBody(r);
				return;
			case 37:
				if (_worldIn && !Loading)
				{
					JoinerEvent(r);
				}
				return;
			case 38:
				if (_worldIn && !Loading)
				{
					JoinerShot(r);
				}
				return;
			case 40:
				if (_worldIn && !Loading)
				{
					JoinerFx(r);
				}
				return;
			case 41:
				if (_worldIn && !Loading)
				{
					JoinerDrops(r);
				}
				return;
			case 42:
				if (_worldIn && !Loading)
				{
					JoinerMoversMessage(r);
				}
				return;
			case 58:
				if (_worldIn && !Loading)
				{
					JoinerCrabFire(r);
				}
				return;
			}
			byte[] array = r.Rest();
			if (!_worldIn || Loading || _buffered.Count > 0 || _incoming != null)
			{
				_buffered.Add(new KeyValuePair<int, byte[]>(id, array));
			}
			else
			{
				ApplyWorldMessage(id, new NetReader(array, 0, array.Length));
			}
		}

		private static void JoinerApplyBuffered()
		{
			if (_buffered.Count == 0 || _incoming != null)
			{
				return;
			}
			List<KeyValuePair<int, byte[]>> list = new List<KeyValuePair<int, byte[]>>(_buffered);
			_buffered.Clear();
			foreach (KeyValuePair<int, byte[]> item in list)
			{
				ApplyWorldMessage(item.Key, new NetReader(item.Value, 0, item.Value.Length));
			}
		}

		private static void ApplyWorldMessage(int id, NetReader r)
		{
			try
			{
				switch (id)
				{
				case 32:
					JoinerSpawn(r);
					break;
				case 33:
					JoinerDespawn(r);
					break;
				case 34:
					JoinerData(r);
					break;
				case 36:
					JoinerBuildingGone(r);
					break;
				case 35:
					JoinerTiles(r);
					break;
				case 39:
					JoinerMeta(r);
					break;
				case 63:
					JoinerPatchAck(r);
					break;
				case 43:
					JoinerBurning(r);
					break;
				case 44:
					JoinerLights(r);
					break;
				case 45:
					JoinerRelief(r);
					break;
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET applying message " + id + ": " + ex.Message);
			}
		}

		public static long LocalBodyId()
		{
			try
			{
				Actor unit_main = ControllableUnit._unit_main;
				return (unit_main != null && unit_main.isAlive() && unit_main.data != null) ? unit_main.data.id : 0;
			}
			catch
			{
				return 0L;
			}
		}

		public static string WorldName()
		{
			try
			{
				string text = World.world?.map_stats?.name;
				return string.IsNullOrEmpty(text) ? "A world" : text;
			}
			catch
			{
				return "A world";
			}
		}

		public static string MapSizeName()
		{
			try
			{
				return MapBox.width + "x" + MapBox.height;
			}
			catch
			{
				return "";
			}
		}

		public static string AgeName()
		{
			try
			{
				string text = World.world.era_manager?.getCurrentAge()?.id;
				if (string.IsNullOrEmpty(text))
				{
					return "";
				}
				string text2 = (text.StartsWith("age_", StringComparison.Ordinal) ? text.Substring(4) : text);
				text2 = text2.Replace('_', ' ');
				return "Age of " + CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text2);
			}
			catch
			{
				return "";
			}
		}

		private static bool FxFar(string id)
		{
			if (!id.Contains("explosion") && !id.Contains("nuke") && !id.Contains("meteor") && !id.Contains("lightning") && !id.Contains("antimatter") && !id.Contains("napalm") && !id.Contains("fireworks") && !id.Contains("flash"))
			{
				return id.Contains("thunder");
			}
			return true;
		}

		private static bool IsMover(BaseEffect e)
		{
			if (!(e is Cloud) && !(e is TornadoEffect) && !(e is Santa))
			{
				return e is Boulder;
			}
			return true;
		}

		private static void ResetEffects()
		{
			_fxWas.Clear();
			_fxNow.Clear();
			_dropsWas.Clear();
			_dropsNow.Clear();
			_moverKeys.Clear();
			_movers.Clear();
			_fxPrimed = false;
			_joinerMovers.Clear();
			_localFx.Clear();
		}

		private static void HostEffects()
		{
			if (_known.Count == 0)
			{
				_fxPrimed = false;
				return;
			}
			try
			{
				ScanEffects();
				ScanDrops();
				if (!_fxPrimed)
				{
					_fxPrimed = true;
					_fxNew.Clear();
					_dropsNew.Clear();
				}
				if (_fxNew.Count > 0)
				{
					SendFx();
				}
				if (_dropsNew.Count > 0)
				{
					SendDrops();
				}
				if (Now - _moversSentAt >= 0.1f)
				{
					_moversSentAt = Now;
					SendMovers();
				}
			}
			catch (Exception ex)
			{
				if (_jsonErrors++ < 5)
				{
					Log.Error("NET effects: " + ex.Message);
				}
			}
		}

		private static void ScanEffects()
		{
			_fxNew.Clear();
			_movers.Clear();
			_fxNow.Clear();
			StackEffects stack_effects = World.world.stack_effects;
			if (stack_effects?.list == null)
			{
				return;
			}
			foreach (BaseEffectController item in stack_effects.list)
			{
				if (item?._list == null || item.asset == null || item == stack_effects.controller_slash_effects)
				{
					continue;
				}
				string id = item.asset.id;
				if (id == null || FxSkip.Contains(id))
				{
					continue;
				}
				int num = Math.Min(item._active_index, item._list.Count);
				for (int i = 0; i < num; i++)
				{
					BaseEffect baseEffect = item._list[i];
					if (baseEffect == null || !baseEffect.active || baseEffect.attachedToActor != null)
					{
						continue;
					}
					_fxNow.Add(baseEffect);
					bool flag = !_fxWas.Contains(baseEffect);
					if (IsMover(baseEffect))
					{
						if (flag || !_moverKeys.ContainsKey(baseEffect))
						{
							_moverKeys[baseEffect] = _nextMoverKey++;
						}
						_movers.Add(baseEffect);
					}
					else if (flag && (!(id == "fx_fire_smoke") || !OnMeteorSlide(baseEffect.transform.position)))
					{
						_fxNew.Add(baseEffect);
					}
				}
			}
			HashSet<BaseEffect> fxWas = _fxWas;
			_fxWas = _fxNow;
			_fxNow = fxWas;
			if (_moverKeys.Count <= _movers.Count + 16)
			{
				return;
			}
			List<BaseEffect> list = new List<BaseEffect>();
			foreach (BaseEffect key in _moverKeys.Keys)
			{
				if (!_fxWas.Contains(key))
				{
					list.Add(key);
				}
			}
			foreach (BaseEffect item2 in list)
			{
				_moverKeys.Remove(item2);
			}
		}

		internal static bool OnMeteorSlide(Vector3 p)
		{
			BaseEffectController baseEffectController = World.world?.stack_effects?.get("fx_meteorite");
			if (baseEffectController?._list == null)
			{
				return false;
			}
			int num = Math.Min(baseEffectController._active_index, baseEffectController._list.Count);
			for (int i = 0; i < num; i++)
			{
				if (!(baseEffectController._list[i] is Meteorite meteorite) || !((BaseEffect)meteorite).active)
				{
					continue;
				}
				Vector2 vector = meteorite.transform.position;
				Vector2 current_position = meteorite.current_position;
				if (!(current_position.sqrMagnitude < 1f))
				{
					Vector2 normalized = current_position.normalized;
					Vector2 lhs = (Vector2)p - vector;
					float num2 = Vector2.Dot(lhs, normalized);
					float num3 = Mathf.Abs(lhs.x * normalized.y - lhs.y * normalized.x);
					if (num2 > -4f && num2 < 280f && num3 < 5f)
					{
						return true;
					}
				}
			}
			return false;
		}

		private static void ScanDrops()
		{
			_dropsNew.Clear();
			_dropsNow.Clear();
			DropManager drop_manager = World.world.drop_manager;
			if (drop_manager?._drops == null)
			{
				return;
			}
			int num = Math.Min(drop_manager._activeIndex, drop_manager._drops.Count);
			for (int i = 0; i < num; i++)
			{
				Drop drop = drop_manager._drops[i];
				if (!(drop == null) && drop.active && !drop._landed && drop._asset != null)
				{
					_dropsNow.Add(drop);
					if (!_dropsWas.Contains(drop))
					{
						_dropsNew.Add(drop);
					}
				}
			}
			HashSet<Drop> dropsWas = _dropsWas;
			_dropsWas = _dropsNow;
			_dropsNow = dropsWas;
		}

		private static void SendFx()
		{
			foreach (KeyValuePair<int, Known> item in _known)
			{
				NetPlayer netPlayer = PlayerById(item.Key);
				if (netPlayer?.Link == null || !netPlayer.Ready)
				{
					continue;
				}
				Vector2 vector = FocusOf(netPlayer, item.Value);
				_fxFor.Clear();
				foreach (BaseEffect item2 in _fxNew)
				{
					if (_fxFor.Count >= 80)
					{
						break;
					}
					if (FxFar(item2.controller.asset.id) || ((Vector2)item2.transform.position - vector).sqrMagnitude <= 10000f)
					{
						_fxFor.Add(item2);
					}
				}
				if (_fxFor.Count == 0)
				{
					continue;
				}
				W.Reset();
				W.VarU(40uL);
				W.U16(_fxFor.Count);
				foreach (BaseEffect item3 in _fxFor)
				{
					WriteFx(item3, item3.controller.asset.id, item3.transform.position);
				}
				netPlayer.Link.Send(W, Delivery.Reliable);
			}
		}

		private static void WriteFx(BaseEffect e, string id, Vector3 p)
		{
			byte b = (byte)((e is Meteorite) ? 1 : ((e is NukeFlash) ? 2 : ((e is AntimatterBombEffect) ? 3 : ((e is Fireworks) ? 4 : 0))));
			W.U8(b);
			W.Str(id);
			W.Pos(p.x);
			W.Pos(p.y);
			Transform transform = e.transform;
			W.F32(transform.localScale.x);
			W.F32(transform.eulerAngles.z);
			W.Bool(e.sprite_renderer != null && e.sprite_renderer.flipX);
			switch (b)
			{
			case 1:
			{
				Meteorite meteorite = (Meteorite)e;
				W.VarI((meteorite.tile != null) ? meteorite.tile.x : ((int)p.x));
				W.VarI((meteorite.tile != null) ? meteorite.tile.y : ((int)p.y));
				W.F32(meteorite.current_position.x);
				W.F32(meteorite.current_position.y);
				W.F32(meteorite._falling_speed);
				W.Str(meteorite.terraform_asset ?? "");
				break;
			}
			case 2:
			{
				NukeFlash nukeFlash = (NukeFlash)e;
				W.Str((nukeFlash._terraform_asset != null) ? nukeFlash._terraform_asset.id : "");
				break;
			}
			}
		}

		private static void SendDrops()
		{
			W.Reset();
			W.VarU(41uL);
			int num = Math.Min(_dropsNew.Count, 120);
			W.U16(num);
			for (int i = 0; i < num; i++)
			{
				Drop drop = _dropsNew[i];
				W.Str(drop._asset.id);
				W.Pos(drop.current_position.x);
				W.Pos(drop.current_position.y);
				W.F32(drop._currentHeightZ);
				W.F32(drop._scale);
				W.F32(drop._falling_speed);
				W.Bool(drop._parabolic);
				if (drop._parabolic)
				{
					W.Pos(drop._startPosition.x);
					W.Pos(drop._startPosition.y);
					W.Pos(drop._targetPosition.x);
					W.Pos(drop._targetPosition.y);
					W.F32(drop._targetHeight);
					W.F32(drop._timeToTarget);
					W.F32(drop._timeInAir);
				}
			}
			foreach (KeyValuePair<int, Known> item in _known)
			{
				NetPlayer netPlayer = PlayerById(item.Key);
				if (netPlayer?.Link != null && netPlayer.Ready)
				{
					netPlayer.Link.Send(W, Delivery.Reliable);
				}
			}
		}

		private static void SendMovers()
		{
			W.Reset();
			W.VarU(42uL);
			int num = Math.Min(_movers.Count, 200);
			W.U16(num);
			for (int i = 0; i < num; i++)
			{
				BaseEffect baseEffect = _movers[i];
				W.VarU(_moverKeys.TryGetValue(baseEffect, out var value) ? value : 0u);
				byte v = (byte)((!(baseEffect is Cloud)) ? ((baseEffect is TornadoEffect) ? 1 : ((baseEffect is Santa) ? 2 : 3)) : 0);
				W.U8(v);
				W.Str(baseEffect.controller.asset.id);
				Vector3 vector = baseEffect.transform.position;
				float v2 = 0f;
				int num2 = baseEffect.state;
				if (baseEffect is Boulder boulder)
				{
					vector = boulder.current_position;
					v2 = boulder.position_height;
					num2 = boulder._bounces_left;
				}
				W.Pos(vector.x);
				W.Pos(vector.y);
				W.F32(v2);
				W.F32(baseEffect.transform.localScale.x);
				W.F32((baseEffect is TornadoEffect tornadoEffect) ? ((BaseEffect)tornadoEffect).scale : baseEffect.scale);
				W.F32(baseEffect.alpha);
				W.VarI(num2);
				W.Bool(baseEffect.sprite_renderer != null && baseEffect.sprite_renderer.flipX);
				W.Str((baseEffect is Cloud cloud && cloud.asset != null) ? cloud.asset.id : "");
			}
			foreach (KeyValuePair<int, Known> item in _known)
			{
				NetPlayer netPlayer = PlayerById(item.Key);
				if (netPlayer?.Link != null && netPlayer.Ready)
				{
					netPlayer.Link.Send(W, Delivery.Unreliable);
				}
			}
		}

		private static void NoteLocalFx(string id, Vector2 p)
		{
			if (!string.IsNullOrEmpty(id))
			{
				if (_localFx.Count > 64)
				{
					_localFx.RemoveRange(0, 32);
				}
				_localFx.Add(new LocalFx
				{
					Id = id,
					P = p,
					At = Now
				});
			}
		}

		private static bool PlayedHere(string id, Vector2 p)
		{
			for (int num = _localFx.Count - 1; num >= 0; num--)
			{
				LocalFx localFx = _localFx[num];
				if (!(Now - localFx.At > ((id == "fx_explosion_meteorite") ? 5f : 1.5f)) && localFx.Id == id && (localFx.P - p).sqrMagnitude < 6.25f)
				{
					_localFx.RemoveAt(num);
					return true;
				}
			}
			return false;
		}

		private static BaseEffect NewEffect(string id)
		{
			return World.world.stack_effects.get(id)?.spawnNew();
		}

		private static void JoinerFx(NetReader r)
		{
			int num = r.U16();
			for (int i = 0; i < num; i++)
			{
				byte b = (byte)r.U8();
				string text = r.Str();
				Vector2 vector = new Vector2(r.Pos(), r.Pos());
				float num2 = r.F32();
				float num3 = r.F32();
				bool flipX = r.Bool();
				int pX = 0;
				int pY = 0;
				float x = 0f;
				float y = 0f;
				float num4 = 0f;
				string text2 = null;
				switch (b)
				{
				case 1:
					pX = (int)r.VarI();
					pY = (int)r.VarI();
					x = r.F32();
					y = r.F32();
					num4 = r.F32();
					text2 = r.Str();
					break;
				case 2:
					text2 = r.Str();
					break;
				}
				if (string.IsNullOrEmpty(text) || PlayedHere(text, vector) || ((text == "fx_explosion_meteorite" || text == "fx_explosion_wave") && MeteorFallingNear(vector)))
				{
					continue;
				}
				BaseEffect baseEffect = null;
				try
				{
					baseEffect = NewEffect(text);
					if (baseEffect == null)
					{
						continue;
					}
					WorldTile worldTile = ((b == 1) ? World.world.GetTile(pX, pY) : World.world.GetTile((int)vector.x, (int)vector.y));
					switch (b)
					{
					case 1:
						if (baseEffect is Meteorite meteorite && worldTile != null)
						{
							meteorite.spawnOn(worldTile, string.IsNullOrEmpty(text2) ? "meteorite" : text2, null);
							meteorite.current_position = new Vector2(x, y);
							if (num4 > 0f)
							{
								meteorite._falling_speed = num4;
							}
							meteorite.updateMainSpritePos();
						}
						else
						{
							baseEffect.controller.killObject(baseEffect);
						}
						goto end_IL_00eb;
					case 2:
						if (baseEffect is NukeFlash nukeFlash && worldTile != null)
						{
							nukeFlash.spawnFlash(worldTile, string.IsNullOrEmpty(text2) ? "atomic_bomb" : text2);
							nukeFlash._bomb_spawned = true;
						}
						else
						{
							baseEffect.prepare(vector, num2);
						}
						break;
					case 3:
						baseEffect.prepare(vector, num2);
						if (baseEffect is AntimatterBombEffect antimatterBombEffect)
						{
							antimatterBombEffect.tile = worldTile;
							antimatterBombEffect.used = true;
						}
						break;
					case 4:
						if (worldTile != null)
						{
							baseEffect.spawnOnTile(worldTile);
						}
						else
						{
							baseEffect.prepare(vector, num2);
						}
						break;
					default:
						baseEffect.prepare(vector, num2);
						break;
					}
					baseEffect.tile = baseEffect.tile ?? worldTile;
					baseEffect.transform.position = new Vector3(vector.x, vector.y, baseEffect.transform.position.z);
					if (num2 > 0f)
					{
						baseEffect.setScale(num2);
					}
					if (Mathf.Abs(num3) > 0.01f)
					{
						baseEffect.transform.rotation = Quaternion.Euler(0f, 0f, num3);
					}
					if (baseEffect.sprite_renderer != null)
					{
						baseEffect.sprite_renderer.flipX = flipX;
					}
					EffectAsset asset = baseEffect.controller.asset;
					if (asset != null && !string.IsNullOrEmpty(asset.sound_launch))
					{
						MusicBox.playSound(asset.sound_launch, vector.x, vector.y);
					}
					end_IL_00eb:;
				}
				catch (Exception ex)
				{
					try
					{
						if (baseEffect != null && baseEffect.active)
						{
							baseEffect.controller.killObject(baseEffect);
						}
					}
					catch
					{
					}
					if (_applyErrors++ < 10)
					{
						Log.Error("NET effect " + text + ": " + ex.Message);
					}
				}
			}
		}

		private static void JoinerDrops(NetReader r)
		{
			int num = r.U16();
			DropManager drop_manager = World.world.drop_manager;
			for (int i = 0; i < num; i++)
			{
				string text = r.Str();
				Vector2 current_position = new Vector2(r.Pos(), r.Pos());
				float num2 = r.F32();
				float pScale = r.F32();
				float num3 = r.F32();
				bool flag = r.Bool();
				Vector2 startPosition = default(Vector2);
				Vector2 targetPosition = default(Vector2);
				float targetHeight = 0f;
				float timeToTarget = 0f;
				float timeInAir = 0f;
				if (flag)
				{
					startPosition = new Vector2(r.Pos(), r.Pos());
					targetPosition = new Vector2(r.Pos(), r.Pos());
					targetHeight = r.F32();
					timeToTarget = r.F32();
					timeInAir = r.F32();
				}
				try
				{
					DropAsset dropAsset = AssetManager.drops.get(text);
					WorldTile tile = World.world.GetTile((int)current_position.x, (int)current_position.y);
					if (dropAsset == null || tile == null || drop_manager == null)
					{
						continue;
					}
					DropsAction action_launch = dropAsset.action_launch;
					dropAsset.action_launch = null;
					Drop drop;
					try
					{
						drop = drop_manager.spawn(tile, dropAsset, num2, pScale, pForceSurprise: false, 0L);
					}
					finally
					{
						dropAsset.action_launch = action_launch;
					}
					if (!(drop == null))
					{
						drop.current_position = current_position;
						drop._currentHeightZ = num2;
						if (num3 > 0f)
						{
							drop._falling_speed = num3;
						}
						if (flag)
						{
							drop._parabolic = true;
							drop._startPosition = startPosition;
							drop._targetPosition = targetPosition;
							drop._targetHeight = targetHeight;
							drop._timeToTarget = timeToTarget;
							drop._timeInAir = timeInAir;
						}
						drop.updatePosition();
					}
				}
				catch (Exception ex)
				{
					if (_applyErrors++ < 10)
					{
						Log.Error("NET drop " + text + ": " + ex.Message);
					}
				}
			}
		}

		private static void JoinerMoversMessage(NetReader r)
		{
			int num = r.U16();
			float now = Now;
			for (int i = 0; i < num; i++)
			{
				uint num2 = (uint)r.VarU();
				byte b = (byte)r.U8();
				string text = r.Str();
				Vector2 vector = new Vector2(r.Pos(), r.Pos());
				float z = r.F32();
				float num3 = r.F32();
				float num4 = r.F32();
				float alpha = r.F32();
				int num5 = (int)r.VarI();
				bool flipX = r.Bool();
				string text2 = r.Str();
				if (num2 == 0 || string.IsNullOrEmpty(text))
				{
					continue;
				}
				BaseEffect baseEffect = null;
				try
				{
					if (_joinerMovers.TryGetValue(num2, out var value) && !(value.E == null) && value.E.active)
					{
						goto IL_01d9;
					}
					if (b != 3 && num5 == 2)
					{
						continue;
					}
					BaseEffect baseEffect2 = (baseEffect = NewEffect(text));
					if (baseEffect2 == null)
					{
						continue;
					}
					WorldTile pTile = World.world.GetTile((int)vector.x, (int)vector.y) ?? World.world.GetTile(0, 0);
					if (baseEffect2 is Cloud cloud && !string.IsNullOrEmpty(text2))
					{
						cloud.spawn(pTile, text2);
						cloud.setLifespan(1000000f);
					}
					else if (baseEffect2 is Santa santa)
					{
						santa.spawnOn(pTile);
					}
					else if (baseEffect2 is TornadoEffect tornadoEffect)
					{
						((BaseEffect)tornadoEffect).prepare(pTile, (num4 > 0f) ? num4 : 1f);
					}
					else
					{
						baseEffect2.prepare(vector, (num3 > 0f) ? num3 : 1f);
					}
					value = new JoinerMover
					{
						E = baseEffect2,
						P = vector,
						Z = z
					};
					_joinerMovers[num2] = value;
					Place(value, b, 1f);
					goto IL_01d9;
					IL_01d9:
					value.P = vector;
					value.Z = z;
					value.Seen = now;
					BaseEffect e = value.E;
					if (num3 > 0f)
					{
						e.transform.localScale = new Vector3(num3, num3, 1f);
					}
					if (e is TornadoEffect tornadoEffect2)
					{
						((BaseEffect)tornadoEffect2).scale = num4;
					}
					e.state = num5;
					if (e is Boulder boulder)
					{
						boulder._bounces_left = num5;
					}
					if (e is Cloud)
					{
						e.setAlpha(alpha);
					}
					if (e.sprite_renderer != null)
					{
						e.sprite_renderer.flipX = flipX;
					}
				}
				catch (Exception ex)
				{
					try
					{
						if (baseEffect != null && baseEffect.active)
						{
							baseEffect.controller.killObject(baseEffect);
						}
						if (baseEffect != null && _joinerMovers.TryGetValue(num2, out var value2) && value2.E == baseEffect)
						{
							_joinerMovers.Remove(num2);
						}
					}
					catch
					{
					}
					if (_applyErrors++ < 10)
					{
						Log.Error("NET mover " + text + ": " + ex.Message);
					}
				}
			}
		}

		private static void JoinerMovers()
		{
			if (_joinerMovers.Count == 0)
			{
				return;
			}
			float now = Now;
			float k = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
			_moversGone.Clear();
			foreach (KeyValuePair<uint, JoinerMover> joinerMover2 in _joinerMovers)
			{
				JoinerMover value = joinerMover2.Value;
				if (value.E == null || !value.E.active || now - value.Seen > 1.5f)
				{
					_moversGone.Add(joinerMover2.Key);
					continue;
				}
				try
				{
					Place(value, byte.MaxValue, k);
				}
				catch
				{
				}
			}
			foreach (uint item in _moversGone)
			{
				JoinerMover joinerMover = _joinerMovers[item];
				_joinerMovers.Remove(item);
				try
				{
					if (joinerMover.E != null && joinerMover.E.active)
					{
						joinerMover.E.controller.killObject(joinerMover.E);
					}
				}
				catch
				{
				}
			}
		}

		private static void Place(JoinerMover jm, byte type, float k)
		{
			BaseEffect e = jm.E;
			if (e is Boulder boulder)
			{
				boulder.current_position = Vector2.Lerp(boulder.current_position, jm.P, k);
				boulder.position_height = Mathf.Lerp(boulder.position_height, jm.Z, k);
				boulder.updateCurrentPosition();
			}
			else
			{
				Vector3 position = e.transform.position;
				Vector2 vector = Vector2.Lerp(position, jm.P, k);
				e.transform.position = new Vector3(vector.x, vector.y, position.z);
			}
		}

		private static void KillJoinerMovers()
		{
			foreach (JoinerMover value in _joinerMovers.Values)
			{
				try
				{
					if (value.E != null && value.E.active)
					{
						value.E.controller.killObject(value.E);
					}
				}
				catch
				{
				}
			}
			_joinerMovers.Clear();
		}

		private static void PictureDrops(bool on)
		{
			try
			{
				if (on && _dropLandHook == null)
				{
					_dropLandHook = Detour.Apply(typeof(Drop).GetMethod("land", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), typeof(WorldSync).GetMethod("PictureDropLand", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
					_meteorHook = Detour.Apply(typeof(Meteorite).GetMethod("explode", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), typeof(WorldSync).GetMethod("PictureMeteorStrike", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				}
				else if (!on && _dropLandHook != null)
				{
					ClearInFlight();
					Detour.Undo(_dropLandHook);
					Detour.Undo(_meteorHook);
					_dropLandHook = (_meteorHook = null);
					KillJoinerMovers();
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET drops as pictures: " + ex.Message);
			}
		}

		private static void ClearInFlight()
		{
			try
			{
				DropManager dropManager = World.world?.drop_manager;
				if (dropManager?._drops != null)
				{
					List<Drop> list = new List<Drop>();
					for (int i = 0; i < Math.Min(dropManager._activeIndex, dropManager._drops.Count); i++)
					{
						if (dropManager._drops[i] != null && dropManager._drops[i].active && !dropManager._drops[i]._landed)
						{
							list.Add(dropManager._drops[i]);
						}
					}
					foreach (Drop item in list)
					{
						item._landed = true;
						dropManager.killObject(item);
					}
				}
				BaseEffectController baseEffectController = World.world?.stack_effects?.get("fx_meteorite");
				if (baseEffectController?._list == null)
				{
					return;
				}
				List<BaseEffect> list2 = new List<BaseEffect>();
				for (int j = 0; j < Math.Min(baseEffectController._active_index, baseEffectController._list.Count); j++)
				{
					if (baseEffectController._list[j] != null && baseEffectController._list[j].active)
					{
						list2.Add(baseEffectController._list[j]);
					}
				}
				foreach (BaseEffect item2 in list2)
				{
					baseEffectController.killObject(item2);
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET clearing drops in flight: " + ex.Message);
			}
		}

		private static void PictureDropLand(Drop d)
		{
			try
			{
				d._landed = true;
				DropAsset asset = d._asset;
				if (asset != null && !string.IsNullOrEmpty(asset.sound_drop) && d.current_tile != null)
				{
					MusicBox.playSound(asset.sound_drop, d.current_tile);
				}
				World.world.drop_manager.landDrop(d);
			}
			catch
			{
			}
		}

		private static bool MeteorFallingNear(Vector2 p)
		{
			BaseEffectController baseEffectController = World.world?.stack_effects?.get("fx_meteorite");
			if (baseEffectController?._list == null)
			{
				return false;
			}
			for (int i = 0; i < Math.Min(baseEffectController._active_index, baseEffectController._list.Count); i++)
			{
				if (baseEffectController._list[i] is Meteorite meteorite && ((BaseEffect)meteorite).active && ((Vector2)meteorite.transform.position - p).sqrMagnitude < 64f)
				{
					return true;
				}
			}
			return false;
		}

		private static void PictureMeteorStrike(Meteorite m)
		{
			try
			{
				WorldTile tile = m.tile;
				if (tile != null)
				{
					Vector2 vector = new Vector2(tile.x, (float)tile.y - 2f);
					EffectsLibrary.spawnAt("fx_explosion_meteorite", vector, 1f);
					NoteLocalFx("fx_explosion_meteorite", vector);
				}
			}
			catch
			{
			}
			try
			{
				((BaseEffect)m).controller.killObject(m);
			}
			catch
			{
			}
		}

		internal static bool ForwardLight(Actor me, PlacedLights.Kind k, Vector2 at, float heading, PlacedLights.WallSpot? wall)
		{
			if (!Mirroring || me?.data == null || me.data.id != _myBody)
			{
				return false;
			}
			W.Reset();
			W.VarU(51uL);
			W.U8(7);
			W.U8(1);
			W.U8((byte)k);
			W.F32(at.x);
			W.F32(at.y);
			W.F32(heading);
			W.Bool(wall.HasValue);
			if (wall.HasValue)
			{
				PlacedLights.WallSpot value = wall.Value;
				W.F32(value.At.X);
				W.F32(value.At.Y);
				W.F32(value.At.Z);
				W.F32(value.Heading);
				W.VarI(value.Building);
				W.F32(value.Local.X);
				W.F32(value.Local.Y);
				W.F32(value.Local.Z);
				W.F32(value.LocalOut.X);
				W.F32(value.LocalOut.Y);
				W.F32(value.LocalOut.Z);
				W.Str(value.On ?? "");
				W.VarI(value.WallX);
				W.VarI(value.WallY);
			}
			_s.ToHost(W, Delivery.Reliable);
			LightsSent++;
			return true;
		}

		internal static bool ForwardTakeLight(Actor me, PlacedLights.Light l)
		{
			if (!Mirroring || me?.data == null || me.data.id != _myBody || l == null)
			{
				return false;
			}
			W.Reset();
			W.VarU(51uL);
			W.U8(7);
			W.U8(2);
			W.VarI(l.Id);
			W.U8((byte)l.Kind);
			_s.ToHost(W, Delivery.Reliable);
			LightsSent++;
			return true;
		}

		private static void HostLight(NetPlayer p, Actor body, NetReader r)
		{
			switch (r.U8())
			{
			case 2:
			{
				PlacedLights.Light light = PlacedLights.ById((int)r.VarI());
				PlacedLights.Kind kind2 = (PlacedLights.Kind)Mathf.Clamp(r.U8(), 0, 3);
				if (light != null && PlacedLights.MayTake(body, light))
				{
					PlacedLights.Remove(light);
					break;
				}
				RefuseLight(p, 2, light?.Kind ?? kind2, (light != null) ? "Not yours to take" : "Already gone");
				SendLights(p);
				break;
			}
			case 1:
			{
				PlacedLights.Kind kind = (PlacedLights.Kind)Mathf.Clamp(r.U8(), 0, 3);
				Vector2 vector = new Vector2(r.F32(), r.F32());
				float num = r.F32();
				PlacedLights.WallSpot? wall = null;
				if (r.Bool())
				{
					PlacedLights.WallSpot value = new PlacedLights.WallSpot
					{
						At = new Vec3(r.F32(), r.F32(), r.F32()),
						Heading = r.F32(),
						Building = r.VarI(),
						Local = new Vec3(r.F32(), r.F32(), r.F32()),
						LocalOut = new Vec3(r.F32(), r.F32(), r.F32())
					};
					string text = r.Str();
					value.On = (string.IsNullOrEmpty(text) ? null : text);
					value.WallX = (int)r.VarI();
					value.WallY = (int)r.VarI();
					wall = value;
					vector = new Vector2(value.At.X, value.At.Y);
				}
				if (float.IsNaN(vector.x) || float.IsNaN(vector.y) || float.IsNaN(num) || (vector - body.current_position).sqrMagnitude > 196f)
				{
					RefuseLight(p, 1, kind, "Too far away");
					break;
				}
				string text2 = ((kind != PlacedLights.Kind.Wall) ? PlacedLights.WhyNotAt(vector, kind) : ((PlacedLights.All.Count >= 3000) ? "Too many lights in this world" : null));
				if (text2 != null)
				{
					RefuseLight(p, 1, kind, text2);
					break;
				}
				PlacedLights.Add(body, kind, vector, num, wall);
				LightsPlacedForJoiners++;
				Log.Info("NET " + p?.ToString() + " set down a " + PlacedLights.Names[(int)kind].ToLowerInvariant());
				break;
			}
			}
		}

		private static void RefuseLight(NetPlayer p, byte op, PlacedLights.Kind k, string why)
		{
			LightsRefused++;
			Log.Info("NET " + p?.ToString() + "'s " + PlacedLights.Names[(int)k].ToLowerInvariant() + ((op == 2) ? " taken back" : " set down") + " refused: " + why);
			W.Reset();
			W.VarU(37uL);
			W.U8(4);
			W.U8(op);
			W.U8((byte)k);
			W.Str(why ?? "");
			p.Link?.Send(W, Delivery.Reliable);
		}

		private static void JoinerLightAnswer(NetReader r)
		{
			int num = r.U8();
			PlacedLights.Kind k = (PlacedLights.Kind)Mathf.Clamp(r.U8(), 0, 3);
			string text = r.Str();
			Actor actor = null;
			try
			{
				actor = ControllableUnit._unit_main;
			}
			catch
			{
			}
			if (actor?.data == null || actor.data.id != _myBody || !actor.isAlive())
			{
				return;
			}
			try
			{
				switch (num)
				{
				case 1:
					Bag.Put(actor, PlacedLights.ItemOf(k), 1, spill: true);
					break;
				case 2:
					Supplies.Take(actor, PlacedLights.ItemOf(k), 1);
					break;
				default:
					return;
				}
				LightsRefunded++;
				if (!string.IsNullOrEmpty(text))
				{
					Hub.Toast?.Invoke(text);
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET your light: " + ex.Message);
			}
		}

		private static void HostLights()
		{
			if (PlacedLights.Version != _lightsSent && !(Now - _lightsSentAt < 0.5f))
			{
				_lightsSent = PlacedLights.Version;
				_lightsSentAt = Now;
				W.Reset();
				W.VarU(44uL);
				W.Str(PlacedLights.Text());
				_s.ToAll(W, Delivery.Reliable);
			}
		}

		private static void SendLights(NetPlayer p)
		{
			if (p?.Link != null)
			{
				W.Reset();
				W.VarU(44uL);
				W.Str(PlacedLights.Text());
				p.Link.Send(W, Delivery.Reliable);
			}
		}

		private static void JoinerLights(NetReader r)
		{
			PlacedLights.FromHost(r.Str());
			LightListsIn++;
		}

		private static List<MetaKind> MetaKinds()
		{
			if (_meta != null && _meta.Count > 0 && _meta[0].Manager == FieldOf(_meta[0].Field))
			{
				return _meta;
			}
			_meta = new List<MetaKind>();
			for (int i = 0; i < MetaFields.Length; i++)
			{
				try
				{
					object obj = FieldOf(MetaFields[i]);
					if (obj == null)
					{
						continue;
					}
					Type type = obj.GetType();
					Type type2 = type;
					while (type2 != null && (!type2.IsGenericType || !type2.GetGenericTypeDefinition().Name.StartsWith("CoreSystemManager", StringComparison.Ordinal)))
					{
						type2 = type2.BaseType;
					}
					if (!(type2 == null))
					{
						Type type3 = type2.GetGenericArguments()[0];
						Type type4 = type2.GetGenericArguments()[1];
						MetaKind metaKind = new MetaKind();
						metaKind.Kind = (byte)(i + 1);
						metaKind.Field = MetaFields[i];
						metaKind.Manager = obj;
						metaKind.DataType = type4;
						metaKind.LoadObject = type.GetMethod("loadObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { type4 }, null);
						metaKind.RemoveObject = type.GetMethod("removeObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { type3 }, null);
						metaKind.Get = type.GetMethod("get", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(long) }, null);
						metaKind.DataField = FindField(type3, "data");
						metaKind.Save = type3.GetMethod("save", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
						metaKind.IsAlive = type3.GetMethod("isAlive", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
						MetaKind metaKind2 = metaKind;
						if (!(metaKind2.LoadObject == null) && !(metaKind2.DataField == null))
						{
							_meta.Add(metaKind2);
						}
					}
				}
				catch (Exception ex)
				{
					Log.Error("NET meta " + MetaFields[i] + ": " + ex.Message);
				}
			}
			return _meta;
		}

		private static object FieldOf(string name)
		{
			try
			{
				return typeof(MapBox).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(World.world);
			}
			catch
			{
				return null;
			}
		}

		private static FieldInfo FindField(Type t, string name)
		{
			while (t != null)
			{
				FieldInfo field = t.GetField(name, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (field != null)
				{
					return field;
				}
				t = t.BaseType;
			}
			return null;
		}

		private static long IdOf(object data)
		{
			if (!(data is BaseSystemData baseSystemData))
			{
				return 0L;
			}
			return baseSystemData.id;
		}

		private static void ResetMeta()
		{
			_meta = null;
			_metaAt = -1f;
			_metaRound.Clear();
			_metaCursor = 0;
			_townRealm.Clear();
			_hostRealm.Clear();
		}

		private static void HostMeta(bool initial)
		{
			if (initial || Now - _metaAt >= 1f)
			{
				ScanMeta(initial);
			}
			if (initial || _known.Count == 0 || _metaRound.Count == 0)
			{
				return;
			}
			float now = Now;
			for (int i = 0; i < _metaRound.Count; i++)
			{
				if (!(Now - now < 0.001f))
				{
					break;
				}
				_metaCursor = (_metaCursor + 1) % _metaRound.Count;
				MetaEntry metaEntry = _metaRound[_metaCursor];
				if (now - metaEntry.T.HashedAt < 2f)
				{
					continue;
				}
				metaEntry.T.HashedAt = now;
				if (IdOf(metaEntry.K.DataField.GetValue(metaEntry.T.O)) == metaEntry.T.Id)
				{
					string json;
					uint num = HashMeta(metaEntry.K, metaEntry.T.O, out json);
					if (json != null && num != metaEntry.T.Hash)
					{
						metaEntry.T.Hash = num;
						SendMeta(metaEntry.K, metaEntry.T.Id, json);
					}
				}
			}
		}

		private static void ScanMeta(bool initial)
		{
			_metaAt = Now;
			int num = ++_metaStamp;
			bool flag = false;
			foreach (MetaKind item in MetaKinds())
			{
				List<TrackedM> list = null;
				try
				{
					foreach (object item2 in (IEnumerable)item.Manager)
					{
						if (item2 == null || (item.IsAlive != null && !(bool)item.IsAlive.Invoke(item2, null)))
						{
							continue;
						}
						long num2 = IdOf(item.DataField.GetValue(item2));
						if (num2 == 0L)
						{
							continue;
						}
						if (item.Tracked.TryGetValue(num2, out var value))
						{
							value.O = item2;
							value.Seen = num;
							continue;
						}
						value = new TrackedM
						{
							O = item2,
							Id = num2,
							Seen = num,
							HashedAt = Now
						};
						item.Tracked[num2] = value;
						flag = true;
						value.Hash = HashMeta(item, item2, out var json);
						if (!initial && _known.Count > 0 && json != null)
						{
							SendMeta(item, num2, json);
						}
					}
					foreach (TrackedM value2 in item.Tracked.Values)
					{
						if (value2.Seen != num)
						{
							(list ?? (list = new List<TrackedM>())).Add(value2);
						}
					}
				}
				catch (Exception ex)
				{
					if (_jsonErrors++ < 5)
					{
						Log.Error("NET meta scan " + item.Field + ": " + ex.Message);
					}
				}
				if (list == null)
				{
					continue;
				}
				flag = true;
				foreach (TrackedM item3 in list)
				{
					item.Tracked.Remove(item3.Id);
					if (_known.Count == 0)
					{
						continue;
					}
					W.Reset();
					W.VarU(39uL);
					W.U8(item.Kind);
					W.VarI(item3.Id);
					W.Str(null);
					foreach (KeyValuePair<int, Known> item4 in _known)
					{
						PlayerById(item4.Key)?.Link?.Send(W, Delivery.Reliable);
					}
				}
			}
			if (!flag && _metaRound.Count > 0)
			{
				return;
			}
			_metaRound.Clear();
			foreach (MetaKind item5 in MetaKinds())
			{
				foreach (TrackedM value3 in item5.Tracked.Values)
				{
					_metaRound.Add(new MetaEntry
					{
						K = item5,
						T = value3
					});
				}
			}
			if (_metaCursor >= _metaRound.Count)
			{
				_metaCursor = 0;
			}
		}

		private static uint HashMeta(MetaKind k, object o, out string json)
		{
			json = null;
			try
			{
				k.Save?.Invoke(o, null);
				json = JsonConvert.SerializeObject(k.DataField.GetValue(o), SaveJson);
				return Fnv(json);
			}
			catch (Exception ex)
			{
				if (_jsonErrors++ < 5)
				{
					Log.Error("NET meta " + k.Field + " data: " + ex.Message);
				}
				return 0u;
			}
		}

		private static void SendMeta(MetaKind k, long id, string json)
		{
			W.Reset();
			W.VarU(39uL);
			W.U8(k.Kind);
			W.VarI(id);
			W.Str(json);
			foreach (KeyValuePair<int, Known> item in _known)
			{
				PlayerById(item.Key)?.Link?.Send(W, Delivery.Reliable);
			}
		}

		private static void JoinerMeta(NetReader r)
		{
			byte kind = (byte)r.U8();
			long num = r.VarI();
			string text = r.Str();
			MetaKind metaKind = MetaKinds().Find((MetaKind x) => x.Kind == kind);
			if (metaKind == null)
			{
				return;
			}
			try
			{
				object obj = ((metaKind.Get != null && num != 0L) ? metaKind.Get.Invoke(metaKind.Manager, new object[1] { num }) : null);
				if (text == null)
				{
					if (obj != null && metaKind.RemoveObject != null)
					{
						metaKind.RemoveObject.Invoke(metaKind.Manager, new object[1] { obj });
					}
					return;
				}
				object obj2 = JsonConvert.DeserializeObject(text, metaKind.DataType, SaveManager._settings);
				if (obj2 == null || (IdOf(obj2) != 0L && IdOf(obj2) != num))
				{
					return;
				}
				if (obj == null)
				{
					metaKind.LoadObject.Invoke(metaKind.Manager, new object[1] { obj2 });
					RelinkAfterMeta(metaKind, num);
					object obj3 = ((metaKind.Get != null) ? metaKind.Get.Invoke(metaKind.Manager, new object[1] { num }) : null);
					if (obj3 is City city)
					{
						_townRealm[num] = city.data.kingdomID;
						JoinerCityRealm(city);
						JoinerCityZones(city);
					}
					if (obj3 != null)
					{
						JoinerRuler(metaKind, num, obj3, RulerInData(metaKind, obj3));
					}
					return;
				}
				if (_baseM.ContainsKey((long)((ulong)metaKind.Kind << 56) | num))
				{
					PatchMetaIfChanged(metaKind.Field, num, obj);
				}
				object value = metaKind.DataField.GetValue(obj);
				if (value != null)
				{
					FieldInfo[] array = AllFields(metaKind.DataType);
					foreach (FieldInfo fieldInfo in array)
					{
						fieldInfo.SetValue(value, fieldInfo.GetValue(obj2));
					}
					if (obj is City city2)
					{
						_townRealm[num] = city2.data.kingdomID;
						JoinerCityRealm(city2);
					}
					JoinerRuler(metaKind, num, obj, RulerInData(metaKind, obj));
					RebaseMeta(metaKind, num, obj);
					if (metaKind.Field == "cities")
					{
						JoinerCityZones(obj);
					}
				}
			}
			catch (Exception ex)
			{
				if (_applyErrors++ < 10)
				{
					Log.Error("NET meta " + metaKind.Field + " " + num + ": " + (ex.InnerException ?? ex).Message);
				}
			}
		}

		private static void JoinerCityRealm(City c)
		{
			try
			{
				long id = c.data.id;
				if (!_townRealm.TryGetValue(id, out var value))
				{
					value = c.data.kingdomID;
				}
				Kingdom kingdom = ((value > 0) ? World.world.kingdoms.get(value) : null);
				if (value > 0 && kingdom == null)
				{
					_townRealm[id] = value;
					return;
				}
				_townRealm.Remove(id);
				if (kingdom == null)
				{
					kingdom = WildKingdomsManager.neutral;
					_hostRealm.Remove(id);
				}
				else
				{
					_hostRealm[id] = value;
				}
				Kingdom kingdom2 = c.kingdom;
				if (kingdom2 != kingdom)
				{
					c.setKingdom(kingdom, pFromLoad: false);
				}
				c.data.kingdomID = ((kingdom.data != null) ? kingdom.data.id : (-1));
				if (kingdom2 != WildKingdomsManager.neutral || kingdom == kingdom2)
				{
					return;
				}
				foreach (Building building in c.buildings)
				{
					if (building != null && building.kingdom == kingdom2)
					{
						building.setKingdom(kingdom);
					}
				}
			}
			catch (Exception ex)
			{
				if (_applyErrors++ < 10)
				{
					Log.Error("NET town " + c.data?.id + "'s realm: " + ex.Message);
				}
			}
		}

		private static void JoinerTownRealms()
		{
			if ((_townRealm.Count == 0 && _hostRealm.Count == 0) || Now - _townRealmsAt < 0.25f)
			{
				return;
			}
			_townRealmsAt = Now;
			foreach (long item in new List<long>(_townRealm.Keys))
			{
				City city = World.world.cities.get(item);
				if (city == null || !city.isAlive())
				{
					_townRealm.Remove(item);
				}
				else
				{
					JoinerCityRealm(city);
				}
			}
			foreach (KeyValuePair<long, long> item2 in new List<KeyValuePair<long, long>>(_hostRealm))
			{
				City city2 = World.world.cities.get(item2.Key);
				if (city2 == null || !city2.isAlive())
				{
					_hostRealm.Remove(item2.Key);
				}
				else if (city2.kingdom?.data == null || city2.kingdom.data.id != item2.Value)
				{
					_townRealm[item2.Key] = item2.Value;
					JoinerCityRealm(city2);
				}
			}
		}

		private static void RelinkAfterMeta(MetaKind k, long id)
		{
			if (k.Field != "cities" && k.Field != "kingdoms")
			{
				return;
			}
			try
			{
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple?.data != null && simple.isAlive())
					{
						if (k.Field == "cities" && simple.data.cityID == id && simple.city == null)
						{
							simple.setCity(World.world.cities.get(id));
						}
						if (k.Field == "kingdoms" && simple.data.civ_kingdom_id == id && (simple.kingdom == null || simple.kingdom.data.id != id))
						{
							simple.setKingdom(World.world.kingdoms.get(id));
						}
					}
				}
			}
			catch (Exception ex)
			{
				if (_applyErrors++ < 10)
				{
					Log.Error("NET relinking " + k.Field + " " + id + ": " + ex.Message);
				}
			}
			if (k.Field != "kingdoms")
			{
				return;
			}
			try
			{
				foreach (City item in World.world.cities.list)
				{
					if (item?.data != null && item.data.kingdomID == id && (item.kingdom == null || item.kingdom.data == null || item.kingdom.data.id != id))
					{
						JoinerCityRealm(item);
					}
				}
			}
			catch (Exception ex2)
			{
				if (_applyErrors++ < 10)
				{
					Log.Error("NET towns of realm " + id + ": " + ex2.Message);
				}
			}
		}

		private static void ResetPatches()
		{
			_baseA.Clear();
			_baseB.Clear();
			_baseM.Clear();
			_hostActors.Clear();
			_hostBuildings.Clear();
			_sentMeta.Clear();
			_myRange = 0L;
			_unacked.Clear();
			_patchSeq = 0;
			_waitLand.Clear();
		}

		private static void JoinerPrepareOwn()
		{
			ResetPatches();
			try
			{
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple?.data != null)
					{
						_hostActors.Add(simple.data.id);
					}
				}
				foreach (Building simple2 in World.world.buildings.getSimpleList())
				{
					if (simple2?.data != null)
					{
						_hostBuildings.Add(simple2.data.id);
					}
				}
				int num = ((_s?.Me != null) ? _s.Me.Id : 2);
				_myRange = (long)num << 40;
				long top = _myRange;
				long end = _myRange + 1099511627776L;
				Action<long> action = delegate(long id)
				{
					if (id >= top && id < end)
					{
						top = id + 1;
					}
				};
				foreach (Actor simple3 in World.world.units.getSimpleList())
				{
					if (simple3?.data != null)
					{
						action(simple3.data.id);
					}
				}
				foreach (Building simple4 in World.world.buildings.getSimpleList())
				{
					if (simple4?.data != null)
					{
						action(simple4.data.id);
					}
				}
				foreach (Item item in World.world.items)
				{
					if (item?.data != null)
					{
						action(item.data.id);
					}
				}
				foreach (MetaKind item2 in MetaKinds())
				{
					foreach (object item3 in (IEnumerable)item2.Manager)
					{
						if (item3 != null)
						{
							action(IdOf(item2.DataField.GetValue(item3)));
						}
					}
				}
				MapStats map_stats = World.world.map_stats;
				FieldInfo[] fields = typeof(MapStats).GetFields(BindingFlags.Instance | BindingFlags.Public);
				foreach (FieldInfo fieldInfo in fields)
				{
					if (fieldInfo.FieldType == typeof(long) && fieldInfo.Name.StartsWith("id_", StringComparison.Ordinal))
					{
						fieldInfo.SetValue(map_stats, Math.Max((long)fieldInfo.GetValue(map_stats), top));
					}
				}
				if (top > _myRange)
				{
					Log.Info("NET ids made here start at " + top + " (this world has " + (top - _myRange) + " from an earlier player " + num + ")");
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET preparing own ids: " + ex.Message);
			}
		}

		private static void JoinerPatches()
		{
			float now = Now;
			if (now - _patchAt < 0.3f)
			{
				return;
			}
			_patchAt = now;
			Actor actor = null;
			try
			{
				actor = ControllableUnit._unit_main;
			}
			catch
			{
			}
			if (actor == null || actor.data == null || actor.data.id != _myBody || !actor.isAlive())
			{
				return;
			}
			PruneUnacked(now);
			Vector2 current_position = actor.current_position;
			List<Actor> list = new List<Actor> { actor };
			try
			{
				Actor actor2 = WorldBoxMod.Instance?.Talk?.Speaker;
				if (actor2 != null && actor2 != actor)
				{
					list.Add(actor2);
				}
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple != actor && simple != actor2 && simple.isAlive() && (simple.current_position - current_position).sqrMagnitude < 144f)
					{
						list.Add(simple);
					}
				}
			}
			catch
			{
			}
			foreach (Actor item in list)
			{
				PatchActorIfChanged(item);
			}
			try
			{
				foreach (Building simple2 in World.world.buildings.getSimpleList())
				{
					if (simple2 != null && Standing(simple2) && (simple2.current_position - current_position).sqrMagnitude < 64f && simple2.asset != null && simple2.asset.type != "type_tree" && simple2.asset.type != "type_vegetation")
					{
						PatchBuildingIfChanged(simple2);
					}
				}
			}
			catch
			{
			}
			if (actor.city != null)
			{
				PatchMetaIfChanged("cities", actor.city.data?.id ?? 0, actor.city);
			}
			if (actor.kingdom != null)
			{
				PatchMetaIfChanged("kingdoms", actor.kingdom.data?.id ?? 0, actor.kingdom);
			}
			if (now - _createAt > 1f)
			{
				_createAt = now;
				JoinerCreations(actor);
			}
		}

		private static void PatchActorIfChanged(Actor a)
		{
			if (a?.data == null || !_hostActors.Contains(a.data.id))
			{
				return;
			}
			List<string> items;
			string text = ActorJson(a, out items);
			if (text != null)
			{
				long id = a.data.id;
				if (!_baseA.TryGetValue(id, out var value))
				{
					_baseA[id] = new Baseline
					{
						S = text
					};
				}
				else if (!(value.S == text))
				{
					SendFieldPatch(1, 0, id, value, text, items, id == _myBody);
				}
			}
		}

		private static void PatchBuildingIfChanged(Building bld)
		{
			if (bld?.data == null || !_hostBuildings.Contains(bld.data.id))
			{
				return;
			}
			string text = BuildingJson(bld);
			if (text != null)
			{
				long id = bld.data.id;
				if (!_baseB.TryGetValue(id, out var value))
				{
					_baseB[id] = new Baseline
					{
						S = text
					};
				}
				else if (!(value.S == text))
				{
					SendFieldPatch(2, 0, id, value, text, null, own: false);
				}
			}
		}

		private static void PatchMetaIfChanged(string field, long id, object o)
		{
			if (id == 0L)
			{
				return;
			}
			MetaKind metaKind = MetaKinds().Find((MetaKind x) => x.Field == field);
			if (metaKind == null)
			{
				return;
			}
			HashMeta(metaKind, o, out var json);
			if (json != null)
			{
				long key = (long)((ulong)metaKind.Kind << 56) | id;
				if (!_baseM.TryGetValue(key, out var value))
				{
					_baseM[key] = new Baseline
					{
						S = json
					};
				}
				else if (!(value.S == json))
				{
					SendFieldPatch(3, metaKind.Kind, id, value, json, null, own: false);
				}
			}
		}

		private static bool Theirs(string path)
		{
			int num = path.IndexOf('/');
			string text = ((num < 0) ? path : path.Substring(0, num));
			if (!text.StartsWith("custom_data", StringComparison.Ordinal))
			{
				switch (text)
				{
				default:
					return text == "resources";
				case "money":
				case "loot":
				case "inventory":
					break;
				}
			}
			return true;
		}

		private static void SendFieldPatch(byte what, byte metaKind, long id, Baseline b, string json, List<string> items, bool own)
		{
			JObject jObject;
			JObject was;
			try
			{
				jObject = JObject.Parse(json);
				was = b.J ?? JObject.Parse(b.S);
			}
			catch
			{
				b.S = json;
				b.J = null;
				return;
			}
			_ops.Clear();
			_opKinds.Clear();
			Diff("", was, jObject, top: true);
			b.S = json;
			b.J = jObject;
			if (!own)
			{
				for (int num = _ops.Count - 1; num >= 0; num--)
				{
					if (!Theirs(_ops[num].Key) && (what != 1 || !IsTownPath(_ops[num].Key)))
					{
						_ops.RemoveAt(num);
						_opKinds.RemoveAt(num);
					}
				}
			}
			if (_ops.Count == 0)
			{
				return;
			}
			int num2 = ++_patchSeq;
			if (what == 1)
			{
				if (!_unacked.TryGetValue(id, out var value))
				{
					value = (_unacked[id] = new List<Pending>());
				}
				value.Add(new Pending
				{
					Seq = num2,
					At = Now,
					Ops = new List<KeyValuePair<string, JToken>>(_ops),
					Kinds = new List<byte>(_opKinds)
				});
			}
			W.Reset();
			W.VarU(52uL);
			W.U8(0);
			W.U8(what);
			W.U8(metaKind);
			W.VarI(id);
			W.VarU((ulong)num2);
			W.VarU((ulong)_ops.Count);
			for (int i = 0; i < _ops.Count; i++)
			{
				W.Str(_ops[i].Key);
				W.U8(_opKinds[i]);
				W.Str(_ops[i].Value?.ToString(Formatting.None));
			}
			int num3 = items?.Count ?? 0;
			W.VarU((ulong)num3);
			for (int j = 0; j < num3; j++)
			{
				W.Str(items[j]);
			}
			_s.ToHost(W, Delivery.Reliable);
			if (_patchesLogged++ < 20)
			{
				string[] obj2 = new string[6] { "NET patch ", null, null, null, null, null };
				object obj3;
				switch (what)
				{
				default:
					obj3 = "meta " + metaKind;
					break;
				case 2:
					obj3 = "building";
					break;
				case 1:
					obj3 = "creature";
					break;
				}
				obj2[1] = (string)obj3;
				obj2[2] = " ";
				obj2[3] = id.ToString();
				obj2[4] = ": ";
				obj2[5] = PatchSummary();
				Log.Info(string.Concat(obj2));
			}
		}

		private static void JoinerPatchAck(NetReader r)
		{
			int seq = (int)r.VarU();
			foreach (List<Pending> value in _unacked.Values)
			{
				value.RemoveAll((Pending x) => x.Seq <= seq);
			}
		}

		private static string WithUnacked(long id, string json)
		{
			if (!_unacked.TryGetValue(id, out var value) || value.Count == 0)
			{
				return json;
			}
			try
			{
				JObject jObject = JObject.Parse(json);
				foreach (Pending item in value)
				{
					Apply(jObject, item.Ops, item.Kinds);
				}
				return jObject.ToString(Formatting.None);
			}
			catch
			{
				return json;
			}
		}

		internal static bool StaleDataKeepsChangeForTest(Actor me)
		{
			if (me?.data == null || me.data.id != _myBody)
			{
				return false;
			}
			PatchActorIfChanged(me);
			List<string> items;
			string text = ActorJson(me, out items);
			if (text == null)
			{
				return false;
			}
			me.data.get("fp_nettest_race", out var pResult, 0);
			me.data.set("fp_nettest_race", pResult + 1);
			PatchActorIfChanged(me);
			ApplyActorData(me, WithUnacked(me.data.id, text), items);
			RebaseActor(me);
			me.data.get("fp_nettest_race", out var pResult2, 0);
			return pResult2 == pResult + 1;
		}

		internal static void PatchForTest(Actor a, string key, string value)
		{
			if (a?.data != null && _hostActors.Contains(a.data.id))
			{
				_baseA.Remove(a.data.id);
				PatchActorIfChanged(a);
				a.data.set(key, value);
				PatchActorIfChanged(a);
			}
		}

		private static void PruneUnacked(float now)
		{
			if (_unacked.Count == 0)
			{
				return;
			}
			List<long> list = null;
			foreach (KeyValuePair<long, List<Pending>> item in _unacked)
			{
				item.Value.RemoveAll((Pending x) => now - x.At > 15f);
				if (item.Value.Count == 0)
				{
					(list ?? (list = new List<long>())).Add(item.Key);
				}
			}
			if (list == null)
			{
				return;
			}
			foreach (long item2 in list)
			{
				_unacked.Remove(item2);
			}
		}

		private static string PatchSummary()
		{
			List<string> list = new List<string>();
			for (int i = 0; i < _ops.Count && i < 6; i++)
			{
				list.Add(((_opKinds[i] == 1) ? "+" : ((_opKinds[i] == 2) ? "-" : "=")) + _ops[i].Key);
			}
			return string.Join(" ", list.ToArray()) + ((_ops.Count > 6) ? " ..." : "");
		}

		private static bool IsNumber(JToken t)
		{
			if (t != null)
			{
				if (t.Type != JTokenType.Integer)
				{
					return t.Type == JTokenType.Float;
				}
				return true;
			}
			return false;
		}

		private static void Diff(string path, JToken was, JToken now, bool top)
		{
			if (JToken.DeepEquals(was, now))
			{
				return;
			}
			if (was is JObject jObject && now is JObject jObject2)
			{
				HashSet<string> hashSet = new HashSet<string>();
				foreach (JProperty item in jObject.Properties())
				{
					hashSet.Add(item.Name);
				}
				foreach (JProperty item2 in jObject2.Properties())
				{
					hashSet.Add(item2.Name);
				}
				{
					foreach (string item3 in hashSet)
					{
						Diff((path == "") ? item3 : (path + "/" + item3), jObject[item3], jObject2[item3], top: false);
					}
					return;
				}
			}
			if ((top || path.IndexOf('/') < 0) && Additive.Contains(path) && (was == null || IsNumber(was)) && (now == null || IsNumber(now)))
			{
				double value = (now?.Value<double>() ?? 0.0) - (was?.Value<double>() ?? 0.0);
				_ops.Add(new KeyValuePair<string, JToken>(path, new JValue(value)));
				_opKinds.Add(1);
			}
			else if (now == null)
			{
				_ops.Add(new KeyValuePair<string, JToken>(path, null));
				_opKinds.Add(2);
			}
			else
			{
				_ops.Add(new KeyValuePair<string, JToken>(path, now));
				_opKinds.Add(0);
			}
		}

		private static void RebaseActor(Actor a)
		{
			if (a?.data != null && _baseA.ContainsKey(a.data.id))
			{
				List<string> items;
				string text = ActorJson(a, out items);
				if (text != null)
				{
					_baseA[a.data.id] = new Baseline
					{
						S = text
					};
				}
			}
		}

		private static void RebaseBuilding(long id)
		{
			if (_baseB.ContainsKey(id))
			{
				Building building = World.world.buildings.get(id);
				string text = ((building != null) ? BuildingJson(building) : null);
				if (text != null)
				{
					_baseB[id] = new Baseline
					{
						S = text
					};
				}
				else
				{
					_baseB.Remove(id);
				}
			}
		}

		private static void RebaseMeta(MetaKind k, long id, object o)
		{
			long key = (long)((ulong)k.Kind << 56) | id;
			if (_baseM.ContainsKey(key))
			{
				HashMeta(k, o, out var json);
				if (json != null)
				{
					_baseM[key] = new Baseline
					{
						S = json
					};
				}
			}
		}

		private static void JoinerCreations(Actor me)
		{
			try
			{
				foreach (MetaKind item2 in MetaKinds())
				{
					foreach (object item3 in (IEnumerable)item2.Manager)
					{
						if (item3 == null)
						{
							continue;
						}
						long num = IdOf(item2.DataField.GetValue(item3));
						if (num >= _myRange && _sentMeta.Add((long)((ulong)item2.Kind << 56) | num))
						{
							HashMeta(item2, item3, out var json);
							if (json != null)
							{
								SendCreate(3, num, json, null, item2.Kind);
							}
						}
					}
				}
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple?.data == null || !simple.isAlive() || simple.data.id < _myRange || _hostActors.Contains(simple.data.id))
					{
						continue;
					}
					_hostActors.Add(simple.data.id);
					simple.prepareForSave();
					string json2 = JsonConvert.SerializeObject(simple.data, SaveJson);
					List<string> list = new List<string>();
					if (simple.data.saved_items != null)
					{
						foreach (long saved_item in simple.data.saved_items)
						{
							Item item = World.world.items.get(saved_item);
							if (item?.data != null)
							{
								list.Add(JsonConvert.SerializeObject(item.data, SaveJson));
							}
						}
					}
					SendCreate(1, simple.data.id, json2, list, 0);
				}
				foreach (Building simple2 in World.world.buildings.getSimpleList())
				{
					if (simple2?.data != null && Standing(simple2) && simple2.data.id >= _myRange && !_hostBuildings.Contains(simple2.data.id))
					{
						_hostBuildings.Add(simple2.data.id);
						string text = BuildingJson(simple2);
						if (text != null)
						{
							SendCreate(2, simple2.data.id, text, null, 0);
						}
					}
				}
			}
			catch (Exception ex)
			{
				if (_applyErrors++ < 10)
				{
					Log.Error("NET sending what you made: " + ex.Message);
				}
			}
		}

		private static void SendCreate(byte what, long id, string json, List<string> items, byte metaKind)
		{
			W.Reset();
			W.VarU(52uL);
			W.U8(1);
			W.U8(what);
			W.U8(metaKind);
			W.VarI(id);
			W.Str(json);
			int num = items?.Count ?? 0;
			W.VarU((ulong)num);
			for (int i = 0; i < num; i++)
			{
				W.Str(items[i]);
			}
			_s.ToHost(W, Delivery.Reliable);
			object obj;
			switch (what)
			{
			default:
				obj = "meta " + metaKind + " ";
				break;
			case 2:
				obj = "building ";
				break;
			case 1:
				obj = "creature ";
				break;
			}
			Log.Info("NET made here, sent to the host: " + (string)obj + id);
		}

		private static void HostPatch(NetPlayer p, NetReader r)
		{
			byte b = (byte)r.U8();
			byte b2 = (byte)r.U8();
			byte metaKind = (byte)r.U8();
			long num = r.VarI();
			switch (b)
			{
			case 1:
			{
				string json = r.Str();
				List<string> items = ReadItems(r);
				if (num >= (long)p.Id << 40 && num < (long)(p.Id + 1) << 40)
				{
					HostCreate(p, b2, metaKind, num, json, items);
				}
				break;
			}
			case 0:
			{
				int num2 = (int)r.VarU();
				try
				{
					HostPatchFields(p, b2, metaKind, num, r);
					break;
				}
				finally
				{
					if (b2 == 1 && _tracked.TryGetValue(num, out var value))
					{
						value.Hash = 0u;
						value.HashedAt = -999f;
					}
					W.Reset();
					W.VarU(63uL);
					W.VarU((ulong)num2);
					p.Link?.Send(W, Delivery.Reliable);
				}
			}
			}
		}

		private static void HostPatchFields(NetPlayer p, byte what, byte metaKind, long id, NetReader r)
		{
			int num = r.Count(4096);
			List<KeyValuePair<string, JToken>> list = new List<KeyValuePair<string, JToken>>(num);
			List<byte> list2 = new List<byte>(num);
			for (int i = 0; i < num; i++)
			{
				string key = r.Str() ?? "";
				byte item = (byte)r.U8();
				string text = r.Str();
				JToken value = null;
				try
				{
					value = ((text == null) ? null : JToken.Parse(text));
				}
				catch
				{
				}
				list.Add(new KeyValuePair<string, JToken>(key, value));
				list2.Add(item);
			}
			List<string> list3 = ReadItems(r);
			if (!_remotes.TryGetValue(p.Id, out var value2) || value2.A == null || !value2.A.isAlive() || value2.A.data == null)
			{
				return;
			}
			Actor a = value2.A;
			bool flag = what == 1 && id != 0L && id == p.Body && id == a.data.id;
			if (!flag)
			{
				for (int num2 = list.Count - 1; num2 >= 0; num2--)
				{
					if (!Theirs(list[num2].Key) && (what != 1 || !IsTownPath(list[num2].Key)))
					{
						list.RemoveAt(num2);
						list2.RemoveAt(num2);
					}
				}
				if (list.Count == 0 && list3.Count == 0)
				{
					return;
				}
			}
			try
			{
				switch (what)
				{
				case 1:
				{
					Actor actor = World.world.units.get(id);
					if (actor == null || !actor.isAlive())
					{
						break;
					}
					if (!flag && !NearBody(a, actor.current_position))
					{
						RefusedPatch(p, "creature", id);
						break;
					}
					HostMembership(p, a, actor, list, list2, flag);
					if (list.Count != 0 || list3.Count != 0)
					{
						List<string> items;
						string text2 = ActorJson(actor, out items);
						if (text2 != null)
						{
							JObject jObject2 = JObject.Parse(text2);
							Apply(jObject2, list, list2);
							ApplyActorData(actor, jObject2.ToString(Formatting.None), list3);
						}
					}
					break;
				}
				case 2:
				{
					Building building = World.world.buildings.get(id);
					if (building == null || !Standing(building))
					{
						break;
					}
					if (!NearBody(a, building.current_position))
					{
						RefusedPatch(p, "building", id);
						break;
					}
					string text3 = BuildingJson(building);
					if (text3 != null)
					{
						JObject jObject3 = JObject.Parse(text3);
						Apply(jObject3, list, list2);
						JoinerBuildingData(id, jObject3.ToString(Formatting.None));
					}
					break;
				}
				case 3:
				{
					MetaKind metaKind2 = MetaKinds().Find((MetaKind x) => x.Kind == metaKind);
					object obj2 = metaKind2?.Get?.Invoke(metaKind2.Manager, new object[1] { id });
					if (obj2 == null)
					{
						break;
					}
					long num3 = ((a.city?.data != null) ? a.city.data.id : 0);
					long num4 = ((a.kingdom?.data != null) ? a.kingdom.data.id : 0);
					if ((!(metaKind2.Field == "cities") || id != num3) && (!(metaKind2.Field == "kingdoms") || id != num4))
					{
						RefusedPatch(p, metaKind2.Field, id);
						break;
					}
					HashMeta(metaKind2, obj2, out var json);
					if (json == null)
					{
						break;
					}
					JObject jObject = JObject.Parse(json);
					Apply(jObject, list, list2);
					object obj3 = JsonConvert.DeserializeObject(jObject.ToString(Formatting.None), metaKind2.DataType, SaveManager._settings);
					object value3 = metaKind2.DataField.GetValue(obj2);
					if (obj3 != null && value3 != null)
					{
						FieldInfo[] array = AllFields(metaKind2.DataType);
						foreach (FieldInfo fieldInfo in array)
						{
							fieldInfo.SetValue(value3, fieldInfo.GetValue(obj3));
						}
					}
					break;
				}
				}
			}
			catch (Exception ex)
			{
				if (_applyErrors++ < 10)
				{
					Log.Error("NET " + p?.ToString() + "'s change to " + id + ": " + ex.Message);
				}
			}
		}

		private static bool NearBody(Actor body, Vector2 at)
		{
			return (at - body.current_position).sqrMagnitude <= 225f;
		}

		private static void RefusedPatch(NetPlayer p, string what, long id)
		{
			if (PatchesRefused++ < 20)
			{
				Log.Info("NET " + p?.ToString() + "'s change to " + what + " " + id + " refused: not near their body");
			}
		}

		private static List<string> ReadItems(NetReader r)
		{
			int num = r.Count(256);
			List<string> list = new List<string>(num);
			for (int i = 0; i < num; i++)
			{
				list.Add(r.Str());
			}
			return list;
		}

		private static void Apply(JObject j, List<KeyValuePair<string, JToken>> ops, List<byte> kinds)
		{
			for (int i = 0; i < ops.Count; i++)
			{
				string[] array = ops[i].Key.Split('/');
				JObject jObject = j;
				for (int k = 0; k < array.Length - 1; k++)
				{
					if (jObject == null)
					{
						break;
					}
					JObject jObject2 = jObject[array[k]] as JObject;
					if (jObject2 == null)
					{
						jObject2 = new JObject();
						jObject[array[k]] = jObject2;
					}
					jObject = jObject2;
				}
				if (jObject != null)
				{
					string propertyName = array[array.Length - 1];
					switch (kinds[i])
					{
					case 2:
						jObject.Remove(propertyName);
						break;
					case 0:
						jObject[propertyName] = ops[i].Value;
						break;
					case 1:
					{
						double num = ((ops[i].Value != null) ? ops[i].Value.Value<double>() : 0.0);
						JToken jToken = jObject[propertyName];
						double num2 = ((jToken != null && IsNumber(jToken)) ? jToken.Value<double>() : 0.0);
						bool flag = jToken == null || jToken.Type == JTokenType.Integer;
						jObject[propertyName] = (flag ? new JValue((long)Math.Round(num2 + num)) : new JValue(num2 + num));
						break;
					}
					}
				}
			}
		}

		private static void HostCreate(NetPlayer p, byte what, byte metaKind, long id, string json, List<string> items)
		{
			try
			{
				switch (what)
				{
				case 3:
				{
					MetaKind metaKind2 = MetaKinds().Find((MetaKind x) => x.Kind == metaKind);
					if (metaKind2 != null && metaKind2.Get?.Invoke(metaKind2.Manager, new object[1] { id }) == null)
					{
						object obj = JsonConvert.DeserializeObject(json, metaKind2.DataType, SaveManager._settings);
						if (obj is BaseSystemData baseSystemData)
						{
							baseSystemData.id = id;
							metaKind2.LoadObject.Invoke(metaKind2.Manager, new object[1] { obj });
							Log.Info("NET " + p?.ToString() + " made " + metaKind2.Field + " " + id);
						}
					}
					break;
				}
				case 1:
				{
					if (World.world.units.get(id) != null)
					{
						break;
					}
					ActorData actorData = JsonConvert.DeserializeObject<ActorData>(json, SaveManager._settings);
					if (actorData != null)
					{
						actorData.id = id;
						LoadItems(items);
						Actor actor = World.world.units.loadObject(actorData);
						if (actor != null)
						{
							LinkLoaded(actor);
						}
						Log.Info("NET " + p?.ToString() + " made " + ((actor != null) ? WorldBoxMod.Describe(actor) : "a creature that would not load") + " (" + id + ")");
					}
					break;
				}
				case 2:
					{
						if (World.world.buildings.get(id) != null)
						{
							break;
						}
						BuildingData buildingData = JsonConvert.DeserializeObject<BuildingData>(json, SaveManager._settings);
						WorldTile worldTile = ((buildingData != null) ? World.world.GetTile(buildingData.mainX, buildingData.mainY) : null);
						if (worldTile != null)
						{
							if (worldTile.building != null && Standing(worldTile.building))
							{
								BuildingData data = worldTile.building.data;
								if ((data == null || data.id != id) && AssetStyle.IsSolid(worldTile.building))
								{
									goto IL_0250;
								}
							}
							JoinerBuildingData(id, json);
							Log.Info("NET " + p?.ToString() + " made building " + id);
							break;
						}
						goto IL_0250;
					}
					IL_0250:
					Log.Info("NET " + p?.ToString() + "'s building " + id + " has no room here");
					break;
				}
			}
			catch (Exception ex)
			{
				if (_applyErrors++ < 10)
				{
					Log.Error("NET " + p?.ToString() + "'s new object " + id + ": " + ex.Message);
				}
			}
		}

		internal static bool ForwardPlot(Actor me, City town, int tier, WorldTile t)
		{
			if (!Mirroring || me?.data == null || me.data.id != _myBody || town?.data == null || t == null)
			{
				return false;
			}
			W.Reset();
			W.VarU(51uL);
			W.U8(14);
			W.U8(1);
			W.VarI(town.data.id);
			W.U8((byte)Mathf.Clamp(tier, 0, 5));
			W.VarI(t.x);
			W.VarI(t.y);
			_s.ToHost(W, Delivery.Reliable);
			PlotsAsked++;
			return true;
		}

		internal static bool ForwardPlotHelp(Actor me, Building site)
		{
			if (!Mirroring)
			{
				return false;
			}
			if (me?.data == null || me.data.id != _myBody || site?.data == null)
			{
				return true;
			}
			W.Reset();
			W.VarU(51uL);
			W.U8(14);
			W.U8(2);
			W.VarI(site.data.id);
			_s.ToHost(W, Delivery.Reliable);
			return true;
		}

		private static void HostPlot(NetPlayer p, Actor body, NetReader r)
		{
			switch (r.U8())
			{
			case 2:
			{
				Building site = World.world.buildings.get(r.VarI());
				if (!Spend(p, 6, 4f, 3f))
				{
					Refused(p, "help at the site", "too many");
				}
				else if (LandPlot.HelpFor(body, site))
				{
					PlotHelpTaken++;
				}
				else
				{
					Refused(p, "help at the site", "not their site, or out of reach");
				}
				break;
			}
			case 1:
			{
				City town = World.world.cities.get(r.VarI());
				int tier = r.U8();
				WorldTile tile = World.world.GetTile((int)r.VarI(), (int)r.VarI());
				Building b = null;
				string text;
				if (tile == null || (new Vector2((float)tile.x + 0.5f, (float)tile.y + 0.5f) - body.current_position).sqrMagnitude > 576f)
				{
					text = "Too far";
				}
				else
				{
					try
					{
						text = LandPlot.RaiseFor(body, town, tier, tile, out b);
					}
					catch (Exception ex)
					{
						Log.Error("NET " + p?.ToString() + "'s plot: " + ex.Message);
						text = "The house can't stand there";
					}
				}
				if (text == null)
				{
					PlotsRaisedForJoiners++;
				}
				Log.Info("NET " + p?.ToString() + "'s plot (tier " + tier + ") " + ((text == null) ? ("raised: site " + b.data.id) : ("refused: " + text)));
				TellPlot(p, text ?? "Plot marked. The town's builders will raise it", text == null);
				break;
			}
			}
		}

		private static void TellPlot(NetPlayer p, string text, bool cheer)
		{
			if (p?.Link != null)
			{
				W.Reset();
				W.VarU(37uL);
				W.U8(5);
				W.Bool(cheer);
				W.Str(text ?? "");
				p.Link.Send(W, Delivery.Reliable);
			}
		}

		private static void JoinerPlotAnswer(NetReader r)
		{
			bool flag = r.Bool();
			string text = r.Str() ?? "";
			PlotAnswers++;
			LastPlotAnswer = text;
			if (text != "")
			{
				Hub.Toast?.Invoke(text);
			}
			Actor actor = null;
			try
			{
				actor = ControllableUnit._unit_main;
			}
			catch
			{
			}
			if (flag && actor != null && actor.isAlive())
			{
				Sounds.PlayAt("event:/SFX/UI/YesPoint", actor.current_position);
			}
			Log.Info("NET your plot: " + text);
		}

		private static void HostPlots()
		{
			if (Now - _plotsAt < 1f)
			{
				return;
			}
			_plotsAt = Now;
			foreach (Remote value in _remotes.Values)
			{
				if (value.A == null || value.P == null)
				{
					continue;
				}
				try
				{
					if (value.A.isAlive())
					{
						NetPlayer p = value.P;
						LandPlot.WatchFor(value.A, delegate(string text, bool cheer)
						{
							TellPlot(p, text, cheer);
						});
					}
				}
				catch (Exception ex)
				{
					if (_applyErrors++ < 10)
					{
						Log.Error("NET " + value.P?.ToString() + "'s plot: " + ex.Message);
					}
				}
			}
		}

		private static PowerHand Read()
		{
			PlayerControl player_control = World.world.player_control;
			return new PowerHand
			{
				ClickTimer = player_control._click_timer,
				UsedPower = player_control.already_used_power,
				WhisperA = Config.whisper_A,
				WhisperB = Config.whisper_B,
				UnityA = Config.unity_A,
				UnityB = Config.unity_B
			};
		}

		private static Kingdom Living(Kingdom k)
		{
			if (k == null || !k.isAlive())
			{
				return null;
			}
			return k;
		}

		private static void Put(PowerHand h)
		{
			PlayerControl player_control = World.world.player_control;
			h.WhisperA = Living(h.WhisperA);
			h.WhisperB = Living(h.WhisperB);
			h.UnityA = Living(h.UnityA);
			h.UnityB = Living(h.UnityB);
			player_control._click_timer = h.ClickTimer;
			player_control.already_used_power = h.UsedPower;
			Config.whisper_A = h.WhisperA;
			Config.whisper_B = h.WhisperB;
			Config.unity_A = h.UnityA;
			Config.unity_B = h.UnityB;
		}

		private static void AsOtherPlayer(NetPlayer p, string id, Action click)
		{
			if (!_hands.TryGetValue(p.Id, out var value) || value.Of != p)
			{
				Dictionary<int, PowerHand> hands = _hands;
				int id2 = p.Id;
				PowerHand obj = new PowerHand
				{
					Of = p
				};
				value = obj;
				hands[id2] = obj;
			}
			if (Now - value.LastAt > 0.4f || value.LastId != id)
			{
				value.ClickTimer = 0f;
			}
			if (value.LastId != id)
			{
				value.WhisperA = (value.WhisperB = (value.UnityA = (value.UnityB = null)));
			}
			value.LastAt = Now;
			value.LastId = id;
			PowerHand h = Read();
			Actor unit_main = ControllableUnit._unit_main;
			List<Actor> list = new List<Actor>(ControllableUnit._units);
			PowerButtonSelector selected_buttons = World.world.selected_buttons;
			PowerButton powerButton = ((selected_buttons != null) ? selected_buttons.selectedButton : null);
			Actor focus_unit = MoveCamera._focus_unit;
			PossessionUI instance = PossessionUI.instance;
			bool state_change_planned = instance != null && instance._state_change_planned;
			bool planned_state = instance != null && instance._planned_state;
			try
			{
				ControllableUnit._units.Clear();
				ControllableUnit._unit_main = null;
				if (selected_buttons != null)
				{
					selected_buttons.selectedButton = null;
				}
				Put(value);
				click();
			}
			finally
			{
				try
				{
					PowerHand powerHand = Read();
					value.ClickTimer = powerHand.ClickTimer;
					value.UsedPower = powerHand.UsedPower;
					value.WhisperA = powerHand.WhisperA;
					value.WhisperB = powerHand.WhisperB;
					value.UnityA = powerHand.UnityA;
					value.UnityB = powerHand.UnityB;
					Put(h);
					foreach (Actor unit in ControllableUnit._units)
					{
						if (unit != null && !list.Contains(unit))
						{
							try
							{
								unit.finishStatusEffect("possessed");
							}
							catch
							{
							}
						}
					}
					ControllableUnit._units.Clear();
					foreach (Actor item in list)
					{
						if (item != null && item.isAlive())
						{
							ControllableUnit._units.Add(item);
						}
					}
					ControllableUnit._unit_main = ((unit_main != null && unit_main.isAlive()) ? unit_main : null);
					if (ControllableUnit._unit_main == null && ControllableUnit._units.Count > 0)
					{
						ControllableUnit.trySelectNewMain();
					}
					if (selected_buttons != null && selected_buttons.selectedButton == null && powerButton != null)
					{
						selected_buttons.selectedButton = powerButton;
					}
					if (MoveCamera._focus_unit == null && focus_unit != null && focus_unit.isAlive())
					{
						MoveCamera._focus_unit = focus_unit;
					}
					if (instance != null)
					{
						instance._state_change_planned = state_change_planned;
						instance._planned_state = planned_state;
					}
				}
				catch (Exception ex)
				{
					Log.Error("NET giving the host's controls back after " + p?.ToString() + "'s power " + id + ": " + ex.Message);
				}
			}
		}

		private static bool LocalOnly(string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				return true;
			}
			if (id.EndsWith("_layer", StringComparison.Ordinal) || id.EndsWith("_edit", StringComparison.Ordinal))
			{
				return true;
			}
			switch (id)
			{
			case "hide_ui":
			case "inspect":
			case "city_select":
			case "follow_unit":
			case "clock":
			case "pause":
			case "relations":
			case "magnet":
				return true;
			default:
				return false;
			}
		}

		private static bool WorldBreaking(string id)
		{
			string[] array = new string[12]
			{
				"bomb", "czar", "antimatter", "acid", "grey_goo", "tsunami", "earthquake", "volcano", "crabzilla", "hellspawn",
				"meteor", "ufo"
			};
			foreach (string value in array)
			{
				if (id.Contains(value))
				{
					return true;
				}
			}
			return false;
		}

		private static void WrapPowers()
		{
			if (_wrapped.Count > 0)
			{
				return;
			}
			try
			{
				foreach (GodPower item in AssetManager.powers.list)
				{
					if (item == null || LocalOnly(item.id))
					{
						continue;
					}
					Wrapped wrapped = new Wrapped
					{
						Power = item,
						Special = item.click_special_action,
						Brush = item.click_brush_action,
						Click = item.click_action,
						PowerBrush = item.click_power_brush_action,
						PowerClick = item.click_power_action
					};
					string id = item.id;
					if (wrapped.Special != null)
					{
						item.click_special_action = (WorldTile tile, string _) => SendPower(id, tile);
					}
					if (wrapped.Brush != null)
					{
						item.click_brush_action = (WorldTile tile, string _) => SendPower(id, tile);
					}
					if (wrapped.Click != null)
					{
						item.click_action = (WorldTile tile, string _) => SendPower(id, tile);
					}
					if (wrapped.PowerBrush != null)
					{
						item.click_power_brush_action = (WorldTile tile, GodPower _) => SendPower(id, tile);
					}
					if (wrapped.PowerClick != null)
					{
						item.click_power_action = (WorldTile tile, GodPower _) => SendPower(id, tile);
					}
					_wrapped.Add(wrapped);
				}
				Log.Info("NET god powers go to the host (" + _wrapped.Count + ")");
			}
			catch (Exception ex)
			{
				Log.Error("NET wrapping powers: " + ex);
			}
		}

		private static void UnwrapPowers()
		{
			foreach (Wrapped item in _wrapped)
			{
				item.Power.click_special_action = item.Special;
				item.Power.click_brush_action = item.Brush;
				item.Power.click_action = item.Click;
				item.Power.click_power_brush_action = item.PowerBrush;
				item.Power.click_power_action = item.PowerClick;
			}
			_wrapped.Clear();
		}

		private static bool SendPower(string id, WorldTile tile)
		{
			if (tile == null || _s == null || !Mirroring)
			{
				return false;
			}
			if (_s.Rules.Powers == 2)
			{
				NoPowers();
				return false;
			}
			if (_s.Rules.Powers == 1 && WorldBreaking(id))
			{
				NoPowers();
				return false;
			}
			if (_sentFrame != Time.frameCount)
			{
				_sentFrame = Time.frameCount;
				_sentThisFrame.Clear();
			}
			if (!_sentThisFrame.Add(id + ":" + tile.x + ":" + tile.y))
			{
				return true;
			}
			W.Reset();
			W.VarU(54uL);
			W.Str(id);
			W.VarI(tile.x);
			W.VarI(tile.y);
			string s = "";
			try
			{
				s = Config.current_brush;
			}
			catch
			{
			}
			W.Str(s);
			WorldTile worldTile = null;
			try
			{
				worldTile = World.world.player_control.first_pressed_tile;
			}
			catch
			{
			}
			W.VarI(worldTile?.x ?? (-1));
			W.VarI(worldTile?.y ?? (-1));
			_s.ToHost(W, Delivery.Reliable);
			return true;
		}

		private static void NoPowers()
		{
			if (!(Now - _noPowersToldAt < 3f))
			{
				_noPowersToldAt = Now;
				WorldBoxMod.Instance?.Toast((_s.Rules.Powers == 2) ? "The host allows no god powers in this world." : "The host doesn't allow world-ending powers here.");
			}
		}

		private static void HostPower(NetPlayer p, NetReader r)
		{
			string text = r.Str() ?? "";
			int x = (int)r.VarI();
			int y = (int)r.VarI();
			string text2 = r.Str() ?? "";
			int num = -1;
			int pY = -1;
			if (r.More)
			{
				num = (int)r.VarI();
				pY = (int)r.VarI();
			}
			if (_s.Rules.Powers == 2 || (_s.Rules.Powers == 1 && WorldBreaking(text)) || LocalOnly(text))
			{
				return;
			}
			GodPower power = AssetManager.powers.get(text);
			if (power == null || World.world.GetTile(x, y) == null)
			{
				return;
			}
			string text3 = null;
			PlayerControl pc = World.world.player_control;
			WorldTile first_pressed_tile = pc.first_pressed_tile;
			TileType first_pressed_type = pc.first_pressed_type;
			TopTileType first_pressed_top_type = pc.first_pressed_top_type;
			try
			{
				text3 = Config.current_brush;
				if (!string.IsNullOrEmpty(text2) && Brush.get(text2) != null)
				{
					Config.current_brush = text2;
				}
				WorldTile worldTile = ((num >= 0) ? World.world.GetTile(num, pY) : null);
				if (worldTile != null)
				{
					pc.first_pressed_tile = worldTile;
					pc.first_pressed_type = worldTile.main_type;
					pc.first_pressed_top_type = worldTile.top_type;
				}
				if (text == "crabzilla")
				{
					HookHatch();
					CrabGuard.NoteForeign(x, y, p.Id);
				}
				AsOtherPlayer(p, text, delegate
				{
					pc.clickedFinal(new Vector2Int(x, y), power, pTrack: true);
				});
				if (power.force_map_mode == MetaType.City)
				{
					RushCities();
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET " + p?.ToString() + "'s power " + text + ": " + ex.Message);
			}
			finally
			{
				try
				{
					if (text3 != null)
					{
						Config.current_brush = text3;
					}
				}
				catch
				{
				}
				pc.first_pressed_tile = first_pressed_tile;
				pc.first_pressed_type = first_pressed_type;
				pc.first_pressed_top_type = first_pressed_top_type;
			}
		}

		internal static bool ForwardDig(Actor me, int x, int y, bool fill, float share, bool sand = false)
		{
			if (!Mirroring || me?.data == null || me.data.id != _myBody)
			{
				return false;
			}
			W.Reset();
			W.VarU(51uL);
			W.U8(8);
			W.VarU((ulong)x);
			W.VarU((ulong)y);
			W.U8(fill ? ((!sand) ? 1 : 2) : 0);
			W.F32(share);
			_s.ToHost(W, Delivery.Reliable);
			DigsSent++;
			return true;
		}

		private static void HostDig(NetPlayer p, Actor body, NetReader r)
		{
			int pX = (int)r.VarU();
			int pY = (int)r.VarU();
			int num = r.U8();
			bool fill = num != 0;
			bool sand = num == 2;
			float num2 = r.F32();
			if (!float.IsNaN(num2) && !(num2 <= 0f))
			{
				num2 = Mathf.Min(num2, 0.5f);
				WorldTile tile;
				try
				{
					tile = World.world.GetTile(pX, pY);
				}
				catch
				{
					return;
				}
				if (tile != null && !(((Vector2)tile.posV3 + new Vector2(0.5f, 0.5f) - body.current_position).sqrMagnitude > 36f) && ((_s?.Rules != null && _s.Rules.GuestsDig) || Earthworks.MayDig(body, tile, out var _)) && Earthworks.Apply(tile, fill, num2, Run.LockedOn(body), float.NaN, sand).Changed)
				{
					DigsForGuests++;
				}
			}
		}

		internal static bool ForwardDigTo(Actor me, int x, int y, bool fill, float share, float toward, bool sand = false)
		{
			if (!Mirroring || me?.data == null || me.data.id != _myBody)
			{
				return false;
			}
			W.Reset();
			W.VarU(51uL);
			W.U8(9);
			W.VarU((ulong)x);
			W.VarU((ulong)y);
			W.U8(fill ? ((!sand) ? 1 : 2) : 0);
			W.F32(share);
			W.F32(toward);
			_s.ToHost(W, Delivery.Reliable);
			DigsSent++;
			return true;
		}

		private static void HostDigTo(NetPlayer p, Actor body, NetReader r)
		{
			int pX = (int)r.VarU();
			int pY = (int)r.VarU();
			int num = r.U8();
			bool fill = num != 0;
			bool sand = num == 2;
			float num2 = r.F32();
			float num3 = r.F32();
			if (float.IsNaN(num2) || num2 <= 0f || float.IsNaN(num3) || float.IsInfinity(num3))
			{
				return;
			}
			WorldTile tile;
			try
			{
				tile = World.world.GetTile(pX, pY);
			}
			catch
			{
				return;
			}
			if (tile != null && !(((Vector2)tile.posV3 + new Vector2(0.5f, 0.5f) - body.current_position).sqrMagnitude > 49f) && ((_s?.Rules != null && _s.Rules.GuestsDig) || Earthworks.MayDig(body, tile, out var refused)))
			{
				bool capped = Run.LockedOn(body);
				if ((num2 >= 1f) ? (Earthworks.LevelTile(tile, fill, num3, capped, out var _, out refused, float.MaxValue, sand) > 0f) : Earthworks.Apply(tile, fill, Mathf.Min(num2, 0.5f), capped, num3, sand).Changed)
				{
					DigsForGuests++;
				}
			}
		}

		private static void HostRelief()
		{
			Relief current = ReliefWorld.Current;
			if (current == null || current.Width == 0 || _known.Count == 0 || current.Version == _reliefSent || Now - _reliefSentAt < 0.5f)
			{
				return;
			}
			int num = current.Width * current.Height;
			if (_sentEdit.Length != num)
			{
				SendReliefWhole(null);
				return;
			}
			_reliefSent = current.Version;
			_reliefSentAt = Now;
			int num2 = 0;
			for (int i = 0; i < num; i++)
			{
				if (Differs(current, i))
				{
					num2++;
				}
			}
			if (num2 == 0)
			{
				return;
			}
			if (num2 > 3000)
			{
				SendReliefWhole(null);
				return;
			}
			W.Reset();
			W.VarU(45uL);
			W.U8(1);
			W.VarU((ulong)num2);
			for (int j = 0; j < num; j++)
			{
				if (Differs(current, j))
				{
					Take(current, j, out var ev, out var rv, out var lv, out var lm, out var lt);
					W.VarU((ulong)j);
					W.VarI(ev);
					W.U8(rv);
					W.VarI(lv);
					W.VarU(lm);
					W.VarU(lt);
					_sentEdit[j] = ev;
					_sentRaw[j] = rv;
					_sentLevel[j] = lv;
					_sentMain[j] = lm;
					_sentTop[j] = lt;
				}
			}
			_s.ToAll(W, Delivery.Reliable);
		}

		private static void Take(Relief rel, int i, out short ev, out byte rv, out short lv, out ushort lm, out ushort lt)
		{
			short[] edit = rel.Edit;
			short[] lakeLevel = rel.LakeLevel;
			byte[] raw = rel.Raw;
			ushort[] lakeMain = rel.LakeMain;
			ushort[] lakeTop = rel.LakeTop;
			ev = (short)((edit != null) ? edit[i] : 0);
			rv = (byte)((raw != null) ? raw[i] : 0);
			lm = (ushort)((lakeMain != null) ? lakeMain[i] : 0);
			lt = (ushort)((lakeTop != null && lm != 0) ? lakeTop[i] : 0);
			lv = (short)((lakeLevel != null && lm != 0) ? lakeLevel[i] : 0);
		}

		private static bool Differs(Relief rel, int i)
		{
			Take(rel, i, out var ev, out var rv, out var lv, out var lm, out var lt);
			if (ev == _sentEdit[i] && rv >> 3 == _sentRaw[i] >> 3 && lv == _sentLevel[i] && lm == _sentMain[i])
			{
				return lt != _sentTop[i];
			}
			return true;
		}

		private static void SendReliefWhole(NetPlayer p)
		{
			Relief current = ReliefWorld.Current;
			if (current == null || current.Width == 0 || _s == null)
			{
				return;
			}
			int num = current.Width * current.Height;
			if (p == null)
			{
				_reliefSent = current.Version;
				_reliefSentAt = Now;
				if (_sentEdit.Length != num)
				{
					_sentEdit = new short[num];
					_sentRaw = new byte[num];
					_sentLevel = new short[num];
					_sentMain = new ushort[num];
					_sentTop = new ushort[num];
				}
				for (int i = 0; i < num; i++)
				{
					Take(current, i, out _sentEdit[i], out _sentRaw[i], out _sentLevel[i], out _sentMain[i], out _sentTop[i]);
				}
			}
			W.Reset();
			W.VarU(45uL);
			W.U8(0);
			W.VarI(ReliefWorld.Land);
			W.VarI(current.Seed);
			W.Str(current.Save(ReliefWorld.TypeName) ?? "");
			if (p != null)
			{
				p.Link?.Send(W, Delivery.Reliable);
			}
			else
			{
				_s.ToAll(W, Delivery.Reliable);
			}
		}

		private static void SendRelief(NetPlayer p)
		{
			if (p?.Link != null)
			{
				Relief current = ReliefWorld.Current;
				if (current != null && _sentEdit.Length != current.Width * current.Height)
				{
					SendReliefWhole(null);
				}
				else
				{
					SendReliefWhole(p);
				}
			}
		}

		internal static void SendLandToAll()
		{
			if (_s != null && NetSession.Hosting)
			{
				SendReliefWhole(null);
			}
		}

		private static void JoinerRelief(NetReader r)
		{
			Relief current = ReliefWorld.Current;
			ReliefIn++;
			switch (r.U8())
			{
			case 0:
			{
				int land = (int)r.VarI();
				int seed = (int)r.VarI();
				string record = r.Str();
				_held.Clear();
				ReliefWorld.FromHost(land, seed, record);
				break;
			}
			case 1:
			{
				bool flag = current == null || current.Width == 0;
				int num = (int)r.VarU();
				int num2 = (flag ? int.MaxValue : (current.Width * current.Height));
				for (int i = 0; i < num; i++)
				{
					int num3 = (int)r.VarU();
					short num4 = (short)Mathf.Clamp((int)r.VarI(), -32767, 32767);
					byte b = (byte)r.U8();
					short num5 = (short)Mathf.Clamp((int)r.VarI(), -32767, 32767);
					int main = (int)r.VarU();
					int top = (int)r.VarU();
					if (num3 >= 0 && num3 < num2)
					{
						if (flag)
						{
							_held[num3] = new HeldTile
							{
								Edit = num4,
								Raw = b,
								Level = num5,
								Main = main,
								Top = top
							};
						}
						else
						{
							current.SetEdit(num3, (float)num4 * (1f / 64f));
							current.SetRaw(num3, (float)(int)b / 255f);
							current.SetLake(num3, (float)num5 * (1f / 64f), main, top);
						}
					}
				}
				break;
			}
			}
		}

		internal static void ApplyHeldRelief(Relief rel)
		{
			if (rel == null || _held.Count == 0)
			{
				return;
			}
			int num = rel.Width * rel.Height;
			foreach (KeyValuePair<int, HeldTile> item in _held)
			{
				if (item.Key >= 0 && item.Key < num)
				{
					HeldTile value = item.Value;
					rel.SetEdit(item.Key, (float)value.Edit * (1f / 64f));
					rel.SetRaw(item.Key, (float)(int)value.Raw / 255f);
					rel.SetLake(item.Key, (float)value.Level * (1f / 64f), value.Main, value.Top);
				}
			}
			Log.Info($"NET relief: {_held.Count} tiles the host changed while this copy was on the map, applied");
			_held.Clear();
		}

		internal static void ForgetHeldRelief()
		{
			_held.Clear();
		}

		public static void AskResync()
		{
			NetSession current = NetSession.Current;
			if (current != null && !current.IsHost && CanResync)
			{
				_resyncing = true;
				_resyncAskedAt = Now;
				W.Reset();
				W.VarU(64uL);
				current.ToHost(W, Delivery.Reliable);
				Log.Info("NET resync asked");
				MpUi.Banner("Resyncing", "Getting the host's world again", bad: false, 6f);
			}
		}

		private static void JoinerResyncReset()
		{
			_worldIn = false;
			ResetTiles();
			ResetMeta();
			ResetPatches();
			ResetEffects();
			ResetActs();
			CrabFire.Clear();
			_rulerWait.Clear();
		}

		public static void HostResync(NetSession s, NetPlayer p)
		{
			if (p.Ready && (!_resyncAt.TryGetValue(p.Id, out var value) || !(Now - value < 13f)))
			{
				_resyncAt[p.Id] = Now;
				_transfers.RemoveAll((Transfer t) => t.Player == p);
				p.Ready = false;
				p.Resync = true;
				Log.Info("NET " + p?.ToString() + " asked for a resync");
				SendWorld(s, p);
			}
		}

		public static void HostResynced(NetPlayer p)
		{
			p.Resync = false;
			if (p.Body != 0L && _remotes.TryGetValue(p.Id, out var value) && value.A != null && value.A.isAlive())
			{
				Confirm(p, p.Body, "");
			}
		}

		private static long RulerInData(MetaKind k, object o)
		{
			try
			{
				if (k.Field == "kingdoms" && o is Kingdom kingdom)
				{
					return (kingdom.data != null) ? kingdom.data.kingID : (-1);
				}
				if (k.Field == "cities" && o is City city)
				{
					return (city.data != null) ? city.data.leaderID : (-1);
				}
			}
			catch
			{
			}
			return -1L;
		}

		private static void JoinerRuler(MetaKind k, long id, object o, long ruler)
		{
			if (k.Field != "kingdoms" && k.Field != "cities")
			{
				return;
			}
			long key = (long)((ulong)k.Kind << 56) | id;
			try
			{
				if (LinkRuler(o, ruler))
				{
					_rulerWait.Remove(key);
				}
				else
				{
					_rulerWait[key] = new KeyValuePair<long, float>(ruler, Now);
				}
			}
			catch (Exception ex)
			{
				_rulerWait.Remove(key);
				if (_applyErrors++ < 10)
				{
					Log.Error("NET ruler of " + k.Field + " " + id + ": " + ex.Message);
				}
			}
		}

		private static bool LinkRuler(object o, long ruler)
		{
			Actor actor = null;
			if (ruler > 0)
			{
				actor = World.world.units.get(ruler);
				if (actor == null || !actor.isAlive())
				{
					return false;
				}
			}
			if (o is Kingdom kingdom)
			{
				if (kingdom.king != actor)
				{
					kingdom.king = actor;
				}
			}
			else if (o is City city && city.leader != actor)
			{
				city.leader = actor;
			}
			return true;
		}

		private static void JoinerRulers()
		{
			if (_rulerWait.Count == 0 || Now - _rulersAt < 0.5f)
			{
				return;
			}
			_rulersAt = Now;
			_rulerKeys.Clear();
			_rulerKeys.AddRange(_rulerWait.Keys);
			foreach (long rulerKey in _rulerKeys)
			{
				KeyValuePair<long, float> keyValuePair = _rulerWait[rulerKey];
				byte kind = (byte)(rulerKey >> 56);
				long num = rulerKey & 0xFFFFFFFFFFFFFFL;
				MetaKind metaKind = MetaKinds().Find((MetaKind x) => x.Kind == kind);
				object obj = null;
				try
				{
					obj = ((metaKind?.Get != null) ? metaKind.Get.Invoke(metaKind.Manager, new object[1] { num }) : null);
				}
				catch
				{
				}
				bool flag = obj == null || Now - keyValuePair.Value > 60f;
				try
				{
					if (!flag)
					{
						flag = LinkRuler(obj, keyValuePair.Key);
					}
				}
				catch
				{
					flag = true;
				}
				if (flag)
				{
					_rulerWait.Remove(rulerKey);
				}
			}
		}

		private static void HostShots()
		{
			if (_known.Count == 0)
			{
				return;
			}
			_shotsNow.Clear();
			try
			{
				foreach (Projectile projectile in World.world.projectiles)
				{
					if (projectile?.data == null || projectile.asset == null)
					{
						continue;
					}
					long id = projectile.data.id;
					_shotsNow.Add(id);
					if (!_shotsSeen.Add(id))
					{
						continue;
					}
					long num = 0L;
					try
					{
						if (projectile.by_who is Actor actor && actor.data != null)
						{
							num = actor.data.id;
						}
					}
					catch
					{
					}
					W.Reset();
					W.VarU(38uL);
					W.Str(projectile.asset.id);
					Vector2 startVector = projectile.getStartVector();
					W.Pos(startVector.x);
					W.Pos(startVector.y);
					W.F32(projectile._current_position_3d.z);
					W.Pos(projectile._vector_target.x);
					W.Pos(projectile._vector_target.y);
					W.VarI(num);
					foreach (KeyValuePair<int, Known> item in _known)
					{
						NetPlayer netPlayer = PlayerById(item.Key);
						if (netPlayer?.Link != null && netPlayer.Ready && (num == 0L || netPlayer.Body != num))
						{
							netPlayer.Link.Send(W, Delivery.Reliable);
						}
					}
				}
			}
			catch (Exception ex)
			{
				if (_jsonErrors++ < 5)
				{
					Log.Error("NET shots: " + ex.Message);
				}
			}
			if (Now - _shotsPrunedAt > 2f)
			{
				_shotsPrunedAt = Now;
				_shotsSeen.IntersectWith(_shotsNow);
			}
		}

		private static void JoinerShot(NetReader r)
		{
			string text = r.Str();
			Vector3 pLaunchPosition = new Vector3(r.Pos(), r.Pos(), 0f);
			float pStartPosZ = r.F32();
			Vector3 pTargetPosition = new Vector3(r.Pos(), r.Pos(), 0f);
			long num = r.VarI();
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			try
			{
				Actor pInitiator = ((num != 0L) ? World.world.units.get(num) : null);
				World.world.projectiles.spawn(pInitiator, null, text, pLaunchPosition, pTargetPosition, 0f, pStartPosZ);
			}
			catch (Exception ex)
			{
				if (_applyErrors++ < 10)
				{
					Log.Error("NET shot " + text + ": " + ex.Message);
				}
			}
		}

		private static void PictureShots(bool on)
		{
			try
			{
				if (on && _landHook == null)
				{
					_landHook = Detour.Apply(typeof(Projectile).GetMethod("targetReached", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), typeof(WorldSync).GetMethod("PictureLanding", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
					_hitHook = Detour.Apply(typeof(ProjectileManager).GetMethod("checkCollision", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), typeof(WorldSync).GetMethod("PictureCollision", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				}
				else if (!on && _landHook != null)
				{
					Detour.Undo(_landHook);
					Detour.Undo(_hitHook);
					_landHook = (_hitHook = null);
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET projectiles as pictures: " + ex.Message);
			}
		}

		private static void PictureLanding(Projectile p)
		{
			try
			{
				p._is_target_reached = true;
				ProjectileAsset asset = p.asset;
				if (asset != null)
				{
					Vector2 transformedPositionWithHeight = p.getTransformedPositionWithHeight();
					if (!string.IsNullOrEmpty(asset.end_effect))
					{
						EffectsLibrary.spawnAt(asset.end_effect, transformedPositionWithHeight, asset.end_effect_scale);
						NoteLocalFx(asset.end_effect, transformedPositionWithHeight);
					}
					if (!string.IsNullOrEmpty(asset.sound_impact))
					{
						MusicBox.playSound(asset.sound_impact, transformedPositionWithHeight.x, transformedPositionWithHeight.y, pGameViewOnly: true);
					}
				}
				p.reset();
			}
			catch
			{
			}
		}

		private static void PictureCollision(ProjectileManager m)
		{
		}

		private static void LoadLastBodies()
		{
			_lastBody.Clear();
			_respawns.Clear();
			try
			{
				string pResult = null;
				World.world.map_stats.custom_data?.get("mp_bodies", out pResult, null);
				if (string.IsNullOrEmpty(pResult))
				{
					return;
				}
				string[] array = pResult.Split(';');
				foreach (string text in array)
				{
					int num = text.IndexOf('=');
					if (num > 0 && long.TryParse(text.Substring(num + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
					{
						_lastBody[text.Substring(0, num)] = result;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET reading last bodies: " + ex.Message);
			}
		}

		private static void SaveLastBodies()
		{
			try
			{
				if (World.world.map_stats.custom_data == null)
				{
					World.world.map_stats.custom_data = new SaveCustomData();
				}
				StringBuilder stringBuilder = new StringBuilder();
				foreach (KeyValuePair<string, long> item in _lastBody)
				{
					stringBuilder.Append(item.Key).Append('=').Append(item.Value.ToString(CultureInfo.InvariantCulture))
						.Append(';');
				}
				World.world.map_stats.custom_data.set("mp_bodies", stringBuilder.ToString());
			}
			catch (Exception ex)
			{
				Log.Error("NET saving last bodies: " + ex.Message);
			}
		}

		private static void RememberBody(NetPlayer p, long id)
		{
			if (!string.IsNullOrEmpty(p.Key) && !p.Host)
			{
				if (id == 0L)
				{
					_lastBody.Remove(p.Key);
				}
				else
				{
					_lastBody[p.Key] = id;
				}
				SaveLastBodies();
			}
		}

		private static long LastBodyOf(NetPlayer p)
		{
			if (string.IsNullOrEmpty(p.Key) || !_lastBody.TryGetValue(p.Key, out var value))
			{
				return 0L;
			}
			Actor actor = World.world.units.get(value);
			if (actor == null || !actor.isAlive() || IsOtherPlayersBody(actor) || actor == ControllableUnit._unit_main)
			{
				return 0L;
			}
			return value;
		}

		private static void GiveBody(NetPlayer p, Actor a)
		{
			ReleaseBody(p);
			Remote value = new Remote
			{
				P = p,
				A = a,
				Id = a.data.id
			};
			_remotes[p.Id] = value;
			p.Body = a.data.id;
			p.God = false;
			p.LastBody = 0L;
			Hold(a);
			RememberBody(p, a.data.id);
			Confirm(p, a.data.id, "");
			_s.SendPlayers();
			Log.Info("NET " + p?.ToString() + " is in " + WorldBoxMod.Describe(a));
		}

		private static Actor BornIn(City city, string asset = null)
		{
			if (city == null || !city.isAlive())
			{
				return null;
			}
			try
			{
				if (string.IsNullOrEmpty(asset))
				{
					asset = city.getActorAsset()?.id;
				}
				WorldTile tile = city.getTile();
				if (string.IsNullOrEmpty(asset) || tile == null)
				{
					return null;
				}
				Actor actor = World.world.units.spawnNewUnit(asset, tile, pSpawnSound: false, pMiracleSpawn: true, 0f, null, pGiveOwnerlessItems: false, pAdultAge: true);
				if (actor == null)
				{
					return null;
				}
				if (actor.city != city)
				{
					actor.joinCity(city);
				}
				return actor;
			}
			catch (Exception ex)
			{
				Log.Error("NET born in " + city.data?.id + ": " + ex.Message);
				return null;
			}
		}

		private static void HostBorn(NetPlayer p, long cityId)
		{
			City city = World.world.cities.get(cityId);
			Actor actor = BornIn(city);
			if (actor == null)
			{
				Refuse(p, "Could not be born there.");
				return;
			}
			GiveBody(p, actor);
			_s.System(p.Name + " was born in " + (city.data?.name ?? "a town"));
		}

		private static void HostDeath(NetPlayer p, Actor dead, long deadId, long cityId, string asset)
		{
			RememberBody(p, 0L);
			p.Body = 0L;
			p.God = true;
			int death = _s.Rules.Death;
			if (death == 2)
			{
				Actor actor = HeirOf(deadId, dead);
				if (actor != null)
				{
					GiveBody(p, actor);
					Confirm(p, actor.data.id, "You live on as " + actor.getName() + ".");
					return;
				}
			}
			if (death == 1 && World.world.cities.get(cityId) != null)
			{
				_respawns.Add(new Respawn
				{
					P = p,
					At = Now + (float)Mathf.Max(0, _s.Rules.RespawnSeconds),
					City = cityId,
					Asset = asset
				});
				Confirm(p, 0L, "You died. Born again in your town in " + _s.Rules.RespawnSeconds + " s.");
			}
			else
			{
				Confirm(p, 0L, "You died. Pick a new body.");
			}
		}

		private static Actor HeirOf(long deadId, Actor dead)
		{
			Actor result = null;
			int num = int.MinValue;
			try
			{
				long num2 = ((dead?.data != null) ? dead.data.lover : 0);
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple?.data != null && simple.isAlive() && simple.data.id != deadId && !IsOtherPlayersBody(simple) && simple != ControllableUnit._unit_main)
					{
						int num3 = int.MinValue;
						if (simple.data.parent_id_1 == deadId || simple.data.parent_id_2 == deadId)
						{
							num3 = 100 + (simple.isAdult() ? 50 : 0);
						}
						else if (simple.data.id == num2)
						{
							num3 = 60;
						}
						else if (dead?.data != null && dead.data.family != 0L && simple.data.family == dead.data.family)
						{
							num3 = 40;
						}
						if (num3 > num)
						{
							num = num3;
							result = simple;
						}
					}
				}
			}
			catch
			{
			}
			if (num <= int.MinValue)
			{
				return null;
			}
			return result;
		}

		private static void HostRespawns()
		{
			for (int num = _respawns.Count - 1; num >= 0; num--)
			{
				Respawn respawn = _respawns[num];
				if (!(Now < respawn.At))
				{
					_respawns.RemoveAt(num);
					if (respawn.P.Link != null && respawn.P.Link.Open && respawn.P.Body == 0L)
					{
						Actor actor = BornIn(World.world.cities.get(respawn.City), respawn.Asset);
						if (actor != null)
						{
							GiveBody(respawn.P, actor);
						}
						else
						{
							Confirm(respawn.P, 0L, "Your town is gone. Pick a new body.");
						}
					}
				}
			}
		}

		public static void AskBorn(City city)
		{
			NetSession current = NetSession.Current;
			if (current == null || city?.data == null)
			{
				return;
			}
			if (current.IsHost)
			{
				Actor actor = BornIn(city);
				if (actor != null)
				{
					ControllableUnit.setControllableCreature(actor);
				}
			}
			else
			{
				W.Reset();
				W.VarU(53uL);
				W.U8(2);
				W.VarI(city.data.id);
				current.ToHost(W, Delivery.Reliable);
			}
		}

		public static void AskLastBody()
		{
			NetSession current = NetSession.Current;
			if (current != null && !current.IsHost)
			{
				W.Reset();
				W.VarU(53uL);
				W.U8(3);
				W.VarI(0L);
				current.ToHost(W, Delivery.Reliable);
			}
		}

		private static void ResetTiles()
		{
			_tileSig = null;
			_tileCursor = 0;
			_changedTiles.Clear();
		}

		private static long TileSig(WorldTile t, bool[] fires)
		{
			TileTypeBase main_type = t.main_type;
			TileTypeBase top_type = t.top_type;
			long num = ((main_type != null) ? (main_type.index_id + 1) : 0) & 0xFFF;
			num |= (long)(((top_type != null) ? (top_type.index_id + 1) : 0) & 0xFFF) << 12;
			WorldTileData data = t.data;
			if (data != null)
			{
				num |= (long)(data.height & 0xFFFF) << 24;
				int tile_id = data.tile_id;
				if (fires != null && tile_id >= 0 && tile_id < fires.Length && fires[tile_id])
				{
					num |= 0x1000000000000000L;
				}
				if (data.frozen)
				{
					num |= 0x2000000000000000L;
				}
			}
			return num | ((long)(t.burned_stages & 0xFF) << 40);
		}

		private static void HostTiles()
		{
			WorldTile[] tiles_list;
			bool[] array;
			try
			{
				tiles_list = World.world.tiles_list;
				array = World.world.tile_manager?.fires;
			}
			catch
			{
				return;
			}
			if (tiles_list == null || tiles_list.Length == 0)
			{
				return;
			}
			if (_tileSig == null || _tileSig.Length != tiles_list.Length)
			{
				_tileSig = new long[tiles_list.Length];
				for (int i = 0; i < tiles_list.Length; i++)
				{
					if (tiles_list[i] != null)
					{
						_tileSig[i] = TileSig(tiles_list[i], array);
					}
				}
				_tileCursor = 0;
				return;
			}
			if (_known.Count == 0)
			{
				_tileCursor = (_tileCursor + 24000) % tiles_list.Length;
				return;
			}
			_changedTiles.Clear();
			int num = Math.Min(24000, tiles_list.Length);
			for (int j = 0; j < num; j++)
			{
				int tileCursor = _tileCursor;
				_tileCursor = (_tileCursor + 1) % tiles_list.Length;
				WorldTile worldTile = tiles_list[tileCursor];
				if (worldTile != null)
				{
					long num2 = TileSig(worldTile, array);
					if (num2 != _tileSig[tileCursor])
					{
						_tileSig[tileCursor] = num2;
						_changedTiles.Add(worldTile);
					}
				}
			}
			if (_changedTiles.Count == 0)
			{
				return;
			}
			for (int k = 0; k < _changedTiles.Count; k += 2000)
			{
				int num3 = Math.Min(_changedTiles.Count, k + 2000);
				Dictionary<string, int> dictionary = new Dictionary<string, int>();
				List<string> list = new List<string>();
				for (int l = k; l < num3; l++)
				{
					WorldTile worldTile2 = _changedTiles[l];
					Name(worldTile2.main_type?.id ?? "", dictionary, list);
					Name(worldTile2.top_type?.id ?? "", dictionary, list);
				}
				W.Reset();
				W.VarU(35uL);
				W.VarU((ulong)list.Count);
				foreach (string item in list)
				{
					W.Str(item);
				}
				W.VarU((ulong)(num3 - k));
				for (int m = k; m < num3; m++)
				{
					WorldTile worldTile3 = _changedTiles[m];
					W.U16(worldTile3.x);
					W.U16(worldTile3.y);
					W.VarU((ulong)dictionary[worldTile3.main_type?.id ?? ""]);
					W.VarU((ulong)dictionary[worldTile3.top_type?.id ?? ""]);
					int num4 = 0;
					WorldTileData data = worldTile3.data;
					int num5 = data?.tile_id ?? (-1);
					if (array != null && num5 >= 0 && num5 < array.Length && array[num5])
					{
						num4 |= 1;
					}
					if (data != null && data.frozen)
					{
						num4 |= 2;
					}
					W.U8(num4);
					W.VarI(data?.height ?? 0);
					W.U8(worldTile3.burned_stages & 0xFF);
				}
				foreach (KeyValuePair<int, Known> item2 in _known)
				{
					PlayerById(item2.Key)?.Link?.Send(W, Delivery.Reliable);
				}
			}
		}

		private static void Name(string id, Dictionary<string, int> names, List<string> order)
		{
			if (!names.ContainsKey(id))
			{
				names[id] = order.Count;
				order.Add(id);
			}
		}

		private static void JoinerTiles(NetReader r)
		{
			int num = r.Count(4096);
			string[] array = new string[num];
			for (int i = 0; i < num; i++)
			{
				array[i] = r.Str() ?? "";
			}
			int num2 = r.Count(100000);
			for (int j = 0; j < num2; j++)
			{
				int pX = r.U16();
				int pY = r.U16();
				int num3 = (int)r.VarU();
				int num4 = (int)r.VarU();
				int num5 = r.U8();
				int num6 = (int)r.VarI();
				int num7 = r.U8();
				WorldTile tile = World.world.GetTile(pX, pY);
				if (tile == null || num3 >= num || num4 >= num)
				{
					continue;
				}
				try
				{
					TileTypeBase cur_tile_type = tile.cur_tile_type;
					TileType tileType = ((array[num3] == "") ? null : AssetManager.tiles.get(array[num3]));
					TopTileType topTileType = ((array[num4] == "") ? null : AssetManager.top_tiles.get(array[num4]));
					if (tileType != null && (tile.main_type != tileType || tile.top_type != topTileType))
					{
						tile.setTileTypes(tileType, topTileType);
					}
					if (tile.data != null && tile.data.height != num6)
					{
						tile.data.height = num6;
						World.world.setTileDirty(tile);
					}
					if (tile.burned_stages != num7)
					{
						if (num7 == 0)
						{
							tile.removeBurn();
						}
						else
						{
							tile.setBurnedStage(num7);
						}
					}
					bool flag = (num5 & 1) != 0;
					if (flag != tile.isOnFire())
					{
						tile.setFireData(flag);
					}
					if (tile.data != null)
					{
						tile.data.frozen = (num5 & 2) != 0;
					}
					MapAction.checkTileState(tile, cur_tile_type);
				}
				catch (Exception ex)
				{
					if (_applyErrors++ < 10)
					{
						Log.Error("NET tile " + pX + "," + pY + ": " + ex.Message);
					}
				}
			}
		}

		internal static void RemoteBodies(List<Actor> into)
		{
			if (NetSession.Current == null || !NetSession.Current.IsHost)
			{
				return;
			}
			foreach (Remote value in _remotes.Values)
			{
				try
				{
					if (value.A != null && value.A.isAlive())
					{
						into.Add(value.A);
					}
				}
				catch
				{
				}
			}
		}

		internal static bool ToastFor(Actor body, string text, bool cheer)
		{
			if (NetSession.Current == null || !NetSession.Current.IsHost || body?.data == null)
			{
				return false;
			}
			foreach (Remote value in _remotes.Values)
			{
				if (value.A == body && value.P != null)
				{
					TellTown(value.P, text, cheer);
					return true;
				}
			}
			return false;
		}

		private static bool StartTownAct(Actor me, byte op)
		{
			if (!Mirroring || me?.data == null || me.data.id != _myBody)
			{
				return false;
			}
			W.Reset();
			W.VarU(51uL);
			W.U8(15);
			W.U8(op);
			return true;
		}

		private static void WriteTiles(List<WorldTile> tiles)
		{
			int num = Mathf.Min(tiles?.Count ?? 0, 60);
			W.VarU((ulong)num);
			for (int i = 0; i < num; i++)
			{
				W.VarI(tiles[i].x);
				W.VarI(tiles[i].y);
			}
		}

		private static List<WorldTile> ReadTiles(NetReader r)
		{
			int num = r.Count(60);
			List<WorldTile> list = new List<WorldTile>(num);
			for (int i = 0; i < num; i++)
			{
				WorldTile tile = World.world.GetTile((int)r.VarI(), (int)r.VarI());
				if (tile != null && !list.Contains(tile))
				{
					list.Add(tile);
				}
			}
			return list;
		}

		internal static bool ForwardFound(Actor me, WorldTile at)
		{
			if (at == null || !StartTownAct(me, 1))
			{
				return false;
			}
			W.VarI(at.x);
			W.VarI(at.y);
			_s.ToHost(W, Delivery.Reliable);
			return true;
		}

		internal static bool ForwardPlans(Actor me, City town, string order, int face, List<WorldTile> tiles)
		{
			if (town?.data == null || string.IsNullOrEmpty(order) || !StartTownAct(me, 2))
			{
				return false;
			}
			W.VarI(town.data.id);
			W.Str(order);
			W.U8((byte)(face & 3));
			WriteTiles(tiles);
			_s.ToHost(W, Delivery.Reliable);
			return true;
		}

		internal static bool ForwardLayout(Actor me, City town, int wall, List<WorldTile> tiles)
		{
			if (town?.data == null || !StartTownAct(me, (byte)((wall < 0) ? 7 : 8)))
			{
				return false;
			}
			W.VarI(town.data.id);
			if (wall >= 0)
			{
				W.U8((byte)wall);
			}
			WriteTiles(tiles);
			_s.ToHost(W, Delivery.Reliable);
			return true;
		}

		internal static bool ForwardSite(Actor me, Building b, byte op)
		{
			if (b?.data == null || !StartTownAct(me, op))
			{
				return false;
			}
			W.VarI(b.data.id);
			_s.ToHost(W, Delivery.Reliable);
			return true;
		}

		internal static bool ForwardUnwall(Actor me, City town, WorldTile at)
		{
			if (town?.data == null || at == null || !StartTownAct(me, 9))
			{
				return false;
			}
			W.VarI(town.data.id);
			W.VarI(at.x);
			W.VarI(at.y);
			_s.ToHost(W, Delivery.Reliable);
			return true;
		}

		internal static bool ForwardRemovePlan(Actor me, Building b)
		{
			return ForwardSite(me, b, 3);
		}

		internal static bool ForwardKnockDown(Actor me, Building b)
		{
			return ForwardSite(me, b, 4);
		}

		internal static bool ForwardBuildNext(Actor me, Building b)
		{
			return ForwardSite(me, b, 5);
		}

		internal static bool ForwardSiteBlow(Actor me, Building b, float factor)
		{
			if (b?.data == null || !StartTownAct(me, 6))
			{
				return false;
			}
			W.VarI(b.data.id);
			W.U8((byte)Mathf.Clamp(Mathf.RoundToInt(factor * 100f), 0, 255));
			_s.ToHost(W, Delivery.Reliable);
			return true;
		}

		private static void JoinerTownAnswer(NetReader r)
		{
			bool flag = r.Bool();
			string text = r.Str() ?? "";
			TownAnswers++;
			LastTownAnswer = text;
			if (text != "")
			{
				Hub.Toast?.Invoke(text);
			}
			Actor actor = null;
			try
			{
				actor = ControllableUnit._unit_main;
			}
			catch
			{
			}
			if (flag && actor != null && actor.isAlive())
			{
				Sounds.PlayAt("event:/SFX/UI/YesPoint", actor.current_position);
			}
			Log.Info("NET your town: " + text);
		}

		private static void TellTown(NetPlayer p, string text, bool cheer)
		{
			if (p?.Link != null)
			{
				W.Reset();
				W.VarU(37uL);
				W.U8(6);
				W.Bool(cheer);
				W.Str(text ?? "");
				p.Link.Send(W, Delivery.Reliable);
			}
		}

		private static bool TownReach(Actor body, WorldTile t, float most = 24f)
		{
			if (t != null)
			{
				return (new Vector2((float)t.x + 0.5f, (float)t.y + 0.5f) - body.current_position).sqrMagnitude <= most * most;
			}
			return false;
		}

		private static void HostTown(NetPlayer p, Actor body, NetReader r)
		{
			int num = r.U8();
			try
			{
				switch (num)
				{
				case 1:
					HostFound(p, body, r);
					break;
				case 2:
					HostPlans(p, body, r);
					break;
				case 7:
				case 8:
					HostLayout(p, body, r, num == 8);
					break;
				case 3:
				case 4:
				case 5:
					HostSite(p, body, r, num);
					break;
				case 6:
					HostSiteBlow(p, body, r);
					break;
				case 9:
					HostUnwall(p, body, r);
					break;
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET " + p?.ToString() + "'s town act " + num + ": " + ex.Message);
			}
		}

		private static void HostFound(NetPlayer p, Actor body, NetReader r)
		{
			WorldTile tile = World.world.GetTile((int)r.VarI(), (int)r.VarI());
			string why = ((!TownReach(body, tile)) ? "Too far" : Village.WhyNotFoundAt(body, tile));
			City city = null;
			if (why == null)
			{
				city = Village.Found(body, tile, out why);
			}
			if (city == null)
			{
				Log.Info("NET " + p?.ToString() + "'s village refused: " + why);
				TellTown(p, why ?? "The land won't take a village here", cheer: false);
				return;
			}
			TownsFoundedForJoiners++;
			Log.Info("NET " + p?.ToString() + " founded " + city.name + " at " + tile.x + "," + tile.y);
			TellTown(p, "You founded " + city.name + ". Build the bonfire: hold the left button at it, with a hammer or bare hands", cheer: true);
		}

		private static void HostPlans(NetPlayer p, Actor body, NetReader r)
		{
			City city = World.world.cities.get(r.VarI());
			string text = r.Str();
			int turns = r.U8();
			List<WorldTile> list = ReadTiles(r);
			if (!Spend(p, 8, 6f, 2f))
			{
				Refused(p, "plans", "too many");
				return;
			}
			PlanPieces.Piece piece = PlanPieces.OfOrder(text);
			BuildingAsset buildingAsset = ((piece != null) ? PlanPieces.AssetOf(piece, city) : null);
			string text2 = ((city == null || !city.isAlive() || !Rep.Rules(body, city)) ? "The town is no longer yours to plan" : ((buildingAsset == null) ? "Not built by your people" : PlanPieces.WhyNot(piece, city)));
			int num = 0;
			string text3 = null;
			bool flag = false;
			if (text2 == null)
			{
				foreach (WorldTile item in list)
				{
					if (TownReach(body, item, 70f))
					{
						if (Blueprints.CountOf(city) >= 12)
						{
							text3 = 12 + " plans are waiting";
							break;
						}
						if (PlanPieces.WhyNot(piece, city) != null)
						{
							text3 = PlanPieces.WhyNot(piece, city);
							break;
						}
						if (!Village.PlansAlone(city) && !Blueprints.CanPay(city, buildingAsset.cost, body, out var missing))
						{
							text3 = "Needs " + missing;
							break;
						}
						bool flag2 = piece.Home && Planning.FirstHome(body, city);
						Building building = Blueprints.PlaceSite(city, buildingAsset, item, body, flag2);
						if (building != null)
						{
							Facing.Set(building, turns);
							SiteLevel.Begin(building);
							flag = flag || flag2;
							num++;
						}
					}
				}
			}
			TownPlansForJoiners += num;
			string text4 = ((piece != null) ? piece.Name.ToLowerInvariant() : "plan");
			string text5 = text2 ?? ((num == 0) ? (text3 ?? "It can't stand there") : ((flag && num == 1) ? "Your house is planned. It's yours when it stands" : ((list.Count == 1) ? null : (num + " " + text4 + ((num == 1) ? "" : "s") + " planned" + ((text3 != null) ? (". " + text3) : "")))));
			Log.Info("NET " + p?.ToString() + "'s plans: " + num + " " + (buildingAsset?.id ?? text) + " of " + list.Count + ((text5 != null) ? (" (" + text5 + ")") : ""));
			if (text5 != null)
			{
				TellTown(p, text5, cheer: false);
			}
		}

		private static void HostLayout(NetPlayer p, Actor body, NetReader r, bool wall)
		{
			City town = World.world.cities.get(r.VarI());
			int num = (wall ? r.U8() : (-1));
			List<WorldTile> list = ReadTiles(r);
			if (!Spend(p, 8, 6f, 2f))
			{
				Refused(p, "layout", "too many");
				return;
			}
			if (town == null || !town.isAlive() || !Rep.Rules(body, town))
			{
				TellTown(p, "The town is no longer yours to plan", cheer: false);
				return;
			}
			list.RemoveAll((WorldTile t) => !TownReach(body, t, 70f));
			string text2;
			if (wall)
			{
				string text = WallPlans.WhyNotKind(town, num);
				list.RemoveAll((WorldTile t) => WallPlans.WhyNotAt(town, t) != null);
				int num2 = ((text == null && list.Count != 0) ? WallPlans.Add(town, list, num) : 0);
				text2 = ((num2 == 0) ? (text ?? "Nowhere on this line can take a wall") : (num2 + " tile" + ((num2 == 1) ? "" : "s") + " of " + WallPlans.Names[num].ToLowerInvariant() + " laid out. Raise them with a hammer, or the town does when it has no building to put up"));
			}
			else
			{
				list.RemoveAll((WorldTile t) => Planning.StreetWhy(town, t) != null);
				if (list.Count > 0)
				{
					town.addRoads(list);
				}
				text2 = ((list.Count == 0) ? "No tile of this can be paved" : (list.Count + " tile" + ((list.Count == 1) ? "" : "s") + " of street laid out. The road builders pave them"));
			}
			TownPlansForJoiners++;
			TellTown(p, text2, cheer: false);
		}

		private static void HostUnwall(NetPlayer p, Actor body, NetReader r)
		{
			City city = World.world.cities.get(r.VarI());
			WorldTile tile = World.world.GetTile((int)r.VarI(), (int)r.VarI());
			if (!Spend(p, 8, 6f, 2f))
			{
				Refused(p, "wall plan", "too many");
			}
			else if (city == null || !city.isAlive() || !Rep.Rules(body, city) || !TownReach(body, tile, 70f))
			{
				TellTown(p, "The town is no longer yours to plan", cheer: false);
			}
			else
			{
				TellTown(p, (WallPlans.Pending(city, tile, out var kind) && WallPlans.Remove(city, tile)) ? (WallPlans.Names[kind] + " plan removed") : "No wall is planned there", cheer: false);
			}
		}

		private static void HostSite(NetPlayer p, Actor body, NetReader r, int op)
		{
			Building building = World.world.buildings.get(r.VarI());
			if (!Spend(p, 8, 6f, 2f))
			{
				Refused(p, "plan", "too many");
				return;
			}
			City city = null;
			try
			{
				city = ((building != null && building.isAlive()) ? building.city : null);
			}
			catch
			{
			}
			if (city == null || !Rep.Rules(body, city) || !TownReach(body, building.current_tile, 70f))
			{
				TellTown(p, "Look at a plan or a building of your town", cheer: false);
				return;
			}
			string text;
			if (op == 5)
			{
				PlanPieces.Piece piece = null;
				try
				{
					piece = PlanPieces.OfOrder(building.asset.group);
				}
				catch
				{
				}
				text = (Blueprints.BuildNext(building) ? (((piece != null) ? piece.Name : building.asset.id) + " is next") : "Look at a plan to build it next");
			}
			else if (!building.isUnderConstruction())
			{
				text = ((op != 4) ? "That isn't a plan any more" : (Blueprints.WhyNotKnockDown(building) ?? (Blueprints.KnockDown(building, body) ? "Knocked down. Half its cost is back in the stockpile" : "That can't be knocked down")));
			}
			else
			{
				bool flag = Blueprints.Refunds(building);
				text = (Blueprints.Remove(building, body) ? ("Plan removed" + (flag ? ", its cost given back" : "")) : "That plan can't be taken back");
			}
			TellTown(p, text, cheer: false);
		}

		private static void HostSiteBlow(NetPlayer p, Actor body, NetReader r)
		{
			Building building = World.world.buildings.get(r.VarI());
			float num = Mathf.Clamp((float)r.U8() / 100f, 0f, 1.5f);
			if (!Spend(p, 7, 4f, 2f))
			{
				Refused(p, "blow at a site", "too many");
				return;
			}
			City city = null;
			try
			{
				city = ((building != null && building.isAlive() && building.isUnderConstruction()) ? building.city : null);
			}
			catch
			{
			}
			if (city == null || num <= 0f || !Rep.Rules(body, city) || Blueprints.DistanceTo(building, body.current_position) > 5.3f)
			{
				Refused(p, "blow at a site", "not their town's site, or out of reach");
				return;
			}
			bool flag = Blueprints.IsPaid(building) || Blueprints.TownsOwn(building);
			if (!flag && Village.PlansAlone(city) && Haul.Give(body, building) > 0)
			{
				flag = Blueprints.IsPaid(building);
			}
			if (!flag && !Blueprints.Pay(city, building, body, out var missing))
			{
				float now = Now;
				if (!_needsToldAt.TryGetValue(p.Id, out var value) || now - value > 2.5f)
				{
					_needsToldAt[p.Id] = now;
					bool flag2 = Village.PlansAlone(city);
					TellTown(p, "Needs " + missing + " more" + ((flag2 && !Haul.NoHands(city)) ? ": carriers bring it from the stockpile" : ", in the stockpile or your bag"), cheer: false);
				}
			}
			else
			{
				Blueprints.Blow(body, building, num);
				BuildHelp.Worked(body, building);
				TownBlowsTaken++;
			}
		}

		private static bool IsTownPath(string path)
		{
			if (!(path == "cityID"))
			{
				return path == "civ_kingdom_id";
			}
			return true;
		}

		private static void HostMembership(NetPlayer p, Actor body, Actor a, List<KeyValuePair<string, JToken>> ops, List<byte> kinds, bool own)
		{
			bool flag = false;
			long num = -1L;
			for (int num2 = ops.Count - 1; num2 >= 0; num2--)
			{
				if (IsTownPath(ops[num2].Key))
				{
					flag = true;
					if (ops[num2].Key == "cityID")
					{
						num = ((kinds[num2] == 2 || ops[num2].Value == null) ? 0 : ops[num2].Value.Value<long>());
					}
					ops.RemoveAt(num2);
					kinds.RemoveAt(num2);
				}
			}
			if (!flag || num < 0)
			{
				return;
			}
			City city = a.city;
			City city2 = ((num > 0) ? World.world.cities.get(num) : null);
			if ((num > 0 && (city2 == null || !city2.isAlive())) || city2 == city)
			{
				return;
			}
			if (!own && (!FamilyOf(body, a) || city2 != body.city || !NearBody(body, a.current_position)))
			{
				RefusedPatch(p, "town of", a.data.id);
				return;
			}
			bool flag2 = ((city2 == null) ? Belong.Leave(a) : Belong.Join(a, city2));
			if (flag2)
			{
				TownMovesForJoiners++;
			}
			Log.Info("NET " + p?.ToString() + ": " + WorldBoxMod.Describe(a) + ((city2 == null) ? (" left " + (city?.name ?? "no town")) : (" moved to " + city2.name)) + (flag2 ? "" : " (the game wouldn't)"));
		}

		private static bool FamilyOf(Actor body, Actor a)
		{
			try
			{
				if (a == null || body == null || a == body)
				{
					return false;
				}
				if (body.lover == a || a.lover == body)
				{
					return true;
				}
				ActorData data = a.data;
				ActorData data2 = body.data;
				if (data.parent_id_1 == data2.id || data.parent_id_2 == data2.id || data2.parent_id_1 == data.id || data2.parent_id_2 == data.id)
				{
					return true;
				}
				return data2.family != 0L && data.family == data2.family;
			}
			catch
			{
				return false;
			}
		}

		public static void SendWorld(NetSession s, NetPlayer p)
		{
			_s = s;
			float now = Now;
			byte[] array;
			try
			{
				HostScanActors(initial: false);
				_scannedBAt = -1f;
				HostScanBuildings(initial: false);
				array = SaveManager.currentWorldToSavedMap().toZip();
			}
			catch (Exception ex)
			{
				Log.Error("NET saving the world for " + p?.ToString() + ": " + ex);
				s.Kick(p, "The host could not save its world to send", ban: false);
				return;
			}
			MarkAllKnown(p);
			Transfer transfer = new Transfer
			{
				Player = p,
				Bytes = array,
				Pieces = (array.Length + 49152 - 1) / 49152
			};
			_transfers.Add(transfer);
			W.Reset();
			W.VarU(20uL);
			W.VarU((ulong)array.Length);
			W.VarU((ulong)transfer.Pieces);
			W.Str(WorldName());
			p.Link.Send(W, Delivery.Reliable);
			Log.Info("NET world for " + p?.ToString() + ": " + array.Length / 1024 + " KB in " + transfer.Pieces + " pieces (saved in " + ((Now - now) * 1000f).ToString("0") + " ms)");
		}

		private static void HostTransfers()
		{
			for (int num = _transfers.Count - 1; num >= 0; num--)
			{
				Transfer transfer = _transfers[num];
				NetLink link = transfer.Player.Link;
				if (link == null || !link.Open)
				{
					_transfers.RemoveAt(num);
				}
				else
				{
					int num2 = 0;
					while (transfer.Next < transfer.Pieces && link.Queued < 524288 && num2 < 16)
					{
						int num3 = transfer.Next * 49152;
						int n = Math.Min(49152, transfer.Bytes.Length - num3);
						W.Reset();
						W.VarU(21uL);
						W.VarU((ulong)transfer.Next);
						W.Bytes(transfer.Bytes, num3, n);
						link.Send(W, Delivery.Reliable);
						transfer.Next++;
						num2++;
					}
					if (transfer.Next >= transfer.Pieces)
					{
						W.Reset();
						W.VarU(22uL);
						W.U32(Crc32(transfer.Bytes));
						link.Send(W, Delivery.Reliable);
						_transfers.RemoveAt(num);
					}
				}
			}
		}

		private static void JoinerWorldBegin(NetReader r)
		{
			_buffered.Clear();
			int num = (int)r.VarU();
			_incomingPieces = (int)r.VarU();
			string text = r.Str();
			if (num <= 0 || num > 536870912)
			{
				NetSession.Leave("The world was too big to receive");
				return;
			}
			_incoming = new byte[num];
			_gotPieces = 0;
			if (_s != null)
			{
				_s.Stage = "Getting the world 0%";
			}
			Log.Info("NET receiving " + text + ": " + num / 1024 + " KB");
		}

		private static void JoinerWorldChunk(NetReader r)
		{
			if (_incoming == null)
			{
				return;
			}
			int num = (int)r.VarU();
			byte[] array = r.Bytes(49152);
			int num2 = num * 49152;
			if (num2 >= 0 && num2 + array.Length <= _incoming.Length)
			{
				Buffer.BlockCopy(array, 0, _incoming, num2, array.Length);
				_gotPieces++;
				if (_s != null)
				{
					_s.Stage = "Getting the world " + 100 * _gotPieces / Math.Max(1, _incomingPieces) + "%";
				}
			}
		}

		private static void JoinerWorldEnd(NetReader r)
		{
			if (_incoming == null)
			{
				return;
			}
			uint num = r.U32();
			if (_gotPieces < _incomingPieces || Crc32(_incoming) != num)
			{
				NetSession.Leave("The world arrived damaged");
				return;
			}
			byte[] incoming = _incoming;
			_incoming = null;
			if (_s != null)
			{
				_s.Stage = "Loading the world";
			}
			try
			{
				bool worldIn = _worldIn;
				if (!worldIn)
				{
					SaveOwnWorld();
				}
				LetGoForLoad();
				Suppress(on: true);
				if (!worldIn)
				{
					_autosavesBefore = Config.autosaves;
				}
				Config.autosaves = false;
				if (worldIn)
				{
					JoinerResyncReset();
				}
				_loadingWorld = true;
				ResetActors();
				ResetBodies();
				SaveManager.loadMapFromBytes(incoming);
				_loadStarted = Now;
				Log.Info("NET loading the host's world (" + incoming.Length / 1024 + " KB)");
			}
			catch (Exception ex)
			{
				Log.Error("NET loading the host's world: " + ex);
				NetSession.Leave("Could not load the host's world");
			}
		}

		public static bool JoinerWorldChanged()
		{
			if (!_worldIn || _loadingWorld || _joinerStats == null)
			{
				return false;
			}
			try
			{
				if (!Config.game_loaded || World.world == null || SmoothLoader.isLoading() || World.world.map_stats == _joinerStats)
				{
					return false;
				}
				_savedBefore = false;
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static void JoinerTransfer()
		{
			if (!_loadingWorld)
			{
				return;
			}
			if (Loading)
			{
				if (Now - _loadStarted > 180f)
				{
					NetSession.Leave("The host's world took too long to load");
				}
				return;
			}
			_loadingWorld = false;
			_worldIn = true;
			bool resyncing = _resyncing;
			_resyncing = false;
			_joinerStats = World.world?.map_stats;
			KeepFrozen();
			ResetActors();
			JoinerIndexActors();
			WrapPowers();
			PictureShots(on: true);
			PictureDrops(on: true);
			JoinerPrepareOwn();
			if (_s != null)
			{
				_s.Stage = "";
			}
			W.Reset();
			W.VarU(23uL);
			_s?.ToHost(W, Delivery.Reliable);
			Log.Info("NET host's world loaded in " + (Now - _loadStarted).ToString("0.0") + " s; " + _buffered.Count + " changes waiting" + (resyncing ? " (resync)" : ""));
			if (resyncing)
			{
				MpUi.Banner("Resynced", "The host's world is loaded again", bad: false, 4f);
				return;
			}
			WorldBoxMod.Instance?.Toast("Joined " + (_s?.HostInfo?.Name ?? "the world") + ". Possess a creature to play");
			MpUi.Banner("Joined " + (_s?.HostInfo?.Name ?? "the world"), "Choose a body on the right, or possess any creature", bad: false, 8f);
		}

		private static void SaveOwnWorld()
		{
			if (_savedBefore)
			{
				return;
			}
			try
			{
				if (!(World.world == null) && Config.game_loaded)
				{
					byte[] array = SaveManager.currentWorldToSavedMap().toZip();
					File.WriteAllBytes(BeforeFile, array);
					_savedBefore = true;
					Log.Info("NET your world saved to " + BeforeFile + " (" + array.Length / 1024 + " KB)");
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET saving your own world before joining: " + ex.Message);
			}
		}

		private static void LetGoForLoad()
		{
			try
			{
				if (ControllableUnit.isControllingUnit())
				{
					ControllableUnit.clear(pCallKill: false);
				}
			}
			catch
			{
			}
		}

		private static void JoinerStop(string reason, bool byMe)
		{
			bool num = _worldIn || _loadingWorld;
			_worldIn = false;
			_loadingWorld = false;
			_incoming = null;
			_buffered.Clear();
			_motion.Clear();
			UnwrapPowers();
			PictureShots(on: false);
			PictureDrops(on: false);
			OwnSpeedBack();
			if (!num)
			{
				return;
			}
			LetGoForLoad();
			try
			{
				Config.paused = false;
			}
			catch
			{
			}
			Config.autosaves = _autosavesBefore;
			Suppress(on: false);
			try
			{
				if (_savedBefore && File.Exists(BeforeFile))
				{
					SaveManager.loadMapFromBytes(File.ReadAllBytes(BeforeFile));
					_savedBefore = false;
					Log.Info("NET your own world loaded back");
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET loading your own world back: " + ex.Message);
			}
		}

		private static void Suppress(bool on)
		{
			try
			{
				(typeof(WorldSync).Assembly.GetType("FirstPerson.FirstPersonSave")?.GetField("Suppressed", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))?.SetValue(null, on);
			}
			catch
			{
			}
		}

		private static uint Crc32(byte[] b)
		{
			if (_crcTable == null)
			{
				_crcTable = new uint[256];
				for (uint num = 0u; num < 256; num++)
				{
					uint num2 = num;
					for (int i = 0; i < 8; i++)
					{
						num2 = (((num2 & 1) != 0) ? (0xEDB88320u ^ (num2 >> 1)) : (num2 >> 1));
					}
					_crcTable[num] = num2;
				}
			}
			uint num3 = uint.MaxValue;
			foreach (byte b2 in b)
			{
				num3 = _crcTable[(num3 ^ b2) & 0xFF] ^ (num3 >> 8);
			}
			return ~num3;
		}

		private static void JoinerCityZones(object have)
		{
			if (!(have is City city) || city.data == null || city.data.zones == null)
			{
				return;
			}
			try
			{
				ZoneCalculator zone_calculator = World.world.zone_calculator;
				HashSet<TileZone> hashSet = new HashSet<TileZone>();
				foreach (ZoneData zone2 in city.data.zones)
				{
					TileZone zone = zone_calculator.getZone(zone2.x, zone2.y);
					if (zone != null)
					{
						hashSet.Add(zone);
					}
				}
				bool flag = false;
				for (int num = city.zones.Count - 1; num >= 0; num--)
				{
					TileZone tileZone = city.zones[num];
					if (tileZone != null && !hashSet.Contains(tileZone))
					{
						city.removeZone(tileZone);
						flag = true;
					}
				}
				foreach (TileZone item in hashSet)
				{
					if (item.city != city)
					{
						city.addZone(item);
						flag = true;
					}
				}
				if (flag)
				{
					zone_calculator.setDrawnZonesDirty();
				}
			}
			catch (Exception ex)
			{
				if (_applyErrors++ < 10)
				{
					Log.Error("NET town land " + city.data.id + ": " + ex.Message);
				}
			}
		}

		private static void RushCities()
		{
			if (Now - _citiesRushedAt < 0.25f)
			{
				return;
			}
			_citiesRushedAt = Now;
			foreach (MetaKind item in MetaKinds())
			{
				if (!(item.Field == "cities"))
				{
					continue;
				}
				foreach (TrackedM value in item.Tracked.Values)
				{
					value.HashedAt = -99f;
				}
			}
		}
	}
}
