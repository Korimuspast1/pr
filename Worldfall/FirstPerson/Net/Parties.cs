using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson.Net
{
	internal static class Parties
	{
		private const byte Invite = 1;

		private const byte Accept = 2;

		private const byte Leave = 3;

		private const byte Invited = 4;

		public static readonly Dictionary<int, float> Pending = new Dictionary<int, float>();

		private static readonly NetWriter W = new NetWriter(64);

		private static int _nextParty = 1;

		private static readonly Dictionary<KeyValuePair<int, int>, float> _sent = new Dictionary<KeyValuePair<int, int>, float>();

		private const float InviteLasts = 300f;

		public static void Reset()
		{
			Pending.Clear();
			_sent.Clear();
		}

		public static bool SameParty(NetPlayer a, NetPlayer b)
		{
			if (a != null && b != null && a.Party != 0)
			{
				return a.Party == b.Party;
			}
			return false;
		}

		public static void InviteTo(NetPlayer target)
		{
			Send(1, target.Id);
		}

		public static void AcceptFrom(int inviter)
		{
			Pending.Remove(inviter);
			Send(2, inviter);
		}

		public static void Decline(int inviter)
		{
			Pending.Remove(inviter);
		}

		public static void LeaveParty()
		{
			Send(3, 0);
		}

		private static void Send(byte what, int who)
		{
			NetSession current = NetSession.Current;
			if (current != null)
			{
				if (current.IsHost)
				{
					Handle(current, current.Me, what, who);
					return;
				}
				W.Reset();
				W.VarU(56uL);
				W.U8(what);
				W.VarU((ulong)who);
				current.ToHost(W, Delivery.Reliable);
			}
		}

		public static void HostMessage(NetSession s, NetPlayer from, NetReader r)
		{
			byte what = (byte)r.U8();
			int who = (int)r.VarU();
			Handle(s, from, what, who);
		}

		private static void Handle(NetSession s, NetPlayer from, byte what, int who)
		{
			switch (what)
			{
			case 1:
			{
				if (s.Players.TryGetValue(who, out var value3) && value3 != from)
				{
					_sent[new KeyValuePair<int, int>(from.Id, value3.Id)] = Time.realtimeSinceStartup;
					if (value3.Host)
					{
						Pending[from.Id] = Time.realtimeSinceStartup;
						s.Note(new NetSession.ChatLine
						{
							Text = from.Name + " invites you to a party (Tab, Players)",
							System = true,
							At = Time.realtimeSinceStartup
						});
					}
					else
					{
						W.Reset();
						W.VarU(56uL);
						W.U8(4);
						W.VarU((ulong)from.Id);
						value3.Link?.Send(W, Delivery.Reliable);
					}
				}
				break;
			}
			case 2:
			{
				if (!s.Players.TryGetValue(who, out var value))
				{
					break;
				}
				KeyValuePair<int, int> key = new KeyValuePair<int, int>(value.Id, from.Id);
				if (_sent.TryGetValue(key, out var value2) && !(Time.realtimeSinceStartup - value2 > 300f))
				{
					_sent.Remove(key);
					if (value.Party == 0)
					{
						value.Party = _nextParty++;
					}
					from.Party = value.Party;
					s.System(from.Name + " joined " + value.Name + "'s party");
					s.SendPlayers();
				}
				break;
			}
			case 3:
			{
				int party = from.Party;
				from.Party = 0;
				int num = 0;
				NetPlayer netPlayer = null;
				foreach (NetPlayer value4 in s.Players.Values)
				{
					if (value4.Party == party && party != 0)
					{
						num++;
						netPlayer = value4;
					}
				}
				if (num == 1)
				{
					netPlayer.Party = 0;
				}
				s.SendPlayers();
				break;
			}
			}
		}

		public static void JoinerMessage(NetSession s, NetReader r)
		{
			byte num = (byte)r.U8();
			int key = (int)r.VarU();
			if (num == 4)
			{
				Pending[key] = Time.realtimeSinceStartup;
				NetPlayer value;
				string text = (s.Players.TryGetValue(key, out value) ? value.Name : "Someone");
				WorldBoxMod.Instance?.Toast(text + " invites you to a party: Tab, then Players");
			}
		}
	}
}
