using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal static class Attention
	{
		private sealed class Gaze
		{
			public float Yaw;

			public float YawRate;

			public float Nod;

			public Vector2 At;

			public Actor Who;

			public float Glance;

			public Kind What;

			public float Until;

			public float NextPick;

			public float Greeted = -100f;

			public float OverrideUntil;

			public float LastFacing = float.NaN;

			public bool Wanted;

			public Vector2 Override;

			public int Seen;

			public uint Rng;
		}

		private enum Kind
		{
			Ahead,
			Glance,
			Point,
			Person,
			You,
			AwayFromYou
		}

		private static readonly Dictionary<Actor, Gaze> Gazes = new Dictionary<Actor, Gaze>();

		private static readonly List<Actor> Stale = new List<Actor>();

		private static readonly List<Vector2> Fights = new List<Vector2>();

		private static float _fightsAt = -10f;

		private static int _frame;

		private const float MaxYaw = 1.2f;

		private const float Range = 28f;

		public static void Watch(Actor a, Vector2 point, float seconds)
		{
			if (a != null)
			{
				Gaze gaze = Of(a);
				gaze.Override = point;
				gaze.OverrideUntil = PoseClock.Now + seconds;
				gaze.NextPick = 0f;
			}
		}

		public static void Tick()
		{
			_frame++;
			if (_frame % 600 != 0)
			{
				return;
			}
			Stale.Clear();
			foreach (KeyValuePair<Actor, Gaze> gaze in Gazes)
			{
				if (_frame - gaze.Value.Seen > 600)
				{
					Stale.Add(gaze.Key);
				}
			}
			foreach (Actor item in Stale)
			{
				Gazes.Remove(item);
			}
		}

		private static Gaze Of(Actor a)
		{
			if (!Gazes.TryGetValue(a, out var value))
			{
				value = new Gaze
				{
					Rng = (uint)((a.GetHashCode() * -1640531535) | 1)
				};
				Gazes[a] = value;
			}
			return value;
		}

		public static void Head(Actor a, Vector2 at, float facing, float speed, float tall, float dt, out float yaw, out float nod)
		{
			yaw = (nod = 0f);
			if (a == null)
			{
				return;
			}
			Gaze gaze = Of(a);
			gaze.Seen = _frame;
			float now = PoseClock.Now;
			Actor actor = ((WorldBoxMod.Instance != null && WorldBoxMod.Instance.IsFirstPerson) ? WorldBoxMod.Instance.Host : null);
			float num = ((actor != null) ? (actor.current_position - at).magnitude : float.MaxValue);
			float target = 0f;
			float num2 = 0f;
			if (num < 28f)
			{
				if (now >= gaze.NextPick || gaze.Until <= now)
				{
					Pick(a, gaze, at, facing, speed, actor, num, now);
				}
				if (!Target(a, gaze, at, actor, now, out var point, out var tall2))
				{
					target = ((gaze.What == Kind.Glance) ? gaze.Glance : 0f);
				}
				else
				{
					Vector2 vector = point - at;
					float num3 = Turn.Wrap(Mathf.Atan2(vector.y, vector.x) - facing);
					if (gaze.What == Kind.AwayFromYou)
					{
						target = (0f - Mathf.Sign((num3 == 0f) ? 1f : num3)) * 1.2f;
						num2 = 0.15f;
					}
					else
					{
						target = ((Mathf.Abs(num3) < 1.7f) ? Mathf.Clamp(num3, -1.2f, 1.2f) : 0f);
						float x = Mathf.Max(0.5f, vector.magnitude);
						num2 = Mathf.Clamp(Mathf.Atan2((tall - tall2) * 0.9f, x), -0.35f, 0.35f);
					}
				}
				if (gaze.What == Kind.Ahead && speed > 0.3f && !float.IsNaN(gaze.LastFacing) && dt > 0f)
				{
					target = Mathf.Clamp(Turn.Wrap(facing - gaze.LastFacing) / dt * 0.12f, -0.45f, 0.45f);
				}
				if (gaze.What == Kind.You && now - gaze.Greeted < 0.7f)
				{
					num2 += 0.2f * Mathf.Sin((now - gaze.Greeted) / 0.7f * MathF.PI);
				}
				if (gaze.What == Kind.You && gaze.Wanted)
				{
					num2 += 0.08f;
				}
			}
			gaze.LastFacing = facing;
			gaze.Yaw = Turn.Damp(gaze.Yaw, ref gaze.YawRate, target, dt, 0.12f, 6f);
			gaze.Nod = Mathf.MoveTowards(gaze.Nod, num2, dt * 1.6f);
			yaw = gaze.Yaw;
			nod = gaze.Nod;
		}

		public static bool FaceTo(Actor a, out Vector2 face)
		{
			face = default(Vector2);
			Actor actor = Chatting(a);
			if (actor == null)
			{
				return false;
			}
			face = actor.current_position;
			return (face - a.current_position).sqrMagnitude < 16f;
		}

		private static Actor Chatting(Actor a)
		{
			try
			{
				string text = ((a.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task != null) ? ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id : null);
				if (text == null || !text.StartsWith("socialize", StringComparison.Ordinal))
				{
					return null;
				}
				return (a.beh_actor_target is Actor actor && actor.isAlive() && actor != a) ? actor : null;
			}
			catch
			{
				return null;
			}
		}

		private static void Pick(Actor a, Gaze g, Vector2 at, float facing, float speed, Actor host, float far, float now)
		{
			float num = Next(g);
			g.NextPick = now + 1.2f + 2f * Next(g);
			g.Who = null;
			if (g.OverrideUntil > now)
			{
				g.What = Kind.Point;
				g.Until = g.OverrideUntil;
				return;
			}
			Actor actor = Chatting(a);
			if (actor != null && num < 0.85f)
			{
				g.What = Kind.Person;
				g.Who = actor;
				g.Until = g.NextPick;
				return;
			}
			if (host != null && host != a && far < 7f)
			{
				Vector2 vector = host.current_position - at;
				if (Mathf.Abs(Turn.Wrap(Mathf.Atan2(vector.y, vector.x) - facing)) < 1.9f || far < 1.6f)
				{
					int num2 = 0;
					bool flag = false;
					try
					{
						num2 = Chronicle.RegardOf(a, host, create: false)?.Opinion ?? 0;
						flag = a.attackedBy == host || num2 <= -50;
					}
					catch
					{
					}
					bool flag2 = (g.Wanted = Wanted(a, host));
					float num3 = (flag2 ? 0.9f : (flag ? 0.6f : ((num2 >= 25) ? 0.75f : 0.5f)));
					if (far < 3.5f)
					{
						num3 += 0.25f;
					}
					if (num < num3)
					{
						bool num4 = g.What == Kind.You;
						g.What = ((flag && !flag2) ? Kind.AwayFromYou : Kind.You);
						g.Until = now + (flag2 ? 3.5f : (1.2f + 1.8f * Next(g)));
						g.NextPick = g.Until;
						if (!num4 && num2 >= 25 && !flag2 && !flag)
						{
							g.Greeted = now;
						}
						return;
					}
				}
			}
			if (Next(g) < 0.6f && NearestFight(at, 10f, out var where))
			{
				g.What = Kind.Point;
				g.At = where;
				g.Until = g.NextPick;
			}
			else if (speed < 0.3f && Next(g) < (Child(a) ? 0.75f : 0.5f))
			{
				g.What = Kind.Glance;
				g.Glance = (Next(g) * 2f - 1f) * 1.1f;
				g.Until = now + 0.8f + 1.4f * Next(g);
				g.NextPick = g.Until;
			}
			else
			{
				g.What = Kind.Ahead;
				g.Until = g.NextPick;
			}
		}

		private static bool Target(Actor a, Gaze g, Vector2 at, Actor host, float now, out Vector2 point, out float tall)
		{
			point = default(Vector2);
			tall = 0f;
			switch (g.What)
			{
			case Kind.Point:
				point = ((g.OverrideUntil > now) ? g.Override : g.At);
				tall = 0.4f;
				return true;
			case Kind.Person:
				if (g.Who == null || !g.Who.isAlive())
				{
					g.What = Kind.Ahead;
					return false;
				}
				point = g.Who.current_position;
				tall = 0.8f;
				return true;
			case Kind.You:
			case Kind.AwayFromYou:
				if (host == null)
				{
					g.What = Kind.Ahead;
					return false;
				}
				point = host.current_position;
				tall = 0.85f;
				return true;
			default:
				return false;
			}
		}

		private static bool Wanted(Actor a, Actor host)
		{
			try
			{
				return host != null && a.kingdom != null && Law.BountyOn(a.kingdom, host) > 0;
			}
			catch
			{
				return false;
			}
		}

		private static bool Child(Actor a)
		{
			try
			{
				return !a.isAdult();
			}
			catch
			{
				return false;
			}
		}

		private static bool NearestFight(Vector2 at, float within, out Vector2 where)
		{
			where = default(Vector2);
			if (Time.time - _fightsAt > 0.5f)
			{
				_fightsAt = Time.time;
				Fights.Clear();
				try
				{
					Actor actor = ((WorldBoxMod.Instance != null) ? WorldBoxMod.Instance.Host : null);
					if (actor == null)
					{
						return false;
					}
					Vector2 current_position = actor.current_position;
					List<Actor> simpleList = World.world.units.getSimpleList();
					for (int i = 0; i < simpleList.Count; i++)
					{
						if (Fights.Count >= 16)
						{
							break;
						}
						Actor actor2 = simpleList[i];
						if (actor2 != null && actor2.isAlive() && actor2.has_attack_target && (actor2.current_position - current_position).sqrMagnitude < 1600f)
						{
							Fights.Add(actor2.current_position);
						}
					}
				}
				catch
				{
				}
			}
			float num = within * within;
			bool result = false;
			foreach (Vector2 fight in Fights)
			{
				float sqrMagnitude = (fight - at).sqrMagnitude;
				if (sqrMagnitude < num && sqrMagnitude > 0.25f)
				{
					num = sqrMagnitude;
					where = fight;
					result = true;
				}
			}
			return result;
		}

		internal static bool LookingAtYouForTest(Actor a, out float yaw)
		{
			yaw = 0f;
			if (a == null || !Gazes.TryGetValue(a, out var value))
			{
				return false;
			}
			yaw = value.Yaw;
			return value.What == Kind.You;
		}

		private static float Next(Gaze g)
		{
			g.Rng ^= g.Rng << 13;
			g.Rng ^= g.Rng >> 17;
			g.Rng ^= g.Rng << 5;
			return (float)(g.Rng & 0xFFFFFF) / 16777216f;
		}
	}
}
