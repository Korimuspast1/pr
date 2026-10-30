using System;
using System.Collections.Generic;
using System.Globalization;

namespace FirstPerson.Core
{
	public static class FlagCloth
	{
		private struct Line
		{
			public float A;

			public float B;

			public Line(float a, float b)
			{
				A = a;
				B = b;
			}

			public float At(float x)
			{
				return A + B * x;
			}

			public Line Shift(float dz)
			{
				return new Line(A + dz, B);
			}
		}

		private sealed class Outline
		{
			public readonly FlagShape Shape;

			public readonly float L;

			public readonly float H;

			public readonly float Q;

			public readonly float Kink;

			public readonly float Slope;

			public readonly float Wedge;

			public float Sleeve => 3f * Q;

			public bool FlyBand
			{
				get
				{
					if (Shape != FlagShape.Plain)
					{
						return Shape == FlagShape.Long;
					}
					return true;
				}
			}

			public float IconLimit
			{
				get
				{
					if (Shape != FlagShape.Swallowtail)
					{
						if (Shape != FlagShape.Pointed)
						{
							return L - 3f * Q;
						}
						return Kink - Q;
					}
					return Wedge - Q;
				}
			}

			public float IconShare
			{
				get
				{
					if (Shape != FlagShape.Swallowtail)
					{
						if (Shape != FlagShape.Long)
						{
							if (Shape != FlagShape.Pointed)
							{
								return 0.45f;
							}
							return 0.3f;
						}
						return 0.26f;
					}
					return 0.36f;
				}
			}

			public Outline(FlagShape shape, float l, float h)
			{
				Shape = shape;
				L = l;
				H = h;
				Q = h / 36f;
				float kink;
				switch (shape)
				{
				default:
					kink = l;
					break;
				case FlagShape.Pointed:
					kink = 0.55f * l;
					break;
				case FlagShape.Swallowtail:
					kink = 0.7f * l;
					break;
				}
				Kink = kink;
				Slope = ((Kink < l) ? (0.5f * h / (l - Kink)) : 0f);
				Wedge = ((shape == FlagShape.Swallowtail) ? (Kink - 2f * Q / Slope) : l);
			}

			public int Spans(float x, Line[] top, Line[] bottom)
			{
				float h = H;
				float slope = Slope;
				float kink = Kink;
				switch (Shape)
				{
				case FlagShape.Long:
					top[0] = new Line(0f, -0.225f * h / L);
					bottom[0] = new Line(0f - h, 0.225f * h / L);
					return 1;
				case FlagShape.Pointed:
					if (x > kink)
					{
						top[0] = new Line(slope * kink, 0f - slope);
						bottom[0] = new Line(0f - h - slope * kink, slope);
						return 1;
					}
					break;
				case FlagShape.Swallowtail:
					if (x > kink)
					{
						top[0] = new Line(0f, 0f);
						bottom[0] = new Line(-0.5f * h - slope * kink, slope);
						top[1] = new Line(-0.5f * h + slope * kink, 0f - slope);
						bottom[1] = new Line(0f - h, 0f);
						return 2;
					}
					break;
				}
				top[0] = new Line(0f, 0f);
				bottom[0] = new Line(0f - h, 0f);
				return 1;
			}

			public void Breaks(List<float> xs)
			{
				if (Kink < L)
				{
					xs.Add(Kink);
				}
				if (Shape == FlagShape.Swallowtail && Wedge > Sleeve)
				{
					xs.Add(Wedge);
				}
				if (FlyBand)
				{
					xs.Add(L - 2f * Q);
				}
				if (Shape == FlagShape.Swallowtail)
				{
					xs.Add(Kink + (L - Kink) * (1f - 8f * Q / H));
				}
				if (Shape == FlagShape.Pointed)
				{
					xs.Add(Kink + (L - Kink) * (1f - 4f * Q / H));
				}
			}

			public bool Inside(float x, float z, Line[] top, Line[] bottom)
			{
				if (x < 0f || x > L)
				{
					return false;
				}
				int num = Spans(x, top, bottom);
				for (int i = 0; i < num; i++)
				{
					if (z <= top[i].At(x) && z >= bottom[i].At(x))
					{
						return true;
					}
				}
				return false;
			}
		}

		private sealed class IconGrid
		{
			public int W;

			public int H;

			public bool[] Set;

			public bool At(int col, int row)
			{
				if (col >= 0 && row >= 0 && col < W && row < H)
				{
					return Set[row * W + col];
				}
				return false;
			}

			public static IconGrid Of(FlagEmblem e, int f)
			{
				if (e == null || !e.Box(out var x, out var y, out var x2, out var y2))
				{
					return null;
				}
				int num = x2 - x + 1;
				int num2 = y2 - y + 1;
				IconGrid iconGrid = new IconGrid
				{
					W = (num + f - 1) / f,
					H = (num2 + f - 1) / f
				};
				iconGrid.Set = new bool[iconGrid.W * iconGrid.H];
				for (int i = 0; i < iconGrid.H; i++)
				{
					for (int j = 0; j < iconGrid.W; j++)
					{
						int num3 = 0;
						int num4 = 0;
						for (int k = 0; k < f; k++)
						{
							for (int l = 0; l < f; l++)
							{
								int num5 = x + j * f + l;
								int num6 = y2 - (i * f + k);
								if (num5 <= x2 && num6 >= y)
								{
									num4++;
									if (e.At(num5, num6) == 3)
									{
										num3++;
									}
								}
							}
						}
						iconGrid.Set[i * iconGrid.W + j] = num4 > 0 && num3 * 2 >= num4;
					}
				}
				return iconGrid;
			}
		}

		private sealed class Slab
		{
			public float X0;

			public float X1;

			public int Strip;

			public readonly List<Band> Bands = new List<Band>();
		}

		private struct Band
		{
			public float T0;

			public float T1;

			public float B0;

			public float B1;

			public byte Tone;
		}

		private struct IconBox
		{
			public float X0;

			public float X1;

			public float Top;

			public float InTop;

			public float InBottom;

			public float OutTop;

			public float OutBottom;

			public bool On(IconGrid g, int col, int row, float u)
			{
				if (!g.At(col, row))
				{
					return false;
				}
				float num = Top + (float)row * u;
				float num2 = num + u;
				if (num >= OutTop - 1E-05f)
				{
					return num2 <= OutBottom + 1E-05f;
				}
				return false;
			}
		}

		private sealed class Pieces
		{
			private readonly float _snap;

			private readonly List<float> _x = new List<float>();

