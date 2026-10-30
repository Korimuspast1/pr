using System;
using System.Collections.Generic;
using FirstPerson.Core;
using FirstPerson.Net;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal static class LandPlot
	{
		public const string Key = "fp_plot";

		private static readonly int[] Prices = new int[6] { 120, 160, 210, 270, 330, 400 };

		private static readonly string[] Sizes = new string[6] { "A cottage", "A small house", "A house", "A good house", "A large house", "A manor" };

		private const float PlaceReach = 18f;

		private static Actor _for;

		private static City _town;

		private static int _tier;

		private static int _paid;

		private static string _site;

		private static string _house;

		private static bool _valid;

		private static bool _owed;

		private static string _why;

		private static WorldTile _aim;

		private static readonly MeshInstance Ghost = new MeshInstance
		{
			NoShadow = true
		};

		private static float _watch;

		private static float _helpAt;

		internal static WorldTile AimForTest;

		private static readonly uint Green = (TerrainCache.Rgb(90, 230, 110) & 0xFFFFFF) | 0x96000000u;

		private static readonly uint Red = (TerrainCache.Rgb(240, 70, 60) & 0xFFFFFF) | 0x96000000u;

		private const long Marking = -1L;

		private static string _dyingPlot;

		private static Building _dyingHouse;

		public static bool Aiming { get; private set; }

		public static string Prompt => "LMB:Place the plot|Esc:Cancel";

		internal static bool ValidForTest => _valid;

		internal static string WhyForTest => _why;

		internal static WorldTile AimTile => _aim;

		public static void Choices(Conversation c, List<Choice> list)
		{
			Actor with = c.With;
			Actor you = c.You;
			City town = Freedom.HomeTown(you);
			if (with != null && you != null && town != null && Grants(with, you, town))
			{
				Choice choice = new Choice("I'd like land for a house.", delegate
				{
					Offer(c, town);
				});
				string text = WhyNot(you, town);
				if (text != null)
				{
					choice.Off(text);
				}
				list.Add(choice);
			}
		}

		private static bool Grants(Actor npc, Actor you, City town)
		{
			try
			{
				if (npc.isKing() && npc.kingdom == town.kingdom)
				{
					return true;
				}
				if (town.leader == npc)
				{
					return true;
				}
				return Rep.Rules(you, town) && npc.city == town && npc.isAdult();
			}
			catch
			{
				return false;
			}
		}

		private static string WhyNot(Actor you, City town)
		{
			if (!Aiming)
			{
				return WhyNotFor(you, town);
			}
			return "You're marking a plot";
		}

		private static string WhyNotFor(Actor you, City town)
		{
			if (PendingSite(you) != null)
			{
				return "Your house is being built";
			}
			Building building = Workbench.HomeOf(you);
			if (building != null && Homes.OwnedBy(building, you) && building.city == town)
			{
				return "You have a house here";
			}
			if (SiteOf(town) == null)
			{
				return "No houses are built here";
			}
			return null;
		}

		private static void Offer(Conversation c, City town)
		{
			c.LastTopic = "land";
			Actor you = c.You;
			if (WhyNot(you, town) != null)
			{
				c.Say("Not now.");
				c.Offer(Dialogue.Topics(c));
				return;
			}
			int num = Mathf.Min(BestTier(town), TopTier(town));
			bool flag = Rep.Rules(you, town);
			List<Choice> list = new List<Choice>();
			for (int i = 0; i <= num; i++)
			{
				int tier = i;
				int price = PriceOf(i, flag);
				Choice choice = new Choice($"{Sizes[i]} ({price} gold)", delegate
				{
					Buy(c, town, tier, price);
				});
				int num2 = Economy.Money(you);
				if (num2 < price)
				{
					choice.Off($"You have {num2} gold");
				}
				list.Add(choice);
			}
			list.Add(Dialogue.Back(c));
			c.Say(flag ? Dialogue.Pick("Your land, Your Grace. Only the building costs. How grand?", "Mark it out and the builders will start. What size?") : Dialogue.Pick("There's land in " + Rep.NameOf(town) + ". Mark out your plot and the town's builders will raise it. How big a house?", "Land costs. The bigger the house, the more of it. What will it be?"));
			c.Offer(list);
		}

		public static int PriceOf(int tier, bool ruler)
		{
			int num = Prices[Mathf.Clamp(tier, 0, Prices.Length - 1)];
			if (!ruler)
			{
				return num;
			}
			return Mathf.RoundToInt((float)num * 0.6f / 10f) * 10;
		}

		private static void Buy(Conversation c, City town, int tier, int price)
		{
			c.LastTopic = "land_bought";
			Actor you = c.You;
			if (WhyNot(you, town) != null || Economy.Money(you) < price)
			{
				c.Say("Come back when you can pay.");
				c.Offer(Dialogue.Topics(c));
				return;
			}
			bool mirroring = WorldSync.Mirroring;
			if (!mirroring && !Economy.Pay(you, price, town, c.With))
			{
				c.Say("You can't pay that.");
				c.Offer(Dialogue.Topics(c));
				return;
			}
			Begin(you, town, tier, price);
			_owed = mirroring;
			c.EndAfter = true;
			c.Say(Dialogue.Pick("It's yours. Mark out the plot on the town's land.", "Done. Show the builders where."));
			Log.Info($"land: bought a tier {tier} plot in {Rep.NameOf(town)} for {price}");
		}

		internal static void Begin(Actor you, City town, int tier, int paid)
		{
			_for = you;
			_town = town;
			_tier = Mathf.Clamp(tier, 0, TopTier(town));
			_paid = paid;
			_owed = false;
			_site = SiteOf(town)?.id;
			_house = HouseOf(town, _tier)?.id;
			_valid = false;
			_why = null;
			_aim = null;
			Aiming = true;
			if (paid > 0 && !WorldSync.Mirroring)
			{
				Save(you, Rep.IdOf(town), -1L, _tier, paid);
			}
			Hub.Toast?.Invoke("Mark out your plot on the town's land");
		}

		public static bool Escape()
		{
			if (!Aiming)
			{
				return false;
			}
			Cancel("the gold is returned");
			return true;
		}

		private static void Cancel(string why)
		{
			Aiming = false;
			if (_for != null && _paid > 0 && !_owed)
			{
				int num = 0;
				try
				{
					num = Economy.PayOut(_for, _paid, _town, null);
				}
				catch
				{
				}
				try
				{
					if (num < _paid && _for.isAlive())
					{
						_for.addMoney(_paid - num);
					}
				}
				catch
				{
				}
			}
			if (_for != null && Read(_for, out var _, out var buildingId, out var _, out var _) && buildingId == -1)
			{
				Clear(_for);
			}
			Hub.Toast?.Invoke(_owed ? "No plot marked" : ("No plot marked. " + Capital(why)));
			Log.Info("land: placing cancelled (" + why + ")");
			_for = null;
			_town = null;
			_paid = 0;
			_owed = false;
		}

		private static string Capital(string s)
		{
			if (!string.IsNullOrEmpty(s))
			{
				return char.ToUpperInvariant(s[0]) + s.Substring(1);
			}
			return s;
		}

		private static void Aim(Actor host)
		{
			if (host != _for)
			{
				Cancel("you left that body");
				return;
			}
			bool flag;
			try
			{
				flag = _town != null && _town.isAlive() && host.city == _town;
			}
			catch
			{
				flag = false;
			}
			if (!flag)
			{
				Cancel("the town is no longer yours");
				return;
			}
			_aim = null;
			_valid = false;
			_why = "Look at the town's land";
			Vec3 point;
			Actor creature;
			if (AimForTest != null)
			{
				_aim = AimForTest;
			}
			else if (FirstPersonAim.Ready(host) && FirstPersonAim.Point(out point, out creature))
			{
				if ((new Vector2(point.X, point.Y) - host.current_position).magnitude <= 18f)
				{
					_aim = World.world.GetTile(Mathf.FloorToInt(point.X), Mathf.FloorToInt(point.Y));
				}
				else
				{
					_why = "Too far";
				}
			}
			if (_aim != null)
			{
				_valid = CanStand(_town, _tier, _aim, out _why);
			}
			if (PlayInput.GetMouseButtonDown(0) && Hub.KeysFree)
			{
				if (!_valid)
				{
					Hub.Toast?.Invoke(_why ?? "The house can't stand there");
				}
				else if (WorldSync.Mirroring)
				{
					Ask(host, _aim);
				}
				else
				{
					Place(host, _aim);
				}
			}
		}

		internal static bool Ask(Actor you, WorldTile t)
		{
			bool flag = WorldSync.ForwardPlot(you, _town, _tier, t);
			Log.Info($"land: plot at {t.x},{t.y} in {Rep.NameOf(_town)} (tier {_tier}) " + (flag ? "asked of the host" : "not sent"));
			if (!flag)
			{
				Hub.Toast?.Invoke("The house can't stand there");
				return false;
			}
			Aiming = false;
			_for = null;
			_paid = 0;
			_owed = false;
			return true;
		}

		internal static string RaiseFor(Actor you, City town, int tier, WorldTile t, out Building b)
		{
			b = null;
			bool flag;
			try
			{
				flag = town != null && town.isAlive() && Freedom.HomeTown(you) == town;
			}
			catch
			{
				flag = false;
			}
			if (!flag)
			{
				return "Not your town";
			}
			string why = WhyNotFor(you, town);
			if (why != null)
			{
				return why;
			}
			if (tier < 0 || tier > Mathf.Min(BestTier(town), TopTier(town)))
			{
				return "No such houses are built here";
			}
			int num = PriceOf(tier, Rep.Rules(you, town));
			if (Economy.Money(you) < num)
			{
				return "You can't pay that";
			}
			if (!CanStand(town, tier, t, out why))
			{
				return why;
			}
			if (!Economy.Pay(you, num, town))
			{
				return "You can't pay that";
			}
			b = Raise(you, town, tier, num, t);
			if (b != null)
			{
				return null;
			}
			int num2 = 0;
			try
			{
				num2 = Economy.PayOut(you, num, town, null);
			}
			catch
			{
			}
			try
			{
				if (num2 < num && you.isAlive())
				{
					you.addMoney(num - num2);
				}
			}
			catch
			{
			}
			return "The house can't stand there";
		}

		internal static bool CanStand(City town, int tier, WorldTile t, out string why)
		{
			why = null;
			BuildingAsset buildingAsset = SiteOf(town);
			BuildingAsset buildingAsset2 = HouseOf(town, tier);
			if (buildingAsset == null || buildingAsset2 == null)
			{
				why = "No houses are built here";
				return false;
			}
			try
			{
				if (t.zone == null || t.zone.city != town)
				{
					why = "Not the town's land";
					return false;
				}
				if (!World.world.buildings.canBuildFrom(t, buildingAsset2, town, BuildPlacingType.New, pFloraGrowth: false) || !World.world.buildings.canBuildFrom(t, buildingAsset, town, BuildPlacingType.New, pFloraGrowth: false))
				{
					why = "The ground isn't clear";
					return false;
				}
			}
			catch (Exception e)
			{
				Hub.Fail("land check", e);
				why = "The house can't stand there";
				return false;
			}
			return true;
		}

		internal static Building Place(Actor you, WorldTile t)
		{
			Building building = Raise(you, _town, _tier, _paid, t);
			if (building == null)
			{
				Hub.Toast?.Invoke("The house can't stand there");
				return null;
			}
			Hub.Toast?.Invoke("Plot marked. The town's builders will raise it");
			Sounds.PlayAt("event:/SFX/UI/YesPoint", you.current_position);
			Aiming = false;
			_for = null;
			_paid = 0;
			return building;
		}

		private static Building Raise(Actor you, City town, int tier, int paid, WorldTile t)
		{
			Building building = null;
			try
			{
				building = World.world.buildings.addBuilding(SiteOf(town)?.id, t, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				if (building != null)
				{
					building.setKingdom(town.kingdom);
					town.listBuilding(building);
					building.setUnderConstruction();
					if (!town.hasBuildingToBuild())
					{
						town.under_construction_building = building;
					}
					Homes.SetOwner(building, you);
				}
			}
			catch (Exception e)
			{
				Hub.Fail("land place", e);
			}
			if (building == null)
			{
				return null;
			}
			Save(you, Rep.IdOf(town), building.data.id, tier, paid);
			Log.Info($"land: plot placed at {t.x},{t.y} in {Rep.NameOf(town)} (tier {tier}, site {building.asset.id}) for {WorldBoxMod.Describe(you)}");
			return building;
		}

		public static void Update(Actor host, float dt)
		{
			if (!WorldBoxMod.WorldLoading)
			{
				if (Aiming)
				{
					Aim(host);
				}
				if (!WorldSync.Mirroring && !((_watch -= dt) > 0f) && host != null)
				{
					_watch = 1f;
					Watch(host, null);
				}
			}
		}

		internal static void WatchFor(Actor body, Action<string, bool> tell)
		{
			if (!WorldBoxMod.WorldLoading && body != null && tell != null)
			{
				Watch(body, tell);
			}
		}

		private static void Tell(string text, bool cheer, Actor you)
		{
			Hub.Toast?.Invoke(text);
			if (cheer && you != null)
			{
				Sounds.PlayAt("event:/SFX/UI/YesPoint", you.current_position);
			}
		}

		private static void Watch(Actor host, Action<string, bool> tell)
		{
			if (!Read(host, out var _, out var buildingId, out var tier, out var paid))
			{
				return;
			}
			if (buildingId == -1)
			{
				if (Aiming && _for == host)
				{
					return;
				}
				Clear(host);
				try
				{
					if (paid > 0)
					{
						host.addMoney(paid);
					}
				}
				catch
				{
				}
				Hub.Toast?.Invoke("Your plot was never marked. The gold is returned");
				Log.Info($"land: a plot paid for ({paid}) and never marked: refunded");
				return;
			}
			Building building = BuildingById(buildingId);
			bool flag = false;
			try
			{
				flag = building != null && building.isAlive() && !building.isRuin();
			}
			catch
			{
			}
			if (!flag)
			{
				Clear(host);
				try
				{
					host.addMoney(paid / 2);
				}
				catch
				{
				}
				if (tell != null)
				{
					tell("Your plot was lost. Half the gold is returned", arg2: false);
				}
				else
				{
					Tell("Your plot was lost. Half the gold is returned", cheer: false, host);
				}
				Log.Info("land: the site is gone");
			}
			else
			{
				bool flag2;
				try
				{
					flag2 = building.isUnderConstruction();
				}
				catch
				{
					return;
				}
				if (!flag2)
				{
					Finish(host, building, tier, paid, tell);
				}
			}
		}

		internal static void Finish(Actor you, Building b, int tier, int paid, Action<string, bool> tell = null)
		{
			int num = TierOf(b);
			for (int i = 0; i < 8; i++)
			{
				if (num >= tier)
				{
					break;
				}
				bool flag = false;
				try
				{
					flag = b.canBeUpgraded() && b.upgradeBuilding();
				}
				catch (Exception e)
				{
					Hub.Fail("land upgrade", e);
				}
				if (!flag)
				{
					break;
				}
				num = TierOf(b);
			}
			bool ruler = paid == PriceOf(tier, ruler: true) && paid != PriceOf(tier, ruler: false);
			if (num < tier)
			{
				int num2 = paid - PriceOf(Mathf.Max(0, num), ruler);
				if (num2 > 0)
				{
					try
					{
						you.addMoney(num2);
					}
					catch
					{
					}
				}
				Log.Info($"land: the house could only grow to tier {num} of {tier}: {num2} gold back");
			}
			Clear(you);
			HomeLife.MoveIn(you, b);
			string text = null;
			try
			{
				text = Rep.NameOf(b.city);
			}
			catch
			{
			}
			string text2 = "Your house is built" + ((text != null) ? (" in " + text) : "");
			if (tell != null)
			{
				tell(text2, arg2: true);
			}
			else
			{
				Tell(text2, cheer: true, you);
			}
			Log.Info($"land: {WorldBoxMod.Describe(you)}'s house stands ({b.asset.id}, tier {num})");
		}

		public static bool PromptAt(PromptAsk ask)
		{
			if (Aiming || ask?.Host == null)
			{
				return false;
			}
			Building building = PendingSite(ask.Host);
			if (building == null)
			{
				return false;
			}
			try
			{
				if ((ask.Picked != building && !((Pos(building) - ask.Pos).magnitude <= 2.6f)) || !building.isUnderConstruction())
				{
					return false;
				}
			}
			catch
			{
				return false;
			}
			int num = Mathf.Max(1, building.asset.construction_progress_needed);
			int num2 = Mathf.Clamp(building.getConstructionProgress() * 100 / num, 0, 99);
			ask.Prompt = $"Hold E:Build ({num2}%)";
			if (PlayInput.GetKey(KeyCode.E) && Time.unscaledTime - _helpAt > 0.35f)
			{
				_helpAt = Time.unscaledTime;
				if (!WorldSync.ForwardPlotHelp(ask.Host, building))
				{
					Help(ask.Host, building);
				}
				Sounds.PlayAt("event:/SFX/HIT/HitWood", Pos(building));
			}
			return true;
		}

		private static void Help(Actor you, Building site)
		{
			int num = Mathf.Max(1, site.asset.construction_progress_needed);
			try
			{
				site.updateBuild(Mathf.Max(2, Mathf.CeilToInt((float)num / 10f)));
			}
			catch (Exception e)
			{
				Hub.Fail("land help", e);
			}
			BuildHelp.Worked(you, site);
		}

		internal static bool HelpFor(Actor body, Building site)
		{
			try
			{
				if (site == null || PendingSite(body) != site || (Pos(site) - body.current_position).magnitude > 6f)
				{
					return false;
				}
			}
			catch
			{
				return false;
			}
			Help(body, site);
			return true;
		}

		private static Vector2 Pos(Building b)
		{
			WorldTile current_tile = b.current_tile;
			if (current_tile == null)
			{
				return ((BaseSimObject)b).cur_transform_position;
			}
			return new Vector2(current_tile.posV3.x, current_tile.posV3.y);
		}

		public static void Collect(TownScene s)
		{
			if (!Aiming || _aim == null || _house == null)
			{
				return;
			}
			BuildingAsset buildingAsset = AssetManager.buildings.get(_house);
			if (buildingAsset == null)
			{
				return;
			}
			string text = null;
			try
			{
				text = Models.BuildingFamily(buildingAsset);
			}
			catch
			{
			}
			MeshModel meshModel = Models.Get(text ?? _house) ?? Models.Get(_house);
			if (meshModel == null)
			{
				return;
			}
			float num = WidthOf(buildingAsset, s.Sprites) / Mathf.Max(0.5f, meshModel.InfoFloat("width", meshModel.Max.X - meshModel.Min.X));
			float x = _aim.posV3.x;
			float num2 = _aim.posV3.y;
			try
			{
				if (buildingAsset.fundament != null)
				{
					num2 += (float)(buildingAsset.fundament.top - buildingAsset.fundament.bottom) * 0.5f;
				}
			}
			catch
			{
			}
			Ghost.Model = meshModel;
			Ghost.Fade = 0.55f;
			Ghost.Flash = (_valid ? Green : Red);
			Ghost.NoShadow = true;
			Ghost.SortDepth = Mathf.Sqrt(s.Dist2(x, num2));
			Ghost.Place(x, num2, s.Ground(x, num2) - 0.05f, -MathF.PI / 2f, num, num, num);
			s.Meshes.Add(Ghost);
		}

		private static float WidthOf(BuildingAsset a, SpriteCache sprites)
		{
			try
			{
				if (a.building_sprites != null && a.building_sprites.animation_data != null && a.building_sprites.animation_data.Count > 0)
				{
					BuildingAnimationData buildingAnimationData = a.building_sprites.animation_data[0];
					if (buildingAnimationData != null && buildingAnimationData.main != null && buildingAnimationData.main.Length != 0 && buildingAnimationData.main[0] != null)
					{
						SpriteImage spriteImage = sprites.Get(buildingAnimationData.main[0]);
						if (spriteImage != null)
						{
							return spriteImage.UnitWidth * Mathf.Abs(a.scale_base.x);
						}
					}
				}
				if (a.fundament != null)
				{
					return a.fundament.width;
				}
			}
			catch
			{
			}
			return 3f;
		}

		internal static BuildingAsset SiteOf(City town)
		{
			return SiteOf(ArchOf(town));
		}

		internal static BuildingAsset SiteOf(ArchitectureAsset arch)
		{
			return Order(arch, "order_tent") ?? Order(arch, "order_house_0");
		}

		internal static BuildingAsset HouseOf(City town, int tier)
		{
			return Order(ArchOf(town), "order_house_" + Mathf.Clamp(tier, 0, 5));
		}

		internal static int TopTier(City town)
		{
			for (int num = 5; num > 0; num--)
			{
				if (HouseOf(town, num) != null)
				{
					return num;
				}
			}
			return 0;
		}

		private static ArchitectureAsset ArchOf(City town)
		{
			try
			{
				return town?.getActorAsset()?.architecture_asset;
			}
			catch
			{
				return null;
			}
		}

		private static BuildingAsset Order(ArchitectureAsset arch, string order)
		{
			if (arch?.building_ids_for_construction == null || !arch.building_ids_for_construction.TryGetValue(order, out var value))
			{
				return null;
			}
			if (!string.IsNullOrEmpty(value))
			{
				return AssetManager.buildings.get(value);
			}
			return null;
		}

		internal static int BestTier(City town)
		{
			int num = 0;
			try
			{
				foreach (Building building in town.buildings)
				{
					num = Mathf.Max(num, TierOf(building));
				}
			}
			catch
			{
			}
			return Mathf.Clamp(num, 0, 5);
		}

		internal static int TierOf(Building b)
		{
			try
			{
				string text = b?.asset?.id;
				if (text == null || !text.StartsWith("house_", StringComparison.Ordinal))
				{
					return -1;
				}
				char c = text[text.Length - 1];
				return (c >= '0' && c <= '9') ? (c - 48) : (-1);
			}
			catch
			{
				return -1;
			}
		}

		internal static Building PendingSite(Actor you)
		{
			if (!Read(you, out var _, out var buildingId, out var _, out var _))
			{
				return null;
			}
			Building building = BuildingById(buildingId);
			try
			{
				return (building != null && building.isAlive() && building.isUnderConstruction()) ? building : null;
			}
			catch
			{
				return null;
			}
		}

		private static Building BuildingById(long id)
		{
			try
			{
				return World.world.buildings.get(id);
			}
			catch
			{
				return null;
			}
		}

		private static void Save(Actor you, long cityId, long buildingId, int tier, int paid)
		{
			try
			{
				you.data.set("fp_plot", cityId + "|" + buildingId + "|" + tier + "|" + paid);
			}
			catch (Exception e)
			{
				Hub.Fail("land save", e);
			}
		}

		internal static bool Read(Actor you, out long cityId, out long buildingId, out int tier, out int paid)
		{
			cityId = (buildingId = 0L);
			tier = (paid = 0);
			string pResult = null;
			try
			{
				you?.data.get("fp_plot", out pResult, null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(pResult))
			{
				return false;
			}
			string[] array = pResult.Split('|');
			if (array.Length >= 4 && long.TryParse(array[0], out cityId) && long.TryParse(array[1], out buildingId) && int.TryParse(array[2], out tier))
			{
				return int.TryParse(array[3], out paid);
			}
			return false;
		}

		internal static void Clear(Actor you)
		{
			try
			{
				you.data.removeString("fp_plot");
			}
			catch
			{
			}
		}

		public static void OnBodyDying(Actor body)
		{
			_dyingPlot = null;
			_dyingHouse = HomeLife.OwnHouse(body);
			try
			{
				body.data.get("fp_plot", out _dyingPlot, null);
			}
			catch
			{
			}
		}

		public static void OnBodyDied(Actor body, Actor heir)
		{
			Building dyingHouse = _dyingHouse;
			string dyingPlot = _dyingPlot;
			_dyingHouse = null;
			_dyingPlot = null;
			if (heir == null)
			{
				return;
			}
			bool flag = false;
			try
			{
				flag = dyingHouse != null && dyingHouse.isAlive() && !dyingHouse.isRuin();
			}
			catch
			{
			}
			if (flag)
			{
				HomeLife.MoveIn(heir, dyingHouse);
				Log.Info("land: " + WorldBoxMod.Describe(heir) + " takes the house (" + dyingHouse.asset.id + ")");
			}
			if (!string.IsNullOrEmpty(dyingPlot))
			{
				try
				{
					heir.data.set("fp_plot", dyingPlot);
				}
				catch
				{
				}
				if (Read(heir, out var _, out var buildingId, out var _, out var _) && buildingId != -1)
				{
					Homes.SetOwner(BuildingById(buildingId), heir);
				}
			}
		}
	}
}
