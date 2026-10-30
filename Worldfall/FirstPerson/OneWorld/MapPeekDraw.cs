using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FirstPerson.Core;
using FirstPerson.Net;
using UnityEngine;

namespace FirstPerson.OneWorld
{
	internal static class MapPeekDraw
	{
		private sealed class Floor
		{
			public Sprite Sprite;

			public Texture2D Tex;

			public RoomLayout Layout;

			public float Used;

			public Vector2 At;
		}

		private sealed class RefCompare : IEqualityComparer<Sprite>
		{
			public bool Equals(Sprite a, Sprite b)
			{
				return (object)a == b;
			}

			public int GetHashCode(Sprite s)
			{
				return RuntimeHelpers.GetHashCode(s);
			}
		}

		private const int Px = 10;

		private const int KeepFloors = 32;

		private const int Pool = 16;

		private const int Order = 30000;

		private static GameObject _root;

		private static SpriteRenderer _floor;

		private static SpriteRenderer _roof;

		private static readonly List<SpriteRenderer> _people = new List<SpriteRenderer>();

		private static readonly List<Actor> _drawn = new List<Actor>();

		private static readonly List<Vector2> _drawnAt = new List<Vector2>();

		private static readonly Dictionary<long, Floor> _floors = new Dictionary<long, Floor>();

		private static readonly Dictionary<Sprite, Sprite[]> _clear = new Dictionary<Sprite, Sprite[]>(new RefCompare());

		private static Sprite _roofSprite;

		private static Building _roofFor;

		private static bool _gameTint;

		internal static void Begin(Building b)
		{
			_roofFor = b;
			_roofSprite = null;
			try
			{
				_roofSprite = b.checkSpriteToRender();
			}
			catch
			{
			}
			Sprite sprite = ((b.animData?.main != null && b.animData.main.Length != 0) ? b.animData.main[0] : null);
			if (sprite != null && !_clear.ContainsKey(sprite))
			{
				_clear[sprite] = new Sprite[1] { Clear(sprite) };
			}
		}

		internal static Sprite[] ClearOf(Sprite main)
		{
			if ((object)main != null)
			{
				if (!_clear.TryGetValue(main, out var value))
				{
					return null;
				}
				return value;
			}
			return null;
		}

		private static Sprite Clear(Sprite s)
		{
			Rect textureRect = s.textureRect;
			int num = Mathf.Max(1, (int)textureRect.width);
			int num2 = Mathf.Max(1, (int)textureRect.height);
			Texture2D texture2D = new Texture2D(num, num2, TextureFormat.RGBA32, mipChain: false);
			texture2D.filterMode = FilterMode.Point;
			texture2D.name = s.name + "_fp_open_roof";
			texture2D.SetPixels32(new Color32[num * num2]);
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
			Sprite sprite = Sprite.Create(texture2D, new Rect(0f, 0f, num, num2), new Vector2(s.pivot.x / (float)num, s.pivot.y / (float)num2), s.pixelsPerUnit, 0u, SpriteMeshType.FullRect);
			sprite.name = s.name + "_fp_open_roof";
			return sprite;
		}

		private static void Ensure()
		{
			if (!(_root != null))
			{
				_root = new GameObject("fp_peek");
				UnityEngine.Object.DontDestroyOnLoad(_root);
				_floor = Renderer("floor", 30000, person: false);
				for (int i = 0; i < 16; i++)
				{
					_people.Add(Renderer("person", 30001, person: true));
				}
				_roof = Renderer("roof", 30002, person: false);
			}
		}

