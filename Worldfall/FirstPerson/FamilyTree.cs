using System.Collections.Generic;

namespace FirstPerson
{
	internal sealed class FamilyTree
	{
		public TreeNode Self;

		public readonly TreeNode[] Grandparents = new TreeNode[4];

		public readonly TreeNode[] Parents = new TreeNode[2];

		public readonly List<TreeNode> Siblings = new List<TreeNode>();

		public readonly List<TreeNode> Partners = new List<TreeNode>();

		public readonly List<TreeNode> Children = new List<TreeNode>();

		public string FamilyName;

		public string Founders;

		public string ClanName;

		public string Motto;

		public string ClanFounder;

		public string Chief;

		public int Generation;

		public int PastChiefs;

		public int Count
		{
			get
			{
				int num = Siblings.Count + Partners.Count + Children.Count;
				TreeNode[] grandparents = Grandparents;
				for (int i = 0; i < grandparents.Length; i++)
				{
					if (grandparents[i] != null)
					{
						num++;
					}
				}
				grandparents = Parents;
				for (int i = 0; i < grandparents.Length; i++)
				{
					if (grandparents[i] != null)
					{
						num++;
					}
				}
				return num;
			}
		}
	}
}
