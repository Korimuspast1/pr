using System;
using System.Collections;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class HandLookTest : CivilTest
	{
		protected override string Prefix => "HANDLOOK";

		protected override IEnumerator Body()
		{
			Settings s = Mod.Settings;
			s.GpuRender = true;
			if (Options.Contains("hd"))
			{
				s.Resolution = 3;
			}
			WorldTile worldTile = CivilTest.Stage();
			if (!Check("open land", worldTile != null))
			{
				yield break;
			}
			Actor king = Village("human", 6, 0, worldTile);
			if (!Check("a village", king != null && king.city != null))
			{
				yield break;
			}
			Actor you = ((Founded.Count > 1) ? Founded[1] : king);
			try
			{
				PhenotypeAsset phenotypeAsset = AssetManager.phenotype_library.get("skin_pale");
				if (phenotypeAsset != null)
				{
					you.data.phenotype_index = phenotypeAsset.phenotype_index;
					you.data.phenotype_shade = 0;
					you.clearSprites();
				}
			}
			catch
			{
			}
			yield return Possess(you);
			if (!Check("first person", InControl(you)))
			{
				yield break;
			}
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			Mod.HintUntil = 0f;
			int savedMode = s.TimeMode;
			(string, int)[] array = new(string, int)[2]
			{
				("day", 1),
				("night", 3)
			};
			for (int i = 0; i < array.Length; i++)
			{
				(string, int) tuple = array[i];
				string tag = tuple.Item1;
				int item = tuple.Item2;
				s.TimeMode = item;
				Mod.SetYaw(MathF.PI / 2f);
				Mod.SetPitchDegrees(-20f);
				yield return CivilTest.Wait(1f);
				Mod.SaveFrame("handlook_out_" + tag + ".png");
			}
			Building b = AddHouse("house_human_1", king.city);
			if (!Check("a house", b != null && HouseInterior.IsEnterable(b)))
			{
				s.TimeMode = savedMode;
				yield break;
			}
			Homes.SetOwner(b, you);
			try
			{
				you.current_position = b.current_position + new Vector2(0f, -3.5f);
				you.dirty_current_tile = true;
			}
			catch
			{
			}
			for (float t = 0f; t < 5f; t += 0.2f)
			{
				if (HouseWorld.Of(b, out var _))
				{
					break;
				}
				yield return CivilTest.Wait(0.2f);
			}
			yield return CivilTest.Wait(0.3f);
			for (int i = 0; i < 4; i++)
			{
				if (Mod.Interior.Inside && Mod.Interior.House == b)
				{
					break;
				}
				Mod.EnterHouseForTest(b);
				for (float t = 0f; t < 1.5f; t += 0.1f)
				{
					if (Mod.Interior.Inside && Mod.Interior.House == b)
					{
						break;
					}
					yield return CivilTest.Wait(0.1f);
				}
			}
			if (Check("in the room", Mod.Interior.Inside && Mod.Interior.House == b))
			{
				Interior room = Mod.Interior.Room;
				Vector2 spawn = new Vector2(room.SpawnX, room.SpawnY);
				array = new(string, int)[2]
				{
					("day", 1),
					("night", 3)
				};
				for (int i = 0; i < array.Length; i++)
				{
					(string, int) tuple = array[i];
					string tag = tuple.Item1;
					int item2 = tuple.Item2;
					s.TimeMode = item2;
					Mod.Interior.Eye = spawn + Dir(room.SpawnYaw) * 1.4f;
					Mod.SetYaw(room.SpawnYaw);
					Mod.SetPitchDegrees(-20f);
					yield return CivilTest.Wait(1f);
					Mod.SaveFrame("handlook_room_" + tag + ".png");
					for (int side = 0; side < 2; side++)
					{
						float num = ((side == 0) ? 1f : (-1f));
						Vector2 eye = spawn + Dir(room.SpawnYaw) * 0.55f + Dir(room.SpawnYaw + num * MathF.PI * 0.5f) * 0.75f;
						Mod.Interior.Eye = eye;
						Mod.SetYaw(room.SpawnYaw - num * MathF.PI * 0.62f);
						Mod.SetPitchDegrees(-8f);
						if (!Mod.Interior.DoorHeldOpen)
						{
							Mod.Interior.ToggleDoor();
						}
						for (float t = 0f; t < 1.5f; t += 0.1f)
						{
							Mod.Interior.Eye = eye;
							yield return CivilTest.Wait(0.1f);
						}
						Note($"{tag} side {side}: door open {Mod.Interior.DoorOpen:F2}");
						Mod.SaveFrame($"handlook_doorback_{tag}_{side}.png");
					}
				}
				if (Mod.Interior.DoorHeldOpen)
				{
					Mod.Interior.ToggleDoor();
				}
				Mod.LeaveRoomForTest();
				yield return CivilTest.Wait(0.3f);
			}
			try
			{
				b.removeBuildingFinal();
			}
			catch
			{
			}
			s.TimeMode = savedMode;
			Vector2 Dir(float a)
			{
				return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
			}
		}
	}
}
