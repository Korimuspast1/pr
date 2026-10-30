using System;
using System.Collections;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class WeaponsTest : CivilTest
	{
		private Actor _host;

		private Vector2 _home;

		private static readonly HashSet<string> UniqueGlows = new HashSet<string> { "ice_hammer", "necromancer_staff", "evil_staff", "white_staff", "plague_doctor_staff", "druid_staff" };

		protected override string Prefix => "WPN";

		protected override IEnumerator Body()
		{
			yield return Part("the 44 weapons and their models", Library());
			WorldTile worldTile = CivilTest.Stage();
			Actor king = ((worldTile != null) ? Village("human", 4, 0, worldTile) : Village("human", 4, 0));
			if (!Check("a village", king != null))
			{
				yield break;
			}
			_host = Founded.Find((Actor u) => u != null && u != king && u.isAdult());
			if (!Check("someone to be", _host != null))
			{
				yield break;
			}
			try
			{
				((BaseSimObject)_host).addStatusEffect("invincible", 900f, pColorEffect: false);
			}
			catch
			{
			}
			yield return Possess(_host);
			if (!Check("first person", InControl(_host)))
			{
				yield break;
			}
			_home = OpenGround(_host.current_position);
			_host.current_position = _home;
			foreach (Actor item in Founded)
			{
				if (item != null && item != _host && item.isAlive())
				{
					item.current_position = _home + new Vector2(14f, 9f);
					try
					{
						item.makeWait(200f);
					}
					catch
					{
					}
				}
			}
			bool gpu = Mod.Settings.GpuRender;
			int timeMode = Mod.Settings.TimeMode;
			Mod.Settings.TimeMode = 1;
			Mod.ForceRelight();
			yield return Part("in your hand, by day", InHand());
			yield return Part("bows drawn", Bows());
			yield return Part("held by others, near to far", Holders());
			yield return Part("mythril and adamantine at night", Night());
			Mod.Bow.HeldForTest = false;
			Mod.Holstered = false;
			Mod.Settings.GpuRender = gpu;
			Mod.Settings.TimeMode = timeMode;
			Mod.ForceRelight();
		}

		private static int Tris(MeshModel m)
		{
			if (m == null)
			{
				return 0;
			}
			int num = 0;
			MeshPart[] parts = m.Parts;
			foreach (MeshPart meshPart in parts)
			{
				num += meshPart.TriangleCount;
			}
			return num;
		}

		private static bool Glows(string id)
		{
			if (!id.EndsWith("_mythril", StringComparison.Ordinal) && !id.EndsWith("_adamantine", StringComparison.Ordinal))
			{
				return UniqueGlows.Contains(id);
			}
			return true;
		}

		private static List<string> AllOwn()
		{
			List<string> list = new List<string>(ViewMeshes.MetalVariants);
			list.AddRange(ViewMeshes.UniqueWeapons);
			return list;
		}

		private static float Nearest(MeshModel m, Vec3 at, out int part, out int index)
		{
			part = (index = -1);
			float num = float.MaxValue;
			for (int i = 0; i < m.Parts.Length; i++)
			{
				float[] pos = m.Parts[i].Pos;
				for (int j = 0; j + 2 < pos.Length; j += 3)
				{
					float num2 = pos[j] - at.X;
					float num3 = pos[j + 1] - at.Y;
					float num4 = pos[j + 2] - at.Z;
					float num5 = num2 * num2 + num3 * num3 + num4 * num4;
					if (num5 < num)
					{
						num = num5;
						part = i;
						index = j;
					}
				}
			}
			if (part >= 0)
			{
				return Mathf.Sqrt(num);
			}
			return float.MaxValue;
		}

		private static Vec3 Vertex(MeshModel m, int part, int index)
		{
			return new Vec3(m.Parts[part].Pos[index], m.Parts[part].Pos[index + 1], m.Parts[part].Pos[index + 2]);
		}

		private static float Dist(Vec3 a, Vec3 b)
		{
			return Mathf.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
		}

		private IEnumerator Library()
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			int num5 = ViewMeshes.MetalVariants.Length;
			Check("44 weapons with models of their own", num5 == 44, num5.ToString());
			string[] metalVariants = ViewMeshes.MetalVariants;
			foreach (string text in metalVariants)
			{
				List<string> list = new List<string>();
				bool flag = false;
				try
				{
					flag = AssetManager.items.get(text) != null;
				}
				catch
				{
				}
				if (flag)
				{
					num++;
				}
				else
				{
					list.Add("no such item in the game");
				}
				WeaponLook look = ViewMeshes.LookOf(text, null);
				if (look.Model == "held_" + text)
				{
					num2++;
				}
				else
				{
					list.Add("look " + (look.Model ?? "null"));
				}
				MeshModel meshModel = Models.Get("held_" + text);
				MeshModel meshModel2 = Models.Get("held_" + text + "_m");
				MeshModel meshModel3 = Models.Get("held_" + text + "_f");
				if (meshModel != null && meshModel2 != null && meshModel3 != null)
				{
					num3++;
				}
				else
				{
					list.Add("missing" + ((meshModel == null) ? " near" : "") + ((meshModel2 == null) ? " _m" : "") + ((meshModel3 == null) ? " _f" : ""));
				}
				int num6 = Tris(meshModel);
				int num7 = Tris(meshModel2);
				int num8 = Tris(meshModel3);
				if (num6 >= 400 && num6 <= 2410 && num7 >= 120 && num7 <= 450 && num8 >= 24 && num8 <= 120)
				{
					num4++;
				}
				else
				{
					list.Add($"triangles {num6}/{num7}/{num8} (budgets near 400-1800, ceiling 2410; mid 120-450; far 24-120)");
				}
				if (meshModel != null && (ViewMeshes.Item(look) != meshModel || (meshModel2 != null && ViewMeshes.Item(look, 1) != meshModel2) || (meshModel3 != null && ViewMeshes.Item(look, 2) != meshModel3)))
				{
					list.Add("not the library's models drawn");
				}
				string text2 = "";
				if (meshModel != null)
				{
					string text3 = meshModel.Info("kind");
					string text4 = meshModel.Info("material");
					string text5 = meshModel.Info("tip");
					string text6 = text.Substring(0, text.IndexOf('_'));
					string text7 = text.Substring(text.IndexOf('_') + 1);
					if (text3 != text6)
					{
						list.Add("info kind=" + (text3 ?? "-"));
					}
					if (text4 != text7)
					{
						list.Add("info material=" + (text4 ?? "-"));
					}
					if (string.IsNullOrEmpty(text5))
					{
						list.Add("no info tip");
					}
					if (meshModel.Parts.Length != 1 || meshModel.Parts[0].Name != "item")
					{
						list.Add($"{meshModel.Parts.Length} parts" + ((meshModel.Parts.Length != 0) ? (", first " + meshModel.Parts[0].Name) : ""));
					}
					if (Glows(text) && look.GlowLight == 0)
					{
						list.Add("no info glow");
					}
					if (!Glows(text) && look.GlowLight != 0)
					{
						list.Add("a glow it shouldn't have");
					}
					text2 = ", kind " + text3 + ", material " + text4 + ", tip " + text5 + ((look.GlowLight != 0) ? $", glow #{look.GlowLight & 0xFFFFFF:X6}" : "");
					if (text.StartsWith("bow_", StringComparison.Ordinal))
					{
						text2 += BowFacts(text, look, meshModel, meshModel2, meshModel3, list);
					}
				}
				if (num6 > 1800)
				{
					text2 += " (near over the 1800 aimed at)";
				}
				Check(text, list.Count == 0, $"{num6}/{num7}/{num8} triangles{text2}" + ((list.Count > 0) ? ("; " + string.Join("; ", list)) : ""));
			}
			Note($"summary: items {num}/{num5}, looks {num2}/{num5}, models {num3}/{num5} (x3), budgets {num4}/{num5}");
			int num9 = 0;
			metalVariants = ViewMeshes.UniqueWeapons;
			foreach (string text8 in metalVariants)
			{
				List<string> list2 = new List<string>();
				bool flag2 = false;
				try
				{
					flag2 = AssetManager.items.get(text8) != null;
				}
				catch
				{
				}
				if (!flag2)
				{
					list2.Add("no such item in the game");
				}
				WeaponLook look2 = ViewMeshes.LookOf(text8, null);
				if (look2.Model != "held_" + text8)
				{
					list2.Add("look " + (look2.Model ?? "null"));
				}
				MeshModel meshModel4 = Models.Get("held_" + text8);
				MeshModel meshModel5 = Models.Get("held_" + text8 + "_m");
				MeshModel meshModel6 = Models.Get("held_" + text8 + "_f");
				if (meshModel4 == null || meshModel5 == null || meshModel6 == null)
				{
					list2.Add("missing" + ((meshModel4 == null) ? " near" : "") + ((meshModel5 == null) ? " _m" : "") + ((meshModel6 == null) ? " _f" : ""));
				}
				int num10 = Tris(meshModel4);
				int num11 = Tris(meshModel5);
				int num12 = Tris(meshModel6);
				if (num10 < 400 || num10 > 2410 || num11 < 120 || num11 > 450 || num12 < 24 || num12 > 120)
				{
					list2.Add($"triangles {num10}/{num11}/{num12} (budgets near 400-2410, mid 120-450, far 24-120)");
				}
				if (meshModel4 != null && (ViewMeshes.Item(look2) != meshModel4 || (meshModel5 != null && ViewMeshes.Item(look2, 1) != meshModel5) || (meshModel6 != null && ViewMeshes.Item(look2, 2) != meshModel6)))
				{
					list2.Add("not the library's models drawn");
				}
				string text9 = "";
				if (meshModel4 != null)
				{
					string text10 = meshModel4.Info("kind");
					string text11 = meshModel4.Info("item");
					string text12 = meshModel4.Info("tip");
					string text13 = ((look2.Kind == WeaponKind.Staff) ? "staff" : ((look2.Kind == WeaponKind.Hammer) ? "hammer" : ((look2.Kind == WeaponKind.Stick) ? "stick" : ((look2.Kind == WeaponKind.Gun) ? "gun" : "?"))));
					if (text10 != text13)
					{
						list2.Add(string.Format("info kind={0} (the game's lookup: {1})", text10 ?? "-", look2.Kind));
					}
					if (text11 != text8)
					{
						list2.Add("info item=" + (text11 ?? "-"));
					}
					if (string.IsNullOrEmpty(text12))
					{
						list2.Add("no info tip");
					}
					if (meshModel4.Parts.Length != 1 || meshModel4.Parts[0].Name != "item")
					{
						list2.Add($"{meshModel4.Parts.Length} parts");
					}
					if (Glows(text8) && look2.GlowLight == 0)
					{
						list2.Add("no info glow");
					}
					if (!Glows(text8) && look2.GlowLight != 0)
					{
						list2.Add("a glow it shouldn't have");
					}
					if (text8 == "flame_hammer" && (!FlameSword.IsModel(meshModel4) || !FlameSword.IsModel(meshModel5) || !FlameSword.IsModel(meshModel6)))
					{
						list2.Add("doesn't burn (FlameSword.IsModel)");
					}
					text9 = ", kind " + text10 + ", tip " + text12 + ((look2.GlowLight != 0) ? $", glow #{look2.GlowLight & 0xFFFFFF:X6}" : "");
				}
				if (list2.Count == 0)
				{
					num9++;
				}
				Check(text8, list2.Count == 0, $"{num10}/{num11}/{num12} triangles{text9}" + ((list2.Count > 0) ? ("; " + string.Join("; ", list2)) : ""));
			}
			Note($"unique weapons: {num9}/{ViewMeshes.UniqueWeapons.Length} with their own models");
			WeaponLook look3 = ViewMeshes.LookOf("flame_sword", null);
			Check("the flame sword keeps its own model at every detail", FlameSword.IsModel(ViewMeshes.Item(look3)) && FlameSword.IsModel(ViewMeshes.Item(look3, 2)), ViewMeshes.Item(look3, 2)?.Name);
			MeshModel meshModel7 = ViewMeshes.Item(ViewMeshes.LookOf("dagger_test", null), 1);
			Check("a weapon with no model of its own is still made here", meshModel7 != null && meshModel7.Name == "held_Dagger", meshModel7?.Name);
			WeaponLook weaponLook = new WeaponLook
			{
				Kind = WeaponKind.Bow,
				Model = "held_bow_mythril"
			};
			Check("a look's bowstring and light read from its model when asked", weaponLook.String != 0 && weaponLook.StringGlow && weaponLook.GlowLight != 0, $"string #{weaponLook.String & 0xFFFFFF:X6}, glows {weaponLook.StringGlow}, light #{weaponLook.GlowLight & 0xFFFFFF:X6}");
			yield break;
		}

		private static string BowFacts(string id, WeaponLook look, MeshModel near, MeshModel middle, MeshModel far, List<string> wrong)
		{
			if (look.String == 0)
			{
				wrong.Add("no info string");
			}
			if (Glows(id) && !look.StringGlow)
			{
				wrong.Add("its string doesn't glow");
			}
			MeshModel meshModel = ViewMeshes.BowString(-0.1f, 0f, arrow: false, look.Head, look.String, look.StringGlow);
			int num = meshModel.SlotIndex("string");
			if (num < 0 || (meshModel.SlotColor[num] & 0xFFFFFF) != (look.String & 0xFFFFFF) || (meshModel.SlotFlags[num] & 1) != 0 != look.StringGlow)
			{
				wrong.Add("the string isn't drawn in its colour");
			}
			string result = string.Format(", string #{0:X6}{1}", look.String & 0xFFFFFF, look.StringGlow ? " lit" : "");
			if (ViewMeshes.Bow(look, 0f) != near)
			{
				wrong.Add("at rest not its own model");
			}
			MeshModel meshModel2 = ViewMeshes.Bow(look, 1f);
			if (meshModel2 == near || meshModel2.Name != near.Name || ViewMeshes.Bow(look, 1f) != meshModel2)
			{
				wrong.Add("drawn, not a kept bent copy");
			}
			MeshModel[] array = new MeshModel[3] { near, middle, far };
			foreach (MeshModel meshModel3 in array)
			{
				if (meshModel3 == null)
				{
					continue;
				}
				string text = ((meshModel3 == near) ? "near" : ((meshModel3 == middle) ? "mid" : "far"));
				MeshModel meshModel4 = ViewMeshes.BendBow(meshModel3, 0.5f);
				MeshModel meshModel5 = ViewMeshes.BendBow(meshModel3, 1f);
				for (int j = -1; j <= 1; j += 2)
				{
					Vec3 vec = new Vec3(0f, ViewMeshes.BowTip.Y, (float)j * ViewMeshes.BowTip.Z);
					int part;
					int index;
					float num2 = Nearest(meshModel3, vec, out part, out index);
					if (num2 > 0.008f)
					{
						wrong.Add($"{text}: no nock near {vec} ({num2:F3} off)");
						continue;
					}
					for (int k = 0; k < 2; k++)
					{
						float num3 = ((k == 0) ? 0.5f : 1f);
						Vec3 vec2 = ViewMeshes.BowTipAt(num3);
						float num4 = Dist(b: new Vec3(vec2.X, vec2.Y, (float)j * vec2.Z), a: Vertex((k == 0) ? meshModel4 : meshModel5, part, index));
						if (num4 > num2 * 1.4f + 0.0005f)
						{
							wrong.Add($"{text}: drawn {num3:F1}, its nock {num4:F4} from the string's end (was {num2:F4} from the nock)");
						}
					}
				}
			}
			return result;
		}

		private static Vector2 OpenGround(Vector2 from)
		{
			for (float num = 9f; num < 20f; num += 1.5f)
			{
				for (int i = 0; i < 16; i++)
				{
					float f = (float)i * MathF.PI / 8f;
					Vector2 vector = from + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num;
					bool flag = true;
					for (int j = -3; j <= 3 && flag; j++)
					{
						for (int k = -3; k <= 3 && flag; k++)
						{
							WorldTile tile = World.world.GetTile((int)vector.x + k, (int)vector.y + j);
							if (tile == null || tile.main_type == null || tile.main_type.liquid || tile.main_type.block)
							{
								flag = false;
							}
							else if (tile.building != null && AssetStyle.IsSolid(tile.building) && tile.building.asset.building_type != BuildingType.Building_Tree && tile.building.asset.building_type != BuildingType.Building_Mineral && tile.building.asset.building_type != BuildingType.Building_Plant)
							{
								flag = false;
							}
						}
					}
					if (flag)
					{
						return new Vector2((float)(int)vector.x + 0.5f, (float)(int)vector.y + 0.5f);
					}
				}
			}
			return from;
		}

		private static bool Dry(Vector2 p)
		{
			WorldTile tile = World.world.GetTile((int)p.x, (int)p.y);
			if (tile != null && tile.main_type != null && !tile.main_type.liquid)
			{
				return !tile.main_type.block;
			}
			return false;
		}

		private static Vector2 Dir(float yaw)
		{
			return new Vector2(Mathf.Cos(yaw), Mathf.Sin(yaw));
		}

		private static float LandAhead(Vector2 from, float yaw)
		{
			for (int i = 0; i < 32; i++)
			{
				float num = yaw + (((i & 1) == 0) ? 1f : (-1f)) * (float)((i + 1) / 2) * MathF.PI / 16f;
				Vector2 vector = Dir(num);
				Vector2 vector2 = new Vector2(0f - vector.y, vector.x);
				bool flag = true;
				float[] array = new float[3] { 5f, 20f, 40f };
				foreach (float num2 in array)
				{
					if (!Dry(from + vector * num2) || !Dry(from + vector * num2 + vector2 * 1.6f))
					{
						flag = false;
						break;
					}
				}
				if (flag)
				{
					return num;
				}
			}
			return yaw;
		}

		private bool Equip(Actor a, string id)
		{
			try
			{
				ActorEquipmentSlot slot = a.equipment.getSlot(EquipmentType.Weapon);
				if (slot.getItem() != null)
				{
					slot.takeAwayItem();
				}
				EquipmentAsset equipmentAsset = AssetManager.items.get(id);
				if (equipmentAsset == null)
				{
					return false;
				}
				Item pItem = World.world.items.generateItem(equipmentAsset, a.kingdom, a.getName(), 1, a, 0, pByPlayer: true);
				slot.setItem(pItem, a);
				a.setStatsDirty();
				return true;
			}
			catch (Exception ex)
			{
				Note("equip " + id + ": " + ex.Message);
				return false;
			}
		}

		private IEnumerator Hold(float seconds)
		{
			float t0 = Time.unscaledTime;
			while (Time.unscaledTime - t0 < seconds)
			{
				if (_host != null)
				{
					_host.current_position = _home;
				}
				yield return null;
			}
		}

		private MeshModel HeldDrawn()
		{
			foreach (MeshInstance overlayMesh in Mod.Renderer.OverlayMeshes)
			{
				if (overlayMesh != null && overlayMesh.Model != null && overlayMesh.Model.Name != null && overlayMesh.Model.Name.StartsWith("held_", StringComparison.Ordinal))
				{
					return overlayMesh.Model;
				}
			}
			return null;
		}

		private IEnumerator InHand()
		{
			Mod.Holstered = false;
			Mod.DebugEyeSet = false;
			Mod.SetPitchDegrees(4f);
			List<string> wrong = new List<string>();
			int own = 0;
			float slowest = 0f;
			List<string> all = AllOwn();
			foreach (string id in all)
			{
				if (!Equip(_host, id))
				{
					wrong.Add(id + " (not equipped)");
					continue;
				}
				yield return Hold(0.75f);
				MeshModel meshModel = HeldDrawn();
				string text = ((id == "alien_blaster") ? "held_blaster" : ("held_" + id));
				if (meshModel != null && meshModel.Name == text)
				{
					own++;
				}
				else
				{
					wrong.Add(id + " (" + (meshModel?.Name ?? "nothing") + ")");
				}
				slowest = Mathf.Max(slowest, Mod.FrameMs);
				Mod.SaveFrame("weapons_fp_" + id + ".png");
			}
			Check("each drawn in your hand with its own model", wrong.Count == 0, $"{own}/{all.Count}, slowest frame {slowest:F1} ms" + ((wrong.Count > 0) ? (": " + string.Join(", ", wrong)) : ""));
			if (Equip(_host, "flame_hammer"))
			{
				yield return Hold(0.75f);
				int flames = 0;
				for (int i = 0; i < 6; i++)
				{
					flames = Math.Max(flames, Mod.FlameSpritesForTest);
					_host.current_position = _home;
					yield return null;
				}
				Check("the flame hammer burns in your hand", Mod.HandsForTest.FlameShown && flames >= 7, $"{flames} flames");
			}
			bool gpu = Mod.Settings.GpuRender;
			Mod.Settings.GpuRender = !gpu;
			string other = (gpu ? "cpu" : "gpu");
			int shown = 0;
			string[] array = new string[5] { "sword_mythril", "axe_adamantine", "spear_silver", "hammer_bronze", "bow_steel" };
			foreach (string id in array)
			{
				if (Equip(_host, id))
				{
					yield return Hold(0.8f);
					MeshModel meshModel2 = HeldDrawn();
					if (meshModel2 != null && meshModel2.Name == "held_" + id)
					{
						shown++;
					}
					Mod.SaveFrame("weapons_fp_" + id + "_" + other + ".png");
				}
			}
			Mod.Settings.GpuRender = gpu;
			Check("and on the " + (gpu ? "CPU" : "graphics card"), shown == 5, $"{shown}/5");
		}

		private IEnumerator Bows()
		{
			Mod.Holstered = false;
			Mod.DebugEyeSet = false;
			Mod.SetPitchDegrees(6f);
			BowControl bow = Mod.Bow;
			string[] metalVariants = ViewMeshes.MetalVariants;
			foreach (string id in metalVariants)
			{
				if (id.StartsWith("bow_", StringComparison.Ordinal) && Check(id + " in hand", Equip(_host, id)))
				{
					bow.HeldForTest = false;
					bow.State.Pose(BowState.Phase.Ready, 0f, 0f);
					yield return Hold(0.9f);
					MeshModel rest = HeldDrawn();
					Mod.SaveFrame("weapons_bow_" + id + "_0.png");
					bow.HeldForTest = true;
					float t0 = Time.unscaledTime;
					while (Time.unscaledTime - t0 < 0.5f)
					{
						_host.current_position = _home;
						bow.State.Pose(BowState.Phase.Drawing, 0.233f, 0.5f);
						yield return null;
					}
					MeshModel half = HeldDrawn();
					float halfDraw = bow.State.Draw;
					Mod.SaveFrame("weapons_bow_" + id + "_50.png");
					t0 = Time.unscaledTime;
					while (Time.unscaledTime - t0 < 0.5f)
					{
						_host.current_position = _home;
						bow.State.Pose(BowState.Phase.Held, 0.2f, 1f);
						yield return null;
					}
					MeshModel meshModel = HeldDrawn();
					Mod.SaveFrame("weapons_bow_" + id + "_100.png");
					bow.State.Pose(BowState.Phase.Ready, 0f, 0f);
					bow.HeldForTest = false;
					bool ok = rest != null && rest.Name == "held_" + id && half != null && meshModel != null && half != rest && meshModel != rest && meshModel != half && half.Name == rest.Name && meshModel.Name == rest.Name;
					Check(id + " bends as it is drawn", ok, string.Format("at rest {0}, half ({1:F2}) {2}, full {3}", rest?.Name, halfDraw, (half == rest) ? "the same" : "bent", (meshModel == rest || meshModel == half) ? "the same" : "bent") + ((meshModel != null) ? $", drawn reaching y {meshModel.Min.Y:F3} (at rest {rest?.Min.Y:F3})" : ""));
					yield return Hold(0.3f);
				}
			}
		}

		private IEnumerator Holders()
		{
			Mod.Holstered = true;
			Mod.DebugEyeSet = false;
			_host.current_position = _home;
			yield return Hold(0.4f);
			Vector2 eye = new Vector2(Mod.EyeForTest.x, Mod.EyeForTest.y);
			float yaw = LandAhead(eye, Mod.ViewYaw);
			Vector2 d = Dir(yaw);
			Vector2 side = new Vector2(0f - d.y, d.x);
			Actor man = CivilTest.Spawn("human", eye + d * 5f);
			Actor archer = CivilTest.Spawn("human", eye + d * 5f + side * 1.6f);
			Actor mage = CivilTest.Spawn("human", eye + d * 5f - side * 1.6f);
			Actor gunner = CivilTest.Spawn("human", eye + d * 5f - side * 3.2f);
			if (!Check("four people in front of you", man != null && archer != null && mage != null && gunner != null))
			{
				CivilTest.Kill(man);
				CivilTest.Kill(archer);
				CivilTest.Kill(mage);
				CivilTest.Kill(gunner);
				yield break;
			}
			Check("a steel sword, a mythril bow, an evil staff, an alien blaster", Equip(man, "sword_steel") && Equip(archer, "bow_mythril") && Equip(mage, "evil_staff") && Equip(gunner, "alien_blaster"));
			float[] at = new float[3] { 5f, 20f, 40f };
			string[] suffix = new string[3] { "", "_m", "_f" };
			string[] level = new string[3] { "near", "middle", "far" };
			for (int k = 0; k < at.Length; k++)
			{
				for (int i = 0; i < 130; i++)
				{
					_host.current_position = _home;
					man.current_position = eye + d * at[k];
					archer.current_position = eye + d * at[k] + side * 1.6f;
					mage.current_position = eye + d * at[k] - side * 1.6f;
					gunner.current_position = eye + d * at[k] - side * 3.2f;
					Mod.SetYaw(yaw);
					Mod.SetPitchDegrees((k == 0) ? (-8f) : (-2f));
					if (i % 15 == 0)
					{
						try
						{
							man.makeWait(30f);
							archer.makeWait(30f);
							mage.makeWait(30f);
							gunner.makeWait(30f);
						}
						catch
						{
						}
					}
					if (k == 0 && i == 100)
					{
						Mod.SaveFrame("weapons_holders_near.png");
					}
					yield return null;
				}
				int detail;
				string text = People.WeaponForTest(man, out detail);
				int detail2;
				string text2 = People.WeaponForTest(archer, out detail2);
				Check($"a sword {at[k]:F0} tiles off: its {level[k]} model", detail == k && text == "held_sword_steel" + suffix[k], string.Format("detail {0}, drawn {1}", detail, text ?? "nothing"));
				Check($"a bow {at[k]:F0} tiles off: its {level[k]} model", detail2 == k && text2 == "held_bow_mythril" + suffix[k], string.Format("detail {0}, drawn {1}", detail2, text2 ?? "nothing"));
				int detail3;
				string text3 = People.WeaponForTest(mage, out detail3);
				int detail4;
				string text4 = People.WeaponForTest(gunner, out detail4);
				Check($"a staff {at[k]:F0} tiles off: its {level[k]} model", detail3 == k && text3 == "held_evil_staff" + suffix[k], string.Format("detail {0}, drawn {1}", detail3, text3 ?? "nothing"));
				Check($"a blaster {at[k]:F0} tiles off: its {level[k]} model", detail4 == k && text4 == "held_alien_blaster" + suffix[k], string.Format("detail {0}, drawn {1}", detail4, text4 ?? "nothing"));
			}
			CivilTest.Kill(man);
			CivilTest.Kill(archer);
			CivilTest.Kill(mage);
			CivilTest.Kill(gunner);
			Mod.Holstered = false;
		}

		private IEnumerator Night()
		{
			Mod.Holstered = false;
			Mod.DebugEyeSet = false;
			Mod.SetPitchDegrees(-6f);
			Mod.Settings.TimeMode = 1;
			Mod.ForceRelight();
			Equip(_host, "sword_mythril");
			yield return Hold(1f);
			Check("no light of its own by day", Mod.WeaponGlowsForTest == 0, $"{Mod.WeaponGlowsForTest} lights");
			Mod.Settings.TimeMode = 3;
			Mod.ForceRelight();
			yield return Hold(1.5f);
			List<string> dark = new List<string>();
			int lit = 0;
			int lights;
			foreach (string id in AllOwn())
			{
				if (!Glows(id) && id != "flame_hammer")
				{
					continue;
				}
				if (!Equip(_host, id))
				{
					dark.Add(id + " (not equipped)");
					continue;
				}
				yield return Hold(0.8f);
				lights = 0;
				for (int i = 0; i < 6; i++)
				{
					lights = Math.Max(lights, (id == "flame_hammer") ? Mod.FlameLightsForTest : Mod.WeaponGlowsForTest);
					_host.current_position = _home;
					yield return null;
				}
				if (lights >= 1)
				{
					lit++;
				}
				else
				{
					dark.Add(id);
				}
				Mod.SaveFrame("weapons_night_" + id + ".png");
			}
			Check("mythril, adamantine, the staffs and the magic hammers light the night round them", dark.Count == 0, $"{lit} lit" + ((dark.Count > 0) ? (", dark: " + string.Join(", ", dark)) : ""));
			Mod.Holstered = true;
			yield return Hold(0.3f);
			Vector2 eye = new Vector2(Mod.EyeForTest.x, Mod.EyeForTest.y);
			float yaw = LandAhead(eye, Mod.ViewYaw);
			Actor man = CivilTest.Spawn("human", eye + Dir(yaw) * 5f);
			if (!Check("someone in front of you at night", man != null))
			{
				Mod.Holstered = false;
				yield break;
			}
			Equip(man, "axe_adamantine");
			int others = 0;
			lights = 0;
			int detail;
			while (lights < 110)
			{
				_host.current_position = _home;
				man.current_position = eye + Dir(yaw) * 5f;
				Mod.SetYaw(yaw);
				Mod.SetPitchDegrees(-8f);
				if (lights % 15 == 0)
				{
					try
					{
						man.makeWait(30f);
					}
					catch
					{
					}
				}
				if (lights > 60)
				{
					others = Math.Max(others, Mod.WeaponGlowOthersForTest);
				}
				if (lights == 90)
				{
					Mod.SaveFrame("weapons_night_holder.png");
				}
				yield return null;
				detail = lights++;
			}
			Check("someone's adamantine axe lights the night round them", others >= 1, string.Format("{0} lights; drawn {1}", others, People.WeaponForTest(man, out detail) ?? "nothing"));
			CivilTest.Kill(man);
			Mod.Holstered = false;
		}
	}
}
