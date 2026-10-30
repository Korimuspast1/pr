using System;

namespace FirstPerson.Core
{
	public sealed class BowlingBallFx
	{
		public static float Radius = 4.5f;

		private readonly MeshInstance _inst = new MeshInstance
		{
			NoShadow = false
		};

		private readonly uint _seed;

		private Mat3 _roll = Mat3.Identity;

		private float _lx;

		private float _ly;

		private bool _have;

		private float _dustClock;

		public Vec3 Centre;

		public BowlingBallFx(uint seed)
		{
			_seed = seed;
			_roll = Mat3.RotZ(PowerFx.Rand(seed, 1) * 6.28f) * Mat3.RotX(PowerFx.Rand(seed, 2) * 6.28f);
		}

		public void Draw(FxSink sink, FxParticles parts, MeshModel model, float x, float y, float height, float dt, float time)
		{
			float num = ((sink.Ground != null) ? sink.Ground(x, y) : 0f);
			Vec3 position = (Centre = new Vec3(x, y, num + Math.Max(0f, height) + Radius * 0.96f));
			if (_have)
			{
				float num2 = x - _lx;
				float num3 = y - _ly;
				float num4 = (float)Math.Sqrt(num2 * num2 + num3 * num3);
				if (num4 > 0.0001f && num4 < 20f)
				{
					Vec3 axis = new Vec3((0f - num3) / num4, num2 / num4, 0f);
					_roll = Mat3.Rotation(axis, num4 / Radius * 0.8f) * _roll;
				}
			}
			_lx = x;
			_ly = y;
			_have = true;
			if (model != null)
			{
				float num5 = Math.Max(0.1f, (model.Max.X - model.Min.X) * 0.5f);
				_inst.Model = model;
				_inst.Basis = _roll * (Radius / num5);
				_inst.Position = position;
				_inst.SortDepth = sink.Distance(x, y);
				_inst.Tag = -2;
				sink.Meshes.Add(_inst);
			}
			else
			{
				sink.Sprite(BlastFx.Puff(0), x, y, position.Z, Radius * 2f, Radius * 2f, TerrainCache.Rgb(66, 91, 120), 0, emissive: false);
			}
			if (parts != null && dt > 0f && height < 2.5f)
			{
				_dustClock += dt * 20f * (1f - height / 2.5f);
				while (_dustClock > 1f)
				{
					_dustClock -= 1f;
					int num6 = parts.List.Count + (int)(time * 577f);
					float num7 = PowerFx.Rand(_seed, num6) * 6.2832f;
					Vec3 at = new Vec3(x + (float)Math.Cos(num7) * Radius * 0.7f, y + (float)Math.Sin(num7) * Radius * 0.7f, num + 0.4f);
					parts.Smoke(at, new Vec3((float)Math.Cos(num7) * 1.5f, (float)Math.Sin(num7) * 1.5f, 0.8f), 1.6f, 1f, 3f, TerrainCache.Rgb(150, 136, 116), TerrainCache.Rgb(184, 176, 160), 150, num6, 1.2f);
				}
			}
		}
	}
}
