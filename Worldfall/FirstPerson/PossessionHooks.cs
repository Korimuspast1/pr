using System;
using System.Reflection;
using FirstPerson.Core;
using FirstPerson.Pad;
using FirstPerson.Towns;
using FirstPerson.Vr;
using UnityEngine;

namespace FirstPerson
{
	internal static class PossessionHooks
	{
		public static bool Active;

		public static float Yaw;

		public static Vector2 AimPoint;

		public static bool Frozen;

		public static bool BlockAttack;

		public static bool HoldAttack;

		public static bool ReleaseAttack;

		public static Func<bool> KeysTaken;

		public static Vector2 ScriptedInput;

		public static bool ScriptedRun;

		public static bool ScriptedJump;

		public static bool ScriptedSteal;

		public static float RunFactor = 0.6f;

		public const float WalkFactor = 0.35f;

		public static bool Walking;

		public static bool Crouched;

		public const float CrouchPace = 0.5f;

		public const float SprintFactor = 1.35f;

		public static float SprintCap = 1.35f;

		public static float GuardSlow;

		public const float GuardPace = 0.6f;

		private static float _bodyRadius = 0.16f;

		private static int _easedFrame = -1;

		private static bool _installed;

		private static string _error;

		private static bool _loggedRuntimeError;

		private static int _provenCalls;

		internal static bool SceneHoldsKeys;

		private static readonly Momentum _momentum = new Momentum();

		private static Actor _momentumOf;

		private const float SidePace = 0.85f;

		private const float BackPace = 0.7f;

		private static float _altDownAt = -1f;

		private const float AltTap = 0.35f;

		private static readonly Leap _leap = new Leap();

		private static int _leapFrame = -1;

		private static float _jumpKeptAt = -99f;

		public const float JumpKeep = 0.12f;

		public static float GroundNow;

		public static float FeetZ;

		private const float TakeOffPush = 0.6f;

		private const float TakeOffLeast = 0.65f;

		private static float _realTime = 1f;

		private static int _realTimeFrame = -1;

		private static float _climb = 1f;

		internal static int YourStepsForTest;

		internal static float SlideForTest = 1f;

		private static Vector2 _surgeDir;

		private static float _surgeSpeed;

		private static float _surgeTime = 99f;

		private static float _surgeLength = 1f;

		public const float DashEnd = 1.4f;

		public const float SlowSeconds = 1f;

		public const float SlowPace = 0.4f;

		public const float DashJumpWindow = 0.2f;

		public const float HopKeep = 0.9f;

		public const float HopGrace = 0.08f;

		private const float SlowBrake = 4f;

		private static Vector2 _dashDir;

		private static float _dashTime = 99f;

		private static float _dashLength = 1f;

		private static float _dashStart;

		private static float _dashEnd;

		private static float _dashOverAt = -99f;

		private static float _landedAt = -99f;

		private static float _dashOverSpeed;

		private static bool _slowOwed;

		private static bool _hopping;

		public static float ChargeSlow;

		internal static int DashesForTest;

		internal static int HopsForTest;

		internal static int DashJumpsForTest;

		private static bool _dashSlow = true;

		private const float DodgeLength = 0.95f;

		private const float DodgeHop = 0.12f;

		private const float DodgeAgain = 0.8f;

		private static float _dodgeAt = -99f;

		private static bool _dodging;

		internal static int DodgesForTest;

		internal static bool ScriptedDodge;

		private const float TapWindow = 0.25f;

		private static readonly float[] _tapAt = new float[4] { -1f, -1f, -1f, -1f };

		private static readonly bool[] _tapUp = new bool[4] { true, true, true, true };

		private static bool _backstepTapped;

		public static float Pace { get; private set; }

		public static bool Running { get; private set; }

		public static float RunBlend { get; private set; }

		public static float BodyRadius
		{
			get
			{
				return _bodyRadius;
			}
			set
			{
				_bodyRadius = value;
				Colliders.GapRadius = value;
			}
		}

		public static bool Installed => _installed;

		public static string Error => _error;

		public static Feel Feel { get; private set; }

		public static float WalkBlend { get; private set; }

		public static bool KeysHeld { get; private set; }

		public static Vector2 Velocity => new Vector2(_momentum.Vx, _momentum.Vy);

		public static bool FallHooked { get; private set; }

		public static Leap Jump => _leap;

		public static bool Leaping => _leap.Active;

		public static Actor LeapOf { get; private set; }

		public static float LeapBase { get; private set; }

		internal static float RealTimeForTest => _realTime;

		internal static float ClimbForTest => Slope(ControllableUnit._unit_main);

		public static bool Dashing => _dashTime < _dashLength;

		public static float SlowLeft { get; private set; }

		public static bool Hopping => _hopping;

