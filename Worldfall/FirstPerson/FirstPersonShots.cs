using System;
using System.Collections.Generic;
using FirstPerson.Core;
using FirstPerson.Net;
using UnityEngine;

namespace FirstPerson
{
	internal static class FirstPersonShots
	{
		private sealed class Shot
		{
			public Projectile P;

			public Actor Owner;

			public Vector2 Start;

			public Vec3 From;

			public float DirX;

			public float DirY;

			public float Rise;

			public float Drop;

			public float Flat;

			public Vec3 Last;

			public float Offset;

			public float Born;

			public float Pad;

			public bool Arrow;

			public bool Bursts;

			public Actor Meant;

			public ArrowTrail Trail;

			public bool Pellet;

			public PelletTrail Tracer;

			public int Volley;

			public readonly HashSet<Actor> Passed = new HashSet<Actor>();

			public object Room;

			public Vector2 Shift;
		}

		private const float GameLift = 1f;

		private const float LoosedSpeed = 30f;

		private const float SnapSpeed = 21f;

		private const float FullDrawSpeed = 34f;

		private static float _bowDraw = -1f;

		private static float _bowAt = -100f;

		private static Vec3? _bowTip;

		private static readonly List<ArrowTrail> Trails = new List<ArrowTrail>();

		private const float PelletSpeed = 58f;

		private const float PelletRange = 24f;

		private static readonly List<PelletTrail> Tracers = new List<PelletTrail>();

		private static Vec3 _gunMuzzle;

		private static float _gunAt = -100f;

		private static int _volleys;

		internal static int Patters;

		internal static Vec3 PatterAt;

		private static int _patterVolley = -1;

		private static int _shotVolley;

		internal static readonly List<Vec3> PelletEnds = new List<Vec3>();

		internal static int PelletsLaunched;

		private static int _endsVolley = -1;

		private static readonly HashSet<string> Thrown = new HashSet<string> { "firebomb", "torch", "rock", "bone", "snowball" };

		private static readonly Dictionary<Projectile, Shot> Shots = new Dictionary<Projectile, Shot>();

		private static readonly List<Projectile> Done = new List<Projectile>();

		private static readonly HashSet<Projectile> Handled = new HashSet<Projectile>();

		private static readonly List<Projectile> HandledGone = new List<Projectile>();

		private static readonly HashSet<Projectile> Before = new HashSet<Projectile>();

		private static readonly List<Projectile> Fresh = new List<Projectile>();

		private static readonly List<Actor> Near = new List<Actor>();

		private static TerrainCache _terrain;

		private static int _updated = -10;

		internal static bool Trace;

		internal static int Launched;

		internal static int Struck;

		internal static int Grounded;

		internal static int Walled;

		internal static Actor LastVictim;

		internal static Actor LastPassed;

		internal static Vec3 LastEnd;

		internal static Vec3 LastAim;

		internal static Vec3 LastFrom;

		internal static float LastSpeed;

		internal static int StreaksEmitted;

		internal static string LastWhat;

		private const float SteepestShot = 1.5f;

		internal static int TracerCount => Tracers.Count;

		internal static int TrailCount => Trails.Count;

		private static void PelletEnded(Shot s, Vec3 at)
		{
			if (s.Volley != _endsVolley)
			{
				PelletEnds.Clear();
				_endsVolley = s.Volley;
			}
			PelletEnds.Add(at);
		}

		public static void GunMuzzle(Vec3 fromEye)
		{
			_gunMuzzle = fromEye;
			_gunAt = Time.unscaledTime;
		}

		private static bool IsPellet(Projectile p)
		{
			if (p != null && p.asset != null)
			{
				return p.asset.id == "shotgun_bullet";
			}
			return false;
		}

		public static bool Steered(Projectile p)
		{
			if (p != null)
			{
				return Shots.ContainsKey(p);
			}
			return false;
		}

		public static bool IsHandled(Projectile p)
		{
			if (p != null)
			{
				return Handled.Contains(p);
			}
			return false;
		}

		public static void MarkHandled(Projectile p)
		{
			if (p != null)
			{
				Handled.Add(p);
			}
		}

		public static float BaseZ(Projectile p, float ground)
		{
			if (p == null || !Shots.TryGetValue(p, out var value))
			{
				return ground;
			}
			return value.Offset;
		}

		public static void Loosed(float draw, Vec3? tipFromEye)
		{
			_bowDraw = draw;
			_bowTip = tipFromEye;
			_bowAt = Time.unscaledTime;
		}

