using System;
using System.Collections.Generic;

namespace FirstPerson
{
	internal static class MapSizes
	{
		internal struct Size
		{
			public string Id;

			public string Name;

			public int Blocks;

			public string Icon;
		}

		internal static readonly List<Size> Bigger = new List<Size>
		{
			new Size
			{
				Id = "fp_glacier",
				Name = "Glacier",
				Blocks = 12,
				Icon = "iconIceberg"
			},
			new Size
			{
				Id = "fp_continent",
				Name = "Continent",
				Blocks = 16,
				Icon = "iconIceberg"
			},
			new Size
			{
				Id = "fp_supercontinent",
				Name = "Supercontinent",
				Blocks = 24,
				Icon = "iconIceberg"
			},
			new Size
			{
				Id = "fp_planet",
				Name = "Planet",
				Blocks = 32,
				Icon = "iconIceberg"
			}
		};

		private static bool _done;

		internal static void Tick()
		{
			if (_done)
			{
				return;
			}
			MapSizeLibrary map_sizes;
			try
			{
				map_sizes = AssetManager.map_sizes;
			}
			catch
			{
				return;
			}
			if (map_sizes == null || map_sizes.list == null || !map_sizes.dict.ContainsKey("iceberg"))
			{
				return;
			}
			_done = true;
			try
			{
				foreach (Size item in Bigger)
				{
					Add(map_sizes, item);
				}
				map_sizes.convertToOldFormat();
				Log.Info("map sizes: " + string.Join(", ", MapSizeLibrary.getSizes()));
			}
			catch (Exception ex)
			{
				Log.Error("map sizes: could not add the bigger worlds: " + ex.Message);
			}
		}

		private static void Add(MapSizeLibrary lib, Size s)
		{
			if (!lib.dict.ContainsKey(s.Id))
			{
				lib.add(new MapSizeAsset
				{
					id = s.Id,
					size = s.Blocks,
					path_icon = s.Icon,
					show_warning = true
				});
				Lang.AddGameText("map_size_" + s.Id, s.Name);
			}
		}

		internal static string EnsureForTest(int blocks)
		{
			Tick();
			MapSizeLibrary map_sizes = AssetManager.map_sizes;
			foreach (MapSizeAsset item in map_sizes.list)
			{
				if (item.size == blocks)
				{
					return item.id;
				}
			}
			string text = "fp_test_" + blocks;
			map_sizes.add(new MapSizeAsset
			{
				id = text,
				size = blocks,
				path_icon = "iconIceberg",
				show_warning = true
			});
			Lang.AddGameText("map_size_" + text, blocks * 64 + " tiles");
			map_sizes.convertToOldFormat();
			return text;
		}
	}
}
