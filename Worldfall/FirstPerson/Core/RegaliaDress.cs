using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FirstPerson.Core
{
	public static class RegaliaDress
	{
		public sealed class Bound
		{
			public MeshModel Model;

			public MeshModel Body;

			public RegaliaKit.Set Set;

			public KingdomStyle Style;

			public int[] FromBody;

			public ulong Skeleton;

			public ulong Empty;

			internal int[] Bones;

			internal int[] BoneFrom;

			internal int[] Piece;

			internal byte[] Hides;

			internal ulong Torso;

			internal ulong HeadPieces;

			internal ulong Covers;

			public int Part(string name)
			{
				return Model.PartIndex(name);
			}

			internal ulong Bit(int piece)
			{
				if (piece >= Piece.Length || Piece[piece] < 0 || Piece[piece] >= 64)
				{
					return 0uL;
				}
				return (ulong)(1L << Piece[piece]);
			}

			internal bool Has(int piece)
			{
				if (piece < Piece.Length)
				{
					return Piece[piece] >= 0;
				}
				return false;
			}

			internal int PartOf(int piece)
			{
				if (piece >= Piece.Length)
				{
					return -1;
				}
				return Piece[piece];
			}
		}

		private readonly struct PairKey : IEquatable<PairKey>
		{
			private readonly MeshModel _a;

			private readonly MeshModel _b;

			public PairKey(MeshModel a, MeshModel b)
			{
				_a = a;
				_b = b;
			}

			public bool Equals(PairKey o)
			{
				if (_a == o._a)
				{
					return _b == o._b;
				}
				return false;
			}

			public override bool Equals(object o)
			{
				if (o is PairKey o2)
				{
					return Equals(o2);
				}
				return false;
			}

			public override int GetHashCode()
			{
				return RuntimeHelpers.GetHashCode(_a) * 31 + RuntimeHelpers.GetHashCode(_b);
			}
		}

		private readonly struct PaletteKey : IEquatable<PaletteKey>
		{
			private readonly MeshModel _m;

			private readonly uint _main;

			private readonly uint _second;

			private readonly uint _emblem;

			private readonly uint _helm;

			private readonly SashGear _sash;

			public PaletteKey(MeshModel m, in RegaliaColors c, SashGear sash, uint helm)
			{
				_m = m;
				_main = c.Main;
				_second = c.Second;
				_emblem = c.Emblem;
				_sash = sash;
				_helm = helm;
			}

			public bool Equals(PaletteKey o)
			{
				if (_m == o._m && _main == o._main && _second == o._second && _emblem == o._emblem && _sash == o._sash)
				{
					return _helm == o._helm;
				}
				return false;
			}

			public override bool Equals(object o)
			{
				if (o is PaletteKey o2)
				{
					return Equals(o2);
				}
				return false;
			}

			public override int GetHashCode()
			{
				return (((RuntimeHelpers.GetHashCode(_m) * 31 + (int)_main) * 31 + (int)_second) * 31 + (int)_emblem) * 31 + (int)(_helm * 7) + (int)_sash;
			}
		}

		private readonly struct KitKey : IEquatable<KitKey>
		{
			private readonly MeshModel _m;

			private readonly ulong _hidden;

			public KitKey(MeshModel m, ulong hidden)
			{
				_m = m;
				_hidden = hidden;
			}

			public bool Equals(KitKey o)
			{
				if (_m == o._m)
				{
					return _hidden == o._hidden;
				}
				return false;
			}

			public override bool Equals(object o)
			{
				if (o is KitKey o2)
				{
					return Equals(o2);
				}
				return false;
			}

			public override int GetHashCode()
			{
				return RuntimeHelpers.GetHashCode(_m) * 31 + _hidden.GetHashCode();
			}
		}

		public sealed class BodyMasks
		{
			public ulong Hair;

			public ulong Beard;

			public ulong Hats;

			public ulong Under;

			public ulong Tall;

			public ulong HatHair;
		}

		public sealed class Wearer
		{
			public readonly MeshInstance[] Sets = new MeshInstance[6];

			public readonly Bound[] Bounds = new Bound[6];

			public readonly ulong[] Shown = new ulong[6];

			public WearPlan Plan;

			public ClothSway.Swing Upper;

			public ClothSway.Swing Hem;

			public ClothSway.Swing Side;

			public ClothSway.Swing RobeUpper;

			public ClothSway.Swing RobeHem;

			public ClothSway.Swing RobeSide;

			public ClothSway.Swing Lift;

			public ClothSway.Swing Yaw;

			public ClothSway.Swing Tail;

			public ClothSway.Swing PlumeSwing;

			public float Clear = float.PositiveInfinity;

			public float Seed;

			public string Helmet;

			public float Distance;

			public bool Child;

			internal readonly MeshModel[] Models = new MeshModel[6];

			internal readonly MeshModel[] Posed = new MeshModel[6];

			internal KingdomStyle KeyStyle = (KingdomStyle)(-2);

			internal bool KeyFemale;

			internal int KeyLod = -1;

			internal int KeySets;

			internal int LookAgain;

			internal MeshModel KeyFit;

			public void Calm()
			{
				Upper = (Hem = (Side = (RobeUpper = (RobeHem = (RobeSide = (Lift = (Yaw = (Tail = (PlumeSwing = default(ClothSway.Swing))))))))));
				Clear = float.PositiveInfinity;
			}

			public void Clean()
			{
				for (int i = 0; i < Shown.Length; i++)
				{
					Shown[i] = 0uL;
				}
				Plan = default(WearPlan);
				Helmet = null;
				Clear = float.PositiveInfinity;
			}

			public string HelmetLook()
			{
				if (Plan.Royal != 0L)
				{
					Bound bound = Bounds[2];
					if (bound != null)
					{
						string text = RegaliaKit.StyleLetter(bound.Style) ?? "?";
						if ((Plan.Royal & bound.Bit(2)) != 0L)
						{
							return "crownhelm:" + text;
						}
						if ((Plan.Royal & bound.Bit(1)) != 0L)
						{
							return "crown:" + text + "+" + Helmet;
						}
						if ((Plan.Royal & bound.Bit(0)) != 0L)
						{
							return "crown:" + text;
						}
					}
				}
				if (Plan.Court != 0L)
				{
					Bound bound2 = Bounds[3];
					if (bound2 != null && (Plan.Court & (bound2.Bit(0) | bound2.Bit(1))) != 0L)
					{
						if ((Plan.Court & bound2.Bit(0)) == 0L)
						{
							return "hood";
						}
						return "hat";
					}
				}
				if (Plan.Soldier != 0L)
				{
					Bound bound3 = Bounds[0];
					if (bound3 != null && (Plan.Soldier & bound3.Bit(5)) != 0L)
					{
						return "cap:" + Helmet;
					}
				}
				if (string.IsNullOrEmpty(Helmet) || !(Distance < 40f) || Child)
				{
					return null;
				}
				return "armour:" + Helmet;
			}
		}

		public struct Dressing
		{
			public MeshInstance Body;

			public MeshModel Fit;

			public PersonRig Rig;

			public RegaliaColors Colors;

			public ClothSway.Input Sway;

			public MeshInstance[] Armour;

			public int ArmourShown;

			public ulong OwnHidden;

			public VoxelObject Template;
		}

		public const float CloseRange = 40f;

		public const float SoldierRange = 60f;

		public const float FarRange = 120f;

		public const float HalberdRange = 45f;

		public const float ShadowRange = 20f;

		public static Action<string> Warn;

		internal const int Sash = 0;

		internal const int Knot = 1;

		internal const int Plume = 2;

		internal const int Coif = 3;

		internal const int CoifCape = 4;

		internal const int HelmCap = 5;

		internal const int Pole = 0;

		internal const int HarnessBare = 1;

		internal const int HarnessOver = 2;

		internal const int Banner = 3;

		internal const int BannerTail = 4;

		internal const int Crown = 0;

		internal const int CrownLow = 1;

		internal const int Helm = 2;

		internal const int Yoke = 3;

		internal const int YokeOver = 4;

		internal const int Mantle = 5;

		internal const int MantleHem = 6;

		internal const int Hat = 0;

		internal const int Hood = 1;

		internal const int HoodCape = 2;

		internal const int Chain = 3;

		internal const int ChainOver = 4;

		internal const int Seal = 5;

		internal const int SealOver = 6;

		internal const int Keys = 7;

		internal const int KeysOver = 8;

		internal const int Baton = 9;

		internal const int Ledger = 10;

		internal const int Circlet = 0;

		internal const int CircletLow = 1;

		internal const int CircletCoif = 2;

		internal const int Laurel = 3;

		internal const int LaurelLow = 4;

		internal const int LaurelHigh = 5;

		internal const int LaurelCoif = 6;

		internal const int RobeYoke = 0;

		internal const int RobeYokeOver = 1;

		internal const int Robe = 2;

		internal const int RobeHem = 3;

		internal const int Lapels = 4;

		internal const int Staff = 5;

		internal const byte HidesHair = 1;

		internal const byte HidesBeard = 2;

		internal const byte HidesHats = 4;

		public static string BindProblem;

		private static readonly Dictionary<PairKey, Bound> Bounds = new Dictionary<PairKey, Bound>();

		private static readonly Dictionary<PaletteKey, uint[]> Palettes = new Dictionary<PaletteKey, uint[]>();

		private static readonly Dictionary<KitKey, MeshModel> Kits = new Dictionary<KitKey, MeshModel>();

		private static readonly Dictionary<MeshModel, BodyMasks> Masks = new Dictionary<MeshModel, BodyMasks>();

		private static readonly BodyMasks Nothing = new BodyMasks();

		private const float PoleGap = 0.015f;

		private const float PoleSide = 0.05f;

		public static string PieceOrderProblem()
		{
			(RegaliaKit.Set, int, string)[] array = new(RegaliaKit.Set, int, string)[42]
			{
				(RegaliaKit.Set.Soldier, 0, "sash"),
				(RegaliaKit.Set.Soldier, 1, "sashknot"),
				(RegaliaKit.Set.Soldier, 2, "plume"),
				(RegaliaKit.Set.Soldier, 3, "coif"),
				(RegaliaKit.Set.Soldier, 4, "coif_cape"),
				(RegaliaKit.Set.Soldier, 5, "helmcap"),
				(RegaliaKit.Set.Captain, 0, "pole"),
				(RegaliaKit.Set.Captain, 1, "harness_bare"),
				(RegaliaKit.Set.Captain, 2, "harness_over"),
				(RegaliaKit.Set.Captain, 3, "banner"),
				(RegaliaKit.Set.Captain, 4, "banner_tail"),
				(RegaliaKit.Set.Royal, 0, "crown"),
				(RegaliaKit.Set.Royal, 1, "crown_low"),
				(RegaliaKit.Set.Royal, 2, "helm"),
				(RegaliaKit.Set.Royal, 3, "mantle_yoke"),
				(RegaliaKit.Set.Royal, 4, "mantle_yoke_over"),
				(RegaliaKit.Set.Royal, 5, "mantle"),
				(RegaliaKit.Set.Royal, 6, "mantle_hem"),
				(RegaliaKit.Set.Court, 0, "hat"),
				(RegaliaKit.Set.Court, 1, "hood"),
				(RegaliaKit.Set.Court, 2, "hood_cape"),
				(RegaliaKit.Set.Court, 3, "chain"),
				(RegaliaKit.Set.Court, 4, "chain_over"),
				(RegaliaKit.Set.Court, 5, "seal"),
				(RegaliaKit.Set.Court, 6, "seal_over"),
				(RegaliaKit.Set.Court, 7, "keys"),
				(RegaliaKit.Set.Court, 8, "keys_over"),
				(RegaliaKit.Set.Court, 9, "baton"),
				(RegaliaKit.Set.Court, 10, "ledger"),
				(RegaliaKit.Set.Honour, 0, "circlet"),
				(RegaliaKit.Set.Honour, 1, "circlet_low"),
				(RegaliaKit.Set.Honour, 2, "circlet_coif"),
				(RegaliaKit.Set.Honour, 3, "laurel"),
				(RegaliaKit.Set.Honour, 4, "laurel_low"),
				(RegaliaKit.Set.Honour, 5, "laurel_high"),
				(RegaliaKit.Set.Honour, 6, "laurel_coif"),
				(RegaliaKit.Set.Robe, 0, "robe_yoke"),
				(RegaliaKit.Set.Robe, 1, "robe_yoke_over"),
				(RegaliaKit.Set.Robe, 2, "robe"),
				(RegaliaKit.Set.Robe, 3, "robe_hem"),
				(RegaliaKit.Set.Robe, 4, "robe_lapels"),
				(RegaliaKit.Set.Robe, 5, "staff")
			};
			for (int i = 0; i < array.Length; i++)
			{
				(RegaliaKit.Set, int, string) tuple = array[i];
				string[] array2 = RegaliaKit.PartsOf(tuple.Item1);
				if (tuple.Item2 >= array2.Length || array2[tuple.Item2] != tuple.Item3)
				{
					return string.Format("{0} piece {1} is {2}, not {3}", tuple.Item1, tuple.Item2, (tuple.Item2 < array2.Length) ? array2[tuple.Item2] : "missing", tuple.Item3);
				}
			}
			return null;
		}

		public static int SetsWorn(in GearLook g, in WearContext c)
		{
			if (!g.Any)
			{
				return 0;
			}
			float distance = c.Distance;
			HeadGear head = g.Head;
			int num = 0;
			if (distance < 60f && (g.Sash != SashGear.None || (head & (HeadGear.Coif | HeadGear.Plume | HeadGear.PlumeTall)) != HeadGear.None))
			{
				num |= 1;
			}
			if (distance < 120f && g.Back == BackGear.Banner)
			{
				num |= 2;
			}
			if (distance < 120f && ((head & (HeadGear.CrownOpen | HeadGear.CrownHelm)) != HeadGear.None || g.Back == BackGear.Mantle))
			{
				num |= 4;
			}
			if (distance < 40f && ((head & (HeadGear.Hat | HeadGear.Hood)) != HeadGear.None || g.Neck != NeckGear.None || (g.Body & BodyGear.KeyRing) != BodyGear.None || g.Hand == HandGear.Baton || g.Hand == HandGear.Ledger))
			{
				num |= 8;
			}
			if (distance < 40f && (head & (HeadGear.Circlet | HeadGear.Laurel)) != HeadGear.None)
			{
				num |= 0x10;
			}
			if (distance < 120f && ((g.Body & BodyGear.Robe) != BodyGear.None || g.Hand == HandGear.Staff))
			{
				num |= 0x20;
			}
			return num;
		}

		public static WearPlan Plan(in GearLook g, in WearContext c, Bound[] sets)
		{
			WearPlan result = new WearPlan
			{
				Sash = g.Sash,
				Style = g.Style
			};
			if (!g.Any)
			{
				return result;
			}
			float distance = c.Distance;
			HeadGear head = g.Head;
			bool flag = !string.IsNullOrEmpty(c.HelmetId);
			bool flag2 = RegaliaRules.HighHelmet(c.HelmetId);
			bool flag3 = distance < 40f && !c.Child;
			bool flag4 = flag && !g.ReplacesHelmet && flag3;
			bool flag5 = !string.IsNullOrEmpty(c.ChestId) && flag3 && !c.Expecting;
			WeaponKind kind = c.Kind;
			bool flag6 = !c.HandsBusy && !c.TorchHeld && kind != WeaponKind.Bow && kind != WeaponKind.Spear && kind != WeaponKind.Shotgun && kind != WeaponKind.Gun && kind != WeaponKind.Staff && c.Stance < 0.05f && !c.Working && !c.Carrying && c.Sit <= 0f && !c.Swimming && !c.Host;
			bool flag7 = c.Sit > 0.3f;
			bool flag8 = (head & HeadGear.Coif) != 0;
			byte hides = 0;
			bool head2 = false;
			bool under = false;
			Bound bound = At(sets, RegaliaKit.Set.Soldier);
			if (bound != null && distance < 60f)
			{
				ulong num = 0uL;
				if (g.Sash != SashGear.None && !c.Expecting)
				{
					num |= bound.Bit(0) | bound.Bit(1);
				}
				if ((head & (HeadGear.Plume | HeadGear.PlumeTall)) != 0 && flag)
				{
					if (flag4)
					{
						num |= bound.Bit(2);
					}
					else if (distance >= 40f && bound.Has(5) && ArmorMeshes.MetalOf(c.HelmetId) != ArmorMeshes.Metal.Mythril)
					{
						num |= bound.Bit(5) | bound.Bit(2);
					}
				}
				if (flag8)
				{
					num |= bound.Bit(3);
					if (!flag5)
					{
						num |= bound.Bit(4);
					}
				}
				result.Soldier = Finish(bound, num, in c, ref hides, ref head2, ref under);
			}
			bound = At(sets, RegaliaKit.Set.Honour);
			if (bound != null && distance < 40f)
			{
				ulong num2 = 0uL;
				if ((head & HeadGear.Circlet) != HeadGear.None)
				{
					num2 |= bound.Bit(flag8 ? 2 : (flag4 ? 1 : 0));
				}
				if ((head & HeadGear.Laurel) != HeadGear.None)
				{
					num2 |= bound.Bit(flag8 ? 6 : ((!flag4) ? 3 : (flag2 ? 5 : 4)));
				}
				result.Honour = Finish(bound, num2, in c, ref hides, ref head2, ref under);
			}
			bound = At(sets, RegaliaKit.Set.Court);
			if (bound != null && distance < 40f)
			{
				ulong num3 = 0uL;
				if ((head & HeadGear.Hat) != HeadGear.None)
				{
					num3 |= bound.Bit(0);
					result.ShowHair = (result.HideTallHair = (result.HideArmourHead = bound.Has(0)));
				}
				if ((head & HeadGear.Hood) != HeadGear.None)
				{
					num3 |= bound.Bit(1);
					if (!flag5)
					{
						num3 |= bound.Bit(2);
					}
					result.HideArmourHead |= bound.Has(1);
				}
				if (g.Neck == NeckGear.ChainOfOffice)
				{
					num3 |= bound.Bit(flag5 ? 4 : 3);
				}
				else if (g.Neck == NeckGear.SealChain)
				{
					num3 |= bound.Bit(flag5 ? 6 : 5);
				}
				if ((g.Body & BodyGear.KeyRing) != BodyGear.None)
				{
					num3 |= bound.Bit(flag5 ? 8 : 7);
				}
				if (flag6 && g.Hand == HandGear.Baton)
				{
					num3 |= bound.Bit(9);
				}
				else if (flag6 && g.Hand == HandGear.Ledger)
				{
					num3 |= bound.Bit(10);
				}
				result.Court = Finish(bound, num3, in c, ref hides, ref head2, ref under);
			}
			bound = At(sets, RegaliaKit.Set.Royal);
			if (bound != null && distance < 120f)
			{
				ulong num4 = 0uL;
				if ((head & HeadGear.CrownHelm) != HeadGear.None)
				{
					num4 |= bound.Bit(2);
					if (distance < 40f && bound.Has(2))
					{
						result.HideArmourHead = true;
					}
				}
				else if ((head & HeadGear.CrownOpen) != HeadGear.None)
				{
					num4 |= bound.Bit((flag4 && bound.Has(1)) ? 1 : 0);
				}
				if (g.Back == BackGear.Mantle)
				{
					num4 |= bound.Bit((flag5 && bound.Has(4)) ? 4 : 3);
					if (!c.Swimming && !flag7)
					{
						num4 |= bound.Bit(5) | bound.Bit(6);
					}
				}
				result.Royal = Finish(bound, num4, in c, ref hides, ref head2, ref under);
			}
			bound = At(sets, RegaliaKit.Set.Captain);
			if (bound != null && distance < 120f && g.Back == BackGear.Banner && !flag7 && !c.Swimming && !c.Indoors)
			{
				result.Captain = Finish(bound, bound.Bit(0) | bound.Bit((!flag5 || !bound.Has(2)) ? 1 : 2) | bound.Bit(3) | bound.Bit(4), in c, ref hides, ref head2, ref under);
			}
			bound = At(sets, RegaliaKit.Set.Robe);
			if (bound != null && distance < 120f)
			{
				ulong num5 = 0uL;
				if ((g.Body & BodyGear.Robe) != BodyGear.None)
				{
					num5 |= bound.Bit((flag5 && bound.Has(1)) ? 1 : 0) | bound.Bit(4);
					if (!flag7 && !c.Swimming)
					{
						num5 |= bound.Bit(2) | bound.Bit(3);
					}
				}
				if (g.Hand == HandGear.Staff && flag6 && distance < 45f)
				{
					num5 |= bound.Bit(5);
				}
				result.Robe = Finish(bound, num5, in c, ref hides, ref head2, ref under);
			}
			result.Halberd = g.Hand == HandGear.Halberd && kind != WeaponKind.Bow && kind != WeaponKind.Gun && kind != WeaponKind.Shotgun && !c.Figure && !c.Host && !c.Indoors && !c.Carrying && !c.Working && !c.HandsBusy && !c.Swimming && c.Sit <= 0f && distance < 45f;
			result.HideHair = (hides & 1) != 0;
			result.HideBeard = (hides & 2) != 0;
			result.HideHats = (hides & 4) != 0 || head2;
			result.HideUnder = under;
			if (result.HideHair)
			{
				result.ShowHair = false;
			}
			return result;
		}

		private static Bound At(Bound[] sets, RegaliaKit.Set s)
		{
			if (sets == null || (int)s >= sets.Length)
			{
				return null;
			}
			return sets[(uint)s];
		}

		private static ulong Finish(Bound b, ulong bits, in WearContext c, ref byte hides, ref bool head, ref bool under)
		{
			if (c.Expecting)
			{
				bits &= ~b.Torso;
			}
			if (bits == 0L)
			{
				return 0uL;
			}
			if ((bits & b.HeadPieces) != 0L)
			{
				head = true;
			}
			if ((bits & b.Covers) != 0L)
			{
				under = true;
			}
			for (int i = 0; i < b.Piece.Length; i++)
			{
				if (b.Piece[i] >= 0 && (bits & (ulong)(1L << b.Piece[i])) != 0L)
				{
					hides |= b.Hides[i];
				}
			}
			return bits;
		}

		public static bool SetOf(MeshModel m, out RegaliaKit.Set set)
		{
			set = RegaliaKit.Set.Soldier;
			if (m == null)
			{
				return false;
			}
			string text = m.Info("set");
			for (int i = 0; i < 6; i++)
			{
				string text2 = RegaliaKit.SetName((RegaliaKit.Set)i);
				if (text == text2 || (text == null && m.Name != null && m.Name.StartsWith("rg_" + text2 + "_", StringComparison.Ordinal)))
				{
					set = (RegaliaKit.Set)i;
					return true;
				}
			}
			return false;
		}

		public static Bound Bind(MeshModel set, MeshModel body)
		{
			BindProblem = null;
			if (set == null || body == null)
			{
				BindProblem = "no model";
				return null;
			}
			if (!SetOf(set, out var set2))
			{
				BindProblem = set.Name + " is not a regalia set";
				return null;
			}
			if (set.Parts.Length > 64)
			{
				BindProblem = set.Name + " has more than 64 parts";
				return null;
			}
			string[] array = RegaliaKit.SkeletonOf(set2);
			Bound bound = new Bound
			{
				Model = set,
				Body = body,
				Set = set2,
				FromBody = new int[set.Parts.Length],
				Bones = new int[array.Length],
				BoneFrom = new int[array.Length]
			};
			for (int i = 0; i < bound.FromBody.Length; i++)
			{
				bound.FromBody[i] = -1;
			}
			for (int j = 0; j < array.Length; j++)
			{
				int num = set.PartIndex(array[j]);
				int num2 = body.PartIndex(array[j]);
				if (num < 0 || num2 < 0)
				{
					BindProblem = set.Name + " on " + body.Name + ": no " + array[j] + " in the " + ((num < 0) ? "set" : "body");
					return null;
				}
				MeshPart meshPart = set.Parts[num];
				MeshPart meshPart2 = body.Parts[num2];
				string text = ((meshPart.Parent >= 0) ? set.Parts[meshPart.Parent].Name : null);
				string text2 = ((meshPart2.Parent >= 0) ? body.Parts[meshPart2.Parent].Name : null);
				if (text != text2)
				{
					BindProblem = set.Name + " on " + body.Name + ": " + array[j] + "'s parent is " + (text ?? "none") + ", the body's " + (text2 ?? "none");
					return null;
				}
				Vec3 vec = meshPart.Pivot - meshPart2.Pivot;
				if (Vec3.Dot(vec, vec) > 1E-06f)
				{
					BindProblem = $"{set.Name} on {body.Name}: {array[j]}'s pivot is {Math.Sqrt(Vec3.Dot(vec, vec)) * 1000.0:F1} mm off";
					return null;
				}
				bound.FromBody[num] = num2;
				bound.Bones[j] = num;
				bound.BoneFrom[j] = num2;
				bound.Skeleton |= (ulong)(1L << num);
			}
			for (int k = 0; k < set.Parts.Length; k++)
			{
				if (set.Parts[k].VertexCount == 0)
				{
					bound.Empty |= (ulong)(1L << k);
				}
			}
			string text3 = set.Info("style");
			if (text3 == null && RegaliaKit.Styled(set2) && set.Name != null)
			{
				string text4 = "rg_" + RegaliaKit.SetName(set2) + "_";
				if (set.Name.Length > text4.Length + 1 && set.Name.StartsWith(text4, StringComparison.Ordinal) && set.Name[text4.Length + 1] == '_')
				{
					text3 = set.Name.Substring(text4.Length, 1);
				}
			}
			int style;
			switch (text3)
			{
			default:
				style = -1;
				break;
			case "c":
				style = 2;
				break;
			case "b":
				style = 1;
				break;
			case "a":
				style = 0;
				break;
			}
			bound.Style = (KingdomStyle)style;
			string[] array2 = RegaliaKit.PartsOf(set2);
			bound.Piece = new int[array2.Length];
			bound.Hides = new byte[array2.Length];
			string text5 = set.Info("torso");
			for (int l = 0; l < array2.Length; l++)
			{
				int num3 = set.PartIndex(array2[l]);
				bound.Piece[l] = num3;
				if (num3 >= 0)
				{
					MeshPart meshPart3 = set.Parts[num3];
					if (meshPart3.Parent >= 0 && set.Parts[meshPart3.Parent].Name == "head")
					{
						bound.HeadPieces |= (ulong)(1L << num3);
					}
					bound.Hides[l] = HidesOf(set, array2[l], bound.Style);
					if ((text5 != null) ? Listed(text5, array2[l]) : DefaultTorso(array2[l]))
					{
						bound.Torso |= (ulong)(1L << num3);
					}
					if (array2[l] == "mantle_yoke" || array2[l] == "mantle_yoke_over" || array2[l] == "robe_yoke" || array2[l] == "robe_yoke_over" || array2[l] == "robe")
					{
						bound.Covers |= (ulong)(1L << num3);
					}
				}
			}
			return bound;
		}

		private static byte HidesOf(MeshModel set, string piece, KingdomStyle style)
		{
			string text = set.Info("hide." + piece);
			if (text == null)
			{
				switch (piece)
				{
				case "helm":
					return (byte)(5 | ((style != KingdomStyle.GoldWings) ? 2 : 0));
				case "coif":
					return 7;
				case "hood":
				case "helmcap":
					return 5;
				default:
					return 0;
				}
			}
			byte b = 0;
			if (Listed(text, "hair"))
			{
				b |= 1;
			}
			if (Listed(text, "beard"))
			{
				b |= 2;
			}
			if (Listed(text, "hats"))
			{
				b |= 4;
			}
			return b;
		}

		private static bool DefaultTorso(string piece)
		{
			switch (piece)
			{
			default:
				return piece == "mantle_yoke_over";
			case "sash":
			case "sashknot":
			case "coif_cape":
			case "chain":
			case "chain_over":
			case "seal":
			case "seal_over":
			case "hood_cape":
			case "robe_yoke":
			case "robe_yoke_over":
			case "robe_lapels":
			case "mantle_yoke":
				return true;
			}
		}

		private static bool Listed(string list, string word)
		{
			int num = 0;
			while (num <= list.Length)
			{
				int num2 = list.IndexOf(',', num);
				if (num2 < 0)
				{
					num2 = list.Length;
				}
				if (num2 - num == word.Length && string.CompareOrdinal(list, num, word, 0, word.Length) == 0)
				{
					return true;
				}
				num = num2 + 1;
			}
			return false;
		}

		public static Bound BindCached(MeshModel set, MeshModel body)
		{
			PairKey key = new PairKey(set, body);
			if (Bounds.TryGetValue(key, out var value))
			{
				return value;
			}
			value = Bind(set, body);
			if (value == null)
			{
				Warn?.Invoke("regalia: " + BindProblem);
			}
			if (Bounds.Count > 256)
			{
				Bounds.Clear();
			}
			Bounds[key] = value;
			return value;
		}

		public static void Pose(MeshInstance inst, Bound b, MeshInstance body)
		{
			inst.Position = body.Position;
			inst.Basis = body.Basis;
			inst.EnsurePose();
			Mat3[] partRotation = body.PartRotation;
			Vec3[] partOffset = body.PartOffset;
			float[] partScale = body.PartScale;
			int[] bones = b.Bones;
			int[] boneFrom = b.BoneFrom;
			for (int i = 0; i < bones.Length; i++)
			{
				int num = bones[i];
				int num2 = boneFrom[i];
				inst.PartRotation[num] = ((partRotation != null && num2 < partRotation.Length) ? partRotation[num2] : Mat3.Identity);
				inst.PartOffset[num] = ((partOffset != null && num2 < partOffset.Length) ? partOffset[num2] : default(Vec3));
				inst.PartScale[num] = ((partScale != null && num2 < partScale.Length) ? partScale[num2] : 1f);
			}
		}

		public static uint[] Palette(MeshModel m, in RegaliaColors c, SashGear sash, uint helm)
		{
			PaletteKey key = new PaletteKey(m, in c, sash, helm);
			if (Palettes.TryGetValue(key, out var value))
			{
				return value;
			}
			value = new uint[m.SlotNames.Length];
			bool flag = false;
			for (int i = 0; i < value.Length; i++)
			{
				value[i] = c.Slot(m.SlotNames[i], sash, helm);
				flag |= value[i] != 0;
			}
			if (!flag)
			{
				value = null;
			}
			if (Palettes.Count >= 2048)
			{
				Palettes.Clear();
			}
			Palettes[key] = value;
			return value;
		}

		public static MeshModel FigureKit(MeshModel m, ulong hidden)
		{
			KitKey key = new KitKey(m, hidden);
			if (Kits.TryGetValue(key, out var value))
			{
				return value;
			}
			MeshPart[] array = new MeshPart[m.Parts.Length];
			Vec3 min = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
			Vec3 max = new Vec3(float.MinValue, float.MinValue, float.MinValue);
			for (int i = 0; i < array.Length; i++)
			{
				MeshPart meshPart = m.Parts[i];
				if (i < 64 && (hidden & (ulong)(1L << i)) != 0L)
				{
					array[i] = new MeshPart
					{
						Name = meshPart.Name,
						Parent = meshPart.Parent,
						Pivot = meshPart.Pivot,
						Pos = new float[0],
						Nrm = new float[0],
						Tri = new int[0],
						Slot = new byte[0],
						Axis = new byte[0]
					};
					continue;
				}
				array[i] = meshPart;
				for (int j = 0; j < meshPart.VertexCount; j++)
				{
					float num = meshPart.Pos[j * 3];
					float num2 = meshPart.Pos[j * 3 + 1];
					float num3 = meshPart.Pos[j * 3 + 2];
					if (num < min.X)
					{
						min.X = num;
					}
					if (num2 < min.Y)
					{
						min.Y = num2;
					}
					if (num3 < min.Z)
					{
						min.Z = num3;
					}
					if (num > max.X)
					{
						max.X = num;
					}
					if (num2 > max.Y)
					{
						max.Y = num2;
					}
					if (num3 > max.Z)
					{
						max.Z = num3;
					}
				}
			}
			value = m.Derive(m.Name + "~kit", array);
			if (min.X <= max.X)
			{
				value.Min = min;
				value.Max = max;
			}
			if (Kits.Count >= 16)
			{
				Kits.Clear();
			}
			Kits[key] = value;
			return value;
		}

		public static BodyMasks MasksOf(MeshModel body)
		{
			if (body == null)
			{
				return Nothing;
			}
			if (Masks.TryGetValue(body, out var value))
			{
				return value;
			}
			value = new BodyMasks();
			string text = body.Info("hair_tall");
			string text2 = body.Info("hair_hat");
			for (int i = 0; i < body.Parts.Length && i < 64; i++)
			{
				string name = body.Parts[i].Name;
				if (name == null)
				{
					continue;
				}
				ulong num = (ulong)(1L << i);
				if (name.StartsWith("hair_", StringComparison.Ordinal))
				{
					value.Hair |= num;
					string text3 = name.Substring("hair_".Length);
					if (text != null && Listed(text, text3))
					{
						value.Tall |= num;
					}
					if (text2 != null && text2 == text3)
					{
						value.HatHair |= num;
					}
				}
				else if (name == "beard" || name.StartsWith("beard_", StringComparison.Ordinal))
				{
					value.Beard |= num;
				}
				else if (name.StartsWith("hat_", StringComparison.Ordinal))
				{
					value.Hats |= num;
				}
				else if (name.StartsWith("cut_", StringComparison.Ordinal) || name.StartsWith("belt_", StringComparison.Ordinal))
				{
					value.Under |= num;
				}
			}
			if (Masks.Count > 256)
			{
				Masks.Clear();
			}
			Masks[body] = value;
			return value;
		}

		public static int Dress(Wearer w, in GearLook g, in WearContext c, in Dressing d, List<MeshInstance> meshes, out int triangles, out int missing)
		{
			triangles = 0;
			missing = 0;
			int num = SetsWorn(in g, in c);
			if (num == 0 || RegaliaKit.Library == null || d.Body?.Model == null || d.Fit == null)
			{
				w.Clean();
				return 0;
			}
			LookUp(w, num, g.Style, c.Female, c.Lod, d.Fit, ref missing);
			WearPlan plan = (w.Plan = Plan(in g, in c, w.Bounds));
			w.Helmet = c.HelmetId;
			w.Distance = c.Distance;
			w.Child = c.Child;
			int num2 = 0;
			float clear = float.PositiveInfinity;
			MeshInstance body = d.Body;
			for (int i = 0; i < 6; i++)
			{
				RegaliaKit.Set set = (RegaliaKit.Set)i;
				ulong num3 = plan[set];
				Bound bound = w.Bounds[i];
				if (num3 == 0L || bound == null)
				{
					w.Shown[i] = 0uL;
					continue;
				}
				MeshInstance meshInstance = w.Sets[i] ?? (w.Sets[i] = new MeshInstance());
				MeshModel model = bound.Model;
				if (w.Posed[i] != model)
				{
					meshInstance.Model = model;
					meshInstance.EnsurePose();
					meshInstance.ResetPose();
					w.Posed[i] = model;
				}
				meshInstance.Model = model;
				Pose(meshInstance, bound, body);
				ulong num4 = ~num3 | bound.Skeleton | bound.Empty;
				uint helm = 0u;
				switch (set)
				{
				case RegaliaKit.Set.Soldier:
					num4 |= FitSoldier(w, bound, meshInstance, num3, in c, in d, (g.Head & HeadGear.PlumeTall) != 0, ref helm);
					break;
				case RegaliaKit.Set.Royal:
					if ((num3 & bound.Bit(5)) != 0L)
					{
						ClothSway.Mantle(ref w.Upper, ref w.Hem, ref w.Side, in d.Sway, ClothSway.TorsoTilt(body, d.Rig.Hips, d.Rig.Torso), w.Seed);
						Hinge(w, bound, meshInstance, in d, in c, 5, 6, ref w.Upper, ref w.Hem, w.Side.A, ref clear);
					}
					break;
				case RegaliaKit.Set.Robe:
					if ((num3 & bound.Bit(2)) != 0L)
					{
						ClothSway.Mantle(ref w.RobeUpper, ref w.RobeHem, ref w.RobeSide, in d.Sway, ClothSway.TorsoTilt(body, d.Rig.Hips, d.Rig.Torso), w.Seed + 0.5f);
						Hinge(w, bound, meshInstance, in d, in c, 2, 3, ref w.RobeUpper, ref w.RobeHem, w.RobeSide.A, ref clear);
					}
					break;
				case RegaliaKit.Set.Captain:
				{
					ClothSway.Banner(ref w.Lift, ref w.Yaw, ref w.Tail, in d.Sway, ClothSway.TorsoTilt(body, d.Rig.Hips, d.Rig.Torso), w.Seed);
					int num5 = bound.PartOf(3);
					int num6 = bound.PartOf(4);
					if (num5 >= 0)
					{
						meshInstance.PartRotation[num5] = Mat3.RotZ(w.Yaw.A) * Mat3.RotX(w.Lift.A);
					}
					if (num6 >= 0)
					{
						meshInstance.PartRotation[num6] = Mat3.RotX(w.Tail.A);
					}
					break;
				}
				}
				meshInstance.Hidden = num4;
				meshInstance.Palette = Palette(model, in d.Colors, (set == RegaliaKit.Set.Soldier) ? plan.Sash : SashGear.None, helm);
				VoxelObject template = d.Template;
				meshInstance.Tag = template.Tag;
				meshInstance.Flash = template.Flash;
				meshInstance.Multiply = template.Multiply;
				meshInstance.SortDepth = template.SortDepth;
				meshInstance.NoShadow = template.NoShadow || (c.Distance > 20f && (set == RegaliaKit.Set.Soldier || set == RegaliaKit.Set.Court || set == RegaliaKit.Set.Honour));
				meshInstance.KeepSmall = body.KeepSmall;
				meshInstance.Static = false;
				if (c.Figure)
				{
					meshInstance.Model = FigureKit(model, num4);
				}
				ulong num7 = ~num4;
				w.Shown[i] = num7;
				for (int j = 0; j < model.Parts.Length && j < 64; j++)
				{
					if ((num7 & (ulong)(1L << j)) != 0L)
					{
						triangles += model.Parts[j].TriangleCount;
					}
				}
				meshes.Add(meshInstance);
				num2++;
			}
			w.Clear = clear;
			HideOnBody(w, in plan, in c, in d);
			return num2;
		}

		private static void LookUp(Wearer w, int sets, KingdomStyle style, bool female, int lod, MeshModel fit, ref int missing)
		{
			bool flag = w.KeyStyle != style || w.KeyFemale != female || w.KeyLod != lod || w.KeyFit != fit || (w.KeySets & sets) != sets;
			if (!flag && w.LookAgain > 0 && --w.LookAgain == 0)
			{
				flag = true;
			}
			if (flag)
			{
				w.KeyStyle = style;
				w.KeyFemale = female;
				w.KeyLod = lod;
				w.KeyFit = fit;
				w.KeySets |= sets;
				w.LookAgain = 0;
				for (int i = 0; i < 6; i++)
				{
					if ((w.KeySets & (1 << i)) == 0)
					{
						w.Models[i] = null;
						w.Bounds[i] = null;
						continue;
					}
					string text = RegaliaKit.ModelName((RegaliaKit.Set)i, style, female, lod);
					MeshModel meshModel = ((text != null) ? RegaliaKit.Library(text) : null);
					w.Models[i] = meshModel;
					w.Bounds[i] = ((meshModel != null) ? BindCached(meshModel, fit) : null);
					if (text != null && w.Bounds[i] == null)
					{
						w.LookAgain = 120;
					}
				}
			}
			for (int j = 0; j < 6; j++)
			{
				if ((sets & (1 << j)) != 0 && w.Bounds[j] == null && (style != KingdomStyle.None || !RegaliaKit.Styled((RegaliaKit.Set)j)))
				{
					missing++;
				}
			}
			for (int k = 0; k < 6; k++)
			{
				if ((sets & (1 << k)) == 0)
				{
					w.Shown[k] = 0uL;
				}
			}
		}

		private static ulong FitSoldier(Wearer w, Bound b, MeshInstance inst, ulong bits, in WearContext c, in Dressing d, bool tall, ref uint helm)
		{
			ulong num = 0uL;
			string chestId = ((!string.IsNullOrEmpty(c.ChestId) && c.Distance < 40f && !c.Expecting && !c.Child) ? c.ChestId : null);
			for (int i = 0; i <= 1; i++)
			{
				int num2 = b.PartOf(i);
				if (num2 >= 0 && (bits & (ulong)(1L << num2)) != 0L)
				{
					if (RegaliaFit.Sash(d.Fit, num2, b.Model, chestId, out var fit))
					{
						inst.PartRotation[num2] = fit.F;
						inst.PartOffset[num2] = fit.D;
					}
					else
					{
						num |= (ulong)(1L << num2);
					}
				}
			}
			int num3 = b.PartOf(2);
			if (num3 >= 0 && (bits & (ulong)(1L << num3)) != 0L)
			{
				Vec3 at;
				float num4;
				float num5;
				if ((bits & b.Bit(5)) != 0)
				{
					at = RegaliaFit.CapSocket(b.Model);
					num4 = 0.1f;
					num5 = 1f;
					helm = ArmorMeshes.MaterialColor(c.HelmetId);
				}
				else
				{
					if (!RegaliaFit.Socket(d.Fit, c.HelmetId, tall, out at))
					{
						return num | (ulong)(1L << num3);
					}
					ArmorMeshes.Metal m = ArmorMeshes.MetalOf(c.HelmetId);
					num4 = RegaliaFit.Tilt(m);
					num5 = RegaliaFit.Height(m);
				}
				ClothSway.Plume(ref w.PlumeSwing, in d.Sway, w.Seed, tall);
				float num6 = (tall ? 1.35f : 1f);
				float num7 = num4 + w.PlumeSwing.A;
				float num8 = num6 * num5;
				if (tall)
				{
					num7 -= HeadBack(in d);
				}
				if (tall && w.Plan.Captain != 0L)
				{
					num7 = ClearOfPole(w, b, in d, num3, at, num7, num6, num8);
				}
				Mat3 mat = Mat3.RotX(0f - num7);
				inst.PartRotation[num3] = new Mat3(mat.C0 * num6, mat.C1 * num6, mat.C2 * num8);
				inst.PartOffset[num3] = at - b.Model.Parts[num3].Pivot;
			}
			return num;
		}

		private static float HeadBack(in Dressing d)
		{
			int num = ((d.Rig != null) ? d.Rig.Head : (-1));
			Mat3[] partRotation = d.Body.PartRotation;
			if (num < 0 || partRotation == null || num >= partRotation.Length)
			{
				return 0f;
			}
			Mat3 mat = partRotation[num];
			float x = mat.C1.X;
			float y = mat.C1.Y;
			float num2 = (float)Math.Sqrt(x * x + y * y);
			float num3 = (float)Math.Sqrt(mat.C2.X * mat.C2.X + mat.C2.Y * mat.C2.Y + mat.C2.Z * mat.C2.Z);
			if (num2 < 0.0001f || num3 < 0.0001f)
			{
				return 0f;
			}
			float num4 = (mat.C2.X * x + mat.C2.Y * y) / (num2 * num3);
			if (!(num4 > 0f))
			{
				return 0f;
			}
			return (float)Math.Asin(Math.Min(1f, num4));
		}

		private static float ClearOfPole(Wearer w, Bound b, in Dressing d, int plume, Vec3 at, float lean, float s, float sz)
		{
			Bound bound = w.Bounds[1];
			int num = ((d.Rig != null) ? d.Rig.Head : (-1));
			Mat3[] partRotation = d.Body.PartRotation;
			if (bound == null || num < 0 || partRotation == null || num >= partRotation.Length || num >= d.Body.Model.Parts.Length)
			{
				return lean;
			}
			RegaliaFit.PoleLine pole = RegaliaFit.PoleOf(bound.Model);
			if (!pole.Valid)
			{
				return lean;
			}
			RegaliaFit.PlumeCorners(b.Model, plume, out var top, out var back);
			Vec3 pivot = d.Body.Model.Parts[num].Pivot;
			float num2 = ((d.Body.PartScale != null) ? d.Body.PartScale[num] : 1f);
			Vec3 socket = partRotation[num].Mul(at - pivot) * num2 + pivot;
			if (d.Body.PartOffset != null)
			{
				socket += d.Body.PartOffset[num];
			}
			if (InFront(in pole, in partRotation[num], num2, socket, top, back, lean, s, sz))
			{
				return lean;
			}
			float num3 = lean - 1f;
			float num4 = lean;
			if (!InFront(in pole, in partRotation[num], num2, socket, top, back, num3, s, sz))
			{
				return num3;
			}
			for (int i = 0; i < 6; i++)
			{
				float num5 = (num3 + num4) * 0.5f;
				if (InFront(in pole, in partRotation[num], num2, socket, top, back, num5, s, sz))
				{
					num3 = num5;
				}
				else
				{
					num4 = num5;
				}
			}
			return num3;
		}

		private static bool InFront(in RegaliaFit.PoleLine pole, in Mat3 head, float sc, Vec3 socket, Vec3 top, Vec3 back, float lean, float s, float sz)
		{
			if (Ahead(in pole, in head, sc, socket, top, lean, s, sz))
			{
				return Ahead(in pole, in head, sc, socket, back, lean, s, sz);
			}
			return false;
		}

		private static bool Ahead(in RegaliaFit.PoleLine pole, in Mat3 head, float sc, Vec3 socket, Vec3 corner, float lean, float s, float sz)
		{
			float num = (float)Math.Cos(lean);
			float num2 = (float)Math.Sin(lean);
			float num3 = corner.Y * s;
			float num4 = corner.Z * sz;
			Vec3 vec = head.Mul(new Vec3(corner.X * s, num3 * num + num4 * num2, num4 * num - num3 * num2)) * sc + socket;
			float num5 = (vec.Z - pole.Foot.Z) / (pole.Top.Z - pole.Foot.Z);
			float num6 = pole.Foot.X + (pole.Top.X - pole.Foot.X) * num5;
			float num7 = pole.Foot.Y + (pole.Top.Y - pole.Foot.Y) * num5;
			if (!(Math.Abs(vec.X - num6) > pole.Radius + 0.05f))
			{
				return vec.Y <= num7 - pole.Radius - 0.015f;
			}
			return true;
		}

		private static void Hinge(Wearer w, Bound b, MeshInstance inst, in Dressing d, in WearContext c, int upperPiece, int hemPiece, ref ClothSway.Swing upper, ref ClothSway.Swing hem, float side, ref float clear)
		{
			int num = b.PartOf(upperPiece);
			int num2 = b.PartOf(hemPiece);
			if (num < 0)
			{
				return;
			}
			float a = upper.A;
			float a2 = hem.A;
			float margin = ((!string.IsNullOrEmpty(c.ChestId) && c.Distance < 40f) ? 0.04f : 0.025f);
			if (!c.Figure && ClothSway.Clear(d.Body, d.Rig, b.Model, num, num2, ref a, ref a2, margin, d.Fit))
			{
				if (a > upper.A)
				{
					upper.A = a;
					if (upper.V < 0f)
					{
						upper.V = 0f;
					}
				}
				if (a2 > hem.A)
				{
					hem.A = a2;
					if (hem.V < 0f)
					{
						hem.V = 0f;
					}
				}
			}
			if (!c.Figure)
			{
				clear = Math.Min(clear, ClothSway.LastClearance);
			}
			inst.PartRotation[num] = Mat3.RotY(side) * Mat3.RotX(upper.A);
			if (num2 >= 0)
			{
				inst.PartRotation[num2] = Mat3.RotX(hem.A);
			}
		}

		private static void HideOnBody(Wearer w, in WearPlan plan, in WearContext c, in Dressing d)
		{
			MeshInstance body = d.Body;
			BodyMasks bodyMasks = MasksOf(body.Model);
			if (plan.ShowHair)
			{
				body.Hidden &= ~(bodyMasks.Hair & ~bodyMasks.Tall & ~d.OwnHidden);
			}
			if (plan.HideTallHair && !plan.HideHair && ((~body.Hidden | (plan.ShowHair ? (~d.OwnHidden) : 0)) & bodyMasks.Tall) != 0L)
			{
				body.Hidden &= ~bodyMasks.HatHair;
			}
			ulong num = 0uL;
			if (plan.HideHair)
			{
				num |= bodyMasks.Hair;
			}
			if (plan.HideBeard)
			{
				num |= bodyMasks.Beard;
			}
			if (plan.HideHats)
			{
				num |= bodyMasks.Hats;
			}
			if (plan.HideUnder)
			{
				num |= bodyMasks.Under;
			}
			if (plan.HideTallHair)
			{
				num |= bodyMasks.Tall;
			}
			body.Hidden |= num;
			int num2 = ((d.Rig != null) ? d.Rig.Head : (-1));
			if (!plan.HideArmourHead || d.Armour == null || num2 < 0 || num2 >= 64)
			{
				return;
			}
			for (int i = 0; i < d.ArmourShown && i < d.Armour.Length; i++)
			{
				if (d.Armour[i] != null)
				{
					d.Armour[i].Hidden |= (ulong)(1L << num2);
				}
			}
		}
	}
}
