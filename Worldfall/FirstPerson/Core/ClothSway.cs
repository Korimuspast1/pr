using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class ClothSway
	{
		public struct Swing
		{
			public float A;

			public float V;
		}

		public struct Input
		{
			public float Dt;

			public float Speed;

			public float Turn;

			public float Along;

			public float Side;

			public float Push;

			public float Gust;

			public float Stride;

			public float Phase;

			public float Clock;

			public bool Still;
		}

		private sealed class Legs
		{
			public int Count;

			public readonly int[] Part = new int[32];

			public readonly Vec3[] At = new Vec3[32];

			public void Add(int part, Vec3 at)
			{
				if (Count < 32)
				{
					Part[Count] = part;
					At[Count] = at;
					Count++;
				}
			}
		}

		private sealed class Panel
		{
			public Vec3 Hinge;

			public float ZLow;

			public float[] Depth;

			public float[] Left;

			public float[] Right;

			public const float Step = 0.04f;

			public const float Band = 0.13f;

			public const float Wide = 0.24f;

			public const float Reach = 0.05f;

			public float DepthAt(float z, float x)
			{
				float num = x - Hinge.X;
				float num2 = ((num >= -0.13f && num <= 0.13f) ? At(Depth, z) : float.PositiveInfinity);
				if (Math.Abs(num) > 0.08f)
				{
					num2 = Math.Min(num2, At((num < 0f) ? Left : Right, z));
				}
				return num2;
			}

			private float At(float[] d, float z)
			{
				if (d.Length == 0)
				{
					return 0f;
				}
				float num = (z - ZLow) / 0.04f;
				if (num <= 0f)
				{
					return d[0];
				}
				int num2 = (int)num;
				if (num2 >= d.Length - 1)
				{
					return d[d.Length - 1];
				}
				float num3 = num - (float)num2;
				return d[num2] + (d[num2 + 1] - d[num2]) * num3;
			}
		}

		public const float BannerLiftMin = -0.1f;

		public const float BannerLiftMax = 1f;

		public const float BannerYaw = 0.45f;

		public const float BannerTailMin = -0.15f;

		public const float BannerTailMax = 0.8f;

		public const float PlumeLimit = 0.25f;

		public const float PlumeTallLimit = 0.15f;

		public static float LastClearance = float.PositiveInfinity;

		private const int MaxPoints = 32;

		private static readonly float[] Tx = new float[32];

		private static readonly float[] Ty = new float[32];

		private static readonly float[] Tz = new float[32];

		private static readonly bool[] TOk = new bool[32];

		private static readonly int[] Chain = new int[16];

		private static readonly Dictionary<MeshModel, Legs> LegCache = new Dictionary<MeshModel, Legs>();

		private static readonly List<Vec3> Cut = new List<Vec3>();

		private static readonly float[] SkirtHeights = new float[3] { 0.03f, 0.12f, 0.25f };

		private static readonly Dictionary<MeshModel, Panel[]> PanelCache = new Dictionary<MeshModel, Panel[]>();

		public static void Step(ref Swing s, float target, float dt, float k, float zeta, float lo, float hi)
		{
			if (dt > 0f)
			{
				int num = ((!(dt > 1f / 60f)) ? 1 : Math.Min(6, (int)Math.Ceiling(dt * 60f)));
				float num2 = dt / (float)num;
				float num3 = 2f * zeta * (float)Math.Sqrt(k);
				for (int i = 0; i < num; i++)
				{
					s.V += (k * (target - s.A) - num3 * s.V) * num2;
					s.A += s.V * num2;
					if (s.A < lo)
					{
						s.A = lo;
						if (s.V < 0f)
						{
							s.V = 0f;
						}
					}
					else if (s.A > hi)
					{
						s.A = hi;
						if (s.V > 0f)
						{
							s.V = 0f;
						}
					}
				}
			}
			else if (s.A < lo)
			{
				s.A = lo;
			}
			else if (s.A > hi)
			{
				s.A = hi;
			}
			if (float.IsNaN(s.A) || float.IsNaN(s.V))
			{
				s.A = Math.Max(lo, Math.Min(hi, 0f));
				s.V = 0f;
			}
		}

		public static void Wind(Mat3 basis, float wx, float wy, out float along, out float side)
		{
			float x = basis.C1.X;
			float y = basis.C1.Y;
			float x2 = basis.C0.X;
			float y2 = basis.C0.Y;
			float num = (float)Math.Sqrt(x * x + y * y);
			float num2 = (float)Math.Sqrt(x2 * x2 + y2 * y2);
			along = ((num > 1E-06f) ? ((0f - (wx * x + wy * y)) / num) : 0f);
			side = ((num2 > 1E-06f) ? ((0f - (wx * x2 + wy * y2)) / num2) : 0f);
		}

		public static float Flutter(float t, float seed, float amp)
		{
			return amp * (0.62f * (float)Math.Sin(2.3f * t + 6.283f * seed) + 0.38f * (float)Math.Sin(3.9f * t + 11.7f * seed + 1.3f));
		}

		public static float TorsoTilt(MeshInstance body, int hips, int torso)
		{
			Mat3[] partRotation = body.PartRotation;
			MeshModel model = body.Model;
			if (partRotation == null || model == null || torso < 0 || torso >= partRotation.Length)
			{
				return 0f;
			}
			float num = 0f;
			float num2 = 0f;
			float num3 = 1f;
			for (int num4 = torso; num4 >= 0; num4 = model.Parts[num4].Parent)
			{
				ref Mat3 reference = ref partRotation[num4];
				float num5 = reference.C0.X * num + reference.C1.X * num2 + reference.C2.X * num3;
				float num6 = reference.C0.Y * num + reference.C1.Y * num2 + reference.C2.Y * num3;
				float num7 = reference.C0.Z * num + reference.C1.Z * num2 + reference.C2.Z * num3;
				num = num5;
				num2 = num6;
				num3 = num7;
			}
			return (float)Math.Atan2(num2, num3);
		}

		public static void Mantle(ref Swing upper, ref Swing hem, ref Swing side, in Input s, float tilt, float seed)
		{
			float target = 0f;
			float target2 = 0f;
			float target3 = 0f;
			if (!s.Still)
			{
				float speed = s.Speed;
				float num = Math.Min(0.45f, 0.1f * speed + 0.05f * speed * speed);
				target = tilt + num + 0.03f * s.Stride * (float)Math.Sin(2f * s.Phase) - s.Along * s.Push + Flutter(s.Clock, seed, 0.02f + 0.03f * s.Gust);
				target2 = 0.5f * num + Flutter(1.4f * s.Clock, seed + 0.37f, 0.04f + 0.05f * s.Gust);
				target3 = Clamp(0.15f * s.Turn + s.Side * s.Push, -0.2f, 0.2f);
			}
			Step(ref upper, target, s.Dt, 38f, 0.55f, 0f, 1f);
			Step(ref hem, target2, s.Dt, 55f, 0.4f, 0f, 0.95f);
			Step(ref side, target3, s.Dt, 25f, 0.6f, -0.2f, 0.2f);
		}

		public static void Banner(ref Swing lift, ref Swing yaw, ref Swing tail, in Input s, float tilt, float seed)
		{
			float target = 0f;
			float target2 = 0f;
			float target3 = 0f;
			if (!s.Still)
			{
				target = tilt + 0.3f * s.Speed + Math.Max(0f, 0f - s.Along) * s.Push + Flutter(2.3f * s.Clock, seed, 0.05f + 0.08f * s.Gust);
				target2 = Clamp(0.3f * s.Turn + 0.8f * s.Side * s.Push, -0.45f, 0.45f);
				target3 = 0.6f * lift.A + Flutter(1.7f * s.Clock, seed + 0.61f, 0.1f + 0.12f * s.Gust);
			}
			Step(ref lift, target, s.Dt, 30f, 0.35f, -0.1f, 1f);
			Step(ref yaw, target2, s.Dt, 20f, 0.5f, -0.45f, 0.45f);
			Step(ref tail, target3, s.Dt, 45f, 0.3f, -0.15f, 0.8f);
		}

		public static void Plume(ref Swing plume, in Input s, float seed, bool tall)
		{
			float num = (tall ? 0.15f : 0.25f);
			float target = 0f;
			if (!s.Still)
			{
				target = Math.Min(0.18f, 0.08f * s.Speed) - 0.5f * s.Along * s.Push + 0.03f * s.Stride * (float)Math.Sin(2f * s.Phase) + Flutter(1.3f * s.Clock, seed + 0.23f, 0.03f + 0.05f * s.Gust);
			}
			Step(ref plume, target, s.Dt, 40f, 0.4f, 0f - num, num);
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

		public static bool Clear(MeshInstance body, PersonRig rig, MeshModel set, int upper, int hem, ref float a1, ref float a2, float margin, MeshModel restBody = null)
		{
			LastClearance = float.PositiveInfinity;
			if (body?.Model == null || rig == null || set == null || upper < 0 || body.PartRotation == null || rig.Torso < 0)
			{
				return false;
			}
			Legs legs = LegsOf(restBody ?? body.Model, rig);
			Panel panel = PanelOf(set, upper);
			Panel panel2 = ((hem >= 0) ? PanelOf(set, hem) : null);
			if (legs.Count == 0 || panel == null)
			{
				return false;
			}
			float num = a1;
			float num2 = a2;
			for (int i = 0; i < legs.Count; i++)
			{
				Vec3 q = Posed(body, legs.Part[i], legs.At[i]);
				bool ok;
				Vec3 vec = IntoRest(body, rig.Torso, q, out ok);
				Tx[i] = vec.X;
				Ty[i] = vec.Y;
				Tz[i] = vec.Z;
				TOk[i] = ok;
			}
			float num3 = a1;
			for (int j = 0; j < legs.Count; j++)
			{
				if (!TOk[j])
				{
					continue;
				}
				float num4 = Tz[j];
				if (!(num4 < panel.ZLow - 0.05f) && !(num4 > panel.Hinge.Z - 0.1f))
				{
					float num5 = (float)Math.Atan2(Ty[j] + margin - (panel.Hinge.Y + panel.DepthAt(num4, Tx[j])), panel.Hinge.Z - num4);
					if (num5 > num3)
					{
						num3 = num5;
					}
				}
			}
			a1 = Math.Min(1f, Math.Max(0f, num3));
			if (panel2 != null)
			{
				for (int k = 0; k < 2; k++)
				{
					float num6 = a2;
					float num7 = 0f;
					float num8 = 0f;
					float c = (float)Math.Cos(0f - a1);
					float s = (float)Math.Sin(0f - a1);
					for (int l = 0; l < legs.Count; l++)
					{
						if (!TOk[l])
						{
							continue;
						}
						TurnX(Ty[l], Tz[l], panel.Hinge.Y, panel.Hinge.Z, c, s, out var ry, out var rz);
						if (!(rz >= panel.ZLow - 0.02f) && !(rz < panel2.ZLow - 0.05f) && !(rz > panel2.Hinge.Z - 0.02f))
						{
							float num9 = (float)Math.Atan2(ry + margin - (panel2.Hinge.Y + panel2.DepthAt(rz, Tx[l])), panel2.Hinge.Z - rz);
							if (num9 > num6)
							{
								num6 = num9;
							}
							if (num9 - 0.95f > num7)
							{
								num7 = num9 - 0.95f;
								num8 = rz;
							}
						}
					}
					a2 = Math.Min(0.95f, Math.Max(0f, num6));
					if (num7 <= 0f || k == 1)
					{
						break;
					}
					float num10 = Math.Max(0.05f, panel.Hinge.Z - num8);
					a1 = Math.Min(1f, a1 + num7 * Math.Max(0f, panel2.Hinge.Z - num8) / num10);
				}
			}
			LastClearance = Clearance(legs.Count, panel, panel2, a1, a2);
			if (!(a1 > num + 1E-05f))
			{
				return a2 > num2 + 1E-05f;
			}
			return true;
		}

		private static float Clearance(int count, Panel p1, Panel p2, float a1, float a2)
		{
			float num = float.PositiveInfinity;
			float c = (float)Math.Cos(0f - a1);
			float s = (float)Math.Sin(0f - a1);
			float c2 = (float)Math.Cos(0f - a2);
			float s2 = (float)Math.Sin(0f - a2);
			for (int i = 0; i < count; i++)
			{
				if (!TOk[i])
				{
					continue;
				}
				TurnX(Ty[i], Tz[i], p1.Hinge.Y, p1.Hinge.Z, c, s, out var ry, out var rz);
				if (rz >= p1.ZLow - 0.02f)
				{
					if (!(rz > p1.Hinge.Z - 0.1f))
					{
						num = Math.Min(num, p1.Hinge.Y + p1.DepthAt(rz, Tx[i]) - ry);
					}
				}
				else if (p2 != null)
				{
					TurnX(ry, rz, p2.Hinge.Y, p2.Hinge.Z, c2, s2, out var ry2, out var rz2);
					if (!(rz2 < p2.ZLow - 0.05f) && !(rz2 > p2.Hinge.Z - 0.02f))
					{
						num = Math.Min(num, p2.Hinge.Y + p2.DepthAt(rz2, Tx[i]) - ry2);
					}
				}
			}
			return num;
		}

		private static void TurnX(float y, float z, float hy, float hz, float c, float s, out float ry, out float rz)
		{
			float num = y - hy;
			float num2 = z - hz;
			ry = hy + c * num - s * num2;
			rz = hz + s * num + c * num2;
		}

		private static Vec3 Posed(MeshInstance m, int part, Vec3 p)
		{
			MeshModel model = m.Model;
			Mat3[] partRotation = m.PartRotation;
			Vec3[] partOffset = m.PartOffset;
			float[] partScale = m.PartScale;
			for (int num = part; num >= 0; num = model.Parts[num].Parent)
			{
				Vec3 pivot = model.Parts[num].Pivot;
				float num2 = p.X - pivot.X;
				float num3 = p.Y - pivot.Y;
				float num4 = p.Z - pivot.Z;
				if (partRotation != null)
				{
					ref Mat3 reference = ref partRotation[num];
					float num5 = reference.C0.X * num2 + reference.C1.X * num3 + reference.C2.X * num4;
					float num6 = reference.C0.Y * num2 + reference.C1.Y * num3 + reference.C2.Y * num4;
					float num7 = reference.C0.Z * num2 + reference.C1.Z * num3 + reference.C2.Z * num4;
					num2 = num5;
					num3 = num6;
					num4 = num7;
				}
				float num8 = ((partScale != null) ? partScale[num] : 1f);
				p = new Vec3(num2 * num8 + pivot.X, num3 * num8 + pivot.Y, num4 * num8 + pivot.Z);
				if (partOffset != null)
				{
					p += partOffset[num];
				}
			}
			return p;
		}

		private static Vec3 IntoRest(MeshInstance m, int part, Vec3 q, out bool ok)
		{
			ok = true;
			MeshModel model = m.Model;
			int num = 0;
			int num2 = part;
			while (num2 >= 0 && num < Chain.Length)
			{
				Chain[num++] = num2;
				num2 = model.Parts[num2].Parent;
			}
			Mat3[] partRotation = m.PartRotation;
			Vec3[] partOffset = m.PartOffset;
			float[] partScale = m.PartScale;
			for (int num3 = num - 1; num3 >= 0; num3--)
			{
				int num4 = Chain[num3];
				Vec3 pivot = model.Parts[num4].Pivot;
				Vec3 vec = q - pivot;
				if (partOffset != null)
				{
					vec -= partOffset[num4];
				}
				float sc = ((partScale != null) ? partScale[num4] : 1f);
				if (!Invert((partRotation != null) ? partRotation[num4] : Mat3.Identity, sc, out var inv))
				{
					ok = false;
					return q;
				}
				q = new Vec3(inv.C0.X * vec.X + inv.C1.X * vec.Y + inv.C2.X * vec.Z + pivot.X, inv.C0.Y * vec.X + inv.C1.Y * vec.Y + inv.C2.Y * vec.Z + pivot.Y, inv.C0.Z * vec.X + inv.C1.Z * vec.Y + inv.C2.Z * vec.Z + pivot.Z);
			}
			return q;
		}

		private static bool Invert(in Mat3 r, float sc, out Mat3 inv)
		{
			float num = r.C0.X * sc;
			float num2 = r.C1.X * sc;
			float num3 = r.C2.X * sc;
			float num4 = r.C0.Y * sc;
			float num5 = r.C1.Y * sc;
			float num6 = r.C2.Y * sc;
			float num7 = r.C0.Z * sc;
			float num8 = r.C1.Z * sc;
			float num9 = r.C2.Z * sc;
			float num10 = num5 * num9 - num6 * num8;
			float num11 = 0f - (num4 * num9 - num6 * num7);
			float num12 = num4 * num8 - num5 * num7;
			float num13 = num * num10 + num2 * num11 + num3 * num12;
			if (Math.Abs(num13) < 1E-09f)
			{
				inv = default(Mat3);
				return false;
			}
			float num14 = 1f / num13;
			inv = new Mat3(new Vec3(num10 * num14, num11 * num14, num12 * num14), new Vec3((0f - (num2 * num9 - num3 * num8)) * num14, (num * num9 - num3 * num7) * num14, (0f - (num * num8 - num2 * num7)) * num14), new Vec3((num2 * num6 - num3 * num5) * num14, (0f - (num * num6 - num3 * num4)) * num14, (num * num5 - num2 * num4) * num14));
			return true;
		}

		private static Legs LegsOf(MeshModel body, PersonRig rig)
		{
			if (LegCache.TryGetValue(body, out var value))
			{
				return value;
			}
			value = new Legs();
			for (int i = 0; i < 2; i++)
			{
				int num = body.PartIndex((i == 0) ? "upper_leg_L" : "upper_leg_R");
				int num2 = body.PartIndex((i == 0) ? "lower_leg_L" : "lower_leg_R");
				if (num2 >= 0 && body.Parts[num2].VertexCount > 0)
				{
					MeshPart meshPart = body.Parts[num2];
					ArmorMeshes.Bounds(meshPart, out var lo, out var _);
					float num3 = meshPart.Pivot.Z - lo.Z;
					value.Add(num2, meshPart.Pivot);
					value.Add(num2, BackAt(meshPart, meshPart.Pivot.X, lo.Z + 0.45f * num3));
					value.Add(num2, BackAt(meshPart, meshPart.Pivot.X, lo.Z + 0.05f * num3));
					value.Add(num2, BackAt(meshPart, meshPart.Pivot.X, lo.Z + 0.01f));
				}
				if (num >= 0 && body.Parts[num].VertexCount > 0)
				{
					MeshPart meshPart2 = body.Parts[num];
					float num4 = ((num2 >= 0) ? body.Parts[num2].Pivot.Z : (meshPart2.Pivot.Z - 0.4f));
					value.Add(num, BackAt(meshPart2, meshPart2.Pivot.X, num4 + 0.4f * (meshPart2.Pivot.Z - num4)));
				}
			}
			if (body.Info("robe") == "1")
			{
				for (int j = 0; j < body.Parts.Length; j++)
				{
					MeshPart meshPart3 = body.Parts[j];
					if (meshPart3.VertexCount != 0 && (!(meshPart3.Name != "hips") || (meshPart3.Name != null && meshPart3.Name.StartsWith("skirt_", StringComparison.Ordinal))))
					{
						ArmorMeshes.Bounds(meshPart3, out var lo2, out var hi2);
						float num5 = Math.Max(0.02f, Math.Min(meshPart3.Pivot.Z, hi2.Z) - lo2.Z);
						float[] skirtHeights = SkirtHeights;
						foreach (float num6 in skirtHeights)
						{
							value.Add(j, BackAt(meshPart3, 0f, lo2.Z + num6 * num5));
						}
					}
				}
			}
			if (LegCache.Count > 64)
			{
				LegCache.Clear();
			}
			LegCache[body] = value;
			return value;
		}

		private static Vec3 BackAt(MeshPart p, float x, float z)
		{
			Cut.Clear();
			ArmorMeshes.Slice(p, 2, z, Cut);
			float num = float.NegativeInfinity;
			foreach (Vec3 item in Cut)
			{
				if (item.Y > num)
				{
					num = item.Y;
				}
			}
			if (float.IsNegativeInfinity(num))
			{
				for (int i = 0; i < p.VertexCount; i++)
				{
					if (Math.Abs(p.Pos[i * 3 + 2] - z) < 0.03f && p.Pos[i * 3 + 1] > num)
					{
						num = p.Pos[i * 3 + 1];
					}
				}
			}
			return new Vec3(x, float.IsNegativeInfinity(num) ? p.Pivot.Y : num, z);
		}

		private static Panel PanelOf(MeshModel set, int part)
		{
			if (part < 0 || part >= set.Parts.Length)
			{
				return null;
			}
			if (!PanelCache.TryGetValue(set, out var value))
			{
				value = new Panel[set.Parts.Length];
				if (PanelCache.Count > 128)
				{
					PanelCache.Clear();
				}
				PanelCache[set] = value;
			}
			if (value[part] != null)
			{
				return value[part];
			}
			MeshPart meshPart = set.Parts[part];
			Panel panel = new Panel
			{
				Hinge = meshPart.Pivot,
				ZLow = meshPart.Pivot.Z,
				Depth = new float[0],
				Left = new float[0],
				Right = new float[0]
			};
			if (meshPart.VertexCount > 0)
			{
				ArmorMeshes.Bounds(meshPart, out var lo, out var _);
				panel.ZLow = lo.Z;
				int num = Math.Max(1, (int)Math.Ceiling((meshPart.Pivot.Z - lo.Z) / 0.04f) + 1);
				panel.Depth = Profile(meshPart, lo.Z, num, -0.13f, 0.13f) ?? new float[num];
				panel.Left = Profile(meshPart, lo.Z, num, -0.24f, -0.13f) ?? panel.Depth;
				panel.Right = Profile(meshPart, lo.Z, num, 0.13f, 0.24f) ?? panel.Depth;
			}
			value[part] = panel;
			return panel;
		}

		private static float[] Profile(MeshPart p, float zLow, int bins, float x0, float x1)
		{
			float[] array = new float[bins];
			for (int i = 0; i < bins; i++)
			{
				array[i] = float.PositiveInfinity;
			}
			bool flag = false;
			for (int j = 0; j < p.VertexCount; j++)
			{
				float num = p.Pos[j * 3] - p.Pivot.X;
				float num2 = p.Pos[j * 3 + 1];
				float num3 = p.Pos[j * 3 + 2];
				if (!(num < x0) && !(num > x1))
				{
					int num4 = Math.Max(0, Math.Min(bins - 1, (int)Math.Round((num3 - zLow) / 0.04f)));
					array[num4] = Math.Min(array[num4], num2 - p.Pivot.Y);
					flag = true;
				}
			}
			if (!flag)
			{
				return null;
			}
			float num5 = float.PositiveInfinity;
			for (int k = 0; k < bins; k++)
			{
				if (float.IsPositiveInfinity(array[k]))
				{
					array[k] = num5;
				}
				else
				{
					num5 = array[k];
				}
			}
			num5 = float.PositiveInfinity;
			for (int num6 = bins - 1; num6 >= 0; num6--)
			{
				if (float.IsPositiveInfinity(array[num6]))
				{
					array[num6] = num5;
				}
				else
				{
					num5 = array[num6];
				}
			}
			for (int l = 0; l < bins; l++)
			{
				if (float.IsPositiveInfinity(array[l]))
				{
					array[l] = 0f;
				}
			}
			return array;
		}
	}
}
