using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstPerson.Vr
{
	internal sealed class VrSystem
	{
		public enum Phase
		{
			Off,
			NoRuntime,
			NoHeadset,
			Starting,
			Ready,
			Running,
			Failed
		}

		public sealed class Layers
		{
			public int Eyes;

			public readonly Xr.Pose[] Pose = new Xr.Pose[2];

			public readonly Xr.Fov[] Fov = new Xr.Fov[2];

			public readonly IntPtr[] Picture = new IntPtr[2];

			public readonly int[] Width = new int[2];

			public readonly int[] Height = new int[2];

			public bool Panel;

			public IntPtr PanelPicture;

			public int PanelWidth;

			public int PanelHeight;

			public Xr.Pose PanelPose;

			public bool PanelOnHead;

			public float PanelMetresWide;

			public float PanelMetresHigh;

			public bool PanelSeeThrough;

			public readonly Quad[] Quads = new Quad[4]
			{
				new Quad(),
				new Quad(),
				new Quad(),
				new Quad()
			};

			public int QuadCount;

			public const int MaxQuads = 4;

			public float Fade;

			public IntPtr FadePicture;

			public int FadeSize;

			internal long DisplayTime;

			internal bool Begin;

			public void Clear()
			{
				Eyes = 0;
				Panel = false;
				QuadCount = 0;
				Fade = 0f;
			}

			public Quad AddQuad()
			{
				if (QuadCount >= 4)
				{
					return null;
				}
				return Quads[QuadCount++];
			}

			public void CopyFrom(Layers o)
			{
				Eyes = o.Eyes;
				for (int i = 0; i < 2; i++)
				{
					Pose[i] = o.Pose[i];
					Fov[i] = o.Fov[i];
					Picture[i] = o.Picture[i];
					Width[i] = o.Width[i];
					Height[i] = o.Height[i];
				}
				Panel = o.Panel;
				PanelPicture = o.PanelPicture;
				PanelWidth = o.PanelWidth;
				PanelHeight = o.PanelHeight;
				PanelPose = o.PanelPose;
				PanelOnHead = o.PanelOnHead;
				PanelMetresWide = o.PanelMetresWide;
				PanelMetresHigh = o.PanelMetresHigh;
				PanelSeeThrough = o.PanelSeeThrough;
				QuadCount = o.QuadCount;
				for (int j = 0; j < o.QuadCount; j++)
				{
					Quads[j].CopyFrom(o.Quads[j]);
				}
				Fade = o.Fade;
				FadePicture = o.FadePicture;
				FadeSize = o.FadeSize;
			}
		}

		public sealed class Quad
		{
			public IntPtr Picture;

			public int Width;

			public int Height;

			public Xr.Pose Pose;

			public bool OnHead;

			public float MetresWide;

			public float MetresHigh;

			public bool SeeThrough;

			public void CopyFrom(Quad o)
			{
				Picture = o.Picture;
				Width = o.Width;
				Height = o.Height;
				Pose = o.Pose;
				OnHead = o.OnHead;
				MetresWide = o.MetresWide;
				MetresHigh = o.MetresHigh;
				SeeThrough = o.SeeThrough;
			}
		}

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate void RenderEventFn(int eventId);

		private sealed class Chain
		{
			public ulong Handle;

			public int Width;

			public int Height;

			public IntPtr[] Images;

			public bool Broken;
		}

		private ulong _set;

		private ulong _move;

		private ulong _turn;

		private ulong _attack;

		private ulong _kick;

		private ulong _use;

		private ulong _contextKey;

		private ulong _jump;

		private ulong _ability;

		private ulong _craft;

		private ulong _mapKey;

		private ulong _sprint;

		private ulong _binoculars;

		private ulong _menu;

		private ulong _aim;

		private ulong _grip;

		private ulong _haptic;

		private ulong _leftHand;

		private ulong _rightHand;

		private readonly ulong[] _handSpaces = new ulong[2];

		private readonly ulong[] _gripSpaces = new ulong[2];

		private bool _attached;

		public Vector2 MoveStick;

		public Vector2 TurnStick;

		public float AttackPull;

		public float KickPull;

		public float UsePull;

		public float ContextPull;

		public bool JumpButton;

		public bool AbilityButton;

		public bool CraftButton;

		public bool MapButton;

		public bool SprintButton;

		public bool BinocularsButton;

		public bool MenuButton;

		public readonly Xr.Pose[] Hands = new Xr.Pose[2];

		public readonly bool[] HandValid = new bool[2];

		public readonly Xr.Pose[] Grips = new Xr.Pose[2];

		public readonly bool[] GripValid = new bool[2];

		public string Profile = "";

		public bool LeftHanded;

		public static VrSystem Current;

		public Phase State;

		public string Status = "Off";

		public string HeadsetName = "";

		public string RuntimeName = "";

		public int EyeWidth;

		public int EyeHeight;

		public int MaxImageWidth;

		public int MaxImageHeight;

		public int SwapFormat;

		public int SessionState;

		private ulong _instance;

		private ulong _system;

		private ulong _session;

		private ulong _local;

		private ulong _viewSpace;

		private IntPtr _device;

		private IntPtr _context;

		private volatile bool _running;

		private volatile bool _sessionMade;

		private volatile bool _sessionGone;

		private bool _wanted;

		private bool _exitAsked;

		private bool _closedByHeadset;

		private bool _instanceLost;

		private volatile bool _createFailed;

		private float _retryAt;

		private string _lastError;

		public bool Waited;

		public bool ShouldRender;

		public long DisplayTime;

		public long DisplayPeriod;

		public readonly Xr.View[] Views = new Xr.View[2];

		public bool ViewsValid;

		public Xr.Pose Head = Xr.Pose.Identity;

		public bool HeadValid;

		public int FramesWaited;

		public int FramesSubmitted;

		public bool Recentred;

		private readonly Layers[] _queued = new Layers[4]
		{
			new Layers(),
			new Layers(),
			new Layers(),
			new Layers()
		};

		private int _queueAt;

		private const int EvCreate = 1448214529;

		private const int EvBegin = 1448214530;

		private const int EvEnd = 1448214531;

		private const int EvDestroy = 1448214532;

		private const int EvSubmit0 = 1448214544;

		private static readonly RenderEventFn RenderEvent = OnRenderEvent;

		private static readonly IntPtr RenderEventPtr = Marshal.GetFunctionPointerForDelegate(RenderEvent);

		private readonly Chain[] _eyeChains = new Chain[2];

		private Chain _panelChain;

		private Chain _fadeChain;

		private readonly Chain[] _quadChains = new Chain[4];

		private long[] _formats = new long[0];

		private readonly HashSet<IntPtr> _checked = new HashSet<IntPtr>();

		private readonly HashSet<IntPtr> _copyOk = new HashSet<IntPtr>();

		private readonly Xr.CompositionLayerProjectionView[] _projViews = new Xr.CompositionLayerProjectionView[2];

		private int _lastEndError;

		public bool Simulated;

		public Xr.Pose SimHead = Xr.Pose.Identity;

		public float SimIpd = 0.064f;

		public Vector2 SimMove;

		public Vector2 SimTurn;

		public bool SimJump;

		public bool SimAbility;

		public bool SimMap;

		public bool SimMenu;

		public bool SimCraft;

		public float SimAttack;

		public float SimKick;

		public float SimUse;

		public float SimContext;

		public readonly Xr.Pose[] SimHands = new Xr.Pose[2]
		{
			Xr.Pose.Identity,
			Xr.Pose.Identity
		};

		public readonly bool[] SimHandValid = new bool[2];

		public readonly IntPtr[] SimImages = new IntPtr[3];

		public volatile int SimCopies;

		public readonly Layers SimLast = new Layers();

		public readonly float[][] SimRows = new float[3][]
		{
			new float[8],
			new float[8],
			new float[8]
		};

		public volatile int SimReads;

		public volatile bool SimReadNext;

		private static readonly Xr.Fov SimLeftFov = new Xr.Fov
		{
			AngleLeft = -0.942f,
			AngleRight = 0.698f,
			AngleUp = 0.768f,
			AngleDown = -0.873f
		};

		public int Main => (!LeftHanded) ? 1 : 0;

		public int Off => LeftHanded ? 1 : 0;

		public bool Bgra => D3D11.Family(SwapFormat) == 90;

		public bool Focused => SessionState == 5;

		public bool Running => _running;

		public float TriggerOf(int hand)
		{
			if (hand == 1 == LeftHanded)
			{
				return KickPull;
			}
			return AttackPull;
		}

		public float GripOf(int hand)
		{
			if (hand == 1 == LeftHanded)
			{
				return ContextPull;
			}
			return UsePull;
		}

		private void Handed()
		{
			if (LeftHanded)
			{
				Vector2 turnStick = TurnStick;
				Vector2 moveStick = MoveStick;
				MoveStick = turnStick;
				TurnStick = moveStick;
				float kickPull = KickPull;
				float attackPull = AttackPull;
				AttackPull = kickPull;
				KickPull = attackPull;
				attackPull = ContextPull;
				kickPull = UsePull;
				UsePull = attackPull;
				ContextPull = kickPull;
				bool craftButton = CraftButton;
				bool jumpButton = JumpButton;
				JumpButton = craftButton;
				CraftButton = jumpButton;
				jumpButton = MapButton;
				craftButton = AbilityButton;
				AbilityButton = jumpButton;
				MapButton = craftButton;
				craftButton = BinocularsButton;
				jumpButton = SprintButton;
				SprintButton = craftButton;
				BinocularsButton = jumpButton;
			}
		}

		private unsafe void MakeControls()
		{
			if (_set == 0L)
			{
				Xr.ActionSetCreateInfo actionSetCreateInfo = new Xr.ActionSetCreateInfo
				{
					Type = 28,
					Priority = 0u
				};
				Xr.SetText(actionSetCreateInfo.ActionSetName, 64, "worldfall");
				Xr.SetText(actionSetCreateInfo.LocalizedActionSetName, 128, "Worldfall first person");
				ulong set = default(ulong);
				Xr.Check(Xr.CreateActionSet(_instance, &actionSetCreateInfo, &set), "xrCreateActionSet");
				_set = set;
				_leftHand = Xr.ToPath(_instance, "/user/hand/left");
				_rightHand = Xr.ToPath(_instance, "/user/hand/right");
				_move = Action("move", "Walk", 3);
				_turn = Action("turn", "Turn", 3);
				_attack = Action("attack", "Attack", 2);
				_kick = Action("kick", "Guard", 2);
				_use = Action("use", "Talk, use, enter", 2);
				_contextKey = Action("context", "Reload, vent", 2);
				_jump = Action("jump", "Jump", 1);
				_ability = Action("ability", "Ability", 1);
				_craft = Action("craft", "Crafting", 1);
				_mapKey = Action("minimap", "Minimap", 1);
				_sprint = Action("sprint", "Sprint", 1);
				_binoculars = Action("binoculars", "Binoculars", 1);
				_menu = Action("menu", "Settings", 1);
				_aim = Action("aim", "Hand pointing", 4, perHand: true);
				_grip = Action("grip", "Hand holding", 4, perHand: true);
				_haptic = Action("haptic", "Rumble", 100, perHand: true);
				Suggest("/interaction_profiles/oculus/touch_controller", (_move, "/user/hand/left/input/thumbstick"), (_turn, "/user/hand/right/input/thumbstick"), (_attack, "/user/hand/right/input/trigger/value"), (_kick, "/user/hand/left/input/trigger/value"), (_use, "/user/hand/right/input/squeeze/value"), (_contextKey, "/user/hand/left/input/squeeze/value"), (_jump, "/user/hand/right/input/a/click"), (_ability, "/user/hand/right/input/b/click"), (_craft, "/user/hand/left/input/x/click"), (_mapKey, "/user/hand/left/input/y/click"), (_sprint, "/user/hand/left/input/thumbstick/click"), (_binoculars, "/user/hand/right/input/thumbstick/click"), (_menu, "/user/hand/left/input/menu/click"), (_aim, "/user/hand/left/input/aim/pose"), (_aim, "/user/hand/right/input/aim/pose"), (_grip, "/user/hand/left/input/grip/pose"), (_grip, "/user/hand/right/input/grip/pose"), (_haptic, "/user/hand/left/output/haptic"), (_haptic, "/user/hand/right/output/haptic"));
				Suggest("/interaction_profiles/valve/index_controller", (_move, "/user/hand/left/input/thumbstick"), (_turn, "/user/hand/right/input/thumbstick"), (_attack, "/user/hand/right/input/trigger/value"), (_kick, "/user/hand/left/input/trigger/value"), (_use, "/user/hand/right/input/squeeze/value"), (_contextKey, "/user/hand/left/input/squeeze/value"), (_jump, "/user/hand/right/input/a/click"), (_ability, "/user/hand/right/input/b/click"), (_craft, "/user/hand/left/input/a/click"), (_menu, "/user/hand/left/input/b/click"), (_sprint, "/user/hand/left/input/thumbstick/click"), (_binoculars, "/user/hand/right/input/thumbstick/click"), (_aim, "/user/hand/left/input/aim/pose"), (_aim, "/user/hand/right/input/aim/pose"), (_grip, "/user/hand/left/input/grip/pose"), (_grip, "/user/hand/right/input/grip/pose"), (_haptic, "/user/hand/left/output/haptic"), (_haptic, "/user/hand/right/output/haptic"));
				Suggest("/interaction_profiles/htc/vive_controller", (_move, "/user/hand/left/input/trackpad"), (_turn, "/user/hand/right/input/trackpad"), (_attack, "/user/hand/right/input/trigger/value"), (_kick, "/user/hand/left/input/trigger/value"), (_use, "/user/hand/right/input/squeeze/click"), (_contextKey, "/user/hand/left/input/squeeze/click"), (_jump, "/user/hand/right/input/trackpad/click"), (_sprint, "/user/hand/left/input/trackpad/click"), (_ability, "/user/hand/right/input/menu/click"), (_menu, "/user/hand/left/input/menu/click"), (_aim, "/user/hand/left/input/aim/pose"), (_aim, "/user/hand/right/input/aim/pose"), (_grip, "/user/hand/left/input/grip/pose"), (_grip, "/user/hand/right/input/grip/pose"), (_haptic, "/user/hand/left/output/haptic"), (_haptic, "/user/hand/right/output/haptic"));
				Suggest("/interaction_profiles/microsoft/motion_controller", (_move, "/user/hand/left/input/thumbstick"), (_turn, "/user/hand/right/input/thumbstick"), (_attack, "/user/hand/right/input/trigger/value"), (_kick, "/user/hand/left/input/trigger/value"), (_use, "/user/hand/right/input/squeeze/click"), (_contextKey, "/user/hand/left/input/squeeze/click"), (_jump, "/user/hand/right/input/trackpad/click"), (_craft, "/user/hand/left/input/trackpad/click"), (_sprint, "/user/hand/left/input/thumbstick/click"), (_binoculars, "/user/hand/right/input/thumbstick/click"), (_ability, "/user/hand/right/input/menu/click"), (_menu, "/user/hand/left/input/menu/click"), (_aim, "/user/hand/left/input/aim/pose"), (_aim, "/user/hand/right/input/aim/pose"), (_grip, "/user/hand/left/input/grip/pose"), (_grip, "/user/hand/right/input/grip/pose"), (_haptic, "/user/hand/left/output/haptic"), (_haptic, "/user/hand/right/output/haptic"));
				Suggest("/interaction_profiles/khr/simple_controller", (_attack, "/user/hand/right/input/select/click"), (_kick, "/user/hand/left/input/select/click"), (_jump, "/user/hand/right/input/menu/click"), (_menu, "/user/hand/left/input/menu/click"), (_aim, "/user/hand/left/input/aim/pose"), (_aim, "/user/hand/right/input/aim/pose"), (_grip, "/user/hand/left/input/grip/pose"), (_grip, "/user/hand/right/input/grip/pose"), (_haptic, "/user/hand/left/output/haptic"), (_haptic, "/user/hand/right/output/haptic"));
			}
		}

		private unsafe ulong Action(string name, string shown, int type, bool perHand = false)
		{
			Xr.ActionCreateInfo actionCreateInfo = new Xr.ActionCreateInfo
			{
				Type = 29,
				ActionType = type
			};
			Xr.SetText(actionCreateInfo.ActionName, 64, name);
			Xr.SetText(actionCreateInfo.LocalizedActionName, 128, shown);
			ulong* ptr = stackalloc ulong[2];
			*ptr = _leftHand;
			ptr[1] = _rightHand;
			if (perHand)
			{
				actionCreateInfo.CountSubactionPaths = 2u;
				actionCreateInfo.SubactionPaths = ptr;
			}
			ulong result = default(ulong);
			Xr.Check(Xr.CreateAction(_set, &actionCreateInfo, &result), "xrCreateAction " + name);
			return result;
		}

		private unsafe void Suggest(string profile, params (ulong action, string path)[] list)
		{
			try
			{
				Xr.ActionSuggestedBinding[] array = new Xr.ActionSuggestedBinding[list.Length];
				for (int i = 0; i < list.Length; i++)
				{
					array[i] = new Xr.ActionSuggestedBinding
					{
						Action = list[i].action,
						Binding = Xr.ToPath(_instance, list[i].path)
					};
				}
				fixed (Xr.ActionSuggestedBinding* suggestedBindings = array)
				{
					Xr.InteractionProfileSuggestedBinding interactionProfileSuggestedBinding = new Xr.InteractionProfileSuggestedBinding
					{
						Type = 51,
						InteractionProfile = Xr.ToPath(_instance, profile),
						CountSuggestedBindings = (uint)array.Length,
						SuggestedBindings = suggestedBindings
					};
					int num = Xr.SuggestInteractionProfileBindings(_instance, &interactionProfileSuggestedBinding);
					if (num < 0)
					{
						Log.Info("vr: bindings for " + profile + " refused: " + Xr.Name(num));
					}
				}
			}
			catch (Exception ex)
			{
				Log.Info("vr: bindings for " + profile + ": " + ex.Message);
			}
		}

		private unsafe void AttachControls()
		{
			if (_set == 0L)
			{
				return;
			}
			ulong set = _set;
			Xr.SessionActionSetsAttachInfo sessionActionSetsAttachInfo = new Xr.SessionActionSetsAttachInfo
			{
				Type = 60,
				CountActionSets = 1u,
				ActionSets = &set
			};
			int num = Xr.AttachSessionActionSets(_session, &sessionActionSetsAttachInfo);
			if (num < 0)
			{
				Log.Error("vr: xrAttachSessionActionSets " + Xr.Name(num));
				return;
			}
			ulong num2 = default(ulong);
			for (int i = 0; i < 2; i++)
			{
				Xr.ActionSpaceCreateInfo actionSpaceCreateInfo = new Xr.ActionSpaceCreateInfo
				{
					Type = 38,
					Action = _aim,
					SubactionPath = ((i == 0) ? _leftHand : _rightHand),
					PoseInActionSpace = Xr.Pose.Identity
				};
				if (Xr.CreateActionSpace(_session, &actionSpaceCreateInfo, &num2) >= 0)
				{
					_handSpaces[i] = num2;
				}
				actionSpaceCreateInfo.Action = _grip;
				if (Xr.CreateActionSpace(_session, &actionSpaceCreateInfo, &num2) >= 0)
				{
					_gripSpaces[i] = num2;
				}
			}
			_attached = true;
		}

		private void DropHandSpaces()
		{
			_attached = false;
			for (int i = 0; i < 2; i++)
			{
				if (_handSpaces[i] != 0L)
				{
					Xr.DestroySpace(_handSpaces[i]);
				}
				if (_gripSpaces[i] != 0L)
				{
					Xr.DestroySpace(_gripSpaces[i]);
				}
				_handSpaces[i] = (_gripSpaces[i] = 0uL);
			}
		}

		private void ReleaseControls()
		{
			_set = (_move = (_turn = (_attack = (_kick = (_use = (_contextKey = (_jump = (_ability = (_craft = (_mapKey = (_sprint = (_binoculars = (_menu = (_aim = (_grip = (_haptic = 0uL))))))))))))))));
			ClearControls();
		}

		private void ClearControls()
		{
			MoveStick = (TurnStick = Vector2.zero);
			AttackPull = (KickPull = (UsePull = (ContextPull = 0f)));
			JumpButton = (AbilityButton = (CraftButton = (MapButton = (SprintButton = (BinocularsButton = (MenuButton = false))))));
		}

		public unsafe void SyncControls()
		{
			if (Simulated)
			{
				SimControls();
				Handed();
				return;
			}
			if (!_running || !_attached || SessionState != 5)
			{
				ClearControls();
				return;
			}
			Xr.ActiveActionSet activeActionSet = new Xr.ActiveActionSet
			{
				ActionSet = _set
			};
			Xr.ActionsSyncInfo actionsSyncInfo = new Xr.ActionsSyncInfo
			{
				Type = 61,
				CountActiveActionSets = 1u,
				ActiveActionSets = &activeActionSet
			};
			if (Xr.SyncActions(_session, &actionsSyncInfo) < 0)
			{
				ClearControls();
				return;
			}
			MoveStick = Stick(_move);
			TurnStick = Stick(_turn);
			AttackPull = Pull(_attack);
			KickPull = Pull(_kick);
			UsePull = Pull(_use);
			ContextPull = Pull(_contextKey);
			JumpButton = Button(_jump);
			AbilityButton = Button(_ability);
			CraftButton = Button(_craft);
			MapButton = Button(_mapKey);
			SprintButton = Button(_sprint);
			BinocularsButton = Button(_binoculars);
			MenuButton = Button(_menu);
			Handed();
		}

		private unsafe Vector2 Stick(ulong action)
		{
			Xr.ActionStateGetInfo actionStateGetInfo = new Xr.ActionStateGetInfo
			{
				Type = 58,
				Action = action
			};
			Xr.ActionStateVector2f actionStateVector2f = new Xr.ActionStateVector2f
			{
				Type = 25
			};
			if (Xr.GetActionStateVector2f(_session, &actionStateGetInfo, &actionStateVector2f) < 0 || actionStateVector2f.IsActive == 0)
			{
				return Vector2.zero;
			}
			return new Vector2(actionStateVector2f.CurrentState.X, actionStateVector2f.CurrentState.Y);
		}

		private unsafe float Pull(ulong action)
		{
			Xr.ActionStateGetInfo actionStateGetInfo = new Xr.ActionStateGetInfo
			{
				Type = 58,
				Action = action
			};
			Xr.ActionStateFloat actionStateFloat = new Xr.ActionStateFloat
			{
				Type = 24
			};
			if (Xr.GetActionStateFloat(_session, &actionStateGetInfo, &actionStateFloat) < 0 || actionStateFloat.IsActive == 0)
			{
				return 0f;
			}
			return actionStateFloat.CurrentState;
		}

		private unsafe bool Button(ulong action)
		{
			Xr.ActionStateGetInfo actionStateGetInfo = new Xr.ActionStateGetInfo
			{
				Type = 58,
				Action = action
			};
			Xr.ActionStateBoolean actionStateBoolean = new Xr.ActionStateBoolean
			{
				Type = 23
			};
			if (Xr.GetActionStateBoolean(_session, &actionStateGetInfo, &actionStateBoolean) < 0 || actionStateBoolean.IsActive == 0)
			{
				return false;
			}
			return actionStateBoolean.CurrentState != 0;
		}

		private void LocateHands()
		{
			for (int i = 0; i < 2; i++)
			{
				HandValid[i] = Locate(_handSpaces[i], out Hands[i]);
				GripValid[i] = Locate(_gripSpaces[i], out Grips[i]);
				if (!GripValid[i] && HandValid[i])
				{
					Grips[i] = GripFromAim(Hands[i]);
					GripValid[i] = true;
				}
			}
		}

		private unsafe bool Locate(ulong space, out Xr.Pose pose)
		{
			pose = Xr.Pose.Identity;
			if (space == 0L)
			{
				return false;
			}
			Xr.SpaceLocation spaceLocation = new Xr.SpaceLocation
			{
				Type = 42
			};
			if (Xr.LocateSpace(space, _local, DisplayTime, &spaceLocation) < 0)
			{
				return false;
			}
			if ((spaceLocation.LocationFlags & 3) != 3)
			{
				return false;
			}
			pose = spaceLocation.Pose;
			return true;
		}

		public static Xr.Pose GripFromAim(Xr.Pose aim)
		{
			Xr.Quat quat = new Xr.Quat
			{
				X = Mathf.Sin(0.47996554f),
				W = Mathf.Cos(0.47996554f)
			};
			Xr.Quat orientation = aim.Orientation;
			Xr.Quat orientation2 = new Xr.Quat
			{
				W = orientation.W * quat.W - orientation.X * quat.X - orientation.Y * quat.Y - orientation.Z * quat.Z,
				X = orientation.W * quat.X + orientation.X * quat.W + orientation.Y * quat.Z - orientation.Z * quat.Y,
				Y = orientation.W * quat.Y - orientation.X * quat.Z + orientation.Y * quat.W + orientation.Z * quat.X,
				Z = orientation.W * quat.Z + orientation.X * quat.Y - orientation.Y * quat.X + orientation.Z * quat.W
			};
			Xr.Vec3 vec = Turn(orientation, 0f, -0.02f, 0.07f);
			return new Xr.Pose
			{
				Orientation = orientation2,
				Position = new Xr.Vec3
				{
					X = aim.Position.X + vec.X,
					Y = aim.Position.Y + vec.Y,
					Z = aim.Position.Z + vec.Z
				}
			};
		}

		public unsafe void Rumble(int hand, float strength, float seconds)
		{
			if (_running && _attached && _haptic != 0L && SessionState == 5)
			{
				Xr.HapticActionInfo hapticActionInfo = new Xr.HapticActionInfo
				{
					Type = 59,
					Action = _haptic,
					SubactionPath = ((hand == 0) ? _leftHand : _rightHand)
				};
				Xr.HapticVibration hapticVibration = new Xr.HapticVibration
				{
					Type = 13,
					Duration = (long)((double)Mathf.Clamp(seconds, 0.01f, 2f) * 1000000000.0),
					Frequency = 0f,
					Amplitude = Mathf.Clamp01(strength)
				};
				Xr.ApplyHapticFeedback(_session, &hapticActionInfo, &hapticVibration);
			}
		}

		private unsafe void ProfileChanged()
		{
			if (_session == 0L || Xr.GetCurrentInteractionProfile == null)
			{
				return;
			}
			List<string> list = new List<string>();
			ulong[] array = new ulong[2] { _leftHand, _rightHand };
			foreach (ulong topLevelUserPath in array)
			{
				Xr.InteractionProfileState interactionProfileState = new Xr.InteractionProfileState
				{
					Type = 53
				};
				if (Xr.GetCurrentInteractionProfile(_session, topLevelUserPath, &interactionProfileState) >= 0 && interactionProfileState.InteractionProfile != 0L)
				{
					list.Add(Xr.PathText(_instance, interactionProfileState.InteractionProfile));
				}
			}
			Profile = ((list.Count > 0) ? list[0] : "");
			Log.Info("vr: controllers " + ((list.Count > 0) ? string.Join(", ", list) : "(none)"));
		}

		public void Tick(bool want)
		{
			if (Waited)
			{
				Submit(null);
			}
			if (Simulated)
			{
				if (want)
				{
					SimTick();
					return;
				}
				_running = false;
				State = Phase.Off;
				Status = "Off";
				return;
			}
			if (!want)
			{
				_closedByHeadset = false;
			}
			else if (_closedByHeadset)
			{
				want = false;
			}
			if (want != _wanted)
			{
				_wanted = want;
				if (want)
				{
					_retryAt = 0f;
					_exitAsked = false;
				}
				else
				{
					Shutdown();
				}
			}
			bool flag = State == Phase.Starting && !_sessionMade;
			if (!_wanted)
			{
				if (!(_instance == 0 || flag))
				{
					if (_session != 0L && !_exitAsked)
					{
						Shutdown();
					}
					if (_session == 0L)
					{
						DestroyInstance();
					}
					else
					{
						PollEvents();
					}
				}
				return;
			}
			if (_sessionMade && _sessionGone)
			{
				if (_createFailed)
				{
					_createFailed = false;
					_retryAt = Time.unscaledTime + 30f;
				}
				if (Time.unscaledTime < _retryAt)
				{
					return;
				}
				_sessionMade = (_sessionGone = false);
				_system = 0uL;
				if (_device != IntPtr.Zero)
				{
					D3D11.Release(_device);
					_device = IntPtr.Zero;
				}
				if (_context != IntPtr.Zero)
				{
					D3D11.Release(_context);
					_context = IntPtr.Zero;
				}
				if (_instanceLost)
				{
					_instanceLost = false;
					DestroyInstance();
				}
			}
			if (_instance == 0L || _system == 0L)
			{
				if (Time.unscaledTime < _retryAt)
				{
					return;
				}
				_retryAt = Time.unscaledTime + 3f;
				if (!Begin())
				{
					return;
				}
			}
			PollEvents();
			if (!flag && _running)
			{
				WaitAndLocate();
			}
		}

		private unsafe bool Begin()
		{
			if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11)
			{
				Fail(Phase.Failed, "VR needs DirectX 11. WorldBox is running on " + SystemInfo.graphicsDeviceType);
				_retryAt = float.MaxValue;
				return false;
			}
			Xr.LogLine = Log.Info;
			if (!Xr.Loaded && !Xr.Load(ModFolder(), out var error))
			{
				Fail(Phase.NoRuntime, "No VR runtime found. Install Quest Link, SteamVR or Virtual Desktop", error);
				_retryAt = Time.unscaledTime + 10f;
				return false;
			}
			try
			{
				if (_instance == 0L)
				{
					MakeInstance();
				}
				if (_system == 0L)
				{
					Xr.SystemGetInfo systemGetInfo = new Xr.SystemGetInfo
					{
						Type = 4,
						FormFactor = 1
					};
					ulong system = default(ulong);
					int num = Xr.GetSystem(_instance, &systemGetInfo, &system);
					if (num == -35)
					{
						Fail(Phase.NoHeadset, "No headset found. Start Quest Link or Air Link and put it on");
						return false;
					}
					Xr.Check(num, "xrGetSystem");
					_system = system;
					Describe();
				}
				MakeControls();
				IntPtr nativeTexturePtr = Texture2D.whiteTexture.GetNativeTexturePtr();
				_device = D3D11.DeviceOf(nativeTexturePtr);
				if (_device == IntPtr.Zero)
				{
					throw new Exception("could not find the game's Direct3D device");
				}
				Xr.GraphicsRequirementsD3D11 graphicsRequirementsD3D = new Xr.GraphicsRequirementsD3D11
				{
					Type = 1000027002
				};
				Xr.Check(Xr.GetD3D11GraphicsRequirements(_instance, _system, &graphicsRequirementsD3D), "xrGetD3D11GraphicsRequirementsKHR");
				int num2 = D3D11.FeatureLevel(_device);
				Log.Info($"vr: d3d11 device feature level 0x{num2:x} (headset needs 0x{graphicsRequirementsD3D.MinFeatureLevel:x}, adapter luid {graphicsRequirementsD3D.LuidHigh}:{graphicsRequirementsD3D.LuidLow})");
				State = Phase.Starting;
				Status = "Starting...";
				_sessionMade = (_sessionGone = false);
				Issue(1448214529);
				return true;
			}
			catch (Exception ex)
			{
				Fail(Phase.Failed, "VR could not start. See FirstPerson.log", ex.Message);
				_retryAt = Time.unscaledTime + 10f;
				return false;
			}
		}

		private unsafe void MakeInstance()
		{
			if (Array.IndexOf(Xr.InstanceExtensions(), "XR_KHR_D3D11_enable") < 0)
			{
				throw new Exception("the VR runtime can't share DirectX 11 pictures (no XR_KHR_D3D11_enable)");
			}
			Xr.InstanceCreateInfo instanceCreateInfo = new Xr.InstanceCreateInfo
			{
				Type = 3
			};
			Xr.SetText(instanceCreateInfo.ApplicationInfo.ApplicationName, 128, "WorldBox (Worldfall)");
			instanceCreateInfo.ApplicationInfo.ApplicationVersion = 1u;
			Xr.SetText(instanceCreateInfo.ApplicationInfo.EngineName, 128, "Worldfall");
			instanceCreateInfo.ApplicationInfo.EngineVersion = 1u;
			instanceCreateInfo.ApplicationInfo.ApiVersion = Xr.MakeVersion(1uL, 0uL, 34uL);
			ulong num = default(ulong);
			fixed (byte* bytes = Encoding.ASCII.GetBytes("XR_KHR_D3D11_enable\0"))
			{
				byte* ptr = bytes;
				instanceCreateInfo.EnabledExtensionCount = 1u;
				instanceCreateInfo.EnabledExtensionNames = &ptr;
				Xr.Check(Xr.CreateInstance(&instanceCreateInfo, &num), "xrCreateInstance");
			}
			_instance = num;
			Xr.BindInstance(num);
			Xr.InstanceProperties instanceProperties = new Xr.InstanceProperties
			{
				Type = 32
			};
			if (Xr.GetInstanceProperties(num, &instanceProperties) >= 0)
			{
				RuntimeName = Xr.Text(instanceProperties.RuntimeName, 128) + " " + Xr.VersionText(instanceProperties.RuntimeVersion);
			}
			Log.Info("vr: OpenXR instance on " + RuntimeName);
		}

		private unsafe void Describe()
		{
			Xr.SystemProperties systemProperties = new Xr.SystemProperties
			{
				Type = 5
			};
			Xr.Check(Xr.GetSystemProperties(_instance, _system, &systemProperties), "xrGetSystemProperties");
			HeadsetName = Xr.Text(systemProperties.SystemName, 256);
			MaxImageWidth = (int)systemProperties.MaxSwapchainImageWidth;
			MaxImageHeight = (int)systemProperties.MaxSwapchainImageHeight;
			uint num = default(uint);
			Xr.Check(Xr.EnumerateViewConfigurationViews(_instance, _system, 2, 0u, &num, null), "xrEnumerateViewConfigurationViews");
			Xr.ViewConfigurationView[] array = new Xr.ViewConfigurationView[Math.Max(1u, num)];
			for (int i = 0; i < array.Length; i++)
			{
				array[i].Type = 41;
			}
			fixed (Xr.ViewConfigurationView* views = array)
			{
				Xr.Check(Xr.EnumerateViewConfigurationViews(_instance, _system, 2, num, &num, views), "xrEnumerateViewConfigurationViews");
			}
			EyeWidth = (int)array[0].RecommendedImageRectWidth;
			EyeHeight = (int)array[0].RecommendedImageRectHeight;
			Log.Info($"vr: headset {HeadsetName}, eye {EyeWidth}x{EyeHeight} recommended (max image {MaxImageWidth}x{MaxImageHeight}), position tracking {systemProperties.PositionTracking != 0}");
		}

		private void Shutdown()
		{
			if (_session != 0L && !_exitAsked)
			{
				_exitAsked = true;
				if (_running && Xr.RequestExitSession != null && Xr.RequestExitSession(_session) >= 0)
				{
					return;
				}
				_running = false;
				Issue(1448214532);
			}
			State = Phase.Off;
			Status = "Off";
		}

		private void DestroyInstance()
		{
			if ((_sessionGone || !_sessionMade) && _instance != 0L)
			{
				ReleaseControls();
				if (_context != IntPtr.Zero)
				{
					D3D11.Release(_context);
					_context = IntPtr.Zero;
				}
				if (_device != IntPtr.Zero)
				{
					D3D11.Release(_device);
					_device = IntPtr.Zero;
				}
				if (_instance != 0L)
				{
					Xr.DestroyInstance(_instance);
				}
				_instance = (_system = 0uL);
				_sessionMade = (_sessionGone = false);
				Log.Info("vr: closed");
			}
		}

		public void Dispose()
		{
			_wanted = false;
			if (_session != 0L)
			{
				_running = false;
				Issue(1448214532);
				GL.Flush();
			}
		}

		private void Fail(Phase phase, string why, string detail = null)
		{
			State = phase;
			Status = why;
			string text = ((detail != null) ? (why + " (" + detail + ")") : why);
			if (text != _lastError)
			{
				Log.Info("vr: " + text);
			}
			_lastError = text;
		}

		private static string ModFolder()
		{
			try
			{
				return Path.GetDirectoryName(typeof(VrSystem).Assembly.Location);
			}
			catch
			{
				return null;
			}
		}

		private unsafe void PollEvents()
		{
			Xr.EventDataBuffer eventDataBuffer = default(Xr.EventDataBuffer);
			for (int i = 0; i < 32; i++)
			{
				eventDataBuffer.Type = 16;
				eventDataBuffer.Next = null;
				if (Xr.PollEvent(_instance, &eventDataBuffer) != 0)
				{
					break;
				}
				switch (eventDataBuffer.Type)
				{
				case 18:
				{
					Xr.EventSessionStateChanged eventSessionStateChanged = *(Xr.EventSessionStateChanged*)(&eventDataBuffer);
					StateChanged(eventSessionStateChanged.State);
					break;
				}
				case 17:
					Log.Info("vr: the runtime is going away");
					_running = false;
					_instanceLost = true;
					if (_session != 0L)
					{
						Issue(1448214532);
					}
					else
					{
						_sessionMade = (_sessionGone = true);
					}
					_retryAt = Time.unscaledTime + 5f;
					Fail(Phase.NoHeadset, "The VR runtime closed. Looking for it again");
					break;
				case 40:
					Recentred = true;
					break;
				case 52:
					ProfileChanged();
					break;
				}
			}
		}

		private void StateChanged(int state)
		{
			SessionState = state;
			Log.Info("vr: session " + Xr.StateName(state));
			switch (state)
			{
			case 2:
				Issue(1448214530);
				Status = "Running on " + HeadsetName;
				break;
			case 3:
			case 4:
				Status = "Running on " + HeadsetName + ". Waiting for the headset to be worn";
				break;
			case 5:
				Status = "Running on " + HeadsetName;
				break;
			case 6:
				_running = false;
				Issue(1448214531);
				break;
			case 7:
			case 8:
				_running = false;
				Issue(1448214532);
				if (_wanted && !_exitAsked)
				{
					if (state == 8)
					{
						_closedByHeadset = true;
						Fail(Phase.Off, "Closed from the headset. Turn VR off and on to restart");
					}
					else
					{
						Fail(Phase.NoHeadset, "Headset lost. Looking for it again");
					}
					_retryAt = Time.unscaledTime + 3f;
				}
				break;
			}
		}

		private unsafe void WaitAndLocate()
		{
			Xr.FrameWaitInfo frameWaitInfo = new Xr.FrameWaitInfo
			{
				Type = 33
			};
			Xr.FrameState frameState = new Xr.FrameState
			{
				Type = 44
			};
			int num = Xr.WaitFrame(_session, &frameWaitInfo, &frameState);
			if (num < 0)
			{
				if (num != -16)
				{
					Log.Error("vr: xrWaitFrame " + Xr.Name(num));
				}
				return;
			}
			Waited = true;
			FramesWaited++;
			DisplayTime = frameState.PredictedDisplayTime;
			DisplayPeriod = frameState.PredictedDisplayPeriod;
			ShouldRender = frameState.ShouldRender != 0;
			ViewsValid = (HeadValid = false);
			if (!ShouldRender)
			{
				return;
			}
			Xr.ViewLocateInfo viewLocateInfo = new Xr.ViewLocateInfo
			{
				Type = 6,
				ViewConfigurationType = 2,
				DisplayTime = DisplayTime,
				Space = _local
			};
			Xr.ViewState viewState = new Xr.ViewState
			{
				Type = 11
			};
			uint num2 = 0u;
			Views[0].Type = (Views[1].Type = 7);
			Views[0].Next = (Views[1].Next = null);
			fixed (Xr.View* views = Views)
			{
				num = Xr.LocateViews(_session, &viewLocateInfo, &viewState, 2u, &num2, views);
			}
			ViewsValid = num >= 0 && num2 == 2 && (viewState.ViewStateFlags & 1) != 0;
			Xr.SpaceLocation spaceLocation = new Xr.SpaceLocation
			{
				Type = 42
			};
			if (Xr.LocateSpace(_viewSpace, _local, DisplayTime, &spaceLocation) >= 0 && (spaceLocation.LocationFlags & 1) != 0L)
			{
				Head = spaceLocation.Pose;
				if ((spaceLocation.LocationFlags & 2) == 0L)
				{
					Head.Position = default(Xr.Vec3);
				}
				HeadValid = true;
			}
			else if (ViewsValid)
			{
				Head.Orientation = Views[0].Pose.Orientation;
				Head.Position = new Xr.Vec3
				{
					X = (Views[0].Pose.Position.X + Views[1].Pose.Position.X) * 0.5f,
					Y = (Views[0].Pose.Position.Y + Views[1].Pose.Position.Y) * 0.5f,
					Z = (Views[0].Pose.Position.Z + Views[1].Pose.Position.Z) * 0.5f
				};
				HeadValid = true;
			}
			LocateHands();
		}

		public void Submit(Layers layers)
		{
			if (Waited)
			{
				Waited = false;
				int num = _queueAt++ & 3;
				Layers layers2 = _queued[num];
				if (layers != null && ShouldRender)
				{
					layers2.CopyFrom(layers);
				}
				else
				{
					layers2.Clear();
				}
				layers2.DisplayTime = DisplayTime;
				layers2.Begin = true;
				if (Simulated)
				{
					SimLast.CopyFrom(layers2);
				}
				FramesSubmitted++;
				Issue(1448214544 + num);
			}
		}

		private void Issue(int ev)
		{
			Current = this;
			GL.IssuePluginEvent(RenderEventPtr, ev);
		}

		private static void OnRenderEvent(int ev)
		{
			VrSystem current = Current;
			if (current == null)
			{
				return;
			}
			try
			{
				if (ev >= 1448214544 && ev < 1448214548)
				{
					current.RtSubmit(current._queued[ev - 1448214544]);
					return;
				}
				switch (ev)
				{
				case 1448214529:
					current.RtCreate();
					break;
				case 1448214530:
					current.RtBegin();
					break;
				case 1448214531:
					current.RtEnd();
					break;
				case 1448214532:
					current.RtDestroy();
					break;
				}
			}
			catch (Exception ex)
			{
				try
				{
					Log.Error("vr: render thread: " + ex);
				}
				catch
				{
				}
			}
		}

		private unsafe void RtCreate()
		{
			if (_session != 0L)
			{
				return;
			}
			Xr.GraphicsBindingD3D11 graphicsBindingD3D = new Xr.GraphicsBindingD3D11
			{
				Type = 1000027000,
				Device = _device
			};
			Xr.SessionCreateInfo sessionCreateInfo = new Xr.SessionCreateInfo
			{
				Type = 8,
				Next = &graphicsBindingD3D,
				SystemId = _system
			};
			ulong session = default(ulong);
			int num = Xr.CreateSession(_instance, &sessionCreateInfo, &session);
			if (num < 0)
			{
				Fail(Phase.Failed, (num == -38) ? "The headset needs the same graphics card as the screen" : "The headset refused the game's graphics. See FirstPerson.log", Xr.Name(num));
				_createFailed = true;
				_sessionGone = true;
				_sessionMade = true;
				return;
			}
			_session = session;
			_context = D3D11.ImmediateContext(_device);
			_local = MakeSpace(2);
			_viewSpace = MakeSpace(1);
			uint num2 = default(uint);
			Xr.Check(Xr.EnumerateSwapchainFormats(session, 0u, &num2, null), "xrEnumerateSwapchainFormats");
			_formats = new long[num2];
			fixed (long* formats = _formats)
			{
				Xr.Check(Xr.EnumerateSwapchainFormats(session, num2, &num2, formats), "xrEnumerateSwapchainFormats");
			}
			SwapFormat = PickFormat(_formats);
			List<string> list = new List<string>();
			long[] formats2 = _formats;
			foreach (long num3 in formats2)
			{
				list.Add(D3D11.FormatName((int)num3));
			}
			Log.Info("vr: session made; image formats " + string.Join(", ", list) + " -> " + D3D11.FormatName(SwapFormat));
			AttachControls();
			State = Phase.Ready;
			Status = "Waiting for the headset...";
			_sessionMade = true;
		}

		private static int PickFormat(long[] offered)
		{
			int[] array = new int[4] { 29, 91, 28, 87 };
			foreach (int num in array)
			{
				if (Array.IndexOf(offered, num) >= 0)
				{
					return num;
				}
			}
			if (offered.Length == 0)
			{
				return 29;
			}
			return (int)offered[0];
		}

		private unsafe ulong MakeSpace(int type)
		{
			Xr.ReferenceSpaceCreateInfo referenceSpaceCreateInfo = new Xr.ReferenceSpaceCreateInfo
			{
				Type = 37,
				ReferenceSpaceType = type,
				PoseInReferenceSpace = Xr.Pose.Identity
			};
			ulong result = default(ulong);
			Xr.Check(Xr.CreateReferenceSpace(_session, &referenceSpaceCreateInfo, &result), "xrCreateReferenceSpace " + type);
			return result;
		}

		private unsafe void RtBegin()
		{
			if (_session != 0L && !_running)
			{
				Xr.SessionBeginInfo sessionBeginInfo = new Xr.SessionBeginInfo
				{
					Type = 10,
					PrimaryViewConfigurationType = 2
				};
				int num = Xr.BeginSession(_session, &sessionBeginInfo);
				if (num < 0)
				{
					Log.Error("vr: xrBeginSession " + Xr.Name(num));
					return;
				}
				State = Phase.Running;
				_running = true;
				Log.Info("vr: session running");
			}
		}

		private void RtEnd()
		{
			if (_session != 0L)
			{
				int r = Xr.EndSession(_session);
				Log.Info("vr: session ended (" + Xr.Name(r) + ")");
			}
		}

		private void RtDestroy()
		{
			_running = false;
			for (int i = 0; i < 2; i++)
			{
				DropChain(_eyeChains[i]);
				_eyeChains[i] = null;
			}
			DropChain(_panelChain);
			DropChain(_fadeChain);
			_panelChain = (_fadeChain = null);
			for (int j = 0; j < _quadChains.Length; j++)
			{
				DropChain(_quadChains[j]);
				_quadChains[j] = null;
			}
			DropHandSpaces();
			if (_local != 0L)
			{
				Xr.DestroySpace(_local);
			}
			if (_viewSpace != 0L)
			{
				Xr.DestroySpace(_viewSpace);
			}
			_local = (_viewSpace = 0uL);
			if (_session != 0L)
			{
				Xr.DestroySession(_session);
			}
			_session = 0uL;
			_sessionGone = true;
			if (State == Phase.Running || State == Phase.Ready)
			{
				State = (_wanted ? Phase.NoHeadset : Phase.Off);
				if (!_wanted)
				{
					Status = "Off";
				}
			}
			if (!_wanted || _closedByHeadset)
			{
				_retryAt = 0f;
			}
			Log.Info("vr: session closed");
		}

		private static void DropChain(Chain c)
		{
			if (c != null && c.Handle != 0L)
			{
				Xr.DestroySwapchain(c.Handle);
			}
		}

		private unsafe Chain Ensure(ref Chain c, int w, int h)
		{
			if (c != null && (c.Broken || (c.Width >= w && c.Height >= h)))
			{
				if (!c.Broken)
				{
					return c;
				}
				return null;
			}
			if (c != null)
			{
				DropChain(c);
			}
			int num = Math.Min(Math.Max(w, (c != null) ? c.Width : 0), (MaxImageWidth > 0) ? MaxImageWidth : 8192);
			int num2 = Math.Min(Math.Max(h, (c != null) ? c.Height : 0), (MaxImageHeight > 0) ? MaxImageHeight : 8192);
			c = new Chain
			{
				Width = num,
				Height = num2
			};
			Xr.SwapchainCreateInfo swapchainCreateInfo = new Xr.SwapchainCreateInfo
			{
				Type = 9,
				UsageFlags = 49uL,
				Format = SwapFormat,
				SampleCount = 1u,
				Width = (uint)num,
				Height = (uint)num2,
				FaceCount = 1u,
				ArraySize = 1u,
				MipCount = 1u
			};
			ulong num4 = default(ulong);
			int num3 = Xr.CreateSwapchain(_session, &swapchainCreateInfo, &num4);
			if (num3 < 0)
			{
				Log.Error($"vr: xrCreateSwapchain {num}x{num2}: {Xr.Name(num3)}");
				c.Broken = true;
				return null;
			}
			c.Handle = num4;
			uint num5 = default(uint);
			Xr.Check(Xr.EnumerateSwapchainImages(num4, 0u, &num5, null), "xrEnumerateSwapchainImages");
			Xr.SwapchainImageD3D11[] array = new Xr.SwapchainImageD3D11[num5];
			for (int i = 0; i < num5; i++)
			{
				array[i].Type = 1000027001;
			}
			fixed (Xr.SwapchainImageD3D11* images = array)
			{
				Xr.Check(Xr.EnumerateSwapchainImages(num4, num5, &num5, images), "xrEnumerateSwapchainImages");
			}
			c.Images = new IntPtr[num5];
			for (int j = 0; j < num5; j++)
			{
				c.Images[j] = array[j].Texture;
			}
			if (num5 != 0)
			{
				D3D11.Texture2DDesc texture2DDesc = D3D11.Desc(c.Images[0]);
				Log.Info($"vr: image queue {num}x{num2}, {num5} images, {D3D11.FormatName(texture2DDesc.Format)}");
			}
			return c;
		}

		private unsafe bool Fill(Chain c, IntPtr picture, int w, int h)
		{
			if (c == null || picture == IntPtr.Zero)
			{
				return false;
			}
			Xr.SwapchainImageAcquireInfo swapchainImageAcquireInfo = new Xr.SwapchainImageAcquireInfo
			{
				Type = 55
			};
			uint num2 = default(uint);
			int num = Xr.AcquireSwapchainImage(c.Handle, &swapchainImageAcquireInfo, &num2);
			if (num < 0)
			{
				Log.Error("vr: xrAcquireSwapchainImage " + Xr.Name(num));
				return false;
			}
			Xr.SwapchainImageWaitInfo swapchainImageWaitInfo = new Xr.SwapchainImageWaitInfo
			{
				Type = 56,
				Timeout = long.MaxValue
			};
			num = Xr.WaitSwapchainImage(c.Handle, &swapchainImageWaitInfo);
			bool flag = false;
			if (num == 0)
			{
				lock (_checked)
				{
					if (!_checked.Contains(picture))
					{
						CheckCopy(picture, c.Images[num2]);
					}
					flag = _copyOk.Contains(picture);
				}
				if (flag)
				{
					D3D11.Copy(_context, c.Images[num2], 0u, 0u, picture, (uint)Math.Min(w, c.Width), (uint)Math.Min(h, c.Height));
				}
			}
			else
			{
				Log.Error("vr: xrWaitSwapchainImage " + Xr.Name(num));
			}
			Xr.SwapchainImageReleaseInfo swapchainImageReleaseInfo = new Xr.SwapchainImageReleaseInfo
			{
				Type = 57
			};
			Xr.ReleaseSwapchainImage(c.Handle, &swapchainImageReleaseInfo);
			return num == 0 && flag;
		}

		private void CheckCopy(IntPtr picture, IntPtr image)
		{
			_checked.Add(picture);
			D3D11.Texture2DDesc texture2DDesc = D3D11.Desc(picture);
			D3D11.Texture2DDesc texture2DDesc2 = D3D11.Desc(image);
			bool flag = D3D11.Family(texture2DDesc.Format) == D3D11.Family(texture2DDesc2.Format) && texture2DDesc.SampleCount == 1;
			if (flag)
			{
				_copyOk.Add(picture);
			}
			Log.Info($"vr: picture {texture2DDesc.Width}x{texture2DDesc.Height} {D3D11.FormatName(texture2DDesc.Format)} -> image {D3D11.FormatName(texture2DDesc2.Format)}: " + (flag ? "copies" : "CAN'T COPY (formats differ)"));
		}

		public void Forget(IntPtr picture)
		{
			lock (_checked)
			{
				_checked.Remove(picture);
				_copyOk.Remove(picture);
			}
		}

		private unsafe void RtSubmit(Layers q)
		{
			if (Simulated && q.Begin)
			{
				SimSubmit(q);
			}
			else
			{
				if (!q.Begin || _session == 0L)
				{
					return;
				}
				q.Begin = false;
				Xr.FrameBeginInfo frameBeginInfo = new Xr.FrameBeginInfo
				{
					Type = 46
				};
				int num = Xr.BeginFrame(_session, &frameBeginInfo);
				if (num < 0)
				{
					Log.Error("vr: xrBeginFrame " + Xr.Name(num));
					return;
				}
				Xr.CompositionLayerProjection compositionLayerProjection = new Xr.CompositionLayerProjection
				{
					Type = 35,
					Space = _local
				};
				Xr.CompositionLayerQuad compositionLayerQuad = new Xr.CompositionLayerQuad
				{
					Type = 36,
					EyeVisibility = 0
				};
				Xr.CompositionLayerQuad compositionLayerQuad2 = new Xr.CompositionLayerQuad
				{
					Type = 36,
					EyeVisibility = 0
				};
				Xr.CompositionLayerQuad* ptr = stackalloc Xr.CompositionLayerQuad[4];
				void** ptr2 = stackalloc void*[7];
				uint num2 = 0u;
				fixed (Xr.CompositionLayerProjectionView* projViews = _projViews)
				{
					if (q.Eyes == 2)
					{
						bool flag = true;
						for (int i = 0; i < 2 && flag; i++)
						{
							Chain chain = Ensure(ref _eyeChains[i], q.Width[i], q.Height[i]);
							flag = Fill(chain, q.Picture[i], q.Width[i], q.Height[i]);
							if (!flag)
							{
								break;
							}
							projViews[i] = new Xr.CompositionLayerProjectionView
							{
								Type = 48,
								Pose = q.Pose[i],
								Fov = q.Fov[i],
								SubImage = new Xr.SwapchainSubImage
								{
									Swapchain = chain.Handle,
									ImageRect = new Xr.Rect2Di
									{
										Width = Math.Min(q.Width[i], chain.Width),
										Height = Math.Min(q.Height[i], chain.Height)
									}
								}
							};
						}
						if (flag)
						{
							compositionLayerProjection.ViewCount = 2u;
							compositionLayerProjection.Views = projViews;
							ptr2[num2++] = &compositionLayerProjection;
						}
					}
					if (q.Panel)
					{
						Chain chain2 = Ensure(ref _panelChain, q.PanelWidth, q.PanelHeight);
						if (Fill(chain2, q.PanelPicture, q.PanelWidth, q.PanelHeight))
						{
							compositionLayerQuad.Space = (q.PanelOnHead ? _viewSpace : _local);
							compositionLayerQuad.LayerFlags = (ulong)(q.PanelSeeThrough ? 2 : 0);
							compositionLayerQuad.SubImage = new Xr.SwapchainSubImage
							{
								Swapchain = chain2.Handle,
								ImageRect = new Xr.Rect2Di
								{
									Width = Math.Min(q.PanelWidth, chain2.Width),
									Height = Math.Min(q.PanelHeight, chain2.Height)
								}
							};
							compositionLayerQuad.Pose = q.PanelPose;
							compositionLayerQuad.Size = new Xr.Extent2Df
							{
								Width = q.PanelMetresWide,
								Height = q.PanelMetresHigh
							};
							ptr2[num2++] = &compositionLayerQuad;
						}
					}
					for (int j = 0; j < q.QuadCount && j < 4; j++)
					{
						Quad quad = q.Quads[j];
						if (!(quad.Picture == IntPtr.Zero) && quad.Width > 0 && quad.Height > 0 && !(quad.MetresWide <= 0f) && !(quad.MetresHigh <= 0f))
						{
							Chain chain3 = Ensure(ref _quadChains[j], quad.Width, quad.Height);
							if (Fill(chain3, quad.Picture, quad.Width, quad.Height))
							{
								ptr[j] = new Xr.CompositionLayerQuad
								{
									Type = 36,
									EyeVisibility = 0,
									Space = (quad.OnHead ? _viewSpace : _local),
									LayerFlags = (ulong)(quad.SeeThrough ? 2 : 0),
									SubImage = new Xr.SwapchainSubImage
									{
										Swapchain = chain3.Handle,
										ImageRect = new Xr.Rect2Di
										{
											Width = Math.Min(quad.Width, chain3.Width),
											Height = Math.Min(quad.Height, chain3.Height)
										}
									},
									Pose = quad.Pose,
									Size = new Xr.Extent2Df
									{
										Width = quad.MetresWide,
										Height = quad.MetresHigh
									}
								};
								ptr2[num2++] = ptr + j;
							}
						}
					}
					if (q.Fade > 0.001f && q.FadePicture != IntPtr.Zero && q.FadeSize > 0)
					{
						Chain chain4 = Ensure(ref _fadeChain, q.FadeSize, q.FadeSize);
						if (Fill(chain4, q.FadePicture, q.FadeSize, q.FadeSize))
						{
							compositionLayerQuad2.Space = _viewSpace;
							compositionLayerQuad2.LayerFlags = 2uL;
							compositionLayerQuad2.SubImage = new Xr.SwapchainSubImage
							{
								Swapchain = chain4.Handle,
								ImageRect = new Xr.Rect2Di
								{
									Width = Math.Min(q.FadeSize, chain4.Width),
									Height = Math.Min(q.FadeSize, chain4.Height)
								}
							};
							compositionLayerQuad2.Pose = new Xr.Pose
							{
								Orientation = new Xr.Quat
								{
									W = 1f
								},
								Position = new Xr.Vec3
								{
									Z = -0.25f
								}
							};
							compositionLayerQuad2.Size = new Xr.Extent2Df
							{
								Width = 4f,
								Height = 4f
							};
							ptr2[num2++] = &compositionLayerQuad2;
						}
					}
					Xr.FrameEndInfo frameEndInfo = new Xr.FrameEndInfo
					{
						Type = 12,
						DisplayTime = q.DisplayTime,
						EnvironmentBlendMode = 1,
						LayerCount = num2,
						Layers = ((num2 != 0) ? ptr2 : null)
					};
					num = Xr.EndFrame(_session, &frameEndInfo);
				}
				if (num < 0 && num != _lastEndError)
				{
					Log.Error("vr: xrEndFrame " + Xr.Name(num));
				}
				_lastEndError = num;
			}
		}

		private void SimTick()
		{
			if (_device == IntPtr.Zero)
			{
				_device = D3D11.DeviceOf(Texture2D.whiteTexture.GetNativeTexturePtr());
				_context = D3D11.ImmediateContext(_device);
				HeadsetName = "a pretend headset";
				EyeWidth = 1832;
				EyeHeight = 1920;
				SwapFormat = 29;
				Log.Info("vr: simulated headset");
			}
			State = Phase.Running;
			Status = "Running (simulated)";
			_running = true;
			SessionState = 5;
			Waited = true;
			ShouldRender = true;
			FramesWaited++;
			DisplayTime += 11111111L;
			Head = SimHead;
			HeadValid = true;
			Xr.Vec3 vec = Turn(SimHead.Orientation, 1f, 0f, 0f);
			float num = SimIpd * 0.5f;
			Xr.Vec3 position = SimHead.Position;
			Views[0] = new Xr.View
			{
				Type = 7,
				Pose = new Xr.Pose
				{
					Orientation = SimHead.Orientation,
					Position = new Xr.Vec3
					{
						X = position.X - vec.X * num,
						Y = position.Y - vec.Y * num,
						Z = position.Z - vec.Z * num
					}
				},
				Fov = SimLeftFov
			};
			Views[1] = new Xr.View
			{
				Type = 7,
				Pose = new Xr.Pose
				{
					Orientation = SimHead.Orientation,
					Position = new Xr.Vec3
					{
						X = position.X + vec.X * num,
						Y = position.Y + vec.Y * num,
						Z = position.Z + vec.Z * num
					}
				},
				Fov = new Xr.Fov
				{
					AngleLeft = 0f - SimLeftFov.AngleRight,
					AngleRight = 0f - SimLeftFov.AngleLeft,
					AngleUp = SimLeftFov.AngleUp,
					AngleDown = SimLeftFov.AngleDown
				}
			};
			ViewsValid = true;
			for (int i = 0; i < 2; i++)
			{
				Hands[i] = SimHands[i];
				HandValid[i] = SimHandValid[i];
				Grips[i] = GripFromAim(SimHands[i]);
				GripValid[i] = SimHandValid[i];
			}
		}

		private void SimControls()
		{
			ClearControls();
			MoveStick = SimMove;
			TurnStick = SimTurn;
			JumpButton = SimJump;
			AbilityButton = SimAbility;
			MapButton = SimMap;
			MenuButton = SimMenu;
			CraftButton = SimCraft;
			AttackPull = SimAttack;
			KickPull = SimKick;
			UsePull = SimUse;
			ContextPull = SimContext;
		}

		private void SimSubmit(Layers q)
		{
			q.Begin = false;
			for (int i = 0; i < 3; i++)
			{
				IntPtr intPtr = ((i >= 2) ? (q.Panel ? q.PanelPicture : IntPtr.Zero) : ((q.Eyes == 2) ? q.Picture[i] : IntPtr.Zero));
				IntPtr intPtr2 = SimImages[i];
				if (intPtr == IntPtr.Zero || intPtr2 == IntPtr.Zero)
				{
					continue;
				}
				int num = ((i < 2) ? q.Width[i] : q.PanelWidth);
				int val = ((i < 2) ? q.Height[i] : q.PanelHeight);
				D3D11.Texture2DDesc texture2DDesc = D3D11.Desc(intPtr2);
				D3D11.Texture2DDesc texture2DDesc2 = D3D11.Desc(intPtr);
				if (D3D11.Family(texture2DDesc.Format) != D3D11.Family(texture2DDesc2.Format))
				{
					continue;
				}
				D3D11.Copy(_context, intPtr2, 0u, 0u, intPtr, (uint)Math.Min(num, (int)texture2DDesc.Width), (uint)Math.Min(val, (int)texture2DDesc.Height));
				SimCopies++;
				if (SimReadNext)
				{
					float[] array = new float[8];
					if (D3D11.ReadRows(_device, _context, intPtr2, new int[2]
					{
						0,
						Math.Min(val, (int)texture2DDesc.Height) - 1
					}, array, num))
					{
						SimRows[i] = array;
					}
				}
			}
			if (SimReadNext)
			{
				SimReadNext = false;
				SimReads++;
			}
		}

		private static Xr.Vec3 Turn(Xr.Quat q, float x, float y, float z)
		{
			float num = 2f * (q.Y * z - q.Z * y);
			float num2 = 2f * (q.Z * x - q.X * z);
			float num3 = 2f * (q.X * y - q.Y * x);
			return new Xr.Vec3
			{
				X = x + q.W * num + (q.Y * num3 - q.Z * num2),
				Y = y + q.W * num2 + (q.Z * num - q.X * num3),
				Z = z + q.W * num3 + (q.X * num2 - q.Y * num)
			};
		}
	}
}
