using System;

namespace FirstPerson.Core
{
	[Flags]
	public enum RoleBits : ushort
	{
		None = 0,
		King = 1,
		Heir = 2,
		Leader = 4,
		Councillor = 8,
		Champion = 0x10,
		Captain = 0x20,
		Soldier = 0x40,
		Guard = 0x80,
		OnCampaign = 0x100,
		Female = 0x200,
		Child = 0x400
	}
}
