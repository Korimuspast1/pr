using System;
using System.Collections;
using System.IO;
using FirstPerson.Pad;
using FirstPerson.Vr;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class PadTest : CivilTest
	{
		private static readonly XInput.State Idle = new XInput.State
		{
			Connected = true
		};

		protected override string Prefix => "PAD";

		protected override IEnumerator Body()
		{
			Note("reader: " + PadControls.Describe() + ", screen " + Screen.width + "x" + Screen.height);
			bool controllerWas = Mod.Settings.Controller;
			Mod.Settings.Controller = true;
			WorldTile worldTile = CivilTest.Stage();
			if (!Check("a stage", worldTile != null))
			{
				yield break;
			}
			CivilTest.ClearBox(worldTile.x - 16, worldTile.y - 16, worldTile.x + 16, worldTile.y + 16);
			Actor host = CivilTest.SpawnNear("human", new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f));
			if (Check("a human to be", host != null))
			{
				try
				{
					((BaseSimObject)host).addStatusEffect("invincible", 600f, pColorEffect: false);
				}
				catch
				{
				}
				yield return Possess(host);
				if (Check("first person on them", InControl(host)))
				{
					Mod.EndConversation();
					PadControls.Simulated = Idle;
					yield return CivilTest.Wait(0.3f);
					Check("a pad connected is read", PadControls.Active, PadControls.Describe());
					Check("first person has it", Mod.PadModeForTest == PadControls.Mode.Play, Mod.PadModeForTest.ToString());
					yield return Part("the left stick walks", Walk(host));
					yield return Part("L3 runs until you stop", Run(host));
					yield return Part("the right stick looks", Look());
					yield return Part("buttons press their keys", Buttons());
					yield return Part("prompts name the pad's buttons", Prompts());
					yield return Part("the trackpad's swipes step through the tools", Tools());
					yield return Part("the quick menu", QuickMenu());
					yield return Part("the settings with the D-pad", Settings());
					yield return Part("the map", Map());
					PadControls.Simulated = null;
					PadControls.DeckForTest = false;
					Mod.Settings.Controller = controllerWas;
				}
			}
		}

		private IEnumerator Hold(XInput.State s, float seconds)
		{
			s.Connected = true;
			PadControls.Simulated = s;
			yield return CivilTest.Wait(seconds);
		}

		private IEnumerator Tap(XInput.Buttons b)
		{
			yield return Hold(new XInput.State
			{
				Held = b
			}, 0.08f);
			yield return Hold(Idle, 0.12f);
		}

		private IEnumerator Walk(Actor host)
		{
			Mod.SetYaw(0f);
			Vector2 from = host.current_position;
			yield return Hold(new XInput.State
			{
				Left = new Vector2(0f, 1f)
			}, 1.5f);
			float full = PossessionHooks.Pace;
			Vector2 vector = host.current_position - from;
			Check("pushed forward, you walk", full > 0.3f, "pace " + full.ToString("0.00"));
			Check("forward is where you look (east)", vector.magnitude > 0.8f && vector.normalized.x > 0.8f, "moved " + vector.ToString("0.0"));
			yield return Hold(new XInput.State
			{
				Left = new Vector2(0f, 0.55f)
			}, 1f);
			float pace = PossessionHooks.Pace;
			Check("half a push walks slower", pace > 0.05f && pace < full * 0.8f, $"half {pace:0.00}, full {full:0.00}");
			yield return Hold(Idle, 0.6f);
			Check("let go, you stop", PossessionHooks.Pace < 0.05f, "pace " + PossessionHooks.Pace.ToString("0.00"));
		}

		private IEnumerator Run(Actor host)
		{
			yield return Hold(new XInput.State
			{
				Left = new Vector2(0f, 1f)
			}, 0.4f);
			float walk = PossessionHooks.Pace;
			yield return Hold(new XInput.State
			{
				Left = new Vector2(0f, 1f),
				Held = XInput.Buttons.LeftThumb
			}, 0.1f);
			yield return Hold(new XInput.State
			{
				Left = new Vector2(0f, 1f)
			}, 1.2f);
			Check("L3 clicked: running", PossessionHooks.Running && PadControls.Held(KeyCode.LeftShift), $"running {PossessionHooks.Running}, pace {PossessionHooks.Pace:0.00} (walk {walk:0.00})");
			Check("faster than the walk", PossessionHooks.Pace > walk * 1.1f, $"{PossessionHooks.Pace:0.00} vs {walk:0.00}");
			yield return Hold(Idle, 0.5f);
			yield return Hold(new XInput.State
			{
				Left = new Vector2(0f, 1f)
			}, 0.4f);
			Check("stopped, the run ends (pushed again: a walk)", !PadControls.Held(KeyCode.LeftShift));
			yield return Hold(Idle, 0.3f);
			bool seen = false;
			XInput.State item = new XInput.State
			{
				Connected = true,
				Left = new Vector2(0f, -1f)
			};
			(XInput.State, float)[] array = new(XInput.State, float)[3]
			{
				(item, 0.1f),
				(Idle, 0.1f),
				(item, 0.1f)
			};
			for (int i = 0; i < array.Length; i++)
			{
				(XInput.State, float) tuple = array[i];
				XInput.State item2 = tuple.Item1;
				float t = tuple.Item2;
				PadControls.Simulated = item2;
				int frames = 0;
				for (float w = 0f; w < t || frames < 2; frames++)
				{
					yield return null;
					if (PadControls.BackstepTap)
					{
						seen = true;
					}
					w += Time.unscaledDeltaTime;
				}
			}
			yield return Hold(Idle, 0.5f);
			Check("flicked back twice: a backstep", seen);
			bool held = false;
			PadControls.Simulated = new XInput.State
			{
				Connected = true,
				Left = new Vector2(0f, -1f)
			};
			for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime)
			{
				if (PadControls.BackstepTap)
				{
					held = true;
				}
				yield return null;
			}
			yield return Hold(Idle, 0.3f);
			Check("held back, a walk backwards and no backstep", !held);
		}

		private IEnumerator Look()
		{
			Mod.SetYaw(0f);
			float yaw0 = Mod.YawForPadTest;
			yield return Hold(new XInput.State
			{
				Right = new Vector2(0.35f, 0f)
			}, 0.5f);
			float small = Mathf.Abs(Mathf.DeltaAngle(yaw0 * 57.29578f, Mod.YawForPadTest * 57.29578f));
			_ = Mod.YawForPadTest;
			yield return Hold(Idle, 0.2f);
			PadControls.Simulated = new XInput.State
			{
				Connected = true,
				Right = new Vector2(1f, 0f)
			};
			float start = Time.unscaledTime;
			float was = Mod.YawForPadTest * 57.29578f;
			float turned = 0f;
			float earlyTurn = 0f;
			float earlyTime = 0f;
			float lateTurn = 0f;
			float lateTime = 0f;
			float last = start;
			while (Time.unscaledTime - start < 1.5f)
			{
				yield return null;
				float unscaledTime = Time.unscaledTime;
				float num = Mod.YawForPadTest * 57.29578f;
				float num2 = Mathf.DeltaAngle(was, num);
				float num3 = unscaledTime - last;
				turned += num2;
				if (unscaledTime - start <= 0.25f)
				{
					earlyTurn += num2;
					earlyTime += num3;
				}
				else if (last - start >= 0.8f)
				{
					lateTurn += num2;
					lateTime += num3;
				}
				was = num;
				last = unscaledTime;
			}
			float num4 = Mathf.Abs(earlyTurn) / Mathf.Max(0.01f, earlyTime);
			float num5 = Mathf.Abs(lateTurn) / Mathf.Max(0.01f, lateTime);
			float num6 = num4 * 0.5f;
			Check("a small push turns slowly", small > 0.5f && small < num6 * 0.3f, $"{small:0.0} deg in half a second vs {num4:0} deg/s at a full push");
			Check("right turns right (yaw goes down)", turned < -10f, $"{turned:0} deg");
			Check("held at the edge it speeds up", num5 > num4 * 1.5f, $"{num4:0} deg/s at first ({earlyTime:0.00} s), {num5:0} deg/s held ({lateTime:0.00} s)");
			yield return Hold(Idle, 0.2f);
			float pitch0 = Mod.PitchForPadTest;
			yield return Hold(new XInput.State
			{
				Right = new Vector2(0f, 0.8f)
			}, 0.4f);
			Check("pushed up, you look up", Mod.PitchForPadTest > pitch0 + 3f, $"{pitch0:0.0} -> {Mod.PitchForPadTest:0.0}");
			yield return Hold(Idle, 0.2f);
		}

		private IEnumerator Buttons()
		{
			(XInput.Buttons, KeyCode, string)[] array = new(XInput.Buttons, KeyCode, string)[8]
			{
				(XInput.Buttons.A, KeyCode.Space, "A jumps (Space)"),
				(XInput.Buttons.B, KeyCode.X, "B is your ability (X)"),
				(XInput.Buttons.Y, KeyCode.E, "Y interacts (E)"),
				(XInput.Buttons.X, KeyCode.Mouse0, "X attacks (left click)"),
				(XInput.Buttons.RightThumb, KeyCode.C, "R3 pings (C)"),
				(XInput.Buttons.DpadDown, KeyCode.J, "D-pad down eats (J)"),
				(XInput.Buttons.DpadLeft, KeyCode.N, "D-pad left: the minimap (N)"),
				(XInput.Buttons.DpadRight, KeyCode.B, "D-pad right stars (B)")
			};
			for (int i = 0; i < array.Length; i++)
			{
				(XInput.Buttons, KeyCode, string) tuple = array[i];
				XInput.Buttons item = tuple.Item1;
				KeyCode k = tuple.Item2;
				string name = tuple.Item3;
				PadControls.Simulated = new XInput.State
				{
					Connected = true,
					Held = item
				};
				bool seen = false;
				for (int j = 0; j < 4; j++)
				{
					yield return null;
					seen |= PadControls.Held(k) || PlayInput.GetKey(k);
				}
				Check(name, seen);
				yield return Hold(Idle, 0.15f);
			}
			PadControls.Simulated = new XInput.State
			{
				Connected = true,
				RightTrigger = 1f
			};
			yield return null;
			yield return null;
			Check("RT attacks (left click)", PlayInput.GetMouseButton(0));
			PadControls.Simulated = new XInput.State
			{
				Connected = true,
				LeftTrigger = 1f
			};
			yield return null;
			yield return null;
			Check("LT guards (right click)", PlayInput.GetMouseButton(1));
			yield return Hold(Idle, 0.4f);
			Check("a pad counts as the touch and VR keys do", PlayInput.Virtual);
		}

		private IEnumerator Prompts()
		{
			yield return Hold(new XInput.State
			{
				Held = XInput.Buttons.A
			}, 0.05f);
			yield return Hold(Idle, 0.1f);
			Check("the pad is the input shown", PadControls.Shown);
			Check("E is shown as Y", GameArt.VrKey("E") == "Y", GameArt.VrKey("E"));
			Check("the left click as RT", GameArt.VrKey("LMB") == "RT", GameArt.VrKey("LMB"));
			PadControls.DeckForTest = true;
			yield return null;
			yield return null;
			Check("on a Deck, R is R4", GameArt.VrKey("R") == "R4", GameArt.VrKey("R"));
			Check("on a Deck, the binoculars are on the right trackpad", GameArt.VrKey("Z") == "R pad", GameArt.VrKey("Z"));
			PadControls.DeckForTest = false;
			Mod.Settings.PadPictures = 3;
			yield return null;
			yield return null;
			Check("PlayStation pictures: E is Triangle", GameArt.VrKey("E") == "Triangle", GameArt.VrKey("E"));
			Mod.Settings.PadPictures = 0;
			yield return null;
		}

		private IEnumerator Tools()
		{
			Check("nothing out to begin with", !Mod.CompassOutForPadTest && !Mod.BinocularsUpForPadTest && !Torches.Out);
			PadControls.SwipeForTest(1);
			yield return CivilTest.Wait(0.5f);
			Check("a swipe: the compass", Mod.CompassOutForPadTest);
			PadControls.SwipeForTest(1);
			yield return CivilTest.Wait(0.6f);
			Check("the next: the torch or (none in the bag) the binoculars, the compass away", !Mod.CompassOutForPadTest && (Torches.Out || Mod.BinocularsUpForPadTest), $"torch {Torches.Out}, binoculars {Mod.BinocularsUpForPadTest}");
			if (Torches.Out)
			{
				PadControls.SwipeForTest(1);
				yield return CivilTest.Wait(0.6f);
			}
			Check("the binoculars stay up (a tap, not a hold)", Mod.BinocularsUpForPadTest);
			yield return CivilTest.Wait(0.6f);
			yield return Capture("pad_binoculars.png");
			PadControls.SwipeForTest(1);
			yield return CivilTest.Wait(0.6f);
			Check("past the binoculars: empty hands", !Mod.CompassOutForPadTest && !Mod.BinocularsUpForPadTest && !Torches.Out);
			PadControls.SwipeForTest(-1);
			yield return CivilTest.Wait(0.6f);
			Check("swiped back: the binoculars again", Mod.BinocularsUpForPadTest);
			PadControls.SwipeForTest(-1);
			yield return CivilTest.Wait(0.6f);
			PadControls.SwipeForTest(-1);
			yield return CivilTest.Wait(0.6f);
			if (Torches.Out)
			{
				PadControls.SwipeForTest(-1);
				yield return CivilTest.Wait(0.6f);
			}
			Check("and back to the compass", Mod.CompassOutForPadTest, $"compass {Mod.CompassOutForPadTest}, torch {Torches.Out}, binoculars {Mod.BinocularsUpForPadTest}");
			PadControls.SwipeForTest(-1);
			yield return CivilTest.Wait(0.6f);
			Check("then empty hands", !Mod.CompassOutForPadTest);
		}

		private IEnumerator QuickMenu()
		{
			yield return Hold(new XInput.State
			{
				Held = XInput.Buttons.Start
			}, 0.7f);
			Check("Menu held opens the quick menu", PadMenu.Open);
			yield return Hold(Idle, 0.2f);
			Check("letting go of Menu leaves it open (no settings)", PadMenu.Open && !Mod.SettingsMenu.Open);
			yield return CivilTest.Wait(0.2f);
			Check("it is drawn", Mod.HudForTest.PadMenuDrawn);
			Vector2 before = ((Mod.Host != null) ? Mod.Host.current_position : Vector2.zero);
			yield return Hold(new XInput.State
			{
				Left = new Vector2(0f, 1f)
			}, 0.05f);
			yield return Hold(Idle, 0.1f);
			Check("the left stick picks, it doesn't walk", Mod.Host == null || (Mod.Host.current_position - before).magnitude < 0.2f);
			int help = Array.FindIndex(PadMenu.Items, (PadMenu.Item it) => it.Name == "Help");
			int helpRow = help / 5;
			int helpCol = help % 5;
			for (int i = 0; i < 30; i++)
			{
				if (PadMenu.Choice == help)
				{
					break;
				}
				int num = PadMenu.Choice / 5;
				int num2 = PadMenu.Choice % 5;
				XInput.Buttons b = ((num == helpRow) ? ((num2 < helpCol) ? XInput.Buttons.DpadRight : XInput.Buttons.DpadLeft) : ((num >= helpRow) ? XInput.Buttons.DpadUp : XInput.Buttons.DpadDown));
				yield return Tap(b);
			}
			Check("the D-pad hops to Help", PadMenu.Choice == help, "on " + PadMenu.Choice);
			yield return Capture("pad_quickmenu.png");
			Mod.SetHelp(open: false);
			yield return Tap(XInput.Buttons.A);
			yield return CivilTest.Wait(0.2f);
			Check("A presses it and closes the menu (F1: the help window)", !PadMenu.Open && Help.Open);
			yield return CivilTest.Wait(0.3f);
			yield return Capture("pad_help.png");
			Mod.SetHelp(open: false);
			yield return Hold(new XInput.State
			{
				Held = XInput.Buttons.Start
			}, 0.7f);
			yield return Hold(Idle, 0.1f);
			yield return Tap(XInput.Buttons.B);
			Check("B closes it", !PadMenu.Open);
		}

		private IEnumerator Settings()
		{
			Mod.SettingsMenu.Tab = 1;
			yield return Tap(XInput.Buttons.Start);
			yield return CivilTest.Wait(0.6f);
			if (!Check("Menu tapped opens the settings", Mod.SettingsMenu.Open))
			{
				yield break;
			}
			Check("a window has the pad", Mod.PadModeForTest == PadControls.Mode.Menu, Mod.PadModeForTest.ToString());
			for (float t = 0f; t < 3f; t += 0.1f)
			{
				if (PadNav.Count > 3)
				{
					break;
				}
				yield return CivilTest.Wait(0.1f);
			}
			Check("its buttons are known to the D-pad", PadNav.Count > 3, "spots " + PadNav.Count);
			yield return Tap(XInput.Buttons.DpadDown);
			yield return CivilTest.Wait(0.2f);
			Check("a hop puts the cursor on a button", PadControls.Driving && PadNav.Under(new Vector2(PadControls.Cursor.x, (float)Screen.height - PadControls.Cursor.y), out var _), $"cursor {PadControls.Cursor}");
			Vector2 first = PadControls.Cursor;
			yield return Tap(XInput.Buttons.DpadDown);
			yield return CivilTest.Wait(0.2f);
			Check("down again: a button below", PadControls.Cursor.y < first.y - 2f, $"{first} -> {PadControls.Cursor}");
			Check("the real pointer is hidden, the pad's drawn", !Cursor.visible);
			yield return Capture("pad_settings.png");
			if (Check("a slider is known to the D-pad", PadNav.FirstSliderForTest(out var r2)))
			{
				PadControls.Place(new Vector2(r2.center.x, (float)Screen.height - r2.center.y));
				float t = Mod.Settings.FieldOfView;
				yield return Tap(XInput.Buttons.DpadRight);
				yield return CivilTest.Wait(0.2f);
				float up = Mod.Settings.FieldOfView;
				yield return Tap(XInput.Buttons.DpadLeft);
				yield return CivilTest.Wait(0.2f);
				Check("right on a slider moves it up, left back down", up > t && Mathf.Abs(Mod.Settings.FieldOfView - t) < 0.01f, $"{t} -> {up} -> {Mod.Settings.FieldOfView}");
			}
			int tab = Mod.SettingsMenu.Tab;
			yield return Tap(XInput.Buttons.RightShoulder);
			yield return CivilTest.Wait(0.4f);
			Check("RB: the next tab", Mod.SettingsMenu.Tab == (tab + 1) % SettingsMenu.Tabs.Length, $"{tab} -> {Mod.SettingsMenu.Tab}");
			yield return Tap(XInput.Buttons.LeftShoulder);
			yield return CivilTest.Wait(0.4f);
			Check("LB: back again", Mod.SettingsMenu.Tab == tab, $"now {Mod.SettingsMenu.Tab}");
			yield return Tap(XInput.Buttons.B);
			yield return CivilTest.Wait(0.4f);
			Check("B closes the settings", !Mod.SettingsMenu.Open);
			Check("back in first person, no cursor", Mod.PadModeForTest == PadControls.Mode.Play && !PadControls.Driving);
		}

		private IEnumerator Map()
		{
			Mod.LetGoForPadTest();
			for (float t = 0f; t < 6f; t += 0.2f)
			{
				if (!Mod.IsFirstPerson && Mod.PadModeForTest == PadControls.Mode.Map)
				{
					break;
				}
				yield return CivilTest.Wait(0.2f);
			}
			if (Check("on the map the pad works the map", Mod.PadModeForTest == PadControls.Mode.Map, Mod.PadModeForTest.ToString()))
			{
				yield return Hold(new XInput.State
				{
					Right = new Vector2(1f, 0f)
				}, 0.3f);
				yield return Hold(Idle, 0.1f);
				Vector2 cursor = PadControls.Cursor;
				Check("the right stick moves the cursor", PadControls.Driving && cursor.x > (float)Screen.width * 0.55f, cursor.ToString());
				Vector3 mousePosition = Input.mousePosition;
				Check("the game's mouse is the cursor", VrMouse.Patched && Mathf.Abs(mousePosition.x - cursor.x) < 2f && Mathf.Abs(mousePosition.y - cursor.y) < 2f, $"mouse {mousePosition}, cursor {cursor}, patched {VrMouse.Patched} {VrMouse.Error}");
				Camera cam = World.world.camera;
				Vector3 at = cam.transform.position;
				yield return Hold(new XInput.State
				{
					Left = new Vector2(0f, 1f)
				}, 0.6f);
				yield return Hold(Idle, 0.4f);
				Check("the left stick pans the map", cam.transform.position.y > at.y + 0.5f, $"{at} -> {cam.transform.position}");
				float zoom = cam.orthographicSize;
				yield return Hold(new XInput.State
				{
					Held = XInput.Buttons.RightShoulder
				}, 0.6f);
				yield return Hold(Idle, 0.6f);
				Check("RB zooms in", cam.orthographicSize < zoom * 0.95f, $"{zoom:0.0} -> {cam.orthographicSize:0.0}");
				yield return Capture("pad_map.png");
				yield return Hold(Idle, 0.2f);
			}
		}

		private IEnumerator Capture(string name)
		{
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, name));
			for (int i = 0; i < 30; i++)
			{
				if (!Mod.HudForTest.CapturePending)
				{
					break;
				}
				yield return null;
			}
		}
	}
}
