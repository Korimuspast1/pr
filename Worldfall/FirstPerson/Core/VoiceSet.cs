using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class VoiceSet
	{
		public sealed class Clip
		{
			public string Name;

			public float[] S;

			public int[] Marks;

			public float[] Period;

			public float[] Smooth;

			public float[] Peak;

			public float[] Even;
		}

		public struct Syllable
		{
			public int Clip;

			public int Start;

			public int End;

			public float Hz;

			public float Voiced;

			public float Bright;

			public float Level;

			public int Class;

			public bool Usable;

			public float Seconds => (float)(End - Start) / 48000f;
		}

		public const int Rate = 48000;

		public const int Hop = 240;

		public const int Unvoiced = 240;

		public string Kind;

		public readonly List<Clip> Clips = new List<Clip>();

		public readonly List<Syllable> Syllables = new List<Syllable>();

		public readonly List<int>[] ByClass = new List<int>[3]
		{
			new List<int>(),
			new List<int>(),
			new List<int>()
		};

		public readonly List<int>[] VoicedByClass = new List<int>[3]
		{
			new List<int>(),
			new List<int>(),
			new List<int>()
		};

		public float MedianHz;

		public float MedianLevel;

		public int Usable => ByClass[0].Count + ByClass[1].Count + ByClass[2].Count;

		public static VoiceSet Build(string kind, IList<KeyValuePair<string, float[]>> clips, Action between = null)
		{
			VoiceSet voiceSet = new VoiceSet
			{
				Kind = kind
			};
			foreach (KeyValuePair<string, float[]> clip2 in clips)
			{
				if (clip2.Value != null && clip2.Value.Length >= 2400)
				{
					Clip clip = Analyse(clip2.Key, clip2.Value, voiceSet.Clips.Count, voiceSet.Syllables);
					if (clip != null)
					{
						voiceSet.Clips.Add(clip);
					}
					between?.Invoke();
				}
			}
			voiceSet.Classify();
			return voiceSet;
		}

		private static Clip Analyse(string name, float[] raw, int index, List<Syllable> into)
		{
			float[] array = Trim(raw);
			if (array == null)
			{
				return null;
			}
			int num = array.Length / 240;
			if (num < 8)
			{
				return null;
			}
			float[] db = Loudness(array, num);
			float[] period = Pitch(array, num, db);
			Clip obj = new Clip
			{
				Name = name,
				S = array
			};
			Mark(obj, period);
			Cut(obj, index, db, period, into);
			return obj;
		}

		private static float[] Trim(float[] x)
		{
			double num = 0.0;
			for (int i = 0; i < x.Length; i++)
			{
				num += (double)x[i];
			}
			num /= (double)x.Length;
			float num2 = 0f;
			for (int j = 0; j < x.Length; j++)
			{
				num2 = Math.Max(num2, Math.Abs(x[j] - (float)num));
			}
			if (num2 < 0.0001f)
			{
				return null;
			}
			float num3 = num2 * 0.006f;
			int k = 0;
			int num4 = x.Length - 1;
			for (; k < x.Length && Math.Abs((double)x[k] - num) < (double)num3; k++)
			{
			}
			while (num4 > k && Math.Abs((double)x[num4] - num) < (double)num3)
			{
				num4--;
			}
			k = Math.Max(0, k - 480);
			num4 = Math.Min(x.Length - 1, num4 + 480);
			float[] array = new float[num4 - k + 1];
			for (int l = 0; l < array.Length; l++)
			{
				array[l] = x[k + l] - (float)num;
			}
			return array;
		}

		private static float[] Loudness(float[] s, int frames)
		{
			float[] array = new float[frames];
			for (int i = 0; i < frames; i++)
			{
				int num = i * 240 + 120;
				int num2 = Math.Max(0, num - 480);
				int num3 = Math.Min(s.Length, num + 480);
				double num4 = 0.0;
				for (int j = num2; j < num3; j++)
				{
					num4 += (double)(s[j] * s[j]);
				}
				array[i] = 10f * (float)Math.Log10(num4 / (double)Math.Max(1, num3 - num2) + 1E-12);
			}
			return array;
		}

		private static float[] Pitch(float[] s, int frames, float[] db)
		{
			float[] array = new float[31];
			float num = 0f;
			for (int i = 0; i < 31; i++)
			{
				double num2 = (double)i - 15.0;
				double num3 = 3.0 / 32.0;
				double num4 = ((num2 == 0.0) ? (2.0 * num3) : (Math.Sin(Math.PI * 2.0 * num3 * num2) / (Math.PI * num2)));
				array[i] = (float)(num4 * (0.54 - 0.46 * Math.Cos(Math.PI * 2.0 * (double)i / 30.0)));
				num += array[i];
			}
			int num5 = s.Length / 4;
			float[] array2 = new float[num5];
			for (int j = 0; j < num5; j++)
			{
				float num6 = 0f;
				int num7 = j * 4;
				for (int k = 0; k < 31; k++)
				{
					int num8 = num7 + k - 15;
					if (num8 >= 0 && num8 < s.Length)
					{
						num6 += s[num8] * array[k];
					}
				}
				array2[j] = num6 / num;
			}
			float num9 = float.MinValue;
			for (int l = 0; l < frames; l++)
			{
				num9 = Math.Max(num9, db[l]);
			}
			float[] array3 = new float[frames];
			float[] array4 = new float[186];
			for (int m = 0; m < frames; m++)
			{
				if (db[m] < num9 - 30f)
				{
					continue;
				}
				int num10 = (m * 240 + 120) / 4 - 180;
				if (num10 < 0 || num10 + 360 + 184 + 1 >= num5)
				{
					continue;
				}
				double num11 = 0.0;
				for (int n = 0; n < 360; n++)
				{
					num11 += (double)(array2[num10 + n] * array2[num10 + n]);
				}
				if (num11 <= 1E-12)
				{
					continue;
				}
				float num12 = 0f;
				for (int num13 = 20; num13 <= 185; num13++)
				{
					double num14 = 0.0;
					double num15 = 0.0;
					for (int num16 = 0; num16 < 360; num16++)
					{
						float num17 = array2[num10 + num16 + num13];
						num14 += (double)(array2[num10 + num16] * num17);
						num15 += (double)(num17 * num17);
					}
					array4[num13] = ((num15 > 1E-12) ? ((float)(num14 / Math.Sqrt(num11 * num15))) : 0f);
					if (num13 <= 184)
					{
						num12 = Math.Max(num12, array4[num13]);
					}
				}
				if (num12 < 0.6f)
				{
					continue;
				}
				int num18 = 0;
				for (int num19 = 21; num19 <= 184; num19++)
				{
					if (array4[num19] >= 0.88f * num12 && array4[num19] >= array4[num19 - 1] && array4[num19] >= array4[num19 + 1])
					{
						num18 = num19;
						break;
					}
				}
				if (num18 != 0)
				{
					array3[m] = Refine(s, m * 240 + 120, num18 * 4);
				}
			}
			float[] array5 = new float[frames];
			List<float> list = new List<float>(5);
			for (int num20 = 0; num20 < frames; num20++)
			{
				if (array3[num20] <= 0f)
				{
					continue;
				}
				list.Clear();
				for (int num21 = -2; num21 <= 2; num21++)
				{
					int num22 = num20 + num21;
					if (num22 >= 0 && num22 < frames && array3[num22] > 0f)
					{
						list.Add(array3[num22]);
					}
				}
				list.Sort();
				array5[num20] = ((list.Count >= 2) ? list[list.Count / 2] : 0f);
			}
			for (int num23 = 1; num23 + 1 < frames; num23++)
			{
				if (array5[num23] <= 0f && array5[num23 - 1] > 0f && array5[num23 + 1] > 0f)
				{
					array5[num23] = 0.5f * (array5[num23 - 1] + array5[num23 + 1]);
				}
			}
			for (int num24 = 0; num24 < frames; num24++)
			{
				if (array5[num24] <= 0f)
				{
					continue;
				}
				int num25;
				for (num25 = num24; num25 < frames && array5[num25] > 0f; num25++)
				{
				}
				if (num25 - num24 < 3)
				{
					for (int num26 = num24; num26 < num25; num26++)
					{
						array5[num26] = 0f;
					}
				}
				else
				{
					Fold(array5, num24, num25);
				}
				num24 = num25;
			}
			return array5;
		}

		private static void Fold(float[] period, int a, int b)
		{
			List<float> list = new List<float>(b - a);
			for (int i = a; i < b; i++)
			{
				list.Add(period[i]);
			}
			list.Sort();
			float num = list[list.Count / 2];
			for (int j = a; j < b; j++)
			{
				float num2 = period[j] / num;
				if (num2 > 1.75f && num2 < 2.3f)
				{
					period[j] *= 0.5f;
				}
				else if (num2 > 0.43f && num2 < 0.57f)
				{
					period[j] *= 2f;
				}
			}
		}

		public static float PitchOf(float[] s)
		{
			List<float> list = new List<float>();
			float[] array = Pitches(s);
			foreach (float num in array)
			{
				if (num > 0f)
				{
					list.Add(num);
				}
			}
			list.Sort();
			if (list.Count <= 0)
			{
				return 0f;
			}
			return list[list.Count / 2];
		}

		public static float[] Pitches(float[] s)
		{
			if (s == null || s.Length < 1920)
			{
				return new float[0];
			}
			int num = s.Length / 240;
			float[] array = Pitch(s, num, Loudness(s, num));
			float[] array2 = new float[num];
			for (int i = 0; i < num; i++)
			{
				array2[i] = ((array[i] > 0f) ? (48000f / array[i]) : 0f);
			}
			return array2;
		}

		private static float Refine(float[] s, int centre, int coarse)
		{
			int num = centre - 720;
			int num2 = Math.Max(73, coarse - 6);
			int num3 = coarse + 6;
			if (num < 0 || num + 1440 + num3 + 1 >= s.Length)
			{
				return coarse;
			}
			double num4 = 0.0;
			for (int i = 0; i < 1440; i++)
			{
				num4 += (double)(s[num + i] * s[num + i]);
			}
			float num5 = -1f;
			int num6 = coarse;
			float[] array = new float[num3 + 2];
			for (int j = num2 - 1; j <= num3 + 1; j++)
			{
				double num7 = 0.0;
				double num8 = 0.0;
				for (int k = 0; k < 1440; k++)
				{
					float num9 = s[num + k + j];
					num7 += (double)(s[num + k] * num9);
					num8 += (double)(num9 * num9);
				}
				array[j] = ((num4 * num8 > 1E-18) ? ((float)(num7 / Math.Sqrt(num4 * num8))) : 0f);
				if (j >= num2 && j <= num3 && array[j] > num5)
				{
					num5 = array[j];
					num6 = j;
				}
			}
			float num10 = array[num6 - 1];
			float num11 = array[num6];
			float num12 = array[num6 + 1];
			float num13 = num10 - 2f * num11 + num12;
			float val = ((Math.Abs(num13) > 1E-06f) ? (0.5f * (num10 - num12) / num13) : 0f);
			return (float)num6 + Math.Max(-0.5f, Math.Min(0.5f, val));
		}

		private static void Mark(Clip c, float[] period)
		{
			float[] s = c.S;
			float num = 0f;
			float num2 = 0f;
			for (int i = 0; i < s.Length; i++)
			{
				if (s[i] > num)
				{
					num = s[i];
				}
				if (0f - s[i] > num2)
				{
					num2 = 0f - s[i];
				}
			}
			float num3 = ((num >= num2) ? 1f : (-1f));
			List<int> list = new List<int>();
			List<float> list2 = new List<float>();
			int num4 = period.Length;
			int num5 = 0;
			int num6 = -240;
			bool flag = false;
			while (num5 < s.Length)
			{
				float num7 = period[Math.Min(num4 - 1, num5 / 240)];
				if (num7 > 0f)
				{
					int num8 = (flag ? (num6 + (int)(0.8f * num7)) : num5);
					int num9 = Math.Min(s.Length - 1, flag ? (num6 + (int)(1.2f * num7)) : (num5 + (int)num7));
					if (num8 > num9)
					{
						break;
					}
					int num10 = num8;
					float num11 = float.MinValue;
					for (int j = num8; j <= num9; j++)
					{
						float num12 = s[j] * num3;
						if (num12 > num11)
						{
							num11 = num12;
							num10 = j;
						}
					}
					list.Add(num10);
					list2.Add(num7);
					num6 = num10;
					flag = true;
					num5 = num10 + (int)(0.8f * num7);
				}
				else
				{
					int num13 = Math.Max(num5, num6 + 240);
					if (num13 >= s.Length)
					{
						break;
					}
					list.Add(num13);
					list2.Add(0f);
					num6 = num13;
					flag = false;
					num5 = num13 + 240;
				}
			}
			c.Marks = list.ToArray();
			c.Period = list2.ToArray();
			Steady(c);
		}

		private static void Steady(Clip c)
		{
			int num = c.Marks.Length;
			c.Smooth = new float[num];
			c.Peak = new float[num];
			c.Even = new float[num];
			for (int i = 0; i < num; i++)
			{
				float num2 = c.Period[i];
				if (!(num2 <= 0f))
				{
					int num3 = Math.Max(4, (int)(num2 * 0.5f));
					float num4 = 0f;
					for (int j = Math.Max(0, c.Marks[i] - num3); j <= Math.Min(c.S.Length - 1, c.Marks[i] + num3); j++)
					{
						num4 = Math.Max(num4, Math.Abs(c.S[j]));
					}
					c.Peak[i] = num4;
				}
			}
			List<float> list = new List<float>(7);
			for (int k = 0; k < num; k++)
			{
				if (c.Period[k] <= 0f)
				{
					continue;
				}
				list.Clear();
				for (int l = k - 3; l <= k + 3; l++)
				{
					if (l >= 0 && l < num && c.Period[l] > 0f)
					{
						list.Add(c.Period[l]);
					}
				}
				list.Sort();
				c.Smooth[k] = list[list.Count / 2];
				list.Clear();
				for (int m = k - 3; m <= k + 3; m++)
				{
					if (m >= 0 && m < num && c.Period[m] > 0f)
					{
						list.Add(c.Peak[m]);
					}
				}
				list.Sort();
				c.Even[k] = list[list.Count / 2];
			}
			float[] array = (float[])c.Smooth.Clone();
			for (int n = 1; n + 1 < num; n++)
			{
				if (array[n] > 0f && array[n - 1] > 0f && array[n + 1] > 0f)
				{
					c.Smooth[n] = 0.25f * array[n - 1] + 0.5f * array[n] + 0.25f * array[n + 1];
				}
			}
		}

		private static void Cut(Clip c, int clipIndex, float[] db, float[] period, List<Syllable> into)
		{
			int num = db.Length;
			float[] array = new float[num];
			for (int i = 0; i < num; i++)
			{
				float num2 = 0f;
				int num3 = 0;
				for (int j = -1; j <= 1; j++)
				{
					int num4 = i + j;
					if (num4 >= 0 && num4 < num)
					{
						num2 += db[num4];
						num3++;
					}
				}
				array[i] = num2 / (float)num3;
			}
			float num5 = float.MinValue;
			for (int k = 0; k < num; k++)
			{
				num5 = Math.Max(num5, array[k]);
			}
			float num6 = num5 - 35f;
			List<int> list = new List<int>();
			for (int l = 1; l + 1 < num; l++)
			{
				if (array[l] >= num5 - 24f && array[l] >= array[l - 1] && array[l] > array[l + 1])
				{
					list.Add(l);
				}
			}
			if (list.Count == 0)
			{
				return;
			}
			List<int> list2 = new List<int>();
			int num7 = list[0];
			for (int m = 1; m < list.Count; m++)
			{
				int num8 = list[m];
				int num9 = num7;
				for (int n = num7; n <= num8; n++)
				{
					if (array[n] < array[num9])
					{
						num9 = n;
					}
				}
				if (array[num9] <= Math.Min(array[num7], array[num8]) - 5f && (num9 - ((list2.Count > 0) ? list2[list2.Count - 1] : 0)) * 240 >= 2666)
				{
					list2.Add(num9);
					num7 = num8;
				}
				else if (array[num8] > array[num7])
				{
					num7 = num8;
				}
			}
			int num10 = 0;
			int num11 = num - 1;
			for (; num10 < num && array[num10] < num6; num10++)
			{
			}
			while (num11 > num10 && array[num11] < num6)
			{
				num11--;
			}
			List<int> list3 = new List<int> { num10 };
			foreach (int item in list2)
			{
				if (item > num10 && item < num11)
				{
					list3.Add(item);
				}
			}
			list3.Add(num11 + 1);
			for (int num12 = 0; num12 + 1 < list3.Count; num12++)
			{
				int num13 = list3[num12];
				int num14 = list3[num12 + 1];
				if ((num14 - num13) * 240 > 21600)
				{
					int num15 = num13 + (num14 - num13) / 4;
					for (int num16 = num13 + (num14 - num13) / 4; num16 < num14 - (num14 - num13) / 4; num16++)
					{
						if (array[num16] < array[num15])
						{
							num15 = num16;
						}
					}
					list3.Insert(num12 + 1, num15);
					num14 = num15;
				}
				into.Add(Measure(c, clipIndex, num13, num14, array, period));
			}
		}

		private static Syllable Measure(Clip c, int clipIndex, int fa, int fb, float[] e, float[] period)
		{
			Syllable result = new Syllable
			{
				Clip = clipIndex,
				Start = fa * 240,
				End = Math.Min(c.S.Length, fb * 240)
			};
			float num = float.MinValue;
			for (int i = fa; i < fb; i++)
			{
				num = Math.Max(num, e[i]);
			}
			List<float> list = new List<float>();
			int num2 = 0;
			int num3 = 0;
			double num4 = 0.0;
			double num5 = 0.0;
			double num6 = 0.0;
			int num7 = 0;
			for (int j = fa; j < fb; j++)
			{
				num3++;
				if (period[j] > 0f)
				{
					num2++;
					list.Add(48000f / period[j]);
				}
				if (!(e[j] < num - 15f))
				{
					int num8 = j * 240;
					int num9 = Math.Min(c.S.Length, num8 + 240);
					for (int k = Math.Max(1, num8); k < num9; k++)
					{
						float num10 = c.S[k];
						float num11 = num10 - c.S[k - 1];
						num4 += (double)(num10 * num10);
						num5 += (double)(num11 * num11);
					}
					num6 += Math.Pow(10.0, e[j] / 10f);
					num7++;
				}
			}
			list.Sort();
			result.Voiced = ((num3 > 0) ? ((float)num2 / (float)num3) : 0f);
			result.Hz = ((list.Count >= 3 && result.Voiced >= 0.3f) ? list[list.Count / 2] : 0f);
			result.Bright = ((num4 > 1E-12) ? ((float)(num5 / num4)) : 0f);
			result.Level = ((num7 > 0) ? (10f * (float)Math.Log10(num6 / (double)num7 + 1E-12)) : (-120f));
			return result;
		}

		private void Classify()
		{
			List<float> list = new List<float>();
			foreach (Syllable syllable2 in Syllables)
			{
				list.Add(syllable2.Level);
			}
			list.Sort();
			MedianLevel = ((list.Count > 0) ? list[list.Count / 2] : (-30f));
			List<float> list2 = new List<float>();
			List<float> list3 = new List<float>();
			for (int i = 0; i < Syllables.Count; i++)
			{
				Syllable value = Syllables[i];
				value.Usable = value.Seconds >= 0.05f && value.Level >= MedianLevel - 14f;
				Syllables[i] = value;
				if (value.Usable)
				{
					list2.Add(value.Bright);
					if (value.Hz > 0f)
					{
						list3.Add(value.Hz);
					}
				}
			}
			list2.Sort();
			list3.Sort();
			MedianHz = ((list3.Count > 0) ? list3[list3.Count / 2] : 0f);
			float num = ((list2.Count > 0) ? list2[list2.Count / 3] : 0f);
			float num2 = ((list2.Count > 0) ? list2[list2.Count * 2 / 3] : 0f);
			for (int j = 0; j < Syllables.Count; j++)
			{
				Syllable value2 = Syllables[j];
				if (value2.Usable)
				{
					value2.Class = ((!(value2.Bright < num)) ? ((value2.Bright < num2) ? 1 : 2) : 0);
					Syllables[j] = value2;
					ByClass[value2.Class].Add(j);
				}
			}
			for (int k = 0; k < 3; k++)
			{
				if (ByClass[k].Count != 0)
				{
					continue;
				}
				for (int l = 0; l < 3; l++)
				{
					if (l != k)
					{
						ByClass[k].AddRange(ByClass[l]);
					}
				}
			}
			List<int> list4 = new List<int>();
			for (int m = 0; m < Syllables.Count; m++)
			{
				Syllable syllable = Syllables[m];
				if (syllable.Usable && syllable.Voiced >= 0.3f && syllable.Hz > 0f && (MedianHz <= 0f || (syllable.Hz > 0.6f * MedianHz && syllable.Hz < 1.6f * MedianHz)))
				{
					list4.Add(m);
				}
			}
			for (int n = 0; n < 3; n++)
			{
				foreach (int item in ByClass[n])
				{
					if (list4.Contains(item))
					{
						VoicedByClass[n].Add(item);
					}
				}
				if (VoicedByClass[n].Count < 2)
				{
					VoicedByClass[n].Clear();
					VoicedByClass[n].AddRange(list4);
				}
			}
		}
	}
}
