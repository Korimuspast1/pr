using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class Slicing
	{
		private sealed class Mark
		{
			public float At;

			public Vec3 Across;

			public Vec3 Toward;

			public float Tilt;
		}

		private sealed class Half
		{
			public object Space;

			public MeshInstance Mesh;

			public Vec3 Min;

			public Vec3 Max;

			public Vec3 Vel;

			public Vec3 Spin;

			public Mat3 Turn = Mat3.Identity;

			public Mat3 Base = Mat3.Identity;

			public Vec3 Pivot;

			public Vec3 Start;

			public Vec3 Axis;

			public bool Topples;

			public float Ground;

			public float Gravity;

			public float Age;

			public float Delay;

			public float Height;

			public bool Landed;

			public int Rest = -1;

			public bool Bleeds;

			public Vec3 CutLocal;

			public Vec3 CutNormal;
		}

		private sealed class Piece
		{
			public readonly List<Vec3> Pos = new List<Vec3>(2048);

			public readonly List<Vec3> Nrm = new List<Vec3>(2048);

			public readonly List<byte> Slot = new List<byte>(700);

			public readonly List<Vec3> Edges = new List<Vec3>(256);

			public Vec3 CutMiddle;

			public int Count => Slot.Count;

			public void Tri(Vec3 a, Vec3 b, Vec3 c, Vec3 na, Vec3 nb, Vec3 nc, byte slot)
			{
				Pos.Add(a);
				Pos.Add(b);
				Pos.Add(c);
				Nrm.Add(na);
				Nrm.Add(nb);
				Nrm.Add(nc);
				Slot.Add(slot);
			}

			public void Edge(Vec3 a, Vec3 b)
			{
				Edges.Add(a);
				Edges.Add(b);
			}

			public void Cap(Vec3 outward, byte slot)
			{
				int num = Edges.Count / 2;
				if (num == 0)
				{
					return;
				}
				int[] parent = new int[num];
				for (int i = 0; i < num; i++)
				{
					parent[i] = i;
				}
				Dictionary<long, int> dictionary = new Dictionary<long, int>();
				for (int j = 0; j < num; j++)
				{
					for (int k = 0; k < 2; k++)
					{
						long key = Key(Edges[j * 2 + k]);
						if (dictionary.TryGetValue(key, out var value))
						{
							parent[Root(j)] = Root(value);
						}
						else
						{
							dictionary[key] = j;
						}
					}
				}
				Dictionary<int, Vec3> dictionary2 = new Dictionary<int, Vec3>();
				Dictionary<int, int> dictionary3 = new Dictionary<int, int>();
				Vec3 vec = default(Vec3);
				for (int l = 0; l < num; l++)
				{
					int key2 = Root(l);
					dictionary2.TryGetValue(key2, out var value2);
					dictionary3.TryGetValue(key2, out var value3);
					dictionary2[key2] = value2 + Edges[l * 2] + Edges[l * 2 + 1];
					dictionary3[key2] = value3 + 2;
					vec = vec + Edges[l * 2] + Edges[l * 2 + 1];
				}
				CutMiddle = vec * (1f / (float)(num * 2));
				for (int m = 0; m < num; m++)
				{
					int key3 = Root(m);
					Vec3 vec2 = dictionary2[key3] * (1f / (float)dictionary3[key3]);
					Vec3 vec3 = Edges[m * 2];
					Vec3 vec4 = Edges[m * 2 + 1];
					if (Vec3.Dot(Vec3.Cross(vec3 - vec2, vec4 - vec2), outward) < 0f)
					{
						Vec3 vec5 = vec3;
						vec3 = vec4;
						vec4 = vec5;
					}
					Tri(vec2, vec3, vec4, outward, outward, outward, slot);
				}
				int Root(int num2)
				{
					while (parent[num2] != num2)
					{
						num2 = (parent[num2] = parent[parent[num2]]);
					}
					return num2;
				}
			}

			private static long Key(Vec3 v)
			{
				long num = (long)Math.Round((double)v.X * 2000.0);
				long num2 = (long)Math.Round((double)v.Y * 2000.0);
				long num3 = (long)Math.Round((double)v.Z * 2000.0);
				return (num * 73856093) ^ (num2 * 19349663) ^ (num3 * 83492791);
			}
		}

		private const float FallSeconds = 0.6f;

		private const float LieSeconds = 7f;

		private const float SinkSeconds = 1.6f;

		private const float Life = 9.5f;

		private const int Max = 12;

		internal static bool Blow;

		internal static int Cuts;

		internal static int LastUpper;

		internal static int LastLower;

		internal static bool CutAnyForTest;

		private static readonly Dictionary<Actor, Mark> Marks = new Dictionary<Actor, Mark>();

		private static readonly List<Half> Halves = new List<Half>();

		private static int _side = 1;

		private static readonly System.Random Rng = new System.Random();

		private static readonly Dictionary<string, bool> TopCache = new Dictionary<string, bool>();

		private static float _topDamage = -1f;

		private static readonly List<Actor> Old = new List<Actor>();

		private static readonly List<Vec3> BakedPos = new List<Vec3>(4096);

		private static readonly List<Vec3> BakedNrm = new List<Vec3>(4096);

		private static readonly List<byte> BakedSlot = new List<byte>(1400);

		private static readonly uint CutColor = TerrainCache.Rgb(150, 24, 30);

		private static int _aged = -1;

		private static readonly Vec3[] Axes = new Vec3[6]
		{
			new Vec3(1f, 0f, 0f),
			new Vec3(-1f, 0f, 0f),
			new Vec3(0f, 1f, 0f),
			new Vec3(0f, -1f, 0f),
			new Vec3(0f, 0f, 1f),
			new Vec3(0f, 0f, -1f)
		};

		public static int Count => Halves.Count;

		public static bool IsTopSword(string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				return false;
			}
			if (TopCache.TryGetValue(id, out var value))
			{
				return value;
			}
			value = false;
			if (id.Contains("sword") && !id.Contains("dagger") && !id.Contains("knife"))
			{
				if (id.Contains("adamantine"))
				{
					value = true;
				}
				else
				{
					if (_topDamage < 0f)
					{
						_topDamage = Damage("sword_adamantine");
					}
					float num = Damage(id);
					value = _topDamage > 0f && num >= _topDamage;
				}
			}
			TopCache[id] = value;
			return value;
		}

		private static float Damage(string id)
		{
			try
			{
				return AssetManager.items.get(id)?.base_stats["damage"] ?? 0f;
			}
			catch
			{
				return 0f;
			}
		}

		public static void Struck(Actor victim)
		{
			if (victim == null)
			{
				return;
			}
			try
			{
				if (victim.getHealth() > 0)
				{
					return;
				}
				if (!CutAnyForTest)
				{
					if (!Blow)
					{
						return;
					}
					Actor unit_main = ControllableUnit._unit_main;
					if (!IsTopSword((unit_main == null || unit_main.getWeapon() == null) ? null : unit_main.getWeaponAsset()?.id))
					{
						return;
					}
				}
			}
			catch
			{
				return;
			}
			Vector2 flat = FirstPersonAim.Flat;
			_side = -_side;
			if (Marks.Count > 16)
			{
				Prune();
			}
			Marks[victim] = new Mark
			{
				At = Time.unscaledTime,
				Toward = new Vec3(flat.x, flat.y, 0f),
				Across = new Vec3(0f - flat.y, flat.x, 0f) * _side,
				Tilt = 0.25f + 0.2f * (float)Rng.NextDouble()
			};
		}

		internal static void MarkCut(Actor victim, Vector2 toward, int side)
		{
			MarkForTest(victim, toward, side);
		}

		internal static void MarkForTest(Actor victim, Vector2 toward, int side)
		{
			toward.Normalize();
			Marks[victim] = new Mark
			{
				At = Time.unscaledTime,
				Toward = new Vec3(toward.x, toward.y, 0f),
				Across = new Vec3(0f - toward.y, toward.x, 0f) * side,
				Tilt = 0.35f
			};
		}

		public static bool Split(Actor who, MeshInstance mesh, MeshInstance weapon, bool beast)
		{
			if (who == null || !Marks.TryGetValue(who, out var value))
			{
				if (!CutAnyForTest || who == null)
				{
					return false;
				}
				value = null;
			}
			Marks.Remove(who);
			if (value == null)
			{
				Vec3 eye = FirstPersonAim.Eye;
				Vector2 vector = new Vector2(mesh.Position.X - eye.X, mesh.Position.Y - eye.Y);
				if (vector.sqrMagnitude < 0.0001f)
				{
					vector = Vector2.right;
				}
				vector.Normalize();
				_side = -_side;
				value = new Mark
				{
					Toward = new Vec3(vector.x, vector.y, 0f),
					Across = new Vec3(0f - vector.y, vector.x, 0f) * _side,
					Tilt = 0.35f,
					At = Time.unscaledTime
				};
			}
			if (Time.unscaledTime - value.At > 2f || mesh == null || mesh.Model == null)
			{
				return false;
			}
			try
			{
				return Cut(mesh, weapon, value, beast);
			}
			catch (Exception ex)
			{
				Log.Error("slicing: " + ex.Message);
				return false;
			}
		}

		private static void Prune()
		{
			Old.Clear();
			foreach (KeyValuePair<Actor, Mark> mark in Marks)
			{
				if (Time.unscaledTime - mark.Value.At > 2f)
				{
					Old.Add(mark.Key);
				}
			}
			foreach (Actor item in Old)
			{
				Marks.Remove(item);
			}
		}

		public static void Clear()
		{
			Halves.Clear();
			Marks.Clear();
		}

		internal static void HalvesForTest(List<KeyValuePair<Vector3, float>> into, List<bool> bodies)
		{
			into.Clear();
			bodies.Clear();
			foreach (Half half in Halves)
			{
				Vec3 position = half.Mesh.Position;
				Vec3 vec = half.Mesh.Basis.Mul(new Vec3(0f, 0f, 1f)).Normalized();
				into.Add(new KeyValuePair<Vector3, float>(new Vector3(position.X, position.Y, position.Z), Mathf.Acos(Mathf.Clamp(vec.Z, -1f, 1f)) * 57.29578f));
				bodies.Add(half.Bleeds);
			}
		}

		private static bool Cut(MeshInstance mesh, MeshInstance weapon, Mark mark, bool beast)
		{
			Bake(mesh);
			int count = BakedSlot.Count;
			if (count < 8)
			{
				return false;
			}
			Vec3 vec = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
			Vec3 vec2 = new Vec3(float.MinValue, float.MinValue, float.MinValue);
			foreach (Vec3 bakedPo in BakedPos)
			{
				vec = new Vec3(Mathf.Min(vec.X, bakedPo.X), Mathf.Min(vec.Y, bakedPo.Y), Mathf.Min(vec.Z, bakedPo.Z));
				vec2 = new Vec3(Mathf.Max(vec2.X, bakedPo.X), Mathf.Max(vec2.Y, bakedPo.Y), Mathf.Max(vec2.Z, bakedPo.Z));
			}
			float num = vec2.Z - vec.Z;
			if (num <= 0.0001f)
			{
				return false;
			}
			Vec3 vec3 = new Vec3(0f, 0f, 1f);
			Vec3 vec5;
			if (beast)
			{
				Vec3 vec4 = mesh.Basis.C1 * -1f;
				vec4 = new Vec3(vec4.X, vec4.Y, 0f).Normalized();
				if (Vec3.Dot(vec4, vec4) < 0.5f)
				{
					vec4 = mark.Toward;
				}
				vec5 = (vec4 + mark.Across * 0.3f + vec3 * 0.15f).Normalized();
			}
			else
			{
				vec5 = (vec3 * Mathf.Cos(mark.Tilt) + mark.Across * Mathf.Sin(mark.Tilt)).Normalized();
			}
			Vec3 vec6 = (vec + vec2) * 0.5f;
			float num2 = float.MaxValue;
			float num3 = float.MinValue;
			foreach (Vec3 bakedPo2 in BakedPos)
			{
				float b = Vec3.Dot(bakedPo2 - vec6, vec5);
				num2 = Mathf.Min(num2, b);
				num3 = Mathf.Max(num3, b);
			}
			Vec3 vec7 = vec6 + vec5 * ((num2 + num3) * 0.5f);
			Piece piece = new Piece();
			Piece piece2 = new Piece();
			for (int i = 0; i < count; i++)
			{
				int num4 = i * 3;
				Vec3 vec8 = BakedPos[num4];
				Vec3 vec9 = BakedPos[num4 + 1];
				Vec3 vec10 = BakedPos[num4 + 2];
				float num5 = Vec3.Dot(vec8 - vec7, vec5);
				float num6 = Vec3.Dot(vec9 - vec7, vec5);
				float num7 = Vec3.Dot(vec10 - vec7, vec5);
				byte slot = BakedSlot[i];
				if (num5 >= 0f && num6 >= 0f && num7 >= 0f)
				{
					piece.Tri(vec8, vec9, vec10, BakedNrm[num4], BakedNrm[num4 + 1], BakedNrm[num4 + 2], slot);
				}
				else if (num5 <= 0f && num6 <= 0f && num7 <= 0f)
				{
					piece2.Tri(vec8, vec9, vec10, BakedNrm[num4], BakedNrm[num4 + 1], BakedNrm[num4 + 2], slot);
				}
				else
				{
					Split(piece, piece2, vec8, vec9, vec10, BakedNrm[num4], BakedNrm[num4 + 1], BakedNrm[num4 + 2], num5, num6, num7, slot);
				}
			}
			if (piece.Count < 6 || piece2.Count < 6)
			{
				return false;
			}
			MeshModel model = mesh.Model;
			int num8 = Mathf.Min(model.SlotColor.Length, 63);
			piece.Cap(vec5 * -1f, (byte)num8);
			piece2.Cap(vec5, (byte)num8);
			float num9 = Mathf.Max(0.05f, mesh.Basis.C2.Z);
			float z = mesh.Position.Z;
			Half half = Make(piece, model, num8, mesh, "slice_upper", vec5 * -1f);
			Half half2 = Make(piece2, model, num8, mesh, "slice_lower", vec5);
			half.Ground = (half2.Ground = z);
			half.Gravity = (half2.Gravity = 9.8f * num9);
			half.Height = (half2.Height = num);
			Vec3 vec11 = (beast ? (new Vec3(vec5.X, vec5.Y, 0f).Normalized() * 0.8f + mark.Across * 0.6f).Normalized() : mark.Across);
			half.Vel = vec11 * ((beast ? 2.2f : 1.6f) * num) + vec3 * ((beast ? 1.4f : 0.9f) * num) + mark.Toward * (0.5f * num);
			if (RoomCombat.FallingIn != null)
			{
				Vector2 away = new Vector2(half.Vel.X, half.Vel.Y);
				Vector2 vector = RoomCombat.FallWay(RoomCombat.FallingIn, new Vector2(half.Mesh.Position.X, half.Mesh.Position.Y), z, away, num);
				if (vector.sqrMagnitude > 1E-06f && away.sqrMagnitude > 1E-06f)
				{
					vector = vector.normalized * away.magnitude;
					half.Vel = new Vec3(vector.x, vector.y, half.Vel.Z);
					vec11 = new Vec3(vector.x, vector.y, 0f).Normalized();
				}
			}
			half.Spin = Vec3.Cross(vec3, vec11).Normalized() * (beast ? 5f : 5.5f);
			half2.Topples = true;
			half2.Delay = (beast ? 0.1f : 0.3f);
			Vec3 vec12 = (half2.Start = half2.Mesh.Position);
			half2.Pivot = new Vec3(vec12.X, vec12.Y, z);
			Vec3 b2 = mark.Toward;
			if (RoomCombat.FallingIn != null)
			{
				Vector2 vector2 = RoomCombat.FallWay(RoomCombat.FallingIn, new Vector2(vec12.X, vec12.Y), z, new Vector2(b2.X, b2.Y), num * 0.5f);
				b2 = new Vec3(vector2.x, vector2.y, 0f);
			}
			half2.Axis = Vec3.Cross(vec3, b2).Normalized();
			Add(half);
			Add(half2);
			if (weapon != null && weapon.Model != null)
			{
				AddWeapon(weapon, mark, num, num9, z);
			}
			Spray(vec7, vec5, mark, num, num9, 34);
			Cuts++;
			LastUpper = piece.Count;
			LastLower = piece2.Count;
			Log.Info($"slicing: cut {model.Name} in two ({piece.Count} + {piece2.Count} triangles, {num:F2} high)");
			return true;
		}

		private static void Bake(MeshInstance m)
		{
			BakedPos.Clear();
			BakedNrm.Clear();
			BakedSlot.Clear();
			MeshModel model = m.Model;
			int num = model.Parts.Length;
			Mat3[] array = new Mat3[num];
			Vec3[] array2 = new Vec3[num];
			for (int i = 0; i < num; i++)
			{
				MeshPart meshPart = model.Parts[i];
				Mat3 a = ((m.PartRotation != null) ? m.PartRotation[i] : Mat3.Identity);
				float num2 = ((m.PartScale != null) ? m.PartScale[i] : 1f);
				if (num2 != 1f)
				{
					a *= num2;
				}
				Vec3 vec = meshPart.Pivot - a.Mul(meshPart.Pivot);
				if (m.PartOffset != null)
				{
					vec += m.PartOffset[i];
				}
				if (meshPart.Parent >= 0)
				{
					array[i] = array[meshPart.Parent] * a;
					array2[i] = array[meshPart.Parent].Mul(vec) + array2[meshPart.Parent];
				}
				else
				{
					array[i] = a;
					array2[i] = vec;
				}
			}
			for (int j = 0; j < num; j++)
			{
				Mat3 wm;
				Vec3 wt;
				float[] pos;
				float[] nrm;
				if (j >= 64 || (m.Hidden & (ulong)(1L << j)) == 0L)
				{
					MeshPart meshPart2 = model.Parts[j];
					wm = m.Basis * array[j];
					wt = m.Basis.Mul(array2[j]) + m.Position;
					bool flag = Vec3.Dot(wm.C0, Vec3.Cross(wm.C1, wm.C2)) < 0f;
					pos = meshPart2.Pos;
					nrm = meshPart2.Nrm;
					int[] tri = meshPart2.Tri;
					int k = 0;
					for (int triangleCount = meshPart2.TriangleCount; k < triangleCount; k++)
					{
						int v = tri[k * 3];
						int v2 = tri[k * 3 + ((!flag) ? 1 : 2)];
						int v3 = tri[k * 3 + (flag ? 1 : 2)];
						Vertex(v);
						Vertex(v2);
						Vertex(v3);
						BakedSlot.Add(meshPart2.Slot[k]);
					}
				}
				void Vertex(int num3)
				{
					BakedPos.Add(wm.Mul(new Vec3(pos[num3 * 3], pos[num3 * 3 + 1], pos[num3 * 3 + 2])) + wt);
					BakedNrm.Add(wm.Mul(new Vec3(nrm[num3 * 3], nrm[num3 * 3 + 1], nrm[num3 * 3 + 2])).Normalized());
				}
			}
		}

		private static void Split(Piece upper, Piece lower, Vec3 a, Vec3 b, Vec3 c, Vec3 na, Vec3 nb, Vec3 nc, float da, float db, float dc, byte slot)
		{
			Vec3[] array = new Vec3[3] { a, b, c };
			Vec3[] array2 = new Vec3[3] { na, nb, nc };
			float[] array3 = new float[3] { da, db, dc };
			List<Vec3> list = new List<Vec3>(4);
			List<Vec3> list2 = new List<Vec3>(4);
			List<Vec3> list3 = new List<Vec3>(4);
			List<Vec3> list4 = new List<Vec3>(4);
			Vec3 a2 = default(Vec3);
			Vec3 b2 = default(Vec3);
			int num = 0;
			for (int i = 0; i < 3; i++)
			{
				int num2 = (i + 1) % 3;
				if (array3[i] >= 0f)
				{
					list.Add(array[i]);
					list2.Add(array2[i]);
				}
				if (array3[i] <= 0f)
				{
					list3.Add(array[i]);
					list4.Add(array2[i]);
				}
				if ((array3[i] > 0f && array3[num2] < 0f) || (array3[i] < 0f && array3[num2] > 0f))
				{
					float num3 = array3[i] / (array3[i] - array3[num2]);
					Vec3 vec = array[i] + (array[num2] - array[i]) * num3;
					Vec3 item = (array2[i] + (array2[num2] - array2[i]) * num3).Normalized();
					list.Add(vec);
					list2.Add(item);
					list3.Add(vec);
					list4.Add(item);
					if (num == 0)
					{
						a2 = vec;
					}
					else
					{
						b2 = vec;
					}
					num++;
				}
			}
			for (int j = 1; j + 1 < list.Count; j++)
			{
				upper.Tri(list[0], list[j], list[j + 1], list2[0], list2[j], list2[j + 1], slot);
			}
			for (int k = 1; k + 1 < list3.Count; k++)
			{
				lower.Tri(list3[0], list3[k], list3[k + 1], list4[0], list4[k], list4[k + 1], slot);
			}
			if (num == 2)
			{
				upper.Edge(a2, b2);
				lower.Edge(a2, b2);
			}
		}

		private static Half Make(Piece piece, MeshModel from, int cutSlot, MeshInstance was, string name, Vec3 outward)
		{
			Vec3 vec = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
			Vec3 vec2 = new Vec3(float.MinValue, float.MinValue, float.MinValue);
			foreach (Vec3 po in piece.Pos)
			{
				vec = new Vec3(Mathf.Min(vec.X, po.X), Mathf.Min(vec.Y, po.Y), Mathf.Min(vec.Z, po.Z));
				vec2 = new Vec3(Mathf.Max(vec2.X, po.X), Mathf.Max(vec2.Y, po.Y), Mathf.Max(vec2.Z, po.Z));
			}
			Vec3 vec3 = (vec + vec2) * 0.5f;
			int count = piece.Pos.Count;
			MeshPart meshPart = new MeshPart
			{
				Name = "half",
				Parent = -1,
				Pivot = default(Vec3),
				Pos = new float[count * 3],
				Nrm = new float[count * 3],
				Tri = new int[count],
				Slot = piece.Slot.ToArray(),
				Axis = new byte[piece.Count],
				Version = 1
			};
			for (int i = 0; i < count; i++)
			{
				Vec3 vec4 = piece.Pos[i] - vec3;
				Vec3 vec5 = piece.Nrm[i];
				meshPart.Pos[i * 3] = vec4.X;
				meshPart.Pos[i * 3 + 1] = vec4.Y;
				meshPart.Pos[i * 3 + 2] = vec4.Z;
				meshPart.Nrm[i * 3] = vec5.X;
				meshPart.Nrm[i * 3 + 1] = vec5.Y;
				meshPart.Nrm[i * 3 + 2] = vec5.Z;
				meshPart.Tri[i] = i;
			}
			for (int j = 0; j < piece.Count; j++)
			{
				Vec3 vec6 = piece.Pos[j * 3];
				Vec3 vec7 = piece.Pos[j * 3 + 1];
				Vec3 obj = Vec3.Cross(b: piece.Pos[j * 3 + 2] - vec6, a: vec7 - vec6);
				float num = Mathf.Abs(obj.X);
				float num2 = Mathf.Abs(obj.Y);
				float num3 = Mathf.Abs(obj.Z);
				meshPart.Axis[j] = (byte)((!(num >= num2) || !(num >= num3)) ? ((num2 >= num3) ? 1u : 2u) : 0u);
			}
			MeshModel meshModel = from.Derive(from.Name + "/" + name, new MeshPart[1] { meshPart });
			int n = Mathf.Max(from.SlotColor.Length, cutSlot + 1);
			meshModel.SlotNames = Grow(from.SlotNames, n, "cut");
			meshModel.SlotKind = Grow(from.SlotKind, n, (byte)8);
			meshModel.SlotFlags = Grow(from.SlotFlags, n, (byte)0);
			meshModel.SlotColor = Grow(from.SlotColor, n, CutColor);
			meshModel.SlotColor[cutSlot] = CutColor;
			meshModel.SlotKind[cutSlot] = 8;
			meshModel.SlotFlags[cutSlot] = 0;
			meshModel.Min = vec - vec3;
			meshModel.Max = vec2 - vec3;
			MeshInstance meshInstance = new MeshInstance
			{
				Model = meshModel,
				Position = vec3,
				Basis = Mat3.Identity,
				Palette = ((was.Palette != null) ? ((uint[])was.Palette.Clone()) : null),
				Multiply = was.Multiply,
				NoShadow = was.NoShadow
			};
			if (meshInstance.Palette != null && cutSlot < meshInstance.Palette.Length)
			{
				meshInstance.Palette[cutSlot] = 0u;
			}
			return new Half
			{
				Mesh = meshInstance,
				Min = meshModel.Min,
				Max = meshModel.Max,
				CutLocal = piece.CutMiddle - vec3,
				CutNormal = outward,
				Bleeds = true
			};
		}

		private static T[] Grow<T>(T[] a, int n, T fill)
		{
			T[] array = new T[n];
			for (int i = 0; i < n; i++)
			{
				array[i] = ((a != null && i < a.Length) ? a[i] : fill);
			}
			return array;
		}

		private static void Add(Half h)
		{
			h.Space = RoomCombat.FallingIn;
			Halves.Add(h);
			while (Halves.Count > 36)
			{
				Halves.RemoveAt(0);
			}
		}

		private static void AddWeapon(MeshInstance weapon, Mark mark, float height, float metre, float ground)
		{
			MeshInstance meshInstance = new MeshInstance
			{
				Model = weapon.Model,
				Position = weapon.Position,
				Basis = weapon.Basis,
				Palette = ((weapon.Palette != null) ? ((uint[])weapon.Palette.Clone()) : null)
			};
			if (weapon.PartRotation != null)
			{
				meshInstance.PartRotation = (Mat3[])weapon.PartRotation.Clone();
				meshInstance.PartOffset = (Vec3[])weapon.PartOffset.Clone();
				meshInstance.PartScale = (float[])weapon.PartScale.Clone();
			}
			Add(new Half
			{
				Mesh = meshInstance,
				Min = weapon.Model.Min,
				Max = weapon.Model.Max,
				Ground = ground,
				Gravity = 9.8f * metre,
				Height = height,
				Vel = mark.Toward * (0.6f * height) + new Vec3(0f, 0f, 0.6f * height),
				Spin = mark.Across * 7f,
				Base = weapon.Basis
			});
		}

		private static void Spray(Vec3 at, Vec3 n, Mark mark, float height, float metre, int count)
		{
			FxParticles fxPool = SceneCollector.FxPool;
			if (fxPool != null)
			{
				Vec3 across = mark.Across;
				Vec3 vec = Vec3.Cross(n, across).Normalized();
				for (int i = 0; i < count; i++)
				{
					float f = (float)(Rng.NextDouble() * Math.PI * 2.0);
					float num = (float)Rng.NextDouble() * 0.12f * height;
					Vec3 vec2 = (across * Mathf.Cos(f) + vec * Mathf.Sin(f)) * num;
					Vec3 vec3 = ((across * Mathf.Cos(f) + vec * Mathf.Sin(f)) * 1.2f + n * (0.4f + (float)Rng.NextDouble())).Normalized();
					float num2 = (0.6f + (float)Rng.NextDouble() * 1.4f) * height;
					fxPool.Add(new FxParticles.P
					{
						Pos = at + vec2,
						Vel = vec3 * num2,
						Life = 0.5f + 0.5f * (float)Rng.NextDouble(),
						Size = 0.022f * height,
						EndSize = 0.016f * height,
						Color = TerrainCache.Rgb(170, 14, 20),
						EndColor = TerrainCache.Rgb(90, 6, 10),
						Gravity = 9.8f * metre,
						Drag = 0.5f,
						Kind = FxParticles.Kind.Drop,
						Matte = true,
						Bounce = false
					});
				}
			}
		}

		public static void Draw(List<MeshInstance> meshes, float dt, float eyeX, float eyeY, object space = null)
		{
			if (Halves.Count == 0)
			{
				return;
			}
			dt = Mathf.Min(dt, 0.05f);
			bool flag = _aged != Time.frameCount;
			_aged = Time.frameCount;
			for (int num = Halves.Count - 1; num >= 0; num--)
			{
				Half half = Halves[num];
				if (flag)
				{
					half.Age += dt;
				}
				if (half.Age > 9.5f)
				{
					Halves.RemoveAt(num);
				}
				else if (half.Space == space)
				{
					if (half.Topples)
					{
						Topple(half);
					}
					else
					{
						Tumble(half, dt);
					}
					float num2 = ((half.Age > 7.9f) ? ((half.Age - 7.9f) / 1.6f) : 0f);
					Vec3 position = half.Mesh.Position;
					if (num2 > 0f)
					{
						half.Mesh.Position = new Vec3(position.X, position.Y, position.Z - num2 * 0.6f * half.Height);
					}
					half.Mesh.Flash = 0u;
					half.Mesh.Static = false;
					half.Mesh.Tag = -1;
					float num3 = half.Mesh.Position.X - eyeX;
					float num4 = half.Mesh.Position.Y - eyeY;
					half.Mesh.SortDepth = Mathf.Sqrt(num3 * num3 + num4 * num4);
					int num5 = (int)(255f - 70f * Mathf.Clamp01(half.Age / 3f));
					half.Mesh.Multiply = TerrainCache.Rgb(num5, num5, num5);
					meshes.Add(half.Mesh);
					if (half.Age < 0.7f && half.Bleeds && Rng.NextDouble() < 0.6)
					{
						FxParticles fxPool = SceneCollector.FxPool;
						Vec3 pos = half.Mesh.Position + half.Mesh.Basis.Mul(half.CutLocal);
						Vec3 vec = half.Mesh.Basis.Mul(half.CutNormal);
						if (fxPool != null)
						{
							FxParticles.P p = new FxParticles.P
							{
								Pos = pos,
								Vel = (vec + new Vec3((float)Rng.NextDouble() - 0.5f, (float)Rng.NextDouble() - 0.5f, 0.3f)) * (0.5f * half.Height),
								Life = 0.5f,
								Size = 0.018f * half.Height,
								EndSize = 0.012f * half.Height,
								Color = TerrainCache.Rgb(160, 12, 18),
								EndColor = TerrainCache.Rgb(90, 6, 10),
								Gravity = half.Gravity,
								Drag = 0.5f,
								Kind = FxParticles.Kind.Drop,
								Matte = true
							};
							fxPool.Add(in p);
						}
					}
				}
			}
		}

		private static void Topple(Half h)
		{
			float num = h.Age - h.Delay;
			float num2 = 0f;
			if (num > 0f)
			{
				float num3 = Mathf.Clamp01(num / 0.6f);
				num2 = num3 * num3 * 88f;
				if (num > 0.6f)
				{
					float num4 = num - 0.6f;
					num2 = 88f - 7f * Mathf.Exp((0f - num4) * 6f) * Mathf.Abs(Mathf.Sin(num4 * 15f));
				}
			}
			Mat3 basis = Mat3.Rotation(h.Axis, num2 * (MathF.PI / 180f));
			h.Mesh.Basis = basis;
			h.Mesh.Position = h.Pivot + basis.Mul(h.Start - h.Pivot);
		}

		private static void Tumble(Half h, float dt)
		{
			h.Vel = new Vec3(h.Vel.X, h.Vel.Y, h.Vel.Z - h.Gravity * dt);
			Vec3 position = h.Mesh.Position;
			Vec3 position2 = position + h.Vel * dt;
			if (h.Space != null && !RoomCombat.Keeps(h.Space, new Vector2(position.X, position.Y), new Vector2(position2.X, position2.Y), h.Ground))
			{
				h.Vel = new Vec3(0f, 0f, h.Vel.Z);
				position2 = new Vec3(position.X, position.Y, position2.Z);
			}
			h.Mesh.Position = position2;
			float num = Mathf.Sqrt(Vec3.Dot(h.Spin, h.Spin));
			if (num > 0.0001f)
			{
				h.Turn = Mat3.Rotation(h.Spin * (1f / num), num * dt) * h.Turn;
			}
			if (h.Landed)
			{
				Vec3 vec = new Vec3(0f, 0f, -1f);
				Mat3 mat = h.Turn * h.Base;
				if (h.Rest < 0)
				{
					float num2 = Mathf.Max(h.Max.X - h.Min.X, Mathf.Max(h.Max.Y - h.Min.Y, h.Max.Z - h.Min.Z));
					float num3 = float.MaxValue;
					for (int i = 0; i < Axes.Length; i++)
					{
						float num4 = Vec3.Dot(mat.Mul(Axes[i]).Normalized(), vec);
						if (!(num4 < -0.1f))
						{
							float num5 = ((i < 2) ? (h.Max.X - h.Min.X) : ((i < 4) ? (h.Max.Y - h.Min.Y) : (h.Max.Z - h.Min.Z))) * (1.2f - 0.4f * num4) + 2f * num2 * Mathf.Abs(Vec3.Dot(Axes[i], h.CutNormal));
							if (num5 < num3)
							{
								num3 = num5;
								h.Rest = i;
							}
						}
					}
				}
				Vec3 a = ((h.Rest >= 0) ? mat.Mul(Axes[h.Rest]).Normalized() : vec);
				Vec3 vec2 = Vec3.Cross(a, vec);
				float num6 = Mathf.Sqrt(Vec3.Dot(vec2, vec2));
				if (num6 > 0.0001f)
				{
					h.Turn = Mat3.Rotation(vec2 * (1f / num6), Mathf.Min(Mathf.Atan2(num6, Vec3.Dot(a, vec)), 5f * dt)) * h.Turn;
				}
				float num7 = Mathf.Exp(-6f * dt);
				h.Vel = new Vec3(h.Vel.X * num7, h.Vel.Y * num7, h.Vel.Z);
				h.Spin *= Mathf.Exp(-7f * dt);
			}
			Mat3 basis = h.Turn * h.Base;
			h.Mesh.Basis = basis;
			float num8 = float.MaxValue;
			for (int j = 0; j < 8; j++)
			{
				Vec3 v = new Vec3(((j & 1) != 0) ? h.Max.X : h.Min.X, ((j & 2) != 0) ? h.Max.Y : h.Min.Y, ((j & 4) != 0) ? h.Max.Z : h.Min.Z);
				num8 = Mathf.Min(num8, basis.Mul(v).Z);
			}
			float num9 = h.Ground - num8;
			if (!(h.Mesh.Position.Z < num9))
			{
				return;
			}
			Vec3 position3 = h.Mesh.Position;
			h.Mesh.Position = new Vec3(position3.X, position3.Y, num9);
			if (h.Vel.Z <= 0f)
			{
				h.Vel = new Vec3(h.Vel.X * 0.55f, h.Vel.Y * 0.55f, h.Landed ? 0f : ((0f - h.Vel.Z) * 0.25f));
				if (!h.Landed)
				{
					h.Spin *= 0.5f;
				}
				h.Landed = true;
			}
		}
	}
}
