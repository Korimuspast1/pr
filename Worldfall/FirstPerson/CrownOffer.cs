using System;
using System.Collections.Generic;
using FirstPerson.Towns;

namespace FirstPerson
{
	internal static class CrownOffer
	{
		private sealed class Offer
		{
			public Kingdom K;

			public City C;

			public Actor You;

			public Actor Alt;

			public float Waited;

			public float Away;

			public bool Open;

			public Actor Herald;
		}

		private const float AnswerWithin = 120f;

		private static Offer _offer;

		private static readonly Dictionary<string, KeyValuePair<long, bool>> Answers = new Dictionary<string, KeyValuePair<long, bool>>();

		private static object _world;

		internal static bool AutoAnswer;

		internal static Actor PendingAltForTest => _offer?.Alt;

		internal static void AnswerForTest(bool yes)
		{
			if (_offer != null)
			{
				Answer(yes, null);
			}
		}

		internal static bool PendingForTest(out bool crown)
		{
			crown = _offer != null && _offer.C == null;
			return _offer != null;
		}

		private static string Key(Kingdom k, City c)
		{
			try
			{
				return (c != null) ? ("c" + c.getID()) : ("k" + k.getID());
			}
			catch
			{
				return "?";
			}
		}

		public static bool Waiting(Kingdom k, City c)
		{
			if (_offer != null && _offer.K == k)
			{
				return _offer.C == c;
			}
			return false;
		}

		public static bool Declined(Kingdom k, City c, Actor you)
		{
			CheckWorld();
			if (you != null && Answers.TryGetValue(Key(k, c), out var value) && value.Key == you.data.id)
			{
				return !value.Value;
			}
			return false;
		}

		public static void Settled(Kingdom k, City c)
		{
			if (Answers.Count != 0)
			{
				Answers.Remove(Key(k, c));
			}
		}

		public static void Ask(Kingdom k, City c, Actor you, Actor alt)
		{
			CheckWorld();
			if (_offer == null)
			{
				_offer = new Offer
				{
					K = k,
					C = c,
					You = you,
					Alt = alt
				};
				Log.Info("crown: the game's choice for " + ((c == null) ? ("the crown of " + Royal.NameOf(k)) : ("leader of " + c.name)) + " fell on you; asking (else " + ((alt != null) ? WorldBoxMod.Describe(alt) : "no one") + ")");
				_ = AutoAnswer;
			}
		}

		private static void CheckWorld()
		{
			object obj = ((World.world != null) ? World.world.map_stats : null);
			if (obj != _world)
			{
				_world = obj;
				_offer = null;
				Answers.Clear();
			}
		}

		public static void Update(Actor host, float dt)
		{
			CheckWorld();
			Offer offer = _offer;
			if (offer == null || AutoAnswer)
			{
				return;
			}
			bool flag = false;
			try
			{
				flag = offer.You == null || !offer.You.isAlive() || offer.You.kingdom != offer.K || !offer.K.isAlive() || (offer.C != null && (!offer.C.isAlive() || offer.C.hasLeader())) || (offer.C == null && offer.K.hasKing());
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
					Log.Info("crown: not there to be asked");
					Answer(yes: false, null);
				}
				return;
			}
			if (!offer.Open)
			{
				offer.Waited += dt;
			}
			if (offer.Waited > 120f)
			{
				Answer(yes: false, null);
				instance.Toast((offer.C != null) ? ("No answer: another will lead " + offer.C.name) : ((offer.Alt != null) ? "No answer: the crown passes on" : "No answer: the throne stands empty"));
			}
			else if (offer.Open)
			{
				if (!instance.Converse.Open)
				{
					offer.Open = false;
				}
			}
			else if (!instance.TalkBusy && !instance.AnyMenuOpen && !(offer.Waited < 1.5f) && (int)offer.Waited % 20 == 1)
			{
				Actor actor = HeraldOf(offer.K, offer.You);
				if (actor != null && instance.StartAudience(actor))
				{
					offer.Herald = actor;
					offer.Open = true;
					Present(instance.Converse, offer);
				}
			}
		}

