using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class FamilyMap
	{
		public struct Dot
		{
			public Actor Actor;

			public Vector2 P;
		}

		public struct Town
		{
			public City City;

			public Vector2 P;

			public string Name;

			public Kingdom Owner;

			public bool Home;
		}

		public bool Open;

		public readonly List<Actor> Picked = new List<Actor>();

		public readonly List<Dot> Foes = new List<Dot>();

		public readonly List<Town> Towns = new List<Town>();

		public Vector2 ListScroll;

		public float Span = 64f;

		public float Zoom = 1f;

		public Vector2 Centre;

		private float _refresh;

		public float ZoomMin => Span / Mathf.Max(Span, Mathf.Max(WarMap.WorldWidth, WarMap.WorldHeight) * 1.05f);

		public float ZoomMax => Mathf.Max(1f, Span / 12f);

		public void Toggle(Actor host)
		{
			Open = !Open;
			if (Open)
			{
				Picked.Clear();
				ListScroll = Vector2.zero;
				_refresh = 0f;
				Fit(host);
			}
		}

		public void Close()
		{
			Open = false;
		}

		public void Fit(Actor host)
		{
			Vector2 current_position = host.current_position;
			float num = current_position.x;
			float num2 = current_position.y;
			float num3 = current_position.x;
			float num4 = current_position.y;
			foreach (Actor member in FamilyFollow.Members)
			{
				try
				{
					if (member != null && member.isAlive())
					{
						Vector2 current_position2 = member.current_position;
						num = Mathf.Min(num, current_position2.x);
						num2 = Mathf.Min(num2, current_position2.y);
						num3 = Mathf.Max(num3, current_position2.x);
						num4 = Mathf.Max(num4, current_position2.y);
					}
				}
				catch
				{
				}
			}
			Span = Mathf.Clamp(Mathf.Max(num3 - num, num4 - num2) * 1.4f + 30f, 64f, 400f);
			Zoom = 1f;
			Centre = new Vector2((num + num3) * 0.5f, (num2 + num4) * 0.5f);
		}

		public void Update(Actor host, float dt)
		{
			Picked.RemoveAll((Actor a) => a == null || !a.isAlive() || !FamilyFollow.Members.Contains(a));
			_refresh -= dt;
			if (_refresh > 0f)
			{
				return;
			}
			_refresh = 0.35f;
			try
			{
				Collect(host);
			}
			catch (Exception ex)
			{
				Log.Error("family map: " + ex.Message);
			}
		}

		private void Collect(Actor host)
		{
			Foes.Clear();
			Towns.Clear();
			List<Vector2> list = new List<Vector2> { host.current_position };
			foreach (Actor member in FamilyFollow.Members)
			{
				try
				{
					if (member != null && member.isAlive())
					{
						list.Add(member.current_position);
					}
				}
				catch
				{
				}
			}
			List<Actor> simpleList = World.world.units.getSimpleList();
			for (int i = 0; i < simpleList.Count; i++)
			{
				Actor actor = simpleList[i];
				if (actor == null || actor == host || !actor.isAlive() || FamilyFollow.Members.Contains(actor))
				{
					continue;
				}
				Vector2 current_position = actor.current_position;
				bool flag = false;
				foreach (Vector2 item in list)
				{
					if ((item - current_position).sqrMagnitude < 256f)
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					continue;
				}
				bool flag2 = FriendlyFire.Foe(host, actor);
				if (!flag2)
				{
					foreach (Actor member2 in FamilyFollow.Members)
					{
						if (member2 != null && FamilyFollow.InDanger(member2, out var by) && by == actor)
						{
							flag2 = true;
							break;
						}
					}
				}
				if (flag2)
				{
					Foes.Add(new Dot
					{
						Actor = actor,
						P = current_position
					});
				}
			}
			City city = host.city;
			foreach (City item2 in World.world.cities.list)
			{
				try
				{
					if (item2 != null && item2.isAlive())
					{
						Towns.Add(new Town
						{
							City = item2,
							P = Service.CityPoint(item2),
							Name = item2.name,
							Owner = item2.kingdom,
							Home = (item2 == city)
						});
					}
				}
				catch
				{
				}
			}
		}

		public List<Actor> Whom()
		{
			if (Picked.Count <= 0)
			{
				return new List<Actor>(FamilyFollow.Members);
			}
			return new List<Actor>(Picked);
		}

		public string WhomName()
		{
			if (Picked.Count != 0)
			{
				if (Picked.Count != 1)
				{
					return Picked.Count + " of them";
				}
				return FamilyFollow.Name(Picked[0]);
			}
			return "everyone";
		}
	}
}
