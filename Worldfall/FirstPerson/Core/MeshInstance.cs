using System;

namespace FirstPerson.Core
{
	public sealed class MeshInstance
	{
		public MeshModel Model;

		public Vec3 Position;

		public Mat3 Basis = Mat3.Identity;

		public Mat3[] PartRotation;

		public Vec3[] PartOffset;

		public float[] PartScale;

		public ulong Hidden;

		public uint[] Palette;

		public uint Flash;

		public uint Multiply;

		public int Tag = -1;

		public bool Emissive;

		public bool NoShadow;

		public bool Static;

		public bool ShadowOnly;

		public float Glow;

		public float Snow;

		public float Sway;

		public float SwayInvTop;

		public float Fade = 1f;

		public bool FadeKeepsShadow;

		public float DissolveScale;

		public CloudLook Cloud;

		public float SortDepth;

		public bool KeepSmall;

		internal float[] CachePos;

		internal float[] CacheNrm;

		internal MeshModel CacheModel;

		internal Vec3 CachePosition;

		internal Mat3 CacheBasis;

		public void Place(float x, float y, float z, float heading, float sx, float sy, float sz)
		{
			Position = new Vec3(x, y, z);
			float num = heading + MathF.PI / 2f;
			float num2 = (float)Math.Cos(num);
			float num3 = (float)Math.Sin(num);
			Basis = new Mat3(new Vec3(num2 * sx, num3 * sx, 0f), new Vec3((0f - num3) * sy, num2 * sy, 0f), new Vec3(0f, 0f, sz));
		}

		public void ResetPose()
		{
			if (PartRotation != null)
			{
				for (int i = 0; i < PartRotation.Length; i++)
				{
					PartRotation[i] = Mat3.Identity;
				}
			}
			if (PartOffset != null)
			{
				Array.Clear(PartOffset, 0, PartOffset.Length);
			}
			if (PartScale != null)
			{
				for (int j = 0; j < PartScale.Length; j++)
				{
					PartScale[j] = 1f;
				}
			}
		}

		public void EnsurePose()
		{
			int num = Model.Parts.Length;
			if (PartRotation == null || PartRotation.Length != num)
			{
				PartRotation = new Mat3[num];
				PartOffset = new Vec3[num];
				PartScale = new float[num];
				ResetPose();
			}
		}

		public void Hide(string part, bool hide = true)
		{
			int num = Model.PartIndex(part);
			if (num >= 0 && num < 64)
			{
				if (hide)
				{
					Hidden |= (ulong)(1L << num);
				}
				else
				{
					Hidden &= (ulong)(~(1L << num));
				}
			}
		}
	}
}
