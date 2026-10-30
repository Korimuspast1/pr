using System;
using System.Collections.Generic;
using FirstPerson.Core;
using FirstPerson.Net;
using UnityEngine;

namespace FirstPerson
{
	internal static class Crabzillas
	{
		private sealed class State
		{
			public readonly MeshInstance Mesh = new MeshInstance();

			public readonly MeshInstance Shadow = new MeshInstance
			{
				ShadowOnly = true
			};

			public readonly CrabzillaGait Gait = new CrabzillaGait();

			public float Yaw = -MathF.PI / 2f;

			public Vector2 Last;

			public float Vx;

			public float Vy;

			public float GroundZ;

			public bool Placed;

			public int Seen;

			public int SeenAt;

			public float PitchTilt;

			public float RollTilt;

			public readonly Vec3[] Homes = new Vec3[8];

			public readonly Vec3[] Feet = new Vec3[8];

			public readonly Vec3[] AimAt = new Vec3[2];

			public readonly float[] Open = new float[2];

			public readonly float[] Power = new float[2];

			public readonly float[] Charge = new float[2];

			public readonly Vec3[] Tip = new Vec3[2];

			public bool TipKnown;

			public readonly Vec3[] Shot = new Vec3[2];

			public readonly bool[] ShotStrikes = new bool[2];
		}

		public const float Reach = 110f;

		public const float ChargeSeconds = 0.4f;

		private const int BlastRadius = 4;

		private const float BlastEvery = 1f / 30f;

		private static readonly Dictionary<Actor, State> States = new Dictionary<Actor, State>();

		private static readonly Dictionary<MeshModel, CrabzillaRig> Rigs = new Dictionary<MeshModel, CrabzillaRig>();

		private static readonly List<Actor> Stale = new List<Actor>();

		private static readonly Mat3[] Carried = new Mat3[2];

		private static readonly Vec3[] Sparks = new Vec3[3];

		private static int _frame;

		public static readonly bool[] Firing = new bool[2];

		public static float Shake;

		public static string AimNote = "";

		public static readonly bool[] ScriptedFire = new bool[2];

		private static readonly Vec3[] Target = new Vec3[2];

		private static readonly bool[] Strikes = new bool[2];

		private static Actor _yawFor;

		private static float _blastClock;

		private const float BodyHalfWidth = 11f;

		private const float BodyFront = -17f;

		private const float BodyBack = 7.5f;

		public static Actor You { get; private set; }

		public static float BodyYaw { get; private set; }

		public static float EyeHeight { get; private set; } = 7.75f;

		public static bool Is(Actor a)
		{
			try
			{
				return a != null && a.asset != null && a.asset.id == "crabzilla";
			}
			catch
			{
				return false;
			}
		}

		public static Vec3 TargetOf(int side)
		{
			return Target[side];
		}

		public static bool StrikesAt(int side)
		{
			return Strikes[side];
		}

		public static bool Eye(Actor host, float viewYaw, bool moving, float dt, out Vector2 offset, out float up)
		{
			offset = Vector2.zero;
			up = EyeHeight;
			if (!Is(host))
			{
				You = null;
				return false;
			}
			You = host;
			if (_yawFor != host)
			{
				_yawFor = host;
				BodyYaw = viewYaw;
			}
			float num = Mathf.DeltaAngle(BodyYaw * 57.29578f, viewYaw * 57.29578f);
			float num2 = Mathf.Clamp((moving ? num : ((Mathf.Abs(num) > 35f) ? (num - Mathf.Sign(num) * 35f) : 0f)) * (1f - Mathf.Exp(-3f * dt)), -110f * dt, 110f * dt);
			BodyYaw = (BodyYaw * 57.29578f + num2) * (MathF.PI / 180f);
			MeshModel meshModel = ModelFor(0f);
			Vec3 vec = ((meshModel != null) ? RigFor(meshModel) : null)?.Eye ?? new Vec3(0f, -4.45f, 7.75f);
			float num3 = Mathf.Cos(BodyYaw);
			float num4 = Mathf.Sin(BodyYaw);
			offset = new Vector2((0f - vec.Y) * num3 - vec.X * num4, (0f - vec.Y) * num4 + vec.X * num3);
			State value;
			float num5 = (States.TryGetValue(host, out value) ? value.Gait.Dip : 0f);
			EyeHeight = vec.Z;
			up = vec.Z - num5;
			return true;
		}

