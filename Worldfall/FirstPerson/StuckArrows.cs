using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class StuckArrows
	{
		private sealed class Fixed
		{
			public Vec3 Nock;

			public Vec3 Dir;

			public float Born;

			public readonly MeshInstance Mesh = new MeshInstance
			{
				NoShadow = true,
				Tag = -1
			};

			public List<Holder> Held;
		}

		private struct Holder
		{
			public Building Building;

			public long Id;

			public BuildingAsset Asset;

			public WorldTile Wall;

			public bool Stands
			{
				get
				{
					try
					{
						if (Wall != null)
						{
							return Wall.Type != null && Wall.Type.wall;
						}
						return Building != null && Building.isAlive() && Building.data != null && Building.data.id == Id && Building.asset == Asset;
					}
					catch
					{
						return false;
					}
				}
			}
		}

		private sealed class Lodged
		{
			public string Part;

			public Vec3 Point;

			public Vec3 Along;

			public float Born;

			public readonly MeshInstance Mesh = new MeshInstance
			{
				NoShadow = true
			};
		}

		private struct Seen
		{
			public MeshInstance Mesh;

			public Actor Actor;

			public bool Person;

			public float X;

			public float Y;

			public float Lift;
		}

		public enum Struck : byte
		{
			Wood,
			Body,
			Ground
		}

		public struct Impact
		{
			public Vec3 At;

			public Struck What;
		}

		private const float FixedLife = 35f;

		private const float BodyLife = 40f;

		private const float FadeSeconds = 2f;

		private const int MaxFixed = 240;

		private const int MaxPerBody = 6;

		public const float Length = 0.44f;

		private static readonly List<Fixed> World = new List<Fixed>();

		private static readonly Dictionary<MeshInstance, List<Lodged>> InBodies = new Dictionary<MeshInstance, List<Lodged>>();

		private static readonly Dictionary<MeshInstance, int> LastDrawn = new Dictionary<MeshInstance, int>();

		private static readonly Dictionary<MeshInstance, MeshInstance> Fallen = new Dictionary<MeshInstance, MeshInstance>();

		private static List<Seen> _seenNow = new List<Seen>();

		private static List<Seen> _seenLast = new List<Seen>();

		private static int _seenFrame = -1;

		private static readonly System.Random Rng = new System.Random(7);

		private static readonly List<MeshInstance> Stale = new List<MeshInstance>();

		private static readonly List<Impact> Impacts = new List<Impact>();

		public static int FixedCount => World.Count;

		public static int LodgedCount
		{
			get
			{
				int num = 0;
				foreach (KeyValuePair<MeshInstance, List<Lodged>> inBody in InBodies)
				{
					num += inBody.Value.Count;
				}
				return num;
			}
		}

		private static void Heard(Vec3 at, Struck what)
		{
			if (Impacts.Count >= 32)
			{
				Impacts.RemoveAt(0);
			}
			Impacts.Add(new Impact
			{
				At = at,
				What = what
			});
		}

		public static void TakeImpacts(List<Impact> into)
		{
			into.Clear();
			into.AddRange(Impacts);
			Impacts.Clear();
		}

		internal static List<Vec3> NewestPoints(int n)
		{
			List<Vec3> list = new List<Vec3>();
			int num = World.Count - 1;
			while (num >= 0 && list.Count < n)
			{
				list.Add(World[num].Nock + World[num].Dir * 0.44f);
				num--;
			}
			return list;
		}

		public static void Clear()
		{
			World.Clear();
			InBodies.Clear();
			LastDrawn.Clear();
			Fallen.Clear();
			_seenNow.Clear();
			_seenLast.Clear();
		}

		public static void Wear(List<MeshInstance> meshes, Actor a, MeshInstance body, bool person, int tag, uint flash, float sortDepth)
		{
			int frameCount = Time.frameCount;
			if (frameCount != _seenFrame)
			{
				List<Seen> seenLast = _seenLast;
				_seenLast = _seenNow;
				_seenNow = seenLast;
				_seenNow.Clear();
				_seenFrame = frameCount;
			}
			Seen item = new Seen
			{
				Mesh = body,
				Actor = a,
				Person = person
			};
			try
			{
				Vector2 current_position = a.current_position;
				item.X = current_position.x;
				item.Y = current_position.y;
				item.Lift = a.position_height;
			}
			catch
			{
				return;
			}
			_seenNow.Add(item);
			Draw(meshes, body, tag, flash, sortDepth);
		}

		public static void Unsee(MeshInstance body)
		{
			_seenNow.RemoveAll((Seen s) => s.Mesh == body);
		}

		public static void Follow(MeshInstance body, MeshInstance corpse)
		{
			if (body != null && corpse != null)
			{
				Fallen[body] = corpse;
				if (InBodies.TryGetValue(body, out var value))
				{
					InBodies.Remove(body);
					InBodies[corpse] = value;
				}
			}
		}

		public static void Draw(List<MeshInstance> meshes, MeshInstance body, int tag, uint flash, float sortDepth)
		{
			LastDrawn[body] = Time.frameCount;
			if (!InBodies.TryGetValue(body, out var value) || value.Count == 0)
			{
				return;
			}
			MeshModel meshModel = Models.Get("arrow");
			if (meshModel == null || body.Model == null)
			{
				return;
			}
			float time = Time.time;
			for (int num = value.Count - 1; num >= 0; num--)
			{
				Lodged lodged = value[num];
				float num2 = time - lodged.Born;
				int num3 = body.Model.PartIndex(lodged.Part);
				if (num2 > 40f || num3 < 0)
				{
					value.RemoveAt(num);
				}
				else
				{
					lodged.Mesh.Model = meshModel;
					lodged.Mesh.Palette = null;
					Archer.PlaceLodged(lodged.Mesh, body, num3, lodged.Point, lodged.Along, 0.44f);
					lodged.Mesh.Tag = tag;
					lodged.Mesh.Flash = flash;
					lodged.Mesh.Multiply = body.Multiply;
					lodged.Mesh.SortDepth = sortDepth;
					lodged.Mesh.Static = false;
					lodged.Mesh.Fade = Mathf.Clamp01((40f - num2) / 2f);
					meshes.Add(lodged.Mesh);
				}
			}
		}

		public static bool Hit(Vector3 game, Vec3 world, Vec3 dir)
		{
			if (Time.frameCount - _seenFrame > 2)
			{
				return false;
			}
			float best = 1.25f;
			Seen victim = default(Seen);
			bool found = false;
			Consider(_seenNow);
			Consider(_seenLast);
			if (!found)
			{
				return false;
			}
			return Lodge(victim, dir);
			void Consider(List<Seen> list)
			{
				for (int i = 0; i < list.Count; i++)
				{
					Seen seen = list[i];
					float num = game.x - seen.X;
					float num2 = game.y + game.z - (seen.Y + seen.Lift);
					float num3 = Mathf.Sqrt(num * num + num2 * num2);
					float num4 = world.X - seen.X;
					float num5 = world.Y - seen.Y;
					if (!(num3 >= best) && !(num4 * num4 + num5 * num5 > 9f))
					{
						best = num3;
						victim = seen;
						found = true;
					}
				}
			}
		}

		public static bool LodgeIn(Actor a, Vec3 dir)
		{
			if (a == null || Time.frameCount - _seenFrame > 2)
			{
				return false;
			}
			for (int i = 0; i < 2; i++)
			{
				List<Seen> list = ((i == 0) ? _seenNow : _seenLast);
				for (int j = 0; j < list.Count; j++)
				{
					if (list[j].Actor == a)
					{
						return Lodge(list[j], dir);
					}
				}
			}
			return false;
		}

		private static bool Lodge(Seen victim, Vec3 dir)
		{
			MeshInstance meshInstance = victim.Mesh;
			if (Fallen.TryGetValue(meshInstance, out var value))
			{
				meshInstance = value;
			}
			if (meshInstance.Model == null)
			{
				return false;
			}
			int num = PickPart(meshInstance.Model, victim.Person);
			if (num < 0)
			{
				return false;
			}
			Archer.Lodge(meshInstance, num, dir.Normalized(), 0.07f, U(), U(), U(), out var point, out var along);
			if (!InBodies.TryGetValue(meshInstance, out var value2))
			{
				value2 = (InBodies[meshInstance] = new List<Lodged>());
			}
			if (value2.Count >= 6)
			{
				value2.RemoveAt(0);
			}
			value2.Add(new Lodged
			{
				Part = meshInstance.Model.Parts[num].Name,
				Point = point,
				Along = along,
				Born = Time.time
			});
			Heard(PersonRig.Transform(meshInstance, num, point), Struck.Body);
			return true;
			float U()
			{
				return (float)Rng.NextDouble() * 2f - 1f;
			}
		}

		private static int PickPart(MeshModel model, bool person)
		{
			double num = Rng.NextDouble();
			string name = ((!person) ? ((num < 0.85) ? "body" : "head") : ((num < 0.62) ? "torso" : ((num < 0.72) ? "upper_arm_L" : ((num < 0.8) ? "upper_arm_R" : ((num < 0.9) ? "upper_leg_L" : "upper_leg_R")))));
			int num2 = model.PartIndex(name);
			if (num2 < 0)
			{
				num2 = model.PartIndex(person ? "torso" : "body");
			}
			if (num2 >= 0)
			{
				return num2;
			}
			int result = -1;
			int num3 = 0;
			for (int i = 0; i < model.Parts.Length; i++)
			{
				if (model.Parts[i].VertexCount > num3)
				{
					num3 = model.Parts[i].VertexCount;
					result = i;
				}
			}
			return result;
		}

		public static void Stick(Vec3 tip, Vec3 dir, bool ground)
		{
			Vec3 vec = dir.Normalized();
			Fixed item = new Fixed
			{
				Nock = tip - vec * 0.44f,
				Dir = vec,
				Born = Time.time,
				Held = (ground ? null : HoldersAt(tip))
			};
			World.Add(item);
			if (World.Count > 240)
			{
				World.RemoveAt(0);
			}
			Heard(tip, ground ? Struck.Ground : Struck.Wood);
		}

		private static List<Holder> HoldersAt(Vec3 tip)
		{
			List<Holder> list = null;
			try
			{
				int num = Mathf.FloorToInt(tip.X);
				int num2 = Mathf.FloorToInt(tip.Y);
				for (int i = num2 - 1; i <= num2 + 1; i++)
				{
					for (int j = num - 1; j <= num + 1; j++)
					{
						WorldTile tile = global::World.world.GetTile(j, i);
						if (tile == null)
						{
							continue;
						}
						if (tile.Type != null && tile.Type.wall)
						{
							(list ?? (list = new List<Holder>())).Add(new Holder
							{
								Wall = tile
							});
						}
						Building building = tile.building;
						if (building == null || building.data == null || !AssetStyle.IsSolid(building))
						{
							continue;
						}
						bool flag = false;
						if (list != null)
						{
							foreach (Holder item in list)
							{
								if (item.Building == building)
								{
									flag = true;
									break;
								}
							}
						}
						if (!flag)
						{
							(list ?? (list = new List<Holder>())).Add(new Holder
							{
								Building = building,
								Id = building.data.id,
								Asset = building.asset
							});
						}
					}
				}
			}
			catch
			{
			}
			return list;
		}

		private static bool StillHeld(Fixed f)
		{
			if (f.Held == null)
			{
				return true;
			}
			foreach (Holder item in f.Held)
			{
				if (item.Stands)
				{
					return true;
				}
			}
			return false;
		}

		public static void DrawWorld(List<MeshInstance> meshes, float eyeX, float eyeY, float range)
		{
			MeshModel meshModel = Models.Get("arrow");
			float time = Time.time;
			float num = range * range;
			for (int num2 = World.Count - 1; num2 >= 0; num2--)
			{
				Fixed obj = World[num2];
				float num3 = time - obj.Born;
				if (num3 > 35f || num3 < 0f)
				{
					World.RemoveAt(num2);
				}
				else if (obj.Held != null && (Time.frameCount + num2) % 15 == 0 && !StillHeld(obj))
				{
					World.RemoveAt(num2);
				}
				else
				{
					float num4 = obj.Nock.X - eyeX;
					float num5 = obj.Nock.Y - eyeY;
					float num6 = num4 * num4 + num5 * num5;
					if (meshModel != null && !(num6 > num))
					{
						obj.Mesh.Model = meshModel;
						obj.Mesh.Palette = null;
						Archer.PlaceArrow(obj.Mesh, obj.Nock, obj.Dir, 0.44f);
						obj.Mesh.Fade = Mathf.Clamp01((35f - num3) / 2f);
						obj.Mesh.SortDepth = Mathf.Sqrt(num6);
						obj.Mesh.Static = false;
						meshes.Add(obj.Mesh);
					}
				}
			}
			if (Time.frameCount % 300 != 0)
			{
				return;
			}
			Stale.Clear();
			foreach (KeyValuePair<MeshInstance, int> item in LastDrawn)
			{
				if (Time.frameCount - item.Value > 900)
				{
					Stale.Add(item.Key);
				}
			}
			foreach (MeshInstance item2 in Stale)
			{
				LastDrawn.Remove(item2);
				InBodies.Remove(item2);
			}
			Stale.Clear();
			foreach (KeyValuePair<MeshInstance, MeshInstance> item3 in Fallen)
			{
				if (!LastDrawn.ContainsKey(item3.Value) && !LastDrawn.ContainsKey(item3.Key))
				{
					Stale.Add(item3.Key);
				}
			}
			foreach (MeshInstance item4 in Stale)
			{
				Fallen.Remove(item4);
			}
		}
	}
}
