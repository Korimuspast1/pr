using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class Pings
	{
		public const int Most = 8;

		public readonly List<Ping> List = new List<Ping>();

		public int Changes;

		public static readonly PingKind[] Wheel = new PingKind[6]
		{
			PingKind.Look,
			PingKind.Go,
			PingKind.Enemy,
			PingKind.Danger,
			PingKind.Loot,
			PingKind.Defend
		};

		public static string Name(PingKind k)
		{
			switch (k)
			{
			case PingKind.Go:
				return "Go here";
			case PingKind.Enemy:
				return "Enemy";
			case PingKind.Danger:
				return "Danger";
			case PingKind.Loot:
				return "Loot";
			case PingKind.Defend:
				return "Defend";
			default:
				return "Look";
			}
		}

		public static string Icon(PingKind k)
		{
			switch (k)
			{
			case PingKind.Go:
				return "iconArrowDestination";
			case PingKind.Enemy:
				return "iconArrowAttackTarget";
			case PingKind.Danger:
				return "iconFireSkull";
			case PingKind.Loot:
				return "iconLoot";
			case PingKind.Defend:
				return "iconShield";
			default:
				return "iconFavoriteStar_Map";
			}
		}

		public static Color ColorOf(PingKind k)
		{
			switch (k)
			{
			case PingKind.Go:
				return new Color(0.45f, 0.95f, 0.4f);
			case PingKind.Enemy:
				return new Color(1f, 0.28f, 0.22f);
			case PingKind.Danger:
				return new Color(1f, 0.6f, 0.15f);
			case PingKind.Loot:
				return new Color(1f, 0.84f, 0.25f);
			case PingKind.Defend:
				return new Color(0.4f, 0.7f, 1f);
			default:
				return new Color(1f, 0.93f, 0.45f);
			}
		}

		private static float Life(PingKind k)
		{
			switch (k)
			{
			default:
				return 60f;
			case PingKind.Enemy:
				return 90f;
			case PingKind.Go:
				return 600f;
			}
		}

		public static bool Follows(PingKind k)
		{
			if (k != PingKind.Look && k != PingKind.Enemy && k != PingKind.Danger)
			{
				return k == PingKind.Defend;
			}
			return true;
		}

		public Ping Add(PingKind kind, Vector3 at, Actor actor, BaseSimObject on, bool far, float life = 0f)
		{
			float unscaledTime = Time.unscaledTime;
			Ping ping = new Ping
			{
				Kind = kind,
				At = at,
				Actor = (Follows(kind) ? actor : null),
				On = (on ?? actor),
				Born = unscaledTime,
				Until = unscaledTime + ((life > 0f) ? life : Life(kind)),
				Far = far
			};
			if (kind == PingKind.Go)
			{
				List.RemoveAll((Ping q) => q.Kind == PingKind.Go);
			}
			List.Add(ping);
			while (List.Count > 8)
			{
				List.RemoveAt(0);
			}
			Changes++;
			return ping;
		}

		public static Vector3 Where(Ping p)
		{
			if (p.Actor != null)
			{
				try
				{
					if (p.Actor.isAlive())
					{
						Vector2 current_position = p.Actor.current_position;
						return new Vector3(current_position.x, current_position.y, p.At.z);
					}
				}
				catch
				{
				}
			}
			return p.At;
		}

		public Ping Near(Vector3 at, Actor actor, float radius)
		{
			Ping result = null;
			float num = radius * radius;
			foreach (Ping item in List)
			{
				if (actor != null && item.Actor == actor)
				{
					return item;
				}
				Vector3 vector = Where(item);
				float sqrMagnitude = (new Vector2(vector.x, vector.y) - new Vector2(at.x, at.y)).sqrMagnitude;
				if (sqrMagnitude < num)
				{
					num = sqrMagnitude;
					result = item;
				}
			}
			return result;
		}

		public Ping On(Actor a)
		{
			if (a == null)
			{
				return null;
			}
			foreach (Ping item in List)
			{
				if (item.Actor == a)
				{
					return item;
				}
			}
			return null;
		}

		public void Remove(Ping p)
		{
			if (List.Remove(p))
			{
				Changes++;
			}
		}

		public void Clear()
		{
			if (List.Count > 0)
			{
				Changes++;
			}
			List.Clear();
		}

		public bool Update(Vector2 you)
		{
			float unscaledTime = Time.unscaledTime;
			bool result = false;
			for (int num = List.Count - 1; num >= 0; num--)
			{
				Ping ping = List[num];
				bool flag = unscaledTime >= ping.Until;
				if (!flag && ping.Actor != null)
				{
					bool flag2;
					try
					{
						flag2 = ping.Actor.isAlive();
					}
					catch
					{
						flag2 = false;
					}
					if (!flag2)
					{
						ping.Actor = null;
						ping.Until = Mathf.Min(ping.Until, unscaledTime + 4f);
					}
				}
				if (!flag && ping.Kind == PingKind.Go && unscaledTime - ping.Born > 1f && (new Vector2(ping.At.x, ping.At.y) - you).sqrMagnitude < 6.25f)
				{
					flag = true;
					result = true;
				}
				if (flag)
				{
					List.RemoveAt(num);
					Changes++;
				}
			}
			return result;
		}

		public static int Rally(Actor you, Actor target, int most)
		{
			if (you == null || target == null || target == you)
			{
				return 0;
			}
			Kingdom kingdom = you.kingdom;
			if (kingdom == null || !kingdom.isCiv())
			{
				return 0;
			}
			try
			{
				if (!target.isAlive())
				{
					return 0;
				}
				Kingdom kingdom2 = target.kingdom;
				if (kingdom2 == kingdom)
				{
					return 0;
				}
				if (kingdom2 != null && kingdom2.isCiv() && !kingdom.isEnemy(kingdom2) && !Royal.AtWar(kingdom, kingdom2))
				{
					return 0;
				}
			}
			catch
			{
				return 0;
			}
			Vector2 at = target.current_position;
			Vector2 current_position = you.current_position;
			List<Actor> list = new List<Actor>();
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				try
				{
					if (simple != null && simple != you && simple != target && simple.isAlive() && simple.kingdom == kingdom && !simple.is_inside_building && !simple.is_inside_boat && simple.isAdult() && simple.isWarrior() && !((BaseSimObject)simple).hasStatus("possessed"))
					{
						float sqrMagnitude = (simple.current_position - at).sqrMagnitude;
						float sqrMagnitude2 = (simple.current_position - current_position).sqrMagnitude;
						if ((!(sqrMagnitude > 676f) || !(sqrMagnitude2 > 400f)) && (!simple.has_attack_target || simple.attack_target == null || simple.attack_target == target || !simple.attack_target.isAlive()))
						{
							list.Add(simple);
						}
					}
				}
				catch
				{
				}
			}
			list.Sort((Actor a, Actor b) => (a.current_position - at).sqrMagnitude.CompareTo((b.current_position - at).sqrMagnitude));
			int num = 0;
			for (int num2 = 0; num2 < list.Count; num2++)
			{
				if (num >= most)
				{
					break;
				}
				try
				{
					list[num2].addAggro(target);
					list[num2].startFightingWith(target);
					num++;
				}
				catch (Exception ex)
				{
					Log.Error("ping: a warrior could not answer: " + ex.Message);
				}
			}
			return num;
		}
	}
}
