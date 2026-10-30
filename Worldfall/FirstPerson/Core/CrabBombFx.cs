using System;
using System.Globalization;

namespace FirstPerson.Core
{
	public static class CrabBombFx
	{
		private static readonly float[,] Cluster = new float[3, 2]
		{
			{ -0.9f, 0.55f },
			{ 0.75f, 0.15f },
			{ -0.55f, -0.75f }
		};

		public static float Height = 1.9f;

		public static void DrawFalling(FxSink sink, MeshModel model, MeshInstance[] pool, float x, float y, float z, float facing, float time, uint seed, float size = 1f)
		{
			if (model == null || pool == null)
			{
				return;
			}
			float num = Height * size / Math.Max(0.2f, model.Max.Z - model.Min.Z);
			Vec3 v = HangPoint(model);
			for (int i = 0; i < 3 && i < pool.Length; i++)
			{
				object obj = pool[i];
				if (obj == null)
				{
					int num2 = i;
					MeshInstance obj2 = new MeshInstance
					{
						NoShadow = false
					};
					MeshInstance meshInstance = obj2;
					pool[num2] = obj2;
					obj = meshInstance;
				}
				MeshInstance meshInstance2 = (MeshInstance)obj;
				float num3 = PowerFx.Rand(seed, i) * 6.2832f;
				float num4 = Cluster[i, 0] * size;
				float num5 = Cluster[i, 1] * size;
				float num6 = (float)Math.Cos(facing);
				float num7 = (float)Math.Sin(facing);
				float x2 = x + num6 * num4 + (float)Math.Sin(time * 0.7f + num3) * 0.12f * size;
				float y2 = y + num7 * num4 + (float)Math.Cos(time * 0.6f + num3) * 0.12f * size;
				float num8 = z + (num5 + 0.8f) * size + (float)Math.Sin(time * 1.3f + num3) * 0.08f * size;
				Mat3 b = Mat3.RotX((float)Math.Sin(time * 1.7f + num3) * 0.2f) * Mat3.RotY((float)Math.Sin(time * 1.3f + num3 * 1.7f) * 0.16f);
				Mat3 basis = Mat3.RotZ(facing + 1.5708f + (float)Math.Sin(time * 0.4f + num3) * 0.5f) * b * num;
				Vec3 vec = new Vec3(x2, y2, num8 + v.Z * num);
				meshInstance2.Model = model;
				meshInstance2.Basis = basis;
				meshInstance2.Position = vec - basis.Mul(v);
				Waggle(meshInstance2, model, time, num3, 1f);
				meshInstance2.SortDepth = sink.Distance(x2, y2);
				sink.AddMesh(meshInstance2);
			}
		}

		private static Vec3 HangPoint(MeshModel model)
		{
			string text = model.Info("chute");
			float z = model.Max.Z;
			if (text != null)
			{
				string[] array = text.Split(',');
				if (array.Length == 3 && float.TryParse(array[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
				{
					z = result;
				}
			}
			return new Vec3(0f, 0f, z);
		}

		private static void Waggle(MeshInstance inst, MeshModel model, float time, float phase, float wild)
		{
			int num = model.Parts.Length;
			if (inst.PartRotation == null || inst.PartRotation.Length != num)
			{
				inst.PartRotation = new Mat3[num];
			}
			for (int i = 0; i < num; i++)
			{
				inst.PartRotation[i] = Mat3.Identity;
			}
			int num2 = model.PartIndex("legs_l");
			int num3 = model.PartIndex("legs_r");
			int num4 = model.PartIndex("claw_l");
			int num5 = model.PartIndex("claw_r");
			float a = (float)Math.Sin(time * 9f + phase) * 0.35f * wild;
			float num6 = (float)Math.Sin(time * 5f + phase * 1.3f) * 0.3f * wild;
			if (num2 >= 0)
			{
				inst.PartRotation[num2] = Mat3.RotY(a);
			}
			if (num3 >= 0)
			{
				inst.PartRotation[num3] = Mat3.RotY(a);
			}
			if (num4 >= 0)
			{
				inst.PartRotation[num4] = Mat3.RotZ(num6) * Mat3.RotX((0f - num6) * 0.6f);
			}
			if (num5 >= 0)
			{
				inst.PartRotation[num5] = Mat3.RotZ(0f - num6) * Mat3.RotX((0f - num6) * 0.6f);
			}
		}

		public static void DrawShrapnel(FxSink sink, FxParticles parts, MeshModel model, MeshInstance inst, Vec3 at, float tumble, float heading, float time, uint seed, float dt, float size = 1f)
		{
			if (model != null && inst != null)
			{
				float num = 0.95f * size / Math.Max(0.2f, model.Max.X - model.Min.X);
				Mat3 basis = Mat3.RotZ(heading) * Mat3.RotX(tumble) * num;
				Vec3 v = new Vec3((model.Min.X + model.Max.X) * 0.5f, (model.Min.Y + model.Max.Y) * 0.5f, (model.Min.Z + model.Max.Z) * 0.5f);
				inst.Model = model;
				inst.Basis = basis;
				inst.Position = at - basis.Mul(v);
				Waggle(inst, model, time * 2.2f, PowerFx.Rand(seed, 3) * 6f, 1.4f);
				inst.SortDepth = sink.Distance(at.X, at.Y);
				sink.AddMesh(inst);
				Vec3 at2 = inst.Position + basis.Mul(new Vec3(0f, 0.12f, 0.2f));
				float num2 = 0.7f + 0.3f * (float)Math.Sin(time * 47f + (float)seed);
				sink.Glow(at2.X, at2.Y, at2.Z, 0.55f * size * num2, TerrainCache.Rgb(255, 220, 120), 0.9f);
				sink.Light(at2.X, at2.Y, at2.Z, 3f, TerrainCache.Rgb(255, 170, 80), 0.6f * num2);
				if (parts != null && dt > 0f && PowerFx.Rand(seed, (int)(time * 60f)) < 0.7f)
				{
					parts.Spark(at2, new Vec3((PowerFx.Rand(seed, (int)(time * 97f)) - 0.5f) * 3f, (PowerFx.Rand(seed, (int)(time * 89f)) - 0.5f) * 3f, 2f), 0.4f, 0.08f, TerrainCache.Rgb(255, 240, 180), BlastFx.Heat(0.6f), 6f, 0.5f);
					parts.Smoke(at, new Vec3(0f, 0f, 0.3f), 0.9f, 0.25f * size, 0.8f * size, TerrainCache.Rgb(110, 104, 104), TerrainCache.Rgb(170, 166, 162), 90, (int)(time * 13f), 1f);
				}
			}
		}
	}
}
