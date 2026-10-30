using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class HurtFromTest : CivilTest
	{
		private const float Slack = 10f;

		private Actor _host;

		private Vector2 _home;

		private float _yaw;

		protected override string Prefix => "HURT";

		protected override IEnumerator Body()
		{
			WorldTile worldTile = CivilTest.Stage();
			if (!Check("open land", worldTile != null))
			{
				yield break;
			}
			CivilTest.ClearBox(worldTile.x - 12, worldTile.y - 12, worldTile.x + 12, worldTile.y + 12);
			_host = CivilTest.SpawnNear("human", new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f));
			if (Check("a villager", _host != null))
			{
				yield return Possess(_host);
				if (Check("first person in them", InControl(_host)))
				{
					_home = _host.current_position;
					_yaw = Mod.ViewYaw;
					Mod.SetPitchDegrees(-3f);
					yield return Part("an arrow from behind on the left", Arrow());
					yield return Part("bites from two sides, and a burn", Bites());
				}
			}
		}

		private IEnumerator Arrow()
		{
			CombatFeedback fb = Mod.Feedback;
			float f = _yaw + MathF.PI * 3f / 4f;
			Actor bandit = CivilTest.SpawnNear("bandit", _home + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * 7f);
			if (!Check("a bandit behind you on the left", bandit != null))
			{
				yield break;
			}
			bool bow = Equip(bandit, "bow_wood");
			yield return CivilTest.Wait(0.3f);
			bool flag = false;
			try
			{
				flag = !bandit.hasMeleeAttack();
			}
			catch
			{
			}
			if (!Check("...with a bow", bow && flag))
			{
				yield break;
			}
			HurtMark mark = null;
			float health0 = Health(_host);
			float shotFrom = 0f;
			for (float t = 0f; t < 25f; t += 0.1f)
			{
				if (mark != null)
				{
					break;
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
				Stay();
				yield return CivilTest.Wait(0.1f);
				mark = fb.HurtMarks.Find((HurtMark m) => m.From == bandit);
				if (mark != null)
				{
					shotFrom = (bandit.current_position - _host.current_position).magnitude;
				}
			}
			if (!Check("an arrow hits you and leaves a mark", mark != null, string.Format("health {0} -> {1}, bandit {2}, last hit by {3}", health0, Health(_host), bandit.isAlive() ? "alive" : "dead", By(_host))))
			{
				CivilTest.Kill(bandit);
				yield break;
			}
			Note($"shot from {shotFrom:F1} tiles");
			Check("...from a distance (an arrow, not a blow)", shotFrom > 2.5f);
			try
			{
				bandit.clearAttackTarget();
				bandit.cancelAllBeh();
				bandit.makeWait(30f);
			}
			catch
			{
			}
			yield return Frames(3);
			float num = Rel(Bearing(bandit));
			Check("the bandit is behind you on the left", num > 80f && num <= 180f, $"at {num:F0} degrees");
			Check("...at the bandit", Mathf.Abs(Mathf.DeltaAngle(Rel(mark), num)) < 10f, $"mark {Rel(mark):F0}, bandit {num:F0}");
			DrawnAt("the HUD draws it there", num);
			Capture("hurtfrom_arrow.png");
			yield return Frames(2);
			_yaw = Bearing(bandit) * (MathF.PI / 180f);
			yield return Frames(3);
			Check("turned to face them, the mark is at the top", Mathf.Abs(Rel(mark)) < 10f, $"mark {Rel(mark):F0}");
			DrawnAt("...as drawn", 0f);
			Capture("hurtfrom_turned.png");
			yield return Frames(2);
			Vector2 fell = bandit.current_position;
			CivilTest.Kill(bandit);
			yield return Frames(3);
			Check("the bandit dead, its mark stays where it fell", mark.From == null && fb.HurtMarks.Contains(mark) && Mathf.Abs(Mathf.DeltaAngle(Rel(mark), Rel(Bearing(fell)))) < 10f, $"following {mark.From != null}, mark {Rel(mark):F0}");
			for (float t = 0f; t < 2.7f; t += 0.1f)
			{
				if (!fb.HurtMarks.Contains(mark))
				{
					break;
				}
				Stay();
				yield return CivilTest.Wait(0.1f);
			}
			Check("...and fades away", !fb.HurtMarks.Contains(mark), $"age {mark.Age:F1} s");
			Heal();
		}

		private IEnumerator Bites()
		{
			CombatFeedback fb = Mod.Feedback;
			yield return Frames(2);
			fb.HurtMarks.Clear();
			Actor right = Beside(-90f, 1.3f);
			Actor aheadLeft = Beside(40f, 1.6f);
			if (!Check("two wolves, at your right and ahead on the left", right != null && aheadLeft != null))
			{
				CivilTest.Kill(right);
				CivilTest.Kill(aheadLeft);
				yield break;
			}
			yield return CivilTest.Wait(0.3f);
			Bite(right);
			yield return Frames(3);
			HurtMark first = fb.HurtMarks.Find((HurtMark m) => m.From == right);
			Check("a bite from the right: a mark at the right", first != null && Mathf.Abs(Mathf.DeltaAngle(Rel(first), Rel(Bearing(right)))) < 10f && Rel(first) < -60f, (first == null) ? ("no mark (last hit by " + By(_host) + ")") : $"mark {Rel(first):F0}, wolf {Rel(Bearing(right)):F0}");
			Bite(aheadLeft);
			yield return Frames(3);
			HurtMark hurtMark = fb.HurtMarks.Find((HurtMark m) => m.From == aheadLeft);
			Check("a bite from ahead on the left: a second mark there", fb.HurtMarks.Count == 2 && hurtMark != null && Mathf.Abs(Mathf.DeltaAngle(Rel(hurtMark), Rel(Bearing(aheadLeft)))) < 10f, (hurtMark == null) ? $"{fb.HurtMarks.Count} marks" : $"{fb.HurtMarks.Count} marks, mark {Rel(hurtMark):F0}, wolf {Rel(Bearing(aheadLeft)):F0}");
			Capture("hurtfrom_two.png");
			Note($"drawn: {Mod.HudForTest.HurtMarksDrawn} marks (0 in a minimised copy, which may not paint its HUD)");
			yield return CivilTest.Wait(0.5f);
			float aged = first?.Age ?? 0f;
			Bite(right);
			yield return Frames(3);
			Check("bitten again by the first: its mark lit afresh, no third", fb.HurtMarks.Count == 2 && first != null && first.Age < aged, $"{fb.HurtMarks.Count} marks, age {aged:F2} -> {first?.Age:F2}");
			int before = fb.HurtMarks.Count;
			float health = Health(_host);
			try
			{
				((BaseSimObject)_host).getHit(3f, pFlash: true, AttackType.Fire, (BaseSimObject)null, pSkipIfShake: false, pMetallicWeapon: false, pCheckDamageReduction: true);
			}
			catch (Exception ex)
			{
				Log.Error("HURT burn: " + ex.Message);
			}
			yield return Frames(3);
			bool flag = fb.HurtMarks.Exists((HurtMark m) => m.Age < 0.04f);
			Check("a burn no one dealt leaves no mark", Health(_host) < health && fb.HurtMarks.Count == before && !flag, $"health {health} -> {Health(_host)}, {fb.HurtMarks.Count} marks");
			CivilTest.Kill(right);
			CivilTest.Kill(aheadLeft);
			Heal();
		}

		private Actor Beside(float deg, float dist)
		{
			float f = _yaw + deg * (MathF.PI / 180f);
			Actor actor = CivilTest.Spawn("wolf", _host.current_position + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * dist);
			try
			{
				if (actor != null)
				{
					actor.cancelAllBeh();
					actor.makeWait(30f);
				}
			}
			catch
			{
			}
			return actor;
		}

		private void Bite(Actor wolf)
		{
			try
			{
				((BaseSimObject)_host).getHit(6f, pFlash: true, AttackType.Weapon, (BaseSimObject)wolf, pSkipIfShake: false, pMetallicWeapon: false, pCheckDamageReduction: true);
			}
			catch (Exception ex)
			{
				Log.Error("HURT bite: " + ex.Message);
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
			Mod.SetYaw(_yaw);
			try
			{
				if (((BaseSimObject)_host).hasStatus("burning"))
				{
					_host.finishStatusEffect("burning");
				}
			}
			catch
			{
			}
		}

		private IEnumerator Frames(int n)
		{
			for (int i = 0; i < n; i++)
			{
				Stay();
				yield return null;
			}
		}

		private void Heal()
		{
			try
			{
				_host.restoreHealth(_host.getMaxHealth());
			}
			catch
			{
			}
		}

		private void DrawnAt(string what, float rel)
		{
			Hud hudForTest = Mod.HudForTest;
			if (hudForTest.HurtMarksDrawn == 0)
			{
				Note("(" + what + ": the HUD painted no marks; a minimised copy may not paint)");
				return;
			}
			float num = hudForTest.HurtMarkAngle * 57.29578f;
			Check(what, Mathf.Abs(Mathf.DeltaAngle(num, rel)) < 10f, $"drawn at {num:F0}, {hudForTest.HurtMarksDrawn} marks");
		}

		private static void Capture(string file)
		{
			try
			{
				ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, file));
			}
			catch
			{
			}
		}

		private float Bearing(Vector2 at)
		{
			Vector2 vector = at - _host.current_position;
			return Mathf.Atan2(vector.y, vector.x) * 57.29578f;
		}

		private float Bearing(Actor a)
		{
			return Bearing(a.current_position);
		}

		private float Rel(float bearingDeg)
		{
			return Mathf.DeltaAngle(Mod.ViewYaw * 57.29578f, bearingDeg);
		}

		private float Rel(HurtMark m)
		{
			return Rel(Mathf.Atan2(m.Toward.y, m.Toward.x) * 57.29578f);
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
				Log.Error("HURT equip " + id + ": " + ex.Message);
				return false;
			}
		}
	}
}
