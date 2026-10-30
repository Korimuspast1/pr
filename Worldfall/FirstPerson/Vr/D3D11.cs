using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace FirstPerson.Vr
{
	internal static class D3D11
	{
		public struct Texture2DDesc
		{
			public uint Width;

			public uint Height;

			public uint MipLevels;

			public uint ArraySize;

			public int Format;

			public uint SampleCount;

			public uint SampleQuality;

			public int Usage;

			public uint BindFlags;

			public uint CpuAccessFlags;

			public uint MiscFlags;
		}

		public struct Box
		{
			public uint Left;

			public uint Top;

			public uint Front;

			public uint Right;

			public uint Bottom;

			public uint Back;
		}

		public struct SubresourceData
		{
			public unsafe void* SysMem;

			public uint SysMemPitch;

			public uint SysMemSlicePitch;
		}

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate uint ReleaseFn(IntPtr self);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private unsafe delegate void GetDeviceFn(IntPtr self, IntPtr* device);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private unsafe delegate void GetImmediateContextFn(IntPtr self, IntPtr* context);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private unsafe delegate void CopySubresourceRegionFn(IntPtr self, IntPtr dst, uint dstSub, uint dstX, uint dstY, uint dstZ, IntPtr src, uint srcSub, Box* box);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private unsafe delegate void GetDescFn(IntPtr self, Texture2DDesc* desc);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate int GetFeatureLevelFn(IntPtr self);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private unsafe delegate int CreateTexture2DFn(IntPtr self, Texture2DDesc* desc, SubresourceData* data, IntPtr* texture);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate void FlushFn(IntPtr self);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate void CopyResourceFn(IntPtr self, IntPtr dst, IntPtr src);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private unsafe delegate int MapFn(IntPtr self, IntPtr resource, uint sub, int mapType, uint flags, MappedSubresource* mapped);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate void UnmapFn(IntPtr self, IntPtr resource, uint sub);

		public struct MappedSubresource
		{
			public unsafe byte* Data;

			public uint RowPitch;

			public uint DepthPitch;
		}

		private const int SlotRelease = 2;

		private const int SlotGetDevice = 3;

		private const int SlotTextureGetDesc = 10;

		private const int SlotCreateTexture2D = 5;

		private const int SlotGetFeatureLevel = 37;

		private const int SlotGetImmediateContext = 40;

		private const int SlotCopySubresourceRegion = 46;

		private const int SlotCopyResource = 47;

		private const int SlotMap = 14;

		private const int SlotUnmap = 15;

		private const int SlotFlush = 111;

		private static readonly Dictionary<IntPtr, Delegate> Bound = new Dictionary<IntPtr, Delegate>();

		private static readonly object Gate = new object();

		private unsafe static T Slot<T>(IntPtr obj, int index) where T : Delegate
		{
			IntPtr intPtr = ((IntPtr*)(void*)(*(IntPtr*)(void*)obj))[index];
			lock (Gate)
			{
				if (Bound.TryGetValue(intPtr, out var value))
				{
					return (T)value;
				}
				T val = (T)Marshal.GetDelegateForFunctionPointer(intPtr, typeof(T));
				Bound[intPtr] = val;
				return val;
			}
		}

		public static void Release(IntPtr obj)
		{
			if (obj != IntPtr.Zero)
			{
				Slot<ReleaseFn>(obj, 2)(obj);
			}
		}

		public unsafe static IntPtr DeviceOf(IntPtr resource)
		{
			if (resource == IntPtr.Zero)
			{
				return IntPtr.Zero;
			}
			IntPtr result = default(IntPtr);
			Slot<GetDeviceFn>(resource, 3)(resource, &result);
			return result;
		}

		public unsafe static IntPtr ImmediateContext(IntPtr device)
		{
			IntPtr result = default(IntPtr);
			Slot<GetImmediateContextFn>(device, 40)(device, &result);
			return result;
		}

		public static int FeatureLevel(IntPtr device)
		{
			return Slot<GetFeatureLevelFn>(device, 37)(device);
		}

		public unsafe static Texture2DDesc Desc(IntPtr texture)
		{
			Texture2DDesc result = default(Texture2DDesc);
			Slot<GetDescFn>(texture, 10)(texture, &result);
			return result;
		}

		public unsafe static void Copy(IntPtr context, IntPtr dst, uint x, uint y, IntPtr src, uint width, uint height)
		{
			Box box = new Box
			{
				Left = 0u,
				Top = 0u,
				Front = 0u,
				Right = width,
				Bottom = height,
				Back = 1u
			};
			Slot<CopySubresourceRegionFn>(context, 46)(context, dst, 0u, x, y, 0u, src, 0u, &box);
		}

		public static void Flush(IntPtr context)
		{
			Slot<FlushFn>(context, 111)(context);
		}

		public unsafe static bool ReadRows(IntPtr device, IntPtr context, IntPtr texture, int[] rows, float[] into, int width = 0)
		{
			Texture2DDesc texture2DDesc = Desc(texture);
			if (Family(texture2DDesc.Format) != 27 && Family(texture2DDesc.Format) != 90)
			{
				return false;
			}
			Texture2DDesc desc = texture2DDesc;
			desc.MipLevels = 1u;
			desc.ArraySize = 1u;
			desc.SampleCount = 1u;
			desc.SampleQuality = 0u;
			desc.Usage = 3;
			desc.BindFlags = 0u;
			desc.CpuAccessFlags = 131072u;
			desc.MiscFlags = 0u;
			IntPtr intPtr = CreateTexture(device, desc, null, 0u);
			if (intPtr == IntPtr.Zero)
			{
				return false;
			}
			try
			{
				Slot<CopyResourceFn>(context, 47)(context, intPtr, texture);
				MappedSubresource mappedSubresource = default(MappedSubresource);
				if (Slot<MapFn>(context, 14)(context, intPtr, 0u, 1, 0u, &mappedSubresource) < 0)
				{
					return false;
				}
				bool flag = Family(texture2DDesc.Format) == 90;
				uint num = ((width > 0) ? Math.Min((uint)width, texture2DDesc.Width) : texture2DDesc.Width);
				for (int i = 0; i < rows.Length; i++)
				{
					int num2 = Math.Max(0, Math.Min((int)(texture2DDesc.Height - 1), rows[i]));
					byte* ptr = mappedSubresource.Data + num2 * mappedSubresource.RowPitch;
					double num3 = 0.0;
					double num4 = 0.0;
					double num5 = 0.0;
					double num6 = 0.0;
					int num7 = 0;
					while (num7 < num)
					{
						num3 += (double)(int)(flag ? ptr[2] : (*ptr));
						num4 += (double)(int)ptr[1];
						num5 += (double)(int)(flag ? (*ptr) : ptr[2]);
						num6 += (double)(int)ptr[3];
						num7++;
						ptr += 4;
					}
					float num8 = Math.Max(1u, num);
					into[i * 4] = (float)(num3 / (double)num8);
					into[i * 4 + 1] = (float)(num4 / (double)num8);
					into[i * 4 + 2] = (float)(num5 / (double)num8);
					into[i * 4 + 3] = (float)(num6 / (double)num8);
				}
				Slot<UnmapFn>(context, 15)(context, intPtr, 0u);
				return true;
			}
			finally
			{
				Release(intPtr);
			}
		}

		public unsafe static IntPtr CreateTexture(IntPtr device, Texture2DDesc desc, void* pixels, uint pitch)
		{
			SubresourceData subresourceData = new SubresourceData
			{
				SysMem = pixels,
				SysMemPitch = pitch
			};
			IntPtr result = default(IntPtr);
			if (Slot<CreateTexture2DFn>(device, 5)(device, &desc, (pixels != null) ? (&subresourceData) : null, &result) >= 0)
			{
				return result;
			}
			return IntPtr.Zero;
		}

		public static int Family(int f)
		{
			switch (f)
			{
			case 27:
			case 28:
			case 29:
			case 30:
			case 31:
			case 32:
				return 27;
			case 87:
			case 90:
			case 91:
				return 90;
			case 9:
			case 10:
			case 11:
			case 12:
			case 13:
			case 14:
				return 9;
			default:
				return f;
			}
		}

		public static string FormatName(int f)
		{
			switch (f)
			{
			case 27:
				return "R8G8B8A8_TYPELESS";
			case 28:
				return "R8G8B8A8_UNORM";
			case 29:
				return "R8G8B8A8_UNORM_SRGB";
			case 87:
				return "B8G8R8A8_UNORM";
			case 90:
				return "B8G8R8A8_TYPELESS";
			case 91:
				return "B8G8R8A8_UNORM_SRGB";
			case 10:
				return "R16G16B16A16_FLOAT";
			case 24:
				return "R10G10B10A2_UNORM";
			default:
				return "DXGI " + f;
			}
		}
	}
}
