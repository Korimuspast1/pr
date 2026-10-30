using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace FirstPerson.Vr
{
	internal static class Xr
	{
		public sealed class Failure : Exception
		{
			public readonly int Result;

			public Failure(string what, int result)
				: base(what + ": " + Name(result))
			{
				Result = result;
			}
		}

		public struct Quat
		{
			public float X;

			public float Y;

			public float Z;

			public float W;
		}

		public struct Vec3
		{
			public float X;

			public float Y;

			public float Z;
		}

		public struct Vec2
		{
			public float X;

			public float Y;
		}

		public struct Pose
		{
			public Quat Orientation;

			public Vec3 Position;

			public static Pose Identity => new Pose
			{
				Orientation = new Quat
				{
					W = 1f
				}
			};
		}

		public struct Fov
		{
			public float AngleLeft;

			public float AngleRight;

			public float AngleUp;

			public float AngleDown;
		}

		public struct Rect2Di
		{
			public int X;

			public int Y;

			public int Width;

			public int Height;
		}

		public struct Extent2Df
		{
			public float Width;

			public float Height;
		}

		public struct ApplicationInfo
		{
			public unsafe fixed byte ApplicationName[128];

			public uint ApplicationVersion;

			public unsafe fixed byte EngineName[128];

			public uint EngineVersion;

			public ulong ApiVersion;
		}

		public struct InstanceCreateInfo
		{
			public int Type;

			public unsafe void* Next;

			public ulong CreateFlags;

			public ApplicationInfo ApplicationInfo;

			public uint EnabledApiLayerCount;

			public unsafe byte** EnabledApiLayerNames;

			public uint EnabledExtensionCount;

			public unsafe byte** EnabledExtensionNames;
		}

		public struct ExtensionProperties
		{
			public int Type;

			public unsafe void* Next;

			public unsafe fixed byte ExtensionName[128];

			public uint ExtensionVersion;
		}

		public struct InstanceProperties
		{
			public int Type;

			public unsafe void* Next;

			public ulong RuntimeVersion;

			public unsafe fixed byte RuntimeName[128];
		}

		public struct SystemGetInfo
		{
			public int Type;

			public unsafe void* Next;

			public int FormFactor;
		}

		public struct SystemProperties
		{
			public int Type;

			public unsafe void* Next;

			public ulong SystemId;

			public uint VendorId;

			public unsafe fixed byte SystemName[256];

			public uint MaxSwapchainImageHeight;

			public uint MaxSwapchainImageWidth;

			public uint MaxLayerCount;

			public uint OrientationTracking;

			public uint PositionTracking;
		}

		public struct GraphicsRequirementsD3D11
		{
			public int Type;

			public unsafe void* Next;

			public uint LuidLow;

			public int LuidHigh;

			public int MinFeatureLevel;
		}

		public struct GraphicsBindingD3D11
		{
			public int Type;

			public unsafe void* Next;

			public IntPtr Device;
		}

		public struct SessionCreateInfo
		{
			public int Type;

			public unsafe void* Next;

			public ulong CreateFlags;

			public ulong SystemId;
		}

		public struct SessionBeginInfo
		{
			public int Type;

			public unsafe void* Next;

			public int PrimaryViewConfigurationType;
		}

		public struct ReferenceSpaceCreateInfo
		{
			public int Type;

			public unsafe void* Next;

			public int ReferenceSpaceType;

			public Pose PoseInReferenceSpace;
		}

		public struct ViewConfigurationView
		{
			public int Type;

			public unsafe void* Next;

			public uint RecommendedImageRectWidth;

			public uint MaxImageRectWidth;

			public uint RecommendedImageRectHeight;

			public uint MaxImageRectHeight;

			public uint RecommendedSwapchainSampleCount;

			public uint MaxSwapchainSampleCount;
		}

		public struct SwapchainCreateInfo
		{
			public int Type;

			public unsafe void* Next;

			public ulong CreateFlags;

			public ulong UsageFlags;

			public long Format;

			public uint SampleCount;

			public uint Width;

			public uint Height;

			public uint FaceCount;

			public uint ArraySize;

			public uint MipCount;
		}

		public struct SwapchainImageD3D11
		{
			public int Type;

			public unsafe void* Next;

			public IntPtr Texture;
		}

		public struct SwapchainImageAcquireInfo
		{
			public int Type;

			public unsafe void* Next;
		}

		public struct SwapchainImageWaitInfo
		{
			public int Type;

			public unsafe void* Next;

			public long Timeout;
		}

		public struct SwapchainImageReleaseInfo
		{
			public int Type;

			public unsafe void* Next;
		}

		public struct FrameWaitInfo
		{
			public int Type;

			public unsafe void* Next;
		}

		public struct FrameState
		{
			public int Type;

			public unsafe void* Next;

			public long PredictedDisplayTime;

			public long PredictedDisplayPeriod;

			public uint ShouldRender;
		}

		public struct FrameBeginInfo
		{
			public int Type;

			public unsafe void* Next;
		}

		public struct FrameEndInfo
		{
			public int Type;

			public unsafe void* Next;

			public long DisplayTime;

			public int EnvironmentBlendMode;

			public uint LayerCount;

			public unsafe void** Layers;
		}

		public struct ViewLocateInfo
		{
			public int Type;

			public unsafe void* Next;

			public int ViewConfigurationType;

			public long DisplayTime;

			public ulong Space;
		}

		public struct ViewState
		{
			public int Type;

			public unsafe void* Next;

			public ulong ViewStateFlags;
		}

		public struct View
		{
			public int Type;

			public unsafe void* Next;

			public Pose Pose;

			public Fov Fov;
		}

		public struct SwapchainSubImage
		{
			public ulong Swapchain;

			public Rect2Di ImageRect;

			public uint ImageArrayIndex;
		}

		public struct CompositionLayerProjectionView
		{
			public int Type;

			public unsafe void* Next;

			public Pose Pose;

			public Fov Fov;

			public SwapchainSubImage SubImage;
		}

		public struct CompositionLayerProjection
		{
			public int Type;

			public unsafe void* Next;

			public ulong LayerFlags;

			public ulong Space;

			public uint ViewCount;

			public unsafe CompositionLayerProjectionView* Views;
		}

		public struct CompositionLayerQuad
		{
			public int Type;

			public unsafe void* Next;

			public ulong LayerFlags;

			public ulong Space;

			public int EyeVisibility;

			public SwapchainSubImage SubImage;

			public Pose Pose;

			public Extent2Df Size;
		}

		public struct EventDataBuffer
		{
			public int Type;

			public unsafe void* Next;

			public unsafe fixed byte Varying[4000];
		}

		public struct EventSessionStateChanged
		{
			public int Type;

			public unsafe void* Next;

			public ulong Session;

			public int State;

			public long Time;
		}

		public struct ActionSetCreateInfo
		{
			public int Type;

			public unsafe void* Next;

			public unsafe fixed byte ActionSetName[64];

			public unsafe fixed byte LocalizedActionSetName[128];

			public uint Priority;
		}

		public struct ActionCreateInfo
		{
			public int Type;

			public unsafe void* Next;

			public unsafe fixed byte ActionName[64];

			public int ActionType;

			public uint CountSubactionPaths;

			public unsafe ulong* SubactionPaths;

			public unsafe fixed byte LocalizedActionName[128];
		}

		public struct ActionSuggestedBinding
		{
			public ulong Action;

			public ulong Binding;
		}

		public struct InteractionProfileSuggestedBinding
		{
			public int Type;

			public unsafe void* Next;

			public ulong InteractionProfile;

			public uint CountSuggestedBindings;

			public unsafe ActionSuggestedBinding* SuggestedBindings;
		}

		public struct SessionActionSetsAttachInfo
		{
			public int Type;

			public unsafe void* Next;

			public uint CountActionSets;

			public unsafe ulong* ActionSets;
		}

		public struct ActiveActionSet
		{
			public ulong ActionSet;

			public ulong SubactionPath;
		}

		public struct ActionsSyncInfo
		{
			public int Type;

			public unsafe void* Next;

			public uint CountActiveActionSets;

			public unsafe ActiveActionSet* ActiveActionSets;
		}

		public struct ActionStateGetInfo
		{
			public int Type;

			public unsafe void* Next;

			public ulong Action;

			public ulong SubactionPath;
		}

		public struct ActionStateBoolean
		{
			public int Type;

			public unsafe void* Next;

			public uint CurrentState;

			public uint ChangedSinceLastSync;

			public long LastChangeTime;

			public uint IsActive;
		}

		public struct ActionStateFloat
		{
			public int Type;

			public unsafe void* Next;

			public float CurrentState;

			public uint ChangedSinceLastSync;

			public long LastChangeTime;

			public uint IsActive;
		}

		public struct ActionStateVector2f
		{
			public int Type;

			public unsafe void* Next;

			public Vec2 CurrentState;

			public uint ChangedSinceLastSync;

			public long LastChangeTime;

			public uint IsActive;
		}

		public struct ActionStatePose
		{
			public int Type;

			public unsafe void* Next;

			public uint IsActive;
		}

		public struct ActionSpaceCreateInfo
		{
			public int Type;

			public unsafe void* Next;

			public ulong Action;

			public ulong SubactionPath;

			public Pose PoseInActionSpace;
		}

		public struct SpaceLocation
		{
			public int Type;

			public unsafe void* Next;

			public ulong LocationFlags;

			public Pose Pose;
		}

		public struct HapticActionInfo
		{
			public int Type;

			public unsafe void* Next;

			public ulong Action;

			public ulong SubactionPath;
		}

		public struct HapticVibration
		{
			public int Type;

			public unsafe void* Next;

			public long Duration;

			public float Frequency;

			public float Amplitude;
		}

		public struct InteractionProfileState
		{
			public int Type;

			public unsafe void* Next;

			public ulong InteractionProfile;
		}

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int GetInstanceProcAddrFn(ulong instance, [MarshalAs(UnmanagedType.LPStr)] string name, IntPtr* function);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int EnumerateInstanceExtensionPropertiesFn(byte* layerName, uint capacity, uint* count, ExtensionProperties* properties);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int CreateInstanceFn(InstanceCreateInfo* info, ulong* instance);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public delegate int HandleFn(ulong handle);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int GetInstancePropertiesFn(ulong instance, InstanceProperties* properties);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int PollEventFn(ulong instance, EventDataBuffer* buffer);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int GetSystemFn(ulong instance, SystemGetInfo* info, ulong* systemId);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int GetSystemPropertiesFn(ulong instance, ulong systemId, SystemProperties* properties);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int EnumerateViewConfigurationViewsFn(ulong instance, ulong systemId, int viewConfigurationType, uint capacity, uint* count, ViewConfigurationView* views);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int GetD3D11GraphicsRequirementsFn(ulong instance, ulong systemId, GraphicsRequirementsD3D11* requirements);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int CreateSessionFn(ulong instance, SessionCreateInfo* info, ulong* session);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int BeginSessionFn(ulong session, SessionBeginInfo* info);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int EnumerateSwapchainFormatsFn(ulong session, uint capacity, uint* count, long* formats);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int CreateSwapchainFn(ulong session, SwapchainCreateInfo* info, ulong* swapchain);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int EnumerateSwapchainImagesFn(ulong swapchain, uint capacity, uint* count, SwapchainImageD3D11* images);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int AcquireSwapchainImageFn(ulong swapchain, SwapchainImageAcquireInfo* info, uint* index);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int WaitSwapchainImageFn(ulong swapchain, SwapchainImageWaitInfo* info);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int ReleaseSwapchainImageFn(ulong swapchain, SwapchainImageReleaseInfo* info);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int CreateReferenceSpaceFn(ulong session, ReferenceSpaceCreateInfo* info, ulong* space);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int LocateSpaceFn(ulong space, ulong baseSpace, long time, SpaceLocation* location);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int WaitFrameFn(ulong session, FrameWaitInfo* info, FrameState* state);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int BeginFrameFn(ulong session, FrameBeginInfo* info);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int EndFrameFn(ulong session, FrameEndInfo* info);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int LocateViewsFn(ulong session, ViewLocateInfo* info, ViewState* state, uint capacity, uint* count, View* views);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int StringToPathFn(ulong instance, [MarshalAs(UnmanagedType.LPStr)] string path, ulong* result);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int PathToStringFn(ulong instance, ulong path, uint capacity, uint* count, byte* buffer);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int CreateActionSetFn(ulong instance, ActionSetCreateInfo* info, ulong* actionSet);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int CreateActionFn(ulong actionSet, ActionCreateInfo* info, ulong* action);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int SuggestBindingsFn(ulong instance, InteractionProfileSuggestedBinding* suggested);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int AttachActionSetsFn(ulong session, SessionActionSetsAttachInfo* info);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int SyncActionsFn(ulong session, ActionsSyncInfo* info);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int GetActionStateBooleanFn(ulong session, ActionStateGetInfo* info, ActionStateBoolean* state);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int GetActionStateFloatFn(ulong session, ActionStateGetInfo* info, ActionStateFloat* state);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int GetActionStateVector2fFn(ulong session, ActionStateGetInfo* info, ActionStateVector2f* state);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int GetActionStatePoseFn(ulong session, ActionStateGetInfo* info, ActionStatePose* state);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int CreateActionSpaceFn(ulong session, ActionSpaceCreateInfo* info, ulong* space);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int ApplyHapticFeedbackFn(ulong session, HapticActionInfo* info, HapticVibration* feedback);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public unsafe delegate int GetCurrentInteractionProfileFn(ulong session, ulong topLevelUserPath, InteractionProfileState* state);

		private struct NegotiateLoaderInfo
		{
			public int StructType;

			public uint StructVersion;

			public UIntPtr StructSize;

			public uint MinInterfaceVersion;

			public uint MaxInterfaceVersion;

			public ulong MinApiVersion;

			public ulong MaxApiVersion;
		}

		private struct NegotiateRuntimeRequest
		{
			public int StructType;

			public uint StructVersion;

			public UIntPtr StructSize;

			public uint RuntimeInterfaceVersion;

			public ulong RuntimeApiVersion;

			public IntPtr GetInstanceProcAddr;
		}

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private unsafe delegate int NegotiateFn(NegotiateLoaderInfo* loaderInfo, NegotiateRuntimeRequest* runtimeRequest);

		public static Action<string> LogLine = delegate
		{
		};

		public const int Success = 0;

		public const int TimeoutExpired = 1;

		public const int SessionLossPending = 3;

		public const int EventUnavailable = 4;

		public const int SessionNotFocused = 8;

		public const int FrameDiscarded = 9;

		public const int ErrorFormFactorUnavailable = -35;

		public const int ErrorRuntimeUnavailable = -51;

		public const int ErrorRuntimeFailure = -2;

		public const int ErrorSessionLost = -17;

		public const int ErrorInstanceLost = -13;

		public const int ErrorSessionNotRunning = -16;

		public const int ErrorCallOrderInvalid = -37;

		public const int ErrorGraphicsDeviceInvalid = -38;

		public const int TypeExtensionProperties = 2;

		public const int TypeInstanceCreateInfo = 3;

		public const int TypeSystemGetInfo = 4;

		public const int TypeSystemProperties = 5;

		public const int TypeViewLocateInfo = 6;

		public const int TypeView = 7;

		public const int TypeSessionCreateInfo = 8;

		public const int TypeSwapchainCreateInfo = 9;

		public const int TypeSessionBeginInfo = 10;

		public const int TypeViewState = 11;

		public const int TypeFrameEndInfo = 12;

		public const int TypeHapticVibration = 13;

		public const int TypeEventDataBuffer = 16;

		public const int TypeEventInstanceLossPending = 17;

		public const int TypeEventSessionStateChanged = 18;

		public const int TypeActionStateBoolean = 23;

		public const int TypeActionStateFloat = 24;

		public const int TypeActionStateVector2f = 25;

		public const int TypeActionStatePose = 27;

		public const int TypeActionSetCreateInfo = 28;

		public const int TypeActionCreateInfo = 29;

		public const int TypeInstanceProperties = 32;

		public const int TypeFrameWaitInfo = 33;

		public const int TypeCompositionLayerProjection = 35;

		public const int TypeCompositionLayerQuad = 36;

		public const int TypeReferenceSpaceCreateInfo = 37;

		public const int TypeActionSpaceCreateInfo = 38;

		public const int TypeEventReferenceSpaceChangePending = 40;

		public const int TypeViewConfigurationView = 41;

		public const int TypeSpaceLocation = 42;

		public const int TypeFrameState = 44;

		public const int TypeFrameBeginInfo = 46;

		public const int TypeCompositionLayerProjectionView = 48;

		public const int TypeEventEventsLost = 49;

		public const int TypeInteractionProfileSuggestedBinding = 51;

		public const int TypeEventInteractionProfileChanged = 52;

		public const int TypeInteractionProfileState = 53;

		public const int TypeSwapchainImageAcquireInfo = 55;

		public const int TypeSwapchainImageWaitInfo = 56;

		public const int TypeSwapchainImageReleaseInfo = 57;

		public const int TypeActionStateGetInfo = 58;

		public const int TypeHapticActionInfo = 59;

		public const int TypeSessionActionSetsAttachInfo = 60;

		public const int TypeActionsSyncInfo = 61;

		public const int TypeGraphicsBindingD3D11 = 1000027000;

		public const int TypeSwapchainImageD3D11 = 1000027001;

		public const int TypeGraphicsRequirementsD3D11 = 1000027002;

		public const int FormFactorHmd = 1;

		public const int ViewConfigStereo = 2;

		public const int SpaceView = 1;

		public const int SpaceLocal = 2;

		public const int SpaceStage = 3;

		public const int BlendOpaque = 1;

		public const int EyeBoth = 0;

		public const int StateUnknown = 0;

		public const int StateIdle = 1;

		public const int StateReady = 2;

		public const int StateSynchronized = 3;

		public const int StateVisible = 4;

		public const int StateFocused = 5;

		public const int StateStopping = 6;

		public const int StateLossPending = 7;

		public const int StateExiting = 8;

		public const ulong UsageColorAttachment = 1uL;

		public const ulong UsageTransferSrc = 8uL;

		public const ulong UsageTransferDst = 16uL;

		public const ulong UsageSampled = 32uL;

		public const ulong UsageMutableFormat = 64uL;

		public const ulong LayerSourceAlpha = 2uL;

		public const ulong LayerUnpremultiplied = 4uL;

		public const ulong ViewOrientationValid = 1uL;

		public const ulong ViewPositionValid = 2uL;

		public const ulong LocationOrientationValid = 1uL;

		public const ulong LocationPositionValid = 2uL;

		public const ulong LocationOrientationTracked = 4uL;

		public const ulong LocationPositionTracked = 8uL;

		public const int ActionBoolean = 1;

		public const int ActionFloat = 2;

		public const int ActionVector2 = 3;

		public const int ActionPose = 4;

		public const int ActionVibration = 100;

		public const long InfiniteDuration = long.MaxValue;

		public const long MinHapticDuration = -1L;

		private static GetInstanceProcAddrFn _gipa;

		public static EnumerateInstanceExtensionPropertiesFn EnumerateInstanceExtensionProperties;

		public static CreateInstanceFn CreateInstance;

		public static HandleFn DestroyInstance;

		public static HandleFn DestroySession;

		public static HandleFn EndSession;

		public static HandleFn RequestExitSession;

		public static HandleFn DestroySwapchain;

		public static HandleFn DestroySpace;

		public static GetInstancePropertiesFn GetInstanceProperties;

		public static PollEventFn PollEvent;

		public static GetSystemFn GetSystem;

		public static GetSystemPropertiesFn GetSystemProperties;

		public static EnumerateViewConfigurationViewsFn EnumerateViewConfigurationViews;

		public static GetD3D11GraphicsRequirementsFn GetD3D11GraphicsRequirements;

		public static CreateSessionFn CreateSession;

		public static BeginSessionFn BeginSession;

		public static EnumerateSwapchainFormatsFn EnumerateSwapchainFormats;

		public static CreateSwapchainFn CreateSwapchain;

		public static EnumerateSwapchainImagesFn EnumerateSwapchainImages;

		public static AcquireSwapchainImageFn AcquireSwapchainImage;

		public static WaitSwapchainImageFn WaitSwapchainImage;

		public static ReleaseSwapchainImageFn ReleaseSwapchainImage;

		public static CreateReferenceSpaceFn CreateReferenceSpace;

		public static LocateSpaceFn LocateSpace;

		public static WaitFrameFn WaitFrame;

		public static BeginFrameFn BeginFrame;

		public static EndFrameFn EndFrame;

		public static LocateViewsFn LocateViews;

		public static StringToPathFn StringToPath;

		public static PathToStringFn PathToString;

		public static CreateActionSetFn CreateActionSet;

		public static CreateActionFn CreateAction;

		public static SuggestBindingsFn SuggestInteractionProfileBindings;

		public static AttachActionSetsFn AttachSessionActionSets;

		public static SyncActionsFn SyncActions;

		public static GetActionStateBooleanFn GetActionStateBoolean;

		public static GetActionStateFloatFn GetActionStateFloat;

		public static GetActionStateVector2fFn GetActionStateVector2f;

		public static GetActionStatePoseFn GetActionStatePose;

		public static CreateActionSpaceFn CreateActionSpace;

		public static ApplyHapticFeedbackFn ApplyHapticFeedback;

		public static GetCurrentInteractionProfileFn GetCurrentInteractionProfile;

		public static string LoadedFrom;

		private const uint LoadWithAlteredSearchPath = 8u;

		public static bool Loaded => _gipa != null;

		public static string Name(int r)
		{
			switch (r)
			{
			case 0:
				return "XR_SUCCESS";
			case 1:
				return "XR_TIMEOUT_EXPIRED";
			case 3:
				return "XR_SESSION_LOSS_PENDING";
			case 4:
				return "XR_EVENT_UNAVAILABLE";
			case 8:
				return "XR_SESSION_NOT_FOCUSED";
			case 9:
				return "XR_FRAME_DISCARDED";
			case -1:
				return "XR_ERROR_VALIDATION_FAILURE";
			case -2:
				return "XR_ERROR_RUNTIME_FAILURE";
			case -3:
				return "XR_ERROR_OUT_OF_MEMORY";
			case -4:
				return "XR_ERROR_API_VERSION_UNSUPPORTED";
			case -6:
				return "XR_ERROR_INITIALIZATION_FAILED";
			case -7:
				return "XR_ERROR_FUNCTION_UNSUPPORTED";
			case -8:
				return "XR_ERROR_FEATURE_UNSUPPORTED";
			case -9:
				return "XR_ERROR_EXTENSION_NOT_PRESENT";
			case -12:
				return "XR_ERROR_HANDLE_INVALID";
			case -13:
				return "XR_ERROR_INSTANCE_LOST";
			case -14:
				return "XR_ERROR_SESSION_RUNNING";
			case -16:
				return "XR_ERROR_SESSION_NOT_RUNNING";
			case -17:
				return "XR_ERROR_SESSION_LOST";
			case -18:
				return "XR_ERROR_SYSTEM_INVALID";
			case -19:
				return "XR_ERROR_PATH_INVALID";
			case -22:
				return "XR_ERROR_PATH_UNSUPPORTED";
			case -23:
				return "XR_ERROR_LAYER_INVALID";
			case -24:
				return "XR_ERROR_LAYER_LIMIT_EXCEEDED";
			case -25:
				return "XR_ERROR_SWAPCHAIN_RECT_INVALID";
			case -26:
				return "XR_ERROR_SWAPCHAIN_FORMAT_UNSUPPORTED";
			case -27:
				return "XR_ERROR_ACTION_TYPE_MISMATCH";
			case -28:
				return "XR_ERROR_SESSION_NOT_READY";
			case -29:
				return "XR_ERROR_SESSION_NOT_STOPPING";
			case -30:
				return "XR_ERROR_TIME_INVALID";
			case -31:
				return "XR_ERROR_REFERENCE_SPACE_UNSUPPORTED";
			case -34:
				return "XR_ERROR_FORM_FACTOR_UNSUPPORTED";
			case -35:
				return "XR_ERROR_FORM_FACTOR_UNAVAILABLE";
			case -37:
				return "XR_ERROR_CALL_ORDER_INVALID";
			case -38:
				return "XR_ERROR_GRAPHICS_DEVICE_INVALID";
			case -39:
				return "XR_ERROR_POSE_INVALID";
			case -41:
				return "XR_ERROR_VIEW_CONFIGURATION_TYPE_UNSUPPORTED";
			case -42:
				return "XR_ERROR_ENVIRONMENT_BLEND_MODE_UNSUPPORTED";
			case -46:
				return "XR_ERROR_ACTIONSET_NOT_ATTACHED";
			case -47:
				return "XR_ERROR_ACTIONSETS_ALREADY_ATTACHED";
			case -50:
				return "XR_ERROR_GRAPHICS_REQUIREMENTS_CALL_MISSING";
			case -51:
				return "XR_ERROR_RUNTIME_UNAVAILABLE";
			default:
				return ((r < 0) ? "XR_ERROR " : "XR result ") + r;
			}
		}

		public static int Check(int result, string what)
		{
			if (result < 0)
			{
				throw new Failure(what, result);
			}
			return result;
		}

		public static ulong MakeVersion(ulong major, ulong minor, ulong patch)
		{
			return ((major & 0xFFFF) << 48) | ((minor & 0xFFFF) << 32) | (patch & 0xFFFFFFFFu);
		}

		public static string VersionText(ulong v)
		{
			return (v >> 48) + "." + ((v >> 32) & 0xFFFF) + "." + (v & 0xFFFFFFFFu);
		}

		public static string StateName(int s)
		{
			switch (s)
			{
			case 1:
				return "idle";
			case 2:
				return "ready";
			case 3:
				return "synchronized";
			case 4:
				return "visible";
			case 5:
				return "focused";
			case 6:
				return "stopping";
			case 7:
				return "loss pending";
			case 8:
				return "exiting";
			default:
				return "unknown";
			}
		}

		private unsafe static string CheckLayouts()
		{
			StringBuilder sb = new StringBuilder();
			Is("ApplicationInfo", sizeof(ApplicationInfo), 272);
			Is("InstanceCreateInfo", sizeof(InstanceCreateInfo), 328);
			Is("ExtensionProperties", sizeof(ExtensionProperties), 152);
			Is("InstanceProperties", sizeof(InstanceProperties), 152);
			Is("SystemGetInfo", sizeof(SystemGetInfo), 24);
			Is("SystemProperties", sizeof(SystemProperties), 304);
			Is("GraphicsRequirementsD3D11", sizeof(GraphicsRequirementsD3D11), 32);
			Is("GraphicsBindingD3D11", sizeof(GraphicsBindingD3D11), 24);
			Is("SessionCreateInfo", sizeof(SessionCreateInfo), 32);
			Is("SessionBeginInfo", sizeof(SessionBeginInfo), 24);
			Is("ReferenceSpaceCreateInfo", sizeof(ReferenceSpaceCreateInfo), 48);
			Is("ViewConfigurationView", sizeof(ViewConfigurationView), 40);
			Is("SwapchainCreateInfo", sizeof(SwapchainCreateInfo), 64);
			Is("SwapchainImageD3D11", sizeof(SwapchainImageD3D11), 24);
			Is("SwapchainImageWaitInfo", sizeof(SwapchainImageWaitInfo), 24);
			Is("FrameState", sizeof(FrameState), 40);
			Is("FrameEndInfo", sizeof(FrameEndInfo), 40);
			Is("ViewLocateInfo", sizeof(ViewLocateInfo), 40);
			Is("ViewState", sizeof(ViewState), 24);
			Is("View", sizeof(View), 64);
			Is("SwapchainSubImage", sizeof(SwapchainSubImage), 32);
			Is("CompositionLayerProjectionView", sizeof(CompositionLayerProjectionView), 96);
			Is("CompositionLayerProjection", sizeof(CompositionLayerProjection), 48);
			Is("CompositionLayerQuad", sizeof(CompositionLayerQuad), 112);
			Is("EventDataBuffer", sizeof(EventDataBuffer), 4016);
			Is("EventSessionStateChanged", sizeof(EventSessionStateChanged), 40);
			Is("ActionSetCreateInfo", sizeof(ActionSetCreateInfo), 216);
			Is("ActionCreateInfo", sizeof(ActionCreateInfo), 224);
			Is("InteractionProfileSuggestedBinding", sizeof(InteractionProfileSuggestedBinding), 40);
			Is("SessionActionSetsAttachInfo", sizeof(SessionActionSetsAttachInfo), 32);
			Is("ActionsSyncInfo", sizeof(ActionsSyncInfo), 32);
			Is("ActionStateGetInfo", sizeof(ActionStateGetInfo), 32);
			Is("ActionStateBoolean", sizeof(ActionStateBoolean), 40);
			Is("ActionStateFloat", sizeof(ActionStateFloat), 40);
			Is("ActionStateVector2f", sizeof(ActionStateVector2f), 48);
			Is("ActionStatePose", sizeof(ActionStatePose), 24);
			Is("ActionSpaceCreateInfo", sizeof(ActionSpaceCreateInfo), 64);
			Is("SpaceLocation", sizeof(SpaceLocation), 56);
			Is("HapticVibration", sizeof(HapticVibration), 32);
			return sb.ToString();
			void Is(string name, int got, int want)
			{
				if (got != want)
				{
					sb.Append(name).Append(' ').Append(got)
						.Append("!=")
						.Append(want)
						.Append(' ');
				}
			}
		}

		private unsafe static T Fn<T>(ulong instance, string name, bool required = true) where T : Delegate
		{
			IntPtr intPtr = default(IntPtr);
			int num = _gipa(instance, name, &intPtr);
			if (num < 0 || intPtr == IntPtr.Zero)
			{
				if (required)
				{
					throw new Failure("xrGetInstanceProcAddr " + name, (num < 0) ? num : (-7));
				}
				return null;
			}
			return (T)Marshal.GetDelegateForFunctionPointer(intPtr, typeof(T));
		}

		public static void BindInstance(ulong i)
		{
			DestroyInstance = Fn<HandleFn>(i, "xrDestroyInstance");
			GetInstanceProperties = Fn<GetInstancePropertiesFn>(i, "xrGetInstanceProperties");
			PollEvent = Fn<PollEventFn>(i, "xrPollEvent");
			GetSystem = Fn<GetSystemFn>(i, "xrGetSystem");
			GetSystemProperties = Fn<GetSystemPropertiesFn>(i, "xrGetSystemProperties");
			EnumerateViewConfigurationViews = Fn<EnumerateViewConfigurationViewsFn>(i, "xrEnumerateViewConfigurationViews");
			GetD3D11GraphicsRequirements = Fn<GetD3D11GraphicsRequirementsFn>(i, "xrGetD3D11GraphicsRequirementsKHR");
			CreateSession = Fn<CreateSessionFn>(i, "xrCreateSession");
			DestroySession = Fn<HandleFn>(i, "xrDestroySession");
			BeginSession = Fn<BeginSessionFn>(i, "xrBeginSession");
			EndSession = Fn<HandleFn>(i, "xrEndSession");
			RequestExitSession = Fn<HandleFn>(i, "xrRequestExitSession");
			EnumerateSwapchainFormats = Fn<EnumerateSwapchainFormatsFn>(i, "xrEnumerateSwapchainFormats");
			CreateSwapchain = Fn<CreateSwapchainFn>(i, "xrCreateSwapchain");
			DestroySwapchain = Fn<HandleFn>(i, "xrDestroySwapchain");
			EnumerateSwapchainImages = Fn<EnumerateSwapchainImagesFn>(i, "xrEnumerateSwapchainImages");
			AcquireSwapchainImage = Fn<AcquireSwapchainImageFn>(i, "xrAcquireSwapchainImage");
			WaitSwapchainImage = Fn<WaitSwapchainImageFn>(i, "xrWaitSwapchainImage");
			ReleaseSwapchainImage = Fn<ReleaseSwapchainImageFn>(i, "xrReleaseSwapchainImage");
			CreateReferenceSpace = Fn<CreateReferenceSpaceFn>(i, "xrCreateReferenceSpace");
			DestroySpace = Fn<HandleFn>(i, "xrDestroySpace");
			LocateSpace = Fn<LocateSpaceFn>(i, "xrLocateSpace");
			WaitFrame = Fn<WaitFrameFn>(i, "xrWaitFrame");
			BeginFrame = Fn<BeginFrameFn>(i, "xrBeginFrame");
			EndFrame = Fn<EndFrameFn>(i, "xrEndFrame");
			LocateViews = Fn<LocateViewsFn>(i, "xrLocateViews");
			StringToPath = Fn<StringToPathFn>(i, "xrStringToPath");
			PathToString = Fn<PathToStringFn>(i, "xrPathToString");
			CreateActionSet = Fn<CreateActionSetFn>(i, "xrCreateActionSet");
			CreateAction = Fn<CreateActionFn>(i, "xrCreateAction");
			SuggestInteractionProfileBindings = Fn<SuggestBindingsFn>(i, "xrSuggestInteractionProfileBindings");
			AttachSessionActionSets = Fn<AttachActionSetsFn>(i, "xrAttachSessionActionSets");
			SyncActions = Fn<SyncActionsFn>(i, "xrSyncActions");
			GetActionStateBoolean = Fn<GetActionStateBooleanFn>(i, "xrGetActionStateBoolean");
			GetActionStateFloat = Fn<GetActionStateFloatFn>(i, "xrGetActionStateFloat");
			GetActionStateVector2f = Fn<GetActionStateVector2fFn>(i, "xrGetActionStateVector2f");
			GetActionStatePose = Fn<GetActionStatePoseFn>(i, "xrGetActionStatePose");
			CreateActionSpace = Fn<CreateActionSpaceFn>(i, "xrCreateActionSpace");
			ApplyHapticFeedback = Fn<ApplyHapticFeedbackFn>(i, "xrApplyHapticFeedback");
			GetCurrentInteractionProfile = Fn<GetCurrentInteractionProfileFn>(i, "xrGetCurrentInteractionProfile");
		}

		[DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern IntPtr LoadLibraryExW(string path, IntPtr file, uint flags);

		[DllImport("kernel32", BestFitMapping = false, CharSet = CharSet.Ansi, SetLastError = true)]
		private static extern IntPtr GetProcAddress(IntPtr module, string name);

		public unsafe static bool Load(string besideMod, out string error)
		{
			error = null;
			if (_gipa != null)
			{
				return true;
			}
			string text = CheckLayouts();
			if (text.Length > 0)
			{
				error = "struct layouts differ from openxr.h: " + text;
				return false;
			}
			if (IntPtr.Size != 8)
			{
				error = "64-bit only";
				return false;
			}
			try
			{
				if (!string.IsNullOrEmpty(besideMod))
				{
					string text2 = Path.Combine(besideMod, "openxr_loader.dll");
					if (File.Exists(text2))
					{
						IntPtr intPtr = LoadLibraryExW(text2, IntPtr.Zero, 8u);
						IntPtr intPtr2 = ((intPtr != IntPtr.Zero) ? GetProcAddress(intPtr, "xrGetInstanceProcAddr") : IntPtr.Zero);
						if (intPtr2 != IntPtr.Zero)
						{
							Bind(intPtr2);
							LoadedFrom = text2;
							LogLine("openxr: using the loader " + text2);
							return true;
						}
						LogLine("openxr: " + text2 + " would not load (error " + Marshal.GetLastWin32Error() + "); trying the runtime directly");
					}
				}
				string text3 = Environment.GetEnvironmentVariable("XR_RUNTIME_JSON");
				if (string.IsNullOrEmpty(text3))
				{
					using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Khronos\\OpenXR\\1"))
					{
						text3 = registryKey?.GetValue("ActiveRuntime") as string;
					}
				}
				if (string.IsNullOrEmpty(text3) || !File.Exists(text3))
				{
					string text4 = InstalledRuntime();
					if (text4 == null)
					{
						error = (string.IsNullOrEmpty(text3) ? "no OpenXR runtime is set up on this PC (install and open Meta Quest Link, SteamVR or Virtual Desktop)" : ("the active OpenXR runtime's file is missing: " + text3));
						return false;
					}
					LogLine("openxr: the active runtime " + (string.IsNullOrEmpty(text3) ? "isn't set" : (text3 + " is missing")) + "; using " + text4);
					text3 = text4;
				}
				Match match = Regex.Match(File.ReadAllText(text3), "\"library_path\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
				if (!match.Success)
				{
					error = "no library_path in " + text3;
					return false;
				}
				string text5 = Regex.Unescape(match.Groups[1].Value.Replace("\\/", "/"));
				if (!Path.IsPathRooted(text5))
				{
					text5 = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(text3), text5));
				}
				IntPtr intPtr3 = LoadLibraryExW(text5, IntPtr.Zero, 8u);
				if (intPtr3 == IntPtr.Zero)
				{
					error = "could not load the OpenXR runtime " + text5 + " (error " + Marshal.GetLastWin32Error() + ")";
					return false;
				}
				IntPtr procAddress = GetProcAddress(intPtr3, "xrNegotiateLoaderRuntimeInterface");
				if (procAddress == IntPtr.Zero)
				{
					error = text5 + " has no xrNegotiateLoaderRuntimeInterface";
					return false;
				}
				NegotiateLoaderInfo negotiateLoaderInfo = new NegotiateLoaderInfo
				{
					StructType = 1,
					StructVersion = 1u,
					StructSize = (UIntPtr)(ulong)sizeof(NegotiateLoaderInfo),
					MinInterfaceVersion = 1u,
					MaxInterfaceVersion = 1u,
					MinApiVersion = MakeVersion(1uL, 0uL, 0uL),
					MaxApiVersion = MakeVersion(1uL, 1023uL, 4095uL)
				};
				NegotiateRuntimeRequest negotiateRuntimeRequest = new NegotiateRuntimeRequest
				{
					StructType = 3,
					StructVersion = 1u,
					StructSize = (UIntPtr)(ulong)sizeof(NegotiateRuntimeRequest)
				};
				int num = ((NegotiateFn)Marshal.GetDelegateForFunctionPointer(procAddress, typeof(NegotiateFn)))(&negotiateLoaderInfo, &negotiateRuntimeRequest);
				if (num < 0 || negotiateRuntimeRequest.GetInstanceProcAddr == IntPtr.Zero)
				{
					error = "the OpenXR runtime refused the handshake: " + Name(num);
					return false;
				}
				Bind(negotiateRuntimeRequest.GetInstanceProcAddr);
				LoadedFrom = text5;
				LogLine("openxr: runtime " + text5 + " (interface " + negotiateRuntimeRequest.RuntimeInterfaceVersion + ", api " + VersionText(negotiateRuntimeRequest.RuntimeApiVersion) + ")");
				return true;
			}
			catch (Exception ex)
			{
				error = "loading OpenXR failed: " + ex.Message;
				return false;
			}
		}

		private static string InstalledRuntime()
		{
			List<string> list = new List<string>();
			string[] valueNames;
			try
			{
				using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Khronos\\OpenXR\\1\\AvailableRuntimes"))
				{
					if (registryKey != null)
					{
						valueNames = registryKey.GetValueNames();
						foreach (string text in valueNames)
						{
							object value = registryKey.GetValue(text);
							if ((!(value is int) || (int)value == 0) && File.Exists(text))
							{
								list.Add(text);
							}
						}
					}
				}
			}
			catch
			{
			}
			if (list.Count == 0)
			{
				return null;
			}
			valueNames = new string[3] { "OVRServer_x64", "vrserver", "VirtualDesktop.Streamer" };
			foreach (string processName in valueNames)
			{
				try
				{
					Process[] processesByName = Process.GetProcessesByName(processName);
					foreach (Process process in processesByName)
					{
						string text2 = null;
						try
						{
							text2 = Path.GetDirectoryName(process.MainModule.FileName);
						}
						catch
						{
						}
						if (text2 == null)
						{
							continue;
						}
						foreach (string item in list)
						{
							string directoryName = Path.GetDirectoryName(item);
							if (directoryName != null && (directoryName.StartsWith(text2, StringComparison.OrdinalIgnoreCase) || text2.StartsWith(directoryName, StringComparison.OrdinalIgnoreCase)))
							{
								return item;
							}
						}
					}
				}
				catch
				{
				}
			}
			return list[0];
		}

		private static void Bind(IntPtr gipa)
		{
			_gipa = (GetInstanceProcAddrFn)Marshal.GetDelegateForFunctionPointer(gipa, typeof(GetInstanceProcAddrFn));
			EnumerateInstanceExtensionProperties = Fn<EnumerateInstanceExtensionPropertiesFn>(0uL, "xrEnumerateInstanceExtensionProperties");
			CreateInstance = Fn<CreateInstanceFn>(0uL, "xrCreateInstance");
		}

		public unsafe static string Text(byte* chars, int max)
		{
			int i;
			for (i = 0; i < max && chars[i] != 0; i++)
			{
			}
			return Encoding.UTF8.GetString(chars, i);
		}

		public unsafe static void SetText(byte* chars, int max, string s)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(s ?? "");
			int num = Math.Min(bytes.Length, max - 1);
			for (int i = 0; i < num; i++)
			{
				chars[i] = bytes[i];
			}
			chars[num] = 0;
		}

		public unsafe static string[] InstanceExtensions()
		{
			uint num = 0u;
			Check(EnumerateInstanceExtensionProperties(null, 0u, &num, null), "xrEnumerateInstanceExtensionProperties");
			ExtensionProperties[] array = new ExtensionProperties[num];
			for (int i = 0; i < array.Length; i++)
			{
				array[i].Type = 2;
			}
			fixed (ExtensionProperties* properties = array)
			{
				Check(EnumerateInstanceExtensionProperties(null, num, &num, properties), "xrEnumerateInstanceExtensionProperties");
			}
			string[] array2 = new string[num];
			for (int j = 0; j < num; j++)
			{
				fixed (byte* extensionName = array[j].ExtensionName)
				{
					array2[j] = Text(extensionName, 128);
				}
			}
			return array2;
		}

		public unsafe static ulong ToPath(ulong instance, string path)
		{
			ulong result = default(ulong);
			Check(StringToPath(instance, path, &result), "xrStringToPath " + path);
			return result;
		}

		public unsafe static string PathText(ulong instance, ulong path)
		{
			if (path == 0L)
			{
				return "(none)";
			}
			uint num = 0u;
			byte* ptr = stackalloc byte[256];
			if (PathToString(instance, path, 256u, &num, ptr) < 0)
			{
				return "?";
			}
			return Text(ptr, 256);
		}
	}
}
