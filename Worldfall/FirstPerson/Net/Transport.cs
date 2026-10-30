using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace FirstPerson.Net
{
	internal abstract class Transport
	{
		protected struct Event
		{
			public NetLink Link;

			public byte Kind;

			public byte[] Data;

			public string Reason;
		}

		protected readonly Queue<Event> Inbox = new Queue<Event>();

		public long InboxCap;

		public float BudgetMs;

		private static readonly List<KeyValuePair<float, Action>> _later = new List<KeyValuePair<float, Action>>();

		public abstract string Name { get; }

		protected void PostUp(NetLink l)
		{
			lock (Inbox)
			{
				Inbox.Enqueue(new Event
				{
					Link = l,
					Kind = 0
				});
			}
		}

		protected void PostDown(NetLink l, string reason)
		{
			lock (Inbox)
			{
				Inbox.Enqueue(new Event
				{
					Link = l,
					Kind = 1,
					Reason = reason
				});
			}
		}

		protected void PostData(NetLink l, byte[] data)
		{
			if (Volatile.Read(ref l.Flooded) != 0)
			{
				return;
			}
			long num = Interlocked.Add(ref l.InboxBytes, data.Length);
			if (InboxCap > 0 && num > InboxCap)
			{
				Interlocked.Add(ref l.InboxBytes, -data.Length);
				if (Interlocked.Exchange(ref l.Flooded, 1) == 0)
				{
					lock (Inbox)
					{
						Inbox.Enqueue(new Event
						{
							Link = l,
							Kind = 3
						});
						return;
					}
				}
				return;
			}
			lock (Inbox)
			{
				Inbox.Enqueue(new Event
				{
					Link = l,
					Kind = 2,
					Data = data
				});
			}
		}

		public virtual void Pump(INetEvents ev)
		{
			long timestamp = Stopwatch.GetTimestamp();
			long num = ((BudgetMs > 0f) ? ((long)((double)(BudgetMs * (float)Stopwatch.Frequency) / 1000.0)) : long.MaxValue);
			while (true)
			{
				Event obj;
				lock (Inbox)
				{
					if (Inbox.Count == 0 || Stopwatch.GetTimestamp() - timestamp > num)
					{
						break;
					}
					obj = Inbox.Dequeue();
				}
				try
				{
					if (obj.Kind == 0)
					{
						ev.LinkUp(obj.Link);
					}
					else if (obj.Kind == 1)
					{
						ev.LinkDown(obj.Link, obj.Reason);
					}
					else if (obj.Kind == 2)
					{
						Interlocked.Add(ref obj.Link.InboxBytes, -obj.Data.Length);
						if (Volatile.Read(ref obj.Link.Flooded) == 0)
						{
							ev.Data(obj.Link, obj.Data, 0, obj.Data.Length);
						}
					}
					else if (obj.Kind == 3)
					{
						Log.Info("NET " + obj.Link?.ToString() + " sent more than " + (InboxCap >> 20) + " MB before it could be taken in: closed");
						obj.Link.Close("Too much data at once");
					}
				}
				catch (Exception ex)
				{
					Log.Error("NET event from " + obj.Link?.ToString() + ": " + ex);
				}
			}
		}

		protected static void Later(float seconds, Action what)
		{
			_later.Add(new KeyValuePair<float, Action>(Time.realtimeSinceStartup + seconds, what));
		}

		public static void RunLater(bool all = false)
		{
			if (_later.Count == 0)
			{
				return;
			}
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			for (int num = _later.Count - 1; num >= 0; num--)
			{
				if (all || !(_later[num].Key > realtimeSinceStartup))
				{
					Action value = _later[num].Value;
					_later.RemoveAt(num);
					try
					{
						value();
					}
					catch (Exception ex)
					{
						Log.Error("NET closing: " + ex.Message);
					}
				}
			}
		}

		public abstract void Shutdown(string reason);
	}
}
