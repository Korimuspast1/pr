using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class Boats
	{
		public const float BoardReach = 1.4f;

		public const float ShoreReach = 2.6f;

		private static readonly Dictionary<Actor, Actor> Aboard = new Dictionary<Actor, Actor>();

		public static bool CanBoard(Actor host, Actor boat, Vector2 from)
		{
			try
			{
				if (host == null || boat == null || host == boat || !host.isAlive() || !boat.isAlive())
				{
					return false;
				}
				if (host.asset == null || host.asset.is_boat || host.is_inside_boat || host.is_inside_building || host.position_height > 0.3f)
				{
					return false;
				}
				if (boat.asset == null || !boat.asset.is_boat || boat.getSimpleComponent<Boat>() == null)
				{
					return false;
				}
				if (Models.Get(Models.CabinFor(Models.BoatFamily(boat))) == null)
				{
					return false;
				}
				return HullDistance(boat, from) < 1.4f;
			}
			catch
			{
				return false;
			}
		}

		public static float HullDistance(Actor boat, Vector2 p)
		{
			return Gap(Hull(boat), p);
		}

		private static float Gap(Collider hull, Vector2 p)
		{
			Vector2 normal;
			float num = Colliders.Distance(hull, p, out normal);
			Footprint shape = hull.Shape;
			if (shape == null || num < 50f)
			{
				return num;
			}
			float num2 = p.x - hull.X;
			float num3 = p.y - hull.Y;
			float num4 = (num2 * hull.Cos + num3 * hull.Sin) / hull.Scale;
			float num5 = ((0f - num2) * hull.Sin + num3 * hull.Cos) / hull.Scale;
			float x = shape.X0;
			float y = shape.Y0;
			float num6 = shape.X0 + (float)shape.W * shape.Cell;
			float num7 = shape.Y0 + (float)shape.H * shape.Cell;
			float num8 = Mathf.Max(0f, Mathf.Max(x - num4, num4 - num6));
			float num9 = Mathf.Max(0f, Mathf.Max(y - num5, num5 - num7));
			return (Mathf.Sqrt(num8 * num8 + num9 * num9) + 5f * shape.Cell) * hull.Scale;
		}

		private static Collider Hull(Actor boat)
		{
			MeshModel meshModel = Models.Get(Models.BoatFamily(boat));
			Vector2 vector = boat.current_position + Creatures.MooringOffset(boat);
			if (meshModel == null)
			{
				return new Collider
				{
					X = vector.x,
					Y = vector.y,
					Radius = 1f
				};
			}
			float heading2;
			float heading = (Creatures.BoatHeading(boat, out heading2) ? heading2 : (boat.flip ? 0f : MathF.PI));
			Creatures.BoatPose(boat, 0f, 0f, heading, out var _, out var _, out var scale);
			return Colliders.FromModel(meshModel, vector.x, vector.y, heading, scale, -0.9f, 2.2f);
		}

		public static bool Board(Actor host, Actor boatActor)
		{
			try
			{
				Boat simpleComponent = boatActor.getSimpleComponent<Boat>();
				if (simpleComponent == null)
				{
					return false;
				}
				host.stopMovement();
				host.cancelAllBeh();
				host.is_inside_boat = true;
				host.inside_boat = simpleComponent;
				boatActor.callbacks_on_death = (BaseActionActor)Delegate.Combine(Delegate.Remove(boatActor.callbacks_on_death, new BaseActionActor(Sank)), new BaseActionActor(Sank));
				Aboard[host] = boatActor;
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("could not get aboard: " + ex.Message);
				return false;
			}
		}

		public static void Adopt(Actor host, Actor boatActor)
		{
			if (host != null && boatActor != null && !Aboard.ContainsKey(host))
			{
				boatActor.callbacks_on_death = (BaseActionActor)Delegate.Combine(Delegate.Remove(boatActor.callbacks_on_death, new BaseActionActor(Sank)), new BaseActionActor(Sank));
				Aboard[host] = boatActor;
			}
		}

		public static void Forget(Actor host)
		{
			try
			{
				Boat inside_boat = host.inside_boat;
				if (inside_boat != null && inside_boat.hasPassenger(host))
				{
					inside_boat.removePassenger(host);
				}
			}
			catch
			{
			}
			Aboard.Remove(host);
		}

		public static bool IsAboard(Actor host, Actor boat)
		{
			try
			{
				return host != null && host.is_inside_boat && host.inside_boat != null && ((ActorSimpleComponent)host.inside_boat).actor == boat;
			}
			catch
			{
				return false;
			}
		}

		public static Vector2 Disembark(Actor host, Actor boatActor, out bool wet)
		{
			wet = false;
			Vector2 vector = host?.current_position ?? Vector2.zero;
			try
			{
				if (boatActor != null)
				{
					try
					{
						boatActor.callbacks_on_death = (BaseActionActor)Delegate.Remove(boatActor.callbacks_on_death, new BaseActionActor(Sank));
					}
					catch
					{
					}
				}
				Aboard.Remove(host);
				if (host == null || (!IsAboard(host, boatActor) && !host.is_inside_boat))
				{
					return vector;
				}
				vector = ((boatActor != null) ? Landing(boatActor, out wet) : host.current_position);
				PutOff(host, vector);
			}
			catch (Exception ex)
			{
				Log.Error("could not get ashore: " + ex.Message);
			}
			return vector;
		}

		public static Vector2 Landing(Actor boat, out bool wet)
		{
			wet = false;
			Vector2 vector = boat.current_position + Creatures.MooringOffset(boat);
			float heading;
			float f = (Creatures.BoatHeading(boat, out heading) ? heading : 0f);
			Collider hull = Hull(boat);
			float num = float.MaxValue;
			Vector2 result = vector;
			for (int i = 0; i < 2; i++)
			{
				if (num != float.MaxValue)
				{
					break;
				}
				for (float num2 = 0.5f; num2 <= 6f; num2 += 0.25f)
				{
					int num3 = Mathf.Max(8, (int)(num2 * 2f * MathF.PI / 0.3f));
					for (int j = 0; j < num3; j++)
					{
						float f2 = (float)j * 2f * MathF.PI / (float)num3;
						Vector2 vector2 = new Vector2(vector.x + Mathf.Cos(f2) * num2, vector.y + Mathf.Sin(f2) * num2);
						float num4 = Gap(hull, vector2);
						if (num4 < 0.25f || num4 > 2.6f)
						{
							continue;
						}
						bool flag = DockDecks.On(vector2);
						if ((flag || ((i == 0) ? FirmGround(vector2) : DryGround(vector2))) && !Blocked(vector2))
						{
							float num5 = num4 + (flag ? 0f : 0.6f);
							if (num5 < num)
							{
								num = num5;
								result = vector2;
							}
						}
					}
					if (num < num2 * 0.5f)
					{
						break;
					}
				}
			}
			if (num < float.MaxValue)
			{
				return result;
			}
			wet = true;
			Vector2 vector3 = new Vector2(0f - Mathf.Sin(f), Mathf.Cos(f));
			for (float num6 = 0.4f; num6 < 4f; num6 += 0.2f)
			{
				float[] array = new float[2] { 1f, -1f };
				foreach (float num7 in array)
				{
					Vector2 vector4 = vector + vector3 * (num7 * num6);
					if (Gap(hull, vector4) > 0.35f && !Blocked(vector4))
					{
						return vector4;
					}
				}
			}
			return vector + vector3 * 1.5f;
		}

		private static bool FirmGround(Vector2 p)
		{
			if (DryGround(p) && DryGround(p + new Vector2(0.3f, 0f)) && DryGround(p - new Vector2(0.3f, 0f)) && DryGround(p + new Vector2(0f, 0.3f)))
			{
				return DryGround(p - new Vector2(0f, 0.3f));
			}
			return false;
		}

		private static bool DryGround(Vector2 p)
		{
			try
			{
				WorldTile tileAt = Toolbox.getTileAt(p.x, p.y);
				return tileAt != null && tileAt.Type != null && !tileAt.Type.liquid && !tileAt.Type.block && !tileAt.Type.lava;
			}
			catch
			{
				return false;
			}
		}

		private static bool Blocked(Vector2 p)
		{
			for (int i = 0; i < Colliders.Near.Count; i++)
			{
				Collider c = Colliders.Near[i];
				if (!c.Mobile && Colliders.Distance(c, p, out var _) < 0.22f)
				{
					return true;
				}
			}
			return false;
		}

		public static bool CanGoAshore(Actor boat)
		{
			try
			{
				Landing(boat, out var wet);
				return !wet;
			}
			catch
			{
				return false;
			}
		}

		private static void PutOff(Actor host, Vector2 at)
		{
			try
			{
				Boat inside_boat = host.inside_boat;
				if (inside_boat != null && inside_boat.hasPassenger(host))
				{
					inside_boat.removePassenger(host);
				}
			}
			catch
			{
			}
			WorldTile pTile = Toolbox.getTileAt(at.x, at.y) ?? host.current_tile;
			host.spawnOn(pTile, 0f);
			host.exitBoat();
			host.current_position = at;
			host.dirty_current_tile = true;
			host.data.transportID = -1L;
		}

		private static void Sank(Actor boat)
		{
			List<Actor> list = new List<Actor>();
			foreach (KeyValuePair<Actor, Actor> item in Aboard)
			{
				if (item.Value == boat)
				{
					list.Add(item.Key);
				}
			}
			foreach (Actor item2 in list)
			{
				Aboard.Remove(item2);
				try
				{
					if (item2 != null && item2.isAlive() && item2.is_inside_boat && (item2.inside_boat == null || ((ActorSimpleComponent)item2.inside_boat).actor == null || ((ActorSimpleComponent)item2.inside_boat).actor == boat))
					{
						PutOff(item2, boat.current_position + new Vector2(0.4f, 0.2f));
						Log.Info("the boat went down with " + WorldBoxMod.Describe(item2) + " aboard");
					}
				}
				catch (Exception ex)
				{
					Log.Error("thrown from a sinking boat: " + ex.Message);
				}
			}
		}
	}
}
