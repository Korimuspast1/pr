using System;
using System.Collections;
using System.Collections.Generic;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class MonsterLawTest : CivilTest
	{
		private Actor _host;

		private Actor _kingA;

		private Actor _kingB;

		private Kingdom _realmA;

		private Kingdom _realmB;

		private readonly List<Actor> _ours = new List<Actor>();

		private readonly List<Actor> _theirs = new List<Actor>();

		private readonly Dictionary<Actor, long> _ids = new Dictionary<Actor, long>();

		private readonly HashSet<Actor> _used = new HashSet<Actor>();

		private readonly Dictionary<Actor, Vector2> _held = new Dictionary<Actor, Vector2>();

		private readonly Dictionary<Actor, long> _heldIds = new Dictionary<Actor, long>();

		private readonly Dictionary<Actor, Actor> _fightsWith = new Dictionary<Actor, Actor>();

		private Vector2 _home;

		private float _lane;

		protected override string Prefix => "MLAW";

		protected override IEnumerator Body()
		{
			_kingA = Village("human", 18, 0);
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
			if (!Check("a neighbour kingdom a way off, at peace with yours", _kingB != null && _kingB.kingdom != _kingA.kingdom && !_kingB.kingdom.isEnemy(_kingA.kingdom)))
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
			_realmA = _kingA.kingdom;
			_realmB = _kingB.kingdom;
			foreach (Actor our in _ours)
			{
				_ids[our] = our.data.id;
			}
			foreach (Actor their in _theirs)
			{
				_ids[their] = their.data.id;
			}
			_host = Pick(_ours, civilian: true);
			Actor[] array = new Actor[2] { _kingA, _kingB };
			foreach (Actor actor in array)
			{
				try
				{
					((BaseSimObject)actor).addStatusEffect("invincible", 900f, pColorEffect: false);
					actor.current_position += new Vector2(0f, 14f);
					actor.dirty_current_tile = true;
				}
				catch
				{
				}
			}
			yield return Possess(_host);
			if (Check("first person in a villager", InControl(_host)))
			{
				yield return CivilTest.Wait(1f);
				_home = _host.current_position;
				_lane = ClearYaw(out var clear);
				Note($"lane {_lane * 57.29578f:F0} degrees, clear {clear} tiles; you {Who(_host)}{Side(_host)}; {_ours.Count} of ours, {_theirs.Count} of theirs");
				Mod.Settings.HeadBob = 0f;
				bool savedFire = Mod.Settings.FriendlyFire;
				yield return Part("a zombie among your townsfolk, friendly fire on", ZombieFight(_ours, friendlyFire: true, "sword_iron"));
				yield return Part("a zombie among your townsfolk, friendly fire off", ZombieFight(_ours, friendlyFire: false, "sword_iron"));
				yield return Part("a zombie among a neighbour town's folk", ZombieFight(_theirs, friendlyFire: false, "sword_iron"));
				yield return Part("a hammer's sweep at a zombie among your townsfolk", ZombieFight(_ours, friendlyFire: true, "hammer_iron"));
				yield return Part("a hammer's sweep at a zombie among a neighbour town's folk", ZombieFight(_theirs, friendlyFire: false, "hammer_iron"));
				yield return Part("an arrow at a zombie past the folk round it", ZombieArrow(_ours));
				yield return Part("a shotgun at a zombie past a neighbour town's folk", ZombieShotgun(_theirs));
				yield return Part("a zombie fight is no cover: a townsman struck on purpose", NoCover());
				yield return Part("a murder in town as a villager", Murder(soldier: false));
				yield return Part("a murder in town as a soldier", Murder(soldier: true));
				yield return Part("a soldier at war: an enemy soldier, then an enemy's villager", WarCrime());
				Mod.Settings.FriendlyFire = savedFire;
			}
		}

		private IEnumerator ZombieFight(List<Actor> folk, bool friendlyFire, string weapon)
		{
			Mod.Settings.FriendlyFire = friendlyFire;
			Calm();
			try
			{
				((BaseSimObject)_host).addStatusEffect("invincible", 40f, pColorEffect: false);
			}
			catch
			{
			}
			if (!Equip(_host, weapon))
			{
				Check("a " + weapon, ok: false);
				yield break;
			}
			yield return CivilTest.Wait(0.3f);
			Home();
			Mod.SetYaw(_lane);
			Mod.SetPitchDegrees(-10f);
			yield return Settle();
			Actor z = SpawnZombie(Ahead(1.05f, 0f));
			Actor actor = Place(Pick(folk, civilian: false), Ahead(1.15f, 0.4f));
			Actor actor2 = Place(Pick(folk, civilian: false), Ahead(1.15f, -0.4f));
			Actor actor3 = Place(Pick(folk, civilian: false), Ahead(1.5f, 0.05f));
			List<Actor> round = new List<Actor> { actor, actor2, actor3 };
			_fightsWith[z] = actor;
			if (!Check("a zombie and three people round it", z != null && actor != null && actor2 != null && actor3 != null, Who(z) + ", " + Who(actor) + Side(actor) + ", " + Who(actor2) + Side(actor2) + ", " + Who(actor3) + Side(actor3)))
			{
				yield break;
			}
			Kingdom k = actor.kingdom;
			yield return CivilTest.Wait(0.4f);
			yield return Settle();
			List<string> seen = new List<string>();
			int onZombie = 0;
			int onFolk = 0;
			for (int blow = 0; blow < 8; blow++)
			{
				if (!z.isAlive())
				{
					break;
				}
				Home();
				Face(z);
				yield return Settle();
				Face(z);
				try
				{
					z.setHealth(z.getMaxHealth());
				}
				catch
				{
				}
				float zh = Health(z);
				Dictionary<Actor, float> before = new Dictionary<Actor, float>();
				foreach (Actor item in round)
				{
					before[item] = Health(item);
				}
				string met = Blow() + "; zombie going for " + ((z.attack_target is Actor a) ? Who(a) : "nobody");
				for (int i = 0; i < 12; i++)
				{
					Hold();
					yield return null;
				}
				if (Health(z) < zh || met.Contains(Who(z) + " Hit"))
				{
					onZombie++;
				}
				foreach (Actor item2 in round)
				{
					if (HurtByYou(item2, before[item2]))
					{
						onFolk++;
					}
				}
				seen.Add($"blow {blow + 1} at {Who(PossessionCombat.LastBlowAt)}: [{met}]");
			}
			foreach (string item3 in seen)
			{
				Note(item3);
			}
			Check("your blows land on the zombie", onZombie >= 4, $"{onZombie} of 8");
			Check("...never on the people round it", onFolk == 0, $"{onFolk} hits on them");
			Check("no crime charged", Law.LastCrime == null && Law.BountyOn(k, _host) == 0, string.Format("the law: {0}, bounty in {1}: {2}", Law.LastCrime ?? "-", k?.name, Law.BountyOn(k, _host)));
			CivilTest.Kill(z);
			foreach (Actor item4 in round)
			{
				Let(item4);
			}
			string text = null;
			for (float zh = 0f; zh < 5f; zh += 0.25f)
			{
				if (text != null)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
				text = AfterYou(k);
			}
			Check("nobody of theirs turns on you afterwards", text == null, text ?? "nobody");
			Mod.Settings.FriendlyFire = false;
		}

		private IEnumerator ZombieArrow(List<Actor> folk)
		{
			Mod.Settings.FriendlyFire = true;
			Calm();
			try
			{
				((BaseSimObject)_host).addStatusEffect("invincible", 40f, pColorEffect: false);
			}
			catch
			{
			}
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
			bool folkHit = false;
			bool flag = false;
			string text = "";
			Actor p1 = Pick(folk, civilian: false);
			Actor p2 = Pick(folk, civilian: false);
			Actor p3 = Pick(folk, civilian: false);
			Kingdom k = p1?.kingdom;
			for (int i = 1; i <= 3; i++)
			{
				if (flag)
				{
					break;
				}
				if (folkHit)
				{
					break;
				}
				Home();
				Mod.SetYaw(_lane);
				Mod.SetPitchDegrees(0f);
				yield return Settle();
				Actor z = SpawnZombie(Ahead(7f, 0f));
				if (z == null)
				{
					Check("a zombie", ok: false);
					yield break;
				}
				Actor actor = Place(p1, Ahead(6.6f, 0.3f));
				Actor item = Place(p2, Ahead(6.6f, -0.3f));
				Actor item2 = Place(p3, Ahead(4.5f, 0.25f));
				List<Actor> round = new List<Actor> { actor, item, item2 };
				_fightsWith[z] = actor;
				yield return CivilTest.Wait(0.4f);
				yield return Settle();
				Face(z);
				yield return Settle();
				try
				{
					z.setHealth(z.getMaxHealth());
				}
				catch
				{
				}
				float zh = Health(z);
				Dictionary<Actor, float> before = new Dictionary<Actor, float>();
				foreach (Actor item3 in round)
				{
					if (item3 != null)
					{
						before[item3] = Health(item3);
					}
				}
				int ended = FirstPersonShots.Struck + FirstPersonShots.Grounded + FirstPersonShots.Walled;
				FirstPersonShots.Trace = true;
				Mod.Bow.LooseForTest(_host);
				for (float t = 0f; t < 3f; t += 0.02f)
				{
					if (FirstPersonShots.Struck + FirstPersonShots.Grounded + FirstPersonShots.Walled != ended)
					{
						break;
					}
					Hold();
					yield return CivilTest.Wait(0.02f);
				}
				FirstPersonShots.Trace = false;
				yield return CivilTest.Wait(0.2f);
				foreach (Actor item4 in round)
				{
					if (item4 != null && before.ContainsKey(item4) && HurtByYou(item4, before[item4]))
					{
						folkHit = true;
					}
				}
				flag = FirstPersonShots.LastVictim == z && Health(z) < zh;
				text = string.Format("try {0}: crosshair on {1}, the arrow ended at {2} in {3}, passed {4}", i, (Mod.Picked == z) ? "the zombie" : ((Mod.Picked is Actor a) ? Who(a) : "nothing"), FirstPersonShots.LastWhat, Who(FirstPersonShots.LastVictim), Who(FirstPersonShots.LastPassed));
				Note(text);
				CivilTest.Kill(z);
				foreach (Actor item5 in round)
				{
					Let(item5);
				}
			}
			Check("an arrow at the zombie flies past the people round it", !folkHit, text);
			Check("...into the zombie", flag, text);
			Check("no crime charged", Law.LastCrime == null && (k == null || Law.BountyOn(k, _host) == 0), "the law: " + (Law.LastCrime ?? "-"));
			string text2 = null;
			for (float zh = 0f; zh < 3f; zh += 0.25f)
			{
				if (text2 != null)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
				text2 = AfterYou(k);
			}
			Check("nobody of theirs turns on you afterwards", text2 == null, text2 ?? "nobody");
			Mod.Settings.FriendlyFire = false;
		}

		private IEnumerator ZombieShotgun(List<Actor> folk)
		{
			Mod.Settings.FriendlyFire = false;
			Calm();
			try
			{
				((BaseSimObject)_host).addStatusEffect("invincible", 40f, pColorEffect: false);
			}
			catch
			{
			}
			if (!Equip(_host, "shotgun"))
			{
				Check("a shotgun", ok: false);
				yield break;
			}
			ShotgunControl gun = Mod.Shotgun;
			for (int i = 0; i < 60; i++)
			{
				if (gun.InHand)
				{
					break;
				}
				yield return null;
			}
			for (float t = 0f; t < 3f; t += 0.05f)
			{
				if (gun.State.State == ShotgunState.Phase.Ready)
				{
					break;
				}
				yield return CivilTest.Wait(0.05f);
			}
			Home();
			Mod.SetYaw(_lane);
			Mod.SetPitchDegrees(0f);
			yield return Settle();
			Actor z = SpawnZombie(Ahead(4.5f, 0f));
			Actor actor = Place(Pick(folk, civilian: false), Ahead(4.4f, 0.55f));
			Actor actor2 = Place(Pick(folk, civilian: false), Ahead(4.4f, -0.55f));
			Actor actor3 = Place(Pick(folk, civilian: false), Ahead(5.2f, 0.3f));
			List<Actor> round = new List<Actor> { actor, actor2, actor3 };
			_fightsWith[z] = actor;
			if (!Check("a zombie and three people round it", z != null && actor != null && actor2 != null && actor3 != null))
			{
				yield break;
			}
			Kingdom k = actor.kingdom;
			yield return CivilTest.Wait(0.4f);
			yield return Settle();
			Face(z);
			yield return Settle();
			try
			{
				z.setHealth(z.getMaxHealth());
			}
			catch
			{
			}
			float zh = Health(z);
			Dictionary<Actor, float> before = new Dictionary<Actor, float>();
			foreach (Actor item in round)
			{
				before[item] = Health(item);
			}
			int launched = FirstPersonShots.PelletsLaunched;
			Mod.Melee.PressForTest();
			gun.ClickForTest();
			for (float t = 0f; t < 1.5f; t += 0.02f)
			{
				Hold();
				yield return CivilTest.Wait(0.02f);
			}
			int num = 0;
			foreach (Actor item2 in round)
			{
				if (HurtByYou(item2, before[item2]))
				{
					num++;
				}
			}
			string detail = string.Format("{0} pellets, zombie {1}->{2}, folk {3}", FirstPersonShots.PelletsLaunched - launched, zh, Health(z), string.Join(", ", round.ConvertAll((Actor a) => before[a] + "->" + Health(a))));
			Check("the pellets find the zombie", Health(z) < zh, detail);
			Check("...and pass the people round it", num == 0, detail);
			Check("no crime charged", Law.LastCrime == null && Law.BountyOn(k, _host) == 0, "the law: " + (Law.LastCrime ?? "-"));
			CivilTest.Kill(z);
			foreach (Actor item3 in round)
			{
				Let(item3);
			}
			string text = null;
			for (float t = 0f; t < 3f; t += 0.25f)
			{
				if (text != null)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
				text = AfterYou(k);
			}
			Check("nobody of theirs turns on you afterwards", text == null, text ?? "nobody");
		}

		private IEnumerator NoCover()
		{
			Mod.Settings.FriendlyFire = true;
			Calm();
			try
			{
				((BaseSimObject)_host).addStatusEffect("invincible", 40f, pColorEffect: false);
			}
			catch
			{
			}
			if (!Equip(_host, "sword_iron"))
			{
				Check("a sword", ok: false);
				yield break;
			}
			yield return CivilTest.Wait(0.3f);
			Home();
			Mod.SetYaw(_lane);
			Mod.SetPitchDegrees(-10f);
			yield return Settle();
			Actor z = SpawnZombie(Ahead(1.4f, 0f));
			Actor victim = Place(Pick(_ours, civilian: false), Ahead(0.8f, 0.05f));
			Actor witness = Place(Pick(_ours, civilian: false), Ahead(3f, -2.5f));
			if (!Check("a townsman just before a zombie, another looking on", z != null && victim != null && witness != null))
			{
				yield break;
			}
			_fightsWith[z] = witness;
			try
			{
				victim.setHealth(victim.getMaxHealth());
			}
			catch
			{
			}
			for (int i = 0; i < 24; i++)
			{
				Hold();
				yield return null;
			}
			Home();
			ClearAround();
			Face(victim);
			yield return Settle();
			Face(victim);
			float h0 = Health(victim);
			Hold();
			string met = Blow();
			for (int i = 0; i < 20; i++)
			{
				Hold();
				yield return null;
			}
			string text = null;
			try
			{
				text = ((victim.attackedBy is Actor a) ? Who(a) : null);
			}
			catch
			{
			}
			Check("a townsman in front of a zombie in a fight: the blow passes him by", !HurtByYou(victim, h0) && PossessionCombat.LastBlowAt != victim, string.Format("blow at {0}, met [{1}], townsman {2}->{3}, last hit by {4}", Who(PossessionCombat.LastBlowAt), met, h0, Health(victim), text ?? "-"));
			Check("...no crime", Law.LastCrime == null, "the law: " + (Law.LastCrime ?? "-"));
			Place(z, Ahead(2.4f, 1.8f));
			try
			{
				victim.setHealth(victim.getMaxHealth());
			}
			catch
			{
			}
			for (int i = 0; i < 18; i++)
			{
				Hold();
				yield return null;
			}
			Home();
			ClearAround();
			Face(victim);
			yield return Settle();
			Face(victim);
			h0 = Health(victim);
			Hold();
			met = Blow();
			for (int i = 0; i < 20; i++)
			{
				Hold();
				yield return null;
			}
			Check("with the zombie off to the side, a blow aimed at the townsman lands", Health(victim) < h0 || !victim.isAlive(), $"blow at {Who(PossessionCombat.LastBlowAt)}, met [{met}], townsman {h0}->{Health(victim)}");
			yield return CivilTest.Wait(0.3f);
			Check("...and is a crime, fight or no fight", Law.LastCrime == "assault" || Law.LastCrime == "murder", string.Format("the law: {0}, seen by {1}", Law.LastCrime ?? "-", Law.LastWitnesses));
			CivilTest.Kill(z);
			try
			{
				if (victim.isAlive())
				{
					victim.setHealth(victim.getMaxHealth());
				}
			}
			catch
			{
			}
			Let(victim);
			Let(witness);
			Calm();
			Mod.Settings.FriendlyFire = false;
		}

		private IEnumerator Murder(bool soldier)
		{
			Mod.Settings.FriendlyFire = true;
			Calm();
			try
			{
				_host.finishStatusEffect("invincible");
			}
			catch
			{
			}
			if (soldier)
			{
				bool flag = Service.Enlist(_host);
				if (!Check("enlisted as a soldier of your town", flag && _host.isWarrior(), string.Format("warrior {0}, army {1}", _host.isWarrior(), (_host.army != null) ? _host.army.name : "none")))
				{
					yield break;
				}
				yield return CivilTest.Wait(0.5f);
			}
			if (!Equip(_host, "sword_iron"))
			{
				Check("a sword", ok: false);
				yield break;
			}
			yield return CivilTest.Wait(0.3f);
			Home();
			Mod.SetYaw(_lane);
			Mod.SetPitchDegrees(-10f);
			yield return Settle();
			Actor victim = Place(Pick(_ours, civilian: true), Ahead(1f, 0f));
			List<Actor> guards = new List<Actor>();
			for (int i = 0; i < 3; i++)
			{
				Actor actor = Place(Pick(_ours, civilian: false, warrior: true), Ahead(4f, -3f + 3f * (float)i));
				if (actor != null)
				{
					guards.Add(actor);
				}
			}
			if (!Check("a townsman before you, warriors of the town looking on", victim != null && guards.Count > 0, $"{Who(victim)}{Side(victim)}, {guards.Count} warriors"))
			{
				yield break;
			}
			Kingdom k = victim.kingdom;
			yield return CivilTest.Wait(0.4f);
			yield return Settle();
			bool flag2 = false;
			string met = "";
			for (int attempt = 0; attempt < 3; attempt++)
			{
				if (flag2)
				{
					break;
				}
				Home();
				ClearAround();
				Face(victim);
				yield return Settle();
				Face(victim);
				try
				{
					victim.setHealth(1);
				}
				catch
				{
				}
				met = Blow();
				for (int j = 0; j < 20; j++)
				{
					Hold();
					yield return null;
				}
				flag2 = !victim.isAlive();
			}
			Check("the townsman falls to your blow", flag2, "the blow met [" + met + "]");
			yield return CivilTest.Wait(0.5f);
			int num = Law.BountyOn(k, _host);
			Check("charged with murder", Law.LastCrime == "murder" && num >= 100, string.Format("the law: {0}, seen by {1}, bounty in {2}: {3}", Law.LastCrime ?? "-", Law.LastWitnesses, k.name, num));
			foreach (Actor item in guards)
			{
				Let(item, near: true);
			}
			string after = null;
			int struck = 0;
			int hp = _host.getHealth();
			for (float t = 0f; t < 10f; t += 0.1f)
			{
				if (struck != 0)
				{
					break;
				}
				yield return CivilTest.Wait(0.1f);
				after = after ?? AfterYou(k);
				int health = _host.getHealth();
				if (health < hp)
				{
					try
					{
						if (_host.attackedBy is Actor actor2 && actor2.kingdom == k)
						{
							struck++;
						}
					}
					catch
					{
					}
				}
				if (health < _host.getMaxHealth() / 2)
				{
					try
					{
						_host.setHealth(_host.getMaxHealth());
					}
					catch
					{
					}
				}
				hp = _host.getHealth();
			}
			Check("the town's warriors come for you", after != null, after ?? "nobody");
			Check("...and strike you", struck > 0, $"{struck} blows from them");
			if (soldier)
			{
				for (float t = 0f; t < 2f; t += 0.1f)
				{
					if (!_host.isWarrior())
					{
						break;
					}
					yield return CivilTest.Wait(0.1f);
				}
				Check("a soldier wanted in his own realm is discharged", !_host.isWarrior() && !Service.IsSoldier(_host), string.Format("warrior {0}, army {1}", _host.isWarrior(), (_host.army != null) ? _host.army.name : "none"));
				string text = Service.EnlistRefusal(_realmA.king ?? _kingA, _host);
				Check("...and may not enlist while wanted", text != null, text ?? "accepted");
				Law.SetBounty(_realmA, _host, 0);
				text = Service.EnlistRefusal(_realmA.king ?? _kingA, _host);
				Check("...but may once the fine is paid", text == null || !text.Contains("price on your head"), text ?? "accepted");
			}
			Calm();
			yield return CivilTest.Wait(0.5f);
			Mod.Settings.FriendlyFire = false;
		}

		private IEnumerator WarCrime()
		{
			Kingdom a = _realmA;
			Kingdom b = _realmB;
			Calm();
			if (!_host.isWarrior())
			{
				Service.Enlist(_host);
			}
			if (!Check("a soldier again", Service.IsSoldier(_host)))
			{
				yield break;
			}
			try
			{
				World.world.diplomacy.startWar(b, a, WarTypeLibrary.normal, pLog: false);
			}
			catch (Exception ex)
			{
				Note("war: " + ex.Message);
			}
			yield return CivilTest.Wait(0.5f);
			if (!Check("the kingdoms are at war", a.isEnemy(b)))
			{
				yield break;
			}
			Mod.Settings.FriendlyFire = true;
			try
			{
				((BaseSimObject)_host).addStatusEffect("invincible", 60f, pColorEffect: false);
			}
			catch
			{
			}
			if (!Equip(_host, "sword_iron"))
			{
				Check("a sword", ok: false);
				yield break;
			}
			yield return CivilTest.Wait(0.3f);
			Home();
			Mod.SetYaw(_lane);
			Mod.SetPitchDegrees(-10f);
			yield return Settle();
			Actor soldier = Place(Pick(_theirs, civilian: false, warrior: true), Ahead(1f, 0f));
			List<Actor> list = new List<Actor>();
			foreach (Actor item in Civil.SubjectsOf(b))
			{
				if (item != null && item != soldier && item.isAlive() && item.isWarrior() && (item.current_position - _home).sqrMagnitude < 900f)
				{
					list.Add(item);
				}
			}
			foreach (Actor item2 in list)
			{
				CivilTest.Kill(item2);
			}
			if (!Check("an enemy soldier before you", soldier?.isWarrior() ?? false, Who(soldier) + Side(soldier)))
			{
				yield break;
			}
			int kills = Service.Kills(_host);
			Equip(soldier, "sword_adamantine");
			long mine = -1L;
			try
			{
				mine = _host.equipment.weapon.getItem().data.id;
			}
			catch
			{
			}
			yield return KillBefore(soldier);
			Check("the enemy soldier falls", !soldier.isAlive());
			long num = -2L;
			try
			{
				num = _host.equipment.weapon.getItem().data.id;
			}
			catch
			{
			}
			Check("...and your own sword is still in your hand (the kill took nothing from you)", mine >= 0 && num == mine && TakeGuard.Installed, $"weapon {mine} -> {num}, guard {TakeGuard.Installed}, kept on bodies {TakeGuard.Kept}");
			yield return CivilTest.Wait(0.4f);
			Check("...no crime", Law.LastCrime == null && Law.BountyOn(b, _host) == 0, string.Format("the law: {0}, bounty in {1}: {2}", Law.LastCrime ?? "-", b.name, Law.BountyOn(b, _host)));
			Check("...a foe felled, toward a captaincy", Service.Kills(_host) == kills + 1, $"{kills} -> {Service.Kills(_host)}");
			Calm();
			Actor victim = Place(Pick(_theirs, civilian: true), Ahead(1f, 0f));
			Actor theirs = Place(Pick(_theirs, civilian: true), Ahead(4f, 2f));
			Actor ours = Place(Pick(_ours, civilian: false), Ahead(4f, -2f));
			bool flag = false;
			try
			{
				flag = victim != null && !victim.isWarrior() && !victim.isKing() && !victim.isCityLeader();
			}
			catch
			{
			}
			if (!Check("an enemy's villager before you, one of theirs and one of yours looking on", flag && theirs != null && ours != null, Who(victim) + Side(victim) + ", " + Who(theirs) + Side(theirs) + ", " + Who(ours) + Side(ours)))
			{
				yield break;
			}
			int standing = Rep.StandingPoints(_host, a);
			kills = Service.Kills(_host);
			yield return KillBefore(victim);
			Check("the villager falls", !victim.isAlive());
			yield return CivilTest.Wait(0.4f);
			int bounty = Law.BountyOn(b, _host);
			Check("a war crime: wanted in their realm", Law.LastWar && Law.LastCrime == "murder" && bounty >= 100, string.Format("the law: {0}{1}, seen by {2} of theirs, bounty in {3}: {4}", Law.LastCrime ?? "-", Law.LastWar ? " (war)" : "", Law.LastWitnesses, b.name, bounty));
			Check("...your own realm doesn't hold its war against you", Law.LastOwnWitnesses == 0 && Rep.StandingPoints(_host, a) >= standing, $"{Law.LastOwnWitnesses} of yours counted, standing in {a.name} {standing} -> {Rep.StandingPoints(_host, a)}");
			Check("...no foe felled", Service.Kills(_host) == kills, $"{kills} -> {Service.Kills(_host)}");
			Check("...and still a soldier (wanted abroad, not at home)", Service.IsSoldier(_host));
			Actor foe = Place(Pick(_theirs, civilian: false, warrior: true), Ahead(1f, 0f));
			if (foe != null)
			{
				Home();
				ClearAround();
				Face(foe);
				yield return Settle();
				Face(foe);
				Note("blow in the war at " + Who(foe) + ": " + Blow());
				for (int i = 0; i < 20; i++)
				{
					Hold();
					yield return null;
				}
			}
			int warBounty = Law.BountyOn(b, _host);
			for (int j = 0; j < 8; j++)
			{
				War war = null;
				try
				{
					war = World.world.wars.getWar(a, b, pOnlyMain: false);
				}
				catch
				{
				}
				if (war == null)
				{
					break;
				}
				try
				{
					World.world.wars.endWar(war, WarWinner.Peace);
				}
				catch (Exception ex2)
				{
					Note("peace: " + ex2.Message);
					break;
				}
			}
			yield return CivilTest.Wait(0.5f);
			Check("peace made", !a.isEnemy(b));
			Check("...and their realm still wants you", Law.BountyOn(b, _host) >= bounty, $"bounty in {b.name}: {Law.BountyOn(b, _host)}");
			if (foe != null && foe.isAlive())
			{
				Law.LastCrime = null;
				Home();
				Face(foe);
				yield return Settle();
				Face(foe);
				Note("blow after the peace at " + Who(foe) + ": " + Blow());
				for (int i = 0; i < 20; i++)
				{
					Hold();
					yield return null;
				}
				Check("a blow in a fight begun in the war, just after the peace: no crime", Law.LastCrime == null && Law.BountyOn(b, _host) == warBounty, string.Format("the law: {0}, bounty in {1} {2} -> {3}", Law.LastCrime ?? "-", b.name, warBounty, Law.BountyOn(b, _host)));
				yield return CivilTest.Wait(1.5f);
				bool flag2 = false;
				try
				{
					flag2 = foe.isAlive() && foe.attack_target == _host;
				}
				catch
				{
				}
				Check("...and they stand down at the peace", !flag2, Who(foe) + " target " + ((foe.isAlive() && foe.attack_target != null) ? "you" : "none"));
			}
			else
			{
				Note("(no enemy soldier left for the peace-in-a-fight check)");
			}
			Let(theirs);
			Let(ours);
			Mod.Settings.FriendlyFire = false;
		}

		private IEnumerator KillBefore(Actor victim)
		{
			for (int attempt = 0; attempt < 3; attempt++)
			{
				if (!victim.isAlive())
				{
					break;
				}
				Home();
				ClearAround();
				Face(victim);
				yield return Settle();
				Face(victim);
				try
				{
					victim.setHealth(1);
				}
				catch
				{
				}
				Note("blow at " + Who(victim) + ": " + Blow());
				for (int i = 0; i < 20; i++)
				{
					Hold();
					yield return null;
				}
			}
		}

		private Actor SpawnZombie(Vector2 at)
		{
			Actor actor = null;
			string[] array = new string[2] { "zombie", "zombie_human" };
			for (int i = 0; i < array.Length; i++)
			{
				actor = CivilTest.Spawn(array[i], at);
				if (actor != null)
				{
					break;
				}
			}
			if (actor == null)
			{
				return null;
			}
			actor.dirty_current_tile = true;
			try
			{
				WorldTile tile = World.world.GetTile((int)at.x, (int)at.y);
				if (tile != null)
				{
					actor.setCurrentTile(tile);
				}
			}
			catch
			{
			}
			_held[actor] = at;
			_heldIds[actor] = actor.data.id;
			return actor;
		}

		private Actor Pick(List<Actor> from, bool civilian, bool warrior = false)
		{
			for (int i = 0; i < 2; i++)
			{
				foreach (Actor item in from)
				{
					if (item != null && item.isAlive() && item != _host && !_used.Contains(item) && !item.isKing() && _ids.TryGetValue(item, out var value) && item.data.id == value && (item.kingdom == _realmA || item.kingdom == _realmB))
					{
						bool flag = true;
						try
						{
							flag = item.profession_asset == null || item.profession_asset.is_civilian;
						}
						catch
						{
						}
						if ((!(i == 0 && civilian) || flag) && (!(i == 0 && warrior) || item.isWarrior()))
						{
							_used.Add(item);
							return item;
						}
					}
				}
			}
			return null;
		}

		private string AfterYou(Kingdom k)
		{
			if (k == null)
			{
				return null;
			}
			List<string> list = new List<string>();
			foreach (Actor item in Civil.SubjectsOf(k))
			{
				if (item != null && item != _host && item.isAlive())
				{
					bool flag = false;
					bool flag2 = false;
					try
					{
						flag = item.has_attack_target && item.attack_target == _host;
					}
					catch
					{
					}
					try
					{
						flag2 = item.isInAggroList(_host);
					}
					catch
					{
					}
					if (flag || flag2)
					{
						list.Add(Who(item) + (flag ? " fighting you" : "") + (flag2 ? " angry" : ""));
					}
				}
			}
			if (list.Count != 0)
			{
				return string.Join(", ", list);
			}
			return null;
		}

		private void ClearAround()
		{
			Vector2 current_position = _host.current_position;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple.isAlive() && simple != _host && (!_held.ContainsKey(simple) || Gone(simple)) && !((simple.current_position - current_position).sqrMagnitude > 9f))
				{
					try
					{
						simple.cancelAllBeh();
						simple.current_position = _home + new Vector2(-7f, 7f);
						simple.dirty_current_tile = true;
					}
					catch
					{
					}
				}
			}
		}

		private void Calm()
		{
			Kingdom[] array = new Kingdom[2] { _realmA, _realmB };
			foreach (Kingdom kingdom in array)
			{
				if (kingdom != null)
				{
					try
					{
						Law.SetBounty(kingdom, _host, 0);
						Law.StandDown(kingdom, _host);
						Rep.Pardon(_host, kingdom, "test");
					}
					catch
					{
					}
				}
			}
			Law.LastCrime = null;
			Law.LastWitnesses = 0;
			_used.Clear();
			List<Actor> list = new List<Actor>();
			foreach (Actor key in _held.Keys)
			{
				if (key == null || !key.isAlive() || Gone(key))
				{
					list.Add(key);
				}
			}
			foreach (Actor item in list)
			{
				_held.Remove(item);
				_heldIds.Remove(item);
				_fightsWith.Remove(item);
			}
			List<Actor> list2 = new List<Actor>();
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple.isAlive() && simple != _host && !_held.ContainsKey(simple))
				{
					Kingdom kingdom2 = simple.kingdom;
					if (kingdom2 != null && !kingdom2.isCiv() && kingdom2.asset != null && kingdom2.asset.count_as_danger && (simple.current_position - _home).sqrMagnitude < 625f)
					{
						list2.Add(simple);
					}
				}
			}
			foreach (Actor item2 in list2)
			{
				Note("(a stray " + Who(item2) + " put away)");
				CivilTest.Kill(item2);
			}
			foreach (Actor simple2 in World.world.units.getSimpleList())
			{
				if (simple2 == null || !simple2.isAlive() || simple2 == _host || (simple2.kingdom != _realmA && simple2.kingdom != _realmB))
				{
					continue;
				}
				try
				{
					BaseSimObject baseSimObject = (simple2.has_attack_target ? simple2.attack_target : null);
					if (baseSimObject != null && (baseSimObject == _host || (baseSimObject.isActor() && baseSimObject.kingdom != null && baseSimObject.kingdom.isCiv())))
					{
						simple2.clearAttackTarget();
						simple2.cancelAllBeh();
					}
					simple2._aggression_targets?.Clear();
					simple2.finishStatusEffect("angry");
				}
				catch
				{
				}
			}
			try
			{
				_host.clearAttackTarget();
			}
			catch
			{
			}
		}

		private bool ByYou(Actor a)
		{
			try
			{
				return a.attackedBy == _host;
			}
			catch
			{
				return false;
			}
		}

		private bool HurtByYou(Actor a, float before)
		{
			if (a == null)
			{
				return false;
			}
			long value;
			long num = (_ids.TryGetValue(a, out value) ? value : (-1));
			if (a.isAlive() && (num < 0 || a.data.id == num))
			{
				if (Health(a) < before)
				{
					return ByYou(a);
				}
				return false;
			}
			if (num >= 0)
			{
				return Quests.YouKilled(num);
			}
			return false;
		}

		private string Blow()
		{
			FriendlyFire.Trace = new List<KeyValuePair<BaseSimObject, string>>();
			FirstPersonAim.Trace = new List<string>();
			string text = ((Mod.Picked is Actor a) ? Who(a) : ((Mod.Picked != null) ? "a building" : "nothing"));
			PossessionCombat.AttackForTest(_host);
			string text2 = Traced();
			string text3 = ((FirstPersonAim.Trace.Count == 0) ? "" : ("; aim: " + string.Join("; ", FirstPersonAim.Trace)));
			FirstPersonAim.Trace = null;
			return text2 + text3 + "; crosshair on " + text;
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
				string[] array = new string[3] { "block", "dodge", "deflect_projectile" };
				foreach (string pTraitID in array)
				{
					if (a.hasTrait(pTraitID))
					{
						a.removeTrait(pTraitID);
					}
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
			_heldIds[a] = a.data.id;
			return a;
		}

		private bool Gone(Actor a)
		{
			try
			{
				long value;
				return !_heldIds.TryGetValue(a, out value) || a.data.id != value;
			}
			catch
			{
				return true;
			}
		}

		private void Let(Actor a, bool near = false)
		{
			if (a == null)
			{
				return;
			}
			_held.Remove(a);
			if (near)
			{
				return;
			}
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
					if (key != null && key.isAlive() && key != Mod.Host && !Gone(key))
					{
						key.cancelAllBeh();
						key.makeWait(5f);
						key.current_position = item.Value;
						if (_fightsWith.TryGetValue(key, out var value) && value != null && value.isAlive())
						{
							key.setAttackTarget(value);
						}
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
				return " [" + text + ", " + text2 + "]";
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
				Log.Error("MLAW equip " + id + ": " + ex.Message);
				return false;
			}
		}
	}
}
