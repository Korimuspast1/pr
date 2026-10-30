using System;
using System.Collections.Generic;
using System.Diagnostics;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class HouseInterior
	{
		private const float WalkSpeed = 1.7f;

		private const float BodyRadius = 0.14f;

		private const float PieceRadius = 0.12f;

		internal bool OutdoorsGathered;

		public bool HasBench;

		public float BenchX;

		public float BenchY;

		public string BenchNote;

		public string Name;

		public Vector2 Eye;

		public readonly List<BaseSimObject> Tagged = new List<BaseSimObject>();

		private readonly List<Actor> _occupants = new List<Actor>();

		private RoomWalkers _walkers = new RoomWalkers();

		private readonly List<RoomSeat> _seats = new List<RoomSeat>();

		private Actor _seatHost;

		private float _seatTimer;

		private Actor _body;

		internal static bool Shelter = true;

		private Vector2? _testOffset;

		private static readonly float[] Deflect = new float[4] { 0.6f, -0.6f, 1.2f, -1.2f };

		private const float DoorAimReach = 2.4f;

		private const float PushReach = 0.85f;

		private float _closedAt = -9f;

		private static readonly HashSet<long> _propped = new HashSet<long>();

		private static object _proppedWorld;

		private bool _doorWanted = true;

		private Building _doorFor;

		private Vector2 _walkWay;

		private float _walkAt = -9f;

		internal static int DoorToggles;

		private HouseWorld.Placement _place;

		internal static bool UseFitted = true;

		private const float LeaveAhead = 2.5f;

		private const float DoorReach = 1.4f;

		private const float WalkerReach = 0.8f;

		private const float DoorSwing = 1.55f;

		internal static int BackdropFrames;

		public bool Inside { get; private set; }

		public Building House { get; private set; }

		public Interior Room { get; private set; }

		public bool Virtual { get; private set; }

		internal IReadOnlyList<RoomWalkers.Walker> WalkersForTest => _walkers.All;

		internal RoomWalkers Walkers => _walkers;

		public bool NearDoor
		{
			get
			{
				if (Inside)
				{
					return (Eye - new Vector2(Room.DoorX, Room.DoorY)).magnitude < 1.1f;
				}
				return false;
			}
		}

		internal IEnumerable<Actor> WalkersAwake
		{
			get
			{
				foreach (RoomWalkers.Walker item in _walkers.All)
				{
					if (!item.Pinned && !item.Leaving && item.Lie <= 0f && item.Bed < 0 && item.Role != HouseOccupancy.Doing.Sleep)
					{
						yield return item.Who;
					}
				}
			}
		}

		public bool DoorHeldOpen
		{
			get
			{
				if (!Propped(House))
				{
					return _doorWanted;
				}
				return true;
			}
		}

		internal HouseWorld.Placement Place => _place;

		public bool Preview { get; private set; }

		public bool Fitted
		{
			get
			{
				if (Room != null)
				{
					return Room.Fit != null;
				}
				return false;
			}
		}

		private Vector2 DoorOut => new Vector2(Room.DoorOutX, Room.DoorOutY);

		private Vector2 DoorWall => new Vector2(Room.Fit.DoorWallX, Room.Fit.DoorWallY);

		public float DoorOpen { get; private set; }

		public float DoorBar { get; private set; }

		internal static void HoldIndoors(Actor body, Building b)
		{
			if (!Shelter || body == null || b == null)
			{
				return;
			}
			try
			{
				if (body.isAlive() && (!body.is_inside_building || body.inside_building != b))
				{
					body.stayInBuilding(b);
				}
			}
			catch
			{
			}
		}

		internal static void LetOut(Actor body, Building b)
		{
			try
			{
				if (body != null && body.isAlive() && body.is_inside_building && body.inside_building == b)
				{
					body.exitBuilding();
				}
			}
			catch
			{
			}
		}

		public void KeepIndoors()
		{
			if (Inside && !Virtual && !Preview)
			{
				HoldIndoors(_body, House);
			}
		}

		public static bool IsEnterable(Building b)
		{
			try
			{
				if (b == null || !b.isAlive() || b.asset == null || b.isUnderConstruction() || b.isRuin())
				{
					return false;
				}
				if (Rooms.CanEnter(b))
				{
					return true;
				}
				return AssetStyle.Classify(b.asset) == BuildingLook.House && RoomName(b.asset) != null;
			}
			catch
			{
				return false;
			}
		}

		public static string RoomName(BuildingAsset asset)
		{
			string text = asset.id ?? "";
			if ((asset.type ?? "") != "type_house")
			{
				return null;
			}
			if (!text.StartsWith("tent", StringComparison.Ordinal))
			{
				return "house";
			}
			return "tent";
		}

		public void Enter(Building b, SpriteImage sprite, float widthTiles, float depthTiles, Actor host, bool preview = false, Interior built = null, HouseWorld.Placement place = default(HouseWorld.Placement))
		{
			uint num = ((sprite != null) ? Dominant(sprite, sprite.OpaqueBottom, sprite.OpaqueBottom + (sprite.OpaqueTop - sprite.OpaqueBottom) * 55 / 100, TerrainCache.Rgb(215, 195, 155)) : TerrainCache.Rgb(215, 195, 155));
			uint num2 = ((sprite != null) ? Dominant(sprite, sprite.OpaqueTop - (sprite.OpaqueTop - sprite.OpaqueBottom) * 35 / 100, sprite.OpaqueTop, TerrainCache.Rgb(160, 60, 50)) : TerrainCache.Rgb(160, 60, 50));
			int num3 = (int)(b.getID() & 0x7FFFFFFF);
			RaceAndTier(b.asset, out var race, out var tier);
			int num4;
			int num5;
			if (Models.Ready)
			{
				num4 = 5 + Mathf.RoundToInt((float)tier * 0.6f) + ((num3 % 3 == 0) ? 1 : 0);
				num5 = 4 + (tier + 1) / 3;
			}
			else
			{
				num4 = Mathf.Clamp(Mathf.RoundToInt(widthTiles * 2.6f), 6, 12);
				num5 = Mathf.Clamp(Mathf.RoundToInt(depthTiles * 2.6f), 5, 10);
			}
			string text = null;
			if (Models.Ready)
			{
				string text2 = Models.BuildingFamily(b);
				text = ((text2 != null) ? Models.Get(text2) : null)?.Info("style");
			}
			RoomAsk roomAsk = new RoomAsk
			{
				B = b,
				Host = host,
				Width = num4,
				Depth = num5,
				Seed = num3,
				Tier = tier,
				Wall = num,
				Roof = num2,
				Race = race,
				Style = text,
				Models = (Models.Ready ? new Func<string, MeshModel>(Models.Get) : null)
			};
			if (built != null)
			{
				Room = built;
				_place = place;
			}
			else
			{
				Room = Rooms.Build(roomAsk) ?? BuildFittedRoom(b, num, num2, num3, race, tier, text) ?? InteriorBuilder.Build(num4, num5, num, num2, num3, race, tier, Models.Ready ? new Func<string, MeshModel>(Models.Get) : null, text);
			}
			if (Room.Fit == null)
			{
				_place = default(HouseWorld.Placement);
			}
			House = b;
			Name = roomAsk.Name ?? RoomName(b.asset) ?? "house";
			Eye = new Vector2(Room.SpawnX, Room.SpawnY);
			HasBench = false;
			BenchNote = null;
			if (Models.Ready && host != null && Workbench.HomeOf(host) == b)
			{
				int num6 = Math.Max(1, Workbench.Level(host));
				HasBench = InteriorBuilder.AddWorkbench(Room, Models.Get, num6, num2, out BenchX, out BenchY);
				BenchNote = $"Your workbench is level {num6}, up to {Workbench.AddsAt(num6)}";
			}
			WhoIsIn(b, host, _occupants, !preview);
			_walkers.StillIn = (Actor a) => a != null && a.inside_building == b;
			_walkers.Start(Room, _occupants, num3);
			_seatHost = host;
			_seatTimer = 0f;
			PinSeats();
			Inside = true;
			Preview = preview;
			if (preview)
			{
				_body = null;
				return;
			}
			_body = host;
			HoldIndoors(host, b);
			string text3 = ((Room.Fit != null) ? $"fitted {Room.MaxX - Room.MinX:F1}x{Room.MaxY - Room.MinY:F1}, {Room.Fit.StoreyZ.Length} storeys" : $"{num4}x{num5}");
			Log.Info(string.Format("entered {0}: {1} tier {2}{3}, {4} room, {5} models, {6} lights, {7} people inside{8}", Name, race, tier, (text != null) ? (" " + text) : "", text3, Room.Meshes.Count, Room.Lights.Count, _occupants.Count, HasBench ? $", your workbench at {BenchX:F1},{BenchY:F1}" : ""));
		}

		internal static void RaceAndTier(BuildingAsset asset, out string race, out int tier)
		{
			string text = ((asset != null) ? (asset.id ?? "") : "");
			race = (text.Contains("elf") ? "elf" : (text.Contains("orc") ? "orc" : (text.Contains("dwarf") ? "dwarf" : "human")));
			char c = ((text.Length > 0) ? text[text.Length - 1] : '0');
			tier = ((c >= '0' && c <= '5') ? (c - 48) : 0);
		}

		public void EnterVirtual(uint wall, uint roof, List<Actor> occupants, string race = "human", int tier = 2, string style = null)
		{
			Room = InteriorBuilder.Build(5 + Mathf.RoundToInt((float)tier * 0.6f), 4 + (tier + 1) / 3, wall, roof, 7, race, tier, Models.Ready ? new Func<string, MeshModel>(Models.Get) : null, style);
			House = null;
			_body = null;
			HasBench = false;
			BenchNote = null;
			Virtual = true;
			_testOffset = null;
			Name = "test house";
			Eye = new Vector2(Room.SpawnX, Room.SpawnY);
			_occupants.Clear();
			foreach (Actor occupant in occupants)
			{
				if (occupant != null && occupant.isAlive() && _occupants.Count < Room.Spots.Count)
				{
					_occupants.Add(occupant);
				}
			}
			_walkers.StillIn = null;
			_walkers.Start(Room, _occupants, 7);
			Inside = true;
		}

		internal void PinForTest(List<RoomSeat> seats)
		{
			_walkers.Pin(seats);
		}

		private void PinSeats()
		{
			if (House != null && Room != null)
			{
				_seats.Clear();
				Rooms.Seated(House, Room, _seatHost, _seats);
				_walkers.Pin(_seats);
			}
		}

		public void Exit()
		{
			if (!Preview)
			{
				RoomFloors.Leave();
			}
			Preview = false;
			_place = default(HouseWorld.Placement);
			Virtual = false;
			if (Inside)
			{
				Log.Info("left " + Name);
			}
			LetOut(_body, House);
			_body = null;
			Inside = false;
			House = null;
			Room = null;
			_occupants.Clear();
			_walkers.Clear();
			Tagged.Clear();
			_seats.Clear();
			_seatHost = null;
		}

		internal Vector2 CombatOffset(Actor host)
		{
			if (Room == null)
			{
				return Vector2.zero;
			}
			if (Fitted)
			{
				return ToWorld(Vector2.zero);
			}
			Vector2 vector = new Vector2((Room.MinX + Room.MaxX) * 0.5f, (Room.MinY + Room.MaxY) * 0.5f);
			if (House == null && _testOffset.HasValue)
			{
				return _testOffset.Value;
			}
			Vector2 vector2 = Vector2.zero;
			try
			{
				vector2 = ((House != null) ? House.current_position : (host?.current_position ?? Vector2.zero));
			}
			catch
			{
			}
			if (House == null)
			{
				_testOffset = vector2 - vector;
			}
			return vector2 - vector;
		}

		public void Move(float forward, float strafe, float yaw, float dt, float speed = 0f)
		{
			if (forward == 0f && strafe == 0f)
			{
				return;
			}
			if (speed <= 0f)
			{
				speed = 1.7f;
			}
			float num = Mathf.Cos(yaw);
			float num2 = Mathf.Sin(yaw);
			Vector2 move = new Vector2(num * forward + num2 * strafe, num2 * forward - num * strafe).normalized * (speed * dt);
			move *= RoomFloors.Pace(Eye, move, speed);
			NoteWalk(move);
			int num3 = Mathf.Clamp(Mathf.CeilToInt(move.magnitude / 0.03f), 1, 12);
			move /= (float)num3;
			for (int i = 0; i < num3; i++)
			{
				Vector2 eye = Eye;
				Vector2 vector = Eye + new Vector2(move.x, 0f);
				if (Free(vector))
				{
					Eye = vector;
					RoomFloors.Stepped(Eye);
				}
				vector = Eye + new Vector2(0f, move.y);
				if (Free(vector))
				{
					Eye = vector;
					RoomFloors.Stepped(Eye);
				}
				if (Eye != eye)
				{
					continue;
				}
				float[] deflect = Deflect;
				foreach (float f in deflect)
				{
					float num4 = Mathf.Cos(f);
					float num5 = Mathf.Sin(f);
					vector = Eye + new Vector2(move.x * num4 - move.y * num5, move.x * num5 + move.y * num4) * Mathf.Cos(f);
					if (Free(vector))
					{
						Eye = vector;
						RoomFloors.Stepped(Eye);
						break;
					}
				}
			}
		}

		internal bool TryPut(Vector2 p)
		{
			if (Room == null || !Free(p))
			{
				return false;
			}
			Eye = p;
			RoomFloors.Stepped(Eye);
			return true;
		}

		private bool Free(Vector2 p)
		{
			if (Room.Fit != null)
			{
				int num = Room.Fit.StoreyAt(RoomFloors.Feet);
				if (!Room.Fit.Clear(num, p.x, p.y, 0.14f) && (num != 0 || !InDoorway(p)))
				{
					return false;
				}
				if (RoomFloor.ThroughRail(Room.Fit, Eye.x, Eye.y, p.x, p.y, RoomFloors.Feet))
				{
					return false;
				}
				float num2 = Room.Fit.StoreyZ[num];
				foreach (float[] solid in Room.Solids)
				{
					if (!((solid.Length >= 6) ? (Math.Abs(solid[4] - num2) > 0.3f) : (num > 0)) && !RoomFloors.Climbs(solid) && RoomFloor.IntoPiece(solid, 0.12f, Eye.x, Eye.y, p.x, p.y))
					{
						return false;
					}
				}
				if (!RoomFloors.CanMove(p))
				{
					return false;
				}
				if (_walkers.Occupied(p, 0.1f, num) && !_walkers.Occupied(Eye, 0.1f, num) && RoomFloors.Feet < num2 + 0.4f)
				{
					return false;
				}
				return true;
			}
			if (p.x < Room.MinX || p.x > Room.MaxX || p.y < Room.MinY || p.y > Room.MaxY)
			{
				return false;
			}
			foreach (float[] solid2 in Room.Solids)
			{
				if (!RoomFloors.Climbs(solid2) && RoomFloor.IntoPiece(solid2, 0.12f, Eye.x, Eye.y, p.x, p.y))
				{
					return false;
				}
			}
			if (!RoomFloors.CanMove(p))
			{
				return false;
			}
			if (RoomFloors.Low && _walkers.Occupied(p, 0.1f) && !_walkers.Occupied(Eye, 0.1f))
			{
				return false;
			}
			return true;
		}

		internal bool SendWalker(Actor who, Vector2 to, int level, float faceYaw, Action onArrive)
		{
			return _walkers.SendTo(who, to, level, faceYaw, onArrive);
		}

		internal string BlockedWhyForTest(Vector2 p)
		{
			if (Room == null)
			{
				return "no room";
			}
			if (Room.Fit != null)
			{
				int num = Room.Fit.StoreyAt(RoomFloors.Feet);
				if (!Room.Fit.Clear(num, p.x, p.y, 0.14f) && (num != 0 || !InDoorway(p)))
				{
					return $"walls (storey {num}, inside {Room.Fit.Inside(num, p.x, p.y)}, doorway {InDoorway(p)})";
				}
				if (RoomFloor.ThroughRail(Room.Fit, Eye.x, Eye.y, p.x, p.y, RoomFloors.Feet))
				{
					return "the stair's rail";
				}
				float num2 = Room.Fit.StoreyZ[num];
				foreach (float[] solid in Room.Solids)
				{
					if (!((solid.Length >= 6) ? (Math.Abs(solid[4] - num2) > 0.3f) : (num > 0)) && !RoomFloors.Climbs(solid) && RoomFloor.IntoPiece(solid, 0.12f, Eye.x, Eye.y, p.x, p.y))
					{
						return $"solid {solid[0]:F2},{solid[1]:F2}-{solid[2]:F2},{solid[3]:F2}";
					}
				}
				if (!RoomFloors.CanMove(p))
				{
					return $"floor field (feet {RoomFloors.Feet:F2})";
				}
				if (_walkers.Occupied(p, 0.1f, num))
				{
					return "someone there";
				}
				return null;
			}
			if (!Free(p))
			{
				return "room of old";
			}
			return null;
		}

		public float DistanceTo(Actor a)
		{
			RoomWalkers.Walker walker = _walkers.Of(a);
			if (walker != null)
			{
				return (Eye - walker.Pos).magnitude;
			}
			return float.MaxValue;
		}

		public Vector2 PositionOf(Actor a)
		{
			return _walkers.Of(a)?.Pos ?? Eye;
		}

		public Vector3 HeadOf(Actor a, SpriteCache sprites)
		{
			RoomWalkers.Walker walker = _walkers.Of(a);
			if (walker == null)
			{
				return new Vector3(Eye.x, Eye.y, 1f);
			}
			float num = BodyHeight(a, sprites);
			if (walker.LieShown > 0.5f)
			{
				return new Vector3(walker.Pos.x - Mathf.Cos(walker.Heading) * num * 0.85f, walker.Pos.y - Mathf.Sin(walker.Heading) * num * 0.85f, walker.Z + 0.2f);
			}
			return new Vector3(walker.Pos.x, walker.Pos.y, walker.Z + num + 0.15f - walker.Sit * 0.24f * num);
		}

		private static float BodyHeight(Actor a, SpriteCache sprites)
		{
			try
			{
				SpriteImage spriteImage = sprites.Get(a.getSpriteToRender());
				if (spriteImage != null)
				{
					return Stature.Peek(a, spriteImage.UnitHeight * spriteImage.VisibleFraction * Mathf.Abs(a.current_scale.y));
				}
			}
			catch
			{
			}
			return 1f;
		}

		public void Collect(List<VoxelObject> voxels, List<MeshInstance> meshes, SpriteCache sprites, Dictionary<Actor, Vector2> lookAt, float dt)
		{
			Tagged.Clear();
			if (!OutdoorsGathered)
			{
				People.Tick();
				Creatures.Tick();
			}
			OutdoorsGathered = false;
			foreach (MeshInstance mesh in Room.Meshes)
			{
				mesh.SortDepth = new Vector2(mesh.Position.X - Eye.x, mesh.Position.Y - Eye.y).magnitude;
				meshes.Add(mesh);
			}
			foreach (VoxelObject prop in Room.Props)
			{
				VoxelObject item = prop;
				item.SortDepth = new Vector2(prop.Origin.X - Eye.x, prop.Origin.Y - Eye.y).magnitude;
				voxels.Add(item);
			}
			if ((_seatTimer -= dt) <= 0f)
			{
				_seatTimer = 1f;
				PinSeats();
				Comings();
			}
			_walkers.YouLevel = ((Room.Fit != null && !Preview) ? Room.Fit.StoreyAt(RoomFloors.Feet) : 0);
			_walkers.Tick(Eye, lookAt.Keys, dt);
			bool flag = !Preview && RoomCombat.Active && RoomCombat.Walkers == _walkers;
			if (flag)
			{
				RoomCombat.Sync();
				RoomCombat.Drawing = true;
			}
			try
			{
				DrawWalkers(voxels, meshes, sprites, dt);
			}
			finally
			{
				RoomCombat.Drawing = false;
			}
			if (flag)
			{
				RoomCombat.DrawFallen(meshes, dt, Eye);
			}
		}

		private void DrawWalkers(List<VoxelObject> voxels, List<MeshInstance> meshes, SpriteCache sprites, float dt)
		{
			foreach (RoomWalkers.Walker item2 in _walkers.All)
			{
				Actor who = item2.Who;
				if (who == null || !who.isAlive())
				{
					continue;
				}
				SpriteImage spriteImage;
				try
				{
					spriteImage = sprites.Get(who.getSpriteToRender());
				}
				catch
				{
					continue;
				}
				if (spriteImage == null)
				{
					continue;
				}
				float x = item2.Pos.x;
				float y = item2.Pos.y;
				float heading = item2.Heading;
				float speed = item2.Speed;
				Vector3 current_scale = who.current_scale;
				float num = spriteImage.UnitWidth * Mathf.Abs(current_scale.x);
				float num2 = spriteImage.UnitHeight * Mathf.Abs(current_scale.y);
				float body = Stature.Of(who, num2 * spriteImage.VisibleFraction, dt);
				float magnitude = new Vector2(x - Eye.x, y - Eye.y).magnitude;
				int count = Tagged.Count;
				if (Models.Ready && People.IsHumanoid(who))
				{
					VoxelObject template = new VoxelObject
					{
						Tag = count,
						NoShadow = true,
						SortDepth = magnitude
					};
					People.AddMesh(meshes, voxels, who, spriteImage, sprites, x, y, item2.Z, body, heading, speed, magnitude, dt, template, swimming: false, -1f, -1f, item2.Sit, item2.LieShown);
					Tagged.Add(who);
					continue;
				}
				if (Models.Ready && Models.CreatureFamily(who) != null && Creatures.Add(meshes, who, spriteImage, x, y, 0f, num, num2, heading, speed, magnitude, dt, count, 0u, 0u, swimming: false))
				{
					Tagged.Add(who);
					continue;
				}
				if (People.IsHumanoid(who))
				{
					VoxelObject template2 = new VoxelObject
					{
						Tag = Tagged.Count,
						NoShadow = true,
						SortDepth = magnitude
					};
					People.Add(voxels, who, spriteImage, sprites, x, y, 0f, body, heading, People.StepFrame(who, speed > 0.1f), template2);
					Tagged.Add(who);
					continue;
				}
				VoxelObject item = VoxelObject.Standing(spriteImage.CreatureShape, x, y, (0f - spriteImage.BottomPadding) * num2, num, num2, spriteImage.PivotX, 0f - Mathf.Cos(heading), 0f - Mathf.Sin(heading));
				item.Tag = Tagged.Count;
				item.NoShadow = true;
				item.SortDepth = magnitude;
				Tagged.Add(who);
				voxels.Add(item);
				if (RoomCombat.Drawing)
				{
					Hitboxes.RecordSprite(who, x, y, item2.Z, num, num2 * spriteImage.VisibleFraction, heading);
				}
			}
		}

		private static uint Dominant(SpriteImage img, int y0, int y1, uint fallback)
		{
			Dictionary<uint, int> dictionary = new Dictionary<uint, int>();
			for (int i = Math.Max(0, y0); i < Math.Min(img.Height, y1); i++)
			{
				for (int j = 0; j < img.Width; j++)
				{
					uint num = img.Pixels[i * img.Width + j];
					if (num >> 24 >= 128)
					{
						int num2 = (int)(num & 0xFF);
						int num3 = (int)((num >> 8) & 0xFF);
						int num4 = (int)((num >> 16) & 0xFF);
						if (num2 + num3 + num4 >= 120)
						{
							uint key = num | 0xFF000000u;
							dictionary.TryGetValue(key, out var value);
							dictionary[key] = value + 1;
						}
					}
				}
			}
			uint result = fallback;
			int num5 = 0;
			foreach (KeyValuePair<uint, int> item in dictionary)
			{
				if (item.Value > num5)
				{
					result = item.Key;
					num5 = item.Value;
				}
			}
			return result;
		}

		private static long PropKey(Building b)
		{
			object obj = World.world?.map_stats;
			if (obj != _proppedWorld)
			{
				_propped.Clear();
				_proppedWorld = obj;
			}
			try
			{
				return (b?.data != null) ? b.data.id : (-1);
			}
			catch
			{
				return -1L;
			}
		}

		public static bool Propped(Building b)
		{
			if (b != null)
			{
				return _propped.Contains(PropKey(b));
			}
			return false;
		}

		public static void Unprop(Building b)
		{
			if (b != null && _propped.Remove(PropKey(b)))
			{
				DoorToggles++;
				Log.Info("door: closed by hand from the street");
			}
		}

		public void ToggleDoor()
		{
			if (House == null)
			{
				return;
			}
			if (DoorHeldOpen)
			{
				_propped.Remove(PropKey(House));
				_doorWanted = false;
				_closedAt = Time.unscaledTime;
			}
			else
			{
				if (_propped.Count > 40)
				{
					_propped.Clear();
				}
				long num = PropKey(House);
				if (num >= 0)
				{
					_propped.Add(num);
				}
				_doorWanted = true;
			}
			DoorToggles++;
			Log.Info("door: " + (_doorWanted ? "opened" : "closed") + " by hand");
		}

		private static bool Alive(Building b)
		{
			try
			{
				return b.isAlive();
			}
			catch
			{
				return false;
			}
		}

		private void NoteWalk(Vector2 move)
		{
			if (!(move.sqrMagnitude < 1E-08f))
			{
				_walkWay = move.normalized;
				_walkAt = Time.unscaledTime;
			}
		}

		private bool DoorWantedForYou(Vector2 door)
		{
			if (House != _doorFor)
			{
				_doorFor = House;
				_doorWanted = true;
			}
			float magnitude = (Eye - door).magnitude;
			if (Time.unscaledTime - _walkAt < 0.15f && magnitude > 0.01f)
			{
				float num = Vector2.Dot(_walkWay, (door - Eye) / magnitude);
				if (num > 0.6f && magnitude < 0.85f && Time.unscaledTime - _closedAt > 0.8f)
				{
					_doorWanted = true;
				}
				else if (num < -0.3f && magnitude > 0.6f)
				{
					_doorWanted = false;
				}
			}
			if (magnitude > 1.4f)
			{
				_doorWanted = false;
			}
			if (!Propped(House))
			{
				return _doorWanted;
			}
			return true;
		}

		public bool DoorAimed(ViewState v)
		{
			if (!Inside || Room == null || Preview)
			{
				return false;
			}
			Vector2 vector = new Vector2(Room.DoorOutX, Room.DoorOutY);
			Vector2 vector2;
			float num;
			float num2;
			if (Fitted && !float.IsNaN(Room.Fit.DoorWallX))
			{
				vector2 = new Vector2(Room.Fit.DoorWallX, Room.Fit.DoorWallY);
				num = Room.Fit.DoorHalf;
				HousePlan.Opening door = Room.Fit.Plan.Door;
				num2 = (door.Z1 - door.Z0) * Room.Fit.Scale;
			}
			else
			{
				vector2 = new Vector2(Room.DoorX, Room.DoorY) + vector * 0.45f;
				num = 0.5f;
				num2 = 1.5f;
			}
			if (vector.sqrMagnitude < 1E-06f)
			{
				return false;
			}
			vector.Normalize();
			float num3 = 1f / Mathf.Sqrt(1f + v.Pitch * v.Pitch);
			Vector3 vector3 = new Vector3(Mathf.Cos(v.Yaw) * num3, Mathf.Sin(v.Yaw) * num3, v.Pitch * num3);
			float num4 = vector3.x * vector.x + vector3.y * vector.y;
			if (num4 < 0.2f)
			{
				return false;
			}
			float num5 = ((vector2.x - v.X) * vector.x + (vector2.y - v.Y) * vector.y) / num4;
			if (num5 < -0.3f || num5 > 2.4f)
			{
				return false;
			}
			float num6 = v.X + vector3.x * num5 - vector2.x;
			float num7 = v.Y + vector3.y * num5 - vector2.y;
			float num8 = v.Z + vector3.z * num5;
			if (Mathf.Abs(num6 * (0f - vector.y) + num7 * vector.x) <= num + 0.15f && num8 > -0.3f)
			{
				return num8 < num2 + 0.25f;
			}
			return false;
		}

		private Interior BuildFittedRoom(Building b, uint wall, uint roof, int seed, string race, int tier, string style)
		{
			if (!Models.Ready || !WalksIn(b.asset) || !HouseWorld.Of(b, out var p))
			{
				return null;
			}
			HousePlan housePlan = HouseWorld.PlanFor(p);
			if (housePlan == null)
			{
				Log.Info("room: no plan of " + p.Model.Name + " (" + HousePlan.Why + "), the room of old");
				return null;
			}
			try
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				Interior result = InteriorBuilder.BuildFitted(housePlan, p.Scale, p.Mirror, wall, roof, seed, race, tier, Models.Get, style);
				_place = p;
				Log.Info(string.Format("room: fitted {0} at scale {1:F3}{2} in {3:F0} ms", p.Model.Name, p.Scale, p.Mirror ? " mirrored" : "", stopwatch.Elapsed.TotalMilliseconds));
				return result;
			}
			catch (Exception ex)
			{
				Log.Error("room: fitting " + p.Model.Name + " failed, the room of old: " + ex.Message);
				return null;
			}
		}

		internal static void LookOf(Building b, SpriteImage sprite, out uint wall, out uint roof, out int seed, out string race, out int tier, out string style)
		{
			wall = ((sprite != null) ? Dominant(sprite, sprite.OpaqueBottom, sprite.OpaqueBottom + (sprite.OpaqueTop - sprite.OpaqueBottom) * 55 / 100, TerrainCache.Rgb(215, 195, 155)) : TerrainCache.Rgb(215, 195, 155));
			roof = ((sprite != null) ? Dominant(sprite, sprite.OpaqueTop - (sprite.OpaqueTop - sprite.OpaqueBottom) * 35 / 100, sprite.OpaqueTop, TerrainCache.Rgb(160, 60, 50)) : TerrainCache.Rgb(160, 60, 50));
			seed = (int)(b.getID() & 0x7FFFFFFF);
			RaceAndTier(b.asset, out race, out tier);
			style = null;
			if (Models.Ready)
			{
				string text = Models.BuildingFamily(b);
				style = ((text != null) ? Models.Get(text) : null)?.Info("style");
			}
		}

		internal static bool WalksIn(BuildingAsset asset)
		{
			string text = ((asset != null) ? (asset.id ?? "") : "");
			if (UseFitted && text.Contains("human"))
			{
				return !text.StartsWith("tent", StringComparison.Ordinal);
			}
			return false;
		}

		public Vector2 ToWorld(Vector2 r)
		{
			return new Vector2(_place.X + r.x - Room.Fit.OffsetX, _place.Y + r.y - Room.Fit.OffsetY);
		}

		public Vector2 ToRoom(Vector2 w)
		{
			return new Vector2(w.x - _place.X + Room.Fit.OffsetX, w.y - _place.Y + Room.Fit.OffsetY);
		}

		public float WorldZ(float roomZ)
		{
			return _place.Ground + Room.Fit.FloorUp + roomZ;
		}

		private void DoorFrame(Vector2 p, out float outward, out float along)
		{
			Vector2 doorOut = DoorOut;
			Vector2 lhs = p - DoorWall;
			outward = Vector2.Dot(lhs, doorOut);
			along = lhs.x * (0f - doorOut.y) + lhs.y * doorOut.x;
		}

		private bool InDoorway(Vector2 p)
		{
			if (!Fitted || float.IsNaN(Room.Fit.DoorWallX))
			{
				return false;
			}
			DoorFrame(p, out var outward, out var along);
			if (Mathf.Abs(along) < Room.Fit.DoorHalf - 0.08400001f && outward > -0.7f)
			{
				return outward < 0.4f;
			}
			return false;
		}

		public bool OutOfDoor(Vector2 eye, Vector2 going)
		{
			if (!Fitted || float.IsNaN(Room.Fit.DoorWallX) || Virtual)
			{
				return false;
			}
			DoorFrame(eye, out var outward, out var along);
			if (outward > -0.05f && Mathf.Abs(along) < Room.Fit.DoorHalf + 0.1f)
			{
				return Vector2.Dot(going, DoorOut) > 0.3f;
			}
			return false;
		}

		public void ComeInAt(Vector2 world)
		{
			if (Fitted && !float.IsNaN(Room.Fit.DoorWallX))
			{
				Vector2 p = ToRoom(world);
				DoorFrame(p, out var outward, out var along);
				along = Mathf.Clamp(along, 0f - (Room.Fit.DoorHalf - 0.14f), Room.Fit.DoorHalf - 0.14f);
				outward = Mathf.Min(outward, -0.12f);
				Vector2 doorOut = DoorOut;
				Eye = DoorWall + doorOut * outward + new Vector2(0f - doorOut.y, doorOut.x) * along;
			}
		}

		public void TakeOver(HouseInterior from, Actor host, bool preview = false)
		{
			if (from != null && from.Inside && from.Room != null)
			{
				Exit();
				if (from._body != null)
				{
					LetOut(from._body, from.House);
				}
				if (!from.Preview)
				{
					RoomFloors.Leave();
				}
				Room = from.Room;
				House = from.House;
				Name = from.Name;
				_place = from._place;
				HasBench = from.HasBench;
				BenchX = from.BenchX;
				BenchY = from.BenchY;
				BenchNote = from.BenchNote;
				RoomWalkers walkers = _walkers;
				_walkers = from._walkers;
				from._walkers = walkers;
				_occupants.Clear();
				_occupants.AddRange(from._occupants);
				_seats.Clear();
				_seats.AddRange(from._seats);
				_seatHost = host;
				_seatTimer = 0f;
				Inside = true;
				Preview = preview;
				DoorOpen = from.DoorOpen;
				DoorBar = from.DoorBar;
				Eye = from.Eye;
				_body = (preview ? null : host);
				if (!preview)
				{
					HoldIndoors(host, House);
				}
				from.Room = null;
				from.Inside = false;
				from.Preview = false;
				from.House = null;
				from._body = null;
				from._place = default(HouseWorld.Placement);
				from._occupants.Clear();
				from._walkers.Clear();
				from._seats.Clear();
				from.Tagged.Clear();
			}
		}

		private ViewState FittedOutside(ViewState inside)
		{
			ViewState result = inside;
			Vector2 vector = ToWorld(new Vector2(inside.X, inside.Y));
			result.X = vector.x;
			result.Y = vector.y;
			result.Z = WorldZ(inside.Z);
			return result;
		}

		public ViewState RoomView(ViewState street)
		{
			ViewState result = street;
			Vector2 vector = ToRoom(new Vector2(street.X, street.Y));
			result.X = vector.x;
			result.Y = vector.y;
			result.Z = street.Z - WorldZ(0f);
			return result;
		}

		public bool DoorFaceInWorld(out Vector3 middle, out float halfWidth, out float height, out Vector2 outward)
		{
			middle = default(Vector3);
			halfWidth = 0f;
			height = 0f;
			outward = default(Vector2);
			if (!Fitted)
			{
				return false;
			}
			HousePlan.Opening door = Room.Fit.Plan.Door;
			float scale = _place.Scale;
			float num = (_place.Mirror ? (0f - scale) : scale);
			middle = new Vector3(_place.X + door.X * num, _place.Y + door.Y * scale, _place.Ground + door.Z0 * scale);
			halfWidth = door.HalfWidth * scale;
			height = (door.Z1 - door.Z0) * scale;
			outward = new Vector2(door.OutX * (_place.Mirror ? (-1f) : 1f), door.OutY).normalized;
			return true;
		}

		public bool DoorwayInWorld(out Vector3 middle, out float halfWidth, out float height, out Vector2 outward)
		{
			middle = default(Vector3);
			halfWidth = 0f;
			height = 0f;
			outward = default(Vector2);
			if (!Fitted || float.IsNaN(Room.Fit.DoorWallX))
			{
				return false;
			}
			Vector2 vector = ToWorld(DoorWall);
			middle = new Vector3(vector.x, vector.y, WorldZ(0f));
			halfWidth = Room.Fit.DoorHalf;
			HousePlan.Opening door = Room.Fit.Plan.Door;
			height = (door.Z1 - door.Z0) * Room.Fit.Scale;
			outward = DoorOut;
			return true;
		}

		internal static Building HouseAt(Actor a)
		{
			try
			{
				if (a == null || !a.isAlive())
				{
					return null;
				}
				Building building = (a.is_inside_building ? a.inside_building : null);
				if (building == null)
				{
					Vector2 current_position = a.current_position;
					building = World.world.GetTile(Mathf.FloorToInt(current_position.x), Mathf.FloorToInt(current_position.y))?.building;
				}
				return (building != null && IsEnterable(building)) ? building : null;
			}
			catch
			{
				return null;
			}
		}

		internal static string Barred(Building b, Actor you)
		{
			try
			{
				Kingdom kingdom = b?.kingdom;
				if (you == null || kingdom == null || !kingdom.isCiv())
				{
					return null;
				}
				if (Workbench.HomeOf(you) == b)
				{
					return null;
				}
				if (Rep.Reigns(you, kingdom))
				{
					return null;
				}
				if (you.kingdom != null && you.kingdom != kingdom && you.kingdom.isEnemy(kingdom))
				{
					return "The door is barred. " + kingdom.name + " is at war with you";
				}
				City city = b.city;
				if ((city != null && Rep.Level(you, city) <= RepLevel.Outlaw) || Rep.Standing(you, kingdom) <= RepLevel.Outlaw)
				{
					return "The door is barred to outlaws";
				}
				return null;
			}
			catch
			{
				return null;
			}
		}

		private static void WhoIsIn(Building b, Actor host, List<Actor> into, bool latest)
		{
			into.Clear();
			HouseOccupancy.Scan(latest);
			foreach (Actor item in HouseOccupancy.Of(b).Inside)
			{
				if (item != null && item != host && item.isAlive())
				{
					into.Add(item);
				}
			}
		}

		private void Comings()
		{
			if (House == null || Room == null || Virtual)
			{
				return;
			}
			Vector2 door = new Vector2(Room.DoorX, Room.DoorY);
			try
			{
				foreach (Actor item in HouseOccupancy.Of(House).Inside)
				{
					if (item == null || item == _seatHost || !item.isAlive())
					{
						continue;
					}
					RoomWalkers.Walker walker = _walkers.Of(item);
					if (walker == null)
					{
						if (_walkers.All.Count < Room.Spots.Count + _walkers.BedPlaces)
						{
							_walkers.Arrive(item, door);
							Log.Info("room: " + WorldBoxMod.Describe(item) + " comes in");
						}
					}
					else if (!walker.Pinned && !walker.Leaving && !walker.Fighting && !walker.Fleeing && HouseOccupancy.StayLeft(item) < 2.5f)
					{
						_walkers.Leave(item, door);
						Log.Info("room: " + WorldBoxMod.Describe(item) + " is off out");
					}
				}
				foreach (RoomWalkers.Walker item2 in _walkers.All)
				{
					if (item2.Who == null || item2.Pinned || item2.Fighting || item2.Fleeing)
					{
						continue;
					}
					bool flag = false;
					try
					{
						flag = item2.Who.inside_building == House;
					}
					catch
					{
					}
					if (item2.Leaving)
					{
						if (flag && HouseOccupancy.StayLeft(item2.Who) > 5f)
						{
							_walkers.Stay(item2.Who);
							Log.Info("room: " + WorldBoxMod.Describe(item2.Who) + " stays in after all");
						}
						continue;
					}
					item2.Role = HouseOccupancy.DoingOf(item2.Who);
					if (!flag)
					{
						_walkers.Leave(item2.Who, door);
						Log.Info("room: " + WorldBoxMod.Describe(item2.Who) + " goes out");
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("room comings: " + ex.Message);
			}
		}

		public bool SeatTaken(Vector2 at)
		{
			foreach (RoomWalkers.Walker item in _walkers.All)
			{
				if ((item.Pos - at).sqrMagnitude < 0.09f || (item.Spot >= 0 && item.Spot < Room.Spots.Count && (new Vector2(Room.Spots[item.Spot][0], Room.Spots[item.Spot][1]) - at).sqrMagnitude < 0.0025000002f))
				{
					return true;
				}
			}
			return false;
		}

		public bool Seated(Actor a)
		{
			RoomWalkers.Walker walker = _walkers.Of(a);
			if (walker != null)
			{
				return walker.Sit > 0.5f;
			}
			return false;
		}

		public bool CameraFits(Vector2 p, float r)
		{
			if (Room == null)
			{
				return false;
			}
			if (Room.Fit != null)
			{
				return Room.Fit.Clear(Room.Fit.StoreyAt(RoomFloors.Feet), p.x, p.y, r);
			}
			if (p.x > Room.MinX + r && p.x < Room.MaxX - r && p.y > Room.MinY + r)
			{
				return p.y < Room.MaxY - r;
			}
			return false;
		}

		public ViewState OutsideView(ViewState inside, Func<float, float, float> ground)
		{
			if (Fitted)
			{
				return FittedOutside(inside);
			}
			ViewState result = inside;
			Building house = House;
			if (house == null || !Steering.DoorOf(house, out var door))
			{
				Vector2 vector = house?.current_position ?? Vector2.zero;
				float num = 1f;
				try
				{
					BuildingFundament fundament = house.asset.fundament;
					if (fundament != null)
					{
						num = Mathf.Max(1f, fundament.top + fundament.bottom + 1);
					}
				}
				catch
				{
				}
				door = vector + new Vector2(0f, (0f - num) * 0.5f - 0.3f);
			}
			door.y += 0.3f;
			float num2 = Mathf.Max(1f, Room.MaxX - Room.MinX);
			float num3 = 2.5f;
			try
			{
				BuildingFundament buildingFundament = house?.asset.fundament;
				if (buildingFundament != null)
				{
					num3 = Mathf.Max(1.5f, buildingFundament.left + buildingFundament.right + 1);
				}
			}
			catch
			{
			}
			float num4 = Mathf.Clamp(num3 / num2, 0.2f, 1f);
			float num5 = (inside.X - Room.DoorX) * num4;
			float num6 = (inside.Y - Room.DoorY) * num4;
			bool flag = false;
			try
			{
				flag = house != null && Models.Mirrored(house);
			}
			catch
			{
			}
			if (flag)
			{
				num5 = 0f - num5;
			}
			result.X = door.x + num5;
			result.Y = door.y + num6;
			result.Z = ground(result.X, result.Y) + inside.Z;
			return result;
		}

		public bool PortalInView(ViewState v)
		{
			float num = ((Screen.width > 0) ? ((float)Screen.height / (float)Screen.width) : 0.5625f);
			float halfUp = Mathf.Atan(Mathf.Tan(v.Fov * 0.5f) * num) + 0.35f;
			return PortalInView(v.X, v.Y, v.Yaw, v.Fov, v.Z, Mathf.Atan(v.Pitch), halfUp);
		}

		public bool PortalInView(float x, float y, float yaw, float fov, float z = float.NaN, float pitch = 0f, float halfUp = 9f)
		{
			if (Room == null)
			{
				return false;
			}
			float num = fov * 0.5f + 0.5f;
			foreach (LightWindow window in Room.Windows)
			{
				float num2 = window.Center.X - x;
				float num3 = window.Center.Y - y;
				if (num2 * window.Inward.X + num3 * window.Inward.Y > 0f || Curtains.Covers(Room, window) || Mathf.Abs(Mathf.DeltaAngle(yaw * 57.29578f, Mathf.Atan2(num3, num2) * 57.29578f)) >= num * 57.29578f)
				{
					continue;
				}
				if (!float.IsNaN(z))
				{
					float x2 = Mathf.Max(0.2f, Mathf.Sqrt(num2 * num2 + num3 * num3));
					float num4 = Mathf.Atan2(window.Center.Z + window.HalfHeight - z, x2);
					if (Mathf.Atan2(window.Center.Z - window.HalfHeight - z, x2) > pitch + halfUp || num4 < pitch - halfUp)
					{
						continue;
					}
				}
				return true;
			}
			if ((Room.DoorLeafPart >= 0 || Room.DoorPortal) && DoorOpen > 0.01f)
			{
				float num5 = Room.DoorX - x;
				float num6 = Room.DoorY - y;
				if (num5 * num5 + num6 * num6 < 0.09f)
				{
					return true;
				}
				if (Mathf.Abs(Mathf.DeltaAngle(yaw * 57.29578f, Mathf.Atan2(num6, num5) * 57.29578f)) < num * 57.29578f)
				{
					return true;
				}
			}
			return false;
		}

		public void UpdateDoor(float dt)
		{
			if (Room == null)
			{
				return;
			}
			Vector2 vector = new Vector2(Room.DoorX, Room.DoorY);
			bool flag = (Preview ? ((Eye - vector).sqrMagnitude < 1.9599999f) : DoorWantedForYou(vector));
			float doorOpen = DoorOpen;
			if (!flag)
			{
				foreach (RoomWalkers.Walker item in _walkers.All)
				{
					if ((item.Pos - vector).sqrMagnitude < 0.64000005f && (item.Leaving || item.Spot < 0 || item.Speed > 0.05f))
					{
						flag = true;
						break;
					}
				}
			}
			float num = Mathf.Clamp(dt, 0f, 0.1f);
			bool flag2 = !Preview && Room.DoorBarPart >= 0 && HomeLock.Locked(House);
			if (flag && DoorBar > 0f)
			{
				DoorBar = Mathf.MoveTowards(DoorBar, 0f, num * 3f);
			}
			else
			{
				DoorOpen = Mathf.MoveTowards(DoorOpen, flag ? 1f : 0f, num * (flag ? 2.4f : 1.3f));
			}
			if (!flag)
			{
				DoorBar = Mathf.MoveTowards(DoorBar, (flag2 && DoorOpen <= 0f) ? 1f : 0f, num * 2f);
			}
			if (!Preview && Room.DoorLeafPart >= 0 && ((doorOpen <= 0f && DoorOpen > 0f) || (doorOpen > 0f && DoorOpen <= 0f)))
			{
				try
				{
					WorldBoxMod.Instance?.Audio?.PlayEffect("door", (DoorOpen > 0f) ? 0.4f : 0.3f);
				}
				catch
				{
				}
			}
			float doorOpen2 = DoorOpen;
			Room.SwingDoor(doorOpen2 * doorOpen2 * (3f - 2f * doorOpen2) * 1.55f, DoorBar);
		}
	}
}
