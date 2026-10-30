namespace FirstPerson.Net
{
	internal sealed class NetPlayer
	{
		public int Id;

		public string Name = "";

		public long Body;

		public bool God = true;

		public int Party;

		public long LastBody;

		public int PingMs;

		public bool Host;

		public string Key = "";

		public NetLink Link;

		public bool Ready;

		public bool WorldSent;

		public bool Resync;

		public ulong SteamId;

		public void Pack(NetWriter w)
		{
			w.VarU((ulong)Id);
			w.Str(Name);
			w.VarI(Body);
			w.Bool(God);
			w.VarU((ulong)Party);
			w.VarU((ulong)PingMs);
			w.Bool(Host);
			w.VarI(LastBody);
		}

		public static NetPlayer Unpack(NetReader r)
		{
			return new NetPlayer
			{
				Id = (int)r.VarU(),
				Name = MpPrefs.Clean(r.Str(), 24),
				Body = r.VarI(),
				God = r.Bool(),
				Party = (int)r.VarU(),
				PingMs = (int)r.VarU(),
				Host = r.Bool(),
				LastBody = r.VarI()
			};
		}

		public override string ToString()
		{
			return Name + " (#" + Id + ")";
		}
	}
}
