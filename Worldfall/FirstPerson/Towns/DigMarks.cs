using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal static class DigMarks
	{
		private static readonly uint Patch = Tint(236, 214, 170, 120);

		private static readonly uint Level = Tint(150, 220, 140, 120);

		private static readonly List<MeshInstance> Tiles = new List<MeshInstance>();

		private static MeshModel _tile;

		private static uint Tint(int r, int g, int b, int a)
		{
			return (TerrainCache.Rgb(r, g, b) & 0xFFFFFF) | (uint)(a << 24);
		}

		public static void Collect(TownScene s)
		{
			if (s.Host == null || Earthworks.ToolOf(s.Host) == null)
			{
				return;
			}
			int num = 0;
			uint tint = (Earthworks.PreviewLevel ? Level : Patch);
			foreach (var (t, num2) in Earthworks.Preview)
			{
				if (num >= 40)
				{
					break;
				}
				Put(s, num++, t, tint, 0.55f + 0.3f * num2);
			}
		}

		private static void Put(TownScene s, int i, WorldTile t, uint tint, float fade)
		{
			if (t == null)
			{
				return;
			}
			if (_tile == null)
			{
				MeshBuilder meshBuilder = new MeshBuilder("dig_tile");
				int slot = meshBuilder.Slot("tile", 0, TerrainCache.Rgb(240, 240, 230), 0);
				meshBuilder.Part("tile", -1, new Vec3(0f, 0f, 0f));
				meshBuilder.Box(slot, new Vec3(0f, 0f, 0.008f), 0.07f, 0.07f, 0.008f);
				_tile = meshBuilder.Build();
			}
			float x = (float)t.x + 0.5f;
			float y = (float)t.y + 0.5f;
			if (s.InView(x, y, 1f))
			{
				while (Tiles.Count <= i)
				{
					Tiles.Add(new MeshInstance
					{
						NoShadow = true
					});
				}
				MeshInstance meshInstance = Tiles[i];
				meshInstance.Model = _tile;
				meshInstance.Fade = fade;
				meshInstance.Flash = tint;
				meshInstance.NoShadow = true;
				meshInstance.SortDepth = Mathf.Sqrt(s.Dist2(x, y));
				meshInstance.Place(x, y, s.Ground(x, y) + 0.02f, -MathF.PI / 2f, 1f, 1f, 1f);
				s.Meshes.Add(meshInstance);
			}
		}
	}
}