		internal static Actor HeraldOf(Kingdom k, Actor you)
		{
			Actor actor = null;
			try
			{
				actor = CourtThrone.EnvoyOf(k);
			}
			catch
			{
			}
			if (actor != null && actor != you && actor.isAlive())
			{
				return actor;
			}
			Actor result = null;
			float num = float.MaxValue;
			foreach (Actor item in Civil.SubjectsOf(k))
			{
				if (item != null && item != you && item.isAlive() && item.isAdult() && Dialogue.IsPerson(item))
				{
					float sqrMagnitude = (item.current_position - you.current_position).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						result = item;
					}
				}
			}
			return result;
		}

		private static void Present(Conversation c, Offer o)
		{
			c.LastTopic = "crown_offer";
			string text = ((o.Alt != null) ? o.Alt.getName() : null);
			List<Choice> list = new List<Choice>();
			if (o.C == null)
			{
				string text2 = Royal.NameOf(o.K);
				c.Say("The throne of " + text2 + " stands empty, and by right it falls to you. Will you take the crown?" + ((text != null) ? "" : ((o.K.countCities() == 1) ? " There is no one else to take it. Refuse, and the throne stands empty." : " There is no one else of the blood. Refuse, and the realm may break apart.")));
				list.Add(new Choice("I accept the crown.", delegate
				{
					Answer(yes: true, c);
				}, ChoiceTone.Quest));
				list.Add(new Choice((text != null) ? ("Let it pass to " + text + ".") : "I won't rule.", delegate
				{
					Answer(yes: false, c);
				}, ChoiceTone.Risk));
			}
			else
			{
				c.Say(o.C.name + " needs a leader, and the people name you. Will you lead them?");
				list.Add(new Choice("I'll lead " + o.C.name + ".", delegate
				{
					Answer(yes: true, c);
				}, ChoiceTone.Quest));
				list.Add(new Choice((text != null) ? ("Let " + text + " lead.") : "Not me.", delegate
				{
					Answer(yes: false, c);
				}, ChoiceTone.Risk));
			}
			c.Offer(list);
		}

		private static void Answer(bool yes, Conversation c)
		{
			Offer offer = _offer;
			if (offer == null)
			{
				return;
			}
			_offer = null;
			Answers[Key(offer.K, offer.C)] = new KeyValuePair<long, bool>(offer.You.data.id, yes);
			WorldBoxMod instance = WorldBoxMod.Instance;
			try
			{
				if (yes && offer.C == null)
				{
					bool flag = Service.IsSoldier(offer.You);
					CrownGuard.Crown(offer.K, offer.You);
					Log.Info("crown: you accepted the crown of " + Royal.NameOf(offer.K));
					instance?.Toast("You are crowned " + (Models.IsFemale(offer.You) ? "queen" : "king") + " of " + Royal.NameOf(offer.K) + (flag ? ". Your service in the army ends" : ""));
				}
				else if (yes)
				{
					if (offer.You.city != offer.C)
					{
						offer.You.joinCity(offer.C);
					}
					offer.C.setLeader(offer.You, pNew: true);
					Log.Info("crown: you lead " + offer.C.name);
					instance?.Toast("You lead " + offer.C.name + " now");
				}
				else
				{
					if (offer.C == null)
					{
						offer.K.data.timer_new_king = 0f;
					}
					Log.Info("crown: you declined " + ((offer.C == null) ? ("the crown of " + Royal.NameOf(offer.K)) : ("to lead " + offer.C.name)));
				}
			}
			catch (Exception ex)
			{
				Log.Error("crown: " + ex.Message);
			}
			if (c != null)
			{
				c.EndAfter = true;
				c.Say((!yes) ? ((offer.Alt == null && offer.C == null) ? "As you wish. The throne stands empty." : Dialogue.Pick("As you wish.", "Then it passes on.")) : ((offer.C == null) ? Dialogue.Pick("Long may you reign.", "It is done. The realm is yours.") : "The people will be glad of it."));
			}
		}
	}
}
