using System;
using System.Collections.Generic;
using System.Text;
using FirstPerson.Net;
using UnityEngine;

namespace FirstPerson
{
	internal static class SimLod
	{
		private sealed class State
		{
			public int Step;

			public int Band;

			public bool Run;

			public float Owed;

			public double LastRun;

			public int HungerOwed;

			public int Phase;
		}

		internal static bool Budgeted = true;

		internal static bool Off;

		internal const float NearTiles = 80f;

		internal const float Band2Tiles = 200f;

		internal const float Band3Tiles = 400f;

		internal const float SeenPixels = 8f;

		internal const float SpeckPixels = 3f;

		private const float OwedCap = 1f;

		private static readonly string[] JobIds = new string[6] { "update_hunger", "u5_curTileAction", "b3_findEnemyTarget", "b6_0_update_decision", "b6_update_ai", "u10_checkSmoothMovement" };

		private static State[] _byHash = new State[65536];

		private static int _hashTop;

		private const int HashCap = 4194304;

		private static readonly Dictionary<Actor, State> _states = new Dictionary<Actor, State>(256);

		private static int _known;

		private static readonly HashSet<BatchActors> _patched = new HashSet<BatchActors>();

		private static readonly HashSet<string> _missing = new HashSet<string>();

		private static readonly List<Actor> _deferred = new List<Actor>(256);

		private static JobManagerActors _manager;

		private static int _batchCount = -1;

		private static int _step;

		private static double _worldTime;

		private static readonly Dictionary<BatchActors, int> _lastJob = new Dictionary<BatchActors, int>();

		private static readonly List<BatchActors> _batchKeys = new List<BatchActors>();

		private static float _elapsedNow;

		private static float _pruneAt;

		private static float _logAt;

		private static readonly long[] _ran = new long[5];

		private static long _skipped;

		private static long _steps;

		private static int _jobsPatched;

		private static readonly Dictionary<string, double> _jobTimesAt = new Dictionary<string, double>();

		private static readonly Dictionary<string, double> _jobTimesNow = new Dictionary<string, double>();

		internal static bool Active { get; private set; }

		internal static void BeginFrame(WorldBoxMod mod)
		{
			bool flag = false;
			try
			{
				MapBox world = World.world;
				flag = Budgeted && !Off && mod != null && world != null && Config.game_loaded && !WorldSync.Mirroring && (mod.IsFirstPerson || GodView.Active) && SceneCollector.SimView != null;
				if (flag)
				{
					Patch(world);
				}
				if (flag && Time.realtimeSinceStartup >= _logAt)
				{
					LogRates();
				}
				if (flag && Time.realtimeSinceStartup >= _pruneAt)
				{
					Prune();
				}
			}
			catch (Exception ex)
			{
				flag = false;
				Log.Error("far creatures: " + ex);
			}
			Active = flag && _jobsPatched > 0;
		}

		private static void Patch(MapBox world)
		{
			JobManagerActors jobManagerActors = world.units?._job_manager;
			if (jobManagerActors == null)
			{
				return;
			}
			List<BatchActors> batches_active = ((JobManagerBase<BatchActors, Actor>)jobManagerActors)._batches_active;
			if (jobManagerActors == _manager && batches_active.Count == _batchCount)
			{
				return;
			}
			if (jobManagerActors != _manager)
			{
				_patched.Clear();
				_lastJob.Clear();
				_batchKeys.Clear();
				_jobsPatched = 0;
			}
			_manager = jobManagerActors;
			_batchCount = batches_active.Count;
			for (int i = 0; i < batches_active.Count; i++)
			{
				BatchActors batchActors = batches_active[i];
				if (batchActors == null || !_patched.Add(batchActors))
				{
					continue;
				}
				HashSet<string> hashSet = new HashSet<string>();
				List<Job<Actor>> jobs_post = ((Batch<Actor>)batchActors).jobs_post;
				for (int j = 0; j < jobs_post.Count; j++)
				{
					Job<Actor> job = jobs_post[j];
					JobUpdater job_updater = job.job_updater;
					if (job != null && job_updater != null && job.id != null)
					{
						JobUpdater jobUpdater = Replacement(batchActors, job.id, job_updater);
						if (jobUpdater != null)
						{
							job.job_updater = jobUpdater;
							hashSet.Add(job.id);
							_jobsPatched++;
						}
					}
				}
				for (int k = 0; k < JobIds.Length; k++)
				{
					if (!hashSet.Contains(JobIds[k]) && _missing.Add(JobIds[k]))
					{
						Log.Info("far creatures: this game has no job '" + JobIds[k] + "' in its actor batches: left as it is");
					}
				}
			}
		}

