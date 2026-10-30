using System;

namespace FirstPerson.Core
{
	public static class Synth
	{
		public enum Ground
		{
			Grass,
			Sand,
			Soil,
			Rock,
			Snow,
			Wood,
			Shallows
		}

		public enum Swish
		{
			Fist,
			Light,
			Blade,
			Heavy,
			Thrust
		}

		private sealed class Noise
		{
			private uint _s;

			public Noise(int seed)
			{
				_s = (uint)((seed * -1640531535) | 1);
			}

			public float Next()
			{
				_s ^= _s << 13;
				_s ^= _s >> 17;
				_s ^= _s << 5;
				return (float)(_s & 0xFFFFFF) / 8388608f - 1f;
			}

			public float Unit()
			{
				return (Next() + 1f) * 0.5f;
			}

			public float Range(float lo, float hi)
			{
				return lo + (hi - lo) * Unit();
			}

			public float Log(float lo, float hi)
			{
				return lo * (float)Math.Pow(hi / lo, Unit());
			}
		}

		private struct Biquad
		{
			private float _b0;

			private float _b1;

			private float _b2;

			private float _a1;

			private float _a2;

			private float _z1;

			private float _z2;

			private static void Coefs(float hz, float q, out float cos, out float alpha)
			{
				hz = Math.Max(10f, Math.Min(hz, 19845f));
				double num = Math.PI * 2.0 * (double)hz / 44100.0;
				cos = (float)Math.Cos(num);
				alpha = (float)Math.Sin(num) / (2f * Math.Max(0.05f, q));
			}

			private void Set(float b0, float b1, float b2, float a0, float a1, float a2)
			{
				_b0 = b0 / a0;
				_b1 = b1 / a0;
				_b2 = b2 / a0;
				_a1 = a1 / a0;
				_a2 = a2 / a0;
			}

			public static Biquad LowPass(float hz, float q = 0.707f)
			{
				Biquad result = default(Biquad);
				result.SetLowPass(hz, q);
				return result;
			}

			public static Biquad HighPass(float hz, float q = 0.707f)
			{
				Biquad result = default(Biquad);
				result.SetHighPass(hz, q);
				return result;
			}

			public static Biquad BandPass(float hz, float q)
			{
				Biquad result = default(Biquad);
				result.SetBandPass(hz, q);
				return result;
			}

			public void SetLowPass(float hz, float q)
			{
				Coefs(hz, q, out var cos, out var alpha);
				Set((1f - cos) * 0.5f, 1f - cos, (1f - cos) * 0.5f, 1f + alpha, -2f * cos, 1f - alpha);
			}

			public void SetHighPass(float hz, float q)
			{
				Coefs(hz, q, out var cos, out var alpha);
				Set((1f + cos) * 0.5f, 0f - (1f + cos), (1f + cos) * 0.5f, 1f + alpha, -2f * cos, 1f - alpha);
			}

			public void SetBandPass(float hz, float q)
			{
				Coefs(hz, q, out var cos, out var alpha);
				Set(alpha, 0f, 0f - alpha, 1f + alpha, -2f * cos, 1f - alpha);
			}

			public float Run(float x)
			{
				float num = _b0 * x + _z1;
				_z1 = _b1 * x - _a1 * num + _z2;
				_z2 = _b2 * x - _a2 * num;
				return num;
			}
		}

		private sealed class WoodBody
		{
			public readonly float[] Hz;

			public readonly float[] Tau;

			public readonly float[] Amp;

			public WoodBody(Noise n, int count, float lo, float hi, float loss, float tilt, float maxTau = 0.2f, float radiate = 0f)
			{
				Hz = new float[count];
				Tau = new float[count];
				Amp = new float[count];
				for (int i = 0; i < count; i++)
				{
					float num = lo * (float)Math.Pow(hi / lo, ((float)i + n.Unit()) / (float)count);
					Hz[i] = num;
					Tau[i] = Math.Min(maxTau, 1f / (MathF.PI * num * loss * n.Range(0.7f, 1.4f)));
					Amp[i] = (float)Math.Pow(lo / num, tilt) * n.Range(0.15f, 1f) * num * num / (num * num + radiate * radiate);
				}
			}
		}

		private static readonly float[] VowelA = new float[3] { 750f, 1200f, 2600f };

		private static readonly float[] VowelO = new float[3] { 500f, 850f, 2500f };

		private static readonly float[] VowelU = new float[3] { 350f, 750f, 2400f };

		private static readonly float[] VowelUh = new float[3] { 620f, 1200f, 2550f };

		private static readonly float[] BladeRatios = new float[4] { 1f, 2.756f, 5.404f, 8.933f };

		public const int Rate = 44100;

		public const float DrumRollBlow = 2.5f;

		private static void AddNorm(float[] s, float[] part, int i0, float peak)
		{
			float num = 1E-06f;
			for (int j = 0; j < part.Length; j++)
			{
				num = Math.Max(num, Math.Abs(part[j]));
			}
			float num2 = peak / num;
			for (int k = 0; k < part.Length && i0 + k < s.Length; k++)
			{
				s[i0 + k] += part[k] * num2;
			}
		}

		private static float Smooth(float x)
		{
			x = Math.Max(0f, Math.Min(1f, x));
			return x * x * (3f - 2f * x);
		}

		private static void Voice(float[] s, Noise n, float start, float dur, float hz0, float hz1, float[] from, float[] to, float glide, float amp, float rough, float formants = 1f, float attack = 0.03f, float release = 0.12f)
		{
			int num = At(start);
			int num2 = Math.Min(s.Length - num, At(dur));
			if (num2 <= 0)
			{
				return;
			}
			float[] array = new float[num2];
			Biquad biquad = default(Biquad);
			Biquad biquad2 = default(Biquad);
			Biquad biquad3 = default(Biquad);
			double num3 = n.Unit();
			float num4 = 0f;
			float num5 = 0f;
			float num6 = 1f;
			bool flag = false;
			float num7 = n.Range(4.5f, 6f);
			float num8 = n.Unit() * 6.28f;
			for (int i = 0; i < num2; i++)
			{
				float num9 = (float)i / 44100f;
				float num10 = num9 / dur;
				float num11 = (hz0 + (hz1 - hz0) * Smooth(num10 / 0.33f)) * (1f - 0.18f * Math.Max(0f, (num10 - 0.72f) / 0.28f));
				num11 *= 1f + 0.012f * (float)Math.Sin(num8 + num9 * num7 * 6.283f) * Math.Min(1f, num9 / 0.25f) + num5;
				double d = num3;
				num3 += (double)(num11 / 44100f);
				if (Math.Floor(num3) != Math.Floor(d))
				{
					num5 = n.Next() * 0.03f * rough;
					num6 = 1f - n.Unit() * 0.25f * rough;
					flag = !flag;
				}
				float num12 = (float)(num3 - Math.Floor(num3));
				float num13 = ((num12 < 0.4f) ? (0.5f - 0.5f * (float)Math.Cos(Math.PI * (double)num12 / 0.4000000059604645)) : ((num12 < 0.56f) ? ((float)Math.Cos(Math.PI / 2.0 * (double)(num12 - 0.4f) / 0.1599999964237213)) : 0f));
				float num14 = (num13 - num4) * 44100f / num11 * 0.1f;
				num4 = num13;
				num14 *= num6 * (flag ? 1f : (1f - 0.6f * rough));
				num14 += n.Next() * (0.04f + 0.12f * rough) * (0.3f + num13);
				if ((i & 0x1F) == 0)
				{
					float num15 = Smooth((num10 - glide) / 0.1f + 0.5f);
					biquad.SetBandPass((from[0] + (to[0] - from[0]) * num15) * formants, 5f);
					biquad2.SetBandPass((from[1] + (to[1] - from[1]) * num15) * formants, 8f);
					biquad3.SetBandPass((from[2] + (to[2] - from[2]) * num15) * formants, 10f);
				}
				float num16 = biquad.Run(num14) + biquad2.Run(num14) * 0.6f + biquad3.Run(num14) * 0.3f;
				float num17 = Smooth(num9 / attack) * Smooth((dur - num9) / release);
				array[i] = (float)Math.Tanh(num16 * (2f + 4f * rough)) * num17;
			}
			AddNorm(s, array, num, amp);
		}

		private static void Growl(float[] s, Noise n, float start, float dur, float baseHz, float open, float flutterHz, float amp, float breath = 0.3f)
		{
			int num = At(start);
			int num2 = Math.Min(s.Length - num, At(dur));
			if (num2 <= 0)
			{
				return;
			}
			float[] array = new float[num2];
			Biquad biquad = default(Biquad);
			Biquad biquad2 = default(Biquad);
			Biquad biquad3 = default(Biquad);
			Biquad biquad4 = Biquad.LowPass(2200f * open);
			double num3 = 0.0;
			double num4 = 0.0;
			float num5 = 0f;
			for (int i = 0; i < num2; i++)
			{
				float num6 = (float)i / 44100f;
				float num7 = num6 / dur;
				float num8 = ((num7 < 0.3f) ? Smooth(num7 / 0.3f) : (1f - (num7 - 0.3f) / 0.7f * 0.6f));
				if ((i & 0x3F) == 0)
				{
					num5 = n.Next() * 0.09f;
				}
				float num9 = baseHz * (0.82f + 0.5f * num8) * (1f + num5);
				num3 += (double)(num9 / 44100f);
				num4 += (double)(num9 * 0.5f / 44100f);
				float num10 = (float)(num3 - Math.Floor(num3));
				float num11 = ((num10 < 0.12f) ? (num10 / 0.12f) : (1f - (num10 - 0.12f) / 0.88f)) * 2f - 1f;
				num11 *= 0.7f + 0.3f * (float)Math.Sin(num6 * 6.283f * (flutterHz + 6f * num8));
				if ((i & 0x1F) == 0)
				{
					biquad.SetBandPass((380f + 420f * num8) * open, 1.4f);
					biquad2.SetBandPass((800f + 700f * num8) * open, 2f);
					biquad3.SetBandPass((2000f + 900f * num8) * open, 3f);
				}
				float num12 = biquad.Run(num11) * 1.4f + biquad2.Run(num11) + biquad3.Run(num11) * 0.45f;
				float num13 = Smooth(num6 / 0.12f) * Smooth((dur - num6) / (dur * 0.3f));
				float num14 = (num12 + biquad4.Run(n.Next()) * breath * (0.6f + num8)) * num13 + (float)Math.Sin(6.283 * num4) * 0.3f * num13;
				array[i] = (float)Math.Tanh(num14 * 1.8f);
			}
			AddNorm(s, array, num, amp);
		}

		private static void Horn(float[] s, Noise n, float start, float dur, float hz, float amp, float bright)
		{
			int num = At(start);
			int num2 = Math.Min(s.Length - num, At(dur + 0.16f));
			if (num2 <= 0)
			{
				return;
			}
			float[] array = new float[num2];
			double num3 = n.Unit();
			double num4 = n.Unit();
			Biquad biquad = default(Biquad);
			Biquad biquad2 = Biquad.BandPass(1150f, 1.3f);
			for (int i = 0; i < num2; i++)
			{
				float num5 = (float)i / 44100f;
				float num6 = ((num5 < dur) ? (Smooth(num5 / 0.05f) * (1f - 0.12f * num5 / dur)) : (0.88f * Smooth(1f - (num5 - dur) / 0.16f)));
				float num7 = hz * (1f - 0.05f * (float)Math.Exp((0f - num5) / 0.035f)) * (1f + 0.005f * (float)Math.Sin(num5 * 6.283f * 5.5f) * Smooth((num5 - 0.25f) / 0.3f));
				num3 += (double)(num7 * 0.997f / 44100f);
				num4 += (double)(num7 * 1.003f / 44100f);
				float x = (float)(num3 - Math.Floor(num3) + (num4 - Math.Floor(num4))) - 1f;
				if ((i & 0x1F) == 0)
				{
					biquad.SetLowPass(250f + 2600f * num6 * bright, 0.8f);
				}
				float num8 = biquad.Run(x);
				num8 += biquad2.Run(num8) * 0.8f;
				array[i] = (float)Math.Tanh(num8 * 1.6f) * num6;
			}
			AddNorm(s, array, num, amp);
		}

		private static void Chime(float[] s, float start, float hz, float amp, float decay)
		{
			float[] array = new float[4] { 1f, 2.76f, 5.4f, 8.93f };
			float[] array2 = new float[4] { 1f, 0.45f, 0.22f, 0.1f };
			float[] array3 = new float[4] { 1f, 0.55f, 0.3f, 0.18f };
			int num = At(start);
			for (int i = 0; i < array.Length; i++)
			{
				if (!(hz * array[i] > 19845f))
				{
					double num2 = Math.PI * 2.0 * (double)hz * (double)array[i] / 44100.0;
					float num3 = decay * array3[i];
					int num4 = Math.Min(s.Length - num, At(num3 * 7f));
					for (int j = 0; j < num4; j++)
					{
						s[num + j] += (float)Math.Sin(num2 * (double)j) * amp * array2[i] * Env((float)j / 44100f, 0.001f, num3);
					}
				}
			}
		}

		private static void Glide(float[] s, float start, float dur, float f0, float f1, float amp, float attack, float decay)
		{
			int num = At(start);
			int num2 = Math.Min(s.Length - num, At(dur));
			double num3 = 0.0;
			for (int i = 0; i < num2; i++)
			{
				float num4 = (float)i / 44100f;
				float num5 = num4 / dur;
				num3 += Math.PI * 2.0 * (double)f0 * Math.Pow(f1 / f0, num5) / 44100.0;
				s[num + i] += (float)Math.Sin(num3) * amp * Smooth(num4 / attack) * Smooth((dur - num4) / decay);
			}
		}

		private static void HowlVoice(float[] s, Noise n, float start, float dur, float hz, float amp)
		{
			int num = At(start);
			int num2 = Math.Min(s.Length - num, At(dur));
			if (num2 > 0)
			{
				float[] array = new float[num2];
				Biquad biquad = Biquad.BandPass(hz * 2.2f, 1.2f);
				Biquad biquad2 = Biquad.BandPass(hz * 3f, 2f);
				double num3 = 0.0;
				float num4 = n.Range(4.2f, 5.6f);
				for (int i = 0; i < num2; i++)
				{
					float num5 = (float)i / 44100f;
					float num6 = num5 / dur;
					float num7 = ((num6 < 0.2f) ? (0.7f + 0.3f * Smooth(num6 / 0.2f)) : ((num6 > 0.72f) ? (1f - 0.32f * Smooth((num6 - 0.72f) / 0.28f)) : (1f + 0.03f * Smooth((num6 - 0.2f) / 0.5f))));
					float num8 = hz * num7 * (1f + 0.01f * (float)Math.Sin(num5 * 6.283f * num4) * Smooth((num5 - 0.3f) / 0.3f));
					num3 += (double)(num8 / 44100f);
					double num9 = Math.PI * 2.0 * num3;
					float num10 = (float)(Math.Sin(num9) + 0.32 * Math.Sin(2.0 * num9) + 0.1 * Math.Sin(3.0 * num9));
					float num11 = num10 * 0.7f + biquad.Run(num10) * 0.6f + biquad2.Run(n.Next()) * 0.08f;
					array[i] = num11 * Smooth(num5 / 0.14f) * Smooth((dur - num5) / 0.4f);
				}
				AddNorm(s, array, num, amp);
			}
		}

		private static void WarDrum(float[] s, Noise n, float start, float hz, float amp)
		{
			Thud(s, start, hz, amp, 0.28f, 0.6f);
			Knock(s, n, start, 260f, 0.8f, amp * 0.25f, 0.03f);
			Brush(s, n, start, 120f, 900f, amp * 0.3f, 0.002f, 0.05f);
		}

		public static float[] RallyHorn(int variant)
		{
			Noise n = new Noise(variant * 151 + 3);
			float[] s = New(2.7f);
			float num = ((variant == 0) ? 196f : 174.6f);
			Horn(s, n, 0f, 0.2f, num, 0.75f, 0.8f);
			Horn(s, n, 0.25f, 0.2f, num * 1.5f, 0.8f, 0.85f);
			Horn(s, n, 0.5f, 1.3f, num * 2f, 1f, 1f);
			Horn(s, n, 0.5f, 1.3f, num, 0.35f, 0.6f);
			WarDrum(s, n, 0f, 56f, 0.7f);
			WarDrum(s, n, 0.5f, 50f, 0.9f);
			Slap(s, 0.14f, 0.28f);
			HighPass(s, 35f);
			return Finish(s, 0.14f, 0.92f);
		}

		public static float[] Cheer(int variant, int voices)
		{
			Noise noise = new Noise(variant * 313 + voices * 17 + 11);
			float[] s = New(1.9f);
			for (int i = 0; i < voices; i++)
			{
				bool num = noise.Unit() < 0.3f;
				float num2 = (num ? noise.Range(250f, 330f) : noise.Range(125f, 185f));
				float formants = (num ? 1.16f : noise.Range(0.95f, 1.05f));
				float num3 = noise.Range(0f, 0.16f);
				float num4 = noise.Range(0.55f, 1f);
				Voice(s, noise, num3, 0.17f, num2 * 0.85f, num2 * 0.9f, VowelU, VowelU, 0.5f, 0.45f * num4, 0.15f, formants, 0.02f, 0.06f);
				Voice(s, noise, num3 + 0.17f + noise.Range(0f, 0.05f), noise.Range(0.55f, 0.85f), num2 * 0.95f, num2 * 1.15f, VowelO, VowelA, 0.12f, num4, 0.3f, formants, 0.025f, 0.2f);
			}
			for (int j = 0; j < Math.Min(voices, 6); j++)
			{
				float num5 = 0.2f + noise.Range(0f, 0.12f);
				Thud(s, num5, noise.Range(60f, 80f), 0.35f, 0.05f);
				Knock(s, noise, num5 + noise.Range(0f, 0.03f), noise.Range(2500f, 4200f), 3f, 0.12f, 0.02f);
			}
			Slap(s, 0.09f, 0.22f);
			HighPass(s, 70f);
			LowPass(s, 7500f);
			return Finish(s, 0.15f, 0.9f);
		}

		public static float[] WarCry(int variant, bool woman)
		{
			Noise noise = new Noise(variant * 89 + (woman ? 500 : 7));
			float[] s = New(1.5f);
			float num = (woman ? noise.Range(200f, 230f) : noise.Range(92f, 108f));
			float num2 = (woman ? 1.1f : 0.88f);
			Voice(s, noise, 0f, 1.25f, num, num * 1.3f, VowelUh, VowelA, 0.15f, 1f, 0.8f, num2, 0.05f, 0.3f);
			Voice(s, noise, 0f, 1.2f, num * 0.5f, num * 0.65f, VowelUh, VowelA, 0.15f, 0.4f, 1f, num2 * 0.9f, 0.08f, 0.3f);
			Thud(s, 0.02f, 46f, 0.5f, 0.18f);
			HighPass(s, 50f);
			return Finish(s, 0.17f, 0.95f);
		}

		public static float[] Grunt(int variant, bool woman)
		{
			Noise noise = new Noise(variant * 41 + ((!woman) ? 1 : 300));
			float[] s = New(0.3f);
			float num = (woman ? noise.Range(210f, 250f) : noise.Range(105f, 130f));
			Brush(s, noise, 0f, 900f, 4000f, 0.15f, 0.01f, 0.03f);
			Voice(s, noise, 0.03f, 0.2f, num * 1.1f, num * 0.9f, VowelUh, VowelUh, 0.5f, 1f, 0.35f, woman ? 1.15f : 1f, 0.012f, 0.1f);
			return Finish(s, 0.09f, 0.8f);
		}

		public static float[] Rage(int variant)
		{
			Noise n = new Noise(variant * 23 + 9);
			float[] s = New(1.4f);
			Thud(s, 0f, 40f, 1f, 0.3f, 0.8f);
			Brush(s, n, 0f, 30f, 200f, 0.5f, 0.01f, 0.25f);
			Thud(s, 0.55f, 50f, 0.7f, 0.06f, 0.5f);
			Thud(s, 0.75f, 58f, 0.5f, 0.05f, 0.5f);
			return Finish(s, 0.13f, 0.95f);
		}

		public static float[] Quake(int variant)
		{
			Noise noise = new Noise(variant * 199 + 31);
			float[] array = New(2f);
			Thud(array, 0f, 36f, 1f, 0.45f, 0.9f);
			Thud(array, 0f, 64f, 0.7f, 0.16f, 0.8f);
			Knock(array, noise, 0f, 280f, 0.8f, 0.55f, 0.03f);
			Grains(array, noise, 0f, 0.45f, 300f, 120f, 900f, 0.5f, 0.006f);
			Grains(array, noise, 0.15f, 1.2f, 70f, 1200f, 4500f, 0.16f, 0.003f);
			Biquad biquad = Biquad.LowPass(110f);
			for (int i = 0; i < array.Length; i++)
			{
				array[i] += biquad.Run(noise.Next()) * 2.5f * Env((float)i / 44100f, 0.02f, 0.5f);
			}
			HighPass(array, 25f);
			return Finish(array, 0.17f, 0.95f);
		}

		public static float[] GraceSong(int variant)
		{
			Noise noise = new Noise(variant * 67 + 13);
			float[] s = New(2.4f);
			float num = ((variant == 0) ? 392f : 349.2f);
			float[] array = new float[9] { 1f, 1.125f, 1.25f, 1.5f, 1.6875f, 2f, 2.25f, 2.5f, 3f };
			for (int i = 0; i < array.Length; i++)
			{
				Pluck(s, noise, (float)i * 0.055f, num * array[i], 0.45f - (float)i * 0.025f, 0.9965f, 5000f);
			}
			for (int j = 0; j < 6; j++)
			{
				Chime(s, 0.35f + noise.Range(0f, 1f), num * 4f * array[(!(noise.Unit() < 0.5f)) ? (3 + (int)(noise.Unit() * 5f) % 5) : 0], 0.06f, 0.5f);
			}
			Brush(s, noise, 0.05f, 1500f, 6000f, 0.12f, 0.25f, 0.35f);
			Grains(s, noise, 0.1f, 0.9f, 60f, 2000f, 6000f, 0.08f, 0.003f);
			return Finish(s, 0.09f, 0.8f);
		}