		public static void Control(Actor host, bool allowed, float dt)
		{
			if (!Is(host))
			{
				Firing[0] = (Firing[1] = false);
				You = null;
				return;
			}
			Firing[0] = allowed && (PlayInput.GetMouseButton(0) || ScriptedFire[0]);
			Firing[1] = allowed && (PlayInput.GetMouseButton(1) || ScriptedFire[1]);
			bool flag = false;
			Vec3 vec;
			if (FirstPersonAim.Ready(host))
			{
				Vec3 eye = FirstPersonAim.Eye;
				flag = FirstPersonAim.Point(out var point, out var creature);
				AimNote = string.Format("eye {0:F1},{1:F1},{2:F1} crosshair {3}{4}", eye.X, eye.Y, eye.Z, flag ? $"{point.X:F1},{point.Y:F1},{point.Z:F1}" : "sky", (creature != null) ? (" on " + creature.asset.id) : "");
				if (flag && FirstPersonAim.Terrain != null && Within(host, point) && point.Z > FirstPersonAim.Terrain.SampleHeight(point.X, point.Y) + 0.8f)
				{
					flag = GroundAlong(FirstPersonAim.Terrain, eye, FirstPersonAim.Ray, out point);
					AimNote += $", in your body: on to the ground at {point.X:F1},{point.Y:F1},{point.Z:F1}";
				}
				if (flag && Length(point - eye) > 110f)
				{
					flag = false;
				}
				vec = (flag ? point : (eye + FirstPersonAim.Ray * 110f));
			}
			else
			{
				Vector2 vector = new Vector2(Mathf.Cos(BodyYaw), Mathf.Sin(BodyYaw));
				Vector2 vector2 = host.current_position + vector * 30f;
				vec = new Vec3(vector2.x, vector2.y, 0f);
			}
			States.TryGetValue(host, out var value);
			TerrainCache terrain = FirstPersonAim.Terrain;
			for (int i = 0; i < 2; i++)
			{
				Target[i] = vec;
				Strikes[i] = flag;
				if (value != null && value.TipKnown && terrain != null && FirstAground(terrain, value.Tip[i], vec, out var at))
				{
					Target[i] = at;
					Strikes[i] = true;
				}
			}
			if (Firing[0] || Firing[1])
			{
				Shake = Mathf.Max(Shake, 0.22f);
			}
			Sound(value);
			Blast(host, dt);
		}

		public static void BlastFor(Actor crab, CrabFire.Shot shot, float elapsed)
		{
			if (!shot.Any)
			{
				shot.Clock = 0f;
				return;
			}
			shot.Clock = Mathf.Min(shot.Clock + elapsed, 2f / 15f);
			TerraformOptions terraformOptions = null;
			try
			{
				terraformOptions = AssetManager.terraform.get("crab_laser");
			}
			catch
			{
			}
			if (terraformOptions == null)
			{
				return;
			}
			States.TryGetValue(crab, out var value);
			while (shot.Clock >= 1f / 30f)
			{
				shot.Clock -= 1f / 30f;
				for (int i = 0; i < 2; i++)
				{
					if (!shot.On[i] || !shot.Strikes[i] || (value != null && Time.frameCount - value.SeenAt < 30 && value.Power[i] < 0.6f))
					{
						continue;
					}
					try
					{
						WorldTile tile = World.world.GetTile((int)shot.At[i].X, (int)shot.At[i].Y);
						if (tile != null)
						{
							MapAction.damageWorld(tile, 4, terraformOptions, crab);
						}
					}
					catch (Exception ex)
					{
						Log.Error("crab laser (another player's): " + ex.Message);
						return;
					}
				}
			}
		}

