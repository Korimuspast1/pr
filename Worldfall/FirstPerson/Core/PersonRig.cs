using System;

namespace FirstPerson.Core
{
	public sealed class PersonRig
	{
		public enum Aid : byte
		{
			None,
			Cane,
			Crutch
		}

		public enum Fidget
		{
			None,
			WeightShift,
			ArmsCrossed,
			HandsOnHips,
			HandsBehind,
			ScratchHead,
			Stretch
		}

		private struct BladeKey
		{
			public float A;

			public float Pitch;

			public float Across;

			public float Twist;

			public float Elbow;

			public float Turn;

			public float Bend;

			public float Wrist;

			public float Lunge;

			public BladeKey(float a, float pitch, float across, float twist, float elbow, float turn, float bend, float wrist, float lunge)
			{
				A = a;
				Pitch = pitch;
				Across = across;
				Twist = twist;
				Elbow = elbow;
				Turn = turn;
				Bend = bend;
				Wrist = wrist;
				Lunge = lunge;
			}
		}

		public const float CaneLength = 0.86f;

		public const float CrutchLength = 0.86f;

		private Vec3 _hugAnchor;

		private Vec3 _shoulderAnchor;

		private bool _anchors;

		public readonly int Hips;

		public readonly int Torso;

		public readonly int Head;

		public readonly int HairShort;

		public readonly int HairLong;

		public readonly int Beard;

		public readonly int UpperArmL;

		public readonly int LowerArmL;

		public readonly int UpperArmR;

		public readonly int LowerArmR;

		public readonly int UpperLegL;

		public readonly int LowerLegL;

		public readonly int UpperLegR;

		public readonly int LowerLegR;

		public readonly int SlotSkin;

		public readonly int SlotHair;

		public readonly int SlotEyes;

		public readonly int SlotShirt;

		public readonly int SlotShirtDark;

		public readonly int SlotPants;

		public readonly int SlotShoes;

		public readonly int SlotBelt;

		public readonly int SlotMouth;

		public readonly int SlotTusk;

		public readonly int SlotAccent;

		public readonly MeshModel Model;

		public readonly bool Robe;

		public readonly bool AlwaysBearded;

		public readonly bool Hat;

		public readonly bool ChildBody;

		private readonly float _hip;

		private readonly float _height;

		public static float TwistSign = -1f;

		public static float HeadSign = -1f;

		public static float PlantReach = 0.3f;

		public static float[] PlantStats;

		private Vec3 _soleL;

		private Vec3 _soleR;

		private float _thigh;

		private float _shin;

		public readonly ulong FemaleParts;

		public readonly ulong PlainMan;

		public readonly ulong PlainWoman;

		private static readonly Mat3 Ident = Mat3.Identity;

		private static readonly Mat3 TryForward = Mat3.RotX(0.02f);

		private static readonly Mat3 TryAside = Mat3.RotY(0.02f);

		private static readonly MeshInstance CheckBody = new MeshInstance();

		private static readonly WalkCycle.Walker CheckWalker = new WalkCycle.Walker();

		private bool[] _posed;

		public const float CrawlTilt = 1.25f;

		public const float BladeGuardWrist = 0.35f;

		private static readonly BladeKey[] Forehand = new BladeKey[6]
		{
			new BladeKey(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0.35f, 0f),
			new BladeKey(0.3f, -2.85f, -0.55f, 0.35f, 1.75f, 0.45f, -0.08f, -0.8f, 0f),
			new BladeKey(0.38f, -2.65f, -0.25f, 0.2f, 0.85f, 0.25f, 0.02f, 0.2f, 0.4f),
			new BladeKey(0.47f, -1.55f, 0.3f, 0f, 0.3f, -0.2f, 0.15f, 1.45f, 1f),
			new BladeKey(0.58f, -0.85f, 0.8f, -0.15f, 0.2f, -0.5f, 0.25f, 1.55f, 0.8f),
			new BladeKey(1f, 0f, 0f, 0f, 0f, 0f, 0f, 0.35f, 0f)
		};

		private static readonly BladeKey[] Backhand = new BladeKey[6]
		{
			new BladeKey(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0.35f, 0f),
			new BladeKey(0.3f, -2.35f, 0.8f, -0.3f, 1.6f, -0.4f, -0.05f, 0.9f, 0f),
			new BladeKey(0.38f, -2.05f, 0.5f, -0.2f, 1f, -0.25f, 0.02f, 0.9f, 0.4f),
			new BladeKey(0.47f, -1.5f, -0.1f, 0f, 0.3f, 0.2f, 0.15f, 1.45f, 1f),
			new BladeKey(0.58f, -1f, -0.65f, 0.1f, 0.25f, 0.45f, 0.2f, 1.55f, 0.8f),
			new BladeKey(1f, 0f, 0f, 0f, 0f, 0f, 0f, 0.35f, 0f)
		};

		public float SitReach => _thigh * 0.99f;

		public float LegShare
		{
			get
			{
				if (!(Height > 0f))
				{
					return WalkCycle.Tune.Leg;
				}
				return _hip / Height;
			}
		}

		public float Height => _height;

		public float SeatPad => 0.12f * _thigh;

		private float GroundPad => 0.22f * _thigh;

		public float KneeHeight => _shin;

		private float KneePad => 0.16f * _thigh;

		public Vec3 HoldAid(MeshInstance m, Aid aid, float phase, float stride, float run)
		{
			if (aid == Aid.None || UpperArmR < 0 || LowerArmR < 0)
			{
				return default(Vec3);
			}
			m.EnsurePose();
			float num = Height / 1.75f;
			float num2 = ((aid == Aid.Crutch) ? 0.86f : 0.86f) * num;
			WalkCycle.Leg(phase, aid == Aid.Cane, WalkCycle.Duty(run), out var angle, out var _);
			float num3 = _hip * (float)Math.Sin(angle * 0.5f * stride);
			Vec3 pivot = Model.Parts[UpperArmR].Pivot;
			Vec3 pivot2 = Model.Parts[LowerArmR].Pivot;
			Vec3 vec = pivot2 + new Vec3(0.004f, -0.012f, -0.3f);
			float num4 = Len(pivot2 - pivot);
			float num5 = Len(vec - pivot2);
			float num6 = Math.Max(0f, Model.Min.Z);
			float num7 = 0.1f * num + num3;
			float num8 = pivot.X + 0.08f * num;
			float num15;
			float num16;
			float num17;
			if (aid == Aid.Crutch)
			{
				float num9 = 0f - pivot.Y;
				float num10 = pivot.Z - 0.07f * num;
				float num11 = num9 - num7;
				float num12 = num10 - num6;
				float num13 = pivot.X - num8;
				float num14 = Math.Min(1f, num2 / Math.Max(0.0001f, (float)Math.Sqrt(num11 * num11 + num12 * num12 + num13 * num13)));
				num15 = num7 + num11 * num14;
				num16 = num6 + num12 * num14;
				num17 = num8 + num13 * num14;
			}
			else
			{
				num15 = 0.07f * num + 0.45f * num7;
				float num18 = num7 - num15;
				num16 = num6 + (float)Math.Sqrt(Math.Max(0.04f * num2 * num2, num2 * num2 - num18 * num18));
				num17 = num8 - 0.02f * num;
			}
			float num19 = num15 - (0f - pivot.Y);
			float num20 = pivot.Z - num16;
			float num21 = num17 - pivot.X;
			float num22 = Math.Max(val2: Math.Min((num4 + num5) * 0.995f, (float)Math.Sqrt(num19 * num19 + num20 * num20)), val1: Math.Abs(num4 - num5) * 1.05f + 0.0001f);
			float num23 = (float)Math.Atan2(num19, num20);
			float num24 = (float)Math.Acos(Clamp((num4 * num4 + num22 * num22 - num5 * num5) / (2f * num4 * num22), -1f, 1f));
			float num25 = (float)Math.Acos(Clamp((num4 * num4 + num5 * num5 - num22 * num22) / (2f * num4 * num5), -1f, 1f));
			float num26 = num23 - num24;
			float num27 = MathF.PI - num25;
			Rot(m, UpperArmR, Mat3.RotX(0f - num26) * Mat3.RotY((float)Math.Atan2(num21, num22)));
			Rot(m, LowerArmR, Mat3.RotX(0f - num27));
			return new Vec3(num8, 0f - num7, num6);
		}

		public void AidPlace(MeshInstance body, Aid aid, Vec3 tipLocal, out Vec3 position, out Mat3 basis)
		{
			Vec3 vec = HandWorld(body);
			Vec3 vec2 = body.Position + body.Basis.Mul(tipLocal);
			Vec3 vec3 = vec - vec2;
			float num = Len(vec3);
			if (num < 1E-05f)
			{
				position = vec;
				basis = body.Basis;
				return;
			}
			vec3 *= 1f / num;
			float num2 = Len(body.Basis.C2) * (Height / 1.75f);
			Vec3 b = body.Basis.C1 * -1f;
			Vec3 v = Vec3.Cross(vec3, b);
			if (Len(v) < 0.0001f)
			{
				v = body.Basis.C0;
			}
			v = v.Normalized();
			Vec3 vec4 = Vec3.Cross(v, vec3).Normalized();
			float num3 = ((aid == Aid.Crutch) ? 0.86f : 0.86f);
			position = vec;
			basis = new Mat3(v * num2, vec4 * num2, vec3 * (num / num3));
		}

		public void Hobble(MeshInstance m, float phase, float stride)
		{
			if (!(stride <= 0.01f) && LowerLegR >= 0 && m.PartRotation != null)
			{
				float num = Math.Min(1f, stride * 2f);
				m.PartRotation[LowerLegR] = Slerp(m.PartRotation[LowerLegR], 0.35f + 0.65f * (1f - num));
				float num2 = (float)Math.Max(0.0, Math.Sin((double)phase - Math.PI / 2.0));
				if (Hips >= 0)
				{
					m.PartOffset[Hips] = m.PartOffset[Hips] + new Vec3(0f, 0f, -0.025f * num2 * num * Height / 1.75f);
				}
				if (Torso >= 0)
				{
					Mul(m, Torso, Mat3.RotY(-0.07f * num2 * num));
				}
			}
		}

		private static Mat3 Slerp(Mat3 r, float keep)
		{
			float num = 1f - keep;
			Vec3 vec = (r.C0 * keep + new Vec3(1f, 0f, 0f) * num).Normalized();
			Vec3 vec2 = r.C1 * keep + new Vec3(0f, 1f, 0f) * num;
			vec2 = (vec2 - vec * Vec3.Dot(vec2, vec)).Normalized();
			return new Mat3(vec, vec2, Vec3.Cross(vec, vec2));
		}

		private static float Len(Vec3 v)
		{
			return (float)Math.Sqrt(Vec3.Dot(v, v));
		}

		private static float Clamp(float v, float lo, float hi)
		{
			if (!(v < lo))
			{
				if (!(v > hi))
				{
					return v;
				}
				return hi;
			}
			return lo;
		}

		private void Anchors()
		{
			if (_anchors)
			{
				return;
			}
			_anchors = true;
			float num = Height / 1.75f;
			float num2 = ((Hips >= 0) ? Model.Parts[Hips].Pivot.Z : (0.52f * Height));
			float num3 = -0.12f * num;
			if (Torso >= 0)
			{
				MeshPart meshPart = Model.Parts[Torso];
				float num4 = float.MaxValue;
				float num5 = meshPart.Pivot.Z + 0.25f * num;
				for (int i = 0; i + 2 < meshPart.Pos.Length; i += 3)
				{
					if (meshPart.Pos[i + 2] < num5 && Math.Abs(meshPart.Pos[i]) < 0.12f * num && meshPart.Pos[i + 1] < num4)
					{
						num4 = meshPart.Pos[i + 1];
					}
				}
				if (num4 < float.MaxValue)
				{
					num3 = num4;
				}
			}
			_hugAnchor = new Vec3(0f, num3 - 0.012f * num, num2 + 0.13f * num);
			if (UpperArmR >= 0)
			{
				Vec3 pivot = Model.Parts[UpperArmR].Pivot;
				_shoulderAnchor = new Vec3(pivot.X * 0.95f, pivot.Y, pivot.Z + 0.036f * num);
			}
			else
			{
				_shoulderAnchor = new Vec3(0.17f * num, 0f, 0.8f * Height);
			}
		}

		public void CarryHand(MeshInstance m, float phase, float stride)
		{
			if (UpperArmR >= 0 && LowerArmR >= 0)
			{
				m.EnsurePose();
				float num = 0.06f * stride * (float)Math.Sin(phase);
				Rot(m, UpperArmR, Mat3.RotX(-0.26f - num) * Mat3.RotY(0.13f));
				Rot(m, LowerArmR, Mat3.RotX(-1.22f + 0.5f * num));
			}
		}

		public void CarryLean(MeshInstance m, CarryStyle style, float lean)
		{
			m.EnsurePose();
			if (Torso >= 0 && lean != 0f)
			{
				Mul(m, Torso, Mat3.RotX(lean));
			}
			if (Head >= 0 && style == CarryStyle.Shoulder)
			{
				Mul(m, Head, Mat3.RotY(-0.2f));
			}
			else if (Head >= 0 && style == CarryStyle.Sack)
			{
				Mul(m, Head, Mat3.RotY(-0.1f));
			}
		}

