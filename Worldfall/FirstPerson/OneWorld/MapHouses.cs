using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson.OneWorld
{
	internal static class MapHouses
	{
		private sealed class Frames
		{
			public Sprite Base;

			public readonly Sprite[][] Of = new Sprite[4][];

			public Vector2Int Chimney = new Vector2Int(-1, -1);

			public bool NoChimney;
		}

		private sealed class ByReference : IEqualityComparer<Sprite>
		{
			public static readonly ByReference Instance = new ByReference();

			public bool Equals(Sprite a, Sprite b)
			{
				return (object)a == b;
			}

			public int GetHashCode(Sprite s)
			{
				return RuntimeHelpers.GetHashCode(s);
			}
		}

		private const float DoorSeconds = 2f;

		private const float CurtainLight = 0.35f;

		private static readonly Color32 Cloth = new Color32(122, 52, 42, byte.MaxValue);

		private static readonly Color32 WindowLight = new Color32(byte.MaxValue, 216, 0, byte.MaxValue);

		private static float _now;

		private static int _day;

		private static float _hour;

		private static Building _yourHome;

		private static readonly Dictionary<BuildingAsset, BuildingOverrideMainSprites> _chained = new Dictionary<BuildingAsset, BuildingOverrideMainSprites>();

		private static bool _installed;

		private static readonly Dictionary<Sprite, Frames> _frames = new Dictionary<Sprite, Frames>(ByReference.Instance);

		private static readonly ConcurrentQueue<KeyValuePair<Sprite, BuildingAsset>> _wanted = new ConcurrentQueue<KeyValuePair<Sprite, BuildingAsset>>();

		private static readonly ConcurrentDictionary<Sprite, bool> _asked = new ConcurrentDictionary<Sprite, bool>(ByReference.Instance);

		private static int _made;

		public static bool On { get; private set; }

		public static bool Full { get; private set; }

		internal static int FramesMadeForTest => _made;

		public static void Tick(Settings settings, bool flatMap)
		{
			int num;
			int num2;
			if (Config.game_loaded && World.world != null)
			{
				num = ((!WorldBoxMod.WorldLoading) ? 1 : 0);
				if (num != 0)
				{
					num2 = ((settings.MapHouses >= 1) ? 1 : 0);
					goto IL_0031;
				}
			}
			else
			{
				num = 0;
			}
			num2 = 0;
			goto IL_0031;
			IL_0031:
			On = (byte)num2 != 0;
			Full = On && settings.MapHouses >= 2;
			if (num == 0)
			{
				return;
			}
			MapLights.Hook();
			Install();
			_now = Time.unscaledTime;
			_day = Calendar.Day;
			_hour = ((WorldBoxMod.Instance?.Day != null) ? WorldBoxMod.Instance.Day.Hour : 0.36f);
			Actor actor = WorldBoxMod.Instance?.Host;
			try
			{
				_yourHome = ((actor != null) ? Workbench.HomeOf(actor) : null);
			}
			catch
			{
				_yourHome = null;
			}
			MapPeek.Tick(settings, flatMap);
			if (!On)
			{
				MapShapes.Tick(full: false);
				return;
			}
			MapShapes.Tick(Full);
			MakeAsked(4);
			if (flatMap)
			{
				MapSmoke.Tick();
			}
		}

		private static void Install()
		{
			if (_installed)
			{
				return;
			}
			_installed = true;
			int num = 0;
			try
			{
				foreach (BuildingAsset item in AssetManager.buildings.list)
				{
					if (item != null && !(item.type != "type_house"))
					{
						_chained[item] = item.get_override_sprites_main;
						item.get_override_sprites_main = Answer;
						num++;
					}
				}
				Log.Info($"map houses: {num} house and tent kinds show their doors and curtains");
			}
			catch (Exception ex)
			{
				Log.Error("map houses: frames not installed: " + ex.Message);
			}
		}

		private static Sprite[] Answer(Building b)
		{
			Sprite[] array = null;
			try
			{
				if (_chained.TryGetValue(b.asset, out var value) && value != null)
				{
					array = value(b);
				}
			}
			catch
			{
			}
			if (MapPeek.Hides(b))
			{
				Sprite[] array2 = MapPeekDraw.ClearOf((b.animData?.main != null && b.animData.main.Length != 0) ? b.animData.main[0] : null);
				if (array2 != null)
				{
					return array2;
				}
			}
			if (!On)
			{
				return array;
			}
			try
			{
				Sprite[] array3 = array ?? b.animData?.main;
				if (array3 == null || array3.Length != 1 || (object)array3[0] == null)
				{
					return array;
				}
				Sprite key = MapShapes.ShapeOf(b) ?? array3[0];
				if (!_frames.TryGetValue(key, out var value2))
				{
					if (_asked.TryAdd(key, value: true))
					{
						_wanted.Enqueue(new KeyValuePair<Sprite, BuildingAsset>(key, b.asset));
					}
					return array;
				}
				int num = (DoorOpen(b) ? 1 : 0) + (Curtained(b) ? 2 : 0);
				return value2.Of[num] ?? value2.Of[num & 2] ?? value2.Of[0] ?? array;
			}
			catch
			{
				return array;
			}
		}

		private static bool DoorOpen(Building b)
		{
			return _now - HouseOccupancy.CrossedAt(b) < 2f;
		}

		private static bool Curtained(Building b)
		{
			return Curtains.DrawnOnMap(b, _day, _hour, _yourHome);
		}

		internal static float Glow(Building b, out Sprite light)
		{
			light = null;
			if (MapPeek.Hides(b))
			{
				return 0f;
			}
			float num = (HouseOccupancy.Lively(b) ? 1f : ((DoorOpen(b) && HouseOccupancy.Count(b) > 0) ? 0.6f : 0f));
			if (num <= 0f)
			{
				return 0f;
			}
			if (Curtained(b))
			{
				num *= 0.35f;
			}
			light = DynamicSprites.getBuildingLight(b);
			if (light == null)
			{
				light = BaseLight(b);
			}
			if (!(light != null))
			{
				return 0f;
			}
			return num;
		}

		private static Sprite BaseLight(Building b)
		{
			Sprite sprite = MapShapes.ShapeOf(b) ?? ((b.animData?.main != null && b.animData.main.Length != 0) ? b.animData.main[0] : null);
			if (sprite == null)
			{
				return null;
			}
			Sprite sprite2 = DynamicSpritesLibrary.building_lights.getSprite(sprite.GetHashCode());
			if (sprite2 != null)
			{
				return sprite2;
			}
			try
			{
				DynamicSprites.getRecoloredBuilding(sprite, b.kingdom?.getColor(), b.asset.atlas_asset);
			}
			catch
			{
				return null;
			}
			return DynamicSpritesLibrary.building_lights.getSprite(sprite.GetHashCode());
		}

		private static void MakeAsked(int most)
		{
			for (int i = 0; i < most; i++)
			{
				if (!_wanted.TryDequeue(out var result))
				{
					break;
				}
				try
				{
					_frames[result.Key] = Make(result.Key, result.Value);
				}
				catch (Exception ex)
				{
					_frames[result.Key] = new Frames
					{
						Base = result.Key
					};
					Log.Error("map houses: frames of " + result.Key.name + ": " + ex.Message);
				}
			}
		}

		private static Frames Make(Sprite s, BuildingAsset asset)
		{
			Frames frames = new Frames
			{
				Base = s
			};
			Rect textureRect = s.textureRect;
			int num = (int)textureRect.width;
			int num2 = (int)textureRect.height;
			int num3 = (int)textureRect.x;
			int num4 = (int)textureRect.y;
			Color32[] pixels = s.texture.GetPixels32();
			int width = s.texture.width;
			Color32[] array = new Color32[num * num2];
			for (int i = 0; i < num2; i++)
			{
				for (int j = 0; j < num; j++)
				{
					array[i * num + j] = pixels[(num4 + i) * width + num3 + j];
				}
			}
			bool flag = s.name == null || !s.name.StartsWith("main", StringComparison.Ordinal);
			MapSprites.Info info = MapSprites.Of(flag ? s.name : (asset.id + "/" + s.name), num, num2);
			if (info != null)
			{
				frames.Chimney = info.Chimney;
				frames.NoChimney = info.NoChimney;
			}
			bool flag2 = false;
			Color32[] array2 = array;
			for (int k = 0; k < array2.Length; k++)
			{
				if (IsLight(array2[k]))
				{
					flag2 = true;
					break;
				}
			}
			RectInt door = info?.Door ?? new RectInt(0, 0, 0, 0);
			bool flag3 = door.width > 0 && door.height > 0;
			frames.Of[0] = ((!flag) ? null : new Sprite[1] { s });
			if (flag3)
			{
				frames.Of[1] = new Sprite[1] { Frame(s, asset, Open(array, num, door), num, num2, "_fp_open") };
			}
			if (flag2)
			{
				frames.Of[2] = new Sprite[1] { Frame(s, asset, Curtain(array), num, num2, "_fp_curtained") };
				if (flag3)
				{
					frames.Of[3] = new Sprite[1] { Frame(s, asset, Curtain(Open(array, num, door)), num, num2, "_fp_open_curtained") };
				}
			}
			return frames;
		}

		private static bool IsLight(Color32 c)
		{
			if (c.a > 0 && c.r == WindowLight.r && c.g == WindowLight.g)
			{
				return c.b == WindowLight.b;
			}
			return false;
		}

		private static Color32[] Open(Color32[] px, int w, RectInt door)
		{
			Color32[] array = (Color32[])px.Clone();
			for (int i = door.yMin; i < door.yMax; i++)
			{
				for (int j = door.xMin; j < door.xMax; j++)
				{
					if (j >= 0 && j < w && i >= 0 && i * w + j < array.Length && array[i * w + j].a > 0)
					{
						array[i * w + j] = WindowLight;
					}
				}
			}
			return array;
		}

		private static Color32[] Curtain(Color32[] px)
		{
			Color32[] array = (Color32[])px.Clone();
			for (int i = 0; i < array.Length; i++)
			{
				if (IsLight(array[i]))
				{
					array[i] = Cloth;
				}
			}
			return array;
		}

		private static Sprite Frame(Sprite s, BuildingAsset asset, Color32[] px, int w, int h, string tag)
		{
			Texture2D texture2D = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);
			texture2D.filterMode = FilterMode.Point;
			texture2D.wrapMode = TextureWrapMode.Clamp;
			texture2D.name = s.name + tag;
			texture2D.SetPixels32(px);
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
			Sprite sprite = Sprite.Create(texture2D, new Rect(0f, 0f, w, h), new Vector2(s.pivot.x / (float)w, s.pivot.y / (float)h), s.pixelsPerUnit, 0u, SpriteMeshType.FullRect);
			sprite.name = s.name + tag;
			try
			{
				DynamicSpriteCreator.createBuildingShadow(asset, sprite, pIsContructionSprite: false);
			}
			catch
			{
			}
			_made++;
			return sprite;
		}

		internal static void ForTest_Reset()
		{
			_asked.Clear();
			KeyValuePair<Sprite, BuildingAsset> result;
			while (_wanted.TryDequeue(out result))
			{
			}
		}

		internal static bool ChimneyOf(Building b, out Vector2Int at)
		{
			Sprite sprite = MapShapes.ShapeOf(b) ?? ((b.animData?.main != null && b.animData.main.Length != 0) ? b.animData.main[0] : null);
			at = new Vector2Int(-1, -1);
			if (sprite == null || !_frames.TryGetValue(sprite, out var value))
			{
				return true;
			}
			at = value.Chimney;
			return !value.NoChimney;
		}
	}
}
