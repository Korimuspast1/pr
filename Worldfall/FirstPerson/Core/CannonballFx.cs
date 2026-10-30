using System;

namespace FirstPerson.Core
{
	public sealed class CannonballFx
	{
		public readonly uint Seed;

		private readonly MeshInstance _mesh = new MeshInstance();

		private const int History = 9;

		private readonly Vec3[] _hist = new Vec3[9];

		private readonly float[] _histAge = new float[9];

		private int _histCount;

		private Vec3 _last;

		private bool _have;

		private float _trail;

		private float _sparkClock;

		public CannonballFx(uint seed)
		{
			Seed = seed;
		}

		public void Draw(FxSink sink, FxParticles parts, Vec3 at, float size, float dt, float detail = 1f)
		{
			if (dt > 0f)
			{
				for (int i = 0; i < _histCount; i++)
				{
					_histAge[i] += dt;
				}
				if (_histCount == 9)
				{
					Array.Copy(_hist, 1, _hist, 0, 8);
					Array.Copy(_histAge, 1, _histAge, 0, 8);
					_histCount--;
				}
				_hist[_histCount] = at;
				_histAge[_histCount] = 0f;
				_histCount++;
			}
			_mesh.Model = CannonFx.Ball;
			if (_mesh.Palette == null)
			{
				_mesh.Palette = new uint[1] { BlastFx.Heat(0.62f) };
			}
			_mesh.Emissive = true;
			_mesh.Basis = Mat3.Identity * (0.3f * size);
			_mesh.Position = at;
			_mesh.Fade = 1f;
			_mesh.NoShadow = false;
			_mesh.SortDepth = sink.Distance(at.X, at.Y);
			sink.AddMesh(_mesh);
			float num = 0.85f + 0.15f * (float)Math.Sin(_trail * 31f + (float)Seed);
			sink.Glow(at.X, at.Y, at.Z, size * 1.05f, BlastFx.Heat(0.78f), 0.5f * num);
			sink.Glow(at.X, at.Y, at.Z, size * 0.45f, BlastFx.Heat(1.05f), 0.6f);
			for (int num2 = _histCount - 1; num2 > 0; num2--)
			{
				float num3 = 1f - _histAge[num2] / 0.22f;
				float val = 1f - _histAge[num2 - 1] / 0.22f;
				if (num3 <= 0f)
				{
					break;
				}
				float num4 = 1f - num3;
				sink.Line(_hist[num2], _hist[num2 - 1], BlastFx.Heat(1.08f - 0.55f * num4), num3 * 0.95f, Math.Max(0f, val) * 0.95f, 0.08f * size);
				Vec3 vec = (_hist[num2] + _hist[num2 - 1]) * 0.5f;
				float num5 = size * (0.5f - 0.3f * num4);
				sink.Sprite(BlastFx.GlowSprite, vec.X, vec.Y, vec.Z, num5, num5, BlastFx.Heat(1f - 0.4f * num4), (byte)Math.Max(2, (int)(200f * num3)), emissive: true);
			}
			if (_have)
			{
				Vec3 vec2 = at - _last;
				float num6 = (float)Math.Sqrt(Vec3.Dot(vec2, vec2));
				if (parts != null && num6 > 0.0001f && num6 < 6f)
				{
					float num7 = 0.45f * size / Math.Max(0.4f, detail);
					_trail += num6;
					int num8 = 0;
					while (_trail > num7 && num8++ < 24)
					{
						_trail -= num7;
						Vec3 at2 = at - vec2 * (_trail / num6);
						int num9 = (int)(Seed & 0x3FF) + num8 * 7 + (int)(at.X * 13f);
						parts.Smoke(at2, new Vec3(0f, 0f, 0.25f), 0.8f + 0.35f * CannonFx.R(Seed, num9), 0.2f * size, 0.85f * size, TerrainCache.Rgb(178, 174, 168), TerrainCache.Rgb(216, 214, 210), 120, num9, 1.4f);
					}
					_sparkClock += dt;
					if (_sparkClock > 0.05f && detail > 0.5f)
					{
						_sparkClock = 0f;
						Vec3 vec3 = vec2 * (-1f / num6);
						int num10 = (int)(at.Y * 17f);
						parts.Spark(at, vec3 * 2f + new Vec3((CannonFx.R(Seed, num10) - 0.5f) * 2f, (CannonFx.R(Seed, num10 + 1) - 0.5f) * 2f, CannonFx.R(Seed, num10 + 2) * 1.5f), 0.3f, 0.06f, BlastFx.Heat(1.1f), BlastFx.Heat(0.5f), 4f, 1f);
					}
				}
			}
			sink.Light(at.X, at.Y, at.Z, 3.5f * size, BlastFx.Heat(0.85f), 0.8f);
			_last = at;
			_have = true;
		}
	}
}
