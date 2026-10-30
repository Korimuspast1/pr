using System;
using System.Collections.Generic;
using System.Diagnostics;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class BuildingFloors
	{
		private sealed class Placed
		{
			public Building Building;

			public MeshModel Model;

			public float X;

			public float Y;

			public float Z;

			public float Cos;

			public float Sin;

			public float Scale;

			public float Tol = 0.03f;

			public bool Mirror;

			public long Seen;
		}

		private static readonly List<Placed> All = new List<Placed>();

		private static readonly object Gate = new object();

		private static readonly Dictionary<MeshModel, ColumnField> Fields = new Dictionary<MeshModel, ColumnField>();

		private static readonly Dictionary<MeshModel, long> FieldUsed = new Dictionary<MeshModel, long>();

		private static readonly HashSet<MeshModel> Working = new HashSet<MeshModel>();

		private static readonly Stopwatch Clock = Stopwatch.StartNew();

		private const int KeepFields = 28;

		internal static int Built;

		internal static double BuiltMs;

		private static object _world;

		public static int Count => All.Count;

		public static void Register(Building b, MeshModel model, float x, float y, float z, float heading, float scale, bool mirror, float tol = 0.03f)
		{
			if (b == null || model == null)
			{
				return;
			}
			float f = heading + MathF.PI / 2f;
			long elapsedMilliseconds = Clock.ElapsedMilliseconds;
			lock (Gate)
			{
				Placed placed = null;
				for (int i = 0; i < All.Count; i++)
				{
					if (All[i].Building == b)
					{
						placed = All[i];
						break;
					}
				}
				if (placed == null)
				{
					placed = new Placed
					{
						Building = b
					};
					All.Add(placed);
				}
				placed.Model = model;
				placed.X = x;
				placed.Y = y;
				placed.Z = z;
				placed.Cos = Mathf.Cos(f);
				placed.Sin = Mathf.Sin(f);
				placed.Scale = Mathf.Max(0.0001f, scale);
				placed.Mirror = mirror;
				placed.Tol = tol;
				placed.Seen = elapsedMilliseconds;
				FieldUsed[model] = elapsedMilliseconds;
				if (Fields.ContainsKey(model) || !Working.Add(model))
				{
					return;
				}
			}
			SlowLane.Run(delegate
			{
				BuildField(model);
			});
		}

		private static void BuildField(MeshModel model)
		{
			ColumnField value = null;
			Stopwatch stopwatch = Stopwatch.StartNew();
			try
			{
				float num = Math.Max(model.Max.X - model.Min.X, model.Max.Y - model.Min.Y);
				value = ColumnField.OfModel(model, Math.Max(0.1f, num / 220f));
			}
			catch (Exception ex)
			{
				Log.Info("floors: " + model.Name + ": " + ex.Message);
			}
			lock (Gate)
			{
				Working.Remove(model);
				Fields[model] = value;
				Built++;
				BuiltMs += stopwatch.Elapsed.TotalMilliseconds;
			}
		}

		public static void Upkeep()
		{
			object obj = null;
			try
			{
				obj = ((World.world != null) ? World.world.map_stats : null);
			}
			catch
			{
			}
			long elapsedMilliseconds = Clock.ElapsedMilliseconds;
			lock (Gate)
			{
				if (obj != _world)
				{
					_world = obj;
					All.Clear();
				}
				for (int num = All.Count - 1; num >= 0; num--)
				{
					if (elapsedMilliseconds - All[num].Seen > 1500)
					{
						All.RemoveAt(num);
					}
				}
				if (Fields.Count <= 28)
				{
					return;
				}
				List<KeyValuePair<MeshModel, long>> list = new List<KeyValuePair<MeshModel, long>>(FieldUsed);
				list.Sort((KeyValuePair<MeshModel, long> p, KeyValuePair<MeshModel, long> q) => p.Value.CompareTo(q.Value));
				for (int num2 = 0; num2 < list.Count; num2++)
				{
					if (Fields.Count <= 21)
					{
						break;
					}
					if (elapsedMilliseconds - list[num2].Value < 5000)
					{
						break;
					}
					Fields.Remove(list[num2].Key);
					FieldUsed.Remove(list[num2].Key);
				}
			}
		}

		public static bool StandOn(float x, float y, float r, float maxZ, out float z, out byte what)
		{
			return StandOn(x, y, r, maxZ, float.NaN, out z, out what);
		}

		public static bool StandOn(float x, float y, float r, float maxZ, float keep, out float z, out byte what)
		{
			z = float.NegativeInfinity;
			what = 0;
			if (All.Count == 0)
			{
				return false;
			}
			bool flag = false;
			lock (Gate)
			{
				for (int i = 0; i < All.Count; i++)
				{
					Placed placed = All[i];
					if (!Fields.TryGetValue(placed.Model, out var value) || value == null)
					{
						continue;
					}
					float num = x - placed.X;
					float num2 = y - placed.Y;
					float num3 = (value.Reach + value.Cell * 2f) * placed.Scale + r;
					if (num * num + num2 * num2 > num3 * num3)
					{
						continue;
					}
					float num4 = (num * placed.Cos + num2 * placed.Sin) / placed.Scale;
					float y2 = ((0f - num) * placed.Sin + num2 * placed.Cos) / placed.Scale;
					if (placed.Mirror)
					{
						num4 = 0f - num4;
					}
					float maxZ2 = (maxZ - placed.Z) / placed.Scale;
					if (value.Support(num4, y2, r / placed.Scale, maxZ2, placed.Tol / placed.Scale, float.IsNaN(keep) ? float.NaN : ((keep - placed.Z) / placed.Scale), out var z2, out var what2))
					{
						float num5 = placed.Z + z2 * placed.Scale;
						if (!flag || !(num5 <= z))
						{
							z = num5;
							what = what2;
							flag = true;
						}
					}
				}
				return flag;
			}
		}

		public static bool Blocked(float x, float y, float r, float z0, float z1)
		{
			if (All.Count == 0)
			{
				return false;
			}
			lock (Gate)
			{
				for (int i = 0; i < All.Count; i++)
				{
					Placed placed = All[i];
					if (!Fields.TryGetValue(placed.Model, out var value) || value == null)
					{
						continue;
					}
					float num = x - placed.X;
					float num2 = y - placed.Y;
					float num3 = (value.Reach + value.Cell * 2f) * placed.Scale + r;
					if (!(num * num + num2 * num2 > num3 * num3))
					{
						float num4 = (num * placed.Cos + num2 * placed.Sin) / placed.Scale;
						float y2 = ((0f - num) * placed.Sin + num2 * placed.Cos) / placed.Scale;
						if (placed.Mirror)
						{
							num4 = 0f - num4;
						}
						float num5 = (z0 - placed.Z) / placed.Scale;
						float num6 = Math.Min((z1 - placed.Z) / placed.Scale, placed.Model.Max.Z);
						if (num5 < num6 && value.Blocked(num4, y2, r / placed.Scale, num5, num6))
						{
							return true;
						}
					}
				}
			}
			return false;
		}

		internal static bool ReadyFor(Building b)
		{
			lock (Gate)
			{
				foreach (Placed item in All)
				{
					ColumnField value;
					if (item.Building == b)
					{
						return Fields.TryGetValue(item.Model, out value) && value != null;
					}
				}
			}
			return false;
		}

		internal static bool WorldOf(Building b, float mx, float my, out Vector2 at, out float baseZ, out float scale)
		{
			at = Vector2.zero;
			baseZ = 0f;
			scale = 1f;
			lock (Gate)
			{
				foreach (Placed item in All)
				{
					if (item.Building == b)
					{
						if (item.Mirror)
						{
							mx = 0f - mx;
						}
						at = new Vector2(item.X + (item.Cos * mx - item.Sin * my) * item.Scale, item.Y + (item.Sin * mx + item.Cos * my) * item.Scale);
						baseZ = item.Z;
						scale = item.Scale;
						return true;
					}
				}
			}
			return false;
		}

		internal static bool DoorClimbForTest(Building b, float r, float step, out float up, out Vector2 from, out Vector2 door)
		{
			up = 0f;
			from = (door = Vector2.zero);
			Placed placed = null;
			ColumnField value;
			lock (Gate)
			{
				foreach (Placed item in All)
				{
					if (item.Building == b)
					{
						placed = item;
						break;
					}
				}
				if (placed == null || !Fields.TryGetValue(placed.Model, out value) || value == null)
				{
					return false;
				}
			}
			if (!Steering.DoorOf(placed.Model, out var at))
			{
				return false;
			}
			float scale = placed.Scale;
			float num = 0f;
			float num2 = placed.Model.Min.Y - 1.5f;
			for (float num3 = num2; num3 < at.y - 0.25f / scale; num3 += 0.03f)
			{
				num = ((!value.Support(at.x, num3, r / scale, num + step / scale, 0.03f / scale, out var z, out var _)) ? 0f : Math.Max(0f, z));
				up = Math.Max(up, num * scale);
			}
			WorldOf(b, at.x, num2, out from, out var baseZ, out var scale2);
			WorldOf(b, at.x, at.y, out door, out scale2, out baseZ);
			return true;
		}

		internal static string DescribeForTest(Building b, Vector2 q)
		{
			lock (Gate)
			{
				foreach (Placed item in All)
				{
					if (item.Building == b)
					{
						float num = q.x - item.X;
						float num2 = q.y - item.Y;
						float num3 = (num * item.Cos + num2 * item.Sin) / item.Scale;
						float num4 = ((0f - num) * item.Sin + num2 * item.Cos) / item.Scale;
						if (item.Mirror)
						{
							num3 = 0f - num3;
						}
						Fields.TryGetValue(item.Model, out var value);
						return string.Format("{0} at {1:F2},{2:F2},{3:F2} scale {4:F3} mirror {5} cos {6:F2} sin {7:F2}; point on its model {8:F2},{9:F2}; field {10}", item.Model.Name, item.X, item.Y, item.Z, item.Scale, item.Mirror, item.Cos, item.Sin, num3, num4, (value != null) ? $"reach {value.Reach:F2} cell {value.Cell:F2}" : "none");
					}
				}
			}
			return "not registered";
		}

		internal static MeshModel ModelOf(Building b)
		{
			lock (Gate)
			{
				foreach (Placed item in All)
				{
					if (item.Building == b)
					{
						return item.Model;
					}
				}
			}
			return null;
		}
	}
}
