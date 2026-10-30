using System;
using System.Collections.Generic;
using FirstPerson.Towns;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal static class FamilyFollow
	{
		public enum Orders
		{
			Free,
			Follow,
			Wait
		}

		public const string WalkTask = "fp_family_follow";

		public const string RunTask = "fp_family_run";

		public const string InTask = "fp_family_in";

		private const float Near = 2.6f;

		private const float RunFrom = 7f;

		public const float Together = 10f;

		public const float HandReach = 6f;

		private const int MaxFamily = 12;

		private static readonly List<Actor> _family = new List<Actor>();

		private static readonly Dictionary<Actor, long> _ids = new Dictionary<Actor, long>();

		private static readonly List<Actor> _scratch = new List<Actor>();

		private static readonly Dictionary<Actor, Orders> _orders = new Dictionary<Actor, Orders>();

		private static readonly Dictionary<Actor, WorldTile> _waitAt = new Dictionary<Actor, WorldTile>();

		private static readonly HashSet<Actor> _withYou = new HashSet<Actor>();

		private static readonly Dictionary<Actor, float> _warnedAt = new Dictionary<Actor, float>();

		private static readonly HashSet<Actor> _hungryTold = new HashSet<Actor>();

		private static readonly Dictionary<Actor, string> _relation = new Dictionary<Actor, string>();

		private static Actor _for;

		private static City _cityWas;

		private static bool _standing;

		private static bool _registered;

		private static bool _stranded;

		private static bool _seenSwitch;

		private static float _listAt = -10f;

		private static float _orderAt = -10f;

		private const string WaitKey = "fp_family_wait";

		private static bool _seenOnce;

		internal static readonly List<string> Said = new List<string>();

		public static List<Actor> Members => _family;

		private static bool Other(Actor a)
		{
			try
			{
				long value;
				return a != null && _ids.TryGetValue(a, out value) && (a.data == null || a.data.id != value);
			}
			catch
			{
				return true;
			}
		}

		private static bool Recycled()
		{
			for (int i = 0; i < _family.Count; i++)
			{
				if (Other(_family[i]))
				{
					return true;
				}
			}
			return false;
		}

		public static bool IsFamily(Actor a)
		{
			if (a != null && _family.Contains(a))
			{
				return OrderOf(a) != Orders.Free;
			}
			return false;
		}

		public static Orders OrderOf(Actor a)
		{
			if (a == null || !_orders.TryGetValue(a, out var value))
			{
				if (!_standing)
				{
					return Orders.Free;
				}
				return Orders.Follow;
			}
			return value;
		}

		public static WorldTile WaitPlace(Actor a)
		{
			if (a == null || !_waitAt.TryGetValue(a, out var value))
			{
				return null;
			}
			return value;
		}

		public static void Update(Actor you, bool standing, Action<string> toast)
		{
			bool flag = false;
			try
			{
				flag = you?.isAlive() ?? false;
			}
			catch
			{
			}
			if (!flag)
			{
				if (_for != null)
				{
					ReleaseAll();
				}
				_for = null;
				return;
			}
			float unscaledTime = Time.unscaledTime;
			if (you != _for)
			{
				ReleaseAll();
				_for = you;
				_cityWas = CityOf(you);
				_stranded = false;
				_standing = standing;
				_seenSwitch = true;
				Refresh(you, toast);
				_listAt = unscaledTime;
				Log.Info("family: " + _family.Count + " of " + WorldBoxMod.Describe(you) + ((_family.Count > 0) ? (": " + Names(_family)) : ""));
			}
			else if (unscaledTime - _listAt > 2f || Recycled())
			{
				_listAt = unscaledTime;
				Refresh(you, toast);
			}
			if (!_seenSwitch || standing != _standing)
			{
				_standing = standing;
				_seenSwitch = true;
				if (_seenOnce)
				{
					foreach (Actor item in _family)
					{
						KeepWait(item, null);
					}
				}
				_seenOnce = true;
				_orders.Clear();
				_waitAt.Clear();
				_withYou.Clear();
				Refresh(you, toast);
				_listAt = unscaledTime;
				foreach (Actor item2 in _family)
				{
					Release(item2);
				}
				if (standing)
				{
					Say(toast, (_family.Count == 0) ? "You have no family to follow you yet. A lover or children of yours will, once you have them." : ("Your family follows you: " + Names(_family) + "."));
				}
				_orderAt = -10f;
			}
			City city = CityOf(you);
			if (city != _cityWas)
			{
				City cityWas = _cityWas;
				_cityWas = city;
				Move(you, cityWas, city, toast);
			}
			if (_withYou.Count > 0)
			{
				foreach (Actor item3 in _family)
				{
					try
					{
						if (item3 != null && item3.isAlive() && _withYou.Contains(item3) && OrderOf(item3) != Orders.Free && CityOf(item3) != city)
						{
							KeepWithYou(item3, city);
						}
					}
					catch
					{
					}
				}
			}
			if (!(unscaledTime - _orderAt < 0.6f) && !Config.paused)
			{
				_orderAt = unscaledTime;
				Order(you, toast);
				Watch(you, toast);
			}
		}

		public static int Give(Actor you, List<Actor> who, Orders order, WorldTile at)
		{
			int num = 0;
			foreach (Actor item in (who != null && who.Count > 0) ? who : _family)
			{
				if (item != null && item.isAlive() && _family.Contains(item))
				{
					_orders[item] = order;
					if (order == Orders.Wait && at != null)
					{
						_waitAt[item] = at;
					}
					else
					{
						_waitAt.Remove(item);
					}
					KeepWait(item, (order == Orders.Wait) ? at : null);
					if (order == Orders.Free)
					{
						Release(item);
						_withYou.Remove(item);
					}
					num++;
				}
			}
			_orderAt = -10f;
			Log.Info($"family: {order} for {num}" + ((at != null) ? $" at {at.x},{at.y}" : ""));
			return num;
		}

		public static string ShareFood(Actor you, List<Actor> who)
		{
			List<Actor> list = new List<Actor>();
			int num = 0;
			foreach (Actor item in (who != null && who.Count > 0) ? who : _family)
			{
				if (item != null && item.isAlive() && Eats(item) && !(item.getNutritionRatio() >= 0.95f))
				{
					if ((item.current_position - you.current_position).magnitude > 6f)
					{
						num++;
					}
					else
					{
						list.Add(item);
					}
				}
			}
			if (list.Count == 0)
			{
				if (num <= 0)
				{
					return "Nobody needs feeding.";
				}
				return "Whoever's hungry is too far off to hand food to. Bring them close (Follow me).";
			}
			list.Sort((Actor x, Actor y) => x.getNutritionRatio().CompareTo(y.getNutritionRatio()));
			int num2 = 0;
			List<string> list2 = new List<string>();
			foreach (Actor item2 in list)
			{
				ResourceAsset resourceAsset = FoodFor(you, item2);
				if (resourceAsset == null)
				{
					break;
				}
				try
				{
					you.takeFromInventory(resourceAsset.id, 1);
					item2.consumeFoodResource(resourceAsset);
					num2++;
					list2.Add(Name(item2));
					_hungryTold.Remove(item2);
					Log.Info("family: fed " + WorldBoxMod.Describe(item2) + " " + resourceAsset.id);
				}
				catch (Exception ex)
				{
					Log.Error("family: feeding: " + ex.Message);
				}
			}
			if (num2 > 0)
			{
				Sounds.PlayAt("event:/SFX/CIVILIZATIONS/CollectFruits", you.current_position);
			}
			if (num2 == 0)
			{
				return "Nothing in your bag they can eat. Pick berries or hunt (hold the left mouse button on bushes, or on game).";
			}
			string text = ((num2 < list.Count) ? " There wasn't enough for everyone." : "");
			return "You shared food with " + string.Join(", ", list2.ToArray()) + "." + text + ((num > 0) ? " Some are too far off to reach." : "");
		}

		public static int FoodInBag(Actor you)
		{
			int num = 0;
			try
			{
				foreach (KeyValuePair<string, ResourceContainer> resource in you.inventory.getResources())
				{
					ResourceAsset resourceAsset = AssetManager.resources.get(resource.Key);
					if (resourceAsset != null && resourceAsset.food && resourceAsset.restore_nutrition > 0)
					{
						num += resource.Value.amount;
					}
				}
			}
			catch
			{
			}
			return num;
		}

		private static ResourceAsset FoodFor(Actor you, Actor a)
		{
			ResourceAsset resourceAsset = null;
			try
			{
				HashSet<string> hashSet = null;
				try
				{
					hashSet = a.subspecies?.getAllowedFoodByDiet();
				}
				catch
				{
				}
				string favorite_food = a.data.favorite_food;
				foreach (KeyValuePair<string, ResourceContainer> resource in you.inventory.getResources())
				{
					if (resource.Value.amount <= 0)
					{
						continue;
					}
					ResourceAsset resourceAsset2 = AssetManager.resources.get(resource.Key);
					if (resourceAsset2 != null && resourceAsset2.food && resourceAsset2.restore_nutrition > 0 && (hashSet == null || hashSet.Contains(resourceAsset2.id)))
					{
						if (resourceAsset2.id == favorite_food)
						{
							return resourceAsset2;
						}
						if (resourceAsset == null || resourceAsset2.tastiness > resourceAsset.tastiness)
						{
							resourceAsset = resourceAsset2;
						}
					}
				}
			}
			catch
			{
			}
			return resourceAsset;
		}

		public static bool Eats(Actor a)
		{
			try
			{
				return a.getMaxNutrition() > 0;
			}
			catch
			{
				return false;
			}
		}

		public static bool CanSettleHere(Actor you)
		{
			try
			{
				if (you.city != null || !you.asset.civ || you.isBaby())
				{
					return false;
				}
				return you.canBuildNewCity() && World.world.cities.canStartNewCityCivilizationHere(you);
			}
			catch
			{
				return false;
			}
		}

		public static City SettleHere(Actor you, Action<string> toast, WorldTile at = null)
		{
			if (at == null && !CanSettleHere(you))
			{
				Say(toast, "You can't found a village here: find open land away from any town (and not in the water).");
				return null;
			}
			try
			{
				if (!Founding.Ready(you, toast))
				{
					return null;
				}
				foreach (Actor item in _family)
				{
					if (OrderOf(item) != Orders.Free)
					{
						_withYou.Add(item);
					}
				}
				if (at != null)
				{
					if (Village.Found(you, at, out var why) == null)
					{
						Say(toast, why ?? "The land won't take a village here.");
						return null;
					}
				}
				else if (!you.buildCityAndStartCivilization())
				{
					Say(toast, "The land won't take a village here.");
					return null;
				}
				City city = CityOf(you);
				Founding.Founded(you, city, charter: false, toast);
				Log.Info("family: " + WorldBoxMod.Describe(you) + " founded " + city?.name + " of " + city?.kingdom?.name);
				if (city != null && at == null)
				{
					Say(toast, "You founded " + city.name + ", the first town of " + city.kingdom?.name + ". Build it up!");
				}
				return city;
			}
			catch (Exception ex)
			{
				Log.Error("family: settling: " + ex);
				return null;
			}
		}

		private static void Refresh(Actor you, Action<string> toast)
		{
			_scratch.Clear();
			try
			{
				Actor lover = you.lover;
				if (lover != null && lover != you && lover.isAlive())
				{
					_scratch.Add(lover);
				}
				long id = you.data.id;
				long num = ((lover != null && lover.isAlive()) ? lover.data.id : (-1));
				List<Actor> simpleList = World.world.units.getSimpleList();
				for (int i = 0; i < simpleList.Count; i++)
				{
					if (_scratch.Count >= 12)
					{
						break;
					}
					Actor actor = simpleList[i];
					if (actor != null && actor != you && actor != lover && actor.isAlive())
					{
						long parent_id_ = actor.data.parent_id_1;
						long parent_id_2 = actor.data.parent_id_2;
						bool num2 = parent_id_ == id || parent_id_2 == id;
						bool flag = num >= 0 && (parent_id_ == num || parent_id_2 == num) && !actor.isAdult();
						if ((num2 || flag) && (actor.asset == you.asset || (lover != null && actor.asset == lover.asset)) && (!actor.isAdult() || !actor.hasLover()))
						{
							_scratch.Add(actor);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("family: " + ex.Message);
			}
			foreach (Actor item in _family)
			{
				bool flag2 = Other(item);
				if (!_scratch.Contains(item) || flag2)
				{
					bool flag3 = flag2;
					try
					{
						flag3 |= item == null || !item.isAlive();
					}
					catch
					{
						flag3 = true;
					}
					if (flag3 && item != null && you == _for)
					{
						Say(toast, Name(item) + ", your " + (_relation.TryGetValue(item, out var value) ? value : "kin") + ", has died.");
					}
					_relation.Remove(item);
					Release(item);
					_withYou.Remove(item);
					_orders.Remove(item);
					_waitAt.Remove(item);
					_warnedAt.Remove(item);
					_hungryTold.Remove(item);
				}
			}
			_family.Clear();
			_family.AddRange(_scratch);
			_ids.Clear();
			foreach (Actor item2 in _family)
			{
				_relation[item2] = Relation(you, item2);
				try
				{
					_ids[item2] = item2.data.id;
				}
				catch
				{
				}
				if (!_orders.ContainsKey(item2))
				{
					KeptWait(item2);
				}
			}
		}

		private static void KeepWait(Actor a, WorldTile at)
		{
			try
			{
				if (at != null)
				{
					a.data.set("fp_family_wait", at.x + "," + at.y);
				}
				else
				{
					a.data.removeString("fp_family_wait");
				}
			}
			catch
			{
			}
		}

		private static void KeptWait(Actor a)
		{
			string pResult = null;
			try
			{
				a.data.get("fp_family_wait", out pResult, null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(pResult))
			{
				return;
			}
			string[] array = pResult.Split(',');
			WorldTile worldTile = null;
			try
			{
				if (array.Length == 2 && int.TryParse(array[0], out var result) && int.TryParse(array[1], out var result2))
				{
					worldTile = World.world.GetTile(result, result2);
				}
			}
			catch
			{
			}
			if (worldTile == null)
			{
				KeepWait(a, null);
				return;
			}
			_orders[a] = Orders.Wait;
			_waitAt[a] = worldTile;
		}

		public static string Relation(Actor you, Actor a)
		{
			try
			{
				if (you.lover == a)
				{
					return "lover";
				}
				bool flag = a.data.sex == ActorSex.Female;
				return ((a.data.parent_id_1 == you.data.id || a.data.parent_id_2 == you.data.id) ? "" : "step") + (flag ? "daughter" : "son");
			}
			catch
			{
				return "family";
			}
		}

		public static string Name(Actor a)
		{
			try
			{
				string name = a.getName();
				return string.IsNullOrEmpty(name) ? "?" : name;
			}
			catch
			{
				return "?";
			}
		}

		private static string Names(List<Actor> list)
		{
			List<string> list2 = new List<string>();
			foreach (Actor item in list)
			{
				if (list2.Count == 4)
				{
					list2.Add("and " + (list.Count - 4) + " more");
					break;
				}
				list2.Add(Name(item));
			}
			return string.Join(", ", list2.ToArray());
		}

		private static City CityOf(Actor a)
		{
			try
			{
				return (a.city != null && a.city.isAlive()) ? a.city : null;
			}
			catch
			{
				return null;
			}
		}

		private static void Move(Actor you, City from, City to, Action<string> toast)
		{
			int num = 0;
			List<string> list = new List<string>();
			foreach (Actor item in _family)
			{
				try
				{
					if (item == null || !item.isAlive() || CityOf(item) == to || OrderOf(item) == Orders.Free)
					{
						continue;
					}
					string why;
					if (item.isKing() || item.isCityLeader())
					{
						string text = ((item.isKing() && item.kingdom != null) ? item.kingdom.name : ((item.city != null) ? item.city.name : "home"));
						list.Add(Name(item) + " stays to rule " + text);
					}
					else if (!JoinYou(item, to, out why))
					{
						if (why != null)
						{
							list.Add(why);
						}
					}
					else
					{
						num++;
						_withYou.Add(item);
					}
				}
				catch (Exception ex)
				{
					Log.Error("family: moving " + WorldBoxMod.Describe(item) + ": " + ex.Message);
				}
			}
			string text2 = ((from != null) ? from.name : "your old home");
			string text3 = ((num == 0) ? null : ((to != null) ? ((num == 1) ? (Mover() + " came to " + to.name + " with you.") : ("Your family came to " + to.name + " with you.")) : ((num == 1) ? (Mover() + " left " + text2 + " with you.") : ("Your family left " + text2 + " with you."))));
			string text4 = ((list.Count > 0) ? (" " + string.Join("; ", list.ToArray()) + ".") : "");
			if (text3 != null || text4.Length > 0)
			{
				Say(toast, (text3 + text4).Trim());
			}
			Log.Info(string.Format("family: {0} moved {1} -> {2}{3}", num, (from != null) ? from.name : "-", (to != null) ? to.name : "no town", text4));
			_orderAt = -10f;
			string Mover()
			{
				foreach (Actor item2 in _family)
				{
					if (item2 != null && item2.isAlive() && CityOf(item2) == to && OrderOf(item2) != Orders.Free)
					{
						return Name(item2);
					}
				}
				return "Your family";
			}
		}

		private static bool JoinYou(Actor a, City to, out string why)
		{
			why = null;
			if (to == null)
			{
				if (CityOf(a) != null || a.isKingdomCiv())
				{
					return Belong.Leave(a);
				}
				return true;
			}
			string species = to.getSpecies();
			if (!string.IsNullOrEmpty(species) && a.asset != null && species != a.asset.id)
			{
				why = Name(a) + " can't live among your new people";
				return false;
			}
			return Belong.Join(a, to);
		}

		private static void KeepWithYou(Actor a, City yours)
		{
			if (a.isKing() || a.isCityLeader())
			{
				_withYou.Remove(a);
				return;
			}
			string arg = CityOf(a)?.name ?? "no town";
			bool flag;
			try
			{
				if (yours == null)
				{
					a.removeFromPreviousFaction();
					try
					{
						a.clearHomeBuilding();
					}
					catch
					{
					}
					if (a.isKingdomCiv())
					{
						a.setDefaultKingdom();
					}
					flag = a.city == null;
				}
				else
				{
					if (a.city != null)
					{
						a.removeFromPreviousFaction();
					}
					try
					{
						a.clearHomeBuilding();
					}
					catch
					{
					}
					a.joinCity(yours);
					flag = a.city == yours;
				}
			}
			catch (Exception ex)
			{
				Log.Error("family: keeping " + WorldBoxMod.Describe(a) + " with you: " + ex.Message);
				flag = false;
			}
			_orderAt = -10f;
			if (!flag)
			{
				_withYou.Remove(a);
			}
			Log.Info($"family: {WorldBoxMod.Describe(a)} had gone to {arg}; back with you: {flag}");
		}

		private static void Order(Actor you, Action<string> toast)
		{
			if (_family.Count == 0)
			{
				return;
			}
			RegisterTasks();
			WorldTile worldTile = null;
			try
			{
				worldTile = you.current_tile;
			}
			catch
			{
			}
			bool flag = worldTile != null && Royal.Standable(worldTile) && !you.is_inside_boat;
			Building building = HouseOf(you);
			Vector2 current_position = you.current_position;
			float yaw = Facing(you);
			int num = 0;
			bool flag2 = false;
			CityOf(you);
			List<Actor> list = null;
			try
			{
				if (Service.CommissionOf(you) != null)
				{
					list = Service.SettlersOf(you);
				}
			}
			catch
			{
			}
			foreach (Actor item in _family)
			{
				try
				{
					if (item == null || !item.isAlive())
					{
						continue;
					}
					Orders orders = OrderOf(item);
					int num2;
					switch (orders)
					{
					case Orders.Free:
						Release(item);
						goto end_IL_008d;
					default:
						num2 = 0;
						break;
					case Orders.Follow:
						num2 = num++;
						break;
					}
					int slot = num2;
					if (item.ai == null || item.isImmovable())
					{
						continue;
					}
					if (item.has_attack_target && (item.attack_target == null || !item.attack_target.isAlive()))
					{
						item.clearAttackTarget();
					}
					if (item.has_attack_target || item.isFighting() || Royal.IsHolding(item) || Royal.IsMarchingTo(item, out var _) || (list != null && list.Contains(item)) || item.is_inside_boat || BoatHelm.Steers(item))
					{
						continue;
					}
					if (Eats(item) && (item.isStarving() || (item.isHungry() && TownFeeds(item))))
					{
						Release(item);
						continue;
					}
					WorldTile current_tile = item.current_tile;
					if (current_tile == null)
					{
						continue;
					}
					if (orders == Orders.Wait)
					{
						Hold(item, current_tile, WaitPlace(item));
						continue;
					}
					if (!flag || !current_tile.isSameIsland(worldTile))
					{
						flag2 = true;
						Release(item);
						continue;
					}
					float magnitude = (item.current_position - current_position).magnitude;
					if (!Asleep(item))
					{
						goto IL_0210;
					}
					if (magnitude <= 7f)
					{
						continue;
					}
					item.stopSleeping();
					goto IL_0210;
					IL_0210:
					if (building != null)
					{
						if (!item.is_inside_building || item.inside_building != building)
						{
							if (item.is_inside_building)
							{
								item.exitBuilding();
							}
							GoIn(item, building);
						}
						continue;
					}
					if (item.is_inside_building)
					{
						item.exitBuilding();
					}
					if (magnitude <= 2.6f)
					{
						if (!OnOurTask(item))
						{
							Send(item, "fp_family_follow", current_tile);
						}
					}
					else
					{
						GoTo(item, (magnitude > 7f) ? "fp_family_run" : "fp_family_follow", Spot(worldTile, current_position, yaw, slot));
					}
					end_IL_008d:;
				}
				catch (Exception ex)
				{
					Log.Error("family: " + ex.Message);
				}
			}
			if (flag2 && !_stranded)
			{
				Say(toast, flag ? "Your family can't get to you from where they are; they'll wait." : "Your family can't follow you here; they'll wait for you.");
			}
			_stranded = flag2;
		}

		private static void Hold(Actor a, WorldTile theirs, WorldTile place)
		{
			if (place == null)
			{
				_waitAt[a] = (place = theirs);
			}
			if (!theirs.isSameIsland(place))
			{
				Release(a);
				return;
			}
			float num = Mathf.Sqrt((theirs.x - place.x) * (theirs.x - place.x) + (theirs.y - place.y) * (theirs.y - place.y));
			if (Asleep(a))
			{
				if (num <= 7f)
				{
					return;
				}
				a.stopSleeping();
			}
			if (a.is_inside_building && num > 3f)
			{
				a.exitBuilding();
			}
			if (num <= 2.5f)
			{
				if (!OnOurTask(a))
				{
					Send(a, "fp_family_follow", theirs);
				}
			}
			else
			{
				GoTo(a, (num > 21f) ? "fp_family_run" : "fp_family_follow", place);
			}
		}

		private static Building HouseOf(Actor you)
		{
			try
			{
				if (!you.is_inside_building || you.is_inside_boat)
				{
					return null;
				}
				Building inside_building = you.inside_building;
				return HouseInterior.IsEnterable(inside_building) ? inside_building : null;
			}
			catch
			{
				return null;
			}
		}

		private static void GoIn(Actor a, Building house)
		{
			if (((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task == null || !(((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id == "fp_family_in") || a.beh_building_target != house)
			{
				WorldTile current_tile = house.current_tile;
				if (current_tile != null)
				{
					a.setTask("fp_family_in");
					a.beh_building_target = house;
					a.beh_tile_target = current_tile;
				}
			}
		}

		private static void GoTo(Actor a, string task, WorldTile spot)
		{
			if (((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task == null || !(((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id == task) || a.beh_tile_target == null || Mathf.Abs(a.beh_tile_target.x - spot.x) + Mathf.Abs(a.beh_tile_target.y - spot.y) > 2)
			{
				Send(a, task, spot);
			}
		}

		private static void Watch(Actor you, Action<string> toast)
		{
			float unscaledTime = Time.unscaledTime;
			foreach (Actor item in _family)
			{
				try
				{
					if (item != null && item.isAlive())
					{
						if (InDanger(item, out var by) && (!_warnedAt.TryGetValue(item, out var value) || unscaledTime - value > 20f))
						{
							_warnedAt[item] = unscaledTime;
							string text = Service.Heading(you.current_position, item.current_position);
							Say(toast, "Your " + Relation(you, item) + " " + Name(item) + " is under attack" + ((by != null) ? (" by " + WorldBoxMod.Describe(by)) : "") + ((text == "here") ? "!" : (", " + text + "!")));
						}
						if (!Eats(item) || !item.isHungry())
						{
							_hungryTold.Remove(item);
						}
						else if (!TownFeeds(item) && !_hungryTold.Contains(item))
						{
							_hungryTold.Add(item);
							City city = CityOf(item);
							string text2 = ((city != null) ? (" " + city.name + "'s stores have nothing they eat.") : "");
							Say(toast, "Your " + Relation(you, item) + " " + Name(item) + " is hungry." + text2 + " " + ((FoodInBag(you) > 0) ? "Share food from your bag on the Family tab of your map (M)." : ((city != null) ? "Gather berries or hunt to feed them." : "Gather berries or hunt to feed them, or settle in a town.")));
						}
					}
				}
				catch
				{
				}
			}
		}

		private static bool TownFeeds(Actor a)
		{
			try
			{
				return CityOf(a)?.hasSuitableFood(a.subspecies) ?? false;
			}
			catch
			{
				return false;
			}
		}

		private static void Say(Action<string> toast, string text)
		{
			Said.Add(text);
			if (Said.Count > 50)
			{
				Said.RemoveAt(0);
			}
			Log.Info("family says: " + text);
			toast?.Invoke(text);
		}

		public static bool InDanger(Actor a, out Actor by)
		{
			by = null;
			try
			{
				if (a.attackedBy is Actor actor && actor.isAlive() && actor.has_attack_target && actor.attack_target == a)
				{
					by = actor;
					return true;
				}
				if (a.has_attack_target && a.attack_target is Actor actor2 && actor2.isAlive() && actor2.has_attack_target && actor2.attack_target == a)
				{
					by = actor2;
					return true;
				}
			}
			catch
			{
			}
			return false;
		}

		private static float Facing(Actor you)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			if (instance != null && instance.IsFirstPerson && instance.Host == you)
			{
				return instance.ViewYaw;
			}
			try
			{
				WorldTile tile_target = you.tile_target;
				if (tile_target != null)
				{
					return Mathf.Atan2((float)tile_target.y + 0.5f - you.current_position.y, (float)tile_target.x + 0.5f - you.current_position.x);
				}
			}
			catch
			{
			}
			return 0f;
		}

		private static WorldTile Spot(WorldTile at, Vector2 me, float yaw, int slot)
		{
			float[] array = new float[12]
			{
				150f, -150f, 120f, -120f, 180f, 95f, -95f, 165f, -165f, 135f,
				-135f, 105f
			};
			float f = yaw + array[slot % array.Length] * (MathF.PI / 180f);
			float num = 1.6f + (float)(slot / 4) * 0.8f;
			for (float num2 = 1f; num2 > 0.2f; num2 -= 0.35f)
			{
				Vector2 vector = me + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num * num2;
				WorldTile tile = World.world.GetTile(Mathf.FloorToInt(vector.x), Mathf.FloorToInt(vector.y));
				if (tile != null && Royal.Standable(tile) && tile.isSameIsland(at))
				{
					return tile;
				}
			}
			return at;
		}

		public static bool Asleep(Actor a)
		{
			try
			{
				return ((BaseSimObject)a).hasStatus("sleeping");
			}
			catch
			{
				return false;
			}
		}

		public static bool OnOurTask(Actor a)
		{
			try
			{
				return a.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task != null && (((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id == "fp_family_follow" || ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id == "fp_family_run" || ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id == "fp_family_in");
			}
			catch
			{
				return false;
			}
		}

		private static void Send(Actor a, string task, WorldTile to)
		{
			a.setTask(task);
			a.beh_tile_target = to;
		}

		private static void Release(Actor a)
		{
			try
			{
				if (a != null && a.isAlive() && OnOurTask(a))
				{
					a.cancelAllBeh();
				}
			}
			catch
			{
			}
		}

		private static void ReleaseAll()
		{
			foreach (Actor item in _family)
			{
				Release(item);
			}
			_family.Clear();
			_withYou.Clear();
			_orders.Clear();
			_waitAt.Clear();
			_warnedAt.Clear();
			_hungryTold.Clear();
			_relation.Clear();
		}

		private static void RegisterTasks()
		{
			if (_registered)
			{
				return;
			}
			_registered = true;
			try
			{
				Register("fp_family_follow", 1f, 1.2f, 2.6f);
				Register("fp_family_run", 1.6f, 0.2f, 0.5f);
				RegisterIn();
			}
			catch (Exception ex)
			{
				Log.Error("could not register the family's follow tasks: " + ex);
			}
		}

		private static void Register(string id, float speed, float waitMin, float waitMax)
		{
			if (AssetManager.tasks_actor.get(id) == null)
			{
				BehaviourTaskActor behaviourTaskActor = new BehaviourTaskActor
				{
					id = id,
					locale_key = "task_unit_move",
					speed_multiplier = speed,
					cancellable_by_reproduction = false,
					cancellable_by_socialize = false
				};
				behaviourTaskActor.setIcon("ui/Icons/iconFamily");
				BehGoToTileTarget behGoToTileTarget = new BehGoToTileTarget();
				((BehaviourActionBase<Actor>)behGoToTileTarget).setupErrorChecks();
				behaviourTaskActor.addBeh(behGoToTileTarget);
				BehRandomWait behRandomWait = new BehRandomWait(waitMin, waitMax);
				((BehaviourActionBase<Actor>)behRandomWait).setupErrorChecks();
				behaviourTaskActor.addBeh(behRandomWait);
				AssetManager.tasks_actor.add(behaviourTaskActor);
			}
		}

		private static void RegisterIn()
		{
			if (AssetManager.tasks_actor.get("fp_family_in") == null)
			{
				BehaviourTaskActor behaviourTaskActor = new BehaviourTaskActor
				{
					id = "fp_family_in",
					locale_key = "task_unit_move",
					speed_multiplier = 1.3f,
					cancellable_by_reproduction = false,
					cancellable_by_socialize = false
				};
				behaviourTaskActor.setIcon("ui/Icons/iconFamily");
				BehaviourActionActor[] array = new BehaviourActionActor[3]
				{
					new BehGoToTileTarget(),
					new BehStayInBuildingTarget(600f, 900f),
					new BehExitBuilding()
				};
				foreach (BehaviourActionActor behaviourActionActor in array)
				{
					((BehaviourActionBase<Actor>)behaviourActionActor).setupErrorChecks();
					behaviourTaskActor.addBeh(behaviourActionActor);
					behaviourActionActor.null_check_city = false;
				}
				AssetManager.tasks_actor.add(behaviourTaskActor);
			}
		}
	}
}
