namespace FirstPerson.Core
{
	public static class BiomePicture
	{
		public static float Shimmer;

		public static float Mirage;

		public static float Warp;

		public static int Frame = -1;

		public static void Set(BiomeMix mix, int frame)
		{
			Shimmer = mix.Shimmer;
			Mirage = mix.Mirage;
			Warp = mix.Warp;
			Frame = frame;
		}
	}
}