		public static float[] MagicHum(float seconds)
		{
			Noise noise = new Noise(808);
			float[] array = New(seconds);
			Biquad biquad = Biquad.BandPass(3500f, 1.5f);
			float[] array2 = new float[6] { 110f, 165.5f, 880f, 1321f, 1762f, 2203f };
			float[] array3 = new float[6] { 0.35f, 0.2f, 0.08f, 0.06f, 0.05f, 0.03f };
			for (int i = 0; i < array.Length; i++)
			{
				float num = (float)i / 44100f;
				float num2 = (float)i / (float)array.Length;
				float num3 = 0f;
				for (int j = 0; j < array2.Length; j++)
				{
					float num4 = 0.6f + 0.4f * (float)Math.Sin(Math.PI * 2.0 * (double)num2 * (double)(3 + j * 2) + (double)j);
					num3 += (float)Math.Sin(Math.PI * 2.0 * (double)array2[j] * (double)num) * array3[j] * num4;
				}
				array[i] = num3 + biquad.Run(noise.Next()) * 0.12f * (0.7f + 0.3f * (float)Math.Sin(Math.PI * 2.0 * (double)num2 * 5.0));
			}
			array = Crossfade(array, 11025);
			Normalize(array, 0.6f);
			return array;
		}

		public static float[] Fuse(float seconds)
		{
			Noise noise = new Noise(4545);
			float[] array = New(seconds);
			Biquad biquad = Biquad.BandPass(5200f, 1.2f);
			for (int i = 0; i < array.Length; i++)
			{
				float num = (float)i / (float)array.Length;
				array[i] = biquad.Run(noise.Next()) * 0.3f * (0.75f + 0.25f * (float)Math.Sin(Math.PI * 18.0 * (double)num) * (float)Math.Sin(Math.PI * 8.0 * (double)num + 0.5));
			}
			Grains(array, noise, 0f, seconds - 0.02f, 500f, 2500f, 9000f, 0.4f, 0.0004f);
			for (int j = 0; j < (int)(seconds * 3f); j++)
			{
				Knock(array, noise, noise.Unit() * (seconds - 0.05f), noise.Range(1500f, 3000f), 2f, 0.25f, 0.004f);
			}
			array = Crossfade(array, 7350);
			Normalize(array, 0.6f);
			return array;
		}

		public static float[] FireCast(int variant, bool hell)
		{
			Noise n = new Noise(variant * 71 + (hell ? 900 : 19));
			float[] array = New(1.2f);
			Sweep(array, n, 0f, 0.6f, hell ? 130f : 200f, hell ? 1000f : 1500f, hell ? 350f : 500f, 0.9f, 0.9f, 0.3f, 1.2f);
			Brush(array, n, 0f, 40f, hell ? 300f : 420f, 0.8f, 0.03f, 0.18f);
			Thud(array, 0.02f, hell ? 48f : 62f, 0.5f, 0.1f);
			Grains(array, n, 0.05f, 0.7f, 140f, 1500f, 6500f, 0.3f, 0.0007f);
			if (hell)
			{
				double num = 0.0;
				for (int i = 0; i < At(0.7f) && i < array.Length; i++)
				{
					float num2 = (float)i / 44100f;
					num += (double)((52f + 20f * (float)Math.Exp((0f - num2) / 0.2f)) / 44100f);
					array[i] += (float)Math.Tanh(((num - Math.Floor(num)) * 2.0 - 1.0) * 2.0) * 0.18f * Env(num2, 0.04f, 0.2f);
				}
				LowPass(array, 5000f);
			}
			return Finish(array, 0.14f, 0.9f);
		}

		public static float[] FlameBurst(int variant)
		{
			Noise noise = new Noise(variant * 233 + 47);
			float[] s = New(1.8f);
			Sweep(s, noise, 0f, 0.9f, 90f, 900f, 260f, 0.7f, 1f, 0.22f, 1.1f);
			Thud(s, 0f, 44f, 0.9f, 0.3f, 0.9f);
			Brush(s, noise, 0f, 40f, 500f, 0.9f, 0.02f, 0.4f);
			Grains(s, noise, 0.05f, 1.4f, 160f, 1500f, 7000f, 0.35f, 0.0007f);
			for (int i = 0; i < 4; i++)
			{
				Knock(s, noise, 0.1f + noise.Range(0f, 1f), noise.Range(300f, 700f), 2f, 0.3f, 0.01f);
			}
			return Finish(s, 0.16f, 0.95f);
		}

		public static float[] Plasma(int variant)
		{
			Noise n = new Noise(variant * 37 + 5);
			float[] array = New(0.8f);
			double num = 0.0;
			double num2 = 0.0;
			for (int i = 0; i < At(0.16f); i++)
			{
				float num3 = (float)i / 44100f / 0.16f;
				float num4 = 400f * (float)Math.Pow(6.0, num3);
				num2 += 0.005271606720309404;
				num += Math.PI * 2.0 * (double)num4 / 44100.0 + Math.Sin(num2) * 0.4;
				array[i] += (float)Math.Sin(num) * 0.3f * Smooth(num3 * 4f);
			}
			int num5 = At(0.16f);
			double num6 = 0.0;
			for (int j = num5; j < array.Length; j++)
			{
				float num7 = (float)(j - num5) / 44100f;
				float num8 = 180f + 2600f * (float)Math.Exp((0f - num7) / 0.05f);
				num6 += Math.PI * 2.0 * (double)num8 / 44100.0;
				array[j] += (float)Math.Sin(num6 + Math.Sin(num6 * 1.5) * 0.8) * 0.55f * Env(num7, 0.001f, 0.1f);
			}
			Modes(array, n, 0.16f, 1f, new float[3] { 3100f, 4700f, 6200f }, new float[3] { 0.09f, 0.06f, 0.04f }, new float[3] { 0.12f, 0.08f, 0.05f });
			Knock(array, n, 0.16f, 2500f, 0.8f, 0.3f, 0.01f);
			Thud(array, 0.16f, 80f, 0.4f, 0.05f);
			return Finish(array, 0.12f, 0.85f);
		}

		public static float[] Spit(int variant)
		{
			Noise n = new Noise(variant * 53 + 29);
			float[] s = New(1.1f);
			Brush(s, n, 0f, 1200f, 2600f, 0.35f, 0.06f, 0.04f);
			Knock(s, n, 0.13f, 420f, 1f, 0.5f, 0.012f);
			Brush(s, n, 0.13f, 800f, 3000f, 0.4f, 0.004f, 0.04f);
			Bubbles(s, n, 0.15f, 0.3f, 12, 500f, 1600f, 0.25f);
			Grains(s, n, 0.25f, 0.75f, 400f, 3000f, 9000f, 0.18f, 0.0005f);
			return Finish(s, 0.11f, 0.85f);
		}

		public static float[] RaiseDead(int variant)
		{
			Noise noise = new Noise(variant * 97 + 61);
			float[] s = New(2.8f);
			float num = ((variant == 0) ? 98f : 92.5f);
			float[] array = new float[4] { 1f, 1.059f, 1.5f, 2.12f };
			for (int i = 0; i < array.Length; i++)
			{
				Voice(s, noise, 0f, 2.4f, num * array[i], num * array[i] * 0.98f, VowelU, VowelO, 0.6f, 0.4f, 0.1f, 1f, 0.6f, 0.7f);
			}
			Glide(s, 0.2f, 2f, 1250f, 880f, 0.04f, 0.4f, 0.8f);
			Thud(s, 0.5f, 38f, 0.7f, 0.3f, 0.6f);
			Grains(s, noise, 0.5f, 0.8f, 200f, 80f, 500f, 0.4f, 0.006f);
			for (int j = 0; j < 3; j++)
			{
				for (int k = 0; k < 5; k++)
				{
					Knock(s, noise, 0.9f + (float)j * 0.3f + (float)k * 0.03f + noise.Range(0f, 0.02f), noise.Range(1300f, 2600f), 5f, 0.18f, 0.008f);
				}
			}
			return Finish(s, 0.12f, 0.9f);
		}

		public static float[] Blessing(int variant)
		{
			Noise n = new Noise(variant * 113 + 7);
			float[] s = New(2.8f);
			float num = ((variant == 0) ? 261.6f : 293.7f);
			float[] array = new float[4] { 1f, 1.26f, 1.5f, 2f };
			float[] array2 = array;
			foreach (float num2 in array2)
			{
				for (int j = 0; j < 2; j++)
				{
					Voice(s, n, 0f, 2.1f, num * num2 * ((j == 0) ? 0.997f : 1.003f), num * num2 * ((j == 0) ? 0.997f : 1.003f), VowelA, VowelO, 0.55f, 0.35f, 0f, 1.18f, 0.4f, 0.8f);
				}
			}
			for (int k = 0; k < 4; k++)
			{
				Chime(s, 0.15f + (float)k * 0.14f, num * 4f * array[k], 0.1f, 0.6f);
			}
			Brush(s, n, 0.1f, 5000f, 10000f, 0.04f, 0.4f, 0.4f);
			return Finish(s, 0.1f, 0.85f);
		}

		public static float[] Remedy(int variant)
		{
			Noise n = new Noise(variant * 29 + 83);
			float[] s = New(1.6f);
			Bubbles(s, n, 0f, 0.7f, 40, 400f, 1600f, 0.3f);
			Knock(s, n, 0.05f, 3600f, 6f, 0.2f, 0.03f);
			Chime(s, 0.3f, 1318.5f, 0.15f, 0.5f);
			Chime(s, 0.45f, 1975.5f, 0.12f, 0.6f);
			Brush(s, n, 0.2f, 5000f, 10000f, 0.03f, 0.3f, 0.3f);
			return Finish(s, 0.09f, 0.8f);
		}

		public static float[] Roots(int variant)
		{
			Noise n = new Noise(variant * 131 + 17);
			float[] s = New(1.7f);
			Creak(s, n, 0f, 0.7f, 0.6f, 45f);
			Creak(s, n, 0.2f, 0.8f, 0.5f, 62f);
			Thud(s, 0f, 32f, 0.6f, 0.3f, 0.5f);
			Grains(s, n, 0f, 0.6f, 220f, 150f, 900f, 0.4f, 0.005f);
			Brush(s, n, 0.3f, 1500f, 6000f, 0.25f, 0.15f, 0.3f);
			Grains(s, n, 0.3f, 0.8f, 120f, 2000f, 6000f, 0.15f, 0.003f);
			return Finish(s, 0.12f, 0.9f);
		}

		public static float[] FrostNova(int variant)
		{
			Noise noise = new Noise(variant * 177 + 3);
			float[] array = New(1.7f);
			Knock(array, noise, 0f, 1800f, 1.2f, 0.7f, 0.01f);
			Thud(array, 0f, 90f, 0.4f, 0.05f);
			Grains(array, noise, 0f, 0.6f, 300f, 2500f, 9000f, 0.45f, 0.0005f);
			float[] array2 = new float[5] { 2093f, 2793f, 3520f, 4186f, 5274f };
			for (int i = 0; i < array2.Length; i++)
			{
				double num = Math.PI * 2.0 * (double)array2[i] * (double)(1f + noise.Next() * 0.01f) / 44100.0;
				int num2 = At(0.02f + (float)i * 0.03f);
				float decay = 0.5f + 0.1f * (float)i;
				for (int j = num2; j < array.Length; j++)
				{
					float num3 = (float)(j - num2) / 44100f;
					array[j] += (float)Math.Sin(num * (double)(j - num2)) * 0.06f * (0.6f + 0.4f * (float)Math.Sin(num3 * 113f + (float)i)) * Env(num3, 0.003f, decay);
				}
			}
			Sweep(array, noise, 0f, 1.3f, 2500f, 1200f, 300f, 1.5f, 0.35f, 0.15f, 1.2f);
			return Finish(array, 0.12f, 0.9f);
		}

		public static float[] Blink(int variant)
		{
			Noise n = new Noise(variant * 19 + 77);
			float[] s = New(0.8f);
			Sweep(s, n, 0f, 0.25f, 3000f, 1500f, 200f, 2f, 0.5f, 0.1f);
			Glide(s, 0f, 0.24f, 1400f, 150f, 0.35f, 0.01f, 0.08f);
			Glide(s, 0.3f, 0.14f, 150f, 1600f, 0.3f, 0.02f, 0.04f);
			Knock(s, n, 0.42f, 1200f, 1.5f, 0.5f, 0.01f);
			Chime(s, 0.42f, 2637f, 0.06f, 0.25f);
			return Finish(s, 0.11f, 0.85f);
		}

		public static float[] Spores(int variant)
		{
			Noise n = new Noise(variant * 43 + 51);
			float[] s = New(1.5f);
			Thud(s, 0f, 110f, 0.5f, 0.06f);
			Brush(s, n, 0f, 200f, 1500f, 0.6f, 0.01f, 0.12f);
			Brush(s, n, 0.05f, 2000f, 8000f, 0.2f, 0.2f, 0.4f);
			Bubbles(s, n, 0.1f, 0.8f, 18, 300f, 900f, 0.25f);
			return Finish(s, 0.1f, 0.85f);
		}

		public static float[] SnowVolley(int variant)
		{
			Noise n = new Noise(variant * 61 + 2);
			float[] s = New(0.9f);
			Grains(s, n, 0f, 0.22f, 400f, 600f, 3500f, 0.5f, 0.002f, swell: true);
			for (int i = 0; i < 3; i++)
			{
				Sweep(s, n, 0.26f + (float)i * 0.08f, 0.2f, 400f, 2200f, 900f, 1.5f, 0.35f, 0.4f);
			}
			return Finish(s, 0.1f, 0.85f);
		}

		public static float[] BoneToss(int variant)
		{
			Noise noise = new Noise(variant * 7 + 99);
			float[] s = New(0.5f);
			for (int i = 0; i < 6; i++)
			{
				Knock(s, noise, (float)i * 0.03f + noise.Range(0f, 0.015f), noise.Range(1100f, 2400f), 6f, 0.3f, 0.01f);
			}
			Sweep(s, noise, 0.14f, 0.22f, 500f, 2000f, 800f, 1.5f, 0.3f, 0.4f);
			return Finish(s, 0.1f, 0.85f);
		}

		public static float[] Chatter(int variant)
		{
			Noise noise = new Noise(variant * 13 + 5);
			float[] s = New(0.7f);
			float num = noise.Range(330f, 420f);
			Voice(s, noise, 0f, 0.12f, num, num * 1.1f, VowelU, VowelU, 0.5f, 0.6f, 0.2f, 1.45f, 0.01f, 0.04f);
			Voice(s, noise, 0.14f, 0.12f, num * 1.1f, num * 1.2f, VowelU, VowelU, 0.5f, 0.7f, 0.2f, 1.45f, 0.01f, 0.04f);
			Voice(s, noise, 0.3f, 0.32f, num * 1.3f, num * 1.6f, VowelO, VowelA, 0.2f, 1f, 0.3f, 1.45f, 0.01f);
			return Finish(s, 0.11f, 0.85f);
		}

		public static float[] Howl(int variant, float pitch)
		{
			Noise noise = new Noise(variant * 211 + (int)(pitch * 100f));
			float num = (2.2f + noise.Unit() * 0.5f) / Math.Max(1f, pitch * 0.8f);
			float[] s = New(num + 0.4f);
			HowlVoice(s, noise, 0f, num, noise.Range(420f, 480f) * pitch, 1f);
			Slap(s, 0.16f, 0.25f);
			return Finish(s, 0.13f, 0.9f);
		}

		public static float[] PackAnswer(int variant, int howls)
		{
			Noise noise = new Noise(variant * 401 + howls);
			float[] s = New(4f);
			for (int i = 0; i < howls; i++)
			{
				float start = (float)i * noise.Range(0.35f, 0.6f);
				HowlVoice(s, noise, start, noise.Range(1.8f, 2.4f), noise.Range(380f, 560f), noise.Range(0.5f, 1f));
			}
			LowPass(s, 2600f);
			Slap(s, 0.21f, 0.35f);
			Slap(s, 0.37f, 0.2f);
			return Finish(s, 0.1f, 0.85f);
		}

		public static float[] Cackle(int variant)
		{
			Noise noise = new Noise(variant * 17 + 3);
			float[] s = New(1.3f);
			for (int i = 0; i < 7; i++)
			{
				float num = noise.Range(420f, 520f) * (1f + (float)i * 0.06f);
				Voice(s, noise, (float)i * 0.15f, 0.12f, num * 0.85f, num * 1.25f, VowelU, VowelA, 0.4f, 1f - (float)i * 0.06f, 0.4f, 1.35f, 0.01f, 0.05f);
			}
			return Finish(s, 0.12f, 0.85f);
		}

		public static float[] BeastRoar(int variant)
		{
			Noise noise = new Noise(variant * 509 + 21);
			float[] s = New(1.8f);
			Growl(s, noise, 0f, 1.55f, noise.Range(78f, 95f), 1.15f, 30f, 1f, 0.35f);
			Thud(s, 0f, 50f, 0.3f, 0.25f, 0.2f);
			HighPass(s, 35f);
			return Finish(s, 0.17f, 0.95f);
		}

		public static float[] Snarl(int variant)
		{
			Noise noise = new Noise(variant * 83 + 41);
			float[] s = New(0.6f);
			Growl(s, noise, 0f, 0.38f, noise.Range(150f, 180f), 1.6f, 42f, 0.8f, 0.5f);
			Sweep(s, noise, 0.05f, 0.28f, 600f, 2500f, 1000f, 1.5f, 0.4f, 0.4f);
			return Finish(s, 0.11f, 0.85f);
		}

		public static float[] Hop(int variant)
		{
			Noise n = new Noise(variant * 11 + 7);
			float[] s = New(0.4f);
			Brush(s, n, 0f, 300f, 2500f, 0.3f, 0.004f, 0.03f);
			Thud(s, 0f, 140f, 0.3f, 0.03f);
			Sweep(s, n, 0.03f, 0.25f, 500f, 2000f, 900f, 1.5f, 0.35f, 0.4f);
			return Finish(s, 0.08f, 0.8f);
		}

		public static float[] Stampede(int variant)
		{
			Noise noise = new Noise(variant * 97 + 13);
			float[] s = New(1.3f);
			Brush(s, noise, 0f, 600f, 2500f, 0.5f, 0.005f, 0.06f);
			Brush(s, noise, 0.1f, 500f, 2200f, 0.35f, 0.005f, 0.05f);
			float[] array = new float[12]
			{
				0.15f, 0.23f, 0.29f, 0.43f, 0.5f, 0.55f, 0.68f, 0.74f, 0.79f, 0.92f,
				0.97f, 1.01f
			};
			for (int i = 0; i < array.Length; i++)
			{
				float num = 0.8f - (float)i * 0.04f;
				Thud(s, array[i], noise.Range(60f, 80f), num, 0.04f);
				Knock(s, noise, array[i], noise.Range(500f, 900f), 1.5f, num * 0.4f, 0.008f);
			}
			Sweep(s, noise, 0.1f, 0.9f, 200f, 900f, 300f, 0.8f, 0.3f, 0.5f);
			return Finish(s, 0.14f, 0.9f);
		}

		public static float[] Slide(int variant)
		{
			Noise n = new Noise(variant * 31 + 19);
			float[] s = New(0.9f);
			Thud(s, 0f, 120f, 0.45f, 0.05f);
			Brush(s, n, 0f, 1200f, 6000f, 0.35f, 0.05f, 0.3f);
			Sweep(s, n, 0f, 0.75f, 300f, 800f, 200f, 0.7f, 0.4f, 0.2f);
			return Finish(s, 0.1f, 0.85f);
		}

		public static float[] Flutter(int variant)
		{
			Noise noise = new Noise(variant * 37 + 23);
			float[] s = New(0.7f);
			float num = 1f + noise.Next() * 0.1f;
			for (int i = 0; i < 4; i++)
			{
				float num2 = (float)i * 0.12f;
				Sweep(s, noise, num2, 0.09f, 300f * num, 1200f * num, 500f * num, 0.9f, 0.6f - (float)i * 0.08f, 0.4f);
				Brush(s, noise, num2 + 0.03f, 1500f, 7000f, 0.15f, 0.005f, 0.03f);
			}
			return Finish(s, 0.1f, 0.85f);
		}

		public static float[] Buzz(int variant)
		{
			Noise noise = new Noise(variant * 59 + 11);
			float[] array = New(0.8f);
			double num = 0.0;
			Biquad biquad = Biquad.LowPass(2500f);
			float num2 = noise.Range(170f, 230f);
			for (int i = 0; i < array.Length; i++)
			{
				float num3 = (float)i / 44100f;
				float num4 = num3 / 0.8f;
				num += (double)(num2 * (1f + 0.4f * Smooth(num4 / 0.3f)) / 44100f);
				float x = (float)((num - Math.Floor(num)) * 2.0 - 1.0);
				array[i] = biquad.Run(x) * (0.7f + 0.3f * (float)Math.Sin(num3 * 6.283f * 23f)) * Smooth(num3 / 0.05f) * Smooth((0.8f - num3) / 0.3f);
			}
			return Finish(array, 0.08f, 0.8f);
		}

		public static float[] Venom(int variant)
		{
			Noise n = new Noise(variant * 71 + 37);
			float[] s = New(0.8f);
			Brush(s, n, 0f, 3500f, 9500f, 0.5f, 0.06f, 0.2f);
			Knock(s, n, 0.3f, 2600f, 3f, 0.4f, 0.006f);
			Thud(s, 0.3f, 140f, 0.35f, 0.03f);
			Brush(s, n, 0.31f, 300f, 1500f, 0.35f, 0.003f, 0.05f);
			Bubbles(s, n, 0.32f, 0.15f, 6, 600f, 1500f, 0.15f);
			return Finish(s, 0.11f, 0.85f);
		}

		public static float[] Sting(int variant)
		{
			Noise n = new Noise(variant * 47 + 3);
			float[] s = New(0.45f);
			Knock(s, n, 0f, 3800f, 4f, 0.35f, 0.004f);
			Knock(s, n, 0.05f, 3300f, 4f, 0.3f, 0.004f);
			Knock(s, n, 0.12f, 2200f, 2f, 0.45f, 0.008f);
			Brush(s, n, 0.12f, 400f, 1800f, 0.3f, 0.003f, 0.04f);
			return Finish(s, 0.1f, 0.85f);
		}

		public static float[] ShellClunk(int variant)
		{
			Noise noise = new Noise(variant * 101 + 9);
			float[] s = New(0.6f);
			Brush(s, noise, 0f, 600f, 3000f, 0.25f, 0.02f, 0.06f);
			Modes(s, noise, 0.06f, 1f + noise.Next() * 0.05f, new float[4] { 180f, 410f, 780f, 1350f }, new float[4] { 0.12f, 0.07f, 0.04f, 0.02f }, new float[4] { 0.6f, 0.35f, 0.2f, 0.1f });
			Knock(s, noise, 0.06f, 700f, 1f, 0.4f, 0.01f);
			Thud(s, 0.06f, 110f, 0.5f, 0.05f);
			return Finish(s, 0.11f, 0.85f);
		}

