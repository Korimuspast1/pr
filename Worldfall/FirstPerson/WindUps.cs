using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class WindUps
	{
		private sealed class Wind
		{
			public double Start;

			public double End;

			public double Struck = -1.0;

			public WeaponKind Kind;

			public bool Beast;
		}

		public const float Seconds = 0.4f;

		public const float MinTell = 0.3f;

		public const float HoldAtMost = 1.5f;

		private static readonly Dictionary<Actor, Wind> Winds = new Dictionary<Actor, Wind>();

		private static readonly Dictionary<Actor, double> HeldSince = new Dictionary<Actor, double>();

		private static readonly List<Actor> _gone = new List<Actor>();

		internal static int Begun;

		internal static int Held;

		public static Actor StartedBy;

		public static WeaponKind StartedKind;

		public static bool StartedBeast;

		public static void Update(Actor host, List<BaseSimObject> nearby, bool on)
		{
			StartedBy = null;
			if (!on || host == null || nearby == null)
			{
				Winds.Clear();
				HeldSince.Clear();
				return;
			}
			double curWorldTime;
			try
			{
				curWorldTime = World.world.getCurWorldTime();
			}
			catch
			{
				return;
			}
			for (int i = 0; i < nearby.Count; i++)
			{
				if (nearby[i] is Actor actor && actor != host)
				{
					try
					{
						Look(actor, host, curWorldTime);
					}
					catch
					{
						Winds.Remove(actor);
					}
				}
			}
			_gone.Clear();
			foreach (KeyValuePair<Actor, Wind> wind in Winds)
			{
				Actor key = wind.Key;
				Wind value = wind.Value;
				double num = WorldPace.Factor;
				bool flag = ((value.Struck >= 0.0) ? (curWorldTime - value.Struck > 0.8 * num) : (curWorldTime > value.End + 0.5 * num));
				bool flag2 = false;
				try
				{
					flag2 = key.isAlive() && key.has_attack_target && key.attack_target == host;
				}
				catch
				{
				}
				if (flag || !flag2)
				{
					_gone.Add(key);
				}
			}
			foreach (Actor item in _gone)
			{
				Winds.Remove(item);
			}
		}

		private static void Look(Actor a, Actor host, double now)
		{
			if (!a.isAlive() || !a.has_attack_target || a.attack_target != host || ((BaseSimObject)a).isFlying() || !a.hasMeleeAttack() || a.asset.skip_fight_logic)
			{
				return;
			}
			if (Winds.TryGetValue(a, out var value))
			{
				if (value.Struck < 0.0 && a.last_attack_timestamp >= value.Start - 0.01)
				{
					value.Struck = a.last_attack_timestamp;
				}
				else if (value.Struck >= 0.0 && a.last_attack_timestamp > value.Struck + 0.01)
				{
					Winds.Remove(a);
				}
				else if (value.Struck < 0.0 && !a.isInAttackRange((BaseSimObject)host) && now > value.Start + 0.05 * (double)WorldPace.Factor)
				{
					Winds.Remove(a);
				}
				return;
			}
			float attackRange = a.getAttackRange();
			if (!((a.current_position - host.current_position).magnitude - ((BaseSimObject)a).stats["size"] - ((BaseSimObject)host).stats["size"] <= attackRange + 1.5f))
			{
				HeldSince.Remove(a);
				return;
			}
			if (a.attack_timer < 0.3f && a.getAttackCooldown() >= 0.6f)
			{
				if (!HeldSince.TryGetValue(a, out var value2))
				{
					if (HeldSince.Count > 64)
					{
						HeldSince.Clear();
					}
					value2 = (HeldSince[a] = now);
				}
				if (now - value2 < (double)(1.5f * WorldPace.Factor))
				{
					a.attack_timer = 0.3f;
					Held++;
				}
			}
			if (a.isInAttackRange((BaseSimObject)host) && !(a.attack_timer > 0.4f))
			{
				value = new Wind
				{
					Start = now,
					End = now + (double)(a.attack_timer * WorldPace.Factor),
					Beast = !People.IsHumanoid(a)
				};
				try
				{
					string text = ((a.getWeapon() == null) ? null : a.getWeaponAsset()?.id);
					value.Kind = ((text != null) ? ViewMeshes.KindOf(text) : WeaponKind.None);
				}
				catch
				{
					value.Kind = WeaponKind.None;
				}
				Winds[a] = value;
				HeldSince.Remove(a);
				Begun++;
				StartedBy = a;
				StartedKind = value.Kind;
				StartedBeast = value.Beast;
			}
		}

		public static bool Phase(Actor a, WeaponKind kind, float span, out float attack)
		{
			attack = 0f;
			if (Winds.Count == 0 || a == null || !Winds.TryGetValue(a, out var value))
			{
				return false;
			}
			double curWorldTime;
			try
			{
				curWorldTime = World.world.getCurWorldTime();
			}
			catch
			{
				return false;
			}
			Shares(kind, out var top, out var hit);
			if (value.Struck >= 0.0)
			{
				float num = (float)((curWorldTime - value.Struck) / (double)WorldPace.Factor);
				attack = hit + num / Mathf.Max(0.1f, span);
				if (attack >= 1f)
				{
					attack = 0f;
				}
				return true;
			}
			float num2 = Mathf.Clamp01((float)((curWorldTime - value.Start) / Math.Max(0.05, value.End - value.Start)));
			attack = ((num2 < 0.75f) ? (top * Smooth(num2 / 0.75f)) : (top + (hit - top) * Smooth((num2 - 0.75f) / 0.25f)));
			if (attack <= 0f)
			{
				attack = 0.001f;
			}
			return true;
		}

		public static float Coil(Actor a)
		{
			if (Winds.Count == 0 || a == null || !Winds.TryGetValue(a, out var value) || value.Struck >= 0.0)
			{
				return 0f;
			}
			double curWorldTime;
			try
			{
				curWorldTime = World.world.getCurWorldTime();
			}
			catch
			{
				return 0f;
			}
			float num = Mathf.Clamp01((float)((curWorldTime - value.Start) / Math.Max(0.05, value.End - value.Start)));
			if (!(num < 0.8f))
			{
				return 1f - Smooth((num - 0.8f) / 0.2f);
			}
			return Smooth(num / 0.8f);
		}

		internal static bool Winding(Actor a)
		{
			if (a != null && Winds.TryGetValue(a, out var value))
			{
				return value.Struck < 0.0;
			}
			return false;
		}

		internal static float Left(Actor a)
		{
			if (a == null || !Winds.TryGetValue(a, out var value) || value.Struck >= 0.0)
			{
				return -1f;
			}
			try
			{
				return (float)((value.End - World.world.getCurWorldTime()) / (double)WorldPace.Factor);
			}
			catch
			{
				return -1f;
			}
		}

		private static void Shares(WeaponKind kind, out float top, out float hit)
		{
			switch (kind)
			{
			case WeaponKind.Sword:
			case WeaponKind.Stick:
			case WeaponKind.Dagger:
				top = 0.3f;
				hit = 0.47f;
				break;
			case WeaponKind.Axe:
			case WeaponKind.Hammer:
				top = 0.4f;
				hit = 0.52f;
				break;
			case WeaponKind.Spear:
				top = 0.3f;
				hit = 0.42f;
				break;
			case WeaponKind.Staff:
				top = 0.3f;
				hit = 0.35f;
				break;
			default:
				top = 0.12f;
				hit = 0.25f;
				break;
			}
		}

		private static float Smooth(float x)
		{
			x = Mathf.Clamp01(x);
			return x * x * (3f - 2f * x);
		}

		public static void Clear()
		{
			Winds.Clear();
			HeldSince.Clear();
			StartedBy = null;
		}
	}
}
