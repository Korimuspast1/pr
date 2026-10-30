using System;

namespace FirstPerson.Core
{
	public static class CurtainMesh
	{
		public static readonly float[] Steps = new float[6] { 1f, 0.8f, 0.62f, 0.46f, 0.33f, 0.22f };

		public const float Gathered = 0.22f;

		private const float Fullness = 1.18f;

		private static MeshModel[] _room;

		private static MeshModel[] _street;

		private static MeshModel _rod;

		public static int Step(float k, out float stretch)
		{
			int num = 0;
			for (int i = 1; i < Steps.Length; i++)
			{
				if (Math.Abs(Steps[i] - k) < Math.Abs(Steps[num] - k))
				{
					num = i;
				}
			}
			stretch = k / Steps[num];
			return num;
		}

		public static MeshModel Room(int step)
		{
			if (_room == null)
			{
				MeshModel[] array = new MeshModel[Steps.Length];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = Panel("curtain_room_" + i, Steps[i], 7, 6, 4, 0);
				}
				_room = array;
			}
			return _room[Math.Max(0, Math.Min(step, _room.Length - 1))];
		}

		public static MeshModel Street(int step)
		{
			if (_street == null)
			{
				MeshModel[] array = new MeshModel[Steps.Length];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = Panel("curtain_street_" + i, Steps[i], 4, 3, 2, 2);
				}
				_street = array;
			}
			return _street[Math.Max(0, Math.Min(step, _street.Length - 1))];
		}

		private static MeshModel Panel(string name, float k, int pleats, int perPleat, int rows, byte flags)
		{
			MeshBuilder meshBuilder = new MeshBuilder(name);
			int slot = meshBuilder.Slot("cloth", 7, TerrainCache.Rgb(200, 182, 150), flags);
			float num = k / (2f * (float)pleats);
			float num2 = 1.18f / (2f * (float)pleats);
			float num3 = (float)Math.Sqrt(Math.Max(0f, num2 * num2 - num * num)) * 0.5f;
			int num4 = pleats * perPleat + 1;
			Vec3[,] array = new Vec3[rows, num4];
			Vec3[,] array2 = new Vec3[rows, num4];
			Random random = new Random(7177);
			float[] array3 = new float[pleats * 2 + 1];
			for (int i = 0; i < array3.Length; i++)
			{
				array3[i] = 0.6f + 0.8f * (float)random.NextDouble();
			}
			for (int j = 0; j < rows; j++)
			{
				float z = (float)(-j) / (float)(rows - 1);
				float num5 = 1f + 0.25f * (float)j / (float)(rows - 1);
				for (int l = 0; l < num4; l++)
				{
					float x = k * (float)l / (float)(num4 - 1);
					float num6 = 2f * (float)pleats * (float)l / (float)(num4 - 1);
					float y = num3 * num5 * array3[Math.Min(array3.Length - 1, (int)num6)] * (float)Math.Sin(Math.PI * (double)num6);
					array[j, l] = new Vec3(x, y, z);
					array2[j, num4 - 1 - l] = new Vec3(x, y, z);
				}
			}
			meshBuilder.Sheet(slot, array);
			meshBuilder.Sheet(slot, array2);
			return meshBuilder.Build();
		}

		public static MeshModel Rod(float toWall)
		{
			if (_rod != null)
			{
				return _rod;
			}
			MeshBuilder meshBuilder = new MeshBuilder("curtain_rod");
			int slot = meshBuilder.Slot("rod", 3, TerrainCache.Rgb(92, 64, 40), 0);
			meshBuilder.Tube(slot, new Vec3(0f, 0f, 0f), new Vec3(1f, 0f, 0f), 0.011f, 0.011f, 6);
			meshBuilder.Blob(slot, new Vec3(-0.012f, 0f, 0f), 0.014f, 0.02f, 0.02f, 6);
			meshBuilder.Blob(slot, new Vec3(1.012f, 0f, 0f), 0.014f, 0.02f, 0.02f, 6);
			float[] array = new float[2] { 0.06f, 0.94f };
			foreach (float x in array)
			{
				meshBuilder.Box(slot, new Vec3(x, (0f - toWall) * 0.5f, 0.012f), 0.006f, toWall * 0.5f, 0.008f);
			}
			_rod = meshBuilder.Build();
			return _rod;
		}
	}
}
