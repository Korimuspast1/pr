using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace FirstPerson.Core
{
	public static class FlagSpots
	{
		internal sealed class Geometry
		{
			public readonly MeshModel M;

			public readonly float[] P;

			public readonly byte[] Kind;

			public readonly int Count;

			public readonly float Width;

			public const float TopCell = 0.1f;

			public readonly float TopX0;

			public readonly float TopY0;

			public readonly int TopW;

			public readonly int TopH;

			public readonly float[] Top;

			public float DoorX;

			public float DoorFront;

			public float DoorTop;

			public float DoorHalf;

			public bool HasDoor;

			public const float FrontCell = 0.08f;

			public float FrontX0;

			public int FrontW;

			public int FrontH;

			public float[] Depth;

			public bool[] Cloth;

			public bool[] Glass;

			public Geometry(MeshModel m)
			{
				M = m;
				Width = m.InfoFloat("width", m.Max.X - m.Min.X);
				int num = 0;
				MeshPart[] parts = m.Parts;
				foreach (MeshPart meshPart in parts)
				{
					num += meshPart.TriangleCount;
				}
				P = new float[num * 9];
				Kind = new byte[num];
				byte[] array = new byte[m.SlotNames.Length];
				for (int j = 0; j < array.Length; j++)
				{
					string text = m.SlotNames[j];
					byte b = (byte)((m.SlotKind != null && j < m.SlotKind.Length) ? m.SlotKind[j] : 0);
					byte b2 = 0;
					if (!Starts(text, NoRoofSlots) && b != 7 && b != 1 && b != 11)
					{
						b2 |= 1;
					}
					if (!Starts(text, NoWallSlots) && b != 1 && b != 11)
					{
						b2 |= 2;
					}
					if (Starts(text, new string[1] { "banner" }))
					{
						b2 |= 4;
					}
					if (Starts(text, new string[1] { "window" }))
					{
						b2 |= 8;
					}
					if (text == "door")
					{
						b2 |= 0x10;
					}
					array[j] = b2;
				}
				int num2 = 0;
				parts = m.Parts;
				foreach (MeshPart meshPart2 in parts)
				{
					byte b3 = (byte)(Starts(meshPart2.Name, MovingParts) ? 32 : 0);
					int num3 = 0;
					while (num3 < meshPart2.TriangleCount)
					{
						for (int k = 0; k < 3; k++)
						{
							int num4 = meshPart2.Tri[num3 * 3 + k] * 3;
							P[num2 * 9 + k * 3] = meshPart2.Pos[num4];
							P[num2 * 9 + k * 3 + 1] = meshPart2.Pos[num4 + 1];
							P[num2 * 9 + k * 3 + 2] = meshPart2.Pos[num4 + 2];
						}
						byte b4 = (byte)((meshPart2.Slot[num3] < array.Length) ? array[meshPart2.Slot[num3]] : 3);
						if (b3 != 0)
						{
							b4 = (byte)((b4 & -4) | 0x20);
						}
						Kind[num2] = b4;
						num3++;
						num2++;
					}
				}
				Count = num2;
				TopX0 = m.Min.X - 1f;
				TopY0 = m.Min.Y - 1f;
				TopW = (int)Math.Ceiling((m.Max.X - m.Min.X + 2f) / 0.1f) + 1;
				TopH = (int)Math.Ceiling((m.Max.Y - m.Min.Y + 2f) / 0.1f) + 1;
				Top = new float[TopW * TopH];
				for (int l = 0; l < Top.Length; l++)
				{
					Top[l] = float.MinValue;
				}
				for (int n = 0; n < Count; n++)
				{
					RasterTop(n);
				}
			}

			private void RasterTop(int t)
			{
				int num = t * 9;
				float num2 = P[num];
				float num3 = P[num + 1];
				float num4 = P[num + 2];
				float num5 = P[num + 3];
				float num6 = P[num + 4];
				float num7 = P[num + 5];
				float num8 = P[num + 6];
				float num9 = P[num + 7];
				float num10 = P[num + 8];
				for (int i = 0; i < 3; i++)
				{
					float num11 = P[num + i * 3];
					float num12 = P[num + i * 3 + 1];
					float num13 = P[num + i * 3 + 2];
					float num14 = P[num + (i + 1) % 3 * 3];
					float num15 = P[num + (i + 1) % 3 * 3 + 1];
					float num16 = P[num + (i + 1) % 3 * 3 + 2];
					float num17 = (float)Math.Sqrt((num14 - num11) * (num14 - num11) + (num15 - num12) * (num15 - num12));
					int num18 = Math.Max(1, (int)Math.Ceiling(num17 / 0.05f));
					for (int j = 0; j <= num18; j++)
					{
						float num19 = (float)j / (float)num18;
						PutTop(num11 + (num14 - num11) * num19, num12 + (num15 - num12) * num19, num13 + (num16 - num13) * num19);
					}
				}
				float num20 = (num5 - num2) * (num9 - num3) - (num6 - num3) * (num8 - num2);
				if (Math.Abs(num20) < 1E-08f)
				{
					return;
				}
				int num21 = Math.Max(0, (int)((Math.Min(num2, Math.Min(num5, num8)) - TopX0) / 0.1f));
				int num22 = Math.Min(TopW - 1, (int)((Math.Max(num2, Math.Max(num5, num8)) - TopX0) / 0.1f));
				int num23 = Math.Max(0, (int)((Math.Min(num3, Math.Min(num6, num9)) - TopY0) / 0.1f));
				int num24 = Math.Min(TopH - 1, (int)((Math.Max(num3, Math.Max(num6, num9)) - TopY0) / 0.1f));
				for (int k = num23; k <= num24; k++)
				{
					for (int l = num21; l <= num22; l++)
					{
						float num25 = TopX0 + ((float)l + 0.5f) * 0.1f;
						float num26 = TopY0 + ((float)k + 0.5f) * 0.1f;
						float num27 = ((num25 - num2) * (num9 - num3) - (num26 - num3) * (num8 - num2)) / num20;
						float num28 = ((num5 - num2) * (num26 - num3) - (num6 - num3) * (num25 - num2)) / num20;
						if (!(num27 < 0f) && !(num28 < 0f) && !(num27 + num28 > 1f))
						{
							float num29 = num4 + (num7 - num4) * num27 + (num10 - num4) * num28;
							int num30 = k * TopW + l;
							if (num29 > Top[num30])
							{
								Top[num30] = num29;
							}
						}
					}
				}
			}

			private void PutTop(float x, float y, float z)
			{
				int num = (int)((x - TopX0) / 0.1f);
				int num2 = (int)((y - TopY0) / 0.1f);
				if (num >= 0 && num2 >= 0 && num < TopW && num2 < TopH)
				{
					int num3 = num2 * TopW + num;
					if (z > Top[num3])
					{
						Top[num3] = z;
					}
				}
			}

			public float TopAt(float x, float y)
			{
				int num = (int)((x - TopX0) / 0.1f);
				int num2 = (int)((y - TopY0) / 0.1f);
				if (num >= 0 && num2 >= 0 && num < TopW && num2 < TopH)
				{
					return Top[num2 * TopW + num];
				}
				return float.MinValue;
			}

			public float HighestNear(float x, float y, float r, float above)
			{
				float num = float.MinValue;
				int num2 = Math.Max(0, (int)((x - r - TopX0) / 0.1f));
				int num3 = Math.Min(TopW - 1, (int)((x + r - TopX0) / 0.1f));
				int num4 = Math.Max(0, (int)((y - r - TopY0) / 0.1f));
				int num5 = Math.Min(TopH - 1, (int)((y + r - TopY0) / 0.1f));
				for (int i = num4; i <= num5; i++)
				{
					float num6 = TopY0 + ((float)i + 0.5f) * 0.1f - y;
					for (int j = num2; j <= num3; j++)
					{
						float num7 = TopX0 + ((float)j + 0.5f) * 0.1f - x;
						if (!(num7 * num7 + num6 * num6 > r * r))
						{
							float num8 = Top[i * TopW + j];
							if (num8 > above && num8 > num)
							{
								num = num8;
							}
						}
					}
				}
				return num;
			}

			public Vec3 Roof(StringBuilder why)
			{
				float num = 0.5f * (M.Min.Y + M.Max.Y);
				Vec3 vec = new Vec3(0f, num, M.Max.Z);
				float num2 = float.MinValue;
				for (int i = 0; i < Count; i++)
				{
					if ((Kind[i] & 1) == 0)
					{
						continue;
					}
					for (int j = 0; j < 3; j++)
					{
						float x = P[i * 9 + j * 3];
						float y = P[i * 9 + j * 3 + 1];
						float num3 = P[i * 9 + j * 3 + 2];
						if (!(TopAt(x, y) > num3 + 0.15f))
						{
							float num4 = Score(x, y, num3, num);
							if (num4 > num2)
							{
								num2 = num4;
								vec = new Vec3(x, y, num3);
							}
						}
					}
				}
				why.Append("roof ").Append(V(vec));
				bool flag = M.Name != null && (M.Name.StartsWith("tw_", StringComparison.Ordinal) || M.Name.StartsWith("ruin_tw_", StringComparison.Ordinal));
				List<Vec3> list = new List<Vec3>();
				for (int k = 0; k < Count; k++)
				{
					if ((Kind[k] & 4) == 0)
					{
						continue;
					}
					for (int l = 0; l < 3; l++)
					{
						float num5 = P[k * 9 + l * 3 + 2];
						bool flag2 = (Kind[k] & 0x20) != 0;
						bool flag3 = !flag && num5 >= vec.Z - 0.8f;
						if (num5 > 0.8f * M.Max.Z && (flag2 || flag3))
						{
							list.Add(new Vec3(P[k * 9 + l * 3], P[k * 9 + l * 3 + 1], num5));
						}
					}
				}
				if (list.Count == 0 || Far(list, vec.X, vec.Y, 1.2f))
				{
					return vec;
				}
				Vec3 vec2 = vec;
				float num6 = float.MinValue;
				for (int m = 0; m < Count; m++)
				{
					if ((Kind[m] & 1) == 0)
					{
						continue;
					}
					for (int n = 0; n < 3; n++)
					{
						float x2 = P[m * 9 + n * 3];
						float y2 = P[m * 9 + n * 3 + 1];
						float num7 = P[m * 9 + n * 3 + 2];
						if (!(TopAt(x2, y2) > num7 + 0.15f) && Far(list, x2, y2, 1.5f))
						{
							float num8 = Score(x2, y2, num7, num);
							if (num8 > num6)
							{
								num6 = num8;
								vec2 = new Vec3(x2, y2, num7);
							}
						}
					}
				}
				if (num6 == float.MinValue)
				{
					return vec;
				}
				why.Append(" off its own flag to ").Append(V(vec2));
				return vec2;
			}

			private static float Score(float x, float y, float z, float yc)
			{
				return z - 0.55f * Math.Abs(x) - 0.35f * Math.Abs(y - yc);
			}

			private static bool Far(List<Vec3> pts, float x, float y, float d)
			{
				foreach (Vec3 pt in pts)
				{
					if ((pt.X - x) * (pt.X - x) + (pt.Y - y) * (pt.Y - y) < d * d)
					{
						return false;
					}
				}
				return true;
			}

			public float Clearance(Vec3 foot, float r, float r0, FlagShape shape, out float scale)
			{
				float num = 1.26f * r;
				float num2 = FlagCloth.LengthOf(shape) * num;
				float num3 = 0.12f * r;
				float num4 = 3f * r;
				float above = foot.Z + 0.3f;
				scale = 1f;
				float num5 = HighestNear(foot.X, foot.Y, num2 + 0.3f, above);
				if (num5 == float.MinValue || num5 <= foot.Z + num4 - num3 - num - 0.2f)
				{
					return num4;
				}
				float num6 = num5 - foot.Z + num3 + num + 0.25f;
				if (num6 <= 1.6f * num4)
				{
					return num6;
				}
				num6 = 1.6f * num4;
				float num7 = 0.3f;
				float num8 = 1f;
				for (int i = 0; i < 18; i++)
				{
					float num9 = 0.5f * (num7 + num8);
					float num10 = HighestNear(foot.X, foot.Y, num9 * num2 + 0.3f, above);
					if (num10 == float.MinValue || num10 <= foot.Z + num6 - num3 - num9 * num - 0.2f)
					{
						num7 = num9;
					}
					else
					{
						num8 = num9;
					}
				}
				scale = num7;
				return num6;
			}

			public float RuinPole(Vec3 foot, float r, float lean, FlagShape shape, out float scale)
			{
				float h = 1.26f * r;
				float l = 0.78f * FlagCloth.LengthOf(shape) * h;
				float drop = 0.12f * r;
				float num = 3f * r;
				float num2 = num / 0.55f;
				float sin = (float)Math.Sin(lean);
				float cos = (float)Math.Cos(lean);
				scale = 1f;
				for (int i = 0; i <= 12; i++)
				{
					float num3 = num + (num2 - num) * (float)i / 12f;
					if (Clear(num3, 1f))
					{
						return num3;
					}
				}
				float num4 = 0.3f;
				float num5 = 1f;
				for (int j = 0; j < 18; j++)
				{
					float num6 = 0.5f * (num4 + num5);
					if (Clear(num2, num6))
					{
						num4 = num6;
					}
					else
					{
						num5 = num6;
					}
				}
				scale = num4;
				return num2;
				bool Clear(float pole, float c)
				{
					float num7 = 0.55f * pole;
					float x = foot.X + num7 * sin;
					float num8 = foot.Z + num7 * cos - drop - c * h;
					float num9 = HighestNear(x, foot.Y, c * l + 0.3f, foot.Z + 0.3f);
					if (num9 != float.MinValue)
					{
						return num9 <= num8 - 0.2f;
					}
					return true;
				}
			}

			public bool Swept(Vec3 foot, float pole, float r, FlagShape shape, float scale, out float worst)
			{
				float num = 1.26f * r * scale;
				float num2 = FlagCloth.LengthOf(shape) * 1.26f * r * scale;
				float num3 = foot.Z + pole - 0.12f * r - num;
				worst = HighestNear(foot.X, foot.Y, num2 + 0.3f, foot.Z + 0.3f);
				if (worst != float.MinValue)
				{
					return worst <= num3 - 0.15f;
				}
				return true;
			}

			public void Maps()
			{
				if (Depth == null)
				{
					FindDoor();
					BuildFront();
				}
			}

			private void FindDoor()
			{
				float num = float.MaxValue;
				for (int i = 0; i < Count; i++)
				{
					if ((Kind[i] & 0x10) != 0)
					{
						for (int j = 0; j < 3; j++)
						{
							num = Math.Min(num, P[i * 9 + j * 3 + 2]);
						}
					}
				}
				float num2 = 0f;
				float num3 = float.MaxValue;
				float num4 = float.MinValue;
				float num5 = float.MaxValue;
				float num6 = float.MinValue;
				int num7 = 0;
				for (int k = 0; k < Count; k++)
				{
					if ((Kind[k] & 0x10) != 0 && !(Math.Min(P[k * 9 + 2], Math.Min(P[k * 9 + 5], P[k * 9 + 8])) > num + 0.35f))
					{
						for (int l = 0; l < 3; l++)
						{
							float num8 = P[k * 9 + l * 3];
							float val = P[k * 9 + l * 3 + 1];
							float val2 = P[k * 9 + l * 3 + 2];
							num2 += num8;
							num3 = Math.Min(num3, num8);
							num4 = Math.Max(num4, num8);
							num5 = Math.Min(num5, val);
							num6 = Math.Max(num6, val2);
							num7++;
						}
					}
				}
				HasDoor = num7 > 0;
				if (HasDoor)
				{
					DoorX = num2 / (float)num7;
					DoorFront = num5;
					DoorTop = num6;
					DoorHalf = 0.5f * (num4 - num3);
					return;
				}
				DoorX = 0f;
				DoorFront = float.MaxValue;
				for (int m = 0; m < Count; m++)
				{
					for (int n = 0; n < 3; n++)
					{
						float value = P[m * 9 + n * 3];
						float val3 = P[m * 9 + n * 3 + 1];
						float num9 = P[m * 9 + n * 3 + 2];
						if (Math.Abs(value) < 1f && num9 > 0.2f && num9 < 2f && (Kind[m] & 2) != 0)
						{
							DoorFront = Math.Min(DoorFront, val3);
						}
					}
				}
				DoorFront = ((DoorFront == float.MaxValue) ? M.Min.Y : DoorFront);
				DoorTop = Math.Max(1.8f, Math.Min(3f, 0.25f * M.Max.Z));
				DoorHalf = 0.6f;
			}

			private void BuildFront()
			{
				FrontX0 = M.Min.X;
				FrontW = Math.Max(1, (int)Math.Ceiling((M.Max.X - M.Min.X) / 0.08f));
				FrontH = Math.Max(1, (int)Math.Ceiling(Math.Max(0.5f, M.Max.Z) / 0.08f));
				Depth = new float[FrontW * FrontH];
				Cloth = new bool[FrontW * FrontH];
				Glass = new bool[FrontW * FrontH];
				for (int i = 0; i < Depth.Length; i++)
				{
					Depth[i] = float.MaxValue;
				}
				float num = DoorFront + 3f;
				for (int j = 0; j < Count; j++)
				{
					int num2 = j * 9;
					if (Math.Min(P[num2 + 1], Math.Min(P[num2 + 4], P[num2 + 7])) > num)
					{
						continue;
					}
					byte b = Kind[j];
					float num3 = P[num2 + 3] - P[num2];
					float num4 = P[num2 + 4] - P[num2 + 1];
					float num5 = P[num2 + 5] - P[num2 + 2];
					float num6 = P[num2 + 6] - P[num2];
					float num7 = P[num2 + 7] - P[num2 + 1];
					float num8 = P[num2 + 8] - P[num2 + 2];
					float num9 = num5 * num6 - num3 * num8;
					float num10 = (float)Math.Sqrt((num4 * num8 - num5 * num7) * (num4 * num8 - num5 * num7) + num9 * num9 + (num3 * num7 - num4 * num6) * (num3 * num7 - num4 * num6));
					if ((b & 4) != 0)
					{
						RasterFront(num2, Cloth, depth: false);
					}
					else if ((b & 8) != 0)
					{
						if (num10 > 0f && num9 / num10 < -0.05f)
						{
							RasterFront(num2, Glass, depth: false);
						}
					}
					else if ((b & 2) != 0 && (b & 0x20) == 0 && num10 > 0f && num9 / num10 < -0.05f)
					{
						RasterFront(num2, null, depth: true);
					}
				}
			}

			private void RasterFront(int o, bool[] mask, bool depth)
			{
				float num = P[o];
				float num2 = P[o + 1];
				float num3 = P[o + 2];
				float num4 = P[o + 3];
				float num5 = P[o + 4];
				float num6 = P[o + 5];
				float num7 = P[o + 6];
				float num8 = P[o + 7];
				float num9 = P[o + 8];
				float num10 = (num4 - num) * (num9 - num3) - (num6 - num3) * (num7 - num);
				if (Math.Abs(num10) < 1E-08f)
				{
					return;
				}
				int num11 = Math.Max(0, (int)((Math.Min(num, Math.Min(num4, num7)) - FrontX0) / 0.08f));
				int num12 = Math.Min(FrontW - 1, (int)((Math.Max(num, Math.Max(num4, num7)) - FrontX0) / 0.08f));
				int num13 = Math.Max(0, (int)(Math.Min(num3, Math.Min(num6, num9)) / 0.08f));
				int num14 = Math.Min(FrontH - 1, (int)(Math.Max(num3, Math.Max(num6, num9)) / 0.08f));
				for (int i = num13; i <= num14; i++)
				{
					for (int j = num11; j <= num12; j++)
					{
						float num15 = FrontX0 + ((float)j + 0.5f) * 0.08f;
						float num16 = ((float)i + 0.5f) * 0.08f;
						float num17 = ((num15 - num) * (num9 - num3) - (num16 - num3) * (num7 - num)) / num10;
						float num18 = ((num4 - num) * (num16 - num3) - (num6 - num3) * (num15 - num)) / num10;
						if (num17 < 0f || num18 < 0f || num17 + num18 > 1f)
						{
							continue;
						}
						int num19 = i * FrontW + j;
						if (mask != null)
						{
							mask[num19] = true;
						}
						if (depth)
						{
							float num20 = num2 + (num5 - num2) * num17 + (num8 - num2) * num18;
							if (num20 < Depth[num19])
							{
								Depth[num19] = num20;
							}
						}
					}
				}
			}

			public bool Wall(float x0, float x1, float z0, float z1, bool noGlass, out float near, out float far)
			{
				near = float.MaxValue;
				far = float.MinValue;
				int num = (int)Math.Floor((x0 - FrontX0) / 0.08f);
				int num2 = (int)Math.Floor((x1 - FrontX0) / 0.08f);
				int num3 = (int)Math.Floor(z0 / 0.08f);
				int num4 = (int)Math.Floor(z1 / 0.08f);
				if (num < 0 || num3 < 0 || num2 >= FrontW || num4 >= FrontH || num2 < num || num4 < num3)
				{
					return false;
				}
				if (ClothNear(x0, x1, z0, z1))
				{
					return false;
				}
				for (int i = num3; i <= num4; i++)
				{
					for (int j = num; j <= num2; j++)
					{
						int num5 = i * FrontW + j;
						if (noGlass && Glass[num5])
						{
							return false;
						}
						float num6 = Depth[num5];
						if (num6 == float.MaxValue)
						{
							return false;
						}
						if (num6 < near)
						{
							near = num6;
						}
						if (num6 > far)
						{
							far = num6;
						}
					}
				}
				return true;
			}

			public bool ClothNear(float x0, float x1, float z0, float z1)
			{
				int num = Math.Max(0, (int)Math.Floor((x0 - 0.25f - FrontX0) / 0.08f));
				int num2 = Math.Min(FrontW - 1, (int)Math.Floor((x1 + 0.25f - FrontX0) / 0.08f));
				int num3 = Math.Max(0, (int)Math.Floor((z0 - 0.25f) / 0.08f));
				int num4 = Math.Min(FrontH - 1, (int)Math.Floor((z1 + 0.25f) / 0.08f));
				for (int i = num3; i <= num4; i++)
				{
					for (int j = num; j <= num2; j++)
					{
						if (Cloth[i * FrontW + j])
						{
							return true;
						}
					}
				}
				return false;
			}

			public bool Open(float x0, float x1, float z0, float z1, float y)
			{
				if (ClothNear(x0, x1, z0, z1))
				{
					return false;
				}
				int num = (int)Math.Ceiling((x0 - FrontX0) / 0.08f - 0.5f);
				int num2 = (int)Math.Floor((x1 - FrontX0) / 0.08f - 0.5f);
				int num3 = Math.Max(0, (int)Math.Ceiling(z0 / 0.08f - 0.5f));
				int num4 = (int)Math.Floor(z1 / 0.08f - 0.5f);
				for (int i = num3; i <= num4; i++)
				{
					for (int j = num; j <= num2; j++)
					{
						if (j >= 0 && j < FrontW && i < FrontH)
						{
							int num5 = i * FrontW + j;
							if (Depth[num5] < y + 0.08f)
							{
								return false;
							}
						}
					}
				}
				return true;
			}

			public float FloorUnder(float x, float y, float r)
			{
				float floor = 0f;
				Samples(x, y, r, r, float.MinValue, 1.2f, round: true, delegate(float px, float py, float pz)
				{
					if (pz > floor)
					{
						floor = pz;
					}
					return false;
				});
				return floor;
			}

			public bool Occupied(float x, float y, float hx, float hy, float z0, float z1)
			{
				return Samples(x, y, hx, hy, z0, z1, round: false, (float px, float py, float pz) => true);
			}

			public bool StandardFits(float x, float y, float p, bool sky, out float floor)
			{
				floor = FloorUnder(x, y, 0.3f);
				float hx = Math.Max(22f, 14f) * p + 0.1f;
				float num = floor + 21f * p - 0.1f;
				float num2 = floor + 64f * p + 0.3f;
				if (Occupied(x, y, 0.3f, 0.3f, floor + 0.05f, num))
				{
					return false;
				}
				return !Occupied(x, y, hx, 0.3f, num, sky ? float.MaxValue : num2);
			}

			private bool Samples(float x, float y, float hx, float hy, float z0, float z1, bool round, Func<float, float, float, bool> hit)
			{
				for (int i = 0; i < Count; i++)
				{
					int num = i * 9;
					float num2 = Math.Min(P[num], Math.Min(P[num + 3], P[num + 6]));
					float num3 = Math.Max(P[num], Math.Max(P[num + 3], P[num + 6]));
					float num4 = Math.Min(P[num + 1], Math.Min(P[num + 4], P[num + 7]));
					float num5 = Math.Max(P[num + 1], Math.Max(P[num + 4], P[num + 7]));
					float num6 = Math.Min(P[num + 2], Math.Min(P[num + 5], P[num + 8]));
					float num7 = Math.Max(P[num + 2], Math.Max(P[num + 5], P[num + 8]));
					if (num3 < x - hx || num2 > x + hx || num5 < y - hy || num4 > y + hy || num6 >= z1 || num7 <= z0)
					{
						continue;
					}
					float num8 = Math.Max(Math.Max(num3 - num2, num5 - num4), num7 - num6);
					int num9 = Math.Max(2, Math.Min(400, (int)Math.Ceiling(num8 / 0.06f)));
					for (int j = 0; j <= num9; j++)
					{
						for (int k = 0; j + k <= num9; k++)
						{
							float num10 = (float)j / (float)num9;
							float num11 = (float)k / (float)num9;
							float num12 = 1f - num10 - num11;
							float num13 = P[num] * num12 + P[num + 3] * num10 + P[num + 6] * num11;
							float num14 = P[num + 1] * num12 + P[num + 4] * num10 + P[num + 7] * num11;
							bool num15;
							if (!round)
							{
								if (Math.Abs(num13 - x) > hx)
								{
									continue;
								}
								num15 = Math.Abs(num14 - y) > hy;
							}
							else
							{
								num15 = (num13 - x) * (num13 - x) + (num14 - y) * (num14 - y) > hx * hx;
							}
							if (!num15)
							{
								float num16 = P[num + 2] * num12 + P[num + 5] * num10 + P[num + 8] * num11;
								if (!(num16 <= z0) && !(num16 >= z1) && hit(num13, num14, num16))
								{
									return true;
								}
							}
						}
					}
				}
				return false;
			}

			public void FrontSpot(ref FlagSpot s, string[] over, StringBuilder why)
			{
				bool flag = M.Name != null && M.Name.Contains("keep");
				float p = (s.BannerPx = Math.Max(0.045f, Math.Min(0.07f, 0.0058f * Width)) * (flag ? 1.2f : 1f));
				if (Point(M.Info("a_banner"), out var at, out var yawDeg) && at.Z > 0.5f)
				{
					s.Front = 1;
					s.BannerTop = at;
					s.BannerYaw = yawDeg * MathF.PI / 180f;
					why.Append("; banner anchor");
					return;
				}
				if (over != null && over.Length > 1 && Point(over[1], out at, out yawDeg))
				{
					if (at.Z > 0.5f)
					{
						s.Front = 1;
						s.BannerTop = at;
						s.BannerYaw = yawDeg * MathF.PI / 180f;
						why.Append("; banner override");
					}
					else
					{
						s.Front = 4;
						s.Stand = at;
						why.Append("; standard override");
					}
					return;
				}
				Maps();
				if (Point(M.Info("a_standard"), out at, out var _))
				{
					s.Front = 4;
					s.Stand = new Vec3(at.X, at.Y, Floor(at.X, at.Y));
					why.Append("; standard anchor");
					return;
				}
				why.Append("; door ").Append(HasDoor ? "" : "(none) ").Append(F(DoorX))
					.Append(',')
					.Append(F(DoorFront))
					.Append(" top ")
					.Append(F(DoorTop));
				if (!WallSearch(ref s, p, why) && !BracketSearch(ref s, p, why))
				{
					StandardSpot(ref s, p, why);
				}
			}

			private float Floor(float x, float y)
			{
				return FloorUnder(x, y, 0.3f);
			}

			private bool WallSearch(ref FlagSpot s, float p0, StringBuilder why)
			{
				float num = float.MaxValue;
				float[] array = new float[3] { 1f, 0.8f, 0.62f };
				foreach (float num2 in array)
				{
					float num3 = p0 * num2;
					float num4 = 28f * num3;
					float num5 = 38f * num3;
					float num6 = DoorTop + 0.25f + num5;
					for (int j = 0; j < 25; j++)
					{
						float num7 = DoorX + (float)(j - 12) * 0.3f;
						if (Math.Abs(num7) > 0.45f * Width)
						{
							continue;
						}
						float num8 = ((Math.Abs(num7 - DoorX) >= DoorHalf + 0.15f + 0.5f * num4) ? Math.Min(num6, 1.9f + num5) : num6);
						for (int k = 0; k < 16; k++)
						{
							float num9 = num8 + (float)k * 0.3f;
							if (num9 > num6 + 3.3f)
							{
								break;
							}
							if (Wall(num7 - 0.5f * num4, num7 + 0.5f * num4, num9 - num5, num9, noGlass: false, out var near, out var far) && !(far - near > 0.25f) && !(near > DoorFront + 2f))
							{
								float num10 = Math.Abs(num7 - DoorX) + 0.6f * Math.Abs(num9 - num6) + 4f * (1f - num2) + 0.5f * Math.Max(0f, near - DoorFront);
								if (!(num10 >= num))
								{
									num = num10;
									s.Front = 2;
									s.BannerTop = new Vec3(num7, near - 2f * num3, num9);
									s.BannerYaw = 0f;
									s.BannerPx = num3;
								}
							}
						}
					}
				}
				if (s.Front == 2)
				{
					why.Append("; wall ").Append(V(s.BannerTop)).Append(" px ")
						.Append(F(s.BannerPx * 1000f))
						.Append("mm");
				}
				return s.Front == 2;
			}

			private bool BracketSearch(ref FlagSpot s, float p0, StringBuilder why)
			{
				float num = float.MaxValue;
				float[] array = new float[2] { 1f, 0.8f };
				foreach (float num2 in array)
				{
					float num3 = p0 * num2;
					float num4 = 28f * num3;
					float num5 = 38f * num3;
					float num6 = DoorTop + 0.35f + num5;
					for (int j = 0; j < 10; j++)
					{
						float num7 = num6 + (float)j * 0.3f;
						for (int k = -1; k <= 1; k += 2)
						{
							for (int l = 0; l < 10; l++)
							{
								float num8 = DoorX + (float)k * (DoorHalf + 0.3f + 0.5f * num4 + (float)l * 0.3f);
								if (Math.Abs(num8) > 0.5f * Width || !Wall(num8 - 0.2f, num8 + 0.2f, num7 - 0.2f, num7 + 0.2f, noGlass: true, out var near, out var far) || far - near > 0.1f)
								{
									continue;
								}
								float y = near - 0.6f;
								if (Open(num8 - 0.5f * num4, num8 + 0.5f * num4, num7 - 0.1f - num5, num7, y))
								{
									float num9 = Math.Abs(num8 - DoorX) + 0.6f * (num7 - num6) + 4f * (1f - num2);
									if (!(num9 >= num))
									{
										num = num9;
										s.Front = 3;
										s.Stand = new Vec3(num8, near, num7);
										s.BannerTop = new Vec3(num8, y, num7 - 0.1f);
										s.BannerYaw = 0f;
										s.BannerPx = num3;
									}
								}
							}
						}
					}
				}
				if (s.Front == 3)
				{
					why.Append("; bracket ").Append(V(s.Stand));
				}
				return s.Front == 3;
			}

			private void StandardSpot(ref FlagSpot s, float p0, StringBuilder why)
			{
				float num = 1f;
				Vec3 at;
				float yawDeg;
				bool flag = Point(M.Info("a_sign"), out at, out yawDeg);
				if (flag && Math.Abs(at.X - DoorX) > 0.01f)
				{
					num = Math.Sign(at.X - DoorX);
				}
				float num2 = 28f * p0;
				float num3 = 0f;
				float num4 = M.Min.Y - 1.8f;
				List<KeyValuePair<float, Vec3>> list = new List<KeyValuePair<float, Vec3>>();
				float num5 = DoorHalf + 1.4f + 0.5f * num2;
				List<float> list2 = new List<float>
				{
					DoorFront - 0.4f,
					DoorFront - 1f,
					DoorFront - 1.8f,
					DoorFront - 2.6f
				};
				if (M.Min.Y < DoorFront - 0.4f)
				{
					list2.Add(M.Min.Y - 0.4f);
					list2.Add(M.Min.Y - 1f);
				}
				foreach (float item in list2)
				{
					float[] array = new float[4] { 1f, 1.6f, 2.2f, 3f };
					foreach (float num6 in array)
					{
						float[] array2 = new float[2]
						{
							0f - num,
							num
						};
						foreach (float num7 in array2)
						{
							float num8 = DoorX + num7 * num5 * num6;
							if ((!flag || !((num8 - at.X) * (num8 - at.X) + (item - at.Y) * (item - at.Y) < 1.44f)) && (flag || !((num8 - num3) * (num8 - num3) + (item - num4) * (item - num4) < 1.6899998f)))
							{
								float key = 1.5f * Math.Abs(Math.Abs(num8 - DoorX) - num5) + (DoorFront - 0.4f - item) + ((num7 == num) ? 0.01f : 0f);
								list.Add(new KeyValuePair<float, Vec3>(key, new Vec3(num8, item, 0f)));
							}
						}
					}
				}
				list.Sort((KeyValuePair<float, Vec3> u, KeyValuePair<float, Vec3> v) => u.Key.CompareTo(v.Key));
				foreach (KeyValuePair<float, Vec3> item2 in list)
				{
					if (StandardFits(item2.Value.X, item2.Value.Y, p0, sky: false, out var floor))
					{
						s.Front = 4;
						s.Stand = new Vec3(item2.Value.X, item2.Value.Y, floor);
						s.BannerPx = p0;
						why.Append("; standard ").Append(V(s.Stand));
						return;
					}
				}
				s.Front = 0;
				why.Append("; no banner spot");
			}

			internal static string V(Vec3 v)
			{
				return "(" + F(v.X) + "," + F(v.Y) + "," + F(v.Z) + ")";
			}
		}

		public const float RuinBroken = 0.55f;

		public const float StandardPx = 64f;

		public const float StandardHangPx = 59f;

		public const float StandardBarHalfPx = 22f;

		public const float BracketReach = 0.6f;

		public const int BannerBoxW = 28;

		public const int BannerBoxH = 38;

		public static readonly Dictionary<string, string> Overrides = new Dictionary<string, string>
		{
			["hall_frog"] = "0,0.9,8.73",
			["hall_snowman"] = "0.93,0.76,9.26",
			["hall_monkey"] = "-1.34,-1.4,8.41|2,-2.6,0"
		};

		public static int Pending;

		public static int Done;

		private const int MaxAside = 2;

		private static readonly Dictionary<MeshModel, FlagSpot> Known = new Dictionary<MeshModel, FlagSpot>();

		private static readonly HashSet<MeshModel> Aside = new HashSet<MeshModel>();

		private static readonly HashSet<MeshModel> Failed = new HashSet<MeshModel>();

		private static readonly Queue<KeyValuePair<MeshModel, FlagSpot>> Finished = new Queue<KeyValuePair<MeshModel, FlagSpot>>();

		private static readonly string[] MovingParts = new string[7] { "flag", "flames", "float", "spin", "wheel", "sails", "pennant" };

		private static readonly string[] NoRoofSlots = new string[19]
		{
			"banner", "pole", "rope", "fire", "flame", "lamp", "glow", "glint", "smoke", "window",
			"door", "void", "water", "leaf", "bone", "horn", "claw", "tusk", "skull"
		};

		private static readonly string[] NoWallSlots = new string[11]
		{
			"banner", "rope", "fire", "flame", "lamp", "glow", "smoke", "leaf", "window", "void",
			"water"
		};

		public static bool TryGet(MeshModel full, out FlagSpot spot)
		{
			spot = default(FlagSpot);
			if (full == null)
			{
				return false;
			}
			if (Pending > 0)
			{
				lock (Finished)
				{
					while (Finished.Count > 0)
					{
						KeyValuePair<MeshModel, FlagSpot> keyValuePair = Finished.Dequeue();
						Aside.Remove(keyValuePair.Key);
						Pending--;
						Done++;
						if (keyValuePair.Value.Pole != null)
						{
							Known[keyValuePair.Key] = keyValuePair.Value;
						}
						else
						{
							Failed.Add(keyValuePair.Key);
						}
					}
				}
			}
			if (Known.TryGetValue(full, out spot))
			{
				return true;
			}
			if (Failed.Contains(full) || Aside.Count >= 2 || !Aside.Add(full))
			{
				return false;
			}
			if (Known.Count > 1024)
			{
				Known.Clear();
			}
			Pending++;
			ThreadPool.QueueUserWorkItem(delegate
			{
				FlagSpot value;
				try
				{
					value = Compute(full);
				}
				catch (Exception ex)
				{
					value = new FlagSpot
					{
						Why = "failed: " + ex.Message
					};
				}
				lock (Finished)
				{
					Finished.Enqueue(new KeyValuePair<MeshModel, FlagSpot>(full, value));
				}
			});
			return false;
		}

		public static float RigOf(MeshModel m)
		{
			float num = m.InfoFloat("width", m.Max.X - m.Min.X);
			return Math.Max(0.85f, Math.Min(1.15f, 0.09f * num));
		}

		public static FlagSpot Compute(MeshModel m)
		{
			StringBuilder stringBuilder = new StringBuilder();
			Geometry geometry = new Geometry(m);
			float num = RigOf(m);
			bool flag = m.Info("ruin") == "1" || m.Name.StartsWith("ruin_", StringComparison.Ordinal);
			FlagSpot s = new FlagSpot
			{
				Rig = num,
				Pole = new float[4],
				PoleCapital = new float[4],
				ClothScale = new float[4],
				ClothScaleCapital = new float[4]
			};
			string value = null;
			if (m.Name != null)
			{
				Overrides.TryGetValue(m.Name, out value);
			}
			string[] array = value?.Split('|');
			if (Point(m.Info("a_flag"), out var at, out var yawDeg))
			{
				stringBuilder.Append("anchor");
			}
			else if (array != null && Point(array[0], out at, out yawDeg))
			{
				stringBuilder.Append("override");
			}
			else
			{
				at = geometry.Roof(stringBuilder);
			}
			s.Foot = at;
			for (int i = 0; i < 4; i++)
			{
				s.Pole[i] = geometry.Clearance(at, num, num, (FlagShape)i, out s.ClothScale[i]);
				s.PoleCapital[i] = geometry.Clearance(at, num * 1.25f, num, (FlagShape)i, out s.ClothScaleCapital[i]);
			}
			stringBuilder.Append(" pole ").Append(F(s.Pole[0])).Append('/')
				.Append(F(s.Pole[2]))
				.Append(" (base ")
				.Append(F(3f * num))
				.Append(')');
			for (int j = 0; j < 4; j++)
			{
				if (s.ClothScale[j] < 1f || s.ClothScaleCapital[j] < 1f)
				{
					stringBuilder.Append(" cloth ").Append(FlagCloth.ShapeName((FlagShape)j)).Append(' ')
						.Append(F(s.ClothScale[j]))
						.Append('/')
						.Append(F(s.ClothScaleCapital[j]));
				}
			}
			if (flag)
			{
				uint num2 = Hash(m.Name ?? "");
				s.Lean = (0.21f + 0.1f * (float)(num2 % 1000) / 1000f) * (((num2 & 0x10000) != 0) ? 1f : (-1f));
				for (int k = 0; k < 4; k++)
				{
					s.Pole[k] = geometry.RuinPole(at, num, s.Lean, (FlagShape)k, out s.ClothScale[k]);
					s.PoleCapital[k] = geometry.RuinPole(at, num * 1.25f, s.Lean, (FlagShape)k, out s.ClothScaleCapital[k]);
				}
				stringBuilder.Append(" ruin lean ").Append(F(s.Lean)).Append(" stump ")
					.Append(F(0.55f * s.Pole[0]));
			}
			else
			{
				geometry.FrontSpot(ref s, array, stringBuilder);
			}
			s.Why = stringBuilder.ToString();
			return s;
		}

		internal static bool Point(string value, out Vec3 at, out float yawDeg)
		{
			at = new Vec3(0f, 0f, 0f);
			yawDeg = 0f;
			if (string.IsNullOrEmpty(value))
			{
				return false;
			}
			string[] array = value.Split('/')[0].Split(',');
			if (array.Length < 3 || !Num(array[0], out var v) || !Num(array[1], out var v2) || !Num(array[2], out var v3))
			{
				return false;
			}
			at = new Vec3(v, v2, v3);
			if (array.Length >= 4)
			{
				Num(array[3], out yawDeg);
			}
			return true;
		}

		private static bool Num(string s, out float v)
		{
			return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
		}

		internal static string F(float v)
		{
			return v.ToString("0.##", CultureInfo.InvariantCulture);
		}

		private static uint Hash(string s)
		{
			uint num = 2166136261u;
			foreach (char c in s)
			{
				num ^= c;
				num *= 16777619;
			}
			return num;
		}

		private static bool Starts(string name, string[] prefixes)
		{
			if (name == null)
			{
				return false;
			}
			foreach (string value in prefixes)
			{
				if (name.StartsWith(value, StringComparison.Ordinal))
				{
					return true;
				}
			}
			return false;
		}
	}
}
