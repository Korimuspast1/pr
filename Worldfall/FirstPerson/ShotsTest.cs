using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class ShotsTest : MonoBehaviour
	{
		private sealed class ShotWatch
		{
			public int Launched;

			public int Ended;

			public IEnumerator Wait(float seconds = 3f)
			{
				for (float t = 0f; t < seconds; t += 0.02f)
				{
					if (Ended() != Ended)
					{
						break;
					}
					yield return new WaitForSecondsRealtime(0.02f);
				}
				if (FirstPersonShots.Launched == Launched)
				{
					WorldBoxMod instance = WorldBoxMod.Instance;
					Actor host = instance.Host;
					Log.Error($"SHOT no shot was launched: release {PossessionHooks.ReleaseAttack}, hold {PossessionHooks.HoldAttack}, block {PossessionHooks.BlockAttack}, " + string.Format("work {0}, bow {1}, timer {2:F2}, aim ready {3}, possessed {4}", (instance.Work.Target == null) ? "-" : instance.Work.Target.asset?.id, instance.Bow.InHand, host?.attack_timer, FirstPersonAim.Ready(host), ((BaseSimObject)host)?.hasStatus("possessed")));
				}
			}
		}

		private WorldBoxMod _mod;

		private Actor _king;

		private readonly List<Actor> _hold = new List<Actor>();

		private readonly List<Actor> _freeze = new List<Actor>();

		private Vector2 _home;

		private bool _homeSet;

		private float _lane = float.NaN;

		private Vector2 _laneStart;

		private int _pass;

		private int _fail;

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			bool quit = File.ReadAllText(path).Contains("quit");
			bool feel = File.ReadAllText(path).Contains("bowfeel");
			bool pace = File.ReadAllText(path).Contains("blowpace");
			bool shotgun = File.ReadAllText(path).Contains("shotgun");
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
			foreach (WorldTile item in KingTest.GoodSites())
			{
				_king = KingTest.FoundKingdom(item, "human", 4, 0);
				if (_king != null)
				{
					break;
				}
			}
			if (_king == null)
			{
				Log.Error("SHOT could not found a village");
				Finish(quit);
				yield break;
			}
			yield return new WaitForSecondsRealtime(1f);
			ControllableUnit.setControllableCreature(_king);
			for (float t = 0f; t < 6f; t += 0.1f)
			{
				if (_mod.IsFirstPerson)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.1f);
			}
			if (!_mod.IsFirstPerson)
			{
				Log.Error("SHOT first person did not start");
				Finish(quit);
				yield break;
			}
			yield return new WaitForSecondsRealtime(2.5f);
			_mod.HintUntil = 0f;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			Settings s = _mod.Settings;
			float savedBob = s.HeadBob;
			int savedTime = s.TimeMode;
			s.HeadBob = 0f;
			s.TimeMode = 1;
			_mod.ForceRelight();
			Immortal(_king);
			FreezeAround();
			FirstPersonShots.Trace = true;
			if (feel)
			{
				yield return Run("bowfeel", BowFeel());
			}
			else if (pace)
			{
				yield return Run("blowpace", BlowPace());
			}
			else if (shotgun)
			{
				yield return Run("shotgun", Shotgun());
			}
			else
			{
				yield return Run("stun", Stun());
				yield return Run("bow", Bow());
				yield return Run("melee", Melee());
				yield return Run("bear", Beast("bear"));
				yield return Run("skeleton", Thrower("skeleton"));
			}
			s.HeadBob = savedBob;
			s.TimeMode = savedTime;
			FirstPersonShots.Trace = false;
			Log.Info($"SHOT checks passed {_pass}, failed {_fail}");
			Finish(quit);
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("SHOT --- " + name);
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
					goto IL_007b;
				}
				catch (Exception ex)
				{
					Log.Error("SHOT " + name + " failed: " + ex);
				}
				break;
				IL_007b:
				yield return current;
			}
			foreach (Actor item in _hold)
			{
				try
				{
					if (item != null && item.isAlive() && item != _mod.Host)
					{
						item.dieSimpleNone();
					}
				}
				catch
				{
				}
			}
			_hold.Clear();
		}

		private void Check(bool ok, string what)
		{
			if (ok)
			{
				_pass++;
			}
			else
			{
				_fail++;
			}
			Log.Info("SHOT " + (ok ? "PASS" : "FAIL") + " " + what);
		}

		private IEnumerator Stun()
		{
			int shrugged = PossessionCombat.StunsShrugged;
			try
			{
				_king.makeStunned();
			}
			catch (Exception ex)
			{
				Log.Error("SHOT stun: " + ex.Message);
			}
			yield return null;
			Check(!((BaseSimObject)_king).hasStatus("stunned") && PossessionCombat.StunsShrugged > shrugged, string.Format("makeStunned on you: stunned {0}, shrugged {1}->{2}", ((BaseSimObject)_king).hasStatus("stunned"), shrugged, PossessionCombat.StunsShrugged));
			try
			{
				((BaseSimObject)_king).addStatusEffect("stunned", 5f, pColorEffect: true);
			}
			catch
			{
			}
			for (int i = 0; i < 3; i++)
			{
				yield return null;
			}
			Check(!((BaseSimObject)_king).hasStatus("stunned"), string.Format("stunned status on you shaken off: stunned {0}, timer {1:F2}", ((BaseSimObject)_king).hasStatus("stunned"), _king.timer_action));
			Vector2 before = _king.current_position;
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			yield return new WaitForSecondsRealtime(0.6f);
			PossessionHooks.ScriptedInput = Vector2.zero;
			yield return new WaitForSecondsRealtime(0.3f);
			float magnitude = (_king.current_position - before).magnitude;
			Check(magnitude > 0.3f, $"you walk on after a stun: moved {magnitude:F2} tiles");
			Actor sheep = Spawn("sheep", Ahead(3f, 1.5f));
			yield return new WaitForSecondsRealtime(0.2f);
			if (sheep != null)
			{
				try
				{
					sheep.makeStunned(3f);
				}
				catch
				{
				}
				yield return null;
				Check(((BaseSimObject)sheep).hasStatus("stunned"), string.Format("a sheep is still stunned: {0}", ((BaseSimObject)sheep).hasStatus("stunned")));
			}
		}

		private IEnumerator Bow()
		{
			if (!Equip(_king, "bow_wood"))
			{
				yield break;
			}
			for (int i = 0; i < 30; i++)
			{
				if (_mod.Bow.InHand)
				{
					break;
				}
				yield return null;
			}
			Check(_mod.Bow.InHand, "the bow is in your hands");
			_home = _king.current_position;
			_homeSet = true;
			int clear;
			float yaw = (_lane = ClearYaw(out clear));
			_laneStart = _home;
			Log.Info($"SHOT shooting along {yaw * 57.29578f:F0} degrees, clear for {clear} tiles");
			_mod.SetYaw(yaw);
			_mod.SetPitchDegrees(0f);
			yield return Settle();
			float far = Mathf.Max(8f, Mathf.Min(24f, (float)clear - 1f));
			yield return Shoot("sheep 6", "sheep", Mathf.Min(6f, far), 0f, expectHit: true);
			yield return Shoot("sheep 13", "sheep", Mathf.Min(13f, far), 0f, expectHit: true);
			yield return Shoot($"sheep {far:F0}", "sheep", far, 0f, expectHit: true);
			yield return Shoot("cat 5", "cat", 5f, 0f, expectHit: true);
			yield return Shoot("beside a sheep (miss)", "sheep", Mathf.Min(9f, far), 0.7f, expectHit: false);
			Home();
			yield return Settle();
			WorldTile worldTile = HillSpot();
			if (worldTile != null)
			{
				yield return Shoot("sheep on a slope", "sheep", 0f, 0f, expectHit: true, worldTile);
			}
			else
			{
				Log.Info("SHOT no hill spot near (skipped)");
			}
			_mod.SetYaw(yaw);
			yield return Settle();
			Vector3 eye = _mod.EyeForTest;
			float num = Mathf.Cos(_mod.ViewYaw);
			float num2 = Mathf.Sin(_mod.ViewYaw);
			float num3 = _mod.TerrainForTest.SampleHeight(eye.x + num * 9f, eye.y + num2 * 9f);
			_mod.SetPitchDegrees(Mathf.Atan((num3 - eye.z) / 9f) * 57.29578f);
			yield return Settle();
			ShotWatch ground;
			yield return Loose("ground 9", out ground);
			yield return ground.Wait();
			Vec3 lastAim = FirstPersonShots.LastAim;
			float num4 = Dist(FirstPersonShots.LastEnd, new Vector3(lastAim.X, lastAim.Y, lastAim.Z));
			Check(FirstPersonShots.LastWhat == "ground" && num4 < 0.4f, $"ground shot: ended at {FirstPersonShots.LastWhat}, {num4:F2} tiles from the crosshair's point {Flat(lastAim, eye):F1} tiles out");
			_mod.SetPitchDegrees(20f);
			yield return Settle();
			ShotWatch sky;
			yield return Loose("sky", out sky);
			yield return sky.Wait(5f);
			float num5 = Flat(FirstPersonShots.LastEnd, eye);
			Check(num5 > Mathf.Min(20f, clear), $"sky shot: ended at {FirstPersonShots.LastWhat} {num5:F1} tiles away");
			_mod.SetYaw(yaw);
			_mod.SetPitchDegrees(0f);
			yield return Wall();
			_mod.SetYaw(yaw);
			_mod.SetPitchDegrees(0f);
		}

		private IEnumerator Shoot(string label, string kind, float dist, float aside, bool expectHit, WorldTile at = null)
		{
			Home();
			if (at == null && !float.IsNaN(_lane))
			{
				_mod.SetYaw(_lane);
			}
			yield return Settle();
			Actor target = Spawn(kind, (at != null) ? new Vector2(at.posV3.x, at.posV3.y) : Ahead(dist, 0f));
			if (target == null)
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(0.4f);
			for (int i = 0; i < 4; i++)
			{
				Hold();
				yield return null;
			}
			if (!Hitboxes.Get(target, out var box))
			{
				Log.Error("SHOT " + label + ": no hitbox");
				yield break;
			}
			Vector3 eye = _mod.EyeForTest;
			Vec3 c = box.Centre + new Vec3(0f, 0f, box.HalfUp * 0.45f);
			float num = c.X - eye.x;
			float num2 = c.Y - eye.y;
			float flat = Mathf.Sqrt(num * num + num2 * num2);
			if (aside != 0f)
			{
				float num3 = num2 / flat;
				float num4 = (0f - num) / flat;
				float num5 = aside + Mathf.Max(box.HalfA, box.HalfB);
				c = new Vec3(c.X + num3 * num5, c.Y + num4 * num5, c.Z);
				num = c.X - eye.x;
				num2 = c.Y - eye.y;
				flat = Mathf.Sqrt(num * num + num2 * num2);
			}
			float pitchSet = Mathf.Atan((c.Z - eye.z) / flat) * 57.29578f;
			_mod.SetYaw(Mathf.Atan2(num2, num));
			_mod.SetPitchDegrees(pitchSet);
			yield return Settle();
			float health = target.getHealth();
			bool behind = _mod.Picked is Building b && AssetStyle.IsSolid(b) && _mod.Renderer.PickDepth < flat - 0.3f;
			string picked = ((_mod.Picked == target) ? "on it" : ((_mod.Picked != null) ? ("on " + _mod.Picked.GetType().Name) : "nothing picked"));
			picked += $" (pick tag {_mod.Renderer.PickTag} at {_mod.Renderer.PickDepth:F2}, view pitch {Mathf.Atan(_mod.View.Pitch) * 57.29578f:F1} for {pitchSet:F1}, seen {Hitboxes.Seen(target)})";
			_mod.SaveFrame("shot_" + label.Replace(' ', '_').Replace("(", "").Replace(")", "")
				.Replace(":", "") + "_aim.png");
			ShotWatch watch;
			yield return Loose(label, out watch);
			yield return new WaitForSecondsRealtime(Mathf.Min(0.12f, flat / 60f));
			Vec3 at2;
			float ground;
			string mid = (FirstPersonShots.FlyingForTest(out at2, out ground) ? $"{Flat(at2, eye):F1} out, {at2.Z - ground:F2} over the ground" : "landed already");
			yield return watch.Wait();
			bool flag = FirstPersonShots.LastVictim == target;
			float num6 = (target.isAlive() ? ((float)target.getHealth()) : 0f);
			if (behind)
			{
				label += " (behind something solid: the shot should stop in it)";
			}
			Check((!behind) ? (flag == expectHit) : (FirstPersonShots.LastWhat == "wall" && !flag), $"{label}: crosshair {picked}, box {box.HalfA * 2f:F2}x{box.HalfB * 2f:F2}x{box.HalfUp * 2f:F2} at {flat:F1} tiles, height {c.Z - eye.z:+0.00;-0.00} from the eye; " + $"mid-flight {mid}; ended at {FirstPersonShots.LastWhat} ({Dist(FirstPersonShots.LastEnd, new Vector3(c.X, c.Y, c.Z)):F2} from the aimed point), health {health}->{num6}");
			_mod.SaveFrame("shot_" + label.Replace(' ', '_').Replace("(", "").Replace(")", "")
				.Replace(":", "") + ".png");
			try
			{
				if (target.isAlive())
				{
					target.dieSimpleNone();
				}
			}
			catch
			{
			}
		}

		private IEnumerator Wall()
		{
			Vector2 current_position = _king.current_position;
			float viewYaw = _mod.ViewYaw;
			WorldTile at = Tile(current_position.x + Mathf.Cos(viewYaw) * 7f, current_position.y + Mathf.Sin(viewYaw) * 7f);
			Building house = null;
			try
			{
				house = World.world.buildings.addBuilding("house_human_1", at, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				house?.completeConstruction();
			}
			catch (Exception ex)
			{
				Log.Error("SHOT house: " + ex.Message);
			}
			if (house == null)
			{
				Log.Info("SHOT no house (skipped)");
				yield break;
			}
			yield return new WaitForSecondsRealtime(0.6f);
			Vector3 eye = _mod.EyeForTest;
			Vector2 vector = new Vector2(at.posV3.x, at.posV3.y);
			float gz = _mod.TerrainForTest.SampleHeight(vector.x, vector.y);
			float baseYaw = Mathf.Atan2(vector.y - eye.y, vector.x - eye.x);
			float dist = Mathf.Max(1f, (vector - new Vector2(eye.x, eye.y)).magnitude);
			bool flag = false;
			for (int k = 0; k < 15; k++)
			{
				if (flag)
				{
					break;
				}
				float num = (float)(k % 5 - 2) * 0.05f;
				float num2 = 0.4f + (float)(k / 5) * 0.5f;
				_mod.SetYaw(baseYaw + num);
				_mod.SetPitchDegrees(Mathf.Atan((gz + num2 - eye.z) / dist) * 57.29578f);
				yield return Settle();
				flag = _mod.Picked == house;
			}
			string picked = ((_mod.Picked == house) ? "on the house" : ((_mod.Picked != null) ? ("on " + _mod.Picked.GetType().Name) : "nothing picked"));
			int stuck = StuckArrows.FixedCount;
			ShotWatch watch;
			yield return Loose("wall", out watch);
			yield return watch.Wait();
			yield return null;
			Check(FirstPersonShots.LastWhat == "wall", $"house wall: crosshair {picked}, ended at {FirstPersonShots.LastWhat} {Flat(FirstPersonShots.LastEnd, eye):F1} tiles off, arrows stuck {stuck}->{StuckArrows.FixedCount}");
			_mod.SaveFrame("shot_wall.png");
		}

		private IEnumerator Melee()
		{
			if (Equip(_king, "sword_iron"))
			{
				yield return new WaitForSecondsRealtime(0.3f);
				_mod.SetPitchDegrees(-10f);
				yield return Blow("sword at a sheep in front", "sheep", 1f, 0f, expectHit: true);
				yield return Blow("sword at a sheep off to the side", "sheep", 1f, 65f, expectHit: false);
				yield return Blow("sword at a sheep out of reach", "sheep", 3.5f, 0f, expectHit: false);
			}
		}

		private IEnumerator Blow(string label, string kind, float dist, float angle, bool expectHit)
		{
			Home();
			yield return Settle();
			Vector3 eyeForTest = _mod.EyeForTest;
			float f = _mod.ViewYaw + angle * (MathF.PI / 180f);
			Actor target = Spawn(kind, new Vector2(eyeForTest.x + Mathf.Cos(f) * dist, eyeForTest.y + Mathf.Sin(f) * dist));
			if (target == null)
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(0.4f);
			for (int i = 0; i < 3; i++)
			{
				Hold();
				yield return null;
			}
			if (angle == 0f)
			{
				Face(target);
			}
			yield return Settle();
			float health = target.getHealth();
			int blows = PossessionCombat.Blows;
			Hitboxes.Get(target, out var box);
			Vector2 nearest;
			float reach = box.FlatDistance(_mod.EyeForTest.x, _mod.EyeForTest.y, out nearest);
			PossessionCombat.AttackForTest(_mod.Host);
			yield return new WaitForSecondsRealtime(0.4f);
			float num = (target.isAlive() ? ((float)target.getHealth()) : 0f);
			bool flag = PossessionCombat.LastBlowAt == target;
			float num2 = 0f;
			try
			{
				num2 = _mod.Host.getAttackRange();
			}
			catch
			{
			}
			Check(flag == expectHit, string.Format("{0}: {1:F2} tiles from the eye to its body (range {2:F2}), blows {3}->{4}, blow at {5}, health {6}->{7}", label, reach, num2, blows, PossessionCombat.Blows, (PossessionCombat.LastBlowAt == null) ? "nobody" : ((PossessionCombat.LastBlowAt == target) ? "it" : "someone else"), health, num));
			try
			{
				if (target.isAlive())
				{
					target.dieSimpleNone();
				}
			}
			catch
			{
			}
		}

		private IEnumerator Beast(string kind)
		{
			Actor beast = Spawn(kind, Ahead(2.5f, 0f));
			if (beast == null)
			{
				yield break;
			}
			Immortal(beast);
			yield return new WaitForSecondsRealtime(0.3f);
			bool leapt = Possess.JumpTo(beast);
			for (float t = 0f; t < 6f; t += 0.1f)
			{
				if (_mod.Host == beast)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.1f);
			}
			yield return new WaitForSecondsRealtime(1.5f);
			if (_mod.Host != beast)
			{
				Log.Error($"SHOT {kind}: could not take over (leap {leapt})");
				yield break;
			}
			_home = beast.current_position;
			FreezeAround();
			_mod.SetPitchDegrees(-12f);
			yield return Settle();
			Vector3 eyeForTest = _mod.EyeForTest;
			Vector2 current_position = beast.current_position;
			Log.Info($"SHOT {kind}: eye {(new Vector2(eyeForTest.x, eyeForTest.y) - current_position).magnitude:F2} tiles out from its middle, {eyeForTest.z - _mod.TerrainForTest.SampleHeight(eyeForTest.x, eyeForTest.y):F2} up");
			yield return Blow(kind + " at a sheep in front of its head", "sheep", 0.85f, 0f, expectHit: true);
			yield return Blow(kind + " at a sheep beside its head", "sheep", 0.85f, 70f, expectHit: false);
		}

		private IEnumerator Thrower(string kind)
		{
			Vector2 vector = ((!float.IsNaN(_lane)) ? _laneStart : Ahead(0f, 3f));
			if (!float.IsNaN(_lane) && _king != null && _king.isAlive())
			{
				Vector2 vector2 = new Vector2(Mathf.Sin(_lane), 0f - Mathf.Cos(_lane)) * 3f;
				_king.current_position = vector + vector2;
				_king.dirty_current_tile = true;
			}
			Actor me = Spawn(kind, vector);
			if (me == null)
			{
				yield break;
			}
			Immortal(me);
			yield return new WaitForSecondsRealtime(0.3f);
			Possess.JumpTo(me);
			for (float t = 0f; t < 6f; t += 0.1f)
			{
				if (_mod.Host == me)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.1f);
			}
			yield return new WaitForSecondsRealtime(1.5f);
			if (_mod.Host != me)
			{
				Log.Error("SHOT " + kind + ": could not take over");
				yield break;
			}
			_home = me.current_position;
			FreezeAround();
			if (!float.IsNaN(_lane))
			{
				_mod.SetYaw(_lane);
			}
			yield return Settle();
			Actor sheep = Spawn("sheep", Ahead(4f, 0f));
			yield return new WaitForSecondsRealtime(0.4f);
			for (int i = 0; i < 3; i++)
			{
				Hold();
				yield return null;
			}
			if (sheep == null)
			{
				yield break;
			}
			Face(sheep);
			yield return Settle();
			float health = sheep.getHealth();
			int launched = FirstPersonShots.Launched;
			int ended = Ended();
			_mod.PressAbilityForTest(down: true);
			for (int i = 0; i < 20; i++)
			{
				Hold();
				yield return null;
			}
			_mod.PressAbilityForTest(down: false);
			for (float t = 0f; t < 3f; t += 0.05f)
			{
				if (Ended() != ended)
				{
					break;
				}
				Hold();
				yield return new WaitForSecondsRealtime(0.05f);
			}
			float num = (sheep.isAlive() ? ((float)sheep.getHealth()) : 0f);
			Check(FirstPersonShots.LastVictim == sheep, $"{kind} ability: launched {launched}->{FirstPersonShots.Launched}, ended at {FirstPersonShots.LastWhat}, health {health}->{num}");
		}

		private static int Ended()
		{
			return FirstPersonShots.Struck + FirstPersonShots.Grounded + FirstPersonShots.Walled;
		}

		private IEnumerator Loose(string label, out ShotWatch watch)
		{
			watch = new ShotWatch
			{
				Launched = FirstPersonShots.Launched,
				Ended = Ended()
			};
			_mod.Bow.LooseForTest(_mod.Host);
			return Frames(2);
		}

		private static IEnumerator Frames(int n)
		{
			for (int i = 0; i < n; i++)
			{
				yield return null;
			}
		}

		private IEnumerator Settle()
		{
			for (int i = 0; i < 5; i++)
			{
				Hold();
				yield return null;
			}
		}

		private void Face(Actor target)
		{
			if (Hitboxes.Get(target, out var box))
			{
				Vector3 eyeForTest = _mod.EyeForTest;
				float num = box.Centre.X - eyeForTest.x;
				float num2 = box.Centre.Y - eyeForTest.y;
				float num3 = Mathf.Max(0.05f, Mathf.Sqrt(num * num + num2 * num2));
				_mod.SetYaw(Mathf.Atan2(num2, num));
				_mod.SetPitchDegrees(Mathf.Clamp(Mathf.Atan((box.Centre.Z - eyeForTest.z) / num3) * 57.29578f, -44f, 44f));
			}
		}

		private float ClearYaw(out int clear)
		{
			Vector3 eyeForTest = _mod.EyeForTest;
			TerrainCache terrainForTest = _mod.TerrainForTest;
			float result = _mod.ViewYaw;
			clear = 0;
			for (int i = 0; i < 32; i++)
			{
				float num = (float)i * MathF.PI * 2f / 32f;
				float num2 = Mathf.Cos(num);
				float num3 = Mathf.Sin(num);
				int num4 = 0;
				for (int j = 2; j <= 28; j++)
				{
					float x = eyeForTest.x + num2 * (float)j;
					float y = eyeForTest.y + num3 * (float)j;
					WorldTile worldTile = Tile(x, y);
					if (worldTile == null || worldTile.Type == null || worldTile.Type.liquid || worldTile.Type.block || (worldTile.building != null && AssetStyle.IsSolid(worldTile.building)))
					{
						break;
					}
					float num5 = terrainForTest.SampleHeight(x, y) + 0.4f;
					bool flag = true;
					for (float num6 = 0.5f; num6 < (float)j && flag; num6 += 0.5f)
					{
						if (terrainForTest.SampleHeight(eyeForTest.x + num2 * num6, eyeForTest.y + num3 * num6) > eyeForTest.z + (num5 - eyeForTest.z) * num6 / (float)j - 0.05f)
						{
							flag = false;
						}
					}
					if (!flag)
					{
						break;
					}
					num4 = j;
				}
				if (num4 > clear)
				{
					clear = num4;
					result = num;
				}
			}
			return result;
		}

		private WorldTile HillSpot()
		{
			Vector2 current_position = _king.current_position;
			TerrainCache terrainForTest = _mod.TerrainForTest;
			Vector3 eyeForTest = _mod.EyeForTest;
			float num = terrainForTest.SampleHeight(current_position.x, current_position.y);
			WorldTile result = null;
			float num2 = 0.8f;
			for (int i = 0; i < 64; i++)
			{
				float f = (float)i * 0.39f;
				float num3 = 6f + (float)(i % 6) * 2f;
				float x = current_position.x + Mathf.Cos(f) * num3;
				float y = current_position.y + Mathf.Sin(f) * num3;
				WorldTile worldTile = Tile(x, y);
				if (worldTile != null && worldTile.Type != null && !worldTile.Type.liquid && !worldTile.Type.block)
				{
					float num4 = Mathf.Abs(terrainForTest.SampleHeight(worldTile.posV3.x, worldTile.posV3.y) - num);
					if (!(num4 <= num2) && InSight(eyeForTest, worldTile.posV3.x, worldTile.posV3.y, 0.15f))
					{
						num2 = num4;
						result = worldTile;
					}
				}
			}
			return result;
		}

		private bool InSight(Vector3 eye, float x, float y, float tall)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			float num = terrainForTest.SampleHeight(x, y) + tall;
			float num2 = x - eye.x;
			float num3 = y - eye.y;
			float num4 = Mathf.Sqrt(num2 * num2 + num3 * num3);
			for (float num5 = 0.2f; num5 < num4; num5 += 0.2f)
			{
				if (terrainForTest.SampleHeight(eye.x + num2 * num5 / num4, eye.y + num3 * num5 / num4) > eye.z + (num - eye.z) * num5 / num4 - 0.12f)
				{
					return false;
				}
			}
			return true;
		}

		private Vector2 Ahead(float dist, float side)
		{
			Vector2 current_position = _mod.Host.current_position;
			float viewYaw = _mod.ViewYaw;
			float num = Mathf.Cos(viewYaw);
			float num2 = Mathf.Sin(viewYaw);
			return new Vector2(current_position.x + num * dist + num2 * side, current_position.y + num2 * dist - num * side);
		}

		private Actor Spawn(string id, Vector2 at)
		{
			WorldTile worldTile = Tile(at.x, at.y);
			if (worldTile == null)
			{
				return null;
			}
			try
			{
				Actor actor = World.world.units.spawnNewUnit(id, worldTile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
				if (actor == null)
				{
					return null;
				}
				_hold.Add(actor);
				actor.current_position = at;
				actor.dirty_current_tile = true;
				return actor;
			}
			catch (Exception ex)
			{
				Log.Error("SHOT spawn " + id + ": " + ex.Message);
				return null;
			}
		}

		private void FreezeAround()
		{
			Actor host = _mod.Host;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				try
				{
					if (simple != null && simple != host && simple.isAlive() && !_freeze.Contains(simple) && (simple.current_position - host.current_position).sqrMagnitude < 1600f)
					{
						_freeze.Add(simple);
					}
				}
				catch
				{
				}
			}
		}

		private void Home()
		{
			if (_homeSet && _mod.Host != null)
			{
				_mod.Host.current_position = _home;
				_mod.Host.dirty_current_tile = true;
			}
		}

		private void Hold()
		{
			for (int i = 0; i < 2; i++)
			{
				foreach (Actor item in (i == 0) ? _hold : _freeze)
				{
					try
					{
						if (item != null && item.isAlive() && item != _mod.Host)
						{
							item.cancelAllBeh();
							item.makeWait(5f);
							item.clearAttackTarget();
						}
					}
					catch
					{
					}
				}
			}
		}

		private static void Immortal(Actor a)
		{
			try
			{
				((BaseSimObject)a).addStatusEffect("invincible", 300f, pColorEffect: false);
			}
			catch
			{
			}
		}

		private static WorldTile Tile(float x, float y)
		{
			return World.world.GetTile((int)x, (int)y);
		}

		private static float Dist(Vec3 a, Vector3 b)
		{
			return Mathf.Sqrt((a.X - b.x) * (a.X - b.x) + (a.Y - b.y) * (a.Y - b.y) + (a.Z - b.z) * (a.Z - b.z));
		}

		private static float Flat(Vec3 a, Vector3 b)
		{
			return Mathf.Sqrt((a.X - b.x) * (a.X - b.x) + (a.Y - b.y) * (a.Y - b.y));
		}

		private static bool Equip(Actor unit, string id)
		{
			try
			{
				ActorEquipmentSlot slot = unit.equipment.getSlot(EquipmentType.Weapon);
				if (slot.getItem() != null)
				{
					slot.takeAwayItem();
				}
				EquipmentAsset equipmentAsset = AssetManager.items.get(id);
				if (equipmentAsset == null)
				{
					return false;
				}
				Item pItem = World.world.items.generateItem(equipmentAsset, unit.kingdom, unit.getName(), 1, unit, 0, pByPlayer: true);
				slot.setItem(pItem, unit);
				unit.setStatsDirty();
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("SHOT equip " + id + ": " + ex.Message);
				return false;
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
			Log.Info("SHOT DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private IEnumerator BowFeel()
		{
			if (!Equip(_king, "bow_wood"))
			{
				yield break;
			}
			for (int i = 0; i < 30; i++)
			{
				if (_mod.Bow.InHand)
				{
					break;
				}
				yield return null;
			}
			Check(_mod.Bow.InHand, "the bow is in your hands");
			_home = _king.current_position;
			_homeSet = true;
			int clear;
			float yaw = (_lane = ClearYaw(out clear));
			_laneStart = _home;
			Log.Info($"SHOT shooting along {yaw * 57.29578f:F0} degrees, clear for {clear} tiles");
			_mod.SetYaw(yaw);
			_mod.SetPitchDegrees(0f);
			yield return Settle();
			float far = Mathf.Max(8f, Mathf.Min(24f, (float)clear - 1f));
			Actor sheep = SpawnAimed(far);
			yield return new WaitForSecondsRealtime(0.4f);
			for (int i = 0; i < 4; i++)
			{
				Hold();
				yield return null;
			}
			yield return AimAt(sheep);
			CombatFeedback fb = _mod.Feedback;
			int credited = fb.Hits + fb.Kills;
			int launched = FirstPersonShots.Launched;
			int ended = Ended();
			float fovRest = _mod.View.Fov * 57.29578f;
			_mod.Bow.HeldForTest = true;
			for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
			{
				Hold();
				yield return null;
			}
			float num = _mod.View.Fov * 57.29578f;
			Check(_mod.Bow.State.State == BowState.Phase.Held && _mod.Bow.Steady > 0.95f, $"held at full draw: {_mod.Bow.State.State}, draw {_mod.Bow.State.Draw:F2}, steady {_mod.Bow.Steady:F2}, view {fovRest:F1} -> {num:F1} degrees");
			Vector3 eye = _mod.EyeForTest;
			float viewYaw = _mod.ViewYaw;
			_mod.Bow.HeldForTest = false;
			int launchFrame = -1;
			int maxStreaks = 0;
			int frame = 0;
			float maxTrails = 0f;
			for (; frame < 120; frame++)
			{
				if (Ended() != ended)
				{
					break;
				}
				Hold();
				yield return null;
				if (launchFrame < 0 && FirstPersonShots.Launched != launched)
				{
					launchFrame = frame;
					Vec3 lastFrom = FirstPersonShots.LastFrom;
					float num2 = Mathf.Cos(viewYaw);
					float num3 = Mathf.Sin(viewYaw);
					float num4 = (lastFrom.X - eye.x) * num2 + (lastFrom.Y - eye.y) * num3;
					float num5 = (lastFrom.X - eye.x) * num3 - (lastFrom.Y - eye.y) * num2;
					Log.Info($"SHOT launched {frame} frames after letting go: from {num4:F2} ahead, {num5:+0.00;-0.00} right, {lastFrom.Z - eye.z:+0.00;-0.00} up of the eye, {FirstPersonShots.LastSpeed:F1} tiles/s");
					Check(num4 > 0.2f && num4 < 1.2f && Mathf.Abs(num5) < 0.12f && Mathf.Abs(lastFrom.Z - eye.z) < 0.25f, "the arrow leaves from its point on the string (ahead, under the crosshair, at eye height)");
					Check(Mathf.Abs(FirstPersonShots.LastSpeed - 34f) < 0.6f, $"a full draw flies at 34 tiles/s: {FirstPersonShots.LastSpeed:F1}");
				}
				if (launchFrame >= 0)
				{
					int num6 = frame - launchFrame;
					maxStreaks = Mathf.Max(maxStreaks, FirstPersonShots.StreaksEmitted);
					maxTrails = Mathf.Max(maxTrails, FirstPersonShots.TrailCount);
					if (FirstPersonShots.FlyingForTest(out var at, out var ground) && (num6 <= 3 || num6 == 6 || num6 == 12))
					{
						Log.Info($"SHOT frame {num6} after launch: {Flat(at, eye):F2} tiles out, {at.Z - ground:F2} over the ground, {FirstPersonShots.StreaksEmitted} trail streaks");
					}
					if (num6 == 1 || num6 == 3 || num6 == 6 || num6 == 12)
					{
						_mod.SaveFrame($"bowfeel_far_{num6}.png");
					}
				}
			}
			for (int i = 0; i < 6; i++)
			{
				Hold();
				yield return null;
			}
			_mod.SaveFrame("bowfeel_far_end.png");
			Check(FirstPersonShots.LastVictim == sheep, $"far sheep ({far:F0} tiles) struck: ended at {FirstPersonShots.LastWhat}");
			Check(fb.Hits + fb.Kills > credited, $"the hit is yours however far off: hits+kills {credited} -> {fb.Hits + fb.Kills}, marker {fb.HitMarker:F2}");
			Check(maxStreaks > 0 && maxTrails > 0f, $"its trail was drawn in flight: up to {maxStreaks} streaks");
			for (int i = 0; i < 20; i++)
			{
				Hold();
				yield return null;
			}
			Check(FirstPersonShots.TrailCount == 0, $"the trail is gone after: {FirstPersonShots.TrailCount} left");
			KillHeld();
			yield return new WaitForSecondsRealtime(1.3f);
			Home();
			_mod.SetYaw(yaw);
			yield return Settle();
			sheep = SpawnAimed(Mathf.Min(12f, far));
			yield return new WaitForSecondsRealtime(0.4f);
			yield return AimAt(sheep);
			launched = FirstPersonShots.Launched;
			ended = Ended();
			_mod.Bow.HeldForTest = true;
			yield return null;
			_mod.Bow.HeldForTest = false;
			float waited;
			for (waited = 0f; waited < 1f; waited += Time.unscaledDeltaTime)
			{
				if (FirstPersonShots.Launched != launched)
				{
					break;
				}
				Hold();
				yield return null;
			}
			float lastSpeed = FirstPersonShots.LastSpeed;
			FirstPersonShots.LastPassed = null;
			Check(FirstPersonShots.Launched != launched && waited < 0.4f, $"a click shoots: launched after {waited:F2} s, draw {_mod.Bow.State.LastShotDraw:F2}");
			Check(lastSpeed > 19f && lastSpeed < 25f, $"a snap shot is slower: {lastSpeed:F1} tiles/s");
			for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime)
			{
				if (Ended() != ended)
				{
					break;
				}
				Hold();
				yield return null;
			}
			bool flag = FirstPersonShots.LastPassed == sheep;
			Check(FirstPersonShots.LastVictim == sheep || flag, "the snap shot strikes the sheep: ended at " + FirstPersonShots.LastWhat + (flag ? ", the sheep dodged it" : ""));
			KillHeld();
			yield return new WaitForSecondsRealtime(1.3f);
			launched = FirstPersonShots.Launched;
			_mod.Bow.HeldForTest = true;
			for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime)
			{
				Hold();
				yield return null;
			}
			_mod.Bow.HeldForTest = false;
			for (float t = 0f; t < 0.84999996f; t += Time.unscaledDeltaTime)
			{
				Hold();
				yield return null;
			}
			_mod.Bow.State.Press();
			float after;
			for (after = 0f; after < 1.5f; after += Time.unscaledDeltaTime)
			{
				if (FirstPersonShots.Launched >= launched + 2)
				{
					break;
				}
				Hold();
				yield return null;
			}
			Check(FirstPersonShots.Launched >= launched + 2, $"a click during the nock is kept: {FirstPersonShots.Launched - launched} shots, the second {after:F2} s after the click");
		}

		private Actor SpawnAimed(float dist)
		{
			Home();
			return Spawn("sheep", Ahead(dist, 0f));
		}

		private IEnumerator AimAt(Actor target)
		{
			if (target != null && Hitboxes.Get(target, out var box))
			{
				Vector3 eyeForTest = _mod.EyeForTest;
				Vec3 vec = box.Centre + new Vec3(0f, 0f, box.HalfUp * 0.45f);
				float num = vec.X - eyeForTest.x;
				float num2 = vec.Y - eyeForTest.y;
				float num3 = Mathf.Sqrt(num * num + num2 * num2);
				_mod.SetYaw(Mathf.Atan2(num2, num));
				_mod.SetPitchDegrees(Mathf.Atan((vec.Z - eyeForTest.z) / num3) * 57.29578f);
				yield return Settle();
			}
		}

		private void KillHeld()
		{
			foreach (Actor item in _hold)
			{
				try
				{
					if (item != null && item.isAlive() && item != _mod.Host)
					{
						item.dieSimpleNone();
					}
				}
				catch
				{
				}
			}
			_hold.Clear();
		}

		private IEnumerator BlowPace()
		{
			_mod.SetPitchDegrees(-10f);
			string[] array = new string[3] { "sword_iron", "hammer_stone", null };
			float reach;
			foreach (string id in array)
			{
				string name = id ?? "fists";
				if (id != null)
				{
					if (!Equip(_king, id))
					{
						continue;
					}
				}
				else
				{
					try
					{
						ActorEquipmentSlot slot = _king.equipment.getSlot(EquipmentType.Weapon);
						if (slot.getItem() != null)
						{
							slot.takeAwayItem();
						}
						_king.setStatsDirty();
					}
					catch
					{
					}
				}
				yield return new WaitForSecondsRealtime(0.8f);
				Home();
				yield return Settle();
				MeleeControl m = _mod.Melee;
				Check(m.Active, name + ": blows are timed (MeleeControl active)");
				if (!m.Active)
				{
					continue;
				}
				WeaponKind kind = ((id != null) ? ViewMeshes.KindOf(id) : WeaponKind.None);
				StrikeTiming tm = StrikeTiming.For(kind, melee: true, hands: true);
				float cooldown = 0f;
				try
				{
					cooldown = _king.getAttackCooldown();
				}
				catch
				{
				}
				float cadence = tm.Total;
				int expected = Mathf.FloorToInt((3f - tm.Hit) / cadence) + 1;
				float firstLanded = -1f;
				int hammered = 0;
				double stamp = _king._possession_attack_happened_frame;
				float next = 0f;
				for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime)
				{
					if (t >= next)
					{
						m.PressForTest();
						next += 0.1f;
					}
					Hold();
					Home();
					yield return null;
					if (id == "sword_iron" && Mathf.Abs(t - tm.StrikeStart) < 0.02f)
					{
						_mod.SaveFrame("blowpace_sword_top.png");
					}
					if (_king._possession_attack_happened_frame != stamp)
					{
						stamp = _king._possession_attack_happened_frame;
						if (hammered == 0)
						{
							firstLanded = t;
						}
						if (hammered == 0 && id == "sword_iron")
						{
							_mod.SaveFrame("blowpace_sword_lands.png");
						}
						hammered++;
					}
				}
				Check(hammered >= expected - 1 && hammered <= expected, $"{name}: hammering the button for 3 s struck {hammered} blows (pace {cadence:F2} s, game cooldown {cooldown:F2} s, at most {expected})");
				Check(firstLanded >= tm.Hit - 0.05f && firstLanded <= tm.Hit + 0.2f, $"{name}: the first blow landed {firstLanded:F2} s after the click (its wind-up: {tm.Hit:F2} s)");
				yield return new WaitForSecondsRealtime(cadence + 0.3f);
				int held = 0;
				stamp = _king._possession_attack_happened_frame;
				m.HeldForTest = true;
				for (next = 0f; next < 3f; next += Time.unscaledDeltaTime)
				{
					Hold();
					Home();
					yield return null;
					if (_king._possession_attack_happened_frame != stamp)
					{
						stamp = _king._possession_attack_happened_frame;
						held++;
					}
				}
				m.HeldForTest = false;
				if (kind == WeaponKind.Sword)
				{
					Check(held == 0, $"{name}: holding the button for 3 s struck {held} blows (a sword held charges, it doesn't swing on)");
				}
				else
				{
					Check(Mathf.Abs(held - hammered) <= 1, $"{name}: holding the button for 3 s struck {held} blows, hammering it {hammered}");
				}
				yield return new WaitForSecondsRealtime(cadence + 0.3f);
				Home();
				yield return Settle();
				Vector3 eyeForTest = _mod.EyeForTest;
				float viewYaw = _mod.ViewYaw;
				Actor sheep = Spawn("sheep", new Vector2(eyeForTest.x + Mathf.Cos(viewYaw) * 1f, eyeForTest.y + Mathf.Sin(viewYaw) * 1f));
				if (sheep == null)
				{
					continue;
				}
				yield return new WaitForSecondsRealtime(0.4f);
				for (int j = 0; j < 3; j++)
				{
					Hold();
					yield return null;
				}
				Face(sheep);
				yield return Settle();
				PossessionCombat.LastBlowAt = null;
				int blows = PossessionCombat.Blows;
				float range = 0f;
				float speed = 0f;
				try
				{
					range = _king.getAttackRange();
					speed = ((BaseSimObject)_king).stats["attack_speed"];
				}
				catch
				{
				}
				Hitboxes.Get(sheep, out var box);
				reach = box.FlatDistance(_mod.EyeForTest.x, _mod.EyeForTest.y, out var nearest);
				FirstPersonAim.Point(out var point, out var aimed, pastFriends: true);
				m.PressForTest();
				string atHit = null;
				for (next = 0f; next < tm.Hit + 0.4f; next += Time.unscaledDeltaTime)
				{
					Hold();
					yield return null;
					if (atHit == null && PossessionCombat.Blows != blows)
					{
						FirstPersonAim.Point(out point, out var creature, pastFriends: true);
						Hitboxes.Get(sheep, out var box2);
						atHit = string.Format("at the blow the crosshair was on {0}, {1:F2} tiles off", (creature == null) ? "nothing" : ((creature == sheep) ? "it" : creature.asset.id), box2.FlatDistance(_mod.EyeForTest.x, _mod.EyeForTest.y, out nearest));
					}
				}
				Check(PossessionCombat.LastBlowAt == sheep, string.Format("{0}: a click struck the sheep in front: blows {1}->{2}, at {3} ", name, blows, PossessionCombat.Blows, (PossessionCombat.LastBlowAt == null) ? "nobody" : ((PossessionCombat.LastBlowAt == sheep) ? "it" : "someone else")) + string.Format("({0:F2} tiles from the eye to its body, range {1:F2}, attack speed {2:F2}, crosshair on {3} at the click, {4})", reach, range, speed, (aimed == null) ? "nothing" : ((aimed == sheep) ? "it" : aimed.asset.id), atHit));
				try
				{
					if (sheep.isAlive())
					{
						sheep.dieSimpleNone();
					}
				}
				catch
				{
				}
				yield return new WaitForSecondsRealtime(cadence);
				aimed = null;
			}
			yield return new WaitForSecondsRealtime(1f);
			GuardControl guard = _mod.Guard;
			_mod.Melee.PressForTest();
			yield return null;
			yield return null;
			guard.HeldForTest = true;
			guard.PressedForTest = true;
			yield return null;
			bool mid = guard.State.Raised;
			yield return new WaitForSecondsRealtime(1.5f);
			bool raised = guard.State.Raised;
			guard.HeldForTest = false;
			Check(!mid && raised, $"the guard waits for a blow's strike: up in the blow {mid}, after it {raised}");
			yield return new WaitForSecondsRealtime(0.5f);
			if (!Equip(_king, "bow_wood"))
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(1f);
			Home();
			_mod.SetPitchDegrees(25f);
			yield return Settle();
			int i = FirstPersonShots.Launched;
			reach = 0f;
			for (float speed = 0f; speed < 4f; speed += Time.unscaledDeltaTime)
			{
				if (speed >= reach)
				{
					_mod.Bow.State.Press();
					reach += 0.1f;
				}
				Hold();
				Home();
				yield return null;
			}
			int num = FirstPersonShots.Launched - i;
			int num2 = Mathf.FloorToInt(2.9615386f) + 1;
			Check(num >= 2 && num <= num2, $"bow: hammering the button for 4 s loosed {num} arrows (at most {num2}, the reload {1.15f:F2} s)");
		}

		private void SgCheck(bool ok, string what)
		{
			Check(ok, "SG " + what);
		}

		private static void SgScreen(string name)
		{
			try
			{
				ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, name));
			}
			catch
			{
			}
		}

		private IEnumerator Shotgun()
		{
			SgCheck(Models.Get("held_shotgun") != null && Models.Get("shotgun_shell") != null, string.Format("models: held_shotgun {0}, shotgun_shell {1}", Models.Get("held_shotgun") != null, Models.Get("shotgun_shell") != null));
			if (!Equip(_king, "shotgun"))
			{
				Log.Error("SG no shotgun item");
				yield break;
			}
			SceneCollector.Trace = true;
			if (File.ReadAllText(Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest")).Contains("onlyothers"))
			{
				_home = _king.current_position;
				_homeSet = true;
				_lane = ClearYaw(out var _);
				_laneStart = _home;
				yield return new WaitForSecondsRealtime(1f);
				yield return SgOthers();
				yield break;
			}
			ShotgunControl gun = _mod.Shotgun;
			Hands hands = _mod.HandsForTest;
			for (int i = 0; i < 40; i++)
			{
				if (gun.InHand)
				{
					break;
				}
				yield return null;
			}
			SgCheck(gun.InHand && !_mod.BlasterForTest.InHand && !_mod.Bow.InHand, $"the shotgun is in your hands (shotgun {gun.InHand}, blaster {_mod.BlasterForTest.InHand}, bow {_mod.Bow.InHand})");
			yield return new WaitForSecondsRealtime(0.15f);
			SgCheck(gun.State.State == ShotgunState.Phase.Racking, $"racked as it is taken up: {gun.State.State}");
			yield return new WaitForSecondsRealtime(1f);
			SgCheck(hands.ShotgunShown && gun.State.State == ShotgunState.Phase.Ready && gun.State.Loaded == 6 && gun.State.Chambered, string.Format("ready: shown {0}, {1}, {2} shells{3}, muzzle from eye {4:F2},{5:F2},{6:F2}", hands.ShotgunShown, gun.State.State, gun.State.Loaded, gun.State.Chambered ? ", chambered" : "", hands.ShotgunMuzzleFromEye.X, hands.ShotgunMuzzleFromEye.Y, hands.ShotgunMuzzleFromEye.Z));
			_mod.SaveFrame("sg_ready.png");
			_home = _king.current_position;
			_homeSet = true;
			int clear2;
			float yaw = (_lane = ClearYaw(out clear2));
			_laneStart = _home;
			Log.Info($"SG shooting along {yaw * 57.29578f:F0} degrees, clear for {clear2} tiles");
			_mod.SetYaw(yaw);
			_mod.SetPitchDegrees(0f);
			yield return Settle();
			yield return SgShootSheep("sheep 5", Mathf.Min(5f, (float)clear2 - 1f), expectHit: true, film: true);
			yield return SgWaitReady();
			yield return SgShootSheep("sheep far", Mathf.Max(6f, Mathf.Min(16f, (float)clear2 - 1f)), expectHit: false, film: false);
			yield return SgWaitReady();
			Home();
			_mod.SetYaw(yaw);
			yield return Settle();
			Vector3 eye = _mod.EyeForTest;
			float fx = Mathf.Cos(yaw);
			float fy = Mathf.Sin(yaw);
			float dist = Mathf.Min(4.5f, (float)clear2 - 1f);
			float num = _mod.TerrainForTest.SampleHeight(eye.x + fx * dist, eye.y + fy * dist);
			_mod.SetPitchDegrees(Mathf.Atan((num - eye.z) / dist) * 57.29578f);
			yield return Settle();
			int particles = SceneCollector.BlastParticleCount;
			yield return SgFire();
			yield return new WaitForSecondsRealtime(0.5f);
			List<Vec3> list = new List<Vec3>(FirstPersonShots.PelletEnds);
			float num2 = 0f;
			List<float> list2 = new List<float>();
			foreach (Vec3 item in list)
			{
				float num3 = item.X - eye.x;
				float num4 = item.Y - eye.y;
				list2.Add(num3 * fx + num4 * fy);
				num2 = Mathf.Max(num2, Mathf.Abs(num3 * fy - num4 * fx));
			}
			list2.Sort();
			float num5 = ((list2.Count > 0) ? list2[list2.Count / 2] : 0f);
			float num6 = dist * 0.058f;
			SgCheck(list.Count >= 8 && num2 > num6 * 0.4f && num2 < num6 * 2.2f + 0.15f && Mathf.Abs(num5 - dist) < dist * 0.5f, $"ground at {dist:F1}: {list.Count} pellets ended, the widest {num2:F2} tiles off the line (the pattern's {num6:F2}), median {num5:F1} tiles out (nearest {((list2.Count > 0) ? list2[0] : 0f):F1}); dust particles {particles}->{SceneCollector.BlastParticleCount}");
			_mod.SaveFrame("sg_ground.png");
			yield return SgWaitReady();
			_mod.SetPitchDegrees(-3f);
			int before = gun.ShotsHeard;
			_mod.Melee.HeldForTest = true;
			float held = 2.6f;
			for (float t = 0f; t < held; t += Time.unscaledDeltaTime)
			{
				Hold();
				yield return null;
			}
			_mod.Melee.HeldForTest = false;
			int num7 = gun.ShotsHeard - before;
			int num8 = Mathf.CeilToInt(held / 0.82f) + 1;
			SgCheck(num7 >= 2 && num7 <= num8, $"held {held:F1} s: {num7} shots (at most {num8} at {0.82f:F2} s a shot), {gun.State.Loaded} left");
			yield return SgWaitReady();
			while (gun.State.Loaded > 0)
			{
				yield return SgFire();
				yield return SgWaitReady();
				if (gun.State.Reloading)
				{
					break;
				}
			}
			int ins = gun.ShellsIn;
			int dry = gun.DryClicks;
			if (!gun.State.Reloading)
			{
				yield return SgFire();
			}
			for (float t = 0f; t < 1f; t += 0.05f)
			{
				if (gun.State.Reloading)
				{
					break;
				}
				Hold();
				yield return new WaitForSecondsRealtime(0.05f);
			}
			SgCheck(gun.State.Reloading, $"empty: reloading by itself ({gun.State.State}, {gun.State.Loaded} shells)");
			for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime)
			{
				if (gun.State.State == ShotgunState.Phase.Loading && gun.State.Time > 0.2f && gun.State.Time < 0.29f)
				{
					break;
				}
				Hold();
				yield return null;
			}
			_mod.SaveFrame("sg_reload.png");
			SgScreen("sg_screen_reload.png");
			for (float t = 0f; t < 6f; t += 0.05f)
			{
				if (!gun.State.Reloading)
				{
					break;
				}
				Hold();
				yield return new WaitForSecondsRealtime(0.05f);
			}
			SgCheck(gun.State.Loaded == 6 && gun.State.Chambered && gun.State.Inserted >= 1 && !gun.State.Reloading, string.Format("reloaded: {0} shells{1}, {2} pushed in by this reload ({3} since the check began), dry clicks {4}->{5}", gun.State.Loaded, gun.State.Chambered ? ", chambered" : "", gun.State.Inserted, gun.ShellsIn - ins, dry, gun.DryClicks));
			yield return SgFire();
			yield return SgWaitReady();
			yield return SgFire();
			yield return SgWaitReady();
			gun.ReloadForTest();
			yield return new WaitForSecondsRealtime(0.5f);
			int shots = gun.ShotsHeard;
			int loaded = gun.State.Loaded;
			yield return SgFire();
			for (float t = 0f; t < 2.5f; t += 0.05f)
			{
				if (gun.ShotsHeard != shots)
				{
					break;
				}
				Hold();
				yield return new WaitForSecondsRealtime(0.05f);
			}
			SgCheck(gun.ShotsHeard == shots + 1 && !gun.State.Reloading, $"reload stopped to fire: shots {shots}->{gun.ShotsHeard}, shells {loaded} when stopped, {gun.State.State} now");
			yield return SgOthers();
		}

		private IEnumerator SgWaitReady(float most = 2f)
		{
			ShotgunControl gun = _mod.Shotgun;
			for (float t = 0f; t < most; t += 0.05f)
			{
				if (gun.State.State == ShotgunState.Phase.Ready && !_mod.Melee.State.Swinging)
				{
					break;
				}
				Hold();
				yield return new WaitForSecondsRealtime(0.05f);
			}
			yield return Settle();
		}

		private IEnumerator SgFire()
		{
			_mod.Melee.PressForTest();
			_mod.Shotgun.ClickForTest();
			for (int i = 0; i < 4; i++)
			{
				Hold();
				yield return null;
			}
		}

		private IEnumerator SgShootSheep(string label, float dist, bool expectHit, bool film)
		{
			Home();
			_mod.SetYaw(_lane);
			yield return Settle();
			Actor sheep = Spawn("sheep", Ahead(dist, 0f));
			if (sheep == null)
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(0.4f);
			for (int i = 0; i < 4; i++)
			{
				Hold();
				yield return null;
			}
			Face(sheep);
			yield return Settle();
			ShotgunControl gun = _mod.Shotgun;
			_ = _mod.HandsForTest;
			float health = sheep.getHealth();
			int pellets = FirstPersonShots.PelletsLaunched;
			int struck = FirstPersonShots.Struck;
			int shots = gun.ShotsHeard;
			int pumps = gun.PumpsHeard;
			int ejected = gun.Ejections;
			int casings = SceneCollector.CasingCount;
			int loaded = gun.State.Loaded;
			int hits = _mod.Feedback.Hits;
			_mod.Melee.PressForTest();
			_mod.Shotgun.ClickForTest();
			for (int i = 0; i < 30; i++)
			{
				if (gun.ShotsHeard != shots)
				{
					break;
				}
				Hold();
				yield return null;
			}
			if (film)
			{
				_mod.SaveFrame("sg_" + label.Replace(' ', '_') + "_flash.png");
				SgScreen("sg_screen_shot.png");
				yield return null;
				_mod.SaveFrame("sg_" + label.Replace(' ', '_') + "_tracers.png");
			}
			yield return new WaitForSecondsRealtime(0.12f);
			int tracers = FirstPersonShots.TracerCount;
			if (film)
			{
				_mod.SaveFrame("sg_" + label.Replace(' ', '_') + "_hits.png");
			}
			yield return new WaitForSecondsRealtime(0.2f);
			if (film)
			{
				_mod.SaveFrame("sg_" + label.Replace(' ', '_') + "_pump.png");
				SgScreen("sg_screen_pump.png");
			}
			int texts = 0;
			foreach (FloatingText text in _mod.Feedback.Texts)
			{
				if (text.Victim == sheep)
				{
					texts++;
				}
			}
			yield return new WaitForSecondsRealtime(0.8f);
			int num = FirstPersonShots.PelletsLaunched - pellets;
			int num2 = FirstPersonShots.Struck - struck;
			float num3 = (sheep.isAlive() ? ((float)sheep.getHealth()) : 0f);
			SgCheck(gun.ShotsHeard == shots + 1 && num >= 10, $"{label}: a shot, {num} pellets loosed from the muzzle (shells {loaded}->{gun.State.Loaded})");
			SgCheck((!expectHit) ? (num2 < num) : (num2 >= num / 2 && num3 < health), $"{label}: {num2} of {num} pellets in it at {dist:F1} tiles, health {health}->{num3}, hits heard {hits}->{_mod.Feedback.Hits}, damage numbers on it {texts}");
			if (expectHit)
			{
				SgCheck(texts <= 1 && _mod.Feedback.Hits - hits <= 2, $"{label}: one damage number ({texts}) and one hit heard ({_mod.Feedback.Hits - hits}) for the volley");
			}
			SgCheck(gun.PumpsHeard == pumps + 1 && gun.Ejections == ejected + 1 && gun.State.Chambered, $"{label}: pumped ({pumps}->{gun.PumpsHeard}), shell out ({ejected}->{gun.Ejections}), chambered {gun.State.Chambered}, tracers drawn mid-flight {tracers}");
			SgCheck(SceneCollector.CasingCount > casings || casings >= 20, $"{label}: spent shells lying about {casings}->{SceneCollector.CasingCount}");
			try
			{
				if (sheep.isAlive())
				{
					sheep.dieSimpleNone();
				}
			}
			catch
			{
			}
		}

		private IEnumerator SgOthers()
		{
			Home();
			_mod.SetYaw(_lane);
			_mod.SetPitchDegrees(-4f);
			yield return Settle();
			Actor shooter = Spawn("human", Ahead(4f, 3f));
			if (shooter == null)
			{
				Log.Info("SG no one to hold the other shotgun (skipped)");
				yield break;
			}
			Immortal(shooter);
			if (!Equip(shooter, "shotgun"))
			{
				yield break;
			}
			List<Actor> bandits = new List<Actor>();
			Actor actor = Spawn("sheep", Ahead(10f, 3f));
			if (actor != null)
			{
				Immortal(actor);
				bandits.Add(actor);
			}
			float nextShot = 0.5f;
			yield return new WaitForSecondsRealtime(0.4f);
			int volleys = SceneCollector.OthersVolleys;
			int casings = SceneCollector.ShellsEjected;
			int drawn = 0;
			int pellets0 = SceneCollector.OthersPelletsTotal;
			bool gunDrawn = false;
			int num;
			for (float t = 0f; t < 9f; t += 0.1f)
			{
				if (SceneCollector.OthersVolleys >= volleys + 2)
				{
					break;
				}
				try
				{
					if (bandits.Count > 0 && !shooter.has_attack_target)
					{
						shooter.startFightingWith(bandits[0]);
					}
					if (bandits.Count > 0 && t >= nextShot)
					{
						Vector2 current_position = bandits[0].current_position;
						shooter.tryToAttack((BaseSimObject)bandits[0], pDoChecks: false, (Action)null, new Vector3(current_position.x, current_position.y, 0f), (Kingdom)null, bandits[0].current_tile, 0f);
						nextShot = t + 1.4f;
					}
				}
				catch (Exception ex)
				{
					Log.Error("SG others: " + ex.Message);
				}
				Vector3 eyeForTest = _mod.EyeForTest;
				Vector2 current_position2 = shooter.current_position;
				_mod.SetYaw(Mathf.Atan2(current_position2.y - eyeForTest.y, current_position2.x - eyeForTest.x) + 0.25f);
				Home();
				if (People.GunMuzzle(shooter, out var _, out var _, out var _))
				{
					gunDrawn = true;
					drawn++;
				}
				num = SceneCollector.OthersPelletsTotal - pellets0;
				if (SceneCollector.OthersVolleys > volleys && num > 0 && drawn % 5 == 1)
				{
					_mod.SaveFrame($"sg_others_{SceneCollector.OthersVolleys - volleys}.png");
				}
				yield return new WaitForSecondsRealtime(0.1f);
			}
			yield return new WaitForSecondsRealtime(0.8f);
			num = SceneCollector.OthersPelletsTotal - pellets0;
			SgCheck(gunDrawn && SceneCollector.OthersVolleys > volleys && num > 0 && SceneCollector.ShellsEjected > casings, $"someone else's shotgun: drawn {gunDrawn}, volleys seen {volleys}->{SceneCollector.OthersVolleys}, their pellets drawn as tracers {num} times, shells thrown out {casings}->{SceneCollector.ShellsEjected}");
			foreach (Actor item in bandits)
			{
				try
				{
					if (item.isAlive())
					{
						item.dieSimpleNone();
					}
				}
				catch
				{
				}
			}
			try
			{
				if (shooter.isAlive())
				{
					shooter.dieSimpleNone();
				}
			}
			catch
			{
			}
		}
	}
}
