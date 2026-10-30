using System;

namespace FirstPerson.Core
{
	public static class MeshRay
	{
		public static bool Segment(MeshInstance m, Vec3 a, Vec3 b, out float hit)
		{
			hit = 1f;
			MeshModel model = m.Model;
			if (model == null || m.ShadowOnly)
			{
				return false;
			}
			float num = Math.Max(Length(m.Basis.C0), Math.Max(Length(m.Basis.C1), Length(m.Basis.C2)));
			Vec3 p = m.Basis.Mul((model.Min + model.Max) * 0.5f) + m.Position;
			float num2 = Length(model.Max - model.Min) * 0.5f * num;
			if (DistanceToSegment(p, a, b) > num2)
			{
				return false;
			}
			if (!Invert(m.Basis, out var inv))
			{
				return false;
			}
			Vec3 vec = inv.Mul(a - m.Position);
			Vec3 vec2 = inv.Mul(b - m.Position) - vec;
			float x = vec.X;
			float y = vec.Y;
			float z = vec.Z;
			float x2 = vec2.X;
			float y2 = vec2.Y;
			float z2 = vec2.Z;
			float num3 = 1f;
			bool result = false;
			for (int i = 0; i < model.Parts.Length; i++)
			{
				if ((i < 64 && (m.Hidden & (ulong)(1L << i)) != 0L) || Posed(m, i))
				{
					continue;
				}
				MeshPart meshPart = model.Parts[i];
				float[] pos = meshPart.Pos;
				int[] tri = meshPart.Tri;
				int num4 = 0;
				int num5 = 0;
				int triangleCount = meshPart.TriangleCount;
				while (num4 < triangleCount)
				{
					byte b2 = model.SlotKind[meshPart.Slot[num4]];
					if (b2 != 1 && b2 != 12 && b2 != 11)
					{
						int num6 = tri[num5] * 3;
						int num7 = tri[num5 + 1] * 3;
						int num8 = tri[num5 + 2] * 3;
						float num9 = pos[num6];
						float num10 = pos[num6 + 1];
						float num11 = pos[num6 + 2];
						float num12 = pos[num7] - num9;
						float num13 = pos[num7 + 1] - num10;
						float num14 = pos[num7 + 2] - num11;
						float num15 = pos[num8] - num9;
						float num16 = pos[num8 + 1] - num10;
						float num17 = pos[num8 + 2] - num11;
						float num18 = y2 * num17 - z2 * num16;
						float num19 = z2 * num15 - x2 * num17;
						float num20 = x2 * num16 - y2 * num15;
						float num21 = num12 * num18 + num13 * num19 + num14 * num20;
						if (!(num21 > -1E-12f) || !(num21 < 1E-12f))
						{
							float num22 = 1f / num21;
							float num23 = x - num9;
							float num24 = y - num10;
							float num25 = z - num11;
							float num26 = (num23 * num18 + num24 * num19 + num25 * num20) * num22;
							if (!(num26 < 0f) && !(num26 > 1f))
							{
								float num27 = num24 * num14 - num25 * num13;
								float num28 = num25 * num12 - num23 * num14;
								float num29 = num23 * num13 - num24 * num12;
								float num30 = (x2 * num27 + y2 * num28 + z2 * num29) * num22;
								if (!(num30 < 0f) && !(num26 + num30 > 1f))
								{
									float num31 = (num15 * num27 + num16 * num28 + num17 * num29) * num22;
									if (!(num31 < 0f) && !(num31 >= num3))
									{
										num3 = num31;
										result = true;
									}
								}
							}
						}
					}
					num4++;
					num5 += 3;
				}
			}
			hit = num3;
			return result;
		}

		private static bool Posed(MeshInstance m, int p)
		{
			if (m.PartRotation != null && p < m.PartRotation.Length)
			{
				Mat3 mat = m.PartRotation[p];
				if (Math.Abs(mat.C0.X - 1f) > 0.0001f || Math.Abs(mat.C1.Y - 1f) > 0.0001f || Math.Abs(mat.C2.Z - 1f) > 0.0001f)
				{
					return true;
				}
			}
			if (m.PartOffset != null && p < m.PartOffset.Length && Vec3.Dot(m.PartOffset[p], m.PartOffset[p]) > 1E-08f)
			{
				return true;
			}
			if (m.PartScale != null && p < m.PartScale.Length)
			{
				return Math.Abs(m.PartScale[p] - 1f) > 0.0001f;
			}
			return false;
		}

		private static float Length(Vec3 v)
		{
			return (float)Math.Sqrt(Vec3.Dot(v, v));
		}

		private static float DistanceToSegment(Vec3 p, Vec3 a, Vec3 b)
		{
			Vec3 vec = b - a;
			float num = Vec3.Dot(vec, vec);
			float num2 = ((num > 1E-12f) ? Math.Max(0f, Math.Min(1f, Vec3.Dot(p - a, vec) / num)) : 0f);
			return Length(a + vec * num2 - p);
		}

		public static bool Invert(Mat3 m, out Mat3 inv)
		{
			Vec3 b = Vec3.Cross(m.C1, m.C2);
			Vec3 vec = Vec3.Cross(m.C2, m.C0);
			Vec3 vec2 = Vec3.Cross(m.C0, m.C1);
			float num = Vec3.Dot(m.C0, b);
			if (Math.Abs(num) < 1E-12f)
			{
				inv = Mat3.Identity;
				return false;
			}
			float num2 = 1f / num;
			inv = new Mat3(new Vec3(b.X * num2, vec.X * num2, vec2.X * num2), new Vec3(b.Y * num2, vec.Y * num2, vec2.Y * num2), new Vec3(b.Z * num2, vec.Z * num2, vec2.Z * num2));
			return true;
		}
	}
}
