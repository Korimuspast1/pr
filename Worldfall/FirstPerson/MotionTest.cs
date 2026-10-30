using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class MotionTest : MonoBehaviour
	{
		private sealed class Watched
		{
			public Actor A;

			public bool Marcher;

			public int Frame = -1;

			public float Angle;

			public float Old;

			public Vector2 Real;

			public Vector2 Drawn;

			public bool Have;
		}

		private sealed class FeetSeen
		{
			public Vector2 Drawn;

			public Vec3 L;

			public Vec3 R;

			public bool DownL;

			public bool DownR;

			public float Phase;

			public float Run;
		}

		private const float Deg = 57.29578f;

		private WorldBoxMod _mod;

		private readonly List<Watched> _watched = new List<Watched>();

		private readonly List<float> _newTurns = new List<float>();

		private readonly List<float> _oldTurns = new List<float>();

		private readonly List<float> _offsets = new List<float>();

		private readonly List<float> _slips = new List<float>();

		private readonly Dictionary<Actor, FeetSeen> _feet = new Dictionary<Actor, FeetSeen>();

		private float _footSlip;

		private float _footGround;

		private float _legSteps;

		private float _legTime;

		private float _legBody;

		private float _legPace;

		private float _legRun;

		private float _legSaid;

		private float _legRunSwing;

		private float _legDrawn;

		private readonly float[] _plant = new float[6];

		private readonly Dictionary<string, Vector2> _byModel = new Dictionary<string, Vector2>();

		private const float RecordSeconds = 50f;

		private IEnumerator Alive(Actor me, WorldTile spot)
		{
			Vector2 current_position = me.current_position;
			float num = 0.6f;
			_mod.SetYaw(num);
			Vector2 vector = new Vector2(Mathf.Cos(num), Mathf.Sin(num));
			Vector2 vector2 = new Vector2(0f - vector.y, vector.x);
			List<Actor> front = new List<Actor>();
			for (int i = 0; i < 4; i++)
			{
				Vector2 vector3 = current_position + vector * (2.2f + (float)(i & 1) * 0.8f) + vector2 * (((float)i - 1.5f) * 1.1f);
				WorldTile tile = World.world.GetTile(Mathf.FloorToInt(vector3.x), Mathf.FloorToInt(vector3.y));
				Actor actor = ((tile != null) ? Spawn("human", tile) : null);
				if (actor != null)
				{
					try
					{
						((BaseSimObject)actor).addStatusEffect("invincible", 600f, pColorEffect: false);
					}
					catch
					{
					}
					front.Add(actor);
				}
			}
			HashSet<Actor> looked = new HashSet<Actor>();
			float most = 0f;
			for (float t = 0f; t < 8f; t += Time.unscaledDeltaTime)
			{
				foreach (Actor item in front)
				{
					if (item != null && item.isAlive() && Attention.LookingAtYouForTest(item, out var yaw))
					{
						looked.Add(item);
						most = Mathf.Max(most, Mathf.Abs(yaw));
					}
				}
				yield return null;
			}
			Log.Info($"MOTION alive: {looked.Count} of {front.Count} villagers in front of you looked at you within 8 s (heads turned up to {most * 57.29578f:F0} deg)");
			Check("alive: most villagers close in front of you look at you", front.Count >= 3 && looked.Count * 2 >= front.Count);
			List<Actor> walkers = new List<Actor>(front);
			for (int j = 0; j < 2; j++)
			{
				WorldTile tile2 = World.world.GetTile(spot.x + 1 + j, spot.y - 5);
				Actor actor2 = ((tile2 != null) ? Spawn("human", tile2) : null);
				if (actor2 != null)
				{
					try
					{
						((BaseSimObject)actor2).addStatusEffect("invincible", 600f, pColorEffect: false);
					}
					catch
					{
					}
					walkers.Add(actor2);
				}
			}
			for (int k = 0; k < walkers.Count; k++)
			{
				bool flag = (k & 1) == 0;
				int num2 = k / 2 - 1;
				WorldTile tile3 = World.world.GetTile(spot.x + (flag ? (-6) : 6), spot.y + 4 + num2);
				WorldTile tile4 = World.world.GetTile(spot.x + (flag ? 6 : (-6)), spot.y + 4 - num2);
				try
				{
					walkers[k].current_position = new Vector2(tile3.posV3.x, tile3.posV3.y);
					walkers[k].dirty_current_tile = true;
					walkers[k].goTo(tile4);
				}
				catch (Exception ex)
				{
					Log.Error("MOTION alive: " + ex.Message);
				}
			}
			_mod.SetYaw(MathF.PI / 2f);
			int frames = 0;
			int overlapping = 0;
			float closest = float.MaxValue;
			for (float t = 0f; t < 7f; t += Time.unscaledDeltaTime)
			{
				yield return null;
				frames++;
				bool flag2 = false;
				for (int l = 0; l < walkers.Count; l++)
				{
					for (int m = l + 1; m < walkers.Count; m++)
					{
						if (_mod.SceneForTest.MotionForTest(walkers[l], out var drawn, out var real, out var angle, out var speed, out var frame, out var dt) && _mod.SceneForTest.MotionForTest(walkers[m], out var drawn2, out real, out dt, out var speed2, out var frame2, out angle) && frame == frame2 && (!(speed < 0.3f) || !(speed2 < 0.3f)))
						{
							real = drawn - drawn2;
							float magnitude = real.magnitude;
							closest = Mathf.Min(closest, magnitude);
							if (magnitude < 0.22f)
							{
								flag2 = true;
							}
						}
					}
				}
				if (flag2)
				{
					overlapping++;
				}
			}
			Log.Info($"MOTION alive: walkers crossing each other's paths were drawn inside each other on {overlapping} of {frames} frames, closest {closest:F2} tiles");
			Check("alive: walkers crossing paths are drawn inside each other under 3% of the time", frames > 0 && (float)overlapping <= (float)frames * 0.03f);
		}

		private IEnumerator Start()
		{
			string flag = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			bool quit = File.ReadAllText(flag).Contains("quit");
			while (!Config.game_loaded || World.world == null || World.world.units == null)
			{
				yield return null;
			}
			for (float t = 0f; t < 60f; t += 1f)
			{
				bool flag2 = false;
				try
				{
					flag2 = SmoothLoader.isLoading();
				}
				catch
				{
				}
				if (!flag2 && t >= 4f)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(1f);
			}
			Config.autosaves = false;
			_mod = WorldBoxMod.Instance;
			if (File.ReadAllText(flag).Contains("motionrec"))
			{
				yield return Record();
				Finish(quit);
				yield break;
			}
			WorldTile spot = FindOpen();
			if (spot == null)
			{
				Log.Error("MOTION no open land");
				Finish(quit);
				yield break;
			}
			Actor me = Spawn("human", spot);
			if (me == null)
			{
				Finish(quit);
				yield break;
			}
			try
			{
				((BaseSimObject)me).addStatusEffect("invincible", 600f, pColorEffect: false);
			}
			catch
			{
			}
			ControllableUnit.setControllableCreature(me);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("MOTION first person did not start");
				Finish(quit);
				yield break;
			}
			_mod.HintUntil = 0f;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			string[] array = new string[12]
			{
				"sheep", "cow", "chicken", "sheep", "human", "human", "human", "cat", "chicken", "cow",
				"human", "sheep"
			};
			System.Random rng = new System.Random(7);
			for (int i = 0; i < array.Length + 4; i++)
			{
				float f = (float)i * 2.39996f;
				float num = 4f + (float)(i % 5);
				WorldTile tile = World.world.GetTile(spot.x + (int)(Mathf.Cos(f) * num), spot.y + (int)(Mathf.Sin(f) * num));
				if (tile == null || tile.Type == null || tile.Type.liquid || tile.Type.block)
				{
					continue;
				}
				bool flag3 = i >= array.Length;
				Actor actor = Spawn(flag3 ? "human" : array[i], tile);
				if (actor != null)
				{
					try
					{
						((BaseSimObject)actor).addStatusEffect("invincible", 600f, pColorEffect: false);
					}
					catch
					{
					}
					_watched.Add(new Watched
					{
						A = actor,
						Marcher = flag3
					});
				}
			}
			Log.Info($"MOTION {_watched.Count} creatures round {spot.x},{spot.y}; fixed step {Time.fixedDeltaTime:F4}, fps lock 30 {Config.fps_lock_30}");
			Settings settings = _mod.Settings;
			int savedMode = settings.TimeMode;
			settings.TimeMode = 1;
			_mod.ForceRelight();
			_mod.SetYaw(0.6f);
			_mod.SetPitchDegrees(-4f);
			PersonRig.PlantStats = _plant;
			yield return Measure("game clock", gameClock: true, 9f, rng, spot);
			yield return Measure("real-time clock", gameClock: false, 9f, rng, spot);
			Report("turning, old way (10/s towards each raw step)", _oldTurns, " deg/s");
			Report("turning, now (bodies walking after their creatures)", _newTurns, " deg/s");
			Report("walking other than the way it faces", _slips, " deg");
			Report("drawn away from the game's place", _offsets, " tiles");
			int num2 = _newTurns.Count((float v) => v > 180f);
			int num3 = _oldTurns.Count((float v) => v > 180f);
			int num4 = _slips.Count((float v) => v > 30f);
			Log.Info($"MOTION frames turning faster than 180 deg/s: old {num3} of {_oldTurns.Count}, now {num2} of {_newTurns.Count}; walking over 30 deg sideways {num4} of {_slips.Count}");
			Check("no body turns faster than its cap", _newTurns.Count > 0 && _newTurns.Max() <= 340f);
			Check("turning over 180 deg/s at most a fifth as often", num2 * 5 <= num3);
			Check("walking sideways (over 30 deg) under 8% of the time", _slips.Count > 0 && (float)num4 < (float)_slips.Count * 0.08f);
			Check("never drawn further than its leash from the game's place (1.6 tiles)", _offsets.Count > 0 && _offsets.Max() <= 1.65f);
			yield return LegsCheck();
			yield return Alive(me, spot);
			yield return Filmstrip(me, spot);
			settings.TimeMode = savedMode;
			Finish(quit);
		}

		private IEnumerator Measure(string name, bool gameClock, float seconds, System.Random rng, WorldTile spot)
		{
			GameClock.OffForTest = gameClock;
			yield return new WaitForSecondsRealtime(1.5f);
			double world0 = World.world.getCurWorldTime();
			float real = 0f;
			float march = 0f;
			int frames = 0;
			float walked = 0f;
			float walkTime = 0f;
			float gamePace = 0f;
			foreach (Watched item in _watched)
			{
				item.Have = false;
			}
			while (real < seconds)
			{
				if (march <= 0f)
				{
					march = 4f;
					foreach (Watched item2 in _watched)
					{
						if (item2.Marcher && item2.A != null && item2.A.isAlive())
						{
							double num = rng.NextDouble() * Math.PI * 2.0;
							int pX = Mathf.Clamp(spot.x + (int)(Math.Cos(num) * 11.0), 2, MapBox.width - 3);
							int pY = Mathf.Clamp(spot.y + (int)(Math.Sin(num) * 11.0), 2, MapBox.height - 3);
							try
							{
								item2.A.goTo(World.world.GetTile(pX, pY));
							}
							catch
							{
							}
						}
					}
				}
				yield return null;
				real += Time.unscaledDeltaTime;
				march -= Time.unscaledDeltaTime;
				frames++;
				foreach (Watched item3 in _watched)
				{
					if (item3.A == null || !item3.A.isAlive() || !_mod.SceneForTest.MotionForTest(item3.A, out var drawn, out var real2, out var angle, out var speed, out var frame, out var dt) || frame == item3.Frame)
					{
						continue;
					}
					bool num2 = item3.Have && frame == item3.Frame + 1;
					if (num2)
					{
						Legs(item3.A, drawn, speed, dt);
					}
					else
					{
						_feet.Remove(item3.A);
					}
					if (num2)
					{
						Vector2 vector = real2 - item3.Real;
						float num3 = item3.Old;
						if (vector.sqrMagnitude > 1E-06f)
						{
							num3 = Mathf.LerpAngle(num3 * 57.29578f, Mathf.Atan2(vector.y, vector.x) * 57.29578f, 1f - Mathf.Exp(-10f * dt)) / 57.29578f;
						}
						if (speed > 0.3f && dt > 0f)
						{
							_newTurns.Add(Mathf.Abs(Mathf.DeltaAngle(item3.Angle * 57.29578f, angle * 57.29578f)) / dt);
							_oldTurns.Add(Mathf.Abs(Mathf.DeltaAngle(item3.Old * 57.29578f, num3 * 57.29578f)) / dt);
						}
						Vector2 vector2 = drawn - item3.Drawn;
						if (dt > 0f && vector2.magnitude / dt > 0.5f)
						{
							_slips.Add(Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(vector2.y, vector2.x) * 57.29578f, angle * 57.29578f)));
						}
						item3.Old = num3;
						bool flag = false;
						float num4 = 0f;
						try
						{
							flag = People.IsHumanoid(item3.A) && item3.A.is_moving && !item3.A.under_forces && !item3.A.has_attack_target;
							num4 = item3.A._current_combined_movement_speed * WalkPace.Of(item3.A);
						}
						catch
						{
						}
						float magnitude = (real2 - item3.Real).magnitude;
						if (flag && magnitude < 0.5f)
						{
							walked += magnitude;
							walkTime += dt;
							gamePace += num4 * dt;
						}
					}
					else
					{
						item3.Old = angle;
					}
					_offsets.Add((drawn - real2).magnitude);
					item3.Angle = angle;
					item3.Real = real2;
					item3.Drawn = drawn;
					item3.Frame = frame;
					item3.Have = true;
				}
			}
			double num5 = World.world.getCurWorldTime() - world0;
			float num6 = (float)(num5 / (double)real);
			float num7 = ((walkTime > 0f) ? (walked / walkTime) : 0f);
			float num8 = ((walkTime > 0f) ? (gamePace / walkTime) : 0f);
			Log.Info($"MOTION {name}: world went {num5:F2} s in {real:F2} s real ({num6:F2}x), {(float)frames / real:F1} fps, fixed step {Time.fixedDeltaTime:F4}; " + $"people walking cover {num7:F2} tiles a real second, their pace is {num8:F2} tiles a game second ({((num8 > 0f) ? (num7 / num8) : 0f):F2}x) over {walkTime:F1} s of walking");
			if (!gameClock)
			{
				Check("the world keeps real time (0.85x to 1.15x)", num6 > 0.85f && num6 < 1.15f);
			}
			if (!gameClock)
			{
				Check("people walk their pace in real time (0.8x to 1.2x)", num8 > 0f && num7 / num8 > 0.8f && num7 / num8 < 1.2f);
			}
		}

		private IEnumerator Filmstrip(Actor me, WorldTile spot)
		{
			Watched w = _watched.FirstOrDefault((Watched v) => v.Marcher && v.A != null && v.A.isAlive() && !v.A.has_attack_target) ?? _watched.FirstOrDefault((Watched v) => v.Marcher && v.A != null && v.A.isAlive());
			if (w == null)
			{
				yield break;
			}
			WorldTile tile = World.world.GetTile(spot.x - 6, spot.y + 4);
			WorldTile tile2 = World.world.GetTile(spot.x + 7, spot.y + 9);
			Vector2 vector = new Vector2((float)spot.x + 1f, (float)spot.y + 7f);
			Vector2 normalized = new Vector2(13f, 5f).normalized;
			_mod.DebugEye = vector + new Vector2(normalized.y, 0f - normalized.x) * 5f;
			_mod.DebugEyeHeight = 2.2f;
			_mod.DebugEyeSet = true;
			_mod.SetPitchDegrees(-14f);
			try
			{
				w.A.current_position = new Vector2(tile.posV3.x, tile.posV3.y);
				w.A.dirty_current_tile = true;
				w.A.goTo(tile2);
			}
			catch (Exception ex)
			{
				Log.Error("MOTION filmstrip: " + ex.Message);
				yield break;
			}
			int shot = 0;
			while (shot < 6)
			{
				for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
				{
					Vector2 vector2 = w.A.current_position - _mod.DebugEye;
					_mod.SetYaw(Mathf.Atan2(vector2.y, vector2.x));
					yield return null;
				}
				_mod.SceneForTest.MotionForTest(w.A, out var drawn, out var real, out var angle, out var speed, out var frame, out var _);
				Log.Info($"MOTION filmstrip {shot}: at {real.x:F2},{real.y:F2} drawn {drawn.x:F2},{drawn.y:F2}, facing {angle * 57.29578f:F0} deg, {speed:F2} tiles/s");
				_mod.SaveFrame($"motion_walk_{shot}.png");
				frame = shot++;
			}
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
		}

		private static Actor Spawn(string id, WorldTile t)
		{
			try
			{
				return World.world.units.spawnNewUnit(id, t, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("MOTION spawn " + id + ": " + ex.Message);
				return null;
			}
		}

		private static float Percentile(List<float> values, float p)
		{
			if (values.Count == 0)
			{
				return 0f;
			}
			List<float> list = values.OrderBy((float v) => v).ToList();
			return list[Mathf.Clamp((int)(p * (float)(list.Count - 1)), 0, list.Count - 1)];
		}

		private static void Report(string what, List<float> values, string unit)
		{
			Log.Info($"MOTION {what}: median {Percentile(values, 0.5f):F2}, 95% {Percentile(values, 0.95f):F2}, 99% {Percentile(values, 0.99f):F2}, max {((values.Count > 0) ? values.Max() : 0f):F2}{unit} ({values.Count} samples)");
		}

		private static void Check(string what, bool ok)
		{
			Log.Info("MOTION " + (ok ? "PASS" : "FAIL") + " " + what);
		}

		private static WorldTile FindOpen()
		{
			int width = MapBox.width;
			int height = MapBox.height;
			List<WorldTile> droppers = new List<WorldTile>();
			try
			{
				foreach (Building building in World.world.buildings)
				{
					if (building != null && building.asset != null && building.asset.spawn_drops && building.current_tile != null)
					{
						droppers.Add(building.current_tile);
					}
				}
			}
			catch
			{
			}
			for (int i = 0; i < Mathf.Max(width, height) / 2; i += 4)
			{
				for (int j = 0; j < 16; j++)
				{
					float f = (float)j * MathF.PI / 8f;
					int num = width / 2 + (int)(Mathf.Cos(f) * (float)i);
					int num2 = height / 2 + (int)(Mathf.Sin(f) * (float)i);
					if (num < 24 || num2 < 24 || num + 24 > width || num2 + 24 > height || Hazard(num, num2))
					{
						continue;
					}
					bool flag = true;
					for (int k = num2 - 12; k < num2 + 12 && flag; k++)
					{
						for (int l = num - 12; l < num + 12 && flag; l++)
						{
							WorldTile tile = World.world.GetTile(l, k);
							flag = tile != null && tile.Type != null && !tile.Type.liquid && !tile.Type.block && tile.zone != null && tile.zone.city == null;
						}
					}
					if (flag)
					{
						return World.world.GetTile(num, num2);
					}
				}
			}
			return null;
			bool Hazard(int cx, int cy)
			{
				foreach (WorldTile item in droppers)
				{
					if ((item.x - cx) * (item.x - cx) + (item.y - cy) * (item.y - cy) < 1296)
					{
						return true;
					}
				}
				for (int m = cy - 14; m <= cy + 14; m++)
				{
					for (int n = cx - 14; n <= cx + 14; n++)
					{
						WorldTile tile2 = World.world.GetTile(n, m);
						try
						{
							if (tile2 != null && ((tile2.Type != null && tile2.Type.lava) || tile2.isOnFire()))
							{
								return true;
							}
						}
						catch
						{
						}
					}
				}
				return false;
			}
		}

		private static void Finish(bool quit)
		{
			GameClock.OffForTest = false;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("MOTION DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private void Legs(Actor a, Vector2 drawn, float speed, float dt)
		{
			bool flag = false;
			bool flag2 = true;
			try
			{
				flag = People.IsHumanoid(a);
				flag2 = a.has_attack_target || a.under_forces;
			}
			catch
			{
			}
			MeshInstance mesh = null;
			PersonRig rig = null;
			int age = int.MaxValue;
			WalkCycle.Walker walker = ((flag && !flag2) ? People.LegsForTest(a, out mesh, out rig, out age) : null);
			if (walker == null || age > 1 || mesh == null || rig == null)
			{
				_feet.Remove(a);
				return;
			}
			float duty = WalkCycle.Duty(walker.Run);
			float angle;
			float lift;
			bool flag3 = WalkCycle.Leg(walker.Phase, left: true, duty, out angle, out lift);
			bool flag4 = WalkCycle.Leg(walker.Phase, left: false, duty, out lift, out angle);
			Vec3 vec = rig.SoleWorld(mesh, left: true);
			Vec3 vec2 = rig.SoleWorld(mesh, left: false);
			if (!_feet.TryGetValue(a, out var value))
			{
				value = (_feet[a] = new FeetSeen());
			}
			else
			{
				float magnitude = (drawn - value.Drawn).magnitude;
				float num = walker.Phase - value.Phase;
				if (speed > 0.3f && num >= 0f && num < MathF.PI * 2f)
				{
					_legTime += dt;
					_legSteps += num / MathF.PI;
					float num2 = mesh.Basis.C2.Z * rig.Height;
					_legBody += num2 * dt;
					_legPace += speed / Mathf.Max(0.1f, num2) * dt;
					_legRun += walker.Run * dt;
					_legSaid += speed * dt;
					if (magnitude < 0.5f)
					{
						_legDrawn += magnitude;
					}
					_legRunSwing += Mathf.Abs(walker.Run - value.Run);
				}
				if (speed > 0.3f && magnitude < 0.5f)
				{
					float footSlip = _footSlip;
					float footGround = _footGround;
					if (flag3 && value.DownL)
					{
						_footSlip += Flat(vec, value.L);
						_footGround += magnitude;
					}
					if (flag4 && value.DownR)
					{
						_footSlip += Flat(vec2, value.R);
						_footGround += magnitude;
					}
					string key = (mesh.Model?.Name ?? "?") + $" {mesh.Basis.C2.Z * rig.Height:F2}";
					_byModel.TryGetValue(key, out var value2);
					_byModel[key] = value2 + new Vector2(_footSlip - footSlip, _footGround - footGround);
				}
			}
			value.Drawn = drawn;
			value.L = vec;
			value.R = vec2;
			value.DownL = flag3;
			value.DownR = flag4;
			value.Phase = walker.Phase;
			value.Run = walker.Run;
		}

		private static float Flat(Vec3 a, Vec3 b)
		{
			return Mathf.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
		}

		private IEnumerator LegsCheck()
		{
			float num = ((_footGround > 0f) ? (_footSlip / _footGround) : 1f);
			float num2 = ((_legTime > 0f) ? (_legSteps / _legTime) : 0f);
			PersonRig.PlantStats = null;
			float num3 = Mathf.Max(0.001f, _legTime);
			Log.Info($"MOTION legs: a foot on the ground slid {num * 100f:F1}% of the way the body went ({_footGround:F1} tiles walked), {num2:F2} steps a second over {_legTime:F1} s of walking; " + $"{_legBody / num3:F2} tiles tall, {_legPace / num3:F2} body heights a second, running {_legRun / num3 * 100f:F0}%; legs leant {_plant[0] / Mathf.Max(1f, _plant[2]) * 57.29578f:F1} deg fore-aft and {_plant[1] / Mathf.Max(1f, _plant[2]) * 57.29578f:F1} aside to hold feet, out of reach {_plant[3] / Mathf.Max(1f, _plant[2]) * 100f:F1}% ({_plant[4] / Mathf.Max(1f, _plant[3]) * 100f:F0}% of those fore-aft, {_plant[5] / Mathf.Max(1f, _plant[3]):F2} legs out on average)");
			Log.Info($"MOTION legs: drawn {_legDrawn:F1} tiles while its speed said {_legSaid:F1}; walk-run swung {_legRunSwing / num3:F2} a second");
			List<KeyValuePair<string, Vector2>> list = new List<KeyValuePair<string, Vector2>>(_byModel);
			list.Sort((KeyValuePair<string, Vector2> x, KeyValuePair<string, Vector2> y) => y.Value.y.CompareTo(x.Value.y));
			for (int num4 = 0; num4 < list.Count && num4 < 6; num4++)
			{
				Log.Info($"MOTION legs by model: {list[num4].Key} tall: slid {list[num4].Value.x / Mathf.Max(0.001f, list[num4].Value.y) * 100f:F0}% over {list[num4].Value.y:F1} tiles");
			}
			Check("legs: a foot on the ground slides under 15% of the way the body goes", _footGround > 2f && num < 0.15f);
			Check("legs: people walking take 1.2 to 5.5 steps a second (quicker under a long dress, or on short legs)", num2 > 1.2f && num2 < 5.5f);
			Dictionary<Actor, float> phases = new Dictionary<Actor, float>();
			Actor actor = null;
			float num5 = float.MaxValue;
			Vector2 vector = ((_mod.Host != null) ? _mod.Host.current_position : Vector2.zero);
			foreach (Watched item in _watched)
			{
				if (item.A != null && item.A.isAlive() && People.IsHumanoid(item.A))
				{
					float sqrMagnitude = (item.A.current_position - vector).sqrMagnitude;
					if (sqrMagnitude < num5)
					{
						num5 = sqrMagnitude;
						actor = item.A;
					}
				}
			}
			if (actor != null)
			{
				Vector2 vector2 = actor.current_position - vector;
				_mod.SetYaw(Mathf.Atan2(vector2.y, vector2.x));
				yield return new WaitForSecondsRealtime(0.4f);
			}
			Config.paused = true;
			yield return new WaitForSecondsRealtime(0.4f);
			MeshInstance mesh;
			PersonRig rig;
			foreach (Watched item2 in _watched)
			{
				if (item2.A != null)
				{
					int age;
					WalkCycle.Walker walker = People.LegsForTest(item2.A, out mesh, out rig, out age);
					if (walker != null && age <= 1)
					{
						phases[item2.A] = walker.Phase;
					}
				}
			}
			yield return new WaitForSecondsRealtime(1f);
			int num6 = 0;
			foreach (KeyValuePair<Actor, float> item3 in phases)
			{
				int age2;
				WalkCycle.Walker walker2 = People.LegsForTest(item3.Key, out mesh, out rig, out age2);
				if (walker2 != null && Mathf.Abs(walker2.Phase - item3.Value) > 0.0001f)
				{
					num6++;
				}
			}
			Config.paused = false;
			Log.Info($"MOTION legs paused: {num6} of {phases.Count} people's legs moved in a second");
			Check("legs: still while the game is paused", phases.Count > 0 && num6 == 0);
		}

		private IEnumerator Record()
		{
			WorldTile site = null;
			Actor actor = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				actor = Found(item, 12, 4);
				if (actor != null)
				{
					site = item;
					break;
				}
			}
			if (actor == null)
			{
				Log.Error("MOTION no site for a village");
				yield break;
			}
			City city = actor.city;
			int houses = 0;
			for (int i = 0; i < 7; i++)
			{
				float f = (float)i * 0.9f + 0.4f;
				float num = 6f + (float)(i % 3) * 2f;
				if (Put(city, (i % 3 == 0) ? "house_human_1" : "house_human_0", site.x + (int)(Mathf.Cos(f) * num), site.y + (int)(Mathf.Sin(f) * num)))
				{
					houses++;
				}
			}
			string[] array = new string[12]
			{
				"sheep", "sheep", "sheep", "cow", "cow", "chicken", "chicken", "chicken", "cat", "cat",
				"dog", "sheep"
			};
			for (int j = 0; j < array.Length; j++)
			{
				float f2 = (float)j * 2.39996f;
				float num2 = 9f + (float)(j % 4) * 2f;
				WorldTile tile = World.world.GetTile(site.x + (int)(Mathf.Cos(f2) * num2), site.y + (int)(Mathf.Sin(f2) * num2));
				if (tile != null && tile.Type != null && !tile.Type.liquid && !tile.Type.block)
				{
					Spawn(array[j], tile);
				}
			}
			List<Actor> walkers = new List<Actor>();
			for (int k = 0; k < 4; k++)
			{
				WorldTile tile2 = World.world.GetTile(site.x - 14 + k * 9, site.y - 13);
				Actor actor2 = ((tile2 != null) ? Spawn("human", tile2) : null);
				if (actor2 != null)
				{
					walkers.Add(actor2);
				}
			}
			WorldTile t = World.world.GetTile(site.x - 12, site.y - 12) ?? site;
			Actor actor3 = Spawn("human", t);
			if (actor3 == null)
			{
				yield break;
			}
			try
			{
				((BaseSimObject)actor3).addStatusEffect("invincible", 600f, pColorEffect: false);
			}
			catch
			{
			}
			ControllableUnit.setControllableCreature(actor3);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("MOTION first person did not start");
				yield break;
			}
			_mod.HintUntil = 0f;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			Settings settings = _mod.Settings;
			int savedMode = settings.TimeMode;
			settings.TimeMode = 1;
			_mod.ForceRelight();
			_mod.DebugEye = new Vector2((float)site.x - 9f, (float)site.y - 9f);
			_mod.DebugEyeHeight = 4f;
			_mod.DebugEyeSet = true;
			_mod.SetYaw(MathF.PI / 4f);
			_mod.SetPitchDegrees(-14f);
			Log.Info($"MOTION recording a village at {site.x},{site.y}: {houses} houses, {city.units.Count} villagers, {walkers.Count} walkers, {50f} s");
			yield return null;
			yield return null;
			Solids(Path.Combine(Log.Folder, "motion_solid_start.txt"));
			string file = Path.Combine(Log.Folder, "motion_rec.txt");
			int lines = 0;
			HashSet<Actor> seen = new HashSet<Actor>();
			StringBuilder line = new StringBuilder(256);
			System.Random rng = new System.Random(11);
			StreamWriter w = new StreamWriter(file, append: false);
			try
			{
				w.WriteLine("# frame dt id kind humanoid x y moving thrown size looking cut n [x y]*n fighting");
				SceneCollector.Watch = delegate(Actor a, Vector2 p, float dt, bool thrown, float size)
				{
					line.Clear();
					List<Vector2> route;
					bool flag = Route(a, out route);
					bool flag2 = _mod.SceneForTest.LookAt.ContainsKey(a);
					line.Append(Time.frameCount).Append(' ').Append(F(dt))
						.Append(' ')
						.Append(a.GetHashCode())
						.Append(' ')
						.Append((a.asset != null) ? a.asset.id : "?")
						.Append(' ')
						.Append(People.IsHumanoid(a) ? 1 : 0)
						.Append(' ')
						.Append(F(p.x))
						.Append(' ')
						.Append(F(p.y))
						.Append(' ')
						.Append(Moving(a) ? 1 : 0)
						.Append(' ')
						.Append(thrown ? 1 : 0)
						.Append(' ')
						.Append(F(size))
						.Append(' ')
						.Append(flag2 ? 1 : 0)
						.Append(' ')
						.Append(flag ? 1 : 0)
						.Append(' ')
						.Append(route.Count);
					foreach (Vector2 item2 in route)
					{
						line.Append(' ').Append(F(item2.x)).Append(' ')
							.Append(F(item2.y));
					}
					bool flag3 = false;
					try
					{
						flag3 = a.has_attack_target;
					}
					catch
					{
					}
					line.Append(' ').Append(flag3 ? 1 : 0);
					w.WriteLine(line.ToString());
					lines++;
					seen.Add(a);
				};
				try
				{
					float march = 0f;
					for (float t2 = 0f; t2 < 50f; t2 += Time.unscaledDeltaTime)
					{
						if (march <= 0f)
						{
							march = 7f;
							foreach (Actor item3 in walkers)
							{
								if (item3 == null || !item3.isAlive())
								{
									continue;
								}
								double num3 = rng.NextDouble() * Math.PI * 2.0;
								WorldTile tile3 = World.world.GetTile(site.x + (int)(Math.Cos(num3) * 13.0), site.y + (int)(Math.Sin(num3) * 13.0));
								try
								{
									if (tile3 != null)
									{
										item3.goTo(tile3);
									}
								}
								catch
								{
								}
							}
						}
						march -= Time.unscaledDeltaTime;
						yield return null;
					}
				}
				finally
				{
					SceneCollector.Watch = null;
				}
			}
			finally
			{
				if (w != null)
				{
					((IDisposable)w).Dispose();
				}
			}
			Solids(Path.Combine(Log.Folder, "motion_solid_end.txt"));
			settings.TimeMode = savedMode;
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
			Log.Info($"MOTION recorded {lines} lines of {seen.Count} creatures to {file}");
		}

		private void Solids(string file)
		{
			float num = Mathf.Floor(_mod.DebugEye.x) - 40f;
			float num2 = Mathf.Floor(_mod.DebugEye.y) - 40f;
			int num3 = 0;
			using (StreamWriter streamWriter = new StreamWriter(file, append: false))
			{
				streamWriter.WriteLine($"{F(num)} {F(num2)} {F(0.25f)} {320} {320}");
				StringBuilder stringBuilder = new StringBuilder(320);
				for (int i = 0; i < 320; i++)
				{
					stringBuilder.Clear();
					for (int j = 0; j < 320; j++)
					{
						bool flag = Steering.Solid(new Vector2(num + ((float)j + 0.5f) * 0.25f, num2 + ((float)i + 0.5f) * 0.25f));
						stringBuilder.Append(flag ? '1' : '0');
						if (flag)
						{
							num3++;
						}
					}
					streamWriter.WriteLine(stringBuilder.ToString());
				}
			}
			Log.Info($"MOTION solids: {num3} of {102400} quarter tiles to {file}");
		}

		private static string F(float v)
		{
			return v.ToString("0.####", CultureInfo.InvariantCulture);
		}

		private static bool Moving(Actor a)
		{
			try
			{
				return a.is_moving;
			}
			catch
			{
				return false;
			}
		}

		private static bool Route(Actor a, out List<Vector2> route)
		{
			route = new List<Vector2>();
			try
			{
				if (!a.is_moving)
				{
					return false;
				}
				Vector2 current_position = a.current_position;
				Vector2 next_step_position = a.next_step_position;
				route.Add(next_step_position);
				float num = (next_step_position - current_position).magnitude;
				current_position = next_step_position;
				List<WorldTile> current_path = a.current_path;
				if (current_path == null)
				{
					return false;
				}
				for (int i = Mathf.Max(0, a.current_path_index); i < current_path.Count; i++)
				{
					if (!(num < 10f))
					{
						break;
					}
					if (route.Count >= 32)
					{
						break;
					}
					WorldTile worldTile = current_path[i];
					if (worldTile == null)
					{
						return false;
					}
					Vector2 vector = new Vector2(worldTile.posV3.x, worldTile.posV3.y);
					if (Steering.Solid(vector))
					{
						return true;
					}
					num += (vector - current_position).magnitude;
					route.Add(vector);
					current_position = vector;
				}
			}
			catch
			{
			}
			return false;
		}

		private static Actor Found(WorldTile center, int adults, int children)
		{
			List<Actor> list = new List<Actor>();
			for (int i = 0; i < adults + children; i++)
			{
				WorldTile worldTile = World.world.GetTile(center.x + i % 5 * 2 - 4, center.y + i / 5 * 2 - 3) ?? center;
				if (worldTile.main_type == null || worldTile.main_type.liquid)
				{
					worldTile = center;
				}
				try
				{
					Actor actor = World.world.units.spawnNewUnit("human", worldTile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, i < adults);
					if (actor != null)
					{
						list.Add(actor);
					}
				}
				catch (Exception ex)
				{
					Log.Error("MOTION spawn villager: " + ex.Message);
				}
			}
			if (list.Count == 0)
			{
				return null;
			}
			Actor actor2 = list[0];
			try
			{
				if (!actor2.buildCityAndStartCivilization())
				{
					foreach (Actor item in list)
					{
						if (item != null && item.isAlive())
						{
							item.dieSimpleNone();
						}
					}
					return null;
				}
				City city = actor2.city;
				foreach (Actor item2 in list)
				{
					if (item2 != actor2 && item2.city != city)
					{
						item2.joinCity(city);
					}
				}
				Kingdom kingdom = actor2.kingdom;
				if (kingdom != null && !kingdom.hasKing())
				{
					kingdom.setKing(actor2);
				}
			}
			catch (Exception ex2)
			{
				Log.Error("MOTION founding a village: " + ex2.Message);
				return null;
			}
			return actor2;
		}

		private static bool Put(City city, string id, int x, int y)
		{
			WorldTile tile = World.world.GetTile(x, y);
			if (tile == null || tile.Type == null || tile.Type.liquid || tile.Type.block)
			{
				return false;
			}
			try
			{
				if (tile.building != null)
				{
					tile.building.startDestroyBuilding();
				}
				Building building = World.world.buildings.addBuilding(id, tile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				if (building == null)
				{
					return false;
				}
				building.completeConstruction();
				building.setKingdom(city.kingdom);
				city.listBuilding(building);
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("MOTION house " + id + ": " + ex.Message);
				return false;
			}
		}
	}
}
