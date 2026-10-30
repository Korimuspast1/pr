using System;

namespace FirstPerson.Core
{
	public sealed class CrabLandFx
	{
		public readonly float X;

		public readonly float Y;

		public readonly float Facing;

		public readonly uint Seed;

		public float Age;

		private readonly MeshInstance[] _chutes = new MeshInstance[3];

		private bool _puffed;

		private static readonly float[,] Spots = new float[3, 2]
		{
			{ -0.9f, 0.3f },
			{ 0.8f, -0.1f },
			{ -0.3f, -0.8f }
		};

		public bool Finished => Age > 6f;

		public CrabLandFx(float x, float y, float facing, uint seed)
		{
			X = x;
			Y = y;
			Facing = facing;
			Seed = seed;
		}

		public void Step(float dt, FxSink sink, FxParticles parts, MeshModel model)
		{
			Age += dt;
			if (!_puffed && parts != null)
			{
				_puffed = true;
				float num = ((sink.Ground != null) ? sink.Ground(X, Y) : 0f);
				for (int i = 0; i < 10; i++)
				{
					float num2 = PowerFx.Rand(Seed, i) * 6.2832f;
					float num3 = 1.2f + 1.5f * PowerFx.Rand(Seed, i + 1);
					parts.Smoke(new Vec3(X, Y, num + 0.3f), new Vec3((float)Math.Cos(num2) * num3, (float)Math.Sin(num2) * num3, 0.4f), 1.4f, 0.6f, 1.7f, TerrainCache.Rgb(170, 154, 128), TerrainCache.Rgb(196, 186, 168), 150, i, 1.5f);
				}
			}
			if (model == null)
			{
				return;
			}
			int num4 = model.PartIndex("chute");
			if (num4 < 0)
			{
				return;
			}
			ulong num5 = 0uL;
			for (int j = 0; j < model.Parts.Length; j++)
			{
				if (j != num4)
				{
					num5 |= (ulong)(1L << j);
				}
			}
			float num6 = CrabBombFx.Height / Math.Max(0.2f, model.Max.Z - model.Min.Z);
			float num7 = BlastFx.Smooth(Age / 1.5f);
			float num8 = 1f - BlastFx.Clamp01((Age - 4f) / 2f);
			for (int k = 0; k < 3; k++)
			{
				object obj = _chutes[k];
				if (obj == null)
				{
					MeshInstance[] chutes = _chutes;
					int num9 = k;
					MeshInstance obj2 = new MeshInstance
					{
						NoShadow = true,
						DissolveScale = 0.08f
					};
					MeshInstance meshInstance = obj2;
					chutes[num9] = obj2;
					obj = meshInstance;
				}
				MeshInstance meshInstance2 = (MeshInstance)obj;
				float num10 = (float)Math.Cos(Facing);
				float num11 = (float)Math.Sin(Facing);
				float num12 = X + num10 * Spots[k, 0] - num11 * Spots[k, 1];
				float num13 = Y + num11 * Spots[k, 0] + num10 * Spots[k, 1];
				float num14 = ((sink.Ground != null) ? sink.Ground(num12, num13) : 0f);
				float a = PowerFx.Rand(Seed, k + 10) * 6.2832f;
				Mat3 b = new Mat3(new Vec3(1f + 0.35f * num7, 0f, 0f), new Vec3(0f, 1f + 0.35f * num7, 0f), new Vec3(0f, 0f, 1f - 0.82f * num7));
				Mat3 basis = Mat3.RotZ(a) * Mat3.RotX(0.5f * num7) * b * num6;
				meshInstance2.Model = model;
				meshInstance2.Hidden = num5;
				meshInstance2.Basis = basis;
				float z = 0.62f;
				meshInstance2.Position = new Vec3(num12, num13, num14 + 0.05f) - basis.Mul(new Vec3(0f, 0f, z)) + new Vec3(0f, 0f, (1f - num7) * 0.9f);
				meshInstance2.Fade = num8;
				meshInstance2.SortDepth = sink.Distance(num12, num13);
				if (num8 > 0.01f)
				{
					sink.AddMesh(meshInstance2);
				}
			}
		}
	}
}
