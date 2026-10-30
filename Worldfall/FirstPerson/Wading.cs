using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class Wading
	{
		private struct Wader
		{
			public Actor Who;

			public float X;

			public float Y;

			public float Speed;

			public float Radius;

			public bool Swims;
		}

		public const float OthersDepth = 0.6f;

		private const float OthersGive = 0.08f;

		private const float NearEdge = 0.08f;

		private static readonly ConcurrentDictionary<Actor, int> _nearEdge = new ConcurrentDictionary<Actor, int>();

		private static volatile int _edgeFrame;

		private const float YourShare = 0.65f;

		private const float YourShareWading = 0.72f;

		public static Actor You;

		public static float YourBody = 1f;

		private static bool _youWade;

		private static Actor _dryOf;

		private static float _dryBody;

		private static readonly Dictionary<Actor, float> Drawn = new Dictionary<Actor, float>();

		private static readonly List<Actor> DrawnGone = new List<Actor>();

		private static int _drawnFrame;

		public const int MostWakes = 16;

		private static readonly List<Wader> Noted = new List<Wader>();

		private static readonly Dictionary<Actor, float> NextSplash = new Dictionary<Actor, float>();

		internal static int NotedForTest;

		private static TerrainCache Land
		{
			get
			{
				if (ReliefWorld.Sampler == null || !ReliefWorld.Sampler.Ready)
				{
					return null;
				}
				return ReliefWorld.Sampler.Cache;
			}
		}

		public static bool YouWade => _youWade;

		private static bool OthersStand(Actor a, float depth)
		{
			if (depth < 0.52000004f)
			{
				return true;
			}
			int edgeFrame = _edgeFrame;
			if (depth < 0.6f)
			{
				_nearEdge[a] = edgeFrame;
				return true;
			}
			if (depth >= 0.68f)
			{
				return false;
			}
			if (_nearEdge.TryGetValue(a, out var value))
			{
				return edgeFrame - value < 30;
			}
			return false;
		}

		public static float DepthAt(TerrainCache land, float x, float y)
		{
			if (land == null || !land.HasSeabed || !land.IsWaterAt(x, y))
			{
				return float.NaN;
			}
			return land.LiquidSurface(x, y) - land.FloorHeight(x, y);
		}

		public static bool Frozen(WorldTile tile)
		{
			TileTypeBase tileTypeBase = tile?.Type;
			if (tileTypeBase != null)
			{
				return tileTypeBase.id == "ice";
			}
			return false;
		}

		public static bool Stands(Actor a)
		{
			if (a != null)
			{
				return Stands(a, a.current_position, a.current_tile);
			}
			return false;
		}

		public static bool Stands(Actor a, Vector2 p, WorldTile tile)
		{
			try
			{
				if (a == null || a.position_height > 0.3f || a.asset == null || a.asset.is_boat)
				{
					return false;
				}
				TileTypeBase tileTypeBase = tile?.Type;
				if (tileTypeBase == null || !tileTypeBase.liquid || tileTypeBase.lava)
				{
					return false;
				}
				if (a.isWaterCreature() || ((BaseSimObject)a).isFlying())
				{
					return false;
				}
				if (Frozen(tile))
				{
					return true;
				}
				float num = DepthAt(Land, p.x, p.y);
				if (float.IsNaN(num))
				{
					return false;
				}
				return (a == You) ? (num < (_youWade ? 0.72f : 0.65f) * YourBody) : OthersStand(a, num);
			}
			catch
			{
				return false;
			}
		}

		public static bool YourFloor(Actor you, Vector2 p, float body, out float floor)
		{
			You = you;
			YourBody = body;
			floor = 0f;
			WorldTile tile = null;
			try
			{
				tile = World.world.GetTile(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
			}
			catch
			{
			}
			_youWade = Stands(you, p, tile) && !DockDecks.On(p);
			if (!_youWade)
			{
				return false;
			}
			TerrainCache land = Land;
			if (land == null)
			{
				return false;
			}
			floor = (Frozen(tile) ? land.LiquidSurface(p.x, p.y) : Mathf.Min(land.SampleHeight(p.x, p.y), land.FloorHeight(p.x, p.y)));
			return true;
		}

		public static float DryBody(Actor you, float body, bool swimming)
		{
			bool flag = swimming || _youWade;
			if (you != _dryOf)
			{
				_dryOf = you;
				_dryBody = body;
			}
			else if (!flag)
			{
				_dryBody = body;
			}
			if (!flag)
			{
				return body;
			}
			return Mathf.Max(body, _dryBody);
		}

		public static float OthersPace(Actor a)
		{
			if (a == null || a == You || !Stands(a))
			{
				return 1f;
			}
			if (Frozen(a.current_tile))
			{
				return 0.95f;
			}
			Vector2 current_position = a.current_position;
			float num = DepthAt(Land, current_position.x, current_position.y);
			if (!float.IsNaN(num))
			{
				return Mathf.Lerp(0.85f, 0.55f, Mathf.Clamp01(num / 0.6f));
			}
			return 1f;
		}

		public static float EasedBottom(Actor a, float bottom, float dt)
		{
			int frameCount = Time.frameCount;
			if (frameCount - _drawnFrame > 600)
			{
				_drawnFrame = frameCount;
				DrawnGone.Clear();
				foreach (KeyValuePair<Actor, float> item in Drawn)
				{
					if (item.Key == null || !item.Key.isAlive())
					{
						DrawnGone.Add(item.Key);
					}
				}
				foreach (Actor item2 in DrawnGone)
				{
					Drawn.Remove(item2);
				}
			}
			if (!Drawn.TryGetValue(a, out var value) || Mathf.Abs(value - bottom) > 1.5f)
			{
				Drawn[a] = bottom;
				return bottom;
			}
			float num = Mathf.Lerp(value, bottom, 1f - Mathf.Exp(-6f * Mathf.Max(0f, dt)));
			Drawn[a] = num;
			return num;
		}

		public static void Note(Actor a, float x, float y, float speed, float body = 1f, bool swims = false)
		{
			if (Noted.Count < 256)
			{
				Noted.Add(new Wader
				{
					Who = a,
					X = x,
					Y = y,
					Speed = speed,
					Radius = Mathf.Clamp(body, 0.3f, 3f) * (swims ? 0.2f : 0.11f),
					Swims = swims
				});
			}
		}

		public static float DepthHere(float x, float y)
		{
			return DepthAt(Land, x, y);
		}

		public static float WaterTopHere(float x, float y)
		{
			return Land?.WaterTop(x, y) ?? float.NaN;
		}

		public static int Wakes(float[] wakes, Vector2 eye, float now, Action<Vector2, float> splash)
		{
			int num = 0;
			NotedForTest = Noted.Count;
			_edgeFrame++;
			if (_nearEdge.Count > 512)
			{
				_nearEdge.Clear();
			}
			if (Noted.Count > 0)
			{
				Noted.Sort((Wader p, Wader q) => ((p.X - eye.x) * (p.X - eye.x) + (p.Y - eye.y) * (p.Y - eye.y)).CompareTo((q.X - eye.x) * (q.X - eye.x) + (q.Y - eye.y) * (q.Y - eye.y)));
				foreach (Wader item in Noted)
				{
					float num2 = (item.X - eye.x) * (item.X - eye.x) + (item.Y - eye.y) * (item.Y - eye.y);
					if (num2 > 225f)
					{
						break;
					}
					bool flag = false;
					try
					{
						flag = Frozen(item.Who.current_tile);
					}
					catch
					{
					}
					if (!flag)
					{
						bool flag2 = item.Speed > 0.2f;
						if (num < 16 && (num + 1) * 4 <= wakes.Length)
						{
							wakes[num * 4] = item.X;
							wakes[num * 4 + 1] = item.Y;
							wakes[num * 4 + 2] = ((flag2 || item.Swims) ? 0.6f : 0.2f);
							wakes[num * 4 + 3] = item.Radius;
							num++;
						}
						if (flag2 && !item.Swims && item.Who != You && num2 < 100f && splash != null && (!NextSplash.TryGetValue(item.Who, out var value) || now >= value))
						{
							NextSplash[item.Who] = now + Mathf.Clamp(0.9f / Mathf.Max(0.3f, item.Speed), 0.25f, 0.8f);
							splash(new Vector2(item.X, item.Y), Mathf.Clamp01(item.Speed / 3f));
						}
					}
				}
			}
			Noted.Clear();
			if (NextSplash.Count > 64)
			{
				NextSplash.Clear();
			}
			return num;
		}

		public static bool Pace(Actor self, out float pace)
		{
			pace = 1f;
			if (self == null || self != You || !_youWade)
			{
				return false;
			}
			if (Frozen(self.current_tile))
			{
				pace = 0.95f;
				return true;
			}
			Vector2 current_position = self.current_position;
			float num = DepthAt(Land, current_position.x, current_position.y);
			if (float.IsNaN(num))
			{
				return false;
			}
			float t = Mathf.Clamp01(num / Mathf.Max(0.2f, 0.72f * YourBody));
			pace = Mathf.Lerp(0.85f, 0.55f, t);
			return true;
		}

		public static bool DrawnFloor(TerrainCache land, Actor a, float x, float y, out float floor)
		{
			floor = 0f;
			if (land == null || !land.HasSeabed || !land.IsWaterAt(x, y) || a == null || a.position_height > 0.3f)
			{
				return false;
			}
			if (a.asset != null && a.asset.is_boat)
			{
				return false;
			}
			if (Frozen(a.current_tile))
			{
				floor = land.LiquidSurface(x, y);
				return true;
			}
			floor = Mathf.Min(land.SampleHeight(x, y), land.FloorHeight(x, y));
			return true;
		}
	}
}
