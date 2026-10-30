using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class GuardTest : CivilTest
	{
		private Actor _host;

		private Vector2 _home;

		private float _yaw;

		private int _savedTiming;

		private bool _savedWindups;

		protected override string Prefix => "GUARD";

		protected override IEnumerator Body()
		{
			WorldTile worldTile = CivilTest.Stage();
			if (!Check("open land", worldTile != null))
			{
				yield break;
			}
			CivilTest.ClearBox(worldTile.x - 14, worldTile.y - 14, worldTile.x + 14, worldTile.y + 14);
			_host = CivilTest.SpawnNear("human", new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f));
			if (Check("a villager", _host != null))
			{
				yield return Possess(_host);
				if (Check("first person in them", InControl(_host)) && Check("a sword", Equip(_host, "sword_iron")))
				{
					_home = _host.current_position;
					_yaw = Mod.ViewYaw;
					Mod.Settings.HeadBob = 0f;
					Mod.SetPitchDegrees(-6f);
					_savedTiming = Mod.Settings.ParryTiming;
					_savedWindups = Mod.Settings.EnemyWindups;
					Mod.Settings.ParryTiming = 0;
					Mod.Settings.EnemyWindups = true;
					yield return CivilTest.Wait(0.5f);
					yield return Part("wind-ups", WindUp());
					yield return Part("block", Block());
					yield return Part("parry and riposte", Parry());
					yield return Part("from behind", Behind());
					yield return Part("guard broken", BrokenGuard());
					Restore();
				}
			}
		}

		private void OnDestroy()
		{
			Restore();
		}

		private void Restore()
		{
			if (!(Mod == null))
			{
				Mod.Settings.ParryTiming = _savedTiming;
				Mod.Settings.EnemyWindups = _savedWindups;
				Mod.Guard.HeldForTest = false;
			}
		}

		private IEnumerator WindUp()
		{
			CombatFeedback fb = Mod.Feedback;
			Actor bandit = Foe(1f);
			if (!Check("a bandit with a sword, after you", bandit != null))
			{
				yield break;
			}
			try
			{
				bandit.attack_timer = 1f;
			}
			catch
			{
			}
			int begun = WindUps.Begun;
			int heard = Mod.Audio.WindUpsPlayed;
			int hurts = fb.Hurts;
			float windAt = -1f;
			float hurtAt = -1f;
			bool shot = false;
			for (float t = 0f; t < 15f; t += Time.unscaledDeltaTime)
			{
				if (!(hurtAt < 0f))
				{
					break;
				}
				Keep(bandit);
				yield return null;
				if (windAt < 0f && WindUps.Winding(bandit))
				{
					windAt = t;
				}
				if (windAt >= 0f && !shot && WindUps.Left(bandit) < 0.15f)
				{
					shot = true;
					yield return Capture("windup");
				}
				if (fb.Hurts > hurts)
				{
					hurtAt = t;
				}
			}
			Check("their blow is wound up first", windAt >= 0f && WindUps.Begun > begun, $"wind-ups {begun}->{WindUps.Begun}, wound up at {windAt:F2} s; {Diag(bandit)}");
			Check("...and lands after it", hurtAt > 0f && windAt >= 0f && hurtAt - windAt >= 0.15f, $"wound up {windAt:F2} s, struck {hurtAt:F2} s");
			Check("...with a sound of its own", Mod.Audio.WindUpsPlayed > heard, $"heard {heard}->{Mod.Audio.WindUpsPlayed}");
			CivilTest.Kill(bandit);
			yield return CivilTest.Wait(0.6f);
		}

		private IEnumerator Block()
		{
			CombatFeedback fb = Mod.Feedback;
			GuardControl guard = Mod.Guard;
			Actor bandit = Foe(1f);
			if (!Check("a bandit with a sword, after you", bandit != null))
			{
				yield break;
			}
			Heal();
			guard.HeldForTest = true;
			guard.PressedForTest = true;
			yield return CivilTest.Wait(0.5f);
			Check("the guard is up", guard.State.Raised && guard.Raise > 0.9f && guard.State.Blocking, $"{guard.State.Phase}, raise {guard.Raise:F2}");
			Check("...you walk slower behind it", PossessionHooks.GuardSlow > 0.9f, PossessionHooks.GuardSlow.ToString("F2"));
			int blocked = GuardControl.Blocked;
			int serial = Blocks.Newest;
			int stamina = Stamina();
			float before = 0f;
			float after = 0f;
			BlockEvent e = null;
			for (float t = 0f; t < 15f; t += Time.unscaledDeltaTime)
			{
				if (e != null)
				{
					break;
				}
				Keep(bandit);
				before = Health(_host);
				yield return null;
				e = Find(serial, (BlockEvent x) => x.OnYou && x.Attacker == bandit);
				after = Health(_host);
			}
			if (!Check("their blow stops on your guard", e != null && GuardControl.Blocked > blocked, $"blocked {blocked}->{GuardControl.Blocked}"))
			{
				guard.HeldForTest = false;
				CivilTest.Kill(bandit);
				yield break;
			}
			yield return null;
			yield return null;
			Check("...seen as BLOCKED", e.Kind == Stop.Blocked && fb.GuardWord == "BLOCKED", $"{e.Kind}, word {fb.GuardWord}");
			Check("...a little of it gets through", GuardControl.LastChip >= 0 && before - after <= (float)GuardControl.LastChip + 1f, $"health {before}->{after}, chip {GuardControl.LastChip}");
			Check("...and it costs stamina", Stamina() < stamina || stamina <= 0, $"stamina {stamina}->{Stamina()}, cost {GuardControl.LastCost}");
			yield return CivilTest.Wait(0.15f);
			yield return Capture("block");
			guard.HeldForTest = false;
			yield return CivilTest.Wait(0.4f);
			Check("let go, the guard comes down", !guard.State.Raised && PossessionHooks.GuardSlow < 0.05f, $"{guard.State.Phase}, slow {PossessionHooks.GuardSlow:F2}");
			CivilTest.Kill(bandit);
			yield return CivilTest.Wait(0.6f);
		}

		private IEnumerator Parry()
		{
			CombatFeedback fb = Mod.Feedback;
			GuardControl guard = Mod.Guard;
			Actor bandit = Foe(1f);
			if (!Check("a bandit with a sword, after you", bandit != null))
			{
				yield break;
			}
			Heal();
			guard.HeldForTest = false;
			yield return CivilTest.Wait(0.4f);
			int parried = GuardControl.Parried;
			int serial = Blocks.Newest;
			int hurts = fb.Hurts;
			BlockEvent e = null;
			bool pressed = false;
			try
			{
				bandit.attack_timer = 1f;
			}
			catch
			{
			}
			for (float t = 0f; t < 20f; t += Time.unscaledDeltaTime)
			{
				if (e != null)
				{
					break;
				}
				Keep(bandit);
				float num = WindUps.Left(bandit);
				if (!pressed && num >= 0f && num < 0.15f)
				{
					pressed = true;
					hurts = fb.Hurts;
					guard.HeldForTest = true;
					guard.PressedForTest = true;
				}
				yield return null;
				e = Find(serial, (BlockEvent x) => x.OnYou && x.Attacker == bandit);
				if (e != null && e.Kind != Stop.Parried)
				{
					serial = e.Serial;
					e = null;
					pressed = false;
					guard.HeldForTest = false;
					yield return CivilTest.Wait(0.7f);
				}
				if (pressed && !guard.State.Raised && e == null)
				{
					pressed = false;
					guard.HeldForTest = false;
				}
			}
			if (!Check("raised just as it came down, the blow is parried", e != null && GuardControl.Parried > parried, $"parried {parried}->{GuardControl.Parried}; {Diag(bandit)}"))
			{
				guard.HeldForTest = false;
				CivilTest.Kill(bandit);
				yield break;
			}
			yield return CivilTest.Wait(0.1f);
			Check("...PARRIED under the crosshair", fb.GuardWord == "PARRIED", fb.GuardWord);
			Check("...no wound", fb.Hurts == hurts, $"hurts {hurts}->{fb.Hurts}");
			bool ok = false;
			try
			{
				ok = ((BaseSimObject)bandit).hasStatus("stunned");
			}
			catch
			{
			}
			Check("...them stunned", ok);
			Check("...and a riposte open", guard.State.Riposte);
			yield return Capture("parry");
			guard.HeldForTest = false;
			int ripostes = GuardControl.Ripostes;
			Face(bandit);
			Mod.Melee.PressForTest();
			for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime)
			{
				if (GuardControl.Ripostes != ripostes)
				{
					break;
				}
				Keep(bandit);
				Face(bandit);
				yield return null;
			}
			Check("your next blow is a critical", GuardControl.Ripostes > ripostes, $"ripostes {ripostes}->{GuardControl.Ripostes}, blow at {Who(PossessionCombat.LastBlowAt)}");
			Check("...and the fight stays theirs: they swung first", CombatFeedback.TheyStarted(bandit, Mod.Host) && CombatFeedback.StruckFirst(bandit), $"they started {CombatFeedback.TheyStarted(bandit, Mod.Host)}, struck first {CombatFeedback.StruckFirst(bandit)}");
			CivilTest.Kill(bandit);
			yield return CivilTest.Wait(0.6f);
		}

		private IEnumerator Behind()
		{
			CombatFeedback fb = Mod.Feedback;
			GuardControl guard = Mod.Guard;
			Actor bandit = Foe(1f);
			if (!Check("a bandit with a sword, after you", bandit != null))
			{
				yield break;
			}
			Heal();
			guard.HeldForTest = true;
			guard.PressedForTest = true;
			int blocked = GuardControl.Blocked;
			int hurts = fb.Hurts;
			for (float t = 0f; t < 15f; t += Time.unscaledDeltaTime)
			{
				if (fb.Hurts != hurts)
				{
					break;
				}
				Keep(bandit, face: false);
				Mod.SetYaw(Mathf.Atan2(_home.y - bandit.current_position.y, _home.x - bandit.current_position.x));
				yield return null;
			}
			Check("a blow from behind goes round the guard", fb.Hurts > hurts && GuardControl.Blocked == blocked, $"hurts {hurts}->{fb.Hurts}, blocked {blocked}->{GuardControl.Blocked}");
			guard.HeldForTest = false;
			CivilTest.Kill(bandit);
			yield return CivilTest.Wait(0.6f);
		}

		private IEnumerator BrokenGuard()
		{
			CombatFeedback fb = Mod.Feedback;
			GuardControl guard = Mod.Guard;
			Actor bandit = Foe(1f);
			if (!Check("a bandit with a sword, after you", bandit != null))
			{
				yield break;
			}
			Heal();
			guard.HeldForTest = true;
			guard.PressedForTest = true;
			yield return CivilTest.Wait(0.5f);
			int broken = GuardControl.Broken;
			int hurts = fb.Hurts;
			for (float t = 0f; t < 15f; t += Time.unscaledDeltaTime)
			{
				if (GuardControl.Broken != broken)
				{
					break;
				}
				Keep(bandit);
				try
				{
					_host.setStamina(0);
				}
				catch
				{
				}
				yield return null;
			}
			if (!Check("with no stamina left, a blow breaks the guard", GuardControl.Broken > broken, $"broken {broken}->{GuardControl.Broken}, stamina {Stamina()}, hurts {hurts}->{fb.Hurts}; {Diag(bandit)}"))
			{
				guard.HeldForTest = false;
				CivilTest.Kill(bandit);
				yield break;
			}
			Check("...you stagger", guard.State.Staggered, guard.State.Phase.ToString());
			yield return CivilTest.Wait(0.1f);
			Check("...the blow lands", fb.Hurts > hurts, $"hurts {hurts}->{fb.Hurts}");
			Check("...GUARD BROKEN under the crosshair", fb.GuardWord == "GUARD BROKEN" && fb.GuardWordBroken, fb.GuardWord);
			yield return Capture("broken");
			int blows = PossessionCombat.Blows;
			Mod.Melee.PressForTest();
			yield return CivilTest.Wait(0.25f);
			Check("...and can't strike for a moment", PossessionCombat.Blows == blows && guard.State.Staggered, $"blows {blows}->{PossessionCombat.Blows}");
			guard.HeldForTest = false;
			CivilTest.Kill(bandit);
			yield return CivilTest.Wait(1f);
			Check("steady again, the guard comes back", !guard.State.Staggered, guard.State.Phase.ToString());
		}

		private Actor Foe(float dist)
		{
			_host.current_position = _home;
			Mod.SetYaw(_yaw);
			Vector2 at = _home + new Vector2(Mathf.Cos(_yaw), Mathf.Sin(_yaw)) * dist;
			Actor actor = CivilTest.SpawnNear("bandit", at);
			if (actor == null)
			{
				return null;
			}
			if (!Equip(actor, "sword_iron"))
			{
				CivilTest.Kill(actor);
				return null;
			}
			try
			{
				actor.restoreHealth(actor.getMaxHealth());
				((BaseSimObject)actor).addStatusEffect("invincible", 120f, pColorEffect: false);
				string[] array = new string[4] { "block", "dodge", "deflect_projectile", "slow" };
				foreach (string pTraitID in array)
				{
					if (actor.hasTrait(pTraitID))
					{
						actor.removeTrait(pTraitID);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("GUARD foe: " + ex.Message);
			}
			return actor;
		}

		private void Keep(Actor bandit, bool face = true)
		{
			if (_host == null || !_host.isAlive())
			{
				return;
			}
			_host.current_position = _home;
			_host.dirty_current_tile = true;
			try
			{
				if (_host.getHealth() < _host.getMaxHealth() / 2)
				{
					Heal();
				}
				if (bandit != null && bandit.isAlive() && (!bandit.has_attack_target || bandit.attack_target != _host))
				{
					bandit.startFightingWith(_host);
				}
			}
			catch
			{
			}
			if (face)
			{
				Face(bandit);
			}
		}

		private void Heal()
		{
			try
			{
				_host.restoreHealth(_host.getMaxHealth());
				_host.setStamina(Mathf.RoundToInt(((BaseSimObject)_host).stats["stamina"]));
			}
			catch
			{
			}
		}

		private int Stamina()
		{
			try
			{
				return _host.getStamina();
			}
			catch
			{
				return -1;
			}
		}

		private void Face(Actor target)
		{
			if (target != null && target.isAlive())
			{
				Vector2 vector = target.current_position - _host.current_position;
				if (vector.sqrMagnitude > 0.0001f)
				{
					Mod.SetYaw(Mathf.Atan2(vector.y, vector.x));
				}
			}
		}

		private static BlockEvent Find(int after, Predicate<BlockEvent> match)
		{
			foreach (BlockEvent @event in Blocks.Events)
			{
				if (@event.Serial > after && match(@event))
				{
					return @event;
				}
			}
			return null;
		}

		private static float Health(Actor a)
		{
			try
			{
				return (a != null && a.isAlive()) ? ((float)a.getHealth()) : 0f;
			}
			catch
			{
				return 0f;
			}
		}

		private static string Who(Actor a)
		{
			if (a != null)
			{
				return WorldBoxMod.Describe(a);
			}
			return "nobody";
		}

		private string Diag(Actor a)
		{
			try
			{
				if (a == null || !a.isAlive())
				{
					return "the bandit is gone";
				}
				return $"bandit {(a.current_position - _host.current_position).magnitude:F2} tiles off, range {a.getAttackRange():F2}, in reach {a.isInAttackRange((BaseSimObject)_host)}, " + string.Format("timer {0:F2}, after you {1}, winding {2}, held {3}, stunned {4}", a.attack_timer, a.has_attack_target && a.attack_target == _host, WindUps.Winding(a), WindUps.Held, ((BaseSimObject)a).hasStatus("stunned"));
			}
			catch (Exception ex)
			{
				return "diag: " + ex.Message;
			}
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
				Log.Error("GUARD equip " + id + ": " + ex.Message);
				return false;
			}
		}

		private IEnumerator Capture(string name)
		{
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "guard_" + name + "_hud.png"));
			for (float t = 0f; t < 4f; t += Time.unscaledDeltaTime)
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
