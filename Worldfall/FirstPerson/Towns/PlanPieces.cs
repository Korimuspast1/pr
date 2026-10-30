using System.Collections.Generic;

namespace FirstPerson.Towns
{
	internal static class PlanPieces
	{
		internal sealed class Piece
		{
			public string Order;

			public string Name;

			public int Level;

			public int Limit;

			public bool NeedsFire = true;

			public bool Home;

			public bool Street;

			public int Wall = -1;

			public int Light = -1;
		}

		internal sealed class Category
		{
			public string Name;

			public string Icon;

			public readonly List<Piece> Pieces = new List<Piece>();
		}

		public static readonly List<Category> All;

		public static Category Lights { get; private set; }

		static PlanPieces()
		{
			All = new List<Category>();
			Add("Homes", "iconHouse").Pieces.Add(new Piece
			{
				Order = "order_tent",
				Name = "Home",
				Level = 1,
				Limit = -1,
				Home = true
			});
			Category category = Add("Hall & guard", "iconCity");
			category.Pieces.Add(new Piece
			{
				Order = "order_bonfire",
				Name = "Bonfire",
				Level = 1,
				Limit = 1,
				NeedsFire = false
			});
			category.Pieces.Add(new Piece
			{
				Order = "order_hall_0",
				Name = "Village hall",
				Level = 1,
				Limit = 1
			});
			category.Pieces.Add(new Piece
			{
				Order = "order_watch_tower",
				Name = "Watch tower",
				Level = 4,
				Limit = 1
			});
			category.Pieces.Add(new Piece
			{
				Order = "order_barracks",
				Name = "Barracks",
				Level = 5,
				Limit = 1
			});
			category.Pieces.Add(new Piece
			{
				Order = "order_training_dummy",
				Name = "Training dummy",
				Level = 5,
				Limit = 3
			});
			category.Pieces.Add(new Piece
			{
				Order = "order_statue",
				Name = "Statue",
				Level = 6,
				Limit = 1
			});
			category.Pieces.Add(new Piece
			{
				Order = "order_library",
				Name = "Library",
				Level = 6,
				Limit = 1
			});
			category.Pieces.Add(new Piece
			{
				Order = "order_temple",
				Name = "Temple",
				Level = 8,
				Limit = 1
			});
			Category category2 = Add("Work", "iconResWood");
			category2.Pieces.Add(new Piece
			{
				Order = "order_stockpile",
				Name = "Stockpile",
				Level = 1,
				Limit = 1,
				NeedsFire = false
			});
			category2.Pieces.Add(new Piece
			{
				Order = "order_windmill_0",
				Name = "Windmill",
				Level = 2,
				Limit = 1
			});
			category2.Pieces.Add(new Piece
			{
				Order = "order_docks_0",
				Name = "Fishing docks",
				Level = 2,
				Limit = 5
			});
			category2.Pieces.Add(new Piece
			{
				Order = "order_mine",
				Name = "Mine",
				Level = 3,
				Limit = 1
			});
			category2.Pieces.Add(new Piece
			{
				Order = "order_well",
				Name = "Well",
				Level = 3,
				Limit = 1
			});
			category2.Pieces.Add(new Piece
			{
				Order = "order_market",
				Name = "Market",
				Level = 7,
				Limit = 1
			});
			Category category3 = Add("Walls", "iconWalls");
			for (int i = 0; i < 3; i++)
			{
				category3.Pieces.Add(new Piece
				{
					Order = null,
					Name = WallPlans.Names[i],
					Level = 1,
					Limit = -1,
					NeedsFire = false,
					Wall = i
				});
			}
			Add("Roads", "iconRoads").Pieces.Add(new Piece
			{
				Order = null,
				Name = "Street",
				Level = 1,
				Limit = -1,
				NeedsFire = false,
				Street = true
			});
			Lights = Add("Lights", "iconFire");
			for (int j = 0; j < PlacedLights.Names.Length; j++)
			{
				Lights.Pieces.Add(new Piece
				{
					Order = null,
					Name = PlacedLights.Names[j],
					Level = 1,
					Limit = -1,
					NeedsFire = false,
					Light = j
				});
			}
		}

