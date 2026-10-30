using UnityEngine;

namespace FirstPerson.Gpu
{
	internal struct InstData
	{
		public Matrix4x4 M;

		public Vector4 Flash;

		public Vector4 Mult;

		public Vector4 Misc;

		public Vector4 Tag;

		public Vector4 Ground;

		public Vector4 Pal;

		public const int Size = 160;
	}
}
