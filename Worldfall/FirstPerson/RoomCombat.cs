using System;
using System.Collections.Generic;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal static class RoomCombat
	{
		private enum Side
		{
			Fight,
			Flee
		}

		private sealed class Fighter
		{
			public Side Side;

			public Actor Target;

			public float LastTimer = -1f;

			public float Since;

			public float Waiting;

			public bool Told;
		}

		private sealed class Pursuer
		{
			public float Seen;

			public float Due = -1f;

			public long Id;
		}

		internal static bool Drawing;

		internal static bool Enabled = true;

		private static int _frame = -100;

		private static readonly Dictionary<Actor, Vector2> Home = new Dictionary<Actor, Vector2>();

		private static Vector2 _youSynced;

		private static readonly List<Actor> People = new List<Actor>();

		internal static object FallingIn;

		internal static float FallingZ;

		private const float SackScale = 0.42f;

		private static readonly Dictionary<Loot.Fallen, MeshInstance> Sacks = new Dictionary<Loot.Fallen, MeshInstance>();

		private static readonly List<Loot.Fallen> _goneSacks = new List<Loot.Fallen>();

		private static readonly Dictionary<Actor, Fighter> Fighters = new Dictionary<Actor, Fighter>();

		private static readonly List<Actor> _scratch = new List<Actor>();

		private static readonly List<Actor> _stepping = new List<Actor>();

		private static readonly List<Actor> _goingOut = new List<Actor>();

		private static readonly HashSet<Actor> Out = new HashSet<Actor>();

		internal static int Blows;

		internal static int Fled;

		internal static int CameIn;

		internal static int WentOut;

		private const float RunAt = 0.15f;

		private static readonly HashSet<string> _reported = new HashSet<string>();

		internal static int Kindled;

		private static bool _burning;

		private static float _blazeAge;

		private static readonly List<MeshInstance> Flames = new List<MeshInstance>();

		private static readonly List<Vec3> FlameAt = new List<Vec3>();

		private static BlastFx.CloudSlots _flameSlots;

		private static readonly System.Random FireRng = new System.Random(11);

		private static readonly Dictionary<Actor, Pursuer> Pursuers = new Dictionary<Actor, Pursuer>();

		private static readonly List<Actor> _gonePursuers = new List<Actor>();

		private const float PursueMemory = 12f;

		private const float FollowRange = 16f;

		private const float PursuePace = 1.6f;

		private static float _pursueCheck;

		private static float _watchIn;

		private static readonly Dictionary<Projectile, MeshInstance> ShotMeshes = new Dictionary<Projectile, MeshInstance>();

		private static readonly List<Projectile> _goneShots = new List<Projectile>();

		private static BlastFx.CloudSlots _boltSlots;

		internal static object Key { get; private set; }

		internal static Building Building { get; private set; }

		internal static Interior Room { get; private set; }

		internal static RoomWalkers Walkers { get; private set; }

		internal static List<BaseSimObject> Tagged { get; private set; }

		internal static Actor You { get; private set; }

		internal static Vector2 Eye { get; private set; }

		internal static Vector2 Offset { get; private set; }

		internal static bool Active
		{
			get
			{
				if (Key != null && You != null)
				{
					return Time.frameCount - _frame <= 2;
				}
				return false;
			}
		}

		internal static IReadOnlyList<Actor> PeopleForTest => People;

		internal static bool Ablaze
		{
			get
			{
				if (Active && Building != null)
				{
					return Torches.Burning(Building);
				}
				return false;
			}
		}

		internal static Vector2 ToWorld(Vector2 room)
		{
			return room + Offset;
		}

		internal static Vector2 ToRoom(Vector2 world)
		{
			return world - Offset;
		}

		public static void Begin(object key, Building building, Interior room, RoomWalkers walkers, List<BaseSimObject> tagged, Actor you, Vector2 eye, Vector2 offset)
		{
			if (!Enabled || key == null || room == null || walkers == null || you == null)
			{
				End();
				return;
			}
			if (key != Key || you != You)
			{
				End();
				WatchNow(you);
			}
			Key = key;
			Building = building;
			Room = room;
			Walkers = walkers;
			Tagged = tagged;
			You = you;
			Eye = eye;
			Offset = offset;
			_frame = Time.frameCount;
			Sync();
		}

		public static void End()
		{
			if (Key == null)
			{
				return;
			}
			try
			{
				FightGoesOut();
			}
			catch (Exception ex)
			{
				Log.Error("room fight: going out: " + ex.Message);
			}
			foreach (KeyValuePair<Actor, Vector2> item in Home)
			{
				Actor key = item.Key;
				Hitboxes.Forget(key);
				try
				{
					if (key == null || !key.isAlive() || Out.Contains(key))
					{
						continue;
					}
					if (key == You)
					{
						if ((key.current_position - _youSynced).sqrMagnitude < 1E-06f)
						{
							key.current_position = item.Value;
						}
						continue;
					}
					bool num;
					if (Building == null)
					{
						num = key.is_inside_boat;
						goto IL_00c6;
					}
					if (!key.is_inside_building)
					{
						continue;
					}
					num = key.inside_building == Building;
					goto IL_00c6;
					IL_00c6:
					if (num)
					{
						key.current_position = item.Value;
					}
				}
				catch
				{
				}
			}
			Home.Clear();
			Out.Clear();
			ClearFighters();
			ClearFire();
			Key = null;
			Building = null;
			Room = null;
			Walkers = null;
			Tagged = null;
			You = null;
			Drawing = false;
			Hitboxes.Room = null;
		}

		internal static void Sync()
		{
			if (Key == null || You == null)
			{
				return;
			}
			Place(You, Eye + Offset);
			_youSynced = Eye + Offset;
			People.Clear();
			if (Walkers != null)
			{
				foreach (RoomWalkers.Walker item in Walkers.All)
				{
					if (item.Who != null)
					{
						bool flag;
						try
						{
							flag = item.Who.isAlive();
						}
						catch
						{
							flag = false;
						}
						if (flag)
						{
							Place(item.Who, item.Pos + Offset);
							People.Add(item.Who);
						}
					}
				}
			}
			Hitboxes.Room = People;
		}

		internal static void MovedTo(Vector2 eye)
		{
			Eye = eye;
			if (Active)
			{
				Place(You, Eye + Offset);
				_youSynced = Eye + Offset;
			}
		}

		private static void Place(Actor a, Vector2 at)
		{
			try
			{
				if (a != null && a.isAlive())
				{
					if (!Home.ContainsKey(a))
					{
						Home[a] = a.current_position;
					}
					a.current_position = at;
				}
			}
			catch
			{
			}
		}

		internal static bool In(BaseSimObject o)
		{
			if (!Active || o == null)
			{
				return false;
			}
			try
			{
				if (!o.isActor())
				{
					return false;
				}
				Actor a = o.a;
				if (a == You)
				{
					return true;
				}
				if (Walkers == null || Walkers.Of(a) == null)
				{
					return false;
				}
				return a.isAlive();
			}
			catch
			{
				return false;
			}
		}

		internal static bool Judge(BaseSimObject by, BaseSimObject target, bool checkFactions, out bool allowed)
		{
			allowed = false;
			if (!Active || by == null || target == null)
			{
				return false;
			}
			bool flag = In(by);
			bool flag2 = In(target);
			if (!flag && !flag2)
			{
				return false;
			}
			if (flag != flag2)
			{
				return true;
			}
			allowed = CanAttackIndoors(by, target, checkFactions);
			return true;
		}

		private static bool CanAttackIndoors(BaseSimObject by, BaseSimObject target, bool checkFactions)
		{
			Actor actor = (target.isActor() ? target.a : null);
			bool is_inside_building = false;
			bool is_inside_boat = false;
			try
			{
				if (actor != null)
				{
					is_inside_building = actor.is_inside_building;
					is_inside_boat = actor.is_inside_boat;
					actor.is_inside_building = false;
					actor.is_inside_boat = false;
				}
				return by.canAttackTarget(target, checkFactions, pAttackBuildings: true);
			}
			catch
			{
				return false;
			}
			finally
			{
				if (actor != null)
				{
					try
					{
						actor.is_inside_building = is_inside_building;
						actor.is_inside_boat = is_inside_boat;
					}
					catch
					{
					}
				}
			}
		}

		internal static bool Witnesses(Kingdom k, Actor victim, Actor you, List<Actor> into)
		{
			if (!Active || Walkers == null)
			{
				return false;
			}
			if (victim != null && !In(victim) && (Walkers.Of(victim) == null || Out.Contains(victim)) && !Walkers.FellHere(victim))
			{
				return false;
			}
			into.Clear();
			foreach (RoomWalkers.Walker item in Walkers.All)
			{
				Actor who = item.Who;
				try
				{
					if (who != null && who != victim && who != you && who.isAlive() && who.isAdult() && who.kingdom == k && !HouseOccupancy.Asleep(who) && !(item.LieShown > 0.5f))
					{
						into.Add(who);
					}
				}
				catch
				{
				}
			}
			return true;
		}

		internal static Vector2 Drawn(Vector2 world)
		{
			if (!Drawing)
			{
				return world;
			}
			return world - Offset;
		}

		public static void DrawFallen(List<MeshInstance> meshes, float dt, Vector2 eye)
		{
			if (!Active)
			{
				return;
			}
			Corpses.Draw(meshes, dt, eye.x, eye.y, Key);
			MeshModel meshModel = Models.Get("int_sack");
			_goneSacks.Clear();
			foreach (KeyValuePair<Loot.Fallen, MeshInstance> sack in Sacks)
			{
				if (!Loot.Bodies.Contains(sack.Key))
				{
					_goneSacks.Add(sack.Key);
				}
			}
			foreach (Loot.Fallen goneSack in _goneSacks)
			{
				Sacks.Remove(goneSack);
			}
			if (meshModel == null)
			{
				return;
			}
			foreach (Loot.Fallen body in Loot.Bodies)
			{
				if (body.Sack && body.Space == Key)
				{
					if (!Sacks.TryGetValue(body, out var value))
					{
						Dictionary<Loot.Fallen, MeshInstance> sacks = Sacks;
						MeshInstance obj = new MeshInstance
						{
							Model = meshModel,
							Static = true
						};
						value = obj;
						sacks[body] = obj;
						Vector2 room = OffTheDoorway(ToRoom(body.At), body.Z);
						body.At = ToWorld(room);
						value.Place(room.x, room.y, body.Z, (float)(body.Id % 628) / 100f, 0.42f, 0.42f, 0.42f);
					}
					value.SortDepth = new Vector2(value.Position.X - eye.x, value.Position.Y - eye.y).magnitude;
					meshes.Add(value);
				}
			}
		}

		internal static Vector2 FallWay(object space, Vector2 feet, float z, Vector2 away, float length, bool sides = false)
		{
			if (!Active || Room == null || space != Key)
			{
				return away;
			}
			return Corpses.LieWay(feet, away, length, sides, (Vector2 p) => OpenFloor(p, z));
		}

		internal static bool Keeps(object space, Vector2 from, Vector2 to, float z)
		{
			if (!Active || Room == null || space != Key)
			{
				return true;
			}
			if (OpenFloor(to, z))
			{
				return true;
			}
			if (OpenFloor(from, z))
			{
				return false;
			}
			return Vector2.Dot(rhs: new Vector2(0f - Room.DoorOutX, 0f - Room.DoorOutY), lhs: to - from) > 0f;
		}

		private static Vector2 OffTheDoorway(Vector2 at, float z)
		{
			if (Room == null || OpenFloor(at, z))
			{
				return at;
			}
			Vector2 vector = new Vector2(0f - Room.DoorOutX, 0f - Room.DoorOutY);
			if (vector.sqrMagnitude < 1E-06f)
			{
				return at;
			}
			vector.Normalize();
			for (float num = 0.15f; num <= 1.5f; num += 0.15f)
			{
				for (int i = 0; i < 5; i++)
				{
					float f = (float)((i + 1) / 2) * 35f * ((i % 2 == 1) ? 1f : (-1f)) * (MathF.PI / 180f);
					float num2 = Mathf.Cos(f);
					float num3 = Mathf.Sin(f);
					Vector2 vector2 = at + new Vector2(vector.x * num2 - vector.y * num3, vector.x * num3 + vector.y * num2) * num;
					if (OpenFloor(vector2, z))
					{
						return vector2;
					}
				}
			}
			return at;
		}

		private static bool OpenFloor(Vector2 p, float z)
		{
			Interior room = Room;
			Vector2 rhs = new Vector2(room.DoorOutX, room.DoorOutY);
			if (rhs.sqrMagnitude > 1E-06f)
			{
				rhs.Normalize();
			}
			if (room.Fit != null)
			{
				if (!room.Fit.Clear(room.Fit.StoreyAt(z + 0.05f), p.x, p.y, 0.1f))
				{
					return false;
				}
				if (float.IsNaN(room.Fit.DoorWallX) || room.Fit.StoreyAt(z + 0.05f) != 0)
				{
					return true;
				}
				Vector2 lhs = p - new Vector2(room.Fit.DoorWallX, room.Fit.DoorWallY);
				float num = Vector2.Dot(lhs, rhs);
				if (Mathf.Abs(lhs.x * (0f - rhs.y) + lhs.y * rhs.x) < room.Fit.DoorHalf + 0.15f)
				{
					return !(num > -0.6f);
				}
				return true;
			}
			if (p.x < room.MinX + 0.1f || p.x > room.MaxX - 0.1f || p.y < room.MinY + 0.1f || p.y > room.MaxY - 0.1f)
			{
				return false;
			}
			return (p - new Vector2(room.DoorX, room.DoorY)).sqrMagnitude > 0.36f;
		}

		internal static bool LootPrompt(WorldBoxMod mod, Actor you, float yaw, out string prompt)
		{
			prompt = null;
			if (!Active || mod == null)
			{
				return false;
			}
			Loot.Fallen fallen = Loot.Near(Eye + Offset, yaw, 1.32f, Key);
			if (fallen == null)
			{
				return false;
			}
			prompt = (fallen.Sack ? ("E:Open " + fallen.Name + "'s bag") : ("E:Search " + fallen.Name + "'s body"));
			if (PlayInput.GetKeyDown(KeyCode.E))
			{
				Loot.Take(you, fallen, mod.Toast);
			}
			return true;
		}

		private static void ClearFighters()
		{
			Fighters.Clear();
		}

		internal static bool Fighting(Actor a)
		{
			if (a != null && Fighters.TryGetValue(a, out var value))
			{
				return value.Side == Side.Fight;
			}
			return false;
		}

		internal static bool Fleeing(Actor a)
		{
			if (a != null && Fighters.TryGetValue(a, out var value))
			{
				return value.Side == Side.Flee;
			}
			return false;
		}

		internal static Actor TargetOf(Actor a)
		{
			if (a == null || !Fighters.TryGetValue(a, out var value) || value.Side != Side.Fight)
			{
				return null;
			}
			return value.Target;
		}

		internal static void Struck(BaseSimObject by, BaseSimObject target)
		{
			try
			{
				if (!Active || by == null || target == null || !by.isActor() || !target.isActor())
				{
					return;
				}
				Actor a = by.a;
				Actor a2 = target.a;
				if (a == a2 || !In(a) || !In(a2))
				{
					return;
				}
				if (a2 != You)
				{
					React(a2, a, a2, struck: true);
				}
				if (a != You && !Fighting(a))
				{
					Engage(a, a2);
				}
				List<Actor> list = new List<Actor>();
				foreach (RoomWalkers.Walker item in Walkers.All)
				{
					if (item.Who != null && item.Who != a && item.Who != a2)
					{
						list.Add(item.Who);
					}
				}
				foreach (Actor item2 in list)
				{
					React(item2, a, a2, struck: false);
				}
			}
			catch (Exception e)
			{
				Report("struck", e);
			}
		}

		private static void React(Actor b, Actor aggressor, Actor victim, bool struck)
		{
			if (b != null && b != You && b.isAlive() && (!Fighters.TryGetValue(b, out var value) || (value.Side != Side.Flee && (value.Target == null || !value.Target.isAlive()))))
			{
				Actor actor = (struck ? aggressor : Opponent(b, aggressor, victim));
				if (actor != null && CanFight(b))
				{
					Engage(b, actor);
				}
				else if (actor != null || !Family(b, You))
				{
					Flee(b);
				}
			}
		}

		private static Actor Opponent(Actor b, Actor aggressor, Actor victim)
		{
			try
			{
				if (aggressor == You)
				{
					if (Family(b, You))
					{
						return null;
					}
					if (Foes(b, You))
					{
						return You;
					}
					if (Fierce(b))
					{
						return You;
					}
					if (victim != null && victim != You && HomeLock.IsFamily(b, victim))
					{
						return You;
					}
					return null;
				}
				if (aggressor.kingdom != null && aggressor.kingdom == b.kingdom && !Foes(aggressor, b))
				{
					return null;
				}
				if (Fierce(b) || (victim != null && HomeLock.IsFamily(b, victim)))
				{
					return aggressor;
				}
				return null;
			}
			catch
			{
				return null;
			}
		}

		private static bool Fierce(Actor a)
		{
			try
			{
				return a.isWarrior() || a.isKing() || a.isCityLeader() || a.hasTrait("bloodlust") || a.hasTrait("veteran") || a.hasTrait("strong") || a.hasTrait("brave");
			}
			catch
			{
				return false;
			}
		}

		private static bool CanFight(Actor a)
		{
			try
			{
				if (!a.isAlive() || !a.isAdult() || a.asset == null || a.asset.skip_fight_logic)
				{
					return false;
				}
				if (a.hasTrait("peaceful") || a.hasTrait("pacifist"))
				{
					return false;
				}
				return a.getHealthRatio() >= 0.15f;
			}
			catch
			{
				return false;
			}
		}

		private static bool Family(Actor a, Actor b)
		{
			try
			{
				return a != null && b != null && HomeLock.IsFamily(a, b);
			}
			catch
			{
				return false;
			}
		}

		private static bool Foes(Actor a, Actor b)
		{
			try
			{
				return a.kingdom != null && b.kingdom != null && a.kingdom != b.kingdom && a.kingdom.isEnemy(b.kingdom);
			}
			catch
			{
				return false;
			}
		}

		private static void Engage(Actor a, Actor target)
		{
			if (a != null && target != null && a != You && a.isAlive())
			{
				if (!Fighters.TryGetValue(a, out var value))
				{
					value = (Fighters[a] = new Fighter());
				}
				if (value.Side != Side.Fight || value.Target != target)
				{
					Log.Info("room fight: " + WorldBoxMod.Describe(a) + " fights " + ((target == You) ? "you" : WorldBoxMod.Describe(target)));
				}
				value.Side = Side.Fight;
				value.Target = target;
				Walkers?.SetFight(a, fight: true, flee: false);
			}
		}

		private static void Flee(Actor a)
		{
			if (a != null && a.isAlive())
			{
				if (!Fighters.TryGetValue(a, out var value))
				{
					value = (Fighters[a] = new Fighter());
				}
				if (value.Side != Side.Flee)
				{
					value.Side = Side.Flee;
					value.Target = null;
					Release(a);
					Walkers?.SetFight(a, fight: false, flee: true);
					Log.Info("room fight: " + WorldBoxMod.Describe(a) + " runs for the door");
				}
			}
		}

		private static void Release(Actor a)
		{
			try
			{
				if (a.timer_action > 0.05f)
				{
					a.timer_action = 0.05f;
				}
			}
			catch
			{
			}
		}

		public static void Tick(float dt)
		{
			if (!Active || Walkers == null)
			{
				return;
			}
			dt = Mathf.Min(dt, 0.1f);
			try
			{
				Sync();
				FollowIn();
				foreach (RoomWalkers.Walker item in Walkers.All)
				{
					Actor who = item.Who;
					if (who != null && !Fighters.ContainsKey(who))
					{
						bool flag = false;
						try
						{
							flag = who.isAlive() && ((who.has_attack_target && who.attack_target == You) || (Foes(who, You) && CanFight(who) && !HouseOccupancy.Asleep(who)));
						}
						catch
						{
						}
						if (flag && CanFight(who))
						{
							Engage(who, You);
						}
					}
				}
				_stepping.Clear();
				_stepping.AddRange(Fighters.Keys);
				foreach (Actor item2 in _stepping)
				{
					if (Fighters.TryGetValue(item2, out var value))
					{
						Step(item2, value, dt);
					}
				}
			}
			catch (Exception e)
			{
				Report("tick", e);
			}
		}

		private static void Step(Actor a, Fighter f, float dt)
		{
			RoomWalkers.Walker walker = Walkers.Of(a);
			bool flag;
			try
			{
				flag = a.isAlive();
			}
			catch
			{
				flag = false;
			}
			if (walker == null || !flag)
			{
				if (flag && walker == null && f.Side == Side.Flee)
				{
					LetOut(a, running: true);
				}
				Fighters.Remove(a);
				return;
			}
			f.Since += dt;
			if (f.Side == Side.Flee)
			{
				if (walker.Leaving && walker.Path.Count == 0 && f.Since > 0.3f)
				{
					LetOut(a, running: true);
				}
				return;
			}
			Actor actor = f.Target;
			if (actor == null || !In(actor))
			{
				actor = null;
				foreach (KeyValuePair<Actor, Fighter> fighter in Fighters)
				{
					if (fighter.Value.Side == Side.Fight && fighter.Value.Target == a && In(fighter.Key))
					{
						actor = fighter.Key;
						break;
					}
				}
				if (actor == null && In(You) && (Foes(a, You) || TargetOf(a) == You))
				{
					actor = You;
				}
				if (actor == null)
				{
					Calm(a, walker);
					return;
				}
				f.Target = actor;
			}
			float num = 1f;
			try
			{
				num = a.getHealthRatio();
			}
			catch
			{
			}
			if (num < 0.15f)
			{
				Flee(a);
				return;
			}
			Hold(a, actor);
			Vector2 vector;
			int num2;
			if (actor == You)
			{
				vector = Eye;
				num2 = Walkers.YouLevel;
			}
			else
			{
				RoomWalkers.Walker walker2 = Walkers.Of(actor);
				if (walker2 == null)
				{
					f.Target = null;
					return;
				}
				vector = walker2.Pos;
				num2 = walker2.Level;
			}
			float num3 = 0.5f;
			float num4 = 0.15f;
			try
			{
				num3 = a.getAttackRange();
				num4 = ((BaseSimObject)actor).stats["size"] + ((BaseSimObject)a).stats["size"];
			}
			catch
			{
			}
			bool flag2 = false;
			try
			{
				flag2 = ((BaseSimObject)a).hasStatus("stunned");
			}
			catch
			{
			}
			walker.ChaseAt = (flag2 ? walker.Pos : vector);
			if (flag2)
			{
				walker.ChaseAt += (vector - walker.Pos).normalized * 0.001f;
			}
			walker.ChaseLevel = (flag2 ? walker.Level : num2);
			walker.Reach = Mathf.Max(0.3f, (num3 + num4) * 0.8f);
			walker.Fighting = true;
			try
			{
				if (a.attack_timer > 0f && Mathf.Abs(a.attack_timer - f.LastTimer) < 1E-05f)
				{
					a.attack_timer = Mathf.Max(0f, a.attack_timer - dt);
				}
				f.LastTimer = a.attack_timer;
			}
			catch
			{
			}
			bool flag3 = (walker.Pos - vector).magnitude <= num3 + num4 + 0.1f && walker.Level == num2 && walker.Sit <= 0f && walker.LieShown <= 0f;
			bool flag4 = false;
			try
			{
				flag4 = a.isAttackReady() && !((BaseSimObject)a).hasStatus("stunned");
			}
			catch
			{
			}
			if (flag3 && flag4)
			{
				Strike(a, actor);
			}
			f.Waiting = ((flag3 && !flag4) ? (f.Waiting + dt) : 0f);
			if (!(f.Waiting > 3f) || f.Told)
			{
				return;
			}
			f.Told = true;
			try
			{
				Log.Info(string.Format("room fight: {0} in reach but no blow: attack timer {1:F2}, cooldown {2:F2}, stunned {3}, action timer {4:F2}, frozen {5}", WorldBoxMod.Describe(a), a.attack_timer, a.getAttackCooldown(), ((BaseSimObject)a).hasStatus("stunned"), a.timer_action, ((BaseSimObject)a).hasStatus("frozen")));
			}
			catch
			{
			}
		}

		private static void Calm(Actor a, RoomWalkers.Walker w)
		{
			Fighters.Remove(a);
			w.Fighting = false;
			Release(a);
			try
			{
				if (a.has_attack_target)
				{
					a.clearAttackTarget();
				}
			}
			catch
			{
			}
			Log.Info("room fight: " + WorldBoxMod.Describe(a) + " calms down");
		}

		private static void Hold(Actor a, Actor target)
		{
			try
			{
				if (Building != null && (!a.is_inside_building || a.inside_building != Building))
				{
					a.stayInBuilding(Building);
				}
				if (a.timer_action < 0.4f)
				{
					a.timer_action = 0.4f;
				}
				if (!a.has_attack_target || a.attack_target != target)
				{
					a.setAttackTarget(target);
				}
			}
			catch
			{
			}
		}

		private static void Strike(Actor a, Actor target)
		{
			Sync();
			try
			{
				Vector2 current_position = target.current_position;
				Vector3 pAttackPosition = new Vector3(current_position.x, current_position.y + ((BaseSimObject)target).getHeight(), 0f);
				a.tryToAttack((BaseSimObject)target, pDoChecks: false, (Action)null, pAttackPosition, (Kingdom)null, target.current_tile, 0f);
				Blows++;
			}
			catch (Exception e)
			{
				Report("blow", e);
			}
		}

		private static void LetOut(Actor a, bool running)
		{
			try
			{
				if (Building == null)
				{
					Out.Add(a);
					Walkers?.Drop(a);
					Release(a);
					if (running)
					{
						Fled++;
					}
					Fighters.Remove(a);
					return;
				}
				Vector2 current_position = ((Walkers != null) ? (Walkers.Door + Offset) : a.current_position);
				if (a.is_inside_building)
				{
					a.exitBuilding();
				}
				a.current_position = current_position;
				a.dirty_current_tile = true;
				a.timer_action = 0f;
				Out.Add(a);
				Walkers?.Drop(a);
				if (running)
				{
					a.cancelAllBeh();
					a.setTask("run_away", pClean: true, pCleanJob: false, pForceAction: true);
					Fled++;
				}
				else
				{
					((BaseSimObject)a).clearIgnoreTargets();
					a.startFightingWith(You);
					WentOut++;
				}
			}
			catch (Exception e)
			{
				Report("out", e);
			}
			Fighters.Remove(a);
		}

		private static void FightGoesOut()
		{
			_goingOut.Clear();
			_goingOut.AddRange(Fighters.Keys);
			foreach (Actor item in _goingOut)
			{
				if (!Fighters.TryGetValue(item, out var value))
				{
					continue;
				}
				bool flag;
				try
				{
					flag = item.isAlive();
				}
				catch
				{
					flag = false;
				}
				if (!flag)
				{
					continue;
				}
				if (value.Side == Side.Flee)
				{
					LetOut(item, running: true);
					continue;
				}
				if (value.Target == You)
				{
					LetOut(item, running: false);
					continue;
				}
				Release(item);
				try
				{
					if (item.has_attack_target)
					{
						item.clearAttackTarget();
					}
				}
				catch
				{
				}
			}
			Fighters.Clear();
		}

		private static void Report(string what, Exception e)
		{
			if (_reported.Add(what))
			{
				Log.Error("room fight: " + what + ": " + e);
			}
		}

		internal static void SwungAt(Actor you, float reach)
		{
			if (Active && Torches.MainLit && Building != null && !(FirstPersonAim.PickDepth > reach + 0.35f))
			{
				Kindle(you);
			}
		}

		internal static void FireLanded(Actor you, string projectile)
		{
			if (Active && Building != null && !string.IsNullOrEmpty(projectile) && (projectile.IndexOf("fire", StringComparison.Ordinal) >= 0 || !(projectile != "napalm") || !(projectile != "plasma_ball")))
			{
				Kindle(you);
			}
		}

		private static void Kindle(Actor you)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			if (Torches.Ignite(you, Building, (instance != null) ? new Action<string>(instance.Toast) : null))
			{
				Kindled++;
				Log.Info("room fight: the house catches fire from inside");
			}
		}

		private static void ClearFire()
		{
			_burning = false;
			_blazeAge = 0f;
			Flames.Clear();
			FlameAt.Clear();
		}

		public static void Blaze(List<MeshInstance> meshes, List<PointLight> lights, float dt)
		{
			bool ablaze = Ablaze;
			if (ablaze && !_burning)
			{
				Emptying();
			}
			if (!ablaze)
			{
				_burning = false;
				_blazeAge = 0f;
				return;
			}
			_burning = true;
			_blazeAge += dt;
			if (Room == null)
			{
				return;
			}
			if (FlameAt.Count == 0 || Flames.Count == 0)
			{
				PlaceFlames();
			}
			MeshModel meshModel = Models.Get("fx_fireball");
			float num = Mathf.Clamp01(_blazeAge / 6f);
			float unscaledTime = Time.unscaledTime;
			for (int i = 0; i < FlameAt.Count; i++)
			{
				Vec3 vec = FlameAt[i];
				float num2 = 0.75f + 0.25f * Mathf.Sin(unscaledTime * (7f + (float)i) + (float)i * 1.7f) * Mathf.Sin(unscaledTime * 3.1f + (float)i);
				if (meshModel != null && i < Flames.Count)
				{
					if (_flameSlots == null)
					{
						_flameSlots = BlastFx.CloudSlots.Of(meshModel);
					}
					_flameSlots.Fire(0.85f, 0f, 0f);
					MeshInstance meshInstance = Flames[i];
					float num3 = (0.1f + 0.16f * num) * num2;
					meshInstance.Model = meshModel;
					meshInstance.Palette = _flameSlots.Palette;
					meshInstance.Emissive = true;
					meshInstance.NoShadow = true;
					meshInstance.Position = new Vec3(vec.X, vec.Y, vec.Z + num3 * 0.8f);
					meshInstance.Basis = BlastFx.AlongAxis(new Vec3(0f, 0f, 1f), unscaledTime * 1.3f + (float)i) * Mat3Scale(num3, num3, num3 * 1.9f);
					meshInstance.SortDepth = new Vector2(vec.X - Eye.x, vec.Y - Eye.y).magnitude;
					meshes.Add(meshInstance);
				}
				if (lights != null && i % 2 == 0)
				{
					lights.Add(new PointLight
					{
						X = vec.X,
						Y = vec.Y,
						Z = vec.Z + 0.4f,
						Radius = 2.6f + 1.4f * num,
						R = 1.2f * num2,
						G = 0.55f * num2,
						B = 0.18f * num2,
						Glow = 0.08f
					});
				}
			}
		}

		private static Mat3 Mat3Scale(float x, float y, float z)
		{
			return new Mat3(new Vec3(x, 0f, 0f), new Vec3(0f, y, 0f), new Vec3(0f, 0f, z));
		}

		private static void PlaceFlames()
		{
			FlameAt.Clear();
			Flames.Clear();
			Interior room = Room;
			float num = room.MinX + 0.15f;
			float num2 = room.MaxX - 0.15f;
			float num3 = room.MinY + 0.15f;
			float num4 = room.MaxY - 0.15f;
			for (int i = 0; i < 9; i++)
			{
				float t = (float)FireRng.NextDouble();
				Vec3 item;
				switch (i % 4)
				{
				case 0:
					item = new Vec3(Mathf.Lerp(num, num2, t), num3, 0f);
					break;
				case 1:
					item = new Vec3(Mathf.Lerp(num, num2, t), num4, 0f);
					break;
				case 2:
					item = new Vec3(num, Mathf.Lerp(num3, num4, t), 0f);
					break;
				default:
					item = new Vec3(num2, Mathf.Lerp(num3, num4, t), 0f);
					break;
				}
				if (room.Fit == null || room.Fit.Inside(0, item.X, item.Y))
				{
					if (i >= 6)
					{
						item = new Vec3(item.X, item.Y, Mathf.Max(0.8f, room.Top * 0.6f));
					}
					FlameAt.Add(item);
					Flames.Add(new MeshInstance());
				}
			}
		}

		private static void Emptying()
		{
			FlameAt.Clear();
			Flames.Clear();
			WorldBoxMod.Instance?.Toast("The house is on fire!");
			if (Walkers == null)
			{
				return;
			}
			List<Actor> list = new List<Actor>();
			foreach (RoomWalkers.Walker item in Walkers.All)
			{
				if (item.Who != null)
				{
					list.Add(item.Who);
				}
			}
			foreach (Actor item2 in list)
			{
				bool flag;
				try
				{
					flag = item2.isAlive();
				}
				catch
				{
					flag = false;
				}
				if (flag)
				{
					if (TargetOf(item2) == You && Building != null)
					{
						LetOut(item2, running: false);
					}
					else
					{
						Flee(item2);
					}
				}
			}
		}

		public static void Watch(Actor host, List<BaseSimObject> near)
		{
			if (host == null)
			{
				return;
			}
			float unscaledTime = Time.unscaledTime;
			if (near != null)
			{
				for (int i = 0; i < near.Count; i++)
				{
					if (!(near[i] is Actor actor) || actor == host)
					{
						continue;
					}
					try
					{
						if (actor.isAlive() && actor.has_attack_target && actor.attack_target == host)
						{
							Pursue(actor);
						}
					}
					catch
					{
					}
				}
			}
			if ((_watchIn -= Time.unscaledDeltaTime) <= 0f)
			{
				_watchIn = 0.25f;
				WatchNow(host);
			}
			if (Pursuers.Count == 0)
			{
				return;
			}
			_gonePursuers.Clear();
			foreach (KeyValuePair<Actor, Pursuer> pursuer in Pursuers)
			{
				if (unscaledTime - pursuer.Value.Seen > 12f)
				{
					_gonePursuers.Add(pursuer.Key);
				}
			}
			foreach (Actor gonePursuer in _gonePursuers)
			{
				Pursuers.Remove(gonePursuer);
			}
		}

		private static void WatchNow(Actor host)
		{
			List<Actor> list = null;
			Vector2 vector = default(Vector2);
			try
			{
				vector = host.current_position;
				list = World.world.units.getSimpleList();
			}
			catch (Exception e)
			{
				Report("watching", e);
			}
			if (list == null)
			{
				return;
			}
			for (int i = 0; i < list.Count; i++)
			{
				Actor actor = list[i];
				try
				{
					if (actor != null && actor != host && actor.isAlive() && actor.has_attack_target && actor.attack_target == host && (actor.current_position - vector).sqrMagnitude <= 256f)
					{
						Pursue(actor);
					}
				}
				catch
				{
				}
			}
		}

		internal static bool PursuingForTest(Actor a)
		{
			if (a != null)
			{
				return Pursuers.ContainsKey(a);
			}
			return false;
		}

		internal static void Pursue(Actor a)
		{
			if (a != null)
			{
				long num;
				try
				{
					num = ((a.data != null) ? a.data.id : (-1));
				}
				catch
				{
					return;
				}
				if (Pursuers.TryGetValue(a, out var value) && value.Id != num)
				{
					Pursuers.Remove(a);
					value = null;
				}
				if (value == null)
				{
					Dictionary<Actor, Pursuer> pursuers = Pursuers;
					Pursuer obj2 = new Pursuer
					{
						Id = num
					};
					value = obj2;
					pursuers[a] = obj2;
					Log.Info("room fight: " + WorldBoxMod.Describe(a) + " is after you");
				}
				value.Seen = Time.unscaledTime;
				if (Pursuers.Count > 64)
				{
					Pursuers.Remove(a);
				}
			}
		}

		private static void FollowIn()
		{
			if ((_pursueCheck -= Time.unscaledDeltaTime) > 0f)
			{
				return;
			}
			_pursueCheck = 0.25f;
			if (Building == null || Walkers == null || Room == null || Pursuers.Count == 0)
			{
				return;
			}
			float unscaledTime = Time.unscaledTime;
			Vector2 vector = Walkers.Door + Offset;
			_gonePursuers.Clear();
			foreach (KeyValuePair<Actor, Pursuer> pursuer in Pursuers)
			{
				Actor key = pursuer.Key;
				Pursuer value = pursuer.Value;
				bool flag;
				try
				{
					flag = key != null && key.isAlive() && key.data != null && key.data.id == value.Id;
				}
				catch
				{
					flag = false;
				}
				if (!flag || unscaledTime - value.Seen > 12f)
				{
					_gonePursuers.Add(key);
					continue;
				}
				if (In(key))
				{
					_gonePursuers.Add(key);
					continue;
				}
				bool flag2;
				try
				{
					flag2 = key.isInsideSomething() || key.asset == null || key.asset.skip_fight_logic || ((BaseSimObject)key).isFlying();
				}
				catch
				{
					flag2 = true;
				}
				if (flag2)
				{
					continue;
				}
				float magnitude;
				try
				{
					magnitude = (key.current_position - vector).magnitude;
				}
				catch
				{
					continue;
				}
				if (magnitude > 16f)
				{
					value.Due = -1f;
					continue;
				}
				if (value.Due < 0f)
				{
					value.Due = unscaledTime + magnitude / 1.6f + 0.4f;
					Log.Info($"room fight: {WorldBoxMod.Describe(key)} comes on to the door, {magnitude:F1} tiles off");
				}
				if (!(unscaledTime < value.Due))
				{
					_gonePursuers.Add(key);
					ComeIn(key);
				}
			}
			foreach (Actor gonePursuer in _gonePursuers)
			{
				Pursuers.Remove(gonePursuer);
			}
		}

		private static void ComeIn(Actor a)
		{
			try
			{
				a.cancelAllBeh();
				((BaseSimObject)a).clearIgnoreTargets();
				a.stayInBuilding(Building);
				Walkers.Arrive(a, Walkers.Door);
				a.current_position = Walkers.Door + Offset;
				Engage(a, You);
				CameIn++;
				Log.Info("room fight: " + WorldBoxMod.Describe(a) + " comes in after you");
			}
			catch (Exception e)
			{
				Report("in after you", e);
			}
		}

		internal static bool InRoomSpace(Vec3 p)
		{
			Interior room = Room;
			if (room == null)
			{
				return false;
			}
			if (p.Z < -0.2f)
			{
				return false;
			}
			if (room.Fit != null)
			{
				int storey = room.Fit.StoreyAt(Mathf.Max(0f, p.Z));
				if (!room.Fit.Inside(storey, p.X, p.Y))
				{
					return false;
				}
				return p.Z < room.Fit.CeilingAt(storey, p.X, p.Y);
			}
			if (p.X > room.MinX && p.X < room.MaxX && p.Y > room.MinY && p.Y < room.MaxY)
			{
				return p.Z < room.Top;
			}
			return false;
		}

		internal static bool WallHit(Vec3 a, Vec3 b, out Vec3 at)
		{
			at = b;
			if (Room == null)
			{
				return false;
			}
			Vec3 vec = b - a;
			int num = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(Vec3.Dot(vec, vec)) / 0.04f), 1, 400);
			Vec3 vec2 = a;
			for (int i = 1; i <= num; i++)
			{
				Vec3 vec3 = a + vec * ((float)i / (float)num);
				if (!InRoomSpace(vec3))
				{
					at = vec2;
					return true;
				}
				vec2 = vec3;
			}
			return false;
		}

		public static void DrawShots(List<MeshInstance> meshes, Vector2 eye)
		{
			_goneShots.Clear();
			foreach (KeyValuePair<Projectile, MeshInstance> shotMesh in ShotMeshes)
			{
				if (shotMesh.Key == null || shotMesh.Key._state != ProjectileState.Active)
				{
					_goneShots.Add(shotMesh.Key);
				}
			}
			foreach (Projectile goneShot in _goneShots)
			{
				ShotMeshes.Remove(goneShot);
			}
			if (!Active)
			{
				return;
			}
			List<Projectile> list;
			try
			{
				list = World.world.projectiles.list;
			}
			catch
			{
				return;
			}
			if (list == null)
			{
				return;
			}
			for (int i = 0; i < list.Count; i++)
			{
				Projectile projectile = list[i];
				try
				{
					Vec3 at;
					if (projectile != null && projectile.asset != null && projectile._state == ProjectileState.Active)
					{
						if (FirstPersonShots.RoomShot(projectile, out at))
						{
							goto IL_014b;
						}
						if (In(projectile.by_who))
						{
							Vector3 current_position_3d = projectile._current_position_3d;
							Vector2 vector = ToRoom(new Vector2(current_position_3d.x, current_position_3d.y));
							at = new Vec3(vector.x, vector.y, current_position_3d.z);
							goto IL_014b;
						}
					}
					goto end_IL_00d3;
					IL_014b:
					if (InRoomSpace(at))
					{
						if (!ShotMeshes.TryGetValue(projectile, out var value))
						{
							Dictionary<Projectile, MeshInstance> shotMeshes = ShotMeshes;
							MeshInstance obj2 = new MeshInstance
							{
								NoShadow = true
							};
							value = obj2;
							shotMeshes[projectile] = obj2;
						}
						Vector3 velocity = projectile._velocity;
						Vec3 vec = new Vec3(velocity.x, velocity.y, velocity.z);
						vec = ((Vec3.Dot(vec, vec) > 1E-06f) ? vec.Normalized() : new Vec3(1f, 0f, 0f));
						if (ShotModel(projectile, value, at, vec))
						{
							value.SortDepth = new Vector2(at.X - eye.x, at.Y - eye.y).magnitude;
							meshes.Add(value);
						}
					}
					end_IL_00d3:;
				}
				catch
				{
				}
			}
		}

		private static bool ShotModel(Projectile p, MeshInstance m, Vec3 tip, Vec3 dir)
		{
			string id = p.asset.id;
			if (id == "arrow")
			{
				MeshModel meshModel = Models.Get("arrow");
				if (meshModel == null)
				{
					return false;
				}
				m.Model = meshModel;
				m.Palette = null;
				m.Emissive = false;
				Archer.PlaceArrow(m, tip - dir * 0.5f, dir, 0.5f);
				return true;
			}
			float size;
			bool tumbles;
			MeshModel meshModel2 = SceneCollector.ThrownModel(id, out size, out tumbles);
			if (meshModel2 != null)
			{
				float num = Mathf.Max(0.01f, Mathf.Max(meshModel2.Max.Z - meshModel2.Min.Z, Mathf.Max(meshModel2.Max.X - meshModel2.Min.X, meshModel2.Max.Y - meshModel2.Min.Y)));
				float num2 = size / num;
				m.Model = meshModel2;
				m.Palette = null;
				m.Emissive = false;
				m.Basis = Mat3.RotZ(Mathf.Atan2(dir.Y, dir.X) + Time.unscaledTime * 6f) * num2;
				m.Position = tip - m.Basis.Mul((meshModel2.Min + meshModel2.Max) * 0.5f);
				return true;
			}
			MeshModel meshModel3 = Models.Get("fx_fireball");
			if (meshModel3 == null)
			{
				return false;
			}
			if (_boltSlots == null)
			{
				_boltSlots = BlastFx.CloudSlots.Of(meshModel3);
			}
			_boltSlots.Fire(0.9f, 0f, 0f);
			m.Model = meshModel3;
			m.Palette = _boltSlots.Palette;
			m.Emissive = true;
			m.Basis = BlastFx.AlongAxis(dir, Time.unscaledTime * 4f) * 0.09f;
			m.Position = tip;
			return true;
		}
	}
}
