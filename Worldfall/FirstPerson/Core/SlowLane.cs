using System;
using System.Collections.Generic;
using System.Threading;

namespace FirstPerson.Core
{
	public static class SlowLane
	{
		private const int Lanes = 2;

		private static readonly Queue<Action> Jobs = new Queue<Action>();

		private static readonly Queue<Action> Soon = new Queue<Action>();

		private static Thread[] _threads;

		private static readonly object Gate = new object();

		public static int Waiting
		{
			get
			{
				lock (Gate)
				{
					return Jobs.Count + Soon.Count;
				}
			}
		}

		public static void Run(Action job)
		{
			lock (Gate)
			{
				if (_threads == null)
				{
					Start();
				}
				Jobs.Enqueue(job);
				Monitor.Pulse(Gate);
			}
		}

		public static void RunSoon(Action job)
		{
			lock (Gate)
			{
				if (_threads == null)
				{
					Start();
				}
				Soon.Enqueue(job);
				Monitor.Pulse(Gate);
			}
		}

		private static void Start()
		{
			_threads = new Thread[2];
			for (int i = 0; i < 2; i++)
			{
				_threads[i] = new Thread(Loop)
				{
					IsBackground = true,
					Name = "Worldfall background " + i,
					Priority = ThreadPriority.BelowNormal
				};
				_threads[i].Start();
			}
		}

		private static void Loop()
		{
			while (true)
			{
				Action action;
				lock (Gate)
				{
					while (Jobs.Count == 0 && Soon.Count == 0)
					{
						Monitor.Wait(Gate);
					}
					action = ((Soon.Count > 0) ? Soon.Dequeue() : Jobs.Dequeue());
				}
				try
				{
					action();
				}
				catch
				{
				}
			}
		}
	}
}
