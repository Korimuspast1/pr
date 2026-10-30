using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal static class PlanGhosts
	{
		private static readonly uint Green = (TerrainCache.Rgb(90, 230, 110) & 0xFFFFFF) | 0x96000000u;

		private static readonly uint Red = (TerrainCache.Rgb(240, 70, 60) & 0xFFFFFF) | 0x96000000u;

		private static readonly uint Pale = (TerrainCache.Rgb(205, 225, 255) & 0xFFFFFF) | 0x6E000000;

		private static readonly MeshInstance Ghost = new MeshInstance
		{
			NoShadow = true
		};

		private static readonly List<MeshInstance> Planned = new List<MeshInstance>();

		private static readonly List<MeshInstance> RowGhosts = new List<MeshInstance>();

		private static MeshModel _tile;

		private static MeshModel _post;

		private static readonly List<MeshInstance> Tiles = new List<MeshInstance>();

		public static void Collect(TownScene s)
		{
			if (!Planning.Begun)
			{
				WallMarks(s);
				DigMarks.Collect(s);
				return;
			}
			WorldTile siteTile = Planning.SiteTile;
			if (Planning.Piece != null && Planning.Piece.Wall >= 0 && !Planning.Founding)
			{
				int num = 0;
				if (Planning.Dragging)
				{
					for (int i = 0; i < Planning.Row.Count; i++)
					{
						PutTile(s, num++, Planning.Row[i], Planning.RowOk[i] ? Green : Red, 0.6f, 0.35f);
					}
				}
				else if (siteTile != null)
				{
					PutTile(s, num++, siteTile, Planning.Valid ? Green : Red, 0.6f, 0.35f);
				}
				{
					foreach (WallPlans.Plan item in WallPlans.Of(Planning.Town))
					{
						if (num >= 300)
						{
							break;
						}
						PutTile(s, num++, World.world.GetTile(item.X, item.Y), Pale, 0.4f, 0.35f);
					}
					return;
				}
			}
			if (Planning.Piece != null && Planning.Piece.Street && !Planning.Founding)
			{
				int num2 = 0;
				if (Planning.Dragging)
				{
					for (int j = 0; j < Planning.Row.Count; j++)
					{
						PutTile(s, num2++, Planning.Row[j], Planning.RowOk[j] ? Green : Red, 0.55f);
					}
				}
				else if (siteTile != null)
				{
					PutTile(s, num2++, siteTile, Planning.Valid ? Green : Red, 0.55f);
				}
				City town = Planning.Town;
				if (town == null || town.road_tiles_to_build == null)
				{
					return;
				}
				for (int k = 0; k < town.road_tiles_to_build.Count; k++)
				{
					if (num2 >= 300)
					{
						break;
					}
					PutTile(s, num2++, town.road_tiles_to_build[k], Pale, 0.35f);
				}
				return;
			}
			if (Planning.Dragging)
			{
				for (int l = 0; l < Planning.Row.Count; l++)
				{
					while (RowGhosts.Count <= l)
					{
						RowGhosts.Add(new MeshInstance
						{
							NoShadow = true
						});
					}
					Put(s, RowGhosts[l], Planning.Asset, Planning.Row[l], Planning.RowOk[l] ? Green : Red, 0.5f, Planning.Face, Planning.DockTurn);
				}
			}
			else if (siteTile != null)
			{
				Put(s, Ghost, Planning.Asset, siteTile, Planning.Valid ? Green : Red, Planning.Nowhere ? 0.16f : 0.55f, Planning.Face, Planning.DockTurn);
			}
			if (Planning.Founding || Planning.Town == null)
			{
				return;
			}
			int num3 = 0;
			foreach (Building item2 in Blueprints.SitesOf(Planning.Town))
			{
				if (!Blueprints.IsPaid(item2) && item2.current_tile != null)
				{
					if (num3 >= Planned.Count)
					{
						Planned.Add(new MeshInstance
						{
							NoShadow = true
						});
					}
					Put(s, Planned[num3++], item2.asset, item2.current_tile, Pale, 0.3f, Facing.Of(item2), Facing.DockTurn(item2, out var turns) ? turns : (-1));
				}
			}
		}

		private static void WallMarks(TownScene s)
		{
			City here = Hub.Here;
			if (here == null || s.Host == null)
			{
				return;
			}
			List<WallPlans.Plan> list = WallPlans.Of(here);
			if (list.Count == 0)
			{
				return;
			}
			Vector2 current_position = s.Host.current_position;
			int num = 0;
			foreach (WallPlans.Plan item in list)
			{
				if (num >= 120)
				{
					break;
				}
				if (!((new Vector2((float)item.X + 0.5f, (float)item.Y + 0.5f) - current_position).sqrMagnitude > 676f))
				{
					PutTile(s, num++, World.world.GetTile(item.X, item.Y), Pale, 0.35f, 0.3f);
				}
			}
		}

		private static void PutTile(TownScene s, int i, WorldTile t, uint tint, float fade, float height = 0f)
		{
			if (t == null)
			{
				return;
			}
			if (_post == null)
			{
				MeshBuilder meshBuilder = new MeshBuilder("plan_post");
				int slot = meshBuilder.Slot("post", 0, TerrainCache.Rgb(240, 240, 230), 0);
				meshBuilder.Part("post", -1, new Vec3(0f, 0f, 0f));
				meshBuilder.Box(slot, new Vec3(0f, 0f, 0.5f), 0.4f, 0.4f, 0.5f);
				_post = meshBuilder.Build();
			}
			if (_tile == null)
			{
				MeshBuilder meshBuilder2 = new MeshBuilder("plan_tile");
				int slot2 = meshBuilder2.Slot("tile", 0, TerrainCache.Rgb(240, 240, 230), 0);
				meshBuilder2.Part("tile", -1, new Vec3(0f, 0f, 0f));
				meshBuilder2.Box(slot2, new Vec3(0f, 0f, 0.015f), 0.46f, 0.46f, 0.015f);
				_tile = meshBuilder2.Build();
			}
			float x = t.posV3.x + 0.5f;
			float y = t.posV3.y + 0.5f;
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
				meshInstance.Model = ((height > 0f) ? _post : _tile);
				meshInstance.Fade = fade;
				meshInstance.Flash = tint;
				meshInstance.NoShadow = true;
				meshInstance.SortDepth = Mathf.Sqrt(s.Dist2(x, y));
				float sz = ((height > 0f) ? height : 1f);
				meshInstance.Place(x, y, s.Ground(x, y) + 0.02f, -MathF.PI / 2f, 1f, 1f, sz);
				s.Meshes.Add(meshInstance);
			}
		}

		private static void Put(TownScene s, MeshInstance inst, BuildingAsset a, WorldTile t, uint tint, float fade, int face, int dockTurn = -1)
		{
			if (a == null || t == null)
			{
				return;
			}
			string text = null;
			try
			{
				text = Models.BuildingFamily(a);
			}
			catch
			{
			}
			MeshModel meshModel = ((text != null) ? Models.Get(text) : null) ?? Models.Get(a.id);
			if (meshModel == null)
			{
				return;
			}
			float num = WidthOf(a, s.Sprites);
			float num2 = num / Mathf.Max(0.5f, meshModel.InfoFloat("width", meshModel.Max.X - meshModel.Min.X));
			float x = t.posV3.x;
			float num3 = t.posV3.y;
			try
			{
				if (a.fundament != null)
				{
					num3 += (float)(a.fundament.top - a.fundament.bottom) * 0.5f;
				}
			}
			catch
			{
			}
			if (s.InView(x, num3, num))
			{
				inst.Model = meshModel;
				inst.Fade = fade;
				inst.Flash = tint;
				inst.NoShadow = true;
				inst.SortDepth = Mathf.Sqrt(s.Dist2(x, num3));
				bool flag = meshModel.InfoFloat("footing", 0f) > 0f && HouseShapes.Mirrored(t.x, t.y);
				float heading = Facing.Heading(face);
				float num4 = s.Ground(x, num3);
				if (a.docks)
				{
					heading = ((dockTurn >= 0) ? Facing.Heading(dockTurn) : DockFacing.TowardsWater(x, num3));
					num4 = DockFacing.Ground(s.Terrain, x, num3);
				}
				inst.Place(x, num3, num4 - 0.05f, heading, flag ? (0f - num2) : num2, num2, num2);
				s.Meshes.Add(inst);
			}
		}

		internal static float WidthOf(BuildingAsset a, SpriteCache sprites)
		{
			try
			{
				if (a.building_sprites != null && a.building_sprites.animation_data != null && a.building_sprites.animation_data.Count > 0)
				{
					BuildingAnimationData buildingAnimationData = a.building_sprites.animation_data[0];
					if (buildingAnimationData != null && buildingAnimationData.main != null && buildingAnimationData.main.Length != 0 && buildingAnimationData.main[0] != null)
					{
						SpriteImage spriteImage = sprites.Get(buildingAnimationData.main[0]);
						if (spriteImage != null)
						{
							return spriteImage.UnitWidth * Mathf.Abs(a.scale_base.x);
						}
					}
				}
				if (a.fundament != null)
				{
					return a.fundament.width;
				}
			}
			catch
			{
			}
			return 3f;
		}

		internal static Sprite SpriteOf(BuildingAsset a)
		{
			try
			{
				if (a?.building_sprites?.animation_data != null && a.building_sprites.animation_data.Count > 0)
				{
					BuildingAnimationData buildingAnimationData = a.building_sprites.animation_data[0];
					if (buildingAnimationData?.main != null && buildingAnimationData.main.Length != 0)
					{
						return buildingAnimationData.main[0];
					}
				}
			}
			catch
			{
			}
			return null;
		}
	}
}
