using System;
using System.Collections.Generic;
using System.Globalization;

namespace FirstPerson.Core
{
	public sealed class Hands
	{
		private struct Hand
		{
			public Vec3 W;

			public Vec3 D;

			public Vec3 T;

			public bool Show;
		}

		private struct TrackMap
		{
			public Vec3 G;

			public Vec3 Anchor;

			public Vec3 R;

			public Vec3 F;

			public Vec3 U;

			public Vec3 QAxis;

			public float QAngle;

			public Vec3 Point(Vec3 p)
			{
				return G + Dir(p - Anchor);
			}

			public Vec3 Dir(Vec3 d)
			{
				Vec3 vec = ((QAngle != 0f) ? Vec3.Rotate(d, QAxis, QAngle) : d);
				return R * vec.X + F * vec.Y + U * vec.Z;
			}

			public Hand Hand(Hand h)
			{
				return new Hand
				{
					W = Point(h.W),
					D = Dir(h.D),
					T = Dir(h.T),
					Show = h.Show
				};
			}
		}

		private readonly MeshInstance _wrap = new MeshInstance
		{
			NoShadow = true
		};

		private readonly MeshInstance _roll = new MeshInstance
		{
			NoShadow = true
		};

		private readonly MeshInstance _tail = new MeshInstance
		{
			NoShadow = true
		};

		private static MeshModel _rollModel;

		private static MeshModel _tailModel;

		private MeshModel _bandageRoll;

		private Vec3 _armROrigin;

		private Vec3 _armRX;

		private Vec3 _armRY;

		private Vec3 _armRZ;

		private MeshModel _armRModel;

		private const float WrapRaise = 0.2f;

		private const float WrapLower = 0.84f;

		private const float WrapTurns = 2f;

		private const float RollNear = 0.056f;

		private const float RollUp = 0.036f;

		private const float RollSize = 1.25f;

		private const float WrapFrom = 0.08f;

		private const float WrapTo = 0.2f;

		private readonly MeshInstance _binoculars = new MeshInstance
		{
			NoShadow = true
		};

		private static MeshModel _binocularsModel;

		private static readonly Vec3 BinocularsLow = new Vec3(0.03f, 0.28f, -0.2f);

		private static readonly Vec3 BinocularsAtEyes = new Vec3(0f, 0.078f, -0.017f);

		private const float BinocularsLowTilt = -0.55f;

		private const float BinocularsLowRoll = 0.22f;

		public Vec3 BlasterMuzzle;

		public readonly List<Vec3> BlasterVents = new List<Vec3>(4);

		public readonly List<Vec3> BlasterVentDirs = new List<Vec3>(4);

		public bool BlasterShown;

		private MeshModel _blasterModel;

		private uint[] _blasterPalette;

		private int _sBarrel = -1;

		private int _sRing = -1;

		private int _sGlow = -1;

		private int _sLens = -1;

		private readonly int[] _sGauge = new int[4] { -1, -1, -1, -1 };

		private int _pKnob = -1;

		private int _pTrigger = -1;

		private Vec3 _knobAt = new Vec3(-0.0286f, 0.014f, 0.064f);

		private Vec3 _muzzleAt = new Vec3(0f, 0.2145f, 0.065f);

		private readonly List<Vec3> _ventsAt = new List<Vec3>();

		private readonly List<Vec3> _ventDirs = new List<Vec3>();

		private BlasterState _blaster;

		private static readonly uint GaugeCool = TerrainCache.Rgb(90, 220, 255);

		private static readonly uint GaugeOff = TerrainCache.Rgb(26, 52, 64);

		private readonly MeshInstance _compass = new MeshInstance
		{
			NoShadow = true
		};

		private static MeshModel _compassModel;

		private const int CompassLid = 1;

		private const int CompassCard = 2;

		private const int CompassPointer = 3;

		private static readonly Vec3 CompassAt = new Vec3(0.116f, 0.29f, -0.098f);

		private static readonly Vec3 CompassFrom = new Vec3(0.2f, 0.22f, -0.4f);

		private static readonly Vec3 CompassFace = new Vec3(-0.21f, -0.52f, 0.83f);

		private const float CaseR = 0.03f;

		private const float CaseBottom = -0.0075f;

		private const float CaseRim = 0.0042f;

		private const float HingeY = 0.0312f;

		private const float CardPull = 55f;

		private const float CardDrag = 5.5f;

		private const float PointerPull = 70f;

		private const float PointerDrag = 10f;

		private readonly MeshInstance _armR = new MeshInstance
		{
			NoShadow = true
		};

		private readonly MeshInstance _armL = new MeshInstance
		{
			NoShadow = true
		};

		private readonly MeshInstance _item = new MeshInstance
		{
			NoShadow = true
		};

		private readonly MeshInstance _string = new MeshInstance
		{
			NoShadow = true
		};

		private readonly MeshInstance _arrow = new MeshInstance
		{
			NoShadow = true
		};

		private readonly MeshInstance _prop = new MeshInstance
		{
			NoShadow = true
		};

		private float _cocked;

		private object _paletteOwner;

		private int _paletteLook = int.MinValue;

		private uint _paletteSleeve;

		private uint _skin = TerrainCache.Rgb(232, 184, 140);

		private uint _sleeve = TerrainCache.Rgb(128, 84, 60);

		private string _heldId;

		private WeaponLook _look;

		private MeshModel _heldModel;

		private float _windupScale = 1f;

		private float _equip = 1f;

		private float _lagX;

		private float _lagZ;

		private float _lastYaw;

		private float _lastPitch;

		private bool _haveLast;

		private int _lastAttacks = -1;

		private bool _leftPunch;

		private int _slashSide = 1;

		private float _hurtPulse;

		private float _lastHurt;

		private Vec3 _eye;

		private Vec3 _r;

		private Vec3 _f;

		private Vec3 _u;

		private float _pitch;

		private float _s;

		private float _fovK = 1f;

		private Vec3 _fl;

		private float _slope;

		public float Aspect = 0.5625f;

		private static readonly Dictionary<uint, VoxelShape> GlowShapes = new Dictionary<uint, VoxelShape>();

		private static readonly Vec3 CarryGrip = new Vec3(-0.07f, 0.41f, -0.215f);

		private static readonly Vec3 AimGrip = new Vec3(-0.004f, 0.5f, -0.14f);

		private static readonly Vec3 AimArrow = new Vec3(-0.05f, 1f, 0.04f).Normalized();

		private static readonly Vec3 Quiver = new Vec3(0.26f, 0.02f, 0.2f);

		private const float CarryCant = 1.18f;

		private const float AimCant = -0.5f;

		private const float FollowThrough = 0.3f;

		private const float ArrowLength = 0.47f;

		private const float BraceY = -0.105f;

		private const float DrawLength = 0.27f;

		private const float RestAbove = 0.034f;

		private Vec3 _bowX = new Vec3(1f, 0f, 0f);

		private Vec3 _bowY = new Vec3(0f, 1f, 0f);

		private Vec3 _bowZ = new Vec3(0f, 0f, 1f);

		private Vec3 _nockRel;

		private float _nockY = -0.105f;

		private float _flex;

		private float _raise;

		private float _vibrate;

		private float _shotDraw;

		private bool _onString;

		private bool _arrowOn = true;

		private bool _heldArrow;

		private bool _leaving;

		private const float LeaveAhead = 0.1f;

		private Vec3 _heldDir = new Vec3(0f, 1f, 0f);

		private MeshModel _arrowModel;

		private readonly BowState _ownBow = new BowState();

		private int _ownAttacks = -1;

		private Hand _bowLeft;

		private bool _bare;

		private ArmLook _armLook;

		private string _armor;

		private MeshModel _gunModel;

		private float _time;

		private const float DigIn = 0.13f;

		private const float DigLever = 0.27f;

		private const float DigToss = 0.42f;

		private const float DigBack = 0.68f;

		public const float EatSeconds = 1.15f;

		private static readonly Vec3 Lips = new Vec3(0.012f, 0.17f, -0.075f);

		private bool _heldPalm;

		public readonly FlameSword Flame = new FlameSword();

		private static readonly uint HandGlow = TerrainCache.Rgb(255, 150, 64) & 0xFFFFFF;

		private int _gripL = -1;

		private int _gripR = -1;

		private float _regripL = 1f;

		private float _regripR = 1f;

		private Vec3 _fromL;

		private Vec3 _fromR;

		private Vec3 _atL;

		private Vec3 _atR;

		private float _lastTurn;

		private float _turnRate;

		private const float Regrip = 0.16f;

		public bool ShotgunShown;

		public Vec3 ShotgunMuzzle;

		public Vec3 ShotgunMuzzleDir;

		public Vec3 ShotgunPort;

		public Vec3 ShotgunPortDir;

		public Vec3 ShotgunUp;

		public Vec3 ShotgunBack;

		public static Vec3 SgGrip = new Vec3(0.156f, 0.232f, -0.186f);

		public static Vec3 SgDir = new Vec3(-0.224f, 0.972f, 0.061f);

		public static float SgCant = -0.05f;

		public static Vec3 SgRightArm = new Vec3(-0.34f, 0.72f, 0.52f);

		public static Vec3 SgRightThumb = new Vec3(-0.12f, 0.62f, 0.78f);

		public static Vec3 SgLeftArm = new Vec3(0.44f, 0.5f, 0.74f);

		public static Vec3 SgLeftThumb = new Vec3(0.05f, 1f, 0.1f);

		public static Vec3 SgPocket = new Vec3(-0.1f, 0.3f, -0.5f);

		private ShotgunState _sg;

		private MeshModel _sgModel;

		private MeshModel _shellModel;

		private MeshModel _sgBound;

		private int _sgPumpPart = -1;

		private int _sgBoltPart = -1;

		private int _sgTrigger = -1;

		private Vec3 _sgMuzzle = new Vec3(0f, 0.502f, 0.0215f);

		private Vec3 _sgPort = new Vec3(0.0148f, 0.0585f, 0.015f);

		private Vec3 _sgPortDir = new Vec3(0.85f, -0.13f, 0.47f);

		private Vec3 _sgPumpGrip = new Vec3(0f, 0.216f, 0.0005f);

		private Vec3 _sgLoad = new Vec3(0f, 0.071f, -0.0185f);

		private float _sgTravel = 0.046f;

		private Vec3 _gx = new Vec3(1f, 0f, 0f);

		private Vec3 _gy = new Vec3(0f, 1f, 0f);

		private Vec3 _gz = new Vec3(0f, 0f, 1f);

		private Vec3 _gunOrigin;

		private float _sgCarry;

		private float _sgPump;

		private int _flashShot = -1;

		private float _flashSince = 99f;

		private bool _flashFirstShown;

		private bool _shellShown;

		private Vec3 _shellAt;

		private readonly MeshInstance _shellInHand = new MeshInstance
		{
			NoShadow = true
		};

		private readonly Random _sgRng = new Random(11);

		private int _flashVariant;

		private float _flashRoll;

		private int _kickShot = -1;

		private float _kickAt;

		private Vec3 _preKickShift;

		private Vec3 _preX = new Vec3(1f, 0f, 0f);

		private Vec3 _preY = new Vec3(0f, 1f, 0f);

		private Vec3 _preZ = new Vec3(0f, 0f, 1f);

		public readonly TorchFire Torch = new TorchFire();

		public readonly TorchFire TorchLeft = new TorchFire();

		private readonly MeshInstance _torchL = new MeshInstance
		{
			NoShadow = true
		};

		private float _torchUp;

		private float _torchReach;

		private bool _torchLeftOn;

		private static readonly uint TorchArmGlow = TerrainCache.Rgb(255, 160, 72) & 0xFFFFFF;

		private bool _leftOn;

		private bool _trueSize;

		private Hand _trackFreeLeft;

		private bool _trackFreeLeftSet;

		private static readonly Vec3 StringRestD = N(-0.62f, 1f, 0.08f);

		private static readonly Vec3 StringDrawD = N(-1f, 0.3f, 0.06f);

		private static readonly Vec3 StringT = N(-0.05f, 0.15f, 1f);

		private static Hand OffHand => new Hand
		{
			W = new Vec3(-0.19f, 0.39f, -0.225f),
			D = N(0.42f, 1f, 0.16f),
			T = N(1f, 0.08f, 0.42f),
			Show = true
		};

		public Vec3 ArrowTipFromEye { get; private set; }

		public float ArrowTipTime { get; private set; } = -100f;

		public float ArrowTipRaise { get; private set; }

		private static Hand DigReady => new Hand
		{
			W = new Vec3(0.14f, 0.34f, -0.2f),
			D = N(-0.35f, 1f, 0.05f),
			T = N(-0.26f, 1f, -0.16f),
			Show = true
		};

		private static Hand DigReadyLeft => new Hand
		{
			W = new Vec3(0.09f, 0.53f, -0.23f),
			D = N(0.5f, 1f, 0.2f),
			T = N(-0.26f, 1f, -0.16f),
			Show = true
		};

		public bool FlameShown
		{
			get
			{
				if (Flame.Shown)
				{
					return Flame.PosedAt == _time;
				}
				return false;
			}
		}

		public Vec3 ShotgunMuzzleFromEye { get; private set; }

		public float ShotgunMuzzleTime { get; private set; } = -100f;

		public float ShotgunScale => _s;

		public bool TorchShown
		{
			get
			{
				if (Torch.Shown)
				{
					return Torch.PosedAt == _time;
				}
				return false;
			}
		}

		public bool TorchLeftShown
		{
			get
			{
				if (TorchLeft.Shown)
				{
					return TorchLeft.PosedAt == _time;
				}
				return false;
			}
		}

		public bool TorchInMain { get; private set; }

		private static Hand TorchSide => new Hand
		{
			W = new Vec3(-0.3f, 0.48f, -0.46f),
			D = N(0.22f, 1f, 0.55f),
			T = N(0.12f, 0.08f, 1f),
			Show = true
		};

		private static Hand TorchOutL => new Hand
		{
			W = new Vec3(-0.15f, 0.6f, -0.27f),
			D = N(0.3f, 1f, 0.3f),
			T = N(0.05f, 0.75f, 1f),
			Show = true
		};

		private static Hand TorchReadyR => new Hand
		{
			W = new Vec3(0.19f, 0.42f, -0.29f),
			D = N(-0.3f, 1f, 0.38f),
			T = N(-0.14f, 0.18f, 1f),
			Show = true
		};

		private static Hand TorchOutR => new Hand
		{
			W = new Vec3(0.16f, 0.6f, -0.27f),
			D = N(-0.3f, 1f, 0.3f),
			T = N(-0.05f, 0.75f, 1f),
			Show = true
		};

		public bool TrackAimValid { get; private set; }

		public Vec3 TrackAimFrom { get; private set; }

		public Vec3 TrackAimDir { get; private set; }

		public Vec3 TrackFreeFrom { get; private set; }

		public Vec3 TrackFreeDir { get; private set; }

		public float TrackPull { get; private set; } = -1f;

		public bool TrackNockNear { get; private set; }

		public HandStrike StrikeR { get; private set; }

		public HandStrike StrikeL { get; private set; }

		public bool TrackLeftOnWeapon => _leftOn;

		public Vec3 RightFistWorld { get; private set; }

		public Vec3 LeftFistWorld { get; private set; }

		public Vec3 RightForearmWorld { get; private set; }

		public Vec3 TrackNockWorld { get; private set; }

		public Vec3 TrackLeftHoldWorld { get; private set; }

		private void BandagePose(List<MeshInstance> overlay, float p, bool other, bool wrapped, bool over, float gaitPhase, float stride)
		{
			float num = Ease(p / 0.2f) * (1f - Ease((p - 0.84f) / 0.16000003f));
			float num2 = Fm.Clamp01((p - 0.2f) / 0.64f);
			float num3 = 1f - num;
			float num4 = Fm.Sin(gaitPhase);
			Vec3 vec = new Vec3(0.004f * Fm.Sin(gaitPhase - 0.5f), 0f, -0.008f * num4 * num4) * stride + new Vec3(0f - _lagX, 0f, _lagZ) * 0.7f;
			Vec3 vec2 = new Vec3(0f, -0.1f * num3, -0.38f * num3 * num3);
			float num5 = 0.6f + num2 * 2f * 2f * MathF.PI;
			Vec3 shoulder = new Vec3(-0.17f, 0.02f, -0.36f);
			if (!other)
			{
				Hand h = new Hand
				{
					W = new Vec3(-0.025f, 0.42f, -0.095f) + vec + vec2,
					D = N(-0.85f, 0.45f, 0.14f),
					T = N(-0.47f, -0.88f, 0f),
					Show = true
				};
				PlaceArm(overlay, _armR, h, left: false);
				float num6 = 0.08f + 0.120000005f * num2;
				Vec3 vec3 = _armROrigin + _armRY * num6;
				Vec3 grip = vec3 + _armRZ * (Fm.Cos(num5) * 0.056f) + _armRX * (Fm.Sin(num5) * 0.036f);
				Vec3 vec4 = PlaceRollHand(overlay, grip, shoulder, _armRY, num5, num);
				if (num2 > 0f && num2 < 1f)
				{
					PlaceTail(overlay, vec4, vec3 + (_armRZ * Fm.Cos(num5 - 1.1f) + _armRX * Fm.Sin(num5 - 1.1f)) * 0.03f, _armRY);
				}
				PlaceWrap(overlay, over ? 5 : ((int)Math.Ceiling(num2 * 5f)));
				return;
			}
			Vec3 vec5 = new Vec3(1f, 0f, 0f);
			Vec3 vec6 = new Vec3(0.05f - 0.08f * num2, 0.6f, -0.14f) + vec + vec2;
			Hand h2 = new Hand
			{
				W = vec6 + new Vec3(0.1f, -0.03f, -0.035f),
				D = N(-0.25f, 1f, 0.1f),
				T = N(-1f, 0.1f, 0.5f),
				Show = true
			};
			PlaceArm(overlay, _armR, h2, left: false);
			if (wrapped)
			{
				PlaceWrap(overlay, 5);
			}
			Vec3 grip2 = vec6 + new Vec3(0f, -1f, 0f) * (Fm.Cos(num5) * 0.056f * 0.85f) + new Vec3(0f, 0f, 1f) * (Fm.Sin(num5) * 0.036f);
			Vec3 vec7 = PlaceRollHand(overlay, grip2, shoulder, vec5, num5, num);
			if (num2 > 0f && num2 < 1f)
			{
				PlaceTail(overlay, vec7, vec6 + (new Vec3(0f, -1f, 0f) * Fm.Cos(num5 - 1.1f) + new Vec3(0f, 0f, 1f) * Fm.Sin(num5 - 1.1f)) * 0.022f, vec5);
			}
		}

		private Vec3 PlaceRollHand(List<MeshInstance> overlay, Vec3 grip, Vec3 shoulder, Vec3 axis, float th, float raise)
		{
			Vec3 vec = (grip - shoulder).Normalized();
			Hand h = new Hand
			{
				W = grip,
				D = vec,
				T = (axis * -1f + vec * 0.2f).Normalized(),
				Show = true
			};
			PlaceArm(overlay, _armL, h, left: true, holding: true);
			Vec3 vec2 = axis.Normalized();
			Vec3 vec3 = grip + vec * 0.008f - vec2 * 0.03f;
			if (raise <= 0.05f)
			{
				return vec3;
			}
			_roll.Model = _bandageRoll ?? _rollModel ?? (_rollModel = RollModel());
			_roll.Palette = null;
			Vec3 vec4 = Vec3.Cross((Math.Abs(vec2.Z) < 0.9f) ? new Vec3(0f, 0f, 1f) : new Vec3(0f, 1f, 0f), vec2).Normalized();
			Vec3 v = Vec3.Cross(vec2, vec4);
			vec4 = Vec3.Rotate(vec4, vec2, th * 1.6f);
			v = Vec3.Rotate(v, vec2, th * 1.6f);
			Place(_roll, vec3, vec2 * 1.25f, vec4 * 1.25f, v * 1.25f);
			_roll.Emissive = false;
			overlay.Add(_roll);
			return vec3;
		}

		private void PlaceTail(List<MeshInstance> overlay, Vec3 from, Vec3 to, Vec3 across)
		{
			Vec3 vec = to - from;
			if (!(Vec3.Dot(vec, vec) < 1E-06f))
			{
				Vec3 vec2 = across.Normalized();
				Vec3 vec3 = Vec3.Cross(vec2, vec).Normalized();
				_tail.Model = _tailModel ?? (_tailModel = TailModel());
				_tail.Palette = null;
				Place(_tail, from, vec2 * 0.017f, vec, vec3 * 0.0015f);
				_tail.Emissive = false;
				overlay.Add(_tail);
			}
		}

