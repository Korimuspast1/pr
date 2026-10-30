using System;

namespace FirstPerson.Core
{
	public static class WalkCycle
	{
		public static class Tune
		{
			public static float StepBase = 0.26f;

			public static float StepPerSpeed = 0.17f;

			public static float StepMin = 0.14f;

			public static float StepWalkMax = 0.52f;

			public static float RobeStep = 0.55f;

			public static float RunFrom = 1.5f;

			public static float RunTo = 1.9f;

			public static float RunStepBase = 0.6f;

			public static float StepPerRun = 0.2f;

			public static float RunStepMax = 0.9f;

			public static float DutyWalk = 0.58f;

			public static float DutyRun = 0.38f;

			public static float HipFollow = 0.75f;

			public static float Leg = 0.47f;

			public static float SwingMax = 0.62f;

			public static float TurnPerStep = 50f;

			public static float TurnFrom = 40f;

			public static float TurnCadenceMax = 2.4f;

			public static float TurnLift = 0.3f;

			public static float TwistUpTo = 0.45f;

			public static float Settle = 2.2f;

			public static float Rise = 0.07f;

			public static float Fall = 0.14f;

			public static float MinPace = 0.04f;
		}

		public sealed class Walker
		{
			public float Phase;

			public float Stride;

			public float Run;

			public float Lift;

			public float TwistL;

			public float TwistR;

			public Foot FootL;

			public Foot FootR;

			private float _yawL;

			private float _yawR;

			private bool _downL;

			private bool _downR;

			private bool _have;

			public static float MaxTwist = 0.7f;

			public static float Return = 0.1f;

			private float _twistL;

			private float _twistR;

			public void Step(float speed, float body, float turn, float facing, float dt, float leg = 0f, bool robe = false)
			{
				float angle;
				if (!_have)
				{
					angle = (_yawL = (_yawR = facing));
					_have = true;
				}
				if (dt <= 0f)
				{
					return;
				}
				body = Math.Max(0.1f, body);
				if (leg <= 0.05f)
				{
					leg = Tune.Leg;
				}
				float num = Math.Max(0f, speed) / body;
				float num2 = Math.Abs(turn) * 57.29578f;
				float num3 = 0f;
				float num4 = 0f;
				float run = 0f;
				float num5 = 0f;
				float num6 = Clamp(leg / Tune.Leg, 0.6f, 1.2f);
				if (num > Tune.MinPace)
				{
					num4 = num / (StepLength(num, out run) * num6 * (robe ? Tune.RobeStep : 1f));
				}
				if (num2 > Tune.TurnFrom)
				{
					float num7 = Math.Min((num2 - 0.5f * Tune.TurnFrom) / Tune.TurnPerStep, Tune.TurnCadenceMax);
					if (num7 > num4)
					{
						num4 = num7;
						num5 = Tune.TurnLift;
					}
				}
				if (num > Tune.MinPace)
				{
					float num8 = num / num4;
					num3 = Math.Min((float)Math.Asin(Clamp(Duty(run) * num8 / leg, 0f, 0.95f)), Tune.SwingMax) / 0.5f;
				}
				float num9 = ((num3 > Stride) ? Tune.Rise : Tune.Fall);
				Stride += (num3 - Stride) * (1f - (float)Math.Exp((0f - dt) / num9));
				Lift += (num5 - Lift) * (1f - (float)Math.Exp((0f - dt) / ((num5 > Lift) ? Tune.Rise : Tune.Fall)));
				Run += (run - Run) * (1f - (float)Math.Exp((0f - dt) / 0.25f));
				float num10 = MathF.PI * num4 * dt;
				if (num3 < 0.05f && num5 <= 0f && (Stride > 0.02f || Lift > 0.02f))
				{
					float val = MathF.PI * Tune.Settle * dt;
					float num11 = (float)(Math.Ceiling((double)Phase / Math.PI - 0.0001) * Math.PI);
					num10 = Math.Max(num10, Math.Min(val, num11 - Phase));
				}
				Phase += num10;
				if (Phase > 1000f)
				{
					Phase -= MathF.PI * 318f;
				}
				float duty = Duty(Run);
				float lift;
				bool down = Leg(Phase, left: true, duty, out angle, out lift);
				bool down2 = Leg(Phase, left: false, duty, out lift, out angle);
				_twistL = Hold(down, ref _downL, ref _yawL, _twistL, facing, dt);
				_twistR = Hold(down2, ref _downR, ref _yawR, _twistR, facing, dt);
				float num12 = Clamp(1f - Stride / Tune.TwistUpTo, 0f, 1f);
				TwistL = _twistL * num12;
				TwistR = _twistR * num12;
			}

			public void Unplant()
			{
				FootL.Down = (FootR.Down = false);
			}

			public void Reset(float facing)
			{
				Lift = (TwistL = (TwistR = (_twistL = (_twistR = 0f))));
				_yawL = (_yawR = facing);
				_downL = (_downR = false);
				FootL.Down = (FootR.Down = false);
				_have = true;
			}

			private static float Hold(bool down, ref bool wasDown, ref float yaw, float twist, float facing, float dt)
			{
				if (down && !wasDown)
				{
					yaw = facing + twist;
				}
				wasDown = down;
				if (down)
				{
					return Clamp(Wrap(yaw - facing), 0f - MaxTwist, MaxTwist);
				}
				return twist * (float)Math.Exp((0f - dt) / Return);
			}

			private static float Wrap(float a)
			{
				while ((double)a > Math.PI)
				{
					a -= MathF.PI * 2f;
				}
				while ((double)a < -Math.PI)
				{
					a += MathF.PI * 2f;
				}
				return a;
			}
		}

		public struct Foot
		{
			public float X;

			public float Y;

			public bool Down;
		}

		public const float SwingPerStride = 0.5f;

		public static float StepLength(float pace, out float run)
		{
			run = Smooth((pace - Tune.RunFrom) / Math.Max(0.001f, Tune.RunTo - Tune.RunFrom));
			float num = Clamp(Tune.StepBase + Tune.StepPerSpeed * pace, Tune.StepMin, Tune.StepWalkMax);
			float num2 = Clamp(Tune.RunStepBase + Tune.StepPerRun * Math.Max(0f, pace - Tune.RunFrom), Tune.RunStepBase, Tune.RunStepMax);
			return num + (num2 - num) * run;
		}

		public static float Duty(float run)
		{
			return Tune.DutyWalk + (Tune.DutyRun - Tune.DutyWalk) * Clamp(run, 0f, 1f);
		}

		public static bool Leg(float phase, bool left, float duty, out float angle, out float lift)
		{
			float num = (phase - MathF.PI / 2f) / (MathF.PI * 2f) + (left ? 0f : 0.5f);
			num -= (float)Math.Floor(num);
			duty = Clamp(duty, 0.2f, 0.8f);
			if (num < duty)
			{
				angle = 1f - 2f * num / duty;
				lift = 0f;
				return true;
			}
			float num2 = (num - duty) / (1f - duty);
			angle = -1f + 2f * num2 * num2 * (3f - 2f * num2);
			lift = (float)Math.Sin(Math.PI * Math.Pow(num2, 0.7));
			return false;
		}

		private static float Clamp(float v, float lo, float hi)
		{
			if (!(v < lo))
			{
				if (!(v > hi))
				{
					return v;
				}
				return hi;
			}
			return lo;
		}

		private static float Smooth(float t)
		{
			t = Clamp(t, 0f, 1f);
			return t * t * (3f - 2f * t);
		}
	}
}
