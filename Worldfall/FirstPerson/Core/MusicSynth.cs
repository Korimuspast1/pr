using System;
using System.Collections.Generic;
using System.Globalization;

namespace FirstPerson.Core
{
	public static class MusicSynth
	{
		private sealed class Key
		{
			public int Tonic;

			public int[] Mode;

			public Key(int tonic, int[] mode)
			{
				Tonic = tonic;
				Mode = mode;
			}

			public int Note(int degree)
			{
				int num = (int)Math.Floor((double)degree / 7.0);
				int num2 = degree - num * 7;
				return Tonic + num * 12 + Mode[num2];
			}

			public int Degree(int midi)
			{
				int result = 0;
				int num = int.MaxValue;
				for (int i = -21; i < 35; i++)
				{
					int num2 = Math.Abs(Note(i) - midi);
					if (num2 < num)
					{
						num = num2;
						result = i;
					}
				}
				return result;
			}
		}

		private struct Chord
		{
			public int Root;

			public int[] Tones;

			public float Beats;

			public int Sharpen;
		}

		private sealed class Mix
		{
			public readonly float[] L;

			public readonly float[] R;

			public readonly float[] SendL;

			public readonly float[] SendR;

			public int Frames => L.Length;

			public Mix(int frames)
			{
				L = new float[frames];
				R = new float[frames];
				SendL = new float[frames];
				SendR = new float[frames];
			}

			public void Put(int i, float v, float gl, float gr, float send)
			{
				if ((uint)i < (uint)L.Length)
				{
					float num = v * gl;
					float num2 = v * gr;
					L[i] += num;
					R[i] += num2;
					SendL[i] += num * send;
					SendR[i] += num2 * send;
				}
			}

			public void PutLR(int i, float l, float r, float send)
			{
				if ((uint)i < (uint)L.Length)
				{
					L[i] += l;
					R[i] += r;
					SendL[i] += l * send;
					SendR[i] += r * send;
				}
			}
		}

		private struct Svf
		{
			private float _low;

			private float _band;

			private float _f;

			public void Tune(float hz)
			{
				_f = 2f * (float)Math.Sin(Math.PI * (double)Math.Min(hz, 5333.3335f) / 32000.0);
			}

			public float Low(float x, float q)
			{
				float num = x - _low - q * _band;
				_band += _f * num;
				_low += _f * _band;
				return _low;
			}

			public float Band(float x, float q)
			{
				float num = x - _low - q * _band;
				_band += _f * num;
				_low += _f * _band;
				return _band;
			}
		}

		private sealed class Rng
		{
			private uint _s;

			public Rng(int seed)
			{
				_s = (uint)(seed * -1640531535 + -1640531527);
				if (_s == 0)
				{
					_s = 1u;
				}
			}

			public uint Next()
			{
				_s ^= _s << 13;
				_s ^= _s >> 17;
				_s ^= _s << 5;
				return _s;
			}

			public float Unit()
			{
				return (float)(Next() & 0xFFFFFF) / 16777216f;
			}

			public int Int(int n)
			{
				return (int)(Next() % (uint)Math.Max(1, n));
			}

			public float Range(float a, float b)
			{
				return a + (b - a) * Unit();
			}

			public bool Chance(float p)
			{
				return Unit() < p;
			}

			public T Pick<T>(T[] a)
			{
				return a[Int(a.Length)];
			}
		}

		private struct Note
		{
			public float Beat;

			public float Beats;

			public int Midi;

			public float Vel;
		}

		private sealed class Theme
		{
			public string Name;

			public string Mood;

			public int[] Mode;

			public int Meter;

			public float Bpm0;

			public float Bpm1;

			public int[] Tonics;

			public string ChordsA;

			public string TuneA;

			public string ChordsB;

			public string TuneB;
		}

		private struct Tn
		{
			public float Beat;

			public float Beats;

			public int Degree;

			public int Acc;
		}

		private sealed class Section
		{
			public string Kind;

			public int Bar0;

			public Chord[] Chords;

			public List<Tn> Tune;

			public Key Key;

			public int Bars => Chords.Length;

			public bool Verse => Kind[0] == 'A';
		}

		private sealed class Performance
		{
			public Theme Theme;

			public Key Key;

			public float Spb;

			public int Meter;

			public List<Section> Sections = new List<Section>();

			public Mix Mix;

			public Rng Rng;

			public int Seed;

			public float Seconds(int bar, float beat = 0f)
			{
				return ((float)(bar * Meter) + beat) * Spb;
			}
		}

		public const float StingLead = 0.06f;

		private static int _lastBattle = -1;

		private const string ClashChords = "1 1 6 6 4 4 5^ 5^ 1 6 4 5^ 1 6 4 5^";

		private const string ClashTune = "1/1.5 1/0.5 5/1 5/1 | 8/1.5 7/0.5 5/2 | 6/1.5 6/0.5 8/1 10/1 | 10/3 r/1 | 4/1.5 4/0.5 6/1 8/1 | 11/1.5 10/0.5 8/2 | 9/1.5 8/0.5 #7/1 5/1 | 9/3 r/1 | 8/1.5 8/0.5 12/1 12/1 | 13/1.5 12/0.5 10/2 | 11/1.5 10/0.5 8/1 11/1 | 12/2 9/1 #7/1 | 8/1.5 10/0.5 12/2 | 13/2 10/2 | 11/2 8/2 | 9/2 #7/2";

		private const string HuntChords = "1 1 7 7 4 4 5^ 5^ 1 7 4 5^ 1 7 4 5^";

		private const string HuntTune = "1/0.75 1/0.25 5/1 5/0.75 6/0.25 5/1 | 8/1.5 7/0.5 5/2 | 7/0.75 7/0.25 9/1 11/1.5 9/0.5 | 9/2 7/2 | 6/0.75 6/0.25 8/1 11/1.5 10/0.5 | 8/2 6/2 | 5/1 #7/1 9/1.5 #7/0.5 | 9/2 5/2 | 8/0.75 8/0.25 12/1 12/0.75 13/0.25 12/1 | 11/1.5 9/0.5 7/2 | 8/1 11/1 13/1.5 11/0.5 | 12/1.5 9/0.5 #7/2 | 8/0.75 8/0.25 10/1 12/2 | 11/1 9/1 7/1 9/1 | 8/2 6/2 | 5/1 #7/1 9/2";

		public const int Rate = 32000;

		private static readonly int[] Ionian = new int[7] { 0, 2, 4, 5, 7, 9, 11 };

		private static readonly int[] Aeolian = new int[7] { 0, 2, 3, 5, 7, 8, 10 };

		private static readonly int[] Dorian = new int[7] { 0, 2, 3, 5, 7, 9, 10 };

		private static readonly int[] Mixolydian = new int[7] { 0, 2, 4, 5, 7, 9, 10 };

		private static readonly int[] Lydian = new int[7] { 0, 2, 4, 6, 7, 9, 11 };

		private static readonly int[] Harmonic = new int[7] { 0, 2, 3, 5, 7, 8, 11 };

		private static readonly int[][] BrightBook = new int[7][]
		{
			new int[4] { 0, 4, 5, 3 },
			new int[4] { 0, 3, 0, 4 },
			new int[4] { 5, 3, 0, 4 },
			new int[4] { 0, 5, 3, 4 },
			new int[4] { 3, 0, 4, 5 },
			new int[4] { 0, 2, 3, 4 },
			new int[4] { 0, 3, 5, 4 }
		};

		private static readonly int[][] DarkBook = new int[5][]
		{
			new int[4] { 0, 5, 6, 0 },
			new int[4] { 0, 3, 5, 4 },
			new int[4] { 0, 6, 5, 6 },
			new int[4] { 0, 5, 2, 6 },
			new int[4] { 0, 3, 6, 2 }
		};

		private const int SineSize = 4096;

		private static readonly float[] Sine = BuildSine();

		private static readonly float[][] Rhythms44 = new float[7][]
		{
			new float[5] { 1f, 0.5f, 0.5f, 1f, 1f },
			new float[4] { 1.5f, 0.5f, 1f, 1f },
			new float[6] { 0.5f, 0.5f, 0.5f, 0.5f, 1f, 1f },
			new float[3] { 2f, 1f, 1f },
			new float[3] { 1f, 1f, 2f },
			new float[6] { 0.5f, 0.5f, 1f, 0.5f, 0.5f, 1f },
			new float[4] { 1f, 1.5f, 0.5f, 1f }
		};

		private static readonly float[][] Rhythms34 = new float[5][]
		{
			new float[4] { 1f, 0.5f, 0.5f, 1f },
			new float[2] { 2f, 1f },
			new float[3] { 1.5f, 0.5f, 1f },
			new float[5] { 0.5f, 0.5f, 0.5f, 0.5f, 1f },
			new float[3] { 1f, 1f, 1f }
		};

		private const float TwoPi = MathF.PI * 2f;

		private static readonly Theme[] Themes = new Theme[11]
		{
			new Theme
			{
				Name = "meadow",
				Mood = "day",
				Mode = Ionian,
				Meter = 4,
				Bpm0 = 72f,
				Bpm1 = 80f,
				Tonics = new int[5] { 62, 67, 65, 60, 63 },
				ChordsA = "1 3 4 1 6 4 29 5",
				TuneA = "3/1.5 4/0.5 5/2 | 7/1.5 6/0.5 5/2 | 4/1 6/1 8/1.5 7/0.5 | 5/4 | 6/1.5 5/0.5 3/1 1/1 | 4/1.5 5/0.5 6/1 8/1 | 9/1.5 8/0.5 6/1 4/1 | 5/3 r/1",
				ChordsB = "4 5 3 6 2 57 1 1",
				TuneB = "6/1 8/1 11/1.5 10/0.5 | 9/2 7/1 5/1 | 7/1 8/0.5 7/0.5 5/1 3/1 | 8/2 6/2 | 6/1 9/1.5 8/0.5 6/1 | 5/1 7/1 9/1 11/1 | 10/3 9/1 | 8/4"
			},
			new Theme
			{
				Name = "daydream",
				Mood = "day",
				Mode = Lydian,
				Meter = 4,
				Bpm0 = 64f,
				Bpm1 = 72f,
				Tonics = new int[4] { 65, 60, 62, 67 },
				ChordsA = "19 2 19 2 3 6 2 2",
				TuneA = "5/1.5 6/0.5 5/1 3/1 | 4/1.5 5/0.5 6/2 | 5/1.5 6/0.5 5/1 8/1 | 9/2 6/2 | 7/1.5 8/0.5 7/1 5/1 | 6/1 8/1 10/2 | 11/1.5 10/0.5 9/1 6/1 | 6/4",
				ChordsB = "6 3 5 19 6 3 2 19",
				TuneB = "10/2 8/1 6/1 | 7/2 5/2 | 9/1.5 8/0.5 7/1 5/1 | 3/2 5/2 | 6/1 8/1 10/2 | 12/1.5 10/0.5 7/2 | 11/2 9/1 6/1 | 8/4"
			},
			new Theme
			{
				Name = "waltz",
				Mood = "day",
				Mode = Ionian,
				Meter = 3,
				Bpm0 = 96f,
				Bpm1 = 108f,
				Tonics = new int[4] { 62, 67, 69, 64 },
				ChordsA = "1 1 4 1 6 2 5 5",
				TuneA = "3/2 5/1 | 8/2 7/0.5 6/0.5 | 6/2 4/1 | 5/3 | 8/2 6/1 | 9/1.5 8/0.5 6/1 | 5/1 7/1 9/1 | 9/3",
				ChordsB = "4 5 3 6 4 1 57 1",
				TuneB = "8/1 6/1 4/1 | 7/2 5/1 | 5/2 7/1 | 8/3 | 6/1 8/1 6/1 | 5/2 3/1 | 2/1 5/1 7/1 | 8/3"
			},
			new Theme
			{
				Name = "hymn",
				Mood = "day",
				Mode = Ionian,
				Meter = 4,
				Bpm0 = 60f,
				Bpm1 = 68f,
				Tonics = new int[4] { 63, 65, 60, 62 },
				ChordsA = "1 4 6 5 4 1 2 5",
				TuneA = "3/2 5/2 | 6/2 4/2 | 8/2 6/2 | 7/1 9/1 5/2 | 6/1.5 5/0.5 4/1 6/1 | 5/2 8/2 | 9/2 6/1 4/1 | 5/1 7/1 9/2",
				ChordsB = "6 3 4 1 4 5 1 1",
				TuneB = "8/1 10/1 8/1 6/1 | 7/2 5/2 | 6/1 8/1 6/1 4/1 | 3/2 5/2 | 4/1 6/1 8/1 6/1 | 7/2 9/2 | 8/2 10/1 9/1 | 8/4"
			},
			new Theme
			{
				Name = "moonlight",
				Mood = "night",
				Mode = Aeolian,
				Meter = 4,
				Bpm0 = 58f,
				Bpm1 = 64f,
				Tonics = new int[5] { 57, 62, 64, 59, 60 },
				ChordsA = "19 6 3 7 19 6 4 5^",
				TuneA = "5/2 8/1.5 7/0.5 | 8/3 6/1 | 7/1.5 5/0.5 3/2 | 2/3 r/1 | 5/2 10/1.5 9/0.5 | 8/2 6/2 | 6/1 8/1 11/1.5 10/0.5 | 9/2 #7/2",
				ChordsB = "6 7 3 6 4 7 5^7 19",
				TuneB = "10/2 8/1 6/1 | 9/2 11/1 9/1 | 7/2 5/1 3/1 | 8/4 | 11/1.5 10/0.5 8/2 | 9/2 7/2 | 5/1 #7/1 9/1 11/1 | 10/1 9/1 8/2"
			},
			new Theme
			{
				Name = "lullaby",
				Mood = "night",
				Mode = Ionian,
				Meter = 3,
				Bpm0 = 66f,
				Bpm1 = 74f,
				Tonics = new int[4] { 65, 67, 62, 60 },
				ChordsA = "1 4 1 5 1 4 5 1",
				TuneA = "5/2 3/1 | 6/2 4/1 | 3/2 5/1 | 2/3 | 5/2 8/1 | 8/2 6/1 | 9/2 7/1 | 8/3",
				ChordsB = "6 3 4 1 6 2 57 1",
				TuneB = "10/2 8/1 | 7/2 5/1 | 8/2 6/1 | 5/3 | 6/1 8/1 10/1 | 9/2 6/1 | 7/1 9/1 7/1 | 8/3"
			},
			new Theme
			{
				Name = "starlight",
				Mood = "night",
				Mode = Dorian,
				Meter = 4,
				Bpm0 = 54f,
				Bpm1 = 60f,
				Tonics = new int[4] { 62, 57, 64, 59 },
				ChordsA = "19 4 19 4 3 7 4 1",
				TuneA = "8/2 5/2 | 6/1.5 5/0.5 4/2 | 3/1 5/1 8/1 7/1 | 8/1 10/1 11/2 | 10/1.5 8/0.5 7/2 | 11/2 9/1 7/1 | 8/1.5 6/0.5 4/2 | 5/2 8/2",
				ChordsB = "7 4 3 7 4 5 2 19",
				TuneB = "9/2 11/2 | 11/2 8/1 6/1 | 7/2 10/2 | 9/1.5 7/0.5 4/2 | 6/1 8/1 11/1 8/1 | 9/2 7/2 | 6/2 4/2 | 3/1 2/1 1/2"
			},
			new Theme
			{
				Name = "nocturne",
				Mood = "night",
				Mode = Aeolian,
				Meter = 4,
				Bpm0 = 62f,
				Bpm1 = 68f,
				Tonics = new int[4] { 57, 64, 62, 59 },
				ChordsA = "19 4 7 3 6 4 5^ 5^7",
				TuneA = "8/1.5 7/0.5 8/1 10/1 | 11/1.5 10/0.5 8/2 | 9/1.5 8/0.5 9/1 11/1 | 12/2 10/1 7/1 | 8/1.5 7/0.5 8/1 10/1 | 11/1 10/1 8/1 6/1 | #7/2 9/1 11/1 | 9/3 r/1",
				ChordsB = "6 7 3 19 4 7 5^7 19",
				TuneB = "10/2 8/1 6/1 | 7/1 9/1 11/2 | 10/2 7/1 5/1 | 8/3 r/1 | 6/1 8/1 11/1 10/1 | 11/1.5 9/0.5 7/2 | 9/1 #7/1 5/1 #7/1 | 8/4"
			},
			new Theme
			{
				Name = "jig",
				Mood = "town",
				Mode = Mixolydian,
				Meter = 6,
				Bpm0 = 318f,
				Bpm1 = 348f,
				Tonics = new int[3] { 62, 67, 69 },
				ChordsA = "1 7 1 5 1 7 4 1",
				TuneA = "5/1 3/1 5/1 8/2 5/1 | 7/2 9/1 11/2 9/1 | 8/1 5/1 3/1 5/2 8/1 | 9/3 7/2 5/1 | 5/1 3/1 5/1 8/1 9/1 10/1 | 11/2 9/1 7/2 9/1 | 8/1 6/1 4/1 6/2 7/1 | 8/2 5/1 8/3",
				ChordsB = "4 1 7 1 4 1 7 1",
				TuneB = "6/1 8/1 6/1 4/2 6/1 | 5/1 8/1 5/1 3/2 5/1 | 4/1 7/1 9/1 11/2 9/1 | 10/3 8/3 | 11/1 10/1 8/1 6/2 8/1 | 10/1 8/1 5/1 3/2 5/1 | 7/1 9/1 7/1 4/1 5/1 7/1 | 8/3 r/3"
			},
			new Theme
			{
				Name = "market",
				Mood = "town",
				Mode = Ionian,
				Meter = 4,
				Bpm0 = 108f,
				Bpm1 = 118f,
				Tonics = new int[4] { 62, 67, 65, 69 },
				ChordsA = "1 4 5 1 1 4 57 1",
				TuneA = "1/0.5 3/0.5 5/1 5/0.5 6/0.5 5/1 | 6/1 8/1 6/0.5 5/0.5 4/1 | 5/1 7/0.5 9/0.5 7/1 5/1 | 8/1.5 7/0.5 8/2 | 10/1 8/1 5/1 8/1 | 11/1 10/0.5 8/0.5 6/1 4/1 | 5/0.5 7/0.5 9/1 7/0.5 9/0.5 11/1 | 10/1 9/1 8/2",
				ChordsB = "6 3 4 1 2 5 1 5",
				TuneB = "6/1 8/1 10/1 8/1 | 7/1 5/1 3/1 5/1 | 4/1 6/1 8/1 6/1 | 5/2 3/1 5/1 | 9/1 6/1 4/1 6/1 | 7/1 5/1 7/1 9/1 | 8/1 10/1 8/1 5/1 | 9/2 7/1 5/1"
			},
			new Theme
			{
				Name = "hearth",
				Mood = "town",
				Mode = Dorian,
				Meter = 3,
				Bpm0 = 80f,
				Bpm1 = 90f,
				Tonics = new int[3] { 62, 64, 57 },
				ChordsA = "1 3 7 4 1 3 5 1",
				TuneA = "1/1 3/1 5/1 | 7/2 5/1 | 9/2 7/1 | 8/2 6/1 | 5/2 3/1 | 5/1 7/1 10/1 | 9/2 7/1 | 8/3",
				ChordsB = "4 1 7 3 4 1 5 1",
				TuneB = "11/2 8/1 | 10/2 8/1 | 9/2 7/1 | 10/1.5 9/0.5 7/1 | 6/1.5 8/0.5 11/1 | 10/2 8/1 | 9/1 7/1 5/1 | 8/3"
			}
		};

		private static readonly object PickLock = new object();

		private static readonly Dictionary<string, string> LastPick = new Dictionary<string, string>();

		public static MusicPiece[] BattleAny(int seed)
		{
			Rng rng = new Rng(seed * 41 + 5);
			int num;
			lock (PickLock)
			{
				do
				{
					num = rng.Int(3);
				}
				while (num == _lastBattle);
				_lastBattle = num;
			}
			MusicPiece[] collection;
			Key k;
			float spb;
			switch (num)
			{
			case 1:
				collection = Clash(seed, out k, out spb);
				break;
			case 2:
				collection = Hunt(seed, out k, out spb);
				break;
			default:
			{
				collection = Battle(seed);
				Rng rng2 = new Rng(seed * 17 + 9);
				k = new Key(rng2.Pick(new int[4] { 62, 64, 57, 60 }) - 24, Aeolian);
				spb = 60f / rng2.Range(136f, 146f);
				break;
			}
			}
			List<MusicPiece> list = new List<MusicPiece>(collection);
			list.AddRange(Endings(k, spb, seed));
			return list.ToArray();
		}