		public bool HandFrame(MeshInstance body, float height, bool palm, out Vec3 position, out Mat3 basis)
		{
			position = body.Position;
			basis = Mat3.Identity;
			if (LowerArmR < 0)
			{
				return false;
			}
			position = HandWorld(body);
			Vec3 vec = TransformDir(body, LowerArmR, new Vec3(0f, -1f, 0f)).Normalized();
			Vec3 vec2 = TransformDir(body, LowerArmR, new Vec3(0f, -0.04f, -1f)).Normalized();
			Vec3 vec3 = vec2 - vec * Vec3.Dot(vec2, vec);
			vec3 = ((Vec3.Dot(vec3, vec3) > 1E-06f) ? vec3.Normalized() : new Vec3(0f, 0f, -1f));
			Vec3 vec4 = Vec3.Cross(vec3, vec);
			float num = Math.Max(0.3f, Math.Min(3f, height));
			if (palm)
			{
				position += vec * (0.026f * num);
			}
			basis = new Mat3(vec4 * num, vec3 * num, vec * num);
			return true;
		}

		public bool LoadPlace(MeshInstance body, CarryStyle style, float height, out Vec3 position, out Mat3 basis)
		{
			position = body.Position;
			basis = Mat3.Identity;
			if (Torso < 0)
			{
				return false;
			}
			Anchors();
			position = Transform(body, Torso, (style == CarryStyle.Hug) ? _hugAnchor : _shoulderAnchor);
			Vec3 vec = TransformDir(body, Torso, new Vec3(0f, -1f, 0f)).Normalized();
			Vec3 vec2 = TransformDir(body, Torso, new Vec3(0f, 0f, 1f));
			vec2 = (vec2 - vec * Vec3.Dot(vec2, vec)).Normalized();
			Vec3 vec3 = Vec3.Cross(vec, vec2);
			float num = Math.Max(0.3f, Math.Min(3f, height));
			basis = new Mat3(vec3 * num, vec * num, vec2 * num);
			return true;
		}

		public void LoadArms(MeshInstance body, Carried c, Vec3 position, Mat3 basis)
		{
			if (UpperArmR >= 0 && LowerArmR >= 0 && c != null)
			{
				body.EnsurePose();
				float scale = Len(body.Basis.C2);
				Vec3 vec = basis.C0.Normalized();
				Vec3 vec2 = basis.C1.Normalized();
				Vec3 vec3 = basis.C2.Normalized();
				Vec3 hand = Model.Parts[LowerArmR].Pivot + new Vec3(0.004f, -0.012f, -0.3f);
				Vec3 pole = ((c.Style == CarryStyle.Hug) ? (vec * 1f - vec3 * 0.7f - vec2 * 0.25f) : (vec * 0.35f - vec3 * 1f + vec2 * 0.55f));
				Archer.Reach(body, UpperArmR, LowerArmR, hand, position + basis.Mul(c.HandR), pole, scale);
				if (c.LeftHand && UpperArmL >= 0 && LowerArmL >= 0)
				{
					Vec3 hand2 = Model.Parts[LowerArmL].Pivot + new Vec3(-0.004f, -0.012f, -0.3f);
					Archer.Reach(body, UpperArmL, LowerArmL, hand2, position + basis.Mul(c.HandL), vec * -1f - vec3 * 0.7f - vec2 * 0.25f, scale);
				}
			}
		}

		public PersonRig(MeshModel m)
		{
			Model = m;
			Hips = m.PartIndex("hips");
			Torso = m.PartIndex("torso");
			Head = m.PartIndex("head");
			HairShort = m.PartIndex("hair_short");
			HairLong = m.PartIndex("hair_long");
			Beard = m.PartIndex("beard");
			UpperArmL = m.PartIndex("upper_arm_L");
			LowerArmL = m.PartIndex("lower_arm_L");
			UpperArmR = m.PartIndex("upper_arm_R");
			LowerArmR = m.PartIndex("lower_arm_R");
			UpperLegL = m.PartIndex("upper_leg_L");
			LowerLegL = m.PartIndex("lower_leg_L");
			UpperLegR = m.PartIndex("upper_leg_R");
			LowerLegR = m.PartIndex("lower_leg_R");
			SlotSkin = m.SlotIndex("skin");
			SlotHair = m.SlotIndex("hair");
			SlotEyes = m.SlotIndex("eyes");
			SlotShirt = m.SlotIndex("shirt");
			SlotShirtDark = m.SlotIndex("shirt_dark");
			SlotPants = m.SlotIndex("pants");
			SlotShoes = m.SlotIndex("shoes");
			SlotBelt = m.SlotIndex("belt");
			SlotMouth = m.SlotIndex("mouth");
			SlotTusk = m.SlotIndex("tusk");
			SlotAccent = m.SlotIndex("accent");
			Robe = m.Info("robe") == "1";
			AlwaysBearded = m.Info("beard") == "always";
			Hat = m.Info("hat") == "1";
			ChildBody = m.Info("child") == "1";
			_height = BodyHeight(m);
			Wardrobe.Parts parts = Wardrobe.PartsOf(m);
			if (parts != null && parts.Has)
			{
				PlainMan = Wardrobe.Hidden(parts, Wardrobe.Plain(female: false));
				PlainWoman = Wardrobe.Hidden(parts, Wardrobe.Plain(female: true));
			}
			for (int i = 0; i < m.Parts.Length && i < 64; i++)
			{
				if (m.Parts[i].Name != null && m.Parts[i].Name.StartsWith("fem_", StringComparison.Ordinal))
				{
					FemaleParts |= (ulong)(1L << i);
				}
			}
			_hip = ((UpperLegL >= 0) ? (m.Parts[UpperLegL].Pivot.Z - Math.Max(0f, m.Min.Z)) : (0.49f * Height));
			if (UpperLegL >= 0 && LowerLegL >= 0)
			{
				_thigh = m.Parts[UpperLegL].Pivot.Z - m.Parts[LowerLegL].Pivot.Z;
				_shin = m.Parts[LowerLegL].Pivot.Z - Math.Max(0f, m.Min.Z);
				if (_thigh <= 0f || _shin <= 0f)
				{
					_thigh = (_shin = 0f);
				}
			}
			if (LowerLegL >= 0)
			{
				_soleL = SoleOf(m, LowerLegL);
			}
			if (LowerLegR >= 0)
			{
				_soleR = SoleOf(m, LowerLegR);
			}
		}

		public static float FidgetSeconds(Fidget f)
		{
			switch (f)
			{
			default:
				return 6f;
			case Fidget.Stretch:
				return 2f;
			case Fidget.ScratchHead:
				return 1.6f;
			}
		}

		public void Idle(MeshInstance m, Fidget f, float amount, float t, float side, bool handsBusy)
		{
			if (f == Fidget.None || amount <= 0f)
			{
				return;
			}
			m.EnsurePose();
			float num = Ease(amount);
			float num2 = ((f == Fidget.WeightShift) ? 1f : 0.5f);
			int part = ((side > 0f) ? LowerLegL : LowerLegR);
			Mul(m, part, Mat3.RotX(0.22f * num2 * num));
			if (Hips >= 0)
			{
				m.PartOffset[Hips] = m.PartOffset[Hips] + new Vec3(-0.025f * side * num2 * num, 0f, -0.012f * num2 * num);
				Mul(m, Hips, Mat3.RotY(0.06f * side * num2 * num));
			}
			Mul(m, Torso, Mat3.RotY(-0.05f * side * num2 * num));
			if (!handsBusy || f == Fidget.Stretch)
			{
				switch (f)
				{
				case Fidget.ArmsCrossed:
					Rot(m, UpperArmL, Mat3.RotY(-0.22f * num) * Mat3.RotX(-0.45f * num));
					Rot(m, UpperArmR, Mat3.RotY(0.22f * num) * Mat3.RotX(-0.5f * num));
					Rot(m, LowerArmL, Mat3.RotZ(1.05f * num) * Mat3.RotX(-1.65f * num));
					Rot(m, LowerArmR, Mat3.RotZ(-1.05f * num) * Mat3.RotX(-1.55f * num));
					break;
				case Fidget.HandsOnHips:
					Rot(m, UpperArmL, Mat3.RotY(0.55f * num) * Mat3.RotX(0.2f * num));
					Rot(m, UpperArmR, Mat3.RotY(-0.55f * num) * Mat3.RotX(0.2f * num));
					Rot(m, LowerArmL, Mat3.RotZ(1.25f * num) * Mat3.RotX(-1.35f * num));
					Rot(m, LowerArmR, Mat3.RotZ(-1.25f * num) * Mat3.RotX(-1.35f * num));
					break;
				case Fidget.HandsBehind:
					Rot(m, UpperArmL, Mat3.RotY(-0.1f * num) * Mat3.RotX(0.5f * num));
					Rot(m, UpperArmR, Mat3.RotY(0.1f * num) * Mat3.RotX(0.5f * num));
					Rot(m, LowerArmL, Mat3.RotZ(1.45f * num) * Mat3.RotX(-1.45f * num));
					Rot(m, LowerArmR, Mat3.RotZ(-1.45f * num) * Mat3.RotX(-1.45f * num));
					Mul(m, Torso, Mat3.RotX(-0.04f * num));
					break;
				case Fidget.ScratchHead:
				{
					int part2 = ((side > 0f) ? UpperArmR : UpperArmL);
					int part3 = ((side > 0f) ? LowerArmR : LowerArmL);
					float num3 = 0.12f * (float)Math.Sin(t * 18f);
					Rot(m, part2, Mat3.RotY(0.45f * side * num) * Mat3.RotX(-2.5f * num));
					Rot(m, part3, Mat3.RotX((-1.9f - num3) * num));
					Mul(m, Head, Mat3.RotY(0.12f * side * num) * Mat3.RotX(0.08f * num));
					break;
				}
				case Fidget.Stretch:
					Rot(m, UpperArmL, Mat3.RotY(-0.25f * num) * Mat3.RotX(-2.9f * num));
					Rot(m, UpperArmR, Mat3.RotY(0.25f * num) * Mat3.RotX(-2.9f * num));
					Rot(m, LowerArmL, Mat3.RotX(-0.3f * num));
					Rot(m, LowerArmR, Mat3.RotX(-0.3f * num));
					Mul(m, Torso, Mat3.RotX(-0.14f * num));
					Mul(m, Head, Mat3.RotX(-0.2f * num));
					break;
				}
			}
		}

		public void Sit(MeshInstance m, float amount)
		{
			if (!(amount <= 0f))
			{
				m.EnsurePose();
				float num = Ease(amount);
				float num2 = 1.45f * num;
				Rot(m, UpperLegL, Mat3.RotX(0f - num2));
				Rot(m, UpperLegR, Mat3.RotX(0f - num2));
				Rot(m, LowerLegL, Mat3.RotX(num2));
				Rot(m, LowerLegR, Mat3.RotX(num2));
				if (Hips >= 0)
				{
					m.PartOffset[Hips] = new Vec3(0f, 0f, (0f - _thigh) * (1f - (float)Math.Cos(num2)));
				}
				Rot(m, Torso, Mat3.RotX(-0.03f * num));
				Rot(m, UpperArmL, Mat3.RotX(-0.55f * num) * Mat3.RotY(-0.1f * num));
				Rot(m, UpperArmR, Mat3.RotX(-0.55f * num) * Mat3.RotY(0.1f * num));
				Rot(m, LowerArmL, Mat3.RotX(-0.75f * num));
				Rot(m, LowerArmR, Mat3.RotX(-0.75f * num));
			}
		}

		public static void LayDown(MeshInstance m, float amount)
		{
			if (!(amount <= 0f))
			{
				float num = Ease(amount);
				m.Basis *= Mat3.RotX(-MathF.PI / 2f * num);
			}
		}

		public void Plant(MeshInstance m, WalkCycle.Walker w)
		{
			if (!RigTune.Plant)
			{
				PlantOld(m, w);
			}
			else if (RigTune.PlantCheck.On)
			{
				PlantChecked(m, w);
			}
			else
			{
				PlantFast(m, w);
			}
		}

		internal void PlantOld(MeshInstance m, WalkCycle.Walker w)
		{
			if (w != null && UpperLegL >= 0 && UpperLegR >= 0 && LowerLegL >= 0 && LowerLegR >= 0 && m.PartRotation != null)
			{
				float duty = WalkCycle.Duty(w.Run);
				float angle;
				float lift;
				bool down = WalkCycle.Leg(w.Phase, left: true, duty, out angle, out lift);
				bool down2 = WalkCycle.Leg(w.Phase, left: false, duty, out lift, out angle);
				PlantLegOld(m, UpperLegL, LowerLegL, _soleL, down, ref w.FootL);
				PlantLegOld(m, UpperLegR, LowerLegR, _soleR, down2, ref w.FootR);
			}
		}

