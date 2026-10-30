using System;
using System.Collections.Generic;
using System.Globalization;

namespace FirstPerson.Core
{
	public sealed class HousePlan
	{
		public sealed class Storey
		{
			public float Floor;

			public float Top;

			public bool[] Inside;

			public float[] Ceiling;

			public readonly List<List<P2>> Loops = new List<List<P2>>();

			public float Area;

			public readonly List<P2> Passages = new List<P2>();
		}

		public struct Opening
		{
			public float X;

			public float Y;

			public float HalfWidth;

			public float Z0;

			public float Z1;

			public float OutX;

			public float OutY;

			public int Storey;

			public bool Found;

			public bool Guessed;
		}

		public struct P2
		{
			public float X;

			public float Y;

			public P2(float x, float y)
			{
				X = x;
				Y = y;
			}
		}

		private struct Tri
		{
			public Vec3 A;

			public Vec3 B;

			public Vec3 C;

			public byte Slot;
		}

		public const float Cell = 0.05f;

		private const float SliceUp = 0.6f;

		private const float CloseGap = 0.22f;

		private const float SideRoomArea = 2.5f;

		private const float MaxBridge = 0.9f;

		private const float PassWidth = 0.9f;

		public string Model;

		public float FloorZ;

		public float X0;

		public float Y0;

		public int W;

		public int H;

		public readonly List<Storey> Storeys = new List<Storey>();

		public Opening Door;

		public readonly List<Opening> Windows = new List<Opening>();

		public float ChimneyX = float.NaN;

		public float ChimneyY = float.NaN;

		public static string Why;

		public static Action<string, int, int, bool[]> DebugMask;

		private static readonly Dictionary<string, HousePlan> Known = new Dictionary<string, HousePlan>();

		private static readonly Dictionary<string, string> Failed = new Dictionary<string, string>();

		public static HousePlan Of(MeshModel model)
		{
			if (model == null)
			{
				Why = "no model";
				return null;
			}
			lock (Known)
			{
				if (Known.TryGetValue(model.Name, out var value))
				{
					return value;
				}
				if (Failed.TryGetValue(model.Name, out var value2))
				{
					Why = value2;
					return null;
				}
			}
			HousePlan housePlan;
			try
			{
				housePlan = Measure(model);
			}
			catch (Exception ex)
			{
				housePlan = null;
				Why = "measuring failed: " + ex.Message;
			}
			lock (Known)
			{
				if (housePlan != null)
				{
					Known[model.Name] = housePlan;
				}
				else
				{
					Failed[model.Name] = Why;
				}
			}
			return housePlan;
		}