		public static float[] PincerSnap(int variant)
		{
			Noise n = new Noise(variant * 67 + 1);
			float[] s = New(0.5f);
			for (int i = 0; i < 2; i++)
			{
				float start = (float)i * 0.09f;
				Knock(s, n, start, 3200f, 4f, 0.7f, 0.006f);
				Modes(s, n, start, 1f, new float[3] { 2200f, 3700f, 5100f }, new float[3] { 0.02f, 0.012f, 0.008f }, new float[3] { 0.2f, 0.12f, 0.08f });
				Thud(s, start, 300f, 0.2f, 0.015f);
			}
			Grains(s, n, 0.18f, 0.12f, 300f, 500f, 3000f, 0.4f, 0.003f);
			return Finish(s, 0.12f, 0.85f);
		}

		public static float[] RottenBite(int variant)
		{
			Noise n = new Noise(variant * 89 + 17);
			float[] s = New(1.2f);
			Knock(s, n, 0f, 2000f, 2f, 0.5f, 0.008f);
			Thud(s, 0f, 120f, 0.5f, 0.04f);
			Grains(s, n, 0f, 0.15f, 300f, 600f, 3000f, 0.35f, 0.003f);
			Brush(s, n, 0.03f, 200f, 1200f, 0.4f, 0.01f, 0.1f);
			Bubbles(s, n, 0.05f, 0.4f, 14, 300f, 900f, 0.25f);
			Voice(s, n, 0.3f, 0.8f, 88f, 80f, VowelUh, VowelO, 0.5f, 0.6f, 0.9f, 0.9f, 0.1f, 0.3f);
			return Finish(s, 0.12f, 0.9f);
		}

		public static float[] AbilityReady()
		{
			float[] s = New(1f);
			Chime(s, 0f, 1046.5f, 0.3f, 0.35f);
			Chime(s, 0.09f, 1568f, 0.35f, 0.45f);
			return Finish(s, 0.05f, 0.6f);
		}

		public static float[] AbilityDenied(int variant)
		{
			Noise n = new Noise(variant * 3 + 71);
			float[] s = New(0.3f);
			Thud(s, 0f, 150f, 0.6f, 0.03f, 0.4f);
			Knock(s, n, 0f, 520f, 1.5f, 0.3f, 0.015f);
			return Finish(s, 0.06f, 0.6f);
		}

		public static float[] BlasterHiss(int variant)
		{
			Noise n = new Noise(variant * 211 + 17);
			float[] s = New(1.4f);
			Brush(s, n, 0f, 2400f, 9000f, 0.55f, 0.015f, 0.32f);
			Brush(s, n, 0.02f, 900f, 3000f, 0.3f, 0.04f, 0.45f);
			Bubbles(s, n, 0.05f, 0.5f, 7, 500f, 1400f, 0.08f);
			return Finish(s, 0.12f, 0.7f);
		}

		public static float[] BlasterRatchet(int variant)
		{
			Noise noise = new Noise(variant * 97 + 5);
			float[] s = New(0.45f);
			Brush(s, noise, 0f, 1200f, 5000f, 0.06f, 0.01f, 0.05f);
			float[] array = new float[4] { 0.01f, 0.06f, 0.11f, 0.17f };
			for (int i = 0; i < array.Length; i++)
			{
				bool flag = i == array.Length - 1;
				Knock(s, noise, array[i] + noise.Range(-0.01f, 0.01f), flag ? 1700f : (3600f + 400f * noise.Unit()), flag ? 3f : 6f, flag ? 0.5f : 0.32f, flag ? 0.012f : 0.005f);
				if (flag)
				{
					Thud(s, array[i], 160f, 0.25f, 0.03f);
				}
			}
			return Finish(s, 0.08f, 0.6f);
		}

		public static float[] BlasterAlarm(int variant)
		{
			float[] array = New(0.45f);
			for (int i = 0; i < 2; i++)
			{
				int num = At((float)i * 0.19f);
				float num2 = 1650f - (float)i * 280f;
				double num3 = 0.0;
				for (int j = 0; j < At(0.13f) && num + j < array.Length; j++)
				{
					float num4 = (float)j / 44100f;
					num3 += Math.PI * 2.0 * (double)(num2 - 300f * num4 / 0.13f) / 44100.0;
					float num5 = (float)Math.Sign(Math.Sin(num3)) * 0.5f + (float)Math.Sin(num3) * 0.5f;
					array[num + j] += num5 * 0.35f * Env(num4, 0.003f, 0.09f);
				}
			}
			Biquad biquad = Biquad.LowPass(5000f);
			for (int k = 0; k < array.Length; k++)
			{
				array[k] = biquad.Run(array[k]);
			}
			return Finish(array, 0.08f, 0.6f);
		}

		public static float[] BlasterDry(int variant)
		{
			Noise n = new Noise(variant * 31 + 9);
			float[] s = New(0.35f);
			Knock(s, n, 0f, 2200f, 4f, 0.45f, 0.006f);
			Brush(s, n, 0.01f, 3000f, 8000f, 0.08f, 0.01f, 0.08f);
			return Finish(s, 0.06f, 0.5f);
		}

		public static float[] NukeRumble(int variant)
		{
			Noise noise = new Noise(variant * 401 + 77);
			float[] array = New(7f);
			Biquad biquad = Biquad.LowPass(140f);
			Biquad biquad2 = Biquad.LowPass(140f);
			Biquad biquad3 = Biquad.LowPass(420f);
			float num = 0f;
			for (int i = 0; i < array.Length; i++)
			{
				float t = (float)i / 44100f;
				float x = noise.Next();
				if ((i & 0xFF) == 0)
				{
					num = 0.75f + 0.25f * noise.Next();
				}
				float num2 = Env(t, 0.25f, 2.2f) * num;
				array[i] = biquad2.Run(biquad.Run(x)) * 3.2f * num2 + biquad3.Run(x) * 0.5f * Env(t, 0.05f, 0.9f);
			}
			Thud(array, 0f, 38f, 0.9f, 0.6f, 0.4f);
			Grains(array, noise, 0.1f, 2.5f, 26f, 300f, 1600f, 0.12f, 0.02f);
			return Finish(array, 0.2f, 0.9f);
		}

		public static float[] ArmorPlate(int variant)
		{
			Noise noise = new Noise(variant * 613 + 29);
			float[] s = New(0.24f);
			int num = 3 + (int)(noise.Unit() * 3f);
			for (int i = 0; i < num; i++)
			{
				float start = noise.Range(0f, 0.075f);
				Modes(s, noise, start, noise.Range(0.85f, 1.2f), new float[4] { 1650f, 2780f, 4120f, 5630f }, new float[4] { 0.03f, 0.022f, 0.015f, 0.01f }, new float[4] { 0.16f, 0.11f, 0.07f, 0.04f });
			}
			Grains(s, noise, 0.005f, 0.1f, 220f, 3500f, 9000f, 0.08f, 0.0006f);
			Brush(s, noise, 0f, 1500f, 5000f, 0.06f, 0.01f, 0.03f);
			HighPass(s, 400f);
			return Finish(s, 0.045f, 0.6f);
		}

		public static float[] ArmorLeather(int variant)
		{
			Noise noise = new Noise(variant * 337 + 11);
			float[] s = New(0.24f);
			Creak(s, noise, noise.Range(0f, 0.03f), noise.Range(0.07f, 0.12f), 0.35f, noise.Range(55f, 90f));
			Brush(s, noise, 0f, 500f, 2500f, 0.15f, 0.015f, 0.04f);
			return Finish(s, 0.035f, 0.5f);
		}

		public static float[] Scuff(Ground ground, int variant)
		{
			return Drag(ground, variant, skid: false);
		}

		public static float[] Skid(Ground ground, int variant)
		{
			return Drag(ground, variant, skid: true);
		}

		private static float[] Drag(Ground ground, int variant, bool skid)
		{
			Noise noise = new Noise(variant * 919 + (int)ground * 7 + (skid ? 1000 : 5));
			float num = (skid ? noise.Range(0.32f, 0.42f) : noise.Range(0.1f, 0.15f));
			float[] s = New(num + 0.2f);
			float decay = num * 0.35f;
			Thud(s, 0f, 66f, skid ? 0.35f : 0.25f, 0.03f);
			switch (ground)
			{
			case Ground.Grass:
				Brush(s, noise, 0f, 900f, 4500f, 0.45f, 0.012f, decay);
				Grains(s, noise, 0f, num, 260f, 1800f, 6000f, 0.2f, 0.0012f);
				break;
			case Ground.Sand:
				Grains(s, noise, 0f, num, 1100f, 1400f, 5200f, 0.32f, 0.0011f);
				Brush(s, noise, 0f, 700f, 3200f, 0.25f, 0.015f, decay);
				break;
			case Ground.Soil:
				Brush(s, noise, 0f, 400f, 2500f, 0.45f, 0.012f, decay);
				Grains(s, noise, 0f, num, 200f, 900f, 3400f, 0.24f, 0.0016f);
				break;
			case Ground.Rock:
				Brush(s, noise, 0f, 1500f, 7000f, 0.35f, 0.006f, decay);
				Grains(s, noise, 0f, num, 420f, 2000f, 7000f, 0.22f, 0.0007f);
				Knock(s, noise, 0.004f, 1900f, 0.9f, 0.15f, 0.004f);
				break;
			case Ground.Snow:
				Grains(s, noise, 0f, num, 1200f, 550f, 3000f, 0.34f, 0.0017f, swell: true);
				break;
			case Ground.Wood:
				Brush(s, noise, 0f, 600f, 3000f, 0.4f, 0.01f, decay);
				Creak(s, noise, 0.02f, num * 0.6f, 0.12f, noise.Range(60f, 85f));
				break;
			case Ground.Shallows:
				Sweep(s, noise, 0f, num, 500f, 1500f, 450f, 1.3f, 0.6f, 0.25f);
				Bubbles(s, noise, 0.03f, num, skid ? 10 : 5, 600f, 1600f, 0.2f);
				break;
			}
			LowPass(s, 6500f);
			return Finish(s, skid ? 0.07f : 0.06f, 0.75f);
		}

		public static float[] PushOff(int variant)
		{
			Noise n = new Noise(variant * 271 + 41);
			float[] s = New(0.26f);
			Thud(s, 0f, 75f, 0.35f, 0.03f);
			Brush(s, n, 0f, 700f, 3500f, 0.3f, 0.006f, 0.035f);
			Brush(s, n, 0.02f, 500f, 2800f, 0.25f, 0.02f, 0.05f);
			return Finish(s, 0.05f, 0.6f);
		}

		public static float[] Oof(int variant, bool woman)
		{
			Noise noise = new Noise(variant * 53 + (woman ? 700 : 3));
			float[] s = New(0.36f);
			float num = (woman ? noise.Range(190f, 230f) : noise.Range(95f, 120f));
			Brush(s, noise, 0f, 600f, 3500f, 0.12f, 0.005f, 0.05f);
			Voice(s, noise, 0.01f, 0.18f, num * 1.15f, num * 0.85f, VowelUh, VowelUh, 0.5f, 1f, 0.45f, woman ? 1.15f : 1f, 0.006f, 0.08f);
			return Finish(s, 0.08f, 0.8f);
		}

		public static float[] BowDraw(int variant, float seconds)
		{
			Noise noise = new Noise(variant * 977 + 31);
			float[] array = New(seconds + 0.2f);
			float num = 1f + noise.Next() * 0.05f;
			Knock(array, noise, 0f, 1700f * num, 1.8f, 0.14f, 0.005f);
			Thud(array, 0f, 150f * num, 0.12f, 0.02f, 0.3f);
			Brush(array, noise, 0.01f, 2200f, 6500f, 0.06f, 0.04f, 0.08f);
			Biquad biquad = Biquad.BandPass(720f * num, 4f);
			Biquad biquad2 = Biquad.BandPass(1650f * num, 5f);
			Biquad biquad3 = Biquad.BandPass(380f * num, 3f);
			double num2 = 0.0;
			float num3 = noise.Range(9f, 16f);
			int num4 = At(0.04f);
			int num5 = Math.Min(array.Length - num4, At(seconds));
			for (int i = 0; i < num5; i++)
			{
				float num6 = (float)i / 44100f;
				float num7 = num6 / seconds;
				float num8 = (28f + 75f * num7 * num7) * (1f + 0.22f * (float)Math.Sin(num6 * num3) + 0.1f * noise.Next());
				num2 += (double)(num8 / 44100f);
				float num9 = (float)(num2 - Math.Floor(num2));
				float x = ((num9 < 0.05f) ? (1f - num9 / 0.05f) : 0f);
				float num10 = 0.25f + 0.75f * (float)Math.Pow(Math.Min(1f, num7 * 1.15f), 1.399999976158142);
				float num11 = ((num7 > 0.94f) ? Math.Max(0f, 1f - (num7 - 0.94f) / 0.06f) : 1f);
				float num12 = biquad.Run(x) * 2.6f + biquad2.Run(x) * 1.4f + biquad3.Run(x) * 1.2f;
				num12 += noise.Next() * 0.012f * num10;
				array[num4 + i] += num12 * 0.32f * num10 * num11;
			}
			Sweep(array, noise, 0.08f, seconds * 0.9f, 2600f, 3800f, 4200f, 7f, 0.03f, 0.85f, 1f);
			HighPass(array, 90f);
			return Finish(array, 0.06f, 0.6f);
		}

		public static float[] BowLoose(int variant)
		{
			Noise noise = new Noise(variant * 389 + 97);
			float[] array = New(0.6f);
			float num = 1f + noise.Next() * 0.06f;
			Knock(array, noise, 0f, 3400f * num, 0.7f, 0.55f, 0.0035f, 0.0002f);
			Thud(array, 0f, 92f * num, 1f, 0.05f, 1.3f);
			Thud(array, 0.002f, 58f * num, 0.55f, 0.08f, 0.6f);
			Modes(array, noise, 0f, num, new float[4] { 175f, 410f, 920f, 1850f }, new float[4] { 0.05f, 0.03f, 0.018f, 0.01f }, new float[4] { 0.45f, 0.3f, 0.16f, 0.07f });
			Pluck(array, noise, 0f, 118f * num, 0.38f, 0.982f, 1800f);
			Pluck(array, noise, 0f, 236f * num, 0.1f, 0.975f, 2600f);
			Brush(array, noise, 0f, 90f, 900f, 0.35f, 0.002f, 0.035f);
			Sweep(array, noise, 0.012f, 0.26f, 3200f * num, 2400f * num, 700f, 1.4f, 0.36f, 0.18f, 1.2f);
			for (int i = At(0.12f); i < array.Length; i++)
			{
				array[i] *= (float)Math.Exp((float)(-(i - At(0.12f))) / 3087f);
			}
			HighPass(array, 45f);
			LowPass(array, 9000f);
			return Finish(array, 0.16f, 0.95f);
		}

		public static float[] BowLetDown(int variant)
		{
			Noise noise = new Noise(variant * 211 + 13);
			float[] array = New(0.45f);
			Biquad biquad = Biquad.BandPass(680f, 4f);
			Biquad biquad2 = Biquad.BandPass(1500f, 5f);
			double num = 0.0;
			int num2 = At(0.32f);
			for (int i = 0; i < num2 && i < array.Length; i++)
			{
				float num3 = (float)i / (float)num2;
				num += (double)((80f - 55f * num3) * (1f + 0.15f * noise.Next()) / 44100f);
				float num4 = (float)(num - Math.Floor(num));
				float x = ((num4 < 0.05f) ? (1f - num4 / 0.05f) : 0f);
				array[i] += (biquad.Run(x) * 2.4f + biquad2.Run(x) * 1.2f) * 0.25f * (1f - num3) * Math.Min(1f, num3 * 12f);
			}
			Knock(array, noise, 0.3f, 1500f, 2f, 0.06f, 0.006f);
			return Finish(array, 0.035f, 0.5f);
		}

		public static float[] BowNock(int variant)
		{
			Noise noise = new Noise(variant * 557 + 71);
			float[] s = New(0.7f);
			Sweep(s, noise, 0f, 0.22f, 1400f, 3600f, 2600f, 1.2f, 0.18f, 0.7f, 1.2f);
			Grains(s, noise, 0.02f, 0.18f, 90f, 900f, 3200f, 0.22f, 0.003f);
			Modes(s, noise, 0.05f, 1f + noise.Next() * 0.05f, new float[2] { 1300f, 2650f }, new float[2] { 0.02f, 0.012f }, new float[2] { 0.05f, 0.03f });
			Knock(s, noise, 0.5f, 3100f, 3f, 0.3f, 0.004f);
			Knock(s, noise, 0.505f, 1900f, 2f, 0.12f, 0.006f);
			return Finish(s, 0.05f, 0.6f);
		}

		public static float[] ArrowWhizz(int variant)
		{
			Noise noise = new Noise(variant * 733 + 19);
			float[] array = New(0.42f);
			float num = 1f + noise.Next() * 0.1f;
			Sweep(array, noise, 0f, 0.36f, 1800f * num, 4200f * num, 900f * num, 2.2f, 0.7f, 0.42f, 1.8f);
			double num2 = 0.0;
			for (int i = 0; i < At(0.36f) && i < array.Length; i++)
			{
				float num3 = (float)i / (float)At(0.36f);
				float num4 = (420f - 180f * num3) * num;
				num2 += (double)(num4 / 44100f);
				float num5 = (float)(num2 - Math.Floor(num2)) * 2f - 1f;
				float num6 = (float)Math.Pow(Math.Sin(Math.PI * (double)Math.Min(1f, num3 * 1.15f)), 3.0);
				array[i] += num5 * 0.05f * num6;
			}
			LowPass(array, 7000f);
			return Finish(array, 0.07f, 0.7f);
		}

		public static float[] ArrowHit(int variant)
		{
			Noise noise = new Noise(variant * 409 + 3);
			float[] array = New(0.35f);
			float num = 1f + noise.Next() * 0.08f;
			Knock(array, noise, 0f, 1150f * num, 1.1f, 0.6f, 0.006f);
			Thud(array, 0f, 135f * num, 0.9f, 0.035f, 0.8f);
			Brush(array, noise, 0f, 200f, 1500f, 0.3f, 0.002f, 0.025f);
			double num2 = 0.0;
			for (int i = At(0.01f); i < array.Length; i++)
			{
				float num3 = (float)(i - At(0.01f)) / 44100f;
				num2 += Math.PI * 124.0 * (double)num / 44100.0;
				array[i] += (float)Math.Sin(num2) * 0.12f * (float)Math.Exp((0f - num3) / 0.06f) * (0.6f + 0.4f * (float)Math.Sin(num3 * 260f));
			}
			return Finish(array, 0.12f, 0.9f);
		}

		public static float[] ArrowBody(int variant)
		{
			Noise noise = new Noise(variant * 211 + 41);
			float[] array = New(0.3f);
			float num = 1f + noise.Next() * 0.08f;
			Knock(array, noise, 0f, 640f * num, 0.9f, 0.55f, 0.008f);
			Thud(array, 0f, 105f * num, 1f, 0.045f, 0.85f);
			Brush(array, noise, 0f, 180f, 1100f, 0.25f, 0.002f, 0.02f);
			double num2 = 0.0;
			for (int i = At(0.008f); i < array.Length; i++)
			{
				float num3 = (float)(i - At(0.008f)) / 44100f;
				num2 += Math.PI * 96.0 * (double)num / 44100.0;
				array[i] += (float)Math.Sin(num2) * 0.07f * (float)Math.Exp((0f - num3) / 0.04f);
			}
			LowPass(array, 3200f);
			return Finish(array, 0.12f, 0.9f);
		}

		public static float[] ArrowConfirm(int variant)
		{
			Noise noise = new Noise(variant * 613 + 29);
			float[] s = New(0.2f);
			float num = 1f + (float)(variant - 1) * 0.035f + noise.Next() * 0.01f;
			Knock(s, noise, 0f, 2350f * num, 2.2f, 0.75f, 0.0028f, 0.0003f);
			Modes(s, noise, 0f, num, new float[2] { 960f, 2280f }, new float[2] { 0.022f, 0.012f }, new float[2] { 0.16f, 0.07f });
			Thud(s, 0.002f, 140f * num, 0.32f, 0.011f, 0.5f);
			HighPass(s, 70f);
			return Finish(s, 0.09f, 0.75f);
		}

		public static float[] ArrowGround(int variant)
		{
			Noise noise = new Noise(variant * 101 + 77);
			float[] s = New(0.25f);
			Thud(s, 0f, 110f + 20f * noise.Next(), 0.7f, 0.025f, 0.5f);
			Brush(s, noise, 0f, 300f, 2500f, 0.35f, 0.001f, 0.02f);
			Knock(s, noise, 0f, 900f, 1f, 0.2f, 0.005f);
			return Finish(s, 0.07f, 0.7f);
		}

		public static float[] ClashMetal(int variant)
		{
			Noise noise = new Noise(variant * 613 + 29);
			float[] s = New(0.75f);
			float num = 1f + noise.Next() * 0.07f;
			float num2 = 840f * num;
			float f = num2 * (1.055f + 0.03f * noise.Unit());
			Knock(s, noise, 0f, 3300f * num, 1.4f, 0.9f, 0.0035f);
			Brush(s, noise, 0f, 2400f, 10000f, 0.38f, 0.0008f, 0.014f);
			Brush(s, noise, 0.004f, 1500f, 6000f, 0.12f, 0.002f, 0.045f);
			Thud(s, 0f, 170f * num, 0.45f, 0.018f, 0.5f);
			Modes(s, noise, 0.0005f, 1f, Scale(BladeRatios, num2), new float[4] { 0.26f, 0.19f, 0.1f, 0.055f }, new float[4] { 0.2f, 0.27f, 0.16f, 0.07f });
			Modes(s, noise, 0.0012f, 1f, Scale(BladeRatios, f), new float[4] { 0.22f, 0.16f, 0.09f, 0.05f }, new float[4] { 0.13f, 0.18f, 0.1f, 0.05f });
			return Finish(s, 0.12f, 0.92f);
		}

		public static float[] ParryRing(int variant)
		{
			Noise noise = new Noise(variant * 977 + 5);
			float[] array = New(1.4f);
			float pitch = 1f + noise.Next() * 0.04f;
			float[] array2 = ClashMetal(variant + 7);
			for (int i = 0; i < array2.Length && i < array.Length; i++)
			{
				array[i] += array2[i] * 0.9f;
			}
			Biquad biquad = default(Biquad);
			int num = At(0.006f);
			int num2 = Math.Min(array.Length - num, At(0.16f));
			for (int j = 0; j < num2; j++)
			{
				float num3 = (float)j / 44100f;
				biquad.SetBandPass(5200f + 30000f * num3, 2.5f);
				array[num + j] += biquad.Run(noise.Next()) * 0.32f * Env(num3, 0.004f, 0.04f);
			}
			Modes(array, noise, 0.01f, pitch, new float[3] { 1320f, 1980f, 3960f }, new float[3] { 0.75f, 0.6f, 0.25f }, new float[3] { 0.11f, 0.075f, 0.03f });
			return Finish(array, 0.13f, 0.92f);
		}

