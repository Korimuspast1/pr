using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson.OneWorld
{
	internal static class MapSmoke
	{
		private const float Every = 2.6f;

		private static readonly Dictionary<Building, float> _next = new Dictionary<Building, float>();

		private static readonly List<Building> _gone = new List<Building>();

		private static float _forget;

		internal static int PuffsForTest;

		internal static int SmokingForTest;

		public static void Tick()
		{
			GlowParticles glowParticles = null;
			try
			{
				glowParticles = World.world.particles_smoke;
			}
			catch
			{
			}
			if (glowParticles == null)
			{
				return;
			}
			float unscaledTime = Time.unscaledTime;
			bool effectsPaused = GameClock.EffectsPaused;
			int num = World.world.buildings.countVisibleBuildings();
			Building[] visibleBuildings = World.world.buildings.getVisibleBuildings();
			SmokingForTest = 0;
			for (int i = 0; i < num; i++)
			{
				Building building = visibleBuildings[i];
				if (building == null || building.asset == null || building.asset.type != "type_house" || !building.asset.id.StartsWith("house_", StringComparison.Ordinal) || !building.isUsable() || building.isAbandoned() || building.isUnderConstruction() || HouseOccupancy.Count(building) == 0 || !MapHouses.ChimneyOf(building, out var at))
				{
					continue;
				}
				SmokingForTest++;
				if (!_next.TryGetValue(building, out var value))
				{
					_next[building] = unscaledTime + UnityEngine.Random.Range(0f, 2.6f);
				}
				else
				{
					if (unscaledTime < value)
					{
						continue;
					}
					_next[building] = unscaledTime + 2.6f * UnityEngine.Random.Range(0.8f, 1.25f);
					if (effectsPaused)
					{
						continue;
					}
					try
					{
						if (glowParticles.particles != null && glowParticles.particles.particleCount > 30)
						{
							continue;
						}
					}
					catch
					{
					}
					Vector2 vector = Chimney(building, at);
					glowParticles.spawn(vector.x, vector.y, pRemoveCooldown: true);
					PuffsForTest++;
				}
			}
			if (!(unscaledTime > _forget))
			{
				return;
			}
			_forget = unscaledTime + 10f;
			_gone.Clear();
			foreach (KeyValuePair<Building, float> item in _next)
			{
				if (item.Key == null || !item.Key.isAlive() || unscaledTime - item.Value > 30f)
				{
					_gone.Add(item.Key);
				}
			}
			foreach (Building item2 in _gone)
			{
				_next.Remove(item2);
			}
		}

		private static Vector2 Chimney(Building b, Vector2Int c)
		{
			Vector3 cur_transform_position = ((BaseSimObject)b).cur_transform_position;
			Sprite sprite = MapShapes.ShapeOf(b) ?? ((b.animData?.main != null && b.animData.main.Length != 0) ? b.animData.main[0] : null);
			if (sprite == null)
			{
				return new Vector2(cur_transform_position.x, cur_transform_position.y + 1f);
			}
			float y = b.getCurrentScale().y;
			Rect rect = sprite.rect;
			if (c.x < 0)
			{
				c = new Vector2Int((int)(rect.width * 0.7f), (int)rect.height - 2);
			}
			float num = ((float)c.x + 0.5f - sprite.pivot.x) * y;
			float num2 = ((float)c.y + 1f - sprite.pivot.y) * y;
			return new Vector2(cur_transform_position.x + (b.flip_x ? (0f - num) : num), cur_transform_position.y + num2);
		}
	}
}