			private readonly List<float> _z = new List<float>();

			private readonly Dictionary<long, int> _at = new Dictionary<long, int>();

			private readonly List<int[]> _rings = new List<int[]>();

			private readonly List<int> _part = new List<int>();

			private readonly List<int> _slot = new List<int>();

			private readonly List<int> _ring = new List<int>();

			public Pieces(float snap)
			{
				_snap = snap;
			}

			private int Corner(float x, float z)
			{
				long num = (long)Math.Round(x / _snap);
				long num2 = (long)Math.Round(z / _snap);
				long key = (num << 32) ^ (num2 & 0xFFFFFFFFu);
				if (_at.TryGetValue(key, out var value))
				{
					return value;
				}
				_x.Add((float)((double)num * (double)_snap));
				_z.Add((float)((double)num2 * (double)_snap));
				return _at[key] = _x.Count - 1;
			}

			public void Begin()
			{
				_ring.Clear();
			}

			public void Point(float x, float z)
			{
				int num = Corner(x, z);
				if (_ring.Count == 0 || _ring[_ring.Count - 1] != num)
				{
					_ring.Add(num);
				}
			}

			public void End(int part, int slot)
			{
				while (_ring.Count > 1 && _ring[0] == _ring[_ring.Count - 1])
				{
					_ring.RemoveAt(_ring.Count - 1);
				}
				if (_ring.Count >= 3)
				{
					_rings.Add(_ring.ToArray());
					_part.Add(part);
					_slot.Add(slot);
				}
			}

			public void Rect(int part, int slot, float x0, float x1, float z0, float z1)
			{
				Begin();
				Point(x0, z0);
				Point(x1, z0);
				Point(x1, z1);
				Point(x0, z1);
				End(part, slot);
			}

			public void Lay(Loom loom)
			{
				if (_x.Count == 0)
				{
					return;
				}
				float num = float.MaxValue;
				float num2 = float.MinValue;
				float num3 = float.MaxValue;
				float num4 = float.MinValue;
				for (int i = 0; i < _x.Count; i++)
				{
					num = Math.Min(num, _x[i]);
					num2 = Math.Max(num2, _x[i]);
					num3 = Math.Min(num3, _z[i]);
					num4 = Math.Max(num4, _z[i]);
				}
				double num5 = 1E-06 * (1.0 + (double)Math.Max(Math.Max(Math.Abs(num), Math.Abs(num2)), Math.Max(Math.Abs(num3), Math.Abs(num4))));
				float num6 = Math.Max(0.001f, Math.Max(num2 - num, num4 - num3) / 48f);
				int num7 = (int)((num2 - num) / num6) + 1;
				int num8 = (int)((num4 - num3) / num6) + 1;
				List<int>[] array = new List<int>[num7 * num8];
				for (int j = 0; j < _x.Count; j++)
				{
					int num9 = Math.Min(num8 - 1, (int)((_z[j] - num3) / num6)) * num7 + Math.Min(num7 - 1, (int)((_x[j] - num) / num6));
					(array[num9] ?? (array[num9] = new List<int>())).Add(j);
				}
				List<float> list = new List<float>();
				List<float> list2 = new List<float>();
				List<KeyValuePair<double, int>> list3 = new List<KeyValuePair<double, int>>();
				for (int k = 0; k < _rings.Count; k++)
				{
					int[] array2 = _rings[k];
					list.Clear();
					list2.Clear();
					for (int l = 0; l < array2.Length; l++)
					{
						int num10 = array2[l];
						int num11 = array2[(l + 1) % array2.Length];
						list.Add(_x[num10]);
						list2.Add(_z[num10]);
						double num12 = _x[num10];
						double num13 = _z[num10];
						double num14 = (double)_x[num11] - num12;
						double num15 = (double)_z[num11] - num13;
						double num16 = num14 * num14 + num15 * num15;
						if (num16 <= 0.0)
						{
							continue;
						}
						double num17 = Math.Sqrt(num16);
						list3.Clear();
						int num18 = Math.Max(0, (int)(((double)Math.Min(_x[num10], _x[num11]) - num5 - (double)num) / (double)num6));
						int num19 = Math.Min(num7 - 1, (int)(((double)Math.Max(_x[num10], _x[num11]) + num5 - (double)num) / (double)num6));
						int num20 = Math.Max(0, (int)(((double)Math.Min(_z[num10], _z[num11]) - num5 - (double)num3) / (double)num6));
						int num21 = Math.Min(num8 - 1, (int)(((double)Math.Max(_z[num10], _z[num11]) + num5 - (double)num3) / (double)num6));
						for (int m = num20; m <= num21; m++)
						{
							for (int n = num18; n <= num19; n++)
							{
								List<int> list4 = array[m * num7 + n];
								if (list4 == null)
								{
									continue;
								}
								foreach (int item in list4)
								{
									if (item != num10 && item != num11)
									{
										double num22 = (double)_x[item] - num12;
										double num23 = (double)_z[item] - num13;
										double num24 = (num22 * num14 + num23 * num15) / num16;
										if (!(num24 <= 1E-09) && !(num24 >= 0.999999999) && !(Math.Abs(num22 * num15 - num23 * num14) / num17 > num5))
										{
											list3.Add(new KeyValuePair<double, int>(num24, item));
										}
									}
								}
							}
						}
						if (list3.Count == 0)
						{
							continue;
						}
						list3.Sort((KeyValuePair<double, int> keyValuePair, KeyValuePair<double, int> keyValuePair2) => keyValuePair.Key.CompareTo(keyValuePair2.Key));
						foreach (KeyValuePair<double, int> item2 in list3)
						{
							list.Add(_x[item2.Value]);
							list2.Add(_z[item2.Value]);
						}
					}
					loom.Piece(_part[k], _slot[k], list, list2);
				}
			}
		}

		private sealed class Loom
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

			private readonly List<PartData> _parts = new List<PartData>();

			private readonly List<int> _ring = new List<int>();

			private readonly List<int> _tris = new List<int>();

			public void Part(string name, int parent, Vec3 pivot)
			{
				_parts.Add(new PartData
				{
					Name = name,
					Parent = parent,
					Pivot = pivot
				});
			}

