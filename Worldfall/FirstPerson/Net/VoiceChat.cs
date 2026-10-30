using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using AOT;
using FMOD;
using FMODUnity;
using Steamworks;
using UnityEngine;

namespace FirstPerson.Net
{
	internal static class VoiceChat
	{
		private sealed class Voice
		{
			public Sound Sound;

			public Channel Channel;

			public readonly short[] Ring = new short[48000];

			public int Read;

			public int Write;

			public int Count;

			public float LastAt;

			public void Release()
			{
				try
				{
					if (Channel.hasHandle())
					{
						Channel.stop();
					}
				}
				catch
				{
				}
				try
				{
					if (Sound.hasHandle())
					{
						lock (_byHandle)
						{
							_byHandle.Remove(Sound.handle);
						}
						Sound.release();
					}
				}
				catch
				{
				}
			}
		}

		public const float Reach = 40f;

		private const int Rate = 24000;

		public static bool Talking;

		public static readonly Dictionary<int, float> Heard = new Dictionary<int, float>();

		private static bool _recording;

		private static readonly NetWriter W = new NetWriter(4096);

		private static readonly MemoryStream _pcm = new MemoryStream(65536);

		private static readonly List<KeyValuePair<int, Vector2>> _gods = new List<KeyValuePair<int, Vector2>>();

		private static readonly Dictionary<int, Voice> _voices = new Dictionary<int, Voice>();

		private static readonly Dictionary<IntPtr, Voice> _byHandle = new Dictionary<IntPtr, Voice>();

		private static SOUND_PCMREAD_CALLBACK _read;

