using System;
using System.Reflection;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class GuardControl
	{
		internal enum Guarded : byte
		{
			None,
			Parried,
			Blocked,
			Broken
		}

		public readonly GuardState State = new GuardState();

		public const float Arc = 70f;

		private const float ParryStunMin = 1f;

		private const float ParryStunMax = 1.5f;

		internal static GuardControl Current;

		private Actor _owner;

		private float _chip = 0.25f;

		private int _lastPresses;

		public float SinceBroken = 99f;

		public float SinceParry = 99f;

		internal bool HeldForTest;

		internal bool PressedForTest;

		internal static int Blocked;

		internal static int Parried;

		internal static int Broken;

		internal static int Ripostes;

		internal static int LastChip;

		internal static int LastCost;

		private static readonly FieldInfo CriticalField = typeof(AttackData).GetField("critical", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

		private static bool _reported;

		public bool Active { get; private set; }

		public float Raise
		{
			get
			{
				if (!Active)
				{
					return 0f;
				}
				return State.Raise;
			}
		}

		internal float Chip => _chip;

		public void Update(Actor host, WeaponKind kind, bool hands, bool canGuard, float dt, MeleeControl melee, BowControl bow, int parryTiming, FirstPersonAudio audio)
		{
			SinceBroken += dt;
			SinceParry += dt;
			if (host != _owner)
			{
				State.Reset();
				_owner = host;
			}
			bool flag = (Active = host != null && !Crabzillas.Is(host) && !BoatHelm.Steers(host));
			Current = (flag ? this : null);
			if (!flag)
			{
				State.Reset();
				PossessionHooks.GuardSlow = 0f;
				return;
			}
			GuardState state = State;
			float parryWindow;
			switch (parryTiming)
			{
			default:
				parryWindow = 0.2f;
				break;
			case 2:
				parryWindow = 0.12f;
				break;
			case 0:
				parryWindow = 0.3f;
				break;
			}
			state.ParryWindow = parryWindow;
			_chip = ((!hands) ? 0.7f : ((kind == WeaponKind.None) ? 0.5f : 0.25f));
			bool flag3 = melee != null && melee.Active && melee.State.Swinging && melee.State.Time < melee.State.Timing.StrikeEnd;
			bool flag4 = bow != null && bow.InHand && bow.Draw > 0.05f;
			bool held = PlayInput.GetMouseButton(1) || HeldForTest;
			bool pressed = PlayInput.GetMouseButtonDown(1) || PressedForTest;
			PressedForTest = false;
			State.Update(Mathf.Min(dt, 0.1f), held, canGuard && !flag3 && !flag4, pressed);
			if (State.Presses != _lastPresses)
			{
				_lastPresses = State.Presses;
				audio?.PlayGuardUp();
			}
			PossessionHooks.GuardSlow = State.Raise;
		}

		internal static bool Take(AttackData data, BaseSimObject by, Actor you, out AttackDataResult result)
		{
			result = AttackDataResult.Continue;
			GuardControl current = Current;
			if (current == null || !current.Active || you == null || current._owner != you)
			{
				return false;
			}
			GuardState state = current.State;
			bool tooBig;
			switch (Judge(data, by, you, state.ParryOpen, state.Blocking, FirstPersonAim.Flat, current._chip, out result, out tooBig))
			{
			case Guarded.Parried:
				state.Parried();
				Parried++;
				current.SinceParry = 0f;
				Blocks.Guarded(data, you, FirstPerson.Stop.Parried);
				return true;
			case Guarded.Blocked:
				Blocked++;
				Blocks.Guarded(data, you, FirstPerson.Stop.Blocked);
				return true;
			case Guarded.Broken:
				state.Stagger();
				Broken++;
				current.SinceBroken = 0f;
				WorldBoxMod.Instance?.GuardBroken(tooBig ? "too heavy to hold" : "out of stamina");
				return false;
			default:
				return false;
			}
		}

		internal static Guarded Judge(AttackData data, BaseSimObject by, Actor you, bool parry, bool block, Vector2 facing, float chip, out AttackDataResult result, out bool tooBig)
		{
			result = AttackDataResult.Continue;
			tooBig = false;
			if (!parry && !block)
			{
				return Guarded.None;
			}
			try
			{
				Vector2 to = ((by != null && by.isAlive()) ? by.current_position : new Vector2(data.initiator_position.x, data.initiator_position.y)) - you.current_position;
				if (to.sqrMagnitude > 0.0001f && Vector2.Angle(facing, to) > 70f)
				{
					return Guarded.None;
				}
				Actor actor = ((by != null && by.isActor()) ? by.a : null);
				bool is_projectile = data.is_projectile;
				if (parry)
				{
					if (!is_projectile && actor != null && actor.isAlive())
					{
						actor.makeStunned(UnityEngine.Random.Range(1f, 1.5f));
						actor.startAttackCooldown();
					}
					result = new AttackDataResult(ApplyAttackState.Deflect, you.data.id);
					return Guarded.Parried;
				}
				if (is_projectile)
				{
					if (!CanBeBlocked(data.projectile_id))
					{
						return Guarded.None;
					}
					result = AttackDataResult.Block;
					return Guarded.Blocked;
				}
				int num = Mathf.Clamp(Mathf.RoundToInt(3f + (float)data.damage * 0.3f), 3, 25);
				tooBig = actor != null && ((BaseSimObject)actor).stats["size"] > 2f * Mathf.Max(0.05f, ((BaseSimObject)you).stats["size"]);
				bool flag = HasStamina(you) && you.getStamina() < num;
				if (tooBig || flag)
				{
					return Guarded.Broken;
				}
				if (HasStamina(you))
				{
					you.spendStamina(num);
				}
				int num2 = (LastChip = Mathf.RoundToInt((float)data.damage * chip));
				LastCost = num;
				if (num2 >= 1)
				{
					((BaseSimObject)you).getHit((float)num2, pFlash: true, data.attack_type, by, pSkipIfShake: false, data.metallic_weapon, pCheckDamageReduction: true);
				}
				result = AttackDataResult.Block;
				return Guarded.Blocked;
			}
			catch (Exception e)
			{
				Report(e);
				result = AttackDataResult.Continue;
				return Guarded.None;
			}
		}

		internal void TakenOnHost(Guarded what, Actor you, BaseSimObject by, bool shot)
		{
			if (you == null || you != _owner || !Active)
			{
				return;
			}
			try
			{
				Vector2 current_position = you.current_position;
				Vector2 vector = ((by != null && by.isAlive()) ? by.current_position : current_position);
				AttackData data = new AttackData(by, you.current_tile, new Vector3(current_position.x, current_position.y, ((BaseSimObject)you).getHeight()), vector, null, null, AttackType.Weapon, pMetallicWeapon: false, pSkipShake: false, shot, null, null, 0.05f);
				switch (what)
				{
				case Guarded.Parried:
					State.Parried();
					Parried++;
					SinceParry = 0f;
					Blocks.Guarded(data, you, FirstPerson.Stop.Parried);
					break;
				case Guarded.Blocked:
					Blocked++;
					Blocks.Guarded(data, you, FirstPerson.Stop.Blocked);
					break;
				case Guarded.Broken:
					State.Stagger();
					Broken++;
					SinceBroken = 0f;
					WorldBoxMod.Instance?.GuardBroken("broken");
					break;
				}
			}
			catch (Exception e)
			{
				Report(e);
			}
		}

		private static bool HasStamina(Actor a)
		{
			try
			{
				return ((BaseSimObject)a).stats["stamina"] > 0f;
			}
			catch
			{
				return false;
			}
		}

		private static bool CanBeBlocked(string projectile)
		{
			try
			{
				return (string.IsNullOrEmpty(projectile) ? null : AssetManager.projectiles.get(projectile))?.can_be_blocked ?? false;
			}
			catch
			{
				return false;
			}
		}

		internal static bool Riposte(ref AttackData data)
		{
			GuardControl current = Current;
			if (current == null || data.is_projectile || !current.State.Riposte)
			{
				return false;
			}
			if (!Critical(ref data))
			{
				return false;
			}
			current.State.TakeRiposte();
			Ripostes++;
			return true;
		}

		internal static bool Critical(ref AttackData data)
		{
			try
			{
				object obj = data;
				CriticalField.SetValue(obj, true);
				AttackData attackData = (AttackData)obj;
				if (!attackData.critical)
				{
					return false;
				}
				data = attackData;
				return true;
			}
			catch (Exception e)
			{
				Report(e);
				return false;
			}
		}

		public void Stop()
		{
			State.Reset();
			Active = false;
			HeldForTest = (PressedForTest = false);
			if (Current == this)
			{
				Current = null;
			}
			PossessionHooks.GuardSlow = 0f;
		}

		private static void Report(Exception e)
		{
			if (!_reported)
			{
				_reported = true;
				Log.Error("guard: " + e);
			}
		}
	}
}
