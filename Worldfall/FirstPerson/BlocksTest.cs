using System;
using System.Collections;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class BlocksTest : CivilTest
	{
		private Actor _host;

		private Vector2 _home;

		private float _yaw;

		private float _deflectChance = -1f;

		private float _dodgeChance = -1f;

		private float _blockChance = -1f;

		protected override string Prefix => "BLOCKS";

		protected override IEnumerator Body()
		{
			WorldTile worldTile = CivilTest.Stage();
			if (!Check("open land", worldTile != null))
			{
				yield break;
			}
			CivilTest.ClearBox(worldTile.x - 14, worldTile.y - 14, worldTile.x + 14, worldTile.y + 14);
			_host = CivilTest.SpawnNear("human", new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f));
			if (!Check("a villager", _host != null))
			{
				yield break;
			}
			yield return Possess(_host);
			if (Check("first person in them", InControl(_host)) && Check("a sword", Equip(_host, "sword_iron")))
			{
				_home = _host.current_position;
				_yaw = Mod.ViewYaw;
				Mod.Settings.HeadBob = 0f;
				Mod.SetPitchDegrees(-6f);
				try
				{
					_deflectChance = SetChance("combat_deflect_projectile", 1f);
					_dodgeChance = SetChance("combat_dodge", 1f);
					_blockChance = SetChance("combat_block", 1f);
				}
				catch (Exception ex)
				{
					Log.Error("BLOCKS chances: " + ex.Message);
				}
				yield return CivilTest.Wait(0.5f);
				yield return Part("your blow turned aside", YourBlowBlocked());
				yield return Part("your blow dodged", YourBlowDodged());
				yield return Part("their blows parried by yours", Parried());
				yield return Part("arrows blocked", ArrowsBlocked());
				yield return Part("two others", Others());
				Restore();
			}
		}

		private void OnDestroy()
		{
			Restore();
		}

		private void Restore()
		{
			if (_deflectChance >= 0f)
			{
				SetChance("combat_deflect_projectile", _deflectChance);
			}
			if (_dodgeChance >= 0f)
			{
				SetChance("combat_dodge", _dodgeChance);
			}
			if (_blockChance >= 0f)
			{
				SetChance("combat_block", _blockChance);
			}
			_deflectChance = (_dodgeChance = (_blockChance = -1f));
		}

		private static float SetChance(string id, float chance)
		{
			CombatActionAsset combatActionAsset = AssetManager.combat_action_library.get(id);
			if (combatActionAsset == null)
			{
				return -1f;
			}
			float chance2 = combatActionAsset.chance;
			combatActionAsset.chance = chance;
			return chance2;
		}

		private IEnumerator YourBlowBlocked()
		{
			CombatFeedback fb = Mod.Feedback;
			Actor bandit = Foe("sword_iron", 1.05f, "deflect_projectile");
			if (!Check("a bandit with a sword before you, who turns blows aside", bandit != null))
			{
				yield break;
			}
			yield return Settle(bandit);
			float health = Health(bandit);
			int stopped = Blocks.YourBlowsStopped;
			int drawn = SceneCollector.StopsDrawn;
			int heard = Mod.Audio.StopsPlayed;
			int hits = fb.Hits;
			int serial = Blocks.Newest;
			Mod.Melee.PressForTest();
			BlockEvent e = null;
			float marker = 0f;
			float stoppedAt = 0f;
			float flash = 0f;
			float hit = 0f;
			float found = -1f;
			for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
			{
				Stay();
				Hold(bandit);
				yield return null;
				e = e ?? Find(serial, (BlockEvent x) => x.YourBlow && x.Defender == bandit);
				marker = Mathf.Max(marker, fb.BlockMarker);
				stoppedAt = Mathf.Max(stoppedAt, fb.StoppedAt);
				hit = Mathf.Max(hit, fb.HitMarker);
				flash = Mathf.Max(flash, (float)(Blocks.FlashOf(bandit) >> 24) / 255f);
				if (e != null && found < 0f)
				{
					found = t;
				}
				if (found >= 0f && t - found > 0.1f && t - found - Time.unscaledDeltaTime <= 0.1f)
				{
					Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "blocks_yours.png"));
				}
				if (e != null && t > 0.6f)
				{
					break;
				}
			}
			if (!Check("the game turned your blow aside", e != null, $"stops {stopped}->{Blocks.YourBlowsStopped}, bandit health {health}->{Health(bandit)}, blow at {Who(PossessionCombat.LastBlowAt)}"))
			{
				CivilTest.Kill(bandit);
				yield break;
			}
			Check("...seen as BLOCKED", e.Kind == Stop.Blocked && !e.Shot, $"{e.Kind}, shot {e.Shot}, {e.Material}");
			Check("...steel on steel", e.Material == Clash.Metal, $"{e.Material}");
			Check("no damage to them, no hit marker, no hit sound", Health(bandit) >= health && hit <= 0f && fb.Hits == hits, $"health {health}->{Health(bandit)}, hit marker {hit:F2}, hits {hits}->{fb.Hits}");
			FloatingText floatingText = fb.Texts.Find((FloatingText x) => x.Word && x.Victim == bandit);
			Check("BLOCKED over them", floatingText != null && floatingText.Text == "BLOCKED", (floatingText == null) ? "no word" : floatingText.Text);
			Check("the crosshair's brackets", marker > 0.5f && fb.BlockKind == Stop.Blocked, $"marker {marker:F2}, {fb.BlockKind}");
			Check("your arm springs back off their sword", stoppedAt > 0.2f && stoppedAt < 0.7f && fb.StoppedRings, $"stopped {stoppedAt:F2} s into the blow, rings {fb.StoppedRings}");
			Check("sparks where they met", SceneCollector.StopsDrawn > drawn, $"drawn {drawn}->{SceneCollector.StopsDrawn}");
			Check("a clang", Mod.Audio.StopsPlayed > heard && Mod.Audio.LastStopSound == "clash_metal", $"played {heard}->{Mod.Audio.StopsPlayed}, {Mod.Audio.LastStopSound}");
			Check("they flash steel", flash > 0.2f, $"flash {flash:F2}");
			Check("the contact on their body, facing you", Near(e.At, bandit, 0.9f) && Vector2.Dot(e.Toward, (_host.current_position - bandit.current_position).normalized) > 0.8f, $"at {e.At.X:F1},{e.At.Y:F1},{e.At.Z:F2}, toward {e.Toward}");
			if (Mod.HudForTest.StopMarkerDrawn || marker > 0f)
			{
				Note($"HUD painted the brackets: {Mod.HudForTest.StopMarkerDrawn} (a minimised copy may not)");
			}
			CivilTest.Kill(bandit);
			yield return CivilTest.Wait(1f);
		}

		private IEnumerator YourBlowDodged()
		{
			CombatFeedback fb = Mod.Feedback;
			Actor bandit = Foe("sword_iron", 1.05f, "dodge");
			if (!Check("a bandit who steps aside", bandit != null))
			{
				yield break;
			}
			yield return Settle(bandit);
			float health = Health(bandit);
			int serial = Blocks.Newest;
			Mod.Melee.PressForTest();
			BlockEvent e = null;
			float stoppedAt = 0f;
			float found = -1f;
			for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
			{
				Stay();
				Hold(bandit);
				yield return null;
				e = e ?? Find(serial, (BlockEvent x) => x.YourBlow && x.Defender == bandit);
				stoppedAt = Mathf.Max(stoppedAt, fb.StoppedAt);
				if (e != null && found < 0f)
				{
					found = t;
				}
				if (found >= 0f && t - found > 0.1f && t - found - Time.unscaledDeltaTime <= 0.1f)
				{
					Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "blocks_dodged.png"));
				}
				if (e != null && t > 0.5f)
				{
					break;
				}
			}
			if (!Check("the bandit stepped out of your blow", e != null && e.Kind == Stop.Dodged, (e == null) ? $"no stop, health {health}->{Health(bandit)}" : $"{e.Kind}"))
			{
				CivilTest.Kill(bandit);
				yield break;
			}
			FloatingText floatingText = fb.Texts.Find((FloatingText x) => x.Word && x.Victim == bandit);
			Check("DODGED over them, the dashes at the crosshair", floatingText != null && floatingText.Text == "DODGED" && fb.BlockKind == Stop.Dodged, (floatingText == null) ? "no word" : $"{floatingText.Text}, {fb.BlockKind}");
			Check("no spring-back (nothing was struck)", stoppedAt <= 0f, $"stopped at {stoppedAt:F2}");
			Check("no damage", Health(bandit) >= health, $"{health}->{Health(bandit)}");
			CivilTest.Kill(bandit);
			yield return CivilTest.Wait(1f);
		}

		private IEnumerator Parried()
		{
			CombatFeedback fb = Mod.Feedback;
			Actor bandit = Foe("sword_iron", 1f, null, hold: false);
			if (!Check("a bandit with a sword, after you", bandit != null))
			{
				yield break;
			}
			yield return CivilTest.Wait(0.3f);
			int hurts = fb.Hurts;
			int serial = Blocks.Newest;
			int heard = Mod.Audio.StopsPlayed;
			BlockEvent e = null;
			float flash = 0f;
			float clash = 0f;
			bool painted = false;
			double struck = 0.0;
			for (float t = 0f; t < 15f; t += Time.unscaledDeltaTime)
			{
				if (e != null)
				{
					break;
				}
				Stay();
				try
				{
					Actor host = _host;
					double curWorldTime;
					struck = (curWorldTime = World.world.getCurWorldTime());
					host.last_attack_timestamp = curWorldTime;
				}
				catch
				{
				}
				try
				{
					if (bandit.isAlive() && (!bandit.has_attack_target || bandit.attack_target != _host))
					{
						bandit.startFightingWith(_host);
					}
				}
				catch
				{
				}
				yield return null;
				e = Find(serial, (BlockEvent x) => x.OnYou && x.Attacker == bandit);
			}
			if (!Check("their blow turned aside by yours", e != null, $"hurts {hurts}->{fb.Hurts}, last hit by {By(_host)}"))
			{
				CivilTest.Kill(bandit);
				yield break;
			}
			Check("...and the parry left your own blow's time alone", _host.last_attack_timestamp == struck, $"stamp {_host.last_attack_timestamp:F3}, your blow {struck:F3}");
			try
			{
				bandit.clearAttackTarget();
				bandit.cancelAllBeh();
				bandit.makeWait(30f);
			}
			catch
			{
			}
			for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
			{
				Stay();
				yield return null;
				flash = Mathf.Max(flash, fb.GuardFlash);
				clash = Mathf.Max(clash, fb.ClashWeight);
				if (!painted && t > 0.15f)
				{
					painted = true;
					Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "blocks_parried.png"));
				}
			}
			Check("...seen as PARRIED", e.Kind == Stop.Parried, $"{e.Kind}");
			Check("PARRIED under the crosshair, the flash round it", fb.GuardWord == "PARRIED" && flash > 0.3f && fb.GuardKind == Stop.Parried, $"word {fb.GuardWord}, flash {flash:F2}");
			Check("no wound", fb.Hurts == hurts, $"hurts {hurts}->{fb.Hurts}");
			HurtMark hurtMark = fb.HurtMarks.Find((HurtMark m) => m.From == bandit);
			Check("a steel mark pointing at them", hurtMark?.Blocked ?? false, (hurtMark == null) ? $"{fb.HurtMarks.Count} marks" : $"blocked {hurtMark.Blocked}");
			Check("your arms knocked back", clash > 0f && fb.SinceClash < 1f, $"weight {clash:F2}, {fb.SinceClash:F2} s ago");
			Check("the parry's ring", Mod.Audio.StopsPlayed > heard && Mod.Audio.LastStopSound == "parry", $"played {heard}->{Mod.Audio.StopsPlayed}, {Mod.Audio.LastStopSound}");
			CivilTest.Kill(bandit);
			yield return CivilTest.Wait(1f);
		}

		private IEnumerator ArrowsBlocked()
		{
			CombatFeedback fb = Mod.Feedback;
			bool ok = false;
			try
			{
				ok = _host.addTrait("block", pRemoveOpposites: true) || _host.hasTrait("block");
			}
			catch
			{
			}
			if (!Check("you can block arrows (the block trait)", ok))
			{
				yield break;
			}
			float f = _yaw + MathF.PI / 6f;
			Actor archer = CivilTest.SpawnNear("bandit", _home + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * 7f);
			if (!Check("an archer", archer != null && Equip(archer, "bow_wood")))
			{
				CivilTest.Kill(archer);
				yield break;
			}
			yield return CivilTest.Wait(0.3f);
			int hurts = fb.Hurts;
			int serial = Blocks.Newest;
			BlockEvent e = null;
			for (float t = 0f; t < 25f; t += 0.1f)
			{
				if (e != null)
				{
					break;
				}
				try
				{
					if (archer.isAlive() && (!archer.has_attack_target || archer.attack_target != _host))
					{
						archer.startFightingWith(_host);
					}
				}
				catch
				{
				}
				Stay();
				yield return CivilTest.Wait(0.1f);
				e = Find(serial, (BlockEvent x) => x.OnYou && x.Shot);
			}
			try
			{
				_host.removeTrait("block");
			}
			catch
			{
			}
			try
			{
				archer.clearAttackTarget();
				archer.cancelAllBeh();
				archer.makeWait(30f);
			}
			catch
			{
			}
			if (!Check("an arrow stopped on you", e != null, $"hurts {hurts}->{fb.Hurts}, last hit by {By(_host)}"))
			{
				CivilTest.Kill(archer);
				yield break;
			}
			yield return null;
			Check("...seen as BLOCKED", e.Kind == Stop.Blocked && fb.GuardWord == "BLOCKED", $"{e.Kind}, word {fb.GuardWord}");
			Check("...from where it was shot", Vector2.Dot(e.Toward, (archer.current_position - _host.current_position).normalized) > 0.8f, $"toward {e.Toward}");
			CivilTest.Kill(archer);
			yield return CivilTest.Wait(1f);
		}

		private IEnumerator Others()
		{
			CombatFeedback fb = Mod.Feedback;
			float f = _yaw + 0.6981317f;
			Vector2 vector = _home + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * 5f;
			Actor bandit = CivilTest.SpawnNear("bandit", vector);
			Actor wolf = CivilTest.Spawn("wolf", vector + new Vector2(0.8f, 0f));
			if (!Check("a bandit who turns blows aside, and a wolf after them", bandit != null && wolf != null && Equip(bandit, "sword_iron")))
			{
				CivilTest.Kill(bandit);
				CivilTest.Kill(wolf);
				yield break;
			}
			try
			{
				bandit.addTrait("deflect_projectile", pRemoveOpposites: true);
			}
			catch
			{
			}
			int others = Blocks.OthersStopped;
			int drawn = SceneCollector.StopsDrawn;
			int words = fb.Texts.FindAll((FloatingText x) => x.Word).Count;
			int serial = Blocks.Newest;
			BlockEvent e = null;
			for (float t = 0f; t < 15f; t += 0.1f)
			{
				if (e != null)
				{
					break;
				}
				try
				{
					if (wolf.isAlive() && (!wolf.has_attack_target || wolf.attack_target != bandit))
					{
						wolf.startFightingWith(bandit);
					}
					if (bandit.isAlive() && (!bandit.has_attack_target || bandit.attack_target != wolf))
					{
						bandit.startFightingWith(wolf);
					}
				}
				catch
				{
				}
				Stay();
				yield return CivilTest.Wait(0.1f);
				e = Find(serial, (BlockEvent x) => !x.YourBlow && !x.OnYou);
			}
			if (!Check("a stop between them, seen", e != null, $"others {others}->{Blocks.OthersStopped}"))
			{
				CivilTest.Kill(bandit);
				CivilTest.Kill(wolf);
				yield break;
			}
			yield return CivilTest.Wait(0.2f);
			Check("sparks for it", SceneCollector.StopsDrawn > drawn, $"drawn {drawn}->{SceneCollector.StopsDrawn}");
			Check("no word, no marker (it wasn't your fight)", fb.Texts.FindAll((FloatingText x) => x.Word).Count == words || fb.Texts.FindAll((FloatingText x) => x.Word).TrueForAll((FloatingText x) => x.Victim != e.Defender), $"words {words}->{fb.Texts.FindAll((FloatingText x) => x.Word).Count}");
			Note($"stop between others: {e.Kind} on {Who(e.Defender)}, {e.Material}");
			CivilTest.Kill(bandit);
			CivilTest.Kill(wolf);
		}

		private Actor Foe(string weapon, float dist, string trait, bool hold = true)
		{
			Vector2 at = _home + new Vector2(Mathf.Cos(_yaw), Mathf.Sin(_yaw)) * dist;
			Actor actor = CivilTest.SpawnNear("bandit", at);
			if (actor == null)
			{
				return null;
			}
			if (weapon != null && !Equip(actor, weapon))
			{
				CivilTest.Kill(actor);
				return null;
			}
			try
			{
				if (trait == "dodge")
				{
					((BaseSimObject)actor).addStatusEffect("dodge", 5f, pColorEffect: false);
				}
				else if (trait != null)
				{
					actor.addTrait(trait, pRemoveOpposites: true);
				}
				actor.restoreHealth(actor.getMaxHealth());
				if (actor.hasTrait("slow"))
				{
					actor.removeTrait("slow");
				}
			}
			catch (Exception ex)
			{
				Log.Error("BLOCKS foe: " + ex.Message);
			}
			if (hold)
			{
				Hold(actor);
			}
			return actor;
		}

		private IEnumerator Settle(Actor target)
		{
			for (int i = 0; i < 20; i++)
			{
				Stay();
				Hold(target);
				Face(target);
				yield return null;
			}
		}

		private static void Hold(Actor a)
		{
			try
			{
				if (a != null && a.isAlive())
				{
					a.cancelAllBeh();
					a.makeWait(5f);
					a.clearAttackTarget();
				}
			}
			catch
			{
			}
		}

		private void Stay()
		{
			if (_host == null || !_host.isAlive())
			{
				return;
			}
			_host.current_position = _home;
			_host.dirty_current_tile = true;
			try
			{
				_host.restoreHealth(_host.getMaxHealth());
				if (((BaseSimObject)_host).hasStatus("burning"))
				{
					_host.finishStatusEffect("burning");
				}
			}
			catch
			{
			}
		}

		private void Face(Actor target)
		{
			if (target != null && Hitboxes.Get(target, out var box))
			{
				Vector3 eyeForTest = Mod.EyeForTest;
				float num = box.Centre.X - eyeForTest.x;
				float num2 = box.Centre.Y - eyeForTest.y;
				float num3 = Mathf.Max(0.05f, Mathf.Sqrt(num * num + num2 * num2));
				Mod.SetYaw(Mathf.Atan2(num2, num));
				Mod.SetPitchDegrees(Mathf.Clamp(Mathf.Atan((box.Centre.Z - eyeForTest.z) / num3) * 57.29578f, -44f, 44f));
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

		private static bool Near(Vec3 at, Actor a, float within)
		{
			Vector2 current_position = a.current_position;
			if (Mathf.Abs(at.X - current_position.x) < within)
			{
				return Mathf.Abs(at.Y - current_position.y) < within;
			}
			return false;
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

		private static string By(Actor a)
		{
			try
			{
				BaseSimObject attackedBy = a.attackedBy;
				return (attackedBy == null) ? "nobody" : (attackedBy.isActor() ? WorldBoxMod.Describe(attackedBy.a) : "a building");
			}
			catch
			{
				return "?";
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
				Log.Error("BLOCKS equip " + id + ": " + ex.Message);
				return false;
			}
		}
	}
}