		private static SpriteRenderer Renderer(string name, int order, bool person)
		{
			GameObject gameObject = new GameObject("fp_peek_" + name);
			gameObject.transform.SetParent(_root.transform, worldPositionStays: false);
			SpriteRenderer spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
			spriteRenderer.sortingLayerName = "Objects";
			spriteRenderer.sortingOrder = order;
			try
			{
				LibraryMaterials instance = LibraryMaterials.instance;
				if (instance != null && (person ? instance.mat_world_object : instance.mat_buildings) != null)
				{
					spriteRenderer.sharedMaterial = (person ? instance.mat_world_object : instance.mat_buildings);
					_gameTint = true;
				}
			}
			catch
			{
			}
			spriteRenderer.enabled = false;
			return spriteRenderer;
		}

		public static void Hide()
		{
			if (_root == null)
			{
				return;
			}
			SpriteRenderer floor = _floor;
			bool enabled = (_roof.enabled = false);
			floor.enabled = enabled;
			foreach (SpriteRenderer person in _people)
			{
				person.enabled = false;
			}
			_drawn.Clear();
			_drawnAt.Clear();
		}

		public static int Show(Building b, float open)
		{
			Ensure();
			HouseWorld.Placement pose;
			RoomLayout roomLayout = InsideSpots.LayoutOf(b, out pose);
			Color color = Night();
			if (_roofFor != b)
			{
				Begin(b);
			}
			_drawn.Clear();
			_drawnAt.Clear();
			if (_roofSprite != null)
			{
				_roof.sprite = _roofSprite;
				Vector3 cur_transform_position = ((BaseSimObject)b).cur_transform_position;
				_roof.transform.position = new Vector3(cur_transform_position.x, cur_transform_position.y, -0.21f);
				Vector3 currentScale = b.getCurrentScale();
				_roof.transform.localScale = new Vector3(currentScale.x, currentScale.y, 1f);
				_roof.flipX = b.flip_x;
				Color color2 = Color.white;
				try
				{
					if (b.kingdom?.asset != null)
					{
						color2 = b.kingdom.asset.color_building;
					}
				}
				catch
				{
				}
				_roof.color = new Color(color.r * color2.r, color.g * color2.g, color.b * color2.b, (1f - open) * color2.a);
				_roof.enabled = open < 1f;
			}
			else
			{
				_roof.enabled = false;
			}
			if (roomLayout == null)
			{
				_floor.enabled = false;
				foreach (SpriteRenderer person in _people)
				{
					person.enabled = false;
				}
				return 0;
			}
			List<Actor> inside = HouseOccupancy.Of(b).Inside;
			int[] array = new int[Math.Max(1, roomLayout.Storeys)];
			RoomLayout layout;
			foreach (Actor item2 in inside)
			{
				if (InsideSpots.Where(item2, out layout, out var place) && place.Storey < array.Length)
				{
					array[place.Storey]++;
				}
			}
			int num = 0;
			for (int i = 1; i < array.Length; i++)
			{
				if (array[i] > array[num])
				{
					num = i;
				}
			}
			bool otherLived = false;
			for (int j = 0; j < array.Length; j++)
			{
				if (j != num && array[j] > 0)
				{
					otherLived = true;
				}
			}
			Floor floor = FloorOf(b, roomLayout, num, otherLived);
			_floor.sprite = floor.Sprite;
			_floor.transform.position = new Vector3(floor.At.x, floor.At.y, -0.205f);
			_floor.transform.localScale = Vector3.one;
			_floor.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(open * 1.5f));
			_floor.enabled = true;
			int num2 = 0;
			foreach (Actor item3 in inside)
			{
				if (num2 >= _people.Count)
				{
					break;
				}
				if (item3 != null && item3.isAlive() && InsideSpots.Where(item3, out layout, out var place2) && place2.Storey == num)
				{
					Sprite sprite = null;
					try
					{
						sprite = item3.getSpriteToRender();
					}
					catch
					{
					}
					if (!(sprite == null))
					{
						SpriteRenderer spriteRenderer = _people[num2++];
						Vector2 item = (WorldSync.Mirroring ? item3.current_position : InsideSpots.ToWorld(b, place2.MidX, place2.MidY));
						bool flag = place2.Use == RoomLayout.Use.Bed && HouseOccupancy.Asleep(item3);
						spriteRenderer.sprite = sprite;
						float num3 = sprite.rect.height / Mathf.Max(0.01f, sprite.pixelsPerUnit) * Mathf.Abs(item3.current_scale.y);
						spriteRenderer.transform.position = new Vector3(item.x + (flag ? (num3 * 0.5f) : 0f), item.y - (flag ? 0f : 0.25f), -0.206f);
						spriteRenderer.transform.localScale = item3.current_scale;
						spriteRenderer.transform.eulerAngles = new Vector3(0f, ((BaseSimObject)item3).current_rotation.y, flag ? 90f : 0f);
						Color color3 = item3.color * color;
						color3.a = item3.color.a * Mathf.Clamp01(open * 1.5f);
						spriteRenderer.color = color3;
						spriteRenderer.enabled = true;
						_drawn.Add(item3);
						_drawnAt.Add(item);
					}
				}
			}
			for (int k = num2; k < _people.Count; k++)
			{
				_people[k].enabled = false;
			}
			return num2;
		}

