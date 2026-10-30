using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson.Towns
{
	internal static class Patrons
	{
		public const string Key = "fp_gifts";

		private const string WalkTask = "fp_patron_walk";

		private const float HonoredMin = 300f;

		private const float HonoredMax = 480f;

		private const float ChampionMin = 180f;

		private const float ChampionMax = 300f;

		private const float FirstMin = 40f;

		private const float FirstMax = 90f;

		private const float Reach = 28f;

		private const float HandOver = 2.4f;

		private const float WalkLimit = 45f;

		private const float WaitLimit = 25f;

		private static Actor _giver;

		private static Actor _for;

		private static long _giverId;

		private static City _from;

		private static float _tick;

		private static float _walking;

		private static float _waiting;

		private static float _resend;

		private static bool _registered;

		private static readonly System.Random Rng = new System.Random();

		private static City _offerFrom;

		private static int _offerGold;

		private static Gear.Piece _offerPiece;

		internal static bool Off;

		private static readonly string[] Metals = new string[5] { "leather", "copper", "bronze", "iron", "steel" };

		private static readonly string[] Slots = new string[3] { "armor", "helmet", "boots" };

		internal static Actor GiverForTest => _giver;

		internal static City FromForTest => _from;

		public static void Update(Actor host, float dt)
		{
			if (host != _for)
			{
				Drop("a new body");
			}
			_for = host;
			if (host == null || Off)
			{
				return;
			}
			if (_giver != null)
			{
				Walk(host, dt);
			}
			else
			{
				if ((_tick -= dt) > 0f)
				{
					return;
				}
				_tick = 2f;
				City here = Hub.Here;
				if (here == null || !Honours(host, here) || !Free(host))
				{
					return;
				}
				double now = Quests.Now;
				double num = NextAt(host, here);
				if (num < 0.0)
				{
					SetNext(host, here, now + 40.0 + Rng.NextDouble() * 50.0);
				}
				else if (!(now < num))
				{
					Actor actor = PickGiver(host, here);
					if (actor == null)
					{
						SetNext(host, here, now + 30.0);
						return;
					}
					Register();
					_giver = actor;
					_giverId = actor.data.id;
					_from = here;
					_walking = (_waiting = (_resend = 0f));
					Send(actor, host);
					Log.Info("patrons: " + WorldBoxMod.Describe(actor) + " of " + Rep.NameOf(here) + " sets out with a gift (" + Rep.Name(Rep.Level(host, here)) + ")");
				}
			}
		}

		public static bool Honours(Actor body, City town)
		{
			return Rep.AtLeast(body, town, RepLevel.Honored);
		}

		private static bool Free(Actor host)
		{
			if (!Hub.KeysFree || Hub.InRoom || Hub.AnyWindowOpen || Hub.AnyModeActive)
			{
				return false;
			}
			if (Economy.InAFight(host))
			{
				return false;
			}
			try
			{
				if (host.is_inside_boat || host.is_inside_building)
				{
					return false;
				}
			}
			catch
			{
				return false;
			}
			WorldBoxMod instance = WorldBoxMod.Instance;
			if (instance != null)
			{
				return !instance.Converse.Open;
			}
			return false;
		}

		private static Actor PickGiver(Actor host, City town)
		{
			List<Actor> list = new List<Actor>();
			try
			{
				Vector2 current_position = host.current_position;
				WorldTile current_tile = host.current_tile;
				long id = host.data.id;
				foreach (Actor unit in town.units)
				{
					if (unit != null && unit != host && unit.isAlive() && unit.isAdult() && Dialogue.IsPerson(unit) && unit != host.lover && unit.data.parent_id_1 != id && unit.data.parent_id_2 != id && !unit.isKing() && !unit.isWarrior() && !unit.is_inside_building && !unit.is_inside_boat && !FamilyFollow.Asleep(unit) && !unit.has_attack_target)
					{
						float magnitude = (unit.current_position - current_position).magnitude;
						if (!(magnitude < 3f) && !(magnitude > 28f) && (current_tile == null || unit.current_tile == null || unit.current_tile.isSameIsland(current_tile)))
						{
							list.Add(unit);
						}
					}
				}
			}
			catch (Exception e)
			{
				Hub.Fail("patrons pick", e);
			}
			if (list.Count != 0)
			{
				return list[Rng.Next(list.Count)];
			}
			return null;
		}

		private static void Walk(Actor host, float dt)
		{
			bool flag;
			try
			{
				flag = _giver.isAlive() && _giver.data.id == _giverId && _from != null && _from.isAlive();
			}
			catch
			{
				flag = false;
			}
			if (!flag)
			{
				Drop("the giver is gone");
				return;
			}
			float magnitude = (_giver.current_position - host.current_position).magnitude;
			if (magnitude > 2.4f)
			{
				_walking += dt;
				if (_walking > 45f || magnitude > 44.8f)
				{
					Later(host, 60.0);
					Drop("the giver couldn't reach you");
				}
				else if ((_resend -= dt) <= 0f)
				{
					_resend = 1f;
					Send(_giver, host);
				}
			}
			else if (!Free(host))
			{
				_waiting += dt;
				if (_waiting > 25f)
				{
					Later(host, 60.0);
					Drop("you were busy");
				}
			}
			else
			{
				Actor giver = _giver;
				City city = _from;
				Release(giver);
				_giver = null;
				_from = null;
				Offer(host, giver, city);
			}
		}

		private static void Offer(Actor host, Actor giver, City from)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			if (instance == null || !instance.StartAudience(giver))
			{
				Later(host, 60.0);
				return;
			}
			Conversation c = instance.Converse;
			c.LastTopic = "gift";
			RepLevel level = Rep.Level(host, from);
			_offerFrom = from;
			_offerGold = 0;
			_offerPiece = null;
			int num = GoldFor(from, level);
			if (Rng.NextDouble() < 0.55 && Economy.CanPay(from, giver) >= num)
			{
				_offerGold = num;
			}
			else
			{
				_offerPiece = ArmorFor(from, level, giver);
			}
			string text = Rep.NameOf(from) ?? "this town";
			string text2 = ((_offerGold > 0) ? (_offerGold + " gold") : GiftName(_offerPiece));
			c.Say((_offerGold > 0) ? Dialogue.Pick(text + " doesn't forget what you've done for it. Take this. " + text2 + ".", "We put this together for you. " + text2 + ", from all of us.", "*presses a purse into your hands* " + text2 + ". You've earned it many times over.") : Dialogue.Pick("I made this for you. " + Cap(An(text2)) + ". May it keep you safe.", "*holds out " + An(text2) + "* People here wanted you to have it.", "For the one who stood by " + text + ". " + Cap(An(text2)) + ", my best work."));
			c.Offer(new List<Choice>
			{
				new Choice("Thank you.", delegate
				{
					Accept(c);
				}),
				new Choice("Keep it. The town needs it more.", delegate
				{
					Decline(c);
				})
			});
			SetNext(host, from, Quests.Now + Gap(level));
			Log.Info("patrons: " + WorldBoxMod.Describe(giver) + " offers " + ((_offerGold > 0) ? (_offerGold + " gold") : _offerPiece?.Id));
		}

		private static void Accept(Conversation c)
		{
			c.LastTopic = "gift_taken";
			Actor you = c.You;
			Actor with = c.With;
			if (_offerGold > 0)
			{
				int num = Economy.PayOut(you, _offerGold, _offerFrom, with);
				if (num > 0)
				{
					c.Toast?.Invoke($"A gift from {Rep.NameOf(_offerFrom)}, {num} gold");
					Sounds.PlayAt("event:/SFX/UI/YesPoint", you.current_position);
				}
				Log.Info($"patrons: took {num} of {_offerGold} gold");
			}
			else if (_offerPiece != null)
			{
				bool flag = Bag.PutPiece(you, _offerPiece, c.Toast);
				if (flag)
				{
					c.Toast?.Invoke("A gift from " + Rep.NameOf(_offerFrom) + ", " + An(GiftName(_offerPiece)));
				}
				Sounds.PlayAt("event:/SFX/UI/YesPoint", you.current_position);
				Log.Info("patrons: took " + _offerPiece.Id + " (" + (flag ? "bag" : "at your feet") + ")");
			}
			Chronicle.ChangeOpinion(with, you, 5);
			_offerGold = 0;
			_offerPiece = null;
			c.Say(Dialogue.Pick("Use it well.", "It's the least we could do.", "Come back to us safe."));
			c.Offer(Dialogue.Topics(c));
		}

		private static void Decline(Conversation c)
		{
			c.LastTopic = "gift_declined";
			Actor you = c.You;
			Chronicle.ChangeOpinion(c.With, you, 10);
			if (_offerFrom != null)
			{
				Rep.Add(you, _offerFrom, 25, "gift returned");
			}
			_offerGold = 0;
			_offerPiece = null;
			c.Say(Dialogue.Pick("*shakes their head, smiling* Then you have our thanks twice over.", "You're a better soul than most. I'll tell the others."));
			c.Offer(Dialogue.Topics(c));
		}

		internal static int GoldFor(City town, RepLevel level)
		{
			Tier tier = TownTier.Of(town);
			int num;
			switch (tier)
			{
			default:
				num = 25;
				break;
			case Tier.Town:
				num = 40;
				break;
			case Tier.City:
				num = 70;
				break;
			}
			int num2 = num;
			int num3;
			switch (tier)
			{
			default:
				num3 = 45;
				break;
			case Tier.Town:
				num3 = 80;
				break;
			case Tier.City:
				num3 = 140;
				break;
			}
			int num4 = num3;
			float num5 = (float)num2 + (float)Rng.NextDouble() * (float)(num4 - num2);
			if (level >= RepLevel.Champion)
			{
				num5 *= 1.5f;
			}
			return Mathf.Max(5, Mathf.RoundToInt(num5 / 5f) * 5);
		}

		internal static Gear.Piece ArmorFor(City town, RepLevel level, Actor maker)
		{
			int num;
			switch (TownTier.Of(town))
			{
			default:
				num = 0;
				break;
			case Tier.Town:
				num = 2;
				break;
			case Tier.City:
				num = 3;
				break;
			}
			int num2 = num + Rng.Next(2);
			if (level >= RepLevel.Champion)
			{
				num2++;
			}
			num2 = Mathf.Clamp(num2, 0, Metals.Length - 1);
			string text = null;
			try
			{
				text = maker?.getName();
			}
			catch
			{
			}
			Gear.Piece piece = new Gear.Piece
			{
				Id = Slots[Rng.Next(Slots.Length)] + "_" + Metals[num2],
				By = text
			};
			if (piece.Asset == null)
			{
				piece.Id = "armor_leather";
			}
			return piece;
		}

		internal static string GiftName(Gear.Piece p)
		{
			if (p == null)
			{
				return "gift";
			}
			if (!string.IsNullOrEmpty(p.Name))
			{
				return p.Name;
			}
			int num = p.Id.IndexOf('_');
			if (num <= 0)
			{
				return Gear.NameOf(p).ToLowerInvariant();
			}
			string text = p.Id.Substring(0, num);
			string text2 = p.Id.Substring(num + 1);
			if (!(text == "armor"))
			{
				if (!(text == "boots"))
				{
					return text2 + " " + text;
				}
				return "pair of " + text2 + " boots";
			}
			return "suit of " + text2 + " armor";
		}

		private static string An(string thing)
		{
			return ((thing.Length > 0 && "aeiou".IndexOf(thing[0]) >= 0) ? "an " : "a ") + thing;
		}

		private static string Cap(string s)
		{
			if (!string.IsNullOrEmpty(s))
			{
				return char.ToUpperInvariant(s[0]) + s.Substring(1);
			}
			return s;
		}

		private static double Gap(RepLevel level)
		{
			if (level < RepLevel.Champion)
			{
				return 300.0 + Rng.NextDouble() * 180.0;
			}
			return 180.0 + Rng.NextDouble() * 120.0;
		}

		private static void Later(Actor host, double wait)
		{
			if (_from != null)
			{
				SetNext(host, _from, Quests.Now + wait);
			}
		}

		private static void Drop(string why)
		{
			if (_giver != null)
			{
				Log.Info("patrons: no gift this time (" + why + ")");
				bool flag = false;
				try
				{
					flag = _giver.isAlive() && _giver.data.id == _giverId;
				}
				catch
				{
				}
				if (flag)
				{
					Release(_giver);
				}
				_giver = null;
				_from = null;
			}
		}

		internal static double NextAt(Actor body, City town)
		{
			long num = Rep.IdOf(town);
			foreach (KeyValuePair<long, double> item in Read(body))
			{
				if (item.Key == num)
				{
					return item.Value;
				}
			}
			return -1.0;
		}

		internal static void SetNext(Actor body, City town, double at)
		{
			long id = Rep.IdOf(town);
			List<KeyValuePair<long, double>> list = Read(body);
			list.RemoveAll((KeyValuePair<long, double> keyValuePair) => keyValuePair.Key == id);
			list.Add(new KeyValuePair<long, double>(id, at));
			StringBuilder stringBuilder = new StringBuilder();
			foreach (KeyValuePair<long, double> item in list)
			{
				if (stringBuilder.Length > 0)
				{
					stringBuilder.Append('|');
				}
				stringBuilder.Append(item.Key).Append(':').Append(item.Value.ToString("0.#", CultureInfo.InvariantCulture));
			}
			try
			{
				body.data.set("fp_gifts", stringBuilder.ToString());
			}
			catch (Exception e)
			{
				Hub.Fail("patrons save", e);
			}
		}

		private static List<KeyValuePair<long, double>> Read(Actor body)
		{
			List<KeyValuePair<long, double>> list = new List<KeyValuePair<long, double>>();
			string pResult = null;
			try
			{
				body.data.get("fp_gifts", out pResult, null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(pResult))
			{
				return list;
			}
			string[] array = pResult.Split('|');
			foreach (string text in array)
			{
				int num = text.IndexOf(':');
				if (num > 0 && long.TryParse(text.Substring(0, num), out var result) && double.TryParse(text.Substring(num + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
				{
					list.Add(new KeyValuePair<long, double>(result, result2));
				}
			}
			return list;
		}

		private static void Send(Actor giver, Actor host)
		{
			WorldTile worldTile = null;
			try
			{
				worldTile = World.world.GetTile(Mathf.FloorToInt(host.current_position.x), Mathf.FloorToInt(host.current_position.y));
			}
			catch
			{
			}
			if (worldTile == null)
			{
				return;
			}
			try
			{
				if (giver.ai == null || ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)giver.ai).task == null || !(((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)giver.ai).task.id == "fp_patron_walk") || giver.beh_tile_target == null || Mathf.Abs(giver.beh_tile_target.x - worldTile.x) + Mathf.Abs(giver.beh_tile_target.y - worldTile.y) > 1)
				{
					giver.setTask("fp_patron_walk");
					giver.beh_tile_target = worldTile;
				}
			}
			catch (Exception e)
			{
				Hub.Fail("patrons walk", e);
			}
		}

		private static void Release(Actor a)
		{
			try
			{
				if (a != null && a.isAlive() && a.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id == "fp_patron_walk")
				{
					a.cancelAllBeh();
				}
			}
			catch
			{
			}
		}

		private static void Register()
		{
			if (_registered)
			{
				return;
			}
			_registered = true;
			try
			{
				if (AssetManager.tasks_actor.get("fp_patron_walk") == null)
				{
					BehaviourTaskActor behaviourTaskActor = new BehaviourTaskActor
					{
						id = "fp_patron_walk",
						locale_key = "task_unit_move",
						speed_multiplier = 1f,
						cancellable_by_reproduction = false,
						cancellable_by_socialize = false
					};
					behaviourTaskActor.setIcon("ui/Icons/iconFamily");
					BehGoToTileTarget behGoToTileTarget = new BehGoToTileTarget();
					((BehaviourActionBase<Actor>)behGoToTileTarget).setupErrorChecks();
					behaviourTaskActor.addBeh(behGoToTileTarget);
					BehRandomWait behRandomWait = new BehRandomWait(0.6f, 1.2f);
					((BehaviourActionBase<Actor>)behRandomWait).setupErrorChecks();
					behaviourTaskActor.addBeh(behRandomWait);
					AssetManager.tasks_actor.add(behaviourTaskActor);
				}
			}
			catch (Exception e)
			{
				Hub.Fail("patrons task", e);
			}
		}
	}
}
