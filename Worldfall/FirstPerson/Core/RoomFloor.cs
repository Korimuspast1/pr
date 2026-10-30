using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace FirstPerson.Core
{
	public sealed class RoomFloor
	{
		private sealed class Same : IEqualityComparer<float[]>
		{
			public static readonly Same Instance = new Same();

			public bool Equals(float[] a, float[] b)
			{
				return a == b;
			}

			public int GetHashCode(float[] a)
			{
				return RuntimeHelpers.GetHashCode(a);
			}
		}

		private sealed class Hatch
		{
			public float X0;

			public float Y0;

			public float X1;

			public float Y1;

			public float Top;

			public bool FrameW;

			public bool FrameE;

			public bool FrameS;

			public bool FrameN;
		}

		private sealed class PartGrower
		{
			private struct Vtx
			{
				public Vec3 P;

				public Vec3 N;
			}

			private readonly List<float> _pos = new List<float>();

			private readonly List<float> _nrm = new List<float>();

			private readonly List<int> _tri = new List<int>();

			private readonly List<byte> _slot = new List<byte>();

			private readonly List<byte> _axis = new List<byte>();

			public PartGrower(MeshPart part)
			{
				_pos.AddRange(part.Pos);
				_nrm.AddRange(part.Nrm);
				_tri.AddRange(part.Tri);
				_slot.AddRange(part.Slot);
				if (part.Axis != null)
				{
					_axis.AddRange(part.Axis);
				}
				while (_axis.Count < _slot.Count)
				{
					_axis.Add(2);
				}
			}

			public MeshPart Done(MeshPart like)
			{
				return new MeshPart
				{
					Name = like.Name,
					Parent = like.Parent,
					Pivot = like.Pivot,
					Pos = _pos.ToArray(),
					Nrm = _nrm.ToArray(),
					Tri = _tri.ToArray(),
					Slot = _slot.ToArray(),
					Axis = _axis.ToArray()
				};
			}

			private Vec3 P(int i)
			{
				return new Vec3(_pos[i * 3], _pos[i * 3 + 1], _pos[i * 3 + 2]);
			}

			private Vec3 N(int i)
			{
				return new Vec3(_nrm[i * 3], _nrm[i * 3 + 1], _nrm[i * 3 + 2]);
			}

			private int Corner(Vec3 p, Vec3 n)
			{
				_pos.Add(p.X);
				_pos.Add(p.Y);
				_pos.Add(p.Z);
				_nrm.Add(n.X);
				_nrm.Add(n.Y);
				_nrm.Add(n.Z);
				return _pos.Count / 3 - 1;
			}

			public void Cut(float x0, float y0, float x1, float y1, float cutZ)
			{
				int num = _tri.Count / 3;
				List<int> list = new List<int>(_tri.Count);
				List<byte> list2 = new List<byte>(num);
				List<byte> list3 = new List<byte>(num);
				List<(Vtx[], byte, byte)> list4 = new List<(Vtx[], byte, byte)>();
				for (int i = 0; i < num; i++)
				{
					int num2 = _tri[i * 3];
					int num3 = _tri[i * 3 + 1];
					int num4 = _tri[i * 3 + 2];
					Vec3 p = P(num2);
					Vec3 p2 = P(num3);
					Vec3 p3 = P(num4);
					float num5 = Math.Min(p.X, Math.Min(p2.X, p3.X));
					float num6 = Math.Max(p.X, Math.Max(p2.X, p3.X));
					float num7 = Math.Min(p.Y, Math.Min(p2.Y, p3.Y));
					float num8 = Math.Max(p.Y, Math.Max(p2.Y, p3.Y));
					if (!(Math.Min(p.Z, Math.Min(p2.Z, p3.Z)) >= cutZ) || !(num6 > x0) || !(num5 < x1) || !(num8 > y0) || !(num7 < y1))
					{
						list.Add(num2);
						list.Add(num3);
						list.Add(num4);
						list2.Add(_slot[i]);
						list3.Add(_axis[i]);
						continue;
					}
					Vtx[] poly = new Vtx[3]
					{
						new Vtx
						{
							P = p,
							N = N(num2)
						},
						new Vtx
						{
							P = p2,
							N = N(num3)
						},
						new Vtx
						{
							P = p3,
							N = N(num4)
						}
					};
					Add(list4, Clip(poly, 0, x0, keepAbove: false), _slot[i], _axis[i]);
					Add(list4, Clip(poly, 0, x1, keepAbove: true), _slot[i], _axis[i]);
					Vtx[] poly2 = Clip(Clip(poly, 0, x0, keepAbove: true), 0, x1, keepAbove: false);
					Add(list4, Clip(poly2, 1, y0, keepAbove: false), _slot[i], _axis[i]);
					Add(list4, Clip(poly2, 1, y1, keepAbove: true), _slot[i], _axis[i]);
				}
				_tri.Clear();
				_tri.AddRange(list);
				_slot.Clear();
				_slot.AddRange(list2);
				_axis.Clear();
				_axis.AddRange(list3);
				foreach (var item6 in list4)
				{
					Vtx[] item = item6.Item1;
					byte item2 = item6.Item2;
					byte item3 = item6.Item3;
					int item4 = Corner(item[0].P, item[0].N);
					int item5 = Corner(item[1].P, item[1].N);
					for (int j = 2; j < item.Length; j++)
					{
						int num9 = Corner(item[j].P, item[j].N);
						_tri.Add(item4);
						_tri.Add(item5);
						_tri.Add(num9);
						_slot.Add(item2);
						_axis.Add(item3);
						item5 = num9;
					}
				}
			}

			private static void Add(List<(Vtx[], byte, byte)> pieces, Vtx[] poly, byte slot, byte axis)
			{
				if (poly != null && poly.Length >= 3)
				{
					pieces.Add((poly, slot, axis));
				}
			}

			private static Vtx[] Clip(Vtx[] poly, int axis, float at, bool keepAbove)
			{
				if (poly == null || poly.Length < 3)
				{
					return null;
				}
				List<Vtx> list = new List<Vtx>(poly.Length + 2);
				for (int i = 0; i < poly.Length; i++)
				{
					Vtx item = poly[i];
					Vtx vtx = poly[(i + 1) % poly.Length];
					float num = ((axis == 0) ? item.P.X : item.P.Y) - at;
					float num2 = ((axis == 0) ? vtx.P.X : vtx.P.Y) - at;
					if (!keepAbove)
					{
						num = 0f - num;
						num2 = 0f - num2;
					}
					if (num >= 0f)
					{
						list.Add(item);
					}
					if (num >= 0f != num2 >= 0f)
					{
						float num3 = num / (num - num2);
						list.Add(new Vtx
						{
							P = item.P + (vtx.P - item.P) * num3,
							N = (item.N + (vtx.N - item.N) * num3).Normalized()
						});
					}
				}
				if (list.Count < 3)
				{
					return null;
				}
				return list.ToArray();
			}

			public void Face(int slot, params Vec3[] p)
			{
				Vec3 n = Vec3.Cross(p[1] - p[0], p[2] - p[0]).Normalized();
				byte item = (byte)((!(Math.Abs(n.X) >= Math.Abs(n.Y)) || !(Math.Abs(n.X) >= Math.Abs(n.Z))) ? ((Math.Abs(n.Y) >= Math.Abs(n.Z)) ? 1u : 2u) : 0u);
				int item2 = Corner(p[0], n);
				int item3 = Corner(p[1], n);
				for (int i = 2; i < p.Length; i++)
				{
					int num = Corner(p[i], n);
					_tri.Add(item2);
					_tri.Add(item3);
					_tri.Add(num);
					_slot.Add((byte)slot);
					_axis.Add(item);
					item3 = num;
				}
			}

			public void Box(int slot, Vec3 c, float hx, float hy, float hz)
			{
				Vec3 vec = new Vec3(hx, 0f, 0f);
				Vec3 vec2 = new Vec3(0f, hy, 0f);
				Vec3 vec3 = new Vec3(0f, 0f, hz);
				Quad(slot, c + vec, vec2, vec3);
				Quad(slot, c - vec, vec3, vec2);
				Quad(slot, c + vec2, vec3, vec);
				Quad(slot, c - vec2, vec, vec3);
				Quad(slot, c + vec3, vec, vec2);
				Quad(slot, c - vec3, vec2, vec);
			}

			private void Quad(int slot, Vec3 c, Vec3 u, Vec3 v)
			{
				Face(slot, c - u - v, c + u - v, c + u + v, c - u + v);
			}
		}

		public sealed class EyeCut
		{
			private float _cut;

			private float _vel;

			private bool _fresh = true;

			public void Reset()
			{
				_fresh = true;
			}

			public float Apply(float eye, float limit, float dt)
			{
				float num = ((float.IsInfinity(limit) || float.IsNaN(limit)) ? 0f : Math.Max(0f, eye - limit));
				if (_fresh || dt <= 0f)
				{
					_fresh = false;
					_cut = num;
					_vel = 0f;
					return eye - _cut;
				}
				_cut = Smooth(_cut, num, ref _vel, (num > _cut) ? 0.06f : 0.12f, Math.Min(dt, 0.1f));
				_cut = Math.Max(_cut, num - 0.15f);
				return eye - _cut;
			}

			private static float Smooth(float from, float to, ref float vel, float time, float dt)
			{
				float num = 2f / time;
				float num2 = num * dt;
				float num3 = 1f / (1f + num2 + 0.48f * num2 * num2 + 0.235f * num2 * num2 * num2);
				float num4 = from - to;
				float num5 = (vel + num * num4) * dt;
				vel = (vel - num * num5) * num3;
				float num6 = to + (num4 + num5) * num3;
				if (to - from > 0f == num6 > to)
				{
					num6 = to;
					vel = 0f;
				}
				return num6;
			}
		}

		public const float Cell = 0.05f;

		public const float Radius = 0.1f;

		private const float Tol = 0.03f;

		public const float WallClear = 0.14f;

		public const float PieceClear = 0.12f;

		public ColumnField Field;

		public readonly HashSet<float[]> WalkOn = new HashSet<float[]>(Same.Instance);

		public readonly List<float[]> Zones = new List<float[]>();

		private const float Shaft = 1.32f;

		private const float FrameR = 0.08f;

		private const float Stoop = 0.75f;

		private const float FloorHead = 1.2f;

		private const float RailKeep = 0.07f;

		private const float EyeSlope = 0.8f;

		public static float StepOf(float body)
		{
			return Math.Max(0.05f, (float)Math.Round(body * 0.28f * 20f) / 20f);
		}

		public static float HeadOf(float body)
		{
			return body * 1.04f;
		}

		public static float Stooped(Interior room, float body)
		{
			FitInfo fitInfo = room?.Fit;
			if (fitInfo == null || fitInfo.StoreyZ == null || fitInfo.StoreyZ.Length < 2)
			{
				return body;
			}
			return Math.Min(body, (fitInfo.StoreyZ[1] - 0.12f - 0.03f) / 1.04f);
		}

		public static RoomFloor Build(Interior room)
		{
			RoomFloor roomFloor = new RoomFloor();
			List<MeshInstance> list;
			lock (room.Meshes)
			{
				list = new List<MeshInstance>(room.Meshes);
			}
			if (list.Count == 0)
			{
				return roomFloor;
			}
			ColumnField columnField = ColumnField.Empty(room.MinX - 0.6f, room.MinY - 0.6f, room.MaxX + 0.6f, room.MaxY + 0.6f, 0.05f, (room.Fit != null && room.Fit.StoreyZ.Length > 1) ? 28 : 14);
			if (room.Fit != null)
			{
				columnField.Add(GroundOf(room), new Vec3(0f, 0f, 0f), Mat3.Identity, 0uL);
			}
			foreach (MeshInstance item in list)
			{
				if (item?.Model == null)
				{
					continue;
				}
				ulong num = item.Hidden;
				float[] partScale = item.PartScale;
				if (partScale != null)
				{
					for (int i = 0; i < partScale.Length && i < 64; i++)
					{
						if (partScale[i] < 0.01f)
						{
							num |= (ulong)(1L << i);
						}
					}
				}
				if (room.Fit != null && item == room.Shell && room.DoorLeafPart >= 0 && room.DoorLeafPart < 64)
				{
					num |= (ulong)(1L << room.DoorLeafPart);
				}
				if (room.Fit != null && item == room.Shell && room.DoorBarPart >= 0 && room.DoorBarPart < 64)
				{
					num |= (ulong)(1L << room.DoorBarPart);
				}
				columnField.Add(item.Model, item.Position, item.Basis, num);
				if (item.Model.Info("walk") != "1")
				{
					continue;
				}
				Box(item, item.Model.Min.X, item.Model.Min.Y, item.Model.Max.X, item.Model.Max.Y, out var x, out var y, out var x2, out var y2);
				roomFloor.Zones.Add(new float[4]
				{
					x - 0.4f,
					y - 0.4f,
					x2 + 0.4f,
					y2 + 0.4f
				});
				foreach (float[] solid in room.Solids)
				{
					if (Math.Abs(solid[0] - x - 0.03f) < 0.01f && Math.Abs(solid[1] - y - 0.03f) < 0.01f && Math.Abs(solid[2] - x2 + 0.03f) < 0.01f && Math.Abs(solid[3] - y2 + 0.03f) < 0.01f)
					{
						roomFloor.WalkOn.Add(solid);
					}
				}
			}
			roomFloor.Field = columnField.Finish();
			return roomFloor;
		}

		private static MeshModel GroundOf(Interior room)
		{
			MeshBuilder meshBuilder = new MeshBuilder("room_ground");
			int slot = meshBuilder.Slot("ground", 0, 0u, 0);
			meshBuilder.Part("ground", -1, new Vec3(0f, 0f, 0f));
			float x = room.MinX - 0.6f;
			float y = room.MinY - 0.6f;
			float x2 = room.MaxX + 0.6f;
			float y2 = room.MaxY + 0.6f;
			meshBuilder.Face(slot, new Vec3(x, y, 0f), new Vec3(x2, y, 0f), new Vec3(x2, y2, 0f), new Vec3(x, y2, 0f));
			return meshBuilder.Build();
		}

		private static void Box(MeshInstance inst, float mx0, float my0, float mx1, float my1, out float x0, out float y0, out float x1, out float y1)
		{
			x0 = (y0 = float.MaxValue);
			x1 = (y1 = float.MinValue);
			for (int i = 0; i < 4; i++)
			{
				Vec3 vec = inst.Position + inst.Basis.Mul(new Vec3(((i & 1) == 0) ? mx0 : mx1, ((i & 2) == 0) ? my0 : my1, 0f));
				x0 = Math.Min(x0, vec.X);
				x1 = Math.Max(x1, vec.X);
				y0 = Math.Min(y0, vec.Y);
				y1 = Math.Max(y1, vec.Y);
			}
		}

		public static bool Hatches(Interior room)
		{
			MeshInstance shell = room.Shell;
			if (shell?.Model == null || room.Top > 2.6f)
			{
				return false;
			}
			List<Hatch> list = new List<Hatch>();
			lock (room.Meshes)
			{
				foreach (MeshInstance mesh in room.Meshes)
				{
					string text = mesh?.Model?.Info("hatch");
					if (string.IsNullOrEmpty(text))
					{
						continue;
					}
					string[] array = text.Split(',');
					if (array.Length < 5)
					{
						continue;
					}
					float[] array2 = new float[5];
					bool flag = true;
					for (int i = 0; i < 5; i++)
					{
						flag &= float.TryParse(array[i], NumberStyles.Float, CultureInfo.InvariantCulture, out array2[i]);
					}
					if (!flag)
					{
						continue;
					}
					Hatch hatch = new Hatch();
					Box(mesh, array2[0], array2[1], array2[2], array2[3], out hatch.X0, out hatch.Y0, out hatch.X1, out hatch.Y1);
					hatch.Top = mesh.Position.Z + array2[4] * Math.Abs(mesh.Basis.C2.Z) + 1.32f;
					string text2 = ((array.Length > 5) ? array[5] : "xXyY");
					foreach (char c in text2)
					{
						ref Mat3 basis = ref mesh.Basis;
						Vec3 v;
						switch (c)
						{
						default:
							v = new Vec3(0f, 1f, 0f);
							break;
						case 'y':
							v = new Vec3(0f, -1f, 0f);
							break;
						case 'X':
							v = new Vec3(1f, 0f, 0f);
							break;
						case 'x':
							v = new Vec3(-1f, 0f, 0f);
							break;
						}
						Vec3 vec = basis.Mul(v);
						if (Math.Abs(vec.X) > Math.Abs(vec.Y))
						{
							if (vec.X < 0f)
							{
								hatch.FrameW = true;
							}
							else
							{
								hatch.FrameE = true;
							}
						}
						else if (vec.Y < 0f)
						{
							hatch.FrameS = true;
						}
						else
						{
							hatch.FrameN = true;
						}
					}
					list.Add(hatch);
				}
			}
			if (list.Count == 0)
			{
				return false;
			}
			MeshModel model = shell.Model;
			int num = model.SlotIndex("ceiling");
			int num2 = model.SlotIndex("timber");
			int num3 = model.SlotIndex("metal");
			if (num < 0)
			{
				return false;
			}
			if (num2 < 0)
			{
				num2 = num;
			}
			if (num3 < 0)
			{
				num3 = num2;
			}
			float top = room.Top;
			float cutZ = top - 0.25f;
			MeshPart[] array3 = new MeshPart[model.Parts.Length];
			for (int k = 0; k < array3.Length; k++)
			{
				array3[k] = model.Parts[k];
			}
			PartGrower partGrower = new PartGrower(array3[0]);
			for (int l = 0; l < array3.Length; l++)
			{
				PartGrower partGrower2 = ((l == 0) ? partGrower : new PartGrower(array3[l]));
				foreach (Hatch item in list)
				{
					partGrower2.Cut(item.X0, item.Y0, item.X1, item.Y1, cutZ);
				}
				if (l != 0)
				{
					array3[l] = partGrower2.Done(array3[l]);
				}
			}
			foreach (Hatch item2 in list)
			{
				Stairwell(partGrower, item2, top, num, num2, num3);
			}
			array3[0] = partGrower.Done(array3[0]);
			MeshModel meshModel = model.Derive(model.Name + "+hatch", array3);
			meshModel.Max = new Vec3(model.Max.X, model.Max.Y, Math.Max(model.Max.Z, MaxTop(list)));
			shell.Model = meshModel;
			return true;
		}

		private static float MaxTop(List<Hatch> cuts)
		{
			float num = 0f;
			foreach (Hatch cut in cuts)
			{
				num = Math.Max(num, cut.Top);
			}
			return num;
		}

		private static void Stairwell(PartGrower g, Hatch h, float H, int planks, int timber, int metal)
		{
			float num = 0.08f;
			float z = H - num;
			if (h.FrameS)
			{
				g.Box(timber, new Vec3((h.X0 + h.X1) * 0.5f, h.Y0, z), (h.X1 - h.X0) * 0.5f + num, num, num);
			}
			if (h.FrameN)
			{
				g.Box(timber, new Vec3((h.X0 + h.X1) * 0.5f, h.Y1, z), (h.X1 - h.X0) * 0.5f + num, num, num);
			}
			if (h.FrameW)
			{
				g.Box(timber, new Vec3(h.X0, (h.Y0 + h.Y1) * 0.5f, z), num, (h.Y1 - h.Y0) * 0.5f, num);
			}
			if (h.FrameE)
			{
				g.Box(timber, new Vec3(h.X1, (h.Y0 + h.Y1) * 0.5f, z), num, (h.Y1 - h.Y0) * 0.5f, num);
			}
			float num2 = h.X0 + (h.FrameW ? num : 0f);
			float num3 = h.X1 - (h.FrameE ? num : 0f);
			float num4 = h.Y0 + (h.FrameS ? num : 0f);
			float num5 = h.Y1 - (h.FrameN ? num : 0f);
			float num6 = H - 0.005f;
			float top = h.Top;
			float z2 = (num6 + top) * 0.5f;
			float hz = (top - num6) * 0.5f;
			g.Box(planks, new Vec3((num2 + num3) * 0.5f, num4 - 0.015f, z2), (num3 - num2) * 0.5f + 0.03f, 0.015f, hz);
			g.Box(planks, new Vec3((num2 + num3) * 0.5f, num5 + 0.015f, z2), (num3 - num2) * 0.5f + 0.03f, 0.015f, hz);
			g.Box(planks, new Vec3(num2 - 0.015f, (num4 + num5) * 0.5f, z2), 0.015f, (num5 - num4) * 0.5f, hz);
			g.Box(planks, new Vec3(num3 + 0.015f, (num4 + num5) * 0.5f, z2), 0.015f, (num5 - num4) * 0.5f, hz);
			g.Face(timber, new Vec3(num2, num4, top), new Vec3(num2, num5, top), new Vec3(num3, num5, top), new Vec3(num3, num4, top));
			float num7 = 0.04f;
			g.Box(timber, new Vec3((num2 + num3) * 0.5f, num4 + num7 * 0.5f, top - 0.03f), (num3 - num2) * 0.5f, num7 * 0.5f, 0.03f);
			g.Box(timber, new Vec3((num2 + num3) * 0.5f, num5 - num7 * 0.5f, top - 0.03f), (num3 - num2) * 0.5f, num7 * 0.5f, 0.03f);
			bool flag = num3 - num2 >= num5 - num4;
			for (int i = 0; i < 2; i++)
			{
				float num8 = 0.28f + 0.44f * (float)i;
				if (flag)
				{
					g.Box(metal, new Vec3(num2 + (num3 - num2) * num8, (num4 + num5) * 0.5f, top - 0.008f), 0.018f, (num5 - num4) * 0.5f - num7, 0.006f);
				}
				else
				{
					g.Box(metal, new Vec3((num2 + num3) * 0.5f, num4 + (num5 - num4) * num8, top - 0.008f), (num3 - num2) * 0.5f - num7, 0.018f, 0.006f);
				}
			}
			g.Box(metal, new Vec3((num2 + num3) * 0.5f, (num4 + num5) * 0.5f, top - 0.03f), 0.035f, 0.035f, 0.012f);
		}

		public float StandAt(float x, float y, float feet, float body)
		{
			if (Field == null || !Field.Support(x, y, 0.1f, feet + StepOf(body), 0.03f, out var z, out var _))
			{
				return 0f;
			}
			return Math.Max(0f, z);
		}

		public bool Clear(float x, float y, float z, float body)
		{
			if (Field == null || !Counts(x, y, z))
			{
				return true;
			}
			float num = ((z <= 0.03f) ? Math.Min(HeadOf(body), 1.2f) : HeadOf(body));
			float num2 = Field.CeilingAt(x, y, 0.1f, z + StepOf(body));
			if (num2 < z + num)
			{
				num = Math.Max(num * 0.75f, num2 - z - 0.01f);
			}
			return !Field.Blocked(x, y, 0.1f, z + StepOf(body), z + num);
		}

		private bool Counts(float x, float y, float z)
		{
			if (z > 0.03f)
			{
				return true;
			}
			foreach (float[] zone in Zones)
			{
				if (x > zone[0] && x < zone[2] && y > zone[1] && y < zone[3])
				{
					return true;
				}
			}
			return false;
		}

		public bool CanMove(float x, float y, float feet, float body)
		{
			if (Field == null)
			{
				return true;
			}
			float num = StandAt(x, y, feet, body);
			if (Clear(x, y, num, body))
			{
				return true;
			}
			if (num < feet - 0.01f)
			{
				return Clear(x, y, feet, body);
			}
			return false;
		}

		public float Settle(ref float x, ref float y, float feet, float body)
		{
			if (Field == null)
			{
				return 0f;
			}
			float num = StandAt(x, y, feet, body);
			if (Clear(x, y, num, body))
			{
				return num;
			}
			for (float num2 = 0.03f; num2 <= 0.18f; num2 += 0.03f)
			{
				for (int i = 0; i < 8; i++)
				{
					float num3 = (float)i * MathF.PI / 4f;
					float num4 = x + (float)Math.Cos(num3) * num2;
					float num5 = y + (float)Math.Sin(num3) * num2;
					float num6 = StandAt(num4, num5, feet, body);
					if (Clear(num4, num5, num6, body))
					{
						x = num4;
						y = num5;
						return num6;
					}
				}
			}
			return feet;
		}

		public float Pace(float x, float y, float dx, float dy, float feet, float body, float speed)
		{
			float num = (float)Math.Sqrt(dx * dx + dy * dy);
			if (Field == null || num < 1E-06f)
			{
				return 1f;
			}
			float num2 = StandAt(x + dx / num * 0.05f, y + dy / num * 0.05f, feet, body) - feet;
			float num3 = Math.Abs(num2) / 0.05f * speed;
			float num4 = ((num2 > 0f) ? 1.2f : 1.7f);
			if (!(num3 > num4))
			{
				return 1f;
			}
			return Math.Max(0.15f, num4 / num3);
		}

		public float CeilingOver(float x, float y, float feet, float body)
		{
			if (Field != null && Counts(x, y, feet))
			{
				return Field.CeilingAt(x, y, 0.060000002f, feet + body * 0.5f);
			}
			return float.PositiveInfinity;
		}

		public static float FittedCeiling(FitInfo fit, float x, float y, float feet, float eye = float.NaN)
		{
			int num = fit.StoreyAt(feet);
			float[] stairwell = fit.Stairwell;
			if (num == 0 && stairwell != null && fit.StoreyZ.Length > 1 && ((x > stairwell[0] && x < stairwell[2] && y > stairwell[1] && y < stairwell[3]) || eye > fit.StoreyZ[1]))
			{
				num = 1;
			}
			return fit.CeilingAt(num, x, y);
		}

		public static float FittedEyeCeiling(FitInfo fit, float x, float y, float feet, float eye = float.NaN, float shown = float.NaN)
		{
			float num = FittedCeiling(fit, x, y, feet, eye);
			float[] stairwell = fit.Stairwell;
			if (stairwell == null || fit.StoreyZ.Length < 2 || fit.StoreyAt(feet) != 0)
			{
				return num;
			}
			if (!(x > stairwell[0]) || !(x < stairwell[2]) || !(y > stairwell[1]) || !(y < stairwell[3]))
			{
				return num;
			}
			for (int i = 0; i < 4; i++)
			{
				float num2;
				switch (i)
				{
				default:
					num2 = stairwell[3] - y;
					break;
				case 2:
					num2 = y - stairwell[1];
					break;
				case 1:
					num2 = stairwell[2] - x;
					break;
				case 0:
					num2 = x - stairwell[0];
					break;
				}
				float num3 = num2;
				if (num3 * 0.8f > 1.5f)
				{
					continue;
				}
				float num4;
				switch (i)
				{
				default:
					num4 = x;
					break;
				case 1:
					num4 = stairwell[2] + 0.03f;
					break;
				case 0:
					num4 = stairwell[0] - 0.03f;
					break;
				}
				float num5 = num4;
				float num6;
				switch (i)
				{
				default:
					num6 = y;
					break;
				case 3:
					num6 = stairwell[3] + 0.03f;
					break;
				case 2:
					num6 = stairwell[1] - 0.03f;
					break;
				}
				float num7 = num6;
				if (fit.Inside(0, num5, num7))
				{
					float num8 = StairZ(fit, num5, num7);
					if (!float.IsNaN(num8))
					{
						num = Math.Min(num, fit.CeilingAt(0, num5, num7) + num3 * 0.8f + (float.IsNaN(shown) ? feet : shown) - num8);
					}
				}
			}
			return num;
		}

		public static bool ThroughRail(FitInfo fit, float fromX, float fromY, float x, float y, float feet)
		{
			if (fit == null || float.IsNaN(fit.StairFootX) || fit.StoreyZ.Length < 2 || feet < 0.03f)
			{
				return false;
			}
			float num = fit.StairHeadX - fit.StairFootX;
			float num2 = fit.StairHeadY - fit.StairFootY;
			float num3 = (float)Math.Sqrt(num * num + num2 * num2);
			if (num3 < 0.0001f)
			{
				return false;
			}
			num /= num3;
			num2 /= num3;
			float num4 = fit.StairWidth * 0.5f;
			float num5 = num3 / (float)Math.Max(2, fit.StairSteps);
			float num6 = (fromX - fit.StairFootX) * num + (fromY - fit.StairFootY) * num2;
			float value = (fromX - fit.StairFootX) * (0f - num2) + (fromY - fit.StairFootY) * num;
			if (num6 < num5 * 1.5f || num6 > num3 || Math.Abs(value) > num4 + 0.06f)
			{
				return false;
			}
			float num7 = (x - fit.StairFootX) * (0f - num2) + (y - fit.StairFootY) * num;
			if (Math.Abs(num7) <= Math.Abs(value) || Math.Abs(num7) < num4 - 0.07f)
			{
				return false;
			}
			float num8 = num7 * ((0f - num2) * fit.StairOpenX + num * fit.StairOpenY);
			if (!fit.StairFree)
			{
				return num8 > 0f;
			}
			return true;
		}

		public static bool IntoPiece(float[] box, float r, float fromX, float fromY, float x, float y)
		{
			float num = Depth(box, r, x, y);
			if (num <= 0f)
			{
				return false;
			}
			float num2 = Depth(box, r, fromX, fromY);
			if (!(num2 <= 0f))
			{
				return num > num2 + 0.0001f;
			}
			return true;
		}

		private static float Depth(float[] b, float r, float x, float y)
		{
			return Math.Min(Math.Min(x - (b[0] - r), b[2] + r - x), Math.Min(y - (b[1] - r), b[3] + r - y));
		}

		private static float StairZ(FitInfo fit, float x, float y)
		{
			if (float.IsNaN(fit.StairFootX))
			{
				return 0f;
			}
			float num = fit.StairHeadX - fit.StairFootX;
			float num2 = fit.StairHeadY - fit.StairFootY;
			float num3 = (float)Math.Sqrt(num * num + num2 * num2);
			if (num3 < 0.0001f)
			{
				return 0f;
			}
			num /= num3;
			num2 /= num3;
			float num4 = (x - fit.StairFootX) * num + (y - fit.StairFootY) * num2;
			float num5 = Math.Abs((x - fit.StairFootX) * (0f - num2) + (y - fit.StairFootY) * num);
			if (num4 <= 0f || num5 > fit.StairWidth * 0.5f + 0.06f)
			{
				return 0f;
			}
			if (!(num4 >= num3 - 0.02f))
			{
				return fit.StoreyZ[1] * num4 / num3;
			}
			return float.NaN;
		}
	}
}
