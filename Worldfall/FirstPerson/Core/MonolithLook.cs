using System;

namespace FirstPerson.Core
{
	public static class MonolithLook
	{
		private static readonly uint RuneDay = TerrainCache.Rgb(28, 34, 44);

		private static readonly uint RuneNight = TerrainCache.Rgb(86, 172, 232);

		private static readonly uint RuneWake = TerrainCache.Rgb(206, 242, 255);

		private static readonly uint White = TerrainCache.Rgb(255, 255, 255);

		private static readonly uint Soil = TerrainCache.Rgb(120, 92, 66);

		public static void Apply(MeshModel model, uint[] palette, float night, float wake, uint wakeColor, uint ground)
		{
			if (model == null || palette == null)
			{
				return;
			}
			int c = ((ground == 0) ? ((int)TerrainCache.Rgb(104, 160, 66)) : ((int)ground | -16777216));
			uint num = TerrainCache.Lerp(TerrainCache.Scale((uint)c, 0.9f), Soil, 0.78f);
			uint num2 = TerrainCache.Scale((uint)c, 1.15f);
			float num3 = ((night <= 0.08f) ? 0f : Math.Min(1f, (night - 0.08f) * 1.6f));
			uint num4 = ((wakeColor == 0) ? RuneWake : TerrainCache.Lerp(wakeColor | 0xFF000000u, White, 0.5f));
			uint num5 = TerrainCache.Lerp(TerrainCache.Lerp(RuneDay, RuneNight, num3 * 0.85f), num4, Math.Min(1f, wake));
			string[] slotNames = model.SlotNames;
			for (int i = 0; i < slotNames.Length && i < palette.Length; i++)
			{
				switch (slotNames[i])
				{
				case "rune":
					palette[i] = num5;
					break;
				case "slab":
				case "sheen":
					palette[i] = ((wake > 0f) ? TerrainCache.Lerp(model.SlotColor[i] | 0xFF000000u, (wakeColor == 0) ? num4 : (wakeColor | 0xFF000000u), 0.15f * Math.Min(1f, wake)) : 0u);
					break;
				case "earth":
					palette[i] = num;
					break;
				case "earth_edge":
					palette[i] = num2;
					break;
				case "crack":
					palette[i] = TerrainCache.Scale(num, 0.42f);
					break;
				case "scorch":
					palette[i] = TerrainCache.Scale(num, 0.6f);
					break;
				}
			}
		}
	}
}
