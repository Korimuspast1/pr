using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal static class People
	{
		private enum Stage
		{
			Child,
			Adult,
			Elder
		}

		private sealed class Model
		{
			public object Space;

			public long Id;

			public VoxelShape[] Frames;

			public Stage Stage;

			public bool Female;

			public int Seen;

			public Humanoid.Palette Palette;

			public MeshInstance Mesh;

			public MeshModel BaseModel;

			public int Expecting;

			public float Phase;

			public WalkCycle.Walker Walk = new WalkCycle.Walker();

			public float WalkHeading = float.NaN;

			public Standing Standing;

			public MeshInstance Weapon;

			public uint[] FlagPalette;

			public string WeaponId;

			public WeaponLook Look;

			public int WeaponDetail;

			public int WeaponChecked = -1000;

			public float Stance;

			public float Face = float.NaN;

			public float FaceTurn;

			public double LastAttack;

			public float Side = 1f;

			public MeshInstance BowString;

			public MeshInstance Nocked;

			public float AimPitch = 0.1f;

			public int Drawn = -10;

			public bool GlideOk;

			public List<MeshInstance> GlideParts;

			public List<Vec3> GlideOffsets;

			public float GlideLift;

			public float GlideBody;

			public bool Fallen;

			public Vector2 Away;

			public PersonRig Rig;

			public double WorkHit;

			public float WorkTimer;

			public readonly MeshInstance[] Armor = new MeshInstance[3];

			public int ArmorShown;

			public string Helmet;

			public string Chest;

			public string Boots;

			public int ArmorChecked = -1000;

			public PersonMemo Memo;

			public LookMemo Looks;

			public Held Hold;

			public bool Crowd;

			public int DressedAt;

			public float CrowdBlend;

			public MeshInstance CrowdGhost;

			public MeshModel DressedModel;

			public LeanRead Lean;

			public bool CarryPlaced;

			public Vec3 CarryPos;

			public Mat3 CarryBasis;
		}

		private sealed class Held
		{
			public MeshModel Model;

			public float Debt;

			public float RestLift;

			public float RestHeading = float.NaN;

			public float Scale;

			public float Seated;

			public float Attack;
		}

		private sealed class Standing
		{
			public PersonRig.Fidget Kind;

			public float T;

			public float Length;

			public float Side = 1f;

			public float Amount;

			public float Still;

			public float Wait = 1f;

			public uint Rng;
		}

		internal sealed class LeanRead
		{
			public int Checked;

			public MeshModel Model;

			public bool Child;

			public bool Swimming;

			public WeaponKind Kind;

			public BaseSimObject Foe;

			public string Tool;

			public string Load;

			public Vector2 WorkAt;

			public bool HasWork;

			public bool Coasts;

			public int Due;

			public float X;

			public float Y;

			public float Z;

			public float Body;

			public float Scale;

			public int Variant;
		}

		private sealed class LookMemo
		{
			public Wardrobe.Look Look;

			public Wardrobe.Who Who;

			public bool Chosen;

			public int Checked = -100000;

			public string Family;

			public string ChildFamily;

			public MeshModel PartsModel;

			public Wardrobe.Parts Parts;

			public MeshModel MaskModel;

			public ulong Mask;

			public Wardrobe.Paint Paint;

			public uint Kingdom;

			public PersonRig.Aid Aid;

			public Vec3 AidTip;

			public bool AidHeld;

			public MeshInstance AidMesh;
		}

		private sealed class Worn
		{
			public readonly RegaliaDress.Wearer W = new RegaliaDress.Wearer();

			public long Id = long.MinValue;

			public int Frame = -10;

			public int Drawn = -10;

			public RoleInfo Role;

			public GearLook Gear;

			public int GearSerial = -1;

			public bool GearBusy;

			public float LastHeading = float.NaN;

			public float Clock;
		}

		private sealed class HairFix
		{
			public long Id = long.MinValue;

			public uint Hair;

			public bool Done;

			public int Tries;

			public int NextAt;
		}

		private sealed class Fallen
		{
			public long Id;

			public readonly MeshInstance[] Sets = new MeshInstance[6];

			public readonly RegaliaDress.Bound[] Bounds = new RegaliaDress.Bound[6];

			public float Upper;

			public float Hem;

			public float Side;

			public float RobeUpper;

			public float RobeHem;

			public float RobeSide;

			public int Drawn = -10;
		}

		private sealed class GunAt
		{
			public Vec3 Muzzle;

			public Vec3 Dir;

			public Vec3 Port;

			public Vec3 PortDir;

			public Vec3 Up;

			public float Scale;

			public int Frame;

			public float FiredAt = -100f;

			public bool Ejected = true;
		}

		private sealed class PersonMemo
		{
			public ArmorMeshes.DressMemo Dressed;

			public HairMemo Hair;

			public bool FamilySet;

			public ActorAsset FamilyAsset;

			public bool FamilyFemale;

			public string FamilySkin;

			public string Family;

			public Probe Tried;
		}

		private struct HairMemo
		{
			public bool HairSet;

			public MeshModel HairModel;

			public string HairHelmet;

			public bool HairHidden;

			public ulong HairMask;

			public bool ShoesSet;

			public string ShoesBoots;

			public uint Shoes;
		}

		private sealed class Probe
		{
			public ArmorMeshes.DressMemo Dressed;

			public HairMemo Hair;

			public readonly MeshInstance[] Into = new MeshInstance[3];
		}

		private sealed class InWater
		{
			public float Swim;

			public float Crawl;

			public float Wade;
		}

		private sealed class HeldTorch
		{
			public readonly MeshInstance Mesh = new MeshInstance();

			public readonly TorchFire Fire = new TorchFire
			{
				Lit = true,
				Grown = 1f
			};
		}

		internal struct HeldGlow
		{
			public Vec3 At;

			public uint Colour;

			public bool Lava;

			public float Time;
		}

		internal static Carried HostCarried;

		private const float CarryRange = 45f;

		private static readonly Dictionary<string, string> CarryKeys = new Dictionary<string, string>();

		internal static readonly int[] CarriedDrawn = new int[5];

		private static int _carriedFrame = -1;

		private static float _crowdLine;

		private static float _crowdPixel;

		private static float _crowdEyeX;

		private static float _crowdEyeY;

		private static float _crowdFx;

		private static float _crowdFy;

		private static bool _crowdGear;

		private static List<CrowdItem> _crowdOut;

		private static readonly List<MeshInstance> _crowdArmor = new List<MeshInstance>();

		internal static long CrowdPosed;

		private const float Give = 0.08f;

		private const float GearReach = 45f;

		private const int Redress = 30;

		private const float BlendSeconds = 0.25f;

		private const float ArmorRange = 40f;

		private const float PlantRange = 30f;

		private static readonly Dictionary<Actor, Model> Figures = new Dictionary<Actor, Model>();

		private static readonly Dictionary<string, uint> SpeciesSkin = new Dictionary<string, uint>();

		private static readonly List<Actor> Stale = new List<Actor>();

		private static int _frame;

		private static readonly Dictionary<SpriteImage, uint> AverageColors = new Dictionary<SpriteImage, uint>();

		internal static bool FarPoses;

		internal static long PosesMade;

		internal static long PosesHeld;

		private static Model _figure;

		private static Model _figureFrom;

		private static bool _asFigure;

		internal static string FigureItem;

		private static readonly ConditionalWeakTable<MeshInstance, FlameSword> FlameOf = new ConditionalWeakTable<MeshInstance, FlameSword>();

		internal static readonly List<FlameSword> FlamesDrawn = new List<FlameSword>();

		internal static Vec3 FlameEye;

		private static int _flamesFrame = -1;

		internal static bool GlideOff;

		internal static long Glided;

		private const int MostGlideParts = 24;

		internal static bool _asHost;

		internal static Actor HostShown;

		internal static bool CoastOff;

		internal static long Coasted;

		private const int Recheck = 16;

		internal static bool LeanOff;

		internal static long LeanPosed;

		internal static bool LooksOn = true;

		internal static bool TimeLooks;

		private static readonly Dictionary<MeshModel, MeshModel> Trousered = new Dictionary<MeshModel, MeshModel>();

		private static readonly Dictionary<MeshModel, ulong> ChestHides = new Dictionary<MeshModel, ulong>();

		private static readonly Dictionary<MeshModel, ulong> BootsHide = new Dictionary<MeshModel, ulong>();

		private static readonly Dictionary<MeshModel, ulong> HairHides = new Dictionary<MeshModel, ulong>();

		internal static bool RegaliaOn = true;

		internal static bool GuardHalberds = true;

		internal static int RegaliaInstances;

		internal static int RegaliaTriangles;

		internal static int RegaliaSkipped;

		internal static int FallenRegaliaForTest = 0;

		internal static double RegaliaMs;

		private static int _rgFrame = -1;

		private static int _rgInstances;

		private static int _rgTriangles;

		private static long _rgTicks;

		private static readonly ConditionalWeakTable<MeshInstance, Worn> WornOf = new ConditionalWeakTable<MeshInstance, Worn>();

		private static readonly Dictionary<string, string> FarNames = new Dictionary<string, string>();

		private static readonly ConditionalWeakTable<Model, HairFix> HairFixes = new ConditionalWeakTable<Model, HairFix>();

		private static readonly ConditionalWeakTable<MeshInstance, Fallen> FallenOf = new ConditionalWeakTable<MeshInstance, Fallen>();

		private static readonly List<Fallen> FallenRecent = new List<Fallen>();

		private static int _fallenFrame = -1;

		private static int _fallenCount;

		internal static RestPose HostRest;

		internal static PersonRig HostRig;

		internal static float HostBody;

		private static readonly Dictionary<Actor, GunAt> Guns = new Dictionary<Actor, GunAt>();

		private static Vec3 _gunMuzzle = new Vec3(0f, 0.502f, 0.0215f);

		private static Vec3 _gunPort = new Vec3(0.0148f, 0.0585f, 0.015f);

		private static Vec3 _gunPortDir = new Vec3(0.85f, -0.13f, 0.47f);

		private static MeshModel _gunBound;

		private static bool _verifyFamilies;

		private static int _familiesCompared;

		private static int _familiesDiffer;

		private static string _familiesFirst;

		private static bool _verifyArmor;

		private static int _hairCompared;

		private static int _hairDiffer;

		private static string _hairFirst;

		private static readonly string[][] ProbeSets = new string[4][]
		{
			new string[3] { "helmet_iron", "armor_iron", "boots_iron" },
			new string[3] { null, "armor_leather", "boots_steel" },
			new string[3] { "helmet_mythril", null, "boots_mythril" },
			new string[3] { "helmet_bronze", "armor_copper", null }
		};

		private static MeshInstance _probeBody;

		private static uint[] _probePalette;

		private static readonly ConditionalWeakTable<Model, InWater> _inWater = new ConditionalWeakTable<Model, InWater>();

		private static readonly ConditionalWeakTable<Model, HeldTorch> TorchOf = new ConditionalWeakTable<Model, HeldTorch>();

		internal static readonly List<TorchFire> TorchesDrawn = new List<TorchFire>();

		internal static float TorchNight;

		private static int _torchesFrame = -1;

		private const int MostTorches = 40;

		private const float TorchModelReach = 24f;

		internal static readonly List<HeldGlow> GlowsDrawn = new List<HeldGlow>();

		private static int _glowsFrame = -1;

		private static readonly Dictionary<Actor, MeshInstance> Wraps = new Dictionary<Actor, MeshInstance>();

		internal static int FamiliesCompared => _familiesCompared;

		internal static string ArmorCounts => $"{ArmorMeshes.DressesCompared} dressings, {_hairCompared} hair and shoes";

		internal static bool CarriesTorchOutside(Actor a)
		{
			return CarriesTorch(a);
		}

		internal static string CarriedModelForTest(Actor a)
		{
			if (a == null || !Figures.TryGetValue(a, out var value) || !value.CarryPlaced || value.Drawn < _frame - 1 || value.Weapon == null)
			{
				return null;
			}
			return value.Weapon.Model?.Name;
		}

		internal static string HeldModelForTest(Actor a)
		{
			if (a == null || !Figures.TryGetValue(a, out var value) || value.Drawn < _frame - 1 || value.Weapon == null)
			{
				return null;
			}
			return value.Weapon.Model?.Name;
		}

		private static string CarryKey(string res)
		{
			if (res == null)
			{
				return null;
			}
			if (CarryKeys.TryGetValue(res, out var value))
			{
				return value;
			}
			value = res;
			try
			{
				string text = AssetManager.resources.get(res)?.path_gameplay_sprite;
				if (text == "bag_food")
				{
					value = "bag_food";
				}
				else if (text == "box_tea")
				{
					value = "tea";
				}
			}
			catch
			{
			}
			CarryKeys[res] = value;
			return value;
		}

		private static Carried CarriedFor(Actor a, Model m, string tool, string load, WeaponKind kind, bool child, bool swimming, float distance)
		{
			if (child || swimming || distance > 45f * Models.LodZoom)
			{
				return null;
			}
			if (_asFigure || _asHost)
			{
				if (kind != WeaponKind.None || HostCarried == null)
				{
					return null;
				}
				if (!(WorldBoxMod.Instance != null) || a != WorldBoxMod.Instance.Host)
				{
					return null;
				}
				return HostCarried;
			}
			if (load != null)
			{
				return CarryLook.Load(CarryKey(load));
			}
			if (tool != null)
			{
				Carried carried = CarryLook.Tool(tool);
				if (carried == null || carried.Style != CarryStyle.Hand)
				{
					return null;
				}
				return carried;
			}
			return null;
		}

		private static void CarryPose(PersonRig rig, MeshInstance inst, Carried c, float phase, float stride)
		{
			if (c.Style == CarryStyle.Hand)
			{
				rig.CarryHand(inst, phase, stride);
			}
			else
			{
				rig.CarryLean(inst, c.Style, c.Lean);
			}
		}

		private static void CarryPlace(Model m, PersonRig rig, MeshInstance inst, Carried c, float body)
		{
			m.CarryPlaced = ((c.Style == CarryStyle.Hand) ? rig.HandFrame(inst, body, c.Palm, out m.CarryPos, out m.CarryBasis) : rig.LoadPlace(inst, c.Style, body, out m.CarryPos, out m.CarryBasis));
			if (m.CarryPlaced && c.Style != CarryStyle.Hand)
			{
				rig.LoadArms(inst, c, m.CarryPos, m.CarryBasis);
			}
		}

		private static bool AddCarried(List<MeshInstance> meshes, Model m, Carried c, float height, VoxelObject template)
		{
			if (!m.CarryPlaced)
			{
				return false;
			}
			MeshModel meshModel = c.Model(m.WeaponDetail);
			if (meshModel == null)
			{
				return false;
			}
			MeshInstance meshInstance = m.Weapon ?? (m.Weapon = new MeshInstance());
			meshInstance.Model = meshModel;
			meshInstance.Palette = null;
			if (meshInstance.PartRotation != null && meshInstance.PartRotation.Length != meshModel.Parts.Length)
			{
				meshInstance.PartRotation = null;
			}
			meshInstance.Basis = m.CarryBasis;
			meshInstance.Position = m.CarryPos;
			meshInstance.Tag = template.Tag;
			meshInstance.Flash = template.Flash;
			meshInstance.NoShadow = template.NoShadow || height < 0.6f;
			meshInstance.SortDepth = template.SortDepth;
			meshInstance.Static = false;
			meshes.Add(meshInstance);
			if (_asFigure && c.Name.StartsWith("item_", StringComparison.Ordinal))
			{
				FigureItem = c.Name.Substring(5);
			}
			if (_carriedFrame != _frame)
			{
				_carriedFrame = _frame;
				Array.Clear(CarriedDrawn, 0, CarriedDrawn.Length);
			}
			CarriedDrawn[(uint)c.Style]++;
			return true;
		}

		internal static object HoldFeetForTest()
		{
			List<(WalkCycle.Walker, WalkCycle.Foot, WalkCycle.Foot)> list = new List<(WalkCycle.Walker, WalkCycle.Foot, WalkCycle.Foot)>(Figures.Count);
			foreach (KeyValuePair<Actor, Model> figure in Figures)
			{
				if (figure.Value.Walk != null)
				{
					list.Add((figure.Value.Walk, figure.Value.Walk.FootL, figure.Value.Walk.FootR));
				}
			}
			return list;
		}

		internal static void PutBackFeetForTest(object taken)
		{
			if (!(taken is List<(WalkCycle.Walker, WalkCycle.Foot, WalkCycle.Foot)> list))
			{
				return;
			}
			foreach (var item in list)
			{
				item.Item1.FootL = item.Item2;
				item.Item1.FootR = item.Item3;
			}
		}

		private static bool CrowdKeepsDress(Model m, MeshModel model)
		{
			if (m.Crowd && m.DressedModel == model && _frame - m.DressedAt < 30)
			{
				return _frame >= m.DressedAt;
			}
			return false;
		}

		private static void Dressed(Model m, MeshModel model)
		{
			m.DressedAt = _frame;
			m.DressedModel = model;
		}

		internal static bool OnCard(Actor a)
		{
			if (_crowdLine <= 0f || !Figures.TryGetValue(a, out var value) || !value.Crowd || value.CrowdBlend < 1f)
			{
				return false;
			}
			try
			{
				return a.data != null && a.data.id == value.Id;
			}
			catch
			{
				return false;
			}
		}

		internal static void BeginCrowd(List<CrowdItem> into, float line, bool gear, float pixel, float eyeX, float eyeY, float fx, float fy)
		{
			_crowdOut = into;
			_crowdLine = ((into != null && pixel > 0f) ? line : 0f);
			_crowdGear = gear;
			_crowdPixel = pixel;
			_crowdEyeX = eyeX;
			_crowdEyeY = eyeY;
			_crowdFx = fx;
			_crowdFy = fy;
		}

		private static float WorkStroke(Actor a, Model m, string tool, WeaponKind swingKind)
		{
			double num = 0.0;
			float num2 = 0f;
			try
			{
				num = WorldPace.ActionNow;
				num2 = a.timer_action;
			}
			catch
			{
			}
			if (num2 > m.WorkTimer + 0.05f)
			{
				m.WorkHit = num;
			}
			m.WorkTimer = num2;
			float num3 = ((tool == "hammer") ? 0.6f : ((swingKind == WeaponKind.None) ? 1.3f : 1f));
			float num4 = (float)((num - m.WorkHit) / (double)num3) + 0.55f;
			return num4 - Mathf.Floor(num4);
		}

		private static bool Crowded(List<MeshInstance> meshes, Actor a, Model m, PersonRig rig, MeshInstance inst, MeshModel model, float x, float y, float z, float body, float heading, float speed, float distance, float dt, float walkDt, bool fresh, bool child, WeaponKind kind, float attack, BaseSimObject foe, bool working, Vector2 workAt, string tool, string load, bool drilling, bool carrying, bool swimming, float sit, float lie, SpriteCache sprites, VoxelObject template)
		{
			bool flag = foe != null;
			WeaponKind weaponKind = ToolMeshes.SwingOf(tool);
			bool flag2 = working && (weaponKind != WeaponKind.None || tool == "basket" || tool == "bucket");
			int num = (flag2 ? CrowdPoses.WorkRow(weaponKind) : (-1));
			int num2 = ((flag && !flag2) ? CrowdPoses.FightRow(kind, m.Side) : (-1));
			bool flag3 = CrowdWants(a, m, model, x, y, body, distance, kind, drilling, carrying, load, sit, lie) && (!flag2 || num >= 0) && (!flag || flag2 || num2 >= 0);
			float num3 = (flag3 ? 1f : 0f);
			m.CrowdBlend = ((_crowdLine <= 0f || fresh) ? num3 : Mathf.MoveTowards(m.CrowdBlend, num3, Time.unscaledDeltaTime / 0.25f));
			m.Crowd = flag3;
			if (!flag3 || m.CrowdBlend < 1f)
			{
				m.Lean = null;
				return false;
			}
			Remember(a, m, model, child, kind, foe, tool, load, workAt, swimming);
			CrowdPose(meshes, a, m, rig, inst, model, x, y, z, body, heading, speed, distance, dt, walkDt, fresh, child, kind, attack, num, num2, weaponKind, tool, load, swimming, sprites, template);
			return true;
		}

		private static void CrowdPose(List<MeshInstance> meshes, Actor a, Model m, PersonRig rig, MeshInstance inst, MeshModel model, float x, float y, float z, float body, float heading, float speed, float distance, float dt, float walkDt, bool fresh, bool child, WeaponKind kind, float attack, int workRow, int fightRow, WeaponKind swing, string tool, string load, bool swimming, SpriteCache sprites, VoxelObject template)
		{
			if (fresh)
			{
				m.Walk.Reset(heading);
				m.WalkHeading = float.NaN;
			}
			float num = ((walkDt >= 0f) ? walkDt : dt);
			float run = 0f;
			float phase;
			float stride;
			if (swimming)
			{
				m.Phase += num * (speed / Mathf.Max(0.1f, body * 0.55f) + 3f);
				phase = m.Phase;
				stride = 0.35f;
			}
			else
			{
				float turn = ((num > 0f && !float.IsNaN(m.WalkHeading)) ? (Mathf.DeltaAngle(m.WalkHeading * 57.29578f, heading * 57.29578f) * (MathF.PI / 180f) / num) : 0f);
				m.Walk.Step(speed, body, turn, heading, num, rig.LegShare, rig.Robe);
				phase = m.Walk.Phase;
				stride = m.Walk.Stride;
				run = m.Walk.Run;
			}
			m.Walk.Unplant();
			m.WalkHeading = heading;
			if (m.Hold != null)
			{
				m.Hold.Model = null;
			}
			int variant = CrowdPoses.VariantOf(rig, child, m.Stage == Stage.Elder);
			CrowdPoses.Motion(variant, phase, stride, run, out var a2, out var b, out var w);
			if (workRow >= 0)
			{
				attack = WorkStroke(a, m, tool, swing);
				a2 = (b = CrowdPoses.Stroke(workRow, attack));
				w = 0f;
			}
			else if (fightRow >= 0)
			{
				a2 = Mathf.Floor((w >= 0.5f) ? b : a2);
				b = CrowdPoses.Stroke(fightRow, (attack > 0f && attack < 1f) ? attack : 0f);
				w = Mathf.Clamp01(m.Stance);
			}
			float num2 = body * LookTall(m) / Mathf.Max(0.1f, rig.Height);
			PersonRig.Place(inst, x, y, z, heading, num2);
			inst.Tag = template.Tag;
			inst.Flash = template.Flash;
			inst.Multiply = template.Multiply;
			inst.NoShadow = template.NoShadow;
			inst.SortDepth = template.SortDepth;
			inst.KeepSmall = true;
			inst.Fade = 1f;
			_crowdOut.Add(new CrowdItem
			{
				Body = inst,
				A = a2,
				B = b,
				W = w
			});
			CrowdPosed++;
			if (template.Tag != 2147483646)
			{
				Hitboxes.RecordSprite(a, x, y, z, body * 0.35f, body, heading);
			}
			float num3 = (x - _crowdEyeX) * _crowdFx + (y - _crowdEyeY) * _crowdFy;
			CoastFrom(m, x, y, z, body, num2, variant, workRow < 0 && fightRow < 0 && !swimming && template.Tag != 2147483646, (num3 > 0.5f && _crowdPixel > 0f) ? (body / (num3 * _crowdPixel)) : 99f);
			if (_crowdGear && !child)
			{
				CrowdHands(rig, inst, model, a2, b, w);
				AddArmor(_crowdArmor, a, m, rig, inst, template);
				for (int i = 0; i < _crowdArmor.Count; i++)
				{
					_crowdOut.Add(new CrowdItem
					{
						Body = _crowdArmor[i],
						Skeleton = model,
						A = a2,
						B = b,
						W = w
					});
				}
				_crowdArmor.Clear();
				m.WeaponDetail = WeaponDetailAt(distance, m.WeaponDetail);
				if (((load == null && tool == null) || !AddTool(meshes, a, m, rig, inst, tool, load, (workRow >= 0) ? attack : 0f, body, heading, sprites, template)) && !AddHalberd(meshes, a, m, rig, inst, kind, body, template) && kind != WeaponKind.None)
				{
					AddWeapon(meshes, m, rig, inst, kind, attack, body, template, 1f - m.Stance);
				}
			}
		}

		private static bool CrowdWants(Actor a, Model m, MeshModel model, float x, float y, float body, float distance, WeaponKind kind, bool drilling, bool carrying, string load, float sit, float lie)
		{
			if (_crowdLine <= 0f || _asFigure || _asHost || RoomCombat.Drawing)
			{
				return false;
			}
			if (distance < Mathf.Max(45f, 60f) * Models.LodZoom)
			{
				return false;
			}
			float num = (x - _crowdEyeX) * _crowdFx + (y - _crowdEyeY) * _crowdFy;
			if (num <= 0.5f)
			{
				return false;
			}
			if (body / (num * _crowdPixel) >= _crowdLine * (m.Crowd ? 1.08f : 0.92f))
			{
				return false;
			}
			if (drilling || carrying || load != null || sit > 0f || lie > 0f || kind == WeaponKind.Bow || kind == WeaponKind.Gun || kind == WeaponKind.Shotgun)
			{
				return false;
			}
			if (m.Expecting > 0 || model.Info("mutation") != null || IsZombie(a))
			{
				return false;
			}
			if (Lounging.Sits(a) || KingGuards.Kneels(a, out var _) || Blocks.PoseOf(a, out var _, out var age) || Headshots.KnockOf(a, out age))
			{
				return false;
			}
			return true;
		}

		private static void CrowdFade(Model m, MeshInstance inst, PersonRig rig, bool child)
		{
			float crowdBlend = m.CrowdBlend;
			inst.Fade = 1f - crowdBlend;
			if (!(crowdBlend <= 0f) && _crowdOut != null && !(_crowdLine <= 0f))
			{
				MeshInstance meshInstance = m.CrowdGhost ?? (m.CrowdGhost = new MeshInstance());
				meshInstance.Model = inst.Model;
				meshInstance.Palette = inst.Palette;
				meshInstance.Hidden = inst.Hidden;
				meshInstance.Position = inst.Position;
				meshInstance.Basis = inst.Basis;
				meshInstance.Tag = inst.Tag;
				meshInstance.Flash = inst.Flash;
				meshInstance.Multiply = inst.Multiply;
				meshInstance.NoShadow = inst.NoShadow;
				meshInstance.SortDepth = inst.SortDepth;
				meshInstance.KeepSmall = true;
				meshInstance.Fade = crowdBlend;
				CrowdPoses.Motion(CrowdPoses.VariantOf(rig, child, m.Stage == Stage.Elder), m.Walk.Phase, m.Walk.Stride, m.Walk.Run, out var a, out var b, out var w);
				_crowdOut.Add(new CrowdItem
				{
					Body = meshInstance,
					A = a,
					B = b,
					W = w
				});
			}
		}

		private static void CrowdHands(PersonRig rig, MeshInstance inst, MeshModel model, float ra, float rb, float w)
		{
			float[] data = CrowdPoses.For(model, rig);
			inst.EnsurePose();
			inst.ResetPose();
			CrowdHand(inst, model, data, rig.LowerArmR, ra, rb, w);
			CrowdHand(inst, model, data, rig.LowerArmL, ra, rb, w);
		}

		private static void CrowdHand(MeshInstance inst, MeshModel model, float[] data, int part, float ra, float rb, float w)
		{
			if (part >= 0)
			{
				CrowdPoses.PartAt(data, model, ra, rb, w, part, out var r, out var t);
				Vec3 pivot = model.Parts[part].Pivot;
				inst.PartRotation[part] = r;
				inst.PartOffset[part] = t + r.Mul(pivot) - pivot;
				inst.PartScale[part] = 1f;
			}
		}

		internal static uint GameSkin(Actor a)
		{
			try
			{
				if (a == null || a.asset == null || a.data == null || a.asset.dynamic_sprite_zombie || a.asset.unit_zombie)
				{
					return 0u;
				}
				int phenotype_index = a.data.phenotype_index;
				if (phenotype_index == 0)
				{
					return 0u;
				}
				Color32[] array = AssetManager.phenotype_library.getAssetByPhenotypeIndex(phenotype_index)?.colors;
				if (array == null || array.Length == 0)
				{
					return 0u;
				}
				Color32 color = array[Mathf.Clamp(a.data.phenotype_shade, 0, array.Length - 1)];
				return TerrainCache.Rgb(color.r, color.g, color.b);
			}
			catch
			{
				return 0u;
			}
		}

		public static bool IsHumanoid(Actor a)
		{
			try
			{
				return a.asset != null && (a.asset.is_humanoid || a.asset.id == "plague_doctor") && !a.asset.is_boat;
			}
			catch
			{
				return false;
			}
		}

		public static void Tick()
		{
			_frame++;
			Attention.Tick();
			PregnantBody.NewFrame();
			Loot.Tick();
			foreach (KeyValuePair<Actor, Model> figure in Figures)
			{
				Model value = figure.Value;
				if (value.Fallen || value.Drawn != _frame - 1 || value.Mesh == null || value.Rig == null || figure.Key == HostShown)
				{
					continue;
				}
				bool flag;
				try
				{
					flag = figure.Key != null && figure.Key.isAlive();
				}
				catch
				{
					flag = false;
				}
				if (flag)
				{
					Loot.Watch(figure.Key);
					continue;
				}
				value.Fallen = true;
				if (Risen(figure.Key))
				{
					continue;
				}
				RoomCombat.FallingIn = value.Space;
				RoomCombat.FallingZ = value.Mesh.Position.Z;
				try
				{
					float lie = Loot.Fell(figure.Key);
					MeshInstance weapon = ((value.Weapon != null && value.Weapon.Model != null) ? value.Weapon : null);
					if (Slicing.Split(figure.Key, value.Mesh, weapon, beast: false))
					{
						Loot.CapLie(figure.Key, 9f);
					}
					else
					{
						Corpses.Fall(value.Mesh, value.Rig, weapon, value.Away, (float)(figure.Key.GetHashCode() & 0x3FF) / 1023f, lie);
					}
				}
				finally
				{
					RoomCombat.FallingIn = null;
				}
			}
			if (_frame % 300 != 0)
			{
				return;
			}
			Stale.Clear();
			foreach (KeyValuePair<Actor, Model> figure2 in Figures)
			{
				if (_frame - figure2.Value.Seen > 600)
				{
					Stale.Add(figure2.Key);
				}
			}
			foreach (Actor item in Stale)
			{
				Figures.Remove(item);
			}
		}

		internal static string DrawnModel(Actor a, out bool female, out ulong hidden)
		{
			female = false;
			hidden = 0uL;
			if (a == null || !Figures.TryGetValue(a, out var value) || value.Mesh == null || value.Mesh.Model == null)
			{
				return null;
			}
			female = value.Female;
			hidden = value.Mesh.Hidden;
			return value.Mesh.Model.Name;
		}

		internal static int ArmorShown(Actor a)
		{
			if (a == null || !Figures.TryGetValue(a, out var value))
			{
				return -1;
			}
			return value.ArmorShown;
		}

		internal static bool PaletteForTest(Actor a, out Humanoid.Palette p)
		{
			p = default(Humanoid.Palette);
			if (a == null || !Figures.TryGetValue(a, out var value))
			{
				return false;
			}
			p = value.Palette;
			return true;
		}

		public static int StepFrame(Actor a, bool moving)
		{
			if (!moving)
			{
				return 0;
			}
			switch ((int)(PoseClock.Now * 8f + (float)(a.GetHashCode() & 3)) & 3)
			{
			default:
				return 0;
			case 2:
				return 2;
			case 0:
				return 1;
			}
		}

		public static void Add(List<VoxelObject> output, Actor a, SpriteImage img, SpriteCache sprites, float x, float y, float z, float body, float heading, int frame, VoxelObject template)
		{
			Model model = Figure(a, img);
			Stage stage = model.Stage;
			if (model.Frames == null)
			{
				bool child = stage == Stage.Child;
				model.Frames = new VoxelShape[3]
				{
					Humanoid.Build(model.Palette, 0, child),
					Humanoid.Build(model.Palette, 1, child),
					Humanoid.Build(model.Palette, 2, child)
				};
			}
			VoxelShape voxelShape = model.Frames[frame];
			float num = Mathf.Cos(heading);
			float num2 = Mathf.Sin(heading);
			float width = body * 8f / (float)voxelShape.Height;
			VoxelObject item = VoxelObject.Standing(voxelShape, x, y, z, width, body, 0.5f, num2, 0f - num);
			item.Tag = template.Tag;
			item.Flash = template.Flash;
			item.Multiply = template.Multiply;
			item.NoShadow = template.NoShadow;
			item.SortDepth = template.SortDepth;
			output.Add(item);
			SpriteImage spriteImage = ((stage == Stage.Child) ? null : HeldItem(a, sprites));
			if (spriteImage != null)
			{
				int num3;
				switch (frame)
				{
				default:
					num3 = 0;
					break;
				case 1:
					num3 = -1;
					break;
				case 2:
					num3 = 1;
					break;
				}
				int num4 = num3;
				Vec3 grip = item.Origin + item.U * (7.5f * item.SizeU) + item.V * (4.6f * item.SizeV) + item.N * ((float)num4 * item.SizeN);
				Vec3 vec = new Vec3(num, num2, 0f);
				Vec3 blade = new Vec3(0f, 0f, 0.8f) + vec * 0.6f;
				VoxelObject item2 = ItemPose.HoldItem(spriteImage, grip, blade, item.U, body * 0.6f, overlay: false);
				item2.Tag = template.Tag;
				item2.Flash = template.Flash;
				item2.NoShadow = template.NoShadow;
				item2.SortDepth = template.SortDepth;
				output.Add(item2);
			}
		}

		private static Model Figure(Actor a, SpriteImage img)
		{
			Stage stage = StageOf(a);
			bool flag = Models.IsFemale(a);
			long num = -1L;
			try
			{
				num = ((a.data != null) ? a.data.id : (-1));
			}
			catch
			{
			}
			if (!Figures.TryGetValue(a, out var value) || value.Stage != stage || value.Female != flag || value.Id != num)
			{
				if (value != null && value.Id != num)
				{
					value = null;
				}
				string text = ((a.asset != null) ? a.asset.id : "");
				Humanoid.Palette palette = Humanoid.Extract(img, text, GameSkin(a));
				if (IsZombie(a))
				{
					palette = ZombieColors.Of(palette, text, ZombieGear.Looked(a), Seed(a));
				}
				if (stage != Stage.Child)
				{
					SpeciesSkin[text] = palette.Skin;
				}
				else if (!palette.SkinSeen)
				{
					palette.Skin = (SpeciesSkin.TryGetValue(text, out var value2) ? value2 : TerrainCache.Rgb(232, 184, 140));
					if (palette.Hair == palette.Skin)
					{
						palette.Hair = TerrainCache.Scale(palette.Skin, 0.5f);
					}
				}
				if (stage == Stage.Elder)
				{
					palette.Hair = TerrainCache.Lerp(palette.Hair, TerrainCache.Rgb(208, 208, 214), 0.8f);
				}
				MeshInstance mesh = value?.Mesh;
				float phase = value?.Phase ?? 0f;
				WalkCycle.Walker walk = ((value != null) ? value.Walk : new WalkCycle.Walker());
				value = new Model
				{
					Stage = stage,
					Female = flag,
					Palette = palette,
					Mesh = mesh,
					Phase = phase,
					Walk = walk,
					Id = num
				};
				Figures[a] = value;
			}
			value.Seen = _frame;
			return value;
		}

		public static void AddMesh(List<MeshInstance> meshes, List<VoxelObject> voxels, Actor a, SpriteImage img, SpriteCache sprites, float x, float y, float z, float body, float heading, float speed, float distance, float dt, VoxelObject template, bool swimming, float hitAge = -1f, float walkDt = -1f, float sit = 0f, float lie = 0f)
		{
			SceneClock.Person(distance);
			if (sit <= 0f && lie <= 0f && LeanCard(meshes, a, x, y, z, body, heading, speed, distance, dt, walkDt, swimming, sprites, template))
			{
				return;
			}
			Model model = Figure(a, img);
			string text = LookFamily(model, FamilyOf(a, model));
			MeshModel meshModel = (model.BaseModel = Models.DetailedCreature(text, distance, model.BaseModel ?? model.Mesh?.Model));
			model.Expecting = ((model.Female && model.Stage != Stage.Child && meshModel != null) ? PregnantBody.StageOf(Births.Progress(a)) : 0);
			if (model.Expecting > 0 && !Models.IsDistant(meshModel))
			{
				meshModel = PregnantBody.Morph(meshModel, model.Expecting);
				Births.BelliesDrawn++;
			}
			if (meshModel == null)
			{
				return;
			}
			meshModel = LookModel(model, meshModel);
			if (model.Mesh == null)
			{
				model.Mesh = new MeshInstance();
			}
			MeshInstance mesh = model.Mesh;
			mesh.Model = meshModel;
			PersonRig personRig = Models.Rig(meshModel);
			if (!CrowdKeepsDress(model, meshModel))
			{
				personRig.Dress(mesh, model.Palette, text == "person_human" || text == "person_dwarf" || text == "person_zombie", model.Female);
				if (model.Stage == Stage.Child && personRig.Beard >= 0 && personRig.Beard < 64)
				{
					mesh.Hidden |= (ulong)(1L << personRig.Beard);
				}
				LookDress(a, model, text, personRig, mesh);
				Mutations.Dress(a, mesh);
				Dressed(model, meshModel);
			}
			model.Rig = personRig;
			bool flag = model.Drawn < _frame - 1;
			if (flag)
			{
				model.Walk.Unplant();
			}
			model.Drawn = _frame;
			model.Space = (RoomCombat.Drawing ? RoomCombat.Key : null);
			bool flag2 = model.Stage == Stage.Child;
			if (_frame - model.WeaponChecked > 90)
			{
				model.WeaponChecked = _frame;
				string text2 = null;
				try
				{
					text2 = ((flag2 || a.getWeapon() == null) ? null : a.getWeaponAsset()?.id);
				}
				catch
				{
				}
				if (text2 != model.WeaponId)
				{
					model.WeaponId = text2;
					model.Look = ViewMeshes.LookOf(text2, (text2 != null) ? HeldItem(a, sprites) : null);
					if (text2 == null)
					{
						model.Look.Kind = WeaponKind.None;
					}
				}
			}
			WeaponKind weaponKind = ((model.WeaponId != null) ? model.Look.Kind : WeaponKind.None);
			if (_asHost && HostRest.On)
			{
				weaponKind = WeaponKind.None;
			}
			if (Rivals.BareHanded(a))
			{
				weaponKind = WeaponKind.None;
			}
			BaseSimObject baseSimObject = null;
			try
			{
				if (!_asFigure && !_asHost && a.has_attack_target && a.attack_target != null && a.attack_target.isAlive())
				{
					baseSimObject = a.attack_target;
				}
			}
			catch
			{
			}
			Drill.Stroke s = default(Drill.Stroke);
			bool flag3 = baseSimObject == null && !flag2 && !swimming && !_asFigure && !_asHost && Drill.At(a, new Vector2(x, y), speed, weaponKind, body, ref s);
			model.Stance = Mathf.MoveTowards(model.Stance, (!_asHost) ? (((baseSimObject != null || (flag3 && s.Guard)) && !swimming && !flag2) ? 1f : 0f) : (swimming ? 0f : HostStance()), dt * 3f);
			string text3 = null;
			string text4 = null;
			bool flag4 = false;
			Vector2 vector = default(Vector2);
			if (!flag2 && baseSimObject == null && !swimming && !_asFigure && !_asHost)
			{
				try
				{
					if (a.asset != null && a.asset.use_tool_items)
					{
						if (a.isCarryingResources())
						{
							text4 = a.inventory.getItemIDToRender();
						}
						else if (a.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task != null && !string.IsNullOrEmpty(((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.force_hand_tool))
						{
							text3 = ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.force_hand_tool;
						}
					}
					if (text3 != null)
					{
						Building beh_building_target = a.beh_building_target;
						WorldTile beh_tile_target = a.beh_tile_target;
						bool flag5 = true;
						if (beh_building_target != null && beh_building_target.current_tile != null)
						{
							vector = new Vector2(beh_building_target.current_tile.posV3.x, beh_building_target.current_tile.posV3.y);
						}
						else if (beh_tile_target != null)
						{
							vector = new Vector2(beh_tile_target.posV3.x, beh_tile_target.posV3.y);
						}
						else
						{
							flag5 = false;
						}
						flag4 = flag5 && speed < 0.15f && (vector - new Vector2(x, y)).sqrMagnitude < 6.7599993f;
					}
				}
				catch
				{
				}
			}
			Carried carried = CarriedFor(a, model, text3, text4, weaponKind, flag2, swimming, distance);
			model.CarryPlaced = false;
			if (baseSimObject != null || flag4 || flag3)
			{
				Vector2 vector2 = ((baseSimObject != null) ? RoomCombat.Drawn(baseSimObject.current_position) : (flag3 ? s.Face : vector)) - new Vector2(x, y);
				if (vector2.sqrMagnitude > 0.0001f)
				{
					float target = Mathf.Atan2(vector2.y, vector2.x);
					if (float.IsNaN(model.Face))
					{
						model.Face = heading;
						model.FaceTurn = 0f;
					}
					model.Face = Turn.Damp(model.Face, ref model.FaceTurn, target, dt, 0.15f, MathF.PI * 3f);
					if (baseSimObject != null || flag3)
					{
						model.AimPitch = Mathf.Lerp(model.AimPitch, Mathf.Clamp(vector2.magnitude * 0.025f, 0.04f, 0.35f), 1f - Mathf.Exp(-4f * dt));
					}
				}
			}
			else if (!float.IsNaN(model.Face))
			{
				model.Face = Turn.Damp(model.Face, ref model.FaceTurn, heading, dt, 0.25f, MathF.PI * 2f);
				if (Mathf.Abs(Mathf.DeltaAngle(model.Face * 57.29578f, heading * 57.29578f)) < 2f)
				{
					model.Face = float.NaN;
				}
			}
			if (!float.IsNaN(model.Face))
			{
				heading = model.Face;
			}
			float num = heading;
			if (weaponKind == WeaponKind.Bow)
			{
				heading -= 1.15f * model.Stance;
			}
			float attack = 0f;
			try
			{
				double last_attack_timestamp = a.last_attack_timestamp;
				if (last_attack_timestamp != model.LastAttack)
				{
					model.LastAttack = last_attack_timestamp;
					model.Side = 0f - model.Side;
				}
				float num2 = WorldPace.Since(last_attack_timestamp);
				float num3 = AttackSeconds(weaponKind);
				if (num2 >= 0f && num2 < num3)
				{
					attack = num2 / num3;
				}
				if (a.attackedBy != null && a.attackedBy.isAlive())
				{
					model.Away = new Vector2(x, y) - RoomCombat.Drawn(a.attackedBy.current_position);
				}
			}
			catch
			{
			}
			if (!_asFigure && !_asHost && WindUps.Phase(a, weaponKind, AttackSeconds(weaponKind), out var attack2))
			{
				attack = attack2;
			}
			if (_asHost)
			{
				attack = HostAttack(weaponKind);
			}
			if (flag3)
			{
				attack = s.Attack;
				model.Side = s.Side;
				weaponKind = s.Pose;
			}
			if (Crowded(meshes, a, model, personRig, mesh, meshModel, x, y, z, body, heading, speed, distance, dt, walkDt, flag, flag2, weaponKind, attack, baseSimObject, flag4, vector, text3, text4, flag3, carried != null, swimming, sit, lie, sprites, template))
			{
				return;
			}
			bool flag6 = HoldPose(a, model, meshModel, distance, flag || baseSimObject != null || flag3 || hitAge >= 0f || weaponKind == WeaponKind.Bow || sit > 0f || lie > 0f || model.Stance > 0.01f);
			float num4 = ((walkDt >= 0f) ? walkDt : dt);
			float num5 = num4 + ((flag6 || model.Hold == null) ? 0f : model.Hold.Debt);
			WalkCycle.Walker walker = null;
			float s2;
			float seated;
			if (flag6)
			{
				HeldPose(model, num4, ref heading, ref z, out s2, out seated, ref attack);
			}
			else
			{
				bool flag7 = !swimming && IsZombie(a);
				float run = 0f;
				float num6;
				float phase;
				if (swimming || flag7)
				{
					num6 = (swimming ? 0.35f : Mathf.Clamp01(speed / Mathf.Max(0.1f, body * 1.4f)));
					model.Phase += num5 * (speed / Mathf.Max(0.1f, body * 0.55f) + (swimming ? 3f : 0f));
					phase = model.Phase;
				}
				else
				{
					if (flag)
					{
						model.Walk.Reset(heading);
						model.WalkHeading = float.NaN;
					}
					float turn = ((num5 > 0f && !float.IsNaN(model.WalkHeading)) ? (Mathf.DeltaAngle(model.WalkHeading * 57.29578f, heading * 57.29578f) * (MathF.PI / 180f) / num5) : 0f);
					model.Walk.Step(speed, body, turn, heading, num5, personRig.LegShare, personRig.Robe);
					walker = model.Walk;
					num6 = walker.Stride;
					phase = walker.Phase;
					run = walker.Run;
				}
				model.WalkHeading = heading;
				float num7 = PregnantBody.Size(model.Expecting);
				if (num7 > 0f)
				{
					num6 *= 1f - 0.3f * num7;
				}
				float lean = (swimming ? 0f : Mathf.Clamp01((speed / Mathf.Max(0.1f, body) - 1.6f) / 1.6f));
				float flinch = ((hitAge >= 0f) ? Mathf.Sin(Mathf.Clamp01(1f - hitAge / 0.3f) * MathF.PI * 0.5f) : 0f);
				float yaw = 0f;
				float nod = 0f;
				if (!_asFigure && !_asHost && !swimming)
				{
					Attention.Head(a, new Vector2(x, y), heading, speed, body, num5, out yaw, out nod);
				}
				SceneClock.Sub(SceneClock.B.Pose);
				personRig.Pose(mesh, num6, phase, 0f, PoseClock.Now + (float)(a.GetHashCode() & 0x3F), PersonRig.HeadSign * yaw, nod, run, walker);
				Mutations.Animate(a, mesh, phase, num6);
				SceneClock.Sub(SceneClock.B.Extras);
				WeaponKind weaponKind2 = ToolMeshes.SwingOf(text3);
				if (flag4 && (weaponKind2 != WeaponKind.None || text3 == "basket" || text3 == "bucket"))
				{
					float num8 = WorkStroke(a, model, text3, weaponKind2);
					personRig.Fight(mesh, weaponKind2, num8, (weaponKind2 == WeaponKind.None) ? 0f : 0.35f, flinch, 0f, 1f);
					attack = num8;
				}
				else
				{
					personRig.Fight(mesh, weaponKind, attack, model.Stance, flinch, lean, model.Side);
				}
				if (flag3)
				{
					FirstPerson.Core.Drill.Settle(personRig, mesh, weaponKind, attack, model.Stance, s.Guard ? 0f : (1f - model.Stance), PoseClock.Now, (float)(a.GetHashCode() & 0xFF) / 255f);
				}
				if (Blocks.PoseOf(a, out var e, out var age))
				{
					Ward.Pose(personRig, mesh, weaponKind, e.Kind == Stop.Dodged, age, ((e.Serial & 1) == 0) ? 1f : (-1f), e.Weight);
				}
				if (Headshots.KnockOf(a, out var age2))
				{
					Ward.HeadKnock(personRig, mesh, age2, ((a.GetHashCode() & 1) == 0) ? 1f : (-1f));
				}
				if (flag7)
				{
					personRig.Shamble(mesh, 1f - model.Stance, phase, (float)(a.GetHashCode() & 0xFF) / 255f, num6, PoseClock.Now);
				}
				if (num7 > 0f && !swimming)
				{
					personRig.Expecting(mesh, num7 * (1f - model.Stance), phase, num6);
				}
				if (carried != null)
				{
					CarryPose(personRig, mesh, carried, phase, num6);
				}
				WaterPose(a, model, personRig, mesh, x, y, body, speed, phase, num6, swimming, _asHost || template.Tag == 2147483646, num5);
				if (flag2 && personRig.Head >= 0 && !personRig.ChildBody)
				{
					mesh.PartScale[personRig.Head] = 1.28f;
				}
				if (model.Stage == Stage.Elder && personRig.Torso >= 0)
				{
					mesh.PartRotation[personRig.Torso] = mesh.PartRotation[personRig.Torso] * Mat3.RotX(0.16f);
				}
				RestPose rest = ((walker != null && sit <= 0f && lie <= 0f) ? RestOf(a, personRig, new Vector2(x, y), heading, speed, model.Stance < 0.05f && !flag4 && !flag3 && num7 <= 0f && weaponKind == WeaponKind.None && text4 == null && text3 == null && carried == null, flag2, body, distance, num5) : default(RestPose));
				seated = Mathf.Max(sit, rest.On ? rest.Amount : 0f);
				if (walker != null && !_asFigure && !_asHost)
				{
					StandAbout(model, a, personRig, mesh, speed, model.Stance < 0.05f && !flag4 && !flag3 && num7 <= 0f && seated <= 0f && lie <= 0f, weaponKind != WeaponKind.None || text4 != null || text3 != null || carried != null, flag2, num5);
				}
				personRig.Sit(mesh, sit);
				s2 = body * LookTall(model) / Mathf.Max(0.1f, personRig.Height);
				float num9 = z;
				ApplyRest(personRig, mesh, rest, s2, ref z, a);
				if (rest.On && !float.IsNaN(rest.Heading))
				{
					heading = rest.Heading;
				}
				LookPose(model, personRig, mesh, walker, phase, num6, run, weaponKind == WeaponKind.None && text4 == null && text3 == null && carried == null && !flag4 && !flag3 && seated <= 0f && lie <= 0f && model.Stance < 0.05f, swimming);
				KeepPose(model, meshModel, z - num9, (rest.On && !float.IsNaN(rest.Heading)) ? rest.Heading : float.NaN, s2, seated, attack);
			}
			SceneClock.Sub(SceneClock.B.Place);
			PersonRig.Place(mesh, x, y, z, heading, s2);
			if (lie > 0f)
			{
				PersonRig.LayDown(mesh, lie);
			}
			WaterPlace(a, model, mesh, x, y, z, heading, body);
			if (walker != null && model.Stance < 0.05f && !flag4 && !flag3 && !_asFigure && distance < 30f && seated <= 0f && lie <= 0f)
			{
				personRig.Plant(mesh, walker);
			}
			else
			{
				model.Walk.Unplant();
			}
			SceneClock.Sub(SceneClock.B.Gear);
			if (carried != null)
			{
				CarryPlace(model, personRig, mesh, carried, body);
			}
			Archer.Bow bow = default(Archer.Bow);
			if (weaponKind == WeaponKind.Bow && !flag2 && carried == null)
			{
				float num10 = Mathf.Cos(model.AimPitch);
				bow = Archer.Shoot(aim: new Vec3(Mathf.Cos(num) * num10, Mathf.Sin(num) * num10, Mathf.Sin(model.AimPitch)), rig: personRig, m: mesh, stance: model.Stance, attack: attack, height: body);
				if (flag3)
				{
					Drill.Bowed(a, bow);
				}
			}
			if (!flag2)
			{
				AddTorch(meshes, a, model, personRig, mesh, weaponKind, body, heading, distance, swimming, flag4 || flag3 || seated > 0f || lie > 0f || text4 != null || text3 != null, template);
			}
			if (!swimming)
			{
				AddLookAid(meshes, model, personRig, mesh, template, distance);
			}
			mesh.Tag = template.Tag;
			mesh.Flash = template.Flash;
			mesh.Multiply = template.Multiply;
			mesh.NoShadow = template.NoShadow;
			mesh.SortDepth = template.SortDepth;
			mesh.KeepSmall = distance > 20f;
			CrowdFade(model, mesh, personRig, flag2);
			meshes.Add(mesh);
			StuckArrows.Wear(meshes, a, mesh, person: true, template.Tag, template.Flash, template.SortDepth);
			SceneClock.Sub(SceneClock.B.Head);
			if (template.Tag != 2147483646)
			{
				Hitboxes.Record(a, mesh, person: true);
			}
			SceneClock.Sub(SceneClock.B.Gear);
			if (!flag2 && distance < 40f * Models.LodZoom)
			{
				AddArmor(meshes, a, model, personRig, mesh, template);
			}
			if (Bandages.Active.Count > 0 && distance < 40f * Models.LodZoom)
			{
				AddWrap(meshes, a, model, mesh, template);
			}
			AddRegalia(meshes, a, model, personRig, mesh, text, distance, body, speed, num4, baseSimObject != null, flag3, flag4, swimming, Mathf.Max(seated, lie), weaponKind, text3, text4, sprites, template);
			if (flag2 || distance > 45f * Models.LodZoom || swimming)
			{
				return;
			}
			model.WeaponDetail = ((!_asFigure) ? WeaponDetailAt(distance, model.WeaponDetail) : 0);
			if ((carried != null && AddCarried(meshes, model, carried, body, template)) || ((text4 != null || text3 != null) && AddTool(meshes, a, model, personRig, mesh, text3, text4, flag4 ? attack : 0f, body, heading, sprites, template)) || AddHalberd(meshes, a, model, personRig, mesh, weaponKind, body, template))
			{
				return;
			}
			switch (weaponKind)
			{
			case WeaponKind.Bow:
				AddBow(meshes, model, bow, body, template);
				break;
			case WeaponKind.Shotgun:
				AddShotgun(meshes, a, model, personRig, mesh, attack, body, num, model.Stance, template);
				break;
			default:
				AddWeapon(meshes, model, personRig, mesh, weaponKind, attack, body, template, 1f - model.Stance);
				break;
			case WeaponKind.None:
			{
				if (_asFigure)
				{
					AddFigureItem(voxels, a, personRig, mesh, heading, body, sprites, template);
					break;
				}
				SpriteImage spriteImage = HeldItem(a, sprites);
				if (spriteImage != null)
				{
					Vec3 grip = personRig.HandWorld(mesh);
					Vec3 vec = new Vec3(Mathf.Cos(heading), Mathf.Sin(heading), 0f);
					Vec3 right = new Vec3(vec.Y, 0f - vec.X, 0f);
					Vec3 blade = new Vec3(0f, 0f, 0.8f) + vec * 0.6f;
					VoxelObject item = ItemPose.HoldItem(spriteImage, grip, blade, right, body * 0.3f, overlay: false);
					item.Tag = template.Tag;
					item.Flash = template.Flash;
					item.NoShadow = template.NoShadow;
					item.SortDepth = template.SortDepth;
					voxels.Add(item);
				}
				break;
			}
			}
		}

		private static bool IsZombie(Actor a)
		{
			return ((a.asset != null) ? a.asset.id : null)?.StartsWith("zombie", StringComparison.Ordinal) ?? false;
		}

		private static int Seed(Actor a)
		{
			try
			{
				return (int)((a.data != null) ? a.data.id : a.GetHashCode());
			}
			catch
			{
				return 0;
			}
		}

		internal static bool ColorsOf(Actor a, out Humanoid.Palette p)
		{
			p = default(Humanoid.Palette);
			if (a == null)
			{
				return false;
			}
			if (Figures.TryGetValue(a, out var value) && value.Stage != Stage.Child)
			{
				p = value.Palette;
				return true;
			}
			SpriteImage spriteImage = ((WorldBoxMod.Instance != null) ? WorldBoxMod.Instance.SpriteOf(a) : null);
			if (spriteImage == null || spriteImage.Pixels == null)
			{
				return false;
			}
			p = Humanoid.Extract(spriteImage, (a.asset != null) ? a.asset.id : "", GameSkin(a));
			return true;
		}

		private static bool Risen(Actor a)
		{
			try
			{
				return a != null && a.data != null && a.isAlreadyTransformed();
			}
			catch
			{
				return false;
			}
		}

		private static float AttackSeconds(WeaponKind kind)
		{
			switch (kind)
			{
			case WeaponKind.Sword:
				return 0.5f;
			case WeaponKind.Stick:
			case WeaponKind.Dagger:
				return 0.42f;
			case WeaponKind.Axe:
				return 0.6f;
			case WeaponKind.Hammer:
				return 0.7f;
			case WeaponKind.Spear:
				return 0.55f;
			case WeaponKind.Bow:
				return 0.7f;
			case WeaponKind.Staff:
				return 0.6f;
			case WeaponKind.Gun:
				return 0.3f;
			case WeaponKind.Shotgun:
				return 0.75f;
			default:
				return 0.4f;
			}
		}

		private static void AddWeapon(List<MeshInstance> meshes, Model m, PersonRig rig, MeshInstance body, WeaponKind kind, float attack, float height, VoxelObject template, float rest)
		{
			if (rig.WeaponPlace(body, kind, attack, height, out var position, out var basis, rest, m.Side))
			{
				MeshInstance meshInstance = m.Weapon ?? (m.Weapon = new MeshInstance());
				meshInstance.Model = ViewMeshes.Item(m.Look, m.WeaponDetail);
				meshInstance.Palette = null;
				meshInstance.Basis = basis;
				meshInstance.Position = position;
				meshInstance.Tag = template.Tag;
				meshInstance.Flash = template.Flash;
				meshInstance.NoShadow = template.NoShadow || height < 0.6f;
				meshInstance.SortDepth = template.SortDepth;
				meshInstance.Static = false;
				PoseHeldFlame(meshes, meshInstance, attack);
				NoteGlow(meshInstance);
				meshes.Add(meshInstance);
			}
		}

		private static void AddBow(List<MeshInstance> meshes, Model m, Archer.Bow bow, float height, VoxelObject template)
		{
			MeshInstance meshInstance = m.Weapon ?? (m.Weapon = new MeshInstance());
			MeshInstance str = m.BowString ?? (m.BowString = new MeshInstance());
			MeshInstance arrow = m.Nocked ?? (m.Nocked = new MeshInstance());
			int count = meshes.Count;
			Archer.Add(meshes, bow, m.Look, Models.Get("arrow"), meshInstance, str, arrow, m.WeaponDetail);
			for (int i = count; i < meshes.Count; i++)
			{
				MeshInstance meshInstance2 = meshes[i];
				meshInstance2.Tag = template.Tag;
				meshInstance2.Flash = template.Flash;
				meshInstance2.NoShadow = template.NoShadow || height < 0.6f || meshInstance2 != meshInstance;
				meshInstance2.SortDepth = template.SortDepth;
				meshInstance2.Static = false;
			}
			NoteGlow(meshInstance);
		}

		private static bool AddTool(List<MeshInstance> meshes, Actor a, Model m, PersonRig rig, MeshInstance body, string tool, string load, float work, float height, float heading, SpriteCache sprites, VoxelObject template)
		{
			MeshModel meshModel;
			ToolMeshes.Hold hold;
			if (load != null)
			{
				SpriteImage spriteImage = HeldItem(a, sprites);
				meshModel = ToolMeshes.Load(load, (spriteImage != null) ? AverageColor(spriteImage) : TerrainCache.Rgb(140, 136, 130));
				hold = ToolMeshes.Hold.Hang;
			}
			else
			{
				Carried carried = CarryLook.Tool(tool);
				meshModel = carried?.Model(m.WeaponDetail);
				hold = ToolMeshes.HoldOf(tool);
				if (meshModel == null)
				{
					meshModel = ToolMeshes.Tool(tool);
				}
				else if (carried.Hold == "hang")
				{
					hold = ToolMeshes.Hold.Hang;
				}
				else if (carried.Hold == "upright")
				{
					hold = ToolMeshes.Hold.Upright;
				}
				else if (carried.Hold == "handle")
				{
					hold = ToolMeshes.Hold.Handle;
				}
			}
			if (meshModel == null)
			{
				return false;
			}
			Vec3 position;
			Mat3 basis;
			switch (hold)
			{
			case ToolMeshes.Hold.Hang:
			{
				position = rig.HandWorld(body);
				float num = Mathf.Clamp(height, 0.3f, 3f);
				Vec3 vec = new Vec3(Mathf.Cos(heading), Mathf.Sin(heading), 0f);
				Vec3 vec2 = new Vec3(0f, 0f, 1f);
				basis = new Mat3(Vec3.Cross(vec, vec2) * num, vec * num, vec2 * num);
				break;
			}
			case ToolMeshes.Hold.Upright:
				if (!rig.WeaponPlace(body, WeaponKind.Staff, 0f, height, out position, out basis, 1f))
				{
					return false;
				}
				break;
			case ToolMeshes.Hold.Handle:
				if (tool == "hoe" && work <= 0f)
				{
					if (!rig.WeaponPlace(body, WeaponKind.Staff, 0f, height, out position, out basis, 1f))
					{
						return false;
					}
					break;
				}
				goto default;
			case ToolMeshes.Hold.Small:
				if (!rig.WeaponPlace(body, WeaponKind.Dagger, 0f, height, out position, out basis, 1f))
				{
					return false;
				}
				break;
			default:
			{
				WeaponKind weaponKind = ToolMeshes.SwingOf(tool);
				if (weaponKind == WeaponKind.None)
				{
					weaponKind = WeaponKind.Axe;
				}
				if (!rig.WeaponPlace(body, weaponKind, work, height, out position, out basis, (work > 0f) ? 0f : 1f))
				{
					return false;
				}
				break;
			}
			}
			MeshInstance meshInstance = m.Weapon ?? (m.Weapon = new MeshInstance());
			meshInstance.Model = meshModel;
			meshInstance.Palette = null;
			if (tool == "flag")
			{
				int num2 = meshModel.SlotIndex("cloth");
				Color color = Color.white;
				try
				{
					if (a.kingdom != null)
					{
						color = Royal.ColorOf(a.kingdom);
					}
				}
				catch
				{
				}
				if (num2 >= 0)
				{
					uint num3 = TerrainCache.Rgb((int)(color.r * 255f), (int)(color.g * 255f), (int)(color.b * 255f)) | 0xFF000000u;
					int num4 = meshModel.SlotIndex("cloth_dark");
					uint[] array = m.FlagPalette;
					if (array == null || array.Length != meshModel.SlotNames.Length || array[num2] != num3)
					{
						array = new uint[meshModel.SlotNames.Length];
						array[num2] = num3;
						if (num4 >= 0)
						{
							array[num4] = TerrainCache.Scale(num3, 0.72f) | 0xFF000000u;
						}
						m.FlagPalette = array;
					}
					meshInstance.Palette = array;
				}
			}
			meshInstance.Basis = basis;
			meshInstance.Position = position;
			meshInstance.Tag = template.Tag;
			meshInstance.Flash = template.Flash;
			meshInstance.NoShadow = template.NoShadow || height < 0.6f;
			meshInstance.SortDepth = template.SortDepth;
			meshInstance.Static = false;
			meshes.Add(meshInstance);
			return true;
		}

		private static uint AverageColor(SpriteImage img)
		{
			if (AverageColors.TryGetValue(img, out var value))
			{
				return value;
			}
			long num = 0L;
			long num2 = 0L;
			long num3 = 0L;
			long num4 = 0L;
			uint[] pixels = img.Pixels;
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
			value = ((num4 > 0) ? TerrainCache.Rgb((int)(num / num4), (int)(num2 / num4), (int)(num3 / num4)) : TerrainCache.Rgb(140, 136, 130));
			if (AverageColors.Count > 256)
			{
				AverageColors.Clear();
			}
			AverageColors[img] = value;
			return value;
		}

		private static void AddArmor(List<MeshInstance> meshes, Actor a, Model m, PersonRig rig, MeshInstance body, VoxelObject template)
		{
			ProbeArmor(m, rig, body);
			if (body.Model != null && body.Model.Info("armor") == "none")
			{
				m.ArmorShown = 0;
				return;
			}
			if (_frame - m.ArmorChecked > 120)
			{
				m.ArmorChecked = _frame;
				m.Helmet = (m.Chest = (m.Boots = null));
				try
				{
					if (a.equipment != null)
					{
						m.Helmet = ItemId(a.equipment.helmet);
						m.Chest = ItemId(a.equipment.armor);
						m.Boots = ItemId(a.equipment.boots);
					}
				}
				catch
				{
				}
				if (m.Helmet == null && m.Chest == null && m.Boots == null && IsZombie(a))
				{
					ZombieGear.Wore(a, out m.Helmet, out m.Chest, out m.Boots);
				}
			}
			m.ArmorShown = 0;
			if (m.Helmet == null && m.Chest == null && m.Boots == null)
			{
				return;
			}
			string text = ((m.Expecting == 0) ? m.Chest : null);
			string text2 = (rig.Hat ? null : m.Helmet);
			int num = (m.ArmorShown = (ArmorMeshes.Remember ? ArmorMeshes.Dress(body, m.BaseModel ?? body.Model, text2, text, m.Boots, m.Armor, ref MemoOf(m).Dressed) : ArmorMeshes.Dress(body, m.BaseModel ?? body.Model, text2, text, m.Boots, m.Armor)));
			for (int i = 0; i < num; i++)
			{
				MeshInstance meshInstance = m.Armor[i];
				meshInstance.Tag = template.Tag;
				meshInstance.Flash = template.Flash;
				meshInstance.Multiply = template.Multiply;
				meshInstance.NoShadow = template.NoShadow;
				meshInstance.SortDepth = template.SortDepth;
				meshInstance.Static = false;
				meshes.Add(meshInstance);
			}
			LookUnderArmour(body, text != null, m.Boots != null);
			if (ArmorMeshes.Remember)
			{
				HairAndShoes(ref MemoOf(m).Hair, rig, body, text2, m.Boots);
				return;
			}
			if (ArmorMeshes.HidesHair(text2))
			{
				body.Hidden |= LookHairMask(body.Model);
				body.Hide("hair_short");
				body.Hide("hair_long");
				body.Hide("fem_bow");
			}
			if (m.Boots != null && rig.SlotShoes >= 0 && body.Palette != null)
			{
				body.Palette[rig.SlotShoes] = TerrainCache.Scale(ArmorMeshes.ShoeColor(m.Boots), 0.85f) | 0xFF000000u;
			}
		}

		public static string ChestArmor(Actor a)
		{
			try
			{
				return (a != null && a.equipment != null) ? ItemId(a.equipment.armor) : null;
			}
			catch
			{
				return null;
			}
		}

		internal static string ItemIdOf(ActorEquipmentSlot slot)
		{
			return ItemId(slot);
		}

		private static string ItemId(ActorEquipmentSlot slot)
		{
			if (slot == null || slot.isEmpty())
			{
				return null;
			}
			Item item = slot.getItem();
			if (item == null || item.getAsset() == null)
			{
				return null;
			}
			return item.getAsset().id;
		}

		public static int AgeOf(Actor a)
		{
			return (int)StageOf(a);
		}

		private static Stage StageOf(Actor a)
		{
			try
			{
				if (!a.isAdult())
				{
					return Stage.Child;
				}
				return (!(a.getAgeRatio() > 0.75f)) ? Stage.Adult : Stage.Elder;
			}
			catch
			{
				return Stage.Adult;
			}
		}

		public static string AgeLabel(Actor a)
		{
			try
			{
				if (!a.isAdult())
				{
					return IsHumanoid(a) ? "child" : "young";
				}
				if (IsHumanoid(a) && a.getAgeRatio() > 0.75f)
				{
					return "elder";
				}
			}
			catch
			{
			}
			return null;
		}

		public static SpriteImage HeldItem(Actor a, SpriteCache sprites)
		{
			try
			{
				if (!a.checkHasRenderedItem())
				{
					return null;
				}
				Item weapon = a.getWeapon();
				if (weapon != null)
				{
					SpriteImage spriteImage = sprites.Get(weapon.getSprite());
					if (spriteImage != null && spriteImage.Width >= 6)
					{
						return spriteImage;
					}
				}
				return sprites.Get(a.getRenderedItemSprite());
			}
			catch
			{
				return null;
			}
		}

		private static bool HoldPose(Actor a, Model m, MeshModel model, float distance, bool busy)
		{
			if (!FarPoses || busy || _asFigure || _asHost || RoomCombat.Drawing || m.Hold == null || m.Hold.Model != model)
			{
				return false;
			}
			float num = distance / Mathf.Max(1f, Models.LodZoom);
			int num2 = ((num >= 120f) ? 4 : ((!(num >= 60f)) ? 1 : 2));
			if (num2 > 1)
			{
				return ((_frame + (a.GetHashCode() & 3)) & (num2 - 1)) != 0;
			}
			return false;
		}

		private static void HeldPose(Model m, float wdt, ref float heading, ref float z, out float s, out float seated, ref float attack)
		{
			Held hold = m.Hold;
			hold.Debt += wdt;
			z += hold.RestLift;
			if (!float.IsNaN(hold.RestHeading))
			{
				heading = hold.RestHeading;
			}
			s = hold.Scale;
			seated = hold.Seated;
			attack = hold.Attack;
			PosesHeld++;
		}

		private static void KeepPose(Model m, MeshModel model, float restLift, float restHeading, float s, float seated, float attack)
		{
			Held obj = m.Hold ?? (m.Hold = new Held());
			obj.Model = model;
			obj.Debt = 0f;
			obj.RestLift = restLift;
			obj.RestHeading = restHeading;
			obj.Scale = s;
			obj.Seated = seated;
			obj.Attack = attack;
			PosesMade++;
		}

		internal static void AddFigure(List<MeshInstance> meshes, List<VoxelObject> voxels, Actor a, SpriteImage img, SpriteCache sprites, float x, float y, float z, float body, float heading, float dt)
		{
			Model model = Figure(a, img);
			if (_figure == null || _figureFrom != model)
			{
				_figure = new Model
				{
					Stage = model.Stage,
					Female = model.Female,
					Palette = model.Palette,
					Id = model.Id
				};
				_figureFrom = model;
			}
			_figure.WeaponChecked = (_figure.ArmorChecked = -1000);
			VoxelObject template = new VoxelObject
			{
				Tag = 2147483646,
				SortDepth = 3f
			};
			Figures[a] = _figure;
			_asFigure = true;
			FigureItem = null;
			try
			{
				AddMesh(meshes, voxels, a, img, sprites, x, y, z, body, heading, 0f, 0f, dt, template, swimming: false);
			}
			finally
			{
				_asFigure = false;
				Figures[a] = model;
			}
		}

		private static void AddFigureItem(List<VoxelObject> voxels, Actor a, PersonRig rig, MeshInstance body, float heading, float height, SpriteCache sprites, VoxelObject template)
		{
			FigureItem = null;
			if (WorldBoxMod.Instance == null || a != WorldBoxMod.Instance.Host)
			{
				return;
			}
			string text = BagOrder.ResourceOf(Hotbar.InHand(a));
			if (text != null && sprites != null)
			{
				Sprite sprite = null;
				try
				{
					sprite = (Supplies.Is(text) ? Supplies.Icon(text) : AssetManager.resources.get(text)?.getSpriteIcon());
				}
				catch
				{
				}
				SpriteImage spriteImage = ((sprite != null) ? sprites.Get(sprite) : null);
				if (spriteImage != null && spriteImage.Width >= 2 && spriteImage.Height >= 2)
				{
					Vec3 vec = rig.HandWorld(body);
					Vec3 vec2 = new Vec3(Mathf.Cos(heading), Mathf.Sin(heading), 0f);
					Vec3 vec3 = new Vec3(0f - vec2.Y, vec2.X, 0f);
					Vec3 vec4 = (new Vec3(0f, 0f, 1f) * 0.9f + vec2 * 0.35f).Normalized();
					float num = height * 0.3f / (float)Math.Max(spriteImage.Width, spriteImage.Height);
					VoxelObject item = new VoxelObject
					{
						Shape = spriteImage.SlabShape(1),
						Tag = template.Tag,
						Flash = template.Flash,
						NoShadow = template.NoShadow,
						SortDepth = template.SortDepth,
						U = vec3,
						V = vec4,
						N = Vec3.Cross(vec3, vec4),
						SizeU = num,
						SizeV = num,
						SizeN = num
					};
					item.Origin = vec + vec2 * (num * 1.5f) - item.U * ((float)spriteImage.Width * 0.5f * num) - item.V * ((float)spriteImage.Height * 0.2f * num);
					voxels.Add(item);
					FigureItem = text;
				}
			}
		}

		private static void PoseHeldFlame(List<MeshInstance> meshes, MeshInstance w, float attack)
		{
			if (!FlameSword.IsModel(w.Model))
			{
				return;
			}
			if (_flamesFrame != _frame)
			{
				_flamesFrame = _frame;
				FlamesDrawn.Clear();
			}
			FlameSword value = FlameOf.GetValue(w, (MeshInstance _) => new FlameSword());
			if (_asFigure)
			{
				value.Cutting = false;
				value.Pose(w, Time.unscaledTime);
				return;
			}
			float num = w.Position.X - FlameEye.X;
			float num2 = w.Position.Y - FlameEye.Y;
			bool flag = num * num + num2 * num2 < 400f;
			value.Cutting = flag && attack > 0.3f && attack < 0.6f;
			value.Pose(w, Time.unscaledTime);
			MeshInstance meshInstance = (flag ? value.Arc() : null);
			if (meshInstance != null)
			{
				meshes.Add(meshInstance);
			}
			if (FlamesDrawn.Count < 32)
			{
				FlamesDrawn.Add(value);
			}
		}

		internal static void NoteGlide(Actor a, List<MeshInstance> meshes, int first, bool ok, float ground, float body)
		{
			if (!Figures.TryGetValue(a, out var value))
			{
				return;
			}
			value.GlideOk = false;
			if (!ok || value.Drawn != _frame || value.Crowd || value.CrowdBlend > 0f || value.Mesh == null || value.Mesh.Model == null || value.Fallen)
			{
				return;
			}
			int num = meshes.Count - first;
			if (num < 1 || num > 25)
			{
				return;
			}
			MeshInstance mesh = value.Mesh;
			int num2 = -1;
			for (int i = first; i < meshes.Count; i++)
			{
				if (meshes[i] == mesh)
				{
					num2 = i;
					break;
				}
			}
			if (num2 < 0)
			{
				return;
			}
			if (value.GlideParts == null)
			{
				value.GlideParts = new List<MeshInstance>(num);
			}
			if (value.GlideOffsets == null)
			{
				value.GlideOffsets = new List<Vec3>(num);
			}
			value.GlideParts.Clear();
			value.GlideOffsets.Clear();
			for (int j = first; j < meshes.Count; j++)
			{
				if (j != num2)
				{
					MeshInstance meshInstance = meshes[j];
					if (meshInstance == null || meshInstance.Model == null || meshInstance.Cloud != null)
					{
						return;
					}
					value.GlideParts.Add(meshInstance);
					value.GlideOffsets.Add(meshInstance.Position - mesh.Position);
				}
			}
			value.GlideOk = true;
			value.GlideLift = mesh.Position.Z - ground;
			value.GlideBody = body;
		}

		internal static MeshInstance GlideBody(Actor a, out float lift, out float body, out List<MeshInstance> parts, out List<Vec3> offsets)
		{
			lift = (body = 0f);
			parts = null;
			offsets = null;
			if (!Figures.TryGetValue(a, out var value) || !value.GlideOk || value.Drawn != _frame - 1 || value.Mesh == null || value.Mesh.Model == null || value.Crowd || value.CrowdBlend > 0f || value.Fallen)
			{
				return null;
			}
			try
			{
				if (a.data == null || a.data.id != value.Id)
				{
					return null;
				}
			}
			catch
			{
				return null;
			}
			lift = value.GlideLift;
			body = value.GlideBody;
			if (value.GlideParts != null && value.GlideParts.Count > 0)
			{
				parts = value.GlideParts;
				offsets = value.GlideOffsets;
			}
			return value.Mesh;
		}

		internal static void Glid(Actor a)
		{
			if (Figures.TryGetValue(a, out var value))
			{
				value.Drawn = _frame;
				value.Seen = _frame;
			}
			Glided++;
		}

		private static float HostAttack(WeaponKind kind)
		{
			CombatFeedback combatFeedback = ((WorldBoxMod.Instance != null) ? WorldBoxMod.Instance.Feedback : null);
			if (combatFeedback == null || !combatFeedback.LastAttackMelee)
			{
				return 0f;
			}
			float sinceAttack = combatFeedback.SinceAttack;
			WeaponKind kind2 = ((combatFeedback.LastSwing == Swing.Attack) ? kind : ((kind != WeaponKind.None) ? WeaponKind.Axe : WeaponKind.None));
			StrikeTiming strikeTiming = StrikeTiming.For(kind2, melee: true, hands: true);
			if (sinceAttack >= strikeTiming.Total || sinceAttack < 0f)
			{
				return 0f;
			}
			HostShares(kind2, out var top, out var hit);
			if (sinceAttack < strikeTiming.StrikeStart)
			{
				return top * Mathf.SmoothStep(0f, 1f, sinceAttack / Mathf.Max(0.01f, strikeTiming.StrikeStart));
			}
			if (sinceAttack < strikeTiming.Hit)
			{
				return top + (hit - top) * (sinceAttack - strikeTiming.StrikeStart) / Mathf.Max(0.01f, strikeTiming.Hit - strikeTiming.StrikeStart);
			}
			return hit + (1f - hit) * (sinceAttack - strikeTiming.Hit) / Mathf.Max(0.05f, strikeTiming.Total - strikeTiming.Hit);
		}

		private static float HostStance()
		{
			float a = ((GuardControl.Current != null) ? GuardControl.Current.Raise : 0f);
			CombatFeedback combatFeedback = ((WorldBoxMod.Instance != null) ? WorldBoxMod.Instance.Feedback : null);
			float b = ((combatFeedback != null && combatFeedback.LastSwing == Swing.Attack && combatFeedback.SinceAttack < 1.5f) ? 1f : 0f);
			return Mathf.Max(a, b);
		}

		private static void HostShares(WeaponKind kind, out float top, out float hit)
		{
			switch (kind)
			{
			case WeaponKind.Sword:
			case WeaponKind.Stick:
			case WeaponKind.Dagger:
				top = 0.3f;
				hit = 0.47f;
				break;
			case WeaponKind.Axe:
			case WeaponKind.Hammer:
				top = 0.4f;
				hit = 0.52f;
				break;
			case WeaponKind.Spear:
				top = 0.3f;
				hit = 0.42f;
				break;
			case WeaponKind.Staff:
				top = 0.3f;
				hit = 0.35f;
				break;
			default:
				top = 0.12f;
				hit = 0.25f;
				break;
			}
		}

		private static void StandAbout(Model m, Actor a, PersonRig rig, MeshInstance inst, float speed, bool free, bool handsBusy, bool child, float dt)
		{
			Standing standing = m.Standing;
			if (standing == null)
			{
				Standing obj = new Standing
				{
					Rng = (uint)((a.GetHashCode() * -2048144777) | 1)
				};
				Standing standing2 = obj;
				m.Standing = obj;
				standing = standing2;
			}
			Standing standing3 = standing;
			bool flag = free && speed < 0.05f;
			standing3.Still = (flag ? (standing3.Still + dt) : 0f);
			if (standing3.Kind != PersonRig.Fidget.None)
			{
				standing3.T += dt;
				bool flag2 = !flag || standing3.T >= standing3.Length;
				standing3.Amount = Mathf.MoveTowards(standing3.Amount, flag2 ? 0f : 1f, dt / 0.4f);
				if (flag2 && standing3.Amount <= 0f)
				{
					standing3.Kind = PersonRig.Fidget.None;
					standing3.Wait = 1f + 3f * Next(standing3);
				}
			}
			else if (flag && standing3.Still > 1.2f)
			{
				standing3.Wait -= dt;
				if (standing3.Wait <= 0f)
				{
					float num = Next(standing3);
					standing3.Kind = ((num < 0.3f) ? PersonRig.Fidget.WeightShift : ((!child) ? ((num < 0.5f) ? PersonRig.Fidget.ArmsCrossed : ((num < 0.66f) ? PersonRig.Fidget.HandsOnHips : ((num < 0.82f) ? PersonRig.Fidget.HandsBehind : ((num < 0.93f) ? PersonRig.Fidget.ScratchHead : PersonRig.Fidget.Stretch)))) : ((num < 0.75f) ? PersonRig.Fidget.ScratchHead : PersonRig.Fidget.Stretch)));
					float num2 = PersonRig.FidgetSeconds(standing3.Kind);
					standing3.Length = ((num2 > 3f) ? (3f + (num2 - 3f) * Next(standing3)) : num2);
					standing3.T = 0f;
					standing3.Amount = 0f;
					standing3.Side = ((Next(standing3) < 0.5f) ? 1f : (-1f));
				}
			}
			if (standing3.Kind != PersonRig.Fidget.None && standing3.Amount > 0f)
			{
				rig.Idle(inst, standing3.Kind, standing3.Amount, standing3.T, standing3.Side, handsBusy);
			}
		}

		private static float Next(Standing s)
		{
			s.Rng ^= s.Rng << 13;
			s.Rng ^= s.Rng >> 17;
			s.Rng ^= s.Rng << 5;
			return (float)(s.Rng & 0xFFFFFF) / 16777216f;
		}

		private static int CoastFrames(float tall)
		{
			if (!(tall < 3f))
			{
				if (!(tall < 5f))
				{
					return 2;
				}
				return 3;
			}
			return 4;
		}

		private static void CoastFrom(Model m, float x, float y, float z, float body, float s, int variant, bool still, float tall)
		{
			LeanRead lean = m.Lean;
			if (lean != null)
			{
				lean.Coasts = still && !CoastOff && !_crowdGear && float.IsNaN(m.Face);
				int num = CoastFrames(tall);
				lean.Due = ((lean.Due <= 0) ? (_frame + 1 + (int)(m.Id % num)) : (_frame + num));
				lean.X = x;
				lean.Y = y;
				lean.Z = z;
				lean.Body = body;
				lean.Scale = s;
				lean.Variant = variant;
			}
		}

		internal static bool Coast(Actor a, float x, float y, TerrainCache terrain, float heading, float speed, float step, out MeshInstance inst, out float z, out float body)
		{
			inst = null;
			z = (body = 0f);
			if (LeanOff || CoastOff || _crowdLine <= 0f || _crowdOut == null || !Figures.TryGetValue(a, out var value))
			{
				return false;
			}
			LeanRead lean = value.Lean;
			if (lean == null || !lean.Coasts || _frame >= lean.Due || !value.Crowd || value.CrowdBlend < 1f || value.Drawn != _frame - 1 || value.Mesh == null || value.Rig == null || value.Mesh.Model != lean.Model || !float.IsNaN(value.Face))
			{
				return false;
			}
			try
			{
				if (!a.isAlive() || a.data == null || a.data.id != value.Id || a.is_inside_building || a.has_attack_target)
				{
					return false;
				}
			}
			catch
			{
				return false;
			}
			inst = value.Mesh;
			body = lean.Body;
			z = lean.Z + terrain.SampleHeight(x, y) - terrain.SampleHeight(lean.X, lean.Y);
			value.Seen = _frame;
			value.Drawn = _frame;
			PersonRig rig = value.Rig;
			float turn = ((step > 0f && !float.IsNaN(value.WalkHeading)) ? (Mathf.DeltaAngle(value.WalkHeading * 57.29578f, heading * 57.29578f) * (MathF.PI / 180f) / step) : 0f);
			value.Walk.Step(speed, body, turn, heading, step, rig.LegShare, rig.Robe);
			value.Walk.Unplant();
			value.WalkHeading = heading;
			CrowdPoses.Motion(lean.Variant, value.Walk.Phase, value.Walk.Stride, value.Walk.Run, out var a2, out var b, out var w);
			PersonRig.Place(inst, x, y, z, heading, lean.Scale);
			inst.Flash = Blocks.FlashOf(a);
			_crowdOut.Add(new CrowdItem
			{
				Body = inst,
				A = a2,
				B = b,
				W = w
			});
			CrowdPosed++;
			Coasted++;
			return true;
		}

		private static void Remember(Actor a, Model m, MeshModel model, bool child, WeaponKind kind, BaseSimObject foe, string tool, string load, Vector2 workAt, bool swimming)
		{
			LeanRead leanRead = m.Lean;
			if (leanRead == null)
			{
				leanRead = (m.Lean = new LeanRead());
				leanRead.Checked = _frame - (int)(m.Id & 7);
			}
			else
			{
				leanRead.Checked = _frame;
			}
			leanRead.Model = model;
			leanRead.Child = child;
			leanRead.Swimming = swimming;
			leanRead.Kind = kind;
			leanRead.Foe = foe;
			leanRead.Tool = tool;
			leanRead.Load = load;
			leanRead.HasWork = false;
			leanRead.WorkAt = default(Vector2);
			if (tool == null)
			{
				return;
			}
			try
			{
				Building beh_building_target = a.beh_building_target;
				WorldTile beh_tile_target = a.beh_tile_target;
				if (beh_building_target != null && beh_building_target.current_tile != null)
				{
					leanRead.WorkAt = new Vector2(beh_building_target.current_tile.posV3.x, beh_building_target.current_tile.posV3.y);
					leanRead.HasWork = true;
				}
				else if (beh_tile_target != null)
				{
					leanRead.WorkAt = new Vector2(beh_tile_target.posV3.x, beh_tile_target.posV3.y);
					leanRead.HasWork = true;
				}
			}
			catch
			{
			}
		}

		private static bool LeanCard(List<MeshInstance> meshes, Actor a, float x, float y, float z, float body, float heading, float speed, float distance, float dt, float walkDt, bool swimming, SpriteCache sprites, VoxelObject template)
		{
			if (LeanOff || _crowdLine <= 0f || _asFigure || _asHost || RoomCombat.Drawing)
			{
				return false;
			}
			if (!Figures.TryGetValue(a, out var value))
			{
				return false;
			}
			LeanRead lean = value.Lean;
			if (lean == null || !value.Crowd || value.CrowdBlend < 1f || value.Drawn != _frame - 1 || value.Mesh == null || value.Rig == null || value.Mesh.Model != lean.Model)
			{
				return false;
			}
			if (_frame - lean.Checked >= 16 || swimming != lean.Swimming)
			{
				return false;
			}
			BaseSimObject baseSimObject;
			double last_attack_timestamp;
			try
			{
				if (a.data == null || a.data.id != value.Id)
				{
					return false;
				}
				baseSimObject = (a.has_attack_target ? a.attack_target : null);
				if (baseSimObject != null && !baseSimObject.isAlive())
				{
					baseSimObject = null;
				}
				last_attack_timestamp = a.last_attack_timestamp;
			}
			catch
			{
				return false;
			}
			if (baseSimObject != lean.Foe)
			{
				return false;
			}
			if (distance < Mathf.Max(45f, 60f) * Models.LodZoom)
			{
				return false;
			}
			float num = (x - _crowdEyeX) * _crowdFx + (y - _crowdEyeY) * _crowdFy;
			if (num <= 0.5f || body / (num * _crowdPixel) >= _crowdLine * 1.08f)
			{
				return false;
			}
			value.Seen = _frame;
			value.Drawn = _frame;
			value.Space = null;
			bool child = lean.Child;
			WeaponKind kind = lean.Kind;
			PersonRig rig = value.Rig;
			MeshInstance mesh = value.Mesh;
			value.Stance = Mathf.MoveTowards(value.Stance, (baseSimObject != null && !swimming && !child) ? 1f : 0f, dt * 3f);
			bool flag = lean.HasWork && speed < 0.15f && (lean.WorkAt - new Vector2(x, y)).sqrMagnitude < 6.7599993f;
			if (baseSimObject != null || flag)
			{
				Vector2 vector = ((baseSimObject != null) ? RoomCombat.Drawn(baseSimObject.current_position) : lean.WorkAt) - new Vector2(x, y);
				if (vector.sqrMagnitude > 0.0001f)
				{
					float target = Mathf.Atan2(vector.y, vector.x);
					if (float.IsNaN(value.Face))
					{
						value.Face = heading;
						value.FaceTurn = 0f;
					}
					value.Face = Turn.Damp(value.Face, ref value.FaceTurn, target, dt, 0.15f, MathF.PI * 3f);
				}
			}
			else if (!float.IsNaN(value.Face))
			{
				value.Face = Turn.Damp(value.Face, ref value.FaceTurn, heading, dt, 0.25f, MathF.PI * 2f);
				if (Mathf.Abs(Mathf.DeltaAngle(value.Face * 57.29578f, heading * 57.29578f)) < 2f)
				{
					value.Face = float.NaN;
				}
			}
			if (!float.IsNaN(value.Face))
			{
				heading = value.Face;
			}
			float attack = 0f;
			if (last_attack_timestamp != value.LastAttack)
			{
				value.LastAttack = last_attack_timestamp;
				value.Side = 0f - value.Side;
			}
			float num2 = WorldPace.Since(last_attack_timestamp);
			float num3 = AttackSeconds(kind);
			if (num2 >= 0f && num2 < num3)
			{
				attack = num2 / num3;
			}
			if (baseSimObject != null)
			{
				try
				{
					if (a.attackedBy != null && a.attackedBy.isAlive())
					{
						value.Away = new Vector2(x, y) - RoomCombat.Drawn(a.attackedBy.current_position);
					}
				}
				catch
				{
				}
			}
			if (WindUps.Phase(a, kind, num3, out var attack2))
			{
				attack = attack2;
			}
			WeaponKind weaponKind = ToolMeshes.SwingOf(lean.Tool);
			bool flag2 = flag && (weaponKind != WeaponKind.None || lean.Tool == "basket" || lean.Tool == "bucket");
			int num4 = (flag2 ? CrowdPoses.WorkRow(weaponKind) : (-1));
			int num5 = ((baseSimObject != null && !flag2) ? CrowdPoses.FightRow(kind, value.Side) : (-1));
			if ((flag2 && num4 < 0) || (baseSimObject != null && !flag2 && num5 < 0))
			{
				return false;
			}
			CrowdPose(meshes, a, value, rig, mesh, lean.Model, x, y, z, body, heading, speed, distance, dt, walkDt, fresh: false, child, kind, attack, num4, num5, weaponKind, lean.Tool, lean.Load, swimming, sprites, template);
			LeanPosed++;
			return true;
		}

		internal static WalkCycle.Walker LegsForTest(Actor a, out MeshInstance mesh, out PersonRig rig, out int age)
		{
			mesh = null;
			rig = null;
			age = int.MaxValue;
			if (a == null || !Figures.TryGetValue(a, out var value) || value.Mesh == null || value.Rig == null)
			{
				return null;
			}
			mesh = value.Mesh;
			rig = value.Rig;
			age = _frame - value.Drawn;
			return value.Walk;
		}

		private static LookMemo LooksOf(Model m)
		{
			return m.Looks ?? (m.Looks = new LookMemo());
		}

		internal static bool LookWhoForTest(Actor a, out Wardrobe.Who who)
		{
			who = default(Wardrobe.Who);
			if (a == null || !Figures.TryGetValue(a, out var value) || value.Looks == null || !value.Looks.Chosen)
			{
				return false;
			}
			who = value.Looks.Who;
			return true;
		}

		internal static bool LookForTest(Actor a, out Wardrobe.Look look, out ulong mask)
		{
			look = default(Wardrobe.Look);
			mask = 0uL;
			if (a == null || !Figures.TryGetValue(a, out var value) || value.Looks == null || !value.Looks.Chosen)
			{
				return false;
			}
			look = value.Looks.Look;
			mask = value.Looks.Mask;
			return true;
		}

		private static string LookFamily(Model m, string family)
		{
			if (!LooksOn || m.Stage != Stage.Child || family == null || !family.StartsWith("person_", StringComparison.Ordinal))
			{
				return family;
			}
			LookMemo lookMemo = LooksOf(m);
			if ((object)lookMemo.Family == family)
			{
				return lookMemo.ChildFamily;
			}
			lookMemo.Family = family;
			bool flag = family.EndsWith("_fem", StringComparison.Ordinal);
			string text = (flag ? family.Substring(0, family.Length - 4) : family) + (flag ? "_child_fem" : "_child");
			lookMemo.ChildFamily = ((Models.Get(text) != null) ? text : family);
			return lookMemo.ChildFamily;
		}

		private static MeshModel LookModel(Model m, MeshModel model)
		{
			if (!LooksOn || model == null || m.Looks == null || !m.Looks.Chosen || !m.Looks.Look.Trousers || model.Info("robe") != "1")
			{
				return model;
			}
			if (Trousered.TryGetValue(model, out var value))
			{
				return value;
			}
			if (Trousered.Count > 64)
			{
				Trousered.Clear();
			}
			value = model.Derive(model.Name, model.Parts);
			value.ParseInfo("robe=0");
			Trousered[model] = value;
			return value;
		}

		private static void LookDress(Actor a, Model m, string family, PersonRig rig, MeshInstance inst)
		{
			if (!LooksOn)
			{
				return;
			}
			LookMemo lookMemo = LooksOf(m);
			if (lookMemo.PartsModel != inst.Model)
			{
				lookMemo.Parts = Wardrobe.PartsOf(inst.Model);
				lookMemo.PartsModel = inst.Model;
			}
			Wardrobe.Parts parts = lookMemo.Parts;
			if (parts != null && parts.Has)
			{
				if (!lookMemo.Chosen || _frame - lookMemo.Checked > 150 + (int)(lookMemo.Who.Id & 0x3F))
				{
					Choose(a, m, family, lookMemo, inst.Model);
				}
				if (lookMemo.MaskModel != inst.Model)
				{
					lookMemo.Mask = Wardrobe.Hidden(parts, in lookMemo.Look);
					lookMemo.Paint = Wardrobe.Paint.Of(inst.Model);
					lookMemo.MaskModel = inst.Model;
				}
				inst.Hidden = (inst.Hidden & ~(m.Female ? rig.PlainWoman : rig.PlainMan)) | lookMemo.Mask;
				Wardrobe.Colour(in lookMemo.Paint, inst.Palette, in lookMemo.Look, (lookMemo.Kingdom != 0) ? lookMemo.Kingdom : m.Palette.Shirt, m.Palette.Skin);
			}
		}

		private static void Choose(Actor a, Model m, string family, LookMemo memo, MeshModel model)
		{
			memo.Checked = _frame;
			memo.Kingdom = KingdomTrim(a);
			Wardrobe.Who w = new Wardrobe.Who
			{
				Race = RaceOf(family),
				Female = m.Female,
				Age = AgeBand(a, m),
				Job = JobOf(a),
				Hurt = HurtOf(a),
				Role = HasRole(a)
			};
			try
			{
				w.Id = a.data.id;
			}
			catch
			{
				w.Id = a.GetHashCode();
			}
			if (memo.Chosen && SameWho(in memo.Who, in w))
			{
				return;
			}
			Wardrobe.Counts counts = Wardrobe.Counts.Of(Wardrobe.PartsOf(((family != null) ? Models.Get(family) : null) ?? Models.Get(StripDetail(model.Name)) ?? model));
			Wardrobe.Counts adult = counts;
			if (m.Stage == Stage.Child)
			{
				MeshModel meshModel = Models.Get((family != null && family.Contains("_child")) ? family.Replace("_child", "") : family);
				if (meshModel != null)
				{
					adult = Wardrobe.Counts.Of(Wardrobe.PartsOf(meshModel));
				}
			}
			memo.Look = Wardrobe.Choose(in w, counts, adult);
			memo.Who = w;
			memo.Chosen = true;
			memo.MaskModel = null;
			memo.Aid = (memo.Look.Crutch ? PersonRig.Aid.Crutch : (memo.Look.Cane ? PersonRig.Aid.Cane : PersonRig.Aid.None));
		}

		private static bool SameWho(in Wardrobe.Who a, in Wardrobe.Who b)
		{
			if (a.Id == b.Id && a.Female == b.Female && a.Age == b.Age && a.Job == b.Job && a.Hurt == b.Hurt && a.Race == b.Race)
			{
				return a.Role == b.Role;
			}
			return false;
		}

		private static uint KingdomTrim(Actor a)
		{
			try
			{
				if (a.kingdom == null || !a.kingdom.isCiv())
				{
					return 0u;
				}
				Color32 colorMain = a.kingdom.getColor().getColorMain32();
				return TerrainCache.Rgb(colorMain.r, colorMain.g, colorMain.b);
			}
			catch
			{
				return 0u;
			}
		}

		internal static bool HasRole(Actor a)
		{
			try
			{
				UnitProfession profession = a.getProfession();
				return profession == UnitProfession.King || profession == UnitProfession.Leader || profession == UnitProfession.Warrior;
			}
			catch
			{
				return false;
			}
		}

		private static string StripDetail(string name)
		{
			if (name == null || (!name.EndsWith("_m", StringComparison.Ordinal) && !name.EndsWith("_f", StringComparison.Ordinal)) || name.EndsWith("_fem", StringComparison.Ordinal))
			{
				return name;
			}
			return name.Substring(0, name.Length - 2);
		}

		private static string RaceOf(string family)
		{
			if (family == null)
			{
				return "";
			}
			if (family.Contains("_elf"))
			{
				return "elf";
			}
			if (family.Contains("_orc"))
			{
				return "orc";
			}
			if (family.Contains("_dwarf"))
			{
				return "dwarf";
			}
			return "human";
		}

		private static Wardrobe.Age AgeBand(Actor a, Model m)
		{
			if (m.Stage == Stage.Child)
			{
				return Wardrobe.Age.Child;
			}
			if (m.Stage == Stage.Elder)
			{
				return Wardrobe.Age.Elder;
			}
			try
			{
				return (a.getAgeRatio() < 0.3f) ? Wardrobe.Age.Young : Wardrobe.Age.Grown;
			}
			catch
			{
				return Wardrobe.Age.Grown;
			}
		}

		internal static Wardrobe.Job JobOf(Actor a)
		{
			try
			{
				string text = ((a.citizen_job != null) ? a.citizen_job.id : null);
				if (text != null)
				{
					if (text.StartsWith("farm", StringComparison.Ordinal))
					{
						return Wardrobe.Job.Farmer;
					}
					if (text.StartsWith("wood", StringComparison.Ordinal))
					{
						return Wardrobe.Job.Woodcutter;
					}
					if (text.StartsWith("miner", StringComparison.Ordinal))
					{
						return Wardrobe.Job.Miner;
					}
					if (text == "builder" || text == "road_builder")
					{
						return Wardrobe.Job.Builder;
					}
					if (text.StartsWith("gatherer", StringComparison.Ordinal))
					{
						return Wardrobe.Job.Gatherer;
					}
					if (text.StartsWith("hunt", StringComparison.Ordinal))
					{
						return Wardrobe.Job.Hunter;
					}
					if (text.Contains("fish"))
					{
						return Wardrobe.Job.Fisher;
					}
					if (text.Contains("smith"))
					{
						return Wardrobe.Job.Smith;
					}
				}
				Building homeBuilding = a.getHomeBuilding();
				if (homeBuilding != null)
				{
					ShopKind shopKind = Shops.Of(homeBuilding);
					if (shopKind != ShopKind.None && Shops.Keeper(homeBuilding) == a)
					{
						return (shopKind == ShopKind.Smithy) ? Wardrobe.Job.Smith : Wardrobe.Job.Trader;
					}
				}
			}
			catch
			{
			}
			return Wardrobe.Job.None;
		}

		private static Wardrobe.Hurt HurtOf(Actor a)
		{
			Wardrobe.Hurt hurt = Wardrobe.Hurt.None;
			try
			{
				if (a.hasTrait("eyepatch"))
				{
					hurt |= Wardrobe.Hurt.Eyepatch;
				}
				if (a.hasTrait("skin_burns"))
				{
					hurt |= Wardrobe.Hurt.Burns;
				}
				if (a.hasTrait("crippled"))
				{
					hurt |= Wardrobe.Hurt.Crippled;
				}
				if (a.data != null && a.data.kills >= 12)
				{
					hurt |= Wardrobe.Hurt.Scars;
				}
			}
			catch
			{
			}
			return hurt;
		}

		private static void LookPose(Model m, PersonRig rig, MeshInstance inst, WalkCycle.Walker walker, float phase, float stride, float run, bool handsFree, bool swimming)
		{
			LookMemo looks = m.Looks;
			if (looks != null)
			{
				looks.AidHeld = false;
			}
			if (!LooksOn || looks == null || !looks.Chosen || looks.MaskModel == null)
			{
				return;
			}
			Wardrobe.Shape(rig, inst, looks.Look.Build);
			if (swimming)
			{
				looks.AidHeld = false;
				return;
			}
			if (looks.Look.Limp || looks.Look.Crutch)
			{
				rig.Hobble(inst, phase, stride);
			}
			looks.AidHeld = looks.Aid != PersonRig.Aid.None && handsFree && walker != null;
			if (looks.AidHeld)
			{
				looks.AidTip = rig.HoldAid(inst, looks.Aid, phase, stride, run);
			}
		}

		internal static uint LookSleeve(Actor a)
		{
			if (!LooksOn || a == null || !Figures.TryGetValue(a, out var value) || value.Looks == null || !value.Looks.Chosen || value.Looks.MaskModel == null)
			{
				return 0u;
			}
			return value.Looks.Look.Shirt | 0xFF000000u;
		}

		private static float LookTall(Model m)
		{
			if (!LooksOn || m.Looks == null || !m.Looks.Chosen || m.Looks.MaskModel == null)
			{
				return 1f;
			}
			return m.Looks.Look.Tall;
		}

		private static void AddLookAid(List<MeshInstance> meshes, Model m, PersonRig rig, MeshInstance body, VoxelObject template, float distance)
		{
			LookMemo looks = m.Looks;
			if (looks != null && looks.AidHeld && !(distance > 45f * Models.LodZoom))
			{
				MeshModel meshModel = Models.Detailed((looks.Aid == PersonRig.Aid.Crutch) ? "prop_crutch" : "prop_cane", distance);
				if (meshModel != null)
				{
					MeshInstance meshInstance = looks.AidMesh ?? (looks.AidMesh = new MeshInstance());
					rig.AidPlace(body, looks.Aid, looks.AidTip, out var position, out var basis);
					meshInstance.Model = meshModel;
					meshInstance.Position = position;
					meshInstance.Basis = basis;
					meshInstance.Tag = template.Tag;
					meshInstance.Flash = template.Flash;
					meshInstance.Multiply = template.Multiply;
					meshInstance.NoShadow = template.NoShadow;
					meshInstance.SortDepth = template.SortDepth;
					meshInstance.Static = false;
					meshes.Add(meshInstance);
				}
			}
		}

		internal static string LookWords(Actor a)
		{
			if (!LooksOn || a == null || !Figures.TryGetValue(a, out var value) || value.Looks == null || !value.Looks.Chosen || value.Looks.MaskModel == null)
			{
				return null;
			}
			Wardrobe.Look look = value.Looks.Look;
			bool female = value.Female;
			bool flag = value.Stage == Stage.Child;
			bool flag2 = value.Stage == Stage.Elder;
			bool flag3 = look.Hat == Wardrobe.Hat.Hunter || look.Hat == Wardrobe.Hat.Hood || look.Hat == Wardrobe.Hat.Scarf;
			string text = ((look.Build == Wardrobe.Build.Stout) ? "stout" : ((look.Build == Wardrobe.Build.Thin) ? "thin" : ((look.Build == Wardrobe.Build.Broad) ? "broad-shouldered" : ((look.Tall > 1.03f) ? "tall" : ((look.Tall < 0.97f) ? "short" : null)))));
			string text2 = (flag3 ? "hooded" : ((look.Hair < 0) ? "bald" : HairWord(value.Palette.Hair)));
			string text3 = (look.Patch ? "one-eyed" : (look.Crutch ? "lame" : (look.Scar ? "scarred" : (look.Burns ? "burnt" : ((look.Hair < 0 && !flag3) ? "bald" : ((text != null && (look.Key() & 1) != 0L) ? text : text2))))));
			string text4 = ((look.Job == Wardrobe.Job.None) ? null : ((look.Job == Wardrobe.Job.Elder) ? "elder" : look.Job.ToString().ToLowerInvariant()));
			string text5;
			if (text4 != null)
			{
				text5 = Lang.Format("the {0} {1}", text3, text4);
			}
			else if (flag2)
			{
				text5 = Lang.Format("the {0} elder", text3);
			}
			else
			{
				string text6 = value.Looks.Who.Race ?? "";
				text5 = Lang.Format("the {0} {1} {2}", text3, text6, (!flag) ? (female ? "woman" : "man") : (female ? "girl" : "boy"));
			}
			if (text5.Length <= 0)
			{
				return text5;
			}
			return char.ToUpperInvariant(text5[0]) + text5.Substring(1);
		}

		private static string HairWord(uint c)
		{
			float num = (float)(c & 0xFF) / 255f;
			float num2 = (float)((c >> 8) & 0xFF) / 255f;
			float num3 = (float)((c >> 16) & 0xFF) / 255f;
			float num4 = Mathf.Max(num, Mathf.Max(num2, num3));
			float num5 = Mathf.Min(num, Mathf.Min(num2, num3));
			float num6 = (num4 + num5) * 0.5f;
			float num7 = num4 - num5;
			float num8 = ((num7 < 0.0001f) ? 0f : (num7 / (1f - Mathf.Abs(2f * num6 - 1f) + 0.0001f)));
			if (num8 < 0.18f)
			{
				if (!(num6 > 0.72f))
				{
					if (!(num6 > 0.45f))
					{
						return "dark-haired";
					}
					return "grey-haired";
				}
				return "white-haired";
			}
			if (num6 < 0.2f)
			{
				return "dark-haired";
			}
			float num9 = ((num4 == num) ? (60f * ((num2 - num3) / num7 % 6f)) : ((num4 == num2) ? (60f * ((num3 - num) / num7 + 2f)) : (60f * ((num - num2) / num7 + 4f))));
			if (num9 < 0f)
			{
				num9 += 360f;
			}
			if ((num9 < 32f || num9 > 340f) && num8 > 0.35f)
			{
				if (!(num6 > 0.62f))
				{
					return "red-haired";
				}
				return "fair-haired";
			}
			if (num9 < 70f && num6 > 0.48f)
			{
				return "fair-haired";
			}
			if (num9 > 160f && num9 < 300f)
			{
				return "dark-haired";
			}
			if (!(num6 < 0.33f))
			{
				return "brown-haired";
			}
			return "dark-haired";
		}

		private static void LookUnderArmour(MeshInstance body, bool chest, bool boots)
		{
			if (!LooksOn || (!chest && !boots))
			{
				return;
			}
			MeshModel model = body.Model;
			if (chest)
			{
				body.Hidden |= Mask(ChestHides, model, (string n) => n.StartsWith("cut_", StringComparison.Ordinal) || n.StartsWith("belt_", StringComparison.Ordinal) || (n.StartsWith("job_", StringComparison.Ordinal) && !n.StartsWith("job_fisher_", StringComparison.Ordinal)));
			}
			if (boots)
			{
				body.Hidden |= Mask(BootsHide, model, (string n) => n.StartsWith("job_fisher_", StringComparison.Ordinal));
			}
		}

		private static ulong LookHairMask(MeshModel model)
		{
			if (model != null)
			{
				return Mask(HairHides, model, (string n) => (n.StartsWith("hair_", StringComparison.Ordinal) && n != "hair_short" && n != "hair_long") || n.StartsWith("hat_", StringComparison.Ordinal));
			}
			return 0uL;
		}

		private static ulong Mask(Dictionary<MeshModel, ulong> cache, MeshModel model, Func<string, bool> which)
		{
			if (cache.TryGetValue(model, out var value))
			{
				return value;
			}
			value = 0uL;
			for (int i = 0; i < model.Parts.Length && i < 64; i++)
			{
				if (model.Parts[i].Name != null && which(model.Parts[i].Name))
				{
					value |= (ulong)(1L << i);
				}
			}
			if (cache.Count > 256)
			{
				cache.Clear();
			}
			cache[model] = value;
			return value;
		}

		private static string FarName(string family)
		{
			if (family == null)
			{
				return "_f";
			}
			if (!FarNames.TryGetValue(family, out var value))
			{
				value = (FarNames[family] = family + "_f");
			}
			return value;
		}

		private static void AddRegalia(List<MeshInstance> meshes, Actor a, Model m, PersonRig rig, MeshInstance body, string family, float distance, float height, float speed, float wdt, bool fighting, bool drilling, bool working, bool swimming, float sit, WeaponKind kind, string tool, string load, SpriteCache sprites, VoxelObject template)
		{
			if (!RegaliaOn || family == null)
			{
				return;
			}
			if (_rgFrame != _frame)
			{
				NewRegaliaFrame();
			}
			bool flag = family == "person_human" || family == "person_human_fem";
			if (!flag && !Peoples(family))
			{
				return;
			}
			long timestamp = Stopwatch.GetTimestamp();
			try
			{
				RoleInfo role = Regalia.RoleOf(a, _asFigure);
				if (m.Stage != Stage.Child && (role.Has(RoleBits.King) || HairFixes.TryGetValue(m, out var _)))
				{
					FixKingsHair(a, m, rig, body, sprites, role.Has(RoleBits.King));
				}
				if (flag && role.Wears && role.Kingdom.Valid && Models.Ready)
				{
					Worn value2 = WornOf.GetValue(body, (MeshInstance _) => new Worn());
					long id;
					try
					{
						id = a.data.id;
					}
					catch
					{
						return;
					}
					if (value2.Id != id)
					{
						value2.Id = id;
						value2.GearSerial = -1;
						value2.W.Seed = (float)((ulong)(id * 2654435761u) % 1000uL) / 1000f;
						value2.Drawn = -10;
					}
					if (value2.Drawn < _frame - 1)
					{
						value2.W.Calm();
						value2.LastHeading = float.NaN;
					}
					value2.Frame = _frame;
					value2.Role = role;
					if (RegaliaKit.Library == null)
					{
						RegaliaKit.Library = Models.Get;
					}
					if (RegaliaDress.Warn == null)
					{
						RegaliaDress.Warn = Log.Info;
					}
					bool flag2 = fighting || drilling || working || load != null || tool != null;
					if (value2.GearSerial != role.Serial || value2.GearBusy != flag2 || _asFigure)
					{
						value2.Gear = RegaliaRules.Compose(role.Bits, role.Seat, role.Kingdom.Style, role.HelmetId, flag2);
						value2.GearSerial = role.Serial;
						value2.GearBusy = flag2;
					}
					float num = distance / Models.LodZoom;
					bool flag3 = num < 40f && m.Stage != Stage.Child;
					MeshModel meshModel = m.BaseModel ?? body.Model;
					WearContext c = new WearContext
					{
						Female = m.Female,
						Child = (m.Stage == Stage.Child),
						Figure = _asFigure,
						Host = _asHost,
						Expecting = (m.Expecting > 0),
						Swimming = swimming,
						HandsBusy = flag2,
						Working = working,
						Carrying = (load != null || tool != null),
						Distance = num,
						Sit = sit,
						Stance = m.Stance,
						Lod = RegaliaKit.LodOf(meshModel),
						HelmetId = ((!flag3) ? role.HelmetId : (rig.Hat ? null : m.Helmet)),
						ChestId = (flag3 ? m.Chest : null),
						Kind = kind
					};
					try
					{
						c.Indoors = a.is_inside_building;
					}
					catch
					{
					}
					if (value2.Gear.Hand == HandGear.Baton || value2.Gear.Hand == HandGear.Ledger || value2.Gear.Hand == HandGear.Staff)
					{
						c.TorchHeld = TorchHeld(m);
					}
					MeshModel fit = (Models.IsDistant(meshModel) ? Models.Get(FarName(family)) : meshModel);
					RegaliaDress.Dressing d = new RegaliaDress.Dressing
					{
						Body = body,
						Fit = fit,
						Rig = rig,
						Colors = role.Kingdom.Colors,
						Armour = m.Armor,
						ArmourShown = (flag3 ? m.ArmorShown : 0),
						OwnHidden = OwnHair(m, rig, body),
						Template = template
					};
					SwayOf(value2, body, m, speed, height, wdt, out d.Sway);
					int triangles;
					int missing;
					int num2 = RegaliaDress.Dress(value2.W, in value2.Gear, in c, in d, meshes, out triangles, out missing);
					RegaliaSkipped += missing;
					_rgInstances += num2;
					_rgTriangles += triangles;
					if (num2 > 0 || value2.W.Plan.Halberd)
					{
						value2.Drawn = _frame;
					}
				}
			}
			finally
			{
				_rgTicks += Stopwatch.GetTimestamp() - timestamp;
			}
		}

		private static bool Peoples(string family)
		{
			if (!family.StartsWith("person_elf", StringComparison.Ordinal) && !family.StartsWith("person_orc", StringComparison.Ordinal))
			{
				return family.StartsWith("person_dwarf", StringComparison.Ordinal);
			}
			return true;
		}

		private static void NewRegaliaFrame()
		{
			RegaliaInstances = _rgInstances;
			RegaliaTriangles = _rgTriangles;
			RegaliaMs = (double)_rgTicks * 1000.0 / (double)Stopwatch.Frequency;
			_rgInstances = (_rgTriangles = 0);
			_rgTicks = 0L;
			_rgFrame = _frame;
			RegaliaFit.NewFrame();
			if (_fallenFrame < _frame - 1)
			{
				FallenRegaliaForTest = 0;
			}
		}

		private static bool TorchHeld(Model m)
		{
			if (TorchOf.TryGetValue(m, out var value))
			{
				return TorchesDrawn.Contains(value.Fire);
			}
			return false;
		}

		private static ulong OwnHair(Model m, PersonRig rig, MeshInstance body)
		{
			ulong num = ((m.Looks != null && m.Looks.Chosen && m.Looks.MaskModel == body.Model) ? m.Looks.Mask : 0);
			int num2 = (m.Palette.LongHair ? rig.HairShort : rig.HairLong);
			if (num2 >= 0 && num2 < 64)
			{
				num |= (ulong)(1L << num2);
			}
			return num;
		}

		private static bool AddHalberd(List<MeshInstance> meshes, Actor a, Model m, PersonRig rig, MeshInstance body, WeaponKind kind, float height, VoxelObject template)
		{
			if (!RegaliaOn || !GuardHalberds || !WornOf.TryGetValue(body, out var value) || value.Frame != _frame || !value.W.Plan.Halberd)
			{
				return false;
			}
			MeshModel meshModel = RegaliaKit.Library?.Invoke(RegaliaKit.HalberdName(m.WeaponDetail));
			if (meshModel == null || !rig.WeaponPlace(body, WeaponKind.Staff, 0f, height, out var position, out var basis, 1f))
			{
				return false;
			}
			Vec3 vec = new Vec3(basis.C2.X, basis.C2.Y, basis.C2.Z).Normalized();
			vec = new Vec3(vec.X * 0.25f, vec.Y * 0.25f, 0.75f + vec.Z * 0.25f).Normalized();
			Vec3 vec2 = new Vec3(0f - body.Basis.C1.X, 0f - body.Basis.C1.Y, 0f).Normalized();
			Vec3 vec3 = vec2 - vec * Vec3.Dot(vec2, vec);
			vec3 = ((Vec3.Dot(vec3, vec3) > 1E-06f) ? vec3.Normalized() : new Vec3(1f, 0f, 0f));
			Vec3 vec4 = Vec3.Cross(vec3, vec);
			float num = Mathf.Clamp(height, 0.3f, 3f);
			MeshInstance meshInstance = m.Weapon ?? (m.Weapon = new MeshInstance());
			meshInstance.Model = meshModel;
			meshInstance.Palette = RegaliaDress.Palette(meshModel, in value.Role.Kingdom.Colors, SashGear.None, 0u);
			meshInstance.Basis = new Mat3(vec4 * num, vec3 * num, vec * num);
			meshInstance.Position = position;
			meshInstance.PartRotation = null;
			meshInstance.PartOffset = null;
			meshInstance.PartScale = null;
			meshInstance.Hidden = 0uL;
			meshInstance.Tag = template.Tag;
			meshInstance.Flash = template.Flash;
			meshInstance.NoShadow = template.NoShadow || height < 0.6f;
			meshInstance.SortDepth = template.SortDepth;
			meshInstance.Static = false;
			meshes.Add(meshInstance);
			return true;
		}

		private static void FixKingsHair(Actor a, Model m, PersonRig rig, MeshInstance body, SpriteCache sprites, bool king)
		{
			HairFix value = HairFixes.GetValue(m, (Model _) => new HairFix());
			long id;
			try
			{
				id = a.data.id;
			}
			catch
			{
				return;
			}
			if (value.Id != id)
			{
				value.Id = id;
				value.Done = false;
				value.Tries = 0;
				value.NextAt = 0;
			}
			if (!value.Done && _frame >= value.NextAt)
			{
				value.NextAt = _frame + 30;
				uint num = 0u;
				try
				{
					ActorTextureSubAsset textureAsset = a.getTextureAsset();
					bool female = m.Female;
					int num2 = a.data.head;
					if (num2 < 0)
					{
						Sprite[] array = ((!female) ? a.animation_container?.heads_male : a.animation_container?.heads_female);
						if (array == null || array.Length == 0)
						{
							array = a.animation_container?.heads;
						}
						num2 = ((array != null && array.Length != 0) ? AnimationHelper.getSpriteIndex(id, array.Length) : 0);
					}
					Sprite sprite = ((textureAsset == null) ? null : ((a.hasTrait("wise") && textureAsset.has_old_heads) ? ActorAnimationLoader.getHeadSpecial(female ? textureAsset.texture_heads_old_female : textureAsset.texture_heads_old_male) : ActorAnimationLoader.getHead(female ? textureAsset.texture_heads_female : textureAsset.texture_heads_male, num2)));
					SpriteImage spriteImage = ((sprite != null && sprites != null) ? sprites.Get(sprite) : null);
					if (spriteImage != null)
					{
						num = RegaliaHair.FromHead(spriteImage, m.Palette.Skin);
					}
					if (spriteImage != null && num == 0)
					{
						num = m.Palette.Hair;
					}
				}
				catch
				{
				}
				if (num == 0 && ++value.Tries >= 20)
				{
					num = RegaliaHair.Seeded(id);
				}
				if (num != 0)
				{
					if (m.Stage == Stage.Elder)
					{
						num = TerrainCache.Lerp(num, TerrainCache.Rgb(208, 208, 214), 0.8f);
					}
					value.Hair = num;
					value.Done = true;
				}
			}
			if (value.Done && rig.SlotHair >= 0 && body.Palette != null && rig.SlotHair < body.Palette.Length)
			{
				body.Palette[rig.SlotHair] = value.Hair | 0xFF000000u;
			}
		}

		private static bool WornLastFrame(MeshInstance body, out RegaliaDress.Wearer wearer)
		{
			wearer = null;
			if (body == null || !WornOf.TryGetValue(body, out var value) || value.Drawn != _frame - 1)
			{
				return false;
			}
			wearer = value.W;
			return true;
		}

		internal static void RegaliaFall(MeshInstance body, MeshInstance corpse)
		{
			if (!RegaliaOn || corpse == null || !WornLastFrame(body, out var wearer))
			{
				return;
			}
			Fallen fallen = null;
			for (int i = 0; i < 6; i++)
			{
				RegaliaKit.Set set = (RegaliaKit.Set)i;
				RegaliaDress.Bound bound = wearer.Bounds[i];
				MeshInstance meshInstance = wearer.Sets[i];
				if (set == RegaliaKit.Set.Captain || bound == null || meshInstance?.PartRotation == null || wearer.Shown[i] == 0L)
				{
					continue;
				}
				ulong num = FallenPieces(set, bound, wearer.Shown[i]);
				if (num == 0L)
				{
					continue;
				}
				MeshInstance meshInstance2 = new MeshInstance
				{
					Model = bound.Model,
					Position = meshInstance.Position,
					Basis = meshInstance.Basis,
					Hidden = (~num | bound.Skeleton | bound.Empty),
					Palette = meshInstance.Palette,
					Multiply = meshInstance.Multiply,
					NoShadow = meshInstance.NoShadow,
					KeepSmall = meshInstance.KeepSmall,
					PartRotation = (Mat3[])meshInstance.PartRotation.Clone(),
					PartOffset = (Vec3[])meshInstance.PartOffset.Clone(),
					PartScale = (float[])meshInstance.PartScale.Clone()
				};
				if (set == RegaliaKit.Set.Soldier)
				{
					for (int j = 0; j <= 1; j++)
					{
						int num2 = bound.PartOf(j);
						if (num2 >= 0 && num2 < meshInstance2.PartRotation.Length)
						{
							meshInstance2.PartRotation[num2] = Mat3.Identity;
							meshInstance2.PartOffset[num2] = default(Vec3);
						}
					}
				}
				if (fallen == null)
				{
					fallen = new Fallen();
				}
				fallen.Sets[i] = meshInstance2;
				fallen.Bounds[i] = bound;
			}
			if (fallen != null)
			{
				if (WornOf.TryGetValue(body, out var value))
				{
					fallen.Id = value.Id;
				}
				fallen.Upper = wearer.Upper.A;
				fallen.Hem = wearer.Hem.A;
				fallen.Side = wearer.Side.A;
				fallen.RobeUpper = wearer.RobeUpper.A;
				fallen.RobeHem = wearer.RobeHem.A;
				fallen.RobeSide = wearer.RobeSide.A;
				FallenOf.Remove(corpse);
				FallenOf.Add(corpse, fallen);
				FallenRecent.Add(fallen);
				if (FallenRecent.Count > 48)
				{
					FallenRecent.RemoveAt(0);
				}
			}
		}

		private static ulong FallenPieces(RegaliaKit.Set set, RegaliaDress.Bound b, ulong shown)
		{
			ulong keep = 0uL;
			switch (set)
			{
			case RegaliaKit.Set.Soldier:
				Stay(0);
				Stay(1);
				break;
			case RegaliaKit.Set.Royal:
				Stay(0);
				Stay(1, 0);
				Stay(2);
				Stay(3);
				Stay(4, 3);
				Stay(5);
				Stay(6);
				break;
			case RegaliaKit.Set.Court:
				Stay(0);
				Stay(1);
				Stay(2);
				Stay(3);
				Stay(4, 3);
				Stay(5);
				Stay(6, 5);
				Stay(7);
				Stay(8, 7);
				break;
			case RegaliaKit.Set.Honour:
				Stay(0);
				Stay(1, 0);
				Stay(2, 0);
				Stay(3);
				Stay(4, 3);
				Stay(5, 3);
				Stay(6, 3);
				break;
			case RegaliaKit.Set.Robe:
				Stay(0);
				Stay(1, 0);
				Stay(2);
				Stay(3);
				Stay(4);
				break;
			}
			return keep;
			void Stay(int piece, int bare = -1)
			{
				if ((shown & b.Bit(piece)) != 0L)
				{
					keep |= ((bare >= 0 && b.Has(bare)) ? b.Bit(bare) : b.Bit(piece));
				}
			}
		}

		internal static void DrawFallenRegalia(List<MeshInstance> meshes, MeshInstance corpse, float dt)
		{
			if (corpse == null || corpse.PartRotation == null || !FallenOf.TryGetValue(corpse, out var value))
			{
				return;
			}
			if (_fallenFrame != _frame)
			{
				_fallenCount = 0;
				_fallenFrame = _frame;
			}
			if (!RegaliaOn)
			{
				return;
			}
			float num = Mathf.Exp(-6f * Mathf.Max(0f, dt));
			value.Upper *= num;
			value.Hem *= num;
			value.Side *= num;
			value.RobeUpper *= num;
			value.RobeHem *= num;
			value.RobeSide *= num;
			for (int i = 0; i < 6; i++)
			{
				MeshInstance meshInstance = value.Sets[i];
				RegaliaDress.Bound bound = value.Bounds[i];
				if (meshInstance != null && bound != null)
				{
					RegaliaDress.Pose(meshInstance, bound, corpse);
					switch (i)
					{
					case 2:
						FallenHinge(meshInstance, bound, 5, 6, value.Upper, value.Hem, value.Side);
						break;
					case 5:
						FallenHinge(meshInstance, bound, 2, 3, value.RobeUpper, value.RobeHem, value.RobeSide);
						break;
					}
					meshInstance.Multiply = corpse.Multiply;
					meshInstance.Flash = 0u;
					meshInstance.Tag = -1;
					meshInstance.SortDepth = corpse.SortDepth;
					meshInstance.Static = false;
					meshes.Add(meshInstance);
					_fallenCount++;
				}
			}
			value.Drawn = _frame;
			FallenRegaliaForTest = _fallenCount;
		}

		private static void FallenHinge(MeshInstance inst, RegaliaDress.Bound b, int upperPiece, int hemPiece, float upper, float hem, float side)
		{
			int num = b.PartOf(upperPiece);
			int num2 = b.PartOf(hemPiece);
			if (num >= 0 && num < inst.PartRotation.Length)
			{
				inst.PartRotation[num] = Mat3.RotY(side) * Mat3.RotX(upper);
			}
			if (num2 >= 0 && num2 < inst.PartRotation.Length)
			{
				inst.PartRotation[num2] = Mat3.RotX(hem);
			}
		}

		internal static string[] FallenRegaliaShown(long id)
		{
			for (int num = FallenRecent.Count - 1; num >= 0; num--)
			{
				Fallen fallen = FallenRecent[num];
				if (fallen.Id == id)
				{
					if (fallen.Drawn < _frame - 1)
					{
						return null;
					}
					List<string> list = new List<string>();
					for (int i = 0; i < 6; i++)
					{
						RegaliaDress.Bound bound = fallen.Bounds[i];
						MeshInstance meshInstance = fallen.Sets[i];
						if (bound == null || meshInstance == null)
						{
							continue;
						}
						for (int j = 0; j < bound.Model.Parts.Length && j < 64; j++)
						{
							if ((meshInstance.Hidden & (ulong)(1L << j)) == 0L)
							{
								list.Add(RegaliaKit.SetName((RegaliaKit.Set)i) + ":" + bound.Model.Parts[j].Name);
							}
						}
					}
					return list.ToArray();
				}
			}
			return null;
		}

		private static Worn WornFor(Actor a)
		{
			if (a == null || !Figures.TryGetValue(a, out var value) || value.Mesh == null || !WornOf.TryGetValue(value.Mesh, out var value2))
			{
				return null;
			}
			return value2;
		}

		internal static string[] RegaliaShown(Actor a)
		{
			Worn worn = WornFor(a);
			if (worn == null || worn.Drawn < _frame - 1)
			{
				return null;
			}
			List<string> list = new List<string>();
			for (int i = 0; i < 6; i++)
			{
				RegaliaDress.Bound bound = worn.W.Bounds[i];
				ulong num = worn.W.Shown[i];
				if (bound == null || num == 0L)
				{
					continue;
				}
				for (int j = 0; j < bound.Model.Parts.Length && j < 64; j++)
				{
					if ((num & (ulong)(1L << j)) != 0L)
					{
						list.Add(RegaliaKit.SetName((RegaliaKit.Set)i) + ":" + bound.Model.Parts[j].Name);
					}
				}
			}
			if (HalberdForTest(a))
			{
				list.Add("halberd:halberd");
			}
			return list.ToArray();
		}

		internal static string RegaliaModelForTest(Actor a, RegaliaKit.Set s)
		{
			Worn worn = WornFor(a);
			if (worn == null || worn.Drawn < _frame - 1 || worn.W.Shown[(uint)s] == 0L)
			{
				return null;
			}
			return worn.W.Bounds[(uint)s]?.Model.Name;
		}

		internal static uint RegaliaSlotForTest(Actor a, RegaliaKit.Set s, string slot)
		{
			Worn worn = WornFor(a);
			MeshInstance meshInstance = ((worn != null) ? worn.W.Sets[(uint)s] : null);
			if (meshInstance?.Model == null || meshInstance.Palette == null || worn.W.Shown[(uint)s] == 0L)
			{
				return 0u;
			}
			int num = meshInstance.Model.SlotIndex(slot);
			if (num < 0 || num >= meshInstance.Palette.Length)
			{
				return 0u;
			}
			return meshInstance.Palette[num];
		}

		internal static string HelmetLookForTest(Actor a)
		{
			Worn worn = WornFor(a);
			if (worn != null && worn.Drawn >= _frame - 1)
			{
				return worn.W.HelmetLook();
			}
			if (a == null || !Figures.TryGetValue(a, out var value) || value.ArmorShown <= 0 || value.Helmet == null)
			{
				return null;
			}
			return "armour:" + value.Helmet;
		}

		internal static float MantleClearForTest(Actor a)
		{
			return WornFor(a)?.W.Clear ?? float.PositiveInfinity;
		}

		internal static bool HalberdForTest(Actor a)
		{
			if (a == null || !Figures.TryGetValue(a, out var value))
			{
				return false;
			}
			Worn worn = WornFor(a);
			if (worn != null && worn.Frame >= _frame - 1 && worn.W.Plan.Halberd && value.Weapon != null && value.Weapon.Model != null && value.Weapon.Model.Name != null)
			{
				return value.Weapon.Model.Name.StartsWith("rg_halberd", StringComparison.Ordinal);
			}
			return false;
		}

		internal static uint HairFixedForTest(Actor a)
		{
			if (a == null || !Figures.TryGetValue(a, out var value) || !HairFixes.TryGetValue(value, out var value2) || !value2.Done)
			{
				return 0u;
			}
			return value2.Hair | 0xFF000000u;
		}

		internal static void RecheckForTest(Actor a)
		{
			if (a != null && Figures.TryGetValue(a, out var value))
			{
				value.ArmorChecked = -1000;
				value.WeaponChecked = -1000;
				Regalia.RoleOf(a, forFigure: true);
				Worn worn = WornFor(a);
				if (worn != null)
				{
					worn.GearSerial = -1;
				}
			}
		}

		internal static void ForgetLookForTest(Actor a)
		{
			if (a != null)
			{
				Figures.Remove(a);
			}
		}

		private static void SwayOf(Worn w, MeshInstance body, Model m, float speed, float height, float wdt, out ClothSway.Input s)
		{
			float num = Mathf.Clamp(wdt, 0f, 0.05f);
			float num2 = Mathf.Atan2(0f - body.Basis.C1.Y, 0f - body.Basis.C1.X);
			float turn = 0f;
			if (num > 0f && !float.IsNaN(w.LastHeading))
			{
				turn = Mathf.Clamp(Mathf.DeltaAngle(w.LastHeading * 57.29578f, num2 * 57.29578f) * (MathF.PI / 180f) / num, -6f, 6f);
			}
			w.LastHeading = num2;
			w.Clock += num;
			ClothSway.Wind(body.Basis, GrassTufts.WindX, GrassTufts.WindY, out var along, out var side);
			bool active = GodView.Active;
			float num3 = (active ? 0f : GrassTufts.WindStrength);
			s = new ClothSway.Input
			{
				Dt = num,
				Speed = speed / Mathf.Max(0.1f, height),
				Turn = turn,
				Along = along,
				Side = side,
				Push = (active ? 0f : (0.05f + 0.25f * num3)),
				Gust = num3,
				Stride = ((m.Walk != null) ? m.Walk.Stride : 0f),
				Phase = ((m.Walk != null) ? m.Walk.Phase : 0f),
				Clock = w.Clock,
				Still = _asFigure
			};
		}

		private static RestPose RestOf(Actor a, PersonRig rig, Vector2 at, float heading, float speed, bool free, bool child, float body, float distance, float dt)
		{
			if (_asFigure)
			{
				return default(RestPose);
			}
			if (_asHost)
			{
				HostRig = rig;
				HostBody = body;
				return HostRest;
			}
			if (KingGuards.Kneels(a, out var pose))
			{
				return pose;
			}
			bool flag = false;
			try
			{
				flag = a.is_inside_building || a.is_inside_boat;
			}
			catch
			{
			}
			if (!flag)
			{
				return Lounging.Pose(a, at, heading, speed, free, child, body, distance, dt);
			}
			return default(RestPose);
		}

		private static void ApplyRest(PersonRig rig, MeshInstance inst, RestPose rest, float scale, ref float z, Actor a)
		{
			if (rest.On)
			{
				float seat = ((rest.Style != RestStyle.Seat) ? 0f : ((rest.Hang || rest.Seat < 0f) ? rig.KneeHeight : (rest.Seat / Mathf.Max(0.0001f, scale))));
				if (rest.Hang)
				{
					z -= rig.KneeHeight * scale * Mathf.SmoothStep(0f, 1f, rest.Amount);
				}
				rig.Rest(inst, rest.Style, rest.Amount, seat, PoseClock.Now + (float)(a.GetHashCode() & 0x3F));
			}
		}

		public static bool GunMuzzle(Actor a, out Vec3 muzzle, out Vec3 dir, out float scale)
		{
			muzzle = (dir = default(Vec3));
			scale = 1f;
			if (a == null || !Guns.TryGetValue(a, out var value) || _frame - value.Frame > 2)
			{
				return false;
			}
			muzzle = value.Muzzle;
			dir = value.Dir;
			scale = value.Scale;
			return true;
		}

		public static void ShotgunFired(Actor a)
		{
			if (a != null && Guns.TryGetValue(a, out var value))
			{
				value.FiredAt = Time.time;
				value.Ejected = false;
			}
		}

		private static void AddShotgun(List<MeshInstance> meshes, Actor a, Model m, PersonRig rig, MeshInstance body, float attack, float height, float heading, float stance, VoxelObject template)
		{
			MeshModel meshModel = Models.Get("held_shotgun");
			if (meshModel != null && meshModel != _gunBound)
			{
				_gunBound = meshModel;
				_gunMuzzle = Vec(meshModel.Info("muzzle"), _gunMuzzle);
				_gunPort = Vec(meshModel.Info("port"), _gunPort);
				_gunPortDir = Vec(meshModel.Info("port_dir"), _gunPortDir).Normalized();
			}
			if (!ShotgunHold.Place(rig, body, stance, attack, height, heading, Mathf.Clamp(m.AimPitch * 0.4f, 0f, 0.12f), out var position, out var basis))
			{
				return;
			}
			MeshInstance meshInstance = m.Weapon ?? (m.Weapon = new MeshInstance());
			meshInstance.Model = meshModel ?? ViewMeshes.Item(m.Look);
			meshInstance.Palette = null;
			meshInstance.Basis = basis;
			meshInstance.Position = position;
			int num = meshInstance.Model.Parts.Length;
			if (meshInstance.PartOffset == null || meshInstance.PartOffset.Length != num)
			{
				meshInstance.PartOffset = new Vec3[num];
			}
			if (meshInstance.PartRotation != null && meshInstance.PartRotation.Length != num)
			{
				meshInstance.PartRotation = null;
			}
			int num2 = meshModel?.PartIndex("pump") ?? (-1);
			int num3 = meshModel?.PartIndex("bolt") ?? (-1);
			float num4 = ShotgunHold.Pump(attack);
			for (int i = 0; i < num; i++)
			{
				meshInstance.PartOffset[i] = new Vec3(0f, 0f, 0f);
			}
			if (num2 >= 0)
			{
				meshInstance.PartOffset[num2] = new Vec3(0f, -0.046f * num4, 0f);
			}
			if (num3 >= 0)
			{
				meshInstance.PartOffset[num3] = new Vec3(0f, -0.046f * num4, 0f);
			}
			meshInstance.Tag = template.Tag;
			meshInstance.Flash = template.Flash;
			meshInstance.NoShadow = template.NoShadow || height < 0.6f;
			meshInstance.SortDepth = template.SortDepth;
			meshInstance.Static = false;
			meshes.Add(meshInstance);
			if (!Guns.TryGetValue(a, out var value))
			{
				if (Guns.Count > 200)
				{
					Guns.Clear();
				}
				value = (Guns[a] = new GunAt());
			}
			value.Frame = _frame;
			value.Muzzle = position + basis.Mul(_gunMuzzle);
			value.Dir = basis.C1.Normalized();
			value.Up = basis.C2.Normalized();
			value.Port = position + basis.Mul(_gunPort);
			value.PortDir = basis.Mul(_gunPortDir).Normalized();
			value.Scale = Mathf.Max(0.3f, Mathf.Min(3f, height));
			if (!value.Ejected && Time.time - value.FiredAt > 0.32f)
			{
				value.Ejected = true;
				if (Time.time - value.FiredAt < 1.5f)
				{
					SceneCollector.EjectShell(value.Port, value.PortDir * (1.4f * value.Scale) + new Vec3(0f, 0f, 1.2f * value.Scale) - value.Dir * (0.3f * value.Scale), value.Dir, value.Scale, value.PortDir);
				}
			}
		}

		private static Vec3 Vec(string s, Vec3 fallback)
		{
			if (string.IsNullOrEmpty(s))
			{
				return fallback;
			}
			string[] array = s.Split(',');
			CultureInfo invariantCulture = CultureInfo.InvariantCulture;
			if (array.Length != 3 || !float.TryParse(array[0], NumberStyles.Float, invariantCulture, out var result) || !float.TryParse(array[1], NumberStyles.Float, invariantCulture, out var result2) || !float.TryParse(array[2], NumberStyles.Float, invariantCulture, out var result3))
			{
				return fallback;
			}
			return new Vec3(result, result2, result3);
		}

		private static PersonMemo MemoOf(Model m)
		{
			return m.Memo ?? (m.Memo = new PersonMemo());
		}

		private static bool Same(string a, string b)
		{
			if ((object)a != b)
			{
				if (a != null && b != null)
				{
					return a == b;
				}
				return false;
			}
			return true;
		}

		private static string FamilyOf(Actor a, Model m)
		{
			if (!Models.FamiliesRemembered)
			{
				return Models.PersonFamily(a);
			}
			PersonMemo personMemo = MemoOf(m);
			ActorAsset asset = a.asset;
			string text = Mutations.SkinOf(a);
			if (personMemo.FamilySet && personMemo.FamilyAsset == asset && personMemo.FamilyFemale == m.Female && (object)personMemo.FamilySkin == text)
			{
				if (_verifyFamilies)
				{
					VerifyFamily(a, personMemo.Family);
				}
				return personMemo.Family;
			}
			bool settled;
			string text2 = Models.PersonFamily(a, out settled);
			personMemo.FamilySet = settled;
			personMemo.FamilyAsset = asset;
			personMemo.FamilyFemale = m.Female;
			personMemo.FamilySkin = text;
			personMemo.Family = text2;
			return text2;
		}

		internal static void VerifyFamilies(bool on)
		{
			_verifyFamilies = on;
			Models.VerifyFamilies(on);
			if (on)
			{
				_familiesCompared = (_familiesDiffer = 0);
				_familiesFirst = null;
			}
		}

		internal static string FamiliesVerdict()
		{
			string text = Models.FamiliesVerdict();
			if (text != null)
			{
				return text;
			}
			if (_familiesDiffer > 0)
			{
				return $"{_familiesDiffer} of {_familiesCompared} families differ; first: {_familiesFirst}";
			}
			if (_familiesCompared != 0)
			{
				return null;
			}
			return "no family was compared";
		}

		private static void VerifyFamily(Actor a, string remembered)
		{
			_familiesCompared++;
			string text = Models.PersonFamily(a);
			if (!(text == remembered))
			{
				_familiesDiffer++;
				if (_familiesFirst == null)
				{
					_familiesFirst = ((a.asset != null) ? a.asset.id : "?") + ": family " + text + " the old way, " + remembered + " remembered";
				}
			}
		}

		private static ulong HideBit(MeshModel model, string part)
		{
			int num = model.PartIndex(part);
			if (num >= 0 && num < 64)
			{
				return (ulong)(1L << num);
			}
			return 0uL;
		}

		private static void HairAndShoes(ref HairMemo f, PersonRig rig, MeshInstance body, string helmet, string boots)
		{
			if (!f.HairSet || f.HairModel != body.Model || !Same(f.HairHelmet, helmet))
			{
				f.HairHidden = ArmorMeshes.HidesHair(helmet);
				f.HairMask = HideBit(body.Model, "hair_short") | HideBit(body.Model, "hair_long") | HideBit(body.Model, "fem_bow") | LookHairMask(body.Model);
				f.HairModel = body.Model;
				f.HairHelmet = helmet;
				f.HairSet = true;
			}
			ulong hidden = body.Hidden;
			if (f.HairHidden)
			{
				body.Hidden |= f.HairMask;
			}
			if (boots != null && rig.SlotShoes >= 0 && body.Palette != null)
			{
				if (!f.ShoesSet || !Same(f.ShoesBoots, boots))
				{
					f.Shoes = TerrainCache.Scale(ArmorMeshes.ShoeColor(boots), 0.85f) | 0xFF000000u;
					f.ShoesBoots = boots;
					f.ShoesSet = true;
				}
				body.Palette[rig.SlotShoes] = f.Shoes;
			}
			if (_verifyArmor)
			{
				VerifyHair(rig, body, helmet, boots, hidden);
			}
		}

		internal static void VerifyArmor(bool on)
		{
			_verifyArmor = on;
			ArmorMeshes.VerifyDresses(on);
			if (on)
			{
				_hairCompared = (_hairDiffer = 0);
				_hairFirst = null;
			}
		}

		internal static string ArmorVerdict()
		{
			string text = ArmorMeshes.DressVerdict();
			if (text != null)
			{
				return text;
			}
			if (_hairDiffer > 0)
			{
				return $"{_hairDiffer} of {_hairCompared} hair and shoes differ; first: {_hairFirst}";
			}
			if (_hairCompared != 0)
			{
				return null;
			}
			return "no hair or shoes were compared";
		}

		private static void VerifyHair(PersonRig rig, MeshInstance body, string helmet, string boots, ulong before)
		{
			_hairCompared++;
			ulong hidden = body.Hidden;
			body.Hidden = before;
			if (ArmorMeshes.HidesHair(helmet))
			{
				body.Hide("hair_short");
				body.Hide("hair_long");
				body.Hide("fem_bow");
				body.Hidden |= LookHairMask(body.Model);
			}
			ulong hidden2 = body.Hidden;
			body.Hidden = hidden;
			string text = ((hidden2 != hidden) ? $"hidden {hidden2:X} the old way, {hidden:X} remembered" : null);
			if (text == null && boots != null && rig.SlotShoes >= 0 && body.Palette != null)
			{
				uint num = body.Palette[rig.SlotShoes];
				uint num2 = TerrainCache.Scale(ArmorMeshes.ShoeColor(boots), 0.85f) | 0xFF000000u;
				if (num2 != num)
				{
					text = $"shoes {num2:X8} the old way, {num:X8} remembered";
				}
			}
			if (text != null)
			{
				_hairDiffer++;
				if (_hairFirst == null)
				{
					_hairFirst = body.Model?.Name + " in " + (helmet ?? "-") + ", boots " + (boots ?? "-") + ": " + text;
				}
			}
		}

		private static void ProbeArmor(Model m, PersonRig rig, MeshInstance body)
		{
			if (!_verifyArmor || body.Model == null)
			{
				return;
			}
			PersonMemo personMemo = MemoOf(m);
			Probe probe = personMemo.Tried ?? (personMemo.Tried = new Probe());
			string[] array = ProbeSets[RuntimeHelpers.GetHashCode(m) & 3];
			MeshInstance meshInstance = _probeBody ?? (_probeBody = new MeshInstance());
			meshInstance.Model = body.Model;
			meshInstance.Position = body.Position;
			meshInstance.Basis = body.Basis;
			meshInstance.PartRotation = body.PartRotation;
			meshInstance.PartOffset = body.PartOffset;
			meshInstance.PartScale = body.PartScale;
			meshInstance.Hidden = body.Hidden;
			meshInstance.Palette = null;
			if (body.Palette != null)
			{
				if (_probePalette == null || _probePalette.Length != body.Palette.Length)
				{
					_probePalette = new uint[body.Palette.Length];
				}
				Array.Copy(body.Palette, _probePalette, body.Palette.Length);
				meshInstance.Palette = _probePalette;
			}
			ArmorMeshes.Dress(meshInstance, m.BaseModel ?? body.Model, array[0], array[1], array[2], probe.Into, ref probe.Dressed);
			HairAndShoes(ref probe.Hair, rig, meshInstance, array[0], array[2]);
			meshInstance.Model = null;
		}

		internal static bool WaterForTest(Actor a, out float swim, out float crawl, out float wade, out MeshInstance mesh)
		{
			swim = (crawl = (wade = 0f));
			mesh = null;
			if (a == null || !Figures.TryGetValue(a, out var value) || value.Mesh == null || !_inWater.TryGetValue(value, out var value2))
			{
				return false;
			}
			swim = value2.Swim;
			crawl = value2.Crawl;
			wade = value2.Wade;
			mesh = value.Mesh;
			return true;
		}

		private static void WaterPose(Actor a, Model m, PersonRig rig, MeshInstance inst, float x, float y, float body, float speed, float phase, float stride, bool swimming, bool you, float dt)
		{
			float num = (swimming ? 0f : (OffWater(a, x, y) ? float.NaN : Wading.DepthHere(x, y)));
			bool flag = swimming || (!float.IsNaN(num) && num > 0.04f);
			if (!_inWater.TryGetValue(m, out var value))
			{
				if (!flag)
				{
					return;
				}
				value = new InWater();
				_inWater.Add(m, value);
			}
			float num2 = Mathf.Max(0.1f, body);
			value.Swim = Mathf.MoveTowards(value.Swim, swimming ? 1f : 0f, dt * 2.5f);
			bool flag2 = swimming && speed > 0.2f * num2 && (you || IsMoving(a));
			value.Crawl = Mathf.MoveTowards(value.Crawl, flag2 ? 1f : 0f, dt * 1.6f);
			float target = ((swimming || float.IsNaN(num)) ? 0f : Mathf.Clamp01((num - 0.12f * num2) / (0.43f * num2)));
			value.Wade = Mathf.MoveTowards(value.Wade, target, dt * 3f);
			if (swimming)
			{
				rig.Swim(inst, value.Crawl, phase, PoseClock.Now, (float)(a.GetHashCode() & 0xFF) / 255f);
			}
			else if (value.Wade > 0.01f)
			{
				rig.Wade(inst, value.Wade, phase, stride);
			}
		}

		private static bool OffWater(Actor a, float x, float y)
		{
			try
			{
				return a.position_height > 0.3f || DockDecks.On(new Vector2(x, y));
			}
			catch
			{
				return false;
			}
		}

		private static bool IsMoving(Actor a)
		{
			try
			{
				return a.is_moving;
			}
			catch
			{
				return false;
			}
		}

		private static void WaterPlace(Actor a, Model m, MeshInstance inst, float x, float y, float z, float heading, float body)
		{
			if (_inWater.TryGetValue(m, out var value) && !(value.Swim <= 0.001f))
			{
				float num = Wading.WaterTopHere(x, y);
				if (!float.IsNaN(num))
				{
					float num2 = value.Crawl * value.Crawl * (3f - 2f * value.Crawl);
					float num3 = 1.25f * num2;
					float num4 = Mathf.Cos(num3);
					float num5 = Mathf.Sin(num3);
					float num6 = 0.025f * body * Mathf.Sin(PoseClock.Now * 2.2f + (float)(a.GetHashCode() & 0xFF) * 0.0246f) * (1f - num2);
					float b = num - body * (0.75f * (1f - num2) + (0.15f + 0.55f * num4) * num2) + num6;
					float num7 = 0.55f * body * num5 * value.Swim;
					inst.Position = new Vec3(x - Mathf.Cos(heading) * num7, y - Mathf.Sin(heading) * num7, Mathf.Lerp(z, b, value.Swim));
					inst.Basis *= Mat3.RotX(num3 * value.Swim);
				}
			}
		}

		private static bool CarriesTorch(Actor a)
		{
			try
			{
				if (a.asset != null && a.asset.id == "bandit")
				{
					return true;
				}
				if (!a.isAdult() || !Dialogue.IsPerson(a))
				{
					return false;
				}
				if (Service.IsSoldier(a) || a.army != null)
				{
					return true;
				}
				return a.city == null && a.kingdom != null && a.kingdom.isCiv();
			}
			catch
			{
				return false;
			}
		}

		private static void AddTorch(List<MeshInstance> meshes, Actor a, Model m, PersonRig rig, MeshInstance inst, WeaponKind kind, float body, float heading, float distance, bool swimming, bool busy, VoxelObject template)
		{
			if (_torchesFrame != _frame)
			{
				_torchesFrame = _frame;
				TorchesDrawn.Clear();
			}
			if (_asFigure || swimming || busy || TorchNight < 0.3f || distance > 42f * Models.LodZoom || TorchesDrawn.Count >= 40 || template.Tag == 2147483646 || kind == WeaponKind.Bow || kind == WeaponKind.Spear || kind == WeaponKind.Staff || kind == WeaponKind.Shotgun || kind == WeaponKind.Gun)
			{
				return;
			}
			long num = 0L;
			try
			{
				if (a.is_inside_building)
				{
					return;
				}
				num = a.data.id;
			}
			catch
			{
				return;
			}
			float num2 = 0.3f + 0.25f * (float)((ulong)(num * 2654435761u) % 1000uL) / 1000f;
			if (TorchNight < num2 || !CarriesTorch(a))
			{
				return;
			}
			MeshModel meshModel = Models.Get("held_torch");
			if (meshModel != null)
			{
				rig.HoldTorch(inst, 1f);
				rig.TorchPlace(inst, heading, body, out var position, out var basis);
				HeldTorch value = TorchOf.GetValue(m, (Model _) => new HeldTorch());
				MeshInstance mesh = value.Mesh;
				mesh.Model = meshModel;
				mesh.Position = position;
				mesh.Basis = basis;
				mesh.Tag = template.Tag;
				mesh.Flash = template.Flash;
				mesh.NoShadow = true;
				mesh.SortDepth = template.SortDepth;
				mesh.Static = false;
				value.Fire.Lit = true;
				value.Fire.Pose(mesh, Time.unscaledTime);
				if (distance < 24f * Models.LodZoom)
				{
					meshes.Add(mesh);
				}
				TorchesDrawn.Add(value.Fire);
			}
		}

		private static int WeaponDetailAt(float distance, int current)
		{
			float num = distance / Models.LodZoom;
			float num2 = 11f;
			float num3 = 30f;
			if (current == 0 && num < num2 + 1.5f)
			{
				return 0;
			}
			if (current == 1 && num > num2 - 1.5f && num < num3 + 1.5f)
			{
				return 1;
			}
			if (current == 2 && num > num3 - 1.5f)
			{
				return 2;
			}
			if (!(num < num2))
			{
				if (!(num < num3))
				{
					return 2;
				}
				return 1;
			}
			return 0;
		}

		private static void NoteGlow(MeshInstance w)
		{
			if (!_asFigure && w != null && w.Model != null && w.Tag != 2147483646 && ViewMeshes.HeldGlow(w.Model, out var colour, out var head, out var lava))
			{
				if (_glowsFrame != _frame)
				{
					_glowsFrame = _frame;
					GlowsDrawn.Clear();
				}
				if (GlowsDrawn.Count < 32)
				{
					GlowsDrawn.Add(new HeldGlow
					{
						At = w.Position + w.Basis.Mul(head),
						Colour = colour,
						Lava = lava,
						Time = Time.unscaledTime
					});
				}
			}
		}

		internal static string WeaponForTest(Actor a, out int detail)
		{
			detail = -1;
			if (a == null || !Figures.TryGetValue(a, out var value))
			{
				return null;
			}
			detail = value.WeaponDetail;
			if (value.Weapon == null || value.Weapon.Model == null || _frame - value.Drawn >= 3)
			{
				return null;
			}
			return value.Weapon.Model.Name;
		}

		internal static MeshModel WeaponModelForTest(Actor a)
		{
			if (a == null || !Figures.TryGetValue(a, out var value) || value.Weapon == null || _frame - value.Drawn >= 3)
			{
				return null;
			}
			return value.Weapon.Model;
		}

		private static void AddWrap(List<MeshInstance> meshes, Actor a, Model m, MeshInstance body, VoxelObject template)
		{
			if (!Bandages.Healing(a))
			{
				return;
			}
			MeshModel meshModel = WrapMeshes.ForPerson(m.BaseModel ?? body.Model, m.Chest != null);
			if (meshModel == null || meshModel.Parts.Length != body.Model.Parts.Length)
			{
				return;
			}
			if (!Wraps.TryGetValue(a, out var value))
			{
				if (Wraps.Count > 64)
				{
					Wraps.Clear();
				}
				value = (Wraps[a] = new MeshInstance());
			}
			value.Model = meshModel;
			value.Position = body.Position;
			value.Basis = body.Basis;
			value.PartRotation = body.PartRotation;
			value.PartOffset = body.PartOffset;
			value.PartScale = body.PartScale;
			value.Palette = null;
			value.Hidden = 0uL;
			value.Tag = template.Tag;
			value.Flash = template.Flash;
			value.Multiply = template.Multiply;
			value.NoShadow = true;
			value.SortDepth = template.SortDepth;
			value.Static = false;
			meshes.Add(value);
		}

		internal static bool WrapDrawnForTest(Actor a)
		{
			if (a != null && Wraps.ContainsKey(a))
			{
				return Bandages.Healing(a);
			}
			return false;
		}
	}
}
