using System;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class BlasterControl
	{
		public readonly BlasterState State = new BlasterState();

		private Actor _owner;

		private string _weapon;

		private int _lastAttacks = -1;

		private float _steamClock;

		private float _smokeClock;

		private float _dryCooldown;

		private System.Random _rng = new System.Random(7);

		public bool InHand { get; private set; }

		public bool Blocks
		{
			get
			{
				if (InHand)
				{
					return State.Locked;
				}
				return false;
			}
		}

		public static bool IsGun(string weaponId)
		{
			if (weaponId != null)
			{
				return ViewMeshes.KindOf(weaponId) == WeaponKind.Gun;
			}
			return false;
		}

		public void Update(Actor host, string weaponId, int attacks, bool lastMelee, bool keysFree, float dt, FirstPersonAudio audio)
		{
			bool flag = IsGun(weaponId);
			if (host != _owner || weaponId != _weapon)
			{
				State.Reset();
				_owner = host;
				_weapon = weaponId;
				_lastAttacks = attacks;
			}
			InHand = flag;
			if (!flag)
			{
				return;
			}
			if (attacks != _lastAttacks)
			{
				if (_lastAttacks >= 0 && !lastMelee)
				{
					State.Shot();
				}
				_lastAttacks = attacks;
			}
			_dryCooldown -= dt;
			if (keysFree && State.Locked && PlayInput.GetMouseButtonDown(0) && _dryCooldown <= 0f)
			{
				audio?.PlayBlaster("blaster_dry", 0.6f);
				_dryCooldown = 0.25f;
			}
			if (keysFree && PlayInput.GetKeyDown(KeyCode.R))
			{
				State.StartVent();
			}
			State.Update(Mathf.Min(dt, 0.1f));
			if (audio != null)
			{
				if (State.Alarm)
				{
					audio.PlayBlaster("blaster_alarm", 0.55f);
				}
				if (State.StartedVent)
				{
					audio.PlayBlaster("cloth", 0.5f);
				}
				if (State.Twisted)
				{
					audio.PlayBlaster("blaster_hiss", 0.85f);
				}
				if (State.Clicked)
				{
					audio.PlayBlaster("blaster_ratchet", 0.75f);
				}
			}
		}

		internal void ShootForTest(int shots)
		{
			for (int i = 0; i < shots; i++)
			{
				State.Shot();
			}
		}

		public void Emit(Hands hands, FxParticles parts, float dt)
		{
			if (!InHand || parts == null || hands == null || !hands.BlasterShown || dt <= 0f)
			{
				return;
			}
			BlasterState state = State;
			float num = 1f;
			float num2 = ((!state.Venting) ? (state.Overheated ? 5f : 0f) : ((state.VentProgress > 0.3f && state.VentProgress < 0.95f) ? 70f : 12f));
			_steamClock += dt * num2;
			while (_steamClock > 1f && hands.BlasterVents.Count > 0)
			{
				_steamClock -= 1f;
				int index = _rng.Next(hands.BlasterVents.Count);
				Vec3 pos = hands.BlasterVents[index];
				Vec3 vec = hands.BlasterVentDirs[index];
				float num3 = (state.Venting ? (1.2f + (float)_rng.NextDouble() * 1.4f) : 0.25f);
				Vec3 vel = vec * num3 + new Vec3((float)(_rng.NextDouble() - 0.5) * 0.3f, (float)(_rng.NextDouble() - 0.5) * 0.3f, 0.35f);
				float num4 = (state.Venting ? 0.05f : 0.03f) * num;
				parts.Add(new FxParticles.P
				{
					Pos = pos,
					Vel = vel,
					Life = 0.7f + (float)_rng.NextDouble() * 0.6f,
					Size = num4,
					EndSize = num4 * 7f,
					Color = TerrainCache.Rgb(238, 242, 246),
					EndColor = TerrainCache.Rgb(210, 214, 220),
					Alpha = (byte)(state.Venting ? 210u : 120u),
					Variant = _rng.Next(6),
					Drag = 2.4f,
					Gravity = -0.4f,
					Kind = FxParticles.Kind.Steam
				});
			}
			if (state.BarrelHeat > 0.3f && !state.Venting)
			{
				_smokeClock += dt * 10f * Mathf.Clamp01((state.BarrelHeat - 0.3f) / 0.6f);
				while (_smokeClock > 1f)
				{
					_smokeClock -= 1f;
					float num5 = 0.025f;
					parts.Add(new FxParticles.P
					{
						Pos = hands.BlasterMuzzle + new Vec3(0f, 0f, 0.01f),
						Vel = new Vec3((float)(_rng.NextDouble() - 0.5) * 0.08f, (float)(_rng.NextDouble() - 0.5) * 0.08f, 0.22f),
						Life = 1.3f,
						Size = num5,
						EndSize = num5 * 6f,
						Color = TerrainCache.Rgb(150, 146, 150),
						EndColor = TerrainCache.Rgb(190, 190, 196),
						Alpha = 90,
						Variant = _rng.Next(6),
						Drag = 1.5f,
						Gravity = -0.15f,
						Kind = FxParticles.Kind.Smoke
					});
				}
			}
		}

		public void Stop()
		{
			InHand = false;
			State.Reset();
		}
	}
}
