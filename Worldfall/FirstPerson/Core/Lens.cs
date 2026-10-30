using System;
using System.Runtime.CompilerServices;

namespace FirstPerson.Core
{
	public struct Lens
	{
		public float Tilt;

		public float Cq;

		public float Sq;

		public float Shift;

		public bool Steep;

		public const float MaxElevation = MathF.PI * 7f / 15f;

		public bool Tilted => Sq != 0f;

		public static Lens Of(in ViewState view, float aspect)
		{
			float num = (float)Math.Atan(view.Pitch);
			float num2 = 1f - ((view.Shear < 0f) ? 0f : ((view.Shear > 1f) ? 1f : view.Shear));
			float num3 = num * num2;
			bool steep = false;
			if (num3 != 0f)
			{
				float tv = (float)Math.Tan(view.Fov * 0.5f) * aspect;
				if (!Fits(num3, num, tv))
				{
					if (view.Free)
					{
						steep = true;
					}
					else
					{
						float num4 = 0f;
						float num5 = num3;
						for (int i = 0; i < 18; i++)
						{
							float num6 = (num4 + num5) * 0.5f;
							if (Fits(num6, num, tv))
							{
								num4 = num6;
							}
							else
							{
								num5 = num6;
							}
						}
						num3 = num4;
					}
				}
			}
			if (Math.Abs(num3) < 1E-05f)
			{
				num3 = 0f;
			}
			return new Lens
			{
				Tilt = num3,
				Steep = steep,
				Cq = ((num3 == 0f) ? 1f : ((float)Math.Cos(num3))),
				Sq = ((num3 == 0f) ? 0f : ((float)Math.Sin(num3))),
				Shift = ((num3 == 0f) ? view.Pitch : ((float)Math.Tan(num - num3)))
			};
		}

		public static float FullTiltDegrees(float fov, float aspect)
		{
			return (float)((1.4660766124725342 - Math.Atan(Math.Tan(fov * 0.5f) * (double)aspect)) * 180.0 / Math.PI);
		}

		private static bool Fits(float tilt, float look, float tv)
		{
			float num = (float)Math.Tan(look - tilt);
			float num2 = tilt + (float)Math.Atan(num + tv);
			float num3 = tilt + (float)Math.Atan(num - tv);
			if (num2 <= MathF.PI * 7f / 15f)
			{
				return num3 >= MathF.PI * -7f / 15f;
			}
			return false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public float SlopeOf(float v)
		{
			float num = Cq - v * Sq;
			if (num <= 1E-06f)
			{
				if (!(v > 0f))
				{
					return -1E+09f;
				}
				return 1E+09f;
			}
			return (Sq + v * Cq) / num;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public float RowOfSlope(float k)
		{
			float num = Cq + k * Sq;
			if (num <= 1E-06f)
			{
				if (!(k > 0f))
				{
					return -1E+09f;
				}
				return 1E+09f;
			}
			return (k * Cq - Sq) / num;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ToCamera(float ahead, float up, out float depth, out float height)
		{
			depth = ahead * Cq + up * Sq;
			height = up * Cq - ahead * Sq;
		}

		public float WidestTan(float fov, float aspect)
		{
			float num = (float)Math.Tan(fov * 0.5f);
			if (!Tilted)
			{
				return num;
			}
			float num2 = num * aspect;
			float num3 = Math.Min(Cq - (Shift + num2) * Sq, Cq - (Shift - num2) * Sq);
			if (!(num3 > 0.001f))
			{
				return num * 1000f;
			}
			return num / num3;
		}
	}
}