		private static JobUpdater Replacement(BatchActors b, string id, JobUpdater orig)
		{
			switch (id)
			{
			case "update_hunger":
				return delegate
				{
					NewStep(b, 0);
					if (Active)
					{
						Hunger(b);
					}
					else
					{
						orig();
					}
				};
			case "u5_curTileAction":
				return delegate
				{
					NewStep(b, 1);
					if (Active)
					{
						TileAction(b);
					}
					else
					{
						orig();
					}
				};
			case "b3_findEnemyTarget":
				return delegate
				{
					NewStep(b, 2);
					if (Active)
					{
						FindEnemy(b);
					}
					else
					{
						orig();
					}
				};
			case "b6_0_update_decision":
				return delegate
				{
					NewStep(b, 3);
					if (Active)
					{
						Decide(b);
					}
					else
					{
						orig();
					}
				};
			case "b6_update_ai":
				return delegate
				{
					NewStep(b, 4);
					if (Active)
					{
						UpdateAi(b);
					}
					else
					{
						orig();
					}
				};
			case "u10_checkSmoothMovement":
				return delegate
				{
					NewStep(b, 5);
					if (Active)
					{
						Movement(b);
					}
					else
					{
						orig();
					}
				};
			default:
				return null;
			}
		}

		private static void Hunger(BatchActors b)
		{
			MapBox world = World.world;
			if (world.timer_nutrition_decay > 0f || world.isPaused() || !((Batch<Actor>)b).check(((Batch<Actor>)b)._cur_container))
			{
				return;
			}
			bool pDoStarvationDamage = WorldLawLibrary.world_law_hunger.isEnabled();
			Actor[] array = ((Batch<Actor>)b)._array;
			int count = ((Batch<Actor>)b)._count;
			for (int i = 0; i < count; i++)
			{
				Actor actor = array[i];
				State state = Classify(actor);
				if (!state.Run)
				{
					state.HungerOwed++;
					continue;
				}
				int num = state.HungerOwed + 1;
				state.HungerOwed = 0;
				for (int j = 0; j < num && (j <= 0 || actor.isAlive()); j++)
				{
					if (actor.needsFood() && !actor.isEgg() && (!actor.hasSubspecies() || !actor.subspecies.has_trait_energy_preserver || !((BaseSimObject)actor).hasStatus("sleeping")))
					{
						actor.updateNutritionDecay(pDoStarvationDamage);
					}
				}
			}
		}

		private static void TileAction(BatchActors b)
		{
			if (!((Batch<Actor>)b).check(((Batch<Actor>)b)._cur_container) || World.world.isPaused())
			{
				return;
			}
			bool installed = DryBoards.Installed;
			bool docks = installed && DryBoards.DocksNow;
			if (installed)
			{
				Climbing.Watch();
			}
			Actor[] array = ((Batch<Actor>)b)._array;
			int count = ((Batch<Actor>)b)._count;
			for (int i = 0; i < count; i++)
			{
				Actor actor = array[i];
				if (Classify(actor).Run)
				{
					if (installed)
					{
						DryBoards.StepActor(actor, docks);
					}
					else
					{
						actor.u5_curTileAction();
					}
				}
			}
		}

		private static void FindEnemy(BatchActors b)
		{
			if (!((Batch<Actor>)b).check(((Batch<Actor>)b)._cur_container) || World.world.isPaused())
			{
				return;
			}
			Actor[] array = ((Batch<Actor>)b)._array;
			int count = ((Batch<Actor>)b)._count;
			for (int i = 0; i < count; i++)
			{
				Actor actor = array[i];
				State state = Classify(actor);
				if (state.Run)
				{
					actor.b3_findEnemyTarget(state.Owed);
				}
			}
		}

		private static void Decide(BatchActors b)
		{
			ObjectContainer<Actor> cur_container = ((Batch<Actor>)b)._cur_container;
			if (!((Batch<Actor>)b).check(cur_container) || World.world.isPaused())
			{
				return;
			}
			Actor[] array = ((Batch<Actor>)b)._array;
			int count = ((Batch<Actor>)b)._count;
			_deferred.Clear();
			for (int i = 0; i < count; i++)
			{
				Actor actor = array[i];
				State state = Classify(actor);
				if (state.Run)
				{
					actor.b6_0_updateDecision(state.Owed);
				}
				else
				{
					_deferred.Add(actor);
				}
			}
			cur_container.Clear();
			for (int j = 0; j < _deferred.Count; j++)
			{
				cur_container.Add(_deferred[j]);
			}
			_deferred.Clear();
		}