		private static void BattleTheme(string chords, string tune, out Chord[] bars, out List<Tn> notes)
		{
			bars = ParseChords(chords, 4);
			notes = ParseTune(tune);
		}

		private static MusicPiece[] Clash(int seed, out Key k, out float spb)
		{
			Rng rng = new Rng(seed * 23 + 17);
			k = new Key(rng.Pick(new int[5] { 62, 64, 57, 60, 59 }) - 24, Aeolian);
			spb = 60f / rng.Range(144f, 152f);
			BattleTheme("1 1 6 6 4 4 5^ 5^ 1 6 4 5^ 1 6 4 5^", "1/1.5 1/0.5 5/1 5/1 | 8/1.5 7/0.5 5/2 | 6/1.5 6/0.5 8/1 10/1 | 10/3 r/1 | 4/1.5 4/0.5 6/1 8/1 | 11/1.5 10/0.5 8/2 | 9/1.5 8/0.5 #7/1 5/1 | 9/3 r/1 | 8/1.5 8/0.5 12/1 12/1 | 13/1.5 12/0.5 10/2 | 11/1.5 10/0.5 8/1 11/1 | 12/2 9/1 #7/1 | 8/1.5 10/0.5 12/2 | 13/2 10/2 | 11/2 8/2 | 9/2 #7/2", out var bars, out var notes);
			int num = (int)(64f * spb * 32000f);
			int frames = num + 96000;
			Mix m = new Mix(frames);
			Mix m2 = new Mix(frames);
			Mix m3 = new Mix(frames);
			int[] array = null;
			float[] array2 = new float[5] { 0f, 1.5f, 2f, 3f, 3.5f };
			float[] array3 = new float[5] { 1f, 0.6f, 0.85f, 0.5f, 0.7f };
			int[] array4 = new int[16]
			{
				0, 0, 2, 0, 1, 0, 2, 0, 0, 0,
				2, 0, 1, 0, 2, 1
			};
			bool[] array5 = new bool[16];
			int[] array6 = new int[6] { 0, 3, 6, 8, 11, 14 };
			foreach (int num2 in array6)
			{
				array5[num2] = true;
			}
			for (int j = 0; j < 16; j++)
			{
				Chord c = bars[j];
				float num3 = (float)(j * 4) * spb;
				int num4 = seed + j * 31;
				for (int l = 0; l < array2.Length; l++)
				{
					Drum(m, num3 + array2[l] * spb, 50f, 0.75f, 0.4f, 0.55f, 850f, array3[l], (l % 2 == 0) ? (-0.15f) : 0.15f, 0.35f, num4 + l);
				}
				Snare(m, num3 + spb, 0.5f, 0.2f, 0.3f, num4 + 7);
				Snare(m, num3 + 3f * spb, 0.55f, 0.2f, 0.3f, num4 + 8);
				if (rng.Chance(0.5f))
				{
					Snare(m, num3 + 3.75f * spb, 0.22f, 0.25f, 0.3f, num4 + 9);
				}
				if (j % 2 == 0)
				{
					Timpani(m, num3, k.Note(c.Root) + 12, 0.75f, 0f, 0.35f, num4 + 10);
				}
				if (j % 4 == 3)
				{
					for (int n = 0; n < 8; n++)
					{
						Drum(m, num3 + (2f + (float)n * 0.25f) * spb, 130f - (float)n * 9f, 0.5f, 0.15f, 0.6f, 1200f, 0.5f + (float)n * 0.05f, -0.5f + (float)n * 0.14f, 0.35f, num4 + 20 + n);
					}
				}
				if (j == 7 || j == 15)
				{
					Swell(m, num3 + 2f * spb, 2f * spb, 0.75f, crash: true, 0.4f, num4 + 40);
				}
				int num5 = ChordNote(k, c, 0, 1);
				int num6 = Above(ChordNote(k, c, 2, 1), num5);
				int[] array7 = new int[3]
				{
					num5,
					num5 + 12,
					num6
				};
				for (int num7 = 0; num7 < 16; num7++)
				{
					Strings(m2, num3 + (float)num7 * 0.25f * spb, 0.22f * spb, array7[array4[num7]], array5[num7] ? 0.95f : 0.62f, -0.3f, 0.006f, 0.04f, 2400f, 0.22f, num4 + num7);
				}
				array = Voice(k, c, array, k.Tonic + 38);
				for (int num8 = 1; num8 < array.Length; num8++)
				{
					Strings(m2, num3, 4f * spb, array[num8], 0.28f, -0.6f + (float)num8 * 0.4f, 0.2f, 0.3f, 1800f, 0.45f, num4 + 50 + num8);
				}
				for (int num9 = 0; num9 < 8; num9++)
				{
					Bass(m2, num3 + (float)num9 * 0.5f * spb, 0.4f * spb, ChordNote(k, c, 0, 0), (num9 % 2 == 0) ? 0.75f : 0.55f, 0.15f, bowed: false);
				}
				if (j < 8)
				{
					for (int num10 = 1; num10 < array.Length; num10++)
					{
						Brass(m3, num3, 1.2f * spb, array[num10], 0.7f, -0.35f + (float)num10 * 0.25f, 0.35f, num4 + 60 + num10);
						Brass(m3, num3 + 3.5f * spb, 0.35f * spb, array[num10], 0.55f, -0.35f + (float)num10 * 0.25f, 0.35f, num4 + 70 + num10);
					}
					continue;
				}
				for (int num11 = 1; num11 < array.Length; num11++)
				{
					Voices(m3, num3, 4f * spb, array[num11], 0.5f, -0.5f + (float)num11 * 0.35f, 0.55f, 0, 0.3f, num4 + 80 + num11);
				}
				for (int num12 = 0; num12 < 2; num12++)
				{
					Voices(m3, num3 + (float)num12 * 2f * spb, 0.5f * spb, ChordNote(k, c, 0, 3), 0.45f, 0.2f, 0.45f, 1, 0.03f, num4 + 90 + num12);
				}
			}
			int num13 = OctaveFor(k, 55);
			foreach (Tn item in notes)
			{
				int num14 = (int)(item.Beat / 4f);
				int num15 = k.Note(item.Degree) + item.Acc + 12 * num13;
				float num16 = ((num14 % 8 == 0 || num14 % 8 == 4) ? 0.85f : 0.78f) * (IsStrong(item.Beat % 4f, 4) ? 1f : 0.88f);
				Brass(m3, item.Beat * spb, item.Beats * spb * 0.95f, num15, num16, 0.1f, 0.4f, seed + num14 * 13 + num15);
				if (num14 >= 8)
				{
					Brass(m3, item.Beat * spb, item.Beats * spb * 0.95f, num15 + 12, num16 * 0.7f, -0.1f, 0.42f, seed + num14 * 17 + num15);
					Brass(m3, item.Beat * spb, item.Beats * spb * 0.95f, k.Note(item.Degree - 2) + 12 * num13 + 12, num16 * 0.45f, -0.25f, 0.42f, seed + num14 * 19 + num15);
				}
			}
			return new MusicPiece[3]
			{
				new MusicPiece
				{
					Name = "battle_drums",
					Loops = true,
					Beat = spb,
					Stereo = Finish(m, 0.7f, 0.45f, 0.5f, 0.12f, loop: true, num)
				},
				new MusicPiece
				{
					Name = "battle_strings",
					Loops = true,
					Beat = spb,
					Stereo = Finish(m2, 0.78f, 0.4f, 0.6f, 0.1f, loop: true, num)
				},
				new MusicPiece
				{
					Name = "battle_brass",
					Loops = true,
					Beat = spb,
					Stereo = Finish(m3, 0.82f, 0.4f, 0.75f, 0.1f, loop: true, num)
				}
			};
		}

		private static MusicPiece[] Hunt(int seed, out Key k, out float spb)
		{
			Rng rng = new Rng(seed * 29 + 3);
			k = new Key(rng.Pick(new int[4] { 62, 64, 57, 59 }) - 24, Dorian);
			spb = 60f / rng.Range(150f, 158f);
			BattleTheme("1 1 7 7 4 4 5^ 5^ 1 7 4 5^ 1 7 4 5^", "1/0.75 1/0.25 5/1 5/0.75 6/0.25 5/1 | 8/1.5 7/0.5 5/2 | 7/0.75 7/0.25 9/1 11/1.5 9/0.5 | 9/2 7/2 | 6/0.75 6/0.25 8/1 11/1.5 10/0.5 | 8/2 6/2 | 5/1 #7/1 9/1.5 #7/0.5 | 9/2 5/2 | 8/0.75 8/0.25 12/1 12/0.75 13/0.25 12/1 | 11/1.5 9/0.5 7/2 | 8/1 11/1 13/1.5 11/0.5 | 12/1.5 9/0.5 #7/2 | 8/0.75 8/0.25 10/1 12/2 | 11/1 9/1 7/1 9/1 | 8/2 6/2 | 5/1 #7/1 9/2", out var bars, out var notes);
			int num = (int)(64f * spb * 32000f);
			int frames = num + 96000;
			Mix m = new Mix(frames);
			Mix m2 = new Mix(frames);
			Mix m3 = new Mix(frames);
			int[] array = null;
			float[] array2 = new float[3] { 0f, 0.5f, 0.75f };
			for (int i = 0; i < 16; i++)
			{
				Chord c = bars[i];
				float num2 = (float)(i * 4) * spb;
				int num3 = seed + i * 37;
				for (int j = 0; j < 4 && (i % 4 != 3 || j < 2); j++)
				{
					for (int l = 0; l < 3; l++)
					{
						Drum(m, num2 + ((float)j + array2[l]) * spb, 150f, 0.45f, 0.1f, 0.6f, 1500f, (l == 0) ? 0.6f : 0.4f, 0.25f, 0.3f, num3 + j * 3 + l);
					}
				}
				Drum(m, num2, 48f, 0.75f, 0.45f, 0.5f, 800f, 1f, -0.1f, 0.35f, num3 + 20);
				Drum(m, num2 + 2f * spb, 48f, 0.75f, 0.4f, 0.5f, 800f, 0.85f, -0.1f, 0.35f, num3 + 21);
				Snare(m, num2 + spb, 0.35f, 0.2f, 0.3f, num3 + 22);
				Snare(m, num2 + 3f * spb, 0.4f, 0.2f, 0.3f, num3 + 23);
				if (i % 4 == 3)
				{
					for (int n = 0; n < 8; n++)
					{
						Drum(m, num2 + (2f + (float)n * 0.25f) * spb, 140f - (float)n * 10f, 0.5f, 0.14f, 0.6f, 1300f, 0.5f + (float)n * 0.05f, 0.5f - (float)n * 0.14f, 0.35f, num3 + 30 + n);
					}
				}
				if (i == 7 || i == 15)
				{
					Swell(m, num2 + 2f * spb, 2f * spb, 0.7f, crash: true, 0.4f, num3 + 50);
				}
				int num4 = ChordNote(k, c, 0, 1);
				int num5 = Above(ChordNote(k, c, 2, 1), num4);
				for (int num6 = 0; num6 < 4; num6++)
				{
					for (int num7 = 0; num7 < 3; num7++)
					{
						int num8 = ((num7 < 2) ? num4 : ((num6 % 2 == 0) ? num5 : (num4 + 12)));
						Strings(m2, num2 + ((float)num6 + array2[num7]) * spb, ((num7 == 0) ? 0.42f : 0.2f) * spb, num8, (num7 == 0) ? 0.9f : 0.65f, -0.3f, 0.006f, 0.04f, 2300f, 0.22f, num3 + num6 * 3 + num7);
					}
				}
				for (int num9 = 0; num9 < 4; num9++)
				{
					Bass(m2, num2 + (float)num9 * spb, 0.6f * spb, ChordNote(k, c, 0, 0), (num9 % 2 == 0) ? 0.8f : 0.6f, 0.15f, bowed: false);
				}
				array = Voice(k, c, array, k.Tonic + 38);
				for (int num10 = 1; num10 < array.Length; num10++)
				{
					Strings(m2, num2, 4f * spb, array[num10], 0.26f, -0.6f + (float)num10 * 0.4f, 0.2f, 0.3f, 1800f, 0.45f, num3 + 60 + num10);
				}
				if (i == 0 || bars[i].Root != bars[i - 1].Root)
				{
					for (int num11 = 1; num11 < array.Length; num11++)
					{
						Brass(m3, num2, 0.9f * spb, array[num11] - 12, 0.65f, -0.35f + (float)num11 * 0.25f, 0.35f, num3 + 70 + num11);
					}
				}
				if (i >= 8)
				{
					for (int num12 = 1; num12 < array.Length; num12++)
					{
						Voices(m3, num2, 4f * spb, array[num12], 0.48f, -0.5f + (float)num12 * 0.35f, 0.55f, 1, 0.3f, num3 + 80 + num12);
					}
				}
			}
			int num13 = OctaveFor(k, 55);
			foreach (Tn item in notes)
			{
				int num14 = (int)(item.Beat / 4f);
				int num15 = k.Note(item.Degree) + item.Acc + 12 * num13;
				float num16 = 0.82f * (IsStrong(item.Beat % 4f, 4) ? 1f : 0.88f);
				Brass(m3, item.Beat * spb, item.Beats * spb * 0.93f, num15, num16, 0.1f, 0.4f, seed + num14 * 13 + num15);
				if (num14 >= 8)
				{
					Brass(m3, item.Beat * spb, item.Beats * spb * 0.93f, num15 + 12, num16 * 0.65f, -0.12f, 0.42f, seed + num14 * 17 + num15);
				}
			}
			return new MusicPiece[3]
			{
				new MusicPiece
				{
					Name = "battle_drums",
					Loops = true,
					Beat = spb,
					Stereo = Finish(m, 0.7f, 0.45f, 0.5f, 0.12f, loop: true, num)
				},
				new MusicPiece
				{
					Name = "battle_strings",
					Loops = true,
					Beat = spb,
					Stereo = Finish(m2, 0.78f, 0.4f, 0.6f, 0.1f, loop: true, num)
				},
				new MusicPiece
				{
					Name = "battle_brass",
					Loops = true,
					Beat = spb,
					Stereo = Finish(m3, 0.82f, 0.4f, 0.75f, 0.1f, loop: true, num)
				}
			};
		}

		private static MusicPiece[] Endings(Key k, float spb, int seed)
		{
			float num = 0.06f;
			int num2 = k.Tonic + 24;
			Key key = new Key(num2, Aeolian);
			Key key2 = new Key(num2, Ionian);
			Mix m = new Mix(192000);
			Drum(m, num, 50f, 0.75f, 0.5f, 0.6f, 850f, 1f, 0f, 0.4f, seed + 1);
			Timpani(m, num, num2 - 12, 0.9f, 0f, 0.4f, seed + 2);
			Crash(m, num, 0.75f, 0.4f, seed + 3);
			int[] array = new int[4]
			{
				key.Note(0),
				key.Note(2),
				key.Note(4),
				key.Note(7)
			};
			for (int i = 0; i < array.Length; i++)
			{
				Brass(m, num, 1.4f, array[i], 0.85f, -0.35f + (float)i * 0.23f, 0.45f, seed + 10 + i);
				Strings(m, num, 1.8f, array[i] + 12, 0.65f, 0.35f - (float)i * 0.23f, 0.01f, 1.6f, 2200f, 0.5f, seed + 20 + i);
				Voices(m, num, 1.6f, array[i] + 12, 0.45f, -0.4f + (float)i * 0.27f, 0.6f, 0, 0.05f, seed + 30 + i);
			}
			Bass(m, num, 1.8f, num2 - 24, 0.85f, 0.3f, bowed: true);
			Mix m2 = new Mix(192000);
			Drum(m2, num, 50f, 0.6f, 0.5f, 0.3f, 700f, 0.45f, 0f, 0.4f, seed + 4);
			for (int j = 0; j < array.Length; j++)
			{
				Strings(m2, num, 2.2f, array[j], 0.5f, -0.4f + (float)j * 0.27f, 0.05f, 2f, 1500f, 0.6f, seed + 40 + j);
			}
			Bass(m2, num, 2.4f, num2 - 24, 0.6f, 0.3f, bowed: true);
			Mix m3 = new Mix(224000);
			int[] array2 = new int[4]
			{
				key2.Note(0),
				key2.Note(2),
				key2.Note(4),
				key2.Note(7)
			};
			Drum(m3, num, 50f, 0.75f, 0.5f, 0.6f, 850f, 1f, 0f, 0.4f, seed + 5);
			Crash(m3, num, 0.7f, 0.4f, seed + 6);
			for (int l = 0; l < array2.Length; l++)
			{
				Brass(m3, num, 0.8f * spb, array2[l], 0.8f, -0.35f + (float)l * 0.23f, 0.45f, seed + 50 + l);
			}
			int[] array3 = new int[5] { 4, 7, 9, 11, 14 };
			for (int n = 0; n < array3.Length; n++)
			{
				float startSec = num + (1f + (float)n / 3f) * spb;
				float durSec = ((n == array3.Length - 1) ? (3f * spb) : (spb / 3f * 0.9f));
				Brass(m3, startSec, durSec, key2.Note(array3[n] - 7), 0.85f, 0.1f, 0.45f, seed + 60 + n);
				Brass(m3, startSec, durSec, key2.Note(array3[n] - 9), 0.5f, -0.2f, 0.45f, seed + 70 + n);
			}
			float startSec2 = num + 2.3333335f * spb;
			for (int num3 = 0; num3 < array2.Length; num3++)
			{
				Strings(m3, startSec2, 3f * spb, array2[num3] + 12, 0.6f, 0.4f - (float)num3 * 0.27f, 0.08f, 1.4f, 2400f, 0.5f, seed + 80 + num3);
				Voices(m3, startSec2, 3f * spb, array2[num3] + 12, 0.42f, -0.4f + (float)num3 * 0.27f, 0.6f, 0, 0.15f, seed + 90 + num3);
			}
			TimpaniRoll(m3, num + spb, 1.3333334f * spb, num2 - 12, 0.3f, 0.75f, 0f, 0.4f, seed + 7);
			Timpani(m3, startSec2, num2 - 12, 0.9f, 0f, 0.4f, seed + 8);
			Crash(m3, startSec2, 0.6f, 0.45f, seed + 9);
			Bass(m3, startSec2, 3f * spb, num2 - 24, 0.8f, 0.3f, bowed: true);
			return new MusicPiece[3]
			{
				new MusicPiece
				{
					Name = "battle_end",
					Beat = spb,
					Stereo = Finish(m, 0.82f, 0.4f, 0.8f, 0.12f, loop: false, 0)
				},
				new MusicPiece
				{
					Name = "battle_soft",
					Beat = spb,
					Stereo = Finish(m2, 0.85f, 0.4f, 0.9f, 0.08f, loop: false, 0)
				},
				new MusicPiece
				{
					Name = "battle_win",
					Beat = spb,
					Stereo = Finish(m3, 0.82f, 0.4f, 0.8f, 0.12f, loop: false, 0)
				}
			};
		}

		public static MusicPiece[] BattleStyle(int style, int seed)
		{
			lock (PickLock)
			{
				_lastBattle = -1;
			}
			int i;
			for (i = seed; new Rng(i * 41 + 5).Int(3) != style; i++)
			{
			}
			return BattleAny(i);
		}

		private static float Hz(float midi)
		{
			return 440f * (float)Math.Pow(2.0, (double)(midi - 69f) / 12.0);
		}

		private static Chord Ch(int root, float beats, bool seventh = false, int sharpen = 0)
		{
			return new Chord
			{
				Root = root,
				Tones = ((!seventh) ? new int[3] { 0, 2, 4 } : new int[4] { 0, 2, 4, 6 }),
				Beats = beats,
				Sharpen = sharpen
			};
		}

		private static int ChordNote(Key k, Chord c, int tone, int octaveShift)
		{
			int num = k.Note(c.Root + c.Tones[tone]) + octaveShift * 12;
			if (c.Sharpen != 0 && c.Tones[tone] == 2)
			{
				num += c.Sharpen;
			}
			return num;
		}

		private static void Pan(float pan, out float gl, out float gr)
		{
			pan = Math.Max(-1f, Math.Min(1f, pan));
			gl = (float)Math.Sqrt(0.5f * (1f - pan));
			gr = (float)Math.Sqrt(0.5f * (1f + pan));
		}

		private static float Tail(int i, int len, int fade)
		{
			if (i >= len - fade)
			{
				return (float)(len - i) / (float)fade;
			}
			return 1f;
		}

		private static float[] BuildSine()
		{
			float[] array = new float[4097];
			for (int i = 0; i <= 4096; i++)
			{
				array[i] = (float)Math.Sin((double)i * 2.0 * Math.PI / 4096.0);
			}
			return array;
		}

