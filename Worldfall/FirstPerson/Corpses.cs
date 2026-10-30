using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class Corpses
	{
		private sealed class Body
		{
			public object Space;

			public MeshInstance Mesh;

			public MeshInstance Weapon;

			public PersonRig Rig;

			public Mat3 Basis0;

			public Mat3 WeaponBasis0;

			public Vec3 Feet;

			public Vec3 WeaponPos0;

			public Vec3 Axis;

			public float Age;

			public float Seed;

			public float Lie = 7f;

			public long Carcass;

			public bool Sideways;

			public bool Landed;

			public float Length;

			public bool Unlaid;
		}

		private const int Max = 48;

		private const float FallSeconds = 0.6f;

		private const float LieSeconds = 7f;

		private const float SinkSeconds = 1.6f;

		private static readonly List<Body> Bodies = new List<Body>();

		private static object _world;

		internal static Vector2 LastAwayForTest;

		private static int _aged = -1;

		public static int Count => Bodies.Count;

		public static void Fall(MeshInstance mesh, PersonRig rig, MeshInstance weapon, Vector2 away, float seed, float lie = 0f)
		{
			if (mesh != null && mesh.Model != null)
			{
				Body body = new Body
				{
					Mesh = Clone(mesh),
					Rig = rig,
					Seed = seed,
					Space = RoomCombat.FallingIn
				};
				if (lie > 7f)
				{
					body.Lie = lie;
				}
				StuckArrows.Follow(mesh, body.Mesh);
				People.RegaliaFall(mesh, body.Mesh);
				body.Basis0 = mesh.Basis;
				body.Feet = mesh.Position;
				if (away.sqrMagnitude < 0.0001f)
				{
					away = new Vector2(Mathf.Cos(seed * 6.28f), Mathf.Sin(seed * 6.28f));
				}
				away.Normalize();
				body.Axis = new Vec3(0f - away.y, away.x, 0f);
				body.Length = Tall(mesh) * 0.9f;
				if (weapon != null && weapon.Model != null)
				{
					body.Weapon = Clone(weapon);
					body.WeaponBasis0 = weapon.Basis;
					body.WeaponPos0 = weapon.Position;
				}
				Add(body);
			}
		}

		public static void Topple(MeshInstance mesh, float heading, float seed, float lie = 0f, long carcass = 0L)
		{
			if (mesh != null && mesh.Model != null)
			{
				Body body = new Body
				{
					Mesh = Clone(mesh),
					Seed = seed,
					Sideways = true,
					Carcass = ((lie > 0f) ? carcass : 0),
					Space = RoomCombat.FallingIn
				};
				if (lie > 7f)
				{
					body.Lie = lie;
				}
				StuckArrows.Follow(mesh, body.Mesh);
				body.Basis0 = mesh.Basis;
				body.Feet = mesh.Position;
				float num = ((seed > 0.5f) ? 1f : (-1f));
				body.Axis = new Vec3(Mathf.Cos(heading) * num, Mathf.Sin(heading) * num, 0f);
				body.Length = Tall(mesh);
				Add(body);
			}
		}

		private static void Add(Body b)
		{
			b.Unlaid = true;
			if (b.Space != null)
			{
				Lay(b);
			}
			Bodies.Add(b);
			if (Bodies.Count > 48)
			{
				Bodies.RemoveAt(0);
			}
		}

		public static void Clear()
		{
			object obj = World.world?.map_stats;
			if (obj == null || obj != _world)
			{
				_world = obj;
				Bodies.Clear();
				Slicing.Clear();
				Loot.Clear();
				Butchery.Clear();
			}
		}

		public static void Draw(List<MeshInstance> meshes, float dt, float eyeX, float eyeY, object space = null)
		{
			bool flag = _aged != Time.frameCount;
			_aged = Time.frameCount;
			for (int num = Bodies.Count - 1; num >= 0; num--)
			{
				Body body = Bodies[num];
				if (flag)
				{
					body.Age += dt;
				}
				if (body.Age > 0.6f + body.Lie + 1.6f)
				{
					Bodies.RemoveAt(num);
				}
				else if (body.Space == space)
				{
					if (body.Unlaid)
					{
						if (body.Age < 0.2f)
						{
							Lay(body);
						}
						else
						{
							body.Unlaid = false;
						}
					}
					float num2 = Mathf.Clamp01(body.Age / 0.6f);
					float num3 = num2 * num2 * 88f;
					if (body.Age > 0.6f)
					{
						float num4 = body.Age - 0.6f;
						num3 = 88f - 7f * Mathf.Exp((0f - num4) * 6f) * Mathf.Abs(Mathf.Sin(num4 * 15f));
						body.Landed = true;
					}
					if (body.Rig != null)
					{
						body.Rig.Limp(body.Mesh, Mathf.Clamp01(body.Age / 0.54f), body.Seed);
					}
					float num5 = 1f;
					if (body.Carcass != 0L)
					{
						float sinceCut;
						float num6 = Butchery.Remaining(body.Carcass, out sinceCut);
						if (num6 >= 0f)
						{
							num5 = 0.55f + 0.45f * num6;
						}
						if (sinceCut < 0.35f)
						{
							num3 += 6f * Mathf.Exp((0f - sinceCut) * 12f) * Mathf.Sin(sinceCut * 45f);
						}
						if (num6 <= 0f && body.Landed)
						{
							body.Lie = Mathf.Min(body.Lie, body.Age - 0.6f);
							body.Carcass = 0L;
						}
					}
					float num7 = ((body.Age > 0.6f + body.Lie) ? ((body.Age - 0.6f - body.Lie) / 1.6f) : 0f);
					Mat3 a = Mat3.Rotation(body.Axis, num3 * (MathF.PI / 180f));
					body.Mesh.Basis = ((num5 < 1f) ? (a * body.Basis0 * num5) : (a * body.Basis0));
					body.Mesh.Position = body.Feet - new Vec3(0f, 0f, num7 * 0.35f * body.Basis0.C2.Z * 1.8f);
					body.Mesh.Flash = 0u;
					body.Mesh.Static = false;
					float num8 = body.Feet.X - eyeX;
					float num9 = body.Feet.Y - eyeY;
					body.Mesh.SortDepth = Mathf.Sqrt(num8 * num8 + num9 * num9);
					body.Mesh.Tag = -1;
					int num10 = (int)(255f - 70f * Mathf.Clamp01(body.Age / 3f));
					body.Mesh.Multiply = TerrainCache.Rgb(num10, num10, num10);
					meshes.Add(body.Mesh);
					StuckArrows.Draw(meshes, body.Mesh, -1, 0u, body.Mesh.SortDepth);
					People.DrawFallenRegalia(meshes, body.Mesh, dt);
					if (body.Weapon != null)
					{
						Vec3 v = body.WeaponPos0 - body.Feet;
						body.Weapon.Basis = a * body.WeaponBasis0;
						body.Weapon.Position = body.Mesh.Position + a.Mul(v);
						body.Weapon.SortDepth = body.Mesh.SortDepth;
						body.Weapon.Tag = -1;
						meshes.Add(body.Weapon);
					}
				}
			}
			Slicing.Draw(meshes, dt, eyeX, eyeY, space);
		}

		private static void Lay(Body b)
		{
			b.Unlaid = false;
			Vector2 feet = new Vector2(b.Feet.X, b.Feet.Y);
			Vector2 vector = new Vector2(b.Axis.Y, 0f - b.Axis.X);
			Vector2 lastAwayForTest = ((b.Space != null) ? RoomCombat.FallWay(b.Space, feet, b.Feet.Z, vector, b.Length, b.Sideways) : ((Colliders.Near.Count <= 0) ? vector : LieWay(feet, vector, b.Length, b.Sideways, Colliders.Clear)));
			if (!(lastAwayForTest.sqrMagnitude < 1E-06f))
			{
				lastAwayForTest.Normalize();
				b.Axis = new Vec3(0f - lastAwayForTest.y, lastAwayForTest.x, 0f);
				if (!b.Sideways)
				{
					LastAwayForTest = lastAwayForTest;
				}
			}
		}

		internal static Vector2 LieWay(Vector2 feet, Vector2 away, float length, bool sides, Func<Vector2, bool> open)
		{
			if (away.sqrMagnitude < 1E-06f || length <= 0f)
			{
				return away;
			}
			away.Normalize();
			Vector2 result = away;
			int num = -1;
			int num2 = (sides ? 2 : 8);
			for (int i = 0; i < num2; i++)
			{
				float num3 = (sides ? ((float)i * 180f) : ((i == 7) ? 180f : ((float)((i + 1) / 2) * 45f * ((i % 2 == 1) ? 1f : (-1f)))));
				float num4 = Mathf.Cos(num3 * (MathF.PI / 180f));
				float num5 = Mathf.Sin(num3 * (MathF.PI / 180f));
				Vector2 vector = new Vector2(away.x * num4 - away.y * num5, away.x * num5 + away.y * num4);
				int num6 = 0;
				for (int j = 1; j <= 4; j++)
				{
					if (open(feet + vector * (length * (float)j / 4f)))
					{
						num6++;
					}
				}
				if (num6 > num)
				{
					result = vector;
					num = num6;
				}
				if (num6 == 4)
				{
					break;
				}
			}
			return result;
		}

		private static float Tall(MeshInstance m)
		{
			return (m.Model.Max.Z - m.Model.Min.Z) * Mathf.Abs(m.Basis.C2.Z);
		}

		private static MeshInstance Clone(MeshInstance m)
		{
			MeshInstance meshInstance = new MeshInstance
			{
				Model = m.Model,
				Position = m.Position,
				Basis = m.Basis,
				Hidden = m.Hidden,
				Palette = ((m.Palette != null) ? ((uint[])m.Palette.Clone()) : null),
				Multiply = m.Multiply,
				Emissive = m.Emissive,
				NoShadow = m.NoShadow,
				Glow = m.Glow
			};
			if (m.PartRotation != null)
			{
				meshInstance.PartRotation = (Mat3[])m.PartRotation.Clone();
				meshInstance.PartOffset = (Vec3[])m.PartOffset.Clone();
				meshInstance.PartScale = (float[])m.PartScale.Clone();
			}
			return meshInstance;
		}
	}
}
