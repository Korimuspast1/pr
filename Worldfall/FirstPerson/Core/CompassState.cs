using System;

namespace FirstPerson.Core
{
	public sealed class CompassState
	{
		public const float DipSeconds = 0.1f;

		public const float RaiseSeconds = 0.26f;

		public const float DropSeconds = 0.17f;

		public const float PopAt = 0.72f;

		public const float LidOpen = 1.95f;

		public const float LidStop = 2.2f;

		public float Target = float.NaN;

		public float Card;

		public float CardSpeed;

		public float Pointer;

		public float PointerSpeed;

		public float LastNorth = float.NaN;

		public float PointerOut;

		public Vec3 At;

		public bool Shown;

		private float _lidSpeed;

		private bool _released;

		public bool Out { get; private set; }

		public float Dip { get; private set; }

		public float Raise { get; private set; }

		public float Lid { get; private set; }

		public float PopKick { get; private set; }

		public float ShutKick { get; private set; }

		public bool Popped { get; private set; }

		public bool Shut { get; private set; }

		public bool Active
		{
			get
			{
				if (!(Raise > 0f))
				{
					return Dip > 0f;
				}
				return true;
			}
		}

		public bool Reading
		{
			get
			{
				if (Out && Raise >= 1f)
				{
					return Lid > 1f;
				}
				return false;
			}
		}

		public void Toggle()
		{
			if (Out)
			{
				PutAway();
			}
			else
			{
				Out = true;
			}
		}

		public void PutAway()
		{
			Out = false;
		}

		public void Drop()
		{
			Out = false;
			float num = (ShutKick = 0f);
			float num3 = (PopKick = num);
			float num5 = (Lid = num3);
			float dip = (Raise = num5);
			Dip = dip;
			_lidSpeed = 0f;
			_released = false;
			PointerOut = 0f;
			LastNorth = float.NaN;
			Shown = false;
		}

		public void Update(float dt)
		{
			bool popped = (Shut = false);
			Popped = popped;
			if (dt <= 0f)
			{
				return;
			}
			if (dt > 0.1f)
			{
				dt = 0.1f;
			}
			PopKick = Math.Max(0f, PopKick - dt * 5f);
			ShutKick = Math.Max(0f, ShutKick - dt * 7f);
			if (Out)
			{
				if (Dip < 1f)
				{
					Dip = Math.Min(1f, Dip + dt / 0.1f);
				}
				else
				{
					Raise = Math.Min(1f, Raise + dt / 0.26f);
				}
				if (!_released && Raise >= 0.72f)
				{
					_released = true;
					Popped = true;
					PopKick = 1f;
					_lidSpeed = 9f;
				}
			}
			else if (Lid > 0.001f || _released)
			{
				_released = false;
			}
			else
			{
				Raise = Math.Max(0f, Raise - dt / 0.17f);
				if (Raise <= 0f)
				{
					Dip = 0f;
					PointerOut = 0f;
				}
			}
			StepLid(dt);
		}

		private void StepLid(float dt)
		{
			int num = (int)Math.Ceiling(dt / 0.002f);
			float num2 = dt / (float)num;
			for (int i = 0; i < num; i++)
			{
				if (_released)
				{
					float num3 = 620f * (1.95f - Lid) - 19f * _lidSpeed;
					_lidSpeed += num3 * num2;
					Lid += _lidSpeed * num2;
					if (Lid > 2.2f)
					{
						Lid = 2.2f;
						if (_lidSpeed > 0f)
						{
							_lidSpeed *= -0.3f;
						}
					}
				}
				else if (Lid > 0f)
				{
					float num4 = -900f * Lid - 16f * _lidSpeed - 40f;
					_lidSpeed += num4 * num2;
					Lid += _lidSpeed * num2;
					if (Lid <= 0f)
					{
						bool flag = _lidSpeed < -4f;
						Lid = 0f;
						_lidSpeed = (flag ? ((0f - _lidSpeed) * 0.12f) : 0f);
						if (flag)
						{
							Shut = true;
							ShutKick = 1f;
						}
					}
				}
				else
				{
					_lidSpeed = 0f;
				}
			}
			if (!_released && Lid <= 0f && Math.Abs(_lidSpeed) < 0.5f)
			{
				Lid = 0f;
				_lidSpeed = 0f;
			}
		}
	}
}
