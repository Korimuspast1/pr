using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class Seats
	{
		internal static string Why;

		private static readonly List<Vector2> _fires = new List<Vector2>();

		private static float _firesAt = -10f;

		public static bool Evening
		{
			get
			{
				WorldBoxMod instance = WorldBoxMod.Instance;
				if (instance == null)
				{
					return false;
				}
				float hour = instance.Day.Hour;
				if (!(hour > 0.68f))
				{
					return hour < 0.12f;
				}
				return true;
			}
		}

		public static bool Ledge(Vector2 from, float ground, float body, float yaw, out RestSeat seat)
		{
			seat = default(RestSeat);
			Vector2 vector = new Vector2(Mathf.Cos(yaw), Mathf.Sin(yaw));
			float num = 0.1f * body;
			float num2 = 0.42f * body;
			for (float num3 = 0.2f; num3 <= 0.8f; num3 += 0.06f)
			{
				Vector2 vector2 = from + vector * num3;
				if (!BuildingFloors.StandOn(vector2.x, vector2.y, 0.04f, ground + num2, out var z, out var what))
				{
					continue;
				}
				float num4 = z - ground;
				if (num4 < num)
				{
					continue;
				}
				if (num4 > num2)
				{
					return false;
				}
				float num5 = num3 - 0.06f;
				float num6 = num3;
				for (int i = 0; i < 6; i++)
				{
					float num7 = (num5 + num6) * 0.5f;
					Vector2 vector3 = from + vector * num7;
					if (BuildingFloors.StandOn(vector3.x, vector3.y, 0.03f, ground + num2, out var z2, out what) && z2 > z - 0.03f)
					{
						num6 = num7;
					}
					else
					{
						num5 = num7;
					}
				}
				Vector2 vector4 = from + vector * num6;
				Vector2 vector5 = vector4 + vector * 0.22f * body;
				if (!BuildingFloors.StandOn(vector5.x, vector5.y, 0.03f, ground + num2, out var z3, out what) || Mathf.Abs(z3 - z) > 0.06f)
				{
					return false;
				}
				seat = new RestSeat
				{
					Kind = RestSeatKind.Ledge,
					At = vector4 + vector * 0.07f * body,
					Heading = yaw + MathF.PI,
					Floor = ground,
					Top = z
				};
				return true;
			}
			return false;
		}

		public static bool Rock(Building rock, MeshInstance mesh, Vector2 from, float body, TerrainCache land, out RestSeat seat)
		{
			seat = default(RestSeat);
			if (rock == null || mesh == null || mesh.Model == null || land == null)
			{
				return false;
			}
			try
			{
				if (rock.asset == null || rock.asset.building_type != BuildingType.Building_Mineral || !rock.isAlive())
				{
					return false;
				}
			}
			catch
			{
				return false;
			}
			MeshModel model = mesh.Model;
			Vector2 vector = new Vector2(mesh.Position.X, mesh.Position.Y);
			Vector2 vector2 = from - vector;
			if (vector2.sqrMagnitude < 0.0001f)
			{
				vector2 = Vector2.right;
			}
			vector2.Normalize();
			float b = Mathf.Sqrt(mesh.Basis.C0.X * mesh.Basis.C0.X + mesh.Basis.C0.Y * mesh.Basis.C0.Y);
			float num = 0.5f * Mathf.Max(model.Max.X - model.Min.X, model.Max.Y - model.Min.Y) * Mathf.Max(0.01f, b);
			if (num < 0.14f * body)
			{
				Why = $"rock too small ({num:F2} across)";
				return false;
			}
			float num2 = mesh.Position.Z + model.Max.Z * mesh.Basis.C2.Z;
			Why = "rock too tall";
			float[] array = new float[5] { 0.45f, 0.3f, 0.6f, 0.15f, 0.75f };
			foreach (float num3 in array)
			{
				Vector2 at = vector + vector2 * num * num3;
				float num4 = land.SampleHeight(at.x, at.y);
				Vec3 a = new Vec3(at.x, at.y, num2 + 0.5f);
				Vec3 b2 = new Vec3(at.x, at.y, num4 - 0.2f);
				if (!MeshRay.Segment(mesh, a, b2, out var hit))
				{
					Why = "off the rock";
					continue;
				}
				float num5 = a.Z + (b2.Z - a.Z) * hit;
				float num6 = num5 - num4;
				if (num6 < 0.08f * body)
				{
					Why = $"rock too low ({num6:F2})";
					continue;
				}
				if (num6 > 0.55f * body)
				{
					Why = $"rock too tall ({num6:F2})";
					continue;
				}
				seat = new RestSeat
				{
					Kind = RestSeatKind.Rock,
					At = at,
					Heading = Mathf.Atan2(vector2.y, vector2.x),
					Floor = num4,
					Top = num5
				};
				return true;
			}
			return false;
		}

		public static bool PierEdge(Vector2 from, float yaw, float body, TerrainCache land, out RestSeat seat)
		{
			seat = default(RestSeat);
			if (land == null || !DockDecks.HeightAt(from.x, from.y, out var z))
			{
				return false;
			}
			float num = float.MaxValue;
			float[] array = new float[7] { 0f, 0.5f, -0.5f, 1f, -1f, 1.57f, -1.57f };
			foreach (float num2 in array)
			{
				Vector2 vector = new Vector2(Mathf.Cos(yaw + num2), Mathf.Sin(yaw + num2));
				for (float num3 = 0.05f; num3 <= 1.1f; num3 += 0.05f)
				{
					Vector2 p = from + vector * num3;
					if (DockDecks.HeightAt(p.x, p.y, out var z2) && z2 > z - 0.08f)
					{
						continue;
					}
					if (Water(land, p))
					{
						float num4 = num3 + Mathf.Abs(num2) * 0.6f;
						if (num4 < num && num3 > 0.1f)
						{
							num = num4;
							Vector2 vector2 = from + vector * (num3 - 0.05f);
							seat = new RestSeat
							{
								Kind = RestSeatKind.Pier,
								At = vector2 - vector * 0.1f * body,
								Heading = yaw + num2,
								Floor = z,
								Top = z,
								Hang = true
							};
						}
					}
					break;
				}
			}
			return num < float.MaxValue;
		}

		private static bool Water(TerrainCache land, Vector2 p)
		{
			int num = (int)p.x;
			int num2 = (int)p.y;
			if (num < 0 || num2 < 0 || num >= land.Width || num2 >= land.Height)
			{
				return false;
			}
			return (land.Flags[num2 * land.Width + num] & 1) != 0;
		}

		public static bool Chair(HouseInterior house, Vector2 eye, float yaw, out RestSeat seat)
		{
			seat = default(RestSeat);
			Interior interior = house?.Room;
			if (interior == null || !house.Inside)
			{
				return false;
			}
			float num = float.MaxValue;
			Vector2 rhs = new Vector2(Mathf.Cos(yaw), Mathf.Sin(yaw));
			for (int i = 0; i < interior.Spots.Count; i++)
			{
				float[] array = interior.Spots[i];
				if (array.Length <= 3 || array[3] <= 0f)
				{
					continue;
				}
				Vector2 vector = new Vector2(array[0], array[1]);
				Vector2 vector2 = vector - eye;
				float magnitude = vector2.magnitude;
				if (magnitude > 1.5f)
				{
					continue;
				}
				float num2 = ((magnitude > 0.05f) ? Vector2.Dot(vector2 / magnitude, rhs) : 1f);
				if ((!(num2 < 0.35f) || !(magnitude > 0.7f)) && !house.SeatTaken(vector))
				{
					float num3 = magnitude - num2 * 0.5f;
					if (!(num3 >= num))
					{
						num = num3;
						seat = new RestSeat
						{
							Kind = RestSeatKind.Chair,
							At = vector,
							Heading = array[2] * (MathF.PI / 180f),
							Floor = ((array.Length > 4) ? array[4] : 0f),
							Knee = true,
							Spot = i
						};
						seat.Top = seat.Floor;
					}
				}
			}
			return num < float.MaxValue;
		}

		public static bool FireNear(Vector2 at, float within, out Vector2 fire)
		{
			fire = default(Vector2);
			float unscaledTime = Time.unscaledTime;
			if (unscaledTime - _firesAt > 2f || unscaledTime < _firesAt)
			{
				_firesAt = unscaledTime;
				_fires.Clear();
				try
				{
					foreach (Building simple in World.world.buildings.getSimpleList())
					{
						if (simple != null && simple.asset != null && simple.isAlive() && !simple.isRuin() && !simple.isUnderConstruction() && (!(simple.asset.type != "type_bonfire") || !(simple.asset.id != "bonfire")))
						{
							WorldTile current_tile = simple.current_tile;
							if (current_tile != null)
							{
								_fires.Add(new Vector2(current_tile.posV3.x, current_tile.posV3.y));
							}
						}
					}
				}
				catch
				{
				}
			}
			float num = within * within;
			bool result = false;
			foreach (Vector2 fire2 in _fires)
			{
				float sqrMagnitude = (fire2 - at).sqrMagnitude;
				if (!(sqrMagnitude >= num))
				{
					num = sqrMagnitude;
					fire = fire2;
					result = true;
				}
			}
			return result;
		}
	}
}