		private void PlantLegOld(MeshInstance m, int upper, int lower, Vec3 sole, bool down, ref WalkCycle.Foot f)
		{
			Vec3 vec = Transform(m, lower, sole);
			if (!down)
			{
				f.Down = false;
				return;
			}
			if (!f.Down)
			{
				f.Down = true;
				f.X = vec.X;
				f.Y = vec.Y;
				return;
			}
			float num = f.X - vec.X;
			float num2 = f.Y - vec.Y;
			if (num * num + num2 * num2 < 1E-10f)
			{
				return;
			}
			float num3 = (_thigh + _shin) * m.Basis.C2.Z;
			if (num * num + num2 * num2 > 0.36f * num3 * num3)
			{
				f.X = vec.X;
				f.Y = vec.Y;
				return;
			}
			Mat3 b = m.PartRotation[upper];
			m.PartRotation[upper] = Mat3.RotX(0.02f) * b;
			Vec3 vec2 = Transform(m, lower, sole);
			m.PartRotation[upper] = Mat3.RotY(0.02f) * b;
			Vec3 vec3 = Transform(m, lower, sole);
			float num4 = (vec2.X - vec.X) / 0.02f;
			float num5 = (vec2.Y - vec.Y) / 0.02f;
			float num6 = (vec3.X - vec.X) / 0.02f;
			float num7 = (vec3.Y - vec.Y) / 0.02f;
			float num8 = num4 * num7 - num5 * num6;
			if (Math.Abs(num8) < 1E-08f)
			{
				m.PartRotation[upper] = b;
				return;
			}
			float num9 = (num * num7 - num2 * num6) / num8;
			float num10 = (num4 * num2 - num5 * num) / num8;
			bool flag = Math.Abs(num9) > PlantReach || Math.Abs(num10) > PlantReach;
			if (PlantStats != null)
			{
				PlantStats[0] += Math.Min(PlantReach, Math.Abs(num9));
				PlantStats[1] += Math.Min(PlantReach, Math.Abs(num10));
				PlantStats[2] += 1f;
				if (flag)
				{
					PlantStats[3] += 1f;
					if (Math.Abs(num9) > PlantReach)
					{
						PlantStats[4] += 1f;
					}
					PlantStats[5] += (float)Math.Sqrt(num * num + num2 * num2) / Math.Max(0.0001f, num3);
				}
			}
			num9 = Math.Max(0f - PlantReach, Math.Min(PlantReach, num9));
			num10 = Math.Max(0f - PlantReach, Math.Min(PlantReach, num10));
			m.PartRotation[upper] = Mat3.RotY(num10) * Mat3.RotX(num9) * b;
			if (flag)
			{
				Vec3 vec4 = Transform(m, lower, sole);
				f.X = vec4.X;
				f.Y = vec4.Y;
			}
		}

		public Vec3 SoleWorld(MeshInstance m, bool left)
		{
			if ((left ? LowerLegL : LowerLegR) >= 0)
			{
				return Transform(m, left ? LowerLegL : LowerLegR, left ? _soleL : _soleR);
			}
			return m.Position;
		}

		private static Vec3 SoleOf(MeshModel m, int part)
		{
			MeshPart meshPart = m.Parts[part];
			if (meshPart.Pos == null || meshPart.VertexCount == 0)
			{
				return meshPart.Pivot;
			}
			float num = float.MaxValue;
			for (int i = 0; i < meshPart.VertexCount; i++)
			{
				num = Math.Min(num, meshPart.Pos[i * 3 + 2]);
			}
			float num2 = 0f;
			float num3 = 0f;
			int num4 = 0;
			for (int j = 0; j < meshPart.VertexCount; j++)
			{
				if (meshPart.Pos[j * 3 + 2] < num + 0.02f)
				{
					num2 += meshPart.Pos[j * 3];
					num3 += meshPart.Pos[j * 3 + 1];
					num4++;
				}
			}
			return new Vec3(num2 / (float)num4, num3 / (float)num4, num);
		}

		private float Reach(float thigh, float knee)
		{
			if (_thigh <= 0f)
			{
				return 0f;
			}
			float num = _thigh * (float)Math.Cos(thigh) + _shin * (float)Math.Cos(thigh - knee);
			return Math.Max(0f, _thigh + _shin - num);
		}

		public static float BodyHeight(MeshModel m)
		{
			float num = m.InfoFloat("body_height", 0f);
			if (num > 0f)
			{
				return num;
			}
			float num2 = m.Max.Z;
			if (m.PartIndex("face_0") >= 0 || m.PartIndex("hair_0") >= 0)
			{
				num2 = float.MinValue;
				MeshPart[] parts = m.Parts;
				foreach (MeshPart meshPart in parts)
				{
					string text = meshPart.Name ?? "";
					if (text != "hips" && text != "torso" && text != "head" && !text.StartsWith("upper_", StringComparison.Ordinal) && !text.StartsWith("lower_", StringComparison.Ordinal))
					{
						continue;
					}
					for (int j = 2; j < meshPart.Pos.Length; j += 3)
					{
						if (meshPart.Pos[j] > num2)
						{
							num2 = meshPart.Pos[j];
						}
					}
				}
				if (num2 == float.MinValue)
				{
					num2 = m.Max.Z;
				}
			}
			return num2 - Math.Max(0f, m.Min.Z);
		}

		public static void Place(MeshInstance m, float x, float y, float z, float heading, float scale)
		{
			m.Place(x, y, z, heading, 0f - scale, scale, scale);
		}

		public void Dress(MeshInstance m, Humanoid.Palette p, bool beardAllowed, bool female = false)
		{
			if (m.Palette == null || m.Palette.Length != Model.SlotNames.Length)
			{
				m.Palette = new uint[Model.SlotNames.Length];
			}
			Set(m, SlotSkin, p.Skin);
			Set(m, SlotHair, p.Hair);
			Set(m, SlotEyes, TerrainCache.Scale(p.Eyes, 0.8f));
			Set(m, SlotShirt, p.Shirt);
			Set(m, SlotShirtDark, TerrainCache.Scale(p.Shirt, 0.68f));
			Set(m, SlotPants, p.Pants);
			Set(m, SlotShoes, p.Shoes);
			Set(m, SlotBelt, TerrainCache.Lerp(p.Shoes, TerrainCache.Rgb(96, 64, 38), 0.5f));
			Set(m, SlotMouth, TerrainCache.Lerp(TerrainCache.Scale(p.Skin, 0.78f), TerrainCache.Rgb(170, 70, 70), 0.35f));
			if (SlotAccent >= 0)
			{
				m.Palette[SlotAccent] = ((p.Accent != 0) ? (p.Accent | 0xFF000000u) : 0u);
			}
			m.Hidden = 0uL;
			if (HairLong >= 0 && HairLong < 64 && !p.LongHair)
			{
				m.Hidden |= (ulong)(1L << HairLong);
			}
			if (HairShort >= 0 && HairShort < 64 && p.LongHair)
			{
				m.Hidden |= (ulong)(1L << HairShort);
			}
			if (Beard >= 0 && Beard < 64 && (female || (!AlwaysBearded && !(p.Beard && beardAllowed))))
			{
				m.Hidden |= (ulong)(1L << Beard);
			}
			if (!female)
			{
				m.Hidden |= FemaleParts;
			}
			m.Hidden |= (female ? PlainWoman : PlainMan);
		}

		private static void Set(MeshInstance m, int slot, uint color)
		{
			if (slot >= 0)
			{
				m.Palette[slot] = color | 0xFF000000u;
			}
		}

		public void Pose(MeshInstance m, float stride, float phase, float swing, float breath, float headTurn, float headNod = 0f, float run = 0f, WalkCycle.Walker walker = null)
		{
			if (!RigTune.Lean)
			{
				PoseOld(m, stride, phase, swing, breath, headTurn, headNod, run, walker);
			}
			else if (RigTune.LeanCheck.On)
			{
				PoseChecked(m, stride, phase, swing, breath, headTurn, headNod, run, walker);
			}
			else
			{
				PoseLean(m, stride, phase, swing, breath, headTurn, headNod, run, walker);
			}
		}

		internal void PoseOld(MeshInstance m, float stride, float phase, float swing, float breath, float headTurn, float headNod, float run, WalkCycle.Walker walker)
		{
			m.EnsurePose();
			m.ResetPose();
			float num = (float)Math.Sin(phase);
			Math.Cos(phase);
			run = ((run < 0f) ? 0f : ((run > 1f) ? 1f : run));
			float num2 = 0.5f * stride;
			float num3 = (0.42f + 0.16f * run) * stride;
			float num4 = 0.9f + 0.55f * run;
			if (Robe)
			{
				if (walker == null)
				{
					num2 *= 0.38f;
				}
				num4 = 0.35f;
			}
			float duty = WalkCycle.Duty(run);
			float angle;
			float lift;
			bool flag = WalkCycle.Leg(phase, left: true, duty, out angle, out lift);
			float angle2;
			float lift2;
			bool flag2 = WalkCycle.Leg(phase, left: false, duty, out angle2, out lift2);
			float num5 = (0.06f + 0.25f * run) * stride;
			float num6 = (flag ? (num5 * (1f - Math.Abs(angle))) : 0f);
			float num7 = (flag2 ? (num5 * (1f - Math.Abs(angle2))) : 0f);
			float num8 = Math.Max(stride, walker?.Lift ?? 0f);
			float num9 = num4 * num8 * lift + num6 + 0.05f;
			float num10 = num4 * num8 * lift2 + num7 + 0.05f;
			float num11 = ((_thigh + _shin > 0f) ? (_shin / (_thigh + _shin)) : 0.5f);
			float a = ((walker != null) ? (TwistSign * walker.TwistL) : 0f);
			float a2 = ((walker != null) ? (TwistSign * walker.TwistR) : 0f);
			Rot(m, UpperLegL, Mat3.RotZ(a) * Mat3.RotX((0f - num2) * angle - num6 * num11));
			Rot(m, UpperLegR, Mat3.RotZ(a2) * Mat3.RotX((0f - num2) * angle2 - num7 * num11));
			Rot(m, LowerLegL, Mat3.RotX(num9));
			Rot(m, LowerLegR, Mat3.RotX(num10));
			float num12 = 0.04f * (float)Math.Sin(breath * 1.3f);
			Rot(m, UpperArmL, Mat3.RotX(num3 * num) * Mat3.RotY(-0.07f - num12 * 0.3f));
			Rot(m, UpperArmR, Mat3.RotX((0f - num3) * num) * Mat3.RotY(0.07f + num12 * 0.3f));
			Rot(m, LowerArmL, Mat3.RotX(-0.18f - 1.05f * run - 0.25f * stride * Math.Max(0f, 0f - num)));
			Rot(m, LowerArmR, Mat3.RotX(-0.18f - 1.05f * run - 0.25f * stride * Math.Max(0f, num)));
			float num13 = 0f;
			if (flag || flag2)
			{
				float val = (flag ? Reach(num2 * angle + num6 * num11, num9) : float.MaxValue);
				float val2 = (flag2 ? Reach(num2 * angle2 + num7 * num11, num10) : float.MaxValue);
				num13 = WalkCycle.Tune.HipFollow * Math.Min(val, val2);
			}
			if (Hips >= 0)
			{
				m.PartOffset[Hips] = new Vec3(0f, 0f, 0f - num13 + 0.004f * (float)Math.Sin(breath * 2.1f));
			}
			Rot(m, Torso, Mat3.RotZ((0.1f + 0.06f * run) * stride * num) * Mat3.RotX(0.04f * stride + num12 * 0.2f));
			if (swing > 0f)
			{
				float num14 = ((swing < 0.4f) ? (swing / 0.4f) : (1f - (swing - 0.4f) / 0.6f * 1.4f));
				Rot(m, UpperArmR, Mat3.RotX(-2.4f * Math.Max(-0.3f, num14)) * Mat3.RotY(0.25f));
				Rot(m, LowerArmR, Mat3.RotX(-0.6f * Math.Max(0f, num14)));
				Rot(m, Torso, Mat3.RotZ(-0.25f * (1f - Math.Abs(num14))) * Mat3.RotX(0.1f));
			}
			Rot(m, Head, Mat3.RotZ(headTurn) * Mat3.RotX(headNod));
		}

		private static void Rot(MeshInstance m, int part, Mat3 r)
		{
			if (part >= 0)
			{
				m.PartRotation[part] = r;
			}
		}

		private static void Mul(MeshInstance m, int part, Mat3 r)
		{
			if (part >= 0)
			{
				m.PartRotation[part] = m.PartRotation[part] * r;
			}
		}

		private static float Ease(float x)
		{
			x = ((x < 0f) ? 0f : ((x > 1f) ? 1f : x));
			return x * x * (3f - 2f * x);
		}

		public void Fight(MeshInstance m, WeaponKind kind, float attack, float stance, float flinch, float lean, float side)
		{
			if (!RigTune.Lean)
			{
				FightOld(m, kind, attack, stance, flinch, lean, side);
			}
			else if (RigTune.LeanCheck.On)
			{
				FightChecked(m, kind, attack, stance, flinch, lean, side);
			}
			else
			{
				FightLean(m, kind, attack, stance, flinch, lean, side);
			}
		}

