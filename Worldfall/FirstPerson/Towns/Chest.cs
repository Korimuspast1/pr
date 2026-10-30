using System;
using System.Collections.Generic;
using System.Text;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal static class Chest
	{
		private sealed class HouseStore : CraftStores.Store
		{
			public Building B;

			public override string Name => "your house's storage";

			public override int Count(string resource)
			{
				return Chest.Count(B, resource);
			}

			public override int Take(string resource, int amount)
			{
				int num = Mathf.Min(amount, Chest.Count(B, resource));
				if (num > 0)
				{
					Change(B, resource, -num);
				}
				return num;
			}
		}

		public const string Key = "fp_chest";

		public const string GearKey = "fp_chest_gear";

		private static readonly int[] TierSlots = new int[6] { 20, 24, 32, 40, 52, 64 };

		private const int TentSlots = 16;

		private const float Reach = 1.25f;

		private const float Over = 0.45f;

		private const float Facing = 32f;

		public static bool Open;

		public static Building House;

		private static int _openedFrame = -1;

		private static int _closedFrame = -1;

		private static bool _registered;

		private static readonly HouseStore Home = new HouseStore();

		private static readonly List<CraftStores.Store> NoStores = new List<CraftStores.Store>();

		private static readonly List<CraftStores.Store> OneStore = new List<CraftStores.Store>(1);

		public static bool Escape()
		{
			if (!Open)
			{
				return false;
			}
			Close();
			return true;
		}

		public static void Show(Building b)
		{
			House = b;
			Open = b != null;
			_openedFrame = Time.frameCount;
		}

		public static void Close()
		{
			if (Open)
			{
				_closedFrame = Time.frameCount;
			}
			Open = false;
			House = null;
		}

		public static bool Yours(Building b, Actor you)
		{
			if (b != null && you != null)
			{
				return Homes.OwnedBy(b, you);
			}
			return false;
		}

		public static void Update(Actor host)
		{
			if (!_registered)
			{
				_registered = true;
				CraftStores.Chests.Add(StoresNear);
			}
			if (Open)
			{
				WorldBoxMod instance = WorldBoxMod.Instance;
				bool flag = false;
				try
				{
					flag = House != null && House.isAlive();
				}
				catch
				{
				}
				if (!flag || host == null || instance == null || !instance.Interior.Inside || instance.Interior.House != House)
				{
					Close();
				}
				else if (Time.frameCount != _openedFrame && PlayInput.GetKeyDown(KeyCode.E))
				{
					Close();
				}
			}
		}

		public static bool RoomPrompt(PromptAsk ask)
		{
			if (ask?.Room?.Room == null || ask.House == null || ask.Host == null || ask.Room.Virtual)
			{
				return false;
			}
			if (!Yours(ask.House, ask.Host))
			{
				return false;
			}
			if (!NearChest(ask.Room.Room, ask.Eye, ask.Yaw, out var any) && (any || HomeLife.NearBed(ask.Room.Room, ask.Eye, ask.Yaw)))
			{
				return false;
			}
			ask.Prompt = "E:Storage";
			if (PlayInput.GetKeyDown(KeyCode.E) && !Open && Time.frameCount != _closedFrame)
			{
				Show(ask.House);
			}
			return true;
		}

		private static bool NearChest(Interior room, Vector2 eye, float yaw, out bool any)
		{
			any = false;
			foreach (MeshInstance mesh in room.Meshes)
			{
				if (!IsChest(mesh.Model))
				{
					continue;
				}
				any = true;
				float num = mesh.Position.X - eye.x;
				float num2 = mesh.Position.Y - eye.y;
				float num3 = num * num + num2 * num2;
				if (!(num3 > 1.5625f))
				{
					if (num3 < 0.20249999f)
					{
						return true;
					}
					if (Mathf.Abs(Mathf.DeltaAngle(yaw * 57.29578f, Mathf.Atan2(num2, num) * 57.29578f)) <= 32f)
					{
						return true;
					}
				}
			}
			return false;
		}

		private static bool IsChest(MeshModel model)
		{
			string text = model?.Name;
			if (text == null)
			{
				return false;
			}
			if (!text.EndsWith("_chest", StringComparison.Ordinal) && !(text == "int_crate") && !(text == "int_barrel"))
			{
				return text == "int_sack";
			}
			return true;
		}

		internal static bool Faced(Interior room, Vector2 eye, float yaw, out float d2)
		{
			d2 = float.MaxValue;
			if (room == null)
			{
				return false;
			}
			foreach (MeshInstance mesh in room.Meshes)
			{
				if (IsChest(mesh.Model))
				{
					float num = mesh.Position.X - eye.x;
					float num2 = mesh.Position.Y - eye.y;
					float num3 = num * num + num2 * num2;
					if (!(num3 > 1.5625f) && !(num3 >= d2) && Mathf.Abs(Mathf.DeltaAngle(yaw * 57.29578f, Mathf.Atan2(num2, num) * 57.29578f)) <= 32f)
					{
						d2 = num3;
					}
				}
			}
			return d2 < float.MaxValue;
		}

		internal static bool ChestOf(Interior room, out Vector2 at)
		{
			at = default(Vector2);
			foreach (MeshInstance mesh in room.Meshes)
			{
				if (IsChest(mesh.Model))
				{
					at = new Vector2(mesh.Position.X, mesh.Position.Y);
					return true;
				}
			}
			return false;
		}

		public static int Size(Building b)
		{
			if (b == null)
			{
				return 0;
			}
			if (HomeGrowth.IsTent(b))
			{
				return 16;
			}
			int tier = 0;
			try
			{
				HouseInterior.RaceAndTier(b.asset, out var _, out tier);
			}
			catch
			{
			}
			return TierSlots[Mathf.Clamp(tier, 0, TierSlots.Length - 1)];
		}

		public static List<KeyValuePair<string, int>> Resources(Building b)
		{
			List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>();
			string pResult = null;
			try
			{
				b?.data.get("fp_chest", out pResult, null);
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
				int num = text.IndexOf('=');
				if (num > 0 && int.TryParse(text.Substring(num + 1), out var result) && result > 0)
				{
					list.Add(new KeyValuePair<string, int>(text.Substring(0, num), result));
				}
			}
			return list;
		}

		public static List<Gear.Piece> Pieces(Building b)
		{
			string pResult = null;
			try
			{
				b?.data.get("fp_chest_gear", out pResult, null);
			}
			catch
			{
			}
			return Gear.Parse(pResult);
		}

		public static int Count(Building b, string res)
		{
			string pResult = null;
			try
			{
				b?.data.get("fp_chest", out pResult, null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(pResult) || string.IsNullOrEmpty(res))
			{
				return 0;
			}
			int num = 0;
			while (num < pResult.Length)
			{
				int num2 = pResult.IndexOf(',', num);
				if (num2 < 0)
				{
					num2 = pResult.Length;
				}
				int num3 = pResult.IndexOf('=', num, num2 - num);
				if (num3 - num == res.Length && string.CompareOrdinal(pResult, num, res, 0, res.Length) == 0)
				{
					int num4 = 0;
					bool flag = num3 + 1 < num2;
					for (int i = num3 + 1; i < num2 && flag; i++)
					{
						char c = pResult[i];
						if (c < '0' || c > '9')
						{
							flag = false;
						}
						else
						{
							num4 = num4 * 10 + (c - 48);
						}
					}
					if (flag && num4 > 0)
					{
						return num4;
					}
				}
				num = num2 + 1;
			}
			return 0;
		}

		public static int Used(Building b)
		{
			int num = Pieces(b).Count;
			foreach (KeyValuePair<string, int> item in Resources(b))
			{
				num += Bag.SlotsFor(item.Key, item.Value);
			}
			return num;
		}

		public static int Free(Building b)
		{
			return Mathf.Max(0, Size(b) - Used(b));
		}

		public static int RoomFor(Building b, string res)
		{
			int num = Count(b, res);
			int num2 = Bag.Stack(res);
			return ((num % num2 != 0) ? (num2 - num % num2) : 0) + Free(b) * num2;
		}

		private static void Change(Building b, string res, int delta)
		{
			List<KeyValuePair<string, int>> list = Resources(b);
			int num = list.FindIndex((KeyValuePair<string, int> kv) => kv.Key == res);
			int num2 = ((num >= 0) ? list[num].Value : 0) + delta;
			if (num >= 0)
			{
				list.RemoveAt(num);
			}
			if (num2 > 0)
			{
				list.Insert((num >= 0) ? num : list.Count, new KeyValuePair<string, int>(res, num2));
			}
			StringBuilder stringBuilder = new StringBuilder();
			foreach (KeyValuePair<string, int> item in list)
			{
				if (stringBuilder.Length > 0)
				{
					stringBuilder.Append(',');
				}
				stringBuilder.Append(item.Key).Append('=').Append(item.Value);
			}
			try
			{
				if (stringBuilder.Length == 0)
				{
					b.data.removeString("fp_chest");
				}
				else
				{
					b.data.set("fp_chest", stringBuilder.ToString());
				}
			}
			catch (Exception ex)
			{
				Log.Error("chest: save: " + ex.Message);
			}
		}

		private static void SavePieces(Building b, List<Gear.Piece> list)
		{
			string text = Gear.Format(list);
			try
			{
				if (text.Length == 0)
				{
					b.data.removeString("fp_chest_gear");
				}
				else
				{
					b.data.set("fp_chest_gear", text);
				}
			}
			catch (Exception ex)
			{
				Log.Error("chest: save gear: " + ex.Message);
			}
		}

		public static int Store(Actor you, Building b, string res, int amount)
		{
			if (you == null || b == null || string.IsNullOrEmpty(res))
			{
				return 0;
			}
			int num = Mathf.Min(amount, Mathf.Min(Civil.BagCount(you, res), RoomFor(b, res)));
			if (num <= 0 || !Civil.TakeFromBag(you, res, num))
			{
				return 0;
			}
			Change(b, res, num);
			return num;
		}

		public static int Fetch(Actor you, Building b, string res, int amount)
		{
			if (you == null || b == null || string.IsNullOrEmpty(res))
			{
				return 0;
			}
			int num = Mathf.Min(amount, Mathf.Min(Count(b, res), Bag.RoomFor(you, res)));
			if (num <= 0)
			{
				return 0;
			}
			num = Bag.Put(you, res, num, spill: false);
			if (num > 0)
			{
				Change(b, res, -num);
			}
			return num;
		}

		public static bool StorePiece(Actor you, Building b, int index)
		{
			if (you == null || b == null || Free(b) <= 0)
			{
				return false;
			}
			Gear.Piece piece = Gear.Remove(you, index);
			if (piece == null)
			{
				return false;
			}
			BagOrder.PieceGone(you, index);
			List<Gear.Piece> list = Pieces(b);
			list.Add(piece);
			SavePieces(b, list);
			return true;
		}

		public static bool FetchPiece(Actor you, Building b, int index)
		{
			if (you == null || b == null)
			{
				return false;
			}
			List<Gear.Piece> list = Pieces(b);
			if (index < 0 || index >= list.Count || !Gear.HasRoom(you))
			{
				return false;
			}
			if (!Gear.Add(you, list[index]))
			{
				return false;
			}
			list.RemoveAt(index);
			SavePieces(b, list);
			return true;
		}

		private static IEnumerable<CraftStores.Store> StoresNear(Actor you)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			Building b = ((instance != null && instance.Interior.Inside) ? instance.Interior.House : (Workbench.AtHome(you) ? Workbench.HomeOf(you) : null));
			if (!Yours(b, you))
			{
				return NoStores;
			}
			Home.B = b;
			OneStore.Clear();
			OneStore.Add(Home);
			return OneStore;
		}
	}
}
