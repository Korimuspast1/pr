using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson.Net
{
	internal static class PlayerTalk
	{
		private enum Mode : byte
		{
			None,
			Asked,
			Talk,
			Give
		}

		public struct Bubble
		{
			public string Text;

			public float At;
		}

		private const byte Ask = 1;

		private const byte Accept = 2;

		private const byte Decline = 3;

		private const byte Say = 4;

		private const byte Bye = 5;

		private const byte Give = 6;

		private const byte GiveBack = 7;

		private const byte Typing = 8;

		public const float Reach = 8f;

		private const float AskLife = 20f;

		private const float TypingLife = 4f;

		private const float TypingEvery = 1.5f;

		public const float BubbleLife = 8f;

		public static readonly Conversation Card = new Conversation();

		private static Mode _mode;

		public static int With;

		private static readonly List<int> _stale = new List<int>();

		private static int _asked;

		private static float _askedAt;

		private static readonly Dictionary<int, float> Asks = new Dictionary<int, float>();

		public static readonly Dictionary<int, Bubble> Bubbles = new Dictionary<int, Bubble>();

		public static readonly Dictionary<int, float> TypingAt = new Dictionary<int, float>();

		private static bool _typingSent;

		private static float _typingSentAt;

		private static bool _listening;

		private static readonly NetWriter W = new NetWriter(512);

		internal static float ReachForTest;

		public static bool CardOpen => Card.Open;

		public static bool Talking
		{
			get
			{
				if (_mode != Mode.Talk)
				{
					return _mode == Mode.Give;
				}
				return true;
			}
		}

		public static string Hint
		{
			get
			{
				if (_mode != Mode.Asked)
				{
					return "1-9 to choose   ·   Enter to type   ·   Esc to leave";
				}
				return "1 or 2 to answer   ·   Esc: not now";
			}
		}

		public static bool TryAsk(Actor a)
		{
			NetSession current = NetSession.Current;
			if (current == null || a == null)
			{
				return false;
			}
			long id;
			try
			{
				id = a.data.id;
			}
			catch
			{
				return false;
			}
			NetPlayer netPlayer = current.PlayerByBody(id);
			if (netPlayer == null || netPlayer == current.Me)
			{
				return false;
			}
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (Talking && With == netPlayer.Id)
			{
				return true;
			}
			if (Asks.ContainsKey(netPlayer.Id))
			{
				Send(2, netPlayer.Id);
				Begin(netPlayer.Id);
				return true;
			}
			if (_asked == netPlayer.Id && realtimeSinceStartup - _askedAt < 20f)
			{
				Toast("Waiting for " + netPlayer.Name + " to answer");
				return true;
			}
			_asked = netPlayer.Id;
			_askedAt = realtimeSinceStartup;
			Send(1, netPlayer.Id);
			Toast("Asked " + netPlayer.Name + " to talk");
			return true;
		}

		public static bool IsPlayer(Actor a)
		{
			NetSession current = NetSession.Current;
			if (current == null || a == null)
			{
				return false;
			}
			try
			{
				NetPlayer netPlayer = current.PlayerByBody(a.data.id);
				return netPlayer != null && netPlayer != current.Me;
			}
			catch
			{
				return false;
			}
		}

		public static void Tick()
		{
			NetSession current = NetSession.Current;
			if (current == null)
			{
				Reset();
				return;
			}
			if (!_listening)
			{
				NetSession.ChatSeen += Heard;
				_listening = true;
			}
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			WorldBoxMod instance = WorldBoxMod.Instance;
			TellTyping(current, realtimeSinceStartup);
			if (_asked != 0 && (realtimeSinceStartup - _askedAt > 20f || !current.Players.ContainsKey(_asked)))
			{
				if (current.Players.TryGetValue(_asked, out var value))
				{
					Toast(value.Name + " did not answer");
				}
				_asked = 0;
			}
			List<int> stale = _stale;
			stale.Clear();
			foreach (KeyValuePair<int, float> ask in Asks)
			{
				if (realtimeSinceStartup - ask.Value > 20f || !current.Players.ContainsKey(ask.Key))
				{
					stale.Add(ask.Key);
				}
			}
			foreach (int item in stale)
			{
				Asks.Remove(item);
				if (_mode == Mode.Asked && With == item)
				{
					Close();
				}
			}
			Actor actor = MyBody();
			bool flag = instance != null && instance.Converse.Open;
			if (Talking)
			{
				NetPlayer value2;
				Actor actor2 = (current.Players.TryGetValue(With, out value2) ? BodyOf(value2) : null);
				if (value2 == null)
				{
					Close();
					Toast("The talk ended: they left the game");
				}
				else if (actor == null || actor2 == null || flag)
				{
					End(value2.Name);
				}
				else if ((actor2.current_position - actor.current_position).magnitude > ((ReachForTest > 0f) ? ReachForTest : 8f))
				{
					End(null);
					Toast(value2.Name + " is too far away. The talk ended");
				}
				else
				{
					Card.With = actor2;
					Card.You = actor;
				}
			}
			else if (_mode == Mode.Asked && (flag || actor == null))
			{
				Card.Open = false;
			}
			if ((_mode == Mode.None || (_mode == Mode.Asked && !Card.Open)) && !flag && actor != null)
			{
				ShowNextAsk(current);
			}
			if (!Card.Open || MpUi.Typing || (instance != null && instance.AnyMenuOpen))
			{
				return;
			}
			for (int i = 0; i < 9; i++)
			{
				if (PlayInput.GetKeyDown((KeyCode)(49 + i)) || PlayInput.GetKeyDown((KeyCode)(257 + i)))
				{
					Card.Selected = i;
					Card.Pick(i);
					break;
				}
			}
		}

		public static bool Escape()
		{
			if (!Card.Open)
			{
				return false;
			}
			if (_mode == Mode.Give)
			{
				MainChoices();
			}
			else if (_mode == Mode.Asked)
			{
				Answer(yes: false);
			}
			else
			{
				End(null);
			}
			return true;
		}

		public static void Reset()
		{
			if (_mode != Mode.None || Card.Open)
			{
				Close();
			}
			_asked = 0;
			Asks.Clear();
			Bubbles.Clear();
			TypingAt.Clear();
			_typingSent = false;
		}

		private static void ShowNextAsk(NetSession s)
		{
			int num = 0;
			float num2 = float.MaxValue;
			foreach (KeyValuePair<int, float> ask in Asks)
			{
				if (ask.Value < num2)
				{
					num2 = ask.Value;
					num = ask.Key;
				}
			}
			if (num == 0 || !s.Players.TryGetValue(num, out var value))
			{
				return;
			}
			Actor actor = BodyOf(value);
			if (actor == null)
			{
				Asks.Remove(num);
				Send(3, num);
				return;
			}
			_mode = Mode.Asked;
			With = num;
			Setup(value, actor);
			Line(value.Name + " wants to talk.", mine: false);
			Card.Choices.Clear();
			Card.Choices.Add(new Choice("Talk", delegate
			{
				Answer(yes: true);
			}));
			Card.Choices.Add(new Choice("Not now", delegate
			{
				Answer(yes: false);
			}, ChoiceTone.Leave));
			Shown();
			Sounds.PlayUI("event:/SFX/Ui/Click");
		}

		private static void Answer(bool yes)
		{
			int with = With;
			Asks.Remove(with);
			Send((byte)(yes ? 2 : 3), with);
			if (yes)
			{
				Begin(with);
			}
			else
			{
				Close();
			}
		}

		private static void Begin(int who)
		{
			NetSession current = NetSession.Current;
			if (current == null || !current.Players.TryGetValue(who, out var value))
			{
				return;
			}
			Actor actor = BodyOf(value);
			if (actor == null || MyBody() == null)
			{
				Send(5, who);
				return;
			}
			Asks.Remove(who);
			if (_asked == who)
			{
				_asked = 0;
			}
			_mode = Mode.Talk;
			With = who;
			Setup(value, actor);
			Line("Talking with " + value.Name + ". Enter to say something.", mine: false);
			MainChoices();
			Sounds.PlayUI("event:/SFX/Ui/Click");
			Log.Info("NET talk: with " + value);
		}

		private static void Setup(NetPlayer p, Actor them)
		{
			Card.Close();
			Card.Open = true;
			Card.With = them;
			Card.You = MyBody();
			Card.Name = p.Name;
			Card.Stranger = null;
			string text = "";
			try
			{
				text = them.getName();
			}
			catch
			{
			}
			Card.Relation = ((text != "" && text != p.Name) ? ("Player  ·  " + text) : "Player");
			Card.Panel = TalkPanel.None;
			Card.EndAfter = false;
			Card.Toast = Toast;
			Card.OpenedAt = Time.unscaledTime;
		}

		private static void Line(string text, bool mine)
		{
			Card.Pages.Clear();
			Card.Pages.Add(text);
			Card.Page = 0;
			Card.YoursUpTo = (mine ? 1 : 0);
			Card.NameAt = 0;
			Card.LineStart = Time.unscaledTime;
		}

		private static void Shown()
		{
			Card.Sets++;
			Card.Selected = 0;
		}

		private static void MainChoices()
		{
			NetSession current = NetSession.Current;
			if (current != null && current.Players.TryGetValue(With, out var p))
			{
				_mode = Mode.Talk;
				Card.Choices.Clear();
				Card.Choices.Add(new Choice("Say something", MpUi.OpenChat));
				Choice choice = new Choice("Invite to party", delegate
				{
					Parties.InviteTo(p);
					Toast("Party invite sent to " + p.Name);
				});
				if (Parties.SameParty(p, current.Me))
				{
					choice.Off("In your party");
				}
				Card.Choices.Add(choice);
				Choice choice2 = new Choice("Give an item", GiveChoices);
				if (Bag.Contents(MyBody()).Count == 0)
				{
					choice2.Off("Your bag is empty");
				}
				Card.Choices.Add(choice2);
				Card.Choices.Add(new Choice("Goodbye", delegate
				{
					End(null);
				}, ChoiceTone.Leave));
				Shown();
			}
		}

		private static void GiveChoices()
		{
			Actor actor = MyBody();
			if (actor == null)
			{
				return;
			}
			_mode = Mode.Give;
			Card.Choices.Clear();
			List<KeyValuePair<string, int>> list = Bag.Contents(actor);
			for (int i = 0; i < list.Count && i < 8; i++)
			{
				string res = list[i].Key;
				int n = list[i].Value;
				Card.Choices.Add(new Choice(Work.Amount(res, n), delegate
				{
					GiveItem(res, n);
				}));
			}
			Card.Choices.Add(new Choice("Back", MainChoices, ChoiceTone.Back));
			Shown();
		}

		private static void GiveItem(string res, int amount)
		{
			Actor actor = MyBody();
			NetSession current = NetSession.Current;
			if (actor != null && current != null && current.Players.TryGetValue(With, out var value))
			{
				amount = Mathf.Min(amount, Civil.BagCount(actor, res));
				if (amount <= 0 || !Civil.TakeFromBag(actor, res, amount))
				{
					Toast("That is no longer in your bag");
					MainChoices();
					return;
				}
				Send(6, With, "", res, amount);
				Line("You give " + value.Name + " " + Work.Amount(res, amount) + ".", mine: false);
				Log.Info("NET talk: gave " + amount + " " + res + " to " + value);
				MainChoices();
			}
		}

		private static void End(string toastName)
		{
			if (With != 0 && (_mode == Mode.Talk || _mode == Mode.Give))
			{
				Send(5, With);
			}
			Close();
			if (toastName != null)
			{
				Toast("The talk with " + toastName + " ended");
			}
		}

		private static void Close()
		{
			bool talking = Talking;
			Card.Close();
			_mode = Mode.None;
			With = 0;
			if (talking && MpUi.ChatOpen)
			{
				MpUi.ChatOpen = false;
				MpUi.ChatText = "";
			}
		}

		public static void SayLine(string text)
		{
			NetSession current = NetSession.Current;
			text = MpPrefs.Clean(text, 200);
			if (current != null && !(text == "") && Talking && current.Players.TryGetValue(With, out var value))
			{
				Send(4, With, text);
				if (_mode == Mode.Give)
				{
					MainChoices();
				}
				Line(text, mine: true);
				current.Note(new NetSession.ChatLine
				{
					From = current.Me.Id,
					Name = "To " + value.Name,
					Text = text,
					Private = true,
					At = Time.realtimeSinceStartup
				});
			}
		}

		private static void Heard(NetSession.ChatLine line)
		{
			if (!line.System && line.From != 0)
			{
				Bubbles[line.From] = new Bubble
				{
					Text = line.Text,
					At = Time.realtimeSinceStartup
				};
				TypingAt.Remove(line.From);
			}
		}

		public static bool TypingNow(int player)
		{
			if (TypingAt.TryGetValue(player, out var value))
			{
				return Time.realtimeSinceStartup - value < 4f;
			}
			return false;
		}

		private static void TellTyping(NetSession s, float now)
		{
			bool flag = MpUi.ChatOpen && MpUi.ChatText != null && MpUi.ChatText.Trim().Length > 0 && !MpUi.ChatText.StartsWith("/p ", StringComparison.Ordinal);
			if (flag != _typingSent || (flag && !(now - _typingSentAt < 1.5f)))
			{
				_typingSent = flag;
				_typingSentAt = now;
				Send(8, 0, flag ? "1" : "0");
			}
		}

		private static void Send(byte kind, int to, string text = "", string res = "", int amount = 0)
		{
			NetSession current = NetSession.Current;
			if (current != null && current.Me != null)
			{
				if (current.IsHost)
				{
					Route(current, current.Me, kind, to, text, res, amount);
					return;
				}
				Write(kind, to, text, res, amount);
				current.ToHost(W, Delivery.Reliable);
			}
		}

		private static void Write(byte kind, int who, string text, string res, int amount)
		{
			W.Reset();
			W.VarU(57uL);
			W.U8(kind);
			W.VarU((ulong)who);
			W.Str(text ?? "");
			W.Str(res ?? "");
			W.VarI(amount);
		}

		public static void HostMessage(NetSession s, NetPlayer from, NetReader r)
		{
			byte kind = (byte)r.U8();
			int to = (int)r.VarU();
			string text = MpPrefs.Clean(r.Str(), 200);
			string res = r.Str() ?? "";
			int amount = (int)r.VarI();
			Route(s, from, kind, to, text, res, amount);
		}

		private static void Route(NetSession s, NetPlayer from, byte kind, int to, string text, string res, int amount)
		{
			if (kind == 8)
			{
				foreach (NetPlayer value2 in s.Players.Values)
				{
					if (value2 != from && (value2.Ready || value2.Host))
					{
						Deliver(s, value2, from.Id, kind, text, res, amount);
					}
				}
				return;
			}
			if (!s.Players.TryGetValue(to, out var value) || value == from || (!value.Ready && !value.Host))
			{
				switch (kind)
				{
				case 6:
					Deliver(s, from, to, 7, "", res, amount);
					break;
				case 1:
				case 2:
				case 4:
					Deliver(s, from, to, 5, "", "", 0);
					break;
				}
			}
			else
			{
				Deliver(s, value, from.Id, kind, text, res, amount);
			}
		}

		private static void Deliver(NetSession s, NetPlayer to, int from, byte kind, string text, string res, int amount)
		{
			if (to == s.Me)
			{
				Handle(s, from, kind, text, res, amount);
				return;
			}
			Write(kind, from, text, res, amount);
			to.Link?.Send(W, Delivery.Reliable);
		}

		public static void JoinerMessage(NetSession s, NetReader r)
		{
			byte kind = (byte)r.U8();
			int num = (int)r.VarU();
			string text = r.Str() ?? "";
			string res = r.Str() ?? "";
			int amount = (int)r.VarI();
			Handle(s, num, kind, text, res, amount);
		}

		private static void Handle(NetSession s, int from, byte kind, string text, string res, int amount)
		{
			s.Players.TryGetValue(from, out var value);
			string text2 = ((value != null) ? value.Name : "Someone");
			switch (kind)
			{
			case 1:
				if (Talking && With != from)
				{
					Send(3, from, "busy");
				}
				else
				{
					if (Talking)
					{
						break;
					}
					if (_asked == from)
					{
						Send(2, from);
						Begin(from);
						break;
					}
					Asks[from] = Time.realtimeSinceStartup;
					WorldBoxMod instance = WorldBoxMod.Instance;
					if (instance != null && instance.Converse.Open)
					{
						Toast(text2 + " wants to talk. End this talk to answer");
					}
				}
				break;
			case 2:
				if (_asked != from)
				{
					Send(5, from);
					break;
				}
				_asked = 0;
				Begin(from);
				break;
			case 3:
				if (_asked == from)
				{
					_asked = 0;
					Toast((text == "busy") ? (text2 + " is talking with someone else") : (text2 + " can't talk now"));
				}
				break;
			case 4:
				if (Talking && With == from)
				{
					if (_mode == Mode.Give)
					{
						MainChoices();
					}
					Line(text, mine: false);
					s.Note(new NetSession.ChatLine
					{
						From = from,
						Name = text2,
						Text = text,
						Private = true,
						At = Time.realtimeSinceStartup
					});
				}
				break;
			case 5:
				Asks.Remove(from);
				if (_asked == from)
				{
					_asked = 0;
				}
				if (With == from && _mode != Mode.None)
				{
					bool talking = Talking;
					Close();
					if (talking)
					{
						Toast(text2 + " ended the talk");
					}
				}
				break;
			case 6:
			case 7:
				Take(s, from, kind, text2, res, amount);
				break;
			case 8:
				if (text == "1")
				{
					TypingAt[from] = Time.realtimeSinceStartup;
				}
				else
				{
					TypingAt.Remove(from);
				}
				break;
			}
		}

		private static void Take(NetSession s, int from, byte kind, string name, string res, int amount)
		{
			amount = Mathf.Clamp(amount, 0, 100000);
			bool flag = Supplies.Is(res);
			if (!flag)
			{
				try
				{
					flag = AssetManager.resources.get(res) != null;
				}
				catch
				{
				}
			}
			if (!flag || amount <= 0)
			{
				Log.Info("NET talk: a gift of " + amount + " '" + res + "' from #" + from + " was not a thing");
				return;
			}
			Actor actor = MyBody();
			if (actor == null)
			{
				if (kind == 6)
				{
					Send(7, from, "", res, amount);
				}
				return;
			}
			Bag.Put(actor, res, amount, spill: true, Toast);
			if (kind == 7)
			{
				Toast(Work.Amount(res, amount) + " came back to your bag: " + name + " could not take it");
				return;
			}
			Toast(name + " gave you " + Work.Amount(res, amount));
			if (Talking && With == from)
			{
				Line(name + " gives you " + Work.Amount(res, amount) + ".", mine: false);
			}
			Log.Info("NET talk: got " + amount + " " + res + " from #" + from);
		}

		private static Actor MyBody()
		{
			long num = WorldSync.LocalBodyId();
			if (num == 0L)
			{
				return null;
			}
			try
			{
				return World.world.units.get(num);
			}
			catch
			{
				return null;
			}
		}

		private static Actor BodyOf(NetPlayer p)
		{
			if (p == null || p.Body == 0L)
			{
				return null;
			}
			try
			{
				Actor actor = World.world.units.get(p.Body);
				return (actor != null && actor.isAlive()) ? actor : null;
			}
			catch
			{
				return null;
			}
		}

		private static void Toast(string text)
		{
			WorldBoxMod.Instance?.Toast(text);
		}

		internal static void GiveForTest(string res, int amount)
		{
			GiveItem(res, amount);
		}
	}
}