		internal void FightOld(MeshInstance m, WeaponKind kind, float attack, float stance, float flinch, float lean, float side)
		{
			m.EnsurePose();
			if (stance > 0f)
			{
				Mul(m, UpperLegL, Mat3.RotX(-0.28f * stance));
				Mul(m, UpperLegR, Mat3.RotX(-0.12f * stance));
				Mul(m, LowerLegL, Mat3.RotX(0.42f * stance));
				Mul(m, LowerLegR, Mat3.RotX(0.3f * stance));
				if (Hips >= 0)
				{
					m.PartOffset[Hips] = m.PartOffset[Hips] + new Vec3(0f, 0f, -0.05f * stance);
				}
				Mul(m, Torso, Mat3.RotX(0.12f * stance));
				switch (kind)
				{
				case WeaponKind.Bow:
					Rot(m, UpperArmL, Mat3.RotX(-1.45f * stance) * Mat3.RotY(-0.1f));
					Rot(m, LowerArmL, Mat3.RotX(-0.1f * stance));
					Rot(m, UpperArmR, Mat3.RotX(-1.35f * stance) * Mat3.RotY(-0.35f * stance));
					Rot(m, LowerArmR, Mat3.RotX(-1.5f * stance));
					break;
				case WeaponKind.Shotgun:
					Rot(m, UpperArmR, Mat3.RotX(-0.42f * stance) * Mat3.RotY(0.32f * stance));
					Rot(m, LowerArmR, Mat3.RotX(-1.3f * stance) * Mat3.RotY(-0.25f * stance));
					Rot(m, UpperArmL, Mat3.RotX(-1.15f * stance) * Mat3.RotY(-0.12f * stance));
					Rot(m, LowerArmL, Mat3.RotX(-0.6f * stance));
					Mul(m, Torso, Mat3.RotZ(-0.12f * stance));
					break;
				case WeaponKind.Spear:
				case WeaponKind.Staff:
					Rot(m, UpperArmR, Mat3.RotX(-0.6f * stance) * Mat3.RotY(0.15f));
					Rot(m, LowerArmR, Mat3.RotX(-1f * stance));
					Rot(m, UpperArmL, Mat3.RotX(-0.8f * stance) * Mat3.RotY(0.25f * stance));
					Rot(m, LowerArmL, Mat3.RotX(-0.9f * stance));
					break;
				default:
					Rot(m, UpperArmR, Mat3.RotX(-0.75f * stance) * Mat3.RotY(0.22f));
					Rot(m, LowerArmR, Mat3.RotX(-1.05f * stance));
					Rot(m, UpperArmL, Mat3.RotX(-0.55f * stance) * Mat3.RotY(-0.2f));
					Rot(m, LowerArmL, Mat3.RotX(-1.25f * stance));
					break;
				}
			}
			if (attack > 0f && attack < 1f)
			{
				switch (kind)
				{
				case WeaponKind.Sword:
				case WeaponKind.Stick:
				case WeaponKind.Dagger:
					BladeStroke(m, attack, side, stance);
					break;
				case WeaponKind.Axe:
				case WeaponKind.Hammer:
				{
					float num3 = ((attack < 0.4f) ? Ease(attack / 0.4f) : ((attack < 0.58f) ? (1f - 1.25f * Ease((attack - 0.4f) / 0.18f)) : (-0.25f * (1f - Ease((attack - 0.58f) / 0.42f)))));
					Rot(m, UpperArmR, Mat3.RotX(-0.75f - 2.15f * num3) * Mat3.RotY(0.2f));
					Rot(m, LowerArmR, Mat3.RotX(-0.6f * Math.Max(0f, num3) - 0.15f));
					Rot(m, UpperArmL, Mat3.RotX(-0.6f - 1.8f * Math.Max(0f, num3)) * Mat3.RotY(-0.15f));
					Rot(m, Torso, Mat3.RotX(-0.15f * Math.Max(0f, num3) + 0.35f * Math.Max(0f, 0f - num3)));
					break;
				}
				case WeaponKind.Spear:
				{
					float num6 = ((attack < 0.3f) ? Ease(attack / 0.3f) : 0f);
					float num7 = ((attack < 0.3f) ? 0f : ((attack < 0.5f) ? Ease((attack - 0.3f) / 0.2f) : (1f - Ease((attack - 0.5f) / 0.5f))));
					Rot(m, UpperArmR, Mat3.RotX(-1f + 0.4f * num6 - 0.55f * num7) * Mat3.RotY(0.15f));
					Rot(m, LowerArmR, Mat3.RotX(-1.2f * (1f - num7) - 0.1f));
					Rot(m, UpperArmL, Mat3.RotX(-1.1f + 0.3f * num6 - 0.4f * num7) * Mat3.RotY(0.3f));
					Rot(m, LowerArmL, Mat3.RotX(-0.8f * (1f - num7)));
					Rot(m, Torso, Mat3.RotX(0.25f * num7 - 0.1f * num6));
					if (Hips >= 0)
					{
						m.PartOffset[Hips] = m.PartOffset[Hips] + new Vec3(0f, -0.12f * num7 + 0.04f * num6, -0.03f * num7);
					}
					break;
				}
				case WeaponKind.Bow:
				{
					float num2 = ((attack < 0.15f) ? Ease(attack / 0.15f) : (1f - Ease((attack - 0.15f) / 0.5f)));
					Rot(m, UpperArmL, Mat3.RotX(-1.5f) * Mat3.RotY(-0.1f));
					Rot(m, LowerArmL, Mat3.RotX(-0.05f));
					Rot(m, UpperArmR, Mat3.RotX(-1.35f + 0.3f * num2) * Mat3.RotY(-0.35f - 0.5f * num2));
					Rot(m, LowerArmR, Mat3.RotX(-1.6f + 0.4f * num2));
					break;
				}
				case WeaponKind.Shotgun:
				{
					float num4 = ShotgunHold.Kick(attack);
					float num5 = ShotgunHold.Pump(attack);
					Mul(m, Torso, Mat3.RotX(-0.16f * num4));
					Mul(m, Head, Mat3.RotX(-0.1f * num4));
					Mul(m, UpperArmR, Mat3.RotX(-0.22f * num4));
					Mul(m, UpperArmL, Mat3.RotX(-0.22f * num4 + 0.28f * num5));
					Mul(m, LowerArmL, Mat3.RotX(-0.35f * num5));
					break;
				}
				case WeaponKind.Staff:
				{
					float num8 = ((attack < 0.35f) ? Ease(attack / 0.35f) : (1f - Ease((attack - 0.35f) / 0.65f)));
					Rot(m, UpperArmR, Mat3.RotX(-0.5f - 1.7f * num8) * Mat3.RotY(0.15f));
					Rot(m, LowerArmR, Mat3.RotX(-0.6f * (1f - num8)));
					Rot(m, Torso, Mat3.RotX(-0.12f * num8));
					break;
				}
				default:
				{
					float num = ((attack < 0.25f) ? Ease(attack / 0.25f) : (1f - Ease((attack - 0.25f) / 0.75f)));
					int part = ((side > 0f) ? UpperArmR : UpperArmL);
					int part2 = ((side > 0f) ? LowerArmR : LowerArmL);
					Rot(m, part, Mat3.RotX(-0.7f - 0.85f * num) * Mat3.RotY(side * -0.15f * num));
					Rot(m, part2, Mat3.RotX(-1.2f * (1f - num) - 0.05f));
					Rot(m, Torso, Mat3.RotZ((0f - side) * 0.3f * num) * Mat3.RotX(0.1f * num));
					break;
				}
				}
			}
			if (flinch > 0f)
			{
				Mul(m, Torso, Mat3.RotX(-0.35f * flinch));
				Mul(m, Head, Mat3.RotX(-0.3f * flinch));
				Mul(m, UpperArmL, Mat3.RotY(-0.35f * flinch));
				Mul(m, UpperArmR, Mat3.RotY(0.35f * flinch));
			}
			if (lean > 0f)
			{
				Mul(m, Torso, Mat3.RotX(0.22f * lean));
			}
		}

		public void Shamble(MeshInstance m, float k, float phase, float seed, float stride = 1f, float time = 0f)
		{
			if (k <= 0f)
			{
				return;
			}
			m.EnsurePose();
			float num = (float)Math.Sin(phase);
			float num2 = (float)Math.Cos(phase);
			float num3 = 0.5f * stride;
			float num4 = 0.9f;
			float num5 = 1f;
			if (Robe)
			{
				num3 *= 0.38f;
				num4 = 0.35f;
				num5 = 0.4f;
			}
			float num6 = (0f - num3) * num;
			float num7 = num3 * num;
			float num8 = num4 * stride * Math.Max(0f, 0f - num2) + 0.05f;
			float num9 = num4 * stride * Math.Max(0f, num2) + 0.05f;
			float num10 = phase + 0.5f * (float)Math.Sin(phase);
			float num11 = (float)Math.Sin(num10);
			float num12 = (float)Math.Cos(num10);
			bool flag = seed < 0.5f;
			float num13 = (flag ? (-1f) : 1f);
			float num14 = -0.62f * stride * num11 * num5;
			float num15 = (0.85f * stride * Math.Max(0f, num12) + 0.08f) * num5;
			float num16 = (0.22f * num11 + 0.08f) * stride * num5;
			float num17 = 0.12f + 0.06f * stride * Math.Max(0f, 0f - num12);
			float num18 = (flag ? num16 : num14);
			float num19 = (flag ? num17 : num15);
			float num20 = (flag ? num14 : num16);
			float num21 = (flag ? num15 : num17);
			Mul(m, UpperLegL, Mat3.RotX((num18 - num6) * k));
			Mul(m, UpperLegR, Mat3.RotX((num20 - num7) * k));
			Mul(m, LowerLegL, Mat3.RotX((num19 - num8) * k));
			Mul(m, LowerLegR, Mat3.RotX((num21 - num9) * k));
			Mul(m, flag ? UpperLegL : UpperLegR, Mat3.RotZ(num13 * 0.28f * k));
			float num22 = Math.Max(0f, num12);
			if (Hips >= 0)
			{
				m.PartOffset[Hips] = m.PartOffset[Hips] + new Vec3(0f, 0f, (-0.05f * stride * num22 - 0.02f * stride) * k);
			}
			float num23 = (float)Math.Sin(time * 0.9f + seed * 11f);
			float num24 = num13 * (0.05f + 0.12f * stride * num22) + 0.04f * (1f - stride) * num23;
			Mul(m, Torso, Mat3.RotX(0.3f * k) * Mat3.RotY(num24 * k) * Mat3.RotZ(0.14f * stride * num11 * k));
			float num25 = ((seed * 7f % 1f > 0.5f) ? 0.38f : (-0.38f));
			float num26 = 0.16f + 0.13f * stride * (float)Math.Sin(2f * num10 - 0.9f) + 0.05f * (1f - stride) * (float)Math.Sin(time * 0.6f + seed * 5f);
			Mul(m, Head, Mat3.RotY(num25 * k) * Mat3.RotX(num26 * k) * Mat3.RotZ(0.12f * (float)Math.Sin(num10 * 0.5f + seed * 9f) * k));
			float num27 = 1.05f + 0.3f * stride + 0.08f * (float)Math.Sin(time * 1.3f + seed * 6f);
			bool flag2 = seed * 13f % 1f > 0.6f;
			bool flag3 = seed * 29f % 1f > 0.5f;
			for (int i = 0; i < 2; i++)
			{
				bool flag4 = i == 0;
				int part = (flag4 ? UpperArmL : UpperArmR);
				int part2 = (flag4 ? LowerArmL : LowerArmR);
				float num28 = (flag4 ? (-1f) : 1f);
				if (flag2 && flag4 == flag3)
				{
					float num29 = 0.32f * stride * (float)Math.Sin(num10 + 0.8f) + 0.05f + 0.05f * (1f - stride) * num23;
					Rot(m, part, Mat3.RotX(num29 * k) * Mat3.RotY(num28 * 0.12f * k));
					Rot(m, part2, Mat3.RotX(-0.04f * k));
				}
				else
				{
					float num30 = num27 - (flag4 ? 0f : 0.12f) + 0.12f * stride * num11 * (flag4 ? 1f : (-1f));
					Rot(m, part, Mat3.RotX((0f - num30) * k) * Mat3.RotY((0f - num28) * 0.1f * k));
					Rot(m, part2, Mat3.RotX((0f - (flag4 ? 0.22f : 0.14f)) * k));
				}
			}
		}

		public void Limp(MeshInstance m, float k, float seed)
		{
			m.EnsurePose();
			float num = 0.6f + 0.3f * (float)Math.Sin(seed * 3.1f);
			Rot(m, UpperArmL, Mat3.RotX(-1.6f * k * num) * Mat3.RotY(-1.1f * k));
			Rot(m, UpperArmR, Mat3.RotX(-1.4f * k) * Mat3.RotY(1.2f * k * num));
			Rot(m, LowerArmL, Mat3.RotX(-0.4f * k));
			Rot(m, LowerArmR, Mat3.RotX(-0.5f * k));
			Rot(m, UpperLegL, Mat3.RotX(-0.35f * k * num));
			Rot(m, UpperLegR, Mat3.RotX(0.15f * k));
			Rot(m, LowerLegL, Mat3.RotX(0.6f * k));
			Rot(m, LowerLegR, Mat3.RotX(0.3f * k));
			Rot(m, Head, Mat3.RotZ(0.5f * k * ((seed > 0.5f) ? 1f : (-1f))) * Mat3.RotX(-0.3f * k));
		}

		public static Vec3 TransformDir(MeshInstance m, int part, Vec3 local)
		{
			if (RigTune.Transform)
			{
				if (!RigTune.TransformCheck.On)
				{
					return TransformDirFast(m, part, local);
				}
				return TransformDirChecked(m, part, local);
			}
			return TransformDirOld(m, part, local);
		}

		internal static Vec3 TransformDirOld(MeshInstance m, int part, Vec3 local)
		{
			MeshModel model = m.Model;
			Vec3 v = local;
			for (int num = part; num >= 0; num = model.Parts[num].Parent)
			{
				if (m.PartRotation != null)
				{
					v = m.PartRotation[num].Mul(v);
				}
			}
			return m.Basis.Mul(v);
		}

