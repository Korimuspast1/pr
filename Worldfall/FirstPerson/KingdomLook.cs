using FirstPerson.Core;

namespace FirstPerson
{
	internal struct KingdomLook
	{
		public long Id;

		public KingdomStyle Style;

		public bool Styled;

		public int StyleSerial;

		public RegaliaColors Colors;

		public bool Valid => Id > 0;
	}
}