		private void PlaceWrap(List<MeshInstance> overlay, int turns)
		{
			if (turns <= 0 || _armRModel == null)
			{
				return;
			}
			MeshModel meshModel = WrapMeshes.ForArm(_armRModel);
			if (meshModel != null)
			{
				_wrap.Model = meshModel;
				_wrap.Palette = null;
				_wrap.PartRotation = null;
				ulong num = 0uL;
				for (int i = Math.Min(turns, meshModel.Parts.Length); i < meshModel.Parts.Length && i < 64; i++)
				{
					num |= (ulong)(1L << i);
				}
				_wrap.Hidden = num;
				Place(_wrap, _armROrigin, _armRX, _armRY, _armRZ);
				_wrap.Emissive = false;
				overlay.Add(_wrap);
			}
		}

		private static MeshModel RollModel()
		{
			MeshBuilder meshBuilder = new MeshBuilder("held_bandage_roll");
			int slot = meshBuilder.Slot("linen", 7, TerrainCache.Rgb(240, 233, 214), 0);
			int slot2 = meshBuilder.Slot("shade", 7, TerrainCache.Rgb(206, 194, 168), 0);
			int slot3 = meshBuilder.Slot("core", 0, TerrainCache.Rgb(122, 100, 76), 0);
			meshBuilder.Part("roll", -1, new Vec3(0f, 0f, 0f));
			meshBuilder.Tube(slot, new Vec3(-0.016f, 0f, 0f), new Vec3(0.016f, 0f, 0f), 0.017f, 0.017f, 12);
			meshBuilder.Tube(slot2, new Vec3(-0.0172f, 0f, 0f), new Vec3(0.0172f, 0f, 0f), 0.011f, 0.011f, 10);
			meshBuilder.Tube(slot3, new Vec3(-0.0178f, 0f, 0f), new Vec3(0.0178f, 0f, 0f), 0.004f, 0.004f, 6);
			return meshBuilder.Build();
		}

		private static MeshModel TailModel()
		{
			MeshBuilder meshBuilder = new MeshBuilder("held_bandage_tail");
			int slot = meshBuilder.Slot("linen", 7, TerrainCache.Rgb(236, 228, 208), 0);
			meshBuilder.Part("tail", -1, new Vec3(0f, 0f, 0f));
			meshBuilder.Box(slot, new Vec3(0f, 0.5f, 0f), 0.5f, 0.5f, 0.5f);
			return meshBuilder.Build();
		}

		private void BinocularsPose(List<MeshInstance> overlay, float lift, float gaitPhase, float stride)
		{
			float num = 1f - (float)Math.Pow(1f - lift, 2.5);
			float num2 = (float)Math.Pow(lift * lift * (3f - 2f * lift), 1.5);
			float num3 = 1f - num;
			Vec3 vec = new Vec3(BinocularsLow.X + (BinocularsAtEyes.X - BinocularsLow.X) * num, BinocularsLow.Y + (BinocularsAtEyes.Y - BinocularsLow.Y) * num2, BinocularsLow.Z + (BinocularsAtEyes.Z - BinocularsLow.Z) * num);
			float num4 = Fm.Sin(gaitPhase);
			vec += new Vec3(0.004f * Fm.Sin(gaitPhase - 0.5f), 0f, -0.01f * num4 * num4) * (stride * num3);
			vec += new Vec3(0f - _lagX, 0f, _lagZ) * (0.4f + 0.6f * num3);
			float radians = -0.55f * num3;
			float radians2 = 0.22f * num3;
			Vec3 vec2 = new Vec3(1f, 0f, 0f);
			Vec3 vec3 = Vec3.Rotate(new Vec3(0f, 1f, 0f), vec2, radians);
			Vec3 v = Vec3.Rotate(new Vec3(0f, 0f, 1f), vec2, radians);
			Vec3 vec4 = Vec3.Rotate(vec2, vec3, radians2);
			v = Vec3.Rotate(v, vec3, radians2);
			_binoculars.Model = _binocularsModel ?? (_binocularsModel = BinocularsModel());
			_binoculars.Palette = null;
			_binoculars.PartRotation = null;
			Place(_binoculars, vec, vec4, vec3, v);
			_binoculars.Emissive = false;
			overlay.Add(_binoculars);
			for (int i = -1; i <= 1; i += 2)
			{
				Vec3 vec5 = vec + vec4 * ((float)i * 0.056f) + vec3 * 0.07f - v * 0.006f;
				Vec3 vec6 = new Vec3((float)i * 0.21f, -0.06f, -0.34f);
				Vec3 vec7 = vec5 - vec6;
				Hand h = new Hand
				{
					W = vec5,
					D = vec7.Normalized(),
					T = (vec3 + v * 0.35f).Normalized(),
					Show = true
				};
				PlaceArm(overlay, (i < 0) ? _armL : _armR, h, i < 0, holding: true);
			}
		}

		private static MeshModel BinocularsModel()
		{
			MeshBuilder meshBuilder = new MeshBuilder("held_binoculars");
			int slot = meshBuilder.Slot("leather", 7, TerrainCache.Rgb(74, 54, 40), 0);
			int slot2 = meshBuilder.Slot("brass", 10, TerrainCache.Rgb(184, 146, 72), 0);
			int slot3 = meshBuilder.Slot("dark", 10, TerrainCache.Rgb(46, 42, 40), 0);
			int slot4 = meshBuilder.Slot("rubber", 0, TerrainCache.Rgb(30, 28, 27), 0);
			int slot5 = meshBuilder.Slot("glass", 12, TerrainCache.Rgb(112, 150, 178), 0);
			meshBuilder.Part("binoculars", -1, new Vec3(0f, 0f, 0f));
			for (int i = -1; i <= 1; i += 2)
			{
				float x = (float)i * 0.036f;
				meshBuilder.Tube(slot4, new Vec3(x, 0f, 0f), new Vec3(x, 0.016f, 0f), 0.0125f, 0.0145f, 10);
				meshBuilder.Tube(slot2, new Vec3(x, 0.015f, 0f), new Vec3(x, 0.022f, 0f), 0.0165f, 0.0165f, 10);
				meshBuilder.Tube(slot, new Vec3(x, 0.021f, 0f), new Vec3(x, 0.094f, 0f), 0.0175f, 0.0195f, 10);
				meshBuilder.Tube(slot2, new Vec3(x, 0.092f, 0f), new Vec3(x, 0.106f, 0f), 0.0215f, 0.0225f, 10);
				meshBuilder.Tube(slot5, new Vec3(x, 0.104f, 0f), new Vec3(x, 0.1075f, 0f), 0.017f, 0.017f, 10);
			}
			meshBuilder.Box(slot3, new Vec3(0f, 0.055f, 0f), 0.022f, 0.03f, 0.0075f);
			meshBuilder.Tube(slot2, new Vec3(-0.009f, 0.03f, 0.011f), new Vec3(0.009f, 0.03f, 0.011f), 0.0075f, 0.0075f, 8);
			return meshBuilder.Build();
		}

		private void BindBlaster(MeshModel m)
		{
			if (m == _blasterModel)
			{
				return;
			}
			_blasterModel = m;
			_blasterPalette = new uint[m.SlotNames.Length];
			_sBarrel = m.SlotIndex("barrel");
			_sRing = m.SlotIndex("barrel_ring");
			_sGlow = m.SlotIndex("glow");
			_sLens = m.SlotIndex("lens");
			for (int i = 0; i < 4; i++)
			{
				_sGauge[i] = m.SlotIndex("gauge" + (i + 1));
			}
			_pKnob = m.PartIndex("knob");
			_pTrigger = m.PartIndex("trigger");
			_knobAt = InfoVec(m, "knob", _knobAt);
			_muzzleAt = InfoVec(m, "muzzle", _muzzleAt);
			_ventsAt.Clear();
			_ventDirs.Clear();
			string text = m.Info("vents");
			string text2 = m.Info("vent_dirs");
			if (text != null)
			{
				string[] array = text.Split(';');
				foreach (string s in array)
				{
					_ventsAt.Add(ParseVec(s, new Vec3(0f, 0.07f, 0.08f)));
				}
			}
			if (text2 != null)
			{
				string[] array = text2.Split(';');
				foreach (string s2 in array)
				{
					_ventDirs.Add(ParseVec(s2, new Vec3(0f, 0f, 1f)).Normalized());
				}
			}
			while (_ventDirs.Count < _ventsAt.Count)
			{
				_ventDirs.Add(new Vec3(0f, 0f, 1f));
			}
		}

		private static Vec3 InfoVec(MeshModel m, string key, Vec3 fallback)
		{
			return ParseVec(m.Info(key), fallback);
		}

		private static Vec3 ParseVec(string s, Vec3 fallback)
		{
			if (string.IsNullOrEmpty(s))
			{
				return fallback;
			}
			string[] array = s.Split(',');
			if (array.Length != 3)
			{
				return fallback;
			}
			CultureInfo invariantCulture = CultureInfo.InvariantCulture;
			if (float.TryParse(array[0], NumberStyles.Float, invariantCulture, out var result) && float.TryParse(array[1], NumberStyles.Float, invariantCulture, out var result2) && float.TryParse(array[2], NumberStyles.Float, invariantCulture, out var result3))
			{
				return new Vec3(result, result2, result3);
			}
			return fallback;
		}

		private static void ItemAxes(in Hand right, out Vec3 x, out Vec3 y, out Vec3 z)
		{
			z = right.T.Normalized();
			y = right.D - z * Vec3.Dot(right.D, z);
			y = ((Vec3.Dot(y, y) > 0.0001f) ? y.Normalized() : new Vec3(0f, 1f, 0f));
			x = Vec3.Cross(y, z);
		}

		private static float KnobAngle(BlasterState b)
		{
			if (b == null || !b.Venting)
			{
				return 0f;
			}
			float ventProgress = b.VentProgress;
			float num = Ease((ventProgress - 0.3f) / 0.25f);
			float num2 = Ease((ventProgress - 0.62f) / 0.26f);
			return (num + num2) * 1.5708f;
		}

		private void BlasterVentPose(BlasterState b, float time, ref Hand right, ref Hand left)
		{
			float ventProgress = b.VentProgress;
			float num = Ease(ventProgress / 0.22f) * (1f - Ease((ventProgress - 0.9f) / 0.1f));
			right.W += new Vec3(-0.04f, 0.03f, 0.06f) * num;
			Vec3 v = right.D.Normalized();
			right.D = Vec3.Rotate(Vec3.Rotate(v, new Vec3(0f, 0f, 1f), 1f * num), new Vec3(1f, 0f, 0f), 0.15f * num).Normalized();
			right.T = Vec3.Rotate(right.T, new Vec3(0f, 0f, 1f), 1f * num).Normalized();
			right.T = Vec3.Rotate(right.T, right.D, 0.25f * num).Normalized();
			ItemAxes(in right, out var x, out var y, out var z);
			Vec3 vec = right.W + x * _knobAt.X + y * _knobAt.Y + z * _knobAt.Z;
			float radians = KnobAngle(b);
			Vec3 t = Vec3.Rotate(z, x, radians);
			Hand hand = new Hand
			{
				W = vec - x * 0.022f - z * 0.006f,
				D = new Vec3(0.42f, 0.22f, 0.88f).Normalized(),
				T = t,
				Show = true
			};
			if (ventProgress > 0.3f && ventProgress < 0.9f)
			{
				hand.W += new Vec3(0.002f * Fm.Sin(time * 47f), 0f, 0.002f * Fm.Sin(time * 39f));
			}
			float k = Ease((ventProgress - 0.05f) / 0.22f) * (1f - Ease((ventProgress - 0.9f) / 0.1f));
			left.W = Mix(left.W, hand.W, k);
			left.D = MixDir(left.D, hand.D, k);
			left.T = MixDir(left.T, hand.T, k);
			left.Show = true;
		}

		private static uint BarrelColor(float h, uint cold)
		{
			if (h <= 0.06f)
			{
				return cold;
			}
			if (h < 0.3f)
			{
				return TerrainCache.Lerp(TerrainCache.Scale(cold, 0.8f), TerrainCache.Rgb(130, 40, 28), (h - 0.06f) / 0.24f);
			}
			if (h < 0.55f)
			{
				return TerrainCache.Lerp(TerrainCache.Rgb(130, 40, 28), TerrainCache.Rgb(220, 60, 24), (h - 0.3f) / 0.25f);
			}
			if (h < 0.8f)
			{
				return TerrainCache.Lerp(TerrainCache.Rgb(220, 60, 24), TerrainCache.Rgb(255, 138, 40), (h - 0.55f) / 0.25f);
			}
			if (h < 1.05f)
			{
				return TerrainCache.Lerp(TerrainCache.Rgb(255, 138, 40), TerrainCache.Rgb(255, 214, 120), (h - 0.8f) / 0.25f);
			}
			return TerrainCache.Lerp(TerrainCache.Rgb(255, 214, 120), TerrainCache.Rgb(255, 246, 214), Math.Min(1f, (h - 1.05f) / 0.15f));
		}

		private void PlaceBlaster(List<MeshInstance> overlay, Hand right, MeshModel m, BlasterState b, float sinceAttack, float time)
		{
			BindBlaster(m);
			uint[] blasterPalette = _blasterPalette;
			float num = b?.BarrelHeat ?? 0f;
			float num2 = b?.Heat ?? 0f;
			bool flag = num > 0.06f;
			if (_sBarrel >= 0)
			{
				blasterPalette[_sBarrel] = BarrelColor(num, m.SlotColor[_sBarrel]);
				m.SlotFlags[_sBarrel] = (flag ? ((byte)1) : ((byte)0));
			}
			if (_sRing >= 0)
			{
				blasterPalette[_sRing] = BarrelColor(num * 0.82f, m.SlotColor[_sRing]);
				m.SlotFlags[_sRing] = ((num * 0.82f > 0.06f) ? ((byte)1) : ((byte)0));
			}
			bool flag2 = b != null && b.Overheated && ((int)(time * 5f) & 1) == 0;
			for (int i = 0; i < 4; i++)
			{
				int num3 = _sGauge[i];
				if (num3 < 0)
				{
					continue;
				}
				float num4 = num2 * 4f - (float)i;
				uint num5;
				if (b != null && b.Overheated)
				{
					num5 = (flag2 ? TerrainCache.Rgb(255, 40, 30) : TerrainCache.Rgb(90, 14, 12));
				}
				else if (num4 <= 0f)
				{
					num5 = GaugeOff;
				}
				else
				{
					uint num6;
					switch (i)
					{
					default:
						num6 = TerrainCache.Rgb(255, 80, 40);
						break;
					case 2:
						num6 = TerrainCache.Rgb(255, 200, 60);
						break;
					case 1:
						num6 = TerrainCache.Rgb(150, 240, 200);
						break;
					case 0:
						num6 = GaugeCool;
						break;
					}
					uint b2 = num6;
					num5 = TerrainCache.Lerp(GaugeOff, b2, Math.Min(1f, num4));
				}
				blasterPalette[num3] = num5;
			}
			if (_sGlow >= 0)
			{
				float s = ((b != null && b.Overheated) ? (0.35f + 0.25f * Fm.Sin(time * 23f)) : 1f);
				blasterPalette[_sGlow] = TerrainCache.Scale(m.SlotColor[_sGlow], s);
			}
			if (_sLens >= 0)
			{
				float t = Fm.Max(0f, 1f - sinceAttack / 0.12f);
				blasterPalette[_sLens] = TerrainCache.Lerp(m.SlotColor[_sLens], TerrainCache.Rgb(255, 255, 255), t);
			}
			_item.Model = m;
			_item.Palette = blasterPalette;
			int num7 = m.Parts.Length;
			if (_item.PartRotation == null || _item.PartRotation.Length != num7)
			{
				_item.PartRotation = new Mat3[num7];
			}
			for (int j = 0; j < num7; j++)
			{
				_item.PartRotation[j] = Mat3.Identity;
			}
			if (_pKnob >= 0)
			{
				_item.PartRotation[_pKnob] = Mat3.RotX(KnobAngle(b));
			}
			if (_pTrigger >= 0)
			{
				_item.PartRotation[_pTrigger] = Mat3.RotX(-0.4f * Fm.Max(0f, 1f - sinceAttack / 0.15f));
			}
			ItemAxes(in right, out var x, out var y, out var z);
			Place(_item, right.W, x, y, z);
			_item.Emissive = false;
			overlay.Add(_item);
			BlasterShown = true;
			BlasterMuzzle = World(right.W + x * _muzzleAt.X + y * _muzzleAt.Y + z * _muzzleAt.Z);
			BlasterVents.Clear();
			BlasterVentDirs.Clear();
			for (int k = 0; k < _ventsAt.Count; k++)
			{
				Vec3 vec = _ventsAt[k];
				Vec3 vec2 = _ventDirs[k];
				BlasterVents.Add(World(right.W + x * vec.X + y * vec.Y + z * vec.Z));
				BlasterVentDirs.Add(WorldDir(x * vec2.X + y * vec2.Y + z * vec2.Z).Normalized());
			}
		}

		private void ChargePose(float charge, float time, ref Hand right, ref Hand left)
		{
			float num = Ease(charge);
			float num2 = ((_slashSide > 0) ? 1f : (-1f));
			right.W += new Vec3(0.035f * num2, -0.085f, 0.03f) * num;
			right.T = Vec3.Rotate(right.T, new Vec3(1f, 0f, 0f), 0.35f * num).Normalized();
			left.W += new Vec3(0.01f * num2, 0.04f, 0.01f) * num;
			float num3 = Fm.Clamp01((charge - 0.8f) / 0.2f);
			if (num3 > 0f)
			{
				float num4 = 0.0025f * num3;
				right.W += new Vec3(Fm.Sin(time * 47f) * num4, 0f, Fm.Sin(time * 61f + 1.3f) * num4);
			}
		}

		private void CompassPose(List<MeshInstance> overlay, in HandsInput input, CompassState c, float gaitPhase, float stride, float run, float dt)
		{
			float num = BackOut(c.Raise);
			float num2 = 1f - Fm.Clamp01(num);
			Vec3 vec = CompassFace.Normalized();
			Vec3 vec2 = new Vec3(0f, vec.Z, 0f - vec.Y).Normalized();
			Vec3 k = Vec3.Cross(vec2, vec);
			Vec3 v = vec;
			Vec3 v2 = vec2;
			float radians = 1.05f * num2;
			float num3 = -0.55f * num2;
			float num4 = c.PopKick * c.PopKick;
			float num5 = c.ShutKick * c.ShutKick;
			num3 += -0.13f * num4 * Fm.Cos((1f - c.PopKick) * 9f) + 0.09f * num5;
			v = Vec3.Rotate(v, vec2, radians);
			v2 = Vec3.Rotate(v2, vec2, radians);
			v = Vec3.Rotate(v, k, num3);
			v2 = Vec3.Rotate(v2, k, num3);
			Vec3 vec3 = Vec3.Cross(v2, v);
			Vec3 vec4 = CompassFrom + (CompassAt - CompassFrom) * num;
			vec4 -= vec * (0.004f * num4 + 0.003f * num5);
			float num6 = Fm.Sin(gaitPhase);
			vec4 += new Vec3(0.004f * Fm.Sin(gaitPhase - 0.5f), 0f, -0.009f * num6 * num6) * stride * (0.4f + 0.6f * num2);
			vec4 += new Vec3(0f - _lagX, 0f, _lagZ) * 0.8f;
			Hand h = CompassHand(vec4, v);
			if (input.Tracked && input.TrackR.Valid)
			{
				TrackMap trackMap = MapOf(in input.TrackR, CompassHand(CompassAt, vec).W, vec2);
				vec4 = trackMap.Point(vec4);
				v = trackMap.Dir(v);
				v2 = trackMap.Dir(v2);
				vec3 = trackMap.Dir(vec3);
				h = trackMap.Hand(h);
			}
			PlaceArm(overlay, _armR, h, left: false, holding: false, cradle: true);
			TorchBesideCompass(overlay, input);
			_compass.Model = _compassModel ?? (_compassModel = CompassModel());
			_compass.Palette = null;
			int num7 = _compass.Model.Parts.Length;
			if (_compass.PartRotation == null || _compass.PartRotation.Length != num7)
			{
				_compass.PartRotation = new Mat3[num7];
			}
			SwingNeedles(c, v2, input.Dt, gaitPhase, stride, run);
			_compass.PartRotation[0] = Mat3.Identity;
			_compass.PartRotation[1] = Mat3.RotX(0f - c.Lid);
			_compass.PartRotation[2] = Mat3.RotZ(c.Card);
			float pointerOut = c.PointerOut;
			_compass.PartRotation[3] = Mat3.RotZ(c.Pointer) * Math.Max(0.001f, pointerOut * (2f - pointerOut));
			Place(_compass, vec4, vec3, v2, v);
			_compass.Emissive = false;
			overlay.Add(_compass);
			c.At = World(vec4);
			c.Shown = true;
		}