		public bool WeaponPlace(MeshInstance body, WeaponKind kind, float attack, float height, out Vec3 position, out Mat3 basis, float rest = 0f, float blowSide = 1f)
		{
			bool flag = kind == WeaponKind.Bow;
			int num = (flag ? LowerArmL : LowerArmR);
			position = body.Position;
			basis = Mat3.Identity;
			if (num < 0)
			{
				return false;
			}
			position = (flag ? LeftHandWorld(body) : HandWorld(body));
			Vec3 vec = TransformDir(body, num, new Vec3(0f, -1f, 0f)).Normalized();
			Vec3 vec2 = TransformDir(body, num, new Vec3(0f, -0.04f, -1f)).Normalized();
			float num2 = ((kind == WeaponKind.Spear) ? 1.57f : (flag ? 0f : ((kind == WeaponKind.Staff) ? (-0.15f) : 0.35f)));
			if (rest > 0f && attack <= 0f)
			{
				float num3 = ((kind == WeaponKind.Spear) ? num2 : ((flag || kind == WeaponKind.Staff) ? (-1.4f) : ((kind == WeaponKind.Axe || kind == WeaponKind.Hammer) ? 1.15f : 0.95f)));
				num2 += (num3 - num2) * Math.Min(1f, rest);
			}
			if ((kind == WeaponKind.Sword || kind == WeaponKind.Dagger || kind == WeaponKind.Stick) && attack > 0f && attack < 1f)
			{
				num2 = BladeWrist(attack, blowSide);
			}
			else if (attack > 0f && kind != WeaponKind.Spear && !flag && kind != WeaponKind.Staff)
			{
				float num4 = ((attack > 0.35f && attack < 0.75f) ? ((float)Math.Sin((double)((attack - 0.35f) / 0.4f) * Math.PI)) : 0f);
				num2 += 0.8f * num4;
			}
			Vec3 vec3 = (vec * (float)Math.Cos(num2) + vec2 * (float)Math.Sin(num2)).Normalized();
			Vec3 vec4 = vec2 - vec3 * Vec3.Dot(vec2, vec3);
			bool flag2 = !flag && kind != WeaponKind.Spear && kind != WeaponKind.Staff;
			vec4 = ((Vec3.Dot(vec4, vec4) > 1E-05f) ? (vec4.Normalized() * (flag2 ? 1f : (-1f))) : vec);
			Vec3 vec5 = Vec3.Cross(vec4, vec3);
			float num5 = Math.Max(0.3f, Math.Min(3f, height));
			basis = new Mat3(vec5 * num5, vec4 * num5, vec3 * num5);
			return true;
		}

		public void Expecting(MeshInstance m, float size, float phase, float stride)
		{
			if (!(size <= 0f))
			{
				m.EnsurePose();
				if (Torso >= 0)
				{
					Mul(m, Torso, Mat3.RotX(0.13f * size));
				}
				if (Head >= 0)
				{
					Mul(m, Head, Mat3.RotX(-0.07f * size));
				}
				if (Hips >= 0)
				{
					Mul(m, Hips, Mat3.RotY(0.07f * size * stride * (float)Math.Sin(phase)));
				}
				if (UpperArmL >= 0)
				{
					Mul(m, UpperArmL, Mat3.RotX(-0.1f * size) * Mat3.RotY(-0.17f * size));
				}
				if (UpperArmR >= 0)
				{
					Mul(m, UpperArmR, Mat3.RotX(-0.1f * size) * Mat3.RotY(0.17f * size));
				}
				if (LowerArmL >= 0)
				{
					Mul(m, LowerArmL, Mat3.RotX(-0.25f * size));
				}
				if (LowerArmR >= 0)
				{
					Mul(m, LowerArmR, Mat3.RotX(-0.25f * size));
				}
			}
		}

		public Vec3 LeftHandWorld(MeshInstance m)
		{
			if (LowerArmL < 0)
			{
				return m.Position;
			}
			Vec3 local = Model.Parts[LowerArmL].Pivot + new Vec3(-0.004f, -0.012f, -0.3f);
			return Transform(m, LowerArmL, local);
		}

		public void HoldTorch(MeshInstance m, float k)
		{
			if (!(k <= 0f) && LowerArmL >= 0)
			{
				m.EnsurePose();
				Rot(m, UpperArmL, Mat3.RotX(-0.3f * k) * Mat3.RotY(-0.14f * k));
				Rot(m, LowerArmL, Mat3.RotX(-1.2f * k));
			}
		}

		public void TorchPlace(MeshInstance body, float heading, float height, out Vec3 position, out Mat3 basis)
		{
			position = LeftHandWorld(body);
			Vec3 vec = new Vec3((float)Math.Cos(heading), (float)Math.Sin(heading), 0f);
			Vec3 vec2 = (new Vec3(0f, 0f, 1f) + vec * 0.12f).Normalized();
			Vec3 vec3 = Vec3.Cross(vec, vec2).Normalized();
			Vec3 vec4 = Vec3.Cross(vec2, vec3);
			float num = Math.Max(0.3f, Math.Min(3f, height));
			basis = new Mat3(vec3 * num, vec4 * num, vec2 * num);
		}

		public Vec3 HandWorld(MeshInstance m)
		{
			if (LowerArmR < 0)
			{
				return m.Position;
			}
			Vec3 local = Model.Parts[LowerArmR].Pivot + new Vec3(0.004f, -0.012f, -0.3f);
			return Transform(m, LowerArmR, local);
		}

		public static Vec3 Transform(MeshInstance m, int part, Vec3 local)
		{
			if (RigTune.Transform)
			{
				if (!RigTune.TransformCheck.On)
				{
					return TransformFast(m, part, local);
				}
				return TransformChecked(m, part, local);
			}
			return TransformOld(m, part, local);
		}

		internal static Vec3 TransformOld(MeshInstance m, int part, Vec3 local)
		{
			MeshModel model = m.Model;
			Vec3 vec = local;
			for (int num = part; num >= 0; num = model.Parts[num].Parent)
			{
				MeshPart meshPart = model.Parts[num];
				Mat3 mat = ((m.PartRotation != null) ? m.PartRotation[num] : Mat3.Identity);
				float num2 = ((m.PartScale != null) ? m.PartScale[num] : 1f);
				vec = mat.Mul(vec - meshPart.Pivot) * num2 + meshPart.Pivot;
				if (m.PartOffset != null)
				{
					vec += m.PartOffset[num];
				}
			}
			return m.Basis.Mul(vec) + m.Position;
		}

		internal static Vec3 TransformFast(MeshInstance m, int part, Vec3 local)
		{
			MeshPart[] parts = m.Model.Parts;
			Mat3[] partRotation = m.PartRotation;
			Vec3[] partOffset = m.PartOffset;
			float[] partScale = m.PartScale;
			float px = local.X;
			float py = local.Y;
			float pz = local.Z;
			for (int num = part; num >= 0; num = parts[num].Parent)
			{
				MeshPart q = parts[num];
				if (partRotation != null)
				{
					Level(q, in partRotation[num], (partScale != null) ? partScale[num] : 1f, partOffset, num, ref px, ref py, ref pz);
				}
				else
				{
					Level(q, in Ident, (partScale != null) ? partScale[num] : 1f, partOffset, num, ref px, ref py, ref pz);
				}
			}
			return Placed(m, px, py, pz);
		}

		internal static Vec3 TransformDirFast(MeshInstance m, int part, Vec3 local)
		{
			MeshPart[] parts = m.Model.Parts;
			Mat3[] partRotation = m.PartRotation;
			float num = local.X;
			float num2 = local.Y;
			float num3 = local.Z;
			for (int num4 = part; num4 >= 0; num4 = parts[num4].Parent)
			{
				if (partRotation != null)
				{
					ref Mat3 reference = ref partRotation[num4];
					float num5 = reference.C0.X * num + reference.C1.X * num2 + reference.C2.X * num3;
					float num6 = reference.C0.Y * num + reference.C1.Y * num2 + reference.C2.Y * num3;
					float num7 = reference.C0.Z * num + reference.C1.Z * num2 + reference.C2.Z * num3;
					num = num5;
					num2 = num6;
					num3 = num7;
				}
			}
			ref Mat3 basis = ref m.Basis;
			return new Vec3(basis.C0.X * num + basis.C1.X * num2 + basis.C2.X * num3, basis.C0.Y * num + basis.C1.Y * num2 + basis.C2.Y * num3, basis.C0.Z * num + basis.C1.Z * num2 + basis.C2.Z * num3);
		}

		private static void Level(MeshPart q, in Mat3 r, float sc, Vec3[] off, int k, ref float px, ref float py, ref float pz)
		{
			float x = q.Pivot.X;
			float y = q.Pivot.Y;
			float z = q.Pivot.Z;
			float num = px - x;
			float num2 = py - y;
			float num3 = pz - z;
			float num4 = r.C0.X * num + r.C1.X * num2 + r.C2.X * num3;
			float num5 = r.C0.Y * num + r.C1.Y * num2 + r.C2.Y * num3;
			float num6 = r.C0.Z * num + r.C1.Z * num2 + r.C2.Z * num3;
			num4 = num4 * sc + x;
			num5 = num5 * sc + y;
			num6 = num6 * sc + z;
			if (off != null)
			{
				ref Vec3 reference = ref off[k];
				num4 += reference.X;
				num5 += reference.Y;
				num6 += reference.Z;
			}
			px = num4;
			py = num5;
			pz = num6;
		}

		private static Vec3 Placed(MeshInstance m, float px, float py, float pz)
		{
			ref Mat3 basis = ref m.Basis;
			ref Vec3 position = ref m.Position;
			return new Vec3(basis.C0.X * px + basis.C1.X * py + basis.C2.X * pz + position.X, basis.C0.Y * px + basis.C1.Y * py + basis.C2.Y * pz + position.Y, basis.C0.Z * px + basis.C1.Z * py + basis.C2.Z * pz + position.Z);
		}

		private static void Times(in Mat3 a, in Mat3 b, out Mat3 c)
		{
			c = default(Mat3);
			Column(in a, in b.C0, ref c.C0);
			Column(in a, in b.C1, ref c.C1);
			Column(in a, in b.C2, ref c.C2);
		}

		private static void Column(in Mat3 a, in Vec3 v, ref Vec3 into)
		{
			float x = v.X;
			float y = v.Y;
			float z = v.Z;
			into.X = a.C0.X * x + a.C1.X * y + a.C2.X * z;
			into.Y = a.C0.Y * x + a.C1.Y * y + a.C2.Y * z;
			into.Z = a.C0.Z * x + a.C1.Z * y + a.C2.Z * z;
		}

		internal void PlantFast(MeshInstance m, WalkCycle.Walker w)
		{
			if (w != null && UpperLegL >= 0 && UpperLegR >= 0 && LowerLegL >= 0 && LowerLegR >= 0 && m.PartRotation != null)
			{
				float duty = WalkCycle.Duty(w.Run);
				float angle;
				float lift;
				bool down = WalkCycle.Leg(w.Phase, left: true, duty, out angle, out lift);
				bool down2 = WalkCycle.Leg(w.Phase, left: false, duty, out lift, out angle);
				PlantLegFast(m, UpperLegL, LowerLegL, _soleL, down, ref w.FootL);
				PlantLegFast(m, UpperLegR, LowerLegR, _soleR, down2, ref w.FootR);
			}
		}

		private void PlantLegFast(MeshInstance m, int upper, int lower, Vec3 sole, bool down, ref WalkCycle.Foot f)
		{
			if (!down)
			{
				f.Down = false;
				return;
			}
			MeshPart[] parts = m.Model.Parts;
			Mat3[] partRotation = m.PartRotation;
			Vec3[] partOffset = m.PartOffset;
			float[] partScale = m.PartScale;
			float px = sole.X;
			float py = sole.Y;
			float pz = sole.Z;
			int num = lower;
			while (num >= 0 && num != upper)
			{
				Level(parts[num], in partRotation[num], (partScale != null) ? partScale[num] : 1f, partOffset, num, ref px, ref py, ref pz);
				num = parts[num].Parent;
			}
			if (num != upper)
			{
				PlantLegOld(m, upper, lower, sole, down, ref f);
				return;
			}
			MeshPart thigh = parts[upper];
			float sc = ((partScale != null) ? partScale[upper] : 1f);
			Mat3 r = m.PartRotation[upper];
			Vec3 vec = FromThigh(m, thigh, in r, sc, upper, px, py, pz);
			if (!f.Down)
			{
				f.Down = true;
				f.X = vec.X;
				f.Y = vec.Y;
				return;
			}
			float num2 = f.X - vec.X;
			float num3 = f.Y - vec.Y;
			if (num2 * num2 + num3 * num3 < 1E-10f)
			{
				return;
			}
			float num4 = (_thigh + _shin) * m.Basis.C2.Z;
			if (num2 * num2 + num3 * num3 > 0.36f * num4 * num4)
			{
				f.X = vec.X;
				f.Y = vec.Y;
				return;
			}
			Times(in TryForward, in r, out var c);
			Vec3 vec2 = FromThigh(m, thigh, in c, sc, upper, px, py, pz);
			Times(in TryAside, in r, out var c2);
			Vec3 vec3 = FromThigh(m, thigh, in c2, sc, upper, px, py, pz);
			float num5 = (vec2.X - vec.X) / 0.02f;
			float num6 = (vec2.Y - vec.Y) / 0.02f;
			float num7 = (vec3.X - vec.X) / 0.02f;
			float num8 = (vec3.Y - vec.Y) / 0.02f;
			float num9 = num5 * num8 - num6 * num7;
			if (Math.Abs(num9) < 1E-08f)
			{
				return;
			}
			float num10 = (num2 * num8 - num3 * num7) / num9;
			float num11 = (num5 * num3 - num6 * num2) / num9;
			bool flag = Math.Abs(num10) > PlantReach || Math.Abs(num11) > PlantReach;
			if (PlantStats != null)
			{
				PlantStats[0] += Math.Min(PlantReach, Math.Abs(num10));
				PlantStats[1] += Math.Min(PlantReach, Math.Abs(num11));
				PlantStats[2] += 1f;
				if (flag)
				{
					PlantStats[3] += 1f;
					if (Math.Abs(num10) > PlantReach)
					{
						PlantStats[4] += 1f;
					}
					PlantStats[5] += (float)Math.Sqrt(num2 * num2 + num3 * num3) / Math.Max(0.0001f, num4);
				}
			}
			num10 = Math.Max(0f - PlantReach, Math.Min(PlantReach, num10));
			num11 = Math.Max(0f - PlantReach, Math.Min(PlantReach, num11));
			m.PartRotation[upper] = Mat3.RotY(num11) * Mat3.RotX(num10) * r;
			if (flag)
			{
				Vec3 vec4 = FromThigh(m, thigh, in m.PartRotation[upper], sc, upper, px, py, pz);
				f.X = vec4.X;
				f.Y = vec4.Y;
			}
		}

