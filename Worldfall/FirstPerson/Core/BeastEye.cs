using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class BeastEye
	{
		public struct Shape
		{
			public int Head;

			public Vec3 Pivot;

			public Vec3 Eye;

			public Vec3 View;

			public bool InHead;
		}

		public static float WidenDown = 16f;

		private static readonly Dictionary<MeshModel, Shape> Shapes = new Dictionary<MeshModel, Shape>();

		private static readonly Dictionary<MeshModel, MeshModel> Muzzles = new Dictionary<MeshModel, MeshModel>();

		public static float LookingDown(float pitchDegrees)
		{
			float num = (-10f - pitchDegrees) / 35f;
			num = ((num < 0f) ? 0f : ((num > 1f) ? 1f : num));
			return num * num * (3f - 2f * num);
		}

		private static float Nod(float pitchDegrees)
		{
			return Clamp(pitchDegrees * (MathF.PI / 180f) * 0.6f, -0.6f, 0.5f);
		}

		public static Mat3 HeadTurn(float bodyYaw, float viewYaw, float pitchDegrees)
		{
			float num = viewYaw - bodyYaw;
			while ((double)num > Math.PI)
			{
				num -= MathF.PI * 2f;
			}
			for (; (double)num < -Math.PI; num += MathF.PI * 2f)
			{
			}
			return Mat3.RotZ(num) * Mat3.RotX(0f - Nod(pitchDegrees));
		}

		public static float Widen(float pitchDegrees)
		{
			return WidenDown * LookingDown(pitchDegrees);
		}

		public static Vec3 Offset(MeshModel m, float scale, float bodyYaw, float viewYaw, float pitchDegrees)
		{
			Shape shape = Of(m);
			Vec3 vec = Turn(shape.Pivot, bodyYaw);
			float num = Nod(pitchDegrees);
			Vec3 vec2 = shape.View - shape.Pivot;
			float num2 = (float)Math.Cos(0f - num);
			float num3 = (float)Math.Sin(0f - num);
			Vec3 p = new Vec3(vec2.X, vec2.Y * num2 - vec2.Z * num3, vec2.Y * num3 + vec2.Z * num2);
			Vec3 vec3 = Turn(p, viewYaw);
			return new Vec3((vec.X + vec3.X) * scale, (vec.Y + vec3.Y) * scale, (shape.Pivot.Z + p.Z) * scale);
		}

		public static ulong HiddenParts(MeshModel m)
		{
			Shape shape = Of(m);
			if (!shape.InHead || shape.Head >= 64)
			{
				return ulong.MaxValue;
			}
			ulong num = (ulong)(1L << shape.Head);
			for (int i = shape.Head + 1; i < m.Parts.Length && i < 64; i++)
			{
				if (m.Parts[i].Parent >= 0 && (num & (ulong)(1L << m.Parts[i].Parent)) != 0L)
				{
					num |= (ulong)(1L << i);
				}
			}
			return num;
		}

		public static MeshModel Muzzle(MeshModel m)
		{
			lock (Muzzles)
			{
				if (Muzzles.TryGetValue(m, out var value))
				{
					return value;
				}
				Shape shape = Of(m);
				MeshModel meshModel = null;
				if (shape.InHead && shape.Head >= 0)
				{
					MeshPart meshPart = m.Parts[shape.Head];
					float num = shape.Pivot.Y - shape.Eye.Y;
					float num2 = shape.Eye.Y - Math.Max(0.04f, num * 0.12f);
					float z = shape.View.Z;
					List<int> list = new List<int>();
					List<byte> list2 = new List<byte>();
					List<byte> list3 = new List<byte>();
					for (int i = 0; i < meshPart.TriangleCount; i++)
					{
						bool flag = true;
						for (int j = 0; j < 3 && flag; j++)
						{
							int num3 = meshPart.Tri[i * 3 + j];
							flag = meshPart.Pos[num3 * 3 + 1] < num2 && meshPart.Pos[num3 * 3 + 2] < z;
						}
						if (flag)
						{
							list.Add(meshPart.Tri[i * 3]);
							list.Add(meshPart.Tri[i * 3 + 1]);
							list.Add(meshPart.Tri[i * 3 + 2]);
							list2.Add(meshPart.Slot[i]);
							list3.Add((byte)((meshPart.Axis != null) ? meshPart.Axis[i] : 2));
						}
					}
					if (list.Count >= 18)
					{
						meshModel = new MeshModel
						{
							Name = m.Name + "#muzzle",
							SlotNames = m.SlotNames,
							SlotKind = m.SlotKind,
							SlotFlags = m.SlotFlags,
							SlotColor = m.SlotColor,
							Parts = new MeshPart[m.Parts.Length],
							Min = m.Min,
							Max = m.Max
						};
						for (int k = 0; k < m.Parts.Length; k++)
						{
							MeshPart meshPart2 = m.Parts[k];
							meshModel.Parts[k] = ((k == shape.Head) ? new MeshPart
							{
								Name = meshPart2.Name,
								Parent = meshPart2.Parent,
								Pivot = meshPart2.Pivot,
								Pos = meshPart2.Pos,
								Nrm = meshPart2.Nrm,
								Tri = list.ToArray(),
								Slot = list2.ToArray(),
								Axis = list3.ToArray(),
								Uv = meshPart2.Uv
							} : new MeshPart
							{
								Name = meshPart2.Name,
								Parent = meshPart2.Parent,
								Pivot = meshPart2.Pivot,
								Pos = new float[0],
								Nrm = new float[0],
								Tri = new int[0],
								Slot = new byte[0],
								Axis = new byte[0]
							});
						}
					}
				}
				Muzzles[m] = meshModel;
				return meshModel;
			}
		}

		private static Vec3 Turn(Vec3 p, float heading)
		{
			float num = (float)Math.Sin(heading);
			float num2 = (float)Math.Cos(heading);
			return new Vec3((0f - p.X) * num - p.Y * num2, p.X * num2 - p.Y * num, p.Z);
		}

		private static float Clamp(float v, float lo, float hi)
		{
			if (!(v < lo))
			{
				if (!(v > hi))
				{
					return v;
				}
				return hi;
			}
			return lo;
		}

		public static Shape Of(MeshModel m)
		{
			lock (Shapes)
			{
				if (Shapes.TryGetValue(m, out var value))
				{
					return value;
				}
				value = new Shape
				{
					Head = m.PartIndex("head")
				};
				int num = m.SlotIndex("eyes");
				int num2 = -1;
				int num3 = 0;
				Vec3 vec = new Vec3(0f, 0f, 0f);
				if (num >= 0)
				{
					for (int i = 0; i < m.Parts.Length; i++)
					{
						MeshPart meshPart = m.Parts[i];
						Vec3 vec2 = new Vec3(0f, 0f, 0f);
						int num4 = 0;
						for (int j = 0; j < meshPart.TriangleCount; j++)
						{
							if (meshPart.Slot[j] == num)
							{
								for (int k = 0; k < 3; k++)
								{
									int num5 = meshPart.Tri[j * 3 + k];
									vec2 += new Vec3(meshPart.Pos[num5 * 3], meshPart.Pos[num5 * 3 + 1], meshPart.Pos[num5 * 3 + 2]);
									num4++;
								}
							}
						}
						if (num4 > num3)
						{
							num2 = i;
							num3 = num4;
							vec = vec2;
						}
					}
				}
				if (num2 >= 0)
				{
					value.Eye = vec * (1f / (float)num3);
					value.InHead = num2 == value.Head;
				}
				else if (value.Head >= 0)
				{
					MeshPart meshPart2 = m.Parts[value.Head];
					float num6 = float.MaxValue;
					float num7 = float.MaxValue;
					float num8 = float.MaxValue;
					float num9 = float.MinValue;
					float num10 = float.MinValue;
					float num11 = float.MinValue;
					for (int l = 0; l < meshPart2.VertexCount; l++)
					{
						float val = meshPart2.Pos[l * 3];
						float val2 = meshPart2.Pos[l * 3 + 1];
						float val3 = meshPart2.Pos[l * 3 + 2];
						num6 = Math.Min(num6, val);
						num7 = Math.Min(num7, val2);
						num8 = Math.Min(num8, val3);
						num9 = Math.Max(num9, val);
						num10 = Math.Max(num10, val2);
						num11 = Math.Max(num11, val3);
					}
					value.Eye = new Vec3((num6 + num9) * 0.5f, num7 + (num10 - num7) * 0.3f, num8 + (num11 - num8) * 0.7f);
					value.InHead = true;
				}
				else
				{
					value.Eye = new Vec3(0f, m.Min.Y + (m.Max.Y - m.Min.Y) * 0.15f, m.Min.Z + (m.Max.Z - m.Min.Z) * 0.8f);
				}
				value.Pivot = ((value.Head >= 0) ? m.Parts[value.Head].Pivot : value.Eye);
				value.View = value.Eye;
				if (value.InHead)
				{
					MeshPart meshPart3 = m.Parts[value.Head];
					float num12 = Math.Max(0.05f, value.Pivot.Y - value.Eye.Y);
					float num13 = value.Eye.Z;
					float num14 = Math.Max(0.03f, num12 * 0.15f);
					float num15 = Math.Max(0.03f, num12 * 0.3f);
					for (int n = 0; n < meshPart3.VertexCount; n++)
					{
						float num16 = meshPart3.Pos[n * 3];
						float num17 = meshPart3.Pos[n * 3 + 1];
						float num18 = meshPart3.Pos[n * 3 + 2];
						if (Math.Abs(num17 - value.Eye.Y) < num14 && Math.Abs(num16 - value.Eye.X) < num15 && num18 < value.Eye.Z + num12 * 0.6f)
						{
							num13 = Math.Max(num13, num18);
						}
					}
					value.View = new Vec3(value.Eye.X, value.Eye.Y, num13 + Math.Max(0.015f, num12 * 0.06f));
				}
				Shapes[m] = value;
				return value;
			}
		}
	}
}
