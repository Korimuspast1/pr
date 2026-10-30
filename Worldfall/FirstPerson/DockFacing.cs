using System;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal static class DockFacing
	{
		public static float Heading(Building b, float x, float y)
		{
			if (Facing.DockTurn(b, out var turns))
			{
				return Facing.Heading(turns);
			}
			return TowardsWater(x, y);
		}

		public static float Ground(TerrainCache t, float x, float y)
		{
			return Mathf.Max(0f, Mathf.Min(t.SampleHeight(x, y), 0.6f));
		}

		public static float TowardsWater(float x, float y)
		{
			float result = -MathF.PI / 2f;
			int num = int.MinValue;
			int[] array = new int[16];
			for (int i = 0; i < 16; i++)
			{
				float f = (float)i * MathF.PI / 8f;
				for (int j = 2; j <= 9; j++)
				{
					if (Wet(World.world.GetTile((int)(x + Mathf.Cos(f) * (float)j), (int)(y + Mathf.Sin(f) * (float)j))))
					{
						array[i] += 10 - j;
					}
				}
			}
			int[] array2 = new int[16];
			for (int k = 0; k < 16; k++)
			{
				array2[k] = array[k] - array[(k + 8) & 0xF];
				if (array2[k] > num)
				{
					num = array2[k];
					result = (float)k * MathF.PI / 8f;
				}
			}
			Vector2 zero = Vector2.zero;
			for (int l = 0; l < 16; l++)
			{
				if (array2[l] >= num - 2)
				{
					zero += new Vector2(Mathf.Cos((float)l * MathF.PI / 8f), Mathf.Sin((float)l * MathF.PI / 8f));
				}
			}
			if (zero.sqrMagnitude > 0.01f)
			{
				float num2 = Mathf.Atan2(zero.y, zero.x);
				float num3 = float.MaxValue;
				for (int m = 0; m < 16; m++)
				{
					if (array2[m] >= num - 2)
					{
						float num4 = Mathf.Abs(Mathf.DeltaAngle(num2 * 57.29578f, (float)m * 22.5f));
						if (num4 < num3)
						{
							num3 = num4;
							result = (float)m * MathF.PI / 8f;
						}
					}
				}
			}
			return result;
		}

		public static int QuarterOf(float heading)
		{
			return (Mathf.RoundToInt((heading + MathF.PI / 2f) / (MathF.PI / 2f)) % 4 + 4) % 4;
		}

		public static bool RunsOverWater(float x, float y, float heading, float width)
		{
			float num = Mathf.Cos(heading);
			float num2 = Mathf.Sin(heading);
			for (float num3 = 1f; num3 <= width * 1.15f; num3 += 0.5f)
			{
				if (!Wet(World.world.GetTile(Mathf.FloorToInt(x + num * num3), Mathf.FloorToInt(y + num2 * num3))))
				{
					return false;
				}
			}
			return true;
		}

		private static bool Wet(WorldTile t)
		{
			try
			{
				return t != null && t.Type != null && t.Type.liquid && !t.Type.lava;
			}
			catch
			{
				return false;
			}
		}
	}
}
