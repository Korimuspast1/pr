using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class Drill
	{
		public struct Blow
		{
			public double Start;

			public double Impact;

			public double End;

			public double From;

			public float FromPhase;

			public float Side;

			public bool Live;

			public bool Landed;

			public bool Matched;

			public float Phase(WeaponKind kind, double t)
			{
				if (!Live)
				{
					return 0f;
				}
				float num = ImpactAt(kind);
				if (!Landed || t < Impact)
				{
					if (num <= 0f)
					{
						return 0f;
					}
					double num2 = Impact - Start;
					float val = ((num2 > 0.0001) ? ((float)((t - Start) / num2)) : 1f);
					return num * Math.Max(0.001f, Math.Min(1f, val));
				}
				double num3 = End - From;
				float num4 = FromPhase + ((num3 > 0.0001) ? ((float)((t - From) / num3)) : 1f) * (1f - FromPhase);
				if (!(num4 >= 1f))
				{
					return Math.Max(0.001f, num4);
				}
				return 0f;
			}
		}

		public sealed class Timing
		{
			public Blow Now;

			public int Landed;

			public int Late;

			public float LastError;

			private float _tilt;

			private const double Slip = 0.25;

			private const double MinWind = 0.12;

			public bool Step(double now, float dt, int step, float timer, float tilt, WeaponKind kind)
			{
				float num = ImpactAt(kind);
				float num2 = StrikeSeconds(kind);
				float num3 = Math.Abs(tilt);
				bool flag = num3 > _tilt + 1f && num3 > 5f;
				_tilt = num3;
				int num4 = StepsToBlow(step);
				if (num4 > 0 && !flag)
				{
					double num5 = now + (double)Math.Max(0f, timer) + (double)((float)num4 * Math.Max(0.005f, dt));
					if (Now.Live && !Now.Landed)
					{
						if (!Now.Matched)
						{
							Now.Impact = Math.Max(num5, Now.Start + 0.12);
						}
					}
					else
					{
						double num6 = num5 - Math.Max(0.12, Math.Min(WindUp(kind), 0.5 * (num5 - now)));
						if (Now.Live && Now.End > num6)
						{
							Hurry(kind, now, Math.Max(now, num6));
						}
						if (now + (double)dt >= num6)
						{
							Now.Live = false;
							Begin(now, Math.Max(num5, now + 0.12), kind);
						}
					}
				}
				if (flag)
				{
					double num7 = now - (double)((40f - Math.Min(40f, num3)) / 300f);
					if (Now.Live && !Now.Matched && Math.Abs(Now.Impact - num7) < 0.25)
					{
						Now.Matched = true;
						if (!Now.Landed)
						{
							Now.Impact = Math.Min(Now.Impact, Math.Max(now, Now.Start + 0.12));
						}
						LastError = (float)(Now.Impact - num7);
					}
					else
					{
						Late++;
						if (Now.Live)
						{
							Hurry(kind, now, now);
						}
						Begin(now, now, kind);
						Now.Matched = true;
						LastError = (float)(now - num7);
					}
				}
				bool result = false;
				if (Now.Live && !Now.Landed && now >= Now.Impact)
				{
					result = (Now.Landed = true);
					Now.From = now;
					Now.FromPhase = num;
					Now.End = now + (double)((1f - num) * num2);
					Landed++;
				}
				if (Now.Live && Now.Landed && now >= Now.End)
				{
					Now.Live = false;
				}
				return result;
			}

			private void Begin(double now, double impact, WeaponKind kind)
			{
				float side = ((Now.Side == 0f) ? 1f : (0f - Now.Side));
				Now = new Blow
				{
					Start = now,
					Impact = Math.Max(now, impact),
					Side = side,
					Live = true
				};
				Now.End = Now.Impact + (double)((1f - ImpactAt(kind)) * StrikeSeconds(kind));
			}

			private void Hurry(WeaponKind kind, double now, double end)
			{
				if (!Now.Live || !Now.Landed)
				{
					Now.Live = false;
					return;
				}
				float num = Now.Phase(kind, now);
				if (num <= 0f || end <= now)
				{
					Now.Live = false;
					return;
				}
				Now.From = now;
				Now.FromPhase = num;
				Now.End = end;
			}
		}

		public struct Sway
		{
			public float X;

			public float Y;

			public float VX;

			public float VY;

			private const float Omega = 13.194689f;

			private const float Damping = 0.22f;

			public bool Moving
			{
				get
				{
					if (!(X * X + Y * Y > 1E-06f))
					{
						return VX * VX + VY * VY > 0.0001f;
					}
					return true;
				}
			}

			public void Hit(float dx, float dy, float knock)
			{
				float num = (float)Math.Sqrt(dx * dx + dy * dy);
				if (!(num < 1E-05f))
				{
					VX += dx / num * knock * 13.194689f;
					VY += dy / num * knock * 13.194689f;
				}
			}

			public void Step(float dt)
			{
				if (!Moving)
				{
					X = (Y = (VX = (VY = 0f)));
					return;
				}
				int num = Math.Max(1, (int)Math.Ceiling(dt / 0.01f));
				float num2 = dt / (float)num;
				for (int i = 0; i < num; i++)
				{
					VX += (-174.09981f * X - 5.805663f * VX) * num2;
					VY += (-174.09981f * Y - 5.805663f * VY) * num2;
					X += VX * num2;
					Y += VY * num2;
				}
				float num3 = (float)Math.Sqrt(X * X + Y * Y);
				if (num3 > 0.45f)
				{
					X *= 0.45f / num3;
					Y *= 0.45f / num3;
				}
			}

			public Mat3 Tilt()
			{
				float num = (float)Math.Sqrt(X * X + Y * Y);
				if (num < 1E-05f)
				{
					return Mat3.Identity;
				}
				return Mat3.Rotation(new Vec3((0f - Y) / num, X / num, 0f), num);
			}
		}

		public struct Shaft
		{
			public int Post;

			public Vec3 From;

			public Vec3 To;

			public Vec3 Dir;

			public double Loosed;

			public double Lands;

			public bool Stuck;

			public Vec3 Nock;

			public Vec3 Along;

			public float Length;
		}

		public sealed class Yard
		{
			public Vec3 Origin;

			public Mat3 Basis = Mat3.Identity;

			public bool Placed;

			public readonly Sway[] Sways = new Sway[3];

			public readonly List<Shaft> Shafts = new List<Shaft>();

			public readonly List<(int post, Vec3 from)> Struck = new List<(int, Vec3)>();

			public int Hits;

			private readonly MeshInstance[] _arrows = new MeshInstance[9];

			private MeshModel _partsOf;

			private readonly int[] _parts = new int[3] { -1, -1, -1 };

			private const int MaxShafts = 9;

			private const float ArrowSpeed = 26f;

			public float Scale => (float)Math.Sqrt(Vec3.Dot(Basis.C2, Basis.C2));

			public Vec3 Front
			{
				get
				{
					Vec3 vec = Basis.Mul(new Vec3(0f, -1f, 0f));
					vec = new Vec3(vec.X, vec.Y, 0f);
					if (!(Vec3.Dot(vec, vec) > 1E-08f))
					{
						return new Vec3(0f, -1f, 0f);
					}
					return vec.Normalized();
				}
			}

			public bool Moving
			{
				get
				{
					for (int i = 0; i < 3; i++)
					{
						if (Sways[i].Moving)
						{
							return true;
						}
					}
					return false;
				}
			}

			public void Place(MeshInstance inst)
			{
				Origin = inst.Position;
				Basis = inst.Basis;
				Placed = true;
			}

			public Vec3 Foot(int post)
			{
				return Origin + Basis.Mul(Posts[post]);
			}

			public Vec3 Straw(int post)
			{
				return Origin + Basis.Mul(Posts[post] + Sways[post].Tilt().Mul(new Vec3(0f, 0f, 0.92f)));
			}

			public Vec3 Spot(int slot, float reach, out int post)
			{
				int num = (slot % 6 + 6) % 6;
				post = ((num % 3 == 0) ? 1 : ((num % 3 != 1) ? 2 : 0));
				Vec3 vec = ((num < 3) ? Front : (Front * -1f));
				return Foot(post) + vec * (0.22f * Scale + reach);
			}

			public void Hit(int post, Vec3 from, float knock)
			{
				Vec3 a = Foot(post) - from;
				float num = Vec3.Dot(Basis.C0, Basis.C0);
				float num2 = Vec3.Dot(Basis.C1, Basis.C1);
				float dx = ((num > 1E-08f) ? (Vec3.Dot(a, Basis.C0) / num) : 0f);
				float dy = ((num2 > 1E-08f) ? (Vec3.Dot(a, Basis.C1) / num2) : 0f);
				Sways[post].Hit(dx, dy, knock);
				Struck.Add((post, from));
				if (Struck.Count > 12)
				{
					Struck.RemoveAt(0);
				}
				Hits++;
			}

			public void Loose(int post, Vec3 nock, float length, double now, uint seed)
			{
				float scale = Scale;
				float num = ((float)(seed & 0x3FF) / 1023f - 0.5f) * 0.28f * scale;
				float z = ((float)((seed >> 10) & 0x3FF) / 1023f - 0.5f) * 0.32f * scale;
				Vec3 vec = Vec3.Cross(Front, new Vec3(0f, 0f, 1f));
				Vec3 vec2 = Straw(post) + vec * num + new Vec3(0f, 0f, z);
				Vec3 vec3 = vec2 - nock;
				float num2 = (float)Math.Sqrt(Vec3.Dot(vec3, vec3));
				if (!(num2 < 0.05f))
				{
					if (Shafts.Count >= 9)
					{
						Shafts.RemoveAt(0);
					}
					Shafts.Add(new Shaft
					{
						Post = post,
						From = nock,
						To = vec2,
						Dir = vec3 * (1f / num2),
						Loosed = now,
						Lands = now + (double)(num2 / 26f),
						Length = length
					});
				}
			}

			public void Step(float dt, double now, float keep)
			{
				for (int i = 0; i < 3; i++)
				{
					Sways[i].Step(dt);
				}
				for (int num = Shafts.Count - 1; num >= 0; num--)
				{
					Shaft value = Shafts[num];
					if (!value.Stuck && now >= value.Lands)
					{
						Vec3 world = value.To + value.Dir * (value.Length * 0.35f) - value.Dir * value.Length;
						value.Nock = ToPost(value.Post, world);
						value.Along = ToPostDir(value.Post, value.Dir);
						value.Stuck = true;
						Hit(value.Post, value.From, Knock(WeaponKind.Bow));
						Shafts[num] = value;
					}
					else if (value.Stuck && now - value.Lands > (double)keep)
					{
						Shafts.RemoveAt(num);
					}
				}
			}

			private Vec3 ToPost(int post, Vec3 world)
			{
				Vec3 v = Inverse(world - Origin) - Posts[post];
				return Transpose(Sways[post].Tilt()).Mul(v);
			}

			private Vec3 ToPostDir(int post, Vec3 dir)
			{
				return Transpose(Sways[post].Tilt()).Mul(Inverse(dir)).Normalized();
			}

			private Vec3 FromPost(int post, Vec3 local)
			{
				return Origin + Basis.Mul(Posts[post] + Sways[post].Tilt().Mul(local));
			}

			private Vec3 FromPostDir(int post, Vec3 local)
			{
				Vec3 vec = Basis.Mul(Sways[post].Tilt().Mul(local));
				if (!(Vec3.Dot(vec, vec) > 1E-10f))
				{
					return vec;
				}
				return vec.Normalized();
			}

			private Vec3 Inverse(Vec3 w)
			{
				float num = Vec3.Dot(Basis.C0, Basis.C0);
				float num2 = Vec3.Dot(Basis.C1, Basis.C1);
				float num3 = Vec3.Dot(Basis.C2, Basis.C2);
				return new Vec3((num > 1E-10f) ? (Vec3.Dot(w, Basis.C0) / num) : 0f, (num2 > 1E-10f) ? (Vec3.Dot(w, Basis.C1) / num2) : 0f, (num3 > 1E-10f) ? (Vec3.Dot(w, Basis.C2) / num3) : 0f);
			}

			private static Mat3 Transpose(Mat3 r)
			{
				return new Mat3(new Vec3(r.C0.X, r.C1.X, r.C2.X), new Vec3(r.C0.Y, r.C1.Y, r.C2.Y), new Vec3(r.C0.Z, r.C1.Z, r.C2.Z));
			}

			public bool Pose(MeshInstance inst)
			{
				MeshModel model = inst.Model;
				if (model != _partsOf)
				{
					_partsOf = model;
					for (int i = 0; i < 3; i++)
					{
						_parts[i] = model?.PartIndex(PostParts[i]) ?? (-1);
					}
				}
				if ((_parts[0] < 0 && _parts[1] < 0 && _parts[2] < 0) || !Moving)
				{
					inst.PartRotation = null;
					inst.PartOffset = null;
					inst.PartScale = null;
					return false;
				}
				inst.EnsurePose();
				inst.ResetPose();
				for (int j = 0; j < 3; j++)
				{
					if (_parts[j] >= 0)
					{
						inst.PartRotation[_parts[j]] = Sways[j].Tilt();
					}
				}
				return true;
			}

			public void DrawShafts(List<MeshInstance> meshes, MeshModel arrow, double now, int tag, float sortDepth)
			{
				if (arrow == null)
				{
					return;
				}
				for (int i = 0; i < Shafts.Count && i < 9; i++)
				{
					Shaft shaft = Shafts[i];
					Vec3 nock;
					Vec3 dir;
					if (shaft.Stuck)
					{
						nock = FromPost(shaft.Post, shaft.Nock);
						dir = FromPostDir(shaft.Post, shaft.Along);
					}
					else
					{
						double num = shaft.Lands - shaft.Loosed;
						float num2 = ((num > 0.0001) ? ((float)Math.Max(0.0, Math.Min(1.0, (now - shaft.Loosed) / num))) : 1f);
						Vec3 vec = shaft.To - shaft.Dir * (shaft.Length * 0.65f);
						nock = shaft.From + (vec - shaft.From) * num2;
						dir = shaft.Dir;
					}
					MeshInstance meshInstance = _arrows[i] ?? (_arrows[i] = new MeshInstance());
					meshInstance.Model = arrow;
					meshInstance.Palette = null;
					Archer.PlaceArrow(meshInstance, nock, dir, shaft.Length, 1.3f);
					meshInstance.Tag = tag;
					meshInstance.SortDepth = sortDepth;
					meshInstance.NoShadow = true;
					meshInstance.Static = false;
					meshes.Add(meshInstance);
				}
			}
		}

		public static readonly Vec3[] Posts = new Vec3[3]
		{
			new Vec3(-1.1f, 0f, 0f),
			new Vec3(0f, 0.25f, 0f),
			new Vec3(1.1f, 0f, 0f)
		};

		public static readonly string[] PostParts = new string[3] { "dummy_0", "dummy_1", "dummy_2" };

		public const float PostRadius = 0.22f;

		public const float StrawMiddle = 0.92f;

		public static float StrikeSeconds(WeaponKind kind)
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
			default:
				return 0.4f;
			}
		}

		public static float ImpactAt(WeaponKind kind)
		{
			switch (kind)
			{
			case WeaponKind.Sword:
			case WeaponKind.Stick:
				return 0.465f;
			case WeaponKind.Dagger:
				return 0.465f;
			case WeaponKind.Axe:
			case WeaponKind.Hammer:
				return 0.51f;
			case WeaponKind.Spear:
				return 0.425f;
			case WeaponKind.Bow:
				return 0f;
			case WeaponKind.Staff:
				return 0.33f;
			default:
				return 0.24f;
			}
		}

		public static float Reach(WeaponKind kind)
		{
			switch (kind)
			{
			case WeaponKind.Sword:
			case WeaponKind.Stick:
				return 0.67f;
			case WeaponKind.Dagger:
				return 0.5f;
			case WeaponKind.Axe:
			case WeaponKind.Hammer:
				return 0.54f;
			case WeaponKind.Spear:
				return 0.91f;
			case WeaponKind.Staff:
				return 0.78f;
			case WeaponKind.Bow:
				return 3.4f;
			default:
				return 0.31f;
			}
		}

		public static float Knock(WeaponKind kind)
		{
			switch (kind)
			{
			case WeaponKind.Hammer:
				return 0.2f;
			case WeaponKind.Axe:
				return 0.16f;
			case WeaponKind.Spear:
				return 0.12f;
			case WeaponKind.Sword:
			case WeaponKind.Stick:
				return 0.11f;
			case WeaponKind.Bow:
				return 0.05f;
			case WeaponKind.Dagger:
				return 0.07f;
			default:
				return 0.08f;
			}
		}

		public static WeaponKind PoseOf(WeaponKind kind)
		{
			if (kind != WeaponKind.Staff)
			{
				return kind;
			}
			return WeaponKind.Spear;
		}

		public static float WindUp(WeaponKind kind)
		{
			return ImpactAt(kind) * StrikeSeconds(kind);
		}

		public static int StepsToBlow(int step)
		{
			if (step >= 4 && step <= 40 && step % 4 == 0)
			{
				return 1;
			}
			if (step >= 7 && step <= 39 && step % 4 == 3)
			{
				return 2;
			}
			return 0;
		}

		public static bool GuardStep(int step)
		{
			if (step >= 3)
			{
				return step < 44;
			}
			return false;
		}

		public static void Settle(PersonRig rig, MeshInstance m, WeaponKind kind, float attack, float footwork, float breather, float time, float seed)
		{
			if (kind == WeaponKind.Bow)
			{
				footwork = 0f;
			}
			float num = ((attack > 0f && attack < 1f) ? ((float)Math.Sin(Math.PI * (double)attack)) : 0f);
			Footwork(rig, m, footwork * (1f - 0.75f * num), time, seed);
			Breather(rig, m, breather, time, seed);
		}

		public static void Footwork(PersonRig rig, MeshInstance m, float k, float time, float seed)
		{
			if (!(k <= 0f))
			{
				m.EnsurePose();
				float num = time * 2f * MathF.PI * 1.6f + seed * 6.28f;
				float num2 = (float)Math.Sin(num);
				float num3 = (float)Math.Sin(num * 0.5f);
				if (rig.Hips >= 0)
				{
					m.PartOffset[rig.Hips] = m.PartOffset[rig.Hips] + new Vec3(0.012f * num3 * k, 0f, (-0.012f + 0.01f * num2) * k);
				}
				Mul(m, rig.LowerLegL, Mat3.RotX((0.07f + 0.06f * Math.Max(0f, num3)) * k));
				Mul(m, rig.LowerLegR, Mat3.RotX((0.07f + 0.06f * Math.Max(0f, 0f - num3)) * k));
				Mul(m, rig.Torso, Mat3.RotY(-0.035f * num3 * k));
				Mul(m, rig.Head, Mat3.RotY(0.03f * num3 * k));
				Mul(m, rig.UpperArmR, Mat3.RotX(0.04f * num2 * k));
				Mul(m, rig.UpperArmL, Mat3.RotX(0.04f * num2 * k));
			}
		}

		public static void Breather(PersonRig rig, MeshInstance m, float k, float time, float seed)
		{
			if (!(k <= 0f))
			{
				m.EnsurePose();
				float num = (float)Math.Sin(time * 2f * MathF.PI * 0.75f + seed * 6.28f);
				Mul(m, rig.Torso, Mat3.RotX((0.07f + 0.03f * num) * k));
				Mul(m, rig.Head, Mat3.RotX(0.1f * k));
				Mul(m, rig.UpperArmL, Mat3.RotY(-0.04f * num * k));
				Mul(m, rig.UpperArmR, Mat3.RotY(0.04f * num * k));
				if (rig.Hips >= 0)
				{
					m.PartOffset[rig.Hips] = m.PartOffset[rig.Hips] + new Vec3(0f, 0f, -0.01f * k);
				}
			}
		}

		private static void Mul(MeshInstance m, int part, Mat3 r)
		{
			if (part >= 0)
			{
				m.PartRotation[part] = m.PartRotation[part] * r;
			}
		}

		public static void Loosed(Archer.Bow bow, out Vec3 nock, out float length)
		{
			length = 0.55f * bow.Scale;
			nock = bow.Grip - bow.Ahead * (length * 0.85f);
		}
	}
}