		private void TorchBesideCompass(List<MeshInstance> overlay, HandsInput input)
		{
			_torchLeftOn = (input.TorchLeft || input.TorchMain) && input.TorchModel != null;
			_torchUp = Fm.MoveTowards(_torchUp, _torchLeftOn ? 1f : 0f, input.Dt / (_torchLeftOn ? 0.4f : 0.25f));
			SyncFire(TorchLeft, input.TorchLit, input.TorchSputter);
			if (!(_torchUp <= 0f) && !input.Tracked)
			{
				Hand torchSide = TorchSide;
				torchSide.W += new Vec3(-0.04f, -0.06f, -0.34f) * (1f - Ease(_torchUp));
				PlaceArm(overlay, _armL, torchSide, left: true, holding: true);
				PlaceTorchLeft(overlay, torchSide, input.TorchModel);
			}
		}

		private static Hand CompassHand(Vec3 at, Vec3 n)
		{
			Vec3 vec = new Vec3(-0.42f, 1f, 0.3f);
			vec = (vec - n * Vec3.Dot(vec, n)).Normalized();
			return new Hand
			{
				W = at + n * -0.0075f,
				D = vec,
				T = Vec3.Cross(n * -1f, vec),
				Show = true
			};
		}

		private static float BackOut(float x)
		{
			x = Fm.Clamp01(x) - 1f;
			return 1f + 2.25f * x * x * x + 1.25f * x * x;
		}

		private bool CaseHeading(Vec3 f, out float yaw)
		{
			Vec3 vec = WorldDir(f);
			yaw = 0f;
			if (vec.X * vec.X + vec.Y * vec.Y < 0.02f * Vec3.Dot(vec, vec))
			{
				return false;
			}
			yaw = (float)Math.Atan2(vec.Y, vec.X);
			return true;
		}

		private static float Wrap(float a)
		{
			while (a > MathF.PI)
			{
				a -= MathF.PI * 2f;
			}
			while (a < -MathF.PI)
			{
				a += MathF.PI * 2f;
			}
			return a;
		}

		private void SwingNeedles(CompassState c, Vec3 f, float dt, float gaitPhase, float stride, float run)
		{
			float yaw;
			bool num = CaseHeading(f, out yaw);
			float num2 = (num ? Wrap(MathF.PI / 2f - yaw) : (float.IsNaN(c.LastNorth) ? c.Card : c.LastNorth));
			bool flag = num && !float.IsNaN(c.Target);
			float num3 = (flag ? Wrap(c.Target - yaw) : c.Pointer);
			float num4 = (float.IsNaN(c.LastNorth) ? 0f : Wrap(num2 - c.LastNorth));
			c.LastNorth = num2;
			bool flag2 = c.Lid > 0.5f;
			c.PointerOut = Fm.MoveTowards(c.PointerOut, (flag2 && flag) ? 1f : 0f, dt / 0.3f);
			if (!flag2 || dt <= 0f)
			{
				c.CardSpeed = (c.PointerSpeed = 0f);
				return;
			}
			float num5 = Fm.Clamp(num4 / dt, -14f, 14f);
			c.Card = Wrap(c.Card + num4);
			c.Pointer = Wrap(c.Pointer + num4);
			float num6 = stride * (0.5f + 1.5f * Fm.Clamp01(run)) * (9f * Fm.Sin(2f * gaitPhase) + 4f * Fm.Sin(3f * gaitPhase + 1f));
			int num7 = Math.Max(1, (int)Math.Ceiling(dt / 0.004f));
			float num8 = dt / (float)num7;
			for (int i = 0; i < num7; i++)
			{
				float v = Wrap(c.Card - num2);
				c.CardSpeed += (-55f * Fm.Sin(v) - 5.5f * (c.CardSpeed + num5) + num6) * num8;
				c.Card = Wrap(c.Card + c.CardSpeed * num8);
				float v2 = Wrap(c.Pointer - num3);
				c.PointerSpeed += (-70f * Fm.Sin(v2) - 10f * (c.PointerSpeed + num5) + num6 * 0.5f) * num8;
				c.Pointer = Wrap(c.Pointer + c.PointerSpeed * num8);
			}
		}

		private static void Disc(MeshBuilder b, int slot, float z, float radius, int sides, bool up)
		{
			Vec3[] array = new Vec3[sides];
			for (int i = 0; i < sides; i++)
			{
				float v = (float)(up ? i : (-i)) * 2f * MathF.PI / (float)sides;
				array[i] = new Vec3(radius * Fm.Cos(v), radius * Fm.Sin(v), z);
			}
			b.Face(slot, array);
		}

		private static void Annulus(MeshBuilder b, int slot, float z, float r0, float r1, int sides, bool up)
		{
			for (int i = 0; i < sides; i++)
			{
				float v = (float)i * 2f * MathF.PI / (float)sides;
				float v2 = (float)(i + 1) * 2f * MathF.PI / (float)sides;
				Vec3 vec = new Vec3(r0 * Fm.Cos(v), r0 * Fm.Sin(v), z);
				Vec3 vec2 = new Vec3(r0 * Fm.Cos(v2), r0 * Fm.Sin(v2), z);
				Vec3 vec3 = new Vec3(r1 * Fm.Cos(v), r1 * Fm.Sin(v), z);
				Vec3 vec4 = new Vec3(r1 * Fm.Cos(v2), r1 * Fm.Sin(v2), z);
				if (up)
				{
					b.Face(slot, vec, vec3, vec4, vec2);
				}
				else
				{
					b.Face(slot, vec, vec2, vec4, vec3);
				}
			}
		}

		private static void Wall(MeshBuilder b, int slot, float[] z, float[] radius, int sides, bool outward, bool smooth = true)
		{
			int num = z.Length;
			Vec3[] array = new Vec3[num];
			float[] array2 = new float[num];
			for (int i = 0; i < num; i++)
			{
				int num2 = (outward ? i : (num - 1 - i));
				array[i] = new Vec3(0f, 0f, z[num2]);
				array2[i] = radius[num2];
			}
			b.Loft(slot, array, array2, array2, new Vec3(1f, 0f, 0f), new Vec3(0f, 1f, 0f), sides, smooth, 0f, caps: false);
		}

		private static void RosePoint(MeshBuilder b, int lit, int shaded, float a, float length, float half, float z)
		{
			Vec3 vec = new Vec3(0f - Fm.Sin(a), Fm.Cos(a), 0f);
			Vec3 vec2 = new Vec3(0f - vec.Y, vec.X, 0f);
			Vec3 vec3 = new Vec3(0f, 0f, z);
			Vec3 vec4 = vec3 + vec * length;
			b.Face(lit, vec3, vec4, vec3 + vec2 * half);
			b.Face(shaded, vec3, vec3 - vec2 * half, vec4);
		}

		private static MeshModel CompassModel()
		{
			MeshBuilder meshBuilder = new MeshBuilder("held_compass");
			int slot = meshBuilder.Slot("brass", 10, TerrainCache.Rgb(204, 162, 78), 16);
			int slot2 = meshBuilder.Slot("brass_dark", 10, TerrainCache.Rgb(122, 90, 40), 0);
			int slot3 = meshBuilder.Slot("lid_inside", 10, TerrainCache.Rgb(226, 188, 104), 16);
			int slot4 = meshBuilder.Slot("card", 0, TerrainCache.Rgb(238, 222, 176), 0);
			int lit = meshBuilder.Slot("rose_lit", 10, TerrainCache.Rgb(214, 166, 70), 0);
			int shaded = meshBuilder.Slot("rose_shade", 10, TerrainCache.Rgb(110, 74, 30), 0);
			int slot5 = meshBuilder.Slot("engraved", 0, TerrainCache.Rgb(48, 32, 16), 0);
			int slot6 = meshBuilder.Slot("north", 0, TerrainCache.Rgb(150, 34, 26), 0);
			int slot7 = meshBuilder.Slot("needle", 10, TerrainCache.Rgb(40, 50, 82), 0);
			int slot8 = meshBuilder.Slot("star", 0, TerrainCache.Rgb(255, 206, 72), 1);
			meshBuilder.Part("case", -1, new Vec3(0f, 0f, 0f));
			Wall(meshBuilder, slot, new float[6] { -0.0075f, -0.0067f, -0.0045f, 0.0015f, 0.0035f, 0.0042f }, new float[6] { 0.0262f, 0.0284f, 0.0298f, 0.03f, 0.0296f, 0.0287f }, 32, outward: true);
			Disc(meshBuilder, slot2, -0.0075f, 0.0262f, 32, up: false);
			Wall(meshBuilder, slot2, new float[2] { -0.0029f, -0.0007f }, new float[2] { 0.0304f, 0.0304f }, 24, outward: true, smooth: false);
			Annulus(meshBuilder, slot, 0.0042f, 0.0262f, 0.0287f, 32, up: true);
			Wall(meshBuilder, slot2, new float[2] { -0.0012f, 0.0042f }, new float[2] { 0.0262f, 0.0262f }, 32, outward: false);
			Disc(meshBuilder, slot2, -0.0012f, 0.0262f, 32, up: true);
			meshBuilder.Box(slot5, new Vec3(0f, 0.0276f, 0.0042499998f), 0.0007f, 0.0011f, 5E-05f);
			for (int i = -1; i <= 1; i += 2)
			{
				meshBuilder.Tube(slot, new Vec3(0.003f * (float)i, 0.0312f, 0.0042f), new Vec3(0.008f * (float)i, 0.0312f, 0.0042f), 0.0016f, 0.0016f, 8);
				meshBuilder.Box(slot, new Vec3(0.0055f * (float)i, 0.0293f, 0.0033999998f), 0.0025f, 0.0012f, 0.0007f);
			}
			meshBuilder.Tube(slot, new Vec3(0f, -0.0294f, 0f), new Vec3(0f, -0.0328f, 0f), 0.0015f, 0.0015f, 8);
			meshBuilder.Tube(slot2, new Vec3(0f, -0.0326f, 0f), new Vec3(0f, -0.0356f, 0f), 0.0027f, 0.0027f, 10, smooth: false);
			meshBuilder.Tube(slot, new Vec3(0f, -0.0355f, 0f), new Vec3(0f, -0.0362f, 0f), 0.0022f, 0.0012f, 10);
			meshBuilder.Tube(slot, new Vec3(0f, 0f, -0.0004f), new Vec3(0f, 0f, 0.0024f), 0.0018f, 0.0018f, 10);
			meshBuilder.Tube(slot, new Vec3(0f, 0f, 0.0024f), new Vec3(0f, 0f, 0.0031f), 0.0019f, 0.0004f, 10);
			meshBuilder.Part("lid", 0, new Vec3(0f, 0.0312f, 0.0042f));
			Wall(meshBuilder, slot, new float[6] { 0.0044f, 0.006f, 0.007f, 0.0076f, 0.0079f, 0.008f }, new float[6] { 0.03f, 0.0302f, 0.0285f, 0.022f, 0.012f, 0f }, 32, outward: true);
			Disc(meshBuilder, slot3, 0.0044f, 0.03f, 32, up: false);
			Annulus(meshBuilder, slot5, 0.00435f, 0.0212f, 0.0224f, 32, up: false);
			for (int j = 0; j < 8; j++)
			{
				float v = (float)j * MathF.PI / 4f;
				float num = ((j % 2 == 0) ? 0.0062f : 0.0036f);
				Vec3 vec = new Vec3(0f - Fm.Sin(v), Fm.Cos(v), 0f);
				Vec3 vec2 = new Vec3(0f - vec.Y, vec.X, 0f);
				Vec3 vec3 = new Vec3(0f, 0f, 0.00435f);
				meshBuilder.Face(slot5, vec3, vec3 + vec2 * 0.0011f, vec3 + vec * num);
				meshBuilder.Face(slot5, vec3, vec3 + vec * num, vec3 - vec2 * 0.0011f);
			}
			meshBuilder.Tube(slot, new Vec3(-0.0028f, 0.0312f, 0.0042f), new Vec3(0.0028f, 0.0312f, 0.0042f), 0.0016f, 0.0016f, 8);
			meshBuilder.Box(slot, new Vec3(0f, 0.0298f, 0.0054f), 0.0024f, 0.0013f, 0.0008f);
			meshBuilder.Part("card", 0, new Vec3(0f, 0f, 0f));
			Wall(meshBuilder, slot4, new float[2] { -0.0008f, -0.0004f }, new float[2] { 0.024f, 0.024f }, 32, outward: true);
			Disc(meshBuilder, slot4, -0.0004f, 0.024f, 32, up: true);
			Annulus(meshBuilder, slot5, -0.00025f, 0.0197f, 0.0202f, 32, up: true);
			for (int k = 0; k < 32; k++)
			{
				float v2 = (float)k * MathF.PI / 16f;
				bool num2 = k % 8 == 0;
				bool flag = k % 4 == 0;
				float num3 = (num2 ? 0.0202f : (flag ? 0.0204f : 0.0209f));
				float num4 = 0.0236f;
				float num5 = (num2 ? 0.00055f : 0.0003f);
				Vec3 vec4 = new Vec3(0f - Fm.Sin(v2), Fm.Cos(v2), 0f);
				Vec3 vec5 = new Vec3(0f - vec4.Y, vec4.X, 0f) * num5;
				Vec3 vec6 = new Vec3(0f, 0f, -0.00025f);
				meshBuilder.Face(slot5, vec6 + vec4 * num3 - vec5, vec6 + vec4 * num4 - vec5, vec6 + vec4 * num4 + vec5, vec6 + vec4 * num3 + vec5);
			}
			for (int l = 0; l < 8; l++)
			{
				RosePoint(meshBuilder, lit, shaded, ((float)l + 0.5f) * MathF.PI / 4f, 0.0118f, 0.0021f, -0.0001f);
			}
			for (int m = 0; m < 4; m++)
			{
				RosePoint(meshBuilder, lit, shaded, ((float)m + 0.5f) * MathF.PI / 2f, 0.0158f, 0.0031f, 0.0001f);
			}
			for (int n = 1; n < 4; n++)
			{
				RosePoint(meshBuilder, lit, shaded, (float)n * MathF.PI / 2f, 0.0196f, 0.004f, 0.0003f);
			}
			RosePoint(meshBuilder, lit, shaded, 0f, 0.0168f, 0.004f, 0.0003f);
			meshBuilder.Face(slot6, new Vec3(0f, 0.0156f, 0.0004f), new Vec3(0.0023f, 0.0192f, 0.0004f), new Vec3(0f, 0.024f, 0.0004f), new Vec3(-0.0023f, 0.0192f, 0.0004f));
			meshBuilder.Face(slot6, new Vec3(0.0006f, 0.0174f, 0.0004f), new Vec3(0.0044f, 0.0194f, 0.0004f), new Vec3(0.0049f, 0.0226f, 0.0004f), new Vec3(0.0025f, 0.02f, 0.0004f));
			meshBuilder.Face(slot6, new Vec3(-0.0025f, 0.02f, 0.0004f), new Vec3(-0.0049f, 0.0226f, 0.0004f), new Vec3(-0.0044f, 0.0194f, 0.0004f), new Vec3(-0.0006f, 0.0174f, 0.0004f));
			meshBuilder.Face(slot5, new Vec3(-0.0034f, 0.018f, 0.00049999997f), new Vec3(0.0034f, 0.018f, 0.00049999997f), new Vec3(0.0034f, 0.0188f, 0.00049999997f), new Vec3(-0.0034f, 0.0188f, 0.00049999997f));
			meshBuilder.Part("pointer", 0, new Vec3(0f, 0f, 0f));
			meshBuilder.Extrude(slot7, new Vec3(0f, 0f, 0.0016f), new Vec3(1f, 0f, 0f), new Vec3(0f, 1f, 0f), 0.00035f, 0f, -0.0066f, 0.0019f, 0f, 0f, 0.0168f, -0.0019f, 0f);
			Vec3 vec7 = new Vec3(0f, 0.0188f, 0.002f);
			Vec3[] array = new Vec3[5];
			for (int num6 = 0; num6 < 5; num6++)
			{
				float v3 = ((float)num6 + 0.5f) * 2f * MathF.PI / 5f;
				array[num6] = vec7 + new Vec3(0f - Fm.Sin(v3), Fm.Cos(v3), 0f) * 0.0022f;
			}
			meshBuilder.Face(slot8, array);
			for (int num7 = 0; num7 < 5; num7++)
			{
				float v4 = (float)num7 * 2f * MathF.PI / 5f;
				Vec3 vec8 = vec7 + new Vec3(0f - Fm.Sin(v4), Fm.Cos(v4), 0f) * 0.0054f;
				meshBuilder.Face(slot8, array[num7], array[(num7 + 4) % 5], vec8);
			}
			return meshBuilder.Build();
		}

		private void ViewAxes(ViewState view)
		{
			Lens lens = Lens.Of(in view, (Aspect > 0f) ? Aspect : 0.5625f);
			_fl = new Vec3(Fm.Cos(view.Yaw), Fm.Sin(view.Yaw), 0f);
			_r = new Vec3(_fl.Y, 0f - _fl.X, 0f);
			_f = _fl * lens.Cq + new Vec3(0f, 0f, lens.Sq);
			_u = _fl * (0f - lens.Sq) + new Vec3(0f, 0f, lens.Cq);
			_pitch = lens.Shift;
			_slope = view.Pitch;
		}

		public void Lower()
		{
			_equip = 0f;
		}

