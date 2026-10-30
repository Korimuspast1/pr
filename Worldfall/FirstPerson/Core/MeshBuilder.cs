using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class MeshBuilder
	{
		private sealed class PartData
		{
			public string Name;

			public int Parent;

			public Vec3 Pivot;

			public readonly List<float> Pos = new List<float>();

			public readonly List<float> Nrm = new List<float>();

			public readonly List<int> Tri = new List<int>();

			public readonly List<byte> Slot = new List<byte>();
		}

		private readonly string _name;

		private readonly List<string> _slotNames = new List<string>();

		private readonly List<byte> _kinds = new List<byte>();

		private readonly List<byte> _flags = new List<byte>();

		private readonly List<uint> _colors = new List<uint>();

		private readonly List<PartData> _parts = new List<PartData>();

		private PartData _cur;

		public bool Mirror;

		public MeshBuilder(string name)
		{
			_name = name;
		}

		public int Slot(string name, byte kind, uint color, byte flags = 0)
		{
			_slotNames.Add(name);
			_kinds.Add(kind);
			_flags.Add(flags);
			_colors.Add(color);
			return _slotNames.Count - 1;
		}

		public int Part(string name, int parent, Vec3 pivot)
		{
			_cur = new PartData
			{
				Name = name,
				Parent = parent,
				Pivot = M(pivot)
			};
			_parts.Add(_cur);
			return _parts.Count - 1;
		}

		public void UsePart(int index)
		{
			_cur = _parts[index];
		}

		private Vec3 M(Vec3 v)
		{
			if (!Mirror)
			{
				return v;
			}
			return new Vec3(0f - v.X, v.Y, v.Z);
		}

		private int Vertex(Vec3 p, Vec3 n)
		{
			if (_cur == null)
			{
				Part("main", -1, new Vec3(0f, 0f, 0f));
			}
			p = M(p);
			n = M(n);
			_cur.Pos.Add(p.X);
			_cur.Pos.Add(p.Y);
			_cur.Pos.Add(p.Z);
			_cur.Nrm.Add(n.X);
			_cur.Nrm.Add(n.Y);
			_cur.Nrm.Add(n.Z);
			return _cur.Pos.Count / 3 - 1;
		}

		private void Triangle(int a, int b, int c, int slot)
		{
			if (Mirror)
			{
				_cur.Tri.Add(a);
				_cur.Tri.Add(c);
				_cur.Tri.Add(b);
			}
			else
			{
				_cur.Tri.Add(a);
				_cur.Tri.Add(b);
				_cur.Tri.Add(c);
			}
			_cur.Slot.Add((byte)slot);
		}

		public void Face(int slot, params Vec3[] p)
		{
			if (p.Length >= 3)
			{
				Vec3 vec = Vec3.Cross(p[1] - p[0], p[2] - p[0]);
				if (Vec3.Dot(vec, vec) < 1E-14f && p.Length > 3)
				{
					vec = Vec3.Cross(p[2] - p[0], p[3] - p[0]);
				}
				vec = vec.Normalized();
				int a = Vertex(p[0], vec);
				int b = Vertex(p[1], vec);
				for (int i = 2; i < p.Length; i++)
				{
					int num = Vertex(p[i], vec);
					Triangle(a, b, num, slot);
					b = num;
				}
			}
		}

		public void SmoothQuad(int slot, Vec3 a, Vec3 b, Vec3 c, Vec3 d, Vec3 na, Vec3 nb, Vec3 nc, Vec3 nd)
		{
			int a2 = Vertex(a, na.Normalized());
			int b2 = Vertex(b, nb.Normalized());
			int num = Vertex(c, nc.Normalized());
			int c2 = Vertex(d, nd.Normalized());
			Triangle(a2, b2, num, slot);
			Triangle(a2, num, c2, slot);
		}

		public void Quad(int slot, Vec3 c, Vec3 u, Vec3 v)
		{
			Face(slot, c - u - v, c + u - v, c + u + v, c - u + v);
		}

		public void Box(int slot, Vec3 c, Vec3 hx, Vec3 hy, Vec3 hz)
		{
			Quad(slot, c + hx, hy, hz);
			Quad(slot, c - hx, hz, hy);
			Quad(slot, c + hy, hz, hx);
			Quad(slot, c - hy, hx, hz);
			Quad(slot, c + hz, hx, hy);
			Quad(slot, c - hz, hy, hx);
		}

		public void Box(int slot, Vec3 c, float hx, float hy, float hz)
		{
			Box(slot, c, new Vec3(hx, 0f, 0f), new Vec3(0f, hy, 0f), new Vec3(0f, 0f, hz));
		}

		public void Loft(int slot, Vec3[] centres, float[] rx, float[] ry, Vec3 ax, Vec3 ay, int sides, bool smooth, float turn = 0f, bool caps = true)
		{
			int num = centres.Length;
			Vec3[,] array = new Vec3[num, sides];
			Vec3[,] array2 = new Vec3[num, sides];
			for (int i = 0; i < num; i++)
			{
				for (int j = 0; j < sides; j++)
				{
					float num2 = (float)((double)(j * 2) * Math.PI / (double)sides) + turn;
					float num3 = (float)Math.Cos(num2);
					float num4 = (float)Math.Sin(num2);
					array[i, j] = centres[i] + ax * (rx[i] * num3) + ay * (ry[i] * num4);
					array2[i, j] = (ax * (num3 / Math.Max(0.0001f, rx[i])) + ay * (num4 / Math.Max(0.0001f, ry[i]))).Normalized();
				}
			}
			for (int k = 0; k + 1 < num; k++)
			{
				for (int l = 0; l < sides; l++)
				{
					int num5 = (l + 1) % sides;
					Vec3 vec = array[k, l];
					Vec3 vec2 = array[k, num5];
					Vec3 vec3 = array[k + 1, num5];
					Vec3 vec4 = array[k + 1, l];
					if (smooth)
					{
						int a = Vertex(vec, array2[k, l]);
						int b = Vertex(vec2, array2[k, num5]);
						int num6 = Vertex(vec3, array2[k + 1, num5]);
						int c = Vertex(vec4, array2[k + 1, l]);
						if (rx[k] > 0f || ry[k] > 0f)
						{
							Triangle(a, b, num6, slot);
						}
						if (rx[k + 1] > 0f || ry[k + 1] > 0f)
						{
							Triangle(a, num6, c, slot);
						}
					}
					else if (rx[k + 1] <= 0f && ry[k + 1] <= 0f)
					{
						Face(slot, vec, vec2, vec3);
					}
					else if (rx[k] <= 0f && ry[k] <= 0f)
					{
						Face(slot, vec, vec3, vec4);
					}
					else
					{
						Face(slot, vec, vec2, vec3, vec4);
					}
				}
			}
			if (caps && (rx[0] > 0f || ry[0] > 0f))
			{
				Vec3[] array3 = new Vec3[sides];
				for (int m = 0; m < sides; m++)
				{
					array3[m] = array[0, sides - 1 - m];
				}
				Face(slot, array3);
			}
			if (caps && (rx[num - 1] > 0f || ry[num - 1] > 0f))
			{
				Vec3[] array4 = new Vec3[sides];
				for (int n = 0; n < sides; n++)
				{
					array4[n] = array[num - 1, n];
				}
				Face(slot, array4);
			}
		}

		public void Tube(int slot, Vec3 a, Vec3 b, float ra, float rb, int sides, bool smooth = true, float turn = 0f)
		{
			Vec3 vec = (b - a).Normalized();
			Vec3 vec2 = Vec3.Cross((Math.Abs(vec.Z) < 0.9f) ? new Vec3(0f, 0f, 1f) : new Vec3(1f, 0f, 0f), vec).Normalized();
			Vec3 ay = Vec3.Cross(vec, vec2);
			Loft(slot, new Vec3[2] { a, b }, new float[2] { ra, rb }, new float[2] { ra, rb }, vec2, ay, sides, smooth, turn);
		}

		public void Capsule(int slot, Vec3 a, Vec3 b, float ra, float rb, int sides = 6)
		{
			Vec3 vec = (b - a).Normalized();
			Vec3 vec2 = Vec3.Cross((Math.Abs(vec.Z) < 0.9f) ? new Vec3(0f, 0f, 1f) : new Vec3(1f, 0f, 0f), vec).Normalized();
			Vec3 ay = Vec3.Cross(vec, vec2);
			Vec3[] centres = new Vec3[6]
			{
				a - vec * (ra * 0.92f),
				a - vec * (ra * 0.6f),
				a,
				b,
				b + vec * (rb * 0.6f),
				b + vec * (rb * 0.92f)
			};
			float[] array = new float[6]
			{
				ra * 0.4f,
				ra * 0.8f,
				ra,
				rb,
				rb * 0.8f,
				rb * 0.4f
			};
			Loft(slot, centres, array, array, vec2, ay, sides, smooth: true);
		}

		public void Blob(int slot, Vec3 c, float hx, float hy, float hz, int sides = 8)
		{
			Vec3[] array = new Vec3[5];
			float[] array2 = new float[5];
			float[] array3 = new float[5];
			float[] array4 = new float[5] { -0.92f, -0.6f, 0f, 0.6f, 0.92f };
			for (int i = 0; i < 5; i++)
			{
				float num = (float)Math.Sqrt(1f - array4[i] * array4[i]);
				array[i] = c + new Vec3(0f, hy * array4[i], 0f);
				array2[i] = hz * num;
				array3[i] = hx * num;
			}
			Loft(slot, array, array2, array3, new Vec3(0f, 0f, 1f), new Vec3(1f, 0f, 0f), sides, smooth: true);
		}

		public void Sheet(int slot, Vec3[,] p)
		{
			int length = p.GetLength(0);
			int length2 = p.GetLength(1);
			if (length < 2 || length2 < 2)
			{
				return;
			}
			int[,] array = new int[length, length2];
			for (int i = 0; i < length; i++)
			{
				for (int j = 0; j < length2; j++)
				{
					Vec3 a = p[i, Math.Min(length2 - 1, j + 1)] - p[i, Math.Max(0, j - 1)];
					Vec3 b = p[Math.Min(length - 1, i + 1), j] - p[Math.Max(0, i - 1), j];
					Vec3 vec = Vec3.Cross(a, b);
					if (Vec3.Dot(vec, vec) < 1E-14f)
					{
						int num = Math.Min(i, length - 2);
						int num2 = Math.Min(j, length2 - 2);
						vec = Vec3.Cross(p[num, num2 + 1] - p[num, num2], p[num + 1, num2] - p[num, num2]);
						if (Vec3.Dot(vec, vec) < 1E-14f)
						{
							vec = Vec3.Cross(p[num + 1, num2 + 1] - p[num + 1, num2], p[num + 1, num2] - p[num, num2]);
						}
					}
					array[i, j] = Vertex(p[i, j], (Vec3.Dot(vec, vec) < 1E-14f) ? new Vec3(0f, 0f, 1f) : vec.Normalized());
				}
			}
			for (int k = 0; k + 1 < length; k++)
			{
				for (int l = 0; l + 1 < length2; l++)
				{
					int a2 = array[k, l];
					int b2 = array[k, l + 1];
					int num3 = array[k + 1, l + 1];
					int c = array[k + 1, l];
					if (Vec3.Dot(Vec3.Cross(p[k, l + 1] - p[k, l], p[k + 1, l + 1] - p[k, l]), Vec3.Cross(p[k, l + 1] - p[k, l], p[k + 1, l + 1] - p[k, l])) > 1E-16f)
					{
						Triangle(a2, b2, num3, slot);
					}
					if (Vec3.Dot(Vec3.Cross(p[k + 1, l + 1] - p[k, l], p[k + 1, l] - p[k, l]), Vec3.Cross(p[k + 1, l + 1] - p[k, l], p[k + 1, l] - p[k, l])) > 1E-16f)
					{
						Triangle(a2, num3, c, slot);
					}
				}
			}
		}

		public void Extrude(int slot, Vec3 origin, Vec3 u, Vec3 v, float half, params float[] outline)
		{
			int num = outline.Length / 2;
			Vec3 vec = Vec3.Cross(u, v).Normalized() * half;
			Vec3[] array = new Vec3[num];
			Vec3[] array2 = new Vec3[num];
			for (int i = 0; i < num; i++)
			{
				Vec3 vec2 = origin + u * outline[i * 2] + v * outline[i * 2 + 1];
				array[i] = vec2 + vec;
				array2[num - 1 - i] = vec2 - vec;
			}
			Face(slot, array);
			Face(slot, array2);
			for (int j = 0; j < num; j++)
			{
				int num2 = (j + 1) % num;
				Face(slot, array[j] - vec * 2f, array[num2] - vec * 2f, array[num2], array[j]);
			}
		}

		public bool CastOut(Vec3 from, Vec3 dir, out Vec3 hit, out Vec3 normal)
		{
			hit = (normal = new Vec3(0f, 0f, 0f));
			if (_cur == null)
			{
				return false;
			}
			from = M(from);
			dir = M(dir);
			float num = -1f;
			List<float> pos = _cur.Pos;
			List<int> tri = _cur.Tri;
			for (int i = 0; i + 2 < tri.Count; i += 3)
			{
				int num2 = tri[i] * 3;
				int num3 = tri[i + 1] * 3;
				int num4 = tri[i + 2] * 3;
				Vec3 vec = new Vec3(pos[num2], pos[num2 + 1], pos[num2 + 2]);
				Vec3 vec2 = new Vec3(pos[num3], pos[num3 + 1], pos[num3 + 2]) - vec;
				Vec3 vec3 = new Vec3(pos[num4], pos[num4 + 1], pos[num4 + 2]) - vec;
				Vec3 b = Vec3.Cross(dir, vec3);
				float num5 = Vec3.Dot(vec2, b);
				if (Math.Abs(num5) < 1E-12f)
				{
					continue;
				}
				float num6 = 1f / num5;
				Vec3 a = from - vec;
				float num7 = Vec3.Dot(a, b) * num6;
				if (num7 < 0f || num7 > 1f)
				{
					continue;
				}
				Vec3 b2 = Vec3.Cross(a, vec2);
				float num8 = Vec3.Dot(dir, b2) * num6;
				if (!(num8 < 0f) && !(num7 + num8 > 1f))
				{
					float num9 = Vec3.Dot(vec3, b2) * num6;
					if (!(num9 <= 0f) && !(num9 < num))
					{
						num = num9;
						normal = Vec3.Cross(vec2, vec3).Normalized();
					}
				}
			}
			if (num < 0f)
			{
				return false;
			}
			if (Vec3.Dot(normal, dir) < 0f)
			{
				normal *= -1f;
			}
			hit = M(from + dir * num);
			normal = M(normal);
			return true;
		}

		public MeshModel Build()
		{
			MeshModel meshModel = new MeshModel
			{
				Name = _name,
				SlotNames = _slotNames.ToArray(),
				SlotKind = _kinds.ToArray(),
				SlotFlags = _flags.ToArray(),
				SlotColor = _colors.ToArray(),
				Parts = new MeshPart[_parts.Count]
			};
			Vec3 min = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
			Vec3 max = new Vec3(float.MinValue, float.MinValue, float.MinValue);
			for (int i = 0; i < _parts.Count; i++)
			{
				PartData partData = _parts[i];
				MeshPart meshPart = new MeshPart
				{
					Name = partData.Name,
					Parent = partData.Parent,
					Pivot = partData.Pivot,
					Pos = partData.Pos.ToArray(),
					Nrm = partData.Nrm.ToArray(),
					Tri = partData.Tri.ToArray(),
					Slot = partData.Slot.ToArray()
				};
				meshPart.Axis = new byte[meshPart.TriangleCount];
				for (int j = 0; j < meshPart.TriangleCount; j++)
				{
					meshPart.Axis[j] = DominantAxis(meshPart, j);
				}
				for (int k = 0; k < meshPart.VertexCount; k++)
				{
					float val = meshPart.Pos[k * 3];
					float val2 = meshPart.Pos[k * 3 + 1];
					float val3 = meshPart.Pos[k * 3 + 2];
					min = new Vec3(Math.Min(min.X, val), Math.Min(min.Y, val2), Math.Min(min.Z, val3));
					max = new Vec3(Math.Max(max.X, val), Math.Max(max.Y, val2), Math.Max(max.Z, val3));
				}
				meshModel.Parts[i] = meshPart;
			}
			meshModel.Min = min;
			meshModel.Max = max;
			return meshModel;
		}

		private static byte DominantAxis(MeshPart p, int t)
		{
			int num = p.Tri[t * 3] * 3;
			int num2 = p.Tri[t * 3 + 1] * 3;
			int num3 = p.Tri[t * 3 + 2] * 3;
			float num4 = p.Pos[num2] - p.Pos[num];
			float num5 = p.Pos[num2 + 1] - p.Pos[num + 1];
			float num6 = p.Pos[num2 + 2] - p.Pos[num + 2];
			float num7 = p.Pos[num3] - p.Pos[num];
			float num8 = p.Pos[num3 + 1] - p.Pos[num + 1];
			float num9 = p.Pos[num3 + 2] - p.Pos[num + 2];
			float num10 = Math.Abs(num5 * num9 - num6 * num8);
			float num11 = Math.Abs(num6 * num7 - num4 * num9);
			float num12 = Math.Abs(num4 * num8 - num5 * num7);
			return (byte)((!(num10 >= num11) || !(num10 >= num12)) ? ((num11 >= num12) ? 1u : 2u) : 0u);
		}
	}
}
