using System;

namespace FirstPerson.Core
{
	public sealed class MoteRecipe
	{
		public BiomeKind Biome;

		public MoteStyle Style;

		public string Name;

		public int Slots;

		public float RMin = 1f;

		public float RMax = 12f;

		public float ZMin = 0.3f;

		public float ZMax = 3f;

		public float Top = 7f;

		public float LifeMin = 6f;

		public float LifeMax = 12f;

		public float FadeIn = 1f;

		public float FadeOut = 1.5f;

		public float SizeMin = 0.06f;

		public float SizeMax = 0.1f;

		public float Aspect = 1f;

		public float Grow;

		public uint[] Colors;

		public byte Alpha = 200;

		public bool Additive;

		public bool Emissive;

		public float Speed;

		public float Drift = 0.2f;

		public float Wind = 0.6f;

		public float Twinkle;

		public float Blink;

		public bool Warm;

		public float DayW = 1f;

		public float NightW = 1f;

		public float DayBright = 1f;

		public float NightBright = 1f;

		public Func<int, SpriteImage> Sprite;

		public int Frames = 1;

		public float FrameRate;

		public float Hue;

		public float Light;

		public uint LightColor;

		public bool Flee;

		public bool Trail;

		public bool Glint;

		public bool Pulse;

		public bool Beat;

		public bool Starry;

		public float Swirl;

		public float Jitter;

		public bool Ahead = true;
	}
}
