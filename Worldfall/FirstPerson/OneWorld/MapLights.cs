using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace FirstPerson.OneWorld
{
	internal static class MapLights
	{
		private static QuantumSpriteUpdater _windows;

		private static QuantumSpriteUpdater _areas;

		private static bool _hooked;

		private static bool _failed;

		private static readonly List<BuildingAsset> _houseAssets = new List<BuildingAsset>();

		private static readonly List<bool> _pools = new List<bool>();

		internal static float DrawMs;

		private static readonly Stopwatch Clock = new Stopwatch();

		internal static int LitForTest;

		internal static int DimForTest;

		internal static int DarkForTest;

		private static bool _drew;

		public static void Hook()
		{
			if (_hooked)
			{
				return;
			}
			QuantumSpriteAsset quantumSpriteAsset;
			QuantumSpriteAsset quantumSpriteAsset2;
			try
			{
				quantumSpriteAsset = AssetManager.quantum_sprites?.get("buildings_light_windows");
				quantumSpriteAsset2 = AssetManager.quantum_sprites?.get("light_areas");
			}
			catch
			{
				return;
			}
			if (quantumSpriteAsset == null || quantumSpriteAsset.draw_call == null || quantumSpriteAsset2 == null || quantumSpriteAsset2.draw_call == null)
			{
				return;
			}
			_hooked = true;
			try
			{
				_windows = quantumSpriteAsset.draw_call;
				quantumSpriteAsset.draw_call = Windows;
				_areas = quantumSpriteAsset2.draw_call;
				quantumSpriteAsset2.draw_call = Areas;
				foreach (BuildingAsset item in AssetManager.buildings.list)
				{
					if (item != null && item.type == "type_house")
					{
						_houseAssets.Add(item);
					}
				}
				Log.Info($"map houses: lit windows and house lights follow who is in ({_houseAssets.Count} house kinds)");
			}
			catch (Exception ex)
			{
				Log.Error("map houses: lights not hooked: " + ex.Message);
			}
		}

		private static void Windows(QuantumSpriteAsset asset)
		{
			if (!MapHouses.On || _failed)
			{
				if (_drew)
				{
					_drew = false;
					Reset(asset);
				}
				_windows(asset);
				return;
			}
			_drew = true;
			Clock.Restart();
			try
			{
				DrawWindows(asset);
				DrawMs += ((float)Clock.Elapsed.TotalMilliseconds - DrawMs) * 0.05f;
			}
			catch (Exception ex)
			{
				_failed = true;
				Log.Error("map houses: lit windows failed, the game's own from now: " + ex);
			}
		}

		private static void Reset(QuantumSpriteAsset asset)
		{
			try
			{
				Color pColor = Color.white;
				QuantumSprite[] all = asset.group_system.getAll();
				foreach (QuantumSprite quantumSprite in all)
				{
					if (!(quantumSprite == null))
					{
						quantumSprite.setColor(ref pColor);
						quantumSprite.setFlipX(pFlipX: false);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("map houses: the window lights put back: " + ex.Message);
			}
		}

		private static void DrawWindows(QuantumSpriteAsset asset)
		{
			LitForTest = (DimForTest = (DarkForTest = 0));
			if (!World.world.quality_changer.shouldRenderBuildings() || !World.world.era_manager.shouldShowLights() || !PlayerConfig.optionBoolEnabled("night_lights"))
			{
				return;
			}
			Randy.randomBool();
			int num = World.world.buildings.countVisibleBuildings();
			Building[] visibleBuildings = World.world.buildings.getVisibleBuildings();
			for (int i = 0; i < num; i++)
			{
				Building building = visibleBuildings[i];
				if (building == null || !building.asset.city_building || !building.isUsable() || building.isAbandoned())
				{
					continue;
				}
				float num2 = 1f;
				Sprite light;
				if (building.asset.hasHousingSlots() && building.asset.type == "type_house")
				{
					num2 = MapHouses.Glow(building, out light);
					if (num2 <= 0f)
					{
						DarkForTest++;
						continue;
					}
					if (num2 < 1f)
					{
						DimForTest++;
					}
					else
					{
						LitForTest++;
					}
				}
				else
				{
					light = DynamicSprites.getBuildingLight(building);
				}
				if (!(light == null))
				{
					Vector3 cur_transform_position = ((BaseSimObject)building).cur_transform_position;
					cur_transform_position.z = -0.19f;
					QuantumSprite quantumSprite = QuantumSpriteLibrary.drawQuantumSprite(asset, cur_transform_position, (WorldTile)null, (Kingdom)null, (City)null, (BattleContainer)null, 1f, pSetColor: false, building.getCurrentScale().y);
					quantumSprite.setSprite(light);
					Color pColor = new Color(1f, 1f, 1f, num2);
					quantumSprite.setColor(ref pColor);
					quantumSprite.setFlipX(building.flip_x);
				}
			}
		}

		private static void Areas(QuantumSpriteAsset asset)
		{
			if (!MapHouses.On || _failed)
			{
				_areas(asset);
				return;
			}
			_pools.Clear();
			foreach (BuildingAsset houseAsset in _houseAssets)
			{
				_pools.Add(houseAsset.draw_light_area);
				houseAsset.draw_light_area = false;
			}
			try
			{
				_areas(asset);
			}
			finally
			{
				for (int i = 0; i < _houseAssets.Count; i++)
				{
					_houseAssets[i].draw_light_area = _pools[i];
				}
			}
			try
			{
				if (!PlayerConfig.optionBoolEnabled("night_lights") || !World.world.era_manager.shouldShowLights() || !World.world.quality_changer.shouldRenderBuildings())
				{
					return;
				}
				Color colorForLight = QuantumSpriteLibrary.getColorForLight();
				int num = World.world.buildings.countVisibleBuildings();
				Building[] visibleBuildings = World.world.buildings.getVisibleBuildings();
				for (int j = 0; j < num; j++)
				{
					Building building = visibleBuildings[j];
					if (building != null && !(building.asset.type != "type_house") && building.asset.draw_light_area && building.isUsable() && !building.isAbandoned())
					{
						Sprite light;
						float num2 = MapHouses.Glow(building, out light);
						if (!(num2 <= 0f))
						{
							Vector2 current_position = building.current_position;
							current_position.x += building.asset.draw_light_area_offset_x;
							current_position.y += building.asset.draw_light_area_offset_y;
							QuantumSpriteLibrary.showLightAt(current_position, colorForLight, building.asset.draw_light_size * ((num2 < 1f) ? 0.6f : 1f));
						}
					}
				}
			}
			catch (Exception ex)
			{
				_failed = true;
				Log.Error("map houses: house lights failed, the game's own from now: " + ex);
			}
		}
	}
}
