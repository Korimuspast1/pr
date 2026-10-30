using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class PeaceTest : CivilTest
	{
		private string _said;

		private Kingdom _home;

		private Kingdom _foe;

		private Actor _king;

		private City _capital;

		private City _theirCapital;

		private City _theirTown;

		private readonly List<City> _spare = new List<City>();

		protected override string Prefix => "PEACE";

		private Action<string> Toaster => delegate(string m)
		{
			_said = m;
			Note("toast: " + m);
		};

		protected override IEnumerator Body()
		{
			Actor king = Village("human", 14, 2);
			if (!Check("our realm", king != null))
			{
				yield break;
			}
			List<Actor> list = new List<Actor>(Founded);
			Kingdom kingdom = king.kingdom;
			for (int i = 0; i < 2; i++)
			{
				Actor actor = Village("human", 4 + i * 6, 0, king.current_tile, 25f + (float)i * 20f);
				if (actor != null && actor.city != null)
				{
					list.AddRange(Founded);
					City city = actor.city;
					try
					{
						city.joinAnotherKingdom(kingdom);
					}
					catch (Exception ex)
					{
						Note("joining: " + ex.Message);
					}
					if (city.kingdom == kingdom)
					{
						_spare.Add(city);
					}
				}
			}
			if (!Check("two more towns of ours", _spare.Count == 2, _spare.Count + " joined"))
			{
				yield break;
			}
			Actor actor2 = Village("orc", 10, 0, king.current_tile, 70f) ?? Village("orc", 10, 0);
			if (!Check("a rival realm", actor2 != null && actor2.kingdom != null && actor2.kingdom != kingdom))
			{
				yield break;
			}
			list.AddRange(Founded);
			Kingdom kingdom2 = actor2.kingdom;
			Actor actor3 = Village("orc", 5, 0, actor2.current_tile, 20f);
			if (actor3 != null && actor3.city != null)
			{
				list.AddRange(Founded);
				City city2 = actor3.city;
				try
				{
					city2.joinAnotherKingdom(kingdom2);
				}
				catch (Exception ex2)
				{
					Note("joining: " + ex2.Message);
				}
				if (city2.kingdom == kingdom2)
				{
					_theirTown = city2;
				}
			}
			if (!Check("a second town of theirs", _theirTown != null))
			{
				yield break;
			}
			foreach (Actor item in list)
			{
				try
				{
					((BaseSimObject)item).addStatusEffect("invincible", 900f, pColorEffect: false);
				}
				catch
				{
				}
			}
			_king = king;
			_home = kingdom;
			_foe = kingdom2;
			_capital = king.city;
			_theirCapital = actor2.city;
			Stockpile(_capital);
			Stockpile(_theirCapital);
			yield return CivilTest.Wait(1.5f);
			try
			{
				_capital.addResourcesToRandomStockpile("gold", 600);
				_theirCapital.addResourcesToRandomStockpile("gold", 400);
			}
			catch
			{
			}
			Note($"ours: {_capital.name} (capital) + {_spare[0].name} ({Peace.People(_spare[0])}), {_spare[1].name} ({Peace.People(_spare[1])}), {Gold(_capital)} gold; " + $"theirs: {_theirCapital.name} (capital) + {_theirTown.name} ({Peace.People(_theirTown)}), {Gold(_theirCapital)} gold");
			yield return Possess(king);
			if (Check("the king in first person", InControl(king) && Royal.IsKing(king)))
			{
				yield return Part("a grudge", Grudge());
				yield return Part("paid in gold", PaidInGold());
				yield return Part("a truce broken", TruceBroken());
				yield return Part("a town, and another", ATown());
				yield return Part("refused", Refused());
				yield return Part("your terms", YourTerms());
				yield return Part("their envoy gives", EnvoyGives());
				yield return Part("their terms", TheirTerms());
				yield return Part("strikes wear them down", Strikes());
				yield return Part("a strike at peace", StrikeAtPeace());
				yield return Part("moods", Moods());
				Peace.ForceD = null;
				Peace.ForcePosition = null;
				Court.SessionForTest = null;
				yield return Part("an old save at war", OldSave());
			}
		}

		private IEnumerator OldSave()
		{
			yield return War();
			long home = _home.getID();
			long foe = _foe.getID();
			string key = ((home > foe) ? (home + "_" + foe) : (foe + "_" + home));
			DiplomacyRelation pObject;
			bool ok = World.world.diplomacy.tryGet(key, out pObject);
			if (!Check("the realms have a relation to save", ok))
			{
				yield break;
			}
			int num = 0;
			foreach (War activeWar in World.world.wars.getActiveWars())
			{
				if (activeWar != null && !activeWar.hasEnded())
				{
					try
					{
						activeWar.data.set("fp_grudged", pData: false);
						num++;
					}
					catch
					{
					}
				}
			}
			Kingdom[] array = new Kingdom[2] { _home, _foe };
			foreach (Kingdom kingdom in array)
			{
				try
				{
					kingdom.data.removeString("fp_grudge");
				}
				catch
				{
				}
			}
			byte[] array2;
			try
			{
				array2 = SaveManager.currentWorldToSavedMap().toZip();
			}
			catch (Exception ex)
			{
				Check("the world saved to bytes", ok: false, ex.Message);
				yield break;
			}
			try
			{
				if (World.world.diplomacy.tryGet(key, out var pObject2))
				{
					World.world.diplomacy.removeObject(pObject2);
				}
			}
			catch (Exception ex2)
			{
				Note("removing the relation: " + ex2.Message);
			}
			Peace.WatchForTest();
			Check("the watch makes no relation of its own (as mid-load)", !World.world.diplomacy.tryGet(key, out pObject));
			Note($"saved {array2.Length} bytes with {num} uncounted war(s); loading it back, watching wars each frame");
			try
			{
				SaveManager.loadMapFromBytes(array2);
			}
			catch (Exception ex3)
			{
				Check("the world loaded from bytes", ok: false, ex3.Message);
				yield break;
			}
			int watched = 0;
			for (float t = 0f; t < 60f; t += Time.unscaledDeltaTime)
			{
				try
				{
					Peace.WatchForTest();
					watched++;
				}
				catch (Exception ex4)
				{
					Note("watch: " + ex4.Message);
				}
				yield return null;
				bool flag = true;
				try
				{
					flag = SmoothLoader.isLoading();
				}
				catch
				{
				}
				if (!flag && Config.game_loaded && t > 2f)
				{
					break;
				}
			}
			yield return CivilTest.Wait(1f);
			Kingdom h = null;
			Kingdom f = null;
			try
			{
				h = World.world.kingdoms.get(home);
				f = World.world.kingdoms.get(foe);
			}
			catch
			{
			}
			Check("both realms are back after the load", h != null && f != null, $"watched {watched} frames");
			Check("their relation loaded once", World.world.diplomacy.tryGet(key, out var pObject3) && pObject3 != null);
			Check("still at war", h != null && f != null && Royal.AtWar(h, f));
			yield return CivilTest.Wait(2.5f);
			float num2 = ((f != null && h != null) ? (Peace.GrudgeOf(f, h) + Peace.GrudgeOf(h, f)) : 0f);
			Check("the war is counted once loaded", num2 > 0f, num2.ToString("F1"));
		}

		private IEnumerator Grudge()
		{
			float before = Peace.GrudgeOf(_foe, _home);
			yield return War();
			float g = Peace.GrudgeOf(_foe, _home);
			Check("declaring war gives them a grudge", g - before >= 24f, $"{before:F1} -> {g:F1}");
			Check("and gives us none against them", Peace.GrudgeOf(_home, _foe) < 1f, Peace.GrudgeOf(_home, _foe).ToString("F1"));
			yield return CivilTest.Wait(2.2f);
			Check("the war is counted once", Mathf.Abs(Peace.GrudgeOf(_foe, _home) - g) < 1f, $"{g:F1} -> {Peace.GrudgeOf(_foe, _home):F1}");
			string pResult = null;
			try
			{
				_foe.data.get("fp_grudge", out pResult, null);
			}
			catch
			{
			}
			Check("kept on their kingdom's data", !string.IsNullOrEmpty(pResult), pResult);
			Peace.Stance stance = Peace.StanceOf(_foe, _home);
			Note("stance: " + Peace.Describe(stance));
			Check("they know who started it", stance.YouStarted && stance.Reasons.Exists((Peace.Why w) => w.Text == "You started this war"));
		}

		private IEnumerator PaidInGold()
		{
			Force(20f, 0f);
			int ours0 = Gold(_capital);
			int theirs0 = Gold(_theirCapital);
			float g0 = Peace.GrudgeOf(_foe, _home);
			yield return AskForPeace();
			Conversation c = Mod.Converse;
			Check("their envoy comes to talk", c.Open && c.LastTopic == "peace_talk", $"open {c.Open}, topic {c.LastTopic}, toast \"{_said}\"");
			Note("envoy: \"" + c.Line + "\"  [" + Choices(c) + "]");
			Check("their price is 120 gold", Find(c, "Pay 120 gold") != null, Choices(c));
			yield return Capture("peace_talk.png");
			Pick(c, "Pay 120 gold");
			yield return CivilTest.Wait(0.5f);
			Check("paid: the war is over", !Royal.AtWar(_home, _foe));
			Check("the gold went from our capital to theirs", ours0 - Gold(_capital) == 120 && Gold(_theirCapital) - theirs0 == 120, $"ours {ours0} -> {Gold(_capital)}, theirs {theirs0} -> {Gold(_theirCapital)}");
			Check("what was paid comes off their grudge", Peace.GrudgeOf(_foe, _home) < g0 - 15f, $"{g0:F1} -> {Peace.GrudgeOf(_foe, _home):F1}");
			yield return EndTalk(c);
		}

		private IEnumerator TruceBroken()
		{
			float g0 = Peace.GrudgeOf(_foe, _home);
			yield return War();
			float num = Peace.GrudgeOf(_foe, _home);
			Check("war right after peace breaks a truce", num - g0 >= 59f, $"{g0:F1} -> {num:F1}");
			Peace.Stance stance = Peace.StanceOf(_foe, _home);
			Note("stance: " + Peace.Describe(stance));
			Check("they say so", stance.Reasons.Exists((Peace.Why w) => w.Text == "You broke a truce"));
		}

		private IEnumerator ATown()
		{
			Force(50f, 0f);
			Peace.Deal d = Peace.TermsFor(Peace.StanceOf(_foe, _home));
			City named = ((d.YouCede.Count > 0) ? d.YouCede[0] : null);
			if (Check("they name one of our towns", d.YouCede.Count == 1 && named != null && !named.isCapitalCity(), d.YouCede.Count + " named"))
			{
				yield return AskForPeace();
				Conversation c = Mod.Converse;
				Note("envoy: \"" + c.Line + "\"  [" + Choices(c) + "]");
				Check("their price is that town", Find(c, "Cede " + named.name) != null, Choices(c));
				List<City> list = Peace.InPlaceOf(d, named);
				if (Check("a town as big to offer instead", list.Count > 0, $"{named.name} has {Peace.People(named)}"))
				{
					City other = list[0];
					Pick(c, "Offer another town");
					yield return CivilTest.Wait(0.2f);
					Note("envoy: \"" + c.Line + "\"  [" + Choices(c) + "]");
					Pick(c, other.name);
					yield return CivilTest.Wait(0.2f);
					Check("they take the other in its place", Find(c, "Cede " + other.name) != null, Choices(c));
					Pick(c, "Cede " + other.name);
					yield return CivilTest.Wait(0.6f);
					Check("ceded: the war is over", !Royal.AtWar(_home, _foe));
					Check(other.name + " is theirs, " + named.name + " still ours", other.kingdom == _foe && named.kingdom == _home, other.name + ": " + Royal.NameOf(other.kingdom) + ", " + named.name + ": " + Royal.NameOf(named.kingdom));
					yield return EndTalk(c);
				}
			}
		}

		private IEnumerator Refused()
		{
			yield return War();
			Force(120f, 0f);
			yield return AskForPeace();
			Conversation c = Mod.Converse;
			Note("envoy: \"" + c.Line + "\"  [" + Choices(c) + "]");
			Check("they refuse", c.Open && c.Line != null && c.Line.Contains("No peace") && Find(c, "Then the war goes on.") != null, c.Line);
			Pick(c, "Then the war goes on.");
			yield return CivilTest.Wait(0.3f);
			Check("still at war", Royal.AtWar(_home, _foe));
			yield return EndTalk(c);
			_said = null;
			Peace.Ask(_king, _foe, Toaster);
			yield return CivilTest.Wait(0.5f);
			Check("they won't hear you again for a year", !Mod.Converse.Open && _said != null && _said.Contains("Ask again in") && Peace.WaitFor(_home, _foe) > 40f, $"\"{_said}\", {Peace.WaitFor(_home, _foe):F0} s");
			Peace.ClearForTest();
		}

		private IEnumerator YourTerms()
		{
			Force(-20f, 0f);
			int ours0 = Gold(_capital);
			int theirs0 = Gold(_theirCapital);
			City theirs1 = Peace.TheirTown(_foe, _home);
			if (!Check("a town of theirs to ask for", theirs1 != null))
			{
				yield break;
			}
			yield return AskForPeace();
			Conversation c = Mod.Converse;
			Note("envoy: \"" + c.Line + "\"  [" + Choices(c) + "]");
			Check("beaten, they ask your terms", c.Line != null && c.Line.Contains("terms") && Find(c, "Peace, and " + theirs1.name) != null, Choices(c));
			Pick(c, "Peace, and " + theirs1.name);
			yield return CivilTest.Wait(0.3f);
			Note("envoy: \"" + c.Line + "\"  [" + Choices(c) + "]");
			Check("not beaten enough to give up a town", Royal.AtWar(_home, _foe) && theirs1.kingdom == _foe && Find(c, "Peace, and " + theirs1.name) == null, Choices(c));
			Choice choice = null;
			foreach (Choice choice2 in c.Choices)
			{
				if (choice2.Label.StartsWith("Peace, and ", StringComparison.Ordinal) && choice2.Label.EndsWith(" gold", StringComparison.Ordinal))
				{
					choice = choice2;
				}
			}
			if (Check("but gold is on offer", choice != null, Choices(c)))
			{
				int asked = int.Parse(choice.Label.Substring("Peace, and ".Length).Split(' ')[0]);
				Pick(c, choice.Label);
				yield return CivilTest.Wait(0.5f);
				Check("they pay: the war is over", !Royal.AtWar(_home, _foe));
				Check($"{asked} gold from their capital to ours", Gold(_capital) - ours0 == asked && theirs0 - Gold(_theirCapital) == asked, $"ours {ours0} -> {Gold(_capital)}, theirs {theirs0} -> {Gold(_theirCapital)}");
				yield return EndTalk(c);
			}
		}

		private IEnumerator EnvoyGives()
		{
			yield return War();
			Force(-50f, -0.5f);
			Check("beaten, they'd send an envoy with an offer", Peace.WouldSendEnvoy(_home, _foe, out var terms) && !terms);
			Court.SessionForTest = true;
			CourtThrone.Envoys.Clear();
			CourtThrone.Envoys.Add(new CourtThrone.Envoy
			{
				From = _foe,
				FromName = Royal.NameOf(_foe),
				Kind = CourtThrone.Offer.Peace,
				Talks = true,
				Until = Time.unscaledTime + 300f
			});
			City theirs = Peace.TheirTown(_foe, _home);
			if (Check("a town of theirs to give", theirs != null))
			{
				CourtThrone.Begin(_king, Toaster);
				yield return CivilTest.Wait(0.6f);
				Conversation c = Mod.Converse;
				Note("envoy: \"" + c.Line + "\"  [" + Choices(c) + "]");
				Check("their envoy offers a town", c.Open && Find(c, "Accept peace and " + theirs.name) != null, Choices(c));
				yield return Capture("peace_envoy.png");
				Pick(c, "Accept peace and " + theirs.name);
				yield return CivilTest.Wait(0.6f);
				Check("peace, and the town is ours", !Royal.AtWar(_home, _foe) && theirs.kingdom == _home, Royal.NameOf(theirs.kingdom));
				CourtThrone.Stop();
				yield return EndTalk(c);
			}
		}

		private IEnumerator TheirTerms()
		{
			yield return War();
			Force(50f, 0.5f);
			Check("winning a war you started, they'd send terms", Peace.WouldSendEnvoy(_home, _foe, out var terms) && terms);
			CourtThrone.Envoys.Clear();
			CourtThrone.Envoys.Add(new CourtThrone.Envoy
			{
				From = _foe,
				FromName = Royal.NameOf(_foe),
				Kind = CourtThrone.Offer.Terms,
				Talks = true,
				Until = Time.unscaledTime + 300f
			});
			CourtThrone.Begin(_king, Toaster);
			yield return CivilTest.Wait(0.6f);
			Conversation c = Mod.Converse;
			Note("envoy: \"" + c.Line + "\"  [" + Choices(c) + "]");
			Check("their envoy brings terms", c.Open && c.Line != null && c.Line.Contains("terms") && Find(c, "Cede ") != null && Find(c, "Refuse") != null, Choices(c));
			Pick(c, "Refuse");
			yield return CivilTest.Wait(0.3f);
			Check("refused: the war goes on", Royal.AtWar(_home, _foe));
			CourtThrone.Stop();
			Court.SessionForTest = null;
			yield return EndTalk(c);
		}

		private IEnumerator Strikes()
		{
			Peace.ForceD = null;
			Peace.ForcePosition = 0f;
			Peace.ClearForTest();
			Peace.Stance stance = Peace.StanceOf(_foe, _home);
			Peace.Struck(_home, _foe, _theirCapital, Royal.CostOf(Royal.Deed.Nuke), Toaster, null);
			Peace.Stance stance2 = Peace.StanceOf(_foe, _home);
			Note("before: " + Peace.Describe(stance));
			Note("after: " + Peace.Describe(stance2));
			Check("a nuke's weight on their capital wears them down", stance2.D < stance.D - 30f && stance2.Reasons.Exists((Peace.Why w) => w.Text == "Your strikes on their towns"), $"{stance.D:F0} -> {stance2.D:F0}");
			Check("but can't make them pay", stance2.D >= Mathf.Min(stance.D, -15f) - 0.01f, $"{stance2.D:F0}");
			yield break;
		}

		private IEnumerator StrikeAtPeace()
		{
			Peace.ForcePosition = null;
			Mod.Royal.MakePeace(_king, _foe, Toaster);
			yield return CivilTest.Wait(0.3f);
			if (Check("at peace", !Royal.AtWar(_home, _foe)))
			{
				_home.data.total_kills += 500L;
				float g0 = Peace.GrudgeOf(_foe, _home);
				float mine0 = Peace.GrudgeOf(_home, _foe);
				_said = null;
				Mod.Royal.Unleash(_king, _theirCapital, Royal.Deed.Meteor, Toaster, delegate
				{
				});
				Check("a strike at peace warns first", !Royal.AtWar(_home, _foe) && _said != null && _said.Contains("means war"), _said);
				Mod.Royal.Unleash(_king, _theirCapital, Royal.Deed.Meteor, Toaster, delegate
				{
				});
				yield return CivilTest.Wait(0.5f);
				War war = Peace.WarBetween(_home, _foe);
				Check("the second click: they declare war on you", war != null && war.getMainAttacker() == _foe, (war != null) ? (Royal.NameOf(war.getMainAttacker()) + " attacks") : "no war");
				Check("and the grudge is yours", Peace.GrudgeOf(_foe, _home) - g0 >= 24f, $"{g0:F1} -> {Peace.GrudgeOf(_foe, _home):F1}");
				yield return CivilTest.Wait(2.6f);
				Check("their declaration isn't held against them", Peace.GrudgeOf(_home, _foe) - mine0 < 1f, $"{mine0:F1} -> {Peace.GrudgeOf(_home, _foe):F1}");
			}
		}

		private IEnumerator Moods()
		{
			(float, string)[] array = new(float, string)[7]
			{
				(120f, "Furious"),
				(80f, "Bitter"),
				(50f, "Bitter"),
				(20f, "Wary"),
				(0f, "Willing"),
				(-20f, "Beaten"),
				(-50f, "Beaten")
			};
			for (int i = 0; i < array.Length; i++)
			{
				(float, string) tuple = array[i];
				Force(tuple.Item1, 0f);
				Peace.Mood mood = Peace.MoodOf(_foe, _home);
				List<string> list = new List<string>();
				foreach (KeyValuePair<string, Color> line in mood.Lines)
				{
					list.Add(line.Key);
				}
				Check($"at {tuple.Item1:F0} they're {tuple.Item2}", mood.Word == tuple.Item2, mood.Word + ": " + string.Join(" / ", list.ToArray()));
			}
			Force(50f, 0.3f);
			Mod.Royal.Open = true;
			Mod.Royal.Selected = _foe;
			yield return CivilTest.Wait(0.8f);
			yield return Capture("peace_council.png");
			Mod.Royal.Open = false;
		}

		private static void Force(float d, float position)
		{
			Peace.ForceD = d;
			Peace.ForcePosition = position;
			Peace.ClearForTest();
		}

		private IEnumerator War()
		{
			if (!Royal.AtWar(_home, _foe))
			{
				Mod.Royal.DeclareWar(_king, _foe, Toaster, delegate
				{
				});
			}
			yield return CivilTest.Wait(2.6f);
			Check("at war", Royal.AtWar(_home, _foe));
		}

		private IEnumerator AskForPeace()
		{
			_said = null;
			Peace.Ask(_king, _foe, Toaster);
			yield return CivilTest.Wait(0.6f);
		}

		private IEnumerator EndTalk(Conversation c)
		{
			c.LineStart -= 999f;
			Mod.EndConversation();
			yield return CivilTest.Wait(0.4f);
		}

		private static Choice Find(Conversation c, string label)
		{
			foreach (Choice choice in c.Choices)
			{
				if (choice.Label != null && choice.Label.StartsWith(label, StringComparison.Ordinal))
				{
					return choice;
				}
			}
			return null;
		}

		private void Pick(Conversation c, string label)
		{
			for (int i = 0; i < 6; i++)
			{
				if (!c.MorePages)
				{
					break;
				}
				c.Next();
				c.Next();
			}
			Choice choice = Find(c, label);
			if (choice == null)
			{
				Note("(no choice \"" + label + "\": " + Choices(c) + ")");
			}
			else
			{
				c.LineStart -= 999f;
				c.Pick(c.Choices.IndexOf(choice));
			}
		}

		private static string Choices(Conversation c)
		{
			return string.Join(" | ", c.Choices.ConvertAll((Choice x) => x.Label + (x.Enabled ? "" : (" (off: " + x.Note + ")"))).ToArray());
		}

		private static int Gold(City c)
		{
			try
			{
				return c?.getResourcesAmount("gold") ?? 0;
			}
			catch
			{
				return 0;
			}
		}

		private void Stockpile(City c)
		{
			try
			{
				if (c.hasStorages())
				{
					return;
				}
				WorldTile tile = c.getTile();
				for (int i = 0; i < 40; i++)
				{
					WorldTile tile2 = World.world.GetTile(tile.x + UnityEngine.Random.Range(-5, 6), tile.y + UnityEngine.Random.Range(-5, 6));
					if (tile2 == null || tile2.Type == null || !tile2.Type.ground || tile2.Type.block || (tile2.building != null && AssetStyle.IsSolid(tile2.building)))
					{
						continue;
					}
					Building building = World.world.buildings.addBuilding("stockpile", tile2, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
					if (building != null)
					{
						if (building.city != c)
						{
							c.listBuilding(building);
						}
						return;
					}
				}
				Note("no stockpile for " + c.name);
			}
			catch (Exception ex)
			{
				Note("stockpile: " + ex.Message);
			}
		}

		private IEnumerator Capture(string name, float wait = 0.4f)
		{
			if (wait > 0f)
			{
				yield return CivilTest.Wait(wait);
			}
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, name));
			for (int i = 0; i < 20; i++)
			{
				if (!Mod.HudForTest.CapturePending)
				{
					break;
				}
				yield return null;
			}
			Note("captured " + name);
		}
	}
}