		private static Vec3 FromThigh(MeshInstance m, MeshPart thigh, in Mat3 r, float sc, int upper, float px, float py, float pz)
		{
			MeshPart[] parts = m.Model.Parts;
			Mat3[] partRotation = m.PartRotation;
			Vec3[] partOffset = m.PartOffset;
			float[] partScale = m.PartScale;
			Level(thigh, in r, sc, partOffset, upper, ref px, ref py, ref pz);
			for (int parent = thigh.Parent; parent >= 0; parent = parts[parent].Parent)
			{
				Level(parts[parent], in partRotation[parent], (partScale != null) ? partScale[parent] : 1f, partOffset, parent, ref px, ref py, ref pz);
			}
			return Placed(m, px, py, pz);
		}

		private static Vec3 TransformChecked(MeshInstance m, int part, Vec3 local)
		{
			Vec3 vec = TransformFast(m, part, local);
			Vec3 vec2 = TransformOld(m, part, local);
			RigTune.TransformCheck.Count(RigTune.Same(vec, vec2) ? null : ("Transform of part " + part + " of " + m.Model.Name + " at " + RigTune.Show(local) + ": " + RigTune.Show(vec) + " vs " + RigTune.Show(vec2)));
			return vec;
		}

		private static Vec3 TransformDirChecked(MeshInstance m, int part, Vec3 local)
		{
			Vec3 vec = TransformDirFast(m, part, local);
			Vec3 vec2 = TransformDirOld(m, part, local);
			RigTune.TransformCheck.Busy++;
			RigTune.TransformCheck.Count(RigTune.Same(vec, vec2) ? null : ("TransformDir of part " + part + " of " + m.Model.Name + " along " + RigTune.Show(local) + ": " + RigTune.Show(vec) + " vs " + RigTune.Show(vec2)));
			return vec;
		}

		private void PlantChecked(MeshInstance m, WalkCycle.Walker w)
		{
			if (w == null || m.PartRotation == null || UpperLegL < 0 || UpperLegR < 0 || LowerLegL < 0 || LowerLegR < 0)
			{
				PlantFast(m, w);
				return;
			}
			MeshInstance checkBody = CheckBody;
			RigTune.CopyPose(m, checkBody);
			WalkCycle.Walker checkWalker = CheckWalker;
			checkWalker.Phase = w.Phase;
			checkWalker.Run = w.Run;
			checkWalker.FootL = w.FootL;
			checkWalker.FootR = w.FootR;
			Mat3 a = m.PartRotation[UpperLegL];
			Mat3 a2 = m.PartRotation[UpperLegR];
			WalkCycle.Foot footL = w.FootL;
			WalkCycle.Foot footR = w.FootR;
			PlantFast(m, w);
			float[] plantStats = PlantStats;
			bool transform = RigTune.Transform;
			PlantStats = null;
			RigTune.Transform = false;
			try
			{
				PlantOld(checkBody, checkWalker);
			}
			finally
			{
				RigTune.Transform = transform;
				PlantStats = plantStats;
			}
			if (!RigTune.Same(a, m.PartRotation[UpperLegL]) || !RigTune.Same(a2, m.PartRotation[UpperLegR]))
			{
				RigTune.PlantCheck.Busy++;
			}
			if (Slid(footL, w.FootL) || Slid(footR, w.FootR))
			{
				RigTune.PlantCheck.Busier++;
			}
			string text = FootDiffers("left foot", checkWalker.FootL, w.FootL) ?? FootDiffers("right foot", checkWalker.FootR, w.FootR) ?? RigTune.PoseDiffers(checkBody, m);
			RigTune.PlantCheck.Count((text == null) ? null : (text + ", " + m.Model.Name + " at phase " + RigTune.Show(w.Phase) + ", run " + RigTune.Show(w.Run)));
			Release(checkBody);
		}

		private static bool Slid(WalkCycle.Foot was, WalkCycle.Foot now)
		{
			if (was.Down && now.Down)
			{
				if (RigTune.Bits(was.X) == RigTune.Bits(now.X))
				{
					return RigTune.Bits(was.Y) != RigTune.Bits(now.Y);
				}
				return true;
			}
			return false;
		}

		private static string FootDiffers(string which, WalkCycle.Foot old, WalkCycle.Foot now)
		{
			if (old.Down != now.Down)
			{
				return which + ": down " + old.Down + " vs " + now.Down;
			}
			if (RigTune.Bits(old.X) != RigTune.Bits(now.X))
			{
				return which + ": X " + RigTune.Show(old.X) + " vs " + RigTune.Show(now.X);
			}
			if (RigTune.Bits(old.Y) != RigTune.Bits(now.Y))
			{
				return which + ": Y " + RigTune.Show(old.Y) + " vs " + RigTune.Show(now.Y);
			}
			return null;
		}

		private static void Release(MeshInstance copy)
		{
			copy.Model = null;
			copy.PartRotation = null;
			copy.PartOffset = null;
			copy.PartScale = null;
		}

		private void PoseChecked(MeshInstance m, float stride, float phase, float swing, float breath, float headTurn, float headNod, float run, WalkCycle.Walker walker)
		{
			MeshInstance checkBody = CheckBody;
			RigTune.CopyPose(m, checkBody);
			PoseLean(m, stride, phase, swing, breath, headTurn, headNod, run, walker);
			PoseOld(checkBody, stride, phase, swing, breath, headTurn, headNod, run, walker);
			string text = RigTune.PoseDiffers(checkBody, m);
			RigTune.LeanCheck.Count((text == null) ? null : ("Pose: " + text + ", " + m.Model.Name));
			Release(checkBody);
		}

		private void FightChecked(MeshInstance m, WeaponKind kind, float attack, float stance, float flinch, float lean, float side)
		{
			MeshInstance checkBody = CheckBody;
			RigTune.CopyPose(m, checkBody);
			FightLean(m, kind, attack, stance, flinch, lean, side);
			FightOld(checkBody, kind, attack, stance, flinch, lean, side);
			if (stance > 0f)
			{
				RigTune.LeanCheck.Busy++;
				if (attack > 0f && attack < 1f)
				{
					RigTune.LeanCheck.Busier++;
				}
			}
			string text = RigTune.PoseDiffers(checkBody, m);
			RigTune.LeanCheck.Count((text == null) ? null : ("Fight (" + kind.ToString() + ", attack " + RigTune.Show(attack) + ", stance " + RigTune.Show(stance) + ", side " + RigTune.Show(side) + "): " + text + ", " + m.Model.Name));
			Release(checkBody);
		}

		private void ResetUnposed(MeshInstance m)
		{
			bool[] array = _posed;
			if (array == null)
			{
				array = new bool[Model.Parts.Length];
				int[] array2 = new int[10] { UpperLegL, UpperLegR, LowerLegL, LowerLegR, UpperArmL, UpperArmR, LowerArmL, LowerArmR, Torso, Head };
				foreach (int num in array2)
				{
					if (num >= 0 && num < array.Length)
					{
						array[num] = true;
					}
				}
				_posed = array;
			}
			Mat3[] partRotation = m.PartRotation;
			if (partRotation == null || partRotation.Length != array.Length)
			{
				m.ResetPose();
				return;
			}
			for (int j = 0; j < partRotation.Length; j++)
			{
				if (!array[j])
				{
					partRotation[j] = Ident;
				}
			}
			if (m.PartOffset != null)
			{
				Array.Clear(m.PartOffset, 0, m.PartOffset.Length);
			}
			if (m.PartScale != null)
			{
				for (int k = 0; k < m.PartScale.Length; k++)
				{
					m.PartScale[k] = 1f;
				}
			}
		}

		internal void PoseLean(MeshInstance m, float stride, float phase, float swing, float breath, float headTurn, float headNod, float run, WalkCycle.Walker walker)
		{
			m.EnsurePose();
			ResetUnposed(m);
			float num = (float)Math.Sin(phase);
			Math.Cos(phase);
			run = ((run < 0f) ? 0f : ((run > 1f) ? 1f : run));
			float num2 = 0.5f * stride;
			float num3 = (0.42f + 0.16f * run) * stride;
			float num4 = 0.9f + 0.55f * run;
			if (Robe)
			{
				if (walker == null)
				{
					num2 *= 0.38f;
				}
				num4 = 0.35f;
			}
			float duty = WalkCycle.Duty(run);
			float angle;
			float lift;
			bool flag = WalkCycle.Leg(phase, left: true, duty, out angle, out lift);
			float angle2;
			float lift2;
			bool flag2 = WalkCycle.Leg(phase, left: false, duty, out angle2, out lift2);
			float num5 = (0.06f + 0.25f * run) * stride;
			float num6 = (flag ? (num5 * (1f - Math.Abs(angle))) : 0f);
			float num7 = (flag2 ? (num5 * (1f - Math.Abs(angle2))) : 0f);
			float num8 = Math.Max(stride, walker?.Lift ?? 0f);
			float num9 = num4 * num8 * lift + num6 + 0.05f;
			float num10 = num4 * num8 * lift2 + num7 + 0.05f;
			float num11 = ((_thigh + _shin > 0f) ? (_shin / (_thigh + _shin)) : 0.5f);
			float z = ((walker != null) ? (TwistSign * walker.TwistL) : 0f);
			float z2 = ((walker != null) ? (TwistSign * walker.TwistR) : 0f);
			Rot(m, UpperLegL, Turns.ZX(z, (0f - num2) * angle - num6 * num11));
			Rot(m, UpperLegR, Turns.ZX(z2, (0f - num2) * angle2 - num7 * num11));
			Rot(m, LowerLegL, Mat3.RotX(num9));
			Rot(m, LowerLegR, Mat3.RotX(num10));
			float num12 = 0.04f * (float)Math.Sin(breath * 1.3f);
			Rot(m, UpperArmL, Turns.XY(num3 * num, -0.07f - num12 * 0.3f));
			Rot(m, UpperArmR, Turns.XY((0f - num3) * num, 0.07f + num12 * 0.3f));
			Rot(m, LowerArmL, Mat3.RotX(-0.18f - 1.05f * run - 0.25f * stride * Math.Max(0f, 0f - num)));
			Rot(m, LowerArmR, Mat3.RotX(-0.18f - 1.05f * run - 0.25f * stride * Math.Max(0f, num)));
			float num13 = 0f;
			if (flag || flag2)
			{
				float val = (flag ? Reach(num2 * angle + num6 * num11, num9) : float.MaxValue);
				float val2 = (flag2 ? Reach(num2 * angle2 + num7 * num11, num10) : float.MaxValue);
				num13 = WalkCycle.Tune.HipFollow * Math.Min(val, val2);
			}
			if (Hips >= 0)
			{
				m.PartOffset[Hips] = new Vec3(0f, 0f, 0f - num13 + 0.004f * (float)Math.Sin(breath * 2.1f));
			}
			Rot(m, Torso, Turns.ZX((0.1f + 0.06f * run) * stride * num, 0.04f * stride + num12 * 0.2f));
			if (swing > 0f)
			{
				float num14 = ((swing < 0.4f) ? (swing / 0.4f) : (1f - (swing - 0.4f) / 0.6f * 1.4f));
				Rot(m, UpperArmR, Mat3.RotX(-2.4f * Math.Max(-0.3f, num14)) * Mat3.RotY(0.25f));
				Rot(m, LowerArmR, Mat3.RotX(-0.6f * Math.Max(0f, num14)));
				Rot(m, Torso, Mat3.RotZ(-0.25f * (1f - Math.Abs(num14))) * Mat3.RotX(0.1f));
			}
			Rot(m, Head, Turns.ZX(headTurn, headNod));
		}