		public void Build(List<MeshInstance> overlay, List<VoxelObject> voxels, ViewState view, in HandsInput input)
		{
			SpriteImage body = input.Body;
			SpriteImage icon = input.Icon;
			bool humanoid = input.Humanoid;
			bool swimming = input.Swimming;
			_gunModel = input.GunModel;
			_armLook = input.ArmLook;
			_armor = input.Armor;
			_blaster = input.Blaster;
			_time = input.Time;
			BlasterShown = false;
			_sgModel = input.ShotgunModel;
			_shellModel = input.ShellModel;
			ShotgunShown = false;
			_item.Flash = (_armR.Flash = (_armL.Flash = 0u));
			string text = input.ItemId;
			_heldModel = input.HeldModel;
			_windupScale = input.WindupScale;
			if (_heldModel != null)
			{
				text = "tw:" + _heldModel.Name;
			}
			Swing swing = input.Swing;
			float swimPhase = input.SwimPhase;
			float stride = input.Stride;
			float gaitPhase = input.GaitPhase;
			float run = input.Run;
			float bodyHeight = input.BodyHeight;
			float dt = input.Dt;
			overlay.Clear();
			if (body != null && (input.Owner != _paletteOwner || input.Look != _paletteLook || _bare == humanoid || input.Sleeve != _paletteSleeve))
			{
				_paletteOwner = input.Owner;
				_paletteLook = input.Look;
				_paletteSleeve = input.Sleeve;
				Dress(body, humanoid, input.Species, input.Skin, input.Sleeve);
			}
			_eye = new Vec3(view.X, view.Y, view.Z);
			ViewAxes(view);
			_fovK = Fm.Clamp((float)Math.Tan(view.Fov * 0.5f), 0.4f, 2.5f);
			_s = Fm.Clamp(bodyHeight, 0.55f, 1.3f);
			bool flag = input.Tracked && input.TrackR.Valid;
			TrackAimValid = false;
			TrackPull = -1f;
			TrackNockNear = false;
			_trackFreeLeftSet = false;
			_trueSize = input.Tracked;
			if (input.Tracked)
			{
				_fovK = 1f;
				_pitch = 0f;
			}
			if (text != _heldId)
			{
				_heldId = text;
				_look = ViewMeshes.LookOf((_heldModel != null) ? null : text, icon);
				if (_heldModel != null)
				{
					_look.Kind = input.HeldKind;
				}
				else if (string.IsNullOrEmpty(text))
				{
					_look.Kind = WeaponKind.None;
				}
				_equip = 0f;
			}
			_equip = Fm.MoveTowards(_equip, 1f, dt / 0.38f);
			if (_haveLast && dt > 0f)
			{
				float num = Fm.DeltaAngle(_lastYaw * (180f / MathF.PI), view.Yaw * (180f / MathF.PI)) * (MathF.PI / 180f);
				float num2 = Fm.Atan(view.Pitch) - Fm.Atan(_lastPitch);
				_lagX = Fm.Lerp(_lagX, Fm.Clamp(num / dt * 0.01f, -0.05f, 0.05f), 1f - Fm.Exp(-10f * dt));
				_lagZ = Fm.Lerp(_lagZ, Fm.Clamp((0f - num2) / dt * 0.008f, -0.04f, 0.04f), 1f - Fm.Exp(-10f * dt));
			}
			_lastYaw = view.Yaw;
			_lastPitch = view.Pitch;
			_haveLast = true;
			if (input.Attacks != _lastAttacks)
			{
				if (_lastAttacks >= 0)
				{
					_leftPunch = !_leftPunch;
					_slashSide = -_slashSide;
				}
				_lastAttacks = input.Attacks;
			}
			if (input.Hurt > _lastHurt + 0.2f)
			{
				_hurtPulse = 1f;
			}
			_lastHurt = input.Hurt;
			_hurtPulse = Fm.Max(0f, _hurtPulse - dt * 6f);
			if (input.Binoculars > 0f)
			{
				BinocularsPose(overlay, Math.Min(1f, input.Binoculars), gaitPhase, stride);
				_equip = 0f;
				return;
			}
			_bandageRoll = input.BandageRoll;
			if (input.Bandage > 0f)
			{
				BandagePose(overlay, Math.Min(1f, input.Bandage), input.BandageOther, input.Wrapped, input.BandageOver, gaitPhase, stride);
				_equip = 0f;
				return;
			}
			if (input.Compass != null)
			{
				input.Compass.Shown = false;
			}
			if (input.Compass != null && input.Compass.Raise > 0f && !swimming)
			{
				CompassPose(overlay, in input, input.Compass, gaitPhase, stride, run, dt);
				_equip = 0f;
				return;
			}
			if (input.Compass != null && input.Compass.Dip > 0f)
			{
				_equip = Math.Min(_equip, 1f - input.Compass.Dip);
			}
			if (swimming)
			{
				if (flag)
				{
					TrackedBare(overlay, in input);
				}
				else
				{
					Swim(overlay, swimPhase);
				}
				return;
			}
			float sinceAttack = input.SinceAttack;
			bool melee = input.Melee;
			WeaponKind kind = _look.Kind;
			bool flag2 = kind != WeaponKind.None || _heldModel != null;
			Hand right = Ready(kind, right: true);
			Hand left = Ready(kind, right: false);
			if (input.Dig && _heldModel != null)
			{
				right = DigReady;
				left = DigReadyLeft;
			}
			bool flag3 = input.HeldItem && _heldModel != null && kind == WeaponKind.None;
			_heldPalm = flag3 && input.HeldPalm;
			if (flag3)
			{
				right = ItemReady(_heldModel, _heldPalm);
				left = OffHand;
			}
			TorchBegin(in input, kind, dt, ref right);
			BowState bowState = ((kind == WeaponKind.Bow) ? (input.Bow ?? FallbackBow(in input)) : null);
			_arrowModel = input.ArrowModel;
			if (bowState != null && flag)
			{
				TrackedBow(in input, bowState, ref right, ref left);
			}
			else if (bowState != null)
			{
				BowPose(bowState, input.Time, dt, ref right, ref left);
				if (swing == Swing.Gather && sinceAttack < 0.6f)
				{
					_onString = false;
					_heldArrow = false;
					Reach(ref right, sinceAttack);
				}
			}
			else if (kind == WeaponKind.Shotgun)
			{
				ShotgunPose(in input, dt, ref right, ref left);
			}
			else if (!flag && input.Dig && _heldModel != null && swing == Swing.Dig)
			{
				if (sinceAttack < 1.2f)
				{
					DigStroke(sinceAttack, input.DigRate, input.DigFill, ref right);
				}
			}
			else if (!flag && sinceAttack < ((swing == Swing.Attack) ? 3f : 1.2f))
			{
				WeaponKind weaponKind = kind;
				switch (swing)
				{
				case Swing.Gather:
					weaponKind = WeaponKind.None;
					break;
				case Swing.Chop:
				case Swing.Mine:
					if (kind != WeaponKind.None && kind != WeaponKind.Bow && kind != WeaponKind.Gun)
					{
						int num3;
						switch (kind)
						{
						default:
							num3 = (int)kind;
							break;
						case WeaponKind.Sword:
						case WeaponKind.Stick:
						case WeaponKind.Dagger:
							num3 = 4;
							break;
						case WeaponKind.Spear:
							num3 = 2;
							break;
						}
						weaponKind = (WeaponKind)num3;
					}
					break;
				}
				if (swing == Swing.Gather)
				{
					Reach(ref right, sinceAttack);
				}
				else if (swing == Swing.Attack && melee && input.StoppedAt > 0f && sinceAttack > input.StoppedAt)
				{
					Rebound(weaponKind, sinceAttack, input.StoppedAt, input.StoppedWeight, input.StoppedRings, ref right, ref left);
				}
				else if (swing == Swing.Attack)
				{
					Blow(weaponKind, melee, sinceAttack, ref right, ref left);
					if (input.Charge > 0f && weaponKind == WeaponKind.Sword)
					{
						ChargePose(input.Charge, input.Time, ref right, ref left);
					}
				}
				else
				{
					Animate(weaponKind, melee, sinceAttack, ref right, ref left);
				}
			}
			if (input.Guard > 0f && !flag)
			{
				GuardPose(kind, Math.Min(1f, input.Guard), ref right, ref left);
			}
			if (!flag && input.ClashWeight > 0f && input.ClashAge < 0.6f && bowState == null)
			{
				Jolt(input.ClashAge, input.ClashWeight, input.ClashRings, kind, ref right, ref left);
			}
			if (kind == WeaponKind.Gun && _blaster != null && _blaster.Venting)
			{
				BlasterVentPose(_blaster, input.Time, ref right, ref left);
			}
			if (flag3 && !flag && input.EatAge >= 0f && input.EatAge < 1.15f)
			{
				EatPose(ref right, input.EatAge, input.HeldDrink, _heldPalm, input.HeldMouth);
			}
			TorchHold(in input, ref right, ref left);
			if (flag && bowState == null)
			{
				TrackedPose(in input, kind, ref right, ref left);
			}
			if (!flag)
			{
				float num4 = Fm.Sin(gaitPhase);
				float num5 = num4 * num4;
				Vec3 vec = new Vec3(0.006f * Fm.Sin(gaitPhase - 0.5f), 0f, -0.012f * num5) * stride * ((bowState != null) ? (1f - 0.65f * _raise) : 1f);
				right.W += vec;
				left.W += vec;
				if (run > 0.05f && stride > 0.05f)
				{
					float num6 = run * stride;
					if (flag2 && kind != WeaponKind.Bow && kind != WeaponKind.Shotgun)
					{
						right.W += new Vec3(0.03f, -0.05f, -0.07f) * num6;
						right.T = Vec3.Rotate(right.T, new Vec3(1f, 0f, 0f), -0.5f * num6).Normalized();
					}
					float num7 = Fm.Sin(gaitPhase) * 0.055f * num6;
					if (!flag2)
					{
						right.W += new Vec3(0f, num7, num7 * 0.5f);
					}
					if (kind != WeaponKind.Bow && kind != WeaponKind.Spear && kind != WeaponKind.Staff)
					{
						left.W += new Vec3(0f, 0f - num7, (0f - num7) * 0.5f);
					}
				}
				right.W += new Vec3(-0.012f, 0f, 0.042f);
				left.W += new Vec3(0.012f, 0f, 0.042f);
				Vec3 vec2 = new Vec3(0f - _lagX, 0f, _lagZ) * ((bowState != null) ? (1f - 0.5f * _raise) : 1f);
				float num8 = 1f - _equip;
				Vec3 vec3 = new Vec3(0f, -0.1f * num8, -0.38f * num8 * num8);
				Vec3 vec4 = new Vec3(0f, -0.03f, -0.05f) * _hurtPulse;
				right.W = right.W + vec2 + vec3 + vec4;
				left.W = left.W + vec2 * 0.8f + vec3 + vec4;
			}
			if (kind == WeaponKind.Spear && !flag)
			{
				Vec3 vec5 = right.T.Normalized();
				left.W = right.W + vec5 * Fm.Max(0.14f, Vec3.Dot(left.W - right.W, vec5));
				left.T = vec5;
			}
			if (bowState != null)
			{
				_bowLeft = left;
				if (_onString && !flag)
				{
					right.W = left.W + _nockRel;
				}
			}
			if (kind == WeaponKind.Shotgun)
			{
				ShotgunHands(ref right, ref left, input.Shotgun);
				if (flag)
				{
					TrackedLeftOnGun(ref left, input.Shotgun, input.TrackL.Valid);
				}
			}
			bool flag4 = kind == WeaponKind.Bow;
			Hand free = (flag4 ? right : left);
			bool showProp = false;
			float glow = 0f;
			if (input.Gesture != Gesture.None && flag)
			{
				Hand free2 = free;
				Hand right2 = right;
				Hand left2 = left;
				GesturePose(in input, dt, ref free2, ref right2, ref left2, flag4, out showProp, out glow);
			}
			else if (input.Gesture != Gesture.None)
			{
				GesturePose(in input, dt, ref free, ref right, ref left, flag4, out showProp, out glow);
			}
			else
			{
				_cocked = 0f;
			}
			if (input.Gesture == Gesture.Throw || input.Gesture == Gesture.Cast)
			{
				if (flag4)
				{
					right = free;
				}
				else
				{
					left = free;
				}
			}
			if (flag && ThroughEyes(in right))
			{
				right.Show = false;
			}
			else
			{
				right.Show = true;
			}
			if (flag && left.Show && ThroughEyes(in left))
			{
				left.Show = false;
			}
			if (right.Show)
			{
				PlaceArm(overlay, _armR, right, left: false, flag2 && (bowState == null || _heldArrow) && !_heldPalm, _heldPalm);
			}
			if (input.Wrapped)
			{
				PlaceWrap(overlay, 5);
			}
			if (left.Show)
			{
				PlaceArm(overlay, _armL, left, left: true, kind == WeaponKind.Spear || kind == WeaponKind.Bow || kind == WeaponKind.Gun || kind == WeaponKind.Shotgun || _torchUp > 0f);
			}
			if (left.Show && _torchUp > 0f)
			{
				PlaceTorchLeft(overlay, left, input.TorchModel);
			}
			if (showProp && input.GestureProp != null)
			{
				PlaceProp(overlay, flag4 ? right : left, input.GestureProp, input.Time);
			}
			if (glow > 0f && input.GestureGlow != 0)
			{
				AddGlow(voxels, flag4 ? right : left, input.GestureGlow, glow, input.Time);
			}
			if (flag2 && kind == WeaponKind.Shotgun)
			{
				PlaceShotgun(overlay, voxels, right, input.Shotgun);
			}
			else if (flag2)
			{
				PlaceItem(overlay, right, sinceAttack);
			}
			else if (icon != null && !string.IsNullOrEmpty(text))
			{
				voxels.Add(ItemPose.HoldItem(icon, World(right.W), Shear(WorldDir(right.T)), _r, 0.16f * _s, overlay: true));
			}
			if (flag)
			{
				TrackedAims(in input, in right, in left, kind, bowState != null, flag4, flag2);
				return;
			}
			HandStrike strikeR = (StrikeL = default(HandStrike));
			StrikeR = strikeR;
		}

		private static Vec3 N(float x, float y, float z)
		{
			return new Vec3(x, y, z).Normalized();
		}

		private static Hand Ready(WeaponKind kind, bool right)
		{
			float num = (right ? 1f : (-1f));
			switch (kind)
			{
			case WeaponKind.Sword:
			case WeaponKind.Stick:
			case WeaponKind.Dagger:
				if (!right)
				{
					return OffHand;
				}
				return new Hand
				{
					W = new Vec3(0.16f, 0.42f, -0.17f),
					D = N(-0.35f, 1f, 0.3f),
					T = N(-0.36f, 0.32f, 1f),
					Show = true
				};
			case WeaponKind.Axe:
			case WeaponKind.Hammer:
				if (!right)
				{
					return OffHand;
				}
				return new Hand
				{
					W = new Vec3(0.17f, 0.4f, -0.2f),
					D = N(-0.3f, 1f, 0.25f),
					T = N(-0.3f, 0.38f, 1f),
					Show = true
				};
			case WeaponKind.Spear:
				if (!right)
				{
					return new Hand
					{
						W = new Vec3(0.11f, 0.47f, -0.17f),
						D = N(0.5f, 1f, 0.25f),
						T = N(-0.14f, 1f, 0.14f),
						Show = true
					};
				}
				return new Hand
				{
					W = new Vec3(0.16f, 0.29f, -0.165f),
					D = N(-0.45f, 1f, 0.3f),
					T = N(-0.14f, 1f, 0.14f),
					Show = true
				};
			case WeaponKind.Staff:
				if (!right)
				{
					return OffHand;
				}
				return new Hand
				{
					W = new Vec3(0.15f, 0.42f, -0.2f),
					D = N(-0.3f, 1f, 0.25f),
					T = N(-0.15f, 0.3f, 1f),
					Show = true
				};
			case WeaponKind.Bow:
				if (!right)
				{
					return new Hand
					{
						W = new Vec3(-0.1f, 0.62f, -0.12f),
						D = N(0.25f, 1f, 0.12f),
						T = N(0.3f, 0.05f, 1f),
						Show = true
					};
				}
				return new Hand
				{
					W = new Vec3(0.04f, 0.38f, -0.1f),
					D = N(-0.9f, 1f, 0.15f),
					T = N(0f, 0.1f, 1f),
					Show = true
				};
			case WeaponKind.Gun:
				if (!right)
				{
					return new Hand
					{
						W = new Vec3(0.07f, 0.37f, -0.2f),
						D = N(0.55f, 1f, 0.15f),
						T = N(-0.3f, -0.1f, 1f),
						Show = true
					};
				}
				return new Hand
				{
					W = new Vec3(0.12f, 0.36f, -0.17f),
					D = N(-0.2f, 1f, 0.12f),
					T = N(-0.05f, -0.22f, 1f),
					Show = true
				};
			default:
				return new Hand
				{
					W = new Vec3(0.15f * num, 0.4f, -0.185f),
					D = N(-0.38f * num, 1f, 0.16f),
					T = N(-1f * num, 0.08f, 0.36f),
					Show = true
				};
			}
		}

		private static float Ease(float x)
		{
			x = Fm.Clamp01(x);
			return x * x * (3f - 2f * x);
		}

		private static float Out(float x)
		{
			x = Fm.Clamp01(x);
			return 1f - (1f - x) * (1f - x);
		}

		private static Vec3 Mix(Vec3 a, Vec3 b, float k)
		{
			return a + (b - a) * k;
		}

		private static Vec3 MixDir(Vec3 a, Vec3 b, float k)
		{
			return (a + (b - a) * k).Normalized();
		}

		private static void Beat(ref Hand h, float t, float windEnd, float strikeEnd, float total, Hand wind, Hand strike)
		{
			Hand hand = h;
			if (t < windEnd)
			{
				float k = Out(t / windEnd);
				h.W = Mix(hand.W, wind.W, k);
				h.D = MixDir(hand.D, wind.D, k);
				h.T = MixDir(hand.T, wind.T, k);
			}
			else if (t < strikeEnd)
			{
				float k2 = Out((t - windEnd) / (strikeEnd - windEnd));
				h.W = Mix(wind.W, strike.W, k2);
				h.D = MixDir(wind.D, strike.D, k2);
				h.T = MixDir(wind.T, strike.T, k2);
			}
			else if (t < total)
			{
				float k3 = Ease((t - strikeEnd) / (total - strikeEnd));
				h.W = Mix(strike.W, hand.W, k3);
				h.D = MixDir(strike.D, hand.D, k3);
				h.T = MixDir(strike.T, hand.T, k3);
			}
		}

		private static void GuardPose(WeaponKind kind, float g, ref Hand right, ref Hand left)
		{
			g = Ease(g);
			Hand hand;
			Hand hand2;
			switch (kind)
			{
			case WeaponKind.Sword:
			case WeaponKind.Axe:
			case WeaponKind.Hammer:
			case WeaponKind.Stick:
			case WeaponKind.Dagger:
				hand = new Hand
				{
					W = new Vec3(0.08f, 0.38f, -0.15f),
					D = N(-0.55f, 1f, 0.15f),
					T = N(-1f, 0.22f, 0.1f),
					Show = true
				};
				hand2 = new Hand
				{
					W = new Vec3(-0.12f, 0.37f, -0.19f),
					D = N(0.45f, 1f, 0.3f),
					T = N(1f, 0.1f, 0.45f),
					Show = true
				};
				break;
			case WeaponKind.Spear:
			case WeaponKind.Staff:
				hand = new Hand
				{
					W = new Vec3(0.15f, 0.33f, -0.165f),
					D = N(-0.4f, 1f, 0.25f),
					T = N(-1f, 0.2f, 0.06f),
					Show = true
				};
				hand2 = new Hand
				{
					W = new Vec3(-0.12f, 0.38f, -0.155f),
					D = N(0.4f, 1f, 0.25f),
					T = N(-1f, 0.2f, 0.06f),
					Show = true
				};
				break;
			case WeaponKind.None:
				hand = new Hand
				{
					W = new Vec3(0.1f, 0.34f, -0.145f),
					D = N(-0.25f, 1f, 0.5f),
					T = N(-0.35f, 0.15f, 1f),
					Show = true
				};
				hand2 = new Hand
				{
					W = new Vec3(-0.1f, 0.34f, -0.145f),
					D = N(0.25f, 1f, 0.5f),
					T = N(0.35f, 0.15f, 1f),
					Show = true
				};
				break;
			default:
				right.W += new Vec3(-0.05f, -0.02f, 0.05f) * g;
				left.W += new Vec3(0.03f, -0.02f, 0.05f) * g;
				return;
			}
			right.W = Mix(right.W, hand.W, g);
			right.D = MixDir(right.D, hand.D, g);
			right.T = MixDir(right.T, hand.T, g);
			left.W = Mix(left.W, hand2.W, g);
			left.D = MixDir(left.D, hand2.D, g);
			left.T = MixDir(left.T, hand2.T, g);
			left.Show = true;
		}

		private static float StrikePath(float x)
		{
			x = Fm.Clamp01(x);
			if (x < 0.6f)
			{
				float num = x / 0.6f;
				return 0.55f * num * num;
			}
			return 0.55f + 0.45f * Out((x - 0.6f) / 0.39999998f);
		}

		private static void Blow(ref Hand h, float t, in StrikeTiming tm, Hand wind, Hand strike)
		{
			Hand hand = h;
			Hand hand2 = wind;
			hand2.W = wind.W + (wind.W - hand.W) * 0.07f;
			if (t < tm.Windup)
			{
				float k = Ease(t / Fm.Max(0.01f, tm.Windup));
				h.W = Mix(hand.W, wind.W, k);
				h.D = MixDir(hand.D, wind.D, k);
				h.T = MixDir(hand.T, wind.T, k);
			}
			else if (t < tm.StrikeStart)
			{
				float k2 = Out((t - tm.Windup) / Fm.Max(0.01f, tm.Hold));
				h.W = Mix(wind.W, hand2.W, k2);
				h.D = wind.D;
				h.T = wind.T;
			}
			else if (t < tm.StrikeEnd)
			{
				float k3 = StrikePath((t - tm.StrikeStart) / Fm.Max(0.01f, tm.Strike));
				h.W = Mix(hand2.W, strike.W, k3);
				h.D = MixDir(hand2.D, strike.D, k3);
				h.T = MixDir(hand2.T, strike.T, k3);
			}
			else
			{
				float num = 0.05f + 0.05f * tm.Weight;
				float num2 = Fm.Max(0.12f, (tm.Total - tm.StrikeEnd - num) * 0.9f);
				float k4 = Ease((t - tm.StrikeEnd - num) / num2);
				h.W = Mix(strike.W, hand.W, k4);
				h.D = MixDir(strike.D, hand.D, k4);
				h.T = MixDir(strike.T, hand.T, k4);
			}
		}

