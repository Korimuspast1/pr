using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class FirstPersonAudio
	{
		private sealed class AbilitySounds
		{
			public string Hold;

			public string Use;

			public string Land;

			public string AnswerFew;

			public string AnswerMany;

			public float UseVolume = 0.8f;

			public float LandVolume = 0.8f;

			public float AnswerDelay = 0.9f;

			public bool Grunt;

			public bool KeepMotion;
		}

		private enum Perch
		{
			Tree,
			Ground,
			Marsh
		}

		private sealed class Kind
		{
			public string Key;

			public int First;

			public int Songs;

			public float Loud;

			public float GapMin;

			public float GapMax;

			public int BoutMin;

			public int BoutMax;

			public float Weight;

			public bool OnGround;

			public Perch Perch;
		}

		private sealed class Singer
		{
			public Kind Kind;

			public int SongA;

			public int SongB;

			public int Song;

			public float Pitch;

			public Vector3 At;

			public float Height;

			public float Next;

			public int Left;

			public int Bouts;
		}

		private struct Rumble
		{
			public float Delay;

			public float Volume;
		}

		public struct State
		{
			public Vector2 Position;

			public float EyeAboveGround;

			public float Yaw;

			public float Pitch;

			public bool Indoors;

			public bool Swimming;

			public bool Moving;

			public bool Airborne;

			public bool Dead;

			public byte Ground;

			public bool Shallows;

			public int Steps;

			public float Stride;

			public float Run;

			public float Crouch;

			public float SwimPhase;

			public float Health;

			public int Attacks;

			public Swing Swing;

			public bool Melee;

			public string WeaponId;

			public int Hits;

			public int Kills;

			public int Hurts;

			public float Wind;

			public float Rain;

			public float HearthDistance;

			public float Hour;

			public float Night;

			public Habitat Habitat;

			public float Heavy;

			public byte Armor;

			public bool Woman;

			public float Voice;

			public Vector2 Velocity;

			public float Full;

			public int Jumps;

			public int Landings;

			public float LandSpeed;
		}

		private struct Pending
		{
			public string Key;

			public float At;

			public float Volume;
		}

		private sealed class DragonVoice
		{
			public Actor Actor;

			public int LastBeat = int.MinValue;

			public float LastBreath;

			public float NextRoar;

			public float Seen;

			public int Beats = -1;

			public int Roars;

			public int Thumps;

			public int Whooshes;

			public int Snorts;
		}

		private struct Dying
		{
			public Sound Sound;

			public float At;
		}

		private sealed class Carrying
		{
			public float Half;

			public float Trim = 1f;

			public bool Creature;

			public bool Lava;
		}

		private sealed class Voice
		{
			public Channel Channel;

			public DSP Source;

			public Vector3 At;

			public float Volume;

			public bool Ambient;

			public bool Huge;

			public bool Speech;

			public float Occlusion;

			public float NextOcclusion;
		}

		private sealed class GameSound
		{
			public FMOD.Studio.EventInstance Instance;

			public Vector3 At;

			public Transform Follow;

			public bool Bed;

			public bool Held;

			public bool Native;

			public DSP Source;

			public ChannelGroup Group;

			public bool Attached;

			public float Age;

			public float Gain = 1f;

			public float NextGain;

			public float Occlusion;

			public float NextOcclusion;

			public float Spread;

			public bool Huge;

			public bool HugeKnown;

			public float KeepSet = 1f;

			public float NativeKeep = 1f;

			public float NativeSet = 1f;

			public Actor Speaker;

			public long SpeakerId;

			public Carrying Hear;

			public float Near = 1f;
		}

		public struct TalkLine
		{
			public bool Open;

			public bool Voiced;

			public long With;

			public int Key;

			public string Kind;

			public Mumble.Speaker Who;

			public string Text;

			public float Seconds;

			public float Age;

			public bool Typed;

			public Vector3 Mouth;
		}

		internal sealed class SpeechJob
		{
			public string Kind;

			public Mumble.Speaker Who;

			public string Text;

			public float Seconds;

			public bool Opening;

			public int Seed;

			public Mumble.Phrase Phrase;

			public long Ms;

			public volatile bool Done;
		}

		private sealed class Said
		{
			public SpeechJob Job;

			public Sound Sound;

			public Channel Channel;

			public bool Playing;

			public bool Spatial;

			public float Ends;

			public float Volume;

			public Vector3 At;

			public Actor Speaker;

			public long SpeakerId;

			public float AskedAt;
		}

		private sealed class WonderLoop
		{
			public Channel Channel;

			public bool Playing;

			public float Volume;

			public float Want;

			public Vector3 At;
		}

		private static readonly Dictionary<string, AbilitySounds> AbilitySet = new Dictionary<string, AbilitySounds>
		{
			["rally"] = new AbilitySounds
			{
				Use = "ab_rally_horn",
				UseVolume = 0.85f,
				AnswerFew = "ab_cheer_few",
				AnswerMany = "ab_cheer_many",
				AnswerDelay = 1.05f
			},
			["warcry"] = new AbilitySounds
			{
				Use = "ab_warcry",
				UseVolume = 0.85f,
				Land = "ab_rage",
				LandVolume = 0.55f
			},
			["slam"] = new AbilitySounds
			{
				Grunt = true,
				KeepMotion = true,
				Land = "ab_quake",
				LandVolume = 0.95f
			},
			["grace"] = new AbilitySounds
			{
				KeepMotion = true,
				Land = "ab_grace",
				LandVolume = 0.75f
			},
			["firebomb"] = new AbilitySounds
			{
				Hold = "ab_hold_fuse",
				Grunt = true,
				KeepMotion = true
			},
			["raise"] = new AbilitySounds
			{
				Hold = "ab_hold_magic",
				KeepMotion = true,
				Land = "ab_raise"
			},
			["fire"] = new AbilitySounds
			{
				Hold = "ab_hold_magic",
				Use = "ab_fire"
			},
			["hellfire"] = new AbilitySounds
			{
				Hold = "ab_hold_magic",
				Use = "ab_hellfire"
			},
			["plasma"] = new AbilitySounds
			{
				Hold = "ab_hold_magic",
				Use = "ab_plasma",
				UseVolume = 0.7f
			},
			["acid"] = new AbilitySounds
			{
				Use = "ab_spit"
			},
			["bless"] = new AbilitySounds
			{
				KeepMotion = true,
				Land = "ab_bless"
			},
			["remedy"] = new AbilitySounds
			{
				KeepMotion = true,
				Land = "ab_remedy"
			},
			["heal"] = new AbilitySounds
			{
				Land = "ab_bless"
			},
			["roots"] = new AbilitySounds
			{
				Hold = "ab_hold_magic",
				KeepMotion = true,
				Land = "ab_roots"
			},
			["frost"] = new AbilitySounds
			{
				KeepMotion = true,
				Land = "ab_frost"
			},
			["flameburst"] = new AbilitySounds
			{
				Use = "ab_fire",
				UseVolume = 0.45f,
				Land = "ab_flameburst"
			},
			["blink"] = new AbilitySounds
			{
				Hold = "ab_hold_magic",
				Land = "ab_blink"
			},
			["spores"] = new AbilitySounds
			{
				Hold = "ab_hold_magic",
				KeepMotion = true,
				Land = "ab_spores"
			},
			["snow"] = new AbilitySounds
			{
				Use = "ab_snow"
			},
			["bone"] = new AbilitySounds
			{
				Use = "ab_bone"
			},
			["rock"] = new AbilitySounds
			{
				Use = "ab_chatter",
				UseVolume = 0.6f,
				KeepMotion = true
			},
			["howl"] = new AbilitySounds
			{
				Use = "ab_howl",
				AnswerFew = "ab_pack_few",
				AnswerMany = "ab_pack_many",
				AnswerDelay = 1.5f
			},
			["roar"] = new AbilitySounds
			{
				Use = "ab_roar",
				UseVolume = 0.9f
			},
			["charge"] = new AbilitySounds
			{
				Use = "ab_charge"
			},
			["pounce"] = new AbilitySounds
			{
				Use = "ab_snarl"
			},
			["flutter"] = new AbilitySounds
			{
				Use = "ab_flutter"
			},
			["slide"] = new AbilitySounds
			{
				Use = "ab_slide"
			},
			["venom"] = new AbilitySounds
			{
				Use = "ab_venom"
			},
			["shell"] = new AbilitySounds
			{
				Use = "ab_shell"
			},
			["pincer"] = new AbilitySounds
			{
				Use = "ab_pincer"
			},
			["rotbite"] = new AbilitySounds
			{
				Use = "ab_rotbite"
			}
		};

		private readonly List<string> _abilityLog = new List<string>();

		private Channel _holdChannel;

		private bool _holdPlaying;

		private string _holdKey;

		private float _holdLevel;

		public float AmbientVolume = 1f;

		private const int MaxBirds = 5;

		private const int MaxFrogs = 4;

		private const float CricketCycle = 39f;

		private const float CricketBout = 12f;

		private const float Hearing = 45f;

		private const float BirdLevel = 0.2f;

		private static readonly Kind[] DayKinds = new Kind[6]
		{
			new Kind
			{
				Key = "bird_chirps",
				Songs = 8,
				Loud = 0.8f,
				GapMin = 0.8f,
				GapMax = 2.6f,
				BoutMin = 3,
				BoutMax = 7,
				Weight = 1f,
				OnGround = true
			},
			new Kind
			{
				Key = "bird_whistle",
				Songs = 6,
				Loud = 0.7f,
				GapMin = 2.5f,
				GapMax = 5.5f,
				BoutMin = 3,
				BoutMax = 6,
				Weight = 0.7f
			},
			new Kind
			{
				Key = "bird_trill",
				Songs = 6,
				Loud = 0.7f,
				GapMin = 3f,
				GapMax = 7f,
				BoutMin = 2,
				BoutMax = 5,
				Weight = 0.8f,
				OnGround = true
			},
			new Kind
			{
				Key = "bird_warble",
				Songs = 10,
				Loud = 0.8f,
				GapMin = 1.2f,
				GapMax = 3.2f,
				BoutMin = 4,
				BoutMax = 9,
				Weight = 1f
			},
			new Kind
			{
				Key = "bird_twitter",
				Songs = 6,
				Loud = 0.6f,
				GapMin = 1.5f,
				GapMax = 4f,
				BoutMin = 2,
				BoutMax = 5,
				Weight = 0.8f,
				OnGround = true
			},
			new Kind
			{
				Key = "bird_coo",
				Songs = 2,
				Loud = 0.8f,
				GapMin = 8f,
				GapMax = 14f,
				BoutMin = 2,
				BoutMax = 3,
				Weight = 0.2f
			}
		};

		private static readonly Kind Owl = new Kind
		{
			Key = "owl",
			Songs = 2,
			Loud = 1.2f,
			GapMin = 6f,
			GapMax = 12f,
			BoutMin = 2,
			BoutMax = 4
		};

		private static readonly Kind Croaker = new Kind
		{
			Key = "frog",
			First = 0,
			Songs = 4,
			Loud = 0.6f,
			GapMin = 0.5f,
			GapMax = 1.6f,
			BoutMin = 3,
			BoutMax = 8,
			Perch = Perch.Marsh
		};

		private static readonly Kind Peeper = new Kind
		{
			Key = "frog",
			First = 4,
			Songs = 2,
			Loud = 0.45f,
			GapMin = 0.3f,
			GapMax = 0.7f,
			BoutMin = 6,
			BoutMax = 14,
			Perch = Perch.Marsh
		};

		private readonly List<Singer> _birds = new List<Singer>();

		private readonly List<Singer> _night = new List<Singer>();

		private readonly List<Singer> _frogs = new List<Singer>();

		private ChannelGroup _ambience;

		private bool _haveAmbience;

		private DSP _ambienceReverb;

		private bool _haveAmbienceReverb;

		private Channel _cricketChannel;

		private bool _cricketsPlaying;

		private float _cricketVolume;

		private float _spawnTimer;

		private readonly Dictionary<string, float[]> _lengths = new Dictionary<string, float[]>();

		private const float ArrowHearing = 36f;

		private const int ArrowsHeardAtOnce = 4;

		private readonly List<StuckArrows.Impact> _impacts = new List<StuckArrows.Impact>();

		public int ArrowImpactsPlayed;

		private static readonly List<Rumble> Rumbles = new List<Rumble>();

		private static readonly object RumbleLock = new object();

		private const float StopHearing = 36f;

		private int _stopsHeard = -1;

		public int StopsPlayed;

		public string LastStopSound;

		public int HeadshotsPlayed;

		private int _headshotsHeard = -1;

		private int _lastLandings = int.MinValue;

		private int _lastJumps = int.MinValue;

		private Vector2 _dragVel;

		private bool _haveDragVel;

		private float _sinceScuff = 9f;

		private float _sinceSkid = 9f;

		private float _braking;

		public bool BowSounds;

		private Channel _drawChannel;

		private bool _drawPlaying;

		private bool _nockPlayed;

		private const string CrabLaser = "event:/SFX/UNIQUE/Crabzilla/CrabzillaLazer";

		private readonly FMOD.Studio.EventInstance[] _crabHum = new FMOD.Studio.EventInstance[2];

		private readonly bool[] _crabHumming = new bool[2];

		private readonly float[] _crabStartedAt = new float[2];

		private readonly int[] _crabAsked = new int[2] { -10, -10 };

		private readonly Vector3[] _crabAt = new Vector3[2];

		private bool _crabFailed;

		public int CrabHumStarts;

		private const float ListenZoom = 11f;

		private const float MuffleCutoff = 750f;

		private const float MuffleOpen = 22000f;

		private const float MuffleTime = 0.35f;

		private const float RoomTime = 0.5f;

		private bool _on;

		private bool _failed;

		private StudioListener _listener;

		private float _savedZoom = -1f;

		private ChannelGroup _group;

		private bool _haveGroup;

		private DSP _reverb;

		private bool _haveReverb;

		private float _room = -1f;

		private float _roomSet = -1f;

		private readonly Dictionary<IntPtr, KeyValuePair<ChannelGroup, DSP>> _muffles = new Dictionary<IntPtr, KeyValuePair<ChannelGroup, DSP>>();

		private bool _muffled;

		private float _muffle;

		private float _muffleCutoff = 22000f;

		private float _rescan;

		private readonly Dictionary<string, Sound[]> _sounds = new Dictionary<string, Sound[]>();

		private readonly Dictionary<string, int> _lastVariant = new Dictionary<string, int>();

		private Channel _windChannel;

		private Channel _fireChannel;

		private Channel _rainChannel;

		private bool _windPlaying;

		private bool _firePlaying;

		private bool _rainPlaying;

		private float _windVolume;

		private float _fireVolume;

		private float _rainVolume;

		private int _lastStep = int.MinValue;

		private int _lastStroke = int.MinValue;

		private int _lastAttacks = int.MinValue;

		private int _lastHits = int.MinValue;

		private int _lastKills = int.MinValue;

		private int _lastHurts = int.MinValue;

		private string _lastWeapon;

		private bool _haveWeapon;

		private float _heartTimer;

		private float _lapTimer;

		private float _airTime;

		private float _sinceStep;

		private bool _wasSwimming;

		private bool _wasAirborne;

		private readonly System.Random _rng = new System.Random();

		private readonly List<Pending> _pending = new List<Pending>();

		private float _clock;

		private static Dictionary<string, List<byte[]>> _made;

		private static bool _making;

		private static long _madeMs;

		private static string _madeError;

		private static readonly object MadeLock = new object();

		private float _hornAt = -10f;

		private float _masterSet = 1f;

		private float _sfxVolume = 1f;

		private static readonly DSP_SFXREVERB[] RoomParams = new DSP_SFXREVERB[13]
		{
			DSP_SFXREVERB.DECAYTIME,
			DSP_SFXREVERB.EARLYDELAY,
			DSP_SFXREVERB.LATEDELAY,
			DSP_SFXREVERB.HFREFERENCE,
			DSP_SFXREVERB.HFDECAYRATIO,
			DSP_SFXREVERB.DIFFUSION,
			DSP_SFXREVERB.DENSITY,
			DSP_SFXREVERB.LOWSHELFFREQUENCY,
			DSP_SFXREVERB.LOWSHELFGAIN,
			DSP_SFXREVERB.HIGHCUT,
			DSP_SFXREVERB.EARLYLATEMIX,
			DSP_SFXREVERB.WETLEVEL,
			DSP_SFXREVERB.DRYLEVEL
		};

		private static readonly float[] OpenAir = new float[13]
		{
			900f, 12f, 22f, 5000f, 40f, 70f, 60f, 250f, -3f, 4500f,
			70f, -22f, 0f
		};

		private static readonly float[] SmallRoom = new float[13]
		{
			520f, 3f, 6f, 5000f, 55f, 90f, 90f, 250f, 0f, 7000f,
			45f, -12f, 0f
		};

		public float StepVolume = 1f;

		public float MasterVolume = 1f;

		public float EffectsVolume = 1f;

		private readonly List<Bus> _lockedBuses = new List<Bus>();

		private float _lockTryAt;

		private static bool _busesLogged;

		private static readonly HashSet<string> Loops = new HashSet<string> { "wind", "fire", "rain", "crickets", "dragon_fire", "ufo_hum" };

		private static readonly HashSet<string> Stereo = new HashSet<string> { "crickets" };

		private const float DragonHearing = 130f;

		private readonly List<DragonVoice> _dragons = new List<DragonVoice>();

		private readonly List<Actor> _dragonScratch = new List<Actor>();

		private float _dragonScan;

		private float _lastRoar = -99f;

		private Channel _dragonFireChannel;

		private bool _dragonFirePlaying;

		private float _dragonFireVolume;

		public int DragonWingBeats;

		public int DragonRoars;

		private Vector3 _dragonFireAt;

		private static int _outRate;

		private readonly List<Dying> _dying = new List<Dying>();

		private float _gameMusic = 1f;

		private float _gameMusicWant = 1f;

		private float _gameMusicWait;

		private float _masterWant = -1f;

		private const float CreatureHalf = 5f;

		private const float TalkTrim = 0.5f;

		private const float LavaTrim = 0.3f;

		private const float LavaHalf = 4f;

		private const int LavaReach = 24;

		private const string LavaBed = "event:/SFX/ENVIRONMENT/LavaEnvironment";

		private static readonly Carrying Plain = new Carrying
		{
			Half = 0f
		};

		private readonly Dictionary<IntPtr, Carrying> _carrying = new Dictionary<IntPtr, Carrying>();

		private Vector2 _lavaAt;

		private float _lavaDistance = -1f;

		private float _lavaNext = -10f;

		private const float HugeIndoors = 0.55f;

		private const float InsideMargin = 0.6f;

		internal static FirstPersonAudio Walls;

		private static Rect _inside;

		private static float _insideAt = -10f;

		private static readonly string[] HugeEvents = new string[12]
		{
			"event:/SFX/EXPLOSIONS/", "event:/SFX/DESTRUCTION/FallMeteorite", "event:/SFX/DESTRUCTION/CrabBombImpact", "event:/SFX/NATURE/EarthQuake", "event:/SFX/NATURE/TornadoIdleLoop", "event:/SFX/BUILDINGS/DestroyBuilding", "event:/SFX/BUILDINGS_IDLE/IdleVolcano", "event:/SFX/UNIQUE/Crabzilla/", "event:/SFX/UNITS/dragon/", "event:/SFX/POWERS/AtomicBomb",
			"event:/SFX/POWERS/AntimatterBomb", "event:/SFX/POWERS/TsarBomb"
		};

		private static readonly string[] SmallEvents = new string[1] { "event:/SFX/EXPLOSIONS/ExplosionTiny" };

		private static readonly HashSet<string> HugeKeys = new HashSet<string> { "dragon_roar", "dragon_land", "dragon_whoosh", "dragon_fire", "ab_rally_horn", "ab_quake" };

		private readonly Dictionary<IntPtr, bool> _hugeEvents = new Dictionary<IntPtr, bool>();

		private readonly List<GameSound> _flat = new List<GameSound>();

		private Channel _roofChannel;

		private Channel _wallsChannel;

		private bool _roofPlaying;

		private bool _wallsPlaying;

		private float _roofVolume;

		private float _wallsVolume;

		public float MusicVolume = 1f;

		public float Danger;

		public string Mood = "day";

		private static readonly object MusicLock = new object();

		private static MusicPiece[] _battleMade;

		private static MusicPiece _victoryMade;

		private static MusicPiece _deathMade;

		private static readonly Dictionary<string, MusicPiece> _calmMade = new Dictionary<string, MusicPiece>();

		private static bool _musicWorking;

		private static string _musicError;

		private static int _musicSeed = Environment.TickCount;

		private static readonly Queue<string> _musicQueue = new Queue<string>();

		private ChannelGroup _music;

		private bool _haveMusic;

		private readonly Channel[] _battle = new Channel[3];

		private readonly float[] _battleLevel = new float[3];

		private readonly float[] _battleEnterAt = new float[3] { -1f, -1f, -1f };

		private readonly float[] _battleWant = new float[3];

		private Sound[] _battleSounds;

		private float _battleBeat;

		private float _battleLoop;

		private bool _battlePlaying;

		private float _battleQuiet;

		private Channel _calm;

		private Channel _sting;

		private Sound _calmSound;

		private Sound _stingSound;

		private bool _calmPlaying;

		private bool _stingPlaying;

		private float _calmLevel;

		private float _calmRoom = 1f;

		private float _calmGap;

		private float _peakDanger;

		private int _killsAtStart;

		private bool _gameMusicDucked;

		private bool _musicReported;

		private static readonly float[] EnterBeats = new float[3] { 2f, 1f, 4f };

		private static readonly float[] EnterRamp = new float[3] { 0.12f, 0f, 0.25f };

		private static readonly float[] LayerRise = new float[3]
		{
			1.1111112f,
			0.71428573f,
			5f / 6f
		};

		private static readonly float[] LayerMix = new float[3] { 1f, 0.95f, 0.9f };

		private const float LayerFall = 1f / 3f;

		public float DangerNow;

		private MusicPiece[] _battleSet;

		private float _fallingFor;

		private float _battleLatch = -1f;

		public string LastEnding;

		public string CalmName;

		private readonly Dictionary<string, Sound[]> _ownSets = new Dictionary<string, Sound[]>();

		private readonly Dictionary<string, float[]> _ownLengths = new Dictionary<string, float[]>();

		private readonly Dictionary<string, Sound[]> _packSets = new Dictionary<string, Sound[]>();

		private readonly Dictionary<string, List<byte[]>> _packFrom = new Dictionary<string, List<byte[]>>();

		private readonly HashSet<string> _keptSets = new HashSet<string>();

		private readonly List<string> _dropSets = new List<string>();

		private object _packSetsApplied;

		internal readonly List<string> UnknownPackSets = new List<string>();

		private static readonly int[] PickupSteps = new int[8] { 0, 2, 4, 5, 7, 9, 11, 12 };

		public readonly float[] LayerEnteredAt = new float[3] { -1f, -1f, -1f };

		public readonly float[] LayerEnteredLate = new float[3];

		public bool SpatialWanted;

		internal static FirstPersonAudio Live;

		internal static Func<float, float, float> GroundAt;

		private const int SourceGainDb = 0;

		private const int SourceSpread = 1;

		private const int SourceRolloff = 2;

		private const int SourceOcclusion = 3;

		private const int SourceAttributes = 7;

		private const int RolloffNone = 2;

		private const int MaxGameSounds = 96;

		private const int MaxVoices = 48;

		private const int MaxSources = 160;

		private const float EventGainDb = 3f;

		private const float HoldMax = 0.3f;

		private const float BodyHeight = 0.6f;

		private const float BedSpread = 110f;

		private const float HillOcclusion = 0.6f;

		private const float HillEar = 0.15f;

		private const float AirFrom = 12f;

		private const float AirOcclusion = 0.15f;

		private static uint _listenerPlugin;

		private static uint _sourcePlugin;

		private static bool _pluginSought;

		private ChannelGroup _world;

		private bool _haveWorld;

		private bool _spatial;

		private bool _spatialFailed;

		private DSP _listenerDsp;

		private DSP _worldReverb;

		private bool _haveListener;

		private bool _haveWorldReverb;

		private IntPtr _coreMaster;

		private readonly Stack<DSP> _freeSources = new Stack<DSP>();

		private readonly List<DSP> _allSources = new List<DSP>();

		private Vector3 _ear;

		private Vector3 _earF;

		private Vector3 _earU;

		private Vector3 _earR;

		private Vector3 _studioEar;

		private Vector3 _studioF;

		private float _earHeight;

		private float _yawNow;

		private float _spatialAt = -10f;

		private float _effectsScale = 1f;

		private float _ambientScale = 1f;

		private readonly float[] _attr = new float[24];

		private readonly byte[] _attrBytes = new byte[96];

		private readonly List<Voice> _voices = new List<Voice>();

		private readonly List<GameSound> _gameSounds = new List<GameSound>();

		public int GameSoundsSpatial;

		public int GameSoundsNative;

		public int VoicesPlayed;

		public float SpatialMs;

		public float VoicesVolume = 1f;

		public int VoiceStyle;

		private const float SpeechLevel = 0.7f;

		private const float TalkDip = 0.708f;

		private const float ChatterReach = 30f;

		private const int MaxChatter = 6;

		private const float TalkTrimSpeech = 0.5f;

		internal static FirstPersonAudio Speech;

		internal static bool ChatterOffForTest;

		private TalkLine _talk;

		private float _dip = 1f;

		private float _speechScale = 1f;

		private static readonly object SpeechLock = new object();

		private static readonly Dictionary<string, VoiceSet> _speechKinds = new Dictionary<string, VoiceSet>();

		private static readonly List<string> _speechWanted = new List<string>();

		private static readonly List<SpeechJob> _speechJobs = new List<SpeechJob>();

		private static readonly Queue<string> _speechLog = new Queue<string>();

		private static System.Threading.Thread _speechThread;

		private static GameClips _clips;

		private static string _bankPath;

		private static bool _wantedMain;

		public static string SpeechNote = "";

		private static bool _clipsTried;

		private Said _talkSaid;

		private int _talkKey;

		private float _lastAge;

		private readonly List<Said> _chatter = new List<Said>();

		private ChannelGroup _speechGroup;

		private bool _haveSpeechGroup;

		private bool _speechBroken;

		public int SpeechSaid;

		public int ChatterSaid;

		public float LastSpeechSeconds;

		public int LastSpeechSyllables;

		public long LastSpeechMs;

		public Vector3 LastSpeechAt;

		public bool SwingSounds;

		private Channel _ufoChannel;

		private bool _ufoPlaying;

		private float _ufoVolume;

		private Vector3 _ufoAt;

		public int WindUpsPlayed;

		private static readonly Dictionary<string, (string Loop, float Volume, string Call)> WonderSounds = new Dictionary<string, (string, float, string)>
		{
			{
				"nature_golden_egg",
				("wonder_egg_hum", 0.32f, "wonder_egg_chime")
			},
			{
				"nature_harp",
				("wonder_harp_air", 0.34f, "wonder_harp_gliss")
			},
			{
				"nature_alien_mold",
				("wonder_mold_breath", 0.34f, "wonder_mold_puff")
			},
			{
				"nature_computer",
				("wonder_computer_hum", 0.32f, "wonder_computer_call")
			},
			{
				"nature_tumor",
				("wonder_tumor_heart", 0.42f, null)
			},
			{
				"nature_biomass",
				("wonder_biomass_bubble", 0.36f, null)
			},
			{
				"nature_super_pumpkin",
				("wonder_pumpkin_crackle", 0.32f, null)
			},
			{
				"nature_cybercore",
				("wonder_cyber_scan", 0.22f, null)
			},
			{
				"nature_golden_brain",
				("wonder_brain_choir", 0.3f, null)
			},
			{
				"nature_corrupted_brain",
				("wonder_badbrain_throb", 0.38f, null)
			},
			{
				"nature_flame_tower",
				("wonder_fire_roar", 0.42f, null)
			},
			{
				"nature_ice_tower",
				("wonder_ice_wind", 0.36f, null)
			},
			{
				"nature_angle_tower",
				("wonder_angle_chime", 0.3f, null)
			}
		};

		private readonly Dictionary<string, WonderLoop> _wonderLoops = new Dictionary<string, WonderLoop>();

		internal static string WonderHeard;

		internal static float WonderHeardVolume;

		internal static int WonderCallsPlayed;

		public int SoundKinds => _sounds.Count;

		public bool HasReverb => _haveReverb;

		public int DragonVoices => _dragons.Count;

		private static int OutRate
		{
			get
			{
				if (_outRate <= 0)
				{
					try
					{
						RuntimeManager.CoreSystem.getSoftwareFormat(out _outRate, out var _, out var _);
					}
					catch
					{
					}
					if (_outRate <= 0)
					{
						_outRate = 48000;
					}
				}
				return _outRate;
			}
		}

		internal float RoofVolumeForTest => _roofVolume;

		public bool BattleOn => _battlePlaying;

		public bool CalmOn => _calmPlaying;

		internal List<string> SoundSetNames
		{
			get
			{
				List<string> list = new List<string>();
				foreach (string key in _sounds.Keys)
				{
					if (key != "none")
					{
						list.Add(key);
					}
				}
				foreach (string key2 in _ownSets.Keys)
				{
					if (!list.Contains(key2))
					{
						list.Add(key2);
					}
				}
				list.Sort(StringComparer.Ordinal);
				return list;
			}
		}

		public float MuffleLevel => _muffle;

		public float MuffleHz => _muffleCutoff;

		public float RoomLevel => _room;

		public float GameMusicLevel => _gameMusic;

		internal static bool SpatialSupported => Application.platform != RuntimePlatform.OSXPlayer;

		public bool Spatial => _spatial;

		public int GameSoundsNow => _gameSounds.Count;

		public int GameOneShotsNow => _gameSounds.FindAll((GameSound g) => !g.Bed && g.Follow == null).Count;

		public int VoicesNow => _voices.Count;

		public static string SpatialNote { get; private set; } = "not started";

		public Vector3 EarForTest => new Vector3(_ear.x, _ear.y, _earHeight);

		public bool ListenerOn => _haveListener;

		private bool Fresh
		{
			get
			{
				if (_spatial)
				{
					return Time.unscaledTime - _spatialAt < 0.5f;
				}
				return false;
			}
		}

		public bool SpeechOn
		{
			get
			{
				if (VoiceStyle != 2)
				{
					return VoicesVolume > 0.001f;
				}
				return false;
			}
		}

		private float FlatSpeechScale => _sfxVolume * VoicesVolume * 0.7f;

		public float DipNow => _dip;

		public bool TalkSpeaking
		{
			get
			{
				if (_talkSaid != null && _talkSaid.Playing)
				{
					return Time.unscaledTime < _talkSaid.Ends;
				}
				return false;
			}
		}

		public bool TalkSpeechWaiting
		{
			get
			{
				if (_talkSaid != null && !_talkSaid.Playing)
				{
					return _talkSaid.Job != null;
				}
				return false;
			}
		}

		public int ChatterNow => _chatter.Count;

		private static void MakeAbilitySounds(Action<string, int, Func<int, float[]>> add)
		{
			add("ab_rally_horn", 2, Synth.RallyHorn);
			add("exec_drum", 2, Synth.DrumRoll);
			add("ab_cheer_few", 2, (int v) => Synth.Cheer(v, 4));
			add("ab_cheer_many", 2, (int v) => Synth.Cheer(v, 11));
			add("ab_warcry_m", 2, (int v) => Synth.WarCry(v, woman: false));
			add("ab_warcry_f", 2, (int v) => Synth.WarCry(v, woman: true));
			add("ab_grunt_m", 3, (int v) => Synth.Grunt(v, woman: false));
			add("ab_grunt_f", 3, (int v) => Synth.Grunt(v, woman: true));
			add("ab_rage", 1, Synth.Rage);
			add("ab_quake", 2, Synth.Quake);
			add("ab_grace", 2, Synth.GraceSong);
			add("ab_hold_magic", 1, (int v) => Synth.MagicHum(4f));
			add("ab_hold_fuse", 1, (int v) => Synth.Fuse(3f));
			add("ab_fire", 2, (int v) => Synth.FireCast(v, hell: false));
			add("ab_hellfire", 2, (int v) => Synth.FireCast(v, hell: true));
			add("ab_flameburst", 2, Synth.FlameBurst);
			add("ab_plasma", 2, Synth.Plasma);
			add("ab_spit", 2, Synth.Spit);
			add("ab_raise", 2, Synth.RaiseDead);
			add("ab_bless", 2, Synth.Blessing);
			add("ab_remedy", 2, Synth.Remedy);
			add("ab_roots", 2, Synth.Roots);
			add("ab_frost", 2, Synth.FrostNova);
			add("ab_blink", 2, Synth.Blink);
			add("ab_spores", 2, Synth.Spores);
			add("ab_snow", 2, Synth.SnowVolley);
			add("ab_bone", 2, Synth.BoneToss);
			add("ab_chatter", 2, Synth.Chatter);
			add("ab_howl", 2, (int v) => Synth.Howl(v, 1f));
			add("ab_howl_high", 2, (int v) => Synth.Howl(v, 1.5f));
			add("ab_pack_few", 2, (int v) => Synth.PackAnswer(v, 2));
			add("ab_pack_many", 2, (int v) => Synth.PackAnswer(v, 4));
			add("ab_cackle", 2, Synth.Cackle);
			add("ab_roar", 2, Synth.BeastRoar);
			add("ab_snarl", 2, Synth.Snarl);
			add("ab_hop", 2, Synth.Hop);
			add("ab_charge", 2, Synth.Stampede);
			add("ab_slide", 2, Synth.Slide);
			add("ab_flutter", 2, Synth.Flutter);
			add("ab_buzz", 2, Synth.Buzz);
			add("ab_venom", 2, Synth.Venom);
			add("ab_sting", 2, Synth.Sting);
			add("ab_shell", 2, Synth.ShellClunk);
			add("ab_pincer", 2, Synth.PincerSnap);
			add("ab_rotbite", 2, Synth.RottenBite);
			add("ab_ready", 1, (int v) => Synth.AbilityReady());
			add("ab_denied", 2, Synth.AbilityDenied);
			Loops.Add("ab_hold_magic");
			Loops.Add("ab_hold_fuse");
		}

		public string TakeAbilitySounds()
		{
			string result = string.Join(" ", _abilityLog);
			_abilityLog.Clear();
			return result;
		}

		private void PlayAbility(string key, float volume, float pan = 0f, Vector3? at = null)
		{
			if (_abilityLog.Count < 32)
			{
				_abilityLog.Add(key);
			}
			if (at.HasValue)
			{
				PlayAt(key, at.Value, volume, 0.04f);
			}
			else
			{
				Play(key, volume, 0.04f, pan);
			}
		}

		private static AbilitySounds SoundsOf(Ability ab)
		{
			if (ab == null || ab.Sound == null || !AbilitySet.TryGetValue(ab.Sound, out var value))
			{
				return null;
			}
			return value;
		}

		private static string ForKind(string key, string kind, bool woman)
		{
			switch (key)
			{
			case "ab_warcry":
				if (!woman)
				{
					return "ab_warcry_m";
				}
				return "ab_warcry_f";
			case "ab_howl":
				if (!(kind == "hyena"))
				{
					if (!(kind == "fox"))
					{
						return key;
					}
					return "ab_howl_high";
				}
				return "ab_cackle";
			case "ab_pack_few":
			case "ab_pack_many":
				if (!(kind == "hyena"))
				{
					return key;
				}
				return "ab_cackle";
			case "ab_snarl":
				if (!(kind == "rabbit") && !(kind == "frog"))
				{
					return key;
				}
				return "ab_hop";
			case "ab_flutter":
				switch (kind)
				{
				default:
					return key;
				case "bee":
				case "fly":
				case "grasshopper":
					return "ab_buzz";
				}
			case "ab_venom":
				if (!(kind == "snake"))
				{
					return "ab_sting";
				}
				return key;
			default:
				return key;
			}
		}

		private static string KindOf(Actor a)
		{
			string text = null;
			try
			{
				text = ((a != null && a.asset != null) ? a.asset.id : null);
			}
			catch
			{
			}
			if (text != null && text.StartsWith("civ_", StringComparison.Ordinal))
			{
				text = text.Substring(4);
			}
			return text ?? "";
		}

		private static bool IsWoman(Actor a)
		{
			try
			{
				return a != null && Models.IsFemale(a);
			}
			catch
			{
				return false;
			}
		}

		public void AbilityUsed(Ability ab, Actor host, bool humanoid)
		{
			if (ab == null)
			{
				return;
			}
			AbilitySounds abilitySounds = SoundsOf(ab);
			string kind = KindOf(host);
			bool flag = IsWoman(host);
			if (abilitySounds == null || abilitySounds.Use == null || abilitySounds.KeepMotion)
			{
				switch (ab.Motion)
				{
				case AbilityMotion.Throw:
					Play("swing_heavy", 0.8f, 0.08f);
					break;
				case AbilityMotion.Cast:
					Play("cast", 0.75f, 0.08f);
					break;
				case AbilityMotion.Shout:
					if (humanoid)
					{
						PlayHorn();
					}
					break;
				case AbilityMotion.Slam:
					Play("swing_heavy", 0.9f, 0.08f);
					break;
				case AbilityMotion.Bite:
					Play("swing_fist", 0.7f, 0.08f);
					break;
				case AbilityMotion.Leap:
					Play("cloth", 0.8f, 0.08f);
					break;
				}
			}
			if (abilitySounds != null)
			{
				if (abilitySounds.Use != null)
				{
					PlayAbility(ForKind(abilitySounds.Use, kind, flag), abilitySounds.UseVolume);
				}
				if (abilitySounds.Grunt && humanoid)
				{
					PlayAbility(flag ? "ab_grunt_f" : "ab_grunt_m", 0.5f);
				}
			}
		}

		public void AbilityLanded(Ability ab, Actor host, int answered)
		{
			AbilitySounds abilitySounds = SoundsOf(ab);
			if (abilitySounds == null)
			{
				return;
			}
			string kind = KindOf(host);
			if (abilitySounds.Land != null)
			{
				PlayAbility(ForKind(abilitySounds.Land, kind, IsWoman(host)), abilitySounds.LandVolume);
			}
			if (answered <= 0 || abilitySounds.AnswerFew == null)
			{
				return;
			}
			int num;
			string key;
			if (answered > 3)
			{
				num = ((abilitySounds.AnswerMany != null) ? 1 : 0);
				if (num != 0)
				{
					key = abilitySounds.AnswerMany;
					goto IL_0079;
				}
			}
			else
			{
				num = 0;
			}
			key = abilitySounds.AnswerFew;
			goto IL_0079;
			IL_0079:
			string text = ForKind(key, kind, woman: false);
			float volume = ((num != 0) ? Mathf.Lerp(0.6f, 0.85f, Mathf.Clamp01((float)(answered - 4) / 8f)) : (0.4f + 0.07f * (float)answered));
			_pending.Add(new Pending
			{
				Key = text,
				At = _clock + abilitySounds.AnswerDelay,
				Volume = volume
			});
			if (_abilityLog.Count < 32)
			{
				_abilityLog.Add(text + "@" + answered);
			}
		}

		public void AbilityDenied()
		{
			Play("ab_denied", 0.4f, 0.05f);
		}

		public void AbilityReady()
		{
			Play("ab_ready", 0.28f, 0f);
		}

		public void AbilityHold(Ability aiming, float dt)
		{
			string text = SoundsOf(aiming)?.Hold;
			if (text != _holdKey)
			{
				if (_holdPlaying)
				{
					FadeOut(_holdChannel, 0.12f);
				}
				_holdPlaying = false;
				_holdKey = text;
				_holdLevel = 0f;
				if (text != null)
				{
					_holdChannel = PlayChannel(text, 0f, 0f);
					_holdPlaying = _holdChannel.hasHandle();
					if (_holdPlaying && _abilityLog.Count < 32)
					{
						_abilityLog.Add(text);
					}
				}
			}
			if (_holdPlaying)
			{
				_holdLevel = Mathf.MoveTowards(_holdLevel, 1f, dt / 0.5f);
				_holdChannel.setVolume(Gain(_holdLevel) * ((text == "ab_hold_fuse") ? 0.4f : 0.3f));
			}
		}

		public void AbilityHeard(Ability ab, Actor by, float distance, float pan)
		{
			AbilitySounds abilitySounds = SoundsOf(ab);
			if (abilitySounds == null)
			{
				return;
			}
			float num = Mathf.Clamp01(1f - distance / 30f);
			float num2 = num * num * 0.8f;
			if (!(num2 < 0.02f))
			{
				string kind = KindOf(by);
				pan = Mathf.Clamp(pan, -0.85f, 0.85f);
				Vector3? at = null;
				try
				{
					Vector2 current_position = by.current_position;
					at = new Vector3(current_position.x, current_position.y, ((GroundAt != null) ? GroundAt(current_position.x, current_position.y) : 0f) + 0.8f + by.position_height);
				}
				catch
				{
				}
				if (abilitySounds.Use != null)
				{
					PlayAbility(ForKind(abilitySounds.Use, kind, IsWoman(by)), abilitySounds.UseVolume * num2, pan, at);
				}
				if (abilitySounds.Land != null)
				{
					PlayAbility(ForKind(abilitySounds.Land, kind, IsWoman(by)), abilitySounds.LandVolume * num2, pan, at);
				}
			}
		}

		private void BeginAmbience()
		{
			_birds.Clear();
			_night.Clear();
			_frogs.Clear();
			_spawnTimer = 1f;
			FMOD.System coreSystem = RuntimeManager.CoreSystem;
			if (!_haveAmbience && coreSystem.createChannelGroup("first_person_ambience", out _ambience) == RESULT.OK)
			{
				_haveAmbience = true;
			}
			if (_haveAmbience && !_haveAmbienceReverb && coreSystem.createDSPByType(DSP_TYPE.SFXREVERB, out _ambienceReverb) == RESULT.OK)
			{
				if (_ambience.addDSP(-1, _ambienceReverb) != RESULT.OK)
				{
					_ambienceReverb.release();
					return;
				}
				_haveAmbienceReverb = true;
				P(DSP_SFXREVERB.DECAYTIME, 1500f);
				P(DSP_SFXREVERB.EARLYDELAY, 18f);
				P(DSP_SFXREVERB.LATEDELAY, 30f);
				P(DSP_SFXREVERB.HFREFERENCE, 5000f);
				P(DSP_SFXREVERB.HFDECAYRATIO, 45f);
				P(DSP_SFXREVERB.DIFFUSION, 100f);
				P(DSP_SFXREVERB.DENSITY, 100f);
				P(DSP_SFXREVERB.LOWSHELFFREQUENCY, 250f);
				P(DSP_SFXREVERB.LOWSHELFGAIN, 0f);
				P(DSP_SFXREVERB.HIGHCUT, 7000f);
				P(DSP_SFXREVERB.EARLYLATEMIX, 60f);
				P(DSP_SFXREVERB.WETLEVEL, -13f);
				P(DSP_SFXREVERB.DRYLEVEL, 0f);
			}
			void P(DSP_SFXREVERB p, float v)
			{
				_ambienceReverb.setParameterFloat((int)p, v);
			}
		}

		private void EndAmbience()
		{
			StopLoop(ref _cricketChannel, ref _cricketsPlaying, 0.8f);
			_cricketVolume = 0f;
			_birds.Clear();
			_night.Clear();
			_frogs.Clear();
		}

		private void UpdateAmbience(State s, float dt)
		{
			Habitat habitat = s.Habitat;
			if (!_haveAmbience || habitat == null || AmbientVolume <= 0.001f)
			{
				_birds.Clear();
				_night.Clear();
				_frogs.Clear();
				StopLoop(ref _cricketChannel, ref _cricketsPlaying);
				_cricketVolume = 0f;
				return;
			}
			float num = 1f - Smooth(0.3f, 0.75f, s.Night);
			float num2 = Smooth(0.45f, 0.85f, s.Night);
			float num3 = Mathf.Max(0f, 1f - Mathf.Abs(s.Hour - 0.28f) / 0.07f);
			float num4 = Mathf.Max(0f, 1f - Mathf.Abs(s.Hour - 0.71f) / 0.06f);
			float num5 = 1f - Mathf.Clamp01(s.Rain * 1.5f);
			float num6 = habitat.Birds * num * (0.6f + 0.4f * num3 + 0.2f * num4) * num5;
			int num7 = Mathf.RoundToInt(5f * num6);
			int num8 = ((num2 > 0.6f && habitat.Perches.Count >= 5 && num5 > 0.5f) ? 1 : 0);
			int num9 = Mathf.RoundToInt(4f * habitat.Frogs * (0.3f + 0.7f * num2));
			_spawnTimer -= dt;
			if (_spawnTimer <= 0f)
			{
				_spawnTimer = 1.5f + (float)_rng.NextDouble() * 3.5f;
				if (_birds.Count < num7)
				{
					Arrive(_birds, PickDayKind(habitat), habitat, s);
				}
				else if (_frogs.Count < num9)
				{
					Arrive(_frogs, (_rng.NextDouble() < 0.35) ? Peeper : Croaker, habitat, s);
				}
				else if (_night.Count < num8 && _rng.NextDouble() < 0.3)
				{
					Arrive(_night, Owl, habitat, s);
				}
			}
			Sing(_birds, num7, s);
			Sing(_frogs, num9, s);
			Sing(_night, num8, s);
			float num10 = Mathf.Clamp01((float)(habitat.Perches.Count - 8) / 10f);
			float num11 = _clock % 39f;
			float num12 = ((num11 < 12f) ? (Smooth(0f, 2.5f, num11) * (1f - Smooth(8.5f, 12f, num11))) : 0f);
			float b = 0.045f * num2 * habitat.Insects * num5 * num10 * num12;
			_cricketVolume = Mathf.Lerp(_cricketVolume, b, 1f - Mathf.Exp(-2f * dt));
			Loop("crickets", ref _cricketChannel, ref _cricketsPlaying, _cricketVolume, _ambience);
		}

		private Kind PickDayKind(Habitat h)
		{
			bool flag = h.Perches.Count > 0;
			float num = 0f;
			Kind[] dayKinds = DayKinds;
			foreach (Kind kind in dayKinds)
			{
				if (flag || kind.OnGround)
				{
					num += kind.Weight;
				}
			}
			float num2 = (float)_rng.NextDouble() * num;
			dayKinds = DayKinds;
			foreach (Kind kind2 in dayKinds)
			{
				if (flag || kind2.OnGround)
				{
					num2 -= kind2.Weight;
					if (num2 <= 0f)
					{
						return kind2;
					}
				}
			}
			return DayKinds[0];
		}

		private void Arrive(List<Singer> into, Kind kind, Habitat h, State s)
		{
			List<Vector3> list = ((kind.Perch == Perch.Marsh) ? h.Marsh : h.Perches);
			if (kind.Perch == Perch.Tree && kind.OnGround && (list.Count == 0 || _rng.NextDouble() < 0.2) && h.Meadow.Count > 0)
			{
				list = h.Meadow;
			}
			bool flag = list == h.Perches;
			if (list.Count == 0)
			{
				return;
			}
			Vector3 at = Vector3.zero;
			bool flag2 = false;
			for (int i = 0; i < 8; i++)
			{
				if (flag2)
				{
					break;
				}
				at = list[_rng.Next(list.Count)];
				float magnitude = new Vector2(at.x - s.Position.x, at.y - s.Position.y).magnitude;
				flag2 = magnitude > 3.5f && magnitude < 32f && !Taken(at);
			}
			if (flag2)
			{
				int num = Mathf.Max(1, kind.Songs);
				int num2 = kind.First + _rng.Next(num);
				into.Add(new Singer
				{
					Kind = kind,
					SongA = num2,
					SongB = kind.First + (num2 - kind.First + 1 + _rng.Next(Mathf.Max(1, num - 1))) % num,
					Song = num2,
					Pitch = 0.94f + (float)_rng.NextDouble() * 0.12f,
					At = at,
					Height = (flag ? at.z : ((kind.Perch == Perch.Marsh) ? 0.1f : 0.3f)),
					Next = _clock + 0.3f + (float)_rng.NextDouble() * 2.5f,
					Left = kind.BoutMin + _rng.Next(kind.BoutMax - kind.BoutMin + 1),
					Bouts = 1 + _rng.Next(3)
				});
			}
		}

		private bool Taken(Vector3 at)
		{
			if (!Near(_birds, at) && !Near(_night, at))
			{
				return Near(_frogs, at);
			}
			return true;
		}

		private static bool Near(List<Singer> list, Vector3 at)
		{
			foreach (Singer item in list)
			{
				if ((item.At - at).sqrMagnitude < 1f)
				{
					return true;
				}
			}
			return false;
		}

		private void Sing(List<Singer> singers, int wanted, State s)
		{
			for (int num = singers.Count - 1; num >= 0; num--)
			{
				Singer singer = singers[num];
				float num2 = singer.At.x - s.Position.x;
				float num3 = singer.At.y - s.Position.y;
				float num4 = Mathf.Sqrt(num2 * num2 + num3 * num3);
				if (num4 > 45f)
				{
					singers.RemoveAt(num);
				}
				else if (!(_clock < singer.Next))
				{
					Kind kind = singer.Kind;
					if (singer.Left <= 0)
					{
						singer.Bouts--;
						if (singer.Bouts <= 0 || singers.Count > wanted)
						{
							singers.RemoveAt(num);
						}
						else
						{
							singer.Left = kind.BoutMin + _rng.Next(kind.BoutMax - kind.BoutMin + 1);
							singer.Song = ((_rng.NextDouble() < 0.5) ? singer.SongA : singer.SongB);
							singer.Next = _clock + 6f + (float)_rng.NextDouble() * 14f;
						}
					}
					else
					{
						singer.Left--;
						float num5 = Mathf.Cos(s.Yaw);
						float num6 = Mathf.Sin(s.Yaw);
						float num7 = 1f / Mathf.Max(0.01f, num4);
						float num8 = (num2 * num6 - num3 * num5) * num7;
						float num9 = (num2 * num5 + num3 * num6) * num7;
						float volume = 0.2f * kind.Loud / (1f + Mathf.Pow((num4 + 1f) / 6f, 1.3f)) * (_spatial ? 1f : (0.85f + 0.15f * num9));
						float num10 = singer.Pitch * (1f + ((float)_rng.NextDouble() - 0.5f) * 0.02f);
						float num11 = ((GroundAt != null) ? GroundAt(singer.At.x, singer.At.y) : 0f);
						float num12 = PlayAmbient(kind.Key, singer.Song, volume, num10, Mathf.Clamp(num8 * 0.8f, -0.8f, 0.8f), new Vector3(singer.At.x, singer.At.y, num11 + singer.Height));
						singer.Next = _clock + num12 / num10 + kind.GapMin + (float)_rng.NextDouble() * (kind.GapMax - kind.GapMin);
					}
				}
			}
		}

		private float PlayAmbient(string key, int variant, float volume, float pitch, float pan, Vector3 at)
		{
			if (!_haveAmbience || volume <= 0.0005f || !_sounds.TryGetValue(key, out var value) || value.Length == 0)
			{
				return 1f;
			}
			variant = Mathf.Clamp(variant, 0, value.Length - 1);
			float[] value2;
			float result = ((_lengths.TryGetValue(key, out value2) && variant < value2.Length) ? value2[variant] : 1f);
			if (_spatial)
			{
				PlayAt(key, at, volume, 0f, pitch, variant, ambient: true);
				return result;
			}
			if (RuntimeManager.CoreSystem.playSound(value[variant], _ambience, paused: true, out var channel) != RESULT.OK)
			{
				return result;
			}
			channel.setVolume(volume);
			channel.setPitch(pitch);
			channel.setPan(pan);
			channel.setPaused(paused: false);
			return result;
		}

		private static float Smooth(float e0, float e1, float x)
		{
			float num = Mathf.Clamp01((x - e0) / (e1 - e0));
			return num * num * (3f - 2f * num);
		}

		private void UpdateArrowImpacts(State s)
		{
			StuckArrows.TakeImpacts(_impacts);
			if (_impacts.Count == 0)
			{
				return;
			}
			Vector2 you = s.Position;
			_impacts.Sort((StuckArrows.Impact a, StuckArrows.Impact b) => Distance2(a.At, you).CompareTo(Distance2(b.At, you)));
			for (int num = 0; num < _impacts.Count && num < 4; num++)
			{
				StuckArrows.Impact impact = _impacts[num];
				float num2 = impact.At.X - you.x;
				float num3 = impact.At.Y - you.y;
				float num4 = Mathf.Sqrt(num2 * num2 + num3 * num3);
				if (!(num4 > 36f))
				{
					float num5 = 1f / (1f + Mathf.Pow(num4 / 5f, 1.4f));
					if (s.Indoors)
					{
						num5 *= 0.45f;
					}
					Vector3 at = new Vector3(impact.At.X, impact.At.Y, impact.At.Z);
					switch (impact.What)
					{
					case StuckArrows.Struck.Wood:
						PlayAt("arrow_hit", at, 0.85f * num5, 0.07f);
						break;
					case StuckArrows.Struck.Body:
						PlayAt("arrow_body", at, 0.95f * num5, 0.07f);
						break;
					default:
						PlayAt("arrow_ground", at, 0.55f * num5, 0.08f);
						break;
					}
					ArrowImpactsPlayed++;
					continue;
				}
				break;
			}
		}

		private static float Distance2(Vec3 p, Vector2 you)
		{
			float num = p.X - you.x;
			float num2 = p.Y - you.y;
			return num * num + num2 * num2;
		}

		public static void QueueRumble(float distance)
		{
			float num = Mathf.Clamp01(1.25f - distance / 260f);
			if (num <= 0.02f)
			{
				return;
			}
			lock (RumbleLock)
			{
				Rumbles.Add(new Rumble
				{
					Delay = Mathf.Min(4f, distance / 70f),
					Volume = num
				});
			}
		}

		private static void MakeBlastSounds(Action<string, int, Func<int, float[]>> add)
		{
			add("blaster_hiss", 2, Synth.BlasterHiss);
			add("blaster_ratchet", 3, Synth.BlasterRatchet);
			add("blaster_alarm", 1, Synth.BlasterAlarm);
			add("blaster_dry", 2, Synth.BlasterDry);
			add("nuke_rumble", 2, Synth.NukeRumble);
		}

		public void PlayBlaster(string key, float volume)
		{
			Play(key, volume, 0.05f);
		}

		private void UpdateBlasts(State s)
		{
			lock (RumbleLock)
			{
				for (int i = 0; i < Rumbles.Count; i++)
				{
					_pending.Add(new Pending
					{
						Key = "nuke_rumble",
						At = _clock + Rumbles[i].Delay,
						Volume = Rumbles[i].Volume * (s.Indoors ? 0.6f : 1f)
					});
				}
				Rumbles.Clear();
			}
		}

		private static void MakeBlockSounds(Action<string, int, Func<int, float[]>> add)
		{
			add("clash_metal", 4, Synth.ClashMetal);
			add("clash_wood", 3, Synth.ClashWood);
			add("clash_flesh", 3, Synth.ClashFlesh);
			add("clash_tink", 3, Synth.ClashTink);
			add("parry", 2, Synth.ParryRing);
			add("headshot", 3, Synth.Headshot);
		}

		private void UpdateStops(State s)
		{
			if (_headshotsHeard < 0)
			{
				_headshotsHeard = Headshots.Landed;
			}
			if (Headshots.Landed != _headshotsHeard)
			{
				_headshotsHeard = Headshots.Landed;
				Play("headshot", 0.72f, 0.04f);
				HeadshotsPlayed++;
			}
			if (_stopsHeard < 0)
			{
				_stopsHeard = Blocks.Newest;
			}
			if (Blocks.Newest == _stopsHeard)
			{
				return;
			}
			float num = Mathf.Cos(s.Yaw);
			float num2 = Mathf.Sin(s.Yaw);
			int num3 = 0;
			List<BlockEvent> events = Blocks.Events;
			for (int i = 0; i < events.Count; i++)
			{
				BlockEvent blockEvent = events[i];
				if (blockEvent.Serial <= _stopsHeard)
				{
					continue;
				}
				_stopsHeard = blockEvent.Serial;
				if (blockEvent.Age > 0.4f || num3 >= 3)
				{
					continue;
				}
				float pan = 0f;
				Vector3? at = null;
				float num4;
				if (blockEvent.OnYou)
				{
					num4 = 1f;
					pan = Mathf.Clamp(blockEvent.Toward.x * num2 - blockEvent.Toward.y * num, -1f, 1f) * 0.3f;
				}
				else
				{
					float num5 = blockEvent.At.X - s.Position.x;
					float num6 = blockEvent.At.Y - s.Position.y;
					float num7 = Mathf.Sqrt(num5 * num5 + num6 * num6);
					if (num7 > 36f)
					{
						continue;
					}
					num4 = 1f / (1f + Mathf.Pow(num7 / (blockEvent.YourBlow ? 6f : 5f), 1.4f));
					if (s.Indoors)
					{
						num4 *= 0.45f;
					}
					at = new Vector3(blockEvent.At.X, blockEvent.At.Y, blockEvent.At.Z);
				}
				float num8 = ((blockEvent.YourBlow || blockEvent.OnYou) ? 0.78f : 0.6f);
				float pitch = 1f - 0.12f * blockEvent.Weight;
				string text;
				switch (blockEvent.Kind)
				{
				case Stop.Dodged:
					if (!blockEvent.YourBlow)
					{
						PlayTuned("swing_light", 0.42f * num8 * num4, 1.12f, pan, at);
					}
					text = "cloth";
					PlayTuned(text, 0.5f * num8 * num4, 1f, pan, at);
					break;
				case Stop.Parried:
					text = "parry";
					PlayTuned(text, 0.85f * num4, pitch, pan, at);
					break;
				default:
					text = ((blockEvent.Material != Clash.Metal) ? ((blockEvent.Material == Clash.Wood) ? "clash_wood" : "clash_flesh") : (blockEvent.Shot ? "clash_tink" : "clash_metal"));
					PlayTuned(text, num8 * num4 * (blockEvent.Shot ? 0.8f : 1f), pitch, pan, at);
					break;
				}
				LastStopSound = text;
				StopsPlayed++;
				num3++;
			}
		}

		private void PlayTuned(string key, float volume, float pitch, float pan, Vector3? at = null)
		{
			if (volume <= 0.001f)
			{
				return;
			}
			if (at.HasValue)
			{
				PlayAt(key, at.Value, volume, 0.04f, pitch);
				return;
			}
			Channel channel = PlayChannel(key, volume, 0.04f);
			if (channel.hasHandle())
			{
				if (pitch != 1f && channel.getPitch(out var pitch2) == RESULT.OK)
				{
					channel.setPitch(pitch2 * pitch);
				}
				if (pan != 0f)
				{
					channel.setPan(pan);
				}
			}
		}

		internal static byte ArmorOf(Actor a)
		{
			try
			{
				if (a == null || a.equipment == null)
				{
					return 0;
				}
				string text = People.ChestArmor(a) ?? People.ItemIdOf(a.equipment.boots);
				if (string.IsNullOrEmpty(text))
				{
					return 0;
				}
				return (byte)(text.Contains("leather") ? 1 : 2);
			}
			catch
			{
				return 0;
			}
		}

		internal static float VoiceOf(Actor a)
		{
			string text = null;
			try
			{
				text = ((a != null && a.asset != null) ? a.asset.id : null);
			}
			catch
			{
			}
			switch (text)
			{
			case "orc":
				return 0.84f;
			case "dwarf":
				return 0.92f;
			case "elf":
				return 1.08f;
			default:
				return 1f;
			}
		}

		private void ArmorStep(State s, float stride, float pan)
		{
			if (s.Armor != 0 && !s.Shallows)
			{
				float num = stride * Mathf.Lerp(0.7f, 1.3f, s.Run) * StepVolume;
				if (s.Armor == 2)
				{
					Play("armor_plate", 0.15f * num, 0.07f, pan);
				}
				else
				{
					Play("armor_leather", 0.11f * num, 0.07f, pan);
				}
			}
		}

		private void Drags(State s, float dt, string ground, float tread, float weight)
		{
			_sinceScuff += dt;
			_sinceSkid += dt;
			if (dt <= 0f || s.Swimming || s.Airborne || s.Dead || s.Full <= 0f)
			{
				_haveDragVel = false;
				_braking = 0f;
				return;
			}
			Vector2 velocity = s.Velocity;
			if (!_haveDragVel)
			{
				_dragVel = velocity;
				_haveDragVel = true;
				return;
			}
			Vector2 dragVel = _dragVel;
			_dragVel = Vector2.Lerp(_dragVel, velocity, 1f - Mathf.Exp((0f - dt) / 0.05f));
			Vector2 vector = (_dragVel - dragVel) / dt;
			float magnitude = _dragVel.magnitude;
			if (magnitude < 0.05f)
			{
				_braking = 0f;
				return;
			}
			Vector2 vector2 = _dragVel / magnitude;
			float num = 0f - Vector2.Dot(vector, vector2);
			float magnitude2 = (vector - Vector2.Dot(vector, vector2) * vector2).magnitude;
			float full = s.Full;
			string text = (s.Shallows ? "shallows" : ground);
			_braking = ((num > 0.8f * full && magnitude > 1.1f * full) ? (_braking + dt) : 0f);
			if (_braking > 0.06f && _sinceSkid > 0.9f)
			{
				Play("skid_" + text, 0.4f * StepVolume * weight, 0.06f, 0f, tread);
				_sinceSkid = (_sinceScuff = 0f);
				return;
			}
			bool num2 = num > 1.2f * full && magnitude > 0.75f * full;
			bool flag = magnitude2 > 1.5f * full && magnitude > 0.75f * full;
			if ((num2 || flag) && _sinceScuff > 0.45f && _sinceSkid > 0.5f)
			{
				Play("scuff_" + text, 0.3f * StepVolume * weight, 0.07f, 0f, tread);
				_sinceScuff = 0f;
			}
		}

		private static void MakeMore(Dictionary<string, List<float[]>> all)
		{
			BowState bow = new BowState();
			Add("bow_pull", 3, (int v) => Synth.BowDraw(v, bow.DrawTime));
			Add("bow_loose", 4, Synth.BowLoose);
			Add("bow_letdown", 2, Synth.BowLetDown);
			Add("bow_nock", 3, Synth.BowNock);
			Add("arrow_whizz", 4, Synth.ArrowWhizz);
			Add("arrow_hit", 3, Synth.ArrowHit);
			Add("arrow_ground", 3, Synth.ArrowGround);
			Add("arrow_body", 3, Synth.ArrowBody);
			Add("arrow_confirm", 3, Synth.ArrowConfirm);
			MakeDragonSounds(Add);
			MakeBlastSounds(Add);
			MakeShotgunSounds(Add);
			MakeAbilitySounds(Add);
			MakeUfoSounds(Add);
			MakeWonderSounds(Add);
			MakePingSounds(Add);
			MakeCompassSounds(Add);
			MakePickupSounds(Add);
			MakeBlockSounds(Add);
			MakeWindUpSounds(Add);
			MakeIndoorSounds(Add);
			void Add(string key, int variants, Func<int, float[]> make)
			{
				List<float[]> list = new List<float[]>();
				for (int i = 0; i < variants; i++)
				{
					list.Add(make(i));
				}
				all[key] = list;
			}
		}

		public void Bow(BowState b)
		{
			if (_on && !_failed && b != null)
			{
				if (b.StartedDraw)
				{
					StopDraw();
					_drawChannel = PlayChannel("bow_pull", 0.55f, 0.03f);
					_drawPlaying = _drawChannel.hasHandle();
				}
				if (b.LetDown)
				{
					StopDraw();
					Play("bow_letdown", 0.4f, 0.05f);
				}
				if (b.Fired)
				{
					StopDraw();
					Play("bow_loose", Mathf.Lerp(0.55f, 0.85f, b.LastShotDraw), 0.04f);
					_nockPlayed = false;
				}
				if (b.State == BowState.Phase.Loosed && !_nockPlayed && b.Time >= 0.44f)
				{
					_nockPlayed = true;
					Play("bow_nock", 0.42f, 0.05f);
				}
			}
		}

		private void StopDraw()
		{
			if (_drawPlaying)
			{
				FadeOut(_drawChannel, 0.04f);
			}
			_drawPlaying = false;
		}

		public void Whizz(float pan, float closeness)
		{
			Play("arrow_whizz", Mathf.Lerp(0.25f, 0.75f, closeness), 0.08f, Mathf.Clamp(pan, -0.9f, 0.9f));
		}

		public void ArrowLanded(float pan, float closeness)
		{
			Play("arrow_ground", 0.35f * closeness, 0.08f, Mathf.Clamp(pan, -0.8f, 0.8f));
		}

		private Channel PlayChannel(string key, float volume, float pitchJitter)
		{
			if (!_on || _failed || !_haveGroup || !_sounds.TryGetValue(key, out var value) || value.Length == 0)
			{
				return default(Channel);
			}
			_lastVariant.TryGetValue(key, out var value2);
			int num = _rng.Next(value.Length);
			if (value.Length > 1 && num == value2)
			{
				num = (num + 1) % value.Length;
			}
			_lastVariant[key] = num;
			if (RuntimeManager.CoreSystem.playSound(value[num], _group, paused: true, out var channel) != RESULT.OK)
			{
				return default(Channel);
			}
			channel.setVolume(volume);
			channel.setPitch(1f + (float)(_rng.NextDouble() * 2.0 - 1.0) * pitchJitter);
			channel.setPaused(paused: false);
			return channel;
		}

		private static void MakeCompassSounds(Action<string, int, Func<int, float[]>> add)
		{
			add("compass_open", 3, Synth.CompassOpen);
			add("compass_shut", 3, Synth.CompassShut);
			add("compass_tick", 2, Synth.CompassTick);
		}

		public void CrabHum(int claw, Vector3 at)
		{
			_crabAsked[claw] = Time.frameCount;
			_crabAt[claw] = at;
		}

		public bool CrabHumPlaying(int claw)
		{
			if (!_crabHum[claw].isValid())
			{
				return false;
			}
			_crabHum[claw].getPlaybackState(out var state);
			return state == PLAYBACK_STATE.PLAYING;
		}

		private void UpdateCrabHum()
		{
			for (int i = 0; i < 2; i++)
			{
				if (Time.frameCount - _crabAsked[i] > 1 || !MusicBox.sounds_on || _crabFailed)
				{
					StopCrabHum(i);
					continue;
				}
				try
				{
					if (!_crabHum[i].isValid())
					{
						_crabHum[i] = RuntimeManager.CreateInstance("event:/SFX/UNIQUE/Crabzilla/CrabzillaLazer");
					}
					HoldEventAt(_crabHum[i], _crabAt[i]);
					bool flag = !_crabHumming[i];
					if (!flag && Time.unscaledTime - _crabStartedAt[i] > 0.5f)
					{
						_crabHum[i].getPlaybackState(out var state);
						flag = state == PLAYBACK_STATE.STOPPED;
					}
					if (flag)
					{
						_crabHum[i].start();
						_crabHumming[i] = true;
						_crabStartedAt[i] = Time.unscaledTime;
						CrabHumStarts++;
					}
				}
				catch (Exception ex)
				{
					_crabFailed = true;
					Log.Error("crab laser sound: " + ex.Message);
				}
			}
		}

		private void StopCrabHum(int claw)
		{
			if (!_crabHumming[claw])
			{
				return;
			}
			_crabHumming[claw] = false;
			try
			{
				_crabHum[claw].stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
			}
			catch
			{
			}
		}

		private void EndCrabHum()
		{
			StopCrabHum(0);
			StopCrabHum(1);
		}

		private void HoldEventAt(FMOD.Studio.EventInstance instance, Vector3 at)
		{
			if (Fresh)
			{
				GameSound gameSound = null;
				for (int i = 0; i < _gameSounds.Count; i++)
				{
					if (gameSound != null)
					{
						break;
					}
					if (_gameSounds[i].Instance.handle == instance.handle)
					{
						gameSound = _gameSounds[i];
					}
				}
				if (gameSound != null && !gameSound.Native)
				{
					gameSound.At = at;
					return;
				}
				if (gameSound == null && _gameSounds.Count < 96)
				{
					GameSound gameSound2 = new GameSound
					{
						Instance = instance,
						At = at
					};
					PlaceAhead(gameSound2);
					_gameSounds.Add(gameSound2);
					return;
				}
			}
			instance.set3DAttributes(SpatialHooks.Attributes(new Vector3(at.x, at.y, _studioEar.z + (_earHeight - at.z))));
		}

		public static void Prepare()
		{
			lock (MadeLock)
			{
				if (_making || _made != null)
				{
					return;
				}
				_making = true;
			}
			System.Threading.Thread thread = new System.Threading.Thread((ThreadStart)delegate
			{
				Dictionary<string, List<byte[]>> dictionary = null;
				Stopwatch stopwatch = Stopwatch.StartNew();
				try
				{
					dictionary = ToPcm16(MakeAll());
				}
				catch (Exception ex)
				{
					_madeError = ex.ToString();
				}
				lock (MadeLock)
				{
					_made = dictionary ?? new Dictionary<string, List<byte[]>>();
					_making = false;
					_madeMs = stopwatch.ElapsedMilliseconds;
				}
			});
			thread.IsBackground = true;
			thread.Name = "FirstPerson sounds";
			thread.Start();
			PrepareMusic();
		}

		public void Begin()
		{
			if (_on)
			{
				return;
			}
			_on = true;
			_lastStep = (_lastStroke = (_lastAttacks = int.MinValue));
			_lastHits = (_lastKills = (_lastHurts = int.MinValue));
			_lastLandings = (_lastJumps = int.MinValue);
			_haveDragVel = false;
			_haveWeapon = false;
			_room = (_roomSet = -1f);
			_pending.Clear();
			Prepare();
			try
			{
				GameObject gameObject = ((MusicBox.inst != null) ? MusicBox.inst._camera_listener : null);
				_listener = ((gameObject != null) ? gameObject.GetComponent<StudioListener>() : null);
				if (_listener != null)
				{
					_listener.enabled = false;
				}
				MoveCamera move_camera = World.world.move_camera;
				if (move_camera != null)
				{
					_savedZoom = move_camera.getTargetZoom();
				}
				if (!_haveGroup && RuntimeManager.CoreSystem.createChannelGroup("first_person_body", out _group) == RESULT.OK)
				{
					_haveGroup = true;
				}
				if (_haveGroup && !_haveReverb && RuntimeManager.CoreSystem.createDSPByType(DSP_TYPE.SFXREVERB, out _reverb) == RESULT.OK)
				{
					if (_group.addDSP(-1, _reverb) == RESULT.OK)
					{
						_haveReverb = true;
					}
					else
					{
						_reverb.release();
					}
				}
				BeginAmbience();
				BeginMusic();
				BeginSpatial();
				Walls = this;
				Speech = this;
			}
			catch (Exception e)
			{
				Fail(e);
			}
		}

		public void End()
		{
			if (!_on)
			{
				return;
			}
			_on = false;
			try
			{
				if (_listener != null)
				{
					_listener.enabled = true;
				}
				MoveCamera moveCamera = ((World.world != null) ? World.world.move_camera : null);
				if (moveCamera != null && _savedZoom > 0f)
				{
					moveCamera.setTargetZoom(_savedZoom);
				}
				StopLoop(ref _windChannel, ref _windPlaying, 0.8f);
				StopLoop(ref _fireChannel, ref _firePlaying, 0.8f);
				StopLoop(ref _rainChannel, ref _rainPlaying, 0.8f);
				EndCrabHum();
				StopLoop(ref _ufoChannel, ref _ufoPlaying, 0.5f);
				EndDragons();
				EndWonders();
				if (Speech == this)
				{
					Speech = null;
				}
				EndSpeech();
				EndSpatial();
				EndAmbience();
				EndMusic();
				if (Walls == this)
				{
					Walls = null;
				}
				EndIndoors();
				_masterWant = 1f;
			}
			catch (Exception ex)
			{
				Log.Error("audio restore failed: " + ex.Message);
			}
			finally
			{
				try
				{
					SetMuffled(on: false, unlock: true);
				}
				catch (Exception ex2)
				{
					Log.Error("audio unmuffle failed: " + ex2.Message);
				}
			}
		}

		public void Update(State s, float dt)
		{
			if (!_on || _failed)
			{
				return;
			}
			try
			{
				if (_sounds.Count == 0)
				{
					CreateSounds();
				}
				if (_lockedBuses.Count == 0 && !s.Indoors && Time.unscaledTime >= _lockTryAt)
				{
					_lockTryAt = Time.unscaledTime + 5f;
					try
					{
						LockTopBuses();
					}
					catch (Exception ex)
					{
						Log.Error("audio: locking the game's buses: " + ex.Message);
					}
				}
				UpdateListener(s);
				UpdateSpatial(s, dt, _sfxVolume);
				UpdateCrabHum();
				UpdateSpace(s.Indoors, dt);
				UpdateBody(s, dt);
				UpdateLoops(s, dt);
				UpdateIndoorWeather(s, dt);
				UpdateAmbience(s, dt);
				UpdateDragons(s, dt);
				UpdateUfos(s, dt);
				UpdateWonders(s, dt);
				UpdateArrowImpacts(s);
				UpdateStops(s);
				UpdateBlasts(s);
				UpdateMusic(s, dt);
				UpdateSpeech(dt);
				_clock += dt;
				for (int num = _pending.Count - 1; num >= 0; num--)
				{
					if (_pending[num].At <= _clock)
					{
						Play(_pending[num].Key, _pending[num].Volume, 0.04f);
						_pending.RemoveAt(num);
					}
				}
				UpdateMuffle(s.Indoors, dt);
			}
			catch (Exception e)
			{
				Fail(e);
			}
		}

		public void PlayDoor()
		{
			Play("door", 0.7f, 0.05f);
		}

		public void PlayRustle()
		{
			Play("rustle", 0.5f, 0.1f);
		}

		public void PlayHorn()
		{
			if (!(Time.unscaledTime - _hornAt < 1.5f))
			{
				_hornAt = Time.unscaledTime;
				Play("horn", 0.85f, 0f);
			}
		}

		public void PlayLeap()
		{
			Play("cast", 0.55f, 0.08f);
		}

		public void PlayEffect(string key, float volume)
		{
			Play(key, volume, 0.08f);
		}

		private void UpdateListener(State s)
		{
			MoveCamera move_camera = World.world.move_camera;
			if (move_camera != null && Mathf.Abs(move_camera.getTargetZoom() - 11f) > 0.01f)
			{
				move_camera.setTargetZoom(11f);
			}
			ATTRIBUTES_3D attributes = new ATTRIBUTES_3D
			{
				position = new Vector3(s.Position.x, s.Position.y, 0f - Mathf.Max(0.2f, s.EyeAboveGround)).ToFMODVector(),
				velocity = Vector3.zero.ToFMODVector(),
				forward = new Vector3(Mathf.Cos(s.Yaw), Mathf.Sin(s.Yaw), 0f).ToFMODVector(),
				up = new Vector3(0f, 0f, -1f).ToFMODVector()
			};
			RuntimeManager.StudioSystem.setListenerAttributes(0, attributes);
			float num = (MusicBox.sounds_on ? 1f : 0f);
			try
			{
				if (MusicBox.inst != null && MusicBox.inst._vca_sound_effects.isValid() && MusicBox.inst._vca_sound_effects.getVolume(out var volume) == RESULT.OK)
				{
					num *= volume;
				}
			}
			catch
			{
			}
			_sfxVolume = num;
			if (_haveGroup)
			{
				_group.setVolume(num * EffectsVolume * _dip);
			}
			if (_haveAmbience)
			{
				_ambience.setVolume(num * AmbientVolume * (1f - _muffle) * _dip);
			}
			SetMaster(MasterVolume);
		}

		private void SetMaster(float volume)
		{
			volume = Mathf.Clamp01(volume);
			if (Mathf.Abs(volume - _masterSet) < 0.001f)
			{
				return;
			}
			try
			{
				if (RuntimeManager.CoreSystem.getMasterChannelGroup(out var channelgroup) == RESULT.OK && channelgroup.setVolume(volume) == RESULT.OK)
				{
					_masterSet = volume;
				}
			}
			catch
			{
			}
		}

		private void UpdateSpace(bool indoors, float dt)
		{
			if (!_haveReverb)
			{
				return;
			}
			float num = (indoors ? 1f : 0f);
			_room = ((_room < 0f) ? num : Mathf.MoveTowards(_room, num, Mathf.Min(dt, 0.05f) / 0.5f));
			if (_room != _roomSet && (!(Mathf.Abs(_room - _roomSet) < 0.04f) || _room == num))
			{
				_roomSet = _room;
				float t = Gain(_room);
				for (int i = 0; i < RoomParams.Length; i++)
				{
					_reverb.setParameterFloat((int)RoomParams[i], Mathf.Lerp(OpenAir[i], SmallRoom[i], t));
				}
			}
		}

		private void UpdateBody(State s, float dt)
		{
			if (s.Dead)
			{
				return;
			}
			_sinceStep += dt;
			string text = GroundKey(s.Ground, s.Indoors);
			float num = ((s.Heavy > 0f) ? s.Heavy : 1f);
			float num2 = Mathf.Clamp(1f / Mathf.Sqrt(Mathf.Sqrt(num)), 0.82f, 1.22f);
			float num3 = Mathf.Clamp(0.7f + 0.3f * num, 0.65f, 1.35f);
			if (!s.Swimming && !s.Airborne && _lastStep != int.MinValue && s.Steps != _lastStep && s.Stride > 0.3f)
			{
				string key = (s.Shallows ? "wade" : ("step_" + text));
				float num4 = Mathf.Clamp01(s.Stride);
				float volume = (s.Shallows ? 0.45f : Mathf.Lerp(0.26f, 0.38f, s.Run)) * num4 * StepVolume * num3 * (1f - 0.5f * Mathf.Clamp01(s.Crouch));
				float num5 = (((s.Steps & 1) == 0) ? (-0.1f) : 0.1f);
				Play(key, volume, 0.04f, num5, num2);
				if (s.Run > 0.4f && !s.Shallows)
				{
					Play("cloth", 0.22f * s.Run * num4 * StepVolume, 0.08f, 0f - num5);
				}
				ArmorStep(s, num4, 0f - num5);
				_sinceStep = 0f;
			}
			_lastStep = s.Steps;
			if (s.Airborne)
			{
				_airTime += dt;
			}
			if (_wasAirborne && !s.Airborne && !s.Swimming)
			{
				bool flag = s.Landings != _lastLandings && _lastLandings != int.MinValue;
				float t = (flag ? Mathf.Clamp01((s.LandSpeed - 2.5f) / 5f) : Mathf.Clamp01(_airTime / 0.6f));
				Play(s.Shallows ? "land_shallows" : ("land_" + text), Mathf.Lerp(0.35f, 0.8f, t) * StepVolume * num3, 0.04f, 0f, num2 * Mathf.Lerp(1.05f, 0.86f, t));
				if ((flag && s.LandSpeed > 6.5f) || (!flag && _airTime > 0.9f))
				{
					Play(s.Woman ? "oof_f" : "oof_m", 0.5f, 0.05f, 0f, (s.Voice > 0f) ? s.Voice : 1f);
				}
			}
			if (!s.Airborne)
			{
				_airTime = 0f;
			}
			_wasAirborne = s.Airborne;
			_lastLandings = s.Landings;
			if (_lastJumps != int.MinValue && s.Jumps != _lastJumps && !s.Swimming)
			{
				Play("pushoff", 0.34f * StepVolume * num3, 0.06f, 0f, num2);
			}
			_lastJumps = s.Jumps;
			Drags(s, dt, text, num2, num3);
			if (s.Swimming && !_wasSwimming)
			{
				Play("splash", 0.6f, 0.05f);
			}
			_wasSwimming = s.Swimming;
			int num6 = Mathf.FloorToInt(s.SwimPhase / (MathF.PI * 2f));
			if (s.Swimming && s.Moving && _lastStroke != int.MinValue && num6 != _lastStroke)
			{
				Play("stroke", 0.5f, 0.08f);
			}
			_lastStroke = num6;
			if (s.Swimming && !s.Moving)
			{
				_lapTimer -= dt;
				if (_lapTimer <= 0f)
				{
					Play("wade", 0.16f, 0.15f);
					_lapTimer = 1.8f + (float)_rng.NextDouble() * 2f;
				}
			}
			WeaponKind weaponKind = ViewMeshes.KindOf(s.WeaponId);
			if (string.IsNullOrEmpty(s.WeaponId))
			{
				weaponKind = WeaponKind.None;
			}
			if (_haveWeapon && s.WeaponId != _lastWeapon && !s.Swimming)
			{
				if (weaponKind == WeaponKind.None)
				{
					Play("rustle", 0.35f, 0.08f);
				}
				else
				{
					Play(IsWooden(s.WeaponId, weaponKind) ? "equip_wood" : "equip_metal", 0.55f, 0.04f);
				}
			}
			_lastWeapon = s.WeaponId;
			_haveWeapon = true;
			if (_lastAttacks != int.MinValue && s.Attacks != _lastAttacks && !s.Swimming && (s.Swing != Swing.Attack || !SwingSounds))
			{
				if (s.Swing == Swing.Gather)
				{
					Play("rustle", 0.4f, 0.1f);
				}
				else if (s.Swing == Swing.Chop || s.Swing == Swing.Mine)
				{
					Play("swing_heavy", 0.42f, 0.06f);
				}
				else if (s.Swing == Swing.Dig)
				{
					Play("swing_heavy", 0.3f, 0.09f);
				}
				else if (!s.Melee && weaponKind == WeaponKind.Bow)
				{
					if (!BowSounds)
					{
						Play("bow_release", 0.6f, 0.04f);
						_pending.Add(new Pending
						{
							Key = "bow_draw",
							At = _clock + 0.3f,
							Volume = 0.42f
						});
					}
				}
				else if (!s.Melee && weaponKind == WeaponKind.Gun)
				{
					Play("zap", 0.5f, 0.06f);
				}
				else if (weaponKind != WeaponKind.Shotgun)
				{
					if (!s.Melee && weaponKind == WeaponKind.Staff)
					{
						Play("cast", 0.5f, 0.05f);
					}
					else
					{
						Play(SwingKey(weaponKind), (weaponKind == WeaponKind.Axe || weaponKind == WeaponKind.Hammer) ? 0.5f : 0.45f, 0.07f);
					}
				}
			}
			_lastAttacks = s.Attacks;
			if (_lastKills != int.MinValue && s.Kills != _lastKills)
			{
				Play("kill", 0.6f, 0.04f);
			}
			else if (_lastHits != int.MinValue && s.Hits != _lastHits)
			{
				Play(HitKey(weaponKind), 0.5f, 0.06f);
			}
			_lastHits = s.Hits;
			_lastKills = s.Kills;
			if (_lastHurts != int.MinValue && s.Hurts != _lastHurts)
			{
				Play("hurt", 0.65f, 0.06f);
			}
			_lastHurts = s.Hurts;
			if (s.Health < 0.3f)
			{
				_heartTimer -= dt;
				if (_heartTimer <= 0f)
				{
					float t2 = Mathf.Clamp01(s.Health / 0.3f);
					Play("heart", Mathf.Lerp(0.85f, 0.35f, t2), 0f);
					_heartTimer = Mathf.Lerp(0.5f, 0.95f, t2);
				}
			}
			else
			{
				_heartTimer = 0f;
			}
		}

		private static bool IsWooden(string id, WeaponKind kind)
		{
			if (kind != WeaponKind.Bow && kind != WeaponKind.Stick && kind != WeaponKind.Staff)
			{
				return id?.Contains("wood") ?? false;
			}
			return true;
		}

		private static string SwingKey(WeaponKind kind)
		{
			switch (kind)
			{
			case WeaponKind.None:
				return "swing_fist";
			case WeaponKind.Sword:
				return "swing_blade";
			case WeaponKind.Axe:
			case WeaponKind.Hammer:
				return "swing_heavy";
			case WeaponKind.Spear:
				return "swing_thrust";
			default:
				return "swing_light";
			}
		}

		private static string HitKey(WeaponKind kind)
		{
			switch (kind)
			{
			case WeaponKind.Sword:
			case WeaponKind.Spear:
			case WeaponKind.Dagger:
				return "hit_blade";
			case WeaponKind.Axe:
			case WeaponKind.Hammer:
				return "hit_heavy";
			case WeaponKind.Bow:
				return "arrow_confirm";
			case WeaponKind.Shotgun:
				return "shotgun_hit";
			default:
				return "hit_blunt";
			}
		}

		private static string GroundKey(byte ground, bool indoors)
		{
			if (indoors)
			{
				return "wood";
			}
			switch (ground)
			{
			case 2:
				return "sand";
			case 3:
			case 7:
			case 9:
				return "soil";
			case 4:
			case 6:
				return "rock";
			case 5:
				return "snow";
			case 8:
				return "wood";
			default:
				return "grass";
			}
		}

		private void UpdateLoops(State s, float dt)
		{
			float t = 1f - Mathf.Exp(-2f * dt);
			_windVolume = Mathf.Lerp(_windVolume, (s.Indoors || s.Dead) ? 0f : (s.Wind * 0.38f), t);
			Loop("wind", ref _windChannel, ref _windPlaying, _windVolume);
			float b = ((s.Indoors && s.HearthDistance >= 0f) ? (0.5f / (1f + s.HearthDistance * s.HearthDistance * 0.35f)) : 0f);
			_fireVolume = Mathf.Lerp(_fireVolume, b, t);
			Loop("fire", ref _fireChannel, ref _firePlaying, _fireVolume);
			_rainVolume = Mathf.Lerp(_rainVolume, s.Dead ? 0f : (s.Rain * 0.34f * (1f - _muffle)), t);
			Loop("rain", ref _rainChannel, ref _rainPlaying, _rainVolume);
		}

		private void Loop(string key, ref Channel channel, ref bool playing, float volume)
		{
			Loop(key, ref channel, ref playing, volume, _group);
		}

		private void Loop(string key, ref Channel channel, ref bool playing, float volume, ChannelGroup group)
		{
			if (volume < 0.005f)
			{
				if (playing)
				{
					StopLoop(ref channel, ref playing);
				}
				return;
			}
			if (playing)
			{
				bool isplaying = false;
				if (channel.isPlaying(out isplaying) != RESULT.OK || !isplaying)
				{
					playing = false;
				}
			}
			if (!playing)
			{
				if (!_sounds.TryGetValue(key, out var value) || value.Length == 0 || RuntimeManager.CoreSystem.playSound(value[0], group, paused: true, out channel) != RESULT.OK)
				{
					return;
				}
				if (value[0].getLength(out var length, TIMEUNIT.PCM) == RESULT.OK && length != 0)
				{
					channel.setPosition((uint)_rng.Next((int)length), TIMEUNIT.PCM);
				}
				channel.setVolume(volume);
				channel.setPaused(paused: false);
				playing = true;
			}
			channel.setVolume(volume);
		}

		private static void StopLoop(ref Channel channel, ref bool playing, float fade = 0.1f)
		{
			if (playing)
			{
				FadeOut(channel, fade);
			}
			playing = false;
		}

		private void Play(string key, float volume, float pitchJitter, float pan = 0f, float pitch = 1f)
		{
			if (volume <= 0.001f || !_on || _failed || !_haveGroup || !_sounds.TryGetValue(key, out var value) || value.Length == 0)
			{
				return;
			}
			_lastVariant.TryGetValue(key, out var value2);
			int num = _rng.Next(value.Length);
			if (value.Length > 1 && num == value2)
			{
				num = (num + 1 + _rng.Next(value.Length - 1)) % value.Length;
			}
			_lastVariant[key] = num;
			Sound sound = value[num];
			if (RuntimeManager.CoreSystem.playSound(sound, _group, paused: true, out var channel) == RESULT.OK)
			{
				channel.setVolume(volume);
				channel.setPitch(pitch * (1f + (float)(_rng.NextDouble() * 2.0 - 1.0) * pitchJitter));
				if (pan != 0f)
				{
					channel.setPan(pan);
				}
				channel.setPaused(paused: false);
			}
		}

		private void UpdateMuffle(bool indoors, float dt)
		{
			float num = (indoors ? 1f : 0f);
			_rescan -= dt;
			if (_muffle == num && (num == 0f || _rescan > 0f))
			{
				return;
			}
			_muffle = Mathf.MoveTowards(_muffle, num, Mathf.Min(dt, 0.05f) / 0.35f);
			if (_muffle <= 0f)
			{
				SetMuffled(on: false);
				return;
			}
			_muffleCutoff = 22000f * Mathf.Pow(3f / 88f, Gain(_muffle));
			if (!_muffled || _rescan <= 0f)
			{
				SetMuffled(on: true);
				_rescan = 1f;
			}
			foreach (KeyValuePair<IntPtr, KeyValuePair<ChannelGroup, DSP>> muffle in _muffles)
			{
				muffle.Value.Value.setParameterFloat(0, _muffleCutoff);
			}
		}

		private void SetMuffled(bool on, bool unlock = false)
		{
			_muffled = on;
			if (!on)
			{
				foreach (KeyValuePair<IntPtr, KeyValuePair<ChannelGroup, DSP>> muffle in _muffles)
				{
					muffle.Value.Key.removeDSP(muffle.Value.Value);
					muffle.Value.Value.release();
				}
				_muffles.Clear();
				if (unlock)
				{
					foreach (Bus lockedBuse in _lockedBuses)
					{
						if (lockedBuse.isValid())
						{
							lockedBuse.unlockChannelGroup();
						}
					}
					_lockedBuses.Clear();
				}
				_muffle = 0f;
				_muffleCutoff = 22000f;
				return;
			}
			FMOD.System coreSystem = RuntimeManager.CoreSystem;
			if (_lockedBuses.Count == 0)
			{
				LockTopBuses();
			}
			foreach (Bus lockedBuse2 in _lockedBuses)
			{
				if (lockedBuse2.isValid() && lockedBuse2.getChannelGroup(out var group) == RESULT.OK && group.hasHandle())
				{
					Muffle(coreSystem, group);
				}
			}
			if (_haveWorld)
			{
				Muffle(coreSystem, _world);
			}
			if (_haveAmbience)
			{
				Muffle(coreSystem, _ambience);
			}
		}

		private void LockTopBuses()
		{
			FMOD.Studio.System studioSystem = RuntimeManager.StudioSystem;
			if (studioSystem.getBankList(out var array) != RESULT.OK || array == null)
			{
				return;
			}
			HashSet<string> hashSet = new HashSet<string>();
			Bank[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				Bank bank = array2[i];
				if (!bank.isValid() || bank.getBusList(out var array3) != RESULT.OK || array3 == null)
				{
					continue;
				}
				Bus[] array4 = array3;
				for (int j = 0; j < array4.Length; j++)
				{
					Bus item = array4[j];
					if (item.getPath(out var path) == RESULT.OK && path != null && path.StartsWith("bus:/"))
					{
						string text = path.Substring(5);
						if (text.Length != 0 && text.IndexOf('/') < 0 && hashSet.Add(path) && item.lockChannelGroup() == RESULT.OK)
						{
							_lockedBuses.Add(item);
						}
					}
				}
			}
			studioSystem.flushCommands();
			if (!_busesLogged)
			{
				_busesLogged = true;
				Log.Info("indoors: muffling " + _lockedBuses.Count + " game buses (" + string.Join(", ", hashSet) + ")");
			}
		}

		private void Muffle(FMOD.System core, ChannelGroup g)
		{
			if ((!_haveGroup || !(g.handle == _group.handle)) && (!_haveMusic || !(g.handle == _music.handle)) && !_muffles.ContainsKey(g.handle) && core.createDSPByType(DSP_TYPE.LOWPASS, out var dsp) == RESULT.OK)
			{
				dsp.setParameterFloat(0, _muffleCutoff);
				if (g.addDSP(-1, dsp) != RESULT.OK)
				{
					dsp.release();
				}
				else
				{
					_muffles[g.handle] = new KeyValuePair<ChannelGroup, DSP>(g, dsp);
				}
			}
		}

		private static Dictionary<string, List<float[]>> MakeAll()
		{
			Dictionary<string, List<float[]>> all = new Dictionary<string, List<float[]>>();
			Synth.Ground[] array = new Synth.Ground[6]
			{
				Synth.Ground.Grass,
				Synth.Ground.Sand,
				Synth.Ground.Soil,
				Synth.Ground.Rock,
				Synth.Ground.Snow,
				Synth.Ground.Wood
			};
			for (int i = 0; i < array.Length; i++)
			{
				Synth.Ground g = array[i];
				string text = g.ToString().ToLowerInvariant();
				Add("step_" + text, 8, (int v) => Synth.Step(g, v));
				Add("land_" + text, 2, (int v) => Synth.Land(g, v));
				Add("scuff_" + text, 3, (int v) => Synth.Scuff(g, v));
				Add("skid_" + text, 2, (int v) => Synth.Skid(g, v));
			}
			Add("wade", 4, (int v) => Synth.Step(Synth.Ground.Shallows, v));
			Add("land_shallows", 2, (int v) => Synth.Land(Synth.Ground.Shallows, v));
			Add("scuff_shallows", 2, (int v) => Synth.Scuff(Synth.Ground.Shallows, v));
			Add("skid_shallows", 2, (int v) => Synth.Skid(Synth.Ground.Shallows, v));
			Add("cloth", 4, Synth.Cloth);
			Add("armor_plate", 4, Synth.ArmorPlate);
			Add("armor_leather", 3, Synth.ArmorLeather);
			Add("pushoff", 3, Synth.PushOff);
			Add("oof_m", 3, (int v) => Synth.Oof(v, woman: false));
			Add("oof_f", 3, (int v) => Synth.Oof(v, woman: true));
			Add("stroke", 3, Synth.SwimStroke);
			Add("splash", 2, (int v) => Synth.Splash(v + 50, 0.7f, 0.15f));
			Add("swing_fist", 3, (int v) => Synth.Swing(Synth.Swish.Fist, v));
			Add("swing_light", 3, (int v) => Synth.Swing(Synth.Swish.Light, v));
			Add("swing_blade", 3, (int v) => Synth.Swing(Synth.Swish.Blade, v));
			Add("swing_heavy", 3, (int v) => Synth.Swing(Synth.Swish.Heavy, v));
			Add("swing_thrust", 3, (int v) => Synth.Swing(Synth.Swish.Thrust, v));
			Add("bow_release", 2, Synth.BowRelease);
			Add("bow_draw", 2, Synth.BowDraw);
			Add("zap", 2, Synth.Zap);
			Add("cast", 2, Synth.Cast);
			Add("equip_metal", 2, (int v) => Synth.Equip(metal: true, v));
			Add("equip_wood", 2, (int v) => Synth.Equip(metal: false, v));
			Add("hit_blunt", 3, (int v) => Synth.Hit(0, kill: false, v));
			Add("hit_blade", 3, (int v) => Synth.Hit(1, kill: false, v));
			Add("hit_heavy", 3, (int v) => Synth.Hit(2, kill: false, v));
			Add("kill", 2, (int v) => Synth.Hit(1, kill: true, v));
			Add("hurt", 3, Synth.Hurt);
			Add("heart", 1, (int v) => Synth.Heartbeat());
			Add("door", 1, (int v) => Synth.Door());
			Add("rustle", 2, (int v) => Synth.Rustle());
			Add("horn", 1, (int v) => Synth.WarHorn());
			Add("wind", 1, (int v) => Synth.Wind(8f));
			Add("fire", 1, (int v) => Synth.Fire(6f));
			Add("rain", 1, (int v) => Synth.Rain(6f));
			Add("bird_chirps", 8, Synth.Chirps);
			Add("bird_whistle", 6, Synth.Whistle);
			Add("bird_trill", 6, Synth.Trill);
			Add("bird_warble", 10, Synth.Warble);
			Add("bird_twitter", 6, Synth.Twitter);
			Add("bird_coo", 2, Synth.Coo);
			Add("owl", 2, Synth.Hoot);
			Add("frog", 6, Synth.Frog);
			Add("crickets", 1, (int v) => Synth.Crickets(9f));
			MakeMore(all);
			return all;
			void Add(string key, int variants, Func<int, float[]> make)
			{
				List<float[]> list = new List<float[]>();
				for (int j = 0; j < variants; j++)
				{
					list.Add(make(j));
				}
				all[key] = list;
			}
		}

		private static Dictionary<string, List<byte[]>> ToPcm16(Dictionary<string, List<float[]>> made)
		{
			Dictionary<string, List<byte[]>> dictionary = new Dictionary<string, List<byte[]>>(made.Count);
			foreach (KeyValuePair<string, List<float[]>> item in made)
			{
				List<byte[]> list = new List<byte[]>(item.Value.Count);
				foreach (float[] item2 in item.Value)
				{
					list.Add(Synth.ToPcm16(item2));
				}
				dictionary[item.Key] = list;
			}
			return dictionary;
		}

		private void CreateSounds()
		{
			Dictionary<string, List<byte[]>> made;
			lock (MadeLock)
			{
				made = _made;
			}
			if (made == null)
			{
				return;
			}
			Stopwatch stopwatch = Stopwatch.StartNew();
			foreach (KeyValuePair<string, List<byte[]>> item in made)
			{
				List<Sound> list = new List<Sound>();
				List<float> list2 = new List<float>();
				int num = ((!Stereo.Contains(item.Key)) ? 1 : 2);
				foreach (byte[] item2 in item.Value)
				{
					CREATESOUNDEXINFO exinfo = new CREATESOUNDEXINFO
					{
						cbsize = Marshal.SizeOf(typeof(CREATESOUNDEXINFO)),
						length = (uint)item2.Length,
						numchannels = num,
						defaultfrequency = 44100,
						format = SOUND_FORMAT.PCM16
					};
					MODE mode = (MODE)(0x1908 | ((!Loops.Contains(item.Key)) ? 1 : 2));
					Sound sound;
					RESULT rESULT = RuntimeManager.CoreSystem.createSound(item2, mode, ref exinfo, out sound);
					if (rESULT == RESULT.OK)
					{
						list.Add(sound);
						list2.Add((float)(item2.Length / 2) / (float)(num * 44100));
					}
					else
					{
						Log.Error("could not create sound " + item.Key + ": " + rESULT);
					}
				}
				_sounds[item.Key] = list.ToArray();
				_lengths[item.Key] = list2.ToArray();
			}
			if (_madeError != null)
			{
				Log.Error("could not make body sounds: " + _madeError);
			}
			Log.Info("body sounds ready: " + _sounds.Count + " kinds, made in " + _madeMs + " ms in the background, loaded in " + stopwatch.ElapsedMilliseconds + " ms");
			if (_sounds.Count == 0)
			{
				_sounds["none"] = new Sound[0];
			}
		}

		private void Fail(Exception e)
		{
			_failed = true;
			Log.Error("first-person audio disabled: " + e);
			try
			{
				if (_listener != null)
				{
					_listener.enabled = true;
				}
				EndSpatial();
			}
			catch
			{
			}
		}

		private static void MakeDragonSounds(Action<string, int, Func<int, float[]>> add)
		{
			add("dragon_wing", 4, Synth.DragonWing);
			add("dragon_roar", 3, Synth.DragonRoar);
			add("dragon_fire", 1, (int v) => Synth.DragonFire(4f));
			add("dragon_whoosh", 2, Synth.DragonWhoosh);
			add("dragon_land", 2, Synth.DragonLand);
			add("dragon_snort", 3, Synth.DragonSnort);
		}

		private void UpdateDragons(State s, float dt)
		{
			_dragonScan -= dt;
			if (_dragonScan <= 0f)
			{
				_dragonScan = 0.5f;
				FindDragons(s.Position);
			}
			float num = 0f;
			float b = 0f;
			Vector3 dragonFireAt = Vector3.zero;
			float time = Time.time;
			for (int num2 = _dragons.Count - 1; num2 >= 0; num2--)
			{
				DragonVoice dragonVoice = _dragons[num2];
				Actor actor = dragonVoice.Actor;
				bool flag;
				try
				{
					flag = actor != null && actor.isAlive() && Dragons.Is(actor);
				}
				catch
				{
					flag = false;
				}
				if (!flag)
				{
					_dragons.RemoveAt(num2);
				}
				else
				{
					Vector2 current_position = actor.current_position;
					if (Dragons.Voice(actor, out var lift, out var fire, out var beats, out var beatPower, out var roars, out var thumps, out var whooshes, out var snorts))
					{
						float num3 = current_position.x - s.Position.x;
						float num4 = current_position.y - s.Position.y;
						float num5 = lift + 4f - s.EyeAboveGround;
						float num6 = Mathf.Sqrt(num3 * num3 + num4 * num4 + num5 * num5);
						if (num6 > 130f)
						{
							_dragons.RemoveAt(num2);
						}
						else
						{
							float num7 = 1f / (1f + Mathf.Pow(num6 / 18f, 1.25f));
							if (s.Indoors)
							{
								num7 *= 0.35f;
							}
							Vector3 vector = new Vector3(current_position.x, current_position.y, Ground(current_position) + lift + 4f);
							if (dragonVoice.Beats < 0)
							{
								dragonVoice.Beats = beats;
								dragonVoice.Roars = roars;
								dragonVoice.Thumps = thumps;
								dragonVoice.Whooshes = whooshes;
								dragonVoice.Snorts = snorts;
							}
							if (beats != dragonVoice.Beats)
							{
								PlayAt("dragon_wing", vector, Mathf.Clamp01((0.55f + 0.5f * beatPower) * num7), 0.06f);
								DragonWingBeats++;
							}
							if (roars != dragonVoice.Roars && _clock - _lastRoar > 2.5f && num6 < 110f)
							{
								PlayAt("dragon_roar", vector, Mathf.Clamp01(1.1f * num7), 0.07f);
								DragonRoars++;
								_lastRoar = _clock;
							}
							if (whooshes != dragonVoice.Whooshes)
							{
								PlayAt("dragon_whoosh", vector, Mathf.Clamp01(1.2f * num7), 0.05f);
							}
							if (thumps != dragonVoice.Thumps)
							{
								PlayAt("dragon_land", new Vector3(current_position.x, current_position.y, Ground(current_position) + 0.5f), Mathf.Clamp01(1.3f * num7), 0.05f);
							}
							if (snorts != dragonVoice.Snorts && num6 < 50f)
							{
								PlayAt("dragon_snort", vector, Mathf.Clamp01(0.8f * num7), 0.1f);
							}
							dragonVoice.Beats = beats;
							dragonVoice.Roars = roars;
							dragonVoice.Thumps = thumps;
							dragonVoice.Whooshes = whooshes;
							dragonVoice.Snorts = snorts;
							if (fire * num7 > num)
							{
								num = fire * num7;
								b = Mathf.Clamp01(fire * num7 * 1.1f);
								dragonFireAt = vector;
							}
						}
					}
					else
					{
						if (!Creatures.DragonPose(actor, out var lift2, out var breath, out var seed))
						{
							seed = (float)(actor.GetHashCode() & 0x3FF) / 97f;
							bool flag2 = false;
							try
							{
								flag2 = ((BaseSimObject)actor).isFlying();
							}
							catch
							{
							}
							lift2 = (flag2 ? 8f : 0f);
							breath = 0f;
						}
						float num8 = current_position.x - s.Position.x;
						float num9 = current_position.y - s.Position.y;
						float num10 = lift2 + 1.5f - s.EyeAboveGround;
						float num11 = Mathf.Sqrt(num8 * num8 + num9 * num9 + num10 * num10);
						if (num11 > 130f)
						{
							_dragons.RemoveAt(num2);
						}
						else
						{
							float num12 = 1f / (1f + Mathf.Pow(num11 / 16f, 1.25f));
							if (s.Indoors)
							{
								num12 *= 0.35f;
							}
							Vector3 vector2 = new Vector3(current_position.x, current_position.y, Ground(current_position) + lift2 + 1.5f);
							if (lift2 > 0.5f)
							{
								int num13 = Mathf.FloorToInt((time * 3.2f + seed - MathF.PI) / (MathF.PI * 2f));
								if (dragonVoice.LastBeat != int.MinValue && num13 != dragonVoice.LastBeat)
								{
									PlayAt("dragon_wing", vector2, Mathf.Clamp01(0.95f * num12), 0.06f);
									DragonWingBeats++;
								}
								dragonVoice.LastBeat = num13;
							}
							else
							{
								dragonVoice.LastBeat = int.MinValue;
							}
							bool num14 = breath > 0.15f && dragonVoice.LastBreath <= 0.15f;
							dragonVoice.LastBreath = breath;
							if (dragonVoice.NextRoar <= 0f)
							{
								dragonVoice.NextRoar = _clock + 6f + (float)_rng.NextDouble() * 20f;
							}
							bool flag3 = _clock >= dragonVoice.NextRoar && num11 < 80f;
							if ((num14 || flag3) && _clock - _lastRoar > 4f)
							{
								PlayAt("dragon_roar", vector2, Mathf.Clamp01(1.1f * num12), 0.07f);
								DragonRoars++;
								_lastRoar = _clock;
								dragonVoice.NextRoar = _clock + 18f + (float)_rng.NextDouble() * 30f;
							}
							if (breath * num12 > num)
							{
								num = breath * num12;
								b = Mathf.Clamp01(breath * num12 * 1.1f);
								dragonFireAt = vector2;
							}
						}
					}
				}
			}
			_dragonFireVolume = Mathf.Lerp(_dragonFireVolume, b, 1f - Mathf.Exp(-6f * dt));
			if (num > 0f)
			{
				_dragonFireAt = dragonFireAt;
			}
			LoopAt("dragon_fire", ref _dragonFireChannel, ref _dragonFirePlaying, _dragonFireVolume, _dragonFireAt);
		}

		private static float Ground(Vector2 p)
		{
			if (GroundAt == null)
			{
				return 0f;
			}
			return GroundAt(p.x, p.y);
		}

		private void FindDragons(Vector2 at)
		{
			try
			{
				List<Actor> simpleList = World.world.units.getSimpleList();
				for (int i = 0; i < simpleList.Count; i++)
				{
					Actor actor = simpleList[i];
					if (actor != null && Dragons.Is(actor) && actor.isAlive() && !((actor.current_position - at).sqrMagnitude > 16900f) && !HasDragon(actor))
					{
						_dragons.Add(new DragonVoice
						{
							Actor = actor
						});
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("dragon sounds: " + ex.Message);
				_dragonScan = 30f;
			}
		}

		private bool HasDragon(Actor a)
		{
			for (int i = 0; i < _dragons.Count; i++)
			{
				if (_dragons[i].Actor == a)
				{
					return true;
				}
			}
			return false;
		}

		private void EndDragons()
		{
			_dragons.Clear();
			StopLoop(ref _dragonFireChannel, ref _dragonFirePlaying, 0.5f);
			_dragonFireVolume = 0f;
		}

		public void PlayFrom(string key, float volume, Vector2 at, Vector2 you, float range = 40f)
		{
			float magnitude = (at - you).magnitude;
			float num = 1f - Mathf.Clamp01((magnitude - 5f) / Mathf.Max(1f, range - 5f));
			Play(key, volume * num * num, 0.03f);
		}

		private static void FadeOut(Channel channel, float seconds)
		{
			try
			{
				if (!channel.hasHandle())
				{
					return;
				}
				if (seconds > 0f && channel.getDSPClock(out var _, out var parentclock) == RESULT.OK)
				{
					float num = FadeAt(channel, parentclock);
					ulong num2 = (ulong)(seconds * (float)OutRate);
					bool flag = channel.removeFadePoints(parentclock, ulong.MaxValue) == RESULT.OK;
					for (int i = 0; i <= 4 && flag; i++)
					{
						flag = channel.addFadePoint(parentclock + (ulong)((long)num2 * (long)i) / 4uL, num * (1f - Gain((float)i / 4f))) == RESULT.OK;
					}
					if (flag && channel.setDelay(0uL, parentclock + num2) == RESULT.OK)
					{
						return;
					}
				}
				channel.stop();
			}
			catch
			{
			}
		}

		private static float FadeAt(Channel channel, ulong clock)
		{
			uint numpoints = 0u;
			if (channel.getFadePoints(ref numpoints, null, null) != RESULT.OK || numpoints == 0)
			{
				return 1f;
			}
			ulong[] array = new ulong[numpoints];
			float[] array2 = new float[numpoints];
			if (channel.getFadePoints(ref numpoints, array, array2) != RESULT.OK || numpoints == 0)
			{
				return 1f;
			}
			if (clock <= array[0])
			{
				return array2[0];
			}
			for (int i = 1; i < numpoints; i++)
			{
				if (clock <= array[i])
				{
					return array2[i - 1] + (array2[i] - array2[i - 1]) * (float)(clock - array[i - 1]) / Mathf.Max(1f, array[i] - array[i - 1]);
				}
			}
			return array2[numpoints - 1];
		}

		private static void FadeIn(Channel channel, float seconds)
		{
			try
			{
				if (channel.getDSPClock(out var _, out var parentclock) == RESULT.OK)
				{
					channel.addFadePoint(parentclock, 0f);
					channel.addFadePoint(parentclock + (ulong)(seconds * (float)OutRate), 1f);
				}
			}
			catch
			{
			}
		}

		private static float Gain(float level)
		{
			level = Mathf.Clamp01(level);
			return level * level * (3f - 2f * level);
		}

		private void ReleaseLater(ref Sound sound, float seconds)
		{
			if (sound.hasHandle())
			{
				_dying.Add(new Dying
				{
					Sound = sound,
					At = Time.unscaledTime + seconds + 0.25f
				});
			}
			sound = default(Sound);
		}

		public void Idle(float dt)
		{
			if (_dying.Count == 0 && _gameMusic == _gameMusicWant && _masterWant < 0f)
			{
				return;
			}
			try
			{
				for (int num = _dying.Count - 1; num >= 0; num--)
				{
					if (Time.unscaledTime >= _dying[num].At)
					{
						_dying[num].Sound.release();
						_dying.RemoveAt(num);
					}
				}
				if (_gameMusic != _gameMusicWant)
				{
					if (_gameMusicWant > _gameMusic && _gameMusicWait > 0f)
					{
						_gameMusicWait -= dt;
					}
					else
					{
						_gameMusic = Mathf.MoveTowards(_gameMusic, _gameMusicWant, dt / ((_gameMusicWant > _gameMusic) ? 3f : 1.5f));
					}
					if (MusicBox.inst != null && MusicBox.inst._music_event.isValid())
					{
						MusicBox.inst._music_event.setVolume(Gain(_gameMusic));
					}
				}
				if (!(_masterWant >= 0f))
				{
					return;
				}
				if (_on)
				{
					_masterWant = -1f;
					return;
				}
				SetMaster(Mathf.MoveTowards(_masterSet, _masterWant, dt / 0.5f));
				if (Mathf.Abs(_masterSet - _masterWant) < 0.001f)
				{
					_masterWant = -1f;
				}
			}
			catch
			{
				_dying.Clear();
				_gameMusic = _gameMusicWant;
				_masterWant = -1f;
			}
		}

		private Carrying CarryingOf(FMOD.Studio.EventInstance instance)
		{
			if (instance.getDescription(out var description) != RESULT.OK)
			{
				return Plain;
			}
			if (_carrying.TryGetValue(description.handle, out var value))
			{
				return value;
			}
			value = new Carrying();
			if (description.getPath(out var path) != RESULT.OK || path == null)
			{
				path = "";
			}
			float min;
			float max;
			float num = ((description.getMinMaxDistance(out min, out max) == RESULT.OK) ? max : 70f);
			value.Creature = path.StartsWith("event:/SFX/UNITS/", StringComparison.Ordinal);
			value.Lava = path == "event:/SFX/ENVIRONMENT/LavaEnvironment";
			value.Half = (value.Lava ? 4f : (value.Creature ? 5f : Mathf.Clamp(num / 14f, 3f, 120f)));
			if (IsHuge(instance))
			{
				value.Half = Mathf.Max(value.Half, 40f);
			}
			if (value.Creature && path.EndsWith("/idle", StringComparison.Ordinal))
			{
				value.Trim = 0.5f;
			}
			if (value.Lava)
			{
				value.Trim = 0.3f;
			}
			if (_carrying.Count > 512)
			{
				_carrying.Clear();
			}
			_carrying[description.handle] = value;
			return value;
		}

		private float Carry(GameSound g)
		{
			Carrying carrying = g.Hear ?? Plain;
			if (carrying.Half <= 0f)
			{
				return carrying.Trim;
			}
			if (g.Bed && !carrying.Lava)
			{
				return carrying.Trim;
			}
			float num = g.At.x - _ear.x;
			float num2 = g.At.y - _ear.y;
			float num3 = g.At.z - _earHeight;
			float num4 = Mathf.Sqrt(num * num + num2 * num2 + num3 * num3);
			return carrying.Trim / (1f + Mathf.Pow(num4 / carrying.Half, 1.4f));
		}

		private bool CarryChanged(GameSound g)
		{
			float num = Carry(g);
			if (Mathf.Abs(num - g.Near) <= Mathf.Max(0.004f, g.Near * 0.03f))
			{
				return false;
			}
			g.Near = num;
			return true;
		}

		private void FollowSpeaker(GameSound g)
		{
			Actor speaker = g.Speaker;
			try
			{
				if (speaker == null || speaker.data == null || speaker.data.id != g.SpeakerId)
				{
					g.Speaker = null;
					return;
				}
				if (Hitboxes.Get(speaker, out var box))
				{
					g.At = new Vector3(box.Centre.X, box.Centre.Y, box.Bottom + (box.Top - box.Bottom) * 0.85f);
					return;
				}
				Vector2 current_position = speaker.current_position;
				g.At = new Vector3(current_position.x, current_position.y, Height(current_position.x, current_position.y) + speaker.position_height);
				if (!speaker.isAlive())
				{
					g.Speaker = null;
				}
			}
			catch
			{
				g.Speaker = null;
			}
		}

		private void PlaceLava(GameSound g)
		{
			if (NearestLava(out var at))
			{
				float num = ((GroundAt != null) ? GroundAt(at.x, at.y) : 0f);
				g.At = new Vector3(at.x, at.y, num + 0.2f);
			}
		}

		private bool NearestLava(out Vector2 at)
		{
			if (_clock >= _lavaNext)
			{
				_lavaNext = _clock + 0.4f;
				_lavaDistance = -1f;
				MapBox world = World.world;
				if (world != null)
				{
					int num = Mathf.FloorToInt(_ear.x);
					int num2 = Mathf.FloorToInt(_ear.y);
					float num3 = float.MaxValue;
					for (int i = -24; i <= 24; i++)
					{
						for (int j = -24; j <= 24; j++)
						{
							if (j * j + i * i > 576)
							{
								continue;
							}
							WorldTile tile = world.GetTile(num + j, num2 + i);
							if (tile != null && tile.Type != null && tile.Type.lava)
							{
								float num4 = (float)tile.x + 0.5f - _ear.x;
								float num5 = (float)tile.y + 0.5f - _ear.y;
								float num6 = num4 * num4 + num5 * num5;
								if (!(num6 >= num3))
								{
									num3 = num6;
									_lavaAt = new Vector2((float)tile.x + 0.5f, (float)tile.y + 0.5f);
								}
							}
						}
					}
					if (num3 < float.MaxValue)
					{
						_lavaDistance = Mathf.Sqrt(num3);
					}
				}
			}
			at = _lavaAt;
			return _lavaDistance >= 0f;
		}

		private static Vector3 TileMiddle(Vector3 at)
		{
			if (at.x != Mathf.Floor(at.x) || at.y != Mathf.Floor(at.y))
			{
				return at;
			}
			return new Vector3(at.x + 0.5f, at.y + 0.5f, at.z);
		}

		private void Track(GameSound g)
		{
			if (g.Follow != null)
			{
				g.At = new Vector3(g.Follow.position.x, g.Follow.position.y, Height(g.Follow.position.x, g.Follow.position.y));
			}
			else if (g.Speaker != null)
			{
				FollowSpeaker(g);
			}
			else if (g.Bed && g.Hear != null && g.Hear.Lava)
			{
				PlaceLava(g);
			}
		}

		internal bool HeardForTest(string pathPart, out Vector3 at, out float carry, out Actor speaker)
		{
			at = default(Vector3);
			carry = -1f;
			speaker = null;
			for (int i = 0; i < 2; i++)
			{
				List<GameSound> list = ((i == 0) ? _gameSounds : _flat);
				for (int num = list.Count - 1; num >= 0; num--)
				{
					GameSound gameSound = list[num];
					if (gameSound.Instance.isValid() && gameSound.Instance.getDescription(out var description) == RESULT.OK && description.getPath(out var path) == RESULT.OK && path != null && path.IndexOf(pathPart, StringComparison.Ordinal) >= 0)
					{
						at = gameSound.At;
						carry = gameSound.Near;
						speaker = gameSound.Speaker;
						return true;
					}
				}
			}
			return false;
		}

		internal bool NearestLavaForTest(out Vector2 at)
		{
			_lavaNext = -10f;
			return NearestLava(out at);
		}

		public static void InsideFor(Rect area)
		{
			_inside = area;
			_insideAt = Time.unscaledTime;
		}

		public static Rect Footprint(Building b)
		{
			Vector2 current_position = b.current_position;
			BuildingFundament buildingFundament = ((b.asset != null) ? b.asset.fundament : null);
			if (buildingFundament == null)
			{
				return new Rect(current_position.x - 1.5f, current_position.y - 1.5f, 3f, 3f);
			}
			WorldTile current_tile = b.current_tile;
			float x = ((current_tile != null) ? ((float)(current_tile.x - buildingFundament.left)) : (current_position.x - (float)buildingFundament.left - 0.5f));
			float y = ((current_tile != null) ? ((float)(current_tile.y - buildingFundament.bottom)) : (current_position.y - (float)buildingFundament.bottom - 0.5f));
			return new Rect(x, y, buildingFundament.left + buildingFundament.right + 1, buildingFundament.top + buildingFundament.bottom + 1);
		}

		private static void MakeIndoorSounds(Action<string, int, Func<int, float[]>> add)
		{
			add("rain_roof", 1, (int v) => Synth.RainRoof(7f));
			add("wind_walls", 1, (int v) => Synth.WindWalls(9f));
			add("curtain", 3, Synth.CurtainSlide);
			Loops.Add("rain_roof");
			Loops.Add("wind_walls");
		}

		private void UpdateIndoorWeather(State s, float dt)
		{
			float t = 1f - Mathf.Exp(-2f * dt);
			float num = (s.Dead ? 0f : _muffle);
			_roofVolume = Mathf.Lerp(_roofVolume, s.Rain * 0.42f * num, t);
			Loop("rain_roof", ref _roofChannel, ref _roofPlaying, _roofVolume);
			_wallsVolume = Mathf.Lerp(_wallsVolume, Mathf.Clamp01(s.Wind - 0.15f) * 0.3f * num, t);
			Loop("wind_walls", ref _wallsChannel, ref _wallsPlaying, _wallsVolume);
		}

		private void EndIndoors()
		{
			StopLoop(ref _roofChannel, ref _roofPlaying, 0.8f);
			StopLoop(ref _wallsChannel, ref _wallsPlaying, 0.8f);
			foreach (GameSound item in _flat)
			{
				if (item.Instance.isValid())
				{
					item.Instance.setVolume(1f);
				}
			}
			_flat.Clear();
		}

		private bool IsHuge(FMOD.Studio.EventInstance instance)
		{
			if (instance.getDescription(out var description) != RESULT.OK)
			{
				return false;
			}
			if (_hugeEvents.TryGetValue(description.handle, out var value))
			{
				return value;
			}
			value = false;
			if (description.getPath(out var path) == RESULT.OK && path != null)
			{
				string[] hugeEvents = HugeEvents;
				foreach (string value2 in hugeEvents)
				{
					if (path.StartsWith(value2, StringComparison.Ordinal))
					{
						value = true;
						break;
					}
				}
				hugeEvents = SmallEvents;
				foreach (string value3 in hugeEvents)
				{
					if (path.StartsWith(value3, StringComparison.Ordinal))
					{
						value = false;
					}
				}
			}
			if (_hugeEvents.Count > 512)
			{
				_hugeEvents.Clear();
			}
			_hugeEvents[description.handle] = value;
			return value;
		}

		private bool InsideHere(Vector3 at)
		{
			float num = at.x - _ear.x;
			float num2 = at.y - _ear.y;
			if (num * num + num2 * num2 < 1.44f)
			{
				return true;
			}
			if (Time.unscaledTime - _insideAt > 0.5f)
			{
				return false;
			}
			Rect inside = _inside;
			if (at.x > inside.xMin - 0.6f && at.x < inside.xMax + 0.6f && at.y > inside.yMin - 0.6f)
			{
				return at.y < inside.yMax + 0.6f;
			}
			return false;
		}

		private float Keep(bool huge, bool ambient, Vector3 at)
		{
			float muffle = _muffle;
			if (muffle <= 0f)
			{
				return 1f;
			}
			if (ambient)
			{
				return 1f - muffle;
			}
			if (InsideHere(at))
			{
				return 1f;
			}
			if (!huge)
			{
				return 1f - muffle;
			}
			return Mathf.Lerp(1f, 0.55f, muffle);
		}

		private float KeepGame(GameSound g)
		{
			if (_muffle <= 0f)
			{
				return 1f;
			}
			if (!g.HugeKnown)
			{
				g.Huge = IsHuge(g.Instance);
				g.HugeKnown = true;
			}
			return Keep(g.Huge, g.Bed, g.At);
		}

		private float KeepVoice(Voice v)
		{
			if (_muffle <= 0f)
			{
				return 1f;
			}
			if (!v.Ambient)
			{
				if (!InsideHere(v.At))
				{
					if (!v.Huge)
					{
						return 1f - _muffle;
					}
					return 1f;
				}
				return 1f;
			}
			return 1f - _muffle;
		}

		private float KeepKey(string key, Vector3 at, bool ambient)
		{
			if (!(_muffle <= 0f))
			{
				if (!ambient)
				{
					if (!HugeKeys.Contains(key) && !InsideHere(at))
					{
						return 1f - _muffle;
					}
					return 1f;
				}
				return 1f - _muffle;
			}
			return 1f;
		}

		private void KeepNative(GameSound g)
		{
			float num = KeepGame(g);
			float num2 = num * g.Near;
			g.NativeKeep = num;
			if (!(Mathf.Abs(num2 - g.NativeSet) < 0.005f))
			{
				g.NativeSet = num2;
				g.Instance.setVolume(num2);
			}
		}

		private void KeepNativeHere(GameSound g)
		{
			Track(g);
			if ((g.Speaker != null || (g.Bed && g.Hear != null && g.Hear.Lava)) && g.Instance.isValid())
			{
				float num = ((GroundAt != null) ? GroundAt(g.At.x, g.At.y) : 0f);
				g.Instance.set3DAttributes(SpatialHooks.Attributes(new Vector3(g.At.x, g.At.y, 0f - Mathf.Max(0f, g.At.z - num))));
			}
			CarryChanged(g);
			KeepNative(g);
		}

		internal float KeepForTest(string pathPart)
		{
			for (int i = 0; i < 2; i++)
			{
				List<GameSound> list = ((i == 0) ? _gameSounds : _flat);
				for (int num = list.Count - 1; num >= 0; num--)
				{
					GameSound gameSound = list[num];
					if (gameSound.Instance.isValid() && gameSound.Instance.getDescription(out var description) == RESULT.OK && description.getPath(out var path) == RESULT.OK && path != null && path.IndexOf(pathPart, StringComparison.Ordinal) >= 0)
					{
						if (!gameSound.Native)
						{
							return gameSound.KeepSet;
						}
						return gameSound.NativeKeep;
					}
				}
			}
			return -1f;
		}

		internal void TakeFlat(FMOD.Studio.EventInstance instance, Vector3 at, Transform follow, bool bed, Actor speaker = null)
		{
			if (!_on || !instance.isValid())
			{
				return;
			}
			for (int i = 0; i < _flat.Count; i++)
			{
				if (_flat[i].Instance.handle == instance.handle)
				{
					_flat[i].At = new Vector3(at.x, at.y, Height(at.x, at.y));
					return;
				}
			}
			if (_flat.Count < 96)
			{
				if (follow == null && !bed)
				{
					at = TileMiddle(at);
				}
				GameSound gameSound = new GameSound
				{
					Instance = instance,
					At = new Vector3(at.x, at.y, Height(at.x, at.y)),
					Follow = follow,
					Bed = bed,
					Native = true
				};
				Begin(gameSound, speaker);
				_flat.Add(gameSound);
				KeepNativeHere(gameSound);
			}
		}

		private void UpdateFlat(float dt)
		{
			for (int num = _flat.Count - 1; num >= 0; num--)
			{
				GameSound gameSound = _flat[num];
				gameSound.Age += dt;
				if (!gameSound.Instance.isValid())
				{
					_flat.RemoveAt(num);
					continue;
				}
				if (!gameSound.Bed && gameSound.Follow == null && gameSound.Age > 1f)
				{
					gameSound.Instance.getPlaybackState(out var state);
					if (state == PLAYBACK_STATE.STOPPED)
					{
						_flat.RemoveAt(num);
						continue;
					}
				}
				KeepNativeHere(gameSound);
			}
		}

		private static void MakeMusicLater(string what)
		{
			lock (MusicLock)
			{
				if (_musicQueue.Contains(what))
				{
					return;
				}
				_musicQueue.Enqueue(what);
				if (_musicWorking)
				{
					return;
				}
				_musicWorking = true;
			}
			System.Threading.Thread thread = new System.Threading.Thread((ThreadStart)delegate
			{
				while (true)
				{
					string text;
					lock (MusicLock)
					{
						if (_musicQueue.Count == 0)
						{
							_musicWorking = false;
							break;
						}
						text = _musicQueue.Peek();
					}
					try
					{
						int seed = Interlocked.Increment(ref _musicSeed);
						switch (text)
						{
						case "battle":
						{
							MusicPiece[] array = MusicSynth.BattleAny(seed);
							MusicPiece[] array2 = array;
							for (int i = 0; i < array2.Length; i++)
							{
								array2[i].ToPcm();
							}
							lock (MusicLock)
							{
								_battleMade = array;
							}
							break;
						}
						case "victory":
						{
							MusicPiece victoryMade = MusicSynth.Victory(seed).ToPcm();
							lock (MusicLock)
							{
								_victoryMade = victoryMade;
							}
							break;
						}
						case "death":
						{
							MusicPiece deathMade = MusicSynth.Death(seed).ToPcm();
							lock (MusicLock)
							{
								_deathMade = deathMade;
							}
							break;
						}
						case "day":
						case "night":
						case "town":
						{
							MusicPiece value = MusicSynth.Calm(text, seed).ToPcm();
							lock (MusicLock)
							{
								_calmMade[text] = value;
							}
							break;
						}
						}
					}
					catch (Exception ex)
					{
						_musicError = text + ": " + ex;
					}
					lock (MusicLock)
					{
						_musicQueue.Dequeue();
					}
				}
			});
			thread.IsBackground = true;
			thread.Name = "FirstPerson music";
			thread.Priority = System.Threading.ThreadPriority.BelowNormal;
			thread.Start();
		}

		private static void PrepareMusic()
		{
			bool flag;
			bool flag2;
			bool flag3;
			bool flag4;
			lock (MusicLock)
			{
				flag = _battleMade == null;
				flag2 = _victoryMade == null;
				flag3 = _deathMade == null;
				flag4 = !_calmMade.ContainsKey("day");
			}
			if (flag)
			{
				MakeMusicLater("battle");
			}
			if (flag2)
			{
				MakeMusicLater("victory");
			}
			if (flag3)
			{
				MakeMusicLater("death");
			}
			if (flag4)
			{
				MakeMusicLater("day");
			}
		}

		private void BeginMusic()
		{
			FMOD.System coreSystem = RuntimeManager.CoreSystem;
			if (!_haveMusic && coreSystem.createChannelGroup("first_person_music", out _music) == RESULT.OK)
			{
				_haveMusic = true;
			}
			_calmGap = 8f + (float)_rng.NextDouble() * 10f;
			_peakDanger = 0f;
			PrepareMusic();
		}

		private void EndMusic()
		{
			StopBattle(1.5f);
			StopCalm(1.5f);
			DuckGameMusic(duck: false);
		}

		private void UpdateMusic(State s, float dt)
		{
			if (!_haveMusic)
			{
				return;
			}
			if (_musicError != null && !_musicReported)
			{
				_musicReported = true;
				Log.Error("music: " + _musicError);
			}
			float num = MusicVolume * 0.62f * GameMusicVolume();
			_music.setVolume(num * _dip);
			bool flag = num > 0.001f;
			DuckGameMusic(flag);
			if (!flag)
			{
				StopBattle(0.1f);
				StopCalm(0.1f);
				return;
			}
			float num2 = Mathf.Clamp01(Danger);
			if (s.Dead)
			{
				num2 = 0f;
			}
			_battleWant[0] = Mathf.Clamp01((num2 - 0.4f) / 0.22f);
			_battleWant[1] = Mathf.Clamp01((num2 - 0.22f) / 0.25f);
			_battleWant[2] = Mathf.Clamp01((num2 - 0.66f) / 0.2f);
			UpdateBattleLatch(num2);
			if (num2 > 0.25f && !_battlePlaying && _battleLatch < 0f)
			{
				StartBattle(s);
			}
			else if (_battlePlaying)
			{
				for (int i = 0; i < 3; i++)
				{
					UpdateLayer(i, _battleWant[i], dt);
				}
				_peakDanger = Mathf.Max(_peakDanger, num2);
				bool flag2 = UpdateBattleEnd(s, num2, dt);
				bool flag3 = _battleLevel[0] + _battleLevel[1] + _battleLevel[2] < 0.01f && _battleEnterAt[0] < 0f && _battleEnterAt[1] < 0f && _battleEnterAt[2] < 0f;
				_battleQuiet = ((flag3 && !flag2) ? (_battleQuiet + dt) : 0f);
				if (_battleQuiet > 1.5f)
				{
					StopBattle(0.1f);
					if (_peakDanger > 0.6f && s.Kills > _killsAtStart && !s.Dead)
					{
						Sting(_victoryMade, 0.9f, "victory");
					}
					_peakDanger = 0f;
					_calmGap = 25f + (float)_rng.NextDouble() * 30f;
				}
			}
			bool flag4 = _battlePlaying && (_battleLevel[1] > 0.05f || num2 > 0.25f);
			float num3 = ((flag4 || s.Dead) ? 0f : 1f);
			_calmRoom = Mathf.MoveTowards(_calmRoom, s.Indoors ? 0.7f : 1f, 0.6f * dt);
			if (_calmPlaying)
			{
				bool isplaying = false;
				if (_calm.isPlaying(out isplaying) != RESULT.OK || !isplaying)
				{
					_calmPlaying = false;
					ReleaseSound(ref _calmSound);
					_calmGap = 50f + (float)_rng.NextDouble() * 90f;
				}
				else
				{
					_calmLevel = Mathf.MoveTowards(_calmLevel, num3, ((num3 < _calmLevel) ? 0.625f : 0.3f) * dt);
					_calm.setVolume(Gain(_calmLevel) * _calmRoom);
					if (_calmLevel <= 0f && num3 <= 0f)
					{
						StopCalm(0.05f);
						_calmGap = 30f;
					}
				}
			}
			else if (!flag4 && !s.Dead)
			{
				_calmGap -= dt;
				string text = Mood ?? "day";
				MusicPiece value;
				lock (MusicLock)
				{
					_calmMade.TryGetValue(text, out value);
				}
				if (value == null)
				{
					MakeMusicLater(text);
				}
				else if (_calmGap <= 0f)
				{
					lock (MusicLock)
					{
						_calmMade.Remove(text);
					}
					MakeMusicLater(text);
					_calmSound = CreateMusicSound(value, loop: false);
					if (_calmSound.hasHandle() && RuntimeManager.CoreSystem.playSound(_calmSound, _music, paused: true, out _calm) == RESULT.OK)
					{
						_calmLevel = 1f;
						_calm.setVolume(_calmRoom);
						_calm.setPaused(paused: false);
						_calmPlaying = true;
						CalmName = value.Name;
						Log.Info($"music: a {text} piece, {value.Name}, {value.Seconds:F0} s");
					}
					else
					{
						ReleaseSound(ref _calmSound);
					}
				}
			}
			if (_stingPlaying && (_sting.isPlaying(out var isplaying2) != RESULT.OK || !isplaying2))
			{
				_stingPlaying = false;
				ReleaseSound(ref _stingSound);
			}
		}

		private void UpdateLayer(int i, float want, float dt)
		{
			float num = _battleLevel[i];
			float num2 = ((want > num) ? LayerRise[i] : (1f / 3f));
			if (_battleEnterAt[i] < 0f && num < 0.002f && want > 0.002f)
			{
				_battleEnterAt[i] = NextBeat(EnterBeats[i]);
			}
			if (_battleEnterAt[i] >= 0f)
			{
				float num3 = Until(_battleEnterAt[i]);
				if (want <= 0.002f)
				{
					_battleEnterAt[i] = -1f;
				}
				else if (num3 > EnterRamp[i])
				{
					want = 0f;
				}
				else
				{
					if (EnterRamp[i] > 0f)
					{
						num2 = Mathf.Max(want, 0.05f) / EnterRamp[i];
					}
					if (num3 <= 0f)
					{
						_battleEnterAt[i] = -1f;
						LayerEnteredAt[i] = BattleBeatNow();
						LayerEnteredLate[i] = ((_battleBeat > 0f) ? (dt / _battleBeat) : 0f);
					}
				}
			}
			_battleLevel[i] = Mathf.MoveTowards(num, want, num2 * dt);
			_battle[i].setVolume(Gain(_battleLevel[i]) * LayerMix[i]);
		}

		private float LoopTime()
		{
			if (_battle[0].getPosition(out var position, TIMEUNIT.PCM) != RESULT.OK)
			{
				return -1f;
			}
			return (float)position / 32000f;
		}

		private float NextBeat(float beats)
		{
			float num = beats * _battleBeat;
			float num2 = LoopTime();
			if (num <= 0f || num2 < 0f)
			{
				return -1f;
			}
			float num3 = (Mathf.Floor(num2 / num) + 1f) * num;
			if (!(num3 >= _battleLoop - 0.001f))
			{
				return num3;
			}
			return 0f;
		}

		private float Until(float at)
		{
			float num = LoopTime();
			if (num < 0f)
			{
				return 0f;
			}
			float num2 = at - num;
			if (num2 < (0f - _battleLoop) * 0.5f)
			{
				num2 += _battleLoop;
			}
			else if (num2 > _battleLoop * 0.5f)
			{
				num2 -= _battleLoop;
			}
			return num2;
		}

		public void MusicDeath()
		{
			if (_haveMusic)
			{
				StopBattle(1.2f);
				StopCalm(1.2f);
				Sting(_deathMade, 0.85f, "death");
			}
		}

		private void StartBattle(State s)
		{
			MusicPiece[] battleMade;
			lock (MusicLock)
			{
				battleMade = _battleMade;
			}
			if (battleMade == null)
			{
				MakeMusicLater("battle");
				return;
			}
			FMOD.System coreSystem = RuntimeManager.CoreSystem;
			_battleSounds = new Sound[3];
			for (int i = 0; i < 3; i++)
			{
				_battleSounds[i] = CreateMusicSound(battleMade[i], loop: true);
			}
			_battleBeat = battleMade[0].Beat;
			_battleLoop = battleMade[0].Seconds;
			_battleSet = battleMade;
			_fallingFor = 0f;
			if (_stingPlaying)
			{
				FadeOut(_sting, 0.4f);
				_stingPlaying = false;
				ReleaseLater(ref _stingSound, 0.4f);
			}
			int num = battleMade[0].Length / 2;
			uint num2 = ((_battleBeat > 0f) ? ((uint)Mathf.Max(0, num - Mathf.RoundToInt(2f * _battleBeat * 32000f))) : 0u);
			for (int j = 0; j < 3; j++)
			{
				if (!_battleSounds[j].hasHandle() || coreSystem.playSound(_battleSounds[j], _music, paused: true, out _battle[j]) != RESULT.OK)
				{
					for (int k = 0; k < j; k++)
					{
						_battle[k].stop();
					}
					StopBattle(0f);
					return;
				}
				_battle[j].setPosition(num2, TIMEUNIT.PCM);
				FadeIn(_battle[j], 0.01f);
				_battleEnterAt[j] = -1f;
				_battleLevel[j] = ((j == 0) ? _battleWant[0] : 0f);
				_battle[j].setVolume(Gain(_battleLevel[j]) * LayerMix[j]);
				LayerEnteredAt[j] = ((_battleLevel[j] > 0f) ? ((float)num2 / 32000f / _battleBeat) : (-1f));
			}
			for (int l = 0; l < 3; l++)
			{
				_battle[l].setPaused(paused: false);
			}
			_battlePlaying = true;
			Log.Info("music: battle, " + battleMade[0].Seconds.ToString("F0") + " s loop");
			_battleQuiet = 0f;
			_killsAtStart = s.Kills;
		}

		private void StopBattle(float fade)
		{
			if (_battlePlaying)
			{
				for (int i = 0; i < 3; i++)
				{
					FadeOut(_battle[i], fade);
				}
			}
			for (int j = 0; j < 3; j++)
			{
				_battleLevel[j] = 0f;
				_battleEnterAt[j] = -1f;
			}
			_battlePlaying = false;
			if (_battleSounds != null)
			{
				for (int k = 0; k < 3; k++)
				{
					ReleaseLater(ref _battleSounds[k], fade);
				}
				_battleSounds = null;
				MakeMusicLater("battle");
			}
		}

		private void StopCalm(float fade)
		{
			if (_calmPlaying)
			{
				FadeOut(_calm, fade);
			}
			_calmPlaying = false;
			ReleaseLater(ref _calmSound, fade);
		}

		internal void PlayVictory()
		{
			MusicPiece victoryMade;
			lock (MusicLock)
			{
				victoryMade = _victoryMade;
			}
			Sting(victoryMade, 1f, "victory");
		}

		private void Sting(MusicPiece piece, float volume, string remake)
		{
			if (piece == null)
			{
				return;
			}
			if (_stingPlaying)
			{
				FadeOut(_sting, 0.3f);
			}
			_stingPlaying = false;
			ReleaseLater(ref _stingSound, 0.3f);
			_stingSound = CreateMusicSound(piece, loop: false);
			if (_stingSound.hasHandle() && RuntimeManager.CoreSystem.playSound(_stingSound, _music, paused: true, out _sting) == RESULT.OK)
			{
				_sting.setVolume(volume);
				_sting.setPaused(paused: false);
				_stingPlaying = true;
			}
			else
			{
				ReleaseSound(ref _stingSound);
			}
			lock (MusicLock)
			{
				if (remake == "victory")
				{
					_victoryMade = null;
				}
				else if (remake == "death")
				{
					_deathMade = null;
				}
			}
			MakeMusicLater(remake);
		}

		private static Sound CreateMusicSound(MusicPiece piece, bool loop)
		{
			byte[] array = piece.Pcm ?? MusicSynth.ToPcm16(piece.Stereo);
			CREATESOUNDEXINFO exinfo = new CREATESOUNDEXINFO
			{
				cbsize = Marshal.SizeOf(typeof(CREATESOUNDEXINFO)),
				length = (uint)array.Length,
				numchannels = 2,
				defaultfrequency = 32000,
				format = SOUND_FORMAT.PCM16
			};
			MODE mode = (MODE)(0x1908 | ((!loop) ? 1 : 2));
			if (RuntimeManager.CoreSystem.createSound(array, mode, ref exinfo, out var sound) != RESULT.OK)
			{
				return default(Sound);
			}
			return sound;
		}

		private static void ReleaseSound(ref Sound sound)
		{
			if (sound.hasHandle())
			{
				sound.release();
			}
			sound = default(Sound);
		}

		private static float GameMusicVolume()
		{
			if (!MusicBox.music_on)
			{
				return 0f;
			}
			try
			{
				if (MusicBox.inst != null && MusicBox.inst._vca_music.isValid() && MusicBox.inst._vca_music.getVolume(out var volume) == RESULT.OK)
				{
					return Mathf.Clamp01(volume);
				}
			}
			catch
			{
			}
			return 1f;
		}

		private void DuckGameMusic(bool duck)
		{
			if (duck != _gameMusicDucked)
			{
				_gameMusicDucked = duck;
				_gameMusicWant = (duck ? 0f : 1f);
				_gameMusicWait = (duck ? 0f : 1f);
			}
		}

		private void UpdateBattleLatch(float d)
		{
			if (!(_battleLatch < 0f))
			{
				if (d < 0.2f || d > _battleLatch + 0.15f)
				{
					_battleLatch = -1f;
				}
				else
				{
					_battleLatch = Mathf.Min(_battleLatch, d);
				}
			}
		}

		private bool UpdateBattleEnd(State s, float d, float dt)
		{
			if (_battleSet == null || _battleSet.Length < 6 || _battleBeat <= 0f)
			{
				return false;
			}
			_fallingFor = ((DangerNow <= 0.32f) ? (_fallingFor + dt) : 0f);
			bool num = _fallingFor > 2f && _peakDanger > 0.45f;
			bool flag = _battleWant[0] + _battleWant[1] + _battleWant[2] <= 0f;
			if (!num && !flag)
			{
				return false;
			}
			float num2 = Mathf.Max(_battleLevel[0], Mathf.Max(_battleLevel[1], _battleLevel[2]));
			if (num2 < 0.08f)
			{
				return false;
			}
			float num3 = 4f * _battleBeat;
			float num4 = NextBeat(4f);
			if (num4 < 0f)
			{
				return false;
			}
			float num5 = Until(num4);
			if (num5 < 0.2f)
			{
				num5 += num3;
			}
			float num6 = NextBeat(16f);
			float num7 = ((num6 >= 0f) ? Until(num6) : (-1f));
			if (num7 >= 0.2f && num7 < 3f)
			{
				num5 = num7;
			}
			if (_battle[0].getDSPClock(out var _, out var parentclock) != RESULT.OK)
			{
				return false;
			}
			int num8;
			object obj;
			if (s.Kills > _killsAtStart && _peakDanger > 0.6f)
			{
				num8 = ((!s.Dead) ? 1 : 0);
				if (num8 != 0)
				{
					obj = _battleSet[5];
					goto IL_01af;
				}
			}
			else
			{
				num8 = 0;
			}
			obj = ((_battleLevel[0] > 0.25f || _battleLevel[2] > 0.1f) ? _battleSet[3] : _battleSet[4]);
			goto IL_01af;
			IL_01af:
			MusicPiece musicPiece = (MusicPiece)obj;
			float volume = ((num8 != 0) ? (0.65f + 0.3f * num2) : (0.5f + 0.42f * num2));
			PlayEnding(musicPiece, volume, num5);
			ulong start = parentclock + (ulong)(num5 * (float)OutRate);
			for (int i = 0; i < 3; i++)
			{
				FadeOutFrom(_battle[i], start, 0.35f);
				_battleLevel[i] = 0f;
				_battleEnterAt[i] = -1f;
			}
			_battlePlaying = false;
			if (_battleSounds != null)
			{
				for (int j = 0; j < 3; j++)
				{
					ReleaseLater(ref _battleSounds[j], num5 + 0.4f);
				}
				_battleSounds = null;
				MakeMusicLater("battle");
			}
			_battleLatch = d;
			_peakDanger = 0f;
			_fallingFor = 0f;
			_calmGap = 25f + (float)_rng.NextDouble() * 30f;
			LastEnding = musicPiece.Name;
			Log.Info($"music: the fight is over; the battle ends with {musicPiece.Name} in {num5:F2} s");
			return true;
		}

		private void PlayEnding(MusicPiece piece, float volume, float wait)
		{
			if (_stingPlaying)
			{
				FadeOut(_sting, 0.3f);
			}
			_stingPlaying = false;
			ReleaseLater(ref _stingSound, 0.3f);
			_stingSound = CreateMusicSound(piece, loop: false);
			if (!_stingSound.hasHandle() || RuntimeManager.CoreSystem.playSound(_stingSound, _music, paused: true, out _sting) != RESULT.OK)
			{
				ReleaseSound(ref _stingSound);
				return;
			}
			float num = wait - 0.06f;
			if (num > 0f && _sting.getDSPClock(out var _, out var parentclock) == RESULT.OK)
			{
				_sting.setDelay(parentclock + (ulong)(num * (float)OutRate), 0uL, stopchannels: false);
			}
			_sting.setVolume(volume);
			_sting.setPaused(paused: false);
			_stingPlaying = true;
		}

		private static void FadeOutFrom(Channel channel, ulong start, float seconds)
		{
			try
			{
				if (channel.hasHandle())
				{
					ulong num = (ulong)(Mathf.Max(0.01f, seconds) * (float)OutRate);
					float num2 = FadeAt(channel, start);
					bool flag = channel.removeFadePoints(start, ulong.MaxValue) == RESULT.OK;
					for (int i = 0; i <= 4 && flag; i++)
					{
						flag = channel.addFadePoint(start + (ulong)((long)num * (long)i) / 4uL, num2 * (1f - Gain((float)i / 4f))) == RESULT.OK;
					}
					if (!flag || channel.setDelay(0uL, start + num) != RESULT.OK)
					{
						FadeOut(channel, seconds);
					}
				}
			}
			catch
			{
			}
		}

		internal int SetSize(string set)
		{
			if (!_sounds.TryGetValue(set, out var value))
			{
				return -1;
			}
			return value.Length;
		}

		internal bool SetFromPack(string set)
		{
			if (_packSets.TryGetValue(set, out var value) && _sounds.TryGetValue(set, out var value2))
			{
				return value == value2;
			}
			return false;
		}

		internal void KeepPackSounds()
		{
			if (_failed || _sounds.Count == 0 || _sounds.ContainsKey("none"))
			{
				return;
			}
			Dictionary<string, List<byte[]>> soundSets = Packs.SoundSets;
			bool flag = false;
			foreach (KeyValuePair<string, Sound[]> packSet in _packSets)
			{
				if (!_sounds.TryGetValue(packSet.Key, out var value) || value != packSet.Value)
				{
					flag = true;
				}
			}
			if (soundSets == _packSetsApplied && !flag)
			{
				return;
			}
			_packSetsApplied = soundSets;
			try
			{
				_keptSets.Clear();
				foreach (KeyValuePair<string, Sound[]> packSet2 in _packSets)
				{
					if (_sounds.TryGetValue(packSet2.Key, out var value2) && value2 == packSet2.Value && soundSets.TryGetValue(packSet2.Key, out var value3) && _packFrom.TryGetValue(packSet2.Key, out var value4) && value3 == value4)
					{
						_keptSets.Add(packSet2.Key);
					}
				}
				foreach (KeyValuePair<string, Sound[]> packSet3 in _packSets)
				{
					if (_keptSets.Contains(packSet3.Key))
					{
						continue;
					}
					if (_sounds.TryGetValue(packSet3.Key, out var value5) && value5 == packSet3.Value && _ownSets.TryGetValue(packSet3.Key, out var value6))
					{
						_sounds[packSet3.Key] = value6;
						if (_ownLengths.TryGetValue(packSet3.Key, out var value7))
						{
							_lengths[packSet3.Key] = value7;
						}
					}
					Sound[] value8 = packSet3.Value;
					foreach (Sound sound in value8)
					{
						sound.release();
					}
				}
				_dropSets.Clear();
				foreach (string key in _packSets.Keys)
				{
					if (!_keptSets.Contains(key))
					{
						_dropSets.Add(key);
					}
				}
				foreach (string dropSet in _dropSets)
				{
					_packSets.Remove(dropSet);
					_ownSets.Remove(dropSet);
					_ownLengths.Remove(dropSet);
					_packFrom.Remove(dropSet);
				}
				UnknownPackSets.Clear();
				int num = 0;
				foreach (KeyValuePair<string, List<byte[]>> item in soundSets)
				{
					if (_keptSets.Contains(item.Key))
					{
						num += _packSets[item.Key].Length;
						continue;
					}
					if (!_sounds.TryGetValue(item.Key, out var value9))
					{
						UnknownPackSets.Add(item.Key);
						continue;
					}
					List<Sound> list = new List<Sound>();
					List<float> list2 = new List<float>();
					foreach (byte[] item2 in item.Value)
					{
						CREATESOUNDEXINFO exinfo = new CREATESOUNDEXINFO
						{
							cbsize = Marshal.SizeOf(typeof(CREATESOUNDEXINFO)),
							length = (uint)item2.Length
						};
						MODE mode = (MODE)(0x908 | ((!Loops.Contains(item.Key)) ? 1 : 2));
						Sound sound2;
						RESULT rESULT = RuntimeManager.CoreSystem.createSound(item2, mode, ref exinfo, out sound2);
						if (rESULT != RESULT.OK)
						{
							Log.Error("packs: a sound of set " + item.Key + " could not be read: " + rESULT);
							continue;
						}
						sound2.getLength(out var length, TIMEUNIT.MS);
						list.Add(sound2);
						list2.Add((float)length / 1000f);
					}
					if (list.Count != 0)
					{
						_ownSets[item.Key] = value9;
						if (_lengths.TryGetValue(item.Key, out var value10))
						{
							_ownLengths[item.Key] = value10;
						}
						Sound[] array = list.ToArray();
						_sounds[item.Key] = array;
						_lengths[item.Key] = list2.ToArray();
						_packSets[item.Key] = array;
						_packFrom[item.Key] = item.Value;
						_lastVariant.Remove(item.Key);
						num += array.Length;
					}
				}
				if (soundSets.Count > 0 || num > 0)
				{
					Log.Info("packs: " + _packSets.Count + " sound sets from packs (" + num + " sounds)" + ((UnknownPackSets.Count > 0) ? ("; no such set: " + string.Join(", ", UnknownPackSets.ToArray())) : ""));
				}
			}
			catch (Exception ex)
			{
				Log.Error("packs: sounds: " + ex.Message);
			}
		}

		private static void MakePickupSounds(Action<string, int, Func<int, float[]>> add)
		{
			add("pickup", 3, Synth.Pickup);
		}

		public void PlayPickup(int run)
		{
			if (_on && !_failed && _haveGroup && _sounds.TryGetValue("pickup", out var value) && value.Length != 0)
			{
				_lastVariant.TryGetValue("pickup", out var value2);
				int num = ((value.Length > 1) ? ((value2 + 1 + _rng.Next(value.Length - 1)) % value.Length) : 0);
				_lastVariant["pickup"] = num;
				if (RuntimeManager.CoreSystem.playSound(value[num], _group, paused: true, out var channel) == RESULT.OK)
				{
					int num2 = PickupSteps[Mathf.Clamp(run, 0, PickupSteps.Length - 1)];
					channel.setVolume(0.3f);
					channel.setPitch(Mathf.Pow(2f, (float)num2 / 12f));
					channel.setPan(0.45f);
					channel.setPaused(paused: false);
				}
			}
		}

		private static void MakePingSounds(Action<string, int, Func<int, float[]>> add)
		{
			string[] array = new string[7] { "ping_look", "ping_go", "ping_enemy", "ping_danger", "ping_loot", "ping_defend", "ping_off" };
			for (int i = 0; i < array.Length; i++)
			{
				int kind = i;
				add(array[i], 1, (int v) => Synth.Ping(kind));
			}
		}

		public float LayerLevel(int layer)
		{
			return _battleLevel[layer];
		}

		public float BattleBeatNow()
		{
			if (!_battlePlaying || _battleBeat <= 0f || _battle[0].getPosition(out var position, TIMEUNIT.PCM) != RESULT.OK)
			{
				return -1f;
			}
			return (float)position / 32000f / _battleBeat;
		}

		public float Heard(int which)
		{
			Channel channel = ((which < 3) ? _battle[which] : ((which == 3) ? _calm : _sting));
			if (!channel.hasHandle() || channel.isPlaying(out var isplaying) != RESULT.OK || !isplaying)
			{
				return -1f;
			}
			if (channel.getVolume(out var volume) != RESULT.OK || channel.getDSPClock(out var _, out var parentclock) != RESULT.OK)
			{
				return -1f;
			}
			return volume * FadeAt(channel, parentclock);
		}

		public void CalmNowForTest()
		{
			_calmGap = 0f;
		}

		public static float GameMusicVolumeNow()
		{
			try
			{
				if (MusicBox.inst != null && MusicBox.inst._music_event.isValid() && MusicBox.inst._music_event.getVolume(out var volume) == RESULT.OK)
				{
					return volume;
				}
			}
			catch
			{
			}
			return -1f;
		}

		private static void MakeShotgunSounds(Action<string, int, Func<int, float[]>> add)
		{
			add("shotgun_blast", 3, Synth.ShotgunBlast);
			add("shotgun_pump_back", 2, Synth.ShotgunPumpBack);
			add("shotgun_pump_fwd", 2, Synth.ShotgunPumpForward);
			add("shotgun_shell_drop", 4, Synth.ShotgunShellDrop);
			add("shotgun_shell_in", 3, Synth.ShotgunShellIn);
			add("shotgun_dry", 2, Synth.ShotgunDry);
			add("shotgun_hit", 3, Synth.ShotgunHit);
			add("pellet_patter", 3, Synth.PelletPatter);
		}

		public void PlayShotgun(string key, float volume, float pan = 0f)
		{
			Play(key, volume, (key == "shotgun_blast") ? 0.025f : 0.05f, pan);
		}

		public int GameOneShotsOlderThan(float seconds)
		{
			return _gameSounds.FindAll((GameSound g) => !g.Bed && g.Follow == null && g.Age > seconds).Count;
		}

		public string GameOneShotsForTest()
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (GameSound gameSound in _gameSounds)
			{
				if (!gameSound.Bed && !(gameSound.Follow != null))
				{
					gameSound.Instance.getPlaybackState(out var state);
					gameSound.Instance.getPaused(out var paused);
					gameSound.Instance.getDescription(out var description);
					description.getPath(out var path);
					stringBuilder.Append($"[{path} {state} paused={paused} age={gameSound.Age:0.0} held={gameSound.Held} attached={gameSound.Attached} native={gameSound.Native}] ");
				}
			}
			return stringBuilder.ToString();
		}

		private void BeginSpatial()
		{
			if (_spatial || _spatialFailed || !SpatialWanted)
			{
				return;
			}
			if (!SpatialSupported)
			{
				_spatialFailed = true;
				SpatialNote = "off: macOS uses the game's panned audio";
				Log.Info("3D sound: macOS uses the game's panned audio");
				return;
			}
			if (CrashGuard.Suspect("spatial"))
			{
				_spatialFailed = true;
				SpatialNote = "off: it crashed the game last time on this platform";
				return;
			}
			CrashGuard.Enter("spatial");
			try
			{
				FMOD.System coreSystem = RuntimeManager.CoreSystem;
				if (!FindResonance(coreSystem))
				{
					_spatialFailed = true;
					CrashGuard.Leave("spatial");
					return;
				}
				if (coreSystem.getMasterChannelGroup(out var channelgroup) == RESULT.OK)
				{
					_coreMaster = channelgroup.handle;
				}
				if (!_haveWorld && coreSystem.createChannelGroup("first_person_world", out _world) == RESULT.OK)
				{
					_haveWorld = true;
				}
				if (!_haveWorld)
				{
					throw new InvalidOperationException("no channel group");
				}
				if (coreSystem.createDSPByPlugin(_listenerPlugin, out _listenerDsp) != RESULT.OK)
				{
					throw new InvalidOperationException("no Resonance listener");
				}
				if (_world.addDSP(-3, _listenerDsp) != RESULT.OK)
				{
					_listenerDsp.release();
					throw new InvalidOperationException("could not add the Resonance listener");
				}
				_haveListener = true;
				if (coreSystem.createDSPByType(DSP_TYPE.SFXREVERB, out _worldReverb) == RESULT.OK)
				{
					if (_world.addDSP(-1, _worldReverb) == RESULT.OK)
					{
						_haveWorldReverb = true;
						P(DSP_SFXREVERB.DECAYTIME, 1300f);
						P(DSP_SFXREVERB.EARLYDELAY, 15f);
						P(DSP_SFXREVERB.LATEDELAY, 28f);
						P(DSP_SFXREVERB.HFREFERENCE, 5000f);
						P(DSP_SFXREVERB.HFDECAYRATIO, 45f);
						P(DSP_SFXREVERB.DIFFUSION, 100f);
						P(DSP_SFXREVERB.DENSITY, 100f);
						P(DSP_SFXREVERB.LOWSHELFFREQUENCY, 250f);
						P(DSP_SFXREVERB.LOWSHELFGAIN, 0f);
						P(DSP_SFXREVERB.HIGHCUT, 6500f);
						P(DSP_SFXREVERB.EARLYLATEMIX, 60f);
						P(DSP_SFXREVERB.WETLEVEL, -18f);
						P(DSP_SFXREVERB.DRYLEVEL, 0f);
					}
					else
					{
						_worldReverb.release();
					}
				}
				_spatial = true;
				Live = this;
				SpatialNote = "on (Resonance Audio)";
				Log.Info("3D sound on: Resonance Audio listener on its own group");
			}
			catch (Exception ex)
			{
				_spatialFailed = true;
				SpatialNote = "failed: " + ex.Message;
				Log.Error("3D sound could not start, sounds stay panned: " + ex.Message);
				EndSpatial();
			}
			CrashGuard.Leave("spatial");
			void P(DSP_SFXREVERB p, float v)
			{
				_worldReverb.setParameterFloat((int)p, v);
			}
		}

		private void EndSpatial()
		{
			if (Live == this)
			{
				Live = null;
			}
			_spatial = false;
			bool flag = StudioGate.Enter(250);
			try
			{
				foreach (GameSound gameSound in _gameSounds)
				{
					LetGo(gameSound, all: true);
					if (gameSound.NativeSet != 1f && gameSound.Instance.isValid())
					{
						gameSound.Instance.setVolume(1f);
					}
				}
			}
			finally
			{
				if (flag)
				{
					StudioGate.Exit();
				}
			}
			_gameSounds.Clear();
			foreach (Voice voice in _voices)
			{
				voice.Channel.removeDSP(voice.Source);
				FadeOut(voice.Channel, 0.05f);
			}
			_voices.Clear();
			foreach (DSP allSource in _allSources)
			{
				allSource.disconnectAll(inputs: true, outputs: true);
				allSource.release();
			}
			_allSources.Clear();
			_freeSources.Clear();
			if (_haveWorld)
			{
				if (_haveWorldReverb)
				{
					_world.removeDSP(_worldReverb);
					_worldReverb.release();
				}
				if (_haveListener)
				{
					_world.removeDSP(_listenerDsp);
					_listenerDsp.release();
				}
			}
			_haveWorldReverb = (_haveListener = false);
			_ufoPlaying = (_dragonFirePlaying = false);
			if (SpatialNote.StartsWith("on"))
			{
				SpatialNote = "off";
			}
		}

		private static bool FindResonance(FMOD.System core)
		{
			if (_pluginSought)
			{
				if (_sourcePlugin != 0)
				{
					return _listenerPlugin != 0;
				}
				return false;
			}
			_pluginSought = true;
			if (core.getNumPlugins(PLUGINTYPE.DSP, out var numplugins) == RESULT.OK)
			{
				for (int i = 0; i < numplugins; i++)
				{
					if (core.getPluginHandle(PLUGINTYPE.DSP, i, out var handle) == RESULT.OK)
					{
						Note(core, handle);
					}
				}
			}
			if (_sourcePlugin == 0 || _listenerPlugin == 0)
			{
				string dataPath = Application.dataPath;
				string[] array = new string[7]
				{
					Path.Combine(dataPath, "Plugins/x86_64/resonanceaudio.dll"),
					Path.Combine(dataPath, "Plugins/resonanceaudio.dll"),
					Path.Combine(dataPath, "Plugins/resonanceaudio.bundle/Contents/MacOS/resonanceaudio"),
					Path.Combine(dataPath, "Plugins/resonanceaudio.bundle"),
					Path.Combine(dataPath, "Plugins/libresonanceaudio.so"),
					Path.Combine(dataPath, "Plugins/x86_64/libresonanceaudio.so"),
					"libresonanceaudio.so"
				};
				foreach (string text in array)
				{
					if ((text.IndexOf('/') >= 0 && !File.Exists(text) && !Directory.Exists(text)) || core.loadPlugin(text, out var handle2) != RESULT.OK)
					{
						continue;
					}
					Note(core, handle2);
					if (core.getNumNestedPlugins(handle2, out var count) == RESULT.OK)
					{
						for (int k = 0; k < count; k++)
						{
							if (core.getNestedPlugin(handle2, k, out var nestedhandle) == RESULT.OK)
							{
								Note(core, nestedhandle);
							}
						}
					}
					Log.Info("3D sound: loaded " + text);
					break;
				}
			}
			if (_sourcePlugin == 0 || _listenerPlugin == 0)
			{
				SpatialNote = "no Resonance Audio plugin";
				Log.Info("3D sound: the game's Resonance Audio plugin wasn't found, sounds stay panned");
				return false;
			}
			return true;
		}

		private static void Note(FMOD.System core, uint handle)
		{
			if (core.getPluginInfo(handle, out var plugintype, out var name, 64, out var _) == RESULT.OK && plugintype == PLUGINTYPE.DSP)
			{
				if (name == "Resonance Audio Source")
				{
					_sourcePlugin = handle;
				}
				else if (name == "Resonance Audio Listener")
				{
					_listenerPlugin = handle;
				}
			}
		}

		private DSP TakeSource()
		{
			if (_freeSources.Count > 0)
			{
				return _freeSources.Pop();
			}
			if (_allSources.Count >= 160)
			{
				return default(DSP);
			}
			if (RuntimeManager.CoreSystem.createDSPByPlugin(_sourcePlugin, out var dsp) != RESULT.OK)
			{
				return default(DSP);
			}
			dsp.setParameterInt(2, 2);
			_allSources.Add(dsp);
			return dsp;
		}

		private void FreeSource(DSP dsp)
		{
			if (dsp.hasHandle())
			{
				dsp.disconnectAll(inputs: true, outputs: true);
				dsp.setParameterFloat(0, 0f);
				dsp.setParameterFloat(1, 0f);
				dsp.setParameterFloat(3, 0f);
				_freeSources.Push(dsp);
			}
		}

		private void UpdateSpatial(State s, float dt, float sfxVolume)
		{
			if (SpatialWanted && !_spatial)
			{
				BeginSpatial();
			}
			else if (!SpatialWanted && _spatial)
			{
				EndSpatial();
			}
			_effectsScale = sfxVolume * EffectsVolume;
			_ambientScale = sfxVolume * AmbientVolume;
			float num = ((GroundAt != null) ? GroundAt(s.Position.x, s.Position.y) : 0f);
			_earHeight = num + Mathf.Max(0.2f, s.EyeAboveGround);
			_yawNow = s.Yaw;
			float num2 = Mathf.Cos(s.Yaw);
			float num3 = Mathf.Sin(s.Yaw);
			float num4 = 1f / Mathf.Sqrt(1f + s.Pitch * s.Pitch);
			float num5 = s.Pitch * num4;
			_ear = new Vector3(s.Position.x, s.Position.y, 0f - _earHeight);
			_earF = new Vector3(num2 * num4, num3 * num4, 0f - num5);
			_earU = new Vector3((0f - num2) * num5, (0f - num3) * num5, 0f - num4);
			_earR = Vector3.Cross(_earU, _earF);
			_studioEar = new Vector3(s.Position.x, s.Position.y, 0f - Mathf.Max(0.2f, s.EyeAboveGround));
			_studioF = new Vector3(num2, num3, 0f);
			UpdateFlat(dt);
			if (!_spatial)
			{
				return;
			}
			_spatialAt = Time.unscaledTime;
			long timestamp = Stopwatch.GetTimestamp();
			for (int num6 = _voices.Count - 1; num6 >= 0; num6--)
			{
				Voice voice = _voices[num6];
				if (voice.Channel.isPlaying(out var isplaying) != RESULT.OK || !isplaying)
				{
					FreeSource(voice.Source);
					_voices.RemoveAt(num6);
				}
				else
				{
					voice.Channel.setVolume(voice.Volume * (voice.Speech ? _speechScale : (voice.Ambient ? _ambientScale : _effectsScale)) * KeepVoice(voice));
					if (_clock >= voice.NextOcclusion)
					{
						voice.Occlusion = Occlusion(voice.At);
						voice.NextOcclusion = _clock + 0.15f;
						voice.Source.setParameterFloat(3, voice.Occlusion);
					}
					Place(voice.Source, voice.At);
				}
			}
			if (StudioGate.Enter())
			{
				try
				{
					UpdateGameSounds(dt);
				}
				finally
				{
					StudioGate.Exit();
				}
			}
			float num7 = (float)(Stopwatch.GetTimestamp() - timestamp) * 1000f / (float)Stopwatch.Frequency;
			SpatialMs = ((SpatialMs <= 0f) ? num7 : Mathf.Lerp(SpatialMs, num7, 0.1f));
		}

		public Channel PlayAtForTest(string key, Vector3 at, float volume)
		{
			return PlayAt(key, at, volume, 0f);
		}

		public float OcclusionForTest(Vector3 at)
		{
			return Occlusion(at);
		}

		public bool EarsForTest(out float left, out float right)
		{
			left = (right = 0f);
			if (!_haveListener)
			{
				return false;
			}
			_listenerDsp.setMeteringEnabled(inputEnabled: false, outputEnabled: true);
			if (_listenerDsp.getMeteringInfo(IntPtr.Zero, out var outputInfo) != RESULT.OK || outputInfo.numchannels < 2)
			{
				return false;
			}
			left = outputInfo.rmslevel[0];
			right = outputInfo.rmslevel[1];
			return true;
		}

		private void UpdateGameSounds(float dt)
		{
			for (int num = _gameSounds.Count - 1; num >= 0; num--)
			{
				GameSound gameSound = _gameSounds[num];
				gameSound.Age += dt;
				if (!gameSound.Instance.isValid())
				{
					Detach(gameSound);
					_gameSounds.RemoveAt(num);
					continue;
				}
				if (gameSound.Native)
				{
					KeepNativeHere(gameSound);
					continue;
				}
				Track(gameSound);
				gameSound.Instance.getPlaybackState(out var state);
				if (state == PLAYBACK_STATE.STOPPED && !gameSound.Held)
				{
					Detach(gameSound);
					if (gameSound.Follow == null && !gameSound.Bed && gameSound.Age > 1f)
					{
						_gameSounds.RemoveAt(num);
						continue;
					}
				}
				else if (!gameSound.Attached)
				{
					if (gameSound.Instance.getChannelGroup(out var group) == RESULT.OK && group.hasHandle())
					{
						Attach(gameSound, group);
					}
					else if (gameSound.Held && gameSound.Age > 0.3f)
					{
						LetGo(gameSound, all: false);
						GameSoundsNative++;
						continue;
					}
				}
				PlaceAhead(gameSound);
				if (!gameSound.Attached)
				{
					continue;
				}
				float num2 = KeepGame(gameSound);
				bool flag = _clock >= gameSound.NextGain;
				if (flag && !GroupAlive(gameSound))
				{
					Detach(gameSound);
					continue;
				}
				if (flag)
				{
					gameSound.Gain = BusGain(gameSound.Group);
					gameSound.NextGain = _clock + 0.25f;
				}
				if (flag || Mathf.Abs(num2 - gameSound.KeepSet) > 0.005f)
				{
					gameSound.KeepSet = num2;
					SetGain(gameSound);
				}
				if (_clock >= gameSound.NextOcclusion)
				{
					gameSound.Occlusion = Occlusion(gameSound.At);
					gameSound.NextOcclusion = _clock + 0.15f;
					gameSound.Source.setParameterFloat(3, gameSound.Occlusion);
				}
				PlaceGame(gameSound);
			}
		}

		private void PlaceGame(GameSound g)
		{
			if (CarryChanged(g))
			{
				SetGain(g);
			}
			Vector3 at = g.At;
			float num = at.x - _ear.x;
			float num2 = at.y - _ear.y;
			float num3 = Mathf.Sqrt(num * num + num2 * num2);
			float num4 = ((!g.Bed) ? 0f : ((g.Hear != null && g.Hear.Lava) ? Mathf.Lerp(120f, 40f, num3 / 12f) : 110f));
			if (num3 < 2f)
			{
				float t = num3 / 2f;
				at.z = Mathf.Lerp(_earHeight - 0.3f, at.z, t);
				num4 = Mathf.Max(num4, Mathf.Lerp(80f, 0f, t));
			}
			if (Mathf.Abs(num4 - g.Spread) > 1f)
			{
				g.Spread = num4;
				g.Source.setParameterFloat(1, num4);
			}
			Place(g.Source, at);
		}

		private void Attach(GameSound g, ChannelGroup group)
		{
			DSP dSP = TakeSource();
			if (!dSP.hasHandle() || group.addDSP(-1, dSP) != RESULT.OK)
			{
				if (dSP.hasHandle())
				{
					FreeSource(dSP);
				}
				LetGo(g, all: false);
				GameSoundsNative++;
				return;
			}
			g.Source = dSP;
			g.Group = group;
			g.Attached = true;
			g.Gain = BusGain(group);
			g.NextGain = _clock + 0.25f;
			g.Occlusion = Occlusion(g.At);
			g.NextOcclusion = _clock + 0.15f;
			g.KeepSet = KeepGame(g);
			g.Near = Carry(g);
			SetGain(g);
			g.Spread = -10f;
			dSP.setParameterFloat(3, g.Occlusion);
			PlaceGame(g);
			if (g.Held)
			{
				g.Instance.setPaused(paused: false);
				g.Held = false;
			}
			GameSoundsSpatial++;
		}

		private void SetGain(GameSound g)
		{
			if (g.Attached)
			{
				g.Source.setParameterFloat(0, Mathf.Clamp(20f * Mathf.Log10(Mathf.Max(g.Gain * g.KeepSet * g.Near, 0.0001f)) + 3f, -80f, 24f));
			}
		}

		private void Detach(GameSound g)
		{
			if (g.Attached)
			{
				if (GroupAlive(g))
				{
					g.Group.removeDSP(g.Source);
					FreeSource(g.Source);
				}
				else
				{
					g.Source.disconnectAll(inputs: true, outputs: true);
					g.Source.release();
					_allSources.Remove(g.Source);
				}
				g.Source = default(DSP);
				g.Group = default(ChannelGroup);
				g.Attached = false;
			}
		}

		private static bool GroupAlive(GameSound g)
		{
			if (g.Instance.isValid() && g.Instance.getChannelGroup(out var group) == RESULT.OK)
			{
				return group.handle == g.Group.handle;
			}
			return false;
		}

		private void LetGo(GameSound g, bool all)
		{
			Detach(g);
			g.Native = true;
			if (!g.Instance.isValid())
			{
				return;
			}
			if (g.Held)
			{
				if (g.Age > 1f)
				{
					g.Instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
				}
				else
				{
					g.Instance.setPaused(paused: false);
				}
				g.Held = false;
			}
			g.Instance.set3DAttributes(SpatialHooks.Attributes(new Vector3(g.At.x, g.At.y, 0f)));
			if (!(g.Follow != null))
			{
				return;
			}
			try
			{
				SpatialHooks.AttachToTransform(g.Instance, g.Follow);
			}
			catch
			{
			}
		}

		private float BusGain(ChannelGroup group)
		{
			float num = 1f;
			ChannelGroup channelGroup = group;
			for (int i = 0; i < 8; i++)
			{
				if (channelGroup.getParentGroup(out var group2) != RESULT.OK)
				{
					break;
				}
				if (!group2.hasHandle())
				{
					break;
				}
				if (group2.handle == _coreMaster)
				{
					break;
				}
				if (group2.getVolume(out var volume) == RESULT.OK)
				{
					num *= volume;
				}
				if (group2.getMute(out var mute) == RESULT.OK && mute)
				{
					num = 0f;
				}
				channelGroup = group2;
			}
			return num;
		}

		private void PlaceAhead(GameSound g)
		{
			float num = g.At.x - _ear.x;
			float num2 = g.At.y - _ear.y;
			float num3 = g.At.z - _earHeight;
			float num4 = Mathf.Max(0.05f, Mathf.Sqrt(num * num + num2 * num2 + num3 * num3));
			Vector3 vector = _studioEar + _studioF * num4;
			g.Instance.set3DAttributes(new ATTRIBUTES_3D
			{
				position = new VECTOR
				{
					x = vector.x,
					y = vector.y,
					z = vector.z
				},
				forward = new VECTOR
				{
					z = 1f
				},
				up = new VECTOR
				{
					y = 1f
				}
			});
		}

		private void Place(DSP source, Vector3 at)
		{
			Vector3 vector = new Vector3(at.x, at.y, 0f - at.z);
			Vector3 lhs = vector - _ear;
			float[] attr = _attr;
			attr[0] = Vector3.Dot(lhs, _earR);
			attr[1] = Vector3.Dot(lhs, _earU);
			attr[2] = Vector3.Dot(lhs, _earF);
			attr[3] = (attr[4] = (attr[5] = 0f));
			attr[6] = 0f;
			attr[7] = 0f;
			attr[8] = 1f;
			attr[9] = 0f;
			attr[10] = 1f;
			attr[11] = 0f;
			attr[12] = vector.x;
			attr[13] = vector.y;
			attr[14] = vector.z;
			attr[15] = (attr[16] = (attr[17] = 0f));
			attr[18] = _earF.x;
			attr[19] = _earF.y;
			attr[20] = _earF.z;
			attr[21] = _earU.x;
			attr[22] = _earU.y;
			attr[23] = _earU.z;
			Buffer.BlockCopy(attr, 0, _attrBytes, 0, 96);
			source.setParameterData(7, _attrBytes);
		}

		private float Occlusion(Vector3 at)
		{
			if (GroundAt == null)
			{
				return 0f;
			}
			float x = _ear.x;
			float y = _ear.y;
			float earHeight = _earHeight;
			float num = at.x - x;
			float num2 = at.y - y;
			float num3 = Mathf.Sqrt(num * num + num2 * num2);
			if (num3 < 2f)
			{
				return 0f;
			}
			int num4 = Mathf.Clamp(Mathf.CeilToInt(num3 / 1.5f), 3, 24);
			float num5 = 0f;
			for (int i = 1; i < num4; i++)
			{
				float num6 = (float)i / (float)num4;
				float num7 = GroundAt(x + num * num6, y + num2 * num6) - (earHeight + (at.z - earHeight) * num6);
				if (num7 > num5)
				{
					num5 = num7;
				}
			}
			return Mathf.Clamp01((num5 - 0.15f) / 2f) * 0.6f + Mathf.Clamp01((num3 - 12f) / 48f) * 0.15f;
		}

		private static float Height(float x, float y)
		{
			return ((GroundAt != null) ? GroundAt(x, y) : 0f) + 0.6f;
		}

		internal bool TakeOneShot(FMOD.Studio.EventInstance instance, Vector3 at, Actor speaker = null)
		{
			if (!Fresh || _gameSounds.Count >= 96 || !instance.isValid())
			{
				return false;
			}
			at = TileMiddle(at);
			GameSound gameSound = new GameSound
			{
				Instance = instance,
				At = new Vector3(at.x, at.y, Height(at.x, at.y)),
				Held = true
			};
			Begin(gameSound, speaker);
			instance.setPaused(paused: true);
			PlaceAhead(gameSound);
			_gameSounds.Add(gameSound);
			return true;
		}

		private void Begin(GameSound g, Actor speaker)
		{
			g.Hear = CarryingOf(g.Instance);
			if (speaker != null)
			{
				try
				{
					g.SpeakerId = speaker.data.id;
					g.Speaker = speaker;
				}
				catch
				{
					g.Speaker = null;
				}
			}
			Track(g);
			g.Near = Carry(g);
		}

		internal bool TakeAttached(FMOD.Studio.EventInstance instance, Transform follow, bool starting)
		{
			if (!Fresh || follow == null || _gameSounds.Count >= 96 || !instance.isValid())
			{
				return false;
			}
			RuntimeManager.DetachInstanceFromGameObject(instance);
			Vector3 position = follow.position;
			GameSound gameSound = new GameSound
			{
				Instance = instance,
				Follow = follow,
				At = new Vector3(position.x, position.y, Height(position.x, position.y)),
				Held = starting
			};
			Begin(gameSound, null);
			if (starting)
			{
				instance.setPaused(paused: true);
			}
			PlaceAhead(gameSound);
			_gameSounds.Add(gameSound);
			return true;
		}

		internal bool TakeBed(FMOD.Studio.EventInstance instance, float x, float y)
		{
			if (!Fresh || !instance.isValid())
			{
				return false;
			}
			for (int i = 0; i < _gameSounds.Count; i++)
			{
				GameSound gameSound = _gameSounds[i];
				if (!(gameSound.Instance.handle != instance.handle))
				{
					if (gameSound.Native)
					{
						return false;
					}
					gameSound.At = new Vector3(x, y, Height(x, y));
					return true;
				}
			}
			if (_gameSounds.Count >= 96)
			{
				return false;
			}
			GameSound gameSound2 = new GameSound
			{
				Instance = instance,
				Bed = true,
				At = new Vector3(x, y, Height(x, y))
			};
			Begin(gameSound2, null);
			PlaceAhead(gameSound2);
			_gameSounds.Add(gameSound2);
			return true;
		}

		private int Variant(string key, int count)
		{
			_lastVariant.TryGetValue(key, out var value);
			int num = _rng.Next(count);
			if (count > 1 && num == value)
			{
				num = (num + 1 + _rng.Next(count - 1)) % count;
			}
			_lastVariant[key] = num;
			return num;
		}

		private float PanToward(Vector3 at)
		{
			float num = at.x - _ear.x;
			float num2 = at.y - _ear.y;
			float num3 = Mathf.Max(0.6f, Mathf.Sqrt(num * num + num2 * num2));
			float num4 = Mathf.Cos(_yawNow);
			float num5 = Mathf.Sin(_yawNow);
			return Mathf.Clamp((num * num5 - num2 * num4) / num3, -1f, 1f) * 0.8f;
		}

		private Channel PlayAt(string key, Vector3 at, float volume, float pitchJitter, float pitch = 1f, int variant = -1, bool ambient = false)
		{
			if (volume <= 0.0005f || !_on || _failed || !_sounds.TryGetValue(key, out var value) || value.Length == 0)
			{
				return default(Channel);
			}
			float num = KeepKey(key, at, ambient);
			if (num <= 0.001f)
			{
				return default(Channel);
			}
			int num2 = ((variant >= 0) ? Mathf.Clamp(variant, 0, value.Length - 1) : Variant(key, value.Length));
			bool flag = _spatial && _voices.Count < 48;
			ChannelGroup channelgroup = (flag ? _world : (ambient ? _ambience : _group));
			if (!flag && !(ambient ? _haveAmbience : _haveGroup))
			{
				return default(Channel);
			}
			if (RuntimeManager.CoreSystem.playSound(value[num2], channelgroup, paused: true, out var channel) != RESULT.OK)
			{
				return default(Channel);
			}
			channel.setPitch(pitch * (1f + (float)(_rng.NextDouble() * 2.0 - 1.0) * pitchJitter));
			if (!flag || !Spatialize(channel, at, volume, ambient))
			{
				if (flag)
				{
					channel.setChannelGroup(ambient ? _ambience : _group);
				}
				channel.setVolume(volume * num);
				channel.setPan(PanToward(at));
			}
			else
			{
				_voices[_voices.Count - 1].Huge = HugeKeys.Contains(key);
			}
			channel.setPaused(paused: false);
			return channel;
		}

		private bool Spatialize(Channel channel, Vector3 at, float volume, bool ambient)
		{
			DSP dSP = TakeSource();
			if (!dSP.hasHandle())
			{
				return false;
			}
			if (channel.addDSP(-1, dSP) != RESULT.OK)
			{
				FreeSource(dSP);
				return false;
			}
			Voice voice = new Voice
			{
				Channel = channel,
				Source = dSP,
				At = at,
				Volume = volume,
				Ambient = ambient,
				Occlusion = Occlusion(at),
				NextOcclusion = _clock + 0.15f
			};
			dSP.setParameterFloat(3, voice.Occlusion);
			Place(dSP, at);
			channel.setVolume(volume * (ambient ? _ambientScale : _effectsScale));
			_voices.Add(voice);
			VoicesPlayed++;
			return true;
		}

		private void LoopAt(string key, ref Channel channel, ref bool playing, float volume, Vector3 at)
		{
			if (!_spatial)
			{
				Loop(key, ref channel, ref playing, volume * KeepKey(key, at, ambient: false));
				if (playing)
				{
					channel.setPan(PanToward(at));
				}
				return;
			}
			if (volume < 0.005f)
			{
				if (playing)
				{
					StopLoop(ref channel, ref playing);
				}
				return;
			}
			Voice voice = null;
			if (playing)
			{
				if (channel.isPlaying(out var isplaying) != RESULT.OK || !isplaying)
				{
					playing = false;
				}
				else
				{
					for (int i = 0; i < _voices.Count; i++)
					{
						if (_voices[i].Channel.handle == channel.handle)
						{
							voice = _voices[i];
							break;
						}
					}
					if (voice == null)
					{
						StopLoop(ref channel, ref playing);
					}
				}
			}
			if (!playing)
			{
				if (!_sounds.TryGetValue(key, out var value) || value.Length == 0 || RuntimeManager.CoreSystem.playSound(value[0], _world, paused: true, out channel) != RESULT.OK)
				{
					return;
				}
				if (value[0].getLength(out var length, TIMEUNIT.PCM) == RESULT.OK && length != 0)
				{
					channel.setPosition((uint)_rng.Next((int)length), TIMEUNIT.PCM);
				}
				if (!Spatialize(channel, at, volume, ambient: false))
				{
					channel.stop();
					return;
				}
				voice = _voices[_voices.Count - 1];
				voice.Huge = HugeKeys.Contains(key);
				channel.setPaused(paused: false);
				playing = true;
			}
			voice.Volume = volume;
			voice.At = at;
		}

		public void Talk(TalkLine line)
		{
			_talk = line;
		}

		private static void WantKind(string kind)
		{
			if (kind == null)
			{
				return;
			}
			lock (SpeechLock)
			{
				if (!_speechKinds.ContainsKey(kind) && !_speechWanted.Contains(kind))
				{
					_speechWanted.Add(kind);
					StartSpeechThread();
					Monitor.Pulse(SpeechLock);
				}
			}
		}

		private static VoiceSet KindSet(string kind)
		{
			if (kind == null)
			{
				return null;
			}
			lock (SpeechLock)
			{
				VoiceSet value;
				return _speechKinds.TryGetValue(kind, out value) ? value : null;
			}
		}

		private static bool KindRead(string kind)
		{
			lock (SpeechLock)
			{
				return _speechKinds.ContainsKey(kind);
			}
		}

		private static SpeechJob Ask(string kind, Mumble.Speaker who, string text, float seconds, bool opening, int seed)
		{
			SpeechJob speechJob = new SpeechJob
			{
				Kind = kind,
				Who = who,
				Text = text,
				Seconds = seconds,
				Opening = opening,
				Seed = seed
			};
			lock (SpeechLock)
			{
				_speechJobs.Add(speechJob);
				StartSpeechThread();
				Monitor.Pulse(SpeechLock);
				return speechJob;
			}
		}

		private static void StartSpeechThread()
		{
			if (_speechThread == null)
			{
				if (_bankPath == null)
				{
					_bankPath = FindBank();
				}
				_speechThread = new System.Threading.Thread(SpeechWork)
				{
					IsBackground = true,
					Name = "FirstPerson voices",
					Priority = System.Threading.ThreadPriority.BelowNormal
				};
				_speechThread.Start();
			}
		}

		private static string FindBank()
		{
			try
			{
				string streamingAssetsPath = Application.streamingAssetsPath;
				string text = Path.Combine(streamingAssetsPath, "Master.bank");
				if (File.Exists(text))
				{
					return text;
				}
				if (Directory.Exists(streamingAssetsPath))
				{
					string[] files = Directory.GetFiles(streamingAssetsPath, "Master.bank", SearchOption.AllDirectories);
					int num = 0;
					if (num < files.Length)
					{
						return files[num];
					}
				}
			}
			catch
			{
			}
			return "";
		}

		private static void SpeechWork()
		{
			while (true)
			{
				SpeechJob speechJob = null;
				string text = null;
				lock (SpeechLock)
				{
					while (_speechJobs.Count == 0 && _speechWanted.Count == 0)
					{
						Monitor.Wait(SpeechLock);
					}
					if (_speechJobs.Count > 0)
					{
						speechJob = _speechJobs[0];
						if (!_speechKinds.ContainsKey(speechJob.Kind))
						{
							text = speechJob.Kind;
						}
						else
						{
							_speechJobs.RemoveAt(0);
						}
					}
					else
					{
						text = _speechWanted[0];
						_speechWanted.RemoveAt(0);
					}
				}
				if (text != null)
				{
					VoiceSet value = ReadKind(text);
					lock (SpeechLock)
					{
						_speechKinds[text] = value;
						_speechWanted.Remove(text);
					}
				}
				else
				{
					Say(speechJob);
				}
			}
		}

		private static void Say(SpeechJob job)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			try
			{
				VoiceSet voiceSet = KindSet(job.Kind);
				if (voiceSet != null)
				{
					job.Phrase = ((job.Text == null) ? Mumble.Chatter(voiceSet, job.Who, job.Seed) : Mumble.Line(voiceSet, job.Who, job.Text, job.Seconds, job.Opening));
				}
			}
			catch (Exception ex)
			{
				Report("ERROR voices: saying a line: " + ex.Message);
			}
			job.Ms = stopwatch.ElapsedMilliseconds;
			job.Done = true;
		}

		private static void SayWaiting()
		{
			while (true)
			{
				SpeechJob speechJob = null;
				lock (SpeechLock)
				{
					for (int i = 0; i < _speechJobs.Count; i++)
					{
						if (_speechKinds.ContainsKey(_speechJobs[i].Kind))
						{
							speechJob = _speechJobs[i];
							_speechJobs.RemoveAt(i);
							break;
						}
					}
				}
				if (speechJob == null)
				{
					break;
				}
				Say(speechJob);
			}
		}

		private static VoiceSet ReadKind(string kind)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			try
			{
				if (_clips == null)
				{
					if (_clipsTried)
					{
						return null;
					}
					_clipsTried = true;
					if (string.IsNullOrEmpty(_bankPath))
					{
						Note("none: no Master.bank");
						return null;
					}
					_clips = GameClips.Open(_bankPath, out var error);
					if (_clips == null)
					{
						Note("none: " + error);
						return null;
					}
				}
				VoiceSet voiceSet = _clips.Kind(kind, SayWaiting);
				Report((voiceSet == null) ? ("voices: no clips for " + kind) : $"voices: {kind} read ({voiceSet.Clips.Count} clips, {voiceSet.Usable} syllables, {voiceSet.MedianHz:0} Hz) in {stopwatch.ElapsedMilliseconds} ms");
				int num = ((voiceSet != null) ? 1 : 0);
				lock (SpeechLock)
				{
					foreach (KeyValuePair<string, VoiceSet> speechKind in _speechKinds)
					{
						if (speechKind.Value != null)
						{
							num++;
						}
					}
				}
				Mark("ready: " + num + " kinds");
				return voiceSet;
			}
			catch (Exception ex)
			{
				Note("none: " + ex.Message);
				return null;
			}
		}

		private static void Report(string line)
		{
			lock (SpeechLock)
			{
				_speechLog.Enqueue(line);
			}
		}

		private static void Mark(string note)
		{
			lock (SpeechLock)
			{
				SpeechNote = note;
			}
		}

		private static void Note(string note)
		{
			Mark(note);
			Report("voices: " + note);
		}

		private void UpdateSpeech(float dt)
		{
			if (_speechBroken)
			{
				return;
			}
			try
			{
				UpdateSpeechNow(dt);
			}
			catch (Exception ex)
			{
				_speechBroken = true;
				Log.Error("voices: stopped: " + ex);
				try
				{
					EndSpeech();
				}
				catch
				{
				}
			}
		}

		private void UpdateSpeechNow(float dt)
		{
			lock (SpeechLock)
			{
				while (_speechLog.Count > 0)
				{
					string text = _speechLog.Dequeue();
					if (text.StartsWith("ERROR"))
					{
						Log.Error(text.Substring(6));
					}
					else
					{
						Log.Info(text);
					}
				}
			}
			if (!_wantedMain && SpeechOn)
			{
				_wantedMain = true;
				string[] array = new string[4] { "human", "elf", "orc", "dwarf" };
				for (int i = 0; i < array.Length; i++)
				{
					WantKind(array[i]);
				}
			}
			float target = ((_talk.Open && SpeechOn) ? 0.708f : 1f);
			_dip = Mathf.MoveTowards(_dip, target, Mathf.Min(dt, 0.05f) / 0.35f * 0.292f);
			if (_haveWorld)
			{
				_world.setVolume(_spatial ? _dip : 1f);
			}
			_speechScale = _sfxVolume * VoicesVolume * 0.7f / (_spatial ? _dip : 1f);
			UpdateTalkSpeech();
			UpdateChatter();
		}

		private void UpdateTalkSpeech()
		{
			TalkLine talk = _talk;
			if (!talk.Open || !talk.Voiced || !SpeechOn || talk.Kind == null || string.IsNullOrEmpty(talk.Text))
			{
				if (_talkSaid != null)
				{
					StopSaid(_talkSaid, 0.15f);
				}
				_talkSaid = null;
				_talkKey = 0;
				return;
			}
			if (talk.Key != _talkKey || _talkSaid == null)
			{
				if (_talkSaid != null)
				{
					StopSaid(_talkSaid, 0.08f);
				}
				_talkKey = talk.Key;
				_talkSaid = new Said
				{
					Job = Ask(talk.Kind, talk.Who, talk.Text, talk.Seconds, VoiceStyle == 1, 0),
					AskedAt = Time.unscaledTime,
					At = talk.Mouth,
					SpeakerId = talk.With
				};
				_lastAge = talk.Age;
			}
			Said talkSaid = _talkSaid;
			if (!talkSaid.Playing && talkSaid.Job != null && talkSaid.Job.Done && talk.Age >= 0f)
			{
				Mumble.Phrase phrase = talkSaid.Job.Phrase;
				float num = ((talk.Age > 0.12f) ? talk.Age : 0f);
				if (phrase != null && num < phrase.Seconds - 0.2f)
				{
					Start(talkSaid, phrase, TalkAt(talk), 1f, num);
				}
				talkSaid.Job = null;
			}
			if (talkSaid.Playing)
			{
				Move(talkSaid, TalkAt(talk));
				if (talk.Typed && talk.Age > _lastAge + 0.5f && talkSaid.Ends - Time.unscaledTime > 0.25f)
				{
					StopSaid(talkSaid, 0.2f);
				}
			}
			_lastAge = talk.Age;
		}

		private Vector3 TalkAt(TalkLine t)
		{
			if (t.Mouth != Vector3.zero)
			{
				return t.Mouth;
			}
			float num = Mathf.Cos(_yawNow);
			float num2 = Mathf.Sin(_yawNow);
			return new Vector3(_ear.x + num * 1.2f, _ear.y + num2 * 1.2f, _earHeight - 0.1f);
		}

		internal bool TakeChatter(Actor a)
		{
			if (!_on || _failed || _speechBroken || ChatterOffForTest || !SpeechOn || a == null)
			{
				return false;
			}
			try
			{
				if (a.asset == null || !Dialogue.IsPerson(a))
				{
					return false;
				}
				string text = VoiceSets.KindOf(a.asset.sound_idle);
				if (text == null || VoiceSets.Of(text) == null)
				{
					return false;
				}
				if (!KindRead(text))
				{
					WantKind(text);
					return false;
				}
				if (KindSet(text) == null)
				{
					return false;
				}
				long id = a.data.id;
				if (_talk.Open && id == _talk.With)
				{
					return true;
				}
				Vector3 at = MouthOf(a);
				if (Vector2.Distance(new Vector2(at.x, at.y), new Vector2(_ear.x, _ear.y)) > 30f)
				{
					return false;
				}
				if (_chatter.Count >= 6)
				{
					return true;
				}
				bool mute;
				Mumble.Speaker who = Speakers.Of(a, out mute);
				if (mute)
				{
					return true;
				}
				_chatter.Add(new Said
				{
					Job = Ask(text, who, null, 0f, opening: false, _rng.Next()),
					Speaker = a,
					SpeakerId = id,
					At = at,
					AskedAt = Time.unscaledTime
				});
				return true;
			}
			catch
			{
				return false;
			}
		}

		private void UpdateChatter()
		{
			for (int num = _chatter.Count - 1; num >= 0; num--)
			{
				Said said = _chatter[num];
				if (!said.Playing)
				{
					if (said.Job == null || Time.unscaledTime - said.AskedAt > 1.5f)
					{
						_chatter.RemoveAt(num);
					}
					else if (said.Job.Done)
					{
						Mumble.Phrase phrase = said.Job.Phrase;
						said.Job = null;
						if (phrase == null || !Alive(said))
						{
							_chatter.RemoveAt(num);
						}
						else
						{
							said.At = MouthOf(said.Speaker);
							float num2 = Vector2.Distance(new Vector2(said.At.x, said.At.y), new Vector2(_ear.x, _ear.y));
							Start(said, phrase, said.At, 0.5f / (1f + Mathf.Pow(num2 / 5f, 1.4f)));
							if (!said.Playing)
							{
								_chatter.RemoveAt(num);
							}
							else
							{
								ChatterSaid++;
							}
						}
					}
				}
				else if (Time.unscaledTime >= said.Ends)
				{
					Free(said);
					_chatter.RemoveAt(num);
				}
				else if (Alive(said))
				{
					Move(said, MouthOf(said.Speaker));
				}
			}
		}

		private static bool Alive(Said s)
		{
			try
			{
				return s.Speaker != null && s.Speaker.isAlive() && s.Speaker.data != null && s.Speaker.data.id == s.SpeakerId;
			}
			catch
			{
				return false;
			}
		}

		internal static Vector3 MouthOf(Actor a)
		{
			if (Hitboxes.Get(a, out var box))
			{
				return new Vector3(box.Centre.X, box.Centre.Y, box.Bottom + (box.Top - box.Bottom) * 0.85f);
			}
			Vector2 current_position = a.current_position;
			return new Vector3(current_position.x, current_position.y, Height(current_position.x, current_position.y) + a.position_height);
		}

		private void Start(Said s, Mumble.Phrase p, Vector3 at, float volume, float from = 0f)
		{
			FMOD.System coreSystem = RuntimeManager.CoreSystem;
			byte[] array = Synth.ToPcm16(p.S);
			CREATESOUNDEXINFO exinfo = new CREATESOUNDEXINFO
			{
				cbsize = Marshal.SizeOf(typeof(CREATESOUNDEXINFO)),
				length = (uint)array.Length,
				numchannels = 1,
				defaultfrequency = 48000,
				format = SOUND_FORMAT.PCM16
			};
			MODE mode = MODE.LOOP_OFF | MODE._2D | MODE.CREATESAMPLE | MODE.OPENMEMORY | MODE.OPENRAW;
			if (coreSystem.createSound(array, mode, ref exinfo, out s.Sound) != RESULT.OK)
			{
				return;
			}
			float num = KeepKey("speech", at, ambient: false);
			if (num <= 0.001f)
			{
				s.Sound.release();
				s.Sound = default(Sound);
				return;
			}
			s.At = at;
			s.Volume = volume;
			bool flag = _spatial && _voices.Count < 48;
			ChannelGroup channelgroup = (flag ? _world : SpeechGroup());
			if (coreSystem.playSound(s.Sound, channelgroup, paused: true, out s.Channel) != RESULT.OK)
			{
				s.Sound.release();
				s.Sound = default(Sound);
				return;
			}
			s.Spatial = flag && Spatialize(s.Channel, at, volume, ambient: false);
			if (s.Spatial)
			{
				Voice voice = _voices[_voices.Count - 1];
				voice.Speech = true;
				voice.Channel.setVolume(volume * _speechScale * KeepVoice(voice));
			}
			else
			{
				s.Channel.setChannelGroup(SpeechGroup());
				s.Channel.setVolume(volume * num * FlatSpeechScale);
				s.Channel.setPan(PanToward(at));
			}
			if (from > 0f)
			{
				s.Channel.setPosition((uint)(from * 1000f), TIMEUNIT.MS);
				FadeIn(s.Channel, 0.04f);
			}
			s.Channel.setPaused(paused: false);
			s.Playing = true;
			s.Ends = Time.unscaledTime + p.Seconds - from + 0.05f;
			SpeechSaid++;
			LastSpeechSeconds = p.Seconds;
			LastSpeechSyllables = p.Syllables;
			LastSpeechAt = at;
			LastSpeechMs = ((s.Job != null) ? s.Job.Ms : 0);
		}

		private ChannelGroup SpeechGroup()
		{
			if (!_haveSpeechGroup && RuntimeManager.CoreSystem.createChannelGroup("first_person_speech", out _speechGroup) == RESULT.OK)
			{
				_haveSpeechGroup = true;
			}
			if (!_haveSpeechGroup)
			{
				return _group;
			}
			return _speechGroup;
		}

		private void Move(Said s, Vector3 at)
		{
			s.At = at;
			if (!s.Playing)
			{
				return;
			}
			if (s.Spatial)
			{
				for (int i = 0; i < _voices.Count; i++)
				{
					if (_voices[i].Channel.handle == s.Channel.handle)
					{
						_voices[i].At = at;
						break;
					}
				}
			}
			else
			{
				s.Channel.setVolume(s.Volume * KeepKey("speech", at, ambient: false) * FlatSpeechScale);
				s.Channel.setPan(PanToward(at));
			}
		}

		private void StopSaid(Said s, float fade)
		{
			if (s.Playing && s.Channel.hasHandle())
			{
				FadeOut(s.Channel, fade);
			}
			s.Job = null;
			s.Playing = false;
			if (s.Sound.hasHandle())
			{
				ReleaseLater(ref s.Sound, fade);
			}
		}

		private void Free(Said s)
		{
			s.Playing = false;
			if (s.Sound.hasHandle())
			{
				ReleaseLater(ref s.Sound, 0.05f);
			}
		}

		private void EndSpeech()
		{
			if (_talkSaid != null)
			{
				StopSaid(_talkSaid, 0.15f);
			}
			_talkSaid = null;
			_talkKey = 0;
			foreach (Said item in _chatter)
			{
				StopSaid(item, 0.1f);
			}
			_chatter.Clear();
			_talk = default(TalkLine);
			_dip = 1f;
			if (_haveWorld)
			{
				_world.setVolume(1f);
			}
		}

		public static bool KindReadyForTest(string kind)
		{
			return KindSet(kind) != null;
		}

		internal static VoiceSet KindSetForTest(string kind)
		{
			return KindSet(kind);
		}

		public static bool KindTriedForTest(string kind)
		{
			return KindRead(kind);
		}

		public static void WantKindForTest(string kind)
		{
			WantKind(kind);
		}

		public float TalkVolumeForTest()
		{
			if (_talkSaid == null || !_talkSaid.Playing || !_talkSaid.Channel.hasHandle())
			{
				return 0f;
			}
			if (_talkSaid.Channel.isPlaying(out var isplaying) != RESULT.OK || !isplaying)
			{
				return 0f;
			}
			_talkSaid.Channel.getVolume(out var volume);
			return volume;
		}

		public void PlaySwing(WeaponKind kind, bool melee, bool hands)
		{
			if (!_on || _failed)
			{
				return;
			}
			if (!melee && kind == WeaponKind.Gun)
			{
				Play("zap", 0.5f, 0.06f);
			}
			else
			{
				if (kind == WeaponKind.Shotgun)
				{
					return;
				}
				if (!melee && kind == WeaponKind.Staff)
				{
					Play("cast", 0.5f, 0.05f);
					return;
				}
				bool flag = kind == WeaponKind.Axe || kind == WeaponKind.Hammer;
				Channel channel = PlayChannel(SwingKey(kind), flag ? 0.55f : 0.47f, 0.05f);
				if (channel.hasHandle())
				{
					float num;
					switch (kind)
					{
					default:
						num = 1f;
						break;
					case WeaponKind.Sword:
					case WeaponKind.Spear:
						num = 0.95f;
						break;
					case WeaponKind.Axe:
						num = 0.88f;
						break;
					case WeaponKind.Hammer:
						num = 0.82f;
						break;
					}
					float num2 = num;
					if (num2 < 1f && channel.getPitch(out var pitch) == RESULT.OK)
					{
						channel.setPitch(pitch * num2);
					}
				}
			}
		}

		public void PlayDash(float charge)
		{
			if (_on && !_failed)
			{
				Play("pushoff", (0.4f + 0.25f * charge) * StepVolume, 0.05f, 0f, 0.92f);
				Play("swing_heavy", 0.3f + 0.25f * charge, 0.05f, 0f, 0.8f - 0.1f * charge);
			}
		}

		private static void MakeUfoSounds(Action<string, int, Func<int, float[]>> add)
		{
			add("ufo_hum", 1, (int v) => Synth.UfoHum(4f));
		}

		private void UpdateUfos(State s, float dt)
		{
			float num = 0f;
			List<Vector3> ufoSpots = SceneCollector.UfoSpots;
			for (int i = 0; i < ufoSpots.Count; i++)
			{
				Vector3 vector = ufoSpots[i];
				float num2 = vector.x - s.Position.x;
				float num3 = vector.y - s.Position.y;
				float num4 = vector.z - s.EyeAboveGround;
				float num5 = Mathf.Sqrt(num2 * num2 + num3 * num3 + num4 * num4);
				float num6 = 1f / (1f + Mathf.Pow(num5 / 9f, 1.7f));
				if (!(num6 <= num))
				{
					num = num6;
					_ufoAt = new Vector3(vector.x, vector.y, ((GroundAt != null) ? GroundAt(vector.x, vector.y) : 0f) + vector.z);
				}
			}
			if (s.Indoors)
			{
				num *= 0.3f;
			}
			if (s.Dead)
			{
				num = 0f;
			}
			_ufoVolume = Mathf.Lerp(_ufoVolume, num * 0.55f, 1f - Mathf.Exp(-3f * dt));
			if (!(_ufoVolume < 0.004f) || _ufoPlaying)
			{
				LoopAt("ufo_hum", ref _ufoChannel, ref _ufoPlaying, (_ufoVolume < 0.004f) ? 0f : _ufoVolume, _ufoAt);
			}
		}

		private static void MakeWindUpSounds(Action<string, int, Func<int, float[]>> add)
		{
			add("windup_swish", 3, (int v) => Synth.WindUpSwish(v, heavy: false));
			add("windup_heavy", 2, (int v) => Synth.WindUpSwish(v, heavy: true));
			add("windup_ring", 2, Synth.WindUpRing);
			add("windup_snarl", 3, Synth.WindUpSnarl);
		}

		public void PlayWindUp(Vector3 at, WeaponKind kind, bool beast)
		{
			if (!_on || _failed)
			{
				return;
			}
			WindUpsPlayed++;
			if (beast)
			{
				PlayAt("windup_snarl", at, 0.55f, 0.06f);
				return;
			}
			bool flag = kind == WeaponKind.Axe || kind == WeaponKind.Hammer;
			PlayAt(flag ? "windup_heavy" : "windup_swish", at, 0.5f, 0.05f);
			if (kind == WeaponKind.Sword || kind == WeaponKind.Dagger || kind == WeaponKind.Axe || kind == WeaponKind.Spear)
			{
				PlayAt("windup_ring", at, 0.3f, 0.04f);
			}
		}

		public void PlayGuardUp()
		{
			if (_on && !_failed)
			{
				Play("cloth", 0.28f, 0.06f);
			}
		}

		public void PlayGuardBroken()
		{
			if (_on && !_failed)
			{
				Channel channel = PlayChannel("clash_metal", 0.85f, 0.03f);
				if (channel.hasHandle() && channel.getPitch(out var pitch) == RESULT.OK)
				{
					channel.setPitch(pitch * 0.72f);
				}
			}
		}

		private static void MakeWonderSounds(Action<string, int, Func<int, float[]>> add)
		{
			add("wonder_egg_hum", 1, (int v) => Synth.WonderEggHum(7f));
			add("wonder_egg_chime", 1, (int v) => Synth.WonderEggChime());
			add("wonder_harp_air", 1, (int v) => Synth.WonderHarpAir(9f));
			add("wonder_harp_gliss", 1, (int v) => Synth.WonderHarpGliss());
			add("wonder_mold_breath", 1, (int v) => Synth.WonderMoldBreath(7f));
			add("wonder_mold_puff", 1, (int v) => Synth.WonderMoldPuff());
			add("wonder_computer_hum", 1, (int v) => Synth.WonderComputerHum(8f));
			add("wonder_computer_call", 1, (int v) => Synth.WonderComputerCall());
			add("wonder_tumor_heart", 1, (int v) => Synth.WonderTumorHeart());
			add("wonder_biomass_bubble", 1, (int v) => Synth.WonderBiomassBubble(7f));
			add("wonder_pumpkin_crackle", 1, (int v) => Synth.WonderPumpkinCrackle(7f));
			add("wonder_cyber_scan", 1, (int v) => Synth.WonderCyberScan(6f));
			add("wonder_brain_choir", 1, (int v) => Synth.WonderBrainChoir(9f));
			add("wonder_badbrain_throb", 1, (int v) => Synth.WonderBadBrainThrob());
			add("wonder_fire_roar", 1, (int v) => Synth.WonderFireRoar(7f));
			add("wonder_ice_wind", 1, (int v) => Synth.WonderIceWind(9f));
			add("wonder_angle_chime", 1, (int v) => Synth.WonderAngleChime(9f));
		}

		private void EndWonders()
		{
			foreach (KeyValuePair<string, WonderLoop> wonderLoop in _wonderLoops)
			{
				WonderLoop value = wonderLoop.Value;
				StopLoop(ref value.Channel, ref value.Playing, 0.5f);
				value.Volume = (value.Want = 0f);
			}
			SceneCollector.WonderPulses.Clear();
		}

		private void UpdateWonders(State s, float dt)
		{
			foreach (WonderLoop value5 in _wonderLoops.Values)
			{
				value5.Want = 0f;
			}
			bool num = Time.frameCount - SceneCollector.WonderSpotFrame <= 3;
			List<SceneCollector.WonderSpot> wonderSpots = SceneCollector.WonderSpots;
			if (num)
			{
				for (int i = 0; i < wonderSpots.Count; i++)
				{
					if (wonderSpots[i].Family != null && WonderSounds.TryGetValue(wonderSpots[i].Family, out (string, float, string) value))
					{
						Vector3 at = wonderSpots[i].At;
						float num2 = at.x - s.Position.x;
						float num3 = at.y - s.Position.y;
						float num4 = at.z - s.EyeAboveGround;
						float num5 = Mathf.Sqrt(num2 * num2 + num3 * num3 + num4 * num4);
						float num6 = value.Item2 / (1f + num5 / 5f * (num5 / 5f));
						if (!_wonderLoops.TryGetValue(value.Item1, out var value2))
						{
							value2 = (_wonderLoops[value.Item1] = new WonderLoop());
						}
						if (!(num6 <= value2.Want))
						{
							value2.Want = num6;
							value2.At = new Vector3(at.x, at.y, ((GroundAt != null) ? GroundAt(at.x, at.y) : 0f) + at.z);
						}
					}
				}
			}
			WonderHeard = null;
			WonderHeardVolume = 0f;
			foreach (KeyValuePair<string, WonderLoop> wonderLoop2 in _wonderLoops)
			{
				WonderLoop value3 = wonderLoop2.Value;
				float num7 = value3.Want;
				if (s.Indoors)
				{
					num7 *= 0.3f;
				}
				if (s.Dead)
				{
					num7 = 0f;
				}
				value3.Volume = Mathf.Lerp(value3.Volume, num7, 1f - Mathf.Exp(-2.5f * dt));
				if (!(value3.Volume < 0.004f) || value3.Playing)
				{
					LoopAt(wonderLoop2.Key, ref value3.Channel, ref value3.Playing, (value3.Volume < 0.004f) ? 0f : value3.Volume, value3.At);
					if (value3.Volume > WonderHeardVolume)
					{
						WonderHeardVolume = value3.Volume;
						WonderHeard = wonderLoop2.Key;
					}
				}
			}
			List<SceneCollector.WonderSpot> wonderPulses = SceneCollector.WonderPulses;
			for (int j = 0; j < wonderPulses.Count; j++)
			{
				if (wonderPulses[j].Family != null && WonderSounds.TryGetValue(wonderPulses[j].Family, out (string, float, string) value4) && value4.Item3 != null)
				{
					Vector3 at2 = wonderPulses[j].At;
					float num8 = at2.x - s.Position.x;
					float num9 = at2.y - s.Position.y;
					float num10 = Mathf.Sqrt(num8 * num8 + num9 * num9);
					float num11 = 0.55f / (1f + num10 / 10f * (num10 / 10f));
					if (s.Indoors)
					{
						num11 *= 0.35f;
					}
					if (!(num11 < 0.01f) && !s.Dead)
					{
						PlayAt(value4.Item3, new Vector3(at2.x, at2.y, ((GroundAt != null) ? GroundAt(at2.x, at2.y) : 0f) + at2.z), num11, 0.03f);
						WonderCallsPlayed++;
					}
				}
			}
			wonderPulses.Clear();
		}
	}
}