		private static void UpdateAi(BatchActors b)
		{
			if (!((Batch<Actor>)b).check(((Batch<Actor>)b)._cur_container) || World.world.isPaused())
			{
				return;
			}
			Actor[] array = ((Batch<Actor>)b)._array;
			int count = ((Batch<Actor>)b)._count;
			for (int i = 0; i < count; i++)
			{
				Actor actor = array[i];
				State state = Classify(actor);
				if (state.Run)
				{
					actor.b6_updateAI(state.Owed);
				}
			}
		}

		private static void Movement(BatchActors b)
		{
			if (!((Batch<Actor>)b).check(((Batch<Actor>)b)._cur_container) || World.world.isPaused())
			{
				return;
			}
			Actor[] array = ((Batch<Actor>)b)._array;
			int count = ((Batch<Actor>)b)._count;
			for (int i = 0; i < count; i++)
			{
				Actor actor = array[i];
				State state = Classify(actor);
				if (state.Run)
				{
					actor.u10_checkSmoothMovement(state.Owed);
				}
			}
		}

		private static void NewStep(BatchActors b, int ordinal)
		{
			if (World.world.isPaused())
			{
				return;
			}
			bool flag;
			if (_lastJob.TryGetValue(b, out var value))
			{
				flag = ordinal <= value;
			}
			else
			{
				_batchKeys.Add(b);
				flag = _batchKeys.Count == 1;
			}
			if (!flag)
			{
				_lastJob[b] = ordinal;
				return;
			}
			for (int i = 0; i < _batchKeys.Count; i++)
			{
				_lastJob[_batchKeys[i]] = -1;
			}
			_lastJob[b] = ordinal;
			_step++;
			_steps++;
			_elapsedNow = ((Batch<Actor>)b)._elapsed;
			_worldTime += ((Batch<Actor>)b)._elapsed;
		}

		private static State Classify(Actor a)
		{
			int hashCode = a.GetHashCode();
			State value;
			if (hashCode >= 0 && hashCode < 4194304)
			{
				if (hashCode >= _byHash.Length)
				{
					int num;
					for (num = _byHash.Length; num <= hashCode; num <<= 1)
					{
					}
					Array.Resize(ref _byHash, Math.Min(num, 4194304));
				}
				value = _byHash[hashCode];
				if (value == null)
				{
					State[] byHash = _byHash;
					State obj = new State
					{
						Phase = hashCode % 12,
						LastRun = _worldTime - (double)_elapsedNow
					};
					State state = obj;
					byHash[hashCode] = obj;
					value = state;
					if (hashCode > _hashTop)
					{
						_hashTop = hashCode;
					}
					_known++;
				}
			}
			else if (!_states.TryGetValue(a, out value))
			{
				value = new State
				{
					Phase = (hashCode & 0x7FFFFFFF) % 12,
					LastRun = _worldTime - (double)_elapsedNow
				};
				_states[a] = value;
				_known++;
			}
			if (value.Step == _step)
			{
				return value;
			}
			value.Step = _step;
			int num2 = (value.Band = BandOf(a));
			value.Run = num2 <= 1 || (_step + value.Phase) % num2 == 0;
			if (value.Run)
			{
				double num3 = _worldTime - value.LastRun;
				if (num3 > 1.0 || num3 <= 0.0)
				{
					num3 = _elapsedNow;
					value.HungerOwed = 0;
				}
				value.Owed = (float)num3;
				value.LastRun = _worldTime;
				_ran[num2]++;
			}
			else
			{
				_skipped++;
			}
			return value;
		}

		private static int BandOf(Actor a)
		{
			SceneCollector simView = SceneCollector.SimView;
			if (simView == null || a._has_status_possessed)
			{
				return 1;
			}
			Vector2 current_position = a.current_position;
			float num = current_position.x - simView.SimFocusX;
			float num2 = current_position.y - simView.SimFocusY;
			float num3 = num * num + num2 * num2;
			if (num3 < 6400f)
			{
				return 1;
			}
			float tall = ((a.asset != null && a.asset.is_boat) ? 2.5f : Mathf.Max(1f, a.current_scale.y));
			if (simView.SimSees(current_position.x, current_position.y, tall, out var px))
			{
				if (!(px >= 8f))
				{
					if (!(px >= 3f))
					{
						return 3;
					}
					return 2;
				}
				return 1;
			}
			if (num3 < 40000f)
			{
				return 2;
			}
			if (num3 < 160000f)
			{
				return 3;
			}
			if (!Dragons.Is(a) && !Crabzillas.Is(a))
			{
				return 4;
			}
			return 1;
		}

