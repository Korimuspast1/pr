using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public struct HeadBox
	{
		private sealed class Shape
		{
			public int Part;

			public Vec3 Mid;

			public Vec3 Half;
		}

		public struct Snap
		{
			private struct Link
			{
				public int Part;

				public Mat3 Rot;

				public Vec3 Off;

				public float Scl;
			}

			private MeshModel _model;

			private Shape _shape;

			private Link[] _chain;

			private int _count;

			private bool _hasRot;

			private bool _hasOff;

			private bool _hasScl;

			private Mat3 _basis;

			private Vec3 _position;

			private bool _worked;

			private bool _has;

			private HeadBox _head;

			[ThreadStatic]
			private static MeshInstance _stand;

			[ThreadStatic]
			private static Mat3[] _standRot;

			[ThreadStatic]
			private static Vec3[] _standOff;

			[ThreadStatic]
			private static float[] _standScl;

			public bool Worked => _worked;

			public bool Take(MeshInstance body)
			{
				_worked = false;
				MeshModel meshModel = body?.Model;
				if (meshModel != _model)
				{
					_model = meshModel;
					_shape = ((meshModel != null) ? ShapeOf(meshModel) : null);
					_count = 0;
					if (_shape != null)
					{
						for (int num = _shape.Part; num >= 0; num = meshModel.Parts[num].Parent)
						{
							if (_chain == null)
							{
								_chain = new Link[6];
							}
							else if (_count == _chain.Length)
							{
								Array.Resize(ref _chain, _count * 2);
							}
							_chain[_count++].Part = num;
						}
					}
				}
				if (_shape == null)
				{
					return false;
				}
				Mat3[] partRotation = body.PartRotation;
				Vec3[] partOffset = body.PartOffset;
				float[] partScale = body.PartScale;
				_hasRot = partRotation != null;
				_hasOff = partOffset != null;
				_hasScl = partScale != null;
				Link[] chain = _chain;
				for (int i = 0; i < _count; i++)
				{
					int part = chain[i].Part;
					if (_hasRot)
					{
						chain[i].Rot = partRotation[part];
					}
					if (_hasOff)
					{
						chain[i].Off = partOffset[part];
					}
					if (_hasScl)
					{
						chain[i].Scl = partScale[part];
					}
				}
				_basis = body.Basis;
				_position = body.Position;
				return true;
			}

			public bool Get(out HeadBox head)
			{
				if (!_worked)
				{
					_worked = true;
					_has = false;
					_head = default(HeadBox);
					if (_shape != null)
					{
						MeshInstance meshInstance = _stand ?? (_stand = new MeshInstance());
						int num = _model.Parts.Length;
						if (_hasRot && (_standRot == null || _standRot.Length < num))
						{
							_standRot = new Mat3[num];
						}
						if (_hasOff && (_standOff == null || _standOff.Length < num))
						{
							_standOff = new Vec3[num];
						}
						if (_hasScl && (_standScl == null || _standScl.Length < num))
						{
							_standScl = new float[num];
						}
						for (int i = 0; i < _count; i++)
						{
							int part = _chain[i].Part;
							if (_hasRot)
							{
								_standRot[part] = _chain[i].Rot;
							}
							if (_hasOff)
							{
								_standOff[part] = _chain[i].Off;
							}
							if (_hasScl)
							{
								_standScl[part] = _chain[i].Scl;
							}
						}
						meshInstance.Model = _model;
						meshInstance.PartRotation = (_hasRot ? _standRot : null);
						meshInstance.PartOffset = (_hasOff ? _standOff : null);
						meshInstance.PartScale = (_hasScl ? _standScl : null);
						meshInstance.Basis = _basis;
						meshInstance.Position = _position;
						_has = Of(meshInstance, out _head);
						meshInstance.Model = null;
					}
				}
				head = _head;
				return _has;
			}
		}

		public Vec3 Centre;

		public Vec3 X;

		public Vec3 Y;

		public Vec3 Z;

		private const float Grow = 1.12f;

		private static readonly Dictionary<MeshModel, Shape> Shapes = new Dictionary<MeshModel, Shape>();

		public float Top => Centre.Z + Math.Abs(X.Z) + Math.Abs(Y.Z) + Math.Abs(Z.Z);

		public static bool Of(MeshInstance body, out HeadBox head)
		{
			head = default(HeadBox);
			MeshModel meshModel = body?.Model;
			if (meshModel == null)
			{
				return false;
			}
			Shape shape = ShapeOf(meshModel);
			if (shape == null)
			{
				return false;
			}
			Vec3 vec = PersonRig.Transform(body, shape.Part, shape.Mid);
			head = new HeadBox
			{
				Centre = vec,
				X = PersonRig.Transform(body, shape.Part, shape.Mid + new Vec3(shape.Half.X * 1.12f, 0f, 0f)) - vec,
				Y = PersonRig.Transform(body, shape.Part, shape.Mid + new Vec3(0f, shape.Half.Y * 1.12f, 0f)) - vec,
				Z = PersonRig.Transform(body, shape.Part, shape.Mid + new Vec3(0f, 0f, shape.Half.Z * 1.12f)) - vec
			};
			return true;
		}

		private static Shape ShapeOf(MeshModel m)
		{
			lock (Shapes)
			{
				if (Shapes.TryGetValue(m, out var value))
				{
					return value;
				}
				int num = m.PartIndex("head");
				if (num >= 0 && m.Parts[num].VertexCount > 0)
				{
					MeshPart meshPart = m.Parts[num];
					float[] pos = meshPart.Pos;
					int vertexCount = meshPart.VertexCount;
					Vec3 vec = new Vec3(0f, 0f, 0f);
					for (int i = 0; i < pos.Length; i += 3)
					{
						vec += new Vec3(pos[i], pos[i + 1], pos[i + 2]);
					}
					vec *= 1f / (float)vertexCount;
					Vec3 vec2 = vec - meshPart.Pivot;
					bool flag = Vec3.Dot(vec2, vec2) > 1E-08f;
					vec2 = vec2.Normalized();
					float num2 = 0f;
					if (flag)
					{
						for (int j = 0; j < pos.Length; j += 3)
						{
							num2 = Math.Max(num2, Vec3.Dot(new Vec3(pos[j], pos[j + 1], pos[j + 2]) - meshPart.Pivot, vec2));
						}
					}
					float num3 = (flag ? (num2 * 0.1f) : float.MinValue);
					Vec3 vec3 = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
					Vec3 vec4 = new Vec3(float.MinValue, float.MinValue, float.MinValue);
					int num4 = 0;
					for (int k = 0; k < pos.Length; k += 3)
					{
						Vec3 vec5 = new Vec3(pos[k], pos[k + 1], pos[k + 2]);
						if (!flag || !(Vec3.Dot(vec5 - meshPart.Pivot, vec2) < num3))
						{
							vec3 = new Vec3(Math.Min(vec3.X, vec5.X), Math.Min(vec3.Y, vec5.Y), Math.Min(vec3.Z, vec5.Z));
							vec4 = new Vec3(Math.Max(vec4.X, vec5.X), Math.Max(vec4.Y, vec5.Y), Math.Max(vec4.Z, vec5.Z));
							num4++;
						}
					}
					if (num4 > 0)
					{
						value = new Shape
						{
							Part = num,
							Mid = (vec3 + vec4) * 0.5f,
							Half = (vec4 - vec3) * 0.5f
						};
					}
				}
				Shapes[m] = value;
				return value;
			}
		}

		public HeadBox Moved(float dx, float dy, float dz)
		{
			return new HeadBox
			{
				Centre = Centre + new Vec3(dx, dy, dz),
				X = X,
				Y = Y,
				Z = Z
			};
		}

		public bool Segment(Vec3 a, Vec3 b, float grow, out float t)
		{
			t = 0f;
			float t2 = 0f;
			float t3 = 1f;
			Vec3 d = b - a;
			Vec3 o = a - Centre;
			if (!Slab(X, o, d, grow, ref t2, ref t3) || !Slab(Y, o, d, grow, ref t2, ref t3) || !Slab(Z, o, d, grow, ref t2, ref t3))
			{
				return false;
			}
			t = t2;
			return true;
		}

		private static bool Slab(Vec3 axis, Vec3 o, Vec3 d, float grow, ref float t0, ref float t1)
		{
			float num = (float)Math.Sqrt(Vec3.Dot(axis, axis));
			if (num < 1E-06f)
			{
				return false;
			}
			Vec3 b = axis * (1f / num);
			float num2 = num * (1f + grow);
			float num3 = Vec3.Dot(o, b);
			float num4 = Vec3.Dot(d, b);
			if (Math.Abs(num4) < 1E-07f)
			{
				return Math.Abs(num3) <= num2;
			}
			float num5 = (0f - num2 - num3) / num4;
			float num6 = (num2 - num3) / num4;
			if (num5 > num6)
			{
				float num7 = num5;
				num5 = num6;
				num6 = num7;
			}
			if (num5 > t0)
			{
				t0 = num5;
			}
			if (num6 < t1)
			{
				t1 = num6;
			}
			return t0 <= t1;
		}
	}
}
