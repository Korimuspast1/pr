namespace FirstPerson
{
	internal static class MapTabs
	{
		public enum Page
		{
			Map,
			War,
			Orders,
			Family,
			News,
			Council
		}

		public static readonly Page[] All = new Page[6]
		{
			Page.Map,
			Page.News,
			Page.War,
			Page.Council,
			Page.Orders,
			Page.Family
		};

		public static readonly string[] Names = new string[6] { "Map", "War", "Orders", "Family", "News", "Council" };

		public static readonly string[] Icons = new string[6] { "iconWorldInfo", "iconWarfare", "iconWarriors", "iconFamily", "iconWorldLog", "iconCrown" };

		public static Page? Last;

		public static int LastAmong;

		public const float Width = 700f;

		public const float Height = 430f;
	}
}
