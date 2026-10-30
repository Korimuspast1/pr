using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal static class CraftStores
	{
		internal abstract class Store
		{
			public abstract string Name { get; }

			public abstract int Count(string resource);

			public abstract int Take(string resource, int amount);
		}

		private sealed class Bag : Store
		{
			public Actor You;

			public override string Name => "your bag";

			public override int Count(string resource)
			{
				try
				{
					return You.inventory.getResource(resource);
				}
				catch
				{
					return 0;
				}
			}

			public override int Take(string resource, int amount)
			{
				int num = Math.Min(amount, Count(resource));
				if (num > 0)
				{
					try
					{
						You.takeFromInventory(resource, num);
					}
					catch
					{
						return 0;
					}
				}
				return num;
			}
		}

		public static readonly List<Func<Actor, IEnumerable<Store>>> Chests = new List<Func<Actor, IEnumerable<Store>>>();

		private static readonly Bag YourBag = new Bag();

		private static readonly List<Store> Found = new List<Store>();

		private static int _foundFrame = -1;

		private static int _foundProviders;

		private static Actor _foundFor;

		public static List<Store> Near(Actor you)
		{
			if (Time.frameCount == _foundFrame && you == _foundFor && Chests.Count == _foundProviders)
			{
				return Found;
			}
			_foundFrame = Time.frameCount;
			_foundFor = you;
			_foundProviders = Chests.Count;
			Found.Clear();
			YourBag.You = you;
			Found.Add(YourBag);
			foreach (Func<Actor, IEnumerable<Store>> chest in Chests)
			{
				try
				{
					IEnumerable<Store> enumerable = chest(you);
					if (enumerable == null)
					{
						continue;
					}
					foreach (Store item in enumerable)
					{
						if (item != null)
						{
							Found.Add(item);
						}
					}
				}
				catch (Exception ex)
				{
					Log.Error("craft stores: " + ex.Message);
				}
			}
			return Found;
		}

		public static int Count(Actor you, string resource)
		{
			int num = 0;
			foreach (Store item in Near(you))
			{
				num += item.Count(resource);
			}
			return num;
		}

		public static int InChests(Actor you, string resource)
		{
			int num = 0;
			List<Store> list = Near(you);
			for (int i = 1; i < list.Count; i++)
			{
				num += list[i].Count(resource);
			}
			return num;
		}

		public static bool Take(Actor you, string resource, int amount)
		{
			if (Count(you, resource) < amount)
			{
				return false;
			}
			int num = amount;
			foreach (Store item in Near(you))
			{
				if (num <= 0)
				{
					break;
				}
				num -= item.Take(resource, num);
			}
			return num <= 0;
		}
	}
}
