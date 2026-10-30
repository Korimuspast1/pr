using System;
using System.Collections.Generic;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal static class Streams
	{
		private sealed class Drawn
		{
			public int Key;

			public int Hash;

			public MeshModel Model;

			public readonly MeshInstance Inst = new MeshInstance();

			public Vector3 Origin;

			public float MinX;

			public float MinY;

			public float MaxX;

			public float MaxY;
		}

		public static bool Changed;

		private const float Width = 0.2f;

		private const float Over = 0.035f;

		private const float FallDrop = 1f;

		private const float Range = 90f;

		private static readonly Dictionary<int, Drawn> _drawn = new Dictionary<int, Drawn>();

		private static readonly List<int> _gone = new List<int>();

		internal static int DrawnForTest;

		internal static int FallsForTest;

		public static void Collect(TownScene s)
		{
			IReadOnlyList<Lakes.Lake> now = Lakes.Now;
			int num = 0;
			_gone.Clear();
			foreach (int key2 in _drawn.Keys)
			{
				_gone.Add(key2);
			}
			foreach (Lakes.Lake item in now)
			{
				List<int> stream = item.Stream;
				if (stream == null || stream.Count < 2)
				{
					continue;
				}
				int key = item.Key;
				_gone.Remove(key);
				int num2 = HashOf(stream);
				if (!_drawn.TryGetValue(key, out var value) || value.Hash != num2)
				{
					value = Build(s.Terrain, stream, key, num2);
					if (value == null)
					{
						continue;
					}
					_drawn[key] = value;
				}
				float x = (value.MinX + value.MaxX) * 0.5f;
				float y = (value.MinY + value.MaxY) * 0.5f;
				float num3 = Mathf.Max(value.MaxX - value.MinX, value.MaxY - value.MinY) * 0.5f + 1f;
				if (!(s.Dist2(x, y) > (90f + num3) * (90f + num3)) && s.InView(x, y, num3))
				{
					MeshInstance inst = value.Inst;
					inst.Model = value.Model;
					inst.Place(value.Origin.x, value.Origin.y, value.Origin.z, -MathF.PI / 2f, 1f, 1f, 1f);
					inst.NoShadow = true;
					inst.Static = true;
					inst.Tag = -1;
					inst.Flash = 0u;
					inst.Fade = 1f;
					inst.SortDepth = Mathf.Sqrt(s.Dist2(x, y));
					s.Meshes.Add(inst);
					num++;
				}
			}
			foreach (int item2 in _gone)
			{
				_drawn.Remove(item2);
			}
			DrawnForTest = num;
		}

		private static int HashOf(List<int> path)
		{
			int num = path.Count;
			foreach (int item in path)
			{
				num = num * 31 + item;
			}
			return num;
		}

		private static Drawn Build(TerrainCache c, List<int> path, int key, int hash)
		{
			int width = c.Width;
			if (width == 0)
			{
				return null;
			}
			List<Vector2> list = new List<Vector2>();
			foreach (int item in path)
			{
				list.Add(new Vector2((float)(item % width) + 0.5f, (float)(item / width) + 0.5f));
			}
			for (int i = 0; i < 2; i++)
			{
				if (list.Count <= 2)
				{
					break;
				}
				List<Vector2> list2 = new List<Vector2> { list[0] };
				for (int j = 0; j + 1 < list.Count; j++)
				{
					list2.Add(Vector2.Lerp(list[j], list[j + 1], 0.25f));
					list2.Add(Vector2.Lerp(list[j], list[j + 1], 0.75f));
				}
				list2.Add(list[list.Count - 1]);
				list = list2;
			}
			float[] array = new float[list.Count];
			for (int k = 0; k < list.Count; k++)
			{
				array[k] = c.SampleHeight(list[k].x, list[k].y) + 0.035f;
			}
			for (int l = 1; l < array.Length; l++)
			{
				if (array[l] > array[l - 1])
				{
					array[l] = array[l - 1];
				}
			}
			Vector3 origin = new Vector3(list[0].x, list[0].y, array[0]);
			MeshBuilder meshBuilder = new MeshBuilder("stream_" + key);
			int num = meshBuilder.Slot("water", 12, TerrainCache.Rgb(64, 126, 178), 16);
			int num2 = meshBuilder.Slot("white", 0, TerrainCache.Rgb(226, 236, 242), 0);
			meshBuilder.Part("item", -1, new Vec3(0f, 0f, 0f));
			Drawn drawn = new Drawn
			{
				Key = key,
				Hash = hash,
				Origin = origin,
				MinX = float.MaxValue,
				MinY = float.MaxValue,
				MaxX = float.MinValue,
				MaxY = float.MinValue
			};
			int num3 = 0;
			for (int m = 0; m + 1 < list.Count; m++)
			{
				Vector2 vector = list[m];
				Vector2 vector2 = list[m + 1];
				Vector2 normalized = (vector2 - vector).normalized;
				if (!(normalized.sqrMagnitude < 1E-06f))
				{
					Vector2 vector3 = new Vector2(0f - normalized.y, normalized.x) * 0.2f;
					float num4 = array[m] - origin.z;
					float num5 = array[m + 1] - origin.z;
					bool flag = num4 - num5 > 1f * (vector2 - vector).magnitude * 2f;
					int slot = ((flag || m % 7 == 3) ? num2 : num);
					meshBuilder.Face(slot, P(vector - vector3, num4), P(vector2 - vector3, num5), P(vector2 + vector3, num5), P(vector + vector3, num4));
					if (flag)
					{
						num3++;
						meshBuilder.Blob(num2, P(vector2, num5 - 0.0175f), 0.32000002f, 0.32000002f, 0.06f, 7);
					}
					drawn.MinX = Mathf.Min(drawn.MinX, Mathf.Min(vector.x, vector2.x));
					drawn.MaxX = Mathf.Max(drawn.MaxX, Mathf.Max(vector.x, vector2.x));
					drawn.MinY = Mathf.Min(drawn.MinY, Mathf.Min(vector.y, vector2.y));
					drawn.MaxY = Mathf.Max(drawn.MaxY, Mathf.Max(vector.y, vector2.y));
				}
			}
			FallsForTest = num3;
			drawn.Model = meshBuilder.Build();
			return drawn;
			Vec3 P(Vector2 p, float h)
			{
				return new Vec3(p.x - origin.x, p.y - origin.y, h);
			}
		}
	}
}
