using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal sealed class ClimbTest : MonoBehaviour
	{
		private sealed class Route
		{
			public WorldTile Deep;

			public Vector2 Start;

			public Vector2 Exit;

			public float Yaw;

			public float Run;
		}

		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private float _flatSpeed;

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			bool quit = File.ReadAllText(path).Contains("quit");
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
			Route route = FindRoute();
			if (route == null)
			{
				RaiseRange();
				yield return new WaitForSecondsRealtime(2f);
				route = FindRoute();
			}
			if (route == null)
			{
				Log.Error("CLIMB no mountain range to climb");
				Finish(quit);
				yield break;
			}
			Log.Info($"CLIMB range heart {route.Deep.x},{route.Deep.y} ({route.Deep.Type.id}); start {route.Start.x:F1},{route.Start.y:F1}, " + $"{route.Run:F1} tiles of rock to the heart, facing {route.Yaw * 57.29578f:F0} deg");
			Actor me = Spawn("human", Tile(route.Start));
			if (me == null)
			{
				Finish(quit);
				yield break;
			}
			me.current_position = route.Start;
			ControllableUnit.setControllableCreature(me);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("CLIMB first person did not start");
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
			Check("the block check and the tile step are taken over", Climbing.On);
			_mod.SetYaw(route.Yaw);
			_mod.SetPitchDegrees(8f);
			yield return new WaitForSecondsRealtime(0.8f);
			_mod.SaveFrame("climb_foot.png");
			yield return Run("up", Up(me, route));
			yield return Run("still", Still(me));
			yield return Run("jump", Jump(me, route));
			yield return Run("views", Views(me, route));
			yield return Run("down", Down(me, route));
			yield return Run("wall", Wall(me, route));
			yield return Run("left", Left(me, route));
			yield return Run("others", Others(route));
			Finish(quit);
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("CLIMB --- " + name);
			while (true)
			{
				object current;
				try
				{
					if (!part.MoveNext())
					{
						break;
					}
					current = part.Current;
					goto IL_0074;
				}
				catch (Exception ex)
				{
					Log.Error("CLIMB " + name + " failed: " + ex);
				}
				break;
				IL_0074:
				yield return current;
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			PossessionHooks.ScriptedRun = false;
			PossessionHooks.ScriptedJump = false;
		}

		private IEnumerator Up(Actor me, Route route)
		{
			TerrainCache land = _mod.TerrainForTest;
			Vector2 heart = Centre(route.Deep);
			Vector2 current_position = me.current_position;
			float h0 = land.SampleHeight(current_position.x, current_position.y);
			float hMax = h0;
			float hop = 0f;
			float health0 = me.getHealth();
			float healthMin = health0;
			bool stunned = false;
			HashSet<WorldTile> rock = new HashSet<WorldTile>();
			float flatGo = 0f;
			float flatT = 0f;
			float steepGo = 0f;
			float steepT = 0f;
			float steepGrade = 0f;
			float paceSteep = 0f;
			Vector2 last = current_position;
			_mod.SetYaw(route.Yaw + MathF.PI);
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			for (float t = 0f; t < 2.5f; t += Time.unscaledDeltaTime)
			{
				yield return null;
				Vector2 current_position2 = me.current_position;
				float magnitude = (current_position2 - last).magnitude;
				float unscaledDeltaTime = Time.unscaledDeltaTime;
				last = current_position2;
				if (t > 0.8f && magnitude < 1f && PossessionHooks.ClimbForTest > 0.999f && !Climbing.IsRock(Toolbox.getTileAt(current_position2.x, current_position2.y)))
				{
					flatGo += magnitude;
					flatT += unscaledDeltaTime;
				}
			}
			current_position = me.current_position;
			h0 = Mathf.Min(h0, land.SampleHeight(current_position.x, current_position.y));
			float stall = 0f;
			float bestToHeart = (heart - current_position).magnitude;
			bool midShot = false;
			float clear = 99f;
			for (float t = 0f; t < 25f; t += Time.unscaledDeltaTime)
			{
				Vector2 current_position3 = me.current_position;
				Vector2 to = heart - current_position3;
				if (to.magnitude < 1.2f)
				{
					break;
				}
				_mod.SetYaw(Mathf.Atan2(to.y, to.x));
				yield return null;
				if (!me.isAlive())
				{
					Log.Error("CLIMB you died on the way up");
					yield break;
				}
				current_position3 = me.current_position;
				float unscaledDeltaTime2 = Time.unscaledDeltaTime;
				WorldTile tileAt = Toolbox.getTileAt(current_position3.x, current_position3.y);
				if (Climbing.IsRock(tileAt))
				{
					rock.Add(tileAt);
				}
				hMax = Mathf.Max(hMax, land.SampleHeight(current_position3.x, current_position3.y));
				hop = Mathf.Max(hop, me.position_height);
				healthMin = Mathf.Min(healthMin, me.getHealth());
				try
				{
					stunned |= me.hasStatusStunned();
				}
				catch
				{
				}
				clear = Mathf.Min(clear, Clearance(land));
				float magnitude2 = (current_position3 - last).magnitude;
				last = current_position3;
				float num = Vector2.Dot(Climbing.SlopeForTest, to.normalized);
				if (magnitude2 < 1f && unscaledDeltaTime2 > 0f && t > 0.5f && num > 1.5f)
				{
					steepGo += magnitude2;
					steepT += unscaledDeltaTime2;
					steepGrade += num * unscaledDeltaTime2;
					paceSteep += PossessionHooks.ClimbForTest * unscaledDeltaTime2;
				}
				if (rock.Count == 3 && !midShot)
				{
					midShot = true;
					_mod.SaveFrame("climb_mid.png");
				}
				float magnitude3 = (heart - current_position3).magnitude;
				if (magnitude3 < bestToHeart - 0.05f)
				{
					bestToHeart = magnitude3;
					stall = 0f;
					continue;
				}
				float num2;
				stall = (num2 = stall + unscaledDeltaTime2);
				if (num2 > 3f)
				{
					Log.Info($"CLIMB no nearer the heart for 3 s, {magnitude3:F1} tiles off");
					break;
				}
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			WorldTile tileAt2 = Toolbox.getTileAt(me.current_position.x, me.current_position.y);
			_flatSpeed = ((flatT > 0f) ? (flatGo / flatT) : 0f);
			float num3 = ((steepT > 0f) ? (steepGo / steepT) : 0f);
			Log.Info(string.Format("CLIMB up: {0} rock tiles walked, climbed {1:F1} tiles, now on {2} ", rock.Count, hMax - h0, (tileAt2 != null) ? tileAt2.Type.id : "?") + $"{(heart - me.current_position).magnitude:F1} tiles from the heart; flat {_flatSpeed:F2} t/s ({flatT:F1} s), " + $"steep {num3:F2} t/s ({steepT:F1} s at grade {((steepT > 0f) ? (steepGrade / steepT) : 0f):F2}, pace share {((steepT > 0f) ? (paceSteep / steepT) : 0f):F2})");
			Check($"walked onto the mountain and on up it ({rock.Count} rock tiles, at least 3)", rock.Count >= 3);
			Check($"climbed at least 2 tiles ({hMax - h0:F1})", hMax - h0 >= 2f);
			Check($"never thrown up on the rock (highest off the ground {hop:F2})", hop < 0.1f);
			Check($"no harm from the rock (health {health0:F0} -> lowest {healthMin:F0})", healthMin >= health0);
			Check("never stunned", !stunned);
			Check("still on the mountain at the end (" + ((tileAt2 != null) ? tileAt2.Type.id : "?") + ")", Climbing.IsRock(tileAt2));
			Check($"the eye never goes into the rock face (closest {clear:F2} over the land a step round it)", clear > 0.05f);
			if (steepT > 0.5f && flatT > 0.5f)
			{
				Check($"slower up the steep part: {num3:F2} against {_flatSpeed:F2} on the flat ({num3 / _flatSpeed:F2}, 0.3 to 0.85)", num3 / _flatSpeed > 0.3f && num3 / _flatSpeed < 0.85f);
			}
			else
			{
				Log.Info("CLIMB not enough flat and steep walking to compare the pace");
			}
		}

		private IEnumerator Still(Actor me)
		{
			Vector2 p0 = me.current_position;
			float hop = 0f;
			float health0 = me.getHealth();
			float healthMin = health0;
			float moved = 0f;
			for (float t = 0f; t < 4f; t += Time.unscaledDeltaTime)
			{
				yield return null;
				if (!me.isAlive())
				{
					yield break;
				}
				hop = Mathf.Max(hop, me.position_height);
				healthMin = Mathf.Min(healthMin, me.getHealth());
				moved = Mathf.Max(moved, (me.current_position - p0).magnitude);
			}
			WorldTile tileAt = Toolbox.getTileAt(me.current_position.x, me.current_position.y);
			Check(string.Format("standing still up there for 4 s you stay put (moved {0:F2}, highest {1:F2}, health {2:F0} -> {3:F0}, on {4})", moved, hop, health0, healthMin, (tileAt != null) ? tileAt.Type.id : "?"), moved < 0.15f && hop < 0.1f && healthMin >= health0 && Climbing.IsRock(tileAt));
		}

		private IEnumerator Jump(Actor me, Route route)
		{
			Vector2 vector = Centre(route.Deep);
			Vector2 current_position = me.current_position;
			Vector2 dir = (((vector - current_position).sqrMagnitude > 1f) ? (vector - current_position).normalized : new Vector2(Mathf.Cos(route.Yaw), Mathf.Sin(route.Yaw)));
			_mod.SetYaw(Mathf.Atan2(dir.y, dir.x));
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			yield return new WaitForSecondsRealtime(0.4f);
			Vector2 before = me.current_position;
			PossessionHooks.ScriptedJump = true;
			float peak = 0f;
			float ahead = 0f;
			float back = 0f;
			bool flew = false;
			for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
			{
				yield return null;
				if (!me.isAlive())
				{
					yield break;
				}
				peak = Mathf.Max(peak, me.position_height);
				flew |= me.position_height > 0.05f;
				float num = Vector2.Dot(me.current_position - before, dir);
				ahead = Mathf.Max(ahead, num);
				back = Mathf.Max(back, ahead - num);
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			WorldTile tileAt = Toolbox.getTileAt(me.current_position.x, me.current_position.y);
			Log.Info(string.Format("CLIMB jump up the slope: peak {0:F2}, went {1:F2} on, fell back {2:F2}, landed on {3}", peak, ahead, back, (tileAt != null) ? tileAt.Type.id : "?"));
			Check($"a jump up the slope leaves the ground ({peak:F2})", flew);
			Check($"and goes on up it, not back off the face (on {ahead:F2}, back {back:F2})", ahead > 0.3f && back < 0.15f);
			Check("landing on the rock, you stay (" + ((tileAt != null) ? tileAt.Type.id : "?") + ")", Climbing.IsRock(tileAt) || !PossessionHooks.IsBlock(tileAt));
		}

		private IEnumerator Views(Actor me, Route route)
		{
			Vector2 p = me.current_position;
			Vector2 vector = Centre(route.Deep) - p;
			float yawUp = ((vector.sqrMagnitude > 0.01f) ? Mathf.Atan2(vector.y, vector.x) : route.Yaw);
			_mod.SetYaw(yawUp);
			_mod.SetPitchDegrees(10f);
			yield return new WaitForSecondsRealtime(0.6f);
			_mod.SaveFrame("climb_up.png");
			_mod.SetYaw(yawUp + MathF.PI);
			_mod.SetPitchDegrees(-18f);
			yield return new WaitForSecondsRealtime(0.6f);
			_mod.SaveFrame("climb_down.png");
			_mod.SetYaw(yawUp + MathF.PI / 2f);
			_mod.SetPitchDegrees(0f);
			yield return new WaitForSecondsRealtime(0.6f);
			_mod.SaveFrame("climb_side.png");
			Log.Info($"CLIMB views from {p.x:F1},{p.y:F1}, eye {_mod.View.Z - _mod.TerrainForTest.SampleHeight(p.x, p.y):F2} over the ground");
		}

		private IEnumerator Down(Actor me, Route route)
		{
			_ = _mod.TerrainForTest;
			float go = 0f;
			float time = 0f;
			float grade = 0f;
			float hop = 0f;
			Vector2 last = me.current_position;
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			for (float t = 0f; t < 15f; t += Time.unscaledDeltaTime)
			{
				Vector2 to = route.Start - me.current_position;
				if (to.magnitude < 0.8f)
				{
					break;
				}
				_mod.SetYaw(Mathf.Atan2(to.y, to.x));
				yield return null;
				if (!me.isAlive())
				{
					yield break;
				}
				Vector2 current_position = me.current_position;
				float num = Vector2.Dot(Climbing.SlopeForTest, to.normalized);
				float magnitude = (current_position - last).magnitude;
				last = current_position;
				hop = Mathf.Max(hop, me.position_height);
				if (num < -1.5f && magnitude < 1f)
				{
					go += magnitude;
					time += Time.unscaledDeltaTime;
					grade += num * Time.unscaledDeltaTime;
				}
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			float num2 = ((time > 0f) ? (go / time) : 0f);
			Log.Info($"CLIMB down: {num2:F2} t/s on the steep part ({time:F1} s at grade {((time > 0f) ? (grade / time) : 0f):F2}) against {_flatSpeed:F2} on the flat; " + $"now {(route.Start - me.current_position).magnitude:F1} tiles from the start");
			Check($"never thrown going down (highest {hop:F2})", hop < 0.1f);
			Check($"back down at the foot ({(route.Start - me.current_position).magnitude:F1} tiles off)", (route.Start - me.current_position).magnitude < 2f);
		}

		private IEnumerator Wall(Actor me, Route route)
		{
			TopTileType topTileType = AssetManager.top_tiles.get("wall_order");
			if (topTileType == null)
			{
				Log.Info("CLIMB no wall tile type, skipped");
				yield break;
			}
			Vector2 p = me.current_position;
			Vector2 dir = -new Vector2(Mathf.Cos(route.Yaw), Mathf.Sin(route.Yaw));
			Vector2 vector = new Vector2(0f - dir.y, dir.x);
			List<WorldTile> placed = new List<WorldTile>();
			for (int i = -3; i <= 3; i++)
			{
				WorldTile worldTile = Tile(p + dir * 2.5f + vector * i);
				if (worldTile != null && worldTile.main_type != null && !worldTile.main_type.liquid && !worldTile.main_type.block && !placed.Contains(worldTile))
				{
					try
					{
						worldTile.setTopTileType(topTileType);
						placed.Add(worldTile);
					}
					catch
					{
					}
				}
			}
			if (placed.Count == 0)
			{
				Log.Info("CLIMB no ground for a wall, skipped");
				yield break;
			}
			yield return new WaitForSecondsRealtime(0.5f);
			_mod.SetYaw(Mathf.Atan2(dir.y, dir.x));
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			bool onWall = false;
			float furthest = 0f;
			for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime)
			{
				yield return null;
				if (!me.isAlive())
				{
					yield break;
				}
				WorldTile tileAt = Toolbox.getTileAt(me.current_position.x, me.current_position.y);
				onWall |= tileAt != null && tileAt.Type != null && tileAt.Type.wall;
				furthest = Mathf.Max(furthest, Vector2.Dot(me.current_position - p, dir));
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			_mod.SaveFrame("climb_wall.png");
			Check(string.Format("a city wall still stops you ({0} wall tiles; went {1:F2} towards it, {2})", placed.Count, furthest, onWall ? "STOOD ON IT" : "never on it"), !onWall && furthest < 2.6f);
			foreach (WorldTile item in placed)
			{
				try
				{
					item.setTopTileType(AssetManager.top_tiles.get("grass_low"));
				}
				catch
				{
				}
			}
		}

		private IEnumerator Left(Actor me, Route route)
		{
			Vector2 vector = Centre(route.Deep);
			Vector2 normalized = (vector - route.Exit).normalized;
			Vector2 up = route.Exit + normalized * Mathf.Min(4f, route.Run * 0.6f);
			if (!Climbing.IsRock(Tile(up)))
			{
				up = vector;
			}
			me.current_position = up;
			me.dirty_current_tile = true;
			yield return new WaitForSecondsRealtime(1.5f);
			WorldTile tileAt = Toolbox.getTileAt(me.current_position.x, me.current_position.y);
			Check(string.Format("set down on the rock, you stay on it ({0}, {1:F2} from where set)", (tileAt != null) ? tileAt.Type.id : "?", (me.current_position - up).magnitude), Climbing.IsRock(tileAt) && (me.current_position - up).magnitude < 0.3f);
			Actor actor = Spawn("human", Tile(route.Start));
			if (actor == null)
			{
				yield break;
			}
			actor.current_position = route.Start;
			float health0 = me.getHealth();
			float healthMin = health0;
			float hop = 0f;
			float offAt = -1f;
			ControllableUnit.setControllableCreature(actor);
			for (float t = 0f; t < 3f; t += 0.1f)
			{
				if (_mod.IsFirstPerson)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.1f);
			}
			yield return null;
			Check("the body left up there is being walked down", Climbing.LeftForTest == me);
			double world0 = World.world.getCurWorldTime();
			for (float t = 0f; t < 30f; t += Time.unscaledDeltaTime)
			{
				yield return null;
				if (!me.isAlive())
				{
					Log.Info("CLIMB the body you left died");
					break;
				}
				hop = Mathf.Max(hop, me.position_height);
				healthMin = Mathf.Min(healthMin, me.getHealth());
				if (!Climbing.IsRock(Toolbox.getTileAt(me.current_position.x, me.current_position.y)))
				{
					offAt = t;
					break;
				}
				if (World.world.getCurWorldTime() - world0 > 29.0)
				{
					break;
				}
			}
			string text = "none";
			try
			{
				text = ((((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)me.ai)?.task != null) ? ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)me.ai).task.id : "none");
			}
			catch
			{
			}
			Log.Info(string.Format("CLIMB the body you left: {0}, highest {1:F2}, health {2:F0} -> {3:F0}, task {4}", (offAt >= 0f) ? $"off the rock after {offAt:F1} s" : "still on the rock", hop, health0, healthMin, text));
			Check($"the body you left isn't thrown ({hop:F2})", hop < 0.3f);
			Check($"nor hurt ({health0:F0} -> {healthMin:F0})", healthMin >= health0);
			Check("and walks down off the rock by itself", offAt >= 0f);
		}

		private IEnumerator Others(Route route)
		{
			Vector2 heart = Centre(route.Deep);
			Vector2 normalized = (heart - route.Exit).normalized;
			Vector2 vector = route.Exit + normalized * Mathf.Min(2.5f, route.Run * 0.5f);
			if (!Climbing.IsRock(Tile(vector)))
			{
				vector = heart;
			}
			Actor sheep = Spawn("sheep", Tile(vector));
			if (sheep == null)
			{
				yield break;
			}
			sheep.current_position = vector;
			float hop = 0f;
			bool off = false;
			for (float t = 0f; t < 4f; t += Time.unscaledDeltaTime)
			{
				yield return null;
				if (!sheep.isAlive())
				{
					break;
				}
				hop = Mathf.Max(hop, sheep.position_height);
				off |= !Climbing.IsRock(Toolbox.getTileAt(sheep.current_position.x, sheep.current_position.y));
			}
			Check(string.Format("a sheep set on the rock still gets the game's way (thrown {0:F2} high, {1})", hop, off ? "got off" : "still on"), hop > 0.3f || off);
			Actor knocked = Spawn("sheep", Tile(route.Start));
			if (knocked == null)
			{
				yield break;
			}
			knocked.current_position = route.Start;
			yield return new WaitForSecondsRealtime(0.3f);
			Vector2 from = knocked.current_position;
			knocked.calculateForce(heart.x, heart.y, from.x, from.y, 2.5f, 0.6f, pCheckCancelJobOnLand: true);
			bool onRock = false;
			bool flew = false;
			float nearest = (heart - from).magnitude;
			for (float t = 0f; t < 2.5f; t += Time.unscaledDeltaTime)
			{
				yield return null;
				if (!knocked.isAlive())
				{
					break;
				}
				flew |= knocked.position_height > 0.05f;
				onRock |= Climbing.IsRock(Toolbox.getTileAt(knocked.current_position.x, knocked.current_position.y));
				nearest = Mathf.Min(nearest, (heart - knocked.current_position).magnitude);
			}
			Log.Info(string.Format("CLIMB a sheep knocked at the mountain: {0}, came {1:F2} tiles nearer, {2}", flew ? "flew" : "never left the ground", (heart - from).magnitude - nearest, onRock ? "LANDED ON THE ROCK" : "never on the rock"));
			Check("a sheep knocked at the mountain bounces off it, as in the game", flew && !onRock);
		}

		private static Route FindRoute()
		{
			int width = MapBox.width;
			int height = MapBox.height;
			int[] array = new int[width * height];
			List<int> list = new List<int>();
			Queue<int> queue = new Queue<int>();
			int num = 0;
			for (int i = 0; i < width * height; i++)
			{
				if (array[i] != 0 || !Climbing.IsRock(World.world.GetTile(i % width, i / width)))
				{
					continue;
				}
				List<int> list2 = new List<int>();
				num = (array[i] = num + 1);
				queue.Enqueue(i);
				while (queue.Count > 0)
				{
					int num2 = queue.Dequeue();
					list2.Add(num2);
					int num3 = num2 % width;
					int num4 = num2 / width;
					for (int j = -1; j <= 1; j++)
					{
						for (int k = -1; k <= 1; k++)
						{
							int num5 = num3 + k;
							int num6 = num4 + j;
							if (num5 >= 0 && num6 >= 0 && num5 < width && num6 < height)
							{
								int num7 = num6 * width + num5;
								if (array[num7] == 0 && Climbing.IsRock(World.world.GetTile(num5, num6)))
								{
									array[num7] = num;
									queue.Enqueue(num7);
								}
							}
						}
					}
				}
				if (list2.Count > list.Count)
				{
					list = list2;
				}
			}
			Log.Info($"CLIMB {num} mountain knots, the biggest {list.Count} tiles");
			if (list.Count < 25)
			{
				return null;
			}
			Dictionary<int, int> dictionary = new Dictionary<int, int>();
			foreach (int item in list)
			{
				int num8 = item % width;
				int num9 = item / width;
				bool flag = false;
				for (int l = 0; l < 4; l++)
				{
					if (flag)
					{
						break;
					}
					int num10;
					switch (l)
					{
					default:
						num10 = 0;
						break;
					case 1:
						num10 = -1;
						break;
					case 0:
						num10 = 1;
						break;
					}
					int num11 = num8 + num10;
					int num12;
					switch (l)
					{
					default:
						num12 = 0;
						break;
					case 3:
						num12 = -1;
						break;
					case 2:
						num12 = 1;
						break;
					}
					int num13 = num9 + num12;
					flag = num11 < 0 || num13 < 0 || num11 >= width || num13 >= height || !Climbing.IsRock(World.world.GetTile(num11, num13));
				}
				if (flag)
				{
					dictionary[item] = 1;
					queue.Enqueue(item);
				}
			}
			while (queue.Count > 0)
			{
				int num14 = queue.Dequeue();
				int num15 = num14 % width;
				int num16 = num14 / width;
				for (int m = 0; m < 4; m++)
				{
					int num17;
					switch (m)
					{
					default:
						num17 = 0;
						break;
					case 1:
						num17 = -1;
						break;
					case 0:
						num17 = 1;
						break;
					}
					int num18 = num15 + num17;
					int num19;
					switch (m)
					{
					default:
						num19 = 0;
						break;
					case 3:
						num19 = -1;
						break;
					case 2:
						num19 = 1;
						break;
					}
					int num20 = num16 + num19;
					if (num18 >= 0 && num20 >= 0 && num18 < width && num20 < height)
					{
						int num21 = num20 * width + num18;
						if (array[num21] == array[num14] && !dictionary.ContainsKey(num21))
						{
							dictionary[num21] = dictionary[num14] + 1;
							queue.Enqueue(num21);
						}
					}
				}
			}
			int num22 = list[0];
			foreach (KeyValuePair<int, int> item2 in dictionary)
			{
				if (item2.Value > dictionary[num22])
				{
					num22 = item2.Key;
				}
			}
			WorldTile tile = World.world.GetTile(num22 % width, num22 / width);
			Vector2 vector = Centre(tile);
			Route route = null;
			for (int n = 0; n < 64; n++)
			{
				float f = (float)n * MathF.PI / 32f;
				Vector2 vector2 = new Vector2(Mathf.Cos(f), Mathf.Sin(f));
				float num23;
				for (num23 = 0f; num23 < 40f && Climbing.IsRock(Tile(vector + vector2 * num23)); num23 += 0.25f)
				{
				}
				if (!(num23 >= 40f) && !(num23 < 4f))
				{
					Vector2 vector3 = vector + vector2 * num23;
					Vector2 vector4 = vector + vector2 * (num23 + 1.6f);
					if (Open(Tile(vector3)) && Open(Tile(vector4)) && Open(Tile(vector + vector2 * (num23 + 0.8f))) && (route == null || Mathf.Min(num23, 16f) > Mathf.Min(route.Run, 16f) || (Mathf.Min(num23, 16f) >= 16f && num23 < route.Run)))
					{
						route = new Route
						{
							Deep = tile,
							Start = vector4,
							Exit = vector3,
							Yaw = Mathf.Atan2(0f - vector2.y, 0f - vector2.x),
							Run = num23
						};
					}
				}
			}
			return route;
		}

		private static bool Open(WorldTile t)
		{
			try
			{
				return t != null && t.Type != null && !t.Type.liquid && !t.Type.block && !t.Type.lava && (t.building == null || !AssetStyle.IsSolid(t.building));
			}
			catch
			{
				return false;
			}
		}

		private static void RaiseRange()
		{
			TileType tileType = AssetManager.tiles.get("mountains");
			TileType tileType2 = AssetManager.tiles.get("summit");
			if (tileType == null)
			{
				return;
			}
			int width = MapBox.width;
			int height = MapBox.height;
			for (int i = 0; i < width / 2; i += 6)
			{
				for (int j = 0; j < 16; j++)
				{
					float f = (float)j * MathF.PI / 8f;
					int num = width / 2 + (int)(Mathf.Cos(f) * (float)i);
					int num2 = height / 2 + (int)(Mathf.Sin(f) * (float)i);
					if (num < 16 || num2 < 16 || num > width - 10 - 6 || num2 > height - 10 - 6)
					{
						continue;
					}
					bool flag = true;
					for (int k = num2 - 10 - 4; k <= num2 + 10 + 4 && flag; k++)
					{
						for (int l = num - 10 - 4; l <= num + 10 + 4 && flag; l++)
						{
							WorldTile tile = World.world.GetTile(l, k);
							flag = tile != null && tile.Type != null && !tile.Type.liquid && !tile.Type.lava && tile.zone != null && tile.zone.city == null;
						}
					}
					if (!flag)
					{
						continue;
					}
					for (int m = num2 - 10; m <= num2 + 10; m++)
					{
						for (int n = num - 10; n <= num + 10; n++)
						{
							float num3 = Mathf.Sqrt((n - num) * (n - num) + (m - num2) * (m - num2));
							if (num3 > 10f)
							{
								continue;
							}
							WorldTile tile2 = World.world.GetTile(n, m);
							try
							{
								if (tile2.building != null)
								{
									tile2.building.startDestroyBuilding();
								}
								tile2.setTileType((num3 < 4f && tileType2 != null) ? tileType2 : tileType);
							}
							catch
							{
							}
						}
					}
					Log.Info($"CLIMB no mountains: raised a range of radius {10} at {num},{num2}");
					return;
				}
			}
		}

		private float Clearance(TerrainCache land)
		{
			ViewState view = _mod.View;
			float num = 99f;
			for (int i = 0; i < 8; i++)
			{
				float f = (float)i * MathF.PI / 4f;
				num = Mathf.Min(num, view.Z - land.SampleHeight(view.X + Mathf.Cos(f) * 0.15f, view.Y + Mathf.Sin(f) * 0.15f));
			}
			return num;
		}

		private static WorldTile Tile(Vector2 p)
		{
			try
			{
				return World.world.GetTile(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
			}
			catch
			{
				return null;
			}
		}

		private static Vector2 Centre(WorldTile t)
		{
			return new Vector2((float)t.x + 0.5f, (float)t.y + 0.5f);
		}

		private static Actor Spawn(string id, WorldTile t)
		{
			if (t == null)
			{
				return null;
			}
			try
			{
				return World.world.units.spawnNewUnit(id, t, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("CLIMB spawn " + id + ": " + ex.Message);
				return null;
			}
		}

		private void Check(string what, bool ok)
		{
			if (ok)
			{
				_pass++;
			}
			else
			{
				_fail++;
			}
			Log.Info("CLIMB " + (ok ? "PASS" : "FAIL") + " " + what);
		}

		private void Finish(bool quit)
		{
			PossessionHooks.ScriptedInput = Vector2.zero;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info($"CLIMB DONE {_pass} passed, {_fail} failed");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
