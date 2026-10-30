using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FirstPerson
{
	internal static class Detour
	{
		public enum Kind
		{
			Unsupported,
			WindowsX64,
			MacX64,
			MacArm64,
			LinuxX64,
			LinuxArm64
		}

		public sealed class Handle
		{
			public IntPtr Address;

			public byte[] Original;

			public IDisposable Hook;
		}

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void CacheFlush(IntPtr first, IntPtr last);

		private static string _system = "?";

		private static string _machine = "?";

		public static readonly Kind Platform = Detect();

		public static Func<MethodBase, MethodBase, IDisposable> Hooker;

		public static string HookerName;

		private static readonly List<IDisposable> _hooks = new List<IDisposable>();

		private static readonly Dictionary<string, string> _replaced = new Dictionary<string, string>();

		private static CacheFlush _flush;

		private static bool _held;

		private const uint PageExecuteReadWrite = 64u;

		private const int ProtRead = 1;

		private const int ProtWrite = 2;

		private const int ProtExec = 4;

		private const int MapPrivate = 2;

		private const int MapAnonymous = 32;

		private const string LibSystem = "/usr/lib/libSystem.B.dylib";

		private static bool IsArm64
		{
			get
			{
				if (Platform != Kind.MacArm64)
				{
					return Platform == Kind.LinuxArm64;
				}
				return true;
			}
		}

		public static string ReplacedName(string call)
		{
			lock (_replaced)
			{
				string value;
				return (call != null && _replaced.TryGetValue(call.Replace('/', '+'), out value)) ? value : null;
			}
		}

		public static Handle Apply(MethodBase original, MethodBase replacement)
		{
			if (original == null)
			{
				throw new ArgumentNullException("original");
			}
			if (replacement == null)
			{
				throw new ArgumentNullException("replacement");
			}
			try
			{
				lock (_replaced)
				{
					_replaced[replacement.DeclaringType.FullName + "." + replacement.Name] = original.DeclaringType.Name + "." + original.Name;
				}
			}
			catch
			{
			}
			if (Hooker != null)
			{
				IDisposable disposable = Hooker(original, replacement);
				lock (_hooks)
				{
					_hooks.Add(disposable);
				}
				return new Handle
				{
					Hook = disposable
				};
			}
			if (Platform == Kind.Unsupported)
			{
				throw new PlatformNotSupportedException("Detours need a 64-bit Windows, macOS, Linux or Android process (" + Describe() + ")");
			}
			if (Platform == Kind.MacArm64)
			{
				throw new PlatformNotSupportedException("Apple Silicon: install the NeoModLoader build of Worldfall for first-person movement (" + Describe() + ")");
			}
			IntPtr intPtr = NativeStart(original);
			IntPtr to = NativeStart(replacement);
			byte[] array = (IsArm64 ? Arm64Jump(to) : X64Jump(to));
			byte[] array2 = new byte[array.Length];
			Marshal.Copy(intPtr, array2, 0, array2.Length);
			Write(intPtr, array);
			return new Handle
			{
				Address = intPtr,
				Original = array2
			};
		}

		public static void Undo(Handle handle)
		{
			if (handle == null)
			{
				return;
			}
			if (handle.Hook != null)
			{
				handle.Hook.Dispose();
				lock (_hooks)
				{
					_hooks.Remove(handle.Hook);
					return;
				}
			}
			Write(handle.Address, handle.Original);
		}

		public static IntPtr Target(MethodBase method)
		{
			return NativeStart(method);
		}

		private static byte[] X64Jump(IntPtr to)
		{
			byte[] array = new byte[12]
			{
				72, 184, 0, 0, 0, 0, 0, 0, 0, 0,
				0, 0
			};
			BitConverter.GetBytes(to.ToInt64()).CopyTo(array, 2);
			array[10] = byte.MaxValue;
			array[11] = 224;
			return array;
		}

		private static byte[] Arm64Jump(IntPtr to)
		{
			byte[] array = new byte[16];
			BitConverter.GetBytes(1476395088u).CopyTo(array, 0);
			BitConverter.GetBytes(3592356352u).CopyTo(array, 4);
			BitConverter.GetBytes(to.ToInt64()).CopyTo(array, 8);
			return array;
		}

		private static IntPtr NativeStart(MethodBase method)
		{
			RuntimeHelpers.PrepareMethod(method.MethodHandle);
			IntPtr intPtr = method.MethodHandle.GetFunctionPointer();
			for (int i = 0; i < 4; i++)
			{
				if (IsArm64)
				{
					uint num = (uint)Marshal.ReadInt32(intPtr);
					if ((num & 0xFC000000u) == 335544320)
					{
						int num2 = (int)(num << 6) >> 6;
						intPtr = new IntPtr(intPtr.ToInt64() + (long)num2 * 4L);
						continue;
					}
					if ((num & 0xFF00001Eu) != 1476395024 || (Marshal.ReadInt32(intPtr, 4) & -993) != -702611456)
					{
						break;
					}
					int num3 = (int)(((num >> 5) & 0x7FFFF) << 13) >> 13;
					intPtr = Marshal.ReadIntPtr(new IntPtr(intPtr.ToInt64() + (long)num3 * 4L));
					continue;
				}
				switch (Marshal.ReadByte(intPtr))
				{
				case 233:
					intPtr = new IntPtr(intPtr.ToInt64() + 5 + Marshal.ReadInt32(intPtr, 1));
					continue;
				case byte.MaxValue:
					if (Marshal.ReadByte(intPtr, 1) == 37)
					{
						intPtr = Marshal.ReadIntPtr(new IntPtr(intPtr.ToInt64() + 6 + Marshal.ReadInt32(intPtr, 2)));
						continue;
					}
					break;
				}
				break;
			}
			return intPtr;
		}

		private static void Write(IntPtr address, byte[] bytes)
		{
			switch (Platform)
			{
			case Kind.WindowsX64:
			{
				if (!VirtualProtect(address, (UIntPtr)(ulong)bytes.Length, 64u, out var oldProtect))
				{
					throw new InvalidOperationException("VirtualProtect failed: " + Marshal.GetLastWin32Error());
				}
				Marshal.Copy(bytes, 0, address, bytes.Length);
				VirtualProtect(address, (UIntPtr)(ulong)bytes.Length, oldProtect, out var _);
				FlushInstructionCache(GetCurrentProcess(), address, (UIntPtr)(ulong)bytes.Length);
				break;
			}
			case Kind.MacX64:
			case Kind.LinuxX64:
			{
				long num = PageSize();
				long num2 = address.ToInt64() & ~(num - 1);
				UIntPtr length = (UIntPtr)(ulong)(address.ToInt64() + bytes.Length - num2);
				if (((Platform == Kind.MacX64) ? MacProtect(new IntPtr(num2), length, 7) : LinuxProtect(new IntPtr(num2), length, 7)) != 0)
				{
					throw new InvalidOperationException("mprotect failed: " + Marshal.GetLastWin32Error());
				}
				Marshal.Copy(bytes, 0, address, bytes.Length);
				break;
			}
			case Kind.MacArm64:
				MacJitWriteProtect(0);
				try
				{
					Marshal.Copy(bytes, 0, address, bytes.Length);
				}
				finally
				{
					MacJitWriteProtect(1);
				}
				MacFlushCache(address, (UIntPtr)(ulong)bytes.Length);
				break;
			case Kind.LinuxArm64:
				LinuxUnprotect(address, bytes.Length);
				Marshal.Copy(bytes, 0, address, bytes.Length);
				Arm64FlushCache(address, bytes.Length);
				break;
			default:
				throw new PlatformNotSupportedException(Describe());
			}
		}

		private static Kind Detect()
		{
			try
			{
				if (IntPtr.Size != 8)
				{
					return Kind.Unsupported;
				}
				if (Environment.OSVersion.Platform == PlatformID.Win32NT)
				{
					return Kind.WindowsX64;
				}
				Uname(out var system, out var machine);
				bool flag = system == "Darwin";
				if (machine == "x86_64" || machine == "amd64")
				{
					return flag ? Kind.MacX64 : Kind.LinuxX64;
				}
				if (flag && (machine == "arm64" || machine.StartsWith("arm64", StringComparison.Ordinal)))
				{
					return Kind.MacArm64;
				}
				if (!flag && (machine == "aarch64" || machine == "arm64" || machine.StartsWith("armv8", StringComparison.Ordinal)))
				{
					return Kind.LinuxArm64;
				}
			}
			catch
			{
			}
			return Kind.Unsupported;
		}

		private static void Uname(out string system, out string machine)
		{
			IntPtr intPtr = Marshal.AllocHGlobal(4096);
			try
			{
				for (int i = 0; i < 4096; i++)
				{
					Marshal.WriteByte(intPtr, i, 0);
				}
				int num;
				try
				{
					num = MacUname(intPtr);
				}
				catch (DllNotFoundException)
				{
					num = LinuxUname(intPtr);
				}
				catch (EntryPointNotFoundException)
				{
					num = LinuxUname(intPtr);
				}
				if (num != 0)
				{
					throw new InvalidOperationException("uname failed");
				}
				system = Marshal.PtrToStringAnsi(intPtr) ?? "";
				int num2 = ((system == "Darwin") ? 256 : 65);
				machine = Marshal.PtrToStringAnsi(new IntPtr(intPtr.ToInt64() + num2 * 4)) ?? "";
				_system = system;
				_machine = machine;
			}
			finally
			{
				Marshal.FreeHGlobal(intPtr);
			}
		}

		public static string Describe()
		{
			return ((Hooker != null) ? (HookerName + " on ") : "") + Platform.ToString() + " (" + Environment.OSVersion.Platform.ToString() + ", " + _system + " " + _machine + ", " + IntPtr.Size * 8 + "-bit)";
		}

		private static long PageSize()
		{
			long num = 0L;
			try
			{
				num = ((Platform == Kind.MacX64) ? MacPageSize() : LinuxPageSize());
			}
			catch
			{
			}
			if (num <= 0)
			{
				return 4096L;
			}
			return num;
		}

		private static void LinuxUnprotect(IntPtr address, int length)
		{
			int num = 0;
			long[] obj = new long[4] { 0L, 4096L, 16384L, 65536L };
			obj[0] = PageSize();
			long[] array = obj;
			foreach (long num2 in array)
			{
				long num3 = address.ToInt64() & ~(num2 - 1);
				long num4 = address.ToInt64() + length;
				if (LinuxProtect(new IntPtr(num3), (UIntPtr)(ulong)(num4 - num3), 7) == 0)
				{
					return;
				}
				num = Marshal.GetLastWin32Error();
			}
			throw new InvalidOperationException("mprotect failed: " + num);
		}

		private static void Arm64FlushCache(IntPtr address, int length)
		{
			if (_flush == null)
			{
				uint[] array = new uint[8] { 3574299424u, 3574299425u, 3573758879u, 3574297888u, 3574297889u, 3573758879u, 3573759967u, 3596551104u };
				UIntPtr length2 = (UIntPtr)(ulong)PageSize();
				IntPtr intPtr = LinuxMap(IntPtr.Zero, length2, 3, 34, -1, 0L);
				if (intPtr == new IntPtr(-1))
				{
					throw new InvalidOperationException("mmap failed: " + Marshal.GetLastWin32Error());
				}
				for (int i = 0; i < array.Length; i++)
				{
					Marshal.WriteInt32(intPtr, i * 4, (int)array[i]);
				}
				if (LinuxProtect(intPtr, length2, 5) != 0)
				{
					throw new InvalidOperationException("mprotect (cache flush) failed: " + Marshal.GetLastWin32Error());
				}
				_flush = (CacheFlush)Marshal.GetDelegateForFunctionPointer(intPtr, typeof(CacheFlush));
			}
			_flush(address, new IntPtr(address.ToInt64() + length - 1));
		}

		public static bool Begin()
		{
			if (Platform == Kind.WindowsX64)
			{
				return true;
			}
			try
			{
				string text = typeof(Detour).Module.ModuleVersionId.ToString();
				string text2 = MarkerPath();
				if (File.Exists(text2) && File.ReadAllText(text2).Trim() == text)
				{
					Log.Error("input hooks skipped: the last attempt on this platform (" + Describe() + ") crashed the game. Delete " + text2 + " to try again.");
					return false;
				}
				File.WriteAllText(text2, text);
				_held = true;
				Application.quitting += End;
			}
			catch (Exception ex)
			{
				Log.Error("input hook guard: " + ex.Message);
			}
			return true;
		}

		public static void End()
		{
			if (Platform == Kind.WindowsX64 || !_held)
			{
				return;
			}
			_held = false;
			try
			{
				string path = MarkerPath();
				if (File.Exists(path))
				{
					File.Delete(path);
				}
			}
			catch
			{
			}
		}

		private static string MarkerPath()
		{
			return Path.Combine(Log.Folder, "input_hooks_pending.txt");
		}

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint newProtect, out uint oldProtect);

		[DllImport("kernel32.dll")]
		private static extern IntPtr GetCurrentProcess();

		[DllImport("kernel32.dll")]
		private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);

		[DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "mprotect", SetLastError = true)]
		private static extern int MacProtect(IntPtr address, UIntPtr length, int prot);

		[DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "getpagesize")]
		private static extern int MacPageSize();

		[DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "uname")]
		private static extern int MacUname(IntPtr buf);

		[DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "pthread_jit_write_protect_np")]
		private static extern void MacJitWriteProtect(int enabled);

		[DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "sys_icache_invalidate")]
		private static extern void MacFlushCache(IntPtr start, UIntPtr length);

		[DllImport("libc", EntryPoint = "mprotect", SetLastError = true)]
		private static extern int LinuxProtect(IntPtr address, UIntPtr length, int prot);

		[DllImport("libc", EntryPoint = "getpagesize")]
		private static extern int LinuxPageSize();

		[DllImport("libc", EntryPoint = "mmap", SetLastError = true)]
		private static extern IntPtr LinuxMap(IntPtr address, UIntPtr length, int prot, int flags, int fd, long offset);

		[DllImport("libc", EntryPoint = "uname")]
		private static extern int LinuxUname(IntPtr buf);
	}
}
