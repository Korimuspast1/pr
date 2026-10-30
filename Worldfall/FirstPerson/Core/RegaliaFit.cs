using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FirstPerson.Core
{
	public static class RegaliaFit
	{
		public struct Affine
		{
			public Mat3 F;

			public Vec3 D;
		}

		private readonly struct SashKey : IEquatable<SashKey>
		{
			private readonly MeshModel _body;

			private readonly MeshModel _set;

			private readonly int _part;

			private readonly int _metal;

			public SashKey(MeshModel body, MeshModel set, int part, int metal)
			{
				_body = body;
				_set = set;
				_part = part;
				_metal = metal;
			}

			public bool Equals(SashKey o)
			{
				if (_body == o._body && _set == o._set && _part == o._part)
				{
					return _metal == o._metal;
				}
				return false;
			}

			public override bool Equals(object o)
			{
				if (o is SashKey o2)
				{
					return Equals(o2);
				}
				return false;
			}

			public override int GetHashCode()
			{
				return ((RuntimeHelpers.GetHashCode(_body) * 31 + RuntimeHelpers.GetHashCode(_set)) * 31 + _part) * 17 + _metal;
			}
		}

		private readonly struct SocketKey : IEquatable<SocketKey>
		{
			private readonly MeshModel _body;

			private readonly int _metal;

			public SocketKey(MeshModel body, int metal)
			{
				_body = body;
				_metal = metal;
			}

			public bool Equals(SocketKey o)
			{
				if (_body == o._body)
				{
					return _metal == o._metal;
				}
				return false;
			}

			public override bool Equals(object o)
			{
				if (o is SocketKey o2)
				{
					return Equals(o2);
				}
				return false;
			}

			public override int GetHashCode()
			{
				return RuntimeHelpers.GetHashCode(_body) * 17 + _metal;
			}
		}

		public struct PoleLine
		{
			public Vec3 Foot;

			public Vec3 Top;

			public float Radius;

			public bool Valid;
		}

		public static int MeasuresPerFrame = 1;

		private static int _measures;

		public static int Measured;

		private static readonly Dictionary<SashKey, Affine> Sashes = new Dictionary<SashKey, Affine>();

		public static readonly Affine None = new Affine
		{
			F = Mat3.Identity,
			D = new Vec3(0f, 0f, 0f)
		};

		private static readonly List<Vec3> Needs = new List<Vec3>();

		private static float[] NeedOf = new float[0];

		public const float LiftPad = 0.003f;

		public const float SpreadPad = 0.008f;

		public const float MaxSpread = 2f;

		public const float MaxLift = 1.25f;

		public const float ChestFoot = 0.05f;

		public const float MaxShift = 0.05f;

		public const float TopReach = 0.05f;

		private static readonly Dictionary<SocketKey, Vec3> Sockets = new Dictionary<SocketKey, Vec3>();

		private static readonly Dictionary<MeshModel, Vec3> Caps = new Dictionary<MeshModel, Vec3>();

		private static readonly Dictionary<MeshModel, PoleLine> Poles = new Dictionary<MeshModel, PoleLine>();

		private static readonly Dictionary<MeshModel, (Vec3 top, Vec3 back)> Plumes = new Dictionary<MeshModel, (Vec3, Vec3)>();

		private static readonly float[] Tilts = new float[8] { 0.12f, 0f, 0.2f, 0.25f, 0.1f, 0.2f, 0.35f, 0.3f };

		private static readonly float[] Heights = new float[8] { 1f, 1f, 0.7f, 0.85f, 1f, 1f, 0.8f, 0.75f };

		public static void NewFrame()
		{
			_measures = 0;
		}

		private static bool MayMeasure()
		{
			if (_measures >= MeasuresPerFrame)
			{
				return false;
			}
			_measures++;
			Measured++;
			return true;
		}

		public static bool Sash(MeshModel body, int part, MeshModel set, string chestId, out Affine fit)
		{
			fit = None;
			if (string.IsNullOrEmpty(chestId) || body == null || set == null || part < 0 || part >= set.Parts.Length)
			{
				return true;
			}
			int num = (int)ArmorMeshes.MetalOf(chestId);
			SashKey key = new SashKey(body, set, part, num);
			if (Sashes.TryGetValue(key, out fit))
			{
				return true;
			}
			if (!MayMeasure())
			{
				fit = None;
				return false;
			}
			fit = MeasureSash(body, set.Parts[part], (ArmorMeshes.Metal)num);
			if (Sashes.Count > 512)
			{
				Sashes.Clear();
			}
			Sashes[key] = fit;
			return true;
		}

		private static Affine MeasureSash(MeshModel body, MeshPart piece, ArmorMeshes.Metal metal)
		{
			bool flag = piece.Name == "sashknot" || piece.Name == "keys" || piece.Name == "keys_over";
			int num = body.PartIndex("torso");
			int num2 = body.PartIndex("hips");
			MeshModel meshModel = ArmorMeshes.For(body, metal);
			if (num < 0 || meshModel == null || num >= meshModel.Parts.Length || piece.VertexCount == 0)
			{
				return None;
			}
			MeshPart meshPart = meshModel.Parts[num];
			MeshPart meshPart2 = ((num2 >= 0 && num2 < meshModel.Parts.Length) ? meshModel.Parts[num2] : null);
			if (meshPart.VertexCount < 4)
			{
				return None;
			}
			if (meshPart2 != null && meshPart2.VertexCount < 4)
			{
				meshPart2 = null;
			}
			ArmorMeshes.Bounds(meshPart, out var lo, out var hi);
			ArmorMeshes.Bounds(piece, out var lo2, out var _);
			float num3 = ShoulderZ(body);
			MeshPart p = ((flag && meshPart2 != null) ? meshPart2 : meshPart);
			ArmorMeshes.Bounds(p, out var lo3, out var hi3);
			ArmorMeshes.Ring ring = ArmorMeshes.Fit(p, flag ? ((lo3.Z + hi3.Z) * 0.5f) : Math.Min(num3, (lo.Z + hi.Z) * 0.5f), 0.02f);
			float x = ring.X;
			float y = ring.Y;
			float kx = 1f;
			float ky = 1f;
			float oy = 0f;
			float num4 = 1f;
			float z = lo2.Z;
			if (NeedOf.Length < piece.VertexCount)
			{
				NeedOf = new float[piece.VertexCount];
			}
			for (int i = 0; i < piece.VertexCount; i++)
			{
				NeedOf[i] = float.NegativeInfinity;
			}
			for (int j = 0; j < 6; j++)
			{
				Needs.Clear();
				for (int k = 0; k < piece.VertexCount; k++)
				{
					float num5 = piece.Pos[k * 3];
					float num6 = piece.Pos[k * 3 + 1];
					float num7 = piece.Pos[k * 3 + 2];
					if (!flag && num7 >= num3)
					{
						continue;
					}
					float num8 = z + num4 * (num7 - z);
					float num9 = num5 - x;
					float num10 = num6 - y;
					float num11 = (float)Math.Sqrt(num9 * num9 + num10 * num10);
					if (!(num11 < 0.02f))
					{
						float num12 = kx * num9;
						float num13 = ky * num10 + oy;
						float num14 = (float)Math.Sqrt(num12 * num12 + num13 * num13);
						float ux = ((num14 > 0.0001f) ? (num12 / num14) : (num9 / num11));
						float uy = ((num14 > 0.0001f) ? (num13 / num14) : (num10 / num11));
						float num15 = OutAlong(meshPart, x, y, num8, ux, uy);
						if ((flag || num8 < lo.Z + 0.05f) && meshPart2 != null)
						{
							num15 = Math.Max(num15, OutAlong(meshPart2, x, y, num8, ux, uy));
						}
						if (!float.IsNegativeInfinity(num15))
						{
							NeedOf[k] = Math.Max(NeedOf[k], num15 + 0.008f);
						}
						if (!float.IsNegativeInfinity(NeedOf[k]))
						{
							Needs.Add(new Vec3(num9, num10, NeedOf[k]));
						}
					}
				}
				float num16 = kx;
				float num17 = ky;
				float num18 = oy;
				Spread(ref kx, ref ky, ref oy);
				bool flag2 = j > 0 && Math.Abs(kx - num16) < 0.002f && Math.Abs(ky - num17) < 0.002f && Math.Abs(oy - num18) < 0.001f;
				if (flag)
				{
					if (flag2)
					{
						break;
					}
					continue;
				}
				float val = num4;
				for (int l = 0; l < piece.VertexCount; l++)
				{
					float num19 = piece.Pos[l * 3];
					float num20 = piece.Pos[l * 3 + 1];
					float num21 = piece.Pos[l * 3 + 2];
					if (num21 < num3 || num21 - z < 0.1f)
					{
						continue;
					}
					float x2 = x + kx * (num19 - x);
					float y2 = y + ky * (num20 - y) + oy;
					float num22 = TopUnder(meshPart, x2, y2, num21 - 0.12f, num21 + 0.05f);
					if (!float.IsNegativeInfinity(num22))
					{
						float num23 = num22 + 0.003f - num21;
						if (num23 > 0f)
						{
							val = Math.Max(val, 1f + num23 / (num21 - z));
						}
					}
				}
				val = Math.Min(val, 1.25f);
				if (val - num4 < 0.001f)
				{
					if (flag2)
					{
						break;
					}
				}
				else
				{
					num4 = val;
				}
			}
			Vec3 vec = new Vec3(x, y, z);
			Vec3 pivot = piece.Pivot;
			Vec3 d = new Vec3((kx - 1f) * (pivot.X - vec.X), (ky - 1f) * (pivot.Y - vec.Y) + oy, (num4 - 1f) * (pivot.Z - vec.Z));
			return new Affine
			{
				F = new Mat3(new Vec3(kx, 0f, 0f), new Vec3(0f, ky, 0f), new Vec3(0f, 0f, num4)),
				D = d
			};
		}

		private static void Spread(ref float kx, ref float ky, ref float oy)
		{
			kx = (ky = 1f);
			oy = 0f;
			if (Needs.Count == 0)
			{
				return;
			}
			float num = float.MaxValue;
			for (float num2 = -0.05f; num2 <= 0.05001f; num2 += 0.0025f)
			{
				for (float num3 = 1f; num3 <= 2.0001f; num3 += 0.01f)
				{
					float num4 = 1f;
					bool flag = true;
					foreach (Vec3 need in Needs)
					{
						float num5 = num3 * need.X;
						float num6 = need.Z * need.Z - num5 * num5;
						if (!(num6 <= 0f))
						{
							if (Math.Abs(need.Y) < 0.01f)
							{
								flag = false;
								break;
							}
							float num7 = (float)Math.Sqrt(num6);
							num4 = Math.Max(num4, (need.Y > 0f) ? ((num7 - num2) / need.Y) : ((num7 + num2) / (0f - need.Y)));
						}
					}
					if (!flag || num4 > 2f)
					{
						continue;
					}
					float num8 = 0f;
					foreach (Vec3 need2 in Needs)
					{
						float num9 = num3 * need2.X;
						float num10 = num4 * need2.Y + num2;
						num8 += (float)Math.Sqrt(num9 * num9 + num10 * num10) - need2.Z;
					}
					if (num8 < num)
					{
						num = num8;
						kx = num3;
						ky = num4;
						oy = num2;
					}
				}
			}
			if (num == float.MaxValue)
			{
				kx = (ky = 2f);
				oy = 0f;
			}
		}

		public static float ShoulderZ(MeshModel body)
		{
			int num = body?.PartIndex("torso") ?? (-1);
			if (num < 0 || body.Parts[num].VertexCount == 0)
			{
				return 1.38f;
			}
			ArmorMeshes.Bounds(body.Parts[num], out var _, out var hi);
			return hi.Z - 0.1f;
		}

		public static float OutAlong(MeshPart p, float x, float y, float z, float ux, float uy)
		{
			float num = float.NegativeInfinity;
			float[] pos = p.Pos;
			int[] tri = p.Tri;
			for (int i = 0; i + 2 < tri.Length; i += 3)
			{
				int num2 = tri[i] * 3;
				int num3 = tri[i + 1] * 3;
				int num4 = tri[i + 2] * 3;
				float num5 = pos[num2];
				float num6 = pos[num2 + 1];
				float num7 = pos[num2 + 2];
				float num8 = pos[num3] - num5;
				float num9 = pos[num3 + 1] - num6;
				float num10 = pos[num3 + 2] - num7;
				float num11 = pos[num4] - num5;
				float num12 = pos[num4 + 1] - num6;
				float num13 = pos[num4 + 2] - num7;
				float num14 = uy * num13;
				float num15 = (0f - ux) * num13;
				float num16 = ux * num12 - uy * num11;
				float num17 = num8 * num14 + num9 * num15 + num10 * num16;
				if (Math.Abs(num17) < 1E-12f)
				{
					continue;
				}
				float num18 = 1f / num17;
				float num19 = x - num5;
				float num20 = y - num6;
				float num21 = z - num7;
				float num22 = (num19 * num14 + num20 * num15 + num21 * num16) * num18;
				if (num22 < 0f || num22 > 1f)
				{
					continue;
				}
				float num23 = num20 * num10 - num21 * num9;
				float num24 = num21 * num8 - num19 * num10;
				float num25 = num19 * num9 - num20 * num8;
				float num26 = (ux * num23 + uy * num24) * num18;
				if (!(num26 < 0f) && !(num22 + num26 > 1f))
				{
					float num27 = (num11 * num23 + num12 * num24 + num13 * num25) * num18;
					if (num27 > 0f && num27 > num)
					{
						num = num27;
					}
				}
			}
			return num;
		}

		public static float TopUnder(MeshPart p, float x, float y, float z0, float z1)
		{
			float num = float.NegativeInfinity;
			float[] pos = p.Pos;
			int[] tri = p.Tri;
			for (int i = 0; i + 2 < tri.Length; i += 3)
			{
				int num2 = tri[i] * 3;
				int num3 = tri[i + 1] * 3;
				int num4 = tri[i + 2] * 3;
				float num5 = pos[num2];
				float num6 = pos[num2 + 1];
				float num7 = pos[num2 + 2];
				float num8 = pos[num3] - num5;
				float num9 = pos[num3 + 1] - num6;
				float num10 = pos[num3 + 2] - num7;
				float num11 = pos[num4] - num5;
				float num12 = pos[num4 + 1] - num6;
				float num13 = pos[num4 + 2] - num7;
				float num14 = num9 * num13 - num10 * num12;
				float num15 = num10 * num11 - num8 * num13;
				float num16 = num8 * num12 - num9 * num11;
				float num17 = (float)Math.Sqrt(num14 * num14 + num15 * num15 + num16 * num16);
				if (num17 < 1E-12f || Math.Abs(num16) < 0.3f * num17)
				{
					continue;
				}
				float num18 = num8 * num12 - num9 * num11;
				if (Math.Abs(num18) < 1E-12f)
				{
					continue;
				}
				float num19 = x - num5;
				float num20 = y - num6;
				float num21 = (num19 * num12 - num20 * num11) / num18;
				float num22 = (num8 * num20 - num9 * num19) / num18;
				if (!(num21 < 0f) && !(num22 < 0f) && !(num21 + num22 > 1f))
				{
					float num23 = num7 + num21 * num10 + num22 * num13;
					if (num23 >= z0 && num23 <= z1 && num23 > num)
					{
						num = num23;
					}
				}
			}
			return num;
		}

		public static bool Socket(MeshModel body, string helmetId, bool tall, out Vec3 at)
		{
			at = default(Vec3);
			if (body == null || string.IsNullOrEmpty(helmetId))
			{
				return false;
			}
			int num = (int)ArmorMeshes.MetalOf(helmetId);
			bool flag = tall && num == 6;
			SocketKey key = new SocketKey(body, flag ? (num + 16) : num);
			if (Sockets.TryGetValue(key, out at))
			{
				return true;
			}
			if (!MayMeasure())
			{
				return false;
			}
			at = MeasureSocket(body, (ArmorMeshes.Metal)num, flag);
			if (Sockets.Count > 256)
			{
				Sockets.Clear();
			}
			Sockets[key] = at;
			return true;
		}

		public static bool Socket(MeshModel body, string helmetId, out Vec3 at)
		{
			return Socket(body, helmetId, tall: false, out at);
		}

		private static Vec3 MeasureSocket(MeshModel body, ArmorMeshes.Metal metal, bool front)
		{
			int num = body.PartIndex("head");
			if (num < 0 || body.Parts[num].VertexCount < 8)
			{
				return new Vec3(0f, 0f, body.Max.Z);
			}
			ArmorMeshes.HeadFit headFit = ArmorMeshes.HeadFit.Of(body, body.Parts[num]);
			ArmorMeshes.Ring ring = headFit.At(headFit.Brow);
			MeshModel meshModel = ArmorMeshes.For(body, metal);
			MeshPart meshPart = ((meshModel != null && num < meshModel.Parts.Length) ? meshModel.Parts[num] : null);
			if (meshPart != null && meshPart.VertexCount > 0 && metal != ArmorMeshes.Metal.Mythril)
			{
				float num2 = float.NegativeInfinity;
				float y = ring.Y;
				for (int i = 0; i < meshPart.VertexCount; i++)
				{
					float num3 = meshPart.Pos[i * 3];
					float num4 = meshPart.Pos[i * 3 + 1];
					float num5 = meshPart.Pos[i * 3 + 2];
					if (Math.Abs(num3 - ring.X) < 0.02f && Math.Abs(num4 - ring.Y) < 0.06f && num5 > num2)
					{
						num2 = num5;
						y = num4;
					}
				}
				if (!float.IsNegativeInfinity(num2))
				{
					return new Vec3(ring.X, y, num2 - 0.008f);
				}
			}
			ArmorMeshes.Ring ring2 = ((meshPart != null && meshPart.VertexCount > 0) ? ArmorMeshes.Fit(meshPart, headFit.Brow + 0.05f * headFit.H, 0.012f, 1.15f) : ring.Grow(1f, 0.012f));
			return new Vec3(ring2.X, front ? (ring2.Y - ring2.Ry) : (ring2.Y + ring2.Ry), ring2.Z + 0.1f * headFit.H);
		}

		public static Vec3 CapSocket(MeshModel soldierF)
		{
			if (soldierF == null)
			{
				return default(Vec3);
			}
			if (Caps.TryGetValue(soldierF, out var value))
			{
				return value;
			}
			int num = soldierF.PartIndex("helmcap");
			value = ((num >= 0) ? soldierF.Parts[num].Pivot : new Vec3(0f, 0f, soldierF.Max.Z));
			if (num >= 0 && soldierF.Parts[num].VertexCount > 0)
			{
				MeshPart meshPart = soldierF.Parts[num];
				ArmorMeshes.Bounds(meshPart, out var lo, out var hi);
				float num2 = (lo.X + hi.X) * 0.5f;
				float num3 = (lo.Y + hi.Y) * 0.5f;
				float num4 = float.NegativeInfinity;
				for (int i = 0; i < meshPart.VertexCount; i++)
				{
					if (Math.Abs(meshPart.Pos[i * 3] - num2) < 0.03f && Math.Abs(meshPart.Pos[i * 3 + 1] - num3) < 0.05f)
					{
						num4 = Math.Max(num4, meshPart.Pos[i * 3 + 2]);
					}
				}
				value = new Vec3(num2, num3, (float.IsNegativeInfinity(num4) ? hi.Z : num4) - 0.006f);
			}
			Caps[soldierF] = value;
			return value;
		}

		public static PoleLine PoleOf(MeshModel captain)
		{
			if (captain == null)
			{
				return default(PoleLine);
			}
			if (Poles.TryGetValue(captain, out var value))
			{
				return value;
			}
			int num = captain.PartIndex("pole");
			int num2 = captain.PartIndex("banner");
			if (num >= 0 && num2 >= 0 && captain.Parts[num].VertexCount > 0)
			{
				MeshPart meshPart = captain.Parts[num];
				ArmorMeshes.Bounds(meshPart, out var lo, out var _);
				float num3 = 0f;
				float num4 = 0f;
				int num5 = 0;
				for (int i = 0; i < meshPart.VertexCount; i++)
				{
					if (meshPart.Pos[i * 3 + 2] < lo.Z + 0.02f)
					{
						num3 += meshPart.Pos[i * 3];
						num4 += meshPart.Pos[i * 3 + 1];
						num5++;
					}
				}
				value.Foot = new Vec3(num3 / (float)num5, num4 / (float)num5, lo.Z);
				value.Top = captain.Parts[num2].Pivot;
				for (int j = 0; j < meshPart.VertexCount; j++)
				{
					if (meshPart.Pos[j * 3 + 2] < lo.Z + 0.02f)
					{
						float num6 = meshPart.Pos[j * 3] - value.Foot.X;
						float num7 = meshPart.Pos[j * 3 + 1] - value.Foot.Y;
						value.Radius = Math.Max(value.Radius, (float)Math.Sqrt(num6 * num6 + num7 * num7));
					}
				}
				value.Valid = value.Top.Z - value.Foot.Z > 0.3f;
			}
			if (Poles.Count > 64)
			{
				Poles.Clear();
			}
			Poles[captain] = value;
			return value;
		}

		public static void PlumeCorners(MeshModel soldier, int plume, out Vec3 top, out Vec3 back)
		{
			if (Plumes.TryGetValue(soldier, out (Vec3, Vec3) value))
			{
				(top, back) = value;
				return;
			}
			top = (back = default(Vec3));
			MeshPart meshPart = soldier.Parts[plume];
			float num = float.NegativeInfinity;
			float num2 = float.NegativeInfinity;
			for (int i = 0; i < meshPart.VertexCount; i++)
			{
				float num3 = meshPart.Pos[i * 3] - meshPart.Pivot.X;
				float num4 = meshPart.Pos[i * 3 + 1] - meshPart.Pivot.Y;
				float num5 = meshPart.Pos[i * 3 + 2] - meshPart.Pivot.Z;
				if (!(Math.Abs(num3) > 0.02f))
				{
					if (num5 > num)
					{
						num = num5;
						top = new Vec3(num3, num4, num5);
					}
					if (num4 > num2)
					{
						num2 = num4;
						back = new Vec3(num3, num4, num5);
					}
				}
			}
			if (Plumes.Count > 64)
			{
				Plumes.Clear();
			}
			Plumes[soldier] = (top, back);
		}

		public static float Tilt(ArmorMeshes.Metal m)
		{
			return Tilts[(uint)(((int)m < Tilts.Length) ? m : ArmorMeshes.Metal.Iron)];
		}

		public static float Height(ArmorMeshes.Metal m)
		{
			return Heights[(uint)(((int)m < Heights.Length) ? m : ArmorMeshes.Metal.Iron)];
		}

		public static void Forget()
		{
			Sashes.Clear();
			Sockets.Clear();
			Caps.Clear();
			Poles.Clear();
			Plumes.Clear();
		}
	}
}
