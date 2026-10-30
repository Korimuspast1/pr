using System.Collections.Generic;
using System.Linq;

namespace FirstPerson
{
	internal static class DeathCause
	{
		public static string Of(Actor a)
		{
			if (a == null)
			{
				return "";
			}
			List<string> list = new List<string>();
			try
			{
				list.Add("last hit " + a._last_attack_type);
			}
			catch
			{
			}
			try
			{
				BaseSimObject attackedBy = a.attackedBy;
				if (attackedBy != null)
				{
					list.Add("by " + (attackedBy.isActor() ? WorldBoxMod.Describe(attackedBy.a) : (attackedBy.isBuilding() ? "a building" : "something")));
				}
			}
			catch
			{
			}
			try
			{
				if (a.data != null)
				{
					list.Add("food " + a.data.nutrition + ", age " + a.data.getAge());
				}
			}
			catch
			{
			}
			try
			{
				string text = string.Join(",", a.getStatusesIds().ToArray());
				if (text.Length > 0)
				{
					list.Add("statuses " + text);
				}
			}
			catch
			{
			}
			if (list.Count <= 0)
			{
				return "";
			}
			return " (" + string.Join("; ", list.ToArray()) + ")";
		}
	}
}
