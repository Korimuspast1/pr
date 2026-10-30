using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal sealed class CourtTest : CivilTest
	{
		private readonly List<Actor> _ours = new List<Actor>();

		protected override string Prefix => "COURT";

		protected override IEnumerator Body()
		{
			Actor king = Village("human", 14, 2);
			if (!Check("our realm", king != null))
			{
				yield break;
			}
			_ours.Clear();
			_ours.AddRange(Founded);
			foreach (Actor our in _ours)
			{
				try
				{
					((BaseSimObject)our).addStatusEffect("invincible", 900f, pColorEffect: false);
				}
				catch
				{
				}
			}
			Kingdom home = king.kingdom;
			Actor actor = Village("orc", 10, 0, king.current_tile, 50f) ?? Village("orc", 10, 0);
			if (!Check("a rival realm", actor != null && actor.kingdom != null && actor.kingdom != home))
			{
				yield break;
			}
			Kingdom foe = actor.kingdom;
			Stockpile(king.city);
			Stockpile(actor.city);
			yield return CivilTest.Wait(1.5f);
			List<Actor> list = new List<Actor>();
			foreach (Actor our2 in _ours)
			{
				if (our2 != null && our2 != king && our2.isAlive() && our2.isAdult())
				{
					list.Add(our2);
				}
			}
			if (!Check("grown people to seat", list.Count >= 8, list.Count + " adults"))
			{
				yield break;
			}
			Actor heir = null;
			Actor leader = null;
			try
			{
				heir = SuccessionTool.findNextHeir(king.kingdom, king);
				leader = king.city.leader;
			}
			catch
			{
			}
			List<Actor> list2 = list.FindAll((Actor a) => a != heir && a != leader);
			if (!Check("people who are neither leader nor heir", list2.Count >= 3, list2.Count + " of " + list.Count))
			{
				yield break;
			}
			Actor rich = list2[list2.Count - 1];
			Actor sly = list2[list2.Count - 2];
			Actor wizard = list2[list2.Count - 3];
			Actor[] array = new Actor[3] { rich, sly, wizard };
			foreach (Actor actor2 in array)
			{
				try
				{
					if (actor2.isWarrior())
					{
						actor2.stopBeingWarrior();
					}
				}
				catch
				{
				}
			}
			try
			{
				rich.addMoney(900);
			}
			catch
			{
			}
			foreach (Actor item in list)
			{
				try
				{
					item.removeTrait("deceitful");
					item.removeTrait("heart_of_wizard");
					item.removeTrait("greedy");
				}
				catch
				{
				}
			}
			try
			{
				sly.addTrait("deceitful", pRemoveOpposites: true);
				wizard.addTrait("heart_of_wizard", pRemoveOpposites: true);
			}
			catch
			{
			}
			Court.Invalidate();
			yield return Possess(king);
			if (Check("the king in first person", InControl(king) && Royal.IsKing(king)))
			{
				yield return Part("seats", Seats(king, home, rich, sly, wizard));
				yield return Part("tiny realm", Tiny(king));
				yield return Part("appointments", Appointments(king, home));
				yield return Part("talk", Talk(king, home));
				yield return Part("in session", Session(king, home));
				yield return Part("nudges", Nudges(king, home, foe));
				yield return Part("holding court", Holding(king, home, foe));
				yield return Part("seated", Seated(king));
				yield return Part("the keep", Keep(king, home, foe));
				yield return Part("the king's other powers", Powers(king, home, foe));
				Court.SessionForTest = null;
				Court.GatedForTest = null;
			}
		}

		private IEnumerator Seats(Actor king, Kingdom home, Actor rich, Actor sly, Actor wizard)
		{
			HashSet<Actor> hashSet = new HashSet<Actor>();
			bool flag = true;
			Seat[] seats = Court.Seats;
			foreach (Seat s in seats)
			{
				Actor actor = Court.Holder(home, s);
				Note(Court.Title(s) + ": " + ((actor != null) ? WorldBoxMod.Describe(actor) : "empty") + ((actor != null && Court.EffectLine(home, s) != null) ? (" — " + Court.EffectLine(home, s)) : ""));
				if (actor == null || actor == king)
				{
					flag = false;
				}
				else
				{
					hashSet.Add(actor);
				}
			}
			Check("five seats, five people, none the king", flag && hashSet.Count == 5);
			Check("the Steward is the richest", Court.Holder(home, Seat.Steward) == rich, WorldBoxMod.Describe(Court.Holder(home, Seat.Steward)));
			Check("the Spymaster is the deceitful one", Court.Holder(home, Seat.Spymaster) == sly);
			Check("the Court mage has a wizard's heart", Court.Holder(home, Seat.Mage) == wizard);
			Check("a deceitful Spymaster makes agents cheaper", Royal.PriceOf(home, Royal.Deed.Bandits) < Royal.CostOf(Royal.Deed.Bandits) && Royal.PriceOf(home, Royal.Deed.Assassin) < Royal.CostOf(Royal.Deed.Assassin), $"bandits {Royal.PriceOf(home, Royal.Deed.Bandits)}/{Royal.CostOf(Royal.Deed.Bandits)}, assassin {Royal.PriceOf(home, Royal.Deed.Assassin)}/{Royal.CostOf(Royal.Deed.Assassin)}");
			Check("a wizard's heart makes the heavens cheaper", Royal.PriceOf(home, Royal.Deed.Meteor) < Royal.CostOf(Royal.Deed.Meteor), Court.EffectLine(home, Seat.Mage));
			Check("each one's line says so", (Court.EffectLine(home, Seat.Spymaster) ?? "").StartsWith("Deceitful") && (Court.EffectLine(home, Seat.Mage) ?? "").StartsWith("Wizard"));
			Check("a seat knows its holder", Court.SeatOf(sly, out var seat) && seat == Seat.Spymaster);
			yield return null;
		}

		private IEnumerator Tiny(Actor king)
		{
			Actor small = Village("elf", 3, 0, king.current_tile, 40f) ?? Village("elf", 3, 0);
			if (!Check("a three-person realm", small != null && small.kingdom != null))
			{
				yield break;
			}
			yield return CivilTest.Wait(1f);
			int num = 0;
			Seat[] seats = Court.Seats;
			foreach (Seat s in seats)
			{
				if (Court.Holder(small.kingdom, s) != null)
				{
					num++;
				}
			}
			Check("a tiny realm has empty seats", num == 2, num + " of 5 seats held");
			Seat s2 = Seat.Mage;
			seats = Court.Seats;
			foreach (Seat seat in seats)
			{
				if (!Court.Open(small.kingdom, seat))
				{
					s2 = seat;
					break;
				}
			}
			Check("an empty seat gives no nudge, and its part stays open", Court.EffectLine(small.kingdom, s2) == null && Hud.CourtPart(small, small.kingdom, s2, courtBusiness: true), Court.Title(s2));
		}

		private IEnumerator Appointments(Actor king, Kingdom home)
		{
			Actor actor = Court.Holder(home, Seat.Steward);
			Actor actor2 = null;
			foreach (Actor our in _ours)
			{
				if (our != null && our != king && our.isAlive() && our.isAdult() && !Court.SeatOf(our, out var _))
				{
					actor2 = our;
					break;
				}
			}
			if (Check("someone without a seat", actor2 != null))
			{
				Check("the king appoints a new Steward", Court.Appoint(home, Seat.Steward, actor2) && Court.Holder(home, Seat.Steward) == actor2 && Court.Appointed(home, Seat.Steward));
				string pResult = null;
				try
				{
					home.data.get("fp_court", out pResult, null);
				}
				catch
				{
				}
				Check("the realm keeps it", pResult?.Contains("steward:" + Court.IdOf(actor2)) ?? false, pResult);
				Check("the old Steward is out", actor == null || !Court.SeatOf(actor, out var seat2) || seat2 != Seat.Steward);
				Court.Appoint(home, Seat.Marshal, actor2);
				Actor actor3 = Court.Holder(home, Seat.Steward);
				Check("moved to Marshal, they leave the Steward's seat to the rule", Court.Holder(home, Seat.Marshal) == actor2 && actor3 != actor2 && actor3 != null, "Steward now " + WorldBoxMod.Describe(actor3));
				Check("the king can't sit on his own council", !Court.Appoint(home, Seat.Chancellor, king));
				yield return null;
			}
		}

		private IEnumerator Talk(Actor king, Kingdom home)
		{
			Actor spy = Court.Holder(home, Seat.Spymaster);
			CivilTest.StandBefore(spy, king, 1.2f);
			yield return CivilTest.Wait(0.3f);
			if (!Check("talking to the Spymaster", Mod.StartConversationForTest(spy)))
			{
				yield break;
			}
			Conversation c = Mod.Converse;
			yield return CivilTest.Wait(0.3f);
			Check("\"Your counsel\" and \"A seat on my council\"", Find(c, "Your counsel") != null && Find(c, "A seat on my council") != null, string.Join(" | ", c.Choices.ConvertAll((Choice x) => x.Label).ToArray()));
			Pick(c, "Your counsel");
			yield return CivilTest.Wait(0.2f);
			Note("counsel: \"" + c.Line + "\"");
			Check("a word on the agents", c.Line != null && c.Line.Contains("kills"), c.Line);
			Check("\"Show me\" works away from a keep (the court isn't a place yet)", Find(c, "Show me")?.Enabled ?? false);
			Pick(c, "Show me");
			yield return CivilTest.Wait(0.4f);
			Check("it opens the council at their part", Mod.Royal.Open && Court.Focus == Seat.Spymaster && !c.Open);
			yield return Capture("court_council.png");
			Mod.Royal.Open = false;
			yield return CivilTest.Wait(0.6f);
			Actor pick = null;
			foreach (Actor our in _ours)
			{
				if (our != null && our != king && our.isAlive() && our.isAdult() && !Court.SeatOf(our, out var _))
				{
					pick = our;
					break;
				}
			}
			if (pick != null)
			{
				CivilTest.StandBefore(pick, king, 1.2f);
				yield return CivilTest.Wait(0.3f);
				Mod.StartConversationForTest(pick);
				yield return CivilTest.Wait(0.3f);
				Pick(c, "A seat on my council");
				yield return CivilTest.Wait(0.2f);
				Pick(c, "Chancellor");
				yield return CivilTest.Wait(0.2f);
				Check("appointed in talk", Court.Holder(home, Seat.Chancellor) == pick, c.Line);
				Mod.EndConversation();
				yield return CivilTest.Wait(0.6f);
			}
		}

		private IEnumerator Session(Actor king, Kingdom home)
		{
			Court.GatedForTest = true;
			Court.SessionForTest = false;
			Check("away from the keep, court business stays open", Hud.CourtPart(king, home, Seat.Chancellor, courtBusiness: true) && Hud.CourtPart(king, home, Seat.Spymaster, courtBusiness: true) && Hud.CourtPart(king, home, Seat.Steward, courtBusiness: true));
			Check("Hold court says where the council sits", !Court.AtThrone(king) && Court.ThroneLine(king).StartsWith("The council sits in"), Court.ThroneLine(king));
			Mod.Royal.Open = true;
			yield return Capture("court_away.png", 0.6f);
			Mod.Royal.Open = false;
			Actor mage = Court.Holder(home, Seat.Mage);
			CivilTest.StandBefore(mage, king, 1.2f);
			yield return CivilTest.Wait(0.6f);
			Mod.StartConversationForTest(mage);
			Conversation c = Mod.Converse;
			yield return CivilTest.Wait(0.3f);
			Pick(c, "Your counsel");
			yield return CivilTest.Wait(0.2f);
			Choice choice = Find(c, "Show me");
			Check("away, \"Show me\" still opens the council", choice?.Enabled ?? false, choice?.Note);
			Mod.EndConversation();
			Court.GatedForTest = null;
			Court.SessionForTest = null;
			yield return CivilTest.Wait(0.6f);
		}

		private IEnumerator Nudges(Actor king, Kingdom home, Kingdom foe)
		{
			Actor actor = Court.Holder(home, Seat.Steward);
			try
			{
				actor.addTrait("greedy", pRemoveOpposites: true);
			}
			catch
			{
			}
			Court.Invalidate(home);
			try
			{
				home.capital.addResourcesToRandomStockpile("gold", 100);
			}
			catch
			{
			}
			int num = CivilTest.Money(actor);
			int num2 = Gold(foe.capital);
			string said = null;
			Mod.Royal.SendGift(king, foe, delegate(string m)
			{
				said = m;
				Note("toast: " + m);
			});
			int num3 = CivilTest.Money(actor) - num;
			int num4 = Gold(foe.capital) - num2;
			Check("a greedy Steward keeps a quarter of a gift", num3 == 10 && num4 == 30, $"kept {num3}, arrived {num4}: \"{said}\"");
			try
			{
				actor.removeTrait("greedy");
			}
			catch
			{
			}
			Actor actor2 = Court.Holder(home, Seat.Marshal);
			try
			{
				actor2.removeTrait("pyromaniac");
				actor2.removeTrait("veteran");
				actor2.removeTrait("genius");
				actor2.removeTrait("pacifist");
				actor2.addTrait("weak", pRemoveOpposites: true);
			}
			catch
			{
			}
			Court.Invalidate(home);
			Check("a weak Marshal drafts fewer", Mathf.Approximately(Court.LevyShare(home), 0.7f), Court.EffectLine(home, Seat.Marshal));
			try
			{
				actor2.removeTrait("weak");
			}
			catch
			{
			}
			Actor actor3 = Court.Holder(home, Seat.Chancellor);
			string[] array = new string[7] { "honest", "wise", "pacifist", "bloodlust", "ambitious", "hotheaded", "stupid" };
			foreach (string pTraitID in array)
			{
				try
				{
					actor3.removeTrait(pTraitID);
				}
				catch
				{
				}
			}
			try
			{
				actor3.addTrait("peaceful", pRemoveOpposites: true);
			}
			catch
			{
			}
			Court.Invalidate(home);
			Check("a peaceful Chancellor softens tribute", Court.TributeReach(home) > 1f && Court.TributeScale(home) < 1f, Court.EffectLine(home, Seat.Chancellor));
			try
			{
				actor3.removeTrait("peaceful");
				actor3.addTrait("honest", pRemoveOpposites: true);
			}
			catch
			{
			}
			Court.Invalidate(home);
			Check("an honest Chancellor needs less trust for an alliance", Court.AllianceShift(home) < 0, Court.EffectLine(home, Seat.Chancellor));
			yield return null;
		}

		private IEnumerator Holding(Actor king, Kingdom home, Kingdom foe)
		{
			Mod.EndConversation();
			CourtThrone.Envoys.Clear();
			try
			{
				foe.capital.addResourcesToRandomStockpile("gold", 80);
			}
			catch
			{
			}
			int ours = Gold(home.capital);
			CourtThrone.Envoys.Add(new CourtThrone.Envoy
			{
				From = foe,
				FromName = Royal.NameOf(foe),
				Kind = CourtThrone.Offer.Tribute,
				Gold = 20,
				Until = Time.unscaledTime + 300f
			});
			Kingdom third = null;
			try
			{
				foreach (Kingdom item in World.world.kingdoms.list)
				{
					if (item != null && item.isAlive() && item != home && item != foe && !item.isCiv())
					{
						third = item;
						break;
					}
				}
			}
			catch
			{
			}
			if (third == null)
			{
				try
				{
					foreach (Kingdom item2 in World.world.kingdoms.list)
					{
						if (item2 != null && item2.isAlive() && item2 != home && item2 != foe)
						{
							third = item2;
							break;
						}
					}
				}
				catch
				{
				}
			}
			if (third != null)
			{
				CourtThrone.Envoys.Add(new CourtThrone.Envoy
				{
					From = foe,
					To = third,
					FromName = Royal.NameOf(foe),
					Kind = CourtThrone.Offer.Alliance,
					Until = Time.unscaledTime + 300f
				});
			}
			Check("someone waits", CourtThrone.Waiting(king) >= 1, CourtThrone.Waiting(king) + " waiting");
			CourtThrone.Begin(king, Mod.Toast);
			yield return CivilTest.Wait(0.5f);
			Conversation c = Mod.Converse;
			Check("an envoy comes forward first", c.Open && CourtThrone.Holding && c.With != null && c.With.kingdom == foe && c.LastTopic == "envoy_offer", $"open {c.Open}, with {WorldBoxMod.Describe(c.With)}, topic {c.LastTopic}: \"{c.Line}\"");
			yield return Capture("court_envoy.png");
			Pick(c, "Accept");
			yield return CivilTest.Wait(0.3f);
			Check("their tribute arrives", Gold(home.capital) - ours == 20, $"{ours} -> {Gold(home.capital)}");
			c.LineStart -= 999f;
			c.End();
			yield return CivilTest.Wait(1.6f);
			Check("a petitioner comes next", c.Open && CourtThrone.Holding && c.LastTopic == "petition" && Dialogue.YouRule(king, c.With), $"open {c.Open}, holding {CourtThrone.Holding}, topic {c.LastTopic}, with {WorldBoxMod.Describe(c.With)}: \"{c.Line}\"");
			Note("petition: \"" + c.Line + "\"");
			Pick(c, "Refuse");
			yield return CivilTest.Wait(0.3f);
			Pick(c, "That is all for today.");
			yield return CivilTest.Wait(0.3f);
			Check("\"That is all for today.\" ends court", !CourtThrone.Holding);
			c.LineStart -= 999f;
			c.End();
			yield return CivilTest.Wait(1.2f);
			Check("and nobody else comes forward", !c.Open, c.Open ? WorldBoxMod.Describe(c.With) : null);
			if (third != null)
			{
				Check("the envoy sent to another king still waits for him", CourtThrone.Envoys.Exists((CourtThrone.Envoy e) => e.To == third), Royal.NameOf(third));
			}
			CourtThrone.Envoys.RemoveAll((CourtThrone.Envoy e) => e.To == third);
			Mod.Royal.DeclareWar(king, foe, delegate(string m)
			{
				Note("toast: " + m);
			}, delegate
			{
			});
			yield return CivilTest.Wait(0.5f);
			if (Check("at war with them", Royal.AtWar(home, foe)))
			{
				CourtThrone.Envoys.Add(new CourtThrone.Envoy
				{
					From = foe,
					FromName = Royal.NameOf(foe),
					Kind = CourtThrone.Offer.Peace,
					Gold = 0,
					Until = Time.unscaledTime + 300f
				});
				CourtThrone.Begin(king, Mod.Toast);
				yield return CivilTest.Wait(0.5f);
				Pick(c, "Accept");
				yield return CivilTest.Wait(0.5f);
				Check("their peace accepted, the war is over", !Royal.AtWar(home, foe));
				CourtThrone.Stop();
				Mod.EndConversation();
				Note($"audiences given: {CourtThrone.Heard}");
				yield return CivilTest.Wait(0.6f);
				if (third != null)
				{
					CourtThrone.Envoys.Add(new CourtThrone.Envoy
					{
						From = third,
						FromName = Royal.NameOf(third),
						Kind = CourtThrone.Offer.Alliance,
						Until = Time.unscaledTime + 300f
					});
					Mod.Royal.Open = true;
					yield return CivilTest.Wait(0.3f);
					CourtThrone.Ask(king, Mod.Toast);
					yield return CivilTest.Wait(0.5f);
					Check("Hold court from the window: it closes, court begins", CourtThrone.Holding && c.Open && !Mod.Royal.Open && c.With != null && c.With.kingdom == third, $"holding {CourtThrone.Holding}, talk {c.Open}, window {Mod.Royal.Open}, with {WorldBoxMod.Describe(c.With)}");
					Pick(c, "Refuse");
					yield return CivilTest.Wait(0.3f);
					CourtThrone.Stop();
					Mod.EndConversation();
					yield return CivilTest.Wait(0.6f);
				}
			}
		}

		private IEnumerator Seated(Actor king)
		{
			List<Actor> three = new List<Actor>();
			foreach (Actor our in _ours)
			{
				if (our != null && our != king && our.isAlive() && our.isAdult() && three.Count < 3)
				{
					three.Add(our);
				}
			}
			if (!Check("three people to seat", three.Count == 3))
			{
				yield break;
			}
			Mod.EnterRoomForTest(new List<Actor>(), "human", 4);
			yield return CivilTest.Wait(0.5f);
			Interior room = Mod.Interior.Room;
			Vector2 eye = Mod.Interior.Eye;
			float spawnYaw = room.SpawnYaw;
			float num = spawnYaw * 57.29578f + 180f;
			Vector2 vector = new Vector2(Mathf.Cos(spawnYaw), Mathf.Sin(spawnYaw));
			Vector2 vector2 = new Vector2(0f - vector.y, vector.x);
			Vector2 vector3 = eye + vector * 2.2f + vector2 * 0.75f;
			Vector2 vector4 = eye + vector * 2.2f - vector2 * 0.75f;
			Vector2 vector5 = eye + vector * 2.8f;
			List<RoomSeat> seats = new List<RoomSeat>
			{
				new RoomSeat
				{
					Who = three[0],
					X = vector3.x,
					Y = vector3.y,
					Yaw = num,
					Sit = 1f
				},
				new RoomSeat
				{
					Who = three[1],
					X = vector4.x,
					Y = vector4.y,
					Yaw = num + 90f,
					Sit = 1f
				},
				new RoomSeat
				{
					Who = three[2],
					X = vector5.x,
					Y = vector5.y,
					Yaw = num,
					Sit = 0f
				}
			};
			Mod.Interior.PinForTest(seats);
			yield return CivilTest.Wait(1.2f);
			int num2 = 0;
			foreach (RoomWalkers.Walker item in Mod.Interior.WalkersForTest)
			{
				if (item.Pinned)
				{
					num2++;
				}
			}
			Check("three held at their places", num2 == 3, num2 + " pinned");
			yield return CivilTest.Wait(2f);
			bool ok = true;
			foreach (RoomWalkers.Walker item2 in Mod.Interior.WalkersForTest)
			{
				if (item2.Pinned && (item2.Speed > 0.01f || item2.Path.Count > 0))
				{
					ok = false;
				}
			}
			Check("they stay put", ok);
			yield return Capture("court_seated.png", 0.3f);
			Mod.Interior.PinForTest(new List<RoomSeat>());
			Check("let go, they leave the room", Mod.Interior.WalkersForTest.Count == 0, Mod.Interior.WalkersForTest.Count + " left");
			Mod.LeaveRoomForTest();
			yield return CivilTest.Wait(0.5f);
		}

		private IEnumerator Keep(Actor king, Kingdom home, Kingdom foe)
		{
			City cap = king.city;
			Building hall = Court.HallOf(cap);
			if (hall == null || hall.asset == null || hall.asset.id != "hall_human_2")
			{
				if (hall != null)
				{
					try
					{
						hall.startDestroyBuilding();
					}
					catch
					{
					}
				}
				yield return CivilTest.Wait(0.5f);
				hall = AddHall("hall_human_2", cap);
			}
			yield return CivilTest.Wait(1.5f);
			try
			{
				if (hall != null && hall.isUnderConstruction())
				{
					hall.completeConstruction();
				}
			}
			catch
			{
			}
			float named = 0f;
			while (!TownTier.IsCapital(cap) && named < 6f)
			{
				yield return CivilTest.Wait(0.25f);
				named += 0.25f;
			}
			if (!TownTier.IsCapital(cap))
			{
				try
				{
					home.capital = cap;
				}
				catch (Exception ex)
				{
					Note("capital: " + ex.Message);
				}
			}
			Building building = Court.CapitalHall(home);
			string detail = "placed " + hall?.asset?.id + " in " + hall?.city?.name + ", capital " + (home.hasCapital() ? home.capital.name : "none") + " (ours " + cap.name + "), its hall " + building?.asset?.id + ((building != null && building != hall) ? " (another)" : "") + $", building {hall?.isUnderConstruction()}";
			if (!Check("the capital has its keep", hall != null && building == hall, detail))
			{
				yield break;
			}
			string roomName = HallRooms.RoomFor(hall);
			float waited = 0f;
			while (roomName != null && !Models.PackReady(roomName) && waited < 8f)
			{
				yield return CivilTest.Wait(0.2f);
				waited += 0.2f;
			}
			Check("the court is a place now", Court.Gated(home), roomName);
			Check("outdoors, out of session but court business stays open", !Court.InSession(king) && Hud.CourtPart(king, home, Seat.Chancellor, courtBusiness: true) && Court.ThroneLine(king).StartsWith("The council sits in"), Court.ThroneLine(king));
			for (float t = 0f; t < 5f; t += 0.5f)
			{
				if (Mod.Interior.Inside)
				{
					break;
				}
				Mod.EnterHouseForTest(hall);
				yield return CivilTest.Wait(0.5f);
			}
			yield return CivilTest.Wait(1.5f);
			Interior room = (Mod.Interior.Inside ? Mod.Interior.Room : null);
			if (!Check("inside the keep", room != null && HallRooms.KindOf(room) == "keep" && Court.AtCourt(king), HallRooms.KindOf(room)))
			{
				if (Mod.Interior.Inside)
				{
					Mod.LeaveRoomForTest();
				}
				yield break;
			}
			Check("at the door: not at the table, and Hold court points to the throne", !Court.InSession(king) && Court.AwayLine(king) == "Hold council at the war table." && !Court.AtThrone(king) && Court.ThroneLine(king) == "Hold court from the throne.", Court.ThroneLine(king));
			float hour = Mod.Day.Hour;
			bool flag = hour > 0.27f && hour < 0.75f;
			int num = 0;
			int num2 = 0;
			Seat[] seats = Court.Seats;
			foreach (Seat s in seats)
			{
				Actor actor = Court.Holder(home, s);
				try
				{
					if (actor != null && actor.current_tile.zone.city == cap)
					{
						num++;
					}
				}
				catch
				{
				}
			}
			foreach (RoomWalkers.Walker item in Mod.Interior.WalkersForTest)
			{
				if (item.Pinned && item.Sit > 0.5f)
				{
					num2++;
				}
			}
			Check(flag ? "the councillors in town sit at the table" : "at night the chamber is empty", (!flag) ? (num2 == 0) : (num2 == num && num2 > 0), $"hour {hour:F2}: {num2} seated, {num} councillors in town");
			Anchors.Spot[] array = HallRooms.Spots(room, "a_seat");
			Anchors.Spot[] array2 = HallRooms.Spots(room, "a_war_table");
			if (!Check("the king's chair and the war table", array.Length == 6 && array2.Length == 1))
			{
				Mod.LeaveRoomForTest();
				yield break;
			}
			Vector2 vector = new Vector2(array[0].At.X, array[0].At.Y);
			Vector2 mid = new Vector2(array2[0].At.X, array2[0].At.Y);
			Mod.Interior.Eye = vector + (mid - vector).normalized * 0.3f;
			Mod.SetYaw(Mathf.Atan2(mid.y - vector.y, mid.x - vector.x));
			yield return CivilTest.Wait(0.6f);
			Check("at the table the council is in session", Court.InSession(king) && Hud.CourtPart(king, home, Seat.Chancellor, courtBusiness: true));
			Check("court is held from the throne, not the table", !Court.AtThrone(king));
			Check("Hold council at the table (E, or Shift+E on the hold-E wheel when a councillor is under the crosshair)", Mod.Prompt == "E:Hold council" || (Mod.Prompt != null && Mod.Prompt.EndsWith("|Shift+E:Hold council")), Mod.Prompt);
			yield return Capture("court_keep_table.png", 0.2f);
			Court.OpenCouncil(null);
			yield return Capture("court_keep_council.png", 0.6f);
			Mod.Royal.Open = false;
			yield return CivilTest.Wait(0.5f);
			Anchors.Spot[] array3 = HallRooms.Spots(room, "a_throne");
			Anchors.Spot[] array4 = HallRooms.Spots(room, "a_petition");
			if (!Check("the throne and the petitioner's place", array3.Length == 1 && array4.Length == 1))
			{
				Mod.LeaveRoomForTest();
				yield break;
			}
			Vector2 vector2 = new Vector2(array3[0].At.X, array3[0].At.Y);
			Vector2 pe = new Vector2(array4[0].At.X, array4[0].At.Y);
			Vector2 normalized = (vector2 - pe).normalized;
			Vector2 vector3 = new Vector2(0f - normalized.y, normalized.x);
			Mod.Interior.Eye = pe + vector3 * 0.9f + normalized * 0.4f;
			Mod.SetYaw(Mathf.Atan2(vector2.y - Mod.Interior.Eye.y, vector2.x - Mod.Interior.Eye.x));
			yield return CivilTest.Wait(0.6f);
			Check("Hold court at the throne", Mod.Prompt != null && Mod.Prompt.StartsWith("E:Hold court") && Court.AtThrone(king), Mod.Prompt);
			CourtThrone.Envoys.Clear();
			CourtThrone.Envoys.Add(new CourtThrone.Envoy
			{
				From = foe,
				FromName = Royal.NameOf(foe),
				Kind = CourtThrone.Offer.Alliance,
				Until = Time.unscaledTime + 300f
			});
			CourtThrone.Begin(king, Mod.Toast);
			yield return CivilTest.Wait(1.6f);
			Conversation c = Mod.Converse;
			RoomWalkers.Walker walker = null;
			foreach (RoomWalkers.Walker item2 in Mod.Interior.WalkersForTest)
			{
				if (item2.Pinned && c.Open && item2.Who == c.With)
				{
					walker = item2;
				}
			}
			Check("the envoy stands before the throne", walker != null && walker.Sit <= 0f && (walker.Pos - pe).magnitude < 0.05f, (walker != null) ? $"at {walker.Pos.x:F1},{walker.Pos.y:F1}, place {pe.x:F1},{pe.y:F1}" : ("not in the room: " + WorldBoxMod.Describe(c.With)));
			Mod.SetYaw(Mathf.Atan2(pe.y - Mod.Interior.Eye.y, pe.x - Mod.Interior.Eye.x));
			yield return Capture("court_keep_audience.png", 0.5f);
			Pick(c, "Accept");
			yield return CivilTest.Wait(0.4f);
			Check("their alliance accepted", Royal.Allied(home, foe));
			RoomWalkers.Walker sitter = null;
			foreach (RoomWalkers.Walker item3 in Mod.Interior.WalkersForTest)
			{
				if (item3.Pinned && item3.Sit > 0.5f)
				{
					sitter = item3;
					break;
				}
			}
			c.LineStart -= 999f;
			c.End();
			if (sitter != null)
			{
				Vector2 chairAt = sitter.Pos;
				Mod.Interior.Eye = chairAt + (mid - chairAt).normalized * 0.8f;
				Mod.StartConversationForTest(sitter.Who);
				yield return CivilTest.Wait(1.4f);
				RoomWalkers.Walker walker2 = null;
				foreach (RoomWalkers.Walker item4 in Mod.Interior.WalkersForTest)
				{
					if (item4.Who == sitter.Who)
					{
						walker2 = item4;
					}
				}
				Check("a councillor you talk to while court sits keeps their chair", c.Open && walker2 != null && walker2.Sit > 0.5f && (walker2.Pos - chairAt).magnitude < 0.05f, (walker2 != null) ? $"sit {walker2.Sit}, {(walker2.Pos - chairAt).magnitude:F2} from the chair, talk {c.Open}" : "not in the room");
				Pick(c, "That is all for today.");
				yield return CivilTest.Wait(0.3f);
			}
			CourtThrone.Stop();
			Mod.EndConversation();
			yield return CivilTest.Wait(1.2f);
			bool ok = true;
			foreach (RoomWalkers.Walker item5 in Mod.Interior.WalkersForTest)
			{
				if (item5.Pinned && item5.Sit <= 0f)
				{
					ok = false;
				}
			}
			Check("court over, nobody stands before the throne", ok);
			Mod.LeaveRoomForTest();
			yield return CivilTest.Wait(0.5f);
			Check("back outdoors, out of session again", !Court.InSession(king));
		}

		private IEnumerator Powers(Actor king, Kingdom home, Kingdom foe)
		{
			Royal royal = Mod.Royal;
			string said = null;
			Action<string> toast = delegate(string m)
			{
				said = m;
				Note("toast: " + m);
			};
			Actor small = Village("elf", 4, 0, king.current_tile, 40f) ?? Village("elf", 4, 0);
			if (Check("a realm with no stockpile", small != null && small.kingdom != null))
			{
				yield return CivilTest.Wait(0.6f);
				if (home.capital.getRandomStockpile() == null)
				{
					Stockpile(home.capital);
				}
				yield return CivilTest.Wait(0.3f);
				try
				{
					home.capital.addResourcesToRandomStockpile("gold", 100);
				}
				catch
				{
				}
				if (!Check("gold in our treasury to give", Gold(home.capital) >= 40, Gold(home.capital) + " gold"))
				{
					yield break;
				}
				try
				{
					if (Royal.AtWar(home, small.kingdom))
					{
						royal.MakePeace(king, small.kingdom, toast);
					}
				}
				catch
				{
				}
				int num = Gold(home.capital);
				bool flag = small.city.getRandomStockpile() == null;
				royal.ReadyForTest("gift");
				royal.SendGift(king, small.kingdom, toast);
				Check("a gift to a capital with no stockpile keeps your gold", !flag || (Gold(home.capital) == num && said != null && said.Contains("fine gifts")), $"stockpile there {!flag}, gold {num} -> {Gold(home.capital)}");
			}
			Actor fourth = Village("dwarf", 6, 0, king.current_tile, 45f) ?? Village("dwarf", 6, 0);
			if (small != null && fourth != null && fourth.kingdom != null)
			{
				yield return CivilTest.Wait(0.6f);
				Kingdom kingdom = small.kingdom;
				Kingdom kingdom2 = fourth.kingdom;
				try
				{
					Kingdom[] array = new Kingdom[4] { home, kingdom, foe, kingdom2 };
					foreach (Kingdom kingdom3 in array)
					{
						if (kingdom3.getAlliance() != null)
						{
							kingdom3.getAlliance().leave(kingdom3);
						}
					}
				}
				catch
				{
				}
				Alliance alliance = null;
				Alliance alliance2 = null;
				try
				{
					alliance = World.world.alliances.newAlliance(home, kingdom);
					alliance2 = World.world.alliances.newAlliance(foe, kingdom2);
				}
				catch (Exception ex)
				{
					Note("alliances: " + ex.Message);
				}
				if (Check("two alliances", alliance != null && alliance2 != null && alliance != alliance2))
				{
					try
					{
						Kingdom[] array = new Kingdom[2] { home, kingdom };
						foreach (Kingdom pK in array)
						{
							World.world.diplomacy.getRelation(pK, foe).data.timestamp_last_war_ended = World.world.getCurWorldTime();
						}
					}
					catch
					{
					}
					Note($"our alliance would take them: {alliance.canJoin(foe)}");
					bool flag2 = false;
					try
					{
						flag2 = Royal.Join(home, foe);
					}
					catch (Exception ex2)
					{
						Note("join: " + ex2.Message);
					}
					bool flag3 = alliance.hasKingdom(foe);
					bool flag4 = alliance2.hasKingdom(foe);
					Check("a realm is never left in two alliances", !(flag3 && flag4) && ((!flag2) ? flag4 : (flag3 && !flag4)), string.Format("joined {0}: in ours {1}, in its old one {2}, its alliance {3}", flag2, flag3, flag4, (foe.getAlliance() == alliance) ? "ours" : ((foe.getAlliance() == alliance2) ? "old" : "none")));
				}
			}
			City town = foe.capital;
			home.data.total_kills += 2000L;
			try
			{
				if (Royal.Allied(home, foe) && home.getAlliance() != null)
				{
					home.getAlliance().leave(home);
				}
			}
			catch
			{
			}
			if (!Royal.AtWar(home, foe))
			{
				royal.DeclareWar(king, foe, toast, delegate
				{
				});
			}
			yield return CivilTest.Wait(0.3f);
			royal.Unleash(king, town, Royal.Deed.Bandits, toast, delegate
			{
			});
			royal.Unleash(king, town, Royal.Deed.LightningStorm, toast, delegate
			{
			});
			int count = royal.Raids.Count;
			int count2 = royal.Storms.Count;
			if (Check("bandits and a storm on their town", count > 0 && count2 > 0, $"{count} raids, {count2} storms"))
			{
				Royal.RaidParty raid = royal.Raids[0];
				Royal.StormRun storm = royal.Storms[0];
				try
				{
					town.joinAnotherKingdom(home, pCaptured: true);
				}
				catch (Exception ex3)
				{
					Note("take the town: " + ex3.Message);
				}
				yield return CivilTest.Wait(2.5f);
				Check("the town is ours", town.kingdom == home);
				Check("then the raid on it ends", !royal.Raids.Contains(raid), raid.Status);
				Check("and the storm over it clears", !royal.Storms.Contains(storm), storm.Status);
			}
		}

		private Building AddHall(string id, City city)
		{
			try
			{
				WorldTile tile = city.getTile();
				for (int i = 0; i < 80; i++)
				{
					if (tile == null)
					{
						break;
					}
					float f = (float)i * 0.61f;
					float num = 6f + (float)i * 0.2f;
					WorldTile tile2 = World.world.GetTile(tile.x + (int)(Mathf.Cos(f) * num), tile.y + (int)(Mathf.Sin(f) * num));
					if (tile2 == null || tile2.Type == null || !tile2.Type.ground || tile2.Type.block || tile2.building != null)
					{
						continue;
					}
					Building building = World.world.buildings.addBuilding(id, tile2, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
					if (building == null)
					{
						continue;
					}
					try
					{
						if (tile2.zone != null)
						{
							List<TileZone> list = new List<TileZone> { tile2.zone };
							TileZone[] neighbours_all = tile2.zone.neighbours_all;
							foreach (TileZone tileZone in neighbours_all)
							{
								if (tileZone != null)
								{
									list.Add(tileZone);
								}
							}
							foreach (TileZone item in list)
							{
								if (item.city == null)
								{
									city.addZone(item);
								}
							}
						}
					}
					catch (Exception ex)
					{
						Note("land: " + ex.Message);
					}
					try
					{
						building.completeConstruction();
						building.setKingdom(city.kingdom);
					}
					catch
					{
					}
					if (building.city != city)
					{
						city.listBuilding(building);
					}
					return building;
				}
			}
			catch (Exception ex2)
			{
				Note("place " + id + ": " + ex2.Message);
			}
			return null;
		}

		private void Stockpile(City c)
		{
			try
			{
				WorldTile tile = c.getTile();
				for (int i = 0; i < 40; i++)
				{
					WorldTile tile2 = World.world.GetTile(tile.x + UnityEngine.Random.Range(-5, 6), tile.y + UnityEngine.Random.Range(-5, 6));
					if (tile2 == null || tile2.Type == null || !tile2.Type.ground || tile2.Type.block || (tile2.building != null && AssetStyle.IsSolid(tile2.building)))
					{
						continue;
					}
					Building building = World.world.buildings.addBuilding("stockpile", tile2, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
					if (building == null)
					{
						continue;
					}
					try
					{
						if (building.isUnderConstruction())
						{
							building.completeConstruction();
						}
					}
					catch
					{
					}
					if (building.city != c)
					{
						c.listBuilding(building);
					}
					return;
				}
				Note("no stockpile for " + c.name);
			}
			catch (Exception ex)
			{
				Note("stockpile: " + ex.Message);
			}
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
				Note("(no choice \"" + label + "\": " + string.Join(" | ", c.Choices.ConvertAll((Choice x) => x.Label).ToArray()) + ")");
			}
			else
			{
				c.LineStart -= 999f;
				c.Pick(c.Choices.IndexOf(choice));
			}
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
