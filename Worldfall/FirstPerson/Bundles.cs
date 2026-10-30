using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class Bundles
	{
		internal sealed class Bundle
		{
			public Vector2 At;

			public float Z;

			public float Yaw;

			public float Age;

			public string Resource;

			public int Count;

			public Gear.Piece Piece;

			public readonly MeshInstance Mesh = new MeshInstance
			{
				Static = true
			};
		}

		public const float Life = 300f;

		public const float FadeSeconds = 3f;

		public const int Most = 40;

		private const float Scale = 0.5f;

		private const float SackScale = 0.38f;

		public static readonly List<Bundle> List = new List<Bundle>();

		private static readonly System.Random Rng = new System.Random();

		private static object _world;

		private static int _tickFrame = -1;

		private static void KeepToWorld()
		{
			object obj = null;
			try
			{
				obj = World.world?.map_stats;
			}
			catch
			{
			}
			if (obj != _world)
			{
				List.Clear();
				_world = obj;
			}
		}

		public static Bundle Drop(Vector2 at, Func<Vector2, float> groundAt, string resource, int count, Gear.Piece piece)
		{
			KeepToWorld();
			Vector2 vector = at;
			for (int i = 0; i < 12; i++)
			{
				Bundle bundle = List.Find((Bundle o) => (o.At - at).sqrMagnitude < 0.16f);
				if (bundle == null && Dry(at))
				{
					break;
				}
				float f = (float)(Rng.NextDouble() * Math.PI * 2.0);
				at = (bundle?.At ?? vector) + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * 0.42f;
			}
			if (!Dry(at))
			{
				at = vector;
			}
			float z = 0f;
			try
			{
				z = groundAt(at);
			}
			catch
			{
			}
			Bundle bundle2 = new Bundle
			{
				At = at,
				Z = z,
				Yaw = (float)(Rng.NextDouble() * Math.PI * 2.0),
				Resource = resource,
				Count = count,
				Piece = piece
			};
			List.Add(bundle2);
			while (List.Count > 40)
			{
				List.RemoveAt(0);
			}
			return bundle2;
		}

		public static bool Dry(Vector2 at)
		{
			WorldTile worldTile = null;
			try
			{
				worldTile = World.world.GetTile((int)at.x, (int)at.y);
			}
			catch
			{
			}
			if (worldTile != null && worldTile.Type != null)
			{
				return !worldTile.Type.liquid;
			}
			return false;
		}

		public static string NameOf(Bundle b)
		{
			if (b.Piece != null)
			{
				return Gear.NameOf(b.Piece);
			}
			return Work.Amount(b.Resource, b.Count);
		}

		public static Bundle Near(Vector2 you, float yaw, float reach)
		{
			KeepToWorld();
			Bundle result = null;
			float num = float.MaxValue;
			Vector2 rhs = new Vector2(Mathf.Cos(yaw), Mathf.Sin(yaw));
			foreach (Bundle item in List)
			{
				Vector2 vector = item.At - you;
				float magnitude = vector.magnitude;
				if (magnitude > reach)
				{
					continue;
				}
				float num2 = ((magnitude < 0.45f) ? 1f : Vector2.Dot(vector / magnitude, rhs));
				if (!(num2 < 0.55f))
				{
					float num3 = magnitude * (2f - num2);
					if (num3 < num)
					{
						num = num3;
						result = item;
					}
				}
			}
			return result;
		}

		public static bool PickUp(Actor host, Bundle b, Action<string> toast)
		{
			if (host == null || b == null || !List.Contains(b))
			{
				return false;
			}
			if (b.Piece != null)
			{
				if (!Gear.HasRoom(host))
				{
					toast("No room in your bag for " + Gear.NameOf(b.Piece));
					return false;
				}
				if (!Gear.Add(host, b.Piece))
				{
					return false;
				}
				List.Remove(b);
				toast("You pick up " + Gear.NameOf(b.Piece));
				return true;
			}
			int num = Mathf.Min(Bag.RoomFor(host, b.Resource), b.Count);
			if (num <= 0)
			{
				toast($"Your {Bag.Name(host).ToLowerInvariant()} is full ({Bag.Size(host)} slots)");
				return false;
			}
			try
			{
				if (Supplies.Is(b.Resource))
				{
					Supplies.Give(host, b.Resource, num);
				}
				else
				{
					host.addToInventory(b.Resource, num);
				}
			}
			catch (Exception ex)
			{
				Log.Error("bundles: picking up " + b.Resource + ": " + ex.Message);
				return false;
			}
			string text = Work.Amount(b.Resource, num);
			b.Count -= num;
			if (b.Count <= 0)
			{
				List.Remove(b);
				toast("You pick up " + text);
				return true;
			}
			toast("You pick up " + text + ". Your bag is full");
			return false;
		}

		private static int BagCount(Actor host)
		{
			int num = 0;
			try
			{
				Dictionary<string, ResourceContainer> dictionary = host.inventory?.getResources();
				if (dictionary != null)
				{
					foreach (KeyValuePair<string, ResourceContainer> item in dictionary)
					{
						if (item.Value.amount > 0)
						{
							num += item.Value.amount;
						}
					}
				}
			}
			catch
			{
			}
			return num;
		}

		private static void Tick()
		{
			if (_tickFrame == Time.frameCount)
			{
				return;
			}
			_tickFrame = Time.frameCount;
			bool flag = false;
			try
			{
				flag = Config.paused || (World.world != null && World.world.isPaused());
			}
			catch
			{
			}
			if (flag)
			{
				return;
			}
			float num = Mathf.Min(Time.unscaledDeltaTime, 0.5f);
			foreach (Bundle item in List)
			{
				item.Age += num;
			}
		}

		public static void DrawWorld(List<MeshInstance> meshes, float eyeX, float eyeY, float range)
		{
			if (List.Count == 0)
			{
				return;
			}
			KeepToWorld();
			Tick();
			float num = range * range;
			MeshModel sack = Models.Get("int_sack");
			for (int num2 = List.Count - 1; num2 >= 0; num2--)
			{
				Bundle bundle = List[num2];
				float age = bundle.Age;
				if (age > 300f)
				{
					List.RemoveAt(num2);
				}
				else
				{
					float num3 = bundle.At.x - eyeX;
					float num4 = bundle.At.y - eyeY;
					float num5 = num3 * num3 + num4 * num4;
					if (!(num5 > num))
					{
						MeshInstance mesh = bundle.Mesh;
						if (Lay(bundle, mesh, sack))
						{
							mesh.Fade = Mathf.Clamp01((300f - age) / 3f);
							mesh.SortDepth = Mathf.Sqrt(num5);
							meshes.Add(mesh);
						}
					}
				}
			}
		}

		private static bool Lay(Bundle b, MeshInstance m, MeshModel sack)
		{
			WeaponKind weaponKind = WeaponKind.None;
			if (b.Piece != null)
			{
				try
				{
					if (b.Piece.Asset != null && b.Piece.Asset.equipment_type == EquipmentType.Weapon)
					{
						weaponKind = ViewMeshes.LookOf(b.Piece.Id, null).Kind;
					}
				}
				catch
				{
				}
			}
			if (weaponKind == WeaponKind.Sword || weaponKind == WeaponKind.Dagger || weaponKind == WeaponKind.Axe || weaponKind == WeaponKind.Hammer || weaponKind == WeaponKind.Spear || weaponKind == WeaponKind.Stick || weaponKind == WeaponKind.Staff)
			{
				m.Model = ViewMeshes.Item(ViewMeshes.LookOf(b.Piece.Id, null));
				m.Palette = null;
				float num = Mathf.Cos(b.Yaw);
				float num2 = Mathf.Sin(b.Yaw);
				m.Basis = new Mat3(new Vec3(0f - num2, num, 0f), new Vec3(0f, 0f, 1f), new Vec3(num, num2, 0f)) * 0.5f;
				m.Position = new Vec3(b.At.x - num * 0.18f, b.At.y - num2 * 0.18f, b.Z + 0.045f);
				m.NoShadow = true;
				return m.Model != null;
			}
			if (sack == null)
			{
				return false;
			}
			m.Model = sack;
			m.Palette = null;
			float num3 = ((b.Piece != null) ? 0.9f : Mathf.Lerp(0.75f, 1.1f, Mathf.Clamp01((float)b.Count / 12f)));
			m.Place(b.At.x, b.At.y, b.Z, b.Yaw, 0.38f * num3, 0.38f * num3, 0.38f * num3);
			m.NoShadow = false;
			return true;
		}

		public static void Clear()
		{
			List.Clear();
		}
	}
}