		public static float[] ClashWood(int variant)
		{
			Noise noise = new Noise(variant * 311 + 71);
			float[] s = New(0.4f);
			float num = 1f + noise.Next() * 0.08f;
			Knock(s, noise, 0f, 950f * num, 2f, 0.85f, 0.009f);
			Modes(s, noise, 0.0005f, num, new float[3] { 410f, 1130f, 1870f }, new float[3] { 0.055f, 0.035f, 0.02f }, new float[3] { 0.3f, 0.2f, 0.1f });
			Thud(s, 0f, 125f * num, 0.55f, 0.03f, 0.6f);
			Brush(s, noise, 0f, 300f, 3200f, 0.22f, 0.001f, 0.012f);
			return Finish(s, 0.12f, 0.9f);
		}

		public static float[] ClashFlesh(int variant)
		{
			Noise noise = new Noise(variant * 157 + 97);
			float[] s = New(0.35f);
			float num = 1f + noise.Next() * 0.08f;
			Brush(s, noise, 0f, 900f, 5200f, 0.55f, 0.0006f, 0.006f);
			Thud(s, 0.002f, 92f * num, 0.85f, 0.045f, 0.6f);
			Knock(s, noise, 0f, 480f * num, 0.8f, 0.35f, 0.012f);
			Brush(s, noise, 0.004f, 80f, 700f, 0.3f, 0.003f, 0.03f);
			return Finish(s, 0.1f, 0.88f);
		}

		public static float[] ClashTink(int variant)
		{
			Noise noise = new Noise(variant * 733 + 11);
			float[] s = New(0.4f);
			float num = 1f + noise.Next() * 0.08f;
			Knock(s, noise, 0f, 5400f * num, 3f, 0.75f, 0.0018f);
			Modes(s, noise, 0.0003f, num, new float[3] { 4150f, 6900f, 9750f }, new float[3] { 0.09f, 0.055f, 0.03f }, new float[3] { 0.22f, 0.14f, 0.07f });
			Brush(s, noise, 0f, 3000f, 11000f, 0.15f, 0.0005f, 0.006f);
			return Finish(s, 0.075f, 0.85f);
		}

		public static float[] Headshot(int variant)
		{
			Noise noise = new Noise(variant * 541 + 23);
			float[] s = New(0.3f);
			float num = 1f + noise.Next() * 0.06f;
			Knock(s, noise, 0f, 2500f * num, 2.6f, 0.95f, 0.004f);
			Brush(s, noise, 0f, 4000f, 11000f, 0.3f, 0.0005f, 0.005f);
			Thud(s, 0.001f, 190f * num, 0.6f, 0.022f, 0.4f);
			Modes(s, noise, 0.0008f, num, new float[3] { 1850f, 2830f, 4300f }, new float[3] { 0.05f, 0.035f, 0.02f }, new float[3] { 0.16f, 0.1f, 0.05f });
			return Finish(s, 0.11f, 0.9f);
		}

		private static float[] Scale(float[] ratios, float f0)
		{
			float[] array = new float[ratios.Length];
			for (int i = 0; i < ratios.Length; i++)
			{
				array[i] = Math.Min(ratios[i] * f0, 19845f);
			}
			return array;
		}

		public static float[] CompassOpen(int variant)
		{
			Noise n = new Noise(variant * 311 + 29);
			float[] s = New(0.4f);
			float num = 1f + 0.04f * (float)variant;
			Knock(s, n, 0f, 3400f * num, 4f, 0.35f, 0.004f);
			Brush(s, n, 0.004f, 2500f, 7000f, 0.06f, 0.003f, 0.03f);
			float num2 = 0.07f + 0.006f * (float)variant;
			Knock(s, n, num2, 2600f * num, 4f, 0.5f, 0.006f);
			Knock(s, n, num2 + 0.0015f, 1200f * num, 2f, 0.2f, 0.01f);
			Modes(s, n, num2, num, new float[4] { 2310f, 3870f, 5420f, 7600f }, new float[4] { 0.16f, 0.1f, 0.07f, 0.04f }, new float[4] { 0.05f, 0.035f, 0.025f, 0.012f });
			Knock(s, n, num2 + 0.05f, 2600f * num, 4f, 0.12f, 0.005f);
			return Finish(s, 0.05f, 0.55f);
		}

		public static float[] CompassShut(int variant)
		{
			Noise n = new Noise(variant * 523 + 7);
			float[] s = New(0.35f);
			float num = 1f + 0.035f * (float)variant;
			Brush(s, n, 0f, 1500f, 5000f, 0.05f, 0.01f, 0.02f);
			Knock(s, n, 0.012f, 2900f * num, 5f, 0.6f, 0.005f);
			Knock(s, n, 0.0135f, 1500f * num, 2.5f, 0.35f, 0.009f);
			Thud(s, 0.012f, 220f, 0.18f, 0.02f);
			Modes(s, n, 0.012f, num, new float[3] { 2480f, 4120f, 6100f }, new float[3] { 0.08f, 0.05f, 0.03f }, new float[3] { 0.03f, 0.02f, 0.012f });
			return Finish(s, 0.06f, 0.6f);
		}

		public static float[] CompassTick(int variant)
		{
			Noise n = new Noise(variant * 97 + 13);
			float[] s = New(0.12f);
			Knock(s, n, 0f, 4200f + 300f * (float)variant, 6f, 0.4f, 0.0025f);
			Modes(s, n, 0f, 1f, new float[2] { 3300f, 5100f }, new float[2] { 0.03f, 0.02f }, new float[2] { 0.015f, 0.008f });
			return Finish(s, 0.03f, 0.35f);
		}

		private static float[] New(float seconds)
		{
			return new float[Math.Max(1, (int)(seconds * 44100f))];
		}

		private static float Env(float t, float attack, float decay)
		{
			if (!(t < 0f))
			{
				if (!(t < attack))
				{
					return (float)Math.Exp((0f - (t - attack)) / decay);
				}
				return t / attack;
			}
			return 0f;
		}

		private static int At(float seconds)
		{
			return (int)(seconds * 44100f);
		}

		private static void Thud(float[] s, float start, float hz, float amp, float decay, float drop = 0.7f)
		{
			double num = 0.0;
			int num2 = At(start);
			int num3 = Math.Min(s.Length - num2, At(decay * 7f + 0.01f));
			for (int i = 0; i < num3; i++)
			{
				float num4 = (float)i / 44100f;
				float num5 = hz * (1f + drop * (float)Math.Exp((0f - num4) / 0.012f));
				num += Math.PI * 2.0 * (double)num5 / 44100.0;
				s[num2 + i] += (float)Math.Sin(num) * amp * Env(num4, 0.0025f, decay);
			}
		}

		private static void Brush(float[] s, Noise n, float start, float lo, float hi, float amp, float attack, float decay)
		{
			Biquad biquad = Biquad.HighPass(lo);
			Biquad biquad2 = Biquad.LowPass(hi);
			Biquad biquad3 = Biquad.LowPass(hi);
			int num = At(start);
			int num2 = Math.Min(s.Length - num, At(attack + decay * 6f));
			for (int i = 0; i < num2; i++)
			{
				float t = (float)i / 44100f;
				s[num + i] += biquad3.Run(biquad2.Run(biquad.Run(n.Next()))) * amp * Env(t, attack, decay);
			}
		}

		private static void Knock(float[] s, Noise n, float start, float hz, float q, float amp, float decay, float attack = 0.0006f)
		{
			Biquad biquad = Biquad.BandPass(hz, q);
			int num = At(start);
			int num2 = Math.Min(s.Length - num, At(attack + decay * 7f));
			float num3 = 1.6f + q * 0.8f;
			for (int i = 0; i < num2; i++)
			{
				float t = (float)i / 44100f;
				s[num + i] += biquad.Run(n.Next()) * amp * num3 * Env(t, attack, decay);
			}
		}

		private static void Modes(float[] s, Noise n, float start, float pitch, float[] hz, float[] decay, float[] amp)
		{
			int num = At(start);
			for (int i = 0; i < hz.Length; i++)
			{
				double num2 = Math.PI * 2.0 * (double)hz[i] * (double)pitch / 44100.0;
				double num3 = (double)n.Unit() * 6.283;
				int num4 = Math.Min(s.Length - num, At(decay[i] * 7f + 0.005f));
				for (int j = 0; j < num4; j++)
				{
					float t = (float)j / 44100f;
					s[num + j] += (float)Math.Sin(num3 + num2 * (double)j) * amp[i] * Env(t, 0.0008f, decay[i]);
				}
			}
		}

		private static void Grains(float[] s, Noise n, float start, float span, float perSecond, float lo, float hi, float amp, float grain, bool swell = false)
		{
			int num = (int)(span * perSecond);
			for (int i = 0; i < num; i++)
			{
				float num2 = n.Unit();
				float num3 = start + span * (swell ? ((float)Math.Sqrt(num2)) : (num2 * num2));
				float val = (swell ? (Math.Min(1f, (num3 - start) / (span * 0.6f)) * (1f - Math.Max(0f, (num3 - start - span * 0.8f) / (span * 0.2f)))) : (1f - (num3 - start) / span));
				float num4 = amp * (0.25f + 0.75f * n.Unit()) * Math.Max(0f, val);
				Biquad biquad = Biquad.BandPass(n.Log(lo, hi), 2.2f);
				float num5 = grain * (0.5f + n.Unit());
				int num6 = At(num3);
				int num7 = Math.Min(s.Length - num6, At(num5 * 6f));
				for (int j = 0; j < num7; j++)
				{
					s[num6 + j] += biquad.Run(n.Next()) * num4 * 3.5f * (float)Math.Exp((float)(-j) / (num5 * 44100f));
				}
			}
		}

		private static void Creak(float[] s, Noise n, float start, float dur, float amp, float hz = 70f)
		{
			Biquad biquad = Biquad.BandPass(1100f, 3f);
			Biquad biquad2 = Biquad.BandPass(2300f, 4f);
			double num = 0.0;
			float num2 = n.Range(20f, 45f);
			int num3 = At(start);
			int num4 = Math.Min(s.Length - num3, At(dur));
			for (int i = 0; i < num4; i++)
			{
				float num5 = (float)i / 44100f;
				float num6 = num5 / dur;
				float num7 = hz * (0.8f + 0.5f * num6) * (1f + 0.15f * (float)Math.Sin(num5 * num2));
				num += (double)(num7 / 44100f);
				float x = ((num - Math.Floor(num) < 0.08) ? 1f : 0f);
				float num8 = (float)Math.Sin(Math.PI * (double)num6);
				s[num3 + i] += (biquad.Run(x) * 2.5f + biquad2.Run(x) * 1.2f) * amp * num8 * num8;
			}
		}

		private static void Bubbles(float[] s, Noise n, float start, float span, int count, float lo, float hi, float amp)
		{
			for (int i = 0; i < count; i++)
			{
				float seconds = start + span * n.Unit();
				float num = n.Log(lo, hi);
				float num2 = n.Range(1.3f, 2.2f);
				float seconds2 = n.Range(0.012f, 0.035f);
				float num3 = amp * n.Range(0.3f, 1f);
				double num4 = 0.0;
				int num5 = At(seconds);
				int num6 = Math.Min(s.Length - num5, At(seconds2));
				for (int j = 0; j < num6; j++)
				{
					float num7 = (float)j / (float)num6;
					num4 += Math.PI * 2.0 * (double)num * (double)(1f + (num2 - 1f) * num7) / 44100.0;
					s[num5 + j] += (float)Math.Sin(num4) * num3 * (float)Math.Sin(Math.PI * (double)num7) * (1f - num7);
				}
			}
		}

		private static void Sweep(float[] s, Noise n, float start, float dur, float f0, float fPeak, float f1, float q, float amp, float peakAt, float shape = 1.6f)
		{
			Biquad biquad = default(Biquad);
			Biquad biquad2 = default(Biquad);
			Biquad biquad3 = default(Biquad);
			int num = At(start);
			int num2 = Math.Min(s.Length - num, At(dur));
			for (int i = 0; i < num2; i++)
			{
				float num3 = (float)i / (float)num2;
				float num4 = ((num3 < peakAt) ? (num3 / peakAt) : (1f - (num3 - peakAt) / (1f - peakAt)));
				float num5 = ((num3 < peakAt) ? (f0 + (fPeak - f0) * (num3 / peakAt)) : (fPeak + (f1 - fPeak) * ((num3 - peakAt) / (1f - peakAt))));
				if ((i & 0xF) == 0)
				{
					biquad.SetBandPass(num5, q);
					biquad2.SetBandPass(num5, q);
					biquad3.SetBandPass(num5 * 0.5f, q * 0.8f);
				}
				float num6 = (float)Math.Pow(Math.Max(0f, (float)Math.Sin(Math.PI / 2.0 * (double)num4)), shape);
				float x = n.Next();
				s[num + i] += (biquad2.Run(biquad.Run(x)) * 3.2f + biquad3.Run(x) * 0.9f) * amp * num6;
			}
		}

		private static void Pluck(float[] s, Noise n, float start, float hz, float amp, float damping, float bright)
		{
			int num = Math.Max(2, (int)(44100f / hz));
			float[] array = new float[num];
			Biquad biquad = Biquad.LowPass(bright);
			for (int i = 0; i < num; i++)
			{
				array[i] = biquad.Run(n.Next());
			}
			int num2 = At(start);
			int num3 = 0;
			float num4 = 0f;
			for (int j = num2; j < s.Length; j++)
			{
				float num5 = array[num3];
				float num6 = (num5 + num4) * 0.5f * damping;
				num4 = num5;
				array[num3] = num6;
				num3 = (num3 + 1) % num;
				float num7 = (float)(j - num2) / 44100f;
				s[j] += num5 * amp * Math.Min(1f, num7 / 0.001f);
			}
		}

		private static void Slap(float[] s, float delay, float gain)
		{
			int num = At(delay);
			int num2 = At(delay * 1.7f);
			for (int num3 = s.Length - 1; num3 >= num; num3--)
			{
				s[num3] += s[num3 - num] * gain;
				if (num3 >= num2)
				{
					s[num3] += s[num3 - num2] * gain * 0.5f;
				}
			}
		}

		private static void LowPass(float[] s, float hz)
		{
			Biquad biquad = Biquad.LowPass(hz);
			for (int i = 0; i < s.Length; i++)
			{
				s[i] = biquad.Run(s[i]);
			}
		}

		private static void HighPass(float[] s, float hz)
		{
			Biquad biquad = Biquad.HighPass(hz);
			for (int i = 0; i < s.Length; i++)
			{
				s[i] = biquad.Run(s[i]);
			}
		}

		private static float[] Finish(float[] s, float rms, float peak)
		{
			int num = s.Length;
			while (num > 882 && Math.Abs(s[num - 1]) < 0.0001f)
			{
				num--;
			}
			if (num < s.Length)
			{
				Array.Resize(ref s, num);
			}
			int num2 = Math.Min(s.Length, 1102);
			for (int i = 0; i < num2; i++)
			{
				s[s.Length - 1 - i] *= (float)i / (float)num2;
			}
			int j = 0;
			for (int num3 = Math.Min(s.Length, 32); j < num3; j++)
			{
				s[j] *= (float)j / 32f;
			}
			int num4 = Math.Min(s.Length, At(0.1f));
			double num5 = 0.0;
			for (int k = 0; k + num4 <= s.Length; k += At(0.01f))
			{
				double num6 = 0.0;
				for (int l = k; l < k + num4; l++)
				{
					num6 += (double)(s[l] * s[l]);
				}
				if (num6 > num5)
				{
					num5 = num6;
				}
			}
			float num7 = (float)Math.Sqrt(num5 / (double)Math.Max(1, num4));
			float num8 = ((num7 > 1E-06f) ? (rms / num7) : 1f);
			for (int m = 0; m < s.Length; m++)
			{
				float num9 = s[m] * num8;
				s[m] = ((Math.Abs(num9) > peak * 0.7f) ? ((float)Math.Sign(num9) * (peak * 0.7f + peak * 0.3f * (float)Math.Tanh((Math.Abs(num9) - peak * 0.7f) / (peak * 0.3f)))) : num9);
			}
			return s;
		}

		private static void Normalize(float[] s, float peak)
		{
			float num = 1E-06f;
			foreach (float value in s)
			{
				num = Math.Max(num, Math.Abs(value));
			}
			float num2 = peak / num;
			for (int j = 0; j < s.Length; j++)
			{
				s[j] *= num2;
			}
		}

		public static float[] Step(Ground ground, int variant)
		{
			if (ground == Ground.Shallows)
			{
				return Wade(variant);
			}
			Noise noise = new Noise(variant * 7919 + (int)ground * 104729 + 1);
			float[] s = New(0.36f);
			float num = 1f + noise.Next() * 0.08f;
			float num2 = 0.05f + noise.Unit() * 0.035f;
			float num3 = 0.85f + 0.3f * noise.Unit();
			float rms = 0.08f;
			switch (ground)
			{
			case Ground.Grass:
				Thud(s, 0f, 60f * num, 0.45f * num3, 0.03f);
				Brush(s, noise, 0f, 900f, 4200f, 0.5f * num3, 0.008f, 0.035f);
				Grains(s, noise, 0.004f, 0.09f, 240f, 1800f, 6000f, 0.22f, 0.0012f);
				Brush(s, noise, num2, 1100f, 4800f, 0.3f, 0.006f, 0.03f);
				Grains(s, noise, num2, 0.07f, 160f, 2200f, 6500f, 0.14f, 0.001f);
				LowPass(s, 5500f);
				rms = 0.075f;
				break;
			case Ground.Sand:
				Thud(s, 0f, 55f * num, 0.3f * num3, 0.035f);
				Grains(s, noise, 0f, 0.13f, 950f, 1400f, 5200f, 0.3f, 0.0011f);
				Brush(s, noise, 0f, 700f, 3200f, 0.22f, 0.012f, 0.05f);
				Grains(s, noise, num2, 0.1f, 700f, 1600f, 5600f, 0.2f, 0.001f);
				LowPass(s, 6000f);
				rms = 0.07f;
				break;
			case Ground.Soil:
				Thud(s, 0f, 70f * num, 0.7f * num3, 0.035f);
				Knock(s, noise, 0f, 240f * num, 1.1f, 0.5f * num3, 0.022f, 0.002f);
				Grains(s, noise, 0.003f, 0.08f, 130f, 900f, 3400f, 0.24f, 0.0016f);
				Knock(s, noise, num2, 280f * num, 1.1f, 0.28f, 0.018f, 0.002f);
				Grains(s, noise, num2, 0.05f, 90f, 1000f, 3600f, 0.14f, 0.0014f);
				LowPass(s, 5000f);
				rms = 0.08f;
				break;
			case Ground.Rock:
				Thud(s, 0f, 92f * num, 0.4f * num3, 0.022f);
				Knock(s, noise, 0f, 1900f * num, 0.9f, 0.4f * num3, 0.0035f);
				Modes(s, noise, 0f, num, new float[3] { 1180f, 2290f, 3850f }, new float[3] { 0.016f, 0.011f, 0.007f }, new float[3] { 0.1f, 0.07f, 0.04f });
				Grains(s, noise, 0.002f, 0.06f, 260f, 2000f, 6500f, 0.14f, 0.0007f);
				Knock(s, noise, num2, 2200f * num, 0.9f, 0.24f, 0.003f);
				Grains(s, noise, num2, 0.05f, 200f, 2400f, 7000f, 0.1f, 0.0007f);
				Slap(s, 0.011f, 0.18f);
				LowPass(s, 6000f);
				rms = 0.065f;
				break;
			case Ground.Snow:
				Thud(s, 0f, 50f * num, 0.25f * num3, 0.045f);
				Grains(s, noise, 0f, 0.15f, 1100f, 550f, 3000f, 0.34f, 0.0017f, swell: true);
				Grains(s, noise, num2 + 0.015f, 0.12f, 800f, 650f, 3300f, 0.22f, 0.0015f, swell: true);
				LowPass(s, 4800f);
				rms = 0.075f;
				break;
			case Ground.Wood:
				Thud(s, 0f, 82f * num, 0.5f * num3, 0.045f);
				Modes(s, noise, 0f, num, new float[4] { 158f, 334f, 581f, 905f }, new float[4] { 0.065f, 0.04f, 0.028f, 0.018f }, new float[4] { 0.32f, 0.18f, 0.1f, 0.05f });
				Knock(s, noise, 0f, 1500f * num, 1.2f, 0.18f * num3, 0.005f);
				Modes(s, noise, num2, num * 1.06f, new float[3] { 172f, 360f, 640f }, new float[3] { 0.045f, 0.03f, 0.02f }, new float[3] { 0.16f, 0.09f, 0.05f });
				Knock(s, noise, num2, 1700f * num, 1.2f, 0.1f, 0.004f);
				if (variant % 4 == 3)
				{
					Creak(s, noise, 0.07f, 0.18f, 0.05f, 65f + 20f * noise.Unit());
				}
				LowPass(s, 5200f);
				rms = 0.085f;
				break;
			}
			return Finish(s, rms, 0.75f);
		}

		public static float[] Wade(int variant)
		{
			Noise n = new Noise(variant * 31 + 977);
			float[] s = New(0.45f);
			Sweep(s, n, 0f, 0.32f, 500f, 1500f, 450f, 1.3f, 0.6f, 0.3f);
			Brush(s, n, 0.01f, 1500f, 4500f, 0.08f, 0.02f, 0.06f);
			Bubbles(s, n, 0.04f, 0.26f, 7, 600f, 1600f, 0.22f);
			Thud(s, 0f, 60f, 0.2f, 0.04f);
			LowPass(s, 4500f);
			return Finish(s, 0.085f, 0.75f);
		}

		public static float[] Land(Ground ground, int variant)
		{
			float[] array = Step(ground, variant + 40);
			Noise n = new Noise(variant * 211 + 3);
			float[] array2 = New(0.45f);
			Thud(array2, 0f, 52f, 1f, 0.07f, 0.9f);
			Brush(array2, n, 0.01f, 500f, 2600f, 0.2f, 0.01f, 0.06f);
			for (int i = 0; i < array.Length && i < array2.Length; i++)
			{
				array2[i] += array[i] * 1.6f;
			}
			if (ground == Ground.Shallows)
			{
				Bubbles(array2, n, 0.05f, 0.25f, 10, 500f, 1500f, 0.3f);
			}
			return Finish(array2, 0.12f, 0.85f);
		}