		private static void Blast(Actor host, float dt)
		{
			if (NetSession.Joined)
			{
				return;
			}
			float num;
			try
			{
				num = (World.world.isPaused() ? 0f : World.world.elapsed);
			}
			catch
			{
				num = 0f;
			}
			if (!Firing[0] && !Firing[1])
			{
				_blastClock = 0f;
				return;
			}
			_blastClock = Mathf.Min(_blastClock + num, 2f / 15f);
			if (_blastClock < 1f / 30f)
			{
				return;
			}
			_blastClock -= 1f / 30f;
			States.TryGetValue(host, out var value);
			TerraformOptions terraformOptions = null;
			try
			{
				terraformOptions = AssetManager.terraform.get("crab_laser");
			}
			catch
			{
			}
			if (terraformOptions == null)
			{
				return;
			}
			for (int i = 0; i < 2; i++)
			{
				if (!Firing[i] || !Strikes[i] || (value != null && value.Power[i] < 0.6f))
				{
					continue;
				}
				try
				{
					WorldTile tile = World.world.GetTile((int)Target[i].X, (int)Target[i].Y);
					if (tile != null)
					{
						MapAction.damageWorld(tile, 4, terraformOptions, host);
					}
				}
				catch (Exception ex)
				{
					Log.Error("crab laser: " + ex.Message);
				}
			}
		}

		private static void Sound(State st)
		{
			FirstPersonAudio firstPersonAudio = ((WorldBoxMod.Instance != null) ? WorldBoxMod.Instance.Audio : null);
			if (firstPersonAudio == null)
			{
				return;
			}
			for (int i = 0; i < 2; i++)
			{
				if (Firing[i])
				{
					Vec3 vec = ((st != null && st.TipKnown) ? st.Tip[i] : FirstPersonAim.Eye);
					firstPersonAudio.CrabHum(i, new Vector3(vec.X, vec.Y, vec.Z));
				}
			}
		}

		public static void Release()
		{
			Firing[0] = (Firing[1] = false);
			You = null;
			_yawFor = null;
		}

		private static bool GroundAlong(TerrainCache terrain, Vec3 eye, Vec3 ray, out Vec3 at)
		{
			at = eye + ray * 110f;
			for (float num = 1f; num < 110f; num += 0.6f)
			{
				Vec3 vec = eye + ray * num;
				if (vec.Z <= Mathf.Max(terrain.SampleHeight(vec.X, vec.Y), 0f))
				{
					at = vec;
					return true;
				}
			}
			return false;
		}

		private static bool Within(Actor crab, Vec3 p)
		{
			if (crab == null || !States.TryGetValue(crab, out var value) || !value.Placed)
			{
				return false;
			}
			float num = Mathf.Cos(value.Yaw);
			float num2 = Mathf.Sin(value.Yaw);
			float num3 = p.X - crab.current_position.x;
			float num4 = p.Y - crab.current_position.y;
			float f = (0f - num3) * num2 + num4 * num;
			float num5 = (0f - num3) * num - num4 * num2;
			if (Mathf.Abs(f) < 11f && num5 > -17f)
			{
				return num5 < 7.5f;
			}
			return false;
		}

		public static void ClearView(List<MeshInstance> meshes)
		{
			Actor you = You;
			if (you == null || !States.TryGetValue(you, out var value) || !value.Placed)
			{
				return;
			}
			float num = value.GroundZ + 5f;
			for (int i = 0; i < meshes.Count; i++)
			{
				MeshInstance meshInstance = meshes[i];
				if (meshInstance.Model != null && meshInstance.Tag != 2147483646 && !meshInstance.ShadowOnly && !meshInstance.Emissive && meshInstance.Static && Within(you, meshInstance.Position) && meshInstance.Position.Z + meshInstance.Model.Max.Z * meshInstance.Basis.C2.Z > num)
				{
					meshInstance.Fade = Mathf.Min(meshInstance.Fade, 0.3f);
				}
			}
		}

