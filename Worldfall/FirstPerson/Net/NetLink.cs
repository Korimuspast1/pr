using System.Threading;

namespace FirstPerson.Net
{
	internal abstract class NetLink
	{
		private static int _nextId;

		public readonly int Id = Interlocked.Increment(ref _nextId);

		public string Address = "";

		public volatile object Tag;

		internal long InboxBytes;

		internal int Flooded;

		public abstract float PingMs { get; }

		public abstract long Queued { get; }

		public abstract bool Open { get; }

		public abstract void Send(byte[] data, int off, int len, Delivery how);

		public void Send(NetWriter w, Delivery how)
		{
			Send(w.Buffer, 0, w.Length, how);
		}

		public abstract void Close(string reason);

		public override string ToString()
		{
			return "#" + Id + " " + Address;
		}
	}
}