		public static float[] Cloth(int variant)
		{
			Noise n = new Noise(variant * 401 + 17);
			float[] s = New(0.2f);
			Brush(s, n, 0f, 600f, 3000f, 0.4f, 0.03f, 0.04f);
			Grains(s, n, 0.01f, 0.1f, 70f, 1500f, 4000f, 0.12f, 0.002f);
			return Finish(s, 0.05f, 0.5f);
		}

		public static float[] Splash(int variant, float seconds, float peakAt)
		{
			Noise n = new Noise(variant * 31 + 977);
			float[] s = New(seconds + 0.1f);
			Sweep(s, n, 0f, seconds, 600f, 2600f, 400f, 0.9f, 0.7f, peakAt, 1.2f);
			Brush(s, n, 0f, 1500f, 6000f, 0.18f, 0.004f, seconds * 0.12f);
			Bubbles(s, n, seconds * 0.1f, seconds * 0.8f, (int)(seconds * 40f), 450f, 1500f, 0.26f);
			Thud(s, 0f, 55f, 0.35f, 0.06f);
			LowPass(s, 5500f);
			return Finish(s, 0.11f, 0.85f);
		}

		public static float[] SwimStroke(int variant)
		{
			Noise n = new Noise(variant * 97 + 5);
			float[] s = New(0.7f);
			Sweep(s, n, 0f, 0.6f, 350f, 1100f, 300f, 1.1f, 0.6f, 0.4f, 1.3f);
			Bubbles(s, n, 0.15f, 0.45f, 9, 450f, 1300f, 0.2f);
			return Finish(s, 0.075f, 0.7f);
		}

		public static float[] Swing(Swish kind, int variant)
		{
			Noise noise = new Noise(variant * 53 + (int)kind * 7717 + 11);
			float num = 1f + noise.Next() * 0.08f;
			float[] s = New(0.6f);
			switch (kind)
			{
			case Swish.Fist:
				Sweep(s, noise, 0f, 0.16f, 450f * num, 1700f * num, 600f * num, 1.1f, 0.6f, 0.4f);
				Brush(s, noise, 0f, 500f, 2400f, 0.15f, 0.02f, 0.03f);
				LowPass(s, 4000f);
				return Finish(s, 0.07f, 0.7f);
			case Swish.Light:
				Sweep(s, noise, 0f, 0.22f, 550f * num, 2400f * num, 800f * num, 1.2f, 0.6f, 0.42f);
				LowPass(s, 5000f);
				return Finish(s, 0.075f, 0.7f);
			case Swish.Blade:
				Sweep(s, noise, 0f, 0.3f, 650f * num, 3000f * num, 900f * num, 1.1f, 0.6f, 0.42f);
				Sweep(s, noise, 0.03f, 0.22f, 2000f * num, 4200f * num, 2600f * num, 9f, 0.35f, 0.45f, 2f);
				LowPass(s, 6500f);
				return Finish(s, 0.08f, 0.75f);
			case Swish.Heavy:
				Sweep(s, noise, 0f, 0.42f, 200f * num, 900f * num, 260f * num, 0.9f, 0.7f, 0.5f, 1.4f);
				Sweep(s, noise, 0.1f, 0.22f, 700f * num, 1600f * num, 600f * num, 1.4f, 0.25f, 0.5f);
				Thud(s, 0.17f, 48f, 0.12f, 0.06f, 0.2f);
				LowPass(s, 3000f);
				return Finish(s, 0.09f, 0.8f);
			default:
				Sweep(s, noise, 0f, 0.2f, 700f * num, 2600f * num, 1400f * num, 1.3f, 0.6f, 0.3f);
				Brush(s, noise, 0f, 600f, 2600f, 0.12f, 0.01f, 0.03f);
				LowPass(s, 5000f);
				return Finish(s, 0.075f, 0.7f);
			}
		}

		public static float[] Whoosh(int variant)
		{
			return Swing(Swish.Light, variant);
		}

		public static float[] BowRelease(int variant)
		{
			Noise noise = new Noise(variant * 131 + 7);
			float[] s = New(0.5f);
			float num = 1f + noise.Next() * 0.06f;
			Pluck(s, noise, 0f, 104f * num, 0.7f, 0.993f, 2400f);
			Pluck(s, noise, 0f, 208f * num, 0.18f, 0.985f, 3000f);
			Modes(s, noise, 0f, num, new float[3] { 240f, 610f, 1150f }, new float[3] { 0.03f, 0.02f, 0.012f }, new float[3] { 0.35f, 0.2f, 0.1f });
			Knock(s, noise, 0f, 900f, 1f, 0.3f, 0.006f);
			Sweep(s, noise, 0.01f, 0.18f, 1500f, 3800f, 2400f, 2f, 0.12f, 0.25f);
			HighPass(s, 60f);
			LowPass(s, 6000f);
			return Finish(s, 0.11f, 0.85f);
		}

		public static float[] BowDraw(int variant)
		{
			Noise noise = new Noise(variant * 173 + 29);
			float[] s = New(0.5f);
			Knock(s, noise, 0f, 2600f, 2f, 0.12f, 0.006f);
			Creak(s, noise, 0.05f, 0.42f, 0.4f, 55f + 15f * noise.Unit());
			Brush(s, noise, 0.05f, 700f, 2500f, 0.08f, 0.15f, 0.12f);
			return Finish(s, 0.05f, 0.6f);
		}

		public static float[] Zap(int variant)
		{
			Noise n = new Noise(variant * 59 + 3);
			float[] array = New(0.35f);
			double num = 0.0;
			double num2 = 0.0;
			for (int i = 0; i < array.Length; i++)
			{
				float num3 = (float)i / 44100f;
				float num4 = 180f + 2600f * (float)Math.Exp((0f - num3) / 0.035f);
				num2 += Math.PI * 2.0 * (double)num4 * 2.5 / 44100.0;
				num += Math.PI * 2.0 * (double)num4 / 44100.0 + Math.Sin(num2) * 0.9 * Math.Exp((double)(0f - num3) / 0.08);
				array[i] += (float)Math.Sin(num) * 0.5f * Env(num3, 0.001f, 0.07f);
			}
			Knock(array, n, 0f, 3000f, 0.8f, 0.3f, 0.01f);
			Thud(array, 0f, 70f, 0.5f, 0.05f);
			return Finish(array, 0.1f, 0.8f);
		}

		public static float[] Cast(int variant)
		{
			Noise noise = new Noise(variant * 61 + 41);
			float[] array = New(0.9f);
			Sweep(array, noise, 0f, 0.5f, 300f, 1800f, 900f, 1.2f, 0.45f, 0.6f, 1.2f);
			float num = noise.Range(700f, 900f);
			float[] array2 = new float[6] { 1f, 1.5f, 2f, 2.52f, 3f, 4f };
			for (int i = 0; i < array2.Length; i++)
			{
				float seconds = 0.05f + (float)i * 0.045f;
				double num2 = Math.PI * 2.0 * (double)num * (double)array2[i] / 44100.0;
				int num3 = At(seconds);
				for (int j = num3; j < array.Length; j++)
				{
					float num4 = (float)(j - num3) / 44100f;
					float num5 = 0.75f + 0.25f * (float)Math.Sin(num4 * 38f + (float)i);
					array[j] += (float)Math.Sin(num2 * (double)(j - num3)) * 0.1f * num5 * Env(num4, 0.01f, 0.18f);
				}
			}
			return Finish(array, 0.07f, 0.7f);
		}

		public static float[] Equip(bool metal, int variant)
		{
			Noise noise = new Noise(variant * 89 + (metal ? 5 : 9));
			float[] s = New(metal ? 0.8f : 0.4f);
			Brush(s, noise, 0f, 600f, 3000f, 0.25f, 0.03f, 0.05f);
			if (metal)
			{
				Sweep(s, noise, 0.06f, 0.28f, 2400f, 6200f, 5200f, 6f, 0.3f, 0.75f, 1f);
				Modes(s, noise, 0.3f, 1f + noise.Next() * 0.04f, new float[4] { 2140f, 3390f, 4720f, 6260f }, new float[4] { 0.35f, 0.25f, 0.18f, 0.12f }, new float[4] { 0.06f, 0.045f, 0.03f, 0.02f });
			}
			else
			{
				Modes(s, noise, 0.08f, 1f, new float[3] { 320f, 780f, 1500f }, new float[3] { 0.04f, 0.025f, 0.015f }, new float[3] { 0.3f, 0.15f, 0.07f });
			}
			return Finish(s, 0.05f, 0.6f);
		}

		public static float[] Hit(int kind, bool kill, int variant)
		{
			Noise noise = new Noise(variant * 101 + kind * 13 + (kill ? 777 : 0));
			float[] s = New(kill ? 0.7f : 0.35f);
			float num = 1f + noise.Next() * 0.08f;
			Knock(s, noise, 0f, 1400f * num, 0.8f, 0.6f, 0.007f);
			Thud(s, 0f, ((kind == 2) ? 75f : 100f) * num, 0.9f, (kind == 2) ? 0.06f : 0.045f, 1f);
			Brush(s, noise, 0f, 80f, 700f, 0.5f, 0.002f, 0.03f);
			if (kind == 1)
			{
				Brush(s, noise, 0f, 3000f, 9000f, 0.25f, 0.002f, 0.018f);
				Modes(s, noise, 0.002f, num, new float[2] { 3100f, 4700f }, new float[2] { 0.05f, 0.03f }, new float[2] { 0.04f, 0.025f });
			}
			if (kind == 2)
			{
				Grains(s, noise, 0.003f, 0.05f, 400f, 700f, 2500f, 0.25f, 0.002f);
			}
			if (kill)
			{
				Thud(s, 0.01f, 46f, 0.8f, 0.16f, 0.5f);
				Brush(s, noise, 0.02f, 60f, 400f, 0.4f, 0.01f, 0.12f);
			}
			return Finish(s, kill ? 0.16f : 0.13f, 0.9f);
		}

		public static float[] Hurt(int variant)
		{
			Noise noise = new Noise(variant * 7 + 61);
			float[] s = New(0.45f);
			Thud(s, 0f, 70f + 10f * noise.Next(), 1f, 0.09f, 0.8f);
			Brush(s, noise, 0f, 60f, 350f, 0.7f, 0.003f, 0.06f);
			Knock(s, noise, 0f, 600f, 0.7f, 0.25f, 0.012f);
			return Finish(s, 0.14f, 0.9f);
		}

		public static float[] Heartbeat()
		{
			Noise n = new Noise(77);
			float[] array = New(0.55f);
			Thud(array, 0f, 48f, 1f, 0.05f, 0.5f);
			Brush(array, n, 0f, 30f, 160f, 0.4f, 0.006f, 0.04f);
			Thud(array, 0.2f, 58f, 0.7f, 0.045f, 0.5f);
			Brush(array, n, 0.2f, 30f, 180f, 0.25f, 0.005f, 0.035f);
			Normalize(array, 0.95f);
			return array;
		}

		public static float[] Wind(float seconds)
		{
			Noise noise = new Noise(4242);
			float[] array = New(seconds);
			Biquad biquad = Biquad.LowPass(300f);
			Biquad biquad2 = Biquad.LowPass(300f);
			Biquad biquad3 = default(Biquad);
			for (int i = 0; i < array.Length; i++)
			{
				float num = (float)i / (float)array.Length;
				float num2 = 0.55f + 0.28f * (float)Math.Sin(Math.PI * 4.0 * (double)num) + 0.17f * (float)Math.Sin(Math.PI * 10.0 * (double)num + 1.3);
				if ((i & 0x1F) == 0)
				{
					biquad.SetLowPass(160f + 380f * num2, 0.7f);
					biquad3.SetBandPass(520f + 420f * (float)Math.Sin(Math.PI * 6.0 * (double)num + 0.4) * num2, 6f);
				}
				float x = noise.Next();
				float num3 = biquad2.Run(biquad.Run(x));
				float num4 = biquad3.Run(x) * Math.Max(0f, num2 - 0.55f) * 2.2f;
				array[i] = num3 * num2 * 2.2f + num4;
			}
			array = Crossfade(array, 14700);
			Normalize(array, 0.8f);
			return array;
		}

		public static float[] Rain(float seconds)
		{
			Noise noise = new Noise(3131);
			float[] array = New(seconds);
			Biquad biquad = Biquad.HighPass(500f);
			Biquad biquad2 = Biquad.LowPass(4200f);
			for (int i = 0; i < array.Length; i++)
			{
				float num = (float)i / (float)array.Length;
				array[i] = biquad2.Run(biquad.Run(noise.Next())) * 0.35f * (0.85f + 0.15f * (float)Math.Sin(Math.PI * 6.0 * (double)num));
			}
			Grains(array, noise, 0f, seconds - 0.05f, 700f, 1800f, 6000f, 0.22f, 0.0012f);
			Grains(array, noise, 0f, seconds - 0.05f, 24f, 900f, 2400f, 0.5f, 0.004f);
			LowPass(array, 7000f);
			array = Crossfade(array, 11025);
			Normalize(array, 0.7f);
			return array;
		}

		public static float[] Fire(float seconds)
		{
			Noise noise = new Noise(1717);
			float[] array = New(seconds);
			Biquad biquad = Biquad.LowPass(220f);
			for (int i = 0; i < array.Length; i++)
			{
				float num = (float)i / (float)array.Length;
				float num2 = 0.75f + 0.25f * (float)Math.Sin(Math.PI * 8.0 * (double)num) * (float)Math.Sin(Math.PI * 14.0 * (double)num + 0.6);
				array[i] = biquad.Run(noise.Next()) * 2.4f * num2;
			}
			for (int j = 0; j < (int)(seconds * 5f); j++)
			{
				Grains(array, noise, noise.Unit() * (seconds - 0.2f), 0.12f, 120f, 1200f, 6000f, 0.6f, 0.0006f);
			}
			for (int k = 0; k < (int)(seconds * 0.8f); k++)
			{
				Knock(array, noise, noise.Unit() * (seconds - 0.1f), noise.Range(300f, 700f), 2f, 0.5f, 0.01f);
			}
			array = Crossfade(array, 8820);
			Normalize(array, 0.8f);
			return array;
		}

		public static float[] WarHorn()
		{
			float[] array = New(2.4f);
			float num = 0f;
			float num2 = 0f;
			Biquad biquad = default(Biquad);
			for (int i = 0; i < array.Length; i++)
			{
				float num3 = (float)i / 44100f;
				float num4 = ((num3 < 0.3f) ? (num3 / 0.3f) : ((num3 > 1.7f) ? ((2.4f - num3) / 0.7f) : 1f));
				num4 = num4 * num4 * (3f - 2f * num4);
				float num5 = 104f * ((num3 < 0.2f) ? (0.93f + 0.07f * num3 / 0.2f) : 1f) * (1f + 0.012f * (float)Math.Sin((double)(num3 * 2f) * Math.PI * 5.199999809265137));
				num += num5 / 44100f;
				num2 += num5 * 0.5f / 44100f;
				float x = 2f * (num - (float)Math.Floor(num)) - 1f;
				if ((i & 0x1F) == 0)
				{
					biquad.SetLowPass(250f + 1600f * num4, 0.9f);
				}
				array[i] = (biquad.Run(x) + 0.35f * (float)Math.Sin(Math.PI * 2.0 * (double)num2)) * num4;
			}
			Normalize(array, 0.9f);
			return array;
		}

		public static float[] Rustle()
		{
			Noise n = new Noise(5);
			float[] s = New(0.4f);
			Brush(s, n, 0f, 800f, 4000f, 0.3f, 0.05f, 0.06f);
			Grains(s, n, 0.02f, 0.25f, 120f, 1500f, 5000f, 0.2f, 0.002f);
			return Finish(s, 0.06f, 0.6f);
		}

		private static float[] Crossfade(float[] s, int length)
		{
			length = Math.Min(length, s.Length / 2);
			for (int i = 0; i < length; i++)
			{
				double num = ((double)i + 0.5) / (double)length * Math.PI * 0.5;
				int num2 = s.Length - length + i;
				s[i] = s[i] * (float)Math.Sin(num) + s[num2] * (float)Math.Cos(num);
			}
			Array.Resize(ref s, s.Length - length);
			return s;
		}

		public static byte[] ToPcm16(float[] s)
		{
			byte[] array = new byte[s.Length * 2];
			for (int i = 0; i < s.Length; i++)
			{
				int num = (int)(Math.Max(-1f, Math.Min(1f, s[i])) * 32767f);
				array[i * 2] = (byte)(num & 0xFF);
				array[i * 2 + 1] = (byte)((num >> 8) & 0xFF);
			}
			return array;
		}

		private static void Strike(float[] s, Noise n, WoodBody body, float start, float contact, float amp, float spread = 0.25f)
		{
			int num = Math.Max(2, At(contact));
			int num2 = At(start);
			if (num2 >= s.Length)
			{
				return;
			}
			double num3 = Math.PI / (2.0 * (double)num);
			for (int i = 0; i < body.Hz.Length; i++)
			{
				float num4 = body.Hz[i];
				if (!(num4 > 19845f))
				{
					double num5 = Math.PI * 2.0 * (double)num4 / 44100.0;
					double num6 = Math.Exp(-1.0 / (double)(body.Tau[i] * 44100f));
					double num7 = 2.0 * num6 * Math.Cos(num5);
					double num8 = (0.0 - num6) * num6;
					double num9 = Math.Sin(num5) * (double)body.Amp[i] * (double)amp * (double)(1f + spread * n.Next());
					int num10 = Math.Min(s.Length - num2, num + At(body.Tau[i] * 7f));
					double num11 = 0.0;
					double num12 = 0.0;
					for (int j = 0; j < num10; j++)
					{
						double num13 = ((j < num) ? (Math.Sin(Math.PI * ((double)j + 0.5) / (double)num) * num3) : 0.0);
						double num14 = num7 * num11 + num8 * num12 + num13 * num9;
						s[num2 + j] += (float)num14;
						num12 = num11;
						num11 = num14;
					}
				}
			}
		}

		private static void Room(float[] s, float rt, float wet, float dark, float predelay = 0.006f)
		{
			float[] array = (float[])s.Clone();
			int[] array2 = new int[4]
			{
				At(0.0297f),
				At(0.0371f),
				At(0.0411f),
				At(0.0437f)
			};
			float[][] array3 = new float[4][];
			float[] array4 = new float[4];
			float[] array5 = new float[4];
			int[] array6 = new int[4];
			for (int i = 0; i < 4; i++)
			{
				array3[i] = new float[array2[i]];
				array4[i] = (float)Math.Pow(10.0, -3.0 * (double)array2[i] / 44100.0 / (double)rt);
			}
			float num = (float)Math.Exp(Math.PI * -2.0 * (double)dark / 44100.0);
			int num2 = At(predelay);
			for (int j = 0; j < s.Length; j++)
			{
				float num3 = ((j >= num2) ? array[j - num2] : 0f);
				for (int k = 0; k < 4; k++)
				{
					array5[k] = (1f - num) * array3[k][array6[k]] + num * array5[k];
				}
				float num4 = (array5[0] + array5[1] + array5[2] + array5[3]) * 0.5f;
				for (int l = 0; l < 4; l++)
				{
					array3[l][array6[l]] = num3 + array4[l] * (array5[l] - num4);
					if (++array6[l] == array2[l])
					{
						array6[l] = 0;
					}
				}
				s[j] = array[j] + wet * (array5[0] - array5[1] + array5[2] - array5[3]) * 0.5f;
			}
		}

		private static void Rattle(float[] s, Noise n, float start, int ticks, float lo, float hi, float amp, float span)
		{
			float num = start;
			for (int i = 0; i < ticks; i++)
			{
				Knock(s, n, num, n.Log(lo, hi), n.Range(5f, 12f), amp * n.Range(0.4f, 1f), n.Range(0.0012f, 0.003f), 0.0002f);
				num += span / (float)ticks * n.Range(0.4f, 1.6f);
				amp *= 0.7f;
			}
		}

		private static void Groan(float[] s, Noise n, float start, float dur, float amp, float hz0, float hz1, float lo, float hi)
		{
			Biquad biquad = Biquad.BandPass(n.Range(lo, hi), 4f);
			Biquad biquad2 = Biquad.BandPass(n.Range(lo, hi) * 1.9f, 5f);
			Biquad biquad3 = Biquad.BandPass(n.Range(lo, hi) * 0.55f, 3f);
			int num = At(start);
			int num2 = Math.Min(s.Length - num, At(dur));
			if (num2 <= 0)
			{
				return;
			}
			float num3 = n.Range(3f, 7f);
			float num4 = n.Range(0f, 6f);
			double num5 = 0.0;
			for (int i = 0; i < num2; i++)
			{
				float num6 = (float)i / 44100f;
				float num7 = num6 / dur;
				float x = 0f;
				if ((double)i >= num5)
				{
					float num8 = (hz0 + (hz1 - hz0) * num7) * (1f + 0.12f * (float)Math.Sin(num4 + num6 * num3));
					num5 = (float)i + 44100f / num8 * n.Range(0.8f, 1.2f);
					x = n.Range(0.5f, 1f);
				}
				float num9 = (float)Math.Sin(Math.PI * (double)Math.Min(1f, num7 * 1.25f));
				float num10 = biquad.Run(x) * 2f + biquad2.Run(x) * 1.1f + biquad3.Run(x) * 1.3f;
				s[num + i] += num10 * amp * num9 * num9 * (0.6f + 0.4f * n.Unit());
			}
		}

		private static WoodBody DoorLeaf(Noise n)
		{
			return new WoodBody(n, 64, 140f, 7000f, 0.035f, 0f, 0.04f, 600f);
		}

		private static WoodBody DoorPanel(Noise n)
		{
			return new WoodBody(n, 7, 55f, 190f, 0.13f, 0f, 0.05f);
		}

		public static float[] Door()
		{
			Noise noise = new Noise(99);
			WoodBody body = DoorLeaf(noise);
			WoodBody body2 = DoorPanel(noise);
			WoodBody body3 = new WoodBody(noise, 26, 120f, 3000f, 0.07f, 0.5f, 0.06f);
			WoodBody body4 = new WoodBody(noise, 10, 1400f, 7000f, 0.01f, 0.25f, 0.022f);
			float[] array = New(0.85f);
			Strike(array, noise, body4, 0.01f, 0.0002f, 0.18f, 0.3f);
			Rattle(array, noise, 0.016f, 2, 2500f, 5500f, 0.04f, 0.02f);
			Groan(array, noise, 0.07f, 0.36f, 0.035f, 50f, 85f, 600f, 1100f);
			float num = 0.47f;
			Strike(array, noise, body, num, 0.0012f, 1.6f, 0.2f);
			Strike(array, noise, body2, num, 0.006f, 1f, 0.2f);
			Strike(array, noise, body3, num + 0.002f, 0.003f, 0.9f);
			Strike(array, noise, body4, num + noise.Range(0.012f, 0.02f), 0.0002f, 0.22f, 0.3f);
			Strike(array, noise, body4, num + noise.Range(0.04f, 0.06f), 0.0002f, 0.07f, 0.3f);
			Room(array, 0.4f, 0.14f, 1500f);
			HighPass(array, 45f);
			Normalize(array, 0.85f);
			Tail(array, 1764);
			return array;
		}