		private static float Sin(double phase01)
		{
			double num = phase01 - Math.Floor(phase01);
			return Sine[(int)(num * 4096.0)];
		}

		private static float Saw(double phase, double inc)
		{
			double num = phase - Math.Floor(phase);
			double num2 = 2.0 * num - 1.0;
			if (num < inc)
			{
				double num3 = num / inc;
				num2 -= num3 + num3 - num3 * num3 - 1.0;
			}
			else if (num > 1.0 - inc)
			{
				double num4 = (num - 1.0) / inc;
				num2 -= num4 * num4 + num4 + num4 + 1.0;
			}
			return (float)num2;
		}

		private static void Pluck(Mix m, float startSec, float midi, float vel, float pan, float bright, float ring, float send, int seed)
		{
			float num = Hz(midi);
			int num2 = (int)(startSec * 32000f);
			float num3 = ring * (float)Math.Pow(220f / num, 0.3499999940395355);
			int num4 = Math.Min(m.Frames - num2, (int)(num3 * 5f * 32000f));
			if (num4 <= 0)
			{
				return;
			}
			Pan(pan, out var gl, out var gr);
			int num5 = Math.Max(1, Math.Min(10, (int)(12800f / num)));
			float[] array = new float[num5];
			float[] array2 = new float[num5];
			double[] array3 = new double[num5];
			double[] array4 = new double[num5];
			Rng rng = new Rng(seed);
			for (int i = 0; i < num5; i++)
			{
				int num6 = i + 1;
				array[i] = (float)Math.Pow(num6, -1.15 - (double)((1f - bright) * 0.9f)) * ((i == 0) ? 1f : 0.9f);
				float num7 = num3 / (1f + 0.55f * (float)i * (1.2f - bright * 0.6f));
				array2[i] = (float)Math.Exp(-1.0 / (double)(num7 * 32000f));
				array3[i] = num * (float)num6 * (1f + 0.0004f * (float)num6 * (float)num6) / 32000f;
				array4[i] = rng.Unit();
			}
			Svf svf = default(Svf);
			svf.Tune(num * 6f);
			int num8 = 640;
			int fade = Math.Min(num4 / 4, 1280);
			float num9 = vel * 0.32f;
			for (int j = 0; j < num4; j++)
			{
				float num10 = 0f;
				for (int k = 0; k < num5; k++)
				{
					array4[k] += array3[k];
					num10 += Sin(array4[k]) * array[k];
					array[k] *= array2[k];
				}
				if (j < num8)
				{
					num10 += svf.Low(rng.Unit() * 2f - 1f, 0.8f) * 0.25f * bright * (1f - (float)j / (float)num8);
				}
				float num11 = ((j < 40) ? ((float)j / 40f) : 1f);
				m.Put(num2 + j, num10 * num9 * num11 * Tail(j, num4, fade), gl, gr, send);
			}
		}

		private static void Strings(Mix m, float startSec, float durSec, float midi, float vel, float pan, float attack, float release, float cutoff, float send, int seed)
		{
			float num = Hz(midi);
			int num2 = (int)(startSec * 32000f);
			int num3 = Math.Min(m.Frames - num2, (int)((durSec + release) * 32000f));
			if (num3 > 0)
			{
				Pan(pan, out var gl, out var gr);
				Rng rng = new Rng(seed);
				double num4 = rng.Unit();
				double num5 = rng.Unit();
				double num6 = rng.Unit();
				double num7 = rng.Unit();
				float num8 = rng.Range(4.6f, 5.6f);
				Svf svf = default(Svf);
				Svf svf2 = default(Svf);
				svf.Tune(cutoff);
				svf2.Tune(cutoff * 1.4f);
				float num9 = vel * 0.22f;
				for (int i = 0; i < num3; i++)
				{
					float num10 = (float)i / 32000f;
					num7 += (double)(num8 / 32000f);
					float num11 = 1f + 0.0028f * Sin(num7) * Math.Min(1f, num10 / 0.6f);
					double num12 = num * num11 / 32000f;
					num4 += num12 * 0.9965;
					num5 += num12 * 1.0035;
					num6 += num12;
					float x = (Saw(num4, num12) + Saw(num5, num12) + Saw(num6, num12) * 0.8f) * 0.33f;
					x = svf2.Low(svf.Low(x, 1.2f), 1.3f);
					float num13 = ((num10 < attack) ? (num10 / attack) : ((num10 < durSec) ? 1f : Math.Max(0f, 1f - (num10 - durSec) / release)));
					num13 = num13 * num13 * (3f - 2f * num13);
					m.Put(num2 + i, x * num13 * num9, gl, gr, send);
				}
			}
		}

		private static void Flute(Mix m, float startSec, float durSec, float midi, float vel, float pan, float send, bool recorder, int seed)
		{
			float num = Hz(midi);
			int num2 = (int)(startSec * 32000f);
			int num3 = Math.Min(m.Frames - num2, (int)((durSec + 0.12f) * 32000f));
			if (num3 > 0)
			{
				Pan(pan, out var gl, out var gr);
				Rng rng = new Rng(seed);
				double num4 = 0.0;
				double num5 = 0.0;
				Svf svf = default(Svf);
				svf.Tune(num * 2f);
				float num6 = (recorder ? 0.35f : 0.18f);
				float num7 = (recorder ? 0.2f : 0.08f);
				float num8 = vel * 0.2f;
				for (int i = 0; i < num3; i++)
				{
					float num9 = (float)i / 32000f;
					num5 += 0.00016562500968575478;
					float num10 = 1f + 0.0045f * Sin(num5) * Math.Min(1f, Math.Max(0f, (num9 - 0.25f) / 0.4f));
					num4 += (double)(num * num10 / 32000f);
					float num11 = Sin(num4) + Sin(num4 * 2.0) * num6 + Sin(num4 * 3.0) * num7;
					float num12 = svf.Band(rng.Unit() * 2f - 1f, 0.9f) * 0.12f;
					float num13 = ((num9 < 0.05f) ? (num9 / 0.05f) : ((num9 < durSec) ? (1f - 0.12f * (num9 / Math.Max(0.3f, durSec))) : Math.Max(0f, 1f - (num9 - durSec) / 0.12f)));
					float num14 = ((num9 < 0.04f) ? ((1f - num9 / 0.04f) * 0.25f) : 0f);
					m.Put(num2 + i, (num11 * 0.85f + num12 + (rng.Unit() * 2f - 1f) * num14 * 0.3f) * num13 * num8, gl, gr, send);
				}
			}
		}

		private static void Bell(Mix m, float startSec, float midi, float vel, float pan, float ring, float send)
		{
			float num = Hz(midi);
			int num2 = (int)(startSec * 32000f);
			int num3 = Math.Min(m.Frames - num2, (int)(ring * 5f * 32000f));
			if (num3 <= 0)
			{
				return;
			}
			Pan(pan, out var gl, out var gr);
			float[] array = new float[5] { 1f, 2.01f, 3f, 4.18f, 5.43f };
			float[] array2 = new float[5] { 1f, 0.32f, 0.14f, 0.09f, 0.04f };
			float[] array3 = new float[5]
			{
				ring,
				ring * 0.45f,
				ring * 0.3f,
				ring * 0.16f,
				ring * 0.09f
			};
			float[] array4 = new float[5];
			double[] array5 = new double[5];
			for (int i = 0; i < 5; i++)
			{
				array4[i] = (float)Math.Exp(-1.0 / (double)(array3[i] * 32000f));
				if (array[i] * num > 13440f)
				{
					array2[i] = 0f;
				}
			}
			float num4 = vel * 0.2f;
			int fade = Math.Min(num3 / 4, 1280);
			for (int j = 0; j < num3; j++)
			{
				float num5 = 0f;
				for (int k = 0; k < 5; k++)
				{
					array5[k] += num * array[k] / 32000f;
					num5 += Sin(array5[k]) * array2[k];
					array2[k] *= array4[k];
				}
				float num6 = ((j < 24) ? ((float)j / 24f) : 1f);
				m.Put(num2 + j, num5 * num4 * num6 * Tail(j, num3, fade), gl, gr, send);
			}
		}

		private static void Bass(Mix m, float startSec, float durSec, float midi, float vel, float send, bool bowed)
		{
			float num = Hz(midi);
			int num2 = (int)(startSec * 32000f);
			float num3 = (bowed ? 0.35f : 0.12f);
			int num4 = Math.Min(m.Frames - num2, (int)((durSec + num3) * 32000f));
			if (num4 <= 0)
			{
				return;
			}
			Pan(0f, out var gl, out var gr);
			double num5 = 0.0;
			double num6 = 0.0;
			Svf svf = default(Svf);
			svf.Tune(bowed ? 520f : 1200f);
			float num7 = 1f;
			float num8 = (float)Math.Exp(-0.000390625);
			float num9 = 1f;
			float num10 = (float)Math.Exp(-0.00010416666666666667);
			float num11 = (bowed ? 0.12f : 0.008f);
			float num12 = vel * 0.3f;
			for (int i = 0; i < num4; i++)
			{
				float num13 = (float)i / 32000f;
				num6 += 0.0001500000071246177;
				double num14 = num * (1f + (bowed ? (0.002f * Sin(num6)) : 0f)) / 32000f;
				num5 += num14;
				if (!bowed && (i & 0xF) == 0)
				{
					svf.Tune(300f + 900f * num7);
				}
				num7 *= num8;
				float num15 = svf.Low(Saw(num5, num14), 1.1f) * 0.8f + Sin(num5) * 0.45f;
				float num16;
				if (num13 < num11)
				{
					num16 = num13 / num11;
				}
				else if (num13 < durSec)
				{
					num16 = (bowed ? 1f : (0.75f + 0.25f * num9));
					num9 *= num10;
				}
				else
				{
					num16 = Math.Max(0f, 1f - (num13 - durSec) / num3) * (bowed ? 1f : (0.75f + 0.25f * num9));
				}
				m.Put(num2 + i, num15 * num16 * num12, gl, gr, send);
			}
		}

		private static void Brass(Mix m, float startSec, float durSec, float midi, float vel, float pan, float send, int seed)
		{
			float num = Hz(midi);
			int num2 = (int)(startSec * 32000f);
			int num3 = Math.Min(m.Frames - num2, (int)((durSec + 0.18f) * 32000f));
			if (num3 <= 0)
			{
				return;
			}
			Pan(pan, out var gl, out var gr);
			Rng rng = new Rng(seed);
			double num4 = rng.Unit();
			double num5 = rng.Unit();
			Svf svf = default(Svf);
			float num6 = 1f;
			float num7 = (float)Math.Exp(-0.00026041666666666666);
			float num8 = 1f;
			float num9 = (float)Math.Exp(-0.00078125);
			float num10 = 1f;
			float num11 = (float)Math.Exp(-0.00020833333333333335);
			float num12 = vel * 0.24f;
			for (int i = 0; i < num3; i++)
			{
				float num13 = (float)i / 32000f;
				double num14 = num * (1f - 0.02f * num8) / 32000f;
				num8 *= num9;
				num4 += num14 * 0.998;
				num5 += num14 * 1.002;
				if ((i & 0xF) == 0)
				{
					svf.Tune(500f + 2600f * num6 * vel + 900f * vel);
				}
				num6 *= num7;
				float num15 = svf.Low((Saw(num4, num14) + Saw(num5, num14)) * 0.5f, 1f);
				float num16;
				if (num13 < 0.03f)
				{
					num16 = num13 / 0.03f;
				}
				else if (num13 < durSec)
				{
					num16 = 0.82f + 0.18f * num10;
					num10 *= num11;
				}
				else
				{
					num16 = Math.Max(0f, 1f - (num13 - durSec) / 0.18f) * (0.82f + 0.18f * num10);
				}
				m.Put(num2 + i, (float)Math.Tanh(num15 * 1.4f) * num16 * num12, gl, gr, send);
			}
		}

		private static void Choir(Mix m, float startSec, float durSec, float midi, float vel, float pan, float send, int seed)
		{
			float num = Hz(midi);
			int num2 = (int)(startSec * 32000f);
			int num3 = Math.Min(m.Frames - num2, (int)((durSec + 0.6f) * 32000f));
			if (num3 > 0)
			{
				Pan(pan, out var gl, out var gr);
				Rng rng = new Rng(seed);
				double num4 = rng.Unit();
				double num5 = rng.Unit();
				double num6 = rng.Unit();
				double num7 = rng.Unit();
				Svf svf = default(Svf);
				Svf svf2 = default(Svf);
				Svf svf3 = default(Svf);
				svf.Tune(760f);
				svf2.Tune(1180f);
				svf3.Tune(2700f);
				float num8 = vel * 0.5f;
				for (int i = 0; i < num3; i++)
				{
					float num9 = (float)i / 32000f;
					num7 += 0.00015937499119900167;
					float num10 = 1f + 0.004f * Sin(num7);
					double num11 = num * num10 / 32000f;
					num4 += num11 * 0.995;
					num5 += num11 * 1.005;
					num6 += num11 * 1.0012;
					float x = (Saw(num4, num11) + Saw(num5, num11) + Saw(num6, num11)) * 0.33f;
					float num12 = svf.Band(x, 0.35f) + svf2.Band(x, 0.4f) * 0.55f + svf3.Band(x, 0.5f) * 0.25f;
					float num13 = ((num9 < 0.45f) ? (num9 / 0.45f) : ((num9 < durSec) ? 1f : Math.Max(0f, 1f - (num9 - durSec) / 0.6f)));
					num13 = num13 * num13 * (3f - 2f * num13);
					m.Put(num2 + i, num12 * num13 * num8, gl, gr, send);
				}
			}
		}

		private static void Drum(Mix m, float startSec, float hz, float drop, float decay, float slap, float slapHz, float vel, float pan, float send, int seed)
		{
			int num = (int)(startSec * 32000f);
			int num2 = Math.Min(m.Frames - num, (int)(decay * 6f * 32000f));
			if (num2 > 0)
			{
				Pan(pan, out var gl, out var gr);
				Rng rng = new Rng(seed);
				double num3 = 0.0;
				Svf svf = default(Svf);
				svf.Tune(slapHz);
				float num4 = 1f;
				float num5 = (float)Math.Exp(-1.0 / (double)(decay * 32000f));
				float num6 = 1f;
				float num7 = (float)Math.Exp(-1.0 / 800.0);
				float num8 = 1f;
				float num9 = (float)Math.Exp(-1.0 / 960.0);
				float num10 = vel * 0.5f;
				int fade = Math.Min(num2 / 4, 1280);
				for (int i = 0; i < num2; i++)
				{
					num3 += (double)(hz * (1f + drop * num8) / 32000f);
					num8 *= num9;
					float num11 = Sin(num3) * num4 + svf.Band(rng.Unit() * 2f - 1f, 0.7f) * num6 * slap;
					num4 *= num5;
					num6 *= num7;
					float num12 = ((i < 8) ? ((float)i / 8f) : 1f);
					m.Put(num + i, (float)Math.Tanh(num11 * 1.3f) * num10 * num12 * Tail(i, num2, fade), gl, gr, send);
				}
			}
		}

		private static void Snare(Mix m, float startSec, float vel, float pan, float send, int seed)
		{
			int num = (int)(startSec * 32000f);
			int num2 = Math.Min(m.Frames - num, 11200);
			if (num2 > 0)
			{
				Pan(pan, out var gl, out var gr);
				Rng rng = new Rng(seed);
				double num3 = 0.0;
				Svf svf = default(Svf);
				svf.Tune(1600f);
				float num4 = 1f;
				float num5 = (float)Math.Exp(-0.00034722222222222224);
				float num6 = 1f;
				float num7 = (float)Math.Exp(-0.000625);
				float num8 = vel * 0.4f;
				int fade = Math.Min(num2 / 4, 1280);
				for (int i = 0; i < num2; i++)
				{
					num3 += 0.005937499925494194;
					float num9 = rng.Unit() * 2f - 1f;
					float num10 = (num9 - svf.Low(num9, 0.8f)) * num4 * 0.7f + Sin(num3) * num6 * 0.6f;
					num4 *= num5;
					num6 *= num7;
					m.Put(num + i, num10 * num8 * Tail(i, num2, fade), gl, gr, send);
				}
			}
		}

		private static void Swell(Mix m, float startSec, float durSec, float vel, bool crash, float send, int seed)
		{
			int num = (int)(startSec * 32000f);
			int num2 = Math.Min(m.Frames - num, (int)((durSec + (crash ? 2.5f : 0.05f)) * 32000f));
			if (num2 <= 0)
			{
				return;
			}
			Rng rng = new Rng(seed);
			Rng rng2 = new Rng(seed + 7);
			Svf svf = default(Svf);
			Svf svf2 = default(Svf);
			svf.Tune(4500f);
			svf2.Tune(4500f);
			float num3 = 1f;
			float num4 = (float)Math.Exp(-3.90625E-05);
			float num5 = vel * 0.18f;
			int fade = Math.Min(num2 / 4, 16000);
			for (int i = 0; i < num2; i++)
			{
				float num6 = (float)i / 32000f;
				float num8;
				if (num6 < durSec)
				{
					float num7 = num6 / durSec;
					num8 = num7 * num7 * (float)Math.Sqrt(num7);
				}
				else if (crash)
				{
					num8 = num3 * Tail(i, num2, fade);
					num3 *= num4;
				}
				else
				{
					num8 = Math.Max(0f, 1f - (num6 - durSec) / 0.05f);
				}
				float num9 = rng.Unit() * 2f - 1f;
				float num10 = rng2.Unit() * 2f - 1f;
				float l = (num9 - svf.Low(num9, 0.7f)) * num8 * num5;
				float r = (num10 - svf2.Low(num10, 0.7f)) * num8 * num5;
				m.PutLR(num + i, l, r, send);
			}
		}

