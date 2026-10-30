using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal sealed class ServiceTest : CivilTest
	{
		private Actor _kingA;

		private Actor _kingB;

		private Actor _host;

		private City _a;

		private City _b;

		private readonly List<Actor> _peopleA = new List<Actor>();

		private readonly List<string> _toasts = new List<string>();

		private string _lastToast;

		private Actor _oldCaptain;

		private readonly List<Actor> _raiders = new List<Actor>();

		protected override string Prefix => "SERVICE";

		private Hud H => Mod.HudForTest;

		protected override IEnumerator Body()
		{
			_kingA = Village("human", 13, 0);
			if (!Check("a first village", _kingA != null))
			{
				yield break;
			}
			_a = _kingA.city;
			foreach (Actor item in Founded)
			{
				if (item != null && item != _kingA && item.isAdult())
				{
					_peopleA.Add(item);
				}
			}
			_kingB = Village("human", 10, 0, _kingA.current_tile, 30f);
			if (!Check("a second village", _kingB != null))
			{
				yield break;
			}
			_b = _kingB.city;
			_host = _peopleA.Find((Actor u) => !u.isWarrior() && !u.isCityLeader());
			if (!Check("a villager who isn't a soldier", _host != null))
			{
				yield break;
			}
			try
			{
				((BaseSimObject)_host).addStatusEffect("invincible", 600f, pColorEffect: false);
				((BaseSimObject)_kingA).addStatusEffect("invincible", 600f, pColorEffect: false);
			}
			catch
			{
			}
			yield return Possess(_host);
			if (!Check("first person on a villager", InControl(_host)))
			{
				yield break;
			}
			yield return Part("no map yet", NoMap());
			yield return Part("enlisting", Enlisting());
			yield return Part("the captain on the map", CaptainOnMap());
			yield return Part("made captain", Promotion());
			yield return Part("war", War());
			yield return Part("defend", Defend());
			yield return Part("a town taken", Taken());
			yield return Part("leaving the army", Leaving());
			yield return Part("a founder", Founder());
			string text = "";
			try
			{
				using (FileStream stream = new FileStream(Path.Combine(Log.Folder, "FirstPerson.log"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					using (StreamReader streamReader = new StreamReader(stream))
					{
						text = streamReader.ReadToEnd();
					}
				}
			}
			catch (Exception ex)
			{
				Note("log: " + ex.Message);
			}
			int num = 0;
			string[] array = text.Split('\n');
			foreach (string text2 in array)
			{
				if (text2.Contains("ERROR") && (text2.Contains("service") || text2.Contains("soldier map")))
				{
					num++;
					Note("error seen: " + text2.Trim());
				}
			}
			Check("no service errors in the log", text.Length > 0 && num == 0, num + " errors");
		}

		private IEnumerator Shot(string name)
		{
			yield return CivilTest.Wait(0.6f);
			H.CaptureHudForTest(Path.Combine(Log.Folder, name));
			for (int i = 0; i < 30; i++)
			{
				if (!H.CapturePending)
				{
					break;
				}
				yield return null;
			}
			yield return null;
		}

		private void Update()
		{
			if (!(Mod == null))
			{
				string toastText = Mod.ToastText;
				if (toastText != null && toastText != _lastToast)
				{
					_lastToast = toastText;
					_toasts.Add(toastText);
					Note("toast: " + toastText);
				}
			}
		}

		private bool Toasted(string part)
		{
			return _toasts.Exists((string t) => t.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0);
		}

		private static Choice Find(Conversation c, string start)
		{
			for (int i = 0; i < 8; i++)
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
			return c.Choices.Find((Choice ch) => ch.Label.StartsWith(start, StringComparison.Ordinal));
		}

		private static string Labels(Conversation c)
		{
			return c.Line + " / " + string.Join(" | ", c.Choices.ConvertAll((Choice x) => x.Label).ToArray());
		}

		private IEnumerator TalkTo(Actor npc)
		{
			Mod.EndConversation();
			_host.current_position = npc.current_position - new Vector2(1.3f, 0f);
			try
			{
				npc.makeWait(6f);
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.3f);
			Mod.StartConversationForTest(npc);
		}

		private bool Say(Conversation c, string start)
		{
			Choice choice = Find(c, start);
			if (choice == null)
			{
				return false;
			}
			c.LineStart -= 999f;
			c.Pick(c.Choices.IndexOf(choice));
			return true;
		}

		private IEnumerator Until(Func<bool> done, float max = 8f, Action each = null)
		{
			for (float t = 0f; t < max; t += 0.1f)
			{
				each?.Invoke();
				bool flag = false;
				try
				{
					flag = done();
				}
				catch
				{
				}
				if (flag)
				{
					break;
				}
				yield return CivilTest.Wait(0.1f);
			}
		}

		private void MapKey()
		{
			if (!Mod.MapWindowOpen)
			{
				WorldNews.Banner = null;
				MapTabs.Last = null;
			}
			Mod.MapKeyForTest();
		}

		private IEnumerator Fresh()
		{
			yield return CivilTest.Wait(0.7f);
		}

		private IEnumerator NoMap()
		{
			_toasts.Clear();
			MapKey();
			yield return null;
			yield return null;
			Check("M without service opens no soldier's map", !Mod.SoldierMap.Open && !Mod.WarMap.Open);
			if (!Toasted("Enlist"))
			{
				MapKey();
				yield return null;
			}
			Check("no orders", Mod.Orders == null);
		}

		private IEnumerator Enlisting()
		{
			Conversation c = Mod.Converse;
			if (_host.isCityLeader())
			{
				Actor actor = _peopleA.Find((Actor u) => u != _host && u.isAlive() && !u.isWarrior());
				try
				{
					if (actor != null)
					{
						_a.setLeader(actor, pNew: true);
					}
				}
				catch (Exception ex)
				{
					Note("leader: " + ex.Message);
				}
				Note("the host led the town; now " + ((_a.leader != null) ? Service.NameOf(_a.leader) : "nobody"));
			}
			yield return TalkTo(_kingA);
			Check("the king offers to enlist you", Find(c, "I want to enlist.") != null, Labels(c));
			Say(c, "I want to enlist.");
			Check("as a soldier or a founder", Find(c, "As a soldier.") != null && Find(c, "As a founder.") != null, Labels(c));
			Say(c, "As a soldier.");
			Check("he tells you the duties and asks you to swear", Find(c, "I swear to serve") != null, Labels(c));
			Say(c, "I swear to serve");
			Mod.EndConversation();
			yield return Fresh();
			Check("you're a warrior of your town", _host.isWarrior() && _host.city == _a, $"warrior {_host.isWarrior()}, town {_host.city?.name}");
			Check("a soldier", Service.IsSoldier(_host));
			Order orders = Mod.Orders;
			Check("at peace: walk the rounds of your town", orders != null && orders.Kind == OrderKind.Patrol && orders.HasWhere && orders.Title.StartsWith("Walk the rounds"), (orders?.Kind).ToString() + " " + orders?.Title);
			Check("orders from the king", orders?.From.Contains(Service.NameOf(_kingA)) ?? false, orders?.From);
			Check("enlisting is told", Toasted("You enlisted"), _lastToast);
			int posts = Service.PostsForTest.Count;
			Check("posts round the town", posts >= 1, $"{posts} posts");
			int purse = CivilTest.Money(_host);
			try
			{
				_kingA.addMoney(200);
			}
			catch
			{
			}
			for (int i = 0; i < posts && i < 4; i++)
			{
				orders = Mod.Orders;
				if (orders == null || !orders.HasWhere)
				{
					break;
				}
				Vector2 post = orders.Where;
				bool walked = false;
				for (float t = 0f; t < 30f; t += Time.unscaledDeltaTime)
				{
					if (walked)
					{
						break;
					}
					Vector2 vector = post - _host.current_position;
					Mod.SetYaw(Mathf.Atan2(vector.y, vector.x));
					PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
					PossessionHooks.ScriptedRun = true;
					yield return null;
					Order orders2 = Mod.Orders;
					walked = orders2 == null || !orders2.HasWhere || orders2.Where != post;
				}
				PossessionHooks.ScriptedInput = Vector2.zero;
				PossessionHooks.ScriptedRun = false;
				if (!walked)
				{
					Note($"post {i + 1} at {post.x:F0},{post.y:F0} not reached on foot in 30 s ({(post - _host.current_position).magnitude:F1} tiles off): set down 3.5 tiles from it");
					Vector2 vector2 = _host.current_position - post;
					_host.current_position = post + ((vector2.sqrMagnitude > 0.01f) ? vector2.normalized : Vector2.right) * 3.5f;
					yield return Fresh();
					yield return CivilTest.Wait(1.2f);
				}
				Check($"post {i + 1}: checked" + (walked ? ", walked to" : " within reach"), Mod.Orders == null || !Mod.Orders.HasWhere || Mod.Orders.Where != post);
				yield return CivilTest.Wait(0.6f);
			}
			orders = Mod.Orders;
			Check("rounds walked: off duty", orders != null && orders.Kind == OrderKind.Patrol && !orders.HasWhere && orders.Title.StartsWith("Off duty"), orders?.Title);
			Check("...and the crown pays", Toasted("Rounds walked"), _lastToast);
			Note($"purse {purse} -> {CivilTest.Money(_host)}");
			yield return TalkTo(_kingA);
			Check("a soldier asks about orders instead", Find(c, "About my orders...") != null && Find(c, "I want to enlist.") == null, Labels(c));
			Mod.EndConversation();
		}

		private IEnumerator CaptainOnMap()
		{
			Actor captain = null;
			foreach (Actor item in _peopleA)
			{
				if (item != _host && item.isAlive() && item.isWarrior())
				{
					captain = item;
					break;
				}
			}
			if (!Check("another soldier to lead", captain != null))
			{
				yield break;
			}
			try
			{
				if (!_a.hasArmy())
				{
					World.world.armies.newArmy(captain, _a);
				}
				if (_a.army.getCaptain() != captain)
				{
					_a.army.setCaptain(captain);
				}
				foreach (Actor item2 in _peopleA)
				{
					if (item2.isAlive() && item2.isWarrior())
					{
						item2.setArmy(_a.army);
					}
				}
				((BaseSimObject)captain).addStatusEffect("invincible", 600f, pColorEffect: false);
			}
			catch (Exception ex)
			{
				Note("army: " + ex.Message);
			}
			yield return Fresh();
			Check("the town's army has a captain", Service.CaptainOf(_a) == captain, (Service.CaptainOf(_a) != null) ? Service.NameOf(Service.CaptainOf(_a)) : "none");
			MapKey();
			yield return CivilTest.Wait(0.6f);
			SoldierMap map = Mod.SoldierMap;
			Check("M opens the soldier's map", map.Open && !map.Founder && !Mod.WarMap.Open);
			Check("the captain is on it", map.Captain == captain && map.Dots.Exists((SoldierMap.Dot d) => d.Captain && d.Actor == captain), $"{map.Dots.Count} dots");
			Check("your fellow soldiers are on it", map.Dots.Exists((SoldierMap.Dot d) => d.Army && !d.Captain));
			Check("your town is on it", map.Towns.Exists((SoldierMap.Town t) => t.Home && t.City == _a));
			yield return Shot("service_peace.png");
			Check("the captain is on your compass", HasMark(captain.current_position, 0.45f, 0.75f, 1f));
			MapKey();
			Check("M again closes it", !map.Open);
			MapKey();
			yield return null;
			Check("...and Esc closes it", map.Open && Mod.CivilEscape() && !map.Open);
		}

		private IEnumerator Promotion()
		{
			_toasts.Clear();
			_oldCaptain = Service.CaptainOf(_a);
			Check("not a captain yet", !Service.IsCaptain(_host) && Service.Merit(_host) < 3f, $"merit {Service.Merit(_host):F2}");
			_host.data.set("fp_enlisted", (float)(Quests.Now - 240.0));
			yield return Until(() => Service.IsCaptain(_host) && Toasted("makes you captain"));
			Check("years of service make you captain", Service.IsCaptain(_host) && _a.army.getCaptain() == _host, string.Format("merit {0:F2}, captain {1}", Service.Merit(_host), (Service.CaptainOf(_a) != null) ? Service.NameOf(Service.CaptainOf(_a)) : "none"));
			Check("the crown says so", Toasted("makes you captain"), _lastToast);
			Check("the old captain is a soldier again", _oldCaptain == null || (_oldCaptain.isWarrior() && Service.CaptainOf(_a) != _oldCaptain));
			if (_oldCaptain != null && _oldCaptain.isAlive())
			{
				try
				{
					_a.army.setCaptain(_oldCaptain);
				}
				catch (Exception ex)
				{
					Note("captain back: " + ex.Message);
				}
				yield return CivilTest.Wait(0.6f);
				yield return Until(() => Service.IsCaptain(_host), 5f);
				yield return CivilTest.Wait(1.8000001f);
				Check("a captaincy lost is taken back quietly", Service.IsCaptain(_host) && _toasts.FindAll((string t) => t.Contains("makes you captain")).Count == 1, _toasts.FindAll((string t) => t.Contains("makes you captain")).Count + " announcements");
			}
			yield return Fresh();
			Order orders = Mod.Orders;
			Check("your orders say you lead", orders?.YouLead ?? false, orders?.Title);
			foreach (Actor item in _peopleA)
			{
				try
				{
					if (item.isAlive() && item.isWarrior() && item.army != _a.army)
					{
						item.setArmy(_a.army);
					}
				}
				catch
				{
				}
			}
			List<Actor> soldiers = Service.ArmyOf(_host);
			Check("soldiers to command", soldiers.Count >= 2, soldiers.Count + " soldiers");
			MapKey();
			yield return CivilTest.Wait(0.6f);
			SoldierMap soldierMap = Mod.SoldierMap;
			Check("the map is a captain's", soldierMap.Open && soldierMap.Captaincy);
			Check("your soldiers are on it", soldierMap.Dots.FindAll((SoldierMap.Dot d) => d.Army).Count >= soldiers.Count, soldierMap.Dots.FindAll((SoldierMap.Dot d) => d.Army).Count + " of " + soldiers.Count);
			Vector2 current_position = _host.current_position;
			_ = current_position + new Vector2(12f, 0f);
			WorldTile tile = null;
			for (int num = 0; num < 12; num++)
			{
				if (tile != null && tile.Type.ground && !tile.Type.block && !tile.is_liquid)
				{
					break;
				}
				tile = World.world.GetTile((int)(current_position.x + 12f - (float)num), (int)current_position.y);
			}
			if (!Check("a place to send them", tile != null))
			{
				yield break;
			}
			Vector2 there = new Vector2((float)tile.x + 0.5f, (float)tile.y + 0.5f);
			List<Actor> army = Service.ArmyOf(_host);
			float before = 0f;
			foreach (Actor item2 in army)
			{
				before += (item2.current_position - there).magnitude;
			}
			int heard = Service.CommandArmy(_host, Service.CommandKind.March, tile, "Holding your mark");
			Check("they hear the command", heard == army.Count && army.TrueForAll((Actor a) => !a.isAlive() || ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai)?.task?.id == "fp_royal_march"), heard + " heard of " + army.Count);
			yield return CivilTest.Wait(0.7f);
			yield return Shot("service_captain.png");
			yield return CivilTest.Wait(5f);
			army.RemoveAll((Actor a) => !a.isAlive() || a.army != _host.army);
			float num2 = 0f;
			foreach (Actor item3 in army)
			{
				num2 += (item3.current_position - there).magnitude;
			}
			Check("they march there", army.Count > 0 && num2 / (float)army.Count < before / (float)Mathf.Max(1, heard) - 2f, $"{before / (float)Mathf.Max(1, heard):F1} -> {((army.Count > 0) ? (num2 / (float)army.Count) : 0f):F1} tiles away on average");
			Service.CommandArmy(_host, Service.CommandKind.Follow, null, "Following you");
			yield return CivilTest.Wait(0.5f);
			Check("'follow me' calls them off the march", soldiers.TrueForAll((Actor a) => !a.isAlive() || ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai)?.task?.id != "fp_royal_march"));
			bool flag = Mod.CaptainPingForTest(PingKind.Go, new Vector3(there.x, there.y, 0f));
			List<Actor> list = Service.ArmyOf(_host);
			Check("a 'Go here' ping commands them", flag && Service.Command == Service.CommandKind.March && Service.CommandTile == tile && list.Count > 0 && list.TrueForAll((Actor a) => ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai)?.task?.id == "fp_royal_march"), $"{list.Count} in the army");
			Check("...a 'Look' ping doesn't", !Mod.CaptainPingForTest(PingKind.Look, new Vector3(there.x, there.y, 0f)));
			Service.CommandArmy(_host, Service.CommandKind.Follow, null, "Following you");
			MapKey();
		}

		private bool HasMark(Vector2 at, float r, float g, float b)
		{
			Vector2 vector = at - _host.current_position;
			if (vector.sqrMagnitude <= 9f)
			{
				return true;
			}
			float num = Mathf.Atan2(vector.y, vector.x);
			foreach (KeyValuePair<float, Color> item in Mod.CompassMarks())
			{
				if (Mathf.Abs(Mathf.DeltaAngle(item.Key * 57.29578f, num * 57.29578f)) < 3f && Mathf.Abs(item.Value.r - r) < 0.05f && Mathf.Abs(item.Value.g - g) < 0.05f && Mathf.Abs(item.Value.b - b) < 0.05f)
				{
					return true;
				}
			}
			return false;
		}

		private IEnumerator War()
		{
			_toasts.Clear();
			foreach (Actor item in new List<Actor>(World.world.units.getSimpleList()))
			{
				try
				{
					if (item != null && item.isAlive() && item.kingdom == _b.kingdom && item.current_tile?.zone?.city == _a)
					{
						item.dieSimpleNone();
					}
				}
				catch
				{
				}
			}
			try
			{
				World.world.diplomacy.startWar(_a.kingdom, _b.kingdom, WarTypeLibrary.normal, pLog: false);
			}
			catch (Exception ex)
			{
				Note("war: " + ex.Message);
			}
			yield return Until(() => Toasted("muster") || Toasted("New orders"));
			Check("at war", Royal.AtWar(_a.kingdom, _b.kingdom));
			Order orders = Mod.Orders;
			bool flag = _a.target_attack_city != null;
			Check("war: muster (or march if the game sent the army)", orders != null && (orders.Kind == OrderKind.Muster || (flag && orders.Kind == OrderKind.Attack)), (orders?.Kind).ToString() + " " + orders?.Title);
			Check("a call to arms is heard", Toasted("muster") || Toasted("New orders"), _lastToast);
			int num = Service.Kills(_host);
			Actor actor = CivilTest.SpawnNear("sheep", _host.current_position, 3f);
			if (actor != null)
			{
				CivilTest.Kill(actor);
				Quests.OnKill(actor);
			}
			Check("a sheep felled is no merit", actor != null && Service.Kills(_host) == num, $"{num} -> {Service.Kills(_host)}");
			Actor actor2 = CivilTest.SpawnNear("human", Service.CityPoint(_b), 3f);
			try
			{
				actor2.joinCity(_b);
				_b.makeWarrior(actor2);
			}
			catch (Exception ex2)
			{
				Note("enemy: " + ex2.Message);
			}
			if (actor2 != null)
			{
				CivilTest.Kill(actor2);
				Quests.OnKill(actor2);
			}
			Check("an enemy soldier felled is", actor2 != null && Service.Kills(_host) == num + 1, $"{num} -> {Service.Kills(_host)}");
			yield return Until(() => Toasted("New orders") && Mod.Orders != null && Mod.Orders.Kind == OrderKind.Attack, 8f, delegate
			{
				_a.target_attack_city = _b;
				_a.target_attack_zone = _b.getTile()?.zone;
			});
			orders = Mod.Orders;
			Check("ordered to march on their town", orders != null && orders.Kind == OrderKind.Attack && orders.Target == _b, (orders?.Kind).ToString() + " " + orders?.Title);
			ServiceTest serviceTest = this;
			bool ok = orders != null && orders.HasWhere && (orders.Where - Service.CityPoint(_b)).magnitude < 12f;
			object detail;
			Vector2 vector;
			if (orders == null)
			{
				detail = "";
			}
			else
			{
				vector = orders.Where;
				detail = vector.ToString() + " vs " + Service.CityPoint(_b).ToString();
			}
			serviceTest.Check("the order points at it", ok, (string)detail);
			Check("new orders are told", Toasted("New orders"), _lastToast);
			Check("the target is on your compass", HasMark(orders?.Where ?? Vector2.zero, 1f, 0.32f, 0.24f));
			MapKey();
			yield return CivilTest.Wait(0.6f);
			SoldierMap soldierMap = Mod.SoldierMap;
			Check("the map shows the orders", soldierMap.Open && soldierMap.Order != null && soldierMap.Order.Kind == OrderKind.Attack && soldierMap.Goal(out vector));
			Check("their town is the target on it", soldierMap.Towns.Exists((SoldierMap.Town t) => t.Target && t.City == _b));
			Check("their soldiers are on it", soldierMap.Dots.Exists((SoldierMap.Dot d) => !d.Ours));
			yield return Shot("service_attack.png");
			MapKey();
			yield return Shot("service_hud.png");
		}

		private IEnumerator Defend()
		{
			_toasts.Clear();
			Vector2 at = Service.CityPoint(_a);
			for (int i = 0; i < 3; i++)
			{
				Actor actor = CivilTest.SpawnNear("human", at + new Vector2(2f + (float)i, 1f));
				if (actor != null)
				{
					try
					{
						actor.joinCity(_b);
						_b.makeWarrior(actor);
						actor.makeWait();
					}
					catch (Exception ex)
					{
						Note("raider: " + ex.Message);
					}
					_raiders.Add(actor);
				}
			}
			Check("raiders in your town", _raiders.Count >= 2 && _raiders.TrueForAll((Actor r) => r.kingdom == _b.kingdom), _raiders.Count + " raiders");
			yield return Until(() => Toasted("Defend") && Mod.Orders != null && Mod.Orders.Kind == OrderKind.Defend, 8f, delegate
			{
				_a.target_attack_city = _b;
			});
			yield return CivilTest.Wait(1.8000001f);
			Order orders = Mod.Orders;
			Check("called home to defend it", orders != null && orders.Kind == OrderKind.Defend && orders.Target == _a, (orders?.Kind).ToString() + " " + orders?.Title);
			Check("pointing at the raiders", orders != null && (orders.Where - at).magnitude < 10f, orders?.Where.ToString());
			Check("...and told so", Toasted("Defend"), _lastToast);
			Check("...once (no flicker)", _toasts.FindAll((string t) => t.Contains("Defend")).Count == 1, _toasts.FindAll((string t) => t.Contains("Defend")).Count + " times");
			foreach (Actor raider in _raiders)
			{
				CivilTest.Kill(raider);
			}
			int cleared = 0;
			yield return Until(() => Mod.Orders != null && Mod.Orders.Kind == OrderKind.Attack, 10f, delegate
			{
				_a.target_attack_city = _b;
				foreach (Actor item in new List<Actor>(World.world.units.getSimpleList()))
				{
					try
					{
						if (item != null && item.isAlive() && item.current_tile?.zone?.city == _a && Service.IsFoe(_a.kingdom, item))
						{
							CivilTest.Kill(item);
							cleared++;
						}
					}
					catch
					{
					}
				}
			});
			orders = Mod.Orders;
			Check("the raiders gone, back to the march", orders != null && orders.Kind == OrderKind.Attack, (orders?.Kind).ToString() + " " + orders?.Title + $" ({cleared} more foes cleared from town)");
		}

		private IEnumerator Taken()
		{
			_toasts.Clear();
			try
			{
				_kingA.addMoney(100);
			}
			catch
			{
			}
			int before = CivilTest.Money(_host);
			int kingBefore = CivilTest.Money(_kingA);
			int treasuryBefore = Economy.Treasury(_a);
			Order order = Service.OrderFor(_host, Mod.Royal, Mod.WarMap);
			try
			{
				_b.joinAnotherKingdom(_a.kingdom, pCaptured: true);
			}
			catch (Exception ex)
			{
				Note("capture: " + ex.Message);
			}
			string text;
			try
			{
				_ = order?.Key;
				text = order?.Key + Service.OrderFor(_host, Mod.Royal, Mod.WarMap)?.Key;
			}
			catch (Exception ex2)
			{
				Note("held order: " + ex2);
				text = null;
			}
			Check("an order of a wiped-out kingdom still reads", order == null || text != null, text);
			yield return Until(() => Toasted("is taken"));
			Check("their town is ours", _b.kingdom == _a.kingdom, _b.kingdom?.name);
			int gain = CivilTest.Money(_host) - before;
			Check("the crown pays for it", gain > 0 && Toasted("is taken") && _toasts.Exists((string t) => t.Contains("pays you " + gain + " gold")), $"{before} -> {CivilTest.Money(_host)}; {_lastToast}");
			Check("...out of the treasury or the king's purse, not from nowhere", CivilTest.Money(_kingA) + Economy.Treasury(_a) <= kingBefore + treasuryBefore - gain + 1, $"king {kingBefore} -> {CivilTest.Money(_kingA)}, treasury {treasuryBefore} -> {Economy.Treasury(_a)}");
		}

		private IEnumerator Leaving()
		{
			Conversation c = Mod.Converse;
			yield return TalkTo(_kingA);
			Say(c, "About my orders...");
			Check("he tells you your orders", c.Line != null && Find(c, "I want to leave the army.") != null, Labels(c));
			Say(c, "I want to leave the army.");
			Check("you can go", Say(c, "Yes. I'm done"), Labels(c));
			Mod.EndConversation();
			yield return Fresh();
			Check("no longer a soldier", !_host.isWarrior() && Mod.Orders == null, $"warrior {_host.isWarrior()}");
		}

		private IEnumerator Founder()
		{
			_toasts.Clear();
			Conversation c = Mod.Converse;
			List<Actor> list = _peopleA.FindAll((Actor u) => u != _host && u.isAlive() && !u.isCityLeader() && !u.isKing() && u.city == _host.city);
			if (!Check("people for a family", list.Count >= 3, list.Count + " in town"))
			{
				yield break;
			}
			Actor lover = list.Find((Actor u) => u.lover == null) ?? list[0];
			Actor child = list.Find((Actor u) => u != lover);
			try
			{
				_host.becomeLoversWith(lover);
				child.setParent1(_host);
			}
			catch (Exception ex)
			{
				Note("family: " + ex.Message);
			}
			Actor stranger = list.Find((Actor u) => u != lover && u != child && Service.Relation(_host, u) == null);
			Check("a lover and a child", Service.Relation(_host, lover) == "lover" && (Service.Relation(_host, child) == "son" || Service.Relation(_host, child) == "daughter"), Service.Relation(_host, lover) + " / " + Service.Relation(_host, child));
			Check("a neighbour is no family", stranger != null && Service.Relation(_host, stranger) == null);
			yield return TalkTo(_kingA);
			Say(c, "I want to enlist.");
			Say(c, "As a founder.");
			Check("he finds you free land", Find(c, "I'll found it.") != null, Labels(c));
			Check("...and names your family to go with you", c.Line != null && c.Line.IndexOf("your lover " + Service.NameOf(lover), StringComparison.OrdinalIgnoreCase) >= 0, c.Line);
			Say(c, "I'll found it.");
			Mod.EndConversation();
			yield return Fresh();
			Commission com = Mod.Charge;
			if (!Check("a charge to found a village", com != null))
			{
				yield break;
			}
			string pResult = null;
			try
			{
				_host.data.get("fp_found", out pResult, null);
			}
			catch
			{
			}
			Check("kept in your saved data", !string.IsNullOrEmpty(pResult), pResult);
			List<Actor> settlers = Service.SettlersOf(_host);
			Check("your family goes with you", settlers.Contains(lover) && settlers.Contains(child), settlers.Count + " settlers");
			Check("only your family", settlers.TrueForAll((Actor s) => Service.Relation(_host, s) != null) && !settlers.Contains(stranger), string.Join(", ", settlers.ConvertAll((Actor s) => Service.NameOf(s) + " " + (Service.Relation(_host, s) ?? "none")).ToArray()));
			Check("the site is on your compass", HasMark(com.Site, 0.45f, 1f, 0.4f));
			MapKey();
			yield return CivilTest.Wait(0.8f);
			SoldierMap soldierMap = Mod.SoldierMap;
			Check("M opens the founder's map", soldierMap.Open && soldierMap.Founder);
			Check("free land shown on it", soldierMap.Sites.Count > 0, soldierMap.Sites.Count + " sites");
			MapKey();
			int x0 = com.X;
			int y0 = com.Y;
			Actor actor = CivilTest.Spawn("human", com.Site);
			bool built = false;
			try
			{
				built = actor?.buildCityAndStartCivilization() ?? false;
			}
			catch (Exception ex2)
			{
				Note("rival village: " + ex2.Message);
			}
			if (built)
			{
				yield return Until(() => Toasted("settled your site"));
			}
			com = Mod.Charge;
			Check("a site settled first moves to free land", !built || (com != null && (com.X != x0 || com.Y != y0) && Toasted("settled your site")), $"rival village {built}; site {x0},{y0} -> {com?.X},{com?.Y}");
			if (com == null)
			{
				yield break;
			}
			MapKey();
			yield return CivilTest.Wait(0.5f);
			yield return Shot("service_founder.png");
			MapKey();
			Vector2 site = com.Site;
			Vector2 current_position = _host.current_position;
			Vector2 current_position2 = current_position + (site - current_position).normalized * Mathf.Min(14f, (site - current_position).magnitude * 0.5f);
			_host.current_position = current_position2;
			yield return CivilTest.Wait(0.3f);
			List<float> before = settlers.ConvertAll((Actor a) => (a.current_position - _host.current_position).magnitude);
			bool toYou = false;
			bool nearer = false;
			string seen = "";
			for (float t = 0f; t < 7f; t += 0.5f)
			{
				if (nearer)
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
				for (int num = 0; num < settlers.Count; num++)
				{
					Actor actor2 = settlers[num];
					if (actor2.isAlive())
					{
						float magnitude = (actor2.current_position - _host.current_position).magnitude;
						if (magnitude < before[num] - 2f || magnitude < 6f)
						{
							nearer = true;
						}
						WorldTile beh_tile_target = actor2.beh_tile_target;
						if (((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)actor2.ai)?.task?.id == "fp_royal_march" && beh_tile_target != null && (new Vector2((float)beh_tile_target.x + 0.5f, (float)beh_tile_target.y + 0.5f) - _host.current_position).magnitude < 5f)
						{
							toYou = true;
						}
						if (t >= 6.5f || nearer)
						{
							seen += $"{Service.NameOf(actor2)} {before[num]:F1}->{magnitude:F1} task {((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)actor2.ai)?.task?.id} inside {actor2.is_inside_building}; ";
						}
					}
				}
			}
			Check("a settler walks after you", settlers.Count == 0 || nearer || toYou, $"nearer {nearer}, marching to you {toYou}: {seen}");
			yield return Until(delegate
			{
				Commission commission = Service.CommissionOf(_host);
				if (commission != null)
				{
					_host.current_position = commission.Site;
				}
				return Service.FoundableHere(_host) != null && Mod.Prompt != null && Mod.Prompt.Contains("Found a village");
			}, 10f);
			Check("free land under you", Service.FoundableHere(_host) != null, $"at {_host.current_position}, site {Service.CommissionOf(_host)?.Site}");
			Check("the prompt says so", Mod.Prompt != null && Mod.Prompt.Contains("Found a village"), Mod.Prompt);
			int cities = World.world.cities.Count;
			City town = Service.Found(_host, Mod.Toast);
			yield return CivilTest.Wait(1f);
			Check("a new village", town != null && town.isAlive() && World.world.cities.Count == cities + 1, town?.name);
			if (town == null)
			{
				yield break;
			}
			Check("of your kingdom", town.kingdom == _a.kingdom, town.kingdom?.name);
			Check("you live in it", _host.city == town, _host.city?.name);
			Check("you lead it", town.leader == _host, (town.leader != null) ? Service.NameOf(town.leader) : "none");
			Check("your settlers live there too", settlers.TrueForAll((Actor s) => !s.isAlive() || s.city == town), string.Join(",", settlers.ConvertAll((Actor s) => s.city?.name ?? "-").ToArray()));
			Check("the charge is done", Service.CommissionOf(_host) == null && Mod.Charge == null);
			Check("founding is told", Toasted("You founded"), _lastToast);
			yield return TalkTo(_kingA);
			Say(c, "I want to enlist.");
			Say(c, "As a founder.");
			Say(c, "I'll found it.");
			Mod.EndConversation();
			yield return Fresh();
			if (Check("a second charge", Mod.Charge != null))
			{
				_toasts.Clear();
				try
				{
					_a.kingdom.setKing(_host);
				}
				catch (Exception ex3)
				{
					Note("crown: " + ex3.Message);
				}
				yield return Until(() => Toasted("crown"));
				Check("crowned, the charge is void", _host.isKing() && Service.CommissionOf(_host) == null && Toasted("crown"), $"king {_host.isKing()}; {_lastToast}");
				Check("...and E founds nothing", Service.FoundableHere(_host) == null);
			}
		}
	}
}