		private static void Tail(float[] s, int samples)
		{
			samples = Math.Min(samples, s.Length);
			for (int i = 0; i < samples; i++)
			{
				s[s.Length - 1 - i] *= (float)i / (float)samples;
			}
		}

		public static float[] DragonWing(int variant)
		{
			Noise noise = new Noise(variant * 613 + 41);
			float[] s = New(0.9f);
			float num = 1f + noise.Next() * 0.08f;
			Sweep(s, noise, 0f, 0.55f, 90f * num, 260f * num, 70f * num, 0.7f, 1f, 0.45f, 1.3f);
			Thud(s, 0.22f, 42f * num, 0.9f, 0.14f, 0.4f);
			Thud(s, 0.22f, 70f * num, 0.4f, 0.08f, 0.5f);
			Grains(s, noise, 0.2f, 0.12f, 260f, 180f, 900f, 0.6f, 0.004f);
			Brush(s, noise, 0.21f, 250f, 1400f, 0.35f, 0.01f, 0.06f);
			LowPass(s, 2400f);
			return Finish(s, 0.16f, 0.95f);
		}

		public static float[] DragonRoar(int variant)
		{
			Noise noise = new Noise(variant * 877 + 5);
			float num = 2.4f + noise.Unit() * 0.5f;
			float[] array = New(num + 0.4f);
			float num2 = 58f + noise.Unit() * 14f;
			Biquad biquad = Biquad.BandPass(500f, 1.4f);
			Biquad biquad2 = Biquad.BandPass(1000f, 2f);
			Biquad biquad3 = Biquad.BandPass(2300f, 3f);
			Biquad biquad4 = Biquad.LowPass(1800f);
			double num3 = 0.0;
			double num4 = 0.0;
			float num5 = 0f;
			for (int i = 0; i < At(num) && i < array.Length; i++)
			{
				float num6 = (float)i / 44100f;
				float num7 = num6 / num;
				float num8 = ((num7 < 0.35f) ? (num7 / 0.35f) : (1f - (num7 - 0.35f) / 0.65f * 0.7f));
				if ((i & 0x3F) == 0)
				{
					num5 = noise.Next() * 0.08f;
				}
				float num9 = num2 * (0.85f + 0.5f * num8) * (1f + num5);
				num3 += (double)(num9 / 44100f);
				num4 += (double)(num9 * 0.5f / 44100f);
				float num10 = (float)(num3 - Math.Floor(num3));
				float num11 = ((num10 < 0.12f) ? (num10 / 0.12f) : (1f - (num10 - 0.12f) / 0.88f));
				num11 = num11 * 2f - 1f;
				float num12 = 0.7f + 0.3f * (float)Math.Sin((double)(num6 * 2f) * Math.PI * (double)(28f + 6f * num8));
				num11 *= num12;
				if ((i & 0x1F) == 0)
				{
					biquad.SetBandPass(380f + 420f * num8, 1.4f);
					biquad2.SetBandPass(800f + 700f * num8, 2f);
					biquad3.SetBandPass(2000f + 900f * num8, 3f);
				}
				float num13 = biquad.Run(num11) * 1.4f + biquad2.Run(num11) * 1f + biquad3.Run(num11) * 0.45f;
				float num14 = biquad4.Run(noise.Next()) * (0.25f + 0.35f * num8);
				float num15 = Math.Min(1f, num6 / 0.25f) * ((num7 > 0.75f) ? Math.Max(0f, 1f - (num7 - 0.75f) / 0.25f) : 1f);
				float num16 = (num13 + num14) * num15 + (float)Math.Sin(Math.PI * 2.0 * num4) * 0.35f * num15;
				array[i] += (float)Math.Tanh(num16 * 1.8f) * 0.8f;
			}
			Thud(array, 0f, 38f, 0.5f, 0.4f, 0.2f);
			HighPass(array, 28f);
			LowPass(array, 6500f);
			return Finish(array, 0.17f, 0.95f);
		}

		public static float[] DragonFire(float seconds)
		{
			Noise noise = new Noise(9191);
			float[] array = New(seconds);
			Biquad biquad = Biquad.LowPass(900f);
			Biquad biquad2 = Biquad.LowPass(240f);
			Biquad biquad3 = default(Biquad);
			double num = 0.0;
			for (int i = 0; i < array.Length; i++)
			{
				float num2 = (float)i / (float)array.Length;
				float num3 = 0.8f + 0.2f * (float)Math.Sin(Math.PI * 10.0 * (double)num2) * (float)Math.Sin(Math.PI * 6.0 * (double)num2 + 1.1);
				if ((i & 0x1F) == 0)
				{
					biquad3.SetBandPass(1400f + 600f * (float)Math.Sin(Math.PI * 8.0 * (double)num2), 1.2f);
				}
				float x = noise.Next();
				float num4 = biquad.Run(x) * 1.6f + biquad2.Run(x) * 2.6f + biquad3.Run(x) * 0.5f;
				num += 0.001179138314910233;
				float num5 = (float)((num - Math.Floor(num)) * 2.0 - 1.0) * 0.12f;
				array[i] = (num4 + num5) * num3;
			}
			for (int j = 0; j < (int)(seconds * 6f); j++)
			{
				Grains(array, noise, noise.Unit() * (seconds - 0.2f), 0.1f, 160f, 1500f, 7000f, 0.5f, 0.0006f);
			}
			array = Crossfade(array, 11025);
			Normalize(array, 0.85f);
			return array;
		}

		public static float[] DragonWhoosh(int variant)
		{
			Noise noise = new Noise(variant * 311 + 77);
			float[] array = New(2.6f);
			float num = 1f + noise.Next() * 0.1f;
			Sweep(array, noise, 0f, 2.4f, 220f * num, 1100f * num, 380f * num, 0.9f, 1.1f, 0.62f, 1.3f);
			Sweep(array, noise, 0.15f, 2.2f, 90f * num, 320f * num, 110f * num, 0.6f, 0.9f, 0.65f, 1.4f);
			Biquad biquad = Biquad.BandPass(70f * num, 3f);
			for (int i = At(0.9f); i < At(2.3f) && i < array.Length; i++)
			{
				float num2 = (float)i / 44100f;
				float num3 = (float)Math.Sin(Math.PI * (double)Math.Min(1f, (num2 - 0.9f) / 1.4f));
				array[i] += biquad.Run(noise.Next()) * 2.2f * num3 * (0.7f + 0.3f * (float)Math.Sin((double)(num2 * 2f) * Math.PI * 23.0));
			}
			LowPass(array, 5200f);
			return Finish(array, 0.15f, 0.95f);
		}

		public static float[] DragonLand(int variant)
		{
			Noise noise = new Noise(variant * 541 + 19);
			float[] s = New(1.8f);
			float num = 1f + noise.Next() * 0.08f;
			Thud(s, 0f, 30f * num, 1f, 0.32f, 0.9f);
			Thud(s, 0.01f, 52f * num, 0.6f, 0.16f, 0.6f);
			Thud(s, 0.03f, 88f * num, 0.3f, 0.07f, 0.5f);
			Brush(s, noise, 0f, 40f, 260f, 0.55f, 0.01f, 0.35f);
			Grains(s, noise, 0.05f, 0.7f, 90f, 400f, 2400f, 0.35f, 0.004f);
			Brush(s, noise, 0.02f, 300f, 1600f, 0.18f, 0.005f, 0.12f);
			HighPass(s, 22f);
			LowPass(s, 4000f);
			return Finish(s, 0.18f, 0.97f);
		}

		public static float[] DragonSnort(int variant)
		{
			Noise n = new Noise(variant * 97 + 3);
			float[] s = New(0.9f);
			Brush(s, n, 0f, 250f, 2200f, 0.8f, 0.02f, 0.12f);
			Brush(s, n, 0.18f, 200f, 1600f, 0.5f, 0.03f, 0.16f);
			Thud(s, 0f, 46f, 0.35f, 0.15f, 0.3f);
			LowPass(s, 3500f);
			return Finish(s, 0.12f, 0.9f);
		}

		public static float[] DrumRoll(int variant)
		{
			Noise noise = new Noise(variant * 977 + 41);
			float[] s = New(3.4f);
			float num = 0.02f;
			int num2 = 0;
			while (num < 2.35f)
			{
				float num3 = num / 2.35f;
				float num4 = (((num2 & 1) == 0) ? 1f : 0.82f);
				float num5 = (0.18f + 0.62f * num3 * num3) * num4 * (0.85f + 0.3f * Math.Abs(noise.Next()));
				float hz = 205f + 30f * noise.Next();
				Knock(s, noise, num, hz, 1.4f, num5 * 0.5f, 0.022f);
				Brush(s, noise, num + 0.001f, 1800f, 6500f, num5 * 0.55f, 0.0008f, 0.03f + 0.01f * Math.Abs(noise.Next()));
				Thud(s, num, 96f + 12f * noise.Next(), num5 * 0.35f, 0.05f, 0.4f);
				float num6 = 1f / (11f + 6f * num3);
				num += num6 * (1f + 0.16f * noise.Next());
				num2++;
			}
			float start = 2.5f + 0.012f * noise.Next();
			WarDrum(s, noise, start, 52f, 1f);
			Knock(s, noise, start, 180f, 1.1f, 0.5f, 0.05f);
			Brush(s, noise, start, 900f, 5000f, 0.35f, 0.001f, 0.09f);
			Slap(s, 0.11f, 0.22f);
			HighPass(s, 40f);
			return Finish(s, 0.12f, 0.9f);
		}

		public static float[] RainRoof(float seconds)
		{
			Noise noise = new Noise(5151);
			float[] array = New(seconds);
			Biquad biquad = Biquad.LowPass(240f);
			Biquad biquad2 = Biquad.LowPass(240f);
			Biquad biquad3 = Biquad.BandPass(900f, 0.8f);
			for (int i = 0; i < array.Length; i++)
			{
				float num = (float)i / (float)array.Length;
				float num2 = 0.8f + 0.12f * (float)Math.Sin(Math.PI * 4.0 * (double)num) + 0.08f * (float)Math.Sin(Math.PI * 10.0 * (double)num + 0.7);
				float x = noise.Next();
				array[i] = biquad2.Run(biquad.Run(x)) * 1.6f * num2 + biquad3.Run(x) * 0.07f * num2;
			}
			Drops(array, noise, seconds, 260f, 700f, 2600f, 1.8f, 0.1f, 0.003f);
			Drops(array, noise, seconds, 40f, 180f, 520f, 2.5f, 0.32f, 0.012f);
			for (int j = 0; j < (int)(seconds * 1.5f); j++)
			{
				Knock(array, noise, noise.Unit() * (seconds - 0.05f), noise.Range(900f, 1500f), 9f, 0.12f, 0.02f);
			}
			LowPass(array, 3200f);
			array = Crossfade(array, 11025);
			Normalize(array, 0.7f);
			return array;
		}

		public static float[] WindWalls(float seconds)
		{
			Noise noise = new Noise(6262);
			float[] array = New(seconds);
			Biquad biquad = Biquad.LowPass(140f);
			Biquad biquad2 = Biquad.LowPass(140f);
			Biquad biquad3 = default(Biquad);
			for (int i = 0; i < array.Length; i++)
			{
				float num = (float)i / (float)array.Length;
				float num2 = 0.5f + 0.3f * (float)Math.Sin(Math.PI * 4.0 * (double)num + 0.4) + 0.2f * (float)Math.Sin(Math.PI * 6.0 * (double)num + 2.1);
				if ((i & 0x1F) == 0)
				{
					biquad.SetLowPass(90f + 150f * num2, 0.8f);
					biquad3.SetBandPass(780f + 160f * (float)Math.Sin(Math.PI * 8.0 * (double)num), 14f);
				}
				float x = noise.Next();
				float num3 = biquad2.Run(biquad.Run(x)) * 2.6f * num2;
				float num4 = biquad3.Run(x) * Math.Max(0f, num2 - 0.72f) * 1.4f;
				array[i] = num3 + num4;
			}
			for (int j = 0; j < (int)(seconds * 0.4f); j++)
			{
				Creak(array, noise, noise.Unit() * (seconds - 0.6f), noise.Range(0.25f, 0.5f), 0.05f, noise.Range(55f, 85f));
			}
			array = Crossfade(array, 14700);
			Normalize(array, 0.75f);
			return array;
		}

		public static float[] CurtainSlide(int variant)
		{
			Noise noise = new Noise(variant * 13 + 4401);
			float num = 0.55f + 0.1f * noise.Unit();
			float[] s = New(num + 0.1f);
			Brush(s, noise, 0.02f, 700f, 3200f, 0.22f, num * 0.35f, num * 0.22f);
			Brush(s, noise, 0f, 180f, 700f, 0.12f, num * 0.3f, num * 0.2f);
			int num2 = 9 + (int)(noise.Unit() * 5f);
			for (int i = 0; i < num2; i++)
			{
				float num3 = ((float)i + 0.5f * noise.Unit()) / (float)num2;
				float num4 = num * (0.5f - 0.5f * (float)Math.Cos(Math.PI * (double)num3)) * 0.85f;
				Knock(s, noise, num4, noise.Range(1700f, 2700f), 5f, 0.16f + 0.08f * noise.Unit(), 0.004f);
				Knock(s, noise, num4 + 0.002f, noise.Range(650f, 900f), 3f, 0.06f, 0.006f);
			}
			for (int j = 0; j < 3; j++)
			{
				Knock(s, noise, num * 0.86f + (float)j * noise.Range(0.015f, 0.03f), noise.Range(1500f, 2300f), 4f, 0.22f, 0.006f);
			}
			return Finish(s, 0.1f, 0.85f);
		}

		private static void Drops(float[] s, Noise n, float seconds, float perSecond, float lo, float hi, float q, float amp, float decay)
		{
			int num = (int)(seconds * perSecond);
			for (int i = 0; i < num; i++)
			{
				float seconds2 = n.Unit() * (seconds - decay * 6f - 0.01f);
				Biquad biquad = Biquad.BandPass(n.Log(lo, hi), q);
				float num2 = amp * (0.3f + 0.7f * n.Unit()) * (1.6f + q * 0.8f);
				float num3 = decay * (0.6f + 0.8f * n.Unit());
				int num4 = At(seconds2);
				int num5 = Math.Min(s.Length - num4, At(num3 * 6f));
				for (int j = 0; j < num5; j++)
				{
					s[num4 + j] += biquad.Run(n.Next()) * num2 * (float)Math.Exp((float)(-j) / (num3 * 44100f));
				}
			}
		}

		public static float[] Pickup(int variant)
		{
			float[] s = New(0.45f);
			Noise n = new Noise(9173 + variant * 31);
			float num = 1f + (float)(variant - 1) * 0.02f;
			Knock(s, n, 0f, 1050f * num, 5f, 0.45f, 0.012f);
			Thud(s, 0f, 230f * num, 0.3f, 0.025f, 0.35f);
			Chime(s, 0.006f, 1568f * num, 0.28f, 0.11f);
			Chime(s, 0.006f, 2349.3f * num, 0.1f, 0.07f);
			return Finish(s, 0.045f, 0.5f);
		}

		public static float[] Ping(int kind)
		{
			float[] s = New(1f);
			switch (kind)
			{
			case 1:
				Chime(s, 0f, 1046.5f, 0.28f, 0.25f);
				Chime(s, 0.06f, 1318.5f, 0.28f, 0.25f);
				Chime(s, 0.12f, 1568f, 0.32f, 0.4f);
				break;
			case 2:
				Chime(s, 0f, 1760f, 0.36f, 0.1f);
				Chime(s, 0.085f, 1760f, 0.36f, 0.1f);
				Chime(s, 0.17f, 1760f, 0.3f, 0.22f);
				break;
			case 3:
				Chime(s, 0f, 1568f, 0.34f, 0.18f);
				Chime(s, 0.1f, 1046.5f, 0.34f, 0.35f);
				break;
			case 4:
				Chime(s, 0f, 2093f, 0.22f, 0.12f);
				Chime(s, 0.045f, 2637f, 0.22f, 0.12f);
				Chime(s, 0.09f, 3136f, 0.22f, 0.14f);
				Chime(s, 0.135f, 4186f, 0.2f, 0.3f);
				break;
			case 5:
				Chime(s, 0f, 784f, 0.34f, 0.3f);
				Chime(s, 0.11f, 1046.5f, 0.32f, 0.45f);
				break;
			case 6:
				Chime(s, 0f, 1046.5f, 0.2f, 0.07f);
				Chime(s, 0.05f, 784f, 0.18f, 0.09f);
				break;
			default:
				Chime(s, 0f, 1318.5f, 0.3f, 0.2f);
				Chime(s, 0.07f, 1975.5f, 0.3f, 0.32f);
				break;
			}
			return Finish(s, 0.05f, 0.6f);
		}

		public static float[] ShotgunBlast(int variant)
		{
			Noise n = new Noise(variant * 523 + 41);
			float[] array = New(1.9f);
			float num = 1f + 0.06f * (float)(variant - 1);
			Brush(array, n, 0f, 1800f, 12000f, 1.1f, 0.0005f, 0.0035f);
			Brush(array, n, 0.0005f, 180f, 2600f, 1.05f, 0.0012f, 0.045f);
			Knock(array, n, 0.001f, 820f * num, 1.1f, 0.55f, 0.03f);
			Thud(array, 0f, 58f * num, 1.15f, 0.11f, 1.6f);
			Thud(array, 0.004f, 110f * num, 0.45f, 0.05f, 0.9f);
			Brush(array, n, 0.01f, 35f, 380f, 0.5f, 0.03f, 0.32f);
			float[] array2 = new float[array.Length];
			Brush(array2, n, 0.17f + 0.02f * (float)variant, 120f, 1600f, 0.32f, 0.01f, 0.12f);
			Thud(array2, 0.175f + 0.02f * (float)variant, 52f, 0.22f, 0.16f, 0.6f);
			Brush(array2, n, 0.42f + 0.03f * (float)variant, 90f, 900f, 0.16f, 0.03f, 0.22f);
			LowPass(array2, 2200f);
			for (int i = 0; i < array.Length; i++)
			{
				array[i] += array2[i];
			}
			Slap(array, 0.045f, 0.18f);
			return Finish(array, 0.26f, 0.99f);
		}

		public static float[] ShotgunPumpBack(int variant)
		{
			Noise n = new Noise(variant * 137 + 3);
			float[] s = New(0.3f);
			Brush(s, n, 0f, 1400f, 6000f, 0.2f, 0.012f, 0.03f);
			Brush(s, n, 0.005f, 300f, 1400f, 0.12f, 0.01f, 0.03f);
			float num = 0.062f + 0.006f * (float)variant;
			Knock(s, n, num, 2500f + 200f * (float)variant, 5f, 0.55f, 0.011f);
			Knock(s, n, num + 0.002f, 1050f, 2.5f, 0.35f, 0.018f);
			Thud(s, num, 170f, 0.2f, 0.025f, 0.5f);
			Modes(s, n, num, 1f, new float[3] { 2950f, 4380f, 6650f }, new float[3] { 0.04f, 0.03f, 0.02f }, new float[3] { 0.05f, 0.035f, 0.02f });
			return Finish(s, 0.11f, 0.8f);
		}

		public static float[] ShotgunPumpForward(int variant)
		{
			Noise n = new Noise(variant * 211 + 19);
			float[] s = New(0.32f);
			Brush(s, n, 0f, 1600f, 6500f, 0.16f, 0.008f, 0.022f);
			float num = 0.042f + 0.005f * (float)variant;
			Knock(s, n, num, 3300f + 150f * (float)variant, 6f, 0.7f, 0.009f);
			Knock(s, n, num + 0.001f, 1400f, 3f, 0.45f, 0.016f);
			Thud(s, num, 220f, 0.26f, 0.022f, 0.6f);
			Modes(s, n, num, 1f, new float[4] { 2800f, 4100f, 6300f, 8200f }, new float[4] { 0.06f, 0.045f, 0.03f, 0.02f }, new float[4] { 0.07f, 0.05f, 0.03f, 0.015f });
			return Finish(s, 0.13f, 0.85f);
		}

		public static float[] ShotgunShellDrop(int variant)
		{
			Noise noise = new Noise(variant * 61 + 7);
			float[] s = New(0.45f);
			float num = 0f;
			float num2 = 0.42f;
			int num3 = 2 + variant % 2;
			for (int i = 0; i < num3; i++)
			{
				Knock(s, noise, num, noise.Range(1700f, 2600f), 4f, num2, 0.009f);
				Modes(s, noise, num, noise.Range(0.95f, 1.05f), new float[3] { 5200f, 7700f, 9800f }, new float[3] { 0.03f, 0.02f, 0.012f }, new float[3]
				{
					num2 * 0.1f,
					num2 * 0.06f,
					num2 * 0.03f
				});
				Knock(s, noise, num + 0.001f, 700f, 2f, num2 * 0.3f, 0.01f);
				num += noise.Range(0.07f, 0.11f) * (1f - 0.25f * (float)i);
				num2 *= 0.5f;
			}
			Grains(s, noise, num, 0.08f, 60f, 1500f, 4000f, 0.03f, 0.002f);
			return Finish(s, 0.06f, 0.6f);
		}

		public static float[] ShotgunShellIn(int variant)
		{
			Noise n = new Noise(variant * 89 + 23);
			float[] s = New(0.2f);
			Brush(s, n, 0f, 900f, 4000f, 0.08f, 0.01f, 0.02f);
			Knock(s, n, 0.018f, 2300f + 250f * (float)variant, 4f, 0.45f, 0.008f);
			Knock(s, n, 0.032f, 950f, 2f, 0.3f, 0.02f);
			Thud(s, 0.03f, 150f, 0.14f, 0.03f, 0.4f);
			return Finish(s, 0.08f, 0.7f);
		}

		public static float[] ShotgunDry(int variant)
		{
			Noise n = new Noise(variant * 43 + 11);
			float[] s = New(0.15f);
			Knock(s, n, 0f, 3800f, 6f, 0.5f, 0.006f);
			Knock(s, n, 0.012f, 1600f, 3f, 0.25f, 0.01f);
			return Finish(s, 0.06f, 0.55f);
		}

