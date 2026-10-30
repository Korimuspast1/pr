namespace FirstPerson.Net
{
	internal static class NetProtocol
	{
		public const int Version = 12;

		public const string WorldfallVersion = "0.9.8.9";

		internal static string BuildForTest;

		public const int ChunkSize = 49152;

		public const int Join = 1;

		public const int Welcome = 2;

		public const int Refused = 3;

		public const int Info = 4;

		public const int Players = 5;

		public const int Kick = 6;

		public const int Chat = 7;

		public const int Mods = 8;

		public const int ModFile = 9;

		public const int ModsReady = 10;

		public const int WorldBegin = 20;

		public const int WorldChunk = 21;

		public const int WorldEnd = 22;

		public const int WorldLoaded = 23;

		public const int Clock = 30;

		public const int Motion = 31;

		public const int Spawn = 32;

		public const int Despawn = 33;

		public const int Data = 34;

		public const int Tiles = 35;

		public const int Building = 36;

		public const int Event = 37;

		public const int Shot = 38;

		public const int Meta = 39;

		public const int Fx = 40;

		public const int Drops = 41;

		public const int Movers = 42;

		public const int Burning = 43;

		public const int Lights = 44;

		public const int Relief = 45;

		public const int MyMotion = 50;

		public const int Act = 51;

		public const int Patch = 52;

		public const int Possess = 53;

		public const int Power = 54;

		public const int Voice = 55;

		public const int Party = 56;

		public const int Talk = 57;

		public const int CrabFire = 58;

		public const int YourBody = 60;

		public const int Died = 61;

		public const int VoiceOut = 62;

		public const int PatchAck = 63;

		public const int Resync = 64;

		public static string Build => BuildForTest ?? "7628f8f7";
	}
}
