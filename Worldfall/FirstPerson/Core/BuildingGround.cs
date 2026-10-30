using System;

namespace FirstPerson.Core
{
	public static class BuildingGround
	{
		public static float Base(float lowest, float atDoor, float middle, float footing, float scale)
		{
			if (footing <= 0f)
			{
				return lowest;
			}
			float num = Math.Max(atDoor, middle - 0.25f);
			float num2 = lowest + footing * scale * 0.85f;
			if (!(num < lowest))
			{
				if (!(num > num2))
				{
					return num;
				}
				return num2;
			}
			return lowest;
		}

		public static float Pitch(float lowest, float middle)
		{
			return Math.Min(middle, lowest + 0.3f);
		}
	}
}
