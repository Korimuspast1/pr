using System;
using System.Collections.Generic;
using System.Reflection;

namespace FirstPerson
{
	internal static class SettlerOffer
	{
		private sealed class Offer
		{
			public City To;

			public City From;

			public Actor You;

			public float Waited;

			public float Away;

			public bool Open;
		}

		private const int MoveIl = 111;

		private const float AnswerWithin = 120f;

		private static bool _installed;

		private static Offer _offer;

		private static object _world;

		public static bool Installed => _installed;

		internal static void AnswerForTest(bool yes)
		{
			if (_offer != null)
			{
				Answer(yes, null);
			}
		}

		internal static bool PendingForTest(out City to)
		{
			to = _offer?.To;
			return _offer != null;
		}

		public static void Install()
		{
			if (!_installed)
			{
				MethodInfo method = typeof(BehKingCheckNewCityFoundation).GetMethod("isPossibleToMoveUnitToCity", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[2]
				{
					typeof(Actor),
					typeof(City)
				}, null);
				int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num != 111)
				{
					throw new InvalidOperationException($"the game's choice of settlers has changed ({num} bytes of IL, not {111})");
				}
				Detour.Apply(method, typeof(SettlerOffer).GetMethod("CanMove", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				_installed = true;
			}
		}

		private static Actor You()
		{
			try
			{
				WorldBoxMod instance = WorldBoxMod.Instance;
				Actor actor = ((instance != null && instance.Body != null && instance.Body.isAlive()) ? instance.Body : ControllableUnit._unit_main);
				return (actor != null && actor.isAlive()) ? actor : null;
			}
			catch
			{
				return null;
			}
		}

		private static bool CanMove(BehKingCheckNewCityFoundation self, Actor pUnit, City pCity)
		{
			if (!GameAllows(pUnit, pCity))
			{
				return false;
			}
			Actor actor = You();
			if (actor == null)
			{
				return true;
			}
			if (pUnit == actor)
			{
				Ask(pCity, actor);
				return false;
			}
			try
			{
				if (actor.lover == pUnit)
				{
					return false;
				}
			}
			catch
			{
			}
			return true;
		}

		private static bool GameAllows(Actor a, City c)
		{
			if (a.isRekt() || !a.isAdult() || a.isCityLeader() || a.isKing() || a.isArmyGroupLeader() || a.army != null)
			{
				return false;
			}
			if (a.hasLover() && (a.lover.isKing() || a.lover.isCityLeader()))
			{
				return false;
			}
			return a.city != c;
		}

		private static void Ask(City to, Actor you)
		{
			CheckWorld();
			if (_offer == null)
			{
				_offer = new Offer
				{
					To = to,
					From = you.city,
					You = you
				};
				Log.Info("settlers: the crown's choice for " + to?.name + " fell on you; asking");
			}
		}

		private static void CheckWorld()
		{
			object obj = ((World.world != null) ? World.world.map_stats : null);
			if (obj != _world)
			{
				_world = obj;
				_offer = null;
			}
		}

		public static void Update(Actor host, float dt)
		{
			CheckWorld();
			Offer offer = _offer;
			if (offer == null)
			{
				return;
			}
			bool flag;
			try
			{
				flag = offer.You == null || !offer.You.isAlive() || offer.To == null || !offer.To.isAlive() || offer.You.city == offer.To || offer.You.kingdom != offer.To.kingdom;
			}
			catch
			{
				flag = true;
			}
			if (flag)
			{
				_offer = null;
				return;
			}
			WorldBoxMod instance = WorldBoxMod.Instance;
			if (instance == null || !instance.IsFirstPerson || host != offer.You)
			{
				offer.Away += dt;
				if (offer.Away > 60f)
				{
					Log.Info("settlers: not there to be asked");
					Answer(yes: false, null);
				}
				return;
			}
			if (!offer.Open)
			{
				offer.Waited += dt;
			}
			bool crown;
			if (offer.Waited > 120f)
			{
				Answer(yes: false, null);
				instance.Toast("No answer: you stay in " + (offer.From?.name ?? "your town"));
			}
			else if (offer.Open)
			{
				if (!instance.Converse.Open)
				{
					offer.Open = false;
				}
			}
			else if (!instance.TalkBusy && !instance.AnyMenuOpen && !CrownOffer.PendingForTest(out crown) && !(offer.Waited < 1.5f) && (int)offer.Waited % 20 == 1)
			{
				Actor actor = CrownOffer.HeraldOf(offer.You.kingdom, offer.You);
				if (actor != null && instance.StartAudience(actor))
				{
					offer.Open = true;
					Present(instance.Converse, offer);
				}
			}
		}

		private static void Present(Conversation c, Offer o)
		{
			c.LastTopic = "settler_offer";
			string name = o.To.name;
			string text = ((o.From != null) ? o.From.name : "your town");
			Actor actor = null;
			try
			{
				actor = o.You.lover;
			}
			catch
			{
			}
			c.Say("The crown has founded " + name + " and needs settlers. You are named among them." + ((actor != null && actor.isAlive()) ? (" " + actor.getName() + " would go with you.") : "") + " Will you go?");
			c.Offer(new List<Choice>
			{
				new Choice("I'll go to " + name + ".", delegate
				{
					Answer(yes: true, c);
				}, ChoiceTone.Quest),
				new Choice("I'll stay in " + text + ".", delegate
				{
					Answer(yes: false, c);
				})
			});
		}

		private static void Answer(bool yes, Conversation c)
		{
			Offer offer = _offer;
			if (offer == null)
			{
				return;
			}
			_offer = null;
			WorldBoxMod instance = WorldBoxMod.Instance;
			try
			{
				if (yes && offer.To.isAlive())
				{
					Actor lover = offer.You.lover;
					Move(offer.You, offer.To);
					if (lover != null && lover.isAlive() && GameAllows(lover, offer.To))
					{
						Move(lover, offer.To);
					}
					Log.Info("settlers: you went to " + offer.To.name);
					instance?.Toast("You settle in " + offer.To.name);
				}
				else
				{
					Log.Info("settlers: you stayed in " + (offer.From?.name ?? "-"));
				}
			}
			catch (Exception ex)
			{
				Log.Error("settlers: " + ex.Message);
			}
			if (c != null)
			{
				c.EndAfter = true;
				c.Say(yes ? Dialogue.Pick("Then go well. The road is yours.", "Good. They'll be glad of you.") : Dialogue.Pick("As you wish.", "Then another goes in your place."));
			}
		}

		private static void Move(Actor a, City to)
		{
			a.stopBeingWarrior();
			a.joinCity(to);
		}
	}
}
