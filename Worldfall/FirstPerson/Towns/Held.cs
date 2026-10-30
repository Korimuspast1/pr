using System;
using FirstPerson.Core;

namespace FirstPerson.Towns
{
	internal static class Held
	{
		private static string _key;

		private static Func<string, MeshModel> _keyLibrary;

		private static Carried _item;

		public static Carried Item(Actor you)
		{
			if (you == null)
			{
				return null;
			}
			string text = null;
			try
			{
				text = Hotbar.InHand(you);
			}
			catch
			{
			}
			if ((object)text == _key && _keyLibrary == ViewMeshes.Library)
			{
				return _item;
			}
			string text2 = BagOrder.ResourceOf(text);
			_key = text;
			_keyLibrary = ViewMeshes.Library;
			_item = ((text2 == null || Supplies.Is(text2)) ? null : CarryLook.Item(text2));
			return _item;
		}

		public static MeshModel Model(Actor you)
		{
			return Item(you)?.Model(0);
		}

		public static WeaponKind Kind(Actor you)
		{
			return WeaponKind.None;
		}
	}
}
