using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class ShotgunControl
	{
		public readonly ShotgunState State = new ShotgunState();

		private Actor _owner;

		private string _weapon;

		private int _lastAttacks = -1;

		private bool _fireAfter;

		private bool _reloadAsked;

		private float _dryCooldown;

		private float _emptyFor;

		private float _wispClock;

		private int _shellsHeard = -1;

		private int _pattersHeard = -1;

		private Actor _keptOwner;

		private string _keptWeapon;

		private int _keptShells = 6;

		internal int ShotsHeard;

		internal int PumpsHeard;

		internal int ShellsIn;

		internal int Ejections;

		internal int DryClicks;

		private bool _testClick;

		public bool InHand { get; private set; }

		public bool Blocks
		{
			get
			{
				if (InHand)
				{
					return State.Blocked;
				}
				return false;
			}
		}

		public float ViewKick { get; private set; }

		public float ChangedAt { get; private set; } = -100f;

		public static bool IsShotgun(string weaponId)
		{
			if (weaponId != null)
			{
				return ViewMeshes.KindOf(weaponId) == WeaponKind.Shotgun;
			}
			return false;
		}

		public void Update(Actor host, string weaponId, int attacks, bool lastMelee, bool canShoot, bool keysFree, float dt, FirstPersonAudio audio, Hands hands, MeleeControl melee, CombatFeedback feedback, Vector3 eye)
		{
			bool flag = IsShotgun(weaponId);
			if (host != _owner || weaponId != _weapon)
			{
				if (IsShotgun(_weapon) && _owner != null)
				{
					_keptOwner = _owner;
					_keptWeapon = _weapon;
					_keptShells = State.Loaded;
				}
				State.Reset();
				if (flag && host == _keptOwner && weaponId == _keptWeapon)
				{
					State.Loaded = _keptShells;
				}
				_owner = host;
				_weapon = weaponId;
				_lastAttacks = attacks;
				_fireAfter = (_reloadAsked = false);
				if (flag)
				{
					State.Equip();
				}
			}
			InHand = flag;
			if (!flag)
			{
				ViewKick = 0f;
				return;
			}
			if (attacks != _lastAttacks)
			{
				if (_lastAttacks >= 0 && !lastMelee && State.Chambered)
				{
					Fire(audio, hands, feedback);
				}
				_lastAttacks = attacks;
			}
			_dryCooldown -= dt;
			bool flag2 = PlayInput.GetMouseButtonDown(0) || _testClick;
			_testClick = false;
			if (canShoot && flag2)
			{
				if (State.Reloading)
				{
					State.StopReload();
					_fireAfter = State.Stopping;
				}
				else if (State.State == ShotgunState.Phase.Ready && (State.Loaded <= 0 || !State.Chambered))
				{
					Dry(audio);
					if (State.Loaded < 6)
					{
						_reloadAsked = true;
					}
				}
			}
			if (keysFree && PlayInput.GetKeyDown(KeyCode.R))
			{
				_reloadAsked = true;
			}
			_emptyFor = ((State.Loaded <= 0 && State.State == ShotgunState.Phase.Ready) ? (_emptyFor + dt) : 0f);
			if (_emptyFor > 0.25f && canShoot)
			{
				_reloadAsked = true;
			}
			if (_reloadAsked && State.State == ShotgunState.Phase.Ready && canShoot)
			{
				State.StartReload();
				_reloadAsked = false;
			}
			State.Update(Mathf.Min(dt, 0.1f));
			ViewKick = Hands.ShotgunKick(State.SinceShot) * 2.2f;
			if (audio != null)
			{
				if (State.PumpedBack)
				{
					audio.PlayShotgun("shotgun_pump_back", 0.6f, 0.1f);
				}
				if (State.PumpedForward)
				{
					audio.PlayShotgun("shotgun_pump_fwd", 0.7f, 0.08f);
					PumpsHeard++;
				}
				if (State.StartedReload)
				{
					audio.PlayEffect("cloth", 0.28f);
				}
				if (State.ShellTaken)
				{
					audio.PlayEffect("cloth", 0.16f);
				}
				if (State.ShellPushed)
				{
					audio.PlayShotgun("shotgun_shell_in", 0.62f, -0.05f);
					ShellsIn++;
					ChangedAt = Time.unscaledTime;
				}
			}
			else if (State.ShellPushed)
			{
				ShellsIn++;
				ChangedAt = Time.unscaledTime;
			}
			if (State.Ejected && hands != null && hands.ShotgunShown)
			{
				float shotgunScale = hands.ShotgunScale;
				Vec3 velocity = hands.ShotgunPortDir * (1.5f * shotgunScale) + new Vec3(0f, 0f, 1.5f * shotgunScale) + hands.ShotgunBack * (0.3f * shotgunScale);
				SceneCollector.EjectShell(hands.ShotgunPort, velocity, hands.ShotgunMuzzleDir, shotgunScale, hands.ShotgunPortDir);
				Ejections++;
			}
			if (State.FinishedReload && _fireAfter)
			{
				_fireAfter = false;
				melee?.QueueClick();
			}
			if (audio != null)
			{
				if (_shellsHeard >= 0 && SceneCollector.ShellsLanded != _shellsHeard)
				{
					Vec3 shellLandedAt = SceneCollector.ShellLandedAt;
					float num = Mathf.Sqrt((shellLandedAt.X - eye.x) * (shellLandedAt.X - eye.x) + (shellLandedAt.Y - eye.y) * (shellLandedAt.Y - eye.y) + (shellLandedAt.Z - eye.z) * (shellLandedAt.Z - eye.z));
					float num2 = 0.5f / (1f + num * num * 0.08f);
					if (num2 > 0.03f)
					{
						audio.PlayShotgun("shotgun_shell_drop", num2, 0.2f);
					}
				}
				if (_pattersHeard >= 0 && FirstPersonShots.Patters != _pattersHeard)
				{
					Vec3 patterAt = FirstPersonShots.PatterAt;
					float num3 = Mathf.Sqrt((patterAt.X - eye.x) * (patterAt.X - eye.x) + (patterAt.Y - eye.y) * (patterAt.Y - eye.y));
					float num4 = 0.45f / (1f + num3 * 0.15f);
					if (num4 > 0.04f)
					{
						audio.PlayShotgun("pellet_patter", num4);
					}
				}
			}
			_shellsHeard = SceneCollector.ShellsLanded;
			_pattersHeard = FirstPersonShots.Patters;
		}

		private void Fire(FirstPersonAudio audio, Hands hands, CombatFeedback feedback)
		{
			State.Shot();
			ShotsHeard++;
			ChangedAt = Time.unscaledTime;
			audio?.PlayShotgun("shotgun_blast", 0.95f);
			if (hands != null && hands.ShotgunShown)
			{
				SceneCollector.OwnShot(hands.ShotgunMuzzle, hands.ShotgunMuzzleDir, hands.ShotgunScale);
			}
			if (feedback != null)
			{
				feedback.Shake = Mathf.Max(feedback.Shake, 0.3f);
			}
			_wispClock = 0f;
		}

		private void Dry(FirstPersonAudio audio)
		{
			if (!(_dryCooldown > 0f))
			{
				State.Dry();
				DryClicks++;
				_dryCooldown = 0.25f;
				audio?.PlayShotgun("shotgun_dry", 0.6f);
			}
		}

		public void Emit(Hands hands, float dt)
		{
			if (!InHand || hands == null || !hands.ShotgunShown)
			{
				return;
			}
			FirstPersonShots.GunMuzzle(hands.ShotgunMuzzleFromEye);
			float sinceShot = State.SinceShot;
			if (sinceShot > 0.25f && sinceShot < 1.6f && dt > 0f)
			{
				_wispClock += dt;
				if (_wispClock > 0.14f)
				{
					_wispClock = 0f;
					byte alpha = (byte)(70f * (1f - (sinceShot - 0.25f) / 1.35f) + 10f);
					ShotgunFx.Wisp(SceneCollector.FxPool, hands.ShotgunMuzzle, new Vec3(0f, 0f, 1f), hands.ShotgunScale * 0.8f, (uint)(sinceShot * 1000f), alpha);
				}
			}
		}

		internal void LoadForTest(int shells)
		{
			State.Reset();
			State.Loaded = Mathf.Clamp(shells, 0, 6);
			State.Chambered = State.Loaded > 0;
		}

		internal void ReloadForTest()
		{
			_reloadAsked = true;
		}

		internal void ClickForTest()
		{
			_testClick = true;
		}

		public void Stop()
		{
			InHand = false;
			if (IsShotgun(_weapon) && _owner != null)
			{
				_keptOwner = _owner;
				_keptWeapon = _weapon;
				_keptShells = State.Loaded;
			}
			State.Reset();
			ViewKick = 0f;
			_owner = null;
			_weapon = null;
		}
	}
}
