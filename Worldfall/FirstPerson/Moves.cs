using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal static class Moves
	{
		public enum Move : byte
		{
			None,
			Dodge,
			Heavy,
			Lunge,
			JumpAttack
		}

		public const string KnownKey = "fp_moves";

		public const string ReadyKey = "fp_move";

		public const int BigBounty = 100;

		public const float HeavyTimes = 1.6f;

		public const float JumpTimes = 1.5f;

		private static readonly Move[] All = new Move[3]
		{
			Move.Heavy,
			Move.Lunge,
			Move.JumpAttack
		};

		private static readonly System.Random Rng = new System.Random();

		public static float Pending = 1f;

		public static float BlowScale = 1f;

		internal static float LastScaleForTest = 1f;

		public static string Name(Move m)
		{
			switch (m)
			{
			case Move.Dodge:
				return "Dodge step";
			case Move.Heavy:
				return "Heavy blow";
			case Move.Lunge:
				return "Sprint lunge";
			case Move.JumpAttack:
				return "Jump attack";
			default:
				return "";
			}
		}

		public static string Input(Move m)
		{
			switch (m)
			{
			case Move.Dodge:
				return "A, D or S tapped twice";
			case Move.Heavy:
				return "Hold the attack button";
			case Move.Lunge:
				return "Attack while sprinting";
			case Move.JumpAttack:
				return "Attack in the air";
			default:
				return "";
			}
		}

		public static string Blurb(Move m)
		{
			switch (m)
			{
			case Move.Dodge:
				return "A quick hop out of a blow's reach";
			case Move.Heavy:
				return "A slower blow that lands much harder";
			case Move.Lunge:
				return "Your run carried into the blow";
			case Move.JumpAttack:
				return "An overhead blow that lands as you come down";
			default:
				return "";
			}
		}

		public static List<Move> Known(Actor a)
		{
			List<Move> list = new List<Move>();
			string pResult = null;
			try
			{
				a?.data.get("fp_moves", out pResult, null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(pResult))
			{
				return list;
			}
			string[] array = pResult.Split(',');
			for (int i = 0; i < array.Length; i++)
			{
				if (int.TryParse(array[i], out var result) && result > 1 && result <= 4 && !list.Contains((Move)result))
				{
					list.Add((Move)result);
				}
			}
			return list;
		}

		public static bool Knows(Actor a, Move m)
		{
			if (m == Move.None || m == Move.Dodge)
			{
				return false;
			}
			string pResult = null;
			try
			{
				a?.data.get("fp_moves", out pResult, null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(pResult))
			{
				return false;
			}
			int num = 0;
			bool flag = false;
			for (int i = 0; i <= pResult.Length; i++)
			{
				char c = ((i < pResult.Length) ? pResult[i] : ',');
				if (c >= '0' && c <= '9')
				{
					num = num * 10 + (c - 48);
					flag = true;
					continue;
				}
				if (flag && num == (int)m)
				{
					return true;
				}
				num = 0;
				flag = false;
			}
			return false;
		}

		public static Move Ready(Actor a)
		{
			int pResult = 0;
			try
			{
				a?.data.get("fp_move", out pResult, 0);
			}
			catch
			{
			}
			Move move = (Move)Mathf.Clamp(pResult, 0, 4);
			if (!Knows(a, move))
			{
				return Move.None;
			}
			return move;
		}

		public static bool IsReady(Actor a, Move m)
		{
			if (m != Move.None)
			{
				return Ready(a) == m;
			}
			return false;
		}

		public static void SetReady(Actor a, Move m)
		{
			if (a == null || (m != Move.None && !Knows(a, m)))
			{
				return;
			}
			try
			{
				a.data.set("fp_move", (int)m);
			}
			catch (Exception ex)
			{
				Log.Error("moves: " + ex.Message);
			}
		}

		public static bool Learn(Actor a, Move m)
		{
			if (a == null || m == Move.None || m == Move.Dodge)
			{
				return false;
			}
			List<Move> list = Known(a);
			if (list.Contains(m))
			{
				return false;
			}
			list.Add(m);
			Write(a, list);
			if (Ready(a) == Move.None)
			{
				SetReady(a, m);
			}
			Log.Info("moves: " + WorldBoxMod.Describe(a) + " learned the " + Name(m).ToLowerInvariant());
			return true;
		}

		private static void Write(Actor a, List<Move> known)
		{
			string[] array = new string[known.Count];
			for (int i = 0; i < known.Count; i++)
			{
				array[i] = ((int)known[i]).ToString();
			}
			try
			{
				a.data.set("fp_moves", string.Join(",", array));
			}
			catch (Exception ex)
			{
				Log.Error("moves: " + ex.Message);
			}
		}

		public static void Rewarded(Quest q, Actor you, Action<string> toast)
		{
			if (q == null || you == null || (q.Kind != QuestKind.Undead && q.Kind != QuestKind.Raid && (q.Kind != QuestKind.Bounty || q.Money < 100)))
			{
				return;
			}
			List<Move> list = Known(you);
			List<Move> list2 = new List<Move>();
			Move[] all = All;
			foreach (Move item in all)
			{
				if (!list.Contains(item))
				{
					list2.Add(item);
				}
			}
			if (list2.Count != 0)
			{
				Move m = list2[Rng.Next(list2.Count)];
				if (Learn(you, m))
				{
					toast?.Invoke("You learned the " + Name(m).ToLowerInvariant() + " (" + Input(m) + "). Choose it on the Magic page (Tab)");
				}
			}
		}

		public static void PassOn(string known, int ready, Actor heir)
		{
			if (heir == null || string.IsNullOrEmpty(known))
			{
				return;
			}
			try
			{
				heir.data.set("fp_moves", known);
				heir.data.set("fp_move", ready);
			}
			catch (Exception ex)
			{
				Log.Error("moves: passing on: " + ex.Message);
			}
		}

		public static void Read(Actor a, out string known, out int ready)
		{
			known = null;
			ready = 0;
			try
			{
				a?.data.get("fp_moves", out known, null);
				a?.data.get("fp_move", out ready, 0);
			}
			catch
			{
			}
		}

		public static float TakePending()
		{
			float pending = Pending;
			Pending = 1f;
			LastScaleForTest = pending;
			return pending;
		}
	}
}