		private static Color Night()
		{
			if (_gameTint)
			{
				return Color.white;
			}
			float num = 0f;
			try
			{
				num = World.world.era_manager.getNightMod();
			}
			catch
			{
			}
			return new Color(1f / 51f, 0f, 21f / 85f, 1f) * num + Color.white * (1f - num);
		}

		public static Actor PersonAt(Vector2 p)
		{
			Actor result = null;
			float num = 0.25f;
			for (int i = 0; i < _drawn.Count; i++)
			{
				float sqrMagnitude = (_drawnAt[i] - p).sqrMagnitude;
				if (sqrMagnitude < num)
				{
					num = sqrMagnitude;
					result = _drawn[i];
				}
			}
			return result;
		}

		private static Floor FloorOf(Building b, RoomLayout L, int storey, bool otherLived)
		{
			long key = b.data.id * 8 + storey * 2 + (otherLived ? 1 : 0);
			if (_floors.TryGetValue(key, out var value) && value.Layout == L)
			{
				value.Used = Time.unscaledTime;
				return value;
			}
			if (value != null)
			{
				Drop(key, value);
			}
			if (_floors.Count >= 32)
			{
				long key2 = 0L;
				float num = float.MaxValue;
				foreach (KeyValuePair<long, Floor> floor in _floors)
				{
					if (floor.Value.Used < num)
					{
						num = floor.Value.Used;
						key2 = floor.Key;
					}
				}
				Drop(key2, _floors[key2]);
			}
			value = Draw(b, L, storey, otherLived);
			_floors[key] = value;
			return value;
		}

		private static void Drop(long key, Floor f)
		{
			_floors.Remove(key);
			if (f.Sprite != null)
			{
				UnityEngine.Object.Destroy(f.Sprite);
			}
			if (f.Tex != null)
			{
				UnityEngine.Object.Destroy(f.Tex);
			}
		}

