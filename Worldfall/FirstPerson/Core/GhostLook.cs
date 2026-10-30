namespace FirstPerson.Core
{
	public static class GhostLook
	{
		private static readonly uint Day = TerrainCache.Rgb(196, 222, 214);

		private static readonly uint Night = TerrainCache.Rgb(105, 140, 128);

		public static void Apply(MeshInstance inst, float ghostly, float night)
		{
			if (ghostly <= 0f)
			{
				inst.Fade = 1f;
				inst.NoShadow = false;
				inst.Emissive = false;
			}
			else
			{
				inst.Fade = ghostly;
				inst.NoShadow = true;
				inst.Emissive = true;
				float t = ((night < 0.3f) ? 0f : ((night > 0.8f) ? 1f : ((night - 0.3f) / 0.5f)));
				inst.Multiply = TerrainCache.Lerp(Day, Night, t);
			}
		}
	}
}
