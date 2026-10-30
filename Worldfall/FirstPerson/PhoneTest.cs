using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class PhoneTest : CivilTest
	{
		private Actor _host;

		private WorldTile _stage;

		private float _w;

		private float _h;

		protected override string Prefix => "PHONE";

		protected override IEnumerator Body()
		{
			_w = Screen.width;
			_h = Screen.height;
			Note($"screen {_w}x{_h}");
			Check("this PC is not taken for a phone", Device.Is == Device.Kind.Pc, Device.Summary);
			Check("no Wine on Windows", Device.Wine == null, Device.Wine);
			WorldTile worldTile = (_stage = CivilTest.Stage());
			if (!Check("a stage", worldTile != null))
			{
				yield break;
			}
			CivilTest.ClearBox(worldTile.x - 14, worldTile.y - 14, worldTile.x + 14, worldTile.y + 14);
			_host = CivilTest.SpawnNear("human", new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f));
			if (Check("a human to be", _host != null))
			{
				try
				{
					((BaseSimObject)_host).addStatusEffect("invincible", 600f, pColorEffect: false);
				}
				catch
				{
				}
				yield return Possess(_host);
				if (Check("first person on them", InControl(_host)))
				{
					int touchWas = Mod.Settings.TouchControls;
					Mod.Settings.TouchControls = 0;
					yield return Part("a PC keeps its mouse", PcUntouched());
					Device.ForceForTest(Device.Kind.EmulatedPhone);
					Mod.RechooseSchemeForTest();
					yield return Part("on a phone the controls come up", ControlsUp());
					yield return Part("the stick walks", Stick());
					yield return Part("a drag turns the view", Drag());
					yield return Part("buttons press their keys", Buttons());
					yield return Part("use, talk and back", UseAndBack());
					yield return Part("the panel of more keys", MoreKeys());
					yield return Part("WorldBox on a phone", Native());
					yield return Part("lighter settings for a phone", Tuning());
					yield return Part("let go", LetGo());
					TouchControls.LiftAllForTest();
					Device.ForceForTest(null);
					Mod.Settings.TouchControls = touchWas;
				}
			}
		}

		private IEnumerator PcUntouched()
		{
			Device.ForceForTest(Device.Kind.Pc);
			Mod.RechooseSchemeForTest();
			yield return CivilTest.Wait(0.3f);
			Check("no touch controls on a PC that wasn't touched", !TouchControls.Shown);
			Check("the cursor stays locked for mouse look", Cursor.lockState == CursorLockMode.Locked, Cursor.lockState.ToString());
			Check("no touch keys held", !TouchControls.Held(KeyCode.E) && !TouchControls.Held(KeyCode.Mouse0));
		}

		private IEnumerator ControlsUp()
		{
			yield return CivilTest.Wait(0.3f);
			Note("menus: " + Menus());
			Mod.EndConversation();
			for (float t = 0f; t < 2f; t += 0.1f)
			{
				if (!TouchControls.MenuMode)
				{
					break;
				}
				yield return CivilTest.Wait(0.1f);
			}
			Check("the touch controls are up", TouchControls.Shown);
			Check("the mouse is the finger until a touch comes (an emulator's touches are clicks)", TouchControls.PointerMode == !Device.TouchSeen);
			Check("the cursor is free (a locked one would put every tap in the middle)", Cursor.lockState == CursorLockMode.None, Cursor.lockState.ToString());
			Check("Attack, Jump, Guard, Use and More show", TouchControls.Attack.Visible && TouchControls.Jump.Visible && TouchControls.Guard.Visible && TouchControls.Use.Visible && TouchControls.More.Visible, Menus());
			Check("no Back without a menu", !TouchControls.BackButton.Visible);
			Check("buttons on the screen and apart", ButtonsFit(), Layout());
			yield return Capture("phone_idle.png");
		}

		private bool ButtonsFit()
		{
			List<TouchControls.Button> list = new List<TouchControls.Button>();
			TouchControls.Button[] buttons = TouchControls.Buttons;
			foreach (TouchControls.Button button in buttons)
			{
				if (button.Visible || button == TouchControls.Ability || button == TouchControls.Context)
				{
					list.Add(button);
				}
			}
			foreach (TouchControls.Button item in list)
			{
				if (item.C.x - item.R < 0f || item.C.x + item.R > _w || item.C.y - item.R < 0f || item.C.y + item.R > _h)
				{
					return false;
				}
				foreach (TouchControls.Button item2 in list)
				{
					if (item2 != item && (item2.C - item.C).magnitude < (item2.R + item.R) * 0.98f)
					{
						return false;
					}
				}
			}
			return true;
		}

		private static string Layout()
		{
			StringBuilder stringBuilder = new StringBuilder();
			TouchControls.Button[] buttons = TouchControls.Buttons;
			foreach (TouchControls.Button button in buttons)
			{
				stringBuilder.Append(button.Name).Append(" (").Append(button.C.x.ToString("0"))
					.Append(',')
					.Append(button.C.y.ToString("0"))
					.Append(" r")
					.Append(button.R.ToString("0"))
					.Append(") ");
			}
			return stringBuilder.ToString();
		}

		private IEnumerator Stick()
		{
			Mod.SetYaw(0f);
			Vector2 rest = TouchControls.StickRest;
			float r = TouchControls.StickRadius;
			Vector2 from = _host.current_position;
			yield return Slide(1, rest, rest + new Vector2(0f, 0f - r));
			yield return Hold(1, rest + new Vector2(0f, 0f - r), 1.5f);
			float pace = PossessionHooks.Pace;
			Vector2 vector = _host.current_position - from;
			Check("pushed forward, you walk", pace > 0.3f, "pace " + pace.ToString("0.00"));
			Check("forward is where you look (east)", vector.magnitude > 0.8f && vector.normalized.x > 0.8f, "moved " + vector.ToString("0.0"));
			yield return Capture("phone_walk.png");
			float full = pace;
			TouchControls.TouchForTest(1, rest + new Vector2(0f, (0f - r) * 0.45f));
			yield return CivilTest.Wait(1f);
			Check("half a push walks slower", PossessionHooks.Pace < full * 0.75f && PossessionHooks.Pace > 0.05f, PossessionHooks.Pace.ToString("0.00") + " of " + full.ToString("0.00"));
			TouchControls.TouchForTest(1, rest + new Vector2(0f, (0f - r) * 1.4f));
			yield return CivilTest.Wait(0.8f);
			Check("past the rim forward, you run", PossessionHooks.Running && TouchControls.Sprint, "pace " + PossessionHooks.Pace.ToString("0.00"));
			TouchControls.LiftForTest(1);
			yield return CivilTest.Wait(0.8f);
			Check("let go, you stop", PossessionHooks.Pace < 0.05f, PossessionHooks.Pace.ToString("0.00"));
			yield return Slide(1, rest, rest + new Vector2(0f, (0f - r) * 2.05f));
			Check("the lock lights", TouchControls.LockLit);
			yield return Capture("phone_lock.png");
			TouchControls.LiftForTest(1);
			yield return CivilTest.Wait(1f);
			Check("let go on the lock, you walk on", TouchControls.AutoRun && PossessionHooks.Pace > 0.3f, "pace " + PossessionHooks.Pace.ToString("0.00"));
			yield return Capture("phone_walking.png");
			TouchControls.TouchForTest(1, rest);
			yield return null;
			yield return null;
			TouchControls.LiftForTest(1);
			yield return CivilTest.Wait(0.8f);
			Check("a touch on the stick stops it", !TouchControls.AutoRun && PossessionHooks.Pace < 0.05f, PossessionHooks.Pace.ToString("0.00"));
		}

		private IEnumerator Drag()
		{
			float yaw0 = Mod.ViewYaw;
			Vector2 vector = new Vector2(_w * 0.62f, _h * 0.35f);
			yield return Slide(2, vector, vector + new Vector2(_h * 0.25f, 0f));
			TouchControls.LiftForTest(2);
			yield return null;
			float num = Mathf.DeltaAngle(yaw0 * 57.29578f, Mod.ViewYaw * 57.29578f);
			float num2 = -45f * Mod.Settings.TouchLook;
			Check("dragging right turns right, 45 degrees for a quarter of the height", Mathf.Abs(num - num2) < 5f, num.ToString("0.0") + " deg");
			yaw0 = Mod.ViewYaw;
			Vector2 c = TouchControls.Attack.C;
			yield return Slide(3, c, c + new Vector2((0f - _h) * 0.1f, 0f));
			TouchControls.LiftForTest(3);
			yield return null;
			num = Mathf.DeltaAngle(yaw0 * 57.29578f, Mod.ViewYaw * 57.29578f);
			Check("a thumb dragged off Attack turns the view", num > 10f, num.ToString("0.0") + " deg");
		}

		private IEnumerator Home()
		{
			_host.current_position = new Vector2((float)_stage.x + 0.5f, (float)_stage.y + 0.5f);
			Mod.SetYaw(0f);
			Mod.SetPitchDegrees(0f);
			yield return CivilTest.Wait(0.5f);
		}

		private IEnumerator Buttons()
		{
			yield return Home();
			yield return CivilTest.Wait(0.7f);
			TouchControls.TouchForTest(3, TouchControls.Attack.C);
			yield return null;
			bool down = PlayInput.GetMouseButton(0);
			bool swung = false;
			for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
			{
				yield return null;
				swung |= Mod.Melee.State.Swinging;
			}
			Check("Attack holds the left button", down && PlayInput.GetMouseButton(0));
			Check("and the arm swings", swung);
			yield return Capture("phone_attack.png");
			TouchControls.LiftForTest(3);
			yield return null;
			yield return null;
			Check("let go, the button is up", !PlayInput.GetMouseButton(0) && !ControllableUnit._attack_pressed_button_left);
			yield return CivilTest.Wait(0.5f);
			float peak = 0f;
			TouchControls.TouchForTest(4, TouchControls.Jump.C);
			for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime)
			{
				yield return null;
				if (t > 0.1f)
				{
					TouchControls.LiftForTest(4);
				}
				peak = Mathf.Max(peak, _host.position_height);
			}
			TouchControls.LiftForTest(4);
			Check("Jump jumps", peak > 0.05f, "peak " + peak.ToString("0.00"));
			TouchControls.TouchForTest(5, TouchControls.Guard.C);
			yield return null;
			Check("Guard holds the right button", PlayInput.GetMouseButton(1));
			TouchControls.LiftForTest(5);
			yield return null;
		}

		private IEnumerator UseAndBack()
		{
			yield return Home();
			Actor npc = CivilTest.SpawnNear("human", _host.current_position + new Vector2(2f, 0f));
			if (!Check("someone to talk to", npc != null))
			{
				yield break;
			}
			Mod.SetYaw(0f);
			float near = 1.4f;
			for (float t = 0f; t < 6f; t += 0.1f)
			{
				if (Mod.Talk.Candidate == npc)
				{
					break;
				}
				bool flag = ((int)(t * 2f) & 1) == 1;
				near = (flag ? 2f : 1.4f);
				Mod.SetPitchDegrees(flag ? 0f : (-6f));
				CivilTest.StandBefore(npc, _host, near);
				yield return CivilTest.Wait(0.1f);
			}
			Note("under the crosshair: " + ((Mod.Picked is Actor a) ? Describe(a) : ((Mod.Picked != null) ? Mod.Picked.GetType().Name : "nothing")) + ", candidate " + ((Mod.Talk.Candidate != null) ? Describe(Mod.Talk.Candidate) : "none") + ", npc " + Describe(npc) + " " + (npc.current_position - _host.current_position).magnitude.ToString("0.0") + " tiles off");
			for (int i = 0; i < 6; i++)
			{
				CivilTest.StandBefore(npc, _host, near);
				yield return null;
			}
			Check("Use lights when there's someone to talk to", TouchControls.Use.Lit, Mod.Prompt);
			yield return Tap(6, TouchControls.Use.C);
			yield return CivilTest.Wait(0.3f);
			Check("Use (E) opens a talk", Mod.Converse.Open);
			Check("in a menu only Back shows", TouchControls.BackButton.Visible && !TouchControls.Attack.Visible && !TouchControls.More.Visible);
			Check("a menu's taps don't swing", !PlayInput.GetMouseButton(0));
			yield return Capture("phone_talk.png");
			if (Mod.Converse.Open)
			{
				yield return Tap(7, TouchControls.BackButton.C);
			}
			yield return CivilTest.Wait(0.3f);
			Check("Back ends the talk", !Mod.Converse.Open);
			CivilTest.Kill(npc);
		}

		private IEnumerator MoreKeys()
		{
			Mod.EndConversation();
			yield return CivilTest.Wait(0.6f);
			yield return Tap(8, TouchControls.More.C);
			Check("More opens the panel", TouchControls.MoreOpen);
			Check("the panel fits the screen", TouchControls.Panel.yMin > 0f && TouchControls.Panel.yMax < _h && TouchControls.Panel.xMin > 0f, TouchControls.Panel.ToString());
			yield return Capture("phone_more.png");
			yield return Tap(9, Tile("Inventory").Box.center);
			yield return CivilTest.Wait(0.3f);
			Check("its Inventory tile opens the inventory (Tab)", Mod.Craft.Open && !TouchControls.MoreOpen);
			yield return Capture("phone_craft.png");
			yield return Tap(10, TouchControls.BackButton.C);
			yield return CivilTest.Wait(0.3f);
			Check("Back closes crafting", !Mod.Craft.Open);
			yield return Tap(11, TouchControls.More.C);
			yield return Tap(12, Tile("Settings").Box.center);
			yield return CivilTest.Wait(0.3f);
			Check("its Settings tile opens F2", Mod.SettingsMenu.Open);
			yield return Tap(13, TouchControls.BackButton.C);
			yield return CivilTest.Wait(0.3f);
			Check("Back closes the settings", !Mod.SettingsMenu.Open);
			yield return Tap(14, TouchControls.More.C);
			yield return Tap(15, new Vector2(_w * 0.05f, _h * 0.95f));
			Check("a tap beside the panel closes it", !TouchControls.MoreOpen);
			yield return Tap(18, TouchControls.More.C);
			TouchControls.TouchForTest(19, Tile("Insult").Box.center);
			yield return null;
			yield return null;
			TouchControls.LiftForTest(19);
			int insults = Mod.Talk.Insults;
			bool swore = false;
			for (int i = 0; i < 6; i++)
			{
				yield return null;
				swore |= Mod.Talk.Insults > insults || (Mod.ToastText ?? "").Contains("insult");
			}
			Check("its Insult tile insults who you look at (or says to look at someone)", swore, Mod.ToastText);
		}

		private IEnumerator Native()
		{
			float w = Screen.width;
			float h = Screen.height;
			Device.ForceForTest(Device.Kind.Phone);
			yield return CivilTest.Wait(0.5f);
			Check("in first person a phone is held sideways", Mod.LandscapeHeldForTest);
			Check("the HUD is drawn bigger on a phone", Skin.U > Mathf.Max(0.6f, (float)Screen.height / 1080f) * 1.1f, Skin.U.ToString("0.00"));
			yield return Capture("phone_native.png");
			Check("a PC's screen isn't turned", (float)Screen.width == w && (float)Screen.height == h);
			Device.ForceForTest(Device.Kind.EmulatedPhone);
			yield return CivilTest.Wait(0.3f);
			Check("off a phone the screen goes back", !Mod.LandscapeHeldForTest);
		}

		private IEnumerator Tuning()
		{
			Settings settings = Mod.Settings;
			int resolution = settings.Resolution;
			float drawDistance = settings.DrawDistance;
			float detailDistance = settings.DetailDistance;
			float creatureDistance = settings.CreatureDistance;
			float fieldOfView = settings.FieldOfView;
			bool phoneTuned = settings.PhoneTuned;
			settings.PhoneTuned = false;
			settings.FieldOfView = 75f;
			Mod.TunePhoneForTest();
			float num = Mathf.Max(_w, _h) / Mathf.Min(_w, _h);
			float num2 = ((num > 1.85f) ? Mathf.Round(2f * Mathf.Atan(Mathf.Tan(0.65449846f) * num / 1.7777778f) * 57.29578f) : 75f);
			Check("render width the lightest", settings.Resolution == 0);
			Check("draw, detail and creature distances shorter", settings.DrawDistance <= 180f && settings.DetailDistance <= 60f && settings.CreatureDistance <= 120f);
			Check("the view widened for the long screen (as much above and below as on a monitor)", Mathf.Abs(settings.FieldOfView - num2) < 0.5f, settings.FieldOfView + " for " + num.ToString("0.00") + ":1");
			Check("done once", settings.PhoneTuned);
			settings.Resolution = resolution;
			settings.DrawDistance = drawDistance;
			settings.DetailDistance = detailDistance;
			settings.CreatureDistance = creatureDistance;
			settings.FieldOfView = fieldOfView;
			settings.PhoneTuned = phoneTuned;
			settings.Save();
			yield return null;
		}

		private IEnumerator LetGo()
		{
			yield return Tap(16, TouchControls.More.C);
			yield return Tap(17, Tile("Let go").Box.center);
			yield return CivilTest.Wait(1.5f);
			Check("Let go lets go of the creature", !ControllableUnit.isControllingUnit());
			Check("the controls are down off first person", !TouchControls.Shown);
		}

		private string Menus()
		{
			return "menu " + TouchControls.MenuMode + ", panel " + TouchControls.MoreOpen + ", talk " + Mod.Converse.Open + ", settings " + Mod.SettingsMenu.Open + ", craft " + Mod.Craft.Open;
		}

		private static string Describe(Actor a)
		{
			return WorldBoxMod.Describe(a) + (a.isAlive() ? "" : " (dead)");
		}

		private static TouchControls.Tile Tile(string name)
		{
			TouchControls.Tile[] tiles = TouchControls.Tiles;
			foreach (TouchControls.Tile tile in tiles)
			{
				if (tile.Name == name)
				{
					return tile;
				}
			}
			return null;
		}

		private static IEnumerator Tap(int id, Vector2 at)
		{
			TouchControls.TouchForTest(id, at);
			yield return null;
			yield return null;
			TouchControls.LiftForTest(id);
			yield return null;
			yield return null;
		}

		private static IEnumerator Slide(int id, Vector2 from, Vector2 to, int steps = 10)
		{
			TouchControls.TouchForTest(id, from);
			yield return null;
			for (int i = 1; i <= steps; i++)
			{
				TouchControls.TouchForTest(id, Vector2.Lerp(from, to, (float)i / (float)steps));
				yield return null;
			}
		}

		private static IEnumerator Hold(int id, Vector2 at, float seconds)
		{
			for (float t = 0f; t < seconds; t += 0.1f)
			{
				TouchControls.TouchForTest(id, at);
				yield return CivilTest.Wait(0.1f);
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
