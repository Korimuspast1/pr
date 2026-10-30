using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal abstract class ModelRoom
	{
		protected const float BodyRadius = 0.2f;

		public string Name;

		public Vector2 Eye;

		public readonly List<BaseSimObject> Tagged = new List<BaseSimObject>();

		protected readonly List<Actor> Occupants = new List<Actor>();

		protected readonly RoomWalkers Walkers = new RoomWalkers();

		public bool Inside { get; protected set; }

		public Interior Room { get; protected set; }

		protected virtual float WalkSpeed => 1.5f;

		public virtual Actor Hidden => null;

		public virtual bool SeesOutside => true;

		public virtual float Daylight => 1f;

		public abstract Vector2 Where { get; }

		internal abstract object CombatKey { get; }

		internal virtual Building CombatBuilding => null;

		internal virtual Vector2 CombatOffset => Where - new Vector2((Room.MinX + Room.MaxX) * 0.5f, (Room.MinY + Room.MaxY) * 0.5f);

		internal RoomWalkers CombatWalkers => Walkers;

		internal IReadOnlyList<RoomWalkers.Walker> WalkersForTest => Walkers.All;

		public bool NearDoor
		{
			get
			{
				if (Inside)
				{
					return (Eye - new Vector2(Room.DoorX, Room.DoorY)).magnitude < 0.75f;
				}
				return false;
			}
		}

		public abstract ViewState OutsideView(ViewState inside);

		public abstract Vec3 LightInside(Vec3 world);

		public abstract void Tick(float dt);

		protected abstract bool Present(Actor a);

		protected void Begin(Interior room, List<Actor> occupants, int seed)
		{
			Room = room;
			Eye = new Vector2(room.SpawnX, room.SpawnY);
			Occupants.Clear();
			foreach (Actor occupant in occupants)
			{
				if (occupant != null && Occupants.Count < room.Spots.Count)
				{
					Occupants.Add(occupant);
				}
			}
			Walkers.Start(room, Occupants, seed);
			Inside = true;
		}

		public virtual void Exit()
		{
			if (Inside)
			{
				Log.Info("left the " + Name);
			}
			Inside = false;
			Room = null;
			Occupants.Clear();
			Walkers.Clear();
			Tagged.Clear();
		}

		public void Move(float forward, float strafe, float yaw, float dt)
		{
			if (forward != 0f || strafe != 0f)
			{
				float num = Mathf.Cos(yaw);
				float num2 = Mathf.Sin(yaw);
				Vector2 vector = new Vector2(num * forward + num2 * strafe, num2 * forward - num * strafe).normalized * (WalkSpeed * dt);
				Vector2 vector2 = Eye + new Vector2(vector.x, 0f);
				if (Free(vector2))
				{
					Eye = vector2;
				}
				vector2 = Eye + new Vector2(0f, vector.y);
				if (Free(vector2))
				{
					Eye = vector2;
				}
			}
		}

		internal bool TryPut(Vector2 p)
		{
			if (Room == null || !Free(p))
			{
				return false;
			}
			Eye = p;
			return true;
		}

		private bool Free(Vector2 p)
		{
			if (p.x < Room.MinX || p.x > Room.MaxX || p.y < Room.MinY || p.y > Room.MaxY)
			{
				return false;
			}
			foreach (float[] solid in Room.Solids)
			{
				if (InBox(p, solid) && !InBox(Eye, solid))
				{
					return false;
				}
			}
			if (Walkers.Occupied(p, 0.1f) && !Walkers.Occupied(Eye, 0.1f))
			{
				return false;
			}
			return true;
		}

		private static bool InBox(Vector2 p, float[] s)
		{
			if (p.x > s[0] - 0.2f && p.x < s[2] + 0.2f && p.y > s[1] - 0.2f)
			{
				return p.y < s[3] + 0.2f;
			}
			return false;
		}

		public float DistanceTo(Actor a)
		{
			RoomWalkers.Walker walker = Walkers.Of(a);
			if (walker != null)
			{
				return (Eye - walker.Pos).magnitude;
			}
			return float.MaxValue;
		}

		public Vector3 HeadOf(Actor a, SpriteCache sprites)
		{
			RoomWalkers.Walker walker = Walkers.Of(a);
			if (walker == null)
			{
				return new Vector3(Eye.x, Eye.y, 1f);
			}
			float num = 1f;
			try
			{
				SpriteImage spriteImage = sprites.Get(a.getSpriteToRender());
				if (spriteImage != null)
				{
					num = Stature.Peek(a, spriteImage.UnitHeight * spriteImage.VisibleFraction * Mathf.Abs(a.current_scale.y));
				}
			}
			catch
			{
			}
			return new Vector3(walker.Pos.x, walker.Pos.y, num + 0.15f);
		}

		public void Collect(List<VoxelObject> voxels, List<MeshInstance> meshes, SpriteCache sprites, Dictionary<Actor, Vector2> lookAt, float dt)
		{
			Tagged.Clear();
			if (!SeesOutside)
			{
				People.Tick();
				Creatures.Tick();
			}
			foreach (MeshInstance mesh in Room.Meshes)
			{
				mesh.SortDepth = new Vector2(mesh.Position.X - Eye.x, mesh.Position.Y - Eye.y).magnitude;
				meshes.Add(mesh);
			}
			Walkers.Tick(Eye, lookAt.Keys, dt);
			bool flag = RoomCombat.Active && RoomCombat.Walkers == Walkers;
			if (flag)
			{
				RoomCombat.Sync();
				RoomCombat.Drawing = true;
			}
			try
			{
				DrawWalkers(voxels, meshes, sprites, dt);
			}
			finally
			{
				RoomCombat.Drawing = false;
			}
			if (flag)
			{
				RoomCombat.DrawFallen(meshes, dt, Eye);
			}
		}

		private void DrawWalkers(List<VoxelObject> voxels, List<MeshInstance> meshes, SpriteCache sprites, float dt)
		{
			foreach (RoomWalkers.Walker item2 in Walkers.All)
			{
				Actor who = item2.Who;
				bool flag;
				try
				{
					flag = who != null && who.isAlive() && Present(who);
				}
				catch
				{
					flag = false;
				}
				if (!flag)
				{
					continue;
				}
				SpriteImage spriteImage;
				try
				{
					if (who.is_inside_boat)
					{
						who._is_in_liquid = false;
					}
					spriteImage = sprites.Get(who.getSpriteToRender());
				}
				catch
				{
					continue;
				}
				if (spriteImage == null)
				{
					continue;
				}
				float x = item2.Pos.x;
				float y = item2.Pos.y;
				Vector3 current_scale = who.current_scale;
				float num = spriteImage.UnitWidth * Mathf.Abs(current_scale.x);
				float num2 = spriteImage.UnitHeight * Mathf.Abs(current_scale.y);
				float body = Stature.Of(who, num2 * spriteImage.VisibleFraction, dt);
				float magnitude = new Vector2(x - Eye.x, y - Eye.y).magnitude;
				int count = Tagged.Count;
				if (Models.Ready && People.IsHumanoid(who))
				{
					VoxelObject template = new VoxelObject
					{
						Tag = count,
						NoShadow = true,
						SortDepth = magnitude
					};
					People.AddMesh(meshes, voxels, who, spriteImage, sprites, x, y, 0f, body, item2.Heading, item2.Speed, magnitude, dt, template, swimming: false);
					Tagged.Add(who);
					continue;
				}
				if (Models.Ready && Models.CreatureFamily(who) != null && Creatures.Add(meshes, who, spriteImage, x, y, 0f, num, num2, item2.Heading, item2.Speed, magnitude, dt, count, 0u, 0u, swimming: false))
				{
					Tagged.Add(who);
					continue;
				}
				VoxelObject item = VoxelObject.Standing(spriteImage.CreatureShape, x, y, (0f - spriteImage.BottomPadding) * num2, num, num2, spriteImage.PivotX, 0f - Mathf.Cos(item2.Heading), 0f - Mathf.Sin(item2.Heading));
				item.Tag = count;
				item.NoShadow = true;
				item.SortDepth = magnitude;
				Tagged.Add(who);
				voxels.Add(item);
				if (RoomCombat.Drawing)
				{
					Hitboxes.RecordSprite(who, x, y, 0f, num, num2 * spriteImage.VisibleFraction, item2.Heading);
				}
			}
		}

		protected static uint KingdomCloth(Kingdom k)
		{
			try
			{
				if (k == null)
				{
					return 0u;
				}
				Color color = Royal.ColorOf(k);
				return TerrainCache.Rgb((int)(color.r * 255f), (int)(color.g * 255f), (int)(color.b * 255f));
			}
			catch
			{
				return 0u;
			}
		}
	}
}
