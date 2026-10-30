using System;
using System.Collections;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class RoomJumpTest : CivilTest
	{
		protected override string Prefix => "ROOMJUMP";

		protected override IEnumerator Body()
		{
			WorldTile worldTile = CivilTest.Stage();
			if (!Check("open land", worldTile != null))
			{
				yield break;
			}
			CivilTest.ClearBox(worldTile.x - 10, worldTile.y - 8, worldTile.x + 16, worldTile.y + 8);
			Building house = null;
			try
			{
				house = World.world.buildings.addBuilding("house_human_1", worldTile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				house?.completeConstruction();
			}
			catch (Exception ex)
			{
				Log.Error("ROOMJUMP house: " + ex.Message);
			}
			if (!Check("a house to go into", house != null && HouseInterior.IsEnterable(house)))
			{
				yield break;
			}
			Actor you = CivilTest.Spawn("human", new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y - 2.5f));
			try
			{
				string[] array = new string[2] { "giant", "tiny" };
				foreach (string text in array)
				{
					if (you.hasTrait(text))
					{
						Note("spawned " + text + ": taken off for a human's height");
						you.removeTrait(text);
					}
				}
			}
			catch
			{
			}
			yield return Possess(you);
			if (!Check("first person in a villager", InControl(you)))
			{
				yield break;
			}
			Mod.SetYaw(MathF.PI / 2f);
			Mod.SetPitchDegrees(0f);
			yield return CivilTest.Wait(1.5f);
			Mod.EnterHouseForTest(house);
			yield return CivilTest.Wait(1f);
			if (!Check("in the house's room", Mod.Interior.Inside && Mod.Interior.House == house))
			{
				yield break;
			}
			for (int jump = 0; jump < 2; jump++)
			{
				float rest = Mod.View.Z;
				float top = rest;
				float low = rest;
				float height = 0f;
				float ceiling = Mod.RoomCeiling();
				PossessionHooks.ScriptedJump = true;
				float t = 0f;
				bool upSeen = false;
				bool downAgain = false;
				float landed = -1f;
				int landedFrame = 0;
				int frames = 0;
				while (t < 20f)
				{
					yield return null;
					t += Time.unscaledDeltaTime;
					frames++;
					float z = Mod.View.Z;
					top = Mathf.Max(top, z);
					if (upSeen)
					{
						low = Mathf.Min(low, z);
					}
					float num = 0f;
					try
					{
						num = you.position_height;
					}
					catch
					{
					}
					height = Mathf.Max(height, num);
					if (frames % 4 == 0)
					{
						Note($"  t {t:F2} h {num:F3} leaping {PossessionHooks.Leaping} lift {PossessionHooks.Jump.Lift:F3} z {z:F2} body {Mod.RoomBodyForTest:F2} sprite {you.getSpriteToRender()?.name} scale {you.current_scale.y:F2} forces {you.under_forces} inside {you.is_inside_building}");
					}
					if (z > rest + 0.06f && !upSeen)
					{
						upSeen = true;
						Mod.SaveFrame($"roomjump_{jump}.png");
					}
					if (upSeen && num <= 0f && landed < 0f)
					{
						landed = t;
						landedFrame = frames;
					}
					if (landed >= 0f && t > landed + 0.6f && frames > landedFrame + 25)
					{
						downAgain = Mathf.Abs(z - rest) < 0.05f;
						break;
					}
				}
				Note($"jump {jump + 1}: {(float)frames / Mathf.Max(0.01f, t):F0} fps, {t:F1} s, eye {rest:F2} -> top {top:F2}, low after {low:F2}, game height {height:F2}, ceiling {ceiling:F2}, body {Mod.RoomBodyForTest:F2}, feet {RoomFloors.Feet:F2} shown {RoomFloors.Shown:F2}, at {Mod.Interior.Eye}, fitted {Mod.Interior.Fitted}, house {Mod.Interior.House?.asset?.id}");
				Check($"jump {jump + 1}: the game jumps your body indoors", height > 0.1f, $"height {height:F2}");
				Check($"jump {jump + 1}: the eye goes up with it", top > rest + Mathf.Min(0.3f, (ceiling - rest) * 0.5f), $"{rest:F2} -> {top:F2}");
				Check($"jump {jump + 1}: the head stays under the ceiling", top < ceiling, $"top {top:F2}, ceiling {ceiling:F2}");
				Check($"jump {jump + 1}: the knees give as you land", low < rest - 0.01f, $"low {low:F2}, rest {rest:F2}");
				Check($"jump {jump + 1}: back down on the floor", downAgain, $"eye {Mod.View.Z:F2}");
				Check($"jump {jump + 1}: still in the room", Mod.Interior.Inside);
				yield return CivilTest.Wait(0.6f);
			}
			if (Mod.Interior.Inside)
			{
				Mod.LeaveRoomForTest();
			}
		}
	}
}
