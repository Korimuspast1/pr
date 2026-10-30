using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FirstPerson
{
	internal static class Gear
	{
		internal sealed class Piece
		{
			public string Id;

			public string By;

			public string Name;

			public string[] Mods = new string[0];

			public int Kills;

			public EquipmentAsset Asset
			{
				get
				{
					try
					{
						return AssetManager.items.get(Id);
					}
					catch
					{
						return null;
					}
				}
			}
		}

		public const string Key = "fp_gear";

		public const int Max = 24;

		private static long _cacheFor = -1L;

		private static string _cacheRaw;

		private static List<Piece> _cache = new List<Piece>();

		public static List<Piece> Of(Actor a)
		{
			string pResult = null;
			long num = -1L;
			try
			{
				num = a.data.id;
				a.data.get("fp_gear", out pResult, null);
			}
			catch
			{
				return new List<Piece>();
			}
			if (num == _cacheFor && pResult == _cacheRaw)
			{
				return _cache;
			}
			_cacheFor = num;
			_cacheRaw = pResult;
			_cache = Parse(pResult);
			return _cache;
		}

		public static int Count(Actor a)
		{
			return Of(a).Count;
		}

		public static bool HasRoom(Actor a, int more = 1)
		{
			return Bag.Free(a) >= more;
		}

		public static bool Add(Actor a, Piece p)
		{
			if (p == null || p.Asset == null || !HasRoom(a))
			{
				return false;
			}
			List<Piece> list = new List<Piece>(Of(a)) { p };
			Save(a, list);
			return true;
		}

		internal static void AddOver(Actor a, Piece p)
		{
			if (p != null && p.Asset != null)
			{
				Save(a, new List<Piece>(Of(a)) { p });
			}
		}

		public static Piece Remove(Actor a, int index)
		{
			List<Piece> list = new List<Piece>(Of(a));
			if (index < 0 || index >= list.Count)
			{
				return null;
			}
			Piece result = list[index];
			list.RemoveAt(index);
			Save(a, list);
			return result;
		}

		public static Piece FromItem(Item item)
		{
			if (item == null || item.asset == null)
			{
				return null;
			}
			Piece piece = new Piece
			{
				Id = item.asset.id
			};
			try
			{
				ItemData data = item.data;
				piece.By = data.by;
				piece.Kills = data.kills;
				piece.Name = data.name;
				if (data.modifiers != null)
				{
					List<string> list = new List<string>();
					foreach (ref string modifier in data.modifiers)
					{
						string current = modifier;
						if (!string.IsNullOrEmpty(current))
						{
							list.Add(current);
						}
					}
					piece.Mods = list.ToArray();
				}
			}
			catch
			{
			}
			return piece;
		}

		public static Item Build(Piece p, Actor owner)
		{
			EquipmentAsset asset = p.Asset;
			if (asset == null)
			{
				return null;
			}
			Item item = World.world.items.generateItem(asset, owner.kingdom, string.IsNullOrEmpty(p.By) ? owner.getName() : p.By, 0, owner, 0, pByPlayer: true);
			if (item == null)
			{
				return null;
			}
			try
			{
				List<string> list = new List<string>();
				if (item.data.modifiers != null)
				{
					foreach (ref string modifier in item.data.modifiers)
					{
						string current = modifier;
						list.Add(current);
					}
				}
				foreach (string item2 in list)
				{
					item.removeMod(item2);
				}
				string[] mods = p.Mods;
				foreach (string pModId in mods)
				{
					item.addMod(pModId);
				}
				if (!string.IsNullOrEmpty(p.Name))
				{
					item.data.name = p.Name;
				}
				item.data.kills = p.Kills;
				item.calculateValues();
			}
			catch (Exception ex)
			{
				Log.Error("gear: rebuilding " + p.Id + ": " + ex.Message);
			}
			return item;
		}

		public static string NameOf(Piece p)
		{
			if (!string.IsNullOrEmpty(p.Name))
			{
				return p.Name;
			}
			EquipmentAsset asset = p.Asset;
			try
			{
				string text = asset?.getTranslatedName();
				if (!string.IsNullOrEmpty(text) && !text.StartsWith("$"))
				{
					return text;
				}
			}
			catch
			{
			}
			return (p.Id ?? "item").Replace('_', ' ');
		}

		public static bool PutOn(Actor a, int index, Action<string> toast, bool quiet = false)
		{
			List<Piece> list = Of(a);
			if (index < 0 || index >= list.Count)
			{
				return false;
			}
			Piece piece = list[index];
			EquipmentAsset asset = piece.Asset;
			if (asset == null)
			{
				Remove(a, index);
				BagOrder.PieceGone(a, index);
				toast?.Invoke("That piece is gone");
				return false;
			}
			ActorEquipmentSlot slot;
			try
			{
				if (a.equipment == null || !a.understandsHowToUseItems())
				{
					toast?.Invoke("This creature can't use gear");
					return false;
				}
				slot = a.equipment.getSlot(asset.equipment_type);
			}
			catch
			{
				toast?.Invoke("This creature can't use gear");
				return false;
			}
			Item item = slot.getItem();
			if (item != null && item.isCursed())
			{
				toast?.Invoke("Your cursed " + NiceName(item) + " won't come off");
				return false;
			}
			Item item2 = Build(piece, a);
			if (item2 == null)
			{
				return false;
			}
			Piece piece2 = ((item != null) ? FromItem(item) : null);
			if (item != null)
			{
				slot.takeAwayItem();
			}
			try
			{
				slot.setItem(item2, a);
			}
			catch (Exception ex)
			{
				Log.Error("gear: putting on " + piece.Id + ": " + ex.Message);
			}
			if (slot.getItem() != item2)
			{
				bool flag = item == null;
				if (item != null)
				{
					try
					{
						if (slot.isEmpty())
						{
							slot.setItem(item, a);
						}
					}
					catch
					{
					}
					flag = slot.getItem() == item;
				}
				LetGo(item2);
				if (!flag)
				{
					LetGo(item);
					AddOver(a, piece2);
				}
				Log.Error("gear: " + piece.Id + " would not go on" + ((slot.getItem() != null) ? (" (" + slot.getItem().asset?.id + " is there)") : "") + ", kept in the bag");
				toast?.Invoke("That piece won't go on");
				return false;
			}
			List<Piece> list2 = new List<Piece>(list);
			list2.RemoveAt(index);
			if (item != null)
			{
				LetGo(item);
				if (piece2 != null)
				{
					list2.Insert(Mathf.Min(index, list2.Count), piece2);
				}
			}
			Save(a, list2);
			try
			{
				a.setStatsDirty();
			}
			catch
			{
			}
			if (!quiet)
			{
				Sounds.PlayAt("event:/SFX/UI/YesPoint", a.current_position);
			}
			Log.Info("gear: put on " + piece.Id + " (" + piece.Mods.Length + " enchantments)" + ((item != null) ? (", " + item.asset.id + " to the bag") : ""));
			return true;
		}

		public static bool TakeOff(Actor a, EquipmentType type, Action<string> toast, bool quiet = false)
		{
			ActorEquipmentSlot slot;
			Item item;
			try
			{
				slot = a.equipment.getSlot(type);
				item = slot.getItem();
			}
			catch
			{
				return false;
			}
			if (item == null)
			{
				return false;
			}
			if (item.isCursed())
			{
				toast?.Invoke("Your cursed " + NiceName(item) + " won't come off");
				return false;
			}
			if (!HasRoom(a, (type != EquipmentType.Weapon || !Hotbar.HandCounts(a)) ? 1 : 0))
			{
				toast?.Invoke("No room in your bag");
				return false;
			}
			Piece piece = FromItem(item);
			if (piece == null)
			{
				return false;
			}
			slot.takeAwayItem();
			LetGo(item);
			if (!Add(a, piece))
			{
				AddOver(a, piece);
			}
			a.setStatsDirty();
			Log.Info("gear: took off " + piece.Id + " into the bag" + (quiet ? " (hotbar)" : ""));
			return true;
		}

		public static Piece Strip(Actor a, EquipmentType type, Action<string> toast)
		{
			ActorEquipmentSlot slot;
			Item item;
			try
			{
				slot = a.equipment.getSlot(type);
				item = slot.getItem();
			}
			catch
			{
				return null;
			}
			if (item == null)
			{
				return null;
			}
			if (item.isCursed())
			{
				toast?.Invoke("Your cursed " + NiceName(item) + " won't come off");
				return null;
			}
			Piece piece = FromItem(item);
			if (piece == null)
			{
				return null;
			}
			slot.takeAwayItem();
			LetGo(item);
			a.setStatsDirty();
			Log.Info("gear: took off " + piece.Id + " to drop it");
			return piece;
		}

		public static void LetGo(Item item)
		{
			try
			{
				World.world.items.removeObject(item);
			}
			catch
			{
			}
		}

		private static string NiceName(Item item)
		{
			try
			{
				return item.asset.id.Replace('_', ' ');
			}
			catch
			{
				return "gear";
			}
		}

		private static string Clean(string s)
		{
			if (!string.IsNullOrEmpty(s))
			{
				return s.Replace("|", " ").Replace(",", " ").Replace("+", " ");
			}
			return "";
		}

		private static void Save(Actor a, List<Piece> list)
		{
			string text = Format(list);
			try
			{
				if (text.Length == 0)
				{
					a.data.removeString("fp_gear");
				}
				else
				{
					a.data.set("fp_gear", text);
				}
			}
			catch (Exception ex)
			{
				Log.Error("gear: save: " + ex.Message);
			}
		}

		internal static string Format(List<Piece> list)
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (Piece item in list)
			{
				if (stringBuilder.Length > 0)
				{
					stringBuilder.Append('|');
				}
				stringBuilder.Append(Clean(item.Id)).Append(',');
				for (int i = 0; i < item.Mods.Length; i++)
				{
					if (i > 0)
					{
						stringBuilder.Append('+');
					}
					stringBuilder.Append(Clean(item.Mods[i]));
				}
				stringBuilder.Append(',').Append(item.Kills).Append(',')
					.Append(Clean(item.By))
					.Append(',')
					.Append(Clean(item.Name));
			}
			return stringBuilder.ToString();
		}

		internal static List<Piece> Parse(string raw)
		{
			List<Piece> list = new List<Piece>();
			if (string.IsNullOrEmpty(raw))
			{
				return list;
			}
			string[] array = raw.Split('|');
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(new char[1] { ',' }, 5);
				if (array2.Length >= 1 && array2[0].Length != 0)
				{
					Piece piece = new Piece
					{
						Id = array2[0]
					};
					if (array2.Length > 1 && array2[1].Length > 0)
					{
						piece.Mods = array2[1].Split('+');
					}
					if (array2.Length > 2)
					{
						int.TryParse(array2[2], out piece.Kills);
					}
					if (array2.Length > 3)
					{
						piece.By = array2[3];
					}
					if (array2.Length > 4)
					{
						piece.Name = array2[4];
					}
					list.Add(piece);
				}
			}
			return list;
		}
	}
}
