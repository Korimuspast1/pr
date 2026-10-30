using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class SpeedLines
	{
		private struct L
		{
			public Vec3 Pos;

			public Vec3 Vel;

			public float Age;

			public float Life;

			public float Length;

			public float Width;

			public uint Color;

			public bool Additive;
		}

		private readonly List<L> _list = new List<L>(64);

		public int Count => _list.Count;

		public void Add(Vec3 pos, Vec3 vel, float length, float width, float life, uint color, bool additive = true)
		{
			if (_list.Count < 200)
			{
				_list.Add(new L
				{
					Pos = pos,
					Vel = vel,
					Length = length,
					Width = width,
					Life = life,
					Color = color,
					Additive = additive
				});
			}
		}

		public void Step(float dt, float gravity = 4f)
		{
			if (dt <= 0f)
			{
				return;
			}
			for (int num = _list.Count - 1; num >= 0; num--)
			{
				L value = _list[num];
				value.Age += dt;
				if (value.Age >= value.Life)
				{
					_list.RemoveAt(num);
				}
				else
				{
					float num2 = (float)Math.Exp(-1.6f * dt);
					value.Vel = new Vec3(value.Vel.X * num2, value.Vel.Y * num2, value.Vel.Z * num2 - gravity * dt);
					value.Pos += value.Vel * dt;
					_list[num] = value;
				}
			}
		}

		public void Draw(FxSink sink)
		{
			foreach (L item in _list)
			{
				float num = item.Age / item.Life;
				float num2 = (float)Math.Sqrt(Vec3.Dot(item.Vel, item.Vel));
				if (!(num2 < 0.001f))
				{
					Vec3 vec = item.Vel * (-1f / num2);
					float num3 = item.Length * (0.4f + 0.6f * (1f - num));
					float num4 = (1f - num) * (1f - num);
					for (int i = 0; i < 7; i++)
					{
						float num5 = (float)i / 6f;
						Vec3 vec2 = item.Pos + vec * (num3 * num5);
						float num6 = item.Width * (1f - 0.75f * num5);
						sink.Sprite(PowerFx.Dot, vec2.X, vec2.Y, vec2.Z, num6, num6, item.Color, (byte)Math.Max(2, (int)(255f * num4 * (1f - 0.6f * num5))), emissive: true, item.Additive);
					}
				}
			}
		}
	}
}
