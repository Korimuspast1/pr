using System;
using System.Globalization;

namespace FirstPerson.Net
{
	internal sealed class HostRules
	{
		public int Powers;

		public int Death;

		public int RespawnSeconds = 10;

		public int Runs;

		public bool Pvp = true;

		public bool GuestsDig;

		public static readonly string[] PowerNames = new string[3] { "All powers", "Limited powers", "No powers" };

		public static readonly string[] DeathNames = new string[3] { "Pick a new body", "Respawn in town", "Heir takes over" };

		public static readonly string[] RunNames = new string[3] { "Runs allowed", "No runs", "Everyone on a run" };

		public static readonly string[] DigNames = new string[2] { "Own town's land", "Anywhere" };

		public void Read(string k, string v)
		{
			int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result);
			switch (k)
			{
			case "powers":
				Powers = Math.Max(0, Math.Min(2, result));
				break;
			case "death":
				Death = Math.Max(0, Math.Min(2, result));
				break;
			case "respawn":
				RespawnSeconds = Math.Max(0, Math.Min(120, result));
				break;
			case "runs":
				Runs = Math.Max(0, Math.Min(2, result));
				break;
			case "pvp":
				Pvp = result != 0;
				break;
			case "dig":
				GuestsDig = result != 0;
				break;
			}
		}

		public void Write(Action<string, object> line)
		{
			line("powers", Powers);
			line("death", Death);
			line("respawn", RespawnSeconds);
			line("runs", Runs);
			line("pvp", Pvp ? 1 : 0);
			line("dig", GuestsDig ? 1 : 0);
		}

		public void Pack(NetWriter w)
		{
			w.U8(Powers);
			w.U8(Death);
			w.U8(RespawnSeconds);
			w.U8(Runs);
			w.Bool(Pvp);
		}

		public static HostRules Unpack(NetReader r)
		{
			return new HostRules
			{
				Powers = r.U8(),
				Death = r.U8(),
				RespawnSeconds = r.U8(),
				Runs = r.U8(),
				Pvp = r.Bool()
			};
		}
	}
}
