using FirstPerson.Core;
using FirstPerson.Vr;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class MeleeControl
	{
		public readonly StrikeState State = new StrikeState();

		private Actor _owner;

		private string _weapon;

		private double _releaseStamp;

		private float _releaseFor;

		private bool _releasing;

		private bool _waitRelease;

		private bool _charging;

		private bool _dashBlow;

		internal bool HeldForTest;

		private bool _clickQueued;

		private float _clickQueuedFor;

		private bool _jumpBlow;

		private bool _heavyBlow;

		private const float HeavyChargeTime = 0.6f;

		private const float HeavyLeast = 0.9f;

		public bool Active { get; private set; }

		public float Readiness
		{
			get
			{
				if (!Active)
				{
					return 1f;
				}
				return State.Readiness;
			}
		}

		public float ViewPitch { get; private set; }

		public float Charge { get; private set; }

		public float AttackClock(float otherwise)
		{
			if (!Active || !State.Swinging)
			{
				return otherwise;
			}
			return State.Time;
		}

		internal void PressForTest()
		{
			QueueClick();
		}

		internal void QueueClick()
		{
			_clickQueued = true;
			_clickQueuedFor = 0f;
		}

		public void Update(Actor host, string weaponId, bool hands, bool canStrike, float lean, float dt, CombatFeedback feedback, FirstPersonAudio audio)
		{
			WeaponKind weaponKind = ((!string.IsNullOrEmpty(weaponId)) ? ViewMeshes.KindOf(weaponId) : WeaponKind.None);
			bool flag = host != null && weaponKind != WeaponKind.Bow && !Crabzillas.Is(host);
			if (host != _owner || weaponId != _weapon)
			{
				State.Reset();
				_owner = host;
				_weapon = weaponId;
			}
			if (audio != null)
			{
				audio.SwingSounds = flag;
			}
			if (!flag)
			{
				if (Active)
				{
					Let();
				}
				Active = false;
				ViewPitch = 0f;
				_charging = (_dashBlow = false);
				Charge = 0f;
				PossessionHooks.ChargeSlow = 0f;
				return;
			}
			Active = true;
			PossessionHooks.HoldAttack = true;
			if (_releasing)
			{
				_releaseFor += dt;
				if (host._possession_attack_happened_frame != _releaseStamp || _releaseFor > 0.6f)
				{
					Let();
				}
			}
			bool flag2 = true;
			try
			{
				flag2 = host.hasMeleeAttack();
				if (host.asset.skip_fight_logic)
				{
					canStrike = false;
				}
			}
			catch
			{
				canStrike = false;
			}
			StrikeTiming timing = StrikeTiming.For(weaponKind, flag2, hands);
			float total = timing.Total;
			int num;
			if (canStrike && !PossessionHooks.BlockAttack)
			{
				GuardControl current = GuardControl.Current;
				num = ((current == null || !current.State.Staggered) ? 1 : 0);
			}
			else
			{
				num = 0;
			}
			bool flag3 = (byte)num != 0;
			bool flag4 = PlayInput.GetMouseButton(0) || HeldForTest;
			if (!flag3 && flag4)
			{
				_waitRelease = true;
			}
			if (!flag4)
			{
				_waitRelease = false;
			}
			if (_waitRelease)
			{
				flag4 = false;
			}
			bool flag5 = PlayInput.GetMouseButtonDown(0) || _clickQueued;
			_clickQueuedFor += dt;
			if (flag3 || _clickQueuedFor > 0.5f)
			{
				_clickQueued = false;
			}
			if (flag3 && flag5)
			{
				State.Press();
			}
			Moves.Move move = Moves.Ready(host);
			bool flag6 = !flag2 || weaponKind == WeaponKind.Gun || weaponKind == WeaponKind.Shotgun;
			bool flag7 = move == Moves.Move.Heavy && hands && !flag6;
			Charging(host, flag7, flag4, flag3, in timing, dt);
			State.Update(Mathf.Min(dt, 0.1f), flag4 && !flag7, flag3, timing, total);
			if (State.Started)
			{
				feedback?.SwingStarted(flag2);
				Moves.Pending = 1f;
				_jumpBlow = move == Moves.Move.JumpAttack && PossessionHooks.Leaping && !flag6;
			}
			if (State.StruckOut)
			{
				audio?.PlaySwing(weaponKind, flag2, hands);
				if (!_dashBlow && !_jumpBlow && move == Moves.Move.Lunge && !flag6 && !VrControls.Active && PossessionHooks.RunBlend > 0.6f && !PossessionHooks.Leaping && !PossessionHooks.Dashing)
				{
					float num2 = Mathf.Max(0.3f, PossessionHooks.BodyRadius / 0.17f) / 0.9f;
					PossessionHooks.Dash(host, FirstPersonAim.Flat, 1.3f * num2, 0.24f, PossessionHooks.Velocity.magnitude, slow: false);
					_dashBlow = true;
					audio?.PlayDash(0.4f);
				}
				else if (!_dashBlow && !_jumpBlow)
				{
					StepIn(host, weaponKind, flag2, hands, in timing, _heavyBlow ? 1.5f : 1f);
				}
			}
			if (State.Landed)
			{
				Strike(host);
				_dashBlow = (_jumpBlow = (_heavyBlow = false));
			}
			ViewPitch = Lean(State, lean);
		}

		private void Charging(Actor host, bool heavy, bool held, bool allowed, in StrikeTiming timing, float dt)
		{
			PossessionHooks.ChargeSlow = 0f;
			bool flag = false;
			try
			{
				flag = SceneCollector.IsSwimming(host);
			}
			catch
			{
			}
			bool flag2 = heavy && allowed && !flag && !PossessionHooks.Leaping;
			if (!State.Swinging)
			{
				_charging = (_dashBlow = (_jumpBlow = (_heavyBlow = false)));
				Charge = 0f;
				State.HoldAt = -1f;
			}
			else if (_dashBlow)
			{
				if (PossessionHooks.Dashing && FirstPersonAim.Melee(host, PossessionCombat.ReachOf(host)) == null)
				{
					State.HoldAt = Mathf.Max(State.Time, timing.Hit - 0.005f);
					return;
				}
				if (PossessionHooks.Dashing)
				{
					PossessionHooks.EndDash();
				}
				State.HoldAt = -1f;
			}
			else if (_jumpBlow)
			{
				if (PossessionHooks.Leaping && FirstPersonAim.Melee(host, PossessionCombat.ReachOf(host)) == null)
				{
					State.HoldAt = Mathf.Max(State.Time, timing.Hit - 0.005f);
					return;
				}
				State.HoldAt = -1f;
				Moves.Pending = 1.5f;
			}
			else if (!heavy)
			{
				_charging = false;
				Charge = 0f;
			}
			else if (!_charging)
			{
				if (held && flag2 && State.Time < timing.StrikeStart)
				{
					State.HoldAt = timing.StrikeStart - 0.002f;
				}
				else if (!held)
				{
					State.HoldAt = -1f;
				}
				if (held && flag2 && State.HoldAt >= 0f && State.Time >= State.HoldAt - 0.001f)
				{
					_charging = true;
					Charge = 0f;
				}
			}
			else if (held && flag2)
			{
				Charge = Mathf.Min(1f, Charge + dt / 0.6f);
				State.HoldAt = timing.StrikeStart - 0.002f;
				PossessionHooks.ChargeSlow = 1f;
			}
			else
			{
				_charging = false;
				State.HoldAt = -1f;
				if (flag2 && Charge >= 0.9f)
				{
					Moves.Pending = 1.6f;
					_heavyBlow = true;
				}
				Charge = 0f;
			}
		}

		private static void StepIn(Actor host, WeaponKind kind, bool melee, bool hands, in StrikeTiming timing, float more = 1f)
		{
			if (!melee || kind == WeaponKind.Gun || kind == WeaponKind.Shotgun || kind == WeaponKind.Bow || VrControls.Active)
			{
				return;
			}
			try
			{
				if (SceneCollector.IsSwimming(host))
				{
					return;
				}
			}
			catch
			{
				return;
			}
			float num;
			if (!hands)
			{
				num = 0.15f;
			}
			else
			{
				switch (kind)
				{
				case WeaponKind.None:
					num = 0.12f;
					break;
				case WeaponKind.Dagger:
					num = 0.15f;
					break;
				case WeaponKind.Stick:
				case WeaponKind.Staff:
					num = 0.17f;
					break;
				case WeaponKind.Sword:
					num = 0.2f;
					break;
				case WeaponKind.Spear:
					num = 0.25f;
					break;
				case WeaponKind.Axe:
					num = 0.28f;
					break;
				case WeaponKind.Hammer:
					num = 0.32f;
					break;
				default:
					num = 0.15f;
					break;
				}
			}
			float num2 = Mathf.Max(0.3f, PossessionHooks.BodyRadius / 0.17f) / 0.9f;
			PossessionHooks.Surge(FirstPersonAim.Flat, num * num2 * more, timing.Strike + 0.1f);
		}

		private static float Lean(StrikeState s, float scale)
		{
			if (!s.Swinging)
			{
				return 0f;
			}
			StrikeTiming timing = s.Timing;
			if (timing.Weight <= 0f)
			{
				return 0f;
			}
			float time = s.Time;
			float num = ((time < timing.StrikeStart) ? (0.35f * Smooth(time / Mathf.Max(0.01f, timing.StrikeStart))) : ((!(time < timing.StrikeEnd)) ? (-1f + Smooth((time - timing.StrikeEnd) / Mathf.Max(0.05f, (s.Cadence - timing.StrikeEnd) * 0.7f))) : (0.35f - 1.35f * Smooth((time - timing.StrikeStart) / Mathf.Max(0.01f, timing.Strike)))));
			return num * 1.3f * timing.Weight * scale;
		}

		private static float Smooth(float x)
		{
			x = Mathf.Clamp01(x);
			return x * x * (3f - 2f * x);
		}

		private void Strike(Actor host)
		{
			try
			{
				host.resetAttackTimeout();
			}
			catch
			{
			}
			PossessionHooks.ReleaseAttack = true;
			_releasing = true;
			_releaseStamp = host._possession_attack_happened_frame;
			_releaseFor = 0f;
		}

		private void Let()
		{
			if (_releasing)
			{
				PossessionHooks.ReleaseAttack = false;
			}
			_releasing = false;
		}

		internal void ResetForTest()
		{
			State.Reset();
		}

		public void Stop()
		{
			Let();
			State.Reset();
			Active = false;
			ViewPitch = 0f;
			HeldForTest = false;
			_charging = (_dashBlow = false);
			Charge = 0f;
			PossessionHooks.ChargeSlow = 0f;
		}
	}
}
