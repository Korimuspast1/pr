using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using FirstPerson.Core;
using FirstPerson.Net;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson.Towns
{
	internal static class SiteLevel
	{
		private sealed class Site
		{
			public Building B;

			public long Id;

			public readonly List<int> Tiles = new List<int>();

			public readonly List<float> From = new List<float>();

			public readonly List<float> To = new List<float>();

			public float Done;
		}

		public const float SteepForTowns = 1f;

		private static readonly Dictionary<long, Site> Sites = new Dictionary<long, Site>();

		private static readonly HashSet<long> Seen = new HashSet<long>();

		private static readonly List<long> Gone = new List<long>();

		private static object _world;

		private static float _tick;

		internal static int Begun;

		internal static int TilesMoved;

		internal static int Refused;

		internal static bool HoldForTest;

		private const int GameCheckIlBytes = 37;

		private static bool _installed;

		internal static int Asked;

		private static long _askTicks;

		private static int _askedLogged;

		private static int _refusedLogged;

		private static float _logAt = 60f;

		public static bool Installed => _installed;

		private static bool Pad(Building b)
		{
			try
			{
				return b.asset == null || (b.asset.id ?? "").StartsWith("tent", StringComparison.Ordinal);
			}
			catch
			{
				return true;
			}
		}

		public static void Begin(Building b)
		{
			WorldSampler sampler = ReliefWorld.Sampler;
			if (b == null || sampler == null || !sampler.Ready || WorldSync.Mirroring)
			{
				return;
			}
			long id;
			try
			{
				id = b.data.id;
			}
			catch
			{
				return;
			}
			if (Sites.ContainsKey(id) || !Seen.Add(id) || Pad(b))
			{
				return;
			}
			WorldTile current_tile;
			try
			{
				current_tile = b.current_tile;
			}
			catch
			{
				return;
			}
			float num = Earthworks.WorkHeight(current_tile);
			if (float.IsNaN(num))
			{
				return;
			}
			Site site = new Site
			{
				B = b,
				Id = id
			};
			int width = sampler.Cache.Width;
			foreach (WorldTile item in new List<WorldTile>(Blueprints.Footprint(current_tile, b.asset)))
			{
				float num2 = Earthworks.EditWithinBand(item, num);
				if (!float.IsNaN(num2))
				{
					int num3 = item.y * width + item.x;
					float num4 = sampler.Relief.EditOf(num3);
					if (!(Mathf.Abs(num2 - num4) < 1f / 64f))
					{
						site.Tiles.Add(num3);
						site.From.Add(num4);
						site.To.Add(num2);
					}
				}
			}
			if (site.Tiles.Count != 0)
			{
				Sites[id] = site;
				Begun++;
			}
		}

		public static void Tick(float dt)
		{
			if ((_tick -= dt) > 0f || HoldForTest)
			{
				return;
			}
			_tick = 0.5f;
			WorldSampler sampler = ReliefWorld.Sampler;
			object obj = World.world?.map_stats;
			if (obj != _world)
			{
				_world = obj;
				Sites.Clear();
				Seen.Clear();
			}
			if (sampler == null || !sampler.Ready || WorldSync.Mirroring || WorldBoxMod.WorldLoading)
			{
				return;
			}
			LogAsks();
			try
			{
				foreach (City item in World.world.cities.list)
				{
					if (item != null && item.isAlive())
					{
						Building under_construction_building = item.under_construction_building;
						if (under_construction_building != null && under_construction_building.isAlive() && under_construction_building.isUnderConstruction())
						{
							Begin(under_construction_building);
						}
					}
				}
				Gone.Clear();
				foreach (Site value in Sites.Values)
				{
					Building b = value.B;
					bool flag = false;
					bool flag2 = false;
					try
					{
						flag = b != null && b.isAlive() && b.data.id == value.Id;
						flag2 = flag && b.isUnderConstruction();
					}
					catch
					{
					}
					if (!flag)
					{
						Gone.Add(value.Id);
						continue;
					}
					float num = (flag2 ? Mathf.Max(0.2f, Blueprints.ProgressOf(b)) : 1f);
					if (num > value.Done + 0.04f || num >= 1f)
					{
						value.Done = num;
						for (int i = 0; i < value.Tiles.Count; i++)
						{
							sampler.Relief.SetEdit(value.Tiles[i], Mathf.Lerp(value.From[i], value.To[i], num), basins: false);
							TilesMoved++;
						}
					}
					if (!flag2)
					{
						Gone.Add(value.Id);
					}
				}
				foreach (long item2 in Gone)
				{
					Sites.Remove(item2);
				}
			}
			catch (Exception e)
			{
				Hub.Fail("site level", e);
			}
		}

		public static float Unevenness(IEnumerable<WorldTile> footprint)
		{
			float num = float.MaxValue;
			float num2 = float.MinValue;
			int num3 = 0;
			foreach (WorldTile item in footprint)
			{
				float num4 = Earthworks.WorkHeight(item);
				if (!float.IsNaN(num4))
				{
					num = Mathf.Min(num, num4);
					num2 = Mathf.Max(num2, num4);
					num3++;
				}
			}
			if (num3 != 0)
			{
				return num2 - num;
			}
			return 0f;
		}

		public static float Drop(WorldTile at, BuildingAsset a)
		{
			if (ReliefWorld.Sampler == null || !ReliefWorld.Sampler.Ready || at == null || a == null)
			{
				return 0f;
			}
			return Unevenness(new List<WorldTile>(Blueprints.Footprint(at, a)));
		}

		private static float DropFast(WorldTile at, BuildingAsset a)
		{
			WorldSampler sampler = ReliefWorld.Sampler;
			if (sampler == null || !sampler.Ready || at == null || a == null)
			{
				return 0f;
			}
			TerrainCache cache = sampler.Cache;
			float[] ground = cache.Ground;
			int width = cache.Width;
			int height = cache.Height;
			BuildingFundament fundament = a.fundament;
			int val = at.x - (fundament?.left ?? 0);
			int val2 = at.x + (fundament?.right ?? 0);
			int val3 = at.y - (fundament?.bottom ?? 0);
			int val4 = at.y + (fundament?.top ?? 0);
			float num = float.MaxValue;
			float num2 = float.MinValue;
			for (int i = Math.Max(0, val3); i <= Math.Min(height - 1, val4); i++)
			{
				for (int j = Math.Max(0, val); j <= Math.Min(width - 1, val2); j++)
				{
					float num3 = ground[i * width + j];
					if (num3 < num)
					{
						num = num3;
					}
					if (num3 > num2)
					{
						num2 = num3;
					}
				}
			}
			if (!(num2 >= num))
			{
				return 0f;
			}
			return num2 - num;
		}

		public static void Install()
		{
			if (!_installed)
			{
				MethodInfo method = typeof(CityBehBuild).GetMethod("isGoodTileForBuilding", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[3]
				{
					typeof(WorldTile),
					typeof(BuildingAsset),
					typeof(City)
				}, null);
				int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num != 37)
				{
					throw new InvalidOperationException("the game's building-site check has changed (" + num + " bytes of IL, not " + 37 + ")");
				}
				Detour.Apply(method, typeof(SiteLevel).GetMethod("IsGoodTileForBuilding", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				_installed = true;
			}
		}

		private static bool IsGoodTileForBuilding(WorldTile pTile, BuildingAsset pAsset, City pCity)
		{
			if (!pTile.canBuildOn(pAsset))
			{
				return false;
			}
			if (!World.world.buildings.canBuildFrom(pTile, pAsset, pCity, BuildPlacingType.New, pFloraGrowth: false))
			{
				return false;
			}
			Asked++;
			long timestamp = Stopwatch.GetTimestamp();
			bool flag = false;
			try
			{
				flag = DropFast(pTile, pAsset) > 1f;
			}
			catch
			{
			}
			_askTicks += Stopwatch.GetTimestamp() - timestamp;
			if (flag)
			{
				Refused++;
				return false;
			}
			return true;
		}

		private static void LogAsks()
		{
			if (!(Time.unscaledTime < _logAt))
			{
				_logAt = Time.unscaledTime + 60f;
				int num = Asked - _askedLogged;
				int num2 = Refused - _refusedLogged;
				if (num != 0)
				{
					double num3 = (double)_askTicks * 1000.0 / (double)Stopwatch.Frequency;
					Log.Info($"site level: the towns asked of {num} sites in the last minute, {num2} too steep, {num3:0.00} ms in all; {Sites.Count} sites levelling");
					_askedLogged = Asked;
					_refusedLogged = Refused;
					_askTicks = 0L;
				}
			}
		}
	}
}
