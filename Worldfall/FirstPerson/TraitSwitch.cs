using System;
using System.Collections.Generic;

namespace FirstPerson
{
	internal static class TraitSwitch
	{
		public const string EarnedKey = "fp_earned";

		private static List<string> Read(Actor a)
		{
			List<string> list = new List<string>();
			string pResult = null;
			try
			{
				a?.data.get("fp_earned", out pResult, null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(pResult))
			{
				return list;
			}
			string[] array = pResult.Split(',');
			foreach (string text in array)
			{
				if (text.Length > 0 && !list.Contains(text))
				{
					list.Add(text);
				}
			}
			return list;
		}

		private static void Write(Actor a, List<string> list)
		{
			try
			{
				a.data.set("fp_earned", string.Join(",", list.ToArray()));
			}
			catch (Exception ex)
			{
				Log.Error("traits: " + ex.Message);
			}
		}

		public static void Earned(Actor a, string id)
		{
			if (a != null && !string.IsNullOrEmpty(id))
			{
				List<string> list = Read(a);
				if (!list.Contains(id))
				{
					list.Add(id);
					Write(a, list);
				}
			}
		}

		public static List<string> EarnedOf(Actor a)
		{
			return Read(a);
		}

		public static bool IsEarned(Actor a, string id)
		{
			return Read(a).Contains(id);
		}

		public static bool Owns(Actor a, string id)
		{
			return IsEarned(a, id);
		}

		public static bool IsOn(Actor a, string id)
		{
			try
			{
				return a.hasTrait(id);
			}
			catch
			{
				return false;
			}
		}

		public static bool Set(Actor a, string id, bool on)
		{
			if (a == null || !IsEarned(a, id))
			{
				return false;
			}
			try
			{
				if (on == a.hasTrait(id))
				{
					return true;
				}
				if (on)
				{
					ActorTrait actorTrait = AssetManager.traits.get(id);
					if (actorTrait == null || !a.addTrait(actorTrait, pRemoveOpposites: true))
					{
						return false;
					}
				}
				else
				{
					a.removeTrait(id);
					if (a.hasTrait(id))
					{
						return false;
					}
				}
				try
				{
					a.setStatsDirty();
				}
				catch
				{
				}
				Log.Info("traits: " + WorldBoxMod.Describe(a) + " switched " + id + " " + (on ? "on" : "off"));
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("traits: switching " + id + ": " + ex.Message);
				return false;
			}
		}
	}
}
