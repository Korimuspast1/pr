using UnityEngine;

namespace FirstPerson
{
	internal static class Journal
	{
		public static bool Open;

		public static int Tab;

		public static Vector2 Scroll;

		public static long PickedGiver = -1L;

		public static QuestKind PickedKind;

		public static long ConfirmGiver = -1L;

		public static float ConfirmUntil;

		public static string PickedEntry;

		public static readonly string[] Tabs = new string[3] { "Tasks", "Pings", "Standing" };

		public static Vector2 Centre;

		public static float Across = 160f;

		public static bool Fit;

		public static Vector2? GlideTo;

		public const float AcrossMin = 24f;

		public static void Toggle()
		{
			Open = !Open;
			Scroll = Vector2.zero;
			ConfirmGiver = -1L;
			GlideTo = null;
		}

		public static bool Is(Quest q, long giver, QuestKind kind)
		{
			if (q != null && q.Giver == giver)
			{
				return q.Kind == kind;
			}
			return false;
		}
	}
}