		private static void Counter(ref Hand h, float t, in StrikeTiming tm, Vec3 wound, Vec3 struck)
		{
			Vec3 vec;
			if (t < tm.Windup)
			{
				vec = wound * Ease(t / Fm.Max(0.01f, tm.Windup));
			}
			else if (t < tm.StrikeStart)
			{
				vec = wound;
			}
			else if (t < tm.StrikeEnd)
			{
				vec = Mix(wound, struck, StrikePath((t - tm.StrikeStart) / Fm.Max(0.01f, tm.Strike)));
			}
			else
			{
				float num = Fm.Max(0.12f, (tm.Total - tm.StrikeEnd) * 0.85f);
				vec = struck * (1f - Ease((t - tm.StrikeEnd - 0.04f) / num));
			}
			h.W += vec;
		}

		private void Rebound(WeaponKind kind, float t, float stoppedAt, float weight, bool rings, ref Hand right, ref Hand left)
		{
			StrikeTiming strikeTiming = StrikeTiming.For(kind, melee: true, hands: true).WithWindup(_windupScale);
			Hand hand = right;
			Hand hand2 = left;
			float num = t - stoppedAt;
			float num2 = Math.Min(stoppedAt, strikeTiming.Hit + 0.03f);
			float num3 = num2 - Fm.Max(0f, num2 - strikeTiming.StrikeStart) * (0.2f + 0.1f * Fm.Clamp01(weight));
			float num4 = 0.08f + 0.05f * weight;
			float num5 = 0.04f + 0.05f * weight;
			float num6 = Fm.Max(0.18f, (strikeTiming.Total - stoppedAt - num4 - num5) * 0.9f);
			if (num < num4 + num5)
			{
				Blow(kind, melee: true, num2 + (num3 - num2) * Out(num / num4), ref right, ref left);
			}
			else
			{
				Hand right2 = hand;
				Hand left2 = hand2;
				Blow(kind, melee: true, num3, ref right2, ref left2);
				float k = Ease((num - num4 - num5) / num6);
				right = MixHand(right2, hand, k);
				left = MixHand(left2, hand2, k);
				right.Show = hand.Show;
				left.Show = hand2.Show;
			}
			if (kind != WeaponKind.None)
			{
				Shiver(ref right, num, weight, rings);
			}
		}

		private static void Shiver(ref Hand h, float since, float weight, bool rings)
		{
			if (rings)
			{
				Vec3 vec = Vec3.Cross(h.T, h.D);
				if (!(Vec3.Dot(vec, vec) < 1E-06f))
				{
					vec = vec.Normalized();
					float num = (0.007f + 0.009f * Fm.Clamp01(weight)) * Fm.Exp((0f - since) / 0.13f);
					float num2 = Fm.Sin(since * 2f * MathF.PI * 23f);
					h.W += vec * (num * num2);
					h.T = (h.T + vec * (num * 4f * num2)).Normalized();
				}
			}
			else
			{
				h.W -= h.D * (0.012f * (0.5f + weight) * Fm.Exp((0f - since) / 0.05f));
			}
		}

		private static void Jolt(float age, float weight, bool rings, WeaponKind kind, ref Hand right, ref Hand left)
		{
			float num = ((age < 0.035f) ? Out(age / 0.035f) : (1f - Ease((age - 0.035f) / 0.4f))) * (0.5f + 0.6f * Fm.Clamp01(weight));
			if (kind == WeaponKind.None)
			{
				right.W += new Vec3(-0.035f, -0.05f, 0.08f) * num;
				left.W += new Vec3(0.035f, -0.05f, 0.08f) * num;
				return;
			}
			right.W += new Vec3(0.03f, -0.065f, 0.025f) * num;
			right.T = (right.T + new Vec3(0.25f, -0.2f, 0f) * num).Normalized();
			left.W += new Vec3(-0.015f, -0.04f, 0.015f) * num;
			Shiver(ref right, age, weight, rings);
		}

		private void Blow(WeaponKind kind, bool melee, float t, ref Hand right, ref Hand left)
		{
			StrikeTiming tm = StrikeTiming.For(kind, melee, hands: true).WithWindup(_windupScale);
			switch (kind)
			{
			case WeaponKind.Sword:
			case WeaponKind.Stick:
			case WeaponKind.Dagger:
				if (_slashSide > 0)
				{
					Blow(ref right, t, in tm, new Hand
					{
						W = new Vec3(0.24f, 0.32f, 0.03f),
						D = N(-0.1f, 1f, 0.7f),
						T = N(0.5f, -0.35f, 1f)
					}, new Hand
					{
						W = new Vec3(-0.13f, 0.5f, -0.21f),
						D = N(-0.45f, 1f, -0.1f),
						T = N(-1f, 0.3f, -0.42f)
					});
					Counter(ref left, t, in tm, new Vec3(0.03f, 0.03f, 0.01f), new Vec3(-0.035f, -0.05f, -0.015f));
				}
				else
				{
					Blow(ref right, t, in tm, new Hand
					{
						W = new Vec3(-0.07f, 0.38f, 0.02f),
						D = N(-0.75f, 1f, 0.45f),
						T = N(-0.75f, -0.15f, 1f)
					}, new Hand
					{
						W = new Vec3(0.29f, 0.48f, -0.21f),
						D = N(0.2f, 1f, -0.08f),
						T = N(1f, 0.28f, -0.32f)
					});
					Counter(ref left, t, in tm, new Vec3(-0.03f, -0.035f, 0f), new Vec3(0.02f, 0.03f, -0.01f));
				}
				break;
			case WeaponKind.Axe:
			case WeaponKind.Hammer:
			{
				bool flag = kind == WeaponKind.Hammer;
				Blow(ref right, t, in tm, flag ? new Hand
				{
					W = new Vec3(0.18f, 0.36f, 0.06f),
					D = N(-0.15f, 0.55f, 1f),
					T = N(0.2f, -0.3f, 1f)
				} : new Hand
				{
					W = new Vec3(0.19f, 0.38f, 0.04f),
					D = N(-0.15f, 0.6f, 1f),
					T = N(0.15f, -0.2f, 1f)
				}, new Hand
				{
					W = new Vec3(0.04f, 0.56f, -0.22f),
					D = N(-0.1f, 1f, -0.14f),
					T = N(-0.05f, 1f, -0.48f)
				});
				Counter(ref left, t, in tm, new Vec3(0.02f, 0.02f, 0.03f), new Vec3(-0.02f, -0.06f, -0.04f));
				break;
			}
			case WeaponKind.Spear:
			{
				Hand hand = right;
				Hand hand2 = left;
				Vec3 vec = new Vec3(0.01f, -0.1f, 0.015f);
				Vec3 vec2 = new Vec3(-0.06f, 0.36f, 0.05f);
				Blow(ref right, t, in tm, new Hand
				{
					W = hand.W + vec,
					D = hand.D,
					T = hand.T
				}, new Hand
				{
					W = hand.W + vec2,
					D = N(-0.35f, 1f, 0.15f),
					T = N(-0.05f, 1f, 0.1f)
				});
				Blow(ref left, t, in tm, new Hand
				{
					W = hand2.W + vec,
					D = hand2.D,
					T = hand2.T
				}, new Hand
				{
					W = hand2.W + vec2,
					D = N(0.45f, 1f, 0.15f),
					T = N(-0.05f, 1f, 0.1f)
				});
				break;
			}
			case WeaponKind.Staff:
				Blow(ref right, t, in tm, new Hand
				{
					W = right.W + new Vec3(0.03f, -0.06f, 0.02f),
					D = right.D,
					T = N(-0.05f, -0.2f, 1f)
				}, new Hand
				{
					W = right.W + new Vec3(-0.06f, 0.21f, 0.1f),
					D = N(-0.2f, 1f, 0.4f),
					T = N(-0.1f, 0.45f, 1f)
				});
				Counter(ref left, t, in tm, new Vec3(0.01f, 0.02f, 0.01f), new Vec3(-0.01f, -0.03f, -0.01f));
				break;
			case WeaponKind.Gun:
				Animate(kind, melee, t, ref right, ref left);
				break;
			default:
			{
				bool leftPunch = _leftPunch;
				Hand h = (leftPunch ? left : right);
				float num = (leftPunch ? (-1f) : 1f);
				Blow(ref h, t, in tm, new Hand
				{
					W = h.W + new Vec3(0.02f * num, -0.07f, 0.02f),
					D = N(-0.2f * num, 1f, 0.3f),
					T = h.T
				}, new Hand
				{
					W = new Vec3(0.035f * num, 0.6f, -0.09f),
					D = N(-0.06f * num, 1f, 0.08f),
					T = N(-1f * num, 0.05f, 0.18f)
				});
				if (leftPunch)
				{
					left = h;
					Counter(ref right, t, in tm, new Vec3(0f, 0.02f, 0f), new Vec3(0.01f, -0.04f, -0.01f));
				}
				else
				{
					right = h;
					Counter(ref left, t, in tm, new Vec3(0f, 0.02f, 0f), new Vec3(-0.01f, -0.04f, -0.01f));
				}
				break;
			}
			}
		}

		private void Animate(WeaponKind kind, bool melee, float t, ref Hand right, ref Hand left)
		{
			float num2;
			float num3;
			float total;
			float total2;
			switch (kind)
			{
			case WeaponKind.Stick:
			case WeaponKind.Dagger:
				num2 = 0.32f;
				goto IL_0041;
			case WeaponKind.Sword:
				num2 = 0.42f;
				goto IL_0041;
			case WeaponKind.Axe:
				num3 = 0.5f;
				goto IL_01ee;
			case WeaponKind.Hammer:
				num3 = 0.6f;
				goto IL_01ee;
			case WeaponKind.Spear:
			{
				Hand hand = right;
				Hand hand2 = left;
				Vec3 vec = new Vec3(0f, -0.06f, 0.01f);
				Vec3 vec2 = new Vec3(-0.06f, 0.34f, 0.05f);
				Beat(ref right, t, 0.1f, 0.21f, 0.45f, new Hand
				{
					W = hand.W + vec,
					D = hand.D,
					T = hand.T
				}, new Hand
				{
					W = hand.W + vec2,
					D = N(-0.35f, 1f, 0.15f),
					T = N(-0.05f, 1f, 0.1f)
				});
				Beat(ref left, t, 0.1f, 0.21f, 0.45f, new Hand
				{
					W = hand2.W + vec,
					D = hand2.D,
					T = hand2.T
				}, new Hand
				{
					W = hand2.W + vec2,
					D = N(0.45f, 1f, 0.15f),
					T = N(-0.05f, 1f, 0.1f)
				});
				break;
			}
			case WeaponKind.Staff:
				Beat(ref right, t, 0.12f, 0.24f, 0.55f, new Hand
				{
					W = right.W + new Vec3(0.02f, -0.04f, -0.02f),
					D = right.D,
					T = N(-0.05f, -0.1f, 1f)
				}, new Hand
				{
					W = right.W + new Vec3(-0.06f, 0.2f, 0.1f),
					D = N(-0.2f, 1f, 0.4f),
					T = N(-0.1f, 0.45f, 1f)
				});
				break;
			case WeaponKind.Gun:
			{
				float x = ((t < 0.05f) ? (t / 0.05f) : Fm.Max(0f, 1f - (t - 0.05f) / 0.3f));
				x = Ease(x);
				right.W += new Vec3(0f, -0.07f, 0.03f) * x;
				right.T = Vec3.Rotate(right.T, new Vec3(1f, 0f, 0f), -0.35f * x).Normalized();
				right.D = Vec3.Rotate(right.D, new Vec3(1f, 0f, 0f), 0.2f * x).Normalized();
				left.W += new Vec3(0f, -0.06f, 0.02f) * x;
				break;
			}
			default:
				{
					bool leftPunch = _leftPunch;
					Hand h = (leftPunch ? left : right);
					float num = (leftPunch ? (-1f) : 1f);
					Beat(ref h, t, 0.02f, 0.085f, 0.3f, new Hand
					{
						W = h.W + new Vec3(0f, -0.02f, 0f),
						D = h.D,
						T = h.T
					}, new Hand
					{
						W = new Vec3(0.035f * num, 0.6f, -0.09f),
						D = N(-0.06f * num, 1f, 0.08f),
						T = N(-1f * num, 0.05f, 0.18f)
					});
					if (leftPunch)
					{
						left = h;
					}
					else
					{
						right = h;
					}
					break;
				}
				IL_0041:
				total = num2;
				if (_slashSide > 0)
				{
					Beat(ref right, t, 0.06f, 0.17f, total, new Hand
					{
						W = new Vec3(0.27f, 0.26f, 0.02f),
						D = N(-0.1f, 1f, 0.6f),
						T = N(0.35f, -0.15f, 1f)
					}, new Hand
					{
						W = new Vec3(-0.12f, 0.5f, -0.2f),
						D = N(-0.45f, 1f, -0.08f),
						T = N(-1f, 0.3f, -0.4f)
					});
				}
				else
				{
					Beat(ref right, t, 0.06f, 0.17f, total, new Hand
					{
						W = new Vec3(-0.04f, 0.3f, 0.02f),
						D = N(-0.5f, 1f, 0.5f),
						T = N(-0.65f, 0.05f, 1f)
					}, new Hand
					{
						W = new Vec3(0.28f, 0.48f, -0.2f),
						D = N(0.2f, 1f, -0.06f),
						T = N(1f, 0.28f, -0.3f)
					});
				}
				break;
				IL_01ee:
				total2 = num3;
				Beat(ref right, t, 0.12f, 0.26f, total2, new Hand
				{
					W = new Vec3(0.22f, 0.36f, 0.1f),
					D = N(-0.15f, 0.55f, 1f),
					T = N(0f, -0.45f, 1f)
				}, new Hand
				{
					W = new Vec3(0.05f, 0.56f, -0.22f),
					D = N(-0.1f, 1f, -0.12f),
					T = N(-0.05f, 1f, -0.5f)
				});
				break;
			}
		}

		private void GesturePose(in HandsInput input, float dt, ref Hand free, ref Hand right, ref Hand left, bool rightIsFree, out bool showProp, out float glow)
		{
			float sinceGesture = input.SinceGesture;
			float num = Fm.Max(0.05f, input.GestureRelease);
			float num2 = (rightIsFree ? 1f : (-1f));
			showProp = false;
			glow = 0f;
			_cocked = Fm.MoveTowards(_cocked, input.GestureHeld ? 1f : 0f, dt / (input.GestureHeld ? 0.16f : 0.05f));
			switch (input.Gesture)
			{
			case Gesture.Throw:
			{
				Hand wind4 = new Hand
				{
					W = new Vec3(0.2f * num2, 0.3f, -0.03f),
					D = N(-0.25f * num2, 0.35f, 1f),
					T = N(0f - num2, -0.2f, 0.3f),
					Show = true
				};
				Hand strike4 = new Hand
				{
					W = new Vec3(0.03f * num2, 0.56f, -0.15f),
					D = N(-0.3f * num2, 1f, 0.25f),
					T = N(0f - num2, 0.3f, 0.2f),
					Show = true
				};
				if (input.GestureHeld)
				{
					Hand hand4 = free;
					float k = Ease(_cocked);
					free.W = Mix(hand4.W, wind4.W, k);
					free.D = MixDir(hand4.D, wind4.D, k);
					free.T = MixDir(hand4.T, wind4.T, k);
					free.Show = true;
					showProp = true;
				}
				else if (sinceGesture < num + 0.45f)
				{
					Beat(ref free, sinceGesture, Fm.Max(0.01f, num * 0.35f), num, num + 0.45f, wind4, strike4);
					free.Show = true;
					showProp = sinceGesture < num * 0.95f;
				}
				break;
			}
			case Gesture.Cast:
			{
				Hand wind3 = new Hand
				{
					W = new Vec3(0.09f * num2, 0.42f, -0.06f),
					D = N(-0.2f * num2, 1f, 0.35f),
					T = N(0f - num2, 0.1f, 0.4f),
					Show = true
				};
				Hand strike3 = new Hand
				{
					W = new Vec3(0.05f * num2, 0.58f, -0.08f),
					D = N(-0.32f * num2, 1f, 0.22f),
					T = N(0f - num2, 0.05f, 0.3f),
					Show = true
				};
				if (input.GestureHeld)
				{
					Hand hand3 = free;
					float num5 = Ease(_cocked);
					free.W = Mix(hand3.W, wind3.W, num5);
					free.D = MixDir(hand3.D, wind3.D, num5);
					free.T = MixDir(hand3.T, wind3.T, num5);
					free.W += new Vec3(0f, 0f, 0.004f * Fm.Sin(input.Time * 23f));
					free.Show = true;
					glow = 0.4f + 0.4f * num5;
				}
				else if (sinceGesture < num + 0.6f)
				{
					Beat(ref free, sinceGesture, Fm.Max(0.01f, num * 0.5f), num + 0.05f, num + 0.6f, wind3, strike3);
					free.Show = true;
					glow = ((sinceGesture < num + 0.25f) ? 1f : Fm.Max(0f, 1f - (sinceGesture - num - 0.25f) / 0.3f));
				}
				break;
			}
			case Gesture.Shout:
				if (!(sinceGesture > 1f))
				{
					float num3 = ((sinceGesture < 0.1f) ? Ease(sinceGesture / 0.1f) : ((sinceGesture < 0.7f) ? 1f : (1f - Ease((sinceGesture - 0.7f) / 0.3f))));
					float num4 = 0.006f * num3 * Fm.Sin(input.Time * 41f);
					Hand hand = new Hand
					{
						W = new Vec3(0.21f, 0.38f, -0.03f + num4),
						D = N(-0.3f, 0.45f, 1f),
						T = N(-1f, -0.1f, 0.3f),
						Show = true
					};
					Hand hand2 = new Hand
					{
						W = new Vec3(-0.21f, 0.38f, -0.03f - num4),
						D = N(0.3f, 0.45f, 1f),
						T = N(1f, -0.1f, 0.3f),
						Show = true
					};
					right.W = Mix(right.W, hand.W, num3);
					right.D = MixDir(right.D, hand.D, num3);
					right.T = MixDir(right.T, hand.T, num3);
					left.W = Mix(left.W, hand2.W, num3);
					left.D = MixDir(left.D, hand2.D, num3);
					left.T = MixDir(left.T, hand2.T, num3);
					left.Show = true;
				}
				break;
			case Gesture.Slam:
				if (!(sinceGesture > num + 0.6f))
				{
					Hand wind = new Hand
					{
						W = new Vec3(0.1f, 0.3f, 0.14f),
						D = N(-0.15f, 0.4f, 1f),
						T = N(-0.2f, -0.6f, 1f),
						Show = true
					};
					Hand strike = new Hand
					{
						W = new Vec3(0.1f, 0.5f, -0.21f),
						D = N(-0.35f, 1f, 0.05f),
						T = N(-0.2f, 0.6f, -0.6f),
						Show = true
					};
					Hand wind2 = new Hand
					{
						W = new Vec3(-0.1f, 0.3f, 0.14f),
						D = N(0.15f, 0.4f, 1f),
						T = N(0.2f, -0.6f, 1f),
						Show = true
					};
					Hand strike2 = new Hand
					{
						W = new Vec3(-0.1f, 0.5f, -0.21f),
						D = N(0.35f, 1f, 0.05f),
						T = N(0.2f, 0.6f, -0.6f),
						Show = true
					};
					Beat(ref right, sinceGesture, num * 0.7f, num, num + 0.6f, wind, strike);
					Beat(ref left, sinceGesture, num * 0.7f, num, num + 0.6f, wind2, strike2);
					left.Show = true;
					glow = ((sinceGesture > num - 0.05f && sinceGesture < num + 0.3f) ? 1f : 0f);
				}
				break;
			}
		}

		private void PlaceProp(List<MeshInstance> overlay, Hand h, MeshModel model, float time)
		{
			float num = Fm.Max(0.01f, Fm.Max(model.Max.Z - model.Min.Z, Fm.Max(model.Max.X - model.Min.X, model.Max.Y - model.Min.Y)));
			float num2 = 0.085f / num;
			Mat3 mat = Mat3.RotZ(time * 1.3f) * Mat3.RotX(0.4f);
			Vec3 vec = mat.Mul(new Vec3(num2, 0f, 0f));
			Vec3 vec2 = mat.Mul(new Vec3(0f, num2, 0f));
			Vec3 vec3 = mat.Mul(new Vec3(0f, 0f, num2));
			Vec3 vec4 = (model.Min + model.Max) * 0.5f;
			Vec3 vec5 = h.W + h.D.Normalized() * 0.022f + new Vec3(0f, 0f, 0.018f);
			_prop.Model = model;
			_prop.Palette = null;
			Place(_prop, vec5 - (vec * vec4.X + vec2 * vec4.Y + vec3 * vec4.Z), vec, vec2, vec3);
			overlay.Add(_prop);
		}