			public void Piece(int part, int slot, List<float> xs, List<float> zs)
			{
				int count = xs.Count;
				if (count < 3)
				{
					return;
				}
				_ring.Clear();
				_tris.Clear();
				for (int i = 0; i < count; i++)
				{
					_ring.Add(i);
				}
				int num = 0;
				int num2 = 0;
				while (_ring.Count > 3 && num2++ < 4 * count)
				{
					int count2 = _ring.Count;
					int num3 = -1;
					for (int j = 0; j < count2; j++)
					{
						if (num3 >= 0)
						{
							break;
						}
						int num4 = (num + j) % count2;
						if (Convex(xs, zs, _ring[(num4 + count2 - 1) % count2], _ring[num4], _ring[(num4 + 1) % count2]) && !InLineWithout(xs, zs, num4))
						{
							num3 = num4;
						}
					}
					if (num3 < 0)
					{
						break;
					}
					_tris.Add(_ring[(num3 + count2 - 1) % count2]);
					_tris.Add(_ring[num3]);
					_tris.Add(_ring[(num3 + 1) % count2]);
					_ring.RemoveAt(num3);
					num = num3 % _ring.Count;
				}
				if (_ring.Count == 3 && Convex(xs, zs, _ring[0], _ring[1], _ring[2]))
				{
					_tris.Add(_ring[0]);
					_tris.Add(_ring[1]);
					_tris.Add(_ring[2]);
				}
				if (_tris.Count == 0)
				{
					return;
				}
				PartData partData = _parts[part];
				for (int k = 0; k < 2; k++)
				{
					int num5 = partData.Pos.Count / 3;
					float item = ((k == 0) ? (-1f) : 1f);
					for (int l = 0; l < count; l++)
					{
						partData.Pos.Add(xs[l]);
						partData.Pos.Add(0f);
						partData.Pos.Add(zs[l]);
						partData.Nrm.Add(0f);
						partData.Nrm.Add(item);
						partData.Nrm.Add(0f);
					}
					for (int m = 0; m < _tris.Count; m += 3)
					{
						partData.Tri.Add(num5 + _tris[m]);
						partData.Tri.Add(num5 + ((k == 0) ? _tris[m + 1] : _tris[m + 2]));
						partData.Tri.Add(num5 + ((k == 0) ? _tris[m + 2] : _tris[m + 1]));
						partData.Slot.Add((byte)slot);
					}
				}
			}

			private bool InLineWithout(List<float> xs, List<float> zs, int k)
			{
				int count = _ring.Count;
				if (count <= 3)
				{
					return false;
				}
				int num = _ring[(k + 1) % count];
				int num2 = -1;
				for (int i = 2; i < count; i++)
				{
					int num3 = _ring[(k + i) % count];
					if (num2 < 0)
					{
						if (xs[num3] != xs[num] || zs[num3] != zs[num])
						{
							num2 = num3;
						}
					}
					else if (!Flat(xs, zs, num, num2, num3))
					{
						return false;
					}
				}
				return true;
			}

			private static bool Convex(List<float> xs, List<float> zs, int a, int b, int c)
			{
				return Turn(xs, zs, a, b, c) > 1E-05 * Lengths(xs, zs, a, b, c);
			}

			private static bool Flat(List<float> xs, List<float> zs, int a, int b, int c)
			{
				return Math.Abs(Turn(xs, zs, a, b, c)) <= 1E-05 * Lengths(xs, zs, a, b, c);
			}

			private static double Lengths(List<float> xs, List<float> zs, int a, int b, int c)
			{
				double num = (double)xs[b] - (double)xs[a];
				double num2 = (double)zs[b] - (double)zs[a];
				double num3 = (double)xs[c] - (double)xs[a];
				double num4 = (double)zs[c] - (double)zs[a];
				return Math.Sqrt((num * num + num2 * num2) * (num3 * num3 + num4 * num4)) + 1E-30;
			}

			private static double Turn(List<float> xs, List<float> zs, int a, int b, int c)
			{
				return ((double)xs[b] - (double)xs[a]) * ((double)zs[c] - (double)zs[a]) - ((double)zs[b] - (double)zs[a]) * ((double)xs[c] - (double)xs[a]);
			}

			public void Tube(int part, int slot, Vec3 a, Vec3 b, float r, int sides)
			{
				PartData d = _parts[part];
				Vec3 vec = (b - a).Normalized();
				Vec3 vec2 = Vec3.Cross((Math.Abs(vec.Z) < 0.9f) ? new Vec3(0f, 0f, 1f) : new Vec3(1f, 0f, 0f), vec).Normalized();
				Vec3 vec3 = Vec3.Cross(vec, vec2);
				Vec3[] array = new Vec3[sides];
				for (int i = 0; i < sides; i++)
				{
					double num = ((double)i + 0.5) * 2.0 * Math.PI / (double)sides;
					array[i] = vec2 * (float)((double)r * Math.Cos(num)) + vec3 * (float)((double)r * Math.Sin(num));
				}
				for (int j = 0; j < sides; j++)
				{
					int num2 = (j + 1) % sides;
					Face(a + array[j], a + array[num2], b + array[num2], b + array[j]);
				}
				for (int k = 1; k + 1 < sides; k += 2)
				{
					int num3 = Math.Min(sides - 1, k + 2);
					Face(b + array[0], b + array[k], b + array[k + 1], b + array[num3]);
					Face(a + array[0], a + array[num3], a + array[k + 1], a + array[k]);
				}
				void Face(Vec3 p0, Vec3 p1, Vec3 p2, Vec3 p3)
				{
					Vec3 vec4 = Vec3.Cross(p1 - p0, p2 - p0).Normalized();
					int num4 = d.Pos.Count / 3;
					Vec3[] array2 = new Vec3[4] { p0, p1, p2, p3 };
					for (int l = 0; l < array2.Length; l++)
					{
						Vec3 vec5 = array2[l];
						d.Pos.Add(vec5.X);
						d.Pos.Add(vec5.Y);
						d.Pos.Add(vec5.Z);
						d.Nrm.Add(vec4.X);
						d.Nrm.Add(vec4.Y);
						d.Nrm.Add(vec4.Z);
					}
					d.Tri.Add(num4);
					d.Tri.Add(num4 + 1);
					d.Tri.Add(num4 + 2);
					d.Slot.Add((byte)slot);
					d.Tri.Add(num4);
					d.Tri.Add(num4 + 2);
					d.Tri.Add(num4 + 3);
					d.Slot.Add((byte)slot);
				}
			}