		private static bool FirstAground(TerrainCache terrain, Vec3 from, Vec3 to, out Vec3 at)
		{
			at = to;
			Vec3 vec = to - from;
			float num = Length(vec);
			if (num < 1f || from.Z <= Mathf.Max(terrain.SampleHeight(from.X, from.Y), 0f))
			{
				return false;
			}
			Vec3 vec2 = vec * (1f / num);
			float num2 = 0f;
			for (float num3 = 0.75f; num3 < num - 0.75f; num3 += 0.75f)
			{
				Vec3 vec3 = from + vec2 * num3;
				if (vec3.Z <= Mathf.Max(terrain.SampleHeight(vec3.X, vec3.Y), 0f))
				{
					float num4 = num2;
					float num5 = num3;
					for (int i = 0; i < 6; i++)
					{
						float num6 = (num4 + num5) * 0.5f;
						Vec3 vec4 = from + vec2 * num6;
						if (vec4.Z <= Mathf.Max(terrain.SampleHeight(vec4.X, vec4.Y), 0f))
						{
							num5 = num6;
						}
						else
						{
							num4 = num6;
						}
					}
					at = from + vec2 * num5;
					return true;
				}
				num2 = num3;
			}
			return false;
		}

		public static void Tick()
		{
			_frame++;
			if (_frame % 300 != 0)
			{
				return;
			}
			Stale.Clear();
			foreach (KeyValuePair<Actor, State> state in States)
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
				if (!flag || _frame - state.Value.Seen > 600)
				{
					Stale.Add(state.Key);
				}
			}
			foreach (Actor item in Stale)
			{
				States.Remove(item);
			}
		}

		private static MeshModel ModelFor(float distance)
		{
			if (!Models.Ready)
			{
				return null;
			}
			return Models.Get((distance < 50f) ? "crabzilla" : ((distance < 120f) ? "crabzilla_m" : "crabzilla_f")) ?? Models.Get("crabzilla");
		}

		private static CrabzillaRig RigFor(MeshModel m)
		{
			if (!Rigs.TryGetValue(m, out var value))
			{
				value = (CrabzillaRig.Fits(m) ? new CrabzillaRig(m) : null);
				Rigs[m] = value;
			}
			return value;
		}