		public static void BeforeAttack()
		{
			Before.Clear();
			HashSet<Projectile> hashSet = All();
			if (hashSet == null)
			{
				return;
			}
			foreach (Projectile item in hashSet)
			{
				Before.Add(item);
			}
		}

		public static void AfterAttack(Actor owner)
		{
			HashSet<Projectile> hashSet = All();
			if (hashSet == null)
			{
				return;
			}
			Fresh.Clear();
			foreach (Projectile item in hashSet)
			{
				if (!Before.Contains(item) && item != null && item.by_who == owner && item._state == ProjectileState.Active && !Shots.ContainsKey(item))
				{
					Fresh.Add(item);
				}
			}
			Before.Clear();
			if (Fresh.Count == 0)
			{
				return;
			}
			Vec3 point;
			Actor creature;
			bool flag = FirstPersonAim.ShotPoint(out point, out creature, pastFriends: true);
			bool flag2 = IsPellet(Fresh[0]);
			if (flag2)
			{
				_volleys++;
			}
			for (int i = 0; i < Fresh.Count; i++)
			{
				if (flag2)
				{
					ShotgunPattern.Offset(i, Fresh.Count, _volleys, out var right, out var up);
					Launch(Fresh[i], owner, flag ? new Vec3?(point) : ((Vec3?)null), 0f - right, creature, up);
					continue;
				}
				float num = ((Fresh.Count > 1) ? (((float)i - (float)(Fresh.Count - 1) * 0.5f) * 2.5f * (MathF.PI / 180f)) : 0f);
				float num2 = 0f;
				float lift = 0f;
				if (num2 > 0f)
				{
					num += UnityEngine.Random.Range(0f - num2, num2);
					lift = UnityEngine.Random.Range(0f - num2, num2) * 0.6f;
				}
				Launch(Fresh[i], owner, flag ? new Vec3?(point) : ((Vec3?)null), num, creature, lift);
			}
			_bowDraw = -1f;
			_bowTip = null;
			FirstPersonAim.ReleaseShot();
		}

		public static void LaunchAbility(Projectile p, Actor owner, Actor target, Vector2 ground)
		{
			if (p == null || !PossessionHooks.Active || owner != ControllableUnit._unit_main || !FirstPersonAim.Ready(owner))
			{
				return;
			}
			TerrainCache terrain = FirstPersonAim.Terrain;
			if (InRoom())
			{
				ground = RoomCombat.ToRoom(ground);
			}
			Vec3 value = new Vec3(ground.x, ground.y, terrain.TopAt(ground.x, ground.y));
			if (target != null)
			{
				Hitbox box;
				if (FirstPersonAim.ThrowPoint(out var point, out var creature) && creature == target)
				{
					value = point;
				}
				else if (Hitboxes.Get(target, out box))
				{
					value = box.Centre;
				}
			}
			Launch(p, owner, value, 0f, target, 0f, thrown: true);
		}

