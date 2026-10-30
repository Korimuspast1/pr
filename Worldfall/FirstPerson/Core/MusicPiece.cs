namespace FirstPerson.Core
{
	public sealed class MusicPiece
	{
		public string Name;

		public float[] Stereo;

		public byte[] Pcm;

		public bool Loops;

		public float Beat;

		public int BarBeats = 4;

		public int Length
		{
			get
			{
				if (Stereo == null)
				{
					return Pcm.Length / 2;
				}
				return Stereo.Length;
			}
		}

		public float Seconds => (float)Length / 2f / 32000f;

		public MusicPiece ToPcm()
		{
			if (Stereo != null)
			{
				Pcm = MusicSynth.ToPcm16(Stereo);
				Stereo = null;
			}
			return this;
		}
	}
}
