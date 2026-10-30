using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal static class Guards
	{
		private sealed class Watch
		{
			public int Health = -1;

			public Actor Attacker;

			public float RalliedAt = -99f;

			public int Seen;
		}

		private const float Interval = 0.25f;

		private const float Witness = 14f;

		private const float GuardWitness = 22f;

		private const float AttackerReach = 12f;

		private const int MostDefenders = 10;

		private const float Again = 4f;

		private static readonly Dictionary<Actor, Watch> Kings = new Dictionary<Actor, Watch>();

		private static readonly List<Actor> Stale = new List<Actor>();

		private static readonly List<Actor> Candidates = new List<Actor>();

		private static float _timer;

		private static int _round;

		public static int LastRallied;

		public static bool Verbose;

		public static void Update(float dt, Actor you, Action<string> toast)
		{
			_timer -= dt;
			if (_timer > 0f)
			{
				return;
			}
			_timer = 0.25f;
			_round++;
			try
			{
				if (World.world == null || World.world.isPaused() || World.world.kingdoms == null)
				{
					return;
				}
				foreach (Kingdom item in World.world.kingdoms.list)
				{
					if (item == null || !item.isAlive() || !item.isCiv())
					{
						continue;
					}
					Actor king = item.king;
					if (king == null || !king.isAlive())
					{
						continue;
					}
					if (!Kings.TryGetValue(king, out var value))
					{
						value = (Kings[king] = new Watch());
					}
					value.Seen = _round;
					int health = king.getHealth();
					bool num = value.Health >= 0 && health < value.Health;
					value.Health = health;
					if (!num)
					{
						continue;
					}
					Actor actor = king.attackedBy as Actor;
					if (Verbose)
					{
						Log.Info("guards: " + WorldBoxMod.Describe(king) + " hurt, attacked by " + ((actor != null) ? WorldBoxMod.Describe(actor) : "nobody"));
					}
					if (actor == null || actor == king || !actor.isAlive() || (actor.current_position - king.current_position).sqrMagnitude > 144f)
					{
						continue;
					}
					float unscaledTime = Time.unscaledTime;
					if (actor == value.Attacker && unscaledTime - value.RalliedAt < 4f)
					{
						continue;
					}
					value.Attacker = actor;
					value.RalliedAt = unscaledTime;
					int num2 = (LastRallied = Rally(item, king, actor, you));
					if (Verbose)
					{
						Log.Info($"guards: {num2} called, {Candidates.Count} could come");
					}
					if (num2 != 0)
					{
						if (king == you)
						{
							toast((num2 == 1) ? "One of your people rushes to defend you!" : "Your people rush to defend you!");
						}
						else if (actor == you)
						{
							toast("The king's people rush to defend him!");
						}
						Log.Info($"defenders: {num2} of {Royal.NameOf(item)} rally to their king against {WorldBoxMod.Describe(actor)}");
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("king's defence failed: " + ex.Message);
			}
			if (_round % 240 != 0)
			{
				return;
			}
			Stale.Clear();
			foreach (KeyValuePair<Actor, Watch> king2 in Kings)
			{
				if (_round - king2.Value.Seen > 40)
				{
					Stale.Add(king2.Key);
				}
			}
			foreach (Actor item2 in Stale)
			{
				Kings.Remove(item2);
			}
		}

		private static int Rally(Kingdom k, Actor king, Actor attacker, Actor you)
		{
			Vector2 at = king.current_position;
			Candidates.Clear();
			int num = 0;
			foreach (Actor unit in k.getUnits())
			{
				if (unit == null || unit == king || unit == attacker || unit == you || !unit.isAlive() || unit.is_inside_building || unit.is_inside_boat || !unit.isAdult())
				{
					continue;
				}
				float num2 = (unit.isWarrior() ? 22f : 14f);
				if (!((unit.current_position - at).sqrMagnitude > num2 * num2))
				{
					if (unit.has_attack_target && unit.attack_target == attacker)
					{
						num++;
					}
					else if ((!unit.has_attack_target || unit.attack_target == null || !unit.attack_target.isAlive()) && !((BaseSimObject)unit).hasStatus("possessed"))
					{
						Candidates.Add(unit);
					}
				}
			}
			Candidates.Sort(delegate(Actor a, Actor b)
			{
				int num5 = ((!a.isWarrior()) ? 1 : 0);
				int num6 = ((!b.isWarrior()) ? 1 : 0);
				return (num5 != num6) ? num5.CompareTo(num6) : (a.current_position - at).sqrMagnitude.CompareTo((b.current_position - at).sqrMagnitude);
			});
			int num3 = 0;
			for (int num4 = 0; num4 < Candidates.Count; num4++)
			{
				if (num + num3 >= 10)
				{
					break;
				}
				Actor actor = Candidates[num4];
				try
				{
					actor.addAggro(attacker);
					actor.startFightingWith(attacker);
					num3++;
				}
				catch (Exception ex)
				{
					Log.Error("a defender could not answer: " + ex.Message);
				}
			}
			return num3;
		}
	}
}