		public static void Launch(Projectile p, Actor owner, Vec3? target, float fan = 0f, Actor meant = null, float lift = 0f, bool thrown = false)
		{
			bool flag = InRoom();
			if (_terrain == null || flag)
			{
				_terrain = FirstPersonAim.Terrain;
			}
			if (p == null || p.asset == null || owner == null || !FirstPersonAim.Ready(owner) || _terrain == null)
			{
				return;
			}
			try
			{
				float num = Mathf.Max(0.3f, FirstPersonAim.Body);
				Vec3 eye = FirstPersonAim.Eye;
				float yaw = FirstPersonAim.Yaw;
				float num2 = Mathf.Cos(yaw);
				float num3 = Mathf.Sin(yaw);
				bool flag2 = People.IsHumanoid(owner) && !BeastView.Active;
				bool flag3 = p.asset.id == "arrow" && _bowDraw >= 0f && Time.unscaledTime - _bowAt < 1.5f;
				bool flag4 = IsPellet(p);
				bool flag5 = flag4 && flag2 && Time.unscaledTime - _gunAt < 0.5f;
				Vec3 vec = (flag2 ? (new Vec3(num2, num3, 0f) * (0.32f * num) + new Vec3(num3, 0f - num2, 0f) * (0.05f * num) + new Vec3(0f, 0f, -0.09f * num)) : (new Vec3(num2, num3, 0f) * (0.22f * num)));
				if (flag5)
				{
					Vec3 gunMuzzle = _gunMuzzle;
					vec = new Vec3(num3, 0f - num2, 0f) * gunMuzzle.X + new Vec3(num2, num3, 0f) * gunMuzzle.Y + new Vec3(0f, 0f, gunMuzzle.Z + FirstPersonAim.Slope * gunMuzzle.Y);
				}
				else if (flag3 && flag2 && _bowTip.HasValue)
				{
					Vec3 value = _bowTip.Value;
					vec = new Vec3(num3, 0f - num2, 0f) * value.X + new Vec3(num2, num3, 0f) * value.Y + new Vec3(0f, 0f, value.Z + FirstPersonAim.Slope * value.Y);
				}
				float num4 = 1f;
				if (target.HasValue)
				{
					num4 = Mathf.Clamp01(((target.Value.X - eye.X) * num2 + (target.Value.Y - eye.Y) * num3 - 0.1f) / (0.8f * num));
				}
				Vec3 vec2 = eye + vec * num4;
				if (target.HasValue && Grazes(vec2, target.Value))
				{
					vec2 = eye + new Vec3(num2, num3, 0f) * (0.25f * num * num4);
				}
				bool handAim = FirstPersonAim.HandAim;
				if (handAim)
				{
					vec2 = (thrown ? FirstPersonAim.FreeFrom : FirstPersonAim.HandFrom);
				}
				float num5 = Surface(vec2.X, vec2.Y) + 0.06f + 0.1f * num;
				if (vec2.Z < num5)
				{
					vec2.Z = num5;
				}
				float num6 = p._speed / Mathf.Max(0.01f, p.asset.mass);
				if (!(num6 > 0.5f))
				{
					num6 = 20f;
				}
				if (!Thrown.Contains(p.asset.id))
				{
					num6 = Mathf.Max(num6, 30f);
				}
				if (flag3)
				{
					num6 = Mathf.Lerp(21f, 34f, Mathf.InverseLerp(0.45f, 1f, _bowDraw));
				}
				if (flag4)
				{
					num6 = 58f;
				}
				float num7 = Mathf.Max(0.01f, SimGlobals.m.gravity);
				float num11;
				float num12;
				if (target.HasValue)
				{
					Vec3 value2 = target.Value;
					float num8 = value2.X - vec2.X;
					float num9 = value2.Y - vec2.Y;
					float num10 = Mathf.Sqrt(num8 * num8 + num9 * num9);
					num11 = ((num10 > 0.01f) ? Mathf.Atan2(num9, num8) : yaw);
					num12 = Pitch(num10, value2.Z - vec2.Z, num6, num7);
					LastAim = value2;
				}
				else if (handAim)
				{
					num11 = (thrown ? FirstPersonAim.ThrowYaw : FirstPersonAim.ShotYaw);
					num12 = Mathf.Atan(thrown ? FirstPersonAim.ThrowSlope : FirstPersonAim.ShotSlope);
					LastAim = vec2 + new Vec3(Mathf.Cos(num11), Mathf.Sin(num11), Mathf.Tan(num12)).Normalized() * 60f;
				}
				else
				{
					num11 = yaw;
					num12 = Mathf.Atan(FirstPersonAim.Slope);
					LastAim = eye + FirstPersonAim.Ray * 60f;
				}
				LastFrom = vec2;
				LastSpeed = num6;
				num11 += fan;
				num12 = Mathf.Clamp(num12 + lift, -1.5f, 1.5f);
				float num13 = Mathf.Cos(num12);
				Shot shot = new Shot
				{
					P = p,
					Owner = owner,
					From = vec2,
					Last = vec2,
					DirX = Mathf.Cos(num11),
					DirY = Mathf.Sin(num11),
					Rise = Mathf.Tan(num12),
					Drop = num7 / (2f * num6 * num6 * num13 * num13),
					Flat = num6 * num13,
					Born = Time.time,
					Arrow = (p.asset.id == "arrow"),
					Meant = meant,
					Bursts = (!flag4 && (!string.IsNullOrEmpty(p.asset.terraform_option) || !string.IsNullOrEmpty(p.asset.end_effect))),
					Pellet = flag4,
					Room = (flag ? RoomCombat.Key : null),
					Shift = (flag ? RoomCombat.Offset : Vector2.zero)
				};
				shot.Pad = (shot.Arrow ? 0.04f : (shot.Pellet ? 0.035f : (shot.Bursts ? 0.14f : 0.08f)));
				if (shot.Pellet)
				{
					if (!flag)
					{
						shot.Tracer = new PelletTrail
						{
							From = vec2,
							DirX = shot.DirX,
							DirY = shot.DirY,
							Rise = shot.Rise,
							Drop = shot.Drop,
							Flat = shot.Flat
						};
						Tracers.Add(shot.Tracer);
					}
					_shotVolley = _volleys;
					shot.Volley = _volleys;
					PelletsLaunched++;
				}
				if (shot.Arrow && !flag)
				{
					shot.Trail = new ArrowTrail
					{
						From = vec2,
						DirX = shot.DirX,
						DirY = shot.DirY,
						Rise = shot.Rise,
						Drop = shot.Drop,
						Flat = shot.Flat
					};
					Trails.Add(shot.Trail);
				}
				Vector2 vector = (p._vector_start = new Vector2(vec2.X, vec2.Y) + shot.Shift);
				p._vector_target = (target.HasValue ? (new Vector2(target.Value.X, target.Value.Y) + shot.Shift) : (vector + new Vector2(shot.DirX, shot.DirY) * 20f));
				p._angle_horizontal = num11;
				p._angle_vertical = num12;
				p._is_target_reached = false;
				shot.Start = vector;
				Shots[p] = shot;
				float num14 = 0f;
				if (flag3 || flag5)
				{
					num14 = Mathf.Clamp(Time.unscaledDeltaTime, 0.01f, 0.04f) * shot.Flat;
					if (target.HasValue)
					{
						num14 = Mathf.Min(num14, 0.5f * Mathf.Sqrt((target.Value.X - vec2.X) * (target.Value.X - vec2.X) + (target.Value.Y - vec2.Y) * (target.Value.Y - vec2.Y)));
					}
				}
				Write(shot, (num14 > 0f) ? Point(shot, num14) : vec2);
				Launched++;
				if (Trace)
				{
					Log.Info(string.Format("shot {0} from {1:F2},{2:F2},{3:F2} (eye {4:F2},{5:F2},{6:F2}) to {7}, speed {8:F1}, pitch {9:F1}, heading {10:F1}", p.asset.id, vec2.X, vec2.Y, vec2.Z, eye.X, eye.Y, eye.Z, target.HasValue ? $"{target.Value.X:F2},{target.Value.Y:F2},{target.Value.Z:F2}" : "the sky", num6, num12 * 57.29578f, num11 * 57.29578f));
				}
			}
			catch (Exception ex)
			{
				Log.Error("first-person shot: " + ex.Message);
				Shots.Remove(p);
			}
		}

