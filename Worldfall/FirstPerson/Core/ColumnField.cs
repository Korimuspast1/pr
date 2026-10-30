using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FirstPerson.Core
{
	public sealed class ColumnField
	{
		public float X0;

		public float Y0;

		public float Cell;

		public int W;

		public int H;

		public float Reach;

		public const sbyte Top = 1;

		public const sbyte Steep = 2;

		public const sbyte Ramp = 3;

		public const sbyte Bottom = -1;

		public const sbyte SteepBottom = -2;

		public const int Need = 4;

		private const float Standable = 0.8f;

		private const int DefaultFaces = 14;

		private int MaxFaces = 14;

		private float[] _z;

		private sbyte[] _kind;

		private byte[] _what;

		private byte[] _count;

		private int[] _start;

		private readonly List<(Vec3 A, Vec3 B, Vec3 C, Vec3 D, float Lo, float Hi)> _ramps = new List<(Vec3, Vec3, Vec3, Vec3, float, float)>();

		private readonly List<(Vec3 A, Vec3 B, Vec3 C, Vec3 D, float Lo, float Hi)> _clears = new List<(Vec3, Vec3, Vec3, Vec3, float, float)>();

		[ThreadStatic]
		private static float[] _sp;

		[ThreadStatic]
		private static sbyte[] _sk;

		[ThreadStatic]
		private static byte[] _sw;

		[ThreadStatic]
		private static float[] _px;

		[ThreadStatic]
		private static float[] _py;

		[ThreadStatic]
		private static float[] _tops;

		[ThreadStatic]
		private static byte[] _whats;

		private static readonly float[] OffX = new float[9] { 0f, 1f, 0.7071f, 0f, -0.7071f, -1f, -0.7071f, 0f, 0.7071f };

		private static readonly float[] OffY = new float[9] { 0f, 0f, 0.7071f, 1f, 0.7071f, 0f, -0.7071f, -1f, -0.7071f };

		public int Faces => _z.Length;

		public static ColumnField OfModel(MeshModel model, float cell = 0.1f)
		{
			if (model == null)
			{
				return null;
			}
			ColumnField columnField = new ColumnField();
			columnField.Init(model.Min.X - cell * 2f, model.Min.Y - cell * 2f, model.Max.X + cell * 2f, model.Max.Y + cell * 2f, cell);
			columnField.Add(model, new Vec3(0f, 0f, 0f), Mat3.Identity, 0uL);
			return columnField.Finish();
		}

		public static ColumnField Empty(float x0, float y0, float x1, float y1, float cell, int faces = 14)
		{
			ColumnField columnField = new ColumnField();
			columnField.MaxFaces = faces;
			columnField.Init(x0, y0, x1, y1, cell);
			return columnField;
		}

		private void Init(float x0, float y0, float x1, float y1, float cell)
		{
			X0 = x0;
			Y0 = y0;
			Cell = cell;
			W = Math.Max(1, (int)Math.Ceiling((x1 - x0) / cell));
			H = Math.Max(1, (int)Math.Ceiling((y1 - y0) / cell));
			_z = new float[W * H * MaxFaces];
			_kind = new sbyte[W * H * MaxFaces];
			_what = new byte[W * H * MaxFaces];
			_count = new byte[W * H];
		}

		private static bool Skip(MeshModel m, int slot)
		{
			if (slot < 0 || slot >= m.SlotKind.Length)
			{
				return true;
			}
			byte b = m.SlotKind[slot];
			if (b == 11 || b == 12 || b == 1)
			{
				return true;
			}
			return (m.SlotFlags[slot] & 1) != 0;
		}

		public void Add(MeshModel model, Vec3 at, Mat3 basis, ulong hidden)
		{
			int flip = ((!(Vec3.Dot(basis.C0, Vec3.Cross(basis.C1, basis.C2)) < 0f)) ? 1 : (-1));
			for (int i = 0; i < model.Parts.Length; i++)
			{
				if (i < 64 && (hidden & (ulong)(1L << i)) != 0L)
				{
					continue;
				}
				MeshPart meshPart = model.Parts[i];
				float[] pos = meshPart.Pos;
				int[] tri = meshPart.Tri;
				for (int j = 0; j < meshPart.TriangleCount; j++)
				{
					int num = meshPart.Slot[j];
					if (!Skip(model, num))
					{
						int num2 = tri[j * 3] * 3;
						int num3 = tri[j * 3 + 1] * 3;
						int num4 = tri[j * 3 + 2] * 3;
						Vec3 a = at + basis.Mul(new Vec3(pos[num2], pos[num2 + 1], pos[num2 + 2]));
						Vec3 b = at + basis.Mul(new Vec3(pos[num3], pos[num3 + 1], pos[num3 + 2]));
						Vec3 c = at + basis.Mul(new Vec3(pos[num4], pos[num4 + 1], pos[num4 + 2]));
						Triangle(a, b, c, flip, 0, model.SlotKind[num]);
					}
				}
			}
			string text = model.Info("ramp");
			if (!string.IsNullOrEmpty(text))
			{
				Ramps(text, at, basis);
			}
		}

		private void Triangle(Vec3 a, Vec3 b, Vec3 c, int flip, sbyte force, byte what)
		{
			float num = b.X - a.X;
			float num2 = b.Y - a.Y;
			float num3 = b.Z - a.Z;
			float num4 = c.X - a.X;
			float num5 = c.Y - a.Y;
			float num6 = c.Z - a.Z;
			float num7 = num2 * num6 - num3 * num5;
			float num8 = num3 * num4 - num * num6;
			float num9 = (num * num5 - num2 * num4) * (float)flip;
			float num10 = (float)Math.Sqrt(num7 * num7 + num8 * num8 + num9 * num9);
			if (num10 < 1E-12f)
			{
				return;
			}
			float num11 = num * num5 - num2 * num4;
			if (Math.Abs(num11) < 1E-12f)
			{
				return;
			}
			sbyte b2 = force;
			if (b2 == 0)
			{
				float num12 = num9 / num10;
				if (num12 >= 0.8f)
				{
					b2 = 1;
				}
				else if (num12 > 0.35f)
				{
					b2 = 2;
				}
				else if (num12 <= -0.8f)
				{
					b2 = -1;
				}
				else
				{
					if (!(num12 < -0.35f))
					{
						return;
					}
					b2 = -2;
				}
			}
			int num13 = Math.Max(0, (int)Math.Floor((Math.Min(a.X, Math.Min(b.X, c.X)) - X0) / Cell - 0.5f));
			int num14 = Math.Min(W - 1, (int)Math.Floor((Math.Max(a.X, Math.Max(b.X, c.X)) - X0) / Cell + 0.5f));
			int num15 = Math.Max(0, (int)Math.Floor((Math.Min(a.Y, Math.Min(b.Y, c.Y)) - Y0) / Cell - 0.5f));
			int num16 = Math.Min(H - 1, (int)Math.Floor((Math.Max(a.Y, Math.Max(b.Y, c.Y)) - Y0) / Cell + 0.5f));
			float num17 = Cell * 0.45f;
			float num18 = ((num11 > 0f) ? 1f : (-1f));
			float val = Math.Min(a.Z, Math.Min(b.Z, c.Z));
			float val2 = Math.Max(a.Z, Math.Max(b.Z, c.Z));
			for (int i = num15; i <= num16; i++)
			{
				for (int j = num13; j <= num14; j++)
				{
					float num19 = X0 + ((float)j + 0.5f) * Cell;
					float num20 = Y0 + ((float)i + 0.5f) * Cell;
					if (!(num18 * EdgeDist(a, b, num19, num20) < 0f - num17) && !(num18 * EdgeDist(b, c, num19, num20) < 0f - num17) && !(num18 * EdgeDist(c, a, num19, num20) < 0f - num17))
					{
						float num21 = ((num19 - a.X) * num5 - (num20 - a.Y) * num4) / num11;
						float num22 = (num * (num20 - a.Y) - num2 * (num19 - a.X)) / num11;
						float z = Math.Max(val, Math.Min(val2, a.Z + num3 * num21 + num6 * num22));
						Insert(i * W + j, z, b2, what);
						Reach = Math.Max(Reach, (float)Math.Sqrt(num19 * num19 + num20 * num20));
					}
				}
			}
		}

		private static float EdgeDist(Vec3 a, Vec3 b, float px, float py)
		{
			float num = b.X - a.X;
			float num2 = b.Y - a.Y;
			float num3 = (float)Math.Sqrt(num * num + num2 * num2);
			if (num3 < 1E-09f)
			{
				return 0f;
			}
			return (num * (py - a.Y) - num2 * (px - a.X)) / num3;
		}

		private void Insert(int cell, float z, sbyte kind, byte what)
		{
			int num = _count[cell];
			int num2 = cell * MaxFaces;
			bool flag = kind > 0;
			for (int i = 0; i < num; i++)
			{
				if (_kind[num2 + i] > 0 == flag && Math.Abs(_z[num2 + i] - z) < 0.001f)
				{
					if (kind == 1 || kind == 3 || kind == -1)
					{
						_kind[num2 + i] = kind;
					}
					return;
				}
			}
			if (num == MaxFaces)
			{
				if (z >= _z[num2 + num - 1])
				{
					return;
				}
				num--;
			}
			int num3 = num;
			while (num3 > 0 && (_z[num2 + num3 - 1] > z || (_z[num2 + num3 - 1] == z && _kind[num2 + num3 - 1] > kind)))
			{
				_z[num2 + num3] = _z[num2 + num3 - 1];
				_kind[num2 + num3] = _kind[num2 + num3 - 1];
				_what[num2 + num3] = _what[num2 + num3 - 1];
				num3--;
			}
			_z[num2 + num3] = z;
			_kind[num2 + num3] = kind;
			_what[num2 + num3] = what;
			_count[cell] = (byte)(num + 1);
		}

		private void Ramps(string info, Vec3 at, Mat3 basis)
		{
			string[] array = info.Split('/');
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(',');
				if (array2.Length < 7)
				{
					continue;
				}
				float[] array3 = new float[9] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0.6f, 0f };
				bool flag = true;
				for (int j = 0; j < Math.Min(9, array2.Length); j++)
				{
					flag &= float.TryParse(array2[j], NumberStyles.Float, CultureInfo.InvariantCulture, out array3[j]);
				}
				if (!flag)
				{
					continue;
				}
				float num = array3[3] - array3[0];
				float num2 = array3[4] - array3[1];
				float num3 = (float)Math.Sqrt(num * num + num2 * num2);
				if (!(num3 < 1E-06f))
				{
					float num4 = (0f - num2) / num3 * array3[6] * 0.5f;
					float num5 = num / num3 * array3[6] * 0.5f;
					Vec3 vec = P(array3[0] + num4, array3[1] + num5, array3[2]);
					Vec3 vec2 = P(array3[0] - num4, array3[1] - num5, array3[2]);
					Vec3 item = P(array3[3] - num4, array3[4] - num5, array3[5]);
					Vec3 item2 = P(array3[3] + num4, array3[4] + num5, array3[5]);
					float item3 = Math.Min(vec.Z, item.Z) + Cell * 0.3f;
					float item4 = Math.Max(vec.Z, item.Z) + array3[7] * Math.Abs(basis.C2.Z);
					_ramps.Add((vec, vec2, item, item2, item3, item4));
					if (array3[8] > 0f)
					{
						float num6 = (0f - num) / num3 * array3[8];
						float num7 = (0f - num2) / num3 * array3[8];
						Vec3 item5 = P(array3[0] + num6 + num4, array3[1] + num7 + num5, array3[2]);
						Vec3 item6 = P(array3[0] + num6 - num4, array3[1] + num7 - num5, array3[2]);
						_clears.Add((item5, item6, vec2, vec, item3, item4));
					}
				}
			}
			Vec3 P(float x, float y, float z)
			{
				return at + basis.Mul(new Vec3(x, y, z));
			}
		}

		private void LayRamps()
		{
			foreach (var clear in _clears)
			{
				ClearStrip(clear.A, clear.B, clear.C, clear.D, clear.Lo, clear.Hi);
			}
			_clears.Clear();
			foreach (var ramp in _ramps)
			{
				ClearStrip(ramp.A, ramp.B, ramp.C, ramp.D, ramp.Lo, ramp.Hi);
				Triangle(ramp.A, ramp.B, ramp.C, 1, 3, 3);
				Triangle(ramp.A, ramp.C, ramp.D, 1, 3, 3);
			}
			_ramps.Clear();
		}

		private void ClearStrip(Vec3 a, Vec3 b, Vec3 c, Vec3 d, float lo, float hi)
		{
			float num = Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X));
			float num2 = Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X));
			float num3 = Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y));
			float num4 = Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y));
			int num5 = Math.Max(0, (int)Math.Floor((num - X0) / Cell));
			int num6 = Math.Min(W - 1, (int)Math.Floor((num2 - X0) / Cell));
			int num7 = Math.Max(0, (int)Math.Floor((num3 - Y0) / Cell));
			int num8 = Math.Min(H - 1, (int)Math.Floor((num4 - Y0) / Cell));
			float num9 = (((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X) > 0f) ? 1f : (-1f));
			for (int i = num7; i <= num8; i++)
			{
				for (int j = num5; j <= num6; j++)
				{
					float px = X0 + ((float)j + 0.5f) * Cell;
					float py = Y0 + ((float)i + 0.5f) * Cell;
					if (num9 * EdgeDist(a, b, px, py) < 0f || num9 * EdgeDist(b, c, px, py) < 0f || num9 * EdgeDist(c, d, px, py) < 0f || num9 * EdgeDist(d, a, px, py) < 0f)
					{
						continue;
					}
					int num10 = i * W + j;
					int num11 = num10 * MaxFaces;
					int num12 = _count[num10];
					int num13 = 0;
					for (int k = 0; k < num12; k++)
					{
						if (!(_z[num11 + k] > lo) || !(_z[num11 + k] <= hi))
						{
							_z[num11 + num13] = _z[num11 + k];
							_kind[num11 + num13] = _kind[num11 + k];
							_what[num11 + num13] = _what[num11 + k];
							num13++;
						}
					}
					_count[num10] = (byte)num13;
				}
			}
		}

		public ColumnField Finish()
		{
			LayRamps();
			int num = 0;
			for (int i = 0; i < _count.Length; i++)
			{
				num += _count[i];
			}
			if (num == 0)
			{
				return null;
			}
			float[] array = new float[num];
			sbyte[] array2 = new sbyte[num];
			byte[] array3 = new byte[num];
			_start = new int[_count.Length + 1];
			int num2 = 0;
			for (int j = 0; j < _count.Length; j++)
			{
				_start[j] = num2;
				int num3 = j * MaxFaces;
				int num4 = 0;
				while (num4 < _count[j])
				{
					array[num2] = _z[num3 + num4];
					array2[num2] = _kind[num3 + num4];
					array3[num2] = _what[num3 + num4];
					num4++;
					num2++;
				}
			}
			_start[_count.Length] = num2;
			_z = array;
			_kind = array2;
			_what = array3;
			_count = null;
			return this;
		}

		private int CellAt(float x, float y)
		{
			int num = (int)Math.Floor((x - X0) / Cell);
			int num2 = (int)Math.Floor((y - Y0) / Cell);
			if (num < 0 || num2 < 0 || num >= W || num2 >= H)
			{
				return -1;
			}
			return num2 * W + num;
		}

		private int Spans(int cell, float[] sp, sbyte[] sk, byte[] sw)
		{
			int num = _start[cell];
			int num2 = _start[cell + 1];
			int num3 = 0;
			int num4 = 0;
			int num5 = -1;
			float num6 = float.NegativeInfinity;
			float num7 = Cell * 2f;
			for (int i = num; i < num2; i++)
			{
				float num8 = _z[i];
				if (_kind[i] < 0)
				{
					if (num4 == 0)
					{
						num5 = i;
					}
					num4++;
					continue;
				}
				if (num3 >= sk.Length)
				{
					break;
				}
				float num9 = ((num4 > 0) ? _z[num5] : ((_kind[i] == 2) ? Math.Max(num6, num8 - num7) : num6));
				num4 = 0;
				if (num3 > 0 && num9 <= num6 + Cell * 0.25f)
				{
					sp[num3 * 2 - 1] = Math.Max(num6, num8);
					sk[num3 - 1] = _kind[i];
					sw[num3 - 1] = _what[i];
					num6 = sp[num3 * 2 - 1];
				}
				else
				{
					sp[num3 * 2] = num9;
					sp[num3 * 2 + 1] = num8;
					sk[num3] = _kind[i];
					sw[num3] = _what[i];
					num3++;
					num6 = num8;
				}
			}
			if (num4 > 0)
			{
				for (int j = num5; j < num2; j++)
				{
					if (num3 >= sk.Length)
					{
						break;
					}
					if (_kind[j] == -1)
					{
						sp[num3 * 2] = _z[j];
						sp[num3 * 2 + 1] = float.PositiveInfinity;
						sk[num3] = -1;
						sw[num3] = 0;
						num3++;
						break;
					}
					sp[num3 * 2] = _z[j];
					sp[num3 * 2 + 1] = _z[j] + num7;
					sk[num3] = -2;
					sw[num3] = 0;
					num3++;
				}
			}
			return num3;
		}

		private int SpansAt(int cell)
		{
			if (_sp == null || _sk.Length < MaxFaces + 1)
			{
				_sp = new float[MaxFaces * 2 + 2];
				_sk = new sbyte[MaxFaces + 1];
				_sw = new byte[MaxFaces + 1];
			}
			return Spans(cell, _sp, _sk, _sw);
		}

		public bool TopAt(float x, float y, float maxZ, out float z, out byte what)
		{
			bool ramp;
			return TopAt(x, y, maxZ, out z, out what, out ramp);
		}

		public bool TopAt(float x, float y, float maxZ, out float z, out byte what, out bool ramp)
		{
			z = float.NegativeInfinity;
			what = 0;
			ramp = false;
			int num = CellAt(x, y);
			if (num < 0 || _start[num] == _start[num + 1])
			{
				return false;
			}
			int num2 = SpansAt(num);
			for (int i = 0; i < num2; i++)
			{
				float num3 = _sp[i * 2 + 1];
				if ((_sk[i] == 1 || _sk[i] == 3) && num3 <= maxZ && num3 > z)
				{
					z = num3;
					what = _sw[i];
					ramp = _sk[i] == 3;
				}
			}
			return !float.IsNegativeInfinity(z);
		}

		public bool Blocked(float x, float y, float r, float z0, float z1)
		{
			int num = Math.Max(0, (int)Math.Floor((x - r - X0) / Cell));
			int num2 = Math.Min(W - 1, (int)Math.Floor((x + r - X0) / Cell));
			int num3 = Math.Max(0, (int)Math.Floor((y - r - Y0) / Cell));
			int num4 = Math.Min(H - 1, (int)Math.Floor((y + r - Y0) / Cell));
			float num5 = r * r;
			for (int i = num3; i <= num4; i++)
			{
				for (int j = num; j <= num2; j++)
				{
					int num6 = i * W + j;
					if (_start[num6] == _start[num6 + 1])
					{
						continue;
					}
					float num7 = X0 + ((float)j + 0.5f) * Cell - x;
					float num8 = Y0 + ((float)i + 0.5f) * Cell - y;
					if (num7 * num7 + num8 * num8 > num5)
					{
						continue;
					}
					int num9 = SpansAt(num6);
					for (int k = 0; k < num9; k++)
					{
						if (_sk[k] != 3 && _sp[k * 2] < z1 && _sp[k * 2 + 1] > z0)
						{
							return true;
						}
					}
				}
			}
			return false;
		}

		public string Dump(float x, float y)
		{
			int num = CellAt(x, y);
			if (num < 0)
			{
				return "outside";
			}
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = _start[num]; i < _start[num + 1]; i++)
			{
				stringBuilder.Append(string.Format(CultureInfo.InvariantCulture, "{0:F2}{1} ", _z[i], (_kind[i] < 0) ? "v" : ((_kind[i] == 1) ? "^" : ((_kind[i] == 2) ? "/" : "r"))));
			}
			return stringBuilder.ToString();
		}

		public string WhatBlocks(float x, float y, float r, float z0, float z1)
		{
			int num = Math.Max(0, (int)Math.Floor((x - r - X0) / Cell));
			int num2 = Math.Min(W - 1, (int)Math.Floor((x + r - X0) / Cell));
			int num3 = Math.Max(0, (int)Math.Floor((y - r - Y0) / Cell));
			int num4 = Math.Min(H - 1, (int)Math.Floor((y + r - Y0) / Cell));
			for (int i = num3; i <= num4; i++)
			{
				for (int j = num; j <= num2; j++)
				{
					int num5 = i * W + j;
					if (_start[num5] == _start[num5 + 1])
					{
						continue;
					}
					float num6 = X0 + ((float)j + 0.5f) * Cell - x;
					float num7 = Y0 + ((float)i + 0.5f) * Cell - y;
					if (num6 * num6 + num7 * num7 > r * r)
					{
						continue;
					}
					int num8 = SpansAt(num5);
					for (int k = 0; k < num8; k++)
					{
						if (_sk[k] != 3 && _sp[k * 2] < z1 && _sp[k * 2 + 1] > z0)
						{
							return string.Format(CultureInfo.InvariantCulture, "({0:F2},{1:F2}) {2:F2}..{3:F2} kind {4}", num6, num7, _sp[k * 2], _sp[k * 2 + 1], _sk[k]);
						}
					}
				}
			}
			return null;
		}

		public float CeilingAt(float x, float y, float r, float above)
		{
			int num = Math.Max(0, (int)Math.Floor((x - r - X0) / Cell));
			int num2 = Math.Min(W - 1, (int)Math.Floor((x + r - X0) / Cell));
			int num3 = Math.Max(0, (int)Math.Floor((y - r - Y0) / Cell));
			int num4 = Math.Min(H - 1, (int)Math.Floor((y + r - Y0) / Cell));
			float num5 = float.PositiveInfinity;
			float num6 = r * r;
			for (int i = num3; i <= num4; i++)
			{
				for (int j = num; j <= num2; j++)
				{
					int num7 = i * W + j;
					if (_start[num7] == _start[num7 + 1])
					{
						continue;
					}
					float num8 = X0 + ((float)j + 0.5f) * Cell - x;
					float num9 = Y0 + ((float)i + 0.5f) * Cell - y;
					if (num8 * num8 + num9 * num9 > num6)
					{
						continue;
					}
					int num10 = SpansAt(num7);
					for (int k = 0; k < num10; k++)
					{
						if (_sk[k] != 3 && _sp[k * 2] > above && _sp[k * 2] < num5)
						{
							num5 = _sp[k * 2];
						}
					}
				}
			}
			return num5;
		}

		public static void BodyPoints(float x, float y, float r, float[] px, float[] py)
		{
			px[0] = x;
			py[0] = y;
			float num = r * 0.7f;
			for (int i = 0; i < 8; i++)
			{
				double num2 = (double)i * Math.PI / 4.0;
				px[i + 1] = x + (float)Math.Cos(num2) * num;
				py[i + 1] = y + (float)Math.Sin(num2) * num;
			}
		}

		public bool Support(float x, float y, float r, float maxZ, float tol, out float z, out byte what)
		{
			return Support(x, y, r, maxZ, tol, float.NaN, out z, out what);
		}

		public bool Support(float x, float y, float r, float maxZ, float tol, float keep, out float z, out byte what)
		{
			if (_px == null)
			{
				_px = new float[9];
				_py = new float[9];
				_tops = new float[9];
				_whats = new byte[9];
			}
			BodyPoints(x, y, r, _px, _py);
			bool flag = false;
			for (int i = 0; i < 9; i++)
			{
				_tops[i] = (TopAt(_px[i], _py[i], maxZ, out var z2, out var what2, out var ramp) ? z2 : float.NegativeInfinity);
				_whats[i] = what2;
				if (i == 0)
				{
					flag = ramp;
				}
			}
			if (TopAt(x, y, maxZ + Cell * 3f, out var z3, out var what3, out var ramp2) && ramp2 && z3 > _tops[0])
			{
				_tops[0] = z3;
				_whats[0] = what3;
				flag = true;
			}
			int num = Choose(_tops, tol, keep);
			if (flag && (num < 0 || _tops[0] > _tops[num]))
			{
				num = 0;
			}
			z = ((num < 0) ? float.NegativeInfinity : _tops[num]);
			what = (byte)((num >= 0) ? _whats[num] : 0);
			return num >= 0;
		}

		public static int Choose(float[] tops, float tol, float keep = float.NaN)
		{
			int num = -1;
			for (int i = 0; i < tops.Length; i++)
			{
				float num2 = tops[i];
				if (float.IsNegativeInfinity(num2) || (num >= 0 && num2 <= tops[num]))
				{
					continue;
				}
				int num3 = 0;
				float num4 = 9f;
				float num5 = -9f;
				float num6 = 9f;
				float num7 = -9f;
				for (int j = 0; j < tops.Length; j++)
				{
					if (!(tops[j] < num2 - tol))
					{
						num3++;
						num4 = Math.Min(num4, OffX[j]);
						num5 = Math.Max(num5, OffX[j]);
						num6 = Math.Min(num6, OffY[j]);
						num7 = Math.Max(num7, OffY[j]);
					}
				}
				int num8 = ((!float.IsNaN(keep) && Math.Abs(num2 - keep) <= tol) ? 3 : 4);
				if (num3 >= num8 && num5 - num4 > 0.6f && num7 - num6 > 0.6f)
				{
					num = i;
				}
			}
			return num;
		}
	}
}
