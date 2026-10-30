using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class Shipyard
	{
		internal enum Hull : byte
		{
			Transport,
			Trading,
			Fishing
		}

		private sealed class Job
		{
			public Hull Kind;

			public double Start = -1.0;

			public long Dock;

			public int X;

			public int Y;

			public float Heading;
		}

		private sealed class Hulls
		{
			public readonly MeshModel[] Stage = new MeshModel[4];
		}

		internal static readonly string[] Names = new string[3] { "transport", "trading ship", "fishing boat" };

		internal static readonly string[] Short = new string[3] { "Transport", "Trader", "Fishing" };

		internal static readonly int[] Prices = new int[3] { 150, 80, 30 };

		private static readonly string[] Types = new string[3] { "boat_type_transport", "boat_type_trading", "boat_type_fishing" };

		public const int PerDock = 3;

		public const float BuildSeconds = 10f;

		private const string Key = "fp_shipyard";

		private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

		private static readonly Dictionary<City, List<Job>> Queues = new Dictionary<City, List<Job>>();

		private static object _world;

		private static int _readTowns;

		private static float _next;

		internal static int LaunchedForTest;

		internal static Actor LastForTest;

		private static City _countedFor;

		private static float _countedAt = -10f;

		private static readonly int[] Counted = new int[3];

		private const string YardKey = "fp_yard";

		private static readonly Dictionary<MeshModel, Hulls> Made = new Dictionary<MeshModel, Hulls>();

		private static readonly List<MeshInstance> Pool = new List<MeshInstance>();

		private static readonly Dictionary<string, uint[]> Palettes = new Dictionary<string, uint[]>();

		internal static List<Building> Docks(City c, Hull h)
		{
			List<Building> list = new List<Building>();
			try
			{
				List<Building> buildingListOfType = c.getBuildingListOfType("type_docks");
				if (buildingListOfType == null)
				{
					return list;
				}
				foreach (Building item in buildingListOfType)
				{
					if (item != null && item.isAlive() && !item.isRuin() && !item.isUnderConstruction() && item.component_docks != null && item.component_docks.tiles_ocean != null && item.component_docks.tiles_ocean.Count != 0 && (h == Hull.Fishing || Builds(item, h)))
					{
						list.Add(item);
					}
				}
			}
			catch
			{
			}
			return list;
		}

		private static bool Builds(Building b, Hull h)
		{
			string[] array = ((b.asset != null) ? b.asset.boat_types : null);
			if (array == null)
			{
				return false;
			}
			string[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				if (array2[i] == Types[(uint)h])
				{
					return true;
				}
			}
			return false;
		}

		internal static bool HasDocks(City c)
		{
			try
			{
				List<Building> buildingListOfType = c.getBuildingListOfType("type_docks");
				return buildingListOfType != null && buildingListOfType.Count > 0;
			}
			catch
			{
				return false;
			}
		}

		private static string AssetId(City c, Hull h)
		{
			try
			{
				ArchitectureAsset architecture_asset = c.getActorAsset().architecture_asset;
				switch (h)
				{
				case Hull.Transport:
					return architecture_asset.actor_asset_id_transport;
				case Hull.Trading:
					return architecture_asset.actor_asset_id_trading;
				default:
					return architecture_asset.actor_asset_id_boat_fishing ?? "boat_fishing";
				}
			}
			catch
			{
				return (h == Hull.Fishing) ? "boat_fishing" : null;
			}
		}

		internal static int Afloat(City c, Hull h)
		{
			float unscaledTime = Time.unscaledTime;
			if (c != _countedFor || unscaledTime - _countedAt > 0.5f || unscaledTime < _countedAt)
			{
				_countedFor = c;
				_countedAt = unscaledTime;
				Array.Clear(Counted, 0, 3);
				string[] array = new string[3]
				{
					AssetId(c, Hull.Transport),
					AssetId(c, Hull.Trading),
					AssetId(c, Hull.Fishing)
				};
				long num = -1L;
				try
				{
					num = c.data.id;
				}
				catch
				{
				}
				try
				{
					foreach (Actor simple in World.world.units.getSimpleList())
					{
						if (simple == null || simple.asset == null || !simple.asset.is_boat || !simple.isAlive())
						{
							continue;
						}
						int num2 = Array.IndexOf(array, simple.asset.id);
						if (num2 < 0)
						{
							continue;
						}
						bool flag = simple.city == c;
						if (!flag && simple.city == null && simple.kingdom == c.kingdom)
						{
							string pResult = null;
							try
							{
								simple.data.get("fp_yard", out pResult, null);
							}
							catch
							{
							}
							flag = pResult == num.ToString(Inv);
						}
						if (flag)
						{
							Counted[num2]++;
						}
					}
				}
				catch
				{
				}
			}
			return Counted[(uint)h];
		}

		internal static int Cap(City c, Hull h)
		{
			return 3 * Docks(c, h).Count;
		}

		internal static int Ordered(City c, Hull h)
		{
			int num = 0;
			if (Queues.TryGetValue(c, out var value))
			{
				foreach (Job item in value)
				{
					if (item.Kind == h)
					{
						num++;
					}
				}
			}
			return num;
		}

		internal static int Building(City c, out float soonest)
		{
			soonest = -1f;
			if (!Queues.TryGetValue(c, out var value) || value.Count == 0)
			{
				return 0;
			}
			double num = Now();
			foreach (Job item in value)
			{
				if (!(item.Start < 0.0))
				{
					float num2 = Mathf.Max(0f, (float)(10.0 - (num - item.Start)));
					if (soonest < 0f || num2 < soonest)
					{
						soonest = num2;
					}
				}
			}
			return value.Count;
		}

		internal static string Refusal(Actor king, City c, Hull h)
		{
			if (!Royal.IsKing(king))
			{
				return "Only the king orders ships";
			}
			bool flag;
			try
			{
				flag = c != null && c.isAlive() && c.kingdom == king.kingdom;
			}
			catch
			{
				flag = false;
			}
			if (!flag)
			{
				return "Not your town";
			}
			if (!HasDocks(c))
			{
				return "No docks";
			}
			int count = Docks(c, h).Count;
			if (count == 0)
			{
				if (h != Hull.Fishing)
				{
					return "Needs a harbour, not fishing docks";
				}
				return "The docks aren't finished";
			}
			if (Afloat(c, h) + Ordered(c, h) >= 3 * count)
			{
				return "The docks are full";
			}
			if (Economy.Money(king) < Prices[(uint)h])
			{
				return Prices[(uint)h] + " gold. You have " + Economy.Money(king);
			}
			return null;
		}

		internal static bool Order(Actor king, City c, Hull h, Action<string> toast)
		{
			if (WorldBoxMod.WorldLoading)
			{
				return false;
			}
			CheckWorld();
			string text = Refusal(king, c, h);
			if (text != null)
			{
				toast?.Invoke(text);
				return false;
			}
			int num = Prices[(uint)h];
			try
			{
				king.spendMoney(num);
			}
			catch (Exception ex)
			{
				Log.Error("shipyard: paying: " + ex.Message);
				return false;
			}
			if (!Queues.TryGetValue(c, out var value))
			{
				value = (Queues[c] = new List<Job>());
			}
			value.Add(new Job
			{
				Kind = h
			});
			Save(c, value);
			Next(c, value, Now());
			string text2 = TownName(c);
			toast?.Invoke("Shipwrights in " + text2 + " take on a " + Names[(uint)h] + " (" + num + " gold)");
			Log.Info($"shipyard: {WorldBoxMod.Describe(king)} orders a {Names[(uint)h]} at {c.name} for {num} gold; {value.Count} on order there");
			return true;
		}

		private static string TownName(City c)
		{
			try
			{
				return Renames.Apply(c.name);
			}
			catch
			{
				return "the town";
			}
		}

		private static double Now()
		{
			return WorldPace.ActionNow;
		}

		public static void Update(Action<string> toast)
		{
			if (World.world == null || WorldBoxMod.WorldLoading)
			{
				return;
			}
			float unscaledTime = Time.unscaledTime;
			if (unscaledTime < _next)
			{
				return;
			}
			_next = unscaledTime + 0.5f;
			CheckWorld();
			if (Queues.Count == 0)
			{
				return;
			}
			double num = Now();
			Kingdom kingdom = null;
			try
			{
				if (ControllableUnit.isControllingUnit())
				{
					kingdom = ControllableUnit._unit_main.kingdom;
				}
			}
			catch
			{
			}
			List<City> list = null;
			foreach (KeyValuePair<City, List<Job>> queue in Queues)
			{
				City key = queue.Key;
				List<Job> value = queue.Value;
				bool flag;
				try
				{
					flag = key != null && key.isAlive() && key.kingdom != null;
				}
				catch
				{
					flag = false;
				}
				if (!flag || value.Count == 0)
				{
					List<City> obj3 = list ?? new List<City>();
					list = obj3;
					obj3.Add(key);
					continue;
				}
				bool flag2 = false;
				for (int num2 = value.Count - 1; num2 >= 0; num2--)
				{
					Job job = value[num2];
					if (!(job.Start < 0.0))
					{
						Building building = DockById(key, job.Dock);
						if (building == null)
						{
							job.Start = -1.0;
							flag2 = true;
						}
						else if (!(num - job.Start < 10.0))
						{
							Actor actor = Launch(key, job, building);
							value.RemoveAt(num2);
							flag2 = true;
							if (actor != null && key.kingdom == kingdom)
							{
								toast?.Invoke("A " + Names[(uint)job.Kind] + " is launched at " + TownName(key));
							}
						}
					}
				}
				if (Next(key, value, num))
				{
					flag2 = true;
				}
				if (flag2)
				{
					Save(key, value);
				}
			}
			if (list == null)
			{
				return;
			}
			foreach (City item in list)
			{
				Queues.Remove(item);
			}
		}

		private static bool Next(City c, List<Job> q, double now)
		{
			bool result = false;
			foreach (Job item in q)
			{
				if (item.Start >= 0.0)
				{
					continue;
				}
				foreach (Building item2 in Docks(c, item.Kind))
				{
					long id;
					try
					{
						id = item2.data.id;
					}
					catch
					{
						continue;
					}
					bool flag = false;
					foreach (Job item3 in q)
					{
						if (item3 != item && item3.Start >= 0.0 && item3.Dock == id)
						{
							flag = true;
							break;
						}
					}
					if (!flag)
					{
						WorldTile worldTile = WaterBy(item2);
						if (worldTile != null)
						{
							item.Dock = id;
							item.X = worldTile.x;
							item.Y = worldTile.y;
							Vector2 vector = new Vector2(worldTile.x - item2.current_tile.x, worldTile.y - item2.current_tile.y);
							item.Heading = ((vector.sqrMagnitude > 0.01f) ? Mathf.Atan2(vector.x, 0f - vector.y) : 0f);
							item.Start = now;
							result = true;
							break;
						}
					}
				}
			}
			return result;
		}

		private static WorldTile WaterBy(Building dock)
		{
			try
			{
				foreach (ref WorldTile item in dock.component_docks.tiles_ocean)
				{
					WorldTile current = item;
					if (current != null && current.isGoodForBoat())
					{
						return current;
					}
				}
			}
			catch
			{
			}
			return null;
		}

		private static Building DockById(City c, long id)
		{
			try
			{
				List<Building> buildingListOfType = c.getBuildingListOfType("type_docks");
				if (buildingListOfType != null)
				{
					foreach (Building item in buildingListOfType)
					{
						if (item != null && item.isAlive() && !item.isRuin() && item.data.id == id)
						{
							return item;
						}
					}
				}
			}
			catch
			{
			}
			return null;
		}

		private static Actor Launch(City c, Job j, Building dock)
		{
			string text = AssetId(c, j.Kind);
			if (text == null)
			{
				return null;
			}
			WorldTile worldTile = null;
			try
			{
				worldTile = World.world.GetTile(j.X, j.Y);
			}
			catch
			{
			}
			if (worldTile == null || !worldTile.isGoodForBoat())
			{
				worldTile = WaterBy(dock);
			}
			if (worldTile == null)
			{
				Log.Info("shipyard: no water by the docks at " + c.name + " for a " + Names[(uint)j.Kind]);
				return null;
			}
			Actor actor = null;
			try
			{
				actor = World.world.units.createNewUnit(text, worldTile);
			}
			catch (Exception ex)
			{
				Log.Error("shipyard: launching: " + ex.Message);
			}
			if (actor == null)
			{
				return null;
			}
			try
			{
				actor.joinCity(c);
			}
			catch
			{
			}
			try
			{
				actor.data.set("fp_yard", c.data.id.ToString(Inv));
			}
			catch
			{
			}
			_countedAt = -10f;
			Hijack.KeepHome(actor);
			LaunchedForTest++;
			LastForTest = actor;
			Log.Info(string.Format("shipyard: {0} launched at {1} ({2},{3}), home {4}", WorldBoxMod.Describe(actor), c.name, worldTile.x, worldTile.y, (actor.getHomeBuilding() != null) ? "dock" : "none"));
			return actor;
		}

		private static void Save(City c, List<Job> q)
		{
			try
			{
				if (q.Count == 0)
				{
					c.data.removeString("fp_shipyard");
					return;
				}
				StringBuilder stringBuilder = new StringBuilder();
				foreach (Job item in q)
				{
					if (stringBuilder.Length > 0)
					{
						stringBuilder.Append('|');
					}
					stringBuilder.Append((int)item.Kind).Append(',').Append(item.Start.ToString("R", Inv))
						.Append(',')
						.Append(item.Dock.ToString(Inv))
						.Append(',')
						.Append(item.X.ToString(Inv))
						.Append(',')
						.Append(item.Y.ToString(Inv))
						.Append(',')
						.Append(item.Heading.ToString("R", Inv));
				}
				c.data.set("fp_shipyard", stringBuilder.ToString());
			}
			catch (Exception ex)
			{
				Log.Error("shipyard: saving: " + ex.Message);
			}
		}

		private static List<Job> Load(City c)
		{
			string pResult = null;
			try
			{
				c.data.get("fp_shipyard", out pResult, null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(pResult))
			{
				return null;
			}
			List<Job> list = new List<Job>();
			string[] array = pResult.Split('|');
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(',');
				if (array2.Length >= 6)
				{
					try
					{
						list.Add(new Job
						{
							Kind = (Hull)Mathf.Clamp(int.Parse(array2[0], Inv), 0, 2),
							Start = double.Parse(array2[1], Inv),
							Dock = long.Parse(array2[2], Inv),
							X = int.Parse(array2[3], Inv),
							Y = int.Parse(array2[4], Inv),
							Heading = float.Parse(array2[5], Inv)
						});
					}
					catch
					{
					}
				}
			}
			if (list.Count <= 0)
			{
				return null;
			}
			return list;
		}

		private static void CheckWorld()
		{
			object obj = World.world?.map_stats;
			if (obj == null)
			{
				return;
			}
			int num = 0;
			try
			{
				num = World.world.cities.list.Count;
			}
			catch
			{
			}
			if (obj == _world && (_readTowns > 0 || num == 0))
			{
				return;
			}
			_world = obj;
			_readTowns = num;
			Queues.Clear();
			try
			{
				foreach (City item in World.world.cities.list)
				{
					List<Job> list = ((item != null) ? Load(item) : null);
					if (list != null)
					{
						Queues[item] = list;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("shipyard: reading the world: " + ex.Message);
			}
			if (Queues.Count > 0)
			{
				Log.Info("shipyard: " + Queues.Count + " towns with ships on order");
			}
		}

		public static void DrawWorld(List<MeshInstance> meshes, float eyeX, float eyeY, float range)
		{
			if (Queues.Count == 0)
			{
				return;
			}
			double num = Now();
			int num2 = 0;
			float num3 = range * range;
			foreach (KeyValuePair<City, List<Job>> queue in Queues)
			{
				City key = queue.Key;
				foreach (Job item in queue.Value)
				{
					if (item.Start < 0.0)
					{
						continue;
					}
					float num4 = (float)item.X + 0.5f;
					float num5 = (float)item.Y + 0.5f;
					float num6 = num4 - eyeX;
					float num7 = num5 - eyeY;
					float num8 = num6 * num6 + num7 * num7;
					if (num8 > num3)
					{
						continue;
					}
					string text = null;
					try
					{
						text = Models.BoatFamilyOf(AssetManager.actor_library.get(AssetId(key, item.Kind)));
					}
					catch
					{
					}
					MeshModel meshModel = ((text != null) ? Models.Get(text) : null);
					if (meshModel == null)
					{
						continue;
					}
					float num9 = Mathf.Clamp01((float)((num - item.Start) / 10.0));
					MeshModel meshModel2 = Stage(meshModel, Mathf.Min(3, (int)(num9 * 4f)));
					if (meshModel2 != null)
					{
						if (num2 == Pool.Count)
						{
							Pool.Add(new MeshInstance());
						}
						MeshInstance meshInstance = Pool[num2++];
						meshInstance.Model = meshModel2;
						meshInstance.Palette = PaletteOf(meshModel, key);
						float num10 = 0.55f;
						meshInstance.Place(num4, num5, 0f - 0.06f * num10, item.Heading, num10, num10, num10);
						meshInstance.NoShadow = false;
						meshInstance.Tag = -1;
						meshInstance.SortDepth = Mathf.Sqrt(num8);
						meshes.Add(meshInstance);
					}
				}
			}
		}

		private static uint[] PaletteOf(MeshModel full, City c)
		{
			Color? kingdom = null;
			string text = full.Name;
			try
			{
				kingdom = Royal.ColorOf(c.kingdom);
				text = text + "@" + c.kingdom.getID().ToString(Inv);
			}
			catch
			{
			}
			if (!Palettes.TryGetValue(text, out var value))
			{
				if (Palettes.Count > 64)
				{
					Palettes.Clear();
				}
				value = (Palettes[text] = Models.BoatColors(full, kingdom));
			}
			return value;
		}

		private static MeshModel Stage(MeshModel full, int stage)
		{
			if (!Made.TryGetValue(full, out var value))
			{
				value = (Made[full] = new Hulls());
			}
			if (value.Stage[stage] != null)
			{
				return value.Stage[stage];
			}
			if (stage >= 3)
			{
				return value.Stage[stage] = full;
			}
			float z = full.Min.Z;
			float z2 = full.Max.Z;
			float num = z2 - z;
			float num2;
			switch (stage)
			{
			default:
				num2 = 0.6f;
				break;
			case 1:
				num2 = 0.38f;
				break;
			case 0:
				num2 = 0.22f;
				break;
			}
			float num3 = z + num * num2;
			List<MeshPart> list = new List<MeshPart>();
			MeshPart[] parts = full.Parts;
			foreach (MeshPart meshPart in parts)
			{
				List<int> list2 = new List<int>();
				List<byte> list3 = new List<byte>();
				List<byte> list4 = new List<byte>();
				for (int j = 0; j < meshPart.TriangleCount; j++)
				{
					int num4 = meshPart.Tri[j * 3];
					int num5 = meshPart.Tri[j * 3 + 1];
					int num6 = meshPart.Tri[j * 3 + 2];
					if (!(Mathf.Max(meshPart.Pos[num4 * 3 + 2], Mathf.Max(meshPart.Pos[num5 * 3 + 2], meshPart.Pos[num6 * 3 + 2])) > num3))
					{
						list2.Add(num4);
						list2.Add(num5);
						list2.Add(num6);
						if (meshPart.Slot != null && j < meshPart.Slot.Length)
						{
							list3.Add(meshPart.Slot[j]);
						}
						if (meshPart.Axis != null && j < meshPart.Axis.Length)
						{
							list4.Add(meshPart.Axis[j]);
						}
					}
				}
				if (list2.Count != 0)
				{
					list.Add(new MeshPart
					{
						Name = meshPart.Name,
						Parent = -1,
						Pivot = meshPart.Pivot,
						Pos = meshPart.Pos,
						Nrm = meshPart.Nrm,
						Tri = list2.ToArray(),
						Slot = ((meshPart.Slot != null) ? list3.ToArray() : null),
						Axis = ((meshPart.Axis != null) ? list4.ToArray() : null)
					});
				}
			}
			return value.Stage[stage] = full.Derive(full.Name + "@hull" + stage, list.ToArray());
		}
	}
}
