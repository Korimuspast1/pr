using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson.Gpu
{
	internal static class GpuShaders
	{
		private static AssetBundle _bundle;

		private static readonly Dictionary<string, Shader> _shaders = new Dictionary<string, Shader>();

		private static bool _tried;

		private static string _devPath;

		private static DateTime _devStamp;

		private static float _nextCheck;

		private static bool _staleLogged;

		public static string Error { get; private set; }

		public static int Version { get; private set; }

		public static bool Ready
		{
			get
			{
				Ensure();
				if (Error == null)
				{
					return _shaders.Count > 0;
				}
				return false;
			}
		}

		public static Shader Get(string name)
		{
			Ensure();
			if (!_shaders.TryGetValue(name, out var value))
			{
				return null;
			}
			return value;
		}

		public static void Ensure()
		{
			if (!_tried)
			{
				_tried = true;
				_devPath = Path.Combine(Log.Folder, "gpu.bundle");
				Load();
			}
			else
			{
				if (Time.unscaledTime < _nextCheck)
				{
					return;
				}
				_nextCheck = Time.unscaledTime + 1f;
				try
				{
					if (File.Exists(_devPath) && File.GetLastWriteTimeUtc(_devPath) != _devStamp)
					{
						Load();
					}
				}
				catch (Exception)
				{
				}
			}
		}

		private static string ResourceName()
		{
			switch (Application.platform)
			{
			case RuntimePlatform.OSXEditor:
			case RuntimePlatform.OSXPlayer:
				return "FirstPerson.gpu.osx.bundle";
			case RuntimePlatform.Android:
				return "FirstPerson.gpu.android.bundle";
			case RuntimePlatform.LinuxPlayer:
			case RuntimePlatform.LinuxEditor:
				return "FirstPerson.gpu.linux.bundle";
			default:
				return "FirstPerson.gpu.bundle";
			}
		}

		private static void Load()
		{
			if (CrashGuard.Suspect("shaders"))
			{
				Error = "off: loading the shaders crashed the game last time";
				Log.Error("gpu: " + Error);
				return;
			}
			CrashGuard.Enter("shaders");
			try
			{
				LoadBundle();
			}
			finally
			{
				CrashGuard.Leave("shaders");
			}
		}

		private static void LoadBundle()
		{
			try
			{
				byte[] array = null;
				string text = "embedded";
				if (File.Exists(_devPath))
				{
					_devStamp = File.GetLastWriteTimeUtc(_devPath);
					if (_devStamp.Ticks >= 639280166598112319L)
					{
						array = File.ReadAllBytes(_devPath);
						text = _devPath;
					}
					else if (!_staleLogged)
					{
						_staleLogged = true;
						Log.Info("gpu: " + _devPath + " is older than this build, so the embedded shaders are used (delete it, or rebuild it to use it)");
					}
				}
				if (array == null)
				{
					string text2 = ResourceName();
					text = "embedded " + text2;
					using (Stream stream = typeof(GpuShaders).Assembly.GetManifestResourceStream(text2))
					{
						if (stream == null)
						{
							Error = "no shader bundle in the mod for " + Application.platform;
							Log.Error("gpu: " + Error);
							return;
						}
						array = new byte[stream.Length];
						int num;
						for (int i = 0; i < array.Length; i += num)
						{
							num = stream.Read(array, i, array.Length - i);
							if (num <= 0)
							{
								break;
							}
						}
					}
				}
				if (_bundle != null)
				{
					_bundle.Unload(unloadAllLoadedObjects: true);
				}
				_shaders.Clear();
				_bundle = AssetBundle.LoadFromMemory(array);
				if (_bundle == null)
				{
					Error = "the shader bundle did not load (" + text + ")";
					Log.Error("gpu: " + Error);
					return;
				}
				List<string> list = new List<string>();
				string error = null;
				Shader[] array2 = _bundle.LoadAllAssets<Shader>();
				foreach (Shader shader in array2)
				{
					_shaders[shader.name] = shader;
					list.Add(shader.name + (shader.isSupported ? "" : " (NOT SUPPORTED)"));
					if (!shader.isSupported)
					{
						error = "shader " + shader.name + " is not supported on this graphics card";
					}
				}
				if (_shaders.Count == 0)
				{
					error = "the shader bundle holds no shaders";
				}
				Error = error;
				Version++;
				Log.Info("gpu: shaders v" + Version + " from " + text + ": " + string.Join(", ", list.ToArray()) + ((Error != null) ? (" ERROR " + Error) : ""));
			}
			catch (Exception ex)
			{
				Error = "shader bundle failed: " + ex.Message;
				Log.Error("gpu: " + Error);
			}
		}
	}
}
