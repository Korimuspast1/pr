using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class ShellCasings
	{
		private sealed class Casing
		{
			public Vec3 Pos;

			public Vec3 Vel;

			public Vec3 Axis;

			public Vec3 Spin;

			public float SpinRate;

			public float Age;

			public float Scale;

			public int Bounces;

			public bool Resting;

			public MeshInstance Mesh = new MeshInstance
			{
				NoShadow = true
			};
		}

		private readonly List<Casing> _list = new List<Casing>();

		public int Max = 24;

		public float Life = 9f;

		public int Landed;

		public Vec3 LandedAt;

		public int Count => _list.Count;

		public void Eject(Vec3 at, Vec3 vel, Vec3 axis, float scale, uint seed)
		{
			if (_list.Count >= Max)
			{
				_list.RemoveAt(0);
			}
			Vec3 spin = new Vec3(BlastFx.Rand(seed, 1) - 0.5f, BlastFx.Rand(seed, 2) - 0.5f, BlastFx.Rand(seed, 3) - 0.5f).Normalized();
			_list.Add(new Casing
			{
				Pos = at,
				Vel = vel,
				Axis = axis.Normalized(),
				Spin = spin,
				SpinRate = 14f + 10f * BlastFx.Rand(seed, 4),
				Scale = scale
			});
		}

		public void Step(float dt, Func<float, float, float> ground)
		{
			Landed = 0;
			if (dt <= 0f)
			{
				return;
			}
			dt = Math.Min(dt, 0.05f);
			for (int num = _list.Count - 1; num >= 0; num--)
			{
				Casing casing = _list[num];
				casing.Age += dt;
				if (casing.Age > Life)
				{
					_list.RemoveAt(num);
				}
				else if (!casing.Resting)
				{
					casing.Vel += new Vec3(0f, 0f, -9.8f * dt);
					casing.Vel *= (float)Math.Exp(-0.25f * dt);
					casing.Pos += casing.Vel * dt;
					casing.Axis = Vec3.Rotate(casing.Axis, casing.Spin, casing.SpinRate * dt).Normalized();
					float num2 = ground?.Invoke(casing.Pos.X, casing.Pos.Y) ?? 0f;
					float num3 = 0.0065f * casing.Scale;
					if (casing.Pos.Z < num2 + num3)
					{
						casing.Pos = new Vec3(casing.Pos.X, casing.Pos.Y, num2 + num3);
						if (casing.Bounces == 0)
						{
							Landed++;
							LandedAt = casing.Pos;
						}
						casing.Bounces++;
						if (casing.Bounces >= 3 || Math.Abs(casing.Vel.Z) < 0.5f)
						{
							casing.Resting = true;
							casing.Axis = new Vec3(casing.Axis.X, casing.Axis.Y, 0f);
							if (Vec3.Dot(casing.Axis, casing.Axis) < 0.0001f)
							{
								casing.Axis = new Vec3(1f, 0f, 0f);
							}
							casing.Axis = casing.Axis.Normalized();
						}
						else
						{
							casing.Vel = new Vec3(casing.Vel.X * 0.45f, casing.Vel.Y * 0.45f, (0f - casing.Vel.Z) * 0.32f);
							casing.SpinRate *= 0.55f;
						}
					}
				}
			}
		}

		public void Draw(List<MeshInstance> into, MeshModel shell)
		{
			if (shell == null)
			{
				return;
			}
			foreach (Casing item in _list)
			{
				Vec3 axis = item.Axis;
				Vec3 vec = ((Math.Abs(axis.Z) < 0.9f) ? Vec3.Cross(axis, new Vec3(0f, 0f, 1f)).Normalized() : new Vec3(1f, 0f, 0f));
				Vec3 vec2 = Vec3.Cross(vec, axis);
				float num = item.Scale * 1.15f;
				item.Mesh.Model = shell;
				item.Mesh.Basis = new Mat3(vec * num, axis * num, vec2 * num);
				item.Mesh.Position = item.Pos;
				item.Mesh.Tag = -2;
				item.Mesh.Fade = ((item.Age > Life - 1f) ? Math.Max(0f, Life - item.Age) : 1f);
				item.Mesh.Static = false;
				into.Add(item.Mesh);
			}
		}

		public void Clear()
		{
			_list.Clear();
		}
	}
}