		private static void Afresh(WeaponKind kind, float attack, float side, out bool upperL, out bool lowerL, out bool upperR, out bool lowerR, out bool torso)
		{
			upperL = (lowerL = (upperR = (lowerR = (torso = false))));
			if (!(attack > 0f) || !(attack < 1f))
			{
				return;
			}
			switch (kind)
			{
			case WeaponKind.Sword:
			case WeaponKind.Stick:
			case WeaponKind.Dagger:
				upperR = (lowerR = true);
				return;
			case WeaponKind.Axe:
			case WeaponKind.Hammer:
				upperR = (lowerR = (upperL = (torso = true)));
				return;
			case WeaponKind.Spear:
				upperR = (lowerR = (upperL = (lowerL = (torso = true))));
				return;
			case WeaponKind.Bow:
				upperL = (lowerL = (upperR = (lowerR = true)));
				return;
			case WeaponKind.Staff:
				upperR = (lowerR = (torso = true));
				return;
			case WeaponKind.Shotgun:
				return;
			}
			if (side > 0f)
			{
				upperR = (lowerR = true);
			}
			else
			{
				upperL = (lowerL = true);
			}
			torso = true;
		}

		internal void FightLean(MeshInstance m, WeaponKind kind, float attack, float stance, float flinch, float lean, float side)
		{
			m.EnsurePose();
			if (stance > 0f)
			{
				Afresh(kind, attack, side, out var upperL, out var lowerL, out var upperR, out var lowerR, out var torso);
				Mul(m, UpperLegL, Mat3.RotX(-0.28f * stance));
				Mul(m, UpperLegR, Mat3.RotX(-0.12f * stance));
				Mul(m, LowerLegL, Mat3.RotX(0.42f * stance));
				Mul(m, LowerLegR, Mat3.RotX(0.3f * stance));
				if (Hips >= 0)
				{
					m.PartOffset[Hips] = m.PartOffset[Hips] + new Vec3(0f, 0f, -0.05f * stance);
				}
				if (!torso)
				{
					Mul(m, Torso, Mat3.RotX(0.12f * stance));
				}
				switch (kind)
				{
				case WeaponKind.Bow:
					if (!upperL)
					{
						Rot(m, UpperArmL, Mat3.RotX(-1.45f * stance) * Mat3.RotY(-0.1f));
					}
					if (!lowerL)
					{
						Rot(m, LowerArmL, Mat3.RotX(-0.1f * stance));
					}
					if (!upperR)
					{
						Rot(m, UpperArmR, Mat3.RotX(-1.35f * stance) * Mat3.RotY(-0.35f * stance));
					}
					if (!lowerR)
					{
						Rot(m, LowerArmR, Mat3.RotX(-1.5f * stance));
					}
					break;
				case WeaponKind.Shotgun:
					if (!upperR)
					{
						Rot(m, UpperArmR, Mat3.RotX(-0.42f * stance) * Mat3.RotY(0.32f * stance));
					}
					if (!lowerR)
					{
						Rot(m, LowerArmR, Mat3.RotX(-1.3f * stance) * Mat3.RotY(-0.25f * stance));
					}
					if (!upperL)
					{
						Rot(m, UpperArmL, Mat3.RotX(-1.15f * stance) * Mat3.RotY(-0.12f * stance));
					}
					if (!lowerL)
					{
						Rot(m, LowerArmL, Mat3.RotX(-0.6f * stance));
					}
					if (!torso)
					{
						Mul(m, Torso, Mat3.RotZ(-0.12f * stance));
					}
					break;
				case WeaponKind.Spear:
				case WeaponKind.Staff:
					if (!upperR)
					{
						Rot(m, UpperArmR, Mat3.RotX(-0.6f * stance) * Mat3.RotY(0.15f));
					}
					if (!lowerR)
					{
						Rot(m, LowerArmR, Mat3.RotX(-1f * stance));
					}
					if (!upperL)
					{
						Rot(m, UpperArmL, Mat3.RotX(-0.8f * stance) * Mat3.RotY(0.25f * stance));
					}
					if (!lowerL)
					{
						Rot(m, LowerArmL, Mat3.RotX(-0.9f * stance));
					}
					break;
				default:
					if (!upperR)
					{
						Rot(m, UpperArmR, Mat3.RotX(-0.75f * stance) * Mat3.RotY(0.22f));
					}
					if (!lowerR)
					{
						Rot(m, LowerArmR, Mat3.RotX(-1.05f * stance));
					}
					if (!upperL)
					{
						Rot(m, UpperArmL, Mat3.RotX(-0.55f * stance) * Mat3.RotY(-0.2f));
					}
					if (!lowerL)
					{
						Rot(m, LowerArmL, Mat3.RotX(-1.25f * stance));
					}
					break;
				}
			}
			if (attack > 0f && attack < 1f)
			{
				switch (kind)
				{
				case WeaponKind.Sword:
				case WeaponKind.Stick:
				case WeaponKind.Dagger:
					BladeStroke(m, attack, side, stance);
					break;
				case WeaponKind.Axe:
				case WeaponKind.Hammer:
				{
					float num3 = ((attack < 0.4f) ? Ease(attack / 0.4f) : ((attack < 0.58f) ? (1f - 1.25f * Ease((attack - 0.4f) / 0.18f)) : (-0.25f * (1f - Ease((attack - 0.58f) / 0.42f)))));
					Rot(m, UpperArmR, Mat3.RotX(-0.75f - 2.15f * num3) * Mat3.RotY(0.2f));
					Rot(m, LowerArmR, Mat3.RotX(-0.6f * Math.Max(0f, num3) - 0.15f));
					Rot(m, UpperArmL, Mat3.RotX(-0.6f - 1.8f * Math.Max(0f, num3)) * Mat3.RotY(-0.15f));
					Rot(m, Torso, Mat3.RotX(-0.15f * Math.Max(0f, num3) + 0.35f * Math.Max(0f, 0f - num3)));
					break;
				}
				case WeaponKind.Spear:
				{
					float num6 = ((attack < 0.3f) ? Ease(attack / 0.3f) : 0f);
					float num7 = ((attack < 0.3f) ? 0f : ((attack < 0.5f) ? Ease((attack - 0.3f) / 0.2f) : (1f - Ease((attack - 0.5f) / 0.5f))));
					Rot(m, UpperArmR, Mat3.RotX(-1f + 0.4f * num6 - 0.55f * num7) * Mat3.RotY(0.15f));
					Rot(m, LowerArmR, Mat3.RotX(-1.2f * (1f - num7) - 0.1f));
					Rot(m, UpperArmL, Mat3.RotX(-1.1f + 0.3f * num6 - 0.4f * num7) * Mat3.RotY(0.3f));
					Rot(m, LowerArmL, Mat3.RotX(-0.8f * (1f - num7)));
					Rot(m, Torso, Mat3.RotX(0.25f * num7 - 0.1f * num6));
					if (Hips >= 0)
					{
						m.PartOffset[Hips] = m.PartOffset[Hips] + new Vec3(0f, -0.12f * num7 + 0.04f * num6, -0.03f * num7);
					}
					break;
				}
				case WeaponKind.Bow:
				{
					float num2 = ((attack < 0.15f) ? Ease(attack / 0.15f) : (1f - Ease((attack - 0.15f) / 0.5f)));
					Rot(m, UpperArmL, Mat3.RotX(-1.5f) * Mat3.RotY(-0.1f));
					Rot(m, LowerArmL, Mat3.RotX(-0.05f));
					Rot(m, UpperArmR, Mat3.RotX(-1.35f + 0.3f * num2) * Mat3.RotY(-0.35f - 0.5f * num2));
					Rot(m, LowerArmR, Mat3.RotX(-1.6f + 0.4f * num2));
					break;
				}
				case WeaponKind.Shotgun:
				{
					float num4 = ShotgunHold.Kick(attack);
					float num5 = ShotgunHold.Pump(attack);
					Mul(m, Torso, Mat3.RotX(-0.16f * num4));
					Mul(m, Head, Mat3.RotX(-0.1f * num4));
					Mul(m, UpperArmR, Mat3.RotX(-0.22f * num4));
					Mul(m, UpperArmL, Mat3.RotX(-0.22f * num4 + 0.28f * num5));
					Mul(m, LowerArmL, Mat3.RotX(-0.35f * num5));
					break;
				}
				case WeaponKind.Staff:
				{
					float num8 = ((attack < 0.35f) ? Ease(attack / 0.35f) : (1f - Ease((attack - 0.35f) / 0.65f)));
					Rot(m, UpperArmR, Mat3.RotX(-0.5f - 1.7f * num8) * Mat3.RotY(0.15f));
					Rot(m, LowerArmR, Mat3.RotX(-0.6f * (1f - num8)));
					Rot(m, Torso, Mat3.RotX(-0.12f * num8));
					break;
				}
				default:
				{
					float num = ((attack < 0.25f) ? Ease(attack / 0.25f) : (1f - Ease((attack - 0.25f) / 0.75f)));
					int part = ((side > 0f) ? UpperArmR : UpperArmL);
					int part2 = ((side > 0f) ? LowerArmR : LowerArmL);
					Rot(m, part, Mat3.RotX(-0.7f - 0.85f * num) * Mat3.RotY(side * -0.15f * num));
					Rot(m, part2, Mat3.RotX(-1.2f * (1f - num) - 0.05f));
					Rot(m, Torso, Mat3.RotZ((0f - side) * 0.3f * num) * Mat3.RotX(0.1f * num));
					break;
				}
				}
			}
			if (flinch > 0f)
			{
				Mul(m, Torso, Mat3.RotX(-0.35f * flinch));
				Mul(m, Head, Mat3.RotX(-0.3f * flinch));
				Mul(m, UpperArmL, Mat3.RotY(-0.35f * flinch));
				Mul(m, UpperArmR, Mat3.RotY(0.35f * flinch));
			}
			if (lean > 0f)
			{
				Mul(m, Torso, Mat3.RotX(0.22f * lean));
			}
		}

		public float RestHip(RestStyle style, float seat)
		{
			if (_thigh <= 0f)
			{
				return _hip;
			}
			switch (style)
			{
			case RestStyle.Seat:
				return Math.Max(0f, seat) + SeatPad;
			case RestStyle.Ground:
			case RestStyle.SitBack:
			case RestStyle.CrossLegged:
				return GroundPad;
			case RestStyle.Kneel:
			case RestStyle.KneelBoth:
				return _thigh + KneePad;
			default:
				return _hip;
			}
		}