		public static bool Add(List<MeshInstance> meshes, List<Billboard> bills, List<PointLight> lights, Actor a, ViewState view, TerrainCache terrain, float dt, bool self, int tag, float time)
		{
			Vector2 current_position = a.current_position;
			float num = current_position.x - view.X;
			float num2 = current_position.y - view.Y;
			float num3 = Mathf.Sqrt(num * num + num2 * num2);
			MeshModel meshModel = ModelFor(self ? 0f : num3);
			CrabzillaRig crabzillaRig = ((meshModel != null) ? RigFor(meshModel) : null);
			if (crabzillaRig == null)
			{
				return false;
			}
			if (!States.TryGetValue(a, out var value))
			{
				value = new State
				{
					Last = current_position
				};
				States[a] = value;
			}
			if (_frame - value.Seen > 30)
			{
				value.Gait.Reset();
				value.Placed = false;
				value.TipKnown = false;
				value.Last = current_position;
			}
			value.Seen = _frame;
			value.SeenAt = Time.frameCount;
			if (dt > 0f)
			{
				float num4 = (current_position.x - value.Last.x) / dt;
				float num5 = (current_position.y - value.Last.y) / dt;
				if (num4 * num4 + num5 * num5 > 3600f)
				{
					num4 = (num5 = 0f);
					value.Gait.Reset();
				}
				float num6 = 1f - Mathf.Exp(-8f * dt);
				value.Vx += (num4 - value.Vx) * num6;
				value.Vy += (num5 - value.Vy) * num6;
			}
			value.Last = current_position;
			float num7 = Mathf.Sqrt(value.Vx * value.Vx + value.Vy * value.Vy);
			if (self)
			{
				value.Yaw = BodyYaw;
			}
			else if (num7 > 1f)
			{
				value.Yaw = Mathf.LerpAngle(value.Yaw * 57.29578f, Mathf.Atan2(value.Vy, value.Vx) * 57.29578f, 1f - Mathf.Exp(-1.5f * dt)) * (MathF.PI / 180f);
			}
			float num8 = Mathf.Cos(value.Yaw);
			float num9 = Mathf.Sin(value.Yaw);
			float num10 = 0f;
			float num11 = 0f;
			float num12 = 0f;
			float num13 = 0f;
			float num14 = 0f;
			Func<float, float, float> func = (float gx, float gy) => Mathf.Max(terrain.SampleHeight(gx, gy), -3.5f);
			for (int num15 = 0; num15 < 8; num15++)
			{
				Vec3 vec = crabzillaRig.Foot[num15];
				float num16 = current_position.x - vec.Y * num8 - vec.X * num9;
				float num17 = current_position.y - vec.Y * num9 + vec.X * num8;
				float num18 = func(num16, num17);
				value.Homes[num15] = new Vec3(num16, num17, num18);
				num14 += num18;
				if (vec.Y < 0f)
				{
					num10 += num18;
				}
				else
				{
					num11 += num18;
				}
				if (num15 < 4)
				{
					num12 += num18;
				}
				else
				{
					num13 += num18;
				}
			}
			float num19 = num14 / 8f;
			if (!value.Placed)
			{
				value.GroundZ = num19;
				value.Placed = true;
			}
			else
			{
				value.GroundZ = Mathf.Lerp(value.GroundZ, num19, 1f - Mathf.Exp(-6f * dt));
			}
			float x = Mathf.Max(1f, crabzillaRig.Foot[3].Y - crabzillaRig.Foot[0].Y);
			float num20 = Mathf.Clamp(0f - Mathf.Atan2((num10 - num11) / 4f, x), -0.2f, 0.2f);
			float num21 = Mathf.Clamp(0f - Mathf.Atan2((num12 - num13) / 4f, Mathf.Max(1f, crabzillaRig.Foot[1].X - crabzillaRig.Foot[5].X)), -0.2f, 0.2f);
			float num22 = 1f - Mathf.Exp(-4f * dt);
			value.PitchTilt += (num20 - value.PitchTilt) * num22;
			value.RollTilt += (num21 - value.RollTilt) * num22;
			value.Gait.Update(value.Homes, value.Vx, value.Vy, dt, func);
			if (self && value.Gait.Landed != 0)
			{
				Shake = Mathf.Max(Shake, 0.32f + Mathf.Min(0.3f, num7 * 0.02f));
				Crush(a, value.Gait);
			}
			MeshInstance mesh = value.Mesh;
			mesh.Model = meshModel;
			mesh.Place(current_position.x, current_position.y, value.GroundZ, value.Yaw, 1f, 1f, 1f);
			Mat3 mat = CrabzillaRig.Transpose(mesh.Basis);
			for (int num23 = 0; num23 < 8; num23++)
			{
				value.Feet[num23] = mat.Mul(value.Gait.Feet[num23] - mesh.Position);
			}
			bool flag = self && You == a;
			CrabFire.Shot shot = null;
			if (!flag)
			{
				CrabFire.Of(a, out shot);
			}
			for (int num24 = 0; num24 < 2; num24++)
			{
				Vec3 vec2 = (flag ? Target[num24] : value.Shot[num24]);
				if (shot != null)
				{
					vec2 = shot.At[num24];
					if (float.IsNaN(vec2.Z))
					{
						vec2.Z = func(vec2.X, vec2.Y);
					}
				}
				value.Shot[num24] = vec2;
				value.ShotStrikes[num24] = (flag ? Strikes[num24] : (shot != null && shot.Strikes[num24]));
				bool flag2 = (flag ? Firing[num24] : (shot != null && shot.On[num24]));
				value.Charge[num24] = Mathf.MoveTowards(value.Charge[num24], flag2 ? 1f : 0f, dt / (flag2 ? 0.4f : 0.3f));
				bool flag3 = flag2 && value.Charge[num24] >= 1f;
				value.Power[num24] = Mathf.MoveTowards(value.Power[num24], flag3 ? 1f : 0f, dt * (flag3 ? 7f : 4f));
				bool flag4 = value.Charge[num24] > 0.02f || value.Power[num24] > 0.02f;
				value.Open[num24] = Mathf.MoveTowards(value.Open[num24], flag4 ? 1f : (0.12f + 0.08f * Mathf.Sin(time * 1.3f + (float)num24 * 2f)), dt * 5f);
				Vec3 vec3 = (flag ? CrabzillaRig.CarryAim(new Vec3(view.X, view.Y, view.Z), Target[num24]) : vec2);
				value.AimAt[num24] = ((flag || flag4) ? mat.Mul(vec3 - mesh.Position) : new Vec3(float.NaN, 0f, 0f));
			}
			Mat3 bodyTurn = Mat3.RotX(value.PitchTilt) * Mat3.RotY(value.RollTilt);
			if (flag)
			{
				float yaw = Mathf.DeltaAngle(value.Yaw * 57.29578f, view.Yaw * 57.29578f) * (MathF.PI / 180f);
				float pitch = CrabzillaRig.CarryPitch(Mathf.Atan(view.Pitch));
				for (int num25 = 0; num25 < 2; num25++)
				{
					Carried[num25] = crabzillaRig.Carry(num25, CrabzillaRig.CarryWrist(num25), yaw, pitch);
				}
			}
			crabzillaRig.Pose(mesh, new Vec3(0f, 0f, 0f - value.Gait.Dip), bodyTurn, value.Feet, value.AimAt, value.Open, value.Charge[0] > 0.02f, value.Charge[1] > 0.02f, self, flag ? Carried : null);
			mesh.Tag = tag;
			mesh.Flash = 0u;
			mesh.Multiply = 0u;
			mesh.Emissive = false;
			mesh.Palette = null;
			mesh.Fade = 1f;
			mesh.SortDepth = num3;
			meshes.Add(mesh);
			MeshModel meshModel2 = Models.Get("crabzilla_f");
			mesh.NoShadow = meshModel2 != null && meshModel2 != meshModel && meshModel2.Parts.Length == meshModel.Parts.Length;
			if (mesh.NoShadow)
			{
				MeshInstance shadow = value.Shadow;
				shadow.Model = meshModel2;
				shadow.Position = mesh.Position;
				shadow.Basis = mesh.Basis;
				shadow.PartRotation = mesh.PartRotation;
				shadow.PartOffset = mesh.PartOffset;
				shadow.PartScale = mesh.PartScale;
				shadow.Hidden = mesh.Hidden & (ulong)(~((crabzillaRig.Eyes >= 0 && crabzillaRig.Eyes < 64) ? (1L << crabzillaRig.Eyes) : 0));
				shadow.Tag = -1;
				shadow.SortDepth = num3;
				meshes.Add(shadow);
			}
			if (!self)
			{
				Hitboxes.Record(a, mesh, person: false);
			}
			Vec3 eye = new Vec3(view.X, view.Y, view.Z);
			for (int num26 = 0; num26 < 2; num26++)
			{
				value.Tip[num26] = crabzillaRig.TipWorld(mesh, num26);
				uint seed = (uint)(num26 * 7 + 3);
				if (value.Charge[num26] > 0.01f || value.Power[num26] > 0.01f)
				{
					crabzillaRig.Sources(mesh, num26, Sparks);
					CrabzillaBeam.Orb(meshes, bills, lights, value.Tip[num26], Mathf.Max(value.Charge[num26], value.Power[num26]), value.Power[num26], Sparks, time, seed, eye);
				}
				if (value.Power[num26] > 0.01f)
				{
					CrabzillaBeam.Emit(meshes, bills, lights, value.Tip[num26], value.Shot[num26], value.Power[num26], time, value.ShotStrikes[num26], seed, eye);
				}
			}
			value.TipKnown = true;
			return true;
		}

		private static void Crush(Actor crab, CrabzillaGait gait)
		{
			if (NetSession.Joined)
			{
				return;
			}
			TerraformOptions terraformOptions = null;
			try
			{
				terraformOptions = AssetManager.terraform.get("crab_step");
			}
			catch
			{
			}
			if (terraformOptions == null)
			{
				return;
			}
			for (int i = 0; i < 8; i++)
			{
				if ((gait.Landed & (1 << i)) == 0)
				{
					continue;
				}
				try
				{
					WorldTile tile = World.world.GetTile((int)gait.Feet[i].X, (int)gait.Feet[i].Y);
					if (tile != null)
					{
						MapAction.damageWorld(tile, 3, terraformOptions, crab);
					}
				}
				catch (Exception ex)
				{
					Log.Error("crab step: " + ex.Message);
					break;
				}
			}
		}

		internal static float PowerOf(Actor a, int side)
		{
			if (a == null || !States.TryGetValue(a, out var value))
			{
				return 0f;
			}
			return value.Power[side];
		}

		private static float Length(Vec3 v)
		{
			return Mathf.Sqrt(Vec3.Dot(v, v));
		}
	}
}
