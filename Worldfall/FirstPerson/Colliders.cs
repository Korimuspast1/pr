using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class Colliders
	{
		private struct FootprintKey : IEquatable<FootprintKey>
		{
			public MeshModel Model;

			public int Lo;

			public int Hi;

			public int Grow;

			public bool Equals(FootprintKey o)
			{
				if (Model == o.Model && Lo == o.Lo && Hi == o.Hi)
				{
					return Grow == o.Grow;
				}
				return false;
			}

			public override bool Equals(object obj)
			{
				if (obj is FootprintKey o)
				{
					return Equals(o);
				}
				return false;
			}

			public override int GetHashCode()
			{
				return (RuntimeHelpers.GetHashCode(Model) * 397) ^ (Lo * 31) ^ Hi ^ (Grow << 20);
			}
		}

		public static readonly List<Collider> Near = new List<Collider>(256);

		internal static readonly List<Collider> ExtraForTest = new List<Collider>();

		public const float BodyRadius = 0.16f;

		public static float GapRadius = 0.16f;

		private static bool _hasBody;

		private static float _feet;

		private static float _lift;

		private static float _body = 1f;

		private static float _eye = 0.9f;

		private const float StepShare = 0.2f;

		private static readonly float[] JumpLifts = new float[3] { 0.3f, 0.55f, 0.8f };

		private static float[] _floor = new float[256];

		private const float SlideDeadZone = 0.2f;

		private static readonly Dictionary<FootprintKey, Footprint> Footprints = new Dictionary<FootprintKey, Footprint>();

		private const int KeepFootprints = 320;

		private const int MaxAside = 3;

		private static readonly HashSet<FootprintKey> Aside = new HashSet<FootprintKey>();

		private static readonly HashSet<FootprintKey> Failed = new HashSet<FootprintKey>();

		private static readonly Queue<KeyValuePair<FootprintKey, Footprint>> Done = new Queue<KeyValuePair<FootprintKey, Footprint>>();

		private static readonly Stopwatch BuildClock = new Stopwatch();

		private static int _frame;

		private static double _builtMs;

		internal static int BuiltCount;

		internal static int BuiltAside;

		internal static double BuiltMs;

		internal static double WorstFrameMs;

		internal static double BuiltAsideMs;

		private static readonly List<KeyValuePair<FootprintKey, Footprint>> _byAge = new List<KeyValuePair<FootprintKey, Footprint>>();

		internal static string BodyForTest
		{
			get
			{
				if (!_hasBody)
				{
					return "no body";
				}
				return $"feet {_feet:F2} lift {_lift:F2} body {_body:F2} eye {_eye:F2}";
			}
		}

		public static float BaseRadius(SpriteImage img, float worldWidth, float share)
		{
			int num = Mathf.Max(1, Mathf.RoundToInt((float)(img.OpaqueTop - img.OpaqueBottom) * share));
			int num2 = 0;
			for (int i = img.OpaqueBottom; i < img.OpaqueBottom + num && i < img.Height; i++)
			{
				int num3 = -1;
				int num4 = -1;
				for (int j = 0; j < img.Width; j++)
				{
					if (img.Pixels[i * img.Width + j] >> 24 >= 128)
					{
						if (num3 < 0)
						{
							num3 = j;
						}
						num4 = j;
					}
				}
				if (num3 >= 0)
				{
					num2 = Mathf.Max(num2, num4 - num3 + 1);
				}
			}
			return (float)num2 * worldWidth / (float)Mathf.Max(1, img.Width) * 0.5f;
		}

		public static void SetBody(float ground, float lift, float height, float eye)
		{
			_hasBody = true;
			_feet = ground;
			_lift = Mathf.Max(0f, lift);
			_body = Mathf.Clamp(height, 0.2f, 8f);
			_eye = Mathf.Max(0f, eye);
		}

		internal static string NearestForTest(Vector2 p)
		{
			float num = 1000f;
			foreach (Collider item in Near)
			{
				if (!item.Mobile)
				{
					Vector2 normal;
					float num2 = Distance(item, p, out normal);
					if (num2 < num)
					{
						num = num2;
					}
				}
			}
			return num.ToString("F2");
		}

		public static void ForgetBody()
		{
			_hasBody = false;
		}

		public static void WalkerBand(float scale, out float lo, out float hi)
		{
			Band(0.05f, 0f, 1f, 0.9f, scale, out lo, out hi);
		}

		public static void YourBand(float baseZ, float scale, out float lo, out float hi)
		{
			if (!_hasBody)
			{
				WalkerBand(scale, out lo, out hi);
			}
			else
			{
				Band(_feet - baseZ, _lift, _body, _eye, scale, out lo, out hi, yours: true);
			}
		}

		public static void PrefetchYours(MeshModel model, float baseZ, float scale)
		{
			if (!_hasBody || model == null)
			{
				return;
			}
			float close = CloseOf(scale);
			for (int i = 1; i <= 7; i++)
			{
				if (Aside.Count >= 2)
				{
					break;
				}
				float num = ((i <= 4) ? (_feet + (float)i * 0.05f) : _feet);
				float lift = ((i <= 4) ? _lift : JumpLifts[i - 5]);
				Band(num - baseZ, lift, _body, _eye, scale, out var lo, out var hi, yours: true);
				FootprintOf(model, lo, hi, close, aside: true);
			}
		}

		private static void Band(float feet, float lift, float body, float eye, float scale, out float lo, out float hi, bool yours = false)
		{
			feet = (yours ? (Mathf.Ceil((feet - 0.05f) * 20f - 0.02f) / 20f + 0.05f) : (Mathf.Round((feet - 0.05f) * 10f) / 10f + 0.05f));
			float num = Mathf.Floor(lift * 4f) / 4f;
			lift = Mathf.Ceil(lift * 4f - 0.2f) / 4f;
			float num2 = (yours ? RoomFloor.StepOf(body) : (Mathf.Round(body * 0.2f * 20f) / 20f));
			float num3 = Mathf.Round(Mathf.Max(body * 1.04f, eye + 0.1f) * 20f) / 20f;
			scale = Mathf.Max(0.0001f, scale);
			lo = (feet + num2 + num) / scale;
			hi = (feet + num3 + lift) / scale;
		}

		public static Collider FromModel(MeshModel model, float x, float y, float heading, float scale, float bandLo, float bandHi, bool mirror = false)
		{
			return Place(FootprintOf(model, bandLo, bandHi, CloseOf(scale)), x, y, heading, scale, mirror);
		}

		public static bool TryFromModel(MeshModel model, float x, float y, float heading, float scale, float bandLo, float bandHi, bool mirror, out Collider c)
		{
			Footprint footprint = FootprintOf(model, bandLo, bandHi, CloseOf(scale), aside: true);
			c = ((footprint == null) ? default(Collider) : Place(footprint, x, y, heading, scale, mirror));
			return footprint != null;
		}

		public static Collider Place(Footprint shape, float x, float y, float heading, float scale, bool mirror)
		{
			float f = heading + MathF.PI / 2f;
			return new Collider
			{
				X = x,
				Y = y,
				Shape = shape,
				Cos = Mathf.Cos(f),
				Sin = Mathf.Sin(f),
				Scale = Mathf.Max(0.0001f, scale),
				Mirror = mirror
			};
		}

		internal static Collider WithBand(Collider c, MeshModel model, float bandLo, float bandHi)
		{
			c.Shape = FootprintOf(model, bandLo, bandHi, CloseOf(c.Scale));
			return c;
		}

		private static float CloseOf(float scale)
		{
			return Mathf.Max(0.22f, Mathf.Round(GapRadius / 0.04f) * 0.04f / Mathf.Max(0.0001f, scale));
		}

		public static Vector2 Slide(Vector2 from, Vector2 move, float radius, float probe, float travel = -1f, bool overCreatures = false)
		{
			float magnitude = move.magnitude;
			if (magnitude < 0.0001f)
			{
				return Vector2.zero;
			}
			Vector2 vector = move / magnitude;
			int count = Near.Count;
			if (_floor.Length < count)
			{
				_floor = new float[count * 2];
			}
			for (int i = 0; i < count; i++)
			{
				_floor[i] = ((overCreatures && Near[i].Mobile) ? float.NegativeInfinity : Mathf.Min(radius, Distance(Near[i], from, out var _)));
			}
			int num = Mathf.Max(1, (int)Math.Ceiling(probe / (radius * 0.5f)));
			float b = probe;
			Vector2 vector2 = from + vector * probe;
			bool flag = false;
			for (int j = 1; j <= num; j++)
			{
				float num2 = probe * (float)j / (float)num;
				Vector2 vector3 = from + vector * num2;
				if (TooClose(vector3, count))
				{
					b = num2;
					vector2 = vector3;
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				return move;
			}
			Vector2 vector4 = Vector2.zero;
			for (int k = 0; k < 6; k++)
			{
				bool flag2 = false;
				for (int l = 0; l < count; l++)
				{
					Vector2 normal2;
					float num3 = Distance(Near[l], vector2, out normal2);
					if (num3 >= _floor[l])
					{
						continue;
					}
					if (Vector2.Dot(normal2, vector) < 0f)
					{
						if (vector4 == Vector2.zero)
						{
							vector4 = normal2;
						}
						else if (Vector2.Dot(vector4, normal2) < 0.7f)
						{
							return Vector2.zero;
						}
					}
					vector2 += normal2 * (_floor[l] - num3);
					flag2 = true;
				}
				if (!flag2)
				{
					break;
				}
			}
			if (TooClose(vector2, count, radius * 0.05f))
			{
				return Vector2.zero;
			}
			Vector2 vector5 = vector2 - from;
			float num4 = Vector2.Dot(vector5, vector);
			if (num4 < 0f)
			{
				vector5 -= vector * num4;
			}
			Vector2 vector6 = vector5 * (magnitude / Mathf.Max(0.0001f, b));
			float num5 = vector6.magnitude;
			if (num5 > magnitude)
			{
				vector6 *= magnitude / num5;
				num5 = magnitude;
			}
			Vector2 vector7 = vector6 * (((travel > 0f) ? travel : magnitude) / magnitude);
			int num6 = Mathf.Max(1, (int)Math.Ceiling(vector7.magnitude / (radius * 0.25f)));
			for (int m = 1; m <= num6; m++)
			{
				if (TooClose(from + vector7 * ((float)m / (float)num6), count, radius * 0.05f))
				{
					float num7 = ((float)m - 1f) / (float)num6;
					vector6 *= num7;
					num5 *= num7;
					break;
				}
			}
			if (!(num5 < magnitude * 0.2f))
			{
				return vector6;
			}
			return Vector2.zero;
		}

		private static bool TooClose(Vector2 q, int count, float slack = 0f)
		{
			for (int i = 0; i < count; i++)
			{
				if (Distance(Near[i], q, out var _) < _floor[i] - slack)
				{
					return true;
				}
			}
			return false;
		}

		public static Vector2 PushOut(Vector2 p, float radius)
		{
			for (int i = 0; i < 4; i++)
			{
				bool flag = false;
				for (int j = 0; j < Near.Count; j++)
				{
					if (!Near[j].Mobile)
					{
						Vector2 normal;
						float num = Distance(Near[j], p, out normal);
						if (!(num >= radius))
						{
							p += normal * (radius - num + 0.002f);
							flag = true;
						}
					}
				}
				if (!flag)
				{
					break;
				}
			}
			return p;
		}

		public static bool ClearOf(Vector2 p, float radius)
		{
			for (int i = 0; i < Near.Count; i++)
			{
				if (!Near[i].Mobile && Distance(Near[i], p, out var _) < radius)
				{
					return false;
				}
			}
			return true;
		}

		internal static string DescribeNear(Vector2 p, int most)
		{
			List<(float, string)> list = new List<(float, string)>();
			foreach (Collider item in Near)
			{
				Vector2 normal;
				float num = Distance(item, p, out normal);
				if (!(num > 1.5f))
				{
					string text = ((item.Owner is Building building && building.asset != null) ? (building.asset.id + (building.isRuin() ? " (ruin)" : "")) : (item.Mobile ? "a creature" : ((item.Shape != null) ? "a model" : (item.Box ? "a box" : "a circle"))));
					list.Add((num, string.Format("{0} {1:F2} off, out {2:F2},{3:F2}{4}", text, num, normal.x, normal.y, (item.Shape != null) ? $" (outline {item.Shape.W}x{item.Shape.H})" : "")));
				}
			}
			list.Sort(((float d, string what) a, (float d, string what) b) => a.d.CompareTo(b.d));
			if (list.Count > most)
			{
				list.RemoveRange(most, list.Count - most);
			}
			if (list.Count != 0)
			{
				return string.Join("; ", list.ConvertAll(((float d, string what) f) => f.what));
			}
			return "nothing";
		}

		public static bool Clear(Vector2 p)
		{
			for (int i = 0; i < Near.Count; i++)
			{
				if (!Near[i].Mobile && Distance(Near[i], p, out var _) < 0.06f)
				{
					return false;
				}
			}
			return true;
		}

		public static Vector2 OutOfCreatures(Vector2 p, float radius)
		{
			Vector2 zero = Vector2.zero;
			int num = 0;
			for (int i = 0; i < Near.Count; i++)
			{
				if (Near[i].Mobile)
				{
					Vector2 normal;
					float num2 = Distance(Near[i], p, out normal);
					if (!(num2 >= radius))
					{
						zero += normal * (radius - num2 + 0.002f);
						num++;
					}
				}
			}
			if (num <= 1 || !(zero.sqrMagnitude < 0.0004f))
			{
				return zero;
			}
			return Vector2.zero;
		}

		internal static float Distance(Collider c, Vector2 q, out Vector2 normal)
		{
			if (c.Shape != null)
			{
				float num = q.x - c.X;
				float num2 = q.y - c.Y;
				float num3 = c.Shape.Reach * c.Scale + 1f;
				if (num * num + num2 * num2 > num3 * num3)
				{
					normal = Vector2.up;
					return 1000f;
				}
				float num4 = (num * c.Cos + num2 * c.Sin) / c.Scale;
				float y = ((0f - num) * c.Sin + num2 * c.Cos) / c.Scale;
				if (c.Mirror)
				{
					num4 = 0f - num4;
				}
				float gx;
				float gy;
				float num5 = c.Shape.Sample(num4, y, out gx, out gy);
				if (c.Mirror)
				{
					gx = 0f - gx;
				}
				normal = new Vector2(gx * c.Cos - gy * c.Sin, gx * c.Sin + gy * c.Cos);
				return num5 * c.Scale;
			}
			if (c.Box)
			{
				float x = Mathf.Clamp(q.x, c.X - c.HalfW, c.X + c.HalfW);
				float y2 = Mathf.Clamp(q.y, c.Y - c.HalfD, c.Y + c.HalfD);
				Vector2 vector = q - new Vector2(x, y2);
				float magnitude = vector.magnitude;
				if (magnitude > 0.0001f)
				{
					normal = vector / magnitude;
					return magnitude;
				}
				float num6 = q.x - (c.X - c.HalfW);
				float num7 = c.X + c.HalfW - q.x;
				float num8 = q.y - (c.Y - c.HalfD);
				float b = c.Y + c.HalfD - q.y;
				float num9 = Mathf.Min(Mathf.Min(num6, num7), Mathf.Min(num8, b));
				normal = ((num9 == num6) ? Vector2.left : ((num9 == num7) ? Vector2.right : ((num9 == num8) ? Vector2.down : Vector2.up)));
				return 0f - num9;
			}
			Vector2 vector2 = q - new Vector2(c.X, c.Y);
			float magnitude2 = vector2.magnitude;
			normal = ((magnitude2 > 0.0001f) ? (vector2 / magnitude2) : Vector2.up);
			return magnitude2 - c.Radius;
		}

		internal static float DistanceIn(in Collider c, Vector2 q, out Vector2 normal)
		{
			if (c.Shape != null)
			{
				float num = q.x - c.X;
				float num2 = q.y - c.Y;
				float num3 = c.Shape.Reach * c.Scale + 1f;
				if (num * num + num2 * num2 > num3 * num3)
				{
					normal = Vector2.up;
					return 1000f;
				}
				float num4 = (num * c.Cos + num2 * c.Sin) / c.Scale;
				float y = ((0f - num) * c.Sin + num2 * c.Cos) / c.Scale;
				if (c.Mirror)
				{
					num4 = 0f - num4;
				}
				float gx;
				float gy;
				float num5 = c.Shape.Sample(num4, y, out gx, out gy);
				if (c.Mirror)
				{
					gx = 0f - gx;
				}
				normal = new Vector2(gx * c.Cos - gy * c.Sin, gx * c.Sin + gy * c.Cos);
				return num5 * c.Scale;
			}
			if (c.Box)
			{
				float x = Mathf.Clamp(q.x, c.X - c.HalfW, c.X + c.HalfW);
				float y2 = Mathf.Clamp(q.y, c.Y - c.HalfD, c.Y + c.HalfD);
				Vector2 vector = q - new Vector2(x, y2);
				float magnitude = vector.magnitude;
				if (magnitude > 0.0001f)
				{
					normal = vector / magnitude;
					return magnitude;
				}
				float num6 = q.x - (c.X - c.HalfW);
				float num7 = c.X + c.HalfW - q.x;
				float num8 = q.y - (c.Y - c.HalfD);
				float b = c.Y + c.HalfD - q.y;
				float num9 = Mathf.Min(Mathf.Min(num6, num7), Mathf.Min(num8, b));
				normal = ((num9 == num6) ? Vector2.left : ((num9 == num7) ? Vector2.right : ((num9 == num8) ? Vector2.down : Vector2.up)));
				return 0f - num9;
			}
			Vector2 vector2 = q - new Vector2(c.X, c.Y);
			float magnitude2 = vector2.magnitude;
			normal = ((magnitude2 > 0.0001f) ? (vector2 / magnitude2) : Vector2.up);
			return magnitude2 - c.Radius;
		}

		internal static float DistanceTo(in Collider c, Vector2 q)
		{
			if (c.Shape != null)
			{
				float num = q.x - c.X;
				float num2 = q.y - c.Y;
				float num3 = c.Shape.Reach * c.Scale + 1f;
				if (num * num + num2 * num2 > num3 * num3)
				{
					return 1000f;
				}
				float num4 = (num * c.Cos + num2 * c.Sin) / c.Scale;
				float y = ((0f - num) * c.Sin + num2 * c.Cos) / c.Scale;
				if (c.Mirror)
				{
					num4 = 0f - num4;
				}
				return c.Shape.SampleDistance(num4, y) * c.Scale;
			}
			if (c.Box)
			{
				float x = Mathf.Clamp(q.x, c.X - c.HalfW, c.X + c.HalfW);
				float y2 = Mathf.Clamp(q.y, c.Y - c.HalfD, c.Y + c.HalfD);
				float magnitude = (q - new Vector2(x, y2)).magnitude;
				if (magnitude > 0.0001f)
				{
					return magnitude;
				}
				float a = q.x - (c.X - c.HalfW);
				float b = c.X + c.HalfW - q.x;
				float a2 = q.y - (c.Y - c.HalfD);
				return 0f - Mathf.Min(b: Mathf.Min(a2, c.Y + c.HalfD - q.y), a: Mathf.Min(a, b));
			}
			return (q - new Vector2(c.X, c.Y)).magnitude - c.Radius;
		}

		public static void BeginFrame()
		{
			_frame++;
			WorstFrameMs = Math.Max(WorstFrameMs, _builtMs);
			_builtMs = 0.0;
			lock (Done)
			{
				while (Done.Count > 0)
				{
					KeyValuePair<FootprintKey, Footprint> keyValuePair = Done.Dequeue();
					Aside.Remove(keyValuePair.Key);
					if (keyValuePair.Value == null)
					{
						Failed.Add(keyValuePair.Key);
						continue;
					}
					keyValuePair.Value.Used = _frame;
					Footprints[keyValuePair.Key] = keyValuePair.Value;
				}
			}
			if (Footprints.Count > 320)
			{
				Forget();
			}
		}

		public static Footprint FootprintOf(MeshModel model, float bandLo, float bandHi, float close = 0.22f, bool aside = false)
		{
			float cell = CellOf(model);
			int grow = Math.Max(1, (int)Math.Ceiling(close / cell));
			FootprintKey key = new FootprintKey
			{
				Model = model,
				Lo = Mathf.RoundToInt(bandLo * 100f),
				Hi = Mathf.RoundToInt(bandHi * 100f),
				Grow = grow
			};
			if (Footprints.TryGetValue(key, out var value))
			{
				value.Used = _frame;
				return value;
			}
			if (aside)
			{
				if (Aside.Count < 3 && !Failed.Contains(key) && Aside.Add(key))
				{
					ThreadPool.QueueUserWorkItem(delegate
					{
						BuildAside(key, model, bandLo, bandHi, cell, grow);
					});
				}
				return null;
			}
			BuildClock.Restart();
			value = BuildFootprint(model, bandLo, bandHi, cell, grow);
			double totalMilliseconds = BuildClock.Elapsed.TotalMilliseconds;
			_builtMs += totalMilliseconds;
			BuiltCount++;
			BuiltMs += totalMilliseconds;
			value.Used = _frame;
			Footprints[key] = value;
			return value;
		}

		private static void BuildAside(FootprintKey key, MeshModel model, float bandLo, float bandHi, float cell, int grow)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			Footprint value = null;
			try
			{
				value = BuildFootprint(model, bandLo, bandHi, cell, grow);
			}
			catch
			{
			}
			lock (Done)
			{
				Done.Enqueue(new KeyValuePair<FootprintKey, Footprint>(key, value));
				BuiltAside++;
				BuiltAsideMs += stopwatch.Elapsed.TotalMilliseconds;
			}
		}

		private static void Forget()
		{
			_byAge.Clear();
			_byAge.AddRange(Footprints);
			_byAge.Sort((KeyValuePair<FootprintKey, Footprint> a, KeyValuePair<FootprintKey, Footprint> b) => a.Value.Used.CompareTo(b.Value.Used));
			int num = Footprints.Count - 240;
			for (int num2 = 0; num2 < num && num2 < _byAge.Count; num2++)
			{
				Footprints.Remove(_byAge[num2].Key);
			}
			_byAge.Clear();
		}

		private static float CellOf(MeshModel model)
		{
			return Math.Max(0.05f, Math.Max(model.Max.X - model.Min.X, model.Max.Y - model.Min.Y) / 110f);
		}

		private static Footprint BuildFootprint(MeshModel model, float bandLo, float bandHi, float cell, int grow)
		{
			float x = model.Min.X;
			float x2 = model.Max.X;
			float y = model.Min.Y;
			float y2 = model.Max.Y;
			int num = grow + 4;
			int num2 = (int)Math.Ceiling((x2 - x) / cell) + num * 2;
			int num3 = (int)Math.Ceiling((y2 - y) / cell) + num * 2;
			Footprint footprint = new Footprint
			{
				X0 = x - (float)num * cell,
				Y0 = y - (float)num * cell,
				Cell = cell,
				W = num2,
				H = num3
			};
			bool[] array = new bool[num2 * num3];
			Vec3[] array2 = new Vec3[8];
			Vec3[] array3 = new Vec3[8];
			MeshPart[] parts = model.Parts;
			foreach (MeshPart meshPart in parts)
			{
				string text = meshPart.Name ?? "";
				if (text.StartsWith("sail", StringComparison.Ordinal) || text.StartsWith("flag", StringComparison.Ordinal))
				{
					continue;
				}
				float[] pos = meshPart.Pos;
				int[] tri = meshPart.Tri;
				for (int j = 0; j < meshPart.TriangleCount; j++)
				{
					int num4 = tri[j * 3] * 3;
					int num5 = tri[j * 3 + 1] * 3;
					int num6 = tri[j * 3 + 2] * 3;
					array2[0] = new Vec3(pos[num4], pos[num4 + 1], pos[num4 + 2]);
					array2[1] = new Vec3(pos[num5], pos[num5 + 1], pos[num5 + 2]);
					array2[2] = new Vec3(pos[num6], pos[num6 + 1], pos[num6 + 2]);
					float num7 = Math.Min(array2[0].Z, Math.Min(array2[1].Z, array2[2].Z));
					if (!(Math.Max(array2[0].Z, Math.Max(array2[1].Z, array2[2].Z)) < bandLo) && !(num7 > bandHi))
					{
						int n = ClipZ(array2, 3, array3, bandLo, keepAbove: true);
						n = ClipZ(array3, n, array2, bandHi, keepAbove: false);
						if (n >= 2)
						{
							FillPolygon(footprint, array, array2, n);
						}
					}
				}
			}
			float[] array4 = Chamfer(array, num2, num3);
			bool[] array5 = new bool[num2 * num3];
			Stack<int> stack = new Stack<int>();
			for (int k = 0; k < num2; k++)
			{
				stack.Push(k);
				stack.Push((num3 - 1) * num2 + k);
			}
			for (int l = 0; l < num3; l++)
			{
				stack.Push(l * num2);
				stack.Push(l * num2 + num2 - 1);
			}
			while (stack.Count > 0)
			{
				int num8 = stack.Pop();
				if (!array5[num8] && !(array4[num8] <= (float)grow))
				{
					array5[num8] = true;
					int num9 = num8 % num2;
					int num10 = num8 / num2;
					if (num9 > 0)
					{
						stack.Push(num8 - 1);
					}
					if (num9 < num2 - 1)
					{
						stack.Push(num8 + 1);
					}
					if (num10 > 0)
					{
						stack.Push(num8 - num2);
					}
					if (num10 < num3 - 1)
					{
						stack.Push(num8 + num2);
					}
				}
			}
			float[] array6 = Chamfer(array5, num2, num3);
			bool[] array7 = new bool[num2 * num3];
			for (int m = 0; m < array7.Length; m++)
			{
				array7[m] = array[m] || (!array5[m] && array6[m] > (float)grow);
			}
			DoorCorridor(model, footprint, array7);
			float[] array8 = Distances(array7, num2, num3);
			bool[] array9 = new bool[num2 * num3];
			for (int num11 = 0; num11 < array9.Length; num11++)
			{
				array9[num11] = !array7[num11];
			}
			float[] array10 = Distances(array9, num2, num3);
			footprint.Dist = new float[num2 * num3];
			float num12 = 0f;
			for (int num13 = 0; num13 < footprint.Dist.Length; num13++)
			{
				if (array7[num13])
				{
					footprint.Dist[num13] = (0f - (array10[num13] - 0.5f)) * cell;
					float num14 = footprint.X0 + ((float)(num13 % num2) + 0.5f) * cell;
					float num15 = footprint.Y0 + ((float)(num13 / num2) + 0.5f) * cell;
					num12 = Math.Max(num12, (float)Math.Sqrt(num14 * num14 + num15 * num15));
				}
				else
				{
					footprint.Dist[num13] = (array8[num13] - 0.5f) * cell;
				}
			}
			footprint.Reach = num12 + cell;
			return footprint;
		}

		private static void DoorCorridor(MeshModel model, Footprint fp, bool[] filled)
		{
			string text = model.Name ?? "";
			if (!text.StartsWith("house_", StringComparison.Ordinal) && !text.StartsWith("tent", StringComparison.Ordinal))
			{
				return;
			}
			HousePlan housePlan = HousePlan.Of(model);
			if (housePlan == null)
			{
				return;
			}
			HousePlan.Opening door = housePlan.Door;
			float num = (float)Math.Sqrt(door.OutX * door.OutX + door.OutY * door.OutY);
			if (num < 0.0001f || door.HalfWidth <= 0f)
			{
				return;
			}
			float num2 = door.OutX / num;
			float num3 = door.OutY / num;
			float num4 = door.HalfWidth * 0.85f;
			for (int i = 0; i < filled.Length; i++)
			{
				if (filled[i])
				{
					float num5 = fp.X0 + ((float)(i % fp.W) + 0.5f) * fp.Cell;
					float num6 = fp.Y0 + ((float)(i / fp.W) + 0.5f) * fp.Cell;
					float num7 = (num5 - door.X) * num2 + (num6 - door.Y) * num3;
					float num8 = Math.Abs((num5 - door.X) * (0f - num3) + (num6 - door.Y) * num2);
					if (num7 > 0.03f && num8 < num4)
					{
						filled[i] = false;
					}
				}
			}
		}

		private static int ClipZ(Vec3[] src, int n, Vec3[] dst, float plane, bool keepAbove)
		{
			int num = 0;
			for (int i = 0; i < n; i++)
			{
				Vec3 vec = src[i];
				Vec3 vec2 = src[(i + 1) % n];
				bool num2 = (keepAbove ? (vec.Z >= plane) : (vec.Z <= plane));
				bool flag = (keepAbove ? (vec2.Z >= plane) : (vec2.Z <= plane));
				if (num2 && num < dst.Length)
				{
					dst[num++] = vec;
				}
				if (num2 != flag && num < dst.Length)
				{
					float num3 = (plane - vec.Z) / (vec2.Z - vec.Z);
					dst[num++] = new Vec3(vec.X + (vec2.X - vec.X) * num3, vec.Y + (vec2.Y - vec.Y) * num3, plane);
				}
			}
			return num;
		}

		private static void FillPolygon(Footprint fp, bool[] solid, Vec3[] p, int n)
		{
			for (int i = 0; i < n; i++)
			{
				Line(fp, solid, p[i], p[(i + 1) % n]);
			}
			float num = float.MaxValue;
			float num2 = float.MinValue;
			float num3 = float.MaxValue;
			float num4 = float.MinValue;
			for (int j = 0; j < n; j++)
			{
				num = Math.Min(num, p[j].X);
				num2 = Math.Max(num2, p[j].X);
				num3 = Math.Min(num3, p[j].Y);
				num4 = Math.Max(num4, p[j].Y);
			}
			int num5 = Math.Max(0, (int)((num - fp.X0) / fp.Cell));
			int num6 = Math.Min(fp.W - 1, (int)((num2 - fp.X0) / fp.Cell));
			int num7 = Math.Max(0, (int)((num3 - fp.Y0) / fp.Cell));
			int num8 = Math.Min(fp.H - 1, (int)((num4 - fp.Y0) / fp.Cell));
			for (int k = num7; k <= num8; k++)
			{
				for (int l = num5; l <= num6; l++)
				{
					float num9 = fp.X0 + ((float)l + 0.5f) * fp.Cell;
					float num10 = fp.Y0 + ((float)k + 0.5f) * fp.Cell;
					int num11 = 0;
					int num12 = 0;
					for (int m = 0; m < n; m++)
					{
						Vec3 vec = p[m];
						Vec3 vec2 = p[(m + 1) % n];
						float num13 = (vec2.X - vec.X) * (num10 - vec.Y) - (vec2.Y - vec.Y) * (num9 - vec.X);
						if (num13 > 1E-07f)
						{
							num11++;
						}
						else if (num13 < -1E-07f)
						{
							num12++;
						}
					}
					if (num11 == 0 || num12 == 0)
					{
						solid[k * fp.W + l] = true;
					}
				}
			}
		}

		private static void Line(Footprint fp, bool[] solid, Vec3 a, Vec3 b)
		{
			float num = (float)Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
			int num2 = Math.Max(1, (int)Math.Ceiling(num / (fp.Cell * 0.5f)));
			for (int i = 0; i <= num2; i++)
			{
				float num3 = (float)i / (float)num2;
				int num4 = (int)((a.X + (b.X - a.X) * num3 - fp.X0) / fp.Cell);
				int num5 = (int)((a.Y + (b.Y - a.Y) * num3 - fp.Y0) / fp.Cell);
				if (num4 >= 0 && num5 >= 0 && num4 < fp.W && num5 < fp.H)
				{
					solid[num5 * fp.W + num4] = true;
				}
			}
		}

		private static float[] Chamfer(bool[] set, int w, int h)
		{
			float[] array = new float[w * h];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = (set[i] ? 0f : 1000000f);
			}
			for (int j = 0; j < h; j++)
			{
				for (int k = 0; k < w; k++)
				{
					int num = j * w + k;
					float num2 = array[num];
					if (num2 == 0f)
					{
						continue;
					}
					if (k > 0)
					{
						num2 = Math.Min(num2, array[num - 1] + 1f);
					}
					if (j > 0)
					{
						num2 = Math.Min(num2, array[num - w] + 1f);
						if (k > 0)
						{
							num2 = Math.Min(num2, array[num - w - 1] + 1.4142f);
						}
						if (k < w - 1)
						{
							num2 = Math.Min(num2, array[num - w + 1] + 1.4142f);
						}
					}
					array[num] = num2;
				}
			}
			for (int num3 = h - 1; num3 >= 0; num3--)
			{
				for (int num4 = w - 1; num4 >= 0; num4--)
				{
					int num5 = num3 * w + num4;
					float num6 = array[num5];
					if (num6 != 0f)
					{
						if (num4 < w - 1)
						{
							num6 = Math.Min(num6, array[num5 + 1] + 1f);
						}
						if (num3 < h - 1)
						{
							num6 = Math.Min(num6, array[num5 + w] + 1f);
							if (num4 < w - 1)
							{
								num6 = Math.Min(num6, array[num5 + w + 1] + 1.4142f);
							}
							if (num4 > 0)
							{
								num6 = Math.Min(num6, array[num5 + w - 1] + 1.4142f);
							}
						}
						array[num5] = num6;
					}
				}
			}
			return array;
		}

		private static float[] Distances(bool[] set, int w, int h)
		{
			int num = Math.Max(w, h);
			double[] array = new double[num];
			double[] array2 = new double[num];
			int[] v = new int[num];
			double[] z = new double[num + 1];
			double[] array3 = new double[w * h];
			for (int i = 0; i < w; i++)
			{
				for (int j = 0; j < h; j++)
				{
					array[j] = (set[j * w + i] ? 0.0 : 1000000000000.0);
				}
				Squared(array, h, array2, v, z);
				for (int k = 0; k < h; k++)
				{
					array3[k * w + i] = array2[k];
				}
			}
			float[] array4 = new float[w * h];
			for (int l = 0; l < h; l++)
			{
				for (int m = 0; m < w; m++)
				{
					array[m] = array3[l * w + m];
				}
				Squared(array, w, array2, v, z);
				for (int n = 0; n < w; n++)
				{
					array4[l * w + n] = (float)Math.Sqrt(Math.Min(array2[n], 1000000000000.0));
				}
			}
			return array4;
		}

		private static void Squared(double[] f, int n, double[] d, int[] v, double[] z)
		{
			int num = 0;
			v[0] = 0;
			z[0] = double.NegativeInfinity;
			z[1] = double.PositiveInfinity;
			for (int i = 1; i < n; i++)
			{
				double num2;
				for (num2 = (f[i] + (double)i * (double)i - (f[v[num]] + (double)v[num] * (double)v[num])) / (2.0 * (double)i - 2.0 * (double)v[num]); num2 <= z[num]; num2 = (f[i] + (double)i * (double)i - (f[v[num]] + (double)v[num] * (double)v[num])) / (2.0 * (double)i - 2.0 * (double)v[num]))
				{
					num--;
				}
				num++;
				v[num] = i;
				z[num] = num2;
				z[num + 1] = double.PositiveInfinity;
			}
			num = 0;
			for (int j = 0; j < n; j++)
			{
				for (; z[num + 1] < (double)j; num++)
				{
				}
				double num3 = j - v[num];
				d[j] = num3 * num3 + f[v[num]];
			}
		}
	}
}
