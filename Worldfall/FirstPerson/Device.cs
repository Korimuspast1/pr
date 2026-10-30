using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FirstPerson
{
	internal static class Device
	{
		public enum Kind
		{
			Pc,
			Phone,
			EmulatedPhone
		}

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr WineGetVersion();

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void WineGetHostVersion(out IntPtr sysname, out IntPtr release);

		private static bool _detected;

		private static Kind _kind;

		private static Kind? _forced;

		public static bool TouchSeen;

		private static readonly Regex PhoneGpu = new Regex("adreno|mali|immortalis|powervr|xclipse|turnip|freedreno|maleoon|vivante", RegexOptions.IgnoreCase);

		private static readonly Regex PhoneCpu = new Regex("snapdragon|qualcomm|kryo|cortex|dimensity|mediatek|exynos|tensor|kirin|unisoc|box64|box86|fex", RegexOptions.IgnoreCase);

		private static readonly Regex PhoneEnv = new Regex("winlator|gamenative|gamehub|mobox|termux|box64|fex-?emu|/data/data/|/storage/emulated", RegexOptions.IgnoreCase);

		public static Kind Is
		{
			get
			{
				if (_forced.HasValue)
				{
					return _forced.Value;
				}
				if (!_detected)
				{
					Detect();
				}
				return _kind;
			}
		}

		public static bool Native => Is == Kind.Phone;

		public static bool Emulated => Is == Kind.EmulatedPhone;

		public static bool OnPhone => Is != Kind.Pc;

		public static float HudScale => (OnPhone ? 1.2f : 1f) * GameArt.UserScale;

		public static string Wine { get; private set; }

		public static string WineHost { get; private set; }

		public static string Summary { get; private set; } = "";

		internal static void ForceForTest(Kind? kind)
		{
			_forced = kind;
		}

		private static void Detect()
		{
			_detected = true;
			_kind = Kind.Pc;
			StringBuilder stringBuilder = new StringBuilder();
			try
			{
				RuntimePlatform platform = Application.platform;
				bool flag = Application.isMobilePlatform;
				try
				{
					flag |= Config.isMobile || Config.isAndroid || Config.isIos;
				}
				catch
				{
				}
				bool flag2 = SystemInfo.deviceType == DeviceType.Handheld;
				string text = SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceVendor;
				string text2 = SystemInfo.processorType ?? "";
				string text3 = EmulatorEnvironment();
				if (platform == RuntimePlatform.WindowsPlayer)
				{
					DetectWine();
				}
				bool flag3 = PhoneGpu.IsMatch(text);
				bool flag4 = PhoneCpu.IsMatch(text2);
				bool flag5 = text3 != null;
				bool flag6 = WineHost != null && WineHost.IndexOf("android", StringComparison.OrdinalIgnoreCase) >= 0;
				if (flag || flag2)
				{
					_kind = Kind.Phone;
				}
				else if (Wine != null && (flag6 || flag3 || flag4 || flag5))
				{
					_kind = Kind.EmulatedPhone;
				}
				stringBuilder.Append(platform);
				if (flag)
				{
					stringBuilder.Append(", mobile build");
				}
				if (flag2)
				{
					stringBuilder.Append(", handheld");
				}
				if (Wine != null)
				{
					stringBuilder.Append(", wine ").Append(Wine).Append((WineHost != null) ? (" on " + WineHost) : "");
				}
				stringBuilder.Append(", gpu '").Append(text).Append('\'')
					.Append(flag3 ? " (a phone's)" : "");
				stringBuilder.Append(", cpu '").Append(text2).Append('\'')
					.Append(flag4 ? " (a phone's)" : "");
				if (flag5)
				{
					stringBuilder.Append(", emulator ").Append(text3);
				}
				stringBuilder.Append(", screen ").Append(Screen.width).Append('x')
					.Append(Screen.height);
				if (Screen.dpi > 0f)
				{
					stringBuilder.Append(" @ ").Append(Mathf.RoundToInt(Screen.dpi)).Append(" dpi");
				}
				stringBuilder.Append(", touch ").Append(Input.touchSupported ? "yes" : "no").Append(", mouse ")
					.Append(Input.mousePresent ? "yes" : "no");
			}
			catch (Exception ex)
			{
				stringBuilder.Append("(detection failed: ").Append(ex.Message).Append(')');
			}
			string environmentVariable = Environment.GetEnvironmentVariable("FIRSTPERSON_DEVICE");
			if (!string.IsNullOrEmpty(environmentVariable))
			{
				switch (environmentVariable.Trim().ToLowerInvariant())
				{
				case "phone":
					_kind = Kind.Phone;
					break;
				case "emulated":
					_kind = Kind.EmulatedPhone;
					break;
				case "pc":
					_kind = Kind.Pc;
					break;
				}
				stringBuilder.Append(", FIRSTPERSON_DEVICE=").Append(environmentVariable);
			}
			Summary = stringBuilder.ToString();
			Log.Info("device: " + Describe(_kind) + " (" + Summary + ")");
		}

		public static string Describe(Kind k)
		{
			switch (k)
			{
			default:
				return "PC";
			case Kind.EmulatedPhone:
				return "PC game on a phone (emulator)";
			case Kind.Phone:
				return "phone or tablet";
			}
		}

		private static string EmulatorEnvironment()
		{
			try
			{
				foreach (DictionaryEntry environmentVariable in Environment.GetEnvironmentVariables())
				{
					string text = (environmentVariable.Key as string) ?? "";
					string input = (environmentVariable.Value as string) ?? "";
					if (text.StartsWith("BOX64", StringComparison.OrdinalIgnoreCase) || text.StartsWith("BOX86", StringComparison.OrdinalIgnoreCase) || text.StartsWith("FEX_", StringComparison.OrdinalIgnoreCase) || text.StartsWith("WINLATOR", StringComparison.OrdinalIgnoreCase))
					{
						return text;
					}
					if (PhoneEnv.IsMatch(input))
					{
						return text;
					}
				}
			}
			catch
			{
			}
			return null;
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		private static extern IntPtr GetModuleHandleW(string name);

		[DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
		private static extern IntPtr GetProcAddress(IntPtr module, string name);

		private static void DetectWine()
		{
			try
			{
				IntPtr moduleHandleW = GetModuleHandleW("ntdll.dll");
				if (moduleHandleW == IntPtr.Zero)
				{
					return;
				}
				IntPtr procAddress = GetProcAddress(moduleHandleW, "wine_get_version");
				if (!(procAddress == IntPtr.Zero))
				{
					Wine = Marshal.PtrToStringAnsi(Marshal.GetDelegateForFunctionPointer<WineGetVersion>(procAddress)()) ?? "?";
					IntPtr procAddress2 = GetProcAddress(moduleHandleW, "wine_get_host_version");
					if (procAddress2 != IntPtr.Zero)
					{
						Marshal.GetDelegateForFunctionPointer<WineGetHostVersion>(procAddress2)(out var sysname, out var release);
						WineHost = Marshal.PtrToStringAnsi(sysname) + " " + Marshal.PtrToStringAnsi(release);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Info("device: wine check: " + ex.Message);
			}
		}
	}
}
