using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class BountyTest : CivilTest
	{
		private Actor _host;

		private Actor _king;

		private City _city;

		private readonly List<Actor> _people = new List<Actor>();

		protected override string Prefix => "BOUNTY";

		protected override IEnumerator Body()
		{
			_king = Village("human", 18, 2);
			if (!Check("a village", _king != null))
			{
				yield break;
			}
			_city = _king.city;
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive() && item != _king && item.isAdult())
				{
					_people.Add(item);
				}
			}
			_host = _people[0];
			try
			{
				_host.addMoney(600);
				((BaseSimObject)_host).addStatusEffect("invincible", 300f, pColorEffect: false);
			}
			catch (Exception ex)
			{
				Note("setup: " + ex.Message);
			}
			yield return Possess(_host);
			if (Check("first person on a villager", InControl(_host)))
			{
				yield return Part("the board", Board());
				yield return Part("crimes", Crimes());
				yield return Part("a foe's board", ForeignBoard());
				yield return Part("leaping out", Leap());
			}
		}

		private IEnumerator ForeignBoard()
		{
			Kingdom ka = _king.kingdom;
			int owed = Law.BountyOn(ka, _host);
			Law.SetBounty(ka, _host, 0);
			Law.StandDown(ka, _host);
			Actor kingB = Village("human", 8, 0, _city.getTile(), 40f);
			if (!Check("a second realm", kingB != null && kingB.kingdom != ka))
			{
				yield break;
			}
			List<Actor> theirs = new List<Actor>();
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive() && item != kingB && item.isAdult())
				{
					theirs.Add(item);
				}
			}
			Kingdom kb = kingB.kingdom;
			try
			{
				World.world.diplomacy.startWar(kb, ka, WarTypeLibrary.normal, pLog: false);
			}
			catch (Exception ex)
			{
				Note("war: " + ex.Message);
			}
			yield return CivilTest.Wait(0.5f);
			if (!Check("the realms are at war", ka.isEnemy(kb)))
			{
				yield break;
			}
			Actor ours = _people.Find((Actor u) => u != null && u.isAlive() && u != _host && !u.isKing() && !u.isCityLeader());
			Actor foe = theirs.Find((Actor u) => u != null && u.isAlive() && !u.isCityLeader());
			if (ours != null)
			{
				Service.Enlist(ours);
			}
			if (foe != null)
			{
				Service.Enlist(foe);
			}
			if (!Check("a soldier of each realm", ours != null && ours.isWarrior() && foe != null && foe.isWarrior()))
			{
				yield break;
			}
			yield return CivilTest.Wait(5.5f);
			Law.Board b = null;
			foreach (Law.Board item2 in Law.BoardsNear(kingB.current_position, 200f))
			{
				if (item2.City != null && item2.City.kingdom == kb)
				{
					b = item2;
				}
			}
			if (Check("the foe's town has a board", b != null))
			{
				ours.current_position = b.At + new Vector2(8f, 3f);
				try
				{
					ours.makeWait(20f);
					foe.makeWait(20f);
				}
				catch
				{
				}
				yield return CivilTest.Wait(0.5f);
				if (!ours.isWarrior())
				{
					Service.Enlist(ours);
				}
				if (!foe.isWarrior())
				{
					Service.Enlist(foe);
				}
				b.PostersAt = -99f;
				List<Poster> list = new List<Poster>(Law.Posters(b, null));
				Check("the board posts your realm's soldier as its foe", list.Exists((Poster p) => p.Id == ours.data.id), string.Join(" | ", list.ConvertAll((Poster p) => p.Name + " (" + p.Why + ")").ToArray()));
				b.PostersAt = -99f;
				List<Poster> list2 = Law.Posters(b, _host);
				Check("but not for you: killing them would be murder", !list2.Exists((Poster p) => p.Id == ours.data.id), string.Join(" | ", list2.ConvertAll((Poster p) => p.Name + " (" + p.Why + ")").ToArray()));
				Check("their soldier is fair game", Law.Lawful(foe, _host), $"alive {foe.isAlive()}, warrior {foe.isWarrior()}, realm {foe.kingdom?.name}, yours {_host.kingdom?.name}, at war {_host.kingdom != null && foe.kingdom != null && _host.kingdom.isEnemy(foe.kingdom)}");
				Check("your own soldier is not", !Law.Lawful(ours, _host), (ours.has_attack_target && ours.attack_target == _host) ? "they're fighting you" : null);
				Quest q = new Quest
				{
					Kind = QuestKind.Bounty,
					FromBoard = true,
					Giver = b.City.data.id,
					GiverName = b.City.name,
					Target = ours.data.id,
					TargetName = Dialogue.NameOf(ours),
					Money = 50,
					Renown = 6,
					Xp = 20
				};
				if (Check("an old bounty on your realm's soldier", Quests.Accept(q, _host, null)))
				{
					yield return CivilTest.Wait(2f);
					Check("it's withdrawn", !Quests.Log.Contains(q) && Law.TakenAt(b.City) == null, Quests.Log.Contains(q) ? Quests.Status(q) : null);
					Law.SetBounty(ka, _host, owed);
				}
			}
		}

		private Law.Board BoardHere()
		{
			foreach (Law.Board item in Law.BoardsNear(_host.current_position, 200f))
			{
				if (item.City == _city)
				{
					return item;
				}
			}
			return null;
		}

		private static Choice Find(Conversation c, string start)
		{
			return c.Choices.Find((Choice ch) => ch.Label.StartsWith(start, StringComparison.Ordinal));
		}

		private IEnumerator Board()
		{
			yield return CivilTest.Wait(5.5f);
			Law.Board b = BoardHere();
			if (!Check("the town has a bounty board", b != null))
			{
				yield break;
			}
			Building building = null;
			foreach (Building building2 in _city.buildings)
			{
				if (building2 != null && building2.isAlive() && building2.asset != null && building2.asset.type == "type_hall")
				{
					building = building2;
				}
			}
			Note(string.Format("board at {0:F1},{1:F1} facing {2:F0} degrees; hall {3}", b.At.x, b.At.y, b.Yaw * 57.29578f, (building != null) ? (building.current_tile.x + "," + building.current_tile.y) : "none yet"));
			Actor wolf = CivilTest.SpawnNear("wolf", b.At, 14f);
			Actor bandit = CivilTest.SpawnNear("bandit", b.At, 22f);
			try
			{
				wolf?.makeWait(30f);
				bandit?.makeWait(30f);
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.5f);
			b.PostersAt = -99f;
			List<Poster> posters = Law.Posters(b, _host);
			Note("posters: " + string.Join(" | ", posters.ConvertAll((Poster p) => p.Name + " (" + p.Why + ", " + p.Reward + " gold)").ToArray()));
			Poster wp = posters.Find((Poster p) => p.Target == wolf);
			Check("the wolf is on the board", wp != null);
			Check("the bandit is on the board", posters.Exists((Poster p) => p.Target == bandit));
			Vector2 face = new Vector2(Mathf.Cos(b.Yaw), Mathf.Sin(b.Yaw));
			_host.current_position = b.At + face * 1.6f;
			Mod.SetYaw(b.Yaw + MathF.PI);
			Mod.SetPitchDegrees(-4f);
			yield return CivilTest.Wait(1.2f);
			Check("facing it, E reads the bounty board", Mod.Prompt != null && Mod.Prompt.Contains("bounty board"), Mod.Prompt);
			Mod.SaveFrame("bounty_board.png");
			Mod.DebugEye = b.At + face * 3.5f;
			Mod.DebugEyeSet = true;
			yield return CivilTest.Wait(0.8f);
			Mod.SaveFrame("bounty_board_far.png");
			Mod.DebugEyeSet = false;
			Law.OpenBoard = b;
			Law.BoardOpen = true;
			yield return CivilTest.Wait(0.5f);
			try
			{
				ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "bounty_panel.png"));
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.3f);
			Check("the board's window holds the keys", Mod.GameKeysMuted);
			if (wp == null)
			{
				Law.BoardOpen = false;
				yield break;
			}
			bool flag = Law.Take(b, wp, _host, Mod.Toast);
			Quest q = Quests.Log.Find((Quest x) => x.FromBoard && x.Target == wolf.data.id);
			Check("the bounty is taken", flag && q != null);
			Check("it's kept in the town's data", Law.TakenAt(_city) != null);
			Check("a second bounty from the same board is refused", !Law.Take(b, posters.Find((Poster p) => p.Target == bandit) ?? wp, _host, null));
			Law.BoardOpen = false;
			if (q != null)
			{
				Quests.OnKill(wolf);
				CivilTest.Kill(wolf);
				yield return CivilTest.Wait(0.8f);
				Check("killing it marks the bounty ready to claim", q.Stage == 1, Quests.Status(q));
				int num = CivilTest.Money(_host);
				Check("claimed at the board", Law.Claim(b, _host, Mod.Toast) && CivilTest.Money(_host) >= num + q.Money, $"+{CivilTest.Money(_host) - num} gold, promised {q.Money}");
				Check("the board can be taken from again", Law.TakenAt(_city) == null);
				if (bandit != null)
				{
					CivilTest.Kill(bandit);
				}
			}
		}

		private Actor Bystander(Vector2 near, float within)
		{
			foreach (Actor person in _people)
			{
				if (person != null && person.isAlive() && person != _host && (person.current_position - near).magnitude < within)
				{
					return person;
				}
			}
			return null;
		}

		private IEnumerator Crimes()
		{
			Kingdom k = _king.kingdom;
			Law.SetBounty(k, _host, 0);
			Actor actor = null;
			foreach (Actor person in _people)
			{
				if (person != null && person.isAlive() && person != _host && !person.isWarrior())
				{
					actor = person;
					break;
				}
			}
			if (!Check("someone to wrong", actor != null))
			{
				yield break;
			}
			int num = 0;
			foreach (Actor person2 in _people)
			{
				if (person2 != null && person2.isAlive() && person2 != _host && person2 != actor && num < 4)
				{
					person2.current_position = actor.current_position + new Vector2(2f + (float)num, 1.5f);
					try
					{
						person2.makeWait(5f);
					}
					catch
					{
					}
					num++;
				}
			}
			_host.current_position = actor.current_position - new Vector2(1.2f, 0f);
			Quests.OnKill(actor);
			CivilTest.Kill(actor);
			int bounty = Law.BountyOn(k, _host);
			Check("a murder in front of witnesses puts a price on your head", bounty >= 100, $"{bounty} gold, {Law.LastWitnesses} witnesses");
			Check("you're wanted in their kingdom", Law.WantedIn(_host).Exists((KeyValuePair<Kingdom, int> kv) => kv.Key == k));
			k.data.get("fp_wanted", out var pResult, null);
			Check("the bounty is kept in the kingdom's own data", pResult?.Contains(_host.data.id.ToString()) ?? false, pResult);
			Actor guard = null;
			foreach (Actor person3 in _people)
			{
				if (person3 != null && person3.isAlive() && person3 != _host && person3.isWarrior())
				{
					guard = person3;
					break;
				}
			}
			if (guard != null)
			{
				guard.current_position = _host.current_position + new Vector2(6f, 2f);
			}
			for (float t = 0f; t < 5f; t += 0.5f)
			{
				yield return CivilTest.Wait(0.5f);
				if (guard != null && guard.has_attack_target && guard.attack_target == _host)
				{
					break;
				}
			}
			int num2 = 0;
			foreach (Actor item in Civil.SubjectsOf(k))
			{
				if (item != null && item.isAlive() && item.has_attack_target && item.attack_target == _host)
				{
					num2++;
				}
			}
			Check("the guards come for you", num2 > 0, num2 + " after you");
			Check("the HUD shows you're wanted", Law.WantedIn(_host).Count > 0);
			try
			{
				ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "bounty_wanted.png"));
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.3f);
			Conversation c = Mod.Converse;
			_host.current_position = _king.current_position - new Vector2(1.3f, 0f);
			try
			{
				_king.makeWait(6f);
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.3f);
			int num3 = CivilTest.Money(_host);
			Mod.StartConversationForTest(_king);
			for (int num4 = 0; num4 < 8; num4++)
			{
				if (c.Choices.Count <= 0)
				{
					break;
				}
				if (!(c.Choices[0].Label == "Go on..."))
				{
					break;
				}
				c.Next();
				c.Next();
			}
			Choice choice = Find(c, "I'll pay my fine");
			Check("the king offers to take your fine", choice?.Enabled ?? false, (choice != null) ? choice.Label : string.Join(" | ", c.Choices.ConvertAll((Choice x) => x.Label).ToArray()));
			if (choice != null)
			{
				c.LineStart -= 999f;
				c.Pick(c.Choices.IndexOf(choice));
			}
			Mod.EndConversation();
			Check("the fine is paid and the bounty gone", Law.BountyOn(k, _host) == 0 && CivilTest.Money(_host) == num3 - bounty, $"{num3} -> {CivilTest.Money(_host)}");
			yield return CivilTest.Wait(0.5f);
			num2 = 0;
			foreach (Actor item2 in Civil.SubjectsOf(k))
			{
				if (item2 != null && item2.isAlive() && item2.has_attack_target && item2.attack_target == _host)
				{
					num2++;
				}
			}
			Check("the guards stand down", num2 == 0, num2 + " still after you");
			Actor lone = null;
			foreach (Actor person4 in _people)
			{
				if (person4 != null && person4.isAlive() && person4 != _host && !person4.isKing())
				{
					lone = person4;
				}
			}
			if (lone != null)
			{
				Vector2 vector = (lone.current_position = FarFromAll(k, 40f));
				_host.current_position = vector - new Vector2(1.2f, 0f);
				yield return CivilTest.Wait(0.2f);
				Quests.OnKill(lone);
				CivilTest.Kill(lone);
				Check("an unseen killing costs nothing", Law.BountyOn(k, _host) == 0, Law.LastWitnesses + " witnesses");
			}
			Actor a2 = null;
			foreach (Actor person5 in _people)
			{
				if (person5 != null && person5.isAlive() && person5 != _host && !person5.isKing() && !person5.isWarrior())
				{
					a2 = person5;
					break;
				}
			}
			if (a2 != null)
			{
				Actor actor2 = Bystander(a2.current_position, 50f);
				if (actor2 != null)
				{
					actor2.current_position = a2.current_position + new Vector2(2f, 0f);
				}
				_host.current_position = a2.current_position - new Vector2(1.2f, 0f);
				yield return CivilTest.Wait(0.2f);
				Law.OnHit(a2);
				Check("an assault seen adds to it", Law.BountyOn(k, _host) == 25, Law.BountyOn(k, _host) + " gold");
				Law.WatchTheft(a2);
				try
				{
					a2.changeHappiness("got_robbed");
				}
				catch
				{
				}
				yield return CivilTest.Wait(0.3f);
				Check("a theft seen adds to it", Law.BountyOn(k, _host) == 65, Law.BountyOn(k, _host) + " gold");
				Regard regard = Chronicle.RegardOf(a2, _host, create: false);
				Check("the victim remembers and thinks less of you", regard != null && regard.Crimes >= 1 && regard.Opinion < 0, (regard != null) ? (regard.Opinion.ToString() ?? "") : null);
			}
		}

		private static Vector2 FarFromAll(Kingdom k, float distance)
		{
			Vector2 zero = Vector2.zero;
			int num = 0;
			foreach (Actor item in Civil.SubjectsOf(k))
			{
				if (item != null && item.isAlive())
				{
					zero += item.current_position;
					num++;
				}
			}
			if (num > 0)
			{
				zero /= (float)num;
			}
			float[] array = new float[4] { 45f, 60f, 80f, 100f };
			foreach (float num2 in array)
			{
				for (int j = 0; j < 12; j++)
				{
					Vector2 vector = zero + new Vector2(Mathf.Cos((float)j * 0.52f), Mathf.Sin((float)j * 0.52f)) * num2;
					WorldTile tile = World.world.GetTile((int)vector.x, (int)vector.y);
					if (tile == null || tile.main_type == null || tile.main_type.liquid)
					{
						continue;
					}
					bool flag = true;
					foreach (Actor item2 in Civil.SubjectsOf(k))
					{
						if (item2 != null && item2.isAlive() && (item2.current_position - vector).magnitude < distance * 0.5f)
						{
							flag = false;
							break;
						}
					}
					if (flag)
					{
						return vector;
					}
				}
			}
			return zero + new Vector2(60f, 0f);
		}

		private IEnumerator Leap()
		{
			Kingdom k = _king.kingdom;
			int bounty = Law.BountyOn(k, _host);
			if (bounty <= 0)
			{
				Law.SetBounty(k, _host, 50);
				bounty = 50;
			}
			Actor other = null;
			foreach (Actor person in _people)
			{
				if (person != null && person.isAlive() && person != _host)
				{
					other = person;
					break;
				}
			}
			if (other != null)
			{
				Actor old = _host;
				yield return Possess(other, 1.2f);
				Check("in another body", InControl(other));
				Check("the new body isn't wanted", Law.WantedIn(other).Count == 0);
				Check("the bounty stays on the body you left", Law.BountyOn(k, old) == bounty, Law.BountyOn(k, old) + " gold");
			}
		}
	}
}