		private static Category Add(string name, string icon)
		{
			Category category = new Category
			{
				Name = name,
				Icon = icon
			};
			All.Add(category);
			return category;
		}

		public static BuildingAsset AssetOf(Piece p, City c)
		{
			if (p == null || c == null || p.Order == null)
			{
				return null;
			}
			try
			{
				ArchitectureAsset architectureAsset = c.getActorAsset()?.architecture_asset;
				if (architectureAsset?.building_ids_for_construction == null)
				{
					return null;
				}
				string text = p.Order;
				if (text == "order_tent" && !architectureAsset.building_ids_for_construction.ContainsKey(text))
				{
					text = "order_house_0";
				}
				BuildingAsset building = architectureAsset.getBuilding(text);
				if (building != null)
				{
					return building;
				}
			}
			catch
			{
			}
			return null;
		}

		public static Piece OfOrder(string order)
		{
			foreach (Category item in All)
			{
				foreach (Piece piece in item.Pieces)
				{
					if (piece.Order == order)
					{
						return piece;
					}
				}
			}
			return null;
		}

		public static int LevelOf(City c)
		{
			return Growth.LevelOf(c);
		}

		public static string WhyNot(Piece p, City c)
		{
			if (p != null && p.Light >= 0)
			{
				return PlacedLights.Lacks(Hub.Host, (PlacedLights.Kind)p.Light);
			}
			if (p != null && p.Street)
			{
				if (c != null)
				{
					return null;
				}
				return "No town";
			}
			if (p != null && p.Wall >= 0)
			{
				return WallPlans.WhyNotKind(c, p.Wall);
			}
			BuildingAsset buildingAsset = AssetOf(p, c);
			if (buildingAsset == null)
			{
				return "Not built by your people";
			}
			if (LevelOf(c) < p.Level)
			{
				return "Opens at level " + p.Level;
			}
			try
			{
				if (p.NeedsFire && c.countBuildingsType("type_bonfire", pCountOnlyFinished: false) == 0)
				{
					return "Needs a bonfire first";
				}
				int num = c.countBuildingsType(buildingAsset.type, pCountOnlyFinished: false);
				if (p.Limit > 0 && num >= p.Limit)
				{
					return (p.Limit == 1) ? "One per town" : (p.Limit + " per town");
				}
				if (p.Limit < 0 && num >= c.getHouseLimit())
				{
					return "No room for more homes yet";
				}
			}
			catch
			{
			}
			return null;
		}

		public static string CostLabel(Piece p, City c)
		{
			if (p != null && p.Light >= 0)
			{
				Supplies.Kind kind = Supplies.Of(PlacedLights.ItemOf((PlacedLights.Kind)p.Light));
				if (kind != null)
				{
					return "Takes a " + kind.One + " (" + PlacedLights.Carried(Hub.Host, (PlacedLights.Kind)p.Light) + " in your bag)";
				}
				return "";
			}
			if (p != null && p.Wall >= 0)
			{
				return WallPlans.Cost(p.Wall, 1) + " a tile";
			}
			if (p != null && p.Street)
			{
				return "Free";
			}
			return CostText(AssetOf(p, c));
		}

		public static string CostText(BuildingAsset a)
		{
			ConstructionCost constructionCost = a?.cost;
			if (constructionCost == null)
			{
				return "Free";
			}
			List<string> list = new List<string>(4);
			if (constructionCost.wood > 0)
			{
				list.Add(constructionCost.wood + " wood");
			}
			if (constructionCost.stone > 0)
			{
				list.Add(constructionCost.stone + " stone");
			}
			if (constructionCost.common_metals > 0)
			{
				list.Add(constructionCost.common_metals + " metal");
			}
			if (constructionCost.gold > 0)
			{
				list.Add(constructionCost.gold + " gold");
			}
			if (list.Count != 0)
			{
				return string.Join(", ", list.ToArray());
			}
			return "Free";
		}
	}
}
