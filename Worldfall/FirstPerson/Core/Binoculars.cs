using System;

namespace FirstPerson.Core
{
	public sealed class Binoculars
	{
		public static readonly float[] Powers = new float[6] { 2f, 3f, 4f, 6f, 8f, 12f };

		public const int DefaultPower = 2;

		public const float RaiseSeconds = 0.45f;

		public const float LowerSeconds = 0.3f;

		public const float HoldSeconds = 0.3f;

		public const float HandsTo = 0.6f;

		public const float DarkFrom = 0.42f;

		public const float DarkTo = 0.62f;

		public const float OpenTo = 0.86f;

		private float _shownPower = Powers[2];

		private bool _held;

		private float _heldFor;

		public const float LensRadius = 0.47f;

		public const float LensApart = 0.29f;

		public bool Up { get; private set; }

		public float Raise { get; private set; }

		public int Power { get; private set; } = 2;

		public bool Active => Raise > 0f;

		public float Magnification => 1f + (_shownPower - 1f) * Smooth(0.62f, 1f, Raise);

		public float Lens
		{
			get
			{
				if (!(Raise < 0.62f))
				{
					return 0.5f + 0.5f * Smooth(0.62f, 0.86f, Raise);
				}
				return 0.5f * Smooth(0.42f, 0.62f, Raise);
			}
		}

		public float Lift => Math.Min(1f, Raise / 0.6f);

		public bool HandsShown
		{
			get
			{
				if (Raise > 0f)
				{
					return Raise < 0.62f;
				}
				return false;
			}
		}

		public float Looking => Smooth(0.8f, 1f, Raise);

		public float ChosenPower => Powers[Power];

		public void Press()
		{
			if (Up && !_held)
			{
				Up = false;
				return;
			}
			Up = true;
			_held = true;
			_heldFor = 0f;
		}

		public void Release()
		{
			if (_held)
			{
				_held = false;
				if (_heldFor > 0.3f)
				{
					Up = false;
				}
			}
		}

		public void Lower()
		{
			Up = false;
			_held = false;
		}

		public void Drop()
		{
			Lower();
			Raise = 0f;
		}

		public bool Step(int notches)
		{
			int num = Math.Max(0, Math.Min(Powers.Length - 1, Power + notches));
			if (num == Power)
			{
				return false;
			}
			Power = num;
			return true;
		}

		public void Update(float dt)
		{
			if (dt < 0f)
			{
				dt = 0f;
			}
			if (_held)
			{
				_heldFor += dt;
			}
			Raise = (Up ? Math.Min(1f, Raise + dt / 0.45f) : Math.Max(0f, Raise - dt / 0.3f));
			float num = Powers[Power];
			float num2 = 1f - (float)Math.Exp(-12f * dt);
			_shownPower = (float)Math.Exp(Math.Log(_shownPower) + (Math.Log(num) - Math.Log(_shownPower)) * (double)num2);
			if (Math.Abs(_shownPower - num) < 0.01f)
			{
				_shownPower = num;
			}
		}

		public static float Fov(float fov, float magnification)
		{
			if (!(magnification <= 1f))
			{
				return 2f * (float)Math.Atan(Math.Tan(fov * 0.5f) / (double)magnification);
			}
			return fov;
		}

		private static float Smooth(float a, float b, float x)
		{
			float num = (x - a) / (b - a);
			num = ((num < 0f) ? 0f : ((num > 1f) ? 1f : num));
			return num * num * (3f - 2f * num);
		}

		public static float Shade(float u, float v, float lens)
		{
			if (lens <= 0f)
			{
				return 0f;
			}
			float num = lens * 2f - 1f;
			if (num <= 0f)
			{
				return lens * 2f;
			}
			if (num > 1f)
			{
				num = 1f;
			}
			float num2 = 0.45f + 0.55f * num;
			float num3 = 0.47f * num2;
			float num4 = 0.29f * num2;
			float num5 = Math.Abs(u) - num4;
			float x = (float)Math.Sqrt(num5 * num5 + v * v) - num3;
			float val = Smooth(-0.012f, 0.0048f, x);
			float val2 = 0.38f * Smooth(-0.11f, 0f, x);
			float num6 = Math.Max(val, val2);
			return 1f - (1f - num6) * num;
		}
	}
}