		private static float Pitch(float d, float h, float speed, float g)
		{
			if (d < 0.05f)
			{
				if (!(h >= 0f))
				{
					return -1.5f;
				}
				return 1.5f;
			}
			float num = speed * speed;
			float num2 = num * num - g * (g * d * d + 2f * h * num);
			if (num2 < 0f)
			{
				return Mathf.Max(MathF.PI / 4f, Mathf.Atan2(h, d));
			}
			return Mathf.Atan((num - Mathf.Sqrt(num2)) / (g * d));
		}

		public static void Update(TerrainCache terrain, SceneCollector scene)
		{
			_terrain = terrain;
			_updated = Time.frameCount;
			if (Handled.Count > 0 && Time.frameCount % 60 == 0)
			{
				ForgetHandled();
			}
			if (Shots.Count == 0)
			{
				return;
			}
			Done.Clear();
			foreach (KeyValuePair<Projectile, Shot> shot in Shots)
			{
				bool flag;
				try
				{
					flag = Fly(shot.Value, terrain, scene);
				}
				catch (Exception ex)
				{
					Log.Error("first-person shot flight: " + ex.Message);
					flag = false;
				}
				if (!flag)
				{
					Done.Add(shot.Key);
					shot.Value.Trail?.End(shot.Value.Trail.Head + 0.42f);
					shot.Value.Tracer?.End(shot.Value.Tracer.Head);
				}
			}
			foreach (Projectile item in Done)
			{
				Shots.Remove(item);
			}
		}

		public static void ReleaseIfIdle()
		{
			if (Shots.Count == 0 || Time.frameCount - _updated <= 1)
			{
				return;
			}
			foreach (KeyValuePair<Projectile, Shot> shot in Shots)
			{
				Release(shot.Key, shot.Value);
			}
			Shots.Clear();
			Trails.Clear();
			Tracers.Clear();
		}

		private static void Release(Projectile p, Shot s)
		{
			try
			{
				if (p._state == ProjectileState.Active && p.by_who == s.Owner)
				{
					if (s.Room != null)
					{
						p.setState(ProjectileState.ToRemove);
						return;
					}
					Vector3 current_position_3d = p._current_position_3d;
					float num = Height(s, current_position_3d.x, current_position_3d.y);
					float num2 = s.Rise - 2f * s.Drop * Along(s, current_position_3d.x, current_position_3d.y);
					p._current_position_3d = new Vector3(current_position_3d.x, current_position_3d.y, Mathf.Max(0.02f, num - Surface(current_position_3d.x, current_position_3d.y)));
					p._velocity = new Vector3(s.DirX * s.Flat, s.DirY * s.Flat, num2 * s.Flat);
					p._collision_timeout = 0f;
				}
			}
			catch
			{
			}
		}

