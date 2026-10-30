namespace FirstPerson.Core
{
	public static class RegaliaRules
	{
		public static GearLook Compose(RoleBits bits, CouncilSeat seat, KingdomStyle style, string equippedHelmetId, bool handsBusy)
		{
			GearLook result = new GearLook
			{
				Style = style
			};
			bool flag = !string.IsNullOrEmpty(equippedHelmetId);
			bool flag2 = HighHelmet(equippedHelmetId);
			if ((bits & RoleBits.King) != RoleBits.None)
			{
				result.Head = ((!flag2) ? HeadGear.CrownOpen : HeadGear.CrownHelm);
				result.Back = BackGear.Mantle;
				result.Any = true;
				return result;
			}
			bool flag3 = (bits & RoleBits.Heir) != 0;
			if ((bits & RoleBits.Child) != RoleBits.None)
			{
				if (flag3)
				{
					result.Head = HeadGear.Circlet;
				}
				result.Any = result.Head != HeadGear.None;
				return result;
			}
			bool flag4 = (bits & RoleBits.Champion) != 0;
			bool flag5 = (bits & RoleBits.Leader) != 0;
			bool flag6 = (bits & RoleBits.Captain) != 0;
			bool flag7 = (bits & RoleBits.Guard) != 0;
			bool flag8 = (bits & (RoleBits.Captain | RoleBits.Soldier | RoleBits.Guard)) != 0;
			HeadGear headGear = (flag8 ? ((!flag) ? HeadGear.Coif : (flag6 ? HeadGear.PlumeTall : HeadGear.Plume)) : HeadGear.None);
			HeadGear headGear2 = (flag3 ? HeadGear.Circlet : (flag4 ? HeadGear.Laurel : (flag5 ? HeadGear.Hat : ((seat == CouncilSeat.Spymaster) ? HeadGear.Hood : HeadGear.None))));
			switch (headGear2)
			{
			case HeadGear.Hat:
			case HeadGear.Hood:
				result.Head = headGear2;
				break;
			case HeadGear.Circlet:
				result.Head = (HeadGear)((uint)((!flag2) ? 4 : 0) | (uint)headGear);
				break;
			case HeadGear.Laurel:
				result.Head = (HeadGear)(8 | ((flag8 && !flag) ? 64 : 0));
				break;
			default:
				result.Head = headGear;
				break;
			}
			result.Neck = ((seat == CouncilSeat.Chancellor) ? NeckGear.SealChain : (flag5 ? NeckGear.ChainOfOffice : NeckGear.None));
			result.Back = (flag6 ? BackGear.Banner : BackGear.None);
			result.Sash = ((seat == CouncilSeat.Marshal) ? SashGear.Gold : ((flag4 || flag6) ? SashGear.GoldEdged : (flag7 ? SashGear.Striped : (flag8 ? SashGear.Plain : SashGear.None))));
			result.Body = (BodyGear)(((seat == CouncilSeat.Steward) ? 1u : 0u) | (uint)((seat == CouncilSeat.Mage) ? 2 : 0));
			int hand;
			if (!handsBusy)
			{
				switch (seat)
				{
				default:
					hand = ((flag7 && !flag4) ? 1 : 0);
					break;
				case CouncilSeat.Steward:
					hand = 4;
					break;
				case CouncilSeat.Mage:
					hand = 3;
					break;
				case CouncilSeat.Marshal:
					hand = 2;
					break;
				}
			}
			else
			{
				hand = 0;
			}
			result.Hand = (HandGear)hand;
			result.Any = result.Head != HeadGear.None || result.Neck != NeckGear.None || result.Sash != SashGear.None || result.Back != BackGear.None || result.Hand != HandGear.None || result.Body != BodyGear.None;
			return result;
		}

		public static bool HighHelmet(string helmetId)
		{
			if (!string.IsNullOrEmpty(helmetId))
			{
				return (int)ArmorMeshes.MetalOf(helmetId) > 2;
			}
			return false;
		}
	}
}
