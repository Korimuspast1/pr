using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class WallPieces
	{
		public const int North = 1;

		public const int East = 2;

		public const int South = 4;

		public const int West = 8;

		private static readonly string[] Shapes;

		private static readonly int[][] Sides;

		private static readonly int[] Bit;

		private static readonly string[] ShapeOf;

		private static readonly int[] TurnOf;

		private static readonly Dictionary<string, Dictionary<string, string>> FamilyNames;

		private static readonly Dictionary<string, string[]> LodNames;

		public const float Scale = 0.5f;

		public const float NearDetail = 6f;

		public const float MiddleDetail = 18f;

		public const float BoxDetail = 120f;

		private static readonly Dictionary<MeshModel, MeshModel> Boxes;

		static WallPieces()
		{
			Shapes = new string[6] { "post", "end", "straight", "corner", "tee", "cross" };
			Sides = new int[6][]
			{
				new int[0],
				new int[1],
				new int[2] { 0, 2 },
				new int[2] { 0, 1 },
				new int[3] { 0, 1, 2 },
				new int[4] { 0, 1, 2, 3 }
			};
			Bit = new int[4] { 2, 1, 8, 4 };
			ShapeOf = new string[16];
			TurnOf = new int[16];
			FamilyNames = new Dictionary<string, Dictionary<string, string>>();
			LodNames = new Dictionary<string, string[]>();
			Boxes = new Dictionary<MeshModel, MeshModel>();
			for (int i = 0; i < 16; i++)
			{
				for (int j = 0; j < Shapes.Length; j++)
				{
					if (ShapeOf[i] != null)
					{
						break;
					}
					for (int k = 0; k < 4; k++)
					{
						int num = 0;
						int[] array = Sides[j];
						foreach (int num2 in array)
						{
							num |= Bit[(num2 + k) & 3];
						}
						if (num == i)
						{
							ShapeOf[i] = Shapes[j];
							TurnOf[i] = k;
							break;
						}
					}
				}
			}
		}

		public static string Shape(int mask, out int quarterTurns)
		{
			quarterTurns = TurnOf[mask & 0xF];
			return ShapeOf[mask & 0xF];
		}

		public static float Heading(int quarterTurns)
		{
			return (float)quarterTurns * (MathF.PI / 2f) - MathF.PI / 2f;
		}

		public static string Family(string wallType, string shape)
		{
			lock (FamilyNames)
			{
				if (!FamilyNames.TryGetValue(wallType, out var value))
				{
					value = new Dictionary<string, string>();
					FamilyNames[wallType] = value;
				}
				if (!value.TryGetValue(shape, out var value2))
				{
					value2 = (value[shape] = wallType + "_" + shape);
				}
				return value2;
			}
		}

		private static string Lod(string family, int level)
		{
			lock (LodNames)
			{
				if (!LodNames.TryGetValue(family, out var value))
				{
					if (LodNames.Count > 4096)
					{
						LodNames.Clear();
					}
					value = new string[4]
					{
						family,
						family + "_m",
						family + "_f",
						family + "_x"
					};
					LodNames[family] = value;
				}
				return value[level];
			}
		}

		public static string Family(string wallType, string shape, int x, int y)
		{
			if (!(shape == "straight") || ((x + y) & 1) != 1)
			{
				return Family(wallType, shape);
			}
			return Family(wallType, "straight_b");
		}

		public static string Detailed(string family, float distance, string current)
		{
			if (current != null && current.StartsWith(family, StringComparison.Ordinal))
			{
				int num = current.Length - family.Length;
				int num2 = ((num != 0) ? ((num == 2 && current.EndsWith("_m", StringComparison.Ordinal)) ? 1 : ((num == 2 && current.EndsWith("_f", StringComparison.Ordinal)) ? 2 : (-1))) : 0);
				if (num2 == 0 && distance < 7f)
				{
					return current;
				}
				if (num2 == 1 && distance > 5f && distance < 19f)
				{
					return current;
				}
				if (num2 == 2 && distance > 17f)
				{
					return current;
				}
			}
			if (!(distance < 6f))
			{
				return Lod(family, (distance < 18f) ? 1 : 2);
			}
			return family;
		}

		public static MeshModel Pick(Func<string, MeshModel> get, string family, float distance, MeshModel current)
		{
			string text = current?.Name;
			bool flag = text != null && text.Length == family.Length + 2 && text.EndsWith("_x", StringComparison.Ordinal) && text.StartsWith(family, StringComparison.Ordinal);
			if (distance > 120f + (flag ? (-4f) : ((current != null) ? 4f : 0f)))
			{
				if (flag)
				{
					return current;
				}
				MeshModel meshModel = get(Lod(family, 2)) ?? get(family);
				if (meshModel != null)
				{
					return FarBox(meshModel, Lod(family, 3));
				}
			}
			return get(Detailed(family, distance, flag ? null : text)) ?? get(family);
		}

		public static MeshModel FarBox(MeshModel piece, string name)
		{
			lock (Boxes)
			{
				if (Boxes.TryGetValue(piece, out var value))
				{
					return value;
				}
				float x = piece.Min.X;
				float y = piece.Min.Y;
				float z = piece.Min.Z;
				float x2 = piece.Max.X;
				float y2 = piece.Max.Y;
				float num = z + (piece.Max.Z - z) * 0.65f;
				float[] array = new float[256];
				float[] array2 = new float[256];
				float num2 = 0f;
				float num3 = 0f;
				MeshPart[] parts = piece.Parts;
				foreach (MeshPart meshPart in parts)
				{
					float[] pos = meshPart.Pos;
					for (int j = 0; j < meshPart.TriangleCount; j++)
					{
						int num4 = meshPart.Tri[j * 3] * 3;
						int num5 = meshPart.Tri[j * 3 + 1] * 3;
						int num6 = meshPart.Tri[j * 3 + 2] * 3;
						float num7 = pos[num5] - pos[num4];
						float num8 = pos[num5 + 1] - pos[num4 + 1];
						float num9 = pos[num5 + 2] - pos[num4 + 2];
						float num10 = pos[num6] - pos[num4];
						float num11 = pos[num6 + 1] - pos[num4 + 1];
						float num12 = pos[num6 + 2] - pos[num4 + 2];
						float num13 = num8 * num12 - num9 * num11;
						float num14 = num9 * num10 - num7 * num12;
						float num15 = num7 * num11 - num8 * num10;
						float num16 = (float)Math.Sqrt(num13 * num13 + num14 * num14 + num15 * num15);
						if (!(num16 < 1E-09f))
						{
							float num17 = num15 / num16;
							float num18 = (pos[num4 + 2] + pos[num5 + 2] + pos[num6 + 2]) / 3f;
							if (num17 > 0.7f && num18 > num)
							{
								array[meshPart.Slot[j]] += num16;
								num2 += num16;
								num3 += num18 * num16;
							}
							else if (Math.Abs(num17) < 0.3f)
							{
								array2[meshPart.Slot[j]] += num16;
							}
						}
					}
				}
				float num19 = ((num2 > 0f) ? (num3 / num2) : piece.Max.Z);
				byte slot = Most(array);
				byte b = Most(array2);
				if (num2 <= 0f)
				{
					slot = b;
				}
				List<float> pos2 = new List<float>();
				List<float> nrm2 = new List<float>();
				List<int> tri2 = new List<int>();
				List<byte> slot2 = new List<byte>();
				Face(0f, 0f, 1f, slot, new float[12]
				{
					x, y, num19, x2, y, num19, x2, y2, num19, x,
					y2, num19
				});
				Face(0f, -1f, 0f, b, new float[12]
				{
					x, y, z, x2, y, z, x2, y, num19, x,
					y, num19
				});
				Face(0f, 1f, 0f, b, new float[12]
				{
					x, y2, z, x, y2, num19, x2, y2, num19, x2,
					y2, z
				});
				Face(-1f, 0f, 0f, b, new float[12]
				{
					x, y, z, x, y, num19, x, y2, num19, x,
					y2, z
				});
				Face(1f, 0f, 0f, b, new float[12]
				{
					x2, y, z, x2, y2, z, x2, y2, num19, x2,
					y, num19
				});
				MeshPart meshPart2 = new MeshPart
				{
					Name = ((piece.Parts.Length != 0) ? piece.Parts[0].Name : "box"),
					Parent = -1,
					Pos = pos2.ToArray(),
					Nrm = nrm2.ToArray(),
					Tri = tri2.ToArray(),
					Slot = slot2.ToArray()
				};
				meshPart2.Axis = new byte[meshPart2.TriangleCount];
				for (int k = 0; k < meshPart2.Axis.Length; k++)
				{
					meshPart2.Axis[k] = MeshLibrary.DominantAxis(meshPart2, k);
				}
				value = piece.Derive(name, new MeshPart[1] { meshPart2 });
				Boxes[piece] = value;
				return value;
				void Face(float nx, float ny, float nz, byte item, float[] corners)
				{
					int num20 = pos2.Count / 3;
					pos2.AddRange(corners);
					for (int l = 0; l < 4; l++)
					{
						nrm2.Add(nx);
						nrm2.Add(ny);
						nrm2.Add(nz);
					}
					tri2.AddRange(new int[6]
					{
						num20,
						num20 + 1,
						num20 + 2,
						num20,
						num20 + 2,
						num20 + 3
					});
					slot2.Add(item);
					slot2.Add(item);
				}
			}
		}

		private static byte Most(float[] area)
		{
			byte b = 0;
			for (int i = 1; i < area.Length; i++)
			{
				if (area[i] > area[b])
				{
					b = (byte)i;
				}
			}
			return b;
		}
	}
}
