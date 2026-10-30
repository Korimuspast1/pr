using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal static class TownLights
	{
		private const int RoadsPerLight = 9;

		private const int MostPerTown = 24;

		private const float Spacing = 6f;

		private const float Every = 4f;

		private const float Between = 60f;

		private static float _timer;

		private static int _cursor;

		private static object _world;

		private static readonly Dictionary<long, float> _lastAt = new Dictionary<long, float>();

		private static readonly List<WorldTile> _roads = new List<WorldTile>();

		internal static int PutUpForTest;

		internal static bool HurryForTest;

		internal static string WhyNoneForTest;

		private static readonly int[] Dx = new int[4] { 1, -1, 0, 0 };

		private static readonly int[] Dy = new int[4] { 0, 0, 1, -1 };

		public static void Tick(float dt)
		{
			object obj = null;
			try
			{
				obj = World.world?.map_stats;
			}
			catch
			{
			}
			if (obj != _world)
			{
				_world = obj;
				_lastAt.Clear();
				_cursor = 0;
			}
			try
			{
				if (World.world == null || World.world.isPaused())
				{
					return;
				}
			}
			catch
			{
				return;
			}
			_timer -= dt;
			if (_timer > 0f && !HurryForTest)
			{
				return;
			}
			_timer = 4f;
			List<City> list;
			try
			{
				list = World.world.cities.list;
			}
			catch
			{
				return;
			}
			if (list == null || list.Count == 0)
			{
				return;
			}
			_cursor = (_cursor + 1) % list.Count;
			City c = list[_cursor];
			try
			{
				Look(c);
			}
			catch (Exception e)
			{
				Hub.Fail("town lights", e);
			}
		}

		internal static bool LookForTest(City c)
		{
			try
			{
				_lastAt.Remove(c.data.id);
			}
			catch
			{
			}
			int putUpForTest = PutUpForTest;
			Look(c);
			return PutUpForTest > putUpForTest;
		}

		private static void Look(City c)
		{
			WhyNoneForTest = null;
			if (c == null || !c.isAlive())
			{
				WhyNoneForTest = "no town";
				return;
			}
			if (Village.PlansAlone(c))
			{
				WhyNoneForTest = "a village you plan yourself";
				return;
			}
			if (!c.hasStorages())
			{
				WhyNoneForTest = "no stores";
				return;
			}
			long id = c.data.id;
			float unscaledTime = Time.unscaledTime;
			if (_lastAt.TryGetValue(id, out var value) && unscaledTime - value < 60f)
			{
				return;
			}
			PlacedLights.Track();
			int num = 0;
			foreach (PlacedLights.Light item in PlacedLights.All)
			{
				if (item.Owner < 0 && item.City == id)
				{
					num++;
				}
			}
			Roads(c);
			int num2 = Mathf.Min(_roads.Count / 9, 24);
			if (num >= num2)
			{
				WhyNoneForTest = $"enough: {num} for {_roads.Count} road tiles";
				return;
			}
			PlacedLights.Kind kind = KindFor(c);
			if (!Afford(c, ref kind))
			{
				WhyNoneForTest = "can't pay";
				return;
			}
			if (!Spot(c, kind, out var at, out var heading))
			{
				WhyNoneForTest = "nowhere beside its " + _roads.Count + " road tiles";
				return;
			}
			Pay(c, kind);
			PlacedLights.Add(null, kind, at, heading, null, c);
			_lastAt[id] = unscaledTime;
			PutUpForTest++;
			Log.Info($"lights: {Rep.NameOf(c)} put up a {PlacedLights.Names[(int)kind].ToLowerInvariant()} at {at.x:F1},{at.y:F1} ({num + 1} of {num2})");
		}

		internal static PlacedLights.Kind KindFor(City c)
		{
			string text = null;
			try
			{
				text = c.getSpecies();
			}
			catch
			{
			}
			if (text == null)
			{
				return PlacedLights.Kind.Torch;
			}
			if (text.Contains("human") || text.Contains("elf"))
			{
				return PlacedLights.Kind.Lamp;
			}
			if (text.Contains("dwarf"))
			{
				return PlacedLights.Kind.Brazier;
			}
			return PlacedLights.Kind.Torch;
		}

		private static int Has(City c, string res)
		{
			try
			{
				return c.getResourcesAmount(res);
			}
			catch
			{
				return 0;
			}
		}

		private static bool Afford(City c, ref PlacedLights.Kind kind)
		{
			Supplies.Kind kind2 = Supplies.Of(PlacedLights.ItemOf(kind));
			if (kind2 != null && Has(c, kind2.Res1) >= kind2.Amount1 && (kind2.Res2 == null || Has(c, kind2.Res2) >= kind2.Amount2))
			{
				return true;
			}
			kind = PlacedLights.Kind.Torch;
			return Has(c, "wood") >= 1;
		}

		private static void Pay(City c, PlacedLights.Kind kind)
		{
			Supplies.Kind kind2 = Supplies.Of(PlacedLights.ItemOf(kind));
			if (kind2 == null)
			{
				return;
			}
			try
			{
				c.takeResource(kind2.Res1, kind2.Amount1);
				if (kind2.Res2 != null)
				{
					c.takeResource(kind2.Res2, kind2.Amount2);
				}
			}
			catch
			{
			}
		}

		private static void Roads(City c)
		{
			_roads.Clear();
			foreach (TileZone zone in c.zones)
			{
				if (zone?.tiles == null)
				{
					continue;
				}
				WorldTile[] tiles = zone.tiles;
				foreach (WorldTile worldTile in tiles)
				{
					if (worldTile != null && worldTile.Type != null && worldTile.Type.road)
					{
						_roads.Add(worldTile);
					}
				}
			}
		}

		private static bool Spot(City c, PlacedLights.Kind kind, out Vector2 at, out float heading)
		{
			at = default(Vector2);
			heading = 0f;
			if (_roads.Count == 0)
			{
				return false;
			}
			Vector2 vector = _roads[0].posV3;
			try
			{
				vector = Shops.Centre(c);
			}
			catch
			{
			}
			float num = float.MinValue;
			bool flag = false;
			int num2 = Mathf.Max(1, _roads.Count / 300);
			for (int i = 0; i < _roads.Count; i += num2)
			{
				WorldTile worldTile = _roads[i];
				for (int j = 0; j < 4; j++)
				{
					WorldTile tile = World.world.GetTile(worldTile.x + Dx[j], worldTile.y + Dy[j]);
					if (tile == null || tile.Type == null || tile.Type.road || tile.zone == null || tile.zone.city != c)
					{
						continue;
					}
					Vector2 vector2 = new Vector2((float)tile.x + 0.5f - (float)Dx[j] * 0.3f, (float)tile.y + 0.5f - (float)Dy[j] * 0.3f);
					if (PlacedLights.WhyNotAt(vector2, kind) != null)
					{
						continue;
					}
					float num3 = Nearest(vector2);
					if (!(num3 < 6f))
					{
						float num4 = Mathf.Min(num3, 30f) - 0.02f * (vector2 - vector).magnitude;
						if (!flag || !(num4 <= num))
						{
							flag = true;
							num = num4;
							at = vector2;
							heading = Mathf.Atan2(-Dy[j], -Dx[j]);
						}
					}
				}
			}
			return flag;
		}

		private static float Nearest(Vector2 p)
		{
			float num = float.MaxValue;
			foreach (PlacedLights.Light item in PlacedLights.All)
			{
				float num2 = item.X - p.x;
				float num3 = item.Y - p.y;
				float num4 = num2 * num2 + num3 * num3;
				if (num4 < num)
				{
					num = num4;
				}
			}
			if (num != float.MaxValue)
			{
				return Mathf.Sqrt(num);
			}
			return 1000000f;
		}
	}
}
