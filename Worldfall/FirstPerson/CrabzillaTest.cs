using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class CrabzillaTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			string options = File.ReadAllText(path);
			bool quit = options.Contains("quit");
			while (!Config.game_loaded || World.world == null || World.world.units == null)
			{
				yield return null;
			}
			for (float t = 0f; t < 60f; t += 1f)
			{
				bool flag = false;
				try
				{
					flag = SmoothLoader.isLoading();
				}
				catch
				{
				}
				if (!flag && t >= 4f)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(1f);
			}
			Config.autosaves = false;
			_mod = WorldBoxMod.Instance;
			List<WorldTile> list = KingTest.GoodSites();
			if (list.Count == 0)
			{
				Log.Error("CRAB no open land");
				Finish(quit);
				yield break;
			}
			if (options.Contains("dump"))
			{
				yield return Run("dump", Dump(list[0]));
			}
			else if (options.Contains("foreign"))
			{
				yield return Run("foreign", Guard(list));
			}
			else
			{
				yield return Run("play", Play(list));
			}
			bool[] scriptedFire = Crabzillas.ScriptedFire;
			bool[] scriptedFire2 = Crabzillas.ScriptedFire;
			bool flag2 = false;
			scriptedFire2[1] = false;
			scriptedFire[0] = flag2;
			PossessionHooks.ScriptedInput = Vector2.zero;
			WorldBoxMod.QuietLevelForTest = -1f;
			if (_mod != null)
			{
				_mod.DebugEyeSet = false;
			}
			Finish(quit);
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("CRAB --- " + name);
			while (true)
			{
				object current;
				try
				{
					if (!part.MoveNext())
					{
						break;
					}
					current = part.Current;
				}
				catch (Exception ex)
				{
					Log.Error("CRAB " + name + " failed: " + ex);
					break;
				}
				yield return current;
			}
		}

		private static Actor Spawn(string id, WorldTile t)
		{
			try
			{
				return World.world.units.spawnNewUnit(id, t, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("CRAB spawn " + id + ": " + ex.Message);
				return null;
			}
		}

		private IEnumerator Guard(List<WorldTile> sites)
		{
			WorldTile at = sites[0];
			WorldTile far = null;
			foreach (WorldTile site in sites)
			{
				if (Vector2.Distance(new Vector2(site.x, site.y), new Vector2(at.x, at.y)) > 40f && Land(site))
				{
					far = site;
					break;
				}
			}
			if (far == null)
			{
				far = World.world.GetTile(Mathf.Min(MapBox.width - 20, at.x + 45), at.y);
			}
			Actor you = Spawn("human", at);
			if (you == null)
			{
				yield break;
			}
			ControllableUnit.setControllableCreature(you);
			for (float t = 0f; t < 8f; t += 0.25f)
			{
				if (_mod.IsFirstPerson && ControllableUnit._unit_main == you)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.25f);
			}
			Check("first person in the villager", _mod.IsFirstPerson && ControllableUnit._unit_main == you);
			Check("crab guard installed", CrabGuard.Installed);
			CrabGuard.NoteForeign(far.x, far.y);
			Actor crab = Spawn("crabzilla", far);
			if (crab == null)
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(1f);
			Check("still in the villager", ControllableUnit._unit_main == you && _mod.IsFirstPerson && Crabzillas.You == null);
			Check("the crab isn't under your control", !ControllableUnit._units.Contains(crab) && !((BaseSimObject)crab).hasStatus("possessed"));
			Crabzilla body = crab.getActorComponent<Crabzilla>();
			Vector2 c0 = crab.current_position;
			Vector2 y0 = you.current_position;
			bool fired = false;
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
			{
				ControllableUnit._attack_pressed_button_left = true;
				yield return null;
				if (body != null && body._beam_enabled)
				{
					fired = true;
				}
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			ControllableUnit._attack_pressed_button_left = false;
			Log.Info($"CRAB guard: you walked {Vector2.Distance(you.current_position, y0):F1} tiles, the crab {Vector2.Distance(crab.current_position, c0):F2}");
			Check("the crab stood still while you walked", Vector2.Distance(crab.current_position, c0) < 0.3f && Vector2.Distance(you.current_position, y0) > 1f);
			Check("the crab's lasers stayed off while you clicked", !fired);
			Actor mine = Spawn("crabzilla", World.world.GetTile(at.x, Mathf.Min(MapBox.height - 2, at.y + 20)));
			for (float t = 0f; t < 8f; t += 0.25f)
			{
				if (Crabzillas.You == mine)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.25f);
			}
			Check("your own Crabzilla takes you in", mine != null && ControllableUnit._unit_main == mine && Crabzillas.You == mine);
		}

		private static void Check(string what, bool ok)
		{
			Log.Info("CRAB " + (ok ? "PASS " : "FAIL ") + what);
		}

		private IEnumerator Play(List<WorldTile> sites)
		{
			WorldTile worldTile = null;
			WorldTile worldTile2 = null;
			foreach (WorldTile site in sites)
			{
				WorldTile tile = World.world.GetTile(site.x, site.y + 30);
				if (tile != null && Land(tile) && Land(World.world.GetTile(site.x, site.y + 15)))
				{
					worldTile = site;
					worldTile2 = tile;
					break;
				}
			}
			if (worldTile == null)
			{
				worldTile = sites[0];
				worldTile2 = World.world.GetTile(worldTile.x, Mathf.Min(MapBox.height - 2, worldTile.y + 30));
			}
			List<Actor> flock = new List<Actor>();
			for (int i = 0; i < 9; i++)
			{
				WorldTile tile2 = World.world.GetTile(worldTile2.x - 2 + i % 3 * 2, worldTile2.y - 2 + i / 3 * 2);
				Actor actor = ((tile2 != null) ? Spawn((i % 3 == 1) ? "human" : "sheep", tile2) : null);
				if (actor != null)
				{
					flock.Add(actor);
				}
			}
			Hold(flock);
			Log.Info($"CRAB site {worldTile.x},{worldTile.y}, flock of {flock.Count} at {worldTile2.x},{worldTile2.y}");
			Actor crab = Spawn("crabzilla", worldTile);
			if (crab == null)
			{
				yield break;
			}
			for (float t = 0f; t < 8f; t += 0.25f)
			{
				if (_mod.IsFirstPerson && Crabzillas.You == crab)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.25f);
			}
			if (!_mod.IsFirstPerson || Crabzillas.You != crab)
			{
				Log.Error("CRAB first person did not start as the crab (first person " + _mod.IsFirstPerson + ")");
				yield break;
			}
			_mod.HintUntil = 0f;
			float hp0 = ((crab.data != null) ? ((float)crab.data.health) : (-1f));
			_mod.SetYaw(MathF.PI / 2f);
			_mod.SetPitchDegrees(0f);
			yield return new WaitForSecondsRealtime(1.5f);
			ViewState view = _mod.View;
			float num = ((FirstPersonAim.Terrain != null) ? FirstPersonAim.Terrain.SampleHeight(crab.current_position.x, crab.current_position.y) : 0f);
			Log.Info($"CRAB first person as the crab: eye {view.Z - num:F2} tiles over the ground (model eye {Crabzillas.EyeHeight:F2}), {Vector2.Distance(new Vector2(view.X, view.Y), crab.current_position):F2} ahead of its middle, hp {hp0:F0}");
			_mod.SaveFrame("crab_fp_idle.png");
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "crab_fp_hud.png"));
			yield return new WaitForSecondsRealtime(0.5f);
			Vector2 p0 = crab.current_position;
			float t2 = Time.realtimeSinceStartup;
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			yield return new WaitForSecondsRealtime(1.6f);
			_mod.SetPitchDegrees(-30f);
			yield return new WaitForSecondsRealtime(0.4f);
			_mod.SaveFrame("crab_fp_walk_down.png");
			_mod.SetPitchDegrees(0f);
			yield return new WaitForSecondsRealtime(0.4f);
			_mod.SaveFrame("crab_fp_walk.png");
			PossessionHooks.ScriptedInput = Vector2.zero;
			float num2 = Vector2.Distance(crab.current_position, p0);
			float num3 = Time.realtimeSinceStartup - t2;
			Log.Info($"CRAB walked {num2:F1} tiles in {num3:F1} s ({num2 / Mathf.Max(0.1f, num3):F1} tiles/s; the game's own crab goes 12 at full pace)");
			yield return new WaitForSecondsRealtime(1.2f);
			Vector2 c = Vector2.zero;
			int alive0 = 0;
			foreach (Actor item in flock)
			{
				if (item != null && item.isAlive())
				{
					c += item.current_position;
					alive0++;
				}
			}
			if (alive0 > 0)
			{
				c /= (float)alive0;
			}
			view = _mod.View;
			Vector2 d = c - new Vector2(view.X, view.Y);
			float num4 = ((FirstPersonAim.Terrain != null) ? FirstPersonAim.Terrain.SampleHeight(c.x, c.y) : 0f);
			_mod.SetYaw(Mathf.Atan2(d.y, d.x));
			_mod.SetPitchDegrees(Mathf.Atan2(num4 + 0.3f - view.Z, d.magnitude) * 57.29578f);
			yield return new WaitForSecondsRealtime(0.8f);
			Log.Info($"CRAB aiming at the flock {d.magnitude:F1} tiles off, {alive0} alive");
			Crabzilla game = ((crab.avatar != null) ? crab.avatar.GetComponent<Crabzilla>() : null);
			bool gameBeam = false;
			Crabzillas.ScriptedFire[0] = true;
			for (int j = 0; j < 6; j++)
			{
				yield return new WaitForSecondsRealtime(0.1f);
				if (game != null && game.isBeamEnabled())
				{
					gameBeam = true;
				}
			}
			_mod.SaveFrame("crab_fp_fire_left.png");
			Vec3 vec = Crabzillas.TargetOf(0);
			Log.Info($"CRAB left beam ends at {vec.X:F1},{vec.Y:F1},{vec.Z:F1} ({Vector2.Distance(new Vector2(vec.X, vec.Y), c):F1} from the flock's middle), strikes {Crabzillas.StrikesAt(0)}; {Crabzillas.AimNote}");
			yield return new WaitForSecondsRealtime(0.8f);
			Crabzillas.ScriptedFire[1] = true;
			for (int j = 0; j < 5; j++)
			{
				yield return new WaitForSecondsRealtime(0.1f);
				if (game != null && game.isBeamEnabled())
				{
					gameBeam = true;
				}
			}
			_mod.SaveFrame("crab_fp_fire_both.png");
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "crab_fp_fire_hud.png"));
			yield return new WaitForSecondsRealtime(1.5f);
			bool[] scriptedFire = Crabzillas.ScriptedFire;
			bool[] scriptedFire2 = Crabzillas.ScriptedFire;
			bool flag = false;
			scriptedFire2[1] = false;
			scriptedFire[0] = flag;
			yield return new WaitForSecondsRealtime(0.8f);
			_mod.SaveFrame("crab_fp_after.png");
			int num5 = 0;
			foreach (Actor item2 in flock)
			{
				if (item2 != null && item2.isAlive())
				{
					num5++;
				}
			}
			float num6 = ((crab.isAlive() && crab.data != null) ? ((float)crab.data.health) : (-1f));
			Log.Info(string.Format("CRAB {0} beams struck the flock: {1} of {2} killed", (num5 < alive0) ? "PASS" : "FAIL", alive0 - num5, alive0));
			Log.Info("CRAB " + ((!gameBeam) ? "PASS" : "FAIL") + " the game's own laser stayed off while you fired");
			Log.Info(string.Format("CRAB {0} the crab unhurt by its own beams (hp {1:F0} -> {2:F0})", (num6 >= hp0 - 1f) ? "PASS" : "FAIL", hp0, num6));
			yield return Run("hum", Hum());
			Vector2 current_position = crab.current_position;
			float bodyYaw = Crabzillas.BodyYaw;
			Vector2 vector = new Vector2(Mathf.Cos(bodyYaw), Mathf.Sin(bodyYaw));
			Vector2 vector2 = new Vector2(Mathf.Sin(bodyYaw), 0f - Mathf.Cos(bodyYaw));
			_mod.DebugEye = current_position + vector2 * 34f + vector * 20f;
			_mod.DebugEyeHeight = 19f;
			_mod.DebugEyeSet = true;
			Vector2 vector3 = current_position + vector * 4f - _mod.DebugEye;
			_mod.SetYaw(Mathf.Atan2(vector3.y, vector3.x));
			_mod.SetPitchDegrees(-17f);
			yield return new WaitForSecondsRealtime(1.5f);
			_mod.SaveFrame("crab_out_stand.png");
			PossessionHooks.ScriptedInput = new Vector2(-1f, 0f);
			yield return new WaitForSecondsRealtime(1.2f);
			_mod.SaveFrame("crab_out_walk.png");
			PossessionHooks.ScriptedInput = Vector2.zero;
			yield return new WaitForSecondsRealtime(0.6f);
			_mod.DebugEyeSet = false;
			Settings set = _mod.Settings;
			int savedMode = set.TimeMode;
			set.TimeMode = 3;
			_mod.ForceRelight();
			_mod.SetYaw(Crabzillas.BodyYaw + 0.3f);
			_mod.SetPitchDegrees(-14f);
			yield return new WaitForSecondsRealtime(2f);
			bool[] scriptedFire3 = Crabzillas.ScriptedFire;
			bool[] scriptedFire4 = Crabzillas.ScriptedFire;
			flag = true;
			scriptedFire4[1] = true;
			scriptedFire3[0] = flag;
			yield return new WaitForSecondsRealtime(0.9f);
			_mod.SaveFrame("crab_fp_night_fire.png");
			bool[] scriptedFire5 = Crabzillas.ScriptedFire;
			bool[] scriptedFire6 = Crabzillas.ScriptedFire;
			flag = false;
			scriptedFire6[1] = false;
			scriptedFire5[0] = flag;
			yield return new WaitForSecondsRealtime(0.5f);
			set.TimeMode = savedMode;
			_mod.ForceRelight();
			Log.Info("CRAB play done");
		}

		private IEnumerator Hum()
		{
			FirstPersonAudio audio = _mod.Audio;
			if (WorldBoxMod.QuietTestCopy)
			{
				WorldBoxMod.QuietLevelForTest = 1E-05f;
			}
			_mod.SetPitchDegrees(30f);
			yield return new WaitForSecondsRealtime(0.8f);
			float[] quiet = new float[2];
			float[] right = new float[2];
			float[] left = new float[2];
			yield return Listen(audio, 0.8f, quiet);
			int starts = audio.CrabHumStarts;
			Crabzillas.ScriptedFire[1] = true;
			yield return new WaitForSecondsRealtime(0.6f);
			bool rightOn = audio.CrabHumPlaying(1) && !audio.CrabHumPlaying(0);
			yield return Listen(audio, 1f, right);
			bool rightStill = audio.CrabHumPlaying(1);
			int rightStarts = audio.CrabHumStarts - starts;
			Crabzillas.ScriptedFire[1] = false;
			yield return new WaitForSecondsRealtime(0.6f);
			bool rightOff = !audio.CrabHumPlaying(1);
			starts = audio.CrabHumStarts;
			Crabzillas.ScriptedFire[0] = true;
			yield return new WaitForSecondsRealtime(0.6f);
			bool leftOn = audio.CrabHumPlaying(0) && !audio.CrabHumPlaying(1);
			yield return Listen(audio, 1f, left);
			int leftStarts = audio.CrabHumStarts - starts;
			Crabzillas.ScriptedFire[0] = false;
			yield return new WaitForSecondsRealtime(0.6f);
			bool flag = !audio.CrabHumPlaying(0);
			WorldBoxMod.QuietLevelForTest = -1f;
			_mod.SetPitchDegrees(0f);
			Log.Info($"CRAB hum: 3D sound {audio.Spatial}; ears quiet {quiet[0]:F1}/{quiet[1]:F1}, right claw {right[0]:F1}/{right[1]:F1}, left claw {left[0]:F1}/{left[1]:F1} dB (left/right)");
			Log.Info(string.Format("CRAB {0} the right claw alone hums, started once (playing {1}/{2}, starts {3})", (rightOn && rightStill && rightStarts == 1) ? "PASS" : "FAIL", rightOn, rightStill, rightStarts));
			Log.Info(string.Format("CRAB {0} the left claw alone hums, started once (playing {1}, starts {2})", (leftOn && leftStarts == 1) ? "PASS" : "FAIL", leftOn, leftStarts));
			Log.Info("CRAB " + ((rightOff && flag) ? "PASS" : "FAIL") + " the hum stops when the claw stops firing");
			if (audio.Spatial)
			{
				float num = Mathf.Max(right[0], right[1]);
				float num2 = Mathf.Max(left[0], left[1]);
				float num3 = Mathf.Max(quiet[0], quiet[1]);
				Log.Info(string.Format("CRAB {0} the hum is heard over the quiet ({1:F1} and {2:F1} dB)", (num > num3 + 6f && num2 > num3 + 6f) ? "PASS" : "FAIL", num - num3, num2 - num3));
				Log.Info(string.Format("CRAB {0} each claw's hum from its own side (right claw {1:F1} dB to the right, left claw {2:F1} dB to the left)", (right[1] - right[0] > 2f && left[0] - left[1] > 2f) ? "PASS" : "FAIL", right[1] - right[0], left[0] - left[1]));
			}
		}

		private static IEnumerator Listen(FirstPersonAudio audio, float seconds, float[] ears)
		{
			double l = 0.0;
			double r = 0.0;
			int n = 0;
			for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
			{
				yield return null;
				if (audio.EarsForTest(out var left, out var right))
				{
					l += (double)(left * left);
					r += (double)(right * right);
					n++;
				}
			}
			ears[0] = 20f * Mathf.Log10(Mathf.Max(1E-09f, Mathf.Sqrt((float)(l / (double)Mathf.Max(1, n)))));
			ears[1] = 20f * Mathf.Log10(Mathf.Max(1E-09f, Mathf.Sqrt((float)(r / (double)Mathf.Max(1, n)))));
		}

		private static bool Land(WorldTile t)
		{
			if (t != null && t.Type != null && !t.Type.liquid)
			{
				return !t.Type.lava;
			}
			return false;
		}

		private static void Hold(List<Actor> list)
		{
			foreach (Actor item in list)
			{
				try
				{
					if (item != null && item.isAlive())
					{
						item.cancelAllBeh();
						item.makeWait(30f);
					}
				}
				catch
				{
				}
			}
		}

		private IEnumerator Dump(WorldTile at)
		{
			Actor crab = Spawn("crabzilla", at);
			if (crab == null)
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(2f);
			if (!crab.isAlive() || crab.avatar == null)
			{
				Log.Error("CRAB no avatar");
				yield break;
			}
			Vector2 current_position = crab.current_position;
			Log.Info(string.Format("CRAB actor at {0:F2},{1:F2} tile {2},{3} size {4} stats size {5:F2} scale {6:F2}", current_position.x, current_position.y, at.x, at.y, crab.asset.actor_size, ((BaseSimObject)crab).stats["size"], ((BaseSimObject)crab).stats["scale"]));
			Transform transform = crab.avatar.transform;
			Log.Info($"CRAB avatar pos {transform.position.x - current_position.x:F2},{transform.position.y - current_position.y:F2},{transform.position.z:F2} lossy {transform.lossyScale.x:F3},{transform.lossyScale.y:F3}");
			string text = Path.Combine(Log.Folder, "crab");
			Directory.CreateDirectory(text);
			HashSet<string> saved = new HashSet<string>();
			StringBuilder stringBuilder = new StringBuilder();
			Walk(transform, "", current_position, stringBuilder, text, saved);
			string[] array = stringBuilder.ToString().Split('\n');
			foreach (string text2 in array)
			{
				if (text2.Length > 0)
				{
					Log.Info("CRAB " + text2);
				}
			}
			Crabzilla crabzilla = crab.avatar.GetComponent<Crabzilla>();
			if (crabzilla == null)
			{
				crabzilla = crab.avatar.GetComponentInChildren<Crabzilla>(includeInactive: true);
			}
			if (crabzilla != null)
			{
				Log.Info($"CRAB crabzilla z_pos {crabzilla.z_pos:F2} bodyPos {crabzilla.bodyPos} moveRotationLimit {crabzilla.moveRotationLimit:F2} mainBody {Where((crabzilla.mainBody != null) ? crabzilla.mainBody.transform : null, current_position)}");
				CrabArm[] array2 = new CrabArm[2] { crabzilla.arm1, crabzilla.arm2 };
				foreach (CrabArm crabArm in array2)
				{
					if (crabArm == null)
					{
						continue;
					}
					Vector3 position = crabArm.joint.transform.position;
					Vector3 position2 = crabArm.laserPoint.position;
					Log.Info($"CRAB arm {crabArm.name} mirrored {crabArm.mirrored} joint {Where(crabArm.joint.transform, current_position)} laserPoint {Where(crabArm.laserPoint, current_position)} reach {Vector2.Distance(position, position2):F2} laser {Where(crabArm.laser.transform, current_position)}");
					if (crabArm.laserSprites == null)
					{
						continue;
					}
					for (int j = 0; j < crabArm.laserSprites.Count; j++)
					{
						Sprite sprite = crabArm.laserSprites[j];
						if (!(sprite == null))
						{
							Log.Info($"CRAB   laser frame {j} {sprite.name} rect {sprite.rect.width}x{sprite.rect.height} ppu {sprite.pixelsPerUnit} pivot {sprite.pivot} world {sprite.bounds.size.x * crabArm.laser.transform.lossyScale.x:F2}x{sprite.bounds.size.y * crabArm.laser.transform.lossyScale.y:F2}");
							SaveSprite(sprite, text, saved);
						}
					}
				}
				if (crabzilla.list_legs != null)
				{
					CrabLeg[] list_legs = crabzilla.list_legs;
					foreach (CrabLeg crabLeg in list_legs)
					{
						if (!(crabLeg == null))
						{
							CrabLegJoint legJoint = crabLeg.legJoint;
							string text3 = ((legJoint == null) ? "-" : $"j0 {Where(legJoint.Joint0, current_position)} j1 {Where(legJoint.Joint1, current_position)} hand {Where(legJoint.Hand, current_position)} target {Where(legJoint.Target, current_position)} len {legJoint.length0:F2}/{legJoint.length1:F2} targetDistance {legJoint.targetDistance:F2} angles {legJoint.angle0:F1}/{legJoint.angle1:F1} z {legJoint.actual_z_pos:F2}");
							Log.Info("CRAB leg " + crabLeg.name + " at " + Where(crabLeg.transform, current_position) + " " + text3);
						}
					}
				}
			}
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
				if (World.world.move_camera != null)
				{
					World.world.move_camera.focusOn(new Vector3(current_position.x, current_position.y + 6f, 0f));
					World.world.move_camera.forceZoom(30f);
				}
			}
			catch (Exception ex)
			{
				Log.Error("CRAB camera: " + ex.Message);
			}
			yield return new WaitForSecondsRealtime(1.5f);
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "crab_map.png"));
			yield return new WaitForSecondsRealtime(1f);
			try
			{
				crab.dieSimpleNone();
			}
			catch
			{
			}
		}

		private static string Where(Transform t, Vector2 p)
		{
			if (t == null)
			{
				return "-";
			}
			Vector3 position = t.position;
			return $"({position.x - p.x:F2},{position.y - p.y:F2},{position.z:F2})";
		}

		private void Walk(Transform t, string path, Vector2 p, StringBuilder sb, string dir, HashSet<string> saved)
		{
			string text = ((path.Length == 0) ? t.name : (path + "/" + t.name));
			Vector3 lossyScale = t.lossyScale;
			sb.Append($"{text} active {t.gameObject.activeInHierarchy} at {Where(t, p)} local {t.localPosition.x:F2},{t.localPosition.y:F2},{t.localPosition.z:F2} rot {t.eulerAngles.z:F1} lossy {lossyScale.x:F3},{lossyScale.y:F3}");
			SpriteRenderer component = t.GetComponent<SpriteRenderer>();
			if (component != null && component.sprite != null)
			{
				Sprite sprite = component.sprite;
				Bounds bounds = sprite.bounds;
				Vector3 vector = t.TransformPoint(bounds.min);
				Vector3 vector2 = t.TransformPoint(bounds.max);
				sb.Append($" sprite {sprite.name} {sprite.rect.width}x{sprite.rect.height} ppu {sprite.pixelsPerUnit} pivot {sprite.pivot.x:F0},{sprite.pivot.y:F0} enabled {component.enabled} order {component.sortingOrder} flip {component.flipX}/{component.flipY} box ({Mathf.Min(vector.x, vector2.x) - p.x:F2},{Mathf.Min(vector.y, vector2.y) - p.y:F2})-({Mathf.Max(vector.x, vector2.x) - p.x:F2},{Mathf.Max(vector.y, vector2.y) - p.y:F2})");
				SaveSprite(sprite, dir, saved);
			}
			Component[] components = t.GetComponents<Component>();
			foreach (Component component2 in components)
			{
				if (component2 != null && !(component2 is Transform) && !(component2 is SpriteRenderer))
				{
					sb.Append(" [" + component2.GetType().Name + "]");
				}
			}
			sb.Append('\n');
			for (int j = 0; j < t.childCount; j++)
			{
				Walk(t.GetChild(j), text, p, sb, dir, saved);
			}
		}

		private void SaveSprite(Sprite s, string dir, HashSet<string> saved)
		{
			if (s == null || !saved.Add(s.name))
			{
				return;
			}
			try
			{
				Texture2D texture = s.texture;
				Rect textureRect = s.textureRect;
				int num = (int)textureRect.width;
				int num2 = (int)textureRect.height;
				RenderTexture temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
				RenderTexture active = RenderTexture.active;
				Graphics.Blit(texture, temporary);
				RenderTexture.active = temporary;
				Texture2D texture2D = new Texture2D(num, num2, TextureFormat.RGBA32, mipChain: false);
				texture2D.ReadPixels(new Rect(textureRect.x, textureRect.y, num, num2), 0, 0);
				texture2D.Apply(updateMipmaps: false);
				RenderTexture.active = active;
				RenderTexture.ReleaseTemporary(temporary);
				Color32[] pixels = texture2D.GetPixels32();
				Texture2D texture2D2 = new Texture2D(num * 4, num2 * 4, TextureFormat.RGBA32, mipChain: false);
				Color32[] array = new Color32[num * 4 * num2 * 4];
				for (int i = 0; i < num2 * 4; i++)
				{
					for (int j = 0; j < num * 4; j++)
					{
						array[i * num * 4 + j] = pixels[i / 4 * num + j / 4];
					}
				}
				texture2D2.SetPixels32(array);
				texture2D2.Apply(updateMipmaps: false);
				File.WriteAllBytes(Path.Combine(dir, "crab_" + s.name + ".png"), texture2D2.EncodeToPNG());
				UnityEngine.Object.Destroy(texture2D2);
				UnityEngine.Object.Destroy(texture2D);
			}
			catch (Exception ex)
			{
				Log.Error("CRAB sprite " + s.name + ": " + ex.Message);
			}
		}

		private static void Finish(bool quit)
		{
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("CRAB DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
