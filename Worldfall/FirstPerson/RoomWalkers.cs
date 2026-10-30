using System;
using System.Collections.Generic;
using FirstPerson.Core;
using FirstPerson.OneWorld;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class RoomWalkers
	{
		internal sealed class Walker
		{
			private Actor _who;

			public long Id;

			public Vector2 Pos;

			public float Heading;

			public float Speed;

			public int Spot;

			public readonly List<Vector2> Path = new List<Vector2>();

			public float Wait;

			public float Blocked;

			public bool Pinned;

			public float PinHeading;

			public float Sit;

			public float Z;

			public bool Leaving;

			public HouseOccupancy.Doing Role;

			public float Lie;

			public int Bed = -1;

			public int BedPlace;

			public float LieShown;

			public Vector2 BedSide;

			internal bool NoWayToBed;

			public int Level;

			public readonly List<float> PathZ = new List<float>();

			public Vector2 SegFrom;

			public float SegFromZ;

			public Action Errand;

			public float ErrandYaw;

			public float ErrandTime;

			public bool Fighting;

			public bool Fleeing;

			public float Reach = 0.55f;

			public float RouteIn;

			public Vector2 ChaseAt;

			public int ChaseLevel;

			public Actor Who
			{
				get
				{
					return _who;
				}
				set
				{
					_who = value;
					try
					{
						Id = ((value != null && value.data != null) ? value.data.id : 0);
					}
					catch
					{
						Id = 0L;
					}
				}
			}
		}

		private sealed class Bed
		{
			public Vector2 Foot;

			public Vector2 Side;

			public float Z;

			public float Heading;

			public float Width;

			public bool Loft;

			public int Level;

			public float Floor;

			public int Places
			{
				get
				{
					if (!(Width >= 0.6f))
					{
						return 1;
					}
					return 2;
				}
			}
		}

		public Func<Actor, bool> StillIn;

		private const float Cell = 0.1f;

		private const int NearCells = 8;

		private const float ReachNear = 0.5f;

		private const float PathRadius = 0.12f;

		private const float Radius = 0.2f;

		private const float Pace = 0.7f;

		private const float RunPace = 1.9f;

		private const float TurnRate = 6f;

		private readonly List<Walker> _list = new List<Walker>();

		private readonly List<Bed> _beds = new List<Bed>();

		private Vector2 _door;

		private Interior _room;

		private System.Random _rng = new System.Random(1);

		private bool[][] _blockedAt;

		private int _w;

		private int _h;

		private float _x0;

		private float _y0;

		public int YouLevel;

		internal static bool KeepInsideSpots = true;

		private const float ErrandGiveUp = 15f;

		private readonly Dictionary<Actor, long> _fell = new Dictionary<Actor, long>();

		internal static Vector2? YouSeat;

		private float[] _pathCost;

		private int[] _pathCame;

		private bool[] _pathDone;

		private readonly List<(float F, int C)> _pathHeap = new List<(float, int)>();

		private readonly List<Vector2> _pathCells = new List<Vector2>();

		public int BedPlaces
		{
			get
			{
				int num = 0;
				foreach (Bed bed in _beds)
				{
					num += bed.Places;
				}
				return num;
			}
		}

		private int Levels
		{
			get
			{
				if (_room == null || _room.Fit == null)
				{
					return 1;
				}
				return _room.Fit.StoreyZ.Length;
			}
		}

		public IReadOnlyList<Walker> All => _list;

		public Vector2 Door => _door;

		public void Stay(Actor a)
		{
			Walker walker = Of(a);
			if (walker != null && walker.Leaving)
			{
				walker.Leaving = false;
				walker.Path.Clear();
				walker.Spot = -1;
				walker.Wait = 0.3f;
			}
		}

		private float LevelZ(int level)
		{
			if (_room == null || _room.Fit == null || level <= 0 || level >= _room.Fit.StoreyZ.Length)
			{
				return 0f;
			}
			return _room.Fit.StoreyZ[level];
		}

		private int LevelOfZ(float z)
		{
			if (_room == null || _room.Fit == null)
			{
				return 0;
			}
			return _room.Fit.StoreyAt(z);
		}

		private int LevelOf(float[] spot)
		{
			if (spot.Length <= 4)
			{
				return 0;
			}
			return LevelOfZ(spot[4]);
		}

		public void Start(Interior room, List<Actor> occupants, int seed)
		{
			_room = room;
			_rng = new System.Random(seed);
			_list.Clear();
			_fell.Clear();
			_door = new Vector2(room.DoorX, room.DoorY);
			BuildGrid();
			FindBeds();
			List<int> list = new List<int>();
			foreach (Actor occupant in occupants)
			{
				Walker walker = new Walker
				{
					Who = occupant,
					Role = HouseOccupancy.DoingOf(occupant),
					Spot = -1,
					Wait = 1.5f + (float)_rng.NextDouble() * 5f
				};
				if (Kept(walker, list))
				{
					_list.Add(walker);
					continue;
				}
				if (walker.Role == HouseOccupancy.Doing.Sleep && CanSit(occupant))
				{
					int num = FreeBed(walker);
					if (num >= 0)
					{
						LieIn(walker, num, 1f);
						_list.Add(walker);
						continue;
					}
				}
				int num2 = -1;
				foreach (int item in Preferred(walker))
				{
					if (!list.Contains(item))
					{
						num2 = item;
						break;
					}
				}
				if (num2 < 0)
				{
					Vector2 vector = new Vector2(room.SpawnX, room.SpawnY) + (new Vector2(room.SpawnX, room.SpawnY) - _door).normalized * 0.3f * _list.Count;
					if (Nearest(vector, 0, out var cx, out var cy))
					{
						vector = CellCentre(cx, cy);
					}
					walker.Pos = vector;
					walker.Heading = Mathf.Atan2(vector.y - _door.y, vector.x - _door.x);
					_list.Add(walker);
				}
				else
				{
					list.Add(num2);
					float[] array = room.Spots[num2];
					walker.Pos = new Vector2(array[0], array[1]);
					walker.Heading = array[2] * (MathF.PI / 180f);
					walker.Spot = num2;
					walker.Level = LevelOf(array);
					walker.Z = LevelZ(walker.Level);
					walker.Sit = ((IsSeat(array) && CanSit(occupant)) ? 1f : 0f);
					_list.Add(walker);
				}
			}
		}

		private bool Kept(Walker w, List<int> taken)
		{
			if (!KeepInsideSpots || !InsideSpots.Where(w.Who, out var _, out var place))
			{
				return false;
			}
			if (place.Use == RoomLayout.Use.Bed)
			{
				if (!CanSit(w.Who))
				{
					return false;
				}
				int num = -1;
				float num2 = 0.09f;
				for (int i = 0; i < _beds.Count; i++)
				{
					float sqrMagnitude = (_beds[i].Foot - new Vector2(place.X, place.Y)).sqrMagnitude;
					if (sqrMagnitude < num2)
					{
						num2 = sqrMagnitude;
						num = i;
					}
				}
				if (num < 0)
				{
					return false;
				}
				int num3 = 0;
				foreach (Walker item in _list)
				{
					if (item.Bed == num)
					{
						num3++;
					}
				}
				if (num3 >= _beds[num].Places)
				{
					return false;
				}
				LieIn(w, num, (w.Role == HouseOccupancy.Doing.Sleep) ? 1f : 0f);
				return true;
			}
			int spot = place.Spot;
			if (spot < 0 || spot >= _room.Spots.Count || taken.Contains(spot))
			{
				return false;
			}
			float[] array = _room.Spots[spot];
			if (Mathf.Abs(array[0] - place.X) > 0.05f || Mathf.Abs(array[1] - place.Y) > 0.05f)
			{
				return false;
			}
			taken.Add(spot);
			w.Pos = new Vector2(array[0], array[1]);
			w.Heading = array[2] * (MathF.PI / 180f);
			w.Spot = spot;
			w.Level = LevelOf(array);
			w.Z = LevelZ(w.Level);
			w.Sit = ((IsSeat(array) && CanSit(w.Who)) ? 1f : 0f);
			return true;
		}

		private void FindBeds()
		{
			_beds.Clear();
			foreach (MeshInstance mesh in _room.Meshes)
			{
				MeshModel model = mesh.Model;
				if (model == null)
				{
					continue;
				}
				string text = model.Info("use");
				if (text == "loft")
				{
					LoftBed(mesh, model);
				}
				else
				{
					if (!(text == "bed") && (text != null || model.Name == null || model.Name.IndexOf("_bed", StringComparison.Ordinal) < 0 || model.Name.IndexOf("side", StringComparison.Ordinal) >= 0))
					{
						continue;
					}
					Vec3 vec = mesh.Basis.Mul(new Vec3(0f, -1f, 0f));
					if (Mathf.Sqrt(vec.X * vec.X + vec.Y * vec.Y) < 0.0001f)
					{
						continue;
					}
					float num = Mathf.Atan2(vec.Y, vec.X);
					Vec3 min = model.Min;
					Vec3 max = model.Max;
					float z = mesh.Basis.Mul(new Vec3(0f, 0f, 1f)).Z;
					float num2 = (max.Y - min.Y) * 0.5f * z;
					float num3 = min.Z;
					float num4 = (max.X - min.X) * 0.3f;
					float num5 = (max.Y - min.Y) * 0.25f;
					float num6 = (min.X + max.X) * 0.5f;
					float num7 = (min.Y + max.Y) * 0.5f;
					MeshPart[] parts = model.Parts;
					foreach (MeshPart meshPart in parts)
					{
						for (int j = 0; j < meshPart.TriangleCount; j++)
						{
							float num8 = 0f;
							float num9 = 0f;
							float num10 = 0f;
							for (int k = 0; k < 3; k++)
							{
								int num11 = meshPart.Tri[j * 3 + k] * 3;
								num8 += meshPart.Pos[num11];
								num9 += meshPart.Pos[num11 + 1];
								num10 += meshPart.Pos[num11 + 2];
							}
							num8 /= 3f;
							num9 /= 3f;
							num10 /= 3f;
							if (Mathf.Abs(num8 - num6) < num4 && Mathf.Abs(num9 - num7) < num5)
							{
								num3 = Mathf.Max(num3, num10);
							}
						}
					}
					Vec3 vec2 = mesh.Basis.Mul(new Vec3((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f, 0f)) + mesh.Position;
					Vector2 vector = new Vector2(vec2.X, vec2.Y);
					Vector2 vector2 = new Vector2(Mathf.Cos(num), Mathf.Sin(num));
					Vector2 foot = vector + vector2 * (num2 - 0.12f);
					float b = mesh.Position.Z + num3 * z;
					Vector2 side = vector + new Vector2(0f - vector2.y, vector2.x) * 0.5f;
					float num12 = 1.44f;
					int num13 = LevelOfZ(mesh.Position.Z);
					for (int l = 0; l < _room.Spots.Count && l < _room.SpotUse.Count; l++)
					{
						if (!(_room.SpotUse[l] != "bed") && LevelOf(_room.Spots[l]) == num13)
						{
							Vector2 vector3 = new Vector2(_room.Spots[l][0], _room.Spots[l][1]);
							float sqrMagnitude = (vector3 - vector).sqrMagnitude;
							if (sqrMagnitude < num12)
							{
								num12 = sqrMagnitude;
								side = vector3;
							}
						}
					}
					float width = Mathf.Abs(max.X - min.X) * z;
					int level = LevelOfZ(mesh.Position.Z);
					_beds.Add(new Bed
					{
						Foot = foot,
						Side = side,
						Z = Mathf.Max(LevelZ(level) + 0.05f, b),
						Heading = num,
						Width = width,
						Level = level,
						Floor = LevelZ(level)
					});
				}
			}
			if (_beds.Count > 0)
			{
				Log.Info($"room: {_beds.Count} beds, the first at {_beds[0].Foot} ({_beds[0].Z:F2} up), got into from {_beds[0].Side}");
			}
			else
			{
				Log.Info("room: no beds");
			}
		}

		private void LoftBed(MeshInstance m, MeshModel model)
		{
			float[] array = CabinBuilder.Floats(model.Info("ramp"));
			if (array != null && array.Length >= 6)
			{
				float num = array[5];
				Vec3 vec = m.Basis.Mul(new Vec3(-0.35f, -0.5f, num + 0.19f)) + m.Position;
				Vec3 vec2 = m.Basis.Mul(new Vec3(array[0], array[1] + 0.1f, 0f)) + m.Position;
				Vec3 vec3 = m.Basis.Mul(new Vec3(0f, -1f, 0f));
				float heading = Mathf.Atan2(vec3.Y, vec3.X);
				_beds.Add(new Bed
				{
					Foot = new Vector2(vec.X, vec.Y),
					Side = new Vector2(vec2.X, vec2.Y),
					Z = vec.Z,
					Heading = heading,
					Width = 0.4f,
					Loft = true,
					Level = LevelOfZ(m.Position.Z),
					Floor = LevelZ(LevelOfZ(m.Position.Z))
				});
			}
		}

		private int FreeBed(Walker w)
		{
			int place;
			return FreeBed(w, out place);
		}

		private int FreeBed(Walker w, out int place)
		{
			place = 0;
			for (int i = 0; i < _beds.Count; i++)
			{
				int num = 0;
				int num2 = -1;
				foreach (Walker item in _list)
				{
					if (item != w && item.Bed == i)
					{
						num++;
						num2 = item.BedPlace;
					}
				}
				if (num < _beds[i].Places)
				{
					place = ((num != 0) ? (1 - num2) : 0);
					return i;
				}
			}
			return -1;
		}

		private Vector2 FootOf(Bed b, int place)
		{
			if (b.Places < 2)
			{
				return b.Foot;
			}
			Vector2 vector = new Vector2(0f - Mathf.Sin(b.Heading), Mathf.Cos(b.Heading));
			return b.Foot + vector * ((place == 0) ? ((0f - b.Width) * 0.22f) : (b.Width * 0.22f));
		}

		private void LieIn(Walker w, int bed, float lie)
		{
			Bed bed2 = _beds[bed];
			FreeBed(w, out w.BedPlace);
			w.Bed = bed;
			w.Lie = lie;
			w.BedSide = bed2.Side;
			if (_blockedAt != null && bed2.Level < _blockedAt.Length && !Free(bed2.Side, bed2.Level) && Nearest(bed2.Side, bed2.Level, out var cx, out var cy))
			{
				w.BedSide = CellCentre(cx, cy);
			}
			w.Level = bed2.Level;
			w.Pos = Vector2.Lerp(w.BedSide, FootOf(bed2, w.BedPlace), lie);
			w.Z = bed2.Floor + (bed2.Z - bed2.Floor) * lie;
			w.LieShown = lie;
			w.Heading = bed2.Heading;
			w.Spot = -1;
			w.Path.Clear();
			w.Speed = 0f;
			w.Sit = 0f;
		}

		private IEnumerable<int> Preferred(Walker w)
		{
			bool flag = CanSit(w.Who);
			List<int> list = new List<int>();
			List<int> rest = new List<int>();
			for (int i = 0; i < _room.Spots.Count; i++)
			{
				if (flag || !IsSeat(_room.Spots[i]))
				{
					string text = ((i < _room.SpotUse.Count) ? _room.SpotUse[i] : "");
					(((w.Role != HouseOccupancy.Doing.Work) ? ((w.Role != HouseOccupancy.Doing.Together) ? (text != "bed" && text != "door") : (IsSeat(_room.Spots[i]) || text == "bed")) : (text == "workbench" || text == "anvil" || text == "counter")) ? list : rest).Add(i);
				}
			}
			for (int num = list.Count - 1; num > 0; num--)
			{
				int index = _rng.Next(num + 1);
				int value = list[num];
				list[num] = list[index];
				list[index] = value;
			}
			for (int num2 = rest.Count - 1; num2 > 0; num2--)
			{
				int index2 = _rng.Next(num2 + 1);
				int value2 = rest[num2];
				rest[num2] = rest[index2];
				rest[index2] = value2;
			}
			foreach (int item in list)
			{
				yield return item;
			}
			foreach (int item2 in rest)
			{
				yield return item2;
			}
		}

		public void Clear()
		{
			_list.Clear();
			_fell.Clear();
			_beds.Clear();
			_room = null;
		}

		public void Pin(List<RoomSeat> seats)
		{
			for (int num = _list.Count - 1; num >= 0; num--)
			{
				if (_list[num].Pinned)
				{
					bool flag = false;
					foreach (RoomSeat seat in seats)
					{
						if (seat.Who == _list[num].Who)
						{
							flag = true;
							break;
						}
					}
					if (!flag)
					{
						_list.RemoveAt(num);
					}
				}
			}
			foreach (RoomSeat seat2 in seats)
			{
				if (seat2.Who == null)
				{
					continue;
				}
				Walker walker = Of(seat2.Who);
				if (walker == null || (!walker.Fighting && !walker.Fleeing))
				{
					if (walker == null)
					{
						List<Walker> list = _list;
						Walker obj = new Walker
						{
							Who = seat2.Who,
							Heading = seat2.Yaw * (MathF.PI / 180f)
						};
						walker = obj;
						list.Add(obj);
					}
					walker.Pinned = true;
					walker.Errand = null;
					walker.Pos = new Vector2(seat2.X, seat2.Y);
					walker.PinHeading = seat2.Yaw * (MathF.PI / 180f);
					walker.Sit = seat2.Sit;
					walker.Z = seat2.Z;
					walker.Spot = -1;
					walker.Path.Clear();
					walker.Speed = 0f;
				}
			}
		}

		public void Arrive(Actor a, Vector2 door)
		{
			if (_room != null && a != null && Of(a) == null)
			{
				Walker walker = new Walker
				{
					Who = a,
					Pos = door,
					Heading = Mathf.Atan2(_room.SpawnY - door.y, _room.SpawnX - door.x),
					Spot = -1,
					Role = HouseOccupancy.DoingOf(a)
				};
				_list.Add(walker);
				if ((!KeepInsideSpots || !InsideSpots.Where(a, out var _, out var place) || place.Use == RoomLayout.Use.Bed || place.Spot < 0 || place.Spot >= _room.Spots.Count || !(Mathf.Abs(_room.Spots[place.Spot][0] - place.X) < 0.05f) || !(Mathf.Abs(_room.Spots[place.Spot][1] - place.Y) < 0.05f) || !GoTo(walker, place.Spot)) && !SetOff(walker, new Vector2(-99f, -99f)))
				{
					walker.Wait = 1f;
				}
			}
		}

		private bool GoTo(Walker w, int s)
		{
			float[] array = _room.Spots[s];
			Vector2 vector = new Vector2(array[0], array[1]);
			Vector2 to = vector;
			if (IsSeat(array))
			{
				to = vector - new Vector2(Mathf.Cos(array[2] * (MathF.PI / 180f)), Mathf.Sin(array[2] * (MathF.PI / 180f))) * 0.36f;
			}
			int num = LevelOf(array);
			if (!Route(w, to, num))
			{
				return false;
			}
			if (IsSeat(array))
			{
				w.Path.Add(vector);
				w.PathZ.Add(LevelZ(num));
			}
			w.Spot = s;
			return true;
		}

		public void Leave(Actor a, Vector2 door)
		{
			Walker walker = Of(a);
			if (walker == null || walker.Pinned || walker.Leaving)
			{
				return;
			}
			walker.Leaving = true;
			walker.Errand = null;
			walker.Spot = -1;
			walker.Path.Clear();
			_door = door;
			if (!(walker.Lie > 0f))
			{
				if (_room != null)
				{
					Route(walker, door, 0);
				}
				if (_room != null)
				{
					Log.Info($"room: {WorldBoxMod.Describe(a)} sets off for the door {door} from {walker.Pos} (storey {walker.Level}): {walker.Path.Count} steps, to {((walker.Path.Count > 0) ? walker.Path[walker.Path.Count - 1] : walker.Pos)}");
				}
			}
		}

		public bool SendTo(Actor who, Vector2 to, int level, float faceYaw, Action onArrive)
		{
			Walker walker = Of(who);
			if (walker == null || _room == null || walker.Pinned || walker.Leaving || walker.Lie > 0f || walker.Bed >= 0 || walker.Role == HouseOccupancy.Doing.Sleep)
			{
				return false;
			}
			walker.Spot = -1;
			walker.Path.Clear();
			walker.PathZ.Clear();
			Route(walker, to, Mathf.Clamp(level, 0, Levels - 1));
			walker.Errand = onArrive ?? ((Action)delegate
			{
			});
			walker.ErrandYaw = faceYaw;
			walker.ErrandTime = 0f;
			walker.Wait = 0f;
			return true;
		}

		private static bool IsSeat(float[] spot)
		{
			if (spot.Length > 3)
			{
				return spot[3] > 0f;
			}
			return false;
		}

		private static bool CanSit(Actor a)
		{
			try
			{
				return a != null && People.IsHumanoid(a);
			}
			catch
			{
				return false;
			}
		}

		public Walker Of(Actor a)
		{
			for (int i = 0; i < _list.Count; i++)
			{
				if (_list[i].Who == a)
				{
					return _list[i];
				}
			}
			return null;
		}

		public bool Occupied(Vector2 p, float radius, int level = -1)
		{
			for (int i = 0; i < _list.Count; i++)
			{
				if ((level < 0 || _list[i].Level == level) && (p - _list[i].Pos).sqrMagnitude < (radius + 0.2f) * (radius + 0.2f) && Here(_list[i]))
				{
					return true;
				}
			}
			return false;
		}

		internal static bool Here(Walker w)
		{
			try
			{
				return w.Who != null && w.Who.isAlive() && w.Who.data != null && w.Who.data.id == w.Id;
			}
			catch
			{
				return false;
			}
		}

		public void DropDead()
		{
			for (int num = _list.Count - 1; num >= 0; num--)
			{
				Walker walker = _list[num];
				if (!Here(walker))
				{
					if (walker.Who != null)
					{
						_fell[walker.Who] = walker.Id;
					}
					_list.RemoveAt(num);
				}
			}
		}

		public bool FellHere(Actor a)
		{
			try
			{
				long value;
				return a != null && _fell.TryGetValue(a, out value) && a.data != null && a.data.id == value;
			}
			catch
			{
				return false;
			}
		}

		public void Tick(Vector2 you, ICollection<Actor> talking, float dt)
		{
			if (_room == null)
			{
				return;
			}
			DropDead();
			if (dt <= 0f)
			{
				return;
			}
			dt = Mathf.Min(dt, 0.1f);
			for (int i = 0; i < _list.Count; i++)
			{
				Walker walker = _list[i];
				if (walker.Fighting)
				{
					Fight(walker, dt);
				}
				else
				{
					if (walker.Bed >= 0 && Beds(walker, dt))
					{
						continue;
					}
					if (walker.Errand != null)
					{
						walker.ErrandTime += dt;
						if (walker.Path.Count == 0 || walker.ErrandTime > 15f)
						{
							walker.Path.Clear();
							walker.PathZ.Clear();
							walker.Speed = Mathf.MoveTowards(walker.Speed, 0f, 3.5f * dt);
							walker.Sit = Mathf.MoveTowards(walker.Sit, 0f, dt * 2.4f);
							walker.Heading = Mathf.LerpAngle(walker.Heading * 57.29578f, walker.ErrandYaw * 57.29578f, 1f - Mathf.Exp(-6f * dt)) * (MathF.PI / 180f);
							if (walker.Sit <= 0f && (Mathf.Abs(Mathf.DeltaAngle(walker.Heading * 57.29578f, walker.ErrandYaw * 57.29578f)) < 20f || walker.ErrandTime > 16f))
							{
								Action errand = walker.Errand;
								walker.Errand = null;
								walker.Spot = -1;
								walker.Wait = 0.8f;
								try
								{
									errand();
								}
								catch (Exception ex)
								{
									Log.Error("room: an errand: " + ex.Message);
								}
							}
							continue;
						}
					}
					if (walker.Pinned)
					{
						float num = ((walker.Sit <= 0f && talking.Contains(walker.Who)) ? Mathf.Atan2(you.y - walker.Pos.y, you.x - walker.Pos.x) : walker.PinHeading);
						walker.Speed = 0f;
						walker.Heading = Mathf.LerpAngle(walker.Heading * 57.29578f, num * 57.29578f, 1f - Mathf.Exp(-6f * dt)) * (MathF.PI / 180f);
						continue;
					}
					float num2 = walker.Heading;
					bool flag = false;
					bool flag2 = walker.Spot >= 0 && walker.Spot < _room.Spots.Count && IsSeat(_room.Spots[walker.Spot]) && CanSit(walker.Who);
					if (talking.Contains(walker.Who))
					{
						num2 = ((walker.Sit > 0.3f && walker.Spot >= 0 && walker.Spot < _room.Spots.Count) ? (_room.Spots[walker.Spot][2] * (MathF.PI / 180f)) : Mathf.Atan2(you.y - walker.Pos.y, you.x - walker.Pos.x));
						walker.Wait = Mathf.Max(walker.Wait, 2.5f);
					}
					else if (walker.Path.Count == 0 && (walker.Leaving || walker.Spot < 0))
					{
						walker.Wait -= dt;
						if (walker.Leaving)
						{
							num2 = Mathf.Atan2(_door.y - 1f - walker.Pos.y, _door.x - walker.Pos.x);
						}
						else if (!(walker.Wait <= 0f) || walker.Role != HouseOccupancy.Doing.Sleep || !CanSit(walker.Who) || !ToBed(walker))
						{
							if (walker.Wait <= 0f && walker.Role == HouseOccupancy.Doing.Sleep)
							{
								walker.Wait = 2f;
							}
							else if (walker.Wait <= 0f && !SetOff(walker, you))
							{
								walker.Wait = 1f;
							}
						}
					}
					else if (walker.Path.Count == 0)
					{
						float[] array = _room.Spots[walker.Spot];
						num2 = array[2] * (MathF.PI / 180f);
						if (flag2 && Mathf.Abs(Mathf.DeltaAngle(walker.Heading * 57.29578f, array[2])) < 25f)
						{
							walker.Sit = Mathf.MoveTowards(walker.Sit, 1f, dt * 1.8f);
						}
						walker.Wait -= dt;
						if (walker.Role != HouseOccupancy.Doing.Sleep || !CanSit(walker.Who) || !ToBed(walker))
						{
							if (walker.Role == HouseOccupancy.Doing.Sleep)
							{
								walker.Wait = Mathf.Max(walker.Wait, 2f);
							}
							else if (walker.Wait <= 0f && !SetOff(walker, you))
							{
								walker.Wait = 1f + (float)_rng.NextDouble() * 2f;
							}
						}
					}
					else
					{
						Vector2 rhs = walker.Path[0] - walker.Pos;
						num2 = Mathf.Atan2(rhs.y, rhs.x);
						Vector2 vector = walker.Pos + rhs.normalized * 0.32f;
						bool flag3 = YouLevel == walker.Level && (vector - you).sqrMagnitude < 0.16000001f;
						for (int j = 0; j < _list.Count; j++)
						{
							if (flag3)
							{
								break;
							}
							if (j != i && _list[j].Level == walker.Level && (vector - _list[j].Pos).sqrMagnitude < 0.115600005f && Vector2.Dot(_list[j].Pos - walker.Pos, rhs) > 0f)
							{
								flag3 = true;
							}
						}
						if (flag3)
						{
							walker.Blocked += dt;
							if (walker.Blocked > 2.5f)
							{
								walker.Blocked = 0f;
								if (walker.Leaving && (walker.Pos - _door).sqrMagnitude > 0.36f)
								{
									Route(walker, _door, 0);
								}
								else
								{
									walker.Path.Clear();
									walker.Wait = 0.5f + (float)_rng.NextDouble();
								}
							}
						}
						else
						{
							walker.Blocked = 0f;
							flag = true;
						}
					}
					if (flag && walker.Sit > 0f)
					{
						walker.Sit = Mathf.MoveTowards(walker.Sit, 0f, dt * 2.4f);
						if (walker.Sit > 0f)
						{
							flag = false;
							num2 = walker.Heading;
						}
					}
					walker.Speed = Mathf.MoveTowards(walker.Speed, (!flag) ? 0f : (walker.Fleeing ? 1.9f : 0.7f), ((!flag) ? 3.5f : (walker.Fleeing ? 5f : 2.2f)) * dt);
					walker.Heading = Mathf.LerpAngle(walker.Heading * 57.29578f, num2 * 57.29578f, 1f - Mathf.Exp(-6f * dt)) * (MathF.PI / 180f);
					if (walker.Path.Count <= 0 || !(walker.Speed > 0f))
					{
						continue;
					}
					Vector2 vector2 = walker.Path[0] - walker.Pos;
					float magnitude = vector2.magnitude;
					float num3 = walker.Speed * dt;
					if (Mathf.Abs(Mathf.DeltaAngle(walker.Heading * 57.29578f, Mathf.Atan2(vector2.y, vector2.x) * 57.29578f)) > 70f)
					{
						num3 *= 0.3f;
					}
					if (magnitude <= num3)
					{
						walker.Pos = walker.Path[0];
						if (walker.PathZ.Count > 0)
						{
							walker.Z = walker.PathZ[0];
							walker.PathZ.RemoveAt(0);
							walker.Level = LevelOfZ(walker.Z);
						}
						walker.SegFrom = walker.Pos;
						walker.SegFromZ = walker.Z;
						walker.Path.RemoveAt(0);
						if (walker.Path.Count == 0)
						{
							walker.Wait = 3f + (float)_rng.NextDouble() * 7f;
						}
					}
					else
					{
						walker.Pos += vector2 / magnitude * num3;
						if (walker.PathZ.Count > 0)
						{
							float magnitude2 = (walker.Path[0] - walker.SegFrom).magnitude;
							walker.Z = ((magnitude2 > 0.0001f) ? Mathf.Lerp(walker.SegFromZ, walker.PathZ[0], Mathf.Clamp01((walker.Pos - walker.SegFrom).magnitude / magnitude2)) : walker.PathZ[0]);
						}
					}
				}
			}
			_list.RemoveAll((Walker x) => x.Leaving && x.Path.Count == 0 && x.Lie <= 0f && (StillIn == null || !StillIn(x.Who)));
		}

		private void Fight(Walker w, float dt)
		{
			Vector2 chaseAt = w.ChaseAt;
			int chaseLevel = w.ChaseLevel;
			w.Errand = null;
			w.Pinned = false;
			w.Leaving = false;
			if (w.Bed >= 0)
			{
				w.Role = HouseOccupancy.Doing.Rest;
				if (Beds(w, dt))
				{
					return;
				}
			}
			w.Spot = -1;
			Vector2 vector = chaseAt - w.Pos;
			float magnitude = vector.magnitude;
			float num = ((magnitude > 0.001f) ? Mathf.Atan2(vector.y, vector.x) : w.Heading);
			if (w.Sit > 0f)
			{
				w.Sit = Mathf.MoveTowards(w.Sit, 0f, dt * 3f);
				w.Speed = 0f;
				w.Heading = Mathf.LerpAngle(w.Heading * 57.29578f, num * 57.29578f, 1f - Mathf.Exp(-6f * dt)) * (MathF.PI / 180f);
				return;
			}
			int num2;
			if (magnitude <= w.Reach)
			{
				num2 = ((w.Level == chaseLevel) ? 1 : 0);
				if (num2 != 0)
				{
					w.Path.Clear();
					w.PathZ.Clear();
					goto IL_01e0;
				}
			}
			else
			{
				num2 = 0;
			}
			if ((w.RouteIn -= dt) <= 0f || w.Path.Count == 0)
			{
				w.RouteIn = 0.35f;
				Vector2 vector2 = ((magnitude > 0.001f) ? (chaseAt - vector / magnitude * Mathf.Min(w.Reach * 0.8f, magnitude)) : chaseAt);
				if (!Route(w, vector2, Mathf.Clamp(chaseLevel, 0, Levels - 1)) && w.Level == chaseLevel)
				{
					w.Path.Clear();
					w.PathZ.Clear();
					w.Path.Add(vector2);
					w.PathZ.Add(LevelZ(w.Level));
				}
			}
			goto IL_01e0;
			IL_01e0:
			bool flag = num2 == 0 && w.Path.Count > 0;
			w.Speed = Mathf.MoveTowards(w.Speed, flag ? 1.9f : 0f, (flag ? 6f : 8f) * dt);
			float num3 = num;
			if (flag)
			{
				Vector2 vector3 = w.Path[0] - w.Pos;
				if (vector3.sqrMagnitude > 1E-06f && magnitude > w.Reach + 0.6f)
				{
					num3 = Mathf.Atan2(vector3.y, vector3.x);
				}
			}
			w.Heading = Mathf.LerpAngle(w.Heading * 57.29578f, num3 * 57.29578f, 1f - Mathf.Exp(-9.6f * dt)) * (MathF.PI / 180f);
			if (!flag || w.Speed <= 0f)
			{
				return;
			}
			Vector2 vector4 = w.Path[0] - w.Pos;
			float magnitude2 = vector4.magnitude;
			float num4 = w.Speed * dt;
			if (magnitude2 <= num4)
			{
				w.Pos = w.Path[0];
				if (w.PathZ.Count > 0)
				{
					w.Z = w.PathZ[0];
					w.PathZ.RemoveAt(0);
					w.Level = LevelOfZ(w.Z);
				}
				w.SegFrom = w.Pos;
				w.SegFromZ = w.Z;
				w.Path.RemoveAt(0);
			}
			else
			{
				w.Pos += vector4 / magnitude2 * num4;
				if (w.PathZ.Count > 0)
				{
					float magnitude3 = (w.Path[0] - w.SegFrom).magnitude;
					w.Z = ((magnitude3 > 0.0001f) ? Mathf.Lerp(w.SegFromZ, w.PathZ[0], Mathf.Clamp01((w.Pos - w.SegFrom).magnitude / magnitude3)) : w.PathZ[0]);
				}
			}
		}

		public void SetFight(Actor a, bool fight, bool flee)
		{
			Walker walker = Of(a);
			if (walker == null)
			{
				return;
			}
			walker.Fighting = fight;
			walker.Fleeing = flee && !fight;
			if (fight)
			{
				walker.Leaving = false;
				walker.Pinned = false;
				walker.RouteIn = 0f;
			}
			if (walker.Fleeing && !walker.Leaving)
			{
				walker.Pinned = false;
				if (walker.Bed >= 0)
				{
					walker.Role = HouseOccupancy.Doing.Rest;
				}
				Leave(a, _door);
			}
		}

		public void Drop(Actor a)
		{
			for (int num = _list.Count - 1; num >= 0; num--)
			{
				if (_list[num].Who == a)
				{
					_list.RemoveAt(num);
				}
			}
		}

		private bool ToBed(Walker w)
		{
			int place;
			int num = FreeBed(w, out place);
			if (num < 0)
			{
				return false;
			}
			w.BedPlace = place;
			if (!Route(w, _beds[num].Side, _beds[num].Level))
			{
				if (!w.NoWayToBed)
				{
					Log.Info($"room: no way to the bed from {w.Pos} (storey {w.Level}) to {_beds[num].Side} (storey {_beds[num].Level}) for " + WorldBoxMod.Describe(w.Who));
				}
				w.NoWayToBed = true;
				return false;
			}
			w.Spot = -1;
			w.Bed = num;
			w.BedSide = ((w.Path.Count > 0) ? w.Path[w.Path.Count - 1] : _beds[num].Side);
			return true;
		}

		private bool Beds(Walker w, float dt)
		{
			if (w.Bed >= _beds.Count)
			{
				w.Bed = -1;
				w.Lie = 0f;
				w.Z = 0f;
				return false;
			}
			Bed bed = _beds[w.Bed];
			bool flag = w.Role == HouseOccupancy.Doing.Sleep && !w.Leaving;
			if (w.Path.Count > 0)
			{
				if (flag)
				{
					return false;
				}
				w.Path.Clear();
			}
			if (flag && w.Lie <= 0f && (w.Pos - w.BedSide).sqrMagnitude > 0.0625f)
			{
				w.Bed = -1;
				return false;
			}
			w.Lie = Mathf.MoveTowards(w.Lie, flag ? 1f : 0f, dt * (flag ? 0.7f : 1.1f));
			w.Speed = 0f;
			w.Sit = 0f;
			w.Heading = Mathf.LerpAngle(w.Heading * 57.29578f, bed.Heading * 57.29578f, 1f - Mathf.Exp(-6f * dt)) * (MathF.PI / 180f);
			if (bed.Loft)
			{
				float num = Mathf.Clamp01(w.Lie * 2f);
				float num2 = Mathf.Clamp01(w.Lie * 2f - 1f);
				w.Pos = Vector2.Lerp(w.BedSide, FootOf(bed, w.BedPlace), num2);
				w.Z = bed.Floor + (bed.Z - bed.Floor) * num;
				w.LieShown = num2;
			}
			else
			{
				w.Pos = Vector2.Lerp(w.BedSide, FootOf(bed, w.BedPlace), w.Lie);
				w.Z = bed.Floor + (bed.Z - bed.Floor) * w.Lie;
				w.LieShown = w.Lie;
			}
			if (!flag && w.Lie <= 0f)
			{
				w.Bed = -1;
				w.Level = bed.Level;
				w.Z = bed.Floor;
				w.LieShown = 0f;
				w.Wait = 0.5f;
				if (w.Leaving)
				{
					Route(w, _door, 0);
				}
			}
			return true;
		}

		private bool SetOff(Walker w, Vector2 you)
		{
			List<int> list = new List<int>();
			foreach (int item in Preferred(w))
			{
				if (item == w.Spot)
				{
					continue;
				}
				Vector2 vector = new Vector2(_room.Spots[item][0], _room.Spots[item][1]);
				if ((vector - you).sqrMagnitude < 0.36f || (YouSeat.HasValue && (vector - YouSeat.Value).sqrMagnitude < 0.25f))
				{
					continue;
				}
				bool flag = false;
				foreach (Walker item2 in _list)
				{
					if (item2 != w && (item2.Spot == item || (item2.Pos - vector).sqrMagnitude < 0.25f))
					{
						flag = true;
					}
				}
				if (!flag)
				{
					list.Add(item);
				}
			}
			while (list.Count > 0)
			{
				int num = list[0];
				list.RemoveAt(0);
				float[] array = _room.Spots[num];
				Vector2 vector2 = new Vector2(array[0], array[1]);
				Vector2 to = vector2;
				if (IsSeat(array))
				{
					to = vector2 - new Vector2(Mathf.Cos(array[2] * (MathF.PI / 180f)), Mathf.Sin(array[2] * (MathF.PI / 180f))) * 0.36f;
				}
				int num2 = LevelOf(array);
				if (Route(w, to, num2))
				{
					if (IsSeat(array))
					{
						w.Path.Add(vector2);
						w.PathZ.Add(LevelZ(num2));
					}
					w.Spot = num;
					return true;
				}
			}
			return false;
		}

		private void BuildGrid()
		{
			_x0 = _room.MinX - 0.1f;
			_y0 = _room.MinY - 0.1f;
			_w = Mathf.CeilToInt((_room.MaxX + 0.1f - _x0) / 0.1f) + 1;
			_h = Mathf.CeilToInt((_room.MaxY + 0.1f - _y0) / 0.1f) + 1;
			FitInfo fit = _room.Fit;
			_blockedAt = new bool[Levels][];
			for (int i = 0; i < _blockedAt.Length; i++)
			{
				bool[] array = new bool[_w * _h];
				float num = LevelZ(i);
				float[] array2 = ((i > 0 && fit != null) ? fit.Stairwell : null);
				for (int j = 0; j < _h; j++)
				{
					for (int k = 0; k < _w; k++)
					{
						Vector2 vector = CellCentre(k, j);
						bool flag = ((fit == null) ? (vector.x < _room.MinX || vector.x > _room.MaxX || vector.y < _room.MinY || vector.y > _room.MaxY) : (!fit.Clear(i, vector.x, vector.y, 0.12f) && (i != 0 || !((vector - _door).sqrMagnitude < 0.09f))));
						if (array2 != null && vector.x > array2[0] - 0.12f && vector.x < array2[2] + 0.12f && vector.y > array2[1] - 0.12f && vector.y < array2[3] + 0.12f)
						{
							flag = true;
						}
						if (!flag)
						{
							foreach (float[] solid in _room.Solids)
							{
								if (!((solid.Length >= 6) ? (Mathf.Abs(solid[4] - num) > 0.3f) : (i > 0)) && vector.x > solid[0] - 0.12f && vector.x < solid[2] + 0.12f && vector.y > solid[1] - 0.12f && vector.y < solid[3] + 0.12f)
								{
									flag = true;
									break;
								}
							}
						}
						array[j * _w + k] = flag;
					}
				}
				_blockedAt[i] = array;
			}
		}

		private bool Route(Walker w, Vector2 to, int toLevel)
		{
			FitInfo fitInfo = ((_room != null) ? _room.Fit : null);
			List<Vector2> path = new List<Vector2>();
			List<float> zs = new List<float>();
			if (toLevel == w.Level || fitInfo == null || float.IsNaN(fitInfo.StairFootX) || toLevel >= Levels)
			{
				List<Vector2> list = FindPath(w.Pos, to, w.Level);
				if (list == null)
				{
					return false;
				}
				Add(list, w.Level);
			}
			else
			{
				Vector2 vector = new Vector2(fitInfo.StairFootX, fitInfo.StairFootY);
				Vector2 vector2 = new Vector2(fitInfo.StairHeadX, fitInfo.StairHeadY);
				Vector2 normalized = (vector2 - vector).normalized;
				Vector2 vector3 = vector - normalized * 0.35f;
				Vector2 vector4 = vector2 + normalized * 0.35f;
				bool flag = toLevel > w.Level;
				List<Vector2> list2 = FindPath(w.Pos, flag ? vector3 : vector4, w.Level);
				List<Vector2> list3 = FindPath(flag ? vector4 : vector3, to, toLevel);
				if (list2 == null || list3 == null)
				{
					return false;
				}
				Add(list2, w.Level);
				if (flag)
				{
					path.Add(vector);
					zs.Add(LevelZ(0));
					path.Add(vector2);
					zs.Add(LevelZ(1));
					path.Add(vector4);
					zs.Add(LevelZ(1));
				}
				else
				{
					path.Add(vector2);
					zs.Add(LevelZ(1));
					path.Add(vector);
					zs.Add(LevelZ(0));
					path.Add(vector3);
					zs.Add(LevelZ(0));
				}
				Add(list3, toLevel);
			}
			w.Path.Clear();
			w.PathZ.Clear();
			w.Path.AddRange(path);
			w.PathZ.AddRange(zs);
			w.SegFrom = w.Pos;
			w.SegFromZ = w.Z;
			return true;
			void Add(List<Vector2> part, int level)
			{
				foreach (Vector2 item in part)
				{
					path.Add(item);
					zs.Add(LevelZ(level));
				}
			}
		}

		private Vector2 CellCentre(int x, int y)
		{
			return new Vector2(_x0 + ((float)x + 0.5f) * 0.1f, _y0 + ((float)y + 0.5f) * 0.1f);
		}

		private bool CellOf(Vector2 p, out int x, out int y)
		{
			x = Mathf.FloorToInt((p.x - _x0) / 0.1f);
			y = Mathf.FloorToInt((p.y - _y0) / 0.1f);
			if (x >= 0 && y >= 0 && x < _w)
			{
				return y < _h;
			}
			return false;
		}

		private bool Free(Vector2 p, int level)
		{
			if (CellOf(p, out var x, out var y))
			{
				return !_blockedAt[level][y * _w + x];
			}
			return false;
		}

		private bool Nearest(Vector2 p, int level, out int cx, out int cy)
		{
			bool[] array = _blockedAt[level];
			CellOf(p, out cx, out cy);
			cx = Mathf.Clamp(cx, 0, _w - 1);
			cy = Mathf.Clamp(cy, 0, _h - 1);
			if (!array[cy * _w + cx])
			{
				return true;
			}
			int num = -1;
			int num2 = -1;
			float num3 = float.MaxValue;
			for (int i = -7; i < 8; i++)
			{
				for (int j = -7; j < 8; j++)
				{
					int num4 = cx + j;
					int num5 = cy + i;
					if (num4 >= 0 && num5 >= 0 && num4 < _w && num5 < _h && !array[num5 * _w + num4])
					{
						float sqrMagnitude = (CellCentre(num4, num5) - p).sqrMagnitude;
						if (sqrMagnitude < num3)
						{
							num3 = sqrMagnitude;
							num = num4;
							num2 = num5;
						}
					}
				}
			}
			if (num < 0)
			{
				return false;
			}
			cx = num;
			cy = num2;
			return true;
		}

		private List<Vector2> FindPath(Vector2 from, Vector2 to, int level = 0)
		{
			if (level < 0 || level >= _blockedAt.Length)
			{
				level = 0;
			}
			bool[] array = _blockedAt[level];
			if (!Nearest(from, level, out var cx, out var cy) || !Nearest(to, level, out var tx, out var ty))
			{
				return null;
			}
			int num = _w * _h;
			int num2 = cy * _w + cx;
			int num3 = ty * _w + tx;
			if (_pathCost == null || _pathCost.Length < num)
			{
				_pathCost = new float[num];
				_pathCame = new int[num];
				_pathDone = new bool[num];
			}
			float[] pathCost = _pathCost;
			int[] pathCame = _pathCame;
			bool[] pathDone = _pathDone;
			for (int i = 0; i < num; i++)
			{
				pathCost[i] = float.MaxValue;
				pathCame[i] = -1;
			}
			Array.Clear(pathDone, 0, num);
			pathCost[num2] = 0f;
			List<(float F, int C)> heap = _pathHeap;
			heap.Clear();
			Push(H(num2), num2);
			while (heap.Count > 0)
			{
				int num4 = Pop();
				if (num4 == num3)
				{
					break;
				}
				if (pathDone[num4])
				{
					continue;
				}
				pathDone[num4] = true;
				int num5 = num4 % _w;
				int num6 = num4 / _w;
				for (int j = -1; j <= 1; j++)
				{
					for (int k = -1; k <= 1; k++)
					{
						if (k == 0 && j == 0)
						{
							continue;
						}
						int num7 = num5 + k;
						int num8 = num6 + j;
						if (num7 < 0 || num8 < 0 || num7 >= _w || num8 >= _h)
						{
							continue;
						}
						int num9 = num8 * _w + num7;
						if (!array[num9] && !pathDone[num9] && (k == 0 || j == 0 || (!array[num6 * _w + num7] && !array[num8 * _w + num5])))
						{
							float num10 = pathCost[num4] + ((k != 0 && j != 0) ? 1.4142f : 1f);
							if (!(num10 >= pathCost[num9]))
							{
								pathCost[num9] = num10;
								pathCame[num9] = num4;
								Push(num10 + H(num9), num9);
							}
						}
					}
				}
			}
			bool flag = pathCame[num3] >= 0 || num3 == num2;
			if (!flag)
			{
				int num11 = -1;
				float num12 = 0.25f;
				for (int l = 0; l < num; l++)
				{
					if (pathCost[l] != float.MaxValue)
					{
						float sqrMagnitude = (CellCentre(l % _w, l / _w) - to).sqrMagnitude;
						if (sqrMagnitude < num12)
						{
							num12 = sqrMagnitude;
							num11 = l;
						}
					}
				}
				if (num11 < 0)
				{
					return null;
				}
				num3 = num11;
			}
			List<Vector2> pathCells = _pathCells;
			pathCells.Clear();
			int num13 = num3;
			while (num13 >= 0 && num13 != num2)
			{
				pathCells.Add(CellCentre(num13 % _w, num13 / _w));
				num13 = pathCame[num13];
			}
			pathCells.Reverse();
			if (pathCells.Count == 0)
			{
				pathCells.Add(flag ? to : CellCentre(num3 % _w, num3 / _w));
			}
			else if (flag)
			{
				pathCells[pathCells.Count - 1] = to;
			}
			List<Vector2> list = new List<Vector2>();
			Vector2 a = from;
			int num14 = 0;
			while (num14 < pathCells.Count)
			{
				int num15 = num14;
				for (int num16 = pathCells.Count - 1; num16 > num14; num16--)
				{
					if (Clear(a, pathCells[num16], level))
					{
						num15 = num16;
						break;
					}
				}
				list.Add(pathCells[num15]);
				a = pathCells[num15];
				num14 = num15 + 1;
			}
			return list;
			float H(int c)
			{
				return Mathf.Sqrt((c % _w - tx) * (c % _w - tx) + (c / _w - ty) * (c / _w - ty));
			}
			int Pop()
			{
				int item = heap[0].C;
				(float, int) tuple = heap[heap.Count - 1];
				heap.RemoveAt(heap.Count - 1);
				int count = heap.Count;
				int num17 = 0;
				if (count > 0)
				{
					while (true)
					{
						int num18 = num17 * 2 + 1;
						int num19 = num18 + 1;
						int num20 = num17;
						var (num21, _) = tuple;
						if (num18 < count && heap[num18].F < num21)
						{
							num20 = num18;
							num21 = heap[num18].F;
						}
						if (num19 < count && heap[num19].F < num21)
						{
							num20 = num19;
						}
						if (num20 == num17)
						{
							break;
						}
						heap[num17] = heap[num20];
						num17 = num20;
					}
					heap[num17] = tuple;
				}
				return item;
			}
			void Push(float f, int c)
			{
				heap.Add((f, c));
				int num17 = heap.Count - 1;
				while (num17 > 0)
				{
					int num18 = (num17 - 1) / 2;
					if (heap[num18].F <= f)
					{
						break;
					}
					heap[num17] = heap[num18];
					num17 = num18;
				}
				heap[num17] = (f, c);
			}
		}

		private bool Clear(Vector2 a, Vector2 b, int level)
		{
			float magnitude = (b - a).magnitude;
			int num = Mathf.Max(1, Mathf.CeilToInt(magnitude / 0.05f));
			for (int i = 1; i < num; i++)
			{
				if (!Free(Vector2.Lerp(a, b, (float)i / (float)num), level))
				{
					return false;
				}
			}
			return true;
		}
	}
}
