using System;
using FirstPerson.Core;
using FirstPerson.Towns;

namespace FirstPerson
{
	internal static class Bonds
	{
		public const int FriendTalks = 6;

		public const int GiftsForAcquaintance = 1;

		public const int GiftsForFriend = 3;

		public const int FriendSpan = 1;

		public const int CloseSpan = 2;

		public static Voices.Bond Of(Actor npc, Actor you, Voices.Tie tie)
		{
			if (npc == null || you == null)
			{
				return Voices.Bond.Stranger;
			}
			Regard regard = Chronicle.RegardOf(npc, you, create: false);
			Mind.Rec rec = Mind.Of(npc);
			bool flag = rec != null && rec.Body == you.data.id;
			int num = regard?.Talks ?? 0;
			int num2 = regard?.Gifts ?? 0;
			int num3 = regard?.Quests ?? 0;
			int num4 = regard?.Crimes ?? 0;
			int num5 = (flag ? rec.Days : 0);
			int num6 = ((flag && rec.FirstDay >= 0) ? Math.Max(0, Mind.Today - rec.FirstDay) : 0);
			RepLevel repLevel = StandingWith(npc, you);
			int num7;
			if (tie == Voices.Tie.Lover)
			{
				num7 = 3;
			}
			else
			{
				bool num8 = repLevel >= RepLevel.Honored;
				int num9 = (num8 ? 3 : 6);
				int num10 = ((!num8) ? 1 : 0);
				int num11 = num + num3 * 2;
				num7 = 0;
				if (num5 >= 2 || num2 >= 1 || num3 > 0 || repLevel >= RepLevel.Trusted)
				{
					num7 = 1;
				}
				if (num7 >= 1 && ((num11 >= num9 && num6 >= num10) || num2 >= 3))
				{
					num7 = 2;
				}
				if (num7 >= 2 && num6 >= 2 && num3 > 0)
				{
					num7 = 3;
				}
				if (tie == Voices.Tie.Kin || Ruler(npc, you))
				{
					num7 = Math.Max(num7, 2);
				}
				if (num4 > 0)
				{
					num7--;
				}
				if (repLevel == RepLevel.Distrusted)
				{
					num7--;
				}
			}
			if (repLevel == RepLevel.Outlaw && tie != Voices.Tie.Lover && tie != Voices.Tie.Kin)
			{
				num7 = 0;
			}
			return (Voices.Bond)Math.Max(0, Math.Min(3, num7));
		}

		private static bool Ruler(Actor npc, Actor you)
		{
			try
			{
				if (npc.isKing() && npc.kingdom != null && npc.kingdom == you.kingdom)
				{
					return true;
				}
				if (npc.isCityLeader() && npc.city != null && npc.city == you.city)
				{
					return true;
				}
			}
			catch
			{
			}
			return false;
		}

		private static RepLevel StandingWith(Actor npc, Actor you)
		{
			try
			{
				if (npc.city != null && npc.city.isAlive())
				{
					return Rep.Level(you, npc.city);
				}
				if (Rep.Civ(npc.kingdom))
				{
					return Rep.Standing(you, npc.kingdom);
				}
			}
			catch
			{
			}
			return RepLevel.Stranger;
		}

		public static string Word(Voices.Bond b)
		{
			switch (b)
			{
			case Voices.Bond.Acquaintance:
				return "Acquaintance";
			case Voices.Bond.Friend:
				return "Friend";
			case Voices.Bond.Close:
				return "Close friend";
			default:
				return "Stranger";
			}
		}
	}
}
