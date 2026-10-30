using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using FMOD;
using FirstPerson.Core;

namespace FirstPerson
{
	internal sealed class GameClips : IDisposable
	{
		private FMOD.System _core;

		private Sound _archive;

		private readonly Fsb5 _fsb;

		private readonly string _bank;

		private bool _open;

		public static GameClips Open(string bankPath, out string error)
		{
			error = null;
			Fsb5 fsb;
			try
			{
				fsb = Fsb5.Read(bankPath);
			}
			catch (Exception ex)
			{
				error = Fail("reading " + bankPath + ": " + ex.Message);
				return null;
			}
			if (fsb.Offset < 0 || fsb.Count == 0)
			{
				error = Fail("no sound archive in " + bankPath);
				return null;
			}
			GameClips gameClips = new GameClips(bankPath, fsb);
			if (!gameClips.Start(out error))
			{
				gameClips.Dispose();
				return null;
			}
			return gameClips;
		}

		private GameClips(string bank, Fsb5 fsb)
		{
			_bank = bank;
			_fsb = fsb;
		}

		private bool Start(out string error)
		{
			error = null;
			RESULT rESULT = Factory.System_Create(out _core);
			if (rESULT != RESULT.OK)
			{
				error = Fail("FMOD system: " + rESULT);
				return false;
			}
			rESULT = _core.setOutput(OUTPUTTYPE.NOSOUND_NRT);
			if (rESULT == RESULT.OK)
			{
				rESULT = _core.init(4, INITFLAGS.NORMAL, IntPtr.Zero);
			}
			if (rESULT != RESULT.OK)
			{
				error = Fail("FMOD init: " + rESULT);
				return false;
			}
			CREATESOUNDEXINFO exinfo = new CREATESOUNDEXINFO
			{
				cbsize = Marshal.SizeOf(typeof(CREATESOUNDEXINFO)),
				fileoffset = (uint)_fsb.Offset
			};
			rESULT = _core.createSound(_bank, MODE.CREATESTREAM | MODE.OPENONLY, ref exinfo, out _archive);
			if (rESULT != RESULT.OK)
			{
				error = Fail("opening the archive: " + rESULT);
				return false;
			}
			_open = true;
			return true;
		}

		private static string Fail(string why)
		{
			return why;
		}

		public bool Has(string name)
		{
			return _fsb.Index.ContainsKey(name);
		}

		public float[] Mono(string name, out int rate)
		{
			rate = 0;
			if (!_open || !_fsb.Index.TryGetValue(name, out var value))
			{
				return null;
			}
			if (_archive.getSubSound(value, out var subsound) != RESULT.OK)
			{
				return null;
			}
			if (subsound.getFormat(out var _, out var format, out var channels, out var _) != RESULT.OK || channels <= 0)
			{
				return null;
			}
			subsound.getDefaults(out var frequency, out var _);
			rate = (int)frequency;
			if (subsound.getLength(out var length, TIMEUNIT.PCM) != RESULT.OK || length == 0 || length > 960000)
			{
				return null;
			}
			int num;
			switch (format)
			{
			default:
				num = 0;
				break;
			case SOUND_FORMAT.PCM16:
				num = 2;
				break;
			case SOUND_FORMAT.PCMFLOAT:
				num = 4;
				break;
			}
			int num2 = num;
			if (num2 == 0)
			{
				return null;
			}
			if (subsound.seekData(0u) != RESULT.OK)
			{
				return null;
			}
			int num3 = (int)length * channels * num2;
			byte[] array = new byte[num3];
			int num4 = 0;
			byte[] array2 = new byte[16384];
			while (num4 < num3)
			{
				uint read;
				RESULT num5 = subsound.readData(array2, out read);
				if (read != 0)
				{
					int num6 = Math.Min((int)read, num3 - num4);
					Buffer.BlockCopy(array2, 0, array, num4, num6);
					num4 += num6;
				}
				if (num5 != RESULT.OK || read == 0)
				{
					break;
				}
			}
			int num7 = num4 / (channels * num2);
			float[] array3 = new float[num7];
			for (int i = 0; i < num7; i++)
			{
				float num8 = 0f;
				for (int j = 0; j < channels; j++)
				{
					int startIndex = (i * channels + j) * num2;
					num8 += ((num2 == 4) ? BitConverter.ToSingle(array, startIndex) : ((float)BitConverter.ToInt16(array, startIndex) / 32768f));
				}
				array3[i] = num8 / (float)channels;
			}
			return array3;
		}

		public VoiceSet Kind(string kind, Action between = null)
		{
			string[] array = VoiceSets.Of(kind);
			if (array == null)
			{
				return null;
			}
			List<KeyValuePair<string, float[]>> list = new List<KeyValuePair<string, float[]>>();
			string[] array2 = array;
			foreach (string text in array2)
			{
				int rate;
				float[] array3 = Mono(text, out rate);
				between?.Invoke();
				if (array3 != null && array3.Length != 0)
				{
					if (rate != 48000)
					{
						array3 = ToRate(array3, rate);
					}
					list.Add(new KeyValuePair<string, float[]>(text, array3));
				}
			}
			if (list.Count == 0)
			{
				return null;
			}
			VoiceSet voiceSet = VoiceSet.Build(kind, list, between);
			if (voiceSet.Usable <= 0)
			{
				return null;
			}
			return voiceSet;
		}

		private static float[] ToRate(float[] s, int rate)
		{
			if (rate <= 0)
			{
				return s;
			}
			double num = (double)rate / 48000.0;
			int num2 = (int)((double)s.Length / num);
			float[] array = new float[num2];
			for (int i = 0; i < num2; i++)
			{
				double num3 = (double)i * num;
				int num4 = (int)num3;
				float num5 = (float)(num3 - (double)num4);
				array[i] = ((num4 + 1 < s.Length) ? (s[num4] + (s[num4 + 1] - s[num4]) * num5) : s[Math.Min(num4, s.Length - 1)]);
			}
			return array;
		}

		public void Dispose()
		{
			_open = false;
			try
			{
				if (_archive.hasHandle())
				{
					_archive.release();
				}
			}
			catch
			{
			}
			try
			{
				if (_core.hasHandle())
				{
					_core.release();
				}
			}
			catch
			{
			}
		}
	}
}