		private static bool Fly(Shot s, TerrainCache terrain, SceneCollector scene)
		{
			Projectile p = s.P;
			if (p.asset == null || p.by_who != s.Owner || p._state != ProjectileState.Active || p._vector_start != s.Start)
			{
				return false;
			}
			if (s.Room != null && (!RoomCombat.Active || s.Room != RoomCombat.Key))
			{
				p.setState(ProjectileState.ToRemove);
				return false;
			}
			if (s.Room == null && RoomCombat.Active)
			{
				Release(p, s);
				return false;
			}
			if (!PossessionCombat.Yours(s.Owner))
			{
				Release(p, s);
				return false;
			}
			Vector3 current_position_3d = p._current_position_3d;
			current_position_3d.x -= s.Shift.x;
			current_position_3d.y -= s.Shift.y;
			Vec3 vec = new Vec3(current_position_3d.x, current_position_3d.y, Height(s, current_position_3d.x, current_position_3d.y));
			Vec3 vec2 = s.Last;
			Vec3 vec3 = vec - vec2;
			Vec3 dir = ((Vec3.Dot(vec3, vec3) > 1E-08f) ? vec3.Normalized() : new Vec3(s.DirX, s.DirY, 0f));
			for (int i = 0; i < 6; i++)
			{
				float num = 2f;
				int num2 = 0;
				Actor actor = null;
				float num3 = GroundHit(terrain, vec2, vec);
				if (num3 <= 1f)
				{
					num = num3;
					num2 = 1;
				}
				Vec3 at2;
				if (scene != null && scene.Strikes(vec2, vec, out var at))
				{
					float num4 = Fraction(vec2, vec, at);
					if (num4 < num)
					{
						num = num4;
						num2 = 2;
					}
				}
				else if (s.Room != null && RoomCombat.WallHit(vec2, vec, out at2))
				{
					float num5 = Fraction(vec2, vec, at2);
					if (num5 < num)
					{
						num = num5;
						num2 = 2;
					}
				}
				Hitboxes.Near(vec2, vec, 5f, s.Owner, Near);
				for (int j = 0; j < Near.Count; j++)
				{
					Actor actor2 = Near[j];
					if (!s.Passed.Contains(actor2) && Hitboxes.Get(actor2, out var box) && box.Segment(vec2, vec, s.Pad, out var t) && t < num)
					{
						num = t;
						num2 = 3;
						actor = actor2;
					}
				}
				if (num2 == 0)
				{
					break;
				}
				Vec3 vec4 = vec2 + (vec - vec2) * Mathf.Clamp01(num);
				if (num2 == 3)
				{
					if (Strike(s, actor, vec4, dir))
					{
						return false;
					}
					s.Passed.Add(actor);
					if (Trace)
					{
						Log.Info($"shot passed {actor.asset?.id} at {vec4.X:F2},{vec4.Y:F2},{vec4.Z:F2} (dodged, or it can't be hurt)");
					}
					LastPassed = actor;
					vec2 = vec4;
					continue;
				}
				End(s, vec4, dir, num2 == 2);
				return false;
			}
			bool flag = ((s.Room != null) ? (!RoomCombat.InRoomSpace(vec)) : (vec.X < 0f || vec.Y < 0f || vec.X >= (float)terrain.Width || vec.Y >= (float)terrain.Height));
			if (s.Pellet && Along(s, vec.X, vec.Y) > 24f)
			{
				s.Tracer?.End(Along(s, vec.X, vec.Y), spent: true);
				p.setState(ProjectileState.ToRemove);
				PelletEnded(s, vec);
				Finish("spent", vec, null);
				return false;
			}
			if (flag || Time.time - s.Born > 12f)
			{
				End(s, vec, dir, wall: false);
				return false;
			}
			s.Last = vec;
			Write(s, vec);
			s.Trail?.Reach(Along(s, vec.X, vec.Y));
			s.Tracer?.Reach(Along(s, vec.X, vec.Y));
			return true;
		}

		private static void Write(Shot s, Vec3 at)
		{
			Projectile p = s.P;
			float num = Mathf.Max(1f, at.Z - Surface(at.X, at.Y));
			p._current_position_3d = new Vector3(at.X + s.Shift.x, at.Y + s.Shift.y, num);
			p._velocity = new Vector3(s.DirX * s.Flat, s.DirY * s.Flat, 0f);
			p._collision_timeout = 5f;
			s.Offset = at.Z - num;
		}