		private void AddGlow(List<VoxelObject> voxels, Hand h, uint color, float strength, float time)
		{
			if (!GlowShapes.TryGetValue(color, out var value))
			{
				if (GlowShapes.Count > 32)
				{
					GlowShapes.Clear();
				}
				value = VoxelShape.NewGrid(1, 1, 1);
				value.Box(0, 0, -1, 1, 1, 1, color | 0xFF000000u);
				GlowShapes[color] = value;
			}
			int num = 3 + (int)(strength * 6f);
			Vec3 vec = h.W + h.D.Normalized() * 0.045f + new Vec3(0f, 0f, 0.02f);
			for (int i = 0; i < num; i++)
			{
				float num2 = time * (5f + (float)i * 0.7f) + (float)i * 2.4f;
				float num3 = 0.02f + 0.025f * strength * (0.5f + 0.5f * Fm.Sin(time * 7f + (float)i));
				Vec3 viewPoint = vec + new Vec3(Fm.Cos(num2) * num3, Fm.Sin(num2 * 0.7f) * num3 * 0.6f, Fm.Sin(num2) * num3);
				float num4 = (0.005f + 0.006f * strength) * _s;
				voxels.Add(new VoxelObject
				{
					Shape = value,
					Overlay = true,
					Emissive = true,
					NoShadow = true,
					Tag = -1,
					U = _r,
					V = _u,
					N = _f,
					SizeU = num4,
					SizeV = num4,
					SizeN = num4,
					Origin = World(viewPoint)
				});
			}
		}

		private static void Reach(ref Hand right, float t)
		{
			Beat(ref right, t, 0.18f, 0.3f, 0.55f, new Hand
			{
				W = new Vec3(0.08f, 0.56f, -0.38f),
				D = N(-0.2f, 1f, -0.7f),
				T = N(-0.3f, 0.3f, 1f)
			}, new Hand
			{
				W = new Vec3(0.1f, 0.44f, -0.3f),
				D = N(-0.25f, 1f, -0.4f),
				T = N(-0.4f, 0.2f, 1f)
			});
		}

		private void Swim(List<MeshInstance> overlay, float swimPhase)
		{
			float b = Fm.Sin(swimPhase);
			float num = Fm.Cos(swimPhase);
			for (int i = -1; i <= 1; i += 2)
			{
				Hand h = new Hand
				{
					W = new Vec3((float)i * (0.08f + 0.17f * Fm.Max(0f, b)), 0.32f + 0.08f * num, -0.07f + 0.02f * num),
					D = N((float)i * (0.15f + 0.5f * Fm.Max(0f, b)), 1f, 0.1f),
					T = N((float)(-i) * 0.9f, 0.1f, 0.3f),
					Show = true
				};
				PlaceArm(overlay, (i > 0) ? _armR : _armL, h, i < 0);
			}
		}

		private Vec3 WorldDir(Vec3 v)
		{
			return _r * (v.X * _fovK) + _f * v.Y + _u * (v.Z * _fovK);
		}

		private Vec3 Shear(Vec3 v)
		{
			return v + _u * (_pitch * Vec3.Dot(v, _f));
		}

		private Vec3 World(Vec3 viewPoint)
		{
			return _eye + Shear(WorldDir(viewPoint * _s));
		}

		private void Place(MeshInstance inst, Vec3 originView, Vec3 x, Vec3 y, Vec3 z)
		{
			inst.Position = World(originView);
			inst.Basis = new Mat3(Shear(WorldDir(x) * _s), Shear(WorldDir(y) * _s), Shear(WorldDir(z) * _s));
			inst.SortDepth = 0f;
			inst.Tag = -1;
		}

		private void PlaceArm(List<MeshInstance> overlay, MeshInstance inst, Hand h, bool left, bool holding = false, bool cradle = false)
		{
			MeshModel armRModel = (inst.Model = ViewMeshes.Arm(_skin, _sleeve, left, _bare, holding, _armLook, _armor, cradle));
			inst.Palette = null;
			Vec3 vec = h.T.Normalized();
			Vec3 vec2 = h.D.Normalized();
			Vec3 vec3 = vec - vec2 * Vec3.Dot(vec, vec2);
			vec3 = ((Vec3.Dot(vec3, vec3) > 0.0001f) ? vec3.Normalized() : new Vec3(0f, 0f, 1f));
			Vec3 vec4 = Vec3.Cross(vec2, vec3);
			Mat3 mat = Mat3.Identity;
			if (holding)
			{
				Vec3 vec5 = Vec3.Cross(vec3, vec);
				float num = (float)Math.Sqrt(Vec3.Dot(vec5, vec5));
				if (num > 0.001f)
				{
					float val = (float)Math.Atan2(num, Vec3.Dot(vec3, vec));
					vec5 *= 1f / num;
					mat = Mat3.Rotation(new Vec3(Vec3.Dot(vec4, vec5), Vec3.Dot(vec2, vec5), Vec3.Dot(vec3, vec5)), Math.Min(val, 0.6f));
				}
			}
			if (inst.PartRotation == null || inst.PartRotation.Length != 2)
			{
				inst.PartRotation = new Mat3[2];
			}
			inst.PartRotation[0] = Mat3.Identity;
			inst.PartRotation[1] = mat;
			Vec3 vec6 = (cradle ? ViewMeshes.CradleSeat : (holding ? ViewMeshes.Grip : ViewMeshes.FistCentre));
			if (left)
			{
				vec6 = new Vec3(0f - vec6.X, vec6.Y, vec6.Z);
			}
			Vec3 vec7 = mat.Mul(vec6 - ViewMeshes.Wrist) + ViewMeshes.Wrist;
			Vec3 vec8 = h.W - vec4 * vec7.X - vec2 * vec7.Y - vec3 * vec7.Z;
			Place(inst, vec8, vec4, vec2, vec3);
			overlay.Add(inst);
			if (inst == _armR)
			{
				_armROrigin = vec8;
				_armRX = vec4;
				_armRY = vec2;
				_armRZ = vec3;
				_armRModel = armRModel;
			}
		}

		private void PlaceItem(List<MeshInstance> overlay, Hand right, float sinceAttack)
		{
			if (_look.Kind == WeaponKind.Bow)
			{
				PlaceBow(overlay, right, sinceAttack);
				return;
			}
			if (_look.Kind == WeaponKind.Gun && _gunModel != null)
			{
				PlaceBlaster(overlay, right, _gunModel, _blaster, sinceAttack, _time);
				return;
			}
			MeshModel meshModel = _heldModel ?? ViewMeshes.Item(_look);
			_item.Model = meshModel;
			_item.Palette = null;
			if (_item.PartRotation != null && _item.PartRotation.Length != meshModel.Parts.Length)
			{
				_item.PartRotation = null;
			}
			ItemFrame(right, _heldPalm, out var x, out var y, out var z);
			Place(_item, right.W, x, y, z);
			_item.Emissive = false;
			PoseFlame(overlay, meshModel, sinceAttack);
			PoseTorch(meshModel);
			overlay.Add(_item);
		}

		private BowState FallbackBow(in HandsInput input)
		{
			if (input.Attacks != _ownAttacks)
			{
				if (_ownAttacks >= 0)
				{
					_ownBow.Pose(BowState.Phase.Loosed, 0f, 0f);
				}
				_ownAttacks = input.Attacks;
			}
			_ownBow.Update(input.Dt, held: false, allowed: true);
			return _ownBow;
		}

		private void BowPose(BowState bow, float time, float dt, ref Hand right, ref Hand left)
		{
			BowState.Phase state = bow.State;
			float draw = bow.Draw;
			float time2 = bow.Time;
			float num;
			switch (state)
			{
			default:
				num = 0f;
				break;
			case BowState.Phase.LettingDown:
				num = Ease(draw / 0.55f) * 0.8f;
				break;
			case BowState.Phase.Loosed:
				num = ((time2 < 0.3f) ? Fm.Max(_raise, 0.65f) : 0.65f);
				break;
			case BowState.Phase.Drawing:
			case BowState.Phase.Held:
				num = Ease(draw / 0.55f);
				break;
			}
			float num2 = num;
			_raise = ((num2 > _raise) ? Fm.Max(num2, Fm.MoveTowards(_raise, num2, dt * 10f)) : Fm.MoveTowards(_raise, num2, dt * ((state == BowState.Phase.Ready) ? 1.6f : 3f)));
			float num3 = Ease(_raise);
			Vec3 vec = Mix(CarryGrip, AimGrip, num3);
			float v = 1.18f + -1.68f * Ease(Fm.Clamp01(num3 / 0.7f));
			Vec3 vec2 = MixDir(N(-0.07f, 1f, -0.07f), AimArrow, num3);
			float num4 = ((state != BowState.Phase.Loosed) ? 0f : ((time2 < 0.05f) ? Out(time2 / 0.05f) : Fm.Max(0f, 1f - (time2 - 0.05f) / 0.3f)));
			vec = vec + vec2 * (0.02f * num4) + new Vec3(0f, 0f, -0.008f * num4);
			float tremble = bow.Tremble;
			if (tremble > 0f)
			{
				vec += new Vec3(Fm.Sin(time * 13.1f) * 0.0055f + Fm.Sin(time * 29f) * 0.002f, 0f, Fm.Sin(time * 17.3f + 1f) * 0.005f) * tremble;
			}
			Vec3 vec3 = new Vec3(Fm.Sin(v), 0f, Fm.Cos(v));
			vec3 = (vec3 + vec2 * (0.12f * num4)).Normalized();
			Vec3 vec4 = (vec3 - vec2 * Vec3.Dot(vec3, vec2)).Normalized();
			_bowY = vec2;
			_bowZ = vec4;
			_bowX = Vec3.Cross(vec2, vec4);
			left.W = vec;
			left.T = vec4;
			left.D = MixDir(N(0.3f, 1f, 0.32f), N(0.2f, 1f, 0.13f), num3);
			_flex = draw;
			_nockY = -0.105f - 0.27f * draw;
			_vibrate = 0f;
			if (state == BowState.Phase.Loosed && time2 < 0.35f)
			{
				_vibrate = 0.018f * Fm.Exp((0f - time2) * 11f) * Fm.Sin(time2 * 95f);
			}
			_arrowOn = bow.ArrowNocked;
			_leaving = state == BowState.Phase.Loosed && bow.Fired;
			_shotDraw = bow.LastShotDraw;
			_heldArrow = false;
			Vec3 vec5 = vec + vec4 * 0.034f;
			Vec3 w = vec5 + vec2 * _nockY + _bowX * 0.012f - vec4 * 0.016f;
			Vec3 vec6 = MixDir(N(-0.5f, 1f, 0.28f), N(-0.62f, 1f, 0.08f), num3);
			Vec3 vec7 = MixDir(vec6, N(-1f, 0.3f, 0.06f), draw);
			Vec3 vec8 = MixDir(vec4, N(-0.05f, 0.15f, 1f), num3);
			if (state != BowState.Phase.Loosed)
			{
				_onString = true;
				right.W = w;
				right.D = vec7;
				right.T = vec8;
				_nockRel = right.W - left.W;
				return;
			}
			_onString = false;
			Vec3 vec9 = vec5 + vec2 * (-0.105f - 0.27f * bow.LastShotDraw) + _bowX * 0.012f;
			Vec3 vec10 = vec5 + vec2 * -0.105f + _bowX * 0.012f - vec4 * 0.016f;
			Vec3 b = vec9 + new Vec3(0.07f, -0.05f, -0.01f);
			Vec3 vec11 = Quiver + new Vec3(0f, 0f, 0.05f);
			if (time2 < 0.12f)
			{
				float k = Out(time2 / 0.12f);
				right.W = Mix(vec9, b, k);
				right.D = vec7;
				right.T = vec8;
			}
			else if (time2 < 0.5f)
			{
				float num5 = Fm.Clamp01((time2 - 0.12f) / 0.38f);
				float k2 = Ease(num5);
				float num6 = Out(num5);
				right.W = new Vec3(b.X + (Quiver.X - b.X) * num6, b.Y + (Quiver.Y - b.Y) * num6, b.Z + (Quiver.Z - b.Z) * num5 * num5);
				right.D = MixDir(vec7, N(-0.25f, 0.25f, 1f), k2);
				right.T = MixDir(vec8, N(-1f, 0.2f, 0.1f), k2);
				_heldArrow = time2 > 0.42000002f;
				_heldDir = N(0.15f, 0.35f, 1f);
			}
			else if (time2 < 0.62f)
			{
				float k3 = Ease((time2 - 0.5f) / 0.120000005f);
				right.W = Mix(Quiver, vec11, k3);
				right.D = N(-0.25f, 0.25f, 1f);
				right.T = N(-1f, 0.2f, 0.1f);
				_heldArrow = true;
				_heldDir = N(0.15f, 0.35f, 1f);
			}
			else if (time2 < 0.95f)
			{
				float k4 = Ease((time2 - 0.62f) / 0.32999998f);
				right.W = Mix(vec11, vec10 - vec2 * 0.03f, k4);
				right.D = MixDir(N(-0.25f, 0.25f, 1f), vec6, k4);
				right.T = MixDir(N(-1f, 0.2f, 0.1f), vec8, k4);
				_heldArrow = true;
				_heldDir = MixDir(N(0.15f, 0.35f, 1f), vec2, k4);
			}
			else
			{
				float num7 = Fm.Clamp01((time2 - 0.95f) / 0.19999999f);
				right.W = vec10 - vec2 * (0.03f * (1f - num7));
				right.D = vec6;
				right.T = vec8;
				_heldArrow = !_arrowOn;
				_heldDir = vec2;
			}
		}

		private void PlaceBow(List<MeshInstance> overlay, Hand right, float sinceAttack)
		{
			Vec3 vec = _bowLeft.W + _bowZ * 0.034f;
			_item.Model = ViewMeshes.Bow(_look, _flex);
			_item.Palette = null;
			Place(_item, vec, _bowX, _bowY, _bowZ);
			overlay.Add(_item);
			_string.Model = ViewMeshes.BowString(_nockY + _vibrate, _flex, _arrowOn && _arrowModel == null, _look.Head, _look.String, _look.StringGlow);
			_string.Palette = null;
			Place(_string, vec, _bowX, _bowY, _bowZ);
			overlay.Add(_string);
			if (_arrowModel == null)
			{
				return;
			}
			Vec3 vec2;
			Vec3 vec3;
			if (_arrowOn)
			{
				vec2 = vec + _bowY * (_nockY + _vibrate) - _bowX * 0.008f + _bowZ * 0.003f;
				vec3 = _bowY;
				Vec3 a = World(vec2 + vec3 * 0.47f) - _eye;
				float num = Vec3.Dot(a, _fl);
				ArrowTipFromEye = new Vec3(Vec3.Dot(a, _r), num, a.Z - _slope * num);
				ArrowTipTime = _time;
				ArrowTipRaise = _raise;
			}
			else if (_leaving)
			{
				vec2 = vec + _bowY * -0.004999995f - _bowX * 0.008f + _bowZ * 0.003f;
				vec3 = _bowY;
			}
			else
			{
				if (!_heldArrow)
				{
					return;
				}
				vec3 = _heldDir.Normalized();
				vec2 = right.W - vec3 * 0.02f;
			}
			Vec3 vec4 = Vec3.Cross(vec3, _bowZ);
			vec4 = ((Vec3.Dot(vec4, vec4) > 1E-05f) ? vec4.Normalized() : _bowX);
			Vec3 vec5 = Vec3.Cross(vec4, vec3);
			_arrow.Model = _arrowModel;
			_arrow.Palette = null;
			float num2 = 0.47f * (_trueSize ? 1f : (_arrowOn ? (1.5f - 0.4f * _flex) : (_leaving ? (1.5f - 0.4f * _shotDraw) : 1.5f)));
			Place(_arrow, vec2 + vec3 * 0.235f, vec4 * num2, vec3 * -0.47f, vec5 * num2);
			overlay.Add(_arrow);
		}

		private void Dress(SpriteImage body, bool humanoid, string species, uint gameSkin, uint sleeve = 0u)
		{
			if (body.Width > 1)
			{
				if (!humanoid)
				{
					gameSkin = 0u;
				}
				Humanoid.Palette palette = Humanoid.Extract(body, species, gameSkin);
				_skin = palette.Skin;
				uint skin = palette.Skin;
				float val = (float)(skin & 0xFF) / 255f;
				float val2 = (float)((skin >> 8) & 0xFF) / 255f;
				float val3 = (float)((skin >> 16) & 0xFF) / 255f;
				float num = Math.Max(val, Math.Max(val2, val3));
				float num2 = Math.Min(val, Math.Min(val2, val3));
				bool flag = num - num2 < 0.1f && num > 0.7f;
				if (humanoid && gameSkin == 0 && (!palette.SkinSeen || flag))
				{
					_skin = TerrainCache.Rgb(226, 178, 136);
				}
				_sleeve = ((!humanoid) ? palette.Skin : ((sleeve != 0) ? sleeve : palette.Shirt));
			}
			_bare = !humanoid;
		}

		private void DigStroke(float t, float rate, bool fill, ref Hand right)
		{
			t *= ((rate > 0f) ? rate : 1f);
			Hand hand = right;
			Hand hand2 = new Hand
			{
				W = hand.W + new Vec3(-0.03f, 0.13f, -0.05f),
				D = N(-0.32f, 1f, -0.05f),
				T = N(-0.22f, 1f, -0.42f)
			};
			Hand hand3 = new Hand
			{
				W = hand.W + new Vec3(0f, -0.04f, -0.07f),
				D = N(-0.35f, 1f, 0.15f),
				T = N(-0.2f, 1f, 0.12f)
			};
			Hand hand4 = (fill ? new Hand
			{
				W = hand.W + new Vec3(0f, 0.1f, 0f),
				D = N(-0.3f, 1f, 0.05f),
				T = N(-0.2f, 1f, -0.45f)
			} : new Hand
			{
				W = hand.W + new Vec3(0.1f, 0.02f, 0.04f),
				D = N(0.05f, 1f, 0.3f),
				T = N(0.75f, 1f, 0.25f)
			});
			if (t < 0.13f)
			{
				Between(ref right, hand, hand2, Out(t / 0.13f));
			}
			else if (t < 0.27f)
			{
				Between(ref right, hand2, hand3, Ease((t - 0.13f) / 0.14000002f));
			}
			else if (t < 0.42f)
			{
				Between(ref right, hand3, hand4, Out((t - 0.27f) / 0.14999998f));
			}
			else if (t < 0.68f)
			{
				Between(ref right, hand4, hand, Ease((t - 0.42f) / 0.26000002f));
			}
		}

		private static void Between(ref Hand h, Hand a, Hand b, float k)
		{
			h.W = Mix(a.W, b.W, k);
			h.D = MixDir(a.D, b.D, k);
			h.T = MixDir(a.T, b.T, k);
		}

		private Hand ItemReady(MeshModel model, bool palm)
		{
			if (palm)
			{
				Vec3 vec = N(0.05f, -0.55f, 1f);
				Vec3 vec2 = new Vec3(-0.38f, 1f, 0.2f);
				vec2 = (vec2 - vec * Vec3.Dot(vec2, vec)).Normalized();
				return new Hand
				{
					W = new Vec3(0.1f, 0.4f, -0.165f),
					D = vec2,
					T = Vec3.Cross(vec * -1f, vec2),
					Show = true
				};
			}
			float num = ((model != null) ? Math.Max(0f, model.Max.Z) : 0.1f);
			float num2 = ((model != null) ? Math.Max(0f, 0f - model.Min.Z) : 0.05f);
			Hand result = new Hand
			{
				W = new Vec3(0.15f, 0.4f, -0.19f),
				D = N(-0.32f, 1f, 0.18f),
				T = N(-0.25f, 0.05f, 1f),
				Show = true
			};
			if (num > 0.12f)
			{
				result.T = N(-0.25f, Math.Min(1.1f, 0.05f + (num - 0.12f) * 4f), 1f);
			}
			if (num2 > 0.07f)
			{
				result.W += new Vec3(0f, 0.04f, Math.Min(0.1f, (num2 - 0.07f) * 0.95f));
			}
			return result;
		}

