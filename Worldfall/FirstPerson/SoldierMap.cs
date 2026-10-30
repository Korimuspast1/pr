using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class SoldierMap
	{
		public struct Dot
		{
			public Actor Actor;

			public Vector2 P;

			public Color Color;

			public bool Ours;

			public bool Army;

			public bool Captain;

			public bool King;

			public bool Fighting;

			public bool Settler;
		}

		public struct Town
		{
			public City City;

			public Vector2 P;

			public string Name;

			public Kingdom Owner;

			public bool Ours;

			public bool Home;

			public bool Target;

			public bool Capital;
		}

		public bool Open;

		public bool Founder;

		public bool Captaincy;

		public Order Order;

		public Commission Commission;

		public Actor Captain;

		public readonly List<Dot> Dots = new List<Dot>(256);

		public readonly List<Town> Towns = new List<Town>();

		public readonly List<Vector2> Clashes = new List<Vector2>();

		public readonly List<TileZone> Sites = new List<TileZone>();

		public int Foes;

		public int Ours;

		public float Span = 64f;

		public float Zoom = 1f;

		public Vector2 Centre;

		private bool _fit;

		private float _refresh;

		public const int ZoneSize = 8;

		private readonly Dictionary<long, Vector2> _cellSum = new Dictionary<long, Vector2>();

		private readonly Dictionary<long, int> _cellCount = new Dictionary<long, int>();

		public float ZoomMin => Span / Mathf.Max(Span, Mathf.Max(WarMap.WorldWidth, WarMap.WorldHeight) * 1.05f);

		public float ZoomMax => Mathf.Max(1f, Span / 12f);

		public void Toggle(Actor host, Action<string> toast)
		{
			if (Open)
			{
				Open = false;
				return;
			}
			bool flag = Service.IsSoldier(host);
			bool flag2 = Service.CommissionOf(host) != null;
			if (!flag && !flag2)
			{
				toast("Only kings keep a war map. Enlist as a soldier or a founder (ask your town's leader) and your orders will show here.");
				return;
			}
			Open = true;
			Founder = flag2 && !flag;
			_fit = true;
			_refresh = 0f;
		}

		public void Close()
		{
			Open = false;
		}

		public void Update(Actor host, Royal royal, WarMap war, float dt)
		{
			if (!Open)
			{
				return;
			}
			bool flag = Service.IsSoldier(host);
			bool flag2 = Service.CommissionOf(host) != null;
			if (!flag && !flag2)
			{
				Open = false;
				return;
			}
			Founder = flag2 && !flag;
			_refresh -= dt;
			if (_refresh > 0f)
			{
				return;
			}
			_refresh = 0.35f;
			try
			{
				Collect(host, royal, war);
			}
			catch (Exception ex)
			{
				Log.Error("soldier map: " + ex.Message);
			}
		}

		private void Collect(Actor host, Royal royal, WarMap war)
		{
			Order = (Founder ? null : Service.OrderFor(host, royal, war));
			Commission = (Founder ? Service.CommissionOf(host) : null);
			Kingdom kingdom = host.kingdom;
			City city = host.city;
			Captain = (Founder ? null : Service.CaptainOf(city));
			Captaincy = Captain != null && Captain == host;
			List<Kingdom> list = ((kingdom != null && kingdom.isCiv()) ? Service.Wars(kingdom) : new List<Kingdom>());
			Towns.Clear();
			foreach (City item in World.world.cities.list)
			{
				if (item == null || !item.isAlive())
				{
					continue;
				}
				WorldTile tile;
				try
				{
					tile = item.getTile();
				}
				catch
				{
					continue;
				}
				if (tile != null)
				{
					Kingdom kingdom2 = item.kingdom;
					bool capital = false;
					try
					{
						capital = kingdom2 != null && kingdom2.capital == item;
					}
					catch
					{
					}
					Towns.Add(new Town
					{
						City = item,
						P = new Vector2((float)tile.x + 0.5f, (float)tile.y + 0.5f),
						Name = item.name,
						Owner = kingdom2,
						Ours = (kingdom2 == kingdom),
						Home = (item == city),
						Target = (Order != null && Order.Target == item),
						Capital = capital
					});
				}
			}
			Dots.Clear();
			Foes = (Ours = 0);
			List<Actor> list2 = (Founder ? Service.SettlersOf(host) : null);
			List<Actor> simpleList = World.world.units.getSimpleList();
			for (int i = 0; i < simpleList.Count; i++)
			{
				Actor actor = simpleList[i];
				if (actor == null || actor == host || !actor.isAlive() || actor.kingdom == null)
				{
					continue;
				}
				bool flag = list2?.Contains(actor) ?? false;
				bool flag2 = actor.kingdom == kingdom;
				bool flag3 = !flag2 && list.Contains(actor.kingdom);
				bool flag4 = false;
				bool flag5 = false;
				bool flag6 = false;
				try
				{
					flag4 = actor.isWarrior();
					flag5 = actor.isKing();
					flag6 = actor.isFighting();
				}
				catch
				{
				}
				if (flag || ((flag2 || flag3) && (flag4 || flag5)))
				{
					bool flag7 = flag2 && flag4 && (actor.city == city || (Captaincy && actor.army != null && actor.army == host.army));
					Dots.Add(new Dot
					{
						Actor = actor,
						P = actor.current_position,
						Color = Royal.ColorOf(actor.kingdom),
						Ours = flag2,
						Army = flag7,
						Captain = (actor == Captain),
						King = flag5,
						Fighting = flag6,
						Settler = flag
					});
					if (flag3 && flag4)
					{
						Foes++;
					}
					if (flag7)
					{
						Ours++;
					}
					if (flag6)
					{
						Cluster(actor.current_position);
					}
				}
			}
			Clashes.Clear();
			foreach (KeyValuePair<long, int> item2 in _cellCount)
			{
				if (item2.Value >= 3)
				{
					Clashes.Add(_cellSum[item2.Key] / item2.Value);
				}
			}
			_cellSum.Clear();
			_cellCount.Clear();
			Sites.Clear();
			if (Founder)
			{
				Sites.AddRange(Service.Sites(host, city, 400));
			}
			if (_fit)
			{
				_fit = false;
				Fit(host);
			}
		}

		private void Cluster(Vector2 p)
		{
			long key = ((long)Mathf.FloorToInt(p.x / 7f) << 32) ^ (uint)Mathf.FloorToInt(p.y / 7f);
			_cellSum.TryGetValue(key, out var value);
			_cellCount.TryGetValue(key, out var value2);
			_cellSum[key] = value + p;
			_cellCount[key] = value2 + 1;
		}

		public bool Goal(out Vector2 p)
		{
			if (Commission != null)
			{
				p = Commission.Site;
				return true;
			}
			if (Order != null && Order.HasWhere)
			{
				p = Order.Where;
				return true;
			}
			p = default(Vector2);
			return false;
		}

		public void Fit(Actor host)
		{
			Vector2 current_position = host.current_position;
			float x0 = current_position.x;
			float y0 = current_position.y;
			float x1 = current_position.x;
			float y1 = current_position.y;
			if (Goal(out var p))
			{
				Grow(p);
			}
			try
			{
				if (host.city != null)
				{
					Grow(Service.CityPoint(host.city));
				}
			}
			catch
			{
			}
			try
			{
				if (Captain != null && Captain.isAlive())
				{
					Grow(Captain.current_position);
				}
			}
			catch
			{
			}
			if (Order != null && Order.Target != null)
			{
				Grow(Service.CityPoint(Order.Target));
			}
			float span = Mathf.Max(56f, Mathf.Max(x1 - x0, y1 - y0) + 28f);
			Span = span;
			Zoom = 1f;
			Centre = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
			void Grow(Vector2 vector)
			{
				x0 = Mathf.Min(x0, vector.x);
				y0 = Mathf.Min(y0, vector.y);
				x1 = Mathf.Max(x1, vector.x);
				y1 = Mathf.Max(y1, vector.y);
			}
		}

		public TileZone SiteNear(Vector2 p)
		{
			TileZone result = null;
			float num = 64f;
			foreach (TileZone site in Sites)
			{
				float sqrMagnitude = (new Vector2((float)site.centerTile.x + 0.5f, (float)site.centerTile.y + 0.5f) - p).sqrMagnitude;
				if (sqrMagnitude < num)
				{
					num = sqrMagnitude;
					result = site;
				}
			}
			return result;
		}
	}
}