		public static void Install()
		{
			if (_installed || _error != null)
			{
				return;
			}
			Type target = typeof(ControllableUnit);
			Type self = typeof(PossessionHooks);
			if (Detour.Platform != Detour.Kind.WindowsX64)
			{
				Log.Info("possession input hooks for " + Detour.Describe());
			}
			if (!Detour.Begin())
			{
				_error = "skipped after a crash on this platform";
				return;
			}
			try
			{
				Detour.Apply(target.GetMethod("updateMovementVectorKeyboard", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), self.GetMethod("UpdateMovementVectorKeyboard", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				Detour.Apply(target.GetMethod("updateMouseAttackPosition", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), self.GetMethod("UpdateMouseAttackPosition", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				_installed = true;
			}
			catch (Exception ex)
			{
				_error = ex.GetType().Name + ": " + ex.Message;
				Log.Error("could not hook possession input, movement stays world-relative: " + ex);
				Detour.End();
				return;
			}
			Optional(delegate
			{
				Detour.Apply(target.GetMethod("updateMovementVectorJoystick", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), self.GetMethod("UpdateMovementVectorJoystick", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
			}, "could not take over the phone's stick, first person can't walk on a phone build");
			Optional(delegate
			{
				Detour.Apply(target.GetMethod("checkActions", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), self.GetMethod("CheckActions", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
			}, "could not take over the dash key, Shift still dashes");
			Optional(delegate
			{
				Detour.Apply(target.GetMethod("isMovementActionActive", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), self.GetMethod("IsMovementActionActive", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
			}, "could not take over the movement check, running may stop you");
			Optional(delegate
			{
				Detour.Apply(typeof(Actor).GetMethod("getMismatchFactorForSideMovement", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), self.GetMethod("MismatchFactor", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
			}, "could not take over side-movement slowdown");
			Optional(delegate
			{
				Detour.Apply(typeof(Actor).GetMethod("updatePossessedMovementTowards", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), self.GetMethod("PossessedStep", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
			}, "could not take over the possessed step, your pace follows the frame rate");
			Optional(delegate
			{
				Detour.Apply(typeof(Actor).GetMethod("applyRandomForce", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), self.GetMethod("RandomForce", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
			}, "could not take over the random hops, your view may bounce");
			Optional(delegate
			{
				Detour.Apply(typeof(StatusLibrary).GetMethod("checkJump", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), typeof(Jumps).GetMethod("CheckJump", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
			}, "could not take over jumping, jumps keep the game's heights");
			Optional(delegate
			{
				Detour.Apply(typeof(Actor).GetMethod("updateFall", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), typeof(Jumps).GetMethod("UpdateFall", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				FallHooked = true;
			}, "could not take over the fall, your jumps keep the game's arc");
			Optional(WalkPace.Install, "could not take over the walking step, the creatures near you keep the game's pace");
			Optional(OwnMind.Install, "could not take over the fighting steps, the creature you control may still fight by itself");
			Optional(LoveGuard.Install, "could not take over falling in love, the game may pair off the creature you control");
			Optional(CrabGuard.Install, "could not take over Crabzilla's controls, another player's Crabzilla may take yours");
			Optional(DryBoards.Install, "could not take over the tile step, a dock's boards still drain stamina and drown");
			Optional(WadeChecks.Install, "could not take over the creatures' checks, people wading are still drawn swimming and stay out of fights");
			Optional(DockGround.Install, "could not take over the harbour's update, a harbour whose water was filled falls at the next load");
			Optional(Climbing.Install, "could not take over the block check, the mountains stay out of bounds");
			Optional(Butchery.Install, "could not take over the meat a kill gives, your kills fill your bag at once as before");
			Optional(CrownGuard.Install, "could not take over the game's choice of kings and leaders, it may crown the body you control");
			Optional(SettlerOffer.Install, "could not take over the game's choice of settlers, the crown may move you to a new village");
			Optional(TakeGuard.Install, "could not take over what you take off others, a better piece may replace yours");
			Optional(GiveGuard.Install, "could not take over the town's handing out of arms, it may swap a piece of yours for one of its own");
			Optional(Loot.InstallKill, "could not take over what a kill hands you, your kills' coins and goods go straight to you");
			Optional(ZombieGear.Install, "could not take over a creature's transformation, zombies rise without the armour they wore");
			Optional(HouseKeep.Install, "could not take over the town's house replacing, an owned house may be pulled down");
			Optional(BuilderBlows.Install, "could not take over the builders' blows, your village's sites may finish a little early");
			Optional(Taxes.Install, "could not take over tax payments, the tax rate's mood cost stays the game's");
			Optional(HomeLock.Install, "could not take over going into houses, a locked door doesn't keep the town's people out");
			Optional(SiteLevel.Install, "could not take over the towns' building-site check, they may build on steep ground");
			PossessionCombat.Install(Optional);
			Optional(WorldNews.Install, "could not follow the world log, the news window shows only what the save holds");
			Optional(SpatialHooks.Install, "could not take over the game's sound calls, its sounds stay panned the game's way");
			Log.Info("possession input hooks installed");
		}

		private static void Optional(Action apply, string failure)
		{
			try
			{
				apply();
			}
			catch (Exception ex)
			{
				Log.Error(failure + ": " + ex.Message);
			}
		}

		public static Vector2 MoveKeys()
		{
			float num = 0f;
			float num2 = 0f;
			if (Held(HotkeyLibrary.up))
			{
				num += 1f;
			}
			if (Held(HotkeyLibrary.down))
			{
				num -= 1f;
			}
			if (Held(HotkeyLibrary.right))
			{
				num2 += 1f;
			}
			if (Held(HotkeyLibrary.left))
			{
				num2 -= 1f;
			}
			if (TouchControls.Shown)
			{
				return new Vector2(num2, num) + TouchControls.Move;
			}
			if (VrControls.Active)
			{
				return new Vector2(num2, num) + VrControls.Move;
			}
			if (PadControls.Active)
			{
				return new Vector2(num2, num) + PadControls.Move;
			}
			return new Vector2(num2, num);
		}

		private static bool Taken()
		{
			try
			{
				return KeysTaken != null && KeysTaken();
			}
			catch
			{
				return false;
			}
		}

		private static bool Held(HotkeyAsset h)
		{
			if (h == null)
			{
				return false;
			}
			if (!Walks(h.default_key_1) && !Walks(h.default_key_2))
			{
				return Walks(h.default_key_3);
			}
			return true;
		}

		private static bool Walks(KeyCode k)
		{
			if (k != KeyCode.None)
			{
				return PlayInput.GetKey(k);
			}
			return false;
		}

		private static bool Pressed(HotkeyAsset h)
		{
			if (h == null)
			{
				return false;
			}
			if ((h.default_key_1 == KeyCode.None || !PlayInput.GetKeyDown(h.default_key_1)) && (h.default_key_2 == KeyCode.None || !PlayInput.GetKeyDown(h.default_key_2)))
			{
				if (h.default_key_3 != KeyCode.None)
				{
					return PlayInput.GetKeyDown(h.default_key_3);
				}
				return false;
			}
			return true;
		}

		private static void UpdateMovementVectorKeyboard()
		{
			if (_provenCalls < 120 && ++_provenCalls == 120)
			{
				Detour.End();
			}
			try
			{
				ControllableUnit._movement_vector = Vector2.zero;
				if (Time.frameCount != _leapFrame)
				{
					_leapFrame = Time.frameCount;
					StepLeap(Active ? ControllableUnit._unit_main : null, Mathf.Min(Time.unscaledDeltaTime, 0.1f));
				}
				if (Active)
				{
					if (Frozen)
					{
						Stop();
						WalkToggle();
						return;
					}
					if (BoatHelm.Steers(ControllableUnit._unit_main))
					{
						Stop();
						Actor unit_main = ControllableUnit._unit_main;
						Vector2 vector = BoatHelm.Course(unit_main.current_position + Creatures.MooringOffset(unit_main));
						float magnitude = vector.magnitude;
						if (magnitude > 0f)
						{
							ControllableUnit._movement_vector = vector / magnitude * StepLength(magnitude);
						}
						return;
					}
					if (Time.frameCount != _easedFrame)
					{
						_easedFrame = Time.frameCount;
						Ease(Time.unscaledDeltaTime);
					}
					if (Pace <= 0f)
					{
						return;
					}
					Vector2 vector2 = MoveNow(YourSpeed(ControllableUnit._unit_main));
					if (vector2.sqrMagnitude < 1E-10f)
					{
						return;
					}
					if (Crabzillas.Is(ControllableUnit._unit_main))
					{
						ControllableUnit._movement_vector = vector2;
						return;
					}
					_climb = Climbing.Pace(vector2);
					Vector2 vector3 = AvoidObstacles(vector2 / Pace);
					SlideForTest = Mathf.Clamp01(Vector2.Dot(vector3, (vector2 / Pace).normalized) / Mathf.Max(0.0001f, (vector2 / Pace).magnitude));
					float magnitude2 = vector3.magnitude;
					if (magnitude2 < 0.999f)
					{
						_momentum.Slid(vector3.x, vector3.y, magnitude2);
					}
					ControllableUnit._movement_vector = ((magnitude2 > 0.999f) ? (vector3 * StepLength(Pace)) : (vector3 / Mathf.Max(0.0001f, magnitude2) * StepLength(Pace * magnitude2, exact: true)));
				}
				else
				{
					Stop();
					Walking = false;
					Crouched = false;
					if (HotkeyLibrary.up.isHolding())
					{
						ControllableUnit._movement_vector.y = 1f;
					}
					else if (HotkeyLibrary.down.isHolding())
					{
						ControllableUnit._movement_vector.y = -1f;
					}
					if (HotkeyLibrary.right.isHolding())
					{
						ControllableUnit._movement_vector.x = 1f;
					}
					else if (HotkeyLibrary.left.isHolding())
					{
						ControllableUnit._movement_vector.x = -1f;
					}
				}
			}
			catch (Exception e)
			{
				ReportRuntimeError(e);
			}
		}

		private static void UpdateMovementVectorJoystick()
		{
			if (Active)
			{
				UpdateMovementVectorKeyboard();
				return;
			}
			try
			{
				ControllableUnit._movement_vector = new Vector2(ControllableUnit.getJoyAxisHorizontalLeft(), ControllableUnit.getJoyAxisVerticalLeft());
			}
			catch (Exception e)
			{
				ReportRuntimeError(e);
			}
		}

		private static void Stop()
		{
			_momentum.Reset();
			StopDash();
			Pace = 0f;
			RunBlend = 0f;
			WalkBlend = 0f;
			KeysHeld = false;
		}

		private static void Ease(float dt)
		{
			Vector2 obj = (Taken() ? Vector2.zero : MoveKeys());
			float value = obj.y + ScriptedInput.y;
			float value2 = obj.x + ScriptedInput.x;
			Running = ScriptedRun || PlayInput.GetKey(KeyCode.LeftShift) || PlayInput.GetKey(KeyCode.RightShift);
			WalkToggle();
			Vector2 vector = new Vector2(Mathf.Clamp(value2, -1f, 1f), Mathf.Clamp(value, -1f, 1f));
			KeysHeld = vector.sqrMagnitude > 1E-06f;
			CrouchToggle(vector);
			float num = Mathf.Max(RunFactor, Mathf.Min(1.35f, SprintCap));
			float num2 = ((!Running) ? (Walking ? Mathf.Min(0.35f, RunFactor) : RunFactor) : ((vector.y > 0.1f) ? num : Mathf.Max(RunFactor, 0.8f)));
			if (Crouched)
			{
				num2 = Mathf.Min(num2, RunFactor * 0.5f);
			}
			if (GuardSlow > 0f)
			{
				num2 = Mathf.Min(num2, Mathf.Lerp(RunFactor, RunFactor * 0.6f, Mathf.Clamp01(GuardSlow)));
			}
			if (ChargeSlow > 0f)
			{
				num2 = Mathf.Min(num2, Mathf.Lerp(RunFactor, RunFactor * 0.6f, Mathf.Clamp01(ChargeSlow)));
			}
			Vector2 vector2 = ((vector.sqrMagnitude > 0f) ? (vector.normalized * num2) : Vector2.zero);
			if (TouchControls.Shown || VrControls.Active || PadControls.Active)
			{
				vector2 *= Mathf.Min(1f, vector.magnitude);
			}
			if (SlowLeft > 0f && !_hopping && vector2.magnitude > 0.4f * RunFactor)
			{
				vector2 = vector2.normalized * (0.4f * RunFactor);
			}
			Actor unit_main = ControllableUnit._unit_main;
			if (unit_main != _momentumOf)
			{
				_momentumOf = unit_main;
				_momentum.Reset();
				StopDash();
				Walking = false;
				Crouched = false;
			}
			Feel = (VrControls.Active ? default(Feel) : Feel.Of(Weight.Of(unit_main)));
			float full = 3f;
			bool grounded = true;
			float grip = 1f;
			try
			{
				if (unit_main != null)
				{
					full = YourSpeed(unit_main);
					grounded = unit_main.position_height <= 0.02f || Flies(unit_main);
					grip = Grip(unit_main);
				}
			}
			catch
			{
			}
			float num3 = Mathf.Min(dt, 0.1f);
			DodgeKey(unit_main, vector, grounded);
			bool num4 = DashStep(unit_main, num3, grounded) || ((_hopping || _dodging) && Leaping);
			if (vector2 != Vector2.zero)
			{
				Vector2 normalized = vector2.normalized;
				vector2 *= ((normalized.y >= 0f) ? Mathf.Lerp(0.85f, 1f, normalized.y) : Mathf.Lerp(0.85f, 0.7f, 0f - normalized.y));
			}
			Vector2 vector3 = ToMap(vector2);
			if (!num4)
			{
				_momentum.Step(num3, vector3.x, vector3.y, RunFactor, num, Feel, grip, grounded);
				SlowStep(num3);
			}
			if (_momentum.Speed < 0.005f && vector2 == Vector2.zero)
			{
				_momentum.Reset();
			}
			Pace = MoveNow(full).magnitude;
			float target = Mathf.Clamp01((Pace - RunFactor) / Mathf.Max(0.05f, 1.35f - RunFactor));
			RunBlend = Mathf.MoveTowards(RunBlend, target, 4f * num3);
			float target2 = ((Walking && !Running && Pace > 0.01f) ? Mathf.Clamp01((RunFactor - Pace) / Mathf.Max(0.05f, RunFactor - 0.35f)) : 0f);
			WalkBlend = Mathf.MoveTowards(WalkBlend, target2, 4f * num3);
		}

		private static void CrouchToggle(Vector2 input)
		{
			if (VrControls.Active)
			{
				Crouched = false;
				return;
			}
			if (!Taken() && (PlayInput.GetKeyDown(KeyCode.LeftControl) || PlayInput.GetKeyDown(KeyCode.RightControl)))
			{
				Crouched = !Crouched;
			}
			if (Crouched && Running && input.y > 0.1f)
			{
				Crouched = false;
			}
		}

		private static void WalkToggle()
		{
			if (VrControls.Active)
			{
				return;
			}
			if (PlayInput.GetKey(KeyCode.LeftAlt) || PlayInput.GetKey(KeyCode.RightAlt))
			{
				if (_altDownAt < 0f)
				{
					_altDownAt = Time.unscaledTime;
				}
				else if (Input.anyKeyDown && !PlayInput.GetKeyDown(KeyCode.LeftAlt) && !PlayInput.GetKeyDown(KeyCode.RightAlt))
				{
					_altDownAt = 99999f;
				}
			}
			else
			{
				if (_altDownAt >= 0f && Time.unscaledTime - _altDownAt < 0.35f && !Taken())
				{
					Walking = !Walking;
				}
				_altDownAt = -1f;
			}
		}

		private static Vector2 MoveNow(float full)
		{
			return Velocity + SurgeNow(full);
		}

		internal static void StartFall(Actor unit, float fromZ)
		{
			if (unit == null || _leap.Active)
			{
				return;
			}
			_leap.StartFall();
			LeapOf = unit;
			LeapBase = fromZ;
			_jumpKeptAt = -99f;
			try
			{
				unit.position_height = Mathf.Max(0.001f, fromZ - GroundNow);
			}
			catch
			{
			}
		}

		internal static void StartLeap(Actor unit, float peak)
		{
			bool flag = DashJump(unit);
			_leap.Start(peak);
			LeapBase = FeetZ;
			LeapOf = unit;
			_jumpKeptAt = -99f;
			try
			{
				unit.position_height = 0.001f;
			}
			catch
			{
			}
			if (!flag)
			{
				Vector2 velocity = Velocity;
				float magnitude = velocity.magnitude;
				Vector2 vector = (Taken() ? Vector2.zero : (MoveKeys() + ScriptedInput));
				Vector2 vector2 = ((vector.sqrMagnitude > 0.01f) ? ToMap(vector.normalized) : ((magnitude > 0.05f) ? (velocity / magnitude) : Vector2.zero));
				if (!(vector2 == Vector2.zero))
				{
					float num = Vector2.Dot(velocity, vector2);
					float num2 = Mathf.Max(0.6f / Mathf.Max(0.5f, YourSpeed(unit)), 0.65f - num);
					_momentum.Push(vector2.x * num2, vector2.y * num2);
				}
			}
		}

		internal static void KeepJump()
		{
			_jumpKeptAt = Time.unscaledTime;
		}

		private static void StepLeap(Actor unit, float dt)
		{
			if (!_leap.Active)
			{
				return;
			}
			if (unit == null || unit != LeapOf || !unit.isAlive() || unit.under_forces)
			{
				_leap.Cancel();
				return;
			}
			float num = GroundNow - LeapBase;
			_leap.Floor = num;
			if (unit.position_height <= 0f && _leap.Lift - num > 0.01f)
			{
				_leap.Landed(Mathf.Max(0f, 0f - _leap.Rise));
				Hop(unit);
				return;
			}
			if (!_leap.Step(dt))
			{
				unit.position_height = Mathf.Max(0.0001f, _leap.Lift - num);
				return;
			}
			unit.position_height = 0f;
			try
			{
				unit.stopForce();
			}
			catch
			{
			}
			Hop(unit);
		}

		private static void Hop(Actor unit)
		{
			Landed();
			if (!(Time.unscaledTime - _jumpKeptAt > 0.12f))
			{
				_jumpKeptAt = -99f;
				if (Jumps.LastPeak > 0f)
				{
					StartLeap(unit, Jumps.LastPeak);
				}
			}
		}

		private static Vector2 ToMap(Vector2 local)
		{
			float num = Mathf.Cos(Yaw);
			float num2 = Mathf.Sin(Yaw);
			return new Vector2(num * local.y + num2 * local.x, num2 * local.y - num * local.x);
		}

		private static Vector2 FromMap(Vector2 map)
		{
			float num = Mathf.Cos(Yaw);
			float num2 = Mathf.Sin(Yaw);
			return new Vector2(num2 * map.x - num * map.y, num * map.x + num2 * map.y);
		}

		private static float Grip(Actor self)
		{
			TileTypeBase tileTypeBase = self.current_tile?.Type;
			if (tileTypeBase == null || !tileTypeBase.liquid)
			{
				return 1f;
			}
			if (tileTypeBase.id == "ice")
			{
				return 0.3f;
			}
			if (DockDecks.On(self.current_position) || self.isWaterCreature())
			{
				return 1f;
			}
			return 0.5f;
		}

		private static bool IsMovementActionActive()
		{
			try
			{
				if (Active)
				{
					return !Frozen && (BoatHelm.Steers(ControllableUnit._unit_main) ? (ControllableUnit._movement_vector != Vector2.zero) : (Pace > 0f));
				}
				if (Config.joyControls)
				{
					return ControllableUnit._movement_vector != Vector2.zero;
				}
				return HotkeyLibrary.up.isHolding() || HotkeyLibrary.down.isHolding() || HotkeyLibrary.left.isHolding() || HotkeyLibrary.right.isHolding();
			}
			catch (Exception e)
			{
				ReportRuntimeError(e);
				return false;
			}
		}

		private static float MismatchFactor(Actor self, Vector2 point)
		{
			try
			{
				if (Active && self == ControllableUnit._unit_main)
				{
					return 0f;
				}
				Vector2 mousePos = World.world.getMousePos();
				Vector2 current_position = self.current_position;
				bool flag = current_position.x < mousePos.x;
				bool flag2 = current_position.x < point.x;
				bool num = current_position.y < mousePos.y;
				bool flag3 = current_position.y < point.y;
				float num2 = Mathf.Abs(point.x - current_position.x);
				float num3 = Mathf.Abs(point.y - current_position.y);
				float num4 = 0f;
				if (flag != flag2)
				{
					num4 += num2;
				}
				if (num != flag3)
				{
					num4 += num3;
				}
				return Mathf.Clamp01(num4 / (num2 + num3 + 0.001f));
			}
			catch (Exception e)
			{
				ReportRuntimeError(e);
				return 0f;
			}
		}

		private static float RealTime()
		{
			if (Time.frameCount != _realTimeFrame)
			{
				_realTimeFrame = Time.frameCount;
				float num = Mathf.Max(0.0001f, Time.fixedDeltaTime);
				try
				{
					if (Config.fps_lock_30)
					{
						num *= 2f;
					}
				}
				catch
				{
				}
				float b = Mathf.Clamp(Time.unscaledDeltaTime / num, 0.5f, 6f);
				_realTime = Mathf.Lerp(_realTime, b, 0.35f);
			}
			return _realTime;
		}

		private static float YourSpeed(Actor self)
		{
			try
			{
				float num = Mathf.Max(1f, ((BaseSimObject)self).stats["speed"]);
				return Mathf.Max(0.4f, num * 0.4f * SimGlobals.m.unit_speed_multiplier * Ground(self)) * Slope(self);
			}
			catch
			{
				return Mathf.Max(0.4f, self._current_combined_movement_speed);
			}
		}

		private static float Ground(Actor self)
		{
			try
			{
				if (self.asset == null || self.asset.ignore_tile_speed_multiplier || ((BaseSimObject)self).isInAir())
				{
					return 1f;
				}
				TileTypeBase tileTypeBase = self.current_tile?.Type;
				if (tileTypeBase == null)
				{
					return 1f;
				}
				if (tileTypeBase.liquid)
				{
					if (Wading.Pace(self, out var pace))
					{
						return pace;
					}
					if (tileTypeBase.id == "ice")
					{
						return 0.95f;
					}
					if (DockDecks.On(self.current_position))
					{
						return 1f;
					}
					if (self.isWaterCreature())
					{
						return 1f;
					}
					return (self.getStamina() > 0) ? 1f : 0.7f;
				}
				if (tileTypeBase.walk_multiplier > 1.01f)
				{
					return 1.12f;
				}
				string ignore_walk_multiplier_if_tag = tileTypeBase.ignore_walk_multiplier_if_tag;
				if (tileTypeBase.walk_multiplier < 0.99f && !string.IsNullOrEmpty(ignore_walk_multiplier_if_tag) && !((BaseSimObject)self).stats.hasTag(ignore_walk_multiplier_if_tag))
				{
					return 0.9f;
				}
				return 1f;
			}
			catch
			{
				return 1f;
			}
		}

		private static float Slope(Actor self)
		{
			try
			{
				if (_climb >= 1f || ((BaseSimObject)self).isInAir() || BoatHelm.Steers(self) || Crabzillas.Is(self))
				{
					return 1f;
				}
				WorldTile current_tile = self.current_tile;
				if (current_tile != null && current_tile.Type != null && current_tile.Type.liquid && !DockDecks.On(self.current_position))
				{
					return 1f;
				}
				return _climb;
			}
			catch
			{
				return 1f;
			}
		}

		private static bool IsYou(Actor a)
		{
			if (Active && a != null)
			{
				return a == ControllableUnit._unit_main;
			}
			return false;
		}

		internal static float SpeedForTest(Actor a)
		{
			return YourSpeed(a);
		}

		internal static float FullSpeed(Actor a)
		{
			if (a == null)
			{
				return 0f;
			}
			return YourSpeed(a);
		}

		private static float PossessedStep(Actor self, float elapsed, Vector2 point)
		{
			try
			{
				self.precalcMovementSpeed(pForce: true);
				if (self.asset.can_flip && self.checkFlip())
				{
					float mismatchFactorForSideMovement = self.getMismatchFactorForSideMovement(point);
					if (mismatchFactorForSideMovement > 0.2f)
					{
						elapsed *= Mathf.Lerp(1f, 0.8f, mismatchFactorForSideMovement);
					}
				}
				float num;
				if (IsYou(self))
				{
					YourStepsForTest++;
					num = YourSpeed(self) * elapsed * RealTime() * Mathf.Max(1f, BoatHelm.Steers(self) ? Mathf.Abs(BoatHelm.Way) : Pace);
				}
				else
				{
					num = self.getMovementDelta(elapsed, 0f);
				}
				Vector2 pNewPos = Vector2.MoveTowards(self.current_position, point, num);
				pNewPos = self.checkVelocityAgainstBlock(pNewPos);
				if (!Toolbox.inMapBorder(ref pNewPos))
				{
					return 0f;
				}
				self.current_position = pNewPos;
				return num;
			}
			catch (Exception e)
			{
				ReportRuntimeError(e);
				return 0f;
			}
		}

		private static void RandomForce(Actor self, float minHeight, float maxHeight)
		{
			try
			{
				if (!IsYou(self))
				{
					float pForceAmountDirection = Randy.randomFloat(1.5f, 2f);
					float pForceHeight = Randy.randomFloat(minHeight, maxHeight);
					WorldTile current_tile = self.current_tile;
					WorldTile random = current_tile.neighboursAll.GetRandom();
					self.calculateForce(current_tile.posV3.x, current_tile.posV3.y, random.posV3.x, random.posV3.y, pForceAmountDirection, pForceHeight, pCheckCancelJobOnLand: true);
				}
			}
			catch (Exception e)
			{
				ReportRuntimeError(e);
			}
		}

		private static void CheckActions()
		{
			try
			{
				bool flag = Active && (SceneHoldsKeys || Taken());
				ControllableUnit._action_pressed_jump = !flag && (HotkeyLibrary.action_jump.isJustPressed() || (Active && ((Pressed(HotkeyLibrary.action_jump) && !Taken()) || ScriptedJump)) || TouchPossessionController.isActionPressedJump());
				if (Active)
				{
					ScriptedJump = false;
				}
				ControllableUnit._action_pressed_dash = !Active && (HotkeyLibrary.action_dash.isJustPressed() || TouchPossessionController.isActionPressedDash());
				if (Active)
				{
					_ = PlayInput.Virtual;
				}
				else
					_ = 0;
				ControllableUnit._action_pressed_backstep = !flag && ((!Active) ? (HotkeyLibrary.action_backstep.isJustPressed() || TouchPossessionController.isActionPressedBackStep()) : (_backstepTapped || PadControls.BackstepTap || (VrControls.Active && Pressed(HotkeyLibrary.action_backstep))));
				bool attack_pressed_button_left = ControllableUnit._attack_pressed_button_left;
				ControllableUnit._action_pressed_steal = (Active && ScriptedSteal) || (!flag && ((!Active && HotkeyLibrary.action_steal.isJustPressed()) || (attack_pressed_button_left && TouchPossessionController.isSelectedActionSteal())));
				if (Active)
				{
					ScriptedSteal = false;
				}
				ControllableUnit._action_pressed_swear = !Active && (HotkeyLibrary.action_swear.isJustPressed() || (attack_pressed_button_left && TouchPossessionController.isSelectedActionSwear()));
				ControllableUnit._action_pressed_talk = !Active && (HotkeyLibrary.action_talk.isJustPressed() || (attack_pressed_button_left && TouchPossessionController.isSelectedActionTalk()));
				ControllableUnit._attack_just_pressed_button_right = ControllableUnit._attack_just_pressed_button_right || (attack_pressed_button_left && TouchPossessionController.isSelectedActionKick());
			}
			catch (Exception e)
			{
				ReportRuntimeError(e);
			}
		}

		private static void UpdateMouseAttackPosition()
		{
			try
			{
				ControllableUnit._click_vector = Vector2.zero;
				if (Active)
				{
					ControllableUnit._click_vector = AimPoint;
					if (PlayInput.Virtual)
					{
						ControllableUnit._attack_pressed_button_left = PlayInput.GetMouseButton(0);
						ControllableUnit._attack_pressed_button_right = PlayInput.GetMouseButton(1);
						ControllableUnit._attack_just_pressed_button_left = PlayInput.GetMouseButtonDown(0);
						ControllableUnit._attack_just_pressed_button_right = PlayInput.GetMouseButtonDown(1);
					}
					if (BlockAttack || Crabzillas.Is(ControllableUnit._unit_main))
					{
						ControllableUnit._attack_pressed_button_left = false;
						ControllableUnit._attack_pressed_button_right = false;
						ControllableUnit._attack_just_pressed_button_left = false;
						ControllableUnit._attack_just_pressed_button_right = false;
					}
					else if (HoldAttack)
					{
						ControllableUnit._attack_pressed_button_left = false;
						ControllableUnit._attack_just_pressed_button_left = false;
						if (ReleaseAttack)
						{
							ControllableUnit._attack_pressed_button_left = true;
							ControllableUnit._attack_just_pressed_button_left = true;
						}
					}
				}
				else if (!InputHelpers.mouseSupported && Input.touchSupported)
				{
					ControllableUnit._click_vector = ControllableUnit.getTouchAttackPosition();
				}
				else
				{
					ControllableUnit._click_vector = World.world.getMousePos();
				}
			}
			catch (Exception e)
			{
				ReportRuntimeError(e);
			}
		}

		private static float StepLength(float pace, bool exact = false)
		{
			if (pace >= 0.999f && !exact)
			{
				return 1.5f;
			}
			float num = 1f;
			float num2 = 0f;
			try
			{
				Actor unit_main = ControllableUnit._unit_main;
				if (unit_main != null)
				{
					num = YourSpeed(unit_main);
				}
				num2 = ((World.world != null) ? World.world.elapsed : 0f);
			}
			catch
			{
				return 1f;
			}
			if (num2 <= 0f)
			{
				return 0.0005f;
			}
			return Mathf.Min(1f, Mathf.Max(0.0005f, pace * num * num2 * RealTime() * 1.02f));
		}

		private static Vector2 AvoidObstacles(Vector2 move)
		{
			Actor unit_main = ControllableUnit._unit_main;
			if (unit_main == null)
			{
				return move;
			}
			bool flag = unit_main.position_height > 0.3f;
			if (flag)
			{
				move = AvoidBlocks(unit_main, move);
				if (Flies(unit_main))
				{
					return move;
				}
			}
			float num = 0f;
			try
			{
				num = YourSpeed(unit_main) * ((World.world != null) ? World.world.elapsed : 0f) * RealTime() * Mathf.Max(1f, Pace);
			}
			catch
			{
			}
			return Colliders.Slide(unit_main.current_position, move, BodyRadius, Mathf.Clamp(num * 2.5f, 0.04f, 0.6f), num * 1.02f, flag);
		}

		private static Vector2 AvoidBlocks(Actor unit, Vector2 move)
		{
			try
			{
				if (unit.ignoresBlocks() || Climbing.Stops(unit, unit.current_tile))
				{
					return move;
				}
				Vector2 current_position = unit.current_position;
				float num = BodyRadius + 0.05f;
				if (move.x != 0f && Climbing.Stops(unit, Toolbox.getTileAt(current_position.x + Mathf.Sign(move.x) * num, current_position.y)))
				{
					move.x = 0f;
				}
				if (move.y != 0f && Climbing.Stops(unit, Toolbox.getTileAt(current_position.x, current_position.y + Mathf.Sign(move.y) * num)))
				{
					move.y = 0f;
				}
				if (move.x != 0f && move.y != 0f && Climbing.Stops(unit, Toolbox.getTileAt(current_position.x + Mathf.Sign(move.x) * num, current_position.y + Mathf.Sign(move.y) * num)))
				{
					move.y = 0f;
				}
			}
			catch
			{
			}
			return move;
		}

		internal static bool Flies(Actor unit)
		{
			try
			{
				return unit.ignoresBlocks() || (unit.asset != null && unit.asset.flying);
			}
			catch
			{
				return false;
			}
		}

		internal static bool IsBlock(WorldTile t)
		{
			try
			{
				return t != null && t.Type != null && t.Type.block;
			}
			catch
			{
				return false;
			}
		}

		private static void ReportRuntimeError(Exception e)
		{
			if (!_loggedRuntimeError)
			{
				_loggedRuntimeError = true;
				Log.Error("possession hook failed: " + e);
			}
		}

		internal static void Surge(Vector2 way, float distance, float seconds)
		{
			if (!(way.sqrMagnitude < 1E-06f) && !(distance <= 0f) && !(seconds <= 0f) && !Leaping && !Dashing)
			{
				_surgeDir = way.normalized;
				_surgeLength = seconds;
				_surgeTime = 0f;
				_surgeSpeed = 2f * distance / seconds;
			}
		}

		private static Vector2 SurgeNow(float full)
		{
			if (_surgeTime >= _surgeLength)
			{
				return Vector2.zero;
			}
			return _surgeDir * (_surgeSpeed * (1f - _surgeTime / _surgeLength) / Mathf.Max(0.5f, full));
		}

		internal static void Dash(Actor unit, Vector2 way, float distance, float seconds, float endShare = -1f, bool slow = true)
		{
			if (!(way.sqrMagnitude < 1E-06f) && !(seconds <= 0f))
			{
				float num = Mathf.Max(0.5f, (unit != null) ? YourSpeed(unit) : 3f);
				_dashDir = way.normalized;
				_dashLength = seconds;
				_dashTime = 0f;
				_dashSlow = slow;
				_dashEnd = ((endShare >= 0f) ? endShare : 1.89f) * num;
				_dashStart = Mathf.Max(_dashEnd, _dashEnd + 3f * (distance / seconds - _dashEnd));
				_surgeTime = 99f;
				SlowLeft = 0f;
				_slowOwed = (_hopping = false);
				DashesForTest++;
			}
		}

		private static float DashSpeed()
		{
			float num = 1f - Mathf.Clamp01(_dashTime / Mathf.Max(0.01f, _dashLength));
			return _dashEnd + (_dashStart - _dashEnd) * num * num;
		}

		internal static void EndDash()
		{
			if (Dashing)
			{
				_dashTime = _dashLength;
				float runFactor = RunFactor;
				_momentum.Vx = _dashDir.x * runFactor;
				_momentum.Vy = _dashDir.y * runFactor;
				DashOver();
			}
		}

		private static void DashOver()
		{
			_dashOverAt = Time.unscaledTime;
			_dashOverSpeed = _momentum.Speed;
			SlowLeft = (_dashSlow ? 1f : 0f);
		}

		private static void DodgeKey(Actor unit, Vector2 keys, bool grounded)
		{
			Vector2 vector = DoubleTap();
			_backstepTapped = false;
			if (unit == null || VrControls.Active)
			{
				return;
			}
			if (!ScriptedDodge)
			{
				if (vector == Vector2.zero || vector.y > 0f)
				{
					return;
				}
				keys = vector;
			}
			if (keys.sqrMagnitude < 0.01f || !grounded || Leaping || Dashing)
			{
				return;
			}
			ScriptedDodge = false;
			if (!(Time.unscaledTime - _dodgeAt < 0.8f) && !Taken())
			{
				float num = Mathf.Max(0.3f, BodyRadius / 0.17f) / 0.9f;
				float peak = 0.12f * num;
				float num2 = 0.95f * num / Mathf.Max(0.05f, Leap.Airtime(peak)) / Mathf.Max(0.5f, YourSpeed(unit));
				Vector2 vector2 = ToMap(keys.normalized);
				_momentum.Vx = vector2.x * num2;
				_momentum.Vy = vector2.y * num2;
				_leap.Start(peak);
				LeapBase = FeetZ;
				LeapOf = unit;
				_jumpKeptAt = -99f;
				try
				{
					unit.position_height = 0.001f;
				}
				catch
				{
				}
				_dodgeAt = Time.unscaledTime;
				_dodging = true;
				DodgesForTest++;
			}
		}

		private static void DodgeLanded()
		{
			if (_dodging)
			{
				_dodging = false;
				float speed = _momentum.Speed;
				float runFactor = RunFactor;
				if (speed > runFactor)
				{
					_momentum.Vx *= runFactor / speed;
					_momentum.Vy *= runFactor / speed;
				}
			}
		}

		private static bool DashStep(Actor unit, float dt, bool grounded)
		{
			float unscaledTime = Time.unscaledTime;
			if (_surgeTime < _surgeLength)
			{
				_surgeTime += dt;
			}
			if (Dashing)
			{
				_dashTime += dt;
				float num = Mathf.Max(0.5f, (unit != null) ? YourSpeed(unit) : 3f);
				float num2 = DashSpeed() / num;
				_momentum.Vx = _dashDir.x * num2;
				_momentum.Vy = _dashDir.y * num2;
				if (!Dashing)
				{
					DashOver();
				}
				return true;
			}
			if (_hopping && !Leaping)
			{
				if (unscaledTime - _landedAt <= 0.08f)
				{
					return true;
				}
				_hopping = false;
				if (_slowOwed)
				{
					_slowOwed = false;
					SlowLeft = 1f;
					_dashOverAt = -99f;
				}
			}
			if (SlowLeft > 0f && grounded && !_hopping)
			{
				SlowLeft = Mathf.Max(0f, SlowLeft - dt);
			}
			return false;
		}

		private static void SlowStep(float dt)
		{
			if (!(SlowLeft <= 0f) && !_hopping)
			{
				float num = 0.4f * RunFactor;
				float speed = _momentum.Speed;
				if (!(speed <= num))
				{
					float num2 = Mathf.Max(num, speed - 4f * dt);
					_momentum.Vx *= num2 / speed;
					_momentum.Vy *= num2 / speed;
				}
			}
		}

		private static bool DashJump(Actor unit)
		{
			float unscaledTime = Time.unscaledTime;
			if ((Dashing && _dashTime > 0.4f * _dashLength) || (!Dashing && SlowLeft > 0f && unscaledTime - _dashOverAt <= 0.2f))
			{
				float num = Mathf.Max(0.5f, YourSpeed(unit));
				float num2 = (Dashing ? (DashSpeed() / num) : _dashOverSpeed);
				_dashTime = _dashLength;
				_momentum.Vx = _dashDir.x * num2;
				_momentum.Vy = _dashDir.y * num2;
				SlowLeft = 0f;
				_slowOwed = (_hopping = true);
				DashJumpsForTest++;
				return true;
			}
			if (_hopping && unscaledTime - _landedAt <= 0.19999999f)
			{
				float num3 = 1.35f;
				float speed = _momentum.Speed;
				if (speed > num3)
				{
					float num4 = Mathf.Max(num3 / speed, 0.9f);
					_momentum.Vx *= num4;
					_momentum.Vy *= num4;
				}
				HopsForTest++;
				return true;
			}
			return false;
		}

		private static void Landed()
		{
			_landedAt = Time.unscaledTime;
			DodgeLanded();
		}

		private static void StopDash()
		{
			_dashTime = 99f;
			_surgeTime = 99f;
			SlowLeft = 0f;
			_slowOwed = (_hopping = (_dodging = false));
		}

		private static Vector2 DoubleTap()
		{
			Vector2 result = Vector2.zero;
			if (Taken() || VrControls.Active)
			{
				ForgetTaps();
				return result;
			}
			float unscaledTime = Time.unscaledTime;
			for (int i = 0; i < 4; i++)
			{
				HotkeyAsset h;
				switch (i)
				{
				default:
					h = HotkeyLibrary.right;
					break;
				case 2:
					h = HotkeyLibrary.left;
					break;
				case 1:
					h = HotkeyLibrary.down;
					break;
				case 0:
					h = HotkeyLibrary.up;
					break;
				}
				if (!Held(h))
				{
					_tapUp[i] = true;
				}
				else
				{
					if (!_tapUp[i])
					{
						continue;
					}
					_tapUp[i] = false;
					if (_tapAt[i] >= 0f && unscaledTime - _tapAt[i] < 0.25f)
					{
						_tapAt[i] = -1f;
						Vector2 vector;
						switch (i)
						{
						default:
							vector = new Vector2(1f, 0f);
							break;
						case 2:
							vector = new Vector2(-1f, 0f);
							break;
						case 1:
							vector = new Vector2(0f, -1f);
							break;
						case 0:
							vector = new Vector2(0f, 1f);
							break;
						}
						result = vector;
					}
					else
					{
						_tapAt[i] = unscaledTime;
					}
				}
			}
			return result;
		}

		private static void ForgetTaps()
		{
			for (int i = 0; i < 4; i++)
			{
				_tapAt[i] = -1f;
				_tapUp[i] = true;
			}
		}
	}
}