		private static bool Strike(Shot s, Actor victim, Vec3 at, Vec3 dir)
		{
			Projectile p = s.P;
			Doorstep.StepOut(victim);
			Vector2 current_position = victim.current_position;
			WorldTile pHitTile = World.world.GetTile((int)current_position.x, (int)current_position.y) ?? victim.current_tile;
			AttackData pData = new AttackData(s.Owner, pHitTile, new Vector3(current_position.x, current_position.y, ((BaseSimObject)victim).getHeight()), s.Owner.current_position, null, p.kingdom, AttackType.Weapon, pMetallicWeapon: false, pSkipShake: false, pProjectile: true, p.asset.id, null, 0.05f);
			int healthBefore = 0;
			try
			{
				healthBefore = victim.getHealth();
			}
			catch
			{
			}
			FriendlyFire.Meant = s.Meant;
			float num = ((!Headshots.Hit(victim, at, dir)) ? 1f : (s.Pellet ? 1.5f : 2f));
			Headshots.Aim(s.Owner, victim, num);
			AttackDataResult pDataResult;
			try
			{
				pDataResult = ((!WorldSync.ForwardShotHit(s.Owner, victim, p.asset.id, num)) ? MapBox.checkAttackFor(pData, victim) : new AttackDataResult
				{
					state = ApplyAttackState.Hit
				});
			}
			finally
			{
				FriendlyFire.Meant = null;
				Headshots.Clear();
			}
			switch (pDataResult.state)
			{
			case ApplyAttackState.Hit:
				s.Trail?.End(Along(s, at.X, at.Y));
				CombatFeedback.ShotLanded(victim, healthBefore);
				Place(p, at, s.Shift);
				if (s.Pellet)
				{
					s.Tracer?.End(Along(s, at.X, at.Y));
					SceneCollector.PelletImpact(at, dir * -1f, ShotgunFx.Surface.Body, 0u);
					p.setState(ProjectileState.ToRemove);
					p._kill_action?.Invoke();
					Struck++;
					PelletEnded(s, at);
					Finish("body", at, victim);
					return true;
				}
				if (s.Arrow)
				{
					Handled.Add(p);
				}
				p.setState(ProjectileState.ToRemove);
				p._kill_action?.Invoke();
				p.targetReached();
				if (s.Arrow)
				{
					StuckArrows.LodgeIn(victim, dir);
				}
				Struck++;
				Finish("body", at, victim);
				return true;
			case ApplyAttackState.Block:
				s.Trail?.End(Along(s, at.X, at.Y));
				s.Tracer?.End(Along(s, at.X, at.Y));
				Place(p, at, s.Shift);
				p.getCollided(new Vector3(p._vector_target.x, p._vector_target.y, 0f));
				Finish("blocked", at, victim);
				return true;
			case ApplyAttackState.Deflect:
				s.Trail?.End(Along(s, at.X, at.Y));
				s.Tracer?.End(Along(s, at.X, at.Y));
				Place(p, at, s.Shift);
				p.getDeflected(new Vector3(p._vector_start.x, p._vector_start.y, 0f), pDataResult);
				Finish("deflected", at, victim);
				return true;
			default:
				return false;
			}
		}

		private static void End(Shot s, Vec3 at, Vec3 dir, bool wall)
		{
			Projectile p = s.P;
			s.Trail?.End(Along(s, at.X, at.Y));
			if (s.Pellet)
			{
				s.Tracer?.End(Along(s, at.X, at.Y));
				if (wall)
				{
					Walled++;
				}
				else
				{
					Grounded++;
				}
				bool flag = !wall && _terrain != null && _terrain.IsWaterAt(at.X, at.Y);
				SceneCollector.PelletImpact(at, dir * -1f, wall ? ShotgunFx.Surface.Wall : (flag ? ShotgunFx.Surface.Water : ShotgunFx.Surface.Ground), GroundColor(at.X, at.Y));
				if (!wall && _patterVolley != _shotVolley)
				{
					_patterVolley = _shotVolley;
					Patters++;
					PatterAt = at;
				}
				PelletEnded(s, at);
				Finish(wall ? "wall" : "ground", at, null);
				p._velocity = Vector3.zero;
				p.setState(ProjectileState.ToRemove);
				return;
			}
			if (wall)
			{
				Walled++;
				if (s.Arrow && s.Room == null)
				{
					StuckArrows.Stick(at + dir * 0.0968f, dir, ground: false);
					Handled.Add(p);
				}
				at -= new Vec3(dir.X, dir.Y, 0f) * 0.12f;
			}
			else
			{
				Grounded++;
			}
			Finish(wall ? "wall" : "ground", at, null);
			if (s.Room != null && s.Bursts)
			{
				RoomCombat.FireLanded(s.Owner, p.asset.id);
			}
			p._current_position_3d = new Vector3(at.X + s.Shift.x, at.Y + s.Shift.y, 0f);
			p._velocity = Vector3.zero;
			if (s.Bursts)
			{
				p._collision_timeout = 0f;
				return;
			}
			try
			{
				EffectsLibrary.spawnAt("fx_miss", new Vector3(at.X + s.Shift.x, at.Y + s.Shift.y, 0f), 0.1f);
			}
			catch
			{
			}
			p.setState(p.asset.can_be_left_on_ground ? ProjectileState.AlphaAnimation : ProjectileState.ToRemove);
			p.targetReached();
		}