		private static float[] Finish(Mix m, float room, float damp, float wet, float targetRms, bool loop, int loopFrames)
		{
			int frames = m.Frames;
			float[] array = new float[frames];
			float[] array2 = new float[frames];
			int[] array3 = new int[8] { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
			int[] array4 = new int[4] { 556, 441, 341, 225 };
			float num = 0.7256236f;
			for (int i = 0; i < 2; i++)
			{
				float[] array5 = ((i == 0) ? m.SendL : m.SendR);
				float[] array6 = ((i == 0) ? array : array2);
				int num2 = ((i != 0) ? 23 : 0);
				int[] array7 = array3;
				for (int j = 0; j < array7.Length; j++)
				{
					int num3 = (int)((float)(array7[j] + num2) * num);
					float[] array8 = new float[num3];
					int num4 = 0;
					float num5 = 0f;
					for (int k = 0; k < frames; k++)
					{
						float num6 = array8[num4];
						num5 = num6 * (1f - damp) + num5 * damp;
						array8[num4] = array5[k] * 0.015f + num5 * room;
						num4 = ((num4 + 1 != num3) ? (num4 + 1) : 0);
						array6[k] += num6;
					}
				}
				array7 = array4;
				for (int j = 0; j < array7.Length; j++)
				{
					int num7 = (int)((float)(array7[j] + num2) * num);
					float[] array9 = new float[num7];
					int num8 = 0;
					for (int l = 0; l < frames; l++)
					{
						float num9 = array9[num8];
						float num10 = array6[l];
						array9[num8] = num10 + num9 * 0.5f;
						array6[l] = num9 - num10;
						num8 = ((num8 + 1 != num7) ? (num8 + 1) : 0);
					}
				}
			}
			float[] array10 = new float[frames * 2];
			for (int n = 0; n < frames; n++)
			{
				array10[n * 2] = m.L[n] + array[n] * wet;
				array10[n * 2 + 1] = m.R[n] + array2[n] * wet;
			}
			if (loop && loopFrames < frames)
			{
				for (int num11 = loopFrames; num11 < frames; num11++)
				{
					int num12 = (num11 - loopFrames) % loopFrames;
					array10[num12 * 2] += array10[num11 * 2];
					array10[num12 * 2 + 1] += array10[num11 * 2 + 1];
				}
				Array.Resize(ref array10, loopFrames * 2);
			}
			double num13 = 0.0;
			for (int num14 = 0; num14 < array10.Length; num14++)
			{
				num13 += (double)(array10[num14] * array10[num14]);
			}
			float num15 = (float)Math.Sqrt(num13 / (double)Math.Max(1, array10.Length));
			float num16 = ((num15 > 1E-06f) ? (targetRms / num15) : 1f);
			for (int num17 = 0; num17 < array10.Length; num17++)
			{
				float num18 = array10[num17] * num16;
				array10[num17] = ((Math.Abs(num18) > 0.6f) ? ((float)Math.Sign(num18) * (0.6f + 0.35f * (float)Math.Tanh((Math.Abs(num18) - 0.6f) / 0.35f))) : num18);
			}
			if (!loop)
			{
				int num19 = Math.Min(frames / 4, 1600);
				for (int num20 = 0; num20 < num19; num20++)
				{
					float num21 = (float)num20 / (float)num19;
					array10[num20 * 2] *= num21;
					array10[num20 * 2 + 1] *= num21;
				}
				int num22 = Math.Min(frames / 3, 64000);
				for (int num23 = 0; num23 < num22; num23++)
				{
					int num24 = frames - 1 - num23;
					float num25 = (float)num23 / (float)num22;
					array10[num24 * 2] *= num25;
					array10[num24 * 2 + 1] *= num25;
				}
			}
			return array10;
		}

		private static List<Note> Melody(Key k, Chord[] bars, float beatsPerBar, int lo, int hi, Rng rng, float density = 1f)
		{
			List<Note> list = new List<Note>();
			float[][] a = ((beatsPerBar == 3f) ? Rhythms34 : Rhythms44);
			float[] array = rng.Pick(a);
			float[] array2 = rng.Pick(a);
			int[] array3 = new int[6] { 0, 1, 2, 1, 0, -1 };
			int[] array4 = new int[6] { 0, -1, -2, 0, 1, 2 };
			int num = k.Degree((lo + hi) / 2);
			float num2 = 0f;
			for (int i = 0; i < bars.Length; i++)
			{
				Chord c = bars[i];
				int num3 = i % 4;
				bool flag = num3 == 3;
				float[] array5 = ((!flag) ? ((num3 == 1) ? array2 : array) : ((beatsPerBar != 3f) ? ((!rng.Chance(0.5f)) ? new float[1] { 4f } : new float[2] { 1f, 3f }) : new float[1] { 3f }));
				if (density < 1f && !flag && rng.Chance(1f - density))
				{
					array5 = ((beatsPerBar != 3f) ? new float[2] { 2f, 2f } : new float[1] { 3f });
				}
				int[] array6 = ((i / 4 % 2 == 0) ? array3 : array4);
				float num4 = 0f;
				for (int j = 0; j < array5.Length; j++)
				{
					float num5 = array5[j];
					if (num4 + num5 > beatsPerBar + 0.01f)
					{
						num5 = beatsPerBar - num4;
					}
					if (num5 <= 0.01f)
					{
						break;
					}
					bool flag2 = Math.Abs((double)num4 - Math.Round(num4)) < 0.009999999776482582 && (int)Math.Round(num4) % 2 == 0;
					int l;
					if (flag && j == array5.Length - 1)
					{
						int degreeMod = c.Root + ((i != bars.Length - 1) ? (rng.Chance(0.3f) ? 2 : 0) : 0);
						l = NearestDegreeOf(k, degreeMod, num);
					}
					else
					{
						int num6 = array6[j % array6.Length] - ((j > 0) ? array6[(j - 1) % array6.Length] : 0);
						if (num3 == 2 && rng.Chance(0.25f))
						{
							num6 += (rng.Chance(0.5f) ? 2 : (-2));
						}
						l = num + Math.Sign(num6) * Math.Min(2, (Math.Abs(num6) != 0) ? Math.Abs(num6) : 0);
						if (flag2)
						{
							l = NearestChordDegree(k, c, l);
						}
					}
					while (k.Note(l) > hi)
					{
						l -= 2;
					}
					for (; k.Note(l) < lo; l += 2)
					{
					}
					num = l;
					int num7 = k.Note(num);
					if (c.Sharpen != 0 && ((num - c.Root) % 7 + 7) % 7 == 2)
					{
						num7 += c.Sharpen;
					}
					list.Add(new Note
					{
						Beat = num2 + num4,
						Beats = num5 * 0.95f,
						Midi = num7,
						Vel = (flag2 ? 0.95f : 0.8f)
					});
					num4 += num5;
				}
				num2 += beatsPerBar;
			}
			return list;
		}

		private static int NearestDegreeOf(Key k, int degreeMod7, int near)
		{
			int result = near;
			int num = int.MaxValue;
			for (int i = near - 7; i <= near + 7; i++)
			{
				if (((i - degreeMod7) % 7 + 7) % 7 == 0 && Math.Abs(i - near) < num)
				{
					num = Math.Abs(i - near);
					result = i;
				}
			}
			return result;
		}

		private static int NearestChordDegree(Key k, Chord c, int near)
		{
			int result = near;
			int num = int.MaxValue;
			for (int i = near - 4; i <= near + 4; i++)
			{
				int num2 = ((i - c.Root) % 7 + 7) % 7;
				bool flag = false;
				int[] tones = c.Tones;
				for (int j = 0; j < tones.Length; j++)
				{
					if (tones[j] % 7 == num2)
					{
						flag = true;
					}
				}
				if (flag && Math.Abs(i - near) < num)
				{
					num = Math.Abs(i - near);
					result = i;
				}
			}
			return result;
		}

		private static int[] Voice(Key k, Chord c, int[] previous, int centre)
		{
			List<int> list = new List<int>();
			for (int i = -2; i <= 2; i++)
			{
				for (int j = 0; j < c.Tones.Length; j++)
				{
					list.Add(ChordNote(k, c, j, i));
				}
			}
			int num = ((previous != null) ? previous.Length : 4);
			int[] array = new int[num];
			if (previous == null)
			{
				for (int l = 0; l < num; l++)
				{
					array[l] = Nearest(list, centre - 7 + l * 5);
				}
			}
			else
			{
				for (int m = 0; m < num; m++)
				{
					array[m] = Nearest(list, previous[m]);
				}
			}
			array[0] = ChordNote(k, c, 0, 0);
			while (array[0] > centre - 5)
			{
				array[0] -= 12;
			}
			while (array[0] < centre - 17)
			{
				array[0] += 12;
			}
			Array.Sort(array);
			return array;
		}

		private static int Nearest(List<int> pool, int target)
		{
			int num = pool[0];
			foreach (int item in pool)
			{
				if (Math.Abs(item - target) < Math.Abs(num - target))
				{
					num = item;
				}
			}
			return num;
		}

		private static Chord[] Progression(int[][] book, int bars, Rng rng, bool seventhSometimes, int[] mode)
		{
			int[] array = rng.Pick(book);
			int[] array2 = rng.Pick(book);
			Chord[] array3 = new Chord[bars];
			for (int i = 0; i < bars; i++)
			{
				int num = ((i / 4 % 4 == 2) ? array2 : array)[i % 4];
				int sharpen = ((mode == Aeolian && num == 4 && i % 4 == 3) ? 1 : 0);
				array3[i] = Ch(num, 4f, seventhSometimes && rng.Chance(0.3f), sharpen);
			}
			array3[bars - 1] = Ch(0, 4f);
			return array3;
		}

		public static MusicPiece Day(int seed)
		{
			Rng rng = new Rng(seed * 7 + 1);
			int[] a = new int[5] { 62, 67, 60, 65, 64 };
			int[] mode = (rng.Chance(0.25f) ? Lydian : (rng.Chance(0.3f) ? Mixolydian : Ionian));
			Key key = new Key(rng.Pick(a) - 12, mode);
			float num = rng.Range(74f, 86f);
			float num2 = 60f / num;
			int num3 = 26;
			Chord[] array = Progression(BrightBook, num3, rng, seventhSometimes: true, mode);
			Mix m = new Mix((int)(((float)(num3 * 4) * num2 + 5f) * 32000f));
			int[] array2 = null;
			for (int i = 0; i < num3; i++)
			{
				Chord c = array[i];
				float num4 = (float)(i * 4) * num2;
				array2 = Voice(key, c, array2, key.Tonic + 19);
				float num5 = ((i < 2) ? (0.55f + 0.2f * (float)i) : ((i >= num3 - 2) ? 0.7f : 0.85f));
				for (int j = 0; j < array2.Length; j++)
				{
					Strings(m, num4, 4f * num2, array2[j], 0.55f * num5, -0.5f + (float)j * 0.33f, (i == 0) ? 1.8f : 0.5f, 0.9f, 1700f, 0.5f, seed + i * 13 + j);
				}
				if (i >= 1)
				{
					Bass(m, num4, 4f * num2, ChordNote(key, c, 0, -1), 0.7f, 0.25f, bowed: true);
				}
				if (i >= 2 && i < num3 - 1)
				{
					int[] array3 = new int[8] { 0, 1, 2, 3, 4, 3, 2, 1 };
					int[] array4 = new int[5]
					{
						ChordNote(key, c, 0, 0),
						ChordNote(key, c, 2 % c.Tones.Length, 0),
						ChordNote(key, c, 0, 1),
						ChordNote(key, c, 1, 1),
						ChordNote(key, c, 2 % c.Tones.Length, 1)
					};
					for (int k = 0; k < 8 && (i % 4 != 3 || k < 6); k++)
					{
						Pluck(m, num4 + (float)k * 0.5f * num2, array4[array3[k]] + 12, (k == 0) ? 0.75f : 0.55f, 0.35f, 0.55f, 1.6f, 0.45f, seed * 31 + i * 8 + k);
					}
				}
			}
			foreach (Note item in Melody(key, array, 4f, key.Tonic + 24, key.Tonic + 38, rng, 0.85f))
			{
				int num6 = (int)(item.Beat / 4f);
				if (num6 >= 4 && num6 < num3 - 1)
				{
					float startSec = item.Beat * num2;
					if (num6 >= 12 && num6 < 18)
					{
						Strings(m, startSec, item.Beats * num2, item.Midi, 0.6f * item.Vel, 0.15f, 0.12f, 0.35f, 2600f, 0.45f, seed + num6 * 7 + item.Midi);
					}
					else
					{
						Flute(m, startSec, item.Beats * num2, item.Midi, 0.75f * item.Vel, -0.12f, 0.4f, recorder: false, seed + num6);
					}
				}
			}
			for (int l = 7; l < num3; l += 8)
			{
				Bell(m, (float)(l * 4 + 2) * num2, ChordNote(key, array[l], 0, 3), 0.3f, 0.5f, 1.4f, 0.6f);
			}
			return new MusicPiece
			{
				Name = "day",
				Stereo = Finish(m, 0.84f, 0.35f, 0.9f, 0.11f, loop: false, 0)
			};
		}

		public static MusicPiece Night(int seed)
		{
			Rng rng = new Rng(seed * 11 + 3);
			int[] a = new int[4] { 57, 62, 64, 59 };
			int[] mode = (rng.Chance(0.5f) ? Dorian : Aeolian);
			Key key = new Key(rng.Pick(a) - 12, mode);
			float num = rng.Range(58f, 66f);
			float num2 = 60f / num;
			int num3 = 20;
			Chord[] array = Progression(DarkBook, num3, rng, seventhSometimes: true, mode);
			Mix m = new Mix((int)(((float)(num3 * 4) * num2 + 6f) * 32000f));
			int[] array2 = null;
			for (int i = 0; i < num3; i++)
			{
				Chord c = array[i];
				float num4 = (float)(i * 4) * num2;
				array2 = Voice(key, c, array2, key.Tonic + 17);
				for (int j = 0; j < array2.Length; j++)
				{
					Strings(m, num4, 4f * num2, array2[j], 0.42f, -0.45f + (float)j * 0.3f, 1.6f, 1.4f, 1100f, 0.6f, seed + i * 17 + j);
				}
				Bass(m, num4, 4f * num2, ChordNote(key, c, 0, -1), 0.45f, 0.35f, bowed: true);
				if (i >= 1 && i < num3 - 1)
				{
					for (int k = 0; k < 4; k++)
					{
						int tone = k % c.Tones.Length;
						Pluck(m, num4 + (float)k * num2, ChordNote(key, c, tone, 1) + ((k == 3) ? 12 : 0), 0.5f, -0.3f + (float)k * 0.2f, 0.4f, 2.2f, 0.6f, seed * 3 + i * 4 + k);
					}
				}
			}
			foreach (Note item in Melody(key, array, 4f, key.Tonic + 31, key.Tonic + 45, rng, 0.55f))
			{
				int num5 = (int)(item.Beat / 4f);
				if (num5 >= 4 && num5 < num3 - 1 && (!(item.Beats < 0.9f) || !rng.Chance(0.4f)))
				{
					Bell(m, item.Beat * num2, item.Midi, 0.55f * item.Vel, 0.25f, 1.6f, 0.65f);
				}
			}
			return new MusicPiece
			{
				Name = "night",
				Stereo = Finish(m, 0.88f, 0.4f, 1.1f, 0.085f, loop: false, 0)
			};
		}

		public static MusicPiece Town(int seed)
		{
			Rng rng = new Rng(seed * 13 + 5);
			int[] a = new int[4] { 62, 67, 69, 64 };
			int[] mode = (rng.Chance(0.5f) ? Dorian : Mixolydian);
			Key key = new Key(rng.Pick(a) - 12, mode);
			float num = rng.Range(132f, 150f);
			float num2 = 60f / num;
			int num3 = 32;
			Chord[] array = new Chord[num3];
			int[][] a2 = new int[4][]
			{
				new int[4] { 0, 6, 0, 4 },
				new int[4] { 0, 3, 6, 0 },
				new int[4] { 0, 6, 3, 4 },
				new int[4] { 0, 4, 6, 0 }
			};
			int[] array2 = rng.Pick(a2);
			int[] array3 = rng.Pick(a2);
			for (int i = 0; i < num3; i++)
			{
				array[i] = Ch(((i / 8 % 2 == 1) ? array3 : array2)[i / 2 % 4], 3f);
			}
			array[num3 - 1] = Ch(0, 3f);
			Mix m = new Mix((int)(((float)(num3 * 3) * num2 + 4f) * 32000f));
			for (int j = 0; j < num3; j++)
			{
				Chord c = array[j];
				float num4 = (float)(j * 3) * num2;
				if (j % 4 == 0)
				{
					Bass(m, num4, 12f * num2, key.Tonic - 12, 0.35f, 0.2f, bowed: true);
					Bass(m, num4, 12f * num2, key.Tonic - 5, 0.18f, 0.2f, bowed: true);
				}
				Pluck(m, num4, ChordNote(key, c, 0, 0), 0.75f, -0.25f, 0.75f, 0.9f, 0.3f, seed + j * 5);
				for (int k = 1; k < 3; k++)
				{
					for (int l = 0; l < 3; l++)
					{
						Pluck(m, num4 + (float)k * num2 + (float)l * 0.012f, ChordNote(key, c, l, 1), 0.45f, -0.3f + (float)l * 0.05f, 0.8f, 0.6f, 0.3f, seed + j * 9 + k * 3 + l);
					}
				}
				Drum(m, num4, 120f, 0.4f, 0.16f, 0.3f, 1400f, 0.55f, 0.2f, 0.25f, seed + j);
				Drum(m, num4 + 2f * num2, 170f, 0.3f, 0.08f, 0.9f, 2200f, 0.35f, 0.25f, 0.25f, seed + j * 3);
				if (j % 8 == 7)
				{
					for (int n = 0; n < 3; n++)
					{
						Drum(m, num4 + (2f + (float)n / 3f) * num2, 160f, 0.3f, 0.06f, 1f, 2400f, 0.25f, 0.25f, 0.25f, seed + j * 7 + n);
					}
				}
			}
			foreach (Note item in Melody(key, array, 3f, key.Tonic + 24, key.Tonic + 40, rng, 0.95f))
			{
				int num5 = (int)(item.Beat / 3f);
				if (num5 >= 4 && num5 < num3 - 1)
				{
					Flute(m, item.Beat * num2, item.Beats * num2, item.Midi, 0.7f * item.Vel, 0.2f, 0.3f, recorder: true, seed + num5 * 3);
				}
			}
			return new MusicPiece
			{
				Name = "town",
				Stereo = Finish(m, 0.78f, 0.45f, 0.7f, 0.1f, loop: false, 0)
			};
		}

		public static MusicPiece[] Battle(int seed)
		{
			Rng rng = new Rng(seed * 17 + 9);
			int[] a = new int[4] { 62, 64, 57, 60 };
			int num = rng.Pick(a) - 24;
			Key key = new Key(num, Aeolian);
			float num2 = rng.Range(136f, 146f);
			float num3 = 60f / num2;
			int[][] a2 = new int[4][]
			{
				new int[4] { 0, 5, 6, 0 },
				new int[4] { 0, 5, 3, 4 },
				new int[4] { 0, 6, 5, 4 },
				new int[4] { 0, 3, 5, 6 }
			};
			int[] array = rng.Pick(a2);
			int[] array2 = rng.Pick(a2);
			Chord[] array3 = new Chord[16];
			for (int i = 0; i < 16; i++)
			{
				int num4 = ((i / 4 == 2) ? array2 : array)[i % 4];
				array3[i] = Ch(num4, 4f, seventh: false, (num4 == 4) ? 1 : 0);
			}
			int num5 = (int)(64f * num3 * 32000f);
			int frames = num5 + 96000;
			Mix m = new Mix(frames);
			Mix m2 = new Mix(frames);
			Mix m3 = new Mix(frames);
			int[] array4 = null;
			for (int j = 0; j < 16; j++)
			{
				Chord c = array3[j];
				float num6 = (float)(j * 4) * num3;
				Drum(m, num6, 52f, 0.7f, 0.42f, 0.5f, 900f, 1f, -0.1f, 0.35f, seed + j * 11);
				Drum(m, num6 + 1.5f * num3, 52f, 0.7f, 0.3f, 0.4f, 900f, 0.55f, 0.1f, 0.35f, seed + j * 11 + 1);
				Drum(m, num6 + 2f * num3, 52f, 0.7f, 0.42f, 0.5f, 900f, 0.9f, -0.1f, 0.35f, seed + j * 11 + 2);
				if (j % 2 == 1)
				{
					Drum(m, num6 + 3.5f * num3, 52f, 0.7f, 0.3f, 0.4f, 900f, 0.6f, 0.1f, 0.35f, seed + j * 11 + 3);
				}
				Snare(m, num6 + num3, 0.55f, 0.2f, 0.3f, seed + j * 3);
				Snare(m, num6 + 3f * num3, 0.6f, 0.2f, 0.3f, seed + j * 3 + 1);
				if (rng.Chance(0.4f))
				{
					Snare(m, num6 + 3.75f * num3, 0.25f, 0.25f, 0.3f, seed + j * 3 + 2);
				}
				if (j % 4 == 3)
				{
					for (int k = 0; k < 4; k++)
					{
						Drum(m, num6 + (2f + (float)k * 0.5f) * num3, 110f - (float)k * 14f, 0.5f, 0.2f, 0.6f, 1100f, 0.6f + (float)k * 0.08f, -0.4f + (float)k * 0.25f, 0.35f, seed + j * 17 + k);
					}
				}
				if (j == 14)
				{
					Swell(m, num6, 8f * num3, 0.8f, crash: true, 0.4f, seed + 99);
				}
				int num7 = ChordNote(key, c, 0, 1);
				int num8 = ChordNote(key, c, 2, 1);
				int[] array5 = new int[8]
				{
					num7,
					num7,
					num8,
					num7,
					num7 + 12,
					num7,
					num8,
					num7
				};
				for (int l = 0; l < 8; l++)
				{
					bool flag = l == 0 || l == 3 || l == 6;
					Strings(m2, num6 + (float)l * 0.5f * num3, 0.42f * num3, array5[l], flag ? 0.95f : 0.7f, -0.3f, 0.012f, 0.06f, 2200f, 0.25f, seed + j * 31 + l);
				}
				array4 = Voice(key, c, array4, num + 31);
				for (int n = 1; n < array4.Length; n++)
				{
					Strings(m2, num6, 4f * num3, array4[n], 0.32f, -0.6f + (float)n * 0.4f, 0.25f, 0.3f, 1500f, 0.45f, seed + j * 5 + n);
				}
				Bass(m2, num6, 2f * num3, ChordNote(key, c, 0, 0), 0.8f, 0.2f, bowed: false);
				Bass(m2, num6 + 2.5f * num3, 1.5f * num3, ChordNote(key, c, 0, 0), 0.7f, 0.2f, bowed: false);
				if (j < 8)
				{
					for (int num9 = 1; num9 < array4.Length; num9++)
					{
						Brass(m3, num6, 1.4f * num3, array4[num9] + 12, 0.75f, -0.3f + (float)num9 * 0.25f, 0.35f, seed + j * 7 + num9);
						Brass(m3, num6 + 3.5f * num3, 0.4f * num3, array4[num9] + 12, 0.55f, -0.3f + (float)num9 * 0.25f, 0.35f, seed + j * 7 + num9 + 50);
					}
				}
				else
				{
					for (int num10 = 1; num10 < array4.Length; num10++)
					{
						Choir(m3, num6, 4f * num3, array4[num10] + 12, 0.5f, -0.5f + (float)num10 * 0.35f, 0.6f, seed + j * 3 + num10);
					}
				}
			}
			foreach (Note item in Melody(key, array3, 4f, num + 36, num + 50, rng))
			{
				int num11 = (int)(item.Beat / 4f);
				if (num11 >= 8)
				{
					Brass(m3, item.Beat * num3, item.Beats * num3, item.Midi, 0.85f * item.Vel, 0.15f, 0.4f, seed + num11 * 13 + item.Midi);
					int num12 = key.Note(key.Degree(item.Midi) - 2);
					Brass(m3, item.Beat * num3, item.Beats * num3, num12, 0.5f * item.Vel, -0.15f, 0.4f, seed + num11 * 19 + item.Midi);
				}
			}
			return new MusicPiece[3]
			{
				new MusicPiece
				{
					Name = "battle_drums",
					Loops = true,
					Beat = num3,
					Stereo = Finish(m, 0.7f, 0.45f, 0.5f, 0.12f, loop: true, num5)
				},
				new MusicPiece
				{
					Name = "battle_strings",
					Loops = true,
					Beat = num3,
					Stereo = Finish(m2, 0.78f, 0.4f, 0.6f, 0.1f, loop: true, num5)
				},
				new MusicPiece
				{
					Name = "battle_brass",
					Loops = true,
					Beat = num3,
					Stereo = Finish(m3, 0.82f, 0.4f, 0.75f, 0.1f, loop: true, num5)
				}
			};
		}

		public static MusicPiece Victory(int seed)
		{
			Key key = new Key(new Rng(seed * 5 + 77).Pick(new int[3] { 60, 62, 65 }) - 12, Ionian);
			float num = 15f / 28f;
			Mix m = new Mix(224000);
			int[] array = new int[5] { 4, 7, 9, 11, 14 };
			float[] array2 = new float[5] { 0f, 0.5f, 1f, 1.5f, 2f };
			float[] array3 = new float[5] { 0.45f, 0.45f, 0.45f, 0.45f, 3f };
			for (int i = 0; i < array.Length; i++)
			{
				int num2 = key.Note(array[i]) + 12;
				Brass(m, array2[i] * num, array3[i] * num, num2, 0.85f, 0.1f, 0.45f, seed + i);
				Brass(m, array2[i] * num, array3[i] * num, key.Note(array[i] - 2) + 12, 0.5f, -0.2f, 0.45f, seed + i + 9);
			}
			for (int j = 0; j < 3; j++)
			{
				Strings(m, 2f * num, 3f * num, key.Note(j * 2 + 7), 0.5f, -0.4f + (float)j * 0.4f, 0.1f, 1f, 2000f, 0.5f, seed + j);
			}
			for (int k = 0; k < 12; k++)
			{
				Drum(m, (float)k * 0.16f * num, 90f, 0.4f, 0.12f, 0.6f, 1000f, 0.25f + (float)k * 0.03f, 0f, 0.3f, seed + k);
			}
			Drum(m, 2f * num, 55f, 0.7f, 0.5f, 0.5f, 900f, 1f, 0f, 0.4f, seed + 50);
			Swell(m, 0f, 2f * num, 0.5f, crash: true, 0.4f, seed + 60);
			return new MusicPiece
			{
				Name = "victory",
				Stereo = Finish(m, 0.8f, 0.4f, 0.7f, 0.12f, loop: false, 0)
			};
		}

		public static MusicPiece Death(int seed)
		{
			new Key(45, Aeolian);
			Mix m = new Mix(224000);
			int[] array = new int[4] { 45, 52, 57, 60 };
			for (int i = 0; i < array.Length; i++)
			{
				Strings(m, 0f, 3.6f, array[i], 0.6f, -0.45f + (float)i * 0.3f, 0.6f, 2.2f, 1100f, 0.6f, seed + i);
			}
			Bass(m, 0f, 3.6f, 33f, 0.7f, 0.3f, bowed: true);
			Bell(m, 0.4f, 69f, 0.6f, 0.2f, 2.4f, 0.7f);
			Bell(m, 0.4f, 57f, 0.4f, -0.2f, 2.4f, 0.7f);
			return new MusicPiece
			{
				Name = "death",
				Stereo = Finish(m, 0.88f, 0.4f, 1f, 0.09f, loop: false, 0)
			};
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

		private static void EPiano(Mix m, float startSec, float durSec, float midi, float vel, float pan, float send, int seed)
		{
			float num = Hz(midi);
			int num2 = (int)(startSec * 32000f);
			float num3 = 2.4f * (float)Math.Pow(220f / num, 0.44999998807907104);
			int num4 = Math.Min(m.Frames - num2, (int)(Math.Min(durSec + 0.35f, num3 * 4f) * 32000f));
			if (num4 <= 0 || num2 < 0)
			{
				return;
			}
			Pan(pan, out var gl, out var gr);
			Rng rng = new Rng(seed);
			double num5 = rng.Unit();
			double num6 = rng.Unit();
			float num7 = 1f;
			float num8 = (float)Math.Exp(-1.0 / (double)(num3 * 32000f));
			float num9 = 1f;
			float num10 = (float)Math.Exp(-0.0001953125);
			float num11 = 1f;
			float num12 = (float)Math.Exp(-0.0008928571428571428);
			float num13 = 0.5f + 0.7f * vel;
			bool flag = num * 7.1f < 13440f;
			int num14 = (int)(durSec * 32000f);
			int num15 = 11200;
			int fade = Math.Min(num4 / 4, 1280);
			float num16 = vel * 0.26f;
			for (int i = 0; i < num4; i++)
			{
				num5 += (double)(num / 32000f);
				float num17 = (0.2f + 1.5f * num9) * num13;
				float num18 = Sin(num5 + (double)(num17 * Sin(num5) * 0.159f));
				if (flag)
				{
					num6 += (double)(num * 7.1f / 32000f);
					num18 += Sin(num6) * 0.1f * num11 * num13;
				}
				float num19 = ((i < 60) ? ((float)i / 60f) : 1f);
				float num20 = ((i < num14) ? 1f : Math.Max(0f, 1f - (float)(i - num14) / (float)num15));
				m.Put(num2 + i, num18 * num7 * num19 * num20 * num20 * num16 * Tail(i, num4, fade), gl, gr, send);
				num7 *= num8;
				num9 *= num10;
				num11 *= num12;
			}
		}

		private static void MusicBox(Mix m, float startSec, float midi, float vel, float pan, float send)
		{
			float num = Hz(midi);
			int num2 = (int)(startSec * 32000f);
			float num3 = 1.5f * (float)Math.Pow(880f / num, 0.30000001192092896);
			int num4 = Math.Min(m.Frames - num2, (int)(num3 * 4f * 32000f));
			if (num4 <= 0 || num2 < 0)
			{
				return;
			}
			Pan(pan, out var gl, out var gr);
			float[] array = new float[3] { 1f, 6.27f, 17.55f };
			float[] array2 = new float[3] { 1f, 0.3f, 0.08f };
			float[] array3 = new float[3]
			{
				num3,
				num3 * 0.12f,
				0.03f
			};
			float[] array4 = new float[3];
			double[] array5 = new double[3];
			for (int i = 0; i < 3; i++)
			{
				array4[i] = (float)Math.Exp(-1.0 / (double)(array3[i] * 32000f));
				if (num * array[i] > 13440f)
				{
					array2[i] = 0f;
				}
			}
			int fade = Math.Min(num4 / 4, 1280);
			float num5 = vel * 0.2f;
			for (int j = 0; j < num4; j++)
			{
				float num6 = 0f;
				for (int k = 0; k < 3; k++)
				{
					array5[k] += num * array[k] / 32000f;
					num6 += Sin(array5[k]) * array2[k];
					array2[k] *= array4[k];
				}
				float num7 = ((j < 16) ? ((float)j / 16f) : 1f);
				m.Put(num2 + j, num6 * num5 * num7 * Tail(j, num4, fade), gl, gr, send);
			}
		}

		private static void Glass(Mix m, float startSec, float durSec, float midi, float vel, float pan, float attack, float release, float send, int seed)
		{
			float num = Hz(midi);
			int num2 = (int)(startSec * 32000f);
			int num3 = Math.Min(m.Frames - num2, (int)((durSec + release) * 32000f));
			if (num3 > 0 && num2 >= 0)
			{
				Pan(pan, out var gl, out var gr);
				Rng rng = new Rng(seed);
				double num4 = rng.Unit();
				double num5 = rng.Unit();
				double num6 = rng.Unit();
				float num7 = rng.Range(0.25f, 0.5f);
				float num8 = vel * 0.16f;
				for (int i = 0; i < num3; i++)
				{
					float num9 = (float)i / 32000f;
					num4 += (double)(num / 32000f);
					num5 += (double)(num * 2.004f / 32000f);
					num6 += (double)(num7 / 32000f);
					float num10 = Sin(num4) + Sin(num5) * 0.32f + Sin(num4 * 3.0) * 0.08f;
					float num11 = ((num9 < attack) ? (num9 / attack) : ((num9 < durSec) ? 1f : Math.Max(0f, 1f - (num9 - durSec) / release)));
					num11 = num11 * num11 * (3f - 2f * num11);
					m.Put(num2 + i, num10 * num11 * num8 * (0.85f + 0.15f * Sin(num6)), gl, gr, send);
				}
			}
		}

		private static void Voices(Mix m, float startSec, float durSec, float midi, float vel, float pan, float send, int vowel, float attack, int seed)
		{
			float num = Hz(midi);
			int num2 = (int)(startSec * 32000f);
			float num3 = Math.Min(0.6f, Math.Max(0.12f, durSec * 0.5f));
			int num4 = Math.Min(m.Frames - num2, (int)((durSec + num3) * 32000f));
			if (num4 > 0 && num2 >= 0)
			{
				Pan(pan, out var gl, out var gr);
				Rng rng = new Rng(seed);
				double num5 = rng.Unit();
				double num6 = rng.Unit();
				double num7 = rng.Unit();
				double num8 = rng.Unit();
				object obj;
				switch (vowel)
				{
				default:
					obj = new float[3] { 260f, 1900f, 2600f };
					break;
				case 1:
					obj = new float[3] { 340f, 870f, 2250f };
					break;
				case 0:
					obj = new float[3] { 760f, 1180f, 2700f };
					break;
				}
				float[] array = (float[])obj;
				object obj2;
				switch (vowel)
				{
				default:
					obj2 = new float[3] { 1f, 0.06f, 0.03f };
					break;
				case 1:
					obj2 = new float[3] { 1f, 0.35f, 0.12f };
					break;
				case 0:
					obj2 = new float[3] { 1f, 0.55f, 0.25f };
					break;
				}
				float[] array2 = (float[])obj2;
				Svf svf = default(Svf);
				Svf svf2 = default(Svf);
				Svf svf3 = default(Svf);
				svf.Tune(array[0]);
				svf2.Tune(array[1]);
				svf3.Tune(array[2]);
				float num9;
				switch (vowel)
				{
				default:
					num9 = 0.8f;
					break;
				case 1:
					num9 = 0.62f;
					break;
				case 0:
					num9 = 0.5f;
					break;
				}
				float num10 = vel * num9;
				for (int i = 0; i < num4; i++)
				{
					float num11 = (float)i / 32000f;
					num8 += 0.00015937499119900167;
					float num12 = 1f + 0.004f * Sin(num8) * Math.Min(1f, num11 / 0.5f);
					double num13 = num * num12 / 32000f;
					num5 += num13 * 0.995;
					num6 += num13 * 1.005;
					num7 += num13 * 1.0012;
					float x = (Saw(num5, num13) + Saw(num6, num13) + Saw(num7, num13)) * 0.33f;
					float num14 = svf.Band(x, 0.35f) * array2[0] + svf2.Band(x, 0.4f) * array2[1] + svf3.Band(x, 0.5f) * array2[2];
					float num15 = ((num11 < attack) ? (num11 / attack) : ((num11 < durSec) ? 1f : Math.Max(0f, 1f - (num11 - durSec) / num3)));
					num15 = num15 * num15 * (3f - 2f * num15);
					m.Put(num2 + i, num14 * num15 * num10, gl, gr, send);
				}
			}
		}

		private static void Reed(Mix m, float startSec, float durSec, float midi, float vel, float pan, float send, bool clarinet, int seed)
		{
			float num = Hz(midi);
			int num2 = (int)(startSec * 32000f);
			int num3 = Math.Min(m.Frames - num2, (int)((durSec + 0.1f) * 32000f));
			if (num3 > 0 && num2 >= 0)
			{
				Pan(pan, out var gl, out var gr);
				Rng rng = new Rng(seed);
				double num4 = rng.Unit();
				double num5 = rng.Unit();
				float num6 = (clarinet ? 0.5f : 0.22f);
				Svf svf = default(Svf);
				Svf svf2 = default(Svf);
				Svf svf3 = default(Svf);
				svf.Tune(Math.Min(5333.3335f, num * (clarinet ? 5f : 9f)));
				svf2.Tune(clarinet ? 1500f : 1100f);
				svf3.Tune(clarinet ? 2600f : 2900f);
				float num7 = vel * (clarinet ? 0.22f : 0.2f);
				for (int i = 0; i < num3; i++)
				{
					float num8 = (float)i / 32000f;
					num5 += 0.00015312500181607902;
					float num9 = 1f + 0.004f * Sin(num5) * Math.Min(1f, Math.Max(0f, (num8 - 0.3f) / 0.4f));
					double num10 = num * num9 / 32000f;
					num4 += num10;
					float x = Saw(num4, num10) - Saw(num4 + (double)num6, num10);
					float num11 = svf.Low(x, 0.9f);
					float num12 = num11 * 0.7f + svf2.Band(num11, 0.6f) * (clarinet ? 0.3f : 0.9f) + svf3.Band(num11, 0.8f) * 0.35f;
					num12 += (rng.Unit() * 2f - 1f) * 0.03f * Math.Max(0f, 1f - num8 / 0.08f);
					float num13 = ((num8 < 0.04f) ? (num8 / 0.04f) : ((num8 < durSec) ? (1f - 0.08f * Math.Min(1f, num8 / Math.Max(0.3f, durSec))) : (Math.Max(0f, 1f - (num8 - durSec) / 0.1f) * 0.92f)));
					m.Put(num2 + i, num12 * num13 * num7, gl, gr, send);
				}
			}
		}

		private static void Bowed(Mix m, float startSec, float durSec, float midi, float vel, float pan, float send, bool cello, int seed)
		{
			float num = Hz(midi);
			int num2 = (int)(startSec * 32000f);
			float num3 = (cello ? 0.25f : 0.09f);
			int num4 = Math.Min(m.Frames - num2, (int)((durSec + num3) * 32000f));
			if (num4 <= 0 || num2 < 0)
			{
				return;
			}
			Pan(pan, out var gl, out var gr);
			Rng rng = new Rng(seed);
			double num5 = rng.Unit();
			double num6 = rng.Unit();
			Svf svf = default(Svf);
			Svf svf2 = default(Svf);
			Svf svf3 = default(Svf);
			Svf svf4 = default(Svf);
			svf.Tune(cello ? 2400f : 4200f);
			svf2.Tune(cello ? 220f : 300f);
			svf3.Tune(cello ? 600f : 1000f);
			svf4.Tune(cello ? 1500f : 2800f);
			float num7 = (cello ? 5.2f : 5.9f);
			float num8 = (cello ? 0.005f : 0.006f);
			float num9 = (cello ? 0.09f : 0.03f);
			float num10 = vel * 0.2f;
			for (int i = 0; i < num4; i++)
			{
				float num11 = (float)i / 32000f;
				num6 += (double)(num7 / 32000f);
				float num12 = 1f + num8 * Sin(num6) * Math.Min(1f, Math.Max(0f, (num11 - 0.15f) / 0.35f));
				double num13 = num * num12 / 32000f;
				num5 += num13;
				float num14 = svf.Low(Saw(num5, num13), 0.8f);
				float num15 = num14 * 0.5f + svf2.Band(num14, 0.7f) * 0.6f + svf3.Band(num14, 0.8f) * 0.5f + svf4.Band(num14, 1f) * 0.3f;
				num15 += (rng.Unit() * 2f - 1f) * 0.02f;
				float num16 = ((num11 < num9) ? (num11 / num9) : ((num11 < durSec) ? 1f : Math.Max(0f, 1f - (num11 - durSec) / num3)));
				num16 = num16 * num16 * (3f - 2f * num16);
				if (num11 < durSec && durSec > 0.6f)
				{
					num16 *= 0.9f + 0.1f * (float)Math.Sin(Math.PI * (double)Math.Min(1f, num11 / durSec));
				}
				m.Put(num2 + i, num15 * num16 * num10, gl, gr, send);
			}
		}

		private static void Timpani(Mix m, float startSec, float midi, float vel, float pan, float send, int seed)
		{
			float num = Hz(midi);
			int num2 = (int)(startSec * 32000f);
			int num3 = Math.Min(m.Frames - num2, 83200);
			if (num3 <= 0 || num2 < 0)
			{
				return;
			}
			Pan(pan, out var gl, out var gr);
			Rng rng = new Rng(seed);
			float[] array = new float[5] { 1f, 1.5f, 1.98f, 2.44f, 2.94f };
			float[] array2 = new float[5] { 1f, 0.5f, 0.32f, 0.18f, 0.1f };
			float[] array3 = new float[5] { 0.9f, 0.55f, 0.4f, 0.28f, 0.2f };
			float[] array4 = new float[5];
			double[] array5 = new double[5];
			for (int i = 0; i < 5; i++)
			{
				array4[i] = (float)Math.Exp(-1.0 / (double)(array3[i] * 32000f));
				array5[i] = rng.Unit();
			}
			Svf svf = default(Svf);
			svf.Tune(500f);
			float num4 = 1f;
			float num5 = (float)Math.Exp(-0.00078125);
			int fade = Math.Min(num3 / 4, 1600);
			float num6 = vel * 0.42f;
			for (int j = 0; j < num3; j++)
			{
				float num7 = 1f + 0.025f * num4;
				num4 *= num5;
				float num8 = 0f;
				for (int k = 0; k < 5; k++)
				{
					array5[k] += num * array[k] * num7 / 32000f;
					num8 += Sin(array5[k]) * array2[k];
					array2[k] *= array4[k];
				}
				if (j < 533)
				{
					num8 += svf.Low(rng.Unit() * 2f - 1f, 0.7f) * 1.2f * (1f - (float)j / 533.3333f);
				}
				float num9 = ((j < 12) ? ((float)j / 12f) : 1f);
				m.Put(num2 + j, (float)Math.Tanh(num8 * 0.9f) * num6 * num9 * Tail(j, num3, fade), gl, gr, send);
			}
		}

		private static void TimpaniRoll(Mix m, float startSec, float durSec, float midi, float vel0, float vel1, float pan, float send, int seed)
		{
			int num = Math.Max(2, (int)(durSec / 0.07f));
			for (int i = 0; i < num; i++)
			{
				float num2 = (float)i / (float)(num - 1);
				Timpani(m, startSec + (float)i * durSec / (float)num, midi, (vel0 + (vel1 - vel0) * num2 * num2) * ((i % 2 == 0) ? 1f : 0.85f), pan + ((i % 2 == 0) ? (-0.05f) : 0.05f), send, seed + i);
			}
		}

		private static void Jingle(Mix m, float startSec, float vel, float pan, float send, int seed)
		{
			int num = (int)(startSec * 32000f);
			int num2 = Math.Min(m.Frames - num, 7040);
			if (num2 <= 0 || num < 0)
			{
				return;
			}
			Pan(pan, out var gl, out var gr);
			Rng rng = new Rng(seed);
			float[] obj = new float[4] { 0f, 6900f, 8300f, 9600f };
			obj[0] = 5200f + rng.Range(-200f, 200f);
			float[] array = obj;
			double[] array2 = new double[4];
			Svf svf = default(Svf);
			svf.Tune(6000f);
			float num3 = 1f;
			float num4 = (float)Math.Exp(-0.0006944444444444445);
			int fade = Math.Min(num2 / 4, 800);
			float num5 = vel * 0.16f;
			for (int i = 0; i < num2; i++)
			{
				float num6 = rng.Unit() * 2f - 1f;
				float num7 = (num6 - svf.Low(num6, 0.7f)) * 0.6f;
				for (int j = 0; j < 4; j++)
				{
					array2[j] += array[j] / 32000f;
					num7 += Sin(array2[j]) * 0.18f * (1f - 0.5f * rng.Unit());
				}
				m.Put(num + i, num7 * num3 * num5 * Tail(i, num2, fade), gl, gr, send);
				num3 *= num4;
			}
		}

		private static void Crash(Mix m, float startSec, float vel, float send, int seed)
		{
			Swell(m, startSec, 0.02f, vel, crash: true, send, seed);
		}

		private static Chord[] ParseChords(string text, int meter)
		{
			string[] array = text.Split(new char[2] { ' ', '|' }, StringSplitOptions.RemoveEmptyEntries);
			Chord[] array2 = new Chord[array.Length];
			for (int i = 0; i < array.Length; i++)
			{
				string obj = array[i];
				int root = obj[0] - 49;
				string text2 = obj.Substring(1);
				List<int> list = new List<int>((text2.IndexOf('s') < 0) ? new int[3] { 0, 2, 4 } : new int[3] { 0, 3, 4 });
				if (text2.IndexOf('7') >= 0)
				{
					list.Add(6);
				}
				if (text2.IndexOf('9') >= 0)
				{
					list.Add(8);
				}
				array2[i] = new Chord
				{
					Root = root,
					Tones = list.ToArray(),
					Beats = meter,
					Sharpen = ((text2.IndexOf('^') >= 0) ? 1 : 0)
				};
			}
			return array2;
		}

		private static List<Tn> ParseTune(string text)
		{
			List<Tn> list = new List<Tn>();
			float num = 0f;
			string[] array = text.Split(new char[2] { ' ', '|' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string obj in array)
			{
				int num2 = obj.IndexOf('/');
				string text2 = obj.Substring(0, num2);
				float num3 = float.Parse(obj.Substring(num2 + 1), CultureInfo.InvariantCulture);
				if (text2 != "r")
				{
					int acc = 0;
					if (text2[0] == '#')
					{
						acc = 1;
						text2 = text2.Substring(1);
					}
					else if (text2[0] == 'b')
					{
						acc = -1;
						text2 = text2.Substring(1);
					}
					list.Add(new Tn
					{
						Beat = num,
						Beats = num3,
						Degree = int.Parse(text2, CultureInfo.InvariantCulture) - 1,
						Acc = acc
					});
				}
				num += num3;
			}
			return list;
		}

		public static List<string> CheckThemes()
		{
			List<string> list = new List<string>();
			Theme[] themes = Themes;
			foreach (Theme theme in themes)
			{
				CheckTheme(list, theme.Name + " A", theme.Meter, theme.ChordsA, theme.TuneA);
				CheckTheme(list, theme.Name + " B", theme.Meter, theme.ChordsB, theme.TuneB);
			}
			CheckTheme(list, "battle clash", 4, "1 1 6 6 4 4 5^ 5^ 1 6 4 5^ 1 6 4 5^", "1/1.5 1/0.5 5/1 5/1 | 8/1.5 7/0.5 5/2 | 6/1.5 6/0.5 8/1 10/1 | 10/3 r/1 | 4/1.5 4/0.5 6/1 8/1 | 11/1.5 10/0.5 8/2 | 9/1.5 8/0.5 #7/1 5/1 | 9/3 r/1 | 8/1.5 8/0.5 12/1 12/1 | 13/1.5 12/0.5 10/2 | 11/1.5 10/0.5 8/1 11/1 | 12/2 9/1 #7/1 | 8/1.5 10/0.5 12/2 | 13/2 10/2 | 11/2 8/2 | 9/2 #7/2");
			CheckTheme(list, "battle hunt", 4, "1 1 7 7 4 4 5^ 5^ 1 7 4 5^ 1 7 4 5^", "1/0.75 1/0.25 5/1 5/0.75 6/0.25 5/1 | 8/1.5 7/0.5 5/2 | 7/0.75 7/0.25 9/1 11/1.5 9/0.5 | 9/2 7/2 | 6/0.75 6/0.25 8/1 11/1.5 10/0.5 | 8/2 6/2 | 5/1 #7/1 9/1.5 #7/0.5 | 9/2 5/2 | 8/0.75 8/0.25 12/1 12/0.75 13/0.25 12/1 | 11/1.5 9/0.5 7/2 | 8/1 11/1 13/1.5 11/0.5 | 12/1.5 9/0.5 #7/2 | 8/0.75 8/0.25 10/1 12/2 | 11/1 9/1 7/1 9/1 | 8/2 6/2 | 5/1 #7/1 9/2");
			return list;
		}

		private static void CheckTheme(List<string> report, string name, int meter, string chordsText, string tuneText)
		{
			Chord[] array = ParseChords(chordsText, meter);
			List<Tn> list = ParseTune(tuneText);
			float num = 0f;
			string[] array2 = tuneText.Split('|');
			foreach (string text in array2)
			{
				float num2 = 0f;
				string[] array3 = text.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string text2 in array3)
				{
					num2 += float.Parse(text2.Substring(text2.IndexOf('/') + 1), CultureInfo.InvariantCulture);
				}
				if (Math.Abs(num2 - (float)meter) > 0.01f)
				{
					report.Add($"{name}: a bar of {num2} beats: {text.Trim()}");
				}
				num += num2;
			}
			if (array.Length * meter != (int)Math.Round(num))
			{
				report.Add($"{name}: {array.Length} chords but {num / (float)meter} bars of tune");
			}
			int num3 = 0;
			int num4 = 0;
			foreach (Tn item in list)
			{
				if (!IsStrong(item.Beat % (float)meter, meter))
				{
					continue;
				}
				Chord chord = array[Math.Min(array.Length - 1, (int)(item.Beat / (float)meter))];
				num3++;
				int num5 = ((item.Degree - chord.Root) % 7 + 7) % 7;
				bool flag = false;
				int[] tones = chord.Tones;
				for (int i = 0; i < tones.Length; i++)
				{
					if (tones[i] % 7 == num5)
					{
						flag = true;
					}
				}
				if (flag)
				{
					num4++;
					continue;
				}
				report.Add($"{name}: degree {item.Degree + 1} on a strong beat of bar {(int)(item.Beat / (float)meter) + 1} is off its chord ({chord.Root + 1})");
			}
			report.Add($"{name}: {num4} of {num3} strong beats on chord tones");
		}

		private static bool IsStrong(float inBar, int meter)
		{
			if (inBar < 0.01f)
			{
				return true;
			}
			switch (meter)
			{
			case 4:
				return Math.Abs(inBar - 2f) < 0.01f;
			case 6:
				return Math.Abs(inBar - 3f) < 0.01f;
			default:
				return false;
			}
		}

		private static Performance Plan(Theme th, int seed, bool mayLift)
		{
			Rng rng = new Rng(seed * 19 + th.Name.Length * 7 + 3);
			Performance p = new Performance
			{
				Theme = th,
				Rng = rng,
				Seed = seed,
				Meter = th.Meter
			};
			int num = 48 + rng.Pick(th.Tonics) % 12;
			if (num > 56)
			{
				num -= 12;
			}
			p.Key = new Key(num, th.Mode);
			p.Spb = 60f / rng.Range(th.Bpm0, th.Bpm1);
			Chord[] array = ParseChords(th.ChordsA, th.Meter);
			Chord[] array2 = ParseChords(th.ChordsB, th.Meter);
			List<Tn> list = ParseTune(th.TuneA);
			List<Tn> list2 = ParseTune(th.TuneB);
			float num2 = (float)th.Meter * p.Spb;
			List<string> list3 = new List<string> { "A", "B", "A3" };
			int num3 = 2;
			if (36f * num2 <= 100f)
			{
				list3.Insert(1, "A2");
			}
			if ((float)(4 + 8 * (list3.Count + 1)) * num2 <= 100f)
			{
				list3.Insert(list3.IndexOf("B") + 1, "B2");
			}
			while ((float)(4 + 8 * (list3.Count + 2)) * num2 <= 70f && list3.Count < 9)
			{
				list3.Add("B2");
				list3.Add("A3");
			}
			bool flag = (float)(4 + 8 * list3.Count) * num2 > 100f;
			if (flag && (float)(num3 + 2 + 8 * (list3.Count - 1) + 4) * num2 > 100f)
			{
				num3 = 1;
			}
			bool flag2 = mayLift && rng.Chance(0.5f);
			int bar = 0;
			Add("intro", (num3 != 2) ? new Chord[1] { array[0] } : new Chord[2]
			{
				array[0],
				array[0]
			}, new List<Tn>(), p.Key);
			Key key = (flag2 ? new Key(p.Key.Tonic + 2, p.Key.Mode) : p.Key);
			for (int i = 0; i < list3.Count; i++)
			{
				string text = list3[i];
				bool num4 = text[0] == 'A';
				bool flag3 = i == list3.Count - 1;
				Chord[] array3 = (num4 ? array : array2);
				List<Tn> list4 = (num4 ? list : list2);
				if (flag3 && flag)
				{
					int num5 = array3.Length / 2;
					Chord[] array4 = new Chord[array3.Length - num5];
					Array.Copy(array3, num5, array4, 0, array4.Length);
					array3 = array4;
					List<Tn> list5 = new List<Tn>();
					foreach (Tn item in list4)
					{
						if (item.Beat >= (float)(num5 * th.Meter) - 0.01f)
						{
							list5.Add(new Tn
							{
								Beat = item.Beat - (float)(num5 * th.Meter),
								Beats = item.Beats,
								Degree = item.Degree,
								Acc = item.Acc
							});
						}
					}
					list4 = list5;
				}
				switch (text)
				{
				case "A2":
				case "B2":
				case "A3":
					list4 = Ornament(list4, rng, (text == "A3") ? 0.6f : 0.35f, th.Meter);
					break;
				}
				Add(text, array3, list4, flag3 ? key : p.Key);
			}
			Add("outro", new Chord[2]
			{
				Ch(0, th.Meter),
				Ch(0, th.Meter)
			}, new List<Tn>(), key);
			p.Mix = new Mix((int)(((float)(bar * th.Meter) * p.Spb + 6f) * 32000f));
			return p;
			void Add(string kind, Chord[] chords, List<Tn> tune, Key key2)
			{
				p.Sections.Add(new Section
				{
					Kind = kind,
					Bar0 = bar,
					Chords = chords,
					Tune = tune,
					Key = key2
				});
				bar += chords.Length;
			}
		}

		private static List<Tn> Ornament(List<Tn> tune, Rng rng, float amount, int meter)
		{
			List<Tn> list = new List<Tn>(tune.Count + 8);
			for (int i = 0; i < tune.Count; i++)
			{
				Tn item = tune[i];
				if (i + 1 < tune.Count && Math.Abs(tune[i + 1].Beat - (item.Beat + item.Beats)) < 0.01f && item.Beats >= 1f && item.Acc == 0 && Math.Abs(tune[i + 1].Degree - item.Degree) == 2 && rng.Chance(amount * 0.45f))
				{
					float num = item.Beats * 0.5f;
					list.Add(new Tn
					{
						Beat = item.Beat,
						Beats = num,
						Degree = item.Degree,
						Acc = 0
					});
					list.Add(new Tn
					{
						Beat = item.Beat + num,
						Beats = num,
						Degree = (item.Degree + tune[i + 1].Degree) / 2,
						Acc = 0
					});
					continue;
				}
				if (list.Count > 0 && item.Beats >= 1f && IsStrong(item.Beat % (float)meter, meter) && rng.Chance(amount * 0.3f))
				{
					Tn value = list[list.Count - 1];
					float num2 = Math.Min(0.25f, value.Beats * 0.25f);
					if (num2 >= 0.1f && Math.Abs(value.Beat + value.Beats - item.Beat) < 0.01f)
					{
						value.Beats -= num2;
						list[list.Count - 1] = value;
						list.Add(new Tn
						{
							Beat = item.Beat - num2,
							Beats = num2,
							Degree = item.Degree + 1,
							Acc = 0
						});
					}
				}
				list.Add(item);
			}
			return list;
		}

		private static int OctaveFor(Key k, int target)
		{
			return (int)Math.Round((double)(target - k.Tonic) / 12.0);
		}

		private static void PlayTune(Performance p, Section s, List<Tn> tune, int target, float vel, float legato, Action<float, float, int, float> play, int degreeShift = 0)
		{
			int num = OctaveFor(p.Key, target);
			foreach (Tn item in tune)
			{
				float num2 = item.Beat % (float)p.Meter;
				int num3 = (int)(item.Beat / (float)p.Meter) % 4;
				float num4 = 0.86f + 0.14f * (float)Math.Sin(Math.PI * (double)((float)num3 + num2 / (float)p.Meter) / 4.0);
				float arg = vel * num4 * (IsStrong(num2, p.Meter) ? 1f : 0.87f);
				int arg2 = s.Key.Note(item.Degree + degreeShift) + ((degreeShift == 0) ? item.Acc : 0) + 12 * num;
				play(p.Seconds(s.Bar0, item.Beat), item.Beats * p.Spb * legato, arg2, arg);
			}
		}

		private static void PlayCounter(Performance p, Section s, int lo, int hi, float density, float vel, Action<float, float, int, float> play)
		{
			OctaveFor(p.Key, (lo + hi) / 2);
			foreach (Note item in Melody(new Key(s.Key.Tonic, s.Key.Mode), s.Chords, p.Meter, lo, hi, p.Rng, density))
			{
				play(p.Seconds(s.Bar0, item.Beat), item.Beats * p.Spb, item.Midi, vel * item.Vel);
			}
		}

		private static int[] Spread(Key k, Chord c, int low, bool ninth)
		{
			int i;
			for (i = ChordNote(k, c, 0, 0); i < low; i += 12)
			{
			}
			while (i >= low + 12)
			{
				i -= 12;
			}
			int num = Above(ChordNote(k, c, 1, 0), i);
			int num2 = Above(ChordNote(k, c, 2, 0), i);
			List<int> list = new List<int>
			{
				i,
				num2,
				i + 12
			};
			if (ninth && c.Sharpen == 0)
			{
				list.Add(Above(k.Note(c.Root + 1), i + 12));
			}
			list.Add(num + 12);
			list.Add(num2 + 12);
			list.Sort();
			return list.ToArray();
		}

		private static int Above(int note, int floor)
		{
			while (note <= floor)
			{
				note += 12;
			}
			while (note - 12 > floor)
			{
				note -= 12;
			}
			return note;
		}

		private static void Echoes(float delay, int repeats, float feedback, float pan, Action<float, float, float> note)
		{
			note(0f, 1f, pan);
			float num = 1f;
			for (int i = 1; i <= repeats; i++)
			{
				num *= feedback;
				note((float)i * delay, num, ((i % 2 == 1) ? (-0.7f) : 0.7f) * 0.8f + pan * 0.2f);
			}
		}

		private static float Level(Section s, float intro, float a, float a2, float b, float a3, float outro)
		{
			switch (s.Kind)
			{
			case "intro":
				return intro;
			case "A":
				return a;
			case "A2":
				return a2;
			case "B":
			case "B2":
				return b;
			case "A3":
				return a3;
			default:
				return outro;
			}
		}

		private static MusicPiece PlayMeadow(Theme th, int seed)
		{
			Performance performance = Plan(th, seed, mayLift: true);
			Mix m = performance.Mix;
			_ = performance.Rng;
			float spb = performance.Spb;
			int[] array = null;
			foreach (Section section in performance.Sections)
			{
				float num = Level(section, 0.5f, 0.5f, 0.55f, 0.62f, 0.66f, 0.5f);
				for (int i = 0; i < section.Bars; i++)
				{
					Chord c = section.Chords[i];
					float num2 = performance.Seconds(section.Bar0 + i);
					array = Voice(section.Key, c, array, section.Key.Tonic + 19);
					bool flag = section.Kind == "intro" && i == 0;
					for (int j = 0; j < array.Length; j++)
					{
						Strings(m, num2, 4f * spb, array[j], 0.5f * num, -0.5f + (float)j * 0.33f, flag ? 2f : 0.5f, 0.9f, 1700f, 0.5f, seed + section.Bar0 * 17 + i * 13 + j);
					}
					if (!flag)
					{
						Bass(m, num2, 4f * spb, ChordNote(section.Key, c, 0, -1), 0.6f, 0.25f, bowed: true);
					}
					if (section.Kind != "outro" && !flag)
					{
						int[] array2 = Spread(section.Key, c, section.Key.Tonic + 12, ninth: true);
						bool flag2 = section.Kind[0] == 'B';
						int num3 = (flag2 ? 4 : 8);
						for (int k = 0; k < num3 && (i % 4 != 3 || k < num3 - 2); k++)
						{
							int val = ((k < array2.Length) ? k : (2 * (array2.Length - 1) - k));
							val = Math.Max(0, Math.Min(array2.Length - 1, val));
							float startSec = num2 + (float)k * (flag2 ? 1f : 0.5f) * spb;
							Pluck(m, startSec, array2[val], (k == 0) ? 0.62f : 0.46f, -0.35f + 0.1f * (float)val, 0.55f, 1.6f, 0.45f, seed * 31 + section.Bar0 * 8 + i * 8 + k);
						}
					}
					if ((section.Kind == "B" || section.Kind == "B2") && i == section.Bars - 1)
					{
						for (int l = 0; l < 12; l++)
						{
							Pluck(m, num2 + (3f + (float)l / 12f) * spb, section.Key.Note(7 + l), 0.3f + (float)l * 0.02f, -0.4f + (float)l * 0.07f, 0.6f, 1.2f, 0.5f, seed + l * 5);
						}
					}
					if (section.Verse && i % 4 == 3)
					{
						Bell(m, num2 + 2f * spb, ChordNote(section.Key, c, 0, 3), 0.25f, 0.5f, 1.4f, 0.6f);
					}
				}
				switch (section.Kind)
				{
				case "A":
					PlayTune(performance, section, section.Tune, 74, 0.75f, 0.95f, delegate(float t, float d, int n, float v)
					{
						Flute(m, t, d, n, v, -0.12f, 0.42f, recorder: false, seed + n);
					});
					break;
				case "A2":
					PlayTune(performance, section, section.Tune, 67, 0.8f, 0.93f, delegate(float t, float d, int n, float v)
					{
						Reed(m, t, d, n, v, 0.1f, 0.38f, clarinet: false, seed + n);
					});
					PlayCounter(performance, section, performance.Key.Tonic + 31, performance.Key.Tonic + 43, 0.45f, 0.42f, delegate(float t, float d, int n, float v)
					{
						Flute(m, t, d, n, v, -0.3f, 0.5f, recorder: false, seed + n * 3);
					});
					break;
				case "B":
				case "B2":
					PlayTune(performance, section, section.Tune, 62, 0.62f, 0.98f, delegate(float t, float d, int n, float v)
					{
						Strings(m, t, d, n, v, 0.15f, 0.12f, 0.35f, 2600f, 0.45f, seed + n * 7);
					});
					if (section.Kind == "B2")
					{
						PlayTune(performance, section, section.Tune, 74, 0.5f, 0.95f, delegate(float t, float d, int n, float v)
						{
							Flute(m, t, d, n, v, -0.2f, 0.45f, recorder: false, seed + n);
						});
					}
					break;
				case "A3":
					PlayTune(performance, section, section.Tune, 74, 0.8f, 0.95f, delegate(float t, float d, int n, float v)
					{
						Flute(m, t, d, n, v, -0.15f, 0.42f, recorder: false, seed + n);
					});
					PlayTune(performance, section, section.Tune, 74, 0.55f, 0.93f, delegate(float t, float d, int n, float v)
					{
						Reed(m, t, d, n, v, 0.2f, 0.4f, clarinet: false, seed + n * 5);
					}, -2);
					break;
				case "outro":
					Pluck(m, performance.Seconds(section.Bar0), section.Key.Tonic + 12, 0.6f, 0f, 0.5f, 2.4f, 0.55f, seed + 1);
					Bell(m, performance.Seconds(section.Bar0, 2f), section.Key.Tonic + 36, 0.35f, 0.3f, 2f, 0.7f);
					break;
				}
			}
			return new MusicPiece
			{
				Name = "day_meadow",
				Stereo = Finish(m, 0.84f, 0.35f, 0.9f, 0.11f, loop: false, 0)
			};
		}

		private static MusicPiece PlayDaydream(Theme th, int seed)
		{
			Performance performance = Plan(th, seed, mayLift: false);
			Mix m = performance.Mix;
			float spb = performance.Spb;
			int[] array = null;
			float delay = 0.75f * spb;
			foreach (Section section in performance.Sections)
			{
				float vel = Level(section, 0.42f, 0.45f, 0.5f, 0.55f, 0.55f, 0.45f);
				for (int i = 0; i < section.Bars; i++)
				{
					Chord c = section.Chords[i];
					float num = performance.Seconds(section.Bar0 + i);
					array = Voice(section.Key, c, array, section.Key.Tonic + 19);
					for (int j = 0; j < array.Length; j++)
					{
						Glass(m, num, 4f * spb, array[j] + 12, vel, -0.4f + (float)j * 0.27f, (section.Kind == "intro" && i == 0) ? 2.5f : 1f, 1.2f, 0.65f, seed + section.Bar0 * 7 + i * 5 + j);
						if (section.Kind[0] == 'B' || section.Kind == "A3")
						{
							Voices(m, num, 4f * spb, array[j], 0.32f, -0.5f + (float)j * 0.33f, 0.7f, 1, 0.9f, seed + i * 3 + j);
						}
					}
					if (section.Kind != "intro")
					{
						Bass(m, num, 4f * spb, ChordNote(section.Key, c, 0, -1), 0.42f, 0.3f, bowed: true);
					}
					if (section.Kind == "outro" && i == 1)
					{
						continue;
					}
					int[] array2 = Spread(section.Key, c, section.Key.Tonic + 12, ninth: true);
					bool flag = section.Kind[0] == 'B';
					int num2 = (flag ? 4 : 8);
					for (int k = 0; k < num2; k++)
					{
						int val = k % array2.Length;
						if (!flag && k >= array2.Length)
						{
							val = array2.Length - 1 - (k - array2.Length) - 1;
						}
						val = Math.Max(0, Math.Min(array2.Length - 1, val));
						float at = num + (float)k * (flag ? 1f : 0.5f) * spb;
						int note = array2[val];
						int sd = seed + section.Bar0 * 11 + i * 8 + k;
						Echoes(delay, 2, 0.38f, -0.2f + 0.08f * (float)val, delegate(float off, float g, float pan)
						{
							EPiano(m, at + off, 0.45f * spb, note, 0.42f * g, pan, 0.55f, sd);
						});
					}
				}
				switch (section.Kind)
				{
				case "A":
					PlayTune(performance, section, section.Tune, 74, 0.72f, 0.96f, delegate(float t, float d, int n, float v)
					{
						Flute(m, t, d, n, v, 0.1f, 0.5f, recorder: false, seed + n);
					});
					break;
				case "A2":
					PlayTune(performance, section, section.Tune, 79, 0.85f, 1f, delegate(float t, float d, int n, float v)
					{
						MusicBox(m, t, n, v, 0.15f, 0.55f);
						Glass(m, t, d, n - 12, v * 0.5f, -0.1f, 0.05f, 0.5f, 0.6f, seed + n);
					});
					break;
				case "B":
				case "B2":
					PlayTune(performance, section, section.Tune, 55, 0.7f, 0.98f, delegate(float t, float d, int n, float v)
					{
						Bowed(m, t, d, n, v, 0.05f, 0.5f, cello: true, seed + n * 3);
					});
					break;
				case "A3":
					PlayTune(performance, section, section.Tune, 74, 0.75f, 0.96f, delegate(float t, float d, int n, float v)
					{
						Flute(m, t, d, n, v, 0.1f, 0.5f, recorder: false, seed + n);
					});
					PlayTune(performance, section, section.Tune, 86, 0.5f, 1f, delegate(float t, float d, int n, float v)
					{
						if (d >= 0.9f * spb)
						{
							MusicBox(m, t, n, v, -0.2f, 0.6f);
						}
					});
					break;
				case "outro":
					Bell(m, performance.Seconds(section.Bar0, 1f), section.Key.Tonic + 36, 0.3f, 0.3f, 2.2f, 0.75f);
					MusicBox(m, performance.Seconds(section.Bar0, 2.5f), section.Key.Tonic + 43, 0.4f, -0.2f, 0.7f);
					break;
				}
			}
			return new MusicPiece
			{
				Name = "day_daydream",
				Stereo = Finish(m, 0.9f, 0.3f, 1.1f, 0.095f, loop: false, 0)
			};
		}

		private static MusicPiece PlayWaltz(Theme th, int seed)
		{
			Performance performance = Plan(th, seed, mayLift: true);
			Mix m = performance.Mix;
			float spb = performance.Spb;
			int[] array = null;
			foreach (Section section in performance.Sections)
			{
				bool flag = section.Kind[0] == 'B';
				for (int i = 0; i < section.Bars; i++)
				{
					Chord c = section.Chords[i];
					float num = performance.Seconds(section.Bar0 + i);
					int num2 = seed + section.Bar0 * 9 + i * 5;
					Pluck(m, num, ChordNote(section.Key, c, 0, 0), 0.62f, -0.25f, 0.6f, 1.3f, 0.35f, num2);
					for (int j = 1; j < 3; j++)
					{
						for (int k = 0; k < 3; k++)
						{
							Pluck(m, num + (float)j * spb + (float)k * 0.01f, ChordNote(section.Key, c, k, 1), flag ? 0.3f : 0.36f, -0.1f + (float)k * 0.12f, flag ? 0.3f : 0.65f, flag ? 0.35f : 0.7f, 0.3f, num2 + j * 3 + k);
						}
					}
					if (flag || section.Kind == "A3" || section.Kind == "outro")
					{
						array = Voice(section.Key, c, array, section.Key.Tonic + 19);
						for (int l = 1; l < array.Length; l++)
						{
							Strings(m, num, 3f * spb, array[l], 0.32f, -0.5f + (float)l * 0.33f, 0.4f, 0.7f, 1600f, 0.5f, num2 + l);
						}
					}
					if ((section.Kind == "A2" || section.Kind == "A3") && i % 2 == 0)
					{
						Bell(m, num, section.Key.Tonic + 48, 0.12f, 0.4f, 0.4f, 0.4f);
					}
					if (section.Kind != "intro")
					{
						Bass(m, num, 2.5f * spb, ChordNote(section.Key, c, 0, -1), 0.4f, 0.2f, bowed: false);
					}
				}
				switch (section.Kind)
				{
				case "A":
					PlayTune(performance, section, section.Tune, 67, 0.8f, 0.92f, delegate(float t, float d, int n, float v)
					{
						Reed(m, t, d, n, v, 0.12f, 0.35f, clarinet: false, seed + n);
					});
					break;
				case "A2":
					PlayTune(performance, section, section.Tune, 74, 0.75f, 0.92f, delegate(float t, float d, int n, float v)
					{
						Flute(m, t, d, n, v, -0.12f, 0.4f, recorder: false, seed + n);
					});
					PlayCounter(performance, section, performance.Key.Tonic + 14, performance.Key.Tonic + 26, 0.6f, 0.45f, delegate(float t, float d, int n, float v)
					{
						Reed(m, t, d, n, v, 0.25f, 0.35f, clarinet: false, seed + n * 3);
					});
					break;
				case "B":
				case "B2":
					PlayTune(performance, section, section.Tune, 60, 0.55f, 0.97f, delegate(float t, float d, int n, float v)
					{
						Brass(m, t, d, n, v, 0.15f, 0.45f, seed + n);
					});
					if (section.Kind == "B2")
					{
						PlayTune(performance, section, section.Tune, 67, 0.55f, 0.92f, delegate(float t, float d, int n, float v)
						{
							Reed(m, t, d, n, v, -0.15f, 0.4f, clarinet: false, seed + n * 2);
						}, 2);
					}
					break;
				case "A3":
					PlayTune(performance, section, section.Tune, 67, 0.8f, 0.92f, delegate(float t, float d, int n, float v)
					{
						Reed(m, t, d, n, v, 0.12f, 0.38f, clarinet: false, seed + n);
					});
					PlayTune(performance, section, section.Tune, 79, 0.55f, 0.92f, delegate(float t, float d, int n, float v)
					{
						Flute(m, t, d, n, v, -0.15f, 0.42f, recorder: false, seed + n * 2);
					});
					break;
				case "outro":
					Bell(m, performance.Seconds(section.Bar0, 1f), section.Key.Tonic + 36, 0.25f, 0.2f, 1.8f, 0.6f);
					break;
				}
			}
			return new MusicPiece
			{
				Name = "day_waltz",
				Stereo = Finish(m, 0.8f, 0.4f, 0.75f, 0.105f, loop: false, 0)
			};
		}

		private static MusicPiece PlayHymn(Theme th, int seed)
		{
			Performance performance = Plan(th, seed, mayLift: true);
			Mix m = performance.Mix;
			float spb = performance.Spb;
			int[] array = null;
			foreach (Section section in performance.Sections)
			{
				float vel = Level(section, 0.45f, 0.52f, 0.5f, 0.48f, 0.62f, 0.55f);
				for (int i = 0; i < section.Bars; i++)
				{
					Chord c = section.Chords[i];
					float num = performance.Seconds(section.Bar0 + i);
					int num2 = seed + section.Bar0 * 13 + i * 7;
					array = Voice(section.Key, c, array, section.Key.Tonic + 17);
					for (int j = 0; j < array.Length; j++)
					{
						Strings(m, num, 4f * spb, array[j], vel, -0.5f + (float)j * 0.33f, (section.Kind == "intro" && i == 0) ? 1.8f : 0.4f, 0.8f, 1500f, 0.5f, num2 + j);
					}
					Bass(m, num, 4f * spb, ChordNote(section.Key, c, 0, -1), 0.6f, 0.25f, bowed: true);
					if (section.Kind[0] == 'B' || section.Kind == "A3")
					{
						for (int k = 1; k < array.Length; k++)
						{
							Voices(m, num, 4f * spb, array[k] + 12, 0.34f, -0.5f + (float)k * 0.35f, 0.6f, 0, 0.5f, num2 + k * 3);
						}
					}
					if (section.Kind == "A3" && i % 4 == 0)
					{
						Timpani(m, num, section.Key.Tonic - 12 + ((c.Root == 4) ? 7 : 0), 0.55f, 0f, 0.35f, num2);
					}
					if ((section.Kind == "B" || section.Kind == "B2") && i == section.Bars - 1)
					{
						TimpaniRoll(m, num + 2f * spb, 2f * spb, performance.Key.Tonic - 5, 0.15f, 0.6f, 0f, 0.35f, num2);
					}
				}
				switch (section.Kind)
				{
				case "A":
					PlayTune(performance, section, section.Tune, 60, 0.6f, 0.97f, delegate(float t, float d, int n, float v)
					{
						Brass(m, t, d, n, v, 0.1f, 0.42f, seed + n);
					});
					break;
				case "A2":
					PlayTune(performance, section, section.Tune, 69, 0.6f, 0.98f, delegate(float t, float d, int n, float v)
					{
						Strings(m, t, d, n, v, 0.1f, 0.1f, 0.35f, 2600f, 0.45f, seed + n * 3);
					});
					PlayCounter(performance, section, performance.Key.Tonic + 7, performance.Key.Tonic + 19, 0.5f, 0.45f, delegate(float t, float d, int n, float v)
					{
						Brass(m, t, d, n, v, -0.25f, 0.42f, seed + n * 5);
					});
					break;
				case "B":
				case "B2":
					PlayTune(performance, section, section.Tune, 74, 0.7f, 0.95f, delegate(float t, float d, int n, float v)
					{
						Flute(m, t, d, n, v, 0.12f, 0.45f, recorder: false, seed + n);
					});
					break;
				case "A3":
					PlayTune(performance, section, section.Tune, 60, 0.7f, 0.97f, delegate(float t, float d, int n, float v)
					{
						Brass(m, t, d, n, v, 0.1f, 0.42f, seed + n);
					});
					PlayTune(performance, section, section.Tune, 72, 0.62f, 0.97f, delegate(float t, float d, int n, float v)
					{
						if (d >= 0.9f * spb)
						{
							Brass(m, t, d, n, v, -0.15f, 0.45f, seed + n * 7);
						}
					});
					break;
				case "outro":
					TimpaniRoll(m, performance.Seconds(section.Bar0), 3f * spb, section.Key.Tonic - 12, 0.4f, 0.1f, 0f, 0.4f, seed + 7);
					break;
				}
			}
			return new MusicPiece
			{
				Name = "day_hymn",
				Stereo = Finish(m, 0.86f, 0.35f, 0.95f, 0.105f, loop: false, 0)
			};
		}

		private static MusicPiece PlayMoonlight(Theme th, int seed)
		{
			Performance performance = Plan(th, seed, mayLift: false);
			Mix m = performance.Mix;
			float spb = performance.Spb;
			int[] array = null;
			float delay = 0.75f * spb;
			foreach (Section section in performance.Sections)
			{
				for (int i = 0; i < section.Bars; i++)
				{
					Chord c = section.Chords[i];
					float num = performance.Seconds(section.Bar0 + i);
					int num2 = seed + section.Bar0 * 7 + i * 11;
					array = Voice(section.Key, c, array, section.Key.Tonic + 17);
					for (int j = 0; j < array.Length; j++)
					{
						Glass(m, num, 4f * spb, array[j] + 12, 0.42f, -0.4f + (float)j * 0.27f, (section.Kind == "intro" && i == 0) ? 2.5f : 1.2f, 1.4f, 0.65f, num2 + j);
						Strings(m, num, 4f * spb, array[j], 0.22f, -0.45f + (float)j * 0.3f, 1.4f, 1.4f, 900f, 0.6f, num2 + j * 5);
						if (section.Kind[0] == 'B')
						{
							Voices(m, num, 4f * spb, array[j] + 12, 0.3f, -0.5f + (float)j * 0.33f, 0.7f, 1, 1f, num2 + j * 9);
						}
					}
					if (section.Kind != "intro")
					{
						Bass(m, num, 4f * spb, ChordNote(section.Key, c, 0, -1), 0.4f, 0.35f, bowed: true);
					}
					if (section.Kind == "outro" && i == 1)
					{
						continue;
					}
					int[] array2 = Spread(section.Key, c, section.Key.Tonic + 12, ninth: true);
					int[] array3 = new int[8] { 0, 1, 2, 3, 4, 3, 2, 1 };
					for (int k = 0; k < 8; k++)
					{
						int note = array2[Math.Min(array2.Length - 1, array3[k])];
						float at = num + (float)k * 0.5f * spb;
						int nsd = num2 + k;
						Echoes(delay, 2, 0.4f, -0.15f + 0.08f * (float)array3[k], delegate(float off, float g, float pan)
						{
							EPiano(m, at + off, 0.5f * spb, note, 0.36f * g, pan, 0.6f, nsd);
						});
					}
				}
				switch (section.Kind)
				{
				case "A":
					PlayTune(performance, section, section.Tune, 62, 0.75f, 0.97f, delegate(float t, float d, int n, float v)
					{
						Reed(m, t, d, n, v, 0.1f, 0.5f, clarinet: true, seed + n);
					});
					break;
				case "A2":
					PlayTune(performance, section, section.Tune, 74, 0.7f, 0.96f, delegate(float t, float d, int n, float v)
					{
						Flute(m, t, d, n, v, -0.1f, 0.55f, recorder: false, seed + n);
					});
					break;
				case "B":
				case "B2":
					PlayTune(performance, section, section.Tune, 50, 0.72f, 0.98f, delegate(float t, float d, int n, float v)
					{
						Bowed(m, t, d, n, v, 0.05f, 0.5f, cello: true, seed + n * 3);
					});
					break;
				case "A3":
					PlayTune(performance, section, section.Tune, 62, 0.75f, 0.97f, delegate(float t, float d, int n, float v)
					{
						Reed(m, t, d, n, v, 0.1f, 0.5f, clarinet: true, seed + n);
					});
					PlayTune(performance, section, section.Tune, 86, 0.45f, 1f, delegate(float t, float d, int n, float v)
					{
						if (d >= 0.9f * spb)
						{
							MusicBox(m, t, n, v, -0.25f, 0.65f);
						}
					});
					break;
				case "outro":
					Bell(m, performance.Seconds(section.Bar0, 2f), section.Key.Tonic + 36, 0.3f, 0.25f, 2.4f, 0.75f);
					break;
				}
			}
			return new MusicPiece
			{
				Name = "night_moonlight",
				Stereo = Finish(m, 0.9f, 0.38f, 1.1f, 0.085f, loop: false, 0)
			};
		}

		private static MusicPiece PlayLullaby(Theme th, int seed)
		{
			Performance performance = Plan(th, seed, mayLift: false);
			Mix m = performance.Mix;
			float spb = performance.Spb;
			int[] array = null;
			foreach (Section section in performance.Sections)
			{
				for (int i = 0; i < section.Bars; i++)
				{
					Chord c = section.Chords[i];
					float num = performance.Seconds(section.Bar0 + i);
					int num2 = seed + section.Bar0 * 5 + i * 3;
					Pluck(m, num, ChordNote(section.Key, c, 0, 0), 0.45f, -0.2f, 0.4f, 1.8f, 0.5f, num2);
					for (int j = 1; j < 3; j++)
					{
						Pluck(m, num + (float)j * spb, ChordNote(section.Key, c, j, 1), 0.3f, 0.1f + (float)j * 0.1f, 0.4f, 1.4f, 0.5f, num2 + j);
					}
					array = Voice(section.Key, c, array, section.Key.Tonic + 19);
					for (int k = 1; k < array.Length; k++)
					{
						Glass(m, num, 3f * spb, array[k] + 12, 0.28f, -0.4f + (float)k * 0.27f, 1f, 1.2f, 0.65f, num2 + k);
						if (section.Kind[0] == 'B' || section.Kind == "A3")
						{
							Strings(m, num, 3f * spb, array[k], 0.25f, -0.4f + (float)k * 0.27f, 0.8f, 1f, 1100f, 0.6f, num2 + k * 7);
						}
					}
					if (section.Kind != "intro")
					{
						Bass(m, num, 3f * spb, ChordNote(section.Key, c, 0, -1), 0.32f, 0.3f, bowed: true);
					}
				}
				switch (section.Kind)
				{
				case "A":
					PlayTune(performance, section, section.Tune, 79, 0.9f, 1f, delegate(float t, float d, int n, float v)
					{
						MusicBox(m, t, n, v, 0.1f, 0.55f);
					});
					break;
				case "A2":
					PlayTune(performance, section, section.Tune, 74, 0.68f, 0.95f, delegate(float t, float d, int n, float v)
					{
						Flute(m, t, d, n, v, -0.1f, 0.5f, recorder: false, seed + n);
					});
					PlayTune(performance, section, section.Tune, 91, 0.4f, 1f, delegate(float t, float d, int n, float v)
					{
						if (d >= 1.5f * spb)
						{
							MusicBox(m, t, n, v, 0.3f, 0.6f);
						}
					});
					break;
				case "B":
				case "B2":
					PlayTune(performance, section, section.Tune, 62, 0.62f, 0.96f, delegate(float t, float d, int n, float v)
					{
						Reed(m, t, d, n, v, 0.1f, 0.5f, clarinet: true, seed + n);
					});
					break;
				case "A3":
					PlayTune(performance, section, section.Tune, 79, 0.9f, 1f, delegate(float t, float d, int n, float v)
					{
						MusicBox(m, t, n, v, 0.1f, 0.55f);
					});
					PlayTune(performance, section, section.Tune, 67, 0.4f, 1f, delegate(float t, float d, int n, float v)
					{
						EPiano(m, t, d, n, v, -0.2f, 0.55f, seed + n);
					});
					break;
				case "outro":
				{
					for (int l = 0; l < 4; l++)
					{
						MusicBox(m, performance.Seconds(section.Bar0, (float)l * 0.75f), section.Key.Note(14 + l * 2) + 12, 0.5f - (float)l * 0.08f, -0.2f + (float)l * 0.15f, 0.65f);
					}
					break;
				}
				}
			}
			return new MusicPiece
			{
				Name = "night_lullaby",
				Stereo = Finish(m, 0.88f, 0.4f, 1f, 0.085f, loop: false, 0)
			};
		}

		private static MusicPiece PlayStarlight(Theme th, int seed)
		{
			Performance performance = Plan(th, seed, mayLift: false);
			Mix m = performance.Mix;
			Rng rng = performance.Rng;
			float spb = performance.Spb;
			int[] array = null;
			foreach (Section section in performance.Sections)
			{
				for (int i = 0; i < section.Bars; i++)
				{
					Chord c = section.Chords[i];
					float num = performance.Seconds(section.Bar0 + i);
					int num2 = seed + section.Bar0 * 11 + i * 3;
					array = Voice(section.Key, c, array, section.Key.Tonic + 17);
					for (int j = 0; j < array.Length; j++)
					{
						Voices(m, num, 4f * spb, array[j] + 12, 0.36f, -0.5f + (float)j * 0.33f, 0.7f, 1, (section.Kind == "intro" && i == 0) ? 2f : 1.1f, num2 + j);
						Glass(m, num, 4f * spb, array[j] + 12, 0.3f, 0.4f - (float)j * 0.27f, 1.2f, 1.4f, 0.7f, num2 + j * 3);
					}
					if (section.Kind != "intro")
					{
						Bass(m, num, 4f * spb, ChordNote(section.Key, c, 0, -1), 0.35f, 0.35f, bowed: true);
					}
					int num3 = ((section.Kind == "intro" || section.Kind == "outro") ? 1 : (2 + rng.Int(2)));
					for (int k = 0; k < num3; k++)
					{
						Bell(m, num + (float)rng.Int(8) * 0.5f * spb, ChordNote(section.Key, c, rng.Int(c.Tones.Length), 3), 0.18f + rng.Unit() * 0.1f, rng.Range(-0.7f, 0.7f), 1.6f, 0.75f);
					}
					if (section.Kind[0] == 'B')
					{
						for (int l = 0; l < 4; l++)
						{
							Pluck(m, num + (float)l * spb, ChordNote(section.Key, c, l % c.Tones.Length, 1) + ((l == 3) ? 12 : 0), 0.4f, -0.3f + (float)l * 0.2f, 0.4f, 2.2f, 0.6f, num2 + l);
						}
					}
				}
				switch (section.Kind)
				{
				case "A":
					PlayTune(performance, section, section.Tune, 50, 0.72f, 0.98f, delegate(float t, float d, int n, float v)
					{
						Bowed(m, t, d, n, v, 0.05f, 0.5f, cello: true, seed + n * 3);
					});
					break;
				case "A2":
					PlayTune(performance, section, section.Tune, 74, 0.68f, 0.96f, delegate(float t, float d, int n, float v)
					{
						Flute(m, t, d, n, v, -0.1f, 0.55f, recorder: false, seed + n);
					});
					break;
				case "B":
				case "B2":
					PlayTune(performance, section, section.Tune, 62, 0.6f, 0.98f, delegate(float t, float d, int n, float v)
					{
						Strings(m, t, d, n, v, 0.1f, 0.2f, 0.5f, 2200f, 0.55f, seed + n * 7);
					});
					break;
				case "A3":
					PlayTune(performance, section, section.Tune, 50, 0.72f, 0.98f, delegate(float t, float d, int n, float v)
					{
						Bowed(m, t, d, n, v, -0.1f, 0.5f, cello: true, seed + n * 3);
					});
					PlayTune(performance, section, section.Tune, 74, 0.55f, 0.96f, delegate(float t, float d, int n, float v)
					{
						Flute(m, t, d, n, v, 0.15f, 0.55f, recorder: false, seed + n);
					});
					break;
				}
			}
			return new MusicPiece
			{
				Name = "night_starlight",
				Stereo = Finish(m, 0.9f, 0.4f, 1.15f, 0.08f, loop: false, 0)
			};
		}

		private static MusicPiece PlayNocturne(Theme th, int seed)
		{
			Performance performance = Plan(th, seed, mayLift: false);
			Mix m = performance.Mix;
			float spb = performance.Spb;
			int[] array = null;
			foreach (Section section in performance.Sections)
			{
				for (int i = 0; i < section.Bars; i++)
				{
					Chord c = section.Chords[i];
					float num = performance.Seconds(section.Bar0 + i);
					int num2 = seed + section.Bar0 * 3 + i * 17;
					int[] array2 = Spread(section.Key, c, section.Key.Tonic - 5, ninth: true);
					int[] array3 = new int[8] { 0, 1, 2, 3, 4, 3, 2, 1 };
					for (int e = 0; e < 8; e++)
					{
						if (section.Kind == "outro" && (i == 1 || e > 4))
						{
							break;
						}
						int note = array2[Math.Min(array2.Length - 1, array3[e])];
						float at = num + (float)e * 0.5f * spb;
						int nsd = num2 + e;
						Echoes(0.75f * spb, 1, 0.3f, -0.25f + 0.06f * (float)array3[e], delegate(float off, float g, float pan)
						{
							EPiano(m, at + off, 0.9f * spb, note, ((e == 0) ? 0.42f : 0.32f) * g, pan, 0.55f, nsd);
						});
					}
					if (section.Kind != "A" && section.Kind != "intro")
					{
						array = Voice(section.Key, c, array, section.Key.Tonic + 17);
						for (int num3 = 0; num3 < array.Length; num3++)
						{
							Strings(m, num, 4f * spb, array[num3], 0.24f, -0.45f + (float)num3 * 0.3f, 1.2f, 1.2f, 1000f, 0.6f, num2 + num3);
						}
					}
				}
				switch (section.Kind)
				{
				case "A":
					PlayTune(performance, section, section.Tune, 67, 0.62f, 1f, delegate(float t, float d, int n, float v)
					{
						EPiano(m, t, d, n, v, 0.15f, 0.5f, seed + n);
					});
					break;
				case "A2":
					PlayTune(performance, section, section.Tune, 67, 0.62f, 1f, delegate(float t, float d, int n, float v)
					{
						EPiano(m, t, d, n, v, 0.15f, 0.5f, seed + n);
						Glass(m, t, d, n + 12, v * 0.35f, -0.1f, 0.05f, 0.6f, 0.6f, seed + n);
					});
					break;
				case "B":
				case "B2":
					PlayTune(performance, section, section.Tune, 50, 0.72f, 0.98f, delegate(float t, float d, int n, float v)
					{
						Bowed(m, t, d, n, v, 0.1f, 0.5f, cello: true, seed + n * 3);
					});
					break;
				case "A3":
					PlayTune(performance, section, section.Tune, 67, 0.62f, 1f, delegate(float t, float d, int n, float v)
					{
						EPiano(m, t, d, n, v, 0.15f, 0.5f, seed + n);
					});
					PlayTune(performance, section, section.Tune, 55, 0.55f, 0.98f, delegate(float t, float d, int n, float v)
					{
						Bowed(m, t, d, n, v, -0.15f, 0.5f, cello: true, seed + n * 5);
					});
					break;
				case "outro":
					EPiano(m, performance.Seconds(section.Bar0, 2f), 6f * spb, section.Key.Tonic + 24, 0.4f, 0f, 0.6f, seed + 9);
					break;
				}
			}
			return new MusicPiece
			{
				Name = "night_nocturne",
				Stereo = Finish(m, 0.88f, 0.4f, 1f, 0.085f, loop: false, 0)
			};
		}

		private static MusicPiece PlayJig(Theme th, int seed)
		{
			Performance performance = Plan(th, seed, mayLift: true);
			Mix m = performance.Mix;
			Rng rng = performance.Rng;
			float spb = performance.Spb;
			foreach (Section section in performance.Sections)
			{
				for (int i = 0; i < section.Bars; i++)
				{
					Chord c = section.Chords[i];
					float num = performance.Seconds(section.Bar0 + i);
					int num2 = seed + section.Bar0 * 13 + i * 7;
					if (i % 2 == 0 && section.Kind != "outro")
					{
						Bass(m, num, 12f * spb, section.Key.Tonic - 12, 0.32f, 0.2f, bowed: true);
						Bass(m, num, 12f * spb, section.Key.Tonic - 5, 0.16f, 0.2f, bowed: true);
					}
					Pluck(m, num, ChordNote(section.Key, c, 0, 0), 0.7f, -0.25f, 0.75f, 0.8f, 0.3f, num2);
					Pluck(m, num + 3f * spb, ChordNote(section.Key, c, 2, -1), 0.6f, -0.25f, 0.7f, 0.7f, 0.3f, num2 + 1);
					int[] array = new int[2] { 2, 5 };
					foreach (int num3 in array)
					{
						for (int k = 0; k < 3; k++)
						{
							Pluck(m, num + (float)num3 * spb + (float)k * 0.01f, ChordNote(section.Key, c, k, 1), 0.38f, -0.3f + (float)k * 0.06f, 0.8f, 0.5f, 0.3f, num2 + num3 * 3 + k);
						}
					}
					if (section.Kind != "intro" || i == 1)
					{
						for (int l = 0; l < 6; l++)
						{
							bool flag = l == 0 || l == 3;
							Drum(m, num + (float)l * spb, flag ? 110f : 160f, 0.35f, flag ? 0.14f : 0.05f, flag ? 0.4f : 0.9f, flag ? 1400f : 2400f, flag ? 0.55f : (0.22f + 0.06f * rng.Unit()), 0.15f, 0.22f, num2 + l * 5);
						}
					}
					if (i % 8 == 7 && section.Kind != "outro")
					{
						for (int n = 0; n < 3; n++)
						{
							Drum(m, num + (4f + (float)n / 1.5f) * spb, 170f, 0.3f, 0.05f, 1f, 2500f, 0.25f, 0.15f, 0.22f, num2 + 40 + n);
						}
					}
				}
				switch (section.Kind)
				{
				case "A":
					PlayTune(performance, section, section.Tune, 69, 0.75f, 0.9f, delegate(float t, float d, int num4, float v)
					{
						Bowed(m, t, d, num4, v, 0.15f, 0.3f, cello: false, seed + num4);
					});
					break;
				case "A2":
					PlayTune(performance, section, section.Tune, 76, 0.7f, 0.88f, delegate(float t, float d, int num4, float v)
					{
						Flute(m, t, d, num4, v, -0.15f, 0.3f, recorder: true, seed + num4);
					});
					PlayTune(performance, section, section.Tune, 69, 0.5f, 0.9f, delegate(float t, float d, int num4, float v)
					{
						Bowed(m, t, d, num4, v, 0.2f, 0.3f, cello: false, seed + num4 * 3);
					}, -2);
					break;
				case "B":
					PlayTune(performance, section, section.Tune, 69, 0.75f, 0.9f, delegate(float t, float d, int num4, float v)
					{
						Bowed(m, t, d, num4, v, 0.15f, 0.3f, cello: false, seed + num4);
					});
					break;
				case "B2":
					PlayTune(performance, section, section.Tune, 76, 0.7f, 0.88f, delegate(float t, float d, int num4, float v)
					{
						Flute(m, t, d, num4, v, -0.15f, 0.3f, recorder: true, seed + num4);
					});
					break;
				case "A3":
					PlayTune(performance, section, section.Tune, 69, 0.75f, 0.9f, delegate(float t, float d, int num4, float v)
					{
						Bowed(m, t, d, num4, v, 0.15f, 0.3f, cello: false, seed + num4);
					});
					PlayTune(performance, section, section.Tune, 81, 0.6f, 0.88f, delegate(float t, float d, int num4, float v)
					{
						Flute(m, t, d, num4, v, -0.2f, 0.3f, recorder: true, seed + num4 * 2);
					});
					break;
				case "outro":
					Drum(m, performance.Seconds(section.Bar0), 100f, 0.4f, 0.2f, 0.4f, 1300f, 0.7f, 0.15f, 0.3f, seed + 3);
					break;
				}
			}
			return new MusicPiece
			{
				Name = "town_jig",
				Stereo = Finish(m, 0.76f, 0.45f, 0.65f, 0.105f, loop: false, 0)
			};
		}

		private static MusicPiece PlayMarket(Theme th, int seed)
		{
			Performance performance = Plan(th, seed, mayLift: true);
			Mix m = performance.Mix;
			_ = performance.Rng;
			float spb = performance.Spb;
			foreach (Section section in performance.Sections)
			{
				for (int i = 0; i < section.Bars; i++)
				{
					Chord c = section.Chords[i];
					float num = performance.Seconds(section.Bar0 + i);
					int num2 = seed + section.Bar0 * 7 + i * 13;
					Pluck(m, num, ChordNote(section.Key, c, 0, 0), 0.7f, -0.2f, 0.7f, 0.9f, 0.3f, num2);
					Pluck(m, num + 2f * spb, ChordNote(section.Key, c, 2, -1), 0.6f, -0.2f, 0.7f, 0.8f, 0.3f, num2 + 1);
					for (int j = 0; j < 8; j++)
					{
						if (j % 4 != 0)
						{
							bool flag = j % 2 == 1;
							for (int k = 0; k < 3; k++)
							{
								int num3 = (flag ? (2 - k) : k);
								Pluck(m, num + (float)j * 0.5f * spb + (float)k * 0.012f, ChordNote(section.Key, c, num3, 1), flag ? 0.26f : 0.36f, -0.3f + (float)num3 * 0.06f, 0.8f, 0.5f, 0.3f, num2 + j * 3 + k);
							}
						}
					}
					if (!(section.Kind == "intro") || i != 0)
					{
						Drum(m, num, 120f, 0.4f, 0.16f, 0.3f, 1400f, 0.5f, 0.15f, 0.25f, num2 + 7);
						Drum(m, num + 2f * spb, 120f, 0.4f, 0.16f, 0.3f, 1400f, 0.42f, 0.15f, 0.25f, num2 + 8);
						for (int l = 1; l < 8; l += 2)
						{
							Jingle(m, num + (float)l * 0.5f * spb, (l % 4 == 3) ? 0.55f : 0.3f, 0.35f, 0.25f, num2 + l);
						}
						if (i % 4 == 3)
						{
							Drum(m, num + 3.5f * spb, 170f, 0.3f, 0.07f, 0.9f, 2200f, 0.35f, 0.15f, 0.25f, num2 + 9);
						}
					}
				}
				switch (section.Kind)
				{
				case "A":
					PlayTune(performance, section, section.Tune, 76, 0.72f, 0.88f, delegate(float t, float d, int n, float v)
					{
						Flute(m, t, d, n, v, 0.15f, 0.3f, recorder: true, seed + n);
					});
					break;
				case "A2":
					PlayTune(performance, section, section.Tune, 69, 0.72f, 0.9f, delegate(float t, float d, int n, float v)
					{
						Bowed(m, t, d, n, v, -0.1f, 0.3f, cello: false, seed + n);
					});
					PlayCounter(performance, section, performance.Key.Tonic + 31, performance.Key.Tonic + 43, 0.6f, 0.42f, delegate(float t, float d, int n, float v)
					{
						Flute(m, t, d, n, v, 0.3f, 0.35f, recorder: true, seed + n * 3);
					});
					break;
				case "B":
					PlayTune(performance, section, section.Tune, 67, 0.75f, 0.9f, delegate(float t, float d, int n, float v)
					{
						Reed(m, t, d, n, v, 0.1f, 0.3f, clarinet: false, seed + n);
					});
					break;
				case "B2":
					PlayTune(performance, section, section.Tune, 67, 0.72f, 0.9f, delegate(float t, float d, int n, float v)
					{
						Reed(m, t, d, n, v, 0.1f, 0.3f, clarinet: false, seed + n);
					});
					PlayTune(performance, section, section.Tune, 76, 0.55f, 0.88f, delegate(float t, float d, int n, float v)
					{
						Flute(m, t, d, n, v, -0.2f, 0.3f, recorder: true, seed + n * 2);
					}, 2);
					break;
				case "A3":
					PlayTune(performance, section, section.Tune, 76, 0.72f, 0.88f, delegate(float t, float d, int n, float v)
					{
						Flute(m, t, d, n, v, 0.15f, 0.3f, recorder: true, seed + n);
					});
					PlayTune(performance, section, section.Tune, 69, 0.6f, 0.9f, delegate(float t, float d, int n, float v)
					{
						Bowed(m, t, d, n, v, -0.15f, 0.3f, cello: false, seed + n * 3);
					});
					break;
				case "outro":
					Jingle(m, performance.Seconds(section.Bar0), 0.7f, 0.3f, 0.3f, seed + 2);
					break;
				}
			}
			return new MusicPiece
			{
				Name = "town_market",
				Stereo = Finish(m, 0.76f, 0.45f, 0.65f, 0.1f, loop: false, 0)
			};
		}

		private static MusicPiece PlayHearth(Theme th, int seed)
		{
			Performance performance = Plan(th, seed, mayLift: false);
			Mix m = performance.Mix;
			float spb = performance.Spb;
			foreach (Section section in performance.Sections)
			{
				for (int i = 0; i < section.Bars; i++)
				{
					Chord c = section.Chords[i];
					float num = performance.Seconds(section.Bar0 + i);
					int num2 = seed + section.Bar0 * 5 + i * 9;
					int[] array = Spread(section.Key, c, section.Key.Tonic, ninth: false);
					int[] array2 = new int[6] { 0, 1, 2, 3, 2, 1 };
					for (int j = 0; j < 6 && (!(section.Kind == "outro") || i != 1 || j <= 0); j++)
					{
						Pluck(m, num + (float)j * 0.5f * spb, array[Math.Min(array.Length - 1, array2[j])], (j == 0) ? 0.5f : 0.36f, -0.25f + 0.1f * (float)array2[j], 0.6f, 1.1f, 0.4f, num2 + j);
					}
					if (section.Kind != "A" && section.Kind != "intro")
					{
						Bass(m, num, 3f * spb, ChordNote(section.Key, c, 0, -1), 0.38f, 0.3f, bowed: true);
					}
					if (section.Kind != "A" && section.Kind != "intro" && section.Kind != "outro")
					{
						Drum(m, num, 110f, 0.4f, 0.16f, 0.25f, 1200f, 0.22f, 0.1f, 0.3f, num2 + 3);
					}
				}
				switch (section.Kind)
				{
				case "A":
					PlayTune(performance, section, section.Tune, 72, 0.7f, 0.95f, delegate(float t, float d, int n, float v)
					{
						Flute(m, t, d, n, v, 0.1f, 0.42f, recorder: false, seed + n);
					});
					break;
				case "A2":
					PlayTune(performance, section, section.Tune, 67, 0.72f, 0.94f, delegate(float t, float d, int n, float v)
					{
						Reed(m, t, d, n, v, 0.1f, 0.4f, clarinet: false, seed + n);
					});
					break;
				case "B":
				case "B2":
					PlayTune(performance, section, section.Tune, 55, 0.72f, 0.97f, delegate(float t, float d, int n, float v)
					{
						Bowed(m, t, d, n, v, 0.05f, 0.45f, cello: true, seed + n * 3);
					});
					break;
				case "A3":
					PlayTune(performance, section, section.Tune, 72, 0.7f, 0.95f, delegate(float t, float d, int n, float v)
					{
						Flute(m, t, d, n, v, 0.15f, 0.42f, recorder: false, seed + n);
					});
					PlayTune(performance, section, section.Tune, 55, 0.55f, 0.97f, delegate(float t, float d, int n, float v)
					{
						Bowed(m, t, d, n, v, -0.15f, 0.45f, cello: true, seed + n * 5);
					});
					break;
				}
			}
			return new MusicPiece
			{
				Name = "town_hearth",
				Stereo = Finish(m, 0.8f, 0.42f, 0.75f, 0.1f, loop: false, 0)
			};
		}

		private static MusicPiece Perform(Theme th, int seed)
		{
			switch (th.Name)
			{
			case "meadow":
				return PlayMeadow(th, seed);
			case "daydream":
				return PlayDaydream(th, seed);
			case "waltz":
				return PlayWaltz(th, seed);
			case "hymn":
				return PlayHymn(th, seed);
			case "moonlight":
				return PlayMoonlight(th, seed);
			case "lullaby":
				return PlayLullaby(th, seed);
			case "starlight":
				return PlayStarlight(th, seed);
			case "nocturne":
				return PlayNocturne(th, seed);
			case "jig":
				return PlayJig(th, seed);
			case "market":
				return PlayMarket(th, seed);
			case "hearth":
				return PlayHearth(th, seed);
			default:
				return null;
			}
		}

		public static string[] ThemeNames()
		{
			string[] array = new string[Themes.Length];
			for (int i = 0; i < Themes.Length; i++)
			{
				array[i] = Themes[i].Mood + "/" + Themes[i].Name;
			}
			return array;
		}

		public static MusicPiece PlayTheme(string name, int seed)
		{
			Theme[] themes = Themes;
			foreach (Theme theme in themes)
			{
				if (theme.Name == name)
				{
					return Perform(theme, seed);
				}
			}
			return null;
		}

		public static MusicPiece Calm(string mood, int seed)
		{
			List<string> list = new List<string>();
			Theme[] themes = Themes;
			foreach (Theme theme in themes)
			{
				if (theme.Mood == mood)
				{
					list.Add(theme.Name);
				}
			}
			list.Add("free");
			Rng rng = new Rng(seed * 29 + 11);
			string text;
			lock (PickLock)
			{
				LastPick.TryGetValue(mood, out var value);
				do
				{
					text = rng.Pick(list.ToArray());
				}
				while (text == value && list.Count > 1);
				LastPick[mood] = text;
			}
			if (text != "free")
			{
				MusicPiece musicPiece = PlayTheme(text, seed);
				if (musicPiece != null)
				{
					return musicPiece;
				}
			}
			if (!(mood == "night"))
			{
				if (mood == "town")
				{
					return Town(seed);
				}
				return Day(seed);
			}
			return Night(seed);
		}
	}
}
