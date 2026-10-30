using System;
using System.Collections.Generic;
using FirstPerson.Core;
using FirstPerson.Net;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal static class Planning
	{
		public static bool Open;

		public static bool Aiming;

		public static string Prompt;

		private const float Reach = 24f;

		private const float KnockWindow = 2.5f;

		private static Actor _for;

		private static City _town;

		private static int _cat;

		private static int _piece;

		private static int _face;

		private static int _dockTurn = -1;

		private static WorldTile _aim;

		private static WorldTile _site;

		private static string _why;

		private static readonly List<TileZone> Claims = new List<TileZone>();

		private static Building _knock;

		private static float _knockAt;

		private static bool _toldHow;

		private static bool _begun;

		internal static WorldTile AimForTest;

		internal static bool ClickForTest;

		internal static bool RightClickForTest;

		private const int SnapReach = 4;

		private static WorldTile _snapAim;

		private static WorldTile _snapSite;

		private static int _snapTurn;

		private static int _snapPlans;

		private static float _snapAt = -10f;

		private static readonly List<(WorldTile t, float d)> SnapSpots = new List<(WorldTile, float)>();

		private static WorldTile _dragFrom;

		private static WorldTile _dragAim;

		internal static readonly List<WorldTile> Row = new List<WorldTile>();

		internal static readonly List<bool> RowOk = new List<bool>();

		internal static bool DragHeldForTest;

		private const float LightReach = 9f;

		private static bool _lightsOnly;

		private static bool _toldLights;

		private static int _lightTurn;

		private static bool _lightAiming;

		private static bool _lightValid;

		private static bool _lightShown;

		private static Vec3 _lightAt;

		private static float _lightHeading;

		private static PlacedLights.WallSpot? _lightWall;

		private static PlacedLights.Light _aimedLight;

		internal static Vec3? LightPointForTest;

		internal static BaseSimObject LightPickedForTest;

		public static bool Founding { get; private set; }

		public static int Face
		{
			get
			{
				BuildingAsset buildingAsset = (Founding ? null : Asset);
				if (buildingAsset == null || !Facing.Turns(buildingAsset))
				{
					return 0;
				}
				if (!Facing.Quarter(buildingAsset))
				{
					return _face & 2;
				}
				return _face & 3;
			}
		}

		internal static bool Begun
		{
			get
			{
				if (_begun)
				{
					return Aiming;
				}
				return false;
			}
		}

		public static City Town => _town;

		internal static WorldTile SiteTile => _site;

		internal static bool Valid
		{
			get
			{
				if (_site != null)
				{
					return _why == null;
				}
				return false;
			}
		}

		internal static bool Nowhere { get; private set; }

		internal static string Why => _why;

		internal static List<TileZone> ClaimsNow => Claims;

		internal static int CategoryIndex => _cat;

		internal static int PieceIndex => _piece;

		public static PlanPieces.Category Category
		{
			get
			{
				if (!_lightsOnly)
				{
					return PlanPieces.All[Mathf.Clamp(_cat, 0, PlanPieces.All.Count - 1)];
				}
				return PlanPieces.Lights;
			}
		}

		public static PlanPieces.Piece Piece
		{
			get
			{
				List<PlanPieces.Piece> pieces = Category.Pieces;
				if (pieces.Count != 0)
				{
					return pieces[Mathf.Clamp(_piece, 0, pieces.Count - 1)];
				}
				return null;
			}
		}

		public static BuildingAsset Asset
		{
			get
			{
				if (!Founding)
				{
					return PlanPieces.AssetOf(Piece, _town);
				}
				return Village.BonfireOf(_for);
			}
		}

		internal static int DockTurn => _dockTurn;

		internal static bool Dragging
		{
			get
			{
				if (_dragFrom != null)
				{
					return Row.Count >= 2;
				}
				return false;
			}
		}

		internal static int StreetTilesWaiting
		{
			get
			{
				if (_town == null || _town.road_tiles_to_build == null)
				{
					return 0;
				}
				return _town.road_tiles_to_build.Count;
			}
		}

		public static bool LightsOnly
		{
			get
			{
				if (_lightsOnly)
				{
					return Aiming;
				}
				return false;
			}
		}

		internal static bool LightAiming
		{
			get
			{
				if (Begun)
				{
					return _lightAiming;
				}
				return false;
			}
		}

		internal static bool LightGhostShown => _lightShown;

		internal static bool LightValid => _lightValid;

		internal static Vec3 LightAt => _lightAt;

		internal static float LightHeading => _lightHeading;

		internal static PlacedLights.Kind LightKind
		{
			get
			{
				if (Piece == null || Piece.Light < 0)
				{
					return PlacedLights.Kind.Torch;
				}
				return (PlacedLights.Kind)Piece.Light;
			}
		}

		internal static PlacedLights.Light AimedLight
		{
			get
			{
				if (!LightAiming)
				{
					return null;
				}
				return _aimedLight;
			}
		}

		internal static bool PickForTest(string order)
		{
			for (int i = 0; i < PlanPieces.All.Count; i++)
			{
				for (int j = 0; j < PlanPieces.All[i].Pieces.Count; j++)
				{
					if (PlanPieces.All[i].Pieces[j].Order == order)
					{
						_cat = i;
						_piece = j;
						return true;
					}
				}
			}
			return false;
		}

		internal static bool PickSpecialForTest(bool street, int wall = -1)
		{
			for (int i = 0; i < PlanPieces.All.Count; i++)
			{
				for (int j = 0; j < PlanPieces.All[i].Pieces.Count; j++)
				{
					PlanPieces.Piece piece = PlanPieces.All[i].Pieces[j];
					if (street ? piece.Street : (piece.Wall == wall))
					{
						_cat = i;
						_piece = j;
						return true;
					}
				}
			}
			return false;
		}

		public static void Key(Actor host)
		{
			if (Aiming)
			{
				Close(null);
			}
			else if (host != null)
			{
				City city = TownFor(host);
				if (city != null)
				{
					Begin(host, city);
				}
				else if (Village.CanFound(host))
				{
					BeginFounding(host);
				}
				else
				{
					BeginLights(host);
				}
			}
		}

		private static City TownFor(Actor host)
		{
			City here = Hub.Here;
			if (here != null && Rep.Rules(host, here))
			{
				return here;
			}
			City result = null;
			float num = 1600f;
			foreach (City item in Hub.Near)
			{
				if (Rep.Rules(host, item))
				{
					float sqrMagnitude = (Shops.Centre(item) - host.current_position).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						result = item;
					}
				}
			}
			return result;
		}

		public static void Begin(Actor host, City town)
		{
			_for = host;
			_town = town;
			Founding = false;
			_lightsOnly = false;
			Aiming = true;
			_begun = true;
			_aim = (_site = null);
			_why = null;
			_knock = null;
			_dockTurn = -1;
			if (_cat >= PlanPieces.All.Count)
			{
				_cat = 0;
			}
			Log.Info(string.Format("planning: {0} ({1}, level {2})", Rep.NameOf(town), Village.PlansAlone(town) ? "planned alone" : "taking turns with the town", PlanPieces.LevelOf(town)));
			if (!_toldHow)
			{
				_toldHow = true;
				Hub.Toast?.Invoke("Planning " + Rep.NameOf(town) + ". Place plans; hammer them yourself, or the builders take them in turn");
			}
		}

		public static void BeginFounding(Actor host)
		{
			_for = host;
			_town = null;
			Founding = true;
			_lightsOnly = false;
			ClearLightAim();
			Aiming = true;
			_begun = true;
			_aim = (_site = null);
			_why = null;
			Hub.Toast?.Invoke("Choose where your village's bonfire burns");
			Log.Info("planning: founding a village");
		}

		public static bool Escape()
		{
			if (!Aiming)
			{
				return false;
			}
			Close(Founding ? "no village founded" : null);
			return true;
		}

		private static void Close(string why)
		{
			Aiming = false;
			Founding = false;
			_begun = false;
			Prompt = null;
			_site = (_aim = null);
			_knock = null;
			_dragFrom = (_dragAim = null);
			Row.Clear();
			RowOk.Clear();
			_lightsOnly = false;
			ClearLightAim();
			if (why != null)
			{
				Hub.Toast?.Invoke(char.ToUpperInvariant(why[0]) + why.Substring(1));
			}
		}

		public static void Choices(Conversation c, List<Choice> list)
		{
		}

		public static void Update(Actor host, float dt)
		{
			if (!Aiming || !_begun)
			{
				return;
			}
			if (host == null || host != _for || !host.isAlive())
			{
				Close(null);
				return;
			}
			if (_lightsOnly)
			{
				UpdateLightsOnly(host);
				return;
			}
			if (!Founding)
			{
				bool flag;
				try
				{
					flag = _town != null && _town.isAlive() && Rep.Rules(host, _town);
				}
				catch
				{
					flag = false;
				}
				if (!flag)
				{
					Close("the town is no longer yours to plan");
					return;
				}
				if ((Shops.Centre(_town) - host.current_position).sqrMagnitude > 8100f)
				{
					Close("you left " + Rep.NameOf(_town));
					return;
				}
			}
			else if (!Village.CanFound(host))
			{
				Close(null);
				return;
			}
			if (Founding && Hub.KeysFree && (PlayInput.GetKeyDown(KeyCode.Q) || PlayInput.GetKeyDown(KeyCode.E)))
			{
				BeginLights(host);
				Sounds.PlayUI("event:/SFX/Ui/Click");
				return;
			}
			if (Hub.KeysFree && !Founding)
			{
				Pick();
			}
			if (!Founding && Piece != null && Piece.Light >= 0)
			{
				UpdateLight(host);
				return;
			}
			ClearLightAim();
			if (Hub.KeysFree && !Founding && PlayInput.GetKeyDown(KeyCode.R))
			{
				Turn();
			}
			if (Hub.KeysFree && !Founding && _dragFrom == null && PlayInput.GetKeyDown(KeyCode.T))
			{
				BuildNext();
			}
			Aim(host);
			bool clickForTest = ClickForTest;
			bool flag2 = clickForTest || (Hub.KeysFree && PlayInput.GetMouseButtonDown(0));
			bool flag3 = RightClickForTest || (Hub.KeysFree && PlayInput.GetMouseButtonDown(1));
			ClickForTest = (RightClickForTest = false);
			if (_dragFrom != null)
			{
				if (flag3 || !Hub.KeysFree)
				{
					_dragFrom = null;
					Row.Clear();
					RowOk.Clear();
				}
				else
				{
					MakeRow();
					if (!DragHeldForTest && !PlayInput.GetMouseButton(0))
					{
						if (Row.Count >= 2)
						{
							PlaceRow(host);
						}
						else
						{
							Place(host);
						}
						_dragFrom = null;
						Row.Clear();
						RowOk.Clear();
					}
				}
			}
			else if (flag2 && !clickForTest && !Founding)
			{
				_dragFrom = _site ?? _aim;
				_dragAim = _aim;
				Row.Clear();
				RowOk.Clear();
			}
			else if (flag2)
			{
				Place(host);
			}
			else if (flag3 && !Founding)
			{
				TakeDown(host);
			}
			Prompt = MakePrompt();
		}

		private static void Pick()
		{
			int count = PlanPieces.All.Count;
			if (PlayInput.GetKeyDown(KeyCode.Q))
			{
				_cat = (_cat + count - 1) % count;
				_piece = 0;
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
			if (PlayInput.GetKeyDown(KeyCode.E))
			{
				_cat = (_cat + 1) % count;
				_piece = 0;
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
			int count2 = Category.Pieces.Count;
			if (count2 == 0)
			{
				return;
			}
			for (int i = 0; i < 8 && i < count2; i++)
			{
				if (PlayInput.GetKeyDown((KeyCode)(49 + i)))
				{
					_piece = i;
					Sounds.PlayUI("event:/SFX/Ui/Click");
				}
			}
			float num = 0f;
			try
			{
				num = Input.mouseScrollDelta.y;
			}
			catch
			{
			}
			if (num > 0.1f)
			{
				_piece = (_piece + count2 - 1) % count2;
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
			else if (num < -0.1f)
			{
				_piece = (_piece + 1) % count2;
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
		}

		private static void Turn()
		{
			BuildingAsset asset = Asset;
			if (asset != null)
			{
				if (asset.docks)
				{
					Vector2 vector = ((_site != null) ? DockMiddle(_site, asset) : Vector2.zero);
					int num = ((_site != null) ? DockFacing.QuarterOf(DockFacing.TowardsWater(vector.x, vector.y)) : 0);
					int num2 = (((_dockTurn >= 0) ? _dockTurn : num) + 1) & 3;
					_dockTurn = ((num2 == num) ? (-1) : num2);
					Sounds.PlayUI("event:/SFX/Ui/Click");
				}
				else if (!Facing.Turns(asset))
				{
					Hub.Toast?.Invoke((Piece != null) ? (Piece.Name + " faces one way only") : "This faces one way only");
				}
				else
				{
					_face = Facing.Next(asset, Face);
					Sounds.PlayUI("event:/SFX/Ui/Click");
				}
			}
		}

		internal static float DockHeading(WorldTile site, BuildingAsset a)
		{
			if (_dockTurn >= 0)
			{
				return Facing.Heading(_dockTurn);
			}
			Vector2 vector = DockMiddle(site, a);
			return DockFacing.TowardsWater(vector.x, vector.y);
		}

		private static Vector2 DockMiddle(WorldTile site, BuildingAsset a)
		{
			BuildingFundament fundament = a.fundament;
			return new Vector2((float)site.x + 0.5f, (float)site.y + 0.5f + ((fundament != null) ? ((float)(fundament.top - fundament.bottom) * 0.5f) : 0f));
		}

		private static void SnapDock(BuildingAsset a)
		{
			WorldTile worldTile = null;
			float unscaledTime = Time.unscaledTime;
			int num = Blueprints.CountOf(_town);
			if (_snapAim == _aim && _snapTurn == _dockTurn && _snapPlans == num && unscaledTime - _snapAt < 0.5f)
			{
				worldTile = _snapSite;
			}
			else
			{
				Vector2 vector = new Vector2((float)_aim.x + 0.5f, (float)_aim.y + 0.5f);
				SnapSpots.Clear();
				for (int i = -4; i <= 4; i++)
				{
					for (int j = -4; j <= 4; j++)
					{
						WorldTile tile = World.world.GetTile(_site.x + j, _site.y + i);
						if (tile != null)
						{
							SnapSpots.Add((tile, (DockMiddle(tile, a) - vector).sqrMagnitude));
						}
					}
				}
				SnapSpots.Sort(((WorldTile t, float d) p, (WorldTile t, float d) q) => p.d.CompareTo(q.d));
				List<TileZone> claims = new List<TileZone>();
				foreach (var snapSpot in SnapSpots)
				{
					var (worldTile2, _) = snapSpot;
					if (snapSpot.d > 20.25f)
					{
						break;
					}
					if (Blueprints.WhyNotHere(_town, a, worldTile2, claims) == null && PierRunsOut(worldTile2, a) && !(SiteLevel.Unevenness(Blueprints.Footprint(worldTile2, a)) > 1f))
					{
						worldTile = worldTile2;
						break;
					}
				}
				_snapAim = _aim;
				_snapTurn = _dockTurn;
				_snapPlans = num;
				_snapSite = worldTile;
				_snapAt = unscaledTime;
			}
			if (worldTile != null)
			{
				_site = worldTile;
				_why = Blueprints.WhyNotHere(_town, a, worldTile, Claims);
			}
		}

		private static bool PierRunsOut(WorldTile site, BuildingAsset a)
		{
			Vector2 vector = DockMiddle(site, a);
			return DockFacing.RunsOverWater(vector.x, vector.y, DockHeading(site, a), (a.fundament != null) ? ((float)a.fundament.width) : 3f);
		}

		private static void Aim(Actor host)
		{
			_aim = (_site = null);
			_why = "Look at the ground";
			Nowhere = false;
			Vec3 point;
			Actor creature;
			if (AimForTest != null)
			{
				_aim = AimForTest;
			}
			else if (FirstPersonAim.Ready(host) && FirstPersonAim.Point(out point, out creature))
			{
				if ((new Vector2(point.X, point.Y) - host.current_position).magnitude <= 24f)
				{
					_aim = World.world.GetTile(Mathf.FloorToInt(point.X), Mathf.FloorToInt(point.Y));
				}
				else
				{
					_why = "Too far";
				}
			}
			if (_aim == null)
			{
				return;
			}
			if (!Founding && Piece != null && Piece.Street)
			{
				_site = _aim;
				_why = StreetWhy(_aim);
				return;
			}
			if (!Founding && Piece != null && Piece.Wall >= 0)
			{
				_site = _aim;
				_why = PlanPieces.WhyNot(Piece, _town) ?? WallPlans.WhyNotAt(_town, _aim);
				return;
			}
			BuildingAsset asset = Asset;
			if (asset == null)
			{
				_why = "Nothing to place";
				return;
			}
			int num = ((asset.fundament != null) ? (asset.fundament.top / 2) : 0);
			_site = World.world.GetTile(_aim.x, _aim.y - num) ?? _aim;
			if (Founding)
			{
				_why = Village.WhyNotFoundAt(host, _site);
				return;
			}
			_why = PlanPieces.WhyNot(Piece, _town);
			if (_why == null && Blueprints.CountOf(_town) >= 12)
			{
				_why = 12 + " plans are waiting";
			}
			Nowhere = _why != null;
			if (_why == null)
			{
				_why = Blueprints.WhyNotHere(_town, asset, _site, Claims);
			}
			if (_why == null && asset.docks && !PierRunsOut(_site, asset))
			{
				_why = "The pier would run aground";
			}
			if (_why == null)
			{
				float num2 = SiteLevel.Unevenness(Blueprints.Footprint(_site, asset));
				if (num2 > 1f)
				{
					_why = $"Not level ({num2:0.0} across)";
				}
			}
			if (_why != null && asset.docks && !Nowhere && _dockTurn < 0)
			{
				SnapDock(asset);
			}
			if (_why == null && !Village.PlansAlone(_town) && !Blueprints.CanPay(_town, asset.cost, host, out var missing))
			{
				_why = "Needs " + missing;
			}
		}

		private static string MakePrompt()
		{
			if (Founding)
			{
				return ((_why == null) ? "LMB:Light the bonfire here" : _why) + "|Q/E:Lights|Esc:Cancel";
			}
			if (_dragFrom != null && Row.Count >= 2)
			{
				int num = 0;
				foreach (bool item in RowOk)
				{
					if (item)
					{
						num++;
					}
				}
				if (Piece != null && Piece.Street)
				{
					return ((num > 0) ? ("Release:Lay out " + num + " tiles of street") : "No tile of this can be paved") + "|RMB:Cancel";
				}
				if (Piece != null && Piece.Wall >= 0)
				{
					return ((num > 0) ? ("Release:Lay out " + num + " of " + Piece.Name.ToLowerInvariant() + " (" + WallPlans.Cost(Piece.Wall, num) + ")") : "Nowhere on this line can take a wall") + "|RMB:Cancel";
				}
				return ((num > 0) ? ("Release:Place " + num + " (" + CostTimes(Asset, num) + ")") : "Nowhere in this row can take one") + "|R:Turn|RMB:Cancel";
			}
			if (Piece != null && Piece.Street)
			{
				return ((_why == null) ? "LMB:Lay out a street (drag along it)" : _why) + "|RMB:Remove|Q/E:Kind|Y:Done";
			}
			if (Piece != null && Piece.Wall >= 0)
			{
				return ((_why == null) ? ("LMB:Lay out a " + Piece.Name.ToLowerInvariant() + " (drag along it, " + WallPlans.Cost(Piece.Wall, 1) + " a tile)") : _why) + "|RMB:Remove|Q/E:Kind|Y:Done";
			}
			PlanPieces.Piece piece = Piece;
			BuildingAsset asset = Asset;
			return ((_why == null && piece != null) ? ("LMB:Place " + piece.Name + " (" + PlanPieces.CostText(asset) + ")" + ((Claims.Count > 0) ? ", claims land" : "")) : _why) + "|R:Turn|T:Build next|RMB:Remove|Q/E:Kind|Y:Done";
		}

		internal static void BeginDragForTest()
		{
			_dragFrom = _site ?? _aim;
			_dragAim = _aim;
			Row.Clear();
			RowOk.Clear();
		}

		private static void MakeRow()
		{
			Row.Clear();
			RowOk.Clear();
			if (Piece != null && (Piece.Street || Piece.Wall >= 0))
			{
				MakeStreet();
				return;
			}
			BuildingAsset asset = Asset;
			if (asset == null || _dragFrom == null || _aim == null || _dragAim == null)
			{
				return;
			}
			int num = _aim.x - _dragAim.x;
			int num2 = _aim.y - _dragAim.y;
			BuildingFundament fundament = asset.fundament;
			int num3 = ((fundament == null) ? 1 : (fundament.left + fundament.right + 1));
			int num4 = ((fundament == null) ? 1 : (fundament.top + fundament.bottom + 1));
			bool flag = Mathf.Abs(num) >= Mathf.Abs(num2);
			int num5 = (flag ? (num3 + 1) : (num4 + 2));
			int num6 = (flag ? num : num2);
			int num7 = Mathf.Abs(num6) / num5 + 1;
			if (num7 < 2)
			{
				return;
			}
			int num8 = 12 - Blueprints.CountOf(_town);
			PlanPieces.Piece piece = Piece;
			if (piece != null && piece.Limit > 0)
			{
				int num9 = 0;
				try
				{
					num9 = _town.countBuildingsType(asset.type, pCountOnlyFinished: false);
				}
				catch
				{
				}
				num8 = Mathf.Min(num8, piece.Limit - num9);
			}
			num7 = Mathf.Min(num7, Mathf.Max(1, num8) + 4);
			int num10 = ((num6 >= 0) ? 1 : (-1));
			int num11 = 0;
			bool flag2 = PlanPieces.WhyNot(piece, _town) == null;
			List<TileZone> list = new List<TileZone>();
			for (int i = 0; i < num7; i++)
			{
				WorldTile tile = World.world.GetTile(_dragFrom.x + (flag ? (num10 * i * num5) : 0), _dragFrom.y + ((!flag) ? (num10 * i * num5) : 0));
				if (tile != null)
				{
					list.Clear();
					bool flag3 = flag2 && num11 < num8 && Blueprints.WhyNotHere(_town, asset, tile, list) == null && (!asset.docks || PierRunsOut(tile, asset)) && SiteLevel.Unevenness(Blueprints.Footprint(tile, asset)) <= 1f;
					if (flag3)
					{
						num11++;
					}
					Row.Add(tile);
					RowOk.Add(flag3);
				}
			}
		}

		private static void MakeStreet()
		{
			if (_dragAim == null || _aim == null)
			{
				return;
			}
			int num = _aim.x - _dragAim.x;
			int num2 = _aim.y - _dragAim.y;
			bool flag = Mathf.Abs(num) >= Mathf.Abs(num2);
			int num3 = (flag ? num : num2);
			int num4 = Mathf.Min(Mathf.Abs(num3) + 1, 60);
			int num5 = ((num3 >= 0) ? 1 : (-1));
			if (num4 < 2)
			{
				return;
			}
			for (int i = 0; i < num4; i++)
			{
				WorldTile tile = World.world.GetTile(_dragAim.x + (flag ? (num5 * i) : 0), _dragAim.y + ((!flag) ? (num5 * i) : 0));
				if (tile != null)
				{
					Row.Add(tile);
					RowOk.Add((Piece.Wall < 0) ? (StreetWhy(tile) == null) : (PlanPieces.WhyNot(Piece, _town) == null && WallPlans.WhyNotAt(_town, tile) == null));
				}
			}
		}

		private static string StreetWhy(WorldTile t)
		{
			return StreetWhy(_town, t);
		}

		internal static string StreetWhy(City town, WorldTile t)
		{
			try
			{
				if (t == null || t.Type == null)
				{
					return "Look at the ground";
				}
				if (t.Type.liquid)
				{
					return "Not on water";
				}
				if (t.Type.road)
				{
					return "A road already";
				}
				if (!t.Type.ground || t.Type.block)
				{
					return "Can't be paved";
				}
				if (t.zone == null || t.zone.city != town)
				{
					return "Not your town's land";
				}
				if (t.building != null && AssetStyle.IsSolid(t.building))
				{
					return "Something stands here";
				}
				if (town.road_tiles_to_build != null && town.road_tiles_to_build.Contains(t))
				{
					return "Laid out already";
				}
				if (WallPlans.Pending(town, t, out var _))
				{
					return "A wall is planned here";
				}
			}
			catch
			{
				return "Can't be paved";
			}
			return null;
		}

		private static void LayStreet(List<WorldTile> tiles)
		{
			if (tiles.Count == 0)
			{
				Hub.Toast?.Invoke(_why ?? "No tile of this can be paved");
				return;
			}
			if (WorldSync.ForwardLayout(_for, _town, -1, tiles))
			{
				Sounds.PlayUI("event:/SFX/Ui/Click");
				return;
			}
			try
			{
				_town.addRoads(tiles);
			}
			catch (Exception e)
			{
				Hub.Fail("street", e);
				return;
			}
			WorldTile worldTile = tiles[0];
			Sounds.PlayAt("event:/SFX/CIVILIZATIONS/BuildRoad", new Vector2(worldTile.posV3.x, worldTile.posV3.y));
			Hub.Toast?.Invoke(tiles.Count + " tile" + ((tiles.Count == 1) ? "" : "s") + " of street laid out. The road builders pave them");
			Log.Info("planning: a street of " + tiles.Count + " tiles in " + Rep.NameOf(_town));
		}

		private static void LayWall(List<WorldTile> tiles, int kind)
		{
			if (tiles.Count > 0 && WorldSync.ForwardLayout(_for, _town, kind, tiles))
			{
				Sounds.PlayUI("event:/SFX/Ui/Click");
				return;
			}
			int num = ((tiles.Count != 0) ? WallPlans.Add(_town, tiles, kind) : 0);
			if (num == 0)
			{
				Hub.Toast?.Invoke(_why ?? "Nowhere on this line can take a wall");
				return;
			}
			WorldTile worldTile = tiles[0];
			Sounds.PlayAt("event:/SFX/BUILDINGS/SpawnBuildingWood", new Vector2(worldTile.posV3.x, worldTile.posV3.y));
			Hub.Toast?.Invoke(num + " tile" + ((num == 1) ? "" : "s") + " of " + WallPlans.Names[kind].ToLowerInvariant() + " laid out. Raise them with a hammer, or the town does when it has no building to put up");
		}

		private static void PlaceRow(Actor host)
		{
			if (Piece != null && (Piece.Street || Piece.Wall >= 0))
			{
				List<WorldTile> list = new List<WorldTile>();
				for (int i = 0; i < Row.Count; i++)
				{
					if (RowOk[i])
					{
						list.Add(Row[i]);
					}
				}
				if (Piece.Wall >= 0)
				{
					LayWall(list, Piece.Wall);
				}
				else
				{
					LayStreet(list);
				}
				return;
			}
			PlanPieces.Piece piece = Piece;
			BuildingAsset asset = Asset;
			if (piece == null || asset == null)
			{
				return;
			}
			if (WorldSync.Mirroring)
			{
				List<WorldTile> list2 = new List<WorldTile>();
				for (int j = 0; j < Row.Count; j++)
				{
					if (RowOk[j])
					{
						list2.Add(Row[j]);
					}
				}
				if (list2.Count == 0)
				{
					Hub.Toast?.Invoke("Nothing in that row could stand");
				}
				else if (WorldSync.ForwardPlans(host, _town, piece.Order, Face, list2))
				{
					Sounds.PlayAt("event:/SFX/BUILDINGS/SpawnBuildingWood", new Vector2(list2[0].posV3.x, list2[0].posV3.y));
				}
				return;
			}
			int num = 0;
			string text = null;
			for (int k = 0; k < Row.Count; k++)
			{
				if (!RowOk[k])
				{
					continue;
				}
				if (Blueprints.CountOf(_town) >= 12)
				{
					text = 12 + " plans are waiting";
					break;
				}
				if (!Village.PlansAlone(_town) && !Blueprints.CanPay(_town, asset.cost, host, out var missing))
				{
					text = "Needs " + missing;
					break;
				}
				bool home = piece.Home && FirstHome(host, _town);
				Building building = Blueprints.PlaceSite(_town, asset, Row[k], host, home);
				if (building != null)
				{
					Facing.Set(building, Face);
					if (asset.docks)
					{
						Facing.SetDock(building, _dockTurn);
					}
					SiteLevel.Begin(building);
					num++;
				}
			}
			if (num > 0)
			{
				WorldTile worldTile = Row[0];
				Sounds.PlayAt("event:/SFX/BUILDINGS/SpawnBuildingWood", new Vector2(worldTile.posV3.x, worldTile.posV3.y));
			}
			string text2 = piece.Name.ToLowerInvariant();
			Hub.Toast?.Invoke((num == 0) ? (text ?? "Nothing in that row could stand") : (num + " " + text2 + ((num == 1) ? "" : "s") + " planned" + ((text != null) ? (". " + text) : "")));
			Log.Info("planning: a row of " + num + " " + asset.id + " (of " + Row.Count + ")" + ((text != null) ? (", " + text) : ""));
		}

		private static string CostTimes(BuildingAsset a, int n)
		{
			ConstructionCost constructionCost = a?.cost;
			if (constructionCost == null)
			{
				return "Free";
			}
			List<string> list = new List<string>(4);
			if (constructionCost.wood > 0)
			{
				list.Add(constructionCost.wood * n + " wood");
			}
			if (constructionCost.stone > 0)
			{
				list.Add(constructionCost.stone * n + " stone");
			}
			if (constructionCost.common_metals > 0)
			{
				list.Add(constructionCost.common_metals * n + " metal");
			}
			if (constructionCost.gold > 0)
			{
				list.Add(constructionCost.gold * n + " gold");
			}
			if (list.Count != 0)
			{
				return string.Join(", ", list.ToArray());
			}
			return "Free";
		}

		private static void Place(Actor host)
		{
			if (_site == null || _why != null)
			{
				if (_why != null)
				{
					Hub.Toast?.Invoke(_why);
				}
				return;
			}
			if (Founding && WorldSync.Mirroring)
			{
				if (!WorldSync.ForwardFound(host, _site))
				{
					return;
				}
				Close(null);
				try
				{
					WorldBoxMod.Instance?.PlayHorn();
					return;
				}
				catch
				{
					return;
				}
			}
			if (Founding)
			{
				City city = FamilyFollow.SettleHere(host, Hub.Toast, _site);
				if (city != null)
				{
					Close(null);
					try
					{
						WorldBoxMod.Instance?.PlayHorn();
					}
					catch
					{
					}
					Hub.Toast?.Invoke("You founded " + city.name + ". Build the bonfire: hold the left button at it, with a hammer or bare hands");
				}
				return;
			}
			PlanPieces.Piece piece = Piece;
			if (piece != null && piece.Street)
			{
				if (_site != null && _why == null)
				{
					LayStreet(new List<WorldTile> { _site });
				}
				return;
			}
			if (piece != null && piece.Wall >= 0)
			{
				if (_site != null && _why == null)
				{
					LayWall(new List<WorldTile> { _site }, piece.Wall);
				}
				else if (_why != null)
				{
					Hub.Toast?.Invoke(_why);
				}
				return;
			}
			BuildingAsset asset = Asset;
			if (WorldSync.ForwardPlans(host, _town, piece.Order, Face, new List<WorldTile> { _site }))
			{
				Sounds.PlayAt("event:/SFX/BUILDINGS/SpawnBuildingWood", new Vector2(_site.posV3.x, _site.posV3.y));
				return;
			}
			bool flag = piece.Home && FirstHome(host, _town);
			Building building = Blueprints.PlaceSite(_town, asset, _site, host, flag);
			if (building == null)
			{
				Hub.Toast?.Invoke("It can't stand there");
				return;
			}
			Facing.Set(building, Face);
			if (asset.docks)
			{
				Facing.SetDock(building, _dockTurn);
			}
			SiteLevel.Begin(building);
			Sounds.PlayAt("event:/SFX/BUILDINGS/SpawnBuildingWood", new Vector2(_site.posV3.x, _site.posV3.y));
			if (flag)
			{
				Hub.Toast?.Invoke("Your house is planned. It's yours when it stands");
			}
		}

		internal static bool FirstHome(Actor you, City town)
		{
			try
			{
				Building building = HomeLife.OwnHouse(you);
				if (building != null && building.isAlive() && building.city == town)
				{
					return false;
				}
			}
			catch
			{
			}
			return !Blueprints.HomePlanned(town, you);
		}

		private static Building Aimed()
		{
			Building building = null;
			try
			{
				building = ((_aim != null) ? _aim.building : null);
			}
			catch
			{
			}
			if (building != null && (building.asset == null || building.city != _town || building.asset.building_type == BuildingType.Building_Tree))
			{
				building = null;
			}
			if (building == null && _aim != null)
			{
				foreach (Building item in Blueprints.SitesOf(_town))
				{
					if (Blueprints.DistanceTo(item, new Vector2((float)_aim.x + 0.5f, (float)_aim.y + 0.5f)) < 0.5f)
					{
						building = item;
						break;
					}
				}
			}
			return building;
		}

		private static void BuildNext()
		{
			Building building = Aimed();
			if (building == null || !building.isUnderConstruction())
			{
				Hub.Toast?.Invoke("Look at a plan to build it next");
				return;
			}
			string text = building.asset.id;
			PlanPieces.Piece piece = null;
			try
			{
				piece = PlanPieces.OfOrder(building.asset.group);
			}
			catch
			{
			}
			if (piece != null)
			{
				text = piece.Name;
			}
			if (WorldSync.ForwardBuildNext(_for, building))
			{
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
			else if (Blueprints.BuildNext(building))
			{
				Hub.Toast?.Invoke(text + " is next");
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
		}

		private static void TakeDown(Actor host)
		{
			if (_aim != null && WallPlans.Pending(_town, _aim, out var _) && WorldSync.ForwardUnwall(host, _town, _aim))
			{
				return;
			}
			if (_aim != null && WallPlans.Pending(_town, _aim, out var kind2) && WallPlans.Remove(_town, _aim))
			{
				Hub.Toast?.Invoke(WallPlans.Names[kind2] + " plan removed");
				return;
			}
			Building building = Aimed();
			if (building == null)
			{
				Hub.Toast?.Invoke("Look at a plan or a building of " + Rep.NameOf(_town));
				return;
			}
			if (building.isUnderConstruction())
			{
				bool flag = Blueprints.Refunds(building);
				if (!WorldSync.ForwardRemovePlan(host, building) && Blueprints.Remove(building, host))
				{
					Hub.Toast?.Invoke("Plan removed" + (flag ? ", its cost given back" : ""));
				}
				return;
			}
			string text = Blueprints.WhyNotKnockDown(building);
			if (text != null)
			{
				Hub.Toast?.Invoke(text);
				return;
			}
			float unscaledTime = Time.unscaledTime;
			if (_knock == building && unscaledTime - _knockAt <= 2.5f)
			{
				_knock = null;
				if (!WorldSync.ForwardKnockDown(host, building) && Blueprints.KnockDown(building, host))
				{
					Hub.Toast?.Invoke("Knocked down. Half its cost is back in the stockpile");
				}
			}
			else
			{
				_knock = building;
				_knockAt = unscaledTime;
				Hub.Toast?.Invoke(Blueprints.KnockPrompt(building));
			}
		}

		internal static bool PickLightForTest(PlacedLights.Kind k)
		{
			PlanPieces.Category lights = PlanPieces.Lights;
			for (int i = 0; i < lights.Pieces.Count; i++)
			{
				if (lights.Pieces[i].Light == (int)k)
				{
					_piece = i;
					if (!_lightsOnly)
					{
						_cat = PlanPieces.All.IndexOf(lights);
					}
					return true;
				}
			}
			return false;
		}

		public static void BeginLights(Actor host)
		{
			_for = host;
			_town = null;
			Founding = false;
			_lightsOnly = true;
			Aiming = true;
			_begun = true;
			_aim = (_site = null);
			_why = null;
			_knock = null;
			if (_piece >= PlanPieces.Lights.Pieces.Count)
			{
				_piece = 0;
			}
			Log.Info("planning: lights");
			if (!_toldLights)
			{
				_toldLights = true;
				Hub.Toast?.Invoke("Set down lights from your bag, anywhere on dry land. Buildings are planned in a town you lead");
			}
		}

		private static void ClearLightAim()
		{
			_lightAiming = (_lightValid = (_lightShown = false));
			_lightWall = null;
			_aimedLight = null;
		}

		private static void UpdateLightsOnly(Actor host)
		{
			if (Hub.KeysFree && (PlayInput.GetKeyDown(KeyCode.Q) || PlayInput.GetKeyDown(KeyCode.E)) && Village.CanFound(host))
			{
				ClearLightAim();
				_lightsOnly = false;
				BeginFounding(host);
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
			else
			{
				if (Hub.KeysFree)
				{
					PickPiece(PlanPieces.Lights.Pieces.Count);
				}
				UpdateLight(host);
			}
		}

		private static void PickPiece(int n)
		{
			if (n == 0)
			{
				return;
			}
			for (int i = 0; i < 8 && i < n; i++)
			{
				if (PlayInput.GetKeyDown((KeyCode)(49 + i)))
				{
					_piece = i;
					Sounds.PlayUI("event:/SFX/Ui/Click");
				}
			}
			float num = 0f;
			try
			{
				num = Input.mouseScrollDelta.y;
			}
			catch
			{
			}
			if (num > 0.1f)
			{
				_piece = (_piece + n - 1) % n;
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
			else if (num < -0.1f)
			{
				_piece = (_piece + 1) % n;
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
		}

		private static void UpdateLight(Actor host)
		{
			_lightAiming = true;
			_dragFrom = (_dragAim = null);
			Row.Clear();
			RowOk.Clear();
			PlacedLights.Kind lightKind = LightKind;
			if (Hub.KeysFree && PlayInput.GetKeyDown(KeyCode.R))
			{
				if (lightKind == PlacedLights.Kind.Wall)
				{
					Hub.Toast?.Invoke("A wall torch faces out from its wall");
				}
				else
				{
					_lightTurn = (_lightTurn + 1) & 7;
					Sounds.PlayUI("event:/SFX/Ui/Click");
				}
			}
			AimLight(host, lightKind);
			bool num = ClickForTest || (Hub.KeysFree && PlayInput.GetMouseButtonDown(0));
			bool flag = RightClickForTest || (Hub.KeysFree && PlayInput.GetMouseButtonDown(1));
			ClickForTest = (RightClickForTest = false);
			if (num)
			{
				if (!_lightValid)
				{
					Hub.Toast?.Invoke(_why ?? "It can't go there");
				}
				else
				{
					string text = PlacedLights.Place(host, lightKind, new Vector2(_lightAt.X, _lightAt.Y), _lightHeading, _lightWall);
					if (text != null)
					{
						Hub.Toast?.Invoke(text);
					}
					else
					{
						AimLight(host, lightKind);
					}
				}
			}
			else if (flag)
			{
				if (_aimedLight == null)
				{
					Hub.Toast?.Invoke("Look at a light to take it back");
				}
				else
				{
					PlacedLights.Light aimedLight = _aimedLight;
					string text2 = PlacedLights.TakeBack(host, aimedLight);
					if (text2 != null)
					{
						Hub.Toast?.Invoke(text2);
					}
					else
					{
						Hub.Toast?.Invoke(PlacedLights.Names[(int)aimedLight.Kind] + " taken back");
						Sounds.PlayUI("event:/SFX/Ui/Click");
						_aimedLight = null;
					}
				}
			}
			Prompt = LightPrompt(lightKind);
		}

		private static void AimLight(Actor host, PlacedLights.Kind k)
		{
			_lightValid = (_lightShown = false);
			_lightWall = null;
			_aimedLight = null;
			_why = "Look at the ground";
			TerrainCache terrain = FirstPersonAim.Terrain;
			Func<float, float, float> func = (float x, float y) => (terrain == null) ? 0f : terrain.SampleHeight(x, y);
			Vec3 point = default(Vec3);
			Vec3 vec = FirstPersonAim.Eye;
			Vec3 ray = FirstPersonAim.Ray;
			BaseSimObject picked = null;
			bool flag = FirstPersonAim.Ready(host);
			if (LightPointForTest.HasValue)
			{
				point = LightPointForTest.Value;
				picked = LightPickedForTest;
				Vector2 current_position = host.current_position;
				vec = new Vec3(current_position.x, current_position.y, func(current_position.x, current_position.y) + 0.8f);
				ray = (point - vec).Normalized();
				flag = true;
			}
			if (flag)
			{
				_aimedLight = PlacedLights.Aimed(vec, ray, 11f, func);
			}
			if (!LightPointForTest.HasValue)
			{
				if (!flag || !FirstPersonAim.Point(out point, out var _))
				{
					return;
				}
				picked = FirstPersonAim.Picked;
			}
			Vector2 vector = new Vector2(point.X, point.Y);
			if ((vector - host.current_position).magnitude > 9f)
			{
				_why = "Too far";
				return;
			}
			if (k == PlacedLights.Kind.Wall)
			{
				if (!PlacedLights.WallAt(vec, ray, point, picked, func, out var spot, out var why))
				{
					_why = why;
					return;
				}
				_lightWall = spot;
				_lightAt = spot.At;
				_lightHeading = spot.Heading;
				_lightShown = true;
				_why = PlacedLights.Lacks(host, k) ?? why;
			}
			else
			{
				_lightAt = new Vec3(point.X, point.Y, func(point.X, point.Y) - 0.02f);
				_lightHeading = FirstPersonAim.Yaw + MathF.PI + (float)_lightTurn * MathF.PI * 0.25f;
				_lightShown = true;
				_why = PlacedLights.Lacks(host, k) ?? PlacedLights.WhyNotAt(vector, k);
			}
			_lightValid = _why == null;
		}

		private static string LightPrompt(PlacedLights.Kind k)
		{
			string text = PlacedLights.Names[(int)k];
			int n = PlacedLights.Carried(_for, k);
			Supplies.Kind kind = Supplies.Of(PlacedLights.ItemOf(k));
			string text2 = (_lightValid ? ("LMB:Set down a " + text.ToLowerInvariant() + " (" + ((kind != null) ? Supplies.Counted(kind, n) : n.ToString()) + " in your bag)") : (_why ?? "Look at the ground"));
			string text3 = ((_aimedLight == null) ? "" : (PlacedLights.MayTake(_for, _aimedLight) ? ("|RMB:Take back the " + PlacedLights.Names[(int)_aimedLight.Kind].ToLowerInvariant()) : "|Not yours to take"));
			string text4 = ((k == PlacedLights.Kind.Wall) ? "" : "|R:Turn");
			string text5 = ((!_lightsOnly) ? "|Q/E:Kind" : (Village.CanFound(_for) ? "|Q/E:Found a village" : ""));
			return text2 + text4 + text3 + text5 + "|Y:Done";
		}
	}
}
