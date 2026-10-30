using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal static class Drill
	{
		internal struct Stroke
		{
			public Vector2 Face;

			public WeaponKind Pose;

			public float Attack;

			public float Side;

			public bool Guard;
		}

		private sealed class Trainee
		{
			public Building Dummy;

			public int Slot = -1;

			public int Post;

			public readonly FirstPerson.Core.Drill.Timing Timing = new FirstPerson.Core.Drill.Timing();

			public WeaponKind Kind;

			public float Body = 1f;

			public int Seen = -1;

			public int Step = -1;

			public float Timer;

			public float Tilt;

			public float Idle;

			public bool Training;

			public bool Loose;

			public double Clock = -1.0;

			public Vector2 Spot;
		}

		private sealed class Dummy
		{
			public readonly FirstPerson.Core.Drill.Yard Yard = new FirstPerson.Core.Drill.Yard();

			public readonly Actor[] Slots = new Actor[6];

			public double Stepped = -1.0;

			public int Seen;
		}

		internal const string Task = "warrior_train_with_dummy";

		private const float Near = 2.6f;

		private const float LetGo = 2f;

		private static readonly Dictionary<Actor, Trainee> Trainees = new Dictionary<Actor, Trainee>();

		private static readonly Dictionary<Building, Dummy> Dummies = new Dictionary<Building, Dummy>();

		private static readonly List<Actor> StaleActors = new List<Actor>();

		private static readonly List<Building> StaleDummies = new List<Building>();

		private static int _swept;

		internal static bool IsDummy(BuildingAsset asset)
		{
			if (asset != null)
			{
				if (!(asset.type == "type_training_dummies"))
				{
					return asset.id == "training_dummy";
				}
				return true;
			}
			return false;
		}

		private static Building TrainingAt(Actor a, out int step, out float timer, out float tilt)
		{
			step = -1;
			timer = (tilt = 0f);
			try
			{
				if (a.ai == null || ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task == null || ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id != "warrior_train_with_dummy")
				{
					return null;
				}
				Building beh_building_target = a.beh_building_target;
				if (beh_building_target == null || !IsDummy(beh_building_target.asset))
				{
					return null;
				}
				step = ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).action_index;
				timer = a.timer_action;
				tilt = a.target_angle.z;
				return beh_building_target;
			}
			catch
			{
				return null;
			}
		}

		internal static Vector2 Spot(Actor a, Vector2 p)
		{
			int frameCount = Time.frameCount;
			if (frameCount - _swept > 300)
			{
				Sweep(frameCount);
			}
			int step;
			float timer;
			float tilt;
			Building building = TrainingAt(a, out step, out timer, out tilt);
			if (!Trainees.TryGetValue(a, out var value))
			{
				if (building == null)
				{
					return p;
				}
				value = new Trainee
				{
					Dummy = building
				};
				Trainees[a] = value;
			}
			if (building != null && building != value.Dummy)
			{
				Forget(a, value);
				value = new Trainee
				{
					Dummy = building
				};
				Trainees[a] = value;
			}
			if (value.Seen != frameCount)
			{
				float num = ((value.Seen >= 0) ? Mathf.Min(0.25f, Time.deltaTime) : 0f);
				value.Seen = frameCount;
				value.Training = building != null;
				value.Step = step;
				value.Timer = timer;
				value.Tilt = tilt;
				value.Idle = (value.Training ? 0f : (value.Idle + num));
			}
			Building dummy = value.Dummy;
			WorldTile worldTile = null;
			try
			{
				if (dummy != null && dummy.isAlive())
				{
					worldTile = dummy.current_tile;
				}
			}
			catch
			{
			}
			if (worldTile == null)
			{
				Forget(a, value);
				return p;
			}
			float magnitude = (p - new Vector2(worldTile.posV3.x, worldTile.posV3.y)).magnitude;
			if (!value.Training && (magnitude > 2.6f || value.Idle > 2f))
			{
				Forget(a, value);
				return p;
			}
			if (!Dummies.TryGetValue(dummy, out var value2))
			{
				Dictionary<Building, Dummy> dummies = Dummies;
				Dummy obj2 = new Dummy
				{
					Seen = frameCount
				};
				value2 = obj2;
				dummies[dummy] = obj2;
			}
			if (value.Slot < 0 && value.Training && magnitude < 4.6f)
			{
				value.Slot = Claim(value2, a);
			}
			if (value.Slot < 0 || !value2.Yard.Placed)
			{
				return p;
			}
			Vec3 vec = value2.Yard.Spot(value.Slot, FirstPerson.Core.Drill.Reach(value.Kind) * value.Body, out value.Post);
			value.Spot = new Vector2(vec.X, vec.Y);
			float t = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.3f, 2.6f, magnitude));
			return Vector2.Lerp(p, value.Spot, t);
		}

		private static int Claim(Dummy d, Actor a)
		{
			int num = -1;
			for (int i = 0; i < d.Slots.Length; i++)
			{
				Actor actor = d.Slots[i];
				if (actor == a)
				{
					return i;
				}
				Trainee value;
				bool flag = actor == null || !Trainees.TryGetValue(actor, out value) || value.Slot != i;
				if (!flag)
				{
					try
					{
						flag = !actor.isAlive();
					}
					catch
					{
						flag = true;
					}
				}
				if (flag && num < 0)
				{
					num = i;
				}
			}
			if (num >= 0)
			{
				d.Slots[num] = a;
				return num;
			}
			return (a.GetHashCode() & 0x7FFFFFFF) % d.Slots.Length;
		}

		private static void Forget(Actor a, Trainee t)
		{
			Trainees.Remove(a);
			if (t.Dummy != null && t.Slot >= 0 && Dummies.TryGetValue(t.Dummy, out var value) && t.Slot < value.Slots.Length && value.Slots[t.Slot] == a)
			{
				value.Slots[t.Slot] = null;
			}
		}

		private static void Sweep(int frame)
		{
			_swept = frame;
			StaleActors.Clear();
			foreach (KeyValuePair<Actor, Trainee> trainee in Trainees)
			{
				if (frame - trainee.Value.Seen > 300)
				{
					StaleActors.Add(trainee.Key);
				}
			}
			foreach (Actor staleActor in StaleActors)
			{
				Forget(staleActor, Trainees[staleActor]);
			}
			StaleDummies.Clear();
			foreach (KeyValuePair<Building, Dummy> dummy in Dummies)
			{
				if (frame - dummy.Value.Seen > 900)
				{
					StaleDummies.Add(dummy.Key);
				}
			}
			foreach (Building staleDummy in StaleDummies)
			{
				Dummies.Remove(staleDummy);
			}
		}

		internal static bool At(Actor a, Vector2 at, float speed, WeaponKind kind, float body, ref Stroke s)
		{
			if (!Trainees.TryGetValue(a, out var value) || value.Slot < 0 || !Dummies.TryGetValue(value.Dummy, out var value2) || !value2.Yard.Placed)
			{
				return false;
			}
			value.Kind = kind;
			value.Body = Mathf.Clamp(body, 0.3f, 3f);
			WeaponKind weaponKind = FirstPerson.Core.Drill.PoseOf(kind);
			double actionNow = WorldPace.ActionNow;
			float dt = ((value.Clock >= 0.0) ? Mathf.Clamp((float)(actionNow - value.Clock), 0f, 0.25f) : 0f);
			value.Clock = actionNow;
			bool flag = value.Timing.Step(actionNow, dt, value.Training ? value.Step : (-1), value.Timer, value.Training ? value.Tilt : 0f, weaponKind);
			if ((at - value.Spot).sqrMagnitude > 0.20249999f || speed > 0.35f)
			{
				return false;
			}
			Vec3 vec = value2.Yard.Straw(value.Post);
			if (flag)
			{
				if (weaponKind == WeaponKind.Bow)
				{
					value.Loose = true;
				}
				else
				{
					value2.Yard.Hit(value.Post, new Vec3(at.x, at.y, vec.Z), FirstPerson.Core.Drill.Knock(weaponKind));
				}
			}
			s.Face = new Vector2(vec.X, vec.Y);
			s.Pose = weaponKind;
			s.Attack = value.Timing.Now.Phase(weaponKind, actionNow);
			s.Side = ((value.Timing.Now.Side == 0f) ? 1f : value.Timing.Now.Side);
			s.Guard = value.Training && FirstPerson.Core.Drill.GuardStep(value.Step);
			return true;
		}

		internal static void Bowed(Actor a, Archer.Bow bow)
		{
			if (Trainees.TryGetValue(a, out var value) && value.Loose)
			{
				value.Loose = false;
				if (Dummies.TryGetValue(value.Dummy, out var value2))
				{
					FirstPerson.Core.Drill.Loosed(bow, out var nock, out var length);
					value2.Yard.Loose(value.Post, nock, length, value.Clock, (uint)(value.Timing.Landed * 2654435761u + (uint)a.GetHashCode()));
				}
			}
		}

		internal static FirstPerson.Core.Drill.Yard Drawn(Building b, MeshInstance inst, out double now)
		{
			now = 0.0;
			if (b == null || !IsDummy(b.asset))
			{
				return null;
			}
			if (!Dummies.TryGetValue(b, out var value))
			{
				value = (Dummies[b] = new Dummy());
			}
			value.Seen = Time.frameCount;
			now = WorldPace.ActionNow;
			float num = ((value.Stepped >= 0.0) ? ((float)(now - value.Stepped)) : 0f);
			value.Stepped = now;
			value.Yard.Place(inst);
			value.Yard.Step(Mathf.Clamp(num, 0f, 0.25f), now, 8f);
			if (num > 1f)
			{
				for (int i = 0; i < value.Yard.Sways.Length; i++)
				{
					value.Yard.Sways[i] = default(FirstPerson.Core.Drill.Sway);
				}
			}
			return value.Yard;
		}

		internal static bool TakesSlash(Vector3 at)
		{
			int frameCount = Time.frameCount;
			foreach (KeyValuePair<Building, Dummy> dummy in Dummies)
			{
				Dummy value = dummy.Value;
				if (!value.Yard.Placed || frameCount - value.Seen > 60)
				{
					continue;
				}
				float num = at.x - value.Yard.Origin.X;
				float num2 = at.y - value.Yard.Origin.Y;
				if (num * num + num2 * num2 > 0.64000005f)
				{
					continue;
				}
				for (int i = 0; i < value.Slots.Length; i++)
				{
					if (value.Slots[i] != null)
					{
						return true;
					}
				}
			}
			return false;
		}

		internal static void CountForTest(out int trainees, out int atPosts, out int landed, out int late, out int hits, out int arrows)
		{
			trainees = (atPosts = (landed = (late = (hits = (arrows = 0)))));
			foreach (KeyValuePair<Actor, Trainee> trainee in Trainees)
			{
				trainees++;
				if (trainee.Value.Slot >= 0)
				{
					atPosts++;
				}
				landed += trainee.Value.Timing.Landed;
				late += trainee.Value.Timing.Late;
			}
			foreach (KeyValuePair<Building, Dummy> dummy in Dummies)
			{
				hits += dummy.Value.Yard.Hits;
				foreach (FirstPerson.Core.Drill.Shaft shaft in dummy.Value.Yard.Shafts)
				{
					if (shaft.Stuck)
					{
						arrows++;
					}
				}
			}
		}

		internal static bool TraineeForTest(Actor a, out int slot, out int post, out Vector2 spot, out float lastError, out float body)
		{
			slot = (post = -1);
			spot = default(Vector2);
			lastError = (body = 0f);
			if (!Trainees.TryGetValue(a, out var value))
			{
				return false;
			}
			body = value.Body;
			slot = value.Slot;
			post = value.Post;
			spot = value.Spot;
			lastError = value.Timing.LastError;
			return true;
		}

		internal static bool YardForTest(Building b, out float scale, out float lean0, out float lean1, out float lean2)
		{
			scale = (lean0 = (lean1 = (lean2 = 0f)));
			if (b == null || !Dummies.TryGetValue(b, out var value) || !value.Yard.Placed)
			{
				return false;
			}
			scale = value.Yard.Scale;
			lean0 = Lean(value.Yard.Sways[0]);
			lean1 = Lean(value.Yard.Sways[1]);
			lean2 = Lean(value.Yard.Sways[2]);
			return true;
		}

		private static float Lean(FirstPerson.Core.Drill.Sway s)
		{
			return Mathf.Sqrt(s.X * s.X + s.Y * s.Y);
		}
	}
}