		public static float[] ShotgunHit(int variant)
		{
			Noise n = new Noise(variant * 151 + 77);
			float[] s = New(0.3f);
			Thud(s, 0f, 105f + 10f * (float)variant, 0.7f, 0.05f, 1f);
			Brush(s, n, 0f, 350f, 2200f, 0.45f, 0.002f, 0.028f);
			Grains(s, n, 0.002f, 0.04f, 300f, 900f, 3500f, 0.12f, 0.003f);
			Knock(s, n, 0.001f, 2400f, 2f, 0.22f, 0.008f);
			return Finish(s, 0.14f, 0.85f);
		}

		public static float[] PelletPatter(int variant)
		{
			Noise n = new Noise(variant * 977 + 5);
			float[] s = New(0.3f);
			Grains(s, n, 0f, 0.12f, 120f, 500f, 2600f, 0.25f, 0.006f);
			Brush(s, n, 0f, 300f, 1500f, 0.08f, 0.01f, 0.05f);
			return Finish(s, 0.06f, 0.6f);
		}

		public static float[] UfoHum(float seconds)
		{
			Noise noise = new Noise(5150);
			float[] array = New(seconds);
			double len = (double)array.Length / 44100.0;
			double num = Hz(58.0);
			double num2 = Hz(87.5);
			double num3 = Hz(59.0);
			double num4 = Hz(1.5);
			double num5 = Hz(760.0);
			double num6 = Hz(0.75);
			double num7 = Hz(5.5);
			Biquad biquad = Biquad.BandPass(2600f, 1.5f);
			Biquad biquad2 = Biquad.LowPass(1400f);
			double num8 = 0.0;
			double num9 = 0.0;
			double num10 = 0.0;
			double num11 = 0.0;
			for (int i = 0; i < array.Length; i++)
			{
				double num12 = (double)i / 44100.0;
				float num13 = 0.62f + 0.38f * (float)Math.Sin(Math.PI * 2.0 * num4 * num12);
				num8 += num / 44100.0;
				num9 += num2 / 44100.0;
				num10 += num3 / 44100.0;
				double num14 = num5 * (1.0 + 0.08 * Math.Sin(Math.PI * 2.0 * num6 * num12) + 0.012 * Math.Sin(Math.PI * 2.0 * num7 * num12));
				num11 += num14 / 44100.0;
				float num15 = 1f - 4f * Math.Abs((float)(num8 - Math.Floor(num8)) - 0.5f);
				float num16 = 1f - 4f * Math.Abs((float)(num9 - Math.Floor(num9)) - 0.5f);
				float num17 = (float)Math.Sin(Math.PI * 2.0 * num10);
				float x = (num15 * 0.55f + num17 * 0.5f + num16 * 0.3f) * num13;
				float num18 = (float)Math.Sin(Math.PI * 2.0 * num11) * 0.16f * (0.6f + 0.4f * (float)Math.Sin(Math.PI * 2.0 * num6 * 2.0 * num12 + 1.0));
				array[i] = biquad2.Run(x) + num18 + biquad.Run(noise.Next()) * 0.05f * num13;
			}
			array = Crossfade(array, 5512);
			Normalize(array, 0.75f);
			return array;
			double Hz(double want)
			{
				return Math.Max(1.0, Math.Round(want * len)) / len;
			}
		}

		private static void Note(float[] s, Noise n, float start, float dur, float[] hz, float amp, float vibHz = 0f, float vibDepth = 0f, float breath = 0.02f, float h2 = 0.06f, float h3 = 0f)
		{
			int num = At(start);
			int num2 = Math.Min(s.Length - num, At(dur));
			if (num2 <= 0)
			{
				return;
			}
			double num3 = (double)n.Unit() * 6.283;
			float num4 = Math.Max(0.003f, dur * 0.14f);
			float num5 = Math.Max(0.006f, dur * 0.3f);
			Biquad biquad = default(Biquad);
			float num6 = 0f;
			int num7 = hz.Length - 1;
			for (int i = 0; i < num2; i++)
			{
				float num8 = (float)i / 44100f;
				float num9 = (float)i / (float)num2;
				float num10 = num9 * (float)num7;
				int num11 = Math.Min(num7, (int)num10);
				int num12 = Math.Min(num7, num11 + 1);
				float num13 = num10 - (float)num11;
				num13 = num13 * num13 * (3f - 2f * num13);
				float num14 = hz[num11] + (hz[num12] - hz[num11]) * num13;
				if (vibDepth > 0f)
				{
					num14 *= 1f + vibDepth * (float)Math.Sin(Math.PI * 2.0 * (double)vibHz * (double)num8);
				}
				num3 += Math.PI * 2.0 * (double)num14 / 44100.0;
				float num15 = ((num8 < num4) ? ((float)Math.Sin(Math.PI / 2.0 * (double)num8 / (double)num4)) : 1f) * ((num8 > dur - num5) ? ((float)Math.Sin(Math.PI / 2.0 * (double)Math.Max(0f, dur - num8) / (double)num5)) : 1f) * (1f - 0.25f * num9);
				float num16 = (float)Math.Sin(num3) + h2 * (float)Math.Sin(2.0 * num3 + 0.4) + h3 * (float)Math.Sin(3.0 * num3 + 1.1);
				if (breath > 0f)
				{
					if ((i & 0x1F) == 0)
					{
						biquad.SetBandPass(num14, 4f);
						num6 = breath * 1.7f / (float)Math.Sqrt(Math.Max(0.0001f, num14 / 4f / 22050f));
					}
					num16 += biquad.Run(n.Next()) * num6;
				}
				s[num + i] += num16 * amp * num15 * num15;
			}
		}

		private static float[] Hz(params float[] hz)
		{
			return hz;
		}

		private static float[] Songbird(float[] s, float rms)
		{
			HighPass(s, 1300f);
			HighPass(s, 1300f);
			LowPass(s, 9500f);
			return Finish(s, rms, 0.7f);
		}

		public static float[] Chirps(int variant)
		{
			Noise noise = new Noise(variant * 2741 + 13);
			float num = noise.Range(3500f, 4500f);
			int num2 = 2 + (int)(noise.Unit() * 4f);
			float[] s = New(0.25f * (float)num2 + 0.3f);
			float num3 = 0.01f;
			for (int i = 0; i < num2; i++)
			{
				float num4 = num * (1f + noise.Next() * 0.05f);
				float num5 = noise.Range(0.04f, 0.07f);
				if (noise.Unit() < 0.25f)
				{
					Note(s, noise, num3, num5 * 1.5f, Hz(num4 * 0.8f, num4 * 1.28f, num4 * 1.05f, num4 * 0.82f), noise.Range(0.75f, 1f), 0f, 0f, 0.03f);
				}
				else
				{
					Note(s, noise, num3, num5, Hz(num4 * 1.12f, num4 * 1.38f, num4 * 1.08f, num4 * 0.84f, num4 * 0.74f), noise.Range(0.75f, 1f), 0f, 0f, 0.03f);
				}
				num3 += num5 + noise.Range(0.09f, 0.19f);
			}
			return Songbird(s, 0.1f);
		}

		public static float[] Whistle(int variant)
		{
			Noise noise = new Noise(variant * 811 + 7);
			float num = noise.Range(3500f, 4300f);
			float num2 = num * noise.Range(0.8f, 0.88f);
			float[] s = New(1.3f);
			float num3 = noise.Range(0.22f, 0.3f);
			Note(s, noise, 0.01f, num3, Hz(num * 1.01f, num, num * 0.995f, num * 0.985f), 1f, 28f, 0.003f, 0.012f, 0.04f);
			float num4 = 0.01f + num3 + noise.Range(0.05f, 0.09f);
			float num5 = noise.Range(0.12f, 0.16f);
			float num6 = noise.Range(0.16f, 0.22f);
			Note(s, noise, num4, num5, Hz(num2 * 1.01f, num2, num2), 0.85f, 28f, 0.003f, 0.012f, 0.04f);
			Note(s, noise, num4 + num5 + 0.012f, num6, Hz(num2, num2 * 0.995f, num2 * 0.98f), 0.8f, 28f, 0.003f, 0.012f, 0.04f);
			if (variant % 3 == 2)
			{
				num4 += num5 + num6 + noise.Range(0.08f, 0.12f);
				Note(s, noise, num4, num6, Hz(num2 * 1.005f, num2 * 0.99f, num2 * 0.975f), 0.7f, 28f, 0.003f, 0.012f, 0.04f);
			}
			return Songbird(s, 0.09f);
		}

		public static float[] Trill(int variant)
		{
			Noise noise = new Noise(variant * 977 + 3);
			float num = noise.Range(4800f, 6000f);
			int num2 = 9 + (int)(noise.Unit() * 9f);
			float num3 = noise.Range(13f, 19f);
			float num4 = noise.Range(0.8f, 0.92f);
			bool flag = noise.Unit() < 0.5f;
			float[] s = New((float)num2 / num3 + 0.6f);
			float num5 = 0.01f;
			for (int i = 0; i < num2; i++)
			{
				float num6 = (float)i / (float)(num2 - 1);
				float num7 = num * (1f + (num4 - 1f) * num6);
				float amp = Math.Min(1f, 0.45f + num6 * 1.6f);
				float dur = 0.6f / num3;
				Note(s, noise, num5, dur, flag ? Hz(num7 * 0.78f, num7 * 1.1f, num7 * 1.2f) : Hz(num7 * 1.2f, num7 * 1.05f, num7 * 0.8f), amp);
				num5 += 1f / num3;
			}
			if (variant % 2 == 0)
			{
				float num8 = num * num4 * 0.82f;
				Note(s, noise, num5 + 0.02f, 0.08f, Hz(num8 * 0.9f, num8 * 1.15f, num8), 0.9f);
				Note(s, noise, num5 + 0.14f, 0.1f, Hz(num8 * 1.05f, num8 * 0.95f, num8 * 0.8f), 0.8f);
			}
			return Songbird(s, 0.09f);
		}

		public static float[] Warble(int variant)
		{
			Noise noise = new Noise(variant * 4127 + 29);
			int num = 3 + (int)(noise.Unit() * 4f);
			float[] s = New((float)num * 0.3f + 0.3f);
			float num2 = 0.01f;
			float num3 = noise.Range(2400f, 3600f);
			for (int i = 0; i < num; i++)
			{
				float num4 = num3 * noise.Range(0.8f, 1.3f);
				float num5 = noise.Range(0.08f, 0.2f);
				float num6 = noise.Range(0.6f, 1f);
				switch ((int)(noise.Unit() * 5f))
				{
				case 0:
					Note(s, noise, num2, num5, Hz(num4 * 0.8f, num4 * 0.95f, num4 * 1.15f), num6, 0f, 0f, 0.02f, 0.08f);
					break;
				case 1:
					Note(s, noise, num2, num5, Hz(num4 * 1.2f, num4 * 1.1f, num4 * 0.85f), num6, 0f, 0f, 0.02f, 0.08f);
					break;
				case 2:
					Note(s, noise, num2, num5, Hz(num4 * 0.9f, num4 * 1.2f, num4 * 1.15f, num4 * 0.92f), num6, 0f, 0f, 0.02f, 0.08f);
					break;
				case 3:
					Note(s, noise, num2, num5, Hz(num4, num4 * 1.02f, num4), num6, noise.Range(35f, 55f), 0.07f, 0.03f, 0.1f);
					break;
				default:
					Note(s, noise, num2, num5 * 0.45f, Hz(num4 * 0.95f, num4 * 1.1f), num6, 0f, 0f, 0.02f, 0.08f);
					Note(s, noise, num2 + num5 * 0.55f, num5 * 0.45f, Hz(num4 * 1.25f, num4 * 1.15f), num6 * 0.85f, 0f, 0f, 0.02f, 0.08f);
					break;
				}
				num2 += num5 + noise.Range(0.03f, 0.09f);
			}
			if (variant % 3 != 0)
			{
				for (int j = 0; j < 3; j++)
				{
					float num7 = num3 * noise.Range(1.6f, 2f);
					Note(s, noise, num2 + (float)j * 0.045f, 0.03f, Hz(num7 * 1.1f, num7 * 0.9f), 0.35f);
				}
			}
			return Songbird(s, 0.09f);
		}

		public static float[] Twitter(int variant)
		{
			Noise noise = new Noise(variant * 6007 + 41);
			float num = noise.Range(0.9f, 1.5f);
			float[] s = New(num + 0.3f);
			float num4;
			for (float num2 = 0.01f; num2 < num; num2 += num4 + noise.Range(0.03f, 0.1f))
			{
				float num3 = noise.Log(3600f, 6800f);
				num4 = noise.Range(0.025f, 0.06f);
				float num5 = noise.Range(0.4f, 1f);
				if (noise.Unit() < 0.15f)
				{
					Note(s, noise, num2, num4 * 2f, Hz(num3 * 0.95f, num3, num3 * 0.98f), num5 * 0.7f, noise.Range(90f, 140f), 0.05f, 0.04f);
				}
				else
				{
					Note(s, noise, num2, num4, (noise.Unit() < 0.5f) ? Hz(num3 * 0.8f, num3 * 1.15f) : Hz(num3 * 1.15f, num3 * 0.85f), num5);
				}
			}
			return Songbird(s, 0.085f);
		}

		public static float[] Coo(int variant)
		{
			Noise noise = new Noise(variant * 313 + 5);
			float num = noise.Range(470f, 560f);
			float[] s = New(2.6f);
			Note(s, noise, 0.02f, 0.26f, Hz(num * 0.9f, num * 1.08f, num * 1.15f), 0.8f, 0f, 0f, 0.09f, 0.12f, 0.03f);
			Note(s, noise, 0.33f, 0.5f, Hz(num * 1.22f, num * 1.25f, num * 1.12f, num), 1f, 0f, 0f, 0.09f, 0.12f, 0.03f);
			Note(s, noise, 1.15f, 0.42f, Hz(num * 1.02f, num, num * 0.97f), 0.8f, 0f, 0f, 0.09f, 0.12f, 0.03f);
			if (variant % 2 == 0)
			{
				Note(s, noise, 1.85f, 0.42f, Hz(num, num * 0.98f, num * 0.95f), 0.7f, 0f, 0f, 0.09f, 0.12f, 0.03f);
			}
			HighPass(s, 250f);
			LowPass(s, 2600f);
			return Finish(s, 0.09f, 0.7f);
		}

		public static float[] Hoot(int variant)
		{
			Noise noise = new Noise(variant * 59 + 101);
			float num = noise.Range(290f, 360f);
			float[] s = New(2.6f);
			if (variant % 2 == 0)
			{
				Note(s, noise, 0.02f, 0.28f, Hz(num * 0.96f, num * 1.02f, num), 0.8f, 0f, 0f, 0.12f, 0.18f, 0.05f);
				Note(s, noise, 0.5f, 0.14f, Hz(num * 0.98f, num * 1.03f), 0.6f, 0f, 0f, 0.12f, 0.18f, 0.05f);
				Note(s, noise, 0.68f, 0.42f, Hz(num * 1.02f, num * 1.06f, num * 1.02f, num * 0.98f), 1f, 0f, 0f, 0.12f, 0.18f, 0.05f);
				Note(s, noise, 1.4f, 0.32f, Hz(num, num * 0.99f, num * 0.96f), 0.85f, 0f, 0f, 0.12f, 0.18f, 0.05f);
				Note(s, noise, 1.98f, 0.32f, Hz(num, num * 0.98f, num * 0.95f), 0.75f, 0f, 0f, 0.12f, 0.18f, 0.05f);
			}
			else
			{
				Note(s, noise, 0.02f, 0.18f, Hz(num * 1.1f, num * 1.12f), 0.6f, 0f, 0f, 0.12f, 0.15f, 0.04f);
				Note(s, noise, 0.3f, 0.12f, Hz(num * 1.12f, num * 1.15f), 0.55f, 0f, 0f, 0.12f, 0.15f, 0.04f);
				Note(s, noise, 0.5f, 1.2f, Hz(num * 1.12f, num * 1.18f, num * 1.15f, num * 1.1f, num * 1.05f), 1f, 11f, 0.035f, 0.12f, 0.15f, 0.04f);
			}
			HighPass(s, 150f);
			LowPass(s, 1800f);
			return Finish(s, 0.09f, 0.7f);
		}

		public static float[] Frog(int variant)
		{
			Noise noise = new Noise(variant * 151 + 77);
			if (variant >= 4)
			{
				float[] s = New(0.3f);
				float num = noise.Range(2500f, 3000f);
				Note(s, noise, 0.01f, noise.Range(0.1f, 0.14f), Hz(num * 0.88f, num, num * 1.04f), 1f, 0f, 0f, 0.03f, 0.05f);
				HighPass(s, 1200f);
				return Finish(s, 0.08f, 0.7f);
			}
			float[] s2 = New(0.55f);
			float num2 = noise.Range(55f, 85f);
			float num3 = noise.Range(900f, 1400f);
			float hz = noise.Range(380f, 520f);
			int num4 = 1 + variant % 2;
			float num5 = 0.01f;
			for (int i = 0; i < num4; i++)
			{
				float num6 = ((i == 0) ? noise.Range(0.13f, 0.2f) : noise.Range(0.08f, 0.12f));
				for (float num7 = 0f; num7 < num6; num7 += 1f / num2)
				{
					float num8 = num7 / num6;
					float num9 = (float)Math.Sin(Math.PI * (double)Math.Min(1f, num8 * 1.2f)) * ((i == 0) ? 1f : 0.8f);
					Knock(s2, noise, num5 + num7, num3 * (1f + 0.08f * num8), 3f, 0.5f * num9, 0.004f, 0.0005f);
					Knock(s2, noise, num5 + num7, hz, 2.5f, 0.6f * num9, 0.006f, 0.0005f);
				}
				num5 += num6 + noise.Range(0.04f, 0.07f);
			}
			HighPass(s2, 200f);
			LowPass(s2, 3500f);
			return Finish(s2, 0.09f, 0.75f);
		}

		public static float[] Crickets(float seconds)
		{
			Noise noise = new Noise(9091);
			int frames = At(seconds);
			float[] s = new float[frames * 2];
			for (int i = 0; i < 6; i++)
			{
				float num = noise.Range(4200f, 5200f);
				float num2 = ((i == 0) ? 0.9f : noise.Range(0.2f, 0.6f));
				float num3 = noise.Range(-0.85f, 0.85f);
				float gl = num2 * (float)Math.Cos((double)(num3 + 1f) * Math.PI / 4.0);
				float gr = num2 * (float)Math.Sin((double)(num3 + 1f) * Math.PI / 4.0);
				int num4 = 3 + (int)(noise.Unit() * 2f);
				float num5 = noise.Range(26f, 36f);
				float seconds2 = noise.Range(0.011f, 0.017f);
				int num6 = Math.Max(1, (int)Math.Round(seconds / noise.Range(0.38f, 0.8f)));
				int num7 = (int)(noise.Unit() * (float)frames / (float)num6);
				double num8 = Math.PI * 2.0 * (double)num / 44100.0;
				int num9 = At(seconds2);
				for (int j = 0; j < num6; j++)
				{
					int num10 = num7 + (int)((long)j * (long)frames / num6);
					if (noise.Unit() < 0.08f)
					{
						continue;
					}
					for (int k = 0; k < num4; k++)
					{
						int num11 = num10 + At((float)k / num5);
						float num12 = ((k == num4 - 1) ? 0.8f : 1f);
						for (int l = 0; l < num9; l++)
						{
							float num13 = (float)l / (float)num9;
							float num14 = (float)Math.Sin(Math.PI * (double)num13);
							Put(num11 + l, (float)Math.Sin(num8 * (double)l * (double)(1f - 0.02f * num13)) * num14 * num14 * num12, gl, gr);
						}
					}
				}
			}
			int num15 = 22050;
			float[] array = new float[frames + num15];
			for (int m = 0; m < 2; m++)
			{
				Biquad biquad = Biquad.BandPass(4600f, 5f);
				Biquad biquad2 = Biquad.BandPass(4600f, 5f);
				float num16 = ((m == 0) ? 31f : 28.5f);
				for (int n = 0; n < array.Length; n++)
				{
					float num17 = (float)n / 44100f;
					float num18 = 0.5f + 0.5f * (float)Math.Sin(Math.PI * 2.0 * (double)num16 * (double)num17);
					float num19 = 0.75f + 0.25f * (float)Math.Sin(Math.PI * 2.0 * (double)num17 / (double)seconds * 3.0 + (double)m);
					array[n] = biquad2.Run(biquad.Run(noise.Next())) * num18 * num18 * num19 * 1.2f;
				}
				for (int num20 = 0; num20 < frames; num20++)
				{
					float num21 = array[num20];
					if (num20 < num15)
					{
						double num22 = ((double)num20 + 0.5) / (double)num15 * Math.PI * 0.5;
						num21 = num21 * (float)Math.Sin(num22) + array[frames + num20] * (float)Math.Cos(num22);
					}
					s[num20 * 2 + m] += num21;
				}
			}
			Normalize(s, 0.6f);
			return s;
			void Put(int frame, float v, float num24, float num25)
			{
				int num23 = (frame % frames + frames) % frames * 2;
				s[num23] += v * num24;
				s[num23 + 1] += v * num25;
			}
		}

		public static float[] WindUpSwish(int variant, bool heavy)
		{
			Noise noise = new Noise(variant * 211 + (heavy ? 97 : 13));
			float num = (heavy ? 0.7f : 1f) * (1f + noise.Next() * 0.06f);
			float[] s = New(0.45f);
			Sweep(s, noise, 0f, 0.3f, 220f * num, 1100f * num, 1500f * num, 0.9f, 0.55f, 0.8f, 2.2f);
			Brush(s, noise, 0.02f, 400f, 2200f, 0.08f, 0.12f, 0.03f);
			LowPass(s, heavy ? 2600f : 3800f);
			return Finish(s, 0.05f, 0.6f);
		}

		public static float[] WindUpRing(int variant)
		{
			Noise noise = new Noise(variant * 431 + 59);
			float pitch = 1f + noise.Next() * 0.05f;
			float[] s = New(0.5f);
			Brush(s, noise, 0f, 5000f, 11000f, 0.12f, 0.02f, 0.03f);
			Modes(s, noise, 0.01f, pitch, new float[3] { 2640f, 3960f, 5280f }, new float[3] { 0.06f, 0.045f, 0.03f }, new float[3] { 0.22f, 0.14f, 0.07f });
			return Finish(s, 0.025f, 0.35f);
		}

