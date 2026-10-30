using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class WrapMeshes
	{
		private struct Section
		{
			public Vec3 C;

			public float Ra;

			public float Rb;
		}

		private const int Sides = 16;

		public const int ArmTurns = 5;

		private static readonly uint Linen = TerrainCache.Rgb(238, 230, 210);

		private static readonly uint Edge = TerrainCache.Rgb(204, 192, 166);

		private static readonly uint Stain = TerrainCache.Rgb(150, 56, 46);

		private static readonly Dictionary<MeshModel, MeshModel> Arms = new Dictionary<MeshModel, MeshModel>();

		private static readonly Dictionary<(MeshModel, bool), MeshModel> People = new Dictionary<(MeshModel, bool), MeshModel>();

		public static MeshModel ForArm(MeshModel arm)
		{
			if (arm == null || arm.Parts.Length == 0)
			{
				return null;
			}
			lock (Arms)
			{
				if (Arms.TryGetValue(arm, out var value))
				{
					return value;
				}
				if (Arms.Count > 64)
				{
					Arms.Clear();
				}
				MeshPart p = arm.Parts[0];
				Section[] array = new Section[13];
				List<Vec3> list = new List<Vec3>();
				for (int i = 0; i < array.Length; i++)
				{
					float at = 0.075f + 0.126f * (float)i / (float)(array.Length - 1);
					list.Clear();
					ArmorMeshes.Slice(p, 1, at, list);
					array[i] = Across(list, at, 1, 0.025f);
				}
				MeshBuilder b = new MeshBuilder("wrap_arm");
				int linen = b.Slot("linen", 7, Linen, 0);
				int edge = b.Slot("edge", 7, Edge, 0);
				int slot = b.Slot("stain", 7, Stain, 0);
				Helix(b, array, 0.075f, 0.201f, new Vec3(0f, 0f, 1f), new Vec3(1f, 0f, 0f), 5, 0.0046f, 0.0028f, linen, edge, delegate(int turn)
				{
					b.Part("turn" + turn, (turn == 0) ? (-1) : 0, new Vec3(0f, 0f, 0f));
				});
				Section section = At(array, 0.075f, 0.201f, 0.13170001f);
				b.Blob(slot, section.C + new Vec3(section.Rb + 0.0042f, 0f, 0.003f), 0.0014f, 0.006f, 0.0045f, 6);
				value = b.Build();
				Arms[arm] = value;
				return value;
			}
		}

		public static MeshModel ForPerson(MeshModel body, bool armoured)
		{
			if (body == null)
			{
				return null;
			}
			(MeshModel, bool) key = (body, armoured);
			lock (People)
			{
				if (People.TryGetValue(key, out var value))
				{
					return value;
				}
				if (People.Count > 96)
				{
					People.Clear();
				}
				value = BuildPerson(body, armoured);
				People[key] = value;
				return value;
			}
		}

		private static MeshModel BuildPerson(MeshModel body, bool armoured)
		{
			int num = body.PartIndex("lower_arm_L");
			if (num < 0 || body.Parts[num].VertexCount < 8)
			{
				return null;
			}
			float num2 = Math.Max(0.5f, body.Max.Z - Math.Max(0f, body.Min.Z)) / 1.8f;
			MeshBuilder meshBuilder = new MeshBuilder(body.Name + "_wrap" + (armoured ? "_a" : ""));
			int linen = meshBuilder.Slot("linen", 7, Linen, 0);
			int edge = meshBuilder.Slot("edge", 7, Edge, 0);
			for (int i = 0; i < body.Parts.Length; i++)
			{
				MeshPart meshPart = body.Parts[i];
				meshBuilder.Part(meshPart.Name, meshPart.Parent, meshPart.Pivot);
				if (i != num)
				{
					continue;
				}
				ArmorMeshes.Bounds(meshPart, out var lo, out var _);
				float num3 = meshPart.Pivot.Z - lo.Z;
				if (num3 <= 0.05f)
				{
					continue;
				}
				float num4 = lo.Z + num3 * 0.3f;
				float num5 = float.MaxValue;
				for (int j = 0; j <= 10; j++)
				{
					float num6 = lo.Z + num3 * (0.12f + 0.4f * (float)j / 10f);
					ArmorMeshes.Ring ring = ArmorMeshes.Fit(meshPart, num6, num3 * 0.04f);
					if (ring.Rx + ring.Ry < num5 - 0.0001f)
					{
						num5 = ring.Rx + ring.Ry;
						num4 = num6;
					}
				}
				float num7 = num4 + num3 * 0.03f;
				float num8 = num4 + num3 * 0.36f;
				float k = (armoured ? 1.06f : 1.03f);
				float pad = (armoured ? 0.013f : 0.003f) * num2;
				Section[] array = new Section[9];
				for (int l = 0; l < array.Length; l++)
				{
					float z = num7 + (num8 - num7) * (float)l / (float)(array.Length - 1);
					ArmorMeshes.Ring ring2 = ArmorMeshes.Fit(meshPart, z, num3 * 0.05f).Grow(k, pad);
					array[l] = new Section
					{
						C = ring2.C,
						Ra = ring2.Rx,
						Rb = ring2.Ry
					};
				}
				Helix(meshBuilder, array, num7, num8, new Vec3(1f, 0f, 0f), new Vec3(0f, 1f, 0f), 4, 0.006f * num2, 0.0035f * num2, linen, edge, null);
			}
			return meshBuilder.Build();
		}

		private static Section Across(List<Vec3> pts, float at, int axis, float fallback)
		{
			if (pts.Count < 3)
			{
				return new Section
				{
					C = ((axis == 1) ? new Vec3(0f, at, 0f) : new Vec3(0f, 0f, at)),
					Ra = fallback,
					Rb = fallback
				};
			}
			float num = float.MaxValue;
			float num2 = float.MinValue;
			float num3 = float.MaxValue;
			float num4 = float.MinValue;
			foreach (Vec3 pt in pts)
			{
				float num5 = ((axis == 1) ? pt.Z : pt.X);
				float num6 = ((axis == 1) ? pt.X : pt.Y);
				if (num5 < num)
				{
					num = num5;
				}
				if (num5 > num2)
				{
					num2 = num5;
				}
				if (num6 < num3)
				{
					num3 = num6;
				}
				if (num6 > num4)
				{
					num4 = num6;
				}
			}
			float num7 = (num + num2) * 0.5f;
			float num8 = (num3 + num4) * 0.5f;
			Vec3 c = ((axis == 1) ? new Vec3(num8, at, num7) : new Vec3(num7, num8, at));
			return new Section
			{
				C = c,
				Ra = (num2 - num) * 0.5f * 1.04f,
				Rb = (num4 - num3) * 0.5f * 1.04f
			};
		}

		private static Section At(Section[] s, float t0, float t1, float t)
		{
			float num = (t - t0) / (t1 - t0) * (float)(s.Length - 1);
			if (num <= 0f)
			{
				return s[0];
			}
			if (num >= (float)(s.Length - 1))
			{
				return s[s.Length - 1];
			}
			int num2 = (int)num;
			float num3 = num - (float)num2;
			return new Section
			{
				C = s[num2].C + (s[num2 + 1].C - s[num2].C) * num3,
				Ra = s[num2].Ra + (s[num2 + 1].Ra - s[num2].Ra) * num3,
				Rb = s[num2].Rb + (s[num2 + 1].Rb - s[num2].Rb) * num3
			};
		}

		private static void Helix(MeshBuilder mb, Section[] s, float t0, float t1, Vec3 a, Vec3 b, int turns, float outNear, float outFar, int linen, int edge, Action<int> beginTurn)
		{
			float num = (t1 - t0) / ((float)turns + 1f + 0.18f);
			float num2 = t0 + num * 0.18f;
			Vec3 along = Vec3.Cross(a, b);
			float[] array = new float[3] { -0.18f, -0.120000005f, 1f };
			for (int i = 0; i < turns; i++)
			{
				beginTurn?.Invoke(i);
				for (int j = 0; j < 16; j++)
				{
					float num3 = (float)j / 16f;
					float num4 = (float)(j + 1) / 16f;
					for (int k = 0; k + 1 < array.Length; k++)
					{
						float lean = ((k == 0) ? 0.6f : 0f);
						Vec3 normal;
						Vec3 a2 = Point(s, t0, t1, num2 + num * ((float)i + num3 + array[k]), num3, a, b, along, Out(array[k], 0.18f, outNear, outFar), lean, out normal);
						Vec3 normal2;
						Vec3 b2 = Point(s, t0, t1, num2 + num * ((float)i + num4 + array[k]), num4, a, b, along, Out(array[k], 0.18f, outNear, outFar), lean, out normal2);
						Vec3 normal3;
						Vec3 c = Point(s, t0, t1, num2 + num * ((float)i + num4 + array[k + 1]), num4, a, b, along, Out(array[k + 1], 0.18f, outNear, outFar), 0f, out normal3);
						Vec3 normal4;
						Vec3 d = Point(s, t0, t1, num2 + num * ((float)i + num3 + array[k + 1]), num3, a, b, along, Out(array[k + 1], 0.18f, outNear, outFar), 0f, out normal4);
						mb.SmoothQuad((k == 0) ? edge : linen, a2, b2, c, d, normal, normal2, normal3, normal4);
					}
				}
			}
		}

		private static float Out(float row, float overlap, float near, float far)
		{
			return near + (far - near) * ((row + overlap) / (1f + overlap));
		}

		private static Vec3 Point(Section[] s, float t0, float t1, float t, float turn, Vec3 a, Vec3 b, Vec3 along, float off, float lean, out Vec3 normal)
		{
			Section section = At(s, t0, t1, t);
			float num = turn * 2f * MathF.PI;
			float num2 = (float)Math.Cos(num);
			float num3 = (float)Math.Sin(num);
			Vec3 vec = section.C - along * Vec3.Dot(section.C, along) + along * t;
			normal = (a * (num2 / Math.Max(0.0001f, section.Ra)) + b * (num3 / Math.Max(0.0001f, section.Rb))).Normalized() - along * lean;
			return vec + a * ((section.Ra + off) * num2) + b * ((section.Rb + off) * num3);
		}
	}
}
