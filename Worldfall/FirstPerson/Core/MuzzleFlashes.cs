using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class MuzzleFlashes
	{
		private struct Flash
		{
			public Vec3 At;

			public Vec3 Dir;

			public float Age;

			public float Scale;

			public bool Own;

			public int Variant;
		}

		private readonly List<Flash> _list = new List<Flash>();

		public const float Life = 0.09f;

		public int Count => _list.Count;

		public void Add(Vec3 at, Vec3 dir, float scale, bool own, int variant)
		{
			if (_list.Count > 32)
			{
				_list.RemoveAt(0);
			}
			_list.Add(new Flash
			{
				At = at,
				Dir = dir.Normalized(),
				Scale = scale,
				Own = own,
				Variant = variant
			});
		}

		public void Step(float dt)
		{
			for (int num = _list.Count - 1; num >= 0; num--)
			{
				Flash value = _list[num];
				value.Age += Math.Max(0f, dt);
				if (value.Age > 0.09f)
				{
					_list.RemoveAt(num);
				}
				else
				{
					_list[num] = value;
				}
			}
		}

		public void Draw(FxSink sink)
		{
			foreach (Flash item in _list)
			{
				float num = 1f - item.Age / 0.09f;
				uint color = TerrainCache.Rgb(255, 196, 110);
				Vec3 vec = item.At + item.Dir * (0.25f * item.Scale);
				float num2 = 1f - BlastFx.Clamp01(sink.Day);
				sink.Light(vec.X, vec.Y, vec.Z, (3.5f + 2.5f * num2) * Math.Max(0.6f, item.Scale), color, (0.35f + 1.05f * num2) * (item.Own ? 1f : 0.85f) * num * num);
				if (!item.Own)
				{
					int num3 = Math.Min(2, (int)(item.Age / 0.032f));
					float num4;
					switch (num3)
					{
					default:
						num4 = 0.34f;
						break;
					case 1:
						num4 = 0.48f;
						break;
					case 0:
						num4 = 0.62f;
						break;
					}
					float num5 = num4 * item.Scale;
					Vec3 vec2 = item.At + item.Dir * (0.08f * item.Scale);
					sink.Sprite(ShotgunFx.FlashStar(num3, item.Variant), vec2.X, vec2.Y, vec2.Z, num5, num5, 0u, (byte)Math.Max(2, (int)(255f * Math.Min(1f, num * 1.6f))), emissive: true, additive: true);
					Vec3 vec3 = item.At + item.Dir * (0.32f * item.Scale);
					sink.Glow(vec3.X, vec3.Y, vec3.Z, 0.9f * item.Scale * (0.6f + 0.4f * num), TerrainCache.Rgb(255, 210, 120), 0.75f * num);
				}
			}
		}

		public void Clear()
		{
			_list.Clear();
		}
	}
}
