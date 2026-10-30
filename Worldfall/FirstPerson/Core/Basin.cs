using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class Basin
	{
		public readonly List<int> Tiles = new List<int>();

		public readonly List<float> Beds = new List<float>();

		public float Floor;

		public float Rim;

		public int Spill = -1;

		public int Out = -1;

		public float Depth => Rim - Floor;

		public int Count => Tiles.Count;

		public float VolumeAt(float level)
		{
			float num = 0f;
			for (int i = 0; i < Beds.Count && Beds[i] < level; i++)
			{
				num += level - Beds[i];
			}
			return num;
		}

		public float LevelFor(float volume)
		{
			if (volume <= 0f || Beds.Count == 0)
			{
				return Floor;
			}
			float num = 0f;
			for (int i = 0; i < Beds.Count; i++)
			{
				float num2 = ((i + 1 < Beds.Count) ? Math.Min(Beds[i + 1], Rim) : Rim);
				float num3 = num + Beds[i];
				float num4 = (float)(i + 1) * num2 - num3;
				if (volume <= num4 || num2 >= Rim)
				{
					return Math.Min(Rim, (volume + num3) / (float)(i + 1));
				}
				num = num3;
			}
			return Rim;
		}
	}
}