		private static uint GroundColor(float x, float y)
		{
			TerrainCache terrain = _terrain;
			if (terrain == null || terrain.Colors == null || terrain.Colors.Length == 0)
			{
				return TerrainCache.Rgb(120, 104, 80);
			}
			int num = Mathf.Clamp((int)x, 0, terrain.Width - 1);
			int num2 = Mathf.Clamp((int)y, 0, terrain.Height - 1);
			return terrain.Colors[num2 * terrain.Width + num];
		}

		private static void Finish(string what, Vec3 at, Actor victim)
		{
			if (Trace)
			{
				Log.Info(string.Format("shot ended at {0} {1:F2},{2:F2},{3:F2}{4}", what, at.X, at.Y, at.Z, (victim != null) ? (" in " + victim.asset?.id) : ""));
			}
			LastWhat = what;
			LastEnd = at;
			LastVictim = victim;
		}

		private static void Place(Projectile p, Vec3 at, Vector2 shift = default(Vector2))
		{
			float num = ((_terrain != null) ? _terrain.SampleHeight(at.X, at.Y) : 0f);
			p._current_position_3d = new Vector3(at.X + shift.x, at.Y + shift.y, Mathf.Max(0.02f, at.Z - num));
		}

		private static bool InRoom()
		{
			if (RoomCombat.Active && RoomCombat.Room != null)
			{
				return FirstPersonAim.Terrain == RoomCombat.Room.Floor;
			}
			return false;
		}

		internal static bool RoomShot(Projectile p, out Vec3 at)
		{
			at = default(Vec3);
			if (p == null || !Shots.TryGetValue(p, out var value) || value.Room == null)
			{
				return false;
			}
			at = value.Last;
			return true;
		}

		private static Vec3 Point(Shot s, float d)
		{
			return new Vec3(s.From.X + s.DirX * d, s.From.Y + s.DirY * d, s.From.Z + d * s.Rise - d * d * s.Drop);
		}

		private static float Along(Shot s, float x, float y)
		{
			return (x - s.From.X) * s.DirX + (y - s.From.Y) * s.DirY;
		}

		private static float Height(Shot s, float x, float y)
		{
			float num = Along(s, x, y);
			return s.From.Z + num * s.Rise - num * num * s.Drop;
		}

		private static float Fraction(Vec3 a, Vec3 b, Vec3 p)
		{
			Vec3 vec = b - a;
			float num = Vec3.Dot(vec, vec);
			if (!(num > 1E-10f))
			{
				return 0f;
			}
			return Mathf.Clamp01(Vec3.Dot(p - a, vec) / num);
		}

		private static float Surface(float x, float y)
		{
			TerrainCache terrain = _terrain;
			if (terrain == null || terrain.Ground.Length == 0 || x < 0f || y < 0f || x >= (float)terrain.Width || y >= (float)terrain.Height)
			{
				return 0f;
			}
			float num = x - 0.5f;
			float num2 = y - 0.5f;
			int num3 = Mathf.FloorToInt(num);
			int num4 = Mathf.FloorToInt(num2);
			float num5 = num - (float)num3;
			float num6 = num2 - (float)num4;
			float num7 = At(terrain, num3, num4);
			float num8 = At(terrain, num3 + 1, num4);
			float num9 = At(terrain, num3, num4 + 1);
			float num10 = At(terrain, num3 + 1, num4 + 1);
			float num11 = num7 + (num8 - num7) * num5;
			float num12 = num9 + (num10 - num9) * num5;
			float num13 = num11 + (num12 - num11) * num6;
			byte b = FlagsAt(terrain, num3, num4);
			byte b2 = FlagsAt(terrain, num3 + 1, num4);
			byte b3 = FlagsAt(terrain, num3, num4 + 1);
			byte b4 = FlagsAt(terrain, num3 + 1, num4 + 1);
			if (((b | b2 | b3 | b4) & 0x40) != 0)
			{
				num13 = TerrainCache.HollowHeight(terrain.Ground, terrain.Width, terrain.Height, num3, num4, num5, num6, num13, b, b2, b3, b4);
			}
			float num14 = terrain.WaterTop(x, y);
			if (!float.IsNaN(num14))
			{
				return num14;
			}
			return num13;
		}

