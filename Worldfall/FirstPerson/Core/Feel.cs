using System;

namespace FirstPerson.Core
{
	public struct Feel
	{
		public float Start;

		public float Build;

		public float Stop;

		public float Plant;

		public float Turn;

		public float Air;

		public static readonly Feel Snappy = new Feel
		{
			Start = 0.15f,
			Build = 0.5f,
			Stop = 0.12f,
			Plant = 0.18f,
			Turn = 0.08f,
			Air = 0.15f
		};

		public bool Instant => Start <= 0f;

		public static Feel Of(float weight)
		{
			float num = Math.Max(0.6f, Math.Min(1.5f, 1f + 0.6f * (weight - 1f)));
			Feel snappy = Snappy;
			snappy.Start *= num;
			snappy.Build *= num;
			snappy.Stop *= num;
			snappy.Plant *= num;
			snappy.Turn *= num;
			return snappy;
		}
	}
}
