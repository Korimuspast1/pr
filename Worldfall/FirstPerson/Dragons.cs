using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class Dragons
	{
		private sealed class State
		{
			public DragonFlight Flight;

			public readonly MeshInstance Mesh = new MeshInstance();

			public readonly MeshInstance Glow = new MeshInstance
			{
				Emissive = true,
				NoShadow = true
			};

			public MeshModel Base;

			public MeshModel Model;

			public DragonRig Rig;

			public int Seen;

			public int Drawn = -10;

			public int GameState = -1;

			public float Held;

			public bool Zombie;

			public uint[] Palette;

			public float Clock;

			public int Beats;

			public int Roars;

			public int Thumps;

			public int Whooshes;

			public int Snorts;

			public float BeatPower;

			public Vec3 TipL;

			public Vec3 TipR;

			public bool TipsKnown;

			public DragonInput Last;

			public bool Dying;

			public float DeadFor;
		}

		private struct Mote
		{
			public Vec3 P;

			public Vec3 V;

			public float Born;

			public float Life;

			public float Size;

			public float Grow;

			public byte Kind;

			public int Variant;

			public uint Tint;
		}

		private struct Footprint
		{
			public float X;

			public float Y;

			public float C;

			public float S;

			public float Front;

			public float Back;

			public float Half;

			public float Belly;
		}

		private struct Wisp
		{
			public Vec3 A;

			public Vec3 B;

			public float Born;

			public float Alpha;
		}

		public const float Range = 260f;

		public const float DrawScale = 0.8f;

		private const float MaxHold = 8f;

		private static readonly Dictionary<Actor, State> States = new Dictionary<Actor, State>();

		private static readonly Dictionary<MeshModel, DragonRig> Rigs = new Dictionary<MeshModel, DragonRig>();

		private static readonly List<Actor> Stale = new List<Actor>();

		private static int _frame;

		public static float Shake;

		public static int Flown;

		public static string LastAct = "";

		private static readonly List<Mote> Motes = new List<Mote>();

		private static float _moteClock;

		private static readonly List<Streak> Trails = new List<Streak>();

		private static uint _rng = 12648430u;

		private static readonly List<Footprint> Footprints = new List<Footprint>();

		private static readonly List<Wisp> Wisps = new List<Wisp>();

		public static bool Ready
		{
			get
			{
				MeshModel meshModel = Models.Get("monster_dragon");
				if (meshModel != null)
				{
					return meshModel.Info("units") == "tiles";
				}
				return false;
			}
		}

		public static bool Is(Actor a)
		{
			try
			{
				return a != null && a.asset != null && (a.asset.id == "dragon" || a.asset.id == "zombie_dragon");
			}
			catch
			{
				return false;
			}
		}

		private static float Rand01(ref uint s)
		{
			s ^= s << 13;
			s ^= s >> 17;
			s ^= s << 5;
			return (float)(s & 0xFFFFFF) / 16777216f;
		}

		public static void Tick()
		{
			_frame++;
			Flown = 0;
			Trails.Clear();
			Footprints.Clear();
			if (_frame % 120 != 0)
			{
				return;
			}
			Stale.Clear();
			foreach (KeyValuePair<Actor, State> state in States)
			{
				if (_frame - state.Value.Seen > 240)
				{
					Stale.Add(state.Key);
				}
			}
			foreach (Actor item in Stale)
			{
				States.Remove(item);
			}
		}

		public static void EmitTrails(List<Streak> streaks)
		{
			if (Trails.Count > 0)
			{
				streaks.AddRange(Trails);
			}
		}

		private static DragonRig RigFor(MeshModel m)
		{
			if (!Rigs.TryGetValue(m, out var value))
			{
				value = new DragonRig(m);
				Rigs[m] = value;
			}
			return value;
		}

		private static MeshModel BaseFor(float distance, MeshModel current)
		{
			string name = ((distance < 55f) ? "monster_dragon" : ((distance < 130f) ? "monster_dragon_m" : "monster_dragon_f"));
			if (current != null)
			{
				string name2 = current.Name;
				float num = ((name2 == "monster_dragon") ? 0f : ((name2 == "monster_dragon_m") ? 50f : 125f));
				float num2 = ((name2 == "monster_dragon") ? 60f : ((name2 == "monster_dragon_m") ? 135f : float.MaxValue));
				if (distance >= num && distance < num2)
				{
					return current;
				}
			}
			return Models.Get(name) ?? Models.Get("monster_dragon");
		}

		private static uint[] ZombiePalette(MeshModel m)
		{
			uint[] array = new uint[m.SlotNames.Length];
			uint b = TerrainCache.Rgb(104, 128, 92);
			for (int i = 0; i < array.Length; i++)
			{
				uint a = m.SlotColor[i];
				switch (m.SlotNames[i])
				{
				case "eye":
					array[i] = TerrainCache.Rgb(150, 255, 110);
					break;
				case "glow":
					array[i] = TerrainCache.Rgb(120, 255, 60);
					break;
				case "horn":
				case "claw":
				case "teeth":
					array[i] = TerrainCache.Lerp(a, TerrainCache.Rgb(200, 204, 176), 0.6f);
					break;
				default:
					array[i] = TerrainCache.Lerp(a, b, 0.6f);
					break;
				}
			}
			return array;
		}

		public static bool Add(List<MeshInstance> meshes, List<Billboard> bills, List<PointLight> lights, Actor a, Vec3 eye, TerrainCache terrain, float step, bool draw, int tag, uint flash, float clock, float night)
		{
			Vector2 current_position = a.current_position;
			float num = current_position.x - eye.X;
			float num2 = current_position.y - eye.Y;
			Mathf.Sqrt(num * num + num2 * num2);
			if (!States.TryGetValue(a, out var value))
			{
				value = new State
				{
					Zombie = (a.asset != null && a.asset.id == "zombie_dragon")
				};
				value.Flight = new DragonFlight((uint)a.GetHashCode())
				{
					Scale = 0.8f
				};
				States[a] = value;
			}
			value.Seen = _frame;
			int num3 = 0;
			int frame = 0;
			bool flying = false;
			bool flag = false;
			bool flag2 = true;
			bool flip = false;
			Dragon dragon = null;
			SpriteAnimation spriteAnimation = null;
			try
			{
				dragon = a.getActorComponent<Dragon>();
				if (dragon != null)
				{
					num3 = (int)dragon.state;
					spriteAnimation = ((dragon.spriteAnimation != null) ? dragon.spriteAnimation : a.sprite_animation);
					flag = dragon.aggroTargets != null && dragon.aggroTargets.Count > 0;
				}
				if (spriteAnimation == null)
				{
					spriteAnimation = a.sprite_animation;
				}
				if (spriteAnimation != null)
				{
					frame = spriteAnimation.currentFrameIndex;
				}
				flying = ((BaseSimObject)a).isFlying();
				flag2 = a.isAlive();
				flip = a.flip;
				if (!flag && a.data != null)
				{
					a.data.get("cityToAttack", out var pResult, -1L);
					flag = pResult >= 0;
				}
			}
			catch
			{
			}
			if (spriteAnimation != null && flag2)
			{
				if (num3 != value.GameState)
				{
					value.GameState = num3;
					value.Held = 0f;
				}
				int holdFrame = value.Flight.HoldFrame;
				bool flag3 = false;
				if (holdFrame > 0 && spriteAnimation.currentFrameIndex == holdFrame - 1 && value.Held < 8f)
				{
					spriteAnimation.nextFrameTime = Mathf.Max(spriteAnimation.nextFrameTime, 0.12f);
					value.Held += step;
					flag3 = true;
				}
				if (!flag3 && !a.is_visible && step > 0f)
				{
					try
					{
						spriteAnimation.update(step);
					}
					catch
					{
					}
				}
			}
			DragonInput last = new DragonInput
			{
				X = current_position.x,
				Y = current_position.y,
				State = num3,
				Frame = frame,
				Flip = flip,
				Flying = flying,
				Alive = flag2,
				Hostile = flag
			};
			return Body(input: value.Last = last, st: value, a: a, meshes: meshes, bills: bills, lights: lights, eye: eye, terrain: terrain, step: step, draw: draw, tag: tag, flash: flash, clock: clock, night: night, fade: 1f);
		}

		public static void AddFallen(List<MeshInstance> meshes, List<Billboard> bills, List<PointLight> lights, Vec3 eye, TerrainCache terrain, float step, float clock, float night)
		{
			Stale.Clear();
			foreach (KeyValuePair<Actor, State> state in States)
			{
				State value = state.Value;
				if (value.Seen == _frame)
				{
					continue;
				}
				if (!value.Dying)
				{
					bool flag;
					try
					{
						flag = state.Key != null && state.Key.isAlive();
					}
					catch
					{
						flag = false;
					}
					if (flag || value.Seen < _frame - 3)
					{
						continue;
					}
					value.Dying = true;
					value.DeadFor = 0f;
				}
				value.DeadFor += step;
				if (value.DeadFor > 24f)
				{
					Stale.Add(state.Key);
					continue;
				}
				DragonInput last = value.Last;
				last.Alive = false;
				last.State = 2;
				Body(value, state.Key, last, meshes, bills, lights, eye, terrain, step, draw: true, -1, 0u, clock, night, Mathf.Clamp01((24f - value.DeadFor) / 4f));
				value.Seen = _frame;
			}
			foreach (Actor item in Stale)
			{
				States.Remove(item);
			}
			Stale.Clear();
		}

		private static bool Body(State st, Actor a, DragonInput input, List<MeshInstance> meshes, List<Billboard> bills, List<PointLight> lights, Vec3 eye, TerrainCache terrain, float step, bool draw, int tag, uint flash, float clock, float night, float fade)
		{
			Func<float, float, float> ground = (float x, float y) => terrain.SampleHeight(x, y);
			DragonFlight flight = st.Flight;
			flight.Step(input, step, ground, eye);
			st.Clock += step;
			Flown++;
			LastAct = flight.ActName;
			if (flight.Downbeat)
			{
				st.Beats++;
				st.BeatPower = flight.BeatPower;
			}
			if (flight.Roar)
			{
				st.Roars++;
			}
			if (flight.Touchdown || flight.Liftoff)
			{
				st.Thumps++;
			}
			if (flight.Whoosh)
			{
				st.Whooshes++;
			}
			if (flight.Snort)
			{
				st.Snorts++;
			}
			float num = Mathf.Sqrt((flight.Pos.X - eye.X) * (flight.Pos.X - eye.X) + (flight.Pos.Y - eye.Y) * (flight.Pos.Y - eye.Y) + (flight.Pos.Z - eye.Z) * (flight.Pos.Z - eye.Z));
			MeshModel meshModel = BaseFor(num, st.Base);
			if (meshModel == null)
			{
				return false;
			}
			if (meshModel != st.Base)
			{
				st.Base = meshModel;
				st.Rig = RigFor(meshModel);
				st.Model = st.Rig.NewModel();
				st.Mesh.Model = st.Model;
				st.Mesh.PartRotation = null;
				st.Palette = (st.Zombie ? ZombiePalette(meshModel) : null);
			}
			DragonRig rig = st.Rig;
			MeshInstance mesh = st.Mesh;
			mesh.Model = st.Model;
			rig.Apply(mesh, flight.Pose, st.Clock);
			DragonRig.Place(mesh, flight.Pos, flight.Heading, flight.Pitch, flight.Roll, 0.8f);
			mesh.Palette = st.Palette;
			if (float.IsNaN(mesh.Position.X + mesh.Position.Y + mesh.Position.Z + mesh.Basis.C0.X + mesh.Basis.C1.Y + mesh.Basis.C2.Z))
			{
				return false;
			}
			Effects(st, flight, rig, mesh, eye, terrain, step, clock, num);
			if (!draw)
			{
				return false;
			}
			mesh.Tag = tag;
			mesh.Flash = flash;
			mesh.Multiply = 0u;
			mesh.NoShadow = false;
			mesh.Emissive = false;
			mesh.SortDepth = num;
			mesh.KeepSmall = num > 20f;
			mesh.Hidden = rig.GlowMask;
			mesh.Fade = fade;
			meshes.Add(mesh);
			if (flight.Altitude < 14f)
			{
				bool flag = flight.Airborne || (flight.Pose.Elev[0] > 0.5f && flight.Pose.Elbow[0] < 0.6f);
				Footprints.Add(new Footprint
				{
					X = flight.Pos.X,
					Y = flight.Pos.Y,
					C = Mathf.Cos(flight.Heading),
					S = Mathf.Sin(flight.Heading),
					Front = 8.8f,
					Back = 10.400001f,
					Half = (flag ? 13f : 4.5f) * 0.8f,
					Belly = flight.Pos.Z + 1.9200001f
				});
			}
			st.Drawn = _frame;
			float glow = flight.Pose.Glow;
			if (glow > 0.04f && rig.GlowMask != 0L)
			{
				MeshInstance glow2 = st.Glow;
				glow2.Model = mesh.Model;
				glow2.Position = mesh.Position;
				glow2.Basis = mesh.Basis;
				glow2.PartRotation = mesh.PartRotation;
				glow2.PartOffset = mesh.PartOffset;
				glow2.PartScale = mesh.PartScale;
				glow2.Palette = mesh.Palette;
				glow2.Hidden = ~rig.GlowMask;
				glow2.Fade = Mathf.Clamp01(glow * 1.3f);
				glow2.Tag = -1;
				glow2.SortDepth = num;
				meshes.Add(glow2);
				Vec3 vec = mesh.Position + mesh.Basis.Mul(rig.Maw);
				float num2 = glow * (0.85f + 0.15f * Mathf.Sin(clock * 19f));
				lights.Add(new PointLight
				{
					X = vec.X,
					Y = vec.Y,
					Z = vec.Z,
					Radius = 3f + 4f * glow,
					R = 1.2f * num2,
					G = 0.6f * num2,
					B = 0.2f * num2,
					Glow = 0.4f * glow
				});
			}
			if (night > 0.3f && num < 60f)
			{
				Vec3 vec2 = mesh.Position + mesh.Basis.Mul(rig.Posed(rig.HeadPart, rig.MouthRest + new Vec3(0f, 2.6f, 0.85f)));
				float num3 = 0.25f * night;
				lights.Add(new PointLight
				{
					X = vec2.X,
					Y = vec2.Y,
					Z = vec2.Z,
					Radius = 2.5f,
					R = 1.1f * num3,
					G = 0.95f * num3,
					B = 0.4f * num3,
					Glow = 0.35f
				});
			}
			if (flight.Fire > 0.02f)
			{
				Vec3 mouth = mesh.Position + mesh.Basis.Mul(rig.Mouth);
				Vec3 dir = mesh.Basis.Mul(rig.Snout).Normalized();
				float width = Mathf.Clamp(1.7600001f, 0.8f, 3f);
				int count = bills.Count;
				int count2 = lights.Count;
				DragonBreath.Emit(bills, lights, mouth, dir, flight.FireReach, width, flight.Fire, clock, (uint)(a.GetHashCode() * 7919), ground);
				if (st.Zombie)
				{
					uint multiply = TerrainCache.Rgb(150, 255, 90);
					for (int num4 = count; num4 < bills.Count; num4++)
					{
						Billboard value = bills[num4];
						value.Multiply = multiply;
						bills[num4] = value;
					}
					for (int num5 = count2; num5 < lights.Count; num5++)
					{
						PointLight value2 = lights[num5];
						float num6 = (value2.R + value2.G + value2.B) / 3f;
						value2.R = 0.5f * num6;
						value2.G = 1.2f * num6;
						value2.B = 0.35f * num6;
						lights[num5] = value2;
					}
				}
			}
			if (st.Dying)
			{
				return true;
			}
			Hitboxes.Record(a, mesh, person: false);
			if (!flight.Airborne && num < 40f)
			{
				Colliders.Near.Add(new Collider
				{
					X = flight.Pos.X,
					Y = flight.Pos.Y,
					Radius = 2.08f,
					Mobile = true
				});
			}
			return true;
		}

		public static void ClearView(List<MeshInstance> meshes)
		{
			if (Footprints.Count == 0)
			{
				return;
			}
			for (int i = 0; i < meshes.Count; i++)
			{
				MeshInstance meshInstance = meshes[i];
				if (meshInstance.Model == null || meshInstance.ShadowOnly || meshInstance.Emissive || !meshInstance.Static)
				{
					continue;
				}
				for (int j = 0; j < Footprints.Count; j++)
				{
					Footprint footprint = Footprints[j];
					float num = meshInstance.Position.X - footprint.X;
					float num2 = meshInstance.Position.Y - footprint.Y;
					float num3 = num * footprint.C + num2 * footprint.S;
					float f = (0f - num) * footprint.S + num2 * footprint.C;
					if (!(num3 > footprint.Front) && !(num3 < 0f - footprint.Back) && !(Mathf.Abs(f) > footprint.Half) && meshInstance.Position.Z + meshInstance.Model.Max.Z * meshInstance.Basis.C2.Z > footprint.Belly)
					{
						meshInstance.Fade = Mathf.Min(meshInstance.Fade, 0.3f);
						break;
					}
				}
			}
		}

		private static void Effects(State st, DragonFlight f, DragonRig rig, MeshInstance inst, Vec3 eye, TerrainCache terrain, float step, float clock, float dist)
		{
			float num = terrain.SampleHeight(f.Pos.X, f.Pos.Y);
			bool flag = num < 0f;
			float z = Math.Max(num, 0f);
			uint tint = (flag ? TerrainCache.Rgb(214, 228, 236) : TerrainCache.Rgb(196, 176, 140));
			float altitude = f.Altitude;
			if (f.Downbeat && altitude < 16f && dist < 140f)
			{
				Ring(f.Pos.X, f.Pos.Y, z, (int)(8f + 10f * f.BeatPower * (1f - altitude / 16f)), 1.6f + 3.5f * (1f - altitude / 16f), 2.2f, tint, 0);
			}
			if (f.Touchdown || f.Liftoff)
			{
				if (dist < 140f)
				{
					Ring(f.Pos.X, f.Pos.Y, z, f.Touchdown ? 28 : 20, f.Touchdown ? 7f : 5f, 3.2f, tint, 0);
				}
				float num2 = 1f - Mathf.Clamp01(dist / 45f);
				Shake = Mathf.Max(Shake, (f.Touchdown ? 0.85f : 0.55f) * num2 * num2);
			}
			if (f.Snort && dist < 90f)
			{
				Vec3 vec = inst.Position + inst.Basis.Mul(rig.Posed(rig.HeadPart, rig.MouthRest + new Vec3(0f, 0.25f, 0.5f)));
				Vec3 vec2 = inst.Basis.Mul(rig.Snout).Normalized();
				for (int i = 0; i < 6; i++)
				{
					float num3 = (((i & 1) == 0) ? (-1f) : 1f);
					Motes.Add(new Mote
					{
						P = vec + new Vec3((0f - vec2.Y) * num3 * 0.2f, vec2.X * num3 * 0.2f, 0f),
						V = vec2 * (1.2f + 0.3f * (float)i) + new Vec3(0f, 0f, 0.6f + 0.1f * (float)i),
						Born = _moteClock - (float)i * 0.05f,
						Life = 2.2f,
						Size = 0.35f,
						Grow = 1.1f,
						Kind = 1,
						Variant = i,
						Tint = TerrainCache.Rgb(120, 116, 112)
					});
				}
			}
			if (f.Pose.Glow > 0.3f && dist < 70f && Rand01(ref _rng) < f.Pose.Glow * step * 14f)
			{
				Vec3 p = inst.Position + inst.Basis.Mul(rig.Mouth);
				Motes.Add(new Mote
				{
					P = p,
					V = new Vec3((Rand01(ref _rng) - 0.5f) * 1.5f, (Rand01(ref _rng) - 0.5f) * 1.5f, -0.5f - Rand01(ref _rng)),
					Born = _moteClock,
					Life = 1.1f,
					Size = 0.14f,
					Grow = 0f,
					Kind = 2,
					Variant = 0
				});
			}
			st.TipsKnown &= f.Airborne;
			if (f.Airborne && dist < 160f)
			{
				Vec3 vec3 = inst.Position + inst.Basis.Mul(rig.Tip(0, 0));
				Vec3 vec4 = inst.Position + inst.Basis.Mul(rig.Tip(1, 0));
				float num4 = Mathf.Clamp01((f.Speed - 14f) / 8f) + Mathf.Clamp01((Mathf.Abs(f.Roll) - 0.7f) * 2f) * Mathf.Clamp01((f.Speed - 8f) / 6f);
				if (st.TipsKnown && num4 > 0.05f)
				{
					float alpha = Mathf.Clamp01(num4) * 0.55f;
					VapourTail(st.TipL, vec3, alpha);
					VapourTail(st.TipR, vec4, alpha);
				}
				st.TipL = vec3;
				st.TipR = vec4;
				st.TipsKnown = true;
			}
		}

		private static void VapourTail(Vec3 a, Vec3 b, float alpha)
		{
			Wisps.Add(new Wisp
			{
				A = a,
				B = b,
				Born = _moteClock,
				Alpha = alpha
			});
		}

		private static void Ring(float x, float y, float z, int n, float speed, float size, uint tint, byte kind)
		{
			for (int i = 0; i < n; i++)
			{
				float f = ((float)i + Rand01(ref _rng) * 0.6f) / (float)n * MathF.PI * 2f;
				float num = speed * (0.7f + 0.6f * Rand01(ref _rng));
				float num2 = 1.5f + Rand01(ref _rng) * 2f;
				Motes.Add(new Mote
				{
					P = new Vec3(x + Mathf.Cos(f) * num2, y + Mathf.Sin(f) * num2, z + 0.2f),
					V = new Vec3(Mathf.Cos(f) * num, Mathf.Sin(f) * num, 0.4f + Rand01(ref _rng) * 0.8f),
					Born = _moteClock,
					Life = 1.6f + Rand01(ref _rng) * 0.8f,
					Size = size * (0.6f + 0.6f * Rand01(ref _rng)),
					Grow = 1.4f,
					Kind = kind,
					Variant = i,
					Tint = tint
				});
			}
			if (Motes.Count > 900)
			{
				Motes.RemoveRange(0, Motes.Count - 900);
			}
		}

		public static void DrawMotes(List<Billboard> bills, Vec3 eye, TerrainCache terrain, float step)
		{
			_moteClock += step;
			for (int num = Motes.Count - 1; num >= 0; num--)
			{
				Mote value = Motes[num];
				float num2 = _moteClock - value.Born;
				if (num2 > value.Life)
				{
					Motes.RemoveAt(num);
				}
				else if (!(num2 < 0f))
				{
					float num3 = (float)Math.Exp((0f - ((value.Kind == 2) ? 0.4f : 2.2f)) * step);
					value.V = new Vec3(value.V.X * num3, value.V.Y * num3, (value.Kind == 2) ? (value.V.Z - 3f * step) : (value.V.Z * num3 + ((value.Kind == 1) ? (0.5f * step) : (0.15f * step))));
					value.P += value.V * step;
					Motes[num] = value;
					float num4 = value.P.X - eye.X;
					float num5 = value.P.Y - eye.Y;
					if (!(num4 * num4 + num5 * num5 > 22500f))
					{
						float num6 = num2 / value.Life;
						float num7 = value.Size * (1f + value.Grow * num6);
						float groundZ = terrain.SampleHeight(value.P.X, value.P.Y);
						if (value.Kind == 2)
						{
							bills.Add(new Billboard
							{
								X = value.P.X,
								Y = value.P.Y,
								Z = value.P.Z,
								GroundZ = groundZ,
								Width = num7,
								Height = num7,
								Image = (((value.Variant & 1) == 0) ? Flames.Ember : Flames.Spark),
								Kind = BillboardKind.Facing,
								Emissive = true,
								Tag = -1
							});
						}
						else
						{
							float num8 = ((value.Kind == 1) ? 150f : 175f) * ((num6 < 0.15f) ? (num6 / 0.15f) : (1f - (num6 - 0.15f) / 0.85f));
							if (!(num8 < 2f))
							{
								bills.Add(new Billboard
								{
									X = value.P.X,
									Y = value.P.Y,
									Z = value.P.Z - num7 * 0.3f,
									GroundZ = groundZ,
									Width = num7,
									Height = num7,
									Image = FireField.SoftSmoke((value.Kind == 1) ? 1 : 3, value.Variant),
									Kind = BillboardKind.Facing,
									Tag = -1,
									Alpha = (byte)Mathf.Clamp(num8, 1f, 255f),
									Multiply = value.Tint,
									Flip = ((value.Variant & 1) != 0)
								});
							}
						}
					}
				}
			}
			for (int num9 = Wisps.Count - 1; num9 >= 0; num9--)
			{
				Wisp wisp = Wisps[num9];
				float num10 = _moteClock - wisp.Born;
				if (num10 > 0.5f || num10 < 0f)
				{
					if (num10 > 0.5f)
					{
						Wisps.RemoveAt(num9);
					}
				}
				else
				{
					float num11 = wisp.Alpha * (1f - num10 / 0.5f);
					Trails.Add(new Streak
					{
						A = wisp.A,
						B = wisp.B,
						Color = 4294244090u,
						AlphaA = num11,
						AlphaB = num11,
						Width = 0.12f,
						Glow = 0.3f
					});
				}
			}
		}

		public static bool Voice(Actor a, out float lift, out float fire, out int beats, out float beatPower, out int roars, out int thumps, out int whooshes, out int snorts)
		{
			if (a != null && States.TryGetValue(a, out var value) && value.Seen >= _frame - 2)
			{
				lift = value.Flight.Altitude;
				fire = value.Flight.Fire;
				beats = value.Beats;
				beatPower = value.BeatPower;
				roars = value.Roars;
				thumps = value.Thumps;
				whooshes = value.Whooshes;
				snorts = value.Snorts;
				return true;
			}
			lift = (fire = (beatPower = 0f));
			beats = (roars = (thumps = (whooshes = (snorts = 0))));
			return false;
		}

		public static bool Airborne(Actor a)
		{
			if (a != null && States.TryGetValue(a, out var value))
			{
				return value.Flight.Airborne;
			}
			return false;
		}

		public static bool Probe(Actor a, out Vec3 pos, out string act, out float alt, out float fire, out int hold, out float held)
		{
			if (a != null && States.TryGetValue(a, out var value))
			{
				pos = value.Flight.Pos;
				act = value.Flight.ActName;
				alt = value.Flight.Altitude;
				fire = value.Flight.Fire;
				hold = value.Flight.HoldFrame;
				held = value.Held;
				return true;
			}
			pos = new Vec3(0f, 0f, 0f);
			act = "";
			alt = (fire = (held = 0f));
			hold = -1;
			return false;
		}
	}
}