			public MeshModel Build(string name, bool rod, string info, FlagEmblem greys = null)
			{
				byte b = greys?.GreyField ?? byte.MaxValue;
				byte b2 = greys?.GreyDark ?? 200;
				byte b3 = greys?.GreyEmblem ?? byte.MaxValue;
				MeshModel meshModel = new MeshModel();
				meshModel.Name = name;
				meshModel.SlotNames = ((!rod) ? new string[3] { "field", "field_dark", "emblem" } : new string[4] { "field", "field_dark", "emblem", "rod" });
				meshModel.SlotKind = ((!rod) ? new byte[3] { 7, 7, 7 } : new byte[4] { 7, 7, 7, 10 });
				meshModel.SlotFlags = ((!rod) ? new byte[3] : new byte[4] { 0, 0, 0, 16 });
				meshModel.SlotColor = ((!rod) ? new uint[3]
				{
					TerrainCache.Rgb(b, b, b),
					TerrainCache.Rgb(b2, b2, b2),
					TerrainCache.Rgb(b3, b3, b3)
				} : new uint[4]
				{
					TerrainCache.Rgb(b, b, b),
					TerrainCache.Rgb(b2, b2, b2),
					TerrainCache.Rgb(b3, b3, b3),
					TerrainCache.Rgb(70, 72, 78)
				});
				meshModel.Parts = new MeshPart[_parts.Count];
				MeshModel meshModel2 = meshModel;
				Vec3 min = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
				Vec3 vec = new Vec3(float.MinValue, float.MinValue, float.MinValue);
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
						meshPart.Axis[j] = Dominant(meshPart, j);
					}
					for (int k = 0; k < meshPart.VertexCount; k++)
					{
						float val = meshPart.Pos[k * 3];
						float val2 = meshPart.Pos[k * 3 + 1];
						float val3 = meshPart.Pos[k * 3 + 2];
						min = new Vec3(Math.Min(min.X, val), Math.Min(min.Y, val2), Math.Min(min.Z, val3));
						vec = new Vec3(Math.Max(vec.X, val), Math.Max(vec.Y, val2), Math.Max(vec.Z, val3));
					}
					meshModel2.Parts[i] = meshPart;
				}
				if (min.X > vec.X)
				{
					vec = new Vec3(0f, 0f, 0f);
					min = vec;
				}
				meshModel2.Min = min;
				meshModel2.Max = vec;
				meshModel2.ParseInfo(info);
				return meshModel2;
			}

			private static byte Dominant(MeshPart p, int t)
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

		public const int Rows = 36;

		public const float RigHeight = 1.26f;

		public const float PoleRig = 3f;

		public const float HoistDrop = 0.12f;

		public const byte GreyField = byte.MaxValue;

		public const byte GreyDark = 200;

		public const byte GreyEmblem = byte.MaxValue;

		public const int SlotField = 0;

		public const int SlotDark = 1;

		public const int SlotEmblem = 2;

		public const int SlotRod = 3;

		public static readonly int[] StripsPerLod = new int[3] { 8, 4, 1 };

		private static readonly int[] TornRowsPerLod = new int[3] { 18, 9, 6 };

		public static float LengthOf(FlagShape s)
		{
			switch (s)
			{
			default:
				return 1.5f;
			case FlagShape.Pointed:
				return 1.8f;
			case FlagShape.Long:
				return 2.4f;
			case FlagShape.Swallowtail:
				return 1.6f;
			}
		}

		public static FlagShape ShapeOf(int style)
		{
			switch (style)
			{
			default:
				return FlagShape.Plain;
			case 2:
				return FlagShape.Pointed;
			case 1:
				return FlagShape.Long;
			case 0:
				return FlagShape.Swallowtail;
			}
		}

		public static FlagTop TopOf(int style)
		{
			switch (style)
			{
			default:
				return FlagTop.Ball;
			case 2:
				return FlagTop.Onion;
			case 1:
				return FlagTop.Wing;
			case 0:
				return FlagTop.Spike;
			}
		}

		public static string ShapeName(FlagShape s)
		{
			switch (s)
			{
			default:
				return "plain";
			case FlagShape.Pointed:
				return "pointed";
			case FlagShape.Long:
				return "long";
			case FlagShape.Swallowtail:
				return "swallowtail";
			}
		}

		public static string ClothName(string set, int icon, FlagShape shape, int lod, int torn = -1)
		{
			return "hf_cloth_" + set + "_" + icon + "_" + ShapeName(shape) + "_" + lod + ((torn >= 0) ? ("_t" + torn) : "");
		}

		public static string BannerName(string set, int bg, int icon, int lod)
		{
			return "hf_banner_" + set + "_" + bg + "_" + icon + "_" + lod;
		}

		public static string PlainName(FlagShape shape, int lod, int torn = -1)
		{
			return "hf_plain_" + ShapeName(shape) + "_" + lod + ((torn >= 0) ? ("_t" + torn) : "");
		}

		public static MeshModel Flag(string name, FlagShape shape, FlagEmblem icon, int lod, int tornSeed = -1, float lengthScale = 1f)
		{
			lod = ((lod >= 0) ? ((lod > 2) ? 2 : lod) : 0);
			if (tornSeed < 0)
			{
				return Woven(name, shape, icon, lod, lengthScale);
			}
			return Torn(name, shape, icon, lod, tornSeed, lengthScale);
		}

		public static MeshModel Plain(FlagShape s, int lod, int tornSeed = -1)
		{
			return Flag(PlainName(s, lod, tornSeed), s, null, lod, tornSeed);
		}

		private static void PlaceIcon(Outline o, IconGrid g, float u, out float x0, out float top)
		{
			float q = o.Q;
			float num = (float)g.W * u;
			float num2 = (float)g.H * u;
			Line[] array = new Line[2];
			Line[] array2 = new Line[2];
			top = (float)Math.Round((o.H - num2) / 2f / q) * q;
			float val = (float)Math.Round((o.IconShare * o.L - num / 2f) / q) * q;
			float num3 = o.Sleeve + q;
			for (int i = 0; i < 2; i++)
			{
				float num4 = ((i == 0) ? (2f * q) : (0.5f * q));
				for (x0 = Math.Max(num3, val); x0 >= num3 - 1E-05f; x0 -= q)
				{
					float num5 = x0 + num;
					if (!(num5 > o.IconLimit + 1E-05f))
					{
						bool flag = true;
						float[] array3 = new float[2] { x0, num5 };
						foreach (float x1 in array3)
						{
							o.Spans(x1, array, array2);
							if (top < 0f - array[0].At(x1) + num4 - 1E-05f || top + num2 > 0f - array2[0].At(x1) - num4 + 1E-05f)
							{
								flag = false;
							}
						}
						if (flag)
						{
							return;
						}
					}
				}
			}
			x0 = num3;
		}

		private static MeshModel Woven(string name, FlagShape shape, FlagEmblem icon, int lod, float lengthScale)
		{
			float num = 1.26f;
			float num2 = LengthOf(shape) * num * Math.Max(0.2f, lengthScale);
			Outline outline = new Outline(shape, num2, num);
			float q = outline.Q;
			int num3 = StripsPerLod[lod];
			int num4;
			switch (lod)
			{
			default:
				num4 = 4;
				break;
			case 1:
				num4 = 2;
				break;
			case 0:
				num4 = 1;
				break;
			}
			int num5 = num4;
			float num6 = (float)num5 * q;
			float num7 = num2 / (float)num3;
			IconGrid iconGrid = IconGrid.Of(icon, num5);
			IconBox iconBox = default(IconBox);
			if (iconGrid != null)
			{
				iconBox = Box(outline, iconGrid, num6);
			}
			List<float> list = new List<float> { 0f, outline.Sleeve, num2 };
			for (int i = 1; i < num3; i++)
			{
				list.Add((float)i * num7);
			}
			outline.Breaks(list);
			if (iconGrid != null)
			{
				list.Add(iconBox.X0);
				list.Add(iconBox.X1);
			}
			Cuts(list, num2);
			Line[] array = new Line[2];
			Line[] array2 = new Line[2];
			List<Slab> list2 = new List<Slab>();
			for (int j = 0; j + 1 < list.Count; j++)
			{
				float num8 = list[j];
				float num9 = list[j + 1];
				float num10 = 0.5f * (num8 + num9);
				Slab slab = new Slab
				{
					X0 = num8,
					X1 = num9,
					Strip = Math.Min(num3 - 1, (int)(num10 / num7))
				};
				int num11 = outline.Spans(num10, array, array2);
				bool flag = iconGrid != null && num8 >= iconBox.X0 - 1E-05f && num9 <= iconBox.X1 + 1E-05f;
				for (int k = 0; k < num11; k++)
				{
					if (flag)
					{
						Line line = array[k].Shift(-2f * q);
						Line line2 = array2[k].Shift(2f * q);
						Line bottom = new Line(0f - iconBox.InTop, 0f);
						Line top = new Line(0f - iconBox.InBottom, 0f);
						if (Math.Min(line.At(num8), line.At(num9)) >= 0f - iconBox.InTop - 1E-05f)
						{
							Add(slab, array[k], line, 2);
							Add(slab, line, bottom, 1);
						}
						else
						{
							Add(slab, array[k], bottom, 2);
						}
						if (Math.Max(line2.At(num8), line2.At(num9)) <= 0f - iconBox.InBottom + 1E-05f)
						{
							Add(slab, top, line2, 1);
							Add(slab, line2, array2[k], 2);
						}
						else
						{
							Add(slab, top, array2[k], 2);
						}
					}
					else
					{
						Bands(outline, slab, array[k], array2[k]);
					}
				}
				list2.Add(slab);
			}
			Merge(list2);
			Pieces pieces = new Pieces(1E-05f);
			foreach (Slab item in list2)
			{
				foreach (Band band in item.Bands)
				{
					if (!(band.T0 - band.B0 < -1E-06f) && !(band.T1 - band.B1 < -1E-06f))
					{
						pieces.Begin();
						pieces.Point(item.X0, band.B0);
						pieces.Point(item.X1, band.B1);
						pieces.Point(item.X1, band.T1);
						pieces.Point(item.X0, band.T0);
						pieces.End(item.Strip, band.Tone - 1);
					}
				}
			}
			if (iconGrid != null)
			{
				List<float> list3 = new List<float>();
				for (int l = 0; l <= iconGrid.W; l++)
				{
					list3.Add(iconBox.X0 + (float)l * num6);
				}
				for (int m = 1; m < num3; m++)
				{
					if ((float)m * num7 > iconBox.X0 + 1E-05f && (float)m * num7 < iconBox.X1 - 1E-05f)
					{
						list3.Add((float)m * num7);
					}
				}
				list3.Sort();
				List<float> list4 = new List<float> { iconBox.InTop, iconBox.InBottom };
				for (int n = 0; n <= iconGrid.H; n++)
				{
					float num12 = iconBox.Top + (float)n * num6;
					if (num12 > iconBox.InTop + 1E-05f && num12 < iconBox.InBottom - 1E-05f)
					{
						list4.Add(num12);
					}
				}
				list4.Sort();
				int num13 = list3.Count - 1;
				int num14 = list4.Count - 1;
				byte[] array3 = new byte[num13 * num14];
				int[] array4 = new int[num13];
				for (int num15 = 0; num15 < num13; num15++)
				{
					float num16 = 0.5f * (list3[num15] + list3[num15 + 1]);
					array4[num15] = Math.Min(num3 - 1, (int)(num16 / num7));
					int col = (int)Math.Floor((num16 - iconBox.X0) / num6);
					for (int num17 = 0; num17 < num14; num17++)
					{
						int row = (int)Math.Floor((0.5f * (list4[num17] + list4[num17 + 1]) - iconBox.Top) / num6);
						array3[num17 * num13 + num15] = (byte)((!iconBox.On(iconGrid, col, row, num6)) ? 1 : 3);
					}
				}
				float[] array5 = new float[num14 + 1];
				for (int num18 = 0; num18 <= num14; num18++)
				{
					array5[num18] = 0f - list4[num18];
				}
				foreach (int[] item2 in Greedy(array3, num13, num14, array4))
				{
					pieces.Rect(array4[item2[0]], item2[4] - 1, list3[item2[0]], list3[item2[1]], array5[item2[3]], array5[item2[2]]);
				}
			}
			Loom loom = new Loom();
			for (int num19 = 0; num19 < num3; num19++)
			{
				loom.Part((num3 == 1) ? "flag" : ("strip" + num19), num19 - 1, new Vec3((float)num19 * num7, 0f, 0f));
			}
			pieces.Lay(loom);
			return loom.Build(name, rod: false, Info(shape, num3, num2));
		}

		private static IconBox Box(Outline o, IconGrid g, float u)
		{
			float q = o.Q;
			PlaceIcon(o, g, u, out var x, out var top);
			IconBox result = new IconBox
			{
				X0 = x,
				X1 = x + (float)g.W * u,
				Top = top
			};
			Line[] array = new Line[2];
			Line[] array2 = new Line[2];
			float num = 0f;
			float num2 = o.H;
			float[] array3 = new float[2] { result.X0, result.X1 };
			foreach (float x2 in array3)
			{
				o.Spans(x2, array, array2);
				num = Math.Max(num, 0f - array[0].At(x2));
				num2 = Math.Min(num2, 0f - array2[0].At(x2));
			}
			result.OutTop = num + 0.25f * q;
			result.OutBottom = num2 - 0.25f * q;
			result.InTop = (float)Math.Ceiling((num + 2f * q) / q - 0.0001f) * q;
			result.InBottom = (float)Math.Floor((num2 - 2f * q) / q + 0.0001f) * q;
			for (int j = 0; j < g.H; j++)
			{
				for (int k = 0; k < g.W; k++)
				{
					if (result.On(g, k, j, u))
					{
						result.InTop = Math.Min(result.InTop, top + (float)j * u);
						result.InBottom = Math.Max(result.InBottom, top + (float)(j + 1) * u);
					}
				}
			}
			return result;
		}

		private static void Bands(Outline o, Slab s, Line top, Line bottom)
		{
			float q = o.Q;
			float x = s.X0;
			float x2 = s.X1;
			float num = 0.5f * (x + x2);
			if (x2 <= o.Sleeve + 1E-05f || (o.FlyBand && x >= o.L - 2f * q - 1E-05f) || top.At(num) - bottom.At(num) < 4f * q)
			{
				Add(s, top, bottom, 2);
				return;
			}
			Line line = top.Shift(-2f * q);
			Line line2 = bottom.Shift(2f * q);
			Add(s, top, line, 2);
			if (o.Shape == FlagShape.Swallowtail && num > o.Wedge && num < o.Kink)
			{
				float kink = o.Kink;
				float slope = o.Slope;
				float num2 = 0.5f * o.H;
				Line line3 = new Line(0f - num2 + 2f * q - slope * kink, slope);
				Line line4 = new Line(0f - num2 - 2f * q + slope * kink, 0f - slope);
				Add(s, line, line3, 1);
				Add(s, line3, line4, 2);
				Add(s, line4, line2, 1);
			}
			else
			{
				Add(s, line, line2, 1);
			}
			Add(s, line2, bottom, 2);
		}

		private static void Add(Slab s, Line top, Line bottom, byte tone)
		{
			s.Bands.Add(new Band
			{
				T0 = top.At(s.X0),
				T1 = top.At(s.X1),
				B0 = bottom.At(s.X0),
				B1 = bottom.At(s.X1),
				Tone = tone
			});
		}

		private static void Cuts(List<float> xs, float l)
		{
			xs.Sort();
			List<float> list = new List<float>();
			foreach (float x in xs)
			{
				if (!(x < -1E-06f) && !(x > l + 1E-06f) && (list.Count <= 0 || !(x - list[list.Count - 1] < 1E-05f)))
				{
					list.Add(Math.Max(0f, Math.Min(l, x)));
				}
			}
			list[list.Count - 1] = l;
			xs.Clear();
			xs.AddRange(list);
		}

		private static void Merge(List<Slab> slabs)
		{
			int num = 0;
			while (num + 1 < slabs.Count)
			{
				Slab slab = slabs[num];
				Slab slab2 = slabs[num + 1];
				bool flag = slab.Strip == slab2.Strip && slab.Bands.Count == slab2.Bands.Count;
				int num2 = 0;
				while (flag && num2 < slab.Bands.Count)
				{
					Band band = slab.Bands[num2];
					Band band2 = slab2.Bands[num2];
					flag = band.Tone == band2.Tone && Straight(slab, band.T0, band.T1, slab2, band2.T0, band2.T1) && Straight(slab, band.B0, band.B1, slab2, band2.B0, band2.B1);
					num2++;
				}
				if (!flag)
				{
					num++;
					continue;
				}
				for (int i = 0; i < slab.Bands.Count; i++)
				{
					Band value = slab.Bands[i];
					Band band3 = slab2.Bands[i];
					value.T1 = band3.T1;
					value.B1 = band3.B1;
					slab.Bands[i] = value;
				}
				slab.X1 = slab2.X1;
				slabs.RemoveAt(num + 1);
			}
		}

		private static bool Straight(Slab a, float a0, float a1, Slab b, float b0, float b1)
		{
			if (Math.Abs(a1 - b0) > 1E-06f)
			{
				return false;
			}
			float num = (a1 - a0) / (a.X1 - a.X0);
			float num2 = (b1 - b0) / (b.X1 - b.X0);
			return Math.Abs(num - num2) < 0.0001f;
		}

		private static string Info(FlagShape shape, int strips, float length)
		{
			return "kind=hallflag;shape=" + ShapeName(shape) + ";strips=" + strips + ";length=" + length.ToString("0.#####", CultureInfo.InvariantCulture);
		}

		private static MeshModel Torn(string name, FlagShape shape, FlagEmblem icon, int lod, int seed, float lengthScale)
		{
			float num = 1.26f;
			float num2 = LengthOf(shape) * num * Math.Max(0.2f, lengthScale);
			Outline outline = new Outline(shape, num2, num);
			float q = outline.Q;
			uint rng = (uint)((seed * 7919 + (int)shape * 104729 + 12345) | 1);
			IconGrid iconGrid = IconGrid.Of(icon, 1);
			float x = 0f;
			float top = 0f;
			if (iconGrid != null)
			{
				PlaceIcon(outline, iconGrid, q, out x, out top);
			}
			float num3 = ((iconGrid != null) ? (x + (float)iconGrid.W * q * 0.5f) : 0f);
			float num4 = ((iconGrid != null) ? (0f - (top + (float)iconGrid.H * q * 0.5f)) : 0f);
			float num5 = ((iconGrid != null) ? (0.5f * (float)Math.Sqrt(iconGrid.W * iconGrid.W + iconGrid.H * iconGrid.H) * q) : 0f);
			float num6 = (0.62f + 0.16f * Next()) * num2;
			float[] array = new float[18];
			float num7 = 0f;
			for (int i = 0; i < 18; i++)
			{
				num7 += (float)Math.Round(Next() * 2f - 1f) * 2f * q;
				num7 = Math.Max(-6f * q, Math.Min(6f * q, num7));
				array[i] = num6 + num7;
			}
			int num8 = 2 + ((Next() < 0.5f) ? 1 : 0);
			float[] array2 = new float[num8];
			float[] array3 = new float[num8];
			float[] array4 = new float[num8];
			for (int j = 0; j < num8; j++)
			{
				array4[j] = (2f + 2f * Next()) * q;
				for (int k = 0; k < 24; k++)
				{
					array2[j] = outline.Sleeve + array4[j] + q + Next() * Math.Max(0f, num6 - outline.Sleeve - 2f * array4[j] - 3f * q);
					array3[j] = 0f - (2f * q + array4[j] + Next() * Math.Max(0f, num - 4f * q - 2f * array4[j]));
					float num9 = array2[j] - num3;
					float num10 = array3[j] - num4;
					if (iconGrid == null || num9 * num9 + num10 * num10 > (num5 + array4[j] + q) * (num5 + array4[j] + q))
					{
						break;
					}
					if (k == 23)
					{
						array4[j] = 0f;
					}
				}
			}
			int num11 = 2 + (int)(Next() * 3f);
			float[] array5 = new float[num11];
			float[] array6 = new float[num11];
			float[] array7 = new float[num11];
			for (int l = 0; l < num11; l++)
			{
				array5[l] = outline.Sleeve + 2f * q + Next() * Math.Max(0f, num6 - outline.Sleeve - 4f * q);
				array6[l] = (2f + 4f * Next()) * q;
				array7[l] = (2f + 2f * Next()) * q;
			}
			int num12 = TornRowsPerLod[lod];
			int num13 = StripsPerLod[lod];
			float num14 = num / (float)num12;
			int num15 = (int)Math.Ceiling(num2 / num14 - 0.0001f);
			byte[] array8 = new byte[num15 * num12];
			Line[] array9 = new Line[2];
			Line[] array10 = new Line[2];
			float num16 = 0f;
			for (int m = 0; m < num12; m++)
			{
				float num17 = (0f - ((float)m + 0.5f)) * num14;
				float num18 = 0f;
				int num19 = m * 18 / num12;
				int num20 = Math.Max(num19 + 1, (m + 1) * 18 / num12);
				for (int n = num19; n < num20; n++)
				{
					num18 += array[n];
				}
				num18 /= (float)(num20 - num19);
				for (int num21 = 0; num21 < num15; num21++)
				{
					float num22 = (float)num21 * num14;
					float num23 = Math.Min(num2, num22 + num14);
					float num24 = 0.5f * (num22 + num23);
					if (num24 > num18 || !outline.Inside(num24, num17, array9, array10))
					{
						continue;
					}
					bool flag = false;
					for (int num25 = 0; num25 < num8; num25++)
					{
						if (flag)
						{
							break;
						}
						flag = array4[num25] > 0f && (num24 - array2[num25]) * (num24 - array2[num25]) + (num17 - array3[num25]) * (num17 - array3[num25]) < array4[num25] * array4[num25];
					}
					for (int num26 = 0; num26 < num11; num26++)
					{
						if (flag)
						{
							break;
						}
						flag = Math.Abs(num24 - array5[num26]) < 0.5f * array6[num26] && ((num26 % 2 == 0) ? (num17 > 0f - array7[num26]) : (num17 < 0f - num + array7[num26]));
					}
					if (flag)
					{
						continue;
					}
					byte b = 1;
					int num27 = outline.Spans(num24, array9, array10);
					for (int num28 = 0; num28 < num27; num28++)
					{
						if (num17 <= array9[num28].At(num24) && num17 >= array10[num28].At(num24) && (num17 >= array9[num28].At(num24) - 2f * q - 1E-05f || num17 <= array10[num28].At(num24) + 2f * q + 1E-05f))
						{
							b = 2;
						}
					}
					if (num24 <= outline.Sleeve + 1E-05f)
					{
						b = 2;
					}
					if (iconGrid != null && b == 1 && IconCovers(iconGrid, q, x, top, num22, num23, 0f - (float)m * num14, (float)(-(m + 1)) * num14))
					{
						b = 3;
					}
					array8[m * num15 + num21] = b;
					num16 = Math.Max(num16, num23);
				}
			}
			int[] array11 = new int[num13 + 1];
			int num29 = Math.Max(num13, (int)Math.Ceiling(num16 / num14 - 0.0001f));
			for (int num30 = 0; num30 <= num13; num30++)
			{
				array11[num30] = (int)Math.Round((double)num30 * (double)num29 / (double)num13);
			}
			for (int num31 = 1; num31 <= num13; num31++)
			{
				array11[num31] = Math.Max(array11[num31], array11[num31 - 1] + 1);
			}
			float[] array12 = new float[num15 + 1];
			for (int num32 = 0; num32 <= num15; num32++)
			{
				array12[num32] = Math.Min(num2, (float)num32 * num14);
			}
			int[] array13 = new int[num15];
			int num33 = 0;
			int num34 = 0;
			for (; num33 < num15; num33++)
			{
				for (; num34 + 1 < num13 && num33 >= array11[num34 + 1]; num34++)
				{
				}
				array13[num33] = num34;
			}
			Pieces pieces = new Pieces(1E-05f);
			foreach (int[] item in Greedy(array8, num15, num12, array13))
			{
				pieces.Rect(array13[item[0]], item[4] - 1, array12[item[0]], array12[item[1]], (float)(-item[3]) * num14, (float)(-item[2]) * num14);
			}
			Loom loom = new Loom();
			for (int num35 = 0; num35 < num13; num35++)
			{
				loom.Part((num13 == 1) ? "flag" : ("strip" + num35), num35 - 1, new Vec3(Math.Min(num2, (float)array11[num35] * num14), 0f, 0f));
			}
			pieces.Lay(loom);
			return loom.Build(name, rod: false, Info(shape, num13, num2) + ";torn=" + seed);
			float Next()
			{
				rng ^= rng << 13;
				rng ^= rng >> 17;
				rng ^= rng << 5;
				return (float)(rng & 0xFFFFFF) / 16777216f;
			}
		}

		private static bool IconCovers(IconGrid g, float q, float bx0, float btop, float xa, float xb, float za, float zb)
		{
			int num = 0;
			int num2 = 0;
			int num3 = (int)Math.Floor((xa - bx0) / q);
			int num4 = (int)Math.Ceiling((xb - bx0) / q);
			int num5 = (int)Math.Floor((0f - za - btop) / q);
			int num6 = (int)Math.Ceiling((0f - zb - btop) / q);
			for (int i = num5; i < num6; i++)
			{
				for (int j = num3; j < num4; j++)
				{
					float num7 = bx0 + ((float)j + 0.5f) * q;
					float num8 = 0f - (btop + ((float)i + 0.5f) * q);
					if (!(num7 < xa) && !(num7 > xb) && !(num8 > za) && !(num8 < zb))
					{
						num2++;
						if (g.At(j, i))
						{
							num++;
						}
					}
				}
			}
			if (num2 > 0)
			{
				return num * 2 >= num2;
			}
			return false;
		}

		public static MeshModel Banner(string name, FlagEmblem banner, int lod)
		{
			lod = ((lod >= 0) ? ((lod > 2) ? 2 : lod) : 0);
			Loom loom = new Loom();
			loom.Part("banner", -1, new Vec3(0f, 0f, 0f));
			if (banner == null || !banner.Box(out var x, out var y, out var x2, out var y2))
			{
				return loom.Build(name, rod: true, "kind=hallbanner", banner);
			}
			int num;
			switch (lod)
			{
			default:
				num = 4;
				break;
			case 1:
				num = 2;
				break;
			case 0:
				num = 1;
				break;
			}
			int num2 = num;
			int num3 = x2 - x + 1;
			int num4 = y2 - y + 1;
			int num5 = (num3 + num2 - 1) / num2;
			int num6 = (num4 + num2 - 1) / num2;
			float num7 = 0.5f * (float)(x + x2 + 1);
			byte[] array = new byte[num5 * num6];
			int[] array2 = new int[4];
			for (int i = 0; i < num6; i++)
			{
				for (int j = 0; j < num5; j++)
				{
					Array.Clear(array2, 0, 4);
					for (int k = 0; k < num2; k++)
					{
						for (int l = 0; l < num2; l++)
						{
							int num8 = x + j * num2 + l;
							int num9 = y2 - (i * num2 + k);
							if (num8 <= x2 && num9 >= y)
							{
								array2[banner.At(num8, num9)]++;
							}
						}
					}
					int num10 = 0;
					for (int m = 1; m < 4; m++)
					{
						if (array2[m] >= array2[num10])
						{
							num10 = m;
						}
					}
					array[i * num5 + j] = (byte)num10;
				}
			}
			float[] array3 = new float[num5 + 1];
			for (int n = 0; n <= num5; n++)
			{
				array3[n] = (float)(x + Math.Min(num3, n * num2)) - num7;
			}
			float[] array4 = new float[num6 + 1];
			for (int num11 = 0; num11 <= num6; num11++)
			{
				array4[num11] = -Math.Min(num4, num11 * num2);
			}
			Pieces pieces = new Pieces(0.0001f);
			foreach (int[] item in Greedy(array, num5, num6, new int[num5]))
			{
				pieces.Rect(0, item[4] - 1, array3[item[0]], array3[item[1]], array4[item[3]], array4[item[2]]);
			}
			pieces.Lay(loom);
			int num12;
			switch (lod)
			{
			default:
				num12 = 0;
				break;
			case 1:
				num12 = 4;
				break;
			case 0:
				num12 = 8;
				break;
			}
			int num13 = num12;
			if (num13 > 0)
			{
				loom.Tube(0, 3, new Vec3(-0.5f * (float)num3 - 1.5f, 0f, 0.6f), new Vec3(0.5f * (float)num3 + 1.5f, 0f, 0.6f), 0.45f, num13);
			}
			return loom.Build(name, rod: true, "kind=hallbanner;width=" + num3 + ";height=" + num4, banner);
		}

		private static List<int[]> Greedy(byte[] tone, int cols, int rows, int[] partOf)
		{
			List<int[]> result = null;
			int num = int.MaxValue;
			List<int[]>[] array = new List<int[]>[2]
			{
				Runs(tone, cols, rows, partOf, columns: false),
				Runs(tone, cols, rows, partOf, columns: true)
			};
			foreach (List<int[]> list in array)
			{
				int num2 = Cost(list, cols, rows);
				if (num2 < num)
				{
					num = num2;
					result = list;
				}
			}
			return result;
		}

		private static int Cost(List<int[]> rects, int cols, int rows)
		{
			bool[] array = new bool[(cols + 1) * (rows + 1)];
			foreach (int[] rect in rects)
			{
				array[rect[2] * (cols + 1) + rect[0]] = (array[rect[2] * (cols + 1) + rect[1]] = true);
				array[rect[3] * (cols + 1) + rect[0]] = (array[rect[3] * (cols + 1) + rect[1]] = true);
			}
			int num = 0;
			foreach (int[] rect2 in rects)
			{
				num += 2;
				for (int i = rect2[0] + 1; i < rect2[1]; i++)
				{
					if (array[rect2[2] * (cols + 1) + i])
					{
						num++;
					}
					if (array[rect2[3] * (cols + 1) + i])
					{
						num++;
					}
				}
				for (int j = rect2[2] + 1; j < rect2[3]; j++)
				{
					if (array[j * (cols + 1) + rect2[0]])
					{
						num++;
					}
					if (array[j * (cols + 1) + rect2[1]])
					{
						num++;
					}
				}
			}
			return num;
		}

		private static List<int[]> Runs(byte[] tone, int cols, int rows, int[] partOf, bool columns)
		{
			List<int[]> list = new List<int[]>();
			Dictionary<long, int[]> dictionary = new Dictionary<long, int[]>();
			Dictionary<long, int[]> dictionary2 = new Dictionary<long, int[]>();
			int num = (columns ? cols : rows);
			int num2 = (columns ? rows : cols);
			for (int i = 0; i < num; i++)
			{
				dictionary2.Clear();
				if (columns && i > 0 && partOf[i] != partOf[i - 1])
				{
					dictionary.Clear();
				}
				int num3 = 0;
				while (num3 < num2)
				{
					byte b = (columns ? tone[num3 * cols + i] : tone[i * cols + num3]);
					int j;
					for (j = num3 + 1; j < num2 && (columns ? tone[j * cols + i] : tone[i * cols + j]) == b && (columns || partOf[j] == partOf[num3]); j++)
					{
					}
					if (b != 0)
					{
						long key = ((long)num3 << 32) | ((long)j << 8) | b;
						if (dictionary.TryGetValue(key, out var value))
						{
							if (columns)
							{
								value[1] = i + 1;
							}
							else
							{
								value[3] = i + 1;
							}
						}
						else
						{
							list.Add(value = ((!columns) ? new int[5]
							{
								num3,
								j,
								i,
								i + 1,
								b
							} : new int[5]
							{
								i,
								i + 1,
								num3,
								j,
								b
							}));
						}
						dictionary2[key] = value;
					}
					num3 = j;
				}
				Dictionary<long, int[]> dictionary3 = dictionary;
				dictionary = dictionary2;
				dictionary2 = dictionary3;
			}
			return list;
		}
	}
}