		private static Floor Draw(Building b, RoomLayout L, int storey, bool otherLived)
		{
			float x0 = float.MaxValue;
			float y0 = float.MaxValue;
			float x1 = float.MinValue;
			float y1 = float.MinValue;
			List<float[]> list = ((storey < L.Walls.Count) ? L.Walls[storey] : L.Walls[0]);
			foreach (float[] item in list)
			{
				Grow(item[0], item[1]);
				Grow(item[2], item[3]);
			}
			x0 -= 0.2f;
			y0 -= 0.2f;
			x1 += 0.2f;
			y1 += 0.2f;
			int w = Mathf.Clamp(Mathf.CeilToInt((x1 - x0) * 10f), 4, 160);
			int h = Mathf.Clamp(Mathf.CeilToInt((y1 - y0) * 10f), 4, 160);
			Color32[] array = new Color32[w * h];
			bool[] wall = new bool[w * h];
			foreach (float[] item2 in list)
			{
				Line(InsideSpots.ToWorld(b, item2[0], item2[1]), InsideSpots.ToWorld(b, item2[2], item2[3]), wall);
			}
			bool[] floor = new bool[w * h];
			Stack<int> stack = new Stack<int>();
			Seed(L.DoorX, L.DoorY);
			Seed((L.MinX + L.MaxX) * 0.5f, (L.MinY + L.MaxY) * 0.5f);
			foreach (RoomLayout.Place place in L.Places)
			{
				if (place.Storey == storey)
				{
					Seed(place.X, place.Y);
				}
			}
			int i;
			while (stack.Count > 0)
			{
				int num = stack.Pop();
				int num2 = num % w;
				int num3 = num / w;
				int[] array2 = new int[4]
				{
					(num2 > 0) ? (num - 1) : (-1),
					(num2 < w - 1) ? (num + 1) : (-1),
					(num3 > 0) ? (num - w) : (-1),
					(num3 < h - 1) ? (num + w) : (-1)
				};
				for (i = 0; i < array2.Length; i++)
				{
					int num4 = array2[i];
					if (num4 >= 0 && !floor[num4] && !wall[num4])
					{
						int num5 = num4 % w;
						int num6 = num4 / w;
						if (num5 == 0 || num6 == 0 || num5 == w - 1 || num6 == h - 1)
						{
							floor[num4] = false;
							continue;
						}
						floor[num4] = true;
						stack.Push(num4);
					}
				}
			}
			HouseInterior.RaceAndTier(b.asset, out var race, out i);
			Color32 color;
			switch (race)
			{
			default:
				color = new Color32(150, 106, 66, byte.MaxValue);
				break;
			case "elf":
				color = new Color32(176, 152, 110, byte.MaxValue);
				break;
			case "orc":
				color = new Color32(124, 96, 68, byte.MaxValue);
				break;
			case "dwarf":
				color = new Color32(118, 116, 120, byte.MaxValue);
				break;
			}
			Color32 color2 = color;
			Color32 color3 = Darker(color2, 0.8f);
			Color32 color4 = new Color32(170, 40, 160, byte.MaxValue);
			try
			{
				ColorAsset colorAsset = b.kingdom?.getColor();
				if (colorAsset != null)
				{
					color4 = colorAsset.k_color_0;
				}
			}
			catch
			{
			}
			for (int j = 0; j < h; j++)
			{
				for (int k = 0; k < w; k++)
				{
					int num7 = j * w + k;
					if (floor[num7])
					{
						array[num7] = ((j % 3 == 0) ? color3 : color2);
					}
				}
			}
			List<RoomLayout.Piece> list2 = new List<RoomLayout.Piece>();
			foreach (RoomLayout.Piece piece in L.Pieces)
			{
				if (piece.Storey == storey)
				{
					list2.Add(piece);
				}
			}
			list2.Sort((RoomLayout.Piece p, RoomLayout.Piece q) => ((p.Kind != RoomLayout.PieceKind.Rug) ? 1 : 0).CompareTo((q.Kind != RoomLayout.PieceKind.Rug) ? 1 : 0));
			foreach (RoomLayout.Piece item3 in list2)
			{
				Piece(item3, b, array, w, h, x0, y0, color4);
			}
			for (int num8 = 0; num8 < h; num8++)
			{
				for (int num9 = 0; num9 < w; num9++)
				{
					int num10 = num8 * w + num9;
					if (wall[num10])
					{
						bool flag = num8 + 1 < h && !wall[num10 + w] && !floor[num10 + w];
						array[num10] = (flag ? color4 : new Color32(52, 40, 32, byte.MaxValue));
						if (num8 > 0 && !wall[num10 - w] && array[num10 - w].a == 0)
						{
							array[num10 - w] = new Color32(40, 30, 24, byte.MaxValue);
						}
					}
				}
			}
			if (otherLived)
			{
				for (int num11 = 0; num11 < 3; num11++)
				{
					for (int num12 = 0; num12 <= num11; num12++)
					{
						int num13 = w - 3 + num12 - 1;
						int num14 = h - 3 - num11 + 2 - 1;
						if (num13 >= 0 && num14 >= 0 && num13 < w && num14 < h)
						{
							array[num14 * w + num13] = new Color32(240, 230, 200, byte.MaxValue);
						}
					}
				}
			}
			Texture2D texture2D = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false)
			{
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
				name = "fp_peek_floor"
			};
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
			Sprite sprite = Sprite.Create(texture2D, new Rect(0f, 0f, w, h), Vector2.zero, 10f, 0u, SpriteMeshType.FullRect);
			return new Floor
			{
				Sprite = sprite,
				Tex = texture2D,
				Layout = L,
				Used = Time.unscaledTime,
				At = new Vector2(x0, y0)
			};
			void Grow(float rx, float ry)
			{
				Vector2 vector = InsideSpots.ToWorld(b, rx, ry);
				x0 = Mathf.Min(x0, vector.x);
				y0 = Mathf.Min(y0, vector.y);
				x1 = Mathf.Max(x1, vector.x);
				y1 = Mathf.Max(y1, vector.y);
			}
			int Ix(float wx)
			{
				return Mathf.FloorToInt((wx - x0) * 10f);
			}
			int Iy(float wy)
			{
				return Mathf.FloorToInt((wy - y0) * 10f);
			}
			void Line(Vector2 a, Vector2 c, bool[] mask)
			{
				int num15 = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(c.x - a.x), Mathf.Abs(c.y - a.y)) * 10f * 2f) + 1;
				for (int l = 0; l <= num15; l++)
				{
					Vector2 vector = Vector2.Lerp(a, c, (float)l / (float)num15);
					int num16 = Ix(vector.x);
					int num17 = Iy(vector.y);
					if (num16 >= 0 && num17 >= 0 && num16 < w && num17 < h)
					{
						mask[num17 * w + num16] = true;
					}
				}
			}
			void Seed(float rx, float ry)
			{
				Vector2 vector = InsideSpots.ToWorld(b, rx, ry);
				int num15 = Ix(vector.x);
				int num16 = Iy(vector.y);
				if (num15 > 0 && num16 > 0 && num15 < w - 1 && num16 < h - 1)
				{
					int num17 = num16 * w + num15;
					if (!wall[num17] && !floor[num17])
					{
						stack.Push(num17);
						floor[num17] = true;
					}
				}
			}
		}

		private static Color32 Darker(Color32 c, float k)
		{
			return new Color32((byte)((float)(int)c.r * k), (byte)((float)(int)c.g * k), (byte)((float)(int)c.b * k), c.a);
		}

		private static void Piece(RoomLayout.Piece p, Building b, Color32[] px, int w, int h, float x0, float y0, Color32 kingdom)
		{
			Vector2[] array = new Vector2[4];
			for (int i = 0; i < 4; i++)
			{
				array[i] = InsideSpots.ToWorld(b, p.Corners[i * 2], p.Corners[i * 2 + 1]);
			}
			float num = Mathf.Min(Mathf.Min(array[0].x, array[1].x), Mathf.Min(array[2].x, array[3].x));
			float num2 = Mathf.Max(Mathf.Max(array[0].x, array[1].x), Mathf.Max(array[2].x, array[3].x));
			float num3 = Mathf.Min(Mathf.Min(array[0].y, array[1].y), Mathf.Min(array[2].y, array[3].y));
			float num4 = Mathf.Max(Mathf.Max(array[0].y, array[1].y), Mathf.Max(array[2].y, array[3].y));
			Vector2 head = InsideSpots.ToWorld(b, p.X - Mathf.Cos(p.Yaw) * 0.3f, p.Y - Mathf.Sin(p.Yaw) * 0.3f) - InsideSpots.ToWorld(b, p.X, p.Y);
			for (float num5 = num3; num5 <= num4; num5 += 0.05f)
			{
				for (float num6 = num; num6 <= num2; num6 += 0.05f)
				{
					if (!Inside(array, num6, num5))
					{
						continue;
					}
					int num7 = Mathf.FloorToInt((num6 - x0) * 10f);
					int num8 = Mathf.FloorToInt((num5 - y0) * 10f);
					if (num7 >= 0 && num8 >= 0 && num7 < w && num8 < h)
					{
						bool edge = !Inside(array, num6 - 0.1f, num5) || !Inside(array, num6 + 0.1f, num5) || !Inside(array, num6, num5 - 0.1f) || !Inside(array, num6, num5 + 0.1f);
						Color32 color = Colour(p.Kind, num6, num5, array, head, kingdom, edge);
						if (color.a > 0)
						{
							px[num8 * w + num7] = color;
						}
					}
				}
			}
		}

		private static Color32 Colour(RoomLayout.PieceKind kind, float x, float y, Vector2[] c, Vector2 head, Color32 kingdom, bool edge)
		{
			Vector2 vector = (c[0] + c[1] + c[2] + c[3]) * 0.25f;
			switch (kind)
			{
			case RoomLayout.PieceKind.Bed:
			{
				if (edge)
				{
					return new Color32(92, 58, 34, byte.MaxValue);
				}
				float num = Vector2.Dot(new Vector2(x, y) - vector, head.normalized);
				Mathf.Max(0.2f, head.magnitude);
				if (!(num > 0.18f))
				{
					return Darker(kingdom, 0.85f);
				}
				return new Color32(236, 232, 220, byte.MaxValue);
			}
			case RoomLayout.PieceKind.Hearth:
				if (!edge)
				{
					if (!((new Vector2(x, y) - vector).sqrMagnitude < 0.02f))
					{
						return new Color32(132, 126, 120, byte.MaxValue);
					}
					return new Color32(byte.MaxValue, 150, 40, byte.MaxValue);
				}
				return new Color32(96, 92, 90, byte.MaxValue);
			case RoomLayout.PieceKind.Table:
				if (!edge)
				{
					return new Color32(140, 96, 56, byte.MaxValue);
				}
				return new Color32(96, 64, 38, byte.MaxValue);
			case RoomLayout.PieceKind.Seat:
				return new Color32(112, 76, 44, byte.MaxValue);
			case RoomLayout.PieceKind.Storage:
				if (!edge)
				{
					return new Color32(118, 82, 48, byte.MaxValue);
				}
				return new Color32(70, 46, 28, byte.MaxValue);
			case RoomLayout.PieceKind.Bench:
				if (!edge)
				{
					return new Color32(120, 120, 128, byte.MaxValue);
				}
				return new Color32(80, 80, 88, byte.MaxValue);
			case RoomLayout.PieceKind.Rug:
				return Darker(kingdom, 0.6f);
			case RoomLayout.PieceKind.Stair:
				if (((int)((x + y) * 10f) & 1) != 0)
				{
					return new Color32(110, 80, 52, byte.MaxValue);
				}
				return new Color32(160, 120, 80, byte.MaxValue);
			default:
				if (!edge)
				{
					return new Color32(104, 90, 70, byte.MaxValue);
				}
				return new Color32(70, 60, 48, byte.MaxValue);
			}
		}

		private static bool Inside(Vector2[] c, float x, float y)
		{
			bool flag = false;
			bool flag2 = false;
			for (int i = 0; i < 4; i++)
			{
				Vector2 vector = c[i];
				Vector2 vector2 = c[(i + 1) & 3];
				float num = (vector2.x - vector.x) * (y - vector.y) - (vector2.y - vector.y) * (x - vector.x);
				if (num > 0f)
				{
					flag = true;
				}
				else if (num < 0f)
				{
					flag2 = true;
				}
			}
			return !(flag && flag2);
		}
	}
}
