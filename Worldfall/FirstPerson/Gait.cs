using System;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class Gait
	{
		public float Phase;

		public float Stride;

		public int Steps;

		public float Speed;

		public float Puff;

		public float Vertical;

		public float Lateral;

		public float Nod;

		private Vector2 _last;

		private bool _have;

		private float _breath;

		public void Reset()
		{
			_have = false;
			Stride = 0f;
			Speed = 0f;
			Puff = 0f;
			Vertical = (Lateral = (Nod = 0f));
		}

		public void Update(Vector2 pos, float dt, bool grounded, bool wantsToMove, float run, float walk, float body)
		{
			if (!(dt <= 0f))
			{
				if (!_have || (pos - _last).sqrMagnitude > 4f)
				{
					_last = pos;
					_have = true;
				}
				float magnitude = (pos - _last).magnitude;
				_last = pos;
				Speed = Mathf.Lerp(Speed, magnitude / dt, 1f - Mathf.Exp(-10f * dt));
				float num = ((grounded && wantsToMove) ? Mathf.Clamp01(Speed / Mathf.Max(0.05f, 0.35f * body)) : 0f);
				Stride = Mathf.MoveTowards(Stride, num, ((num > Stride) ? 5f : 6f) * dt);
				float num2 = Mathf.Clamp(1f / Mathf.Sqrt(Mathf.Max(0.3f, body)), 0.55f, 1.25f);
				float num3 = Mathf.Lerp(Mathf.Lerp(1.8f, 1.5f, walk), 3.1f, run) * num2 * (0.45f + 0.55f * Stride);
				float num4 = Phase;
				if (Stride > 0.001f)
				{
					Phase += dt * MathF.PI * num3;
				}
				else
				{
					float target = Mathf.Round(Phase / MathF.PI) * MathF.PI;
					Phase = Mathf.MoveTowards(Phase, target, dt * 3f);
				}
				if (Phase > 1000f)
				{
					float num5 = Mathf.Floor(Phase / (MathF.PI * 2f)) * 2f * MathF.PI;
					Phase -= num5;
					num4 -= num5;
				}
				if (Stride > 0.3f && Mathf.Floor((Phase - MathF.PI / 2f) / MathF.PI) != Mathf.Floor((num4 - MathF.PI / 2f) / MathF.PI))
				{
					Steps++;
				}
				float num6 = Mathf.Sin(Phase);
				float num7 = num6 * num6;
				float num8 = Mathf.Lerp(Mathf.Lerp(0.018f, 0.012f, walk), 0.045f, run) * body;
				Vertical = (0f - num8) * num7 * Stride;
				Lateral = Mathf.Lerp(Mathf.Lerp(0.011f, 0.008f, walk), 0.007f, run) * body * Mathf.Sin(Phase - 0.5f) * Stride;
				Nod = Mathf.Lerp(0.2f, 0.6f, run) * num7 * Stride;
				Puff = Mathf.Clamp01(Puff + (run * Stride * 0.25f - 0.12f * (1f - run * Stride)) * dt);
				_breath += dt * (1.7f + 1.1f * Puff);
				Vertical += 0.004f * body * (1f + 1.5f * Puff) * Mathf.Sin(_breath) * (1f - Stride) * (grounded ? 1f : 0f);
			}
		}
	}
}
