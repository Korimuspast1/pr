using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class AssetDump : MonoBehaviour
	{
		private static bool _allBoats;

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			string options = File.ReadAllText(path);
			bool quit = options.Contains("quit");
			while (!Config.game_loaded || World.world == null || AssetManager.buildings == null)
			{
				yield return null;
			}
			yield return new WaitForSecondsRealtime(3f);
			string dir = Path.Combine(Log.Folder, "assets");
			Directory.CreateDirectory(dir);
			if (options.Contains("uidump"))
			{
				for (float t = 0f; t < 30f; t += 1f)
				{
					if (!SmoothLoader.isLoading())
					{
						break;
					}
					yield return new WaitForSecondsRealtime(1f);
				}
				yield return UiDump.Run(dir);
				if (quit)
				{
					Application.Quit();
				}
				yield break;
			}
			if (options.Contains("fxdump"))
			{
				for (float t = 0f; t < 30f; t += 1f)
				{
					if (!SmoothLoader.isLoading())
					{
						break;
					}
					yield return new WaitForSecondsRealtime(1f);
				}
				yield return FxDump.Run(dir);
				if (quit)
				{
					Application.Quit();
				}
				yield break;
			}
			if (options.Contains("actors"))
			{
				_allBoats = options.Contains("allboats");
				for (float t = 0f; t < 30f; t += 1f)
				{
					if (!SmoothLoader.isLoading())
					{
						break;
					}
					yield return new WaitForSecondsRealtime(1f);
				}
				yield return DumpActors(dir);
				if (quit)
				{
					Application.Quit();
				}
				yield break;
			}
			StringBuilder builds = new StringBuilder("id\tconstruction sprite\tsize\tunits\tprogress needed" + Environment.NewLine);
			StringBuilder table = new StringBuilder();
			table.AppendLine("id\ttype\tbuilding_type\tgroup\tkingdom\tciv_kingdom\tfundament l,r,t,b\tflora\tlight\thousing\tsprite\tunits");
			int written = 0;
			foreach (BuildingAsset item in AssetManager.buildings.list)
			{
				string text = "-";
				string text2 = "-";
				try
				{
					item.checkSpritesAreLoaded();
					Sprite sprite = null;
					if (item.building_sprites != null && item.building_sprites.animation_data != null && item.building_sprites.animation_data.Count > 0)
					{
						BuildingAnimationData buildingAnimationData = item.building_sprites.animation_data[0];
						if (buildingAnimationData.main != null && buildingAnimationData.main.Length != 0)
						{
							sprite = buildingAnimationData.main[0];
						}
					}
					SpriteImage spriteImage = ((sprite != null) ? WorldBoxMod.Instance.SpriteImageForTest(sprite) : null);
					if (spriteImage != null)
					{
						text = $"{spriteImage.Width}x{spriteImage.Height} rows {spriteImage.OpaqueBottom}-{spriteImage.OpaqueTop}";
						text2 = $"{spriteImage.UnitWidth:F2}x{spriteImage.UnitHeight:F2}";
						Save(spriteImage, Path.Combine(dir, "b_" + item.id + ".png"), 6);
						written++;
					}
					Sprite sprite2 = ((!item.has_sprite_construction) ? null : item.building_sprites?.construction);
					SpriteImage spriteImage2 = ((sprite2 != null) ? WorldBoxMod.Instance.SpriteImageForTest(sprite2) : null);
					if (spriteImage2 != null)
					{
						Save(spriteImage2, Path.Combine(dir, "c_" + item.id + ".png"), 6);
						builds.AppendLine($"{item.id}\t{sprite2.name}\t{spriteImage2.Width}x{spriteImage2.Height} rows {spriteImage2.OpaqueBottom}-{spriteImage2.OpaqueTop}\t{spriteImage2.UnitWidth:F2}x{spriteImage2.UnitHeight:F2}\t{item.construction_progress_needed}");
					}
				}
				catch (Exception ex)
				{
					text = "error " + ex.GetType().Name;
				}
				BuildingFundament fundament = item.fundament;
				string text3 = ((fundament != null) ? $"{fundament.left},{fundament.right},{fundament.top},{fundament.bottom}" : "-");
				bool flag = false;
				try
				{
					flag = item.hasHousingSlots();
				}
				catch
				{
				}
				table.AppendLine(string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\t{11}", item.id, item.type, item.building_type, item.group, item.kingdom, item.civ_kingdom, text3, item.flora, item.draw_light_area ? item.draw_light_size.ToString("F2") : "-", flag, text, text2));
				if (written % 20 == 0)
				{
					yield return null;
				}
			}
			table.AppendLine();
			table.AppendLine("actor id\thumanoid\tciv");
			foreach (ActorAsset item2 in AssetManager.actor_library.list)
			{
				table.AppendLine($"{item2.id}\t{item2.is_humanoid}\t{item2.civ}");
			}
			File.WriteAllText(Path.Combine(dir, "assets.tsv"), table.ToString());
			File.WriteAllText(Path.Combine(dir, "construction.tsv"), builds.ToString());
			Log.Info($"ASSETS wrote {written} building sprites and the table to {dir}");
			if (quit)
			{
				Application.Quit();
			}
		}

		private static IEnumerator DumpActors(string dir)
		{
			MapBox world = World.world;
			List<WorldTile> list = new List<WorldTile>();
			List<WorldTile> list2 = new List<WorldTile>();
			for (int i = 6; i < MapBox.height - 6; i += 4)
			{
				for (int j = 6; j < MapBox.width - 6; j += 4)
				{
					WorldTile tile = world.GetTile(j, i);
					if (tile != null && tile.main_type != null)
					{
						if (!tile.main_type.liquid && !tile.main_type.lava && tile.main_type.height_min >= 98 && tile.main_type.height_min < 199 && tile.building == null)
						{
							list.Add(tile);
						}
						else if (tile.main_type.liquid && tile.main_type.ocean)
						{
							list2.Add(tile);
						}
					}
				}
			}
			List<ActorAsset> kinds = new List<ActorAsset>();
			foreach (ActorAsset item in AssetManager.actor_library.list)
			{
				string text = item.id ?? "";
				if (!text.StartsWith("zombie", StringComparison.Ordinal) || !(text != "zombie_animal_sheep") || !(text != "zombie"))
				{
					bool flag = text.StartsWith("boat_transport_", StringComparison.Ordinal) || text.StartsWith("boat_trading_", StringComparison.Ordinal);
					if ((!_allBoats || flag) && (!flag || _allBoats || text.EndsWith("_human", StringComparison.Ordinal) || text.EndsWith("_orc", StringComparison.Ordinal) || text.EndsWith("_cat", StringComparison.Ordinal)) && !(text == "god_finger") && !(text == "crabzilla"))
					{
						kinds.Add(item);
					}
				}
			}
			Dictionary<string, Actor> units = new Dictionary<string, Actor>();
			Dictionary<string, string> notes = new Dictionary<string, string>();
			int num = 0;
			int num2 = 0;
			foreach (ActorAsset item2 in kinds)
			{
				bool num3 = item2.is_boat || item2.id == "piranha";
				List<WorldTile> list3 = (num3 ? list2 : list);
				int num4 = (num3 ? num2++ : num++);
				if (list3.Count == 0)
				{
					notes[item2.id] = "no tile";
					continue;
				}
				WorldTile pTile = list3[num4 * 7 % list3.Count];
				try
				{
					Actor actor = world.units.spawnNewUnit(item2.id, pTile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, pAdultAge: true);
					if (actor == null)
					{
						notes[item2.id] = "spawn returned nothing";
						continue;
					}
					units[item2.id] = actor;
					try
					{
						actor.makeWait(30f);
					}
					catch
					{
					}
				}
				catch (Exception ex)
				{
					notes[item2.id] = "spawn error " + ex.GetType().Name + " " + ex.Message;
				}
			}
			Log.Info($"ASSETS spawned {units.Count} of {kinds.Count} creature kinds");
			yield return new WaitForSecondsRealtime(4f);
			StringBuilder table = new StringBuilder();
			table.AppendLine("id\tsprite\tsize\tflying\thovering\tboat\tboat_type\tclass\torder\tfamily\tdefault_height\trotating\tbase");
			int n = 0;
			foreach (ActorAsset a in kinds)
			{
				string value;
				string text2 = (notes.TryGetValue(a.id, out value) ? value : "-");
				if (units.TryGetValue(a.id, out var unit) && unit != null)
				{
					for (int tries = 0; tries < 6; tries++)
					{
						bool flag2;
						try
						{
							flag2 = WorldBoxMod.Instance.SpriteImageForTest(unit.getSpriteToRender()) != null;
						}
						catch
						{
							flag2 = true;
						}
						if (flag2)
						{
							break;
						}
						yield return null;
					}
					try
					{
						if (!unit.isAlive())
						{
							text2 = "died";
						}
						else
						{
							SpriteImage spriteImage = WorldBoxMod.Instance.SpriteImageForTest(unit.getSpriteToRender());
							if (spriteImage == null)
							{
								text2 = "no sprite";
							}
							else
							{
								text2 = $"{spriteImage.Width}x{spriteImage.Height} rows {spriteImage.OpaqueBottom}-{spriteImage.OpaqueTop} units {spriteImage.UnitWidth * Mathf.Abs(unit.current_scale.x):F2}x{spriteImage.UnitHeight * Mathf.Abs(unit.current_scale.y):F2}";
								Save(spriteImage, Path.Combine(dir, "a_" + a.id + ".png"), 8);
								n++;
							}
						}
					}
					catch (Exception ex2)
					{
						text2 = "sprite error " + ex2.GetType().Name;
					}
				}
				table.AppendLine($"{a.id}\t{text2}\t{a.actor_size}\t{a.flying}\t{a.hovering}\t{a.is_boat}\t{a.boat_type}\t{a.name_taxonomic_class}\t{a.name_taxonomic_order}\t{a.name_taxonomic_family}\t{a.default_height:F2}\t{a.rotating_animation}\t{a.base_asset_id}");
				yield return null;
				unit = null;
			}
			foreach (Actor value2 in units.Values)
			{
				try
				{
					if (value2 != null && value2.isAlive())
					{
						value2.dieSimpleNone();
					}
				}
				catch
				{
				}
			}
			File.WriteAllText(Path.Combine(dir, _allBoats ? "boats.tsv" : "actors.tsv"), table.ToString());
			Log.Info($"ASSETS wrote {n} creature sprites and actors.tsv");
		}

		internal static void Save(SpriteImage img, string file, int k)
		{
			Texture2D texture2D = new Texture2D(img.Width * k, img.Height * k, TextureFormat.RGBA32, mipChain: false);
			Color32[] array = new Color32[img.Width * k * img.Height * k];
			for (int i = 0; i < img.Height * k; i++)
			{
				for (int j = 0; j < img.Width * k; j++)
				{
					uint num = img.Pixels[i / k * img.Width + j / k];
					array[i * img.Width * k + j] = new Color32((byte)(num & 0xFF), (byte)((num >> 8) & 0xFF), (byte)((num >> 16) & 0xFF), (byte)(num >> 24));
				}
			}
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false);
			File.WriteAllBytes(file, texture2D.EncodeToPNG());
			UnityEngine.Object.Destroy(texture2D);
		}
	}
}
