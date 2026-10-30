using System;
using System.Collections;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class FreeFlyTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

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
			Settings s = _mod.Settings;
			bool savedGod = s.GodView;
			float savedSpeed = s.FlySpeed;
			int savedTime = s.TimeMode;
			s.TimeMode = 1;
			s.FlySpeed = 1f;
			bool savedGpu = s.GpuRender;
			if (options.Contains("cpu"))
			{
				s.GpuRender = false;
			}
			Log.Info(string.Format("FF renderer {0}, screen {1}x{2}", s.GpuRender ? "GPU" : "CPU", Screen.width, Screen.height));
			WorldTile home = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				if (KingTest.FoundKingdom(item, "human", 12, 4) != null)
				{
					home = item;
					break;
				}
			}
			if (home == null)
			{
				Log.Error("FF could not found a village");
				Finish(quit, s, savedGod, savedSpeed, savedTime);
				yield break;
			}
			Log.Info($"FF village at {home.x},{home.y}");
			yield return new WaitForSecondsRealtime(5f);
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			Vector2 at = new Vector2((float)home.x + 0.5f, (float)home.y + 0.5f);
			TerrainCache land = _mod.Land;
			s.GodView = true;
			for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
			{
				Hold(at, 25f);
				yield return null;
			}
			ViewState god = _mod.GodShownForTest;
			Check("the god view is up", _mod.GodShown && _mod.GodAlpha >= 1f, $"shown {_mod.GodShown}, cover {_mod.GodAlpha:F2}");
			_mod.StartFlyForTest();
			ViewState first = _mod.FlyCameraForTest;
			yield return null;
			Check("F flies", _mod.Flying && GodView.Flying && _mod.GodShown, $"flying {_mod.Flying}, mouse {GodView.Flying}, shown {_mod.GodShown}");
			float magnitude = new Vector3(first.X - god.X, first.Y - god.Y, first.Z - god.Z).magnitude;
			Check("it starts where the god view's camera was", magnitude < 0.05f && Mathf.Abs(DivePath.Wrap(first.Yaw - god.Yaw)) < 0.001f && Mathf.Abs(first.Pitch - god.Pitch) < 0.001f && first.Shear > 0.99f, $"{magnitude:F3} tiles off, yaw {first.Yaw:F3}/{god.Yaw:F3}, pitch {first.Pitch:F3}/{god.Pitch:F3}, shear {first.Shear:F2}");
			yield return new WaitForSecondsRealtime(0.7f);
			ViewState flyCameraForTest = _mod.FlyCameraForTest;
			Check("...and eases into the eye's own tilt and lens", flyCameraForTest.Shear < 0.001f && Mathf.Abs(flyCameraForTest.Fov - s.FieldOfView * (MathF.PI / 180f)) < 0.001f, $"shear {flyCameraForTest.Shear:F3}, fov {flyCameraForTest.Fov * 57.29578f:F1}");
			Check("the cursor is held for looking", Cursor.lockState == CursorLockMode.Locked, Cursor.lockState.ToString());
			_mod.SaveFrame("ff_start.png");
			Vector3 p0 = new Vector3(at.x, at.y - 16f, Ground(land, at.x, at.y - 16f) + 6f);
			_mod.PlaceFlyForTest(p0, MathF.PI / 2f, 0f);
			yield return null;
			p0 = _mod.FlyPosition;
			_mod.FlyKeysForTest = new Vector3(1f, 0f, 0f);
			float fps = 0f;
			int frames = 0;
			for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
			{
				yield return null;
				frames++;
				fps += Time.unscaledDeltaTime;
			}
			Vector3 flyPosition = _mod.FlyPosition;
			Check("forward flies the way it looks (north)", flyPosition.y - p0.y > 6f && Mathf.Abs(flyPosition.x - p0.x) < 0.3f, $"moved ({flyPosition.x - p0.x:F2}, {flyPosition.y - p0.y:F2}, {flyPosition.z - p0.z:F2}) in 1 s, {(float)frames / Mathf.Max(0.001f, fps):F0} fps");
			_mod.FlyKeysForTest = new Vector3(0f, 0f, -1f);
			float least = 99f;
			for (float t = 0f; t < 2.5f; t += Time.unscaledDeltaTime)
			{
				yield return null;
				Vector3 flyPosition2 = _mod.FlyPosition;
				least = Mathf.Min(least, flyPosition2.z - Ground(land, flyPosition2.x, flyPosition2.y));
			}
			Vector3 flyPosition3 = _mod.FlyPosition;
			Check("down stops over the land", least > 0.3f && flyPosition3.z - Ground(land, flyPosition3.x, flyPosition3.y) < 0.6f, $"least {least:F2} over the ground, ends {flyPosition3.z - Ground(land, flyPosition3.x, flyPosition3.y):F2} over it");
			_mod.FlyKeysForTest = null;
			_mod.PlaceFlyForTest(new Vector3(at.x, at.y - 12f, Ground(land, at.x, at.y - 12f) + 1.6f), MathF.PI / 2f, -4f);
			yield return new WaitForSecondsRealtime(0.5f);
			_mod.SaveFrame("ff_low.png");
			Building house = PlaceHouse(at + new Vector2(9f, -4f));
			if (house == null)
			{
				Fail("a house to fly at", "none could be placed");
			}
			else
			{
				Vector2 hp = ((house.current_tile != null) ? new Vector2((float)house.current_tile.x + 0.5f, (float)house.current_tile.y + 0.5f) : at);
				float t = Ground(land, hp.x, hp.y - 4f);
				bool flag2 = false;
				for (float t2 = 0f; t2 < 6f; t2 += Time.unscaledDeltaTime)
				{
					if (flag2)
					{
						break;
					}
					_mod.PlaceFlyForTest(new Vector3(hp.x, hp.y - 4f, t + 0.7f), MathF.PI / 2f, 0f);
					yield return null;
					flag2 = BuildingFloors.ReadyFor(house);
				}
				Check("the house's shape is known near it", flag2, house.asset.id);
				Vector3 h0 = _mod.FlyPosition;
				bool inside = false;
				_mod.FlyKeysForTest = new Vector3(1f, 0f, 0f);
				for (float t2 = 0f; t2 < 2.5f; t2 += Time.unscaledDeltaTime)
				{
					yield return null;
					inside |= WorldBoxMod.FlyBlockedForTest(_mod.FlyPosition);
				}
				_mod.FlyKeysForTest = null;
				Vector3 h1 = _mod.FlyPosition;
				Check("flown at, the camera stops at the house's wall", !inside && h1.y - h0.y > 0.8f && h1.y < hp.y + 0.3f && Mathf.Abs(h1.z - h0.z) < 0.6f, $"from y {h0.y:F2} to {h1.y:F2} (house at {hp.y:F2}), z {h0.z:F2} -> {h1.z:F2}, ever inside {inside}");
				_mod.PlaceFlyForTest(new Vector3(h1.x, h1.y - 0.5f, h1.z), MathF.PI / 2f, 0f);
				yield return new WaitForSecondsRealtime(0.3f);
				_mod.SaveFrame("ff_house.png");
				_mod.PlaceFlyForTest(new Vector3(h1.x, h1.y - 1.2f, h1.z), MathF.PI / 2f, 0f);
				_mod.FlyKeysForTest = new Vector3(0f, 0f, 1f);
				for (float t2 = 0f; t2 < 0.35f; t2 += Time.unscaledDeltaTime)
				{
					yield return null;
					inside |= WorldBoxMod.FlyBlockedForTest(_mod.FlyPosition);
				}
				float before = _mod.FlyPosition.z;
				_mod.FlyKeysForTest = new Vector3(1f, 0f, 0.6f);
				for (float t2 = 0f; t2 < 2f; t2 += Time.unscaledDeltaTime)
				{
					yield return null;
					inside |= WorldBoxMod.FlyBlockedForTest(_mod.FlyPosition);
				}
				_mod.FlyKeysForTest = null;
				Vector3 flyPosition4 = _mod.FlyPosition;
				Check("up and forward, it goes over the roof", !inside && flyPosition4.y > hp.y + 1f, $"from z {before:F2} to y {flyPosition4.y:F2}, z {flyPosition4.z:F2}, ever inside {inside}");
			}
			Actor villager = Villager(at);
			if (villager == null)
			{
				Fail("a villager to look at", "none");
			}
			else
			{
				bool inside = false;
				bool pointMatches = false;
				string seen = "nothing";
				for (float before = 0f; before < 1.2f; before += Time.unscaledDeltaTime)
				{
					Aim(villager, 5f, 1.5f);
					yield return null;
					if (GodView.MouseValid && GodView.MouseCreature != null)
					{
						seen = WorldBoxMod.Describe(GodView.MouseCreature);
						inside |= GodView.MouseCreature == villager;
						Vector2 mousePos = World.world.getMousePos();
						pointMatches |= (mousePos - GodView.MousePoint).sqrMagnitude < 0.0001f;
					}
				}
				Check("the crosshair rests on the villager", inside, "saw " + seen + ", wanted " + WorldBoxMod.Describe(villager));
				Check("...and the game's point under the mouse is on it", pointMatches || !GodView.Installed, $"hooks {GodView.Installed}");
				_mod.SaveFrame("ff_crosshair.png");
			}
			_mod.PlaceFlyForTest(new Vector3(at.x, at.y, Ground(land, at.x, at.y) + 10f), MathF.PI / 2f, -88f);
			yield return new WaitForSecondsRealtime(0.6f);
			Check("looking right down it still draws", _mod.GodShown && _mod.Flying, $"pitch {_mod.FlyPitchDegrees:F1}");
			_mod.SaveFrame("ff_down.png");
			float top = _mod.FlyTopForTest;
			float yaw = 0.7f;
			_mod.PlaceFlyForTest(new Vector3(at.x, at.y - 30f, Ground(land, at.x, at.y - 30f) + top - 3f), yaw, -30f);
			yield return null;
			_mod.FlyKeysForTest = new Vector3(0f, 0f, 1f);
			bool flag3 = false;
			for (float before = 0f; before < 3f; before += Time.unscaledDeltaTime)
			{
				if (flag3)
				{
					break;
				}
				yield return null;
				flag3 = _mod.FlyLeaving;
			}
			_mod.FlyKeysForTest = null;
			Check("up past the top it goes on into the god view", flag3, $"top {top:F0} tiles over the ground");
			ViewState last = _mod.FlyCameraForTest;
			for (float before = 0f; before < 2f; before += Time.unscaledDeltaTime)
			{
				if (!_mod.Flying)
				{
					break;
				}
				last = _mod.FlyCameraForTest;
				yield return null;
			}
			yield return null;
			yield return null;
			ViewState godShownForTest = _mod.GodShownForTest;
			float magnitude2 = new Vector3(godShownForTest.X - last.X, godShownForTest.Y - last.Y, godShownForTest.Z - last.Z).magnitude;
			Check("...landed: the god view, turned the way it flew", !_mod.Flying && s.GodView && _mod.GodShown && Mathf.Abs(DivePath.Wrap(_mod.GodTurnForTest - (yaw - MathF.PI / 2f))) < 0.001f, $"flying {_mod.Flying}, god view {s.GodView}, shown {_mod.GodShown}, turn {_mod.GodTurnForTest:F3} for {yaw - MathF.PI / 2f:F3}");
			Check("...without a jump", magnitude2 < 1.5f && Mathf.Abs(godShownForTest.Pitch - last.Pitch) < 0.05f, $"{magnitude2:F2} tiles, pitch {last.Pitch:F3} -> {godShownForTest.Pitch:F3}, shear {last.Shear:F2} -> {godShownForTest.Shear:F2}");
			Check("...the cursor back", Cursor.lockState == CursorLockMode.None && Cursor.visible, Cursor.lockState.ToString());
			_mod.SaveFrame("ff_landed.png");
			for (float before = 0f; before < 0.5f; before += Time.unscaledDeltaTime)
			{
				Hold(at, 25f);
				yield return null;
			}
			_mod.StartFlyForTest();
			yield return new WaitForSecondsRealtime(0.5f);
			bool esc = _mod.FlyEscapeForTest();
			for (float before = 0f; before < 1.5f; before += Time.unscaledDeltaTime)
			{
				if (!_mod.Flying)
				{
					break;
				}
				yield return null;
			}
			Check("Esc lands back in the god view", esc && !_mod.Flying && _mod.GodShown, $"taken {esc}, flying {_mod.Flying}");
			Actor actor = Villager(at);
			if (actor == null)
			{
				Fail("a villager to follow", "none");
			}
			else
			{
				try
				{
					World.world.followUnit(actor);
				}
				catch (Exception ex)
				{
					Log.Info("FF follow: " + ex.Message);
				}
				yield return null;
				bool pointMatches = MoveCamera.hasFocusUnit();
				_mod.StartFlyForTest();
				yield return null;
				bool inside = !MoveCamera.hasFocusUnit();
				try
				{
					HotkeyLibrary.follow_unit.just_pressed_action?.Invoke(HotkeyLibrary.follow_unit);
				}
				catch (Exception ex2)
				{
					Log.Info("FF follow: " + ex2.Message);
				}
				yield return null;
				Check("flight lets go of the game's follow, and the game's F stays quiet", pointMatches && inside && !MoveCamera.hasFocusUnit() && _mod.Flying, $"following before {pointMatches}, dropped {inside}, following now {MoveCamera.hasFocusUnit()}, flying {_mod.Flying}");
				_mod.LeaveFlyForTest();
				for (float before = 0f; before < 1.5f; before += Time.unscaledDeltaTime)
				{
					if (!_mod.Flying)
					{
						break;
					}
					yield return null;
				}
			}
			villager = Villager(at);
			if (villager == null)
			{
				Fail("a villager to take over", "none");
			}
			else
			{
				_mod.StartFlyForTest();
				yield return new WaitForSecondsRealtime(0.6f);
				for (float before = 0f; before < 0.5f; before += Time.unscaledDeltaTime)
				{
					Aim(villager, 6f, 2f);
					yield return null;
				}
				_mod.NoDive = false;
				Aim(villager, 6f, 2f);
				yield return null;
				Actor under = GodView.MouseCreature;
				_mod.FlyTakeOverForTest();
				bool inside = false;
				int shot = 0;
				for (float before = 0f; before < 5f; before += Time.unscaledDeltaTime)
				{
					if (_mod.IsFirstPerson && !_mod.Diving)
					{
						break;
					}
					yield return null;
					inside |= _mod.Diving;
					if (_mod.Diving && shot < 3 && before > (float)shot * 0.3f)
					{
						_mod.SaveFrame($"ff_dive{shot++}.png");
					}
				}
				_mod.NoDive = true;
				Check("middle click takes over the creature under the crosshair, diving in", _mod.IsFirstPerson && inside && !_mod.Flying && _mod.Host != null && (_mod.Host == under || _mod.Host == villager), string.Format("first person {0}, dove {1}, flying {2}, host {3}, under {4}", _mod.IsFirstPerson, inside, _mod.Flying, (_mod.Host != null) ? WorldBoxMod.Describe(_mod.Host) : "none", (under != null) ? WorldBoxMod.Describe(under) : "none"));
			}
			if (_mod.IsFirstPerson)
			{
				yield return new WaitForSecondsRealtime(1f);
				Actor under = _mod.Host;
				try
				{
					HotkeyLibrary.follow_unit.just_pressed_action?.Invoke(HotkeyLibrary.follow_unit);
				}
				catch (Exception ex3)
				{
					Log.Info("FF follow: " + ex3.Message);
				}
				yield return null;
				yield return null;
				bool flag4 = false;
				try
				{
					flag4 = MoveCamera.inSpectatorMode();
				}
				catch
				{
				}
				Check("the game's F (follow) held back in first person", _mod.IsFirstPerson && ControllableUnit.isControllingUnit() && _mod.Host == under && !flag4, $"first person {_mod.IsFirstPerson}, controlling {ControllableUnit.isControllingUnit()}, same body {_mod.Host == under}, spectating {flag4}");
				ViewState eye = _mod.EyeNowForTest;
				_mod.LiftOutForTest();
				for (float before = 0f; before < 2f; before += Time.unscaledDeltaTime)
				{
					if (_mod.Flying)
					{
						break;
					}
					yield return null;
				}
				yield return null;
				Vector3 flyPosition5 = _mod.FlyPosition;
				float magnitude3 = new Vector3(flyPosition5.x - eye.X, flyPosition5.y - eye.Y, flyPosition5.z - eye.Z).magnitude;
				Check("F held: the body let go of, flight from its eyes", _mod.Flying && !_mod.IsFirstPerson && !ControllableUnit.isControllingUnit() && magnitude3 < 1f, $"flying {_mod.Flying}, first person {_mod.IsFirstPerson}, controlling {ControllableUnit.isControllingUnit()}, {magnitude3:F2} tiles from the eye, body alive {under?.isAlive() ?? false}");
				_mod.SaveFrame("ff_lift.png");
				_mod.LeaveFlyForTest();
				for (float before = 0f; before < 1.5f; before += Time.unscaledDeltaTime)
				{
					if (!_mod.Flying)
					{
						break;
					}
					yield return null;
				}
			}
			else
			{
				Fail("F held in first person", "not in first person");
			}
			yield return RealKeys(at);
			s.GpuRender = savedGpu;
			Finish(quit, s, savedGod, savedSpeed, savedTime);
		}

		private IEnumerator RealKeys(Vector2 at)
		{
			Actor actor = Villager(at);
			if (actor == null)
			{
				Fail("keys: a villager", "none");
				yield break;
			}
			_mod.NoDive = true;
			Possess.Swap(actor);
			for (float t = 0f; t < 4f; t += Time.unscaledDeltaTime)
			{
				if (_mod.IsFirstPerson)
				{
					break;
				}
				yield return null;
			}
			yield return new WaitForSecondsRealtime(1f);
			Check("keys: in first person", _mod.IsFirstPerson && !_mod.Flying, $"first person {_mod.IsFirstPerson}");
			if (!_mod.IsFirstPerson)
			{
				yield break;
			}
			PlayInput.TestPress(KeyCode.F);
			bool flag = false;
			for (float t = 0f; t < 2f; t += Time.unscaledDeltaTime)
			{
				if (flag)
				{
					break;
				}
				yield return null;
				flag = _mod.Flying;
			}
			yield return new WaitForSecondsRealtime(0.3f);
			Check("keys: F held lifts out into flight", _mod.Flying && !_mod.IsFirstPerson && _mod.GodShown && !_mod.FlyLeaving, $"flying {_mod.Flying}, first person {_mod.IsFirstPerson}, shown {_mod.GodShown}, leaving {_mod.FlyLeaving}, cursor {Cursor.lockState}");
			Vector3 p0 = _mod.FlyPosition;
			PlayInput.TestPress(KeyCode.W);
			yield return new WaitForSecondsRealtime(1f);
			Vector3 p1 = _mod.FlyPosition;
			_mod.SaveFrame("ff_keys_w.png");
			PlayInput.TestRelease(KeyCode.W);
			string why = $"moved {(p1 - p0).magnitude:F2} tiles from {p0}, yaw {_mod.FlyYaw * 57.29578f:F0}, pitch {_mod.FlyPitchDegrees:F0}, flying {_mod.Flying}, leaving {_mod.FlyLeaving}, blocked here {WorldBoxMod.FlyBlockedForTest(p1)}, a step on {WorldBoxMod.FlyBlockedForTest(p1 + new Vector3(Mathf.Cos(_mod.FlyYaw), Mathf.Sin(_mod.FlyYaw), 0f) * 0.15f)}";
			if ((p1 - p0).magnitude < 3f)
			{
				PlayInput.TestPress(KeyCode.Space);
				yield return new WaitForSecondsRealtime(0.5f);
				PlayInput.TestRelease(KeyCode.Space);
				Vector3 up = _mod.FlyPosition;
				PlayInput.TestPress(KeyCode.S);
				yield return new WaitForSecondsRealtime(0.5f);
				PlayInput.TestRelease(KeyCode.S);
				why += $"; then up {up.z - p1.z:F2}, back {(_mod.FlyPosition - up).magnitude:F2}";
			}
			Check("keys: W flies on, F still held", (p1 - p0).magnitude > 3f && _mod.Flying, why);
			PlayInput.TestRelease(KeyCode.F);
			yield return new WaitForSecondsRealtime(0.4f);
			Vector3 p2 = _mod.FlyPosition;
			PlayInput.TestPress(KeyCode.W);
			yield return new WaitForSecondsRealtime(0.6f);
			PlayInput.TestRelease(KeyCode.W);
			Check("keys: F let go, W still flies", (_mod.FlyPosition - p2).magnitude > 1.5f && _mod.Flying, $"moved {(_mod.FlyPosition - p2).magnitude:F2}");
			yield return new WaitForSecondsRealtime(0.3f);
			PlayInput.TestPress(KeyCode.F);
			yield return null;
			yield return null;
			PlayInput.TestRelease(KeyCode.F);
			for (float t = 0f; t < 2f; t += Time.unscaledDeltaTime)
			{
				if (!_mod.Flying)
				{
					break;
				}
				yield return null;
			}
			yield return new WaitForSecondsRealtime(0.3f);
			bool flag2 = false;
			try
			{
				flag2 = MoveCamera.hasFocusUnit();
			}
			catch
			{
			}
			Check("keys: F lands in the god view, free to move", !_mod.Flying && _mod.GodShown && !flag2 && Cursor.lockState == CursorLockMode.None, $"flying {_mod.Flying}, shown {_mod.GodShown}, following {flag2}, cursor {Cursor.lockState}");
			Camera camera = World.world.camera;
			Vector3 c0 = camera.transform.position;
			PlayInput.TestPress(KeyCode.F);
			yield return null;
			yield return null;
			PlayInput.TestRelease(KeyCode.F);
			for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
			{
				if (_mod.Flying)
				{
					break;
				}
				yield return null;
			}
			yield return new WaitForSecondsRealtime(0.5f);
			Vector3 q0 = _mod.FlyPosition;
			PlayInput.TestPress(KeyCode.W);
			yield return new WaitForSecondsRealtime(1f);
			PlayInput.TestRelease(KeyCode.W);
			Check("keys: F on the god view flies, W moves it", _mod.Flying && (_mod.FlyPosition - q0).magnitude > 3f, $"flying {_mod.Flying}, moved {(_mod.FlyPosition - q0).magnitude:F2}, map camera from {c0}");
			_mod.LeaveFlyForTest();
			for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
			{
				if (!_mod.Flying)
				{
					break;
				}
				yield return null;
			}
		}

		private void Aim(Actor a, float back, float up)
		{
			Vector2 current_position = a.current_position;
			float num = Mathf.Max(_mod.Land.SampleHeight(current_position.x, current_position.y), 0f);
			Vector3 p = new Vector3(current_position.x, current_position.y - back, num + up);
			_mod.PlaceFlyForTest(p, MathF.PI / 2f, 0f);
			Vector3 flyPosition = _mod.FlyPosition;
			float pitchDegrees = Mathf.Atan2(num + 0.45f - flyPosition.z, back) * 57.29578f;
			_mod.PlaceFlyForTest(flyPosition, MathF.PI / 2f, pitchDegrees);
		}

		private static float Ground(TerrainCache land, float x, float y)
		{
			return Mathf.Max(land.SampleHeight(x, y), 0f);
		}

		private static Actor Villager(Vector2 near)
		{
			Actor result = null;
			float num = 900f;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple.isAlive() && !simple.is_inside_building && simple.asset != null && simple.asset.id.Contains("human") && Possess.CanJumpTo(simple))
				{
					float sqrMagnitude = (simple.current_position - near).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						result = simple;
					}
				}
			}
			return result;
		}

		private static Building PlaceHouse(Vector2 near)
		{
			for (int i = 0; i < 60; i++)
			{
				float f = (float)i * 0.9f;
				float num = (float)i * 0.4f;
				WorldTile tile = World.world.GetTile((int)(near.x + Mathf.Cos(f) * num), (int)(near.y + Mathf.Sin(f) * num));
				if (tile == null || tile.Type == null || !tile.Type.ground || tile.Type.block || tile.Type.liquid || tile.building != null)
				{
					continue;
				}
				try
				{
					Building building = World.world.buildings.addBuilding("house_human_1", tile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
					if (building != null)
					{
						return building;
					}
				}
				catch (Exception ex)
				{
					Log.Info("FF house: " + ex.Message);
				}
			}
			return null;
		}

		private static void Hold(Vector2 at, float zoom)
		{
			try
			{
				Camera camera = World.world.camera;
				camera.transform.position = new Vector3(at.x, at.y, camera.transform.position.z);
				camera.orthographicSize = zoom;
				if (World.world.move_camera != null)
				{
					World.world.move_camera.setTargetZoom(zoom);
				}
				World.world.setZoomOrthographic(zoom);
			}
			catch (Exception ex)
			{
				Log.Error("FF camera: " + ex.Message);
			}
		}

		private void Check(string what, bool ok, string detail)
		{
			if (ok)
			{
				_pass++;
			}
			else
			{
				_fail++;
			}
			Log.Info("FF " + (ok ? "PASS" : "FAIL") + " " + what + ": " + detail);
		}

		private void Fail(string what, string detail)
		{
			Check(what, ok: false, detail);
		}

		private void Finish(bool quit, Settings s, bool god, float speed, int time)
		{
			_mod.FlyKeysForTest = null;
			GodView.TestMouse = null;
			_mod.NoDive = true;
			s.GodView = god;
			s.FlySpeed = speed;
			s.TimeMode = time;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info($"FF {_pass} passed, {_fail} failed");
			Log.Info("FF DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
