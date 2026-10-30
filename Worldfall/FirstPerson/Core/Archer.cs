using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class Archer
	{
		public struct Bow
		{
			public Vec3 Grip;

			public Vec3 Ahead;

			public Vec3 Up;

			public float Scale;

			public float NockY;

			public float Flex;

			public bool Arrow;
		}

		private sealed class Shape
		{
			public Vec3 Anchor;

			public Vec3 Quiver;

			public Vec3 HandL;

			public Vec3 HandR;
		}

		public const float Turn = 1.15f;

		private static readonly Dictionary<MeshModel, Shape> Shapes = new Dictionary<MeshModel, Shape>();

		private static Shape ShapeOf(PersonRig rig)
		{
			if (Shapes.TryGetValue(rig.Model, out var value))
			{
				return value;
			}
			MeshModel model = rig.Model;
			Bounds(model.Parts[rig.Head], out var lo, out var hi);
			float num = Math.Max(0.12f, hi.Y - lo.Y);
			MeshPart meshPart = model.Parts[rig.UpperArmR];
			value = new Shape
			{
				Anchor = new Vec3(0.16f * num, lo.Y * 0.55f, lo.Z + 0.3f * (hi.Z - lo.Z)),
				Quiver = new Vec3(meshPart.Pivot.X * 0.6f, 0.12f, meshPart.Pivot.Z + 0.14f),
				HandL = model.Parts[rig.LowerArmL].Pivot + new Vec3(-0.004f, -0.012f, -0.3f),
				HandR = model.Parts[rig.LowerArmR].Pivot + new Vec3(0.004f, -0.012f, -0.3f)
			};
			if (Shapes.Count > 64)
			{
				Shapes.Clear();
			}
			Shapes[model] = value;
			return value;
		}

		private static void Bounds(MeshPart p, out Vec3 lo, out Vec3 hi)
		{
			lo = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
			hi = new Vec3(float.MinValue, float.MinValue, float.MinValue);
			for (int i = 0; i < p.VertexCount; i++)
			{
				float val = p.Pos[i * 3];
				float val2 = p.Pos[i * 3 + 1];
				float val3 = p.Pos[i * 3 + 2];
				lo = new Vec3(Math.Min(lo.X, val), Math.Min(lo.Y, val2), Math.Min(lo.Z, val3));
				hi = new Vec3(Math.Max(hi.X, val), Math.Max(hi.Y, val2), Math.Max(hi.Z, val3));
			}
			if (p.VertexCount == 0)
			{
				lo = (hi = p.Pivot);
			}
		}

		private static bool Rigged(PersonRig rig)
		{
			if (rig.Head >= 0 && rig.UpperArmL >= 0 && rig.LowerArmL >= 0 && rig.UpperArmR >= 0)
			{
				return rig.LowerArmR >= 0;
			}
			return false;
		}

		private static float Ease(float x)
		{
			x = ((x < 0f) ? 0f : ((x > 1f) ? 1f : x));
			return x * x * (3f - 2f * x);
		}

		private static Vec3 Lerp(Vec3 a, Vec3 b, float t)
		{
			return a + (b - a) * t;
		}

		private static float Length(Vec3 v)
		{
			return (float)Math.Sqrt(Vec3.Dot(v, v));
		}

		public static Bow Shoot(PersonRig rig, MeshInstance m, float stance, float attack, Vec3 aim, float height)
		{
			Bow result = Carry(rig, m, height);
			float num = Ease(stance);
			if (num <= 0.001f || !Rigged(rig))
			{
				return result;
			}
			m.EnsurePose();
			Shape shape = ShapeOf(rig);
			MeshModel model = rig.Model;
			float num2 = Length(m.Basis.C2);
			Vec3 vec = new Vec3(0f, 0f, 1f);
			Vec3 vec2 = m.Basis.Mul(new Vec3(1f, 0f, 0f)).Normalized();
			Vec3 a = PersonRig.Transform(m, rig.LowerArmL, shape.HandL);
			Vec3 a2 = PersonRig.Transform(m, rig.LowerArmR, shape.HandR);
			Look(rig, m, aim, num);
			Vec3 vec3 = PersonRig.Transform(m, rig.Head, shape.Anchor);
			MeshPart meshPart = model.Parts[rig.UpperArmL];
			MeshPart meshPart2 = model.Parts[rig.LowerArmL];
			Vec3 vec4 = PersonRig.Transform(m, rig.UpperArmL, meshPart.Pivot);
			float num3 = (Length(meshPart2.Pivot - meshPart.Pivot) + Length(shape.HandL - meshPart2.Pivot)) * num2 * 0.97f;
			Vec3 vec5 = vec3 - vec4;
			float num4 = Vec3.Dot(aim, vec5);
			float num5 = Vec3.Dot(vec5, vec5) - num3 * num3;
			float num6 = num4 * num4 - num5;
			Vec3 b = vec3 + aim * ((num6 > 0f) ? (0f - num4 + (float)Math.Sqrt(num6)) : Math.Max(0f, 0f - num4));
			Reach(m, rig.UpperArmL, rig.LowerArmL, shape.HandL, Lerp(a, b, num), vec * -1f + vec2 * -0.25f, num2);
			Vec3 grip = PersonRig.Transform(m, rig.LowerArmL, shape.HandL);
			Vec3 vec6 = grip - vec3;
			float full = Length(vec6);
			Vec3 ahead = ((full > 0.0001f) ? (vec6 * (1f / full)) : aim);
			float brace = (0f - ViewMeshes.BowTip.Y) * height;
			Vec3 vec7 = vec3 - ahead * (0.09f * num2) + vec2 * (0.04f * num2) + vec * (0.02f * num2);
			Vec3 vec8 = PersonRig.Transform(m, (rig.Torso >= 0) ? rig.Torso : rig.UpperArmR, shape.Quiver);
			float num7 = 1f;
			bool flag = true;
			Vec3 b2;
			if (attack <= 0f || attack >= 1f)
			{
				b2 = Nock(1f);
			}
			else if (attack < 0.12f)
			{
				num7 = 0f;
				flag = false;
				b2 = Lerp(Nock(1f), vec7, Ease(attack / 0.06f));
			}
			else if (attack < 0.4f)
			{
				num7 = 0f;
				flag = false;
				b2 = Lerp(vec7, vec8, Ease((attack - 0.12f) / 0.28f));
			}
			else if (attack < 0.6f)
			{
				num7 = 0f;
				flag = attack > 0.52f;
				b2 = Lerp(vec8, Nock(0f), Ease((attack - 0.4f) / 0.2f));
			}
			else
			{
				num7 = Ease((attack - 0.6f) / 0.32f);
				b2 = Nock(num7);
			}
			Vec3 vec9 = Vec3.Cross(ahead, vec);
			vec9 = ((Vec3.Dot(vec9, vec9) > 1E-06f) ? vec9.Normalized() : vec2);
			Reach(m, rig.UpperArmR, rig.LowerArmR, shape.HandR, Lerp(a2, b2, num), vec9 + ahead * -0.5f + vec * 0.1f, num2);
			Vec3 v = (vec - ahead * Vec3.Dot(vec, ahead)).Normalized();
			v = Vec3.Rotate(v, ahead, 0.14f);
			Vec3 vec10 = Lerp(result.Ahead, ahead, num).Normalized();
			Vec3 vec11 = Lerp(result.Up, v, num);
			vec11 = (vec11 - vec10 * Vec3.Dot(vec11, vec10)).Normalized();
			Vec3 vec12 = Nock(num7);
			float num8 = 0f - Math.Max(0f - ViewMeshes.BowTip.Y, Vec3.Dot(grip - vec12, ahead) / height);
			return new Bow
			{
				Grip = grip,
				Ahead = vec10,
				Up = vec11,
				Scale = height,
				NockY = result.NockY + (num8 - result.NockY) * num,
				Flex = num7 * num,
				Arrow = (flag && num > 0.6f)
			};
			Vec3 Nock(float d)
			{
				return grip - ahead * (brace + Math.Max(0f, full - brace) * d);
			}
		}

		public static Bow Carry(PersonRig rig, MeshInstance m, float height)
		{
			Vec3 vec = new Vec3(0f, 0f, 1f);
			Vec3 vec2 = m.Basis.Mul(new Vec3(0f, -1f, 0f)).Normalized();
			Vec3 grip = rig.LeftHandWorld(m);
			Vec3 vec3 = (((rig.LowerArmL >= 0) ? PersonRig.TransformDir(m, rig.LowerArmL, new Vec3(0f, -0.04f, -1f)).Normalized() : (vec * -1f)) * -0.6f + vec * 0.4f + vec2 * 0.12f).Normalized();
			Vec3 ahead = (vec2 - vec3 * Vec3.Dot(vec2, vec3)).Normalized();
			return new Bow
			{
				Grip = grip,
				Ahead = ahead,
				Up = vec3,
				Scale = height,
				NockY = ViewMeshes.BowTip.Y,
				Flex = 0f,
				Arrow = false
			};
		}

		public static Mat3 Basis(Bow bow)
		{
			return new Mat3(Vec3.Cross(bow.Ahead, bow.Up) * bow.Scale, bow.Ahead * bow.Scale, bow.Up * bow.Scale);
		}

		public static void Add(List<MeshInstance> meshes, Bow bow, WeaponLook look, MeshModel arrowModel, MeshInstance limbs, MeshInstance str, MeshInstance arrow, int detail = 0)
		{
			Mat3 basis = Basis(bow);
			limbs.Model = ViewMeshes.Bow(look, bow.Flex, detail);
			limbs.Palette = null;
			limbs.Basis = basis;
			limbs.Position = bow.Grip;
			meshes.Add(limbs);
			str.Model = ViewMeshes.BowString(bow.NockY, bow.Flex, bow.Arrow && arrowModel == null, look.Head, look.String, look.StringGlow);
			str.Palette = null;
			str.Basis = basis;
			str.Position = bow.Grip;
			meshes.Add(str);
			if (bow.Arrow && arrowModel != null)
			{
				Vec3 vec = Vec3.Cross(bow.Ahead, bow.Up);
				Vec3 nock = bow.Grip + (bow.Ahead * bow.NockY - vec * 0.012f + bow.Up * 0.008f) * bow.Scale;
				arrow.Model = arrowModel;
				arrow.Palette = null;
				PlaceArrow(arrow, nock, bow.Ahead, Math.Max(0.47f, 0.07f - bow.NockY) * bow.Scale, 1.3f);
				meshes.Add(arrow);
			}
		}

		public static void PlaceArrow(MeshInstance inst, Vec3 nock, Vec3 dir, float length, float thick = 1.6f)
		{
			Vec3 vec = dir.Normalized();
			Vec3 vec2 = Vec3.Cross(vec, new Vec3(0f, 0f, 1f));
			vec2 = ((Vec3.Dot(vec2, vec2) > 1E-06f) ? vec2.Normalized() : new Vec3(1f, 0f, 0f));
			Vec3 vec3 = Vec3.Cross(vec2, vec);
			inst.Basis = new Mat3(vec2 * ((0f - length) * thick), vec * (0f - length), vec3 * (length * thick));
			inst.Position = nock + vec * (length * 0.5f);
		}

		public static void Lodge(MeshInstance body, int part, Vec3 dir, float depth, float u, float v, float w, out Vec3 point, out Vec3 along)
		{
			Bounds(body.Model.Parts[part], out var lo, out var hi);
			Vec3 vec = (lo + hi) * 0.5f;
			Vec3 vec2 = new Vec3(Math.Max(0.001f, (hi.X - lo.X) * 0.5f), Math.Max(0.001f, (hi.Y - lo.Y) * 0.5f), Math.Max(0.001f, (hi.Z - lo.Z) * 0.5f));
			along = Unworld(body, part, dir);
			Vec3 vec3 = vec + new Vec3(vec2.X * 0.35f * u, vec2.Y * 0.35f * v, vec2.Z * 0.45f * w);
			Vec3 vec4 = new Vec3((vec3.X - vec.X) / vec2.X, (vec3.Y - vec.Y) / vec2.Y, (vec3.Z - vec.Z) / vec2.Z);
			Vec3 vec5 = new Vec3(along.X / vec2.X, along.Y / vec2.Y, along.Z / vec2.Z);
			float num = Vec3.Dot(vec5, vec5);
			float num2 = -2f * Vec3.Dot(vec4, vec5);
			float num3 = Vec3.Dot(vec4, vec4) - 1f;
			float num4 = num2 * num2 - 4f * num * num3;
			float num5 = ((num > 1E-12f && num4 > 0f) ? ((0f - num2 + (float)Math.Sqrt(num4)) / (2f * num)) : 0f);
			Vec3 vec6 = vec3 - along * num5;
			float num6 = Math.Max(0.0001f, Length(body.Basis.C2));
			point = vec6 + along * Math.Min(num5, depth / num6);
		}

		public static void PlaceLodged(MeshInstance arrow, MeshInstance body, int part, Vec3 point, Vec3 along, float length)
		{
			Vec3 vec = PersonRig.Transform(body, part, point);
			Vec3 vec2 = PersonRig.TransformDir(body, part, along).Normalized();
			PlaceArrow(arrow, vec - vec2 * length, vec2, length);
		}

		private static void Look(PersonRig rig, MeshInstance m, Vec3 aim, float k)
		{
			MeshPart meshPart = rig.Model.Parts[rig.Head];
			Vec3 vec = Unworld(m, meshPart.Parent, aim);
			float val = (float)Math.Atan2(vec.X, 0f - vec.Y);
			float val2 = 0f - (float)Math.Atan2(vec.Z, Math.Sqrt(vec.X * vec.X + vec.Y * vec.Y));
			val = Math.Max(-1.45f, Math.Min(1.45f, val));
			val2 = Math.Max(-0.6f, Math.Min(0.6f, val2));
			m.PartRotation[rig.Head] = Mat3.RotZ(val * k) * Mat3.RotX(val2 * k);
		}

		internal static void Reach(MeshInstance m, int upper, int lower, Vec3 hand, Vec3 target, Vec3 pole, float scale)
		{
			MeshModel model = m.Model;
			MeshPart meshPart = model.Parts[upper];
			MeshPart meshPart2 = model.Parts[lower];
			Vec3 v = meshPart2.Pivot - meshPart.Pivot;
			Vec3 v2 = hand - meshPart2.Pivot;
			float num = Length(v) * scale;
			float num2 = Length(v2) * scale;
			Vec3 vec = PersonRig.Transform(m, upper, meshPart.Pivot);
			Vec3 vec2 = target - vec;
			float num3 = Length(vec2);
			if (!(num3 < 1E-05f) && !(num < 1E-05f) && !(num2 < 1E-05f))
			{
				Vec3 vec3 = vec2 * (1f / num3);
				num3 = Math.Max(Math.Abs(num - num2) * 1.05f + 0.0001f, Math.Min((num + num2) * 0.999f, num3));
				float num4 = (num * num - num2 * num2 + num3 * num3) / (2f * num3);
				float num5 = (float)Math.Sqrt(Math.Max(0f, num * num - num4 * num4));
				Vec3 vec4 = pole - vec3 * Vec3.Dot(pole, vec3);
				if (Vec3.Dot(vec4, vec4) < 1E-08f)
				{
					vec4 = Vec3.Cross(vec3, new Vec3(0f, 0f, 1f));
				}
				vec4 = vec4.Normalized();
				Vec3 vec5 = vec + vec3 * num4 + vec4 * num5;
				m.PartRotation[upper] = Arc(v.Normalized(), Unworld(m, meshPart.Parent, vec5 - vec));
				Vec3 vec6 = vec + vec3 * num3;
				m.PartRotation[lower] = Arc(v2.Normalized(), Unworld(m, upper, vec6 - vec5));
			}
		}

		public static Vec3 Unworld(MeshInstance m, int part, Vec3 world)
		{
			Vec3 a = Frame(m, part, new Vec3(1f, 0f, 0f));
			Vec3 a2 = Frame(m, part, new Vec3(0f, 1f, 0f));
			return new Vec3(z: Vec3.Dot(Frame(m, part, new Vec3(0f, 0f, 1f)), world), x: Vec3.Dot(a, world), y: Vec3.Dot(a2, world)).Normalized();
		}

		private static Vec3 Frame(MeshInstance m, int part, Vec3 v)
		{
			if (part < 0)
			{
				return m.Basis.Mul(v);
			}
			return PersonRig.TransformDir(m, part, v);
		}

		private static Mat3 Arc(Vec3 a, Vec3 b)
		{
			Vec3 vec = Vec3.Cross(a, b);
			float num = Length(vec);
			float num2 = Vec3.Dot(a, b);
			if (num < 1E-06f)
			{
				if (num2 > 0f)
				{
					return Mat3.Identity;
				}
				Vec3 b2 = ((Math.Abs(a.X) < 0.9f) ? new Vec3(1f, 0f, 0f) : new Vec3(0f, 1f, 0f));
				return Mat3.Rotation(Vec3.Cross(a, b2).Normalized(), MathF.PI);
			}
			return Mat3.Rotation(vec * (1f / num), (float)Math.Atan2(num, num2));
		}
	}
}
