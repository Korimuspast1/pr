using System;
using System.Collections.Generic;
using System.Linq;
using FirstPerson.Net;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal static class Founding
	{
		public const int Wood = 10;

		public const int Food = 10;

		public const int Followers = 3;

		private const float WithinTiles = 12f;

		private const string SupplyKey = "fp_supply";

		private static readonly List<City> Waiting = new List<City>();

		private static float _tick;

		private static object _world;

		public static List<Actor> With(Actor you)
		{
			List<Actor> list = new List<Actor>();
			try
			{
				Vector2 at = you.current_position;
				foreach (Actor member in FamilyFollow.Members)
				{
					if (member != null && member.isAlive() && member.isAdult() && FamilyFollow.OrderOf(member) != FamilyFollow.Orders.Free && Near(member))
					{
						list.Add(member);
					}
				}
				foreach (Hireling item in Economy.Hired)
				{
					if (item.Still && Near(item.Guard) && !list.Contains(item.Guard))
					{
						list.Add(item.Guard);
					}
				}
				bool Near(Actor a)
				{
					return (a.current_position - at).sqrMagnitude <= 144f;
				}
			}
			catch
			{
			}
			return list;
		}

		public static string Cost(Actor you)
		{
			if (!Run.LockedOn(you) || With(you).Count >= 3)
			{
				return null;
			}
			return $"{10} wood and {10} food";
		}

		public static string Note(Actor you)
		{
			if (!Run.LockedOn(you))
			{
				return "";
			}
			int count = With(you).Count;
			if (count < 3)
			{
				return $" ({10} wood, {10} food)";
			}
			return $" ({count} with you)";
		}

		public static bool Ready(Actor you, Action<string> toast)
		{
			if (Cost(you) == null)
			{
				return true;
			}
			int num = Civil.BagCount(you, "wood");
			int num2 = FamilyFollow.FoodInBag(you);
			if (num >= 10 && num2 >= 10)
			{
				return true;
			}
			toast?.Invoke($"To found a town alone you need {10} wood and {10} food in your bag (you have {num} and {num2}), or {3} or more with you.");
			return false;
		}

		public static void Founded(Actor you, City c, bool charter, Action<string> toast)
		{
			if (you == null || c == null)
			{
				return;
			}
			Run.Tally(you, "founded");
			if (charter)
			{
				return;
			}
			bool flag = Cost(you) != null;
			if (flag)
			{
				List<string> list = new List<string>();
				int num = 0;
				if (Civil.TakeFromBag(you, "wood", 10))
				{
					list.Add("wood:" + 10);
				}
				int num2 = 10;
				foreach (KeyValuePair<string, int> item in Bag.Contents(you).ToList())
				{
					if (num2 <= 0)
					{
						break;
					}
					ResourceAsset resourceAsset = null;
					try
					{
						resourceAsset = AssetManager.resources.get(item.Key);
					}
					catch
					{
					}
					if (resourceAsset != null && resourceAsset.food && resourceAsset.restore_nutrition > 0)
					{
						int num3 = Mathf.Min(num2, item.Value);
						if (Civil.TakeFromBag(you, item.Key, num3))
						{
							list.Add(item.Key + ":" + num3);
							num2 -= num3;
							num += num3;
						}
					}
				}
				try
				{
					c.data.set("fp_supply", string.Join(",", list.ToArray()));
				}
				catch
				{
				}
				if (!Waiting.Contains(c))
				{
					Waiting.Add(c);
				}
				toast?.Invoke(string.Format("Your {0} wood and {1} food will go into {2}'s first stockpile.", (list.Count > 0 && list[0].StartsWith("wood:")) ? 10 : 0, num, c.name));
			}
			int num4 = 0;
			foreach (Hireling item2 in Economy.Hired.ToList())
			{
				try
				{
					if (item2.Still && item2.Guard.city != c && !item2.Guard.isKing() && !item2.Guard.isCityLeader() && !((item2.Guard.current_position - you.current_position).sqrMagnitude > 144f))
					{
						item2.Guard.joinCity(c);
						num4++;
					}
				}
				catch
				{
				}
			}
			Log.Info(string.Format("founding: {0} founded {1}, {2}, {3} sellsword(s) moved in", WorldBoxMod.Describe(you), c.name, flag ? "with supplies" : "with followers", num4));
		}

		public static void Update(float dt)
		{
			try
			{
				if (!Config.game_loaded || SmoothLoader.isLoading())
				{
					return;
				}
			}
			catch
			{
				return;
			}
			object obj2 = World.world?.map_stats;
			if (obj2 != _world)
			{
				_world = obj2;
				Rescan();
			}
			if (!WorldSync.Mirroring)
			{
				try
				{
					Village.Tick(dt);
				}
				catch (Exception e)
				{
					Hub.Fail("village", e);
				}
			}
			try
			{
				PlacedLights.Tick(dt);
			}
			catch (Exception e2)
			{
				Hub.Fail("lights", e2);
			}
			if (!WorldSync.Mirroring)
			{
				try
				{
					TownLights.Tick(dt);
				}
				catch (Exception e3)
				{
					Hub.Fail("town lights", e3);
				}
			}
			_tick -= dt;
			if (_tick > 0f || Waiting.Count == 0)
			{
				return;
			}
			_tick = 3f;
			for (int num = Waiting.Count - 1; num >= 0; num--)
			{
				City city = Waiting[num];
				try
				{
					if (city == null || !city.isAlive())
					{
						Waiting.RemoveAt(num);
						Log.Info("founding: a new town fell before its first stockpile; its supplies are lost");
					}
					else if (Economy.HasStores(city))
					{
						city.data.get("fp_supply", out var pResult, null);
						if (!string.IsNullOrEmpty(pResult))
						{
							string[] array = pResult.Split(',');
							foreach (string text in array)
							{
								int num2 = text.IndexOf(':');
								if (num2 > 0 && int.TryParse(text.Substring(num2 + 1), out var result))
								{
									city.addResourcesToRandomStockpile(text.Substring(0, num2), result);
								}
							}
						}
						city.data.removeString("fp_supply");
						Waiting.RemoveAt(num);
						Log.Info("founding: " + city.name + "'s first stockpile has your supplies (" + pResult + ")");
					}
				}
				catch (Exception ex)
				{
					Log.Error("founding: supplies: " + ex.Message);
					Waiting.RemoveAt(num);
				}
			}
		}

		public static void Rescan()
		{
			Waiting.Clear();
			try
			{
				foreach (City item in World.world.cities.list)
				{
					if (item != null && item.isAlive())
					{
						item.data.get("fp_supply", out var pResult, null);
						if (!string.IsNullOrEmpty(pResult))
						{
							Waiting.Add(item);
						}
					}
				}
			}
			catch
			{
			}
		}
	}
}