		private static void ItemFrame(Hand h, bool palm, out Vec3 x, out Vec3 y, out Vec3 z)
		{
			if (palm)
			{
				y = h.D.Normalized();
				z = Vec3.Cross(h.T, h.D);
				z -= y * Vec3.Dot(z, y);
				z = ((Vec3.Dot(z, z) > 1E-06f) ? z.Normalized() : new Vec3(0f, 0f, 1f));
			}
			else
			{
				z = h.T.Normalized();
				y = h.D - z * Vec3.Dot(h.D, z);
				y = ((Vec3.Dot(y, y) > 0.0001f) ? y.Normalized() : new Vec3(0f, 1f, 0f));
			}
			x = Vec3.Cross(y, z);
		}

		private void EatPose(ref Hand right, float t, bool drink, bool palm, Vec3 mouth)
		{
			float num = ((t < 0.3f) ? Out(t / 0.3f) : ((t < 0.78f) ? 1f : (1f - Ease((t - 0.78f) / 0.37f))));
			if (!(num <= 0f))
			{
				Hand h = new Hand
				{
					Show = true
				};
				if (palm)
				{
					Vec3 vec = N(0.1f, -0.85f, 0.55f);
					Vec3 vec2 = new Vec3(-0.5f, 0.4f, 0.75f);
					h.T = Vec3.Cross(b: h.D = (vec2 - vec * Vec3.Dot(vec2, vec)).Normalized(), a: vec * -1f);
				}
				else if (mouth.Z < -0.03f)
				{
					h.D = N(-0.25f, 1f, 0.2f);
					h.T = N(0.95f, 0.08f, 0.3f);
				}
				else
				{
					h.D = N(-0.62f, 0.75f, 0.22f);
					h.T = (drink ? N(-0.18f, -0.78f, 0.6f) : N(-0.32f, -0.22f, 1f));
				}
				ItemFrame(h, palm, out var x, out var y, out var z);
				h.W = Lips - (x * mouth.X + y * mouth.Y + z * mouth.Z);
				if (t > 0.3f && t < 0.78f)
				{
					h.W += new Vec3(0f, -0.012f, 0.008f) * (float)Math.Abs(Math.Sin((double)((t - 0.3f) / 0.48f * 2f) * Math.PI));
				}
				right.W = Mix(right.W, h.W, num);
				right.D = MixDir(right.D, h.D, num);
				right.T = MixDir(right.T, h.T, num);
			}
		}

		private void PoseFlame(List<MeshInstance> overlay, MeshModel model, float sinceAttack)
		{
			if (!FlameSword.IsModel(model))
			{
				Flame.Shown = false;
				return;
			}
			StrikeTiming strikeTiming = StrikeTiming.For((_look.Kind != WeaponKind.Hammer) ? WeaponKind.Sword : WeaponKind.Hammer, melee: true, hands: true).WithWindup(_windupScale);
			Flame.Cutting = (_trueSize ? (Flame.Speed > 7f * Flame.Scale) : (sinceAttack >= strikeTiming.StrikeStart && sinceAttack <= strikeTiming.StrikeEnd + 0.03f));
			Flame.Pose(_item, _time);
			MeshInstance meshInstance = Flame.Arc();
			if (meshInstance != null)
			{
				overlay.Add(meshInstance);
			}
			float num = 0.16f + 0.05f * Fm.Sin(_time * 13.1f) + 0.1f * Flame.Heat;
			if (_armR.Flash >> 24 < (uint)(num * 255f))
			{
				_armR.Flash = HandGlow | ((uint)(num * 255f) << 24);
			}
		}

		public bool BuildHelm(List<MeshInstance> overlay, ViewState view, in HelmGrip g, uint skin, uint sleeve, float bodyHeight, float dt)
		{
			overlay.Clear();
			_skin = skin;
			_sleeve = sleeve;
			_bare = false;
			_paletteOwner = null;
			_eye = new Vec3(view.X, view.Y, view.Z);
			ViewAxes(view);
			_fovK = Fm.Clamp((float)Math.Tan(view.Fov * 0.5f), 0.4f, 2.5f);
			_s = Fm.Clamp(bodyHeight, 0.3f, 1.3f) * 1.2f;
			_haveLast = false;
			_equip = 1f;
			Vec3 a = g.Hub - _eye;
			float num = Vec3.Dot(a, _f);
			float value = Vec3.Dot(a, _r);
			if (num < 0.05f || Math.Abs(value) > num * 1.6f)
			{
				_gripL = (_gripR = -1);
				return false;
			}
			float num2 = ((_gripL >= 0 && dt > 0f) ? (Angle(g.Turn - _lastTurn) / dt) : 0f);
			_turnRate = ((_gripL >= 0) ? (_turnRate + (num2 - _turnRate) * Math.Min(1f, dt * 12f)) : 0f);
			_lastTurn = g.Turn;
			float lead = _turnRate * 0.16f;
			int num3 = Math.Max(3, g.Spokes);
			float step = MathF.PI * 2f / (float)num3;
			bool flag = Math.Abs(_turnRate) < 0.05f;
			Grip(ref _gripL, ref _regripL, ref _fromL, ref _atL, in g, num3, step, 0.79f, lead, flag && _regripR >= 1f, dt);
			Grip(ref _gripR, ref _regripR, ref _fromR, ref _atR, in g, num3, step, 2.36f, lead, flag && _regripL >= 1f, dt);
			PlaceArm(overlay, _armL, HandAt(_atL, Along(in g, Spot(in g, _gripL, step)), left: true), left: true, holding: true);
			PlaceArm(overlay, _armR, HandAt(_atR, Along(in g, Spot(in g, _gripR, step)) * -1f, left: false), left: false, holding: true);
			return true;
		}

		private static float Spot(in HelmGrip g, int k, float step)
		{
			return g.First + ((float)k + 0.5f) * step + g.Turn;
		}

		private static Vec3 Radial(in HelmGrip g, float a)
		{
			return g.Left * Fm.Cos(a) + g.Up * Fm.Sin(a);
		}

		private static Vec3 Along(in HelmGrip g, float a)
		{
			return g.Left * (0f - Fm.Sin(a)) + g.Up * Fm.Cos(a);
		}

		private void Grip(ref int held, ref float moving, ref Vec3 from, ref Vec3 at, in HelmGrip g, int n, float step, float prefer, float lead, bool settle, float dt)
		{
			int num = 0;
			float num2 = float.MaxValue;
			for (int i = 0; i < n; i++)
			{
				float num3 = Math.Abs(Angle(Spot(in g, i, step) + lead - prefer));
				if (num3 < num2)
				{
					num2 = num3;
					num = i;
				}
			}
			if (held < 0)
			{
				held = num;
				moving = 1f;
			}
			else if (held != num && moving >= 1f)
			{
				float num4 = Angle(Spot(in g, held, step) - prefer);
				bool flag = num4 * (MathF.PI / 2f - prefer) > 0f;
				if ((settle && Math.Abs(num4) > num2 + 0.1f) || Math.Abs(num4) > (flag ? 0.8f : 0.42f))
				{
					from = at;
					held = num;
					moving = 0f;
				}
			}
			Vec3 vec = g.Hub + Radial(in g, Spot(in g, held, step)) * g.Radius;
			moving = Math.Min(1f, moving + dt / 0.16f);
			float num5 = moving * moving * (3f - 2f * moving);
			at = ((moving >= 1f) ? vec : (from + (vec - from) * num5 + (g.Hub - _eye).Normalized() * (-0.03f * (float)Math.Sin((double)num5 * Math.PI))));
		}

		private static float Angle(float a)
		{
			while (a > MathF.PI)
			{
				a -= MathF.PI * 2f;
			}
			while (a < -MathF.PI)
			{
				a += MathF.PI * 2f;
			}
			return a;
		}

		private Hand HandAt(Vec3 world, Vec3 thumb, bool left)
		{
			Vec3 vec = ToView(world);
			Vec3 vec2 = new Vec3(left ? (-0.21f) : 0.21f, -0.06f, -0.34f);
			Vec3 vec3 = vec - vec2;
			if (Vec3.Dot(vec3, vec3) < 0.0001f)
			{
				vec3 = new Vec3(0f, 1f, 0f);
			}
			return new Hand
			{
				W = vec,
				D = vec3.Normalized(),
				T = ToViewDir(thumb),
				Show = true
			};
		}

		private Vec3 ToView(Vec3 world)
		{
			Vec3 vec = world - _eye;
			Vec3 a = vec - _u * (_pitch * Vec3.Dot(vec, _f));
			return new Vec3(Vec3.Dot(a, _r) / _fovK, Vec3.Dot(a, _f), Vec3.Dot(a, _u) / _fovK) * (1f / _s);
		}

		private Vec3 ToViewDir(Vec3 dir)
		{
			Vec3 a = dir - _u * (_pitch * Vec3.Dot(dir, _f));
			return new Vec3(Vec3.Dot(a, _r) / _fovK, Vec3.Dot(a, _f), Vec3.Dot(a, _u) / _fovK).Normalized();
		}

		private void BindShotgun(MeshModel m)
		{
			if (m != _sgBound)
			{
				_sgBound = m;
				_sgPumpPart = m.PartIndex("pump");
				_sgBoltPart = m.PartIndex("bolt");
				_sgTrigger = m.PartIndex("trigger");
				_sgMuzzle = InfoVec(m, "muzzle", _sgMuzzle);
				_sgPort = InfoVec(m, "port", _sgPort);
				_sgPortDir = InfoVec(m, "port_dir", _sgPortDir).Normalized();
				_sgPumpGrip = InfoVec(m, "pump_grip", _sgPumpGrip);
				_sgLoad = InfoVec(m, "load_port", _sgLoad);
				_sgTravel = m.InfoFloat("pump_travel", _sgTravel);
			}
		}

		public static float ShotgunKick(float t)
		{
			if (t < 0f || t > 0.6f)
			{
				return 0f;
			}
			if (t < 0.045f)
			{
				return Out(t / 0.045f);
			}
			float num = t - 0.045f;
			return Fm.Exp((0f - num) * 9f) * Fm.Cos(num * 11f);
		}

		private void ShotgunPose(in HandsInput input, float dt, ref Hand right, ref Hand left)
		{
			ShotgunState shotgun = input.Shotgun;
			Vec3 vec = SgGrip;
			Vec3 y = SgDir.Normalized();
			Vec3 x = Vec3.Cross(y, new Vec3(0f, 0f, 1f)).Normalized();
			Vec3 z = Vec3.Cross(x, y);
			Turn(ref x, ref y, ref z, y, SgCant);
			float num = ((input.Run > 0.05f) ? (input.Run * input.Stride) : 0f);
			bool flag = shotgun != null && (shotgun.State != ShotgunState.Phase.Ready || shotgun.SinceShot < 0.4f);
			_sgCarry = Fm.MoveTowards(_sgCarry, flag ? 0f : Fm.Clamp01(num * 1.4f), dt / (flag ? 0.12f : 0.3f));
			float num2 = Ease(_sgCarry);
			if (num2 > 0f)
			{
				vec += new Vec3(-0.012f, -0.012f, -0.018f) * num2;
				Turn(ref x, ref y, ref z, new Vec3(0f, 0f, 1f), 0.3f * num2);
				Turn(ref x, ref y, ref z, x, -0.14f * num2);
				Turn(ref x, ref y, ref z, y, -0.42f * num2);
			}
			if (shotgun != null)
			{
				_preKickShift = vec;
				_preX = x;
				_preY = y;
				_preZ = z;
				float num3 = ShotgunKick(KickTime(shotgun));
				if (num3 != 0f)
				{
					vec = vec - y * (0.06f * num3) + z * (0.014f * num3) + x * (0.006f * num3);
					Turn(ref x, ref y, ref z, x, 0.3f * num3);
					Turn(ref x, ref y, ref z, y, 0.07f * num3);
					Turn(ref x, ref y, ref z, new Vec3(0f, 0f, 1f), 0.035f * num3);
				}
				_preKickShift = vec - _preKickShift;
				_sgPump = shotgun.Pump;
				if (_sgPump > 0f)
				{
					vec = vec - y * (0.012f * _sgPump) + x * (-0.006f * _sgPump);
					Turn(ref x, ref y, ref z, x, -0.05f * _sgPump);
					Turn(ref x, ref y, ref z, y, 0.06f * _sgPump);
				}
				if (shotgun.State == ShotgunState.Phase.Cycling && shotgun.Time > 0.47f)
				{
					float num4 = Fm.Clamp01((shotgun.Time - 0.47f) / 0.15f);
					vec += y * (0.008f * Fm.Sin(num4 * MathF.PI) * (1f - num4));
				}
				float num5 = Ease(shotgun.Roll);
				if (num5 > 0f)
				{
					vec += new Vec3(-0.05f, -0.012f, 0.048f) * num5;
					Turn(ref x, ref y, ref z, new Vec3(0f, 0f, 1f), 0.12f * num5);
					Turn(ref x, ref y, ref z, x, 0.16f * num5);
					Turn(ref x, ref y, ref z, y, 0.72f * num5);
					if (shotgun.State == ShotgunState.Phase.Loading)
					{
						float num6 = shotgun.Time - 0.3f;
						if (num6 > -0.04f && num6 < 0.12f)
						{
							vec += z * (-0.005f * Fm.Sin(Fm.Clamp01((num6 + 0.04f) / 0.16f) * MathF.PI));
						}
					}
				}
			}
			if (input.Swing != Swing.Attack && input.SinceAttack < 0.45f)
			{
				float num7 = ((input.SinceAttack < 0.12f) ? Out(input.SinceAttack / 0.12f) : (1f - Ease((input.SinceAttack - 0.12f) / 0.33f)));
				vec = vec + y * (0.07f * num7) + new Vec3(0f, 0f, -0.02f * num7);
			}
			_gx = x;
			_gy = y;
			_gz = z;
			right = new Hand
			{
				W = vec,
				D = SgToView(SgRightArm).Normalized(),
				T = SgToView(SgRightThumb).Normalized(),
				Show = true
			};
			left.Show = true;
		}

		private float KickTime(ShotgunState sg)
		{
			if (sg.Shots != _kickShot)
			{
				_kickShot = sg.Shots;
				_kickAt = _time;
				if (sg.SinceShot < 0.3f)
				{
					return 0f;
				}
				_kickAt = _time - 1f;
			}
			float num = _time - _kickAt;
			if (!(num <= 0f))
			{
				return Math.Min(sg.SinceShot, num + 1f / 60f);
			}
			return 0f;
		}

		private Vec3 SgToView(Vec3 g)
		{
			return _gx * g.X + _gy * g.Y + _gz * g.Z;
		}

		private static void Turn(ref Vec3 x, ref Vec3 y, ref Vec3 z, Vec3 axis, float angle)
		{
			if (angle != 0f)
			{
				axis = axis.Normalized();
				x = Vec3.Rotate(x, axis, angle);
				y = Vec3.Rotate(y, axis, angle);
				z = Vec3.Rotate(z, axis, angle);
			}
		}

		private void ShotgunHands(ref Hand right, ref Hand left, ShotgunState sg)
		{
			_gunOrigin = right.W;
			Vec3 w = _gunOrigin + SgToView(_sgPumpGrip - new Vec3(0f, _sgTravel * _sgPump, 0f));
			Hand hand = (left = new Hand
			{
				W = w,
				D = SgToView(SgLeftArm).Normalized(),
				T = SgToView(SgLeftThumb).Normalized(),
				Show = true
			});
			_shellShown = false;
			if (sg == null || !sg.Reloading)
			{
				return;
			}
			Vec3 vec = _gunOrigin + SgToView(_sgLoad);
			Vec3 w2 = vec - _gz * 0.05f - _gy * 0.022f;
			Hand hand2 = new Hand
			{
				W = vec - _gz * 0.014f - _gy * 0.024f,
				D = (SgToView(new Vec3(0.2f, 0.35f, 0.92f)) + new Vec3(0.3f, 0f, 0f)).Normalized(),
				T = _gy,
				Show = true
			};
			Hand hand3 = hand2;
			hand3.W = w2;
			Hand hand4 = new Hand
			{
				W = SgPocket,
				D = new Vec3(0.3f, 0.45f, 0.85f).Normalized(),
				T = new Vec3(0.2f, 1f, 0.1f).Normalized(),
				Show = true
			};
			float time = sg.Time;
			switch (sg.State)
			{
			case ShotgunState.Phase.ReloadIn:
			{
				float k = Ease(Fm.Clamp01((time - 0.05f) / 0.25f));
				left = MixHand(hand, hand4, k);
				break;
			}
			case ShotgunState.Phase.Loading:
			{
				Hand a2 = ((sg.Inserted == 0) ? hand4 : hand2);
				if (time < 0.12f)
				{
					left = MixHand(a2, hand4, Ease(time / 0.12f));
				}
				else if (time < 0.26f)
				{
					left = MixHand(hand4, hand3, Ease((time - 0.12f) / 0.14f));
					_shellShown = true;
				}
				else if (time < 0.3f)
				{
					left = MixHand(hand3, hand2, Ease((time - 0.26f) / 0.04000002f));
					_shellShown = true;
				}
				else
				{
					left = hand2;
				}
				if (_shellShown)
				{
					_shellAt = left.W + _gz * 0.02f + _gy * 0.03f + _gx * 0.004f;
				}
				break;
			}
			case ShotgunState.Phase.ReloadOut:
			{
				Hand a = ((sg.Inserted > 0) ? hand2 : hand4);
				left = MixHand(a, hand, Ease(time / 0.28f));
				break;
			}
			}
		}

		private static Hand MixHand(Hand a, Hand b, float k)
		{
			return new Hand
			{
				W = Mix(a.W, b.W, k),
				D = MixDir(a.D, b.D, k),
				T = MixDir(a.T, b.T, k),
				Show = true
			};
		}

		private void PlaceShotgun(List<MeshInstance> overlay, List<VoxelObject> voxels, Hand right, ShotgunState sg)
		{
			MeshModel meshModel = _sgModel ?? ViewMeshes.Item(_look);
			if (_sgModel != null)
			{
				BindShotgun(_sgModel);
			}
			_item.Model = meshModel;
			_item.Palette = null;
			int num = meshModel.Parts.Length;
			if (_item.PartRotation == null || _item.PartRotation.Length != num)
			{
				_item.PartRotation = new Mat3[num];
			}
			if (_item.PartOffset == null || _item.PartOffset.Length != num)
			{
				_item.PartOffset = new Vec3[num];
			}
			for (int i = 0; i < num; i++)
			{
				_item.PartRotation[i] = Mat3.Identity;
				_item.PartOffset[i] = new Vec3(0f, 0f, 0f);
			}
			bool num2 = _sgModel != null;
			float num3 = sg?.SinceShot ?? 99f;
			if (num2)
			{
				Vec3 vec = new Vec3(0f, (0f - _sgTravel) * _sgPump, 0f);
				if (_sgPumpPart >= 0)
				{
					_item.PartOffset[_sgPumpPart] = vec;
				}
				if (_sgBoltPart >= 0)
				{
					_item.PartOffset[_sgBoltPart] = vec;
				}
				if (_sgTrigger >= 0)
				{
					_item.PartRotation[_sgTrigger] = Mat3.RotX(-0.38f * Fm.Max(0f, 1f - num3 / 0.12f));
				}
			}
			Vec3 gunOrigin = _gunOrigin;
			Place(_item, gunOrigin, _gx, _gy, _gz);
			_item.Emissive = false;
			float num4 = Fm.Max(0f, 1f - num3 / 0.07f);
			_item.Flash = ((num4 > 0f) ? ((TerrainCache.Rgb(255, 196, 120) & 0xFFFFFF) | ((uint)(num4 * 90f) << 24)) : 0u);
			overlay.Add(_item);
			_armR.Flash = (_armL.Flash = _item.Flash);
			Vec3 vec2 = gunOrigin + SgToView(_sgMuzzle);
			ShotgunShown = true;
			ShotgunMuzzle = World(vec2);
			ShotgunMuzzleDir = (World(vec2 + _gy * 0.2f) - ShotgunMuzzle).Normalized();
			ShotgunPort = World(gunOrigin + SgToView(_sgPort));
			ShotgunPortDir = WorldDir(SgToView(_sgPortDir)).Normalized();
			ShotgunUp = WorldDir(_gz).Normalized();
			ShotgunBack = ShotgunMuzzleDir * -1f;
			if (_shellShown && _shellModel != null)
			{
				_shellInHand.Model = _shellModel;
				_shellInHand.Palette = null;
				Place(_shellInHand, _shellAt, _gx, _gy, _gz);
				overlay.Add(_shellInHand);
			}
			if (sg != null)
			{
				Vec3 viewPoint = gunOrigin - _preKickShift + _preX * _sgMuzzle.X + _preY * _sgMuzzle.Y + _preZ * _sgMuzzle.Z;
				MuzzleFlash(voxels, vec2, _gy, sg);
				Vec3 a = World(viewPoint) - _eye;
				float num5 = Vec3.Dot(a, _fl);
				ShotgunMuzzleFromEye = new Vec3(Vec3.Dot(a, _r), num5, a.Z - _slope * num5);
				ShotgunMuzzleTime = _time;
			}
		}

