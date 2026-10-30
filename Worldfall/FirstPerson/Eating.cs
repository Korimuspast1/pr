using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal static class Eating
	{
		public const KeyCode Key = KeyCode.J;

		private const float FullRatio = 0.95f;

		private const float LowRatio = 0.2f;

		public static bool Hungry(Actor host)
		{
			try
			{
				return host != null && host.isAlive() && host.getMaxNutrition() > 0 && host.getNutritionRatio() < 0.2f;
			}
			catch
			{
				return false;
			}
		}

		public static void Eat(Actor host, Action<string> toast)
		{
			if (host == null || !host.isAlive())
			{
				return;
			}
			int num = 0;
			try
			{
				num = host.getMaxNutrition();
			}
			catch
			{
			}
			if (num <= 0)
			{
				toast("You don't need to eat.");
				return;
			}
			if (host.getNutritionRatio() >= 0.95f)
			{
				toast("You're not hungry.");
				return;
			}
			ResourceAsset resourceAsset = FromBag(host);
			string text = "from your bag";
			if (resourceAsset != null)
			{
				host.takeFromInventory(resourceAsset.id, 1);
			}
			else
			{
				resourceAsset = FromCity(host, toast, out var pay);
				if (resourceAsset == null)
				{
					return;
				}
				if (pay)
				{
					host.spendMoney(resourceAsset.money_cost);
				}
				host.city.eatFoodItem(resourceAsset.id);
				text = "from the stores of " + host.city.name;
			}
			try
			{
				host.consumeFoodResource(resourceAsset);
			}
			catch (Exception ex)
			{
				Log.Error("eating " + resourceAsset.id + " failed: " + ex.Message);
				return;
			}
			Sounds.PlayAt("event:/SFX/CIVILIZATIONS/CollectFruits", host.current_position);
			toast("You eat some " + Work.ResourceName(resourceAsset.id).ToLowerInvariant() + " " + text + ".");
		}

		public static bool Edible(Actor host, string id)
		{
			try
			{
				ResourceAsset resourceAsset = AssetManager.resources.get(id);
				return resourceAsset != null && resourceAsset.food && resourceAsset.restore_nutrition > 0 && host.getMaxNutrition() > 0 && Allowed(host, id);
			}
			catch
			{
				return false;
			}
		}

		public static bool EatFromBag(Actor host, string id, Action<string> toast)
		{
			if (host == null || !host.isAlive() || !Edible(host, id))
			{
				return false;
			}
			if (host.getNutritionRatio() >= 0.95f)
			{
				toast("You're not hungry.");
				return false;
			}
			ResourceAsset pAsset = AssetManager.resources.get(id);
			try
			{
				host.takeFromInventory(id, 1);
				host.consumeFoodResource(pAsset);
			}
			catch (Exception ex)
			{
				Log.Error("eating " + id + " failed: " + ex.Message);
				return false;
			}
			Sounds.PlayAt("event:/SFX/CIVILIZATIONS/CollectFruits", host.current_position);
			toast("You eat some " + Work.ResourceName(id).ToLowerInvariant() + ".");
			return true;
		}

		private static ResourceAsset FromBag(Actor host)
		{
			Dictionary<string, ResourceContainer> dictionary = null;
			try
			{
				dictionary = host.inventory?.getResources();
			}
			catch
			{
			}
			if (dictionary == null)
			{
				return null;
			}
			string text = null;
			try
			{
				text = host.data.favorite_food;
			}
			catch
			{
			}
			ResourceAsset resourceAsset = null;
			foreach (KeyValuePair<string, ResourceContainer> item in dictionary)
			{
				if (item.Value.amount <= 0)
				{
					continue;
				}
				ResourceAsset resourceAsset2 = AssetManager.resources.get(item.Key);
				if (resourceAsset2 != null && resourceAsset2.food && resourceAsset2.restore_nutrition > 0 && Allowed(host, resourceAsset2.id))
				{
					if (resourceAsset2.id == text)
					{
						return resourceAsset2;
					}
					if (resourceAsset == null || resourceAsset2.tastiness > resourceAsset.tastiness)
					{
						resourceAsset = resourceAsset2;
					}
				}
			}
			return resourceAsset;
		}

		private static bool Allowed(Actor host, string id)
		{
			try
			{
				return (host.subspecies?.getAllowedFoodByDiet())?.Contains(id) ?? true;
			}
			catch
			{
				return true;
			}
		}

		private static ResourceAsset FromCity(Actor host, Action<string> toast, out bool pay)
		{
			pay = false;
			City city = host.city;
			if (city == null)
			{
				toast(People.IsHumanoid(host) ? "Nothing to eat. Pick berries or hunt, or join a city." : "Nothing to eat in your bag.");
				return null;
			}
			ResourceAsset resourceAsset = null;
			try
			{
				if (city.hasSuitableFood(host.subspecies))
				{
					resourceAsset = city.getFoodItem(host.subspecies, host.data.favorite_food);
				}
			}
			catch
			{
			}
			if (resourceAsset == null)
			{
				toast("Nothing to eat in your bag, and " + city.name + "'s stores are empty.");
				return null;
			}
			pay = !host.isFoodFreeForThisPerson();
			if (pay && !host.hasEnoughMoney(resourceAsset.money_cost))
			{
				toast("You can't afford a meal from " + city.name + "'s stores (" + resourceAsset.money_cost + " coins).");
				return null;
			}
			return resourceAsset;
		}
	}
}