		public void Rest(MeshInstance m, RestStyle style, float amount, float seat, float time)
		{
			if (style == RestStyle.None || amount <= 0f || _thigh <= 0f)
			{
				return;
			}
			m.EnsurePose();
			float num = Ease(amount);
			float num2 = RestHip(style, seat);
			if (Hips >= 0)
			{
				m.PartOffset[Hips] = new Vec3(0f, 0f, (num2 - _hip) * num);
			}
			float num3 = 0.012f * (float)Math.Sin(time * 1.7f);
			float thigh = _thigh;
			float shin = _shin;
			switch (style)
			{
			case RestStyle.Seat:
			{
				float num4 = num2;
				float ahead4;
				float down;
				if (num4 >= shin)
				{
					float num5 = Math.Min(num4 - shin, thigh * 0.55f);
					ahead4 = (float)Math.Sqrt(Math.Max(0f, thigh * thigh - num5 * num5));
					down = num5 + shin;
				}
				else
				{
					down = num4;
					ahead4 = thigh * (1f + 0.24f * (1f - num4 / shin));
				}
				LegTo(m, UpperLegL, LowerLegL, ahead4, down, -0.06f, 0f, num);
				LegTo(m, UpperLegR, LowerLegR, ahead4, down, 0.06f, 0f, num);
				Rot(m, Torso, Mat3.RotX((0.02f + num3) * num));
				HandsOnThighs(m, num);
				break;
			}
			case RestStyle.Ground:
			{
				float ahead3 = 0.62f * (thigh + shin);
				LegTo(m, UpperLegL, LowerLegL, ahead3, num2, -0.16f, 0f, num);
				LegTo(m, UpperLegR, LowerLegR, ahead3, num2, 0.16f, 0f, num);
				Rot(m, Torso, Mat3.RotX((0.12f + num3) * num));
				Rot(m, UpperArmL, Mat3.RotX(-0.8f * num) * Mat3.RotY(-0.2f * num));
				Rot(m, UpperArmR, Mat3.RotX(-0.8f * num) * Mat3.RotY(0.2f * num));
				Rot(m, LowerArmL, Mat3.RotX(-0.2f * num));
				Rot(m, LowerArmR, Mat3.RotX(-0.2f * num));
				break;
			}
			case RestStyle.SitBack:
			{
				float ahead2 = 0.96f * (thigh + shin);
				LegTo(m, UpperLegL, LowerLegL, ahead2, num2, -0.1f, 0f, num);
				LegTo(m, UpperLegR, LowerLegR, ahead2, num2, 0.1f, 0f, num);
				Rot(m, Torso, Mat3.RotX((-0.42f + num3) * num));
				Rot(m, UpperArmL, Mat3.RotX(0.62f * num) * Mat3.RotY(0.25f * num));
				Rot(m, UpperArmR, Mat3.RotX(0.62f * num) * Mat3.RotY(-0.25f * num));
				Rot(m, LowerArmL, Mat3.RotX(-0.08f * num));
				Rot(m, LowerArmR, Mat3.RotX(-0.08f * num));
				Mul(m, Head, Mat3.RotX(0.3f * num));
				break;
			}
			case RestStyle.CrossLegged:
				Rot(m, UpperLegL, Mat3.RotZ(-0.72f * num) * Mat3.RotX(-1.42f * num) * Mat3.RotZ(-1.5708f * num));
				Rot(m, UpperLegR, Mat3.RotZ(0.72f * num) * Mat3.RotX(-1.3399999f * num) * Mat3.RotZ(1.5708f * num));
				Rot(m, LowerLegL, Mat3.RotX(2.2f * num));
				Rot(m, LowerLegR, Mat3.RotX(2.1000001f * num));
				Rot(m, Torso, Mat3.RotX((0.06f + num3) * num));
				Rot(m, UpperArmL, Mat3.RotX(-0.4f * num) * Mat3.RotY(0.3f * num));
				Rot(m, UpperArmR, Mat3.RotX(-0.4f * num) * Mat3.RotY(-0.3f * num));
				Rot(m, LowerArmL, Mat3.RotX(-0.5f * num));
				Rot(m, LowerArmR, Mat3.RotX(-0.5f * num));
				break;
			case RestStyle.Kneel:
			{
				float val = Math.Max(0f, num2 - shin);
				float ahead = (float)Math.Sqrt(Math.Max(0f, thigh * thigh - Math.Min(val, thigh) * Math.Min(val, thigh)));
				LegTo(m, UpperLegL, LowerLegL, ahead, num2, -0.08f, 0f, num);
				Rot(m, UpperLegR, Mat3.RotZ(0.05f * num) * Mat3.RotX(0.06f * num));
				Rot(m, LowerLegR, Mat3.RotX(1.5108f * num));
				Rot(m, Torso, Mat3.RotX((0.1f + num3) * num));
				Rot(m, UpperArmL, Mat3.RotX(-0.5f * num) * Mat3.RotY(-0.06f * num));
				Rot(m, LowerArmL, Mat3.RotX(-0.35f * num));
				Rot(m, UpperArmR, Mat3.RotX(0.04f * num) * Mat3.RotY(-0.12f * num));
				Rot(m, LowerArmR, Mat3.RotX(-0.12f * num));
				Mul(m, Head, Mat3.RotX(0.22f * num));
				break;
			}
			case RestStyle.KneelBoth:
				Rot(m, UpperLegL, Mat3.RotZ(-0.06f * num) * Mat3.RotX(-0.04f * num));
				Rot(m, UpperLegR, Mat3.RotZ(0.06f * num) * Mat3.RotX(-0.04f * num));
				Rot(m, LowerLegL, Mat3.RotX(1.6107999f * num));
				Rot(m, LowerLegR, Mat3.RotX(1.6107999f * num));
				Rot(m, Torso, Mat3.RotX((0.05f + num3) * num));
				Rot(m, UpperArmL, Mat3.RotY(-0.2f * num) * Mat3.RotX(-0.3f * num));
				Rot(m, UpperArmR, Mat3.RotY(0.2f * num) * Mat3.RotX(-0.3f * num));
				Rot(m, LowerArmL, Mat3.RotZ(0.8f * num) * Mat3.RotX(-1.5f * num));
				Rot(m, LowerArmR, Mat3.RotZ(-0.8f * num) * Mat3.RotX(-1.5f * num));
				Mul(m, Head, Mat3.RotX(0.32f * num));
				break;
			}
		}

		private void HandsOnThighs(MeshInstance m, float k)
		{
			Rot(m, UpperArmL, Mat3.RotX(-0.2f * k) * Mat3.RotY(-0.1f * k));
			Rot(m, UpperArmR, Mat3.RotX(-0.2f * k) * Mat3.RotY(0.1f * k));
			Rot(m, LowerArmL, Mat3.RotX(-0.95f * k));
			Rot(m, LowerArmR, Mat3.RotX(-0.95f * k));
		}

		private void LegTo(MeshInstance m, int upper, int lower, float ahead, float down, float splay, float roll, float k)
		{
			if (upper >= 0 && lower >= 0)
			{
				float thigh = _thigh;
				float shin = _shin;
				float val = (float)Math.Sqrt(ahead * ahead + down * down);
				val = Math.Max(Math.Abs(thigh - shin) + 0.001f, Math.Min(val, (thigh + shin) * 0.999f));
				float num = MathF.PI - (float)Math.Acos(Clamp1((thigh * thigh + shin * shin - val * val) / (2f * thigh * shin)));
				float num2 = (float)Math.Atan2(ahead, down);
				float num3 = (float)Math.Acos(Clamp1((thigh * thigh + val * val - shin * shin) / (2f * thigh * val)));
				float num4 = num2 + num3;
				Rot(m, upper, Mat3.RotZ(splay * k) * Mat3.RotX((0f - num4) * k) * Mat3.RotZ(roll * k));
				Rot(m, lower, Mat3.RotX(num * k));
			}
		}

		private static float Clamp1(float x)
		{
			if (!(x < -1f))
			{
				if (!(x > 1f))
				{
					return x;
				}
				return 1f;
			}
			return -1f;
		}

		public void Swim(MeshInstance m, float crawl, float stroke, float time, float seed)
		{
			m.EnsurePose();
			float num = Ease(crawl);
			float num2 = 1f - num;
			float num3 = time * 2.6f + seed * 6.28f;
			float num4 = (float)Math.Sin(num3);
			float num5 = (float)Math.Cos(num3);
			float num6 = (float)Math.Sin(stroke * 3f);
			Rot(m, UpperLegL, Mat3.RotZ(-0.25f * num2) * Mat3.RotX(num2 * (-0.55f - 0.25f * num4) + num * 0.24f * num6));
			Rot(m, UpperLegR, Mat3.RotZ(0.25f * num2) * Mat3.RotX(num2 * (-0.55f + 0.25f * num4) - num * 0.24f * num6));
			Rot(m, LowerLegL, Mat3.RotX(num2 * (0.95f + 0.35f * num5) + num * (0.22f + 0.12f * Math.Max(0f, num6))));
			Rot(m, LowerLegR, Mat3.RotX(num2 * (0.95f - 0.35f * num5) + num * (0.22f + 0.12f * Math.Max(0f, 0f - num6))));
			float num7 = Wrap(stroke);
			float num8 = Wrap(stroke + MathF.PI);
			float num9 = ((num7 < MathF.PI) ? ((float)Math.Sin(num7)) : 0f);
			float num10 = ((num8 < MathF.PI) ? ((float)Math.Sin(num8)) : 0f);
			Rot(m, UpperArmL, Mat3.RotX(num2 * -0.3f + num * num7) * Mat3.RotY(-0.95f * num2 - 0.12f * num));
			Rot(m, UpperArmR, Mat3.RotX(num2 * -0.3f + num * num8) * Mat3.RotY(0.95f * num2 + 0.12f * num));
			Rot(m, LowerArmL, Mat3.RotZ(0.45f * num4 * num2) * Mat3.RotX(num2 * -0.55f - num * 0.95f * num9));
			Rot(m, LowerArmR, Mat3.RotZ(-0.45f * num4 * num2) * Mat3.RotX(num2 * -0.55f - num * 0.95f * num10));
			Rot(m, Torso, Mat3.RotZ(0.3f * num * (float)Math.Sin(stroke)) * Mat3.RotX(-0.08f * num2));
			Rot(m, Head, Mat3.RotX(1.05f * num));
			if (Hips >= 0)
			{
				m.PartOffset[Hips] = new Vec3(0f, 0f, 0f);
			}
		}

		public void Wade(MeshInstance m, float share, float phase, float stride)
		{
			if (!(share <= 0.01f))
			{
				m.EnsurePose();
				float num = Ease(share);
				float num2 = ((stride < 0.05f) ? 0f : ((stride > 0.4f) ? 1f : ((stride - 0.05f) / 0.35f)));
				float duty = WalkCycle.Duty(0f);
				WalkCycle.Leg(phase, left: true, duty, out var angle, out var lift);
				WalkCycle.Leg(phase, left: false, duty, out angle, out var lift2);
				Mul(m, UpperLegL, Mat3.RotX(-0.55f * num * num2 * lift));
				Mul(m, UpperLegR, Mat3.RotX(-0.55f * num * num2 * lift2));
				Mul(m, LowerLegL, Mat3.RotX(0.8f * num * num2 * lift));
				Mul(m, LowerLegR, Mat3.RotX(0.8f * num * num2 * lift2));
				Mul(m, Torso, Mat3.RotX(-0.08f * num * num2));
				Mul(m, UpperArmL, Mat3.RotY(-0.6f * num) * Mat3.RotX(-0.25f * num));
				Mul(m, UpperArmR, Mat3.RotY(0.6f * num) * Mat3.RotX(-0.25f * num));
				Mul(m, LowerArmL, Mat3.RotX(-0.5f * num));
				Mul(m, LowerArmR, Mat3.RotX(-0.5f * num));
			}
		}

		private static float Wrap(float a)
		{
			a %= MathF.PI * 2f;
			if (!(a < 0f))
			{
				return a;
			}
			return a + MathF.PI * 2f;
		}

		private void BladeStroke(MeshInstance m, float a, float side, float stance)
		{
			BladeKey bladeKey = BladeAt(a, side, stance);
			Rot(m, UpperArmR, Mat3.RotX(bladeKey.Pitch) * Mat3.RotY(bladeKey.Across) * Mat3.RotZ(bladeKey.Twist));
			Rot(m, LowerArmR, Mat3.RotX(0f - bladeKey.Elbow));
			Mul(m, Torso, Mat3.RotZ(bladeKey.Turn) * Mat3.RotX(bladeKey.Bend));
			if (Hips >= 0)
			{
				m.PartOffset[Hips] = m.PartOffset[Hips] + new Vec3(0f, -0.06f * bladeKey.Lunge, 0f);
			}
		}

		internal static float BladeWrist(float a, float side)
		{
			return BladeAt(a, side, 1f).Wrist;
		}

		private static BladeKey BladeAt(float a, float side, float stance)
		{
			BladeKey[] keys = ((side < 0f) ? Backhand : Forehand);
			a = ((a < 0f) ? 0f : ((a > 1f) ? 1f : a));
			int i;
			for (i = 0; i < keys.Length - 2 && a > keys[i + 1].A; i++)
			{
			}
			BladeKey bladeKey = Key(Math.Max(0, i - 1));
			BladeKey bladeKey2 = Key(i);
			BladeKey bladeKey3 = Key(i + 1);
			BladeKey bladeKey4 = Key(Math.Min(keys.Length - 1, i + 2));
			float h = bladeKey3.A - bladeKey2.A;
			float u = ((h > 1E-05f) ? ((a - bladeKey2.A) / h) : 0f);
			bool first = i == 0;
			bool last = i + 1 == keys.Length - 1;
			return new BladeKey(a, Lane(bladeKey.Pitch, bladeKey2.Pitch, bladeKey3.Pitch, bladeKey4.Pitch, bladeKey.A, bladeKey2.A, bladeKey3.A, bladeKey4.A, first, last), Lane(bladeKey.Across, bladeKey2.Across, bladeKey3.Across, bladeKey4.Across, bladeKey.A, bladeKey2.A, bladeKey3.A, bladeKey4.A, first, last), Lane(bladeKey.Twist, bladeKey2.Twist, bladeKey3.Twist, bladeKey4.Twist, bladeKey.A, bladeKey2.A, bladeKey3.A, bladeKey4.A, first, last), Lane(bladeKey.Elbow, bladeKey2.Elbow, bladeKey3.Elbow, bladeKey4.Elbow, bladeKey.A, bladeKey2.A, bladeKey3.A, bladeKey4.A, first, last), Lane(bladeKey.Turn, bladeKey2.Turn, bladeKey3.Turn, bladeKey4.Turn, bladeKey.A, bladeKey2.A, bladeKey3.A, bladeKey4.A, first, last), Lane(bladeKey.Bend, bladeKey2.Bend, bladeKey3.Bend, bladeKey4.Bend, bladeKey.A, bladeKey2.A, bladeKey3.A, bladeKey4.A, first, last), Lane(bladeKey.Wrist, bladeKey2.Wrist, bladeKey3.Wrist, bladeKey4.Wrist, bladeKey.A, bladeKey2.A, bladeKey3.A, bladeKey4.A, first, last), Lane(bladeKey.Lunge, bladeKey2.Lunge, bladeKey3.Lunge, bladeKey4.Lunge, bladeKey.A, bladeKey2.A, bladeKey3.A, bladeKey4.A, first, last));
			BladeKey Key(int num)
			{
				BladeKey result = keys[num];
				if (num == 0 || num == keys.Length - 1)
				{
					result.Pitch = -0.75f * stance;
					result.Across = 0.22f;
					result.Elbow = 1.05f * stance;
				}
				return result;
			}
			float Lane(float v0, float v1, float v2, float v3, float num3, float num6, float num2, float num5, bool flag, bool flag2)
			{
				float num = ((flag || (v1 - v0) * (v2 - v1) <= 0f) ? 0f : ((v2 - v0) / (num2 - num3)));
				float num4 = ((flag2 || (v2 - v1) * (v3 - v2) <= 0f) ? 0f : ((v3 - v1) / (num5 - num6)));
				float num7 = u * u;
				float num8 = num7 * u;
				return (2f * num8 - 3f * num7 + 1f) * v1 + (num8 - 2f * num7 + u) * h * num + (-2f * num8 + 3f * num7) * v2 + (num8 - num7) * h * num4;
			}
		}
	}
}
