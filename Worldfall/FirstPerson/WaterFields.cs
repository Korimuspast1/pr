using System;
using System.Collections.Generic;
using FirstPerson.Core;
using FirstPerson.Net;
using UnityEngine;

namespace FirstPerson
{
	internal static class WaterFields
	{
		public static float Boost = 0.25f;

		public const int Reach = 3;

		private static float _passed;

		private static float _at;

		private static readonly HashSet<int> _streams = new HashSet<int>();

		internal static int Boosted;

		public static void Tick(TerrainCache c)
		{
			if (NetSession.Joined || c == null || c.Width == 0)
			{
				return;
			}
			float b = 0f;
			try
			{
				b = (World.world.isPaused() ? 0f : World.world.elapsed);
			}
			catch
			{
			}
			_passed += Mathf.Max(0f, b);
			if (Time.unscaledTime < _at)
			{
				return;
			}
			_at = Time.unscaledTime + 2f;
			float num = _passed * Boost;
			_passed = 0f;
			if (num <= 0f)
			{
				return;
			}
			_streams.Clear();
			foreach (Lakes.Lake item in Lakes.Now)
			{
				if (item.Stream == null)
				{
					continue;
				}
				foreach (int item2 in item.Stream)
				{
					_streams.Add(item2);
				}
			}
			int num2 = 0;
			try
			{
				foreach (Building simple in World.world.buildings.getSimpleList())
				{
					if (simple != null && simple.asset != null && simple.asset.building_type == BuildingType.Building_Wheat && simple.isAlive())
					{
						Wheat component_wheat = simple.component_wheat;
						if (component_wheat != null && !component_wheat.isMaxLevel() && simple.current_tile != null && NearWater(c, simple.current_tile.x, simple.current_tile.y))
						{
							simple.data.grow_time += num;
							num2++;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("fields near water: " + ex.Message);
			}
			Boosted = num2;
		}

		private static bool NearWater(TerrainCache c, int x, int y)
		{
			int width = c.Width;
			int height = c.Height;
			for (int i = Math.Max(0, y - 3); i <= Math.Min(height - 1, y + 3); i++)
			{
				for (int j = Math.Max(0, x - 3); j <= Math.Min(width - 1, x + 3); j++)
				{
					int num = i * width + j;
					if ((c.Flags[num] & 1) != 0 || _streams.Contains(num))
					{
						return true;
					}
				}
			}
			return false;
		}
	}
}
