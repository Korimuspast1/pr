using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class BowControl
	{
		public readonly BowState State = new BowState();

		private Actor _owner;

		private string _weapon;

		private double _releaseStamp;

		private float _releaseFor;

		private Hands _hands;

		public bool Tracked;

		internal bool HeldForTest;

		private bool _releasing;

		public bool InHand { get; private set; }

		public bool OnString { get; private set; }

		public float Draw
		{
			get
			{
				if (!InHand)
				{
					return 0f;
				}
				return State.Draw;
			}
		}

		public float Steady { get; private set; }

		public void Update(Actor host, string weaponId, bool canShoot, float runBlend, float dt, FirstPersonAudio audio, Hands hands = null)
		{
			_hands = hands;
			bool flag = weaponId != null && ViewMeshes.KindOf(weaponId) == WeaponKind.Bow;
			if (host != _owner || weaponId != _weapon)
			{
				State.Reset();
				_owner = host;
				_weapon = weaponId;
			}
			InHand = flag;
			PossessionHooks.HoldAttack = flag;
			if (audio != null)
			{
				audio.BowSounds = flag;
			}
			float num = (flag ? Mathf.Clamp01(State.Draw / 0.6f) : 0f);
			Steady = ((num > Steady) ? num : Mathf.MoveTowards(Steady, num, dt * 2.5f));
			if (!flag)
			{
				if (_releasing)
				{
					_releasing = false;
					PossessionHooks.ReleaseAttack = false;
				}
				return;
			}
			if (!PossessionHooks.ReleaseAttack)
			{
				_releasing = false;
			}
			if (PossessionHooks.ReleaseAttack)
			{
				_releaseFor += dt;
				if ((host?._possession_attack_happened_frame ?? 0.0) != _releaseStamp || _releaseFor > 1f)
				{
					PossessionHooks.ReleaseAttack = false;
				}
			}
			bool flag2 = PlayInput.GetMouseButton(0) || HeldForTest;
			bool flag3 = canShoot && runBlend < 0.6f;
			if (Tracked && hands != null)
			{
				if (!flag2 || !flag3)
				{
					OnString = false;
				}
				else if (!OnString && PlayInput.GetMouseButtonDown(0) && hands.TrackNockNear && State.ArrowNocked)
				{
					OnString = true;
				}
				State.UpdatePulled(Mathf.Min(dt, 0.1f), OnString ? Mathf.Max(0f, hands.TrackPull) : (-1f), flag3);
				if (State.Fired || State.LetDown)
				{
					OnString = false;
				}
				if (State.Fired && host != null)
				{
					Release(host);
				}
				audio?.Bow(State);
			}
			else
			{
				OnString = false;
				if (flag3 && PlayInput.GetMouseButtonDown(0))
				{
					State.Press();
				}
				State.Update(Mathf.Min(dt, 0.1f), flag2, flag3);
				if (State.Fired && host != null)
				{
					Release(host);
				}
				audio?.Bow(State);
			}
		}

		internal void LooseForTest(Actor host)
		{
			State.Pose(BowState.Phase.Held, 0.5f, 1f);
			State.Update(0.016f, held: false, allowed: true);
			Release(host);
		}

		private void Release(Actor host)
		{
			bool flag = _hands != null && Time.unscaledTime - _hands.ArrowTipTime < 0.5f && _hands.ArrowTipRaise > 0.5f;
			if (Tracked && _hands != null && _hands.TrackAimValid)
			{
				FirstPersonAim.HoldShot(_hands.TrackAimFrom, _hands.TrackAimDir);
			}
			FirstPersonShots.Loosed(State.LastShotDraw, flag ? new Vec3?(_hands.ArrowTipFromEye) : ((Vec3?)null));
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

		public void Stop()
		{
			_releasing = false;
			InHand = false;
			OnString = false;
			Steady = 0f;
			State.Reset();
			PossessionHooks.HoldAttack = false;
			PossessionHooks.ReleaseAttack = false;
		}
	}
}
