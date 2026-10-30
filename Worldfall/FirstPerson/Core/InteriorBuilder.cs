using System;
using System.Collections.Generic;
using System.Globalization;

namespace FirstPerson.Core
{
	public static class InteriorBuilder
	{
		private sealed class DoorLook
		{
			public string Top = "square";

			public string Frame = "timber";

			public string Hold = "ledges";

			public bool Gold;

			public bool Bar = true;

			public bool Curtain;
		}

		private sealed class DoorKit
		{
			public int Plank;

			public int Plank2;

			public int Gap;

			public int Ledge;

			public int Post;

			public int Beam;

			public int Ashlar;

			public int Iron;

			public int Rope;

			public int Bone;

			public int Hide;
		}

		private sealed class Layout
		{
			private struct Gap
			{
				public char Wall;

				public float A0;

				public float A1;

				public float Z0;

				public float Z1;
			}

			public float WallTop = 9f;

			public Func<float, float, float> CeilingAt;

			private List<Gap> _gaps;

			private readonly List<(char Wall, float A0, float A1, float Top)> _atWall = new List<(char, float, float, float)>();

			private const float ReliefCell = 0.03f;

			private Dictionary<char, float[,]> _relief;

			private Vec3[] _shellTris;

			private const float HeadClear = 1.1f;

			public FloorGrid Floor;

			private readonly List<(WallSide W, float A0, float A1, float Z0, float Z1)> _hungOnRuns = new List<(WallSide, float, float, float, float)>();

			private const float WayCell = 0.05f;

			private const float WayR = 0.3f;

			private const int WayLoss = 48;

			public bool Lenient;

			private bool[] _wayOpen;

			private bool[] _waySeen;

			private int[] _wayStack;

			private int _wayW;

			private int _wayH;

			private int _wayReached = -1;

			private bool[] _wayBase;

			private int _wayBaseStorey = int.MinValue;

			public readonly Interior Room;

			public readonly float X0;

			public readonly float Y0;

			public readonly float X1;

			public readonly float Y1;

			private readonly Func<string, MeshModel> _models;

			private readonly Dictionary<string, uint> _colors;

			private readonly Dictionary<MeshModel, uint[]> _palettes = new Dictionary<MeshModel, uint[]>();

			private readonly List<float[]> _taken = new List<float[]>();

			public float[] Last;

			public float LastX;

			public float LastY;

			public readonly List<(string Use, float X, float Y, float[] Box, float Heading)> Placed = new List<(string, float, float, float[], float)>();

			public float Base;

			public FitInfo Fitted;

			public int Storey;

			private readonly List<string> _takenBy = new List<string>();

			public readonly List<(float[] Box, float Top)> Stood = new List<(float[], float)>();

			private void EnsureGaps()
			{
				if (_gaps != null)
				{
					return;
				}
				_gaps = new List<Gap>();
				foreach (LightWindow window in Room.Windows)
				{
					char c = WallOf(window.Inward);
					if (c != 0)
					{
						float num = ((c == 'N' || c == 'S') ? window.Center.X : window.Center.Y);
						_gaps.Add(new Gap
						{
							Wall = c,
							A0 = num - window.HalfWidth,
							A1 = num + window.HalfWidth,
							Z0 = window.Center.Z - window.HalfHeight,
							Z1 = window.Center.Z + window.HalfHeight
						});
					}
				}
				if (Base < 0.01f)
				{
					_gaps.Add(new Gap
					{
						Wall = 'S',
						A0 = Room.DoorX - 0.45f,
						A1 = Room.DoorX + 0.45f,
						Z0 = 0f,
						Z1 = 1.35f
					});
				}
			}

			private static char WallOf(Vec3 inward)
			{
				if (!(inward.Y > 0.9f))
				{
					if (!(inward.Y < -0.9f))
					{
						if (!(inward.X > 0.9f))
						{
							if (!(inward.X < -0.9f))
							{
								return '\0';
							}
							return 'E';
						}
						return 'W';
					}
					return 'N';
				}
				return 'S';
			}

			private static bool AlongX(char wall)
			{
				if (wall != 'N')
				{
					return wall == 'S';
				}
				return true;
			}

			private static void Span(char wall, float[] box, out float a0, out float a1)
			{
				if (AlongX(wall))
				{
					a0 = box[0];
					a1 = box[2];
				}
				else
				{
					a0 = box[1];
					a1 = box[3];
				}
			}

			private float FromWall(char wall, float[] box)
			{
				switch (wall)
				{
				default:
					return X1 - box[2];
				case 'W':
					return box[0] - X0;
				case 'S':
					return box[1] - Y0;
				case 'N':
					return Y1 - box[3];
				}
			}

			private bool BlocksGap(float[] box, float top, float bottom = 0f)
			{
				EnsureGaps();
				foreach (Gap gap in _gaps)
				{
					if (!(FromWall(gap.Wall, box) > 0.45f) && !(gap.Z1 < bottom + 0.02f))
					{
						Span(gap.Wall, box, out var a, out var a2);
						if (!(a2 <= gap.A0 - 0.05f) && !(a >= gap.A1 + 0.05f) && top > gap.Z0 + 0.02f)
						{
							return true;
						}
					}
				}
				return false;
			}

			private void NoteAtWall(float[] box, float top)
			{
				string text = "NSWE";
				foreach (char c in text)
				{
					if (!(FromWall(c, box) > 0.3f))
					{
						Span(c, box, out var a, out var a2);
						_atWall.Add((c, a, a2, top));
					}
				}
			}

			public float Height(string name)
			{
				MeshModel meshModel = _models(name);
				if (meshModel != null)
				{
					return (meshModel.Max.Z - meshModel.Min.Z) * 0.5714286f;
				}
				return 0f;
			}

			public bool Fit(string name, char wall, float along, float range = 0.8f, float gap = 0.02f)
			{
				int count = Room.Skipped.Count;
				for (float num = 0f; num <= range + 0.001f; num += 0.08f)
				{
					if (AgainstWall(name, wall, along + num, gap) || (num > 0f && AgainstWall(name, wall, along - num, gap)))
					{
						Room.Skipped.RemoveRange(count, Room.Skipped.Count - count);
						return true;
					}
				}
				Settle(count, $"{name} (no place on the {wall} wall near {along:F1}");
				return false;
			}

			private void Settle(int before, string what)
			{
				Dictionary<string, int> dictionary = new Dictionary<string, int>();
				string text = null;
				for (int i = before; i < Room.Skipped.Count; i++)
				{
					string text2 = Room.Skipped[i];
					int num = text2.IndexOf(" (", StringComparison.Ordinal);
					string text3 = ((num >= 0) ? text2.Substring(num + 2).TrimEnd(')') : text2);
					dictionary.TryGetValue(text3, out var value);
					value = (dictionary[text3] = value + 1);
					if (text == null || value > dictionary[text])
					{
						text = text3;
					}
				}
				Room.Skipped.RemoveRange(before, Room.Skipped.Count - before);
				Room.Skipped.Add(what + ((text != null) ? (": mostly " + text) : "") + ")");
			}

			public bool PutNear(string name, float x, float y, float heading, float range = 0.5f, bool solid = true)
			{
				int count = Room.Skipped.Count;
				for (float num = 0f; num <= range + 0.001f; num += 0.1f)
				{
					int num2 = ((num <= 0f) ? 1 : 8);
					for (int i = 0; i < num2; i++)
					{
						float num3 = (float)i * MathF.PI * 0.25f;
						if (Put(name, x + (float)Math.Cos(num3) * num, y + (float)Math.Sin(num3) * num, heading, solid))
						{
							Room.Skipped.RemoveRange(count, Room.Skipped.Count - count);
							return true;
						}
					}
				}
				Settle(count, $"{name} (no place near {x:F1},{y:F1}");
				return false;
			}

			public bool PutNearAny(string name, float heading, float range, params (float X, float Y)[] places)
			{
				int count = Room.Skipped.Count;
				for (int i = 0; i < places.Length; i++)
				{
					var (x, y) = places[i];
					if (PutNear(name, x, y, heading, range))
					{
						Room.Skipped.RemoveRange(count, Room.Skipped.Count - count);
						return true;
					}
				}
				if (Room.Skipped.Count - count > 1)
				{
					Room.Skipped.RemoveRange(count, Room.Skipped.Count - count - 1);
				}
				return false;
			}

			private float[,] ReliefOf(char w)
			{
				if (_relief == null)
				{
					_relief = new Dictionary<char, float[,]>();
					Room.WallRelief = _relief;
					List<Vec3> list = new List<Vec3>();
					MeshInstance shell = Room.Shell;
					if (shell?.Model != null)
					{
						MeshPart[] parts = shell.Model.Parts;
						foreach (MeshPart meshPart in parts)
						{
							Vec3[] array = new Vec3[meshPart.VertexCount];
							for (int j = 0; j < array.Length; j++)
							{
								array[j] = shell.Position + shell.Basis.Mul(new Vec3(meshPart.Pos[j * 3], meshPart.Pos[j * 3 + 1], meshPart.Pos[j * 3 + 2]));
							}
							for (int k = 0; k + 2 < meshPart.Tri.Length; k += 3)
							{
								list.Add(array[meshPart.Tri[k]]);
								list.Add(array[meshPart.Tri[k + 1]]);
								list.Add(array[meshPart.Tri[k + 2]]);
							}
						}
					}
					_shellTris = list.ToArray();
				}
				if (_relief.TryGetValue(w, out var value))
				{
					return value;
				}
				float num = Math.Min(WallTop + 0.3f, 6f);
				float num2 = (AlongX(w) ? (X1 - X0) : (Y1 - Y0));
				value = new float[Math.Max(1, (int)Math.Ceiling(num2 / 0.03f)), Math.Max(1, (int)Math.Ceiling(num / 0.03f))];
				_relief[w] = value;
				Relief(w, value);
				return value;
			}

			private float DepthOf(char w, Vec3 p)
			{
				switch (w)
				{
				default:
					return X1 - p.X;
				case 'W':
					return p.X - X0;
				case 'S':
					return p.Y - Y0;
				case 'N':
					return Y1 - p.Y;
				}
			}

			private float AlongOf(char w, Vec3 p)
			{
				if (!AlongX(w))
				{
					return p.Y - Y0;
				}
				return p.X - X0;
			}

			private void Relief(char w, float[,] grid)
			{
				Vec3[] shellTris = _shellTris;
				Vec3 vec;
				switch (w)
				{
				default:
					vec = new Vec3(-1f, 0f, 0f);
					break;
				case 'W':
					vec = new Vec3(1f, 0f, 0f);
					break;
				case 'S':
					vec = new Vec3(0f, 1f, 0f);
					break;
				case 'N':
					vec = new Vec3(0f, -1f, 0f);
					break;
				}
				Vec3 b = vec;
				float[] array = new float[90];
				for (int i = 0; i + 2 < shellTris.Length; i += 3)
				{
					Vec3 vec2 = shellTris[i];
					Vec3 vec3 = shellTris[i + 1];
					Vec3 vec4 = shellTris[i + 2];
					Vec3 vec5 = Vec3.Cross(vec3 - vec2, vec4 - vec2);
					float num = (float)Math.Sqrt(Vec3.Dot(vec5, vec5));
					if (num < 1E-09f || Vec3.Dot(vec5, b) < 0.9f * num)
					{
						continue;
					}
					float num2 = (vec2.Z + vec3.Z + vec4.Z) / 3f;
					if (!(num2 < 0.15f) && !(num2 > WallTop))
					{
						int num3 = (int)Math.Round((DepthOf(w, (vec2 + vec3 + vec4) * (1f / 3f)) + 0.3f) / 0.01f);
						if (num3 >= 0 && num3 < array.Length)
						{
							array[num3] += num;
						}
					}
				}
				int num4 = 30;
				for (int j = 0; j < array.Length; j++)
				{
					if (array[j] > array[num4])
					{
						num4 = j;
					}
				}
				float num5 = (float)num4 * 0.01f - 0.3f;
				int length = grid.GetLength(0);
				int length2 = grid.GetLength(1);
				for (int k = 0; k + 2 < shellTris.Length; k += 3)
				{
					Vec3 p = shellTris[k];
					Vec3 p2 = shellTris[k + 1];
					Vec3 p3 = shellTris[k + 2];
					float val = DepthOf(w, p);
					float val2 = DepthOf(w, p2);
					float val3 = DepthOf(w, p3);
					float num6 = Math.Max(val, Math.Max(val2, val3));
					float num7 = Math.Min(val, Math.Min(val2, val3));
					if (num6 < num5 + 0.015f || num7 > num5 + 0.4f)
					{
						continue;
					}
					float num8 = Math.Min(num6, num5 + 0.4f) - num5;
					float num9 = AlongOf(w, p) / 0.03f;
					float num10 = p.Z / 0.03f;
					float num11 = AlongOf(w, p2) / 0.03f;
					float num12 = p2.Z / 0.03f;
					float num13 = AlongOf(w, p3) / 0.03f;
					float num14 = p3.Z / 0.03f;
					int num15 = Math.Max(0, (int)Math.Floor(Math.Min(num9, Math.Min(num11, num13))));
					int num16 = Math.Min(length - 1, (int)Math.Floor(Math.Max(num9, Math.Max(num11, num13))));
					int num17 = Math.Max(0, (int)Math.Floor(Math.Min(num10, Math.Min(num12, num14))));
					int num18 = Math.Min(length2 - 1, (int)Math.Floor(Math.Max(num10, Math.Max(num12, num14))));
					if (num15 > num16 || num17 > num18)
					{
						continue;
					}
					float num19 = (num11 - num9) * (num14 - num10) - (num13 - num9) * (num12 - num10);
					for (int l = num17; l <= num18; l++)
					{
						for (int m = num15; m <= num16; m++)
						{
							float num20 = (float)m + 0.5f;
							float num21 = (float)l + 0.5f;
							float num22 = (num11 - num9) * (num21 - num10) - (num12 - num10) * (num20 - num9);
							float num23 = (num13 - num11) * (num21 - num12) - (num14 - num12) * (num20 - num11);
							float num24 = (num9 - num13) * (num21 - num14) - (num10 - num14) * (num20 - num13);
							bool num25;
							if (!(num19 >= 0f))
							{
								if (!(num22 <= 0f) || !(num23 <= 0f))
								{
									continue;
								}
								num25 = num24 <= 0f;
							}
							else
							{
								if (!(num22 >= 0f) || !(num23 >= 0f))
								{
									continue;
								}
								num25 = num24 >= 0f;
							}
							if (num25 && grid[m, l] < num8)
							{
								grid[m, l] = num8;
							}
						}
					}
					Line(grid, num9, num10, num11, num12, num8);
					Line(grid, num11, num12, num13, num14, num8);
					Line(grid, num13, num14, num9, num10, num8);
				}
			}

			private static void Line(float[,] grid, float ax, float ay, float bx, float by, float stand)
			{
				int length = grid.GetLength(0);
				int length2 = grid.GetLength(1);
				int num = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(bx - ax), Math.Abs(by - ay)) * 2f));
				for (int i = 0; i <= num; i++)
				{
					float num2 = (float)i / (float)num;
					int num3 = (int)Math.Floor(ax + (bx - ax) * num2);
					int num4 = (int)Math.Floor(ay + (by - ay) * num2);
					if (num3 >= 0 && num3 < length && num4 >= 0 && num4 < length2 && grid[num3, num4] < stand)
					{
						grid[num3, num4] = stand;
					}
				}
			}

			private bool ReliefFace(char wall, float a0, float a1, float z0, float z1, out float face)
			{
				face = 0f;
				float[,] array = ReliefOf(wall);
				float num = (AlongX(wall) ? X0 : Y0);
				int num2 = Math.Max(0, (int)Math.Floor((a0 - num) / 0.03f));
				int num3 = Math.Min(array.GetLength(0) - 1, (int)Math.Floor((a1 - num) / 0.03f));
				int num4 = Math.Max(0, (int)Math.Floor(z0 / 0.03f));
				int num5 = Math.Min(array.GetLength(1) - 1, (int)Math.Floor(z1 / 0.03f));
				float num6 = 0f;
				float num7 = float.MaxValue;
				int num8 = 0;
				int num9 = 0;
				for (int i = num2; i <= num3; i++)
				{
					float num10 = 0f;
					for (int j = num4; j <= num5; j++)
					{
						float num11 = array[i, j];
						num8++;
						if (num11 > 0.012f)
						{
							num9++;
						}
						if (num11 > num10)
						{
							num10 = num11;
						}
					}
					if (num10 > num6)
					{
						num6 = num10;
					}
					if (num10 < num7)
					{
						num7 = num10;
					}
				}
				if (num6 <= 0.013f)
				{
					return true;
				}
				if (num6 - num7 <= 0.015f && ((float)num9 >= (float)num8 * 0.6f || num6 <= 0.035f))
				{
					face = num6 + 0.004f;
					return true;
				}
				return false;
			}

			private bool HangClear(char wall, float along, float half, float z, float h)
			{
				float face;
				return HangClear(wall, along, half, z, h, out face);
			}

			private bool HangClear(char wall, float along, float half, float z, float h, out float face)
			{
				face = 0f;
				float num = (AlongX(wall) ? X0 : Y0);
				float num2 = (AlongX(wall) ? X1 : Y1);
				if (along - half < num + 0.08f || along + half > num2 - 0.08f || z < 0f || z + h > WallTop - 0.04f)
				{
					return false;
				}
				EnsureGaps();
				foreach (Gap gap in _gaps)
				{
					if (gap.Wall == wall && along + half > gap.A0 - 0.06f && along - half < gap.A1 + 0.06f && z < gap.Z1 + 0.05f && z + h > gap.Z0 - 0.05f)
					{
						return false;
					}
				}
				foreach (var item in _atWall)
				{
					if (item.Wall == wall && along + half > item.A0 - 0.02f && along - half < item.A1 + 0.02f && z < item.Top + 0.05f)
					{
						return false;
					}
				}
				return ReliefFace(wall, along - half - 0.04f, along + half + 0.04f, z - 0.03f, z + h + 0.03f, out face);
			}

			private bool TryHang(string name, char wall, float along, float z)
			{
				MeshModel meshModel = _models(name);
				if (meshModel == null)
				{
					return false;
				}
				float num = (meshModel.Max.X - meshModel.Min.X) * 0.5714286f * 0.5f;
				float num2 = (meshModel.Max.X + meshModel.Min.X) * 0.5f * 0.5714286f;
				float num3 = (meshModel.Max.Z - meshModel.Min.Z) * 0.5714286f;
				if (!HangClear(wall, along, num, z, num3, out var face))
				{
					return false;
				}
				float num4 = meshModel.Max.Y * 0.5714286f + 0.004f + face;
				float num5;
				float num6;
				float num7;
				switch (wall)
				{
				case 'N':
					num5 = along - num2;
					num6 = Y1 - num4;
					num7 = -MathF.PI / 2f;
					break;
				case 'S':
					num5 = along + num2;
					num6 = Y0 + num4;
					num7 = MathF.PI / 2f;
					break;
				case 'W':
					num5 = X0 + num4;
					num6 = along - num2;
					num7 = 0f;
					break;
				default:
					num5 = X1 - num4;
					num6 = along + num2;
					num7 = MathF.PI;
					break;
				}
				MeshInstance meshInstance = new MeshInstance
				{
					Model = meshModel,
					Palette = PaletteOf(meshModel),
					NoShadow = true,
					Static = true,
					Tag = -1
				};
				meshInstance.Place(num5, num6, z - meshModel.Min.Z * 0.5714286f, num7, 0.5714286f, 0.5714286f, 0.5714286f);
				Room.Meshes.Add(meshInstance);
				AddLight(meshModel, meshInstance);
				_gaps.Add(new Gap
				{
					Wall = wall,
					A0 = along - num,
					A1 = along + num,
					Z0 = z,
					Z1 = z + num3
				});
				Placed.Add((meshModel.Info("use") ?? "", num5, num6, Footprint(meshModel, num5, num6, num7), num7));
				return true;
			}

			private bool TryHangCentred(string name, char wall, float along, float z, float range)
			{
				MeshModel meshModel = _models(name);
				if (meshModel == null)
				{
					return false;
				}
				float num = (meshModel.Max.X - meshModel.Min.X) * 0.5714286f * 0.5f;
				float h = (meshModel.Max.Z - meshModel.Min.Z) * 0.5714286f;
				float num2 = (AlongX(wall) ? X0 : Y0);
				float num3 = (AlongX(wall) ? X1 : Y1);
				float num4 = float.MaxValue;
				float along2 = 0f;
				float z2 = 0f;
				bool flag = false;
				for (int i = 0; i <= 14; i++)
				{
					float num5 = ((i <= 8) ? (z - (float)i * 0.04f) : (z + (float)(i - 8) * 0.04f));
					if (num5 < 0.2f)
					{
						continue;
					}
					int num6 = (int)((num3 - num2) / 0.03f);
					int num7 = -1;
					for (int j = 0; j <= num6; j++)
					{
						float along3 = num2 + (float)j * 0.03f;
						int num8;
						if (j < num6)
						{
							num8 = (HangClear(wall, along3, 0.001f, num5, h) ? 1 : 0);
							if (num8 != 0 && num7 < 0)
							{
								num7 = j;
							}
						}
						else
						{
							num8 = 0;
						}
						if (num8 != 0 || num7 < 0)
						{
							continue;
						}
						float num9 = num2 + (float)num7 * 0.03f;
						float num10 = num2 + (float)(j - 1) * 0.03f;
						num7 = -1;
						if (num10 - num9 < 2f * num)
						{
							continue;
						}
						float num11 = num9 + num;
						float num12 = num10 - num;
						float num13 = ((along < num11) ? (num11 - along) : ((along > num12) ? (along - num12) : 0f));
						bool flag2 = num13 <= range;
						if (flag && !flag2)
						{
							continue;
						}
						float num14 = ((num10 - num9 <= Math.Max(1.7f, num * 5f)) ? ((num11 + num12) * 0.5f) : Math.Max(num11, Math.Min(num12, along)));
						if (HangClear(wall, num14, num, num5, h))
						{
							float num15 = Math.Abs(num14 - along) * 0.5f + num13 + Math.Abs(num5 - z) * 2f;
							if ((flag2 && !flag) || num15 < num4)
							{
								num4 = num15;
								along2 = num14;
								z2 = num5;
								flag = flag2;
							}
						}
					}
				}
				if (num4 < float.MaxValue)
				{
					return TryHang(name, wall, along2, z2);
				}
				return false;
			}

			public bool HangAny(string name, float z, params (char Wall, float Along)[] places)
			{
				return HangAny(name, z, centred: true, places);
			}

			public bool HangAny(string name, float z, bool centred, params (char Wall, float Along)[] places)
			{
				z += Base;
				for (int i = 0; i < places.Length; i++)
				{
					var (wall, along) = places[i];
					if (centred ? TryHangCentred(name, wall, along, z, 0.6f) : TryHangNear(name, wall, along, z, 0.6f))
					{
						return true;
					}
				}
				if (_models(name) != null)
				{
					Room.Skipped.Add(name + " (nowhere to hang)");
				}
				return false;
			}

			public bool Hang(string name, char wall, float along, float z, float range = 0.6f, bool centred = false)
			{
				z += Base;
				if (centred)
				{
					if (TryHangCentred(name, wall, along, z, range))
					{
						return true;
					}
				}
				else if (TryHangNear(name, wall, along, z, range) || TryHangNear(name, wall, along, z, range * 2f + 0.3f))
				{
					return true;
				}
				if (_models(name) != null)
				{
					Room.Skipped.Add($"{name} (nowhere to hang on the {wall} wall near {along:F1})");
				}
				return false;
			}

			private bool TryHangNear(string name, char wall, float along, float z, float range)
			{
				for (int i = 0; i <= 4; i++)
				{
					for (float num = 0f; num <= range + 0.001f; num += 0.03f)
					{
						if (TryHang(name, wall, along + num, z - (float)i * 0.04f) || (num > 0f && TryHang(name, wall, along - num, z - (float)i * 0.04f)))
						{
							return true;
						}
					}
				}
				return false;
			}

			public bool Ceiling(string name, float x, float y, float z, float heading = 0f, uint cord = 0u)
			{
				MeshModel meshModel = _models(name);
				if (meshModel == null)
				{
					return false;
				}
				if (Fitted != null)
				{
					if (ByStair(Fitted, Storey, new Vec3(x, y, 0f)))
					{
						Room.Skipped.Add(name + " (over the stair)");
						return false;
					}
					if (z < 1.1f && !OverSolid(x, y))
					{
						Room.Skipped.Add(name + " (at head height in the way)");
						return false;
					}
				}
				z += Base;
				float num = z + (meshModel.Max.Z - meshModel.Min.Z) * 0.5714286f;
				float num2 = ((CeilingAt != null) ? CeilingAt(x, y) : WallTop);
				if (num > num2 - 0.02f)
				{
					Room.Skipped.Add(name + " (no room under the ceiling)");
					return false;
				}
				MeshInstance meshInstance = new MeshInstance
				{
					Model = meshModel,
					Palette = PaletteOf(meshModel),
					NoShadow = true,
					Static = true,
					Tag = -1
				};
				meshInstance.Place(x, y, z - meshModel.Min.Z * 0.5714286f, heading, 0.5714286f, 0.5714286f, 0.5714286f);
				Room.Meshes.Add(meshInstance);
				AddLight(meshModel, meshInstance);
				if (num2 - num > 0.02f)
				{
					MeshBuilder meshBuilder = new MeshBuilder("cord");
					int slot = meshBuilder.Slot("cord", 10, (cord != 0) ? cord : TerrainCache.Rgb(58, 56, 60), 0);
					meshBuilder.Part("cord", -1, new Vec3(0f, 0f, 0f));
					meshBuilder.Tube(slot, new Vec3(x, y, num - 0.01f), new Vec3(x, y, num2 + 0.03f), 0.006f, 0.006f, 4);
					Room.Meshes.Add(new MeshInstance
					{
						Model = meshBuilder.Build(),
						NoShadow = true,
						Static = true,
						Tag = -1
					});
				}
				Placed.Add((meshModel.Info("use") ?? "", x, y, Footprint(meshModel, x, y, heading), heading));
				return true;
			}

			public bool Has(string name)
			{
				return _models(name) != null;
			}

			private bool OverSolid(float x, float y)
			{
				foreach (float[] solid in Room.Solids)
				{
					if (!((solid.Length >= 6) ? (Math.Abs(solid[4] - Base) > 0.05f) : (Base > 0.01f)) && x >= solid[0] && x <= solid[2] && y >= solid[1] && y <= solid[3])
					{
						return true;
					}
				}
				return false;
			}

			public MeshModel Model(string name)
			{
				return _models(name);
			}

			public uint[] PaletteFor(MeshModel m)
			{
				return PaletteOf(m);
			}

			public float Clearance(float x, float y, float most)
			{
				float num = most;
				foreach (float[] item in _taken)
				{
					float num2 = Math.Max(0f, Math.Max(item[0] - x, x - item[2]));
					float num3 = Math.Max(0f, Math.Max(item[1] - y, y - item[3]));
					num = Math.Min(num, (float)Math.Sqrt(num2 * num2 + num3 * num3));
				}
				if (Floor != null)
				{
					num = Math.Min(num, Floor.Edge(x, y));
				}
				return num;
			}

			public bool WallFree(WallSide w, float a, float half, float z, float h)
			{
				Vec3 vec = w.At(a, 0f);
				foreach (var hungOnRun in _hungOnRuns)
				{
					if (hungOnRun.W == w && a + half > hungOnRun.A0 - 0.06f && a - half < hungOnRun.A1 + 0.06f && z < hungOnRun.Z1 + 0.05f && z + h > hungOnRun.Z0 - 0.05f)
					{
						return false;
					}
				}
				foreach (var item in Stood)
				{
					var (array, _) = item;
					if (!(item.Top < z + 0.03f))
					{
						float num = Math.Max(array[0], Math.Min(array[2], vec.X));
						float num2 = Math.Max(array[1], Math.Min(array[3], vec.Y));
						if ((float)Math.Sqrt((num - vec.X) * (num - vec.X) + (num2 - vec.Y) * (num2 - vec.Y)) < half + 0.1f)
						{
							return false;
						}
					}
				}
				return true;
			}

			public bool Free(float[] box)
			{
				foreach (float[] item in _taken)
				{
					if (box[0] < item[2] - 0.03f && box[2] > item[0] + 0.03f && box[1] < item[3] - 0.03f && box[3] > item[1] + 0.03f)
					{
						return false;
					}
				}
				return true;
			}

			public bool HungNear(WallSide w, float a, float reach)
			{
				foreach (var hungOnRun in _hungOnRuns)
				{
					if (hungOnRun.W == w && hungOnRun.A1 > a - reach && hungOnRun.A0 < a + reach)
					{
						return true;
					}
				}
				return false;
			}

			public bool KeepsWay(float[] box)
			{
				if (Fitted == null || Floor == null)
				{
					return true;
				}
				WayBase();
				if (!Touches(box))
				{
					return false;
				}
				int covered;
				int num = Flood(box, out covered);
				return _wayReached - covered - num <= (Lenient ? 192 : 48);
			}

			private void WayBase()
			{
				if (_wayOpen == null)
				{
					BuildWay();
				}
				if (_wayReached < 0 || _wayBaseStorey != Storey)
				{
					_wayReached = Flood(null, out var _);
					if (_wayBase == null || _wayBase.Length != _waySeen.Length)
					{
						_wayBase = new bool[_waySeen.Length];
					}
					Array.Copy(_waySeen, _wayBase, _waySeen.Length);
					_wayBaseStorey = Storey;
				}
			}

			public bool Reached(float x, float y)
			{
				if (Fitted == null || Floor == null)
				{
					return true;
				}
				WayBase();
				int num = (int)Math.Round((x - X0) / 0.05f);
				int num2 = (int)Math.Round((y - Y0) / 0.05f);
				if (num >= 0 && num2 >= 0 && num < _wayW && num2 < _wayH)
				{
					return _wayBase[num2 * _wayW + num];
				}
				return false;
			}

			private bool Touches(float[] box)
			{
				float num = 0.4f;
				int num2 = Math.Max(0, (int)Math.Floor((box[0] - num - X0) / 0.05f));
				int num3 = Math.Min(_wayW - 1, (int)Math.Ceiling((box[2] + num - X0) / 0.05f));
				int num4 = Math.Max(0, (int)Math.Floor((box[1] - num - Y0) / 0.05f));
				int num5 = Math.Min(_wayH - 1, (int)Math.Ceiling((box[3] + num - Y0) / 0.05f));
				for (int i = num4; i <= num5; i++)
				{
					for (int j = num2; j <= num3; j++)
					{
						if (_wayBase[i * _wayW + j])
						{
							return true;
						}
					}
				}
				return false;
			}

			private void WayTaken(float[] box)
			{
				if (_wayOpen != null)
				{
					WayClose(box);
					_wayReached = -1;
				}
			}

			private void BuildWay()
			{
				_wayW = (int)((X1 - X0) / 0.05f) + 1;
				_wayH = (int)((Y1 - Y0) / 0.05f) + 1;
				_wayOpen = new bool[_wayW * _wayH];
				_waySeen = new bool[_wayW * _wayH];
				_wayStack = new int[_wayW * _wayH];
				FitInfo fitted = Fitted;
				bool flag = !float.IsNaN(fitted.StairFootX);
				float num = 0f;
				float num2 = 0f;
				float num3 = 0f;
				if (flag)
				{
					num = fitted.StairHeadX - fitted.StairFootX;
					num2 = fitted.StairHeadY - fitted.StairFootY;
					num3 = (float)Math.Sqrt(num * num + num2 * num2);
					num /= num3;
					num2 /= num3;
				}
				for (int i = 0; i < _wayH; i++)
				{
					for (int j = 0; j < _wayW; j++)
					{
						float num4 = X0 + (float)j * 0.05f;
						float num5 = Y0 + (float)i * 0.05f;
						bool flag2 = Floor.Clear(num4, num5, 0.3f);
						if (flag2 && flag && Storey == 0)
						{
							float num6 = (num4 - fitted.StairFootX) * num + (num5 - fitted.StairFootY) * num2;
							float num7 = Math.Abs((num4 - fitted.StairFootX) * (0f - num2) + (num5 - fitted.StairFootY) * num);
							if (num6 > 0f && num6 < num3 && num7 < fitted.StairWidth * 0.5f)
							{
								flag2 = false;
							}
						}
						if (flag2 && Storey > 0 && fitted.Stairwell != null)
						{
							float[] stairwell = fitted.Stairwell;
							if (num4 > stairwell[0] - 0.3f && num4 < stairwell[2] + 0.3f && num5 > stairwell[1] - 0.3f && num5 < stairwell[3] + 0.3f)
							{
								flag2 = false;
							}
						}
						if (!flag2 && Storey < fitted.Plan.Storeys.Count)
						{
							foreach (HousePlan.P2 passage in fitted.Plan.Storeys[Storey].Passages)
							{
								float num8 = fitted.RoomX(passage.X);
								float num9 = fitted.RoomY(passage.Y);
								if (Math.Abs(num4 - num8) < 0.6f && Math.Abs(num5 - num9) < 0.6f && Floor.Clear(num4, num5, 0.15f))
								{
									flag2 = true;
								}
							}
						}
						_wayOpen[i * _wayW + j] = flag2;
					}
				}
				foreach (float[] solid in Room.Solids)
				{
					if ((solid.Length >= 6) ? (Math.Abs(solid[4] - Base) < 0.3f) : (Base < 0.01f))
					{
						WayClose(solid);
					}
				}
			}

			private void WayBounds(float[] box, out int i0, out int i1, out int j0, out int j1)
			{
				i0 = Math.Max(0, (int)Math.Ceiling((box[0] - 0.3f - X0) / 0.05f));
				i1 = Math.Min(_wayW - 1, (int)Math.Floor((box[2] + 0.3f - X0) / 0.05f));
				j0 = Math.Max(0, (int)Math.Ceiling((box[1] - 0.3f - Y0) / 0.05f));
				j1 = Math.Min(_wayH - 1, (int)Math.Floor((box[3] + 0.3f - Y0) / 0.05f));
			}

			private void WayClose(float[] box)
			{
				WayBounds(box, out var i, out var i2, out var j, out var j2);
				for (int k = j; k <= j2; k++)
				{
					for (int l = i; l <= i2; l++)
					{
						_wayOpen[k * _wayW + l] = false;
					}
				}
			}

			private int Flood(float[] box, out int covered)
			{
				int num = 0;
				Array.Clear(_waySeen, 0, _waySeen.Length);
				if (box != null)
				{
					WayBounds(box, out var i, out var i2, out var j, out var j2);
					for (int k = j; k <= j2; k++)
					{
						for (int l = i; l <= i2; l++)
						{
							int num2 = k * _wayW + l;
							if (_wayOpen[num2])
							{
								num++;
								_waySeen[num2] = true;
							}
						}
					}
				}
				covered = num;
				FitInfo fitted = Fitted;
				float num3 = Room.SpawnX;
				float num4 = Room.SpawnY;
				if (Storey > 0 && !float.IsNaN(fitted.StairFootX))
				{
					float num5 = fitted.StairHeadX - fitted.StairFootX;
					float num6 = fitted.StairHeadY - fitted.StairFootY;
					float num7 = (float)Math.Sqrt(num5 * num5 + num6 * num6);
					num3 = fitted.StairHeadX + num5 / num7 * 0.35f;
					num4 = fitted.StairHeadY + num6 / num7 * 0.35f;
				}
				int num8 = (int)Math.Round((num3 - X0) / 0.05f);
				int num9 = (int)Math.Round((num4 - Y0) / 0.05f);
				int num10 = -1;
				int num11 = int.MaxValue;
				for (int m = 0; m < _wayH; m++)
				{
					for (int n = 0; n < _wayW; n++)
					{
						int num12 = m * _wayW + n;
						if (_wayOpen[num12] && !_waySeen[num12])
						{
							int num13 = (n - num8) * (n - num8) + (m - num9) * (m - num9);
							if (num13 < num11)
							{
								num11 = num13;
								num10 = num12;
							}
						}
					}
				}
				if (num10 < 0)
				{
					return 0;
				}
				int num14 = 0;
				int num15 = 0;
				_wayStack[num15++] = num10;
				_waySeen[num10] = true;
				while (num15 > 0)
				{
					int num16 = _wayStack[--num15];
					num14++;
					int num17 = num16 % _wayW;
					int num18 = num16 / _wayW;
					if (num17 > 0 && _wayOpen[num16 - 1] && !_waySeen[num16 - 1])
					{
						_waySeen[num16 - 1] = true;
						_wayStack[num15++] = num16 - 1;
					}
					if (num17 < _wayW - 1 && _wayOpen[num16 + 1] && !_waySeen[num16 + 1])
					{
						_waySeen[num16 + 1] = true;
						_wayStack[num15++] = num16 + 1;
					}
					if (num18 > 0 && _wayOpen[num16 - _wayW] && !_waySeen[num16 - _wayW])
					{
						_waySeen[num16 - _wayW] = true;
						_wayStack[num15++] = num16 - _wayW;
					}
					if (num18 < _wayH - 1 && _wayOpen[num16 + _wayW] && !_waySeen[num16 + _wayW])
					{
						_waySeen[num16 + _wayW] = true;
						_wayStack[num15++] = num16 + _wayW;
					}
				}
				return num14;
			}

			public void Hung(MeshModel m, MeshInstance inst, WallSide w, float a, float half, float z, float h)
			{
				AddLight(m, inst);
				_hungOnRuns.Add((w, a - half, a + half, z, z + h));
				Placed.Add((m.Info("use") ?? "", inst.Position.X, inst.Position.Y, new float[4]
				{
					inst.Position.X - 0.05f,
					inst.Position.Y - 0.05f,
					inst.Position.X + 0.05f,
					inst.Position.Y + 0.05f
				}, 0f));
			}

			private bool InFit(float[] b)
			{
				float item = (b[0] + b[2]) * 0.5f;
				float item2 = (b[1] + b[3]) * 0.5f;
				(float, float)[] array = new(float, float)[9]
				{
					(b[0] + 0.02f, b[1] + 0.02f),
					(b[2] - 0.02f, b[1] + 0.02f),
					(b[0] + 0.02f, b[3] - 0.02f),
					(b[2] - 0.02f, b[3] - 0.02f),
					(item, b[1] + 0.02f),
					(item, b[3] - 0.02f),
					(b[0] + 0.02f, item2),
					(b[2] - 0.02f, item2),
					(item, item2)
				};
				for (int i = 0; i < array.Length; i++)
				{
					var (rx, ry) = array[i];
					if (!Fitted.Inside(Storey, rx, ry))
					{
						return false;
					}
				}
				return true;
			}

			public Layout(Interior room, Func<string, MeshModel> models, Dictionary<string, uint> colors, float x0, float y0, float x1, float y1)
			{
				Room = room;
				_models = models;
				_colors = colors;
				X0 = x0;
				Y0 = y0;
				X1 = x1;
				Y1 = y1;
				if (room?.Fit == null)
				{
					KeepDoorBar();
				}
			}

			public void KeepDoorBar()
			{
				float[] array = Room?.DoorBarKeep;
				if (array != null)
				{
					_taken.Add(new float[4]
					{
						array[0],
						array[1],
						array[2],
						array[3]
					});
					_takenBy.Add("the door's bar");
				}
			}

			public void Keep(float x0, float y0, float x1, float y1)
			{
				_taken.Add(new float[4] { x0, y0, x1, y1 });
				_takenBy.Add("the way kept clear");
			}

			public void RoundLast()
			{
				if (Last != null && _taken.Count != 0)
				{
					float num = (Last[0] + Last[2]) * 0.5f;
					float num2 = (Last[1] + Last[3]) * 0.5f;
					float num3 = (Last[2] - Last[0]) * 0.5f * 0.72f;
					_taken[_taken.Count - 1] = new float[4]
					{
						num - num3,
						num2 - num3,
						num + num3,
						num2 + num3
					};
					Room.Solids[Room.Solids.Count - 1] = new float[4]
					{
						num - num3 + 0.03f,
						num2 - num3 + 0.03f,
						num + num3 - 0.03f,
						num2 + num3 - 0.03f
					};
				}
			}

			public float Width(string name)
			{
				MeshModel meshModel = _models(name);
				if (meshModel != null)
				{
					return (meshModel.Max.X - meshModel.Min.X) * 0.5714286f;
				}
				return 0f;
			}

			public float Depth(string name)
			{
				MeshModel meshModel = _models(name);
				if (meshModel != null)
				{
					return (meshModel.Max.Y - meshModel.Min.Y) * 0.5714286f;
				}
				return 0f;
			}

			public bool Put(string name, float x, float y, float heading, bool solid = true, bool anywhere = false, float z = 0f)
			{
				Last = null;
				MeshModel meshModel = _models(name);
				if (meshModel == null)
				{
					return false;
				}
				z += Base;
				float[] array = Footprint(meshModel, x, y, heading);
				if (array[0] < X0 - 0.01f || array[2] > X1 + 0.01f || array[1] < Y0 - 0.01f || array[3] > Y1 + 0.01f)
				{
					Room.Skipped.Add(name + " (out of the room)");
					return false;
				}
				if (Fitted != null && !InFit(array))
				{
					Room.Skipped.Add(name + " (through a wall)");
					return false;
				}
				float num = z + meshModel.Max.Z * 0.5714286f;
				if (!anywhere)
				{
					for (int i = 0; i < _taken.Count; i++)
					{
						float[] array2 = _taken[i];
						if (array[0] < array2[2] - 0.03f && array[2] > array2[0] + 0.03f && array[1] < array2[3] - 0.03f && array[3] > array2[1] + 0.03f)
						{
							Room.Skipped.Add(name + " (on " + ((i < _takenBy.Count) ? _takenBy[i] : "?") + ")");
							return false;
						}
					}
					if (BlocksGap(array, num, z))
					{
						Room.Skipped.Add(name + " (before a window)");
						return false;
					}
					if (solid && Fitted != null && !KeepsWay(array))
					{
						Room.Skipped.Add(name + " (would shut a way)");
						return false;
					}
				}
				MeshInstance meshInstance = new MeshInstance
				{
					Model = meshModel,
					Palette = PaletteOf(meshModel),
					NoShadow = true,
					Static = true,
					Tag = -1
				};
				meshInstance.Place(x, y, z, heading, 0.5714286f, 0.5714286f, 0.5714286f);
				Room.Meshes.Add(meshInstance);
				if (solid)
				{
					_taken.Add(array);
					_takenBy.Add(name);
					float num2 = Math.Abs((float)Math.Sin(2f * heading));
					float num3 = 0.03f + num2 * 0.18f * Math.Min(array[2] - array[0], array[3] - array[1]) * 0.5f;
					Room.Solids.Add((!(Base > 0.01f)) ? new float[4]
					{
						array[0] + num3,
						array[1] + num3,
						array[2] - num3,
						array[3] - num3
					} : new float[6]
					{
						array[0] + num3,
						array[1] + num3,
						array[2] - num3,
						array[3] - num3,
						z,
						num
					});
					if (Fitted != null)
					{
						WayTaken(Room.Solids[Room.Solids.Count - 1]);
					}
					NoteAtWall(array, num);
				}
				AddLight(meshModel, meshInstance);
				Last = array;
				LastX = x;
				LastY = y;
				Placed.Add((meshModel.Info("use") ?? "", x, y, array, heading));
				if (solid)
				{
					Stood.Add((array, num));
				}
				return true;
			}

			public bool AgainstWall(string name, char wall, float along, float gap = 0.02f)
			{
				MeshModel meshModel = _models(name);
				if (meshModel == null)
				{
					return false;
				}
				float num = meshModel.Max.Y * 0.5714286f + gap;
				switch (wall)
				{
				case 'N':
					return Put(name, along, Y1 - num, -MathF.PI / 2f);
				case 'S':
					return Put(name, along, Y0 + num, MathF.PI / 2f);
				case 'W':
					return Put(name, X0 + num, along, 0f);
				default:
					return Put(name, X1 - num, along, MathF.PI);
				}
			}

			public bool Next(string name, char wall, ref float cursor, float gap = 0.15f)
			{
				float num = Width(name);
				if (num <= 0f)
				{
					return false;
				}
				if (AgainstWall(name, wall, cursor + num * 0.5f))
				{
					cursor += num + gap;
					return true;
				}
				cursor += 0.25f;
				return false;
			}

			private static float[] Footprint(MeshModel m, float x, float y, float heading)
			{
				float num = (float)Math.Sin(heading);
				float num2 = (float)Math.Cos(heading);
				float num3 = float.MaxValue;
				float num4 = float.MaxValue;
				float num5 = float.MinValue;
				float num6 = float.MinValue;
				for (int i = 0; i < 4; i++)
				{
					float num7 = (((i & 1) == 0) ? m.Min.X : m.Max.X);
					float num8 = (((i & 2) == 0) ? m.Min.Y : m.Max.Y);
					float val = x + ((0f - num7) * num - num8 * num2) * 0.5714286f;
					float val2 = y + (num7 * num2 - num8 * num) * 0.5714286f;
					num3 = Math.Min(num3, val);
					num5 = Math.Max(num5, val);
					num4 = Math.Min(num4, val2);
					num6 = Math.Max(num6, val2);
				}
				return new float[4] { num3, num4, num5, num6 };
			}

			private uint[] PaletteOf(MeshModel m)
			{
				if (_palettes.TryGetValue(m, out var value))
				{
					return value;
				}
				value = new uint[m.SlotNames.Length];
				for (int i = 0; i < value.Length; i++)
				{
					value[i] = (_colors.TryGetValue(m.SlotNames[i], out var value2) ? value2 : m.SlotColor[i]);
				}
				_palettes[m] = value;
				return value;
			}

			private void AddLight(MeshModel m, MeshInstance inst)
			{
				string text = m.Info("light");
				if (text == null)
				{
					return;
				}
				string[] array = text.Split(',');
				if (array.Length != 3)
				{
					return;
				}
				float x = float.Parse(array[0], CultureInfo.InvariantCulture);
				float y = float.Parse(array[1], CultureInfo.InvariantCulture);
				float z = float.Parse(array[2], CultureInfo.InvariantCulture);
				Vec3 vec = inst.Position + inst.Basis.Mul(new Vec3(x, y, z));
				string text2 = m.Info("use") ?? "";
				bool flag = Room.Race == "elf";
				PointLight light = new PointLight
				{
					X = vec.X,
					Y = vec.Y,
					Z = vec.Z
				};
				bool fire = true;
				if (text2 == "hearth")
				{
					light.Radius = 5.5f;
					light.R = 0.95f;
					light.G = 0.64f;
					light.B = 0.36f;
					light.Glow = 0.12f;
					if (flag)
					{
						light.R = 0.9f;
						light.G = 0.72f;
						light.B = 0.46f;
					}
					Room.HearthX = vec.X;
					Room.HearthY = vec.Y;
				}
				else if (text2 == "lamp" || flag)
				{
					light.Radius = ((text2 == "sconce" || text2 == "sidetable") ? 2.2f : 3.4f);
					light.R = 0.68f;
					light.G = 0.76f;
					light.B = 0.56f;
					light.Glow = 0.07f;
					fire = false;
				}
				else if (text2 == "chandelier")
				{
					light.Radius = 3.6f;
					light.R = 0.62f;
					light.G = 0.44f;
					light.B = 0.24f;
					light.Glow = 0.06f;
				}
				else if (text2 == "sconce")
				{
					light.Radius = 2f;
					light.R = 0.5f;
					light.G = 0.34f;
					light.B = 0.18f;
					light.Glow = 0.04f;
				}
				else
				{
					light.Radius = 2.4f;
					light.R = 0.55f;
					light.G = 0.38f;
					light.B = 0.2f;
					light.Glow = 0.04f;
				}
				Room.Lights.Add(new RoomLight
				{
					Light = light,
					Fire = fire
				});
			}
		}

		private sealed class FloorsKept
		{
			private const float C = 0.05f;

			private const float R = 0.23f;

			private const int Loss = 48;

			private readonly float _x0;

			private readonly float _y0;

			private readonly int _w;

			private readonly int _h;

			private readonly int _door;

			private readonly int _base0;

			private readonly int _base1;

			private readonly bool[] _open0;

			private readonly bool[] _open1;

			private readonly bool[] _seen;

			private readonly int[] _stack;

			public FloorsKept(FloorGrid g0, FloorGrid g1, float x0, float y0, float x1, float y1, float doorX, float doorY)
			{
				_x0 = x0;
				_y0 = y0;
				_w = (int)((x1 - x0) / 0.05f) + 1;
				_h = (int)((y1 - y0) / 0.05f) + 1;
				_open0 = new bool[_w * _h];
				_open1 = new bool[_w * _h];
				_seen = new bool[_w * _h];
				_stack = new int[_w * _h];
				for (int i = 0; i < _h; i++)
				{
					for (int j = 0; j < _w; j++)
					{
						float x2 = x0 + (float)j * 0.05f;
						float y2 = y0 + (float)i * 0.05f;
						_open0[i * _w + j] = g0.Clear(x2, y2, 0.23f);
						_open1[i * _w + j] = g1.Clear(x2, y2, 0.23f);
					}
				}
				_door = Nearest(_open0, doorX, doorY, 16, null);
				_base0 = ((_door >= 0) ? Flood(_open0, _door, null) : 0);
				Array.Clear(_seen, 0, _seen.Length);
				for (int k = 0; k < _open1.Length; k++)
				{
					if (_open1[k] && !_seen[k])
					{
						_base1 = Math.Max(_base1, Fill(_open1, k, null));
					}
				}
			}

			public bool Whole(Vec3 foot, Vec3 d, float length, float width, float[] well)
			{
				if (_door < 0)
				{
					return true;
				}
				float hw = width * 0.5f + 0.23f;
				Func<float, float, bool> blocked = delegate(float x, float y)
				{
					float num7 = (x - foot.X) * d.X + (y - foot.Y) * d.Y;
					float num8 = Math.Abs((x - foot.X) * (0f - d.Y) + (y - foot.Y) * d.X);
					return num7 > -0.05f && num7 < length + 0.05f && num8 < hw;
				};
				int num = Flood(_open0, _door, blocked);
				int num2 = Covered(_open0, blocked);
				if (num < _base0 - num2 - 48)
				{
					return false;
				}
				int num3 = Nearest(_open0, foot.X - d.X * 0.35f, foot.Y - d.Y * 0.35f, 4, blocked);
				if (num3 < 0 || !_seen[num3])
				{
					return false;
				}
				Func<float, float, bool> blocked2 = (float x, float y) => x > well[0] - 0.23f && x < well[2] + 0.23f && y > well[1] - 0.23f && y < well[3] + 0.23f;
				int num4 = Nearest(_open1, foot.X + d.X * (length + 0.35f), foot.Y + d.Y * (length + 0.35f), 6, blocked2);
				if (num4 < 0)
				{
					return false;
				}
				int num5 = Flood(_open1, num4, blocked2);
				int num6 = Covered(_open1, blocked2);
				return num5 >= _base1 - num6 - 48;
			}

			private int Covered(bool[] open, Func<float, float, bool> blocked)
			{
				int num = 0;
				for (int i = 0; i < open.Length; i++)
				{
					if (open[i] && blocked(_x0 + (float)(i % _w) * 0.05f, _y0 + (float)(i / _w) * 0.05f))
					{
						num++;
					}
				}
				return num;
			}

			private int Nearest(bool[] open, float x, float y, int reach, Func<float, float, bool> blocked)
			{
				int num = (int)Math.Round((x - _x0) / 0.05f);
				int num2 = (int)Math.Round((y - _y0) / 0.05f);
				int result = -1;
				int num3 = int.MaxValue;
				for (int i = Math.Max(0, num2 - reach); i <= Math.Min(_h - 1, num2 + reach); i++)
				{
					for (int j = Math.Max(0, num - reach); j <= Math.Min(_w - 1, num + reach); j++)
					{
						int num4 = i * _w + j;
						if (open[num4] && (blocked == null || !blocked(_x0 + (float)j * 0.05f, _y0 + (float)i * 0.05f)))
						{
							int num5 = (j - num) * (j - num) + (i - num2) * (i - num2);
							if (num5 < num3)
							{
								num3 = num5;
								result = num4;
							}
						}
					}
				}
				return result;
			}

			private int Flood(bool[] open, int start, Func<float, float, bool> blocked)
			{
				Array.Clear(_seen, 0, _seen.Length);
				return Fill(open, start, blocked);
			}

			private int Fill(bool[] open, int start, Func<float, float, bool> blocked)
			{
				int num = 0;
				int num2 = 0;
				_stack[num2++] = start;
				_seen[start] = true;
				while (num2 > 0)
				{
					int num3 = _stack[--num2];
					num++;
					int num4 = num3 % _w;
					int num5 = num3 / _w;
					for (int i = 0; i < 4; i++)
					{
						int num6;
						switch (i)
						{
						default:
							num6 = ((num5 < _h - 1) ? (num3 + _w) : (-1));
							break;
						case 2:
							num6 = ((num5 > 0) ? (num3 - _w) : (-1));
							break;
						case 1:
							num6 = ((num4 < _w - 1) ? (num3 + 1) : (-1));
							break;
						case 0:
							num6 = ((num4 > 0) ? (num3 - 1) : (-1));
							break;
						}
						int num7 = num6;
						if (num7 >= 0 && !_seen[num7] && open[num7] && (blocked == null || !blocked(_x0 + (float)(num7 % _w) * 0.05f, _y0 + (float)(num7 / _w) * 0.05f)))
						{
							_seen[num7] = true;
							_stack[num2++] = num7;
						}
					}
				}
				return num;
			}
		}

		private sealed class Style
		{
			public uint Wall;

			public uint Timber;

			public uint Floor;

			public uint Ceiling;

			public uint Trim;

			public uint Stone;

			public uint Leaf;

			public uint Bark;

			public uint Door;

			public uint Metal;

			public uint Dark;

			public byte WallKind = 13;

			public byte CeilingKind = 3;

			public float WallH = 1.6f;

			public byte FloorSurface = 8;

			public bool MeshFloor = true;

			public float WinW = 0.42f;

			public float WinZ0 = 0.62f;

			public float WinZ1 = 1.12f;
		}

		private struct Hole
		{
			public float A0;

			public float A1;

			public float Z0;

			public float Z1;

			public bool Door;

			public bool Flap;
		}

		private sealed class WallSide
		{
			public Vec3 Start;

			public Vec3 Along;

			public Vec3 Inward;

			public float Length;

			public readonly List<Hole> Holes = new List<Hole>();

			public Vec3 At(float a, float z, float depth = 0f)
			{
				return Start + Along * a + new Vec3(0f, 0f, z) - Inward * depth;
			}
		}

		private sealed class Shell
		{
			public readonly MeshBuilder B = new MeshBuilder("room");

			public int Wall;

			public int Timber;

			public int Floor;

			public int Ceiling;

			public int Glass;

			public int Trim;

			public int Stone;

			public int Leaf;

			public int Bark;

			public int Door;

			public int Metal;

			public int Dark;

			public Style S;

			public DoorLook Look;

			public float X0;

			public float Y0;

			public float X1;

			public float Y1;

			public int DoorLeafPart = -1;

			public float DoorSwingSign = 1f;

			public int DoorBarPart = -1;

			public Vec3 DoorBarAlong;

			public Vec3 DoorBarInward;

			public Vec3 DoorBarMove;

			public float DoorBarLean;

			public float DoorBarYaw;

			public float DoorBarTip;

			public float[] DoorBarKeep;

			public List<WallSide> Walls;

			public bool DoorPortal;

			public readonly WallSide North = new WallSide();

			public readonly WallSide South = new WallSide();

			public readonly WallSide West = new WallSide();

			public readonly WallSide East = new WallSide();

			public WallSide[] All => new WallSide[4] { North, South, West, East };
		}

		public const float WallHeight = 1.6f;

		private const float BlockWallHeight = 2.25f;

		private const int Px = 8;

		private const float PointedRadius = 1.25f;

		private const float FitCell = 0.25f;

		private const float SlabThick = 0.12f;

		private const float StairWidth = 0.6f;

		[ThreadStatic]
		private static float _lastHeading;

		private const float Scale = 0.5714286f;

		private const float Pi = MathF.PI;

		private const float WallThick = 0.14f;

		public static Interior Build(int roomW, int roomD, uint wallColor, uint roofColor, int seed, string race = "human", int tier = 1, Func<string, MeshModel> models = null, string style = null)
		{
			if (style != null && HasVariantRoom(style, models) && models("int_" + Known(race) + "_bed") != null)
			{
				return BuildVariant(roomW, roomD, wallColor, roofColor, seed, race, Math.Max(0, Math.Min(5, tier)), models, style);
			}
			if (models != null && models("int_barrel") != null && models("int_" + Known(race) + "_bed") != null)
			{
				return BuildRoom(roomW, roomD, wallColor, roofColor, seed, Known(race), Math.Max(0, Math.Min(5, tier)), models);
			}
			return BuildBlocks(roomW, roomD, wallColor, roofColor, seed);
		}

		private static string Known(string race)
		{
			switch (race)
			{
			default:
				return "human";
			case "elf":
			case "orc":
			case "dwarf":
				return race;
			}
		}

		private static Interior BuildBlocks(int roomW, int roomD, uint wallColor, uint roofColor, int seed)
		{
			roomW = Math.Max(5, roomW);
			roomD = Math.Max(4, roomD);
			Interior interior = new Interior
			{
				Top = 2.25f
			};
			Random random = new Random(seed);
			uint num = TerrainCache.Scale(TerrainCache.Lerp(roofColor, TerrainCache.Rgb(90, 60, 35), 0.6f), 0.8f);
			uint c = TerrainCache.Rgb(150, 104, 62);
			int num2 = roomW + 2;
			int num3 = roomD + 2;
			TerrainCache terrainCache = new TerrainCache();
			terrainCache.Allocate(num2, num3);
			terrainCache.CastShadows = false;
			terrainCache.DitherEdges = false;
			terrainCache.OutsideIsWater = false;
			terrainCache.OutsideHeight = 2.25f;
			terrainCache.OutsideColor = TerrainCache.Scale(wallColor, 0.7f);
			uint num4 = TerrainCache.Lerp(roofColor, TerrainCache.Rgb(150, 40, 40), 0.5f);
			int num5 = 1 + roomW / 4;
			int num6 = 1 + roomW - roomW / 4;
			int num7 = 1 + roomD / 4;
			int num8 = 1 + roomD - roomD / 4;
			for (int i = 0; i < num3; i++)
			{
				for (int j = 0; j < num2; j++)
				{
					int num9 = i * num2 + j;
					bool flag = j >= num5 && j < num6 && i >= num7 && i < num8;
					bool flag2 = flag && (j == num5 || j == num6 - 1 || i == num7 || i == num8 - 1);
					terrainCache.BaseColors[num9] = (flag ? (flag2 ? TerrainCache.Scale(num4, 0.7f) : num4) : TerrainCache.Scale(c, 0.92f + (float)(TerrainModel.Hash(j, i * 7) & 0xF) / 100f));
					terrainCache.Materials[num9] = (byte)((!flag) ? 8 : 0);
				}
			}
			terrainCache.Relight(0, 0, num2, num3);
			interior.Floor = terrainCache;
			interior.CeilingTop = TerrainCache.Scale(num, 0.55f);
			interior.CeilingBottom = TerrainCache.Scale(c, 0.62f);
			float num10 = 1f;
			float num11 = 1f;
			float num12 = 1f + (float)roomW;
			float num13 = 1f + (float)roomD;
			AddWall(interior, Wall(roomW, wallColor, num, windows: true, door: false, seed), (num10 + num12) * 0.5f, num13, roomW, 1f, 0f);
			AddWall(interior, Wall(roomW, wallColor, num, windows: false, door: true, seed + 1), (num10 + num12) * 0.5f, num11, roomW, -1f, 0f);
			AddWall(interior, Wall(roomD, wallColor, num, windows: true, door: false, seed + 2), num10, (num11 + num13) * 0.5f, roomD, 0f, 1f);
			AddWall(interior, Wall(roomD, wallColor, num, roomD > 5, door: false, seed + 3), num12, (num11 + num13) * 0.5f, roomD, 0f, -1f);
			AddWindows(interior, roomW, (num10 + num12) * 0.5f, num13, 1f, 0f);
			AddWindows(interior, roomD, num10, (num11 + num13) * 0.5f, 0f, 1f);
			if (roomD > 5)
			{
				AddWindows(interior, roomD, num12, (num11 + num13) * 0.5f, 0f, -1f);
			}
			interior.MinX = num10 + 0.3f;
			interior.MaxX = num12 - 0.3f;
			interior.MinY = num11 + 0.3f;
			interior.MaxY = num13 - 0.3f;
			float num14 = num10 + 1.15f;
			float num15 = num13 - 0.55f;
			AddProp(interior, VoxelShape.Furniture(Bed(roofColor), 4, 2), num14, num15, 2f, 0.55f, 1f, 0f, 1f, 0.5f);
			float num16 = (num10 + num12) * 0.5f + 0.4f;
			float num17 = (num11 + num13) * 0.5f + 0.2f;
			AddProp(interior, VoxelShape.Furniture(Table(), 5, 4), num16, num17, 1.3f, 0.72f, 1f, 0f, 0.65f, 0.55f);
			AddProp(interior, VoxelShape.Furniture(Stool(), 2, 2), num16 - 1f, num17, 0.45f, 0.4f, 0f, 1f, 0.2f, 0.2f);
			AddProp(interior, VoxelShape.Furniture(Stool(), 2, 2), num16 + 1f, num17, 0.45f, 0.4f, 0f, 1f, 0.2f, 0.2f);
			AddProp(interior, VoxelShape.Slab(Chest(), 3), num10 + 0.5f, num11 + 1.2f, 0.8f, 0.55f, 0f, 1f, 0.3f, 0.4f);
			AddProp(interior, VoxelShape.Inflate(Barrel(), 0.9f, 3), num12 - 0.5f, num11 + 0.55f, 0.55f, 0.8f, 1f, 0f, 0.28f, 0.28f);
			if (random.Next(2) == 0)
			{
				AddProp(interior, VoxelShape.Inflate(Barrel(), 0.9f, 3), num12 - 1.15f, num11 + 0.5f, 0.5f, 0.72f, 1f, 0f, 0.25f, 0.25f);
			}
			AddProp(interior, VoxelShape.Slab(Hearth(), 3), num12 - 0.35f, (num11 + num13) * 0.5f + 0.9f, 1.4f, 1.7f, 0f, -1f, 0.35f, 0.7f);
			interior.HearthX = num12 - 0.35f;
			interior.HearthY = (num11 + num13) * 0.5f + 0.9f;
			interior.Spots.Add(new float[3]
			{
				num16 - 1f,
				num17 - 0.6f,
				90f
			});
			interior.Spots.Add(new float[3]
			{
				num16 + 1f,
				num17 + 0.6f,
				270f
			});
			interior.Spots.Add(new float[3]
			{
				num12 - 1.3f,
				(num11 + num13) * 0.5f + 0.9f,
				0f
			});
			interior.Spots.Add(new float[3]
			{
				num14 + 1.6f,
				num15 - 0.9f,
				180f
			});
			interior.Spots.Add(new float[3]
			{
				(num10 + num12) * 0.5f - 1f,
				num11 + 1.4f,
				45f
			});
			interior.DoorX = (num10 + num12) * 0.5f;
			interior.DoorY = num11 + 0.45f;
			interior.SpawnX = interior.DoorX;
			interior.SpawnY = num11 + 0.9f;
			interior.SpawnYaw = MathF.PI / 2f;
			return interior;
		}

		private static void AddWall(Interior room, SpriteImage img, float x, float y, float length, float rightX, float rightY)
		{
			VoxelObject item = VoxelObject.Standing(VoxelShape.Slab(img, 1), x, y, 0f, length, 2.25f, 0.5f, rightX, rightY);
			item.NoShadow = true;
			room.Props.Add(item);
		}

		private static void AddWindows(Interior room, int tiles, float cx, float cy, float rightX, float rightY)
		{
			int num = tiles * 8;
			Vec3 vec = new Vec3(rightX, rightY, 0f);
			Vec3 inward = Vec3.Cross(vec, new Vec3(0f, 0f, 1f));
			for (int i = 9; i + 6 < num - 2; i += 24)
			{
				float num2 = ((float)i + 3f) / 8f - (float)tiles * 0.5f;
				room.Windows.Add(new LightWindow
				{
					Center = new Vec3(cx + rightX * num2, cy + rightY * num2, 1.25f),
					Right = vec,
					HalfWidth = 0.375f,
					HalfHeight = 0.375f,
					Inward = inward
				});
			}
		}

		private static void AddProp(Interior room, VoxelShape shape, float x, float y, float width, float height, float rightX, float rightY, float halfX, float halfY)
		{
			VoxelObject item = VoxelObject.Standing(shape, x, y, 0f, width, height, 0.5f, rightX, rightY);
			item.NoShadow = true;
			room.Props.Add(item);
			bool num = Math.Abs(rightX) > 0.5f;
			float num2 = (num ? (width * 0.5f) : halfX);
			float num3 = (num ? halfY : (width * 0.5f));
			room.Solids.Add(new float[4]
			{
				x - num2,
				y - num3,
				x + num2,
				y + num3
			});
		}

		private static SpriteImage Wall(int tiles, uint wall, uint trim, bool windows, bool door, int seed)
		{
			int num = tiles * 8;
			int num2 = 18;
			PixelCanvas pixelCanvas = new PixelCanvas(num, num2);
			pixelCanvas.Noisy(0, 0, num, num2, wall, 0.1f, seed);
			for (int i = 4; i < num2 - 2; i += 4)
			{
				pixelCanvas.Rect(0, i, num, 1, TerrainCache.Scale(wall, 0.88f));
			}
			pixelCanvas.Rect(0, 0, num, 2, trim);
			pixelCanvas.Rect(0, num2 - 2, num, 2, TerrainCache.Scale(trim, 0.85f));
			for (int j = 0; j < num; j += 24)
			{
				pixelCanvas.Rect(j, 0, 2, num2, trim);
			}
			pixelCanvas.Rect(num - 2, 0, 2, num2, trim);
			if (windows)
			{
				for (int k = 9; k + 6 < num - 2; k += 24)
				{
					pixelCanvas.Rect(k - 1, 4, 8, 8, trim);
					pixelCanvas.Rect(k, 5, 6, 6, TerrainCache.Rgb(160, 205, 240));
					pixelCanvas.Rect(k + 3, 5, 1, 6, trim);
					pixelCanvas.Rect(k, 8, 6, 1, trim);
					pixelCanvas.Set(k + 1, 6, TerrainCache.Rgb(235, 245, 255));
					pixelCanvas.Set(k + 2, 5, TerrainCache.Rgb(235, 245, 255));
					pixelCanvas.Rect(k - 1, 12, 8, 1, TerrainCache.Scale(trim, 1.2f));
				}
			}
			if (door)
			{
				int num3 = num / 2 - 4;
				pixelCanvas.Rect(num3 - 1, 3, 10, num2 - 3, trim);
				pixelCanvas.Noisy(num3, 4, 8, num2 - 4, TerrainCache.Rgb(105, 68, 38), 0.12f, seed + 9);
				for (int l = num3 + 2; l < num3 + 8; l += 3)
				{
					pixelCanvas.Rect(l, 4, 1, num2 - 6, TerrainCache.Rgb(80, 50, 28));
				}
				pixelCanvas.Set(num3 + 8 - 2, num2 / 2 + 1, TerrainCache.Rgb(230, 190, 70));
			}
			return pixelCanvas.ToImage(tiles, 2.25f);
		}

		private static SpriteImage Bed(uint blanketTint)
		{
			PixelCanvas pixelCanvas = new PixelCanvas(16, 8);
			uint c = TerrainCache.Rgb(110, 72, 40);
			uint c2 = TerrainCache.Lerp(blanketTint, TerrainCache.Rgb(170, 40, 45), 0.6f);
			pixelCanvas.Rect(0, 0, 2, 8, c);
			pixelCanvas.Rect(14, 3, 2, 5, c);
			pixelCanvas.Rect(2, 3, 12, 3, TerrainCache.Rgb(235, 230, 220));
			pixelCanvas.Noisy(5, 3, 9, 3, c2, 0.1f, 3);
			pixelCanvas.Rect(5, 3, 9, 1, TerrainCache.Scale(c2, 1.15f));
			pixelCanvas.Rect(2, 3, 3, 2, TerrainCache.Rgb(250, 250, 245));
			pixelCanvas.Rect(2, 6, 12, 1, c);
			pixelCanvas.Rect(1, 7, 1, 1, c);
			pixelCanvas.Rect(14, 7, 1, 1, c);
			return pixelCanvas.ToImage(2f, 0.55f);
		}

		private static SpriteImage Table()
		{
			PixelCanvas pixelCanvas = new PixelCanvas(12, 7);
			uint c = TerrainCache.Rgb(165, 115, 70);
			uint c2 = TerrainCache.Rgb(115, 76, 44);
			pixelCanvas.Noisy(0, 0, 12, 2, c, 0.1f, 5);
			pixelCanvas.Rect(1, 2, 10, 1, c2);
			pixelCanvas.Rect(1, 3, 2, 4, c2);
			pixelCanvas.Rect(9, 3, 2, 4, c2);
			return pixelCanvas.ToImage(1.3f, 0.72f);
		}

		private static SpriteImage Stool()
		{
			PixelCanvas pixelCanvas = new PixelCanvas(5, 4);
			pixelCanvas.Rect(0, 0, 5, 2, TerrainCache.Rgb(150, 100, 60));
			pixelCanvas.Rect(0, 2, 1, 2, TerrainCache.Rgb(110, 70, 40));
			pixelCanvas.Rect(4, 2, 1, 2, TerrainCache.Rgb(110, 70, 40));
			return pixelCanvas.ToImage(0.45f, 0.4f);
		}

		private static SpriteImage Chest()
		{
			PixelCanvas pixelCanvas = new PixelCanvas(8, 6);
			pixelCanvas.Noisy(0, 0, 8, 6, TerrainCache.Rgb(135, 88, 48), 0.12f, 7);
			pixelCanvas.Rect(0, 0, 8, 2, TerrainCache.Rgb(110, 70, 38));
			pixelCanvas.Rect(0, 2, 8, 1, TerrainCache.Rgb(90, 90, 95));
			pixelCanvas.Rect(3, 1, 2, 5, TerrainCache.Rgb(95, 95, 100));
			pixelCanvas.Set(4, 3, TerrainCache.Rgb(230, 190, 70));
			return pixelCanvas.ToImage(0.8f, 0.55f);
		}

		private static SpriteImage Barrel()
		{
			PixelCanvas pixelCanvas = new PixelCanvas(6, 8);
			pixelCanvas.Noisy(0, 0, 6, 8, TerrainCache.Rgb(140, 92, 50), 0.14f, 11);
			for (int i = 1; i < 6; i += 2)
			{
				pixelCanvas.Rect(i, 0, 1, 8, TerrainCache.Rgb(118, 76, 40));
			}
			pixelCanvas.Rect(0, 1, 6, 1, TerrainCache.Rgb(100, 100, 105));
			pixelCanvas.Rect(0, 6, 6, 1, TerrainCache.Rgb(100, 100, 105));
			return pixelCanvas.ToImage(0.55f, 0.8f);
		}

		private static SpriteImage Hearth()
		{
			PixelCanvas pixelCanvas = new PixelCanvas(12, 14);
			pixelCanvas.Noisy(3, 0, 6, 5, TerrainCache.Rgb(125, 120, 115), 0.18f, 13);
			pixelCanvas.Noisy(0, 5, 12, 9, TerrainCache.Rgb(135, 130, 125), 0.2f, 17);
			for (int i = 6; i < 14; i += 2)
			{
				pixelCanvas.Rect(0, i, 12, 1, TerrainCache.Rgb(105, 100, 98));
			}
			pixelCanvas.Rect(0, 5, 12, 1, TerrainCache.Rgb(110, 75, 45));
			pixelCanvas.Rect(3, 8, 6, 6, TerrainCache.Rgb(35, 25, 20));
			pixelCanvas.Rect(4, 12, 4, 2, TerrainCache.Rgb(250, 150, 40));
			pixelCanvas.Rect(5, 11, 2, 1, TerrainCache.Rgb(255, 210, 80));
			pixelCanvas.Set(4, 11, TerrainCache.Rgb(240, 100, 30));
			pixelCanvas.Set(7, 11, TerrainCache.Rgb(240, 100, 30));
			pixelCanvas.Set(5, 10, TerrainCache.Rgb(255, 230, 120));
			pixelCanvas.Rect(3, 13, 6, 1, TerrainCache.Rgb(90, 55, 30));
			return pixelCanvas.ToImage(1.4f, 1.7f);
		}

		private static string[] BenchPieces(int level)
		{
			switch (Math.Max(1, Math.Min(5, level)))
			{
			case 1:
				return new string[1] { "int_workbench" };
			case 2:
				return new string[2] { "int_workbench", "int_crate" };
			case 3:
				return new string[3] { "int_workbench", "int_dwarf_anvil", "int_crate" };
			case 4:
				return new string[3] { "int_workbench", "int_dwarf_anvil", "int_dwarf_forge" };
			default:
				return new string[4] { "int_workbench", "int_dwarf_anvil", "int_v_bigforge", "int_v_weapons" };
			}
		}

		public static bool AddWorkbench(Interior room, Func<string, MeshModel> models, int level, uint roofColor, out float x, out float y)
		{
			x = (y = 0f);
			if (room == null || models == null || level <= 0)
			{
				return false;
			}
			float num = room.MinX - 0.25f;
			float num2 = room.MinY - 0.25f;
			float num3 = room.MaxX + 0.25f;
			float num4 = room.MaxY + 0.25f;
			Layout layout = new Layout(room, models, FurnitureColors(room.Race, roofColor), num, num2, num3, num4);
			foreach (float[] solid in room.Solids)
			{
				layout.Keep(solid[0], solid[1], solid[2], solid[3]);
			}
			layout.Keep(room.DoorX - 0.65f, room.DoorY - 0.6f, room.DoorX + 0.65f, room.DoorY + 1.1f);
			layout.Keep(room.SpawnX - 0.35f, room.SpawnY - 0.35f, room.SpawnX + 0.35f, room.SpawnY + 0.35f);
			foreach (float[] spot in room.Spots)
			{
				layout.Keep(spot[0] - 0.3f, spot[1] - 0.3f, spot[0] + 0.3f, spot[1] + 0.3f);
			}
			string[] array = BenchPieces(level);
			if (models(array[0]) == null)
			{
				array[0] = "int_v_trestle";
			}
			float num5 = layout.Width(array[0]);
			if (num5 <= 0f)
			{
				return false;
			}
			char[] array2 = new char[4] { 'N', 'E', 'W', 'S' };
			foreach (char c in array2)
			{
				int num7;
				float num8;
				if (c != 'N')
				{
					num7 = ((c == 'S') ? 1 : 0);
					if (num7 == 0)
					{
						num8 = num2;
						goto IL_01ec;
					}
				}
				else
				{
					num7 = 1;
				}
				num8 = num;
				goto IL_01ec;
				IL_01ec:
				float num9 = num8 + 0.1f;
				float num10 = ((num7 != 0) ? num3 : num4);
				float cursor = num9;
				bool flag = false;
				while (!flag && cursor + num5 < num10)
				{
					flag = layout.Next(array[0], c, ref cursor, 0.12f);
				}
				if (flag)
				{
					x = layout.LastX;
					y = layout.LastY;
					float num11 = cursor;
					for (int i = 1; i < array.Length; i++)
					{
						float num12 = layout.Width(array[i]);
						if (!(num12 <= 0f))
						{
							float num13 = cursor;
							bool flag2 = false;
							while (!flag2 && cursor + num12 < num10 && cursor - num11 < 1.2f)
							{
								flag2 = layout.Next(array[i], c, ref cursor, 0.1f);
							}
							if (!flag2)
							{
								cursor = num13;
							}
						}
					}
					return true;
				}
			}
			return false;
		}

		private static DoorLook LookFor(string race, string style)
		{
			switch (style)
			{
			case "human_stone":
				return new DoorLook
				{
					Top = "round",
					Frame = "stone"
				};
			case "human_timber":
				return new DoorLook();
			case "orc_skull":
				return new DoorLook
				{
					Curtain = true
				};
			case "dwarf_stump":
				return new DoorLook
				{
					Top = "round",
					Frame = "timber",
					Hold = "bands"
				};
			default:
				switch (race)
				{
				case "elf":
					return new DoorLook
					{
						Top = "pointed",
						Frame = "gothic",
						Gold = true,
						Bar = false
					};
				case "orc":
					return new DoorLook
					{
						Frame = "log",
						Hold = "poles"
					};
				case "dwarf":
					return new DoorLook
					{
						Top = "round",
						Frame = "stone",
						Hold = "bands"
					};
				default:
					return new DoorLook();
				}
			}
		}

		private static DoorKit Kit(Shell sh, DoorLook look)
		{
			Style s = sh.S;
			MeshBuilder b = sh.B;
			DoorKit doorKit = new DoorKit
			{
				Plank = sh.Door,
				Plank2 = b.Slot("door_b", 2, TerrainCache.Scale(s.Door, 1.17f), 0),
				Gap = sh.Dark,
				Ledge = b.Slot("ledge", 3, TerrainCache.Scale(s.Door, 1.06f), 0),
				Post = b.Slot("post", 2, s.Timber, 0),
				Beam = sh.Timber,
				Ashlar = b.Slot("ashlar", 13, TerrainCache.Lerp(s.Stone, TerrainCache.Rgb(214, 208, 196), 0.35f), 0),
				Iron = (look.Gold ? b.Slot("gilt", 10, TerrainCache.Rgb(214, 178, 90), 0) : b.Slot("ironwork", 10, TerrainCache.Rgb(48, 45, 46), 0))
			};
			if (look.Hold == "poles" || look.Frame == "log" || look.Curtain)
			{
				doorKit.Rope = b.Slot("rope", 0, TerrainCache.Rgb(128, 104, 70), 0);
				doorKit.Bone = b.Slot("bone", 0, TerrainCache.Rgb(226, 218, 196), 0);
				doorKit.Hide = b.Slot("hide", 7, TerrainCache.Rgb(188, 156, 112), 0);
			}
			return doorKit;
		}

		private static float Spring(DoorLook look, Hole h)
		{
			float num = (h.A1 - h.A0) * 0.5f;
			string top = look.Top;
			if (!(top == "round"))
			{
				if (top == "pointed")
				{
					float num2 = num * 1.25f;
					return h.Z1 - (float)Math.Sqrt(num2 * num2 - (num2 - num) * (num2 - num));
				}
				return h.Z1;
			}
			return h.Z1 - num;
		}

		private static (float a, float z) HeadPoint(DoorLook look, Hole h, float s, float e)
		{
			float num = (h.A1 - h.A0) * 0.5f;
			float num2 = (h.A0 + h.A1) * 0.5f;
			float num3 = Spring(look, h);
			if (look.Top == "round")
			{
				float num4 = MathF.PI * (1f - s);
				return (a: num2 + (num + e) * (float)Math.Cos(num4), z: num3 + (num + e) * (float)Math.Sin(num4));
			}
			if (look.Top == "pointed")
			{
				float num5 = num * 1.25f;
				float num6 = num5 + e;
				bool num7 = s > 0.5f;
				float num8 = (num7 ? ((1f - s) * 2f) : (s * 2f));
				float num9 = (float)Math.Acos((num5 - num) / num6) * num8;
				float num10 = h.A0 + num5 - num6 * (float)Math.Cos(num9);
				return new ValueTuple<float, float>(item2: num3 + num6 * (float)Math.Sin(num9), item1: num7 ? (2f * num2 - num10) : num10);
			}
			return (a: h.A0 - e + (h.A1 - h.A0 + 2f * e) * s, z: h.Z1 + e);
		}

		private static List<(float a, float z)> HeadLine(DoorLook look, Hole h, int n)
		{
			List<(float, float)> list = new List<(float, float)>();
			if (look.Top == "square")
			{
				list.Add((h.A0, h.Z1));
				list.Add((h.A1, h.Z1));
				return list;
			}
			for (int i = 0; i <= n; i++)
			{
				list.Add(HeadPoint(look, h, (float)i / (float)n, 0f));
			}
			return list;
		}

		private static float HeadAt(DoorLook look, Hole h, float a)
		{
			float num = (h.A1 - h.A0) * 0.5f;
			float num2 = (h.A0 + h.A1) * 0.5f;
			float num3 = Spring(look, h);
			if (look.Top == "round")
			{
				float num4 = Math.Min(num, Math.Abs(a - num2));
				return num3 + (float)Math.Sqrt(Math.Max(0f, num * num - num4 * num4));
			}
			if (look.Top == "pointed")
			{
				float num5 = num * 1.25f;
				float val = ((a < num2) ? (a - h.A0) : (h.A1 - a));
				float num6 = num5 - Math.Max(0f, val);
				return num3 + (float)Math.Sqrt(Math.Max(0f, num5 * num5 - num6 * num6));
			}
			return h.Z1;
		}

		private static void Block(Shell sh, WallSide w, int slot, IList<(float a, float z)> outline, float d0, float d1, bool closed = false)
		{
			int count = outline.Count;
			if (count < 3)
			{
				return;
			}
			float num = 0f;
			float num2 = 0f;
			foreach (var item in outline)
			{
				num += item.a;
				num2 += item.z;
			}
			num /= (float)count;
			num2 /= (float)count;
			Vec3[] array = new Vec3[count];
			Vec3 a = new Vec3(0f, 0f, 0f);
			for (int i = 0; i < count; i++)
			{
				array[i] = w.At(outline[i].a, outline[i].z, d0);
			}
			for (int j = 0; j < count; j++)
			{
				a += Vec3.Cross(array[j], array[(j + 1) % count]);
			}
			if (Vec3.Dot(a, w.Inward) < 0f)
			{
				Array.Reverse(array);
			}
			sh.B.Face(slot, array);
			if (closed)
			{
				Vec3[] array2 = new Vec3[count];
				for (int k = 0; k < count; k++)
				{
					array2[k] = array[count - 1 - k] - w.Inward * (d1 - d0);
				}
				sh.B.Face(slot, array2);
			}
			for (int l = 0; l < count; l++)
			{
				(float, float) tuple = outline[l];
				(float, float) tuple2 = outline[(l + 1) % count];
				float num3 = (tuple.Item1 + tuple2.Item1) * 0.5f - num;
				float num4 = (tuple.Item2 + tuple2.Item2) * 0.5f - num2;
				if (!(Math.Abs(tuple.Item1 - tuple2.Item1) + Math.Abs(tuple.Item2 - tuple2.Item2) < 0.0001f))
				{
					Vec3 toward = w.At((tuple.Item1 + tuple2.Item1) * 0.5f + num3, (tuple.Item2 + tuple2.Item2) * 0.5f + num4, (d0 + d1) * 0.5f);
					FaceToward(sh.B, slot, toward, w.At(tuple.Item1, tuple.Item2, d0), w.At(tuple2.Item1, tuple2.Item2, d0), w.At(tuple2.Item1, tuple2.Item2, d1), w.At(tuple.Item1, tuple.Item2, d1));
				}
			}
		}

		private static void Block(Shell sh, WallSide w, int slot, float a0, float a1, float z0, float z1, float d0, float d1, bool closed = false)
		{
			Block(sh, w, slot, new(float, float)[4]
			{
				(a0, z0),
				(a1, z0),
				(a1, z1),
				(a0, z1)
			}, d0, d1, closed);
		}

		private static void FanToward(MeshBuilder b, int slot, Vec3 toward, Vec3 corner, List<Vec3> rest)
		{
			List<Vec3> list = new List<Vec3> { corner };
			list.AddRange(rest);
			Vec3 a = new Vec3(0f, 0f, 0f);
			Vec3 vec = new Vec3(0f, 0f, 0f);
			for (int i = 0; i < list.Count; i++)
			{
				Vec3 vec2 = list[i];
				Vec3 b2 = list[(i + 1) % list.Count];
				a += Vec3.Cross(vec2, b2);
				vec += vec2;
			}
			vec *= 1f / (float)list.Count;
			if (Vec3.Dot(a, toward - vec) < 0f)
			{
				rest.Reverse();
				list = new List<Vec3> { corner };
				list.AddRange(rest);
			}
			b.Face(slot, list.ToArray());
		}

		private static void Rod(Shell sh, WallSide w, int slot, float a0, float z0, float a1, float z1, float d, float r, int sides = 6)
		{
			sh.B.Tube(slot, w.At(a0, z0, d), w.At(a1, z1, d), r, r, sides);
		}

		private static void InsideDoor(Shell sh, WallSide w, Hole h, DoorLook look, string race)
		{
			DoorKit doorKit = Kit(sh, look);
			if (look.Curtain)
			{
				HideCurtain(sh, w, h, doorKit);
				return;
			}
			MeshBuilder b = sh.B;
			float num = 0.14f * 0.55f;
			float num2 = num + 0.03f;
			float a = (h.A0 + h.A1) * 0.5f;
			float num3 = Spring(look, h);
			bool flag = look.Top != "square";
			List<(float, float)> list = HeadLine(look, h, (look.Top == "pointed") ? 12 : 10);
			Vec3 toward = w.At(a, num3 * 0.6f, -1.5f);
			int slot = ((race == "dwarf") ? sh.Stone : ((race == "orc") ? sh.Timber : sh.Wall));
			Vec3 toward2 = w.At(a, Math.Min(num3, h.Z1) * 0.5f, num2 * 0.5f);
			FaceToward(b, slot, toward2, w.At(h.A0, 0f), w.At(h.A0, num3), w.At(h.A0, num3, num2), w.At(h.A0, 0f, num2));
			FaceToward(b, slot, toward2, w.At(h.A1, 0f), w.At(h.A1, num3), w.At(h.A1, num3, num2), w.At(h.A1, 0f, num2));
			Vec3 toward3 = w.At(a, flag ? num3 : 0f, num2 * 0.5f);
			for (int i = 0; i + 1 < list.Count; i++)
			{
				(float, float) tuple = list[i];
				(float, float) tuple2 = list[i + 1];
				FaceToward(b, slot, toward3, w.At(tuple.Item1, tuple.Item2), w.At(tuple2.Item1, tuple2.Item2), w.At(tuple2.Item1, tuple2.Item2, num2), w.At(tuple.Item1, tuple.Item2, num2));
			}
			if (flag)
			{
				int num4 = list.Count / 2;
				List<Vec3> list2 = new List<Vec3>();
				for (int j = 0; j <= num4; j++)
				{
					list2.Add(w.At(list[j].Item1, list[j].Item2));
				}
				FanToward(b, sh.Wall, toward, w.At(h.A0, h.Z1), list2);
				List<Vec3> list3 = new List<Vec3>();
				for (int k = num4; k < list.Count; k++)
				{
					list3.Add(w.At(list[k].Item1, list[k].Item2));
				}
				FanToward(b, sh.Wall, toward, w.At(h.A1, h.Z1), list3);
			}
			List<(float, float)> list4 = new List<(float, float)>
			{
				(h.A0, 0f),
				(h.A1, 0f)
			};
			for (int num5 = list.Count - 1; num5 >= 0; num5--)
			{
				list4.Add(list[num5]);
			}
			Vec3[] array = new Vec3[list4.Count];
			for (int l = 0; l < array.Length; l++)
			{
				array[l] = w.At(list4[l].Item1, list4[l].Item2, num2);
			}
			int slot2 = b.Slot("doorway", 0, sh.S.Dark, 4);
			FaceToward(b, slot2, toward, array);
			sh.DoorPortal = true;
			sh.DoorLeafPart = b.Part("door_leaf", 0, w.At(h.A0, 0f, num));
			Vec3 a2 = Vec3.Cross(new Vec3(0f, 0f, 1f), w.Along);
			sh.DoorSwingSign = ((Vec3.Dot(a2, w.Inward) >= 0f) ? 1f : (-1f));
			float num6 = h.A1 - h.A0;
			int num7 = Math.Max(4, (int)Math.Round(num6 / ((look.Hold == "poles") ? 0.17f : 0.135f)));
			float[] array2 = new float[num7];
			float num8 = 0f;
			for (int m = 0; m < num7; m++)
			{
				int num9 = Math.Min(m, num7 - 1 - m);
				array2[m] = 1f + ((look.Hold == "poles") ? 0.3f : 0.16f) * (float)Math.Sin((float)num9 * 2.3f + 0.7f);
				num8 += array2[m];
			}
			float num10 = h.A0;
			List<float> list5 = new List<float> { h.A0 };
			for (int n = 0; n < num7; n++)
			{
				float num11 = num10 + ((n == 0) ? 0f : 0.0045f);
				float num12 = num10 + num6 * array2[n] / num8 - ((n == num7 - 1) ? 0f : 0.0045f);
				num10 += num6 * array2[n] / num8;
				list5.Add(num10);
				List<(float, float)> list6 = new List<(float, float)>
				{
					(num11, 0.03f),
					(num12, 0.03f)
				};
				float num13 = ((look.Hold == "poles" && !flag) ? (0.035f * (float)Math.Abs(Math.Sin((float)n * 3.7f))) : 0f);
				list6.Add((num12, HeadAt(look, h, num12) - num13));
				for (int num14 = list.Count - 1; num14 >= 0; num14--)
				{
					if (list[num14].Item1 < num12 - 0.005f && list[num14].Item1 > num11 + 0.005f)
					{
						list6.Add((list[num14].Item1, list[num14].Item2 - num13));
					}
				}
				list6.Add((num11, HeadAt(look, h, num11) - num13));
				Block(sh, w, ((n * 5 + 2) % 7 < 2) ? doorKit.Plank2 : doorKit.Plank, list6, num, num2, closed: true);
			}
			float num15 = (flag ? num3 : h.Z1) - ((look.Hold == "bands") ? 0.12f : 0.15f);
			float num16 = 0.2f;
			float num17 = (num15 + num16) * 0.5f;
			float[] array3 = new float[3] { num16, num17, num15 };
			float num18 = num;
			string hold = look.Hold;
			float[] array4;
			if (!(hold == "bands"))
			{
				if (hold == "poles")
				{
					array4 = new float[2]
					{
						num16 + 0.04f,
						num15
					};
					foreach (float num20 in array4)
					{
						Rod(sh, w, doorKit.Post, h.A0 + 0.01f, num20, h.A1 - 0.01f, num20 + 0.015f, num - 0.03f, 0.03f, 5);
						for (int num21 = 0; num21 <= num7; num21 += Math.Max(1, num7 / 2))
						{
							float num22 = Math.Min(h.A1 - 0.05f, Math.Max(h.A0 + 0.05f, list5[num21]));
							Rod(sh, w, doorKit.Rope, num22 - 0.018f, num20 + 0.008f, num22 + 0.018f, num20 + 0.008f, num - 0.03f, 0.036f, 5);
						}
					}
					num18 = num - 0.06f;
				}
				else
				{
					array4 = array3;
					foreach (float num23 in array4)
					{
						Block(sh, w, doorKit.Ledge, h.A0 + 0.03f, h.A1 - 0.03f, num23 - 0.045f, num23 + 0.045f, num - 0.026f, num);
						for (int num24 = 0; num24 < num7; num24++)
						{
							float num25 = (list5[num24] + list5[num24 + 1]) * 0.5f;
							float[] array5 = new float[2] { -0.022f, 0.022f };
							foreach (float num27 in array5)
							{
								Block(sh, w, doorKit.Iron, num25 - 0.009f, num25 + 0.009f, num23 + num27 - 0.009f, num23 + num27 + 0.009f, num - 0.032f, num - 0.026f);
							}
						}
					}
					for (int num28 = 0; num28 < 2; num28++)
					{
						Vec3 vec = w.At(h.A0 + 0.09f, array3[num28] + 0.05f, num - 0.013f);
						Vec3 vec2 = w.At(h.A1 - 0.09f, array3[num28 + 1] - 0.05f, num - 0.013f);
						Vec3 vec3 = vec2 - vec;
						float num29 = (float)Math.Sqrt(Vec3.Dot(vec3, vec3));
						Vec3 vec4 = vec3 * (1f / num29);
						Vec3 vec5 = Vec3.Cross(w.Inward, vec4).Normalized() * 0.038f;
						Vec3 vec6 = vec4 * (num29 * 0.5f);
						Vec3 vec7 = w.Inward * 0.013f;
						if (Vec3.Dot(Vec3.Cross(vec5, vec6), vec7) < 0f)
						{
							vec5 *= -1f;
						}
						b.Box(doorKit.Ledge, (vec + vec2) * 0.5f, vec5, vec6, vec7);
					}
					num18 = num - 0.026f;
				}
			}
			else
			{
				array4 = array3;
				foreach (float num30 in array4)
				{
					Block(sh, w, doorKit.Iron, h.A0 + 0.005f, h.A1 - 0.005f, num30 - 0.032f, num30 + 0.032f, num - 0.012f, num);
					for (int num31 = 0; num31 < num7; num31++)
					{
						Block(sh, w, doorKit.Iron, (list5[num31] + list5[num31 + 1]) * 0.5f - 0.011f, (list5[num31] + list5[num31 + 1]) * 0.5f + 0.011f, num30 - 0.011f, num30 + 0.011f, num - 0.022f, num - 0.012f);
					}
				}
				num18 = num - 0.012f;
			}
			array4 = new float[2]
			{
				array3[0],
				array3[2]
			};
			foreach (float num32 in array4)
			{
				sh.B.Tube(doorKit.Iron, w.At(h.A0 + 0.012f, num32 - 0.06f, num18 - 0.012f), w.At(h.A0 + 0.012f, num32 + 0.06f, num18 - 0.012f), 0.016f, 0.016f, 6);
			}
			float num33 = Math.Min(0.52f, num15 - 0.08f);
			if (look.Hold == "bands")
			{
				Block(sh, w, doorKit.Iron, h.A1 - 0.2f, h.A1 - 0.05f, num33 - 0.075f, num33 + 0.065f, num18 - 0.03f, num18);
				Block(sh, w, doorKit.Gap, h.A1 - 0.135f, h.A1 - 0.115f, num33 - 0.035f, num33 + 0.01f, num18 - 0.032f, num18 - 0.03f);
			}
			else
			{
				Block(sh, w, doorKit.Iron, h.A1 - 0.3f, h.A1 - 0.25f, num33 - 0.035f, num33 + 0.035f, num18 - 0.008f, num18);
				Block(sh, w, doorKit.Iron, h.A1 - 0.28f, h.A1 + 0f, num33 - 0.012f, num33 + 0.012f, num18 - 0.024f, num18 - 0.008f);
				Block(sh, w, doorKit.Iron, h.A1 - 0.035f, h.A1 + 0.004f, num33 - 0.05f, num33 + 0.03f, num18 - 0.036f, num18);
			}
			float num34 = h.A1 - 0.17f;
			float num35 = num33 - 0.17f;
			float num36 = 0.05f;
			Block(sh, w, doorKit.Iron, num34 - 0.03f, num34 + 0.03f, num35 + num36 - 0.03f, num35 + num36 + 0.03f, num18 - 0.006f, num18);
			for (int num37 = 0; num37 < 10; num37++)
			{
				float num38 = MathF.PI * 2f * (float)num37 / 10f;
				float num39 = MathF.PI * 2f * (float)(num37 + 1) / 10f;
				Rod(sh, w, doorKit.Iron, num34 + num36 * (float)Math.Sin(num38), num35 + num36 * (float)Math.Cos(num38), num34 + num36 * (float)Math.Sin(num39), num35 + num36 * (float)Math.Cos(num39), num18 - 0.018f, 0.009f, 4);
			}
			OuterSide(sh, w, h, look, doorKit, array3, list5, num2);
			b.UsePart(0);
			float num40 = Surround(sh, w, h, look, doorKit, num3);
			int slot3 = ((look.Frame == "stone" || look.Frame == "gothic") ? doorKit.Ashlar : ((look.Frame == "log") ? doorKit.Post : sh.Timber));
			Block(sh, w, slot3, h.A0 - 0.05f, h.A1 + 0.05f, 0f, 0.028f, -0.06f, num2);
			if (look.Bar)
			{
				float num41 = Math.Min(0.7f, num15 - 0.04f);
				float num42 = ((look.Frame == "log") ? 0.2f : 0.15f);
				float num43 = 0f - num40 - 0.1f;
				float num44 = 0f - num40 - 0.025f;
				array4 = new float[2]
				{
					h.A0 - num42 + 0.06f,
					h.A1 + num42 - 0.06f
				};
				foreach (float num45 in array4)
				{
					Block(sh, w, doorKit.Iron, num45 - 0.035f, num45 + 0.035f, num41 - 0.058f, num41 - 0.04f, num43 - 0.012f, 0f);
					Block(sh, w, doorKit.Iron, num45 - 0.035f, num45 + 0.035f, num41 - 0.058f, num41 + 0.025f, num43 - 0.014f, num43);
				}
				DoorBar(sh, w, h, (look.Hold == "poles") ? doorKit.Post : doorKit.Beam, h.A1 - h.A0 + 2f * num42, w.At((h.A0 + h.A1) * 0.5f, num41, (num43 + num44) * 0.5f), (num44 - num43) * 0.5f);
			}
		}

		private static void OuterSide(Shell sh, WallSide w, Hole h, DoorLook look, DoorKit k, float[] rows, List<float> edges, float back)
		{
			float d = back - 0.004f;
			int num = edges.Count - 1;
			string hold = look.Hold;
			if (!(hold == "poles"))
			{
				if (hold == "bands")
				{
					float[] array = rows;
					foreach (float num2 in array)
					{
						Block(sh, w, k.Iron, h.A0 + 0.005f, h.A1 - 0.005f, num2 - 0.03f, num2 + 0.03f, d, back + 0.012f, closed: true);
						for (int j = 0; j < num; j++)
						{
							float num3 = (edges[j] + edges[j + 1]) * 0.5f;
							Block(sh, w, k.Iron, num3 - 0.011f, num3 + 0.011f, num2 - 0.011f, num2 + 0.011f, back + 0.008f, back + 0.022f, closed: true);
						}
					}
				}
				else
				{
					float num4 = h.A0 + (h.A1 - h.A0) * 0.68f;
					float[] array = new float[2]
					{
						rows[0],
						rows[2]
					};
					foreach (float num5 in array)
					{
						List<(float, float)> outline = new List<(float, float)>
						{
							(h.A0 + 0.01f, num5 - 0.024f),
							(num4, num5 - 0.024f),
							(num4 + 0.06f, num5),
							(num4, num5 + 0.024f),
							(h.A0 + 0.01f, num5 + 0.024f)
						};
						Block(sh, w, k.Iron, outline, d, back + 0.01f, closed: true);
						for (int l = 0; l < num; l++)
						{
							float num6 = (edges[l] + edges[l + 1]) * 0.5f;
							if (num6 > num4 - 0.02f)
							{
								break;
							}
							Block(sh, w, k.Iron, num6 - 0.008f, num6 + 0.008f, num5 - 0.008f, num5 + 0.008f, back + 0.006f, back + 0.017f, closed: true);
						}
					}
				}
			}
			else
			{
				float[] array = new float[2]
				{
					rows[0] + 0.06f,
					rows[2] - 0.02f
				};
				foreach (float num7 in array)
				{
					Rod(sh, w, k.Post, h.A0 + 0.02f, num7, h.A1 - 0.02f, num7 - 0.012f, back + 0.026f, 0.026f, 5);
				}
			}
			float num8 = Math.Min(0.52f, rows[2] - 0.08f);
			float num9 = h.A1 - 0.17f;
			float num10 = 0.045f;
			Block(sh, w, k.Iron, num9 - 0.035f, num9 + 0.035f, num8 - 0.035f, num8 + 0.035f, d, back + 0.008f, closed: true);
			for (int m = 0; m < 10; m++)
			{
				float num11 = MathF.PI * 2f * (float)m / 10f;
				float num12 = MathF.PI * 2f * (float)(m + 1) / 10f;
				Rod(sh, w, k.Iron, num9 + num10 * (float)Math.Sin(num11), num8 - num10 + num10 * (float)Math.Cos(num11), num9 + num10 * (float)Math.Sin(num12), num8 - num10 + num10 * (float)Math.Cos(num12), back + 0.02f, 0.008f, 4);
			}
		}

		private static void DoorBar(Shell sh, WallSide w, Hole h, int slot, float len, Vec3 across, float halfDeep)
		{
			Vec3 v = new Vec3(0f, 0f, 1f);
			WallSide wallSide = null;
			float num = 0f;
			float num2 = float.MaxValue;
			IEnumerable<WallSide> walls = sh.Walls;
			foreach (WallSide item in walls ?? sh.All)
			{
				if (item.Length < 0.28f || Vec3.Dot(item.Inward, w.Inward) < 0.6f)
				{
					continue;
				}
				for (float num3 = 0.14f; num3 <= item.Length - 0.1f - 0.04f; num3 += 0.02f)
				{
					if (InHole(item, num3, 0.16f))
					{
						continue;
					}
					Vec3 a = item.At(num3, 0f) - w.Start;
					float num4 = Vec3.Dot(a, w.Along);
					if (Math.Abs(Vec3.Dot(a, w.Inward)) > 0.35f)
					{
						continue;
					}
					float num5;
					if (num4 >= h.A1 + 0.24f + 0.04f)
					{
						num5 = num4 - h.A1;
					}
					else
					{
						if (!(num4 <= h.A0 - 0.24f - 0.04f))
						{
							continue;
						}
						num5 = 10f + h.A0 - num4;
					}
					if (num5 < num2)
					{
						num2 = num5;
						wallSide = item;
						num = num3;
					}
				}
			}
			if (wallSide != null && !(num2 > 11.5f))
			{
				float num6 = 0.2f;
				float num7 = h.Z1 - 0.03f;
				if (len * (float)Math.Cos(num6) > num7)
				{
					num6 = Math.Min(0.6f, (float)Math.Acos(Math.Min(1f, num7 / len)));
				}
				if (Vec3.Dot(Mat3.Rotation(wallSide.Along, num6).Mul(v), wallSide.Inward) > 0f)
				{
					num6 = 0f - num6;
				}
				Mat3 mat = Mat3.Rotation(wallSide.Along, num6);
				Vec3 vec = mat.Mul(v);
				Vec3 vec2 = mat.Mul(wallSide.Inward);
				float num8 = (float)Math.Cos(num6);
				float num9 = Math.Abs((float)Math.Sin(num6));
				float z = len * 0.5f * num8 + halfDeep * num9 + 0.005f;
				float depth = -0.02f - len * 0.5f * num9 - halfDeep * num8;
				Vec3 vec3 = wallSide.At(num, z, depth);
				MeshBuilder b = sh.B;
				sh.DoorBarPart = b.Part("door_bar", 0, vec3);
				Vec3 vec4 = wallSide.Along * 0.04f;
				Vec3 vec5 = vec * (len * 0.5f);
				Vec3 vec6 = vec2 * halfDeep;
				if (Vec3.Dot(Vec3.Cross(vec4, vec5), vec6) < 0f)
				{
					vec4 *= -1f;
				}
				b.Box(slot, vec3, vec4, vec5, vec6);
				b.UsePart(0);
				float num10 = (float)Math.Atan2(Vec3.Cross(wallSide.Along, w.Along).Z, Vec3.Dot(wallSide.Along, w.Along));
				if (Vec3.Dot(Mat3.RotZ(num10).Mul(wallSide.Along), w.Along) < Vec3.Dot(Mat3.RotZ(0f - num10).Mul(wallSide.Along), w.Along))
				{
					num10 = 0f - num10;
				}
				float num11 = MathF.PI / 2f;
				Vec3 b2 = w.Along * ((num2 < 10f) ? (-1f) : 1f);
				if (Vec3.Dot(Mat3.Rotation(w.Inward, num11).Mul(v), b2) < 0f)
				{
					num11 = 0f - num11;
				}
				sh.DoorBarAlong = wallSide.Along;
				sh.DoorBarInward = w.Inward;
				sh.DoorBarLean = num6;
				sh.DoorBarYaw = num10;
				sh.DoorBarTip = num11;
				sh.DoorBarMove = across - vec3;
				float num12 = 0.02f + len * num9 + halfDeep * 2f * num8 + 0.05f;
				Vec3 vec7 = wallSide.At(num - 0.04f - 0.05f, 0f);
				Vec3 vec8 = wallSide.At(num + 0.04f + 0.05f, 0f, 0f - num12);
				sh.DoorBarKeep = new float[4]
				{
					Math.Min(vec7.X, vec8.X),
					Math.Min(vec7.Y, vec8.Y),
					Math.Max(vec7.X, vec8.X),
					Math.Max(vec7.Y, vec8.Y)
				};
			}
		}

		private static void TakeDoor(Interior room, Shell sh)
		{
			room.DoorLeafPart = sh.DoorLeafPart;
			room.DoorSwingSign = sh.DoorSwingSign;
			room.DoorBarPart = sh.DoorBarPart;
			room.DoorBarAlong = sh.DoorBarAlong;
			room.DoorBarInward = sh.DoorBarInward;
			room.DoorBarMove = sh.DoorBarMove;
			room.DoorBarLean = sh.DoorBarLean;
			room.DoorBarYaw = sh.DoorBarYaw;
			room.DoorBarTip = sh.DoorBarTip;
			room.DoorBarKeep = sh.DoorBarKeep;
			room.DoorPortal = sh.DoorPortal;
		}

		private static float Surround(Shell sh, WallSide w, Hole h, DoorLook look, DoorKit k, float spring)
		{
			bool flag = look.Top != "square";
			switch (look.Frame)
			{
			case "stone":
			{
				float num8 = 0f;
				int num9 = 0;
				while (num8 < spring - 0.02f)
				{
					float num10 = Math.Min(spring, num8 + ((num9 % 2 == 0) ? 0.22f : 0.18f));
					if (spring - num10 < 0.07f)
					{
						num10 = spring;
					}
					float num11 = ((num9 % 2 == 0) ? 0.14500001f : 0.1f);
					float d = -0.035f - (float)(num9 % 3) * 0.004f;
					Block(sh, w, k.Ashlar, h.A0 - num11, h.A0 - 0.004f, num8 + 0.006f, num10 - 0.006f, d, 0f);
					Block(sh, w, k.Ashlar, h.A1 + 0.004f, h.A1 + num11, num8 + 0.006f, num10 - 0.006f, d, 0f);
					num8 = num10;
					num9++;
				}
				if (!flag)
				{
					Block(sh, w, k.Ashlar, h.A0 - 0.14500001f, h.A1 + 0.14500001f, h.Z1 + 0.006f, h.Z1 + 0.15f, -0.041f, 0f);
					return 0.041f;
				}
				for (int m = 0; m < 9; m++)
				{
					bool flag2 = m == 4;
					float num12 = ((float)m + 0.04f) / 9f;
					float num13 = ((float)m + 0.96f) / 9f;
					float e = (flag2 ? 0.125f : 0.1f);
					List<(float, float)> list2 = new List<(float, float)>
					{
						HeadPoint(look, h, num12, 0.004f),
						HeadPoint(look, h, num13, 0.004f)
					};
					for (int num14 = 3; num14 >= 0; num14--)
					{
						list2.Add(HeadPoint(look, h, num12 + (num13 - num12) * (float)num14 / 3f, e));
					}
					Block(sh, w, k.Ashlar, list2, flag2 ? (-0.055f) : (-0.035f - (float)(m % 2) * 0.005f), 0f);
				}
				return 0.035f;
			}
			case "gothic":
			{
				float[] array = new float[2] { -1f, 1f };
				foreach (float num5 in array)
				{
					float num6 = ((num5 < 0f) ? (h.A0 - 0.045f) : (h.A1 + 0.045f));
					Block(sh, w, k.Ashlar, num6 - 0.06f, num6 + 0.06f, 0f, 0.07f, -0.1f, 0f);
					Rod(sh, w, k.Ashlar, num6, 0.07f, num6, spring - 0.07f, -0.05f, 0.032f, 8);
					Block(sh, w, k.Ashlar, num6 - 0.065f, num6 + 0.065f, spring - 0.07f, spring, -0.11f, 0f);
					Block(sh, w, k.Iron, num6 - 0.05f, num6 + 0.05f, spring - 0.085f, spring - 0.07f, -0.09f, 0f);
					float num7 = ((num5 < 0f) ? (h.A0 - 0.19f) : (h.A1 + 0.11f));
					Block(sh, w, k.Ashlar, num7, num7 + 0.08f, 0f, spring, -0.03f, 0f);
				}
				(float, float, float)[] array2 = new(float, float, float)[2]
				{
					(0.004f, 0.06f, -0.065f),
					(0.09f, 0.19f, -0.03f)
				};
				for (int j = 0; j < array2.Length; j++)
				{
					(float, float, float) tuple = array2[j];
					float item = tuple.Item1;
					float item2 = tuple.Item2;
					float item3 = tuple.Item3;
					for (int l = 0; l < 12; l++)
					{
						float s = (float)l / 12f;
						float s2 = (float)(l + 1) / 12f;
						Block(sh, w, k.Ashlar, new(float, float)[4]
						{
							HeadPoint(look, h, s, item),
							HeadPoint(look, h, s2, item),
							HeadPoint(look, h, s2, item2),
							HeadPoint(look, h, s, item2)
						}, item3, 0f);
					}
				}
				(float, float) tuple2 = HeadPoint(look, h, 0.5f, 0.12f);
				sh.B.Blob(k.Iron, w.At(tuple2.Item1, tuple2.Item2, -0.06f), 0.04f, 0.04f, 0.03f, 6);
				return 0.065f;
			}
			case "log":
			{
				float num15 = 0.058f;
				float num16 = -0.075f;
				float num17 = h.Z1 + 0.07f;
				float[] array = new float[2]
				{
					h.A0 - num15,
					h.A1 + num15
				};
				foreach (float a in array)
				{
					sh.B.Tube(k.Post, w.At(a, 0f, num16), w.At(a, num17 + 0.05f, num16), num15, num15 * 0.9f, 7);
					float[] array3 = new float[2] { -0.12f, -0.07f };
					foreach (float num18 in array3)
					{
						sh.B.Tube(k.Rope, w.At(a, num17 + num18 - 0.012f, num16), w.At(a, num17 + num18 + 0.012f, num16), num15 + 0.008f, num15 + 0.008f, 7);
					}
				}
				sh.B.Tube(k.Post, w.At(h.A0 - 0.22f, num17, num16 - 0.005f), w.At(h.A1 + 0.22f, num17 + 0.015f, num16 - 0.005f), 0.062f, 0.058f, 7);
				return 0f - num16 + num15;
			}
			default:
			{
				Block(sh, w, k.Post, h.A0 - 0.11f, h.A0, 0f, spring, -0.058f, 0f);
				Block(sh, w, k.Post, h.A1, h.A1 + 0.11f, 0f, spring, -0.058f, 0f);
				if (flag)
				{
					for (int i = 0; i < 7; i++)
					{
						float num = ((float)i + 0.02f) / 7f;
						float num2 = ((float)i + 0.98f) / 7f;
						List<(float, float)> list = new List<(float, float)>
						{
							HeadPoint(look, h, num, 0f),
							HeadPoint(look, h, num2, 0f)
						};
						for (int num3 = 3; num3 >= 0; num3--)
						{
							list.Add(HeadPoint(look, h, num + (num2 - num) * (float)num3 / 3f, 0.11f));
						}
						Block(sh, w, k.Beam, list, -0.058f, 0f);
					}
					return 0.058f;
				}
				Block(sh, w, k.Beam, h.A0 - 0.11f - 0.07f, h.A1 + 0.11f + 0.07f, h.Z1, h.Z1 + 0.13f, -0.07f, 0f);
				float[] array = new float[2]
				{
					h.A0 - 0.11f - 0.035f,
					h.A1 + 0.11f + 0.035f
				};
				foreach (float num4 in array)
				{
					Block(sh, w, k.Beam, num4 - 0.035f, num4 + 0.035f, h.Z1 - 0.1f, h.Z1, -0.048f, 0f);
					Block(sh, w, k.Gap, num4 - 0.012f, num4 + 0.012f, h.Z1 + 0.055f, h.Z1 + 0.079f, -0.072f, -0.07f);
				}
				return 0.07f;
			}
			}
		}

		private static void HideCurtain(Shell sh, WallSide w, Hole h, DoorKit k)
		{
			MeshBuilder b = sh.B;
			float a = (h.A0 + h.A1) * 0.5f;
			float num = h.Z1 - 0.02f;
			Vec3 toward = w.At(a, num * 0.5f, -1.5f);
			FaceToward(b, k.Gap, toward, w.At(h.A0, 0f, 0.14f), w.At(h.A1, 0f, 0.14f), w.At(h.A1, h.Z1, 0.14f), w.At(h.A0, h.Z1, 0.14f));
			float num2 = h.A0 - 0.08f;
			float num3 = h.A1 + 0.08f;
			float[] array = new float[10];
			float[] array2 = new float[10];
			for (int i = 0; i <= 9; i++)
			{
				array[i] = 0.03f + 0.05f * (float)(TerrainModel.Hash(i * 13 + 5, 77) & 0xF) / 15f;
				array2[i] = -0.035f - ((i % 2 == 0) ? 0f : 0.03f);
			}
			for (int j = 0; j < 9; j++)
			{
				float a2 = num2 + (num3 - num2) * (float)j / 9f;
				float a3 = num2 + (num3 - num2) * (float)(j + 1) / 9f;
				FaceToward(b, k.Hide, toward, w.At(a2, array[j], array2[j]), w.At(a3, array[j + 1], array2[j + 1]), w.At(a3, num, array2[j + 1]), w.At(a2, num, array2[j]));
			}
			Rod(sh, w, k.Post, h.A0 - 0.2f, num + 0.03f, h.A1 + 0.2f, num + 0.03f, -0.07f, 0.035f);
			float[] array3 = new float[2]
			{
				h.A0 - 0.2f,
				h.A1 + 0.2f
			};
			foreach (float a4 in array3)
			{
				b.Blob(k.Bone, w.At(a4, num + 0.03f, -0.07f), 0.045f, 0.04f, 0.04f, 6);
			}
			array3 = new float[2]
			{
				h.A0 - 0.12f,
				h.A1 + 0.12f
			};
			foreach (float a5 in array3)
			{
				b.Tube(k.Post, w.At(a5, 0f, -0.06f), w.At(a5, num + 0.09f, -0.06f), 0.05f, 0.045f, 6);
			}
		}

		private static void TentFlap(Shell sh, WallSide w, IList<(float inset, float z)> profile, Func<float, float> half, uint cloth, int toggleSlot)
		{
			MeshBuilder b = sh.B;
			int slot = b.Slot("flap", 7, TerrainCache.Scale(cloth, 0.94f), 0);
			int slot2 = b.Slot("hem", 7, TerrainCache.Scale(cloth, 0.72f), 0);
			int slot3 = b.Slot("tie", 0, TerrainCache.Rgb(110, 86, 58), 0);
			float num = w.Length * 0.5f;
			Vec3 toward = w.At(num, 0.5f, -2f);
			for (int i = 0; i + 1 < profile.Count; i++)
			{
				float item = profile[i].z;
				float item2 = profile[i + 1].z;
				float num2 = half(item);
				float num3 = half(item2);
				Vec3 toward2 = (Point(num, i, 0f) + Point(num, i + 1, 0f)) * 0.5f + w.Inward;
				FaceToward(b, slot, toward2, Point(num - num2, i, 0.022f), Point(num + 0.05f, i, 0.022f), Point(num + 0.05f, i + 1, 0.022f), Point(num - num3, i + 1, 0.022f));
				FaceToward(b, slot, toward2, Point(num - 0.020000001f, i, 0.034f), Point(num + num2, i, 0.034f), Point(num + num3, i + 1, 0.034f), Point(num - 0.020000001f, i + 1, 0.034f));
				FaceToward(b, sh.Dark, toward2, Point(num - 0.020000001f - 0.008f, i, 0.036f), Point(num - 0.020000001f + 0.004f, i, 0.036f), Point(num - 0.020000001f + 0.004f, i + 1, 0.036f), Point(num - 0.020000001f - 0.008f, i + 1, 0.036f));
				FaceToward(b, slot2, toward2, Point(num - num2, i, 0.025f), Point(num - num2 + 0.035f, i, 0.025f), Point(num - num3 + 0.035f, i + 1, 0.025f), Point(num - num3, i + 1, 0.025f));
				FaceToward(b, slot2, toward2, Point(num + num2 - 0.035f, i, 0.037f), Point(num + num2, i, 0.037f), Point(num + num3, i + 1, 0.037f), Point(num + num3 - 0.035f, i + 1, 0.037f));
			}
			int index = profile.Count - 1;
			float item3 = profile[index].z;
			float num4 = half(item3);
			float item4 = profile[index].inset;
			if (num4 > 0.08f)
			{
				FaceToward(b, slot2, toward, w.At(num - num4, item3 - 0.05f, 0f - item4 - 0.04f), w.At(num + num4, item3 - 0.05f, 0f - item4 - 0.04f), w.At(num + num4, item3, 0f - item4 - 0.04f), w.At(num - num4, item3, 0f - item4 - 0.04f));
			}
			float item5 = profile[0].z;
			float num5 = half(item5);
			FaceToward(b, slot2, toward, w.At(num - num5, item5, 0f - profile[0].inset - 0.026f), w.At(num + 0.05f, item5, 0f - profile[0].inset - 0.026f), w.At(num + 0.05f, item5 + 0.06f, 0f - profile[0].inset - 0.026f), w.At(num - num5, item5 + 0.06f, 0f - profile[0].inset - 0.026f));
			FaceToward(b, slot2, toward, w.At(num - 0.020000001f, item5, 0f - profile[0].inset - 0.038f), w.At(num + num5, item5, 0f - profile[0].inset - 0.038f), w.At(num + num5, item5 + 0.06f, 0f - profile[0].inset - 0.038f), w.At(num - 0.020000001f, item5 + 0.06f, 0f - profile[0].inset - 0.038f));
			float item6 = profile[profile.Count - 1].z;
			for (float num6 = 0.22f; num6 < item6 - 0.12f; num6 += 0.26f)
			{
				int j;
				for (j = 0; j + 2 < profile.Count && profile[j + 1].z < num6; j++)
				{
				}
				float num7 = (num6 - profile[j].z) / Math.Max(0.0001f, profile[j + 1].z - profile[j].z);
				float num8 = profile[j].inset + (profile[j + 1].inset - profile[j].inset) * num7 + 0.05f;
				float num9 = num - 0.020000001f;
				b.Tube(toggleSlot, w.At(num9 - 0.045f, num6, 0f - num8), w.At(num9 + 0.035f, num6 + 0.01f, 0f - num8), 0.012f, 0.012f, 5);
				b.Tube(slot3, w.At(num9 + 0.005f, num6 - 0.03f, 0f - num8 + 0.008f), w.At(num9 + 0.005f, num6 + 0.03f, 0f - num8 + 0.008f), 0.006f, 0.006f, 3);
			}
			Vec3 Point(float a, int index2, float extra)
			{
				return w.At(a, profile[index2].z, 0f - profile[index2].inset - extra);
			}
		}

		private static List<(char Wall, float At)> RaceWindows(string race, int W, int D)
		{
			List<(char, float)> list = new List<(char, float)>
			{
				('N', 0.2f),
				('N', 0.8f)
			};
			if (!(race == "orc"))
			{
				if (race == "dwarf")
				{
					if (D >= 5)
					{
						list.Add(('E', 0.18f));
					}
				}
				else
				{
					list.Add(('W', (D >= 5) ? 0.5f : 0.3f));
					if (D >= 5)
					{
						list.Add(('E', 0.18f));
					}
				}
			}
			if (W >= 6)
			{
				list.Add(('S', 0.2f));
				list.Add(('S', 0.8f));
			}
			return list;
		}

		private static Func<float, float, float> RaceCeiling(string race, Style st, float x0, float y0, float x1, float y1)
		{
			float H = st.WallH;
			float ym = (y0 + y1) * 0.5f;
			float half = (y1 - y0) * 0.5f;
			float D = y1 - y0;
			switch (race)
			{
			case "dwarf":
				return (float num, float num2) => H - 0.02f;
			case "elf":
				return delegate(float num3, float num2)
				{
					float num = Math.Min(1f, Math.Abs(num2 - ym) / half);
					return H + D * 0.3f * (float)Math.Sqrt(1f - num * num) - 0.05f;
				};
			case "orc":
				return (float num2, float num) => H + (D * 0.4f + 0.3f) * (1f - Math.Min(1f, Math.Abs(num - ym) / half)) - 0.06f;
			default:
				return (float num2, float num) => H + D * 0.32f * (1f - Math.Min(1f, Math.Abs(num - ym) / half)) - 0.06f;
			}
		}

		private static void HearthSeats(Layout L, float[] hearth, string seat, string between, float back = 0.72f, float apart = 0.62f)
		{
			if (hearth != null)
			{
				float num = (hearth[0] + hearth[2]) * 0.5f;
				float num2 = hearth[1] - back;
				L.PutNear(seat, num - apart, num2, 1.1207964f, 0.2f);
				L.PutNear(seat, num + apart, num2, 2.0207963f, 0.2f);
				if (between != null)
				{
					L.PutNear(between, num, num2 - 0.08f, MathF.PI / 2f, 0.15f);
				}
			}
		}

		private static void BedsWest(Layout L, string bed, string chest, string nightstand, float[] ys, Random rng)
		{
			foreach (float along in ys)
			{
				if (!L.Fit(bed, 'W', along, 0.45f))
				{
					continue;
				}
				float[] last = L.Last;
				float lastY = L.LastY;
				bool flag = lastY > (L.Y0 + L.Y1) * 0.5f;
				if (chest != null)
				{
					float item = L.X0 + 0.04f + L.Depth(chest) * 0.5f;
					float num = L.Width(chest) * 0.5f + 0.06f;
					float item2 = (flag ? (last[1] - num) : (last[3] + num));
					float item3 = (flag ? (last[3] + num) : (last[1] - num));
					L.PutNearAny(chest, 0f, 0.15f, (last[2] + 0.06f + L.Depth(chest) * 0.5f, lastY), (item, item2), (item, item3));
				}
				if (nightstand != null)
				{
					float num2 = L.Width(nightstand) * 0.5f + 0.04f;
					float y = (flag ? (last[1] - num2) : (last[3] + num2));
					float y2 = (flag ? (last[3] + num2) : (last[1] - num2));
					if (!L.Put(nightstand, last[0] + num2, y, 0f))
					{
						L.Put(nightstand, last[0] + num2, y2, 0f);
					}
				}
			}
		}

		private static void KitchenEast(Layout L, float y, int tier, Random rng, string counter = "int_p_counter")
		{
			if (L.Fit(counter, 'E', y))
			{
				float lastY = L.LastY;
				float num = L.Height(counter);
				float z = Math.Min(num + 0.42f, L.WallTop - L.Height("int_p_wallshelf") - 0.12f);
				L.Hang("int_p_wallshelf", 'E', lastY - 0.08f, z, 0.35f);
				L.Hang("int_p_potrack", 'E', lastY + 0.62f, Math.Min(num + 0.3f, L.WallTop - L.Height("int_p_potrack") - 0.12f), 0.4f);
				float num2 = ((lastY + 0.6f < L.Y1 - 0.45f) ? (lastY + 0.6f) : (lastY - 0.6f));
				float num3 = ((L.CeilingAt != null) ? L.CeilingAt(L.X1 - 0.75f, num2) : L.WallTop);
				L.Ceiling("int_p_herbs", L.X1 - 0.75f, num2, Math.Min(1.18f, num3 - L.Height("int_p_herbs") - 0.04f), MathF.PI / 2f);
				L.PutNear("int_p_bucket", L.X1 - 0.2f, lastY - 0.75f, 0f, 0.3f);
			}
			L.PutNear("int_barrel", L.X1 - 0.22f, L.Y0 + 0.22f, (float)rng.NextDouble() * 6f, 0.25f);
			L.PutNear("int_sack", L.X1 - 0.62f, L.Y0 + 0.2f, (float)rng.NextDouble() * 6f, 0.25f);
			if (tier >= 1)
			{
				L.PutNearAny("int_p_jars", MathF.PI, 0.3f, (L.X1 - 0.22f, L.Y0 + 0.62f), (L.X1 - 1f, L.Y0 + 0.22f));
			}
			if (tier >= 2)
			{
				L.PutNear("int_p_basket", L.X1 - 0.95f, L.Y0 + 0.2f, MathF.PI / 2f, 0.3f);
			}
			if (tier >= 3)
			{
				L.PutNear("int_barrel", L.X1 - 0.55f, L.Y0 + 0.58f, 1f, 0.3f);
			}
		}

		private static void Dining(Layout L, string table, string seat, string rug, float x, float y, int seats, bool round, bool ring)
		{
			if (rug != null)
			{
				L.Put(rug, x, y, -MathF.PI / 2f, solid: false, anywhere: true);
			}
			TableWithSeats(L, table, seat, x, y, seats, round);
			if (ring)
			{
				float num = ((L.CeilingAt != null) ? L.CeilingAt(x, y) : L.WallTop);
				L.Ceiling("int_p_candlering", x, y, Math.Min(1.32f, num - L.Height("int_p_candlering") - 0.04f));
			}
		}

		private static void FurnishHuman(Layout L, int tier, Random rng)
		{
			float x = L.X0;
			float y = L.Y0;
			float x2 = L.X1;
			float y2 = L.Y1;
			float num = x2 - x;
			float num2 = y2 - y;
			float num3 = (x + x2) * 0.5f;
			string text = ((tier >= 2 && L.Has("int_p_fireplace")) ? "int_p_fireplace" : "int_human_hearth");
			L.AgainstWall(text, 'N', num3);
			float[] last = L.Last;
			if (last != null)
			{
				L.Fit("int_p_firewood", 'N', last[2] + 0.06f + L.Width("int_p_firewood") * 0.5f, 0.25f);
				if (text == "int_human_hearth" && tier >= 1)
				{
					L.PutNear("int_p_cauldron", last[2] + 0.25f, last[1] - 0.2f, 0f, 0.15f);
				}
				L.Put((tier >= 1) ? "int_p_pelt" : "int_human_rug", num3, last[1] - 0.42f, -MathF.PI / 2f, solid: false, anywhere: true);
				if (tier >= 1)
				{
					HearthSeats(L, last, "int_p_armchair", "int_p_sidetable");
				}
				else
				{
					L.PutNear("int_human_bench", num3, last[1] - 0.7f, MathF.PI / 2f, 0.2f);
				}
				L.Hang("int_p_sconce", 'N', last[0] - 0.18f, 0.82f, 0.2f);
				L.Hang("int_p_sconce", 'N', last[2] + 0.18f, 0.82f, 0.2f);
			}
			BedsWest(L, "int_human_bed", "int_human_chest", (tier >= 1) ? "int_p_sidetable" : null, (tier < 2) ? new float[1] { y + num2 * 0.62f } : new float[2]
			{
				y + num2 * 0.22f,
				y + num2 * 0.7f
			}, rng);
			if (tier >= 1)
			{
				L.Fit("int_human_wardrobe", 'N', x + 0.06f + L.Width("int_human_wardrobe") * 0.5f, 0.3f);
			}
			if (tier >= 3)
			{
				L.Fit("int_human_bookcase", 'S', x + 0.06f + L.Width("int_human_bookcase") * 0.5f, 0.6f);
			}
			KitchenEast(L, y + num2 * 0.55f, tier, rng);
			Dining(L, "int_human_table", (tier >= 2) ? "int_human_chair" : "int_human_bench", (tier >= 1) ? "int_human_rug" : null, x + num * ((num >= 6f) ? 0.66f : 0.7f), y + num2 * 0.44f, (tier >= 2) ? (2 + Math.Min(2, tier - 1)) : 2, round: false, tier >= 2);
			L.Fit("int_p_broom", 'S', L.Room.DoorX + 0.62f, 0.2f);
			L.Hang("int_p_sconce", 'S', L.Room.DoorX - 0.62f, 0.82f, 0.15f);
			if (num >= 6f)
			{
				L.Fit("int_human_bench", 'S', x + num * 0.2f, 0.4f);
			}
			if (tier >= 1)
			{
				L.Hang("int_p_shield", 'W', y + num2 * 0.5f, 0.98f, 1.2f, centred: true);
			}
			if (tier >= 2)
			{
				L.Hang("int_p_arms", 'E', y + num2 * 0.15f, 0.9f, 0.8f, centred: true);
			}
			if (tier >= 3)
			{
				L.Hang("int_p_antlers", 'W', y + num2 * 0.2f, 0.98f, 1f, centred: true);
			}
			if (tier >= 3)
			{
				L.Hang("int_p_tapestry", 'S', x + num * 0.32f, 0.5f, 0.8f, centred: true);
			}
			if (tier >= 4)
			{
				L.Hang("int_p_shield_b", 'E', y + num2 * 0.88f, 0.98f, 0.6f, centred: true);
			}
			if (tier >= 2)
			{
				L.Hang("int_p_sconce", 'W', y + num2 * 0.5f, 0.82f, 1f);
			}
		}

		private static void FurnishElf(Layout L, int tier, Random rng)
		{
			float x = L.X0;
			float y = L.Y0;
			float x2 = L.X1;
			float y2 = L.Y1;
			float num = x2 - x;
			float num2 = y2 - y;
			float num3 = (x + x2) * 0.5f;
			L.AgainstWall("int_elf_hearth", 'N', num3);
			float[] last = L.Last;
			if (last != null)
			{
				L.Put("int_elf_rug", num3, last[1] - 0.5f, -MathF.PI / 2f, solid: false, anywhere: true);
				HearthSeats(L, last, "int_v_cushions", null, 0.6f, 0.76f);
				if (tier >= 2)
				{
					L.Fit("int_v_harp", 'N', last[2] + 0.35f, 0.3f);
				}
				L.Hang("int_p_sconce", 'N', last[0] - 0.2f, 0.85f, 0.2f);
				L.Hang("int_p_sconce", 'N', last[2] + 0.2f, 0.85f, 0.2f);
			}
			BedsWest(L, "int_elf_bed", "int_elf_chest", null, (tier < 2) ? new float[1] { y + num2 * 0.62f } : new float[2]
			{
				y + num2 * 0.22f,
				y + num2 * 0.72f
			}, rng);
			L.Fit("int_elf_shelf", 'E', y + num2 * 0.6f, 0.6f);
			if (tier >= 1)
			{
				L.Fit("int_v_lectern", 'E', y + num2 * 0.36f, 0.4f);
			}
			if (tier >= 3)
			{
				L.Fit("int_elf_shelf", 'E', y + num2 * 0.82f, 0.4f);
			}
			L.PutNear("int_elf_lantern", x2 - 0.24f, y2 - 0.3f, MathF.PI * -3f / 4f, 0.3f);
			if (tier >= 2)
			{
				L.PutNear("int_elf_lantern", x + 0.24f, y + 0.3f, MathF.PI / 4f, 0.3f);
			}
			L.PutNear("int_elf_plant", x2 - 0.3f, y + 0.3f, (float)rng.NextDouble() * 6f, 0.3f);
			if (tier >= 2)
			{
				L.PutNear("int_v_pot_tree", x + 0.32f, y2 - 0.32f, 0.5f, 0.3f);
			}
			L.Fit("int_p_counter", 'S', x + num * 0.25f, 0.6f);
			if (L.Last != null)
			{
				L.HangAny("int_p_wallshelf", L.Height("int_p_counter") + 0.45f, false, ('S', L.LastX), ('S', L.LastX + 0.8f), ('W', y + 0.62f));
			}
			L.PutNear("int_p_basket", x2 - 0.75f, y + 0.25f, MathF.PI / 2f, 0.3f);
			if (tier >= 1)
			{
				L.PutNear("int_p_jars", x2 - 0.3f, y + 0.65f, MathF.PI, 0.3f);
			}
			Dining(L, "int_elf_table", "int_elf_stool", null, x + num * 0.68f, y + num2 * 0.42f, 3 + ((tier >= 3) ? 1 : 0), round: true, ring: false);
			if (tier >= 1)
			{
				L.Put("int_v_chandelier", x + num * 0.68f, y + num2 * 0.42f, 0f, solid: false, anywhere: true);
			}
			if (tier >= 1)
			{
				L.HangAny("int_p_tapestry", 0.42f, ('W', y + num2 * 0.5f), ('E', y + num2 * 0.82f), ('S', x2 - num * 0.3f));
			}
			if (tier >= 3)
			{
				L.Hang("int_p_shield_b", 'E', y + num2 * 0.15f, 1f, 0.6f, centred: true);
			}
		}

		private static void FurnishOrc(Layout L, int tier, Random rng)
		{
			float x = L.X0;
			float y = L.Y0;
			float x2 = L.X1;
			float y2 = L.Y1;
			float num = x2 - x;
			float num2 = y2 - y;
			float num3 = (x + x2) * 0.5f;
			float num4 = y + num2 * 0.5f;
			L.Put("int_orc_rug", num3, num4 - 0.85f, -MathF.PI / 2f, solid: false, anywhere: true);
			L.Put("int_orc_firepit", num3, num4, -MathF.PI / 2f);
			float[] last = L.Last;
			if (last != null)
			{
				L.Put("int_p_pelt", num3 - 0.95f, num4 + 0.1f, 0.4f, solid: false, anywhere: true);
				L.PutNear("int_p_cauldron", last[2] + 0.28f, num4 + 0.2f, 0f, 0.15f);
				L.PutNear("int_orc_stool", num3 - 0.45f, last[1] - 0.32f, 1.1707964f, 0.15f);
				L.PutNear("int_orc_stool", num3 + 0.45f, last[1] - 0.32f, 1.9707963f, 0.15f);
				if (tier >= 1)
				{
					L.PutNear("int_orc_drum", last[0] - 0.5f, num4 - 0.45f, 0f, 0.2f);
				}
			}
			L.Fit("int_p_firewood", 'N', x + num * 0.33f, 0.5f);
			L.Fit("int_orc_rack", 'N', num3 + 0.45f, 0.6f);
			if (tier >= 1)
			{
				L.PutNear("int_orc_totem", x + 0.28f, y2 - 0.28f, -MathF.PI / 4f, 0.3f);
			}
			char[] array = ((tier < 2) ? new char[1] { 'W' } : new char[2] { 'W', 'E' });
			foreach (char wall in array)
			{
				if (L.Fit("int_orc_bed", wall, y + num2 * 0.7f, 0.4f))
				{
					float[] last2 = L.Last;
					L.Fit("int_orc_chest", wall, last2[1] - 0.08f - L.Width("int_orc_chest") * 0.5f, 0.3f);
				}
			}
			if (L.PutNear("int_orc_table", x2 - 0.9f, y + 1f, MathF.PI, 0.4f))
			{
				float[] last3 = L.Last;
				float lastX = L.LastX;
				float lastY = L.LastY;
				L.PutNear("int_orc_stool", last3[0] - 0.22f, lastY - 0.22f, 0f, 0.15f);
				L.PutNear("int_orc_stool", last3[0] - 0.22f, lastY + 0.24f, 0f, 0.15f);
				if (tier >= 1)
				{
					L.PutNear("int_orc_stool", lastX, last3[3] + 0.22f, -MathF.PI / 2f, 0.15f);
				}
			}
			L.PutNear("int_barrel", x + 0.22f, y + 0.22f, (float)rng.NextDouble() * 6f, 0.25f);
			L.PutNear("int_sack", x + 0.6f, y + 0.2f, (float)rng.NextDouble() * 6f, 0.25f);
			if (tier >= 1)
			{
				L.PutNear("int_crate", x + 0.24f, y + 0.62f, 0.3f, 0.25f);
			}
			L.Hang("int_p_antlers", 'N', x + num * 0.2f, 0.7f, 0.8f, centred: true);
			if (tier >= 1)
			{
				L.HangAny("int_p_shield", 0.72f, ('W', y + num2 * 0.45f), ('S', x + num * 0.3f), ('N', x2 - num * 0.2f));
			}
			if (tier >= 2)
			{
				L.HangAny("int_p_shield_b", 0.72f, ('E', y + num2 * 0.45f), ('N', x2 - num * 0.15f), ('S', x2 - num * 0.3f));
			}
			if (tier >= 3)
			{
				L.Hang("int_p_arms", 'S', x + num * 0.3f, 0.5f, 0.6f, centred: true);
			}
			if (tier >= 2)
			{
				L.Fit("int_v_banner", 'S', x2 - num * 0.32f, 0.5f);
			}
		}

		private static void FurnishDwarf(Layout L, int tier, Random rng)
		{
			float x = L.X0;
			float y = L.Y0;
			float x2 = L.X1;
			float y2 = L.Y1;
			float num = x2 - x;
			float num2 = y2 - y;
			float along = (x + x2) * 0.5f;
			L.AgainstWall("int_dwarf_forge", 'N', along);
			float[] last = L.Last;
			if (last != null)
			{
				L.PutNear("int_dwarf_anvil", last[0] + 0.2f, last[1] - 0.45f, 1.8849558f, 0.2f);
				L.PutNear("int_dwarf_stool", last[2] - 0.15f, last[1] - 0.5f, 1.9707963f, 0.2f);
				L.Fit("int_p_firewood", 'N', last[2] + 0.06f + L.Width("int_p_firewood") * 0.5f, 0.25f);
				L.Hang("int_p_sconce", 'N', last[0] - 0.18f, 0.78f, 0.2f);
				L.Hang("int_p_sconce", 'N', last[2] + 0.18f, 0.78f, 0.2f);
			}
			BedsWest(L, "int_dwarf_bed", "int_dwarf_chest", null, (tier < 2) ? new float[1] { y + num2 * 0.68f } : new float[2]
			{
				y + num2 * 0.22f,
				y + num2 * 0.78f
			}, rng);
			if (tier >= 2)
			{
				L.Fit("int_dwarf_shelf", 'W', y + num2 * 0.5f, 0.4f);
			}
			L.Fit("int_dwarf_kegs", 'E', y + num2 * 0.3f, 0.5f);
			KitchenEast(L, y + num2 * 0.62f, tier, rng);
			Dining(L, "int_dwarf_table", "int_dwarf_stool", (tier >= 2) ? "int_orc_rug" : null, x + num * 0.6f, y + num2 * 0.42f, 2 + Math.Min(2, tier), round: false, tier >= 1);
			if (tier >= 1)
			{
				L.Fit("int_dwarf_rack", 'S', x + num * 0.25f, 0.6f);
			}
			L.Hang("int_p_sconce", 'S', L.Room.DoorX + 0.6f, 0.75f, 0.15f);
			L.Hang("int_p_arms", 'W', y + num2 * 0.5f, 0.55f, 0.8f, centred: true);
			if (tier >= 1)
			{
				L.Hang("int_p_shield", 'S', x2 - num * 0.25f, 0.62f, 0.6f, centred: true);
			}
			if (tier >= 3)
			{
				L.Hang("int_p_shield_b", 'E', y + num2 * 0.85f, 0.62f, 0.5f, centred: true);
			}
			if (tier >= 3)
			{
				L.Fit("int_v_banner", 'S', x + num * 0.15f, 0.4f);
			}
		}

		public static Interior BuildFitted(HousePlan plan, float scale, bool mirror, uint wallColor, uint roofColor, int seed, string race, int tier, Func<string, MeshModel> models, string style)
		{
			race = Known(race);
			Interior interior = new Interior
			{
				Race = race,
				Tier = tier
			};
			Random rng = new Random(seed);
			Style style2 = StyleFor(race, wallColor, roofColor);
			FitInfo fitInfo = (interior.Fit = new FitInfo
			{
				Plan = plan,
				Scale = scale,
				Mirror = mirror,
				FloorUp = plan.FloorZ * scale,
				Tent = (style?.StartsWith("tent", StringComparison.Ordinal) ?? false)
			});
			float num = (mirror ? (0f - scale) : scale);
			float num2 = float.MaxValue;
			float num3 = float.MaxValue;
			float num4 = float.MinValue;
			float num5 = float.MinValue;
			foreach (HousePlan.Storey storey in plan.Storeys)
			{
				foreach (List<HousePlan.P2> loop in storey.Loops)
				{
					foreach (HousePlan.P2 item in loop)
					{
						num2 = Math.Min(num2, item.X * num);
						num4 = Math.Max(num4, item.X * num);
						num3 = Math.Min(num3, item.Y * scale);
						num5 = Math.Max(num5, item.Y * scale);
					}
				}
			}
			fitInfo.OffsetX = 1f - num2;
			fitInfo.OffsetY = 1f - num3;
			float minX = 1f;
			float minY = 1f;
			float num6 = 1f + (num4 - num2);
			float num7 = 1f + (num5 - num3);
			fitInfo.StoreyZ = new float[plan.Storeys.Count];
			for (int i = 0; i < plan.Storeys.Count; i++)
			{
				fitInfo.StoreyZ[i] = (plan.Storeys[i].Floor - plan.FloorZ) * scale;
			}
			if (plan.Storeys.Count > 1 && plan.Storeys[0].Area * scale * scale < 3.2f)
			{
				fitInfo.StoreyZ = new float[1];
				fitInfo.UpperShut = true;
			}
			else if (plan.Storeys.Count > 1)
			{
				PlanStair(fitInfo, (race == "dwarf") ? 0.92f : 1.05f);
				if (float.IsNaN(fitInfo.StairFootX))
				{
					fitInfo.StoreyZ = new float[1];
					fitInfo.UpperShut = true;
				}
			}
			int num8 = (int)Math.Ceiling(num6) + 2;
			int num9 = (int)Math.Ceiling(num7) + 2;
			TerrainCache terrainCache = new TerrainCache();
			terrainCache.Allocate(num8, num9);
			terrainCache.CastShadows = false;
			terrainCache.DitherEdges = false;
			terrainCache.OutsideIsWater = false;
			terrainCache.OutsideHeight = style2.WallH;
			terrainCache.OutsideColor = TerrainCache.Scale(style2.Wall, 0.7f);
			for (int j = 0; j < num9; j++)
			{
				for (int k = 0; k < num8; k++)
				{
					int num10 = j * num8 + k;
					terrainCache.BaseColors[num10] = ((race == "dwarf") ? TerrainCache.Scale(style2.Floor, 0.45f) : TerrainCache.Scale(style2.Floor, 0.95f + (float)(TerrainModel.Hash(k, j * 7) & 7) / 160f));
					terrainCache.Materials[num10] = style2.FloorSurface;
					if (style2.MeshFloor || race == "dwarf")
					{
						terrainCache.Heights[num10] = (terrainCache.Ground[num10] = -0.03f);
					}
				}
			}
			terrainCache.Relight(0, 0, num8, num9);
			interior.Floor = terrainCache;
			interior.CeilingTop = TerrainCache.Scale(style2.Ceiling, 0.5f);
			interior.CeilingBottom = TerrainCache.Scale(style2.Ceiling, 0.65f);
			HousePlan.Opening door = plan.Door;
			float num11 = fitInfo.RoomX(door.X);
			float num12 = fitInfo.RoomY(door.Y);
			float num13 = (0f - door.OutX) * (mirror ? (-1f) : 1f);
			float num14 = 0f - door.OutY;
			float num15 = (float)Math.Sqrt(num13 * num13 + num14 * num14);
			num13 /= num15;
			num14 /= num15;
			interior.DoorX = num11 + num13 * 0.45f;
			interior.DoorY = num12 + num14 * 0.45f;
			interior.SpawnX = num11 + num13 * 0.75f;
			interior.SpawnY = num12 + num14 * 0.75f;
			interior.SpawnYaw = (float)Math.Atan2(num14, num13);
			interior.DoorOutX = 0f - num13;
			interior.DoorOutY = 0f - num14;
			interior.MinX = minX;
			interior.MaxX = num6;
			interior.MinY = minY;
			interior.MaxY = num7;
			interior.Meshes.Add(BuildFittedShell(interior, fitInfo, style2, race, style, tier, rng));
			if (!float.IsNaN(fitInfo.DoorWallX))
			{
				interior.DoorX = fitInfo.DoorWallX + num13 * 0.45f;
				interior.DoorY = fitInfo.DoorWallY + num14 * 0.45f;
				interior.SpawnX = fitInfo.DoorWallX + num13 * 0.75f;
				interior.SpawnY = fitInfo.DoorWallY + num14 * 0.75f;
			}
			if (!float.IsNaN(fitInfo.StairFootX))
			{
				interior.Meshes.Add(Stair(fitInfo, style2, race));
			}
			FurnishFitted(interior, fitInfo, models, race, tier, roofColor, rng, style);
			return interior;
		}

		private static MeshInstance BuildFittedShell(Interior room, FitInfo fit, Style st, string race, string style, int tier, Random rng)
		{
			HousePlan plan = fit.Plan;
			Shell shell = NewShell(st, race);
			shell.Look = LookFor(race, style);
			MeshBuilder b = shell.B;
			b.Part("room", -1, new Vec3(0f, 0f, 0f));
			for (int i = 0; i < fit.StoreyZ.Length; i++)
			{
				float z = fit.StoreyZ[i];
				foreach (WallSide w in shell.Walls = Walls(fit, i))
				{
					w.Holes.Sort((Hole p, Hole q) => p.A0.CompareTo(q.A0));
					int storey = i;
					WallUp(shell, w, (float a) => fit.CeilingAt(storey, w.Start.X + w.Along.X * a + w.Inward.X * 0.1f, w.Start.Y + w.Along.Y * a + w.Inward.Y * 0.1f) - z + 0.06f);
					if (i > 0)
					{
						b.Face(shell.Wall, w.At(0f, 0f), w.At(0f, 0f, 0.14f), w.At(w.Length, 0f, 0.14f), w.At(w.Length, 0f));
					}
					foreach (Hole hole in w.Holes)
					{
						Opening(shell, w, hole, race);
						if (hole.Door)
						{
							room.DoorLeafPart = shell.DoorLeafPart;
							continue;
						}
						room.Windows.Add(new LightWindow
						{
							Center = w.At((hole.A0 + hole.A1) * 0.5f, (hole.Z0 + hole.Z1) * 0.5f, 0.08400001f),
							Right = w.Along * -1f,
							HalfWidth = (hole.A1 - hole.A0) * 0.5f,
							HalfHeight = (hole.Z1 - hole.Z0) * 0.5f,
							Inward = w.Inward
						});
					}
					FitFrame(shell, w, i, fit, race);
				}
				FloorOf(shell, fit, i, st);
				CeilingOf(shell, fit, i, st, race);
				room.Top = Math.Max(room.Top, (plan.Storeys[i].Top - plan.FloorZ) * fit.Scale);
			}
			if (fit.UpperShut)
			{
				Trapdoor(shell, fit);
			}
			MeshModel meshModel = shell.B.Build();
			uint[] array = new uint[meshModel.SlotNames.Length];
			for (int num = 0; num < array.Length; num++)
			{
				array[num] = meshModel.SlotColor[num];
			}
			MeshInstance result = (room.Shell = new MeshInstance
			{
				Model = meshModel,
				Palette = array,
				NoShadow = true,
				Static = true,
				Tag = -1
			});
			room.GlassSlot = shell.Glass;
			TakeDoor(room, shell);
			return result;
		}

		private static void Trapdoor(Shell sh, FitInfo fit)
		{
			HousePlan plan = fit.Plan;
			double num = 0.0;
			double num2 = 0.0;
			int num3 = 0;
			for (float num4 = fit.RoomY(plan.Y0); num4 < fit.RoomY(plan.Y0 + (float)plan.H * 0.05f); num4 += 0.1f)
			{
				for (float num5 = Math.Min(fit.RoomX(plan.X0), fit.RoomX(plan.X0 + (float)plan.W * 0.05f)); num5 < Math.Max(fit.RoomX(plan.X0), fit.RoomX(plan.X0 + (float)plan.W * 0.05f)); num5 += 0.1f)
				{
					if (fit.Inside(0, num5, num4) && plan.InRoom(1, fit.ModelX(num5), fit.ModelY(num4)))
					{
						num += (double)num5;
						num2 += (double)num4;
						num3++;
					}
				}
			}
			if (num3 != 0)
			{
				float num6 = (float)(num / (double)num3);
				float num7 = (float)(num2 / (double)num3);
				float num8 = fit.CeilingAt(0, num6, num7) - 0.005f;
				MeshBuilder b = sh.B;
				b.Face(sh.Dark, new Vec3(num6 - 0.3f, num7 - 0.3f, num8), new Vec3(num6 - 0.3f, num7 + 0.3f, num8), new Vec3(num6 + 0.3f, num7 + 0.3f, num8), new Vec3(num6 + 0.3f, num7 - 0.3f, num8));
				for (int i = 0; i < 4; i++)
				{
					float num9 = -0.26000002f + (float)i * 0.52000004f / 3f;
					b.Box(sh.Door, new Vec3(num6 + num9, num7, num8 - 0.012f), 0.07f, 0.27f, 0.01f);
				}
				b.Box(sh.Timber, new Vec3(num6, num7 - 0.3f, num8 - 0.03f), 0.34f, 0.04f, 0.03f);
				b.Box(sh.Timber, new Vec3(num6, num7 + 0.3f, num8 - 0.03f), 0.34f, 0.04f, 0.03f);
				b.Box(sh.Timber, new Vec3(num6 - 0.3f, num7, num8 - 0.03f), 0.04f, 0.3f, 0.03f);
				b.Box(sh.Timber, new Vec3(num6 + 0.3f, num7, num8 - 0.03f), 0.04f, 0.3f, 0.03f);
				b.Tube(sh.Metal, new Vec3(num6 + 0.15f, num7 - 0.04f, num8 - 0.03f), new Vec3(num6 + 0.15f, num7 + 0.04f, num8 - 0.03f), 0.012f, 0.012f, 5);
			}
		}

		private static Shell NewShell(Style st, string race)
		{
			Shell obj = new Shell
			{
				S = st
			};
			MeshBuilder b = obj.B;
			obj.Wall = b.Slot("wall", st.WallKind, st.Wall, 0);
			obj.Timber = b.Slot("timber", 3, st.Timber, 0);
			obj.Floor = b.Slot("floor", (byte)((race == "dwarf") ? 14 : 3), st.Floor, 0);
			obj.Ceiling = b.Slot("ceiling", st.CeilingKind, st.Ceiling, 0);
			obj.Glass = b.Slot("glass", 12, TerrainCache.Rgb(200, 224, 246), 5);
			obj.Trim = b.Slot("trim", 3, st.Trim, 0);
			obj.Stone = b.Slot("stone", 4, st.Stone, 0);
			obj.Leaf = b.Slot("leaf", 1, st.Leaf, 0);
			obj.Bark = b.Slot("bark", 2, st.Bark, 0);
			obj.Door = b.Slot("door", 2, st.Door, 0);
			obj.Metal = b.Slot("metal", 10, st.Metal, 0);
			obj.Dark = b.Slot("dark", 0, st.Dark, 0);
			return obj;
		}

		private static List<WallSide> Walls(FitInfo fit, int storey)
		{
			HousePlan plan = fit.Plan;
			HousePlan.Storey storey2 = plan.Storeys[storey];
			List<WallSide> list = new List<WallSide>();
			float z = fit.StoreyZ[storey];
			foreach (List<HousePlan.P2> loop in storey2.Loops)
			{
				int count = loop.Count;
				Vec3[] array = new Vec3[count];
				for (int i = 0; i < count; i++)
				{
					array[i] = new Vec3(fit.RoomX(loop[i].X), fit.RoomY(loop[i].Y), z);
				}
				if (fit.Mirror)
				{
					Array.Reverse(array);
				}
				for (int j = 0; j < count; j++)
				{
					Vec3 start = array[j];
					Vec3 vec = array[(j + 1) % count];
					float num = (float)Math.Sqrt((vec.X - start.X) * (vec.X - start.X) + (vec.Y - start.Y) * (vec.Y - start.Y));
					if (!(num < 0.02f))
					{
						Vec3 along = new Vec3((vec.X - start.X) / num, (vec.Y - start.Y) / num, 0f);
						list.Add(new WallSide
						{
							Start = start,
							Along = along,
							Inward = new Vec3(along.Y, 0f - along.X, 0f),
							Length = num
						});
					}
				}
			}
			List<(HousePlan.Opening, bool)> list2 = new List<(HousePlan.Opening, bool)>();
			if (storey == 0)
			{
				list2.Add((plan.Door, true));
			}
			foreach (HousePlan.Opening window in plan.Windows)
			{
				if (window.Storey == storey)
				{
					list2.Add((window, false));
				}
			}
			foreach (var item3 in list2)
			{
				HousePlan.Opening item = item3.Item1;
				bool item2 = item3.Item2;
				float num2 = 0f - item.OutY;
				float outX = item.OutX;
				float num3 = fit.RoomX(item.X - num2 * item.HalfWidth);
				float num4 = fit.RoomY(item.Y - outX * item.HalfWidth);
				float num5 = fit.RoomX(item.X + num2 * item.HalfWidth);
				float num6 = fit.RoomY(item.Y + outX * item.HalfWidth);
				float num7 = item.OutX * (fit.Mirror ? (-1f) : 1f);
				float outY = item.OutY;
				float num8 = (num3 + num5) * 0.5f;
				float num9 = (num4 + num6) * 0.5f;
				WallSide wallSide = null;
				float num10 = 0.45f;
				foreach (WallSide item4 in list)
				{
					if (item4.Inward.X * num7 + item4.Inward.Y * outY > -0.6f)
					{
						continue;
					}
					float num11 = (num8 - item4.Start.X) * item4.Along.X + (num9 - item4.Start.Y) * item4.Along.Y;
					if (!(num11 < -0.1f) && !(num11 > item4.Length + 0.1f))
					{
						float num12 = Math.Abs((num8 - item4.Start.X) * item4.Inward.X + (num9 - item4.Start.Y) * item4.Inward.Y);
						if (num12 < num10)
						{
							num10 = num12;
							wallSide = item4;
						}
					}
				}
				if (wallSide == null)
				{
					continue;
				}
				float num13 = (num3 - wallSide.Start.X) * wallSide.Along.X + (num4 - wallSide.Start.Y) * wallSide.Along.Y;
				float num14 = (num5 - wallSide.Start.X) * wallSide.Along.X + (num6 - wallSide.Start.Y) * wallSide.Along.Y;
				if (num13 > num14)
				{
					float num15 = num13;
					num13 = num14;
					num14 = num15;
				}
				num13 = Math.Max(0.04f, num13);
				num14 = Math.Min(wallSide.Length - 0.04f, num14);
				if (num14 - num13 < 0.12f)
				{
					continue;
				}
				float z2 = (item.Z0 - storey2.Floor) * fit.Scale;
				float z3 = (item.Z1 - storey2.Floor) * fit.Scale;
				if (item2)
				{
					z2 = 0f;
				}
				bool flap = item2 && fit.Tent;
				bool flag = false;
				foreach (Hole hole in wallSide.Holes)
				{
					if (num14 > hole.A0 - 0.05f && num13 < hole.A1 + 0.05f)
					{
						flag = true;
					}
				}
				if (!flag)
				{
					wallSide.Holes.Add(new Hole
					{
						A0 = num13,
						A1 = num14,
						Z0 = z2,
						Z1 = z3,
						Door = item2,
						Flap = flap
					});
					if (item2)
					{
						Vec3 vec2 = wallSide.At((num13 + num14) * 0.5f, 0f);
						fit.DoorWallX = vec2.X;
						fit.DoorWallY = vec2.Y;
						fit.DoorHalf = (num14 - num13) * 0.5f;
					}
				}
			}
			return list;
		}

		private static void WallUp(Shell sh, WallSide w, Func<float, float> topAt)
		{
			List<float> list = new List<float> { 0f, w.Length };
			foreach (Hole hole2 in w.Holes)
			{
				list.Add(hole2.A0);
				list.Add(hole2.A1);
			}
			for (float num = 0.3f; num < w.Length; num += 0.3f)
			{
				list.Add(num);
			}
			list.Sort();
			for (int i = 0; i + 1 < list.Count; i++)
			{
				float num2 = list[i];
				float num3 = list[i + 1];
				if (num3 - num2 < 0.0001f)
				{
					continue;
				}
				float num4 = topAt(num2);
				float num5 = topAt(num3);
				float num6 = (num2 + num3) * 0.5f;
				Hole? hole = null;
				foreach (Hole hole3 in w.Holes)
				{
					if (num6 > hole3.A0 && num6 < hole3.A1)
					{
						hole = hole3;
					}
				}
				if (!hole.HasValue)
				{
					Up(sh, w, num2, num3, 0f, 0f, num4, num5);
					continue;
				}
				Hole value = hole.Value;
				if (value.Z0 > 0.001f)
				{
					Up(sh, w, num2, num3, 0f, 0f, value.Z0, value.Z0);
				}
				if (num4 > value.Z1 || num5 > value.Z1)
				{
					Up(sh, w, num2, num3, value.Z1, value.Z1, Math.Max(num4, value.Z1), Math.Max(num5, value.Z1));
				}
			}
		}

		private static void Up(Shell sh, WallSide w, float a0, float a1, float b0, float b1, float t0, float t1)
		{
			if (!(t0 - b0 < 0.0001f) || !(t1 - b1 < 0.0001f))
			{
				sh.B.Face(sh.Wall, w.At(a0, b0), w.At(a1, b1), w.At(a1, t1), w.At(a0, t0));
			}
		}

		private static void FloorOf(Shell sh, FitInfo fit, int storey, Style st)
		{
			if (storey == 0 && !st.MeshFloor)
			{
				return;
			}
			float z = fit.StoreyZ[storey];
			float zu = z - 0.12f + 0.01f;
			ForCells(fit, storey, delegate(float cx0, float cy0, float cx1, float cy1)
			{
				sh.B.Face(sh.Floor, new Vec3(cx0, cy0, z), new Vec3(cx1, cy0, z), new Vec3(cx1, cy1, z), new Vec3(cx0, cy1, z));
				if (storey > 0)
				{
					sh.B.Face(sh.Ceiling, new Vec3(cx0, cy0, zu), new Vec3(cx0, cy1, zu), new Vec3(cx1, cy1, zu), new Vec3(cx1, cy0, zu));
				}
			}, (storey > 0) ? fit.Stairwell : null);
			if (storey != 0 && fit.Stairwell != null)
			{
				float[] stairwell = fit.Stairwell;
				float z2 = z - 0.12f;
				Vec3 toward = new Vec3((stairwell[0] + stairwell[2]) * 0.5f, (stairwell[1] + stairwell[3]) * 0.5f, z - 0.06f);
				FaceToward(sh.B, sh.Timber, toward, new Vec3(stairwell[0], stairwell[1], z2), new Vec3(stairwell[0], stairwell[1], z), new Vec3(stairwell[0], stairwell[3], z), new Vec3(stairwell[0], stairwell[3], z2));
				FaceToward(sh.B, sh.Timber, toward, new Vec3(stairwell[2], stairwell[3], z2), new Vec3(stairwell[2], stairwell[3], z), new Vec3(stairwell[2], stairwell[1], z), new Vec3(stairwell[2], stairwell[1], z2));
				FaceToward(sh.B, sh.Timber, toward, new Vec3(stairwell[2], stairwell[1], z2), new Vec3(stairwell[2], stairwell[1], z), new Vec3(stairwell[0], stairwell[1], z), new Vec3(stairwell[0], stairwell[1], z2));
				FaceToward(sh.B, sh.Timber, toward, new Vec3(stairwell[0], stairwell[3], z2), new Vec3(stairwell[0], stairwell[3], z), new Vec3(stairwell[2], stairwell[3], z), new Vec3(stairwell[2], stairwell[3], z2));
			}
		}

		private static bool InWell(FitInfo fit, float x, float y)
		{
			float[] stairwell = fit.Stairwell;
			if (stairwell != null && x > stairwell[0] && x < stairwell[2] && y > stairwell[1])
			{
				return y < stairwell[3];
			}
			return false;
		}

		private static void ForCells(FitInfo fit, int storey, Action<float, float, float, float> cell)
		{
			ForCells(fit, storey, cell, null);
		}

		private static void ForCells(FitInfo fit, int storey, Action<float, float, float, float> cell, float[] hole)
		{
			if (hole != null)
			{
				Action<float, float, float, float> whole = cell;
				cell = delegate(float x0, float y0, float x1, float y1)
				{
					if (x1 <= hole[0] || x0 >= hole[2] || y1 <= hole[1] || y0 >= hole[3])
					{
						whole(x0, y0, x1, y1);
					}
					else
					{
						float arg = Math.Max(y0, hole[1]);
						float arg2 = Math.Min(y1, hole[3]);
						if (hole[1] > y0)
						{
							whole(x0, y0, x1, hole[1]);
						}
						if (hole[3] < y1)
						{
							whole(x0, hole[3], x1, y1);
						}
						if (hole[0] > x0)
						{
							whole(x0, arg, hole[0], arg2);
						}
						if (hole[2] < x1)
						{
							whole(hole[2], arg, x1, arg2);
						}
					}
				};
			}
			float num = float.MaxValue;
			float num2 = float.MaxValue;
			float num3 = float.MinValue;
			float num4 = float.MinValue;
			foreach (List<HousePlan.P2> loop in fit.Plan.Storeys[storey].Loops)
			{
				foreach (HousePlan.P2 item in loop)
				{
					float val = fit.RoomX(item.X);
					float val2 = fit.RoomY(item.Y);
					num = Math.Min(num, val);
					num3 = Math.Max(num3, val);
					num2 = Math.Min(num2, val2);
					num4 = Math.Max(num4, val2);
				}
			}
			for (float num5 = num2; num5 < num4; num5 += 0.25f)
			{
				for (float num6 = num; num6 < num3; num6 += 0.25f)
				{
					float num7 = Math.Min(num3, num6 + 0.25f);
					float num8 = Math.Min(num4, num5 + 0.25f);
					if (fit.Inside(storey, (num6 + num7) * 0.5f, (num5 + num8) * 0.5f) || fit.Inside(storey, num6 + 0.02f, num5 + 0.02f) || fit.Inside(storey, num7 - 0.02f, num5 + 0.02f) || fit.Inside(storey, num6 + 0.02f, num8 - 0.02f) || fit.Inside(storey, num7 - 0.02f, num8 - 0.02f))
					{
						cell(num6, num5, num7, num8);
					}
				}
			}
		}

		private static void CeilingOf(Shell sh, FitInfo fit, int storey, Style st, string race)
		{
			ForCells(fit, storey, delegate(float cx0, float cy0, float cx1, float cy1)
			{
				sh.B.SmoothQuad(sh.Ceiling, Corner(cx0, cy0), Corner(cx0, cy1), Corner(cx1, cy1), Corner(cx1, cy0), Normal(cx0, cy0), Normal(cx0, cy1), Normal(cx1, cy1), Normal(cx1, cy0));
			}, (storey + 1 < fit.StoreyZ.Length) ? fit.Stairwell : null);
			FitBeams(sh, fit, storey, race);
			Vec3 Corner(float x, float y)
			{
				return new Vec3(x, y, fit.CeilingAt(storey, x, y));
			}
			Vec3 Normal(float x, float y)
			{
				float num = 0.12f;
				float x2 = (fit.CeilingAt(storey, x + num, y) - fit.CeilingAt(storey, x - num, y)) / (2f * num);
				float y2 = (fit.CeilingAt(storey, x, y + num) - fit.CeilingAt(storey, x, y - num)) / (2f * num);
				return new Vec3(x2, y2, -1f);
			}
		}

		private static void FitFrame(Shell sh, WallSide w, int storey, FitInfo fit, string race)
		{
			if (fit.Tent)
			{
				return;
			}
			float z = fit.StoreyZ[storey];
			switch (race)
			{
			case "dwarf":
				if (w.Length > 0.6f)
				{
					if (!ByStair(fit, storey, w.At(0.1f, 0f)))
					{
						WallBox(sh, w, sh.Stone, 0.1f, TopAt(0.1f) * 0.5f, 0.1f, TopAt(0.1f) * 0.5f, 0.04f);
					}
					if (!ByStair(fit, storey, w.At(w.Length - 0.1f, 0f)))
					{
						WallBox(sh, w, sh.Stone, w.Length - 0.1f, TopAt(w.Length - 0.1f) * 0.5f, 0.1f, TopAt(w.Length - 0.1f) * 0.5f, 0.04f);
					}
				}
				Skirting(sh, w, sh.Stone, 0.07f, 0.03f);
				return;
			case "orc":
			{
				int num = Math.Max(1, (int)Math.Round(w.Length / 0.9f));
				for (int i = 0; i <= num; i++)
				{
					float a = 0.05f + (w.Length - 0.1f) * (float)i / (float)num;
					if (!InHole(w, a, 0.08f) && !ByStair(fit, storey, w.At(a, 0f)))
					{
						Vec3 vec = w.At(a, 0f) + w.Inward * 0.05f;
						sh.B.Tube(sh.Bark, vec, vec + new Vec3(0f, 0f, TopAt(a) - 0.02f), 0.045f, 0.038f, 6);
					}
				}
				return;
			}
			case "elf":
			{
				int num2 = Math.Max(1, (int)Math.Round(w.Length / 1.3f));
				for (int j = 0; j <= num2; j++)
				{
					float a2 = 0.06f + (w.Length - 0.12f) * (float)j / (float)num2;
					if (!InHole(w, a2, 0.1f) && !ByStair(fit, storey, w.At(a2, 0f)))
					{
						float num3 = TopAt(a2);
						WallBox(sh, w, sh.Timber, a2, num3 * 0.5f, 0.035f, num3 * 0.5f, 0.03f);
					}
				}
				Vec3 a3 = w.At(0.1f, TopAt(0.1f) - 0.08f) + w.Inward * 0.05f;
				bool flag = false;
				for (float num4 = 0.3f; num4 < w.Length - 0.05f; num4 += 0.22f)
				{
					float num5 = 0.035f * (float)Math.Sin(num4 * 4.1f);
					Vec3 vec2 = w.At(num4, TopAt(num4) - 0.08f - num5) + w.Inward * 0.05f;
					bool num6 = TopAt(num4) - 0.08f - num5 - 0.09f > 1.12f && !ByStair(fit, storey, w.At(num4, 0f));
					if (num6 && flag)
					{
						sh.B.Tube(sh.Bark, a3, vec2, 0.012f, 0.012f, 4);
					}
					if (num6)
					{
						sh.B.Blob(sh.Leaf, vec2 + new Vec3(0f, 0f, -0.02f), 0.065f, 0.055f, 0.05f, 6);
					}
					flag = num6;
					a3 = vec2;
				}
				return;
			}
			}
			int num7 = Math.Max(1, (int)Math.Round(w.Length / 1.5f));
			for (int k = 0; k <= num7; k++)
			{
				float a4 = ((k == 0) ? 0.06f : ((k == num7) ? (w.Length - 0.06f) : (w.Length * (float)k / (float)num7)));
				if (!(w.Length < 0.3f) && !InHole(w, a4, 0.12f) && !ByStair(fit, storey, w.At(a4, 0f)))
				{
					float num8 = TopAt(a4);
					WallBox(sh, w, sh.Timber, a4, num8 * 0.5f, 0.05f, num8 * 0.5f, 0.025f);
				}
			}
			float num9 = TopAt(0.05f);
			float num10 = TopAt(w.Length - 0.05f);
			if (Math.Abs(num9 - num10) < 0.05f && w.Length > 0.3f)
			{
				WallBox(sh, w, sh.Timber, w.Length * 0.5f, Math.Min(num9, num10) - 0.05f, w.Length * 0.5f, 0.05f, 0.035f);
			}
			Skirting(sh, w, sh.Timber, 0.04f, 0.015f);
			float TopAt(float num11)
			{
				return fit.CeilingAt(storey, w.Start.X + w.Along.X * num11 + w.Inward.X * 0.1f, w.Start.Y + w.Along.Y * num11 + w.Inward.Y * 0.1f) - z;
			}
		}

		private static void Skirting(Shell sh, WallSide w, int slot, float half, float outward)
		{
			float num = 0f;
			foreach (Hole hole in w.Holes)
			{
				if (hole.Door)
				{
					if (hole.A0 - num > 0.01f)
					{
						WallBox(sh, w, slot, (num + hole.A0) * 0.5f, half, (hole.A0 - num) * 0.5f, half, outward);
					}
					num = hole.A1;
				}
			}
			if (w.Length - num > 0.01f)
			{
				WallBox(sh, w, slot, (num + w.Length) * 0.5f, half, (w.Length - num) * 0.5f, half, outward);
			}
		}

		private static void FitBeams(Shell sh, FitInfo fit, int storey, string race)
		{
			HousePlan.Storey storey2 = fit.Plan.Storeys[storey];
			float num = float.MaxValue;
			float num2 = float.MaxValue;
			float num3 = float.MinValue;
			float num4 = float.MinValue;
			foreach (List<HousePlan.P2> loop in storey2.Loops)
			{
				foreach (HousePlan.P2 item in loop)
				{
					float val = fit.RoomX(item.X);
					float val2 = fit.RoomY(item.Y);
					num = Math.Min(num, val);
					num3 = Math.Max(num3, val);
					num2 = Math.Min(num2, val2);
					num4 = Math.Max(num4, val2);
				}
			}
			float num5 = 0f;
			float num6 = 0f;
			float num7 = float.MaxValue;
			float num8 = float.MinValue;
			float x = 0f;
			float y = 0f;
			int num9 = 0;
			bool flag = storey + 1 < fit.StoreyZ.Length;
			for (float num10 = num2 + 0.15f; num10 < num4; num10 += 0.3f)
			{
				for (float num11 = num + 0.15f; num11 < num3; num11 += 0.3f)
				{
					if (fit.Inside(storey, num11, num10) && (!flag || (!InWell(fit, num11 - 0.12f, num10 - 0.12f) && !InWell(fit, num11 + 0.12f, num10 - 0.12f) && !InWell(fit, num11 - 0.12f, num10 + 0.12f) && !InWell(fit, num11 + 0.12f, num10 + 0.12f))))
					{
						float num12 = fit.CeilingAt(storey, num11, num10);
						num5 += Math.Abs(fit.CeilingAt(storey, num11 + 0.1f, num10) - fit.CeilingAt(storey, num11 - 0.1f, num10));
						num6 += Math.Abs(fit.CeilingAt(storey, num11, num10 + 0.1f) - fit.CeilingAt(storey, num11, num10 - 0.1f));
						if (num12 > num8)
						{
							num8 = num12;
							x = num11;
							y = num10;
						}
						num7 = Math.Min(num7, num12);
						num9++;
					}
				}
			}
			if (num9 == 0)
			{
				return;
			}
			bool flag2 = race == "orc" || (num5 > 0.5f * num6 && num6 > 0.5f * num5);
			if (fit.Tent)
			{
				if (flag2)
				{
					sh.B.Tube(sh.Bark, new Vec3(x, y, fit.StoreyZ[storey]), new Vec3(x, y, num8), 0.04f, 0.035f, 6);
					return;
				}
				bool flag3 = num5 > num6;
				Follow(fit, storey, flag3 ? new Vec3(x, num2, 0f) : new Vec3(num, y, 0f), flag3 ? new Vec3(x, num4, 0f) : new Vec3(num3, y, 0f), 0.05f, delegate(Vec3 a3, Vec3 b2)
				{
					sh.B.Tube(sh.Bark, a3, b2, 0.035f, 0.035f, 5);
				});
				return;
			}
			int slot = ((race == "dwarf" || race == "human") ? sh.Timber : sh.Bark);
			if (num8 - num7 < 0.15f)
			{
				bool flag4 = num3 - num < num4 - num2;
				if (num7 - fit.StoreyZ[storey] < 1.3f)
				{
					float num13 = (flag4 ? num2 : num);
					float num14 = (flag4 ? num4 : num3);
					for (float num15 = num13 + 0.22f; num15 < num14 - 0.1f; num15 += 0.42f)
					{
						Line(fit, storey, flag4 ? new Vec3(num, num15, 0f) : new Vec3(num15, num2, 0f), flag4 ? new Vec3(num3, num15, 0f) : new Vec3(num15, num4, 0f), 0.022f, delegate(Vec3 vec4, Vec3 vec3)
						{
							Vec3 vec2 = vec3 - vec4;
							Vec3 c = (vec4 + vec3) * 0.5f;
							float num27 = (float)Math.Sqrt(vec2.X * vec2.X + vec2.Y * vec2.Y);
							if (!(num27 < 0.05f))
							{
								Vec3 vec5 = new Vec3(vec2.X / num27, vec2.Y / num27, 0f);
								Vec3 vec6 = new Vec3(0f - vec5.Y, vec5.X, 0f);
								sh.B.Box(slot, c, vec6 * 0.035f, vec5 * (num27 * 0.5f), new Vec3(0f, 0f, 0.012f));
							}
						}, level: false, lowOk: true);
					}
					return;
				}
				float num16 = (flag4 ? num2 : num);
				float num17 = (flag4 ? num4 : num3);
				for (float num18 = num16 + 0.3f; num18 < num17 - 0.1f; num18 += 0.55f)
				{
					Line(fit, storey, flag4 ? new Vec3(num, num18, 0f) : new Vec3(num18, num2, 0f), flag4 ? new Vec3(num3, num18, 0f) : new Vec3(num18, num4, 0f), 0.07f, delegate(Vec3 a3, Vec3 c)
					{
						Beam(sh.B, slot, a3, c, 0.05f);
					});
				}
				return;
			}
			if (flag2)
			{
				List<Vec3> list = new List<Vec3>();
				using (List<List<HousePlan.P2>>.Enumerator enumerator = storey2.Loops.GetEnumerator())
				{
					if (enumerator.MoveNext())
					{
						List<HousePlan.P2> current2 = enumerator.Current;
						float num19 = 0f;
						for (int num20 = 0; num20 < current2.Count; num20++)
						{
							HousePlan.P2 p = current2[num20];
							HousePlan.P2 p2 = current2[(num20 + 1) % current2.Count];
							float num21 = (float)Math.Sqrt((p2.X - p.X) * (p2.X - p.X) + (p2.Y - p.Y) * (p2.Y - p.Y)) * fit.Scale;
							for (float num22 = ((num19 > 0f) ? (0.7f - num19) : 0f); num22 < num21; num22 += 0.7f)
							{
								list.Add(new Vec3(fit.RoomX(p.X + (p2.X - p.X) * num22 / num21 / fit.Scale), fit.RoomY(p.Y + (p2.Y - p.Y) * num22 / num21 / fit.Scale), 0f));
							}
							num19 = (num19 + num21) % 0.7f;
						}
					}
				}
				Vec3 vec = new Vec3(x, y, 0f);
				{
					foreach (Vec3 item2 in list)
					{
						Vec3 a = item2 + (vec - item2) * Math.Min(0.4f, 0.12f / Math.Max(0.01f, Dist(vec, item2)));
						Follow(fit, storey, a, vec, 0.06f, delegate(Vec3 a3, Vec3 b2)
						{
							sh.B.Tube(slot, a3, b2, 0.035f, 0.035f, 5);
						});
					}
					return;
				}
			}
			bool flag5 = num5 > num6;
			float num23 = (flag5 ? num2 : num);
			float num24 = (flag5 ? num4 : num3);
			for (float num25 = num23 + 0.2f; num25 < num24 - 0.1f; num25 += 0.5f)
			{
				Follow(fit, storey, flag5 ? new Vec3(num, num25, 0f) : new Vec3(num25, num2, 0f), flag5 ? new Vec3(num3, num25, 0f) : new Vec3(num25, num4, 0f), 0.05f, delegate(Vec3 a3, Vec3 c)
				{
					Beam(sh.B, slot, a3, c, 0.035f);
				});
			}
			Vec3 a2 = (flag5 ? new Vec3(x, num2, 0f) : new Vec3(num, y, 0f));
			Vec3 b = (flag5 ? new Vec3(x, num4, 0f) : new Vec3(num3, y, 0f));
			Follow(fit, storey, a2, b, 0.08f, delegate(Vec3 a3, Vec3 c)
			{
				Beam(sh.B, slot, a3, c, 0.055f);
			});
			float eave = num7 + 0.02f;
			if (!(eave - fit.StoreyZ[storey] > 1.25f))
			{
				return;
			}
			for (float num26 = num23 + 0.9f; num26 < num24 - 0.5f; num26 += 1.8f)
			{
				Line(fit, storey, flag5 ? new Vec3(num, num26, 0f) : new Vec3(num26, num2, 0f), flag5 ? new Vec3(num3, num26, 0f) : new Vec3(num26, num4, 0f), 0f, delegate(Vec3 vec2, Vec3 vec3)
				{
					Beam(sh.B, slot, new Vec3(vec2.X, vec2.Y, eave - 0.06f), new Vec3(vec3.X, vec3.Y, eave - 0.06f), 0.055f);
				}, level: true);
			}
		}

		private static float Dist(Vec3 a, Vec3 b)
		{
			return (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
		}

		private static bool UnderLowFloor(FitInfo fit, int storey, float x, float y)
		{
			if (storey + 1 < fit.StoreyZ.Length && fit.Inside(storey + 1, x, y))
			{
				return fit.StoreyZ[storey + 1] - fit.StoreyZ[storey] < 1.42f;
			}
			return false;
		}

		private static void Line(FitInfo fit, int storey, Vec3 a, Vec3 b, float under, Action<Vec3, Vec3> beam, bool level = false, bool lowOk = false)
		{
			float num = Dist(a, b);
			int num2 = Math.Max(2, (int)(num / 0.05f));
			int num3 = -1;
			for (int i = 0; i <= num2; i++)
			{
				float num4 = (float)i / (float)num2;
				float num5 = a.X + (b.X - a.X) * num4;
				float num6 = a.Y + (b.Y - a.Y) * num4;
				int num7;
				if (i < num2 && fit.Inside(storey, num5, num6) && (storey + 1 >= fit.StoreyZ.Length || !InWell(fit, num5, num6)) && (lowOk || !UnderLowFloor(fit, storey, num5, num6)))
				{
					num7 = ((!ByStair(fit, storey, new Vec3(num5, num6, 0f))) ? 1 : 0);
					if (num7 != 0 && num3 < 0)
					{
						num3 = i;
					}
				}
				else
				{
					num7 = 0;
				}
				if (num7 != 0 || num3 < 0)
				{
					continue;
				}
				float num8 = (float)num3 / (float)num2;
				float num9 = (float)(i - 1) / (float)num2;
				Vec3 vec = new Vec3(a.X + (b.X - a.X) * num8, a.Y + (b.Y - a.Y) * num8, 0f);
				Vec3 vec2 = new Vec3(a.X + (b.X - a.X) * num9, a.Y + (b.Y - a.Y) * num9, 0f);
				if (Dist(vec, vec2) > 0.2f)
				{
					if (!level)
					{
						vec2.Z = (vec.Z = Math.Min(fit.CeilingAt(storey, (vec.X + vec2.X) * 0.5f, (vec.Y + vec2.Y) * 0.5f), Math.Min(fit.CeilingAt(storey, vec.X, vec.Y), fit.CeilingAt(storey, vec2.X, vec2.Y))) - under);
					}
					beam(vec, vec2);
				}
				num3 = -1;
			}
		}

		private static void Follow(FitInfo fit, int storey, Vec3 a, Vec3 b, float under, Action<Vec3, Vec3> beam)
		{
			float num = Dist(a, b);
			int num2 = Math.Max(2, (int)(num / 0.25f));
			Vec3 arg = default(Vec3);
			bool flag = false;
			for (int i = 0; i <= num2; i++)
			{
				float num3 = (float)i / (float)num2;
				float num4 = a.X + (b.X - a.X) * num3;
				float num5 = a.Y + (b.Y - a.Y) * num3;
				if (!fit.Inside(storey, num4, num5) || UnderLowFloor(fit, storey, num4, num5) || ByStair(fit, storey, new Vec3(num4, num5, 0f)))
				{
					flag = false;
					continue;
				}
				Vec3 vec = new Vec3(num4, num5, fit.CeilingAt(storey, num4, num5) - under);
				if (flag)
				{
					beam(arg, vec);
				}
				arg = vec;
				flag = true;
			}
		}

		private static void PlanStair(FitInfo fit, float headRoom)
		{
			float num = fit.StoreyZ[1];
			List<WallSide> list = Walls(fit, 0);
			float num2 = fit.RoomX(fit.Plan.Door.X);
			float num3 = fit.RoomY(fit.Plan.Door.Y);
			float num4 = float.MaxValue;
			float num5 = float.MaxValue;
			float num6 = float.MinValue;
			float num7 = float.MinValue;
			foreach (WallSide item5 in list)
			{
				Vec3 vec = item5.At(item5.Length, 0f);
				num4 = Math.Min(num4, Math.Min(item5.Start.X, vec.X));
				num6 = Math.Max(num6, Math.Max(item5.Start.X, vec.X));
				num5 = Math.Min(num5, Math.Min(item5.Start.Y, vec.Y));
				num7 = Math.Max(num7, Math.Max(item5.Start.Y, vec.Y));
			}
			FloorGrid floorGrid = new FloorGrid(fit, 0, num4, num5, num6, num7);
			FloorGrid g = new FloorGrid(fit, 1, num4, num5, num6, num7);
			FloorsKept floorsKept = new FloorsKept(floorGrid, g, num4, num5, num6, num7, num2, num3);
			List<(float, Vec3, Vec3, bool, bool)> list2 = new List<(float, Vec3, Vec3, bool, bool)>();
			(float, float, float)[] array = new(float, float, float)[3]
			{
				(0.19f, 0.26f, 0f),
				(0.23f, 0.16f, 3f),
				(0.24f, 0.1f, 6f)
			};
			for (int i = 0; i < array.Length; i++)
			{
				(float, float, float) tuple = array[i];
				float item = tuple.Item1;
				float item2 = tuple.Item2;
				float item3 = tuple.Item3;
				int num8 = (int)Math.Ceiling(num / item);
				float num9 = (float)num8 * item2;
				float num10 = ((item2 < 0.12f) ? 0.45f : 0.6f);
				list2.Clear();
				for (int j = 0; j < 8; j += 2)
				{
					Vec3 vec2 = new Vec3((float)Math.Cos((float)j * MathF.PI / 4f), (float)Math.Sin((float)j * MathF.PI / 4f), 0f);
					Vec3 side = new Vec3(0f - vec2.Y, vec2.X, 0f);
					for (float num11 = num5 + 0.1f; num11 < num7; num11 += 0.1f)
					{
						for (float num12 = num4 + 0.1f; num12 < num6; num12 += 0.1f)
						{
							Vec3 vec3 = new Vec3(num12, num11, 0f);
							if (!Flight(floorGrid, g, vec3, vec2, side, num9, num10) || !Headroom(fit, vec3, vec2, side, num9, num10, headRoom))
							{
								continue;
							}
							float num13 = float.MaxValue;
							for (float num14 = -0.45f; num14 <= num9; num14 += 0.15f)
							{
								Vec3 vec4 = vec3 + vec2 * num14;
								num13 = Math.Min(num13, (float)Math.Sqrt((vec4.X - num2) * (vec4.X - num2) + (vec4.Y - num3) * (vec4.Y - num3)));
							}
							if (num13 < 1.1f)
							{
								continue;
							}
							float num15 = Hug(floorGrid, vec3, vec2, side, num9, num10, 1f);
							float num16 = Hug(floorGrid, vec3, vec2, side, num9, num10, -1f);
							bool flag = num15 < 0.08f;
							bool flag2 = num16 < 0.08f;
							float item4 = item3 + ((flag || flag2) ? 0f : 2.5f) + 2f / (0.5f + num13);
							bool flag3 = false;
							foreach (WallSide item6 in list)
							{
								foreach (Hole hole in item6.Holes)
								{
									if (hole.Door)
									{
										continue;
									}
									for (int k = 0; k < 3; k++)
									{
										float num17;
										switch (k)
										{
										default:
											num17 = hole.A1;
											break;
										case 1:
											num17 = (hole.A0 + hole.A1) * 0.5f;
											break;
										case 0:
											num17 = hole.A0;
											break;
										}
										float a = num17;
										Vec3 vec5 = item6.At(a, 0f);
										float num18 = (vec5.X - vec3.X) * vec2.X + (vec5.Y - vec3.Y) * vec2.Y;
										float num19 = Math.Abs((vec5.X - vec3.X) * side.X + (vec5.Y - vec3.Y) * side.Y);
										if (num18 > -0.25f && num18 < num9 + 0.25f && num19 < num10 * 0.5f + 0.3f)
										{
											flag3 = true;
										}
									}
								}
							}
							if (!flag3)
							{
								list2.Add((item4, vec3, vec2, flag, flag2));
							}
						}
					}
				}
				list2.Sort(((float Score, Vec3 Foot, Vec3 D, bool A, bool B) p, (float Score, Vec3 Foot, Vec3 D, bool A, bool B) q) => p.Score.CompareTo(q.Score));
				int num20 = 0;
				foreach (var item7 in list2)
				{
					if (++num20 > 80)
					{
						break;
					}
					Vec3 hd = item7.Item2 + item7.Item3 * num9;
					float[] array2 = WellOf(num, item7.Item2, hd, num10);
					if (floorsKept.Whole(item7.Item2, item7.Item3, num9, num10, array2))
					{
						fit.StairFootX = item7.Item2.X;
						fit.StairFootY = item7.Item2.Y;
						fit.StairHeadX = hd.X;
						fit.StairHeadY = hd.Y;
						fit.StairWidth = num10;
						fit.StairRun = item2;
						fit.StairSteps = num8;
						Vec3 vec6 = new Vec3(0f - item7.Item3.Y, item7.Item3.X, 0f);
						Vec3 vec7 = (item7.Item4 ? (vec6 * -1f) : vec6);
						fit.StairOpenX = vec7.X;
						fit.StairOpenY = vec7.Y;
						fit.StairFree = !item7.Item4 && !item7.Item5;
						fit.Stairwell = array2;
						fit.StairWellBack = WellBack(num, num9);
						break;
					}
				}
				if (!float.IsNaN(fit.StairFootX))
				{
					break;
				}
			}
		}

		private static float WellFrom(float rise, float total)
		{
			float num = rise - 0.12f - 0.12f - 1.12f;
			return Math.Max(-0.2f, num / rise * total - 0.1f);
		}

		private static float WellBack(float rise, float total)
		{
			return Math.Max(0f, 0f - WellFrom(rise, total));
		}

		private static float[] WellOf(float rise, Vec3 f, Vec3 hd, float width)
		{
			float num = Dist(f, hd);
			Vec3 vec = (hd - f) * (1f / num);
			float num2 = WellFrom(rise, num);
			Vec3 vec2 = f + vec * num2;
			Vec3 vec3 = new Vec3(0f - vec.Y, vec.X, 0f);
			float num3 = width * 0.5f + 0.06f;
			float[] array = new float[4]
			{
				float.MaxValue,
				float.MaxValue,
				float.MinValue,
				float.MinValue
			};
			Vec3[] array2 = new Vec3[4]
			{
				vec2 + vec3 * num3,
				vec2 - vec3 * num3,
				hd + vec3 * num3,
				hd - vec3 * num3
			};
			for (int i = 0; i < array2.Length; i++)
			{
				Vec3 vec4 = array2[i];
				array[0] = Math.Min(array[0], vec4.X);
				array[1] = Math.Min(array[1], vec4.Y);
				array[2] = Math.Max(array[2], vec4.X);
				array[3] = Math.Max(array[3], vec4.Y);
			}
			return array;
		}

		private static bool Flight(FloorGrid g0, FloorGrid g1, Vec3 foot, Vec3 d, Vec3 side, float length, float width)
		{
			Vec3 vec = foot - d * 0.35f;
			Vec3 vec2 = foot + d * (length + 0.35f);
			Vec3 vec3 = foot - d * 0.8f;
			if (!g0.Clear(vec.X, vec.Y, 0.22f) || !g0.Clear(vec3.X, vec3.Y, 0.25f) || !g1.Clear(vec2.X, vec2.Y, 0.22f))
			{
				return false;
			}
			for (float num = length * 0.55f; num <= length; num += 0.12f)
			{
				for (float num2 = -0.5f; num2 <= 0.5f; num2 += 0.5f)
				{
					Vec3 vec4 = foot + d * num + side * (num2 * width);
					if (!g1.Inside(vec4.X, vec4.Y))
					{
						return false;
					}
				}
			}
			for (float num3 = -0.45f; num3 <= length; num3 += 0.12f)
			{
				for (float num4 = -0.5f; num4 <= 0.5f; num4 += 0.25f)
				{
					Vec3 vec5 = foot + d * num3 + side * (num4 * width);
					if (!g0.Inside(vec5.X, vec5.Y))
					{
						return false;
					}
				}
			}
			return true;
		}

		private static bool Headroom(FitInfo fit, Vec3 foot, Vec3 d, Vec3 side, float length, float width, float head)
		{
			float num = fit.StoreyZ[1];
			Vec3 vec = foot - d * 0.35f;
			Vec3 vec2 = foot + d * (length + 0.35f);
			Vec3 vec3 = foot - d * 0.8f;
			if (fit.CeilingAt(0, vec.X, vec.Y) < head || fit.CeilingAt(0, vec3.X, vec3.Y) < head)
			{
				return false;
			}
			if (fit.CeilingAt(1, vec2.X, vec2.Y) - num < head)
			{
				return false;
			}
			float num2 = WellFrom(num, length);
			for (float num3 = 0f; num3 < Math.Min(length, num2 + 0.1f); num3 += 0.1f)
			{
				for (float num4 = -0.4f; num4 <= 0.4f; num4 += 0.4f)
				{
					Vec3 vec4 = foot + d * num3 + side * (num4 * width);
					Vec3 vec5 = vec4 - d * 0.12f;
					float num5 = num * num3 / length;
					if (fit.CeilingAt(0, vec4.X, vec4.Y) - num5 < head || fit.CeilingAt(0, vec5.X, vec5.Y) - num5 < head)
					{
						return false;
					}
				}
			}
			for (float num6 = length * 0.55f; num6 <= length + 0.01f; num6 += 0.12f)
			{
				for (float num7 = -0.4f; num7 <= 0.4f; num7 += 0.4f)
				{
					Vec3 vec6 = foot + d * num6 + side * (num7 * width);
					if (fit.CeilingAt(1, vec6.X, vec6.Y) - num * Math.Min(1f, num6 / length) < head)
					{
						return false;
					}
				}
			}
			return true;
		}

		private static bool ByStair(FitInfo fit, int storey, Vec3 p)
		{
			if (float.IsNaN(fit.StairFootX) || storey > 1)
			{
				return false;
			}
			Vec3 vec = new Vec3(fit.StairFootX, fit.StairFootY, 0f);
			Vec3 vec2 = new Vec3(fit.StairHeadX, fit.StairHeadY, 0f);
			float val = Dist(vec, vec2);
			Vec3 vec3 = (vec2 - vec) * (1f / Math.Max(0.0001f, val));
			Vec3 vec4 = ((storey == 0) ? (vec - vec3 * 0.9f) : vec2);
			Vec3 b = ((storey == 0) ? vec2 : (vec2 + vec3 * 0.8f));
			float num = Dist(vec4, b);
			float num2 = ((num < 0.0001f) ? 0f : Math.Max(0f, Math.Min(num, (p.X - vec4.X) * vec3.X + (p.Y - vec4.Y) * vec3.Y)));
			Vec3 vec5 = vec4 + vec3 * num2;
			return Math.Sqrt((p.X - vec5.X) * (p.X - vec5.X) + (p.Y - vec5.Y) * (p.Y - vec5.Y)) < (double)(fit.StairWidth * 0.5f + 0.3f);
		}

		private static float Hug(FloorGrid g, Vec3 foot, Vec3 d, Vec3 side, float length, float width, float which)
		{
			float num = 0f;
			for (float num2 = 0f; num2 <= length; num2 += 0.2f)
			{
				Vec3 vec = foot + d * num2 + side * (which * width * 0.5f);
				float num3;
				for (num3 = 0f; num3 < 0.3f && g.Inside(vec.X + side.X * which * (num3 + 0.03f), vec.Y + side.Y * which * (num3 + 0.03f)); num3 += 0.03f)
				{
				}
				num = Math.Max(num, num3);
			}
			return num;
		}

		private static MeshInstance Stair(FitInfo fit, Style st, string race)
		{
			MeshBuilder meshBuilder = new MeshBuilder("fit_stair");
			int num = meshBuilder.Slot("wood", (byte)((race == "dwarf") ? 4 : 3), (race == "dwarf") ? st.Stone : st.Timber, 0);
			int num2 = meshBuilder.Slot("dark", 3, TerrainCache.Scale(st.Timber, 0.75f), 0);
			meshBuilder.Part("stair", -1, new Vec3(0f, 0f, 0f));
			Vec3 vec = new Vec3(fit.StairFootX, fit.StairFootY, 0f);
			Vec3 vec2 = new Vec3(fit.StairHeadX, fit.StairHeadY, 0f);
			float num3 = Dist(vec, vec2);
			float num4 = fit.StoreyZ[1];
			Vec3 vec3 = (vec2 - vec) * (1f / num3);
			Vec3 vec4 = new Vec3(0f - vec3.Y, vec3.X, 0f);
			int num5 = Math.Max(2, fit.StairSteps);
			float num6 = num3 / (float)num5;
			float num7 = fit.StairWidth * 0.5f;
			bool flag = fit.StairRun < 0.2f;
			float num8 = ((race == "dwarf") ? 0.045f : 0.025f);
			for (int i = 0; i < num5 - 1; i++)
			{
				float num9 = num4 * (float)(i + 1) / (float)num5;
				Vec3 vec5 = vec + vec3 * (num6 * ((float)i + 0.5f));
				meshBuilder.Box(num, new Vec3(vec5.X, vec5.Y, num9 - num8) + vec3 * 0.015f, vec3 * (num6 * 0.5f + 0.015f), vec4 * (num7 - 0.01f), new Vec3(0f, 0f, num8));
				if (!flag)
				{
					meshBuilder.Box(num2, new Vec3(vec5.X, vec5.Y, num9 - num4 / (float)num5 * 0.5f - num8) - vec3 * (num6 * 0.5f - 0.012f), vec3 * 0.012f, vec4 * (num7 - 0.02f), new Vec3(0f, 0f, Math.Max(0.01f, num4 / (float)num5 * 0.5f - num8)));
				}
			}
			float[] array = new float[2]
			{
				0f - num7 + 0.02f,
				num7 - 0.02f
			};
			foreach (float num10 in array)
			{
				Vec3 a = vec + vec4 * num10 + new Vec3(0f, 0f, 0.07f);
				Vec3 c = vec2 + vec4 * num10 + new Vec3(0f, 0f, num4 - 0.04f);
				Beam(meshBuilder, (race == "dwarf") ? num : num2, a, c, flag ? 0.035f : 0.045f);
			}
			array = ((!fit.StairFree) ? new float[1] { 1f } : new float[2] { 1f, -1f });
			foreach (float num11 in array)
			{
				Vec3 vec6 = new Vec3(fit.StairOpenX, fit.StairOpenY, 0f) * ((num7 + 0.03f) * num11);
				for (int k = 1; k < num5; k += 2)
				{
					Vec3 vec7 = vec + vec3 * (num6 * ((float)k + 0.5f)) + vec6;
					float num12 = num4 * (float)(k + 1) / (float)num5;
					Beam(meshBuilder, num2, new Vec3(vec7.X, vec7.Y, num12), new Vec3(vec7.X, vec7.Y, num12 + 0.55f), 0.025f);
				}
				Beam(meshBuilder, num2, vec + vec3 * (num6 * 1.5f) + vec6 + new Vec3(0f, 0f, num4 * 2f / (float)num5 + 0.55f), vec2 + vec6 + new Vec3(0f, 0f, num4 + 0.55f), 0.03f);
			}
			MeshModel meshModel = meshBuilder.Build();
			meshModel.ParseInfo(string.Format(CultureInfo.InvariantCulture, "walk=1;use=stair;ramp={0:F3},{1:F3},0,{2:F3},{3:F3},{4:F3},{5:F3},{6:F3},{7:F3}", vec.X - vec3.X * 0.04f, vec.Y - vec3.Y * 0.04f, vec2.X, vec2.Y, num4, fit.StairWidth + 0.12f, 12f, Math.Max(0f, fit.StairWellBack - 0.04f)));
			uint[] array2 = new uint[meshModel.SlotNames.Length];
			for (int l = 0; l < array2.Length; l++)
			{
				array2[l] = meshModel.SlotColor[l];
			}
			return new MeshInstance
			{
				Model = meshModel,
				Palette = array2,
				NoShadow = true,
				Static = true,
				Tag = -1
			};
		}

		private static void FurnishFitted(Interior room, FitInfo fit, Func<string, MeshModel> models, string race, int tier, uint roofColor, Random rng, string style = null)
		{
			if (models == null || models("int_barrel") == null || models("int_" + race + "_bed") == null)
			{
				return;
			}
			if (style != null && style.StartsWith("tent", StringComparison.Ordinal))
			{
				FurnishTent(room, fit, models, race, roofColor, rng);
				return;
			}
			Dictionary<string, uint> colors = FurnitureColors(race, roofColor);
			int num = fit.StoreyZ.Length;
			Layout[] array = new Layout[num];
			List<WallSide>[] array2 = new List<WallSide>[num];
			for (int i = 0; i < num; i++)
			{
				array2[i] = Walls(fit, i);
				float num2 = float.MaxValue;
				float num3 = float.MaxValue;
				float num4 = float.MinValue;
				float num5 = float.MinValue;
				float num6 = float.MaxValue;
				foreach (WallSide item3 in array2[i])
				{
					Vec3 vec = item3.At(item3.Length, 0f);
					num2 = Math.Min(num2, Math.Min(item3.Start.X, vec.X));
					num4 = Math.Max(num4, Math.Max(item3.Start.X, vec.X));
					num3 = Math.Min(num3, Math.Min(item3.Start.Y, vec.Y));
					num5 = Math.Max(num5, Math.Max(item3.Start.Y, vec.Y));
					num6 = Math.Min(num6, fit.CeilingAt(i, item3.Start.X + item3.Inward.X * 0.1f + item3.Along.X * item3.Length * 0.5f, item3.Start.Y + item3.Inward.Y * 0.1f + item3.Along.Y * item3.Length * 0.5f));
				}
				int storey = i;
				array[i] = new Layout(room, models, colors, num2, num3, num4, num5)
				{
					Base = fit.StoreyZ[i],
					Fitted = fit,
					Storey = i,
					WallTop = num6,
					CeilingAt = (float rx, float ry) => fit.CeilingAt(storey, rx, ry),
					Floor = new FloorGrid(fit, i, num2, num3, num4, num5)
				};
				foreach (HousePlan.P2 passage in fit.Plan.Storeys[i].Passages)
				{
					float num7 = fit.RoomX(passage.X);
					float num8 = fit.RoomY(passage.Y);
					array[i].Keep(num7 - 0.55f, num8 - 0.55f, num7 + 0.55f, num8 + 0.55f);
					array[i].Keep(num7 - 0.3f, num8 - 0.85f, num7 + 0.3f, num8 + 0.85f);
					array[i].Keep(num7 - 0.85f, num8 - 0.3f, num7 + 0.85f, num8 + 0.3f);
				}
			}
			Layout layout = array[0];
			float num9 = 0f - room.DoorOutX;
			float num10 = 0f - room.DoorOutY;
			float num11 = room.DoorX + num9 * 0.4f;
			float num12 = room.DoorY + num10 * 0.4f;
			layout.Keep(num11 - 0.55f, num12 - 0.55f, num11 + 0.55f, num12 + 0.55f);
			layout.Keep(room.DoorX - 0.5f, room.DoorY - 0.5f, room.DoorX + 0.5f, room.DoorY + 0.5f);
			layout.KeepDoorBar();
			if (!float.IsNaN(fit.StairFootX))
			{
				float stairFootX = fit.StairFootX;
				float stairFootY = fit.StairFootY;
				float stairHeadX = fit.StairHeadX;
				float stairHeadY = fit.StairHeadY;
				float num13 = (float)Math.Sqrt((stairHeadX - stairFootX) * (stairHeadX - stairFootX) + (stairHeadY - stairFootY) * (stairHeadY - stairFootY));
				float num14 = (stairHeadX - stairFootX) / num13;
				float num15 = (stairHeadY - stairFootY) / num13;
				float val = stairFootX - num14 * 0.7f;
				float val2 = stairFootY - num15 * 0.7f;
				float num16 = fit.StairWidth * 0.5f + 0.08f;
				layout.Keep(Math.Min(val, stairHeadX) - num16, Math.Min(val2, stairHeadY) - num16, Math.Max(val, stairHeadX) + num16, Math.Max(val2, stairHeadY) + num16);
				float val3 = stairFootX - num14 * 1.2f;
				float val4 = stairFootY - num15 * 1.2f;
				layout.Keep(Math.Min(val, val3) - 0.42f, Math.Min(val2, val4) - 0.42f, Math.Max(val, val3) + 0.42f, Math.Max(val2, val4) + 0.42f);
				MeshInstance meshInstance = ((room.Meshes.Count > 1) ? room.Meshes[room.Meshes.Count - 1] : null);
				if (meshInstance?.Model != null && meshInstance.Model.Info("walk") == "1")
				{
					room.Solids.Add(new float[4]
					{
						meshInstance.Model.Min.X + 0.03f,
						meshInstance.Model.Min.Y + 0.03f,
						meshInstance.Model.Max.X - 0.03f,
						meshInstance.Model.Max.Y - 0.03f
					});
				}
				if (fit.Stairwell != null && num > 1)
				{
					float[] stairwell = fit.Stairwell;
					array[1].Keep(stairwell[0] - 0.15f, stairwell[1] - 0.15f, stairwell[2] + 0.15f, stairwell[3] + 0.15f);
					float val5 = stairHeadX + num14 * 0.6f;
					float val6 = stairHeadY + num15 * 0.6f;
					array[1].Keep(Math.Min(stairHeadX, val5) - 0.45f, Math.Min(stairHeadY, val6) - 0.45f, Math.Max(stairHeadX, val5) + 0.45f, Math.Max(stairHeadY, val6) + 0.45f);
				}
			}
			Vec3 vec2 = (float.IsNaN(fit.Plan.ChimneyX) ? Furthest(array2[0], room.DoorX, room.DoorY) : new Vec3(fit.RoomX(fit.Plan.ChimneyX), fit.RoomY(fit.Plan.ChimneyY), 0f));
			float[] array3 = null;
			float heading = 0f;
			object obj;
			switch (race)
			{
			default:
				obj = ((tier >= 2 && models("int_p_fireplace") != null) ? "int_p_fireplace" : "int_human_hearth");
				break;
			case "orc":
				obj = "int_orc_firepit";
				break;
			case "dwarf":
				obj = "int_dwarf_forge";
				break;
			case "elf":
				obj = "int_elf_hearth";
				break;
			}
			string name = (string)obj;
			if (race == "orc")
			{
				Vec3 vec3 = OpenMiddle(layout, fit, 0, 1.1f);
				if (layout.PutNear(name, vec3.X, vec3.Y, -MathF.PI / 2f, 0.6f))
				{
					array3 = layout.Last;
					heading = -MathF.PI / 2f;
				}
			}
			else if (AtWallNear(layout, array2[0], name, vec2.X, vec2.Y, 1.6f))
			{
				array3 = layout.Last;
				heading = _lastHeading;
			}
			if (array3 != null)
			{
				AroundHearth(layout, array3, heading, race, tier, rng);
			}
			Layout layout2 = ((num > 1) ? array[1] : layout);
			List<WallSide> list = ((num > 1) ? array2[1] : array2[0]);
			string name2 = "int_" + race + "_bed";
			string name3 = "int_" + race + "_chest";
			int num17 = ((num <= 1) ? ((tier < 2) ? 1 : 2) : ((tier >= 4) ? 3 : 2));
			float num18 = ((array3 != null) ? ((array3[0] + array3[2]) * 0.5f) : room.DoorX);
			float num19 = ((array3 != null) ? ((array3[1] + array3[3]) * 0.5f) : room.DoorY);
			List<float[]> list2 = new List<float[]>();
			layout2.Lenient = true;
			foreach (WallSide item4 in ByDistance(list, num18, num19, room.DoorX, room.DoorY))
			{
				if (list2.Count >= num17)
				{
					break;
				}
				float num20 = 0.5f;
				while (num20 >= 0f && list2.Count < num17)
				{
					if (AgainstRun(layout2, item4, name2, item4.Length * num20, 0.02f) || (num20 != 0.5f && AgainstRun(layout2, item4, name2, item4.Length * (1f - num20), 0.02f)))
					{
						float[] last = layout2.Last;
						list2.Add(last);
						float x = layout2.LastX + item4.Inward.X * (layout2.Depth(name2) * 0.5f + layout2.Depth(name3) * 0.5f + 0.06f);
						float y = layout2.LastY + item4.Inward.Y * (layout2.Depth(name2) * 0.5f + layout2.Depth(name3) * 0.5f + 0.06f);
						layout2.PutNear(name3, x, y, _lastHeading, 0.15f);
						if (tier >= 1 && race != "orc")
						{
							layout2.PutNearAny("int_p_sidetable", 0f, 0.1f, (last[0] - 0.22f, (last[1] + last[3]) * 0.5f), (last[2] + 0.22f, (last[1] + last[3]) * 0.5f));
						}
						break;
					}
					num20 -= 0.25f;
				}
			}
			layout2.Lenient = false;
			if (list2.Count == 0 && StandAtRun(layout2, fit, list, name2, rng, out var at, float.NaN, float.NaN, underCeiling: true, 0.3f, null, bed: true, keepReason: true))
			{
				list2.Add(layout2.Last);
				float x2 = layout2.LastX + at.Inward.X * (layout2.Depth(name2) * 0.5f + layout2.Depth(name3) * 0.5f + 0.06f);
				float y2 = layout2.LastY + at.Inward.Y * (layout2.Depth(name2) * 0.5f + layout2.Depth(name3) * 0.5f + 0.06f);
				layout2.PutNear(name3, x2, y2, _lastHeading, 0.15f);
			}
			if (list2.Count == 0)
			{
				if (num > 1)
				{
					layout.Lenient = true;
					foreach (WallSide item5 in ByDistance(array2[0], num18, num19, room.DoorX, room.DoorY))
					{
						if (AgainstRun(layout, item5, name2, item5.Length * 0.5f, 0.02f))
						{
							list2.Add(layout.Last);
							break;
						}
					}
					layout.Lenient = false;
				}
				Layout[] array4 = ((num <= 1) ? new Layout[1] { layout } : new Layout[2] { layout2, layout });
				foreach (Layout layout3 in array4)
				{
					if (list2.Count > 0)
					{
						break;
					}
					Vec3 vec4 = OpenMiddle(layout3, fit, layout3.Storey, 0.5f);
					float num22 = (float)(rng.NextDouble() * 3.1415927410125732);
					for (int num23 = 0; num23 < 8; num23++)
					{
						if (list2.Count != 0)
						{
							break;
						}
						if (layout3.PutNear("int_v_bedroll", vec4.X, vec4.Y, num22 + (float)num23 * MathF.PI / 8f, 1.2f, solid: false))
						{
							list2.Add(layout3.Last);
						}
					}
				}
			}
			if (tier >= 1)
			{
				object obj2;
				switch (race)
				{
				default:
					obj2 = "int_orc_rack";
					break;
				case "dwarf":
					obj2 = "int_dwarf_shelf";
					break;
				case "elf":
					obj2 = "int_elf_shelf";
					break;
				case "human":
					obj2 = "int_human_wardrobe";
					break;
				}
				string name4 = (string)obj2;
				foreach (WallSide item6 in ByDistance(list, num18, num19, room.DoorX, room.DoorY))
				{
					if (FitRun(layout2, item6, name4, item6.Length * 0.5f, item6.Length * 0.5f))
					{
						break;
					}
				}
			}
			if (num > 1)
			{
				Vec3 vec5 = OpenMiddle(layout2, fit, 1, 0.8f);
				layout2.Put((race == "orc") ? "int_orc_rug" : ((race == "elf") ? "int_elf_rug" : "int_human_rug"), vec5.X, vec5.Y, -MathF.PI / 2f, solid: false, anywhere: true);
				if (race == "elf")
				{
					layout2.PutNear("int_elf_lantern", vec5.X + 0.6f, vec5.Y + 0.4f, 0.3f, 0.4f);
				}
				if (tier >= 3 && race == "human")
				{
					foreach (WallSide item7 in list)
					{
						if (FitRun(layout2, item7, "int_human_bookcase", item7.Length * 0.5f, item7.Length * 0.5f))
						{
							break;
						}
					}
				}
			}
			if (array3 == null && race == "human")
			{
				Layout layout4 = ((num > 1) ? array[1] : null);
				List<WallSide> list3 = new List<WallSide>(array2[0]);
				foreach (List<WallSide> item8 in Wings(layout, fit, array2[0], room))
				{
					list3.RemoveAll(item8.Contains);
				}
				if (list3.Count == 0)
				{
					list3 = array2[0];
				}
				string[] array5 = new string[2] { "int_human_hearth", "int_v_stove" };
				foreach (string text in array5)
				{
					if (models(text) != null && StandAtRun(layout, fit, list3, text, rng, out var _, vec2.X, vec2.Y, underCeiling: false, 0.5f, layout4, bed: false, keepReason: true))
					{
						array3 = layout.Last;
						heading = _lastHeading;
						name = text;
						float num24 = layout.Height(text);
						if (layout4 != null && num24 > fit.StoreyZ[1])
						{
							layout4.Keep(array3[0] - 0.05f, array3[1] - 0.05f, array3[2] + 0.05f, array3[3] + 0.05f);
							room.Solids.Add(new float[6]
							{
								array3[0] + 0.03f,
								array3[1] + 0.03f,
								array3[2] - 0.03f,
								array3[3] - 0.03f,
								fit.StoreyZ[1],
								num24
							});
						}
						AroundHearth(layout, array3, heading, race, tier, rng);
						num18 = (array3[0] + array3[2]) * 0.5f;
						num19 = (array3[1] + array3[3]) * 0.5f;
						break;
					}
				}
			}
			if (race != "orc" || tier >= 1)
			{
				string name5 = ((race == "orc") ? "int_orc_table" : "int_p_counter");
				foreach (WallSide item9 in Beside(array2[0], num18, num19, room.DoorX, room.DoorY))
				{
					if (FitRun(layout, item9, name5, item9.Length * 0.5f, item9.Length * 0.5f))
					{
						float lastX = layout.LastX;
						float lastY = layout.LastY;
						float num25 = layout.Height(name5);
						if (race != "orc")
						{
							HangOnRun(layout, item9, "int_p_wallshelf", lastX, lastY, num25 + 0.42f);
						}
						if (tier >= 1 && race != "orc")
						{
							HangOnRun(layout, item9, "int_p_potrack", lastX + item9.Along.X * 0.6f, lastY + item9.Along.Y * 0.6f, num25 + 0.3f);
						}
						float num26 = lastX + item9.Inward.X * 0.12f;
						float num27 = lastY + item9.Inward.Y * 0.12f;
						float num28 = fit.CeilingAt(0, num26, num27);
						if (race != "dwarf")
						{
							layout.Ceiling("int_p_herbs", num26, num27, Math.Min(1.18f, num28 - layout.Height("int_p_herbs") - 0.04f), (float)Math.Atan2(item9.Along.Y, item9.Along.X));
						}
						layout.PutNear("int_p_bucket", lastX - item9.Along.X * 0.7f + item9.Inward.X * 0.1f, lastY - item9.Along.Y * 0.7f + item9.Inward.Y * 0.1f, 0f, 0.3f);
						break;
					}
				}
				Vec3 vec6 = Corner(array2[0], num18, num19, room.DoorX, room.DoorY);
				layout.PutNear("int_barrel", vec6.X, vec6.Y, (float)rng.NextDouble() * 6f);
				layout.PutNear("int_sack", vec6.X, vec6.Y, (float)rng.NextDouble() * 6f, 0.7f);
				if (tier >= 1)
				{
					layout.PutNear((race == "orc") ? "int_crate" : "int_p_jars", vec6.X, vec6.Y, MathF.PI, 0.8f);
				}
			}
			if (race == "dwarf")
			{
				foreach (WallSide item10 in Beside(array2[0], num18, num19, room.DoorX, room.DoorY))
				{
					if (FitRun(layout, item10, "int_dwarf_kegs", item10.Length * 0.5f, item10.Length * 0.5f))
					{
						break;
					}
				}
			}
			string text2 = "int_" + race + "_table";
			object obj3;
			switch (race)
			{
			default:
				obj3 = "int_orc_stool";
				break;
			case "dwarf":
				obj3 = "int_dwarf_stool";
				break;
			case "elf":
				obj3 = "int_elf_stool";
				break;
			case "human":
				obj3 = ((tier >= 2) ? "int_human_chair" : "int_human_bench");
				break;
			}
			string seat = (string)obj3;
			if (race != "orc")
			{
				Vec3 vec7 = OpenMiddle(layout, fit, 0, 0.9f);
				int seats = ((race == "elf") ? 3 : (2 + Math.Min(2, tier / 2)));
				int count = room.Skipped.Count;
				bool flag = models(text2) != null && layout.PutNear(text2, vec7.X, vec7.Y, -MathF.PI / 2f);
				if (!flag && race == "human" && models(text2) != null)
				{
					flag = layout.PutNear(text2, vec7.X, vec7.Y, 0f) || layout.PutNear(text2, vec7.X, vec7.Y, -MathF.PI / 2f, 1.1f) || layout.PutNear(text2, vec7.X, vec7.Y, 0f, 1.1f);
					int num29 = room.Skipped.Count - count - ((!flag) ? 1 : 0);
					if (num29 > 0)
					{
						room.Skipped.RemoveRange(count, num29);
					}
				}
				if (flag)
				{
					float lastX2 = layout.LastX;
					float lastY2 = layout.LastY;
					float[] last2 = layout.Last;
					bool flag2 = race == "elf";
					if (flag2)
					{
						layout.RoundLast();
					}
					SeatsRound(layout, layout.Last, lastX2, lastY2, seat, seats, flag2);
					if (race == "human" && tier >= 1 && last2 != null)
					{
						RugUnder(layout, "int_human_rug", lastX2, lastY2, (last2[2] - last2[0] >= last2[3] - last2[1]) ? (-MathF.PI / 2f) : 0f);
					}
					if (race == "human" && tier == 1 && fit.CeilingAt(0, lastX2, lastY2) > 1.5f)
					{
						layout.Ceiling("int_p_candlering", lastX2, lastY2, Math.Min(1.32f, fit.CeilingAt(0, lastX2, lastY2) - layout.Height("int_p_candlering") - 0.04f));
					}
					if (tier >= 2 && race != "elf")
					{
						layout.Ceiling("int_p_candlering", lastX2, lastY2, Math.Min(1.32f, fit.CeilingAt(0, lastX2, lastY2) - layout.Height("int_p_candlering") - 0.04f));
					}
					if (race == "elf" && tier >= 1)
					{
						layout.Put("int_v_chandelier", lastX2, lastY2, 0f, solid: false, anywhere: true);
					}
				}
			}
			AtWallNear(layout, array2[0], "int_p_broom", room.DoorX + room.DoorOutY * 0.7f, room.DoorY - room.DoorOutX * 0.7f, 0.5f);
			foreach (var item11 in Decor(race, tier))
			{
				string item = item11.Item1;
				float item2 = item11.Item2;
				bool flag3 = false;
				foreach (WallSide item12 in Shuffled(array2[0], rng))
				{
					if (HangOnRun(layout, item12, item, item12.At(item12.Length * 0.5f, 0f).X + item12.Inward.X * 0.02f, item12.At(item12.Length * 0.5f, 0f).Y + item12.Inward.Y * 0.02f, item2))
					{
						flag3 = true;
						break;
					}
				}
				if (flag3 || num <= 1)
				{
					continue;
				}
				foreach (WallSide item13 in Shuffled(array2[1], rng))
				{
					if (HangOnRun(layout2, item13, item, item13.At(item13.Length * 0.5f, 0f).X, item13.At(item13.Length * 0.5f, 0f).Y, item2))
					{
						break;
					}
				}
			}
			if (race == "human")
			{
				DressFitted(room, fit, array, array2, tier, rng);
			}
			for (int num30 = 0; num30 < num; num30++)
			{
				LightIfDark(room, array[num30], fit, array2[num30], num30, rng);
			}
			for (int num31 = 0; num31 < num; num31++)
			{
				PlaceSpots(room, array[num31]);
			}
		}

		private static void DressFitted(Interior room, FitInfo fit, Layout[] layouts, List<WallSide>[] walls, int tier, Random rng)
		{
			int num = layouts.Length;
			for (int i = 0; i < num; i++)
			{
				Layout layout = layouts[i];
				foreach (List<WallSide> item in Wings(layout, fit, walls[i], room))
				{
					List<string> list = new List<string>();
					if (i == 0 && tier >= 3)
					{
						list.Add("int_v_desk");
					}
					list.Add((i == 0) ? "int_human_bookcase" : "int_v_dresser");
					list.Add((i == 0) ? "int_crate" : "int_human_chest");
					list.Add("int_p_basket");
					int num2 = 0;
					foreach (string item2 in list)
					{
						if (num2 >= 2 || layout.Model(item2) == null || !StandAtRun(layout, fit, item, item2, rng, out var at))
						{
							continue;
						}
						num2++;
						if (item2 == "int_v_desk")
						{
							float num3 = layout.Depth("int_v_desk") * 0.5f + layout.Depth("int_human_chair") * 0.5f + 0.04f;
							float num4 = layout.LastX + at.Inward.X * num3;
							float num5 = layout.LastY + at.Inward.Y * num3;
							float x = num4 + at.Inward.X * 0.4f;
							float y = num5 + at.Inward.Y * 0.4f;
							if (layout.Reached(x, y) && layout.Clearance(x, y, 0.5f) >= 0.3f)
							{
								layout.Put("int_human_chair", num4, num5, _lastHeading + MathF.PI);
							}
						}
					}
					List<WallSide> list2 = new List<WallSide>(item);
					list2.Sort((WallSide p, WallSide q) => q.Length.CompareTo(p.Length));
					(string, float)[] array = new(string, float)[3]
					{
						("int_p_sconce", 0.82f),
						("int_p_tapestry", 0.36f),
						("int_p_shield", 0.95f)
					};
					for (int num6 = 0; num6 < array.Length; num6++)
					{
						var (name, z) = array[num6];
						foreach (WallSide item3 in list2)
						{
							if (!(item3.Length < 0.8f) && !layout.HungNear(item3, item3.Length * 0.5f, 0.45f) && HangLower(layout, item3, name, item3.Length * 0.5f, z))
							{
								break;
							}
						}
					}
				}
				float num7 = FloorArea(layout);
				bool flag = i == 0;
				bool flag2 = i > 0 || num == 1;
				List<string> list3 = new List<string>();
				if (flag)
				{
					list3.Add("int_human_shelf");
					if (tier >= 1)
					{
						list3.Add("int_crate");
					}
					list3.Add("int_p_basket");
					if (tier >= 3)
					{
						list3.Add("int_v_dresser");
					}
				}
				if (flag2 && i > 0)
				{
					if (tier >= 1)
					{
						list3.Add("int_v_dresser");
					}
					list3.Add("int_human_chest");
					list3.Add("int_p_basket");
				}
				int num8 = Math.Max(1, Math.Min(4, (int)(num7 / (flag ? 3.2f : 3.6f))));
				int num9 = 0;
				foreach (string item4 in list3)
				{
					if (num9 >= num8)
					{
						break;
					}
					if (layout.Model(item4) != null && StandAtRun(layout, fit, walls[i], item4, rng, out var at2))
					{
						num9++;
						if (item4 == "int_crate")
						{
							float num10 = layout.Width("int_crate") * 0.5f + 0.2f;
							string name2 = ((rng.Next(2) == 0) ? "int_barrel" : "int_sack");
							layout.PutNearAny(name2, (float)rng.NextDouble() * 6f, 0.12f, (layout.LastX + at2.Along.X * num10, layout.LastY + at2.Along.Y * num10), (layout.LastX - at2.Along.X * num10, layout.LastY - at2.Along.Y * num10));
						}
					}
				}
				List<(string, float)> list4 = new List<(string, float)>();
				if (flag)
				{
					list4.Add(("int_p_wallshelf", 0.95f));
					if (tier >= 1)
					{
						list4.Add(("int_p_tapestry", 0.36f));
					}
					list4.Add(("int_p_antlers", 0.9f));
					list4.Add(("int_p_sconce", 0.82f));
					if (tier >= 2)
					{
						list4.Add(("int_p_shield_b", 0.95f));
					}
				}
				if (flag2 && i > 0)
				{
					list4.Add(("int_p_tapestry", 0.36f));
					list4.Add(("int_p_sconce", 0.82f));
					if (tier >= 1)
					{
						list4.Add(("int_p_shield", 0.95f));
					}
					list4.Add(("int_p_wallshelf", 0.95f));
				}
				int num11 = Math.Max(1, Math.Min(4, (int)(num7 / 2.6f)));
				int num12 = 0;
				List<WallSide> list5 = new List<WallSide>(walls[i]);
				list5.Sort((WallSide p, WallSide q) => q.Length.CompareTo(p.Length));
				HashSet<WallSide> hashSet = new HashSet<WallSide>();
				foreach (var (text, z2) in list4)
				{
					if (num12 >= num11)
					{
						break;
					}
					if (text != "int_p_sconce" && OnStorey(layout, text))
					{
						continue;
					}
					foreach (WallSide item5 in list5)
					{
						if (item5.Length < 1f || hashSet.Contains(item5))
						{
							continue;
						}
						bool flag3 = false;
						float[] array2 = new float[3] { 0.5f, 0.33f, 0.67f };
						foreach (float num13 in array2)
						{
							float num14 = item5.Length * num13;
							if (!layout.HungNear(item5, num14, 0.55f) && HangLower(layout, item5, text, num14, z2))
							{
								flag3 = true;
								break;
							}
						}
						if (flag3)
						{
							hashSet.Add(item5);
							num12++;
							break;
						}
					}
				}
			}
		}

		private static List<List<WallSide>> Wings(Layout L, FitInfo fit, List<WallSide> walls, Interior room)
		{
			List<List<WallSide>> list = new List<List<WallSide>>();
			List<HousePlan.P2> passages = fit.Plan.Storeys[L.Storey].Passages;
			if (passages.Count == 0 || L.Floor == null)
			{
				return list;
			}
			int w = (int)((L.X1 - L.X0) / 0.1f) + 1;
			int h = (int)((L.Y1 - L.Y0) / 0.1f) + 1;
			bool[] open = new bool[w * h];
			for (int i = 0; i < h; i++)
			{
				for (int j = 0; j < w; j++)
				{
					float num = L.X0 + (float)j * 0.1f;
					float num2 = L.Y0 + (float)i * 0.1f;
					bool flag = L.Floor.Clear(num, num2, 0.12f);
					foreach (HousePlan.P2 item in passages)
					{
						float num3 = fit.RoomX(item.X);
						float num4 = fit.RoomY(item.Y);
						if (Math.Abs(num - num3) < 0.3f && Math.Abs(num2 - num4) < 0.3f)
						{
							flag = false;
						}
					}
					open[i * w + j] = flag;
				}
			}
			float num5 = room.SpawnX;
			float num6 = room.SpawnY;
			if (L.Storey > 0 && !float.IsNaN(fit.StairFootX))
			{
				num5 = fit.StairHeadX;
				num6 = fit.StairHeadY;
			}
			bool[] seen = new bool[w * h];
			Stack<int> stack = new Stack<int>();
			int num7 = Math.Max(0, Math.Min(w - 1, (int)Math.Round((num5 - L.X0) / 0.1f)));
			int num8 = Math.Max(0, Math.Min(h - 1, (int)Math.Round((num6 - L.Y0) / 0.1f)));
			int num9 = -1;
			int num10 = int.MaxValue;
			for (int k = 0; k < open.Length; k++)
			{
				if (open[k])
				{
					int num11 = (k % w - num7) * (k % w - num7) + (k / w - num8) * (k / w - num8);
					if (num11 < num10)
					{
						num10 = num11;
						num9 = k;
					}
				}
			}
			if (num9 < 0)
			{
				return list;
			}
			stack.Push(num9);
			seen[num9] = true;
			while (stack.Count > 0)
			{
				int num12 = stack.Pop();
				int num13 = num12 % w;
				int num14 = num12 / w;
				int[] array = new int[4]
				{
					(num13 > 0) ? (num12 - 1) : (-1),
					(num13 < w - 1) ? (num12 + 1) : (-1),
					(num14 > 0) ? (num12 - w) : (-1),
					(num14 < h - 1) ? (num12 + w) : (-1)
				};
				foreach (int num15 in array)
				{
					if (num15 >= 0 && open[num15] && !seen[num15])
					{
						seen[num15] = true;
						stack.Push(num15);
					}
				}
			}
			List<WallSide> list2 = new List<WallSide>();
			foreach (WallSide wall in walls)
			{
				int num16 = 0;
				int num17 = 0;
				float num18 = 0.1f;
				while (num18 < wall.Length - 0.05f)
				{
					Vec3 vec = wall.At(num18, 0f) + wall.Inward * 0.2f;
					if (InWing(vec.X, vec.Y))
					{
						num17++;
					}
					num18 += 0.1f;
					num16++;
				}
				if (num16 > 0 && num17 * 2 > num16)
				{
					list2.Add(wall);
				}
			}
			if (list2.Count > 0)
			{
				list.Add(list2);
			}
			return list;
			bool InWing(float x, float y)
			{
				int num19 = (int)Math.Round((x - L.X0) / 0.1f);
				int num20 = (int)Math.Round((y - L.Y0) / 0.1f);
				if (num19 >= 0 && num20 >= 0 && num19 < w && num20 < h && open[num20 * w + num19])
				{
					return !seen[num20 * w + num19];
				}
				return false;
			}
		}

		private static bool OnStorey(Layout L, string name)
		{
			float num = ((L.Fitted != null && L.Storey + 1 < L.Fitted.StoreyZ.Length) ? (L.Fitted.StoreyZ[L.Storey + 1] - 0.05f) : float.MaxValue);
			foreach (MeshInstance mesh in L.Room.Meshes)
			{
				if (mesh.Model != null && mesh.Model.Name == name && mesh.Position.Z >= L.Base - 0.05f && mesh.Position.Z < num)
				{
					return true;
				}
			}
			return false;
		}

		private static float FloorArea(Layout L)
		{
			if (L.Floor == null)
			{
				return (L.X1 - L.X0) * (L.Y1 - L.Y0);
			}
			int num = 0;
			for (float num2 = L.Y0; num2 < L.Y1; num2 += 0.1f)
			{
				for (float num3 = L.X0; num3 < L.X1; num3 += 0.1f)
				{
					if (L.Floor.Inside(num3, num2))
					{
						num++;
					}
				}
			}
			return (float)num * 0.01f;
		}

		private static bool StandAtRun(Layout L, FitInfo fit, List<WallSide> walls, string name, Random rng, out WallSide at, float nearX = float.NaN, float nearY = float.NaN, bool underCeiling = true, float front = 0.3f, Layout above = null, bool bed = false, bool keepReason = false)
		{
			at = null;
			MeshModel meshModel = L.Model(name);
			if (meshModel == null)
			{
				return false;
			}
			float num = L.Width(name) * 0.5f;
			float num2 = L.Depth(name);
			float num3 = L.Height(name);
			float num4 = meshModel.Max.Y * 0.5714286f + 0.02f;
			List<(WallSide, float, float)> list = new List<(WallSide, float, float)>();
			foreach (WallSide item3 in Shuffled(walls, rng))
			{
				if (item3.Length < 2f * num + 0.1f)
				{
					continue;
				}
				for (float num5 = num + 0.06f; num5 <= item3.Length * 0.5f + 0.01f; num5 += 0.12f)
				{
					float[] array = new float[2]
					{
						num5,
						item3.Length - num5
					};
					foreach (float num6 in array)
					{
						Vec3 vec = item3.At(num6, 0f);
						list.Add((item3, num6, float.IsNaN(nearX) ? ((float)list.Count) : ((vec.X - nearX) * (vec.X - nearX) + (vec.Y - nearY) * (vec.Y - nearY))));
					}
				}
			}
			list.Sort(((WallSide W, float A, float D) p, (WallSide W, float A, float D) q) => p.D.CompareTo(q.D));
			int count = L.Room.Skipped.Count;
			foreach (var item4 in list)
			{
				WallSide item = item4.Item1;
				float item2 = item4.Item2;
				float num7 = (float)Math.Atan2(item.Inward.Y, item.Inward.X);
				Vec3 vec2 = item.At(item2, 0f) + item.Inward * num4;
				float num8 = vec2.X + item.Inward.X * (num2 * 0.5f + 0.32f);
				float num9 = vec2.Y + item.Inward.Y * (num2 * 0.5f + 0.32f);
				if ((L.Base < 0.01f && (num8 - L.Room.DoorX) * (num8 - L.Room.DoorX) + (num9 - L.Room.DoorY) * (num9 - L.Room.DoorY) < 1f) || ByStair(fit, L.Storey, new Vec3(vec2.X, vec2.Y, 0f)) || ByStair(fit, L.Storey, new Vec3(num8, num9, 0f)) || (underCeiling && fit.CeilingAt(L.Storey, vec2.X, vec2.Y) - L.Base < num3 + 0.04f))
				{
					continue;
				}
				Vec3 vec3 = item.At(item2, 0f) + item.Inward * (num2 * 0.5f + 0.02f);
				float num10 = Math.Abs(item.Along.X) * num + Math.Abs(item.Inward.X) * num2 * 0.5f;
				float num11 = Math.Abs(item.Along.Y) * num + Math.Abs(item.Inward.Y) * num2 * 0.5f;
				float[] array2 = new float[4]
				{
					vec3.X - num10,
					vec3.Y - num11,
					vec3.X + num10,
					vec3.Y + num11
				};
				if (bed)
				{
					bool flag = true;
					float[] array = new float[2]
					{
						array2[0] - 0.35f,
						array2[2] + 0.35f
					};
					foreach (float num12 in array)
					{
						if (fit.Clear(L.Storey, num12, vec3.Y, 0.18f) && !L.Reached(num12, vec3.Y))
						{
							flag = false;
						}
					}
					if (!flag)
					{
						continue;
					}
				}
				else
				{
					float x = num8 + item.Inward.X * (front - 0.3f);
					float y = num9 + item.Inward.Y * (front - 0.3f);
					if (L.Clearance(x, y, 1f) < front || !L.Reached(x, y))
					{
						continue;
					}
				}
				if ((above == null || !(L.Base + num3 > above.Base) || (above.Free(array2) && above.KeepsWay(array2))) && L.Put(name, vec2.X, vec2.Y, num7))
				{
					L.Room.Skipped.RemoveRange(count, L.Room.Skipped.Count - count);
					_lastHeading = num7;
					at = item;
					return true;
				}
			}
			int num13 = (keepReason ? 1 : 0);
			if (L.Room.Skipped.Count - count > num13)
			{
				L.Room.Skipped.RemoveRange(count, L.Room.Skipped.Count - count - num13);
			}
			return false;
		}

		private static bool HangLower(Layout L, WallSide w, string name, float along, float z)
		{
			Vec3 vec = w.At(along, 0f);
			for (int i = 0; i < 4; i++)
			{
				float num = z - (float)i * 0.12f;
				if (num < 0.3f)
				{
					break;
				}
				if (HangOnRun(L, w, name, vec.X + w.Inward.X * 0.02f, vec.Y + w.Inward.Y * 0.02f, num))
				{
					return true;
				}
			}
			return false;
		}

		private static void RugUnder(Layout L, string rug, float x, float y, float heading)
		{
			float num = L.Width(rug) * 0.5f;
			float num2 = L.Depth(rug) * 0.5f;
			bool num3 = Math.Abs(Math.Sin(heading)) > 0.5;
			float num4 = (num3 ? num : num2);
			float num5 = (num3 ? num2 : num);
			foreach (var item in L.Placed)
			{
				if (item.Use == "rug" && item.Box[0] < x + num4 && item.Box[2] > x - num4 && item.Box[1] < y + num5 && item.Box[3] > y - num5)
				{
					return;
				}
			}
			int count = L.Room.Skipped.Count;
			if (!L.Put(rug, x, y, heading, solid: false, anywhere: true))
			{
				L.Room.Skipped.RemoveRange(count, L.Room.Skipped.Count - count);
			}
		}

		private static void LightIfDark(Interior room, Layout L, FitInfo fit, List<WallSide> walls, int storey, Random rng)
		{
			float num = fit.StoreyZ[storey];
			float num2 = ((storey + 1 < fit.StoreyZ.Length) ? fit.StoreyZ[storey + 1] : float.MaxValue);
			foreach (RoomLight light in room.Lights)
			{
				if (light.Light.Z >= num - 0.05f && light.Light.Z < num2 - 0.05f && fit.Inside(storey, light.Light.X, light.Light.Y))
				{
					return;
				}
			}
			List<WallSide> list = new List<WallSide>(walls);
			list.Sort((WallSide a, WallSide b) => b.Length.CompareTo(a.Length));
			int count = room.Lights.Count;
			foreach (WallSide item in list)
			{
				if (HangOnRun(L, item, "int_p_sconce", item.At(item.Length * 0.5f, 0f).X, item.At(item.Length * 0.5f, 0f).Y, 0.82f) && room.Lights.Count > count)
				{
					return;
				}
			}
			Vec3 vec = OpenMiddle(L, fit, storey, 0.3f);
			if (!L.PutNear("int_v_lamp", vec.X, vec.Y, (float)rng.NextDouble() * 6f, 1.2f, solid: false) || room.Lights.Count <= count)
			{
				float num3 = fit.CeilingAt(storey, vec.X, vec.Y);
				room.Lights.Add(new RoomLight
				{
					Light = new PointLight
					{
						X = vec.X,
						Y = vec.Y,
						Z = Math.Min(num + 1f, num3 - 0.25f),
						Radius = 2.4f,
						R = 0.55f,
						G = 0.38f,
						B = 0.2f,
						Glow = 0.04f
					},
					Fire = true
				});
			}
		}

		private static void FurnishTent(Interior room, FitInfo fit, Func<string, MeshModel> models, string race, uint roofColor, Random rng)
		{
			List<WallSide> walls = Walls(fit, 0);
			Layout layout = new Layout(room, models, VariantColors(race, roofColor), room.MinX, room.MinY, room.MaxX, room.MaxY)
			{
				Fitted = fit,
				Storey = 0,
				WallTop = 1f,
				CeilingAt = (float x, float y) => fit.CeilingAt(0, x, y),
				Floor = new FloorGrid(fit, 0, room.MinX, room.MinY, room.MaxX, room.MaxY)
			};
			float num = 0f - room.DoorOutX;
			float num2 = 0f - room.DoorOutY;
			layout.Keep(room.DoorX - 0.45f, room.DoorY - 0.45f, room.DoorX + 0.45f, room.DoorY + 0.45f);
			Vec3 vec = Furthest(walls, room.DoorX, room.DoorY);
			float num3 = (room.MinX + room.MaxX) * 0.5f;
			float num4 = (room.MinY + room.MaxY) * 0.5f;
			float num5 = ((room.MaxX - room.MinX >= room.MaxY - room.MinY) ? 0f : (MathF.PI / 2f));
			if (!layout.PutNear("int_v_bedroll", (num3 + vec.X) * 0.5f, (num4 + vec.Y) * 0.5f, num5, 0.4f, solid: false))
			{
				for (int num6 = 1; num6 < 8 && !layout.PutNear("int_v_bedroll", num3, num4, num5 + (float)num6 * MathF.PI / 8f, 0.8f, solid: false); num6++)
				{
				}
			}
			object obj;
			switch (race)
			{
			default:
				obj = "int_human_chest";
				break;
			case "dwarf":
				obj = "int_dwarf_chest";
				break;
			case "elf":
				obj = "int_elf_chest";
				break;
			case "orc":
				obj = "int_orc_chest";
				break;
			}
			string name = (string)obj;
			AtWallNear(layout, walls, name, vec.X + num2 * 0.6f, vec.Y - num * 0.6f, 0.8f);
			string name2 = ((race == "elf") ? "int_v_cushions" : ((race == "orc") ? "int_orc_drum" : "int_sack"));
			AtWallNear(layout, walls, name2, vec.X - num2 * 0.6f, vec.Y + num * 0.6f, 0.8f);
			if (race == "orc")
			{
				layout.Put("int_orc_rug", num3, num4, -MathF.PI / 2f, solid: false, anywhere: true);
			}
			if (race == "elf")
			{
				layout.PutNear("int_elf_lantern", num3 + num2 * 0.5f, num4 - num * 0.5f, 0.3f, 0.4f);
			}
			else
			{
				layout.Put("int_v_lamp", num3, num4, 0f, solid: false, anywhere: true);
			}
			LightIfDark(room, layout, fit, walls, 0, rng);
			PlaceSpots(room, layout);
		}

		private static IEnumerable<(string, float)> Decor(string race, int tier)
		{
			yield return ("int_p_sconce", 0.82f);
			if (tier >= 1)
			{
				yield return ((race == "orc") ? "int_p_antlers" : "int_p_shield", (race == "orc") ? 0.7f : 0.98f);
			}
			if (tier >= 2)
			{
				yield return ("int_p_sconce", 0.82f);
			}
			if (tier >= 2 && race != "elf")
			{
				yield return ("int_p_arms", 0.9f);
			}
			if (tier >= 3)
			{
				yield return ((race == "elf") ? "int_p_tapestry" : "int_p_antlers", (race == "elf") ? 0.42f : 0.98f);
			}
			if (tier >= 4)
			{
				yield return ("int_p_shield_b", 0.98f);
			}
		}

		private static void AroundHearth(Layout L, float[] hearth, float heading, string race, int tier, Random rng)
		{
			float num = (hearth[0] + hearth[2]) * 0.5f;
			float num2 = (hearth[1] + hearth[3]) * 0.5f;
			float num3 = (float)Math.Cos(heading);
			float num4 = (float)Math.Sin(heading);
			float num5 = 0f - num4;
			float num6 = num3;
			float num7 = ((Math.Abs(num3) > 0.5f) ? ((hearth[2] - hearth[0]) * 0.5f) : ((hearth[3] - hearth[1]) * 0.5f));
			float num8 = ((Math.Abs(num3) > 0.5f) ? ((hearth[3] - hearth[1]) * 0.5f) : ((hearth[2] - hearth[0]) * 0.5f));
			string name = ((race == "elf") ? "int_elf_rug" : ((race == "orc") ? "int_orc_rug" : ((tier >= 1) ? "int_p_pelt" : "int_human_rug")));
			L.Put(name, num + num3 * (num7 + 0.42f), num2 + num4 * (num7 + 0.42f), heading, solid: false, anywhere: true);
			object obj;
			switch (race)
			{
			case "orc":
				L.PutNear("int_orc_stool", num + num3 * (num7 + 0.3f) + num5 * 0.45f, num2 + num4 * (num7 + 0.3f) + num6 * 0.45f, heading + MathF.PI - 0.4f, 0.15f);
				L.PutNear("int_orc_stool", num + num3 * (num7 + 0.3f) - num5 * 0.45f, num2 + num4 * (num7 + 0.3f) - num6 * 0.45f, heading + MathF.PI + 0.4f, 0.15f);
				L.PutNear("int_p_cauldron", num + num5 * (num8 + 0.3f), num2 + num6 * (num8 + 0.3f), 0f, 0.2f);
				if (tier >= 1)
				{
					L.PutNear("int_orc_drum", num - num5 * (num8 + 0.45f), num2 - num6 * (num8 + 0.45f), 0f, 0.25f);
				}
				return;
			default:
				obj = ((tier >= 1) ? "int_p_armchair" : "int_human_bench");
				break;
			case "dwarf":
				obj = "int_dwarf_stool";
				break;
			case "elf":
				obj = "int_v_cushions";
				break;
			}
			string text = (string)obj;
			float num9 = ((race == "elf") ? 0.6f : 0.72f);
			float num10 = ((race == "elf") ? 0.76f : 0.62f);
			if (text == "int_human_bench")
			{
				L.PutNear(text, num + num3 * (num7 + num9), num2 + num4 * (num7 + num9), heading + MathF.PI, 0.2f);
			}
			else
			{
				float[] array = new float[2] { 1f, -1f };
				foreach (float num11 in array)
				{
					float num12 = num + num3 * (num7 + num9) + num5 * num10 * num11;
					float num13 = num2 + num4 * (num7 + num9) + num6 * num10 * num11;
					if (L.Reached(num12 + num3 * 0.4f, num13 + num4 * 0.4f))
					{
						L.PutNear(text, num12, num13, heading + MathF.PI + 0.45f * num11, 0.2f);
					}
				}
				if (race == "human" && tier >= 1)
				{
					L.PutNear("int_p_sidetable", num + num3 * (num7 + num9 - 0.08f), num2 + num4 * (num7 + num9 - 0.08f), heading + MathF.PI, 0.15f);
				}
			}
			if (race == "dwarf")
			{
				L.PutNear("int_dwarf_anvil", num + num3 * (num7 + 0.45f) - num5 * (num8 * 0.6f), num2 + num4 * (num7 + 0.45f) - num6 * (num8 * 0.6f), heading + 0.3f, 0.2f);
			}
			if (race != "elf")
			{
				float num14 = L.Width("int_p_firewood") * 0.5f + num8 + 0.06f;
				L.PutNearAny("int_p_firewood", heading, 0.15f, (num + num5 * num14 - num3 * 0f, num2 + num6 * num14), (num - num5 * num14, num2 - num6 * num14));
			}
			if (race == "human" && tier >= 1 && L.Has("int_p_cauldron"))
			{
				L.PutNear("int_p_cauldron", num + num3 * (num7 + 0.2f) - num5 * (num8 + 0.25f), num2 + num4 * (num7 + 0.2f) - num6 * (num8 + 0.25f), 0f, 0.15f);
			}
			if (race == "elf" && tier >= 2)
			{
				L.PutNear("int_v_harp", num + num5 * (num8 + 0.4f), num2 + num6 * (num8 + 0.4f), heading, 0.3f);
			}
		}

		private static void SeatsRound(Layout L, float[] t, float x, float y, string seat, int seats, bool round)
		{
			if (t == null)
			{
				return;
			}
			float num = (t[2] - t[0]) * 0.5f;
			float num2 = (t[3] - t[1]) * 0.5f;
			float num3 = L.Depth(seat) * 0.5f;
			if (round)
			{
				for (int i = 0; i < seats; i++)
				{
					float num4 = MathF.PI / 2f + (float)i * 2f * MathF.PI / (float)seats + 0.35f;
					float num5 = num + num3 + 0.06f;
					if (!(L.Clearance(x + (float)Math.Cos(num4) * (num5 + num3 + 0.3f), y + (float)Math.Sin(num4) * (num5 + num3 + 0.3f), 0.5f) < 0.24f))
					{
						L.PutNear(seat, x + (float)Math.Cos(num4) * num5, y + (float)Math.Sin(num4) * num5, num4 + MathF.PI, 0.12f);
					}
				}
				return;
			}
			List<(float, float, float)> list = new List<(float, float, float)>
			{
				(x, y + num2 + num3 + 0.04f, -MathF.PI / 2f),
				(x, y - num2 - num3 - 0.04f, MathF.PI / 2f),
				(x - num - num3 - 0.04f, y, 0f),
				(x + num + num3 + 0.04f, y, MathF.PI)
			};
			int j = 0;
			int num6 = 0;
			for (; j < list.Count; j++)
			{
				if (num6 >= seats)
				{
					break;
				}
				float x2 = list[j].Item1 - (float)Math.Cos(list[j].Item3) * (num3 + 0.3f);
				float y2 = list[j].Item2 - (float)Math.Sin(list[j].Item3) * (num3 + 0.3f);
				if (!(L.Clearance(x2, y2, 0.5f) < 0.24f) && L.Put(seat, list[j].Item1, list[j].Item2, list[j].Item3))
				{
					num6++;
				}
			}
		}

		private static bool AgainstRun(Layout L, WallSide w, string name, float along, float gap)
		{
			MeshModel meshModel = L.Model(name);
			if (meshModel == null)
			{
				return false;
			}
			float num = meshModel.Max.Y * 0.5714286f + gap;
			float num2 = L.Width(name) * 0.5f;
			if (along < num2 - 0.05f || along > w.Length - num2 + 0.05f)
			{
				return false;
			}
			Vec3 vec = w.At(along, 0f) + w.Inward * num;
			return L.Put(heading: _lastHeading = (float)Math.Atan2(w.Inward.Y, w.Inward.X), name: name, x: vec.X, y: vec.Y);
		}

		private static bool FitRun(Layout L, WallSide w, string name, float along, float range)
		{
			int count = L.Room.Skipped.Count;
			for (float num = 0f; num <= range + 0.001f; num += 0.08f)
			{
				if (AgainstRun(L, w, name, along + num, 0.02f) || (num > 0f && AgainstRun(L, w, name, along - num, 0.02f)))
				{
					L.Room.Skipped.RemoveRange(count, L.Room.Skipped.Count - count);
					return true;
				}
			}
			if (L.Room.Skipped.Count - count > 1)
			{
				L.Room.Skipped.RemoveRange(count, L.Room.Skipped.Count - count - 1);
			}
			return false;
		}

		private static bool AtWallNear(Layout L, List<WallSide> walls, string name, float x, float y, float range)
		{
			List<(float, WallSide, float)> list = new List<(float, WallSide, float)>();
			foreach (WallSide wall in walls)
			{
				float val = (x - wall.Start.X) * wall.Along.X + (y - wall.Start.Y) * wall.Along.Y;
				float num = Math.Max(0f, Math.Min(wall.Length, val));
				Vec3 vec = wall.At(num, 0f);
				list.Add(((float)Math.Sqrt((vec.X - x) * (vec.X - x) + (vec.Y - y) * (vec.Y - y)), wall, num));
			}
			list.Sort(((float D, WallSide W, float A) p, (float D, WallSide W, float A) q) => p.D.CompareTo(q.D));
			foreach (var (num2, w, along) in list)
			{
				if (num2 > range + 1.5f)
				{
					break;
				}
				if (FitRun(L, w, name, along, Math.Max(0.4f, range)))
				{
					return true;
				}
			}
			return false;
		}

		private static bool HangOnRun(Layout L, WallSide w, string name, float x, float y, float z)
		{
			MeshModel meshModel = L.Model(name);
			if (meshModel == null)
			{
				return false;
			}
			float num = (meshModel.Max.X - meshModel.Min.X) * 0.5714286f * 0.5f;
			float num2 = (meshModel.Max.Z - meshModel.Min.Z) * 0.5714286f;
			float val = (x - w.Start.X) * w.Along.X + (y - w.Start.Y) * w.Along.Y;
			val = Math.Max(num + 0.08f, Math.Min(w.Length - num - 0.08f, val));
			if (w.Length < 2f * num + 0.16f)
			{
				return false;
			}
			float num3 = L.Base + z;
			float num4 = L.Fitted.CeilingAt(L.Storey, w.At(val, 0f).X + w.Inward.X * 0.1f, w.At(val, 0f).Y + w.Inward.Y * 0.1f);
			if (num3 + num2 > num4 - 0.06f)
			{
				return false;
			}
			foreach (Hole hole in w.Holes)
			{
				if (val + num > hole.A0 - 0.08f && val - num < hole.A1 + 0.08f && num3 - L.Base < hole.Z1 + 0.05f && num3 - L.Base + num2 > hole.Z0 - 0.05f)
				{
					return false;
				}
			}
			if (!L.WallFree(w, val, num, num3, num2))
			{
				return false;
			}
			if ((meshModel.Max.Y - meshModel.Min.Y) * 0.5714286f > 0.05f && z < 1.15f && ByStair(L.Fitted, L.Storey, w.At(val, 0f) + w.Inward * 0.1f))
			{
				return false;
			}
			Vec3 vec = w.At(val, 0f) + w.Inward * (meshModel.Max.Y * 0.5714286f + 0.004f);
			float heading = (float)Math.Atan2(w.Inward.Y, w.Inward.X);
			MeshInstance meshInstance = new MeshInstance
			{
				Model = meshModel,
				Palette = L.PaletteFor(meshModel),
				NoShadow = true,
				Static = true,
				Tag = -1
			};
			meshInstance.Place(vec.X, vec.Y, num3 - meshModel.Min.Z * 0.5714286f, heading, 0.5714286f, 0.5714286f, 0.5714286f);
			L.Room.Meshes.Add(meshInstance);
			L.Hung(meshModel, meshInstance, w, val, num, num3, num2);
			return true;
		}

		private static Vec3 Furthest(List<WallSide> walls, float x, float y)
		{
			Vec3 result = new Vec3(x, y, 0f);
			float num = -1f;
			foreach (WallSide wall in walls)
			{
				Vec3 vec = wall.At(wall.Length * 0.5f, 0f);
				float num2 = (vec.X - x) * (vec.X - x) + (vec.Y - y) * (vec.Y - y);
				if (num2 > num && wall.Length > 0.8f)
				{
					num = num2;
					result = vec;
				}
			}
			return result;
		}

		private static List<WallSide> ByDistance(List<WallSide> walls, float ax, float ay, float bx, float by)
		{
			List<WallSide> list = new List<WallSide>(walls);
			list.Sort((WallSide p, WallSide q) => Score(p).CompareTo(Score(q)));
			return list;
			float Score(WallSide w)
			{
				Vec3 vec = w.At(w.Length * 0.5f, 0f);
				float val = (float)Math.Sqrt((vec.X - ax) * (vec.X - ax) + (vec.Y - ay) * (vec.Y - ay));
				float val2 = (float)Math.Sqrt((vec.X - bx) * (vec.X - bx) + (vec.Y - by) * (vec.Y - by));
				return 0f - (Math.Min(val, val2) + 0.3f * Math.Min(w.Length, 2.5f));
			}
		}

		private static List<WallSide> Beside(List<WallSide> walls, float hx, float hy, float dx, float dy)
		{
			List<WallSide> list = new List<WallSide>();
			foreach (WallSide wall in walls)
			{
				float a = Math.Max(0f, Math.Min(wall.Length, (hx - wall.Start.X) * wall.Along.X + (hy - wall.Start.Y) * wall.Along.Y));
				Vec3 vec = wall.At(a, 0f);
				float num = (float)Math.Sqrt((vec.X - hx) * (vec.X - hx) + (vec.Y - hy) * (vec.Y - hy));
				float num2 = Math.Abs((hx - wall.Start.X) * wall.Inward.X + (hy - wall.Start.Y) * wall.Inward.Y);
				if (num < 2.2f && num2 > 0.5f && wall.Length > 1f)
				{
					list.Add(wall);
				}
			}
			list.Sort((WallSide p, WallSide q) => Far(q, dx, dy).CompareTo(Far(p, dx, dy)));
			return list;
		}

		private static float Far(WallSide w, float x, float y)
		{
			Vec3 vec = w.At(w.Length * 0.5f, 0f);
			return (vec.X - x) * (vec.X - x) + (vec.Y - y) * (vec.Y - y);
		}

		private static Vec3 Corner(List<WallSide> walls, float hx, float hy, float dx, float dy)
		{
			Vec3 result = ((walls.Count > 0) ? walls[0].Start : new Vec3(hx, hy, 0f));
			float num = float.MaxValue;
			foreach (WallSide wall in walls)
			{
				Vec3 vec = wall.Start + wall.Along * 0.3f + wall.Inward * 0.3f;
				float num2 = (float)Math.Sqrt((vec.X - dx) * (vec.X - dx) + (vec.Y - dy) * (vec.Y - dy));
				if (!(num2 < 1.3f))
				{
					float num3 = (float)Math.Sqrt((vec.X - hx) * (vec.X - hx) + (vec.Y - hy) * (vec.Y - hy)) - num2 * 0.3f;
					if (num3 < num)
					{
						num = num3;
						result = vec;
					}
				}
			}
			return result;
		}

		private static Vec3 OpenMiddle(Layout L, FitInfo fit, int storey, float clearance)
		{
			Vec3 result = new Vec3((L.X0 + L.X1) * 0.5f, (L.Y0 + L.Y1) * 0.5f, 0f);
			float num = float.MinValue;
			for (float num2 = L.Y0 + 0.3f; num2 < L.Y1 - 0.3f; num2 += 0.15f)
			{
				for (float num3 = L.X0 + 0.3f; num3 < L.X1 - 0.3f; num3 += 0.15f)
				{
					if (!((L.Floor != null) ? (!L.Floor.Clear(num3, num2, clearance * 0.6f)) : (!fit.Clear(storey, num3, num2, clearance * 0.6f))))
					{
						float num4 = L.Clearance(num3, num2, 2.5f) - 0.15f * (float)Math.Sqrt((num3 - result.X) * (num3 - result.X) + (num2 - result.Y) * (num2 - result.Y));
						if (num4 > num)
						{
							num = num4;
							result = new Vec3(num3, num2, 0f);
						}
					}
				}
			}
			return result;
		}

		private static IEnumerable<WallSide> Shuffled(List<WallSide> walls, Random rng)
		{
			List<WallSide> list = new List<WallSide>(walls);
			for (int num = list.Count - 1; num > 0; num--)
			{
				int index = rng.Next(num + 1);
				WallSide value = list[num];
				list[num] = list[index];
				list[index] = value;
			}
			return list;
		}

		private static Interior BuildRoom(int roomW, int roomD, uint wallColor, uint roofColor, int seed, string race, int tier, Func<string, MeshModel> models)
		{
			roomW = Math.Max(4, roomW);
			roomD = Math.Max(4, roomD);
			Interior interior = new Interior
			{
				Race = race,
				Tier = tier
			};
			Random rng = new Random(seed);
			Style style = StyleFor(race, wallColor, roofColor);
			float num = 1f;
			float num2 = 1f;
			float num3 = 1f + (float)roomW;
			float num4 = 1f + (float)roomD;
			int num5 = roomW + 2;
			int num6 = roomD + 2;
			TerrainCache terrainCache = new TerrainCache();
			terrainCache.Allocate(num5, num6);
			terrainCache.CastShadows = false;
			terrainCache.DitherEdges = false;
			terrainCache.OutsideIsWater = false;
			terrainCache.OutsideHeight = style.WallH;
			terrainCache.OutsideColor = TerrainCache.Scale(style.Wall, 0.7f);
			for (int i = 0; i < num6; i++)
			{
				for (int j = 0; j < num5; j++)
				{
					int num7 = i * num5 + j;
					terrainCache.BaseColors[num7] = ((race == "dwarf") ? TerrainCache.Scale(style.Floor, 0.45f) : TerrainCache.Scale(style.Floor, 0.95f + (float)(TerrainModel.Hash(j, i * 7) & 7) / 160f));
					terrainCache.Materials[num7] = style.FloorSurface;
					if (style.MeshFloor || race == "dwarf")
					{
						terrainCache.Heights[num7] = (terrainCache.Ground[num7] = -0.03f);
					}
				}
			}
			terrainCache.Relight(0, 0, num5, num6);
			interior.Floor = terrainCache;
			interior.CeilingTop = TerrainCache.Scale(style.Ceiling, 0.5f);
			interior.CeilingBottom = TerrainCache.Scale(style.Ceiling, 0.65f);
			interior.Meshes.Add(BuildShell(interior, style, race, num, num2, num3, num4, rng, RaceWindows(race, roomW, roomD)));
			interior.MinX = num + 0.25f;
			interior.MaxX = num3 - 0.25f;
			interior.MinY = num2 + 0.25f;
			interior.MaxY = num4 - 0.25f;
			interior.DoorX = (num + num3) * 0.5f;
			interior.DoorY = num2 + 0.45f;
			interior.SpawnX = interior.DoorX;
			interior.SpawnY = num2 + 0.75f;
			interior.SpawnYaw = MathF.PI / 2f;
			interior.HearthX = num3 - 0.4f;
			interior.HearthY = (num2 + num4) * 0.5f;
			Layout layout = new Layout(interior, models, FurnitureColors(race, roofColor), num, num2, num3, num4);
			layout.Keep(interior.DoorX - 0.55f, num2, interior.DoorX + 0.55f, num2 + 1.05f);
			layout.WallTop = style.WallH;
			layout.CeilingAt = RaceCeiling(race, style, num, num2, num3, num4);
			switch (race)
			{
			case "elf":
				FurnishElf(layout, tier, rng);
				break;
			case "orc":
				FurnishOrc(layout, tier, rng);
				break;
			case "dwarf":
				FurnishDwarf(layout, tier, rng);
				break;
			default:
				FurnishHuman(layout, tier, rng);
				break;
			}
			PlaceSpots(interior, layout);
			return interior;
		}

		private static Dictionary<string, uint> FurnitureColors(string race, uint roof)
		{
			Dictionary<string, uint> dictionary = new Dictionary<string, uint>
			{
				["dark"] = Rgb(40, 32, 28),
				["fire"] = Rgb(255, 132, 32),
				["fire_core"] = Rgb(255, 226, 110),
				["paper"] = Rgb(232, 220, 190),
				["leather"] = Rgb(120, 70, 48),
				["bone"] = Rgb(232, 226, 204),
				["wax"] = Rgb(240, 232, 210),
				["food"] = Rgb(196, 120, 60),
				["rock"] = Rgb(128, 122, 116),
				["leaf"] = Rgb(62, 132, 60),
				["leaf_light"] = Rgb(110, 170, 80),
				["fur"] = Rgb(120, 92, 64),
				["fur_light"] = Rgb(206, 186, 150),
				["bark"] = Rgb(96, 70, 48),
				["metal"] = Rgb(110, 112, 120),
				["ceramic"] = Rgb(176, 110, 80),
				["stone"] = Rgb(140, 136, 128),
				["glow"] = Rgb(255, 214, 140),
				["cloth_light"] = Rgb(232, 226, 210)
			};
			switch (race)
			{
			case "elf":
				dictionary["wood"] = Rgb(196, 160, 108);
				dictionary["wood_dark"] = Rgb(150, 116, 76);
				dictionary["cloth"] = TerrainCache.Lerp(roof, Rgb(80, 140, 96), 0.6f);
				dictionary["cloth_light"] = Rgb(234, 238, 224);
				dictionary["stone"] = Rgb(196, 198, 190);
				dictionary["metal"] = Rgb(204, 178, 116);
				dictionary["ceramic"] = Rgb(188, 204, 212);
				dictionary["glow"] = Rgb(176, 255, 214);
				dictionary["fur_light"] = Rgb(228, 222, 200);
				dictionary["leaf"] = Rgb(64, 136, 64);
				dictionary["leaf_light"] = Rgb(120, 182, 84);
				dictionary["bark"] = Rgb(120, 92, 64);
				break;
			case "orc":
				dictionary["wood"] = Rgb(124, 90, 58);
				dictionary["wood_dark"] = Rgb(88, 62, 40);
				dictionary["cloth"] = Rgb(120, 70, 50);
				dictionary["cloth_light"] = Rgb(186, 166, 132);
				dictionary["fur"] = Rgb(108, 82, 58);
				dictionary["fur_light"] = Rgb(168, 144, 108);
				dictionary["bark"] = Rgb(92, 66, 44);
				dictionary["metal"] = Rgb(100, 100, 106);
				dictionary["glow"] = Rgb(255, 170, 80);
				break;
			case "dwarf":
				dictionary["wood"] = Rgb(132, 92, 56);
				dictionary["wood_dark"] = Rgb(86, 58, 38);
				dictionary["cloth"] = TerrainCache.Lerp(roof, Rgb(140, 60, 40), 0.5f);
				dictionary["cloth_light"] = Rgb(220, 210, 190);
				dictionary["stone"] = Rgb(146, 140, 132);
				dictionary["metal"] = Rgb(126, 128, 136);
				dictionary["fur"] = Rgb(112, 84, 60);
				dictionary["glow"] = Rgb(255, 190, 110);
				dictionary["ceramic"] = Rgb(150, 96, 70);
				break;
			default:
				dictionary["wood"] = Rgb(150, 104, 62);
				dictionary["wood_dark"] = Rgb(98, 66, 42);
				dictionary["cloth"] = TerrainCache.Lerp(roof, Rgb(170, 40, 45), 0.45f);
				break;
			}
			return dictionary;
			uint Rgb(int r, int g, int b)
			{
				return TerrainCache.Rgb(r, g, b);
			}
		}

		private static void TableWithSeats(Layout L, string table, string seat, float x, float y, int seats, bool round)
		{
			if (!L.Put(table, x, y, -MathF.PI / 2f))
			{
				return;
			}
			if (round)
			{
				L.RoundLast();
			}
			float[] last = L.Last;
			float num = (last[2] - last[0]) * 0.5f;
			float num2 = (last[3] - last[1]) * 0.5f;
			float num3 = L.Depth(seat) * 0.5f;
			if (round)
			{
				for (int i = 0; i < seats; i++)
				{
					float num4 = MathF.PI / 2f + (float)i * 2f * MathF.PI / (float)seats + 0.35f;
					float num5 = num + num3 + 0.06f;
					float x2 = x + (float)Math.Cos(num4) * num5;
					float y2 = y + (float)Math.Sin(num4) * num5;
					if (!L.Put(seat, x2, y2, num4 + MathF.PI))
					{
						for (float num6 = 0.25f; num6 < 0.8f && !L.Put(seat, x + (float)Math.Cos(num4 + num6) * num5, y + (float)Math.Sin(num4 + num6) * num5, num4 + num6 + MathF.PI) && !L.Put(seat, x + (float)Math.Cos(num4 - num6) * num5, y + (float)Math.Sin(num4 - num6) * num5, num4 - num6 + MathF.PI); num6 += 0.25f)
						{
						}
					}
				}
				return;
			}
			List<(float, float, float)> list = new List<(float, float, float)>
			{
				(x, y + num2 + num3 + 0.04f, -MathF.PI / 2f),
				(x, y - num2 - num3 - 0.04f, MathF.PI / 2f),
				(x - num - num3 - 0.04f, y, 0f),
				(x + num + num3 + 0.04f, y, MathF.PI)
			};
			if (num > 0.6f)
			{
				list.Insert(2, (x - num * 0.5f, y - num2 - num3 - 0.04f, MathF.PI / 2f));
				list[1] = (x + num * 0.4f, y - num2 - num3 - 0.04f, MathF.PI / 2f);
			}
			int j = 0;
			int num7 = 0;
			for (; j < list.Count; j++)
			{
				if (num7 >= seats)
				{
					break;
				}
				if (L.Put(seat, list[j].Item1, list[j].Item2, list[j].Item3))
				{
					num7++;
				}
			}
		}

		private static void AtFootOfBed(Layout L, string chest, float[] bed, float bedX)
		{
			if (bed != null)
			{
				float num = L.Depth(chest);
				L.Put(chest, bedX, bed[1] - 0.06f - num * 0.5f, -MathF.PI / 2f);
			}
		}

		private static void Corner(Layout L, string name, bool east, bool north, float inset, float heading)
		{
			float x = (east ? (L.X1 - inset) : (L.X0 + inset));
			float y = (north ? (L.Y1 - inset) : (L.Y0 + inset));
			L.Put(name, x, y, heading);
		}

		private static void PlaceSpots(Interior room, Layout L)
		{
			float num = L.Base;
			List<(float, float, float, float, float, string)> list = new List<(float, float, float, float, float, string)>();
			foreach (var item2 in L.Placed)
			{
				float[] item = item2.Box;
				float num2 = (item[0] + item[2]) * 0.5f;
				float num3 = (item[1] + item[3]) * 0.5f;
				float num4 = (float)Math.Cos(item2.Heading);
				float num5 = (float)Math.Sin(item2.Heading);
				float num6 = ((Math.Abs(num4) > 0.5f) ? ((item[2] - item[0]) * 0.5f) : ((item[3] - item[1]) * 0.5f));
				switch (item2.Use)
				{
				case "seat":
					list.Add((item2.X, item2.Y, item2.X + num4, item2.Y + num5, 1f, item2.Use));
					break;
				case "table":
					list.Add((num2, item[1] - 0.55f, num2, num3, 0f, item2.Use));
					list.Add((num2, item[3] + 0.55f, num2, num3, 0f, item2.Use));
					break;
				case "hearth":
					list.Add((item2.X + num4 * (num6 + 0.55f) - num5 * 0.55f, item2.Y + num5 * (num6 + 0.55f) + num4 * 0.55f, item2.X, item2.Y, 0f, item2.Use));
					list.Add((item2.X + num4 * (num6 + 0.45f) + num5 * 0.6f, item2.Y + num5 * (num6 + 0.45f) - num4 * 0.6f, item2.X, item2.Y, 0f, item2.Use));
					break;
				case "anvil":
				case "shelf":
				case "counter":
				case "workbench":
				case "wardrobe":
					list.Add((item2.X + num4 * (num6 + 0.32f), item2.Y + num5 * (num6 + 0.32f), item2.X, item2.Y, 0f, item2.Use));
					break;
				case "bed":
					list.Add((item[2] + 0.35f, num3, item[2], num3, 0f, item2.Use));
					list.Add((item[0] - 0.35f, num3, item[0], num3, 0f, item2.Use));
					break;
				}
			}
			if (num < 0.01f)
			{
				list.Add((room.DoorX + 0.9f, room.DoorY + 0.6f, room.DoorX, room.DoorY + 2f, 0f, "door"));
				list.Add(((room.MinX + room.MaxX) * 0.5f, (room.MinY + room.MaxY) * 0.5f + 0.3f, room.DoorX, room.DoorY, 0f, "middle"));
			}
			foreach (var item3 in list)
			{
				if (item3.Item1 < room.MinX + 0.1f || item3.Item1 > room.MaxX - 0.1f || item3.Item2 < room.MinY + 0.1f || item3.Item2 > room.MaxY - 0.1f || (L.Fitted != null && !L.Fitted.Clear(L.Storey, item3.Item1, item3.Item2, 0.18f)))
				{
					continue;
				}
				bool flag = true;
				if (item3.Item5 <= 0f)
				{
					foreach (float[] solid in room.Solids)
					{
						bool num7;
						if (!(num > 0.01f))
						{
							num7 = solid.Length >= 6;
						}
						else
						{
							if (solid.Length < 6)
							{
								continue;
							}
							num7 = solid[4] > num + 0.3f;
						}
						if (!num7 && item3.Item1 > solid[0] - 0.2f && item3.Item1 < solid[2] + 0.2f && item3.Item2 > solid[1] - 0.2f && item3.Item2 < solid[3] + 0.2f)
						{
							flag = false;
							break;
						}
					}
				}
				if (!flag || (item3.Item1 - room.SpawnX) * (item3.Item1 - room.SpawnX) + (item3.Item2 - room.SpawnY) * (item3.Item2 - room.SpawnY) < 0.64000005f)
				{
					continue;
				}
				foreach (float[] spot in room.Spots)
				{
					if ((item3.Item1 - spot[0]) * (item3.Item1 - spot[0]) + (item3.Item2 - spot[1]) * (item3.Item2 - spot[1]) < ((item3.Item5 > 0f) ? 0.3f : 0.6f) * ((item3.Item5 > 0f) ? 0.3f : 0.6f))
					{
						flag = false;
						break;
					}
				}
				if (flag)
				{
					float num8 = (float)(Math.Atan2(item3.Item4 - item3.Item2, item3.Item3 - item3.Item1) * 180.0 / Math.PI);
					while (room.SpotUse.Count < room.Spots.Count)
					{
						room.SpotUse.Add("");
					}
					room.Spots.Add((!(num > 0.01f)) ? ((!(item3.Item5 > 0f)) ? new float[3] { item3.Item1, item3.Item2, num8 } : new float[4] { item3.Item1, item3.Item2, num8, 1f }) : new float[5]
					{
						item3.Item1,
						item3.Item2,
						num8,
						(item3.Item5 > 0f) ? 1f : 0f,
						num
					});
					room.SpotUse.Add(item3.Item6 ?? "");
				}
			}
		}

		private static Style StyleFor(string race, uint wallColor, uint roofColor)
		{
			uint timber = TerrainCache.Rgb(86, 58, 38);
			switch (race)
			{
			case "elf":
				return new Style
				{
					Wall = TerrainCache.Lerp(TerrainCache.Rgb(198, 194, 176), wallColor, 0.15f),
					Timber = TerrainCache.Rgb(160, 124, 84),
					Floor = TerrainCache.Rgb(164, 132, 92),
					Ceiling = TerrainCache.Rgb(196, 192, 176),
					Trim = TerrainCache.Rgb(138, 104, 68),
					Stone = TerrainCache.Rgb(186, 188, 182),
					Leaf = TerrainCache.Rgb(70, 140, 70),
					Bark = TerrainCache.Rgb(120, 92, 64),
					Door = TerrainCache.Rgb(168, 130, 86),
					Metal = TerrainCache.Rgb(196, 170, 110),
					Dark = TerrainCache.Rgb(46, 40, 34),
					CeilingKind = 13,
					WallH = 1.7f,
					WinW = 0.4f,
					WinZ0 = 0.55f,
					WinZ1 = 1.25f
				};
			case "orc":
				return new Style
				{
					Wall = TerrainCache.Rgb(150, 116, 82),
					Timber = TerrainCache.Rgb(92, 66, 44),
					Floor = TerrainCache.Rgb(118, 92, 66),
					Ceiling = TerrainCache.Rgb(128, 96, 66),
					Trim = TerrainCache.Rgb(84, 60, 40),
					Stone = TerrainCache.Rgb(110, 104, 98),
					Leaf = TerrainCache.Rgb(70, 110, 50),
					Bark = TerrainCache.Rgb(92, 66, 44),
					Door = TerrainCache.Rgb(104, 76, 50),
					Metal = TerrainCache.Rgb(104, 104, 110),
					Dark = TerrainCache.Rgb(40, 30, 24),
					WallKind = 7,
					CeilingKind = 7,
					WallH = 1.2f,
					FloorSurface = 9,
					MeshFloor = false,
					WinW = 0.3f,
					WinZ0 = 0.5f,
					WinZ1 = 0.8f
				};
			case "dwarf":
				return new Style
				{
					Wall = TerrainCache.Lerp(TerrainCache.Rgb(132, 126, 118), wallColor, 0.15f),
					Timber = timber,
					Floor = TerrainCache.Rgb(124, 120, 114),
					Ceiling = TerrainCache.Rgb(112, 78, 50),
					Trim = TerrainCache.Rgb(74, 50, 32),
					Stone = TerrainCache.Rgb(150, 144, 136),
					Leaf = TerrainCache.Rgb(70, 120, 60),
					Bark = TerrainCache.Rgb(96, 70, 48),
					Door = TerrainCache.Rgb(100, 66, 40),
					Metal = TerrainCache.Rgb(110, 112, 120),
					Dark = TerrainCache.Rgb(36, 30, 26),
					WallKind = 4,
					WallH = 1.45f,
					FloorSurface = 6,
					MeshFloor = false,
					WinW = 0.34f,
					WinZ0 = 0.68f,
					WinZ1 = 1.06f
				};
			default:
				return new Style
				{
					Wall = TerrainCache.Lerp(TerrainCache.Rgb(224, 210, 178), wallColor, 0.35f),
					Timber = timber,
					Floor = TerrainCache.Rgb(150, 104, 62),
					Ceiling = TerrainCache.Rgb(124, 86, 54),
					Trim = TerrainCache.Rgb(78, 52, 34),
					Stone = TerrainCache.Rgb(140, 136, 128),
					Leaf = TerrainCache.Rgb(70, 130, 60),
					Bark = TerrainCache.Rgb(96, 70, 48),
					Door = TerrainCache.Rgb(112, 74, 44),
					Metal = TerrainCache.Rgb(110, 112, 120),
					Dark = TerrainCache.Rgb(40, 32, 28)
				};
			}
		}

		private static MeshInstance BuildShell(Interior room, Style st, string race, float x0, float y0, float x1, float y1, Random rng, List<(char Wall, float At)> windows = null)
		{
			Shell shell = new Shell
			{
				S = st,
				X0 = x0,
				Y0 = y0,
				X1 = x1,
				Y1 = y1
			};
			MeshBuilder b = shell.B;
			shell.Wall = b.Slot("wall", st.WallKind, st.Wall, 0);
			shell.Timber = b.Slot("timber", 3, st.Timber, 0);
			shell.Floor = b.Slot("floor", (byte)((race == "dwarf") ? 14 : 3), st.Floor, 0);
			shell.Ceiling = b.Slot("ceiling", st.CeilingKind, st.Ceiling, 0);
			shell.Glass = b.Slot("glass", 12, TerrainCache.Rgb(200, 224, 246), 5);
			shell.Trim = b.Slot("trim", 3, st.Trim, 0);
			shell.Stone = b.Slot("stone", 4, st.Stone, 0);
			shell.Leaf = b.Slot("leaf", 1, st.Leaf, 0);
			shell.Bark = b.Slot("bark", 2, st.Bark, 0);
			shell.Door = b.Slot("door", 2, st.Door, 0);
			shell.Metal = b.Slot("metal", 10, st.Metal, 0);
			shell.Dark = b.Slot("dark", 0, st.Dark, 0);
			b.Part("room", -1, new Vec3(0f, 0f, 0f));
			float num = x1 - x0;
			float num2 = y1 - y0;
			shell.North.Start = new Vec3(x0, y1, 0f);
			shell.North.Along = new Vec3(1f, 0f, 0f);
			shell.North.Inward = new Vec3(0f, -1f, 0f);
			shell.North.Length = num;
			shell.South.Start = new Vec3(x1, y0, 0f);
			shell.South.Along = new Vec3(-1f, 0f, 0f);
			shell.South.Inward = new Vec3(0f, 1f, 0f);
			shell.South.Length = num;
			shell.West.Start = new Vec3(x0, y0, 0f);
			shell.West.Along = new Vec3(0f, 1f, 0f);
			shell.West.Inward = new Vec3(1f, 0f, 0f);
			shell.West.Length = num2;
			shell.East.Start = new Vec3(x1, y1, 0f);
			shell.East.Along = new Vec3(0f, -1f, 0f);
			shell.East.Inward = new Vec3(-1f, 0f, 0f);
			shell.East.Length = num2;
			float num3 = ((race == "dwarf") ? 0.66f : 0.72f);
			float z = ((race == "orc") ? 1.05f : ((race == "dwarf") ? 1.14f : 1.24f));
			shell.South.Holes.Add(new Hole
			{
				A0 = num / 2f - num3 / 2f,
				A1 = num / 2f + num3 / 2f,
				Z0 = 0f,
				Z1 = z,
				Door = true
			});
			float winW = st.WinW;
			if (windows != null)
			{
				foreach (var (c, num4) in windows)
				{
					switch (c)
					{
					case 'N':
						AddWindow(shell.North, num * num4, winW, st);
						break;
					case 'S':
						AddWindow(shell.South, num * (1f - num4), winW, st);
						break;
					case 'W':
						AddWindow(shell.West, num2 * num4, winW, st);
						break;
					default:
						AddWindow(shell.East, num2 * (1f - num4), winW, st);
						break;
					}
				}
			}
			else
			{
				AddWindow(shell.North, num * 0.62f, winW, st);
				if (num >= 7f)
				{
					AddWindow(shell.North, num * 0.86f, winW, st);
				}
				AddWindow(shell.West, num2 * 0.42f, winW, st);
				if (num2 >= 5f)
				{
					AddWindow(shell.East, num2 * 0.18f, winW, st);
				}
				if (num >= 6f)
				{
					AddWindow(shell.South, num * 0.2f, winW, st);
					AddWindow(shell.South, num * 0.8f, winW, st);
				}
			}
			WallSide[] all = shell.All;
			foreach (WallSide wallSide in all)
			{
				wallSide.Holes.Sort((Hole p, Hole q) => p.A0.CompareTo(q.A0));
				WallFace(shell, wallSide, st.WallH);
				foreach (Hole hole in wallSide.Holes)
				{
					Opening(shell, wallSide, hole, race);
					if (!hole.Door)
					{
						Vec3 right = wallSide.Along * -1f;
						room.Windows.Add(new LightWindow
						{
							Center = wallSide.At((hole.A0 + hole.A1) * 0.5f, (hole.Z0 + hole.Z1) * 0.5f, 0.08400001f),
							Right = right,
							HalfWidth = (hole.A1 - hole.A0) * 0.5f,
							HalfHeight = (hole.Z1 - hole.Z0) * 0.5f,
							Inward = wallSide.Inward
						});
					}
				}
			}
			if (st.MeshFloor)
			{
				b.Face(shell.Floor, new Vec3(x0, y0, 0f), new Vec3(x1, y0, 0f), new Vec3(x1, y1, 0f), new Vec3(x0, y1, 0f));
			}
			switch (race)
			{
			case "elf":
				ElfRoom(shell, room, rng);
				break;
			case "orc":
				OrcRoom(shell, room, rng);
				break;
			case "dwarf":
				DwarfRoom(shell, room);
				break;
			default:
				HumanRoom(shell, room);
				break;
			}
			MeshModel meshModel = shell.B.Build();
			uint[] array = new uint[meshModel.SlotNames.Length];
			for (int num5 = 0; num5 < array.Length; num5++)
			{
				array[num5] = meshModel.SlotColor[num5];
			}
			MeshInstance result = (room.Shell = new MeshInstance
			{
				Model = meshModel,
				Palette = array,
				NoShadow = true,
				Static = true,
				Tag = -1
			});
			room.GlassSlot = shell.Glass;
			TakeDoor(room, shell);
			return result;
		}

		private static void AddWindow(WallSide w, float at, float width, Style st)
		{
			float num = Math.Max(0.25f, at - width / 2f);
			float num2 = Math.Min(w.Length - 0.25f, at + width / 2f);
			foreach (Hole hole in w.Holes)
			{
				if (num2 > hole.A0 - 0.3f && num < hole.A1 + 0.3f)
				{
					return;
				}
			}
			w.Holes.Add(new Hole
			{
				A0 = num,
				A1 = num2,
				Z0 = st.WinZ0,
				Z1 = st.WinZ1
			});
		}

		private static void WallRect(Shell sh, WallSide w, int slot, float a0, float a1, float z0, float z1, float depth = 0f)
		{
			if (!(a1 - a0 < 0.0001f) && !(z1 - z0 < 0.0001f))
			{
				sh.B.Face(slot, w.At(a0, z0, depth), w.At(a1, z0, depth), w.At(a1, z1, depth), w.At(a0, z1, depth));
			}
		}

		private static void WallFace(Shell sh, WallSide w, float top)
		{
			float a = 0f;
			foreach (Hole hole in w.Holes)
			{
				WallRect(sh, w, sh.Wall, a, hole.A0, 0f, top);
				WallRect(sh, w, sh.Wall, hole.A0, hole.A1, 0f, hole.Z0);
				WallRect(sh, w, sh.Wall, hole.A0, hole.A1, hole.Z1, top);
				a = hole.A1;
			}
			WallRect(sh, w, sh.Wall, a, w.Length, 0f, top);
		}

		private static void WallBox(Shell sh, WallSide w, int slot, float a, float z, float halfAlong, float halfUp, float halfOut, float lift = 0f)
		{
			Vec3 c = w.At(a, z) + w.Inward * (halfOut + lift);
			sh.B.Box(slot, c, w.Along * halfAlong, new Vec3(0f, 0f, halfUp), w.Inward * halfOut);
		}

		private static void Band(Shell sh, WallSide w, int slot, float z, float halfUp, float halfOut, float lift = 0f)
		{
			List<Hole> list = new List<Hole>();
			foreach (Hole hole in w.Holes)
			{
				if (z + halfUp > hole.Z0 && z - halfUp < hole.Z1)
				{
					list.Add(hole);
				}
			}
			list.Sort((Hole p, Hole q) => p.A0.CompareTo(q.A0));
			float num = 0f;
			foreach (Hole item in list)
			{
				if (item.A0 - num > 0.01f)
				{
					WallBox(sh, w, slot, (num + item.A0) * 0.5f, z, (item.A0 - num) * 0.5f, halfUp, halfOut, lift);
				}
				num = Math.Max(num, item.A1);
			}
			if (w.Length - num > 0.01f)
			{
				WallBox(sh, w, slot, (num + w.Length) * 0.5f, z, (w.Length - num) * 0.5f, halfUp, halfOut, lift);
			}
		}

		private static bool InHole(WallSide w, float a, float margin = 0f)
		{
			foreach (Hole hole in w.Holes)
			{
				if (a > hole.A0 - margin && a < hole.A1 + margin)
				{
					return true;
				}
			}
			return false;
		}

		private static void Opening(Shell sh, WallSide w, Hole h, string race)
		{
			if (h.Door)
			{
				if (!h.Flap)
				{
					InsideDoor(sh, w, h, sh.Look ?? LookFor(race, null), race);
				}
				return;
			}
			float num = 0.14f;
			MeshBuilder b = sh.B;
			int slot = ((race == "dwarf") ? sh.Stone : ((race == "orc") ? sh.Timber : sh.Wall));
			b.Face(slot, w.At(h.A0, h.Z0), w.At(h.A0, h.Z0, num), w.At(h.A0, h.Z1, num), w.At(h.A0, h.Z1));
			b.Face(slot, w.At(h.A1, h.Z1), w.At(h.A1, h.Z1, num), w.At(h.A1, h.Z0, num), w.At(h.A1, h.Z0));
			if (!h.Door)
			{
				b.Face(slot, w.At(h.A0, h.Z0), w.At(h.A1, h.Z0), w.At(h.A1, h.Z0, num), w.At(h.A0, h.Z0, num));
			}
			b.Face(slot, w.At(h.A0, h.Z1, num), w.At(h.A1, h.Z1, num), w.At(h.A1, h.Z1), w.At(h.A0, h.Z1));
			float a = (h.A0 + h.A1) * 0.5f;
			float num2 = (h.A1 - h.A0) * 0.5f;
			int slot2 = ((race == "dwarf") ? sh.Stone : sh.Trim);
			float num3 = ((race == "dwarf") ? 0.07f : 0.045f);
			WallBox(sh, w, slot2, h.A0 - num3 * 0.5f, (h.Z0 + h.Z1) * 0.5f, num3 * 0.5f, (h.Z1 - h.Z0) * 0.5f + num3, 0.018f);
			WallBox(sh, w, slot2, h.A1 + num3 * 0.5f, (h.Z0 + h.Z1) * 0.5f, num3 * 0.5f, (h.Z1 - h.Z0) * 0.5f + num3, 0.018f);
			WallBox(sh, w, slot2, a, h.Z1 + num3 * 0.5f, num2 + num3, num3 * 0.5f * ((race == "dwarf") ? 1.6f : 1f), 0.02f);
			WallRect(sh, w, sh.Glass, h.A0, h.A1, h.Z0, h.Z1, num * 0.62f);
			WallBox(sh, w, sh.Trim, a, (h.Z0 + h.Z1) * 0.5f, 0.014f, (h.Z1 - h.Z0) * 0.5f, 0.012f, (0f - num) * 0.62f);
			WallBox(sh, w, sh.Trim, a, h.Z0 + (h.Z1 - h.Z0) * 0.62f, num2, 0.012f, 0.012f, (0f - num) * 0.62f);
			WallBox(sh, w, slot2, a, h.Z0 - 0.02f, num2 + num3 + 0.03f, 0.02f, 0.045f);
		}

		private static void Frame(Shell sh, float top, int slot, float every, bool skirting)
		{
			WallSide[] all = sh.All;
			foreach (WallSide wallSide in all)
			{
				List<float> list = new List<float>
				{
					0.06f,
					wallSide.Length - 0.06f
				};
				int num = Math.Max(1, (int)Math.Round(wallSide.Length / every));
				for (int j = 1; j < num; j++)
				{
					list.Add(wallSide.Length * (float)j / (float)num);
				}
				foreach (float item in list)
				{
					bool flag = true;
					foreach (Hole hole in wallSide.Holes)
					{
						if (item > hole.A0 - 0.12f && item < hole.A1 + 0.12f)
						{
							flag = false;
						}
					}
					if (flag)
					{
						WallBox(sh, wallSide, slot, item, top * 0.5f, 0.05f, top * 0.5f, 0.025f);
					}
				}
				WallBox(sh, wallSide, slot, wallSide.Length * 0.5f, top - 0.05f, wallSide.Length * 0.5f, 0.05f, 0.035f);
				if (!skirting)
				{
					continue;
				}
				float num2 = 0f;
				foreach (Hole hole2 in wallSide.Holes)
				{
					if (hole2.Door)
					{
						if (hole2.A0 - num2 > 0.01f)
						{
							WallBox(sh, wallSide, slot, (num2 + hole2.A0) * 0.5f, 0.04f, (hole2.A0 - num2) * 0.5f, 0.04f, 0.015f);
						}
						num2 = hole2.A1;
					}
				}
				if (wallSide.Length - num2 > 0.01f)
				{
					WallBox(sh, wallSide, slot, (num2 + wallSide.Length) * 0.5f, 0.04f, (wallSide.Length - num2) * 0.5f, 0.04f, 0.015f);
				}
			}
		}

		private static void Beam(MeshBuilder b, int slot, Vec3 a, Vec3 c, float r)
		{
			Vec3 vec = c - a;
			float num = (float)Math.Sqrt(Vec3.Dot(vec, vec));
			if (!(num < 0.0001f))
			{
				Vec3 vec2 = vec * (1f / num);
				Vec3 vec3 = ((Math.Abs(vec2.Z) < 0.95f) ? Vec3.Cross(vec2, new Vec3(0f, 0f, 1f)).Normalized() : new Vec3(1f, 0f, 0f));
				Vec3 vec4 = Vec3.Cross(vec3, vec2);
				b.Box(slot, (a + c) * 0.5f, vec3 * r, vec2 * (num * 0.5f), vec4 * r);
			}
		}

		private static void HumanRoom(Shell sh, Interior room)
		{
			Style s = sh.S;
			Frame(sh, s.WallH, sh.Timber, 1.5f, skirting: true);
			float x = sh.X0;
			float x2 = sh.X1;
			float y = sh.Y0;
			float y2 = sh.Y1;
			float y3 = (y + y2) * 0.5f;
			float num = x2 - x;
			float num2 = y2 - y;
			float wallH = s.WallH;
			float num3 = num2 * 0.32f;
			float num4 = wallH + num3;
			MeshBuilder b = sh.B;
			b.Face(sh.Ceiling, new Vec3(x2, y, wallH), new Vec3(x, y, wallH), new Vec3(x, y3, num4), new Vec3(x2, y3, num4));
			b.Face(sh.Ceiling, new Vec3(x, y2, wallH), new Vec3(x2, y2, wallH), new Vec3(x2, y3, num4), new Vec3(x, y3, num4));
			b.Face(sh.Wall, new Vec3(x, y, wallH), new Vec3(x, y2, wallH), new Vec3(x, y3, num4));
			b.Face(sh.Wall, new Vec3(x2, y2, wallH), new Vec3(x2, y, wallH), new Vec3(x2, y3, num4));
			int num5 = Math.Max(3, (int)Math.Round(num / 0.5f));
			for (int i = 0; i <= num5; i++)
			{
				float x3 = x + 0.05f + (num - 0.1f) * (float)i / (float)num5;
				Beam(b, sh.Timber, new Vec3(x3, y + 0.02f, wallH - 0.06f), new Vec3(x3, y3, num4 - 0.06f), 0.035f);
				Beam(b, sh.Timber, new Vec3(x3, y2 - 0.02f, wallH - 0.06f), new Vec3(x3, y3, num4 - 0.06f), 0.035f);
			}
			Beam(b, sh.Timber, new Vec3(x, y3, num4 - 0.08f), new Vec3(x2, y3, num4 - 0.08f), 0.06f);
			int num6 = Math.Max(1, (int)Math.Round(num / 1.8f));
			for (int j = 1; j <= num6; j++)
			{
				float num7 = x + num * (float)j / (float)(num6 + 1);
				Beam(b, sh.Timber, new Vec3(num7, y, wallH - 0.08f), new Vec3(num7, y2, wallH - 0.08f), 0.06f);
				Beam(b, sh.Timber, new Vec3(num7, y3, wallH - 0.02f), new Vec3(num7, y3, num4 - 0.1f), 0.045f);
				Beam(b, sh.Timber, new Vec3(num7, y3, wallH + num3 * 0.4f), new Vec3(num7 - 0.35f, y3, num4 - 0.12f), 0.03f);
				Beam(b, sh.Timber, new Vec3(num7, y3, wallH + num3 * 0.4f), new Vec3(num7 + 0.35f, y3, num4 - 0.12f), 0.03f);
			}
			room.Top = num4;
		}

		private static void DwarfRoom(Shell sh, Interior room)
		{
			Style s = sh.S;
			float x = sh.X0;
			float x2 = sh.X1;
			float y = sh.Y0;
			float y2 = sh.Y1;
			float num = x2 - x;
			float wallH = s.WallH;
			MeshBuilder b = sh.B;
			b.Face(sh.Ceiling, new Vec3(x, y, wallH), new Vec3(x, y2, wallH), new Vec3(x2, y2, wallH), new Vec3(x2, y, wallH));
			int num2 = Math.Max(2, (int)Math.Round(num / 1.1f));
			for (int i = 0; i <= num2; i++)
			{
				float x3 = x + 0.1f + (num - 0.2f) * (float)i / (float)num2;
				Beam(b, sh.Timber, new Vec3(x3, y, wallH - 0.09f), new Vec3(x3, y2, wallH - 0.09f), 0.085f);
			}
			Beam(b, sh.Timber, new Vec3(x, y + 0.08f, wallH - 0.07f), new Vec3(x2, y + 0.08f, wallH - 0.07f), 0.07f);
			Beam(b, sh.Timber, new Vec3(x, y2 - 0.08f, wallH - 0.07f), new Vec3(x2, y2 - 0.08f, wallH - 0.07f), 0.07f);
			WallSide[] all = sh.All;
			foreach (WallSide wallSide in all)
			{
				WallBox(sh, wallSide, sh.Stone, 0.12f, wallH * 0.5f, 0.12f, wallH * 0.5f, 0.05f);
				WallBox(sh, wallSide, sh.Stone, wallSide.Length - 0.12f, wallH * 0.5f, 0.12f, wallH * 0.5f, 0.05f);
				float num3 = 0f;
				foreach (Hole hole in wallSide.Holes)
				{
					if (hole.Door)
					{
						if (hole.A0 - num3 > 0.01f)
						{
							WallBox(sh, wallSide, sh.Stone, (num3 + hole.A0) * 0.5f, 0.07f, (hole.A0 - num3) * 0.5f, 0.07f, 0.03f);
						}
						num3 = hole.A1;
					}
				}
				if (wallSide.Length - num3 > 0.01f)
				{
					WallBox(sh, wallSide, sh.Stone, (num3 + wallSide.Length) * 0.5f, 0.07f, (wallSide.Length - num3) * 0.5f, 0.07f, 0.03f);
				}
			}
			int[] array = new int[3]
			{
				sh.Floor,
				b.Slot("flag2", 14, TerrainCache.Scale(s.Floor, 0.9f), 0),
				b.Slot("flag3", 14, TerrainCache.Scale(s.Floor, 1.08f), 0)
			};
			Random random = new Random(17);
			float num4 = y;
			float num5 = 0f;
			while (num4 < y2 - 0.05f)
			{
				float num6 = Math.Min(y2 - num4, 0.42f + (float)random.NextDouble() * 0.14f);
				float num8;
				for (float num7 = x - num5 % 2f * 0.22f; num7 < x2 - 0.05f; num7 += num8)
				{
					num8 = 0.4f + (float)random.NextDouble() * 0.3f;
					float num9 = Math.Max(x, num7) + 0.012f;
					float num10 = Math.Min(x2, num7 + num8) - 0.012f;
					float z = 0.008f + (float)random.NextDouble() * 0.006f;
					if (num10 - num9 > 0.05f)
					{
						b.Face(array[random.Next(3)], new Vec3(num9, num4 + 0.012f, z), new Vec3(num10, num4 + 0.012f, z), new Vec3(num10, num4 + num6 - 0.012f, z), new Vec3(num9, num4 + num6 - 0.012f, z));
					}
				}
				num4 += num6;
				num5 += 1f;
			}
			room.Top = wallH;
		}

		private static void ElfRoom(Shell sh, Interior room, Random rng)
		{
			Style s = sh.S;
			float x = sh.X0;
			float x2 = sh.X1;
			float y = sh.Y0;
			float y2 = sh.Y1;
			float num = (y + y2) * 0.5f;
			float num2 = x2 - x;
			float num3 = y2 - y;
			float wallH = s.WallH;
			MeshBuilder b = sh.B;
			float num4 = num3 * 0.3f;
			Vec3[] array = new Vec3[11];
			for (int i = 0; i <= 10; i++)
			{
				float num5 = MathF.PI * (float)i / 10f;
				array[i] = new Vec3(0f, num - num3 * 0.5f * (float)Math.Cos(num5), wallH + num4 * (float)Math.Sin(num5));
			}
			for (int j = 0; j < 10; j++)
			{
				Vec3 vec = array[j];
				Vec3 vec2 = array[j + 1];
				b.Face(sh.Ceiling, new Vec3(x2, vec.Y, vec.Z), new Vec3(x, vec.Y, vec.Z), new Vec3(x, vec2.Y, vec2.Z), new Vec3(x2, vec2.Y, vec2.Z));
				b.Face(sh.Ceiling, new Vec3(x, num, wallH), new Vec3(x, vec2.Y, vec2.Z), new Vec3(x, vec.Y, vec.Z));
				b.Face(sh.Ceiling, new Vec3(x2, num, wallH), new Vec3(x2, vec.Y, vec.Z), new Vec3(x2, vec2.Y, vec2.Z));
			}
			int num6 = Math.Max(2, (int)Math.Round(num2 / 1.3f));
			WallSide[] array2;
			for (int k = 0; k <= num6; k++)
			{
				float x3 = x + 0.06f + (num2 - 0.12f) * (float)k / (float)num6;
				array2 = new WallSide[2] { sh.North, sh.South };
				foreach (WallSide wallSide in array2)
				{
					float num7 = Vec3.Dot(new Vec3(x3, 0f, 0f) - wallSide.Start, wallSide.Along);
					bool flag = true;
					foreach (Hole hole in wallSide.Holes)
					{
						if (num7 > hole.A0 - 0.1f && num7 < hole.A1 + 0.1f)
						{
							flag = false;
						}
					}
					if (flag)
					{
						WallBox(sh, wallSide, sh.Timber, num7, wallH * 0.5f, 0.035f, wallH * 0.5f, 0.03f);
					}
				}
				for (int m = 0; m < 10; m++)
				{
					Vec3 vec3 = array[m];
					Vec3 vec4 = array[m + 1];
					Vec3 vec5 = new Vec3(x3, num, wallH);
					Vec3 vec6 = new Vec3(x3, vec3.Y, vec3.Z);
					Vec3 vec7 = new Vec3(x3, vec4.Y, vec4.Z);
					vec6 += (vec5 - vec6).Normalized() * 0.04f;
					vec7 += (vec5 - vec7).Normalized() * 0.04f;
					b.Tube(sh.Timber, vec6, vec7, 0.035f, 0.035f, 6);
				}
				if (k % 2 != 0)
				{
					continue;
				}
				int num8 = rng.Next(2);
				int num9 = 10 - rng.Next(3);
				for (int n = num8; n < num9; n++)
				{
					Vec3 vec8 = array[n];
					Vec3 vec9 = array[n + 1];
					for (int num10 = 0; num10 < 2; num10++)
					{
						Vec3 vec10 = vec8 + (vec9 - vec8) * (0.25f + 0.5f * (float)num10);
						Vec3 vec11 = new Vec3(x3, num, wallH);
						Vec3 vec12 = new Vec3(x3, vec10.Y, vec10.Z);
						Vec3 c = vec12 + (vec11 - vec12).Normalized() * 0.07f + new Vec3((float)(rng.NextDouble() - 0.5) * 0.06f, 0f, 0f);
						b.Blob(sh.Leaf, c, 0.075f, 0.075f, 0.055f, 6);
					}
				}
			}
			array2 = sh.All;
			foreach (WallSide wallSide2 in array2)
			{
				Vec3 a = wallSide2.At(0.1f, wallH - 0.07f) + wallSide2.Inward * 0.05f;
				for (float num11 = 0.3f; num11 < wallSide2.Length - 0.05f; num11 += 0.2f)
				{
					float num12 = 0.035f * (float)Math.Sin(num11 * 4.1f);
					Vec3 vec13 = wallSide2.At(num11, wallH - 0.07f - num12) + wallSide2.Inward * 0.05f;
					b.Tube(sh.Bark, a, vec13, 0.012f, 0.012f, 4);
					b.Blob(sh.Leaf, vec13 + new Vec3(0f, 0f, -0.02f), 0.07f, 0.06f, 0.055f, 6);
					a = vec13;
				}
			}
			room.Top = wallH + num4;
		}

		private static void OrcRoom(Shell sh, Interior room, Random rng)
		{
			Style s = sh.S;
			float x = sh.X0;
			float x2 = sh.X1;
			float y = sh.Y0;
			float y2 = sh.Y1;
			float y3 = (y + y2) * 0.5f;
			float num = x2 - x;
			float num2 = y2 - y;
			float wallH = s.WallH;
			MeshBuilder b = sh.B;
			float num3 = num2 * 0.4f + 0.3f;
			float num4 = wallH + num3;
			b.Face(sh.Ceiling, new Vec3(x2, y, wallH), new Vec3(x, y, wallH), new Vec3(x, y3, num4), new Vec3(x2, y3, num4));
			b.Face(sh.Ceiling, new Vec3(x, y2, wallH), new Vec3(x2, y2, wallH), new Vec3(x2, y3, num4), new Vec3(x, y3, num4));
			b.Face(sh.Wall, new Vec3(x, y, wallH), new Vec3(x, y2, wallH), new Vec3(x, y3, num4));
			b.Face(sh.Wall, new Vec3(x2, y2, wallH), new Vec3(x2, y, wallH), new Vec3(x2, y3, num4));
			WallSide[] all = sh.All;
			foreach (WallSide wallSide in all)
			{
				int num5 = Math.Max(2, (int)Math.Round(wallSide.Length / 0.9f));
				for (int j = 0; j <= num5; j++)
				{
					float num6 = 0.05f + (wallSide.Length - 0.1f) * (float)j / (float)num5;
					bool flag = true;
					foreach (Hole hole in wallSide.Holes)
					{
						if (num6 > hole.A0 - 0.08f && num6 < hole.A1 + 0.08f)
						{
							flag = false;
						}
					}
					if (flag)
					{
						Vec3 vec = wallSide.At(num6, 0f) + wallSide.Inward * 0.05f;
						b.Tube(sh.Bark, vec, vec + new Vec3(0f, 0f, wallH + 0.05f), 0.05f, 0.042f, 6);
						float a = num6 + (wallSide.Length - 0.1f) / (float)num5 * 0.5f;
						if (j < num5 && !InHole(wallSide, a, 0.02f))
						{
							WallBox(sh, wallSide, sh.Dark, a, wallH * 0.5f, 0.006f, wallH * 0.5f - 0.04f, 0.004f);
						}
					}
				}
				WallBox(sh, wallSide, sh.Bark, wallSide.Length * 0.5f, wallH - 0.03f, wallSide.Length * 0.5f, 0.035f, 0.035f);
			}
			int num7 = Math.Max(3, (int)Math.Round(num / 0.7f));
			for (int k = 0; k <= num7; k++)
			{
				float x3 = x + 0.05f + (num - 0.1f) * (float)k / (float)num7;
				b.Tube(sh.Bark, new Vec3(x3, y + 0.04f, wallH - 0.04f), new Vec3(x3, y3, num4 - 0.07f), 0.04f, 0.035f, 5);
				b.Tube(sh.Bark, new Vec3(x3, y2 - 0.04f, wallH - 0.04f), new Vec3(x3, y3, num4 - 0.07f), 0.04f, 0.035f, 5);
			}
			b.Tube(sh.Bark, new Vec3(x - 0.05f, y3, num4 - 0.1f), new Vec3(x2 + 0.05f, y3, num4 - 0.1f), 0.07f, 0.065f, 6);
			int leaf = b.Slot("herb_light", 1, TerrainCache.Scale(s.Leaf, 1.35f), 0);
			for (int l = 0; l < 3; l++)
			{
				float x4 = x + num * (0.25f + 0.25f * (float)l);
				b.Tube(sh.Dark, new Vec3(x4, y3, num4 - 0.15f), new Vec3(x4, y3, num4 - 0.45f), 0.006f, 0.006f, 3);
				HerbBundle(b, sh.Bark, sh.Leaf, leaf, sh.Dark, new Vec3(x4, y3, num4 - 0.45f), 0.16f, rng);
			}
			room.Top = num4;
		}

		private static string KeyPiece(string style)
		{
			switch (style)
			{
			case "human_stone":
				return "int_v_inglenook";
			case "human_timber":
				return "int_v_loft";
			case "elf_spire":
				return "int_v_shrine";
			case "elf_pavilion":
				return "int_v_fountain";
			case "orc_lodge":
				return "int_v_bigforge";
			case "orc_skull":
				return "int_v_skullpole";
			case "dwarf_lodge":
				return "int_v_hoodhearth";
			case "dwarf_stump":
				return "int_v_spiral";
			default:
				if (style.StartsWith("tent", StringComparison.Ordinal))
				{
					return "int_v_bedroll";
				}
				goto case null;
			case null:
				return null;
			}
		}

		public static bool HasVariantRoom(string style, Func<string, MeshModel> models)
		{
			string text = KeyPiece(style);
			if (text != null && models != null && models(text) != null)
			{
				return models("int_barrel") != null;
			}
			return false;
		}

		private static Interior BuildVariant(int roomW, int roomD, uint wallColor, uint roofColor, int seed, string race, int tier, Func<string, MeshModel> models, string style)
		{
			Random rng = new Random(seed);
			race = Known(race);
			switch (style)
			{
			case "human_stone":
				return StoneCottage(roomW, roomD, wallColor, roofColor, rng, tier, models);
			case "human_timber":
				return TimberHouse(roomW, roomD + 1, wallColor, roofColor, rng, tier, models);
			case "elf_spire":
				return SpireHall(Math.Max(5, roomW - 1), roomD + 2, wallColor, roofColor, rng, tier, models);
			case "elf_pavilion":
				return Pavilion(Math.Max(roomW, roomD) + 1, wallColor, roofColor, rng, tier, models);
			case "orc_lodge":
				return Smithy(roomW + 1, roomD, wallColor, roofColor, rng, tier, models);
			case "orc_skull":
				return SkullHut(Math.Max(roomW, roomD), wallColor, roofColor, rng, tier, models);
			case "dwarf_lodge":
				return GreatHall(Math.Max(5, roomW - 1), roomD + 2, wallColor, roofColor, rng, tier, models);
			case "dwarf_stump":
				return StumpRoom(5 + tier / 2, wallColor, roofColor, rng, tier, models);
			default:
				return TentRoom(style, wallColor, roofColor, rng, models);
			}
		}

		private static Interior Begin(int roomW, int roomD, Style st, string race, int tier, out float x0, out float y0, out float x1, out float y1, uint floorColor = 0u)
		{
			roomW = Math.Max(4, roomW);
			roomD = Math.Max(3, roomD);
			Interior interior = new Interior
			{
				Race = race,
				Tier = tier
			};
			x0 = 1f;
			y0 = 1f;
			x1 = 1f + (float)roomW;
			y1 = 1f + (float)roomD;
			int num = roomW + 2;
			int num2 = roomD + 2;
			TerrainCache terrainCache = new TerrainCache();
			terrainCache.Allocate(num, num2);
			terrainCache.CastShadows = false;
			terrainCache.DitherEdges = false;
			terrainCache.OutsideIsWater = false;
			terrainCache.OutsideHeight = st.WallH;
			terrainCache.OutsideColor = TerrainCache.Scale(st.Wall, 0.7f);
			uint c = ((floorColor != 0) ? floorColor : st.Floor);
			for (int i = 0; i < num2; i++)
			{
				for (int j = 0; j < num; j++)
				{
					int num3 = i * num + j;
					terrainCache.BaseColors[num3] = (st.MeshFloor ? TerrainCache.Scale(c, 0.45f) : TerrainCache.Scale(c, 0.95f + (float)(TerrainModel.Hash(j, i * 7) & 7) / 160f));
					terrainCache.Materials[num3] = st.FloorSurface;
					if (st.MeshFloor)
					{
						terrainCache.Heights[num3] = (terrainCache.Ground[num3] = -0.03f);
					}
				}
			}
			terrainCache.Relight(0, 0, num, num2);
			interior.Floor = terrainCache;
			interior.CeilingTop = TerrainCache.Scale(st.Ceiling, 0.5f);
			interior.CeilingBottom = TerrainCache.Scale(st.Ceiling, 0.65f);
			interior.MinX = x0 + 0.25f;
			interior.MaxX = x1 - 0.25f;
			interior.MinY = y0 + 0.25f;
			interior.MaxY = y1 - 0.25f;
			interior.DoorX = (x0 + x1) * 0.5f;
			interior.DoorY = y0 + 0.45f;
			interior.SpawnX = interior.DoorX;
			interior.SpawnY = y0 + 0.75f;
			interior.SpawnYaw = MathF.PI / 2f;
			interior.HearthX = (x0 + x1) * 0.5f;
			interior.HearthY = (y0 + y1) * 0.5f;
			return interior;
		}

		private static Shell NewShell(Style st, string race, float x0, float y0, float x1, float y1, out int gold, out int cloth, out int water, out int red)
		{
			Shell obj = new Shell
			{
				S = st,
				X0 = x0,
				Y0 = y0,
				X1 = x1,
				Y1 = y1
			};
			MeshBuilder b = obj.B;
			obj.Wall = b.Slot("wall", st.WallKind, st.Wall, 0);
			obj.Timber = b.Slot("timber", 3, st.Timber, 0);
			obj.Floor = b.Slot("floor", (byte)((race == "dwarf") ? 14 : 3), st.Floor, 0);
			obj.Ceiling = b.Slot("ceiling", st.CeilingKind, st.Ceiling, 0);
			obj.Glass = b.Slot("glass", 12, TerrainCache.Rgb(200, 224, 246), 5);
			obj.Trim = b.Slot("trim", 3, st.Trim, 0);
			obj.Stone = b.Slot("stone", 4, st.Stone, 0);
			obj.Leaf = b.Slot("leaf", 1, st.Leaf, 0);
			obj.Bark = b.Slot("bark", 2, st.Bark, 0);
			obj.Door = b.Slot("door", 2, st.Door, 0);
			obj.Metal = b.Slot("metal", 10, st.Metal, 0);
			obj.Dark = b.Slot("dark", 0, st.Dark, 0);
			gold = b.Slot("gold", 10, TerrainCache.Rgb(214, 178, 90), 0);
			cloth = b.Slot("cloth", 7, TerrainCache.Rgb(222, 210, 186), 0);
			water = b.Slot("water", 12, TerrainCache.Rgb(120, 170, 200), 0);
			red = b.Slot("red", 7, TerrainCache.Rgb(150, 36, 32), 0);
			b.Part("room", -1, new Vec3(0f, 0f, 0f));
			float length = x1 - x0;
			float length2 = y1 - y0;
			obj.North.Start = new Vec3(x0, y1, 0f);
			obj.North.Along = new Vec3(1f, 0f, 0f);
			obj.North.Inward = new Vec3(0f, -1f, 0f);
			obj.North.Length = length;
			obj.South.Start = new Vec3(x1, y0, 0f);
			obj.South.Along = new Vec3(-1f, 0f, 0f);
			obj.South.Inward = new Vec3(0f, 1f, 0f);
			obj.South.Length = length;
			obj.West.Start = new Vec3(x0, y0, 0f);
			obj.West.Along = new Vec3(0f, 1f, 0f);
			obj.West.Inward = new Vec3(1f, 0f, 0f);
			obj.West.Length = length2;
			obj.East.Start = new Vec3(x1, y1, 0f);
			obj.East.Along = new Vec3(0f, -1f, 0f);
			obj.East.Inward = new Vec3(-1f, 0f, 0f);
			obj.East.Length = length2;
			return obj;
		}

		private static void BuildWalls(Shell sh, Interior room, IEnumerable<WallSide> walls, string race, float top)
		{
			foreach (WallSide wall in walls)
			{
				wall.Holes.Sort((Hole p, Hole q) => p.A0.CompareTo(q.A0));
				WallFace(sh, wall, top);
				foreach (Hole hole in wall.Holes)
				{
					Opening(sh, wall, hole, race);
					if (!hole.Door)
					{
						AddDaylight(room, wall, hole);
					}
				}
			}
		}

		private static void AddDaylight(Interior room, WallSide w, Hole h)
		{
			room.Windows.Add(new LightWindow
			{
				Center = w.At((h.A0 + h.A1) * 0.5f, (h.Z0 + h.Z1) * 0.5f, 0.08400001f),
				Right = w.Along * -1f,
				HalfWidth = (h.A1 - h.A0) * 0.5f,
				HalfHeight = (h.Z1 - h.Z0) * 0.5f,
				Inward = w.Inward
			});
		}

		private static void AddDoor(Shell sh, WallSide w, string race, string style, float maxH = 9f)
		{
			float num = ((race == "dwarf") ? 0.66f : 0.72f);
			float val = ((race == "orc") ? 1.05f : ((race == "dwarf") ? 1.14f : 1.24f));
			w.Holes.Add(new Hole
			{
				A0 = w.Length / 2f - num / 2f,
				A1 = w.Length / 2f + num / 2f,
				Z0 = 0f,
				Z1 = Math.Min(val, maxH),
				Door = true
			});
			sh.Look = LookFor(race, style);
		}

		private static MeshInstance Finish(Shell sh, Interior room)
		{
			MeshModel meshModel = sh.B.Build();
			uint[] array = new uint[meshModel.SlotNames.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = meshModel.SlotColor[i];
			}
			MeshInstance meshInstance = (room.Shell = new MeshInstance
			{
				Model = meshModel,
				Palette = array,
				NoShadow = true,
				Static = true,
				Tag = -1
			});
			room.GlassSlot = sh.Glass;
			TakeDoor(room, sh);
			room.Meshes.Add(meshInstance);
			return meshInstance;
		}

		private static void FaceToward(MeshBuilder b, int slot, Vec3 toward, params Vec3[] p)
		{
			Vec3 a = Vec3.Cross(p[1] - p[0], p[2] - p[0]);
			Vec3 vec = new Vec3(0f, 0f, 0f);
			foreach (Vec3 vec2 in p)
			{
				vec += vec2;
			}
			vec *= 1f / (float)p.Length;
			if (Vec3.Dot(a, toward - vec) < 0f)
			{
				Array.Reverse(p);
			}
			b.Face(slot, p);
		}

		private static void Flagstones(MeshBuilder b, int a, int c, int d, float x0, float y0, float x1, float y1, int seed)
		{
			int[] array = new int[3] { a, c, d };
			Random random = new Random(seed);
			float num = y0;
			float num2 = 0f;
			while (num < y1 - 0.05f)
			{
				float num3 = Math.Min(y1 - num, 0.4f + (float)random.NextDouble() * 0.16f);
				float num5;
				for (float num4 = x0 - num2 % 2f * 0.22f; num4 < x1 - 0.05f; num4 += num5)
				{
					num5 = 0.38f + (float)random.NextDouble() * 0.32f;
					float num6 = Math.Max(x0, num4) + 0.012f;
					float num7 = Math.Min(x1, num4 + num5) - 0.012f;
					float z = 0.008f + (float)random.NextDouble() * 0.006f;
					if (num7 - num6 > 0.05f)
					{
						b.Face(array[random.Next(3)], new Vec3(num6, num + 0.012f, z), new Vec3(num7, num + 0.012f, z), new Vec3(num7, num + num3 - 0.012f, z), new Vec3(num6, num + num3 - 0.012f, z));
					}
				}
				num += num3;
				num2 += 1f;
			}
		}

		private static void BeamCeiling(Shell sh, float H, float every, float r)
		{
			MeshBuilder b = sh.B;
			float x = sh.X0;
			float x2 = sh.X1;
			float y = sh.Y0;
			float y2 = sh.Y1;
			float num = x2 - x;
			b.Face(sh.Ceiling, new Vec3(x, y, H), new Vec3(x, y2, H), new Vec3(x2, y2, H), new Vec3(x2, y, H));
			int num2 = Math.Max(2, (int)Math.Round(num / every));
			for (int i = 0; i <= num2; i++)
			{
				float x3 = x + 0.1f + (num - 0.2f) * (float)i / (float)num2;
				Beam(b, sh.Timber, new Vec3(x3, y, H - r), new Vec3(x3, y2, H - r), r);
			}
			Beam(b, sh.Timber, new Vec3(x, y + 0.08f, H - 0.07f), new Vec3(x2, y + 0.08f, H - 0.07f), 0.07f);
			Beam(b, sh.Timber, new Vec3(x, y2 - 0.08f, H - 0.07f), new Vec3(x2, y2 - 0.08f, H - 0.07f), 0.07f);
		}

		private static void Herbs(Shell sh, Vec3 a, Vec3 b, int n, Random rng)
		{
			MeshBuilder b2 = sh.B;
			int leaf = b2.Slot("herb_light", 1, TerrainCache.Scale(sh.S.Leaf, 1.35f), 0);
			int num = b2.Slot("onion", 0, TerrainCache.Rgb(184, 112, 58), 0);
			int num2 = b2.Slot("garlic", 0, TerrainCache.Rgb(226, 218, 196), 0);
			for (int i = 0; i < n; i++)
			{
				Vec3 vec = a + (b - a) * (((float)i + 0.5f) / (float)n);
				float z = 0.04f + 0.04f * (float)rng.NextDouble();
				Vec3 vec2 = vec - new Vec3(0f, 0f, z);
				b2.Tube(sh.Dark, vec, vec2, 0.004f, 0.004f, 3);
				if (i % 3 == 1)
				{
					Strung(b2, sh.Bark, (i % 2 == 0) ? num2 : num, vec2, 4);
				}
				else
				{
					HerbBundle(b2, sh.Bark, sh.Leaf, leaf, sh.Dark, vec2, 0.15f, rng);
				}
			}
		}

		private static void HerbBundle(MeshBuilder b, int stem, int leaf, int leaf2, int tie, Vec3 top, float length, Random rng)
		{
			Vec3 vec = top - new Vec3(0f, 0f, 0.018f);
			b.Tube(tie, top, vec, 0.009f, 0.009f, 5);
			for (int i = 0; i < 6; i++)
			{
				float num = (float)i * 1.047f + (float)(rng.NextDouble() - 0.5) * 0.5f;
				float num2 = 0.016f + 0.016f * (float)rng.NextDouble();
				Vec3 vec2 = vec + new Vec3((float)Math.Cos(num) * num2, (float)Math.Sin(num) * num2, (0f - length) * (0.75f + 0.25f * (float)rng.NextDouble()));
				b.Tube(stem, vec, vec2, 0.003f, 0.002f, 3);
				for (int j = 1; j <= 3; j++)
				{
					float num3 = 0.008f + 0.004f * (float)rng.NextDouble();
					b.Blob(((i + j) % 2 == 0) ? leaf : leaf2, vec + (vec2 - vec) * (0.3f + 0.23f * (float)j), num3, num3, num3 * 1.6f, 5);
				}
			}
		}

		private static void Strung(MeshBuilder b, int plait, int bulb, Vec3 top, int n)
		{
			b.Tube(plait, top, top - new Vec3(0f, 0f, 0.035f * (float)n + 0.02f), 0.004f, 0.004f, 3);
			for (int i = 0; i < n; i++)
			{
				float num = ((i % 2 == 0) ? 1f : (-1f));
				Vec3 vec = top - new Vec3((0f - num) * 0.014f, 0f, 0.03f + (float)i * 0.035f);
				b.Blob(bulb, vec, 0.018f, 0.018f, 0.016f, 6);
				b.Tube(bulb, vec + new Vec3(0f, 0f, 0.012f), vec + new Vec3(num * 0.008f, 0f, 0.03f), 0.006f, 0.002f, 3);
			}
		}

		private static void PointedTop(Shell sh, WallSide w, Hole h, int frameSlot)
		{
			float a = (h.A0 + h.A1) * 0.5f;
			float num = (h.A1 - h.A0) * 0.5f * 1.5f;
			float depth = 0.0868f;
			MeshBuilder b = sh.B;
			Vec3 vec = w.At(h.A0, h.Z1, depth);
			Vec3 vec2 = w.At(h.A1, h.Z1, depth);
			Vec3 vec3 = w.At(a, h.Z1 + num, depth);
			FaceToward(b, sh.Glass, w.At(a, h.Z1, 1f) + w.Inward * 2f, vec, vec2, vec3);
			Vec3 vec4 = w.At(h.A0, h.Z1, -0.012f);
			Vec3 vec5 = w.At(h.A1, h.Z1, -0.012f);
			Vec3 vec6 = w.At(a, h.Z1 + num, -0.012f);
			FaceToward(b, sh.Glass, w.At(a, h.Z1) + w.Inward * 2f, vec4, vec5, vec6);
			Beam(b, frameSlot, w.At(h.A0 - 0.03f, h.Z1, -0.02f), w.At(a, h.Z1 + num + 0.03f, -0.02f), 0.025f);
			Beam(b, frameSlot, w.At(h.A1 + 0.03f, h.Z1, -0.02f), w.At(a, h.Z1 + num + 0.03f, -0.02f), 0.025f);
		}

		private static void RoundWindow(Shell sh, Interior room, WallSide w, float a, float z, float r, int ringSlot)
		{
			MeshBuilder b = sh.B;
			Vec3[] array = new Vec3[12];
			for (int i = 0; i < 12; i++)
			{
				float num = MathF.PI * 2f * (float)i / 12f;
				array[i] = w.At(a + (float)Math.Cos(num) * r, z + (float)Math.Sin(num) * r, -0.01f);
			}
			FaceToward(b, sh.Glass, w.At(a, z) + w.Inward * 2f, array);
			for (int j = 0; j < 12; j++)
			{
				float num2 = MathF.PI * 2f * (float)j / 12f;
				float num3 = MathF.PI * 2f * (float)(j + 1) / 12f;
				Beam(b, ringSlot, w.At(a + (float)Math.Cos(num2) * (r + 0.03f), z + (float)Math.Sin(num2) * (r + 0.03f), -0.025f), w.At(a + (float)Math.Cos(num3) * (r + 0.03f), z + (float)Math.Sin(num3) * (r + 0.03f), -0.025f), 0.03f);
			}
			WallBox(sh, w, ringSlot, a, z, r, 0.012f, 0.01f, 0.012f);
			WallBox(sh, w, ringSlot, a, z, 0.012f, r, 0.01f, 0.012f);
			room.Windows.Add(new LightWindow
			{
				Center = w.At(a, z, -0.02f),
				Right = w.Along * -1f,
				HalfWidth = r * 0.8f,
				HalfHeight = r * 0.8f,
				Inward = w.Inward
			});
		}

		private static List<WallSide> Octagon(float cx, float cy, float apothem)
		{
			float num = apothem / (float)Math.Cos(0.39269909262657166);
			Vec3[] array = new Vec3[8];
			for (int i = 0; i < 8; i++)
			{
				float num2 = MathF.PI * -3f / 8f - (float)i * MathF.PI / 4f;
				array[i] = new Vec3(cx + (float)Math.Cos(num2) * num, cy + (float)Math.Sin(num2) * num, 0f);
			}
			List<WallSide> list = new List<WallSide>();
			for (int j = 0; j < 8; j++)
			{
				Vec3 vec = array[j];
				Vec3 vec2 = array[(j + 1) % 8];
				Vec3 vec3 = (vec2 - vec).Normalized();
				list.Add(new WallSide
				{
					Start = vec,
					Along = vec3,
					Inward = Vec3.Cross(vec3, new Vec3(0f, 0f, 1f)),
					Length = (float)Math.Sqrt(Vec3.Dot(vec2 - vec, vec2 - vec))
				});
			}
			return list;
		}

		private static void OctagonCorners(Interior room, float cx, float cy, float apothem)
		{
			float num = apothem * (1f - (float)Math.Tan(0.39269909262657166)) * 0.75f + 0.15f;
			float[] array = new float[2] { -1f, 1f };
			foreach (float num2 in array)
			{
				float[] array2 = new float[2] { -1f, 1f };
				foreach (float num3 in array2)
				{
					float num4 = cx + num2 * apothem;
					float num5 = cy + num3 * apothem;
					room.Solids.Add(new float[4]
					{
						Math.Min(num4, num4 - num2 * num),
						Math.Min(num5, num5 - num3 * num),
						Math.Max(num4, num4 - num2 * num),
						Math.Max(num5, num5 - num3 * num)
					});
				}
			}
		}

		private static void Dome(Shell sh, List<WallSide> walls, float cx, float cy, float H, float rise, int ribSlot, float eye)
		{
			MeshBuilder b = sh.B;
			int count = walls.Count;
			Vec3 vec = new Vec3(cx, cy, H);
			for (int i = 0; i < count; i++)
			{
				Vec3 start = walls[i].Start;
				Vec3 start2 = walls[(i + 1) % count].Start;
				for (int j = 0; j < 6; j++)
				{
					float t = (float)j / 6f;
					float t2 = (float)(j + 1) / 6f;
					Vec3 vec2 = P(start, t);
					Vec3 vec3 = P(start2, t);
					Vec3 vec4 = P(start2, t2);
					Vec3 vec5 = P(start, t2);
					FaceToward(b, sh.Ceiling, vec, vec2, vec3, vec4, vec5);
				}
				Vec3 a = start + (vec - start).Normalized() * 0.04f;
				for (int k = 1; k <= 6; k++)
				{
					float num = (float)k / 6f * MathF.PI * 0.5f;
					float num2 = Math.Max((float)Math.Cos(num), eye);
					Vec3 vec6 = new Vec3(cx + (start.X - cx) * num2 * 0.97f, cy + (start.Y - cy) * num2 * 0.97f, H + rise * (float)Math.Sin(num) - 0.04f);
					b.Tube(ribSlot, a, vec6, 0.035f, 0.035f, 5);
					a = vec6;
				}
			}
			if (eye > 0f)
			{
				Vec3[] array = new Vec3[count];
				for (int l = 0; l < count; l++)
				{
					array[l] = new Vec3(cx + (walls[l].Start.X - cx) * eye, cy + (walls[l].Start.Y - cy) * eye, H + rise + 0.01f);
				}
				FaceToward(b, sh.Glass, vec, array);
				for (int m = 0; m < count; m++)
				{
					b.Tube(ribSlot, array[m] - new Vec3(0f, 0f, 0.03f), array[(m + 1) % count] - new Vec3(0f, 0f, 0.03f), 0.04f, 0.04f, 5);
				}
			}
			Vec3 P(Vec3 corner, float num4)
			{
				float num3 = num4 * MathF.PI * 0.5f;
				float val = (float)Math.Cos(num3);
				float num5 = (float)Math.Sin(num3);
				if (num4 >= 0.999f)
				{
					val = eye;
				}
				return new Vec3(cx + (corner.X - cx) * Math.Max(val, eye), cy + (corner.Y - cy) * Math.Max(val, eye), H + rise * num5);
			}
		}

		private static void HideCone(Shell sh, List<WallSide> walls, float cx, float cy, float H, float rise, float hole)
		{
			MeshBuilder b = sh.B;
			int count = walls.Count;
			Vec3 vec = new Vec3(cx, cy, H + rise);
			Vec3 vec2 = new Vec3(cx, cy, H);
			for (int i = 0; i < count; i++)
			{
				Vec3 vec3 = walls[i].Start + new Vec3(0f, 0f, H);
				Vec3 vec4 = walls[(i + 1) % count].Start + new Vec3(0f, 0f, H);
				Vec3 vec5 = vec3 + (vec - vec3) * (1f - hole);
				Vec3 vec6 = vec4 + (vec - vec4) * (1f - hole);
				FaceToward(b, sh.Ceiling, vec2, vec3, vec4, vec6, vec5);
				b.Tube(sh.Bark, vec3 + (vec2 - vec3).Normalized() * 0.05f, vec + (vec - vec3) * 0.12f, 0.045f, 0.03f, 5);
				Vec3 vec7 = (vec3 + vec4) * 0.5f;
				Vec3 vec8 = (vec5 + vec6) * 0.5f;
				b.Tube(sh.Dark, vec7 + (vec2 - vec7).Normalized() * 0.02f, vec8 + (vec2 - vec8).Normalized() * 0.02f, 0.006f, 0.006f, 3);
			}
		}

		private static Dictionary<string, uint> VariantColors(string race, uint roof)
		{
			Dictionary<string, uint> dictionary = FurnitureColors(race, roof);
			dictionary["water"] = TerrainCache.Rgb(110, 160, 196);
			dictionary["crystal"] = ((race == "elf") ? TerrainCache.Rgb(150, 236, 226) : TerrainCache.Rgb(170, 220, 240));
			dictionary["gold"] = TerrainCache.Rgb(214, 178, 90);
			dictionary["iron"] = TerrainCache.Rgb(70, 70, 76);
			dictionary["red"] = TerrainCache.Lerp(roof, TerrainCache.Rgb(150, 34, 30), 0.65f);
			dictionary["hide"] = TerrainCache.Rgb(150, 112, 78);
			dictionary["canvas"] = TerrainCache.Rgb(220, 208, 184);
			return dictionary;
		}

		private static Interior StoneCottage(int roomW, int roomD, uint wallColor, uint roofColor, Random rng, int tier, Func<string, MeshModel> models)
		{
			Style style = StyleFor("human", wallColor, roofColor);
			style.Wall = TerrainCache.Rgb(150, 144, 134);
			style.WallKind = 4;
			style.WallH = 1.5f;
			style.MeshFloor = false;
			style.FloorSurface = 6;
			style.Floor = TerrainCache.Rgb(132, 126, 118);
			style.Ceiling = TerrainCache.Rgb(112, 80, 52);
			style.WinW = 0.4f;
			style.WinZ0 = 0.66f;
			style.WinZ1 = 1.1f;
			float x;
			float y;
			float x2;
			float y2;
			Interior interior = Begin(roomW, roomD, style, "human", tier, out x, out y, out x2, out y2);
			int gold;
			int cloth;
			int water;
			int red;
			Shell shell = NewShell(style, "human", x, y, x2, y2, out gold, out cloth, out water, out red);
			float num = x2 - x;
			float num2 = y2 - y;
			float H = style.WallH;
			AddDoor(shell, shell.South, "human", "human_stone");
			AddWindow(shell.North, num * 0.86f, style.WinW, style);
			AddWindow(shell.West, num2 * 0.3f, style.WinW, style);
			AddWindow(shell.South, num * 0.2f, style.WinW, style);
			if (num >= 6f)
			{
				AddWindow(shell.South, num * 0.82f, style.WinW, style);
			}
			BuildWalls(shell, interior, shell.All, "dwarf", H);
			WallSide[] all = shell.All;
			foreach (WallSide wallSide in all)
			{
				float num3 = 0f;
				foreach (Hole hole in wallSide.Holes)
				{
					if (hole.Door)
					{
						if (hole.A0 - num3 > 0.01f)
						{
							WallBox(shell, wallSide, shell.Stone, (num3 + hole.A0) * 0.5f, 0.06f, (hole.A0 - num3) * 0.5f, 0.06f, 0.025f);
						}
						num3 = hole.A1;
					}
				}
				if (wallSide.Length - num3 > 0.01f)
				{
					WallBox(shell, wallSide, shell.Stone, (num3 + wallSide.Length) * 0.5f, 0.06f, (wallSide.Length - num3) * 0.5f, 0.06f, 0.025f);
				}
			}
			int c = shell.B.Slot("flag2", 14, TerrainCache.Scale(style.Floor, 0.88f), 0);
			int d = shell.B.Slot("flag3", 14, TerrainCache.Scale(style.Floor, 1.1f), 0);
			Flagstones(shell.B, shell.Stone, c, d, x, y, x2, y2, 23);
			BeamCeiling(shell, H, 0.85f, 0.08f);
			int num4 = Math.Max(2, (int)Math.Round(num / 0.85f));
			float x3 = x + 0.1f + (num - 0.2f) * (float)(num4 - 1) / (float)num4;
			Herbs(shell, new Vec3(x3, y2 - 0.4f, H - 0.16f), new Vec3(x3, y + num2 * 0.45f, H - 0.16f), 4 + Math.Min(3, tier), rng);
			Finish(shell, interior);
			interior.Top = H;
			Layout layout = new Layout(interior, models, VariantColors("human", roofColor), x, y, x2, y2);
			layout.Keep(interior.DoorX - 0.55f, y, interior.DoorX + 0.55f, y + 1.05f);
			layout.WallTop = H;
			layout.CeilingAt = (float num5, float num6) => H - 0.17f;
			layout.AgainstWall("int_v_inglenook", 'E', y + num2 * 0.55f);
			float[] last = layout.Last;
			if (last != null)
			{
				if (tier >= 1)
				{
					layout.Put("int_p_pelt", last[0] - 0.45f, (last[1] + last[3]) * 0.5f, 0f, solid: false, anywhere: true);
				}
				if (tier >= 1)
				{
					layout.PutNear("int_p_armchair", last[0] - 0.75f, last[3] - 0.1f, -0.5f, 0.25f);
				}
				layout.Fit("int_p_firewood", 'E', last[1] - 0.08f - layout.Width("int_p_firewood") * 0.5f, 0.3f);
				layout.Hang("int_p_potrack", 'E', last[3] + 0.45f, 0.62f, 0.3f);
			}
			float cursor = x + 0.1f;
			if (tier >= 2)
			{
				cursor += 0.7f;
				layout.Next("int_v_stair", 'N', ref cursor, 0.2f);
			}
			layout.Next("int_v_dresser", 'N', ref cursor, 0.2f);
			layout.Fit("int_p_counter", 'N', x + num * 0.86f, 0.5f);
			layout.PutNear("int_p_jars", x2 - 0.25f, y2 - 0.7f, MathF.PI, 0.3f);
			BedsWest(layout, "int_human_bed", (tier >= 1) ? "int_human_chest" : null, null, new float[1] { y + num2 * 0.6f }, rng);
			layout.PutNear("int_barrel", x + 0.22f, y + 0.22f, 0.4f, 0.25f);
			layout.PutNear("int_sack", x + 0.6f, y + 0.2f, 1.1f, 0.25f);
			layout.PutNear("int_crate", x + 0.24f, y + 0.62f, 0.2f, 0.25f);
			float x4 = x + num * 0.45f;
			float y3 = y + num2 * 0.46f;
			if (layout.Put("int_v_trestle", x4, y3, -MathF.PI / 2f))
			{
				float[] last2 = layout.Last;
				layout.Put("int_human_bench", x4, last2[1] - 0.25f, MathF.PI / 2f);
				layout.Put("int_human_bench", x4, last2[3] + 0.25f, -MathF.PI / 2f);
				if (tier >= 2)
				{
					layout.Ceiling("int_p_candlering", x4, y3, Math.Min(1.22f, H - 0.17f - layout.Height("int_p_candlering") - 0.04f));
				}
			}
			if (tier >= 3)
			{
				layout.Put("int_human_rug", x2 - 1.4f, y + num2 * 0.55f, MathF.PI / 2f, solid: false, anywhere: true);
			}
			layout.PutNear("int_p_bucket", x2 - 0.22f, y + 0.25f, 0f, 0.3f);
			layout.Hang("int_p_sconce", 'S', interior.DoorX - 0.6f, 0.8f, 0.15f);
			if (tier >= 1)
			{
				layout.Hang("int_p_shield", 'W', y + num2 * 0.3f, 0.95f, 1f, centred: true);
			}
			if (tier >= 2)
			{
				layout.Hang("int_p_arms", 'S', x + num * 0.65f, 0.7f, 0.6f, centred: true);
			}
			if (tier >= 3)
			{
				layout.Hang("int_p_antlers", 'W', y + num2 * 0.72f, 0.82f, 0.6f, centred: true);
			}
			PlaceSpots(interior, layout);
			return interior;
		}

		private static Interior TimberHouse(int roomW, int roomD, uint wallColor, uint roofColor, Random rng, int tier, Func<string, MeshModel> models)
		{
			Style style = StyleFor("human", wallColor, roofColor);
			style.WallH = 1.2f;
			float x;
			float y;
			float x2;
			float y2;
			Interior interior = Begin(roomW, roomD, style, "human", tier, out x, out y, out x2, out y2);
			int gold;
			int cloth;
			int water;
			int red;
			Shell shell = NewShell(style, "human", x, y, x2, y2, out gold, out cloth, out water, out red);
			float W = x2 - x;
			float num = y2 - y;
			float H = style.WallH;
			float xm = (x + x2) * 0.5f;
			AddDoor(shell, shell.South, "human", "human_timber", H - 0.1f);
			AddWindow(shell.South, W * 0.22f, style.WinW, style);
			AddWindow(shell.South, W * 0.8f, style.WinW, style);
			AddWindow(shell.East, num * 0.85f, style.WinW, style);
			AddWindow(shell.East, num * 0.18f, style.WinW, style);
			AddWindow(shell.West, num * 0.45f, style.WinW, style);
			BuildWalls(shell, interior, shell.All, "human", H);
			if (style.MeshFloor)
			{
				shell.B.Face(shell.Floor, new Vec3(x, y, 0f), new Vec3(x2, y, 0f), new Vec3(x2, y2, 0f), new Vec3(x, y2, 0f));
			}
			Frame(shell, H, shell.Timber, 1.1f, skirting: true);
			WallSide[] all = shell.All;
			foreach (WallSide wallSide in all)
			{
				int num2 = Math.Max(1, (int)Math.Round(wallSide.Length / 1.1f));
				for (int j = 0; j < num2; j++)
				{
					float num3 = wallSide.Length * (float)j / (float)num2 + 0.1f;
					float num4 = wallSide.Length * (float)(j + 1) / (float)num2 - 0.1f;
					bool flag = true;
					foreach (Hole hole in wallSide.Holes)
					{
						if (num4 > hole.A0 - 0.05f && num3 < hole.A1 + 0.05f)
						{
							flag = false;
						}
					}
					if (flag)
					{
						Vec3 a = wallSide.At(num3, (j % 2 == 0) ? 0.1f : (H - 0.1f)) + wallSide.Inward * 0.025f;
						Vec3 c = wallSide.At(num4, (j % 2 == 0) ? (H - 0.1f) : 0.1f) + wallSide.Inward * 0.025f;
						Beam(shell.B, shell.Timber, a, c, 0.03f);
					}
				}
			}
			float rise = W * 0.62f;
			float num5 = H + rise;
			MeshBuilder b = shell.B;
			Vec3 toward = new Vec3(xm, (y + y2) * 0.5f, H);
			FaceToward(b, shell.Ceiling, toward, new Vec3(x, y2, H), new Vec3(x, y, H), new Vec3(xm, y, num5), new Vec3(xm, y2, num5));
			FaceToward(b, shell.Ceiling, toward, new Vec3(x2, y, H), new Vec3(x2, y2, H), new Vec3(xm, y2, num5), new Vec3(xm, y, num5));
			FaceToward(b, shell.Wall, toward, new Vec3(x, y, H), new Vec3(x2, y, H), new Vec3(xm, y, num5));
			FaceToward(b, shell.Wall, toward, new Vec3(x2, y2, H), new Vec3(x, y2, H), new Vec3(xm, y2, num5));
			int num6 = Math.Max(3, (int)Math.Round(num / 0.55f));
			for (int k = 0; k <= num6; k++)
			{
				float y3 = y + 0.05f + (num - 0.1f) * (float)k / (float)num6;
				Beam(b, shell.Timber, new Vec3(x + 0.02f, y3, H - 0.05f), new Vec3(xm, y3, num5 - 0.06f), 0.035f);
				Beam(b, shell.Timber, new Vec3(x2 - 0.02f, y3, H - 0.05f), new Vec3(xm, y3, num5 - 0.06f), 0.035f);
				if (k % 2 == 1)
				{
					Beam(b, shell.Timber, new Vec3(x + W * 0.22f, y3, H + rise * 0.56f), new Vec3(x2 - W * 0.22f, y3, H + rise * 0.56f), 0.04f);
				}
			}
			Beam(b, shell.Timber, new Vec3(xm, y, num5 - 0.08f), new Vec3(xm, y2, num5 - 0.08f), 0.06f);
			for (int l = 1; l <= Math.Max(1, (int)Math.Round(num / 2f)); l++)
			{
				float y4 = y + num * (float)l / (float)(Math.Max(1, (int)Math.Round(num / 2f)) + 1);
				Beam(b, shell.Timber, new Vec3(x, y4, H - 0.06f), new Vec3(x2, y4, H - 0.06f), 0.06f);
			}
			float y5 = y + num / (float)(Math.Max(1, (int)Math.Round(num / 2f)) + 1);
			Herbs(shell, new Vec3(x + 0.45f, y5, H - 0.12f), new Vec3(x + W * 0.5f, y5, H - 0.12f), 3, rng);
			Finish(shell, interior);
			interior.Top = num5;
			Layout layout = new Layout(interior, models, VariantColors("human", roofColor), x, y, x2, y2);
			layout.Keep(interior.DoorX - 0.55f, y, interior.DoorX + 0.55f, y + 1.05f);
			layout.WallTop = H;
			layout.CeilingAt = (float num8, float num9) => H + rise * (1f - Math.Min(1f, Math.Abs(num8 - xm) / (W * 0.5f))) - 0.06f;
			layout.AgainstWall("int_v_loft", 'N', x + 0.1f + layout.Width("int_v_loft") * 0.5f);
			layout.Fit("int_human_hearth", 'E', y + num * 0.5f, 0.3f);
			float[] last = layout.Last;
			if (last != null)
			{
				layout.Put((tier >= 1) ? "int_p_pelt" : "int_human_rug", last[0] - 0.42f, (last[1] + last[3]) * 0.5f, 0f, solid: false, anywhere: true);
				layout.PutNear((tier >= 1) ? "int_p_armchair" : "int_human_chair", last[0] - 0.72f, last[1] - 0.05f, -0.45f, 0.25f);
				if (tier >= 1)
				{
					layout.PutNear("int_p_sidetable", last[0] - 0.75f, last[1] - 0.48f, 0f, 0.2f);
				}
				layout.Hang("int_p_sconce", 'E', last[3] + 0.18f, 0.7f, 0.2f);
			}
			if (layout.Fit("int_v_desk", 'W', y + num * 0.22f, 0.4f))
			{
				layout.Put("int_human_chair", layout.Last[2] + 0.28f, layout.LastY, MathF.PI);
			}
			float num7 = x2 - 0.1f - layout.Width("int_human_wardrobe");
			layout.AgainstWall("int_human_wardrobe", 'N', num7 + layout.Width("int_human_wardrobe") * 0.5f);
			if (tier >= 2)
			{
				layout.Fit("int_human_bookcase", 'E', y + num * 0.68f, 0.3f);
			}
			float x3 = xm + 0.2f;
			float y6 = y + num * 0.48f;
			Dining(layout, "int_human_table", "int_human_chair", "int_human_rug", x3, y6, 2 + Math.Min(2, tier), round: false, tier >= 2);
			if (layout.Fit("int_p_counter", 'S', x + W * 0.22f, 0.4f))
			{
				layout.HangAny("int_p_potrack", 0.5f, false, ('S', layout.LastX + 0.75f), ('W', y + 0.6f), ('E', y + num * 0.25f));
			}
			layout.PutNear("int_barrel", x2 - 0.22f, y + 0.22f, 0.4f, 0.25f);
			layout.PutNear("int_sack", x2 - 0.6f, y + 0.2f, 1.2f, 0.25f);
			if (tier >= 1)
			{
				layout.PutNear("int_p_jars", x2 - 0.24f, y + 0.6f, MathF.PI, 0.3f);
			}
			if (tier >= 1)
			{
				layout.HangAny("int_p_shield", 0.68f, ('W', y + num * 0.45f), ('E', y + num * 0.35f), ('S', x + W * 0.62f), ('N', x + W * 0.3f));
			}
			if (tier >= 3)
			{
				layout.HangAny("int_p_arms", 0.5f, ('S', x + W * 0.62f), ('N', x + W * 0.35f), ('W', y + num * 0.7f), ('E', y + num * 0.7f));
			}
			PlaceSpots(interior, layout);
			return interior;
		}

		private static Interior SpireHall(int roomW, int roomD, uint wallColor, uint roofColor, Random rng, int tier, Func<string, MeshModel> models)
		{
			Style style = StyleFor("elf", wallColor, roofColor);
			style.WallH = 1.95f;
			style.WinW = 0.4f;
			style.WinZ0 = 0.55f;
			style.WinZ1 = 1.35f;
			float x;
			float y0;
			float x2;
			float y1;
			Interior interior = Begin(roomW, roomD, style, "elf", tier, out x, out y0, out x2, out y1);
			int gold;
			int cloth;
			int water;
			int red;
			Shell shell = NewShell(style, "elf", x, y0, x2, y1, out gold, out cloth, out water, out red);
			float num = x2 - x;
			float D = y1 - y0;
			float wallH = style.WallH;
			float num2 = (x + x2) * 0.5f;
			AddDoor(shell, shell.South, "elf", "elf_spire");
			int bays = Math.Max(2, (int)Math.Round(D / 1.6f));
			for (int i = 0; i < bays; i++)
			{
				float at = D * ((float)i + 0.5f) / (float)bays;
				AddWindow(shell.West, at, style.WinW, style);
				AddWindow(shell.East, at, style.WinW, style);
			}
			AddWindow(shell.South, num * 0.18f, style.WinW * 0.8f, style);
			AddWindow(shell.South, num * 0.82f, style.WinW * 0.8f, style);
			BuildWalls(shell, interior, shell.All, "elf", wallH);
			WallSide[] all = shell.All;
			foreach (WallSide wallSide in all)
			{
				foreach (Hole hole in wallSide.Holes)
				{
					if (!hole.Door)
					{
						PointedTop(shell, wallSide, hole, gold);
					}
				}
			}
			if (style.MeshFloor)
			{
				shell.B.Face(shell.Floor, new Vec3(x, y0, 0f), new Vec3(x2, y0, 0f), new Vec3(x2, y1, 0f), new Vec3(x, y1, 0f));
			}
			MeshBuilder b = shell.B;
			Vec3[] array = new Vec3[13];
			float num3 = num * 0.78f;
			for (int k = 0; k <= 6; k++)
			{
				float num4 = (float)k / 6f;
				float num5 = MathF.PI;
				float num6 = (float)Math.Acos((num2 - (x + num3)) / num3);
				float num7 = num5 + (num6 - num5) * num4;
				float num8 = x + num3 + num3 * (float)Math.Cos(num7);
				float z = wallH + num3 * (float)Math.Sin(num7);
				array[k] = new Vec3(num8, 0f, z);
				array[12 - k] = new Vec3(2f * num2 - num8, 0f, z);
			}
			float z2 = array[6].Z;
			for (int l = 0; l < 12; l++)
			{
				Vec3 vec = array[l];
				Vec3 vec2 = array[l + 1];
				FaceToward(b, shell.Ceiling, new Vec3(num2, (y0 + y1) * 0.5f, wallH), new Vec3(vec.X, y0, vec.Z), new Vec3(vec2.X, y0, vec2.Z), new Vec3(vec2.X, y1, vec2.Z), new Vec3(vec.X, y1, vec.Z));
			}
			float[] array2 = new float[2] { y0, y1 };
			foreach (float y2 in array2)
			{
				List<Vec3> list = new List<Vec3>
				{
					new Vec3(x, y2, wallH)
				};
				for (int m = 0; m <= 12; m++)
				{
					list.Add(new Vec3(array[m].X, y2, array[m].Z));
				}
				list.Add(new Vec3(x2, y2, wallH));
				for (int n = 1; n < list.Count - 1; n++)
				{
					FaceToward(b, shell.Wall, new Vec3(num2, (y0 + y1) * 0.5f, wallH), new Vec3(num2, y2, wallH), list[n], list[n + 1]);
				}
			}
			int num9 = bays;
			for (int num10 = 0; num10 <= num9; num10++)
			{
				float y3 = y0 + 0.06f + (D - 0.12f) * (float)num10 / (float)num9;
				for (int num11 = 0; num11 < 12; num11++)
				{
					Vec3 vec3 = array[num11];
					Vec3 vec4 = array[num11 + 1];
					b.Tube(shell.Timber, new Vec3(vec3.X + ((vec3.X < num2) ? 0.04f : (-0.04f)), y3, vec3.Z - 0.04f), new Vec3(vec4.X + ((vec4.X < num2) ? 0.04f : (-0.04f)), y3, vec4.Z - 0.04f), 0.035f, 0.035f, 5);
				}
				all = new WallSide[2] { shell.West, shell.East };
				foreach (WallSide wallSide2 in all)
				{
					float num12 = Vec3.Dot(new Vec3(0f, y3, 0f) - wallSide2.Start, wallSide2.Along);
					bool flag = true;
					foreach (Hole hole2 in wallSide2.Holes)
					{
						if (num12 > hole2.A0 - 0.1f && num12 < hole2.A1 + 0.1f)
						{
							flag = false;
						}
					}
					if (flag)
					{
						WallBox(shell, wallSide2, shell.Stone, num12, wallH * 0.5f, 0.05f, wallH * 0.5f, 0.04f);
					}
				}
			}
			b.Tube(gold, new Vec3(num2, y0, z2 - 0.05f), new Vec3(num2, y1, z2 - 0.05f), 0.03f, 0.03f, 5);
			all = new WallSide[2] { shell.West, shell.East };
			foreach (WallSide wallSide3 in all)
			{
				for (float num13 = 0.3f; num13 < wallSide3.Length - 0.2f; num13 += 0.32f)
				{
					b.Blob(shell.Leaf, wallSide3.At(num13, wallH - 0.08f) + wallSide3.Inward * 0.05f, 0.07f, 0.06f, 0.055f, 6);
				}
			}
			Finish(shell, interior);
			interior.Top = z2;
			Layout layout = new Layout(interior, models, VariantColors("elf", roofColor), x, y0, x2, y1);
			layout.Keep(interior.DoorX - 0.55f, y0, interior.DoorX + 0.55f, y0 + 1.05f);
			layout.Keep(num2 - 0.45f, y0, num2 + 0.45f, y1 - 1f);
			layout.WallTop = wallH;
			layout.AgainstWall("int_v_shrine", 'N', num2);
			layout.Put("int_elf_rug", num2, y0 + D * 0.55f, -MathF.PI / 2f, solid: false, anywhere: true);
			layout.Fit("int_elf_bed", 'W', Bay(1), 0.3f);
			if (bays > 2)
			{
				layout.Fit("int_elf_shelf", 'W', Bay(2), 0.3f);
			}
			if (tier >= 1)
			{
				layout.Fit("int_elf_chest", 'W', y0 + D * 1.5f / (float)bays, 0.3f);
			}
			layout.Fit("int_v_lectern", 'E', Bay(1), 0.3f);
			if (bays > 2)
			{
				layout.Fit((tier >= 2) ? "int_elf_bed" : "int_v_harp", 'E', Bay(2), 0.3f);
			}
			if (bays > 3)
			{
				layout.Fit((tier >= 2) ? "int_v_harp" : "int_elf_shelf", 'E', Bay(3), 0.3f);
			}
			layout.PutNear("int_elf_plant", x + 0.23f, y0 + 0.23f, 0f, 0.15f);
			layout.PutNear("int_elf_plant", x2 - 0.23f, y0 + 0.23f, MathF.PI, 0.15f);
			layout.PutNear("int_v_pot_tree", num2 - 1.05f, y1 - 0.4f, 0.5f, 0.3f);
			layout.PutNear("int_v_pot_tree", num2 + 1.05f, y1 - 0.4f, 2f, 0.3f);
			if (num >= 5f && layout.PutNear("int_elf_table", x + 1.05f, y0 + D * 0.62f, -MathF.PI / 2f, 0.3f))
			{
				float[] last = layout.Last;
				float lastX = layout.LastX;
				float lastY = layout.LastY;
				layout.PutNear("int_elf_stool", last[2] + 0.22f, lastY, MathF.PI, 0.12f);
				layout.PutNear("int_elf_stool", lastX, last[1] - 0.2f, MathF.PI / 2f, 0.12f);
			}
			layout.PutNear("int_elf_lantern", num2 + 0.95f, y0 + 0.3f, MathF.PI / 2f, 0.25f);
			layout.HangAny("int_p_tapestry", 0.35f, ('S', x + num * 0.35f), ('W', y0 + D * 0.5f), ('E', y0 + D * 0.5f), ('N', x + num * 0.25f));
			layout.Hang("int_p_sconce", 'N', num2 - 0.75f, 0.9f, 0.3f);
			layout.Hang("int_p_sconce", 'N', num2 + 0.75f, 0.9f, 0.3f);
			layout.Put("int_v_chandelier", num2, y0 + D * 0.33f, 0f, solid: false, anywhere: true);
			if (D > 6f)
			{
				layout.Put("int_v_chandelier", num2, y0 + D * 0.7f, 0f, solid: false, anywhere: true);
			}
			PlaceSpots(interior, layout);
			return interior;
			float Bay(int num14)
			{
				return y0 + D * (float)num14 / (float)bays;
			}
		}

		private static Interior Pavilion(int size, uint wallColor, uint roofColor, Random rng, int tier, Func<string, MeshModel> models)
		{
			Style style = StyleFor("elf", wallColor, roofColor);
			style.WallH = 1.75f;
			style.WinW = 0.42f;
			style.WinZ0 = 0.5f;
			style.WinZ1 = 1.3f;
			float x;
			float y;
			float x2;
			float y2;
			Interior interior = Begin(size, size, style, "elf", tier, out x, out y, out x2, out y2);
			float num = (x + x2) * 0.5f;
			float num2 = (y + y2) * 0.5f;
			float num3 = (float)size * 0.5f;
			float wallH = style.WallH;
			int gold;
			int cloth;
			int water;
			int red;
			Shell shell = NewShell(style, "elf", x, y, x2, y2, out gold, out cloth, out water, out red);
			List<WallSide> list = Octagon(num, num2, num3);
			AddDoor(shell, list[0], "elf", "elf_pavilion");
			for (int i = 1; i < 8; i++)
			{
				if (i != 4)
				{
					AddWindow(list[i], list[i].Length * 0.5f, style.WinW, style);
				}
			}
			BuildWalls(shell, interior, list, "elf", wallH);
			foreach (WallSide item in list)
			{
				foreach (Hole hole in item.Holes)
				{
					if (!hole.Door)
					{
						PointedTop(shell, item, hole, gold);
					}
				}
			}
			foreach (WallSide item2 in list)
			{
				WallBox(shell, item2, shell.Stone, 0.06f, wallH * 0.5f, 0.07f, wallH * 0.5f, 0.06f);
				WallBox(shell, item2, gold, item2.Length * 0.5f, wallH - 0.04f, item2.Length * 0.5f, 0.03f, 0.03f);
			}
			if (style.MeshFloor)
			{
				Vec3[] array = new Vec3[8];
				for (int j = 0; j < 8; j++)
				{
					array[j] = list[j].Start;
				}
				FaceToward(shell.B, shell.Floor, new Vec3(num, num2, 1f), array);
			}
			Dome(shell, list, num, num2, wallH, num3 * 0.95f, shell.Timber, 0.18f);
			foreach (WallSide item3 in list)
			{
				for (float num4 = 0.2f; num4 < item3.Length - 0.1f; num4 += 0.3f)
				{
					shell.B.Blob(shell.Leaf, item3.At(num4, wallH - 0.14f) + item3.Inward * 0.06f, 0.07f, 0.06f, 0.055f, 6);
				}
			}
			Finish(shell, interior);
			interior.Top = wallH + num3 * 0.95f;
			OctagonCorners(interior, num, num2, num3);
			interior.DoorX = num;
			interior.DoorY = num2 - num3 + 0.45f;
			interior.SpawnX = num;
			interior.SpawnY = num2 - num3 + 0.75f;
			Layout layout = new Layout(interior, models, VariantColors("elf", roofColor), x, y, x2, y2);
			layout.Keep(num - 0.55f, y, num + 0.55f, y + 1.05f);
			layout.WallTop = wallH;
			layout.Put("int_v_fountain", num, num2 + 0.2f, -MathF.PI / 2f);
			layout.Fit((tier >= 1) ? "int_elf_bed" : "int_v_cushions", 'N', num, 0.3f);
			string[] array2 = new string[6]
			{
				"int_v_pot_tree",
				"int_elf_shelf",
				"int_elf_lantern",
				"int_elf_lantern",
				(tier >= 2) ? "int_elf_shelf" : "int_elf_plant",
				"int_v_pot_tree"
			};
			for (int k = 1; k <= 6; k++)
			{
				string name = array2[k - 1];
				float num5 = MathF.PI * -3f / 8f + (float)(k - 1) * MathF.PI / 4f + MathF.PI / 4f;
				float num6 = num3 - layout.Depth(name) * 0.5f - 0.32f;
				layout.PutNear(name, num + (float)Math.Cos(num5) * num6, num2 + (float)Math.Sin(num5) * num6, num5 + MathF.PI, 0.2f);
			}
			string[] array3 = new string[6] { "int_v_cushions", "int_elf_chest", "int_v_cushions", "int_v_cushions", "int_p_basket", "int_v_cushions" };
			int num7 = 0;
			for (int l = 1; l < 8; l++)
			{
				if (l != 4)
				{
					string name2 = array3[num7++];
					float num8 = -MathF.PI / 2f + (float)l * MathF.PI / 4f;
					float num9 = num3 - layout.Depth(name2) * 0.5f - 0.12f;
					layout.PutNear(name2, num + (float)Math.Cos(num8) * num9, num2 + (float)Math.Sin(num8) * num9, num8 + MathF.PI, 0.2f);
				}
			}
			layout.Put("int_elf_rug", num, num2 - 0.9f, -MathF.PI / 2f, solid: false, anywhere: true);
			layout.Put("int_elf_rug", num, num2 + 1.1f, MathF.PI / 2f, solid: false, anywhere: true);
			layout.Put("int_v_chandelier", num - num3 * 0.5f, num2, 0f, solid: false, anywhere: true);
			PlaceSpots(interior, layout);
			return interior;
		}

		private static Interior Smithy(int roomW, int roomD, uint wallColor, uint roofColor, Random rng, int tier, Func<string, MeshModel> models)
		{
			Style style = StyleFor("orc", wallColor, roofColor);
			style.Wall = TerrainCache.Rgb(64, 48, 36);
			style.WallKind = 3;
			style.WallH = 1.55f;
			style.MeshFloor = false;
			style.FloorSurface = 6;
			style.Floor = TerrainCache.Rgb(96, 90, 84);
			style.Ceiling = TerrainCache.Rgb(58, 44, 34);
			style.CeilingKind = 3;
			style.WinW = 0.3f;
			style.WinZ0 = 0.85f;
			style.WinZ1 = 1.15f;
			float x;
			float y;
			float x2;
			float y2;
			Interior interior = Begin(roomW, roomD, style, "orc", tier, out x, out y, out x2, out y2);
			int gold;
			int cloth;
			int water;
			int red;
			Shell shell = NewShell(style, "orc", x, y, x2, y2, out gold, out cloth, out water, out red);
			float num = x2 - x;
			float num2 = y2 - y;
			float wallH = style.WallH;
			AddDoor(shell, shell.South, "orc", "orc_lodge");
			AddWindow(shell.West, num2 * 0.5f, style.WinW, style);
			AddWindow(shell.East, num2 * 0.5f, style.WinW, style);
			AddWindow(shell.South, num * 0.2f, style.WinW, style);
			BuildWalls(shell, interior, shell.All, "orc", wallH);
			WallSide[] all = shell.All;
			foreach (WallSide wallSide in all)
			{
				float num3 = 0f;
				foreach (Hole hole in wallSide.Holes)
				{
					if (hole.Door)
					{
						if (hole.A0 - num3 > 0.01f)
						{
							WallBox(shell, wallSide, shell.Stone, (num3 + hole.A0) * 0.5f, 0.38f, (hole.A0 - num3) * 0.5f, 0.38f, 0.05f);
						}
						num3 = hole.A1;
					}
				}
				if (wallSide.Length - num3 > 0.01f)
				{
					WallBox(shell, wallSide, shell.Stone, (num3 + wallSide.Length) * 0.5f, 0.38f, (wallSide.Length - num3) * 0.5f, 0.38f, 0.05f);
				}
				WallBox(shell, wallSide, shell.Metal, wallSide.Length * 0.5f, wallH - 0.3f, wallSide.Length * 0.5f, 0.035f, 0.012f);
				for (float num4 = 0.4f; num4 < wallSide.Length - 0.2f; num4 += 0.32f)
				{
					bool flag = true;
					foreach (Hole hole2 in wallSide.Holes)
					{
						if (num4 > hole2.A0 - 0.05f && num4 < hole2.A1 + 0.05f)
						{
							flag = false;
						}
					}
					if (flag)
					{
						WallBox(shell, wallSide, shell.Dark, num4, (0.76f + wallH) * 0.5f, 0.006f, (wallH - 0.76f) * 0.5f, 0.004f);
					}
				}
			}
			int c = shell.B.Slot("flag2", 14, TerrainCache.Scale(style.Floor, 0.85f), 0);
			int d = shell.B.Slot("flag3", 14, TerrainCache.Scale(style.Floor, 1.1f), 0);
			Flagstones(shell.B, shell.Stone, c, d, x, y, x2, y2, 41);
			BeamCeiling(shell, wallH, 1f, 0.1f);
			for (int j = 0; j < 3 + tier / 2; j++)
			{
				float num5 = x + num * (0.2f + 0.6f * (float)rng.NextDouble());
				float y3 = y + num2 * (0.3f + 0.5f * (float)rng.NextDouble());
				float num6 = 0.35f + 0.25f * (float)rng.NextDouble();
				for (int k = 0; k < (int)(num6 / 0.07f); k++)
				{
					shell.B.Blob(shell.Metal, new Vec3(num5, y3, wallH - 0.12f - (float)k * 0.07f), (k % 2 == 0) ? 0.022f : 0.012f, (k % 2 == 0) ? 0.012f : 0.022f, 0.035f, 5);
				}
				shell.B.Tube(shell.Metal, new Vec3(num5, y3, wallH - 0.12f - num6), new Vec3(num5 + 0.04f, y3, wallH - 0.2f - num6), 0.012f, 0.004f, 4);
			}
			Finish(shell, interior);
			interior.Top = wallH;
			Layout layout = new Layout(interior, models, VariantColors("orc", roofColor), x, y, x2, y2);
			layout.Keep(interior.DoorX - 0.55f, y, interior.DoorX + 0.55f, y + 1.05f);
			float num7 = (x + x2) * 0.5f;
			if (layout.AgainstWall("int_v_bigforge", 'N', num7 + num * 0.1f))
			{
				layout.Put("int_dwarf_anvil", layout.LastX - 0.1f, layout.Last[1] - 0.75f, MathF.PI * 11f / 20f);
				layout.Put("int_barrel", (layout.Last != null) ? (layout.Last[2] + 0.45f) : (num7 + 1.2f), y2 - 1.6f, 0.3f);
			}
			layout.WallTop = wallH;
			layout.Fit("int_v_weapons", 'W', y + num2 * 0.2f, 0.4f);
			layout.Fit("int_orc_bed", 'W', y + num2 * 0.5f, 0.4f);
			layout.Fit("int_v_banner", 'W', y + num2 * 0.84f, 0.3f);
			layout.Fit("int_v_weapons", 'E', y + num2 * 0.24f, 0.4f);
			layout.Fit("int_v_banner", 'E', y + num2 * 0.8f, 0.3f);
			layout.PutNear("int_barrel", x2 - 0.25f, y + num2 * 0.5f, 0.6f, 0.2f);
			if (tier >= 2)
			{
				layout.Fit("int_orc_rack", 'S', x + num * 0.72f, 0.5f);
			}
			layout.Fit("int_p_firewood", 'N', x + num * 0.2f, 0.5f);
			float num8 = num7 - num * 0.15f;
			float y4 = y + num2 * 0.42f;
			if (layout.Put("int_orc_table", num8, y4, -MathF.PI / 2f))
			{
				float[] last = layout.Last;
				layout.Put("int_orc_stool", num8 - 0.35f, last[1] - 0.25f, MathF.PI / 2f);
				layout.Put("int_orc_stool", num8 + 0.35f, last[1] - 0.25f, MathF.PI / 2f);
				layout.Put("int_orc_stool", num8, last[3] + 0.25f, -MathF.PI / 2f);
			}
			layout.PutNear("int_crate", x2 - 0.3f, y + 0.3f, 0.2f, 0.25f);
			layout.PutNear("int_sack", x2 - 0.85f, y + 0.3f, 1.2f, 0.25f);
			layout.Hang("int_p_shield", 'W', y + num2 * 0.5f, 0.92f, 0.8f, centred: true);
			layout.Hang("int_p_shield_b", 'E', y + num2 * 0.55f, 0.92f, 0.8f, centred: true);
			if (tier >= 1)
			{
				layout.Hang("int_p_antlers", 'S', x + num * 0.35f, 0.82f, 0.4f, centred: true);
			}
			PlaceSpots(interior, layout);
			return interior;
		}

		private static Interior SkullHut(int size, uint wallColor, uint roofColor, Random rng, int tier, Func<string, MeshModel> models)
		{
			Style style = StyleFor("orc", wallColor, roofColor);
			style.WallH = 1.05f;
			style.WinW = 0.26f;
			style.WinZ0 = 0.5f;
			style.WinZ1 = 0.75f;
			float x;
			float y;
			float x2;
			float y2;
			Interior interior = Begin(size, size, style, "orc", tier, out x, out y, out x2, out y2);
			float num = (x + x2) * 0.5f;
			float num2 = (y + y2) * 0.5f;
			float num3 = (float)size * 0.5f;
			float wallH = style.WallH;
			int gold;
			int cloth;
			int water;
			int red;
			Shell shell = NewShell(style, "orc", x, y, x2, y2, out gold, out cloth, out water, out red);
			List<WallSide> list = Octagon(num, num2, num3);
			AddDoor(shell, list[0], "orc", "orc_skull");
			AddWindow(list[2], list[2].Length * 0.5f, style.WinW, style);
			AddWindow(list[6], list[6].Length * 0.5f, style.WinW, style);
			BuildWalls(shell, interior, list, "orc", wallH);
			foreach (WallSide item in list)
			{
				int num4 = Math.Max(2, (int)Math.Round(item.Length / 0.24f));
				for (int i = 0; i <= num4; i++)
				{
					float num5 = 0.04f + (item.Length - 0.08f) * (float)i / (float)num4;
					bool flag = true;
					foreach (Hole hole in item.Holes)
					{
						if (num5 > hole.A0 - 0.06f && num5 < hole.A1 + 0.06f)
						{
							flag = false;
						}
					}
					if (flag)
					{
						Vec3 vec = item.At(num5, 0f) + item.Inward * 0.06f;
						shell.B.Tube(shell.Bark, vec, vec + new Vec3(0f, 0f, wallH + 0.08f), 0.065f, 0.06f, 6);
					}
				}
				Band(shell, item, shell.Dark, wallH * 0.55f, 0.012f, 0.066f);
			}
			HideCone(shell, list, num, num2, wallH, num3 * 1.35f, 0.12f);
			Finish(shell, interior);
			interior.Top = wallH + num3 * 1.35f;
			OctagonCorners(interior, num, num2, num3);
			interior.DoorX = num;
			interior.DoorY = num2 - num3 + 0.45f;
			interior.SpawnX = num;
			interior.SpawnY = num2 - num3 + 0.75f;
			Layout layout = new Layout(interior, models, VariantColors("orc", roofColor), x, y, x2, y2);
			layout.Keep(num - 0.55f, y, num + 0.55f, y + 1.05f);
			layout.Put("int_orc_rug", num, num2 - 0.2f, -MathF.PI / 2f, solid: false, anywhere: true);
			layout.Put("int_orc_firepit", num, num2 + 0.1f, -MathF.PI / 2f);
			if (layout.Last != null)
			{
				layout.PutNear("int_p_cauldron", layout.Last[2] + 0.3f, num2 + 0.35f, 0f, 0.2f);
			}
			string[] array = new string[7]
			{
				"int_v_skullpole",
				"int_orc_chest",
				"int_orc_bed",
				"int_orc_totem",
				(tier >= 2) ? "int_orc_bed" : "int_orc_drum",
				"int_sack",
				"int_v_skullpole"
			};
			for (int j = 1; j < 8; j++)
			{
				string name = array[j - 1];
				float num6 = -MathF.PI / 2f + (float)j * MathF.PI / 4f;
				float num7 = layout.Depth(name) * 0.5f + 0.1f;
				float num8 = num3 - num7 - 0.12f;
				layout.PutNear(name, num + (float)Math.Cos(num6) * num8, num2 + (float)Math.Sin(num6) * num8, num6 + MathF.PI, 0.35f);
			}
			layout.PutNear("int_p_firewood", num - 0.9f, num2 + num3 - 0.55f, -MathF.PI / 2f, 0.3f);
			PlaceSpots(interior, layout);
			return interior;
		}

		private static Interior GreatHall(int roomW, int roomD, uint wallColor, uint roofColor, Random rng, int tier, Func<string, MeshModel> models)
		{
			Style style = StyleFor("dwarf", wallColor, roofColor);
			style.Wall = TerrainCache.Rgb(116, 82, 54);
			style.WallKind = 3;
			style.WallH = 0.85f;
			style.MeshFloor = true;
			style.Floor = TerrainCache.Rgb(138, 98, 62);
			style.Ceiling = TerrainCache.Rgb(104, 72, 46);
			style.CeilingKind = 3;
			style.WinW = 0.34f;
			style.WinZ0 = 0.35f;
			style.WinZ1 = 0.7f;
			float x;
			float y;
			float x2;
			float y2;
			Interior interior = Begin(roomW, roomD, style, "dwarf", tier, out x, out y, out x2, out y2, TerrainCache.Rgb(138, 98, 62));
			int gold;
			int cloth;
			int water;
			int red;
			Shell shell = NewShell(style, "dwarf", x, y, x2, y2, out gold, out cloth, out water, out red);
			float num = x2 - x;
			float num2 = y2 - y;
			float wallH = style.WallH;
			float xm = (x + x2) * 0.5f;
			float y3 = (y + y2) * 0.5f;
			shell.Floor = shell.B.Slot("boards", 3, style.Floor, 0);
			AddDoor(shell, shell.South, "dwarf", "dwarf_lodge");
			AddWindow(shell.West, num2 * 0.3f, style.WinW, style);
			AddWindow(shell.West, num2 * 0.72f, style.WinW, style);
			AddWindow(shell.East, num2 * 0.3f, style.WinW, style);
			AddWindow(shell.East, num2 * 0.72f, style.WinW, style);
			BuildWalls(shell, interior, shell.All, "dwarf", wallH);
			MeshBuilder b = shell.B;
			b.Face(shell.Floor, new Vec3(x, y, 0f), new Vec3(x2, y, 0f), new Vec3(x2, y2, 0f), new Vec3(x, y2, 0f));
			for (float num3 = x + 0.22f; num3 < x2; num3 += 0.22f)
			{
				b.Face(shell.Dark, new Vec3(num3 - 0.006f, y, 0.003f), new Vec3(num3 + 0.006f, y, 0.003f), new Vec3(num3 + 0.006f, y2, 0.003f), new Vec3(num3 - 0.006f, y2, 0.003f));
			}
			float num4 = num * 1f;
			float num5 = wallH + num4;
			Vec3[] array = new Vec3[7];
			for (int i = 0; i <= 6; i++)
			{
				float num6 = (float)i / 6f;
				array[i] = new Vec3(x + (xm - x) * num6, 0f, wallH + num4 * (float)Math.Pow(num6, 1.35));
			}
			for (int j = 0; j < 6; j++)
			{
				Vec3 vec = array[j];
				Vec3 vec2 = array[j + 1];
				FaceToward(b, shell.Ceiling, new Vec3(xm, y3, wallH), new Vec3(vec.X, y, vec.Z), new Vec3(vec2.X, y, vec2.Z), new Vec3(vec2.X, y2, vec2.Z), new Vec3(vec.X, y2, vec.Z));
				FaceToward(b, shell.Ceiling, new Vec3(xm, y3, wallH), new Vec3(2f * xm - vec.X, y, vec.Z), new Vec3(2f * xm - vec2.X, y, vec2.Z), new Vec3(2f * xm - vec2.X, y2, vec2.Z), new Vec3(2f * xm - vec.X, y2, vec.Z));
				FaceToward(b, shell.Wall, new Vec3(xm, y3, wallH), new Vec3(xm, y2, wallH), new Vec3(vec.X, y2, vec.Z), new Vec3(vec2.X, y2, vec2.Z));
				FaceToward(b, shell.Wall, new Vec3(xm, y3, wallH), new Vec3(xm, y2, wallH), new Vec3(2f * xm - vec.X, y2, vec.Z), new Vec3(2f * xm - vec2.X, y2, vec2.Z));
			}
			float num7 = 0.33f;
			float num8 = wallH;
			foreach (Hole hole in shell.South.Holes)
			{
				if (hole.Door)
				{
					num7 = (hole.A1 - hole.A0) * 0.5f;
					num8 = Math.Max(wallH, hole.Z1);
				}
			}
			int k;
			for (k = 0; k < 6 && array[k + 1].Z <= num8; k++)
			{
			}
			float x3 = array[k].X + (array[k + 1].X - array[k].X) * (num8 - array[k].Z) / Math.Max(0.0001f, array[k + 1].Z - array[k].Z);
			float[] array2 = new float[2] { 1f, -1f };
			foreach (float s in array2)
			{
				Vec3 toward = new Vec3(xm, y3, wallH);
				if (num8 > wallH)
				{
					FaceToward(b, shell.Wall, toward, new Vec3(X(x), y, wallH), new Vec3(X(xm - num7), y, wallH), new Vec3(X(xm - num7), y, num8), new Vec3(X(x3), y, num8));
				}
				Vec3 vec3 = new Vec3(X(x3), y, num8);
				for (int m = k + 1; m <= 6; m++)
				{
					Vec3 vec4 = new Vec3(X(array[m].X), y, array[m].Z);
					FaceToward(b, shell.Wall, toward, new Vec3(xm, y, num8), vec3, vec4);
					vec3 = vec4;
				}
				float X(float num13)
				{
					if (!(s > 0f))
					{
						return 2f * xm - num13;
					}
					return num13;
				}
			}
			int num9 = Math.Max(3, (int)Math.Round(num2 / 1.2f));
			for (int n = 0; n <= num9; n++)
			{
				float y4 = y + 0.08f + (num2 - 0.16f) * (float)n / (float)num9;
				array2 = new float[2] { -1f, 1f };
				foreach (float num10 in array2)
				{
					Vec3 a = new Vec3(xm + num10 * (num * 0.5f - 0.08f), y4, 0f);
					for (int num11 = 0; num11 <= 6; num11++)
					{
						Vec3 vec5 = array[num11];
						Vec3 vec6 = new Vec3(xm + num10 * (xm - vec5.X - 0.08f), y4, vec5.Z - 0.08f);
						if (num11 == 0)
						{
							vec6 = new Vec3(xm + num10 * (num * 0.5f - 0.1f), y4, wallH);
						}
						Beam(b, shell.Timber, a, vec6, 0.06f);
						a = vec6;
					}
				}
				if (n % 2 == 1)
				{
					Beam(b, shell.Timber, new Vec3(x + num * 0.18f, y4, wallH + num4 * 0.5f), new Vec3(x2 - num * 0.18f, y4, wallH + num4 * 0.5f), 0.055f);
				}
			}
			Beam(b, shell.Timber, new Vec3(xm, y, num5 - 0.1f), new Vec3(xm, y2, num5 - 0.1f), 0.08f);
			WallSide w = new WallSide
			{
				Start = new Vec3(x, y2, 0f),
				Along = new Vec3(1f, 0f, 0f),
				Inward = new Vec3(0f, -1f, 0f),
				Length = num
			};
			RoundWindow(shell, interior, w, num * 0.5f, wallH + num4 * 0.45f, Math.Min(0.6f, num * 0.12f), shell.Timber);
			Finish(shell, interior);
			interior.Top = num5;
			Layout layout = new Layout(interior, models, VariantColors("dwarf", roofColor), x, y, x2, y2);
			layout.Keep(interior.DoorX - 0.55f, y, interior.DoorX + 0.55f, y + 1.05f);
			layout.Put("int_v_hoodhearth", xm, y + num2 * 0.38f, -MathF.PI / 2f);
			if (layout.Put("int_v_feast", xm, y + num2 * 0.68f, -MathF.PI / 2f))
			{
				float[] last = layout.Last;
				for (int num12 = 0; num12 < 3; num12++)
				{
					float x4 = last[0] + (last[2] - last[0]) * ((float)num12 + 0.5f) / 3f;
					layout.Put("int_dwarf_stool", x4, last[1] - 0.22f, MathF.PI / 2f);
					layout.Put("int_dwarf_stool", x4, last[3] + 0.22f, -MathF.PI / 2f);
				}
			}
			layout.WallTop = 1.45f;
			layout.Fit("int_dwarf_kegs", 'W', y + num2 * 0.12f, 0.5f);
			layout.Fit("int_dwarf_bed", 'W', y + num2 * 0.5f, 0.6f);
			if (tier >= 2)
			{
				layout.Fit("int_dwarf_bed", 'W', y + num2 * 0.88f, 0.5f);
			}
			layout.Fit("int_dwarf_shelf", 'E', y + num2 * 0.5f, 0.6f);
			layout.Fit("int_dwarf_chest", 'E', y + num2 * 0.3f, 0.3f);
			layout.Fit("int_dwarf_rack", 'E', y + num2 * 0.88f, 0.5f);
			layout.Put("int_v_lanterns", xm, y + num2 * 0.68f, 0f, solid: false, anywhere: true);
			layout.PutNear("int_barrel", x2 - 0.3f, y2 - 0.3f, 0.6f, 0.3f);
			layout.PutNear("int_barrel", x + 0.3f, y2 - 0.3f, 1.6f, 0.3f);
			if (layout.Fit("int_p_counter", 'S', x + num * 0.22f, 0.4f))
			{
				layout.Hang("int_p_potrack", 'S', layout.LastX, layout.Height("int_p_counter") + 0.32f, 0.3f);
			}
			layout.PutNear("int_p_jars", x2 - 0.3f, y + 0.3f, MathF.PI, 0.3f);
			layout.PutNear("int_p_firewood", xm + 0.95f, y + num2 * 0.38f, MathF.PI, 0.3f);
			PlaceSpots(interior, layout);
			return interior;
		}

		private static Interior StumpRoom(int size, uint wallColor, uint roofColor, Random rng, int tier, Func<string, MeshModel> models)
		{
			Style style = StyleFor("dwarf", wallColor, roofColor);
			style.Wall = TerrainCache.Rgb(122, 86, 56);
			style.WallKind = 2;
			style.WallH = 1.5f;
			style.MeshFloor = true;
			style.Floor = TerrainCache.Rgb(150, 110, 70);
			style.Ceiling = TerrainCache.Rgb(130, 94, 60);
			style.CeilingKind = 3;
			float x;
			float y;
			float x2;
			float y2;
			Interior interior = Begin(size, size, style, "dwarf", tier, out x, out y, out x2, out y2, TerrainCache.Rgb(150, 110, 70));
			float num = (x + x2) * 0.5f;
			float num2 = (y + y2) * 0.5f;
			float num3 = (float)size * 0.5f;
			float wallH = style.WallH;
			int gold;
			int cloth;
			int water;
			int red;
			Shell shell = NewShell(style, "dwarf", x, y, x2, y2, out gold, out cloth, out water, out red);
			shell.Floor = shell.B.Slot("boards", 3, style.Floor, 0);
			List<WallSide> list = Octagon(num, num2, num3);
			AddDoor(shell, list[0], "dwarf", "dwarf_stump");
			BuildWalls(shell, interior, list, "dwarf", wallH);
			MeshBuilder b = shell.B;
			Vec3[] array = new Vec3[8];
			for (int i = 0; i < 8; i++)
			{
				array[i] = list[i].Start;
			}
			FaceToward(b, shell.Floor, new Vec3(num, num2, 1f), array);
			int[] array2 = new int[3] { 2, 4, 6 };
			foreach (int index in array2)
			{
				RoundWindow(shell, interior, list[index], list[index].Length * 0.5f, wallH * 0.58f, 0.24f, shell.Timber);
			}
			foreach (WallSide item in list)
			{
				for (float num4 = 0.3f; num4 < wallH; num4 += 0.38f)
				{
					Band(shell, item, shell.Dark, num4, 0.006f, 0.004f);
				}
				WallBox(shell, item, shell.Timber, 0.05f, wallH * 0.5f, 0.06f, wallH * 0.5f, 0.05f);
			}
			for (int k = 0; k < 5; k++)
			{
				float num5 = -0.6707964f + (float)k * 1.15f;
				Vec3 a = new Vec3(num + (float)Math.Cos(num5) * num3, num2 + (float)Math.Sin(num5) * num3, 0.25f);
				Vec3 vec = new Vec3(num + (float)Math.Cos(num5 + 0.12f) * num3 * 0.82f, num2 + (float)Math.Sin(num5 + 0.12f) * num3 * 0.82f, 0.06f);
				Vec3 b2 = new Vec3(num + (float)Math.Cos(num5 + 0.2f) * num3 * 0.68f, num2 + (float)Math.Sin(num5 + 0.2f) * num3 * 0.68f, -0.02f);
				b.Tube(shell.Bark, a, vec, 0.09f, 0.06f, 6);
				b.Tube(shell.Bark, vec, b2, 0.06f, 0.02f, 5);
			}
			Vec3[] array3 = new Vec3[8];
			for (int l = 0; l < 8; l++)
			{
				array3[l] = list[l].Start + new Vec3(0f, 0f, wallH);
			}
			FaceToward(b, shell.Ceiling, new Vec3(num, num2, 0f), array3);
			for (int m = 0; m < 8; m++)
			{
				Beam(b, shell.Timber, new Vec3(num, num2, wallH - 0.07f), list[m].Start + (new Vec3(num, num2, 0f) - list[m].Start).Normalized() * 0.05f + new Vec3(0f, 0f, wallH - 0.07f), 0.055f);
			}
			Finish(shell, interior);
			interior.Top = wallH;
			OctagonCorners(interior, num, num2, num3);
			interior.DoorX = num;
			interior.DoorY = num2 - num3 + 0.45f;
			interior.SpawnX = num;
			interior.SpawnY = num2 - num3 + 0.75f;
			Layout layout = new Layout(interior, models, VariantColors("dwarf", roofColor), x, y, x2, y2);
			layout.Keep(num - 0.5f, y, num + 0.5f, y + 1.05f);
			layout.Put("int_v_spiral", num + 0.15f, num2 + 0.35f, -MathF.PI / 2f);
			string[] array4 = new string[7] { "int_v_stove", "int_barrel", "int_dwarf_shelf", "int_dwarf_chest", "int_v_alcove", "int_p_jars", "int_dwarf_kegs" };
			for (int n = 1; n < 8; n++)
			{
				string name = array4[n - 1];
				float num6 = -MathF.PI / 2f + (float)n * MathF.PI / 4f;
				float num7 = layout.Depth(name) * 0.5f + 0.06f;
				float num8 = num3 - num7 - 0.08f;
				layout.PutNear(name, num + (float)Math.Cos(num6) * num8, num2 + (float)Math.Sin(num6) * num8, num6 + MathF.PI, 0.2f);
			}
			layout.Put("int_orc_rug", num - 0.6f, num2 - 0.7f, 0.3f, solid: false, anywhere: true);
			if (layout.Put("int_dwarf_table", num - 0.9f, num2 - 0.35f, -MathF.PI / 2f))
			{
				layout.Put("int_dwarf_stool", layout.Last[2] + 0.25f, layout.LastY, MathF.PI);
			}
			PlaceSpots(interior, layout);
			return interior;
		}

		private static Interior TentRoom(string style, uint wallColor, uint roofColor, Random rng, Func<string, MeshModel> models)
		{
			string text = (style.Contains("elf") ? "elf" : (style.Contains("orc") ? "orc" : (style.Contains("dwarf") ? "dwarf" : "human")));
			int num;
			switch (style)
			{
			default:
				num = ((style == "tent_elf_b") ? 1 : 0);
				break;
			case "tent_human_b":
			case "tent_elf":
			case "tent_orc":
				num = 1;
				break;
			}
			bool flag = (byte)num != 0;
			Style style2 = StyleFor(text, wallColor, roofColor);
			style2.WallKind = 7;
			style2.CeilingKind = 7;
			uint num2 = (style2.Wall = ((text == "orc") ? TerrainCache.Rgb(150, 112, 78) : ((text == "elf" && style == "tent_elf_b") ? TerrainCache.Rgb(70, 120, 60) : ((text == "elf") ? TerrainCache.Rgb(226, 230, 214) : TerrainCache.Rgb(222, 210, 186)))));
			style2.Ceiling = TerrainCache.Lerp(num2, roofColor, (text == "elf" && style == "tent_elf") ? 0.5f : 0.15f);
			style2.WallH = (flag ? 0.55f : ((text == "dwarf") ? 0.5f : 0.2f));
			style2.MeshFloor = false;
			style2.FloorSurface = 3;
			style2.Floor = TerrainCache.Rgb(120, 98, 72);
			style2.WinW = 0.25f;
			int roomD = (flag ? 4 : 3);
			float x;
			float y;
			float x2;
			float y2;
			Interior interior = Begin(4, roomD, style2, text, 0, out x, out y, out x2, out y2);
			int gold;
			int cloth;
			int water;
			int red;
			Shell shell = NewShell(style2, text, x, y, x2, y2, out gold, out cloth, out water, out red);
			float num3 = x2 - x;
			float H = style2.WallH;
			float num4 = (x + x2) * 0.5f;
			float num5 = (y + y2) * 0.5f;
			MeshBuilder b = shell.B;
			int toggleSlot = ((text == "orc") ? b.Slot("bone", 0, TerrainCache.Rgb(226, 218, 196), 0) : shell.Timber);
			if (flag)
			{
				List<WallSide> list = Octagon(num4, num5, num3 * 0.5f);
				list[0].Holes.Add(new Hole
				{
					A0 = list[0].Length / 2f - 0.36f,
					A1 = list[0].Length / 2f + 0.36f,
					Z0 = 0f,
					Z1 = H,
					Door = true,
					Flap = true
				});
				BuildWalls(shell, interior, list, text, H);
				float num6 = num3 * 0.5f;
				float up = 1.15f - H;
				List<(float, float)> list2 = new List<(float, float)>
				{
					(0f, 0f),
					(0f, H)
				};
				if (style == "tent_elf_b")
				{
					float num7 = num3 * 0.42f;
					Dome(shell, list, num4, num5, H, num7, shell.Bark, 0f);
					for (int i = 1; i <= 6; i++)
					{
						float num8 = (float)i / 6f * MathF.PI * 0.5f;
						float num9 = H + num7 * (float)Math.Sin(num8);
						float num10 = num6 * (1f - (float)Math.Cos(num8));
						if (num9 < H + up)
						{
							list2.Add((num10, num9));
							continue;
						}
						(float, float) tuple = list2[list2.Count - 1];
						float num11 = (H + up - tuple.Item2) / (num9 - tuple.Item2);
						list2.Add((tuple.Item1 + (num10 - tuple.Item1) * num11, H + up));
						break;
					}
				}
				else
				{
					float num12 = num3 * 0.55f;
					HideCone(shell, list, num4, num5, H, num12, (style == "tent_orc") ? 0.1f : 0.02f);
					list2.Add((num6 * up / num12, H + up));
				}
				TentFlap(shell, list[0], list2, (float z) => (!(z <= H)) ? (0.38f - 0.14f * (z - H) / up) : 0.38f, num2, toggleSlot);
				if (style == "tent_human_b" || style == "tent_elf")
				{
					b.Tube((style == "tent_elf") ? gold : shell.Timber, new Vec3(num4, num5, 0f), new Vec3(num4, num5, H + num3 * 0.55f), 0.05f, 0.04f, 6);
				}
				OctagonCorners(interior, num4, num5, num3 * 0.5f);
				interior.Top = H + num3 * 0.55f;
			}
			else
			{
				shell.South.Holes.Add(new Hole
				{
					A0 = num3 / 2f - 0.36f,
					A1 = num3 / 2f + 0.36f,
					Z0 = 0f,
					Z1 = H,
					Door = true,
					Flap = true
				});
				BuildWalls(shell, interior, shell.All, text, H);
				float ridge = H + num3 * 0.42f;
				FaceToward(b, shell.Ceiling, new Vec3(num4, num5, H), new Vec3(x, y, H), new Vec3(x, y2, H), new Vec3(num4, y2, ridge), new Vec3(num4, y, ridge));
				FaceToward(b, shell.Ceiling, new Vec3(num4, num5, H), new Vec3(x2, y, H), new Vec3(x2, y2, H), new Vec3(num4, y2, ridge), new Vec3(num4, y, ridge));
				FaceToward(b, shell.Wall, new Vec3(num4, num5, H), new Vec3(x, y2, H), new Vec3(x2, y2, H), new Vec3(num4, y2, ridge));
				FaceToward(b, shell.Wall, new Vec3(num4, num5, H), new Vec3(x, y, H), new Vec3(num4 - 0.3f, y, H), new Vec3(num4, y, ridge));
				FaceToward(b, shell.Wall, new Vec3(num4, num5, H), new Vec3(num4 + 0.3f, y, H), new Vec3(x2, y, H), new Vec3(num4, y, ridge));
				TentFlap(shell, shell.South, new List<(float, float)>
				{
					(0f, 0f),
					(0f, H),
					(0f, ridge - 0.05f)
				}, (float z) => (!(z <= H)) ? (0.3f * (ridge - z) / (ridge - H) + 0.04f) : 0.38f, num2, toggleSlot);
				b.Tube(shell.Timber, new Vec3(num4, y - 0.05f, ridge - 0.04f), new Vec3(num4, y2 + 0.05f, ridge - 0.04f), 0.035f, 0.035f, 6);
				float[] array = new float[2]
				{
					y - 0.06f,
					y2 - 0.05f
				};
				foreach (float y3 in array)
				{
					b.Tube(shell.Timber, new Vec3(num4, y3, 0f), new Vec3(num4, y3, ridge), 0.04f, 0.04f, 6);
				}
				if (text == "dwarf")
				{
					WallSide[] all = shell.All;
					foreach (WallSide wallSide in all)
					{
						for (float num14 = 0.1f; num14 < H; num14 += 0.2f)
						{
							float num15 = 0f;
							foreach (Hole hole in wallSide.Holes)
							{
								if (hole.A0 - num15 > 0.01f)
								{
									WallBox(shell, wallSide, shell.Bark, (num15 + hole.A0) * 0.5f, num14, (hole.A0 - num15) * 0.5f, 0.09f, 0.05f);
								}
								num15 = hole.A1;
							}
							if (wallSide.Length - num15 > 0.01f)
							{
								WallBox(shell, wallSide, shell.Bark, (num15 + wallSide.Length) * 0.5f, num14, (wallSide.Length - num15) * 0.5f, 0.09f, 0.05f);
							}
						}
					}
				}
				interior.Top = ridge;
			}
			interior.Windows.Add(new LightWindow
			{
				Center = new Vec3(num4, y + 0.05f, H * 0.6f + 0.2f),
				Right = new Vec3(-1f, 0f, 0f),
				HalfWidth = 0.35f,
				HalfHeight = 0.4f,
				Inward = new Vec3(0f, 1f, 0f)
			});
			Finish(shell, interior);
			Layout layout = new Layout(interior, models, VariantColors(text, roofColor), x, y, x2, y2);
			layout.Keep(num4 - 0.5f, y, num4 + 0.5f, y + 1f);
			if (flag)
			{
				layout.Put("int_v_bedroll", num4 - 0.75f, num5 + 0.35f, 0f, solid: false);
				object name;
				switch (text)
				{
				default:
					name = "int_human_chest";
					break;
				case "dwarf":
					name = "int_dwarf_chest";
					break;
				case "elf":
					name = "int_elf_chest";
					break;
				case "orc":
					name = "int_orc_chest";
					break;
				}
				layout.Put((string)name, num4 + 0.85f, num5 + 0.6f, MathF.PI);
				layout.Put((text == "elf") ? "int_v_cushions" : ((text == "orc") ? "int_orc_drum" : "int_sack"), num4 + 0.85f, num5 - 0.45f, MathF.PI * 4f / 5f);
				if (text == "orc")
				{
					layout.Put("int_orc_rug", num4, num5, -MathF.PI / 2f, solid: false, anywhere: true);
				}
				if (text == "elf")
				{
					layout.Put("int_elf_lantern", num4 - 0.9f, num5 - 0.7f, 0.3f);
				}
			}
			else
			{
				layout.Put("int_v_bedroll", x + layout.Depth("int_v_bedroll") * 0.5f + 0.05f, num5 + 0.2f, 0f, solid: false);
				layout.AgainstWall((text == "dwarf") ? "int_dwarf_chest" : "int_human_chest", 'N', num4 + 0.7f);
				if (text == "dwarf")
				{
					layout.AgainstWall("int_v_stove", 'E', num5 + 0.3f);
				}
				else
				{
					layout.Put("int_barrel", x2 - 0.3f, y2 - 0.35f, 0.2f);
				}
				layout.Put("int_sack", x2 - 0.35f, y + 0.45f, 0.6f);
			}
			if (text != "elf" && text != "dwarf")
			{
				layout.Put("int_v_lamp", num4 + (flag ? (-0.2f) : 0.5f), num5 - 0.1f, 0f, solid: false, anywhere: true);
			}
			PlaceSpots(interior, layout);
			return interior;
		}
	}
}