		private static void Prune()
		{
			_pruneAt = Time.realtimeSinceStartup + 20f;
			int num = _step - 600;
			for (int i = 0; i <= _hashTop && i < _byHash.Length; i++)
			{
				State state = _byHash[i];
				if (state != null && state.Step < num)
				{
					_byHash[i] = null;
					_known--;
				}
			}
			if (_states.Count == 0)
			{
				return;
			}
			_deferred.Clear();
			foreach (KeyValuePair<Actor, State> state2 in _states)
			{
				if (state2.Key == null || state2.Value.Step < num)
				{
					_deferred.Add(state2.Key);
				}
			}
			for (int j = 0; j < _deferred.Count; j++)
			{
				_states.Remove(_deferred[j]);
				_known--;
			}
			_deferred.Clear();
		}

		private static void LogRates()
		{
			_logAt = Time.realtimeSinceStartup + 60f;
			if (_steps > 0)
			{
				Log.Info("far creatures: " + Report());
			}
			Array.Clear(_ran, 0, _ran.Length);
			_skipped = 0L;
			_steps = 0L;
		}

		internal static string Report()
		{
			long num = Math.Max(1L, _steps);
			return $"{_steps} steps; a step ran {_ran[1] / num} every step, {_ran[2] / num} every 2nd, {_ran[3] / num} every 3rd, {_ran[4] / num} every 4th, {_skipped / num} waited; {_known} known, {_jobsPatched} jobs in {_patched.Count} batches";
		}

		internal static void ResetCounts()
		{
			Array.Clear(_ran, 0, _ran.Length);
			_skipped = 0L;
			_steps = 0L;
			_jobTimesAt.Clear();
			foreach (KeyValuePair<string, double> item in JobTimesNow())
			{
				_jobTimesAt[item.Key] = item.Value;
			}
		}

		private static Dictionary<string, double> JobTimesNow()
		{
			_jobTimesNow.Clear();
			JobManagerActors jobManagerActors = World.world?.units?._job_manager;
			if (jobManagerActors == null)
			{
				return _jobTimesNow;
			}
			List<BatchActors> batches_active = ((JobManagerBase<BatchActors, Actor>)jobManagerActors)._batches_active;
			for (int i = 0; i < batches_active.Count; i++)
			{
				List<Job<Actor>> jobs_post = ((Batch<Actor>)batches_active[i]).jobs_post;
				for (int j = 0; j < jobs_post.Count; j++)
				{
					Job<Actor> job = jobs_post[j];
					if (job != null && job.id != null)
					{
						_jobTimesNow.TryGetValue(job.id, out var value);
						_jobTimesNow[job.id] = value + job.time_benchmark;
					}
				}
			}
			return _jobTimesNow;
		}

		internal static string JobTimes(int most = 8)
		{
			long num = Math.Max(1L, _steps);
			List<KeyValuePair<string, double>> list = new List<KeyValuePair<string, double>>();
			double num2 = 0.0;
			foreach (KeyValuePair<string, double> item in JobTimesNow())
			{
				_jobTimesAt.TryGetValue(item.Key, out var value);
				double num3 = (item.Value - value) * 1000.0 / (double)num;
				num2 += num3;
				list.Add(new KeyValuePair<string, double>(item.Key, num3));
			}
			list.Sort((KeyValuePair<string, double> a, KeyValuePair<string, double> b) => b.Value.CompareTo(a.Value));
			StringBuilder stringBuilder = new StringBuilder(256);
			stringBuilder.Append("post jobs ").Append(num2.ToString("F1")).Append(" ms a step over ")
				.Append(num)
				.Append(" steps: ");
			for (int num4 = 0; num4 < list.Count && num4 < most; num4++)
			{
				if (num4 > 0)
				{
					stringBuilder.Append(", ");
				}
				stringBuilder.Append(list[num4].Key).Append(' ').Append(list[num4].Value.ToString("F2"));
			}
			return stringBuilder.ToString();
		}
	}
}
