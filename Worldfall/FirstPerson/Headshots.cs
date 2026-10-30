using System;
using System.Collections.Generic;
using System.Reflection;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class Headshots
	{
		public const float Blow = 1.5f;

		public const float Shot = 2f;

		public const float Pellet = 1.5f;

		public const float KnockSeconds = 0.4f;

		public const float ShowSeconds = 0.6f;

		private static BaseSimObject _victim;

		private static BaseSimObject _by;

		private static float _times = 1f;

		private static bool _applied;

		private static bool _reported;

		private static readonly FieldInfo DamageField = typeof(AttackData).GetField("damage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

		private static readonly Dictionary<Actor, float> Struck = new Dictionary<Actor, float>();

		private static readonly Dictionary<Actor, float> Shown = new Dictionary<Actor, float>();

		private static readonly List<Actor> Old = new List<Actor>();

		internal static int Landed;

		internal static int LastBase;

		internal static int LastDamage;

		internal static float LastTimes;

		public static bool Aimed(Actor target, float reach)
		{
			if (target == null || !Hitboxes.GetHead(target, out var head))
			{
				return false;
			}
			Vec3 eye = FirstPersonAim.Eye;
			Vec3 ray = FirstPersonAim.Ray;
			float t;
			return head.Segment(eye, eye + ray * (reach + 0.25f), 0.06f, out t);
		}

		public static bool Hit(Actor target, Vec3 at, Vec3 dir)
		{
			if (target == null || !Hitboxes.GetHead(target, out var head))
			{
				return false;
			}
			dir = dir.Normalized();
			float num = 0.6f;
			if (Hitboxes.Get(target, out var box))
			{
				num = Mathf.Max(0.3f, 2f * Mathf.Max(box.HalfA, Mathf.Max(box.HalfB, box.HalfUp)));
			}
			float t;
			return head.Segment(at - dir * 0.05f, at + dir * num, 0.04f, out t);
		}

		public static void Aim(BaseSimObject by, BaseSimObject victim, float times)
		{
			_by = by;
			_victim = ((times > 1f) ? victim : null);
			_times = times;
		}

		public static void Clear()
		{
			_by = (_victim = null);
			_times = 1f;
		}

		public static void SkillScale(ref AttackData data, Actor you)
		{
			try
			{
				if (you == null || data.initiator == null || data.initiator != you || data.damage <= 0)
				{
					return;
				}
				float num = (data.is_projectile ? 1f : Moves.BlowScale);
				if (!(Mathf.Abs(num - 1f) < 0.001f))
				{
					int num2 = Mathf.Max(1, Mathf.RoundToInt((float)data.damage * num));
					if (num2 != data.damage)
					{
						object obj = data;
						DamageField.SetValue(obj, num2);
						data = (AttackData)obj;
					}
				}
			}
			catch (Exception ex)
			{
				if (!_reported)
				{
					_reported = true;
					Log.Error("skill damage: " + ex.Message);
				}
			}
		}

		public static bool Before(ref AttackData data, BaseSimObject target)
		{
			_applied = false;
			if (_victim == null || target != _victim || data.initiator != _by || data.damage <= 0)
			{
				return false;
			}
			int num = Mathf.Max(data.damage + 1, Mathf.RoundToInt((float)data.damage * _times));
			try
			{
				object obj = data;
				DamageField.SetValue(obj, num);
				AttackData attackData = (AttackData)obj;
				if (attackData.damage != num)
				{
					return false;
				}
				LastBase = data.damage;
				data = attackData;
			}
			catch (Exception ex)
			{
				if (!_reported)
				{
					_reported = true;
					Log.Error("headshot damage: " + ex.Message);
				}
				return false;
			}
			LastDamage = data.damage;
			LastTimes = _times;
			_applied = true;
			return true;
		}

		public static void After(BaseSimObject target, AttackDataResult result)
		{
			if (!_applied)
			{
				return;
			}
			_applied = false;
			if (result.state == ApplyAttackState.Hit && target != null && target.isActor())
			{
				float unscaledTime = Time.unscaledTime;
				Struck[target.a] = unscaledTime;
				Shown[target.a] = unscaledTime;
				Landed++;
				if (Struck.Count > 32)
				{
					Forget(unscaledTime);
				}
			}
		}

		public static bool Take(Actor victim)
		{
			if (victim == null || !Shown.TryGetValue(victim, out var value))
			{
				return false;
			}
			Shown.Remove(victim);
			return Time.unscaledTime - value < 0.6f;
		}

		public static bool Killed(Actor victim)
		{
			if (victim != null && Struck.TryGetValue(victim, out var value))
			{
				return Time.unscaledTime - value < 0.6f;
			}
			return false;
		}

		public static bool KnockOf(Actor a, out float age)
		{
			age = 99f;
			if (a == null || Struck.Count == 0 || !Struck.TryGetValue(a, out var value))
			{
				return false;
			}
			age = Time.unscaledTime - value;
			if (age >= 0f)
			{
				return age < 0.4f;
			}
			return false;
		}

		private static void Forget(float now)
		{
			Old.Clear();
			foreach (KeyValuePair<Actor, float> item in Struck)
			{
				if (now - item.Value > 0.6f)
				{
					Old.Add(item.Key);
				}
			}
			foreach (Actor item2 in Old)
			{
				Struck.Remove(item2);
			}
			Old.Clear();
			foreach (KeyValuePair<Actor, float> item3 in Shown)
			{
				if (now - item3.Value > 0.6f)
				{
					Old.Add(item3.Key);
				}
			}
			foreach (Actor item4 in Old)
			{
				Shown.Remove(item4);
			}
		}
	}
}
