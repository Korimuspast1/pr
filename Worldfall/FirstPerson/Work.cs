using System;
using System.Collections.Generic;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class Work
	{
		public const int Capacity = 24;

		public const float Reach = 2.3f;

		private const float SwingSeconds = 0.55f;

		public Building Target;

		public string Verb;

		public string Needs;

		public float Progress;

		public Building Storage;

		public bool ForceSwing;

		public int BagCount;

		public bool TargetFull;

		public readonly List<KeyValuePair<string, int>> Bag = new List<KeyValuePair<string, int>>();

		public readonly List<BagGain> Gained = new List<BagGain>();

		private readonly Dictionary<Building, float> _progress = new Dictionary<Building, float>();

		private readonly Dictionary<string, int> _lastBag = new Dictionary<string, int>();

		private readonly Dictionary<string, int> _scratch = new Dictionary<string, int>();

		private readonly List<KeyValuePair<string, int>> _supplies = new List<KeyValuePair<string, int>>();

		private Actor _bagOwner;

		private float _cooldown;

		private Vector3 _yieldFrom;

		private float _yieldAt = -9f;

		public const string BareHands = "hands";

		internal void YieldFrom(Vector3 from)
		{
			_yieldFrom = from;
			_yieldAt = Time.unscaledTime;
		}

		internal void YieldFromForTest(Vector3 from)
		{
			YieldFrom(from);
		}

		public void Reset()
		{
			Target = null;
			Storage = null;
			_progress.Clear();
			_lastBag.Clear();
			Gained.Clear();
			_bagOwner = null;
		}

		public void Update(Actor host, BaseSimObject picked, Vector2 you, float dt, bool mouseFree, SceneCollector scene, CombatFeedback feedback, Action<string> toast, Func<float, float, float> groundAt, float eyeZ, float yaw, float lookUp, bool holstered)
		{
			ReadBag(host, you, eyeZ, yaw, lookUp);
			Target = null;
			Storage = null;
			Verb = null;
			Needs = null;
			Progress = 0f;
			_cooldown -= dt;
			Building building = picked as Building;
			Building building2 = Blueprints.SiteUnderAim(host, you);
			if (building2 != null && building2 != building && (building == null || building.asset == null || !building.isAlive() || (Position(building) - you).magnitude > Blueprints.DistanceTo(building2, you) + 0.5f))
			{
				building = building2;
			}
			if (((building == null || building.asset == null || !building.isAlive() || !AssetStyle.IsSolid(building)) && WallPlans.HandWork(this, host, you, mouseFree, holstered, feedback, toast)) || ((building == null || building.asset == null || !building.isAlive() || !AssetStyle.IsSolid(building)) && Earthworks.HandWork(this, host, you, mouseFree, holstered, feedback, toast, scene)) || building == null || building.asset == null || !building.isAlive())
			{
				return;
			}
			float magnitude = (Position(building) - you).magnitude;
			if (BagCount > 0 && IsStorage(host, building) && magnitude < 3f)
			{
				Storage = building;
			}
			else
			{
				if (Blueprints.SiteWork(this, host, building, you, mouseFree, holstered, scene, feedback, toast) || !Workable(building, out var verb, out var sound, out var hits) || magnitude > 2.3f)
				{
					return;
				}
				string tool = ToolInHand(host, holstered);
				if (!RightTool(building, tool))
				{
					Needs = NeedsFor(building, tool, holstered ? ToolInHand(host, holstered: false) : null);
					return;
				}
				Target = building;
				Verb = verb;
				TargetFull = NoRoomFor(host, building);
				_progress.TryGetValue(building, out Progress);
				if (!mouseFree || (!ForceSwing && !PlayInput.GetMouseButton(0)) || _cooldown > 0f)
				{
					return;
				}
				_cooldown = 0.55f;
				if (!AlwaysWorkable(building) && !HasResourcesToCollect(building))
				{
					toast("There's nothing to " + verb.ToLowerInvariant() + " here yet.");
					return;
				}
				if (TargetFull)
				{
					toast($"Your {FirstPerson.Bag.Name(host).ToLowerInvariant()} is full ({FirstPerson.Bag.Size(host)} slots). Store it in your city (E at a storehouse), drop something, or craft (Tab).");
					return;
				}
				feedback.WorkSwing((verb == "Chop") ? Swing.Chop : ((verb == "Mine") ? Swing.Mine : Swing.Gather));
				scene.Shake(building);
				float num = groundAt(Position(building).x, Position(building).y);
				scene.Chips(building, you, Mathf.Clamp(eyeZ - num - 0.25f, 0.1f, 1.2f), (building.asset.building_type == BuildingType.Building_Tree) ? 5 : 4);
				Sounds.PlayAt(sound, Position(building));
				Progress = Mathf.Min(1f, Progress + ToolBonus(tool, building) / (float)hits);
				_progress[building] = Progress;
				if (_progress.Count > 64)
				{
					_progress.Clear();
				}
				if (!(Progress < 0.999f))
				{
					_progress.Remove(building);
					float num2 = Mathf.Max(0.4f, magnitude - 0.3f);
					if (lookUp < -0.01f)
					{
						num2 = Mathf.Min(num2, Mathf.Max(0.4f, (eyeZ - num - 0.12f) / (0f - lookUp)));
					}
					_yieldFrom = new Vector3(you.x + Mathf.Cos(yaw) * num2, you.y + Mathf.Sin(yaw) * num2, eyeZ + lookUp * num2);
					_yieldAt = Time.unscaledTime;
					Finish(host, building, you, scene, toast);
				}
			}
		}

		public bool SwingInto(Actor host, Building b, Vector2 you, Vector3 at, float ground, SceneCollector scene, CombatFeedback feedback, Action<string> toast, bool holstered)
		{
			if (b == null || b.asset == null || !b.isAlive())
			{
				return false;
			}
			if (!Workable(b, out var verb, out var sound, out var hits))
			{
				return false;
			}
			string tool = ToolInHand(host, holstered);
			if (!RightTool(b, tool) || (!AlwaysWorkable(b) && !HasResourcesToCollect(b)))
			{
				return false;
			}
			if (NoRoomFor(host, b))
			{
				toast($"Your {FirstPerson.Bag.Name(host).ToLowerInvariant()} is full ({FirstPerson.Bag.Size(host)} slots). Store it in your city (E at a storehouse), drop something, or craft (Tab).");
				return false;
			}
			feedback.WorkSwing((verb == "Chop") ? Swing.Chop : ((verb == "Mine") ? Swing.Mine : Swing.Gather));
			scene.Shake(b);
			scene.Chips(b, you, Mathf.Clamp(at.z - ground, 0.1f, 1.2f), (b.asset.building_type == BuildingType.Building_Tree) ? 5 : 4);
			Sounds.PlayAt(sound, Position(b));
			_progress.TryGetValue(b, out var value);
			value = Mathf.Min(1f, value + ToolBonus(tool, b) / (float)hits);
			_progress[b] = value;
			if (_progress.Count > 64)
			{
				_progress.Clear();
			}
			if (value < 0.999f)
			{
				return true;
			}
			_progress.Remove(b);
			_yieldFrom = at;
			_yieldAt = Time.unscaledTime;
			Finish(host, b, you, scene, toast);
			return true;
		}

		public float ProgressOf(Building b)
		{
			if (b == null || !_progress.TryGetValue(b, out var value))
			{
				return 0f;
			}
			return value;
		}

		internal void SetProgressForTest(Building b, float p)
		{
			if (b != null)
			{
				_progress[b] = p;
			}
		}

		internal static Vector2 PlaceOf(Building b)
		{
			return Position(b);
		}

		private void Finish(Actor host, Building b, Vector2 you, SceneCollector scene, Action<string> toast)
		{
			BuildingAsset asset = b.asset;
			bool flag = asset.building_type == BuildingType.Building_Tree;
			if (flag && Burnt(b))
			{
				try
				{
					b.startDestroyBuilding();
				}
				catch (Exception ex)
				{
					Log.Error("burnt tree chop failed for " + asset.id + ": " + ex.Message);
				}
				FirstPerson.Bag.Put(host, "wood", 1, spill: true, toast);
				try
				{
					host.addExperience(1);
					return;
				}
				catch
				{
					return;
				}
			}
			if (flag)
			{
				scene.Fell(b, you);
			}
			else if (asset.building_type == BuildingType.Building_Mineral)
			{
				scene.MinedOut(b, you);
			}
			try
			{
				b.extractResources(host);
			}
			catch (Exception ex2)
			{
				Log.Error("extractResources failed for " + asset.id + ": " + ex2.Message);
			}
			if (asset.resources_given == null)
			{
				return;
			}
			bool flag2 = false;
			foreach (ResourceContainer item in asset.resources_given)
			{
				int num = item.amount;
				if (asset.building_type == BuildingType.Building_Mineral && Safe(() => host.hasTrait("miner")) && UnityEngine.Random.value < 0.5f)
				{
					num++;
				}
				if (num > 0 && FirstPerson.Bag.Put(host, item.id, num, spill: true, toast) < num)
				{
					flag2 = true;
				}
			}
			try
			{
				host.addExperience((!flag) ? 1 : 2);
			}
			catch
			{
			}
			if (!flag2 && FirstPerson.Bag.Free(host) == 0)
			{
				toast("Your " + FirstPerson.Bag.Name(host).ToLowerInvariant() + " has no free slot now.");
			}
		}

		private static bool NoRoomFor(Actor host, Building b)
		{
			try
			{
				if (b?.asset?.resources_given == null)
				{
					return false;
				}
				foreach (ResourceContainer item in b.asset.resources_given)
				{
					if (FirstPerson.Bag.RoomFor(host, item.id) > 0)
					{
						return false;
					}
				}
				return b.asset.resources_given.Count > 0;
			}
			catch
			{
				return false;
			}
		}

		public void Store(Actor host, Action<string> toast)
		{
			int bagCount = BagCount;
			try
			{
				List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>();
				foreach (Quest item in Quests.Log)
				{
					if (item.Kind == QuestKind.Supply && item.Stage < 2 && !string.IsNullOrEmpty(item.Thing) && !Supplies.Is(item.Thing))
					{
						int num = Mathf.Min(item.Need, Civil.BagCount(host, item.Thing));
						if (num > 0 && Civil.TakeFromBag(host, item.Thing, num))
						{
							list.Add(new KeyValuePair<string, int>(item.Thing, num));
						}
					}
				}
				host.giveInventoryResourcesToCity();
				int num2 = 0;
				foreach (KeyValuePair<string, int> item2 in list)
				{
					host.addToInventory(item2.Key, item2.Value);
					num2 += item2.Value;
				}
				bagCount -= num2;
				toast("You stored " + bagCount + " resources in " + host.city.name + "." + ((num2 > 0) ? (" Kept " + num2 + " for your task.") : ""));
				Sounds.PlayAt("event:/SFX/CIVILIZATIONS/CollectFruits", host.current_position);
			}
			catch (Exception ex)
			{
				Log.Error("storing failed: " + ex.Message);
			}
		}

		private void ReadBag(Actor host, Vector2 you, float eyeZ, float yaw, float lookUp)
		{
			float unscaledTime = Time.unscaledTime;
			for (int num = Gained.Count - 1; num >= 0; num--)
			{
				if (unscaledTime >= Gained[num].Gone + 0.5f || unscaledTime < Gained[num].Born - 1f)
				{
					Gained.RemoveAt(num);
				}
			}
			Bag.Clear();
			BagCount = 0;
			_scratch.Clear();
			Dictionary<string, ResourceContainer> dictionary = null;
			try
			{
				dictionary = host.inventory?.getResources();
			}
			catch
			{
			}
			if (dictionary != null)
			{
				foreach (KeyValuePair<string, ResourceContainer> item in dictionary)
				{
					if (item.Value.amount > 0)
					{
						Bag.Add(new KeyValuePair<string, int>(item.Key, item.Value.amount));
						BagCount += item.Value.amount;
						_scratch[item.Key] = item.Value.amount;
					}
				}
			}
			Bag.Sort((KeyValuePair<string, int> a, KeyValuePair<string, int> b) => string.CompareOrdinal(a.Key, b.Key));
			_supplies.Clear();
			Supplies.AddTo(host, _supplies);
			foreach (KeyValuePair<string, int> supply in _supplies)
			{
				_scratch[supply.Key] = supply.Value;
			}
			if (_bagOwner == host)
			{
				bool flag = unscaledTime - _yieldAt < 1f;
				Vector3 vector = (flag ? _yieldFrom : new Vector3(you.x + Mathf.Cos(yaw) * 1.3f, you.y + Mathf.Sin(yaw) * 1.3f, eyeZ + (lookUp - 0.2f) * 1.3f));
				int count = Gained.Count;
				foreach (KeyValuePair<string, int> item2 in Bag)
				{
					_lastBag.TryGetValue(item2.Key, out var value);
					if (item2.Value > value)
					{
						Gained.Add(new BagGain
						{
							Id = item2.Key,
							Amount = item2.Value - value,
							From = vector,
							Born = unscaledTime,
							Order = Gained.Count - count
						});
						if (flag)
						{
							Quests.OnGathered(item2.Key, item2.Value - value);
						}
					}
				}
				foreach (KeyValuePair<string, int> supply2 in _supplies)
				{
					_lastBag.TryGetValue(supply2.Key, out var value2);
					if (supply2.Value > value2)
					{
						Gained.Add(new BagGain
						{
							Id = supply2.Key,
							Amount = supply2.Value - value2,
							From = vector,
							Born = unscaledTime,
							Order = Gained.Count - count
						});
					}
				}
				for (int num2 = count; num2 < Gained.Count; num2++)
				{
					Gained[num2].Of = Gained.Count - count;
				}
				if (Gained.Count > count && flag)
				{
					_yieldAt = -9f;
				}
				if (Gained.Count > 24)
				{
					Gained.RemoveRange(0, Gained.Count - 24);
				}
			}
			else
			{
				Gained.Clear();
			}
			_bagOwner = host;
			_lastBag.Clear();
			foreach (KeyValuePair<string, int> item3 in _scratch)
			{
				_lastBag[item3.Key] = item3.Value;
			}
		}

		public static bool Workable(Building b, out string verb, out string sound, out int hits)
		{
			verb = null;
			sound = null;
			hits = 0;
			BuildingAsset asset = b.asset;
			if (asset.resources_given == null || asset.resources_given.Count == 0 || b.chopped)
			{
				return false;
			}
			if (asset.building_type == BuildingType.Building_Tree && Burnt(b))
			{
				verb = "Chop";
				sound = "event:/SFX/CIVILIZATIONS/ChopTree";
				hits = 3;
				return true;
			}
			switch (asset.building_type)
			{
			case BuildingType.Building_Tree:
				verb = "Chop";
				sound = "event:/SFX/CIVILIZATIONS/ChopTree";
				hits = 6;
				return true;
			case BuildingType.Building_Mineral:
				verb = "Mine";
				sound = "event:/SFX/CIVILIZATIONS/MiningMineral";
				hits = 8;
				return true;
			case BuildingType.Building_Fruits:
				verb = "Pick";
				sound = "event:/SFX/CIVILIZATIONS/CollectFruits";
				hits = 2;
				return true;
			case BuildingType.Building_Plant:
				verb = "Gather";
				sound = "event:/SFX/CIVILIZATIONS/CollectHerbs";
				hits = 2;
				return true;
			case BuildingType.Building_Wheat:
				verb = "Harvest";
				sound = "event:/SFX/CIVILIZATIONS/HarvestCrops";
				hits = 2;
				return true;
			case BuildingType.Building_Hives:
				verb = "Collect honey from";
				sound = "event:/SFX/CIVILIZATIONS/CollectFruits";
				hits = 3;
				return true;
			default:
				return false;
			}
		}

		private static bool Burnt(Building b)
		{
			try
			{
				return b.isRuin();
			}
			catch
			{
				return false;
			}
		}

		private static bool AlwaysWorkable(Building b)
		{
			BuildingType building_type = b.asset.building_type;
			if (building_type != BuildingType.Building_Tree && building_type != BuildingType.Building_Mineral)
			{
				return !b.asset.gatherable;
			}
			return true;
		}

		private static bool HasResourcesToCollect(Building b)
		{
			try
			{
				return b.hasResourcesToCollect();
			}
			catch
			{
				return true;
			}
		}

		public static string ToolInHand(Actor host, bool holstered)
		{
			string text = null;
			if (!holstered)
			{
				try
				{
					text = ((host.getWeapon() == null) ? null : host.getWeaponAsset()?.id);
				}
				catch
				{
				}
			}
			if (!string.IsNullOrEmpty(text))
			{
				return text;
			}
			if (!holstered && Earthworks.ToolOf(host) == "tw_item_pickaxe")
			{
				return "tw_item_pickaxe";
			}
			if (!People.IsHumanoid(host))
			{
				return null;
			}
			return "hands";
		}

		private static bool IsAxe(string tool)
		{
			if (tool != null && tool.Contains("axe"))
			{
				return !tool.Contains("pickaxe");
			}
			return false;
		}

		private static bool IsPickaxe(string tool)
		{
			return tool?.Contains("pickaxe") ?? false;
		}

		public static bool RightTool(Building b, string tool)
		{
			switch (b.asset.building_type)
			{
			case BuildingType.Building_Tree:
				if (!(tool == "hands"))
				{
					return IsAxe(tool);
				}
				return true;
			case BuildingType.Building_Mineral:
				if (!IsPickaxe(tool))
				{
					return IsAxe(tool);
				}
				return true;
			default:
				return true;
			}
		}

		private static string NeedsFor(Building b, string tool, string putAway)
		{
			if (tool == null)
			{
				return null;
			}
			if (b.asset.building_type == BuildingType.Building_Tree)
			{
				return "Chop with an axe or bare hands|Hold Q:Put it away";
			}
			if (!IsAxe(putAway) && !IsPickaxe(putAway))
			{
				return "Mine with a pickaxe or an axe";
			}
			return "Mine with a pickaxe or an axe|Hold Q:Take it up";
		}

		private static float ToolBonus(string tool, Building b)
		{
			switch (b.asset.building_type)
			{
			case BuildingType.Building_Tree:
				if (!IsAxe(tool))
				{
					return 1f;
				}
				return 2f;
			case BuildingType.Building_Mineral:
				if (!IsPickaxe(tool))
				{
					return 1.25f;
				}
				return 2f;
			default:
				if (tool != null && !(tool == "hands"))
				{
					return 1.25f;
				}
				return 1f;
			}
		}

		private static bool IsStorage(Actor host, Building b)
		{
			try
			{
				return host.city != null && b.city == host.city && (b.asset.storage || b.asset.is_stockpile) && b.isUsable();
			}
			catch
			{
				return false;
			}
		}

		public static string Amount(string id, int n)
		{
			Supplies.Kind kind = Supplies.Of(id);
			if (kind == null)
			{
				return n + " " + ResourceName(id).ToLowerInvariant();
			}
			return Supplies.Counted(kind, n);
		}

		public static string ResourceName(string id)
		{
			string text = Supplies.Name(id);
			if (text != null)
			{
				return text;
			}
			try
			{
				string text2 = AssetManager.resources.get(id)?.getTranslatedName();
				if (!string.IsNullOrEmpty(text2) && !text2.StartsWith("$"))
				{
					return text2;
				}
			}
			catch
			{
			}
			return id.Replace('_', ' ');
		}

		private static Vector2 Position(Building b)
		{
			return new Vector2(b.current_tile.posV3.x, b.current_tile.posV3.y);
		}

		private static bool Safe(Func<bool> f)
		{
			try
			{
				return f();
			}
			catch
			{
				return false;
			}
		}
	}
}
