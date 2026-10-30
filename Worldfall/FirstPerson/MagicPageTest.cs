using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class MagicPageTest : CivilTest
	{
		private Actor _host;

		protected override string Prefix => "MAGIC";

		protected override IEnumerator Body()
		{
			Actor actor = Village("human", 10, 0);
			if (!Check("a village", actor != null))
			{
				yield break;
			}
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive() && item != actor && item.isAdult() && !item.isWarrior())
				{
					_host = item;
					break;
				}
			}
			if (Check("a villager", _host != null))
			{
				try
				{
					((BaseSimObject)_host).addStatusEffect("invincible", 300f, pColorEffect: false);
				}
				catch
				{
				}
				yield return Possess(_host);
				if (Check("first person on a villager", InControl(_host)))
				{
					yield return Part("traits", Traits());
					yield return Part("spells", Spells());
					Abilities.SpellsForTest.Clear();
				}
			}
		}

		private IEnumerator Traits()
		{
			Check("nothing earned yet", TraitSwitch.EarnedOf(_host).Count == 0);
			List<string> born = new List<string>();
			foreach (ActorTrait trait in _host.getTraits())
			{
				born.Add(trait.id);
			}
			Note("born with: " + string.Join(", ", born.ToArray()));
			string id = null;
			string[] decentTraits = Quests.DecentTraits;
			foreach (string text in decentTraits)
			{
				if (!_host.hasTrait(text))
				{
					id = text;
					break;
				}
			}
			if (Check("a decent trait to earn", id != null))
			{
				bool flag = _host.addTrait(AssetManager.traits.get(id), pRemoveOpposites: true);
				TraitSwitch.Earned(_host, id);
				Check("earned " + id, flag && TraitSwitch.IsEarned(_host, id) && _host.hasTrait(id));
				Check("switched off: gone from the body, still yours", TraitSwitch.Set(_host, id, on: false) && !_host.hasTrait(id) && TraitSwitch.Owns(_host, id));
				yield return CivilTest.Wait(0.3f);
				Check("switched on again", TraitSwitch.Set(_host, id, on: true) && _host.hasTrait(id));
				Check("again off and on, as often as you like", TraitSwitch.Set(_host, id, on: false) && TraitSwitch.Set(_host, id, on: true) && _host.hasTrait(id));
				if (born.Count > 0)
				{
					string text2 = born[0];
					Check("a born trait (" + text2 + ") doesn't switch", !TraitSwitch.Set(_host, text2, on: false) && _host.hasTrait(text2));
				}
				TraitSwitch.Set(_host, id, on: false);
				Check("an earned trait switched off isn't offered again", !Quests.TraitsToPick(_host, Quests.DecentTraits).Contains(id));
				TraitSwitch.Set(_host, id, on: true);
				Mod.Craft.Open = true;
				Mod.Craft.Tab = 3;
				yield return CivilTest.Wait(1f);
				Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "magic_page.png"));
				yield return CivilTest.Wait(0.6f);
				Check("the Magic tab stays open", Mod.Craft.Open && Mod.Craft.Tab == 3);
				Mod.Craft.Open = false;
				Mod.Craft.Tab = 0;
				yield return CivilTest.Wait(0.3f);
			}
		}

		private IEnumerator Spells()
		{
			string[] array = new string[2] { "human", "orc" };
			foreach (string kind in array)
			{
				Actor a = CivilTest.SpawnNear(kind, _host.current_position, 6f);
				yield return CivilTest.Wait(0.3f);
				if (a != null)
				{
					try
					{
						a.setStatsDirty();
					}
					catch
					{
					}
					yield return CivilTest.Wait(0.3f);
					Note("a " + kind + " knows: " + string.Join(", ", Abilities.Known(a).ConvertAll((Ability x) => x.Name + ((x.SpellId != null) ? (" (" + x.SpellId + ")") : "")).ToArray()));
					CivilTest.Kill(a);
				}
			}
			Abilities.SpellsForTest.Clear();
			Abilities.SpellsForTest.Add("cast_fire");
			Abilities.SpellsForTest.Add("cast_shield");
			List<Ability> list = Abilities.Known(_host);
			Ability fire = list.Find((Ability x) => x.SpellId == "cast_fire");
			Ability shield = list.Find((Ability x) => x.SpellId == "cast_shield");
			Check("the spells known are listed", fire != null && shield != null, string.Join(", ", list.ConvertAll((Ability x) => x.Name).ToArray()));
			if (fire == null || shield == null)
			{
				yield break;
			}
			Abilities.Choose(_host, fire);
			yield return CivilTest.Wait(0.4f);
			Check("Set Ablaze put on X", Mod.Ability == fire, Mod.Ability?.Name);
			try
			{
				_host.restoreMana(999);
				_host.restoreStamina(999);
			}
			catch
			{
			}
			Vector2 at = _host.current_position + new Vector2(5f, 0f);
			Actor wolf = CivilTest.Spawn("wolf", at);
			yield return CivilTest.Wait(0.4f);
			if (!Check("a wolf to cast it on", wolf != null))
			{
				yield break;
			}
			try
			{
				wolf.makeWait();
				wolf.addAggro(_host);
			}
			catch
			{
			}
			Mod.SetYaw(0f);
			Mod.SetPitchDegrees(-9f);
			float health = wolf.getHealth();
			int mana = _host.getMana();
			int stamina = _host.getStamina();
			yield return CivilTest.Wait(0.3f);
			if (CrownOffer.PendingForTest(out var crown))
			{
				Note("the game made this body a town's leader: the offer declined");
				CrownOffer.AnswerForTest(yes: false);
			}
			if (Mod.Converse.Open)
			{
				Note("a talk came up on its own: ended, or X does nothing");
				Mod.Converse.End();
				yield return CivilTest.Wait(0.2f);
			}
			Mod.PressAbilityForTest(down: true);
			for (float t = 0f; t < 0.5f; t += 0.1f)
			{
				wolf.current_position = at;
				yield return CivilTest.Wait(0.1f);
			}
			Mod.PressAbilityForTest(down: false);
			yield return CivilTest.Wait(0.6f);
			Check("it cost mana or stamina", _host.getMana() < mana || _host.getStamina() < stamina, $"mana {mana} -> {_host.getMana()}, stamina {stamina} -> {_host.getStamina()}");
			Check("then it cools down", Abilities.CooldownLeft(_host) > 0f, Abilities.CooldownLeft(_host).ToString("F1"));
			bool burning = false;
			for (float t = 0f; t < 4f; t += 0.2f)
			{
				try
				{
					burning = ((BaseSimObject)wolf).hasStatus("burning");
				}
				catch
				{
				}
				if (burning || (float)wolf.getHealth() < health || !wolf.isAlive())
				{
					break;
				}
				yield return CivilTest.Wait(0.2f);
			}
			Mod.SaveFrame("magic_cast.png");
			Check("the wolf catches fire (or is hurt)", burning || (float)wolf.getHealth() < health || !wolf.isAlive(), $"burning {burning}, health {health} -> {wolf.getHealth()}");
			CivilTest.Kill(wolf);
			Abilities.Choose(_host, shield);
			yield return CivilTest.Wait(Mathf.Max(0.4f, Abilities.CooldownLeft(_host) + 0.3f));
			Check("Shield put on X", Mod.Ability == shield, Mod.Ability?.Name);
			try
			{
				_host.restoreMana(999);
				_host.restoreStamina(999);
			}
			catch
			{
			}
			if (CrownOffer.PendingForTest(out crown))
			{
				Note("the game made this body a town's leader: the offer declined");
				CrownOffer.AnswerForTest(yes: false);
			}
			if (Mod.Converse.Open)
			{
				Note("a talk came up on its own: ended, or X does nothing");
				Mod.Converse.End();
				yield return CivilTest.Wait(0.2f);
			}
			Mod.PressAbilityForTest(down: true);
			yield return CivilTest.Wait(0.1f);
			Mod.PressAbilityForTest(down: false);
			yield return CivilTest.Wait(0.6f);
			string text = "";
			try
			{
				foreach (string statusesId in _host.getStatusesIds())
				{
					text = text + statusesId + " ";
				}
			}
			catch
			{
			}
			Check("shielded", text.Contains("shield"), text);
			Abilities.SpellsForTest.Clear();
			yield return CivilTest.Wait(3.5f);
			Check("unlearned spells drop off X", Mod.Ability == null || Mod.Ability.SpellId == null || Abilities.Known(_host).Contains(Mod.Ability), Mod.Ability?.Name);
		}
	}
}
