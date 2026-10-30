using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class WarReelTest : CivilTest
	{
		private const int Fps = 30;

		private string _dir;

		private readonly List<Actor> _men = new List<Actor>();

		private readonly List<Actor> _orcs = new List<Actor>();

		private Vector2 _centre;

		private Vector2 _dir2;

		private Vector2 _side;

		private Kingdom _humanK;

		private Kingdom _orcK;

		private City _humanCity;

		private City _orcCity;

		private static readonly string[] HumanArms = new string[8] { "sword_steel", "sword_iron", "spear_iron", "axe_iron", "sword_steel", "hammer_iron", "bow_wood", "spear_steel" };

		private static readonly string[] OrcArms = new string[7] { "axe_iron", "sword_iron", "spear_iron", "hammer_iron", "axe_steel", "bow_wood", "sword_bronze" };

		protected override string Prefix => "WREEL";

		protected override IEnumerator Body()
		{
			Settings s = Mod.Settings;
			string settingsFile = Path.Combine(Log.Folder, "settings.txt");
			string savedSettings = null;
			try
			{
				savedSettings = (File.Exists(settingsFile) ? File.ReadAllText(settingsFile) : null);
			}
			catch
			{
			}
			int savedRes = s.Resolution;
			int savedTime = s.TimeMode;
			float savedDraw = s.DrawDistance;
			float savedZoom = s.GodViewZoom;
			s.Resolution = WorldBoxMod.ResolutionWidths.Length - 1;
			s.TimeMode = 1;
			s.DrawDistance = 600f;
			s.GodViewZoom = 40f;
			_dir = Path.Combine(Log.Folder, "warreel");
			try
			{
				if (Directory.Exists(_dir))
				{
					Directory.Delete(_dir, recursive: true);
				}
			}
			catch
			{
			}
			Directory.CreateDirectory(_dir);
			Note(string.Format("screen {0}x{1}, renderer {2}", Screen.width, Screen.height, s.GpuRender ? "GPU" : "CPU"));
			yield return Part("armies", Armies());
			if (_men.Count > 0 && _orcs.Count > 0)
			{
				yield return Part("flyover", FlyClip());
				yield return Part("god view", GodClip());
				yield return Part("low pass", LowClip());
				yield return Part("in the melee", MeleeClip());
			}
			Time.captureFramerate = 0;
			PossessionHooks.ScriptedInput = Vector2.zero;
			s.Resolution = savedRes;
			s.TimeMode = savedTime;
			s.DrawDistance = savedDraw;
			s.GodViewZoom = savedZoom;
			Mod.NoDive = true;
			if (savedSettings != null)
			{
				try
				{
					File.WriteAllText(settingsFile, savedSettings);
				}
				catch
				{
				}
			}
		}

		private IEnumerator Armies()
		{
			Actor actor = Village("human", 10, 2);
			if (!Check("a human kingdom", actor != null))
			{
				yield break;
			}
			WorldTile current_tile = actor.current_tile;
			Actor actor2 = Village("orc", 10, 2, current_tile, 40f);
			if (!Check("an orc kingdom nearby", actor2 != null))
			{
				yield break;
			}
			_humanK = actor.kingdom;
			_orcK = actor2.kingdom;
			_humanCity = actor.city;
			_orcCity = actor2.city;
			try
			{
				World.world.diplomacy.startWar(_orcK, _humanK, WarTypeLibrary.normal, pLog: false);
			}
			catch (Exception ex)
			{
				Note("war: " + ex.Message);
			}
			Check("at war", Royal.AtWar(_humanK, _orcK));
			Vector2 current_position = actor.current_position;
			Vector2 current_position2 = actor2.current_position;
			Vector2 at = (current_position + current_position2) * 0.5f;
			_centre = OpenField(at) ?? OpenField(current_position + (current_position2 - current_position) * 0.35f) ?? Vec(CivilTest.Stage());
			_dir2 = (((current_position2 - current_position).sqrMagnitude > 1f) ? (current_position2 - current_position).normalized : Vector2.right);
			_side = new Vector2(0f - _dir2.y, _dir2.x);
			Note($"towns {current_position} and {current_position2}, field at {_centre}");
			CivilTest.ClearBox((int)_centre.x - 26, (int)_centre.y - 26, (int)_centre.x + 26, (int)_centre.y + 26);
			yield return CivilTest.Wait(2f);
			Raise(_men, "human", _humanCity, HumanArms, _centre - _dir2 * 10f, -1f, 60);
			Raise(_orcs, "orc", _orcCity, OrcArms, _centre + _dir2 * 10f, 1f, 60);
			Check("the armies raised", _men.Count >= 40 && _orcs.Count >= 40, $"{_men.Count} men, {_orcs.Count} orcs");
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
		}

		private Vector2? OpenField(Vector2 at)
		{
			Vector2? result = null;
			int num = int.MaxValue;
			for (float num2 = 0f; num2 < 40f; num2 += 3f)
			{
				for (int i = 0; i < 12; i++)
				{
					Vector2 value = at + new Vector2(Mathf.Cos((float)i * MathF.PI / 6f), Mathf.Sin((float)i * MathF.PI / 6f)) * num2;
					int num3 = 0;
					int num4 = 0;
					for (int j = -16; j <= 16; j += 2)
					{
						for (int k = -16; k <= 16; k += 2)
						{
							WorldTile tile = World.world.GetTile((int)value.x + k, (int)value.y + j);
							if (tile == null || tile.main_type == null || tile.main_type.liquid || tile.main_type.block || tile.Type.lava)
							{
								num3++;
							}
							else if (tile.building != null && tile.building.asset != null && tile.building.asset.building_type == BuildingType.Building_Tree)
							{
								num4++;
							}
						}
					}
					if (num3 <= 3)
					{
						int num5 = num4 * 3 + (int)num2;
						if (num5 < num)
						{
							num = num5;
							result = value;
						}
					}
				}
			}
			if (result.HasValue)
			{
				Note($"field score {num}");
			}
			return result;
		}

		private static Vector2 Vec(WorldTile t)
		{
			if (t == null)
			{
				return new Vector2((float)MapBox.width * 0.5f, (float)MapBox.height * 0.5f);
			}
			return new Vector2((float)t.x + 0.5f, (float)t.y + 0.5f);
		}

		private void Raise(List<Actor> into, string species, City city, string[] arms, Vector2 front, float back, int count)
		{
			string[] array = new string[5] { "iron", "steel", "bronze", "iron", "leather" };
			for (int i = 0; i < count; i++)
			{
				int num = i / 10;
				int num2 = i % 10;
				Vector2 at = front + _dir2 * (back * (float)num * 1.4f) + _side * (((float)num2 - 4.5f) * 1.3f);
				Actor actor = CivilTest.SpawnNear(species, at);
				if (actor == null)
				{
					continue;
				}
				try
				{
					if (actor.city != city)
					{
						actor.joinCity(city);
					}
					city.makeWarrior(actor);
				}
				catch (Exception ex)
				{
					Note("warrior: " + ex.Message);
				}
				Equip(actor, EquipmentType.Weapon, arms[i % arms.Length]);
				string text = array[i / 3 % array.Length];
				Equip(actor, EquipmentType.Armor, "armor_" + text);
				Equip(actor, EquipmentType.Helmet, "helmet_" + text);
				if (i % 2 == 0)
				{
					Equip(actor, EquipmentType.Boots, "boots_" + text);
				}
				into.Add(actor);
			}
		}

		private static void Equip(Actor unit, EquipmentType type, string id)
		{
			try
			{
				EquipmentAsset equipmentAsset = AssetManager.items.get(id);
				if (equipmentAsset != null)
				{
					ActorEquipmentSlot slot = unit.equipment.getSlot(type);
					if (slot.getItem() != null)
					{
						slot.takeAwayItem();
					}
					slot.setItem(World.world.items.generateItem(equipmentAsset, unit.kingdom, unit.getName(), 1, unit, 0, pByPlayer: true), unit);
					unit.setStatsDirty();
				}
			}
			catch
			{
			}
		}

		private int Alive(List<Actor> list)
		{
			int num = 0;
			foreach (Actor item in list)
			{
				if (item != null && item.isAlive())
				{
					num++;
				}
			}
			return num;
		}

		private Vector2 Front()
		{
			Vector2 zero = Vector2.zero;
			int num = 0;
			foreach (Actor man in _men)
			{
				if (man == null || !man.isAlive())
				{
					continue;
				}
				foreach (Actor orc in _orcs)
				{
					if (orc != null && orc.isAlive() && (orc.current_position - man.current_position).sqrMagnitude < 9f)
					{
						zero += man.current_position + orc.current_position;
						num += 2;
						break;
					}
				}
			}
			if (num >= 4)
			{
				return zero / num;
			}
			Vector2 vector = Mean(_men, _centre - _dir2 * 4f);
			Vector2 vector2 = Mean(_orcs, _centre + _dir2 * 4f);
			return (vector + vector2) * 0.5f;
		}

		private void Reinforce(int below, int count)
		{
			Vector2 vector = Front();
			if (Alive(_men) < below)
			{
				Raise(_men, "human", _humanCity, HumanArms, vector - _dir2 * 8f, -1f, count);
			}
			if (Alive(_orcs) < below)
			{
				Raise(_orcs, "orc", _orcCity, OrcArms, vector + _dir2 * 8f, 1f, count);
			}
			Note($"reinforced: {Alive(_men)} men, {Alive(_orcs)} orcs");
		}

		private static Vector2 Mean(List<Actor> list, Vector2 fallback)
		{
			Vector2 zero = Vector2.zero;
			int num = 0;
			foreach (Actor item in list)
			{
				if (item != null && item.isAlive())
				{
					zero += item.current_position;
					num++;
				}
			}
			if (num <= 0)
			{
				return fallback;
			}
			return zero / num;
		}

		private static bool DryAt(Vector2 p)
		{
			WorldTile tile = World.world.GetTile((int)p.x, (int)p.y);
			if (tile != null && tile.main_type != null && !tile.main_type.liquid)
			{
				return !tile.main_type.block;
			}
			return false;
		}

		private float Ground(float x, float y)
		{
			TerrainCache land = Mod.Land;
			if (land == null || land.Width <= 0)
			{
				return 0f;
			}
			return Mathf.Max(land.SampleHeight(x, y), 0f);
		}

		private void Hour(int mode)
		{
			Mod.Settings.TimeMode = mode;
			Mod.ForceRelight();
		}

		private string Clip(string name)
		{
			string text = Path.Combine(_dir, name);
			Directory.CreateDirectory(text);
			return text;
		}

		private void Frame(string dir, int k)
		{
			byte[] array = Mod.ShownPngForTest();
			if (array == null)
			{
				return;
			}
			Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
			try
			{
				if (texture2D.LoadImage(array))
				{
					File.WriteAllBytes(Path.Combine(dir, $"f_{k:0000}.jpg"), texture2D.EncodeToJPG(93));
				}
				if (k == 0)
				{
					Note($"{Path.GetFileName(dir)} frames {texture2D.width}x{texture2D.height}");
				}
			}
			finally
			{
				UnityEngine.Object.Destroy(texture2D);
			}
		}

		private IEnumerator GodClip()
		{
			Mod.NoDive = true;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Mod.LeaveFlyForTest();
			yield return CivilTest.Wait(1.5f);
			Reinforce(35, 30);
			Mod.Settings.GodView = true;
			Vector2 f = Front();
			for (float t = 0f; t < 2f; t += Time.unscaledDeltaTime)
			{
				ShowreelTest.Hold(f, 28f);
				yield return null;
			}
			string dir = Clip("god");
			Time.captureFramerate = 30;
			int frames = 240;
			for (int k = 0; k < frames; k++)
			{
				float t2 = (float)k / (float)(frames - 1);
				f = Vector2.Lerp(f, Front(), 0.05f);
				ShowreelTest.Hold(f, Mathf.Lerp(28f, 10f, Mathf.SmoothStep(0f, 1f, t2)));
				yield return new WaitForEndOfFrame();
				Frame(dir, k);
				yield return null;
			}
			Time.captureFramerate = 0;
			Note($"god view: {frames} frames, {Alive(_men)} men and {Alive(_orcs)} orcs standing");
		}

		private IEnumerator FlyClip()
		{
			Mod.NoDive = true;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Mod.Settings.GodView = true;
			for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
			{
				ShowreelTest.Hold(_centre, 28f);
				yield return null;
			}
			Mod.StartFlyForTest();
			yield return CivilTest.Wait(1f);
			if (Check("free flight", Mod.Flying))
			{
				string dir = Clip("flyover");
				Time.captureFramerate = 30;
				int frames = 360;
				Vector2 f = Front();
				Vector2 from = f - _dir2 * 26f + _side * 12f;
				Vector2 to = f - _dir2 * 7f + _side * 5f;
				for (int k = 0; k < frames; k++)
				{
					float t2 = Mathf.SmoothStep(0f, 1f, (float)k / (float)(frames - 1));
					f = Vector2.Lerp(f, Front(), 0.03f);
					Vector2 vector = Vector2.Lerp(from, to, t2);
					float num = Ground(vector.x, vector.y) + Mathf.Lerp(16f, 3f, t2);
					Vector2 vector2 = f - vector;
					float pitchDegrees = (0f - Mathf.Atan2(num - Ground(f.x, f.y) - 0.6f, Mathf.Max(1f, vector2.magnitude))) * 57.29578f;
					Mod.PlaceFlyForTest(new Vector3(vector.x, vector.y, num), Mathf.Atan2(vector2.y, vector2.x), pitchDegrees);
					yield return new WaitForEndOfFrame();
					Frame(dir, k);
					yield return null;
				}
				Time.captureFramerate = 0;
				Note($"flyover: {frames} frames, {Alive(_men)} men and {Alive(_orcs)} orcs standing");
			}
		}

		private IEnumerator LowClip()
		{
			Reinforce(35, 30);
			Mod.Settings.GodView = true;
			for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
			{
				ShowreelTest.Hold(Front(), 20f);
				yield return null;
			}
			Mod.StartFlyForTest();
			Hour(2);
			yield return CivilTest.Wait(1.5f);
			string dir = Clip("low");
			Time.captureFramerate = 30;
			int frames = 300;
			Vector2 f = Front();
			float side = 1f;
			int num = 0;
			int num2 = 0;
			for (float num3 = -7f; num3 <= 7f; num3 += 1f)
			{
				if (DryAt(f + _side * 7f + _dir2 * num3))
				{
					num++;
				}
				if (DryAt(f - _side * 7f + _dir2 * num3))
				{
					num2++;
				}
			}
			if (num2 > num)
			{
				side = -1f;
			}
			for (int k = 0; k < frames; k++)
			{
				float t2 = (float)k / (float)(frames - 1);
				f = Vector2.Lerp(f, Front(), 0.03f);
				Vector2 vector = f + _side * (7f * side) + _dir2 * Mathf.Lerp(-6f, 6f, t2);
				float num4 = Ground(f.x, f.y);
				float num5 = Mathf.Max(Ground(vector.x, vector.y), num4) + 1.7f;
				Vector2 vector2 = f - vector;
				float pitchDegrees = (0f - Mathf.Atan2(num5 - num4 - 0.7f, Mathf.Max(1f, vector2.magnitude))) * 57.29578f;
				Mod.PlaceFlyForTest(new Vector3(vector.x, vector.y, num5), Mathf.Atan2(vector2.y, vector2.x), pitchDegrees);
				yield return new WaitForEndOfFrame();
				Frame(dir, k);
				yield return null;
			}
			Time.captureFramerate = 0;
			Note($"low pass: {frames} frames, {Alive(_men)} men and {Alive(_orcs)} orcs standing");
			Hour(1);
			Mod.LeaveFlyForTest();
			yield return CivilTest.Wait(2f);
		}

		private IEnumerator MeleeClip()
		{
			Reinforce(35, 30);
			Actor you = null;
			float num = float.MaxValue;
			Vector2 b = Front();
			foreach (Actor man in _men)
			{
				if (man != null && man.isAlive() && man.hasMeleeAttack())
				{
					float num2 = Vector2.Distance(man.current_position, b);
					if (num2 < num)
					{
						num = num2;
						you = man;
					}
				}
			}
			if (!Check("a soldier to fight as", you != null))
			{
				yield break;
			}
			try
			{
				((BaseSimObject)you).addStatusEffect("invincible", 120f, pColorEffect: false);
			}
			catch
			{
			}
			Mod.Settings.GodView = false;
			yield return Possess(you);
			if (!Check("in his eyes", InControl(you)))
			{
				yield break;
			}
			yield return CivilTest.Wait(1.5f);
			string dir = Clip("melee");
			Time.captureFramerate = 30;
			int frames = 420;
			int swings = 0;
			float nextSwing = 0f;
			for (int k = 0; k < frames; k++)
			{
				if (!you.isAlive())
				{
					break;
				}
				Actor actor = null;
				float num3 = float.MaxValue;
				foreach (Actor orc in _orcs)
				{
					if (orc != null && orc.isAlive())
					{
						float num4 = Vector2.Distance(orc.current_position, you.current_position);
						if (num4 < num3)
						{
							num3 = num4;
							actor = orc;
						}
					}
				}
				if (actor != null)
				{
					Vector2 vector = actor.current_position - you.current_position;
					float num5 = Mathf.Atan2(vector.y, vector.x);
					Mod.SetYaw(Mathf.LerpAngle(Mod.ViewYaw * 57.29578f, num5 * 57.29578f, 0.15f) * (MathF.PI / 180f));
					Mod.SetPitchDegrees(-4f);
					PossessionHooks.ScriptedInput = ((num3 > 1.3f) ? new Vector2(0f, 1f) : Vector2.zero);
					float num6 = (float)k / 30f;
					if (num3 < 2.2f && num6 >= nextSwing)
					{
						PossessionCombat.AttackForTest(you);
						swings++;
						nextSwing = num6 + 0.9f;
					}
				}
				else
				{
					PossessionHooks.ScriptedInput = Vector2.zero;
				}
				Mod.HudForTest.CaptureHudForTest(Path.Combine(dir, $"f_{k:0000}.png"));
				yield return null;
				for (int i = 0; i < 4; i++)
				{
					if (!Mod.HudForTest.CapturePending)
					{
						break;
					}
					yield return null;
				}
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			Time.captureFramerate = 0;
			Note($"melee: {swings} swings, {Alive(_men)} men and {Alive(_orcs)} orcs standing");
		}
	}
}
