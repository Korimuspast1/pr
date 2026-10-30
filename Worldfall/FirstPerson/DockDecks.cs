using System;
using System.Collections.Generic;
using System.Threading;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class DockDecks
	{
		private sealed class Dock
		{
			public Building Building;

			public WalkSurface Surface;

			public float X;

			public float Y;

			public float Z;

			public float Cos;

			public float Sin;

			public float Scale;

			public float Reach;
		}

		private static readonly List<Dock> All = new List<Dock>();

		private static readonly object Gate = new object();

		internal static bool Boxes = false;

		private static float[] _boxes = new float[0];

		internal static bool VerifyBoxes;

		internal static int BoxCompared;

		internal static int BoxDiffered;

		internal static int BoxFresh;

		internal static string BoxFirst;

		private static object _world;

		private static float _pruneIn;

		public static int Count => All.Count;

		private static void Rebox()
		{
			float[] array = new float[All.Count * 4];
			for (int i = 0; i < All.Count; i++)
			{
				BoxOf(All[i].X, All[i].Y, All[i].Reach, array, i * 4);
			}
			Volatile.Write(ref _boxes, array);
		}

		internal static void BoxOf(float x, float y, float reach, float[] boxes, int at)
		{
			bool flag = Math.Abs(x) < 100000f && Math.Abs(y) < 100000f && reach >= 0f && reach < 100000f;
			boxes[at] = (flag ? (x - reach - 1f) : float.NegativeInfinity);
			boxes[at + 1] = (flag ? (x + reach + 1f) : float.PositiveInfinity);
			boxes[at + 2] = (flag ? (y - reach - 1f) : float.NegativeInfinity);
			boxes[at + 3] = (flag ? (y + reach + 1f) : float.PositiveInfinity);
		}

		internal static bool BeyondForTest(float x, float y, float dockX, float dockY, float reach)
		{
			float num = x - dockX;
			float num2 = y - dockY;
			return num * num + num2 * num2 > reach * reach;
		}

		internal static bool BoxesSkip(float[] boxes, float x, float y)
		{
			if (float.IsNaN(x) || float.IsNaN(y))
			{
				return false;
			}
			for (int i = 0; i < boxes.Length; i += 4)
			{
				if (!(x < boxes[i]) && !(x > boxes[i + 1]) && !(y < boxes[i + 2]) && !(y > boxes[i + 3]))
				{
					return false;
				}
			}
			return true;
		}

		private static void CompareBoxes()
		{
			float[] array = Volatile.Read(ref _boxes);
			float[] array2;
			lock (Gate)
			{
				array2 = new float[All.Count * 4];
				for (int i = 0; i < All.Count; i++)
				{
					BoxOf(All[i].X, All[i].Y, All[i].Reach, array2, i * 4);
				}
			}
			BoxFresh++;
			bool flag = array.Length == array2.Length;
			int num = 0;
			while (flag && num < array.Length)
			{
				flag = NpcTune.Bits(array[num]) == NpcTune.Bits(array2[num]);
				num++;
			}
			if (!flag)
			{
				BoxDiffered++;
				if (BoxFirst == null)
				{
					BoxFirst = "the boxes kept (" + array.Length / 4 + " docks) not the docks' now (" + array2.Length / 4 + ")";
				}
			}
		}

		public static void Register(Building b, MeshModel model, float x, float y, float z, float heading, float scale)
		{
			WalkSurface walkSurface = WalkSurface.Of(model);
			if (b == null || walkSurface == null)
			{
				return;
			}
			float f = heading + MathF.PI / 2f;
			lock (Gate)
			{
				Dock dock = null;
				for (int i = 0; i < All.Count; i++)
				{
					if (All[i].Building == b)
					{
						dock = All[i];
						break;
					}
				}
				bool num = dock == null;
				if (dock == null)
				{
					dock = new Dock
					{
						Building = b
					};
					All.Add(dock);
				}
				float x2 = dock.X;
				float y2 = dock.Y;
				float reach = dock.Reach;
				dock.Surface = walkSurface;
				dock.X = x;
				dock.Y = y;
				dock.Z = z;
				dock.Cos = Mathf.Cos(f);
				dock.Sin = Mathf.Sin(f);
				dock.Scale = Mathf.Max(0.0001f, scale);
				dock.Reach = walkSurface.Reach * dock.Scale + 0.5f;
				if (num || x2 != dock.X || y2 != dock.Y || reach != dock.Reach)
				{
					Rebox();
				}
			}
		}

		public static void Prune()
		{
			lock (Gate)
			{
				int count = All.Count;
				for (int num = All.Count - 1; num >= 0; num--)
				{
					Building building = All[num].Building;
					bool flag;
					try
					{
						flag = building == null || !building.isAlive() || building.isRuin();
					}
					catch
					{
						flag = true;
					}
					if (flag)
					{
						All.RemoveAt(num);
					}
				}
				if (All.Count != count)
				{
					Rebox();
				}
			}
		}

		public static void Clear()
		{
			lock (Gate)
			{
				All.Clear();
				Rebox();
			}
		}

		public static void Upkeep(float dt)
		{
			object obj = null;
			try
			{
				obj = ((World.world != null) ? World.world.map_stats : null);
			}
			catch
			{
			}
			if (obj != _world)
			{
				_world = obj;
				Clear();
			}
			_pruneIn -= dt;
			if (!(_pruneIn > 0f) && All.Count != 0)
			{
				_pruneIn = 1f;
				Prune();
			}
		}

		internal static bool PointForTest(int dock, float mx, float my, out Vector2 at, out float heading)
		{
			at = Vector2.zero;
			heading = 0f;
			lock (Gate)
			{
				if (dock < 0 || dock >= All.Count)
				{
					return false;
				}
				Dock dock2 = All[dock];
				at = new Vector2(dock2.X + (dock2.Cos * mx - dock2.Sin * my) * dock2.Scale, dock2.Y + (dock2.Sin * mx + dock2.Cos * my) * dock2.Scale);
				heading = Mathf.Atan2(dock2.Sin, dock2.Cos) - MathF.PI / 2f;
				return true;
			}
		}

		internal static Building BuildingForTest(int dock)
		{
			lock (Gate)
			{
				return (dock >= 0 && dock < All.Count) ? All[dock].Building : null;
			}
		}

		public static bool HeightAt(float x, float y, out float z)
		{
			z = 0f;
			if (VerifyBoxes)
			{
				CompareBoxes();
			}
			if (All.Count == 0)
			{
				return false;
			}
			if (Boxes && BoxesSkip(Volatile.Read(ref _boxes), x, y))
			{
				if (VerifyBoxes)
				{
					CompareSkip(x, y);
				}
				return false;
			}
			bool flag = false;
			lock (Gate)
			{
				for (int i = 0; i < All.Count; i++)
				{
					Dock dock = All[i];
					float num = x - dock.X;
					float num2 = y - dock.Y;
					if (num * num + num2 * num2 > dock.Reach * dock.Reach)
					{
						continue;
					}
					float x2 = (num * dock.Cos + num2 * dock.Sin) / dock.Scale;
					float y2 = ((0f - num) * dock.Sin + num2 * dock.Cos) / dock.Scale;
					if (dock.Surface.HeightAt(x2, y2, out var z2))
					{
						float num3 = dock.Z + z2 * dock.Scale;
						if (!flag || num3 > z)
						{
							z = num3;
						}
						flag = true;
					}
				}
				return flag;
			}
		}

		public static bool On(Vector2 p)
		{
			float z;
			return HeightAt(p.x, p.y, out z);
		}

		private static void CompareSkip(float x, float y)
		{
			bool flag = false;
			float num = 0f;
			lock (Gate)
			{
				for (int i = 0; i < All.Count; i++)
				{
					Dock dock = All[i];
					float num2 = x - dock.X;
					float num3 = y - dock.Y;
					if (num2 * num2 + num3 * num3 > dock.Reach * dock.Reach)
					{
						continue;
					}
					float x2 = (num2 * dock.Cos + num3 * dock.Sin) / dock.Scale;
					float y2 = ((0f - num2) * dock.Sin + num3 * dock.Cos) / dock.Scale;
					if (dock.Surface.HeightAt(x2, y2, out var z))
					{
						float num4 = dock.Z + z * dock.Scale;
						if (!flag || num4 > num)
						{
							num = num4;
						}
						flag = true;
					}
				}
			}
			BoxCompared++;
			if (flag)
			{
				BoxDiffered++;
				if (BoxFirst == null)
				{
					BoxFirst = "(" + NpcTune.Show(x) + ", " + NpcTune.Show(y) + "): skipped, but a deck at " + NpcTune.Show(num);
				}
			}
		}
	}
}