		public static void Tick(NetSession s)
		{
			MpPrefs current = MpPrefs.Current;
			bool flag = false;
			if (current.Voice && SteamLobbies.Available && !MpUi.Typing)
			{
				bool flag2 = false;
				try
				{
					flag2 = PlayInput.GetKey((KeyCode)current.TalkKey);
				}
				catch
				{
				}
				flag = current.OpenMic || flag2;
			}
			Talking = flag;
			try
			{
				if (flag != _recording)
				{
					_recording = flag;
					Record(flag);
				}
				if (_recording)
				{
					SendRecorded(s);
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET voice: " + ex.Message);
				_recording = false;
			}
			Mix(s);
		}

		public static void Stop()
		{
			try
			{
				if (_recording)
				{
					Record(on: false);
				}
			}
			catch
			{
			}
			_recording = false;
			Talking = false;
			foreach (Voice value in _voices.Values)
			{
				value.Release();
			}
			_voices.Clear();
		}

		private static void Record(bool on)
		{
			SteamUser.VoiceRecord = on;
		}

		private static void SendRecorded(NetSession s)
		{
			if (!SteamUser.HasVoiceData)
			{
				return;
			}
			byte[] array = SteamUser.ReadVoiceDataBytes();
			if (array != null && array.Length != 0)
			{
				if (s.IsHost)
				{
					Relay(s, s.Me, array, 0, array.Length);
					return;
				}
				W.Reset();
				W.VarU(55uL);
				W.Bytes(array, 0, array.Length);
				s.ToHost(W, (array.Length < 1000) ? Delivery.Unreliable : Delivery.Reliable);
			}
		}

		public static void Relay(NetSession s, NetPlayer from, byte[] data, int off, int len)
		{
			Vector2 vector = WhereIs(from);
			foreach (NetPlayer value in s.Players.Values)
			{
				if (value != from && (value.Link != null || value.Host) && !((WhereIs(value) - vector).sqrMagnitude > 1920.0001f))
				{
					if (value.Host)
					{
						Heard[from.Id] = Time.realtimeSinceStartup;
						Play(from.Id, data, off, len);
						continue;
					}
					W.Reset();
					W.VarU(62uL);
					W.VarU((ulong)from.Id);
					W.Bytes(data, off, len);
					value.Link.Send(W, (len < 1000) ? Delivery.Unreliable : Delivery.Reliable);
				}
			}
		}

		public static void Received(int from, byte[] data)
		{
			Heard[from] = Time.realtimeSinceStartup;
			Play(from, data, 0, data.Length);
		}

		private static Vector2 WhereIs(NetPlayer p)
		{
			try
			{
				if (p.Body != 0L)
				{
					Actor actor = World.world.units.get(p.Body);
					if (actor != null)
					{
						return actor.current_position;
					}
				}
				if (p == NetSession.Current?.Me)
				{
					Camera main = Camera.main;
					if (main != null)
					{
						return main.transform.position;
					}
				}
				WorldSync.GodPlayers(_gods);
				foreach (KeyValuePair<int, Vector2> god in _gods)
				{
					if (god.Key == p.Id)
					{
						return god.Value;
					}
				}
			}
			catch
			{
			}
			return new Vector2(-9999f, -9999f);
		}

		private static void Play(int from, byte[] data, int off, int len)
		{
			if (!MpPrefs.Current.Voice || !SteamLobbies.Available)
			{
				return;
			}
			try
			{
				_pcm.SetLength(0L);
				Decompress(data, off, len, _pcm);
				Voice voice = VoiceOf(from);
				if (voice == null)
				{
					return;
				}
				byte[] buffer = _pcm.GetBuffer();
				int num = (int)_pcm.Length / 2;
				lock (voice)
				{
					for (int i = 0; i < num; i++)
					{
						if (voice.Count >= voice.Ring.Length)
						{
							voice.Read = (voice.Read + 1) % voice.Ring.Length;
							voice.Count--;
						}
						voice.Ring[voice.Write] = (short)(buffer[i * 2] | (buffer[i * 2 + 1] << 8));
						voice.Write = (voice.Write + 1) % voice.Ring.Length;
						voice.Count++;
					}
				}
				voice.LastAt = Time.realtimeSinceStartup;
			}
			catch (Exception ex)
			{
				Log.Error("NET playing a voice: " + ex.Message);
			}
		}

		private static void Decompress(byte[] data, int off, int len, MemoryStream into)
		{
			SteamUser.SampleRate = 24000u;
			SteamUser.DecompressVoice((off == 0 && len == data.Length) ? data : Sub(data, off, len), into);
		}

		private static byte[] Sub(byte[] b, int off, int len)
		{
			byte[] array = new byte[len];
			Buffer.BlockCopy(b, off, array, 0, len);
			return array;
		}

		private static Voice VoiceOf(int from)
		{
			if (_voices.TryGetValue(from, out var value))
			{
				return value;
			}
			if (_read == null)
			{
				_read = ReadPcm;
			}
			CREATESOUNDEXINFO exinfo = new CREATESOUNDEXINFO
			{
				cbsize = Marshal.SizeOf(typeof(CREATESOUNDEXINFO)),
				numchannels = 1,
				defaultfrequency = 24000,
				format = SOUND_FORMAT.PCM16,
				length = 96000u,
				decodebuffersize = 960u,
				pcmreadcallback = _read
			};
			MODE mode = MODE.LOOP_NORMAL | MODE._2D | MODE.CREATESTREAM | MODE.OPENUSER;
			if (RuntimeManager.CoreSystem.createSound(IntPtr.Zero, mode, ref exinfo, out var sound) != RESULT.OK)
			{
				return null;
			}
			value = new Voice
			{
				Sound = sound
			};
			lock (_byHandle)
			{
				_byHandle[sound.handle] = value;
			}
			RuntimeManager.CoreSystem.playSound(sound, default(ChannelGroup), paused: false, out value.Channel);
			_voices[from] = value;
			return value;
		}

		[MonoPInvokeCallback(typeof(SOUND_PCMREAD_CALLBACK))]
		private static RESULT ReadPcm(IntPtr sound, IntPtr data, uint len)
		{
			Voice value;
			lock (_byHandle)
			{
				_byHandle.TryGetValue(sound, out value);
			}
			int num = (int)(len / 2);
			short[] array = new short[num];
			if (value != null)
			{
				lock (value)
				{
					int num2 = Math.Min(num, value.Count);
					for (int i = 0; i < num2; i++)
					{
						array[i] = value.Ring[value.Read];
						value.Read = (value.Read + 1) % value.Ring.Length;
					}
					value.Count -= num2;
				}
			}
			Marshal.Copy(array, 0, data, num);
			return RESULT.OK;
		}

		private static void Mix(NetSession s)
		{
			if (_voices.Count == 0)
			{
				return;
			}
			Vector2 vector = WhereIs(s.Me);
			float f = 0f;
			bool flag = false;
			try
			{
				WorldBoxMod instance = WorldBoxMod.Instance;
				if (instance != null && instance.IsFirstPerson)
				{
					f = PossessionHooks.Yaw;
					flag = true;
				}
			}
			catch
			{
			}
			float voiceVolume = MpPrefs.Current.VoiceVolume;
			List<int> list = null;
			foreach (KeyValuePair<int, Voice> voice in _voices)
			{
				Voice value = voice.Value;
				if (Time.realtimeSinceStartup - value.LastAt > 30f)
				{
					(list ?? (list = new List<int>())).Add(voice.Key);
					continue;
				}
				NetPlayer value2 = null;
				s.Players.TryGetValue(voice.Key, out value2);
				Vector2 vector2 = ((value2 != null) ? WhereIs(value2) : vector) - vector;
				float magnitude = vector2.magnitude;
				float num = Mathf.Clamp01(1f - magnitude / 40f);
				num = num * num * voiceVolume;
				float pan = 0f;
				if (flag && magnitude > 0.5f)
				{
					pan = Mathf.Clamp((Mathf.Sin(f) * vector2.x - Mathf.Cos(f) * vector2.y) / magnitude, -1f, 1f) * 0.8f;
				}
				try
				{
					value.Channel.setVolume(num);
					value.Channel.setPan(pan);
				}
				catch
				{
				}
			}
			if (list == null)
			{
				return;
			}
			foreach (int item in list)
			{
				_voices[item].Release();
				_voices.Remove(item);
			}
		}
	}
}
