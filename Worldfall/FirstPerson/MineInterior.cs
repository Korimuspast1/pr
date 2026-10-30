using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class MineInterior : ModelRoom
	{
		private static readonly Dictionary<Building, Vector4> Drawn = new Dictionary<Building, Vector4>();

		private Vector3 _mouthWorld;

		private Vector2 _mouthRoom;

		private Actor _you;

		public Building Mine { get; private set; }

		public override Actor Hidden => _you;

		public override bool SeesOutside => false;

		protected override float WalkSpeed => 1.6f;

		public override float Daylight => 0.42f;

		public override Vector2 Where => new Vector2(_mouthWorld.x, _mouthWorld.y);

		internal override object CombatKey => Mine;

		internal override Building CombatBuilding => Mine;

		internal override Vector2 CombatOffset => new Vector2(_mouthWorld.x - _mouthRoom.x, _mouthWorld.y - _mouthRoom.y);

		public static void Register(Building b, float x, float y, float z, float scale)
		{
			if (b != null)
			{
				if (Drawn.Count > 64)
				{
					Drawn.Clear();
				}
				Drawn[b] = new Vector4(x, y, z, scale);
			}
		}

		public static float MouthDistance(Building b, Vector2 p)
		{
			if (!Mouth(b, out var mouth))
			{
				return float.MaxValue;
			}
			return (mouth - p).magnitude;
		}

		public static bool Mouth(Building b, out Vector2 mouth)
		{
			mouth = Vector2.zero;
			if (b == null || !Drawn.TryGetValue(b, out var value))
			{
				return false;
			}
			float[] array = CabinBuilder.Floats(Models.Get("mine")?.Info("mouth"));
			mouth = ((array != null && array.Length >= 2) ? new Vector2(value.x + array[0] * value.w, value.y + array[1] * value.w) : new Vector2(value.x, value.y - 1.3f * value.w));
			return true;
		}

		public static bool IsEnterable(Building b)
		{
			try
			{
				return b != null && b.isAlive() && b.asset != null && b.asset.type == "type_mine" && !b.isUnderConstruction() && !b.isRuin() && Drawn.ContainsKey(b) && Models.Get("int_mine") != null;
			}
			catch
			{
				return false;
			}
		}

		public bool Enter(Building mine, Actor host)
		{
			MeshModel meshModel = CabinBuilder.PlainOpenings(Models.Get("int_mine"));
			MeshModel meshModel2 = Models.Get("mine");
			if (meshModel == null || !Drawn.TryGetValue(mine, out var value))
			{
				return false;
			}
			Interior interior = CabinBuilder.Build(meshModel, ModelRoom_Cloth(mine));
			Mine = mine;
			_you = host;
			Name = "mine";
			float[] array = CabinBuilder.Floats(meshModel2?.Info("mouth"));
			float w = value.w;
			_mouthWorld = ((array != null && array.Length >= 3) ? new Vector3(value.x + array[0] * w, value.y + array[1] * w, value.z + array[2] * w) : new Vector3(value.x, value.y - 1.2f * w, value.z));
			float[] array2 = CabinBuilder.Floats(meshModel.Info("mouth"));
			Vec3 position = interior.Shell.Position;
			_mouthRoom = ((array2 != null && array2.Length >= 2) ? new Vector2(position.X + array2[0] * 0.5714286f, position.Y + array2[1] * 0.5714286f) : new Vector2(interior.DoorX, interior.MinY));
			List<Actor> list = new List<Actor>();
			try
			{
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple != host && simple.isAlive() && simple.is_inside_building && simple.inside_building == mine)
					{
						list.Add(simple);
					}
				}
			}
			catch
			{
			}
			Begin(interior, list, (int)(mine.getID() & 0x7FFFFFFF));
			HouseInterior.HoldIndoors(host, mine);
			Log.Info($"into the mine: {meshModel.Name}, {interior.Lights.Count} lanterns, {Occupants.Count} miners at work, mouth at {_mouthWorld}");
			return true;
		}

		private static uint ModelRoom_Cloth(Building b)
		{
			try
			{
				return ModelRoom.KingdomCloth(b.kingdom);
			}
			catch
			{
				return 0u;
			}
		}

		public override void Exit()
		{
			HouseInterior.LetOut(_you, Mine);
			base.Exit();
			Mine = null;
			_you = null;
		}

		public void KeepIndoors()
		{
			if (base.Inside)
			{
				HouseInterior.HoldIndoors(_you, Mine);
			}
		}

		public bool Standing()
		{
			try
			{
				return Mine != null && Mine.isAlive() && !Mine.isRuin();
			}
			catch
			{
				return false;
			}
		}

		protected override bool Present(Actor a)
		{
			if (a.is_inside_building)
			{
				return a.inside_building == Mine;
			}
			return false;
		}

		public override void Tick(float dt)
		{
		}

		public override ViewState OutsideView(ViewState inside)
		{
			return new ViewState
			{
				X = _mouthWorld.x + (inside.X - _mouthRoom.x),
				Y = _mouthWorld.y + (inside.Y - _mouthRoom.y),
				Z = _mouthWorld.z + inside.Z,
				Yaw = inside.Yaw,
				Pitch = inside.Pitch,
				Fov = inside.Fov,
				Shear = inside.Shear,
				Free = inside.Free
			};
		}

		public override Vec3 LightInside(Vec3 world)
		{
			return new Vec3(world.X, world.Y, Mathf.Max(0.2f, world.Z)).Normalized();
		}
	}
}
