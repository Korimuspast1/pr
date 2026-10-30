using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class Creatures
	{
		private sealed class ShipsHeld
		{
			public readonly List<(State St, float Scale, float Wheel, float Heading)> Ships = new List<(State, float, float, float)>();

			public readonly List<(Mooring M, Vector2 Offset, Vector2 Target, Vector2 CheckedAt, float CheckedHeading, float NextCheck)> Berths = new List<(Mooring, Vector2, Vector2, Vector2, float, float)>();
		}

		private sealed class State
		{
			public object Space;

			public MeshInstance Mesh;

			public string Family;

			public uint[] Palette;

			public float Phase;

			public float Scale;

			public float Seed;

			public int Seen;

			public int Drawn = -10;

			public float Heading;

			public bool Fallen;

			public bool Boat;

			public long Id;

			public float Lift;

			public float Breath;

			public float Prowl;

			public float Lifted;

			public float Tall;

			public float Wheel;

			public int Posed = -10;

			public Riding Ride;
		}

		public struct Fire
		{
			public Vec3 Mouth;

			public Vec3 Dir;

			public float Reach;

			public float Width;

			public float Strength;

			public uint Seed;
		}

		internal sealed class ShipInfo
		{
			public float Length;

			public float Ghostly;
		}

		private sealed class Mooring
		{
			public Vector2 Offset;

			public Vector2 Target;

			public Vector2 CheckedAt = new Vector2(float.NaN, float.NaN);

			public float CheckedHeading;

			public float NextCheck;

			public int Seen;

			public Vector2 Spread;

			public Vector2 At;

			public float Heading;

			public float Half;

			public float Beam;
		}

		private sealed class Riding
		{
			public float Heave;

			public float HeaveV;

			public float Pitch;

			public float PitchV;

			public float Roll;

			public float RollV;

			public float At = float.NaN;
		}

		internal const float KeepSmallFrom = 20f;

		public static readonly List<Fire> Breaths = new List<Fire>();

		private const float RealScale = 0.5714286f;

		private static readonly Dictionary<Actor, State> States = new Dictionary<Actor, State>();

		private static readonly Dictionary<(MeshModel, SpriteImage), uint[]> SidePalettes = new Dictionary<(MeshModel, SpriteImage), uint[]>();

		private static readonly List<Actor> Stale = new List<Actor>();

		private static int _frame;

		private static readonly MeshInstance HiddenShadow = new MeshInstance
		{
			ShadowOnly = true
		};

		private static readonly MeshInstance YourMuzzle = new MeshInstance
		{
			NoShadow = true
		};

		private static readonly Dictionary<MeshModel, ShipInfo> ShipInfos = new Dictionary<MeshModel, ShipInfo>();

		private static State _figure;

		private static State _figureFrom;

		private static readonly Dictionary<Actor, Mooring> Moorings = new Dictionary<Actor, Mooring>();

		private static readonly List<Actor> StaleMoorings = new List<Actor>();

		private static readonly float[] HullAlong = new float[5] { -0.85f, -0.425f, 0f, 0.425f, 0.85f };

		private static readonly float[] HullTaper = new float[5] { 0.55f, 0.9f, 1f, 0.9f, 0.6f };

		private static readonly float[] SpreadTurns = new float[5]
		{
			0f,
			MathF.PI / 4f,
			-MathF.PI / 4f,
			MathF.PI / 2f,
			-MathF.PI / 2f
		};

		private const float RideFull = 80f;

		private static TerrainCache SeaLand
		{
			get
			{
				if (ReliefWorld.Sampler == null || !ReliefWorld.Sampler.Ready)
				{
					return null;
				}
				return ReliefWorld.Sampler.Cache;
			}
		}

		internal static object HoldShipsForTest()
		{
			ShipsHeld shipsHeld = new ShipsHeld();
			foreach (KeyValuePair<Actor, State> state in States)
			{
				if (state.Value.Boat)
				{
					shipsHeld.Ships.Add((state.Value, state.Value.Scale, state.Value.Wheel, state.Value.Heading));
				}
			}
			foreach (KeyValuePair<Actor, Mooring> mooring in Moorings)
			{
				Mooring value = mooring.Value;
				shipsHeld.Berths.Add((value, value.Offset, value.Target, value.CheckedAt, value.CheckedHeading, value.NextCheck));
			}
			return shipsHeld;
		}

		internal static void PutBackShipsForTest(object taken)
		{
			if (!(taken is ShipsHeld shipsHeld))
			{
				return;
			}
			foreach (var ship in shipsHeld.Ships)
			{
				ship.St.Scale = ship.Scale;
				ship.St.Wheel = ship.Wheel;
				ship.St.Heading = ship.Heading;
			}
			foreach (var berth in shipsHeld.Berths)
			{
				berth.M.Offset = berth.Offset;
				berth.M.Target = berth.Target;
				berth.M.CheckedAt = berth.CheckedAt;
				berth.M.CheckedHeading = berth.CheckedHeading;
				berth.M.NextCheck = berth.NextCheck;
			}
		}

		public static bool Airborne(Actor a)
		{
			if (a != null && States.TryGetValue(a, out var value))
			{
				return value.Lift > 0.5f;
			}
			return false;
		}

		public static bool DragonPose(Actor a, out float lift, out float breath, out float seed)
		{
			if (a != null && States.TryGetValue(a, out var value) && value.Family == "monster_dragon")
			{
				lift = value.Lift;
				breath = value.Breath;
				seed = value.Seed;
				return true;
			}
			lift = (breath = (seed = 0f));
			return false;
		}

		private static uint[] SidePalette(MeshModel model, SpriteImage img)
		{
			if (img == null)
			{
				return Models.CreaturePalette(model, img);
			}
			if (SidePalettes.TryGetValue((model, img), out var value))
			{
				return value;
			}
			if (SidePalettes.Count > 600)
			{
				SidePalettes.Clear();
			}
			value = CreatureColors.FromSideSprite(model, img);
			SidePalettes[(model, img)] = value;
			return value;
		}

		public static void Tick()
		{
			_frame++;
			Breaths.Clear();
			Butchery.Tick(Time.unscaledDeltaTime);
			foreach (KeyValuePair<Actor, State> state in States)
			{
				State value = state.Value;
				if (value.Fallen || value.Boat || value.Drawn != _frame - 1 || value.Mesh == null || value.Mesh.Model == null)
				{
					continue;
				}
				bool flag;
				try
				{
					flag = state.Key != null && state.Key.isAlive();
				}
				catch
				{
					flag = false;
				}
				if (flag)
				{
					continue;
				}
				value.Fallen = true;
				RoomCombat.FallingIn = value.Space;
				try
				{
					bool flag2 = Slicing.Split(state.Key, value.Mesh, null, beast: true);
					float lie = ((value.Space != null) ? 0f : Butchery.Fell(state.Key, flag2));
					if (!flag2)
					{
						long carcass = 0L;
						try
						{
							carcass = state.Key.data.id;
						}
						catch
						{
						}
						Corpses.Topple(value.Mesh, value.Heading, value.Seed % 1f, lie, carcass);
					}
				}
				finally
				{
					RoomCombat.FallingIn = null;
				}
			}
			if (_frame % 300 != 0)
			{
				return;
			}
			Stale.Clear();
			foreach (KeyValuePair<Actor, State> state2 in States)
			{
				if (_frame - state2.Value.Seen > 600)
				{
					Stale.Add(state2.Key);
				}
			}
			foreach (Actor item in Stale)
			{
				States.Remove(item);
			}
		}

		internal static string DrawnModel(Actor a)
		{
			if (a == null || !States.TryGetValue(a, out var value) || value.Mesh == null || value.Mesh.Model == null)
			{
				return null;
			}
			return value.Mesh.Model.Name;
		}

		internal static float DrawnSize(Actor a)
		{
			if (a == null || !States.TryGetValue(a, out var value) || value.Mesh == null || value.Mesh.Model == null)
			{
				return 0f;
			}
			CreatureRig creatureRig = Models.CreatureRigFor(value.Mesh.Model);
			return value.Scale * Mathf.Max(creatureRig.Height, creatureRig.Length);
		}

		private static State StateOf(Actor a, string family)
		{
			long num = -1L;
			try
			{
				num = ((a.data != null) ? a.data.id : (-1));
			}
			catch
			{
			}
			if (!States.TryGetValue(a, out var value) || value.Family != family || value.Id != num)
			{
				value = new State
				{
					Family = family,
					Mesh = new MeshInstance(),
					Seed = (float)(a.GetHashCode() & 0x3FF) / 97f,
					Id = num
				};
				States[a] = value;
			}
			value.Seen = _frame;
			return value;
		}

		private static float Hover(string family)
		{
			switch (family)
			{
			case "animal_bee":
				return 0.35f;
			case "animal_fly":
				return 0.45f;
			case "animal_butterfly":
				return 0.55f;
			case "monster_fairy":
				return 0.45f;
			case "monster_crystal_sword":
				return 0.25f;
			case "monster_ghost":
				return 0.15f;
			default:
				return 0f;
			}
		}

		public static bool Add(List<MeshInstance> meshes, Actor a, SpriteImage img, float x, float y, float z, float spriteW, float spriteH, float heading, float speed, float distance, float dt, int tag, uint flash, uint multiply, bool swimming, float walkDt = -1f)
		{
			string text = Models.CreatureFamily(a);
			if (text == null)
			{
				return false;
			}
			State state = StateOf(a, text);
			MeshModel meshModel = Models.DetailedCreature(text, distance, state.Mesh.Model);
			if (meshModel == null)
			{
				return false;
			}
			CreatureRig creatureRig = Models.CreatureRigFor(meshModel);
			MeshInstance mesh = state.Mesh;
			mesh.Model = meshModel;
			if (state.Palette == null)
			{
				state.Palette = (MutationLook.Owns(meshModel) ? new uint[meshModel.SlotNames.Length] : SidePalette(meshModel, img));
			}
			mesh.Palette = state.Palette;
			float num;
			if (text.StartsWith("animal_", StringComparison.Ordinal))
			{
				bool flag = true;
				try
				{
					flag = a.isAdult();
				}
				catch
				{
				}
				num = 0.5714286f * ((flag || creatureRig.Young) ? 1f : 0.55f) * (0.94f + 0.12f * (state.Seed % 1f));
			}
			else
			{
				num = Mathf.Max(spriteH * img.VisibleFraction, spriteW * Models.OpaqueWidth(img) * 0.9f) / Mathf.Max(0.05f, Mathf.Max(creatureRig.Height, creatureRig.Length));
				if (Mutations.CreatureShare(a, text, out var share))
				{
					num = spriteH * share / Mathf.Max(0.05f, creatureRig.Height);
				}
			}
			state.Scale = ((state.Scale <= 0f) ? num : Mathf.Lerp(state.Scale, num, 1f - Mathf.Exp(-2f * dt)));
			float scale = state.Scale;
			float num2 = creatureRig.Height * scale;
			float num3 = Mathf.Clamp01(speed / Mathf.Max(0.05f, num2 * 1.5f));
			float num4 = creatureRig.LegLength * scale;
			float num5 = ((walkDt >= 0f) ? walkDt : dt);
			if (num4 > 0.02f && !swimming)
			{
				float num6 = speed / num4;
				float num7 = Mathf.Clamp(0.22f + 0.1f * num6, 0.22f, 0.6f);
				num3 = Mathf.Clamp01(speed / Mathf.Max(0.05f, num4 * 0.6f)) * num7 / creatureRig.SwingPerStride;
				state.Phase += num5 * MathF.PI * speed / (2f * num4 * Mathf.Sin(num7));
			}
			else
			{
				state.Phase += num5 * (speed / Mathf.Max(0.04f, num2 * 0.5f) + (swimming ? 2f : 0f));
			}
			float attack = 0f;
			try
			{
				float num8 = WorldPace.Since(a.last_attack_timestamp);
				if (num8 >= 0f && num8 < 0.45f)
				{
					attack = num8 / 0.45f;
				}
			}
			catch
			{
			}
			float num9 = Hover(text);
			float num10 = z;
			bool flag2 = num9 > 0f || text == "monster_ufo";
			float now = PoseClock.Now;
			bool flag3 = text == "monster_dragon";
			DragonState dragonState = DragonState.Idle;
			if (flag3)
			{
				bool flag4 = false;
				try
				{
					Dragon actorComponent = a.getActorComponent<Dragon>();
					if (actorComponent != null)
					{
						dragonState = actorComponent.state;
						flag4 = true;
					}
				}
				catch
				{
				}
				bool flag5;
				try
				{
					flag5 = ((BaseSimObject)a).isFlying();
				}
				catch
				{
					flag5 = false;
				}
				float num11 = Mathf.Max(7f, num2 * 1.8f);
				float num12;
				if (!flag4)
				{
					num12 = (flag5 ? num11 : 0f);
				}
				else
				{
					switch (dragonState)
					{
					default:
						num12 = 0f;
						break;
					case DragonState.Slide:
						num12 = Mathf.Max(1.5f, num2 * 0.5f);
						break;
					case DragonState.Fly:
					case DragonState.Up:
						num12 = num11;
						break;
					}
				}
				float num13 = num12;
				state.Lift = Mathf.MoveTowards(state.Lift, num13, ((num13 > state.Lift) ? (2.2f + num11 * 0.15f) : 6f) * dt);
				state.Breath = Mathf.MoveTowards(state.Breath, (flag4 && (dragonState == DragonState.LandAttack || dragonState == DragonState.Slide)) ? 1f : 0f, dt * 4f);
				flag2 = state.Lift > 0.25f || dragonState == DragonState.Up;
				if (flag2)
				{
					attack = 0f;
				}
			}
			creatureRig.Pose(mesh, swimming ? Mathf.Max(0.4f, num3) : num3, state.Phase, now, flag2, attack, state.Seed);
			if (creatureRig.Prowls)
			{
				bool flag6 = false;
				try
				{
					flag6 = a.has_attack_target && !swimming;
				}
				catch
				{
				}
				state.Prowl = Mathf.MoveTowards(state.Prowl, flag6 ? 1f : 0f, dt * 2.5f);
				creatureRig.Prowl(mesh, state.Prowl);
			}
			float num14 = (flag2 ? 0f : WindUps.Coil(a));
			if (num14 > 0f)
			{
				Ward.Coil(mesh, creatureRig.HeadPart, num14);
			}
			if (Headshots.KnockOf(a, out var age))
			{
				Ward.HeadKnock(mesh, creatureRig.HeadPart, age);
			}
			bool flag7 = a == BeastView.Inside;
			mesh.Hidden = (flag7 ? BeastView.HiddenParts(meshModel) : 0);
			Mutations.Creature(a, mesh, state.Phase, num3);
			if (flag7 && creatureRig.HeadPart >= 0)
			{
				mesh.PartRotation[creatureRig.HeadPart] = BeastView.HeadTurn;
			}
			if (flag3)
			{
				creatureRig.Breathe(mesh, state.Breath, now);
				z += state.Lift + (flag2 ? (0.25f * num2 * Mathf.Sin(now * 3.2f + state.Seed - 0.6f) * Mathf.Min(1f, state.Lift / 3f)) : 0f);
			}
			if (num9 > 0f)
			{
				z += num9 + 0.08f * Mathf.Sin(now * 2.3f + state.Seed * 5f);
			}
			if (swimming)
			{
				z = ((ReliefWorld.Sampler != null && ReliefWorld.Sampler.Ready) ? ReliefWorld.Sampler.Cache.LiquidSurface(x, y) : 0f) - num2 * ((text == "animal_fish") ? 0.75f : 0.5f) + ((SeaLand != null) ? SeaLand.WaveAt(x, y) : 0f);
			}
			mesh.Place(x, y, z, heading, scale, scale, scale);
			if (flag3 && state.Breath > 0.02f && creatureRig.HeadPart >= 0)
			{
				Vec3 mouth = PersonRig.Transform(mesh, creatureRig.HeadPart, creatureRig.Mouth);
				Vec3 vec = PersonRig.TransformDir(mesh, creatureRig.HeadPart, new Vec3(0f, -1f, 0f)).Normalized();
				float num15 = ((dragonState == DragonState.Slide) ? 0.75f : 0.32f);
				Vec3 dir = (vec + new Vec3(0f, 0f, 0f - num15)).Normalized();
				Breaths.Add(new Fire
				{
					Mouth = mouth,
					Dir = dir,
					Reach = Mathf.Max(5f, num2 * 2.4f),
					Width = Mathf.Clamp(num2 * 0.3f, 0.6f, 2.5f),
					Strength = state.Breath,
					Seed = (uint)(state.Seed * 7919f)
				});
			}
			state.Drawn = _frame;
			state.Space = (RoomCombat.Drawing ? RoomCombat.Key : null);
			state.Heading = heading;
			mesh.Tag = tag;
			mesh.Flash = flash;
			mesh.Multiply = multiply;
			mesh.NoShadow = swimming;
			mesh.Emissive = false;
			mesh.SortDepth = distance;
			mesh.KeepSmall = distance > 20f;
			meshes.Add(mesh);
			if (!flag7)
			{
				StuckArrows.Wear(meshes, a, mesh, person: false, tag, flash, distance);
			}
			if (!flag7 && tag != 2147483646)
			{
				Hitboxes.Record(a, mesh, person: false);
			}
			if (mesh.Hidden != 0L)
			{
				meshes.Add(ShadowOf(mesh));
			}
			if (flag7)
			{
				MeshModel meshModel2 = BeastEye.Muzzle(meshModel);
				if (meshModel2 != null)
				{
					meshes.Add(MuzzleOf(mesh, meshModel2));
				}
			}
			state.Lifted = (swimming ? 0f : (z - num10));
			state.Tall = num2;
			return true;
		}

		private static MeshInstance MuzzleOf(MeshInstance body, MeshModel muzzle)
		{
			MeshInstance yourMuzzle = YourMuzzle;
			yourMuzzle.Model = muzzle;
			yourMuzzle.Position = body.Position;
			yourMuzzle.Basis = body.Basis;
			yourMuzzle.PartRotation = body.PartRotation;
			yourMuzzle.PartOffset = body.PartOffset;
			yourMuzzle.PartScale = body.PartScale;
			yourMuzzle.Palette = body.Palette;
			yourMuzzle.Hidden = 0uL;
			yourMuzzle.Tag = body.Tag;
			yourMuzzle.Flash = body.Flash;
			yourMuzzle.Multiply = body.Multiply;
			yourMuzzle.SortDepth = body.SortDepth;
			yourMuzzle.Fade = 1f;
			return yourMuzzle;
		}

		private static MeshInstance ShadowOf(MeshInstance body)
		{
			MeshInstance hiddenShadow = HiddenShadow;
			hiddenShadow.Model = body.Model;
			hiddenShadow.Position = body.Position;
			hiddenShadow.Basis = body.Basis;
			hiddenShadow.PartRotation = body.PartRotation;
			hiddenShadow.PartOffset = body.PartOffset;
			hiddenShadow.PartScale = body.PartScale;
			hiddenShadow.Palette = body.Palette;
			hiddenShadow.Hidden = ~body.Hidden;
			hiddenShadow.Tag = body.Tag;
			hiddenShadow.NoShadow = body.NoShadow;
			hiddenShadow.SortDepth = body.SortDepth;
			hiddenShadow.Fade = 1f;
			return hiddenShadow;
		}

		public static bool DrawnAs(Actor a, out MeshModel model, out float scale, out float lift, out float tall)
		{
			model = null;
			scale = (lift = (tall = 0f));
			if (a == null || !States.TryGetValue(a, out var value) || value.Boat || value.Mesh == null || value.Mesh.Model == null || value.Scale <= 0f)
			{
				return false;
			}
			model = value.Mesh.Model;
			scale = value.Scale;
			lift = value.Lifted;
			tall = value.Tall;
			return true;
		}

		public static bool BoatHeading(Actor a, out float heading)
		{
			heading = 0f;
			if (a == null || !States.TryGetValue(a, out var value) || !value.Boat || value.Drawn < _frame - 3)
			{
				return false;
			}
			heading = value.Heading;
			return true;
		}

		public static Actor BoatNear(float x, float y, float reach, Actor except)
		{
			Actor result = null;
			float num = reach * reach;
			foreach (KeyValuePair<Actor, State> state in States)
			{
				if (state.Value.Boat && state.Key != except && state.Value.Drawn >= _frame - 3 && state.Key != null)
				{
					Vector2 vector = state.Key.current_position + MooringOffset(state.Key);
					float num2 = (vector.x - x) * (vector.x - x) + (vector.y - y) * (vector.y - y);
					if (num2 < num)
					{
						num = num2;
						result = state.Key;
					}
				}
			}
			return result;
		}

		public static MeshInstance BoatPlaced(Actor a)
		{
			if (a == null || !States.TryGetValue(a, out var value) || !value.Boat || value.Posed != _frame)
			{
				return null;
			}
			return value.Mesh;
		}

		public static void BoatPose(Actor a, float x, float y, float heading, out Vec3 pos, out Mat3 basis, out float scale)
		{
			if (a == null || !States.TryGetValue(a, out var value) || !value.Boat || value.Scale <= 0f)
			{
				value = new State
				{
					Scale = 0.55f,
					Seed = ((a != null) ? ((float)(a.GetHashCode() & 0x3FF) / 97f) : 0f)
				};
			}
			Vector2 vector = MooringOffset(a);
			Pose(value, x + vector.x, y + vector.y, heading, SceneCollector.ShipHeel(a), out pos, out basis);
			scale = value.Scale;
		}

		private static void Pose(State st, float x, float y, float heading, float heel, out Vec3 pos, out Mat3 basis)
		{
			float scale = st.Scale;
			Ride(st, x, y, heading, out var heave, out var bowUp, out var portUp);
			float t = Sea.Now + st.Seed * 7f;
			float num = 1f - Sea.Storm;
			bowUp += IdlePitch(t) * num;
			portUp += IdleRoll(t) * num;
			pos = new Vec3(x, y, WaterUnder(x, y) + heave);
			float f = heading + MathF.PI / 2f;
			float num2 = Mathf.Cos(f);
			float num3 = Mathf.Sin(f);
			basis = new Mat3(new Vec3(num2 * scale, num3 * scale, 0f), new Vec3((0f - num3) * scale, num2 * scale, 0f), new Vec3(0f, 0f, scale)) * Mat3.RotY(heel - portUp) * Mat3.RotX(0f - bowUp);
		}

		public static bool AddBoat(List<MeshInstance> meshes, Actor a, SpriteImage img, float x, float y, float spriteW, float heading, float distance, int tag, uint flash, float night, bool collide)
		{
			string text = Models.BoatFamily(a);
			if (text == null)
			{
				return false;
			}
			State state = StateOf(a, text);
			MeshModel meshModel = Models.Detailed(text, distance, state.Mesh.Model);
			if (meshModel == null)
			{
				return false;
			}
			state.Boat = true;
			MeshInstance mesh = state.Mesh;
			mesh.Model = meshModel;
			if (state.Palette == null)
			{
				state.Palette = Models.BoatColors(meshModel, (a.kingdom != null) ? new Color?(Royal.ColorOf(a.kingdom)) : ((Color?)null));
			}
			mesh.Palette = state.Palette;
			float b = (RigTune.Creature ? ShipInfoOf(meshModel).Length : meshModel.InfoFloat("length", meshModel.Max.Y - meshModel.Min.Y));
			float num = Mathf.Clamp(Mathf.Max(1f, spriteW * Models.OpaqueWidth(img)) * 0.85f / Mathf.Max(1f, b), 0.42f, 0.75f);
			state.Scale = ((state.Scale <= 0f) ? num : Mathf.Lerp(state.Scale, num, 0.02f));
			float scale = state.Scale;
			float num2 = Mathf.Max(0.001f, Time.unscaledDeltaTime);
			if (a == BoatHelm.Boat)
			{
				state.Wheel = BoatHelm.Wheel;
			}
			else if (state.Drawn >= _frame - 2)
			{
				float num3 = Mathf.DeltaAngle(state.Heading * 57.29578f, heading * 57.29578f) / num2;
				state.Wheel = Mathf.Lerp(state.Wheel, Mathf.Clamp((0f - num3) / 25f, -1f, 1f), 1f - Mathf.Exp(-3f * num2));
			}
			state.Heading = heading;
			state.Drawn = _frame;
			state.Space = (RoomCombat.Drawing ? RoomCombat.Key : null);
			Vector2 vector = Moored(a, Models.Get(text) ?? meshModel, x, y, heading, scale);
			x += vector.x;
			y += vector.y;
			if (collide)
			{
				MeshModel model = Models.Get(text) ?? meshModel;
				Colliders.Near.Add(Colliders.FromModel(model, x, y, heading, scale, -0.9f, 2.2f));
			}
			if (meshes == null)
			{
				return true;
			}
			Pose(state, x, y, heading, SceneCollector.ShipHeel(a), out var pos, out var basis);
			mesh.Position = pos;
			mesh.Basis = basis;
			state.Posed = _frame;
			mesh.EnsurePose();
			mesh.ResetPose();
			float num4 = PoseClock.Now + state.Seed * 7f;
			for (int i = 0; i < meshModel.Parts.Length; i++)
			{
				string name = meshModel.Parts[i].Name;
				if (name.StartsWith("flag", StringComparison.Ordinal))
				{
					mesh.PartRotation[i] = Mat3.RotZ(0.3f * Mathf.Sin(num4 * 3.1f + (float)i) + 0.1f);
				}
				else if (name == "wheel")
				{
					mesh.PartRotation[i] = Mat3.RotY((0f - state.Wheel) * 2.4f);
				}
			}
			mesh.Glow = Mathf.Clamp01((night - 0.1f) * 2f);
			mesh.Tag = tag;
			mesh.Flash = flash;
			mesh.Multiply = 0u;
			mesh.NoShadow = false;
			mesh.KeepSmall = distance > 20f;
			GhostLook.Apply(mesh, RigTune.Creature ? ShipInfoOf(meshModel).Ghostly : meshModel.InfoFloat("ghostly", 0f), night);
			mesh.SortDepth = distance;
			meshes.Add(mesh);
			if (tag >= 0 && tag != 2147483646)
			{
				Hitboxes.Record(a, mesh, person: false);
			}
			return true;
		}

		internal static ShipInfo ShipInfoOf(MeshModel model)
		{
			if (!ShipInfos.TryGetValue(model, out var value))
			{
				Dictionary<MeshModel, ShipInfo> shipInfos = ShipInfos;
				ShipInfo obj = new ShipInfo
				{
					Length = model.InfoFloat("length", model.Max.Y - model.Min.Y),
					Ghostly = model.InfoFloat("ghostly", 0f)
				};
				value = obj;
				shipInfos[model] = obj;
			}
			return value;
		}

		internal static bool AddFigure(List<MeshInstance> meshes, Actor a, SpriteImage img, float x, float y, float z, float heading, float dt)
		{
			string text = Models.CreatureFamily(a);
			if (text == null)
			{
				return false;
			}
			State state = StateOf(a, text);
			if (_figure == null || _figureFrom != state)
			{
				_figure = new State
				{
					Family = text,
					Mesh = new MeshInstance(),
					Seed = state.Seed,
					Palette = state.Palette,
					Scale = state.Scale
				};
				_figureFrom = state;
			}
			Actor inside = BeastView.Inside;
			int count = Breaths.Count;
			States[a] = _figure;
			BeastView.Inside = null;
			try
			{
				Vector3 current_scale = a.current_scale;
				return Add(meshes, a, img, x, y, z, img.UnitWidth * Mathf.Abs(current_scale.x), img.UnitHeight * Mathf.Abs(current_scale.y), heading, 0f, 0f, dt, 2147483646, 0u, 0u, swimming: false);
			}
			finally
			{
				States[a] = state;
				BeastView.Inside = inside;
				if (Breaths.Count > count)
				{
					Breaths.RemoveRange(count, Breaths.Count - count);
				}
			}
		}

		private static Vector2 Moored(Actor a, MeshModel model, float x, float y, float heading, float scale)
		{
			if (!Moorings.TryGetValue(a, out var value))
			{
				value = new Mooring();
				Moorings[a] = value;
			}
			value.Seen = _frame;
			float unscaledTime = Time.unscaledTime;
			Vector2 vector = new Vector2(x, y);
			if (unscaledTime >= value.NextCheck || (vector - value.CheckedAt).sqrMagnitude > 0.04f || Mathf.Abs(Mathf.DeltaAngle(heading * 57.29578f, value.CheckedHeading * 57.29578f)) > 10f)
			{
				value.Target = ((DockDecks.Count == 0) ? Vector2.zero : ClearOfDocks(model, vector, heading, scale, value.Target));
				value.CheckedAt = vector;
				value.CheckedHeading = heading;
				value.NextCheck = unscaledTime + 0.4f;
			}
			float num = Mathf.Min(0.1f, Time.unscaledDeltaTime);
			value.Offset = Vector2.Lerp(value.Offset, value.Target, 1f - Mathf.Exp(-2.5f * num));
			if (value.Target == Vector2.zero && value.Offset.sqrMagnitude < 1E-06f)
			{
				value.Offset = Vector2.zero;
			}
			Spaced(a, value, model, vector + value.Offset, heading, scale, num);
			if (_frame % 600 == 0)
			{
				StaleMoorings.Clear();
				foreach (KeyValuePair<Actor, Mooring> mooring in Moorings)
				{
					if (_frame - mooring.Value.Seen > 600)
					{
						StaleMoorings.Add(mooring.Key);
					}
				}
				foreach (Actor staleMooring in StaleMoorings)
				{
					Moorings.Remove(staleMooring);
				}
			}
			return value.Offset + value.Spread;
		}

		public static Vector2 MooringOffset(Actor a)
		{
			if (a == null || !Moorings.TryGetValue(a, out var value))
			{
				return Vector2.zero;
			}
			return value.Offset + value.Spread;
		}

		private static void Spaced(Actor a, Mooring m, MeshModel model, Vector2 at, float heading, float scale, float dt)
		{
			m.Half = Mathf.Max(0.5f, model.InfoFloat("length", model.Max.Y - model.Min.Y) * 0.5f * scale);
			m.Beam = Mathf.Max(0.3f, model.InfoFloat("width", model.Max.X - model.Min.X) * 0.5f * scale);
			m.Heading = heading;
			if (a == BoatHelm.Boat)
			{
				m.Spread = Vector2.MoveTowards(m.Spread, Vector2.zero, 2f * dt);
				m.At = at + m.Spread;
				return;
			}
			Vector2 vector = Overlap(a, m, at + m.Spread, heading);
			if (vector != Vector2.zero)
			{
				Vector2 vector2 = Vector2.ClampMagnitude(vector, 3f * dt);
				for (int i = 0; i < SpreadTurns.Length; i++)
				{
					float num = Mathf.Cos(SpreadTurns[i]);
					float num2 = Mathf.Sin(SpreadTurns[i]);
					Vector2 vector3 = Vector2.ClampMagnitude(m.Spread + new Vector2(vector2.x * num - vector2.y * num2, vector2.x * num2 + vector2.y * num), 2f * m.Half + 1f);
					if (OnWater(at + vector3) && (DockDecks.Count <= 0 || HullClear(model, at + vector3, heading, scale)))
					{
						m.Spread = vector3;
						break;
					}
				}
			}
			else if (m.Spread != Vector2.zero)
			{
				Vector2 vector4 = Vector2.MoveTowards(m.Spread, Vector2.zero, 0.5f * dt);
				if (Overlap(a, m, at + vector4, heading) == Vector2.zero)
				{
					m.Spread = vector4;
				}
			}
			m.At = at + m.Spread;
		}

		private static Vector2 Overlap(Actor a, Mooring m, Vector2 c, float heading)
		{
			Vector2 zero = Vector2.zero;
			Vector2 vector = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));
			foreach (KeyValuePair<Actor, Mooring> mooring in Moorings)
			{
				Mooring value = mooring.Value;
				if (mooring.Key == a || value.Half <= 0f || _frame - value.Seen > 2)
				{
					continue;
				}
				float num = m.Half + value.Half;
				if ((c - value.At).sqrMagnitude > num * num)
				{
					continue;
				}
				Vector2 vector2 = new Vector2(Mathf.Cos(value.Heading), Mathf.Sin(value.Heading));
				float num2 = 0.02f;
				Vector2 vector3 = Vector2.zero;
				for (int i = 0; i < 5; i++)
				{
					for (int j = 0; j < 5; j++)
					{
						Vector2 vector4 = c + vector * (HullAlong[i] * m.Half);
						Vector2 vector5 = value.At + vector2 * (HullAlong[j] * value.Half);
						Vector2 vector6 = vector4 - vector5;
						float magnitude = vector6.magnitude;
						float num3 = m.Beam * HullTaper[i] + value.Beam * HullTaper[j] - magnitude;
						if (!(num3 <= num2))
						{
							num2 = num3;
							vector3 = ((magnitude > 0.001f) ? (vector6 / magnitude) : (new Vector2(0f - vector.y, vector.x) * ((a.GetHashCode() < mooring.Key.GetHashCode()) ? 1f : (-1f))));
						}
					}
				}
				if (vector3 != Vector2.zero)
				{
					zero += vector3 * (num2 * ((mooring.Key == BoatHelm.Boat) ? 1f : 0.5f));
				}
			}
			return zero;
		}

		internal static bool HullForTest(Actor a, out Vector2 mid, out float heading, out float half, out float beam, out Vector2 offset)
		{
			mid = (offset = Vector2.zero);
			heading = (half = (beam = 0f));
			if (a == null || !Moorings.TryGetValue(a, out var value) || _frame - value.Seen > 3 || value.Half <= 0f)
			{
				return false;
			}
			mid = value.At;
			heading = value.Heading;
			half = value.Half;
			beam = value.Beam;
			offset = value.Offset + value.Spread;
			return true;
		}

		private static Vector2 ClearOfDocks(MeshModel model, Vector2 raw, float heading, float scale, Vector2 previous)
		{
			if (HullClear(model, raw, heading, scale))
			{
				return Vector2.zero;
			}
			if (previous != Vector2.zero && HullClear(model, raw + previous, heading, scale))
			{
				return previous;
			}
			Vector2 result = Vector2.zero;
			float num = float.MaxValue;
			for (float num2 = 0.25f; num2 <= 6f; num2 += 0.25f)
			{
				for (int i = 0; i < 24; i++)
				{
					float num3 = heading + MathF.PI / 2f + (float)i * MathF.PI / 12f;
					Vector2 vector = new Vector2(Mathf.Cos(num3), Mathf.Sin(num3)) * num2;
					float num4 = Mathf.Abs(Mathf.Cos(num3 - heading));
					float num5 = num2 * (1f + 0.6f * num4);
					if (!(num5 >= num) && OnWater(raw + vector) && HullClear(model, raw + vector, heading, scale))
					{
						num = num5;
						result = vector;
					}
				}
				if (num < num2)
				{
					break;
				}
			}
			return result;
		}

		private static bool HullClear(MeshModel model, Vector2 c, float heading, float scale)
		{
			Vector2 vector = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));
			Vector2 vector2 = new Vector2(0f - vector.y, vector.x);
			float num = 0.12f / Mathf.Max(0.1f, scale);
			float num2 = model.Min.X - num;
			float num3 = model.Max.X + num;
			float num4 = model.Min.Y - num;
			float num5 = model.Max.Y + num;
			for (int i = 0; i <= 6; i++)
			{
				float num6 = num4 + (num5 - num4) * (float)i / 6f;
				for (int j = 0; j <= 2; j++)
				{
					float num7;
					switch (j)
					{
					default:
						num7 = num3 * 0.8f;
						break;
					case 1:
						num7 = 0f;
						break;
					case 0:
						num7 = num2 * 0.8f;
						break;
					}
					float num8 = num7;
					if (DockDecks.On(c + vector2 * (num8 * scale) - vector * (num6 * scale)))
					{
						return false;
					}
				}
			}
			return true;
		}

		internal static bool ClearForTest(Actor boat)
		{
			MeshModel meshModel = Models.Get(Models.BoatFamily(boat));
			if (meshModel == null || !States.TryGetValue(boat, out var value))
			{
				return false;
			}
			return HullClear(meshModel, boat.current_position + MooringOffset(boat), value.Heading, value.Scale);
		}

		private static bool OnWater(Vector2 p)
		{
			try
			{
				WorldTile tileAt = Toolbox.getTileAt(p.x, p.y);
				return tileAt != null && tileAt.Type != null && tileAt.Type.liquid && !tileAt.Type.lava;
			}
			catch
			{
				return false;
			}
		}

		private static void Ride(State st, float x, float y, float heading, out float heave, out float bowUp, out float portUp)
		{
			Riding riding = st.Ride ?? (st.Ride = new Riding());
			float now = Sea.Now;
			float num = now - riding.At;
			if (num == 0f)
			{
				heave = riding.Heave;
				bowUp = riding.Pitch;
				portUp = riding.Roll;
				return;
			}
			HullSize(st, out var half, out var beam);
			Feel(x, y, heading, half, beam, out var heave2, out var bowUp2, out var portUp2);
			if (float.IsNaN(riding.At) || num < 0f || num > 1f)
			{
				riding.Heave = heave2;
				riding.Pitch = bowUp2;
				riding.Roll = portUp2;
				riding.HeaveV = (riding.PitchV = (riding.RollV = 0f));
			}
			else
			{
				float num2 = 6f / Mathf.Sqrt(Mathf.Max(1f, half));
				float num3 = 0.7f;
				for (float num4 = num; num4 > 0f; num4 -= 0.05f)
				{
					float num5 = Mathf.Min(0.05f, num4);
					riding.HeaveV += (num2 * num2 * (heave2 - riding.Heave) - 2f * num3 * num2 * riding.HeaveV) * num5;
					riding.PitchV += (num2 * num2 * (bowUp2 - riding.Pitch) - 2f * num3 * num2 * riding.PitchV) * num5;
					riding.RollV += (num2 * num2 * (portUp2 - riding.Roll) - 2f * num3 * num2 * riding.RollV) * num5;
					riding.Heave += riding.HeaveV * num5;
					riding.Pitch += riding.PitchV * num5;
					riding.Roll += riding.RollV * num5;
				}
			}
			riding.At = now;
			heave = riding.Heave;
			bowUp = riding.Pitch;
			portUp = riding.Roll;
		}

		private static void Feel(float x, float y, float heading, float half, float beam, out float heave, out float bowUp, out float portUp)
		{
			TerrainCache seaLand = SeaLand;
			heave = (bowUp = (portUp = 0f));
			if (seaLand != null)
			{
				float num = x - Sea.EyeX;
				float num2 = y - Sea.EyeY;
				if (num * num + num2 * num2 > 6400f)
				{
					heave = seaLand.WaveAt(x, y);
					return;
				}
				float num3 = Mathf.Cos(heading);
				float num4 = Mathf.Sin(heading);
				float num5 = seaLand.WaveAt(x + num3 * half, y + num4 * half);
				float num6 = seaLand.WaveAt(x - num3 * half, y - num4 * half);
				float num7 = seaLand.WaveAt(x - num4 * beam, y + num3 * beam);
				float num8 = seaLand.WaveAt(x + num4 * beam, y - num3 * beam);
				heave = (num5 + num6 + num7 + num8) * 0.25f;
				bowUp = Mathf.Atan2(num5 - num6, 2f * half);
				portUp = Mathf.Atan2(num7 - num8, 2f * beam);
			}
		}

		private static void HullSize(State st, out float half, out float beam)
		{
			MeshModel meshModel = ((st.Mesh != null) ? st.Mesh.Model : null);
			float num = ((st.Scale > 0f) ? st.Scale : 0.55f);
			if (meshModel == null)
			{
				half = 2f;
				beam = 0.8f;
			}
			else
			{
				half = Mathf.Max(0.5f, meshModel.InfoFloat("length", meshModel.Max.Y - meshModel.Min.Y) * 0.5f * num);
				beam = Mathf.Max(0.3f, meshModel.InfoFloat("width", meshModel.Max.X - meshModel.Min.X) * 0.5f * num);
			}
		}

		private static float WaterUnder(float x, float y)
		{
			return SeaLand?.LiquidSurface(x, y) ?? 0f;
		}

		public static bool BoatTilt(Actor a, out float bowUp, out float portUp)
		{
			float heave = (bowUp = (portUp = 0f));
			if (a == null || !States.TryGetValue(a, out var value) || !value.Boat)
			{
				return false;
			}
			Vector2 vector = a.current_position + MooringOffset(a);
			Ride(value, vector.x, vector.y, value.Heading, out heave, out bowUp, out portUp);
			float t = Sea.Now + value.Seed * 7f;
			float num = 1f - Sea.Storm;
			bowUp += IdlePitch(t) * num;
			portUp += IdleRoll(t) * num;
			return true;
		}

		internal static bool RideForTest(Actor a, out float z, out float water, out float keel)
		{
			z = (water = (keel = 0f));
			TerrainCache seaLand = SeaLand;
			if (a == null || seaLand == null || !States.TryGetValue(a, out var value) || !value.Boat || value.Posed < _frame - 3 || value.Mesh == null || value.Mesh.Model == null)
			{
				return false;
			}
			Vec3 position = value.Mesh.Position;
			z = position.Z;
			water = WaterUnder(position.X, position.Y) + seaLand.WaveAt(position.X, position.Y);
			keel = (0f - value.Mesh.Model.Min.Z) * value.Scale;
			return true;
		}

		private static float IdleRoll(float t)
		{
			return 0.03f * Mathf.Sin(t * 0.9f);
		}

		private static float IdlePitch(float t)
		{
			return 0.012f * Mathf.Sin(t * 1.15f + 1f);
		}
	}
}
