using FirstPerson.Core;

namespace FirstPerson
{
	internal struct RoleInfo
	{
		public RoleBits Bits;

		public CouncilSeat Seat;

		public KingdomLook Kingdom;

		public string HelmetId;

		public int Serial;

		public bool Wears;

		public bool Has(RoleBits b)
		{
			return (Bits & b) != 0;
		}
	}
}
