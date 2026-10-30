namespace FirstPerson.Core
{
	public sealed class MeshTexture
	{
		public string Name;

		public string MimeType;

		public string Uri;

		public byte[] Encoded;

		public uint[] Pixels;

		public int Width;

		public int Height;

		public bool Crisp;

		public bool Cutout;

		public uint Factor = uint.MaxValue;

		public string Problem;

		public string Filter;

		public MeshTexture Source;
	}
}
