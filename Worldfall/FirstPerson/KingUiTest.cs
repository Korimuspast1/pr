using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class KingUiTest : CivilTest
	{
		protected override string Prefix => "KUI";

		private IEnumerator Capture(string name, float wait = 0.7f)
		{
			if (wait > 0f)
			{
				yield return CivilTest.Wait(wait);
			}
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, name));
			yield return null;
			yield return null;
			Note("captured " + name);
		}

		protected override IEnumerator Body()
		{
			Actor king = Village("human", 12, 2);
			if (!Check("our village", king != null))
			{
				yield break;
			}
			Actor rival = Village("orc", 10, 1, null, 60f);
			if (!Check("a rival village far off", rival != null && rival.kingdom != null && rival.kingdom != king.kingdom))
			{
				yield break;
			}
			foreach (Actor item in Founded)
			{
				try
				{
					((BaseSimObject)item).addStatusEffect("invincible", 600f, pColorEffect: false);
				}
				catch
				{
				}
			}
			yield return Possess(king);
			if (!Check("the king in first person", InControl(king) && Royal.IsKing(king)))
			{
				yield break;
			}
			try
			{
				Mod.Royal.DeclareWar(king, rival.kingdom, Mod.Toast, Mod.PlayHorn);
			}
			catch (Exception ex)
			{
				Note("war: " + ex.Message);
			}
			try
			{
				Mod.Royal.Levy(king, Mod.Toast);
			}
			catch
			{
			}
			yield return CivilTest.Wait(1.5f);
			Mod.Royal.Open = true;
			yield return Capture("kui_council.png", 1.2f);
			Mod.Royal.Open = false;
			Building stone = null;
			try
			{
				WorldTile tile = World.world.GetTile((int)king.current_position.x + 2, (int)king.current_position.y);
				stone = World.world.buildings.addBuilding("monolith", tile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
			}
			catch (Exception ex2)
			{
				Note("monolith: " + ex2.Message);
			}
			Mod.OpenObeliskForTest(stone);
			yield return Capture("kui_obelisk.png", 1f);
			Check("the obelisk's window", stone != null);
			Mod.Royal.ObeliskOpen = false;
			yield return CivilTest.Wait(0.4f);
			Check("the window art", GameArt.WindowBig != null && GameArt.RedButton != null && GameArt.CloseTab != null, $"window {GameArt.WindowBig != null}, red {GameArt.RedButton != null}, close {GameArt.CloseTab != null}");
			WarMap map = Mod.WarMap;
			map.Toggle(king, Mod.Toast);
			yield return Capture("kui_map.png", 1.5f);
			Check("the war map opens", map.Open);
			map.Zoom = map.ZoomMin;
			map.Centre = new Vector2(WarMap.WorldWidth * 0.5f, WarMap.WorldHeight * 0.5f);
			yield return Capture("kui_map_world.png", 1.2f);
			Check("zoomed out to the whole world", map.Span / map.Zoom >= Mathf.Max(WarMap.WorldWidth, WarMap.WorldHeight), $"view {map.Span / map.Zoom:F0} tiles, world {WarMap.WorldWidth}x{WarMap.WorldHeight}");
			map.Zoom = Mathf.Min(map.ZoomMax, map.Zoom * 12f);
			map.Centre = king.current_position;
			int marked = map.MarkIn(new Rect(king.current_position.x - 30f, king.current_position.y - 30f, 60f, 60f));
			try
			{
				WarFlag f = map.Plant(WarMap.GroundFor(World.world.GetTile((int)king.current_position.x + 6, (int)king.current_position.y + 4)), king.kingdom);
				map.Plant(WarMap.GroundFor(World.world.GetTile((int)king.current_position.x - 8, (int)king.current_position.y - 3)), king.kingdom);
				map.Assign(Mod.Royal, king, map.MarkedOwn(), f, out var _);
			}
			catch (Exception ex3)
			{
				Note("flags: " + ex3.Message);
			}
			yield return Capture("kui_map_marks.png", 1.2f);
			Note($"marked {marked}, zoom {map.Zoom:F2} (min {map.ZoomMin:F2}, max {map.ZoomMax:F2}), view {map.WantView}");
			map.Close();
			map.ClearFlags();
			yield return CivilTest.Wait(0.4f);
			try
			{
				if (!king.isFavorite())
				{
					Mod.Favs.Toggle(king);
				}
				Mod.Favs.Update(0f, force: true);
			}
			catch (Exception ex4)
			{
				Note("favourite: " + ex4.Message);
			}
			Mod.Craft.Toggle();
			yield return Capture("kui_craft.png", 1f);
			Check("crafting has recipes", Mod.Craft.Recipes.Count > 0, Mod.Craft.Recipes.Count + " recipes");
			Mod.Craft.Open = false;
			yield return CivilTest.Wait(0.3f);
			Mod.SetSettingsMenu(open: true);
			SettingsMenu menu = Mod.SettingsMenu;
			for (int tab = 0; tab < SettingsMenu.Tabs.Length; tab++)
			{
				menu.Tab = tab;
				menu.Scroll = Vector2.zero;
				yield return Capture("kui_set_" + SettingsMenu.Tabs[tab].ToLowerInvariant() + ".png", (tab == 0) ? 1f : 0.5f);
			}
			menu.Tab = 1;
			menu.Scroll = new Vector2(0f, 99999f);
			yield return Capture("kui_set_picture_foot.png", 0.5f);
			Mod.SetSettingsMenu(open: false);
			yield return CivilTest.Wait(0.5f);
		}
	}
}
