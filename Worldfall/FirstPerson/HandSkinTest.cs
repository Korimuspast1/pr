using System;
using System.Collections;
using System.Reflection;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class HandSkinTest : CivilTest
	{
		private static readonly string[] Skins = new string[9] { "skin_pale", "skin_light", "skin_medium", "skin_dark", "skin_black", "skin_blue", "skin_purple", "skin_green", "skin_red" };

		private static readonly string[] Kinds = new string[4] { "human", "elf", "orc", "dwarf" };

		protected override string Prefix => "HANDSKIN";

		protected override IEnumerator Body()
		{
			WorldTile worldTile = CivilTest.Stage();
			if (worldTile == null)
			{
				Check("a site for the test", ok: false);
				yield break;
			}
			CivilTest.ClearBox(worldTile.x - 6, worldTile.y - 6, worldTile.x + 6, worldTile.y + 6);
			Actor first = CivilTest.Spawn("human", new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f));
			yield return CivilTest.Wait(0.5f);
			yield return Possess(first);
			if (!Check("first person on a host", InControl(first)))
			{
				yield break;
			}
			Mod.Settings.TimeMode = 1;
			Mod.ForceRelight();
			FieldInfo handsField = typeof(WorldBoxMod).GetField("_hands", BindingFlags.Instance | BindingFlags.NonPublic);
			FieldInfo skinField = typeof(Hands).GetField("_skin", BindingFlags.Instance | BindingFlags.NonPublic);
			Vector2 at = first.current_position + new Vector2(0f, -2f);
			int photos = 0;
			string[] kinds = Kinds;
			foreach (string kind in kinds)
			{
				string[] skins = Skins;
				foreach (string skinId in skins)
				{
					PhenotypeAsset ph = null;
					try
					{
						ph = AssetManager.phenotype_library.get(skinId);
					}
					catch
					{
					}
					if (ph == null || ph.colors == null || ph.colors.Length == 0)
					{
						Note("no such skin in this game: " + skinId);
						continue;
					}
					int shade = ((skinId == "skin_dark" || skinId == "skin_black") ? (ph.colors.Length - 1) : 0);
					Actor a = CivilTest.Spawn(kind, at);
					if (a == null)
					{
						Check(kind + " spawned", ok: false);
						continue;
					}
					a.data.phenotype_index = ph.phenotype_index;
					a.data.phenotype_shade = shade;
					a.clearSprites();
					a._last_main_sprite = null;
					yield return CivilTest.Wait(0.3f);
					yield return Possess(a, 1f);
					if (!Check("in control of a " + kind + " (" + skinId + ")", InControl(a)))
					{
						CivilTest.Kill(a);
						continue;
					}
					Mod.SetYaw(MathF.PI / 2f);
					Mod.SetPitchDegrees(-20f);
					yield return CivilTest.Wait(0.6f);
					Color32 color = ph.colors[shade];
					uint num = TerrainCache.Rgb(color.r, color.g, color.b);
					uint num2 = People.GameSkin(a);
					object obj2 = ((handsField != null) ? handsField.GetValue(Mod) : null);
					uint num3 = ((obj2 != null && skinField != null) ? ((uint)skinField.GetValue(obj2)) : 0u);
					SpriteImage spriteImage = Mod.SpriteForTest(a);
					string text = "?";
					if (spriteImage != null)
					{
						Humanoid.Palette palette = Humanoid.Extract(spriteImage, a.asset.id);
						text = Hex(palette.Skin) + (palette.SkinSeen ? "" : " unseen");
					}
					Check($"{kind} {skinId} shade {shade}: hands {Hex(num3)} = game skin {Hex(num)}", num3 == num && num2 == num, "GameSkin " + Hex(num2) + ", sprite guess " + text);
					if (photos++ < 40)
					{
						Mod.SaveFrame("handskin_" + kind + "_" + skinId + ".png");
					}
					Mod.SetPitchDegrees(0f);
				}
			}
			Actor actor = CivilTest.Spawn("zombie", at);
			if (actor != null)
			{
				Check("a zombie has no living skin", People.GameSkin(actor) == 0, Hex(People.GameSkin(actor)));
				CivilTest.Kill(actor);
			}
			yield return Possess(first, 1f);
		}

		private static string Hex(uint c)
		{
			return $"#{c & 0xFF:X2}{(c >> 8) & 0xFF:X2}{(c >> 16) & 0xFF:X2}";
		}
	}
}
