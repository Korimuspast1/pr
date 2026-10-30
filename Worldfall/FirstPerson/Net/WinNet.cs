using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace FirstPerson.Net
{
	internal static class WinNet
	{
		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate int GetLong(IntPtr self, out int value);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate int GetEnabled(IntPtr self, int profile, out short value);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate uint Release(IntPtr self);

		public static volatile object PublicFirewalled;

		private static readonly Guid ClsidPolicy2 = new Guid("E2B3C97F-6AE1-41AC-817A-F6F92166D7DD");

		private static readonly Guid IidPolicy2 = new Guid("98325047-C671-4174-8D81-DEFCD3F03186");

		private const int ProfilePublic = 4;

		public static bool IsPublic
		{
			get
			{
				object publicFirewalled = PublicFirewalled;
				bool flag = default(bool);
				int num;
				if (publicFirewalled is bool)
				{
					flag = (bool)publicFirewalled;
					num = 1;
				}
				else
				{
					num = 0;
				}
				return (byte)((uint)num & (flag ? 1u : 0u)) != 0;
			}
		}

		[DllImport("ole32.dll")]
		private static extern int CoInitializeEx(IntPtr reserved, int coinit);

		[DllImport("ole32.dll")]
		private static extern void CoUninitialize();

		[DllImport("ole32.dll")]
		private static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, int context, ref Guid iid, out IntPtr obj);

		public static void Check()
		{
			PublicFirewalled = null;
			if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
			{
				Thread thread = new Thread(Run);
				thread.IsBackground = true;
				thread.Name = "net-winnet";
				thread.Start();
			}
		}

		internal static void Run()
		{
			bool flag = false;
			IntPtr obj = IntPtr.Zero;
			try
			{
				flag = CoInitializeEx(IntPtr.Zero, 0) >= 0;
				Guid clsid = ClsidPolicy2;
				Guid iid = IidPolicy2;
				int num = CoCreateInstance(ref clsid, IntPtr.Zero, 1, ref iid, out obj);
				if (num < 0 || obj == IntPtr.Zero)
				{
					Log.Info("NET network type unknown (CoCreateInstance 0x" + num.ToString("X8") + ")");
					return;
				}
				IntPtr ptr = Marshal.ReadIntPtr(obj);
				if (((GetLong)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(ptr, 7 * IntPtr.Size), typeof(GetLong)))(obj, out var value) < 0)
				{
					Log.Info("NET network type unknown");
					return;
				}
				bool flag2 = (value & 4) != 0;
				bool flag3 = false;
				if (flag2)
				{
					flag3 = ((GetEnabled)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(ptr, 8 * IntPtr.Size), typeof(GetEnabled)))(obj, 4, out var value2) >= 0 && value2 != 0;
				}
				PublicFirewalled = flag2 && flag3;
				Log.Info("NET this network: " + (flag2 ? "Public" : "Private") + " (profiles " + value + ")" + (flag2 ? (", firewall " + (flag3 ? "on" : "off")) : ""));
			}
			catch (Exception ex)
			{
				Log.Info("NET network type unknown: " + ex.GetType().Name + " " + ex.Message);
			}
			finally
			{
				if (obj != IntPtr.Zero)
				{
					try
					{
						((Release)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj), 2 * IntPtr.Size), typeof(Release)))(obj);
					}
					catch
					{
					}
				}
				if (flag)
				{
					try
					{
						CoUninitialize();
					}
					catch
					{
					}
				}
			}
		}
	}
}
