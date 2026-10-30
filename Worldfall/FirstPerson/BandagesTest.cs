using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class BandagesTest : CivilTest
	{
		private Actor _me;

		private Actor _other;

		private City _city;

		private readonly List<Actor> _people = new List<Actor>();

		protected override string Prefix => "BAND";

		private static Supplies.Kind BandageKind => Supplies.Of("tw_item_bandage");

		private static Supplies.Kind SalveKind => Supplies.Of("tw_item_salve");

		protected override IEnumerator Body()
		{
			Icons();
			Actor king = Village("human", 14, 0);
			if (!Check("a village", king != null))
			{
				yield break;
			}
			_city = king.city;
			yield return CivilTest.Wait(2f);
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive() && item != king && item.isAdult())
				{
					_people.Add(item);
				}
			}
			_me = _people.FirstOrDefault((Actor u) => !u.isWarrior()) ?? _people.FirstOrDefault();
			_other = _people.FirstOrDefault((Actor u) => u != _me && !u.isWarrior());
			if (!Check("villagers", _me != null && _other != null && _people.Count >= 4))
			{
				yield break;
			}
			foreach (Actor item2 in Founded)
			{
				try
				{
					((BaseSimObject)item2).addStatusEffect("invincible", 600f, pColorEffect: false);
				}
				catch
				{
				}
			}
			Stockpile();
			yield return Possess(_me);
			if (Check("first person", InControl(_me)))
			{
				Empty(_me);
				yield return Part("made by hand", Making());
				yield return Part("on yourself", OnYourself());
				yield return Part("on the hotbar", OnTheHotbar());
				yield return Part("on someone else", OnSomeone());
				yield return Part("F3 suits the wound", QuickKey());
				yield return Part("bought from a trader", Trader());
				yield return Part("carried and looted", Looted());
				yield return Part("dropped and picked up", Dropped());
				yield return Part("leaving your town", LeavingTown());
				Empty(_me);
			}
		}

		private IEnumerator Making()
		{
			Check("stacks: bandages 10, salves 5", Bag.Stack("tw_item_bandage") == 10 && Bag.Stack("tw_item_salve") == 5);
			Mod.Craft.EnsureRecipes();
			Crafting.Recipe recipe = Mod.Craft.Recipes.Find((Crafting.Recipe r) => r.Supply == BandageKind);
			Crafting.Recipe recipe2 = Mod.Craft.Recipes.Find((Crafting.Recipe r) => r.Supply == SalveKind);
			if (!Check("bandage and salve recipes", recipe != null && recipe2 != null))
			{
				yield break;
			}
			Check("a bandage is made by hand anywhere", recipe.Rank == 0 && Crafting.Locked(_me, recipe) == null, recipe.Cost);
			Check("a salve needs a workbench", Crafting.Locked(_me, recipe2) != null, Crafting.Locked(_me, recipe2));
			Check("nothing to make it with yet", !Crafting.CanMake(_me, recipe));
			Bag.Put(_me, "herbs", 4, spill: false);
			Bag.Put(_me, "wheat", 2, spill: false);
			Check("herbs and wheat: it can be made", Crafting.CanMake(_me, recipe), string.Format("{0} herbs, {1} wheat", Civil.BagCount(_me, "herbs"), Civil.BagCount(_me, "wheat")));
			Check("started", Mod.Craft.Start(_me, recipe, base.Note));
			for (float t = 0f; t < 30f; t += 0.2f)
			{
				if (Mod.Craft.Queue.Count <= 0)
				{
					break;
				}
				yield return CivilTest.Wait(0.2f);
			}
			for (int i = 0; i < 3; i++)
			{
				yield return null;
			}
			bool flag = Mod.Work.Gained.Exists((BagGain g) => g.Id == "tw_item_bandage" && !g.Landed);
			Check("the bag's button doesn't dip as they fly home", Mod.HudForTest.BagShownForTest == Mod.Work.BagCount || !flag, $"shown {Mod.HudForTest.BagShownForTest}, bag {Mod.Work.BagCount}, bandages flying: {flag}");
			yield return CivilTest.Wait(0.3f);
			Check("2 bandages made, into the bag", Supplies.Count(_me, "tw_item_bandage") == 2, Supplies.Count(_me, "tw_item_bandage").ToString());
			Check("2 herbs and 1 wheat used", Civil.BagCount(_me, "herbs") == 2 && Civil.BagCount(_me, "wheat") == 1);
			Check("they take a bag slot", Bag.Contents(_me).Any((KeyValuePair<string, int> kv) => kv.Key == "tw_item_bandage" && kv.Value == 2));
			Check("...shown in the bag's slots", BagOrder.Of(_me).Contains(BagOrder.ResourceKey("tw_item_bandage")), string.Join("|", BagOrder.Of(_me).ToArray()));
			Check("named and drawn", Work.ResourceName("tw_item_bandage") == "Bandages" && Supplies.Icon("tw_item_bandage") != null);
			Check("counted in words", Work.Amount("tw_item_bandage", 1) == "a bandage" && Work.Amount("tw_item_salve", 2) == "2 salves" && Work.Amount("wood", 5) == "5 wood", Work.Amount("tw_item_bandage", 1) + " / " + Work.Amount("tw_item_salve", 2) + " / " + Work.Amount("wood", 5));
		}

		private IEnumerator OnYourself()
		{
			yield return FaceOpen();
			Supplies.Kind kind = BandageKind;
			SetHealth(_me, 1f);
			Check("not hurt: not used", !Bandages.Use(_me, kind, _me, base.Note) && Supplies.Count(_me, kind.Id) == 2);
			SetHealth(_me, 0.4f);
			int before = Health(_me);
			int max = Max(_me);
			int total = Mathf.RoundToInt((float)max * kind.Heal);
			Check("used on yourself", Bandages.Use(_me, kind, _me, base.Note));
			Check("one less in the bag", Supplies.Count(_me, kind.Id) == 1);
			Check("healing", Bandages.Healing(_me) && Bandages.Pending(_me) > 0, Bandages.Pending(_me) + " to come");
			Check("the hands wind it on", Bandages.Wrapping && !Bandages.WrapOther);
			yield return null;
			yield return null;
			Check("no blows while they do", PossessionHooks.BlockAttack);
			yield return CivilTest.Wait(0.35f);
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "band_wind.png"));
			for (int i = 0; i < 120; i++)
			{
				if (!Mod.HudForTest.CapturePending)
				{
					break;
				}
				yield return null;
			}
			Note("wound on " + Mathf.RoundToInt(Bandages.WrapProgress * 100f) + "% when pictured");
			for (float w = 0f; w < 3f; w += 0.05f)
			{
				if (!Bandages.Wrapping)
				{
					break;
				}
				yield return CivilTest.Wait(0.05f);
			}
			yield return CivilTest.Wait(0.5f);
			Mod.SaveFrame("band_wrapped.png");
			Check("the wrap stays on while it heals", !Bandages.Wrapping && Bandages.Healing(_me));
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "band_hud.png"));
			for (int i = 0; i < 120; i++)
			{
				if (!Mod.HudForTest.CapturePending)
				{
					break;
				}
				yield return null;
			}
			Check("the hearts to come, in green", Mod.HudForTest.HealAheadForTest > 0, Mod.HudForTest.HealAheadForTest + " hearts");
			Check("its time beside the bar", Mod.HudForTest.HealTimerForTest);
			Check("a second one runs beside it", Bandages.Use(_me, kind, _me, base.Note) && Bandages.Timer(_me, out var _, out var _) == 2);
			yield return CivilTest.Wait(0.5f);
			int hit = Health(_me) - 5;
			SetHealthTo(_me, hit);
			yield return CivilTest.Wait(1f);
			Check("a blow doesn't stop them", Bandages.Healing(_me) && Health(_me) > hit, $"{hit} -> {Health(_me)}");
			float t;
			for (t = 0f; t < 60f; t += 0.25f)
			{
				if (!Bandages.Healing(_me))
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			int num = Health(_me);
			Check("both ran out", !Bandages.Healing(_me), t.ToString("0.0") + " s");
			Check("half of full health back (two quarters, less the blow)", num >= Mathf.Min(max, before - 5 + 2 * total - 2) && num <= max, $"{before} -> {num} of {max}, {total} each");
			Check("the bag's empty of them", Supplies.Count(_me, kind.Id) == 0);
		}

		private IEnumerator OnTheHotbar()
		{
			Supplies.Give(_me, "tw_item_bandage", 3);
			Hotbar.Sync(_me, base.Note);
			int num = BagOrder.Of(_me).IndexOf(BagOrder.ResourceKey("tw_item_bandage"));
			int num2 = Hotbar.Chosen(_me);
			if (num >= 0 && num != num2)
			{
				BagOrder.Swap(_me, num, num2);
			}
			Hotbar.Moved();
			yield return null;
			Check("chosen on the hotbar", Hotbar.Supply(_me) == BandageKind, Hotbar.InHand(_me));
			SetHealth(_me, 1f);
			yield return null;
			Check("whole: a click is a blow as ever", Mod.HotbarFoodForTest == null);
			SetHealth(_me, 0.5f);
			yield return null;
			Check("hurt: a click would bind", Mod.HotbarFoodForTest == "tw_item_bandage");
			yield return FaceOpen();
			WorldBoxMod.HotbarClickForTest = Time.frameCount + 1;
			yield return null;
			yield return null;
			Check("a click uses it", Supplies.Count(_me, "tw_item_bandage") == 2 && Bandages.Healing(_me), Supplies.Count(_me, "tw_item_bandage").ToString());
			for (float t = 0f; t < 30f; t += 0.25f)
			{
				if (!Bandages.Healing(_me))
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			Hotbar.Choose(_me, (Hotbar.Chosen(_me) + 1) % Mathf.Max(1, Hotbar.Size(_me)), wheel: false);
		}

		private IEnumerator OnSomeone()
		{
			yield return CivilTest.Wait(0.3f);
			SetHealth(_me, 1f);
			try
			{
				if (_other.is_inside_building)
				{
					_other.exitBuilding();
				}
			}
			catch
			{
			}
			SetHealth(_other, 0.4f);
			yield return FaceOpen();
			float yaw = Mod.ViewYaw;
			float near = 1.4f;
			for (float t = 0f; t < 6f; t += 0.1f)
			{
				if (Mod.Talk.Candidate == _other)
				{
					break;
				}
				bool flag = ((int)(t * 2f) & 1) == 1;
				near = (flag ? 2f : 1.4f);
				Mod.SetYaw(yaw);
				Mod.SetPitchDegrees(flag ? 0f : (-6f));
				try
				{
					if (_other.is_inside_building)
					{
						_other.exitBuilding();
					}
				}
				catch
				{
				}
				CivilTest.StandBefore(_other, _me, near);
				yield return CivilTest.Wait(0.1f);
			}
			for (int i = 0; i < 4; i++)
			{
				CivilTest.StandBefore(_other, _me, near);
				yield return null;
			}
			if (!Check("them under the crosshair", Mod.Talk.Candidate == _other, "under it: " + ((Mod.Picked is Actor a) ? Dialogue.NameOf(a) : ((Mod.Picked != null) ? Mod.Picked.GetType().Name : "nothing")) + ", they're " + (_other.current_position - _me.current_position).magnitude.ToString("0.0") + " tiles off" + (_other.is_inside_building ? ", indoors" : "")))
			{
				yield break;
			}
			string text = Bandages.WithHint(Mod.Prompt, _me);
			Check("the prompt offers it", text?.Contains(":Bandage") ?? false, text);
			int opinion = Chronicle.Opinion(_other, _me);
			int rep = Rep.Points(_me, _other.city);
			int num = Supplies.Count(_me, "tw_item_bandage");
			Apothecary.QuickItem(_me);
			Check("F3 binds them, not you", Bandages.Healing(_other) && !Bandages.Healing(_me) && Supplies.Count(_me, "tw_item_bandage") == num - 1);
			Check("the hands wind it round their arm", Bandages.Wrapping && Bandages.WrapOther);
			yield return CivilTest.Wait(0.35f);
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "band_other_wind.png"));
			for (int i = 0; i < 120; i++)
			{
				if (!Mod.HudForTest.CapturePending)
				{
					break;
				}
				yield return null;
			}
			for (float t = 0f; t < 1.2f; t += 0.1f)
			{
				CivilTest.StandBefore(_other, _me, near);
				yield return CivilTest.Wait(0.1f);
			}
			Check("a wrap on their arm", People.WrapDrawnForTest(_other));
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "band_other_wrap.png"));
			for (int i = 0; i < 120; i++)
			{
				if (!Mod.HudForTest.CapturePending)
				{
					break;
				}
				CivilTest.StandBefore(_other, _me, near);
				yield return null;
			}
			Check("their goodwill", Chronicle.Opinion(_other, _me) > opinion, opinion + " -> " + Chronicle.Opinion(_other, _me));
			Check("standing in their town", Rep.Points(_me, _other.city) > rep, rep + " -> " + Rep.Points(_me, _other.city));
			int again = Rep.Points(_me, _other.city);
			SetHealth(_other, 0.2f);
			yield return CivilTest.Wait(1.3f);
			for (int i = 0; i < 4; i++)
			{
				CivilTest.StandBefore(_other, _me, near);
				yield return null;
			}
			Apothecary.QuickItem(_me);
			Check("bound again soon after: no more standing", Rep.Points(_me, _other.city) == again);
			Mod.SetPitchDegrees(0f);
		}

		private IEnumerator QuickKey()
		{
			Mod.SetYaw(Mod.ViewYaw + MathF.PI);
			yield return CivilTest.Wait(0.3f);
			Supplies.Give(_me, "tw_item_salve", 1);
			Supplies.Give(_me, "tw_item_bandage", 1);
			SetHealth(_me, 0.3f);
			Check("a deep wound: the salve", Bandages.Best(_me, _me) == SalveKind);
			SetHealth(_me, 0.85f);
			Check("a light one: a bandage", Bandages.Best(_me, _me) == BandageKind);
			SetHealth(_me, 0.3f);
			yield return CivilTest.Wait(1.2f);
			int num = Supplies.Count(_me, "tw_item_salve");
			Apothecary.QuickItem(_me);
			Check("F3 with nobody before you: yourself, with the salve", Bandages.Healing(_me) && Supplies.Count(_me, "tw_item_salve") == num - 1);
			Bandages.Heal heal = Bandages.Active.LastOrDefault((Bandages.Heal x) => x.Who == _me);
			Check("a salve gives half over 10 s", heal != null && heal.Kind == SalveKind && heal.Seconds == 10f && Mathf.Abs((float)heal.Total - (float)Max(_me) * 0.5f) <= 1f, (heal != null) ? (heal.Total + " over " + heal.Seconds) : "none");
		}

		private IEnumerator Trader()
		{
			Actor actor = _people.FirstOrDefault((Actor u) => u != _me && u != _other && !u.isWarrior() && u.isAlive());
			if (Check("a trader", actor != null))
			{
				try
				{
					_me.addMoney(200);
				}
				catch
				{
				}
				int had = Supplies.Count(_me, "tw_item_bandage");
				int money = CivilTest.Money(_me);
				Conversation c = Mod.Converse;
				bool[] ok = new bool[1];
				yield return Talk(actor);
				yield return Say(c, "Let's trade.", ok);
				yield return Say(c, "What can I buy?", ok);
				Check("bandages and a salve for sale", Find(c, "Buy 3 bandages") != null && Find(c, "Buy a salve") != null, Labels(c));
				yield return Say(c, "Buy 3 bandages", ok);
				Check("3 bought", Supplies.Count(_me, "tw_item_bandage") == had + 3 && CivilTest.Money(_me) < money, string.Format("{0} -> {1}, {2} -> {3} gold", had, Supplies.Count(_me, "tw_item_bandage"), money, CivilTest.Money(_me)));
				Mod.EndConversation();
				yield return CivilTest.Wait(0.3f);
			}
		}

		private IEnumerator Looted()
		{
			List<Actor> bandits = new List<Actor>();
			int num = 0;
			for (int i = 0; i < 6; i++)
			{
				Actor actor = CivilTest.SpawnNear("bandit", _me.current_position + new Vector2(12f + (float)i, 6f));
				if (actor != null)
				{
					try
					{
						((BaseSimObject)actor).addStatusEffect("invincible", 30f, pColorEffect: false);
						actor.makeWait(30f);
					}
					catch
					{
					}
					bandits.Add(actor);
					if (Supplies.Count(actor, "tw_item_bandage") > 0)
					{
						num++;
					}
				}
			}
			if (!Check("bandits", bandits.Count >= 4))
			{
				yield break;
			}
			Check("bandits carry bandages of their own", num >= 1, num + " of " + bandits.Count);
			Check("a villager doesn't", Supplies.Count(_other, "tw_item_salve") == 0 && !_people.Where((Actor u) => !u.isWarrior() && u != _me).Any((Actor u) => Supplies.Count(u, "tw_item_bandage") > 0));
			Actor bandit = bandits.FirstOrDefault((Actor b) => Supplies.Count(b, "tw_item_bandage") > 0) ?? bandits[0];
			Supplies.Give(bandit, "tw_item_salve", 1);
			int carried = Supplies.Count(bandit, "tw_item_bandage");
			int salves = Supplies.Count(bandit, "tw_item_salve");
			Vector2 ahead = _me.current_position + new Vector2(Mathf.Cos(Mod.ViewYaw), Mathf.Sin(Mod.ViewYaw)) * 2.5f;
			for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
			{
				bandit.current_position = ahead;
				yield return null;
			}
			Loot.WatchForTest(bandit);
			try
			{
				bandit.finishStatusEffect("invincible");
			}
			catch
			{
			}
			string name = Dialogue.NameOf(bandit);
			CivilTest.Kill(bandit);
			Loot.Fallen fallen = null;
			for (int i2 = 0; i2 < 30; i2++)
			{
				if (fallen != null)
				{
					break;
				}
				yield return null;
				fallen = Loot.Bodies.Find((Loot.Fallen f) => f.Name == name);
			}
			if (!Check("his body keeps his bandages and salve", fallen != null && fallen.Bag.Exists((KeyValuePair<string, int> kv) => kv.Key == "tw_item_bandage" && kv.Value == carried) && fallen.Bag.Exists((KeyValuePair<string, int> kv) => kv.Key == "tw_item_salve" && kv.Value == salves), (fallen == null) ? "no body" : string.Join(", ", fallen.Bag.ConvertAll((KeyValuePair<string, int> kv) => kv.Key + " " + kv.Value).ToArray())))
			{
				yield break;
			}
			int num2 = Supplies.Count(_me, "tw_item_bandage");
			int num3 = Supplies.Count(_me, "tw_item_salve");
			Loot.Take(_me, fallen, base.Note);
			Check("taken into your bag", Supplies.Count(_me, "tw_item_bandage") == num2 + carried && Supplies.Count(_me, "tw_item_salve") == num3 + salves, string.Format("{0} -> {1} bandages, {2} -> {3} salves", num2, Supplies.Count(_me, "tw_item_bandage"), num3, Supplies.Count(_me, "tw_item_salve")));
			foreach (Actor item in bandits)
			{
				if (item != bandit)
				{
					CivilTest.Kill(item);
				}
			}
		}

		private IEnumerator Dropped()
		{
			Mod.SetYaw(Mod.ViewYaw + 0.6f);
			yield return CivilTest.Wait(0.2f);
			int num = Supplies.Count(_me, "tw_item_bandage");
			int count = Bundles.List.Count;
			if (Check("bandages to drop", num > 0))
			{
				bool flag = Mod.DropResource("tw_item_bandage", 1);
				Bundles.Bundle bundle = ((Bundles.List.Count > count) ? Bundles.List[Bundles.List.Count - 1] : null);
				Check("one dropped as a sack", flag && bundle != null && bundle.Resource == "tw_item_bandage" && bundle.Count == 1 && Supplies.Count(_me, "tw_item_bandage") == num - 1);
				Check("...called a bandage", bundle != null && Bundles.NameOf(bundle) == "a bandage", (bundle != null) ? Bundles.NameOf(bundle) : null);
				if (bundle != null)
				{
					Check("picked up again", Mod.PickUpForTest(bundle) && Supplies.Count(_me, "tw_item_bandage") == num);
				}
			}
		}

		private IEnumerator LeavingTown()
		{
			int b = Supplies.Count(_me, "tw_item_bandage");
			int s = Supplies.Count(_me, "tw_item_salve");
			bool left = Belong.Leave(_me);
			yield return null;
			yield return null;
			Check("left the town", left && _me.city == null);
			Check("no bandages from leaving it", Supplies.Count(_me, "tw_item_bandage") == b && Supplies.Count(_me, "tw_item_salve") == s, string.Format("{0} -> {1} bandages, {2} -> {3} salves", b, Supplies.Count(_me, "tw_item_bandage"), s, Supplies.Count(_me, "tw_item_salve")));
		}

		private void Icons()
		{
			Supplies.Kind[] all = Supplies.All;
			foreach (Supplies.Kind kind in all)
			{
				Sprite sprite = Supplies.Icon(kind.Id);
				if (!Check("an icon for " + kind.One, sprite != null && sprite.texture != null))
				{
					continue;
				}
				try
				{
					Texture2D texture = sprite.texture;
					int num = 8;
					Texture2D texture2D = new Texture2D(texture.width * num, texture.height * num, TextureFormat.RGBA32, mipChain: false);
					for (int j = 0; j < texture2D.height; j++)
					{
						for (int k = 0; k < texture2D.width; k++)
						{
							Color pixel = texture.GetPixel(k / num, j / num);
							texture2D.SetPixel(k, j, (pixel.a > 0f) ? pixel : new Color(0.17f, 0.15f, 0.15f, 1f));
						}
					}
					texture2D.Apply(updateMipmaps: false);
					File.WriteAllBytes(Path.Combine(Log.Folder, "band_icon_" + kind.One + ".png"), texture2D.EncodeToPNG());
				}
				catch (Exception ex)
				{
					Note("icon picture: " + ex.Message);
				}
			}
		}

		private IEnumerator FaceOpen()
		{
			float start = Mod.ViewYaw;
			for (int k = 0; k < 16; k++)
			{
				Mod.SetYaw(start + (float)k * MathF.PI / 8f);
				Mod.SetPitchDegrees(-4f);
				yield return CivilTest.Wait(0.15f);
				yield return null;
				if (Mod.Work.Target == null && Mod.Picked == null)
				{
					break;
				}
			}
			Note("facing " + (Mod.ViewYaw * 57.29578f).ToString("0") + " deg, under the crosshair: " + ((Mod.Picked != null) ? Mod.Picked.GetType().Name : "nothing"));
		}

		private static int Health(Actor a)
		{
			try
			{
				return a.getHealth();
			}
			catch
			{
				return 0;
			}
		}

		private static int Max(Actor a)
		{
			try
			{
				return Mathf.Max(1, a.getMaxHealth());
			}
			catch
			{
				return 1;
			}
		}

		private static void SetHealth(Actor a, float share)
		{
			SetHealthTo(a, Mathf.RoundToInt((float)Max(a) * share));
		}

		private static void SetHealthTo(Actor a, int hp)
		{
			try
			{
				a.data.health = Mathf.Clamp(hp, 1, Max(a));
			}
			catch
			{
			}
		}

		private static void Empty(Actor a)
		{
			foreach (KeyValuePair<string, int> item in Bag.Contents(a))
			{
				if (!Supplies.Is(item.Key))
				{
					try
					{
						a.takeFromInventory(item.Key, item.Value);
					}
					catch
					{
					}
				}
			}
			Supplies.Clear(a);
			while (Gear.Count(a) > 0)
			{
				Gear.Remove(a, 0);
			}
		}

		private void Stockpile()
		{
			try
			{
				WorldTile tile = _city.getTile();
				for (int i = 0; i < 30; i++)
				{
					WorldTile tile2 = World.world.GetTile(tile.x + UnityEngine.Random.Range(-5, 6), tile.y + UnityEngine.Random.Range(-5, 6));
					if (tile2 == null || tile2.Type == null || !tile2.Type.ground || tile2.Type.block || (tile2.building != null && AssetStyle.IsSolid(tile2.building)))
					{
						continue;
					}
					Building building = World.world.buildings.addBuilding("stockpile", tile2, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
					if (building != null)
					{
						if (building.city != _city)
						{
							_city.listBuilding(building);
						}
						break;
					}
				}
			}
			catch (Exception ex)
			{
				Note("stockpile: " + ex.Message);
			}
		}

		private static Choice Find(Conversation c, string start)
		{
			return c.Choices.Find((Choice ch) => ch.Label.StartsWith(start, StringComparison.Ordinal));
		}

		private static string Labels(Conversation c)
		{
			return string.Join(" | ", c.Choices.ConvertAll((Choice x) => x.Label + (x.Enabled ? "" : (" [off: " + x.Note + "]"))).ToArray());
		}

		private IEnumerator Say(Conversation c, string start, bool[] ok)
		{
			for (int i = 0; i < 12; i++)
			{
				if (!c.MorePages)
				{
					break;
				}
				c.Next();
				c.Next();
			}
			Choice choice = Find(c, start);
			ok[0] = choice?.Enabled ?? false;
			if (!ok[0])
			{
				Note("(no \"" + start + "\" now: " + Labels(c) + ")");
				yield break;
			}
			c.LineStart -= 999f;
			c.Pick(c.Choices.IndexOf(choice));
			yield return CivilTest.Wait(0.15f);
			for (int j = 0; j < 12; j++)
			{
				if (!c.MorePages)
				{
					break;
				}
				c.Next();
				c.Next();
			}
		}

		private IEnumerator Talk(Actor npc)
		{
			if (Mod.Converse.Open)
			{
				Mod.EndConversation();
			}
			CivilTest.StandBefore(npc, _me, 1.4f);
			yield return CivilTest.Wait(0.3f);
			Mod.StartConversationForTest(npc);
			yield return CivilTest.Wait(0.2f);
			Conversation converse = Mod.Converse;
			for (int i = 0; i < 12; i++)
			{
				if (!converse.MorePages)
				{
					break;
				}
				converse.Next();
				converse.Next();
			}
		}
	}
}
