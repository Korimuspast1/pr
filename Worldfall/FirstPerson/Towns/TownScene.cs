using System.Collections.Generic;
using FirstPerson.Core;

namespace FirstPerson.Towns
{
	internal sealed class TownScene
	{
		public SceneCollector Collector;

		public ViewState View;

		public TerrainCache Terrain;

		public SpriteCache Sprites;

		public Actor Host;

		public float Dt;

		public float Clock;

		public float Night;

		public int Frame;

		public bool FromSky;

		public List<MeshInstance> Meshes => Collector.Meshes;

		public List<Billboard> Billboards => Collector.Billboards;

		public List<PointLight> Lights => Collector.Lights;

		public float Ground(float x, float y)
		{
			return Terrain.SampleHeight(x, y);
		}

		public bool InView(float x, float y, float radius)
		{
			return Collector.TownsInView(x - View.X, y - View.Y, radius);
		}

		public float Dist2(float x, float y)
		{
			float num = x - View.X;
			float num2 = y - View.Y;
			return num * num + num2 * num2;
		}
	}
}