		private static HousePlan Measure(MeshModel model)
		{
			Why = null;
			List<Tri> list = new List<Tri>();
			int slot = model.SlotIndex("door_gap");
			int slot2 = model.SlotIndex("door");
			int slot3 = model.SlotIndex("window");
			float num = float.MaxValue;
			float num2 = float.MaxValue;
			float num3 = float.MinValue;
			float num4 = float.MinValue;
			MeshPart[] parts = model.Parts;
			foreach (MeshPart meshPart in parts)
			{
				if (!(meshPart.Name == "door_leaf") && meshPart.Pos != null)
				{
					for (int j = 0; j < meshPart.Tri.Length; j += 3)
					{
						Tri item = new Tri
						{
							A = V(meshPart.Pos, meshPart.Tri[j]),
							B = V(meshPart.Pos, meshPart.Tri[j + 1]),
							C = V(meshPart.Pos, meshPart.Tri[j + 2]),
							Slot = meshPart.Slot[j / 3]
						};
						list.Add(item);
						num = Math.Min(num, Math.Min(item.A.X, Math.Min(item.B.X, item.C.X)));
						num3 = Math.Max(num3, Math.Max(item.A.X, Math.Max(item.B.X, item.C.X)));
						num2 = Math.Min(num2, Math.Min(item.A.Y, Math.Min(item.B.Y, item.C.Y)));
						num4 = Math.Max(num4, Math.Max(item.A.Y, Math.Max(item.B.Y, item.C.Y)));
					}
				}
			}
			if (list.Count == 0)
			{
				Why = "no faces";
				return null;
			}
			HousePlan housePlan = new HousePlan
			{
				Model = model.Name
			};
			housePlan.X0 = num - 0.3f;
			housePlan.Y0 = num2 - 0.3f;
			housePlan.W = (int)Math.Ceiling((num3 - num + 0.6f) / 0.05f) + 1;
			housePlan.H = (int)Math.Ceiling((num4 - num2 + 0.6f) / 0.05f) + 1;
			housePlan.Door = BiggestFlat(list, slot);
			if (!housePlan.Door.Found)
			{
				housePlan.Door = Lowest(list, slot2);
			}
			if (housePlan.Door.Found && housePlan.Door.HalfWidth < 0.25f)
			{
				housePlan.Door.HalfWidth = 0.45f;
				if (Math.Abs(housePlan.Door.OutY) > 0.7f)
				{
					housePlan.Door.OutX = 0f;
					housePlan.Door.OutY = -1f;
				}
			}
			if (!housePlan.Door.Found)
			{
				housePlan.Door = new Opening
				{
					X = 0f,
					Y = num2,
					HalfWidth = 0.5f,
					Z0 = 0f,
					Z1 = 1.8f,
					OutX = 0f,
					OutY = -1f,
					Found = true,
					Guessed = true
				};
			}
			housePlan.FloorZ = housePlan.Door.Z0;
			List<Opening> list2 = Clusters(list, slot3);
			list2.Sort((Opening p, Opening q) => p.Z0.CompareTo(q.Z0));
			float num5 = 0.86f;
			foreach (Opening item3 in list2)
			{
				if (item3.Z0 > housePlan.FloorZ + 0.3f && item3.Z0 < housePlan.FloorZ + 1.6f)
				{
					num5 = item3.Z0 - housePlan.FloorZ;
					break;
				}
			}
			List<float> list3 = new List<float> { housePlan.FloorZ };
			foreach (Opening item4 in list2)
			{
				float num6 = list3[list3.Count - 1];
				if (item4.Z0 - num6 > 2.1f + num5 * 0.5f && list3.Count < 2)
				{
					list3.Add(item4.Z0 - num5);
				}
			}
			string text = model.Info("smoke");
			if (text != null)
			{
				string[] array = text.Split(',');
				if (array.Length >= 2 && float.TryParse(array[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && float.TryParse(array[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
				{
					housePlan.ChimneyX = result;
					housePlan.ChimneyY = result2;
				}
			}
			for (int num7 = 0; num7 < list3.Count; num7++)
			{
				Storey storey = housePlan.Room(list, list3[num7], (num7 == 0) ? null : housePlan.Storeys[0]);
				if (storey == null || (num7 > 0 && storey.Area < 4f))
				{
					if (num7 != 0)
					{
						break;
					}
					return null;
				}
				housePlan.Storeys.Add(storey);
			}
			if (housePlan.InRoom(0, housePlan.Door.X + housePlan.Door.OutX * 0.4f, housePlan.Door.Y + housePlan.Door.OutY * 0.4f))
			{
				housePlan.Door.OutX = 0f - housePlan.Door.OutX;
				housePlan.Door.OutY = 0f - housePlan.Door.OutY;
			}
			for (int num8 = 0; num8 < housePlan.Storeys.Count; num8++)
			{
				housePlan.Ceilings(list, housePlan.Storeys[num8], (num8 + 1 < housePlan.Storeys.Count) ? housePlan.Storeys[num8 + 1] : null);
			}
			foreach (Opening item5 in list2)
			{
				Opening item2 = item5;
				item2.Storey = ((housePlan.Storeys.Count > 1 && item5.Z0 >= housePlan.Storeys[1].Floor - 0.2f) ? 1 : 0);
				if (item2.Z1 - housePlan.Storeys[item2.Storey].Floor < 0.3f)
				{
					continue;
				}
				bool flag = false;
				for (float num9 = 0.1f; num9 <= 0.7f; num9 += 0.1f)
				{
					if (flag)
					{
						break;
					}
					flag = housePlan.InRoom(item2.Storey, item2.X - item2.OutX * num9, item2.Y - item2.OutY * num9);
				}
				if (!flag)
				{
					continue;
				}
				bool flag2 = false;
				for (float num10 = 0.15f; num10 <= 0.75f; num10 += 0.1f)
				{
					if (flag2)
					{
						break;
					}
					flag2 = housePlan.InRoom(item2.Storey, item2.X + item2.OutX * num10, item2.Y + item2.OutY * num10);
				}
				if (flag2)
				{
					continue;
				}
				bool flag3 = false;
				foreach (Opening window in housePlan.Windows)
				{
					if (window.Storey == item2.Storey && Math.Abs(window.X - item2.X) < 0.2f && Math.Abs(window.Y - item2.Y) < 0.2f && Math.Abs(window.Z0 - item2.Z0) < 0.2f)
					{
						flag3 = true;
					}
				}
				if (!flag3)
				{
					housePlan.Windows.Add(item2);
				}
			}
			for (int num11 = 0; num11 < housePlan.Storeys.Count; num11++)
			{
				housePlan.Outline(housePlan.Storeys[num11], num11);
			}
			return housePlan;
		}

		private static Vec3 V(float[] pos, int i)
		{
			return new Vec3(pos[i * 3], pos[i * 3 + 1], pos[i * 3 + 2]);
		}

		private int Index(float x, float y)
		{
			int num = (int)Math.Floor((x - X0) / 0.05f);
			int num2 = (int)Math.Floor((y - Y0) / 0.05f);
			if (num >= 0 && num2 >= 0 && num < W && num2 < H)
			{
				return num2 * W + num;
			}
			return -1;
		}

		public float CellX(int i)
		{
			return X0 + ((float)i + 0.5f) * 0.05f;
		}

		public float CellY(int j)
		{
			return Y0 + ((float)j + 0.5f) * 0.05f;
		}

		public bool InRoom(int storey, float x, float y)
		{
			int num = Index(x, y);
			if (num >= 0)
			{
				return Storeys[storey].Inside[num];
			}
			return false;
		}

		public float CeilingAt(int storey, float x, float y)
		{
			int num = Index(x, y);
			if (num < 0)
			{
				return float.NaN;
			}
			return Storeys[storey].Ceiling[num];
		}

		private static List<Opening> Clusters(List<Tri> tris, int slot)
		{
			List<(float, float, float, float, float, float, float, float, float)> list = new List<(float, float, float, float, float, float, float, float, float)>();
			if (slot < 0)
			{
				return new List<Opening>();
			}
			foreach (Tri tri in tris)
			{
				if (tri.Slot != slot)
				{
					continue;
				}
				Vec3 vec = Vec3.Cross(tri.B - tri.A, tri.C - tri.A);
				float num = (float)Math.Sqrt(Vec3.Dot(vec, vec)) * 0.5f;
				if (num < 1E-05f)
				{
					continue;
				}
				vec = vec.Normalized();
				if (Math.Abs(vec.Z) > 0.5f)
				{
					continue;
				}
				float num2 = Math.Min(tri.A.X, Math.Min(tri.B.X, tri.C.X));
				float num3 = Math.Max(tri.A.X, Math.Max(tri.B.X, tri.C.X));
				float num4 = Math.Min(tri.A.Y, Math.Min(tri.B.Y, tri.C.Y));
				float num5 = Math.Max(tri.A.Y, Math.Max(tri.B.Y, tri.C.Y));
				float num6 = Math.Min(tri.A.Z, Math.Min(tri.B.Z, tri.C.Z));
				float num7 = Math.Max(tri.A.Z, Math.Max(tri.B.Z, tri.C.Z));
				int num8 = -1;
				for (int i = 0; i < list.Count; i++)
				{
					(float, float, float, float, float, float, float, float, float) tuple = list[i];
					if (num2 <= tuple.Item2 + 0.04f && num3 >= tuple.Item1 - 0.04f && num4 <= tuple.Item4 + 0.04f && num5 >= tuple.Item3 - 0.04f && num6 <= tuple.Item6 + 0.04f && num7 >= tuple.Item5 - 0.04f && vec.X * tuple.Item7 + vec.Y * tuple.Rest.Item1 > 0.5f)
					{
						num8 = i;
						break;
					}
				}
				if (num8 < 0)
				{
					list.Add((num2, num3, num4, num5, num6, num7, vec.X * num, vec.Y * num, num));
					continue;
				}
				(float, float, float, float, float, float, float, float, float) tuple2 = list[num8];
				list[num8] = (Math.Min(tuple2.Item1, num2), Math.Max(tuple2.Item2, num3), Math.Min(tuple2.Item3, num4), Math.Max(tuple2.Item4, num5), Math.Min(tuple2.Item5, num6), Math.Max(tuple2.Item6, num7), tuple2.Item7 + vec.X * num, tuple2.Rest.Item1 + vec.Y * num, tuple2.Rest.Item2 + num);
			}
			List<Opening> list2 = new List<Opening>();
			foreach (var item in list)
			{
				if (!(item.Rest.Item2 < 0.02f))
				{
					float num9 = (float)Math.Sqrt(item.Item7 * item.Item7 + item.Rest.Item1 * item.Rest.Item1);
					if (!(num9 < 1E-06f))
					{
						float num10 = item.Item7 / num9;
						float num11 = item.Rest.Item1 / num9;
						float halfWidth = (Math.Abs(num11) * (item.Item2 - item.Item1) + Math.Abs(num10) * (item.Item4 - item.Item3)) * 0.5f;
						list2.Add(new Opening
						{
							X = (item.Item1 + item.Item2) * 0.5f,
							Y = (item.Item3 + item.Item4) * 0.5f,
							HalfWidth = halfWidth,
							Z0 = item.Item5,
							Z1 = item.Item6,
							OutX = num10,
							OutY = num11,
							Found = true
						});
					}
				}
			}
			return list2;
		}

		private static Opening BiggestFlat(List<Tri> tris, int slot)
		{
			Opening result = default(Opening);
			float num = 0f;
			foreach (Opening item in Clusters(tris, slot))
			{
				float num2 = item.HalfWidth * 2f * (item.Z1 - item.Z0);
				if (num2 > num && item.Z1 - item.Z0 > 0.8f)
				{
					result = item;
					num = num2;
				}
			}
			return result;
		}

		private static Opening Lowest(List<Tri> tris, int slot)
		{
			Opening result = default(Opening);
			foreach (Opening item in Clusters(tris, slot))
			{
				if (item.Z1 - item.Z0 > 0.6f && (!result.Found || item.Z0 < result.Z0 - 0.05f || (Math.Abs(item.Z0 - result.Z0) < 0.05f && item.HalfWidth > result.HalfWidth)))
				{
					result = item;
				}
			}
			return result;
		}

		private Storey Room(List<Tri> tris, float floor, Storey below)
		{
			int num = W * H;
			bool[] array = new bool[num];
			float z = floor + 0.6f;
			foreach (Tri tri in tris)
			{
				int num2 = 0;
				P2 p = default(P2);
				P2 p2 = default(P2);
				if (Cross(tri.A, tri.B, z, out var p3))
				{
					p = p3;
					num2++;
				}
				if (Cross(tri.B, tri.C, z, out p3))
				{
					if (num2 == 0)
					{
						p = p3;
					}
					else
					{
						p2 = p3;
					}
					num2++;
				}
				if (num2 < 2 && Cross(tri.C, tri.A, z, out p3))
				{
					if (num2 == 0)
					{
						p = p3;
					}
					else
					{
						p2 = p3;
					}
					num2++;
				}
				if (num2 != 0)
				{
					Line(array, p, (num2 >= 2) ? p2 : p);
				}
			}
			if (below == null && !Door.Guessed)
			{
				float num3 = 0f - Door.OutY;
				float outX = Door.OutX;
				Line(array, new P2(Door.X - num3 * (Door.HalfWidth + 0.1f), Door.Y - outX * (Door.HalfWidth + 0.1f)), new P2(Door.X + num3 * (Door.HalfWidth + 0.1f), Door.Y + outX * (Door.HalfWidth + 0.1f)));
			}
			int num4 = (int)Math.Round(4.400000095367432);
			int[] toWall = Distance(array);
			bool[] array2 = new bool[num];
			Stack<int> stack = new Stack<int>();
			for (int i = 0; i < W; i++)
			{
				Seed(array2, toWall, num4, stack, i);
				Seed(array2, toWall, num4, stack, (H - 1) * W + i);
			}
			for (int j = 0; j < H; j++)
			{
				Seed(array2, toWall, num4, stack, j * W);
				Seed(array2, toWall, num4, stack, j * W + W - 1);
			}
			while (stack.Count > 0)
			{
				int num5 = stack.Pop();
				int num6 = num5 % W;
				int num7 = num5 / W;
				if (num6 > 0)
				{
					Seed(array2, toWall, num4, stack, num5 - 1);
				}
				if (num6 < W - 1)
				{
					Seed(array2, toWall, num4, stack, num5 + 1);
				}
				if (num7 > 0)
				{
					Seed(array2, toWall, num4, stack, num5 - W);
				}
				if (num7 < H - 1)
				{
					Seed(array2, toWall, num4, stack, num5 + W);
				}
			}
			int[] array3 = Distance(array2);
			bool[] array4 = new bool[num];
			bool[] array5 = new bool[num];
			for (int k = 0; k < num; k++)
			{
				array5[k] = array3[k] > num4;
				array4[k] = !array[k] && array5[k];
			}
			int[] array6 = new int[num];
			List<int> list = new List<int> { 0 };
			for (int l = 0; l < num; l++)
			{
				if (!array4[l] || array6[l] != 0)
				{
					continue;
				}
				int count = list.Count;
				int num8 = 0;
				list.Add(0);
				array6[l] = count;
				stack.Push(l);
				while (stack.Count > 0)
				{
					int num9 = stack.Pop();
					num8++;
					int num10 = num9 % W;
					int num11 = num9 / W;
					if (num10 > 0 && array4[num9 - 1] && array6[num9 - 1] == 0)
					{
						array6[num9 - 1] = count;
						stack.Push(num9 - 1);
					}
					if (num10 < W - 1 && array4[num9 + 1] && array6[num9 + 1] == 0)
					{
						array6[num9 + 1] = count;
						stack.Push(num9 + 1);
					}
					if (num11 > 0 && array4[num9 - W] && array6[num9 - W] == 0)
					{
						array6[num9 - W] = count;
						stack.Push(num9 - W);
					}
					if (num11 < H - 1 && array4[num9 + W] && array6[num9 + W] == 0)
					{
						array6[num9 + W] = count;
						stack.Push(num9 + W);
					}
				}
				list[count] = num8;
			}
			int num12 = 0;
			if (below == null && Door.Guessed)
			{
				for (float num13 = 0f; num13 < 1.2f; num13 += 0.1f)
				{
					if (num12 != 0)
					{
						break;
					}
					(float, float)[] array7 = new(float, float)[5]
					{
						(0f, 0f),
						(num13, 0f),
						(0f - num13, 0f),
						(0f, num13),
						(0f, 0f - num13)
					};
					for (int m = 0; m < array7.Length; m++)
					{
						(float, float) tuple = array7[m];
						float item = tuple.Item1;
						float item2 = tuple.Item2;
						int num14 = Index(item, item2);
						if (num14 >= 0 && array6[num14] != 0 && (float)list[array6[num14]] * 0.05f * 0.05f > 1f)
						{
							num12 = array6[num14];
							break;
						}
					}
				}
				if (num12 == 0)
				{
					Why = "no room round the middle";
					return null;
				}
				float num15 = float.MaxValue;
				for (int n = 0; n < num; n++)
				{
					if (array6[n] == num12 && Math.Abs(CellX(n % W)) < 0.3f)
					{
						num15 = Math.Min(num15, CellY(n / W));
					}
				}
				Door.Y = num15 - 0.025f - 0.05f;
			}
			else if (below == null)
			{
				for (float num16 = 0.35f; num16 < 1.2f; num16 += 0.1f)
				{
					if (num12 != 0)
					{
						break;
					}
					for (float num17 = 0f; num17 <= 0.5f; num17 += 0.1f)
					{
						if (num12 != 0)
						{
							break;
						}
						float[] array8 = new float[2] { 1f, -1f };
						foreach (float num18 in array8)
						{
							int num19 = Index(Door.X - Door.OutX * num16 - Door.OutY * num17 * num18, Door.Y - Door.OutY * num16 + Door.OutX * num17 * num18);
							if (num19 >= 0 && array6[num19] != 0 && (float)list[array6[num19]] * 0.05f * 0.05f > 1f)
							{
								num12 = array6[num19];
								break;
							}
						}
					}
				}
				if (num12 == 0)
				{
					Why = "no room inside the door";
					return null;
				}
			}
			else
			{
				int[] array9 = new int[list.Count];
				for (int num20 = 0; num20 < num; num20++)
				{
					if (array6[num20] != 0 && below.Inside[num20])
					{
						array9[array6[num20]]++;
					}
				}
				for (int num21 = 1; num21 < list.Count; num21++)
				{
					if (array9[num21] > ((num12 != 0) ? array9[num12] : 0))
					{
						num12 = num21;
					}
				}
				if (num12 == 0 || (float)list[num12] * 0.05f * 0.05f < 3f)
				{
					return null;
				}
			}
			bool[] array10 = new bool[num];
			for (int num22 = 0; num22 < num; num22++)
			{
				array10[num22] = array6[num22] == num12;
			}
			int[] array11 = Distance(array10);
			List<P2> list2 = new List<P2>();
			int num23 = (int)Math.Round(9.0);
			for (int num24 = 1; num24 < list.Count; num24++)
			{
				if (num24 == num12 || (float)list[num24] * 0.05f * 0.05f < 2.5f)
				{
					continue;
				}
				bool flag = false;
				for (int num25 = 0; num25 < num; num25++)
				{
					if (flag)
					{
						break;
					}
					if (array6[num25] == num24 && array11[num25] <= num23)
					{
						flag = true;
					}
				}
				if (!flag)
				{
					continue;
				}
				bool[] array12 = new bool[num];
				for (int num26 = 0; num26 < num; num26++)
				{
					array12[num26] = array6[num26] == num24;
				}
				if (Covered(tris, array12, floor + 1.5f) < 0.8f)
				{
					continue;
				}
				P2 item3 = Join(array10, array6, num24, array11, array5, num23);
				if (float.IsNaN(item3.X))
				{
					continue;
				}
				list2.Add(item3);
				for (int num27 = 0; num27 < num; num27++)
				{
					if (array6[num27] == num24)
					{
						array10[num27] = true;
					}
				}
				array11 = Distance(array10);
			}
			if (DebugMask != null)
			{
				DebugMask("wall" + ((below != null) ? 1 : 0), W, H, array);
				DebugMask("outside" + ((below != null) ? 1 : 0), W, H, array2);
				bool[] array13 = new bool[num];
				for (int num28 = 0; num28 < num; num28++)
				{
					array13[num28] = array6[num28] == num12;
				}
				DebugMask("main" + ((below != null) ? 1 : 0), W, H, array13);
				DebugMask("inside" + ((below != null) ? 1 : 0), W, H, array10);
			}
			Storey storey = new Storey
			{
				Floor = floor,
				Inside = array10
			};
			storey.Passages.AddRange(list2);
			for (int num29 = 0; num29 < num; num29++)
			{
				if (array10[num29])
				{
					storey.Area += 0.0025000002f;
				}
			}
			return storey;
		}

		private static bool Same(P2 a, P2 b)
		{
			if (Math.Abs(a.X - b.X) < 1E-05f)
			{
				return Math.Abs(a.Y - b.Y) < 1E-05f;
			}
			return false;
		}

		private static bool Cross(Vec3 a, Vec3 b, float z, out P2 p)
		{
			p = default(P2);
			if ((a.Z - z) * (b.Z - z) > 0f || Math.Abs(a.Z - b.Z) < 1E-06f)
			{
				return false;
			}
			float num = (z - a.Z) / (b.Z - a.Z);
			p = new P2(a.X + (b.X - a.X) * num, a.Y + (b.Y - a.Y) * num);
			return true;
		}

		private void Line(bool[] grid, P2 a, P2 b)
		{
			float num = b.X - a.X;
			float num2 = b.Y - a.Y;
			int num3 = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(num), Math.Abs(num2)) / 0.025f));
			for (int i = 0; i <= num3; i++)
			{
				int num4 = Index(a.X + num * (float)i / (float)num3, a.Y + num2 * (float)i / (float)num3);
				if (num4 >= 0)
				{
					grid[num4] = true;
				}
			}
		}

		private void Seed(bool[] outside, int[] toWall, int r, Stack<int> stack, int c)
		{
			if (!outside[c] && toWall[c] > r)
			{
				outside[c] = true;
				stack.Push(c);
			}
		}

		private int[] Distance(bool[] set)
		{
			int num = W * H;
			int[] array = new int[num];
			for (int i = 0; i < num; i++)
			{
				array[i] = ((!set[i]) ? 1048576 : 0);
			}
			for (int j = 0; j < H; j++)
			{
				for (int k = 0; k < W; k++)
				{
					int num2 = j * W + k;
					int num3 = array[num2];
					if (k > 0)
					{
						num3 = Math.Min(num3, array[num2 - 1] + 1);
					}
					if (j > 0)
					{
						num3 = Math.Min(num3, array[num2 - W] + 1);
						if (k > 0)
						{
							num3 = Math.Min(num3, array[num2 - W - 1] + 1);
						}
						if (k < W - 1)
						{
							num3 = Math.Min(num3, array[num2 - W + 1] + 1);
						}
					}
					array[num2] = num3;
				}
			}
			for (int num4 = H - 1; num4 >= 0; num4--)
			{
				for (int num5 = W - 1; num5 >= 0; num5--)
				{
					int num6 = num4 * W + num5;
					int num7 = array[num6];
					if (num5 < W - 1)
					{
						num7 = Math.Min(num7, array[num6 + 1] + 1);
					}
					if (num4 < H - 1)
					{
						num7 = Math.Min(num7, array[num6 + W] + 1);
						if (num5 < W - 1)
						{
							num7 = Math.Min(num7, array[num6 + W + 1] + 1);
						}
						if (num5 > 0)
						{
							num7 = Math.Min(num7, array[num6 + W - 1] + 1);
						}
					}
					array[num6] = num7;
				}
			}
			return array;
		}

		private P2 Join(bool[] inside, int[] label, int id, int[] toMain, bool[] within, int pass)
		{
			int num = W * H;
			int num2 = int.MaxValue;
			for (int i = 0; i < num; i++)
			{
				if (label[i] == id)
				{
					num2 = Math.Min(num2, toMain[i]);
				}
			}
			int num3 = Math.Max(num2 + 1, pass);
			double num4 = 0.0;
			double num5 = 0.0;
			int num6 = 0;
			for (int j = 0; j < num; j++)
			{
				if (label[j] == id && toMain[j] <= num3)
				{
					num4 += (double)(j % W);
					num5 += (double)(j / W);
					num6++;
				}
			}
			if (num6 == 0)
			{
				return new P2(float.NaN, float.NaN);
			}
			int num7 = (int)Math.Round(num4 / (double)num6);
			int num8 = (int)Math.Round(num5 / (double)num6);
			int num9 = -1;
			float num10 = float.MaxValue;
			for (int k = 0; k < num; k++)
			{
				if (label[k] == id && toMain[k] <= num3)
				{
					float num11 = (k % W - num7) * (k % W - num7) + (k / W - num8) * (k / W - num8);
					if (num11 < num10)
					{
						num10 = num11;
						num9 = k;
					}
				}
			}
			int num12 = (int)Math.Round(9.0);
			List<int> list = new List<int>();
			int num13 = num9;
			int num14 = 0;
			for (int l = 0; l < 200; l++)
			{
				if (num13 < 0)
				{
					break;
				}
				if (inside[num13])
				{
					break;
				}
				list.Add(num13);
				if (!within[num13])
				{
					num14++;
				}
				int num15 = num13 % W;
				int num16 = num13 / W;
				int num17 = num13;
				for (int m = -1; m <= 1; m++)
				{
					for (int n = -1; n <= 1; n++)
					{
						int num18 = num15 + n;
						int num19 = num16 + m;
						if (num18 >= 0 && num19 >= 0 && num18 < W && num19 < H && toMain[num19 * W + num18] < toMain[num17])
						{
							num17 = num19 * W + num18;
						}
					}
				}
				if (num17 == num13)
				{
					break;
				}
				num13 = num17;
			}
			if (num9 < 0 || num13 < 0 || !inside[num13] || (float)num14 * 0.05f > 0.9f)
			{
				return new P2(float.NaN, float.NaN);
			}
			foreach (int item in list)
			{
				int num20 = item % W;
				int num21 = item / W;
				for (int num22 = -num12; num22 <= num12; num22++)
				{
					for (int num23 = -num12; num23 <= num12; num23++)
					{
						int num24 = num20 + num23;
						int num25 = num21 + num22;
						if (num24 >= 0 && num25 >= 0 && num24 < W && num25 < H && num23 * num23 + num22 * num22 <= num12 * num12 && (within[num25 * W + num24] || (num14 > 0 && !within[item])))
						{
							inside[num25 * W + num24] = true;
						}
					}
				}
			}
			if (num9 < 0 || num13 < 0)
			{
				return new P2(float.NaN, float.NaN);
			}
			return new P2((CellX(num9 % W) + CellX(num13 % W)) * 0.5f, (CellY(num9 / W) + CellY(num13 / W)) * 0.5f);
		}

		private void Ceilings(List<Tri> tris, Storey s, Storey above)
		{
			int num = W * H;
			float[] ceil = new float[num];
			float[] cap = new float[num];
			float[] low = new float[num];
			for (int i = 0; i < num; i++)
			{
				ceil[i] = float.PositiveInfinity;
				cap[i] = float.PositiveInfinity;
				low[i] = float.PositiveInfinity;
			}
			float head = s.Floor + 1.5f;
			float knee = s.Floor + 0.45f;
			foreach (Tri tri in tris)
			{
				Vec3 vec = Vec3.Cross(tri.B - tri.A, tri.C - tri.A);
				float num2 = (float)Math.Sqrt(Vec3.Dot(vec, vec));
				if (num2 < 1E-08f || Math.Abs(vec.Z) / num2 < 0.15f)
				{
					continue;
				}
				bool level = Math.Abs(vec.Z) / num2 > 0.92f;
				if ((level && vec.Z > 0f) || Math.Max(tri.A.Z, Math.Max(tri.B.Z, tri.C.Z)) < knee)
				{
					continue;
				}
				RasterTri(tri, s.Inside, delegate(int c, float zz)
				{
					if (zz >= head)
					{
						if (level)
						{
							if (zz < cap[c])
							{
								cap[c] = zz;
							}
						}
						else if (zz < ceil[c])
						{
							ceil[c] = zz;
						}
					}
					if (zz >= knee && zz < low[c])
					{
						low[c] = zz;
					}
				});
			}
			for (int num3 = 0; num3 < num; num3++)
			{
				if (float.IsInfinity(ceil[num3]))
				{
					ceil[num3] = cap[num3];
				}
			}
			float val = float.MinValue;
			for (int num4 = 0; num4 < num; num4++)
			{
				if (!s.Inside[num4])
				{
					ceil[num4] = float.NaN;
					continue;
				}
				float num5 = ceil[num4];
				if (float.IsInfinity(num5))
				{
					num5 = (float.IsInfinity(low[num4]) ? (s.Floor + 2.6f) : low[num4]);
				}
				if (above != null && above.Inside[num4])
				{
					num5 = Math.Min(num5, above.Floor - 0.18f);
				}
				ceil[num4] = num5;
				val = Math.Max(val, num5);
			}
			float[] array = new float[num];
			List<float> list = new List<float>(25);
			val = float.MinValue;
			for (int num6 = 0; num6 < H; num6++)
			{
				for (int num7 = 0; num7 < W; num7++)
				{
					int num8 = num6 * W + num7;
					if (!s.Inside[num8])
					{
						array[num8] = float.NaN;
						continue;
					}
					list.Clear();
					for (int num9 = -2; num9 <= 2; num9++)
					{
						for (int num10 = -2; num10 <= 2; num10++)
						{
							int num11 = num7 + num10;
							int num12 = num6 + num9;
							if (num11 >= 0 && num12 >= 0 && num11 < W && num12 < H && s.Inside[num12 * W + num11])
							{
								list.Add(ceil[num12 * W + num11]);
							}
						}
					}
					list.Sort();
					float num13 = list[list.Count / 2];
					if (above != null && above.Inside[num8])
					{
						num13 = Math.Min(num13, above.Floor - 0.18f);
					}
					array[num8] = num13;
					val = Math.Max(val, num13);
				}
			}
			s.Ceiling = array;
			s.Top = val;
		}

		private float Covered(List<Tri> tris, bool[] mask, float over)
		{
			int num = W * H;
			int num2 = 0;
			int num3 = 0;
			bool[] roofed = new bool[num];
			foreach (Tri tri in tris)
			{
				if (Math.Max(tri.A.Z, Math.Max(tri.B.Z, tri.C.Z)) < over)
				{
					continue;
				}
				RasterTri(tri, mask, delegate(int c, float zz)
				{
					if (zz >= over)
					{
						roofed[c] = true;
					}
				});
			}
			for (int num4 = 0; num4 < num; num4++)
			{
				if (mask[num4])
				{
					num2++;
					if (roofed[num4])
					{
						num3++;
					}
				}
			}
			if (num2 != 0)
			{
				return (float)num3 / (float)num2;
			}
			return 0f;
		}

		private void RasterTri(Tri t, bool[] mask, Action<int, float> at)
		{
			float num = Math.Min(t.A.X, Math.Min(t.B.X, t.C.X));
			float num2 = Math.Max(t.A.X, Math.Max(t.B.X, t.C.X));
			float num3 = Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y));
			float num4 = Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y));
			int num5 = Math.Max(0, (int)((num - X0) / 0.05f));
			int num6 = Math.Min(W - 1, (int)((num2 - X0) / 0.05f));
			int num7 = Math.Max(0, (int)((num3 - Y0) / 0.05f));
			int num8 = Math.Min(H - 1, (int)((num4 - Y0) / 0.05f));
			float num9 = (t.B.Y - t.C.Y) * (t.A.X - t.C.X) + (t.C.X - t.B.X) * (t.A.Y - t.C.Y);
			if (Math.Abs(num9) < 1E-09f)
			{
				return;
			}
			for (int i = num7; i <= num8; i++)
			{
				for (int j = num5; j <= num6; j++)
				{
					int num10 = i * W + j;
					if (mask[num10])
					{
						float num11 = CellX(j);
						float num12 = CellY(i);
						float num13 = ((t.B.Y - t.C.Y) * (num11 - t.C.X) + (t.C.X - t.B.X) * (num12 - t.C.Y)) / num9;
						float num14 = ((t.C.Y - t.A.Y) * (num11 - t.C.X) + (t.A.X - t.C.X) * (num12 - t.C.Y)) / num9;
						float num15 = 1f - num13 - num14;
						if (!(num13 < -0.0001f) && !(num14 < -0.0001f) && !(num15 < -0.0001f))
						{
							at(num10, num13 * t.A.Z + num14 * t.B.Z + num15 * t.C.Z);
						}
					}
				}
			}
		}

		private void Outline(Storey s, int storey)
		{
			List<Opening> list = new List<Opening>();
			if (storey == 0)
			{
				list.Add(Door);
			}
			foreach (Opening window in Windows)
			{
				if (window.Storey == storey)
				{
					list.Add(window);
				}
			}
			foreach (List<P2> item in Trace(s.Inside))
			{
				bool[] pinned = new bool[item.Count];
				List<P2> pts = item;
				foreach (Opening item2 in list)
				{
					pts = Pin(pts, ref pinned, item2);
				}
				List<P2> list2 = Simplify(pts, pinned, 0.06f);
				if (list2.Count >= 3)
				{
					s.Loops.Add(list2);
				}
			}
		}

		private List<List<P2>> Trace(bool[] inside)
		{
			Dictionary<int, List<int>> next = new Dictionary<int, List<int>>();
			int num = W + 1;
			for (int i = 0; i < H; i++)
			{
				for (int j = 0; j < W; j++)
				{
					if (inside[i * W + j])
					{
						if (!At(j, i - 1))
						{
							Step(i * num + j + 1, i * num + j);
						}
						if (!At(j + 1, i))
						{
							Step((i + 1) * num + j + 1, i * num + j + 1);
						}
						if (!At(j, i + 1))
						{
							Step((i + 1) * num + j, (i + 1) * num + j + 1);
						}
						if (!At(j - 1, i))
						{
							Step(i * num + j, (i + 1) * num + j);
						}
					}
				}
			}
			List<List<P2>> list = new List<List<P2>>();
			while (next.Count > 0)
			{
				int num2 = -1;
				using (Dictionary<int, List<int>>.Enumerator enumerator = next.GetEnumerator())
				{
					if (enumerator.MoveNext())
					{
						num2 = enumerator.Current.Key;
					}
				}
				List<P2> list2 = new List<P2>();
				int num3 = num2;
				int num4 = -1;
				for (int k = 0; k < 400000; k++)
				{
					if (!next.TryGetValue(num3, out var value))
					{
						break;
					}
					if (value.Count == 0)
					{
						break;
					}
					int index = 0;
					if (value.Count > 1 && num4 >= 0)
					{
						int num5 = num3 % num - num4 % num;
						int num6 = num3 / num - num4 / num;
						for (int l = 0; l < value.Count; l++)
						{
							int num7 = value[l] % num - num3 % num;
							int num8 = value[l] / num - num3 / num;
							if (num5 * num8 - num6 * num7 < 0)
							{
								index = l;
								break;
							}
						}
					}
					int num9 = value[index];
					value.RemoveAt(index);
					if (value.Count == 0)
					{
						next.Remove(num3);
					}
					list2.Add(new P2(X0 + (float)(num3 % num) * 0.05f, Y0 + (float)(num3 / num) * 0.05f));
					num4 = num3;
					num3 = num9;
					if (num3 == num2)
					{
						break;
					}
				}
				if (list2.Count >= 4)
				{
					list.Add(list2);
				}
			}
			list.Sort((List<P2> a, List<P2> b) => Math.Abs(Area(b)).CompareTo(Math.Abs(Area(a))));
			return list;
			bool At(int num10, int num11)
			{
				if (num10 >= 0 && num11 >= 0 && num10 < W && num11 < H)
				{
					return inside[num11 * W + num10];
				}
				return false;
			}
			void Step(int from, int to)
			{
				if (!next.TryGetValue(from, out var value2))
				{
					value2 = (next[from] = new List<int>(1));
				}
				value2.Add(to);
			}
		}

		private static float Area(List<P2> loop)
		{
			float num = 0f;
			for (int i = 0; i < loop.Count; i++)
			{
				P2 p = loop[i];
				P2 p2 = loop[(i + 1) % loop.Count];
				num += p.X * p2.Y - p2.X * p.Y;
			}
			return num * 0.5f;
		}

		private static List<P2> Pin(List<P2> pts, ref bool[] pinned, Opening o)
		{
			float num = 0f - o.OutY;
			float outX = o.OutX;
			P2[] array = new P2[2]
			{
				new P2(o.X - num * o.HalfWidth, o.Y - outX * o.HalfWidth),
				new P2(o.X + num * o.HalfWidth, o.Y + outX * o.HalfWidth)
			};
			int[] array2 = new int[2];
			for (int i = 0; i < 2; i++)
			{
				float num2 = float.MaxValue;
				for (int j = 0; j < pts.Count; j++)
				{
					float num3 = pts[j].X - array[i].X;
					float num4 = pts[j].Y - array[i].Y;
					float num5 = num3 * num + num4 * outX;
					float num6 = num3 * o.OutX + num4 * o.OutY;
					float num7 = num5 * num5 * 4f + num6 * num6;
					if (num6 > 0.05f)
					{
						num7 += 100f;
					}
					if (num7 < num2)
					{
						num2 = num7;
						array2[i] = j;
					}
				}
				if (num2 > 0.36f)
				{
					return pts;
				}
			}
			int num8 = array2[0];
			int num9 = array2[1];
			int num10 = (num9 - num8 + pts.Count) % pts.Count;
			if ((num8 - num9 + pts.Count) % pts.Count < num10)
			{
				int num11 = num8;
				num8 = num9;
				num9 = num11;
			}
			List<P2> list = new List<P2>();
			List<bool> list2 = new List<bool>();
			for (int k = 0; k < pts.Count; k++)
			{
				int num12 = (k - num8 + pts.Count) % pts.Count;
				int num13 = (num9 - num8 + pts.Count) % pts.Count;
				if (num12 <= 0 || num12 >= num13)
				{
					list.Add(pts[k]);
					list2.Add(pinned[k] || k == num8 || k == num9);
				}
			}
			float num14 = 0f;
			for (int l = 0; l < 2; l++)
			{
				num14 += (pts[array2[l]].X - array[l].X) * o.OutX + (pts[array2[l]].Y - array[l].Y) * o.OutY;
			}
			num14 *= 0.5f;
			for (int m = 0; m < list.Count; m++)
			{
				if (!list2[m])
				{
					continue;
				}
				for (int n = 0; n < 2; n++)
				{
					if (Same(list[m], pts[array2[n]]))
					{
						list[m] = new P2(array[n].X + o.OutX * num14, array[n].Y + o.OutY * num14);
					}
				}
			}
			pinned = list2.ToArray();
			return list;
		}

		private static List<P2> Simplify(List<P2> pts, bool[] pinned, float tol)
		{
			int count = pts.Count;
			if (count < 4)
			{
				return new List<P2>(pts);
			}
			bool[] array = new bool[count];
			bool flag = false;
			for (int i = 0; i < count; i++)
			{
				if (pinned[i])
				{
					array[i] = true;
					flag = true;
				}
			}
			if (!flag)
			{
				int num = 0;
				float num2 = -1f;
				for (int j = 1; j < count; j++)
				{
					float num3 = (pts[j].X - pts[0].X) * (pts[j].X - pts[0].X) + (pts[j].Y - pts[0].Y) * (pts[j].Y - pts[0].Y);
					if (num3 > num2)
					{
						num2 = num3;
						num = j;
					}
				}
				array[0] = (array[num] = true);
			}
			bool flag2 = true;
			while (flag2)
			{
				flag2 = false;
				int num4 = -1;
				for (int k = 0; k < count; k++)
				{
					if (array[k])
					{
						num4 = k;
						break;
					}
				}
				int num5 = num4;
				do
				{
					int num6 = (num5 + 1) % count;
					while (!array[num6])
					{
						num6 = (num6 + 1) % count;
					}
					float num7 = tol;
					int num8 = -1;
					for (int num9 = (num5 + 1) % count; num9 != num6; num9 = (num9 + 1) % count)
					{
						float num10 = Off(pts[num9], pts[num5], pts[num6]);
						if (num10 > num7)
						{
							num7 = num10;
							num8 = num9;
						}
					}
					if (num8 >= 0)
					{
						array[num8] = true;
						flag2 = true;
					}
					num5 = num6;
				}
				while (num5 != num4);
			}
			List<P2> list = new List<P2>();
			for (int l = 0; l < count; l++)
			{
				if (array[l])
				{
					list.Add(pts[l]);
				}
			}
			Square(list);
			return list;
		}

		private static float Off(P2 p, P2 a, P2 b)
		{
			float num = b.X - a.X;
			float num2 = b.Y - a.Y;
			float num3 = (float)Math.Sqrt(num * num + num2 * num2);
			if (num3 < 1E-06f)
			{
				return (float)Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
			}
			return Math.Abs((p.X - a.X) * num2 - (p.Y - a.Y) * num) / num3;
		}

		private static void Square(List<P2> pts)
		{
			int count = pts.Count;
			for (int i = 0; i < count; i++)
			{
				P2 p = pts[i];
				P2 p2 = pts[(i + 1) % count];
				float num = p2.X - p.X;
				float num2 = p2.Y - p.Y;
				if (Math.Abs(num) > 1E-05f && Math.Abs(num2 / num) < 0.07f)
				{
					float y = (p.Y + p2.Y) * 0.5f;
					pts[i] = new P2(p.X, y);
					pts[(i + 1) % count] = new P2(p2.X, y);
				}
				else if (Math.Abs(num2) > 1E-05f && Math.Abs(num / num2) < 0.07f)
				{
					float x = (p.X + p2.X) * 0.5f;
					pts[i] = new P2(x, p.Y);
					pts[(i + 1) % count] = new P2(x, p2.Y);
				}
			}
		}
	}
}
