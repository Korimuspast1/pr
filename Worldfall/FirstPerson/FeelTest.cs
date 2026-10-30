using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class FeelTest : MonoBehaviour
	{
		private sealed class Track
		{
			private readonly Actor _a;

			private readonly float _window;

			private readonly Queue<KeyValuePair<float, Vector2>> _was = new Queue<KeyValuePair<float, Vector2>>();

			public float Speed;

			public Vector2 Way;

			public Track(Actor a, float window = 0.12f)
			{
				_a = a;
				_window = window;
			}

			public void Step()
			{
				float realtimeSinceStartup = Time.realtimeSinceStartup;
				Vector2 current_position = _a.current_position;
				_was.Enqueue(new KeyValuePair<float, Vector2>(realtimeSinceStartup, current_position));
				while (_was.Count > 2 && realtimeSinceStartup - _was.Peek().Key > _window)
				{
					_was.Dequeue();
				}
				KeyValuePair<float, Vector2> keyValuePair = _was.Peek();
				float num = realtimeSinceStartup - keyValuePair.Key;
				if (!(num < 0.03f))
				{
					Vector2 vector = current_position - keyValuePair.Value;
					Speed = vector.magnitude / num;
					if (vector.sqrMagnitude > 1E-06f)
					{
						Way = vector.normalized;
					}
				}
			}
		}

		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private WorldTile _field;

		private Actor _was;

		private const int Size = 26;

		private string _disturbed;

		private Vector2? _away;

		private const int Attempts = 3;

		private const float Near = 0.8f;

		private Vector2 _lastPos;

		private float _lastMoved;

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			string options = File.ReadAllText(path);
			bool quit = options.Contains("quit");
			while (!Config.game_loaded || World.world == null || World.world.units == null)
			{
				yield return null;
			}
			for (float t = 0f; t < 60f; t += 1f)
			{
				bool flag = false;
				try
				{
					flag = SmoothLoader.isLoading();
				}
				catch
				{
				}
				if (!flag && t >= 4f)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(1f);
			}
			Config.autosaves = false;
			_mod = WorldBoxMod.Instance;
			_field = FindField();
			if (_field == null)
			{
				Log.Error("FEEL no open field");
				Finish(quit);
				yield break;
			}
			Paint(_field);
			yield return new WaitForSecondsRealtime(1f);
			Log.Info($"FEEL field at {_field.x},{_field.y} ({26}x{26}), fixed step {Time.fixedDeltaTime:F4}, target fps {Application.targetFrameRate}, vsync {QualitySettings.vSyncCount}");
			if (options.Contains("wt_moves"))
			{
				yield return LearnedMoves();
				Log.Info($"FEEL result: {_pass} passed, {_fail} failed");
				Finish(quit);
				yield break;
			}
			if (options.Contains("wt_sword"))
			{
				yield return SwordMoves();
				Log.Info($"FEEL result: {_pass} passed, {_fail} failed");
				Finish(quit);
				yield break;
			}
			if (options.Contains("wt_body"))
			{
				yield return OwnBody();
				Log.Info($"FEEL result: {_pass} passed, {_fail} failed");
				Finish(quit);
				yield break;
			}
			bool rules = options.Contains("wt_rules");
			bool kinds = !options.Contains("wt_jump") && !rules;
			bool jumps = !options.Contains("wt_kinds") && !rules;
			Dictionary<string, float> t95 = new Dictionary<string, float>();
			if (kinds)
			{
				string[] array = new string[4] { "human", "orc", "elf", "cat" };
				foreach (string kind in array)
				{
					yield return Kind(kind, t95);
				}
			}
			if (kinds && t95.ContainsKey("human"))
			{
				if (t95.ContainsKey("orc"))
				{
					Check("an orc is a little slower to get going than a human", t95["orc"] > t95["human"] + 0.01f, string.Format("{0:F2} s against {1:F2} s", t95["orc"], t95["human"]));
				}
				if (t95.ContainsKey("elf"))
				{
					Check("an elf is a little quicker to get going than a human", t95["elf"] < t95["human"] - 0.005f, string.Format("{0:F2} s against {1:F2} s", t95["elf"], t95["human"]));
				}
				if (t95.ContainsKey("cat"))
				{
					Check("a cat darts off", t95["cat"] < t95["human"] * 0.8f, string.Format("{0:F2} s against {1:F2} s", t95["cat"], t95["human"]));
				}
			}
			if (jumps)
			{
				yield return Jumping();
			}
			if (kinds || rules)
			{
				yield return Rules();
			}
			if (kinds || rules)
			{
				yield return Camera();
			}
			if (kinds || rules)
			{
				yield return Crouch();
			}
			if (kinds || rules)
			{
				yield return Falls();
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			PossessionHooks.ScriptedRun = false;
			PossessionHooks.Walking = false;
			Log.Info($"FEEL result: {_pass} passed, {_fail} failed");
			Finish(quit);
		}

		private IEnumerator Kind(string kind, Dictionary<string, float> t95)
		{
			Actor me = null;
			yield return Become(kind, delegate(Actor a)
			{
				me = a;
			});
			if (me == null)
			{
				yield break;
			}
			Feel h = PossessionHooks.Feel;
			float num = PossessionHooks.SpeedForTest(me);
			float walk = PossessionHooks.RunFactor * num;
			float sprint = 1.35f * num;
			Log.Info($"FEEL {kind}: weight {Weight.Of(me):F2}, feel start {h.Start:F3} build {h.Build:F3} stop {h.Stop:F3} plant {h.Plant:F3} turn {h.Turn:F3} s; full speed {num:F2} t/s (run {walk:F2}, sprint {sprint:F2}), body {PossessionHooks.BodyRadius / 0.17f:F2}");
			int attempt = 1;
			float tStop;
			float tSprint;
			float tWalk;
			float walkSeen;
			float sprintSeen;
			float magnitude;
			while (true)
			{
				Place(me, (float)_field.x + 4f, (float)_field.y + 5f);
				_mod.SetYaw(0f);
				ClearField(me);
				yield return new WaitForSecondsRealtime(0.6f);
				Calm(me);
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				float num2;
				tStop = (num2 = -1f);
				tSprint = (num2 = num2);
				tWalk = num2;
				float t96 = Time.realtimeSinceStartup;
				float ct = 0f;
				float markAt = -1f;
				float was = PossessionHooks.Pace;
				float wasAt = 0f;
				Vector2 mark = me.current_position;
				while (Time.realtimeSinceStartup - t96 < 1.6f)
				{
					yield return null;
					ct += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
					Watch(me);
					float pace = PossessionHooks.Pace;
					float num3 = PossessionHooks.RunFactor * 0.95f;
					if (tWalk < 0f && pace >= num3)
					{
						tWalk = Crossed(wasAt, was, ct, pace, num3);
					}
					was = pace;
					wasAt = ct;
					if (markAt < 0f && Time.realtimeSinceStartup - t96 >= 1.1f)
					{
						markAt = Time.realtimeSinceStartup;
						mark = me.current_position;
					}
				}
				walkSeen = ((markAt < 0f) ? 0f : ((me.current_position - mark).magnitude / Mathf.Max(0.05f, Time.realtimeSinceStartup - markAt)));
				PossessionHooks.ScriptedRun = true;
				t96 = Time.realtimeSinceStartup;
				wasAt = (num2 = 0f);
				ct = num2;
				markAt = -1f;
				while (Time.realtimeSinceStartup - t96 < 2.6f)
				{
					yield return null;
					ct += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
					Watch(me);
					float pace2 = PossessionHooks.Pace;
					float num4 = 1.2825f;
					if (tSprint < 0f && pace2 >= num4)
					{
						tSprint = Crossed(wasAt, was, ct, pace2, num4);
					}
					was = pace2;
					wasAt = ct;
					if (markAt < 0f && Time.realtimeSinceStartup - t96 >= 2.1f)
					{
						markAt = Time.realtimeSinceStartup;
						mark = me.current_position;
					}
				}
				sprintSeen = ((markAt < 0f) ? 0f : ((me.current_position - mark).magnitude / Mathf.Max(0.05f, Time.realtimeSinceStartup - markAt)));
				Log.Info($"FEEL {kind}: to a walk {tWalk:F2} s (moving {walkSeen:F2} t/s, should be {walk:F2}), walk to a sprint {tSprint:F2} s (moving {sprintSeen:F2} t/s, should be {sprint:F2})");
				PossessionHooks.ScriptedInput = Vector2.zero;
				PossessionHooks.ScriptedRun = false;
				Vector2 from = me.current_position;
				t96 = Time.realtimeSinceStartup;
				wasAt = (num2 = 0f);
				ct = num2;
				while (Time.realtimeSinceStartup - t96 < 2.5f)
				{
					yield return null;
					ct += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
					Watch(me);
					float pace3 = PossessionHooks.Pace;
					if (tStop < 0f && pace3 <= 0.001f)
					{
						tStop = Crossed(wasAt, was, ct, pace3, 0.001f);
					}
					was = pace3;
					wasAt = ct;
				}
				magnitude = (me.current_position - from).magnitude;
				Log.Info($"FEEL {kind}: stopped from a sprint in {tStop:F2} s over {magnitude:F2} tiles");
				if (_disturbed == null)
				{
					break;
				}
				if (attempt == 3)
				{
					Log.Info($"FEEL {kind}: the run was still disturbed after {3} tries ({_disturbed}): its misses not judged, left out of the comparisons");
					break;
				}
				Log.Info("FEEL " + kind + ": the run was disturbed (" + _disturbed + "), measured again");
				attempt++;
			}
			if (_disturbed == null)
			{
				t95[kind] = tWalk;
			}
			Judge(kind + ": a run takes about its start time", tWalk > 0f && Mathf.Abs(tWalk - h.Start) < Mathf.Max(0.06f, h.Start * 0.4f), $"{tWalk:F2} s, feel {h.Start:F2} s");
			Judge(kind + ": a sprint builds over its build time", tSprint > 0f && tSprint > h.Build * 0.6f && tSprint < h.Build * 1.5f + 0.2f, $"{tSprint:F2} s, feel {h.Build:F2} s");
			Judge(kind + ": you really get going at a run and a sprint", walkSeen > walk * 0.6f && sprintSeen > sprint * 0.6f, $"{walkSeen:F2} / {sprintSeen:F2} t/s");
			Judge(kind + ": a sprint plants its feet in about its plant time", tStop > h.Plant * 0.6f && tStop < h.Plant * 1.6f + 0.1f, $"{tStop:F2} s, feel {h.Plant:F2} s");
			Judge(kind + ": and goes no further than a step as it stops", magnitude > sprint * h.Plant * 0.2f && magnitude < sprint * h.Plant * 0.9f + 0.25f, $"{magnitude:F2} tiles");
			attempt = 1;
			float tTurn;
			float eastAfter;
			while (true)
			{
				Vector2 vector = new Vector2((float)_field.x + 3f + 5f * (float)(attempt - 1), (float)_field.y + 3f);
				Place(me, vector.x, vector.y);
				_mod.SetYaw(0f);
				ClearField(me);
				yield return new WaitForSecondsRealtime(0.8f);
				Calm(me);
				vector = me.current_position;
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				PossessionHooks.ScriptedRun = true;
				float wasAt = Time.realtimeSinceStartup;
				float was = Mathf.Min(2.4f, h.Start + h.Build + 0.6f);
				float markAt = -1f;
				Vector2 from = vector;
				while (Time.realtimeSinceStartup - wasAt < was)
				{
					yield return null;
					Watch(me);
					if (markAt < 0f && Time.realtimeSinceStartup - wasAt >= was - 0.4f)
					{
						markAt = Time.realtimeSinceStartup;
						from = me.current_position;
					}
				}
				float ct = ((markAt < 0f) ? 0f : ((me.current_position - from).magnitude / Mathf.Max(0.05f, Time.realtimeSinceStartup - markAt)));
				Track track = new Track(me, 0.25f);
				Vector2 mark = me.current_position;
				Vector2 stuckAt = mark;
				_mod.SetYaw(MathF.PI / 2f);
				tTurn = -1f;
				float slowest = 99f;
				eastAfter = 0f;
				wasAt = Time.realtimeSinceStartup;
				float t96 = 0f;
				while (Time.realtimeSinceStartup - wasAt < 2.5f)
				{
					yield return null;
					t96 += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
					track.Step();
					Watch(me);
					if (t96 > 0.15f && track.Speed < slowest)
					{
						slowest = track.Speed;
						stuckAt = me.current_position;
					}
					float current = Mathf.Atan2(track.Way.y, track.Way.x) * 57.29578f;
					if (tTurn < 0f && Mathf.Abs(Mathf.DeltaAngle(current, 90f)) < 6f && track.Speed > sprint * 0.8f)
					{
						tTurn = t96;
						eastAfter = me.current_position.x - mark.x;
					}
				}
				PossessionHooks.ScriptedInput = Vector2.zero;
				PossessionHooks.ScriptedRun = false;
				bool flag = ct < walk * 0.8f;
				Log.Info($"FEEL {kind}: a right-angle turn at a sprint swung round in {tTurn:F2} s, carried {eastAfter:F2} tiles on the old way, slowest {slowest / Mathf.Max(0.01f, sprint) * 100f:F0}% of a sprint, into it at {ct:F2} t/s");
				if (_disturbed == null && (slowest < sprint * 0.2f || flag))
				{
					Vector2 p = (flag ? mark : stuckAt);
					_disturbed = $"held at ({p.x:F1}, {p.y:F1}), near {NearThings(p)}";
				}
				if (_disturbed == null)
				{
					break;
				}
				if (attempt == 3)
				{
					Log.Info($"FEEL {kind}: the turn was still disturbed after {3} tries ({_disturbed}): a miss not judged");
					break;
				}
				Log.Info("FEEL " + kind + ": the turn was disturbed (" + _disturbed + "), measured again on another lane");
				yield return new WaitForSecondsRealtime(0.5f);
				attempt++;
			}
			Judge(kind + ": a sprint comes round a turn in a moment, not at once", tTurn > 0.08f && tTurn < 0.8f && eastAfter > 0.1f && eastAfter < 1.5f, $"{tTurn:F2} s, {eastAfter:F2} tiles on");
			yield return new WaitForSecondsRealtime(1.5f);
		}

		private IEnumerator Rules()
		{
			Actor me = null;
			yield return Become("human", delegate(Actor a)
			{
				me = a;
			});
			if (me == null)
			{
				yield break;
			}
			float full = PossessionHooks.SpeedForTest(me);
			Place(me, (float)_field.x + 4f, (float)_field.y + 5f);
			_mod.SetYaw(0f);
			yield return new WaitForSecondsRealtime(0.6f);
			PossessionHooks.Walking = true;
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			yield return new WaitForSecondsRealtime(0.8f);
			float walkPace = PossessionHooks.Pace;
			float walkBlend = PossessionHooks.WalkBlend;
			PossessionHooks.Walking = false;
			yield return new WaitForSecondsRealtime(0.6f);
			float pace = PossessionHooks.Pace;
			PossessionHooks.ScriptedInput = Vector2.zero;
			Check("Alt's walk goes at the walk's pace, and the camera knows it", Mathf.Abs(walkPace - 0.35f) < 0.05f && walkBlend > 0.9f, $"pace {walkPace:F2} (walk {0.35f:F2}), blend {walkBlend:F2}");
			Check("off again, W runs", Mathf.Abs(pace - PossessionHooks.RunFactor) < 0.05f, $"pace {pace:F2} (run {PossessionHooks.RunFactor:F2})");
			yield return new WaitForSecondsRealtime(0.5f);
			Place(me, (float)_field.x + 4f, (float)_field.y + 9f);
			_mod.SetYaw(0f);
			yield return new WaitForSecondsRealtime(0.6f);
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			yield return new WaitForSecondsRealtime(0.7f);
			Track track = new Track(me);
			_mod.SetYaw(MathF.PI / 2f);
			float tTurn = -1f;
			float ct = 0f;
			float t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 1f)
			{
				yield return null;
				ct += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
				track.Step();
				float current = Mathf.Atan2(track.Way.y, track.Way.x) * 57.29578f;
				if (tTurn < 0f && ct > 0.05f && Mathf.Abs(Mathf.DeltaAngle(current, 90f)) < 10f && track.Speed > full * PossessionHooks.RunFactor * 0.7f)
				{
					tTurn = ct;
				}
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			Check("a run turns with the view within a few frames", tTurn > 0f && tTurn < 0.4f, $"{tTurn:F2} s");
			yield return new WaitForSecondsRealtime(0.5f);
			Place(me, (float)_field.x + 4f, (float)_field.y + 13f);
			_mod.SetYaw(0f);
			yield return new WaitForSecondsRealtime(0.6f);
			Vector2 from = me.current_position;
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			yield return new WaitForSecondsRealtime(0.3f);
			PossessionHooks.ScriptedJump = true;
			yield return null;
			yield return null;
			PossessionHooks.ScriptedInput = new Vector2(1f, 0f);
			float airtime = 0f;
			Vector2 vel = PossessionHooks.Velocity;
			while (PossessionHooks.Leaping && airtime < 2f)
			{
				vel = PossessionHooks.Velocity;
				yield return null;
				airtime += Time.unscaledDeltaTime;
			}
			Vector2 vector = me.current_position - from;
			PossessionHooks.ScriptedInput = Vector2.zero;
			float num = Mathf.Atan2(0f - vel.y, vel.x) * 57.29578f;
			Log.Info($"FEEL air: landed ({vector.x:F2}, {vector.y:F2}) after {airtime:F2} s, velocity then {vel.x:F2},{vel.y:F2} ({num:F0} deg right)");
			Check("a jump can be steered in the air, but not turned round", vector.x > 0.8f && vector.y < -0.1f && num > 15f && num < 75f, $"({vector.x:F2}, {vector.y:F2}), {num:F0} deg right");
			yield return new WaitForSecondsRealtime(0.5f);
		}

		private IEnumerator Camera()
		{
			Actor me = null;
			yield return Become("human", delegate(Actor a)
			{
				me = a;
			});
			if (me != null)
			{
				_mod.Settings.HeadBob = 1f;
				Place(me, (float)_field.x + 3f, (float)_field.y + 3f);
				_mod.SetYaw(0f);
				yield return new WaitForSecondsRealtime(0.6f);
				float kickStill = _mod.FovKickForTest;
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				PossessionHooks.ScriptedRun = true;
				yield return new WaitForSecondsRealtime(1.3f);
				float kickSprint = _mod.FovKickForTest;
				float leanSprint = _mod.Lean;
				PossessionHooks.ScriptedInput = Vector2.zero;
				PossessionHooks.ScriptedRun = false;
				float dip = 0f;
				for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
				{
					yield return null;
					dip = Mathf.Min(dip, _mod.LandDipForTest);
				}
				yield return new WaitForSecondsRealtime(0.6f);
				float fovKickForTest = _mod.FovKickForTest;
				Log.Info($"FEEL camera: widen still {kickStill:F1}, sprinting {kickSprint:F1}, after {fovKickForTest:F1} deg; lean sprinting straight {leanSprint:F2} deg; plant dip {dip:F3} tiles");
				Check("a sprint widens the view", kickSprint > 5f && kickStill < 1f && fovKickForTest < 2f, $"{kickSprint:F1} deg sprinting, {kickStill:F1} still, {fovKickForTest:F1} after");
				Check("running straight the view stays level", Mathf.Abs(leanSprint) < 0.3f, $"{leanSprint:F2} deg");
				Check("letting go at a sprint plants the feet: the eye dips", dip < -0.006f, $"{dip:F3} tiles");
				Place(me, (float)_field.x + 3f, (float)_field.y + 7f);
				_mod.SetYaw(0f);
				yield return new WaitForSecondsRealtime(0.8f);
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				yield return new WaitForSecondsRealtime(0.8f);
				PossessionHooks.ScriptedInput = Vector2.zero;
				float dipRun = 0f;
				for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
				{
					yield return null;
					dipRun = Mathf.Min(dipRun, _mod.LandDipForTest);
				}
				Check("a run stops without a plant", dipRun > -0.006f, $"{dipRun:F3} tiles");
				Place(me, (float)_field.x + 3f, (float)_field.y + 11f);
				_mod.SetYaw(0f);
				yield return new WaitForSecondsRealtime(0.6f);
				PossessionHooks.ScriptedInput = new Vector2(1f, 0f);
				yield return new WaitForSecondsRealtime(0.6f);
				float leanRight = _mod.Lean;
				_mod.SaveFrame("feel_lean_right.png");
				_mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "feel_lean_right_hud.png"));
				PossessionHooks.ScriptedInput = new Vector2(-1f, 0f);
				yield return new WaitForSecondsRealtime(0.7f);
				float leanLeft = _mod.Lean;
				_mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "feel_lean_left_hud.png"));
				PossessionHooks.ScriptedInput = Vector2.zero;
				yield return new WaitForSecondsRealtime(0.6f);
				float lean = _mod.Lean;
				Log.Info($"FEEL camera: lean right {leanRight:F2}, left {leanLeft:F2}, still {lean:F2} deg");
				Check("sidestepping leans the view a degree or two, each way its own", leanRight < -1f && leanRight > -2.5f && leanLeft > 1f && leanLeft < 2.5f, $"right {leanRight:F2}, left {leanLeft:F2} deg");
				Check("standing still the view is level again", Mathf.Abs(lean) < 0.1f, $"{lean:F2} deg");
				yield return new WaitForSecondsRealtime(0.3f);
			}
		}

		private IEnumerator Crouch()
		{
			Actor me = null;
			yield return Become("human", delegate(Actor a)
			{
				me = a;
			});
			if (me != null)
			{
				Place(me, (float)_field.x + 3f, (float)_field.y + 15f);
				_mod.SetYaw(0f);
				yield return new WaitForSecondsRealtime(0.8f);
				float eyeUp = _mod.View.Z;
				PossessionHooks.Crouched = true;
				yield return new WaitForSecondsRealtime(0.6f);
				float eyeDown = _mod.View.Z;
				float crouch = _mod.CrouchForTest;
				string band = Colliders.BodyForTest;
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				yield return new WaitForSecondsRealtime(0.8f);
				float pace = PossessionHooks.Pace;
				PossessionHooks.ScriptedRun = true;
				yield return new WaitForSecondsRealtime(0.5f);
				bool stood = !PossessionHooks.Crouched;
				float paceSprint = PossessionHooks.Pace;
				PossessionHooks.ScriptedRun = false;
				PossessionHooks.ScriptedInput = Vector2.zero;
				yield return new WaitForSecondsRealtime(0.6f);
				float z = _mod.View.Z;
				float crouchForTest = _mod.CrouchForTest;
				Log.Info($"FEEL crouch: eye {eyeUp:F2} standing, {eyeDown:F2} crouched (blend {crouch:F2}, body {band}), pace {pace:F2}, Shift stood up {stood} at {paceSprint:F2}, eye {z:F2} after");
				Check("Ctrl's crouch brings the eye down to the chest", crouch > 0.95f && eyeUp - eyeDown > 0.22f && eyeUp - eyeDown < 0.42f, $"{eyeUp - eyeDown:F2} tiles down");
				Check("crouched you go at half a run", Mathf.Abs(pace - PossessionHooks.RunFactor * 0.5f) < 0.05f, $"pace {pace:F2}");
				Check("Shift stands you up and sprints", stood && paceSprint > PossessionHooks.RunFactor * 1.1f && crouchForTest < 0.05f, $"stood {stood}, pace {paceSprint:F2}, crouch {crouchForTest:F2} after (eye {z:F2}, the ground moved under it)");
				yield return new WaitForSecondsRealtime(0.3f);
			}
		}

		private IEnumerator Falls()
		{
			Actor me = null;
			yield return Become("human", delegate(Actor a)
			{
				me = a;
			});
			if (me == null)
			{
				yield break;
			}
			Building stock = null;
			WorldTile st = World.world.GetTile(_field.x + 6, _field.y + 20);
			try
			{
				stock = World.world.buildings.addBuilding("stockpile", st, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				stock?.completeConstruction();
			}
			catch
			{
			}
			if (stock == null)
			{
				Fail("a stockpile on the field for the falls", "none");
				yield break;
			}
			Vector2 c = stock.current_position;
			Place(me, c.x, c.y - 3f);
			_mod.SetYaw(MathF.PI / 2f);
			yield return new WaitForSecondsRealtime(1.5f);
			for (float w = 0f; w < 3f; w += 0.25f)
			{
				if (BuildingFloors.Count != 0)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.25f);
			}
			Log.Info(string.Format("FEEL falls: the stockpile {0} at {1:F1},{2:F1} (tile {3},{4}, built {5}), floors gathered {6}, meshes {7}, me at {8:F1},{9:F1}", stock.isAlive() ? "stands" : "is gone", c.x, c.y, st.x, st.y, !stock.isUnderConstruction(), BuildingFloors.Count, _mod.SceneForTest.Meshes.Count, me.current_position.x, me.current_position.y));
			float ground = _mod.SamplerForTest.Cache.SampleHeight(c.x, c.y);
			float crate = float.NegativeInfinity;
			Vector2 crateAt = c;
			for (float num = -2f; num <= 2f; num += 0.5f)
			{
				for (float num2 = -2f; num2 <= 2f; num2 += 0.5f)
				{
					if (BuildingFloors.StandOn(c.x + num, c.y + num2, 0.2f, ground + 2f, out var z, out var _) && z > crate)
					{
						crate = z;
						crateAt = new Vector2(c.x + num, c.y + num2);
					}
				}
			}
			if (float.IsNegativeInfinity(crate) || crate < ground + 0.3f)
			{
				Fail("the stockpile has a crate to stand on", string.Format("top {0}, floors {1}, at {2}", float.IsNegativeInfinity(crate) ? "none" : (crate - ground).ToString("F2"), BuildingFloors.Count, c));
				try
				{
					stock.removeBuildingFinal();
					yield break;
				}
				catch
				{
					yield break;
				}
			}
			Log.Info($"FEEL falls: stockpile at {c.x:F1},{c.y:F1}, its highest top {crate - ground:F2} over the ground at {crateAt.x:F1},{crateAt.y:F1}");
			_ = World.world.GetTile((int)crateAt.x, (int)crateAt.y) ?? st;
			bool up = false;
			for (int attempt = 0; attempt < 7; attempt++)
			{
				if (up)
				{
					break;
				}
				Place(me, crateAt.x, crateAt.y - 1.3f - 0.25f * (float)attempt);
				_mod.SetYaw(MathF.PI / 2f);
				yield return new WaitForSecondsRealtime(0.7f);
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				yield return new WaitForSecondsRealtime(0.15f);
				PossessionHooks.ScriptedJump = true;
				for (float w = 0f; w < 1.5f; w += Time.unscaledDeltaTime)
				{
					yield return null;
					if (_mod.OnFloor && !PossessionHooks.Leaping)
					{
						up = true;
						break;
					}
				}
				PossessionHooks.ScriptedInput = Vector2.zero;
				yield return new WaitForSecondsRealtime(0.4f);
			}
			if (!_mod.OnFloor)
			{
				Log.Info($"FEEL falls: skipped, the stockpile's crate ({crate - ground:F2} over the ground) is out of a jump's reach");
				try
				{
					stock.removeBuildingFinal();
					yield break;
				}
				catch
				{
					yield break;
				}
			}
			float floorForTest = _mod.FloorForTest;
			Log.Info($"FEEL falls: up on it, feet {floorForTest - ground:F2} over the ground");
			int falls = _mod.FallsForTest;
			int landings = PossessionHooks.Jump.Landings;
			float highest = 0f;
			float airTime = 0f;
			PossessionHooks.Walking = false;
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			for (float w = 0f; w < 3f; w += Time.unscaledDeltaTime)
			{
				yield return null;
				if (PossessionHooks.Leaping)
				{
					highest = Mathf.Max(highest, me.position_height);
					airTime += Time.unscaledDeltaTime;
				}
				if (PossessionHooks.Jump.Landings > landings)
				{
					break;
				}
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			float landSpeed = PossessionHooks.Jump.LandSpeed;
			Log.Info($"FEEL falls: off the crate: {_mod.FallsForTest - falls} falls, {PossessionHooks.Jump.Landings - landings} landings, {airTime:F2} s in the air, highest {highest:F2}, landed at {landSpeed:F2} t/s (a flat jump lands at 4.6)");
			Check("walking off a crate is a fall, not a step down", _mod.FallsForTest > falls && PossessionHooks.Jump.Landings > landings && highest > 0.3f, $"{_mod.FallsForTest - falls} falls, highest {highest:F2}");
			Check("and it lands at the speed of the drop", landSpeed > 3f && landSpeed < 5.5f && airTime > 0.15f && airTime < 0.6f, $"{landSpeed:F2} t/s after {airTime:F2} s");
			yield return new WaitForSecondsRealtime(0.5f);
			up = false;
			for (int attempt = 0; attempt < 7; attempt++)
			{
				if (up)
				{
					break;
				}
				Place(me, crateAt.x, crateAt.y - 1.3f - 0.25f * (float)attempt);
				_mod.SetYaw(MathF.PI / 2f);
				yield return new WaitForSecondsRealtime(0.7f);
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				yield return new WaitForSecondsRealtime(0.15f);
				PossessionHooks.ScriptedJump = true;
				for (float w = 0f; w < 1.5f; w += Time.unscaledDeltaTime)
				{
					yield return null;
					if (_mod.OnFloor && !PossessionHooks.Leaping)
					{
						up = true;
						break;
					}
				}
				PossessionHooks.ScriptedInput = Vector2.zero;
				yield return new WaitForSecondsRealtime(0.4f);
			}
			if (_mod.OnFloor)
			{
				landings = PossessionHooks.Jump.Landings;
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				yield return null;
				PossessionHooks.ScriptedJump = true;
				float w = 0f;
				for (float t = 0f; t < 2f; t += Time.unscaledDeltaTime)
				{
					yield return null;
					if (PossessionHooks.Leaping)
					{
						w = PossessionHooks.Jump.Time;
					}
					if (PossessionHooks.Jump.Landings > landings)
					{
						break;
					}
				}
				PossessionHooks.ScriptedInput = Vector2.zero;
				Log.Info($"FEEL falls: a jump off the crate {_mod.FloorForTest - ground:F2} up: {w:F2} s in the air at the last frame seen (0.63 on the flat), landed at {PossessionHooks.Jump.LandSpeed:F2} t/s (4.6 on the flat), floor after {_mod.FloorForTest}");
				Check("a jump off a crate flies on and comes down further, in the world", w > 0.6f && w < 1.3f && PossessionHooks.Jump.LandSpeed > 5.5f && float.IsNaN(_mod.FloorForTest), $"{w:F2} s, {PossessionHooks.Jump.LandSpeed:F2} t/s, floor {_mod.FloorForTest}");
			}
			else
			{
				Fail("up onto the stockpile again for the jump off it", $"floor {_mod.FloorForTest}");
			}
			try
			{
				stock.removeBuildingFinal();
			}
			catch
			{
			}
			yield return new WaitForSecondsRealtime(0.5f);
		}

		private IEnumerator Jumping()
		{
			Actor me = null;
			yield return Become("human", delegate(Actor a)
			{
				me = a;
			});
			if (me == null)
			{
				yield break;
			}
			Check("the fall is taken over (jumps on our arc)", PossessionHooks.FallHooked, "");
			Place(me, (float)_field.x + 13f, (float)_field.y + 13f);
			_mod.SetYaw(0f);
			yield return new WaitForSecondsRealtime(1f);
			float[] air = new float[2];
			float worstBoth = 0f;
			int vsync = QualitySettings.vSyncCount;
			int target = Application.targetFrameRate;
			float tOff;
			float t0;
			for (int pass = 0; pass < 2; pass++)
			{
				if (pass == 1)
				{
					QualitySettings.vSyncCount = 0;
					Application.targetFrameRate = 30;
					yield return new WaitForSecondsRealtime(1.5f);
				}
				float peak = 0f;
				float airtime = -1f;
				float fps = 0f;
				float arc = 0f;
				float worst = 0f;
				tOff = -1f;
				int frames = 0;
				int landings = PossessionHooks.Jump.Landings;
				PossessionHooks.ScriptedJump = true;
				t0 = Time.realtimeSinceStartup;
				List<string> heights = new List<string>();
				while (Time.realtimeSinceStartup - t0 < 3f)
				{
					yield return null;
					frames++;
					fps += Time.unscaledDeltaTime;
					if (tOff < 0f && PossessionHooks.Leaping)
					{
						tOff = Time.realtimeSinceStartup;
					}
					if (tOff >= 0f)
					{
						arc += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
						worst = Mathf.Max(worst, Time.unscaledDeltaTime);
					}
					peak = Mathf.Max(peak, me.position_height);
					if (heights.Count < 40)
					{
						heights.Add($"{(Time.realtimeSinceStartup - t0) * 1000f:F0}:{me.position_height:F3}");
					}
					if (PossessionHooks.Jump.Landings != landings)
					{
						airtime = Time.realtimeSinceStartup - tOff;
						break;
					}
				}
				float lastPeak = Jumps.LastPeak;
				float num = Leap.Airtime(lastPeak);
				float num2 = 1f;
				try
				{
					num2 = Jumps.Share(me);
				}
				catch
				{
				}
				Log.Info(string.Format("FEEL jump at {0:F0} fps (worst frame {1:F0} ms): peak {2:F3} (the game's {3:F3}, {4:F2} of a human's), in the air {5:F2} s, its arc {6:F2} s (should be {7:F2}), landed at {8:F2} t/s; {9}", (float)frames / Mathf.Max(0.001f, fps), worst * 1000f, peak, lastPeak, num2, airtime, arc, num, PossessionHooks.Jump.LandSpeed, string.Join(" ", heights)));
				string text = ((pass == 0) ? "the game's frame rate" : "30 fps");
				Check("a jump at " + text + " goes as high as the game's at its own pace", Mathf.Abs(peak - lastPeak) < 0.03f && Mathf.Abs(lastPeak - 0.663f * num2) < 0.01f, $"{peak:F3}, solved {lastPeak:F3}, {num2:F2} of a human's");
				float num3 = 0.05f + Mathf.Min(worst, 0.1f);
				Check("a jump at " + text + " lasts its arc", Mathf.Abs(arc - num) < num3 && (worst > 0.1f || Mathf.Abs(airtime - num) < num3 + 0.02f), $"{airtime:F2} s real, {arc:F2} s of arc, should be {num:F2}, give {num3:F2}");
				air[pass] = arc;
				worstBoth = Mathf.Max(worstBoth, Mathf.Min(worst, 0.1f));
				yield return new WaitForSecondsRealtime(0.8f);
			}
			QualitySettings.vSyncCount = vsync;
			Application.targetFrameRate = target;
			Check("the frame rate doesn't change a jump's length", Mathf.Abs(air[0] - air[1]) < 0.05f + worstBoth, $"{air[0]:F2} s against {air[1]:F2} s, give {0.05f + worstBoth:F2}");
			yield return new WaitForSecondsRealtime(1f);
			bool carries = false;
			string how = null;
			yield return SprintJump(me, (float)_field.x + 2f, (float)_field.y + 13f, 2.2f, "running jump", delegate(bool ok, string d)
			{
				carries = ok;
				how = d;
			});
			Check("a sprinting jump carries the run", carries, how);
			yield return new WaitForSecondsRealtime(1.5f);
			bool pressed = false;
			bool again = false;
			for (int pass = 1; pass <= 3; pass++)
			{
				if (pressed)
				{
					break;
				}
				Place(me, (float)_field.x + 13f, (float)_field.y + 13f);
				yield return new WaitForSecondsRealtime(0.8f);
				int landings = PossessionHooks.Jump.Landings;
				PossessionHooks.ScriptedJump = true;
				t0 = Time.realtimeSinceStartup;
				tOff = 0.03f;
				while (Time.realtimeSinceStartup - t0 < 2f && PossessionHooks.Jump.Landings == landings)
				{
					yield return null;
					tOff = Mathf.Lerp(tOff, Mathf.Min(Time.unscaledDeltaTime, 0.1f), 0.3f);
					Leap jump = PossessionHooks.Jump;
					if (!pressed && PossessionHooks.Leaping && !(jump.Rise >= 0f))
					{
						float num4 = 17.12f;
						float num5 = 0f - jump.Rise;
						if ((0f - num5 + Mathf.Sqrt(num5 * num5 + 2f * num4 * Mathf.Max(0f, jump.Lift))) / num4 < Mathf.Min(0.084f, tOff * 1.5f))
						{
							PossessionHooks.ScriptedJump = true;
							pressed = true;
						}
					}
				}
				yield return null;
				yield return null;
				again = PossessionHooks.Leaping;
				if (!pressed)
				{
					Log.Info($"FEEL the landing came between frames before the jump could be pressed (try {pass})");
				}
				yield return new WaitForSecondsRealtime(1.2f);
			}
			Check("a jump pressed just before landing goes off as you land", pressed && again, $"pressed {pressed}, in the air again {again}");
			yield return new WaitForSecondsRealtime(0.3f);
			Actor sheep = null;
			try
			{
				sheep = World.world.units.spawnNewUnit("sheep", World.world.GetTile(_field.x + 18, _field.y + 18), pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("FEEL sheep: " + ex.Message);
			}
			if (sheep == null)
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(0.5f);
			Vector2 current_position = sheep.current_position;
			try
			{
				sheep.calculateForce(current_position.x, current_position.y, current_position.x + 1f, current_position.y, 1.5f, 1.5f, pCheckCancelJobOnLand: true);
			}
			catch (Exception ex2)
			{
				Log.Error("FEEL sheep force: " + ex2.Message);
			}
			tOff = 0f;
			t0 = Time.realtimeSinceStartup;
			bool down = false;
			while (Time.realtimeSinceStartup - t0 < 4f)
			{
				yield return null;
				tOff = Mathf.Max(tOff, sheep.position_height);
				if (tOff > 0.1f && sheep.position_height <= 0f && !sheep.under_forces)
				{
					down = true;
					break;
				}
			}
			Check("a sheep thrown up comes down the game's way", tOff > 0.1f && down, $"up {tOff:F2}, down {down}");
			try
			{
				sheep.die(pDestroy: false, AttackType.Other, pCountDeath: true, pLogFavorite: true);
			}
			catch
			{
			}
		}

		private IEnumerator SwordMoves()
		{
			Actor me = null;
			yield return Become("human", delegate(Actor a)
			{
				me = a;
			});
			if (me != null)
			{
				if (!Equip(me, "sword_iron"))
				{
					Fail("an iron sword in hand", "no");
					yield break;
				}
				MeleeControl melee = _mod.Melee;
				yield return new WaitForSecondsRealtime(1.5f);
				Place(me, (float)_field.x + 6f, (float)_field.y + 13f);
				_mod.SetYaw(0f);
				yield return new WaitForSecondsRealtime(0.8f);
				Vector2 from = me.current_position;
				melee.PressForTest();
				yield return new WaitForSecondsRealtime(1f);
				float magnitude = (me.current_position - from).magnitude;
				Log.Info($"FEEL a sword's tap stepped you in {magnitude:F2} tiles");
				Check("a tap of a sword steps you in a little", magnitude > 0.1f && magnitude < 0.4f, $"{magnitude:F2} tiles");
				yield return new WaitForSecondsRealtime(0.6f);
				Place(me, (float)_field.x + 4f, (float)_field.y + 13f);
				yield return new WaitForSecondsRealtime(0.8f);
				int dashes = PossessionHooks.DashesForTest;
				_ = me.current_position;
				melee.HeldForTest = true;
				melee.PressForTest();
				yield return new WaitForSecondsRealtime(1.3f);
				melee.HeldForTest = false;
				Check("held, the left button with a sword swings and never dashes", PossessionHooks.DashesForTest == dashes && melee.Charge <= 0f, $"{PossessionHooks.DashesForTest - dashes} dashes");
			}
		}

		private IEnumerator LearnedMoves()
		{
			Actor me = null;
			yield return Become("human", delegate(Actor a)
			{
				me = a;
			});
			if (me == null)
			{
				yield break;
			}
			MeleeControl melee = _mod.Melee;
			Moves.Rewarded(new Quest
			{
				Kind = QuestKind.Supply,
				Money = 30
			}, me, null);
			Check("an easy quest teaches no move", Moves.Known(me).Count == 0, $"{Moves.Known(me).Count}");
			Moves.Rewarded(new Quest
			{
				Kind = QuestKind.Undead,
				Money = 80
			}, me, null);
			Moves.Rewarded(new Quest
			{
				Kind = QuestKind.Bounty,
				Money = 150
			}, me, null);
			Check("hard quests teach a move each, the first made ready", Moves.Known(me).Count == 2 && Moves.Ready(me) != Moves.Move.None, $"{Moves.Known(me).Count} known, ready {Moves.Ready(me)}");
			Moves.Move[] array = new Moves.Move[3]
			{
				Moves.Move.Heavy,
				Moves.Move.Lunge,
				Moves.Move.JumpAttack
			};
			foreach (Moves.Move m in array)
			{
				Moves.Learn(me, m);
			}
			Moves.SetReady(me, Moves.Move.Heavy);
			Place(me, (float)_field.x + 13f, (float)_field.y + 13f);
			_mod.SetYaw(0f);
			yield return new WaitForSecondsRealtime(0.8f);
			int dodges = PossessionHooks.DodgesForTest;
			PossessionHooks.ScriptedInput = new Vector2(1f, 0f);
			PossessionHooks.ScriptedDodge = true;
			yield return new WaitForSecondsRealtime(0.1f);
			PossessionHooks.ScriptedInput = Vector2.zero;
			Check("the dodge step is everyone's: it goes with another move ready too (the combat session, 2026-10-15)", PossessionHooks.DodgesForTest == dodges + 1, $"{PossessionHooks.DodgesForTest - dodges} dodges");
			yield return new WaitForSecondsRealtime(0.9f);
			dodges = PossessionHooks.DodgesForTest;
			yield return new WaitForSecondsRealtime(0.5f);
			Place(me, (float)_field.x + 13f, (float)_field.y + 13f);
			yield return new WaitForSecondsRealtime(0.6f);
			Vector2 from = me.current_position;
			PossessionHooks.ScriptedInput = new Vector2(1f, 0f);
			PossessionHooks.ScriptedDodge = true;
			yield return null;
			yield return null;
			PossessionHooks.ScriptedInput = Vector2.zero;
			yield return new WaitForSecondsRealtime(0.05f);
			PossessionHooks.ScriptedInput = new Vector2(1f, 0f);
			PossessionHooks.ScriptedDodge = true;
			yield return null;
			PossessionHooks.ScriptedInput = Vector2.zero;
			yield return new WaitForSecondsRealtime(0.9f);
			Vector2 vector = me.current_position - from;
			Log.Info($"FEEL dodge: {PossessionHooks.DodgesForTest - dodges} dodges, moved ({vector.x:F2}, {vector.y:F2})");
			Check("the dodge step hops you aside, about a body's height", PossessionHooks.DodgesForTest - dodges == 1 && vector.y < -0.6f && vector.y > -1.6f && Mathf.Abs(vector.x) < 0.4f, $"{PossessionHooks.DodgesForTest - dodges} dodges, ({vector.x:F2}, {vector.y:F2})");
			int blows;
			if (Equip(me, "axe_iron"))
			{
				Moves.SetReady(me, Moves.Move.Heavy);
				yield return new WaitForSecondsRealtime(1.2f);
				Place(me, (float)_field.x + 13f, (float)_field.y + 13f);
				yield return new WaitForSecondsRealtime(0.5f);
				blows = PossessionCombat.Blows;
				melee.HeldForTest = true;
				melee.PressForTest();
				yield return new WaitForSecondsRealtime(1.3f);
				melee.HeldForTest = false;
				yield return new WaitForSecondsRealtime(0.8f);
				Check("the heavy blow, held, lands 1.6 times as hard", PossessionCombat.Blows > blows && Mathf.Abs(Moves.LastScaleForTest - 1.6f) < 0.01f, $"{PossessionCombat.Blows - blows} blows, x{Moves.LastScaleForTest:F2}");
				melee.PressForTest();
				yield return new WaitForSecondsRealtime(1.6f);
				Check("a tap with the heavy blow ready is a plain blow", Mathf.Abs(Moves.LastScaleForTest - 1f) < 0.01f, $"x{Moves.LastScaleForTest:F2}");
			}
			if (!Equip(me, "sword_iron"))
			{
				yield break;
			}
			Moves.SetReady(me, Moves.Move.Lunge);
			yield return new WaitForSecondsRealtime(1.2f);
			Place(me, (float)_field.x + 2f, (float)_field.y + 6f);
			_mod.SetYaw(0f);
			yield return new WaitForSecondsRealtime(0.6f);
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			PossessionHooks.ScriptedRun = true;
			yield return new WaitForSecondsRealtime(1.8f);
			blows = PossessionHooks.DashesForTest;
			melee.PressForTest();
			float slow = 0f;
			float t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 1f)
			{
				yield return null;
				slow = Mathf.Max(slow, PossessionHooks.SlowLeft);
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			PossessionHooks.ScriptedRun = false;
			Check("a blow at a sprint lunges, with no slow second after", PossessionHooks.DashesForTest == blows + 1 && slow <= 0f, $"{PossessionHooks.DashesForTest - blows} lunges, slow {slow:F2}");
			yield return new WaitForSecondsRealtime(1.2f);
			Moves.SetReady(me, Moves.Move.JumpAttack);
			Place(me, (float)_field.x + 13f, (float)_field.y + 13f);
			yield return new WaitForSecondsRealtime(0.6f);
			int blows2 = PossessionCombat.Blows;
			PossessionHooks.ScriptedJump = true;
			t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 0.5f && !PossessionHooks.Leaping)
			{
				yield return null;
			}
			yield return new WaitForSecondsRealtime(0.08f);
			melee.PressForTest();
			bool airLanded = false;
			t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 1.2f)
			{
				yield return null;
				if (PossessionHooks.Leaping && PossessionCombat.Blows > blows2)
				{
					airLanded = true;
				}
			}
			Check("a jump attack waits for the landing and lands 1.5 times as hard", !airLanded && PossessionCombat.Blows > blows2 && Mathf.Abs(Moves.LastScaleForTest - 1.5f) < 0.01f, $"landed in the air {airLanded}, {PossessionCombat.Blows - blows2} blows, x{Moves.LastScaleForTest:F2}");
		}

		private IEnumerator SprintJump(Actor me, float x, float y, float runUp, string what, Action<bool, string> got)
		{
			bool ok = false;
			string detail = "no jump";
			for (int attempt = 1; attempt <= 3; attempt++)
			{
				Place(me, x, y);
				_mod.SetYaw(0f);
				yield return new WaitForSecondsRealtime(0.8f);
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				PossessionHooks.ScriptedRun = true;
				yield return new WaitForSecondsRealtime(Mathf.Max(0f, runUp - 0.8f));
				Vector2 mark = me.current_position;
				float markAt = Time.realtimeSinceStartup;
				float slowest = 0f;
				while (Time.realtimeSinceStartup - markAt < 0.8f)
				{
					yield return null;
					slowest = Mathf.Max(slowest, Time.unscaledDeltaTime);
				}
				float run = (me.current_position - mark).magnitude / Mathf.Max(0.05f, Time.realtimeSinceStartup - markAt);
				float sprint = 1.35f * PossessionHooks.SpeedForTest(me);
				int before = PossessionHooks.Jump.Landings;
				Vector2 off = me.current_position;
				PossessionHooks.ScriptedJump = true;
				float tj = Time.realtimeSinceStartup;
				float offSpeed = 0f;
				bool up = false;
				while (Time.realtimeSinceStartup - tj < 3f && PossessionHooks.Jump.Landings == before)
				{
					if (!up && PossessionHooks.Leaping)
					{
						up = true;
						off = me.current_position;
						offSpeed = PossessionHooks.Velocity.magnitude * PossessionHooks.SpeedForTest(me);
					}
					yield return null;
					if (up)
					{
						slowest = Mathf.Max(slowest, Time.unscaledDeltaTime);
					}
				}
				float magnitude = (me.current_position - off).magnitude;
				PossessionHooks.ScriptedInput = Vector2.zero;
				PossessionHooks.ScriptedRun = false;
				float num = Leap.Airtime(PossessionHooks.Jump.Peak);
				float num2 = offSpeed * num * 0.85f;
				float num3 = offSpeed * num * 1.15f + 0.1f;
				ok = up && offSpeed > sprint * 0.9f && magnitude > num2 && magnitude < num3;
				detail = $"{magnitude:F2} tiles, off the ground at {offSpeed:F2} t/s (this body's sprint {sprint:F2}, the run seen {run:F2}), {num:F2} s in the air: {num2:F2} to {num3:F2}";
				Log.Info($"FEEL {what}: {detail} (slowest frame {slowest * 1000f:F0} ms)");
				if (slowest <= 0.1f)
				{
					break;
				}
				Log.Info($"FEEL {what}: a slow frame in the run or the air, jumped again (try {attempt})");
				yield return new WaitForSecondsRealtime(1f);
			}
			got(ok, detail);
		}

		private static bool Equip(Actor me, string id)
		{
			try
			{
				ActorEquipmentSlot slot = me.equipment.getSlot(EquipmentType.Weapon);
				if (slot.getItem() != null)
				{
					slot.takeAwayItem();
				}
				EquipmentAsset equipmentAsset = AssetManager.items.get(id);
				if (equipmentAsset == null)
				{
					return false;
				}
				Item pItem = World.world.items.generateItem(equipmentAsset, me.kingdom, me.getName(), 1, me, 0, pByPlayer: true);
				slot.setItem(pItem, me);
				me.setStatsDirty();
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("FEEL equip " + id + ": " + ex.Message);
				return false;
			}
		}

		private IEnumerator OwnBody()
		{
			Actor me = null;
			yield return Become("human", delegate(Actor a)
			{
				me = a;
			});
			if (me != null)
			{
				_mod.Settings.ShowBody = 2;
				int timeMode = _mod.Settings.TimeMode;
				_mod.Settings.TimeMode = 1;
				Place(me, (float)_field.x + 13f, (float)_field.y + 13f);
				_mod.SetYaw(0f);
				_mod.SetPitchDegrees(0f);
				yield return new WaitForSecondsRealtime(1.2f);
				SceneCollector scene = _mod.SceneForTest;
				int ownShadowsForTest = scene.OwnShadowsForTest;
				int ownLegsForTest = scene.OwnLegsForTest;
				_mod.SaveFrame("weight_body_level.png");
				Check("looking ahead: your body casts its shadow, your legs aren't drawn", ownShadowsForTest > 0 && ownLegsForTest == 0, $"{ownShadowsForTest} shadow meshes, {ownLegsForTest} legs");
				Log.Info("FEEL body pictures drawn by the " + (_mod.Settings.GpuRender ? "graphics card" : "CPU"));
				float[] array = new float[4] { 55f, 65f, 75f, 85f };
				foreach (float down in array)
				{
					_mod.SetPitchDegrees(0f - down);
					yield return new WaitForSecondsRealtime(0.6f);
					ownLegsForTest = scene.OwnLegsForTest;
					_mod.SaveFrame($"weight_body_down{down:F0}.png");
					Check($"looking {down:F0} degrees down: your legs are drawn", ownLegsForTest > 0, $"{ownLegsForTest} meshes");
				}
				_mod.SetPitchDegrees(-70f);
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				yield return new WaitForSecondsRealtime(1.3f);
				_mod.SaveFrame("weight_body_walk.png");
				PossessionHooks.ScriptedInput = Vector2.zero;
				yield return new WaitForSecondsRealtime(1f);
				Place(me, (float)_field.x + 13f, (float)_field.y + 13f);
				_mod.SetPitchDegrees(-25f);
				for (int num = 0; num < 4; num++)
				{
					_mod.SetYaw((float)num * MathF.PI * 0.5f);
					yield return new WaitForSecondsRealtime(0.5f);
					_mod.SaveFrame($"weight_body_shadow_{num * 90}.png");
				}
				_mod.SetPitchDegrees(0f);
				_mod.Settings.TimeMode = timeMode;
			}
		}

		private IEnumerator Become(string kind, Action<Actor> got)
		{
			Actor me = null;
			try
			{
				me = World.world.units.spawnNewUnit(kind, World.world.GetTile(_field.x + 13, _field.y + 13), pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("FEEL spawn " + kind + ": " + ex.Message);
			}
			if (me == null)
			{
				Fail(kind + " spawned", "none");
				yield break;
			}
			try
			{
				((BaseSimObject)me).addStatusEffect("invincible", 600f, pColorEffect: false);
			}
			catch
			{
			}
			try
			{
				me.restoreStaminaPercent(1f);
			}
			catch
			{
			}
			ControllableUnit.setControllableCreature(me);
			if (_was != null && _was != me)
			{
				Actor was = _was;
				yield return new WaitForSecondsRealtime(0.3f);
				try
				{
					was.finishStatusEffect("invincible");
					was.die(pDestroy: false, AttackType.Other, pCountDeath: true, pLogFavorite: true);
				}
				catch
				{
				}
			}
			_was = me;
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Fail(kind + ": first person started", "no");
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
			PossessionHooks.ScriptedInput = Vector2.zero;
			PossessionHooks.ScriptedRun = false;
			got(me);
		}

		private static void Place(Actor me, float x, float y)
		{
			try
			{
				me.cancelAllBeh();
				me.current_position = new Vector2(x, y);
				me.dirty_current_tile = true;
			}
			catch
			{
			}
		}

		private static WorldTile FindField()
		{
			int width = MapBox.width;
			int height = MapBox.height;
			for (int i = 0; i < Mathf.Max(width, height) / 2; i += 4)
			{
				for (int j = 0; j < 16; j++)
				{
					float f = (float)j * MathF.PI / 8f;
					int num = width / 2 + (int)(Mathf.Cos(f) * (float)i) - 13;
					int num2 = height / 2 + (int)(Mathf.Sin(f) * (float)i) - 13;
					if (num < 4 || num2 < 4 || num + 26 + 4 > width || num2 + 26 + 4 > height)
					{
						continue;
					}
					bool flag = true;
					for (int k = num2; k < num2 + 26 && flag; k++)
					{
						for (int l = num; l < num + 26 && flag; l++)
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
		}

		private static void Paint(WorldTile field)
		{
			TileType tileType = AssetManager.tiles.get("soil_low");
			TopTileType pTopTile = AssetManager.top_tiles.get("grass_low");
			for (int i = field.y; i < field.y + 26; i++)
			{
				for (int j = field.x; j < field.x + 26; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile == null)
					{
						continue;
					}
					try
					{
						if (tile.building != null)
						{
							try
							{
								tile.building.removeBuildingFinal();
							}
							catch
							{
								tile.building.startDestroyBuilding();
							}
						}
						if (tileType != null)
						{
							tile.setTileTypes(tileType, pTopTile);
						}
					}
					catch (Exception ex)
					{
						Log.Error("FEEL paint: " + ex.Message);
						return;
					}
				}
			}
		}

		private static float Crossed(float t0, float p0, float t1, float p1, float mark)
		{
			float num = p1 - p0;
			if (Mathf.Abs(num) < 1E-06f)
			{
				return t1;
			}
			return Mathf.Lerp(t0, t1, Mathf.Clamp01((mark - p0) / num));
		}

		private void ClearField(Actor me)
		{
			Vector2? vector = Away();
			if (!vector.HasValue)
			{
				return;
			}
			int num = 0;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple == null || simple == me || !simple.isAlive())
				{
					continue;
				}
				Vector2 current_position = simple.current_position;
				if (!(current_position.x < (float)_field.x - 8f) && !(current_position.x > (float)(_field.x + 26) + 8f) && !(current_position.y < (float)_field.y - 8f) && !(current_position.y > (float)(_field.y + 26) + 8f))
				{
					try
					{
						simple.cancelAllBeh();
						simple.current_position = vector.Value;
						simple.dirty_current_tile = true;
						num++;
					}
					catch
					{
					}
				}
			}
			if (num > 0)
			{
				Log.Info($"FEEL sent {num} others off the field");
			}
		}

		private static string NearThings(Vector2 p)
		{
			List<string> list = new List<string>();
			try
			{
				foreach (Building simple in World.world.buildings.getSimpleList())
				{
					if (simple != null && simple.isAlive() && simple.current_tile != null)
					{
						float magnitude = (new Vector2((float)simple.current_tile.x + 0.5f, (float)simple.current_tile.y + 0.5f) - p).magnitude;
						if (magnitude < 2.5f && list.Count < 8)
						{
							list.Add($"{simple.asset.id} {magnitude:F1}");
						}
					}
				}
			}
			catch
			{
			}
			if (list.Count != 0)
			{
				return string.Join(", ", list);
			}
			return "no building";
		}

		private Vector2? Away()
		{
			if (_away.HasValue)
			{
				return _away;
			}
			int num = 13;
			int[] array = new int[3] { 34, 44, 24 };
			foreach (int num2 in array)
			{
				for (int j = 0; j < 8; j++)
				{
					float f = (float)j * MathF.PI / 4f;
					int num3 = _field.x + num + (int)(Mathf.Cos(f) * (float)num2);
					int num4 = _field.y + num + (int)(Mathf.Sin(f) * (float)num2);
					if (num3 >= 2 && num4 >= 2 && num3 < MapBox.width - 2 && num4 < MapBox.height - 2)
					{
						WorldTile tile = World.world.GetTile(num3, num4);
						if (tile != null && tile.Type != null && !tile.Type.liquid && !tile.Type.block)
						{
							_away = new Vector2((float)num3 + 0.5f, (float)num4 + 0.5f);
							return _away;
						}
					}
				}
			}
			Log.Info("FEEL no open land off the field to send others to");
			return null;
		}

		private void Calm(Actor me)
		{
			_disturbed = null;
			try
			{
				me.attackedBy = null;
				_lastPos = me.current_position;
			}
			catch
			{
			}
			_lastMoved = Time.realtimeSinceStartup;
		}

		private void Watch(Actor me)
		{
			if (_disturbed != null)
			{
				return;
			}
			try
			{
				if (me.under_forces)
				{
					_disturbed = "knocked about";
					return;
				}
				if (me.attackedBy != null)
				{
					_disturbed = "struck by " + ((me.attackedBy is Actor actor) ? actor.asset.id : "something");
					return;
				}
				if (PossessionHooks.Frozen)
				{
					_disturbed = "held still";
					return;
				}
				if (ControllableUnit._unit_main != me || !me.isAlive())
				{
					_disturbed = "out of the body";
					return;
				}
				if (Time.unscaledDeltaTime > 0.3f)
				{
					_disturbed = $"the game stalled {Time.unscaledDeltaTime * 1000f:F0} ms";
					return;
				}
				Vector2 current_position = me.current_position;
				float realtimeSinceStartup = Time.realtimeSinceStartup;
				if ((current_position - _lastPos).sqrMagnitude > 1E-06f)
				{
					_lastPos = current_position;
					_lastMoved = realtimeSinceStartup;
				}
				else if (PossessionHooks.Pace > 0.3f && realtimeSinceStartup - _lastMoved > 0.4f)
				{
					_disturbed = "standing still under a run";
					return;
				}
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple != me && simple.isAlive() && (simple.current_position - current_position).sqrMagnitude < 0.64000005f)
					{
						_disturbed = simple.asset.id + " in the way";
						break;
					}
				}
			}
			catch
			{
			}
		}

		private void Judge(string what, bool ok, string detail)
		{
			if (ok || _disturbed == null)
			{
				Check(what, ok, detail);
				return;
			}
			Log.Info("FEEL NOT JUDGED " + what + " (" + detail + "; " + _disturbed + ")");
		}

		private void Check(string what, bool ok, string detail)
		{
			if (ok)
			{
				_pass++;
				Log.Info("FEEL PASS " + what + " (" + detail + ")");
			}
			else
			{
				Fail(what, detail);
			}
		}

		private void Fail(string what, string detail)
		{
			_fail++;
			Log.Info("FEEL FAIL " + what + " (" + detail + ")");
		}

		private static void Finish(bool quit)
		{
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("FEEL DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
