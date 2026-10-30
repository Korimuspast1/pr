using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace FirstPerson.Core
{
	public static class HallRoomBuilder
	{
		public struct Spot
		{
			public Vec3 At;

			public float Yaw;

			public bool HasYaw;
		}

		private sealed class Info
		{
			public string Kind;

			public readonly Dictionary<string, Spot[]> Anchors = new Dictionary<string, Spot[]>();

			public int[] Coins = new int[0];
		}

		public static readonly string[] Names = new string[7] { "seat", "war_table", "cell", "cell_door", "throne", "petition", "steward" };

		private static readonly ConditionalWeakTable<Interior, Info> Rooms = new ConditionalWeakTable<Interior, Info>();

		private static readonly Spot[] None = new Spot[0];

		public static Interior Build(MeshModel model, uint kingdom)
		{
			Interior interior = CabinBuilder.Build(model, 0u);
			interior.Race = model.Info("race") ?? "human";
			MeshInstance meshInstance = ((interior.Meshes.Count > 0) ? interior.Meshes[0] : null);
			if (meshInstance != null && kingdom != 0)
			{
				for (int i = 0; i < model.SlotNames.Length; i++)
				{
					if (model.SlotNames[i] == "banner")
					{
						meshInstance.Palette[i] = kingdom;
					}
					else if (model.SlotNames[i] == "banner_dark")
					{
						meshInstance.Palette[i] = TerrainCache.Scale(kingdom, 0.6f);
					}
				}
			}
			Info info = new Info
			{
				Kind = ((model.Name != null && model.Name.StartsWith("tw_room_keep_", StringComparison.Ordinal)) ? "keep" : "hall")
			};
			Vec3 vec = meshInstance?.Position ?? new Vec3(0f, 0f, 0f);
			string[] names = Names;
			foreach (string text in names)
			{
				string text2 = model.Info("a_" + text);
				if (string.IsNullOrEmpty(text2))
				{
					continue;
				}
				List<Spot> list = new List<Spot>();
				string[] array = text2.Split('/');
				for (int k = 0; k < array.Length; k++)
				{
					string[] array2 = array[k].Split(',');
					if (array2.Length >= 3 && Num(array2[0], out var v) && Num(array2[1], out var v2) && Num(array2[2], out var v3))
					{
						Spot item = new Spot
						{
							At = new Vec3(vec.X + v * 0.5714286f, vec.Y + v2 * 0.5714286f, v3 * 0.5714286f)
						};
						if (array2.Length >= 4 && Num(array2[3], out var v4))
						{
							item.Yaw = v4;
							item.HasYaw = true;
						}
						list.Add(item);
					}
				}
				info.Anchors[text] = list.ToArray();
			}
			List<int> list2 = new List<int>();
			for (int l = 1; l <= 3; l++)
			{
				int num = model.PartIndex("coins_" + l);
				if (num >= 0)
				{
					list2.Add(num);
				}
			}
			info.Coins = list2.ToArray();
			Rooms.Remove(interior);
			Rooms.Add(interior, info);
			Treasury(interior, 0.5f);
			return interior;
		}

		private static bool Num(string s, out float v)
		{
			return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
		}

		public static Spot[] Spots(Interior room, string name)
		{
			if (room == null || string.IsNullOrEmpty(name) || !Rooms.TryGetValue(room, out var value))
			{
				return None;
			}
			string key = (name.StartsWith("a_", StringComparison.Ordinal) ? name.Substring(2) : name);
			if (!value.Anchors.TryGetValue(key, out var value2))
			{
				return None;
			}
			return value2;
		}

		public static string KindOf(Interior room)
		{
			if (room == null || !Rooms.TryGetValue(room, out var value))
			{
				return null;
			}
			return value.Kind;
		}

		public static void Treasury(Interior room, float fill)
		{
			if (room != null && room.Meshes.Count != 0 && Rooms.TryGetValue(room, out var value) && value.Coins.Length != 0)
			{
				MeshInstance meshInstance = room.Meshes[0];
				meshInstance.EnsurePose();
				int num = ((!(fill <= 0.02f)) ? ((fill < 0.34f) ? 1 : ((fill < 0.67f) ? 2 : 3)) : 0);
				for (int i = 0; i < value.Coins.Length; i++)
				{
					meshInstance.PartScale[value.Coins[i]] = ((i < num) ? 1f : 0f);
				}
			}
		}
	}
}
