using System;

namespace FirstPerson.Core
{
	[Flags]
	public enum HeadGear : ushort
	{
		None = 0,
		CrownOpen = 1,
		CrownHelm = 2,
		Circlet = 4,
		Laurel = 8,
		Hat = 0x10,
		Hood = 0x20,
		Coif = 0x40,
		Plume = 0x80,
		PlumeTall = 0x100
	}
}
