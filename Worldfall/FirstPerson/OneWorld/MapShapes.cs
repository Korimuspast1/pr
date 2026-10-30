using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson.OneWorld
{
	internal static class MapShapes
	{
		private sealed class Known
		{
			public WorldTile Tile;

			public BuildingAsset Asset;

			public string Family;

			public bool Mirror;
		}

		internal static readonly HashSet<string> Approved = new HashSet<string>();

		private static readonly Dictionary<Building, Sprite> _shape = new Dictionary<Building, Sprite>();

		private static readonly Dictionary<Building, Known> _known = new Dictionary<Building, Known>();

		private static readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();

		private static readonly List<Building> _flipped = new List<Building>();

		private static readonly HashSet<Building> _seen = new HashSet<Building>();

		private static bool _wasFull;

		internal static int MirroredForTest;

		internal static int ShapedForTest;

		public static void Tick(bool full)
		{
			if (!full)
			{
				if (_wasFull)
				{
					Unflip();
				}
				_wasFull = false;
				_shape.Clear();
				return;
			}
			_wasFull = true;
			MirroredForTest = (ShapedForTest = 0);
			int num;
			Building[] visibleBuildings;
			try
			{
				num = World.world.buildings.countVisibleBuildings();
				visibleBuildings = World.world.buildings.getVisibleBuildings();
			}
			catch
			{
				return;
			}
			_seen.Clear();
			for (int i = 0; i < num; i++)
			{
				Building building = visibleBuildings[i];
				if (building == null || building.asset == null || building.asset.type != "type_house")
				{
					continue;
				}
				_seen.Add(building);
				if (!Models.Ready)
				{
					continue;
				}
				if (!_known.TryGetValue(building, out var value) || value.Tile != building.current_tile || value.Asset != building.asset)
				{
					if (_known.Count > 4096)
					{
						_known.Clear();
					}
					value = new Known
					{
						Tile = building.current_tile,
						Asset = building.asset
					};
					try
					{
						value.Family = Models.BuildingFamily(building);
						MeshModel meshModel = ((value.Family != null) ? Models.Get(value.Family) : null);
						value.Mirror = meshModel != null && meshModel.InfoFloat("footing", 0f) > 0f && Models.Mirrored(building);
					}
					catch
					{
					}
					_known[building] = value;
				}
				string family = value.Family;
				bool flag = value.Mirror && !building.isRuin() && !building.isUnderConstruction();
				if (building.flip_x != flag)
				{
					building.flip_x = flag;
					if (flag)
					{
						_flipped.Add(building);
					}
				}
				if (flag)
				{
					MirroredForTest++;
				}
				Sprite sprite = ((family != null) ? SpriteOf(family, building) : null);
				if (sprite != null)
				{
					_shape[building] = sprite;
					ShapedForTest++;
				}
				else
				{
					_shape.Remove(building);
				}
			}
			if (_shape.Count > 2048)
			{
				_shape.Clear();
			}
		}

		internal static Sprite ShapeOf(Building b)
		{
			if (b == null || !_shape.TryGetValue(b, out var value))
			{
				return null;
			}
			return value;
		}

		private static Sprite SpriteOf(string family, Building b)
		{
			if (!family.EndsWith("_b", StringComparison.Ordinal) && !family.EndsWith("_c", StringComparison.Ordinal))
			{
				return null;
			}
			string text = (family.Contains("_elf") ? "elf" : (family.Contains("_orc") ? "orc" : (family.Contains("_dwarf") ? "dwarf" : (family.Contains("_human") ? "human" : null))));
			if (text == null || !Approved.Contains(text))
			{
				return null;
			}
			if (_sprites.TryGetValue(family, out var value))
			{
				return value;
			}
			value = Load(family, b.asset);
			_sprites[family] = value;
			return value;
		}

		private static Sprite Load(string family, BuildingAsset asset)
		{
			try
			{
				using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FirstPerson.map_sprites." + family + ".png"))
				{
					if (stream == null)
					{
						return null;
					}
					byte[] array = new byte[stream.Length];
					stream.Read(array, 0, array.Length);
					Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false)
					{
						filterMode = FilterMode.Point,
						wrapMode = TextureWrapMode.Clamp,
						name = family
					};
					if (!texture2D.LoadImage(array, markNonReadable: false))
					{
						return null;
					}
					Sprite sprite = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.1f), 1f, 0u, SpriteMeshType.FullRect);
					sprite.name = family;
					try
					{
						DynamicSpriteCreator.createBuildingShadow(asset, sprite, pIsContructionSprite: false);
					}
					catch
					{
					}
					return sprite;
				}
			}
			catch (Exception ex)
			{
				Log.Error("map shapes: " + family + ": " + ex.Message);
				return null;
			}
		}

		private static void Unflip()
		{
			foreach (Building item in _flipped)
			{
				try
				{
					if (item != null && item.asset != null && item.asset.type == "type_house")
					{
						item.flip_x = false;
					}
				}
				catch
				{
				}
			}
			_flipped.Clear();
		}
	}
}