		private void MuzzleFlash(List<VoxelObject> voxels, Vec3 muzzle, Vec3 along, ShotgunState sg)
		{
			if (sg.Shots != _flashShot)
			{
				_flashShot = sg.Shots;
				_flashFirstShown = false;
				_flashVariant = _sgRng.Next(3);
				_flashRoll = (float)_sgRng.NextDouble() * 6.2832f;
				if (sg.SinceShot > 0.3f)
				{
					_flashFirstShown = true;
				}
			}
			float sinceShot = sg.SinceShot;
			int num = (_flashFirstShown ? ((int)(sinceShot / 0.032f)) : 0);
			_flashFirstShown = true;
			if (num >= 3)
			{
				return;
			}
			SpriteImage img = ShotgunFx.FlashStar(num, _flashVariant);
			float num2;
			switch (num)
			{
			default:
				num2 = 0.11f;
				break;
			case 1:
				num2 = 0.155f;
				break;
			case 0:
				num2 = 0.2f;
				break;
			}
			float width = num2;
			Vec3 u = Vec3.Rotate(new Vec3(1f, 0f, 0f), new Vec3(0f, 1f, 0f), _flashRoll);
			Vec3 v = Vec3.Rotate(new Vec3(0f, 0f, 1f), new Vec3(0f, 1f, 0f), _flashRoll);
			Vec3 vec = muzzle + along * (0.03f + 0.02f * (float)num);
			AddFlashSprite(voxels, img, vec, u, v, width, 0.5f, 0.5f);
			SpriteImage spriteImage = ShotgunFx.FlashTongue(num, _flashVariant);
			Vec3 b = (vec * -1f).Normalized();
			Vec3 vec2 = Vec3.Cross(along, b);
			if (!(Vec3.Dot(vec2, vec2) < 0.0001f))
			{
				vec2 = vec2.Normalized();
				float num3;
				switch (num)
				{
				default:
					num3 = 0.13f;
					break;
				case 1:
					num3 = 0.22f;
					break;
				case 0:
					num3 = 0.3f;
					break;
				}
				float width2 = num3;
				AddFlashSprite(voxels, spriteImage, muzzle + along * 0.01f, along, vec2, width2, 0f, 0.5f, (float)spriteImage.Height / (float)spriteImage.Width);
			}
		}

		private void AddFlashSprite(List<VoxelObject> voxels, SpriteImage img, Vec3 at, Vec3 u, Vec3 v, float width, float px, float py, float aspect = 1f)
		{
			Vec3 vec = Shear(WorldDir(u) * _s);
			Vec3 vec2 = Shear(WorldDir(v) * _s);
			float num = (float)Math.Sqrt(Vec3.Dot(vec, vec));
			float num2 = (float)Math.Sqrt(Vec3.Dot(vec2, vec2));
			if (!(num < 1E-06f) && !(num2 < 1E-06f))
			{
				Vec3 vec3 = vec * (1f / num);
				Vec3 vec4 = vec2 * (1f / num2);
				Vec3 vec5 = Vec3.Cross(vec3, vec4);
				if (!(Vec3.Dot(vec5, vec5) < 1E-06f))
				{
					vec5 = vec5.Normalized();
					float num3 = width * num / (float)img.Width;
					float num4 = width * aspect * num2 / (float)img.Height;
					Vec3 origin = World(at) - vec3 * (px * (float)img.Width * num3) - vec4 * (py * (float)img.Height * num4);
					voxels.Add(new VoxelObject
					{
						Shape = img.SlabShape(1),
						Overlay = true,
						Emissive = true,
						NoShadow = true,
						Tag = -1,
						U = vec3,
						V = vec4,
						N = vec5,
						SizeU = num3,
						SizeV = num4,
						SizeN = Math.Min(num3, num4) * 0.5f,
						Origin = origin
					});
				}
			}
		}

		private static bool TwoHanded(WeaponKind k)
		{
			if (k != WeaponKind.Bow && k != WeaponKind.Spear && k != WeaponKind.Staff && k != WeaponKind.Gun)
			{
				return k == WeaponKind.Shotgun;
			}
			return true;
		}

		private void TorchBegin(in HandsInput input, WeaponKind kind, float dt, ref Hand right)
		{
			TorchInMain = input.TorchMain && TorchFire.IsModel(_heldModel);
			_torchLeftOn = input.TorchLeft && input.TorchModel != null && !TwoHanded(kind);
			_torchUp = Fm.MoveTowards(_torchUp, _torchLeftOn ? 1f : 0f, dt / (_torchLeftOn ? 0.4f : 0.25f));
			if (!_torchLeftOn && kind != WeaponKind.None && TwoHanded(kind))
			{
				_torchUp = 0f;
			}
			_torchReach = Fm.MoveTowards(_torchReach, input.TorchReach ? 1f : 0f, dt / (input.TorchReach ? 0.2f : 0.3f));
			SyncFire(Torch, input.TorchLit, input.TorchSputter);
			SyncFire(TorchLeft, input.TorchLit, input.TorchSputter);
			if (_torchUp > 0f)
			{
				_leftPunch = false;
			}
			if (TorchInMain && !input.Tracked)
			{
				right = TorchReadyR;
			}
		}

		private void SyncFire(TorchFire f, bool lit, float sputter)
		{
			f.Sputter = sputter;
			if (lit == f.Lit)
			{
				return;
			}
			bool flag = f.PosedAt >= 0f && _time - f.PosedAt < 0.2f;
			if (lit)
			{
				if (flag)
				{
					f.Catch();
					return;
				}
				f.Lit = true;
				f.Grown = 1f;
			}
			else if (flag)
			{
				f.Douse();
			}
			else
			{
				f.Lit = false;
			}
		}

		private void TorchHold(in HandsInput input, ref Hand right, ref Hand left)
		{
			if (input.Tracked)
			{
				return;
			}
			float num = Ease(_torchReach);
			if (TorchInMain && num > 0f)
			{
				Hand torchOutR = TorchOutR;
				right.W = Mix(right.W, torchOutR.W, num);
				right.D = MixDir(right.D, torchOutR.D, num);
				right.T = MixDir(right.T, torchOutR.T, num);
			}
			if (!(_torchUp <= 0f))
			{
				Hand torchSide = TorchSide;
				if (!TorchInMain && num > 0f)
				{
					Hand torchOutL = TorchOutL;
					torchSide.W = Mix(torchSide.W, torchOutL.W, num);
					torchSide.D = MixDir(torchSide.D, torchOutL.D, num);
					torchSide.T = MixDir(torchSide.T, torchOutL.T, num);
				}
				float num2 = Ease(_torchUp);
				torchSide.W += new Vec3(-0.04f, -0.06f, -0.34f) * (1f - num2);
				left = torchSide;
			}
		}

		private void PlaceTorchLeft(List<MeshInstance> overlay, Hand left, MeshModel model)
		{
			if (model != null)
			{
				_torchL.Model = model;
				_torchL.Palette = null;
				Vec3 vec = left.T.Normalized();
				Vec3 vec2 = left.D - vec * Vec3.Dot(left.D, vec);
				vec2 = ((Vec3.Dot(vec2, vec2) > 0.0001f) ? vec2.Normalized() : new Vec3(0f, 1f, 0f));
				Vec3 x = Vec3.Cross(vec2, vec);
				Place(_torchL, left.W, x, vec2, vec);
				_torchL.Emissive = false;
				TorchLeft.Pose(_torchL, _time);
				overlay.Add(_torchL);
				TorchGlow(ref _armL.Flash, TorchLeft);
			}
		}

		private void PoseTorch(MeshModel model)
		{
			if (!TorchFire.IsModel(model))
			{
				Torch.Shown = false;
				return;
			}
			Torch.Pose(_item, _time);
			TorchGlow(ref _armR.Flash, Torch);
		}

		private void TorchGlow(ref uint flash, TorchFire f)
		{
			if (f.Lit)
			{
				uint num = (uint)(Fm.Clamp01((0.18f + 0.05f * Fm.Sin(_time * 12.3f)) * (0.4f + 0.6f * f.Grown) * (1f - 0.5f * f.Sputter)) * 255f);
				if (flash >> 24 < num)
				{
					flash = TorchArmGlow | (num << 24);
				}
			}
		}

		private TrackMap MapOf(in HandTrack t, Vec3 anchor, Vec3 follow)
		{
			TrackMap result = new TrackMap
			{
				G = ToView(t.At),
				Anchor = anchor
			};
			Vec3 vec = ToViewDir(t.Forward).Normalized();
			Vec3 vec2 = ToViewDir(t.Up);
			Vec3 vec3 = vec2 - vec * Vec3.Dot(vec2, vec);
			vec3 = ((Vec3.Dot(vec3, vec3) > 1E-06f) ? vec3.Normalized() : new Vec3(0f, 0f, 1f));
			result.F = vec;
			result.U = vec3;
			result.R = Vec3.Cross(vec, vec3);
			follow = follow.Normalized();
			Vec3 b = new Vec3(0f, 1f, 0f);
			Vec3 vec4 = Vec3.Cross(follow, b);
			float num = (float)Math.Sqrt(Vec3.Dot(vec4, vec4));
			float num2 = Vec3.Dot(follow, b);
			if (num > 0.0001f)
			{
				result.QAxis = vec4 * (1f / num);
				result.QAngle = (float)Math.Atan2(num, num2);
			}
			return result;
		}

		private static Vec3 FollowAxis(WeaponKind kind, in Hand rest)
		{
			switch (kind)
			{
			case WeaponKind.Spear:
				return rest.T;
			case WeaponKind.Gun:
			{
				Vec3 vec = rest.T.Normalized();
				Vec3 vec2 = rest.D - vec * Vec3.Dot(rest.D, vec);
				if (!(Vec3.Dot(vec2, vec2) > 0.0001f))
				{
					return rest.D;
				}
				return vec2;
			}
			default:
				return rest.D;
			}
		}

		private Hand FreeLeft(in HandsInput input, WeaponKind kind, Hand fallback)
		{
			if (!input.TrackL.Valid)
			{
				return fallback;
			}
			Hand h = ((kind == WeaponKind.None) ? Ready(WeaponKind.None, right: false) : OffHand);
			return MapOf(in input.TrackL, h.W, h.D).Hand(h);
		}

		private void TrackedPose(in HandsInput input, WeaponKind kind, ref Hand right, ref Hand left)
		{
			Hand rest = ((kind == WeaponKind.Shotgun) ? new Hand
			{
				W = SgGrip,
				D = SgDir,
				T = new Vec3(0f, 0f, 1f),
				Show = true
			} : Ready(kind, right: true));
			Vec3 follow = ((kind == WeaponKind.Shotgun) ? SgDir : FollowAxis(kind, in rest));
			TrackMap trackMap = MapOf(in input.TrackR, rest.W, follow);
			Hand h = left;
			right = trackMap.Hand(right);
			if (kind == WeaponKind.Shotgun)
			{
				_gx = trackMap.Dir(_gx);
				_gy = trackMap.Dir(_gy);
				_gz = trackMap.Dir(_gz);
			}
			Hand hand = (_trackFreeLeft = FreeLeft(in input, kind, left));
			_trackFreeLeftSet = true;
			if (kind == WeaponKind.Spear || kind == WeaponKind.Gun)
			{
				Hand hand2 = trackMap.Hand(h);
				TrackLeftHoldWorld = World(hand2.W);
				left = Hold(hand2, hand, input.TrackL.Valid);
			}
			else if (kind != WeaponKind.Shotgun)
			{
				left = hand;
				_leftOn = false;
			}
		}

		private Hand Hold(Hand on, Hand free, bool tracked)
		{
			if (!tracked)
			{
				_leftOn = true;
				return on;
			}
			Vec3 vec = on.W - free.W;
			float num = (float)Math.Sqrt(Vec3.Dot(vec, vec));
			_leftOn = (_leftOn ? (num < 0.11f) : (num < 0.07f));
			if (!_leftOn)
			{
				return free;
			}
			return on;
		}

		private void TrackedLeftOnGun(ref Hand left, ShotgunState sg, bool leftTracked)
		{
			if (_trackFreeLeftSet)
			{
				if (sg != null && sg.Reloading)
				{
					_leftOn = true;
					return;
				}
				TrackLeftHoldWorld = World(left.W);
				left = Hold(left, _trackFreeLeft, leftTracked);
			}
		}

		private void TrackedBare(List<MeshInstance> overlay, in HandsInput input)
		{
			Hand h = Ready(WeaponKind.None, right: true);
			Hand hand = Ready(WeaponKind.None, right: false);
			Hand h2 = MapOf(in input.TrackR, h.W, h.D).Hand(h);
			Hand h3 = (input.TrackL.Valid ? MapOf(in input.TrackL, hand.W, hand.D).Hand(hand) : hand);
			PlaceArm(overlay, _armR, h2, left: false);
			PlaceArm(overlay, _armL, h3, left: true);
			TrackAimValid = false;
			HandStrike strikeR = (StrikeL = default(HandStrike));
			StrikeR = strikeR;
			RightFistWorld = World(h2.W);
			LeftFistWorld = World(h3.W);
		}

		private void TrackedBow(in HandsInput input, BowState bow, ref Hand right, ref Hand left)
		{
			Hand hand = Ready(WeaponKind.Bow, right: false);
			left = (input.TrackL.Valid ? MapOf(in input.TrackL, hand.W, hand.D).Hand(hand) : hand);
			Hand h = Ready(WeaponKind.None, right: true);
			Hand hand2 = MapOf(in input.TrackR, h.W, h.D).Hand(h);
			Vec3 vec = left.T.Normalized();
			Vec3 vec2 = left.D - vec * Vec3.Dot(left.D, vec);
			vec2 = ((Vec3.Dot(vec2, vec2) > 0.0001f) ? vec2.Normalized() : new Vec3(0f, 1f, 0f));
			Vec3 vec3 = vec;
			BowState.Phase state = bow.State;
			bool flag = input.BowOnString && (state == BowState.Phase.Drawing || state == BowState.Phase.Held);
			Vec3 vec4 = left.W + vec3 * 0.034f;
			Vec3 vec5 = vec4 + vec2 * -0.105f;
			TrackNockNear = !flag && NearSegment(hand2.W, vec5, vec5 - vec2 * 0.27f) < 0.13f;
			TrackNockWorld = World(vec5);
			_nockY = -0.105f;
			if (flag)
			{
				Vec3 vec6 = (vec4 - hand2.W).Normalized();
				Vec3 vec7 = Flat(vec, vec6);
				Vec3 vec8 = Vec3.Cross(vec6, vec7);
				Vec3 vec9 = hand2.W - vec8 * 0.012f + vec7 * 0.016f;
				vec2 = (vec4 - vec9).Normalized();
				vec3 = Flat(vec, vec2);
				vec4 = left.W + vec3 * 0.034f;
				Vec3 vec10 = vec4 - vec9;
				float num = (float)Math.Sqrt(Vec3.Dot(vec10, vec10));
				_nockY = 0f - Fm.Clamp(num, 0.105f, 0.4425f);
				TrackPull = Fm.Clamp((num + -0.105f) / 0.27f, 0f, 1.25f);
			}
			else
			{
				vec3 = Flat(vec, vec2);
			}
			_bowY = vec2;
			_bowZ = vec3;
			_bowX = Vec3.Cross(vec2, vec3);
			float num2 = (flag ? Fm.Clamp01(TrackPull) : 0f);
			if (flag)
			{
				Vec3 vec11 = new Vec3(1f, 0f, 0f);
				Vec3 aimArrow = AimArrow;
				Vec3 b = Flat(new Vec3(Fm.Sin(-0.5f), 0f, Fm.Cos(-0.5f)), aimArrow);
				vec11 = Vec3.Cross(aimArrow, b);
				Vec3 a = MixDir(StringRestD, StringDrawD, num2);
				Vec3 stringT = StringT;
				right = new Hand
				{
					W = hand2.W,
					D = (_bowX * Vec3.Dot(a, vec11) + _bowY * Vec3.Dot(a, aimArrow) + _bowZ * Vec3.Dot(a, b)).Normalized(),
					T = (_bowX * Vec3.Dot(stringT, vec11) + _bowY * Vec3.Dot(stringT, aimArrow) + _bowZ * Vec3.Dot(stringT, b)).Normalized(),
					Show = true
				};
			}
			else
			{
				right = hand2;
			}
			_flex = num2;
			_vibrate = 0f;
			float time = bow.Time;
			if (state == BowState.Phase.Loosed && time < 0.35f)
			{
				_vibrate = 0.018f * Fm.Exp((0f - time) * 11f) * Fm.Sin(time * 95f);
			}
			_arrowOn = bow.ArrowNocked;
			_leaving = state == BowState.Phase.Loosed && bow.Fired;
			_shotDraw = bow.LastShotDraw;
			_heldArrow = false;
			_onString = flag;
			_raise = 1f;
		}

		private static bool ThroughEyes(in Hand h)
		{
			return NearSegment(new Vec3(0f, 0f, 0f), h.W, h.W - h.D.Normalized() * 0.32f) < 0.09f;
		}

		private static Vec3 Flat(Vec3 v, Vec3 axis)
		{
			Vec3 vec = v - axis * Vec3.Dot(v, axis);
			if (Vec3.Dot(vec, vec) > 1E-06f)
			{
				return vec.Normalized();
			}
			vec = new Vec3(0f, 0f, 1f) - axis * axis.Z;
			if (!(Vec3.Dot(vec, vec) > 1E-06f))
			{
				return new Vec3(1f, 0f, 0f);
			}
			return vec.Normalized();
		}

		private static float NearSegment(Vec3 p, Vec3 a, Vec3 b)
		{
			Vec3 vec = b - a;
			float num = Vec3.Dot(vec, vec);
			float num2 = ((num > 1E-08f) ? Fm.Clamp01(Vec3.Dot(p - a, vec) / num) : 0f);
			Vec3 vec2 = p - (a + vec * num2);
			return (float)Math.Sqrt(Vec3.Dot(vec2, vec2));
		}

		private void TrackedAims(in HandsInput input, in Hand right, in Hand left, WeaponKind kind, bool bow, bool gestureRight, bool armed)
		{
			bool flag = bow || kind == WeaponKind.Gun || kind == WeaponKind.Shotgun;
			if (armed && !flag && _item.Model != null)
			{
				Vec3 vec = right.T.Normalized();
				float num = Fm.Max(0.12f, _item.Model.Max.Z);
				StrikeR = new HandStrike
				{
					Valid = true,
					A = World(right.W + vec * 0.05f),
					B = World(right.W + vec * num),
					Radius = 0.035f * _s
				};
			}
			else if (!armed)
			{
				StrikeR = new HandStrike
				{
					Valid = true,
					Fist = true,
					A = World(right.W),
					B = World(right.W),
					Radius = 0.06f * _s
				};
			}
			else
			{
				StrikeR = default(HandStrike);
			}
			bool flag2 = input.TrackL.Valid && !bow && !_leftOn && left.Show;
			StrikeL = (flag2 ? new HandStrike
			{
				Valid = true,
				Fist = true,
				A = World(left.W),
				B = World(left.W),
				Radius = 0.06f * _s
			} : default(HandStrike));
			RightFistWorld = World(right.W);
			LeftFistWorld = World(left.W);
			RightForearmWorld = WorldDir(right.D).Normalized();
			Vec3 vec2 = input.TrackR.Forward.Normalized();
			if (bow)
			{
				Vec3 vec3 = _bowLeft.W + _bowZ * 0.034f;
				TrackAimFrom = World(vec3 + _bowY * (_nockY + 0.47f));
				TrackAimDir = WorldDir(_bowY).Normalized();
			}
			else
			{
				TrackAimFrom = (ShotgunShown ? ShotgunMuzzle : (BlasterShown ? BlasterMuzzle : (World(right.W) + vec2 * (0.06f * _s))));
				TrackAimDir = vec2;
			}
			TrackAimValid = true;
			TrackFreeDir = ((gestureRight || !input.TrackL.Valid) ? input.TrackR : input.TrackL).Forward.Normalized();
			TrackFreeFrom = World((gestureRight || !input.TrackL.Valid) ? right.W : left.W) + TrackFreeDir * (0.05f * _s);
		}
	}
}