		private static float At(TerrainCache t, int x, int y)
		{
			x = Mathf.Clamp(x, 0, t.Width - 1);
			y = Mathf.Clamp(y, 0, t.Height - 1);
			return t.Ground[y * t.Width + x];
		}

		private static byte FlagsAt(TerrainCache t, int x, int y)
		{
			x = Mathf.Clamp(x, 0, t.Width - 1);
			y = Mathf.Clamp(y, 0, t.Height - 1);
			return t.Flags[y * t.Width + x];
		}

		private static bool Grazes(Vec3 a, Vec3 b)
		{
			float num = b.X - a.X;
			float num2 = b.Y - a.Y;
			float num3 = Mathf.Sqrt(num * num + num2 * num2);
			if (num3 < 0.5f)
			{
				return false;
			}
			float num4 = 1f - 0.3f / num3;
			for (float num5 = 0.05f; num5 < num4; num5 += 0.15f / num3)
			{
				if (a.Z + (b.Z - a.Z) * num5 < Surface(a.X + num * num5, a.Y + num2 * num5) + 0.03f)
				{
					return true;
				}
			}
			return false;
		}

		private static float GroundHit(TerrainCache terrain, Vec3 a, Vec3 b)
		{
			if (a.Z <= Surface(a.X, a.Y))
			{
				return 0f;
			}
			float num = b.X - a.X;
			float num2 = b.Y - a.Y;
			int num3 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(num * num + num2 * num2) / 0.1f), 1, 200);
			float num4 = 0f;
			for (int i = 1; i <= num3; i++)
			{
				float num5 = (float)i / (float)num3;
				if (a.Z + (b.Z - a.Z) * num5 > Surface(a.X + num * num5, a.Y + num2 * num5))
				{
					num4 = num5;
					continue;
				}
				float num6 = num4;
				float num7 = num5;
				for (int j = 0; j < 6; j++)
				{
					float num8 = (num6 + num7) * 0.5f;
					if (a.Z + (b.Z - a.Z) * num8 > Surface(a.X + num * num8, a.Y + num2 * num8))
					{
						num6 = num8;
					}
					else
					{
						num7 = num8;
					}
				}
				return num7;
			}
			return 2f;
		}

		private static HashSet<Projectile> All()
		{
			try
			{
				return (World.world != null && World.world.projectiles != null) ? ((CoreSystemManager<Projectile, ProjectileData>)World.world.projectiles)._hashset : null;
			}
			catch
			{
				return null;
			}
		}

		private static void ForgetHandled()
		{
			HandledGone.Clear();
			HashSet<Projectile> hashSet = All();
			foreach (Projectile item in Handled)
			{
				if (hashSet == null || !hashSet.Contains(item))
				{
					HandledGone.Add(item);
				}
			}
			foreach (Projectile item2 in HandledGone)
			{
				Handled.Remove(item2);
			}
		}

		internal static bool FlyingForTest(out Vec3 at, out float ground)
		{
			using (Dictionary<Projectile, Shot>.Enumerator enumerator = Shots.GetEnumerator())
			{
				if (enumerator.MoveNext())
				{
					at = enumerator.Current.Value.Last;
					ground = Surface(at.X, at.Y);
					return true;
				}
			}
			at = default(Vec3);
			ground = 0f;
			return false;
		}

		public static void EmitTrails(List<Streak> into, float dt)
		{
			int count = into.Count;
			for (int num = Trails.Count - 1; num >= 0; num--)
			{
				ArrowTrail arrowTrail = Trails[num];
				arrowTrail.Step(dt);
				if (arrowTrail.Done)
				{
					Trails.RemoveAt(num);
				}
				else
				{
					arrowTrail.Emit(into);
				}
			}
			for (int num2 = Tracers.Count - 1; num2 >= 0; num2--)
			{
				PelletTrail pelletTrail = Tracers[num2];
				pelletTrail.Step(dt);
				if (pelletTrail.Done)
				{
					Tracers.RemoveAt(num2);
				}
				else
				{
					pelletTrail.Emit(into);
				}
			}
			StreaksEmitted = into.Count - count;
		}

		public static void Clear()
		{
			Shots.Clear();
			Handled.Clear();
			Trails.Clear();
			Tracers.Clear();
		}
	}
}
