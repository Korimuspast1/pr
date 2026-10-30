using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class FriendlyFireTest : CivilTest
	{
		private Actor _host;

		private Actor _kingA;

		private Actor _kingB;

		private readonly List<Actor> _ours = new List<Actor>();

		private readonly List<Actor> _theirs = new List<Actor>();

		private readonly Dictionary<Actor, Vector2> _held = new Dictionary<Actor, Vector2>();

		private Vector2 _home;

		private float _lane;

		private int _usedOurs;

		private readonly HashSet<Actor> _usedFoes = new HashSet<Actor>();

		protected override string Prefix => "FRIENDLY";

		protected override IEnumerator Body()
		{
			CrownOffer.AutoAnswer = true;
			_kingA = Village("human", 14, 0);
			if (!Check("a village of your own", _kingA != null))
			{
				yield break;
			}
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive() && item != _kingA && item.isAdult())
				{
					_ours.Add(item);
				}
			}
			WorldTile tile = _kingA.city.getTile();
			_kingB = Village("human", 10, 0, tile, 40f);
			if (!Check("a second kingdom a way off", _kingB != null && _kingB.kingdom != _kingA.kingdom))
			{
				yield break;
			}
			foreach (Actor item2 in Founded)
			{
				if (item2 != null && item2.isAlive() && item2 != _kingB && item2.isAdult())
				{
					_theirs.Add(item2);
				}
			}
			_host = Civilian(_ours);
			if (_host != null)
			{
				_ours.Remove(_host);
			}
			else
			{
				_host = Ours();
			}
			yield return Possess(_host);
			if (Check("first person in a villager", InControl(_host)))
			{
				try
				{
					((BaseSimObject)_host).addStatusEffect("invincible", 600f, pColorEffect: false);
				}
				catch
				{
				}
				_home = _host.current_position;
				_lane = ClearYaw(out var clear);
				Note($"lane {_lane * 57.29578f:F0} degrees, clear {clear} tiles; you {Who(_host)}{Side(_host)}; {_ours.Count} of ours, {_theirs.Count} of theirs");
				Mod.Settings.HeadBob = 0f;
				bool savedFire = Mod.Settings.FriendlyFire;
				Mod.Settings.FriendlyFire = false;
				yield return Part("peace: a blow at a sheep beside a townsman", PeaceCollateral());
				yield return Part("war", War());
				yield return Part("war: who your blow may land on", WhoMayBeStruck());
				yield return Part("battle: a blow with a friend in the way", BattleBlow());
				yield return Part("battle: an arrow past a friend", BattleArrow());
				yield return Part("quest stars", Stars());
				yield return Part("no battle: a blow aimed at a townsman", PeaceDeliberate());
				Mod.Settings.FriendlyFire = savedFire;
				CrownOffer.AutoAnswer = false;
			}
		}

		private IEnumerator PeaceCollateral()
		{
			if (!Equip(_host, "sword_iron"))
			{
				Check("a sword", ok: false);
				yield break;
			}
			yield return CivilTest.Wait(0.3f);
			Home();
			Mod.SetPitchDegrees(-10f);
			yield return Settle();
			Actor sheep = SpawnAt("sheep", Ahead(1f, 0f));
			Actor friend = Place(Ours(), Ahead(1.15f, 0.15f));
			yield return CivilTest.Wait(0.4f);
			yield return Settle();
			Face(sheep);
			yield return Settle();
			Check("no battle on in a quiet village", !FriendlyFire.InBattle(_host));
			float s0 = Health(sheep);
			float f0 = Health(friend);
			int spared = FriendlyFire.Spared;
			PossessionCombat.AttackForTest(_host);
			yield return CivilTest.Wait(0.4f);
			float num = Health(sheep);
			float num2 = Health(friend);
			Check("the blow lands on the sheep", PossessionCombat.LastBlowAt == sheep && num < s0, $"blow at {Who(PossessionCombat.LastBlowAt)}, sheep {s0}->{num}");
			Check("the townsman beside it is spared", num2 >= f0, $"townsman {f0}->{num2}, spared {spared}->{FriendlyFire.Spared}");
			Let(friend);
			CivilTest.Kill(sheep);
		}

		private IEnumerator PeaceDeliberate()
		{
			if (!Equip(_host, "sword_iron"))
			{
				Check("a sword", ok: false);
				yield break;
			}
			yield return CivilTest.Wait(0.3f);
			Actor friend = Ours();
			bool saved = Mod.Settings.FriendlyFire;
			for (int pass = 0; pass < 2; pass++)
			{
				bool on = pass == 1;
				Mod.Settings.FriendlyFire = on;
				bool flag = false;
				bool flag2 = false;
				string text = "";
				for (int attempt = 1; attempt <= 2; attempt++)
				{
					if (flag)
					{
						break;
					}
					Home();
					Mod.SetPitchDegrees(-10f);
					yield return Settle();
					if (attempt == 1 && pass == 0)
					{
						Check("the fighting is over here", !FriendlyFire.InBattle(_host));
					}
					Place(friend, Ahead(1f, 0f));
					yield return CivilTest.Wait(0.4f);
					yield return Settle();
					Face(friend);
					yield return Settle();
					if (on)
					{
						try
						{
							friend.setHealth(1);
						}
						catch
						{
						}
					}
					float f0 = Health(friend);
					int spared = FriendlyFire.Spared;
					string onCrosshair = ((Mod.Picked == friend) ? "the townsman" : ((Mod.Picked is Actor a) ? Who(a) : ((Mod.Picked != null) ? Mod.Picked.GetType().Name : "nothing")));
					FriendlyFire.LastRefused = null;
					FriendlyFire.Trace = new List<KeyValuePair<BaseSimObject, string>>();
					PossessionCombat.AttackForTest(_host);
					string seen = Traced();
					yield return CivilTest.Wait(0.4f);
					float num = Health(friend);
					flag2 = friend == null || !friend.isAlive();
					flag = (on ? (PossessionCombat.LastBlowAt == friend && flag2) : (num >= f0 && PossessionCombat.LastBlowAt != friend));
					text = string.Format("friendly fire {0}, try {1}: crosshair on {2}, blow at {3}, townsman {4} {5}->{6}{7}, spared {8}->{9}, refused {10}, the blow met [{11}], battle {12}, the law: {13}", on ? "on" : "off", attempt, onCrosshair, Who(PossessionCombat.LastBlowAt), Who(friend), f0, num, flag2 ? " (dead)" : "", spared, FriendlyFire.Spared, Who(FriendlyFire.LastRefused as Actor), seen, FriendlyFire.InBattle(_host), Law.LastCrime ?? "-");
					Note(text);
				}
				Check(on ? "friendly fire on: a blow aimed at a townsman lands and kills" : "friendly fire off: a blow aimed right at a townsman passes over them", flag, text);
				if (flag2)
				{
					break;
				}
			}
			Mod.Settings.FriendlyFire = saved;
			Let(friend);
			yield return CivilTest.Wait(0.3f);
			Calm();
		}

		private static string Traced()
		{
			List<KeyValuePair<BaseSimObject, string>> trace = FriendlyFire.Trace;
			FriendlyFire.Trace = null;
			if (trace == null || trace.Count == 0)
			{
				return "nobody";
			}
			List<string> list = new List<string>();
			foreach (KeyValuePair<BaseSimObject, string> item in trace)
			{
				list.Add(((item.Key != null && item.Key.isActor()) ? Who(item.Key.a) : ((item.Key != null) ? "a building" : "?")) + " " + item.Value);
			}
			return string.Join(", ", list);
		}

		private void Calm()
		{
			try
			{
				Law.SetBounty(_host.kingdom, _host, 0);
			}
			catch
			{
			}
			foreach (Actor our in _ours)
			{
				try
				{
					if (our != null && our.isAlive() && our.has_attack_target && our.attack_target == _host)
					{
						our.clearAttackTarget();
						our.cancelAllBeh();
					}
				}
				catch
				{
				}
			}
		}

		private IEnumerator War()
		{
			Kingdom a = _kingA.kingdom;
			Kingdom b = _kingB.kingdom;
			try
			{
				World.world.diplomacy.startWar(b, a, WarTypeLibrary.normal, pLog: false);
			}
			catch (Exception ex)
			{
				Log.Error("FRIENDLY war: " + ex.Message);
			}
			yield return CivilTest.Wait(0.5f);
			Check("the kingdoms are at war", a.isEnemy(b));
			Actor foe = Place(Theirs(), Ahead(6f, 4f));
			yield return CivilTest.Wait(0.5f);
			Check("an enemy soldier close by makes it a battle", FriendlyFire.InBattle(_host), $"foe {Who(foe)} at {(foe.current_position - _host.current_position).magnitude:F1} tiles");
			CivilTest.Kill(foe);
		}

		private IEnumerator WhoMayBeStruck()
		{
			Home();
			Mod.SetYaw(_lane);
			Mod.SetPitchDegrees(-10f);
			yield return Settle();
			Calm();
			Actor soldier = Place(Theirs(), Ahead(1.2f, 0.8f));
			Actor villager = Place(TheirCivilian(), Ahead(1.2f, -0.8f));
			Actor friend = Place(Ours(), Ahead(1.6f, 0f));
			if (!Check("an enemy soldier, an enemy villager and a townsman", soldier != null && villager != null && friend != null, Who(soldier) + Side(soldier) + ", " + Who(villager) + Side(villager) + ", " + Who(friend) + Side(friend)))
			{
				yield break;
			}
			yield return CivilTest.Wait(0.3f);
			yield return Settle();
			bool friendlyFire = Mod.Settings.FriendlyFire;
			Mod.Settings.FriendlyFire = false;
			bool reached;
			string detail = Strike(soldier, out reached);
			bool reached2;
			string detail2 = Strike(villager, out reached2);
			string detail3 = Strike(friend, out var reached3);
			Check("friendly fire off: your villager's blow may land on an enemy soldier of your own kind", reached, detail);
			Check("...and on an enemy villager", reached2, detail2);
			Check("...but passes over your townsman", !reached3, detail3);
			Mod.Settings.FriendlyFire = true;
			detail3 = Strike(friend, out reached3);
			Check("friendly fire on: it lands on your townsman too", reached3, detail3);
			int num = 0;
			int num2 = 0;
			string arg = "";
			for (int i = 0; i < 4; i++)
			{
				if (!friend.isAlive())
				{
					break;
				}
				try
				{
					friend.setHealth(Mathf.Max(3, Mathf.RoundToInt((float)friend.getMaxHealth() * 0.3f)));
				}
				catch
				{
				}
				float num3 = Health(friend);
				int fullBlows = FriendlyFire.FullBlows;
				arg = Strike(friend, out var reached4);
				bool flag = Health(friend) <= num3 - 2f;
				try
				{
					friend.setHealth(friend.getMaxHealth());
				}
				catch
				{
				}
				if (reached4)
				{
					num2++;
					if (flag && FriendlyFire.FullBlows > fullBlows)
					{
						num++;
					}
				}
			}
			Check("...in full past half health, where the game would stun them instead", num2 > 0 && num == num2, $"{num} of {num2} in full; last {arg}");
			Mod.Settings.FriendlyFire = friendlyFire;
			CivilTest.Kill(soldier);
			CivilTest.Kill(villager);
			Let(friend);
			yield return CivilTest.Wait(0.3f);
			Calm();
		}

		private string Strike(Actor victim, out bool reached)
		{
			reached = false;
			if (victim == null || !victim.isAlive())
			{
				return "nobody";
			}
			Vector2 current_position = victim.current_position;
			WorldTile pHitTile = World.world.GetTile((int)current_position.x, (int)current_position.y) ?? victim.current_tile;
			AttackData pData = new AttackData(_host, pHitTile, new Vector3(current_position.x, current_position.y, ((BaseSimObject)victim).getHeight()), _host.current_position, null, World.world.kingdoms_wild.get("possessed"), AttackType.Weapon, pMetallicWeapon: false, pSkipShake: false, pProjectile: false, null, null, 0.05f);
			float num = Health(victim);
			int spared = FriendlyFire.Spared;
			int refused = FriendlyFire.Refused;
			FriendlyFire.Meant = victim;
			ApplyAttackState state;
			try
			{
				state = MapBox.checkAttackFor(pData, victim).state;
			}
			catch (Exception ex)
			{
				return "threw " + ex.Message;
			}
			finally
			{
				FriendlyFire.Meant = null;
			}
			reached = state != ApplyAttackState.Continue && state != ApplyAttackState.Miss;
			return $"{Who(victim)}{Side(victim)}: {state}, health {num}->{Health(victim)}, spared {spared}->{FriendlyFire.Spared}, refused {refused}->{FriendlyFire.Refused}";
		}

		private IEnumerator BattleBlow()
		{
			Home();
			Mod.SetYaw(_lane);
			Mod.SetPitchDegrees(-10f);
			yield return Settle();
			Calm();
			Actor friend = Place(Ours(), Ahead(0.7f, 0f));
			Actor foe = Place(Theirs(), Ahead(0.85f, 0.45f));
			yield return CivilTest.Wait(0.4f);
			yield return Settle();
			Face(friend);
			yield return Settle();
			bool battle = FriendlyFire.InBattle(_host);
			bool friendly = FriendlyFire.Friendly(_host, friend);
			float f0 = Health(friend);
			float e0 = Health(foe);
			string onCrosshair = ((Mod.Picked == friend) ? "the friend" : Who(Mod.Picked as Actor));
			int spared = FriendlyFire.Spared;
			try
			{
				friend.attackedBy = null;
			}
			catch
			{
			}
			FriendlyFire.LastRefused = null;
			PossessionCombat.AttackForTest(_host);
			BaseSimObject refused = FriendlyFire.LastRefused;
			float fNow = Health(friend);
			BaseSimObject by = null;
			try
			{
				by = friend.attackedBy;
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.4f);
			float num = Health(friend);
			float num2 = Health(foe);
			Check("in a battle the blow passes the friend under the crosshair", fNow >= f0 && by != _host, string.Format("battle {0}, friendly {1}{2}, crosshair on {3}, friend {4}->{5} at the blow, {6} after (last hit by {7}), spared {8}->{9}", battle, friendly, Side(friend), onCrosshair, f0, fNow, num, (by == null) ? "nobody" : ((by == _host) ? "you" : (by.isActor() ? Who(by.a) : "something")), spared, FriendlyFire.Spared));
			Actor lastBlowAt = PossessionCombat.LastBlowAt;
			bool flag = false;
			bool flag2 = false;
			try
			{
				flag = lastBlowAt != null && lastBlowAt.kingdom != null && lastBlowAt.kingdom.isEnemy(_host.kingdom);
			}
			catch
			{
			}
			try
			{
				flag2 = num2 < e0 && foe.attackedBy == _host;
			}
			catch
			{
			}
			Check("...and goes to an enemy instead", flag || flag2, $"blow at {Who(lastBlowAt)}{Side(lastBlowAt)}, the foe beside them {e0}->{num2}{Side(foe)}");
			Check("...whom your villager may strike", refused == null || (refused != lastBlowAt && refused != foe), "refused " + ((refused == null) ? "nobody" : (refused.isActor() ? Who(refused.a) : "a building")));
			Let(friend);
			CivilTest.Kill(foe);
		}

		private IEnumerator BattleArrow()
		{
			if (!Equip(_host, "bow_wood"))
			{
				Check("a bow", ok: false);
				yield break;
			}
			for (int i = 0; i < 30; i++)
			{
				if (Mod.Bow.InHand)
				{
					break;
				}
				yield return null;
			}
			Actor friend = Ours();
			bool friendHurt = false;
			bool foeHit = false;
			string last = "";
			for (int i = 1; i <= 3; i++)
			{
				if (foeHit)
				{
					break;
				}
				if (friendHurt)
				{
					break;
				}
				Home();
				Mod.SetYaw(_lane);
				Mod.SetPitchDegrees(0f);
				yield return Settle();
				Calm();
				Actor foe = Place(Theirs(), Ahead(8f, 0f));
				if (foe == null)
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
				yield return Settle();
				Face(foe);
				yield return Settle();
				Vector3 eyeForTest = Mod.EyeForTest;
				Hitboxes.Get(foe, out var box);
				float num = box.Centre.X - eyeForTest.x;
				float num2 = box.Centre.Y - eyeForTest.y;
				float num3 = Mathf.Sqrt(num * num + num2 * num2);
				Vector2 at = Vector2.zero;
				float num4 = float.MaxValue;
				for (float num5 = 2.5f; num5 <= num3 - 1.5f; num5 += 0.25f)
				{
					float num6 = eyeForTest.z + (box.Centre.Z - eyeForTest.z) * num5 / num3;
					Vector2 vector = new Vector2(eyeForTest.x + num / num3 * num5, eyeForTest.y + num2 / num3 * num5);
					float num7 = Mathf.Abs(num6 - (Mod.TerrainForTest.SampleHeight(vector.x, vector.y) + 0.45f));
					if (num7 < num4)
					{
						num4 = num7;
						at = vector;
					}
				}
				Place(friend, at);
				yield return CivilTest.Wait(0.4f);
				yield return Settle();
				Face(foe);
				yield return Settle();
				bool battle = FriendlyFire.InBattle(_host);
				float f0 = Health(friend);
				float e0 = Health(foe);
				string onCrosshair = ((Mod.Picked == friend) ? "the friend" : ((Mod.Picked == foe) ? "the foe" : Who(Mod.Picked as Actor)));
				int ended = Ended();
				int spared = FriendlyFire.Spared;
				int launched = FirstPersonShots.Launched;
				try
				{
					friend.attackedBy = null;
				}
				catch
				{
				}
				FirstPersonShots.Trace = true;
				Mod.Bow.LooseForTest(_host);
				for (float t = 0f; t < 3f; t += 0.02f)
				{
					if (Ended() != ended)
					{
						break;
					}
					Hold();
					yield return CivilTest.Wait(0.02f);
				}
				FirstPersonShots.Trace = false;
				yield return CivilTest.Wait(0.2f);
				float num8 = Health(friend);
				float num9 = Health(foe);
				BaseSimObject baseSimObject = null;
				try
				{
					baseSimObject = friend.attackedBy;
				}
				catch
				{
				}
				friendHurt = FirstPersonShots.LastVictim == friend || (num8 < f0 && baseSimObject == _host);
				foeHit = FirstPersonShots.LastVictim == foe && num9 < e0;
				last = $"try {i}: battle {battle}, crosshair on {onCrosshair}, ended at {FirstPersonShots.LastWhat} in {Who(FirstPersonShots.LastVictim)}, " + string.Format("friend {0}->{1}, foe {2}->{3}{4}, spared {5}->{6}, shots loosed {7}{8}", f0, num8, e0, num9, Side(foe), spared, FriendlyFire.Spared, FirstPersonShots.Launched - launched, Mod.Converse.Open ? ", a talk open" : "");
				Note(last);
				CivilTest.Kill(foe);
			}
			Check("an arrow loosed at a friend in a battle flies past them", !friendHurt, last);
			Check("...into the foe behind", foeHit, last);
			Let(friend);
		}

		private IEnumerator Stars()
		{
			Equip(_host, "sword_iron");
			Home();
			Mod.SetYaw(_lane);
			Mod.SetPitchDegrees(-4f);
			yield return Settle();
			Actor giver = Place(Ours(), Ahead(3.5f, -0.8f));
			Actor wolf = SpawnAt("wolf", Ahead(6f, 0.8f));
			if (Check("a wolf and someone who wants it gone", giver != null && wolf != null))
			{
				try
				{
					((BaseSimObject)wolf).addStatusEffect("invincible", 60f, pColorEffect: false);
				}
				catch
				{
				}
				yield return CivilTest.Wait(0.4f);
				Quest q = new Quest
				{
					Kind = QuestKind.Hunt,
					Giver = giver.data.id,
					GiverName = Dialogue.NameOf(giver),
					Target = wolf.data.id,
					TargetName = "wolf",
					Money = 10,
					Opinion = 5
				};
				Check("the hunt is taken on", Quests.Accept(q, _host, null));
				List<KeyValuePair<Actor, bool>> starred = new List<KeyValuePair<Actor, bool>>();
				Quests.Starred(starred);
				Check("a gold star over the wolf", starred.Exists((KeyValuePair<Actor, bool> kv) => kv.Key == wolf && kv.Value));
				Check("a pale star over who asked", starred.Exists((KeyValuePair<Actor, bool> kv) => kv.Key == giver && !kv.Value));
				for (int i = 0; i < 20; i++)
				{
					Hold();
					yield return null;
				}
				Hud hudForTest = Mod.HudForTest;
				Note($"drawn: {hudForTest.StarsGold} gold, {hudForTest.StarsPale} pale (0 in a minimised copy, which may not paint its HUD)");
				try
				{
					ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "friendly_stars.png"));
				}
				catch
				{
				}
				Mod.SaveFrame("friendly_stars_frame.png");
				yield return CivilTest.Wait(0.5f);
				try
				{
					wolf.finishStatusEffect("invincible");
				}
				catch
				{
				}
				Quests.OnKill(wolf);
				CivilTest.Kill(wolf);
				yield return CivilTest.Wait(0.8f);
				Quests.Starred(starred);
				Check("the hunt is done", q.Stage == 1, Quests.Status(q));
				Check("the giver's star turns gold", starred.Exists((KeyValuePair<Actor, bool> kv) => kv.Key == giver && kv.Value) && starred.Count == 1, starred.Count + " starred");
				Quests.Abandon(q, _host);
				Quests.Starred(starred);
				Check("no stars once the task is gone", starred.Count == 0);
				Let(giver);
			}
		}

		private Actor Ours()
		{
			while (_usedOurs < _ours.Count)
			{
				Actor actor = _ours[_usedOurs++];
				if (actor != null && actor.isAlive() && actor != _host)
				{
					return actor;
				}
			}
			return null;
		}

		private Actor Theirs()
		{
			for (int i = 0; i < 2; i++)
			{
				for (int j = 0; j < _theirs.Count; j++)
				{
					Actor actor = _theirs[j];
					if (actor != null && actor.isAlive() && !_usedFoes.Contains(actor))
					{
						bool flag = true;
						try
						{
							flag = actor.profession_asset == null || actor.profession_asset.is_civilian;
						}
						catch
						{
						}
						if (!(i == 0 && flag))
						{
							_usedFoes.Add(actor);
							return actor;
						}
					}
				}
			}
			return null;
		}

		private Actor TheirCivilian()
		{
			Actor actor = Civilian(_theirs.FindAll((Actor t) => !_usedFoes.Contains(t)));
			if (actor != null)
			{
				_usedFoes.Add(actor);
			}
			return actor;
		}

		private static Actor Civilian(List<Actor> from)
		{
			foreach (Actor item in from)
			{
				try
				{
					if (item != null && item.isAlive() && item.profession_asset != null && item.profession_asset.is_civilian)
					{
						return item;
					}
				}
				catch
				{
				}
			}
			return null;
		}

		private Actor Place(Actor a, Vector2 at)
		{
			if (a == null)
			{
				return null;
			}
			try
			{
				a.cancelAllBeh();
				a.clearAttackTarget();
				if (a.is_inside_building)
				{
					a.exitBuilding();
				}
			}
			catch
			{
			}
			a.current_position = at;
			a.dirty_current_tile = true;
			try
			{
				WorldTile tile = World.world.GetTile((int)at.x, (int)at.y);
				if (tile != null)
				{
					a.setCurrentTile(tile);
				}
			}
			catch
			{
			}
			_held[a] = at;
			return a;
		}

		private Actor SpawnAt(string id, Vector2 at)
		{
			Actor actor = CivilTest.Spawn(id, at);
			if (actor == null)
			{
				return null;
			}
			actor.dirty_current_tile = true;
			_held[actor] = at;
			return actor;
		}

		private void Let(Actor a)
		{
			if (a == null)
			{
				return;
			}
			_held.Remove(a);
			try
			{
				if (a.isAlive())
				{
					a.current_position = _home + new Vector2(-6f, -6f);
					a.dirty_current_tile = true;
				}
			}
			catch
			{
			}
		}

		private void Hold()
		{
			foreach (KeyValuePair<Actor, Vector2> item in _held)
			{
				Actor key = item.Key;
				try
				{
					if (key != null && key.isAlive() && key != Mod.Host)
					{
						key.cancelAllBeh();
						key.makeWait(5f);
						key.current_position = item.Value;
					}
				}
				catch
				{
				}
			}
		}

		private IEnumerator Settle()
		{
			for (int i = 0; i < 6; i++)
			{
				Hold();
				yield return null;
			}
		}

		private void Home()
		{
			if (_host != null && _host.isAlive())
			{
				_host.current_position = _home;
				_host.dirty_current_tile = true;
			}
		}

		private Vector2 Ahead(float dist, float side)
		{
			Vector3 eyeForTest = Mod.EyeForTest;
			float viewYaw = Mod.ViewYaw;
			float num = Mathf.Cos(viewYaw);
			float num2 = Mathf.Sin(viewYaw);
			return new Vector2(eyeForTest.x + num * dist + num2 * side, eyeForTest.y + num2 * dist - num * side);
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

		private float ClearYaw(out int clear)
		{
			Vector3 eyeForTest = Mod.EyeForTest;
			TerrainCache terrainForTest = Mod.TerrainForTest;
			float num = Mod.ViewYaw;
			clear = 0;
			for (int i = 0; i < 32; i++)
			{
				float num2 = (float)i * MathF.PI * 2f / 32f;
				float num3 = Mathf.Cos(num2);
				float num4 = Mathf.Sin(num2);
				int num5 = 0;
				for (int j = 2; j <= 14; j++)
				{
					float num6 = eyeForTest.x + num3 * (float)j;
					float num7 = eyeForTest.y + num4 * (float)j;
					WorldTile tile = World.world.GetTile((int)num6, (int)num7);
					if (tile == null || tile.Type == null || tile.Type.liquid || tile.Type.block || (tile.building != null && AssetStyle.IsSolid(tile.building)))
					{
						break;
					}
					float num8 = terrainForTest.SampleHeight(num6, num7) + 0.4f;
					bool flag = true;
					for (float num9 = 0.5f; num9 < (float)j && flag; num9 += 0.5f)
					{
						if (terrainForTest.SampleHeight(eyeForTest.x + num3 * num9, eyeForTest.y + num4 * num9) > eyeForTest.z + (num8 - eyeForTest.z) * num9 / (float)j - 0.05f)
						{
							flag = false;
						}
					}
					if (!flag)
					{
						break;
					}
					num5 = j;
				}
				if (num5 > clear)
				{
					clear = num5;
					num = num2;
				}
			}
			Mod.SetYaw(num);
			return num;
		}

		private static int Ended()
		{
			return FirstPersonShots.Struck + FirstPersonShots.Grounded + FirstPersonShots.Walled;
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

		private string Side(Actor a)
		{
			if (a == null)
			{
				return "";
			}
			try
			{
				string text = ((a.kingdom == _host.kingdom) ? "ours" : ((a.kingdom != null && a.kingdom.isEnemy(_host.kingdom)) ? "enemy" : "other"));
				string text2 = ((a.profession_asset != null) ? a.profession_asset.id : "-");
				return " [" + text + ", " + text2 + ((a.has_attack_target && a.attack_target == _host) ? ", after you" : "") + "]";
			}
			catch
			{
				return "";
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
				Log.Error("FRIENDLY equip " + id + ": " + ex.Message);
				return false;
			}
		}
	}
}