		public static float[] WindUpSnarl(int variant)
		{
			Noise noise = new Noise(variant * 59 + 7);
			float[] s = New(0.5f);
			Growl(s, noise, 0f, 0.36f, noise.Range(95f, 125f), 1.1f, 34f, 0.75f, 0.45f);
			HighPass(s, 45f);
			return Finish(s, 0.07f, 0.75f);
		}

		private static double Whole(double want, float seconds)
		{
			return Math.Max(1.0, Math.Round(want * (double)seconds)) / (double)seconds;
		}

		private static void Pad(float[] s, float seconds, float[] hz, float[] amp, float swellCycles, float swellDepth)
		{
			for (int i = 0; i < hz.Length; i++)
			{
				double num = Whole(hz[i], seconds);
				double num2 = Math.PI * 2.0 * num / 44100.0;
				for (int j = 0; j < s.Length; j++)
				{
					float num3 = (float)j / (float)s.Length;
					float num4 = 1f - swellDepth * 0.5f * (1f + (float)Math.Sin(Math.PI * 2.0 * (double)(swellCycles + (float)i) * (double)num3 + (double)i * 1.7));
					s[j] += (float)Math.Sin(num2 * (double)j) * amp[i] * num4;
				}
			}
		}

		private static void Bleep(float[] s, float start, float hz, float dur, float amp)
		{
			int num = At(start);
			int num2 = Math.Min(s.Length - num, At(dur));
			double num3 = 0.0;
			for (int i = 0; i < num2; i++)
			{
				num3 += (double)(hz / 44100f);
				float num4 = (float)(num3 - Math.Floor(num3));
				float num5 = ((num4 < 0.5f) ? 1f : (-1f));
				float num6 = 1f - 4f * Math.Abs(num4 - 0.5f);
				float num7 = Math.Min(1f, (float)i / 132.3f) * Math.Min(1f, (float)(num2 - i) / 264.6f);
				s[num + i] += (num5 * 0.35f + num6 * 0.65f) * amp * num7;
			}
		}

		public static float[] WonderEggHum(float seconds)
		{
			Noise noise = new Noise(9101);
			float[] s = New(seconds);
			Pad(s, seconds, new float[6] { 130.8f, 196f, 261.6f, 392f, 1046.5f, 1568f }, new float[6] { 0.32f, 0.2f, 0.14f, 0.07f, 0.025f, 0.018f }, 1f, 0.5f);
			for (int i = 0; i < (int)(seconds * 1.2f); i++)
			{
				Chime(s, noise.Unit() * (seconds - 1.2f), noise.Range(2093f, 3520f), 0.012f, 0.35f);
			}
			s = Crossfade(s, 14700);
			Normalize(s, 0.55f);
			return s;
		}

		public static float[] WonderEggChime()
		{
			Noise n = new Noise(9131);
			float[] array = New(3.2f);
			float[] array2 = new float[4] { 1046.5f, 1318.5f, 1568f, 2093f };
			for (int i = 0; i < array2.Length; i++)
			{
				Chime(array, (float)i * 0.05f, array2[i], 0.28f - (float)i * 0.04f, 1.4f);
			}
			Chime(array, 0f, 523.3f, 0.2f, 1.8f);
			for (int j = 0; j < array.Length; j++)
			{
				float num = (float)j / 44100f;
				array[j] += (float)Math.Sin(821.84063817909 * (double)num) * 0.18f * Env(num, 0.25f, 0.9f);
			}
			Brush(array, n, 0f, 3000f, 9000f, 0.03f, 0.2f, 0.5f);
			return Finish(array, 0.1f, 0.8f);
		}

		public static float[] WonderHarpAir(float seconds)
		{
			Noise noise = new Noise(9203);
			float[] s = New(seconds);
			Pad(s, seconds, new float[5] { 220f, 329.6f, 440f, 659.3f, 880f }, new float[5] { 0.12f, 0.1f, 0.09f, 0.05f, 0.035f }, 1f, 0.7f);
			float[] array = new float[8] { 1f, 1.125f, 1.25f, 1.5f, 1.6875f, 2f, 2.25f, 2.5f };
			int num = (int)(seconds * 0.9f);
			for (int i = 0; i < num; i++)
			{
				Pluck(s, noise, ((float)i + noise.Range(0.1f, 0.6f)) * (seconds - 1.5f) / (float)num, 440f * array[(int)(noise.Unit() * (float)array.Length) % array.Length], 0.22f, 0.9968f, 4200f);
			}
			Grains(s, noise, 0.2f, seconds - 0.6f, 20f, 3500f, 8000f, 0.05f, 0.004f);
			s = Crossfade(s, 14700);
			Normalize(s, 0.5f);
			return s;
		}

		public static float[] WonderHarpGliss()
		{
			Noise noise = new Noise(9227);
			float[] s = New(3.6f);
			float[] array = new float[12]
			{
				1f, 1.125f, 1.25f, 1.5f, 1.6875f, 2f, 2.25f, 2.5f, 3f, 3.375f,
				3.75f, 4f
			};
			for (int i = 0; i < array.Length; i++)
			{
				Pluck(s, noise, (float)i * 0.045f, 220f * array[i], 0.42f - (float)i * 0.015f, 0.9972f, 5200f);
			}
			for (int j = 0; j < 5; j++)
			{
				Pluck(s, noise, 0.6f + (float)j * 0.07f, 220f * array[array.Length - 2 - j], 0.22f, 0.997f, 4800f);
			}
			for (int k = 0; k < 6; k++)
			{
				Chime(s, 0.3f + noise.Range(0f, 0.9f), 1760f * array[(int)(noise.Unit() * 6f) % 6], 0.04f, 0.6f);
			}
			return Finish(s, 0.11f, 0.85f);
		}

		public static float[] WonderMoldBreath(float seconds)
		{
			Noise noise = new Noise(9311);
			float[] array = New(seconds);
			Biquad biquad = Biquad.LowPass(500f);
			Biquad biquad2 = default(Biquad);
			for (int i = 0; i < array.Length; i++)
			{
				float num = (float)i / (float)array.Length;
				float num2 = 0.5f + 0.5f * (float)Math.Sin(Math.PI * 4.0 * (double)num - Math.PI / 2.0);
				if ((i & 0x1F) == 0)
				{
					biquad2.SetBandPass(300f + 500f * num2, 1.4f);
				}
				float x = noise.Next();
				array[i] = biquad.Run(x) * 0.9f * (0.3f + num2) + biquad2.Run(x) * 0.4f * num2 * num2;
			}
			for (int j = 0; j < (int)(seconds * 1.5f); j++)
			{
				Bubbles(array, noise, noise.Unit() * (seconds - 0.3f), 0.25f, 3, 90f, 260f, 0.25f);
			}
			Creak(array, noise, seconds * 0.35f, 0.5f, 0.12f, 50f);
			array = Crossfade(array, 14700);
			Normalize(array, 0.55f);
			return array;
		}

		public static float[] WonderMoldPuff()
		{
			Noise n = new Noise(9337);
			float[] s = New(2f);
			Thud(s, 0f, 62f, 0.8f, 0.09f, 0.6f);
			Sweep(s, n, 0.02f, 1.1f, 300f, 1200f, 500f, 0.9f, 0.5f, 0.2f);
			Grains(s, n, 0.08f, 0.9f, 80f, 2500f, 7000f, 0.12f, 0.002f);
			return Finish(s, 0.12f, 0.85f);
		}

		public static float[] WonderComputerHum(float seconds)
		{
			Noise noise = new Noise(9419);
			float[] array = New(seconds);
			Biquad biquad = Biquad.LowPass(240f);
			double num = Whole(60.0, seconds);
			double num2 = Whole(7800.0, seconds);
			for (int i = 0; i < array.Length; i++)
			{
				float num3 = (float)i / 44100f;
				array[i] = biquad.Run(noise.Next()) * 1.6f + (float)Math.Sin(Math.PI * 2.0 * num * (double)num3) * 0.12f + (float)Math.Sin(Math.PI * 2.0 * num * 2.0 * (double)num3) * 0.06f + (float)Math.Sin(Math.PI * 2.0 * num2 * (double)num3) * 0.012f;
			}
			float[] array2 = new float[6] { 523.3f, 659.3f, 784f, 1046.5f, 880f, 698.5f };
			int num4 = (int)(seconds * 0.7f);
			for (int j = 0; j < num4; j++)
			{
				float num5 = ((float)j + noise.Range(0.1f, 0.5f)) * (seconds - 1f) / (float)num4;
				int num6 = 2 + (int)(noise.Unit() * 4f);
				for (int k = 0; k < num6; k++)
				{
					Bleep(array, num5 + (float)k * 0.075f, array2[(int)(noise.Unit() * (float)array2.Length) % array2.Length], 0.06f, 0.12f);
				}
			}
			for (int l = 0; l < (int)(seconds * 3f); l++)
			{
				Knock(array, noise, noise.Unit() * (seconds - 0.1f), 2600f, 3f, 0.08f, 0.004f);
			}
			array = Crossfade(array, 11025);
			Normalize(array, 0.5f);
			return array;
		}

		public static float[] WonderComputerCall()
		{
			float[] s = New(1.6f);
			float[] array = new float[6] { 523.3f, 659.3f, 784f, 1046.5f, 1318.5f, 1568f };
			for (int i = 0; i < array.Length; i++)
			{
				Bleep(s, (float)i * 0.08f, array[i], 0.09f, 0.3f);
			}
			for (int j = 0; j < 6; j++)
			{
				Bleep(s, 0.6f + (float)j * 0.05f, (j % 2 == 0) ? 2093f : 1568f, 0.05f, 0.2f);
			}
			Bleep(s, 0.95f, 1046.5f, 0.35f, 0.25f);
			return Finish(s, 0.1f, 0.8f);
		}

		public static float[] WonderTumorHeart()
		{
			Noise n = new Noise(9503);
			float[] array = New(6.88f);
			for (int i = 0; i < 8; i++)
			{
				float num = (float)i * 0.86f;
				Thud(array, num, 46f, 1f, 0.07f, 0.5f);
				Brush(array, n, num, 40f, 260f, 0.5f, 0.006f, 0.05f);
				Thud(array, num + 0.2f, 55f, 0.65f, 0.06f, 0.5f);
				Brush(array, n, num + 0.2f, 40f, 300f, 0.3f, 0.005f, 0.04f);
				Bubbles(array, n, num + 0.32f, 0.18f, 2, 120f, 320f, 0.18f);
			}
			Normalize(array, 0.7f);
			return array;
		}

		public static float[] WonderBiomassBubble(float seconds)
		{
			Noise noise = new Noise(9601);
			float[] array = New(seconds);
			Biquad biquad = Biquad.LowPass(380f);
			for (int i = 0; i < array.Length; i++)
			{
				float num = (float)i / (float)array.Length;
				array[i] = biquad.Run(noise.Next()) * 0.6f * (0.7f + 0.3f * (float)Math.Sin(Math.PI * 6.0 * (double)num));
			}
			for (int j = 0; j < (int)(seconds * 2.2f); j++)
			{
				Bubbles(array, noise, noise.Unit() * (seconds - 0.3f), 0.2f, 2, 70f, 240f, 0.5f);
			}
			for (int k = 0; k < (int)(seconds * 4f); k++)
			{
				Bubbles(array, noise, noise.Unit() * (seconds - 0.2f), 0.1f, 1, 300f, 900f, 0.18f);
			}
			array = Crossfade(array, 11025);
			Normalize(array, 0.55f);
			return array;
		}

		public static float[] WonderPumpkinCrackle(float seconds)
		{
			Noise noise = new Noise(9707);
			float[] array = New(seconds);
			Biquad biquad = Biquad.LowPass(180f);
			double num = Whole(55.0, seconds);
			double num2 = Whole(65.4, seconds);
			for (int i = 0; i < array.Length; i++)
			{
				float num3 = (float)i / 44100f;
				float num4 = (float)i / (float)array.Length;
				float num5 = 0.6f + 0.4f * (float)Math.Sin(Math.PI * 4.0 * (double)num4);
				array[i] = biquad.Run(noise.Next()) * 0.9f + ((float)Math.Sin(Math.PI * 2.0 * num * (double)num3) * 0.14f + (float)Math.Sin(Math.PI * 2.0 * num2 * (double)num3) * 0.1f) * num5;
			}
			for (int j = 0; j < (int)(seconds * 4f); j++)
			{
				Grains(array, noise, noise.Unit() * (seconds - 0.2f), 0.08f, 90f, 1500f, 6000f, 0.35f, 0.0006f);
			}
			for (int k = 0; k < (int)(seconds * 0.6f); k++)
			{
				Knock(array, noise, noise.Unit() * (seconds - 0.1f), noise.Range(400f, 900f), 2f, 0.3f, 0.008f);
			}
			array = Crossfade(array, 11025);
			Normalize(array, 0.5f);
			return array;
		}

		public static float[] WonderCyberScan(float seconds)
		{
			Noise noise = new Noise(9803);
			float[] array = New(seconds);
			double num = Whole(50.0, seconds);
			double num2 = Math.Max(1.0, Math.Round((double)seconds / 3.0));
			double num3 = 0.0;
			for (int i = 0; i < array.Length; i++)
			{
				float num4 = (float)i / 44100f;
				float num5 = (float)i / (float)array.Length;
				float value = (float)Math.Sin(Math.PI * 2.0 * num * (double)num4);
				value = (float)Math.Sign(value) * (float)Math.Pow(Math.Abs(value), 0.4) * 0.12f + (float)Math.Sin(Math.PI * 2.0 * num * 2.0 * (double)num4) * 0.1f;
				double num6 = 1150.0 + 260.0 * Math.Sin(Math.PI * 2.0 * num2 * (double)num5);
				num3 += num6 / 44100.0;
				array[i] = value + (float)Math.Sin(Math.PI * 2.0 * num3) * 0.05f * (0.6f + 0.4f * (float)Math.Sin(Math.PI * 2.0 * num2 * 2.0 * (double)num5));
			}
			for (int j = 0; j < (int)(seconds * 0.5f); j++)
			{
				float start = noise.Unit() * (seconds - 0.4f);
				Sweep(array, noise, start, 0.22f, 1800f, 3200f, 2200f, 6f, 0.05f, 0.5f);
			}
			array = Crossfade(array, 11025);
			Normalize(array, 0.5f);
			return array;
		}

		private static void Voice(float[] s, float seconds, float hz, float amp, float vowel, float vibrato, int seed)
		{
			double num = Whole(hz, seconds);
			float num2 = 700f + -400f * vowel;
			float num3 = 1220f + -350f * vowel;
			double num4 = Whole(5.2 + (double)seed * 0.3, seconds);
			int num5 = Math.Min(24, (int)(3200f / hz));
			float[] array = new float[num5];
			for (int i = 1; i <= num5; i++)
			{
				float num6 = (float)(num * (double)i);
				float num7 = (float)(Math.Exp(0.0 - Math.Pow((num6 - num2) / 140f, 2.0)) + 0.6 * Math.Exp(0.0 - Math.Pow((num6 - num3) / 200f, 2.0)) + 0.08 / (double)i);
				array[i - 1] = num7;
			}
			float[] array2 = new float[2048];
			for (int j = 0; j < 2048; j++)
			{
				for (int k = 0; k < num5; k++)
				{
					array2[j] += (float)Math.Sin(Math.PI * 2.0 * (double)j * (double)(k + 1) / 2048.0) * array[k];
				}
			}
			double num8 = 0.0;
			for (int l = 0; l < s.Length; l++)
			{
				float num9 = (float)l / 44100f;
				float num10 = (float)l / (float)s.Length;
				num8 += num * (1.0 + (double)vibrato * Math.Sin(Math.PI * 2.0 * num4 * (double)num9)) / 44100.0;
				double num11 = (num8 - Math.Floor(num8)) * 2048.0;
				int num12 = (int)num11;
				float num13 = (float)(num11 - (double)num12);
				float num14 = array2[num12 % 2048] * (1f - num13) + array2[(num12 + 1) % 2048] * num13;
				float num15 = 0.65f + 0.35f * (float)Math.Sin(Math.PI * 2.0 * (double)(1 + seed % 2) * (double)num10 + (double)seed);
				s[l] += num14 * amp * num15;
			}
		}

		public static float[] WonderBrainChoir(float seconds)
		{
			Noise n = new Noise(9901);
			float[] s = New(seconds);
			float[] array = new float[5] { 146.8f, 220f, 293.7f, 370f, 440f };
			for (int i = 0; i < array.Length; i++)
			{
				Voice(s, seconds, array[i], 0.1f, (i % 2 == 0) ? 0.15f : 0.55f, 0.006f, i);
			}
			Grains(s, n, 0.1f, seconds - 0.4f, 14f, 4000f, 9000f, 0.03f, 0.004f);
			LowPass(s, 5000f);
			s = Crossfade(s, 14700);
			Normalize(s, 0.5f);
			return s;
		}

		public static float[] WonderBadBrainThrob()
		{
			Noise noise = new Noise(9931);
			float seconds = 6.88f;
			float[] array = New(seconds);
			double num = Whole(41.2, seconds);
			double num2 = Whole(58.3, seconds);
			double num3 = Whole(43.7, seconds);
			for (int i = 0; i < array.Length; i++)
			{
				float num4 = (float)i / 44100f;
				float num5 = (float)Math.Exp(0f - num4 % 0.86f / 0.25f);
				array[i] = ((float)Math.Sin(Math.PI * 2.0 * num * (double)num4) * 0.3f + (float)Math.Sin(Math.PI * 2.0 * num2 * (double)num4) * 0.18f + (float)Math.Sin(Math.PI * 2.0 * num3 * (double)num4) * 0.2f) * (0.45f + 0.55f * num5);
			}
			for (int j = 0; j < 8; j++)
			{
				Thud(array, (float)j * 0.86f, 38f, 0.6f, 0.09f, 0.4f);
				Bubbles(array, noise, (float)j * 0.86f + 0.25f, 0.3f, 2, 90f, 220f, 0.2f);
			}
			Biquad biquad = default(Biquad);
			Biquad biquad2 = default(Biquad);
			for (int k = 0; k < array.Length; k++)
			{
				float num6 = (float)k / (float)array.Length;
				float num7 = Math.Max(0f, (float)Math.Sin(Math.PI * 6.0 * (double)num6)) * Math.Max(0f, (float)Math.Sin(Math.PI * 14.0 * (double)num6 + 1.0));
				if ((k & 0x3F) == 0)
				{
					biquad.SetBandPass(900f + 600f * (float)Math.Sin(Math.PI * 10.0 * (double)num6), 5f);
					biquad2.SetBandPass(2400f + 500f * (float)Math.Sin(Math.PI * 8.0 * (double)num6 + 1.0), 6f);
				}
				float x = noise.Next();
				array[k] += (biquad.Run(x) + biquad2.Run(x) * 0.6f) * 0.25f * num7;
			}
			Normalize(array, 0.6f);
			return array;
		}

		public static float[] WonderFireRoar(float seconds)
		{
			Noise noise = new Noise(9967);
			float[] array = New(seconds);
			Biquad biquad = Biquad.LowPass(160f);
			Biquad biquad2 = Biquad.LowPass(900f);
			for (int i = 0; i < array.Length; i++)
			{
				float num = (float)i / (float)array.Length;
				float num2 = 0.7f + 0.3f * (float)Math.Sin(Math.PI * 6.0 * (double)num) * (float)Math.Sin(Math.PI * 10.0 * (double)num + 0.6);
				float x = noise.Next();
				array[i] = biquad.Run(x) * 3f * num2 + biquad2.Run(x) * 0.25f * num2;
			}
			for (int j = 0; j < (int)(seconds * 6f); j++)
			{
				Grains(array, noise, noise.Unit() * (seconds - 0.2f), 0.12f, 120f, 1200f, 6000f, 0.5f, 0.0006f);
			}
			for (int k = 0; k < (int)(seconds * 1f); k++)
			{
				Knock(array, noise, noise.Unit() * (seconds - 0.1f), noise.Range(250f, 600f), 2f, 0.5f, 0.012f);
			}
			array = Crossfade(array, 11025);
			Normalize(array, 0.7f);
			return array;
		}

		public static float[] WonderIceWind(float seconds)
		{
			Noise noise = new Noise(9973);
			float[] array = New(seconds);
			Biquad biquad = Biquad.LowPass(400f);
			Biquad biquad2 = default(Biquad);
			Biquad biquad3 = default(Biquad);
			for (int i = 0; i < array.Length; i++)
			{
				float num = (float)i / (float)array.Length;
				float num2 = 0.55f + 0.3f * (float)Math.Sin(Math.PI * 4.0 * (double)num) + 0.15f * (float)Math.Sin(Math.PI * 10.0 * (double)num + 1.1);
				if ((i & 0x1F) == 0)
				{
					biquad2.SetBandPass(900f + 500f * (float)Math.Sin(Math.PI * 6.0 * (double)num + 0.4) * num2, 9f);
					biquad3.SetBandPass(1700f + 400f * (float)Math.Sin(Math.PI * 8.0 * (double)num + 2.0), 12f);
				}
				float x = noise.Next();
				array[i] = biquad.Run(x) * num2 * 1.4f + biquad2.Run(x) * num2 * num2 * 0.9f + biquad3.Run(x) * Math.Max(0f, num2 - 0.6f) * 1.2f;
			}
			for (int j = 0; j < (int)(seconds * 1.2f); j++)
			{
				Chime(array, noise.Unit() * (seconds - 0.8f), noise.Range(2800f, 5200f), 0.02f, 0.25f);
			}
			Creak(array, noise, seconds * 0.3f, 0.6f, 0.1f, 40f);
			Creak(array, noise, seconds * 0.72f, 0.4f, 0.08f, 55f);
			array = Crossfade(array, 14700);
			Normalize(array, 0.55f);
			return array;
		}

		public static float[] WonderAngleChime(float seconds)
		{
			Noise noise = new Noise(9979);
			float[] s = New(seconds);
			Pad(s, seconds, new float[5] { 440f, 554.4f, 659.3f, 880f, 1108.7f }, new float[5] { 0.1f, 0.08f, 0.08f, 0.06f, 0.04f }, 1f, 0.6f);
			Voice(s, seconds, 440f, 0.05f, 0.7f, 0.005f, 3);
			Voice(s, seconds, 659.3f, 0.04f, 0.7f, 0.005f, 4);
			float[] array = new float[4] { 1760f, 2217.5f, 2637f, 3520f };
			for (int i = 0; i < (int)(seconds * 1.4f); i++)
			{
				Chime(s, noise.Unit() * (seconds - 1f), array[(int)(noise.Unit() * (float)array.Length) % array.Length], 0.025f, 0.6f);
			}
			s = Crossfade(s, 14700);
			Normalize(s, 0.5f);
			return s;
		}
	}
}
