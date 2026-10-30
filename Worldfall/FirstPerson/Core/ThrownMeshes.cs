using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class ThrownMeshes
	{
		private static readonly Dictionary<string, MeshModel> Made = new Dictionary<string, MeshModel>();

		public static MeshModel Get(string name)
		{
			if (Made.TryGetValue(name, out var value))
			{
				return value;
			}
			if (!(name == "thrown_bone"))
			{
				if (!(name == "snowball"))
				{
					return null;
				}
				value = Snowball();
			}
			else
			{
				value = Bone();
			}
			Made[name] = value;
			return value;
		}

		private static MeshModel Bone()
		{
			MeshBuilder meshBuilder = new MeshBuilder("thrown_bone");
			int slot = meshBuilder.Slot("bone", 0, TerrainCache.Rgb(232, 224, 200), 0);
			meshBuilder.Part("main", -1, new Vec3(0f, 0f, 0f));
			meshBuilder.Tube(slot, new Vec3(0f, -0.36f, 0f), new Vec3(0f, 0.36f, 0f), 0.07f, 0.07f, 6);
			float[] array = new float[2] { -0.4f, 0.4f };
			foreach (float y in array)
			{
				float[] array2 = new float[2] { -0.08f, 0.08f };
				foreach (float x in array2)
				{
					meshBuilder.Blob(slot, new Vec3(x, y, 0f), 0.1f, 0.1f, 0.09f, 6);
				}
			}
			return meshBuilder.Build();
		}

		private static MeshModel Snowball()
		{
			MeshBuilder meshBuilder = new MeshBuilder("snowball");
			int slot = meshBuilder.Slot("snow", 0, TerrainCache.Rgb(236, 242, 250), 0);
			meshBuilder.Part("main", -1, new Vec3(0f, 0f, 0f));
			meshBuilder.Blob(slot, new Vec3(0f, 0f, 0f), 0.5f, 0.5f, 0.48f);
			return meshBuilder.Build();
		}
	}
}
