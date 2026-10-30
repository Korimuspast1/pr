using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace FirstPerson.Core
{
	public sealed class WorkerPool
	{
		public abstract class Job
		{
			public abstract void Run(int chunk, int worker);
		}

		private sealed class Slot
		{
			public Job Job;

			public int Chunks;

			public int Limit;

			public int Stayers;

			public int Completed;

			public ExceptionDispatchInfo Error;

			public volatile bool Dropped;
		}

		private readonly Thread[] _threads;

		private readonly ManualResetEventSlim[] _wake;

		private readonly Slot[] _slots = new Slot[2]
		{
			new Slot(),
			new Slot()
		};

		private long _ticket;

		private int _job;

		private int[] _doneIn = new int[256];

		private int _busy;

		private volatile bool _stop;

		private static readonly long SpinTicks = Stopwatch.Frequency / 25000;

		private long[] _probes;

		private int _probed;

		private static readonly float[] ProbeIn = new float[6]
		{
			1.1754944E-38f,
			0.5f,
			float.Epsilon,
			1f,
			8.940697E-08f,
			2.9802322E-08f
		};

		public int Workers => _threads.Length;

		public bool Failed => _slots[_job & 1].Error != null;

		public bool Started => Volatile.Read(ref _probed) >= _threads.Length;

		public WorkerPool(int workers, string name)
		{
			workers = Math.Max(0, workers);
			_threads = new Thread[workers];
			_wake = new ManualResetEventSlim[workers];
			_probes = new long[workers];
			for (int i = 0; i < workers; i++)
			{
				_wake[i] = new ManualResetEventSlim(initialState: false, 0);
				int w = i + 1;
				_threads[i] = new Thread((ThreadStart)delegate
				{
					Loop(w);
				})
				{
					IsBackground = true,
					Name = name + " " + w
				};
				_threads[i].Start();
			}
		}

		public bool TryBegin()
		{
			return Interlocked.CompareExchange(ref _busy, 1, 0) == 0;
		}

		public void End()
		{
			Volatile.Write(ref _busy, 0);
		}

		public void Start(Job job, int chunks, int helpers = int.MaxValue, int limit = int.MaxValue, int stayers = 0)
		{
			int num = _job + 1;
			Slot obj = _slots[num & 1];
			obj.Job = job;
			obj.Chunks = chunks;
			obj.Limit = Math.Min(limit, chunks);
			obj.Stayers = stayers;
			obj.Completed = 0;
			obj.Error = null;
			obj.Dropped = false;
			if (_doneIn.Length < chunks)
			{
				_doneIn = new int[Math.Max(chunks, _doneIn.Length * 2)];
			}
			_job = num;
			Interlocked.Exchange(ref _ticket, (long)num << 32);
			int num2 = Math.Min(Math.Min(helpers, _threads.Length), chunks - 1);
			for (int i = 0; i < num2; i++)
			{
				_wake[i].Set();
			}
		}

		public bool TryTake(out int chunk)
		{
			Slot slot = _slots[_job & 1];
			long num;
			int num2;
			do
			{
				num = Volatile.Read(ref _ticket);
				num2 = (int)(num & 0xFFFFFFFFu);
				if (num2 >= slot.Chunks || num2 >= Volatile.Read(ref slot.Limit))
				{
					chunk = -1;
					return false;
				}
			}
			while (Interlocked.CompareExchange(ref _ticket, num + 1, num) != num);
			chunk = num2;
			return true;
		}

		public void Raise(int limit)
		{
			Slot slot = _slots[_job & 1];
			limit = Math.Min(limit, slot.Chunks);
			Volatile.Write(ref slot.Limit, limit);
			int num = Math.Min(slot.Stayers, _wake.Length);
			if (num != 0)
			{
				int num2 = limit - (int)(Volatile.Read(ref _ticket) & 0xFFFFFFFFu);
				for (int i = 0; i < num && i < num2; i++)
				{
					_wake[i].Set();
				}
			}
		}

		public void RunHere(int chunk)
		{
			Slot slot = _slots[_job & 1];
			try
			{
				if (!slot.Dropped)
				{
					slot.Job.Run(chunk, 0);
				}
			}
			finally
			{
				Done(chunk);
			}
		}

		private void Done(int chunk)
		{
			Slot obj = _slots[_job & 1];
			Volatile.Write(ref _doneIn[chunk], _job);
			Interlocked.Increment(ref obj.Completed);
		}

		public bool IsDone(int chunk)
		{
			return Volatile.Read(ref _doneIn[chunk]) == _job;
		}

		public void Await(int chunk)
		{
			int spins = 0;
			while (!IsDone(chunk))
			{
				Pause(ref spins);
			}
		}

		public void Finish()
		{
			Slot slot = _slots[_job & 1];
			if (slot.Error != null)
			{
				slot.Dropped = true;
			}
			Raise(slot.Chunks);
			try
			{
				int chunk;
				while (TryTake(out chunk))
				{
					RunHere(chunk);
				}
			}
			catch
			{
				Drop();
				throw;
			}
			int spins = 0;
			while (Volatile.Read(ref slot.Completed) < slot.Chunks)
			{
				Pause(ref spins);
			}
			slot.Job = null;
			ExceptionDispatchInfo error = slot.Error;
			slot.Error = null;
			error?.Throw();
		}

		public void Drop()
		{
			Slot slot = _slots[_job & 1];
			slot.Dropped = true;
			Raise(slot.Chunks);
			int chunk;
			while (TryTake(out chunk))
			{
				Done(chunk);
			}
			int spins = 0;
			while (Volatile.Read(ref slot.Completed) < slot.Chunks)
			{
				Pause(ref spins);
			}
			slot.Job = null;
			slot.Error = null;
		}

		private static void Pause(ref int spins)
		{
			if (++spins < 64)
			{
				Thread.SpinWait(20);
			}
			else
			{
				Thread.Yield();
			}
		}

		public static long Probe()
		{
			float[] probeIn = ProbeIn;
			float num = probeIn[0] * probeIn[1];
			float num2 = probeIn[2] * probeIn[3];
			float num3 = probeIn[3] + probeIn[4];
			float num4 = 0f - probeIn[3] - probeIn[4];
			float num5 = probeIn[3] + probeIn[5];
			float num6 = 0f - probeIn[3] - probeIn[5];
			long num7 = 17L;
			float[] array = new float[6] { num, num2, num3, num4, num5, num6 };
			foreach (float value in array)
			{
				num7 = num7 * 31 + BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
			}
			return num7;
		}

		public bool WaitStarted(int ms)
		{
			if (Started)
			{
				return true;
			}
			Stopwatch stopwatch = Stopwatch.StartNew();
			while (stopwatch.ElapsedMilliseconds < ms)
			{
				Thread.Sleep(1);
				if (Started)
				{
					return true;
				}
			}
			return false;
		}

		public bool FloatsAgree()
		{
			if (!Started)
			{
				throw new InvalidOperationException("worker threads not started yet");
			}
			long num = Probe();
			long[] probes = _probes;
			for (int i = 0; i < probes.Length; i++)
			{
				if (probes[i] != num)
				{
					return false;
				}
			}
			return true;
		}

		private void Loop(int w)
		{
			_probes[w - 1] = Probe();
			Interlocked.Increment(ref _probed);
			ManualResetEventSlim manualResetEventSlim = _wake[w - 1];
			while (true)
			{
				if (!manualResetEventSlim.IsSet)
				{
					long num = Stopwatch.GetTimestamp() + SpinTicks;
					while (!manualResetEventSlim.IsSet && Stopwatch.GetTimestamp() < num)
					{
						Thread.SpinWait(20);
					}
					if (!manualResetEventSlim.IsSet)
					{
						manualResetEventSlim.Wait();
					}
				}
				manualResetEventSlim.Reset();
				if (_stop)
				{
					break;
				}
				Work(w);
			}
		}

		private void Work(int w)
		{
			while (true)
			{
				long num = Volatile.Read(ref _ticket);
				int num2 = (int)(num >> 32);
				int num3 = (int)(num & 0xFFFFFFFFu);
				Slot slot = _slots[num2 & 1];
				Job job = slot.Job;
				int chunks = slot.Chunks;
				if (job == null || num3 >= chunks || num3 >= Volatile.Read(ref slot.Limit))
				{
					break;
				}
				if (Interlocked.CompareExchange(ref _ticket, num + 1, num) != num)
				{
					continue;
				}
				if (!slot.Dropped)
				{
					try
					{
						job.Run(num3, w);
					}
					catch (Exception source)
					{
						Interlocked.CompareExchange(ref slot.Error, ExceptionDispatchInfo.Capture(source), null);
					}
				}
				Volatile.Write(ref _doneIn[num3], num2);
				Interlocked.Increment(ref slot.Completed);
			}
		}

		public void Dispose()
		{
			_stop = true;
			ManualResetEventSlim[] wake = _wake;
			for (int i = 0; i < wake.Length; i++)
			{
				wake[i].Set();
			}
		}
	}
}
