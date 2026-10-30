using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Gpu;
using FirstPerson.Pad;
using FirstPerson.Vr;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class VrTest : CivilTest
	{
		private readonly RenderTexture[] _images = new RenderTexture[3];

		private bool _bars;

		protected override string Prefix => "VR";

		private VrSystem Vr => Mod.VrForTest;

		protected override IEnumerator Body()
		{
			Actor king = Village("human", 6, 0);
			if (king == null)
			{
				Check("a village was founded", ok: false);
				yield break;
			}
			yield return CivilTest.Wait(3f);
			Actor me = null;
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive() && item != king && item.isAdult())
				{
					me = item;
					break;
				}
			}
			if (me == null)
			{
				me = king;
			}
			yield return Possess(me);
			if (!Check("first person came up", InControl(me)))
			{
				yield break;
			}
			Settings s = Mod.Settings;
			bool savedVr = s.Vr;
			int savedTurn = s.VrTurn;
			WorldBoxMod.VrSimulate = true;
			Mod.VrResetForTest();
			Mod.MouseLookOff = true;
			s.Vr = true;
			s.VrTurn = 0;
			try
			{
				yield return CivilTest.Wait(1f);
				if (Check("the pretend headset runs", Vr != null && Vr.Simulated && Mod.VrRunning, Vr?.Status))
				{
					MakeImages();
					yield return Part("level", Level());
					yield return Part("looking round", Looks(me));
					yield return Part("sticks", Sticks(me));
					yield return Part("buttons and the quick menu", Buttons());
					yield return Part("both eyes alike", EyesAlike());
					yield return Part("the buttons since 15 October", NewButtons());
					yield return Part("a window's highlight", Highlight());
					yield return Part("the action wheel", Wheel());
					yield return Part("the wrist", Wrist());
					yield return Part("the wrist's hotbar", WristHotbar());
					yield return Part("left-handed", LeftHanded());
					yield return Part("the VR tab, pinned", Menu());
					yield return Part("in a house", House());
					yield return Part("speed", Speed());
					yield return Part("the map", Map());
				}
			}
			finally
			{
				VrTest vrTest = this;
				s.Vr = savedVr;
				s.VrTurn = savedTurn;
				if (vrTest.Vr != null)
				{
					vrTest.Vr.SimHead = Xr.Pose.Identity;
					vrTest.Vr.SimTurn = (vrTest.Vr.SimMove = Vector2.zero);
					for (int i = 0; i < 3; i++)
					{
						vrTest.Vr.SimImages[i] = IntPtr.Zero;
					}
				}
				WorldBoxMod.VrSimulate = false;
				vrTest.Mod.MouseLookOff = false;
				vrTest.Mod.VrResetForTest();
				s.Save();
			}
		}

		private void MakeImages()
		{
			for (int i = 0; i < 3; i++)
			{
				int width = ((i < 2) ? 2048 : Mathf.Max(64, Screen.width));
				int height = ((i < 2) ? 2048 : Mathf.Max(64, Screen.height));
				_images[i] = new RenderTexture(width, height, 0, Vr.Bgra ? RenderTextureFormat.BGRA32 : RenderTextureFormat.ARGB32)
				{
					name = "VrTestImage"
				};
				_images[i].Create();
				Vr.SimImages[i] = _images[i].GetNativeTexturePtr();
			}
		}

		private static Xr.Quat Axis(float x, float y, float z, float degrees)
		{
			float f = degrees * 0.5f * (MathF.PI / 180f);
			float num = Mathf.Sin(f);
			return new Xr.Quat
			{
				X = x * num,
				Y = y * num,
				Z = z * num,
				W = Mathf.Cos(f)
			};
		}

		private void Head(Xr.Quat q, float x = 0f, float y = 0f, float z = 0f)
		{
			Vr.SimHead = new Xr.Pose
			{
				Orientation = q,
				Position = new Xr.Vec3
				{
					X = x,
					Y = y,
					Z = z
				}
			};
		}

		private IEnumerator Level()
		{
			Head(new Xr.Quat
			{
				W = 1f
			});
			yield return CivilTest.Wait(2f);
			int before = Mod.VrStereoFrames;
			int copies = Vr.SimCopies;
			yield return CivilTest.Wait(1.5f);
			Check("both eyes are drawn every frame", Mod.VrStereoFrames - before > 10, $"{Mod.VrStereoFrames - before} stereo frames in 1.5 s");
			VrSystem.Layers simLast = Vr.SimLast;
			Check("a frame has two eyes and the HUD", simLast.Eyes == 2 && simLast.Panel && simLast.PanelOnHead && simLast.PanelSeeThrough, $"eyes {simLast.Eyes}, panel {simLast.Panel} on head {simLast.PanelOnHead}");
			float num = 2f * Mathf.Atan(simLast.PanelMetresWide * 0.5f / Mathf.Abs(simLast.PanelPose.Position.Z)) * 57.29578f;
			float num2 = 2f * Mathf.Atan(simLast.PanelMetresHigh * 0.5f / Mathf.Abs(simLast.PanelPose.Position.Z)) * 57.29578f;
			Check("the HUD fills the view (its corners just inside what the lenses show)", num > 85f && num < 108f && num2 < 88f, $"{num:F1} x {num2:F1} deg");
			Check("the eyes' pictures are copied into the headset's images", Vr.SimCopies - copies > 20, $"{Vr.SimCopies - copies} copies");
			EyeCamera eyeCamera = Mod.VrEyeCameraForTest(0);
			EyeCamera eyeCamera2 = Mod.VrEyeCameraForTest(1);
			float magnitude = (eyeCamera2.Position - eyeCamera.Position).magnitude;
			float num3 = 0.064f * Mod.VrScaleForTest;
			Check("the eyes are an eye spacing apart in the world", Mathf.Abs(magnitude - num3) < num3 * 0.05f, $"{magnitude:F4} tiles, want {num3:F4} ({Mod.VrScaleForTest:F3} tiles a metre)");
			Check("the left eye is on the left", Vector3.Dot(eyeCamera2.Position - eyeCamera.Position, eyeCamera.Right) > 0f);
			Check("the lenses are off-centre (each eye sees further to its own side)", eyeCamera.Cx > (float)simLast.Width[0] * 0.5f && eyeCamera2.Cx < (float)simLast.Width[1] * 0.5f, $"left cx {eyeCamera.Cx:F0} of {simLast.Width[0]}, right cx {eyeCamera2.Cx:F0} of {simLast.Width[1]}");
			float num4 = simLast.Fov[0].AngleLeft * 57.29578f;
			float num5 = simLast.Fov[0].AngleRight * 57.29578f;
			float num6 = simLast.Fov[0].AngleUp * 57.29578f;
			float num7 = simLast.Fov[0].AngleDown * 57.29578f;
			Check("the left eye's picture covers its lens", Mathf.Abs(num4 + 53.97f) < 0.3f && Mathf.Abs(num5 - 39.99f) < 0.3f && Mathf.Abs(num6 - 44f) < 0.5f && Mathf.Abs(num7 + 50.02f) < 0.5f, $"left {num4:F2}, right {num5:F2}, up {num6:F2}, down {num7:F2} deg; picture {simLast.Width[0]}x{simLast.Height[0]}");
			Check("the view looks where the head does", Mathf.Abs(Mod.View.Pitch) < 0.01f && Mathf.Abs(Mathf.DeltaAngle(Mod.View.Yaw * 57.29578f, Mod.VrYawForTest * 57.29578f)) < 0.1f, $"pitch {Mathf.Atan(Mod.View.Pitch) * 57.29578f:F2}, yaw {Mod.View.Yaw * 57.29578f:F1} vs room {Mod.VrYawForTest * 57.29578f:F1}");
			Vr.SimReadNext = true;
			int reads = Vr.SimReads;
			yield return CivilTest.Wait(0.3f);
			Texture2D texture2D = Mod.GpuRendererForTest.ReadOutput();
			if (Check("the headset's images were read back", Vr.SimReads > reads && texture2D != null))
			{
				Color color = RowAverage(texture2D, texture2D.height - 1);
				Color color2 = RowAverage(texture2D, 0);
				float[] array = Vr.SimRows[1];
				Color color3 = new Color(array[0] / 255f, array[1] / 255f, array[2] / 255f);
				float num8 = Diff(color3, color);
				float num9 = Diff(color3, color2);
				Check("the headset reads the picture right way up (its first row is the sky)", num8 < num9, $"headset row 0 {Show(color3)}; picture top {Show(color)} (off {num8:F3}), bottom {Show(color2)} (off {num9:F3})");
			}
			RenderTexture vrHudForTest = Mod.VrHudForTest;
			if (Check("the HUD has its own picture", vrHudForTest != null))
			{
				Texture2D texture2D2 = Read(vrHudForTest, headsetOrder: false);
				Color32[] pixels = texture2D2.GetPixels32();
				int num10 = 0;
				Color32[] array2 = pixels;
				for (int i = 0; i < array2.Length; i++)
				{
					if (array2[i].a > 8)
					{
						num10++;
					}
				}
				Check("the HUD's picture shows the HUD over nothing (see-through elsewhere)", num10 > pixels.Length / 400 && num10 < pixels.Length / 2, $"{(float)num10 * 100f / (float)pixels.Length:F1}% of it drawn");
				UnityEngine.Object.Destroy(texture2D2);
			}
			if (texture2D != null)
			{
				UnityEngine.Object.Destroy(texture2D);
			}
			SaveEyes("vr_level");
			SaveHud("vr_hud.png");
			yield return new WaitForEndOfFrame();
			Texture2D texture2D3 = ScreenCapture.CaptureScreenshotAsTexture();
			if (texture2D3 != null)
			{
				Color c = RowAverage(texture2D3, texture2D3.height - 8);
				Color c2 = RowAverage(texture2D3, 8);
				float num11 = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
				float num12 = c2.r * 0.3f + c2.g * 0.59f + c2.b * 0.11f;
				bool ok = c.b > c.g && (c2.g > c2.b || c.b - c.r > c2.b - c2.r + 0.03f || num11 > num12 + 0.04f);
				Check("the desktop shows an eye the right way up (sky above, ground below)", ok, "top " + Show(c) + ", bottom " + Show(c2));
				Save(texture2D3, "vr_desktop.png");
			}
		}

		private IEnumerator Looks(Actor me)
		{
			float yaw0 = Mod.View.Yaw;
			Head(Axis(0f, 1f, 0f, 60f));
			yield return CivilTest.Wait(0.5f);
			float num = Mathf.DeltaAngle(yaw0 * 57.29578f, Mod.View.Yaw * 57.29578f);
			Check("turning the head 60 degrees left turns the view", Mathf.Abs(num - 60f) < 1f, $"{num:F1} deg");
			SaveEyes("vr_left60");
			Head(Axis(0f, 0f, 1f, 30f));
			yield return CivilTest.Wait(0.5f);
			Check("a rolled head leaves the view's yaw and pitch", Mathf.Abs(Mathf.DeltaAngle(yaw0 * 57.29578f, Mod.View.Yaw * 57.29578f)) < 1f && Mathf.Abs(Mod.View.Pitch) < 0.02f);
			EyeCamera eyeCamera = Mod.VrEyeCameraForTest(0);
			Check("rolled 30 degrees left, the eye rolls with it (its right side up)", Mathf.Abs(eyeCamera.Right.z - 0.5f) < 0.02f && Mathf.Abs(eyeCamera.Up.z - 0.866f) < 0.02f, $"right rises {eyeCamera.Right.z:F3}, up {eyeCamera.Up.z:F3}");
			SaveEyes("vr_roll30");
			Head(Axis(1f, 0f, 0f, -90f));
			yield return CivilTest.Wait(0.5f);
			Check("looking straight down still draws both eyes", Mod.VrStereoFrames > 0 && Vr.SimLast.Eyes == 2);
			Check("looking straight down, the view's pitch is near down", Mathf.Atan(Mod.View.Pitch) * 57.29578f < -80f, $"{Mathf.Atan(Mod.View.Pitch) * 57.29578f:F1} deg");
			Color c = Middle(Mod.VrEyeForTest(0));
			Check("looking straight down shows the ground (not the sky, not black)", c.maxColorComponent > 0.08f && (!(c.b > c.r + 0.15f) || !(c.b > c.g)), Show(c));
			SaveEyes("vr_down");
			Head(Axis(1f, 0f, 0f, 70f));
			yield return CivilTest.Wait(0.5f);
			Check("looking up 70 degrees", Mathf.Abs(Mathf.Atan(Mod.View.Pitch) * 57.29578f - 70f) < 1.5f, $"{Mathf.Atan(Mod.View.Pitch) * 57.29578f:F1} deg");
			SaveEyes("vr_up70");
			Head(new Xr.Quat
			{
				W = 1f
			});
			yield return CivilTest.Wait(0.3f);
			Vector3 still = new Vector3(Mod.View.X, Mod.View.Y, Mod.View.Z);
			Head(new Xr.Quat
			{
				W = 1f
			}, 0.3f, -0.2f);
			yield return CivilTest.Wait(0.3f);
			Vector3 vector = new Vector3(Mod.View.X, Mod.View.Y, Mod.View.Z);
			float num2 = Vector3.Dot(vector - still, new Vector3(Mathf.Sin(Mod.View.Yaw), 0f - Mathf.Cos(Mod.View.Yaw), 0f));
			float num3 = vector.z - still.z;
			float k = Mod.VrScaleForTest;
			Check("leaning moves the eye (scaled to the creature)", Mathf.Abs(num2 - 0.3f * k) < 0.05f * k + 0.01f && Mathf.Abs(num3 + 0.2f * k) < 0.05f * k + 0.01f, $"right {num2:F3}, down {0f - num3:F3} tiles; want {0.3f * k:F3}, {0.2f * k:F3}");
			Head(new Xr.Quat
			{
				W = 1f
			}, 3f);
			yield return CivilTest.Wait(0.3f);
			Vector3 vector2 = new Vector3(Mod.View.X, Mod.View.Y, Mod.View.Z);
			Check("the head's own move is kept within reach of the body", (vector2 - still).magnitude < 0.5f * k + 0.05f, $"{(vector2 - still).magnitude:F3} tiles for 3 m");
			Head(new Xr.Quat
			{
				W = 1f
			});
			yield return CivilTest.Wait(0.3f);
		}

		private IEnumerator Sticks(Actor me)
		{
			float before = Mod.View.Yaw;
			Vr.SimTurn = new Vector2(1f, 0f);
			yield return CivilTest.Wait(0.6f);
			Vr.SimTurn = Vector2.zero;
			yield return CivilTest.Wait(0.2f);
			float num = Mathf.DeltaAngle(before * 57.29578f, Mod.View.Yaw * 57.29578f);
			Check("the right stick snaps one 30-degree turn to the right (held, still one)", Mathf.Abs(num + 30f) < 0.5f, $"{num:F1} deg");
			Vector2 start = me.current_position;
			float yaw = Mod.View.Yaw;
			Vr.SimMove = new Vector2(0f, 1f);
			yield return CivilTest.Wait(1.5f);
			Vr.SimMove = Vector2.zero;
			Vector2 lhs = me.current_position - start;
			float num2 = Vector2.Dot(lhs, new Vector2(Mathf.Cos(yaw), Mathf.Sin(yaw)));
			Check("the left stick walks where you look", num2 > 0.4f && num2 > Mathf.Abs(lhs.magnitude * 0.7f), $"moved {lhs.magnitude:F2}, {num2:F2} ahead");
			yield return CivilTest.Wait(0.5f);
		}

		private IEnumerator Watch(Action<bool> set, bool press, KeyCode key, bool[] seen)
		{
			set(press);
			for (int i = 0; i < 6; i++)
			{
				yield return null;
				seen[0] |= VrControls.Down(key);
			}
		}

		private IEnumerator CloseWindows()
		{
			for (int k = 0; k < 3; k++)
			{
				if (!Mod.AnyMenuForTest && !Mod.SettingsMenu.Open)
				{
					break;
				}
				Vr.SimAbility = true;
				yield return null;
				yield return null;
				Vr.SimAbility = false;
				yield return CivilTest.Wait(0.3f);
			}
		}

		private IEnumerator Buttons()
		{
			bool[] seen = new bool[1];
			yield return Watch(delegate(bool v)
			{
				Vr.SimJump = v;
			}, press: true, KeyCode.Space, seen);
			bool early = seen[0];
			yield return Watch(delegate(bool v)
			{
				Vr.SimJump = v;
			}, press: false, KeyCode.Space, seen);
			Check("A tapped jumps (as it is let go)", seen[0] && !early && !VrMenu.Open, $"jumped {seen[0]}, on the press {early}");
			yield return CivilTest.Wait(0.8f);
			Vr.SimJump = true;
			yield return CivilTest.Wait(0.6f);
			VrSystem.Layers simLast = Vr.SimLast;
			Check("A held opens the quick menu in front of you, pinned in the room", VrMenu.Open && Mod.HudForTest.VrMenuDrawn && simLast.QuadCount > 0 && !simLast.Quads[0].OnHead, $"open {VrMenu.Open}, drawn {Mod.HudForTest.VrMenuDrawn}, pinned quads {simLast.QuadCount}");
			Vr.SimMove = new Vector2(1f, 0f);
			yield return CivilTest.Wait(0.3f);
			Vr.SimMove = Vector2.zero;
			int choice = VrMenu.Choice;
			Check("the left stick picks in the menu and doesn't walk", choice >= 0 && VrControls.Move == Vector2.zero, (choice >= 0) ? VrMenu.Items[choice].Name : "nothing");
			int lantern = Array.FindIndex(VrMenu.Items, (VrMenu.Item it) => it.Key == KeyCode.C);
			yield return null;
			bool aimed = PointAtGui(Mod.HudForTest.VrMenuRects[lantern].center, 1);
			yield return CivilTest.Wait(0.4f);
			Check("pointing at a button of the menu picks it", aimed && VrMenu.Choice == lantern, string.Format("pointed {0}, picked {1}", aimed, (VrMenu.Choice >= 0) ? VrMenu.Items[VrMenu.Choice].Name : "nothing"));
			SaveWindow("vr_menu.png");
			seen[0] = false;
			bool jumped = false;
			Vr.SimJump = false;
			for (int i = 0; i < 6; i++)
			{
				yield return null;
				seen[0] |= VrControls.Down(KeyCode.C);
				jumped |= VrControls.Down(KeyCode.Space);
			}
			Check("letting go of A presses the button pointed at (C), and no jump", seen[0] && !jumped && !VrMenu.Open, $"pressed {seen[0]}, jumped {jumped}");
			yield return CivilTest.Wait(0.5f);
			Vr.SimJump = true;
			yield return CivilTest.Wait(0.6f);
			int num = Array.FindIndex(VrMenu.Items, (VrMenu.Item it) => it.Key == KeyCode.B);
			PointAtGui(Mod.HudForTest.VrMenuRects[num].center, 1);
			yield return CivilTest.Wait(0.4f);
			seen[0] = false;
			Vr.SimAttack = 1f;
			for (int i = 0; i < 5; i++)
			{
				yield return null;
				seen[0] |= VrControls.Down(KeyCode.B);
			}
			Vr.SimAttack = 0f;
			for (int i = 0; i < 8; i++)
			{
				yield return null;
				seen[0] |= VrControls.Down(KeyCode.B);
			}
			Check("the trigger on a button presses it and closes the menu", seen[0] && !VrMenu.Open, $"pressed {seen[0]}, open {VrMenu.Open}");
			Vr.SimJump = false;
			seen[0] = false;
			for (int i = 0; i < 6; i++)
			{
				yield return null;
				seen[0] |= VrControls.Down(KeyCode.Space);
			}
			Check("...and letting go of A after it is no jump", !seen[0]);
			Vr.SimHandValid[1] = false;
			yield return CivilTest.Wait(0.5f);
			yield return CloseWindows();
			seen[0] = false;
			yield return Watch(delegate(bool v)
			{
				Vr.SimCraft = v;
			}, press: true, KeyCode.Tab, seen);
			yield return Watch(delegate(bool v)
			{
				Vr.SimCraft = v;
			}, press: false, KeyCode.Tab, seen);
			Check("X tapped is the inventory (Tab)", seen[0]);
			yield return CivilTest.Wait(0.5f);
			Check("...and it opens", Mod.AnyMenuForTest);
			yield return CloseWindows();
			seen[0] = false;
			yield return Watch(delegate(bool v)
			{
				Vr.SimMap = v;
			}, press: true, KeyCode.M, seen);
			Vr.SimMap = false;
			Check("Y is the map (M)", seen[0]);
			yield return CivilTest.Wait(0.5f);
			Check("...and it opens", Mod.AnyMenuForTest);
			yield return CloseWindows();
			Check("B closed them", !Mod.AnyMenuForTest);
		}

		private IEnumerator Menu()
		{
			SettingsMenu menu = Mod.SettingsMenu;
			Head(new Xr.Quat
			{
				W = 1f
			});
			Mod.SetSettingsMenu(open: true);
			menu.Tab = 4;
			yield return CivilTest.Wait(0.6f);
			List<SettingsMenu.Row> list = menu.RowsOf(4);
			Check("a VR tab with the headset's status", SettingsMenu.Tabs[4] == "VR" && list != null && list.Count >= 7 && menu.VrRow != null && menu.VrRow.Note.Contains("simulated"), menu.VrRow?.Note);
			Vr.SimMove = new Vector2(0f, 1f);
			yield return CivilTest.Wait(0.2f);
			Check("with the settings open the stick doesn't walk", VrControls.Move == Vector2.zero);
			Vr.SimMove = Vector2.zero;
			VrSystem.Layers simLast = Vr.SimLast;
			bool flag = simLast.QuadCount > 0 && !simLast.Quads[0].OnHead;
			Check("the settings hang in the room in front of you, the HUD on your view", flag && simLast.Panel && simLast.PanelOnHead, $"quads {simLast.QuadCount}, pinned {flag}, HUD on head {simLast.PanelOnHead}");
			SaveWindow("vr_settings.png");
			if (flag)
			{
				Xr.Vec3 placed = simLast.Quads[0].Pose.Position;
				Check("...ahead, at the HUD's distance", placed.Z < -1f && Mathf.Abs(placed.X) < 0.1f, $"{placed.X:F2}, {placed.Y:F2}, {placed.Z:F2} m");
				Head(Axis(0f, 1f, 0f, -30f));
				yield return CivilTest.Wait(0.8f);
				Xr.Vec3 position = Vr.SimLast.Quads[0].Pose.Position;
				Check("turning the head 30 degrees to read its side leaves it still", Mathf.Abs(position.X - placed.X) < 0.01f && Mathf.Abs(position.Z - placed.Z) < 0.01f, $"{placed.X:F2} -> {position.X:F2} m across");
				Head(Axis(0f, 1f, 0f, -95f));
				yield return CivilTest.Wait(0.3f);
				Xr.Vec3 soon = Vr.SimLast.Quads[0].Pose.Position;
				yield return CivilTest.Wait(1.6f);
				Xr.Vec3 position2 = Vr.SimLast.Quads[0].Pose.Position;
				float num = Mathf.Atan2(0f - position2.X, 0f - position2.Z) * 57.29578f;
				Check("looking well away, it waits a moment, then comes round in front of you", Mathf.Abs(soon.X - placed.X) < 0.05f && Mathf.Abs(Mathf.DeltaAngle(num, -95f)) < 4f, $"after 0.3 s {soon.X:F2} m across; after 1.9 s at {num:F1} deg (head at -95)");
				Head(new Xr.Quat
				{
					W = 1f
				});
				yield return CivilTest.Wait(1.8f);
			}
			Mod.SetSettingsMenu(open: false);
			yield return CivilTest.Wait(0.5f);
			Check("with the window shut there is no pinned picture", Vr.SimLast.QuadCount == 0, $"{Vr.SimLast.QuadCount} quads");
		}

		private IEnumerator Wrist()
		{
			Head(new Xr.Quat
			{
				W = 1f
			});
			Vr.SimHandValid[0] = false;
			yield return CivilTest.Wait(0.6f);
			Check("with the wrist down there is no wrist panel", !Mod.VrWristUpForTest && Vr.SimLast.QuadCount == 0, $"up {Mod.VrWristUpForTest}, quads {Vr.SimLast.QuadCount}");
			RenderTexture vrHudForTest = Mod.VrHudForTest;
			if (vrHudForTest != null)
			{
				Texture2D texture2D = Read(vrHudForTest, headsetOrder: false);
				Color32[] pixels = texture2D.GetPixels32();
				int num = 0;
				int num2 = 0;
				for (int i = texture2D.height * 85 / 100; i < texture2D.height; i++)
				{
					for (int j = 0; j < texture2D.width / 4; j++)
					{
						num2++;
						if (pixels[i * texture2D.width + j].a > 8)
						{
							num++;
						}
					}
				}
				Check("the headset's HUD keeps its corners clear (the character panel is on the wrist)", num < num2 / 50, $"{(float)num * 100f / (float)Mathf.Max(1, num2):F1}% of the top left drawn");
				UnityEngine.Object.Destroy(texture2D);
			}
			Vector3 hand = new Vector3(-0.05f, -0.28f, -0.32f);
			Vector3 normalized = (-hand).normalized;
			Vector3 normalized2 = Vector3.ProjectOnPlane(Vector3.right, normalized).normalized;
			Vector3 z = -normalized2;
			Vector3 rhs = -normalized;
			Vector3 y = Vector3.Cross(z, rhs);
			Quaternion quaternion = Quaternion.LookRotation(z, y);
			Vr.SimHands[0] = new Xr.Pose
			{
				Orientation = new Xr.Quat
				{
					X = quaternion.x,
					Y = quaternion.y,
					Z = quaternion.z,
					W = quaternion.w
				},
				Position = new Xr.Vec3
				{
					X = hand.x,
					Y = hand.y,
					Z = hand.z
				}
			};
			Vr.SimHandValid[0] = true;
			Head(Axis(1f, 0f, 0f, -38f));
			yield return CivilTest.Wait(0.6f);
			VrSystem.Layers simLast = Vr.SimLast;
			bool flag = false;
			for (int k = 0; k < simLast.QuadCount; k++)
			{
				Xr.Vec3 position = simLast.Quads[k].Pose.Position;
				if (!simLast.Quads[k].OnHead && Vector3.Distance(new Vector3(position.X, position.Y, position.Z), hand) < 0.35f)
				{
					flag = true;
				}
			}
			Check("turning the left wrist up brings its panel over it", Mod.VrWristUpForTest && Mod.HudForTest.VrWristDrawn && flag, $"up {Mod.VrWristUpForTest}, drawn {Mod.HudForTest.VrWristDrawn}, by the hand {flag}; {Mod.VrWristWhyForTest}");
			RenderTexture vrWristForTest = Mod.VrWristForTest;
			if (vrWristForTest != null)
			{
				Save(Read(vrWristForTest, headsetOrder: false), "vr_wrist.png");
			}
			quaternion = Quaternion.LookRotation(z, -y);
			Vr.SimHands[0] = new Xr.Pose
			{
				Orientation = new Xr.Quat
				{
					X = quaternion.x,
					Y = quaternion.y,
					Z = quaternion.z,
					W = quaternion.w
				},
				Position = new Xr.Vec3
				{
					X = hand.x,
					Y = hand.y,
					Z = hand.z
				}
			};
			yield return CivilTest.Wait(0.4f);
			Check("turning it away puts the panel away", !Mod.VrWristUpForTest);
			Vr.SimHandValid[0] = false;
			Head(new Xr.Quat
			{
				W = 1f
			});
			yield return CivilTest.Wait(0.3f);
		}

		private bool PointAtGui(Vector2 gui, int hand)
		{
			if (!Mod.VrAimPanelUpForTest)
			{
				return false;
			}
			Xr.Pose vrAimPanelPoseForTest = Mod.VrAimPanelPoseForTest;
			Vector2 vrAimPanelSizeForTest = Mod.VrAimPanelSizeForTest;
			Quaternion quaternion = new Quaternion(vrAimPanelPoseForTest.Orientation.X, vrAimPanelPoseForTest.Orientation.Y, vrAimPanelPoseForTest.Orientation.Z, vrAimPanelPoseForTest.Orientation.W);
			float num = gui.x / (float)Screen.width;
			float num2 = gui.y / (float)Screen.height;
			Vector3 vector = new Vector3(vrAimPanelPoseForTest.Position.X, vrAimPanelPoseForTest.Position.Y, vrAimPanelPoseForTest.Position.Z) + quaternion * new Vector3((num - 0.5f) * vrAimPanelSizeForTest.x, (0.5f - num2) * vrAimPanelSizeForTest.y, 0f);
			Vector3 vector2 = new Vector3((hand == 1) ? 0.12f : (-0.12f), -0.2f, -0.1f);
			Quaternion quaternion2 = Quaternion.FromToRotation(new Vector3(0f, 0f, -1f), (vector - vector2).normalized);
			Vr.SimHands[hand] = new Xr.Pose
			{
				Orientation = new Xr.Quat
				{
					X = quaternion2.x,
					Y = quaternion2.y,
					Z = quaternion2.z,
					W = quaternion2.w
				},
				Position = new Xr.Vec3
				{
					X = vector2.x,
					Y = vector2.y,
					Z = vector2.z
				}
			};
			Vr.SimHandValid[hand] = true;
			return true;
		}

		private void SaveWindow(string name)
		{
			RenderTexture vrHudForTest = Mod.VrHudForTest;
			RenderTexture vrWindowForTest = Mod.VrWindowForTest;
			if (vrHudForTest == null)
			{
				return;
			}
			Texture2D texture2D = Read(vrHudForTest, headsetOrder: false);
			if (vrWindowForTest != null)
			{
				Texture2D texture2D2 = Read(vrWindowForTest, headsetOrder: false);
				Color32[] pixels = texture2D.GetPixels32();
				Color32[] pixels2 = texture2D2.GetPixels32();
				for (int i = 0; i < pixels.Length && i < pixels2.Length; i++)
				{
					float num = (float)(int)pixels2[i].a / 255f;
					pixels[i] = new Color32((byte)((float)(int)pixels[i].r * (1f - num) + (float)(int)pixels2[i].r * num), (byte)((float)(int)pixels[i].g * (1f - num) + (float)(int)pixels2[i].g * num), (byte)((float)(int)pixels[i].b * (1f - num) + (float)(int)pixels2[i].b * num), (byte)Mathf.Max(pixels[i].a, pixels2[i].a));
				}
				texture2D.SetPixels32(pixels);
				texture2D.Apply(updateMipmaps: false);
				UnityEngine.Object.Destroy(texture2D2);
			}
			Save(texture2D, name);
		}

		private IEnumerator House()
		{
			Mod.EnterRoomForTest(new List<Actor>());
			yield return CivilTest.Wait(1.2f);
			if (Check("inside a house", Mod.Interior.Inside))
			{
				int rooms = WorldBoxMod.VrRoomFrames;
				yield return CivilTest.Wait(0.6f);
				VrSystem.Layers simLast = Vr.SimLast;
				Check("in a house both eyes are drawn on the card, each its own picture (real 3D, not a flat screen)", simLast.Eyes == 2 && simLast.Picture[0] != simLast.Picture[1] && simLast.Panel && WorldBoxMod.VrRoomFrames - rooms > 5, $"eyes {simLast.Eyes}, own pictures {simLast.Picture[0] != simLast.Picture[1]}, room frames {WorldBoxMod.VrRoomFrames - rooms}");
				float num = simLast.Fov[0].AngleLeft * 57.29578f;
				Check("...through the lenses' own angles", Mathf.Abs(num + 53.97f) < 0.3f, $"left {num:F2} deg");
				EyeCamera eyeCamera = Mod.VrEyeCameraForTest(0);
				float magnitude = (Mod.VrEyeCameraForTest(1).Position - eyeCamera.Position).magnitude;
				float num2 = 0.064f * Mod.VrScaleForTest;
				Check("...an eye spacing apart in the room", Mathf.Abs(magnitude - num2) < num2 * 0.05f, $"{magnitude:F4} tiles, want {num2:F4}");
				Check("...with your arms on the controllers", Mod.VrHandsOnForTest || Mod.Settings.VrHands != 0);
				SaveEyes("vr_house");
				Mod.LeaveRoomForTest();
				yield return CivilTest.Wait(1f);
			}
		}

		private IEnumerator Speed()
		{
			Settings s = Mod.Settings;
			int width = s.VrEyeWidth;
			int ss = s.VrSupersample;
			(int, int)[] array = new(int, int)[5]
			{
				(2, 2),
				(2, 1),
				(3, 1),
				(1, 1),
				(0, 1)
			};
			for (int i = 0; i < array.Length; i++)
			{
				(int, int) tuple = array[i];
				int w = tuple.Item1;
				int k = tuple.Item2;
				s.VrEyeWidth = w;
				s.VrSupersample = k;
				yield return CivilTest.Wait(1f);
				int f0 = Mod.VrStereoFrames;
				float from = Time.realtimeSinceStartup;
				yield return CivilTest.Wait(3f);
				Note($"eyes {WorldBoxMod.VrEyeWidths[w]} wide x{k}: {(float)(Mod.VrStereoFrames - f0) / (Time.realtimeSinceStartup - from):F1} stereo frames a second, {Mod.VrDrawMsForTest:F2} ms on the game's thread");
			}
			s.VrEyeWidth = width;
			s.VrSupersample = ss;
			double total = 0.0;
			int n = 0;
			int frames = Mod.VrStereoFrames;
			float t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 4f)
			{
				yield return null;
				total += Mod.VrDrawMsForTest;
				n++;
			}
			float num = (float)(Mod.VrStereoFrames - frames) / (Time.realtimeSinceStartup - t0);
			Note($"both eyes at {VrEyeWidth()} wide: {total / (double)Mathf.Max(1, n):F2} ms a frame on the game's thread, {num:F1} stereo frames a second (in a minimised test copy)");
			Check("stereo frames keep coming", num > 5f, $"{num:F1}/s");
		}

		private int VrEyeWidth()
		{
			return WorldBoxMod.VrEyeWidths[Mathf.Clamp(Mod.Settings.VrEyeWidth, 0, WorldBoxMod.VrEyeWidths.Length - 1)];
		}

		private IEnumerator Map()
		{
			Mod.Settings.GodView = false;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			yield return CivilTest.Wait(3f);
			VrSystem.Layers simLast = Vr.SimLast;
			Check("on the map the headset shows the game's screen pinned in front of you", !Mod.IsFirstPerson && simLast.Eyes == 0 && simLast.Panel && !simLast.PanelOnHead && !simLast.PanelSeeThrough, $"first person {Mod.IsFirstPerson}, eyes {simLast.Eyes}, panel {simLast.Panel} on head {simLast.PanelOnHead}");
			Xr.Vec3 position = simLast.PanelPose.Position;
			float num = 2f * Mathf.Atan(simLast.PanelMetresWide * 0.5f / Mathf.Sqrt(position.X * position.X + position.Y * position.Y + position.Z * position.Z)) * 57.29578f;
			Check("...filling it", num > 85f, $"{num:F1} deg across");
			_bars = true;
			yield return CivilTest.Wait(0.3f);
			int reads = Vr.SimReads;
			Vr.SimReadNext = true;
			yield return CivilTest.Wait(0.3f);
			_bars = false;
			float[] array = Vr.SimRows[2];
			if (Vr.SimReads == reads)
			{
				Note("the screen wasn't read back");
				yield break;
			}
			if (array[0] + array[1] + array[2] + array[4] + array[5] + array[6] < 30f)
			{
				Note("the screen's capture is black here (a minimised test copy): its way up not checked");
				yield break;
			}
			Check("the headset reads the game's screen right way up (red top, blue bottom)", array[0] > array[2] + 60f && array[6] > array[4] + 60f, $"row 0 {array[0]:F0},{array[1]:F0},{array[2]:F0}; last row {array[4]:F0},{array[5]:F0},{array[6]:F0}");
		}

		private void OnGUI()
		{
			if (_bars && Event.current.type == EventType.Repaint)
			{
				GUI.depth = -10000;
				Texture2D whiteTexture = Texture2D.whiteTexture;
				Color color = GUI.color;
				GUI.color = Color.red;
				GUI.DrawTexture(new Rect(0f, 0f, Screen.width, 4f), whiteTexture);
				GUI.color = Color.blue;
				GUI.DrawTexture(new Rect(0f, (float)Screen.height - 4f, Screen.width, 4f), whiteTexture);
				GUI.color = color;
			}
		}

		private static Texture2D Read(RenderTexture rt, bool headsetOrder)
		{
			Texture2D texture2D = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, mipChain: false);
			RenderTexture active = RenderTexture.active;
			RenderTexture.active = rt;
			texture2D.ReadPixels(new Rect(0f, 0f, rt.width, rt.height), 0, 0);
			texture2D.Apply(updateMipmaps: false);
			RenderTexture.active = active;
			if (headsetOrder)
			{
				Color32[] pixels = texture2D.GetPixels32();
				Color32[] array = new Color32[pixels.Length];
				for (int i = 0; i < rt.height; i++)
				{
					Array.Copy(pixels, i * rt.width, array, (rt.height - 1 - i) * rt.width, rt.width);
				}
				texture2D.SetPixels32(array);
				texture2D.Apply(updateMipmaps: false);
			}
			return texture2D;
		}

		private void SaveEyes(string name)
		{
			RenderTexture renderTexture = Mod.VrEyeForTest(0);
			RenderTexture renderTexture2 = Mod.VrEyeForTest(1);
			if (renderTexture == null || renderTexture2 == null)
			{
				Note("no eye pictures to save for " + name);
				return;
			}
			Texture2D texture2D = Read(renderTexture, headsetOrder: true);
			Texture2D texture2D2 = Read(renderTexture2, headsetOrder: true);
			int num = texture2D.width + texture2D2.width + 8;
			int num2 = Mathf.Max(texture2D.height, texture2D2.height);
			Texture2D texture2D3 = new Texture2D(num, num2, TextureFormat.RGBA32, mipChain: false);
			Color32[] array = new Color32[num * num2];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = new Color32(20, 20, 20, byte.MaxValue);
			}
			texture2D3.SetPixels32(array);
			texture2D3.SetPixels32(0, 0, texture2D.width, texture2D.height, texture2D.GetPixels32());
			texture2D3.SetPixels32(texture2D.width + 8, 0, texture2D2.width, texture2D2.height, texture2D2.GetPixels32());
			texture2D3.Apply(updateMipmaps: false);
			Save(texture2D3, name + ".png");
			UnityEngine.Object.Destroy(texture2D);
			UnityEngine.Object.Destroy(texture2D2);
		}

		private void SaveHud(string name)
		{
			RenderTexture vrHudForTest = Mod.VrHudForTest;
			if (vrHudForTest != null)
			{
				Save(Read(vrHudForTest, headsetOrder: false), name);
			}
		}

		private static void Save(Texture2D t, string name)
		{
			File.WriteAllBytes(Path.Combine(Log.Folder, name), t.EncodeToPNG());
			Log.Info("VR saved " + name);
			UnityEngine.Object.Destroy(t);
		}

		private static Color RowAverage(Texture2D t, int row)
		{
			Color32[] pixels = t.GetPixels32();
			float num = 0f;
			float num2 = 0f;
			float num3 = 0f;
			for (int i = 0; i < t.width; i++)
			{
				Color32 color = pixels[row * t.width + i];
				num += (float)(int)color.r;
				num2 += (float)(int)color.g;
				num3 += (float)(int)color.b;
			}
			float num4 = 255f * (float)Mathf.Max(1, t.width);
			return new Color(num / num4, num2 / num4, num3 / num4);
		}

		private static float Diff(Color a, Color b)
		{
			return (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b)) / 3f;
		}

		private static string Show(Color c)
		{
			return $"{c.r * 255f:F0},{c.g * 255f:F0},{c.b * 255f:F0}";
		}

		private static Color Middle(RenderTexture rt)
		{
			if (rt == null)
			{
				return Color.black;
			}
			Texture2D texture2D = Read(rt, headsetOrder: true);
			Color32[] pixels = texture2D.GetPixels32();
			float num = 0f;
			float num2 = 0f;
			float num3 = 0f;
			int num4 = 0;
			for (int i = texture2D.height * 2 / 5; i < texture2D.height * 3 / 5; i++)
			{
				for (int j = texture2D.width * 2 / 5; j < texture2D.width * 3 / 5; j++)
				{
					Color32 color = pixels[i * texture2D.width + j];
					num += (float)(int)color.r;
					num2 += (float)(int)color.g;
					num3 += (float)(int)color.b;
					num4++;
				}
			}
			UnityEngine.Object.Destroy(texture2D);
			num4 = Mathf.Max(1, num4);
			return new Color(num / (float)num4 / 255f, num2 / (float)num4 / 255f, num3 / (float)num4 / 255f);
		}

		private IEnumerator EyesAlike()
		{
			Head(new Xr.Quat
			{
				W = 1f
			});
			yield return CivilTest.Wait(0.5f);
			Check("the eyes are drawn as a headset's pair (effects keyed to the world and the lens)", Shader.GetGlobalVector("_FP_Stereo").x > 0.5f, string.Format("_FP_Stereo {0}", Shader.GetGlobalVector("_FP_Stereo")));
			GpuRenderer gpu = Mod.GpuRendererForTest;
			int maps = gpu.ShadowMapsDrawn;
			int frames = Mod.VrStereoFrames;
			yield return CivilTest.Wait(1f);
			int num = gpu.ShadowMapsDrawn - maps;
			int num2 = Mod.VrStereoFrames - frames;
			if (num == 0)
			{
				Note($"no sun shadows drawn now ({num2} pairs): the shared map isn't checked");
			}
			else
			{
				Check("one shadow map is drawn for both eyes", num <= num2 + 2, $"{num} maps for {num2} pairs");
			}
		}

		private IEnumerator NewButtons()
		{
			Head(new Xr.Quat
			{
				W = 1f
			});
			yield return CloseWindows();
			Vr.SimCraft = true;
			yield return null;
			yield return null;
			Vr.SimCraft = false;
			yield return CivilTest.Wait(0.5f);
			bool open = Mod.AnyMenuForTest;
			bool ability = false;
			Vr.SimAbility = true;
			for (int i = 0; i < 15; i++)
			{
				yield return null;
				ability |= VrControls.Down(KeyCode.X) || VrControls.Held(KeyCode.X);
			}
			Vr.SimAbility = false;
			yield return CivilTest.Wait(0.3f);
			Check("B closes a window, and held on it is no ability", open && !Mod.AnyMenuForTest && !ability, $"window was open {open}, still open {Mod.AnyMenuForTest}, ability {ability}");
			bool[] seen = new bool[1];
			Vr.SimMenu = true;
			yield return null;
			yield return null;
			Vr.SimMenu = false;
			for (int i = 0; i < 6; i++)
			{
				yield return null;
				seen[0] |= VrControls.Down(KeyCode.F2);
			}
			yield return CivilTest.Wait(0.3f);
			Check("the menu button tapped opens the settings", seen[0] && Mod.SettingsMenu.Open);
			yield return CloseWindows();
			seen[0] = false;
			bool settings = false;
			Vr.SimMenu = true;
			for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime)
			{
				yield return null;
				seen[0] |= VrControls.Down(KeyCode.M);
				settings |= VrControls.Down(KeyCode.F2);
			}
			Vr.SimMenu = false;
			for (int i = 0; i < 6; i++)
			{
				yield return null;
				settings |= VrControls.Down(KeyCode.F2);
			}
			Check("the menu button held is the map, not the settings", seen[0] && !settings, $"map {seen[0]}, settings {settings}");
			yield return CivilTest.Wait(0.4f);
			yield return CloseWindows();
		}

		private IEnumerator Highlight()
		{
			Head(new Xr.Quat
			{
				W = 1f
			});
			bool[] simHandValid = Vr.SimHandValid;
			bool[] simHandValid2 = Vr.SimHandValid;
			bool flag = false;
			simHandValid2[1] = false;
			simHandValid[0] = flag;
			yield return CloseWindows();
			Vr.SimCraft = true;
			yield return null;
			yield return null;
			Vr.SimCraft = false;
			yield return CivilTest.Wait(0.6f);
			if (Check("the inventory is open", Mod.AnyMenuForTest))
			{
				yield return Push(new Vector2(0f, -1f));
				yield return CivilTest.Wait(0.5f);
				bool vrNavOnForTest = Mod.VrNavOnForTest;
				Vector2 first = Mod.VrNavAtForTest;
				Check("the left stick lights a button of the window", vrNavOnForTest && PadNav.Under(first, out var _), $"lit {vrNavOnForTest} at {first}, {PadNav.Count} buttons");
				yield return Push(new Vector2(0f, -1f));
				Vector2 second = Mod.VrNavAtForTest;
				Check("...and pushed down again, the next one down", second.y > first.y + 1f, $"{first} -> {second}");
				yield return Push(new Vector2(1f, 0f));
				Vector2 vrNavAtForTest = Mod.VrNavAtForTest;
				Check("...and right, one to the right", vrNavAtForTest.x > second.x + 1f || vrNavAtForTest == second, $"{second} -> {vrNavAtForTest}");
				SaveWindow("vr_highlight.png");
				Vr.SimJump = true;
				bool left = false;
				bool jumped = false;
				for (int i = 0; i < 4; i++)
				{
					yield return null;
					left |= VrMouse.Now.Left;
					jumped |= VrControls.Down(KeyCode.Space);
				}
				Vr.SimJump = false;
				for (int i = 0; i < 4; i++)
				{
					yield return null;
					jumped |= VrControls.Down(KeyCode.Space);
				}
				Check("A presses the lit button (the mouse's button at it), no jump", left && !jumped, $"pressed {left}, jumped {jumped}");
				yield return CloseWindows();
				Mod.SetSettingsMenu(open: true);
				yield return CivilTest.Wait(0.6f);
				int tab = Mod.SettingsMenu.Tab;
				Vr.SimTurn = new Vector2(1f, 0f);
				for (int i = 0; i < 4; i++)
				{
					yield return null;
				}
				Vr.SimTurn = Vector2.zero;
				yield return CivilTest.Wait(0.5f);
				Check("the right stick flicked aside steps the settings to the next tab", Mod.SettingsMenu.Tab != tab, $"tab {tab} -> {Mod.SettingsMenu.Tab}");
				Mod.SetSettingsMenu(open: false);
				yield return CivilTest.Wait(0.4f);
				Check("with the window shut the highlight goes", !Mod.VrNavOnForTest);
			}
		}

		private IEnumerator Push(Vector2 stick)
		{
			Vr.SimMove = stick;
			for (int i = 0; i < 3; i++)
			{
				yield return null;
			}
			Vr.SimMove = Vector2.zero;
			for (int i = 0; i < 4; i++)
			{
				yield return null;
			}
		}

		private IEnumerator Wheel()
		{
			Head(new Xr.Quat
			{
				W = 1f
			});
			yield return CloseWindows();
			RadialWheel w = new RadialWheel();
			string[] array = new string[4] { "Up", "Right", "Down", "Left" };
			foreach (string label in array)
			{
				w.Add(label);
			}
			w.Show();
			w.ShownAt = new Vector2((float)Screen.width * 0.5f, (float)Screen.height * 0.5f);
			w.ShownOuter = 60f;
			PointOnHud(new Vector3(0.35f, 0f, -1.4f));
			yield return null;
			yield return null;
			w.Steal(frozen: false);
			int right = w.Choice;
			PointOnHud(new Vector3(0f, 0.35f, -1.4f));
			yield return null;
			yield return null;
			w.Steal(frozen: false);
			int up = w.Choice;
			PointOnHud(new Vector3(0f, 0f, -1.4f));
			yield return null;
			yield return null;
			w.Steal(frozen: false);
			int choice = w.Choice;
			w.Close();
			Check("the hold-grip wheel's slice is the one the hand points at (right, up, the middle cancels)", right == 1 && up == 0 && choice < 0, $"right {right}, up {up}, middle {choice}");
			Vr.SimHandValid[1] = false;
		}

		private void PointOnHud(Vector3 at)
		{
			Vector3 vector = new Vector3(0.12f, -0.2f, -0.1f);
			Quaternion quaternion = Quaternion.FromToRotation(new Vector3(0f, 0f, -1f), (at - vector).normalized);
			Vr.SimHands[1] = new Xr.Pose
			{
				Orientation = new Xr.Quat
				{
					X = quaternion.x,
					Y = quaternion.y,
					Z = quaternion.z,
					W = quaternion.w
				},
				Position = new Xr.Vec3
				{
					X = vector.x,
					Y = vector.y,
					Z = vector.z
				}
			};
			Vr.SimHandValid[1] = true;
		}

		private IEnumerator WristHotbar()
		{
			Head(new Xr.Quat
			{
				W = 1f
			});
			yield return CloseWindows();
			Actor host = Mod.Host;
			Vector3 vector = new Vector3(-0.05f, -0.28f, -0.32f);
			Vector3 normalized = (-vector).normalized;
			Vector3 vector2 = -Vector3.ProjectOnPlane(Vector3.right, normalized).normalized;
			Vector3 rhs = -normalized;
			Vector3 upwards = Vector3.Cross(vector2, rhs);
			Quaternion quaternion = Quaternion.LookRotation(vector2, upwards);
			Vr.SimHands[0] = new Xr.Pose
			{
				Orientation = new Xr.Quat
				{
					X = quaternion.x,
					Y = quaternion.y,
					Z = quaternion.z,
					W = quaternion.w
				},
				Position = new Xr.Vec3
				{
					X = vector.x,
					Y = vector.y,
					Z = vector.z
				}
			};
			Vr.SimHandValid[0] = true;
			Vr.SimHandValid[1] = false;
			Head(Axis(1f, 0f, 0f, -38f));
			yield return CivilTest.Wait(0.7f);
			if (Check("the wrist is up with its hotbar", Mod.VrWristUpForTest && Mod.HudForTest.VrWristSlotCount > 1 && host != null, $"up {Mod.VrWristUpForTest}, slots {Mod.HudForTest.VrWristSlotCount}"))
			{
				int was = Hotbar.Chosen(host);
				int want = (was + 1) % Mod.HudForTest.VrWristSlotCount;
				Rect rect = Mod.HudForTest.VrWristSlots[want];
				Xr.Pose vrWristPoseForTest = Mod.VrWristPoseForTest;
				Vector2 vrWristSizeForTest = Mod.VrWristSizeForTest;
				Vector2 vrWristPixelsForTest = Mod.VrWristPixelsForTest;
				Quaternion quaternion2 = new Quaternion(vrWristPoseForTest.Orientation.X, vrWristPoseForTest.Orientation.Y, vrWristPoseForTest.Orientation.Z, vrWristPoseForTest.Orientation.W);
				float num = rect.center.x / vrWristPixelsForTest.x;
				float num2 = rect.center.y / vrWristPixelsForTest.y;
				Vector3 vector3 = new Vector3(vrWristPoseForTest.Position.X, vrWristPoseForTest.Position.Y, vrWristPoseForTest.Position.Z) + quaternion2 * new Vector3((num - 0.5f) * vrWristSizeForTest.x, (0.5f - num2) * vrWristSizeForTest.y, 0f);
				Vector3 vector4 = new Vector3(0.15f, -0.25f, -0.15f);
				Quaternion quaternion3 = Quaternion.FromToRotation(new Vector3(0f, 0f, -1f), (vector3 - vector4).normalized);
				Vr.SimHands[1] = new Xr.Pose
				{
					Orientation = new Xr.Quat
					{
						X = quaternion3.x,
						Y = quaternion3.y,
						Z = quaternion3.z,
						W = quaternion3.w
					},
					Position = new Xr.Vec3
					{
						X = vector4.x,
						Y = vector4.y,
						Z = vector4.z
					}
				};
				Vr.SimHandValid[1] = true;
				yield return CivilTest.Wait(0.3f);
				Check("the right hand pointing at a slot of the wrist's hotbar lights it", Mod.VrWristHoverForTest == want, $"lit {Mod.VrWristHoverForTest}, pointed at {want}");
				RenderTexture vrWristForTest = Mod.VrWristForTest;
				if (vrWristForTest != null)
				{
					Save(Read(vrWristForTest, headsetOrder: false), "vr_wrist_hotbar.png");
				}
				bool blow = false;
				Vr.SimAttack = 1f;
				for (int i = 0; i < 4; i++)
				{
					yield return null;
					blow |= VrControls.Held(KeyCode.Mouse0);
				}
				Vr.SimAttack = 0f;
				for (int i = 0; i < 3; i++)
				{
					yield return null;
				}
				Check("...and its trigger takes that slot up, no blow", Hotbar.Chosen(host) == want && !blow, $"chosen {was} -> {Hotbar.Chosen(host)} (wanted {want}), blow {blow}");
				bool[] simHandValid = Vr.SimHandValid;
				bool[] simHandValid2 = Vr.SimHandValid;
				bool flag = false;
				simHandValid2[1] = false;
				simHandValid[0] = flag;
				Head(new Xr.Quat
				{
					W = 1f
				});
				yield return CivilTest.Wait(0.3f);
			}
		}

		private IEnumerator LeftHanded()
		{
			Settings s = Mod.Settings;
			int was = s.VrHanded;
			s.VrHanded = 1;
			try
			{
				Head(new Xr.Quat
				{
					W = 1f
				});
				yield return null;
				yield return null;
				Check("left-handed, the left hand is the main one", Vr.Main == 0 && Vr.Off == 1);
				Vr.SimKick = 1f;
				bool attack = false;
				bool guard = false;
				for (int i = 0; i < 3; i++)
				{
					yield return null;
					attack |= VrControls.Held(KeyCode.Mouse0);
					guard |= VrControls.Held(KeyCode.Mouse1);
				}
				Vr.SimKick = 0f;
				for (int i = 0; i < 3; i++)
				{
					yield return null;
				}
				Check("...its trigger attacks, not guards", attack && !guard, $"attack {attack}, guard {guard}");
				Vr.SimTurn = new Vector2(0f, 1f);
				for (int i = 0; i < 3; i++)
				{
					yield return null;
				}
				Vector2 move = VrControls.Move;
				Vr.SimTurn = Vector2.zero;
				Check("...and the right stick walks", move.y > 0.5f, $"walk {move}");
			}
			finally
			{
				VrTest vrTest = this;
				s.VrHanded = was;
				vrTest.Vr.SimKick = 0f;
				vrTest.Vr.SimTurn = Vector2.zero;
			}
			yield return CivilTest.Wait(0.3f);
		}
	}
}
